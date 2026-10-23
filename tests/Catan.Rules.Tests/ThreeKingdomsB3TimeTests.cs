using System.Runtime.Serialization.Json;
using System.Text;
using Catan.Core.ThreeKingdoms;
using Xunit;
using static Catan.Rules.Tests.ThreeKingdomsB2EventTests;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB3TimeTests
{
    private static ThreeKingdomsB2TestHost Create(string first, string second = "tk-g21", string third = "tk-g29")
    {
        var host = ThreeKingdomsB2TestHost.Create(first, second, third);
        host.State.RulesVersion = GeneralCatalog.B3RulesVersion; host.State.PackageVersion = GeneralCatalog.B3PackageVersion;
        host.State.PackageContentHash = GeneralCatalog.B3ContentHash; return host;
    }
    private static B2SkillCommand Action(ThreeKingdomsB2TestHost host, string player, string option) => B2SkillEngine.Actions(host, player).First(a => a.Command.Option == option).Command;
    private static void Return(ThreeKingdomsB2TestHost host, string player, string cause, string batch, params int[] counts)
    {
        var cards = Cards(counts); int before = host.GetHand(player).Sum();
        host.RemoveHand(player, cards); host.AddBank(cards);
        B2SkillEngine.HoldReturnedCards(host, player, cause, batch, cards);
        B2SkillEngine.OnCardsMoved(host, batch + ":" + player, new[] { Change(player, before, before - cards.Sum(), cards.Sum()) });
    }

    [Fact]
    public void LianpoQueuesBeforeMixedRewardAndIsLimitedPerTenure()
    {
        var host = Create("tk-g13"); host.State.Clock.Phase = ThreeKingdomsPhase.End;
        B2SkillEngine.OnEnd(host, "结束一");
        ResolveWithReplay(host, "P1", Action(host, "P1", "accept"));
        Assert.Equal(new[] { "P1" }, host.State.Clock.ExtraTurnQueue); Assert.Equal(0, host.GetHand("P1").Sum());
        ResolveRewardWithReplay(host, 5); ResolveRewardWithReplay(host, 7); ResolveRewardWithReplay(host, 2); ResolveRewardWithReplay(host, 4);
        Assert.Equal(4, host.GetHand("P1").Sum());
        B2SkillEngine.OnEnd(host, "结束二"); Assert.False(B2SkillEngine.HasPending(host.State));
        ThreeKingdomsEngine.CompleteNormalTurn(host.State, host.DrawRandom);
        Assert.True(host.State.Clock.IsExtraTurn); Assert.Equal(1, host.State.Clock.Round); Assert.All(host.State.Generals, g => Assert.Equal(3, g.RemainingLife));
        host.AssertConserved();
    }

    [Fact]
    public void LianpoPendingRewardFinishesAfterRetirementAndDoesNotSpendNewTenure()
    {
        var host = Create("tk-g13"); host.State.Clock.Phase = ThreeKingdomsPhase.End;
        B2SkillEngine.OnEnd(host, "结束"); ResolveWithReplay(host, "P1", Action(host, "P1", "accept"));
        ResolveRewardWithReplay(host, 1);
        host.State.Generals[0].TenureId = "新司马懿"; host.State.Generals[0].Counters = Array.Empty<GeneralCounter>();
        Drain(host); Assert.Equal(4, host.GetHand("P1").Sum()); Assert.Empty(host.State.Generals[0].Counters);
        B2SkillEngine.OnEnd(host, "下个结束"); Assert.Equal("tk-g13-s1", host.State.Skills.Frames[0].SkillId);
    }

    [Fact]
    public void LianpoFaceDownExtraOpportunityIsSkippedWithoutRefundOrLifeLoss()
    {
        var host = Create("tk-g13"); host.State.Players[0].FaceDown = true; host.State.Clock.Phase = ThreeKingdomsPhase.End;
        B2SkillEngine.OnEnd(host, "结束"); Drain(host); ThreeKingdomsEngine.CompleteNormalTurn(host.State, host.DrawRandom);
        Assert.False(host.State.Players[0].FaceDown); Assert.Equal(1, host.State.Clock.ExtraOpportunitiesConsumed);
        Assert.Equal("P2", host.State.Clock.ActivePlayerId); Assert.Equal(4, host.GetHand("P1").Sum()); Assert.Equal(3, host.State.Generals[0].RemainingLife); host.AssertConserved();
    }

    [Fact]
    public void SevenDiscardsWaitForWholeBatchAndGuzhengIncludesItsOwnCards()
    {
        var host = Create("tk-g21", "tk-g29", "tk-g13"); host.Give("P1", 2); host.Give("P2", 0, 3);
        Return(host, "P1", "seven-discard", "七点一", 2);
        Assert.False(B2SkillEngine.HasPending(host.State)); Assert.Equal(0, host.GetHand("P1").Sum());
        Assert.Equal(2, B2SkillEngine.EscrowCards(host.State)[0]);
        var restored = host.Copy();
        Return(host, "P2", "seven-discard", "七点一", 0, 3); Return(restored, "P2", "seven-discard", "七点一", 0, 3);
        B2SkillEngine.CompleteSevenDiscard(host, "七点一"); B2SkillEngine.CompleteSevenDiscard(restored, "七点一");
        Assert.Equal(host.Snapshot(), restored.Snapshot()); Assert.Equal(5, host.GetHand("P1").Sum());
        Assert.Equal("P2", host.State.Skills.PendingDecision.PlayerId); Drain(host);
        Assert.Equal(1, host.GetHand("P2").Sum()); Assert.DoesNotContain(B2SkillEngine.EscrowCards(host.State), n => n != 0); host.AssertConserved();
    }

    [Theory][InlineData("P3", 2, 4)][InlineData("P2", 0, 6)]
    public void GuzhengAndLuoyingCompeteForSamePhysicalCardsInSeatOrder(string active, int caozhi, int zhangzhao)
    {
        var host = Create("tk-g30", "tk-g21", "tk-g13"); host.State.Clock.ActivePlayerId = active; host.Give("P3", 4, 2);
        Return(host, "P3", "seven-discard", "同一批", 4, 2); B2SkillEngine.CompleteSevenDiscard(host, "同一批");
        if (active == "P3")
        {
            Assert.Equal("luoying", host.State.Skills.PendingDecision.Kind);
            var take = B2SkillEngine.Actions(host, "P1").Single(a => a.Command.Counts[0] == 2).Command; ResolveWithReplay(host, "P1", take);
        }
        Drain(host); Assert.Equal(caozhi, host.GetHand("P1").Sum()); Assert.Equal(zhangzhao, host.GetHand("P2").Sum()); host.AssertConserved();
    }

    [Fact]
    public void SongweiOncePerNonemptySevenBatchAndNoOtherReturnTriggersIt()
    {
        var host = Create("tk-g29", "tk-g13", "tk-g21"); host.Give("P2", 4); host.Give("P3", 0, 2);
        B2SkillEngine.CompleteSevenDiscard(host, "无人弃牌"); Assert.False(B2SkillEngine.HasPending(host.State));
        Return(host, "P2", "discard", "其他弃牌", 1); Drain(host); Assert.Equal(0, host.GetHand("P1").Sum());
        Return(host, "P2", "seven-discard", "七点", 3); Return(host, "P3", "seven-discard", "七点", 0, 2);
        B2SkillEngine.CompleteSevenDiscard(host, "七点"); Drain(host); Assert.Equal(1, host.GetHand("P1").Sum());
        string snapshot = host.Snapshot(); B2SkillEngine.CompleteSevenDiscard(host, "七点"); Assert.Equal(snapshot, host.Snapshot()); host.AssertConserved();
    }

    [Theory][InlineData("payment")][InlineData("discard")][InlineData("play")]
    public void LuoyingOnlyTakesRealWoodAndCapsEachSourceForEachRealTurn(string cause)
    {
        var host = Create("tk-g30", "tk-g21", "tk-g13"); host.State.Clock.ActivePlayerId = "P2"; host.Give("P2", 6, 2); host.Give("P3", 2);
        Return(host, "P2", cause, "第一笔", 3, 2);
        ResolveWithReplay(host, "P1", B2SkillEngine.Actions(host, "P1").Single(a => a.Command.Counts[0] == 2).Command);
        Return(host, "P2", cause, "第二笔", 2); Drain(host); Assert.Equal(2, host.GetHand("P1")[0]); Assert.Equal(0, host.GetHand("P1")[1]);
        Return(host, "P3", cause, "别的来源", 1); Drain(host); Assert.Equal(3, host.GetHand("P1")[0]);
        host.State.Clock.TurnInstanceId++; Return(host, "P2", cause, "额外回合也刷新", 1); Drain(host); Assert.Equal(4, host.GetHand("P1")[0]); host.AssertConserved();
    }

    [Fact]
    public void LuoyingNoOwnTurnOrOwnPaymentAndRetiredChoiceReleasesCards()
    {
        var host = Create("tk-g30", "tk-g21", "tk-g13"); host.Give("P1", 2); host.Give("P2", 2);
        Return(host, "P2", "payment", "自己的回合", 1); Drain(host); Assert.Equal(2, host.GetHand("P1")[0]);
        host.State.Clock.ActivePlayerId = "P2"; Return(host, "P1", "payment", "自己支付", 1); Drain(host); Assert.Equal(1, host.GetHand("P1")[0]);
        Return(host, "P2", "payment", "旧将待决", 1); var stale = Action(host, "P1", "luoying");
        host.State.Generals[0].TenureId = "新任次"; B2SkillEngine.Pump(host);
        Assert.False(B2SkillEngine.HasPending(host.State)); RejectUnchanged(host, "P1", stale); host.AssertConserved();
    }

    [Fact]
    public void LuoyingRejectsExcessAndForgedNonWoodWithoutMutation()
    {
        var host = Create("tk-g30", "tk-g21", "tk-g13"); host.State.Clock.ActivePlayerId = "P2"; host.Give("P2", 3);
        Return(host, "P2", "payment", "费用", 3);
        var tooMany = Clone(Action(host, "P1", "luoying")); tooMany.Counts[0] = 3; RejectUnchanged(host, "P1", tooMany);
        var wrongType = Clone(Action(host, "P1", "luoying")); wrongType.Counts[1] = 1; RejectUnchanged(host, "P1", wrongType);
        RejectUnchanged(host, "P3", Action(host, "P1", "luoying")); Drain(host); host.AssertConserved();
    }

    [Fact]
    public void FangzhuTogglesExactlyTwoAndDoesNotInterruptCurrentTurn()
    {
        var host = Create("tk-g29"); host.State.Players[1].FaceDown = true;
        var command = Action(host, "P1", "fangzhu"); command.TargetPlayerIds = new[] { "P1", "P2" }; ResolveWithReplay(host, "P1", command);
        Assert.True(host.State.Players[0].FaceDown); Assert.False(host.State.Players[1].FaceDown); Assert.Equal(ThreeKingdomsPhase.Play, host.State.Clock.Phase);
        Assert.DoesNotContain(B2SkillEngine.Actions(host, "P1"), a => a.Command.Option == "fangzhu"); command.WindowId = host.State.WindowId; RejectUnchanged(host, "P1", command);
    }

    [Theory][InlineData("duplicate")][InlineData("one")][InlineData("unknown")][InlineData("other-turn")]
    public void FangzhuInvalidTargetsOrTimingCannotSpendLimitedUse(string kind)
    {
        var host = Create("tk-g29"); var command = Action(host, "P1", "fangzhu");
        if (kind == "duplicate") command.TargetPlayerIds = new[] { "P2", "P2" };
        if (kind == "one") command.TargetPlayerIds = new[] { "P2" };
        if (kind == "unknown") command.TargetPlayerIds = new[] { "P2", "P9" };
        if (kind == "other-turn") host.State.Clock.ActivePlayerId = "P2";
        RejectUnchanged(host, "P1", command); Assert.Empty(host.State.Generals[0].Counters);
    }

    [Fact]
    public void FangzhuTargetResponseResolvesBeforeFlipAndKongchengCancelsOnlyItsPart()
    {
        var host = Create("tk-g29", "tk-g37", "tk-g19"); var command = Action(host, "P1", "fangzhu"); command.TargetPlayerIds = new[] { "P2", "P3" };
        ResolveWithReplay(host, "P1", command); Assert.All(host.State.Players, p => Assert.False(p.FaceDown));
        Assert.Equal("P2", host.State.Skills.PendingDecision.PlayerId); Drain(host);
        Assert.True(host.State.Players[1].FaceDown); Assert.False(host.State.Players[2].FaceDown); Assert.Equal(1, host.GetHand("P2").Sum()); host.AssertConserved();
    }

    [Fact]
    public void JiushiTogglesBeforeThreeResourceChoicesAndNewPhaseMayFlipBack()
    {
        var host = Create("tk-g30"); ResolveWithReplay(host, "P1", Action(host, "P1", "jiushi"));
        Assert.True(host.State.Players[0].FaceDown); Assert.Equal(0, host.GetHand("P1").Sum());
        Assert.DoesNotContain(B2SkillEngine.Actions(host, "P1"), a => a.Command.CardType >= 5); Drain(host);
        Assert.Equal(3, host.GetHand("P1").Sum()); Assert.DoesNotContain(B2SkillEngine.Actions(host, "P1"), a => a.Command.Option == "jiushi");
        host.State.Clock.PhaseId++; ResolveWithReplay(host, "P1", Action(host, "P1", "jiushi")); Drain(host);
        Assert.False(host.State.Players[0].FaceDown); Assert.Equal(6, host.GetHand("P1").Sum()); host.AssertConserved();
    }

    [Fact]
    public void JiushiPaysSevenAndConsumesSkippedExtraOpportunityWithoutGrantingRealTurn()
    {
        var host = Create("tk-g21", "tk-g30", "tk-g29"); host.Give("P2", 7); host.State.Players[1].FaceDown = true; host.State.Clock.Phase = ThreeKingdomsPhase.End;
        B2SkillEngine.OnEnd(host, "结束"); ResolveWithReplay(host, "P2", Action(host, "P2", "accept"));
        Assert.Equal(0, host.GetHand("P2").Sum()); Assert.True(host.State.Players[1].FaceDown); Assert.Equal(new[] { "P2" }, host.State.Clock.ExtraTurnQueue);
        ThreeKingdomsEngine.CompleteNormalTurn(host.State, host.DrawRandom);
        Assert.Equal(1, host.State.Clock.ExtraOpportunitiesConsumed); Assert.False(host.State.Players[1].FaceDown);
        Assert.False(host.State.Clock.IsExtraTurn); Assert.Equal("P2", host.State.Clock.ActivePlayerId); Assert.Equal(1, host.State.Clock.Round); host.AssertConserved();
    }

    [Fact]
    public void JiushiOutsideQuotaSurvivesExtraOpportunitiesAndResetsOnlyOwnNormalStart()
    {
        var host = Create("tk-g21", "tk-g30", "tk-g29"); host.Give("P2", 28); host.State.Clock.Phase = ThreeKingdomsPhase.End;
        for (int i = 0; i < 3; i++)
        {
            host.State.Players[1].FaceDown = true; B2SkillEngine.OnEnd(host, "结束" + i); Drain(host);
            host.State.Clock.IsExtraTurn = true; B2SkillEngine.OnOpportunityStart(host, i + 2, "P2");
        }
        Assert.Equal(7, host.GetHand("P2")[0]); B2SkillEngine.OnEnd(host, "第四个结束"); Assert.False(B2SkillEngine.HasPending(host.State));
        B2SkillEngine.ResetNormalOpportunity(host.State, "P1"); B2SkillEngine.OnEnd(host, "别人的正常机会"); Assert.False(B2SkillEngine.HasPending(host.State));
        B2SkillEngine.ResetNormalOpportunity(host.State, "P2"); B2SkillEngine.OnEnd(host, "自己的正常机会之后"); Assert.Equal("optional", host.State.Skills.PendingDecision.Kind); host.AssertConserved();
    }

    [Fact]
    public void ReturnedCardsAreDeduplicatedDeepClonedAndCountedInInvariants()
    {
        var host = Create("tk-g21"); host.Give("P2", 1, 2); Return(host, "P2", "seven-discard", "七点", 1, 2);
        string before = host.Snapshot(); B2SkillEngine.HoldReturnedCards(host, "P2", "seven-discard", "七点", Cards(1, 2)); Assert.Equal(before, host.Snapshot());
        var clone = B2SkillEngine.Clone(host.State.Skills); clone.ReturnedCards[0].Cards[0] = 99; Assert.Equal(1, host.State.Skills.ReturnedCards[0].Cards[0]);
        for (int i = 0; i < 8; i++) Assert.Equal(30, host.Bank[i] + host.Hands.Values.Sum(h => h[i]) + B2SkillEngine.EscrowCards(host.State)[i]);
        Assert.Empty(B2SkillEngine.Validate(host)); B2SkillEngine.CompleteSevenDiscard(host, "七点"); Drain(host); host.AssertConserved();
    }

    [Fact]
    public void B2PackageDoesNotEnableTimeSkillsOrSerializeB3ReturnState()
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g30", "tk-g13", "tk-g29");
        Assert.DoesNotContain(B2SkillEngine.Actions(host, "P1"), a => a.Command.Option == "jiushi");
        B2SkillEngine.OnEnd(host, "结束"); Assert.False(B2SkillEngine.HasPending(host.State));
        host.Give("P2", 2); Return(host, "P2", "seven-discard", "旧版七点", 2); Drain(host);
        Assert.Null(host.State.Skills.ReturnedCards); Assert.Null(B2SkillEngine.Clone(host.State.Skills).ReturnedCards);
        using var stream = new MemoryStream(); new DataContractJsonSerializer(typeof(B2SkillState)).WriteObject(stream, host.State.Skills);
        Assert.DoesNotContain("ReturnedCards", Encoding.UTF8.GetString(stream.ToArray())); host.AssertConserved();
    }

    [Fact]
    public void B3EndWindowUsesSeatOrderIncludingExistingOptionalEndSkills()
    {
        var host = Create("tk-g13", "tk-g06", "tk-g30"); host.State.Clock.ActivePlayerId = "P2"; host.State.Clock.Phase = ThreeKingdomsPhase.End;
        host.State.Players[2].FaceDown = true; host.Give("P3", 7);
        B2SkillEngine.OnEnd(host, "结束次序");
        Assert.Equal(new[] { "tk-g06-s1", "tk-g30-s2", "tk-g13-s1" }, host.State.Skills.Frames.Select(f => f.SkillId));
        Assert.Equal("P2", host.State.Skills.PendingDecision.PlayerId);
    }

    [Fact]
    public void SevenInterceptionWaitsForLossResponseAndDoesNotMakeDiscardedCardsAvailableToDraw()
    {
        var host = Create("tk-g23", "tk-g21", "tk-g29"); host.Give("P1", 1); host.EmptyBank();
        Return(host, "P1", "seven-discard", "七点", 1);
        // 银行无牌，连营实际不能摸；弃牌仍在临时区，不能被连营抽回。
        Assert.Equal(0, host.GetHand("P1").Sum()); Assert.Equal(1, B2SkillEngine.EscrowCards(host.State)[0]);
        B2SkillEngine.CompleteSevenDiscard(host, "七点"); Drain(host);
        Assert.Equal(1, host.GetHand("P2")[0]); Assert.Equal(0, host.GetHand("P1").Sum()); host.AssertConserved();
    }

    [Fact]
    public void SevenClaimsQueueAfterPendingLossRewardAndCanResumeBothChoices()
    {
        var host = Create("tk-g23", "tk-g21", "tk-g29"); host.Give("P1", 1);
        Return(host, "P1", "seven-discard", "七点", 1); Assert.Equal("tk-g23-s1", host.State.Skills.Frames[0].SkillId);
        B2SkillEngine.CompleteSevenDiscard(host, "七点"); Assert.Equal("P1", host.State.Skills.PendingDecision.PlayerId);
        Assert.Equal(0, host.GetHand("P2").Sum()); ResolveRewardWithReplay(host, 2); Drain(host);
        Assert.Equal(2, host.GetHand("P1").Sum()); Assert.Equal(1, host.GetHand("P2")[0]); host.AssertConserved();
    }
}
