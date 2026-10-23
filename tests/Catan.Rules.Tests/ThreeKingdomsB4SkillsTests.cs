using Catan.Core.ThreeKingdoms;
using Xunit;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB4SkillsTests
{
    private static ThreeKingdomsB2TestHost Host(string first, string second = "tk-g06", string third = "tk-g09")
    {
        var h = ThreeKingdomsB2TestHost.Create(first, second, third);
        h.State.RulesVersion = GeneralCatalog.B4RulesVersion; h.State.PackageVersion = GeneralCatalog.B4PackageVersion;
        h.State.PackageContentHash = GeneralCatalog.B4ContentHash; return h;
    }
    private static B2SkillCommand Action(IThreeKingdomsCardHost h, string player, string option, int? type = null) => B2SkillEngine.Actions(h, player).First(a => a.Command.Option == option && (!type.HasValue || a.Command.CardType == type)).Command;
    private static void Resolve(IThreeKingdomsCardHost h, string player, string option, int? type = null) => B2SkillEngine.Resolve(h, player, Action(h, player, option, type));
    private static void Drain(IThreeKingdomsCardHost h)
    {
        for (int i = 0; B2SkillEngine.HasPending(h.State) && i < 80; i++)
        {
            B2SkillEngine.Pump(h); var d = h.State.Skills.PendingDecision; if (d == null) continue;
            var actions = B2SkillEngine.Actions(h, d.PlayerId); var a = actions.FirstOrDefault(a => a.Command.Option == "skip") ?? actions.First();
            B2SkillEngine.Resolve(h, d.PlayerId, a.Command);
        }
        Assert.False(B2SkillEngine.HasPending(h.State));
    }
    private static int Counter(ThreeKingdomsB2TestHost h, string player, string key) => h.State.Generals.Single(g => g.OwnerPlayerId == player).Counters.FirstOrDefault(c => c.Key == key)?.Value ?? 0;

    [Fact]
    public void DaoshuCorrectGuessKeepsCardRefundsUseAndDrawsOnceBelowFour()
    {
        var h = Host("tk-g40"); h.Give("P2", 2);
        Resolve(h, "P1", "b4-daoshu", 0); Drain(h);
        Assert.Equal(1, h.Hands["P2"].Sum()); Assert.Equal(2, h.Hands["P1"].Sum()); Assert.Equal(0, Counter(h, "P1", "daoshu"));
        Assert.Contains(B2SkillEngine.Actions(h, "P1"), a => a.Command.Option == "b4-daoshu"); h.AssertConserved();
    }
    [Fact]
    public void DaoshuWrongGuessConsumesOnlyCurrentPlayPhaseAndCannotReplay()
    {
        var h = Host("tk-g40"); h.Give("P2", 3); var c = Action(h, "P1", "b4-daoshu", 2);
        B2SkillEngine.Resolve(h, "P1", c); Drain(h); Assert.Equal(1, Counter(h, "P1", "daoshu"));
        Assert.DoesNotContain(B2SkillEngine.Actions(h, "P1"), a => a.Command.Option == "b4-daoshu"); string snapshot = h.Snapshot();
        Assert.Throws<InvalidOperationException>(() => B2SkillEngine.Resolve(h, "P1", c)); Assert.Equal(snapshot, h.Snapshot());
        h.State.Clock.PhaseId++; Assert.Contains(B2SkillEngine.Actions(h, "P1"), a => a.Command.Option == "b4-daoshu"); h.AssertConserved();
    }
    [Fact]
    public void DaoshuRewardAlsoObservesExternalBulkTransferButNotBankGain()
    {
        var h = Host("tk-g40"); h.Give("P1", 2);
        B2SkillEngine.OnCardsMoved(h, "trade", new[] { new B2HandChange { PlayerId = "P1", BeforeCount = 0, AfterCount = 2, GainedCards = new[] { 2,0,0,0,0,0,0,0 }, SourcePlayerId = "P2" } }); Drain(h);
        Assert.Equal(3, h.Hands["P1"].Sum()); B2SkillEngine.OnCardsMoved(h, "bank", new[] { new B2HandChange { PlayerId = "P1", BeforeCount = 2, AfterCount = 3 } }); Drain(h);
        Assert.Equal(3, h.Hands["P1"].Sum()); h.AssertConserved();
    }
    [Theory][InlineData("tk-g06")][InlineData("tk-g09")]
    public void DaoshuCannotBeForgedByDifferentGeneral(string general)
    {
        var h = Host(general); h.Give("P2", 1); string before = h.Snapshot();
        Assert.Throws<InvalidOperationException>(() => B2SkillEngine.Resolve(h, "P1", new B2SkillCommand { WindowId = h.State.WindowId, SkillId = "tk-g40-s1", Option = "b4-daoshu", CardType = 0, TargetPlayerIds = new[] { "P2" } })); Assert.Equal(before, h.Snapshot());
    }
    [Fact]
    public void B4SkillsDoNotAppearInOldB3Package()
    {
        var h = Host("tk-g40"); h.State.RulesVersion = GeneralCatalog.B3RulesVersion; h.Give("P2", 1);
        Assert.DoesNotContain(B2SkillEngine.Actions(h, "P1"), a => a.Command.Option.StartsWith("b4-"));
    }
    [Fact]
    public void XiashuAllowsEmptyRevealAndKeepsHiddenPilePrivateAcrossSave()
    {
        var h = Host("tk-g26"); h.Give("P1", 2); h.Give("P2", 0, 1);
        Resolve(h, "P1", "b4-xiashu"); Assert.Equal("P2", h.State.Skills.PendingDecision.PlayerId); Assert.Empty(B2SkillEngine.Actions(h, "P3"));
        var saved = h.Copy(); Resolve(h, "P2", "b4-selected"); Resolve(saved, "P2", "b4-selected");
        Assert.All(h.State.Skills.RevealedCards, n => Assert.Equal(0, n)); Resolve(h, "P1", "b4-hidden"); Resolve(saved, "P1", "b4-hidden");
        Assert.Equal(saved.Snapshot(), h.Snapshot()); Assert.Equal(3, h.Hands["P1"].Sum()); Assert.Equal(0, h.Hands["P2"].Sum()); h.AssertConserved();
    }
    [Fact]
    public void XiashuRevealCanSelectWholeBatchAndEachPlayerOnlyControlsOwnDecision()
    {
        var h = Host("tk-g26"); h.Give("P1", 1); h.Give("P2", 0, 1); Resolve(h, "P1", "b4-xiashu");
        var choice = Action(h, "P2", "b4-select", 0); string before = h.Snapshot(); Assert.Throws<InvalidOperationException>(() => B2SkillEngine.Resolve(h, "P1", choice)); Assert.Equal(before, h.Snapshot());
        Resolve(h, "P2", "b4-select", 0); Resolve(h, "P2", "b4-selected"); Assert.Equal(1, h.State.Skills.RevealedCards[0]); Resolve(h, "P1", "b4-revealed");
        Assert.Equal(1, h.Hands["P1"][0]); Assert.Equal(1, h.Hands["P2"][1]); h.AssertConserved();
    }
    [Fact]
    public void XiashuSelectionProjectsRemainingInventoryAndPrivateSelectedCounts()
    {
        var h=Host("tk-g26");h.Give("P1",3);Resolve(h,"P1","b4-xiashu");
        var first=B2SkillEngine.Actions(h,"P2").Single(a=>a.Command.Option=="b4-select"&&a.Command.CardType==0);Assert.Equal(3,first.AvailableCount);Assert.Equal(0,first.Command.Counts[0]);
        Resolve(h,"P2","b4-select",0);var second=B2SkillEngine.Actions(h,"P2").Single(a=>a.Command.Option=="b4-select"&&a.Command.CardType==0);Assert.Equal(2,second.AvailableCount);Assert.Equal(1,second.Command.Counts[0]);Assert.Empty(B2SkillEngine.Actions(h,"P1"));
        var saved=h.Copy();Assert.Equal(1,B2SkillEngine.Actions(saved,"P2").Single(a=>a.Command.Option=="b4-selected").Command.Counts[0]);h.AssertConserved();
    }
    [Fact]
    public void KuanshiCrossingSecondLossAndSecondGainAreIndependentOncePerOtherTurn()
    {
        var h = Host("tk-g06", "tk-g26");
        var c = new B2HandChange { PlayerId = "P2", BeforeCount = 5, AfterCount = 2, LostCount = 3 };
        B2SkillEngine.OnCardsMoved(h, "loss", new[] { c }); Drain(h); Assert.Equal(1, h.Hands["P2"].Sum());
        B2SkillEngine.OnCardsMoved(h, "gain", new[] { new B2HandChange { PlayerId = "P2", BeforeCount = 1, AfterCount = 4 } }); Drain(h); Assert.Equal(2, h.Hands["P2"].Sum());
        B2SkillEngine.OnCardsMoved(h, "more-loss", new[] { c }); Drain(h); Assert.Equal(2, h.Hands["P2"].Sum()); h.AssertConserved();
    }
    [Theory][InlineData(true)][InlineData(false)]
    public void QuhuLocksSubsetAndNeighborSeatsAndResolvesTrueOrFalseGuess(bool correct)
    {
        var h = Host("tk-g39"); h.Give("P1", 1,0,0,2); h.Give("P3", 2);
        Resolve(h, "P1", "b4-quhu"); Resolve(h, "P1", "b4-select", 3); Resolve(h, "P1", "b4-selected"); Resolve(h, "P1", "b4-declare", 1);
        Assert.Equal("P2", h.State.Skills.PendingDecision.PlayerId); Assert.Empty(B2SkillEngine.Actions(h, "P3"));
        var clone = h.Copy(); Resolve(h, "P2", correct ? "b4-true" : "b4-false"); Resolve(clone, "P2", correct ? "b4-true" : "b4-false"); Drain(h); Drain(clone);
        Assert.Equal(clone.Snapshot(), h.Snapshot()); Assert.Equal(correct ? 1 : 2, h.Hands["P1"].Sum()); Assert.Equal(correct ? 2 : 1, h.Hands["P3"].Sum()); h.AssertConserved();
    }
    [Fact]
    public void JiemingLocksOddGapAndSplitsTwoChosenThreeRandom()
    {
        var inner = Host("tk-g39"); inner.Give("P1", 2); var h = new B4Host(inner) { Limit = 7 };
        Resolve(h, "P1", "b4-jieming"); Assert.Equal(2, h.State.Skills.Frames.Single().Remaining); Resolve(h, "P1", "card", 3); Resolve(h, "P1", "card", 3); Drain(h);
        Assert.Equal(7, inner.Hands["P1"].Sum()); Assert.True(inner.Hands["P1"][3] >= 2); Assert.Equal(3, inner.RandomCalls); Assert.DoesNotContain(B2SkillEngine.Actions(h, "P1"), a => a.Command.Option == "b4-jieming"); inner.AssertConserved();
    }
    [Fact]
    public void WukuCountsBuildingEventsOnceAndAwakensAtAnyTurnEnd()
    {
        var h = Host("tk-g06", "tk-g41"); foreach (string name in new[] { "Road", "Ship", "City" }) B2SkillEngine.OnBuilt(h, name, "P1", name);
        B2SkillEngine.OnBuilt(h, "Road", "P1", "Road"); B2SkillEngine.OnBuilt(h, "Knight", "P1", "Knight"); Assert.Equal(3, Counter(h, "P2", "wuku"));
        B2SkillEngine.OnEnd(h, "end"); Drain(h); Assert.Equal(1, Counter(h, "P2", "sanchen")); Assert.Equal(8, B2SkillEngine.EffectiveCardTypes(h.State, "P2", 0).Length);
    }
    [Fact]
    public void MiewuVirtualCardPaysThreeMarksAndRealCardAndDefersRewardUntilCompletion()
    {
        var inner = Host("tk-g41"); inner.Give("P1", 1); var g = inner.State.Generals[0]; g.Counters = new[] { new GeneralCounter { Key = "wuku", Value = 3 }, new GeneralCounter { Key = "sanchen", Value = 1 } }; var h = new B4Host(inner);
        Resolve(h, "P1", "b4-miewu-progress", 0); Drain(h); Assert.Equal(0, Counter(inner, "P1", "wuku")); Assert.Equal(0, inner.Hands["P1"].Sum()); Assert.Single(h.Uses);
        B2SkillEngine.OnB4VirtualProgressCompleted(h, "P1", h.Uses[0]); Drain(h); Assert.Equal(1, inner.Hands["P1"].Sum()); B2SkillEngine.OnB4VirtualProgressCompleted(h, "P1", h.Uses[0]); Drain(h); Assert.Equal(1, inner.Hands["P1"].Sum()); inner.AssertConserved();
    }
    [Theory][InlineData(0,1)][InlineData(1,0)][InlineData(2,3)][InlineData(3,2)]
    public void LongdanOffersExactlyOneConversionWithoutChangingRealCards(int real, int effective)
    {
        var h = Host("tk-g42"); Assert.Equal(new[] { real, effective }, B2SkillEngine.EffectiveCardTypes(h.State, "P1", real)); Assert.Equal(new[] { 4 }, B2SkillEngine.EffectiveCardTypes(h.State, "P1", 4));
    }
    [Theory][InlineData(5,2)][InlineData(6,4)][InlineData(7,0)]
    public void QingguoConvertsCommodityToCorrespondingResourceOnly(int real, int effective)
    {
        var h = Host("tk-g44"); Assert.Equal(new[] { real, effective }, B2SkillEngine.EffectiveCardTypes(h.State, "P1", real)); Assert.Equal(new[] { effective }, B2SkillEngine.EffectiveCardTypes(h.State, "P1", effective));
    }
    [Fact]
    public void YajiaoInspectsEachActualLostCardButUnmatchedDrawNeverEntersHand()
    {
        var h = Host("tk-g06", "tk-g42"); h.EmptyBank(1,8);
        B2SkillEngine.OnCardsMoved(h, "lost", new[] { new B2HandChange { PlayerId = "P2", BeforeCount = 2, AfterCount = 0, LostCount = 2, LostCards = new[] { 1,1,0,0,0,0,0,0 } } });
        Resolve(h, "P2", "accept"); Resolve(h, "P2", "accept"); Drain(h); Assert.Equal(1, h.Hands["P2"].Sum()); Assert.Equal(2, h.RandomCalls); h.AssertConserved();
    }
    [Fact]
    public void WenjiCanRepeatAndMarksAllCardsOfSelectedTrueTypeUntilEnd()
    {
        var h = Host("tk-g45"); h.Give("P2", 2); Resolve(h, "P1", "b4-wenji"); Resolve(h, "P2", "b4-give", 0);
        Resolve(h, "P1", "b4-wenji"); Resolve(h, "P2", "b4-give", 0); Assert.Equal(2, B2SkillEngine.WenjiTradeRatio(h.State, "P1", 0));
        B2SkillEngine.OnEnd(h, "end"); Drain(h); Assert.Equal(4, h.Hands["P1"].Sum()); Assert.Equal(0, B2SkillEngine.WenjiTradeRatio(h.State, "P1", 0)); h.AssertConserved();
    }
    [Fact]
    public void TunjiangDoesNotDrawAfterUnusedProgressCardLoss()
    {
        var h = Host("tk-g45"); h.Give("P2", 1); Resolve(h, "P1", "b4-wenji"); Resolve(h, "P2", "b4-give", 0);
        B2SkillEngine.OnCardsMoved(h, "progress-loss", new[] { new B2HandChange { PlayerId = "P1", BeforeCount = 1, AfterCount = 1, LostProgressCount = 1 } }); B2SkillEngine.OnEnd(h, "end"); Drain(h); Assert.Equal(1, h.Hands["P1"].Sum()); h.AssertConserved();
    }
    [Fact]
    public void ZishuTriggersOncePerBulkGainAndExcludesOwnRewardSource()
    {
        var h = Host("tk-g52"); h.Give("P1", 3); B2SkillEngine.OnCardsMoved(h, "gain", new[] { new B2HandChange { PlayerId = "P1", BeforeCount = 0, AfterCount = 3 } }); Drain(h);
        Assert.Equal(4, h.Hands["P1"].Sum()); Assert.Equal(1, h.RandomCalls); B2SkillEngine.OnCardsMoved(h, "gain", new[] { new B2HandChange { PlayerId = "P1", BeforeCount = 0, AfterCount = 3 } }); Drain(h); Assert.Equal(4, h.Hands["P1"].Sum()); h.AssertConserved();
    }
    [Fact]
    public void XiemuRequiresTwoDistinctPhysicalCardsIncludingTradeAndCreatesOnlyOneVirtualUse()
    {
        var inner = Host("tk-g52"); inner.DrawProgress("P1",0); inner.DrawProgress("P1",1); var h = new B4Host(inner);
        Resolve(h,"P1","b4-xiemu"); Drain(h); Assert.Empty(inner.Progress["P1"]); Assert.Single(h.Uses); Assert.Equal("ResourceMonopoly",h.Uses[0].CardId); inner.AssertConserved();
    }
    [Fact]
    public void LuoshenRewardsIntermediateOddOnceAndStopsBeforeFormalSubmission()
    {
        var h = Host("tk-g44"); h.State.Clock.Phase = ThreeKingdomsPhase.Roll;
        for (uint seed = 1; seed < 10000; seed++) { var candidate = Host("tk-g44"); candidate.RandomState = seed; if ((candidate.DrawRandom(6) + candidate.DrawRandom(6) + 2) % 2 == 1) { h.RandomState = seed; break; } }
        B3DiceEngine.BeginRoll(h,"P1","formal"); Assert.Equal("dice-luoshen",h.State.Dice.PendingDecision.Kind); Assert.Null(B3DiceEngine.TakeFinal(h)); Assert.Equal(1,h.Hands["P1"].Sum());
        var copy=h.Copy(); var stop=B3DiceEngine.Actions(h,"P1").Single(a=>a.Command.Option=="luoshen-stop").Command; B3DiceEngine.Resolve(h,"P1",stop); B3DiceEngine.Resolve(copy,"P1",stop);
        Assert.NotNull(B3DiceEngine.TakeFinal(h)); Assert.NotNull(B3DiceEngine.TakeFinal(copy)); Assert.Equal(copy.Snapshot(),h.Snapshot()); Assert.Empty(B3DiceEngine.Validate(h.State)); h.AssertConserved();
    }
    [Fact]
    public void SameEventSiblingSkillOrderIsChosenAndResumesWithoutReorderingParent()
    {
        var h=Host("tk-g26");var g=h.State.Generals[0];
        h.State.Skills.Frames=new[]{new B2SkillFrame{Id="first",PlayerId="P1",TenureId=g.TenureId,SkillId="tk-g26-s1",Kind="resource",Remaining=1,SourceEventId="shared"},new B2SkillFrame{Id="second",PlayerId="P1",TenureId=g.TenureId,SkillId="tk-g26-s2",Kind="resource",Remaining=1,SourceEventId="shared"},new B2SkillFrame{Id="parent",PlayerId="P1",TenureId=g.TenureId,SkillId="tk-g26-s1",Kind="resource",Remaining=1,SourceEventId="shared",Started=true}};
        B2SkillEngine.Pump(h);Assert.Equal("b4-order",h.State.Skills.PendingDecision.Kind);var a=Action(h,"P1","b4-order");a.ProgressCardId="parent";string before=h.Snapshot();Assert.Throws<InvalidOperationException>(()=>B2SkillEngine.Resolve(h,"P1",a));Assert.Equal(before,h.Snapshot());
        a=Action(h,"P1","b4-order");a.ProgressCardId="second";var copy=h.Copy();B2SkillEngine.Resolve(h,"P1",a);B2SkillEngine.Resolve(copy,"P1",a);Assert.Equal("second",h.State.Skills.Frames[0].Id);Drain(h);Drain(copy);Assert.Equal(copy.Snapshot(),h.Snapshot());Assert.Equal(3,h.Hands["P1"].Sum());h.AssertConserved();
    }
    [Fact]
    public void DifferentSourcesAndOtherPlayersNeverShareOrderDecision()
    {
        var h=Host("tk-g26");h.State.Skills.Frames=new[]{new B2SkillFrame{Id="a",PlayerId="P1",SkillId="tk-g26-s1",Kind="resource",Remaining=1,SourceEventId="one"},new B2SkillFrame{Id="b",PlayerId="P2",SkillId="tk-g26-s2",Kind="resource",Remaining=1,SourceEventId="one"},new B2SkillFrame{Id="c",PlayerId="P1",SkillId="tk-g26-s2",Kind="resource",Remaining=1,SourceEventId="two"}};
        B2SkillEngine.Pump(h);Assert.Equal("card",h.State.Skills.PendingDecision.Kind);Drain(h);Assert.Equal(2,h.Hands["P1"].Sum());Assert.Equal(1,h.Hands["P2"].Sum());h.AssertConserved();
    }
    private sealed class B4Host : IThreeKingdomsB4Host
    {
        public B4Host(ThreeKingdomsB2TestHost inner) { Inner=inner; }
        public ThreeKingdomsB2TestHost Inner {get;}
        public int Limit {get;set;}=7;
        public List<B4VirtualUse> Uses {get;}=new();
        public ThreeKingdomsState State=>Inner.State; public int FinalDiceTotal=>Inner.FinalDiceTotal;
        public int[] GetHand(string p)=>Inner.GetHand(p); public int[] GetBank()=>Inner.GetBank(); public void RemoveHand(string p,int[] c)=>Inner.RemoveHand(p,c); public void AddHand(string p,int[] c)=>Inner.AddHand(p,c);
        public void RemoveBank(int[] c)=>Inner.RemoveBank(c); public void AddBank(int[] c)=>Inner.AddBank(c); public int DrawRandom(int max)=>Inner.DrawRandom(max);
        public B2ProgressCard[] GetProgressCards(string p)=>Inner.GetProgressCards(p); public int GetProgressDeckCount(int c)=>Inner.GetProgressDeckCount(c); public void RemoveProgress(string p,string c)=>Inner.RemoveProgress(p,c);
        public B2ProgressCard DrawProgress(string p,int c)=>Inner.DrawProgress(p,c); public void ReturnProgress(B2ProgressCard c)=>Inner.ReturnProgress(c); public B2HarvestTile[] GetHarvestTiles(string p)=>Inner.GetHarvestTiles(p);
        public int GetHandLimit(string p)=>Limit; public B4VirtualUse[] GetVirtualProgressUses(string p,string timing)=>new[]{new B4VirtualUse {CardId="ResourceMonopoly",OptionId="monopoly-wood",Label="垄断木头",CardType=0}};
        public void UseVirtualProgress(string p,B4VirtualUse use)=>Uses.Add(use);
    }
}
