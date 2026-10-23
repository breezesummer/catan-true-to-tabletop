using System.Reflection;
using Catan.Core;
using Catan.Core.ThreeKingdoms;
using M4 = Catan.Core.M4;
using M5 = Catan.Core.M5;
using Xunit;

namespace Catan.Rules.Tests;

/// <summary>原生宿主的空城集成夹具；恢复检查另由正常 B3 会话测试负责。</summary>
public sealed class ThreeKingdomsB3ImmunityTests
{
    private static ThreeKingdomsState Generals()
    {
        var tk = ThreeKingdomsEngine.Create(new[] { "P1", "P2", "P3", "P4" }, 42, _ => 0, true, GeneralCatalog.B3PackageVersion);
        while (!tk.OpeningComplete)
        {
            var d = tk.PendingDecision;
            if (d.Kind == ThreeKingdomsDecisionKind.SelectOpening) ThreeKingdomsEngine.ChooseGenerals(tk, d.PlayerId, d.CandidateIds.Take(2).ToArray(), d.DecisionId, tk.WindowId, _ => 0);
            else ThreeKingdomsEngine.DeployGeneral(tk, d.PlayerId, d.CandidateIds[0], d.DecisionId, tk.WindowId, _ => 0);
        }
        ThreeKingdomsEngine.BeginNormalTurns(tk);
        ThreeKingdomsEngine.AdvancePhase(tk, "P1", tk.WindowId);
        ThreeKingdomsEngine.AdvancePhase(tk, "P1", tk.WindowId);
        ThreeKingdomsEngine.MarkDrawPhase(tk);
        ThreeKingdomsEngine.AdvancePhase(tk, "P1", tk.WindowId);
        var desired = new[] { "tk-g22", "tk-g19", "tk-g06", "tk-g09" };
        for (int i = 0; i < desired.Length; i++)
        {
            var current = tk.Generals.Single(g => g.Region == GeneralRegion.Active && g.OwnerPlayerId == "P" + (i + 1));
            var wanted = tk.Generals.Single(g => g.GeneralId == desired[i]);
            (current.GeneralId, wanted.GeneralId) = (wanted.GeneralId, current.GeneralId);
            (current.InstanceId, wanted.InstanceId) = (wanted.InstanceId, current.InstanceId);
        }
        return tk;
    }

    private static dynamic Fixture(bool combined, Action<dynamic>? configure = null)
    {
        if (combined) return M5Harness.Fixture(s => { s.RulesVersion = M5.CombinedGameSession.ThreeKingdomsB3RulesVersion; s.ThreeKingdoms = Generals(); configure?.Invoke(s); });
        return M4CoreHarness.Fixture(s => { s.RulesVersion = M4.CitiesKnightsGameSession.ThreeKingdomsB3RulesVersion; s.ThreeKingdoms = Generals(); configure?.Invoke(s); });
    }
    private static dynamic Player(dynamic state, string id) => ((IEnumerable<object>)state.Players).Single(p => (string)((dynamic)p).Id == id);
    private static dynamic Cards(bool combined, params string[] names)
    {
        Type type = combined ? typeof(M5.ProgressCardKind) : typeof(M4.ProgressCardKind);
        var array = Array.CreateInstance(type, names.Length);
        for (int i = 0; i < names.Length; i++) array.SetValue(Enum.Parse(type, names[i]), i);
        return array;
    }
    private static dynamic Command(bool combined, dynamic game, string kind, string player = "P1")
    {
        dynamic c = combined ? (object)new M5.Command() : new M4.Command();
        c.Id = ThreeKingdomsTestGame.NextId(); c.PlayerId = player;
        c.Kind = (dynamic)Enum.Parse(combined ? typeof(M5.CommandKind) : typeof(M4.CommandKind), kind);
        c.WindowId = game.GetPlayerView(player).ThreeKingdoms.WindowId;
        return c;
    }
    private static void Accept(dynamic game, dynamic command)
    {
        var result = game.Execute(command);
        Assert.True((bool)result.Success, (string)(result.ErrorCode + ":" + result.Message)); game.AssertInvariants();
    }
    private static void Reject(dynamic game, dynamic command)
    {
        string before = game.Save(); Assert.False((bool)game.Execute(command).Success); Assert.Equal(before, (string)game.Save()); game.AssertInvariants();
    }
    private static object? Invoke(object game, string method, params object[] arguments) => game.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(game, arguments);
    private static void SetState(bool combined, object game, object state)
    {
        if (combined) { M5Harness.Balance((M5.GameState)state); M5Harness.SetState((M5.CombinedGameSession)game, (M5.GameState)state); }
        else { M4CoreHarness.Balance((M4.GameState)state); M4CoreHarness.SetState((M4.CitiesKnightsGameSession)game, (M4.GameState)state); }
    }
    private static dynamic Play(bool combined, dynamic game, string card, string target = "P2")
    {
        dynamic c = Command(combined, game, "PlayProgressCard");
        c.ProgressCard = (dynamic)Enum.Parse(combined ? typeof(M5.ProgressCardKind) : typeof(M4.ProgressCardKind), card);
        c.OtherPlayerId = target; return c;
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void EmptyHandStillHasProgressCardsButEspionageIsConsumedWithoutRevealingThem(bool combined)
    {
        dynamic game = Fixture(combined, s => { Player(s, "P1").ProgressCards = Cards(combined, "Espionage"); Player(s, "P2").ProgressCards = Cards(combined, "Mining", "Alchemy"); });
        Accept(game, Play(combined, game, "Espionage"));
        dynamic after = game.GetAuthoritativeStateForTesting();
        Assert.Equal(0, (int)Player(after, "P1").ProgressCards.Length);
        Assert.Equal(2, (int)Player(after, "P2").ProgressCards.Length);
        Assert.Null((object?)after.PendingDecision);
        Assert.Equal(0, (int)game.GetPlayerView("P1").PrivateTargetProgressCards.Length);
    }

    [Theory][InlineData(false, false)][InlineData(false, true)][InlineData(true, false)][InlineData(true, true)]
    public void AnyResourceOrCommodityDisablesKongchengImmediately(bool combined, bool commodity)
    {
        dynamic game = Fixture(combined, s => {
            Player(s, "P1").ProgressCards = Cards(combined, "Espionage"); Player(s, "P2").ProgressCards = Cards(combined, "Mining");
            if (commodity) Player(s, "P2").Commodities.Paper = 1; else Player(s, "P2").Resources.Wood = 1;
        });
        Accept(game, Play(combined, game, "Espionage"));
        Assert.Equal("ProgressEspionage", (string)game.GetAuthoritativeStateForTesting().PendingDecision.Kind);
        Assert.Equal(1, (int)game.GetPlayerView("P1").PrivateTargetProgressCards.Length);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void IllegalTargetStillRejectsBeforeCardPayment(bool combined)
    {
        dynamic game = Fixture(combined, s => Player(s, "P1").ProgressCards = Cards(combined, "Espionage"));
        Reject(game, Play(combined, game, "Espionage", "P1"));
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void KongchengDoesNotGrantProgressCapacityException(bool combined)
    {
        dynamic game = Fixture(combined, s => Player(s, "P2").ProgressCards = Cards(combined, "Mining", "Alchemy", "Crane", "Medicine", "Irrigation"));
        dynamic state = game.GetAuthoritativeStateForTesting();
        Assert.True((bool)Invoke((object)game, "B3Immune", (object)state, "P2")!);
        Assert.False((bool)Invoke((object)game, "B2Protected", (object)state, "P2")!);
        Assert.False((bool)Invoke((object)game, "B2Unlimited", (object)state, "P2")!);
        // 原生容量流程仍必须要求弃至四张，空城不拦普通规则的容量处理。
        Invoke((object)game, "DrawProgressCard", (object)state, (object)Player(state, "P2"), combined ? M5.ImprovementTrack.Science : (object)M4.ImprovementTrack.Science);
        Assert.Contains((IEnumerable<object>)state.DecisionQueue, d => (string)((dynamic)d).Kind == "ProgressDiscard" && (string)((dynamic)d).PlayerId == "P2");
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void TreasonCannotRemoveEmptyHandGeneralsKnight(bool combined)
    {
        dynamic seed = Fixture(combined); string vertex = ((Vertex[])seed.GetPlayerView("P1").Board.Vertices)[0].Id;
        dynamic game = Fixture(combined, s => {
            Player(s, "P1").ProgressCards = Cards(combined, "Treason");
            s.Knights = combined ? (dynamic)new[] { new M5.Knight { PlayerId = "P2", LocationId = vertex, Level = 1 } } : new[] { new M4.Knight { PlayerId = "P2", LocationId = vertex, Level = 1 } };
        });
        Accept(game, Play(combined, game, "Treason"));
        dynamic after = game.GetAuthoritativeStateForTesting();
        Assert.Single((IEnumerable<object>)after.Knights); Assert.Equal("P2", (string)after.Knights[0].PlayerId); Assert.Null((object?)after.PendingDecision);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void DiplomacyConsumesTheCardButCannotRemoveAnOpponentsEmptyHandRoad(bool combined)
    {
        dynamic seed = Fixture(combined); var board = seed.GetPlayerView("P1").Board;
        var land = ((Tile[])seed.GetAuthoritativeStateForTesting().Tiles).First(t => t.Resource != "sea" && t.Resource != "desert");
        var edge = ((Edge[])board.Edges).First(e => e.Vertices.All(land.Vertices.Contains));
        dynamic game = Fixture(combined, s => { Player(s, "P1").ProgressCards = Cards(combined, "Diplomacy"); s.Roads = new[] { new PiecePlacement { PlayerId = "P2", LocationId = edge.Id } }; });
        dynamic c = Play(combined, game, "Diplomacy"); c.TargetId = edge.Id; Accept(game, c);
        dynamic after = game.GetAuthoritativeStateForTesting(); Assert.Single((IEnumerable<object>)after.Roads); Assert.Equal(0, (int)Player(after, "P1").ProgressCards.Length);
    }

    [Theory][InlineData(false, false)][InlineData(false, true)][InlineData(true, false)][InlineData(true, true)]
    public void KnightAndIntrigueCannotDisplaceProtectedKnightOrCreateOverlappingPieces(bool combined, bool progress)
    {
        dynamic seed = Fixture(combined); var edges = (Edge[])seed.GetPlayerView("P1").Board.Edges;
        var land = ((Tile[])seed.GetAuthoritativeStateForTesting().Tiles).First(t => t.Resource != "sea" && t.Resource != "desert");
        var edge = edges.First(e => e.Vertices.All(land.Vertices.Contains));
        string source = edge.Vertices[0], target = edge.Vertices[1];
        dynamic game = Fixture(combined, s => {
            s.Roads = new[] { new PiecePlacement { PlayerId = "P1", LocationId = edge.Id } };
            s.Knights = combined ? (dynamic)new[] {
                new M5.Knight { PlayerId = "P1", LocationId = source, Level = 2, Active = true, ActivatedTurn = 1 },
                new M5.Knight { PlayerId = "P2", LocationId = target, Level = 1 }
            } : new[] {
                new M4.Knight { PlayerId = "P1", LocationId = source, Level = 2, Active = true, ActivatedTurn = 1 },
                new M4.Knight { PlayerId = "P2", LocationId = target, Level = 1 }
            };
            if (progress) Player(s, "P1").ProgressCards = Cards(combined, "Intrigue");
        });
        dynamic command = progress ? Play(combined, game, "Intrigue") : Command(combined, game, "DisplaceKnight");
        command.SourceId = source; command.TargetId = target; Accept(game, command);
        dynamic after = game.GetAuthoritativeStateForTesting();
        var knights = ((IEnumerable<object>)after.Knights).Cast<dynamic>().ToArray();
        Assert.Equal(2, knights.Length); Assert.Equal(source, (string)knights.Single(k => (string)k.PlayerId == "P1").LocationId);
        Assert.Equal(target, (string)knights.Single(k => (string)k.PlayerId == "P2").LocationId);
        Assert.Null((object?)after.PendingDecision);
        if (progress) Assert.Equal(0, (int)Player(after, "P1").ProgressCards.Length);
        else Assert.False((bool)knights.Single(k => (string)k.PlayerId == "P1").Active);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void KongchengDoesNotPreventRemovingOwnRoadThroughDiplomacy(bool combined)
    {
        dynamic seed = Fixture(combined); var board = seed.GetPlayerView("P1").Board;
        var land = ((Tile[])seed.GetAuthoritativeStateForTesting().Tiles).First(t => t.Resource != "sea" && t.Resource != "desert");
        var edge = ((Edge[])board.Edges).First(e => e.Vertices.All(land.Vertices.Contains));
        dynamic game = Fixture(combined, s => {
            var tk = (ThreeKingdomsState)s.ThreeKingdoms;
            var p1 = tk.Generals.Single(g => g.Region == GeneralRegion.Active && g.OwnerPlayerId == "P1");
            var p2 = tk.Generals.Single(g => g.Region == GeneralRegion.Active && g.OwnerPlayerId == "P2");
            (p1.GeneralId, p2.GeneralId) = (p2.GeneralId, p1.GeneralId); (p1.InstanceId, p2.InstanceId) = (p2.InstanceId, p1.InstanceId);
            Player(s, "P1").ProgressCards = Cards(combined, "Diplomacy"); s.Roads = new[] { new PiecePlacement { PlayerId = "P1", LocationId = edge.Id } };
        });
        dynamic command = Play(combined, game, "Diplomacy"); command.TargetId = edge.Id; Accept(game, command);
        Assert.Empty((IEnumerable<object>)game.GetAuthoritativeStateForTesting().Roads);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void BarbarianAttackKeepsOriginalWeakestSetAndOnlyCancelsImmunePlayersLoss(bool combined)
    {
        dynamic seed = Fixture(combined); var vertices = ((Vertex[])seed.GetPlayerView("P1").Board.Vertices).Select(v => v.Id).ToArray();
        var edges = (Edge[])seed.GetPlayerView("P1").Board.Edges;
        string distant = vertices.First(v => v != vertices[0] && !edges.Any(e => e.Vertices.Contains(vertices[0]) && e.Vertices.Contains(v)));
        dynamic game = Fixture(combined, s => {
            s.Cities = new[] { new PiecePlacement { PlayerId = "P2", LocationId = vertices[0] }, new PiecePlacement { PlayerId = "P3", LocationId = distant } };
        });
        dynamic state = game.GetAuthoritativeStateForTesting();
        Invoke((object)game, "BarbarianAttack", (object)state);
        Assert.DoesNotContain((IEnumerable<object>)state.DecisionQueue, d => (string)((dynamic)d).Kind == "PillageCity" && (string)((dynamic)d).PlayerId == "P2");
        Assert.Contains((IEnumerable<object>)state.DecisionQueue, d => (string)((dynamic)d).Kind == "PillageCity" && (string)((dynamic)d).PlayerId == "P3");
        Assert.Equal(2, (int)state.BarbarianAttackStrength);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void AcquiringImmunityBeforeDeferredEspionageSuppressesItsHiddenViewAndPendingTransfer(bool combined)
    {
        dynamic game = Fixture(combined, s => { Player(s, "P1").ProgressCards = Cards(combined, "Espionage"); Player(s, "P2").ProgressCards = Cards(combined, "Mining"); Player(s, "P2").Resources.Wood = 1; });
        Accept(game, Play(combined, game, "Espionage"));
        dynamic state = game.GetAuthoritativeStateForTesting(); Player(state, "P2").Resources.Wood = 0; SetState(combined, (object)game, (object)state);
        Assert.Equal(0, (int)game.GetPlayerView("P1").PrivateTargetProgressCards.Length);
        dynamic finish = Command(combined, game, "ResolveProgressChoice"); finish.Decline = true; Accept(game, finish);
        Assert.Equal(1, (int)Player(game.GetAuthoritativeStateForTesting(), "P2").ProgressCards.Length);
        Assert.Null((object?)game.GetAuthoritativeStateForTesting().PendingDecision);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void ReturningGeneralToPoolImmediatelyEndsKongcheng(bool combined)
    {
        dynamic game = Fixture(combined); dynamic state = game.GetAuthoritativeStateForTesting();
        var tk = (ThreeKingdomsState)state.ThreeKingdoms;
        var active = tk.Generals.Single(g => g.Region == GeneralRegion.Active && g.OwnerPlayerId == "P2");
        var reserve = tk.Generals.Single(g => g.Region == GeneralRegion.Hidden && g.OwnerPlayerId == "P2");
        (active.GeneralId, reserve.GeneralId) = (reserve.GeneralId, active.GeneralId);
        Assert.False((bool)Invoke((object)game, "B3Immune", (object)state, "P2")!);
    }
}
