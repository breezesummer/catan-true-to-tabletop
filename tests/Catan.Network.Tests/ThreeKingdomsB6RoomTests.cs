using System.Collections;
using System.Reflection;
using Catan.Core.ThreeKingdoms;
using Catan.Network.Contracts;
using Catan.Rooms;
using Xunit;
using M4=Catan.Core.M4;
using M5=Catan.Core.M5;

namespace Catan.Network.Tests;

public sealed class ThreeKingdomsB6RoomTests
{
    [Theory][InlineData("cities-knights",3)][InlineData("cities-knights",4)][InlineData("combined",3)][InlineData("combined",4)]
    public void FullPackageIndependentSelectionsRetryAndReconnectUseSameIdentity(string rules,int seats)
    {
        using var f=new ThreeKingdomsB4RoomTests.Fixture(rules,seats,package:GeneralCatalog.B6PackageVersion);
        var initial=f.Clients.Select(f.Snapshot).ToArray();Assert.All(initial,v=>Assert.Equal(GeneralCatalog.B6ContentHash,f.Tk(v).PackageContentHash));
        Assert.Equal(54,GeneralCatalog.B6Definitions.Count());Assert.Equal(4*seats+1,initial.SelectMany(v=>f.Tk(v).PendingDecision.CandidateIds).Distinct().Count());
        for(int i=seats-1;i>=0;i--)
        {
            var c=f.Decide(initial[i]);var applied=f.Submit(initial[i],c,initial[i].Revision);Assert.True(applied.Success,applied.Message);Assert.True(f.Submit(initial[i],c,initial[i].Revision).IsDuplicate);
            for(int j=0;j<i;j++)Assert.Equal(f.Tk(initial[j]).PendingDecision.CandidateIds,f.Tk(f.Snapshot(f.Clients[j])).PendingDecision.CandidateIds);
        }
        var before=f.Tk(f.Snapshot(f.Clients[0]));f.Restart();var restored=f.Reconnect(f.Clients[0]);Assert.True(restored.Success,restored.Message);Assert.Equal(before.PendingDecision.CandidateIds,f.Tk(restored).PendingDecision.CandidateIds);
        var wrong=f.Request("Reconnect",f.Clients[1]);wrong.ExpectedThreeKingdomsContentHash=GeneralCatalog.B5ContentHash;Assert.Equal("VersionMismatch",f.Directory.Handle(wrong).Code);
    }

    private static object Field(object instance,string name)=>instance.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(instance)!;
    private static void SetField(object instance,string name,object value)=>instance.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(instance,value);
    private static void AssertControlledRejoinBoundary(ThreeKingdomsB4RoomTests.Fixture f,B6ControlGrant grant)
    {
        var room=((IDictionary)Field(f.Directory,"rooms")).Values.Cast<object>().Single();var state=Field(room,"state");
        var seats=((IEnumerable)state.GetType().GetProperty("Seats")!.GetValue(state)!).Cast<object>().ToArray();
        object Seat(string id)=>seats.Single(s=>(string)s.GetType().GetProperty("SeatId")!.GetValue(s)! == id);
        void Set(object s,string name,object? value)=>s.GetType().GetProperty(name)!.SetValue(s,value);
        bool Ai(object s)=>(bool)s.GetType().GetProperty("ControlledByAi")!.GetValue(s)!;
        var controller=Seat(grant.ControllerPlayerId);var asset=Seat(grant.AssetPlayerId);
        // 两席均在上一个机会离线后重连；当前已有惑心授权，下一边界只应把控制交还实际操作者。
        foreach(var seat in new[]{controller,asset}){Set(seat,"ControlledByAi",true);Set(seat,"RejoinAfterTurnKey","prior-opportunity");}
        room.GetType().GetMethod("RestoreHumanControlAtTurnBoundary",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(room,null);
        Assert.False(Ai(controller));Assert.True(Ai(asset));
        foreach(var seat in new[]{controller,asset}){Set(seat,"ControlledByAi",false);Set(seat,"RejoinAfterTurnKey",null);}
    }
    private static void SeedControlOpening(ThreeKingdomsB4RoomTests.Fixture f,string rules)
    {
        // 测试仅替换尚未接收游戏命令的随机初始种子，后续全部经真实房间提交，可按完整历史重放。
        object? selected=null;
        for(uint seed=1;seed<500;seed++)
        {
            dynamic session=rules=="combined"?(object)M5.CombinedGameSession.Create("heading-for-new-shores",4,seed,threeKingdoms:true,threeKingdomsPackageVersion:GeneralCatalog.B6PackageVersion):M4.CitiesKnightsGameSession.Create(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"scenario.json")),4,seed,threeKingdoms:true,threeKingdomsPackageVersion:GeneralCatalog.B6PackageVersion);
            ThreeKingdomsState tk=session.GetAuthoritativeStateForTesting().ThreeKingdoms;
            if(tk.SeatPlayerIds[0]!="P2"&&ThreeKingdomsEngine.GetPendingDecision(tk,"P2").CandidateIds.Contains("tk-g04")){selected=session;break;}
        }
        Assert.NotNull(selected);var room=((IDictionary)Field(f.Directory,"rooms")).Values.Cast<object>().Single();var game=Field(room,"game");SetField(game,"session",selected!);
        string save=(string)game.GetType().GetMethod("Save",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(game,null)!;
        Field(room,"state").GetType().GetProperty("GameSave")!.SetValue(Field(room,"state"),save);
        SetField(room,"projectionRevision",-1L);
    }
    private static dynamic View(NetworkResponse r,string rules)=>rules=="combined"?(object)WireJson.Deserialize<M5.PlayerView>(r.ViewJson):WireJson.Deserialize<M4.PlayerView>(r.ViewJson);
    private static NetworkResponse Actor(ThreeKingdomsB4RoomTests.Fixture f,string rules)
    {
        var snapshots=f.Clients.Select(f.Snapshot).ToArray();
        var pending=snapshots.FirstOrDefault(r=>f.Tk(r).PendingDecision!=null&&B2SkillEngine.ControllerForViewB6(f.Tk(r),f.Tk(r).PendingDecision.PlayerId)==r.SeatId||f.Tk(r).SkillDecision!=null&&B2SkillEngine.ControllerForViewB6(f.Tk(r),f.Tk(r).SkillDecision.PlayerId)==r.SeatId);
        if(pending!=null)return pending;dynamic v=View(snapshots[0],rules);
        string asset=v.Discards.Length>0?v.Discards[0].PlayerId:v.TradeOffer?.NextResponder()??v.PendingDecision?.PlayerId??v.ActivePlayerId;
        string controller=B2SkillEngine.ControllerForViewB6(f.Tk(snapshots[0]),asset);return snapshots.Single(r=>r.SeatId==controller);
    }
    private static object Choose(ThreeKingdomsB4RoomTests.Fixture f,NetworkResponse snapshot,string rules)
    {
        var tk=f.Tk(snapshot);dynamic c=f.Decide(snapshot);
        if(tk.PendingDecision?.CandidateIds.Contains("tk-g04")==true&&snapshot.SeatId=="P2")
            c.GeneralIds=tk.PendingDecision.RequiredCount==2?new[]{"tk-g04",tk.PendingDecision.CandidateIds.First(id=>id!="tk-g04")}:new[]{"tk-g04"};
        if(tk.SkillDecision!=null&&tk.SkillActions.Any(a=>a.Command.SkillId=="tk-g04-s1"&&a.Command.Option=="accept"))c.SkillCommand=tk.SkillActions.First(a=>a.Command.SkillId=="tk-g04-s1"&&a.Command.Option=="accept").Command;
        return c;
    }

    [Theory][InlineData("cities-knights")][InlineData("combined")]
    public async Task ControlledTurnAuthenticatesRealSenderAndRestoresGrantThenRevokesIt(string rules)
    {
        using var f=new ThreeKingdomsB4RoomTests.Fixture(rules,4,package:GeneralCatalog.B6PackageVersion);SeedControlOpening(f,rules);
        for(int i=0;i<150&&f.Tk(f.Snapshot(f.Clients[1])).ControlB6==null;i++)
        {var own=Actor(f,rules);var selected=Choose(f,own,rules);var applied=f.Submit(own,selected,own.Revision);Assert.True(applied.Success,$"步骤{i}：{applied.Code}:{applied.Message}；命令{WireJson.SerializeObject(selected)}；待决{System.Text.Json.JsonSerializer.Serialize(f.Tk(own).PendingDecision)}");}
        var controller=f.Clients.Single(c=>c.SeatId=="P2");var current=f.Snapshot(controller);var grant=f.Tk(current).ControlB6;Assert.NotNull(grant);Assert.Equal("P2",grant.ControllerPlayerId);Assert.NotEqual("P2",grant.AssetPlayerId);
        AssertControlledRejoinBoundary(f,grant);
        for(int i=0;i<80;i++)
        {
            var actor=Actor(f,rules);if(actor.SeatId=="P2")break;
            var result=f.Submit(actor,Choose(f,actor,rules),actor.Revision);Assert.True(result.Success,result.Message);
        }
        current=f.Snapshot(controller);Assert.NotNull(f.Tk(current).ControlB6);
        dynamic view=View(current,rules);Assert.Equal(grant.AssetPlayerId,(string)view.PlayerId);Assert.Equal("P2",(string)view.ViewerPlayerId);
        var assetClient=f.Clients.Single(c=>c.SeatId==grant.AssetPlayerId);dynamic assetView=View(f.Snapshot(assetClient),rules);Assert.Empty((Array)assetView.LegalActions);
        dynamic command=Choose(f,current,rules);Assert.Equal("P2",(string)command.PlayerId);Assert.Equal(grant.AssetPlayerId,(string)command.ActingAsPlayerId);
        dynamic forged=rules=="combined"?(object)WireJson.Deserialize<M5.Command>(WireJson.SerializeObject(command)):WireJson.Deserialize<M4.Command>(WireJson.SerializeObject(command));forged.PlayerId=grant.AssetPlayerId;forged.Id=Guid.NewGuid().ToString("N");Assert.Equal("SeatForgery",f.Submit(current,forged,current.Revision).Code);
        var outsider=f.Snapshot(f.Clients.First(c=>c.SeatId!="P2"&&c.SeatId!=grant.AssetPlayerId));forged.PlayerId=outsider.SeatId;forged.Id=Guid.NewGuid().ToString("N");Assert.Equal("ControlPermission",f.Submit(outsider,forged,outsider.Revision).Code);
        var appliedCommand=f.Submit(current,command,current.Revision);Assert.True(appliedCommand.Success,appliedCommand.Message);string save=f.GameSave;Assert.True(f.Submit(current,command,current.Revision).IsDuplicate);Assert.Equal(save,f.GameSave);
        f.Restart();for(int i=0;i<f.Clients.Length;i++)f.Clients[i]=f.Reconnect(f.Clients[i]);controller=f.Clients.Single(c=>c.SeatId=="P2");current=f.Snapshot(controller);
        Assert.Equal(grant.AssetPlayerId,f.Tk(current).ControlB6.AssetPlayerId);Assert.Equal(grant.OpportunityId,f.Tk(current).ControlB6.OpportunityId);
        Assert.False(current.HumanControlAvailable);Assert.True(current.Room.Seats.Single(s=>s.SeatId=="P2").RejoinPending);
        dynamic interrupted=Choose(f,current,rules);interrupted.Id=Guid.NewGuid().ToString("N");Assert.Equal("AiTakeover",f.Submit(current,interrupted,current.Revision).Code);
        for(int i=0;i<280&&(f.Tk(f.Snapshot(controller)).ControlB6!=null||!f.Snapshot(controller).HumanControlAvailable);i++)
        {
            var own=Actor(f,rules);if(!own.HumanControlAvailable){Assert.Equal(1,await f.Directory.TickAiAsync());continue;}
            dynamic next=Choose(f,own,rules);dynamic v=View(own,rules);
            if((string)v.ActivePlayerId==grant.AssetPlayerId&&v.Phase.ToString()=="Action"&&v.PendingDecision==null&&f.Tk(own).SkillDecision==null)
            {if(rules=="combined")next=new M5.Command{PlayerId="P2",ActingAsPlayerId=grant.AssetPlayerId,Kind=M5.CommandKind.EndTurn,WindowId=f.Tk(own).WindowId,Id=Guid.NewGuid().ToString("N")};else next=new M4.Command{PlayerId="P2",ActingAsPlayerId=grant.AssetPlayerId,Kind=M4.CommandKind.EndTurn,WindowId=f.Tk(own).WindowId,Id=Guid.NewGuid().ToString("N")};}
            var result=f.Submit(own,next,own.Revision);Assert.True(result.Success,$"代操步骤{i}：{result.Code}:{result.Message}");
        }
        current=f.Snapshot(controller);Assert.Null(f.Tk(current).ControlB6);Assert.True(current.HumanControlAvailable);command.Id=Guid.NewGuid().ToString("N");command.WindowId=f.Tk(current).WindowId;
        Assert.Equal("ControlPermission",f.Submit(current,command,current.Revision).Code);
    }
}
