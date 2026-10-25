using Catan.Core.ThreeKingdoms;
using Catan.Network.Contracts;
using Xunit;
using M4=Catan.Core.M4;
using M5=Catan.Core.M5;

namespace Catan.Network.Tests;

public sealed class ThreeKingdomsB5RoomTests
{
    [Theory]
    [InlineData("cities-knights",3)] [InlineData("cities-knights",4)]
    [InlineData("combined",3)] [InlineData("combined",4)]
    public void FortyNineGeneralRoomPreservesIndependentChoicesAndReconnect(string rules,int seats)
    {
        using var f=new ThreeKingdomsB4RoomTests.Fixture(rules,seats,package:GeneralCatalog.B5PackageVersion);
        var initial=f.Clients.Select(f.Snapshot).ToArray();
        Assert.All(initial,s=>Assert.Equal(GeneralCatalog.B5ContentHash,f.Tk(s).PackageContentHash));
        Assert.Equal(4*seats+1,initial.SelectMany(s=>f.Tk(s).PendingDecision.CandidateIds).Distinct().Count());
        foreach(var i in Enumerable.Range(0,seats).Reverse())
        {
            var command=f.Decide(initial[i]);var applied=f.Submit(f.Clients[i],command,initial[i].Revision);
            Assert.True(applied.Success,applied.Message);
            var retry=f.Submit(f.Clients[i],command,initial[i].Revision);Assert.True(retry.IsDuplicate);Assert.Equal(applied.Revision,retry.Revision);
        }
        var before=f.Tk(f.Snapshot(f.Clients[0]));f.Restart();var reconnect=f.Reconnect(f.Clients[0]);
        Assert.True(reconnect.Success,reconnect.Message);Assert.Equal(before.PendingDecision.CandidateIds,f.Tk(reconnect).PendingDecision.CandidateIds);
        Assert.Equal(before.PendingDecision.DecisionId,f.Tk(reconnect).PendingDecision.DecisionId);
        var bad=f.Request("Reconnect",f.Clients[1]);bad.ExpectedThreeKingdomsPackageVersion=GeneralCatalog.B4PackageVersion;bad.ExpectedThreeKingdomsContentHash=GeneralCatalog.B4ContentHash;
        Assert.Equal("VersionMismatch",f.Directory.Handle(bad).Code);
    }

    [Theory][InlineData("cities-knights")][InlineData("combined")]
    public void NewGeneralDecisionIsPrivateAndSurvivesRetryAndSave(string rules)
    {
        using var f=new ThreeKingdomsB4RoomTests.Fixture(rules,4,package:GeneralCatalog.B5PackageVersion);
        for(int step=0;step<1400;step++)
        {
            var views=f.Clients.Select(f.Snapshot).ToArray();
            var own=views.FirstOrDefault(s=>f.Tk(s).PendingDecision?.PlayerId==s.SeatId||f.Tk(s).SkillDecision?.PlayerId==s.SeatId);
            if(own==null)
            {
                dynamic v=rules=="combined"?(object)WireJson.Deserialize<M5.PlayerView>(views[0].ViewJson):WireJson.Deserialize<M4.PlayerView>(views[0].ViewJson);
                if(v.Phase.ToString()=="Finished")break;
                string actor=v.Discards.Length>0?v.Discards[0].PlayerId:v.TradeOffer?.NextResponder()??v.PendingDecision?.PlayerId??v.ActivePlayerId;
                own=views.Single(s=>s.SeatId==actor);
            }
            var command=f.Decide(own);var tk=f.Tk(own);
            bool b5= tk.SkillDecision!=null && tk.SkillActions.Any(a=>a.Command.SkillId!=null && !GeneralCatalog.B4Definitions.SelectMany(g=>g.Skills).Any(skill=>skill.Id==a.Command.SkillId));
            if(b5)
            {
                foreach(var observer in views.Where(s=>s.SeatId!=own.SeatId))Assert.Empty(f.Tk(observer).SkillActions);
                string before=f.GameSave;var applied=f.Submit(own,command,own.Revision);Assert.True(applied.Success,applied.Message);
                Assert.True(f.Submit(own,command,own.Revision).IsDuplicate);
                if(rules=="combined") {var restored=M5.CombinedGameSession.Load(before);Assert.True(restored.Execute((M5.Command)command).Success);Assert.Equal(f.GameSave,restored.Save());}
                else {var restored=M4.CitiesKnightsGameSession.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"scenario.json")),before);Assert.True(restored.Execute((M4.Command)command).Success);Assert.Equal(f.GameSave,restored.Save());}
                return;
            }
            var next=f.Submit(own,command,own.Revision);Assert.True(next.Success,$"步骤{step}：{next.Message}");
        }
        Assert.Fail("49将正常联机对局没有到达B5技能选择。");
    }
}
