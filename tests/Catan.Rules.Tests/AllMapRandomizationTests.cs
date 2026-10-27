using System.Reflection;
using Catan.Core.M3;
using Catan.Core.M5;
using Xunit;

namespace Catan.Rules.Tests;

public sealed class AllMapRandomizationTests
{
    public static IEnumerable<object[]> Maps() => SeafarersScenarios.Ids.SelectMany(id => new[] { 3, 4 }.Select(n => new object[] { id, n }));
    private static string Layout(IEnumerable<Catan.Core.Tile> tiles) => string.Join("|", tiles.Select(t => $"{t.Id}:{t.Resource}:{t.Number}"));

    [Theory, MemberData(nameof(Maps))]
    public void AllMapsVaryOrKeepDocumentedPirateLayoutAndReplayBothModes(string id, int players)
    {
        var a = CombinedGameSession.Create(id, players, 123);
        var b = CombinedGameSession.Create(id, players, 456);
        Assert.Equal(a.Save(), CombinedGameSession.Load(a.Save()).Save());
        Assert.Equal(a.Save(), CombinedGameSession.Create(id, players, 123).Save());
        var first = Layout(a.GetPlayerView("P1").Board.Tiles);
        var other = Layout(b.GetPlayerView("P1").Board.Tiles);
        if (id == "pirate-islands") Assert.Equal(first, other); else Assert.NotEqual(first, other);
        var sea = SeafarersGameSession.Create(id, players, 123);
        Assert.Equal(sea.Save(), SeafarersGameSession.Load(sea.Save()).Save());
        Assert.Equal(first, Layout(sea.GetPlayerView("P1").Board.Tiles));
        var seaOther = SeafarersGameSession.Create(id, players, 456);
        if (id == "pirate-islands") Assert.Equal(Layout(sea.GetPlayerView("P1").Board.Tiles), Layout(seaOther.GetPlayerView("P1").Board.Tiles));
        else Assert.NotEqual(Layout(sea.GetPlayerView("P1").Board.Tiles), Layout(seaOther.GetPlayerView("P1").Board.Tiles));
    }

    [Theory, MemberData(nameof(Maps))]
    public void LegacySavedMapsKeepTheirOriginalIdentityAndLayout(string id, int players)
    {
        var method = typeof(CombinedGameSession).GetMethod("CreateForVersion", BindingFlags.Static | BindingFlags.NonPublic)!;
        var old = (CombinedGameSession)method.Invoke(null, new object?[] { id, players, 123u, null, CombinedGameSession.AllScenarioVersion })!;
        var oldView = old.GetPlayerView("P1");
        var oldCommand = new Catan.AI.CombinedAi().Decide(old.GetAiPlayerView(oldView.PendingDecision?.PlayerId ?? oldView.ActivePlayerId));
        oldCommand.Id = "legacy-combined-map"; Assert.True(old.Execute(oldCommand).Success);
        Assert.Equal(old.Save(), CombinedGameSession.Load(old.Save()).Save());
        var seaMethod = typeof(SeafarersGameSession).GetMethod("CreateForVersion", BindingFlags.Static | BindingFlags.NonPublic)!;
        var sea = (SeafarersGameSession)seaMethod.Invoke(null, new object?[] { id, players, 123u, null, SeafarersGameSession.ScenarioVersion })!;
        var seaView = sea.GetPlayerView("P1");
        var seaCommand = new Catan.AI.SeafarersAi().Decide(sea.GetAiPlayerView(seaView.PendingDecision?.PlayerId ?? seaView.ActivePlayerId));
        seaCommand.Id = "legacy-seafarers-map"; Assert.True(sea.Execute(seaCommand).Success);
        Assert.Equal(sea.Save(), SeafarersGameSession.Load(sea.Save()).Save());
    }

    [Theory, MemberData(nameof(Maps))]
    public void SeedSweepPreservesComponentsSpecialMarkersAndVariableSetupRestrictions(string id, int players)
    {
        if (id is "heading-for-new-shores" or "through-the-desert" or "new-world" or "pirate-islands") return; // 既有专项覆盖。
        var template = SeafarersScenarios.Create(id, players);
        for (uint seed = 0; seed < 256; seed++)
        {
            var map = CombinedScenarios.CreateSeeded(id, players, seed);
            Assert.Equal(template.Tiles.Select(t => t.Resource).Order(), map.Tiles.Select(t => t.Resource).Order());
            Assert.Equal(template.Tiles.Select(t => t.Number).Order(), map.Tiles.Select(t => t.Number).Order());
            Assert.Equal(template.StartingTileIds, map.StartingTileIds);
            Assert.Equal(RulesHarness.Canonical(template.Regions), RulesHarness.Canonical(map.Regions));
            Assert.Equal(RulesHarness.Canonical(template.Vertices), RulesHarness.Canonical(map.Vertices));
            Assert.Equal(RulesHarness.Canonical(template.Edges), RulesHarness.Canonical(map.Edges));
            Assert.Equal(template.HiddenResources, map.HiddenResources); Assert.Equal(template.HiddenNumbers, map.HiddenNumbers);
            Assert.Equal(RulesHarness.Canonical(template.Villages), RulesHarness.Canonical(map.Villages));
            Assert.Equal(RulesHarness.Canonical(template.Wonders), RulesHarness.Canonical(map.Wonders));
            Assert.Equal(template.BonusEdgeIds, map.BonusEdgeIds); Assert.Equal(template.GiftCardEdgeIds, map.GiftCardEdgeIds);
            Assert.Equal(template.BannedSetupVertexIds, map.BannedSetupVertexIds);
            foreach (var fixedTile in template.Tiles.Where(t => !t.Number.HasValue || !template.StartingTileIds.Contains(t.Id)))
                Assert.Equal((fixedTile.Resource, fixedTile.Number), (map.Tiles.Single(t => t.Id == fixedTile.Id).Resource, map.Tiles.Single(t => t.Id == fixedTile.Id).Number));
            if (id == "four-islands") Assert.All(map.Tiles.Where(t => t.Resource is "wood" or "wool"), t => Assert.DoesNotContain(t.Number, new int?[] { 2, 3, 11, 12 }));
            if (id == "forgotten-tribe")
                Assert.All(map.Tiles.Where(t => (t.R == 2 && t.Q == 3) || (t.R == 3 && t.Q == 3) || (t.R == 4 && t.Q == 2)), t => Assert.DoesNotContain(t.Number, new int?[] { 5, 6, 8, 9 }));
            if (id == "wonders-of-catan") Assert.All(map.Tiles.Where(t => t.Number.HasValue && map.Tiles.Any(d => d.Resource == "desert" && t.Vertices.Intersect(d.Vertices).Count() == 2)), t => Assert.DoesNotContain(t.Number, new int?[] { 6, 8 }));
            if (id != "fog-islands") Assert.All(map.Tiles.Where(t => t.Number is 6 or 8), t => Assert.DoesNotContain(map.Tiles, other => other.Number is 6 or 8 && other.Id != t.Id && other.Vertices.Intersect(t.Vertices).Count() == 2));
            if (id is "four-islands" or "fog-islands" or "cloth-for-catan") Assert.Equal(12, map.Tiles.Single(t => t.Id == map.InitialRobberTileId).Number);
        }
    }
}
