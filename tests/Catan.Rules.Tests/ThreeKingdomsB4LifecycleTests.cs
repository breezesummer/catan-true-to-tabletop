using System.Runtime.Serialization.Json;
using System.Text;
using Catan.Core.ThreeKingdoms;
using Xunit;

namespace Catan.Rules.Tests;

/// <summary>依据 B4 同批选将、独立隐退库和耗尽边界合同。</summary>
public sealed class ThreeKingdomsB4LifecycleTests
{
    private static int First(int count) => 0;
    private static ThreeKingdomsState Create(int seats = 3) => ThreeKingdomsEngine.Create(
        Enumerable.Range(1, seats).Select(i => "P" + i).ToArray(), 71, First, true, GeneralCatalog.B4PackageVersion);
    private static ThreeKingdomsState Ready(int seats = 3)
    {
        var state = Create(seats);
        ResolveAll(state);
        ThreeKingdomsEngine.BeginNormalTurns(state);
        return state;
    }
    private static void Resolve(ThreeKingdomsState state, string player)
    {
        var d = ThreeKingdomsEngine.GetPendingDecision(state, player)!;
        if (d.Kind is ThreeKingdomsDecisionKind.SelectOpening or ThreeKingdomsDecisionKind.SelectReplacement)
            ThreeKingdomsEngine.ChooseGenerals(state, player, d.CandidateIds.Take(d.RequiredCount).ToArray(), d.DecisionId, d.WindowId, First);
        else ThreeKingdomsEngine.DeployGeneral(state, player, d.CandidateIds[0], d.DecisionId, d.WindowId, First);
    }
    private static void ResolveAll(ThreeKingdomsState state)
    {
        while (state.PendingDecision != null)
        {
            Assert.Empty(ThreeKingdomsEngine.Validate(state));
            Resolve(state, state.PendingDecision.PlayerId);
        }
        Assert.Empty(ThreeKingdomsEngine.Validate(state));
    }
    private static void End(ThreeKingdomsState state)
    {
        state.Clock.Phase = ThreeKingdomsPhase.End;
        ThreeKingdomsEngine.CompleteNormalTurn(state, First);
    }
    private static string Json<T>(T value)
    {
        using var stream = new MemoryStream();
        new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    private static ThreeKingdomsState Restore(ThreeKingdomsState state)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Json(state)));
        return (ThreeKingdomsState)new DataContractJsonSerializer(typeof(ThreeKingdomsState)).ReadObject(stream)!;
    }

    [Theory][InlineData(3, 12, false)][InlineData(3, 13, true)][InlineData(4, 16, false)][InlineData(4, 17, true)]
    public void SimultaneousOpeningRequiresFullCandidateSupply(int seats, int cards, bool allowed)
    {
        if (allowed) ThreeKingdomsEngine.RequireOpeningPoolSize(cards, seats, true);
        else Assert.Throws<InvalidOperationException>(() => ThreeKingdomsEngine.RequireOpeningPoolSize(cards, seats, true));
    }

    [Fact]
    public void FourPlayerOpeningNeverFillsPoolWithIncompleteDefinitions()
    {
        if (GeneralCatalog.B4Definitions.Length < 17)
        {
            Assert.Throws<InvalidOperationException>(() => Create(4));
            return;
        }
        var state = Create(4);
        Assert.Equal(4, state.PendingDecisions.Length);
        Assert.Equal(new[] { 4, 4, 4, 5 }, state.PendingDecisions.Select(d => d.CandidateCount));
        Assert.Equal(17, state.PendingDecisions.SelectMany(d => d.CandidateIds).Distinct().Count());
        ResolveAll(state);
    }

    [Fact]
    public void EverySeatReceivesPrivateCandidateImmediatelyAndUsesIndependentStableTokens()
    {
        var state = Create();
        var second = ThreeKingdomsEngine.GetPendingDecision(state, "P2")!;
        string secondBefore = Json(second);
        Assert.Equal(new[] { 4, 4, 5 }, state.PendingDecisions.Select(d => d.CandidateCount));
        Assert.Equal(13, state.Generals.Count(g => g.Region == GeneralRegion.Candidate));
        Assert.Equal(13, state.GeneralDrawOrder.Length);
        foreach (var player in state.SeatPlayerIds)
        {
            var view = ThreeKingdomsEngine.Project(state, player);
            Assert.Equal(player, view.PendingDecision.PlayerId);
            Assert.Equal(view.PendingDecision.WindowId, view.WindowId);
            Assert.All(view.Players.Where(p => p.PlayerId != player), p => Assert.Empty(p.HiddenGeneralIds));
        }
        Resolve(state, "P3"); Resolve(state, "P3");
        Resolve(state, "P1"); Resolve(state, "P1");
        Assert.Equal(secondBefore, Json(ThreeKingdomsEngine.GetPendingDecision(state, "P2")));
        Assert.Null(ThreeKingdomsEngine.Project(state, "P3").PendingDecision);
        Assert.DoesNotContain(state.Generals,g => g.Region == GeneralRegion.Active);
        ThreeKingdomsEngine.ChooseGenerals(state, "P2", second.CandidateIds.Take(2).ToArray(), second.DecisionId, second.WindowId, First);
        Resolve(state, "P2");
        Assert.True(state.OpeningComplete);
        Assert.Equal(3, state.Generals.Count(g => g.Region == GeneralRegion.Active));
        Assert.Empty(ThreeKingdomsEngine.Validate(state));
    }

    [Fact]
    public void AnySubmissionOrderProducesSameDrawsAndFinalState()
    {
        string? canonical = null;
        foreach (var order in new[] { new[] { "P1", "P2", "P3" }, new[] { "P3", "P1", "P2" }, new[] { "P2", "P3", "P1" } })
        {
            var state = Create();
            foreach (string player in order) { Resolve(state, player); Resolve(state, player); }
            Assert.Empty(ThreeKingdomsEngine.Validate(state));
            canonical ??= Json(state);
            Assert.Equal(canonical, Json(state));
        }
    }

    [Fact]
    public void ExpiredGeneralsRetireAsOneBatchAndStayPrivateUntilEveryoneDeploys()
    {
        var state = Ready();
        var oldGenerals = state.Generals.Where(g => g.Region == GeneralRegion.Active).Select(g => g.GeneralId).ToArray();
        for (int i = 0; i < 9; i++) End(state);
        Assert.Equal(ThreeKingdomsPhase.RoundEnd, state.Clock.Phase);
        Assert.Equal(3, state.PendingDecisions.Length);
        Assert.DoesNotContain(state.Generals,g => g.Region == GeneralRegion.Active);
        Assert.All(oldGenerals, id => Assert.Equal(GeneralRegion.Retired, state.Generals.Single(g => g.GeneralId == id).Region));
        string p1 = Json(ThreeKingdomsEngine.GetPendingDecision(state, "P1"));
        Resolve(state, "P2"); Resolve(state, "P2");
        Assert.Equal(p1, Json(ThreeKingdomsEngine.GetPendingDecision(state, "P1")));
        Assert.DoesNotContain(state.Generals,g => g.Region == GeneralRegion.Active);
        Assert.Empty(ThreeKingdomsEngine.Validate(state));
        var restored = Restore(state);
        ResolveAll(state); ResolveAll(restored);
        Assert.Equal(Json(state), Json(restored));
        Assert.Equal(4, state.Clock.Round);
        Assert.All(state.Generals.Where(g => g.Region == GeneralRegion.Active), g =>
        { Assert.Equal(3, g.RemainingLife); Assert.Equal(4, g.EnteredRound); });
    }

    [Fact]
    public void OrdinaryRetirementStaysOutOfNonemptyPublicPoolAndDiscardedCandidatesReturnDirectly()
    {
        var state = Ready();
        string retiredId = state.Generals.Single(g => g.Region == GeneralRegion.Active && g.OwnerPlayerId == "P1").GeneralId;
        ThreeKingdomsEngine.RetireGeneral(state, "P1", state.WindowId, First);
        Assert.Equal(GeneralRegion.Retired, state.Generals.Single(g => g.GeneralId == retiredId).Region);
        Assert.DoesNotContain(retiredId, state.PendingDecision.CandidateIds);
        Assert.Equal(0, state.RetirementRecycleCount);
        string rejected = state.PendingDecision.CandidateIds[1];
        Resolve(state, "P1");
        Assert.Equal(GeneralRegion.Pool, state.Generals.Single(g => g.GeneralId == rejected).Region);
        Resolve(state, "P1");
        Assert.Empty(ThreeKingdomsEngine.Validate(state));
    }

    [Fact]
    public void DrawingTwoWithOnePublicCardConsumesItBeforeRecyclingAndNeverRecyclesRetainedCards()
    {
        var state = Ready();
        var pool = state.Generals.Where(g => g.Region == GeneralRegion.Pool).ToArray();
        string first = pool[0].GeneralId;
        foreach (var g in pool.Skip(1)) { g.Region = GeneralRegion.Retained; g.OwnerPlayerId = "P2"; }
        string old = state.Generals.Single(g => g.Region == GeneralRegion.Active && g.OwnerPlayerId == "P1").GeneralId;
        var samples = new List<int>();
        ThreeKingdomsEngine.RetireGeneral(state, "P1", state.WindowId, count => { samples.Add(count); return 0; });
        Assert.Equal(new[] { first, old }, state.PendingDecision.CandidateIds);
        Assert.Equal(new[] { 1, 1 }, samples);
        Assert.Equal(1, state.RetirementRecycleCount);
        Assert.Equal(pool.Length - 1, state.Generals.Count(g => g.Region == GeneralRegion.Retained));
        Assert.DoesNotContain(state.Generals,g => g.Region == GeneralRegion.Retired);
        ResolveAll(state);
    }

    [Fact]
    public void ShortSupplyOffersOneWithoutCreatingOrDuplicatingEntities()
    {
        var state = Ready();
        foreach (var g in state.Generals.Where(g => g.Region == GeneralRegion.Pool)) { g.Region = GeneralRegion.Retained; g.OwnerPlayerId = "P2"; }
        ThreeKingdomsEngine.RetireGeneral(state, "P1", state.WindowId, First);
        Assert.Single(state.PendingDecision.CandidateIds);
        Assert.Equal(1, state.PendingDecision.RequiredCount);
        ResolveAll(state);
        Assert.Equal(GeneralCatalog.B4Definitions.Length, state.Generals.Select(g => g.InstanceId).Distinct().Count());
    }

    [Fact]
    public void PartialOpeningRestoresAndClonesEveryDecisionWithoutSharingMutableArrays()
    {
        var state = Create();
        Resolve(state, "P2"); Resolve(state, "P3"); Resolve(state, "P3");
        var restored = Restore(state);
        var cloned = ThreeKingdomsEngine.Clone(state);
        Assert.Equal(Json(state), Json(restored)); Assert.Equal(Json(state), Json(cloned));
        cloned.PendingDecisions[0].CandidateIds[0] = "非法实体";
        Assert.NotEqual(cloned.PendingDecisions[0].CandidateIds[0], state.PendingDecisions[0].CandidateIds[0]);
        Assert.NotEmpty(ThreeKingdomsEngine.Validate(cloned));
        ResolveAll(state); ResolveAll(restored);
        Assert.Equal(Json(state), Json(restored));
    }

    [Fact]
    public void ForgedSeatsExpiredTokensAndInvalidCountsAreRejectedWithoutMutation()
    {
        var state = Create();
        var d = ThreeKingdomsEngine.GetPendingDecision(state, "P2")!;
        string before = Json(state);
        Assert.Throws<InvalidOperationException>(() => ThreeKingdomsEngine.ChooseGenerals(state, "P1", d.CandidateIds.Take(2).ToArray(), d.DecisionId, d.WindowId, First));
        Assert.Throws<InvalidOperationException>(() => ThreeKingdomsEngine.ChooseGenerals(state, "P2", d.CandidateIds.Take(1).ToArray(), d.DecisionId, d.WindowId, First));
        Assert.Equal(before, Json(state));
        Resolve(state, "P2");
        before = Json(state);
        Assert.Throws<InvalidOperationException>(() => ThreeKingdomsEngine.ChooseGenerals(state, "P2", d.CandidateIds.Take(2).ToArray(), d.DecisionId, d.WindowId, First));
        Assert.Equal(before, Json(state));
    }

    [Fact]
    public void AnotherPlayersCandidatesAndDeploymentDoNotLeakIntoObserverProjection()
    {
        var state = Create();
        string before = Json(ThreeKingdomsEngine.Project(state, "P1"));
        var d = ThreeKingdomsEngine.GetPendingDecision(state, "P2")!;
        (d.CandidateIds[0], d.CandidateIds[1]) = (d.CandidateIds[1], d.CandidateIds[0]);
        Assert.Equal(before, Json(ThreeKingdomsEngine.Project(state, "P1")));
        Resolve(state, "P2");
        var a = ThreeKingdomsEngine.Clone(state);
        var b = ThreeKingdomsEngine.Clone(state);
        var da = ThreeKingdomsEngine.GetPendingDecision(a, "P2")!;
        var db = ThreeKingdomsEngine.GetPendingDecision(b, "P2")!;
        ThreeKingdomsEngine.DeployGeneral(a, "P2", da.CandidateIds[0], da.DecisionId, da.WindowId, First);
        ThreeKingdomsEngine.DeployGeneral(b, "P2", db.CandidateIds[1], db.DecisionId, db.WindowId, First);
        Assert.Equal(Json(ThreeKingdomsEngine.Project(a, "P1")), Json(ThreeKingdomsEngine.Project(b, "P1")));
    }

    [Fact]
    public void LegacyB3StillDealsSeriallyAndRecyclesImmediately()
    {
        var state = ThreeKingdomsEngine.Create(new[] { "P1", "P2", "P3", "P4" }, 71, First, true, GeneralCatalog.B3PackageVersion);
        Assert.Null(state.PendingDecisions);
        Assert.Equal(4, state.Generals.Count(g => g.Region == GeneralRegion.Candidate));
        while (!state.OpeningComplete) Resolve(state, state.PendingDecision.PlayerId);
        ThreeKingdomsEngine.BeginNormalTurns(state);
        string old = state.Generals.Single(g => g.Region == GeneralRegion.Active && g.OwnerPlayerId == "P1").GeneralId;
        ThreeKingdomsEngine.RetireGeneral(state, "P1", state.WindowId, First);
        Assert.Contains(old, state.PendingDecision.CandidateIds);
        Assert.DoesNotContain(state.Generals,g => g.Region == GeneralRegion.Retired);
        Assert.Empty(ThreeKingdomsEngine.Validate(state));
    }
}
