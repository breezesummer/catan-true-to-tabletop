using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Catan.Core;
using Catan.Core.M2;
using Catan.Core.TradeModules;
using Xunit;
using Xunit.Abstractions;
using Command = Catan.Core.M2.Command;
using CommandKind = Catan.Core.M2.CommandKind;
using GamePhase = Catan.Core.M2.GamePhase;
using GameState = Catan.Core.M2.GameState;
using PendingDecision = Catan.Core.M2.PendingDecision;
using static Catan.Rules.Tests.M2Harness;

namespace Catan.Rules.Tests;

public sealed class FishingTests(ITestOutputHelper output)
{
    static BaseGameSession NewFish(int count = 4, uint seed = 42, bool harbor = false, int? target = null) =>
        BaseGameSession.Create(AcceptanceFixture.Json, count, seed, target, new TradeModuleOptions { Fishing = true, Harbormaster = harbor });
    static FishingState Fish(GameState s) => s.TradeModules.Fishing;
    static FishHolding Hand(GameState s, string player = "P1") => Fish(s).Holdings.Single(h => h.PlayerId == player);
    static void Give(GameState s, string player, params string[] tokens)
    {
        Fish(s).Supply = Fish(s).Supply.Except(tokens).ToArray();
        Hand(s, player).TokenIds = tokens;
    }
    static void Boot(GameState s, string player)
    {
        Fish(s).Supply = Fish(s).Supply.Where(t => t != FishingRules.OldBootId).ToArray(); Fish(s).OldBootPlayerId = player;
    }
    static void Top(GameState s, params string[] tokens) => Fish(s).Supply = tokens.Concat(Fish(s).Supply.Except(tokens)).ToArray();
    static BaseGameSession FixtureFish(Action<GameState>? setup = null, bool harbor = false, int? target = null)
    {
        var game = NewFish(harbor: harbor, target: target); var s = game.GetAuthoritativeStateForTesting();
        s.ActivePlayerId = "P1"; s.Phase = GamePhase.Action; s.Turn = 9; s.SetupPlacementIndex = 8;
        setup?.Invoke(s); Balance(s); SetState(game, s); return game;
    }
    static Command FishCommand(BaseGameSession g, CommandKind kind, string player = "P1")
    {
        var c = Cmd(kind, player); c.ExpectedEventSequence = g.GetAuthoritativeStateForTesting().EventSequence; return c;
    }
    static Command Spend(BaseGameSession g, FishAction action, params string[] tokens)
    {
        var c = FishCommand(g, CommandKind.SpendFish); c.FishAction = action; c.FishTokenIds = tokens; return c;
    }
    static Command Replacement(BaseGameSession g, string? token = null)
    {
        var f = Fish(g.GetAuthoritativeStateForTesting()); var c = FishCommand(g, CommandKind.ResolveFishReplacement, f.PendingPlayerId);
        c.DecisionId = f.PendingDecisionId; c.TargetId = token!; return c;
    }
    static void ProduceFishFixture(BaseGameSession g, int number)
    {
        var s = g.GetAuthoritativeStateForTesting(); Invoke(g, "ProduceFish", s, number); SetState(g, s);
    }
    static string Json(object value) => JsonSerializer.Serialize(value);

    [Theory]
    [InlineData(3, false)] [InlineData(4, false)] [InlineData(3, true)] [InlineData(4, true)]
    public void StandardMapPoolTargetsAndDeterminism(int count, bool harbor)
    {
        var expected = new[] { "V04,V08,V12", "V39,V44,V48", "V52,V49,V53", "V43,V38,V33", "V27,V21,V16", "V02,V05,V01" };
        for (uint seed = 1; seed <= 40; seed++)
        {
            var g = NewFish(count, seed, harbor); var s = g.GetAuthoritativeStateForTesting(); var f = Fish(s); var v = g.GetPlayerView("P1");
            Assert.Equal(30, f.Supply.Length); Assert.Equal(55, f.Supply.Sum(FishingRules.Value));
            Assert.Equal(11, f.Supply.Count(t => FishingRules.Value(t) == 1)); Assert.Equal(10, f.Supply.Count(t => FishingRules.Value(t) == 2)); Assert.Equal(8, f.Supply.Count(t => FishingRules.Value(t) == 3));
            Assert.Null(s.RobberTileId); Assert.Equal(harbor ? 11 : 10, v.TargetVictoryPoints);
            Assert.Equal(expected, f.Grounds.Select(p => string.Join(",", p.VertexIds)));
            Assert.Equal(new[] { 4, 5, 6, 8, 9, 10 }, f.Grounds.Select(p => p.Number).Order());
            Assert.Equal(18, f.Grounds.SelectMany(p => p.VertexIds).Distinct().Count());
            var lake = v.Board.Tiles.Single(t => t.Resource == "lake"); Assert.Null(lake.Number);
            Assert.InRange(Math.Max(Math.Abs(lake.Q), Math.Max(Math.Abs(lake.R), Math.Abs(lake.Q + lake.R))), 0, 1);
            Assert.Equal(new[] { 2, 3, 11, 12 }, v.TradeModules.Fishing.LakeNumbers);
            Assert.Equal(g.Save(), NewFish(count, seed, harbor).Save()); Assert.Equal(g.Save(), BaseGameSession.Load(AcceptanceFixture.Json, g.Save()).Save());
            g.AssertInvariants();
        }
        Assert.Equal(16, NewFish(count, harbor: harbor, target: 16).GetPlayerView("P1").TargetVictoryPoints);
    }

    [Fact]
    public void F01SecondSetupOnlyOneTokenAndBootDoesNotRedraw()
    {
        var g = NewFish();
        for (int i = 0; i < 8; i++)
        {
            var s = g.GetAuthoritativeStateForTesting(); var v = g.GetPlayerView(s.ActivePlayerId);
            var fishVertices = v.TradeModules.Fishing.Grounds.SelectMany(x => x.VertexIds).Concat(v.Board.Tiles.Single(t => t.Resource == "lake").Vertices);
            var target = v.LegalVertexIds.First(fishVertices.Contains);
            var before = Fish(s).Supply.Length;
            Accepted(g, Cmd(CommandKind.SetupSettlement, s.ActivePlayerId, target));
            Assert.Equal(before - (i >= 4 ? 1 : 0), Fish(g.GetAuthoritativeStateForTesting()).Supply.Length);
            Accepted(g, Cmd(CommandKind.SetupRoad, s.ActivePlayerId, g.GetPlayerView(s.ActivePlayerId).LegalEdgeIds.First()));
        }
        Assert.Equal(g.Save(), BaseGameSession.Load(AcceptanceFixture.Json, g.Save()).Save());
        var boot = NewFish(); var state = boot.GetAuthoritativeStateForTesting(); state.SetupPlacementIndex = 4; Top(state, "BOOT"); SetState(boot, state);
        var vertex = Fish(state).Grounds[0].VertexIds[0];
        Accepted(boot, Cmd(CommandKind.SetupSettlement, state.ActivePlayerId, vertex));
        var after = boot.GetAuthoritativeStateForTesting(); Assert.Equal(state.ActivePlayerId, Fish(after).OldBootPlayerId);
        Assert.Empty(Hand(after, state.ActivePlayerId).TokenIds); Assert.Equal(29, Fish(after).Supply.Length);
    }

    [Fact]
    public void F02ProductionLocksClockwiseOrderAndCityDrawsTwice()
    {
        // 同一渔场两端不相邻：P2村、P3城；另用湖泊的三个相间顶点核对P2/P3/P1顺序。
        var g = NewFish(3); var s = g.GetAuthoritativeStateForTesting(); var ground = Fish(s).Grounds[0];
        s.ActivePlayerId = "P2"; s.Turn = 1; s.Phase = GamePhase.Action; s.SetupPlacementIndex = 6;
        s.Settlements = new[] { Piece(ground.VertexIds[0], "P2") }; s.Cities = new[] { Piece(ground.VertexIds[2], "P3") };
        Top(s, "F01", "F12", "F22", "F02"); Balance(s); SetState(g, s); ProduceFishFixture(g, ground.Number);
        s = g.GetAuthoritativeStateForTesting(); Assert.Equal(new[] { "F01" }, Hand(s, "P2").TokenIds); Assert.Equal(new[] { "F12", "F22" }, Hand(s, "P3").TokenIds); Assert.Equal("F02", Fish(s).Supply[0]);
        var lake = g.GetPlayerView("P1").Board.Tiles.Single(t => t.Resource == "lake");
        g = NewFish(3); s = g.GetAuthoritativeStateForTesting(); s.ActivePlayerId = "P2"; s.Turn = 1; s.Phase = GamePhase.Action; s.SetupPlacementIndex = 6;
        s.Settlements = new[] { Piece(lake.Vertices[0], "P2"), Piece(lake.Vertices[4], "P1") }; s.Cities = new[] { Piece(lake.Vertices[2], "P3") };
        Top(s, "F01", "F12", "F22", "F02"); Balance(s); SetState(g, s); ProduceFishFixture(g, 2);
        s = g.GetAuthoritativeStateForTesting(); Assert.Equal(new[] { "F01" }, Hand(s, "P2").TokenIds); Assert.Equal(new[] { "F12", "F22" }, Hand(s, "P3").TokenIds); Assert.Equal(new[] { "F02" }, Hand(s, "P1").TokenIds);
        // 从城市第一抽旧靴，第二抽仍执行。
        g = FixtureFish(st => { var gr = Fish(st).Grounds[0]; st.Cities = new[] { Piece(gr.VertexIds[0]) }; Give(st, "P1", "F01", "F02", "F04", "F05", "F06", "F07"); Top(st, "BOOT", "F23", "F03"); });
        ProduceFishFixture(g, Fish(g.GetAuthoritativeStateForTesting()).Grounds[0].Number); s = g.GetAuthoritativeStateForTesting();
        Assert.Equal("P1", Fish(s).OldBootPlayerId); Assert.Equal(7, Hand(s).TokenIds.Length); Assert.Contains("F23", Hand(s).TokenIds); Assert.False(Hand(s).ReplacementUsed); Assert.Equal("F03", Fish(s).Supply[0]);
    }

    [Fact]
    public void F03FullHandReplacementBootStopsAndRejectingPreservesAllowance()
    {
        BaseGameSession Full() => FixtureFish(s =>
        {
            Give(s, "P2", "F01", "F02", "F03", "F04", "F05", "F06", "F07");
            s.Cities = new[] { Piece(Fish(s).Grounds[0].VertexIds[0], "P2") }; Top(s, "BOOT", "F22");
        });
        var g = Full(); var number = Fish(g.GetAuthoritativeStateForTesting()).Grounds[0].Number; ProduceFishFixture(g, number);
        Assert.Equal(GamePhase.FishReplacement, g.GetPlayerView("P1").Phase); Assert.Equal("P2", g.GetPlayerView("P1").PendingDecision.PlayerId);
        Rejected(g, Cmd(CommandKind.EndTurn)); var wrong = Replacement(g, "F01"); wrong.PlayerId = "P1"; Rejected(g, wrong);
        var invalid = Replacement(g, "BOOT"); Rejected(g, invalid); invalid = Replacement(g, "F01"); invalid.DecisionId = "old"; Rejected(g, invalid);
        Rejected(g, Spend(g, FishAction.TakeResource, "F01", "F02", "F03", "F04"));
        var command = Replacement(g, "F01"); var beforePreview = g.Save(); Assert.True(g.Preview(command).Success); Assert.Equal(beforePreview, g.Save());
        Accepted(g, command); var s = g.GetAuthoritativeStateForTesting(); Assert.Equal(6, Hand(s, "P2").TokenIds.Length);
        Assert.Equal("P2", Fish(s).OldBootPlayerId); Assert.True(Hand(s, "P2").ReplacementUsed); Assert.Equal("F22", Fish(s).Supply[0]); Assert.Equal(GamePhase.Action, s.Phase);
        Assert.True(g.Execute(command).IsDuplicate); Assert.Equal(Json(s), Json(g.GetAuthoritativeStateForTesting()));
        g = Full(); ProduceFishFixture(g, number); Accepted(g, Replacement(g)); Assert.False(Hand(g.GetAuthoritativeStateForTesting(), "P2").ReplacementUsed);
        ProduceFishFixture(g, number); Assert.Equal(GamePhase.FishReplacement, g.GetPlayerView("P1").Phase);
        Accepted(g, Replacement(g, "F01")); ProduceFishFixture(g, number); // 六枚先补一枚，已用换抽，仍停止。
        Assert.Equal(GamePhase.Action, g.GetPlayerView("P1").Phase); Assert.Equal(7, Hand(g.GetAuthoritativeStateForTesting(), "P2").TokenIds.Length);
        Accepted(g, Cmd(CommandKind.EndTurn)); Assert.False(Hand(g.GetAuthoritativeStateForTesting(), "P2").ReplacementUsed);
    }

    [Fact]
    public void F06RefillAndBothPoolsEmptyNeverCreateTokens()
    {
        var g = FixtureFish(s =>
        {
            s.Cities = new[] { Piece(Fish(s).Grounds[0].VertexIds[0]) };
            var fish = Fish(s); fish.Discard = fish.Supply.Where(t => t != "BOOT").ToArray(); fish.Supply = new[] { "BOOT" };
        });
        var number = Fish(g.GetAuthoritativeStateForTesting()).Grounds[0].Number;
        var draws = g.GetAuthoritativeStateForTesting().Random.Draws; ProduceFishFixture(g, number);
        var after = g.GetAuthoritativeStateForTesting(); Assert.Equal("P1", Fish(after).OldBootPlayerId); Assert.Single(Hand(after).TokenIds);
        Assert.Empty(Fish(after).Discard); Assert.Equal(28, Fish(after).Supply.Length); Assert.True(after.Random.Draws > draws);
        // 三十枚组件中最多持有28枚鱼，故实际基础局双池空不可达；共用队列仍应有界停止。
        var empty = new FishingState { Supply = Array.Empty<string>(), Holdings = new[] { new FishHolding { PlayerId = "P1" } } };
        typeof(FishingRules).GetMethod("StartDraw", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { empty, new[] { new FishDrawClaim { PlayerId = "P1", Count = 2 } }, (Action<string[]>)(_ => throw new Exception("不应洗牌")) });
        Assert.Empty(empty.Queue); Assert.Empty(empty.Holdings[0].TokenIds); Assert.Null(empty.PendingPlayerId);
    }

    [Theory]
    [InlineData(2)] [InlineData(3)] [InlineData(11)] [InlineData(12)]
    public void F11LakeProducesAndRobberBlocksAllFourNumbers(int number)
    {
        var g = NewFish(); var lake = g.GetPlayerView("P1").Board.Tiles.Single(t => t.Resource == "lake");
        g = FixtureFish(s => { s.Settlements = new[] { Piece(lake.Vertices[0]) }; Top(s, "F01"); });
        ProduceFishFixture(g, number); Assert.Equal(new[] { "F01" }, Hand(g.GetAuthoritativeStateForTesting()).TokenIds);
        var s = g.GetAuthoritativeStateForTesting(); s.RobberTileId = lake.Id; SetState(g, s); var before = g.Save(); ProduceFishFixture(g, number); Assert.Equal(before, g.Save());
        s = g.GetAuthoritativeStateForTesting(); s.Phase = GamePhase.RobberMove; s.PendingDecision = new PendingDecision { Kind = "Robber", PlayerId = "P1", ReturnPhase = GamePhase.Action }; SetState(g, s);
        Rejected(g, Cmd(CommandKind.MoveRobber, target: Fish(s).Grounds[0].Id));
        Accepted(g, Cmd(CommandKind.MoveRobber, target: g.GetPlayerView("P1").Board.Tiles.First(t => t.Id != lake.Id).Id));
    }

    [Theory]
    [InlineData(GamePhase.Action)] [InlineData(GamePhase.ProductionAwaitRoll)]
    public void F07F09F10FiveActionsAreAtomicIdempotentAndPreRollLegal(GamePhase phase)
    {
        foreach (var action in Enum.GetValues<FishAction>())
        {
            var g = FixtureFish(s =>
            {
                s.Phase = phase; Give(s, "P1", "F22", "F23", "F24"); Player(s, "P2").Resources.Wood = 1;
                s.RobberTileId = Fish(s).LakeTileId; s.Settlements = new[] { Piece("V01") };
            });
            var c = Spend(g, action, action is FishAction.RemoveRobber or FishAction.StealResource ? new[] { "F22" } : action == FishAction.BuyDevelopmentCard ? new[] { "F22", "F23", "F24" } : new[] { "F22", "F23" });
            c.OtherPlayerId = "P2"; c.Resource = Resource.Ore;
            c.TargetId = g.GetPlayerView("P1").Board.Edges.First(e => e.Vertices.Contains("V01")).Id;
            var before = g.Save(); Assert.True(g.Preview(c).Success); Assert.Equal(before, g.Save()); Accepted(g, c);
            var s = g.GetAuthoritativeStateForTesting(); Assert.Equal(phase, s.Phase); Assert.Equal(c.FishTokenIds, Fish(s).Discard); Assert.Equal(3 - c.FishTokenIds.Length, Hand(s).TokenIds.Length);
            switch (action)
            {
                case FishAction.RemoveRobber: Assert.Null(s.RobberTileId); Assert.Equal(1, Player(s, "P2").Resources.Wood); break;
                case FishAction.StealResource: Assert.Equal(1, Player(s).Resources.Wood); Assert.Equal(0, Player(s, "P2").Resources.Total); break;
                case FishAction.TakeResource: Assert.Equal(1, Player(s).Resources.Ore); break;
                case FishAction.BuildRoad: Assert.Single(s.Roads); Assert.Equal(14, Player(s).Pieces.Roads); Assert.Equal(0, Player(s).Resources.Total); break;
                case FishAction.BuyDevelopmentCard: Assert.Single(Player(s).DevelopmentCards); Assert.Equal(9, Player(s).DevelopmentCards[0].BoughtTurn); Rejected(g, Cmd(CommandKind.PlayKnight)); break;
            }
            before = g.Save(); Assert.True(g.Execute(c).IsDuplicate); Assert.Equal(before, g.Save());
            c.Id += "-stale"; Rejected(g, c); c.ExpectedEventSequence = s.EventSequence; Rejected(g, c);
        }
    }

    [Fact]
    public void F08InvalidPaymentsAndTargetsPreserveEntireState()
    {
        BaseGameSession Rich() => FixtureFish(s => { Give(s, "P1", "F22", "F23", "F24"); Give(s, "P2", "F25"); s.RobberTileId = Fish(s).LakeTileId; });
        foreach (var tokens in new[] { Array.Empty<string>(), new[] { "F22", "F22" }, new[] { "F22", "F25" }, new[] { "BOOT" }, new[] { "F22" }, new[] { "nonsense" } })
        {
            var g = Rich(); Rejected(g, Spend(g, FishAction.TakeResource, tokens));
        }
        var game = Rich(); var command = Spend(game, (FishAction)100, "F22", "F23", "F24"); Rejected(game, command);
        command = Spend(game, FishAction.BuildRoad, "F22", "F23"); command.TargetId = "E01"; Rejected(game, command); // 无连接
        command.TargetId = "bad"; Rejected(game, command);
        command = Spend(game, FishAction.TakeResource, "F22", "F23"); command.Resource = (Resource)999; Rejected(game, command);
        command = Spend(game, FishAction.StealResource, "F22"); foreach (var id in new[] { "P1", "P2", "unknown" }) { command.OtherPlayerId = id; Rejected(game, command); }
        command = Spend(game, FishAction.RemoveRobber, "F22"); command.PlayerId = "P2"; Rejected(game, command);
        command.PlayerId = "P1"; command.ExpectedEventSequence = null; Rejected(game, command);
        game = FixtureFish(s => { Give(s, "P1", "F22", "F23", "F24"); Player(s, "P2").Resources.Ore = 19; });
        command = Spend(game, FishAction.TakeResource, "F22", "F23"); command.Resource = Resource.Ore; Rejected(game, command);
        Rejected(game, Spend(game, FishAction.RemoveRobber, "F22"));
        game = FixtureFish(s => { Give(s, "P1", "F22", "F23", "F24"); Player(s, "P2").DevelopmentCards = Deck().Select(k => new DevelopmentCard { Kind = k, BoughtTurn = 1 }).ToArray(); });
        Rejected(game, Spend(game, FishAction.BuyDevelopmentCard, "F22", "F23", "F24"));
        game = FixtureFish(s => { Give(s, "P1", "F22", "F23"); s.Phase = GamePhase.RobberMove; s.PendingDecision = new PendingDecision { Kind = "Robber", PlayerId = "P1", ReturnPhase = GamePhase.Action }; });
        Rejected(game, Spend(game, FishAction.TakeResource, "F22", "F23"));
        game = New(); command = FishCommand(game, CommandKind.SpendFish, game.GetPlayerView("P1").ActivePlayerId); command.FishAction = FishAction.TakeResource; command.FishTokenIds = new[] { "F22", "F23" }; Rejected(game, command);
    }

    [Fact]
    public void F12BootUsesPublicPointsAndPersonalVictoryWindow()
    {
        var g = FixtureFish(s =>
        {
            Boot(s, "P1"); s.Settlements = new[] { Piece("V01"), Piece("V22", "P2") };
            Card(s, DevelopmentCardKind.VictoryPoint); Card(s, DevelopmentCardKind.VictoryPoint);
        });
        var before = g.GetPlayerView("P1"); var c = FishCommand(g, CommandKind.PassOldBoot); c.OtherPlayerId = "P3"; Rejected(g, c);
        c.OtherPlayerId = "P2"; Accepted(g, c); var v = g.GetPlayerView("P1");
        Assert.Equal(before.OwnVictoryPoints, v.OwnVictoryPoints); Assert.Equal(before.Players.Select(p => p.VictoryPoints), v.Players.Select(p => p.VictoryPoints));
        Assert.Equal(10, v.TradeModules.Fishing.OwnVictoryTarget); Assert.Equal(11, g.GetPlayerView("P2").TradeModules.Fishing.OwnVictoryTarget);
        Rejected(g, FishCommand(g, CommandKind.PassOldBoot, "P2"));
        // 五座村庄和五张隐藏胜利点：持靴10分不胜，转给公开分相同者才在本人窗口获胜。
        g = FixtureFish(s =>
        {
            Boot(s, "P1"); s.Settlements = new[] { "V01", "V08", "V17", "V28", "V39" }.Select(x => Piece(x)).Concat(new[] { "V03", "V11", "V21", "V33", "V43" }.Select(x => Piece(x, "P2"))).ToArray();
            for (int i = 0; i < 5; i++) Card(s, DevelopmentCardKind.VictoryPoint);
        });
        Rejected(g, Cmd(CommandKind.DeclareVictory)); c = FishCommand(g, CommandKind.PassOldBoot); c.OtherPlayerId = "P2"; Accepted(g, c);
        Assert.Equal("P1", g.GetPlayerView("P1").WinnerPlayerId);
        Assert.Equal(21, FishingRules.VictoryTarget(new FishingState { OldBootPlayerId = "P1" }, "P1", 20));
    }

    [Fact]
    public void N02OpponentTokensPoolAndRandomAreAbsentFromViewsAiAndTheftHistory()
    {
        var g = FixtureFish(s => { Give(s, "P1", "F22"); Give(s, "P2", "F01"); Player(s, "P2").Resources.Ore = 1; });
        var before = Json(g.GetPlayerView("P1")); var s = g.GetAuthoritativeStateForTesting();
        Hand(s, "P2").TokenIds = new[] { "F23" }; Fish(s).Supply = Fish(s).Supply.Select(t => t == "F23" ? "F01" : t).ToArray(); SetState(g, s);
        Assert.Equal(before, Json(g.GetPlayerView("P1"))); Assert.Equal(Json(g.GetPlayerView("P1").TradeModules), Json(g.GetAiPlayerView("P1").TradeModules));
        Assert.DoesNotContain("F23", before); Assert.DoesNotContain("Random", before); Assert.DoesNotContain("QueueIndex", before);
        var c = Spend(g, FishAction.StealResource, "F22"); c.OtherPlayerId = "P2"; Accepted(g, c);
        Assert.All(g.GetHistory("P3").Single().Changes, change => { Assert.Equal("resource-count", change.Category); Assert.Equal(-1, change.GoodIndex); Assert.NotEqual("矿石", change.Name); });
        Assert.Contains(g.GetHistory("P1").Single().Changes, change => change.Name == "矿石");
        Assert.Contains(g.GetHistory("P2").Single().Changes, change => change.Name == "矿石");
        Assert.Equal(new[] { 3 }, g.GetPlayerView("P3").TradeModules.Fishing.DiscardValues); Assert.Empty(g.GetPlayerView("P3").TradeModules.Fishing.OwnTokens);
        // 修改分离视图与权威测试副本均不影响会话。
        before = g.Save(); var view = g.GetPlayerView("P1"); view.TradeModules.Fishing.Grounds[0].VertexIds[0] = "bad";
        Fish(g.GetAuthoritativeStateForTesting()).Supply[0] = "bad"; Assert.Equal(before, g.Save());
    }

    [Fact]
    public void N03LegacyModulesDisabledAndUnsupportedModesAreExplicit()
    {
        var legacy = New(); var off = BaseGameSession.Create(AcceptanceFixture.Json, 4, 42, tradeModules: new TradeModuleOptions());
        Assert.Equal(legacy.Save(), off.Save()); Assert.DoesNotContain("Fish", off.Save()); Assert.Equal(off.Save(), BaseGameSession.Load(AcceptanceFixture.Json, off.Save()).Save());
        var harbor = BaseGameSession.Create(AcceptanceFixture.Json, tradeModules: new TradeModuleOptions { Harbormaster = true });
        Assert.Contains(TradeModuleState.CurrentVersion, harbor.Save()); Assert.DoesNotContain("\"Fishing\"", harbor.Save());
        Assert.Equal(harbor.Save(), BaseGameSession.Load(AcceptanceFixture.Json, harbor.Save()).Save());
        foreach (var scenario in new[] { "heading-for-new-shores", "pirate-islands" })
            Assert.Throws<ArgumentException>(() => HarbormasterRules.Create(new TradeModuleOptions { Fishing = true }, scenario));
        var valid = NewFish().Save();
        foreach (var field in new[] { "Version", "MapVersion", "LakeTileId" })
        {
            var bad = JsonNode.Parse(valid)!; bad["TradeModules"]!["Fishing"]![field] = "bad";
            Assert.Throws<InvalidDataException>(() => BaseGameSession.Load(AcceptanceFixture.Json, bad.ToJsonString()));
        }
        var missing = JsonNode.Parse(valid)!; missing["TradeModules"]!.AsObject().Remove("Fishing"); Assert.Throws<InvalidDataException>(() => BaseGameSession.Load(AcceptanceFixture.Json, missing.ToJsonString()));
    }

    [Fact]
    public void N04InvariantsRejectDuplicateTokensBootDiscardAndCorruptCursor()
    {
        foreach (var mutation in new Action<GameState>[] {
            s => Fish(s).Supply[0] = Fish(s).Supply[1],
            s => { Give(s, "P1", "F01","F02","F03","F04","F05","F06","F07","F08"); },
            s => { Fish(s).Supply = Fish(s).Supply.Where(t => t != "BOOT").ToArray(); Fish(s).Discard = new[] { "BOOT" }; },
            s => Fish(s).QueueIndex = 1,
            s => Fish(s).Grounds[0].VertexIds[0] = "V54",
            s => Fish(s).OldBootPlayerId = "P9" })
        {
            var g = NewFish(); var s = g.GetAuthoritativeStateForTesting(); mutation(s);
            typeof(BaseGameSession).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(g, s);
            Assert.Throws<InvalidDataException>(() => g.AssertInvariants());
        }
    }
    [Theory]
    [InlineData(3, false)] [InlineData(4, false)] [InlineData(3, true)] [InlineData(4, true)]
    public void N01ViewOnlyCompleteGameRestoresPendingDrawsAndPayments(int players, bool harbor)
    {
        var g = NewFish(players, 97, harbor); var driver = new M2ViewOnlyDriver();
        int commands = 0, replacements = 0, spends = 0; bool restoredPending = false, restoredPayment = false;
        var kinds = new HashSet<FishAction>();
        while (g.GetPlayerView("P1").Phase != GamePhase.Finished && commands < 7000)
        {
            var publicView = g.GetPlayerView("P1");
            var actor = publicView.Phase == GamePhase.FishReplacement ? publicView.PendingDecision.PlayerId : publicView.Phase == GamePhase.Discard ? publicView.Discards[0].PlayerId : publicView.ActivePlayerId;
            var view = g.GetPlayerView(actor); var fish = view.TradeModules.Fishing;
            Command c;
            // 仅测试驱动使用所属PlayerView；产品捕鱼AI在F3接入。
            if (view.Phase == GamePhase.FishReplacement)
            {
                c = new Command { PlayerId = actor, Kind = CommandKind.ResolveFishReplacement, DecisionId = fish.PendingDecisionId, ExpectedEventSequence = fish.StateSequence,
                    TargetId = replacements++ % 2 == 0 ? fish.OwnTokens.OrderBy(t => t.Value).First().Id : null! };
                if (!restoredPending)
                {
                    var saved = g.Save(); g = BaseGameSession.Load(AcceptanceFixture.Json, saved); Assert.Equal(saved, g.Save()); restoredPending = true;
                    // 恢复后预览不会推进队列，重复指令也不会重抽。
                    c.Id = $"f2-{commands++}"; var resumed = BaseGameSession.Load(AcceptanceFixture.Json, saved);
                    Assert.True(g.Preview(c).Success); Assert.Equal(saved, g.Save()); Accepted(g, c); Accepted(resumed, c);
                    Assert.Equal(g.Save(), resumed.Save()); Assert.True(resumed.Execute(c).IsDuplicate); Assert.Equal(g.Save(), resumed.Save()); continue;
                }
            }
            else if (view.Phase == GamePhase.SetupSettlement)
            {
                var shore = fish.Grounds.SelectMany(f => f.VertexIds).ToHashSet();
                var vertex = view.LegalVertexIds.OrderByDescending(v => (shore.Contains(v) ? 12 : 0) + view.Board.Tiles.Where(t => t.Vertices.Contains(v) && t.Number.HasValue).Sum(t => 6 - Math.Abs(7 - t.Number!.Value))).ThenBy(v => v).First();
                c = new Command { PlayerId = actor, Kind = CommandKind.SetupSettlement, TargetId = vertex };
            }
            else if (view.Phase == GamePhase.Action && fish.OldBootPlayerId == actor && view.Players.Any(p => p.Id != actor && p.VictoryPoints >= view.Players.Single(p => p.Id == actor).VictoryPoints))
            {
                c = new Command { PlayerId = actor, Kind = CommandKind.PassOldBoot, ExpectedEventSequence = fish.StateSequence,
                    OtherPlayerId = view.Players.Where(p => p.Id != actor && p.VictoryPoints >= view.Players.Single(p => p.Id == actor).VictoryPoints).OrderByDescending(p => p.VictoryPoints).First().Id };
            }
            else if (view.Phase == GamePhase.Action && fish.OwnTokens.Sum(t => t.Value) >= 7 && view.DevelopmentDeckCount > 0 && restoredPending)
            {
                var chosen = new List<string>(); int value = 0;
                foreach (var t in fish.OwnTokens.OrderByDescending(t => t.Value)) { chosen.Add(t.Id); value += t.Value; if (value >= 7) break; }
                c = new Command { PlayerId = actor, Kind = CommandKind.SpendFish, FishAction = FishAction.BuyDevelopmentCard, FishTokenIds = chosen.ToArray(), ExpectedEventSequence = fish.StateSequence }; spends++; kinds.Add(c.FishAction.Value);
            }
            else c = driver.Decide(view);
            c.Id = $"f2-{commands++}"; Accepted(g, c);
            if (c.Kind == CommandKind.SpendFish && !restoredPayment)
            {
                var saved = g.Save(); g = BaseGameSession.Load(AcceptanceFixture.Json, saved); Assert.Equal(saved, g.Save()); Assert.True(g.Execute(c).IsDuplicate); Assert.Equal(saved, g.Save()); restoredPayment = true;
            }
        }
        var end = g.GetPlayerView("P1"); Assert.Equal(GamePhase.Finished, end.Phase);
        Assert.True(replacements > 0, $"未覆盖换抽：回合{end.Turn}，支付{spends}"); Assert.True(spends > 0, $"未覆盖支付：回合{end.Turn}，换抽{replacements}"); Assert.True(restoredPending); Assert.True(restoredPayment);
        Assert.Equal(g.Save(), BaseGameSession.Load(AcceptanceFixture.Json, g.Save()).Save());
        output.WriteLine($"人数={players}，海港={harbor}，胜者={end.WinnerPlayerId}，回合={end.Turn}，命令={commands}，换抽选择={replacements}，鱼支付={spends}，待决与支付恢复通过");
    }

    [Fact]
    public void F06FiniteSupplySettlesInOrderAndStopsWithoutRollback()
    {
        // 抽取算法的独立局面；仅提供F0指定三枚池，不冒充满足30枚守恒的整局存档。
        var f = new FishingState { Supply = new[] { "F01" }, Discard = new[] { "F12", "F22" }, Holdings = new[] { new FishHolding { PlayerId = "P1" }, new FishHolding { PlayerId = "P2" } } };
        int shuffles = 0;
        typeof(FishingRules).GetMethod("StartDraw", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { f,
            new[] { new FishDrawClaim { PlayerId = "P1", Count = 2 }, new FishDrawClaim { PlayerId = "P2", Count = 2 } }, (Action<string[]>)(_ => shuffles++) });
        Assert.Equal(new[] { "F01", "F12" }, f.Holdings[0].TokenIds); Assert.Equal(new[] { "F22" }, f.Holdings[1].TokenIds);
        Assert.Equal(1, shuffles); Assert.Empty(f.Supply); Assert.Empty(f.Discard); Assert.Empty(f.Queue);
    }

    [Fact]
    public void FishAndBootAreNotResourcesForSevenTradeOrRobbery()
    {
        var g = FixtureFish(s =>
        {
            s.Phase = GamePhase.ProductionAwaitRoll; s.Random.State = 256; Player(s).Resources.Wood = 7; Player(s, "P2").Resources.Brick = 9;
            Give(s, "P1", "F01", "F02", "F03", "F04", "F05", "F06", "F07"); Give(s, "P3", "F22"); Boot(s, "P1");
            s.Settlements = new[] { Piece("V01", "P3") };
        });
        Accepted(g, Cmd(CommandKind.RollDice)); var view = g.GetPlayerView("P1"); Assert.Equal(7, view.LastDice1 + view.LastDice2);
        Assert.Equal("P2", Assert.Single(view.Discards).PlayerId); Assert.Equal(4, view.Discards[0].Amount);
        Rejected(g, Spend(g, FishAction.TakeResource, "F01", "F02", "F03", "F04"));
        var discard = Cmd(CommandKind.DiscardResources, "P2"); discard.Resources = Bag(brick: 4); Accepted(g, discard);
        Accepted(g, Cmd(CommandKind.MoveRobber, target: "T01")); var steal = Cmd(CommandKind.StealResource); steal.OtherPlayerId = "P3"; Accepted(g, steal);
        Assert.Equal(new[] { "F22" }, Hand(g.GetAuthoritativeStateForTesting(), "P3").TokenIds); Assert.Equal(7, g.GetPlayerView("P1").OwnResources.Total);
        var offer = Cmd(CommandKind.ProposeTrade); offer.OtherPlayerId = "P3"; offer.Give = Bag(wood: 1); offer.Receive = Bag(ore: 1);
        Accepted(g, offer); Assert.Null(g.GetPlayerView("P1").TradeOffer); Assert.Equal(7, g.GetPlayerView("P1").OwnResources.Total);
        Assert.Equal("P1", Fish(g.GetAuthoritativeStateForTesting()).OldBootPlayerId);
    }

}
