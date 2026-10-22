using Catan.AI;
using Catan.Core;
using Xunit;
using M = Catan.Core.M5;
using static Catan.Rules.Tests.M5Harness;

namespace Catan.Rules.Tests;

/// <summary>固定公开局面的策略预期；关键选择均交给真实规则入口，并检查守恒及非法席位拒绝。</summary>
public sealed class M6CombinedStrategyTests
{
    [Fact]
    public void WinningCityComesBeforeActivatingAnUnprotectedCityKnight()
    {
        var sites = SeparateSites(3);
        var game = Fixture(s =>
        {
            s.Cities = [Piece(sites[0])]; s.Settlements = [Piece(sites[1])];
            s.Knights = [Knight(sites[2], active: false)];
            Player(s).DefenderPoints = 12; Player(s).Resources = Bag(wheat: 2, ore: 3);
        });
        Assert.Equal(15, game.GetPlayerView("P1").OwnVictoryPoints);
        Assert.Contains(game.GetPlayerView("P1").LegalActions, c => c.Kind == M.CommandKind.ActivateKnight);
        var command = DecideAndExecute(game);
        Assert.Equal(M.CommandKind.BuildCity, command.Kind);
        Assert.Equal(M.GamePhase.Finished, game.GetPlayerView("P1").Phase);
        Assert.Equal("P1", game.GetPlayerView("P1").WinnerPlayerId);
    }

    [Fact]
    public void WinningMetropolisComesBeforeDefenseAndCompletesItsMandatoryChoice()
    {
        var sites = SeparateSites(2);
        var game = Fixture(s =>
        {
            s.Cities = [Piece(sites[0])]; s.Knights = [Knight(sites[1], active: false)];
            Player(s).DefenderPoints = 12; Player(s).Resources.Wheat = 1;
            Player(s).Improvements[2] = 3; Player(s).Commodities.Paper = 4;
        });
        var command = DecideAndExecute(game);
        Assert.Equal(M.CommandKind.ImproveCity, command.Kind); Assert.Equal(M.ImprovementTrack.Science, command.Track);
        Assert.Equal("Metropolis", game.GetPlayerView("P1").PendingDecision.Kind);
        DecideAndExecute(game);
        Assert.Equal(16, game.GetPlayerView("P1").OwnVictoryPoints);
        Assert.Equal(M.GamePhase.Finished, game.GetPlayerView("P1").Phase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void FirstIslandSettlementBeatsAnAffordableCityUpgrade(int historicalDefensePoints)
    {
        var board = New().GetPlayerView("P1").Board;
        var scenario = M.CombinedScenarios.Create("heading-for-new-shores", 4);
        var island = scenario.Regions.First(r => !r.TileIds.Intersect(scenario.StartingTileIds).Any());
        var landing = board.Vertices.First(v => board.Tiles.Any(t => island.TileIds.Contains(t.Id) && t.Vertices.Contains(v.Id)) &&
            board.Edges.Any(e => e.Vertices.Contains(v.Id) && board.Tiles.Any(t => t.Resource == "sea" && e.Vertices.All(t.Vertices.Contains)))).Id;
        var shipEdge = board.Edges.First(e => e.Vertices.Contains(landing) && board.Tiles.Any(t => t.Resource == "sea" && e.Vertices.All(t.Vertices.Contains)));
        var home = SeparateSites(3);
        var game = Fixture(s =>
        {
            s.Cities = [Piece(home[0])]; s.Settlements = [Piece(home[1])]; s.Knights = [Knight(home[2])];
            s.Ships = [new M.ShipPlacement { PlayerId = "P1", LocationId = shipEdge.Id }];
            Player(s).Resources = Bag(wood: 1, brick: 1, wool: 1, wheat: 3, ore: 3);
            Player(s).DefenderPoints = historicalDefensePoints;
            var main = scenario.Regions.Single(r => r.TileIds.Intersect(scenario.StartingTileIds).Any()).Id;
            Player(s).HomeRegions = [main]; Player(s).SettledRegions = [main];
        });
        Assert.NotEmpty(game.GetPlayerView("P1").LegalCityVertexIds);
        int before = game.GetPlayerView("P1").OwnVictoryPoints;
        var command = DecideAndExecute(game);
        Assert.Equal(M.CommandKind.BuildSettlement, command.Kind);
        Assert.Contains(board.Tiles, t => island.TileIds.Contains(t.Id) && t.Vertices.Contains(command.TargetId));
        Assert.Equal(before + 3, game.GetPlayerView("P1").OwnVictoryPoints);
        Assert.Equal(2, game.GetPlayerView("P1").Players.Single(p => p.Id == "P1").BonusVictoryPoints);
        if (historicalDefensePoints == 10) Assert.Equal(M.GamePhase.Finished, game.GetPlayerView("P1").Phase);
    }

    [Fact]
    public void OneRoadCanClaimTheWinningLongestRouteBeforeKnightActivation()
    {
        var chain = LandChain(5);
        var sites = SeparateSites(1, chain.vertices);
        var game = Fixture(s =>
        {
            s.Cities = [Piece(chain.vertices[0])]; s.Roads = chain.edges.Take(4).Select(e => Piece(e.Id)).ToArray();
            s.Knights = [Knight(sites[0], active: false)]; Player(s).DefenderPoints = 12;
            Player(s).Resources = Bag(wood: 1, brick: 1, wheat: 1);
        });
        Assert.Equal(4, game.GetPlayerView("P1").Players.Single(p => p.Id == "P1").LongestRoadLength);
        var command = DecideAndExecute(game);
        Assert.Equal(M.CommandKind.BuildRoad, command.Kind);
        Assert.Equal("P1", game.GetPlayerView("P1").LongestRoadPlayerId);
        Assert.Equal(M.GamePhase.Finished, game.GetPlayerView("P1").Phase);
    }

    [Fact]
    public void SafeCityDoesNotPromoteASecondStrengthPointBeforeTakingAScore()
    {
        var sites = SeparateSites(6);
        var game = Fixture(s =>
        {
            s.Cities = [Piece(sites[0]), Piece(sites[2], "P2"), Piece(sites[3], "P3"), Piece(sites[4], "P4")];
            s.Settlements = [Piece(sites[1])]; s.Knights = [Knight(sites[5])];
            Player(s).Resources = Bag(wool: 1, wheat: 2, ore: 3);
        });
        Assert.Contains(game.GetPlayerView("P1").LegalActions, c => c.Kind == M.CommandKind.PromoteKnight);
        var command = DecideAndExecute(game);
        Assert.Equal(M.CommandKind.BuildCity, command.Kind);
        Assert.Equal(1, game.GetPlayerView("P1").Board.Knights.Single().Level);
    }

    [Fact]
    public void EnoughInactiveStrengthSavesForWheatInsteadOfBuyingAnotherKnightLevel()
    {
        var sites = SeparateSites(2);
        var game = Fixture(s =>
        {
            s.Cities = [Piece(sites[0])]; s.Knights = [Knight(sites[1], active: false)];
            Player(s).Resources = Bag(wood: 4, wool: 2, ore: 2);
        });
        Assert.Contains(game.GetPlayerView("P1").LegalActions, c => c.Kind == M.CommandKind.PromoteKnight);
        var command = DecideAndExecute(game);
        Assert.Equal(M.CommandKind.BankTrade, command.Kind); Assert.Equal(Resource.Wheat, command.ReceiveResource);
        Assert.False(command.ReceiveIsCommodity);
        Assert.Equal(1, game.GetPlayerView("P1").OwnResources.Wheat);
        Assert.Equal(M.CommandKind.ActivateKnight, DecideAndExecute(game).Kind);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(6, true)]
    public void NoWheatPromotesAnActiveKnightOnlyWhenTheAttackIsClose(int barbarianPosition, bool promote)
    {
        var sites = SeparateSites(4);
        var game = Fixture(s =>
        {
            s.Cities = [Piece(sites[0]), Piece(sites[1])];
            s.Knights = [Knight(sites[2]), Knight(sites[3], active: false)];
            s.BarbarianPosition = barbarianPosition; Player(s).Resources = Bag(wool: 1, ore: 1);
        });
        var command = DecideAndExecute(game);
        Assert.Equal(promote, command.Kind == M.CommandKind.PromoteKnight);
        if (promote) Assert.Equal(2, game.GetPlayerView("P1").Board.Knights.Where(k => k.Active).Sum(k => k.Level));
        else Assert.Equal(2, game.GetPlayerView("P1").Board.Knights.Sum(k => k.Level));
    }

    [Fact]
    public void NoCityDoesNotSpendWheatActivatingAnIdleKnight()
    {
        var sites = SeparateSites(2);
        var game = Fixture(s =>
        {
            s.Settlements = [Piece(sites[0])]; s.Knights = [Knight(sites[1], active: false)]; Player(s).Resources.Wheat = 1;
        });
        var command = DecideAndExecute(game);
        Assert.NotEqual(M.CommandKind.ActivateKnight, command.Kind);
        Assert.Equal(1, game.GetPlayerView("P1").OwnResources.Wheat);
    }

    [Fact]
    public void EarlyAqueductTakesPrecedenceOverAnOrdinaryCityUpgrade()
    {
        var sites = SeparateSites(3);
        var game = Fixture(s =>
        {
            s.Cities = [Piece(sites[0])]; s.Settlements = [Piece(sites[1])]; s.Knights = [Knight(sites[2])];
            Player(s).Resources = Bag(wheat: 2, ore: 3); Player(s).Improvements[2] = 2; Player(s).Commodities.Paper = 3;
        });
        Assert.NotEmpty(game.GetPlayerView("P1").LegalCityVertexIds);
        var command = DecideAndExecute(game);
        Assert.Equal(M.CommandKind.ImproveCity, command.Kind); Assert.Equal(M.ImprovementTrack.Science, command.Track);
        Assert.Equal(3, game.GetPlayerView("P1").OwnImprovements[2]);
    }

    [Fact]
    public void LateTradeBenefitBeatsAnAqueductWithLittleTimeToRepay()
    {
        var sites = SeparateSites(2);
        var game = Fixture(s =>
        {
            s.Turn = 95; s.Cities = [Piece(sites[0])]; s.Knights = [Knight(sites[1])];
            Player(s).Improvements = [2, 0, 2]; Player(s).Commodities.Paper = 3; Player(s).Commodities.Cloth = 3;
        });
        var command = DecideAndExecute(game);
        Assert.Equal(M.CommandKind.ImproveCity, command.Kind); Assert.Equal(M.ImprovementTrack.Trade, command.Track);
        Assert.Equal(3, game.GetPlayerView("P1").OwnImprovements[0]);
    }

    [Fact]
    public void OpponentsLockedMetropolisDoesNotWasteFourPaperOnAnEmptyLevel()
    {
        var sites = SeparateSites(3);
        var game = Fixture(s =>
        {
            s.Cities = [Piece(sites[0]), Piece(sites[1], "P2")]; s.Knights = [Knight(sites[2], level: 2)];
            Player(s).Improvements[2] = 3; Player(s).Commodities.Paper = 4; Player(s, "P2").Improvements[2] = 5;
            Player(s).ProgressCards = [M.ProgressCardKind.Crane];
            s.Metropolises = [new M.Metropolis { PlayerId = "P2", LocationId = sites[1], Track = M.ImprovementTrack.Science }];
        });
        Assert.Contains(game.GetPlayerView("P1").LegalActions, c => c.Kind == M.CommandKind.ImproveCity && c.Track == M.ImprovementTrack.Science);
        var command = DecideAndExecute(game);
        Assert.False(command.Kind == M.CommandKind.ImproveCity && command.Track == M.ImprovementTrack.Science);
        Assert.False(command.Kind == M.CommandKind.PlayProgressCard && command.ProgressCard == M.ProgressCardKind.Crane && command.Track == M.ImprovementTrack.Science);
        Assert.Equal(3, game.GetPlayerView("P1").OwnImprovements[2]);
    }

    [Fact]
    public void FreeTheftTargetsTheLeaderInsteadOfTheFirstSeatOrLargestHand()
    {
        var tile = New().GetPlayerView("P1").Board.Tiles.First(t => t.Number == 6);
        var game = Fixture(s =>
        {
            s.Settlements = [Piece(tile.Vertices[0], "P2"), Piece(tile.Vertices[3], "P3")];
            Player(s, "P2").Resources.Wood = 5; Player(s, "P3").Resources.Brick = 1; Player(s, "P3").DefenderPoints = 13;
            s.FirstBarbarianAttack = true; s.RobberTileId = tile.Id; s.PirateTileId = "frame";
            s.Phase = M.GamePhase.RobberSteal;
            s.PendingDecision = new M.PendingDecision { Kind = "Steal", PlayerId = "P1", EligibleVictimIds = ["P2", "P3"], ReturnPhase = M.GamePhase.Action };
        });
        var command = DecideAndExecute(game);
        Assert.Equal(M.CommandKind.StealResource, command.Kind); Assert.Equal("P3", command.OtherPlayerId);
        Assert.Equal(1, game.GetPlayerView("P1").OwnResources.Brick);
        Assert.Equal(5, game.GetPlayerView("P2").OwnResources.Wood);
    }

    private static M.Command DecideAndExecute(M.CombinedGameSession game)
    {
        var view = game.GetPlayerView("P1");
        var before = M6Game.Json(view);
        var command = new CombinedAi().Decide(view);
        Assert.Equal(M6Game.Json(command), M6Game.Json(new CombinedAi().Decide(view)));
        Assert.Equal(before, M6Game.Json(view));
        command.Id = Guid.NewGuid().ToString("N");
        command.PlayerId = "P2"; Rejected(game, command); command.PlayerId = "P1";
        Accepted(game, command);
        return command;
    }

    private static M.Knight Knight(string vertex, bool active = true, int level = 1) => new()
    { LocationId = vertex, PlayerId = "P1", Active = active, Level = level };

    private static string[] SeparateSites(int count, string[]? forbidden = null)
    {
        var board = New().GetPlayerView("P1").Board;
        var starting = M.CombinedScenarios.Create("heading-for-new-shores", 4).StartingTileIds;
        var chosen = new List<string>();
        foreach (var vertex in board.Vertices.Where(v => board.Tiles.Any(t => starting.Contains(t.Id) && t.Vertices.Contains(v.Id))))
        {
            if ((forbidden ?? []).Contains(vertex.Id) || chosen.Any(v => board.Edges.Any(e => e.Vertices.Contains(v) && e.Vertices.Contains(vertex.Id)))) continue;
            chosen.Add(vertex.Id); if (chosen.Count == count) return chosen.ToArray();
        }
        throw new InvalidOperationException("没有足够间隔的测试落点。");
    }

    private static (Edge[] edges, string[] vertices) LandChain(int length)
    {
        var board = New().GetPlayerView("P1").Board;
        var edges = board.Edges.Where(e => board.Tiles.Any(t => t.Resource != "sea" && e.Vertices.All(t.Vertices.Contains))).ToArray();
        var result = new List<Edge>(); var vertices = new List<string>();
        bool Find(string vertex)
        {
            if (result.Count == length) return true;
            foreach (var edge in edges.Where(e => e.Vertices.Contains(vertex)))
            {
                var next = edge.Vertices.Single(v => v != vertex); if (vertices.Contains(next)) continue;
                result.Add(edge); vertices.Add(next); if (Find(next)) return true;
                vertices.RemoveAt(vertices.Count - 1); result.RemoveAt(result.Count - 1);
            }
            return false;
        }
        vertices.Add(edges[0].Vertices[0]);
        Assert.True(Find(vertices[0])); return (result.ToArray(), vertices.ToArray());
    }
}
