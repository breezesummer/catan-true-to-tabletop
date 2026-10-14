using Catan.Core;
using Xunit;
using M2 = Catan.Core.M2;

namespace Catan.Rules.Tests;

public sealed class TabletopHandOrderTests
{
    [Fact]
    public void NewlyReceivedGoodsAppendInAuthorizedHistoryOrderUntilSorted()
    {
        var order=new TabletopHandOrder();order.Sync([1,0,0,0,1,0,0,0],5,null);
        order.Sync([2,0,1,0,1,0,0,0],5,[new(){Good=2,Amount=1},new(){Good=0,Amount=1}]);
        Assert.Equal(new[]{0,4,2,0},order.Goods);
        order.Sync([2,0,1,0,1,0,0,0],5,null);Assert.Equal(new[]{0,4,2,0},order.Goods);
        order.Sort();Assert.Equal(new[]{0,0,2,4},order.Goods);
        order.Sync([2,1,1,0,1,0,0,0],5,[new(){Good=1,Amount=1}]);Assert.Equal(new[]{0,0,2,4,1},order.Goods);
    }
    [Fact]
    public void SpendingUsesOldestMatchingCardAndPreservesUnrelatedPositions()
    {
        var order=new TabletopHandOrder();order.Sync([1,0,0,0,1,0,0,0],5,null);
        order.Sync([2,0,1,0,1,0,0,0],5,[new(){Good=2,Amount=1},new(){Good=0,Amount=1}]);
        order.Sync([1,0,1,0,1,0,0,0],5,[new(){Good=0,Amount=-1}]);
        Assert.Equal(new[]{4,2,0},order.Goods);
        Assert.All(order.Goods,good=>Assert.InRange(good,0,4));
    }
    [Fact]
    public void FirstViewCanReconstructAChangeBeforeItsFirstRepaint()
    {
        var order=new TabletopHandOrder();order.Sync([1,0,1,0,1,0,0,0],5,[new(){Good=4,Amount=-1},new(){Good=2,Amount=1}]);
        Assert.Equal(new[]{0,4,2},order.Goods);
    }
    [Fact]
    public void ViewCountsWinOverUnavailableOrStaleHistory()
    {
        var order=new TabletopHandOrder();int[] owned=[1,0,0,0,0,0,0,0];
        order.Sync(owned,5,[new(){Good=0,Amount=9},new(){Good=-1,Amount=1},new(){Good=7,Amount=1}]);
        Assert.Equal(new[]{0},order.Goods);Assert.Equal(1,owned[0]);
        order.Sync(new int[8],5,null);Assert.Empty(order.Goods);
    }
    [Fact]
    public void NewSpecialCardKeepsItsAcquisitionPositionEvenIfSnapshotIsSorted()
    {
        var order=new TabletopSpecialHandOrder();Assert.Equal(new[]{"c1","c4"},order.Sync(new[]{"c1","c4"}));
        Assert.Equal(new[]{"c1","c4","c2"},order.Sync(new[]{"c1","c2","c4"}));
        Assert.Equal(new[]{"c4","c2"},order.Sync(new[]{"c2","c4"}));
        order.Sort(new[]{"c2","c4"});Assert.Equal(new[]{"c2","c4"},order.Sync(new[]{"c2","c4"}));
    }
    [Theory]
    [InlineData(118,false)]
    [InlineData(108,false)]
    [InlineData(80,true)]
    [InlineData(8,true)]
    public void NeighborSpreadIsNeededOnlyWhenALaterCardCoversTheFace(float nextX,bool obscured)
    {
        Assert.Equal(obscured,TabletopHandLayout.IsObscured(nextX,0,108));
    }
    [Fact]
    public void CollectionSlotsAppendPlayerTradeAndBankGainsAndShareFlightDestinations()
    {
        var game=M2Harness.Fixture(s=>{M2Harness.Player(s).Resources.Wood=5;M2Harness.Player(s).Resources.Ore=1;M2Harness.Player(s,"P2").Resources.Wool=1;});
        var timeline=new TabletopCollectionTimeline();timeline.Observe("P1",game.GetHistory("P1"),0);
        timeline.HandSlots("P1",[5,0,0,0,1,0,0,0],5,0);
        var offer=M2Harness.Cmd(M2.CommandKind.ProposeTrade);offer.OtherPlayerId="P2";offer.Give=M2Harness.Bag(wood:1);offer.Receive=M2Harness.Bag(wool:1);
        M2Harness.Accepted(game,offer);M2Harness.Accepted(game,M2Harness.Cmd(M2.CommandKind.AcceptTrade,"P2"));
        var exchange=M2Harness.Cmd(M2.CommandKind.BankTrade);exchange.GiveResource=Resource.Wood;exchange.GiveAmount=4;exchange.ReceiveResource=Resource.Ore;exchange.ReceiveAmount=1;
        M2Harness.Accepted(game,exchange);timeline.Observe("P1",game.GetHistory("P1"),10);
        int[] owned=[0,0,1,0,2,0,0,0];var slots=timeline.HandSlots("P1",owned,5,10.1);
        Assert.Equal(new[]{4,2,4},slots.Select(s=>s.Good));
        Assert.Null(slots[1].Flight);Assert.NotNull(slots[2].Flight);
        Assert.Equal(new[]{4,2,4},timeline.HandSlots("P1",owned,5,10.2).Select(s=>s.Good));
        timeline.SortHand("P1");Assert.Equal(new[]{2,4,4},timeline.HandSlots("P1",owned,5,12).Select(s=>s.Good));
    }
    [Fact]
    public void HiddenTheftIdentityIsNeverReconstructedFromAnObserversHistory()
    {
        var game=M2Harness.Fixture(s=>
        {
            s.Phase=M2.GamePhase.RobberSteal;
            s.Settlements=[M2Harness.Piece(M2Harness.New().GetPlayerView("P1").Board.Tiles.Single(t=>t.Id==s.RobberTileId).Vertices[0],"P2")];
            s.PendingDecision=new M2.PendingDecision{Kind="Steal",PlayerId="P1",EligibleVictimIds=["P2"],ReturnPhase=M2.GamePhase.Action};
            M2Harness.Player(s,"P2").Resources.Ore=1;
        });
        var timeline=new TabletopCollectionTimeline();timeline.Observe("P3",game.GetHistory("P3"),0);
        timeline.HandSlots("P1",new int[8],5,0);
        var steal=M2Harness.Cmd(M2.CommandKind.StealResource);steal.OtherPlayerId="P2";M2Harness.Accepted(game,steal);
        timeline.Observe("P3",game.GetHistory("P3"),10);
        Assert.Empty(timeline.HandSlots("P1",new int[8],5,10));Assert.Empty(timeline.Active(10.1));
    }
}
