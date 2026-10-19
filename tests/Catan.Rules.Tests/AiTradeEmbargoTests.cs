using System.Reflection;
using Catan.AI;
using Catan.Core;
using Xunit;
using M2 = Catan.Core.M2;
using M3 = Catan.Core.M3;
using M4 = Catan.Core.M4;
using M5 = Catan.Core.M5;

namespace Catan.Rules.Tests;

public sealed class AiTradeEmbargoTests
{
    public static IEnumerable<object[]> Rules() => new[] { "base", "seafarers", "cities", "combined" }.Select(r => new object[] { r });
    public static IEnumerable<object[]> NearVictoryOffers()
    {
        foreach (var rule in Rules()) foreach (int gap in new[] { 2, 3 }) foreach (int gift in new[] { 2, 15 })
            yield return new object[] { rule[0], gap, gift };
    }
    public static IEnumerable<object[]> ScoreLeads()
    {
        foreach (var rule in Rules())
        {
            yield return new object[] { rule[0], new[] { 3, 9, 3, 2 }, true };
            yield return new object[] { rule[0], new[] { 4, 6, 1, 1 }, false };
            yield return new object[] { rule[0], new[] { 7, 8, 2, 1 }, false };
        }
    }
    public static IEnumerable<object[]> JoinedRoutes()
    {
        foreach (var rule in Rules()) foreach (int first in new[] { 2, 3 }) yield return new object[] { rule[0], first };
    }

    [Theory, MemberData(nameof(Rules))]
    public void OnePointFromVictoryRejectsEvenFifteenResourcesForOne(string rules)
    {
        var game = Fixture(rules, new[] { 4, 9, 4, 4 }, 10);
        Assert.Equal(9, (int)game.GetPlayerView("P2").OwnVictoryPoints);
        Respond(rules, game, 15, "RejectTrade");
    }

    [Theory, MemberData(nameof(NearVictoryOffers))]
    public void TwoOrThreeStepsFromVictoryRejectsOrdinaryProfitButAllowsExceptionalProfit(string rules, int gap, int gift)
    {
        var game = Fixture(rules, new[] { 4, 10 - gap, 4, 4 }, 10);
        Respond(rules, game, gift, gift == 15 ? "AcceptTrade" : "RejectTrade");
    }

    [Theory, MemberData(nameof(ScoreLeads))]
    public void SixPointEmbargoComparesSecondPlaceRatherThanTotalScoreOrLastPlace(string rules, int[] scores, bool embargo)
    {
        var game = Fixture(rules, scores, 20);
        Assert.Equal(scores[1], (int)game.GetPlayerView("P2").OwnVictoryPoints);
        Respond(rules, game, 15, embargo ? "RejectTrade" : "AcceptTrade");
    }

    [Theory, MemberData(nameof(Rules))]
    public void ProposalsSkipEmbargoedPlayersAndPlayersWhoDoNotHoldTheRequestedResource(string rules)
    {
        var game = Fixture(rules, new[] { 0, 9, 0, 0 }, 10, s =>
        {
            s.ActivePlayerId = "P1";
            s.Players[0].Resources = new ResourceBag { Wood = 2 };
            s.Players[1].Resources = new ResourceBag { Brick = 2, Wool = 2, Wheat = 2, Ore = 2 };
            s.Players[2].Resources = new ResourceBag { Wood = 3 };
            s.Players[3].Resources = new ResourceBag { Wool = 1 };
        });
        var adapter = new M6Game(rules, game);
        dynamic offer = adapter.Decide(adapter.AiView("P1"));
        Assert.Equal("ProposeTrade", (string)offer.Kind.ToString());
        Assert.Equal("P4", (string)offer.OtherPlayerId);
        Assert.Equal(1, (int)offer.Receive.Wool);
        Assert.Equal(1, (int)offer.Give.Wood);
        offer.Id = "精确手牌主动报价";
        adapter.Accept(offer);
        Assert.NotNull((object?)game.GetPlayerView("P1").TradeOffer);
        adapter.AssertInvariants();
    }

    [Theory]
    [InlineData("seafarers")][InlineData("combined")]
    public void ReachableForeignIslandCountsAsOneWinningActionForThreePoints(string rules)
    {
        dynamic scenario = rules == "seafarers" ? (object)M3.SeafarersScenarios.Create("heading-for-new-shores", 4) : M5.CombinedScenarios.Create("heading-for-new-shores", 4);
        var board = Board(rules);
        Tile[] tiles = board.Tiles; Edge[] edges = board.Edges;
        string[] startingTiles = scenario.StartingTileIds;
        var island = ((Array)scenario.Regions).Cast<dynamic>().First(r => !((string[])r.TileIds).Intersect(startingTiles).Any());
        string[] islandTiles = island.TileIds;
        string landing = tiles.Where(t => islandTiles.Contains(t.Id)).SelectMany(t => t.Vertices).First(v =>
            edges.Any(e => e.Vertices.Contains(v) && tiles.Any(t => t.Resource == "sea" && e.Vertices.All(t.Vertices.Contains))));
        var ship = edges.First(e => e.Vertices.Contains(landing) && tiles.Any(t => t.Resource == "sea" && e.Vertices.All(t.Vertices.Contains)));
        string mainRegion = ((Array)scenario.Regions).Cast<dynamic>().Single(r => ((string[])r.TileIds).Intersect(startingTiles).Any()).Id;
        var game = Fixture(rules, new[] { 4, 7, 0, 0 }, 10, s =>
        {
            s.Players[1].Resources.Brick = 1; s.Players[1].Resources.Wool = 1; s.Players[1].Resources.Wheat = 1;
            s.Players[1].HomeRegions = new[] { mainRegion }; s.Players[1].SettledRegions = new[] { mainRegion };
            if (rules == "seafarers") s.Ships = new[] { new M3.ShipPlacement { PlayerId = "P2", LocationId = ship.Id } };
            else s.Ships = new[] { new M5.ShipPlacement { PlayerId = "P2", LocationId = ship.Id } };
        }, startingTiles);
        Assert.Contains(landing, (string[])game.GetPlayerView("P2").LegalVertexIds);
        Respond(rules, game, 15, "RejectTrade");
        dynamic build = Command(rules, "BuildSettlement", "P2"); build.TargetId = landing;
        var adapter = new M6Game(rules, game); adapter.Accept(build);
        Assert.Equal(10, (int)game.GetPlayerView("P2").OwnVictoryPoints);
        Assert.Equal("Finished", (string)game.GetPlayerView("P2").Phase.ToString());
        adapter.AssertInvariants();
    }

    [Theory]
    [InlineData("cities", false)][InlineData("combined", false)]
    [InlineData("cities", true)][InlineData("combined", true)]
    public void ImprovingIntoAnUnclaimedOrStolenMetropolisCountsAsOneWinningAction(string rules, bool steal)
    {
        var game = Fixture(rules, new[] { 4, 8, 2, 0 }, 10, s =>
        {
            s.Players[1].Improvements[2] = steal ? 4 : 3;
            s.Players[1].Commodities.Paper = steal ? 5 : 4;
            if (!steal) return;
            s.Players[2].Improvements[2] = 4;
            string city = ((PiecePlacement[])s.Cities).Single(c => c.PlayerId == "P3").LocationId;
            if (rules == "cities") s.Metropolises = new[] { new M4.Metropolis { PlayerId = "P3", LocationId = city, Track = M4.ImprovementTrack.Science } };
            else s.Metropolises = new[] { new M5.Metropolis { PlayerId = "P3", LocationId = city, Track = M5.ImprovementTrack.Science } };
        });
        Respond(rules, game, 15, "RejectTrade");
        dynamic improve = Command(rules, "ImproveCity", "P2");
        if (rules == "cities") improve.Track = M4.ImprovementTrack.Science; else improve.Track = M5.ImprovementTrack.Science;
        var adapter = new M6Game(rules, game); adapter.Accept(improve);
        Assert.Equal("Metropolis", (string)game.GetPlayerView("P2").PendingDecision.Kind);
        dynamic select = Command(rules, "ResolveChoice", "P2"); select.TargetId = game.GetPlayerView("P2").PendingDecision.Options[0];
        adapter.Accept(select);
        Assert.Equal(10, (int)game.GetPlayerView("P2").OwnVictoryPoints);
        Assert.Equal("Finished", (string)game.GetPlayerView("P2").Phase.ToString());
        adapter.AssertInvariants();
    }

    [Fact]
    public void LevelThreeWonderIsAnImmediateVictoryThreatEvenAtZeroVictoryPoints()
    {
        var game = M3Harness.Fixture(s =>
        {
            s.ActivePlayerId = "P2";
            s.Players[0].Resources = new ResourceBag { Wood = 1, Ore = 5 };
            s.Players[1].Resources = new ResourceBag { Wood = 15, Brick = 1, Wool = 3 };
            s.Players[1].WonderId = "grand-theater"; s.Players[1].WonderLevel = 3;
        }, "wonders-of-catan");
        Assert.Equal(0, game.GetPlayerView("P2").OwnVictoryPoints);
        Assert.Equal(1, WinningActions(game.GetAiPlayerView("P1")));
        Respond("seafarers", game, 15, "RejectTrade");
        var build = M3Harness.Cmd(M3.CommandKind.BuildWonder, "P2", "grand-theater");
        M3Harness.Accepted(game, build);
        Assert.Equal(M3.GamePhase.Finished, game.GetPlayerView("P2").Phase);
        Assert.Equal("P2", game.GetPlayerView("P2").WinnerPlayerId);
    }

    [Theory, MemberData(nameof(JoinedRoutes))]
    public void OneConnectorCanJoinTwoShortRoutesAndWinTheTwoPointAward(string rules, int firstLength)
    {
        dynamic board = Board(rules); Tile[] tiles = board.Tiles; Edge[] edges = board.Edges;
        var land = edges.Where(e => tiles.Any(t => t.Resource != "sea" && t.Resource != "fog" && e.Vertices.All(t.Vertices.Contains))).ToArray();
        var path = Path(land, firstLength + 3);
        var vertices = path.SelectMany(e => e.Vertices).Distinct().ToArray();
        var cities = SeparateCities(tiles, edges, vertices);
        var game = Fixture(rules, new[] { 0, 0, 0, 0 }, 10, s =>
        {
            s.Cities = cities; s.Roads = path.Where((_, i) => i != firstLength).Select(e => new PiecePlacement { LocationId = e.Id, PlayerId = "P2" }).ToArray();
            s.Players[1].Resources.Brick = 1;
        });
        object view = game.GetAiPlayerView("P1");
        dynamic opponent = ((Array)((dynamic)view).Players).Cast<dynamic>().Single(p => p.Id == "P2");
        Assert.Equal(firstLength, (int)opponent.LongestRoadLength);
        opponent.Pieces.Settlements = 0;
        Assert.Equal(1, WinningActions(view));
        dynamic build = Command(rules, "BuildRoad", "P2"); build.TargetId = path[firstLength].Id;
        new M6Game(rules, game).Accept(build);
        Assert.Equal("P2", (string)game.GetPlayerView("P1").LongestRoadPlayerId);
        Assert.Equal(10, (int)game.GetPlayerView("P2").OwnVictoryPoints);
        Assert.Equal("Finished", (string)game.GetPlayerView("P1").Phase.ToString());
    }

    [Theory]
    [InlineData("base")][InlineData("seafarers")]
    public void AThirdPlayedKnightCanBeAnImmediateTwoPointVictoryThreat(string rules)
    {
        var game = Fixture(rules, new[] { 4, 8, 0, 0 }, 10, s =>
        {
            s.Players[1].PlayedKnights = 2;
            if (rules == "base")
            {
                s.PlayedDevelopmentCards = new[] { M2.DevelopmentCardKind.Knight, M2.DevelopmentCardKind.Knight };
                s.Players[1].DevelopmentCards = new[] { new M2.DevelopmentCard { Kind = M2.DevelopmentCardKind.Knight, BoughtTurn = 1 } };
            }
            else
            {
                s.PlayedDevelopmentCards = new[] { M3.DevelopmentCardKind.Knight, M3.DevelopmentCardKind.Knight };
                s.Players[1].DevelopmentCards = new[] { new M3.DevelopmentCard { Kind = M3.DevelopmentCardKind.Knight, BoughtTurn = 1 } };
            }
        });
        Assert.Equal(1, WinningActions((object)game.GetAiPlayerView("P1")));
        Respond(rules, game, 15, "RejectTrade");
        new M6Game(rules, game).Accept(Command(rules, "PlayKnight", "P2"));
        Assert.Equal("P2", (string)game.GetPlayerView("P1").LargestArmyPlayerId);
        Assert.Equal("Finished", (string)game.GetPlayerView("P1").Phase.ToString());
        Assert.Equal(10, (int)game.GetPlayerView("P2").OwnVictoryPoints);
    }

    [Fact]
    public void PirateIslandWarshipsDoNotCreateALargestArmyVictoryThreat()
    {
        var view = M3.SeafarersGameSession.Create("pirate-islands", 4, 97).GetAiPlayerView("P1");
        var opponent = view.Players.Single(p => p.Id == "P2");
        typeof(M3.PublicPlayer).GetProperty("VictoryPoints")!.SetValue(opponent, 8);
        typeof(M3.PublicPlayer).GetProperty("PlayedKnights")!.SetValue(opponent, 2);
        typeof(M3.PublicPlayer).GetProperty("DevelopmentCardCount")!.SetValue(opponent, 1);
        Assert.Equal(2, WinningActions(view));
    }

    // 以下只改变脱离权威状态的投影供应，隔离“差一条路”的风险估算边界；完整交易另由上方真实夹具验证。
    [Theory, MemberData(nameof(Rules))]
    public void ProjectedEmptyRoadSupplyDoesNotPretendTheFifthRoadCanWin(string rules)
    {
        var (view, _) = RouteView(rules, false);
        Assert.Equal(1, WinningActions(view));
        dynamic opponent = ((Array)((dynamic)view).Players).Cast<dynamic>().Single(p => p.Id == "P2");
        opponent.Pieces.Roads = 0;
        Assert.Equal(2, WinningActions(view));
    }

    [Theory]
    [InlineData("seafarers")][InlineData("combined")]
    public void ProjectedEmptyShipSupplyDoesNotPretendTheFifthShipCanWin(string rules)
    {
        var (view, _) = RouteView(rules, true);
        Assert.Equal(1, WinningActions(view));
        object opponent = ((Array)((dynamic)view).Players).Cast<dynamic>().Single(p => p.Id == "P2");
        opponent.GetType().GetProperty("ShipsRemaining")!.SetValue(opponent, 0);
        Assert.Equal(2, WinningActions(view));
    }

    [Theory]
    [InlineData("seafarers")][InlineData("combined")]
    public void PirateBlockingTheOnlyWinningShipExtensionRequiresAnotherAction(string rules)
    {
        var (view, pirateTile) = RouteView(rules, true);
        Assert.Equal(1, WinningActions(view));
        object board = ((dynamic)view).Board;
        board.GetType().GetProperty("PirateTileId")!.SetValue(board, pirateTile);
        Assert.Equal(2, WinningActions(view));
    }

    private static int WinningActions(object view) => (int)typeof(BaseGameAi).Assembly.GetType("Catan.AI.AiOpponentStrategy")!
        .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
        .Single(m => m.Name == "MinimumWinningActions" && m.GetParameters()[0].ParameterType == view.GetType()).Invoke(null, new[] { view, "P2" })!;

    private static Edge[] Path(Edge[] edges, int length)
    {
        var path = new List<Edge>(); var vertices = new List<string> { edges[0].Vertices[0] };
        bool Find(string vertex)
        {
            if (path.Count == length) return true;
            foreach (var edge in edges.Where(e => e.Vertices.Contains(vertex)))
            {
                string next = edge.Vertices.Single(v => v != vertex); if (vertices.Contains(next)) continue;
                path.Add(edge); vertices.Add(next); if (Find(next)) return true;
                path.RemoveAt(path.Count - 1); vertices.RemoveAt(vertices.Count - 1);
            }
            return false;
        }
        Assert.True(Find(vertices[0])); return path.ToArray();
    }

    private static PiecePlacement[] SeparateCities(Tile[] tiles, Edge[] edges, IEnumerable<string> excluded)
    {
        var candidates = tiles.Where(t => t.Resource != "sea" && t.Resource != "fog").SelectMany(t => t.Vertices).Distinct().Except(excluded).ToArray();
        var selected = new List<string>();
        foreach (var vertex in candidates)
        {
            if (edges.Any(e => e.Vertices.Contains(vertex) && e.Vertices.Any(selected.Contains))) continue;
            selected.Add(vertex); if (selected.Count == 6) break;
        }
        Assert.Equal(6, selected.Count);
        return selected.Select((v, i) => new PiecePlacement { LocationId = v, PlayerId = i < 2 ? "P1" : "P2" }).ToArray();
    }

    private static (object view, string? pirateTile) RouteView(string rules, bool ship)
    {
        dynamic board = Board(rules); Tile[] tiles = board.Tiles; Edge[] edges = board.Edges;
        var candidates = edges.Where(e => tiles.Any(t => (ship ? t.Resource == "sea" : t.Resource != "sea" && t.Resource != "fog") && e.Vertices.All(t.Vertices.Contains))).ToArray();
        var path = new List<Edge>(); var vertices = new List<string>();
        bool Find(string vertex)
        {
            if (path.Count == 5) return true;
            foreach (var edge in candidates.Where(e => e.Vertices.Contains(vertex)))
            {
                string next = edge.Vertices.Single(v => v != vertex); if (vertices.Contains(next)) continue;
                path.Add(edge); vertices.Add(next); if (Find(next)) return true;
                path.RemoveAt(path.Count - 1); vertices.RemoveAt(vertices.Count - 1);
            }
            return false;
        }
        vertices.Add(candidates[0].Vertices[0]); Assert.True(Find(vertices[0]));
        var cities = SeparateCities(tiles, edges, vertices);
        var extra = candidates.Where(e => !path.Contains(e) && e.Vertices.Intersect(vertices.Take(5)).Any()).ToArray();
        var game = Fixture(rules, new[] { 0, 0, 0, 0 }, 10, s =>
        {
            s.Cities = cities;
            if (!ship) s.Roads = path.Take(4).Select(e => new PiecePlacement { PlayerId = "P2", LocationId = e.Id }).ToArray();
            else if (rules == "seafarers")
                s.Ships = path.Take(4).Select(e => new M3.ShipPlacement { PlayerId = "P2", LocationId = e.Id })
                    .Concat(extra.Select(e => new M3.ShipPlacement { PlayerId = "P3", LocationId = e.Id })).ToArray();
            else s.Ships = path.Take(4).Select(e => new M5.ShipPlacement { PlayerId = "P2", LocationId = e.Id })
                    .Concat(extra.Select(e => new M5.ShipPlacement { PlayerId = "P3", LocationId = e.Id })).ToArray();
            if (ship)
            {
                s.PirateTileId = "frame";
                if (rules == "combined")
                {
                    s.FirstBarbarianAttack = true;
                    s.RobberTileId = tiles.First(t => t.Resource != "sea" && t.Resource != "fog").Id;
                }
            }
        });
        object view = game.GetAiPlayerView("P1");
        dynamic opponent = ((Array)((dynamic)view).Players).Cast<dynamic>().Single(p => p.Id == "P2");
        Assert.Equal(4, (int)opponent.LongestRoadLength);
        opponent.Pieces.Settlements = 0;
        return (view, ship ? tiles.First(t => t.Resource == "sea" && path[4].Vertices.All(t.Vertices.Contains)).Id : null);
    }

    private static void Respond(string rules, dynamic game, int wood, string expected)
    {
        var adapter = new M6Game(rules, game);
        var before = Enumerable.Range(1, 4).Select(i => (ResourceBag)game.GetPlayerView("P" + i).OwnResources).ToArray();
        ResourceBag bank = game.GetPlayerView("P1").Bank;
        dynamic offer = Command(rules, "ProposeTrade", "P2");
        offer.OtherPlayerId = "P1"; offer.Give = new ResourceBag { Wood = wood }; offer.Receive = new ResourceBag { Ore = 1 };
        adapter.Accept(offer);
        dynamic response = adapter.Decide(adapter.AiView("P1"));
        Assert.Equal(expected, (string)response.Kind.ToString());
        response.Id = "交易禁运响应"; adapter.Accept(response);
        Assert.Null((object?)game.GetPlayerView("P1").TradeOffer);
        foreach (var resource in Enum.GetValues<Resource>())
        {
            Assert.Equal(bank[resource], ((ResourceBag)game.GetPlayerView("P1").Bank)[resource]);
            int total = bank[resource];
            for (int seat = 1; seat <= 4; seat++)
            {
                int actual = ((ResourceBag)game.GetPlayerView("P" + seat).OwnResources)[resource]; total += actual;
                if (expected == "RejectTrade") Assert.Equal(before[seat - 1][resource], actual);
            }
            Assert.Equal(19, total);
        }
        adapter.AssertInvariants();
    }

    private static dynamic Fixture(string rules, int[] scores, int target, Action<dynamic>? configure = null, string[]? homeTiles = null)
    {
        dynamic board = Board(rules); Tile[] tiles = board.Tiles; Edge[] edges = board.Edges;
        var available = tiles.Where(t => t.Resource != "sea" && t.Resource != "fog" && (homeTiles == null || homeTiles.Contains(t.Id))).SelectMany(t => t.Vertices).Distinct().ToList();
        var used = new HashSet<string>(); var cities = new List<PiecePlacement>(); var villages = new List<PiecePlacement>();
        for (int p = 0; p < scores.Length; p++)
        {
            int cityCount = Math.Min(4, scores[p] / 2), villageCount = scores[p] - cityCount * 2;
            for (int piece = 0; piece < cityCount + villageCount; piece++)
            {
                string site = available.First(v => !used.Contains(v) && !edges.Any(e => e.Vertices.Contains(v) && e.Vertices.Any(used.Contains)));
                used.Add(site); var placement = new PiecePlacement { LocationId = site, PlayerId = "P" + (p + 1) };
                if (piece < cityCount) cities.Add(placement); else villages.Add(placement);
            }
        }
        void Configure(dynamic s)
        {
            s.ActivePlayerId = "P2"; s.TargetVictoryPoints = target; s.Cities = cities.ToArray(); s.Settlements = villages.ToArray();
            s.Players[0].Resources = new ResourceBag { Wood = 1, Ore = 5 };
            s.Players[1].Resources = new ResourceBag { Wood = 15 };
            configure?.Invoke(s);
        }
        return rules switch
        {
            "base" => (object)M2Harness.Fixture(s => Configure(s)), "seafarers" => M3Harness.Fixture(s => Configure(s)),
            "cities" => M4CoreHarness.Fixture(s => Configure(s)), "combined" => M5Harness.Fixture(s => Configure(s)),
            _ => throw new ArgumentException(rules)
        };
    }
    private static dynamic Board(string rules) => rules switch
    {
        "base" => (object)M2Harness.New().GetPlayerView("P1").Board, "seafarers" => M3Harness.New().GetPlayerView("P1").Board,
        "cities" => M4CoreHarness.New().GetPlayerView("P1").Board, "combined" => M5Harness.New().GetPlayerView("P1").Board,
        _ => throw new ArgumentException(rules)
    };
    private static dynamic Command(string rules, string kind, string player) => rules switch
    {
        "base" => (object)M2Harness.Cmd(Enum.Parse<M2.CommandKind>(kind), player), "seafarers" => M3Harness.Cmd(Enum.Parse<M3.CommandKind>(kind), player),
        "cities" => M4CoreHarness.Cmd(Enum.Parse<M4.CommandKind>(kind), player), "combined" => M5Harness.Cmd(Enum.Parse<M5.CommandKind>(kind), player),
        _ => throw new ArgumentException(rules)
    };
}
