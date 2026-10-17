using Catan.Core;
using System.Text.Json.Nodes;
using M4 = Catan.Core.M4;
using M5 = Catan.Core.M5;
using Xunit;

namespace Catan.Rules.Tests;

public class BarbarianBattleTests
{
    // 官方 2025 城市与骑士规则 PDF 第 11 页：比较一次力量，再结算所有并列最低贡献者。
    [Theory]
    [InlineData("cities")]
    [InlineData("combined")]
    public void TiedPillageKeepsAttackStrengthAfterFirstCityIsLost(string rules)
    {
        dynamic game = Battle(rules, 4, 3);
        Accept(game, Command(rules, "RollDice", "P4"));
        Assert.Equal(4, (int)game.GetPlayerView("P1").BarbarianStrength);
        Assert.Equal("P4", (string)game.GetPlayerView("P1").PendingDecision.PlayerId);

        ResolvePillage(game, rules);
        dynamic view = game.GetPlayerView("P1");
        Assert.Equal(3, (int)view.Board.Cities.Length);
        Assert.Equal(3, (int)view.DefenderStrength);
        Assert.Equal("P1", (string)view.PendingDecision.PlayerId);
        Assert.Equal("PillageCity", (string)view.PendingDecision.Kind);
        Assert.Equal(4, (int)view.BarbarianStrength);

        RejectUnchanged(game, Command(rules, "ResolveChoice", "P2", view.PendingDecision.Options[0]));
        RejectUnchanged(game, Command(rules, "ResolveChoice", "P1", "不存在的城市"));
        RejectUnchanged(game, Command(rules, "EndTurn", "P4"));
        ResolvePillage(game, rules);
        Assert.Equal(4, (int)game.GetPlayerView("P3").BarbarianStrength);
        ResolvePillage(game, rules);
        view = game.GetPlayerView("P1");
        Assert.Equal(0, (int)view.BarbarianPosition);
        Assert.Equal(1, (int)view.BarbarianStrength);
        Assert.Equal(0, (int)view.DefenderStrength);
        game.AssertInvariants();
    }

    [Theory]
    [InlineData("cities", 3)]
    [InlineData("cities", 4)]
    [InlineData("combined", 3)]
    [InlineData("combined", 4)]
    public void EqualOrGreaterDefenseNeverPillagesAnyCity(string rules, int strength)
    {
        dynamic game = Battle(rules, 3, strength);
        Accept(game, Command(rules, "RollDice", "P4"));
        dynamic view = game.GetPlayerView("P1");
        Assert.Equal(3, (int)view.Board.Cities.Length);
        Assert.Equal(0, (int)view.BarbarianPosition);
        Assert.Equal(1, (int)((Array)view.Players).Cast<dynamic>().Single(p => (string)p.Id == "P2").DefenderPoints);
        Assert.NotEqual("PillageCity", (string?)view.PendingDecision?.Kind);
        RejectUnchanged(game, Command(rules, "ResolveChoice", "P1", view.Board.Cities[0].LocationId));
    }

    [Theory]
    [InlineData("cities")]
    [InlineData("combined")]
    public void MidBattleSaveAndLegacySaveReplayOriginalStrengthAndRemainingPillages(string rules)
    {
        dynamic game = rules == "cities" ? (object)M4CoreHarness.New(seed: 53) : M5Harness.New(seed: 53);
        dynamic driver = rules == "cities" ? (object)new M4ViewOnlyDriver() : new M5ViewOnlyDriver();
        for (int n = 0; n < 500 && game.GetPlayerView("P1").PendingDecision?.Kind != "PillageCity"; n++)
        {
            dynamic view = game.GetPlayerView("P1");
            string actor = view.Phase.ToString() == "Discard" ? view.Discards[0].PlayerId : view.PendingDecision?.PlayerId ?? view.ActivePlayerId;
            dynamic seat = game.GetPlayerView(actor);
            dynamic next = seat.Phase.ToString() == "Action" ? Command(rules, "EndTurn", actor) : driver.Decide(seat);
            next.Id = "蛮族存档复现-" + n;
            Accept(game, next);
        }
        Assert.Equal("PillageCity", (string)game.GetPlayerView("P1").PendingDecision.Kind);
        ResolvePillage(game, rules);
        dynamic pending = game.GetPlayerView("P1");
        Assert.Equal(3, (int)pending.Board.Cities.Length);
        Assert.Equal(4, (int)pending.BarbarianStrength);
        string save = game.Save();
        dynamic loaded = Load(rules, save);
        Assert.Equal(save, (string)loaded.Save());

        var legacy = JsonNode.Parse(save)!.AsObject();
        Assert.True(legacy.Remove("BarbarianAttackStrength"));
        Assert.True(legacy.Remove("BarbarianDefenseStrength"));
        dynamic migrated = Load(rules, legacy.ToJsonString());
        Assert.Equal(save, (string)migrated.Save());
        Assert.Equal(4, (int)migrated.GetPlayerView("P1").BarbarianStrength);
        foreach (string field in new[] { "BarbarianAttackStrength", "BarbarianDefenseStrength" })
        {
            var forged = JsonNode.Parse(save)!;
            forged[field] = forged[field]!.GetValue<int>() + 1;
            Assert.Throws<InvalidDataException>(() => { Load(rules, forged.ToJsonString()); });
            forged = JsonNode.Parse(save)!;
            forged.AsObject().Remove(field);
            Assert.Throws<InvalidDataException>(() => { Load(rules, forged.ToJsonString()); });
        }

        while (game.GetPlayerView("P1").PendingDecision?.Kind == "PillageCity")
        {
            dynamic decision = game.GetPlayerView("P1").PendingDecision;
            dynamic next = Command(rules, "ResolveChoice", decision.PlayerId, decision.Options[0]);
            Accept(game, next); Accept(loaded, next); Accept(migrated, next);
            Assert.Equal((string)game.Save(), (string)loaded.Save());
            Assert.Equal((string)game.Save(), (string)migrated.Save());
        }
        Assert.Equal(0, (int)game.GetPlayerView("P1").BarbarianPosition);
        Assert.Null(JsonNode.Parse((string)game.Save())!["BarbarianAttackStrength"]);
        Assert.Null(JsonNode.Parse((string)game.Save())!["BarbarianDefenseStrength"]);
    }

    private static dynamic Load(string rules, string save) => rules == "cities"
        ? (object)M4.CitiesKnightsGameSession.Load(AcceptanceFixture.Json, save)
        : M5.CombinedGameSession.Load(save);

    private static dynamic Battle(string rules, int cities, int strength)
    {
        dynamic template = rules == "cities" ? (object)M4CoreHarness.New() : M5Harness.New();
        dynamic board = template.GetPlayerView("P1").Board;
        var sites = new List<string>();
        foreach (var vertex in (Array)board.Vertices)
        {
            string id = ((dynamic)vertex).Id;
            if (!sites.Any(s => ((Array)board.Edges).Cast<dynamic>().Any(e => ((string[])e.Vertices).Contains(s) && ((string[])e.Vertices).Contains(id)))) sites.Add(id);
            if (sites.Count == 5) break;
        }
        void Configure(dynamic s)
        {
            s.ActivePlayerId = "P4";
            s.BarbarianPosition = 6;
            s.AlchemyDice1 = 3; s.AlchemyDice2 = 4;
            s.Random.State = 1u; // 下次事件骰为蛮族，生产骰为 7，避免产出混入断言。
            s.Cities = Enumerable.Range(0, cities).Select(i => new PiecePlacement { PlayerId = "P" + (i + 1), LocationId = sites[i] }).ToArray();
        }
        if (rules == "cities") return M4CoreHarness.Fixture(s =>
        {
            Configure(s); s.Phase = M4.GamePhase.ProductionAwaitRoll;
            s.Knights = strength == 4
                ? new[] { new M4.Knight { PlayerId = "P2", LocationId = sites[4], Level = 3, Active = true }, new M4.Knight { PlayerId = "P2", LocationId = sites[3], Level = 1, Active = true } }
                : new[] { new M4.Knight { PlayerId = "P2", LocationId = sites[4], Level = strength, Active = true } };
        });
        return M5Harness.Fixture(s =>
        {
            Configure(s); s.Phase = M5.GamePhase.ProductionAwaitRoll;
            s.Knights = strength == 4
                ? new[] { new M5.Knight { PlayerId = "P2", LocationId = sites[4], Level = 3, Active = true }, new M5.Knight { PlayerId = "P2", LocationId = sites[3], Level = 1, Active = true } }
                : new[] { new M5.Knight { PlayerId = "P2", LocationId = sites[4], Level = strength, Active = true } };
        });
    }

    private static dynamic Command(string rules, string kind, string player, string? target = null) => rules == "cities"
        ? (object)M4CoreHarness.Cmd(Enum.Parse<M4.CommandKind>(kind), player, target)
        : M5Harness.Cmd(Enum.Parse<M5.CommandKind>(kind), player, target);
    private static void Accept(dynamic game, dynamic command)
    {
        dynamic result = game.Execute(command);
        Assert.True((bool)result.Success, (string)result.Message);
        game.AssertInvariants();
    }
    private static void ResolvePillage(dynamic game, string rules)
    {
        dynamic decision = game.GetPlayerView("P1").PendingDecision;
        Accept(game, Command(rules, "ResolveChoice", decision.PlayerId, decision.Options[0]));
    }
    private static void RejectUnchanged(dynamic game, dynamic command)
    {
        string before = game.Save();
        Assert.False((bool)game.Execute(command).Success);
        Assert.Equal(before, (string)game.Save());
        game.AssertInvariants();
    }
}
