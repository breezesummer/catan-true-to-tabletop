using System.Reflection;
using System.Text.Json;
using Catan.Core;
using Xunit;
using M2 = Catan.Core.M2;
using M3 = Catan.Core.M3;
using M4 = Catan.Core.M4;
using M5 = Catan.Core.M5;

namespace Catan.Rules.Tests;

public sealed class AiOpponentHandViewTests
{
    [Theory]
    [InlineData("base")][InlineData("seafarers")][InlineData("cities")][InlineData("combined")]
    public void AiProjectionKnowsExactOpponentResourcesAndPublicProjectionKeepsThemHidden(string rules)
    {
        dynamic game = Fixture(rules);
        string before = game.Save();
        for (int seat = 1; seat <= 4; seat++)
        {
            string playerId = "P" + seat;
            var hands = (OpponentHandView[])game.GetAiPlayerView(playerId).OpponentHands;
            Assert.Equal(3, hands.Length);
            Assert.DoesNotContain(hands, h => h.PlayerId == playerId);
            Assert.Empty((OpponentHandView[])game.GetPlayerView(playerId).OpponentHands);
            foreach (var hand in hands)
            {
                ResourceBag expected = game.GetPlayerView(hand.PlayerId).OwnResources;
                foreach (var resource in Enum.GetValues<Resource>()) Assert.Equal(expected[resource], hand.Resources[resource]);
            }
        }
        var opponent = ((OpponentHandView[])game.GetAiPlayerView("P1").OpponentHands).Single(p => p.PlayerId == "P2");
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, Enum.GetValues<Resource>().Select(r => opponent.Resources[r]));
        Assert.Equal(before, (string)game.Save());
        Assert.Throws<ArgumentException>(() => { game.GetAiPlayerView("未知席位"); });
        Assert.Equal(before, (string)game.Save());
    }

    [Theory]
    [InlineData("base")][InlineData("seafarers")][InlineData("cities")][InlineData("combined")]
    public void AiProjectionIsDetachedFromAuthorityAndOtherViews(string rules)
    {
        dynamic game = Fixture(rules);
        string before = game.Save();
        object original = game.GetAiPlayerView("P1");
        string expected = JsonSerializer.Serialize(original);
        var hand = ((OpponentHandView[])((dynamic)original).OpponentHands).Single(p => p.PlayerId == "P2");
        hand.Resources.Wood = 999;
        hand.ProgressCardCounts[0] = 999;
        Assert.Equal(before, (string)game.Save());
        Assert.Equal(expected, JsonSerializer.Serialize((object)game.GetAiPlayerView("P1")));
        Assert.Empty((OpponentHandView[])game.GetPlayerView("P1").OpponentHands);
        game.AssertInvariants();
    }

    [Theory]
    [InlineData("cities")][InlineData("combined")]
    public void AiProjectionKnowsCommoditiesAndProgressColorsButNotSpecificCards(string rules)
    {
        dynamic game = Fixture(rules);
        var hand = ((OpponentHandView[])game.GetAiPlayerView("P1").OpponentHands).Single(p => p.PlayerId == "P2");
        Assert.Equal(1, hand.Commodities.Cloth);
        Assert.Equal(2, hand.Commodities.Coin);
        Assert.Equal(3, hand.Commodities.Paper);
        Assert.Equal(6, hand.Commodities.Total);
        Assert.Equal(new[] { 1, 1, 2 }, hand.ProgressCardCounts);
        Assert.Equal(0, hand.DevelopmentCardCount);
        Assert.Equal(new[] { "Commodities", "DevelopmentCardCount", "PlayerId", "ProgressCardCounts", "Resources" },
            typeof(OpponentHandView).GetProperties().Select(p => p.Name).Order());

        string original = JsonSerializer.Serialize((object)game.GetAiPlayerView("P1"));
        if (game is M4.CitiesKnightsGameSession city)
        {
            var state = city.GetAuthoritativeStateForTesting();
            M4CoreHarness.Player(state, "P2").ProgressCards[0] = M4.ProgressCardKind.Alchemy;
            M4CoreHarness.Balance(state);
            foreach (var deck in state.ProgressDecks) Array.Reverse(deck.Cards);
            state.Random.State ^= 0x13579BDF;
            M4CoreHarness.SetState(city, state);
        }
        else
        {
            var combined = (M5.CombinedGameSession)game;
            var state = combined.GetAuthoritativeStateForTesting();
            M5Harness.Player(state, "P2").ProgressCards[0] = M5.ProgressCardKind.Alchemy;
            M5Harness.Balance(state);
            foreach (var deck in state.ProgressDecks) Array.Reverse(deck.Cards);
            state.Random.State ^= 0x13579BDF;
            M5Harness.SetState(combined, state);
        }
        Assert.Equal(original, JsonSerializer.Serialize((object)game.GetAiPlayerView("P1")));
    }

    [Theory]
    [InlineData("base")][InlineData("seafarers")]
    public void BaseDevelopmentCardsExposeOnlyCountAndNeverIdentityOrDeckOrder(string rules)
    {
        dynamic game = Fixture(rules);
        string original = JsonSerializer.Serialize((object)game.GetAiPlayerView("P1"));
        var hand = ((OpponentHandView[])game.GetAiPlayerView("P1").OpponentHands).Single(p => p.PlayerId == "P2");
        Assert.Equal(1, hand.DevelopmentCardCount);
        Assert.Equal(new[] { 0, 0, 0 }, hand.ProgressCardCounts);
        Assert.Equal(0, hand.Commodities.Total);
        if (game is M2.BaseGameSession basic)
        {
            var state = basic.GetAuthoritativeStateForTesting();
            M2Harness.Player(state, "P2").DevelopmentCards[0].Kind = M2.DevelopmentCardKind.Monopoly;
            M2Harness.Balance(state);
            Array.Reverse(state.DevelopmentDeck);
            state.Random.State ^= 0x13579BDF;
            M2Harness.SetState(basic, state);
        }
        else
        {
            var sea = (M3.SeafarersGameSession)game;
            var state = sea.GetAuthoritativeStateForTesting();
            M3Harness.Player(state, "P2").DevelopmentCards[0].Kind = M3.DevelopmentCardKind.Monopoly;
            M3Harness.Balance(state);
            Array.Reverse(state.DevelopmentDeck);
            state.Random.State ^= 0x13579BDF;
            M3Harness.SetState(sea, state);
        }
        Assert.Equal(original, JsonSerializer.Serialize((object)game.GetAiPlayerView("P1")));
    }

    [Fact]
    public void AiProjectionStillCannotSeeUnrevealedIslands()
    {
        var game = M3.SeafarersGameSession.Create("fog-islands", 4, 97);
        string before = JsonSerializer.Serialize(game.GetAiPlayerView("P1"));
        var state = game.GetAuthoritativeStateForTesting();
        Assert.NotEmpty(state.HiddenResources);
        Array.Reverse(state.HiddenResources);
        Array.Reverse(state.HiddenNumbers);
        state.Random.State ^= 0x13579BDF;
        typeof(M3.SeafarersGameSession).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, state);
        Assert.Equal(before, JsonSerializer.Serialize(game.GetAiPlayerView("P1")));
    }

    private static object Fixture(string rules) => rules switch
    {
        "base" => M2Harness.Fixture(s => { s.Players[1].Resources = Resources(); M2Harness.Card(s, M2.DevelopmentCardKind.Knight, "P2"); }),
        "seafarers" => M3Harness.Fixture(s => { s.Players[1].Resources = Resources(); M3Harness.Card(s, M3.DevelopmentCardKind.Knight, "P2"); }),
        "cities" => M4CoreHarness.Fixture(s =>
        {
            var p = s.Players[1]; p.Resources = Resources(); p.Commodities = new M4.CommodityBag { Cloth = 1, Coin = 2, Paper = 3 };
            p.ProgressCards = [M4.ProgressCardKind.Medicine, M4.ProgressCardKind.Crane, M4.ProgressCardKind.Merchant, M4.ProgressCardKind.Treason];
        }),
        "combined" => M5Harness.Fixture(s =>
        {
            var p = s.Players[1]; p.Resources = Resources(); p.Commodities = new M5.CommodityBag { Cloth = 1, Coin = 2, Paper = 3 };
            p.ProgressCards = [M5.ProgressCardKind.Medicine, M5.ProgressCardKind.Crane, M5.ProgressCardKind.Merchant, M5.ProgressCardKind.Treason];
        }),
        _ => throw new ArgumentException(rules)
    };
    private static ResourceBag Resources() => new() { Wood = 1, Brick = 2, Wool = 3, Wheat = 4, Ore = 5 };
}
