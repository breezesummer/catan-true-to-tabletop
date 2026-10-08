using System.Reflection;
using System.Runtime.Versioning;
using Catan.Core;
using Xunit;
using static Catan.Rules.Tests.RulesHarness;

namespace Catan.Rules.Tests;

public sealed class AcceptanceTests
{
    [Fact]
    public void CoreTargetsUnityCompatibleFrameworkWithoutUnityDependencies()
    {
        var assembly = typeof(GameSession).Assembly;
        Assert.Equal(".NETStandard,Version=v2.1", assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), name => name.Name!.StartsWith("Unity", StringComparison.Ordinal));
    }

    [Fact]
    public void FullC01ToC21SequenceMatchesEveryFrozenLedger()
    {
        var game = New();
        Invariants(game);
        var commands = MainCommands();
        Assert.Equal(21, commands.Count);
        var checkpoints = new Dictionary<int, string>
        {
            [16] = "afterSetup", [17] = "afterRoll", [18] = "afterTrade",
            [19] = "afterBuild", [20] = "afterEnd", [21] = "afterRestoreProbe"
        };
        for (var i = 0; i < commands.Count; i++)
        {
            var result = Accepted(game, commands[i]);
            Assert.NotEmpty(result.Events);
            var state = State(game);
            Assert.Equal(i + 1, state["processedCommands"]!.AsArray().Count);
            if (i < 16)
            {
                var placement = AcceptanceFixture.Read()["setup"]![i / 2]!;
                var collection = i % 2 == 0 ? "settlements" : "roads";
                var expectedId = placement.Text(i % 2 == 0 ? "vertex" : "edge");
                Assert.Contains(state[collection]!.AsArray(), piece =>
                    piece!.Text("locationId") == expectedId && piece!.Text("playerId") == placement.Text("player"));
                if (i < 15)
                {
                    // Initial resources are awarded together after C16, never on first or partial placements.
                    foreach (var player in state["players"]!.AsArray())
                        Assert.Equal(new[] { 0, 0, 0, 0, 0 }, AcceptanceFixture.ResourceVector(player!["resources"]!));
                }
            }
            if (checkpoints.TryGetValue(i + 1, out var checkpoint)) Ledger(game, checkpoint);
        }
        Assert.Equal(9, State(game)["roads"]!.AsArray().Count);
        Assert.Equal(8, State(game)["settlements"]!.AsArray().Count);
    }

    [Theory]
    [InlineData("N01", 2)]
    [InlineData("N02", 7)]
    [InlineData("N03", 16)]
    [InlineData("N04", 17)]
    [InlineData("N05", 17)]
    [InlineData("N06", 18)]
    [InlineData("N07", 18)]
    [InlineData("N08", 19)]
    [InlineData("N09", 17)]
    [InlineData("N10", 7)]
    public void FrozenIllegalCasesRejectWithoutAnyAuthoritativeChange(string caseId, int at)
    {
        var game = Through(at);
        var example = AcceptanceFixture.Read()["negativeCases"]!.AsArray().Single(n => n!.Text("id") == caseId)!;
        var before = game.Save();
        var preview = game.Preview(CommandFrom(example));
        Assert.False(preview.Success);
        Assert.Empty(preview.NewEvents);
        Assert.Equal(before, game.Save());
        var result = game.Execute(CommandFrom(example));
        Assert.False(result.Success);
        Assert.False(result.IsDuplicate);
        var expectedCode = caseId == "N02" ? "RoadNotAtAnchor" : example.Text("reason");
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Equal(preview.ErrorCode, result.ErrorCode);
        Assert.Empty(result.NewEvents);
        Assert.Equal(before, game.Save());
        Invariants(game);
    }

    [Theory]
    [InlineData(17, false)]
    [InlineData(18, false)]
    [InlineData(19, false)]
    [InlineData(17, true)]
    [InlineData(18, true)]
    [InlineData(19, true)]
    public void ProductionTradeAndRoadRetriesReturnOriginalResultWithoutPublishingAgain(int index, bool restore)
    {
        var game = Through(index - 1);
        var command = MainCommands()[index - 1];
        var first = Accepted(game, command);
        if (restore) game = GameSession.Load(AcceptanceFixture.Json, game.Save());
        var before = game.Save();
        var retried = game.Execute(command);
        Assert.True(retried.Success);
        Assert.True(retried.IsDuplicate);
        Assert.Equal(first.ErrorCode, retried.ErrorCode);
        Assert.Equal(first.Message, retried.Message);
        Assert.Equal(Canonical(first.Events), Canonical(retried.Events));
        Assert.Empty(retried.NewEvents);
        Assert.Equal(before, game.Save());
        Invariants(game);
    }

    [Theory]
    [InlineData(17, false)]
    [InlineData(18, false)]
    [InlineData(19, false)]
    [InlineData(17, true)]
    [InlineData(18, true)]
    [InlineData(19, true)]
    public void ReusingAnAcceptedCommandIdWithChangedPayloadRejects(int index, bool restore)
    {
        var game = Through(index);
        if (restore) game = GameSession.Load(AcceptanceFixture.Json, game.Save());
        var command = MainCommands()[index - 1];
        if (index == 17) command.PlayerId = "P2";
        else if (index == 18) command.ReceiveResource = Resource.Ore;
        else command.TargetId = "E70";
        var before = game.Save();
        var result = game.Execute(command);
        Assert.False(result.Success);
        Assert.Equal("CommandIdConflict", result.ErrorCode);
        Assert.Empty(result.NewEvents);
        Assert.Equal(before, game.Save());
    }

    [Fact]
    public void ControlledDiceQueueStopsRatherThanCyclingOrFabricatingAnotherRoll()
    {
        var game = Through(21);
        Accepted(game, new Command { Id = "Q-END", PlayerId = "P2", Kind = CommandKind.EndTurn });
        var before = game.Save();
        var result = game.Execute(new Command { Id = "Q-ROLL", PlayerId = "P3", Kind = CommandKind.RollDice });
        Assert.False(result.Success);
        Assert.Equal("TestConfigurationEnded", result.ErrorCode);
        Assert.Equal(before, game.Save());
        Assert.Empty(result.NewEvents);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(18)]
    public void PreviewAndCancelledGestureDoNotCommitRulesOrConsumeCommandIds(int index)
    {
        var game = Through(index);
        var next = MainCommands()[index];
        var before = game.Save();
        for (var repetition = 0; repetition < 3; repetition++)
            Assert.True(game.Preview(next).Success);
        Assert.Equal(before, game.Save());
        // A cancelled visual gesture is represented by discarding the preview.
        var direct = GameSession.Load(AcceptanceFixture.Json, before);
        Accepted(game, next);
        Accepted(direct, next);
        Assert.Equal(direct.Save(), game.Save());
    }

    [Fact]
    public void RollDiceIntentCannotSupplyDiceOrRandomState()
    {
        var memberNames = typeof(Command).GetMembers(BindingFlags.Public | BindingFlags.Instance).Select(m => m.Name).ToArray();
        Assert.DoesNotContain(memberNames, name => name.Contains("Dice", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Random", StringComparison.OrdinalIgnoreCase) || name.Contains("Seed", StringComparison.OrdinalIgnoreCase));
    }
}
