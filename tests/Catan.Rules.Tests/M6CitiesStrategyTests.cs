using Catan.AI;
using Catan.Core;
using Xunit;
using M = Catan.Core.M4;
using static Catan.Rules.Tests.M4CoreHarness;

namespace Catan.Rules.Tests;

public sealed class M6CitiesStrategyTests
{
    private static string[] Sites(int count = 8)
    {
        var board = New().GetPlayerView("P1").Board;
        var result = new List<string>();
        foreach (var v in board.Vertices)
            if (!result.Any(id => board.Edges.Any(e => e.Vertices.Contains(id) && e.Vertices.Contains(v.Id))))
            {
                result.Add(v.Id);
                if (result.Count == count) return result.ToArray();
            }
        throw new InvalidOperationException("测试地图缺少足够间隔的落点。");
    }

    private static M.Knight Knight(string vertex, string player = "P1", int level = 1, bool active = false) =>
        new() { LocationId = vertex, PlayerId = player, Level = level, Active = active };

    private static M.Command DecideAndExecute(M.CitiesKnightsGameSession game)
    {
        var view = game.GetPlayerView("P1");
        string state = game.Save(), projected = M6Game.Json(view);
        var command = new CitiesKnightsAi().Decide(view);
        Assert.Equal(projected, M6Game.Json(view));
        Assert.Equal(state, game.Save());
        command.Id = "城市策略-" + Guid.NewGuid();
        var player = command.PlayerId;
        command.PlayerId = "P4";
        Rejected(game, command);
        command.PlayerId = player;
        Accepted(game, command);
        return command;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WinningCityOrMedicinePrecedesImminentDefense(bool medicine)
    {
        var sites = Sites();
        var game = Fixture(s =>
        {
            s.BarbarianPosition = 6;
            s.Cities = [Piece(sites[0]), Piece(sites[1], "P2")];
            s.Settlements = [Piece(sites[2])];
            s.Knights = [Knight(sites[3])];
            Player(s).DefenderPoints = 9;
            Player(s).Resources = medicine ? Bag(wool: 1, wheat: 1, ore: 2) : Bag(wool: 1, wheat: 2, ore: 3);
            if (medicine) Player(s).ProgressCards = [M.ProgressCardKind.Medicine];
        });
        Assert.Equal(12, game.GetPlayerView("P1").OwnVictoryPoints);
        var command = DecideAndExecute(game);
        Assert.Equal(medicine ? M.CommandKind.PlayProgressCard : M.CommandKind.BuildCity, command.Kind);
        Assert.Equal("P1", game.GetPlayerView("P1").WinnerPlayerId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WinningMetropolisAndCraneCompleteBothLegalStepsBeforeDefense(bool crane)
    {
        var sites = Sites();
        var game = Fixture(s =>
        {
            s.BarbarianPosition = 6;
            s.Cities = [Piece(sites[0]), Piece(sites[1], "P2")];
            s.Knights = [Knight(sites[2])];
            Player(s).DefenderPoints = 9;
            Player(s).Resources = Bag(wheat: 1, wool: 1, ore: 1);
            Player(s).Improvements[(int)M.ImprovementTrack.Science] = 3;
            Player(s).Commodities.Paper = crane ? 3 : 4;
            if (crane) Player(s).ProgressCards = [M.ProgressCardKind.Crane];
        });
        var command = DecideAndExecute(game);
        Assert.Equal(crane ? M.CommandKind.PlayProgressCard : M.CommandKind.ImproveCity, command.Kind);
        Assert.Equal(M.ImprovementTrack.Science, command.Track);
        Assert.Equal("Metropolis", game.GetPlayerView("P1").PendingDecision.Kind);
        DecideAndExecute(game);
        Assert.Equal("P1", game.GetPlayerView("P1").WinnerPlayerId);
    }

    [Fact]
    public void MerchantCanWinBeforeSpendingWheatOnDefense()
    {
        var sites = Sites();
        var game = Fixture(s =>
        {
            s.Cities = [Piece(sites[0]), Piece(sites[1], "P2")];
            s.Knights = [Knight(sites[2])];
            Player(s).DefenderPoints = 10;
            Player(s).Resources.Wheat = 1;
            Player(s).ProgressCards = [M.ProgressCardKind.Merchant];
        });
        Assert.Equal(M.ProgressCardKind.Merchant, DecideAndExecute(game).ProgressCard);
        Assert.Equal("P1", game.GetPlayerView("P1").WinnerPlayerId);
    }

    [Fact]
    public void MinimumDefenseActivatesOnlyOneSufficientKnight()
    {
        var sites = Sites();
        var game = Fixture(s =>
        {
            s.BarbarianPosition = 6;
            s.Cities = sites.Take(4).Select((v, i) => Piece(v, "P" + (i + 1))).ToArray();
            s.Knights = [Knight(sites[4]), Knight(sites[5], level: 2)];
            Player(s).Resources.Wheat = 2;
        });
        var command = DecideAndExecute(game);
        Assert.Equal(M.CommandKind.ActivateKnight, command.Kind);
        Assert.Equal(sites[4], command.TargetId);
        Assert.Equal(M.CommandKind.EndTurn, DecideAndExecute(game).Kind);
        Assert.Single(game.GetPlayerView("P1").Board.Knights, k => k.Active);
        Assert.Equal(1, game.GetPlayerView("P1").OwnResources.Wheat);
    }

    [Fact]
    public void CollectiveDefenseDoesNotActivateRedundantKnightBeforeScoring()
    {
        var sites = Sites();
        string desert = New().GetPlayerView("P1").Board.Tiles.Single(t => t.Resource == "desert").Id;
        var game = Fixture(s =>
        {
            s.FirstBarbarianAttack = true; s.RobberTileId = desert;
            s.Cities = [Piece(sites[0]), Piece(sites[1], "P2")];
            s.Settlements = [Piece(sites[2])];
            s.Knights = [Knight(sites[3]), Knight(sites[4], "P2", level: 2, active: true)];
            Player(s).Resources = Bag(wheat: 2, ore: 3);
        });
        Assert.Equal(M.CommandKind.BuildCity, DecideAndExecute(game).Kind);
        Assert.False(game.GetPlayerView("P1").Board.Knights.Single(k => k.PlayerId == "P1").Active);
    }

    [Fact]
    public void EarlyAqueductBeatsAnotherCityWhenDefenseIsAlreadySafe()
    {
        var sites = Sites();
        var game = Fixture(s =>
        {
            s.Cities = [Piece(sites[0])]; s.Settlements = [Piece(sites[1])];
            s.Knights = [Knight(sites[2], active: true)];
            Player(s).Resources = Bag(wheat: 2, ore: 3);
            Player(s).Improvements[(int)M.ImprovementTrack.Science] = 2;
            Player(s).Commodities.Paper = 3;
        });
        var command = DecideAndExecute(game);
        Assert.Equal(M.CommandKind.ImproveCity, command.Kind);
        Assert.Equal(M.ImprovementTrack.Science, command.Track);
        Assert.Equal(3, game.GetPlayerView("P1").OwnImprovements[(int)M.ImprovementTrack.Science]);
    }

    [Fact]
    public void LateTradeImprovementBeatsBeginningScience()
    {
        var sites = Sites();
        var game = Fixture(s =>
        {
            s.Turn = 96; s.Cities = [Piece(sites[0])];
            s.Knights = [Knight(sites[1], active: true)];
            Player(s).Improvements[(int)M.ImprovementTrack.Science] = 1;
            Player(s).Improvements[(int)M.ImprovementTrack.Trade] = 1;
            Player(s).Commodities.Paper = 2; Player(s).Commodities.Cloth = 2;
        });
        var command = DecideAndExecute(game);
        Assert.Equal(M.CommandKind.ImproveCity, command.Kind);
        Assert.Equal(M.ImprovementTrack.Trade, command.Track);
    }

    [Fact]
    public void PermanentlyClaimedMetropolisDoesNotAttractUselessFourthLevel()
    {
        var sites = Sites();
        var game = Fixture(s =>
        {
            s.Cities = [Piece(sites[0]), Piece(sites[1], "P2")];
            s.Knights = [Knight(sites[2], level: 2, active: true)];
            Player(s).Improvements[(int)M.ImprovementTrack.Science] = 3;
            Player(s, "P2").Improvements[(int)M.ImprovementTrack.Science] = 5;
            s.Metropolises = [new M.Metropolis { LocationId = sites[1], PlayerId = "P2", Track = M.ImprovementTrack.Science }];
            Player(s).Commodities.Paper = 4; Player(s).Commodities.Cloth = 1;
        });
        var command = DecideAndExecute(game);
        Assert.Equal(M.CommandKind.ImproveCity, command.Kind);
        Assert.Equal(M.ImprovementTrack.Trade, command.Track);
        Assert.Equal(4, game.GetPlayerView("P1").OwnCommodities.Paper);
    }

    [Fact]
    public void AqueductSuppliesMissingActivationWheatInsteadOfArbitraryEmptyResource()
    {
        var sites = Sites();
        var game = Fixture(s =>
        {
            s.Phase = M.GamePhase.PendingChoice;
            s.PendingDecision = new M.PendingDecision { Kind = "Aqueduct", PlayerId = "P1" };
            s.Cities = [Piece(sites[0]), Piece(sites[1], "P2")];
            s.Knights = [Knight(sites[2])];
        });
        var command = DecideAndExecute(game);
        Assert.Equal(Resource.Wheat, command.Resource);
        Assert.Equal(M.CommandKind.ActivateKnight, DecideAndExecute(game).Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CuttingRivalRoadWithSettlementOrKnightWinsImmediately(bool knight)
    {
        var board = New().GetPlayerView("P1").Board;
        (string[] edges, string[] vertices)? Find(string start, int count, HashSet<string> excluded)
        {
            var edges = new List<string>(); var vertices = new List<string> { start };
            bool Walk(string vertex)
            {
                if (edges.Count == count) return true;
                foreach (var edge in board.Edges.Where(e => e.Vertices.Contains(vertex)))
                {
                    string next = edge.Vertices.Single(v => v != vertex);
                    if (vertices.Contains(next) || excluded.Contains(next)) continue;
                    edges.Add(edge.Id); vertices.Add(next);
                    if (Walk(next)) return true;
                    edges.RemoveAt(edges.Count - 1); vertices.RemoveAt(vertices.Count - 1);
                }
                return false;
            }
            return Walk(start) ? (edges.ToArray(), vertices.ToArray()) : null;
        }
        (string[] edges, string[] vertices)? rival = null, own = null;
        foreach (var vertex in board.Vertices)
        {
            rival = Find(vertex.Id, 6, []);
            if (!rival.HasValue) continue;
            foreach (var crossing in rival.Value.vertices.Skip(2).Take(3))
            {
                own = Find(crossing, 5, rival.Value.vertices.Where(v => v != crossing).ToHashSet());
                if (own.HasValue) break;
            }
            if (own.HasValue) break;
        }
        Assert.True(rival.HasValue && own.HasValue);
        string crossingVertex = own!.Value.vertices[0];
        var game = Fixture(s =>
        {
            s.Roads = rival!.Value.edges.Select(e => Piece(e, "P2")).Concat(own.Value.edges.Select(e => Piece(e))).ToArray();
            Player(s).DefenderPoints = knight ? 11 : 10;
            Player(s).Resources = knight ? Bag(wool: 1, ore: 1) : Bag(1, 1, 1, 1);
        });
        Assert.Equal("P2", game.GetPlayerView("P1").LongestRoadPlayerId);
        var command = DecideAndExecute(game);
        Assert.Equal(knight ? M.CommandKind.RecruitKnight : M.CommandKind.BuildSettlement, command.Kind);
        Assert.Equal(crossingVertex, command.TargetId);
        Assert.Equal("P1", game.GetPlayerView("P1").WinnerPlayerId);
        Assert.Equal("P1", game.GetPlayerView("P1").LongestRoadPlayerId);
    }
}
