using Catan.Core;
using Xunit;

namespace Catan.Rules.Tests;

public sealed class InteractionPresentationTests
{
    [Fact]
    public void GoldClickAddsTwoWoolAndRejectsThirdCardWithoutChangingSelection()
    {
        var selected=new ResourceBag();var bank=new ResourceBag{Wool=3,Wood=2};
        Assert.Null(OperationResourceChoice.Add(2,2,selected,bank));
        Assert.Null(OperationResourceChoice.Add(2,2,selected,bank));
        Assert.NotNull(OperationResourceChoice.Add(0,2,selected,bank));
        Assert.Equal((2,0,2),(selected.Wool,selected.Wood,selected.Total));
        Assert.Equal(3,bank.Wool);
    }
    [Theory]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(-1)]
    public void GoldNeverAllowsCommoditiesOrInvalidResource(int good)
    {
        var selected=new ResourceBag();
        Assert.NotNull(OperationResourceChoice.Add(good,2,selected,new ResourceBag{Wood=4}));
        Assert.Equal(0,selected.Total);
    }
    [Fact]
    public void EmptyBankStackAndLimitedTotalSupplyCannotBeOverselected()
    {
        var selected=new ResourceBag();var bank=new ResourceBag{Wood=1};
        Assert.NotNull(OperationResourceChoice.Add(2,3,selected,bank));
        Assert.Null(OperationResourceChoice.Add(0,3,selected,bank));
        Assert.NotNull(OperationResourceChoice.Add(0,3,selected,bank));
        Assert.Equal(1,selected.Total);
    }
    [Theory]
    [InlineData("P1",false,0,2,"P2")]
    [InlineData("P2",false,2,0,"P1")]
    [InlineData("P1",true,0,2,"*")]
    [InlineData("P3",true,2,0,"P1")]
    public void BothTradeDirectionsAlwaysPlaceViewersPaymentOnTheLeft(string viewer,bool broadcast,int ownGood,int otherGood,string otherSeat)
    {
        var s=new OperationSnapshot{Seat=viewer,OtherPlayers=new[]{"P1","P2","P3"}.Where(p=>p!=viewer).ToArray(),Offer=new OperationOffer{Proposer="P1",Recipient="P2",IsBroadcast=broadcast}};
        s.Offer.Give[0]=2;s.Offer.Receive[2]=1;
        var sides=OperationOfferPerspective.For(s);
        Assert.True(sides.Involved);Assert.Equal(otherSeat,sides.OtherSeat);
        Assert.True(sides.OwnGive[ownGood]>0);Assert.True(sides.OtherGive[otherGood]>0);
        Assert.NotSame(s.Goods,sides.OwnGive);
    }
    [Fact]
    public void UninvolvedTradeDoesNotPretendTheViewerMustPay()
    {
        var s=new OperationSnapshot{Seat="P3",OtherPlayers=["P1","P2"],Offer=new OperationOffer{Proposer="P1",Recipient="P2"}};
        s.Offer.Receive[4]=3;
        var sides=OperationOfferPerspective.For(s);
        Assert.False(sides.Involved);Assert.Equal(0,sides.OwnGive.Sum());Assert.Equal("P1",sides.OtherSeat);
    }
    static Edge Edge(string id,string a,string b)=>new(){Id=id,Vertices=[a,b]};
    static PiecePlacement Route(string id,string player="P1")=>new(){LocationId=id,PlayerId=player};
    [Fact]
    public void KnightPathFollowsOwnedRoadsThroughEveryCornerInsteadOfCuttingAcrossTheBoard()
    {
        Edge[] edges=[Edge("E1","A","B"),Edge("E2","B","C"),Edge("E3","C","D"),Edge("E4","A","D")];
        var routes=new[]{Route("E1"),Route("E2"),Route("E3"),Route("E4","P2")};
        Assert.Equal(new[]{"A","B","C","D"},TabletopMovementPath.Find("A","D","P1",edges,routes,[]));
    }
    [Fact]
    public void OpponentBuildingBlocksIntermediatePathButAllowsDisplacementDestination()
    {
        Edge[] edges=[Edge("E1","A","B"),Edge("E2","B","C")];
        PiecePlacement[] routes=[Route("E1"),Route("E2")],blocked=[Route("B","P2")];
        Assert.Empty(TabletopMovementPath.Find("A","C","P1",edges,routes,blocked));
        Assert.Equal(new[]{"A","B"},TabletopMovementPath.Find("A","B","P1",edges,routes,blocked));
    }
    [Fact]
    public void MovementAndPromotionCarryPreviousPublicPieceButNewRecruitmentDoesNot()
    {
        var timeline=new TabletopBuildTimeline();
        timeline.Observe(3,"P1",[new(){Kind="knight",PlayerId="P1",LocationId="A"}]);
        var moved=Assert.Single(timeline.Observe(3,"P1",[new(){Kind="knight",PlayerId="P1",LocationId="B"}]));
        Assert.Equal("A",moved.Previous.LocationId);
        var promoted=Assert.Single(timeline.Observe(3,"P1",[new(){Kind="knight",PlayerId="P1",LocationId="B",Level=2}]));
        Assert.Equal(1,promoted.Previous.Level);
        Assert.Empty(timeline.Observe(3,"P1",[new(){Kind="knight",PlayerId="P1",LocationId="B",Level=2}]));
    }
    [Fact]
    public void CityReplacementRemembersVillageAndRestoredSaveDoesNotReplayIt()
    {
        var timeline=new TabletopBuildTimeline();
        timeline.Observe(3,"P1",[new(){Kind="settlement",PlayerId="P1",LocationId="A"}]);
        var upgraded=Assert.Single(timeline.Observe(3,"P1",[new(){Kind="city",PlayerId="P1",LocationId="A"}]));
        Assert.Equal("settlement",upgraded.Previous.Kind);
        var restored=new TabletopBuildTimeline();Assert.Empty(restored.Observe(3,"P1",[upgraded]));
    }
}
