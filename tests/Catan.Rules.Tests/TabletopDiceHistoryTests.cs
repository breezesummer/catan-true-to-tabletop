using Catan.AI;
using Catan.Core;
using Xunit;
using M2 = Catan.Core.M2;
using M3 = Catan.Core.M3;
using M4 = Catan.Core.M4;
using M5 = Catan.Core.M5;

namespace Catan.Rules.Tests;

public sealed class TabletopDiceHistoryTests
{
    [Fact]
    public void EmptyHistoryHasNoRollsAndNeverDividesByZero()
    {
        var history = new TabletopDiceHistory(null);
        Assert.Empty(history.Rolls);
        Assert.Equal(0, history.Total);
        Assert.Equal(0, history.MaximumCount);
        Assert.All(Enumerable.Range(2, 11), total =>
        {
            Assert.Equal(0, history.Count(total));
            Assert.Equal(0, history.Frequency(total));
        });
    }

    [Theory]
    [InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)][InlineData(6)][InlineData(7)]
    [InlineData(8)][InlineData(9)][InlineData(10)][InlineData(11)][InlineData(12)]
    public void EveryTwoDiceTotalHasItsOwnBucket(int total)
    {
        var history = new TabletopDiceHistory(new[] { Roll(total) });
        Assert.Equal(1, history.Total);
        Assert.Equal(1, history.Count(total));
        Assert.Equal(1, history.Frequency(total));
        Assert.Equal(1, Enumerable.Range(2, 11).Sum(history.Count));
        Assert.Equal(total, history.Rolls[0].Dice1 + history.Rolls[0].Dice2);
    }

    [Fact]
    public void EqualResultsAreSeparateRollsAndFrequenciesUseTheNumberOfRolls()
    {
        var rolls = new[] { Roll(2), Roll(7), Roll(7), Roll(12) };
        var history = new TabletopDiceHistory(rolls);
        Assert.Equal(4, history.Total);
        Assert.Equal(2, history.MaximumCount);
        Assert.Equal(0.25, history.Frequency(2));
        Assert.Equal(0.5, history.Frequency(7));
        Assert.Equal(0.25, history.Frequency(12));
        Assert.Equal(0, history.Frequency(6));
        Assert.Equal(1, Enumerable.Range(2, 11).Sum(history.Frequency));
        Assert.Equal(rolls, history.Rolls);
    }

    [Fact]
    public void PlayerFilterIncludesBothTheirOwnRollAndRollsWhoseProductionAffectsThem()
    {
        var tile = M4CoreHarness.New().GetPlayerView("P1").Board.Tiles.First(t => t.Resource == "wood");
        int producedTotal = tile.Number!.Value;
        var production = M4CoreHarness.Fixture(s =>
        {
            s.Phase = M4.GamePhase.ProductionAwaitRoll;
            s.AlchemyDice1 = Math.Min(6, producedTotal - 1);
            s.AlchemyDice2 = producedTotal - s.AlchemyDice1;
            s.Settlements = [M4CoreHarness.Piece(tile.Vertices[0], "P2")];
        });
        M4CoreHarness.Accepted(production, M4CoreHarness.Cmd(M4.CommandKind.RollDice));
        var affectedRoll = Assert.Single(production.GetHistory("P3"));
        var ownRoll = Roll(12, "P2");
        var unrelatedRoll = Roll(7, "P4");
        var filtered = new TabletopDiceHistory([affectedRoll, ownRoll, unrelatedRoll], "P2");
        Assert.Equal(new[] { affectedRoll, ownRoll }, filtered.Rolls);
        Assert.Equal(2, filtered.Total);
        Assert.Equal(0.5, filtered.Frequency(12));
        Assert.Equal(0.5, filtered.Frequency(producedTotal));
        Assert.Empty(new TabletopDiceHistory([affectedRoll, ownRoll, unrelatedRoll], "P3").Rolls);
    }

    [Fact]
    public void NonRollActionsRejectedRollsAndCommandRetriesDoNotInflateStatistics()
    {
        var game = M4CoreHarness.Fixture(s =>
        {
            s.Phase = M4.GamePhase.ProductionAwaitRoll;
            s.AlchemyDice1 = 3; s.AlchemyDice2 = 4;
        });
        var command = M4CoreHarness.Cmd(M4.CommandKind.RollDice);
        M4CoreHarness.Rejected(game, M4CoreHarness.Cmd(M4.CommandKind.RollDice, "P2"));
        Assert.Equal(0, new TabletopDiceHistory(game.GetHistory("P1")).Total);
        M4CoreHarness.Accepted(game, command);
        Assert.True(game.Execute(command).IsDuplicate);
        M4CoreHarness.Accepted(game, M4CoreHarness.Cmd(M4.CommandKind.EndTurn));
        var history = new TabletopDiceHistory(game.GetHistory("P1"));
        Assert.Equal(1, history.Total);
        Assert.Equal(1, history.Count(7));
        Assert.Equal(1, history.Frequency(7));
        Assert.Equal(2, game.GetHistory("P1").Length);
    }

    [Theory]
    [InlineData("base")][InlineData("seafarers")][InlineData("cities")][InlineData("combined")]
    public void ExistingAuthoritativeSavesRestoreTheSameRollsAndFilteredFrequencies(string rules)
    {
        dynamic game = New(rules);
        for (int i = 0; i < 45; i++)
        {
            dynamic publicView = game.GetPlayerView("P1");
            dynamic view = game.GetPlayerView(M6Game.Actor(publicView));
            dynamic command = Decide(rules, view);
            command.Id = "骰子统计恢复-" + i;
            Assert.True((bool)game.Execute(command).Success);
        }
        dynamic restored = Load(rules, game.Save());
        GameHistoryEntry[] original = game.GetHistory("P1");
        Assert.NotEmpty(new TabletopDiceHistory(original).Rolls);
        foreach (string player in new[] { "P1", "P2", "P3", "P4" })
        {
            GameHistoryEntry[] reloaded = restored.GetHistory(player);
            foreach (string? filter in new string?[] { null, "P1", "P2", "P3", "P4" })
            {
                var before = new TabletopDiceHistory(original, filter);
                var after = new TabletopDiceHistory(reloaded, filter);
                Assert.Equal(before.Rolls.Select(r => (r.Sequence, r.Turn, r.PlayerId, r.Dice1, r.Dice2, r.EventDie)),
                    after.Rolls.Select(r => (r.Sequence, r.Turn, r.PlayerId, r.Dice1, r.Dice2, r.EventDie)));
                Assert.Equal(Enumerable.Range(2, 11).Select(before.Count), Enumerable.Range(2, 11).Select(after.Count));
                Assert.Equal(Enumerable.Range(2, 11).Select(before.Frequency), Enumerable.Range(2, 11).Select(after.Frequency));
            }
        }
    }

    static GameHistoryEntry Roll(int total, string player = "P1")
    {
        var game = M4CoreHarness.Fixture(s =>
        {
            s.ActivePlayerId = player;
            s.Phase = M4.GamePhase.ProductionAwaitRoll;
            s.AlchemyDice1 = Math.Min(6, total - 1);
            s.AlchemyDice2 = total - s.AlchemyDice1;
        });
        M4CoreHarness.Accepted(game, M4CoreHarness.Cmd(M4.CommandKind.RollDice, player));
        return Assert.Single(game.GetHistory("P1"));
    }

    static object New(string rules) => rules switch
    {
        "base" => M2.BaseGameSession.Create(AcceptanceFixture.Json, 4, 97),
        "seafarers" => M3.SeafarersGameSession.Create("heading-for-new-shores", 4, 97),
        "cities" => M4.CitiesKnightsGameSession.Create(AcceptanceFixture.Json, 4, 97),
        _ => M5.CombinedGameSession.Create("heading-for-new-shores", 4, 97)
    };
    static object Load(string rules, string save) => rules switch
    {
        "base" => M2.BaseGameSession.Load(AcceptanceFixture.Json, save),
        "seafarers" => M3.SeafarersGameSession.Load(save),
        "cities" => M4.CitiesKnightsGameSession.Load(AcceptanceFixture.Json, save),
        _ => M5.CombinedGameSession.Load(save)
    };
    static object Decide(string rules, object view) => rules switch
    {
        "base" => new BaseGameAi().Decide((M2.PlayerView)view),
        "seafarers" => new SeafarersAi().Decide((M3.PlayerView)view),
        "cities" => new CitiesKnightsAi().Decide((M4.PlayerView)view),
        _ => new CombinedAi().Decide((M5.PlayerView)view)
    };
}
