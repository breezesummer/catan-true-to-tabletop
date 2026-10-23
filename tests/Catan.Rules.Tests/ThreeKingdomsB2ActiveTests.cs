using Catan.Core.ThreeKingdoms;
using Xunit;
using static Catan.Rules.Tests.ThreeKingdomsB2EventTests;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB2ActiveTests
{
    [Fact]
    public void RendeTransfersFourCardsAndPaysTwoMixedChoicesWithMidRewardResume()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g15"); host.Give("P1", 4);
        var gift = Active(host, "gift"); gift.Counts = Cards(4);
        ResolveWithReplay(host, "P1", gift);
        Assert.Equal(4, host.GetHand("P2").Sum()); Assert.Equal(0, host.GetHand("P1").Sum());
        ResolveRewardWithReplay(host, 5); ResolveRewardWithReplay(host, 7); Drain(host);
        Assert.Equal(2, host.GetHand("P1").Sum()); Assert.Equal(1, host.GetHand("P1")[5]); Assert.Equal(1, host.GetHand("P1")[7]); host.AssertConserved();
    }

    [Theory][InlineData("self")][InlineData("zero")][InlineData("too-many")][InlineData("other-turn")][InlineData("unknown-target")]
    public void RendeInvalidGiftNeverMovesCardsOrChangesCounters(string invalid)
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g15"); host.Give("P1", 1);
        var gift = Active(host, "gift");
        if (invalid == "self") gift.TargetPlayerIds = new[] { "P1" };
        if (invalid == "unknown-target") gift.TargetPlayerIds = new[] { "未知" };
        if (invalid == "zero") gift.Counts = Cards();
        if (invalid == "too-many") gift.Counts = Cards(2);
        if (invalid == "other-turn") host.State.Clock.ActivePlayerId = "P2";
        RejectUnchanged(host, "P1", gift); host.AssertConserved();
    }

    [Fact]
    public void RendeTargetsGuojiaBeforeGivingThenRewardsOnlyRealGivenQuantity()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g15", "tk-g37"); host.Give("P1", 2);
        var gift = Active(host, "gift"); gift.Counts = Cards(2);
        ResolveWithReplay(host, "P1", gift);
        Assert.Equal("P2", host.State.Skills.PendingDecision.PlayerId);
        Assert.Equal(2, host.GetHand("P1").Sum()); Assert.Equal(0, host.GetHand("P2").Sum());
        var response = B2SkillEngine.Actions(host, "P2").First(a => a.Command.CardType == 3);
        ResolveWithReplay(host, "P2", response.Command); Drain(host);
        Assert.Equal(3, host.GetHand("P2").Sum()); Assert.Equal(1, host.GetHand("P1").Sum()); host.AssertConserved();
    }

    [Fact]
    public void ZhihengBothOptionsCanBeUsedOnceAndOldProgressIsReturnedAfterNewTopIsDrawn()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g22"); host.Give("P1", 1, 0, 0, 0, 0, 1);
        var oldCard = host.DrawProgress("P1", 1); string expectedNew = host.Decks[1][0].Id;
        var hand = Active(host, "exchange-hand"); hand.Counts = Cards(1, 0, 0, 0, 0, 1);
        ResolveWithReplay(host, "P1", hand); Drain(host);
        Assert.Equal(2, host.GetHand("P1").Take(5).Sum()); Assert.Equal(0, host.GetHand("P1").Skip(5).Sum());
        var again = Clone(hand); again.WindowId = host.State.WindowId; RejectUnchanged(host, "P1", again);
        ResolveWithReplay(host, "P1", Active(host, "exchange-progress")); Drain(host);
        Assert.Equal(expectedNew, Assert.Single(host.GetProgressCards("P1")).Id);
        Assert.Equal(oldCard.Id, host.Decks[1].Last().Id);
        Assert.DoesNotContain(B2SkillEngine.Actions(host, "P1"), a => a.Command.Option.StartsWith("exchange-"));
        host.AssertConserved();
    }

    [Theory][InlineData(0)][InlineData(5)]
    public void ZhihengRejectsZeroOrFiveCardCost(int count)
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g22"); host.Give("P1", 5);
        var command = Active(host, "exchange-hand"); command.Counts = Cards(count);
        RejectUnchanged(host, "P1", command); host.AssertConserved();
    }

    [Fact]
    public void ZhihengCannotUseItsOldProgressCardAsTheOnlyDeckSupply()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g22");
        var old = host.DrawProgress("P1", 1); while (host.Decks[1].Count > 0) host.DrawProgress("P3", 1);
        var command = new B2SkillCommand { SkillId = "tk-g22-s1", Option = "exchange-progress", ProgressCardId = old.Id, WindowId = host.State.WindowId };
        RejectUnchanged(host, "P1", command); host.AssertConserved();
    }

    [Fact]
    public void ZhihengCommodityWithEmptyResourceBankYieldsZeroButStillConsumesHandOption()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g22"); host.Give("P1", 0, 0, 0, 0, 0, 2); host.EmptyBank(0, 5);
        var command = Active(host, "exchange-hand"); command.Counts = Cards(0, 0, 0, 0, 0, 2);
        ResolveWithReplay(host, "P1", command); Drain(host);
        Assert.Equal(0, host.GetHand("P1").Sum()); Assert.Equal(0, host.RandomCalls);
        Assert.DoesNotContain(B2SkillEngine.Actions(host, "P1"), a => a.Command.Option == "exchange-hand"); host.AssertConserved();
    }

    [Fact]
    public void QianxunOwnDiscardTriggersLianyingOnceAndRenewsOnlyInNewPhase()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g23"); host.State.Clock.ActivePlayerId = "P2"; host.Give("P1", 0, 0, 1);
        ResolveWithReplay(host, "P1", Active(host, "discard")); Drain(host);
        Assert.Equal(2, host.GetHand("P1").Sum());
        Assert.DoesNotContain(B2SkillEngine.Actions(host, "P1"), a => a.Command.Option == "discard");
        host.State.Clock.PhaseId++;
        Assert.Contains(B2SkillEngine.Actions(host, "P1"), a => a.Command.Option == "discard"); host.AssertConserved();
    }

    [Fact]
    public void QianxunDiscardStaysInEscrowUntilLianyingSeesEmptyBank()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g23"); host.Give("P1", 1); host.EmptyBank(0, 5);
        ResolveWithReplay(host, "P1", Active(host, "discard")); Drain(host);
        Assert.Equal(0, host.GetHand("P1").Sum()); Assert.Equal(1, host.GetBank()[0]);
        Assert.Equal(0, host.RandomCalls); host.AssertConserved();
    }

    [Theory][InlineData("Espionage")][InlineData("GuildDues")][InlineData("Indulgence")][InlineData("Snatch")]
    public void QianxunRejectsOnlyItsNamedTargetsBeforeAnyResponse(string effect)
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g23");
        string before = host.Snapshot();
        Assert.False(B2SkillEngine.CanTarget(host.State, "P1", effect));
        Assert.Throws<InvalidOperationException>(() => B2SkillEngine.OnTargeted(host, "被禁止目标", "P2", "P1", effect));
        Assert.Equal(before, host.Snapshot());
        Assert.True(B2SkillEngine.CanTarget(host.State, "P1", "tk-g01-s1"));
        Assert.True(B2SkillEngine.CanTarget(host.State, "P1", "Robber"));
    }

    [Fact]
    public void JushouTakesFourChosenResourcesThenBlocksDrawsUntilOwnNextOpportunity()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g06", "tk-g01");
        host.State.Clock.Phase = ThreeKingdomsPhase.End;
        B2SkillEngine.OnEnd(host, "自己结束");
        ResolveWithReplay(host, "P1", Active(host, "accept"));
        ResolveRewardWithReplay(host, 0); ResolveRewardWithReplay(host, 0); ResolveRewardWithReplay(host, 4); ResolveRewardWithReplay(host, 4); Drain(host);
        Assert.Equal(Cards(2, 0, 0, 0, 2), host.GetHand("P1")); Assert.True(B2SkillEngine.IsDrawBlocked(host.State, "P1"));
        B2SkillEngine.OnOpportunityStart(host, 2, "P2"); Assert.True(B2SkillEngine.IsDrawBlocked(host.State, "P1"));
        B2SkillEngine.OnOpportunityStart(host, 4, "P1"); Assert.False(B2SkillEngine.IsDrawBlocked(host.State, "P1")); host.AssertConserved();
    }

    [Theory][InlineData(0)][InlineData(2)]
    public void JushouPartialSupplyStillCreatesBlockAndSkippedOpportunityRemovesIt(int supply)
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g06", "tk-g01"); host.EmptyBank(0, 5);
        if (supply > 0) { host.RemoveHand("P3", Cards(supply)); host.AddBank(Cards(supply)); }
        host.State.Clock.Phase = ThreeKingdomsPhase.End; B2SkillEngine.OnEnd(host, "据守短缺"); Drain(host);
        Assert.Equal(supply, host.GetHand("P1").Sum()); Assert.True(B2SkillEngine.IsDrawBlocked(host.State, "P1"));
        B2SkillEngine.OnOpportunityStart(host, 5, "P2", new[] { "P1" }); Assert.False(B2SkillEngine.IsDrawBlocked(host.State, "P1")); host.AssertConserved();
    }

    [Fact]
    public void ZhengguTargetsGuojiaThenSnapshotsTwiceAndSurvivesSourceRetirement()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g09", "tk-g37", "tk-g06"); host.Give("P1", 1); host.Give("P2", 3);
        host.State.Clock.Phase = ThreeKingdomsPhase.End; B2SkillEngine.OnEnd(host, "镇骨结束");
        ResolveWithReplay(host, "P1", Active(host, "accept"));
        var target = Active(host, "target"); target.TargetPlayerIds = new[] { "P2" };
        ResolveWithReplay(host, "P1", target);
        Assert.Equal("P2", host.State.Skills.PendingDecision.PlayerId);
        Drain(host); Assert.Equal(4, host.GetHand("P1").Sum()); Assert.Equal(4, host.GetHand("P2").Sum());
        Assert.Single(host.State.Skills.TimedEffects, e => e.Kind == "zhenggu");
        host.State.Generals[0].Region = GeneralRegion.Pool;
        host.Give("P2", 2);
        B2SkillEngine.OnPlayStart(host, "乙实际出牌", "P2"); Drain(host);
        Assert.Equal(6, host.GetHand("P1").Sum()); Assert.Equal(6, host.GetHand("P2").Sum());
        Assert.DoesNotContain(host.State.Skills.TimedEffects, e => e.Kind == "zhenggu");
        B2SkillEngine.OnPlayStart(host, "乙实际出牌", "P2"); Drain(host);
        Assert.Equal(6, host.GetHand("P1").Sum()); host.AssertConserved();
    }

    [Fact]
    public void FenyinDifferentSuccessfulTypesRewardAndIgnoredKindsDoNotBreakPreviousType()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g12");
        foreach (var item in new[] { ("a", "Road"), ("b", "Knight"), ("c", "Repair"), ("d", "Ship"), ("e", "Ship"), ("f", "City") })
        { B2SkillEngine.OnBuilt(host, item.Item1, "P1", item.Item2); Drain(host); }
        Assert.Equal(2, host.GetHand("P1").Sum());
        B2SkillEngine.OnBuilt(host, "f", "P1", "City"); Drain(host); Assert.Equal(2, host.GetHand("P1").Sum());
        host.State.Clock.PhaseId++; B2SkillEngine.OnBuilt(host, "new-phase", "P1", "Settlement"); Drain(host);
        Assert.Equal(2, host.GetHand("P1").Sum()); host.AssertConserved();
    }

    [Fact]
    public void KejiTracksActualLossesEvenInAtomicTradeAndProtectionExpiresAfterNextOpportunity()
    {
        var protectedHost = ThreeKingdomsB2TestHost.Create("tk-g28");
        protectedHost.State.Clock.Phase = ThreeKingdomsPhase.End; B2SkillEngine.OnEnd(protectedHost, "无失牌结束"); Drain(protectedHost);
        Assert.Equal(1, protectedHost.GetHand("P1").Sum()); Assert.False(B2SkillEngine.IsProtected(protectedHost.State, "P1"));
        protectedHost.State.Clock.OpportunityId = 2; B2SkillEngine.OnOpportunityStart(protectedHost, 2, "P2");
        Assert.True(B2SkillEngine.IsProtected(protectedHost.State, "P1"));
        B2SkillEngine.OnOpportunityEnd(protectedHost, 2, "P2"); Assert.False(B2SkillEngine.IsProtected(protectedHost.State, "P1"));
        var lostHost = ThreeKingdomsB2TestHost.Create("tk-g28");
        B2SkillEngine.OnCardsMoved(lostHost, "原子交易实际付二得二", new[] { Change("P1", 2, 2, 2, 2) });
        lostHost.State.Clock.Phase = ThreeKingdomsPhase.End; B2SkillEngine.OnEnd(lostHost, "交易后结束"); Drain(lostHost);
        Assert.Equal(0, lostHost.GetHand("P1").Sum()); Assert.Empty(lostHost.State.Skills.TimedEffects);
        protectedHost.AssertConserved(); lostHost.AssertConserved();
    }

    [Fact]
    public void ZhengguMultipleMarkersEachSnapshotCurrentHandsAndSkipDoesNotConsumeThem()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g09", "tk-g06", "tk-g01"); host.Give("P2", 4);
        for (int turn = 0; turn < 2; turn++)
        {
            host.State.Clock.TurnInstanceId++; host.State.Clock.Phase = ThreeKingdomsPhase.End;
            B2SkillEngine.OnEnd(host, "镇骨结束-" + turn);
            ResolveWithReplay(host, "P1", Active(host, "accept"));
            var target = Active(host, "target"); target.TargetPlayerIds = new[] { "P2" };
            ResolveWithReplay(host, "P1", target); Drain(host);
        }
        Assert.Equal(2, host.State.Skills.TimedEffects.Count(e => e.Kind == "zhenggu"));
        B2SkillEngine.OnOpportunityStart(host, 4, "P3", new[] { "P2" });
        Assert.Equal(2, host.State.Skills.TimedEffects.Count(e => e.Kind == "zhenggu"));
        host.Give("P2", 2); int randomBefore = host.RandomCalls;
        var restored = host.Copy();
        B2SkillEngine.OnPlayStart(host, "乙实际阶段", "P2"); B2SkillEngine.OnPlayStart(restored, "乙实际阶段", "P2");
        Assert.Equal(host.Snapshot(), restored.Snapshot());
        Drain(host); Assert.Equal(6, host.GetHand("P1").Sum()); Assert.Equal(randomBefore + 2, host.RandomCalls);
        Assert.DoesNotContain(host.State.Skills.TimedEffects, e => e.Kind == "zhenggu"); host.AssertConserved();
    }

    [Fact]
    public void JushouDeclineDoesNotBlockAndRetirementDoesNotEraseAnAcceptedBlock()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g06", "tk-g01"); host.State.Clock.Phase = ThreeKingdomsPhase.End;
        B2SkillEngine.OnEnd(host, "先放弃"); ResolveWithReplay(host, "P1", Active(host, "skip")); Drain(host);
        Assert.False(B2SkillEngine.IsDrawBlocked(host.State, "P1")); Assert.Equal(0, host.GetHand("P1").Sum());
        host.State.Clock.TurnInstanceId++;
        B2SkillEngine.OnEnd(host, "以后发动"); Drain(host);
        host.State.Generals[0].GeneralId = "tk-g28"; host.State.Generals[0].TenureId = "后来吕蒙";
        Assert.True(B2SkillEngine.IsDrawBlocked(host.State, "P1"));
        Assert.Equal(4, host.GetHand("P1").Sum()); host.AssertConserved();
    }

    [Theory][InlineData("not-play")][InlineData("not-owner")][InlineData("public-point")]
    public void ZhihengStillRequiresLegalPhaseActorAndNonPublicProgress(string invalid)
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g22");
        var old = host.DrawProgress("P1", 0);
        var command = Active(host, "exchange-progress");
        if (invalid == "not-play") host.State.Clock.Phase = ThreeKingdomsPhase.Draw;
        if (invalid == "not-owner") host.State.Clock.ActivePlayerId = "P2";
        if (invalid == "public-point") old.PublicPoint = true;
        RejectUnchanged(host, "P1", command); host.AssertConserved();
    }

    [Fact]
    public void FenyinNoSupplyStillUpdatesTypeAndNewTenureDoesNotInheritItsRecord()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g12"); host.EmptyBank(0, 5);
        B2SkillEngine.OnBuilt(host, "路", "P1", "Road"); Drain(host);
        B2SkillEngine.OnBuilt(host, "村", "P1", "Settlement"); Drain(host);
        host.RemoveHand("P3", Cards(2)); host.AddBank(Cards(2));
        B2SkillEngine.OnBuilt(host, "同类村", "P1", "Settlement"); Drain(host);
        Assert.Equal(0, host.GetHand("P1").Sum());
        host.State.Generals[0].TenureId = "新留赞"; host.State.Generals[0].Counters = Array.Empty<GeneralCounter>();
        B2SkillEngine.OnBuilt(host, "新任次船", "P1", "Ship"); Drain(host);
        Assert.Equal(0, host.GetHand("P1").Sum()); host.AssertConserved();
    }

    [Fact]
    public void QicaiTracksOnlyCurrentActiveTenureAndDoesNotGrantOtherPlayersUnlimitedCards()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g33");
        Assert.True(B2SkillEngine.HasUnlimitedProgress(host.State, "P1")); Assert.False(B2SkillEngine.HasUnlimitedProgress(host.State, "P2"));
        host.State.Generals[0].Region = GeneralRegion.Hidden;
        Assert.False(B2SkillEngine.HasUnlimitedProgress(host.State, "P1"));
        host.State.Generals[0].Region = GeneralRegion.Active; host.State.Generals[0].TenureId = "新黄月英";
        Assert.True(B2SkillEngine.HasUnlimitedProgress(host.State, "P1"));
    }

    internal static B2SkillCommand Active(ThreeKingdomsB2TestHost host, string option) =>
        Clone(B2SkillEngine.Actions(host, "P1").First(action => action.Command.Option == option).Command);
}
