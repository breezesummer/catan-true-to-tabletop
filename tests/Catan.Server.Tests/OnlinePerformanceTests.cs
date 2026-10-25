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
