using System.Text.Json;
using Catan.Core.ThreeKingdoms;
using Xunit;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB6SeatTradeTests
{
    private sealed class Host : IThreeKingdomsB6SeatTradeHost
    {
        public ThreeKingdomsB2TestHost Inner {get;set;}=new();
        public bool Adjacent {get;set;}=true;
        public ThreeKingdomsState State=>Inner.State;
        public static Host Create(string general)
        {var h=new Host{Inner=ThreeKingdomsB2TestHost.Create(general,"tk-g12","tk-g09")};h.State.RulesVersion=GeneralCatalog.B6RulesVersion;h.State.PackageVersion=GeneralCatalog.B6PackageVersion;h.State.PackageContentHash=GeneralCatalog.B6ContentHash;return h;}
        public int[] GetHand(string p)=>Inner.GetHand(p);public int[] GetBank()=>Inner.GetBank();
        public void RemoveHand(string p,int[] c)=>Inner.RemoveHand(p,c);public void AddHand(string p,int[] c)=>Inner.AddHand(p,c);
        public void RemoveBank(int[] c)=>Inner.RemoveBank(c);public void AddBank(int[] c)=>Inner.AddBank(c);public int DrawRandom(int max)=>Inner.DrawRandom(max);
        public B2ProgressCard[] GetProgressCards(string p)=>Inner.GetProgressCards(p);public int GetProgressDeckCount(int c)=>Inner.GetProgressDeckCount(c);
        public void RemoveProgress(string p,string c)=>Inner.RemoveProgress(p,c);public B2ProgressCard DrawProgress(string p,int c)=>Inner.DrawProgress(p,c);public void ReturnProgress(B2ProgressCard c)=>Inner.ReturnProgress(c);
        public B2HarvestTile[] GetHarvestTiles(string p)=>Inner.GetHarvestTiles(p);public int FinalDiceTotal=>Inner.FinalDiceTotal;
        public bool AreTerritoriesAdjacent(string a,string b)=>Adjacent;
        public int[] JiaoziChoices(B4VirtualUse u)=>new[]{0,1};public void CompleteJiaoziConstruction(B4VirtualUse u,int t)=>throw new NotSupportedException();
        public Host Copy()=>new(){Inner=Inner.Copy(),Adjacent=Adjacent};
    }
    private static B2SkillAction Use(Host h,string option,int type=-1,string target="P2")
    {
        var action=B2SkillEngine.Actions(h,"P1").First(a=>a.Command.Option==option&&(type<0||a.Command.CardType==type)&&(a.Command.TargetPlayerIds.Length!=1||a.Command.TargetPlayerIds[0]==target));
        B2SkillEngine.Resolve(h,"P1",action.Command);return action;
    }
    [Theory][InlineData(1)][InlineData(2)]
    public void FuqiExchangesRequestedRealCardsAtomicallyAndCountsOnce(int count)
    {
        var h=Host.Create("tk-g46");h.Inner.Give("P1",1);h.Inner.Give("P2",0,0,0,0,0,1,1);
        Use(h,"seat-fuqi");Use(h,"seat-fuqi-give",0);Use(h,"seat-fuqi-request",5);if(count==2)Use(h,"seat-fuqi-request",6);
        var restored=h.Copy();var command=Use(h,"seat-fuqi-submit").Command;B2SkillEngine.Resolve(restored,"P1",command);
        Assert.Equal(h.Inner.Snapshot(),restored.Inner.Snapshot());Assert.Equal(count,h.GetHand("P1").Sum());Assert.Equal(3-count,h.GetHand("P2").Sum());Assert.Equal(1,h.State.Generals[0].Counters.Single(c=>c.Key=="fuqi").Value);h.Inner.AssertConserved();
    }
    [Fact]
    public void FuqiHiddenInsufficiencyConsumesBothAttemptsWithoutChangingCards()
    {
        var h=Host.Create("tk-g46");h.Inner.Give("P1",1);
        for(int attempt=0;attempt<2;attempt++){Use(h,"seat-fuqi");Use(h,"seat-fuqi-give",0);Use(h,"seat-fuqi-request",7);Use(h,"seat-fuqi-submit");}
        Assert.Equal(1,h.GetHand("P1")[0]);Assert.DoesNotContain(B2SkillEngine.Actions(h,"P1"),a=>a.Command.Option=="seat-fuqi");
        h.State.Clock.PhaseId++;Assert.Contains(B2SkillEngine.Actions(h,"P1"),a=>a.Command.Option=="seat-fuqi");h.Inner.AssertConserved();
    }
    [Fact]
    public void FuqiDoesNotRevealRequestedHandBeforeSubmitAndCancelDoesNotUseQuota()
    {
        var h=Host.Create("tk-g46");h.Inner.Give("P1",1);Use(h,"seat-fuqi");Use(h,"seat-fuqi-give",0);
        var first=JsonSerializer.Serialize(B2SkillEngine.Actions(h,"P1"));h.Inner.Give("P2",0,0,0,0,0,2);Assert.Equal(first,JsonSerializer.Serialize(B2SkillEngine.Actions(h,"P1")));
        Use(h,"seat-cancel");Assert.DoesNotContain(h.State.Generals[0].Counters,c=>c.Key=="fuqi");
        h.Adjacent=false;Assert.DoesNotContain(B2SkillEngine.Actions(h,"P1"),a=>a.Command.Option=="seat-fuqi");
    }
    [Theory][InlineData("P1")][InlineData("P2")]
    public void FuqiHonorsHandLossBanOnEitherParty(string banned)
    {
        var h=Host.Create("tk-g46");h.Inner.Give("P1",1);h.State.Response=new B4ResponseState();
        h.State.Skills.TimedEffects=new[]{new B2TimedEffect{Id="ban",Kind="response-indulgence-active",TargetPlayerId=banned,CreatedTurnId=h.State.Clock.TurnInstanceId}};
        Assert.True(B2SkillEngine.IsB4HandLossForbidden(h.State,banned));Assert.DoesNotContain(B2SkillEngine.Actions(h,"P1"),a=>a.Command.Option=="seat-fuqi"&&(banned=="P1"||a.Command.TargetPlayerIds.Contains(banned)));
    }
    [Fact]
    public void BaiyiSwapsSeatsButNotOwnerCursorOrCurrentTargetOrder()
    {
        var h=Host.Create("tk-g48");int cursor=h.State.Clock.NormalSeatCursor;string tenure=h.State.Generals[0].TenureId;
        Use(h,"seat-baiyi");Assert.Equal(new[]{"P2","P1","P3"},h.State.SeatPlayerIds);Assert.Equal("P1",h.State.Clock.ActivePlayerId);Assert.Equal(cursor,h.State.Clock.NormalSeatCursor);
        Assert.Equal("P1",h.State.Generals.Single(g=>g.TenureId==tenure).OwnerPlayerId);Assert.DoesNotContain(B2SkillEngine.Actions(h,"P1"),a=>a.Command.Option=="seat-baiyi");
    }
    [Fact]
    public void JinglueRevealsOnlyToOwnerLocksRealTypeAndKeepsLastTypeAcrossTurns()
    {
        var h=Host.Create("tk-g48");h.Inner.Give("P2",2,0,0,0,0,2);Use(h,"seat-jinglue");
        foreach(var viewer in new[]{"P1","P2","P3"}){var view=new ThreeKingdomsView();B2SkillEngine.ProjectSeatTradeB6(h,view,viewer);Assert.Equal(viewer=="P1",view.MilitaryHands?.Any(x=>x.PlayerId=="P2")==true);}
        Use(h,"seat-jinglue-take",5);Assert.Equal(1,h.GetHand("P1")[5]);Assert.True(B2SkillEngine.CanGainB6(h.State,"P1",5));Assert.False(B2SkillEngine.CanGainB6(h.State,"P2",5));Assert.True(B2SkillEngine.CanGainB6(h.State,"P2",0));
        h.State.Clock.PhaseId++;Use(h,"seat-jinglue");Assert.DoesNotContain(B2SkillEngine.Actions(h,"P1"),a=>a.Command.Option=="seat-jinglue-take"&&a.Command.CardType==5);Use(h,"seat-jinglue-take",0);
        Assert.False(B2SkillEngine.CanGainB6(h.State,"P3",0));h.Inner.AssertConserved();
    }
    [Fact]
    public void JinglueCannotCancelAfterPrivateRevealToInspectEveryOpponentForFree()
    {
        var h=Host.Create("tk-g48");h.Inner.Give("P2",2);Use(h,"seat-jinglue");Assert.DoesNotContain(B2SkillEngine.Actions(h,"P1"),a=>a.Command.Option=="seat-cancel");
        Use(h,"seat-jinglue-take",0);h.State.Clock.PhaseId++;Use(h,"seat-jinglue");Use(h,"seat-cancel");
        Assert.DoesNotContain(B2SkillEngine.Actions(h,"P1"),a=>a.Command.Option=="seat-jinglue");
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void JinglueSurvivesRetirementAndExpiresAtNextOpportunityIncludingSkipped(bool skipped)
    {
        var h=Host.Create("tk-g48");h.Inner.Give("P2",1);Use(h,"seat-jinglue");Use(h,"seat-jinglue-take",0);h.State.Generals[0].Region=GeneralRegion.Retired;
        Assert.False(B2SkillEngine.CanGainB6(h.State,"P2",0));B2SkillEngine.OnSeatTradeB6OpportunityStart(h,2,skipped?"P3":"P1",skipped?new[]{"P1"}:null);Assert.True(B2SkillEngine.CanGainB6(h.State,"P2",0));
    }
    [Theory][InlineData(5,2,3,1)][InlineData(1,2,3,-1)][InlineData(2,2,3,0)][InlineData(3,2,3,0)]
    public void JiaoziCountsAllRealHandTypesAndRequiresUniqueRanking(int own,int other,int third,int expected)
    {var h=Host.Create("tk-g46");h.Inner.Give("P1",0,0,0,0,0,own);h.Inner.Give("P2",other);h.Inner.Give("P3",third);Assert.Equal(expected,B2SkillEngine.JiaoziAdjustmentB6(h,"P1"));}
    [Fact]
    public void JiaoziAppliesAfterDiscountAndKeepsMinimumOneAndFreeZero()
    {
        int[] standard={0,0,0,2,3,0,0,0},discounted={0,0,0,1,2,0,0,0};Assert.Equal(2,B2SkillEngine.JiaoziCostB6(discounted,standard,-1,3).Sum());Assert.Equal(4,B2SkillEngine.JiaoziCostB6(discounted,standard,1,4).Sum());
        Assert.Equal(1,B2SkillEngine.JiaoziCostB6(new[]{1,0,0,0,0,0,0,0},new[]{1,1,0,0,0,0,0,0},-1,0).Sum());Assert.Equal(0,B2SkillEngine.JiaoziCostB6(new int[8],standard,1,4).Sum());
        Assert.Throws<InvalidOperationException>(()=>B2SkillEngine.JiaoziCostB6(discounted,standard,-1,5));Assert.Throws<InvalidOperationException>(()=>B2SkillEngine.JiaoziCostB6(discounted,standard,-1,0));
    }
}
