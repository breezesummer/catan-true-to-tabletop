using System.Reflection;
using Catan.AI;
using Catan.Core.ThreeKingdoms;
using Xunit;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsXiashuAiTests
{
    private static B2SkillCommand? Choose(ThreeKingdomsView view, int[] hand, int[] desired, int other = 0) =>
        (B2SkillCommand?)typeof(CombinedAi).Assembly.GetType("Catan.AI.ThreeKingdomsAi")!
            .GetMethod("Choose", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [view, hand, desired, (Func<string, int>)(_ => other)]);

    private static ThreeKingdomsB2TestHost Host(bool direct)
    {
        var h = ThreeKingdomsB2TestHost.Create("tk-g26");
        h.State.RulesVersion = direct ? GeneralCatalog.B6RulesVersion : GeneralCatalog.B4RulesVersion;
        h.State.PackageVersion = direct ? GeneralCatalog.B6PackageVersion : GeneralCatalog.B4PackageVersion;
        h.State.PackageContentHash = direct ? GeneralCatalog.B6ContentHash : GeneralCatalog.B4ContentHash;
        return h;
    }

    private static ThreeKingdomsView View(ThreeKingdomsB2TestHost h, string player)
    {
        var view = ThreeKingdomsEngine.Project(h.State, player);
        B2SkillEngine.Project(h, view, player);
        return view;
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void AiSplitsMixedCardsAndTargetKeepsUnchosenPileAcrossSave(bool direct, bool takeShown)
    {
        var h = Host(direct);
        h.Give("P1", 1, 0, 0, 1); h.Give("P2", 4, 0, 0, 2, 0, 1, 0, 1);
        B2SkillEngine.Resolve(h, "P1", B2SkillEngine.Actions(h, "P1").First(a =>
            a.Command.Option == "b4-xiashu" && a.Command.TargetPlayerIds[0] == "P2").Command);
        int[] full = h.GetHand("P2"), desired = [0, 0, 0, 2, 0, 1, 0, 1];
        for (int step = 0; h.State.Skills.PendingDecision?.Kind == "b4-reveal" && step < 20; step++)
        {
            var view = View(h, "P2");
            var before = view.SkillActions.Select(a => a.Command.Counts.ToArray()).ToArray();
            var c = Choose(view, h.GetHand("P2"), desired)!;
            Assert.NotNull(c);
            for (int i = 0; i < before.Length; i++) Assert.Equal(before[i], view.SkillActions[i].Command.Counts);
            B2SkillEngine.Resolve(h, "P2", c);
            h = h.Copy();
        }
        Assert.Equal("b4-pile", h.State.Skills.PendingDecision?.Kind);
        var shown = h.State.Skills.RevealedCards.ToArray();
        Assert.InRange(shown.Sum(), 1, full.Sum() - 1);
        for (int i = 0; i < 8; i++) Assert.InRange(shown[i], full[i] / 2, (full[i] + 1) / 2);
        var piles = View(h, "P1");
        Assert.Equal(full.Sum(), piles.SkillActions.Sum(a => a.AvailableCount ?? 0));
        Assert.All(piles.SkillActions, a => Assert.All(a.Command.Counts, n => Assert.Equal(0, n)));
        Assert.Empty(View(h, "P3").SkillActions);
        string option = takeShown ? "b4-revealed" : "b4-hidden";
        B2SkillEngine.Resolve(h, "P1", piles.SkillActions.Single(a => a.Command.Option == option).Command);
        Assert.Equal(takeShown ? shown : full.Zip(shown, (n, s) => n - s).ToArray(), h.GetHand("P1"));
        Assert.Equal(takeShown ? full.Zip(shown, (n, s) => n - s).ToArray() : shown, h.GetHand("P2"));
        Assert.True(h.GetHand("P2").Sum() > 0);
        h.AssertConserved();
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(19, 10)]
    public void SingleTypeAndTinyHandsRemainLegal(int count, int expectedShown)
    {
        var h = Host(true); h.Give("P1", 1); h.Give("P2", Math.Max(0, count - 1));
        B2SkillEngine.Resolve(h, "P1", B2SkillEngine.Actions(h, "P1").First(a =>
            a.Command.Option == "b4-xiashu" && a.Command.TargetPlayerIds[0] == "P2").Command);
        // 零张是已存在待决的边界，单独由投影夹具检查，不伪造规则移牌。
        var hand = new int[8]; hand[0] = count;
        var command = Choose(View(h, "P2"), hand, new int[8])!;
        Assert.Equal("b45-selected", command.Option); Assert.Equal(expectedShown, command.Counts.Sum());
    }

    [Theory]
    [InlineData(0, 4, 0, "b4-hidden")]
    [InlineData(4, 0, 0, "b4-revealed")]
    [InlineData(2, 3, 2, "b4-revealed")]
    [InlineData(2, 3, 0, "b4-hidden")]
    public void ChoosesPileByCountAndNeededMaterials(int shown, int hidden, int need, string expected)
    {
        var view = new ThreeKingdomsView { SkillsEnabled = true, SkillDecision = new() { Kind = "b4-pile" },
            RevealedSkillCards = [shown, 0, 0, 0, 0, 0, 0, 0], SkillActions = [
                new() { AvailableCount = shown, Command = new() { Option = "b4-revealed" } },
                new() { AvailableCount = hidden, Command = new() { Option = "b4-hidden" } }] };
        Assert.Equal(expected, Choose(view, new int[8], [need, 0, 0, 0, 0, 0, 0, 0])!.Option);
    }

    [Fact]
    public void ActivationAccountsForGiftCostAndOpponentHandSize()
    {
        var view = new ThreeKingdomsView { SkillsEnabled = true, PackageVersion = GeneralCatalog.B6PackageVersion,
            SkillActions = [new() { Command = new() { Option = "b4-xiashu", TargetPlayerIds = ["P2"] } }] };
        Assert.Null(Choose(view, [4, 0, 0, 0, 0, 0, 0, 0], [4, 0, 0, 0, 0, 0, 0, 0], 1));
        Assert.Equal("b4-xiashu", Choose(view, [1, 0, 0, 0, 0, 0, 0, 0], new int[8], 8)!.Option);
    }
}
