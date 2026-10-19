using System.Reflection;
using Catan.AI;
using Catan.Core;
using Xunit;
using M3 = Catan.Core.M3;
using static Catan.Rules.Tests.M3Harness;

namespace Catan.Rules.Tests;

public sealed class Ai20SeafaringSetupTests
{
    [Theory]
    [InlineData("base")][InlineData("seafarers")][InlineData("cities")][InlineData("combined")]
    public void MiddleSeatsKeepChoosingExecutableOpeningCandidates(string rules)
    {
        var game = M6Game.Create(rules);
        var seats = new HashSet<string>();
        for (var i = 0; i < 40; i++)
        {
            dynamic publicView = game.View();
            if (!publicView.Phase.ToString().StartsWith("Setup")) break;
            string actor = M6Game.Actor(publicView);
            dynamic view = game.View(actor);
            dynamic command = game.Decide(view);
            if (view.Phase.ToString() == "SetupSettlement")
            {
                seats.Add(actor);
                Assert.Contains((string)command.TargetId, (string[])view.LegalVertexIds);
            }
            command.Id = "AI2初设-" + i;
            game.Accept(command);
            game.AssertInvariants();
        }
        Assert.Equal(4, seats.Count);
    }

    [Theory]
    [InlineData("base", 2)][InlineData("base", 3)]
    [InlineData("seafarers", 2)][InlineData("seafarers", 3)]
    [InlineData("cities", 2)][InlineData("cities", 3)]
    [InlineData("combined", 2)][InlineData("combined", 3)]
    public void MiddleSeatsLeanTowardRichBrickButCanYieldToMoreProductiveOtherResources(string rules, int seat)
    {
        var game = M6Game.Create(rules);
        dynamic view = game.View(M6Game.Actor(game.View()));
        string[] players = ((Array)view.Players).Cast<object>().Select(p => (string)p.GetType().GetProperty("Id")!.GetValue(p)!).ToArray();
        string actor = view.PlayerId;
        int index = Array.IndexOf(players, actor);
        Set(view, "StartingPlayerId", players[(index - seat + 1 + players.Length) % players.Length]);
        Set(view, "LegalVertexIds", new[] { "砖点", "其他点" });
        Set(view.Board, "Edges", Array.Empty<Edge>());
        Set(view.Board, "Tiles", new[] { Tile("砖", "brick", 6, "砖点"), Tile("羊", "wool", 6, "其他点") });
        Assert.Equal("砖点", (string)game.Decide(view).TargetId);
        Set(view.Board, "Tiles", new[] { Tile("砖", "brick", 6, "砖点"), Tile("羊", "wool", 6, "其他点"),
            Tile("麦", "wheat", 8, "其他点"), Tile("矿", "ore", 5, "其他点") });
        Assert.Equal("其他点", (string)game.Decide(view).TargetId);
    }

    [Fact]
    public void DenialPreferenceOnlyAppliesToFirstRoundMiddleSeatsInFourPlayerGames()
    {
        var game = M6Game.Create("seafarers");
        dynamic view = game.View(M6Game.Actor(game.View()));
        Set(view, "PlayerId", "P2"); Set(view, "ActivePlayerId", "P2"); Set(view, "StartingPlayerId", "P1");
        Set(view, "LegalVertexIds", new[] { "砖点", "其他点" });
        Set(view.Board, "Edges", Array.Empty<Edge>());
        Set(view.Board, "Tiles", new[] { Tile("砖", "brick", 6, "砖点"), Tile("羊", "wool", 6, "其他点") });
        Assert.Equal("砖点", (string)game.Decide(view).TargetId);
        Set(view, "StartingPlayerId", "P2");
        Assert.Equal("其他点", (string)game.Decide(view).TargetId);
        Set(view, "StartingPlayerId", "P3");
        Assert.Equal("其他点", (string)game.Decide(view).TargetId);
        Set(view, "StartingPlayerId", "P1");
        Set(view.Board, "Settlements", new[] { Piece("家", "P2") });
        Assert.Equal("其他点", (string)game.Decide(view).TargetId);
    }

    [Theory]
    [InlineData("brick")][InlineData("wood")]
    public void ClusterPreferenceIsBoundedAndPoorTilesReceiveNoDenialBonus(string resource)
    {
        var game = M6Game.Create("seafarers");
        dynamic view = game.View(M6Game.Actor(game.View()));
        Set(view, "PlayerId", "P2"); Set(view, "ActivePlayerId", "P2");
        Set(view.Board, "Edges", Array.Empty<Edge>());
        Set(view.Board, "Tiles", new[] { Tile("一", resource, 5, "点"), Tile("二", resource, 9, "点") });
        Set(view, "StartingPlayerId", "P1"); double middle = OpeningScore(view, "点");
        Set(view, "StartingPlayerId", "P2"); double first = OpeningScore(view, "点");
        Assert.InRange(middle - first, 0.1, 6.0);
        Set(view.Board, "Tiles", new[] { Tile("一", resource, 2, "点"), Tile("二", resource, 12, "点") });
        Set(view, "StartingPlayerId", "P1"); middle = OpeningScore(view, "点");
        Set(view, "StartingPlayerId", "P2"); first = OpeningScore(view, "点");
        Assert.Equal(first, middle);
    }

    [Fact]
    public void MiddleSeatBrickPortDenialRequiresRichBrickOnTheBoardAndSomeLocalProduction()
    {
        var game = M6Game.Create("seafarers");
        dynamic view = game.View(M6Game.Actor(game.View()));
        Set(view, "PlayerId", "P2"); Set(view, "ActivePlayerId", "P2");
        Set(view.Board, "Edges", Array.Empty<Edge>());
        Set(view.Board, "Ports", new[] { new M3.Port { Id = "砖港", Resource = Resource.Brick, Vertices = ["港", "海"] } });
        Set(view.Board, "Tiles", new[] { Tile("当地", "wool", 5, "港"), Tile("远砖一", "brick", 6, "远一"), Tile("远砖二", "brick", 8, "远二") });
        Set(view, "StartingPlayerId", "P1"); double middle = OpeningScore(view, "港");
        Set(view, "StartingPlayerId", "P2"); double first = OpeningScore(view, "港");
        Assert.InRange(middle - first, 0.1, 3.0);
        Set(view.Board, "Tiles", new[] { Tile("当地", "wool", 2, "港"), Tile("远砖一", "brick", 6, "远一"), Tile("远砖二", "brick", 8, "远二") });
        Set(view, "StartingPlayerId", "P1"); middle = OpeningScore(view, "港");
        Set(view, "StartingPlayerId", "P2"); first = OpeningScore(view, "港");
        Assert.Equal(first, middle);
    }

    [Fact]
    public void IndependentIslandsTakePriorityOverAProductiveDesertCrossing()
    {
        Tile[] tiles =
        [
            Tile("主岛", "wood", 6, "家", "A", "B"),
            Tile("沙漠", "desert", null, "A", "B", "C", "D"),
            Tile("陆路彼岸", "wheat", 6, "C", "D", "彼岸"),
            Tile("彼岸矿", "ore", 8, "C", "D", "彼岸"),
            Tile("海岛甲", "wool", 5, "甲"), Tile("海岛乙", "wood", 4, "乙")
        ];
        // 三处均是同样的三分机会；沙漠后方较肥，却还需修路，两个独立岛已可登陆。
        Assert.True(Score(tiles, "甲", 0) > Score(tiles, "彼岸", 1));
        Assert.True(Score(tiles, "乙", 0) > Score(tiles, "彼岸", 1));
    }

    [Fact]
    public void MultipleLegalIslandLandingsBeatContinuingDesertRoads()
    {
        const string scenarioId = "through-the-desert";
        var scenario = M3.SeafarersScenarios.Create(scenarioId, 4);
        var board = New(scenarioId).GetPlayerView("P1").Board;
        var home = scenario.Regions.OrderByDescending(r => r.TileIds.Length).First();
        var mainland = ConnectedLand(board.Tiles, home.TileIds[0]);
        var islands = scenario.Regions.Where(r => !r.TileIds.Any(mainland.Contains)).Take(2).ToArray();
        Assert.Equal(2, islands.Length);
        var landings = islands.Select(r => board.Tiles.First(t => r.TileIds.Contains(t.Id)).Vertices[0]).ToArray();
        var approach = landings.Select(v => board.Edges.First(e => e.Vertices.Contains(v) && SeaEdge(board, e))).ToArray();
        var desert = board.Tiles.First(t => t.Resource == "desert");
        var road = board.Edges.First(e => e.Vertices.All(desert.Vertices.Contains));
        var root = board.Tiles.First(t => home.TileIds.Contains(t.Id)).Vertices[0];
        var game = Fixture(s =>
        {
            s.Settlements = [Piece(root)]; s.Ships = approach.Select(e => Ship(e.Id)).ToArray(); s.Roads = [Piece(road.Id)];
            Player(s).HomeRegions = [home.Id]; Player(s).SettledRegions = [home.Id]; Player(s).Resources = Bag(1, 1, 1, 1);
        }, scenarioId);
        var view = game.GetPlayerView("P1");
        Assert.All(landings, v => Assert.Contains(v, view.LegalVertexIds));
        Assert.NotEmpty(view.LegalEdgeIds);
        var command = new SeafarersAi().Decide(view);
        Assert.Equal(M3.CommandKind.BuildSettlement, command.Kind);
        Assert.Contains(islands, r => board.Tiles.Any(t => r.TileIds.Contains(t.Id) && t.Vertices.Contains(command.TargetId)));
        command.Id = "AI2多岛登陆"; Accepted(game, command);
        Rejected(game, Cmd(M3.CommandKind.BuildSettlement, target: command.TargetId));
    }

    [Fact]
    public void PirateCanBlockAnEmptyShippingApproachBeforeTheOpponentBuildsThere()
    {
        var board = New().GetPlayerView("P1").Board;
        // 对方还没有船；从一资源海岸出发，海盗必须预见下一步造船，不能仅查看现存舰队。
        var root = board.Vertices.First(v => CoastVertex(board, v.Id)
            && board.Tiles.Count(t => t.Number.HasValue && t.Vertices.Contains(v.Id)) == 1).Id;
        var game = Fixture(s =>
        {
            s.Settlements = [Piece(root, "P2")];
            foreach (var tile in s.Tiles.Where(t => t.Number.HasValue && t.Vertices.Contains(root))) tile.Number = 2;
            Player(s, "P2").Resources = Bag(wood: 1, wool: 1);
            s.ActivePlayerId = "P2";
        });
        var available = game.GetPlayerView("P2").LegalShipEdgeIds;
        Assert.NotEmpty(available);
        var state = game.GetAuthoritativeStateForTesting();
        state.ActivePlayerId = "P1"; state.Phase = M3.GamePhase.RobberMove;
        state.PendingDecision = new M3.PendingDecision { Kind = "Robber", PlayerId = "P1", ReturnPhase = M3.GamePhase.Action };
        SetState(game, state);
        var command = new SeafarersAi().Decide(game.GetPlayerView("P1"));
        Assert.Equal(M3.CommandKind.MovePirate, command.Kind);
        var empty = board.Edges.First(e => available.Contains(e.Id)
            && e.Vertices.All(board.Tiles.Single(t => t.Id == command.TargetId).Vertices.Contains));
        Assert.Empty(game.GetPlayerView("P1").Board.Ships);
        command.Id = "AI2提前封锁登岛"; Accepted(game, command);
        // 在同一权威局面验证被封锁的下一段确实无法建设，且拒绝不改变状态。
        state = game.GetAuthoritativeStateForTesting();
        state.ActivePlayerId = "P2"; state.Phase = M3.GamePhase.Action; state.PendingDecision = null!;
        SetState(game, state);
        Assert.DoesNotContain(empty.Id, game.GetPlayerView("P2").LegalShipEdgeIds);
        Rejected(game, Cmd(M3.CommandKind.BuildShip, "P2", empty.Id));
        state = game.GetAuthoritativeStateForTesting(); state.PirateTileId = "frame"; SetState(game, state);
        Assert.Contains(empty.Id, game.GetPlayerView("P2").LegalShipEdgeIds);
        Accepted(game, Cmd(M3.CommandKind.BuildShip, "P2", empty.Id));
    }

    private static Tile Tile(string id, string resource, int? number, params string[] vertices) =>
        new() { Id = id, Resource = resource, Number = number, Vertices = vertices };
    private static void Set(object value, string name, object content) => value.GetType().GetProperty(name)!.SetValue(value, content);
    private static double OpeningScore(object view, string target) => (double)typeof(SeafarersAi).Assembly
        .GetType("Catan.AI.AiHeuristics")!.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
        .Single(m => m.Name == "SetupValue" && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == view.GetType())
        .Invoke(null, [view, target])!;
    private static double Score(Tile[] tiles, string target, int path) => (double)typeof(SeafarersAi).Assembly
        .GetType("Catan.AI.SeafaringPlanning")!.GetMethod("SettlementValue", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null,
            ["through-the-desert", tiles, Array.Empty<Edge>(), new[] { Piece("家") }, "P1", target, path, 60, 2, 14])!;
    private static HashSet<string> ConnectedLand(Tile[] tiles, string start)
    {
        var found = new HashSet<string> { start };
        var queue = new Queue<Tile>(); queue.Enqueue(tiles.Single(t => t.Id == start));
        while (queue.TryDequeue(out var tile))
            foreach (var next in tiles.Where(t => t.Resource != "sea" && t.Resource != "fog" && t.Vertices.Intersect(tile.Vertices).Count() >= 2))
                if (found.Add(next.Id)) queue.Enqueue(next);
        return found;
    }
}
