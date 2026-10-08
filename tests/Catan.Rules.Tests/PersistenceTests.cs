using System.Diagnostics;
using System.Text.Json.Nodes;
using Catan.Core;
using Xunit;
using static Catan.Rules.Tests.RulesHarness;

namespace Catan.Rules.Tests;

public sealed class PersistenceTests
{
    [Theory]
    [InlineData(7)]
    [InlineData(17)]
    [InlineData(20)]
    public void RequiredSavePointsRoundTripEveryAuthoritativeFieldAndContinueIdentically(int at)
    {
        var uninterrupted = Through(at);
        var saved = uninterrupted.Save();
        var resumed = GameSession.Load(AcceptanceFixture.Json, saved);
        Assert.Equal(saved, resumed.Save());
        Assert.Equal(Canonical(uninterrupted.GetAuthoritativeStateForTesting()), Canonical(resumed.GetAuthoritativeStateForTesting()));
        if (at == 7) PendingLedger(resumed);
        foreach (var command in MainCommands().Skip(at))
        {
            Accepted(uninterrupted, command);
            Accepted(resumed, command);
            Assert.Equal(uninterrupted.Save(), resumed.Save());
        }
        Ledger(resumed, "afterRestoreProbe");
    }

    [Fact]
    public void PendingSaveRestoresAnchorSeatAndPlacementBeforePermittingC08()
    {
        var game = Through(7);
        game = GameSession.Load(AcceptanceFixture.Json, game.Save());
        PendingLedger(game);
        var before = game.Save();
        var wrongSeat = new Command { Id = "PENDING-SEAT", PlayerId = "P3", Kind = CommandKind.SetupRoad, TargetId = "E56" };
        var wrongAnchor = new Command { Id = "PENDING-ANCHOR", PlayerId = "P4", Kind = CommandKind.SetupRoad, TargetId = "E01" };
        var wrongKind = new Command { Id = "PENDING-KIND", PlayerId = "P4", Kind = CommandKind.EndTurn };
        foreach (var command in new[] { wrongSeat, wrongAnchor, wrongKind })
        {
            Assert.False(game.Execute(command).Success);
            Assert.Equal(before, game.Save());
        }
        Accepted(game, MainCommands()[7]);
        Assert.Single(State(game)["roads"]!.AsArray(), p => p!.Text("locationId") == "E56" && p!.Text("playerId") == "P4");
        var after = game.Save();
        Assert.True(game.Execute(MainCommands()[7]).IsDuplicate);
        Assert.Equal(after, game.Save());
    }

    [Theory]
    [InlineData("saveFormatVersion", "99")]
    [InlineData("rulesVersion", "\"unknown-rules\"")]
    [InlineData("rulesBaselineId", "\"unknown-baseline\"")]
    [InlineData("scenarioVersion", "\"999.0.0\"")]
    [InlineData("scenarioId", "\"unknown-scenario\"")]
    [InlineData("scenarioContentHash", "\"bad-content-hash\"")]
    public void UnknownSaveRulesAndScenarioVersionsAreExplicitlyRejected(string key, string replacement)
    {
        var save = JsonNode.Parse(Through(17).Save())!.AsObject();
        Assert.True(save.ContainsKey(key), $"Missing mandatory save metadata: {key}");
        save[key] = JsonNode.Parse(replacement);
        var failure = Assert.Throws<InvalidDataException>(() => GameSession.Load(AcceptanceFixture.Json, save.ToJsonString()));
        Assert.False(string.IsNullOrWhiteSpace(failure.Message));
    }

    [Theory]
    [InlineData("randomCursor")]
    [InlineData("hand")]
    [InlineData("pieces")]
    [InlineData("pendingAnchor")]
    [InlineData("dedupFingerprint")]
    [InlineData("eventSequence")]
    public void CorruptAuthoritativeSaveFieldsAreRejectedRatherThanSilentlyRepaired(string corruption)
    {
        var save = JsonNode.Parse(Through(7).Save())!;
        switch (corruption)
        {
            case "randomCursor": save["random"]!["cursor"] = 1; break;
            case "hand": save["players"]![0]!["resources"]!["wood"] = 1; break;
            case "pieces": save["players"]![0]!["pieces"]!["roads"] = 0; break;
            case "pendingAnchor": save["pendingDecision"]!["anchorVertexId"] = "V01"; break;
            case "dedupFingerprint": save["processedCommands"]![0]!["fingerprint"] = "corrupt"; break;
            case "eventSequence": save["eventSequence"] = 999; break;
        }
        Assert.Throws<InvalidDataException>(() => GameSession.Load(AcceptanceFixture.Json, save.ToJsonString()));
    }

    [Fact]
    public void SaveContainsVersionsFullRandomContinuationAndConfirmedRequestReceipts()
    {
        var fixture = AcceptanceFixture.Read();
        var state = JsonNode.Parse(Through(20).Save())!;
        foreach (var key in new[] { "rulesVersion", "rulesBaselineId", "scenarioId", "scenarioVersion" })
            Assert.Equal(fixture.Text(key), state.Text(key));
        Assert.True(state.Number("saveFormatVersion") > 0);
        Assert.False(string.IsNullOrEmpty(state.Text("scenarioContentHash")));
        Assert.Equal(fixture["controlledRandom"]!.Text("algorithmId"), state["random"]!.Text("algorithmId"));
        Assert.Equal(fixture["controlledRandom"]!["dice"]!.ToJsonString(), state["random"]!["dice"]!.ToJsonString());
        Assert.Equal(1, state["random"]!.Number("cursor"));
        Assert.Equal(20, state["processedCommands"]!.AsArray().Count);
        Assert.All(state["processedCommands"]!.AsArray(), command =>
        {
            Assert.False(string.IsNullOrWhiteSpace(command!.Text("fingerprint")));
            Assert.NotNull(command!["command"]);
            Assert.NotEmpty(command["events"]!.AsArray());
        });
    }

    [Fact]
    public void C20SaveSurvivesProcessExitAndNewProcessC21PreservesNextDice()
    {
        var repository = FindRepository();
        var configuration = Environment.GetEnvironmentVariable("CATAN_TEST_CONFIGURATION")
            ?? new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var probeDll = Path.Combine(repository, "tests", "Catan.SaveProbe", "bin", configuration, "net10.0", "Catan.SaveProbe.dll");
        Assert.True(File.Exists(probeDll), $"Missing built independent process probe: {probeDll}");
        var caseDirectory = Path.Combine(repository, ".local", "m1", "test-processes", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(caseDirectory);
        var diskSave = Path.Combine(caseDirectory, "after-c20.json");
        var diskRestored = Path.Combine(caseDirectory, "loaded-c20.json");
        var diskProbe = Path.Combine(caseDirectory, "after-c21.json");
        var writer = RunProbe(probeDll, "write", diskSave, diskRestored, diskProbe);
        var reader = RunProbe(probeDll, "read", diskSave, diskRestored, diskProbe);
        Assert.NotEqual(writer, reader);
        var uninterrupted = Through(20);
        Assert.Equal(uninterrupted.Save(), File.ReadAllText(diskSave));
        Assert.Equal(uninterrupted.Save(), File.ReadAllText(diskRestored));
        Accepted(uninterrupted, MainCommands()[20]);
        Assert.Equal(uninterrupted.Save(), File.ReadAllText(diskProbe));
        var restored = GameSession.Load(AcceptanceFixture.Json, File.ReadAllText(diskProbe));
        Ledger(restored, "afterRestoreProbe");
    }

    private static void PendingLedger(GameSession game)
    {
        var expected = AcceptanceFixture.Read()["pendingSave"]!;
        var state = State(game);
        Assert.Equal(expected.Text("activePlayer"), state.Text("activePlayerId"));
        Assert.Equal(expected.Text("phase"), state.Text("phase"));
        Assert.Equal(AcceptanceFixture.Normalize(expected["pendingDecision"]!).ToJsonString(),
            AcceptanceFixture.Normalize(state["pendingDecision"]!).ToJsonString());
        Assert.Equal(expected.Number("rngCursor"), state["random"]!.Number("cursor"));
        AssertResources(expected["bank"]!, state["bank"]!);
        foreach (var player in state["players"]!.AsArray())
        {
            var id = player!.Text("id");
            AssertResources(expected["hands"]![id]!, player!["resources"]!);
            AssertPieces(expected["remainingPieces"]![id]!, player["pieces"]!);
            foreach (var collection in new[] { "settlements", "roads" })
                Assert.Equal(expected[collection]![id]!.Texts().Order(), state[collection]!.AsArray()
                    .Where(piece => piece!.Text("playerId") == id).Select(piece => piece!.Text("locationId")).Order());
        }
        Invariants(game);
    }

    private static int RunProbe(string probeDll, string mode, params string[] files)
    {
        var executable = Environment.GetEnvironmentVariable("CATAN_DOTNET") ?? "dotnet";
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in new[] { probeDll, mode, AcceptanceFixture.PathOnDisk }.Concat(files))
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var id = process.Id;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail("Independent persistence probe exceeded 30 seconds.");
        }
        Assert.True(process.ExitCode == 0, $"Probe {mode} failed: {output.GetAwaiter().GetResult()} {error.GetAwaiter().GetResult()}");
        return id;
    }

    private static string FindRepository()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "docs", "acceptance", "v0.1", "scenario.json"))) return directory.FullName;
        throw new DirectoryNotFoundException("Unable to locate the versioned acceptance fixture repository.");
    }
}
