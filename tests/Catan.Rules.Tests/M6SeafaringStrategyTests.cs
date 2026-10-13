using System.Reflection;
using Catan.AI;
using Catan.Core;
using Xunit;
using M3 = Catan.Core.M3;
using static Catan.Rules.Tests.M3Harness;

namespace Catan.Rules.Tests;

public sealed class M6SeafaringStrategyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NewIslandSettlementBeatsAvailableCityAndExecutesForThreePoints(bool winning)
    {
        var scenario = M3.SeafarersScenarios.Create("heading-for-new-shores", 4);
        var home = scenario.Regions.OrderByDescending(r => r.TileIds.Length).First();
        var island = scenario.Regions.Where(r => r.Id != home.Id).OrderBy(r => r.Id).First();
        var board = New().GetPlayerView("P1").Board;
        var root = board.Tiles.First(t => home.TileIds.Contains(t.Id) && t.Number.HasValue).Vertices[0];
        var landing = board.Tiles.First(t => island.TileIds.Contains(t.Id)).Vertices[0];
        var ship = board.Edges.First(e => e.Vertices.Contains(landing) && SeaEdge(board, e));
        var game = Fixture(s =>
        {
            s.Turn = 55;
            s.Settlements = [Piece(root)];
            s.Ships = [Ship(ship.Id)];
            Player(s).HomeRegions = [home.Id];
            Player(s).SettledRegions = [home.Id];
            Player(s).Resources = Bag(1, 1, 1, 2, 3);
            Player(s).BonusVictoryPoints = winning ? 10 : 0;
        });
        var view = game.GetPlayerView("P1");
        Assert.NotEmpty(view.LegalCityVertexIds);
        Assert.Contains(landing, view.LegalVertexIds);
        var before = view.OwnVictoryPoints;
        var saved = game.Save();
        var projection = M6Game.Json(view);
        var command = new SeafarersAi().Decide(view);
        Assert.Equal(M3.CommandKind.BuildSettlement, command.Kind);
        Assert.Contains(board.Tiles, t => island.TileIds.Contains(t.Id) && t.Vertices.Contains(command.TargetId));
        Assert.Equal(saved, game.Save());
        Assert.Equal(projection, M6Game.Json(view));
        command.Id = "航海策略-登岛-" + winning;
        Accepted(game, command);
        Assert.Equal(before + 3, game.GetPlayerView("P1").OwnVictoryPoints);
        Assert.Equal(winning, game.GetPlayerView("P1").Phase == M3.GamePhase.Finished);
        Rejected(game, Cmd(M3.CommandKind.BuildSettlement, target: command.TargetId));
    }

    [Fact]
    public void ExtraTwoShipsAreWorthwhileOnlyIfTheExtraTileRepaysTheirFourCards()
    {
        Tile[] tiles =
        [
            new() { Id = "近岸", Resource = "wool", Number = 6, Vertices = ["近点", "远点", "A", "B", "C", "D"] },
            new() { Id = "远岸", Resource = "wood", Number = 8, Vertices = ["远点", "A", "E", "F", "G", "H"] },
        ];
        // 近点每次掷骰 5/36 张，远点 10/36 张；两船费用四张并预留八次掷骰施工。
        Assert.True(Score(tiles, "远点", 2, 80) > Score(tiles, "近点", 0, 80));
        Assert.True(Score(tiles, "远点", 2, 20) < Score(tiles, "近点", 0, 20));
        Assert.Equal(36 + 80 * 5.0 / 36 - 4, Score(tiles, "近点", 0, 80), 8);
        Assert.Equal(36 + 72 * 10.0 / 36 - 8, Score(tiles, "远点", 2, 80), 8);
    }

    [Fact]
    public void PublicDesertSeparatesRewardRegionsAndAnAlreadySettledRegionIsNotRewardedAgain()
    {
        Tile[] tiles =
        [
            new() { Id = "主岛", Resource = "wood", Number = 6, Vertices = ["家", "A", "B", "C", "D", "E"] },
            new() { Id = "沙漠", Resource = "desert", Vertices = ["A", "B", "F", "G", "H", "I"] },
            new() { Id = "彼岸", Resource = "wool", Number = 8, Vertices = ["F", "G", "目标", "J", "K", "L"] },
        ];
        Assert.Equal(3, Points("through-the-desert", tiles, [Piece("家")], "目标"));
        Assert.Equal(1, Points("through-the-desert", tiles, [Piece("家"), Piece("J")], "目标"));
        Assert.Equal(1, Points("heading-for-new-shores", tiles, [Piece("家")], "目标"));
        Assert.Equal(1, Points("fog-islands", tiles, [Piece("家")], "目标"));
    }

    [Theory]
    [InlineData("heading-for-new-shores", 3)]
    [InlineData("four-islands", 3)]
    [InlineData("through-the-desert", 3)]
    [InlineData("new-world", 2)]
    [InlineData("wonders-of-catan", 2)]
    [InlineData("fog-islands", 1)]
    [InlineData("forgotten-tribe", 1)]
    [InlineData("cloth-for-catan", 1)]
    [InlineData("pirate-islands", 1)]
    public void IslandRewardUsesTheEnabledScenarioRatherThanAssumingEveryLandingIsThreePoints(string scenario, int expected)
    {
        Tile[] tiles =
        [
            new() { Id = "家岛", Resource = "wood", Number = 6, Vertices = ["家", "A", "B", "C", "D", "E"] },
            new() { Id = "新岛", Resource = "wool", Number = 8, Vertices = ["目标", "F", "G", "H", "I", "J"] },
        ];
        Assert.Equal(expected, Points(scenario, tiles, [Piece("家")], "目标"));
    }

    [Fact]
    public void ACoastalRoadDoesNotBecomeAShippingRootWithoutAnOwnedBuilding()
    {
        var board = New().GetPlayerView("P1").Board;
        var coast = board.Vertices.First(v => CoastVertex(board, v.Id));
        var road = board.Edges.First(e => e.Vertices.Contains(coast.Id) && LandEdge(board, e));
        var nextShip = board.Edges.First(e => e.Id != road.Id && e.Vertices.Contains(coast.Id) && SeaEdge(board, e));
        var game = Fixture(s =>
        {
            s.Roads = [Piece(road.Id)];
            Player(s).Resources = Bag(wood: 1, wool: 1);
        });
        Assert.Empty(game.GetPlayerView("P1").LegalShipEdgeIds);
        Rejected(game, Cmd(M3.CommandKind.BuildShip, target: nextShip.Id));
        var command = new SeafarersAi().Decide(game.GetPlayerView("P1"));
        Assert.NotEqual(M3.CommandKind.BuildShip, command.Kind);
        Assert.NotEqual(M3.CommandKind.MoveShip, command.Kind);
        command.Id = "航海策略-路船换乘需要建筑";
        Accepted(game, command);
    }

    [Fact]
    public void RobberTargetsProductiveLeadingOpponentInsteadOfTheLargestLowScoringHand()
    {
        var board = New().GetPlayerView("P1").Board;
        var high = board.Tiles.First(t => t.Number == 6);
        var low = board.Tiles.First(t => t.Number == 2 && !t.Vertices.Intersect(high.Vertices).Any());
        var leadingVertex = high.Vertices.First();
        var lowVertex = low.Vertices.First(v => !board.Edges.Any(e => e.Vertices.Contains(v) && e.Vertices.Contains(leadingVertex)));
        var game = Fixture(s =>
        {
            s.Settlements = [Piece(leadingVertex, "P2"), Piece(lowVertex, "P3")];
            Player(s, "P2").BonusVictoryPoints = 8;
            Player(s, "P2").Resources = Bag(wood: 1);
            Player(s, "P3").Resources = Bag(wood: 10);
            s.Phase = M3.GamePhase.RobberMove;
            s.PendingDecision = new M3.PendingDecision { Kind = "Robber", PlayerId = "P1", ReturnPhase = M3.GamePhase.Action };
        });
        var command = new SeafarersAi().Decide(game.GetPlayerView("P1"));
        Assert.Equal(M3.CommandKind.MoveRobber, command.Kind);
        Assert.Contains(leadingVertex, board.Tiles.Single(t => t.Id == command.TargetId).Vertices);
        command.Id = "航海策略-限制领跑者";
        Accepted(game, command);
        var theft = new SeafarersAi().Decide(game.GetPlayerView("P1"));
        Assert.Equal("P2", theft.OtherPlayerId);
        theft.Id = "航海策略-抢领跑者";
        Accepted(game, theft);
    }

    [Fact]
    public void PirateCanBlockAnOpponentShippingLaneWithoutBlockingOurShips()
    {
        var board = New().GetPlayerView("P1").Board;
        var sea = board.Tiles.First(t => t.Resource == "sea");
        var edge = board.Edges.First(e => e.Vertices.All(sea.Vertices.Contains));
        var game = Fixture(s =>
        {
            s.Ships = [Ship(edge.Id, "P2")];
            Player(s, "P2").Resources = Bag(wool: 2);
            Player(s, "P2").BonusVictoryPoints = 8;
            s.Phase = M3.GamePhase.RobberMove;
            s.PendingDecision = new M3.PendingDecision { Kind = "Robber", PlayerId = "P1", ReturnPhase = M3.GamePhase.Action };
        });
        var command = new SeafarersAi().Decide(game.GetPlayerView("P1"));
        Assert.Equal(M3.CommandKind.MovePirate, command.Kind);
        Assert.True(edge.Vertices.All(board.Tiles.Single(t => t.Id == command.TargetId).Vertices.Contains));
        command.Id = "航海策略-海盗阻船";
        Accepted(game, command);
        Assert.Contains("P2", game.GetPlayerView("P1").PendingDecision.EligibleVictimIds);
    }

    [Fact]
    public void AOneRoadLongestRouteVictoryTakesPriorityOverPreparingOtherProjects()
    {
        var board = New().GetPlayerView("P1").Board;
        var tile = board.Tiles.First(t => t.Resource == "wood");
        var route = Enumerable.Range(0, 4).Select(i => board.Edges.Single(e => e.Vertices.Contains(tile.Vertices[i]) && e.Vertices.Contains(tile.Vertices[i + 1]))).ToArray();
        var game = Fixture(s =>
        {
            s.Roads = route.Select(e => Piece(e.Id)).ToArray();
            Player(s).Resources = Bag(wood: 1, brick: 1);
            Player(s).BonusVictoryPoints = 12;
        });
        var view = game.GetPlayerView("P1");
        Assert.Equal(4, view.Players.Single(p => p.Id == "P1").LongestRoadLength);
        var command = new SeafarersAi().Decide(view);
        Assert.Equal(M3.CommandKind.BuildRoad, command.Kind);
        command.Id = "航海策略-一条路直接获胜";
        Accepted(game, command);
        Assert.Equal(M3.GamePhase.Finished, game.GetPlayerView("P1").Phase);
        Assert.Equal("P1", game.GetPlayerView("P1").LongestRoadPlayerId);
    }

    [Fact]
    public void AnEmptyHandCanReuseAnIdleOpenShipToAdvanceTowardALanding()
    {
        const string root = "V-1:1", source = "EV-1:-1|V-1:1", destination = "EV-1:1|V0:2";
        var game = Fixture(s => { s.Settlements = [Piece(root)]; s.Ships = [Ship(source)]; });
        var view = game.GetPlayerView("P1");
        Assert.Equal(0, view.OwnResources.Total);
        Assert.Empty(view.LegalShipEdgeIds);
        Assert.Contains(source, view.MovableShipEdgeIds);
        var command = new SeafarersAi().Decide(view);
        Assert.Equal(M3.CommandKind.MoveShip, command.Kind);
        Assert.Equal(source, command.SourceId);
        Assert.Equal(destination, command.TargetId);
        command.Id = "航海策略-无木羊也能挪船";
        Accepted(game, command);
        Assert.Equal(0, game.GetPlayerView("P1").OwnResources.Total);
        Assert.Single(game.GetPlayerView("P1").Board.Ships);
        Assert.True(game.GetPlayerView("P1").ShipMovedThisTurn);
        var forbidden = Cmd(M3.CommandKind.MoveShip, target: source);
        forbidden.SourceId = destination;
        Rejected(game, forbidden);
    }

    [Fact]
    public void JoiningTwoThreeRoadChainsCanImmediatelyWinTheLongestRouteAward()
    {
        var board = New().GetPlayerView("P1").Board;
        var landEdges = board.Edges.Where(e => LandEdge(board, e)).ToArray();
        Edge[]? FindPath(string vertex, Edge[] path, HashSet<string> visited)
        {
            if (path.Length == 7) return path;
            foreach (var edge in landEdges.Where(e => e.Vertices.Contains(vertex)))
            {
                var next = edge.Vertices.First(v => v != vertex);
                if (visited.Contains(next)) continue;
                var result = FindPath(next, path.Append(edge).ToArray(), visited.Append(next).ToHashSet());
                if (result != null) return result;
            }
            return null;
        }
        var root = landEdges[0].Vertices[0];
        var path = FindPath(root, [], [root])!;
        Assert.Equal(7, path.Length);
        var game = Fixture(s =>
        {
            s.Roads = path.Where((_, i) => i != 3).Select(e => Piece(e.Id)).ToArray();
            Player(s).Resources = Bag(wood: 1, brick: 1);
            Player(s).BonusVictoryPoints = 12;
        });
        Assert.Equal(3, game.GetPlayerView("P1").Players.Single(p => p.Id == "P1").LongestRoadLength);
        var command = new SeafarersAi().Decide(game.GetPlayerView("P1"));
        Assert.Equal(M3.CommandKind.BuildRoad, command.Kind);
        command.Id = "航海策略-一条路连接两条三段链";
        Accepted(game, command);
        Assert.Equal(M3.GamePhase.Finished, game.GetPlayerView("P1").Phase);
        Assert.True(game.GetPlayerView("P1").Players.Single(p => p.Id == "P1").LongestRoadLength >= 5);
    }

    [Fact]
    public void PirateFleetPassingProductiveLandDoesNotPlanAnIllegalSettlementOutsideItsBeachhead()
    {
        var scenario = M3.SeafarersScenarios.Create("pirate-islands", 4);
        var board = New("pirate-islands").GetPlayerView("P1").Board;
        var ownFort = board.Fortresses.Single(f => f.PlayerId == "P1");
        var west = board.Tiles.Where(t => t.Number.HasValue && !scenario.StartingTileIds.Contains(t.Id)).SelectMany(t => t.Vertices)
            .First(v => v != ownFort.BeachheadVertexId && v != ownFort.VertexId
                && !board.Edges.Any(e => e.Vertices.Contains(v) && (e.Vertices.Contains(ownFort.VertexId) || board.Settlements.Any(b => e.Vertices.Contains(b.LocationId)))));
        var ship = board.Edges.First(e => e.Vertices.Contains(west) && SeaEdge(board, e) && !board.Ships.Any(s => s.LocationId == e.Id));
        var game = Fixture(s =>
        {
            s.Turn = 100;
            s.Fortresses.Single(f => f.PlayerId == "P1").Strength = 0;
            s.Settlements = s.Settlements.Append(Piece(ownFort.VertexId)).ToArray();
            s.Ships = s.Ships.Append(Ship(ship.Id)).ToArray();
            Player(s).Resources = Bag(5, 5, 5, 5, 5);
        }, "pirate-islands");
        Rejected(game, Cmd(M3.CommandKind.BuildSettlement, target: west));
        var view = game.GetPlayerView("P1");
        var expansion = ((string target, string[] path, bool ship)?)typeof(SeafarersAi)
            .GetMethod("Expansion", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [view, null]);
        Assert.True(expansion.HasValue);
        Assert.True(expansion.Value.target == ownFort.BeachheadVertexId || board.Tiles.Any(t => scenario.StartingTileIds.Contains(t.Id) && t.Vertices.Contains(expansion.Value.target)));
        var command = new SeafarersAi().Decide(view);
        Assert.NotEqual(M3.CommandKind.EndTurn, command.Kind);
        command.Id = "航海策略-不能在非指定西岸建村";
        Accepted(game, command);
    }

    private static readonly Type Planning = typeof(SeafarersAi).Assembly.GetType("Catan.AI.SeafaringPlanning")!;
    private static int Points(string scenario, Tile[] tiles, PiecePlacement[] own, string target) =>
        (int)Planning.GetMethod("SettlementPoints", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [scenario, tiles, own, target])!;
    private static double Score(Tile[] tiles, string target, int path, int remaining) =>
        (double)Planning.GetMethod("SettlementValue", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null,
            ["heading-for-new-shores", tiles, Array.Empty<Edge>(), Array.Empty<PiecePlacement>(), "P1", target, path, remaining, 2, 14])!;
}
