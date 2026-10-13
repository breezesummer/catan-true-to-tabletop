using Catan.AI;
using Catan.Core;
using Xunit;
using M2 = Catan.Core.M2;
using M4 = Catan.Core.M4;

namespace Catan.Rules.Tests;

public sealed class TabletopCollectionTimelineTests
{
    [Fact]
    public void CityHarvestFliesOneResourceAndOneCommodityAfterTheDiceSettle()
    {
        var tile = M4CoreHarness.New().GetPlayerView("P1").Board.Tiles.First(t => t.Resource == "wood");
        var game = M4CoreHarness.Fixture(s =>
        {
            s.Phase = M4.GamePhase.ProductionAwaitRoll;
            s.AlchemyDice1 = Math.Min(6, tile.Number!.Value - 1);
            s.AlchemyDice2 = tile.Number.Value - s.AlchemyDice1;
            s.Cities = [M4CoreHarness.Piece(tile.Vertices[0], "P2")];
        });
        var timeline = Baseline("P1", game.GetHistory("P1"));
        M4CoreHarness.Accepted(game, M4CoreHarness.Cmd(M4.CommandKind.RollDice));
        Assert.Equal(1, game.GetPlayerView("P2").OwnResources.Wood);
        Assert.Equal(1, game.GetPlayerView("P2").OwnCommodities.Paper);
        timeline.Observe("P1", game.GetHistory("P1"), 10);
        Assert.Empty(timeline.Active(10.5));
        var flights = timeline.Active(11.6);
        Assert.Equal(new[] { ("P2", 0), ("P2", 7) }, flights.Select(f => (f.Player, f.Good)));
        Assert.Empty(timeline.Active(13));
        // 动画结束不再提交规则；结算在动画开始前就已完成。
        Assert.Equal(1, game.GetPlayerView("P2").OwnResources.Wood);
        Assert.Equal(1, game.GetPlayerView("P2").OwnCommodities.Paper);
    }

    [Fact]
    public void BankExchangeFliesOnlyTheTwoCardsReceivedAndReobservingNeverReplaysThem()
    {
        var game = M2Harness.Fixture(s => M2Harness.Player(s).Resources.Wood = 8);
        var timeline = Baseline("P1", game.GetHistory("P1"));
        var exchange = M2Harness.Cmd(M2.CommandKind.BankTrade);
        exchange.GiveResource = Resource.Wood; exchange.GiveAmount = 8;
        exchange.ReceiveResource = Resource.Ore; exchange.ReceiveAmount = 2;
        M2Harness.Accepted(game, exchange);
        var history = game.GetHistory("P1");
        timeline.Observe("P1", history, 10);
        var initial = timeline.Active(10.6);
        Assert.Equal(new[] { ("P1", 4), ("P1", 4) }, initial.Select(f => (f.Player, f.Good)));
        timeline.Observe("P1", history, 10.7);
        Assert.Equal(initial, timeline.Active(10.7));
        Assert.Equal(0, game.GetPlayerView("P1").OwnResources.Wood);
        Assert.Equal(2, game.GetPlayerView("P1").OwnResources.Ore);
        timeline.Observe("P1", game.GetHistory("P1"), 15);
        Assert.Empty(timeline.Active(15));
    }

    [Fact]
    public void TradeProposalAndPlayerExchangeNeverCreateBankFlights()
    {
        var game = M2Harness.Fixture(s =>
        {
            M2Harness.Player(s).Resources.Wood = 2;
            M2Harness.Player(s, "P2").Resources.Ore = 1;
        });
        var timeline = Baseline("P3", game.GetHistory("P3"));
        var offer = M2Harness.Cmd(M2.CommandKind.ProposeTrade);
        offer.OtherPlayerId = "P2"; offer.Give = M2Harness.Bag(wood: 2); offer.Receive = M2Harness.Bag(ore: 1);
        M2Harness.Accepted(game, offer);
        timeline.Observe("P3", game.GetHistory("P3"), 10);
        Assert.Empty(timeline.Active(10.6));
        M2Harness.Accepted(game, M2Harness.Cmd(M2.CommandKind.AcceptTrade, "P2"));
        timeline.Observe("P3", game.GetHistory("P3"), 11);
        Assert.Empty(timeline.Active(11.6));
        Assert.Equal(1, game.GetPlayerView("P1").OwnResources.Ore);
        Assert.Equal(2, game.GetPlayerView("P2").OwnResources.Wood);
    }

    [Fact]
    public void MonopolyTakesCardsFromPlayersWithoutPretendingTheyCameFromTheBank()
    {
        var game = M2Harness.Fixture(s =>
        {
            M2Harness.Card(s, M2.DevelopmentCardKind.Monopoly);
            M2Harness.Player(s, "P2").Resources.Ore = 2;
            M2Harness.Player(s, "P3").Resources.Ore = 3;
        });
        var timeline = Baseline("P1", game.GetHistory("P1"));
        var monopoly = M2Harness.Cmd(M2.CommandKind.PlayMonopoly); monopoly.Resource = Resource.Ore;
        M2Harness.Accepted(game, monopoly);
        timeline.Observe("P1", game.GetHistory("P1"), 10);
        Assert.Empty(timeline.Active(10.6));
        Assert.Equal(5, game.GetPlayerView("P1").OwnResources.Ore);
    }

    [Theory]
    [InlineData("P1")][InlineData("P2")][InlineData("P3")]
    public void PrivateTheftNeverBecomesABankFlightForTheThiefVictimOrObserver(string viewer)
    {
        var game = M2Harness.Fixture(s =>
        {
            s.Phase = M2.GamePhase.RobberSteal;
            s.Settlements = [M2Harness.Piece(M2Harness.New().GetPlayerView("P1").Board.Tiles.Single(t => t.Id == s.RobberTileId).Vertices[0], "P2")];
            s.PendingDecision = new M2.PendingDecision { Kind = "Steal", PlayerId = "P1", EligibleVictimIds = ["P2"], ReturnPhase = M2.GamePhase.Action };
            M2Harness.Player(s, "P2").Resources.Ore = 1;
        });
        var timeline = Baseline(viewer, game.GetHistory(viewer));
        var steal = M2Harness.Cmd(M2.CommandKind.StealResource); steal.OtherPlayerId = "P2";
        M2Harness.Accepted(game, steal);
        timeline.Observe(viewer, game.GetHistory(viewer), 10);
        Assert.Empty(timeline.Active(10.6));
        Assert.Equal(1, game.GetPlayerView("P1").OwnResources.Ore);
        if (viewer == "P3") Assert.All(game.GetHistory(viewer)[0].Changes, c => Assert.Equal(-1, c.GoodIndex));
    }

    [Fact]
    public void LoadingAnExistingGameEstablishesABaselineWithoutReplayingHistoricalCollections()
    {
        var game = M2.BaseGameSession.Create(AcceptanceFixture.Json, 4, 97);
        var ai = new BaseGameAi();
        for (int i = 0; i < 45; i++)
        {
            var publicView = game.GetPlayerView("P1");
            var command = ai.Decide(game.GetPlayerView(M6Game.Actor(publicView)));
            command.Id = "收牌动画读档基线-" + i;
            Assert.True(game.Execute(command).Success);
        }
        var loaded = M2.BaseGameSession.Load(AcceptanceFixture.Json, game.Save());
        var entries = loaded.GetHistory("P1");
        Assert.Contains(entries, e => e.Changes.Any(c => c.Category == "goods" && c.Amount > 0));
        var timeline = Baseline("P1", entries);
        Assert.Empty(timeline.Active(0.1));
        Assert.Empty(timeline.Active(0.6));
        Assert.Empty(timeline.Active(1.6));
        timeline.Observe("P1", loaded.GetHistory("P1"), 2);
        Assert.Empty(timeline.Active(2.1));
        Assert.Empty(timeline.Active(3.6));
    }

    [Fact]
    public void AuthoritativeCardsStayInvisibleUntilTheirIndividualFlightsArrive()
    {
        var (timeline, game) = ExchangeTwoOre();
        int[] owned = [0,0,0,0,3,0,0,0];
        Assert.Equal(3,game.GetPlayerView("P1").OwnResources.Ore);
        foreach(double now in new[]{10,10.4,11.049})
        {
            var slots=timeline.HandSlots("P1",owned,5,now);
            Assert.Equal(1,slots.Count(slot=>slot.Flight==null));
            Assert.Equal(2,slots.Count(slot=>slot.Flight!=null));
        }
        Assert.Equal(2,timeline.HandSlots("P1",owned,5,11.051).Count(slot=>slot.Flight==null));
        Assert.Equal(3,timeline.HandSlots("P1",owned,5,11.111).Count(slot=>slot.Flight==null));
        Assert.Equal(3,game.GetPlayerView("P1").OwnResources.Ore);
        Assert.Equal(3,owned[4]);
    }

    [Fact]
    public void DiceDelayAlsoHidesIncomingCardsBeforeAnyFlightHasStarted()
    {
        var tile=M4CoreHarness.New().GetPlayerView("P1").Board.Tiles.First(t=>t.Resource=="wood");
        var game=M4CoreHarness.Fixture(s=>
        {
            s.Phase=M4.GamePhase.ProductionAwaitRoll;
            s.AlchemyDice1=Math.Min(6,tile.Number!.Value-1);s.AlchemyDice2=tile.Number.Value-s.AlchemyDice1;
            s.Cities=[M4CoreHarness.Piece(tile.Vertices[0],"P1")];
        });
        var timeline=Baseline("P1",game.GetHistory("P1"));
        M4CoreHarness.Accepted(game,M4CoreHarness.Cmd(M4.CommandKind.RollDice));
        timeline.Observe("P1",game.GetHistory("P1"),10);
        int[] owned=[1,0,0,0,0,0,0,1];
        Assert.Empty(timeline.Active(10.5));
        Assert.All(timeline.HandSlots("P1",owned,8,10.5),slot=>{Assert.NotNull(slot.Flight);Assert.Equal(0,slot.Weight);});
        Assert.Equal(2,timeline.HandSlots("P1",owned,8,12.1).Count(slot=>slot.Flight==null));
    }

    [Fact]
    public void ExistingCardsMakeRoomOnlyNearArrivalAndFlightDestinationMatchesFinalHand()
    {
        var (timeline, _)=ExchangeTwoOre();int[] owned=[0,0,0,0,3,0,0,0];
        var early=timeline.HandSlots("P1",owned,5,10.5);
        Assert.Equal(666,TabletopHandLayout.Resource(early,0,720,1060,false).X);
        var near=timeline.HandSlots("P1",owned,5,11.11-0.00001);
        var flying=TabletopHandLayout.Resource(near,2,720,1060,false);
        var landed=timeline.HandSlots("P1",owned,5,11.111);
        for(int index=0;index<landed.Length;index++)
        {
            var actual=TabletopHandLayout.Resource(landed,index,720,1060,false);
            var expected=TabletopHandLayout.Card(index,3,720,1060,false);
            Assert.Equal(expected.X,actual.X,3);Assert.Equal(expected.Y,actual.Y,3);
        }
        Assert.InRange(Math.Abs(flying.X-TabletopHandLayout.Resource(landed,2,720,1060,false).X),0,0.01);
    }

    [Fact]
    public void SpendingIncomingCardsCancelsTheirPresentationWithoutRestoringAnyGoods()
    {
        var (timeline, _)=ExchangeTwoOre();int[] owned=new int[8];
        Assert.Empty(timeline.HandSlots("P1",owned,5,10.2));
        Assert.Empty(timeline.Active(10.3));
        Assert.Empty(timeline.HandSlots("P1",owned,5,20));
        Assert.All(owned,count=>Assert.Equal(0,count));
    }

    [Fact]
    public void RepeatedViewRefreshAndOtherPlayersCannotHideTheViewersOwnCards()
    {
        var (timeline,game)=ExchangeTwoOre();int[] owned=[0,0,0,0,3,0,0,0];
        timeline.Observe("P1",game.GetHistory("P1"),10.2);
        Assert.Equal(2,timeline.HandSlots("P1",owned,5,10.2).Count(slot=>slot.Flight!=null));
        Assert.All(timeline.HandSlots("P2",owned,5,10.2),slot=>Assert.Null(slot.Flight));
        Assert.Equal(2,timeline.HandSlots("P1",owned,5,10.2).Count(slot=>slot.Flight!=null));
    }

    [Fact]
    public void SeatChangeOrRewoundHistoryImmediatelyShowsTheNewBaselineHand()
    {
        var (timeline,game)=ExchangeTwoOre();int[] owned=[0,0,0,0,3,0,0,0];
        timeline.Observe("P2",game.GetHistory("P2"),10.2);
        Assert.All(timeline.HandSlots("P2",owned,5,10.2),slot=>Assert.Null(slot.Flight));
        Assert.Empty(timeline.Active(10.2));
        var (rewound,_)=ExchangeTwoOre();
        rewound.Observe("P1",Array.Empty<GameHistoryEntry>(),10.2);
        Assert.All(rewound.HandSlots("P1",owned,5,10.2),slot=>Assert.Null(slot.Flight));
        Assert.Empty(rewound.Active(10.2));
    }

    static (TabletopCollectionTimeline Timeline,M2.BaseGameSession Game) ExchangeTwoOre()
    {
        var game=M2Harness.Fixture(s=>{M2Harness.Player(s).Resources.Wood=8;M2Harness.Player(s).Resources.Ore=1;});
        var timeline=Baseline("P1",game.GetHistory("P1"));
        var trade=M2Harness.Cmd(M2.CommandKind.BankTrade);
        trade.GiveResource=Resource.Wood;trade.GiveAmount=8;trade.ReceiveResource=Resource.Ore;trade.ReceiveAmount=2;
        M2Harness.Accepted(game,trade);timeline.Observe("P1",game.GetHistory("P1"),10);
        return(timeline,game);
    }

    static TabletopCollectionTimeline Baseline(string player, GameHistoryEntry[] entries)
    {
        var timeline = new TabletopCollectionTimeline();
        timeline.Observe(player, entries, 0);
        return timeline;
    }
}
