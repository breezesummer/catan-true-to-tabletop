using Catan.AI;
using Catan.Core;
using Xunit;
using M = Catan.Core.M4;
using static Catan.Rules.Tests.M4CoreHarness;

namespace Catan.Rules.Tests;

public sealed class M6CitiesPendingTests
{
    [Theory]
    [InlineData("Aqueduct")][InlineData("DefenderProgress")][InlineData("PillageCity")]
    [InlineData("Metropolis")][InlineData("DisplacedKnight")][InlineData("ProgressRoads")]
    [InlineData("ProgressHarbor")][InlineData("ProgressGuildDues")][InlineData("ProgressEspionage")]
    [InlineData("ProgressWedding")][InlineData("ProgressSabotage")][InlineData("ProgressTreasonRemove")]
    [InlineData("ProgressTreasonPlace")][InlineData("ProgressDiscard")]
    public void EveryChoiceCompletesThroughSharedValidatorAndThenAdvancesNextSeat(string kind)
    {
        var board = New().GetPlayerView("P1").Board;
        var edge = board.Edges.First(e => board.Tiles.Any(t => t.Resource != "sea" && e.Vertices.All(t.Vertices.Contains)));
        var game = Fixture(s =>
        {
            s.Phase = M.GamePhase.PendingChoice;
            s.PendingDecision = new M.PendingDecision { Kind = kind, PlayerId = "P2", OtherPlayerId = "P1", Amount = 2, Track = M.ImprovementTrack.Science };
            s.DecisionQueue = [new M.PendingDecision { Kind = "Aqueduct", PlayerId = "P3" }];
            switch (kind)
            {
                case "PillageCity":
                case "Metropolis":
                    s.Cities = [Piece(edge.Vertices[0], "P2")];
                    s.PendingDecision.Options = [edge.Vertices[0]];
                    if (kind == "Metropolis") Player(s, "P2").Improvements[(int)M.ImprovementTrack.Science] = 4;
                    break;
                case "DisplacedKnight":
                    s.PendingDecision.Options = [edge.Vertices[1]];
                    s.PendingDecision.DisplacedKnight = new M.Knight { PlayerId = "P2", LocationId = edge.Vertices[0], Level = 1 };
                    s.Roads = [Piece(edge.Id, "P2")];
                    break;
                case "ProgressRoads": s.Settlements = [Piece(edge.Vertices[0], "P2")]; break;
                case "ProgressHarbor": Player(s, "P2").Commodities.Cloth = 1; break;
                case "ProgressGuildDues": Player(s).Resources.Wood = 1; Player(s).Commodities.Coin = 1; break;
                case "ProgressEspionage": Player(s).ProgressCards = [M.ProgressCardKind.Medicine]; break;
                case "ProgressWedding":
                case "ProgressSabotage": Player(s, "P2").Resources.Wood = 1; Player(s, "P2").Commodities.Coin = 1; break;
                case "ProgressTreasonRemove":
                    s.Knights = [new M.Knight { PlayerId = "P2", LocationId = edge.Vertices[1], Level = 3 }];
                    s.Roads = [Piece(edge.Id)];
                    s.PendingDecision.Options = [edge.Vertices[1]];
                    break;
                case "ProgressTreasonPlace":
                    s.Roads = [Piece(edge.Id, "P2")];
                    s.PendingDecision.Options = [edge.Vertices[1]];
                    s.PendingDecision.KnightLevel = 3;
                    break;
                case "ProgressDiscard": Player(s, "P2").ProgressCards = Enumerable.Repeat(M.ProgressCardKind.Merchant, 5).ToArray(); break;
            }
        });
        var seen = new HashSet<string>();
        int count = 0;
        while (game.GetPlayerView("P1").PendingDecision != null && count < 8)
        {
            var status = game.GetPlayerView("P1");
            string actor = status.PendingDecision.PlayerId;
            var view = game.GetPlayerView(actor);
            seen.Add(view.PendingDecision.Kind + "/" + actor);
            var projection = M6Game.Json(view);
            var before = game.Save();
            var command = new CitiesKnightsAi().Decide(view);
            Assert.Equal(M6Game.Json(command), M6Game.Json(new CitiesKnightsAi().Decide(view)));
            command.Id = "m6-pending-" + count++;
            if (command.Resources != null)
            {
                command.Resources.Wood++;
                Assert.Equal(projection, M6Game.Json(view));
                command.Resources.Wood--;
            }
            if (command.Commodities != null)
            {
                command.Commodities.Coin++;
                Assert.Equal(projection, M6Game.Json(view));
                command.Commodities.Coin--;
            }
            Assert.Equal(projection, M6Game.Json(view));
            Assert.Equal(before, game.Save());
            string owner = command.PlayerId;
            command.PlayerId = "P4";
            Assert.False(game.Execute(command).Success);
            Assert.Equal(before, game.Save());
            command.PlayerId = owner;
            Accepted(game, command);
        }
        Assert.Null(game.GetPlayerView("P1").PendingDecision);
        Assert.Equal(M.GamePhase.Action, game.GetPlayerView("P1").Phase);
        Assert.Contains(kind + "/P2", seen);
        Assert.Contains("Aqueduct/P3", seen);
        Assert.Equal(1, game.GetPlayerView("P3").OwnResources.Total);
        switch (kind)
        {
            case "PillageCity": Assert.Empty(game.GetPlayerView("P2").Board.Cities); break;
            case "Metropolis": Assert.Single(game.GetPlayerView("P2").Board.Metropolises); break;
            case "ProgressDiscard": Assert.Equal(4, game.GetPlayerView("P2").OwnProgressCards.Length); break;
            case "ProgressHarbor": Assert.Equal(1, game.GetPlayerView("P1").OwnCommodities.Cloth); break;
            case "ProgressWedding": Assert.Equal(2, game.GetPlayerView("P1").OwnResources.Total + game.GetPlayerView("P1").OwnCommodities.Total); break;
            case "ProgressSabotage": Assert.Equal(0, game.GetPlayerView("P2").OwnResources.Total + game.GetPlayerView("P2").OwnCommodities.Total); break;
            case "ProgressGuildDues": Assert.Equal(2, game.GetPlayerView("P2").OwnResources.Total + game.GetPlayerView("P2").OwnCommodities.Total); break;
            case "ProgressEspionage": Assert.Contains(M.ProgressCardKind.Medicine, game.GetPlayerView("P2").OwnProgressCards); break;
            case "ProgressTreasonRemove": Assert.Contains("ProgressTreasonPlace/P1", seen); break;
        }
        Invariants(game);
    }
}
