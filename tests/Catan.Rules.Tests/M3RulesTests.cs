using Catan.Core;
using Catan.Core.M3;
using Xunit;
using CommandKind = Catan.Core.M3.CommandKind;
using GamePhase = Catan.Core.M3.GamePhase;
using GameState = Catan.Core.M3.GameState;
using BoardView = Catan.Core.M3.BoardView;
using static Catan.Rules.Tests.M3Harness;

namespace Catan.Rules.Tests;

public sealed class M3RulesTests
{
    [Fact]
    public void ShipCostsWoodAndWoolAndCoastalEdgeCannotHoldBothPieceTypes()
    {
        var board = New().GetPlayerView("P1").Board;
        var coast = board.Edges.First(e => SeaEdge(board, e) && LandEdge(board, e));
        var game = Fixture(s => { s.PirateTileId = null!; s.Settlements = new[] { Piece(coast.Vertices[0]) }; Player(s).Resources = Bag(2, 2, 2); });
        Accepted(game, Cmd(CommandKind.BuildShip, target: coast.Id));
        var view = game.GetPlayerView("P1");
        Assert.Equal(1, view.OwnResources.Wood); Assert.Equal(1, view.OwnResources.Wool); Assert.Equal(2, view.OwnResources.Brick);
        Assert.Equal(14, view.Players[0].ShipsRemaining);
        Assert.Equal(view.Turn, Assert.Single(view.Board.Ships).BuiltTurn);
        Rejected(game, Cmd(CommandKind.BuildRoad, target: coast.Id));
        Rejected(game, Cmd(CommandKind.BuildShip, target: coast.Id));
        var inland = board.Edges.First(e => board.Tiles.Count(t => t.Resource != "sea" && e.Vertices.All(t.Vertices.Contains)) == 2);
        game = Fixture(s => { s.PirateTileId = null!; s.Settlements = new[] { Piece(inland.Vertices[0]) }; Player(s).Resources = Bag(wood: 1, wool: 1); });
        Rejected(game, Cmd(CommandKind.BuildShip, target: inland.Id));
    }

    [Fact]
    public void ShipCannotConnectOnlyToRoadOrThroughEnemyBuilding()
    {
        var board = New().GetPlayerView("P1").Board;
        var junction = board.Vertices.Select(v => new { Vertex = v.Id, Edges = board.Edges.Where(e => e.Vertices.Contains(v.Id)).ToArray() })
            .First(v => v.Edges.Any(e => LandEdge(board, e)) && v.Edges.Count(e => SeaEdge(board, e)) >= 2);
        var road = junction.Edges.First(e => LandEdge(board, e));
        var ship = junction.Edges.First(e => e.Id != road.Id && SeaEdge(board, e));
        var game = Fixture(s => { s.PirateTileId = null!; s.Roads = new[] { Piece(road.Id) }; Player(s).Resources = Bag(wood: 2, wool: 2); });
        Rejected(game, Cmd(CommandKind.BuildShip, target: ship.Id));
        game = Fixture(s => { s.PirateTileId = null!; s.Ships = new[] { Ship(road.Id) }; s.Settlements = new[] { Piece(junction.Vertex, "P2") }; Player(s).Resources = Bag(wood: 2, wool: 2); });
        Rejected(game, Cmd(CommandKind.BuildShip, target: ship.Id));
        game = Fixture(s => { s.PirateTileId = null!; s.Roads = new[] { Piece(road.Id) }; s.Settlements = new[] { Piece(junction.Vertex) }; Player(s).Resources = Bag(wood: 2, wool: 2); });
        Accepted(game, Cmd(CommandKind.BuildShip, target: ship.Id));
    }

    [Fact]
    public void CoastalSetupAllowsShipAndPreservesFiniteSupplies()
    {
        var game = New();
        var player = game.GetPlayerView("P1").ActivePlayerId;
        var view = game.GetPlayerView(player);
        var vertex = view.LegalVertexIds.First(v => CoastVertex(view.Board, v));
        Accepted(game, Cmd(CommandKind.SetupSettlement, player, vertex));
        var ships = game.GetPlayerView(player).LegalShipEdgeIds;
        Assert.NotEmpty(ships);
        Accepted(game, Cmd(CommandKind.SetupShip, player, ships.First()));
        var after = game.GetPlayerView(player);
        Assert.Single(after.Board.Ships); Assert.Empty(after.Board.Roads);
        Assert.Equal(14, after.Players.Single(p => p.Id == player).ShipsRemaining);
        Assert.Equal(0, after.OwnResources.Total);
    }

    [Fact]
    public void OpenShipMovesOnceWithoutPaymentAndCannotMoveAgainOrBeNewlyBuilt()
    {
        var board = New().GetPlayerView("P1").Board;
        var junction = board.Vertices.First(v => CoastVertex(board, v.Id) && board.Edges.Count(e => e.Vertices.Contains(v.Id) && SeaEdge(board, e)) >= 2);
        var edges = board.Edges.Where(e => e.Vertices.Contains(junction.Id) && SeaEdge(board, e)).Take(2).ToArray();
        var game = Fixture(s => { s.PirateTileId = null!; s.Settlements = new[] { Piece(junction.Id) }; s.Ships = new[] { Ship(edges[0].Id) }; });
        var move = Cmd(CommandKind.MoveShip, target: edges[1].Id); move.SourceId = edges[0].Id;
        Accepted(game, move);
        Assert.True(game.GetPlayerView("P1").ShipMovedThisTurn);
        Assert.Equal(0, game.GetPlayerView("P1").OwnResources.Total);
        Assert.Equal(edges[1].Id, Assert.Single(game.GetPlayerView("P1").Board.Ships).LocationId);
        var second = Cmd(CommandKind.MoveShip, target: edges[0].Id); second.SourceId = edges[1].Id; Rejected(game, second);
        game = Fixture(s => { s.PirateTileId = null!; s.Settlements = new[] { Piece(junction.Id) }; s.Ships = new[] { Ship(edges[0].Id, built: s.Turn) }; });
        Rejected(game, move);
    }

    [Fact]
    public void ShipMoveCannotUseRemovedSourceAsItsOwnConnection()
    {
        var board = New().GetPlayerView("P1").Board;
        var pair = (from a in board.Edges where SeaEdge(board, a)
                    from b in board.Edges where b.Id != a.Id && SeaEdge(board, b) && a.Vertices.Intersect(b.Vertices).Any()
                    let shared = a.Vertices.Intersect(b.Vertices).Single()
                    let root = a.Vertices.First(v => v != shared)
                    where CoastVertex(board, root)
                    select (a, b, root)).First();
        var game = Fixture(s => { s.PirateTileId = null!; s.Settlements = new[] { Piece(pair.root) }; s.Ships = new[] { Ship(pair.a.Id) }; });
        var move = Cmd(CommandKind.MoveShip, target: pair.b.Id); move.SourceId = pair.a.Id;
        Rejected(game, move);
    }

    [Fact]
    public void ClosedShipRouteAndNonEndpointCannotMoveEvenWithEnemyInterruption()
    {
        var board = New().GetPlayerView("P1").Board;
        var path = FindPath(board, 4, e => SeaEdge(board, e));
        foreach (var interrupted in new[] { false, true })
        {
            var game = Fixture(s =>
            {
                s.PirateTileId = null!; s.Ships = path.edges.Select(e => Ship(e)).ToArray();
                s.Settlements = new[] { Piece(path.vertices[0]), Piece(path.vertices[^1]) }
                    .Concat(interrupted ? new[] { Piece(path.vertices[2], "P2") } : Array.Empty<PiecePlacement>()).ToArray();
            });
            Assert.DoesNotContain(path.edges[0], game.GetPlayerView("P1").MovableShipEdgeIds);
            Assert.DoesNotContain(path.edges[^1], game.GetPlayerView("P1").MovableShipEdgeIds);
        }
    }

    [Fact]
    public void PirateBlocksBuildAndMovingAwayAndOntoItsSeaHex()
    {
        var board = New().GetPlayerView("P1").Board;
        var sea = board.Tiles.First(t => t.Resource == "sea" && board.Edges.Count(e => e.Vertices.All(t.Vertices.Contains)) >= 3);
        var edge = board.Edges.First(e => e.Vertices.All(sea.Vertices.Contains));
        var game = Fixture(s => { s.PirateTileId = sea.Id; s.Settlements = new[] { Piece(edge.Vertices[0]) }; Player(s).Resources = Bag(wood: 1, wool: 1); });
        Rejected(game, Cmd(CommandKind.BuildShip, target: edge.Id));
        game = Fixture(s => { s.PirateTileId = sea.Id; s.Ships = new[] { Ship(edge.Id) }; });
        Assert.DoesNotContain(edge.Id, game.GetPlayerView("P1").MovableShipEdgeIds);
        var outside = board.Edges.First(e => SeaEdge(board, e) && e.Vertices.Intersect(sea.Vertices).Count() == 1);
        var root = outside.Vertices.Intersect(sea.Vertices).Single();
        var into = board.Edges.First(e => e.Vertices.Contains(root) && e.Vertices.All(sea.Vertices.Contains));
        game = Fixture(s => { s.PirateTileId = sea.Id; s.Settlements = new[] { Piece(root) }; s.Ships = new[] { Ship(outside.Id) }; });
        var move = Cmd(CommandKind.MoveShip, target: into.Id); move.SourceId = outside.Id; Rejected(game, move);
    }

    [Fact]
    public void FifteenShipsExhaustSupplyButZeroRoadSupplyStillAllowsRoadBuildingShips()
    {
        var board = New().GetPlayerView("P1").Board;
        var seaEdges = board.Edges.Where(e => SeaEdge(board, e)).ToArray();
        var game = Fixture(s => { s.PirateTileId = "frame"; s.Ships = seaEdges.Take(15).Select(e => Ship(e.Id)).ToArray(); Player(s).Resources = Bag(wood: 1, wool: 1); });
        Rejected(game, Cmd(CommandKind.BuildShip, target: seaEdges[15].Id));
        var root = board.Vertices.First(v => CoastVertex(board, v.Id));
        var protectedEdges = board.Edges.Where(e => e.Vertices.Contains(root.Id)).Select(e => e.Id).ToHashSet();
        game = Fixture(s =>
        {
            s.PirateTileId = "frame"; s.Settlements = new[] { Piece(root.Id) };
            s.Roads = board.Edges.Where(e => !protectedEdges.Contains(e.Id) && LandEdge(board, e)).Take(15).Select(e => Piece(e.Id)).ToArray();
            Card(s, DevelopmentCardKind.RoadBuilding);
        });
        Assert.Equal(0, game.GetPlayerView("P1").Players[0].Pieces.Roads);
        Accepted(game, Cmd(CommandKind.PlayRoadBuilding));
        Accepted(game, Cmd(CommandKind.PlaceFreeShip, target: game.GetPlayerView("P1").LegalShipEdgeIds.First()));
        Accepted(game, Cmd(CommandKind.PlaceFreeShip, target: game.GetPlayerView("P1").LegalShipEdgeIds.First()));
        Assert.Equal(13, game.GetPlayerView("P1").Players[0].ShipsRemaining); Assert.Equal(GamePhase.Action, game.GetPlayerView("P1").Phase);
    }

    [Fact]
    public void KnightMovesPirateAndOnlyShipsOnDestinationAreEligibleForTheft()
    {
        var board = New().GetPlayerView("P1").Board;
        var sea = board.Tiles.First(t => t.Resource == "sea");
        var edge = board.Edges.First(e => e.Vertices.All(sea.Vertices.Contains));
        var game = Fixture(s =>
        {
            s.PirateTileId = null!; s.Phase = GamePhase.ProductionAwaitRoll;
            s.Ships = new[] { Ship(edge.Id, "P2") }; s.Settlements = new[] { Piece(edge.Vertices[0], "P3") };
            Player(s, "P2").Resources = Bag(ore: 1); Player(s, "P3").Resources = Bag(wool: 1); Card(s, DevelopmentCardKind.Knight);
        });
        Accepted(game, Cmd(CommandKind.PlayKnight));
        Rejected(game, Cmd(CommandKind.MovePirate, target: board.Tiles.First(t => t.Resource != "sea").Id));
        Accepted(game, Cmd(CommandKind.MovePirate, target: sea.Id));
        Assert.Equal(new[] { "P2" }, game.GetPlayerView("P1").PendingDecision.EligibleVictimIds);
        var theft = Cmd(CommandKind.StealResource); theft.OtherPlayerId = "P3"; Rejected(game, theft);
        theft = Cmd(CommandKind.StealResource); theft.OtherPlayerId = "P2"; Accepted(game, theft);
        Assert.Equal(1, game.GetPlayerView("P1").OwnResources.Ore); Assert.Equal(0, game.GetPlayerView("P2").OwnResources.Total);
        Assert.Equal(GamePhase.ProductionAwaitRoll, game.GetPlayerView("P1").Phase);
    }

    [Fact]
    public void RoadBuildingCanPlaceShipAndRoadWithNoResourcePayment()
    {
        var board = New().GetPlayerView("P1").Board;
        var vertex = board.Vertices.First(v => CoastVertex(board, v.Id) && board.Edges.Count(e => e.Vertices.Contains(v.Id)) >= 3).Id;
        var ship = board.Edges.First(e => e.Vertices.Contains(vertex) && SeaEdge(board, e));
        var road = board.Edges.First(e => e.Vertices.Contains(vertex) && e.Id != ship.Id && LandEdge(board, e));
        var game = Fixture(s => { s.PirateTileId = null!; s.Settlements = new[] { Piece(vertex) }; Card(s, DevelopmentCardKind.RoadBuilding); });
        Accepted(game, Cmd(CommandKind.PlayRoadBuilding));
        Rejected(game, Cmd(CommandKind.EndTurn));
        Accepted(game, Cmd(CommandKind.PlaceFreeShip, target: ship.Id));
        Assert.Equal(GamePhase.RoadBuilding, game.GetPlayerView("P1").Phase);
        Accepted(game, Cmd(CommandKind.PlaceFreeRoad, target: road.Id));
        Assert.Equal(GamePhase.Action, game.GetPlayerView("P1").Phase);
        Assert.Equal(0, game.GetPlayerView("P1").OwnResources.Total);
        Assert.Single(game.GetPlayerView("P1").Board.Ships); Assert.Single(game.GetPlayerView("P1").Board.Roads);
    }

    [Fact]
    public void GoldVillageAndCityChooseOneAndTwoResourcesWithoutCreatingNewResourceType()
    {
        var board = New().GetPlayerView("P1").Board;
        var tile = board.Tiles.First(t => t.Resource == "gold" && t.Number.HasValue);
        var game = Fixture(s => { s.RobberTileId = board.Tiles.First(t => t.Resource == "desert").Id; s.Settlements = new[] { Piece(tile.Vertices[0]) }; s.Cities = new[] { Piece(tile.Vertices[2], "P2") }; });
        var state = game.GetAuthoritativeStateForTesting(); state.LastDice1 = 1; state.LastDice2 = tile.Number!.Value - 1;
        Invoke(game, "ResolveProduction", state); Invoke(game, "BeginGold", state); SetState(game, state);
        var view = game.GetPlayerView("P1");
        Assert.Equal(GamePhase.GoldChoice, view.Phase);
        Assert.Equal(1, view.GoldClaims.Single(g => g.PlayerId == "P1").Amount);
        Assert.Equal(2, view.GoldClaims.Single(g => g.PlayerId == "P2").Amount);
        var bad = Cmd(CommandKind.ChooseGoldResources); bad.Resources = Bag(wood: 2); Rejected(game, bad);
        while (game.GetPlayerView("P1").GoldClaims.Length > 0)
        {
            var claim = game.GetPlayerView("P1").GoldClaims[0];
            var choose = Cmd(CommandKind.ChooseGoldResources, claim.PlayerId); choose.Resources = claim.Amount == 1 ? Bag(wood: 1) : Bag(wool: 1, ore: 1);
            Accepted(game, choose);
        }
        Assert.Equal(1, game.GetPlayerView("P1").OwnResources.Wood);
        Assert.Equal(1, game.GetPlayerView("P2").OwnResources.Wool); Assert.Equal(1, game.GetPlayerView("P2").OwnResources.Ore);
    }

    [Fact]
    public void MixedRouteJoinsOnlyAtOwnBuilding()
    {
        var board = New().GetPlayerView("P1").Board;
        var path = FindPath(board, 5, e => LandEdge(board, e) && SeaEdge(board, e));
        foreach (var joined in new[] { false, true })
        {
            var game = Fixture(s =>
            {
                s.Roads = path.edges.Take(2).Select(e => Piece(e)).ToArray(); s.Ships = path.edges.Skip(2).Select(e => Ship(e)).ToArray();
                if (joined) s.Settlements = new[] { Piece(path.vertices[2]) };
            });
            var view = game.GetPlayerView("P1");
            Assert.Equal(joined ? 5 : 3, view.Players.Single(p => p.Id == "P1").LongestRoadLength);
            Assert.Equal(joined ? "P1" : null, view.LongestRoadPlayerId);
        }
    }

    internal static (string[] vertices, string[] edges) FindPath(BoardView board, int length, Func<Edge, bool> allowed)
    {
        (string[], string[])? Visit(string vertex, List<string> vertices, List<string> edges)
        {
            if (edges.Count == length) return (vertices.ToArray(), edges.ToArray());
            foreach (var edge in board.Edges.Where(e => allowed(e) && e.Vertices.Contains(vertex) && !edges.Contains(e.Id)))
            {
                var next = edge.Vertices.First(v => v != vertex); if (vertices.Contains(next)) continue;
                vertices.Add(next); edges.Add(edge.Id); var result = Visit(next, vertices, edges); if (result != null) return result;
                vertices.RemoveAt(vertices.Count - 1); edges.RemoveAt(edges.Count - 1);
            }
            return null;
        }
        foreach (var vertex in board.Vertices)
        { var found = Visit(vertex.Id, new List<string> { vertex.Id }, new()); if (found != null) return found.Value; }
        throw new InvalidOperationException("Reference graph contains no requested path.");
    }
}
