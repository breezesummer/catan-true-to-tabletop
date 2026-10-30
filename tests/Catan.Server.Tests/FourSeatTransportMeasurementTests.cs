using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Catan.Network.Contracts;
using Catan.Rooms;
using Catan.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using M5=Catan.Core.M5;

namespace Catan.Server.Tests;

public sealed class FourSeatTransportMeasurementTests(ITestOutputHelper output)
{
    const string RuleSet="combined";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category","NetworkMeasurement")]
    public async Task FourLiveSocketsReduceIdleTrafficAndStillReceiveChangedBoardWithoutPolling(bool fishing)
    {
        using var rooms=new RoomDirectory(new MemoryStorage(),Scenario());
        var transport=new NetworkTransport(rooms,new ServerOptions());
        var builder=WebApplication.CreateBuilder();builder.Logging.ClearProviders();builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app=builder.Build();app.UseWebSockets();app.Map("/ws",transport.WebSocketAsync);
        await app.StartAsync();
        var address=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var peers=new List<MeasuredPeer>();
        try
        {
            for(int i=0;i<4;i++)
            {
                var peer=new MeasuredPeer(fishing);peers.Add(peer);
                await peer.Connect(new Uri(address.Replace("http://","ws://")+"/ws"),deadline.Token);
                var enrolled=await peer.Enroll(i==0?"Create":"Join",i==0?null:peers[0].Latest.RoomId,deadline.Token);
                Assert.True(enrolled.Success,enrolled.Code);
            }
            foreach(var peer in peers)
            {
                await peer.Request("Get",null,deadline.Token);
                Assert.True((await peer.Request("Ready",new ReadyPayload{Ready=true},deadline.Token)).Success);
            }
            await peers[0].Request("Get",null,deadline.Token);
            Assert.True((await peers[0].Request("Start",null,deadline.Token)).Success);
            foreach(var peer in peers)Assert.NotNull((await peer.Request("Get",null,deadline.Token)).ViewJson);
            await Task.Delay(1200,deadline.Token); // 初次推送不计入静止窗口。

            // 同一服务、同一棋盘、四条真实本机 WebSocket，仅切换 Get 节奏；不冒充四设备或旧服务二进制对比。
            var legacy=await Measure(peers,TimeSpan.FromSeconds(1),TimeSpan.FromSeconds(21),deadline.Token);
            var current=await Measure(peers,TimeSpan.FromSeconds(10),TimeSpan.FromSeconds(21),deadline.Token);
            Assert.True(legacy.Bytes>0);Assert.True(current.Bytes<legacy.Bytes*.5);
            Assert.All(legacy.Seats,seat=>Assert.True(seat.Messages>=15));
            Assert.All(current.Seats,seat=>Assert.True(seat.Messages>=2));

            // 停止周期 Get 后提交一个合法开局村庄；其余三席只靠 750ms 推送获知棋盘改变。
            var actor=peers.Single(peer=>WireJson.Deserialize<M5.PlayerView>(peer.Latest.ViewJson).ActivePlayerId==peer.Latest.SeatId);
            var before=WireJson.Deserialize<M5.PlayerView>(actor.Latest.ViewJson);
            Assert.Equal(M5.GamePhase.SetupSettlement,before.Phase);
            long revision=actor.Latest.Revision+1;
            var watchers=peers.Where(peer=>peer!=actor).Select(peer=>peer.WatchPush(revision)).ToArray();
            long changedAt=Stopwatch.GetTimestamp();
            var result=await actor.Request("Command",new M5.Command
            {Id=Guid.NewGuid().ToString("N"),PlayerId=actor.Latest.SeatId,Kind=M5.CommandKind.SetupSettlement,TargetId=before.LegalVertexIds[0]},deadline.Token);
            Assert.True(result.Success,result.Code);
            var arrivals=await Task.WhenAll(watchers).WaitAsync(TimeSpan.FromSeconds(5),deadline.Token);
            var delays=arrivals.Select(ticks=>Stopwatch.GetElapsedTime(changedAt,ticks).TotalMilliseconds).ToArray();
            Assert.All(peers,peer=>Assert.Contains(WireJson.Deserialize<M5.PlayerView>(peer.Latest.ViewJson).Board.Settlements,
                piece=>piece.LocationId==before.LegalVertexIds[0]&&piece.PlayerId==before.PlayerId));

            var report=new
            {
                measuredUtc=DateTimeOffset.UtcNow,scope="同机四条真实 WebSocket；相同新服务与静止棋盘；不是四台设备或历史服务回放",
                fishing=fishing,ruleSet=RuleSet,scenario="heading-for-new-shores",players=4,
                legacyOneSecond=legacy,currentTenSeconds=current,
                receiveByteReduction=1-current.Bytes/(double)legacy.Bytes,boardPushDelayMs=delays
            };
            string json=JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true});output.WriteLine(json);
            string? path=Environment.GetEnvironmentVariable("CATAN_TRANSPORT_MEASUREMENT_OUTPUT");
            if(!string.IsNullOrWhiteSpace(path)){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);await File.WriteAllTextAsync(path+(fishing?".fishing.json":".baseline.json"),json,deadline.Token);}
        }
        finally
        {
            foreach(var peer in peers)await peer.DisposeAsync();
            await app.StopAsync();
        }
    }

    static async Task<Measurement> Measure(List<MeasuredPeer> peers,TimeSpan interval,TimeSpan duration,CancellationToken cancellation)
    {
        foreach(var peer in peers)peer.StartMeasurement();
        using var window=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        long started=Stopwatch.GetTimestamp();
        var loops=peers.Select(async peer=>
        {
            try
            {
                while(!window.IsCancellationRequested)
                {
                    Assert.True((await peer.Request("Get",null,cancellation)).Success);
                    await Task.Delay(interval,window.Token);
                }
            }
            catch(OperationCanceledException) when(window.IsCancellationRequested) { }
        }).ToArray();
        await Task.Delay(duration,cancellation);window.Cancel();
        await Task.WhenAll(loops);
        var seats=peers.Select(peer=>peer.StopMeasurement()).ToArray();
        return new(interval.TotalSeconds,Stopwatch.GetElapsedTime(started).TotalSeconds,seats.Sum(s=>s.Bytes),seats.Sum(s=>s.Messages),seats);
    }

    sealed record SeatMeasurement(string SeatId,long Bytes,int Messages,int Pushes);
    sealed record Measurement(double GetIntervalSeconds,double WindowSeconds,long Bytes,int Messages,SeatMeasurement[] Seats);
    sealed class MeasuredPeer(bool fishing):IAsyncDisposable
    {
        readonly ClientWebSocket socket=new();
        readonly CancellationTokenSource stopped=new();
        readonly ConcurrentDictionary<string,TaskCompletionSource<NetworkResponse>> pending=new();
        readonly object gate=new();
        readonly string credential=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        Task? receiver;
        NetworkResponse latest=new();
        bool measuring;long bytes;int messages,pushes;
        long watchedRevision;TaskCompletionSource<long>? pushWatch;
        internal NetworkResponse Latest=>Volatile.Read(ref latest);
        internal async Task Connect(Uri address,CancellationToken cancellation)
        {await socket.ConnectAsync(address,cancellation);receiver=Receive();}
        internal Task<NetworkResponse> Enroll(string operation,string? room,CancellationToken cancellation)=>Exchange(new()
        {
            Operation=operation,RoomId=room,EnrollmentId=Guid.NewGuid().ToString("N"),EnrollmentCredential=credential,
            ExpectedRulesVersion=NetworkProtocol.RulesVersion(RuleSet),ExpectedScenarioVersion=NetworkProtocol.ScenarioVersion(RuleSet),
            Payload=operation=="Create"?WireJson.Serialize(new CreateRoomPayload{Settings=new RoomSettings{PlayerCount=4,RuleSet=RuleSet,ScenarioId="heading-for-new-shores",Fishing=fishing,Harbormaster=fishing,TradeModulesVersion=fishing?NetworkProtocol.TradeModulesVersion:null}}):WireJson.Serialize(new JoinRoomPayload())
        },cancellation);
        internal Task<NetworkResponse> Request(string operation,object? payload,CancellationToken cancellation)=>Exchange(new()
        {
            Operation=operation,RoomId=Latest.RoomId,Credential=credential,ConnectionId=Latest.ConnectionId,RuleSet=RuleSet,
            ExpectedRevision=Latest.Revision,ExpectedRulesVersion=NetworkProtocol.RulesVersion(RuleSet),ExpectedScenarioVersion=NetworkProtocol.ScenarioVersion(RuleSet),
            CommandId=payload is M5.Command command?command.Id:null,Payload=payload==null?null:WireJson.SerializeObject(payload)
        },cancellation);
        async Task<NetworkResponse> Exchange(NetworkRequest request,CancellationToken cancellation)
        {
            request.SupportedTradeModulesVersion=NetworkProtocol.TradeModulesVersion;
            request.RequestId=Guid.NewGuid().ToString("N");
            var receipt=new TaskCompletionSource<NetworkResponse>(TaskCreationOptions.RunContinuationsAsynchronously);pending[request.RequestId]=receipt;
            await socket.SendAsync(Encoding.UTF8.GetBytes(WireJson.Serialize(request)),WebSocketMessageType.Text,true,cancellation);
            return await receipt.Task.WaitAsync(TimeSpan.FromSeconds(10),cancellation);
        }
        async Task Receive()
        {
            try
            {
                var buffer=new byte[16384];
                while(!stopped.IsCancellationRequested)
                {
                    using var message=new MemoryStream();WebSocketReceiveResult part;
                    do
                    {
                        part=await socket.ReceiveAsync(buffer,stopped.Token);
                        if(part.MessageType!=WebSocketMessageType.Text)throw new IOException("测量连接非文本响应。");
                        message.Write(buffer,0,part.Count);
                    }while(!part.EndOfMessage);
                    var response=WireJson.Deserialize<NetworkResponse>(Encoding.UTF8.GetString(message.ToArray()));
                    if(response.Room!=null&&response.Revision>=Latest.Revision)Volatile.Write(ref latest,response);
                    lock(gate)
                    {
                        if(measuring){bytes+=message.Length;messages++;if(response.Code=="Push")pushes++;}
                        if(response.Code=="Push"&&response.Revision>=watchedRevision)pushWatch?.TrySetResult(Stopwatch.GetTimestamp());
                    }
                    if(response.RequestId!=null&&pending.TryRemove(response.RequestId,out var receipt))receipt.TrySetResult(response);
                }
            }
            catch(Exception error)
            {foreach(var receipt in pending.Values)receipt.TrySetException(error);if(!stopped.IsCancellationRequested)throw;}
        }
        internal void StartMeasurement(){lock(gate){bytes=messages=pushes=0;measuring=true;}}
        internal SeatMeasurement StopMeasurement(){lock(gate){measuring=false;return new(Latest.SeatId,bytes,messages,pushes);}}
        internal Task<long> WatchPush(long revision)
        {lock(gate){watchedRevision=revision;pushWatch=new(TaskCreationOptions.RunContinuationsAsynchronously);return pushWatch.Task;}}
        public async ValueTask DisposeAsync()
        {stopped.Cancel();socket.Abort();if(receiver!=null)try{await receiver;}catch{}socket.Dispose();stopped.Dispose();}
    }
    static string Scenario()
    {
        for(var directory=new DirectoryInfo(AppContext.BaseDirectory);directory!=null;directory=directory.Parent)
        {
            var path=Path.Combine(directory.FullName,"docs","acceptance","v0.1","scenario.json");
            if(File.Exists(path))return File.ReadAllText(path);
        }
        throw new FileNotFoundException("缺少场景夹具。");
    }
    sealed class MemoryStorage:IRoomStorage
    {
        readonly Dictionary<string,string> documents=new();
        public IEnumerable<string> LoadAll()=>documents.Values.ToArray();
        public void Commit(string roomId,string privateDocument)=>documents[roomId]=privateDocument;
        public void Delete(string roomId)=>documents.Remove(roomId);
        public void Dispose() { }
    }
}
