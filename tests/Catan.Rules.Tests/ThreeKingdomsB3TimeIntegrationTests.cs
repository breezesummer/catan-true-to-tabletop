using Catan.Core;
using Catan.Core.ThreeKingdoms;
using System.Runtime.Serialization.Json;
using System.Text;
using M4 = Catan.Core.M4;
using M5 = Catan.Core.M5;
using Xunit;

namespace Catan.Rules.Tests;

/// <summary>独立原生状态夹具，经正式 Execute 验证真实支付、弃牌与宿主回合；不冒充从建局重放的证据。</summary>
public sealed class ThreeKingdomsB3TimeIntegrationTests
{
    private static ThreeKingdomsState Generals(params string[] generals)
    {
        var tk = ThreeKingdomsEngine.Create(new[] { "P1", "P2", "P3", "P4" }, 42, _ => 0, true, GeneralCatalog.B3PackageVersion);
        while (!tk.OpeningComplete)
        {
            var d = tk.PendingDecision;
            if (d.Kind == ThreeKingdomsDecisionKind.SelectOpening) ThreeKingdomsEngine.ChooseGenerals(tk, d.PlayerId, d.CandidateIds.Take(2).ToArray(), d.DecisionId, tk.WindowId, _ => 0);
            else ThreeKingdomsEngine.DeployGeneral(tk, d.PlayerId, d.CandidateIds[0], d.DecisionId, tk.WindowId, _ => 0);
        }
        ThreeKingdomsEngine.BeginNormalTurns(tk);
        ThreeKingdomsEngine.AdvancePhase(tk, "P1", tk.WindowId); ThreeKingdomsEngine.AdvancePhase(tk, "P1", tk.WindowId);
        ThreeKingdomsEngine.MarkDrawPhase(tk); ThreeKingdomsEngine.AdvancePhase(tk, "P1", tk.WindowId);
        for (int i = 0; i < generals.Length; i++)
        {
            var current = tk.Generals.Single(g => g.Region == GeneralRegion.Active && g.OwnerPlayerId == "P" + (i + 1));
            var wanted = tk.Generals.Single(g => g.GeneralId == generals[i]);
            (current.GeneralId, wanted.GeneralId) = (wanted.GeneralId, current.GeneralId);
            (current.InstanceId, wanted.InstanceId) = (wanted.InstanceId, current.InstanceId);
        }
        return tk;
    }
    private static M4.CitiesKnightsGameSession Fixture(string[] generals, Action<M4.GameState>? configure = null) => M4CoreHarness.Fixture(s =>
    {
        s.RulesVersion = M4.CitiesKnightsGameSession.ThreeKingdomsB3RulesVersion; s.ThreeKingdoms = Generals(generals); configure?.Invoke(s);
    });
    private static M4.Command Cmd(M4.CitiesKnightsGameSession game, M4.CommandKind kind, string player = "P1") => new()
    { Id = ThreeKingdomsTestGame.NextId(), Kind = kind, PlayerId = player, WindowId = game.GetPlayerView(player).ThreeKingdoms.WindowId };
    private static void Accept(M4.CitiesKnightsGameSession game, M4.Command command)
    { var result = game.Execute(command); Assert.True(result.Success, command.Kind + ": " + result.ErrorCode + ": " + result.Message); game.AssertInvariants(); }
    private static void Skill(M4.CitiesKnightsGameSession game, string player, Func<B2SkillAction, bool> select)
    {
        var command = Cmd(game, M4.CommandKind.UseGeneralSkill, player);
        command.SkillCommand = game.GetPlayerView(player).ThreeKingdoms.SkillActions.First(select).Command; Accept(game, command);
    }
    private static void Drain(M4.CitiesKnightsGameSession game, bool acceptOptional = false)
    {
        for (int guard = 0; guard < 100; guard++)
        {
            var d = game.GetPlayerView("P1").ThreeKingdoms.SkillDecision; if (d == null) return;
            var actions = game.GetPlayerView(d.PlayerId).ThreeKingdoms.SkillActions;
            var selected = actions.FirstOrDefault(a => a.Command.Option == (acceptOptional ? "accept" : "skip")) ?? actions.First();
            Skill(game, d.PlayerId, a => ReferenceEquals(a, selected) || a.Command.Option == selected.Command.Option && a.Command.CardType == selected.Command.CardType && a.Command.Counts.SequenceEqual(selected.Command.Counts));
        }
        Assert.Fail("原生技能队列超过安全步数。");
    }
    private static void ReachPlay(M4.CitiesKnightsGameSession game)
    {
        for (int guard = 0; guard < 100; guard++)
        {
            Drain(game); var s = game.GetAuthoritativeStateForTesting(); if (s.ThreeKingdoms.Clock.Phase == ThreeKingdomsPhase.Play) return;
            Assert.Empty(s.Discards); Assert.Null(s.PendingDecision);
            Accept(game, Cmd(game, s.ThreeKingdoms.Clock.Phase == ThreeKingdomsPhase.Roll ? M4.CommandKind.RollDice : M4.CommandKind.AdvanceThreeKingdomsPhase, s.ActivePlayerId));
        }
        Assert.Fail("原生阶段未回到出牌。");
    }

    [Fact]
    public void RealSevenLocksAllAmountsAndGuzhengWaitsUntilLastDiscardBeforeSingleSongwei()
    {
        var game = Fixture(new[] { "tk-g21", "tk-g30", "tk-g29", "tk-g06" }, s =>
        {
            M4CoreHarness.Player(s).Resources.Wood = 8; M4CoreHarness.Player(s, "P2").Resources.Brick = 8;
            M4CoreHarness.Player(s, "P3").Resources.Wool = 8; M4CoreHarness.Player(s, "P4").Resources.Ore = 8;
            s.Phase = M4.GamePhase.ProductionAwaitRoll; s.ThreeKingdoms.Clock.Phase = ThreeKingdomsPhase.Roll; s.ThreeKingdoms.Clock.DrawPhaseTurnInstanceId = 0; s.AlchemyDice1 = 3; s.AlchemyDice2 = 4;
        });
        Accept(game, Cmd(game, M4.CommandKind.RollDice)); Drain(game);
        Assert.Equal(4, game.GetAuthoritativeStateForTesting().Discards.Length);
        Assert.All(game.GetAuthoritativeStateForTesting().Discards, d => Assert.Equal(4, d.Amount));
        var types = new[] { Resource.Wood, Resource.Brick, Resource.Wool, Resource.Ore };
        for (int i = 0; i < 4; i++)
        {
            var discard = Cmd(game, M4.CommandKind.DiscardResources, "P" + (i + 1)); discard.Resources = new ResourceBag(); discard.Resources[types[i]] = 4; Accept(game, discard);
            var state = game.GetAuthoritativeStateForTesting();
            if (i < 3)
            {
                Assert.Equal(4, M4CoreHarness.Player(state).Resources.Wood); Assert.Null(state.ThreeKingdoms.Skills.PendingDecision);
                Assert.Equal(4 * (i + 1), B2SkillEngine.EscrowCards(state.ThreeKingdoms).Sum()); Assert.All(state.Discards, d => Assert.Equal(4, d.Amount));
            }
        }
        var complete = game.GetAuthoritativeStateForTesting(); Assert.Empty(complete.Discards);
        Assert.Equal(20, M4CoreHarness.Player(complete).Resources.Total); Assert.Equal(4, M4CoreHarness.Player(complete, "P2").Resources.Total);
        Assert.Equal("P3", complete.ThreeKingdoms.Skills.PendingDecision.PlayerId); Assert.Equal("tk-g29-s2", complete.ThreeKingdoms.Skills.Frames[0].SkillId);
        Skill(game, "P3", a => a.Command.CardType == 5);
        Assert.Null(game.GetAuthoritativeStateForTesting().ThreeKingdoms.Skills.PendingDecision);
        Assert.Equal(1, M4CoreHarness.Player(game.GetAuthoritativeStateForTesting(), "P3").Commodities.Cloth);
        Assert.Empty(game.GetAuthoritativeStateForTesting().Discards); game.AssertInvariants();
    }

    [Fact]
    public void NativeLastWoodQianxunTriggersLianyingBeforeLuoyingCanIntercept()
    {
        var game = Fixture(new[] { "tk-g23", "tk-g30", "tk-g21", "tk-g29" }, s => M4CoreHarness.Player(s).Resources.Wood = 1);
        Skill(game, "P1", a => a.Command.Option == "discard");
        Assert.Equal("P1", game.GetPlayerView("P1").ThreeKingdoms.SkillDecision.PlayerId);
        Assert.Equal(0, M4CoreHarness.Player(game.GetAuthoritativeStateForTesting(), "P2").Resources.Total);
        Skill(game, "P1", a => a.Command.CardType == 1);
        Assert.Equal("P2", game.GetPlayerView("P1").ThreeKingdoms.SkillDecision.PlayerId);
        Assert.Equal(2, M4CoreHarness.Player(game.GetAuthoritativeStateForTesting()).Resources.Total);
        Skill(game, "P2", a => a.Command.Option == "luoying");
        Assert.Equal(1, M4CoreHarness.Player(game.GetAuthoritativeStateForTesting(), "P2").Resources.Wood); game.AssertInvariants();
    }

    [Fact]
    public void NativeRoadAndBankPaymentsShareLuoyingSourceQuotaButPlayerTradeIsNotIntercepted()
    {
        var board = M4CoreHarness.New().GetPlayerView("P1").Board; string vertex = board.Vertices[0].Id;
        string edge = board.Edges.First(e => e.Vertices.Contains(vertex)).Id;
        var game = Fixture(new[] { "tk-g21", "tk-g30", "tk-g29", "tk-g06" }, s =>
        {
            s.Settlements = new[] { M4CoreHarness.Piece(vertex) }; M4CoreHarness.Player(s).Resources = new ResourceBag { Wood = 6, Brick = 1 };
            M4CoreHarness.Player(s, "P3").Resources.Wool = 1;
        });
        // 先交易，确保落英额度仍为空，不能由“额度已满”掩盖错误截获。
        var offer = Cmd(game, M4.CommandKind.ProposeTrade); offer.OtherPlayerId = "P3"; offer.Give = new ResourceBag { Wood = 1 }; offer.Receive = new ResourceBag { Wool = 1 }; Accept(game, offer);
        Accept(game, Cmd(game, M4.CommandKind.AcceptTrade, "P3"));
        Assert.Equal(1, M4CoreHarness.Player(game.GetAuthoritativeStateForTesting(), "P3").Resources.Wood);
        Assert.Equal(0, M4CoreHarness.Player(game.GetAuthoritativeStateForTesting(), "P2").Resources.Wood);
        Assert.Null(game.GetAuthoritativeStateForTesting().ThreeKingdoms.Skills.PendingDecision);
        var build = Cmd(game, M4.CommandKind.BuildRoad); build.TargetId = edge; Accept(game, build);
        Skill(game, "P2", a => a.Command.Option == "luoying");
        var bank = Cmd(game, M4.CommandKind.BankTrade); bank.GiveResource = Resource.Wood; bank.GiveAmount = 4; bank.ReceiveResource = Resource.Wheat; bank.ReceiveAmount = 1; Accept(game, bank);
        Assert.Single(game.GetPlayerView("P2").ThreeKingdoms.SkillActions, a => a.Command.Option == "luoying");
        Skill(game, "P2", a => a.Command.Option == "luoying");
        Assert.Equal(2, M4CoreHarness.Player(game.GetAuthoritativeStateForTesting(), "P2").Resources.Wood);
        Assert.Null(game.GetAuthoritativeStateForTesting().ThreeKingdoms.Skills.PendingDecision); game.AssertInvariants();
        string final = game.Save(); Assert.True(game.Execute(bank).IsDuplicate); Assert.Equal(final, game.Save());
    }

    [Fact]
    public void ActualEndQueuesJiushiThenLianpoSkipsOnlyFirstExtraAndReturnsToFixedNormalSeat()
    {
        var game = Fixture(new[] { "tk-g06", "tk-g30", "tk-g13", "tk-g29" }, s =>
        { M4CoreHarness.Player(s, "P2").Resources.Wood = 7; s.ThreeKingdoms.Players.Single(p => p.PlayerId == "P2").FaceDown = true; });
        Accept(game, Cmd(game, M4.CommandKind.EndTurn)); Skill(game, "P1", a => a.Command.Option == "skip");
        Skill(game, "P2", a => a.Command.Option == "accept"); Skill(game, "P3", a => a.Command.Option == "accept"); Drain(game);
        var queued = game.GetAuthoritativeStateForTesting(); Assert.Equal(new[] { "P2", "P3" }, queued.ThreeKingdoms.Clock.ExtraTurnQueue);
        Accept(game, Cmd(game, M4.CommandKind.AdvanceThreeKingdomsPhase));
        var extra = game.GetAuthoritativeStateForTesting(); Assert.Equal("P3", extra.ActivePlayerId); Assert.Equal(extra.ActivePlayerId, extra.ThreeKingdoms.Clock.ActivePlayerId);
        Assert.True(extra.ThreeKingdoms.Clock.IsExtraTurn); Assert.Equal(1, extra.ThreeKingdoms.Clock.ExtraOpportunitiesConsumed);
        Assert.False(extra.ThreeKingdoms.Players.Single(p => p.PlayerId == "P2").FaceDown); Assert.Equal(1, extra.ThreeKingdoms.Clock.Round);
        Assert.All(extra.ThreeKingdoms.Generals.Where(g => g.Region == GeneralRegion.Active), g => Assert.Equal(3, g.RemainingLife));
        ReachPlay(game); Accept(game, Cmd(game, M4.CommandKind.EndTurn, "P3")); Drain(game); Accept(game, Cmd(game, M4.CommandKind.AdvanceThreeKingdomsPhase, "P3"));
        var normal = game.GetAuthoritativeStateForTesting(); Assert.Equal("P2", normal.ActivePlayerId); Assert.Equal(normal.ActivePlayerId, normal.ThreeKingdoms.Clock.ActivePlayerId);
        Assert.False(normal.ThreeKingdoms.Clock.IsExtraTurn); Assert.Equal(1, normal.ThreeKingdoms.Clock.NormalSeatCursor);
        Assert.Equal(1, normal.ThreeKingdoms.Clock.Round); Assert.All(normal.ThreeKingdoms.Generals.Where(g => g.Region == GeneralRegion.Active), g => Assert.Equal(3, g.RemainingLife));
    }

    [Fact]
    public void CombinedNativeBankPaymentFeedsLuoyingAndSkippedJiushiKeepsHostClockAligned()
    {
        var game = M5Harness.Fixture(s =>
        {
            s.RulesVersion = M5.CombinedGameSession.ThreeKingdomsB3RulesVersion; s.ThreeKingdoms = Generals("tk-g29", "tk-g30", "tk-g06", "tk-g21");
            M5Harness.Player(s).Resources.Wood = 4; M5Harness.Player(s, "P2").Resources.Wood = 7;
            s.ThreeKingdoms.Players.Single(p => p.PlayerId == "P2").FaceDown = true;
            s.ThreeKingdoms.Generals.Single(g => g.Region == GeneralRegion.Active && g.GeneralId == "tk-g21").Counters = new[] { new GeneralCounter { Key = "zhijian", Value = 3 } };
        });
        M5.Command Command(M5.CommandKind kind, string player = "P1") => new() { Id = ThreeKingdomsTestGame.NextId(), Kind = kind, PlayerId = player, WindowId = game.GetPlayerView(player).ThreeKingdoms.WindowId };
        void Execute(M5.Command c) { var r = game.Execute(c); Assert.True(r.Success, r.ErrorCode + ":" + r.Message); game.AssertInvariants(); }
        void Select(string player, string option)
        { var c = Command(M5.CommandKind.UseGeneralSkill, player); c.SkillCommand = game.GetPlayerView(player).ThreeKingdoms.SkillActions.First(a => a.Command.Option == option).Command; Execute(c); }
        var bank = Command(M5.CommandKind.BankTrade); bank.GiveResource = Resource.Wood; bank.GiveAmount = 4; bank.ReceiveResource = Resource.Wool; bank.ReceiveAmount = 1; Execute(bank);
        Select("P2", "luoying"); Assert.Equal(8, M5Harness.Player(game.GetAuthoritativeStateForTesting(), "P2").Resources.Wood);
        Execute(Command(M5.CommandKind.EndTurn)); Select("P2", "accept");
        Execute(Command(M5.CommandKind.AdvanceThreeKingdomsPhase)); var state = game.GetAuthoritativeStateForTesting();
        Assert.Equal("P2", state.ActivePlayerId); Assert.Equal(state.ActivePlayerId, state.ThreeKingdoms.Clock.ActivePlayerId);
        Assert.Equal(1, state.ThreeKingdoms.Clock.ExtraOpportunitiesConsumed); Assert.False(state.ThreeKingdoms.Clock.IsExtraTurn);
        Assert.False(state.ThreeKingdoms.Players.Single(p => p.PlayerId == "P2").FaceDown); Assert.Equal(1, state.ThreeKingdoms.Clock.Round); game.AssertInvariants();
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void FogShipPaymentKeepsItsRealWoodAvailableToLuoyingDespiteSameActionExplorationReward(bool emptyWoodBank)
    {
        var (game, edge) = FogFixture("tk-g29", emptyWoodBank);
        var build = new M5.Command { Id = ThreeKingdomsTestGame.NextId(), PlayerId = "P1", Kind = M5.CommandKind.BuildShip, TargetId = edge.Id, WindowId = game.GetPlayerView("P1").ThreeKingdoms.WindowId };
        var result = game.Execute(build); Assert.True(result.Success, result.ErrorCode + ":" + result.Message); game.AssertInvariants();
        Assert.Equal("P2", game.GetPlayerView("P1").ThreeKingdoms.SkillDecision?.PlayerId);
        var take = new M5.Command { Id = ThreeKingdomsTestGame.NextId(), PlayerId = "P2", Kind = M5.CommandKind.UseGeneralSkill,
            WindowId = game.GetPlayerView("P2").ThreeKingdoms.WindowId, SkillCommand = game.GetPlayerView("P2").ThreeKingdoms.SkillActions.First(a => a.Command.Option == "luoying").Command };
        Assert.True(game.Execute(take).Success); var final = game.GetAuthoritativeStateForTesting();
        Assert.Equal(1, M5Harness.Player(final, "P2").Resources.Wood); Assert.Equal(emptyWoodBank ? 0 : 1, M5Harness.Player(final).Resources.Wood); game.AssertInvariants();
    }

    [Fact]
    public void FogLastHandPaymentResolvesLianyingThenLuoyingBeforeRewardAndResumesSavedTailExactlyOnce()
    {
        var (game, edge) = FogFixture("tk-g23");
        int fogCount = game.GetAuthoritativeStateForTesting().Tiles.Count(t => t.Resource == "fog");
        var build = new M5.Command { Id = ThreeKingdomsTestGame.NextId(), PlayerId = "P1", Kind = M5.CommandKind.BuildShip, TargetId = edge.Id, WindowId = game.GetPlayerView("P1").ThreeKingdoms.WindowId };
        Accept(game, build);
        var paid = game.GetAuthoritativeStateForTesting();
        Assert.Equal("P1", paid.ThreeKingdoms.Skills.PendingDecision.PlayerId);
        Assert.Equal(0, M5Harness.Player(paid).Resources.Total);
        Assert.Equal(fogCount, paid.Tiles.Count(t => t.Resource == "fog"));
        Assert.Equal(new[] { edge.Id }, paid.ThreeKingdomsSpecialContinuation.NewEdgeIds);
        Assert.Null(paid.ThreeKingdomsNativePayments);
        Assert.Equal(2, paid.ThreeKingdoms.Skills.LossLedger.Single(l => l.PlayerId == "P1").LostHandCards);
        Assert.Equal(2, paid.Ships.Length);
        string authoritative = StateJson(paid);
        foreach (string player in paid.Players.Select(p => p.Id)) game.GetPlayerView(player);
        Assert.Equal(authoritative, StateJson(game.GetAuthoritativeStateForTesting()));

        // 原生夹具的权威状态序列化恢复：每个私有待决点新建相同地图，恢复后执行完全相同命令。
        var restored = RestoreFogState(paid);
        var lianying = new M5.Command { Id = ThreeKingdomsTestGame.NextId(), PlayerId = "P1", Kind = M5.CommandKind.UseGeneralSkill,
            WindowId = game.GetPlayerView("P1").ThreeKingdoms.WindowId, SkillCommand = game.GetPlayerView("P1").ThreeKingdoms.SkillActions.First(a => a.Command.CardType == 1).Command };
        Accept(game, lianying); Accept(restored, lianying);
        var afterLianying = game.GetAuthoritativeStateForTesting();
        Assert.Equal(StateJson(afterLianying), StateJson(restored.GetAuthoritativeStateForTesting()));
        Assert.Equal("P2", afterLianying.ThreeKingdoms.Skills.PendingDecision.PlayerId);
        Assert.Equal(2, M5Harness.Player(afterLianying).Resources.Total);
        Assert.Equal(fogCount, afterLianying.Tiles.Count(t => t.Resource == "fog"));
        Assert.NotNull(afterLianying.ThreeKingdomsSpecialContinuation);

        restored = RestoreFogState(afterLianying);
        var luoying = new M5.Command { Id = ThreeKingdomsTestGame.NextId(), PlayerId = "P2", Kind = M5.CommandKind.UseGeneralSkill,
            WindowId = game.GetPlayerView("P2").ThreeKingdoms.WindowId, SkillCommand = game.GetPlayerView("P2").ThreeKingdoms.SkillActions.First(a => a.Command.Option == "luoying").Command };
        Accept(game, luoying); Accept(restored, luoying);
        var final = game.GetAuthoritativeStateForTesting();
        Assert.Equal(StateJson(final), StateJson(restored.GetAuthoritativeStateForTesting()));
        Assert.Equal(fogCount - 1, final.Tiles.Count(t => t.Resource == "fog"));
        Assert.Equal(M5Harness.Player(afterLianying).Resources.Wood + 1, M5Harness.Player(final).Resources.Wood);
        Assert.Equal(3, M5Harness.Player(final).Resources.Total);
        Assert.Equal(1, M5Harness.Player(final, "P2").Resources.Wood);
        Assert.Null(final.ThreeKingdomsSpecialContinuation); Assert.Null(final.ThreeKingdomsNativePayments);
        Assert.Equal(2, final.ThreeKingdoms.Skills.LossLedger.Single(l => l.PlayerId == "P1").LostHandCards);
        Assert.Equal(2, final.Ships.Length);
        string saved = game.Save(); Assert.True(game.Execute(build).IsDuplicate); Assert.Equal(saved, game.Save());
        Assert.True(game.Execute(luoying).IsDuplicate); Assert.Equal(saved, game.Save());
    }

    [Fact]
    public void PaymentSnapshotsAndTailAreDeepClonedAndAbsentFromLegacyJson()
    {
        var state = M5Harness.New().GetAuthoritativeStateForTesting();
        Assert.DoesNotContain("ThreeKingdomsNativePayments", StateJson(state));
        Assert.DoesNotContain("ThreeKingdomsSpecialContinuation", StateJson(state));
        state.ThreeKingdomsNativePayments = new[] { new B3NativePayment { EventId = "pay", PlayerId = "P1", Cards = new[] { 1, 0, 0, 0, 0, 0, 0, 0 } } };
        state.ThreeKingdomsSpecialContinuation = new B3SpecialContinuation { PlayerId = "P1", NewEdgeIds = new[] { "edge" } };
        var copy = (M5.GameState)typeof(M5.GameState).GetMethod("CopyForTransaction", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(state, null)!;
        copy.ThreeKingdomsNativePayments[0].Cards[0] = 9; copy.ThreeKingdomsSpecialContinuation.NewEdgeIds[0] = "changed";
        Assert.Equal(1, state.ThreeKingdomsNativePayments[0].Cards[0]); Assert.Equal("edge", state.ThreeKingdomsSpecialContinuation.NewEdgeIds[0]);
        var legacy = M4CoreHarness.New().GetAuthoritativeStateForTesting();
        using var stream = new MemoryStream(); new DataContractJsonSerializer(typeof(M4.GameState)).WriteObject(stream, legacy);
        Assert.DoesNotContain("ThreeKingdomsNativePayments", Encoding.UTF8.GetString(stream.ToArray()));
    }

    private static string StateJson(M5.GameState state)
    { using var stream = new MemoryStream(); new DataContractJsonSerializer(typeof(M5.GameState)).WriteObject(stream, state); return Encoding.UTF8.GetString(stream.ToArray()); }
    private static void Accept(M5.CombinedGameSession game, M5.Command command)
    { var result = game.Execute(command); Assert.True(result.Success, result.ErrorCode + ":" + result.Message); game.AssertInvariants(); }
    private static M5.CombinedGameSession RestoreFogState(M5.GameState state)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(StateJson(state)));
        var restored = (M5.GameState)new DataContractJsonSerializer(typeof(M5.GameState)).ReadObject(stream)!;
        var game = M5.CombinedGameSession.Create("fog-islands", 4, 97);
        typeof(M5.CombinedGameSession).GetField("state", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(game, restored);
        game.AssertInvariants(); return game;
    }

    private static (M5.CombinedGameSession Game, Edge Edge) FogFixture(string builder, bool emptyWoodBank = false)
    {
        var game = M5.CombinedGameSession.Create("fog-islands", 4, 97); var s = game.GetAuthoritativeStateForTesting();
        var board = game.GetPlayerView("P1").Board;
        bool Sea(Edge e) { var adjacent = board.Tiles.Where(t => e.Vertices.All(t.Vertices.Contains)).ToArray(); return adjacent.Length == 1 || adjacent.Any(t => t.Resource == "fog" || t.Resource == "sea"); }
        var edge = board.Edges.First(e => Sea(e) && board.Tiles.Count(t => t.Resource == "fog" && t.Vertices.Any(e.Vertices.Contains)) == 1);
        var neighbor = board.Edges.First(e => e.Id != edge.Id && Sea(e) && e.Vertices.Intersect(edge.Vertices).Any());
        s.Phase = M5.GamePhase.Action; s.PendingDecision = null!; s.Turn = 9; s.ActivePlayerId = "P1"; s.SetupPlacementIndex = 8;
        s.RulesVersion = M5.CombinedGameSession.ThreeKingdomsB3RulesVersion; s.ThreeKingdoms = Generals(builder, "tk-g30", "tk-g21", "tk-g06");
        s.PirateTileId = null!; s.Ships = new[] { new M5.ShipPlacement { PlayerId = "P1", LocationId = neighbor.Id, BuiltTurn = 1 } };
        M5Harness.Player(s).Resources = new ResourceBag { Wood = 1, Wool = 1 }; if (emptyWoodBank) M5Harness.Player(s, "P3").Resources.Wood = 18;
        var hidden = s.Special.HiddenResources.ToList(); Assert.True(hidden.Remove("wood")); s.Special.HiddenResources = new[] { "wood" }.Concat(hidden).ToArray();
        M5Harness.Balance(s); M5Harness.SetState(game, s);
        return (game, edge);
    }
}
