using Catan.Core;
using Catan.Core.M3;
using Xunit;
using Xunit.Abstractions;
using Command = Catan.Core.M3.Command;
using CommandKind = Catan.Core.M3.CommandKind;
using GamePhase = Catan.Core.M3.GamePhase;
using PlayerView = Catan.Core.M3.PlayerView;
using static Catan.Rules.Tests.M3Harness;

namespace Catan.Rules.Tests;

public sealed class M3FullGameTests
{
    private readonly ITestOutputHelper output;
    public M3FullGameTests(ITestOutputHelper output) => this.output = output;

    [Theory]
    [InlineData("heading-for-new-shores", 3, 1u)]
    [InlineData("heading-for-new-shores", 4, 2u)]
    [InlineData("four-islands", 4, 3u)]
    [InlineData("fog-islands", 4, 4u)]
    [InlineData("through-the-desert", 4, 5u)]
    [InlineData("forgotten-tribe", 4, 6u)]
    [InlineData("cloth-for-catan", 4, 7u)]
    [InlineData("pirate-islands", 4, 8u)]
    [InlineData("wonders-of-catan", 4, 9u)]
    [InlineData("new-world", 4, 10u)]
    [InlineData("four-islands", 3, 11u)]
    [InlineData("fog-islands", 3, 12u)]
    [InlineData("through-the-desert", 3, 13u)]
    [InlineData("forgotten-tribe", 3, 14u)]
    [InlineData("cloth-for-catan", 3, 15u)]
    [InlineData("pirate-islands", 3, 16u)]
    [InlineData("wonders-of-catan", 3, 17u)]
    [InlineData("new-world", 3, 18u)]
    public void ViewOnlyLocalSeatsFinishNormalGameAndPendingSavesResumeDeterministically(string scenario, int seats, uint seed)
    {
        var game = New(scenario, seats, seed);
        var driver = new M3ViewOnlyDriver();
        var checkedPhases = new HashSet<GamePhase>();
        var commands = 0;
        var last = game.GetPlayerView("P1");
        while (last.Phase != GamePhase.Finished && commands < 10000)
        {
            // Policy gets only a detached PlayerView. The authority and save are test assertions, never policy inputs.
            var actor = Actor(last);
            var view = game.GetPlayerView(actor);
            var command = driver.Decide(view);
            command.Id = $"full-{seats}-{seed}-{commands++}";
            var result = game.Execute(command);
            Assert.True(result.Success, $"Turn {last.Turn} {command.Kind}: {result.ErrorCode} {result.Message}");
            game.AssertInvariants();
            last = game.GetPlayerView("P1");
            if ((last.PendingDecision != null || last.Phase == GamePhase.GoldChoice) && checkedPhases.Add(last.Phase))
            {
                var restored = SeafarersGameSession.Load( game.Save());
                Assert.Equal(game.Save(), restored.Save());
                var nextActor = Actor(last);
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
        if (scenario != "wonders-of-catan" && scenario != "cloth-for-catan") Assert.True(game.GetPlayerView(last.WinnerPlayerId).OwnVictoryPoints >= last.TargetVictoryPoints);
        output.WriteLine($"scenario={scenario}, seats={seats}, seed={seed}, winner={last.WinnerPlayerId}, turn={last.Turn}, commands={commands}, save-phases={string.Join(",", checkedPhases.Order())}");
        Assert.Contains(GamePhase.SetupRoad, checkedPhases);
        var loaded = SeafarersGameSession.Load( game.Save());
        Assert.Equal(game.Save(), loaded.Save());
        Invariants(game);
    }

    internal static string Actor(PlayerView view) => view.Phase == GamePhase.Discard ? view.Discards.First().PlayerId :
        view.Phase == GamePhase.GoldChoice ? view.GoldClaims.First().PlayerId : view.PendingDecision?.PlayerId ?? view.ActivePlayerId;
}

/// <summary>Local test seats. This class has no SeafarersGameSession, GameState, RNG, save, or opponent-hand access.</summary>
internal sealed class M3ViewOnlyDriver
{
    private static readonly ResourceBag City = Bag(wheat: 2, ore: 3);
    private static readonly ResourceBag Settlement = Bag(1, 1, 1, 1);
    private static readonly ResourceBag Road = Bag(1, 1);
    private static readonly ResourceBag Ship = Bag(wood: 1, wool: 1);
    private static readonly ResourceBag Development = Bag(wool: 1, wheat: 1, ore: 1);

    internal Command Decide(PlayerView view)
    {
        Command Make(CommandKind kind, string? target = null) => new() { PlayerId = view.PlayerId, Kind = kind, TargetId = target! };
        switch (view.Phase)
        {
            case GamePhase.SetupPort:
                return Make(CommandKind.SetupPort, view.LegalPortEdgeIds.First());
            case GamePhase.SetupSettlement:
                return Make(CommandKind.SetupSettlement, view.LegalVertexIds.OrderByDescending(v => Value(view, v)).ThenBy(v => v).First());
            case GamePhase.SetupRoad:
                return view.LegalEdgeIds.Length > 0 ? Make(CommandKind.SetupRoad, BestRoad(view, view.LegalEdgeIds)) : Make(CommandKind.SetupShip, view.LegalShipEdgeIds.First());
            case GamePhase.GoldChoice:
                var gold = Make(CommandKind.ChooseGoldResources); gold.Resources = Bag();
                for (var i = 0; i < view.GoldClaims.First(g => g.PlayerId == view.PlayerId).Amount; i++)
                { var r = Resources.Where(r => view.Bank[r] > gold.Resources[r]).OrderBy(r => view.OwnResources[r] + gold.Resources[r]).First(); gold.Resources[r]++; }
                return gold;
            case GamePhase.PortPlacement:
                var port = Make(CommandKind.PlacePort, view.LegalPortEdgeIds.First()); port.SourceId = view.OwnHeldPorts.First().Id; return port;
            case GamePhase.Discard:
                var cards = new ResourceBag(); var remaining = view.Discards.Single(d => d.PlayerId == view.PlayerId).Amount;
                while (remaining-- > 0)
                { var r = Resources.OrderByDescending(r => view.OwnResources[r] - cards[r]).First(); cards[r]++; }
                var discard = Make(CommandKind.DiscardResources); discard.Resources = cards; return discard;
            case GamePhase.RobberMove:
                var destination = view.Board.Tiles.Where(t => t.Id != view.Board.RobberTileId && t.Resource != "sea" && t.Resource != "fog"
                    && (view.ScenarioId != "forgotten-tribe" || t.Number.HasValue)
                    && (view.ScenarioId != "cloth-for-catan" || !view.Board.Villages.Any(v => t.Vertices.Contains(v.VertexId)))).OrderByDescending(t =>
                    Buildings(view).Where(b => t.Vertices.Contains(b.LocationId)).Sum(b => b.PlayerId == view.PlayerId ? -100 : 10 + view.Players.Single(p => p.Id == b.PlayerId).ResourceCount)).First();
                return Make(CommandKind.MoveRobber, destination.Id);
            case GamePhase.RobberSteal:
                var theft = Make(CommandKind.StealResource); theft.OtherPlayerId = view.PendingDecision.EligibleVictimIds.OrderByDescending(id => view.Players.Single(p => p.Id == id).ResourceCount).First(); return theft;
            case GamePhase.RoadBuilding:
                return view.LegalEdgeIds.Length > 0 ? Make(CommandKind.PlaceFreeRoad, BestRoad(view, view.LegalEdgeIds)) :
                    view.LegalShipEdgeIds.Length > 0 ? Make(CommandKind.PlaceFreeShip, BestRoad(view, view.LegalShipEdgeIds)) : Make(CommandKind.FinishRoadBuilding);
            case GamePhase.ProductionAwaitRoll:
                return CanPlay(view, DevelopmentCardKind.Knight) && (view.ScenarioId != "pirate-islands" || view.Board.Ships.Any(s => s.PlayerId == view.PlayerId && !s.IsWarship)) ? Make(CommandKind.PlayKnight) : Make(CommandKind.RollDice);
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
        if (view.ScenarioId == "pirate-islands" && view.Board.Fortresses.Single(f => f.PlayerId == view.PlayerId).Strength > 0)
        {
            var ownShips = view.Board.Ships.Where(s => s.PlayerId == view.PlayerId && s.IsInvasionRoute).ToArray();
            if (CanPlay(view, DevelopmentCardKind.Knight) && ownShips.Any(s => !s.IsWarship)) return Make(CommandKind.PlayKnight);
            var extension = PirateExtension(view);
            var buyShip = extension != null && (ownShips.Length < 3 || ownShips.Count(s => s.IsWarship) >= Math.Min(4, ownShips.Length) || view.DevelopmentDeckCount == 0 || !ownShips.Any(s => !s.IsWarship));
            var pirateGoal = buyShip ? Ship : Development;
            if (buyShip && Covers(view.OwnResources, Ship) && view.LegalShipEdgeIds.Contains(extension!)) return Make(CommandKind.BuildShip, extension);
            if (!buyShip && view.DevelopmentDeckCount > 0 && Covers(view.OwnResources, Development)) return Make(CommandKind.BuyDevelopmentCard);
            var navyTrade = TradeFor(view, pirateGoal); if (navyTrade != null) return navyTrade;
        }
        if (view.ScenarioId == "wonders-of-catan")
        {
            if (me.WonderId == null && view.Board.Cities.Count(c => c.PlayerId == view.PlayerId) >= 2)
            {
                var available = view.Wonders.FirstOrDefault(w => w.Requirement == "two-cities" && !view.Players.Any(p => p.WonderId == w.Id));
                if (available != null) return Make(CommandKind.ClaimWonder, available.Id);
            }
            if (me.WonderId != null)
            {
                var wonder = view.Wonders.Single(w => w.Id == me.WonderId);
                if (Covers(view.OwnResources, wonder.Cost)) return Make(CommandKind.BuildWonder, me.WonderId);
                var wonderTrade = TradeFor(view, wonder.Cost); if (wonderTrade != null) return wonderTrade;
            }
        }
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
        var bankTrade = TradeFor(view, goal); if (bankTrade != null) return bankTrade;
        if (view.DevelopmentDeckCount > 0 && Covers(view.OwnResources, Development)) return Make(CommandKind.BuyDevelopmentCard);
        return Make(CommandKind.EndTurn);
    }

    private static Command? TradeFor(PlayerView view, ResourceBag goal)
    {
        foreach (var need in Resources.Where(r => view.OwnResources[r] < goal[r] && view.Bank[r] > 0).OrderBy(r => view.OwnResources[r] - goal[r]))
        {
            var give = Resources.Where(r => r != need && view.OwnResources[r] - goal[r] >= Rate(view, r)).OrderByDescending(r => view.OwnResources[r] - goal[r]).ToArray();
            if (give.Length > 0) return new Command { PlayerId = view.PlayerId, Kind = CommandKind.BankTrade, GiveResource = give[0], GiveAmount = Rate(view, give[0]), ReceiveResource = need, ReceiveAmount = 1 };
        }
        return null;
    }

    private static string? PirateExtension(PlayerView view)
    {
        var fortress = view.Board.Fortresses.Single(f => f.PlayerId == view.PlayerId);
        var current = fortress.StartingVertexId;
        var used = new HashSet<string>(); var passedBeachhead = current == fortress.BeachheadVertexId;
        while (true)
        {
            var ship = view.Board.Ships.FirstOrDefault(s => s.PlayerId == view.PlayerId && s.IsInvasionRoute && !used.Contains(s.LocationId) && view.Board.Edges.Single(e => e.Id == s.LocationId).Vertices.Contains(current));
            if (ship == null) break;
            used.Add(ship.LocationId); current = view.Board.Edges.Single(e => e.Id == ship.LocationId).Vertices.First(v => v != current);
            passedBeachhead |= current == fortress.BeachheadVertexId;
        }
        if (current == fortress.VertexId) return null;
        var goal = passedBeachhead ? fortress.VertexId : fortress.BeachheadVertexId;
        var distances = new Dictionary<string, int> { [goal] = 0 }; var queue = new Queue<string>(); queue.Enqueue(goal);
        while (queue.Count > 0)
        {
            var vertex = queue.Dequeue();
            foreach (var edge in view.Board.Edges.Where(e => e.Vertices.Contains(vertex) && SailingEdge(view, e)))
                foreach (var next in edge.Vertices) if (!distances.ContainsKey(next)) { distances[next] = distances[vertex] + 1; queue.Enqueue(next); }
        }
        return view.Board.Edges.Where(e => e.Vertices.Contains(current) && SailingEdge(view, e)
            && !view.Board.Ships.Any(s => s.LocationId == e.Id) && !view.Board.Roads.Any(r => r.LocationId == e.Id)
            && e.Vertices.Any(v => distances.TryGetValue(v, out var d) && d == distances[current] - 1))
            .OrderBy(e => e.Id).Select(e => e.Id).FirstOrDefault();
    }

    private static bool SailingEdge(PlayerView view, Edge edge)
    {
        var tiles = view.Board.Tiles.Where(t => edge.Vertices.All(t.Vertices.Contains)).ToArray();
        return tiles.Length == 1 || tiles.Any(t => t.Resource is "sea" or "fog");
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
            foreach (var edge in view.Board.Edges.Where(e => e.Vertices.Contains(v) && !otherRoads.Contains(e.Id) && LandEdge(view.Board, e) && !view.Board.Ships.Any(s => s.LocationId == e.Id)))
            {
                var next = edge.Vertices.First(x => x != v);
                if (enemyVertices.Contains(next)) continue;
                var path = ownRoads.Contains(edge.Id) ? paths[v] : paths[v].Append(edge.Id).ToArray();
                if (path.Length > 4 || (paths.TryGetValue(next, out var old) && old.Length <= path.Length)) continue;
                paths[next] = path; if (!pending.Contains(next)) pending.Add(next);
            }
        }
        var targets = view.Board.Vertices.Where(v => !buildings.Any(b => b.LocationId == v.Id)
            && view.Board.Tiles.Any(t => t.Vertices.Contains(v.Id) && t.Resource != "sea" && t.Resource != "fog")
            && ((view.ScenarioId != "forgotten-tribe" && view.ScenarioId != "cloth-for-catan") || view.Board.Tiles.Any(t => t.Vertices.Contains(v.Id) && t.Number.HasValue))
            && !view.Board.Edges.Where(e => e.Vertices.Contains(v.Id)).Any(e => buildings.Any(b => e.Vertices.Contains(b.LocationId)))
            && paths.ContainsKey(v.Id)).Select(v => (target: v.Id, path: paths[v.Id])).ToArray();
        if (targets.Length == 0) return null;
        return targets.OrderByDescending(t => Value(view, t.target) - t.path.Length * 12).ThenBy(t => t.target).First();
    }
}

