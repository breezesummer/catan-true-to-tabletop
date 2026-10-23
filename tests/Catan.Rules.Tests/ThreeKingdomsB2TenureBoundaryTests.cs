using Catan.Core.ThreeKingdoms;
using Xunit;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB2TenureBoundaryTests
{
    [Fact]
    public void RaidRevalidationMayReselectOrReduceButCannotIncreaseTheCommittedReplacementCount()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g01", "tk-g37");
        host.Give("P2", 1); host.Give("P3", 1);
        host.State.Clock.Phase = ThreeKingdomsPhase.Draw;
        B2SkillEngine.BeginHarvest(host, new[] { new B2HarvestAllocation { PlayerId = "P1", Cards = new[] { 2, 0, 0, 0, 0, 0, 0, 0 } } });
        var raid = B2SkillEngine.Actions(host, "P1").First(a => a.Command.Option == "raid" && a.Command.Counts.Sum() == 1 && a.Command.TargetPlayerIds.SequenceEqual(new[] { "P2" })).Command;
        B2SkillEngine.Resolve(host, "P1", raid);
        Assert.Equal("P2", host.State.Skills.PendingDecision.PlayerId);
        host.State.Skills.TimedEffects = new[] { new B2TimedEffect { Id = "响应中新保护", Kind = "protection", PlayerId = "P2", OpportunityId = host.State.Clock.OpportunityId } };
        B2SkillEngine.Resolve(host, "P2", B2SkillEngine.Actions(host, "P2").First().Command);
        Assert.Equal("raid-revalidate", host.State.Skills.PendingDecision.Kind);
        host.State.Skills.TimedEffects = Array.Empty<B2TimedEffect>();
        Assert.DoesNotContain(B2SkillEngine.Actions(host, "P1"), action => action.Command.Option == "raid" && action.Command.Counts.Sum() > 1);
        var d = host.State.Skills.PendingDecision;
        var invalid = new B2SkillCommand { SkillId = "tk-g01-s1", Option = "raid", WindowId = host.State.WindowId, DecisionId = d.DecisionId,
            Counts = new[] { 2, 0, 0, 0, 0, 0, 0, 0 }, TargetPlayerIds = new[] { "P2", "P3" } };
        string before = host.Snapshot();
        Assert.Throws<InvalidOperationException>(() => B2SkillEngine.Resolve(host, "P1", invalid));
        Assert.Equal(before, host.Snapshot());
        B2SkillEngine.Resolve(host, "P1", B2SkillEngine.Actions(host, "P1").First(a => a.Command.Option == "pass").Command);
        Assert.Equal(2, host.GetHand("P1")[0]); host.AssertConserved();
    }

    [Fact]
    public void SavedSkippedOpportunityCursorPreventsHistoricalSkipsFromClearingNewJushou()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g06");
        host.State.Clock.SkippedOpportunityPlayerIds = new[] { "P1" };
        host.State.Skills.ProcessedSkippedCount = 1;
        host.State.Skills.TimedEffects = new[] { new B2TimedEffect { Id = "后来据守", Kind = "draw-block", PlayerId = "P1" } };
        host.State.Skills = B2SkillEngine.Clone(host.State.Skills);
        Assert.Equal(1, host.State.Skills.ProcessedSkippedCount);
        B2SkillEngine.OnOpportunityStart(host, 2, "P2", host.State.Clock.SkippedOpportunityPlayerIds.Skip(host.State.Skills.ProcessedSkippedCount).ToArray());
        Assert.True(B2SkillEngine.IsDrawBlocked(host.State, "P1"));
        host.State.Clock.SkippedOpportunityPlayerIds = new[] { "P1", "P1" };
        B2SkillEngine.OnOpportunityStart(host, 4, "P3", host.State.Clock.SkippedOpportunityPlayerIds.Skip(host.State.Skills.ProcessedSkippedCount).ToArray());
        host.State.Skills.ProcessedSkippedCount = host.State.Clock.SkippedOpportunityPlayerIds.Length;
        Assert.False(B2SkillEngine.IsDrawBlocked(host.State, "P1"));
        Assert.Empty(B2SkillEngine.Validate(host));
        host.State.Skills.ProcessedSkippedCount = 3;
        Assert.Contains("技能跳过机会游标无效。", B2SkillEngine.Validate(host));
        host.State.Skills.ProcessedSkippedCount = -1;
        Assert.Contains("技能跳过机会游标无效。", B2SkillEngine.Validate(host));
    }

    [Fact]
    public void UnacceptedDrawReplacementDisappearsAfterRetirementAndOriginalReservedCardsAreStillClaimed()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g28");
        host.State.Clock.Phase = ThreeKingdomsPhase.Draw;
        B2SkillEngine.BeginHarvest(host, new[] { new B2HarvestAllocation { PlayerId = "P1", Cards = new[] { 2, 0, 0, 0, 0, 0, 0, 0 } } });
        Assert.Equal("harvest", host.State.Skills.PendingDecision.Kind);
        var retiredAction = B2SkillEngine.Actions(host, "P1").First(a => a.Command.Option == "shelie").Command;
        host.State.Generals[0].TenureId = "吕蒙重新登场";
        host.State.WindowId = "换将后的新窗口";
        B2SkillEngine.RefreshDecision(host.State);
        B2SkillEngine.Pump(host);
        Assert.False(B2SkillEngine.HasPending(host.State));
        Assert.Equal(2, host.GetHand("P1")[0]);
        Assert.Equal(0, host.RandomCalls);
        string before = host.Snapshot();
        Assert.Throws<InvalidOperationException>(() => B2SkillEngine.Resolve(host, "P1", retiredAction));
        Assert.Equal(before, host.Snapshot()); host.AssertConserved();
    }

    [Fact]
    public void EveryRendeRewardEarnedByOneCommittedBatchSurvivesRetirementWhileFirstRewardIsPending()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g15");
        B2SkillEngine.OnCardsMoved(host, "一次交出四张", new[] { new B2HandChange { PlayerId = "P1", BeforeCount = 4, AfterCount = 0, LostCount = 4, GivenCount = 4 } });
        Assert.Equal(2, host.State.Skills.Frames.Length);
        host.State.Generals[0].TenureId = "刘备重新登场";
        host.State.Generals[0].Counters = Array.Empty<GeneralCounter>();
        host.State.WindowId = "新的换将窗口";
        B2SkillEngine.RefreshDecision(host.State); B2SkillEngine.Pump(host);
        for (int i = 0; i < 2; i++)
        {
            var command = B2SkillEngine.Actions(host, "P1").First(a => a.Command.CardType == 0).Command;
            B2SkillEngine.Resolve(host, "P1", command);
        }
        Assert.False(B2SkillEngine.HasPending(host.State));
        Assert.Equal(2, host.GetHand("P1")[0]);
        Assert.Empty(host.State.Generals[0].Counters);
        host.AssertConserved();
    }
}
