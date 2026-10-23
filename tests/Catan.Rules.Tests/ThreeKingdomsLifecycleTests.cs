using System.Runtime.Serialization.Json;
using System.Text;
using Catan.Core.ThreeKingdoms;
using Xunit;

namespace Catan.Rules.Tests;

/// <summary>依据 B0 §3–4 验证固定座位时钟；受控抽取是测试夹具，不更改产品随机规则。</summary>
public sealed class ThreeKingdomsLifecycleTests
{
    private static int First(int maximum) => 0;
    private static ThreeKingdomsState Ready(int seats = 3)
    {
        var state = ThreeKingdomsEngine.Create(Enumerable.Range(1, seats).Select(i => "P" + i).ToArray(), 97, First);
        while (!state.OpeningComplete) Resolve(state);
        ThreeKingdomsEngine.BeginNormalTurns(state);
        Assert.Equal(ThreeKingdomsPhase.Preparation, state.Clock.Phase);
        return state;
    }
    private static void Resolve(ThreeKingdomsState state, Func<int, int>? draw = null, string? preferred = null)
    {
        draw ??= First;
        var d = state.PendingDecision;
        var choices = d.CandidateIds.OrderBy(id => id == preferred ? 0 : 1).Take(d.RequiredCount).ToArray();
        if (d.Kind == ThreeKingdomsDecisionKind.SelectOpening || d.Kind == ThreeKingdomsDecisionKind.SelectReplacement)
            ThreeKingdomsEngine.ChooseGenerals(state, d.PlayerId, choices, d.DecisionId, d.WindowId, draw);
        else ThreeKingdomsEngine.DeployGeneral(state, d.PlayerId, choices[0], d.DecisionId, d.WindowId, draw);
    }
    private static GeneralInstance Active(ThreeKingdomsState state, string player = "P1") => state.Generals.Single(g => g.Region == GeneralRegion.Active && g.OwnerPlayerId == player);
    private static void End(ThreeKingdomsState state, Func<int, int>? draw = null)
    {
        state.Clock.Phase = ThreeKingdomsPhase.End;
        ThreeKingdomsEngine.CompleteNormalTurn(state, draw ?? First);
    }
    private static void EndRound(ThreeKingdomsState state, int seats = 3)
    {
        for (int i = 0; i < seats; i++) End(state);
    }
    private static string Json<T>(T value)
    {
        using var stream = new MemoryStream(); new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value); return Encoding.UTF8.GetString(stream.ToArray());
    }
    private static ThreeKingdomsState Copy(ThreeKingdomsState state)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Json(state)));
        return (ThreeKingdomsState)new DataContractJsonSerializer(typeof(ThreeKingdomsState)).ReadObject(stream)!;
    }

    [Theory][InlineData(3)][InlineData(4)]
    public void ExactlyThreeNormalRoundsExpireAllSeatsInOrderAndNewTenuresKeepThreeLife(int seats)
    {
        var state = Ready(seats);
        string[] originalTenures = state.Generals.Where(g => g.Region == GeneralRegion.Active).Select(g => g.TenureId).ToArray();
        for (int round = 1; round <= 2; round++)
        {
            for (int seat = 0; seat < seats; seat++)
            {
                Assert.Equal("P" + (seat + 1), state.Clock.ActivePlayerId);
                End(state);
                Assert.All(state.Generals.Where(g => g.Region == GeneralRegion.Active), g => Assert.Equal(seat == seats - 1 ? 3 - round : 4 - round, g.RemainingLife));
            }
            Assert.Equal(round + 1, state.Clock.Round);
        }
        EndRound(state, seats);
        for (int seat = 0; seat < seats; seat++)
        {
            Assert.Equal("P" + (seat + 1), state.PendingDecision.PlayerId);
            Assert.Equal(ThreeKingdomsDecisionKind.SelectReplacement, state.PendingDecision.Kind);
            Resolve(state); Resolve(state);
            var current = Active(state, "P" + (seat + 1));
            Assert.Equal(3, current.RemainingLife);
            Assert.Equal(4, current.EnteredRound);
            Assert.DoesNotContain(current.TenureId, originalTenures);
        }
        Assert.Equal(4, state.Clock.Round);
        Assert.Equal(seats * 3, state.Clock.NormalOpportunitiesConsumed);
        Assert.Equal("P1", state.Clock.ActivePlayerId);
        Assert.Empty(ThreeKingdomsEngine.Validate(state));
        Assert.All(state.Generals.Where(g => g.Region == GeneralRegion.Hidden), g => { Assert.Equal(0, g.RemainingLife); Assert.Null(g.TenureId); });
    }

    [Fact]
    public void MidRoundDeploymentStillAgesAtThatRoundsEndAndHiddenGeneralDoesNot()
    {
        var state = Ready();
        string hidden = state.Generals.Single(g => g.Region == GeneralRegion.Hidden && g.OwnerPlayerId == "P1").GeneralId;
        ThreeKingdomsEngine.RetireGeneral(state, "P1", state.WindowId, First);
        Resolve(state); Resolve(state, preferred: hidden);
        Assert.Equal(hidden, Active(state).GeneralId);
        Assert.Equal(1, Active(state).EnteredRound);
        Assert.Equal(3, Active(state).RemainingLife);
        EndRound(state);
        Assert.Equal(2, Active(state).RemainingLife);
        Assert.Equal(1, Active(state).LastLifeDecrementRound);
        Assert.All(state.Generals.Where(g => g.Region == GeneralRegion.Hidden), g => { Assert.Equal(0, g.RemainingLife); Assert.Null(g.TenureId); });
        Assert.Empty(ThreeKingdomsEngine.Validate(state));
    }

    [Fact]
    public void SameEntityCanBeRetiredAndRedrawnRepeatedlyWithNewTenureAndResetCounters()
    {
        var state = Ready();
        string target = Active(state).GeneralId, instance = Active(state).InstanceId;
        var tenures = new HashSet<string> { Active(state).TenureId };
        for (int repetition = 0; repetition < 20; repetition++)
        {
            Active(state).Counters = new[] { new GeneralCounter { Key = "本任限定", Value = 1, TurnInstanceId = state.Clock.TurnInstanceId } };
            Func<int, int> returnTarget = maximum =>
            {
                var pool = state.Generals.Where(g => g.Region == GeneralRegion.Pool).OrderBy(g => g.GeneralId, StringComparer.Ordinal).Select(g => g.GeneralId).ToArray();
                return Math.Max(0, Array.IndexOf(pool, target));
            };
            ThreeKingdomsEngine.RetireGeneral(state, "P1", state.WindowId, returnTarget);
            Assert.Contains(target, state.PendingDecision.CandidateIds);
            Resolve(state, returnTarget, target); Resolve(state, returnTarget, target);
            Assert.Equal(instance, Active(state).InstanceId);
            Assert.True(tenures.Add(Active(state).TenureId));
            Assert.Empty(Active(state).Counters);
            Assert.Equal(3, Active(state).RemainingLife);
            Assert.Equal(54, state.Generals.Select(g => g.InstanceId).Distinct().Count());
            Assert.Empty(ThreeKingdomsEngine.Validate(state));
        }
    }

    [Fact]
    public void SuspendedParentEffectSurvivesEveryReplacementDecisionAndOldWindowsExpire()
    {
        var state = Ready();
        string oldWindow = state.WindowId;
        state.EffectFrames = new[] { new ThreeKingdomsEffectFrame {
            EffectId = "父效果", ParentEffectId = "根效果", RootEventId = "根事件", SourceTenureId = Active(state).TenureId,
            SkillId = "未来技能测试夹具", PlayerId = "P2", Step = 7, CostPaid = true,
            CompletedBranches = new[] { "已付费用", "已完成第一目标" }, Targets = new[] { "P2", "P3" },
            TurnInstanceId = state.Clock.TurnInstanceId, PhaseId = state.Clock.PhaseId, ReturnWindowId = oldWindow
        } };
        string parent = Json(state.EffectFrames);
        ThreeKingdomsEngine.RetireGeneral(state, "P1", oldWindow, First);
        Assert.Equal(parent, Json(state.EffectFrames.Where(f => f.SkillId != "tk-lifecycle-replacement").ToArray()));
        Assert.NotEqual(oldWindow, state.WindowId);
        string firstDecision = state.PendingDecision.DecisionId;
        string during = Json(state);
        Assert.Throws<InvalidOperationException>(() => ThreeKingdomsEngine.RetireGeneral(state, "P1", state.WindowId, First));
        Assert.Equal(during, Json(state));
        var restored = Copy(state);
        Resolve(state); Resolve(restored); Assert.Equal(Json(state), Json(restored));
        Assert.NotEqual(firstDecision, state.PendingDecision.DecisionId);
        Resolve(state); Resolve(restored); Assert.Equal(Json(state), Json(restored));
        Assert.Equal(parent, Json(state.EffectFrames));
        Assert.Equal(ThreeKingdomsPhase.Preparation, state.Clock.Phase);
        string final = Json(state);
        Assert.Throws<InvalidOperationException>(() => ThreeKingdomsEngine.RetireGeneral(state, "P1", oldWindow, First));
        Assert.Equal(final, Json(state));
    }

    [Fact]
    public void UnauthorizedProjectionIsInvariantUnderSecretCandidateAndHiddenIdentitySwaps()
    {
        var state = ThreeKingdomsEngine.Create(new[] { "P1", "P2", "P3" }, 97, First);
        string before = Json(ThreeKingdomsEngine.Project(state, "P2"));
        var selected = state.Generals.First(g => g.Region == GeneralRegion.Candidate);
        var pool = state.Generals.First(g => g.Region == GeneralRegion.Pool);
        string old = selected.GeneralId;
        (selected.GeneralId, pool.GeneralId) = (pool.GeneralId, selected.GeneralId);
        state.PendingDecision.CandidateIds = state.PendingDecision.CandidateIds.Select(id => id == old ? selected.GeneralId : id).ToArray();
        Assert.Equal(before, Json(ThreeKingdomsEngine.Project(state, "P2")));
        while (!state.OpeningComplete) Resolve(state);
        before = Json(ThreeKingdomsEngine.Project(state, "P2"));
        selected = state.Generals.First(g => g.Region == GeneralRegion.Hidden && g.OwnerPlayerId == "P1");
        pool = state.Generals.First(g => g.Region == GeneralRegion.Pool);
        (selected.GeneralId, pool.GeneralId) = (pool.GeneralId, selected.GeneralId);
        Assert.Equal(before, Json(ThreeKingdomsEngine.Project(state, "P2")));
    }

    [Fact]
    public void CatalogPreservesAll54DefinitionsAnd97SkillsWithOnlyElevenCompleteB2GeneralsEnabled()
    {
        Assert.Equal(54, GeneralCatalog.Definitions.Length);
        Assert.Equal(97, GeneralCatalog.Definitions.Sum(g => g.Skills.Length));
        Assert.Equal(Enumerable.Range(1, 54).Select(i => "tk-g" + i.ToString("D2")), GeneralCatalog.Definitions.Select(g => g.Id));
        string[] enabled = { "tk-g01", "tk-g06", "tk-g09", "tk-g12", "tk-g15", "tk-g22", "tk-g23", "tk-g28", "tk-g33", "tk-g37", "tk-g43" };
        Assert.Equal(enabled, GeneralCatalog.EnabledDefinitions.Select(g => g.Id));
        Assert.Equal(15, GeneralCatalog.EnabledDefinitions.Sum(g => g.Skills.Length));
        Assert.All(GeneralCatalog.Definitions, g => { Assert.Equal(enabled.Contains(g.Id), g.SkillsEnabled); Assert.NotEmpty(g.SourceHash); Assert.All(g.Skills, s => Assert.Equal(g.SkillsEnabled, s.Enabled)); });
        Assert.NotEqual(GeneralCatalog.Get("tk-g08").Id, GeneralCatalog.Get("tk-g17").Id);
        Assert.Contains("骨架", GeneralCatalog.PackageLabel);
    }

    [Fact]
    public void SkippedNormalOpportunityConsumesSeatAndRoundButCreatesNoRealTurn()
    {
        var state = Ready();
        long instance = state.Clock.TurnInstanceId;
        state.Players.Single(p => p.PlayerId == "P2").FaceDown = true;
        End(state);
        Assert.Equal("P3", state.Clock.ActivePlayerId);
        Assert.Equal(2, state.Clock.NormalOpportunitiesConsumed);
        Assert.Equal(instance + 1, state.Clock.TurnInstanceId);
        Assert.Equal(new[] { "P2" }, state.Clock.SkippedOpportunityPlayerIds);
        Assert.False(state.Players.Single(p => p.PlayerId == "P2").FaceDown);
        Assert.All(state.Generals.Where(g => g.Region == GeneralRegion.Active), g => Assert.Equal(3, g.RemainingLife));
        End(state);
        Assert.Equal(2, state.Clock.Round);
        Assert.Equal(3, state.Clock.NormalOpportunitiesConsumed);
        Assert.All(state.Generals.Where(g => g.Region == GeneralRegion.Active), g => Assert.Equal(2, g.RemainingLife));
        Assert.Empty(ThreeKingdomsEngine.Validate(state));
    }

    [Fact]
    public void LastSeatQueuedExtrasRunFifoBeforeRoundAgingAndSkippedExtraConsumesNoNormalSeat()
    {
        var state = Ready();
        End(state); End(state);
        Assert.Equal("P3", state.Clock.ActivePlayerId);
        ThreeKingdomsEngine.QueueExtraTurn(state, "P2");
        ThreeKingdomsEngine.QueueExtraTurn(state, "P1");
        ThreeKingdomsEngine.QueueExtraTurn(state, "P3");
        state.Players.Single(p => p.PlayerId == "P1").FaceDown = true;
        long turn = state.Clock.TurnInstanceId;
        End(state);
        Assert.True(state.Clock.IsExtraTurn);
        Assert.Equal("P2", state.Clock.ActivePlayerId);
        Assert.Equal(1, state.Clock.Round);
        Assert.Equal(3, state.Clock.NormalOpportunitiesConsumed);
        Assert.All(state.Generals.Where(g => g.Region == GeneralRegion.Active), g => Assert.Equal(3, g.RemainingLife));
        End(state);
        Assert.True(state.Clock.IsExtraTurn);
        Assert.Equal("P3", state.Clock.ActivePlayerId);
        Assert.Equal(3, state.Clock.NormalOpportunitiesConsumed);
        Assert.Equal(2, state.Clock.ExtraOpportunitiesConsumed);
        Assert.Equal(turn + 2, state.Clock.TurnInstanceId);
        Assert.Equal(new[] { "P1" }, state.Clock.SkippedOpportunityPlayerIds);
        End(state);
        Assert.False(state.Clock.IsExtraTurn);
        Assert.Equal("P1", state.Clock.ActivePlayerId);
        Assert.Equal(2, state.Clock.Round);
        Assert.Equal(3, state.Clock.ExtraOpportunitiesConsumed);
        Assert.Equal(3, state.Clock.NormalOpportunitiesConsumed);
        Assert.All(state.Generals.Where(g => g.Region == GeneralRegion.Active), g => Assert.Equal(2, g.RemainingLife));
        Assert.Empty(ThreeKingdomsEngine.Validate(state));
    }

    [Fact]
    public void EveryRealTurnHasOneGlobalDrawPhaseAndExtraTurnHasItsOwnPhaseIdentity()
    {
        var state = Ready();
        void CompletePhases()
        {
            foreach (var expected in new[] { ThreeKingdomsPhase.Judgment, ThreeKingdomsPhase.Roll })
            {
                ThreeKingdomsEngine.AdvancePhase(state, state.Clock.ActivePlayerId, state.WindowId);
                Assert.Equal(expected, state.Clock.Phase);
            }
            ThreeKingdomsEngine.MarkDrawPhase(state);
            Assert.Equal(ThreeKingdomsPhase.Draw, state.Clock.Phase);
            Assert.Equal(state.Clock.TurnInstanceId, state.Clock.DrawPhaseTurnInstanceId);
            string before = Json(state);
            Assert.Throws<InvalidOperationException>(() => ThreeKingdomsEngine.MarkDrawPhase(state));
            Assert.Equal(before, Json(state));
            ThreeKingdomsEngine.AdvancePhase(state, state.Clock.ActivePlayerId, state.WindowId);
            Assert.Equal(ThreeKingdomsPhase.Play, state.Clock.Phase);
            ThreeKingdomsEngine.AdvancePhase(state, state.Clock.ActivePlayerId, state.WindowId);
            Assert.Equal(ThreeKingdomsPhase.End, state.Clock.Phase);
        }
        CompletePhases();
        long firstTurn = state.Clock.TurnInstanceId, firstPhase = state.Clock.PhaseId;
        ThreeKingdomsEngine.QueueExtraTurn(state, "P1");
        ThreeKingdomsEngine.CompleteNormalTurn(state, First);
        Assert.True(state.Clock.IsExtraTurn);
        Assert.NotEqual(firstTurn, state.Clock.TurnInstanceId);
        Assert.True(state.Clock.PhaseId > firstPhase);
        CompletePhases();
        Assert.Equal(1, state.Clock.Round);
    }

    [Fact]
    public void CloneOwnsEveryNestedMutableCollectionIncludingSuspendedEffectAndRoundQueue()
    {
        var state = Ready();
        Active(state).Counters = new[] { new GeneralCounter { Key = "次数", Value = 1 } };
        state.EffectFrames = new[] { new ThreeKingdomsEffectFrame { EffectId = "父", SkillId = "测试", Targets = new[] { "P2" }, CompletedBranches = new[] { "已付" } } };
        state.ProcessedTriggerIds = new[] { "事件:技能:任次" };
        state.RoundEndTenureSnapshot = new[] { Active(state).TenureId };
        state.ExpiredPlayerQueue = new[] { "P3" };
        ThreeKingdomsEngine.QueueExtraTurn(state, "P2");
        string before = Json(state);
        var clone = ThreeKingdomsEngine.Clone(state);
        Assert.Equal(before, Json(clone));
        clone.Generals[0].Counters[0].Value = 99;
        clone.Players[0].FaceDown = true;
        clone.SeatPlayerIds[0] = "改席位";
        clone.GeneralDrawOrder[0] = "改抽序";
        clone.Clock.ExtraTurnQueue[0] = "P3";
        clone.EffectFrames[0].Targets[0] = "P3";
        clone.EffectFrames[0].CompletedBranches[0] = "未付";
        clone.ProcessedTriggerIds[0] = "新触发";
        clone.RoundEndTenureSnapshot[0] = "新任次";
        clone.ExpiredPlayerQueue[0] = "P2";
        Assert.Equal(before, Json(state));
        var opening = ThreeKingdomsEngine.Create(new[] { "P1", "P2", "P3" }, 97, First);
        before = Json(opening);
        ThreeKingdomsEngine.Clone(opening).PendingDecision.CandidateIds[0] = "改候选";
        Assert.Equal(before, Json(opening));
    }

    [Fact]
    public void FutureRetainedCardsLeaveOneReturnedEntitySoReplacementDealsOneWithoutInventingCards()
    {
        var state = Ready();
        foreach (var general in state.Generals.Where(g => g.Region == GeneralRegion.Pool)) { general.Region = GeneralRegion.Retained; general.OwnerPlayerId = "P2"; }
        string returned = Active(state).GeneralId;
        ThreeKingdomsEngine.RetireGeneral(state, "P1", state.WindowId, First);
        Assert.Equal(new[] { returned }, state.PendingDecision.CandidateIds);
        Assert.Equal(1, state.PendingDecision.RequiredCount);
        Resolve(state); Resolve(state);
        Assert.Equal(54, state.Generals.Length);
        Assert.Equal(54, state.Generals.Select(g => g.InstanceId).Distinct().Count());
        Assert.Empty(ThreeKingdomsEngine.Validate(state));
    }

    [Fact]
    public void InvalidMissingActiveEntityCannotPassAuthorityValidation()
    {
        var state = Ready();
        var active = Active(state);
        active.Region = GeneralRegion.Pool; active.OwnerPlayerId = null; active.TenureId = null;
        active.RemainingLife = 0; active.EnteredRound = 0; active.LastLifeDecrementRound = 0;
        Assert.NotEmpty(ThreeKingdomsEngine.Validate(state));
        state = Ready(); state.Generals[1].InstanceId = state.Generals[0].InstanceId;
        Assert.NotEmpty(ThreeKingdomsEngine.Validate(state));
    }
}
