using System.Reflection;
using System.Text;
using Catan.Core;
using Catan.Network.Contracts;
using Xunit;

namespace Catan.Server.Tests;

public sealed class OnlinePerformanceTests
{
    static MethodInfo Method(string name)=>typeof(M7Client).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!;
    static FieldInfo Field(string name)=>typeof(M7Client).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!;
    static GameHistoryEntry Entry(string json)=>WireJson.Deserialize<GameHistoryEntry>(json);
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task ReceiptAndStateMayArriveInEitherOrderButMissingEventsKeepTheCommandPending(bool stateFirst)
    {
        var client=new M7Client();Field("connectionId").SetValue(client,"连接1");Field("automaticReconnect").SetValue(client,false);
        var decoder=(M7ResponseDecoder)Field("responseDecoder").GetValue(client)!;
        async Task State(long revision,bool includeHistory)
        {
            var response=new NetworkResponse{Success=true,RoomId="room",Revision=revision,HumanControlAvailable=true,Room=new RoomSummary{Started=true,GameNumber=1,RuleSet="base"},RuleSet="base",ViewJson="{\"PlayerId\":\"P1\",\"Events\":[{\"Sequence\":"+revision+"}]}",PresentationHistoryJson=includeHistory?"[{\"Sequence\":"+revision+"}]":null};
            response=await decoder.Decode(Encoding.UTF8.GetBytes(WireJson.Serialize(response)),"base","Get","state",CancellationToken.None);
            Method("Apply").Invoke(client,new object[]{response,false,false});
        }
        try
        {
            await State(1,true);Assert.True(client.CanInteract);
            Field("pendingCommand").SetValue(client,new NetworkRequest{CommandId="唯一编号"});
            if(stateFirst)await State(2,true);
            var receipt=new NetworkResponse{Success=true,Revision=2,CommittedRevision=2,RoomId="room",HumanControlAvailable=true,Room=new RoomSummary{Started=true,GameNumber=1,RuleSet="base"}};
            Field("pendingReceipt").SetValue(client,receipt);Method("Apply").Invoke(client,new object[]{receipt,false,false});
            if(!stateFirst)
            {
                Assert.True(client.CommandPending);Assert.False(client.CanInteract);await State(2,false);
                Assert.True(client.CommandPending);Assert.False(client.CanInteract);
                Method("MergeHistory").Invoke(client,new object[]{new[]{Entry("{\"Sequence\":2}")}});
            }
            Assert.False(client.CommandPending);Assert.True(client.CanInteract);
        }
        finally{client.EndSession();}
    }
    [Fact]
    public async Task OldServerFallbackKeepsTheOriginalCommandIdentity()
    {
        var client=new M7Client();client.ConfigureEndpoint("http://127.0.0.1:47771","");
        var seen=new List<NetworkRequest>();
        var handler=new FallbackHandler(seen);
        ((HttpClient)Field("http").GetValue(client)!).Dispose();Field("http").SetValue(client,new HttpClient(handler));
        try
        {
            var request=new NetworkRequest{Operation="CommandReceipt",CommandId="同一命令",RequestId="同一请求",Payload="{}"};
            var response=await (Task<NetworkResponse>)Method("Send").Invoke(client,new object[]{request,CancellationToken.None})!;
            Assert.True(response.Success);Assert.Equal(new[]{"CommandReceipt","Command"},seen.Select(r=>r.Operation));Assert.All(seen,r=>Assert.Equal("同一命令",r.CommandId));
        }
        finally{client.EndSession();}
    }
    sealed class FallbackHandler(List<NetworkRequest> seen):HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message,CancellationToken cancellationToken)
        {
            var request=WireJson.Deserialize<NetworkRequest>(await message.Content!.ReadAsStringAsync(cancellationToken));seen.Add(request);
            var response=new NetworkResponse{Success=request.Operation=="Command",Code=request.Operation=="Command"?null:"UnknownOperation",RequestId=request.RequestId};
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new StringContent(WireJson.Serialize(response))};
        }
    }

    [Fact]
    public void HistoryPreservesMultiEventOrderUpdatesContentAndDoesNotInvalidateOnDuplicates()
    {
        var buffer=new M7HistoryBuffer();
        var command=Entry("{\"Sequence\":3,\"Kind\":\"BuildRoad\"}");
        var first=Entry("{\"Sequence\":3,\"SkillEventId\":\"a\",\"Action\":\"开始\"}");
        var second=Entry("{\"Sequence\":3,\"SkillEventId\":\"b\"}");
        Assert.True(buffer.Merge(new[]{command,first,second}));var original=buffer.Snapshot;
        Assert.False(buffer.Merge(new[]{Entry(WireJson.Serialize(first))}));Assert.Same(original,buffer.Snapshot);
        var update=Entry("{\"Sequence\":3,\"SkillEventId\":\"a\",\"Action\":\"结果\"}");
        Assert.True(buffer.Merge(new[]{update,Entry("{\"Sequence\":1}")}));
        Assert.Equal(new long[]{1,3,3,3},buffer.Snapshot.Select(e=>e.Sequence));
        Assert.Same(update,buffer.Snapshot[2]);Assert.Same(second,buffer.Snapshot[3]);
        buffer.Clear();Assert.Empty(buffer.Snapshot);
    }
    [Theory]
    [InlineData("base")][InlineData("seafarers")][InlineData("cities-knights")][InlineData("combined")]
    public async Task BackgroundResponseHasViewAndHistoryReadyBeforeAtomicNotification(string rules)
    {
        var client=new M7Client();
        try
        {
            var decoder=(M7ResponseDecoder)Field("responseDecoder").GetValue(client)!;
            var response=new NetworkResponse{Success=true,Revision=3,RuleSet=rules,ViewJson="{\"PlayerId\":\"P1\"}",PresentationHistoryJson="[{\"Sequence\":3,\"SkillEventId\":\"a\"}]"};
            response=await decoder.Decode(Encoding.UTF8.GetBytes(WireJson.Serialize(response)),rules,"Command","request",CancellationToken.None);
            Assert.True(decoder.TryGet(response,out var prepared));Assert.NotNull(prepared.View);
            int notified=0;
            client.ViewChanged+=()=>{notified++;Assert.Equal(3,client.History.Single().Sequence);Assert.Same(prepared.View,Field("decodedView").GetValue(client));};
            Method("Apply").Invoke(client,new object[]{response,false,false});Assert.Equal(1,notified);
            Method("Apply").Invoke(client,new object[]{new NetworkResponse{Success=true,Revision=2,Room=new RoomSummary(),ViewJson="{}"},false,false});
            Assert.Equal(3,client.Revision);Assert.Same(prepared.View,Field("decodedView").GetValue(client));
        }
        finally{client.EndSession();}
    }
    [Fact]
    public async Task FullApplicationQueueBackpressuresAndCancellationDoesNotLeakOldCallbacks()
    {
        var client=new M7Client();client.ConfigureEndpoint("http://127.0.0.1:47771","");
        try
        {
            for(int i=0;i<32;i++)await (Task)Method("EnqueueMain").Invoke(client,new object[]{(Action)(()=>{})})!;
            int old=0;var blocked=(Task)Method("EnqueueMain").Invoke(client,new object[]{(Action)(()=>old++)})!;
            Assert.False(blocked.IsCompleted);client.EndSession();await blocked.WaitAsync(TimeSpan.FromSeconds(2));
            Method("Update").Invoke(client,null);Assert.Equal(0,old);
            client.ConfigureEndpoint("http://127.0.0.1:47771","");
            int current=0;await (Task)Method("EnqueueMain").Invoke(client,new object[]{(Action)(()=>current++)})!;
            Method("Update").Invoke(client,null);Assert.Equal(1,current);
        }
        finally{client.EndSession();}
    }
}
