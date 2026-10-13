using Catan.Core;
using Xunit;
using M5 = Catan.Core.M5;

namespace Catan.Rules.Tests;

public sealed class M6SetupStrategyTests
{
    [Theory]
    [InlineData("base")][InlineData("seafarers")][InlineData("cities")][InlineData("combined")]
    public void NormalOpeningChoosesHighFrequencyTileAndUsesCommonRuleEntry(string rules)
    {
        var game = M6Game.Create(rules);
        dynamic view = game.View(M6Game.Actor(game.View()));
        string original = M6Game.Json(view);
        dynamic command = game.Decide(view);
        Tile[] tiles = view.Board.Tiles;
        Assert.Contains(tiles, t => (t.Number == 6 || t.Number == 8) && t.Vertices.Contains((string)command.TargetId));
        Assert.Equal(original, M6Game.Json(view));
        string before = game.Save();
        command.Id = "策略初设";
        string actor = command.PlayerId;
        command.PlayerId = actor == "P1" ? "P2" : "P1";
        Assert.False((bool)game.Execute(command).Success);
        Assert.Equal(before, game.Save());
        command.PlayerId = actor;
        game.Accept(command);
        game.AssertInvariants();
        Assert.Equal(game.Save(), M6Game.Load(rules, game.Save()).Save());
    }

    [Theory]
    [InlineData("seafarers")][InlineData("combined")]
    public void EqualYieldOpeningFavorsWoolOverBrick(string rules)
    {
        var (game, view) = TwoCandidates(rules, "wool", "brick");
        Assert.Equal("A", (string)game.Decide(view).TargetId);
    }

    [Theory]
    [InlineData("cities")][InlineData("combined")]
    public void InitialCityFavorsPaperProductionForAqueduct(string rules)
    {
        var (game, view) = TwoCandidates(rules, "wood", "ore");
        Set(view.Board, "Settlements", new[] { new PiecePlacement { LocationId = "home", PlayerId = view.PlayerId } });
        Assert.Equal("A", (string)game.Decide(view).TargetId);
    }

    [Fact]
    public void RichBrickPortCanOutweighEqualWoolYield()
    {
        var (game, view) = TwoCandidates("combined", "brick", "wool");
        Set(view.Board, "Ports", new[] { new M5.Port { Id = "砖港", Vertices = new[] { "A", "port-end" }, Resource = Resource.Brick } });
        Set(view.Board, "Tiles", new[] { Tile("a1", "A", "brick", 6), Tile("a2", "A", "brick", 5), Tile("b1", "B", "wool", 6), Tile("b2", "B", "wool", 5) });
        Assert.Equal("A", (string)game.Decide(view).TargetId);
    }

    [Fact]
    public void UnproductiveBrickPortDoesNotAttractOpening()
    {
        var (game, view) = TwoCandidates("combined", "ore", "wool");
        Set(view.Board, "Ports", new[] { new M5.Port { Id = "砖港", Vertices = new[] { "A", "port-end" }, Resource = Resource.Brick } });
        Assert.Equal("B", (string)game.Decide(view).TargetId);
    }

    [Fact]
    public void SecondOpeningComplementsExistingProductionInsteadOfRepeatingWool()
    {
        var (game, view) = TwoCandidates("combined", "wool", "wheat");
        Set(view.Board, "Settlements", new[] { new PiecePlacement { LocationId = "home", PlayerId = view.PlayerId } });
        Set(view.Board, "Tiles", new[] { Tile("a", "A", "wool", 6), Tile("b", "B", "wheat", 6), Tile("h", "home", "wool", 8) });
        Assert.Equal("B", (string)game.Decide(view).TargetId);
    }

    [Fact]
    public void NoSixOrEightFallsBackToProductiveLegalVertex()
    {
        var (game, view) = TwoCandidates("combined", "wool", "wool");
        Set(view.Board, "Tiles", new[] { Tile("a", "A", "wool", 5), Tile("b", "B", "wool", 2) });
        Assert.Equal("A", (string)game.Decide(view).TargetId);
    }

    // 合成投影只隔离评估输入；上方正常初设案例另行验证真实规则入口和不变量。
    private static (M6Game game, dynamic view) TwoCandidates(string rules, string first, string second)
    {
        var game = M6Game.Create(rules);
        dynamic view = game.View(M6Game.Actor(game.View()));
        Set(view, "LegalVertexIds", new[] { "A", "B" });
        Set(view.Board, "Tiles", new[] { Tile("a", "A", first, 6), Tile("b", "B", second, 6) });
        Set(view.Board, "Edges", Array.Empty<Edge>());
        return (game, view);
    }

    private static Tile Tile(string id, string vertex, string resource, int number) => new()
    { Id = id, Vertices = new[] { vertex }, Resource = resource, Number = number };
    private static void Set(object value, string name, object content) => value.GetType().GetProperty(name)!.SetValue(value, content);
}
