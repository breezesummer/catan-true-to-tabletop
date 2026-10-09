using System.Diagnostics;
using System.Text.Json;
using Catan.AI;
using Xunit;
using Xunit.Abstractions;
using M2 = Catan.Core.M2;
using M3 = Catan.Core.M3;
using M4 = Catan.Core.M4;
using M5 = Catan.Core.M5;

namespace Catan.Rules.Tests;

/// <summary>Normal production sessions; only the observer can save or inspect authority.</summary>
public sealed class M6FullGameTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> Games()
    {
        foreach (uint seed in new uint[] { 97, 2027 })
        foreach (int seats in new[] { 3, 4 })
        {
            yield return new object[] { "base", "base", seats, seed };
            yield return new object[] { "cities", "base", seats, seed };
            foreach (string scenario in new[] { "heading-for-new-shores", "four-islands", "fog-islands", "through-the-desert", "forgotten-tribe", "cloth-for-catan", "pirate-islands", "wonders-of-catan", "new-world" })
                yield return new object[] { "seafarers", scenario, seats, seed };
            foreach (string scenario in new[] { "heading-for-new-shores", "through-the-desert" })
                yield return new object[] { "combined", scenario, seats, seed };
        }
    }

    [Theory]
    [MemberData(nameof(Games))]
    public void ProductionAiFinishesNormalGameAndResumesPendingSaves(string rules, string scenario, int seats, uint seed)
    {
        var clock = Stopwatch.StartNew();
        var game = M6Game.Create(rules, scenario, seats, seed);
        var savedDecisions = new HashSet<string>();
        var decisions = new HashSet<string>();
        var actions = new Dictionary<string, int>();
        int count = 0;
        while (game.View().Phase.ToString() != "Finished" && count < 10000)
        {
            dynamic status = game.View();
            dynamic view = game.View(M6Game.Actor(status));
            string decision = view.Phase + "/" + view.PendingDecision?.Kind + (view.TradeOffer != null ? "/TradeResponse" : "");
            decisions.Add(decision);
            bool checkDecision = savedDecisions.Add(decision);
            string? before = checkDecision ? game.Save() : null;
            string? projected = checkDecision ? M6Game.Json(view) : null;
            dynamic command = game.Decide(view);
            if (checkDecision)
            {
                // Same view, fresh policy and resumed authority must all produce the same intent.
                Assert.Equal(M6Game.Json(command), M6Game.Json(game.Decide(view)));
                Assert.Equal(projected, M6Game.Json(view));
                Assert.Equal(before, game.Save());
                var restored = M6Game.Load(rules, before!);
                Assert.Equal(before, restored.Save());
                Assert.Equal(M6Game.Json(command), M6Game.Json(restored.Decide(restored.View(M6Game.Actor(status)))));
                command.Id = $"m6-{count++}";
                Assert.Equal(projected, M6Game.Json(view)); // Returned LegalActions must be detached too.
                game.Accept(command);
                restored.Accept(command);
                Assert.Equal(game.Save(), restored.Save());
            }
            else
            {
                command.Id = $"m6-{count++}";
                game.Accept(command);
            }
            string kind = command.Kind.ToString();
            actions[kind] = actions.GetValueOrDefault(kind) + 1;
            game.AssertInvariants();
        }
        dynamic last = game.View();
        Assert.True(last.Phase.ToString() == "Finished", $"{rules}/{scenario}/{seats}/seed={seed}: {count} commands, turn={last.Turn}, no winner; actions={JsonSerializer.Serialize(actions)}");
        Assert.False(string.IsNullOrEmpty((string?)last.WinnerPlayerId));
        Assert.Throws<InvalidOperationException>(() => game.Decide(game.View((string)last.WinnerPlayerId)));
        Assert.Equal(game.Save(), M6Game.Load(rules, game.Save()).Save());
        output.WriteLine($"rules={rules}, scenario={scenario}, seats={seats}, seed={seed}, winner={last.WinnerPlayerId}, turn={last.Turn}, commands={count}, seconds={clock.Elapsed.TotalSeconds:F2}");
        output.WriteLine("saved-decisions=" + string.Join(",", savedDecisions.Order()));
        output.WriteLine("actions=" + JsonSerializer.Serialize(actions));
    }
}

/// <summary>Test-only adapter. Policy invocation below has exactly one input: a detached PlayerView.</summary>
internal sealed class M6Game(string rules, object session)
{
    internal dynamic View(string player = "P1") => ((dynamic)session).GetPlayerView(player);
    internal string Save() => ((dynamic)session).Save();
    internal void AssertInvariants() => ((dynamic)session).AssertInvariants();
    internal dynamic Execute(object command) => ((dynamic)session).Execute((dynamic)command);
    internal void Accept(object command)
    {
        dynamic c = command;
        dynamic r = Execute(command);
        Assert.True((bool)r.Success, $"{rules}/turn={View().Turn}/{View().Phase}/{View().PendingDecision?.Kind}: {Json(command)} -> {r.ErrorCode}: {r.Message}");
    }
    internal dynamic Decide(object view) => rules switch
    {
        "base" => new BaseGameAi().Decide((M2.PlayerView)view),
        "seafarers" => new SeafarersAi().Decide((M3.PlayerView)view),
        "cities" => new CitiesKnightsAi().Decide((M4.PlayerView)view),
        "combined" => new CombinedAi().Decide((M5.PlayerView)view),
        _ => throw new ArgumentException(rules)
    };
    internal static string Actor(dynamic view)
    {
        if (view.TradeOffer != null) return view.TradeOffer.OtherPlayerId;
        if (view.Phase.ToString() == "Discard") return view.Discards[0].PlayerId;
        if (view.Phase.ToString() == "GoldChoice") return view.GoldClaims[0].PlayerId;
        return view.PendingDecision?.PlayerId ?? view.ActivePlayerId;
    }
    internal static string Json(object value) => JsonSerializer.Serialize(value, value.GetType());
    internal static M6Game Create(string rules, string scenario = "heading-for-new-shores", int seats = 4, uint seed = 97) => new(rules, rules switch
    {
        "base" => M2.BaseGameSession.Create(AcceptanceFixture.Json, seats, seed),
        "seafarers" => M3.SeafarersGameSession.Create(scenario, seats, seed),
        "cities" => M4.CitiesKnightsGameSession.Create(AcceptanceFixture.Json, seats, seed),
        "combined" => M5.CombinedGameSession.Create(scenario, seats, seed),
        _ => throw new ArgumentException(rules)
    });
    internal static M6Game Load(string rules, string save) => new(rules, rules switch
    {
        "base" => M2.BaseGameSession.Load(AcceptanceFixture.Json, save),
        "seafarers" => M3.SeafarersGameSession.Load(save),
        "cities" => M4.CitiesKnightsGameSession.Load(AcceptanceFixture.Json, save),
        "combined" => M5.CombinedGameSession.Load(save),
        _ => throw new ArgumentException(rules)
    });
}
