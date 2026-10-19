using Catan.AI;
using Catan.Core;
using Xunit;
using M4 = Catan.Core.M4;
using M5 = Catan.Core.M5;
using H4 = Catan.Rules.Tests.M4CoreHarness;
using H5 = Catan.Rules.Tests.M5Harness;

namespace Catan.Rules.Tests;

/// <summary>AI 2.0：城骑 PDF 第 9–10、12、15–16 页；航海家 PDF 第 5 页。仅比较合法候选，执行后验证守恒与无权席位回滚。</summary>
public sealed class AiTacticalPatchTests
{
    [Fact]
    public void BishopRelievesOwnHexAndStillStealsFromAProductiveRivalHexInCities()
    {
        var board = H4.New().GetPlayerView("P1").Board;
        var own = board.Tiles.First(t => t.Number == 6);
        var enemy = board.Tiles.First(t => t.Number == 8 && !t.Vertices.Intersect(own.Vertices).Any());
        var game = H4.Fixture(s =>
        {
            s.FirstBarbarianAttack = true; s.RobberTileId = own.Id;
            s.Settlements = [H4.Piece(own.Vertices[0]), H4.Piece(enemy.Vertices[0], "P2")];
            H4.Player(s).ProgressCards = [M4.ProgressCardKind.Taxation];
            H4.Player(s, "P2").Resources.Wood = 1;
        });
        var command = Execute(game);
        Assert.Equal(M4.CommandKind.PlayProgressCard, command.Kind);
        Assert.Equal(M4.ProgressCardKind.Taxation, command.ProgressCard);
        Assert.Contains(board.Tiles, t => t.Id == command.TargetId && t.Number.HasValue && t.Vertices.Contains(enemy.Vertices[0]));
        Assert.Equal(1, game.GetPlayerView("P1").OwnResources.Wood);
        Assert.NotEqual(own.Id, game.GetPlayerView("P1").Board.RobberTileId);
    }

    [Fact]
    public void BishopRelievesOwnHexAndStillStealsFromAProductiveRivalHexInCombined()
    {
        var board = H5.New().GetPlayerView("P1").Board;
        var own = board.Tiles.First(t => t.Number == 6);
        var enemy = board.Tiles.First(t => t.Number == 8 && !t.Vertices.Intersect(own.Vertices).Any());
        var game = H5.Fixture(s =>
        {
            s.FirstBarbarianAttack = true; s.RobberTileId = own.Id; s.PirateTileId = "frame";
            s.Settlements = [H5.Piece(own.Vertices[0]), H5.Piece(enemy.Vertices[0], "P2")];
            H5.Player(s).ProgressCards = [M5.ProgressCardKind.Taxation];
            H5.Player(s, "P2").Resources.Wood = 1;
        });
        var command = Execute(game);
        Assert.Equal(M5.CommandKind.PlayProgressCard, command.Kind);
        Assert.Equal(M5.ProgressCardKind.Taxation, command.ProgressCard);
        Assert.Contains(board.Tiles, t => t.Id == command.TargetId && t.Number.HasValue && t.Vertices.Contains(enemy.Vertices[0]));
        Assert.Equal(1, game.GetPlayerView("P1").OwnResources.Wood);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KnightRecruitmentOrMovementRemovesRivalsLongestRoad(bool move)
    {
        var board = H4.New().GetPlayerView("P1").Board;
        var crossing = Crossing(board.Edges);
        var game = H4.Fixture(s =>
        {
            s.Roads = crossing.chain.Select(e => H4.Piece(e, "P2")).Append(H4.Piece(crossing.spur.Id)).ToArray();
            H4.Player(s, "P2").DefenderPoints = 9;
            if (move) s.Knights = [new M4.Knight { PlayerId = "P1", LocationId = crossing.source, Active = true, Level = 1 }];
            else H4.Player(s).Resources = H4.Bag(wool: 1, ore: 1);
        });
        Assert.Equal("P2", game.GetPlayerView("P1").LongestRoadPlayerId);
        var command = Execute(game);
        Assert.Equal(move ? M4.CommandKind.MoveKnight : M4.CommandKind.RecruitKnight, command.Kind);
        Assert.Equal(crossing.target, command.TargetId);
        Assert.Null(game.GetPlayerView("P1").LongestRoadPlayerId);
        Assert.Equal(9, game.GetPlayerView("P1").Players.Single(p => p.Id == "P2").VictoryPoints);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CombinedKnightRecruitmentOrMovementRemovesRivalsLongestRoad(bool move)
    {
        var board = H5.New().GetPlayerView("P1").Board;
        var crossing = Crossing(board.Edges.Where(e => board.Tiles.Any(t => t.Resource != "sea" && e.Vertices.All(t.Vertices.Contains))).ToArray());
        var game = H5.Fixture(s =>
        {
            s.Roads = crossing.chain.Select(e => H5.Piece(e, "P2")).Append(H5.Piece(crossing.spur.Id)).ToArray();
            H5.Player(s, "P2").DefenderPoints = 12;
            if (move) s.Knights = [new M5.Knight { PlayerId = "P1", LocationId = crossing.source, Active = true, Level = 1 }];
            else H5.Player(s).Resources = H5.Bag(wool: 1, ore: 1);
        });
        Assert.Equal("P2", game.GetPlayerView("P1").LongestRoadPlayerId);
        var command = Execute(game);
        Assert.Equal(move ? M5.CommandKind.MoveKnight : M5.CommandKind.RecruitKnight, command.Kind);
        Assert.Equal(crossing.target, command.TargetId);
        Assert.Null(game.GetPlayerView("P1").LongestRoadPlayerId);
    }

    [Fact]
    public void CombinedKnightOccupiesRivalsLandingAndPreventsSettlement()
    {
        var board = H5.New().GetPlayerView("P1").Board;
        var coast = board.Vertices.Select(v => new { vertex = v.Id, edges = board.Edges.Where(e => e.Vertices.Contains(v.Id)
            && board.Tiles.Any(t => t.Resource == "sea" && e.Vertices.All(t.Vertices.Contains))).ToArray() })
            .First(v => v.edges.Length >= 2 && board.Tiles.Any(t => t.Resource != "sea" && t.Vertices.Contains(v.vertex)));
        var game = H5.Fixture(s =>
        {
            s.Ships = [new M5.ShipPlacement { PlayerId = "P1", LocationId = coast.edges[0].Id }, new M5.ShipPlacement { PlayerId = "P2", LocationId = coast.edges[1].Id }];
            H5.Player(s).Resources = H5.Bag(wool: 1, ore: 1);
            H5.Player(s, "P2").Resources = H5.Bag(1, 1, 1, 1);
            H5.Player(s, "P2").DefenderPoints = 13;
        });
        var command = Execute(game);
        Assert.Equal(M5.CommandKind.RecruitKnight, command.Kind);
        Assert.Equal(coast.vertex, command.TargetId);
        var state = game.GetAuthoritativeStateForTesting(); state.ActivePlayerId = "P2"; H5.SetState(game, state);
        H5.Rejected(game, H5.Cmd(M5.CommandKind.BuildSettlement, "P2", coast.vertex));
    }

    [Fact]
    public void EspionageTargetsNearWinnerBeforeAnAffordableCity()
    {
        var board = H4.New().GetPlayerView("P1").Board;
        var home = board.Vertices[0].Id;
        var enemy = board.Vertices.First(v => !board.Edges.Any(e => e.Vertices.Contains(home) && e.Vertices.Contains(v.Id))).Id;
        var game = H4.Fixture(s =>
        {
            s.Settlements = [H4.Piece(home), H4.Piece(enemy, "P3")];
            H4.Player(s).Resources = H4.Bag(wheat: 2, ore: 3);
            H4.Player(s).ProgressCards = [M4.ProgressCardKind.Espionage];
            H4.Player(s, "P2").ProgressCards = [M4.ProgressCardKind.Mining];
            H4.Player(s, "P3").ProgressCards = [M4.ProgressCardKind.Medicine];
            H4.Player(s, "P3").DefenderPoints = 11;
            H4.Player(s, "P3").Resources = H4.Bag(wheat: 2, ore: 3);
        });
        var command = Execute(game);
        Assert.Equal(M4.CommandKind.PlayProgressCard, command.Kind);
        Assert.Equal(M4.ProgressCardKind.Espionage, command.ProgressCard);
        Assert.Equal("P3", command.OtherPlayerId);
    }

    [Fact]
    public void CombinedEspionageTargetsNearWinnerBeforeAnAffordableCity()
    {
        var board = H5.New().GetPlayerView("P1").Board;
        var land = board.Vertices.Where(v => board.Tiles.Any(t => t.Resource != "sea" && t.Vertices.Contains(v.Id))).ToArray();
        var home = land[0].Id;
        var enemy = land.First(v => !board.Edges.Any(e => e.Vertices.Contains(home) && e.Vertices.Contains(v.Id))).Id;
        var game = H5.Fixture(s =>
        {
            s.Settlements = [H5.Piece(home), H5.Piece(enemy, "P3")];
            H5.Player(s).Resources = H5.Bag(wheat: 2, ore: 3);
            H5.Player(s).ProgressCards = [M5.ProgressCardKind.Espionage];
            H5.Player(s, "P2").ProgressCards = [M5.ProgressCardKind.Mining];
            H5.Player(s, "P3").ProgressCards = [M5.ProgressCardKind.Medicine];
            H5.Player(s, "P3").DefenderPoints = 14;
            H5.Player(s, "P3").Resources = H5.Bag(wheat: 2, ore: 3);
        });
        var command = Execute(game);
        Assert.Equal(M5.CommandKind.PlayProgressCard, command.Kind);
        Assert.Equal(M5.ProgressCardKind.Espionage, command.ProgressCard);
        Assert.Equal("P3", command.OtherPlayerId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CitiesCanPrepareAnInactiveKnightOrDisplaceAWeakerBlocker(bool prepare)
    {
        var crossing = Crossing(H4.New().GetPlayerView("P1").Board.Edges);
        var game = H4.Fixture(s =>
        {
            s.Roads = crossing.chain.Select(e => H4.Piece(e, "P2")).Append(H4.Piece(crossing.spur.Id)).ToArray();
            H4.Player(s, "P2").DefenderPoints = 9;
            s.Knights = [new M4.Knight { PlayerId = "P1", LocationId = crossing.source, Active = !prepare, Level = 2 }];
            if (prepare) H4.Player(s).Resources.Wheat = 1;
            else s.Knights = s.Knights.Append(new M4.Knight { PlayerId = "P2", LocationId = crossing.target, Level = 1 }).ToArray();
        });
        var command = Execute(game);
        if (prepare)
        {
            Assert.Equal(M4.CommandKind.ActivateKnight, command.Kind);
            var tooEarly = H4.Cmd(M4.CommandKind.MoveKnight, target: crossing.target); tooEarly.SourceId = crossing.source;
            H4.Rejected(game, tooEarly);
            var nextTurn = game.GetAuthoritativeStateForTesting(); nextTurn.Turn += 4; H4.SetState(game, nextTurn);
            command = Execute(game);
        }
        Assert.Equal(prepare ? M4.CommandKind.MoveKnight : M4.CommandKind.DisplaceKnight, command.Kind);
        Assert.Equal(crossing.target, command.TargetId);
        Assert.Null(game.GetPlayerView("P1").LongestRoadPlayerId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CombinedCanPrepareAnInactiveKnightOrDisplaceAWeakerBlocker(bool prepare)
    {
        var board = H5.New().GetPlayerView("P1").Board;
        var crossing = Crossing(board.Edges.Where(e => board.Tiles.Any(t => t.Resource != "sea" && e.Vertices.All(t.Vertices.Contains))).ToArray());
        var game = H5.Fixture(s =>
        {
            s.Roads = crossing.chain.Select(e => H5.Piece(e, "P2")).Append(H5.Piece(crossing.spur.Id)).ToArray();
            H5.Player(s, "P2").DefenderPoints = 12;
            s.Knights = [new M5.Knight { PlayerId = "P1", LocationId = crossing.source, Active = !prepare, Level = 2 }];
            if (prepare) H5.Player(s).Resources.Wheat = 1;
            else s.Knights = s.Knights.Append(new M5.Knight { PlayerId = "P2", LocationId = crossing.target, Level = 1 }).ToArray();
        });
        var command = Execute(game);
        if (prepare)
        {
            Assert.Equal(M5.CommandKind.ActivateKnight, command.Kind);
            var tooEarly = H5.Cmd(M5.CommandKind.MoveKnight, target: crossing.target); tooEarly.SourceId = crossing.source;
            H5.Rejected(game, tooEarly);
            var nextTurn = game.GetAuthoritativeStateForTesting(); nextTurn.Turn += 4; H5.SetState(game, nextTurn);
            command = Execute(game);
        }
        Assert.Equal(prepare ? M5.CommandKind.MoveKnight : M5.CommandKind.DisplaceKnight, command.Kind);
        Assert.Equal(crossing.target, command.TargetId);
        Assert.Null(game.GetPlayerView("P1").LongestRoadPlayerId);
    }

    [Fact]
    public void CombinedPirateBlocksTheNextUnbuiltShipBeforeAnyFleetExists()
    {
        var board = H5.New().GetPlayerView("P1").Board;
        var root = board.Vertices.First(v => board.Tiles.Any(t => t.Resource == "sea" && t.Vertices.Contains(v.Id))
            && board.Tiles.Count(t => t.Number.HasValue && t.Vertices.Contains(v.Id)) == 1).Id;
        var game = H5.Fixture(s =>
        {
            s.Settlements = [H5.Piece(root, "P2")];
            var coastalTile = s.Tiles.Single(t => t.Number.HasValue && t.Vertices.Contains(root));
            var lowYieldTile = s.Tiles.First(t => t.Number == 2);
            int? previousNumber = coastalTile.Number;
            coastalTile.Number = lowYieldTile.Number; lowYieldTile.Number = previousNumber;
            H5.Player(s, "P2").Resources = H5.Bag(wood: 1, wool: 1);
            s.FirstBarbarianAttack = true; s.PirateTileId = "frame";
            s.RobberTileId = board.Tiles.First(t => t.Resource == "desert").Id;
            s.ActivePlayerId = "P2";
        });
        var available = game.GetPlayerView("P2").LegalShipEdgeIds;
        Assert.NotEmpty(available);
        var state = game.GetAuthoritativeStateForTesting();
        state.ActivePlayerId = "P1"; state.Phase = M5.GamePhase.RobberMove;
        state.PendingDecision = new M5.PendingDecision { Kind = "Robber", PlayerId = "P1", ReturnPhase = M5.GamePhase.Action };
        H5.SetState(game, state);
        var command = Execute(game);
        Assert.Equal(M5.CommandKind.MovePirate, command.Kind);
        Assert.Empty(game.GetPlayerView("P1").Board.Ships);
        var empty = board.Edges.First(e => available.Contains(e.Id)
            && e.Vertices.All(board.Tiles.Single(t => t.Id == command.TargetId).Vertices.Contains));
        state = game.GetAuthoritativeStateForTesting();
        state.ActivePlayerId = "P2"; state.Phase = M5.GamePhase.Action; state.PendingDecision = null!; H5.SetState(game, state);
        Assert.DoesNotContain(empty.Id, game.GetPlayerView("P2").LegalShipEdgeIds);
        H5.Rejected(game, H5.Cmd(M5.CommandKind.BuildShip, "P2", empty.Id));
        state = game.GetAuthoritativeStateForTesting(); state.PirateTileId = "frame"; H5.SetState(game, state);
        H5.Accepted(game, H5.Cmd(M5.CommandKind.BuildShip, "P2", empty.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CitiesCommercialHarborDoesNotOfferResourcesToAnEmbargoedPlayer(bool alreadyPlayed)
    {
        var game = H4.Fixture(s =>
        {
            H4.Player(s).Resources.Wood = 1;
            H4.Player(s, "P2").DefenderPoints = 12;
            H4.Player(s, "P2").Commodities.Coin = 1;
            if (alreadyPlayed) s.CommercialHarborPlayers = ["P2"];
            else H4.Player(s).ProgressCards = [M4.ProgressCardKind.CommercialHarbor];
        });
        Assert.Contains(game.GetAiPlayerView("P1").LegalActions, c => alreadyPlayed
            ? c.Kind == M4.CommandKind.CommercialHarborOffer && c.OtherPlayerId == "P2"
            : c.Kind == M4.CommandKind.PlayProgressCard && c.ProgressCard == M4.ProgressCardKind.CommercialHarbor);
        var command = Execute(game);
        Assert.NotEqual(M4.CommandKind.CommercialHarborOffer, command.Kind);
        Assert.False(command.Kind == M4.CommandKind.PlayProgressCard && command.ProgressCard == M4.ProgressCardKind.CommercialHarbor);
        Assert.Equal(0, game.GetPlayerView("P2").OwnResources.Wood);
        Assert.Equal(1, game.GetPlayerView("P1").OwnResources.Wood);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CombinedCommercialHarborDoesNotOfferResourcesToAnEmbargoedPlayer(bool alreadyPlayed)
    {
        var game = H5.Fixture(s =>
        {
            H5.Player(s).Resources.Wood = 1;
            H5.Player(s, "P2").DefenderPoints = 15;
            H5.Player(s, "P2").Commodities.Coin = 1;
            if (alreadyPlayed) s.CommercialHarborPlayers = ["P2"];
            else H5.Player(s).ProgressCards = [M5.ProgressCardKind.CommercialHarbor];
        });
        Assert.Contains(game.GetAiPlayerView("P1").LegalActions, c => alreadyPlayed
            ? c.Kind == M5.CommandKind.CommercialHarborOffer && c.OtherPlayerId == "P2"
            : c.Kind == M5.CommandKind.PlayProgressCard && c.ProgressCard == M5.ProgressCardKind.CommercialHarbor);
        var command = Execute(game);
        Assert.NotEqual(M5.CommandKind.CommercialHarborOffer, command.Kind);
        Assert.False(command.Kind == M5.CommandKind.PlayProgressCard && command.ProgressCard == M5.ProgressCardKind.CommercialHarbor);
        Assert.Equal(0, game.GetPlayerView("P2").OwnResources.Wood);
        Assert.Equal(1, game.GetPlayerView("P1").OwnResources.Wood);
    }

    [Fact]
    public void CitiesStillCompletesForcedCommercialHarborResponseToAnEmbargoedPlayer()
    {
        var game = H4.Fixture(s =>
        {
            s.ActivePlayerId = "P2";
            H4.Player(s).Commodities.Coin = 1;
            H4.Player(s, "P2").DefenderPoints = 12;
            H4.Player(s, "P2").Resources.Wood = 1;
            H4.Player(s, "P2").ProgressCards = [M4.ProgressCardKind.CommercialHarbor];
        });
        var play = H4.Cmd(M4.CommandKind.PlayProgressCard, "P2"); play.ProgressCard = M4.ProgressCardKind.CommercialHarbor;
        H4.Accepted(game, play);
        var offer = H4.Cmd(M4.CommandKind.CommercialHarborOffer, "P2"); offer.OtherPlayerId = "P1"; offer.Resource = Resource.Wood;
        H4.Accepted(game, offer);
        Assert.Equal("ProgressHarbor", game.GetPlayerView("P1").PendingDecision.Kind);
        Assert.Equal(M4.CommandKind.ResolveProgressChoice, Execute(game).Kind);
        Assert.Null(game.GetPlayerView("P1").PendingDecision);
        Assert.Equal(M4.GamePhase.Action, game.GetPlayerView("P1").Phase);
        Assert.Equal(1, game.GetPlayerView("P2").OwnCommodities.Coin);
        Assert.Equal(1, game.GetPlayerView("P1").OwnResources.Wood);
    }

    [Fact]
    public void CombinedStillCompletesForcedCommercialHarborResponseToAnEmbargoedPlayer()
    {
        var game = H5.Fixture(s =>
        {
            s.ActivePlayerId = "P2";
            H5.Player(s).Commodities.Coin = 1;
            H5.Player(s, "P2").DefenderPoints = 15;
            H5.Player(s, "P2").Resources.Wood = 1;
            H5.Player(s, "P2").ProgressCards = [M5.ProgressCardKind.CommercialHarbor];
        });
        var play = H5.Cmd(M5.CommandKind.PlayProgressCard, "P2"); play.ProgressCard = M5.ProgressCardKind.CommercialHarbor;
        H5.Accepted(game, play);
        var offer = H5.Cmd(M5.CommandKind.CommercialHarborOffer, "P2"); offer.OtherPlayerId = "P1"; offer.Resource = Resource.Wood;
        H5.Accepted(game, offer);
        Assert.Equal("ProgressHarbor", game.GetPlayerView("P1").PendingDecision.Kind);
        Assert.Equal(M5.CommandKind.ResolveProgressChoice, Execute(game).Kind);
        Assert.Null(game.GetPlayerView("P1").PendingDecision);
        Assert.Equal(M5.GamePhase.Action, game.GetPlayerView("P1").Phase);
        Assert.Equal(1, game.GetPlayerView("P2").OwnCommodities.Coin);
        Assert.Equal(1, game.GetPlayerView("P1").OwnResources.Wood);
    }

    [Fact]
    public void CitiesRejectsEvenProfitableTradeWhenOneRecruitmentCutsAndClaimsTheWinningRoad()
    {
        var routes = AwardCrossing(H4.New().GetPlayerView("P1").Board.Edges);
        var game = H4.Fixture(s =>
        {
            s.ActivePlayerId = "P2";
            s.Roads = routes.rival.Select(e => H4.Piece(e, "P3")).Concat(routes.own.Select(e => H4.Piece(e, "P2"))).ToArray();
            H4.Player(s).DefenderPoints = 8; H4.Player(s).Resources.Wheat = 1;
            H4.Player(s, "P2").DefenderPoints = 11;
            H4.Player(s, "P2").Resources = H4.Bag(wood: 15, wool: 1, ore: 1);
        });
        Assert.Equal("P3", game.GetPlayerView("P1").LongestRoadPlayerId);
        Assert.Equal(5, game.GetPlayerView("P1").Players.Single(p => p.Id == "P2").LongestRoadLength);
        var offer = H4.Cmd(M4.CommandKind.ProposeTrade, "P2"); offer.OtherPlayerId = "P1";
        offer.Give = H4.Bag(wood: 15); offer.Receive = H4.Bag(wheat: 1); H4.Accepted(game, offer);
        Assert.Equal(M4.CommandKind.RejectTrade, Execute(game).Kind);
        H4.Accepted(game, H4.Cmd(M4.CommandKind.RecruitKnight, "P2", routes.target));
        Assert.Equal("P2", game.GetPlayerView("P1").LongestRoadPlayerId);
        Assert.Equal("P2", game.GetPlayerView("P1").WinnerPlayerId);
    }

    [Fact]
    public void CombinedRejectsEvenProfitableTradeWhenOneRecruitmentCutsAndClaimsTheWinningRoad()
    {
        var board = H5.New().GetPlayerView("P1").Board;
        var routes = AwardCrossing(board.Edges.Where(e => board.Tiles.Any(t => t.Resource != "sea" && e.Vertices.All(t.Vertices.Contains))).ToArray());
        var game = H5.Fixture(s =>
        {
            s.ActivePlayerId = "P2";
            s.Roads = routes.rival.Select(e => H5.Piece(e, "P3")).Concat(routes.own.Select(e => H5.Piece(e, "P2"))).ToArray();
            H5.Player(s).DefenderPoints = 11; H5.Player(s).Resources.Wheat = 1;
            H5.Player(s, "P2").DefenderPoints = 14;
            H5.Player(s, "P2").Resources = H5.Bag(wood: 15, wool: 1, ore: 1);
        });
        Assert.Equal("P3", game.GetPlayerView("P1").LongestRoadPlayerId);
        Assert.Equal(5, game.GetPlayerView("P1").Players.Single(p => p.Id == "P2").LongestRoadLength);
        var offer = H5.Cmd(M5.CommandKind.ProposeTrade, "P2"); offer.OtherPlayerId = "P1";
        offer.Give = H5.Bag(wood: 15); offer.Receive = H5.Bag(wheat: 1); H5.Accepted(game, offer);
        Assert.Equal(M5.CommandKind.RejectTrade, Execute(game).Kind);
        H5.Accepted(game, H5.Cmd(M5.CommandKind.RecruitKnight, "P2", routes.target));
        Assert.Equal("P2", game.GetPlayerView("P1").LongestRoadPlayerId);
        Assert.Equal("P2", game.GetPlayerView("P1").WinnerPlayerId);
    }

    private static (string[] rival, string[] own, string target) AwardCrossing(Edge[] edges)
    {
        (string[] edges, string[] vertices)? Find(string start, int length, HashSet<string> excluded)
        {
            var chain = new List<string>(); var vertices = new List<string> { start };
            bool Walk(string vertex)
            {
                if (chain.Count == length) return true;
                foreach (var edge in edges.Where(e => e.Vertices.Contains(vertex)))
                {
                    string next = edge.Vertices.Single(v => v != vertex);
                    if (vertices.Contains(next) || excluded.Contains(next)) continue;
                    chain.Add(edge.Id); vertices.Add(next); if (Walk(next)) return true;
                    vertices.RemoveAt(vertices.Count - 1); chain.RemoveAt(chain.Count - 1);
                }
                return false;
            }
            return Walk(start) ? (chain.ToArray(), vertices.ToArray()) : null;
        }
        foreach (var start in edges.SelectMany(e => e.Vertices).Distinct())
        {
            var rival = Find(start, 6, []); if (!rival.HasValue) continue;
            foreach (var target in rival.Value.vertices.Skip(2).Take(3))
            {
                var own = Find(target, 5, rival.Value.vertices.Where(v => v != target).ToHashSet());
                if (own.HasValue) return (rival.Value.edges, own.Value.edges, target);
            }
        }
        throw new InvalidOperationException("没有可用于截断夺奖的交叉路线。");
    }

    private static M4.Command Execute(M4.CitiesKnightsGameSession game)
    {
        var view = game.GetAiPlayerView("P1"); var before = M6Game.Json(view);
        var command = new CitiesKnightsAi().Decide(view);
        Assert.Equal(M6Game.Json(command), M6Game.Json(new CitiesKnightsAi().Decide(view)));
        Assert.Equal(before, M6Game.Json(view));
        command.Id = Guid.NewGuid().ToString("N"); command.PlayerId = "P4"; H4.Rejected(game, command);
        command.PlayerId = "P1"; H4.Accepted(game, command); return command;
    }

    private static M5.Command Execute(M5.CombinedGameSession game)
    {
        var view = game.GetAiPlayerView("P1"); var before = M6Game.Json(view);
        var command = new CombinedAi().Decide(view);
        Assert.Equal(M6Game.Json(command), M6Game.Json(new CombinedAi().Decide(view)));
        Assert.Equal(before, M6Game.Json(view));
        command.Id = Guid.NewGuid().ToString("N"); command.PlayerId = "P4"; H5.Rejected(game, command);
        command.PlayerId = "P1"; H5.Accepted(game, command); return command;
    }

    private static (string[] chain, Edge spur, string target, string source) Crossing(Edge[] edges)
    {
        foreach (var start in edges.SelectMany(e => e.Vertices).Distinct())
        {
            var chain = new List<Edge>(); var vertices = new List<string> { start };
            (string[] chain, Edge spur, string target, string source)? result = null;
            bool Find(string vertex)
            {
                if (chain.Count == 6)
                {
                    var target = vertices[3];
                    var spur = edges.FirstOrDefault(e => e.Vertices.Contains(target) && !chain.Contains(e) && !vertices.Contains(e.Vertices.Single(v => v != target)));
                    if (spur == null) return false;
                    result = (chain.Select(e => e.Id).ToArray(), spur, target, spur.Vertices.Single(v => v != target)); return true;
                }
                foreach (var edge in edges.Where(e => e.Vertices.Contains(vertex)))
                {
                    var next = edge.Vertices.Single(v => v != vertex); if (vertices.Contains(next)) continue;
                    chain.Add(edge); vertices.Add(next); if (Find(next)) return true;
                    vertices.RemoveAt(vertices.Count - 1); chain.RemoveAt(chain.Count - 1);
                }
                return false;
            }
            if (Find(start)) return result!.Value;
        }
        throw new InvalidOperationException("没有可用的六段路线与交叉支路。");
    }
}
