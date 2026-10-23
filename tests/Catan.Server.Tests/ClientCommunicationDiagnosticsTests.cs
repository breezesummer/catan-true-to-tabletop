using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Catan.Network.Contracts;
using Xunit;

namespace Catan.Server.Tests;

public sealed class ClientCommunicationDiagnosticsTests
{
    [Fact]
    public void JsonLinesAreBoundedAndCarryCorrelationWithoutArbitraryPayloadFields()
    {
        string directory=TemporaryDirectory();
        using var writer=new M7CommunicationWriter(directory,maxFileBytes:2048,maxFiles:3);
        for(int i=0;i<60;i++)writer.Record(new M7CommunicationEvent{Event="response_received",Operation="Command",RequestId="request-"+i,CommandId="command-1",RoomId="room-1",DurationMs=12.25,Bytes=120,Revision=i});
        Assert.True(writer.Flush(5000));
        string[] files=Directory.GetFiles(directory,"comm-*.jsonl");Assert.InRange(files.Length,1,3);
        foreach(string file in files)
        {
            Assert.InRange(new FileInfo(file).Length,1,2048);
            foreach(string line in ReadSharedLines(file))
            {
                using var document=JsonDocument.Parse(line);var value=document.RootElement;
                Assert.Equal(writer.SessionId,value.GetProperty("session").GetString());
                Assert.Equal("command-1",value.GetProperty("commandId").GetString());
                Assert.Equal(12.25,value.GetProperty("durationMs").GetDouble());
                Assert.EndsWith("Z",value.GetProperty("utc").GetString());
                Assert.False(value.TryGetProperty("payload",out _));Assert.False(value.TryGetProperty("credential",out _));
            }
        }
    }

    [Fact]
    public void QueueOverflowAndUnwritableDirectoryNeverEscapeIntoTheGame()
    {
        string parent=TemporaryDirectory();Directory.CreateDirectory(parent);string blocked=Path.Combine(parent,"is-a-file");File.WriteAllText(blocked,"x");
        using var writer=new M7CommunicationWriter(blocked,capacity:1);
        for(int i=0;i<10000;i++)writer.Record(new M7CommunicationEvent{Event="request_send",Count=i});
        Assert.True(writer.Flush(5000));Assert.True(writer.DroppedEvents>0);Assert.NotNull(writer.LastErrorCode);
    }

    [Fact]
    public async Task HttpLogsMetadataOnlyAndDoesNotPostTransportWorkToTheCallerContext()
    {
        M7CommunicationLog.Start(TemporaryDirectory());
        var client=new M7Client();client.ConfigureEndpoint("http://127.0.0.1:47771","");
        string requestId=Guid.NewGuid().ToString("N"),commandId=Guid.NewGuid().ToString("N");
        const string credential="绝不能写入日志的恢复凭据",password="绝不能写入日志的房间口令",hand="绝不能写入日志的私密手牌";
        var response=new NetworkResponse{RequestId=requestId,Success=true,RoomId="room-test",Revision=7,Credential=credential,ViewJson=hand};
        var context=new CountingContext();
        SetHttp(client,new HttpClient(new DelayedResponseHandler(response)));
        try
        {
            var request=new NetworkRequest{Operation="Command",RoomId="room-test",RequestId=requestId,CommandId=commandId,Credential=credential,Payload=password};
            var result=await Task.Run(()=>
            {
                SynchronizationContext.SetSynchronizationContext(context);
                return (Task<NetworkResponse>)Method("Send").Invoke(client,new object[]{request,CancellationToken.None})!;
            });
            Assert.Equal(hand,result.ViewJson);Assert.Equal(0,context.Posted);
            Assert.True(M7CommunicationLog.Flush(5000));
            string logs=string.Join("\n",Directory.GetFiles(M7CommunicationLog.DirectoryPath!,"comm-*.jsonl").SelectMany(ReadSharedLines));
            Assert.Contains(requestId,logs);Assert.Contains(commandId,logs);Assert.Contains("response_received",logs);
            Assert.DoesNotContain(credential,logs);Assert.DoesNotContain(password,logs);Assert.DoesNotContain(hand,logs);
        }
        finally{client.EndSession();}
    }

    [Fact]
    public void CallbackBacklogIsAppliedInOrderAcrossFrames()
    {
        var client=new M7Client();var applied=new List<int>();
        try
        {
            for(int i=0;i<100;i++){int index=i;Method("EnqueueMain").Invoke(client,new object[]{(Action)(()=>applied.Add(index))});}
            Method("Update").Invoke(client,null);Assert.InRange(applied.Count,1,32);
            for(int i=0;i<100&&applied.Count<100;i++)Method("Update").Invoke(client,null);
            Assert.Equal(Enumerable.Range(0,100),applied);
        }
        finally{client.EndSession();}
    }

    [Fact]
    public async Task LateHttpPollCannotOverwriteRecoveredWebSocketTransportLabel()
    {
        var client=new M7Client();client.ConfigureEndpoint("http://127.0.0.1:47771","");
        var handler=new DelayedResponseHandler(new NetworkResponse{Success=true});SetHttp(client,new HttpClient(handler));
        Field("nextLobbyPollAt").SetValue(client,float.MaxValue);Field("nextSyncAt").SetValue(client,float.MaxValue);
        try
        {
            Method("PollState").Invoke(client,null);
            Field("socketConnected").SetValue(client,true);
            typeof(M7Client).GetProperty("TransportMode")!.SetValue(client,"WS 在线同步");
            await handler.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            // 只排空回执，不用 Update 的真实socket状态检查覆盖此特定竞态夹具。
            var queue=(System.Collections.Concurrent.ConcurrentQueue<Action>)Field("main").GetValue(client)!;
            for(int i=0;i<100&&(bool)Field("syncBusy").GetValue(client)!;i++)
            {while(queue.TryDequeue(out var action))action();await Task.Delay(10);}
            Assert.False((bool)Field("syncBusy").GetValue(client)!);Assert.Equal("WS 在线同步",client.TransportMode);
        }
        finally{client.EndSession();}
    }

    static string TemporaryDirectory()=>Path.Combine(Path.GetTempPath(),"catan-communication-tests",Guid.NewGuid().ToString("N"));
    static IEnumerable<string> ReadSharedLines(string path)
    {
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
        using var reader=new StreamReader(stream);while(reader.ReadLine() is {} line)yield return line;
    }
    static MethodInfo Method(string name)=>typeof(M7Client).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!;
    static FieldInfo Field(string name)=>typeof(M7Client).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!;
    static void SetHttp(M7Client client,HttpClient replacement){((HttpClient)Field("http").GetValue(client)!).Dispose();Field("http").SetValue(client,replacement);}
    sealed class CountingContext:SynchronizationContext
    {
        internal int Posted;
        public override void Post(SendOrPostCallback callback,object? state){Interlocked.Increment(ref Posted);ThreadPool.QueueUserWorkItem(_=>callback(state));}
    }
    sealed class DelayedResponseHandler(NetworkResponse response):HttpMessageHandler
    {
        internal readonly TaskCompletionSource Completed=new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
        {
            await Task.Delay(20,cancellation).ConfigureAwait(false);Completed.TrySetResult();
            return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(WireJson.Serialize(response),Encoding.UTF8,"application/json")};
        }
    }
}
