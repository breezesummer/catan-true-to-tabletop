using System.Reflection;
using System.Text.Json.Nodes;
using Catan.Core;
using Xunit;
using M2 = Catan.Core.M2;
using M3 = Catan.Core.M3;
using M4 = Catan.Core.M4;
using M5 = Catan.Core.M5;

namespace Catan.Rules.Tests;

public sealed class VictoryPointSettingsTests
{
    private static dynamic Create(string rules, int? points = null) => rules switch
    {
        "base" => M2.BaseGameSession.Create(AcceptanceFixture.Json, 4, 97, points),
        "seafarers" => M3.SeafarersGameSession.Create("heading-for-new-shores", 4, 97, points),
        "cities" => M4.CitiesKnightsGameSession.Create(AcceptanceFixture.Json, 4, 97, points),
        "combined" => M5.CombinedGameSession.Create("heading-for-new-shores", 4, 97, points),
        _ => throw new ArgumentException(rules)
    };

    [Theory]
    [InlineData("base", 10)][InlineData("seafarers", 14)][InlineData("cities", 13)][InlineData("combined", 16)]
    public void DefaultsAndLegacySavesRetainOriginalGoals(string rules, int expected)
    {
        dynamic game = Create(rules);
        Assert.Equal(expected, (int)game.GetPlayerView("P1").TargetVictoryPoints);
        var adapter = new M6Game(rules, (object)game);
        dynamic command = adapter.Decide(adapter.View(M6Game.Actor(adapter.View())));
        command.Id = "旧存档-保留开局待决";
        adapter.Accept(command);
        var legacy = JsonNode.Parse((string)game.Save())!.AsObject();
        Assert.True(legacy.Remove("TargetVictoryPoints"));
        var restored = M6Game.Load(rules, legacy.ToJsonString());
        Assert.Equal(expected, (int)restored.View().TargetVictoryPoints);
        Assert.Equal((string)game.Save(), restored.Save());
    }

    [Theory]
    [InlineData("base")][InlineData("seafarers")][InlineData("cities")][InlineData("combined")]
    public void SelectedGoalIsPublicSavedAndResumesPendingDecision(string rules)
    {
        dynamic authority = Create(rules, 7);
        var game = new M6Game(rules, authority);
        dynamic view = game.View(M6Game.Actor(game.View()));
        dynamic command = game.Decide(view);
        command.Id = "胜利分数-开局待决";
        game.Accept(command);
        Assert.NotNull(game.View().PendingDecision);
        foreach (string seat in new[] { "P1", "P2", "P3", "P4" })
            Assert.Equal(7, (int)game.View(seat).TargetVictoryPoints);
        var restored = M6Game.Load(rules, game.Save());
        Assert.Equal(game.Save(), restored.Save());
        command = game.Decide(game.View(M6Game.Actor(game.View())));
        command.Id = "胜利分数-待决续跑";
        game.Accept(command); restored.Accept(command);
        Assert.Equal(game.Save(), restored.Save());
        game.AssertInvariants(); restored.AssertInvariants();
    }

    [Theory]
    [InlineData("base")][InlineData("seafarers")][InlineData("cities")][InlineData("combined")]
    public void OutOfRangeGoalsAreRejectedAtCreationLoadAndInvariantChecks(string rules)
    {
        foreach (int points in new[] { int.MinValue, 0, 4, 21, int.MaxValue })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => { Create(rules, points); });
            dynamic game = Create(rules);
            var altered = JsonNode.Parse((string)game.Save())!.AsObject();
            altered["TargetVictoryPoints"] = points;
            Assert.Throws<InvalidDataException>(() => M6Game.Load(rules, altered.ToJsonString()));
            dynamic state = game.GetAuthoritativeStateForTesting();
            state.TargetVictoryPoints = points;
            ((object)game).GetType().GetField("state", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, state);
            Assert.Throws<InvalidDataException>(() => { game.AssertInvariants(); });
        }
        foreach (int points in new[] { 5, 20 })
            Assert.Equal(points, (int)Create(rules, points).GetPlayerView("P1").TargetVictoryPoints);
    }

    private static dynamic FivePointFixture(string rules, int target) => rules switch
    {
        "base" => M2Harness.Fixture(s =>
        {
            s.TargetVictoryPoints = target;
            for (int i = 0; i < 5; i++) M2Harness.Card(s, M2.DevelopmentCardKind.VictoryPoint);
            M2Harness.Player(s).Resources.Wood = 4;
        }),
        "seafarers" => M3Harness.Fixture(s => { s.TargetVictoryPoints = target; M3Harness.Player(s).BonusVictoryPoints = 5; M3Harness.Player(s).Resources.Wood = 4; }),
        "cities" => M4CoreHarness.Fixture(s => { s.TargetVictoryPoints = target; M4CoreHarness.Player(s).DefenderPoints = 5; M4CoreHarness.Player(s).Resources.Wood = 4; }),
        "combined" => M5Harness.Fixture(s => { s.TargetVictoryPoints = target; M5Harness.Player(s).DefenderPoints = 5; M5Harness.Player(s).Resources.Wood = 4; }),
        _ => throw new ArgumentException(rules)
    };

    private static dynamic Command(string rules, string kind) => rules switch
    {
        "base" => M2Harness.Cmd(Enum.Parse<M2.CommandKind>(kind)),
        "seafarers" => M3Harness.Cmd(Enum.Parse<M3.CommandKind>(kind)),
        "cities" => M4CoreHarness.Cmd(Enum.Parse<M4.CommandKind>(kind)),
        "combined" => M5Harness.Cmd(Enum.Parse<M5.CommandKind>(kind)),
        _ => throw new ArgumentException(rules)
    };

    [Theory]
    [InlineData("base")][InlineData("seafarers")][InlineData("cities")][InlineData("combined")]
    public void VictoryUsesConfiguredThresholdAndRejectsEarlyDeclarationWithoutMutation(string rules)
    {
        dynamic below = FivePointFixture(rules, 6);
        string before = below.Save();
        dynamic result = below.Execute(Command(rules, "DeclareVictory"));
        Assert.False((bool)result.Success);
        Assert.Equal("InsufficientPoints", (string)result.ErrorCode);
        Assert.Equal(before, (string)below.Save());
        below.AssertInvariants();
        dynamic atGoal = FivePointFixture(rules, 5);
        Assert.True((bool)atGoal.Execute(Command(rules, "DeclareVictory")).Success);
        Assert.Equal("Finished", atGoal.GetPlayerView("P1").Phase.ToString());
        Assert.Equal("P1", (string)atGoal.GetPlayerView("P1").WinnerPlayerId);
        atGoal.AssertInvariants();
    }

    [Theory]
    [InlineData("base")][InlineData("seafarers")][InlineData("cities")][InlineData("combined")]
    public void AutomaticVictoryAfterLegalActionUsesConfiguredThreshold(string rules)
    {
        dynamic game = FivePointFixture(rules, 5);
        dynamic command = Command(rules, "BankTrade");
        command.GiveResource = Resource.Wood; command.ReceiveResource = Resource.Wheat;
        Assert.True((bool)game.Execute(command).Success);
        Assert.Equal("Finished", game.GetPlayerView("P1").Phase.ToString());
        game.AssertInvariants();
    }

    [Theory]
    [InlineData("base")][InlineData("seafarers")][InlineData("cities")][InlineData("combined")]
    public void ProductionAiCompletesCustomFivePointGameAndReloadsWinner(string rules)
    {
        var game = new M6Game(rules, (object)Create(rules, 5));
        for (int count = 0; count < 4000 && game.View().Phase.ToString() != "Finished"; count++)
        {
            dynamic command = game.Decide(game.View(M6Game.Actor(game.View())));
            command.Id = "自定义胜利-" + count;
            game.Accept(command);
        }
        Assert.Equal("Finished", game.View().Phase.ToString());
        Assert.Equal(5, (int)game.View().TargetVictoryPoints);
        Assert.True((int)game.View((string)game.View().WinnerPlayerId).OwnVictoryPoints >= 5);
        game.AssertInvariants();
        Assert.Equal(game.Save(), M6Game.Load(rules, game.Save()).Save());
    }

    [Theory]
    [InlineData(1, false)][InlineData(2, true)]
    public void CustomWonderGoalStillRequiresHigherWonderLevel(int level, bool wins)
    {
        var game = M3Harness.Fixture(s =>
        {
            s.TargetVictoryPoints = 7;
            M3Harness.Player(s).BonusVictoryPoints = 7;
            M3Harness.Player(s).WonderId = "grand-theater"; M3Harness.Player(s).WonderLevel = level;
            M3Harness.Player(s, "P2").WonderId = "grand-castle"; M3Harness.Player(s, "P2").WonderLevel = 1;
        }, "wonders-of-catan");
        if (wins) M3Harness.Accepted(game, M3Harness.Cmd(M3.CommandKind.DeclareVictory));
        else M3Harness.Rejected(game, M3Harness.Cmd(M3.CommandKind.DeclareVictory));
    }

    [Fact]
    public void FourWonderLevelsStillWinWithoutMeetingCustomPointGoal()
    {
        var game = M3Harness.Fixture(s =>
        {
            s.TargetVictoryPoints = 20;
            M3Harness.Player(s).WonderId = "grand-theater"; M3Harness.Player(s).WonderLevel = 4;
        }, "wonders-of-catan");
        M3Harness.Accepted(game, M3Harness.Cmd(M3.CommandKind.DeclareVictory));
        Assert.Equal(M3.GamePhase.Finished, game.GetPlayerView("P1").Phase);
    }

    [Fact]
    public void CustomPirateGoalStillRequiresCapturingOwnFortress()
    {
        var game = M3Harness.Fixture(s => { s.TargetVictoryPoints = 5; M3Harness.Player(s).BonusVictoryPoints = 5; }, "pirate-islands");
        M3Harness.Rejected(game, M3Harness.Cmd(M3.CommandKind.DeclareVictory));
    }

    [Theory]
    [InlineData(3)][InlineData(4)]
    public void PirateGoalCannotExceedItsThirteenBuildingPoints(int players)
    {
        var scenario = M3.SeafarersScenarios.Create("pirate-islands", players);
        var game = M3.SeafarersGameSession.Create("pirate-islands", players, 97, 13);
        var state = game.GetAuthoritativeStateForTesting();
        var player = state.Players[0];
        int settlements = player.Pieces.Settlements + state.Settlements.Count(b => b.PlayerId == player.Id) + state.Fortresses.Count(f => f.PlayerId == player.Id);
        int cities = player.Pieces.Cities + state.Cities.Count(b => b.PlayerId == player.Id);
        Assert.Equal(13, settlements + 2 * cities);
        Assert.False(scenario.UseLargestArmy); Assert.False(scenario.UseLongestRoute);
        Assert.Equal(0, scenario.IslandBonusPoints); Assert.Empty(scenario.BonusEdgeIds); Assert.Empty(scenario.Villages);
        Assert.DoesNotContain(M3.DevelopmentCardKind.VictoryPoint, state.DevelopmentDeck);
        Assert.Equal(13, game.GetPlayerView("P1").TargetVictoryPoints);
        Assert.Equal(game.Save(), M3.SeafarersGameSession.Load(game.Save()).Save());
        foreach (int invalid in new[] { 14, 20 })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => M3.SeafarersGameSession.Create("pirate-islands", players, 97, invalid));
            var corrupt = JsonNode.Parse(game.Save())!.AsObject(); corrupt["TargetVictoryPoints"] = invalid;
            Assert.Throws<InvalidDataException>(() => M3.SeafarersGameSession.Load(corrupt.ToJsonString()));
        }
        var legacy = JsonNode.Parse(M3.SeafarersGameSession.Create("pirate-islands", players, 97).Save())!.AsObject();
        legacy.Remove("TargetVictoryPoints");
        Assert.Equal(10, M3.SeafarersGameSession.Load(legacy.ToJsonString()).GetPlayerView("P1").TargetVictoryPoints);
        state.TargetVictoryPoints = 14;
        typeof(M3.SeafarersGameSession).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, state);
        Assert.Throws<InvalidDataException>(() => game.AssertInvariants());
    }
}
