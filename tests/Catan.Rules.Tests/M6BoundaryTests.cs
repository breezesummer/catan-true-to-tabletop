using System.Reflection;
using Catan.AI;
using Catan.Core;
using Xunit;
using M2 = Catan.Core.M2;
using M3 = Catan.Core.M3;
using M4 = Catan.Core.M4;
using M5 = Catan.Core.M5;

namespace Catan.Rules.Tests;

public sealed class M6BoundaryTests
{
    [Theory]
    [InlineData(typeof(BaseGameAi), typeof(M2.PlayerView), typeof(M2.Command))]
    [InlineData(typeof(SeafarersAi), typeof(M3.PlayerView), typeof(M3.Command))]
    [InlineData(typeof(CitiesKnightsAi), typeof(M4.PlayerView), typeof(M4.Command))]
    [InlineData(typeof(CombinedAi), typeof(M5.PlayerView), typeof(M5.Command))]
    public void PublicPolicyHasOnlyPlayerViewInputAndNoAuthorityOrServiceFields(Type policy, Type view, Type command)
    {
        var decide = Assert.Single(policy.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly));
        Assert.Equal("Decide", decide.Name);
        Assert.Equal(view, Assert.Single(decide.GetParameters()).ParameterType);
        Assert.Equal(command, decide.ReturnType);
        Assert.Empty(Assert.Single(policy.GetConstructors()).GetParameters());
        Assert.All(policy.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public), field =>
        {
            Assert.DoesNotContain("Session", field.FieldType.Name);
            Assert.DoesNotContain("GameState", field.FieldType.Name);
            Assert.DoesNotContain("Random", field.FieldType.Name);
            Assert.False(typeof(Delegate).IsAssignableFrom(field.FieldType));
            Assert.True(field.IsStatic && field.IsInitOnly, "Policies must remain stateless: " + field);
        });
    }

    [Theory]
    [InlineData("base")][InlineData("seafarers")][InlineData("cities")][InlineData("combined")]
    public void NullAndWrongSeatViewsFailWithoutAuthorityMutation(string rules)
    {
        var game = M6Game.Create(rules);
        Assert.Throws<ArgumentNullException>(() => game.Decide(null!));
        string before = game.Save();
        string actor = M6Game.Actor(game.View());
        string other = actor == "P1" ? "P2" : "P1";
        Assert.Throws<InvalidOperationException>(() => game.Decide(game.View(other)));
        Assert.Equal(before, game.Save());
        var view = game.View(actor);
        var command = game.Decide(view);
        command.Id = "boundary-wrong-seat";
        command.PlayerId = other;
        Assert.False((bool)game.Execute(command).Success);
        Assert.Equal(before, game.Save());
        command.PlayerId = actor;
        command.TargetId = "not-a-board-location";
        Assert.False((bool)game.Execute(command).Success);
        Assert.Equal(before, game.Save());
    }

    [Fact]
    public void BasePolicyCannotDistinguishOpponentHandsDeckOrderOrRandomState()
    {
        var game = M2Harness.Fixture(s =>
        {
            M2Harness.Player(s, "P2").Resources.Wood = 2;
            M2Harness.Player(s, "P3").Resources.Ore = 2;
            M2Harness.Player(s).Resources.Wheat = 1;
        });
        var first = game.GetPlayerView("P1");
        var state = game.GetAuthoritativeStateForTesting();
        (M2Harness.Player(state, "P2").Resources, M2Harness.Player(state, "P3").Resources) =
            (M2Harness.Player(state, "P3").Resources, M2Harness.Player(state, "P2").Resources);
        Array.Reverse(state.DevelopmentDeck);
        state.Random.State ^= 0x13579BDF;
        M2Harness.SetState(game, state);
        var second = game.GetPlayerView("P1");
        Assert.Equal(M6Game.Json(first), M6Game.Json(second));
        Assert.Equal(M6Game.Json(new BaseGameAi().Decide(first)), M6Game.Json(new BaseGameAi().Decide(second)));
    }

    [Fact]
    public void CombinedPolicyCannotDistinguishPrivateResourcesCommoditiesProgressDecksOrRandom()
    {
        var game = M5Harness.Fixture(s =>
        {
            M5Harness.Player(s, "P2").Resources.Wood = 2;
            M5Harness.Player(s, "P3").Resources.Ore = 2;
            M5Harness.Player(s, "P2").Commodities.Paper = 1;
            M5Harness.Player(s, "P3").Commodities.Coin = 1;
            M5Harness.Player(s, "P2").ProgressCards = [M5.ProgressCardKind.Medicine];
            M5Harness.Player(s, "P3").ProgressCards = [M5.ProgressCardKind.Treason];
        });
        var first = game.GetPlayerView("P1");
        var state = game.GetAuthoritativeStateForTesting();
        var two = M5Harness.Player(state, "P2"); var three = M5Harness.Player(state, "P3");
        (two.Resources, three.Resources) = (three.Resources, two.Resources);
        (two.Commodities, three.Commodities) = (three.Commodities, two.Commodities);
        (two.ProgressCards, three.ProgressCards) = (three.ProgressCards, two.ProgressCards);
        foreach (var deck in state.ProgressDecks) Array.Reverse(deck.Cards);
        state.Random.State ^= 0x13579BDF;
        M5Harness.SetState(game, state);
        var second = game.GetPlayerView("P1");
        Assert.Equal(M6Game.Json(first), M6Game.Json(second));
        Assert.Equal(M6Game.Json(new CombinedAi().Decide(first)), M6Game.Json(new CombinedAi().Decide(second)));
    }

    [Fact]
    public void FogPolicyCannotDistinguishUnrevealedTerrainOrRandomState()
    {
        var game = M3.SeafarersGameSession.Create("fog-islands", 4, 97);
        string actor = game.GetPlayerView("P1").ActivePlayerId;
        var first = game.GetPlayerView(actor);
        var state = game.GetAuthoritativeStateForTesting();
        var field = typeof(M3.SeafarersGameSession).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.NotEmpty(state.HiddenResources);
        Array.Reverse(state.HiddenResources);
        Array.Reverse(state.HiddenNumbers);
        state.Random.State ^= 0x13579BDF;
        field.SetValue(game, state);
        var second = game.GetPlayerView(actor);
        Assert.Equal(M6Game.Json(first), M6Game.Json(second));
        Assert.Equal(M6Game.Json(new SeafarersAi().Decide(first)), M6Game.Json(new SeafarersAi().Decide(second)));
    }
}
