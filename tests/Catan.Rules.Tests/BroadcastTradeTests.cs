using Catan.Core;
using Xunit;
using M2 = Catan.Core.M2;
using M3 = Catan.Core.M3;
using M4 = Catan.Core.M4;
using M5 = Catan.Core.M5;

namespace Catan.Rules.Tests;

public sealed class BroadcastTradeTests
{
    [Theory]
    [InlineData("base")][InlineData("seafarers")][InlineData("cities")][InlineData("combined")]
    public void AnyPendingRecipientCanAcceptAndOnlyOneExchangeOccurs(string rules)
    {
        var (game, session) = Fixture(rules);
        var offer = Offer(rules);
        game.Accept(offer);
        Assert.Equal(new[] { "P2", "P3", "P4" }, (string[])game.View().TradeOffer.PendingRecipientIds);
        Assert.False((bool)game.View().TradeOffer.CanRespond("unknown"));
        Assert.False((bool)game.View().TradeOffer.CanRespond("P1"));
        Assert.Equal("P4", (string)game.View().TradeOffer.NextResponder("P4"));
        var detachedOffer = game.View("P4").TradeOffer;
        detachedOffer.RecipientPlayerIds[0] = "P4";
        Assert.Equal(new[] { "P2", "P3", "P4" }, (string[])game.View().TradeOffer.PendingRecipientIds);

        // 预览拒绝不能更改共享的报价数组或权威状态。
        string before = game.Save();
        Assert.True((bool)session.Preview(Command(rules, "RejectTrade", "P2")).Success);
        Assert.Equal(before, game.Save());
        game.Accept(Command(rules, "RejectTrade", "P2"));
        Assert.Equal(new[] { "P3", "P4" }, (string[])game.View().TradeOffer.PendingRecipientIds);
        Assert.Equal(2, (int)game.View().OwnResources.Wood);
        RejectUnchanged(game, Command(rules, "AcceptTrade", "P2"));
        RejectUnchanged(game, Command(rules, "RejectTrade", "P2"));
        RejectUnchanged(game, Command(rules, "CancelTrade", "P3"));
        RejectUnchanged(game, Command(rules, "AcceptTrade", "P1"));
        RejectUnchanged(game, Command(rules, "EndTurn", "P1"));

        // P4 可直接响应，不依赖客户端将报价逐个转发给 P3。
        var accept = Command(rules, "AcceptTrade", "P4");
        game.Accept(accept);
        Assert.Null((object?)game.View().TradeOffer);
        Assert.Equal(0, (int)game.View("P1").OwnResources.Wood);
        Assert.Equal(1, (int)game.View("P1").OwnResources.Ore);
        Assert.Equal(2, (int)game.View("P4").OwnResources.Wood);
        Assert.Equal(4, (int)game.View("P4").OwnResources.Ore);
        Assert.Equal(0, (int)game.View("P3").OwnResources.Wood);
        before = game.Save();
        Assert.True((bool)game.Execute(accept).IsDuplicate);
        Assert.Equal(before, game.Save());
        RejectUnchanged(game, Command(rules, "AcceptTrade", "P3"));
        game.AssertInvariants();
    }

    [Theory]
    [InlineData("base")][InlineData("seafarers")][InlineData("cities")][InlineData("combined")]
    public void AllRecipientsMustRejectAndOnlyProposerCanWithdraw(string rules)
    {
        var (game, _) = Fixture(rules);
        var offTurn = Offer(rules); offTurn.PlayerId = "P2";
        RejectUnchanged(game, offTurn);
        game.Accept(Offer(rules));
        foreach (string player in new[] { "P3", "P4" })
        {
            game.Accept(Command(rules, "RejectTrade", player));
            Assert.NotNull((object?)game.View().TradeOffer);
        }
        game.Accept(Command(rules, "RejectTrade", "P2"));
        Assert.Null((object?)game.View().TradeOffer);
        Assert.Equal(2, (int)game.View().OwnResources.Wood);
        game.Accept(Offer(rules));
        game.Accept(Command(rules, "RejectTrade", "P2"));
        game.Accept(Command(rules, "CancelTrade", "P1"));
        Assert.Null((object?)game.View().TradeOffer);
        Assert.Equal(2, (int)game.View().OwnResources.Wood);
        game.AssertInvariants();
    }

    [Theory]
    [InlineData("base")][InlineData("seafarers")][InlineData("cities")][InlineData("combined")]
    public void AiUsesOnlyItsOwnPermissionFilteredViewToRespond(string rules)
    {
        var (game, session) = Fixture(rules);
        game.Accept(Offer(rules));
        var detached = game.View("P4");
        string projection = M6Game.Json(detached);
        var reject = game.Decide(game.View("P2"));
        Assert.Equal("RejectTrade", (string)reject.Kind.ToString());
        reject.Id = Guid.NewGuid().ToString("N");
        game.Accept(reject);
        Assert.Throws<InvalidOperationException>(() => game.Decide(game.View("P2")));
        Assert.Throws<InvalidOperationException>(() => game.Decide(game.View("P1")));
        var accept = game.Decide(detached);
        Assert.Equal("AcceptTrade", (string)accept.Kind.ToString());
        Assert.Equal(projection, M6Game.Json(detached));
        accept.Id = Guid.NewGuid().ToString("N");
        game.Accept(accept);
        // 对外公开的成交只包含实际交换；不公开任何完整手牌或牌库。
        GameHistoryEntry[] history = session.GetHistory("P3", 0L);
        var trade = Assert.Single(history, e => e.Kind == "AcceptTrade");
        Assert.Equal("P4", trade.PlayerId);
        Assert.Equal("P1", trade.OtherPlayerId);
        Assert.Equal(4, trade.Changes.Length);
        Assert.All(trade.Changes, c => Assert.Equal("goods", c.Category));
        game.AssertInvariants();
    }

    [Theory]
    [InlineData("cities")][InlineData("combined")]
    public void BroadcastAlsoTransfersCommoditiesAndRejectsUnaffordableAcceptance(string rules)
    {
        void Hands(dynamic state)
        {
            state.Players[0].Commodities.Paper = 2;
            state.Players[2].Commodities.Coin = 1;
        }
        object session = rules == "cities" ? M4CoreHarness.Fixture(s => Hands(s)) : M5Harness.Fixture(s => Hands(s));
        var game = new M6Game(rules, session);
        var offer = Command(rules, "ProposeTrade");
        offer.OtherPlayerId = "*";
        if (rules == "cities")
        {
            offer.GiveCommodities = new M4.CommodityBag { Paper = 2 };
            offer.ReceiveCommodities = new M4.CommodityBag { Coin = 1 };
        }
        else
        {
            offer.GiveCommodities = new M5.CommodityBag { Paper = 2 };
            offer.ReceiveCommodities = new M5.CommodityBag { Coin = 1 };
        }
        game.Accept(offer);
        RejectUnchanged(game, Command(rules, "AcceptTrade", "P2"));
        game.Accept(Command(rules, "AcceptTrade", "P3"));
        Assert.Equal(1, (int)game.View().OwnCommodities.Coin);
        Assert.Equal(0, (int)game.View().OwnCommodities.Paper);
        Assert.Equal(2, (int)game.View("P3").OwnCommodities.Paper);
        Assert.Equal(0, (int)game.View("P3").OwnCommodities.Coin);
        game.AssertInvariants();
    }

    [Theory]
    [InlineData("base")][InlineData("seafarers")][InlineData("cities")][InlineData("combined")]
    public void RealCommandHistoryRestoresBroadcastAndPartialRejections(string rules)
    {
        var game = M6Game.Create(rules);
        (string proposer, string recipient, Resource give, Resource receive)? opportunity = null;
        for (int step = 0; step < 2000 && opportunity == null; step++)
        {
            dynamic status = game.View();
            if (status.Phase.ToString() == "Action" && status.TradeOffer == null)
            {
                string actor = status.ActivePlayerId;
                ResourceBag own = game.View(actor).OwnResources;
                foreach (Resource give in Enum.GetValues<Resource>().Where(r => own[r] > 0))
                foreach (dynamic player in (Array)status.Players)
                {
                    if ((string)player.Id == actor) continue;
                    ResourceBag theirs = game.View((string)player.Id).OwnResources;
                    var receives = Enum.GetValues<Resource>().Where(r => r != give && theirs[r] > 0).ToArray();
                    if (receives.Length > 0) { opportunity = (actor, (string)player.Id, give, receives[0]); break; }
                }
            }
            if (opportunity == null)
            {
                var next = game.Decide(game.View(M6Game.Actor(status)));
                next.Id = Guid.NewGuid().ToString("N"); game.Accept(next);
            }
        }
        Assert.True(opportunity.HasValue);
        var found = opportunity!.Value;
        var offer = Command(rules, "ProposeTrade", found.proposer);
        offer.OtherPlayerId = "*";
        var giveBag = new ResourceBag(); giveBag[found.give] = 1; offer.Give = giveBag;
        var receiveBag = new ResourceBag(); receiveBag[found.receive] = 1; offer.Receive = receiveBag;
        // 旧版定向报价不新增序列化字段，既有权威存档仍能原样重放。
        offer.OtherPlayerId = found.recipient;
        game.Accept(offer);
        Assert.DoesNotContain("RecipientPlayerIds", game.Save());
        Assert.DoesNotContain("RejectedPlayerIds", game.Save());
        Assert.Equal(game.Save(), M6Game.Load(rules, game.Save()).Save());
        game.Accept(Command(rules, "CancelTrade", found.proposer));
        offer.Id = Guid.NewGuid().ToString("N"); offer.OtherPlayerId = "*";
        game.Accept(offer);
        var restored = M6Game.Load(rules, game.Save());
        Assert.Equal(game.Save(), restored.Save());
        string rejecting = ((string[])game.View().TradeOffer.PendingRecipientIds).First(p => p != found.recipient);
        var rejection = Command(rules, "RejectTrade", rejecting);
        game.Accept(rejection); restored.Accept(rejection);
        Assert.Equal(game.Save(), restored.Save());
        restored = M6Game.Load(rules, game.Save());
        Assert.Equal(new[] { rejecting }, (string[])restored.View().TradeOffer.RejectedPlayerIds);
        Assert.False((bool)restored.View().TradeOffer.CanRespond(rejecting));
        var forged = System.Text.Json.Nodes.JsonNode.Parse(game.Save())!;
        forged["TradeOffer"]!["RejectedPlayerIds"]![0] = found.recipient;
        Assert.Throws<System.IO.InvalidDataException>(() => M6Game.Load(rules, forged.ToJsonString()));
        var accept = Command(rules, "AcceptTrade", found.recipient);
        game.Accept(accept); restored.Accept(accept);
        Assert.Equal(game.Save(), restored.Save());
        Assert.Null((object?)restored.View().TradeOffer);
        game.AssertInvariants(); restored.AssertInvariants();
    }

    [Fact]
    public void PresentationAllowsAllPlayersWithTheSameResourceValidation()
    {
        var snapshot = new OperationSnapshot { CanTrade = true, CanInteract = true, Goods = new[] { 2, 0, 0, 0, 0, 0, 0, 0 }, OtherPlayers = new[] { "P2", "P3" } };
        var trade = new OperationTrade { OtherPlayer = OperationTrade.AllPlayers, Give = new[] { 2, 0, 0, 0, 0, 0, 0, 0 }, Receive = new[] { 0, 0, 0, 0, 1, 0, 0, 0 } };
        Assert.Null(OperationSelection.ValidateTrade(snapshot, trade));
        trade.Give[0] = 3;
        Assert.Equal("付出的资源超过手牌", OperationSelection.ValidateTrade(snapshot, trade));
    }

    private static (M6Game game, dynamic session) Fixture(string rules)
    {
        void Hands(dynamic state)
        {
            state.Players[0].Resources.Wood = 2;
            state.Players[2].Resources.Ore = 5;
            state.Players[3].Resources.Ore = 5;
        }
        object session = rules switch
        {
            "base" => M2Harness.Fixture(s => Hands(s)), "seafarers" => M3Harness.Fixture(s => Hands(s)),
            "cities" => M4CoreHarness.Fixture(s => Hands(s)), "combined" => M5Harness.Fixture(s => Hands(s)),
            _ => throw new ArgumentException(rules)
        };
        return (new M6Game(rules, session), session);
    }

    private static dynamic Offer(string rules)
    {
        var command = Command(rules, "ProposeTrade");
        command.OtherPlayerId = "*"; command.Give = new ResourceBag { Wood = 2 }; command.Receive = new ResourceBag { Ore = 1 };
        return command;
    }

    private static dynamic Command(string rules, string kind, string player = "P1") => rules switch
    {
        "base" => M2Harness.Cmd(Enum.Parse<M2.CommandKind>(kind), player),
        "seafarers" => M3Harness.Cmd(Enum.Parse<M3.CommandKind>(kind), player),
        "cities" => M4CoreHarness.Cmd(Enum.Parse<M4.CommandKind>(kind), player),
        "combined" => M5Harness.Cmd(Enum.Parse<M5.CommandKind>(kind), player),
        _ => throw new ArgumentException(rules)
    };

    private static void RejectUnchanged(M6Game game, object command)
    {
        string before = game.Save();
        Assert.False((bool)game.Execute(command).Success);
        Assert.Equal(before, game.Save());
        game.AssertInvariants();
    }
}
