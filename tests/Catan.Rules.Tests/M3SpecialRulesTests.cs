using Catan.Core;
using Catan.Core.M3;
using Xunit;
using CommandKind = Catan.Core.M3.CommandKind;
using GamePhase = Catan.Core.M3.GamePhase;
using GameState = Catan.Core.M3.GameState;
using BoardView = Catan.Core.M3.BoardView;
using static Catan.Rules.Tests.M3Harness;

namespace Catan.Rules.Tests;

public sealed class M3SpecialRulesTests
{
    [Fact]
    public void InvasionMayChooseAnotherOwnedMainlandCoastButCannotRerootAfterSailingWest()
    {
        var scenario = SeafarersScenarios.Create("pirate-islands", 4); var board = New("pirate-islands").GetPlayerView("P1").Board;
        var root = board.Vertices.First(v => !board.Settlements.Any(b => b.LocationId == v.Id)
            && board.Tiles.Any(t => scenario.StartingTileIds.Contains(t.Id) && t.Vertices.Contains(v.Id))
            && board.Edges.Any(e => e.Vertices.Contains(v.Id) && SeaEdge(board, e))).Id;
        var game = Fixture(s => s.Settlements = s.Settlements.Append(Piece(root)).ToArray(), "pirate-islands");
        Assert.Contains(root, game.GetPlayerView("P1").LegalInvasionRootVertexIds);
        Accepted(game, Cmd(CommandKind.ChooseInvasionRoute, target: root));
        Assert.Equal(root, game.GetPlayerView("P1").Board.Fortresses.Single(f => f.PlayerId == "P1").InvasionStartingVertexId);
        Rejected(game, Cmd(CommandKind.ChooseInvasionRoute, target: board.Settlements.First(b => b.PlayerId == "P2").LocationId));
        game = PirateRouteFixture(2);
        Assert.Empty(game.GetPlayerView("P1").LegalInvasionRootVertexIds);
        Rejected(game, Cmd(CommandKind.ChooseInvasionRoute, target: board.Fortresses.Single(f => f.PlayerId == "P1").StartingVertexId));
    }

    [Fact]
    public void WesternInvasionCannotBranchOrAttackBeforeReachingOwnFortress()
    {
        var game = Fixture(s => Player(s).Resources = Bag(wood: 2, wool: 2), "pirate-islands");
        Rejected(game, Cmd(CommandKind.AttackFortress));
        game = PirateRouteFixture(2); var state = game.GetAuthoritativeStateForTesting(); Player(state).Resources = Bag(wood: 2, wool: 2); Balance(state); SetState(game, state);
        var board = game.GetPlayerView("P1").Board; var scenario = SeafarersScenarios.Create("pirate-islands", 4);
        var ownVertices = board.Ships.Where(s => s.PlayerId == "P1" && s.IsInvasionRoute).SelectMany(s => board.Edges.Single(e => e.Id == s.LocationId).Vertices).ToHashSet();
        var branch = board.Edges.First(e => e.Vertices.Any(ownVertices.Contains) && SeaEdge(board, e)
            && !board.Ships.Any(s => s.LocationId == e.Id) && !board.Tiles.Any(t => scenario.StartingTileIds.Contains(t.Id) && e.Vertices.All(t.Vertices.Contains)));
        Rejected(game, Cmd(CommandKind.BuildShip, target: branch.Id));
    }

    [Fact]
    public void GoldChoiceDuringSetupReturnsToSamePendingRoadAnchor()
    {
        var reference = New(); var actor = reference.GetPlayerView("P1").ActivePlayerId;
        var vertex = reference.GetPlayerView(actor).LegalVertexIds.First(); var tileId = reference.GetPlayerView(actor).Board.Tiles.First(t => t.Vertices.Contains(vertex) && t.Resource != "sea").Id;
        var game = Fixture(s =>
        {
            s.Phase = GamePhase.SetupSettlement; s.SetupPlacementIndex = s.Players.Length;
            s.Tiles.Single(t => t.Id == tileId).Resource = "gold";
        });
        Accepted(game, Cmd(CommandKind.SetupSettlement, target: vertex));
        var view = game.GetPlayerView("P1"); Assert.Equal(GamePhase.GoldChoice, view.Phase);
        Assert.Equal(1, view.GoldClaims.Single(g => g.PlayerId == "P1").Amount);
        Rejected(game, Cmd(CommandKind.EndTurn));
        var gold = Cmd(CommandKind.ChooseGoldResources); gold.Resources = Bag(ore: 1); Accepted(game, gold);
        view = game.GetPlayerView("P1"); Assert.Equal(GamePhase.SetupRoad, view.Phase); Assert.Equal(vertex, view.PendingDecision.AnchorVertexId);
        Accepted(game, Cmd(CommandKind.SetupRoad, target: view.LegalEdgeIds.First()));
    }

    [Fact]
    public void FogGoldChoiceRestoresRemainingRoadBuildingPlacement()
    {
        var board = New("fog-islands").GetPlayerView("P1").Board;
        var edge = board.Edges.First(e => SeaEdge(board, e) && board.Tiles.Count(t => t.Resource == "fog" && t.Vertices.Intersect(e.Vertices).Any()) == 1);
        var neighbor = board.Edges.First(e => e.Id != edge.Id && SeaEdge(board, e) && e.Vertices.Intersect(edge.Vertices).Any());
        var game = Fixture(s =>
        {
            s.PirateTileId = "frame"; s.Ships = new[] { Ship(neighbor.Id) }; Card(s, DevelopmentCardKind.RoadBuilding);
            var resources = s.HiddenResources.ToList(); Assert.True(resources.Remove("gold")); s.HiddenResources = new[] { "gold" }.Concat(resources).ToArray();
        }, "fog-islands");
        Accepted(game, Cmd(CommandKind.PlayRoadBuilding)); Accepted(game, Cmd(CommandKind.PlaceFreeShip, target: edge.Id));
        Assert.Equal(GamePhase.GoldChoice, game.GetPlayerView("P1").Phase);
        var gold = Cmd(CommandKind.ChooseGoldResources); gold.Resources = Bag(ore: 1); Accepted(game, gold);
        var view = game.GetPlayerView("P1"); Assert.Equal(GamePhase.RoadBuilding, view.Phase); Assert.Equal(1, view.PendingDecision.RemainingRoads);
        Rejected(game, Cmd(CommandKind.EndTurn));
    }

    [Fact]
    public void GiftPortPlacementRestoresRemainingRoadBuildingPlacement()
    {
        var board = New("forgotten-tribe").GetPlayerView("P1").Board; var port = board.GiftPorts[0];
        var reward = board.Edges.Single(e => e.Id == port.EdgeId);
        var neighbor = board.Edges.First(e => e.Id != reward.Id && SeaEdge(board, e) && e.Vertices.Intersect(reward.Vertices).Any());
        var coast = board.Edges.First(e => SeaEdge(board, e) && LandEdge(board, e));
        var game = Fixture(s => { s.PirateTileId = "frame"; s.Ships = new[] { Ship(neighbor.Id) }; s.Settlements = new[] { Piece(coast.Vertices[0]) }; Card(s, DevelopmentCardKind.RoadBuilding); }, "forgotten-tribe");
        Accepted(game, Cmd(CommandKind.PlayRoadBuilding)); Accepted(game, Cmd(CommandKind.PlaceFreeShip, target: reward.Id));
        var view = game.GetPlayerView("P1"); Assert.Equal(GamePhase.PortPlacement, view.Phase);
        var place = Cmd(CommandKind.PlacePort, target: view.LegalPortEdgeIds.First()); place.SourceId = port.Id; Accepted(game, place);
        view = game.GetPlayerView("P1"); Assert.Equal(GamePhase.RoadBuilding, view.Phase); Assert.Equal(1, view.PendingDecision.RemainingRoads);
    }

    [Theory]
    [InlineData("great-wall", 1, 3, 0, 1, 0)]
    [InlineData("great-bridge", 3, 0, 1, 1, 0)]
    [InlineData("grand-monument", 0, 0, 0, 3, 2)]
    [InlineData("grand-theater", 1, 1, 3, 0, 0)]
    [InlineData("grand-castle", 0, 1, 0, 1, 3)]
    public void WonderPrintedCostsAreExactAndFourLevelsWinImmediately(string id, int wood, int brick, int wool, int wheat, int ore)
    {
        // Costs transcribed independently from the five component cards in official 2025 p.1.
        var cost = Bag(wood, brick, wool, wheat, ore);
        var game = Fixture(s =>
        {
            Player(s).WonderId = id;
            foreach (var resource in Resources) Player(s).Resources[resource] = 4 * cost[resource];
        }, "wonders-of-catan");
        Assert.Equal(RulesHarness.Canonical(cost), RulesHarness.Canonical(game.GetPlayerView("P1").Wonders.Single(w => w.Id == id).Cost));
        for (var level = 1; level <= 4; level++)
        {
            Accepted(game, Cmd(CommandKind.BuildWonder, target: id));
            Assert.Equal(level, game.GetPlayerView("P1").Players[0].WonderLevel);
            foreach (var resource in Resources) Assert.Equal((4 - level) * cost[resource], game.GetPlayerView("P1").OwnResources[resource]);
            Assert.Equal(level == 4 ? GamePhase.Finished : GamePhase.Action, game.GetPlayerView("P1").Phase);
        }
        Assert.Equal("P1", game.GetPlayerView("P1").WinnerPlayerId);
        Rejected(game, Cmd(CommandKind.BuildWonder, target: id));
    }

    [Theory]
    [InlineData("great-wall")]
    [InlineData("great-bridge")]
    [InlineData("grand-monument")]
    [InlineData("grand-theater")]
    [InlineData("grand-castle")]
    public void WonderRequiresPrintedQualificationAndIsExclusive(string id)
    {
        var game = Fixture(s => { }, "wonders-of-catan");
        Rejected(game, Cmd(CommandKind.ClaimWonder, target: id));
        var board = game.GetPlayerView("P1").Board; var wonder = game.GetPlayerView("P1").Wonders.Single(w => w.Id == id);
        game = Fixture(s =>
        {
            if (id is "great-wall" or "great-bridge") s.Settlements = new[] { Piece(wonder.VertexIds.First()) };
            else if (id == "grand-theater") s.Cities = new[] { Piece(board.Vertices[0].Id), Piece(board.Vertices[3].Id) };
            else if (id == "grand-castle") { s.Cities = new[] { Piece(board.Vertices[0].Id) }; Player(s).BonusVictoryPoints = 4; }
            else
            {
                var portVertex = board.Ports[0].Vertices[0]; s.Cities = new[] { Piece(portVertex) };
                s.Roads = PathFrom(board, portVertex, 5, e => LandEdge(board, e)).Select(e => Piece(e)).ToArray();
            }
        }, "wonders-of-catan");
        Accepted(game, Cmd(CommandKind.ClaimWonder, target: id));
        Assert.Equal(id, game.GetPlayerView("P1").Players[0].WonderId);
        Rejected(game, Cmd(CommandKind.ClaimWonder, target: id));
        var state = game.GetAuthoritativeStateForTesting(); state.ActivePlayerId = "P2"; SetState(game, state);
        Rejected(game, Cmd(CommandKind.ClaimWonder, "P2", id));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void TenPointsRequireStrictlyHigherWonderLevel(int ownLevel, bool wins)
    {
        var game = Fixture(s =>
        {
            Player(s).WonderId = "grand-theater"; Player(s).WonderLevel = ownLevel; Player(s).BonusVictoryPoints = 10;
            Player(s, "P2").WonderId = "grand-castle"; Player(s, "P2").WonderLevel = 1;
        }, "wonders-of-catan");
        if (wins) Accepted(game, Cmd(CommandKind.DeclareVictory)); else Rejected(game, Cmd(CommandKind.DeclareVictory));
        Assert.Equal(wins ? GamePhase.Finished : GamePhase.Action, game.GetPlayerView("P1").Phase);
    }

    [Fact]
    public void ClothConnectionAwardsOneClothClosesShipsAndUnlocksPirateTheft()
    {
        var board = New("cloth-for-catan").GetPlayerView("P1").Board;
        var village = board.Villages[0]; var edge = board.Edges.First(e => e.Vertices.Contains(village.VertexId) && SeaEdge(board, e));
        var root = edge.Vertices.First(v => v != village.VertexId);
        var game = Fixture(s => { s.PirateTileId = "frame"; s.Settlements = new[] { Piece(root) }; Player(s).Resources = Bag(wood: 1, wool: 1); Card(s, DevelopmentCardKind.Knight); }, "cloth-for-catan");
        // Before relation: even a Knight may not activate pirate in this scenario.
        Accepted(game, Cmd(CommandKind.PlayKnight));
        var sea = board.Tiles.First(t => t.Resource == "sea"); Rejected(game, Cmd(CommandKind.MovePirate, target: sea.Id));
        Accepted(game, Cmd(CommandKind.MoveRobber, target: board.Tiles.First(t => t.Number.HasValue && t.Id != board.RobberTileId && !t.Vertices.Contains(village.VertexId)).Id));
        if (game.GetPlayerView("P1").Phase == GamePhase.RobberSteal) throw new InvalidOperationException("Test robber location must have no neighboring opponent.");
        Accepted(game, Cmd(CommandKind.BuildShip, target: edge.Id));
        var view = game.GetPlayerView("P1"); Assert.Equal(1, view.Players[0].Cloth); Assert.Equal(4, view.Board.Villages[0].Cloth);
        var state = game.GetAuthoritativeStateForTesting(); state.Turn++; state.DevelopmentCardPlayedThisTurn = false; Card(state, DevelopmentCardKind.Knight); Balance(state); SetState(game, state);
        Assert.DoesNotContain(edge.Id, game.GetPlayerView("P1").MovableShipEdgeIds);
        var seaEdge = board.Edges.First(e => e.Vertices.All(sea.Vertices.Contains));
        state = game.GetAuthoritativeStateForTesting(); state.Ships = state.Ships.Append(Ship(seaEdge.Id, "P2")).ToArray(); Player(state, "P2").Cloth = 1; state.ClothSupply--; Balance(state); SetState(game, state);
        Accepted(game, Cmd(CommandKind.PlayKnight)); Accepted(game, Cmd(CommandKind.MovePirate, target: sea.Id));
        var steal = Cmd(CommandKind.StealCloth); steal.OtherPlayerId = "P2"; Accepted(game, steal);
        Assert.Equal(2, game.GetPlayerView("P1").Players[0].Cloth); Assert.Equal(0, game.GetPlayerView("P1").Players[1].Cloth);
    }

    [Fact]
    public void CollectedPortMustBePlacedAtOwnSeparatedCoastAndWorksImmediately()
    {
        var board = New("forgotten-tribe").GetPlayerView("P1").Board; var port = board.GiftPorts[0];
        var reward = board.Edges.Single(e => e.Id == port.EdgeId);
        var neighbor = board.Edges.First(e => e.Id != reward.Id && SeaEdge(board, e) && e.Vertices.Intersect(reward.Vertices).Any());
        var coast = board.Edges.First(e => SeaEdge(board, e) && LandEdge(board, e) && !board.Ports.Any(p => p.Vertices.Intersect(e.Vertices).Any()));
        var game = Fixture(s =>
        { s.PirateTileId = "frame"; s.Ships = new[] { Ship(neighbor.Id) }; s.Settlements = new[] { Piece(coast.Vertices[0]) }; Player(s).Resources = Bag(4, 4, 4, 4, 3); }, "forgotten-tribe");
        Accepted(game, Cmd(CommandKind.BuildShip, target: reward.Id));
        var view = game.GetPlayerView("P1"); Assert.Equal(GamePhase.PortPlacement, view.Phase); Assert.Single(view.OwnHeldPorts);
        Rejected(game, Cmd(CommandKind.EndTurn));
        var bad = Cmd(CommandKind.PlacePort, target: board.Edges.First(e => !e.Vertices.Contains(coast.Vertices[0])).Id); bad.SourceId = port.Id; Rejected(game, bad);
        var place = Cmd(CommandKind.PlacePort, target: view.LegalPortEdgeIds.First()); place.SourceId = port.Id; Accepted(game, place);
        Assert.Empty(game.GetPlayerView("P1").OwnHeldPorts); Assert.Equal(GamePhase.Action, game.GetPlayerView("P1").Phase);
        var trade = Cmd(CommandKind.BankTrade); trade.GiveResource = port.Resource ?? Resource.Wood; trade.ReceiveResource = trade.GiveResource == Resource.Ore ? Resource.Wheat : Resource.Ore;
        trade.GiveAmount = port.Resource.HasValue ? 2 : 3; Accepted(game, trade);
    }

    [Theory]
    [InlineData(2, 2, 3)]
    [InlineData(3, 1, 3)]
    [InlineData(4, 0, 2)]
    public void FortressAttackWinTieLossUseOneDieAndAlwaysEndTurn(int warships, int lost, int strength)
    {
        var game = PirateRouteFixture(warships); var before = game.GetPlayerView("P1");
        var ownCount = before.Board.Ships.Count(s => s.PlayerId == "P1");
        Accepted(game, Cmd(CommandKind.AttackFortress)); var view = game.GetPlayerView("P1");
        // xorshift32-rejection-v1 translates the first nonzero sample 270369 to 270368; modulo 6 + 1 is 3.
        Assert.Equal(3, view.LastPirateStrength); Assert.Equal(strength, view.Board.Fortresses.Single(f => f.PlayerId == "P1").Strength);
        Assert.Equal(ownCount - lost, view.Board.Ships.Count(s => s.PlayerId == "P1"));
        Assert.Equal("P2", view.ActivePlayerId); Assert.Equal(GamePhase.ProductionAwaitRoll, view.Phase);
    }

    [Fact]
    public void KnightConvertsNearestNormalInvasionShipAndNeverMovesPirate()
    {
        var game = PirateRouteFixture(1); var state = game.GetAuthoritativeStateForTesting(); Card(state, DevelopmentCardKind.Knight); Balance(state); SetState(game, state);
        var before = game.GetPlayerView("P1"); var expected = OrderedInvasion(before.Board, "P1").First(s => !s.IsWarship).LocationId;
        Accepted(game, Cmd(CommandKind.PlayKnight)); var view = game.GetPlayerView("P1");
        Assert.True(view.Board.Ships.Single(s => s.LocationId == expected).IsWarship); Assert.Equal(before.Board.PirateTileId, view.Board.PirateTileId);
        Assert.Equal(GamePhase.Action, view.Phase); Assert.Null(view.LargestArmyPlayerId);
        Rejected(game, Cmd(CommandKind.MovePirate, target: "frame"));
    }

    [Fact]
    public void PirateFleetLossBeforeSevenCanRemoveDiscardObligationAndSevenStealsAnyOpponent()
    {
        var scenario = SeafarersScenarios.Create("pirate-islands", 4); var board = New("pirate-islands").GetPlayerView("P1").Board;
        var target = board.Tiles.Single(t => t.Id == scenario.PiratePath[1]);
        var game = Fixture(s =>
        {
            s.PirateTileId = scenario.PiratePath[0]; s.LastDice1 = 1; s.LastDice2 = 6;
            s.Settlements = new[] { Piece(target.Vertices[0]) }; Player(s).Resources = Bag(wood: 8); Player(s, "P3").Resources = Bag(ore: 1);
        }, "pirate-islands");
        var state = game.GetAuthoritativeStateForTesting(); Invoke(game, "ResolveRoll", state); SetState(game, state);
        var view = game.GetPlayerView("P1"); Assert.Equal(7, view.OwnResources.Total); Assert.Empty(view.Discards); Assert.Equal(GamePhase.RobberSteal, view.Phase);
        Assert.Equal(new[] { "P2", "P3", "P4" }, view.PendingDecision.EligibleVictimIds);
        var steal = Cmd(CommandKind.StealResource); steal.OtherPlayerId = "P3"; Accepted(game, steal); Assert.Equal(1, game.GetPlayerView("P1").OwnResources.Ore);
    }

    [Fact]
    public void WinningFleetGoldIsChosenBeforeSevenDiscardAmountIsCalculated()
    {
        var scenario = SeafarersScenarios.Create("pirate-islands", 4); var board = New("pirate-islands").GetPlayerView("P1").Board;
        var target = board.Tiles.Single(t => t.Id == scenario.PiratePath[1]); var game = PirateRouteFixture(2);
        var state = game.GetAuthoritativeStateForTesting(); state.PirateTileId = scenario.PiratePath[0]; state.LastDice1 = 1; state.LastDice2 = 6;
        state.Settlements = new[] { Piece(target.Vertices[0]) }; Player(state).Resources = Bag(wood: 7); Balance(state);
        Invoke(game, "ResolveRoll", state); Invoke(game, "BeginGold", state); SetState(game, state);
        Assert.Equal(GamePhase.GoldChoice, game.GetPlayerView("P1").Phase); Assert.Empty(game.GetPlayerView("P1").Discards);
        var gold = Cmd(CommandKind.ChooseGoldResources); gold.Resources = Bag(ore: 1); Accepted(game, gold);
        var view = game.GetPlayerView("P1"); Assert.Equal(GamePhase.Discard, view.Phase); Assert.Equal(4, view.Discards.Single(d => d.PlayerId == "P1").Amount);
    }

    internal static SeafarersGameSession PirateRouteFixture(int warships)
    {
        var board = New("pirate-islands").GetPlayerView("P1").Board; var fortress = board.Fortresses.Single(f => f.PlayerId == "P1");
        var a = ShortestSeaPath(board, fortress.StartingVertexId, fortress.BeachheadVertexId);
        var b = ShortestSeaPath(board, fortress.BeachheadVertexId, fortress.VertexId);
        var edges = a.Concat(b).ToArray(); Assert.True(edges.Length >= warships);
        return Fixture(s =>
        {
            s.Random.State = 1;
            s.Ships = s.Ships.Where(ship => ship.PlayerId != "P1").Concat(edges.Select((id, i) => new ShipPlacement { LocationId = id, PlayerId = "P1", BuiltTurn = 1, IsInvasionRoute = true, IsWarship = i < warships })).ToArray();
        }, "pirate-islands");
    }

    internal static ShipPlacement[] OrderedInvasion(BoardView board, string player)
    {
        var vertex = board.Fortresses.Single(f => f.PlayerId == player).StartingVertexId; var ships = new List<ShipPlacement>();
        while (true)
        {
            var ship = board.Ships.FirstOrDefault(s => s.PlayerId == player && s.IsInvasionRoute && !ships.Contains(s) && board.Edges.Single(e => e.Id == s.LocationId).Vertices.Contains(vertex));
            if (ship == null) break; ships.Add(ship); vertex = board.Edges.Single(e => e.Id == ship.LocationId).Vertices.First(v => v != vertex);
        }
        return ships.ToArray();
    }

    internal static string[] ShortestSeaPath(BoardView board, string start, string goal)
    {
        var paths = new Dictionary<string, string[]> { [start] = Array.Empty<string>() }; var queue = new Queue<string>(); queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var at = queue.Dequeue(); if (at == goal) return paths[at];
            foreach (var edge in board.Edges.Where(e => e.Vertices.Contains(at) && (SeaEdge(board, e) || board.Tiles.Count(t => e.Vertices.All(t.Vertices.Contains)) == 1)))
                foreach (var next in edge.Vertices) if (!paths.ContainsKey(next)) { paths[next] = paths[at].Append(edge.Id).ToArray(); queue.Enqueue(next); }
        }
        throw new InvalidOperationException("No fixture sea path.");
    }

    internal static string[] PathFrom(BoardView board, string start, int length, Func<Edge, bool> allowed)
    {
        string[]? Visit(string vertex, List<string> edges, HashSet<string> seen)
        {
            if (edges.Count == length) return edges.ToArray();
            foreach (var edge in board.Edges.Where(e => allowed(e) && e.Vertices.Contains(vertex) && !edges.Contains(e.Id)))
            {
                var next = edge.Vertices.First(v => v != vertex); if (seen.Contains(next)) continue;
                edges.Add(edge.Id); seen.Add(next); var result = Visit(next, edges, seen); if (result != null) return result;
                edges.RemoveAt(edges.Count - 1); seen.Remove(next);
            }
            return null;
        }
        return Visit(start, new(), new() { start }) ?? throw new InvalidOperationException("No fixture road path.");
    }
}
