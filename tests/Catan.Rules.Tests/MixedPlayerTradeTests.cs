using Catan.Core;
using M2 = Catan.Core.M2;
using M3 = Catan.Core.M3;
using M4 = Catan.Core.M4;
using M5 = Catan.Core.M5;
using Xunit;

namespace Catan.Rules.Tests;

public class MixedPlayerTradeTests
{
    public static IEnumerable<object[]> LegalOffers()
    {
        foreach (string rules in new[] { "base", "seafarers", "cities", "combined" })
        foreach (bool reverse in new[] { false, true })
        foreach (bool broadcast in new[] { false, true })
            yield return new object[] { rules, reverse, broadcast };
    }

    // 基础规则 PDF 第 7 页允许自定玩家报价；城市与骑士 PDF 第 7 页允许任意资源／商品组合。
    [Theory, MemberData(nameof(LegalOffers))]
    public void MixedResourcesCanTradeForOneKindInEitherDirection(string rules, bool reverse, bool broadcast)
    {
        dynamic game = Fixture(rules);
        dynamic offer = Command(rules, "ProposeTrade");
        offer.OtherPlayerId = broadcast ? "*" : "P2";
        offer.Give = reverse ? new ResourceBag { Ore = 2 } : new ResourceBag { Wood = 1, Brick = 1 };
        offer.Receive = reverse ? new ResourceBag { Wood = 1, Brick = 1 } : new ResourceBag { Ore = 2 };
        var before = (ResourceBag)game.GetPlayerView("P1").Bank;
        Assert.True((bool)game.Execute(offer).Success);
        dynamic accept = Command(rules, "AcceptTrade", "P2");
        Assert.True((bool)game.Execute(accept).Success);
        var own = (ResourceBag)game.GetPlayerView("P1").OwnResources;
        var other = (ResourceBag)game.GetPlayerView("P2").OwnResources;
        Assert.Equal(reverse ? 2 : 0, own.Wood); Assert.Equal(reverse ? 2 : 0, own.Brick);
        Assert.Equal(reverse ? 0 : 4, own.Ore);
        Assert.Equal(reverse ? 0 : 2, other.Wood); Assert.Equal(reverse ? 0 : 2, other.Brick);
        Assert.Equal(reverse ? 4 : 0, other.Ore);
        Assert.Null((object?)game.GetPlayerView("P1").TradeOffer);
        foreach (var resource in Enum.GetValues<Resource>())
        {
            Assert.Equal(before[resource], ((ResourceBag)game.GetPlayerView("P1").Bank)[resource]);
            Assert.Equal(19, before[resource] + own[resource] + other[resource]);
        }
        string after = game.Save();
        Assert.True((bool)game.Execute(accept).IsDuplicate);
        Assert.Equal(after, (string)game.Save());
        game.AssertInvariants();
    }

    [Theory]
    [InlineData("base")][InlineData("seafarers")][InlineData("cities")][InlineData("combined")]
    public void MixedOfferStillRejectsMatchingResourcesGiftsAndUnrelatedPlayers(string rules)
    {
        dynamic game = Fixture(rules);
        dynamic offer = Command(rules, "ProposeTrade");
        offer.OtherPlayerId = "P2";
        offer.Give = new ResourceBag { Wood = 1, Brick = 1 };
        offer.Receive = new ResourceBag { Wood = 1 };
        RejectUnchanged(game, offer);
        offer.Id += "-赠送"; offer.Receive = new ResourceBag(); RejectUnchanged(game, offer);
        offer.Id += "-超额"; offer.Give.Wood = 2; offer.Receive.Ore = 1; RejectUnchanged(game, offer);
        offer.Id += "-旁观者"; offer.PlayerId = "P3"; offer.OtherPlayerId = "P4";
        RejectUnchanged(game, offer);
    }

    private static dynamic Fixture(string rules)
    {
        void Hands(dynamic s)
        {
            s.Players[0].Resources = new ResourceBag { Wood = 1, Brick = 1, Ore = 2 };
            s.Players[1].Resources = new ResourceBag { Wood = 1, Brick = 1, Ore = 2 };
        }
        return rules switch
        {
            "base" => (object)M2Harness.Fixture(s => Hands(s)),
            "seafarers" => M3Harness.Fixture(s => Hands(s)),
            "cities" => M4CoreHarness.Fixture(s => Hands(s)),
            "combined" => M5Harness.Fixture(s => Hands(s)),
            _ => throw new ArgumentException(rules)
        };
    }
    private static dynamic Command(string rules, string kind, string player = "P1") => rules switch
    {
        "base" => (object)M2Harness.Cmd(Enum.Parse<M2.CommandKind>(kind), player),
        "seafarers" => M3Harness.Cmd(Enum.Parse<M3.CommandKind>(kind), player),
        "cities" => M4CoreHarness.Cmd(Enum.Parse<M4.CommandKind>(kind), player),
        "combined" => M5Harness.Cmd(Enum.Parse<M5.CommandKind>(kind), player),
        _ => throw new ArgumentException(rules)
    };
    private static void RejectUnchanged(dynamic game, dynamic command)
    {
        string before = game.Save();
        Assert.False((bool)game.Execute(command).Success);
        Assert.Equal(before, (string)game.Save());
        game.AssertInvariants();
    }
}
