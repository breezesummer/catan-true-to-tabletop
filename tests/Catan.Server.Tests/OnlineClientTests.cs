using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using Catan.Network.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Catan.Server.Tests;

public sealed class OnlineClientTests
{
    static string HistoryJson(params long[] sequences)=>"["+string.Join(",",sequences.Select(s=>"{\"Sequence\":"+s+",\"Kind\":\"BuildRoad\"}"))+"]";

    [Fact]
    public void SnapshotHistoryIsReadyBeforeViewNotificationAndDoesNotSkipBackfill()
    {
        var client=Client();
        try
        {
            int notified=0;
            client.ViewChanged+=()=>{notified++;Assert.Equal(notified,client.History.Last().Sequence);};
            void Apply(long revision,string history,long after=0)=>Method("Apply").Invoke(client,new object[]{
                new NetworkResponse{Success=true,Revision=revision,ViewJson="{}",PresentationHistoryJson=history,PresentationHistoryAfterSequence=after},false,false});
            Apply(1,HistoryJson(1));Apply(2,HistoryJson(1,2));
            Assert.Equal(2,notified);Assert.Equal(new long[]{1,2},client.History.Select(e=>e.Sequence));
            Assert.Equal(0L,Field("historyCursor").GetValue(client));
            Apply(2,HistoryJson(1,2));
            Assert.Equal(2,notified);Assert.Equal(2,client.History.Length);
            // 心跳／推送的同修订响应不重复解析展示记录。
            Apply(2,"无需解析的重复内容");
            // 迟到的旧快照不能让已展示记录倒退。
            Apply(1,HistoryJson(1));Assert.Equal(2,client.Revision);
        }
        finally{client.EndSession();}
    }

    [Fact]
    public void MissingHistoryRangeFallsBackToPagingAndRestartDropsOldHistory()
    {
        var client=Client();
        try
        {
            void Apply(long revision,int game,string history,long after)=>Method("Apply").Invoke(client,new object[]{
                new NetworkResponse{Success=true,Revision=revision,Room=new RoomSummary{GameNumber=game},ViewJson="{}",PresentationHistoryJson=history,PresentationHistoryAfterSequence=after},false,false});
            Apply(1,1,HistoryJson(1),0);
            Apply(2,1,HistoryJson(80,81),79);
            Assert.Equal(1,Assert.Single(client.History).Sequence);
            Apply(3,2,HistoryJson(1,2),0);
            Assert.Equal(new long[]{1,2},client.History.Select(e=>e.Sequence));
        }
        finally{client.EndSession();}
    }

    [Theory]
    [InlineData("203.0.113.10", "http://203.0.113.10:47771")]
    [InlineData(" 203.0.113.10:48888 ", "http://203.0.113.10:48888")]
    [InlineData("http://203.0.113.10:47771/", "http://203.0.113.10:47771")]
    [InlineData("[2001:db8::10]:48888", "http://[2001:db8::10]:48888")]
    [InlineData("http://[2001:db8::10]", "http://[2001:db8::10]:47771")]
    [InlineData("http://203.0.113.10:80", "http://203.0.113.10")]
    public void PublicIpAddressIsNormalizedWithoutDomainOrCertificate(string entered,string expected)
        =>Assert.Equal(expected,M7OnlineEndpoint.Normalize(entered));

    [Theory]
    [InlineData("")]
    [InlineData("game.example.com:47771")]
    [InlineData("https://203.0.113.10:47771")]
    [InlineData("http://user:secret@203.0.113.10:47771")]
    [InlineData("http://203.0.113.10:47771/api/request")]
    [InlineData("http://203.0.113.10:47771?credential=secret")]
    [InlineData("http://203.0.113.10:47771#fragment")]
    [InlineData("http://203.0.113.10:0")]
    [InlineData("http://203.0.113.10:65536")]
    public void InvalidAddressesAndSilentHttpsDowngradeAreRejected(string address)
        =>Assert.Throws<M7NetworkFailure>(()=>M7OnlineEndpoint.Normalize(address));

    [Theory]
    [InlineData("http://203.0.113.10:47771","ws://203.0.113.10:47771/ws")]
    [InlineData("https://192.168.1.2:47771","wss://192.168.1.2:47771/ws")]
    [InlineData("http://[2001:db8::10]:47771","ws://[2001:db8::10]:47771/ws")]
    public void WebSocketSchemeMatchesConfiguredTransport(string endpoint,string expected)
        =>Assert.Equal(expected,M7OnlineEndpoint.WebSocketUri(endpoint).AbsoluteUri);

    [Fact]
    public async Task EmptyOnlineLobbyAndStoppedServerHaveDistinctStatesAndRecoverAutomatically()
    {
        await using var first=Server("http://127.0.0.1:0",_=>Task.FromResult(List("first")));
        await first.StartAsync();string endpoint=Address(first);
        var client=Client();
        try
        {
            Assert.Equal(M7LobbyConnectionState.Unconfigured,client.LobbyConnectionState);
            client.ConfigureEndpoint(endpoint,"");Assert.Equal(M7LobbyConnectionState.Connecting,client.LobbyConnectionState);
            client.ListRooms();await PumpUntil(client,()=>!client.Busy);
            Assert.Equal(M7LobbyConnectionState.Online,client.LobbyConnectionState);
            Assert.Single(client.Rooms);Assert.False(client.Connected);
            await first.StopAsync();
            client.ListRooms();await PumpUntil(client,()=>!client.Busy);
            Assert.Equal(M7LobbyConnectionState.Offline,client.LobbyConnectionState);Assert.Empty(client.Rooms);
            await using var restarted=Server(endpoint,_=>Task.FromResult(List()));await restarted.StartAsync();
            // 将调度点设为已到期，验证正式 Update 的离线重试路径。
            Field("nextLobbyPollAt").SetValue(client,0f);
            await PumpUntil(client,()=>client.LobbyConnectionState==M7LobbyConnectionState.Online);
            Assert.Empty(client.Rooms);Assert.False(client.Busy);
        }
        finally {client.EndSession();}
    }

    [Fact]
    public async Task ChangingServerDiscardsOldRequestsAndOldRoomLists()
    {
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var old=Server("http://127.0.0.1:0",async _=>{entered.TrySetResult();await release.Task;return List("old");});
        await using var current=Server("http://127.0.0.1:0",_=>Task.FromResult(List("new")));
        await old.StartAsync();await current.StartAsync();var client=Client();
        try
        {
            client.ConfigureEndpoint(Address(old),"");client.ListRooms();await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            client.ConfigureEndpoint(Address(current),"");client.ListRooms();
            await PumpUntil(client,()=>!client.Busy);release.TrySetResult();
            await PumpUntil(client,()=>client.Rooms.Length==1);await Task.Delay(50);Tick(client);
            Assert.Equal(Address(current),client.Endpoint);Assert.Equal("new",Assert.Single(client.Rooms).RoomId);
            Assert.Equal(M7LobbyConnectionState.Online,client.LobbyConnectionState);
        }
        finally {release.TrySetResult();client.EndSession();}
    }

    [Fact]
    public async Task LobbyListDoesNotConfirmAnOldSeatAndIsIndependentOfSeatReconnectEpoch()
    {
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var app=Server("http://127.0.0.1:0",async _=>{entered.TrySetResult();await release.Task;return List("visible");});
        await app.StartAsync();var client=Client();
        try
        {
            client.ConfigureEndpoint(Address(app),"");Field("connectionId").SetValue(client,"old-seat-connection");
            Field("nextSyncAt").SetValue(client,float.MaxValue);
            client.ListRooms();await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Field("connectionEpoch").SetValue(client,(int)Field("connectionEpoch").GetValue(client)!+1);
            release.TrySetResult();await PumpUntil(client,()=>!client.Busy);
            Assert.Equal("visible",Assert.Single(client.Rooms).RoomId);Assert.False(client.Connected);
            Assert.Equal(M7LobbyConnectionState.Online,client.LobbyConnectionState);
        }
        finally {release.TrySetResult();client.EndSession();}
    }

    [Fact]
    public async Task BoundClientRefreshesLobbyAfterRestartEvenWhenFirstListPrecedesRoomPresence()
    {
        await using var first=Server("http://127.0.0.1:0",_=>Task.FromResult(List("before-restart")));
        await first.StartAsync();string endpoint=Address(first);var client=Client();
        try
        {
            client.ConfigureEndpoint(endpoint,"");client.ListRooms();await PumpUntil(client,()=>!client.Busy);
            Field("connectionId").SetValue(client,"retained-connection");
            Field("nextSyncAt").SetValue(client,float.MaxValue);
            await first.StopAsync();client.ListRooms();await PumpUntil(client,()=>!client.Busy);
            Assert.Equal(M7LobbyConnectionState.Offline,client.LobbyConnectionState);Assert.Empty(client.Rooms);
            bool roomPresent=false;int listRequests=0;
            await using var restarted=Server(endpoint,async context=>
            {
                using var reader=new StreamReader(context.Request.Body);var request=WireJson.Deserialize<NetworkRequest>(await reader.ReadToEndAsync());
                if(request.Operation=="List"){listRequests++;return roomPresent?List("restored-room"):List();}
                roomPresent=true;return new NetworkResponse{Success=true,Message="已同步。",Room=new RoomSummary{RoomId="restored-room"}};
            });
            await restarted.StartAsync();client.ListRooms();await PumpUntil(client,()=>!client.Busy);
            Assert.Equal(M7LobbyConnectionState.Online,client.LobbyConnectionState);Assert.Empty(client.Rooms);Assert.False(client.Connected);
            Field("nextSyncAt").SetValue(client,0f);Field("retryAt").SetValue(client,float.MaxValue);
            // 正式 Update 持续每秒 Get；这些同步不能推迟五秒一次的大厅刷新。
            await PumpUntil(client,()=>client.Rooms.Any(room=>room.RoomId=="restored-room"));
            Assert.True(roomPresent);Assert.True(listRequests>=2);Assert.True(client.Connected);
        }
        finally {client.EndSession();}
    }

    [Fact]
    public async Task ActualClientWebSocketUsesPlainHttpAndRoomPushDoesNotEraseLobbyList()
    {
        var received=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var push=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var app=Server("http://127.0.0.1:0",_=>Task.FromResult(List("other-room")));
        app.UseWebSockets();app.Map("/ws",async context=>
        {
            using var socket=await context.WebSockets.AcceptWebSocketAsync();
            try
            {
                var bytes=new byte[16384];await socket.ReceiveAsync(bytes,context.RequestAborted);received.TrySetResult();
                await push.Task.WaitAsync(context.RequestAborted);
                await socket.SendAsync(Encoding.UTF8.GetBytes(WireJson.Serialize(new NetworkResponse{Success=true,Room=new RoomSummary{RoomId="joined-room"}})),WebSocketMessageType.Text,true,context.RequestAborted);
                await socket.ReceiveAsync(bytes,context.RequestAborted);
            }
            catch(Exception) { }
        });
        await app.StartAsync();var client=Client();
        try
        {
            client.ConfigureEndpoint(Address(app),"");Method("StartSocket").Invoke(client,null);
            await PumpUntil(client,()=>received.Task.IsCompleted&&client.TransportMode=="WS 在线同步");
            Assert.Equal("",client.Fingerprint);Assert.DoesNotContain("WSS",client.TransportMode);
            client.ListRooms();await PumpUntil(client,()=>!client.Busy);Assert.Equal("other-room",Assert.Single(client.Rooms).RoomId);
            push.TrySetResult();await PumpUntil(client,()=>client.Room!=null);
            Assert.Equal("joined-room",client.Room.RoomId);Assert.Equal("other-room",Assert.Single(client.Rooms).RoomId);
        }
        finally {push.TrySetResult();client.EndSession();Tick(client);}
    }

    [Fact]
    public async Task SocketHeartbeatKeepsRunningWithoutUnityFramesAndUsesTenSecondSpacing()
    {
        var first=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var second=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrivals=new System.Collections.Concurrent.ConcurrentQueue<DateTime>();
        await using var app=Server("http://127.0.0.1:0",_=>Task.FromResult(List()));
        app.UseWebSockets();app.Map("/ws",async context=>
        {
            using var socket=await context.WebSockets.AcceptWebSocketAsync();var bytes=new byte[16384];
            try
            {
                while(!context.RequestAborted.IsCancellationRequested)
                {
                    var read=await socket.ReceiveAsync(bytes,context.RequestAborted);
                    if(read.MessageType==WebSocketMessageType.Close)break;
                    var request=WireJson.Deserialize<NetworkRequest>(Encoding.UTF8.GetString(bytes,0,read.Count));
                    arrivals.Enqueue(DateTime.UtcNow);
                    await socket.SendAsync(Encoding.UTF8.GetBytes(WireJson.Serialize(new NetworkResponse{RequestId=request.RequestId,Success=true})),WebSocketMessageType.Text,true,context.RequestAborted);
                    if(arrivals.Count==1)first.TrySetResult();if(arrivals.Count>=2)second.TrySetResult();
                }
            }
            catch(Exception) { }
        });
        await app.StartAsync();var client=Client();
        try
        {
            client.ConfigureEndpoint(Address(app),"");Method("StartSocket").Invoke(client,null);
            // 不调用Tick：接收解析和下一次保活都不能依赖暂停的Unity帧。
            await first.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await second.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var times=arrivals.ToArray();Assert.Equal(2,times.Length);Assert.True((times[1]-times[0]).TotalSeconds>=9);
        }
        finally{client.EndSession();}
    }

    [Fact]
    public async Task UnrelatedPushesDoNotHideAMissingHeartbeatAcknowledgement()
    {
        var closed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var app=Server("http://127.0.0.1:0",_=>Task.FromResult(List()));
        app.UseWebSockets();app.Map("/ws",async context=>
        {
            using var socket=await context.WebSockets.AcceptWebSocketAsync();var bytes=new byte[16384];
            try
            {
                await socket.ReceiveAsync(bytes,context.RequestAborted);
                var payload=Encoding.UTF8.GetBytes(WireJson.Serialize(new NetworkResponse{Success=true,RequestId="different-request"}));
                while(!context.RequestAborted.IsCancellationRequested)
                {await socket.SendAsync(payload,WebSocketMessageType.Text,true,context.RequestAborted);await Task.Delay(100,context.RequestAborted);}
            }
            catch(Exception){closed.TrySetResult();}
        });
        await app.StartAsync();var client=Client();
        try
        {
            client.ConfigureEndpoint(Address(app),"");Method("StartSocket").Invoke(client,null);
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(18));
            var socket=(ClientWebSocket)Field("socket").GetValue(client)!;Assert.NotEqual(WebSocketState.Open,socket.State);
        }
        finally{client.EndSession();}
    }

    [Fact]
    public async Task FailedSocketFallsBackToHttpAndAutomaticallyRestoresPushWithSameSeatConnection()
    {
        int sockets=0;var operations=new System.Collections.Concurrent.ConcurrentQueue<string>();
        NetworkResponse State(string? request=null)=>new(){Success=true,RequestId=request,RoomId="room",ConnectionId="same-connection",SeatId="p1",Room=new RoomSummary{RoomId="room",RuleSet="base"}};
        await using var app=Server("http://127.0.0.1:0",async context=>
        {
            using var reader=new StreamReader(context.Request.Body);var request=WireJson.Deserialize<NetworkRequest>(await reader.ReadToEndAsync());operations.Enqueue(request.Operation);return State(request.RequestId);
        });
        app.UseWebSockets();app.Map("/ws",async context=>
        {
            using var socket=await context.WebSockets.AcceptWebSocketAsync();int number=Interlocked.Increment(ref sockets);var bytes=new byte[16384];
            try
            {
                var read=await socket.ReceiveAsync(bytes,context.RequestAborted);
                if(number==1){socket.Abort();return;}
                var request=WireJson.Deserialize<NetworkRequest>(Encoding.UTF8.GetString(bytes,0,read.Count));
                await socket.SendAsync(Encoding.UTF8.GetBytes(WireJson.Serialize(State(request.RequestId))),WebSocketMessageType.Text,true,context.RequestAborted);
                await socket.ReceiveAsync(bytes,context.RequestAborted);
            }
            catch(Exception) { }
        });
        await app.StartAsync();var client=Client();
        try
        {
            client.ConfigureEndpoint(Address(app),"");Method("Apply").Invoke(client,new object[]{State(),true,false});client.Playing=true;
            int epoch=(int)Field("connectionEpoch").GetValue(client)!;Method("StartSocket").Invoke(client,null);
            await PumpUntil(client,()=>operations.Contains("Get")&&client.TransportMode.Contains("HTTP"));
            await PumpUntil(client,()=>sockets>=2&&client.TransportMode=="WS 在线同步");
            Assert.True(client.Connected);Assert.Equal(epoch,Field("connectionEpoch").GetValue(client));
            Assert.Equal("same-connection",Field("connectionId").GetValue(client));Assert.DoesNotContain("Reconnect",operations);
        }
        finally{client.EndSession();}
    }

    [Fact]
    public async Task HttpReconnectReplacesAnInFlightSocketHandshakeWithoutApplyingOldIdentityRejection()
    {
        var oldEntered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOld=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var newIdentity=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        int sockets=0;
        NetworkResponse State(string connection,string? request=null)=>new(){Success=true,RequestId=request,RoomId="room",Credential="private-test-credential",ConnectionId=connection,SeatId="p1",Room=new RoomSummary{RoomId="room",RuleSet="base"}};
        await using var app=Server("http://127.0.0.1:0",async context=>
        {
            using var reader=new StreamReader(context.Request.Body);var request=WireJson.Deserialize<NetworkRequest>(await reader.ReadToEndAsync());
            return State("new-connection",request.RequestId);
        });
        app.UseWebSockets();app.Map("/ws",async context=>
        {
            int number=Interlocked.Increment(ref sockets);
            try
            {
                // 第一次 WS 握手仍挂起时，HTTP Reconnect 会改变 connectionId。
                if(number==1){oldEntered.TrySetResult();await releaseOld.Task.WaitAsync(context.RequestAborted);}
                using var socket=await context.WebSockets.AcceptWebSocketAsync();var bytes=new byte[16384];
                var read=await socket.ReceiveAsync(bytes,context.RequestAborted);
                var request=WireJson.Deserialize<NetworkRequest>(Encoding.UTF8.GetString(bytes,0,read.Count));
                var response=request.ConnectionId=="new-connection"?State("new-connection",request.RequestId):new NetworkResponse{RequestId=request.RequestId,Code="ConnectionSuperseded",Success=false};
                await socket.SendAsync(Encoding.UTF8.GetBytes(WireJson.Serialize(response)),WebSocketMessageType.Text,true,context.RequestAborted);
                if(number>1)newIdentity.TrySetResult(request.ConnectionId);
                await socket.ReceiveAsync(bytes,context.RequestAborted);
            }
            catch(Exception) { }
        });
        await app.StartAsync();var client=Client();
        try
        {
            client.ConfigureEndpoint(Address(app),"");Method("Apply").Invoke(client,new object[]{State("old-connection"),true,false});client.Playing=true;
            Field("nextSyncAt").SetValue(client,float.MaxValue);Method("StartSocket").Invoke(client,null);
            await oldEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.True((bool)Field("socketStarting").GetValue(client)!);
            client.Reconnect();
            await PumpUntil(client,()=>!client.Busy&&Field("connectionId").GetValue(client)?.ToString()=="new-connection");
            // 旧握手尚未释放，新的 WS 必须已经启动，不能等旧连接的12秒超时。
            await PumpUntil(client,()=>newIdentity.Task.IsCompleted&&client.TransportMode=="WS 在线同步");
            Assert.Equal("new-connection",await newIdentity.Task);Assert.Equal(2,sockets);
            releaseOld.TrySetResult();await Task.Delay(100);Tick(client);
            Assert.True(client.Connected);Assert.False(client.ControlLost);Assert.Equal("new-connection",Field("connectionId").GetValue(client));
        }
        finally{releaseOld.TrySetResult();client.EndSession();}
    }

    [Fact]
    public void LanStillRequiresHttpsWithItsPinnedCertificate()
    {
        var client=Client();
        try
        {
            string pin=new('a',64);
            client.ConfigureEndpoint("https://127.0.0.1:47771",pin,false);Assert.Equal(pin,client.Fingerprint);
            Assert.Throws<M7NetworkFailure>(()=>client.ConfigureEndpoint("http://127.0.0.1:47771",pin,false));
            Assert.Equal("https://127.0.0.1:47771",client.Endpoint);
        }
        finally {client.EndSession();}
    }

    [Theory]
    [InlineData(Catan.Core.ThreeKingdoms.GeneralCatalog.B4PackageVersion,true)]
    [InlineData(Catan.Core.ThreeKingdoms.GeneralCatalog.B45PackageVersion,true)]
    [InlineData(Catan.Core.ThreeKingdoms.GeneralCatalog.B4PackageVersion,false)]
    [InlineData(Catan.Core.ThreeKingdoms.GeneralCatalog.B45PackageVersion,false)]
    [InlineData(Catan.Core.ThreeKingdoms.GeneralCatalog.B5PackageVersion,true)]
    [InlineData(Catan.Core.ThreeKingdoms.GeneralCatalog.B5PackageVersion,false)]
    [InlineData(Catan.Core.ThreeKingdoms.GeneralCatalog.B5DirectPackageVersion,true)]
    [InlineData(Catan.Core.ThreeKingdoms.GeneralCatalog.B5DirectPackageVersion,false)]
    public async Task JoinDescriptionAcceptsMatchingB4AndB45PackagesAndRejectsWrongHashes(string package,bool matching)
    {
        string hash=package==Catan.Core.ThreeKingdoms.GeneralCatalog.B5DirectPackageVersion?Catan.Core.ThreeKingdoms.GeneralCatalog.B5DirectContentHash:package==Catan.Core.ThreeKingdoms.GeneralCatalog.B5PackageVersion?Catan.Core.ThreeKingdoms.GeneralCatalog.B5ContentHash:package==Catan.Core.ThreeKingdoms.GeneralCatalog.B45PackageVersion?Catan.Core.ThreeKingdoms.GeneralCatalog.B45ContentHash:Catan.Core.ThreeKingdoms.GeneralCatalog.B4ContentHash;
        if(!matching)hash="错误内容哈希";
        NetworkRequest? joined=null;
        await using var server=Server("http://127.0.0.1:0",async context=>
        {
            var request=WireJson.Deserialize<NetworkRequest>(await new StreamReader(context.Request.Body).ReadToEndAsync());
            if(request.Operation=="Describe")return new NetworkResponse{Success=true,Room=new RoomSummary{RoomId="B45COMPAT",RuleSet="combined",RulesVersion=NetworkProtocol.RulesVersion("combined"),ScenarioVersion=NetworkProtocol.ScenarioVersion("combined"),ThreeKingdomsPackageVersion=package,ThreeKingdomsContentHash=hash}};
            if(request.Operation=="Join")joined=request;
            return new NetworkResponse{Success=false,Code="InvalidRequest",Message="测试服务已接收握手"};
        });
        await server.StartAsync();var client=Client();
        try
        {
            client.ConfigureEndpoint(Address(server),"");client.Join("B45COMPAT","测试玩家","");
            try{await PumpUntil(client,()=>Field("rememberedGeneralPackage").GetValue(client) is string||client.Status.Contains("内容不一致"));}
            catch(OperationCanceledException){throw new InvalidOperationException("客户端握手未完成："+client.Status+" / "+client.LastError);}
            if(matching)
            {
                Assert.Equal(package,Field("rememberedGeneralPackage").GetValue(client));Assert.Equal(hash,Field("rememberedGeneralHash").GetValue(client));
                // macOS单元测试没有Windows/Android安全凭据保管；此例验证真实Describe入口的包协商，房间入席另由网络测试覆盖。
                if(joined!=null){Assert.Equal(package,joined.ExpectedThreeKingdomsPackageVersion);Assert.Equal(hash,joined.ExpectedThreeKingdomsContentHash);}
            }
            else {Assert.Null(joined);Assert.Contains("内容不一致",client.Status);}
        }
        finally {client.EndSession();}
    }

    static M7Client Client()
    {
        var client=new M7Client();client.UseCredentialDirectory(Path.Combine(Path.GetTempPath(),"catan-online-client-tests",Guid.NewGuid().ToString("N")));return client;
    }
    static NetworkResponse List(params string[] ids)=>new(){Success=true,Rooms=ids.Select(id=>new RoomSummary{RoomId=id}).ToArray()};
    static WebApplication Server(string address,Func<HttpContext,Task<NetworkResponse>> respond)
    {
        var builder=WebApplication.CreateBuilder();builder.Logging.ClearProviders();builder.WebHost.UseUrls(address);
        var app=builder.Build();app.MapPost("/api/request",async context=>await context.Response.WriteAsync(WireJson.Serialize(await respond(context))));return app;
    }
    static string Address(WebApplication app)=>app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    static MethodInfo Method(string name)=>typeof(M7Client).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!;
    static FieldInfo Field(string name)=>typeof(M7Client).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!;
    static void Tick(M7Client client)=>Method("Update").Invoke(client,null);
    static async Task PumpUntil(M7Client client,Func<bool> complete)
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        do {Tick(client);if(complete())return;await Task.Delay(10,timeout.Token);} while(true);
    }
}
