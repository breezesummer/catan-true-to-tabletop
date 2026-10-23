using Catan.Core.ThreeKingdoms;
using Xunit;

namespace Catan.Rules.Tests;

/// <summary>依据 B0 首批规范与 B2 补充规范，检验公共批次、库存、次数和恢复合同。</summary>
public sealed class ThreeKingdomsB2EventTests
{
    [Fact]
    public void LianyingPaysWholeBatchThenChoosesOneAndDrawsOneExactlyOnce()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g23");
        host.Give("P1", 1, 1);
        host.RemoveHand("P1", Cards(1, 1)); host.AddBank(Cards(1, 1));
        B2SkillEngine.OnCardsMoved(host, "建路整笔支付", new[] { Change("P1", 2, 0, 2) });
        Assert.Equal(0, host.GetHand("P1").Sum());
        ResolveRewardWithReplay(host, 2);
        Drain(host);
        Assert.Equal(2, host.GetHand("P1").Sum());
        Assert.True(host.GetHand("P1")[2] >= 1);
        string before = host.Snapshot();
        B2SkillEngine.OnCardsMoved(host, "建路整笔支付", new[] { Change("P1", 2, 0, 2) });
        Assert.Equal(before, host.Snapshot()); host.AssertConserved();
    }

    [Theory][InlineData(0, 0, 0)][InlineData(2, 2, 2)][InlineData(2, 1, 1)][InlineData(0, 2, 0)]
    public void LianyingIgnoresZeroChangesAndAtomicTradesThatDoNotEndEmpty(int before, int after, int lost)
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g23");
        host.Give("P1", after);
        B2SkillEngine.OnCardsMoved(host, "原子批次", new[] { Change("P1", before, after, lost) });
        Drain(host);
        Assert.Equal(after, host.GetHand("P1").Sum()); host.AssertConserved();
    }

    [Theory][InlineData(0)][InlineData(1)]
    public void LianyingUsesActualRemainingSupplyWithoutRecursiveEmptyHandTrigger(int available)
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g23");
        host.EmptyBank(0, 5);
        if (available != 0) { host.RemoveHand("P3", Cards(1)); host.AddBank(Cards(1)); }
        B2SkillEngine.OnCardsMoved(host, "最后牌转移后", new[] { Change("P1", 1, 0, 1) });
        Drain(host);
        Assert.Equal(available, host.GetHand("P1").Sum());
        Assert.Equal(0, host.RandomCalls); host.AssertConserved();
    }

    [Fact]
    public void LianyingChoiceRejectsOtherActorCommodityAndStaleWindowWithoutMutation()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g23");
        B2SkillEngine.OnCardsMoved(host, "待决失牌", new[] { Change("P1", 2, 0, 2) });
        var action = B2SkillEngine.Actions(host, "P1").First(a => a.Command.CardType >= 0);
        RejectUnchanged(host, "P2", Clone(action.Command));
        var commodity = Clone(action.Command); commodity.CardType = 5; RejectUnchanged(host, "P1", commodity);
        var stale = Clone(action.Command); stale.WindowId = "旧窗口"; RejectUnchanged(host, "P1", stale);
        var oldDecision = Clone(action.Command); oldDecision.DecisionId = "旧待决"; RejectUnchanged(host, "P1", oldDecision);
        Assert.Empty(B2SkillEngine.Actions(host, "P2"));
        Drain(host); host.AssertConserved();
    }

    [Fact]
    public void RendeBatchThresholdMatchesSeparateRealTransfersAndDoesNotNetReceivedCards()
    {
        var batch = ThreeKingdomsB2TestHost.Create("tk-g15");
        var separate = ThreeKingdomsB2TestHost.Create("tk-g15");
        B2SkillEngine.OnCardsMoved(batch, "交出四张", new[] { Change("P1", 4, 4, 4, 4) });
        Drain(batch);
        B2SkillEngine.OnCardsMoved(separate, "先交一张", new[] { Change("P1", 1, 1, 1, 1) });
        Drain(separate); Assert.Equal(0, separate.GetHand("P1").Sum());
        B2SkillEngine.OnCardsMoved(separate, "再交三张", new[] { Change("P1", 3, 3, 3, 3) });
        Drain(separate);
        Assert.Equal(2, batch.GetHand("P1").Sum());
        Assert.Equal(batch.GetHand("P1"), separate.GetHand("P1"));
        batch.AssertConserved(); separate.AssertConserved();
    }

    [Theory][InlineData(0, true)][InlineData(2, false)]
    public void RendeDoesNotRewardBankPaymentsOrGivingOutsideOwnRealTurn(int given, bool ownTurn)
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g15");
        if (!ownTurn) host.State.Clock.ActivePlayerId = "P2";
        B2SkillEngine.OnCardsMoved(host, "费用或回合外", new[] { Change("P1", 2, 0, 2, given) });
        Drain(host); Assert.Equal(0, host.GetHand("P1").Sum()); host.AssertConserved();
    }

    [Fact]
    public void RendeZeroSupplyConsumesPairAndNewTurnDoesNotCarryOddRemainder()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g15"); host.EmptyBank();
        B2SkillEngine.OnCardsMoved(host, "无供应交二", new[] { Change("P1", 2, 0, 2, 2) }); Drain(host);
        host.RemoveHand("P3", Cards(2)); host.AddBank(Cards(2));
        B2SkillEngine.OnCardsMoved(host, "单张余数", new[] { Change("P1", 1, 0, 1, 1) }); Drain(host);
        Assert.Equal(0, host.GetHand("P1").Sum());
        host.State.Clock.TurnInstanceId++;
        B2SkillEngine.OnCardsMoved(host, "新回合单张", new[] { Change("P1", 1, 0, 1, 1) }); Drain(host);
        Assert.Equal(0, host.GetHand("P1").Sum()); host.AssertConserved();
    }

    [Fact]
    public void YijiRewardsBeforeOriginalEffectAndDeduplicatesSameUseButNotIndependentUse()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g37");
        B2SkillEngine.OnTargeted(host, "第一次技能", "P2", "P1", "tk-g09-s1");
        ResolveRewardWithReplay(host, 3); Drain(host);
        Assert.Equal(1, host.GetHand("P1")[3]);
        B2SkillEngine.OnTargeted(host, "第一次技能", "P2", "P1", "tk-g09-s1"); Drain(host);
        Assert.Equal(1, host.GetHand("P1").Sum());
        B2SkillEngine.OnTargeted(host, "独立第二次技能", "P2", "P1", "tk-g09-s1"); Drain(host);
        Assert.Equal(2, host.GetHand("P1").Sum()); host.AssertConserved();
    }

    [Theory][InlineData(true, false)][InlineData(false, true)]
    public void YijiSelfTargetOrNoSupplyDoesNotCreateDeadDecision(bool selfTarget, bool empty)
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g37"); if (empty) host.EmptyBank(0, 5);
        B2SkillEngine.OnTargeted(host, "目标确认", selfTarget ? "P1" : "P2", "P1", "tk-g09-s1"); Drain(host);
        Assert.Equal(0, host.GetHand("P1").Sum()); host.AssertConserved();
    }

    [Theory][InlineData(false, false, 1)][InlineData(true, false, 0)][InlineData(false, true, 0)]
    public void JizhiFormalDiceChecksThisEventColorAndActualProgressRecipients(bool black, bool received, int reward)
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g33");
        B2SkillEngine.OnDiceResolved(host, "正式骰局", black, received ? new[] { "P1" } : Array.Empty<string>());
        Drain(host); Assert.Equal(reward, host.GetHand("P1").Sum());
        string before = host.Snapshot();
        B2SkillEngine.OnDiceResolved(host, "正式骰局", black, received ? new[] { "P1" } : Array.Empty<string>());
        Assert.Equal(before, host.Snapshot()); host.AssertConserved();
    }

    [Fact]
    public void JizhiNonBlackSevenAndIndependentSuccessfulProgressUsesEachRewardOnce()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g33"); host.FinalDiceTotal = 7;
        B2SkillEngine.OnDiceResolved(host, "非黑七", false, Array.Empty<string>()); Drain(host);
        Assert.Equal(1, host.GetHand("P1").Sum());
        B2SkillEngine.OnProgressUsed(host, "第一张成功完成", "P1"); Drain(host);
        B2SkillEngine.OnProgressUsed(host, "第一张成功完成", "P1"); Drain(host);
        B2SkillEngine.OnProgressUsed(host, "第二张成功完成", "P1"); Drain(host);
        Assert.Equal(3, host.GetHand("P1").Sum()); host.AssertConserved();
    }

    [Fact]
    public void JizhiEmptySupplyDoesNotDrawRandomOrLeaveChoiceAndDoesNotCatchUpLater()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g33"); host.EmptyBank(0, 5);
        B2SkillEngine.OnDiceResolved(host, "供应零骰局", false, Array.Empty<string>());
        B2SkillEngine.OnProgressUsed(host, "供应零用牌", "P1"); Drain(host);
        Assert.Equal(0, host.RandomCalls); Assert.Equal(0, host.GetHand("P1").Sum());
        host.RemoveHand("P3", Cards(2)); host.AddBank(Cards(2));
        Drain(host); Assert.Equal(0, host.GetHand("P1").Sum()); host.AssertConserved();
    }

    [Theory]
    [InlineData(2, 0)][InlineData(3, 1)][InlineData(4, 0)][InlineData(5, 1)][InlineData(6, 1)][InlineData(7, 0)]
    [InlineData(8, 1)][InlineData(9, 0)][InlineData(10, 1)][InlineData(11, 0)][InlineData(12, 1)]
    public void QinzhengOnlyTheSixConfirmedTotalsTriggerIncludingBlackEvents(int total, int expected)
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g43"); host.FinalDiceTotal = total;
        B2SkillEngine.OnDiceResolved(host, "点数过滤", true, Array.Empty<string>()); Drain(host);
        Assert.Equal(expected, host.GetHand("P1").Sum()); host.AssertConserved();
    }

    [Fact]
    public void QinzhengFiveRewardsLoopAcrossTurnsWithExactCommodityAndColorSelectionAndRecovery()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g43");
        for (int reward = 0; reward < 6; reward++)
        {
            host.FinalDiceTotal = new[] { 3, 5, 6, 8, 10, 12 }[reward]; host.State.Clock.TurnInstanceId++;
            B2SkillEngine.OnDiceResolved(host, "循环骰-" + reward, false, Array.Empty<string>());
            if (reward == 1 || reward == 3) ResolveRewardWithReplay(host, 4);
            if (reward == 2) ResolveRewardWithReplay(host, 7);
            if (reward == 4)
            {
                string top = host.Decks[1][0].Id;
                var choice = B2SkillEngine.Actions(host, "P1").First(a => a.Command.CardType == 1);
                ResolveWithReplay(host, "P1", choice.Command);
                Assert.Equal(top, Assert.Single(host.GetProgressCards("P1")).Id);
            }
            Drain(host);
        }
        Assert.Equal(5, host.GetHand("P1").Sum()); Assert.Equal(1, host.GetHand("P1")[7]);
        Assert.True(host.GetHand("P1")[4] >= 2); Assert.Single(host.GetProgressCards("P1")); host.AssertConserved();
    }

    [Fact]
    public void QinzhengNoCommodityStillAdvancesAndNeverRetroactivelyPaysMissingReward()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g43"); host.EmptyBank(5, 8);
        for (int reward = 0; reward < 4; reward++)
        {
            B2SkillEngine.OnDiceResolved(host, "短缺骰-" + reward, false, Array.Empty<string>()); Drain(host);
        }
        Assert.Equal(3, host.GetHand("P1").Take(5).Sum()); Assert.Equal(0, host.GetHand("P1").Skip(5).Sum());
        B2SkillEngine.OnDiceResolved(host, "下一项须发展牌", false, Array.Empty<string>()); Drain(host);
        Assert.Single(host.GetProgressCards("P1")); host.AssertConserved();
    }

    [Theory][InlineData(false, true, true, 0, 0)][InlineData(true, false, true, 0, 1)][InlineData(true, true, false, 0, 0)]
    public void DicePurposeAndCommitFlagsSeparateJizhiFromQinzheng(bool actual, bool formal, bool final, int jizhi, int qinzheng)
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g33", "tk-g43");
        B2SkillEngine.OnDiceResolved(host, "用途明确骰局", false, Array.Empty<string>(), actual, formal, final); Drain(host);
        Assert.Equal(jizhi, host.GetHand("P1").Sum()); Assert.Equal(qinzheng, host.GetHand("P2").Sum()); host.AssertConserved();
    }

    [Fact]
    public void QinzhengZeroRewardUnderExistingJushouBlockStillAdvancesCycle()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g43");
        host.State.Skills.TimedEffects = new[] { new B2TimedEffect { Id = "旧据守", Kind = "draw-block", PlayerId = "P1" } };
        B2SkillEngine.OnDiceResolved(host, "被禁第一项", false, Array.Empty<string>()); Drain(host);
        Assert.Equal(0, host.GetHand("P1").Sum()); Assert.Equal(0, host.RandomCalls);
        B2SkillEngine.OnOpportunityStart(host, 4, "P1");
        B2SkillEngine.OnDiceResolved(host, "第二项自选", false, Array.Empty<string>());
        Assert.Equal("card", host.State.Skills.PendingDecision.Kind); ResolveRewardWithReplay(host, 3); Drain(host);
        Assert.Equal(1, host.GetHand("P1")[3]); Assert.Equal(0, host.RandomCalls); host.AssertConserved();
    }

    [Fact]
    public void QinzhengAcceptedOldFrameFinishesButNewTenureRestartsAtRandomResource()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g43");
        B2SkillEngine.OnDiceResolved(host, "随机首项", false, Array.Empty<string>()); Drain(host);
        B2SkillEngine.OnDiceResolved(host, "自选第二项", false, Array.Empty<string>());
        host.State.Generals[0].TenureId = "重新登场"; host.State.Generals[0].Counters = Array.Empty<GeneralCounter>();
        ResolveRewardWithReplay(host, 4); Drain(host);
        int beforeRandom = host.RandomCalls;
        B2SkillEngine.OnDiceResolved(host, "新任次首项", false, Array.Empty<string>()); Drain(host);
        Assert.Equal(beforeRandom + 1, host.RandomCalls); Assert.Equal(3, host.GetHand("P1").Sum()); host.AssertConserved();
    }

    [Fact]
    public void RendeAcceptedRewardsSurviveRetirementWithoutCreditingNewTenure()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g15");
        B2SkillEngine.OnCardsMoved(host, "旧任次给四", new[] { Change("P1", 4, 0, 4, 4) });
        ResolveRewardWithReplay(host, 5);
        host.State.Generals[0].TenureId = "新刘备"; host.State.Generals[0].Counters = Array.Empty<GeneralCounter>();
        ResolveRewardWithReplay(host, 6); Drain(host);
        B2SkillEngine.OnCardsMoved(host, "新任次只给一", new[] { Change("P1", 1, 0, 1, 1) }); Drain(host);
        Assert.Equal(2, host.GetHand("P1").Sum()); Assert.Equal(1, host.GetHand("P1")[5]); Assert.Equal(1, host.GetHand("P1")[6]); host.AssertConserved();
    }

    internal static int[] Cards(params int[] values) => values.Concat(Enumerable.Repeat(0, 8)).Take(8).ToArray();
    internal static B2HandChange Change(string player, int before, int after, int lost, int given = 0) =>
        new() { PlayerId = player, BeforeCount = before, AfterCount = after, LostCount = lost, GivenCount = given };
    internal static B2SkillCommand Clone(B2SkillCommand command) => System.Text.Json.JsonSerializer.Deserialize<B2SkillCommand>(System.Text.Json.JsonSerializer.Serialize(command))!;
    internal static void RejectUnchanged(ThreeKingdomsB2TestHost host, string player, B2SkillCommand command)
    {
        string before = host.Snapshot(); Assert.Throws<InvalidOperationException>(() => B2SkillEngine.Resolve(host, player, command)); Assert.Equal(before, host.Snapshot());
    }
    internal static void ResolveRewardWithReplay(ThreeKingdomsB2TestHost host, int type)
    {
        B2SkillEngine.Pump(host);
        var action = B2SkillEngine.Actions(host, "P1").First(a => a.Command.CardType == type);
        ResolveWithReplay(host, "P1", action.Command);
    }
    internal static void ResolveWithReplay(ThreeKingdomsB2TestHost host, string player, B2SkillCommand command)
    {
        var restored = host.Copy();
        B2SkillEngine.Resolve(host, player, command); B2SkillEngine.Resolve(restored, player, Clone(command));
        Assert.Equal(host.Snapshot(), restored.Snapshot());
    }
    internal static void Drain(ThreeKingdomsB2TestHost host)
    {
        B2SkillEngine.Pump(host);
        for (int steps = 0; B2SkillEngine.HasPending(host.State) && steps < 100; steps++)
        {
            var decision = host.State.Skills.PendingDecision;
            Assert.NotNull(decision);
            var actions = B2SkillEngine.Actions(host, decision.PlayerId);
            Assert.NotEmpty(actions);
            var action = actions.FirstOrDefault(a => !a.Label.Contains("放弃") && !a.Label.Contains("不发动")) ?? actions[0];
            ResolveWithReplay(host, decision.PlayerId, action.Command); B2SkillEngine.Pump(host);
        }
        Assert.False(B2SkillEngine.HasPending(host.State), "技能队列超过100步，不能伪装正常完成。");
    }
}
