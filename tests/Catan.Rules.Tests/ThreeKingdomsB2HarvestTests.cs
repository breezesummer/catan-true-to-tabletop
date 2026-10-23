using Catan.Core.ThreeKingdoms;
using Xunit;
using static Catan.Rules.Tests.ThreeKingdomsB2EventTests;
using static Catan.Rules.Tests.ThreeKingdomsB2ActiveTests;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB2HarvestTests
{
    [Fact]
    public void RaidAtomicallyReplacesChosenAllocationWithOneActualCardFromEachDifferentTarget()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g01"); host.Give("P2", 0, 0, 1); host.Give("P3", 0, 0, 0, 0, 1);
        Begin(host, Cards(1, 1, 0, 0, 0, 1));
        var raid = Active(host, "raid"); raid.Counts = Cards(1, 1); raid.TargetPlayerIds = new[] { "P2", "P3" };
        ResolveWithReplay(host, "P1", raid); Drain(host);
        Assert.Equal(Cards(0, 0, 1, 0, 1, 1), host.GetHand("P1"));
        Assert.Equal(0, host.GetHand("P2").Sum()); Assert.Equal(0, host.GetHand("P3").Sum());
        Assert.Equal(30, host.GetBank()[0]); Assert.Equal(30, host.GetBank()[1]); Assert.Equal(2, host.RandomCalls);
        Assert.Equal(3, host.State.Skills.Harvest.Single(a => a.PlayerId == "P1").ActuallyReceived); host.AssertConserved();
    }

    [Theory][InlineData("self")][InlineData("duplicate")][InlineData("too-many")][InlineData("unallocated-type")][InlineData("empty-target")]
    public void RaidInvalidTargetsAndForgoneAllocationAreRejectedBeforeAnyRandomOrSupplyChange(string invalid)
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g01"); host.Give("P2", 0, 0, 1);
        Begin(host, Cards(1, 1));
        var raid = Active(host, "raid");
        if (invalid == "self") raid.TargetPlayerIds = new[] { "P1" };
        if (invalid == "duplicate") { raid.TargetPlayerIds = new[] { "P2", "P2" }; raid.Counts = Cards(1, 1); }
        if (invalid == "too-many") { raid.TargetPlayerIds = new[] { "P2", "P3" }; raid.Counts = Cards(3); }
        if (invalid == "unallocated-type") raid.Counts = Cards(0, 0, 1);
        if (invalid == "empty-target") raid.TargetPlayerIds = new[] { "P3" };
        RejectUnchanged(host, "P1", raid);
        ResolveWithReplay(host, "P1", Active(host, "pass")); Drain(host); host.AssertConserved();
    }

    [Fact]
    public void RaidTargetProtectionAfterYijiRestoresOriginalReservedAllocationWhenCanceled()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g01", "tk-g37"); host.Give("P2", 0, 0, 1);
        Begin(host, Cards(1));
        ResolveWithReplay(host, "P1", Active(host, "raid"));
        Assert.Equal("P2", host.State.Skills.PendingDecision.PlayerId);
        Assert.Equal(29, host.GetBank()[0]); Assert.Equal(0, host.GetHand("P1").Sum());
        host.State.Skills.TimedEffects = new[] { new B2TimedEffect { Id = "目标新增克己", Kind = "protection", PlayerId = "P2", OpportunityId = 1 } };
        var yiji = B2SkillEngine.Actions(host, "P2").First(a => a.Command.CardType == 4);
        ResolveWithReplay(host, "P2", yiji.Command);
        Assert.Equal("raid-revalidate", host.State.Skills.PendingDecision.Kind);
        ResolveWithReplay(host, "P1", Active(host, "pass")); Drain(host);
        Assert.Equal(Cards(1), host.GetHand("P1")); Assert.Equal(2, host.GetHand("P2").Sum());
        Assert.Equal(0, host.RandomCalls); host.AssertConserved();
    }

    [Fact]
    public void RaidLastCardTriggersLianyingOnlyAfterWholeHarvestCommit()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g01", "tk-g23"); host.Give("P2", 0, 0, 0, 0, 1);
        Begin(host, Cards(1));
        ResolveWithReplay(host, "P1", Active(host, "raid"));
        Assert.Equal(1, host.GetHand("P1")[4]); Assert.Equal(0, host.GetHand("P2").Sum());
        Assert.Equal("P2", host.State.Skills.PendingDecision.PlayerId);
        Drain(host); Assert.Equal(2, host.GetHand("P2").Sum()); Assert.Equal(1, host.GetHand("P1").Sum()); host.AssertConserved();
    }

    [Fact]
    public void RaidCannotReplaceZeroAllocationOrChooseProtectedTarget()
    {
        var zero = ThreeKingdomsB2TestHost.Create("tk-g01"); zero.Give("P2", 1);
        Begin(zero, Cards()); Drain(zero); Assert.Equal(0, zero.GetHand("P1").Sum()); Assert.Equal(0, zero.RandomCalls); zero.AssertConserved();
        var protectedHost = ThreeKingdomsB2TestHost.Create("tk-g01"); protectedHost.Give("P2", 1);
        protectedHost.State.Skills.TimedEffects = new[] { new B2TimedEffect { Id = "已有克己", Kind = "protection", PlayerId = "P2", OpportunityId = 1 } };
        Begin(protectedHost, Cards(0, 1)); Drain(protectedHost);
        Assert.Equal(1, protectedHost.GetHand("P1")[1]); Assert.Equal(1, protectedHost.GetHand("P2")[0]); Assert.Equal(0, protectedHost.RandomCalls); protectedHost.AssertConserved();
    }

    [Fact]
    public void ShelieRevealsFourAndSelectsAtMostOneOfThreeDifferentTypesWithEveryChoiceRecoverable()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g28"); host.EmptyBank(0, 5);
        host.RemoveHand("P3", Cards(1, 1, 1, 1)); host.AddBank(Cards(1, 1, 1, 1));
        Begin(host, Cards()); ResolveWithReplay(host, "P1", Active(host, "shelie"));
        Assert.Equal(4, host.State.Skills.RevealedCards.Sum()); Assert.Equal(0, host.GetHand("P1").Sum());
        ResolveRewardWithReplay(host, 0);
        var duplicate = Clone(B2SkillEngine.Actions(host, "P1").First(a => a.Command.Option == "card").Command); duplicate.CardType = 0;
        RejectUnchanged(host, "P1", duplicate);
        ResolveRewardWithReplay(host, 1); ResolveRewardWithReplay(host, 2); Drain(host);
        Assert.Equal(Cards(1, 1, 1), host.GetHand("P1")); Assert.Equal(1, host.GetBank()[3]);
        Assert.Equal(0, host.State.Skills.RevealedCards.Sum()); Assert.Equal(4, host.RandomCalls); host.AssertConserved();
    }

    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public void ShelieEmptyOrRepeatedSameResourceRevealsOnlyActualSupplyAndNeverGivesDuplicateType(int supply)
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g28"); host.EmptyBank(0, 5);
        if (supply > 0) { host.RemoveHand("P3", Cards(supply)); host.AddBank(Cards(supply)); }
        Begin(host, Cards()); ResolveWithReplay(host, "P1", Active(host, "shelie")); Drain(host);
        Assert.Equal(supply == 0 ? 0 : 1, host.GetHand("P1").Sum());
        Assert.Equal(supply == 2 ? 1 : 0, host.GetBank()[0]); host.AssertConserved();
    }

    [Fact]
    public void OtherPlayersReservedProductionIsUnavailableToShelieAndLaterRewards()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g28"); host.EmptyBank(0, 5);
        host.RemoveHand("P3", Cards(0, 0, 0, 0, 1)); host.AddBank(Cards(0, 0, 0, 0, 1));
        host.State.Clock.Phase = ThreeKingdomsPhase.Draw;
        B2SkillEngine.BeginHarvest(host, new[] { Allocation("P1", Cards()), Allocation("P2", Cards(0, 0, 0, 0, 1)) });
        ResolveWithReplay(host, "P1", Active(host, "shelie")); Drain(host);
        Assert.Equal(0, host.GetHand("P1").Sum()); Assert.Equal(1, host.GetHand("P2")[4]); Assert.Equal(0, host.RandomCalls); host.AssertConserved();
    }

    [Fact]
    public void TianduReplacesNormalAllocationWithOneCardPerQualifiedDistinctTileIncludingCityCommodity()
    {
        var host = TianduHost(9);
        Begin(host, Cards(0, 0, 0, 0, 2));
        ResolveWithReplay(host, "P1", Active(host, "tiles"));
        ResolveRewardWithReplay(host, 7); ResolveRewardWithReplay(host, 3); Drain(host);
        Assert.Equal(Cards(0, 0, 0, 1, 0, 0, 0, 1), host.GetHand("P1"));
        Assert.Equal(30, host.GetBank()[4]); Assert.Equal(2, host.State.Skills.Harvest.Single(a => a.PlayerId == "P1").ActuallyReceived); host.AssertConserved();
    }

    [Theory][InlineData("duplicate")][InlineData("wrong-sum")][InlineData("missing-tile")]
    public void TianduInvalidPairDoesNotReleaseOrdinaryReservation(string invalid)
    {
        var host = TianduHost(9); Begin(host, Cards(0, 0, 0, 0, 2));
        var pair = Active(host, "tiles");
        pair.TileIds = invalid == "duplicate" ? new[] { "A森林", "A森林" } : invalid == "wrong-sum" ? new[] { "A森林", "C村庄" } : new[] { "A森林", "不存在" };
        RejectUnchanged(host, "P1", pair);
        ResolveWithReplay(host, "P1", Active(host, "pass")); Drain(host);
        Assert.Equal(2, host.GetHand("P1")[4]); host.AssertConserved();
    }

    [Fact]
    public void TianduQualifiedSevenProducesOnlyReplacementAndNeverAqueduct()
    {
        var host = TianduHost(7); host.Tiles["P1"][1].Number = 3;
        Begin(host, Cards(), aqueduct: true); ResolveWithReplay(host, "P1", Active(host, "tiles")); Drain(host);
        Assert.Equal(2, host.GetHand("P1").Sum()); Assert.Equal(2, host.State.Skills.Harvest.Single(a => a.PlayerId == "P1").ActuallyReceived); host.AssertConserved();
    }

    [Fact]
    public void TianduZeroReplacementKeepsNonSevenAqueductEligibilityAndDoesNotRepeatReplacement()
    {
        var host = TianduHost(9); host.EmptyBank();
        host.RemoveHand("P3", Cards(0, 0, 1)); host.AddBank(Cards(0, 0, 1));
        Begin(host, Cards(), aqueduct: true); ResolveWithReplay(host, "P1", Active(host, "tiles")); Drain(host);
        Assert.Equal(0, host.State.Skills.Harvest.Single(a => a.PlayerId == "P1").ActuallyReceived);
        Assert.Equal(Cards(0, 0, 1), host.GetHand("P1")); host.AssertConserved();
    }

    [Fact]
    public void TianduVillageCannotSelectCommodityAndFirstTileSelectionIsNotRepeatedAfterRestore()
    {
        var host = TianduHost(9); host.Tiles["P1"][0].AllowedCardTypes = new[] { 0 };
        Begin(host, Cards()); ResolveWithReplay(host, "P1", Active(host, "tiles"));
        var bad = Clone(B2SkillEngine.Actions(host, "P1").First().Command); bad.CardType = 7; RejectUnchanged(host, "P1", bad);
        ResolveRewardWithReplay(host, 0); ResolveRewardWithReplay(host, 3); Drain(host);
        Assert.Equal(Cards(1, 0, 0, 1), host.GetHand("P1")); host.AssertConserved();
    }

    [Fact]
    public void GoldIsReservedBeforeAnyReplacementAndTheSameHarvestCannotBeOpenedAgain()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g28"); host.EmptyBank(0, 5);
        host.RemoveHand("P3", Cards(1)); host.AddBank(Cards(1)); host.State.Clock.Phase = ThreeKingdomsPhase.Draw;
        B2SkillEngine.BeginHarvest(host, new[] { Allocation("P1", Cards()), new B2HarvestAllocation { PlayerId = "P2", GoldCount = 1 } });
        Assert.Equal("P2", host.State.Skills.PendingDecision.PlayerId);
        ResolveWithReplay(host, "P2", B2SkillEngine.Actions(host, "P2").Single().Command);
        ResolveWithReplay(host, "P1", Active(host, "shelie")); Drain(host);
        Assert.Equal(0, host.GetHand("P1").Sum()); Assert.Equal(1, host.GetHand("P2")[0]);
        string before = host.Snapshot();
        Assert.Throws<InvalidOperationException>(() => B2SkillEngine.BeginHarvest(host, new[] { Allocation("P1", Cards()) }));
        Assert.Equal(before, host.Snapshot()); host.AssertConserved();
    }

    [Fact]
    public void AllGoldReservationsPrecedeDiceSkillRewardsSoJizhiCannotTakeAnotherPlayersLastResource()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g33"); host.EmptyBank(0, 5);
        host.RemoveHand("P3", Cards(1)); host.AddBank(Cards(1)); host.State.Clock.Phase = ThreeKingdomsPhase.Draw;
        B2SkillEngine.BeginHarvest(host, new[] { Allocation("P1", Cards()), new B2HarvestAllocation { PlayerId = "P2", GoldCount = 1 } }, pump: false);
        B2SkillEngine.OnDiceResolved(host, "真实骰局后技能", false, Array.Empty<string>());
        Assert.Equal("P2", host.State.Skills.PendingDecision.PlayerId);
        Assert.Equal("gold", host.State.Skills.PendingDecision.Kind);
        ResolveWithReplay(host, "P2", B2SkillEngine.Actions(host, "P2").Single().Command); Drain(host);
        Assert.Equal(0, host.GetHand("P1").Sum()); Assert.Equal(1, host.GetHand("P2")[0]); Assert.Equal(0, host.RandomCalls); host.AssertConserved();
    }

    [Theory][InlineData("tk-g01")][InlineData("tk-g28")][InlineData("tk-g37")]
    public void OldJushouBlockPreventsAllDrawReplacementsAndOrdinaryAllocation(string general)
    {
        var host = ThreeKingdomsB2TestHost.Create(general); host.Give("P2", 1);
        host.State.Skills.TimedEffects = new[] { new B2TimedEffect { Id = "旧据守", Kind = "draw-block", PlayerId = "P1" } };
        Begin(host, Cards(1), aqueduct: true); Drain(host);
        Assert.Equal(0, host.GetHand("P1").Sum()); Assert.Equal(0, host.RandomCalls); host.AssertConserved();
    }

    private static ThreeKingdomsB2TestHost TianduHost(int total)
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g37"); host.FinalDiceTotal = total;
        host.Tiles["P1"] = new[] {
            new B2HarvestTile { TileId = "A森林", Number = 4, AllowedCardTypes = new[] { 0, 7 } },
            new B2HarvestTile { TileId = "B麦田", Number = 5, AllowedCardTypes = new[] { 3 } },
            new B2HarvestTile { TileId = "C村庄", Number = 2, AllowedCardTypes = new[] { 2 } }
        };
        return host;
    }
    private static void Begin(ThreeKingdomsB2TestHost host, int[] cards, bool aqueduct = false)
    {
        host.State.Clock.Phase = ThreeKingdomsPhase.Draw;
        var allocation = Allocation("P1", cards); allocation.AqueductEligible = aqueduct;
        B2SkillEngine.BeginHarvest(host, new[] { allocation });
    }
    private static B2HarvestAllocation Allocation(string player, int[] cards) => new() { PlayerId = player, Cards = cards };
}
