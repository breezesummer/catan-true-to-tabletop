using Catan.Core;
using Catan.Core.M3;
using Xunit;
using CommandKind = Catan.Core.M3.CommandKind;
using GamePhase = Catan.Core.M3.GamePhase;
using static Catan.Rules.Tests.M3Harness;

namespace Catan.Rules.Tests;

public sealed class M3ScenarioTests
{
    [Theory]
    [InlineData("heading-for-new-shores", 2)]
    [InlineData("four-islands", 2)]
    [InlineData("through-the-desert", 2)]
    [InlineData("wonders-of-catan", 1)]
    public void FirstSettlementOnEachForeignRegionAwardsBonusOnlyOncePerPlayer(string scenarioId, int bonus)
    {
        var scenario = SeafarersScenarios.Create(scenarioId, 4); var board = New(scenarioId).GetPlayerView("P1").Board;
        var region = scenario.Regions.OrderBy(r => r.TileIds.Length).First();
        var tile = board.Tiles.First(t => region.TileIds.Contains(t.Id));
        var vertex = tile.Vertices.First(v => board.Edges.Any(e => e.Vertices.Contains(v) && SeaEdge(board, e)));
        var edge = board.Edges.First(e => e.Vertices.Contains(vertex) && SeaEdge(board, e));
        var game = Fixture(s =>
        {
            s.Ships = new[] { Ship(edge.Id) }; Player(s).Resources = Bag(3, 3, 3, 3, 3);
            Player(s).HomeRegions = scenario.Regions.Where(r => r.Id != region.Id).Select(r => r.Id).ToArray();
            // Another player's earlier visit cannot consume this player's independent region bonus.
            Player(s, "P2").SettledRegions = new[] { region.Id }; Player(s, "P2").BonusVictoryPoints = bonus;
        }, scenarioId);
        Accepted(game, Cmd(CommandKind.BuildSettlement, target: vertex));
        Assert.Equal(bonus, game.GetPlayerView("P1").Players[0].BonusVictoryPoints);
        Accepted(game, Cmd(CommandKind.BuildCity, target: vertex));
        Assert.Equal(bonus, game.GetPlayerView("P1").Players[0].BonusVictoryPoints);
        var state = game.GetAuthoritativeStateForTesting();
        // A separate legal settlement on the same region must not earn a second award.
        var second = tile.Vertices.First(v => v != vertex && !board.Edges.Any(e => e.Vertices.Contains(v) && e.Vertices.Contains(vertex)));
        var secondEdge = board.Edges.First(e => e.Vertices.Contains(second));
        state.Roads = state.Roads.Append(Piece(secondEdge.Id)).ToArray(); Player(state).Resources = Bag(1, 1, 1, 1); Balance(state); SetState(game, state);
        Accepted(game, Cmd(CommandKind.BuildSettlement, target: second));
        Assert.Equal(bonus, game.GetPlayerView("P1").Players[0].BonusVictoryPoints);
    }

    [Theory]
    [InlineData("heading-for-new-shores", 14, 2)]
    [InlineData("four-islands", 13, 2)]
    [InlineData("fog-islands", 12, 2)]
    [InlineData("through-the-desert", 14, 2)]
    [InlineData("forgotten-tribe", 13, 2)]
    [InlineData("cloth-for-catan", 14, 3)]
    [InlineData("pirate-islands", 10, 3)]
    [InlineData("wonders-of-catan", 10, 2)]
    [InlineData("new-world", 12, 2)]
    public void AllOfficialScenariosHaveExpectedGoalAndStartingPieceCount(string scenario, int goal, int buildings)
    {
        foreach (var seats in new[] { 3, 4 })
        {
            var game = New(scenario, seats); var before = game.GetPlayerView("P1");
            Assert.Equal(goal, before.TargetVictoryPoints); CompleteSetup(game);
            var view = game.GetPlayerView("P1"); Assert.Equal(GamePhase.ProductionAwaitRoll, view.Phase);
            foreach (var player in view.Players) Assert.Equal(buildings, view.Board.Settlements.Count(s => s.PlayerId == player.Id));
            Invariants(game);
        }
    }

    [Fact]
    public void ClothStartsWithEightFiveTokenVillagesAndTenInSupplyAndNoLongestRoute()
    {
        var game = New("cloth-for-catan"); var view = game.GetPlayerView("P1");
        Assert.Equal(8, view.Board.Villages.Length); Assert.All(view.Board.Villages, v => Assert.Equal(5, v.Cloth));
        Assert.Equal(10, view.ClothSupply); Assert.Null(view.LongestRoadPlayerId);
        var driver = new M3ViewOnlyDriver(); var settlements = 0;
        while (view.Phase is GamePhase.SetupSettlement or GamePhase.SetupRoad)
        {
            var cmd = driver.Decide(game.GetPlayerView(view.ActivePlayerId)); cmd.Id = "cloth-setup-" + settlements + "-" + view.Phase;
            if (cmd.Kind == CommandKind.SetupSettlement) settlements++;
            Accepted(game, cmd); view = game.GetPlayerView("P1");
            if (settlements <= 8) Assert.All(view.Players, p => Assert.Equal(0, p.ResourceCount));
        }
        Assert.Equal(12, settlements); Assert.All(view.Players, p => Assert.Equal(3, view.Board.Settlements.Count(b => b.PlayerId == p.Id)));
    }

    [Fact]
    public void ClothProductionUsesGeneralSupplyForShortageButEmptyVillageProducesNothing()
    {
        foreach (var empty in new[] { false, true })
        {
            var game = Fixture(s =>
            {
                var village = s.Villages[0]; village.Cloth = empty ? 0 : 1; village.TradingPlayerIds = new[] { "P1", "P2" };
                Player(s, "P3").Cloth = empty ? 5 : 4;
                s.LastDice1 = 1; s.LastDice2 = village.Number - 1;
            }, "cloth-for-catan");
            var state = game.GetAuthoritativeStateForTesting(); Invoke(game, "ResolveProduction", state); SetState(game, state);
            Assert.Equal(empty ? 0 : 1, game.GetPlayerView("P1").Players.Single(p => p.Id == "P1").Cloth);
            Assert.Equal(empty ? 0 : 1, game.GetPlayerView("P1").Players.Single(p => p.Id == "P2").Cloth);
            Assert.Equal(empty ? 10 : 9, game.GetPlayerView("P1").ClothSupply);
            Assert.Equal(0, game.GetPlayerView("P1").Board.Villages[0].Cloth);
        }
    }

    [Fact]
    public void ClothScoresPairsAndVillageExhaustionFinishesAtEndOfTurn()
    {
        var game = Fixture(s =>
        {
            foreach (var village in s.Villages.Take(5)) village.Cloth = 0;
            Player(s).Cloth = 12; Player(s, "P2").Cloth = 13;
        }, "cloth-for-catan");
        Assert.Equal(6, game.GetPlayerView("P1").OwnVictoryPoints);
        Assert.Equal(6, game.GetPlayerView("P2").OwnVictoryPoints);
        Accepted(game, Cmd(CommandKind.EndTurn));
        Assert.Equal(GamePhase.Finished, game.GetPlayerView("P1").Phase); Assert.Equal("P2", game.GetPlayerView("P1").WinnerPlayerId);
    }

    [Fact]
    public void ForgottenTribeGiftCardIsCollectedOnceAndCannotPlaySameTurn()
    {
        var board = New("forgotten-tribe").GetPlayerView("P1").Board;
        var reward = board.GiftCardEdgeIds.First(); var edge = board.Edges.Single(e => e.Id == reward);
        var neighbor = board.Edges.First(e => e.Id != edge.Id && SeaEdge(board, e) && e.Vertices.Intersect(edge.Vertices).Any());
        var game = Fixture(s =>
        {
            s.PirateTileId = null!; s.Ships = new[] { Ship(neighbor.Id) }; Player(s).Resources = Bag(wood: 2, wool: 2);
            // Independent fixture gives a known Knight without inspecting shuffled reward order.
            s.GiftCards = new[] { new GiftCard { EdgeId = reward, Kind = DevelopmentCardKind.Knight } };
        }, "forgotten-tribe");
        Accepted(game, Cmd(CommandKind.BuildShip, target: reward));
        var view = game.GetPlayerView("P1");
        var card = Assert.Single(view.OwnDevelopmentCards); Assert.Equal(DevelopmentCardKind.Knight, card.Kind); Assert.Equal(view.Turn, card.BoughtTurn);
        Assert.Empty(view.Board.GiftCardEdgeIds); Rejected(game, Cmd(CommandKind.PlayKnight));
        Rejected(game, Cmd(CommandKind.BuildShip, target: reward));
    }

    [Theory]
    [InlineData(3, 20)]
    [InlineData(4, 25)]
    public void PirateIslandDevelopmentAndReservedPieceSuppliesMatchEdition(int seats, int deckSize)
    {
        var game = New("pirate-islands", seats); var view = game.GetPlayerView("P1");
        Assert.Equal(deckSize, view.DevelopmentDeckCount); Assert.Equal(seats, view.Board.Fortresses.Length);
        Assert.All(view.Board.Fortresses, f => Assert.Equal(3, f.Strength)); Assert.Equal(seats, view.Board.Ships.Length);
        Assert.All(view.Players, p => { Assert.Equal(14, p.ShipsRemaining); Assert.Equal(3, p.Pieces.Settlements); });
        Assert.Null(view.Board.RobberTileId); Assert.Null(view.LongestRoadPlayerId); Assert.Null(view.LargestArmyPlayerId);
        Invariants(game);
    }

    [Fact]
    public void FogRevealsNewLandAndGivesItsResourceWithoutLeakingRemainingDrawOrder()
    {
        var board = New("fog-islands").GetPlayerView("P1").Board;
        var edge = board.Edges.First(e => SeaEdge(board, e) && board.Tiles.Count(t => t.Resource == "fog" && t.Vertices.Intersect(e.Vertices).Any()) == 1);
        var fog = board.Tiles.Single(t => t.Resource == "fog" && t.Vertices.Intersect(edge.Vertices).Any());
        var neighbor = board.Edges.First(e => e.Id != edge.Id && SeaEdge(board, e) && e.Vertices.Intersect(edge.Vertices).Any());
        var game = Fixture(s =>
        {
            s.PirateTileId = null!; s.Ships = new[] { Ship(neighbor.Id) }; Player(s).Resources = Bag(wood: 1, wool: 1);
            var resources = s.HiddenResources.ToList(); resources.Remove("ore"); s.HiddenResources = new[] { "ore" }.Concat(resources).ToArray();
        }, "fog-islands");
        Accepted(game, Cmd(CommandKind.BuildShip, target: edge.Id));
        var view = game.GetPlayerView("P1"); var revealed = view.Board.Tiles.Single(t => t.Id == fog.Id);
        Assert.Equal("ore", revealed.Resource); Assert.NotNull(revealed.Number); Assert.Equal(1, view.OwnResources.Ore);
        Assert.DoesNotContain("hiddenResources", RulesHarness.Canonical(view), StringComparison.OrdinalIgnoreCase);
        Rejected(game, Cmd(CommandKind.BuildShip, target: edge.Id));
    }
}
