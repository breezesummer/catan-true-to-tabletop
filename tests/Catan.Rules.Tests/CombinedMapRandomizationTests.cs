using System.Reflection;
using System.Text.Json.Nodes;
using Catan.Core.M5;
using Xunit;

namespace Catan.Rules.Tests;

public sealed class CombinedMapRandomizationTests
{
    public static IEnumerable<object[]> Maps()
    {
        foreach (var id in new[] { "heading-for-new-shores", "through-the-desert" })
        foreach (var players in new[] { 3, 4 }) yield return new object[] { id, players };
    }

    [Theory, MemberData(nameof(Maps))]
    public void NewMapsVaryTerrainNumbersAndPortsWhileTheSameSeedReplaysExactly(string id, int players)
    {
        var a = CombinedGameSession.Create(id, players, 123);
        var b = CombinedGameSession.Create(id, players, 123);
        var c = CombinedGameSession.Create(id, players, 456);
        Assert.Equal(a.Save(), b.Save());
        Assert.Equal("m5-combined-variable-2025-v002", a.GetAuthoritativeStateForTesting().ScenarioVersion);
        var first = a.GetPlayerView("P1").Board; var other = c.GetPlayerView("P1").Board;
        Assert.NotEqual(string.Join("|", first.Tiles.Select(t => t.Resource)), string.Join("|", other.Tiles.Select(t => t.Resource)));
        Assert.NotEqual(string.Join("|", first.Tiles.Select(t => t.Number)), string.Join("|", other.Tiles.Select(t => t.Number)));
        Assert.NotEqual(string.Join("|", first.Ports.Select(p => p.Resource)), string.Join("|", other.Ports.Select(p => p.Resource)));
    }

    [Theory, MemberData(nameof(Maps))]
    public void SeedSweepPreservesOfficialInventoryTopologyRegionsAndRedDiscRestrictions(string id, int players)
    {
        // 数量独立转录自官方 2025 航海家第 4/5、10/11 页，测试不复用生成器计算预期。
        var quantities = (id, players) switch
        {
            ("heading-for-new-shores", 3) => new[] { 13, 2, 4, 3, 5, 4, 4, 0 },
            ("heading-for-new-shores", 4) => new[] { 14, 2, 5, 5, 5, 5, 5, 1 },
            ("through-the-desert", 3) => new[] { 10, 2, 3, 5, 4, 4, 4, 3 },
            _ => new[] { 12, 2, 5, 5, 5, 5, 5, 3 }
        };
        var numberCounts = (id, players) switch
        {
            ("heading-for-new-shores", 3) => new[] { 1, 2, 3, 3, 2, 3, 2, 3, 2, 1 },
            ("heading-for-new-shores", 4) => new[] { 2, 3, 3, 3, 3, 3, 3, 3, 3, 1 },
            ("through-the-desert", 3) => new[] { 1, 2, 3, 3, 3, 3, 3, 2, 1, 1 },
            _ => new[] { 1, 3, 3, 3, 3, 3, 3, 3, 3, 2 }
        };
        var resources = new[] { "sea", "gold", "brick", "wood", "wool", "wheat", "ore", "desert" };
        var numbers = new[] { 2, 3, 4, 5, 6, 8, 9, 10, 11, 12 };
        var template = CombinedScenarios.Create(id, players);
        var main = template.StartingTileIds.ToHashSet();
        var seeds = Enumerable.Range(0, 256).Select(n => (uint)n).Concat(new[] { 0x9e3779b9u, uint.MaxValue });
        foreach (var seed in seeds)
        {
            var generated = CombinedScenarios.CreateSeeded(id, players, seed);
            Assert.Equal(quantities, resources.Select(r => generated.Tiles.Count(t => t.Resource == r)));
            Assert.Equal(numberCounts, numbers.Select(n => generated.Tiles.Count(t => t.Number == n)));
            Assert.Equal(RulesHarness.Canonical(template.Vertices), RulesHarness.Canonical(generated.Vertices));
            Assert.Equal(RulesHarness.Canonical(template.Edges), RulesHarness.Canonical(generated.Edges));
            Assert.Equal(RulesHarness.Canonical(template.Regions), RulesHarness.Canonical(generated.Regions));
            Assert.Equal(template.StartingTileIds, generated.StartingTileIds);
            Assert.Equal(template.Tiles.Select(t => (t.Id, t.Q, t.R)), generated.Tiles.Select(t => (t.Id, t.Q, t.R)));
            Assert.Equal(template.Tiles.Where(t => t.Resource == "sea").Select(t => t.Id), generated.Tiles.Where(t => t.Resource == "sea").Select(t => t.Id));
            Assert.Equal(template.Ports.Select(p => (p.Id, p.EdgeId)), generated.Ports.Select(p => (p.Id, p.EdgeId)));
            Assert.Equal(template.Ports.Select(p => p.Resource).Order(), generated.Ports.Select(p => p.Resource).Order());
            foreach (bool isMain in new[] { true, false })
            {
                Assert.Equal(template.Tiles.Where(t => main.Contains(t.Id) == isMain).Select(t => t.Resource).Order(),
                    generated.Tiles.Where(t => main.Contains(t.Id) == isMain).Select(t => t.Resource).Order());
                Assert.Equal(template.Tiles.Where(t => main.Contains(t.Id) == isMain).Select(t => t.Number).Order(),
                    generated.Tiles.Where(t => main.Contains(t.Id) == isMain).Select(t => t.Number).Order());
            }
            var red = generated.Tiles.Where(t => t.Number is 6 or 8).ToArray();
            foreach (var tile in red)
                Assert.DoesNotContain(red, other => other.Id != tile.Id && other.Vertices.Intersect(tile.Vertices).Count() == 2);
            Assert.All(generated.Tiles.Where(t => t.Resource is "sea" or "desert"), t => Assert.Null(t.Number));
            Assert.All(generated.Tiles.Where(t => t.Resource is not "sea" and not "desert"), t => Assert.NotNull(t.Number));
            if (id == "through-the-desert")
            {
                Assert.Equal(template.Tiles.Where(t => t.Resource == "desert").Select(t => t.Id), generated.Tiles.Where(t => t.Resource == "desert").Select(t => t.Id));
                Assert.DoesNotContain(generated.Tiles, t => t.Resource == "gold" && t.Number is 6 or 8);
            }
            else Assert.Equal(players == 3 ? 12 : null, generated.Tiles.Single(t => t.Id == generated.InitialRobberTileId).Number);
        }
    }

    [Theory, MemberData(nameof(Maps))]
    public void SetupSaveRestoresTheMapPendingDecisionAndRandomContinuation(string id, int players)
    {
        var game = CombinedGameSession.Create(id, players, 123);
        var before = game.GetPlayerView(game.GetPlayerView("P1").ActivePlayerId);
        var command = new Command { Id = "map-setup", PlayerId = before.ActivePlayerId, Kind = CommandKind.SetupSettlement, TargetId = before.LegalVertexIds.First() };
        Assert.True(game.Execute(command).Success);
        var save = game.Save(); var restored = CombinedGameSession.Load(save);
        Assert.Equal(save, restored.Save());
        var view = game.GetPlayerView(before.ActivePlayerId);
        var next = new Command { Id = "map-setup-road", PlayerId = view.ActivePlayerId, Kind = CommandKind.SetupRoad, TargetId = view.LegalEdgeIds.First() };
        Assert.True(game.Execute(next).Success); Assert.True(restored.Execute(next).Success);
        Assert.Equal(game.Save(), restored.Save()); game.AssertInvariants(); restored.AssertInvariants();
    }

    [Theory, MemberData(nameof(Maps))]
    public void IllegalSetupAndTamperedMapSeedOrVersionAreRejectedWithoutChangingAuthority(string id, int players)
    {
        var game = CombinedGameSession.Create(id, players, 123);
        var save = game.Save(); var view = game.GetPlayerView("P1");
        Assert.False(game.Execute(new Command { Id = "invalid-map", PlayerId = view.ActivePlayerId, Kind = CommandKind.SetupSettlement, TargetId = "unknown-vertex" }).Success);
        Assert.Equal(save, game.Save());
        foreach (var field in new[] { "initialSeed", "scenarioVersion" })
        {
            var node = JsonNode.Parse(save)!;
            // DataContract JSON uses the declared C# member names.
            node[field == "initialSeed" ? "InitialSeed" : "ScenarioVersion"] = field == "initialSeed" ? JsonValue.Create(456u) : JsonValue.Create("unknown-map-version");
            Assert.Throws<System.IO.InvalidDataException>(() => CombinedGameSession.Load(node.ToJsonString()));
            Assert.Equal(save, game.Save());
        }
        game.AssertInvariants();
    }

    [Theory, MemberData(nameof(Maps))]
    public void LegacySaveKeepsItsFixedBoardVersionAndContinuesThroughTheCurrentCommandEntrance(string id, int players)
    {
        var legacy = (CombinedGameSession)typeof(CombinedGameSession).GetMethod("CreateForVersion", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object?[] { id, players, 47u, null, "m5-combined-official-2025-v001" })!;
        var expected = CombinedScenarios.Create(id, players);
        Assert.Equal(RulesHarness.Canonical(expected.Tiles), RulesHarness.Canonical(legacy.GetPlayerView("P1").Board.Tiles));
        var save = legacy.Save(); var restored = CombinedGameSession.Load(save);
        Assert.Equal(save, restored.Save());
        var view = restored.GetPlayerView(restored.GetPlayerView("P1").ActivePlayerId);
        var command = new Command { Id = "legacy-setup", PlayerId = view.ActivePlayerId, Kind = CommandKind.SetupSettlement, TargetId = view.LegalVertexIds.First() };
        Assert.True(restored.Execute(command).Success); Assert.True(legacy.Execute(command).Success);
        Assert.Equal(legacy.Save(), restored.Save()); restored.AssertInvariants();
    }
}
