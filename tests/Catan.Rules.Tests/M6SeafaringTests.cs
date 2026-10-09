using Catan.AI;
using Catan.Core;
using Xunit;
using M3 = Catan.Core.M3;
using static Catan.Rules.Tests.M3Harness;

namespace Catan.Rules.Tests;

public sealed class M6SeafaringTests
{
    [Fact]
    public void ExhaustedRoadSupplyDoesNotPlayRoadBuildingWhenNoShipCanConnect()
    {
        var board = New().GetPlayerView("P1").Board;
        string inland = board.Vertices.First(v => board.Tiles.Count(t => t.Vertices.Contains(v.Id)) == 3
            && board.Tiles.Where(t => t.Vertices.Contains(v.Id)).All(t => t.Resource != "sea")).Id;
        var roads = board.Edges.Where(e => board.Tiles.Any(t => t.Resource != "sea" && e.Vertices.All(t.Vertices.Contains))).Take(15).ToArray();
        var game = Fixture(s =>
        {
            s.Settlements = [Piece(inland)];
            s.Roads = roads.Select(e => Piece(e.Id)).ToArray();
            Card(s, M3.DevelopmentCardKind.RoadBuilding);
        });
        var view = game.GetPlayerView("P1");
        Assert.Equal(0, view.Players.Single(p => p.Id == "P1").Pieces.Roads);
        Assert.Equal(15, view.Players.Single(p => p.Id == "P1").ShipsRemaining);
        Assert.False(game.Preview(Cmd(M3.CommandKind.PlayRoadBuilding)).Success);
        var command = new SeafarersAi().Decide(view);
        Assert.NotEqual(M3.CommandKind.PlayRoadBuilding, command.Kind);
        command.Id = "m6-depleted-road-supply";
        Accepted(game, command);
    }

    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(2)]
    public void GoldChoiceHandlesEmptyOrInsufficientBankAndAdvancesToNextSeat(int available)
    {
        var game = Fixture(s =>
        {
            foreach (Resource resource in Enum.GetValues<Resource>()) Player(s, "P4").Resources[resource] = 19;
            Player(s, "P4").Resources.Ore -= available;
            s.Phase = M3.GamePhase.GoldChoice;
            s.PendingDecision = new M3.PendingDecision { Kind = "Gold", PlayerId = "P2", ReturnPhase = M3.GamePhase.Action };
            s.GoldReturnPhase = M3.GamePhase.Action;
            s.GoldClaims = [new M3.GoldClaim { PlayerId = "P2", Amount = 2 }, new M3.GoldClaim { PlayerId = "P3", Amount = 1 }];
        });
        for (int step = 0; step < 2; step++)
        {
            var view = game.GetPlayerView(step == 0 ? "P2" : "P3");
            string before = M6Game.Json(view);
            var command = new SeafarersAi().Decide(view);
            Assert.Equal(step == 0 ? available : 0, command.Resources.Total);
            command.Id = "m6-gold-bank-" + step;
            Assert.Equal(before, M6Game.Json(view));
            Accepted(game, command);
        }
        Assert.Equal(M3.GamePhase.Action, game.GetPlayerView("P1").Phase);
        Assert.Empty(game.GetPlayerView("P1").GoldClaims);
        Assert.Equal(available, game.GetPlayerView("P2").OwnResources.Total);
        Invariants(game);
    }

    [Fact]
    public void TakeoverAfterHumanRerootDoesNotUpgradeAnUnrelatedCoastalShip()
    {
        var scenario = M3.SeafarersScenarios.Create("pirate-islands", 4);
        var board = New("pirate-islands").GetPlayerView("P1").Board;
        var ownShipVertices = board.Ships.Where(s => s.PlayerId == "P1").SelectMany(s => board.Edges.Single(e => e.Id == s.LocationId).Vertices).ToHashSet();
        var root = board.Vertices.First(v => !board.Settlements.Any(b => b.LocationId == v.Id) && !ownShipVertices.Contains(v.Id)
            && board.Tiles.Any(t => scenario.StartingTileIds.Contains(t.Id) && t.Vertices.Contains(v.Id))
            && board.Edges.Any(e => e.Vertices.Contains(v.Id) && SeaEdge(board, e))).Id;
        var game = Fixture(s =>
        {
            s.Settlements = s.Settlements.Append(Piece(root)).ToArray();
            Card(s, M3.DevelopmentCardKind.Knight);
        }, "pirate-islands");
        Accepted(game, Cmd(M3.CommandKind.ChooseInvasionRoute, target: root));
        var state = game.GetAuthoritativeStateForTesting();
        state.Phase = M3.GamePhase.ProductionAwaitRoll;
        SetState(game, state);
        var view = game.GetPlayerView("P1");
        Assert.Contains(view.Board.Ships, ship => ship.PlayerId == "P1" && !ship.IsInvasionRoute && !ship.IsWarship);
        Assert.DoesNotContain(view.Board.Ships, ship => ship.PlayerId == "P1" && ship.IsInvasionRoute);
        var command = new SeafarersAi().Decide(view);
        Assert.Equal(M3.CommandKind.RollDice, command.Kind);
        command.Id = "m6-human-reroot-takeover";
        Accepted(game, command);
        Assert.Contains(game.GetPlayerView("P1").OwnDevelopmentCards, c => c.Kind == M3.DevelopmentCardKind.Knight);
    }
}
