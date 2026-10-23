using Catan.Core.ThreeKingdoms;
using Xunit;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB3DiceTests
{
    private static ThreeKingdomsB2TestHost Host(string first = "tk-g19", string second = "tk-g13", string third = "tk-g43")
    {
        var h = ThreeKingdomsB2TestHost.Create(first, second, third);
        h.State.RulesVersion = GeneralCatalog.B3RulesVersion; h.State.PackageVersion = GeneralCatalog.B3PackageVersion;
        h.State.PackageContentHash = GeneralCatalog.B3ContentHash; h.State.Clock.Phase = ThreeKingdomsPhase.Roll;
        return h;
    }
    private static B2SkillCommand Pick(ThreeKingdomsB2TestHost h, string player, string option, int? card = null, int? red = null) =>
        B3DiceEngine.Actions(h, player).First(a => a.Command.Option == option && (card == null || a.Command.CardType == card) && (red == null || a.Command.Counts[0] == red)).Command;
    private static void Choose(ThreeKingdomsB2TestHost h, string player, string option, int? card = null, int? red = null) => B3DiceEngine.Resolve(h, player, Pick(h, player, option, card, red));
    private static void GiveGhostCost(ThreeKingdomsB2TestHost h, int quantity = 2)
    {
        h.Give("P2", quantity);
        h.Tiles["P2"] = new[] { new B2HarvestTile { TileId = "自己的木田", Number = 5, AllowedCardTypes = new[] { 0 } } };
    }
    private static void SameChoiceAfterSave(ThreeKingdomsB2TestHost h, string player, string option, int? card = null)
    {
        var restored = h.Copy(); var command = Pick(h, player, option, card);
        B3DiceEngine.Resolve(h, player, command); B3DiceEngine.Resolve(restored, player, command);
        Assert.Equal(h.Snapshot(), restored.Snapshot());
        Assert.Empty(B3DiceEngine.Validate(h.State));
    }

    [Fact]
    public void OrdinaryRollConsumesThreeRandomValuesAndCanOnlyBeSubmittedOnce()
    {
        var h = Host("tk-g06"); B3DiceEngine.BeginRoll(h, "P1", "骰局-1");
        Assert.Equal(3, h.RandomCalls); var result = B3DiceEngine.TakeFinal(h);
        Assert.NotNull(result); Assert.True(result.ActualRoll); Assert.True(result.NumericActualRoll); Assert.True(result.EventActualRoll);
        Assert.InRange(result.Red, 1, 6); Assert.InRange(result.White, 1, 6); Assert.InRange(result.EventFace, 1, 6);
        Assert.Null(B3DiceEngine.TakeFinal(h)); Assert.False(B3DiceEngine.HasPending(h.State));
        var snapshot = h.Snapshot(); Assert.Throws<InvalidOperationException>(() => B3DiceEngine.BeginRoll(h, "P1", "骰局-2")); Assert.Equal(snapshot, h.Snapshot());
    }

    [Fact]
    public void ObserveHasNoRandomBeforeConsentAndEachDieChoosesIndependently()
    {
        var h = Host(); B3DiceEngine.BeginRoll(h, "P1", "观星骰局");
        Assert.Equal(0, h.RandomCalls); Assert.Null(B3DiceEngine.TakeFinal(h));
        SameChoiceAfterSave(h, "P1", "observe"); Assert.Equal(6, h.RandomCalls);
        var candidates = h.State.Dice.Roll.Candidates;
        SameChoiceAfterSave(h, "P1", "select", 1); SameChoiceAfterSave(h, "P1", "select", 0); SameChoiceAfterSave(h, "P1", "select", 1);
        var result = B3DiceEngine.TakeFinal(h)!;
        Assert.Equal(candidates[1].Red, result.Red); Assert.Equal(candidates[0].White, result.White); Assert.Equal(candidates[1].EventFace, result.EventFace);
        Assert.Null(B3DiceEngine.TakeFinal(h)); Assert.Equal(6, h.RandomCalls);
    }

    [Fact]
    public void ObserveMayBeDeclinedWithoutMakingSecondCandidate()
    {
        var h = Host(); B3DiceEngine.BeginRoll(h, "P1", "放弃观星"); Choose(h, "P1", "skip");
        Assert.Equal(3, h.RandomCalls); Assert.Single(h.State.Dice.Roll.Candidates); Assert.NotNull(B3DiceEngine.TakeFinal(h));
    }

    [Fact]
    public void GhostMayModifyTheSameDieRepeatedlyAndPaysEvenForDiscardedCandidate()
    {
        var h = Host(); GiveGhostCost(h, 3); B3DiceEngine.BeginRoll(h, "P1", "观星改骰"); Choose(h, "P1", "observe");
        Assert.Equal(3, h.RandomCalls); SameChoiceAfterSave(h, "P2", "modify", 0);
        Assert.Equal(5, h.State.Dice.Roll.Candidates[0].Red); Assert.Equal(2, h.Hands["P2"][0]); Assert.Equal(3, h.RandomCalls);
        SameChoiceAfterSave(h, "P2", "modify", 0); Assert.Equal(1, h.Hands["P2"][0]);
        SameChoiceAfterSave(h, "P2", "skip"); Assert.Equal(6, h.RandomCalls); SameChoiceAfterSave(h, "P2", "skip");
        Choose(h, "P1", "select", 1); Choose(h, "P1", "select", 1); Choose(h, "P1", "select", 1);
        Assert.NotNull(B3DiceEngine.TakeFinal(h)); Assert.Equal(1, h.Hands["P2"][0]); h.AssertConserved();
    }

    [Fact]
    public void GhostPaymentClaimCompletesBeforeSavedModificationAndSurvivesRetirement()
    {
        var h = Host("tk-g06", "tk-g13", "tk-g30"); GiveGhostCost(h, 1);
        B3DiceEngine.BeginRoll(h, "P1", "鬼才费用子链"); var original = h.State.Dice.Roll.Candidates[0].Red;
        Choose(h, "P2", "modify", 0); Assert.Equal("luoying", h.State.Skills.PendingDecision.Kind);
        Assert.Equal(original, h.State.Dice.Roll.Candidates[0].Red); Assert.False(h.State.Dice.Roll.Modifications[0].Applied);
        Assert.Null(B3DiceEngine.TakeFinal(h)); Assert.Empty(B3DiceEngine.Actions(h, "P2"));
        h.State.Generals[1].Region = GeneralRegion.Pool; var saved = h.Copy();
        var claim = B2SkillEngine.Actions(h, "P3").First(a => a.Command.Option == "luoying").Command;
        B2SkillEngine.Resolve(h, "P3", claim); B2SkillEngine.Resolve(saved, "P3", claim);
        B3DiceEngine.Pump(h); B3DiceEngine.Pump(saved); Assert.Equal(h.Snapshot(), saved.Snapshot());
        Assert.True(h.State.Dice.Roll.Modifications[0].Applied); Assert.Equal(5, B3DiceEngine.TakeFinal(h)!.Red);
        Assert.Equal(1, h.Hands["P3"][0]); Assert.Equal(0, h.Hands["P2"][0]); h.AssertConserved();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(8)]
    public void GhostCannotUseZeroOrHighProductionNumbers(int number)
    {
        var h = Host("tk-g06"); GiveGhostCost(h); h.Tiles["P2"][0].Number = number;
        B3DiceEngine.BeginRoll(h, "P1", "不合法地产"); Assert.Empty(B3DiceEngine.Actions(h, "P2")); Assert.NotNull(B3DiceEngine.TakeFinal(h)); Assert.Equal(2, h.Hands["P2"][0]);
    }

    [Fact]
    public void GhostRequiresTheRealCardTypeAndTheHostsCurrentProductionEligibility()
    {
        var h = Host("tk-g06"); h.Give("P2", 0, 0, 0, 0, 0, 1);
        h.Tiles["P2"] = new[] { new B2HarvestTile { TileId = "非城市牧场", Number = 6, AllowedCardTypes = new[] { 2 } } };
        B3DiceEngine.BeginRoll(h, "P1", "商品不能伪作羊"); Assert.NotNull(B3DiceEngine.TakeFinal(h)); Assert.Equal(1, h.Hands["P2"][5]);
        h.State.Clock.TurnInstanceId++; h.Tiles["P2"][0].AllowedCardTypes = new[] { 2, 5 };
        B3DiceEngine.BeginRoll(h, "P1", "城市商品来源"); Assert.Contains(B3DiceEngine.Actions(h, "P2"), a => a.Command.CardType == 5);
        h.Tiles["P2"] = Array.Empty<B2HarvestTile>(); var old = Pick(h, "P2", "skip");
        old.Option = "modify"; old.CardType = 5; old.TileIds = new[] { "非城市牧场" }; var snapshot = h.Snapshot();
        Assert.Throws<InvalidOperationException>(() => B3DiceEngine.Resolve(h, "P2", old)); Assert.Equal(snapshot, h.Snapshot());
    }

    [Fact]
    public void WrongPlayerStaleWindowAndForgedValuesCannotConsumeRandomOrCards()
    {
        var h = Host(); GiveGhostCost(h); B3DiceEngine.BeginRoll(h, "P1", "防伪骰局"); Choose(h, "P1", "observe");
        var legal = Pick(h, "P2", "modify", 0); var snapshot = h.Snapshot();
        Assert.Throws<InvalidOperationException>(() => B3DiceEngine.Resolve(h, "P1", legal)); Assert.Equal(snapshot, h.Snapshot());
        legal.Counts[0] = 2; Assert.Throws<InvalidOperationException>(() => B3DiceEngine.Resolve(h, "P2", legal)); Assert.Equal(snapshot, h.Snapshot());
        legal = Pick(h, "P2", "modify", 0); legal.WindowId = "旧窗口"; Assert.Throws<InvalidOperationException>(() => B3DiceEngine.Resolve(h, "P2", legal)); Assert.Equal(snapshot, h.Snapshot());
        legal = Pick(h, "P2", "modify", 0); legal.Counts[7] = 1; Assert.Throws<InvalidOperationException>(() => B3DiceEngine.Resolve(h, "P2", legal)); Assert.Equal(snapshot, h.Snapshot());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void FixedSevenChoosesEventAndRedWhiteWithoutAnyRandomOrGhostWindow(int color)
    {
        var h = Host("tk-g19", "tk-g21", "tk-g13"); B3DiceEngine.BeginTurn(h); Assert.Equal(0, h.RandomCalls);
        var restored = h.Copy(); var command = Pick(h, "P2", "fixed", color, 5);
        B3DiceEngine.Resolve(h, "P2", command); B3DiceEngine.Resolve(restored, "P2", command); Assert.Equal(h.Snapshot(), restored.Snapshot());
        B3DiceEngine.BeginRoll(h, "P1", "固定七"); var result = B3DiceEngine.TakeFinal(h)!;
        Assert.Equal(5, result.Red); Assert.Equal(2, result.White); Assert.Equal(color, result.EventFace);
        Assert.False(result.ActualRoll); Assert.False(result.NumericActualRoll); Assert.False(result.EventActualRoll); Assert.Equal(0, h.RandomCalls);
        Assert.Equal(1, h.State.Generals[1].Counters.Single(c => c.Key == "zhijian").Value); Assert.Empty(B3DiceEngine.Actions(h, "P1"));
    }

    [Fact]
    public void FixedSevenUsageIsLimitedToThreePerTenureAndDecliningCostsNothing()
    {
        var h = Host("tk-g06", "tk-g21"); B3DiceEngine.BeginTurn(h); Choose(h, "P2", "skip"); Assert.Empty(h.State.Generals[1].Counters);
        for (var i = 0; i < 3; i++)
        {
            h.State.Clock.TurnInstanceId++; B3DiceEngine.BeginTurn(h); Choose(h, "P2", "fixed", 4, 1);
            B3DiceEngine.BeginRoll(h, "P1", "直谏第" + i); Assert.NotNull(B3DiceEngine.TakeFinal(h));
        }
        h.State.Clock.TurnInstanceId++; B3DiceEngine.BeginTurn(h); Assert.Empty(B3DiceEngine.Actions(h, "P2"));
        Assert.Null(h.State.Dice.FixedResult); Assert.False(B3DiceEngine.HasPending(h.State));
        h.State.Generals[1].TenureId = "新登场"; h.State.Generals[1].Counters = Array.Empty<GeneralCounter>();
        h.State.Clock.TurnInstanceId++; B3DiceEngine.BeginTurn(h); Assert.Contains(B3DiceEngine.Actions(h, "P2"), a => a.Command.Option == "fixed");
    }

    [Fact]
    public void FixedSevenRejectsAlchemyOverrideWithoutStateMutation()
    {
        var h = Host("tk-g19", "tk-g21"); B3DiceEngine.BeginTurn(h); Choose(h, "P2", "fixed", 4, 3);
        var snapshot = h.Snapshot(); Assert.Throws<InvalidOperationException>(() => B3DiceEngine.BeginRoll(h, "P1", "冲突", 6, 6)); Assert.Equal(snapshot, h.Snapshot());
        B3DiceEngine.BeginRoll(h, "P1", "固定结果"); Assert.Equal(7, B3DiceEngine.TakeFinal(h)!.Total);
    }

    [Fact]
    public void JudgmentUsesTwoDiceAndCanBeModifiedWithoutFormalEventOrProduction()
    {
        var h = Host("tk-g06"); GiveGhostCost(h); h.State.Clock.Phase = ThreeKingdomsPhase.Judgment;
        B3DiceEngine.BeginJudgment(h, "P1", "判定"); Assert.Equal(2, h.RandomCalls);
        SameChoiceAfterSave(h, "P2", "modify", 0); SameChoiceAfterSave(h, "P2", "skip");
        var result = B3DiceEngine.TakeFinal(h)!; Assert.Equal(B3DicePurpose.Judgment, result.Purpose); Assert.Equal(0, result.EventFace);
        Assert.True(result.NumericActualRoll); Assert.False(result.EventActualRoll); Assert.Equal(5, result.Red); Assert.Equal(2, h.RandomCalls);
        Assert.DoesNotContain(h.State.Skills.ProcessedEvents, e => e.StartsWith("dice:")); h.AssertConserved();
    }

    [Fact]
    public void ComparisonHasNoEventDieAndKeepsItsDistinctPurpose()
    {
        var h = Host("tk-g06"); B3DiceEngine.BeginJudgment(h, "P2", "比大小", true); var result = B3DiceEngine.TakeFinal(h)!;
        Assert.Equal(B3DicePurpose.Comparison, result.Purpose); Assert.Equal(2, h.RandomCalls); Assert.Equal(0, result.EventFace);
    }

    [Fact]
    public void RerollRetainsAttemptsButOnlyLastResultCanBeSubmitted()
    {
        var h = Host("tk-g06"); B3DiceEngine.BeginRoll(h, "P1", "重掷基础"); Assert.Equal(3, h.RandomCalls);
        var restored = h.Copy(); B3DiceEngine.Reroll(h, "重掷基础"); B3DiceEngine.Reroll(restored, "重掷基础"); Assert.Equal(h.Snapshot(), restored.Snapshot());
        Assert.Equal(6, h.RandomCalls); Assert.Equal(2, h.State.Dice.Roll.Candidates.Length);
        var result = B3DiceEngine.TakeFinal(h)!; Assert.Equal(2, result.Attempt); Assert.Equal(h.State.Dice.Roll.Candidates[1].Red, result.Red);
        var snapshot = h.Snapshot(); Assert.Throws<InvalidOperationException>(() => B3DiceEngine.Reroll(h, "重掷基础")); Assert.Equal(snapshot, h.Snapshot()); Assert.Null(B3DiceEngine.TakeFinal(h));
    }

    [Fact]
    public void AlchemyWithObserveOnlyRollsAndSelectsTwoEventCandidates()
    {
        var h = Host(); GiveGhostCost(h); B3DiceEngine.BeginRoll(h, "P1", "炼金观星", 2, 6); Choose(h, "P1", "observe");
        Assert.Equal(2, h.RandomCalls); Assert.Empty(B3DiceEngine.Actions(h, "P2")); Assert.Equal(2, h.State.Dice.Roll.ChoiceCursor);
        SameChoiceAfterSave(h, "P1", "select", 1); var result = B3DiceEngine.TakeFinal(h)!;
        Assert.Equal(2, result.Red); Assert.Equal(6, result.White); Assert.False(result.ActualRoll); Assert.False(result.NumericActualRoll); Assert.True(result.EventActualRoll);
        Assert.Equal(2, h.Hands["P2"][0]); Assert.Equal(2, h.RandomCalls);
    }

    [Fact]
    public void RetiredGhostCannotReuseItsOldResponseAndPumpContinues()
    {
        var h = Host("tk-g06"); GiveGhostCost(h); B3DiceEngine.BeginRoll(h, "P1", "鬼才隐退"); var old = Pick(h, "P2", "modify", 0);
        h.State.Generals[1].Region = GeneralRegion.Pool; var snapshot = h.Snapshot();
        Assert.Throws<InvalidOperationException>(() => B3DiceEngine.Resolve(h, "P2", old)); Assert.Equal(snapshot, h.Snapshot());
        B3DiceEngine.Pump(h); Assert.NotNull(B3DiceEngine.TakeFinal(h)); Assert.Equal(2, h.Hands["P2"][0]);
    }

    [Fact]
    public void UnacceptedObserveExpiresOnRetirementButAcceptedChoicesStillFinish()
    {
        var h = Host(); B3DiceEngine.BeginRoll(h, "P1", "未发动观星"); var old = Pick(h, "P1", "observe");
        h.State.Generals[0].Region = GeneralRegion.Pool; var snapshot = h.Snapshot();
        Assert.Throws<InvalidOperationException>(() => B3DiceEngine.Resolve(h, "P1", old)); Assert.Equal(snapshot, h.Snapshot());
        B3DiceEngine.Pump(h); Assert.NotNull(B3DiceEngine.TakeFinal(h)); Assert.Equal(3, h.RandomCalls);
        var accepted = Host(); B3DiceEngine.BeginRoll(accepted, "P1", "已发动观星"); Choose(accepted, "P1", "observe");
        accepted.State.Generals[0].Region = GeneralRegion.Pool; B3DiceEngine.Pump(accepted);
        Choose(accepted, "P1", "select", 1); Choose(accepted, "P1", "select", 1); Choose(accepted, "P1", "select", 1);
        Assert.NotNull(B3DiceEngine.TakeFinal(accepted)); Assert.Equal(6, accepted.RandomCalls);
    }

    [Fact]
    public void RefreshInvalidatesOldDecisionAndUnrelatedPlayerSeesNoPrivateAction()
    {
        var h = Host(); B3DiceEngine.BeginRoll(h, "P1", "窗口刷新"); var old = Pick(h, "P1", "observe"); h.State.WindowId = "新窗口";
        B3DiceEngine.RefreshDecision(h.State); var snapshot = h.Snapshot(); Assert.Throws<InvalidOperationException>(() => B3DiceEngine.Resolve(h, "P1", old)); Assert.Equal(snapshot, h.Snapshot());
        var view = new ThreeKingdomsView(); B3DiceEngine.Project(h, view, "P2");
        Assert.Empty(view.SkillActions); Assert.Null(view.SkillDecision.DecisionId); Assert.Null(view.SkillDecision.WindowId); Assert.Null(view.SkillDecision.Kind);
        Choose(h, "P1", "skip"); Assert.NotNull(B3DiceEngine.TakeFinal(h));
    }

    [Fact]
    public void PreviewAndCloneDoNotConsumeRandomAndCannotMutateAuthority()
    {
        var h = Host(); GiveGhostCost(h); B3DiceEngine.BeginRoll(h, "P1", "克隆隔离"); Choose(h, "P1", "observe");
        var snapshot = h.Snapshot(); B3DiceEngine.Actions(h, "P2"); B3DiceEngine.Project(h, new ThreeKingdomsView(), "P2"); Assert.Equal(snapshot, h.Snapshot());
        var copy = B3DiceEngine.Clone(h.State.Dice); copy.Roll.Candidates[0].Red = 99; copy.Roll.Responders[0] = "伪造"; copy.PendingDecision.PlayerId = "伪造";
        Assert.Equal(snapshot, h.Snapshot());
    }

    [Theory]
    [InlineData("骰面")]
    [InlineData("重复候选")]
    [InlineData("选项")]
    [InlineData("用途")]
    [InlineData("时钟")]
    [InlineData("待决")]
    [InlineData("提前提交")]
    [InlineData("旧规则")]
    public void RuntimeInvariantsRejectMalformedDiceAuthority(string mutation)
    {
        var h = Host(); B3DiceEngine.BeginRoll(h, "P1", "骰子不变量"); Choose(h, "P1", "observe");
        Assert.Empty(B3DiceEngine.Validate(h.State)); var roll = h.State.Dice.Roll;
        switch (mutation)
        {
            case "骰面": roll.Candidates[0].Red = 8; break;
            case "重复候选": roll.Candidates[1].Index = 0; break;
            case "选项": roll.SelectedIndices[1] = 2; break;
            case "用途": roll.Purpose = (B3DicePurpose)99; break;
            case "时钟": roll.TurnInstanceId++; break;
            case "待决": h.State.Dice.PendingDecision.PlayerId = "P99"; break;
            case "提前提交": roll.Submitted = true; break;
            case "旧规则": h.State.RulesVersion = GeneralCatalog.B2RulesVersion; break;
        }
        Assert.NotEmpty(B3DiceEngine.Validate(h.State));
    }

    [Fact]
    public void RuntimeInvariantsRequireGhostPaymentEvidenceAndPermitSubmittedPreviousTurn()
    {
        var h = Host("tk-g06"); GiveGhostCost(h, 1); B3DiceEngine.BeginRoll(h, "P1", "支付证据"); Choose(h, "P2", "modify", 0);
        Assert.Empty(B3DiceEngine.Validate(h.State)); var saved = h.Copy();
        h.State.Dice.Roll.Modifications[0].PaymentEventId = "未支付"; Assert.NotEmpty(B3DiceEngine.Validate(h.State));
        Assert.NotNull(B3DiceEngine.TakeFinal(saved)); saved.State.Clock.TurnInstanceId++; B3DiceEngine.BeginTurn(saved);
        Assert.Empty(B3DiceEngine.Validate(saved.State));
    }

    [Fact]
    public void RuntimeInvariantsRejectFinalResultThatDiffersFromSelectedCandidate()
    {
        var h = Host("tk-g06"); B3DiceEngine.BeginRoll(h, "P1", "最终选择一致性");
        Assert.Empty(B3DiceEngine.Validate(h.State));
        var result = h.State.Dice.Roll.Result; result.Red = result.Red == 6 ? 5 : 6;
        Assert.NotEmpty(B3DiceEngine.Validate(h.State));
    }
}
