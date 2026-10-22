using Catan.Core;
using Catan.Core.M2;
using Xunit;
using Xunit.Abstractions;
using Command = Catan.Core.M2.Command;
using CommandKind = Catan.Core.M2.CommandKind;
using GamePhase = Catan.Core.M2.GamePhase;
using PlayerView = Catan.Core.M2.PlayerView;
using static Catan.Rules.Tests.M2Harness;

namespace Catan.Rules.Tests;

public sealed class M2FullGameTests
{
    private readonly ITestOutputHelper output;
    public M2FullGameTests(ITestOutputHelper output) => this.output = output;

    [Theory]
    [InlineData(3, 1u)]
    [InlineData(4, 2u)]
    public void ViewOnlyLocalSeatsFinishNormalGameAndPendingSavesResumeDeterministically(int seats, uint seed)
    {
        var game = New(seats, seed);
        var driver = new M2ViewOnlyDriver();
        var checkedPhases = new HashSet<GamePhase>();
        var commands = 0;
        var last = game.GetPlayerView("P1");
        while (last.Phase != GamePhase.Finished && commands < 7000)
        {
            // Policy gets only a detached PlayerView. The authority and save are test assertions, never policy inputs.
            var actor = last.Phase == GamePhase.Discard ? last.Discards.First().PlayerId : last.ActivePlayerId;
            var view = game.GetPlayerView(actor);
            var command = driver.Decide(view);
            command.Id = $"full-{seats}-{seed}-{commands++}";
            var result = game.Execute(command);
            Assert.True(result.Success, $"Turn {last.Turn} {command.Kind}: {result.ErrorCode} {result.Message}");
            game.AssertInvariants();
            last = game.GetPlayerView("P1");
            if (last.PendingDecision != null && checkedPhases.Add(last.Phase))
            {
                var restored = BaseGameSession.Load(AcceptanceFixture.Json, game.Save());
                Assert.Equal(game.Save(), restored.Save());
                var nextActor = last.Phase == GamePhase.Discard ? last.Discards.First().PlayerId : last.ActivePlayerId;
                var next = driver.Decide(game.GetPlayerView(nextActor)); next.Id = $"full-{seats}-{seed}-{commands++}";
                Assert.True(game.Execute(next).Success);
                Assert.True(restored.Execute(next).Success);
                Assert.Equal(game.Save(), restored.Save());
                last = game.GetPlayerView("P1");
            }
        }
        Assert.True(last.Phase == GamePhase.Finished,
            $"Seed {seed}: no winner after {commands} commands/{last.Turn} turns; points {string.Join(",", last.Players.Select(p => p.Id + "=" + p.VictoryPoints))}");
        Assert.NotNull(last.WinnerPlayerId);
        Assert.True(game.GetPlayerView(last.WinnerPlayerId).OwnVictoryPoints >= 10);
        Assert.Equal(last.ActivePlayerId, last.WinnerPlayerId);
        output.WriteLine($"seats={seats}, seed={seed}, winner={last.WinnerPlayerId}, turn={last.Turn}, commands={commands}, save-phases={string.Join(",", checkedPhases.Order())}");
        Assert.Contains(GamePhase.SetupRoad, checkedPhases);
        Assert.Contains(GamePhase.RobberMove, checkedPhases);
        Assert.Contains(GamePhase.RobberSteal, checkedPhases);
        Assert.Contains(GamePhase.Discard, checkedPhases);
        Assert.Contains(GamePhase.RoadBuilding, checkedPhases);
        var loaded = BaseGameSession.Load(AcceptanceFixture.Json, game.Save());
        Assert.Equal(game.Save(), loaded.Save());
        Invariants(game);
    }
}

/// <summary>Local test seats. This class has no BaseGameSession, GameState, RNG, save, or opponent-hand access.</summary>
internal sealed class M2ViewOnlyDriver
{
    private static readonly ResourceBag City = Bag(wheat: 2, ore: 3);
    private static readonly ResourceBag Settlement = Bag(1, 1, 1, 1);
    private static readonly ResourceBag Road = Bag(1, 1);
    private static readonly ResourceBag Development = Bag(wool: 1, wheat: 1, ore: 1);

    internal Command Decide(PlayerView view)
    {
        Command Make(CommandKind kind, string? target = null) => new() { PlayerId = view.PlayerId, Kind = kind, TargetId = target! };
        switch (view.Phase)
        {
            case GamePhase.SetupSettlement:
                return Make(CommandKind.SetupSettlement, view.LegalVertexIds.OrderByDescending(v => Value(view, v)).ThenBy(v => v).First());
            case GamePhase.SetupRoad:
                return Make(CommandKind.SetupRoad, BestRoad(view, view.LegalEdgeIds));
            case GamePhase.Discard:
                var cards = new ResourceBag(); var remaining = view.Discards.Single(d => d.PlayerId == view.PlayerId).Amount;
                while (remaining-- > 0)
                { var r = Resources.OrderByDescending(r => view.OwnResources[r] - cards[r]).First(); cards[r]++; }
                var discard = Make(CommandKind.DiscardResources); discard.Resources = cards; return discard;
            case GamePhase.RobberMove:
                var destination = view.Board.Tiles.Where(t => t.Id != view.Board.RobberTileId).OrderByDescending(t =>
                    Buildings(view).Where(b => t.Vertices.Contains(b.LocationId)).Sum(b => b.PlayerId == view.PlayerId ? -100 : 10 + view.Players.Single(p => p.Id == b.PlayerId).ResourceCount)).First();
                return Make(CommandKind.MoveRobber, destination.Id);
            case GamePhase.RobberSteal:
                var theft = Make(CommandKind.StealResource); theft.OtherPlayerId = view.PendingDecision.EligibleVictimIds.OrderByDescending(id => view.Players.Single(p => p.Id == id).ResourceCount).First(); return theft;
            case GamePhase.RoadBuilding:
                return view.LegalEdgeIds.Length == 0 ? Make(CommandKind.FinishRoadBuilding) : Make(CommandKind.PlaceFreeRoad, BestRoad(view, view.LegalEdgeIds));
            case GamePhase.ProductionAwaitRoll:
                return CanPlay(view, DevelopmentCardKind.Knight) ? Make(CommandKind.PlayKnight) : Make(CommandKind.RollDice);
            case GamePhase.Action: break;
            default: throw new InvalidOperationException("Unexpected phase " + view.Phase);
        }
        if (view.TradeOffer != null)
        { var offer = view.TradeOffer; return Make(Covers(view.OwnResources, offer.Receive) ? CommandKind.AcceptTrade : CommandKind.RejectTrade); }
        if (view.LegalCityVertexIds.Length > 0)
            return Make(CommandKind.BuildCity, view.LegalCityVertexIds.OrderByDescending(v => Value(view, v)).First());
        if (view.LegalVertexIds.Length > 0)
            return Make(CommandKind.BuildSettlement, view.LegalVertexIds.OrderByDescending(v => Value(view, v)).First());
        var expansion = Expansion(view);
        var me = view.Players.Single(p => p.Id == view.PlayerId);
        ResourceBag goal;
        CommandKind goalKind;
        string? target = null;
        if (me.Pieces.Cities > 0 && view.Board.Settlements.Any(b => b.PlayerId == view.PlayerId)
            && (view.OwnResources.Ore >= 2 || me.Pieces.Settlements == 0 || expansion == null))
        { goal = City; goalKind = CommandKind.BuildCity; target = view.Board.Settlements.Where(b => b.PlayerId == view.PlayerId).OrderByDescending(b => Value(view, b.LocationId)).First().LocationId; }
        else if (me.Pieces.Settlements > 0 && expansion != null)
        { goal = expansion.Value.path.Length == 0 ? Settlement : Road; goalKind = expansion.Value.path.Length == 0 ? CommandKind.BuildSettlement : CommandKind.BuildRoad; target = expansion.Value.path.Length == 0 ? expansion.Value.target : expansion.Value.path[0]; }
        else { goal = Development; goalKind = CommandKind.BuyDevelopmentCard; }

        if (CanPlay(view, DevelopmentCardKind.YearOfPlenty) && view.Bank.Total > 0)
        {
            var selection = Bag();
            for (var i = 0; i < Math.Min(2, view.Bank.Total); i++)
            {
                var r = Resources.Where(r => view.Bank[r] > selection[r]).OrderByDescending(r => goal[r] - view.OwnResources[r] - selection[r]).ThenBy(r => view.OwnResources[r] + selection[r]).First();
                selection[r]++;
            }
            var card = Make(CommandKind.PlayYearOfPlenty); card.Resources = selection; return card;
        }
        if (CanPlay(view, DevelopmentCardKind.Monopoly))
        {
            var card = Make(CommandKind.PlayMonopoly);
            card.Resource = Resources.OrderByDescending(r => (19 - view.Bank[r] - view.OwnResources[r]) * 2 + Math.Max(0, goal[r] - view.OwnResources[r])).First(); return card;
        }
        if (CanPlay(view, DevelopmentCardKind.RoadBuilding) && me.Pieces.Roads > 0 && expansion is { path.Length: > 0 })
            return Make(CommandKind.PlayRoadBuilding);
        if (Covers(view.OwnResources, goal) && (goalKind != CommandKind.BuyDevelopmentCard || view.DevelopmentDeckCount > 0))
            return Make(goalKind, target);
        foreach (var need in Resources.Where(r => view.OwnResources[r] < goal[r] && view.Bank[r] > 0).OrderBy(r => view.OwnResources[r] - goal[r]))
        {
            var give = Resources.Where(r => r != need && view.OwnResources[r] - goal[r] >= Rate(view, r)).OrderByDescending(r => view.OwnResources[r] - goal[r]).ToArray();
            if (give.Length == 0) continue;
            var trade = Make(CommandKind.BankTrade); trade.GiveResource = give[0]; trade.GiveAmount = Rate(view, give[0]); trade.ReceiveResource = need; trade.ReceiveAmount = 1; return trade;
        }
        if (view.DevelopmentDeckCount > 0 && Covers(view.OwnResources, Development)) return Make(CommandKind.BuyDevelopmentCard);
        return Make(CommandKind.EndTurn);
    }

    private static bool CanPlay(PlayerView view, DevelopmentCardKind kind) => !view.DevelopmentCardPlayedThisTurn && view.OwnDevelopmentCards.Any(c => c.Kind == kind && c.BoughtTurn < view.Turn);
    private static bool Covers(ResourceBag hand, ResourceBag cost) => Resources.All(r => hand[r] >= cost[r]);
    private static PiecePlacement[] Buildings(PlayerView view) => view.Board.Settlements.Concat(view.Board.Cities).ToArray();
    private static int Rate(PlayerView view, Resource resource)
    {
        var ports = view.Board.Ports.Where(p => Buildings(view).Any(b => b.PlayerId == view.PlayerId && p.Vertices.Contains(b.LocationId))).ToArray();
        return ports.Any(p => p.Resource == resource) ? 2 : ports.Any(p => p.Resource == null) ? 3 : 4;
    }
    private static double Value(PlayerView view, string vertex)
    {
        var owned = Buildings(view).Where(b => b.PlayerId == view.PlayerId).Select(b => b.LocationId).ToArray();
        return view.Board.Tiles.Where(t => t.Vertices.Contains(vertex) && t.Number.HasValue).Sum(t =>
        {
            var pips = 6 - Math.Abs(7 - t.Number!.Value);
            var already = view.Board.Tiles.Count(other => other.Resource == t.Resource && other.Vertices.Intersect(owned).Any());
            return pips * (t.Resource == "ore" || t.Resource == "wheat" ? 1.3 : 1.0) + (already == 0 ? 3 : 0);
        });
    }
    private static string BestRoad(PlayerView view, string[] legal)
    {
        var expansion = Expansion(view);
        if (expansion.HasValue && expansion.Value.path.Length > 0 && legal.Contains(expansion.Value.path[0])) return expansion.Value.path[0];
        return legal.OrderByDescending(id => view.Board.Edges.Single(e => e.Id == id).Vertices.Max(v => Value(view, v))).ThenBy(e => e).First();
    }
    private static (string target, string[] path)? Expansion(PlayerView view)
    {
        var buildings = Buildings(view);
        var ownRoads = view.Board.Roads.Where(r => r.PlayerId == view.PlayerId).Select(r => r.LocationId).ToHashSet();
        var otherRoads = view.Board.Roads.Where(r => r.PlayerId != view.PlayerId).Select(r => r.LocationId).ToHashSet();
        var enemyVertices = buildings.Where(b => b.PlayerId != view.PlayerId).Select(b => b.LocationId).ToHashSet();
        var starts = view.Board.Edges.Where(e => ownRoads.Contains(e.Id)).SelectMany(e => e.Vertices)
            .Concat(buildings.Where(b => b.PlayerId == view.PlayerId).Select(b => b.LocationId)).Where(v => !enemyVertices.Contains(v)).Distinct().ToArray();
        var paths = starts.ToDictionary(v => v, _ => Array.Empty<string>());
        var pending = starts.ToList();
        while (pending.Count > 0)
        {
            var v = pending.OrderBy(v => paths[v].Length).First(); pending.Remove(v);
            foreach (var edge in view.Board.Edges.Where(e => e.Vertices.Contains(v) && !otherRoads.Contains(e.Id)))
            {
                var next = edge.Vertices.First(x => x != v);
                if (enemyVertices.Contains(next)) continue;
                var path = ownRoads.Contains(edge.Id) ? paths[v] : paths[v].Append(edge.Id).ToArray();
                if (path.Length > 4 || (paths.TryGetValue(next, out var old) && old.Length <= path.Length)) continue;
                paths[next] = path; if (!pending.Contains(next)) pending.Add(next);
            }
        }
        var targets = view.Board.Vertices.Where(v => !buildings.Any(b => b.LocationId == v.Id)
            && !view.Board.Edges.Where(e => e.Vertices.Contains(v.Id)).Any(e => buildings.Any(b => e.Vertices.Contains(b.LocationId)))
            && paths.ContainsKey(v.Id)).Select(v => (target: v.Id, path: paths[v.Id])).ToArray();
        if (targets.Length == 0) return null;
        return targets.OrderByDescending(t => Value(view, t.target) - t.path.Length * 12).ThenBy(t => t.target).First();
    }
}
