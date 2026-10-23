using Catan.AI;
using Catan.Core.ThreeKingdoms;
using Xunit;
using M4 = Catan.Core.M4;
using M5 = Catan.Core.M5;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsAiProjectionTests
{
    [Theory]
    [InlineData("cities", false)]
    [InlineData("cities", true)]
    [InlineData("combined", false)]
    [InlineData("combined", true)]
    public void SkillResponderTakesPriorityOverUnderlyingTurnAndDiscardOwner(string rules, bool discard)
    {
        dynamic view = Projection(rules, discard);
        var input = new B2SkillCommand { SkillId = "tk-g37-s2", Option = "card", CardType = 0, Counts = new int[8],
            TargetPlayerIds = ["P1"], TileIds = ["tile-a"], DecisionId = "response-1", WindowId = "skill-window" };
        view.ThreeKingdoms.SkillActions = new[] { new B2SkillAction { Label = "选择木材", Command = input } };
        dynamic command = Decide(rules, view);
        Assert.Equal("P2", (string)command.PlayerId);
        Assert.Equal("UseGeneralSkill", command.Kind.ToString());
        Assert.Equal("response-1", (string)command.SkillCommand.DecisionId);
        Assert.Equal("skill-window", (string)command.SkillCommand.WindowId);
        Assert.Equal("outer-window", (string)command.WindowId);
        command.SkillCommand.Counts[0] = 9;
        command.SkillCommand.TargetPlayerIds[0] = "P3";
        command.SkillCommand.TileIds[0] = "changed";
        Assert.Equal(0, input.Counts[0]);
        Assert.Equal("P1", input.TargetPlayerIds[0]);
        Assert.Equal("tile-a", input.TileIds[0]);
        view.ThreeKingdoms.SkillDecision.PlayerId = "P3";
        Assert.Throws<InvalidOperationException>(() => Decide(rules, view));
    }

    [Theory]
    [InlineData("cities", "accept")]
    [InlineData("cities", "skip")]
    [InlineData("cities", "done")]
    [InlineData("cities", "target")]
    [InlineData("cities", "raid")]
    [InlineData("cities", "tiles")]
    [InlineData("cities", "color")]
    [InlineData("cities", "pass")]
    [InlineData("cities", "shelie")]
    [InlineData("combined", "accept")]
    [InlineData("combined", "skip")]
    [InlineData("combined", "done")]
    [InlineData("combined", "target")]
    [InlineData("combined", "raid")]
    [InlineData("combined", "tiles")]
    [InlineData("combined", "color")]
    [InlineData("combined", "pass")]
    [InlineData("combined", "shelie")]
    public void EveryConcreteSkillChoiceIncludingOnlySkipRemainsOperable(string rules, string option)
    {
        dynamic view = Projection(rules, false);
        view.ThreeKingdoms.SkillActions = new[] { new B2SkillAction { Label = "合法选择", Command = new B2SkillCommand {
            SkillId = "tk-g28-s1", Option = option, TargetPlayerIds = ["P1"], DecisionId = "response-1", WindowId = "skill-window", CardType = 2 } } };
        dynamic command = Decide(rules, view);
        Assert.Equal(option, (string)command.SkillCommand.Option);
        Assert.Equal("response-1", (string)command.SkillCommand.DecisionId);
    }

    private static object Projection(string rules, bool discard)
    {
        dynamic view;
        if (rules == "cities")
        {
            var city = M4.CitiesKnightsGameSession.Create(AcceptanceFixture.Json, 3, 419).GetPlayerView("P2");
            Set(city, "Phase", discard ? M4.GamePhase.Discard : M4.GamePhase.Action);
            Set(city, "Discards", discard ? new[] { new M4.DiscardRequirement { PlayerId = "P1", Amount = 4 } } : Array.Empty<M4.DiscardRequirement>());
            view = city;
        }
        else
        {
            var combined = M5.CombinedGameSession.Create("heading-for-new-shores", 3, 419).GetPlayerView("P2");
            Set(combined, "Phase", discard ? M5.GamePhase.Discard : M5.GamePhase.Action);
            Set(combined, "Discards", discard ? new[] { new M5.DiscardRequirement { PlayerId = "P1", Amount = 4 } } : Array.Empty<M5.DiscardRequirement>());
            view = combined;
        }
        Set(view, "ActivePlayerId", "P1");
        Set(view, "ThreeKingdoms", new ThreeKingdomsView { SkillsEnabled = true, WindowId = "outer-window", SkillDecision = new B2SkillDecision {
            PlayerId = "P2", DecisionId = "response-1", WindowId = "skill-window", Title = "遗计", Description = "选择 1 张资源。" } });
        return view;
    }

    private static object Decide(string rules, object view) => rules == "cities" ? new CitiesKnightsAi().Decide((M4.PlayerView)view) : new CombinedAi().Decide((M5.PlayerView)view);
    private static void Set(object view, string name, object value) => view.GetType().GetProperty(name)!.SetValue(view, value);
}
