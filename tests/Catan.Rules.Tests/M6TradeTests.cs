using Catan.Core;
using Xunit;
using M2 = Catan.Core.M2;
using M3 = Catan.Core.M3;
using M4 = Catan.Core.M4;
using M5 = Catan.Core.M5;

namespace Catan.Rules.Tests;

public sealed class M6TradeTests
{
    [Theory]
    [InlineData("cities", true)][InlineData("cities", false)]
    [InlineData("combined", true)][InlineData("combined", false)]
    public void MixedCommodityOfferChecksOwnCommodityPaymentBeforeAcceptance(string rules, bool affordable)
    {
        void Hands(dynamic s)
        {
            s.Players[0].Resources.Wood = 2;
            s.Players[0].Commodities.Paper = 1;
            s.Players[1].Resources.Ore = 5;
            s.Players[1].Commodities.Coin = affordable ? 1 : 0;
        }
        object session = rules == "cities" ? M4CoreHarness.Fixture(s => Hands(s)) : M5Harness.Fixture(s => Hands(s));
        dynamic offer = rules == "cities" ? M4CoreHarness.Cmd(M4.CommandKind.ProposeTrade) : M5Harness.Cmd(M5.CommandKind.ProposeTrade);
        offer.OtherPlayerId = "P2";
        offer.Give = new ResourceBag { Wood = 2 };
        offer.Receive = new ResourceBag { Ore = 1 };
        if (rules == "cities")
        {
            offer.GiveCommodities = new M4.CommodityBag { Paper = 1 };
            offer.ReceiveCommodities = new M4.CommodityBag { Coin = 1 };
        }
        else
        {
            offer.GiveCommodities = new M5.CommodityBag { Paper = 1 };
            offer.ReceiveCommodities = new M5.CommodityBag { Coin = 1 };
        }
        var game = new M6Game(rules, session);
        game.Accept(offer);
        var response = game.Decide(game.View("P2"));
        Assert.Equal(affordable ? "AcceptTrade" : "RejectTrade", (string)response.Kind.ToString());
        response.Id = "m6-mixed-response";
        game.Accept(response);
        Assert.Equal(affordable ? 1 : 0, (int)game.View("P2").OwnCommodities.Paper);
        Assert.Equal(affordable ? 1 : 0, (int)game.View("P1").OwnCommodities.Coin);
        game.AssertInvariants();
    }

    [Theory]
    [InlineData("base", true)][InlineData("base", false)]
    [InlineData("seafarers", true)][InlineData("seafarers", false)]
    [InlineData("cities", true)][InlineData("cities", false)]
    [InlineData("combined", true)][InlineData("combined", false)]
    public void OfferResponseUsesRecipientViewAndAcceptsBeneficialAffordableTradeOnly(string rules, bool affordable)
    {
        // The active proposer offers two wood for one ore; the off-turn recipient has five ore or none.
        void Hands(dynamic s) { s.Players[0].Resources.Wood = 2; s.Players[1].Resources.Ore = affordable ? 5 : 0; }
        object session = rules switch
        {
            "base" => M2Harness.Fixture(s => Hands(s)),
            "seafarers" => M3Harness.Fixture(s => Hands(s)),
            "cities" => M4CoreHarness.Fixture(s => Hands(s)),
            "combined" => M5Harness.Fixture(s => Hands(s)),
            _ => throw new ArgumentException(rules)
        };
        dynamic offer = rules switch
        {
            "base" => M2Harness.Cmd(M2.CommandKind.ProposeTrade),
            "seafarers" => M3Harness.Cmd(M3.CommandKind.ProposeTrade),
            "cities" => M4CoreHarness.Cmd(M4.CommandKind.ProposeTrade),
            "combined" => M5Harness.Cmd(M5.CommandKind.ProposeTrade),
            _ => throw new ArgumentException(rules)
        };
        offer.OtherPlayerId = "P2";
        offer.Give = new ResourceBag { Wood = 2 };
        offer.Receive = new ResourceBag { Ore = 1 };
        var game = new M6Game(rules, session);
        game.Accept(offer);
        Assert.Equal("P1", (string)game.View().ActivePlayerId);
        Assert.Equal("P2", M6Game.Actor(game.View()));
        Assert.Throws<InvalidOperationException>(() => game.Decide(game.View("P3")));
        var view = game.View("P2");
        string projected = M6Game.Json(view);
        var response = game.Decide(view);
        Assert.Equal(affordable ? "AcceptTrade" : "RejectTrade", (string)response.Kind.ToString());
        response.Id = "m6-trade-response";
        Assert.Equal(projected, M6Game.Json(view));
        string before = game.Save();
        response.PlayerId = "P3";
        Assert.False((bool)game.Execute(response).Success);
        Assert.Equal(before, game.Save());
        response.PlayerId = "P2";
        game.Accept(response);
        Assert.Null((object?)game.View().TradeOffer);
        Assert.Equal(affordable ? 2 : 0, (int)game.View("P2").OwnResources.Wood);
        game.AssertInvariants();
        string after = game.Save();
        Assert.True((bool)game.Execute(response).IsDuplicate);
        Assert.Equal(after, game.Save());
    }

    [Theory]
    [InlineData("base")][InlineData("seafarers")][InlineData("cities")][InlineData("combined")]
    public void AiInitiatesLegalTradeAndDoesNotRepeatRejectedOfferInSameTurn(string rules)
    {
        void Hands(dynamic s) { s.Players[0].Resources.Wood = 2; s.Players[1].Resources.Wood = 1; }
        object session = rules switch
        {
            "base" => M2Harness.Fixture(s => Hands(s)),
            "seafarers" => M3Harness.Fixture(s => Hands(s)),
            "cities" => M4CoreHarness.Fixture(s => Hands(s)),
            "combined" => M5Harness.Fixture(s => Hands(s)),
            _ => throw new ArgumentException(rules)
        };
        var game = new M6Game(rules, session);
        var offer = game.Decide(game.View("P1"));
        Assert.Equal("ProposeTrade", (string)offer.Kind.ToString());
        offer.Id = "m6-propose";
        game.Accept(offer);
        var response = game.Decide(game.View((string)offer.OtherPlayerId));
        Assert.Equal("RejectTrade", (string)response.Kind.ToString());
        response.Id = "m6-reject";
        game.Accept(response);
        var continuation = game.Decide(game.View("P1"));
        Assert.NotEqual("ProposeTrade", (string)continuation.Kind.ToString());
        continuation.Id = "m6-after-reject";
        game.Accept(continuation);
        game.AssertInvariants();
    }
}
