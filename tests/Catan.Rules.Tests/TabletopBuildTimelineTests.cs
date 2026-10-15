using Xunit;

namespace Catan.Rules.Tests;

public sealed class TabletopBuildTimelineTests
{
    static TabletopBuildPiece Piece(string kind,string location="V1",string player="P2",int level=1,bool active=false)=>
        new(){Kind=kind,LocationId=location,PlayerId=player,Level=level,Active=active};

    [Fact]
    public void ExistingSaveStartsWithoutFlyingOrHighlightingItsHistoricalBuildings()
    {
        var timeline=new TabletopBuildTimeline();
        Assert.Empty(timeline.Observe(17,"P2",[Piece("city"),Piece("road","E1")]));
        Assert.Empty(timeline.Highlighted);
        Assert.Empty(timeline.Observe(17,"P2",[Piece("city"),Piece("road","E1")]));
    }

    [Fact]
    public void PublicBuildFliesOnceAndRemainsHighlightedForItsTurn()
    {
        var timeline=new TabletopBuildTimeline();timeline.Observe(17,"P2",[]);
        var piece=Assert.Single(timeline.Observe(17,"P2",[Piece("settlement")]));
        Assert.Equal(("P2","settlement","V1"),(piece.PlayerId,piece.Kind,piece.LocationId));
        Assert.Single(timeline.Highlighted);
        Assert.Empty(timeline.Observe(17,"P2",[Piece("settlement")]));
        Assert.Single(timeline.Highlighted);
        timeline.Observe(18,"P3",[Piece("settlement")]);
        Assert.Empty(timeline.Highlighted);
    }

    [Fact]
    public void ACityUpgradeReplacesTheSettlementHighlightAndRepeatingTheSnapshotDoesNotReplayIt()
    {
        var timeline=new TabletopBuildTimeline();timeline.Observe(7,"P2",[]);
        timeline.Observe(7,"P2",[Piece("settlement")]);
        Assert.Equal("city",Assert.Single(timeline.Observe(7,"P2",[Piece("city")])).Kind);
        Assert.Equal("city",Assert.Single(timeline.Highlighted).Kind);
        Assert.Empty(timeline.Observe(7,"P2",[Piece("city")]));
    }

    [Theory]
    [InlineData("city","settlement")]
    [InlineData("city-with-wall","city")]
    [InlineData("metropolis-with-wall","city-with-wall")]
    public void BarbarianDowngradesAndRemovedUpgradesDoNotPretendToBeNewConstruction(string before,string after)
    {
        var timeline=new TabletopBuildTimeline();timeline.Observe(7,"P2",[Piece(before)]);
        Assert.Empty(timeline.Observe(7,"P2",[Piece(after)]));
        Assert.Empty(timeline.Highlighted);
    }

    [Fact]
    public void KnightActivationDoesNotFlyAgainButPromotionDoes()
    {
        var timeline=new TabletopBuildTimeline();timeline.Observe(7,"P2",[Piece("knight")]);
        Assert.Empty(timeline.Observe(7,"P2",[Piece("knight",active:true)]));
        Assert.Equal(2,Assert.Single(timeline.Observe(7,"P2",[Piece("knight",level:2,active:true)])).Level);
        Assert.Empty(timeline.Observe(7,"P2",[Piece("knight",level:1)]));
        Assert.Empty(timeline.Highlighted);
    }

    [Fact]
    public void SetupSeatChangesClearEarlierConstructionEvenWhenTheTurnNumberStaysZero()
    {
        var timeline=new TabletopBuildTimeline();timeline.Observe(0,"P2",[]);
        timeline.Observe(0,"P2",[Piece("settlement")]);
        Assert.Single(timeline.Highlighted);
        Assert.Single(timeline.Observe(0,"P3",[Piece("settlement"),Piece("road","E1")]));
        Assert.Empty(timeline.Highlighted);
    }

    [Fact]
    public void SeveralAiBuildingsInOneNetworkSnapshotAreAllPresentedWithoutAuthoritativeData()
    {
        var timeline=new TabletopBuildTimeline();timeline.Observe(5,"P2",[]);
        var delta=timeline.Observe(5,"P2",[Piece("settlement"),Piece("road","E1"),Piece("ship","E2")]);
        Assert.Equal(3,delta.Length);Assert.Equal(3,timeline.Highlighted.Length);
        Assert.Empty(timeline.Observe(5,"P2",delta));
    }

    [Fact]
    public void RemovedBuildingAlsoLosesItsCurrentTurnHighlight()
    {
        var timeline=new TabletopBuildTimeline();timeline.Observe(5,"P2",[]);
        timeline.Observe(5,"P2",[Piece("road","E1")]);
        timeline.Observe(5,"P2",[]);
        Assert.Empty(timeline.Highlighted);
    }

    [Fact]
    public void ReplacedRemovedAndMovedPiecesInvalidateTheirEarlierInFlightIdentities()
    {
        var timeline=new TabletopBuildTimeline();timeline.Observe(5,"P2",[]);
        var village=Piece("settlement");var knight=Piece("knight","V2");var ship=Piece("ship","E1");
        timeline.Observe(5,"P2",[village,knight,ship]);
        Assert.All(new[]{village,knight,ship},piece=>Assert.True(timeline.Contains(piece)));
        var city=Piece("city");var promoted=Piece("knight","V2",level:2);var movedShip=Piece("ship","E2");
        timeline.Observe(5,"P2",[city,promoted,movedShip]);
        Assert.All(new[]{village,knight,ship},piece=>Assert.False(timeline.Contains(piece)));
        Assert.All(new[]{city,promoted,movedShip},piece=>Assert.True(timeline.Contains(piece)));
        timeline.Observe(5,"P2",[]);
        Assert.All(new[]{city,promoted,movedShip},piece=>Assert.False(timeline.Contains(piece)));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void EveryAiSpeedLeavesTimeForItsBuildToLandBeforeTheNextAction(int speed)
    {
        Assert.True(TabletopAiSpeed.Interval(speed)>TabletopAiSpeed.FlightDuration(speed));
        Assert.True(TabletopAiSpeed.FlightDuration(speed)>=.3);
    }

    [Fact]
    public void TheThreeSpeedsAreOrderedAndInvalidSettingsAreClamped()
    {
        Assert.True(TabletopAiSpeed.Interval(0)>TabletopAiSpeed.Interval(1));
        Assert.True(TabletopAiSpeed.Interval(1)>TabletopAiSpeed.Interval(2));
        Assert.Equal(0,TabletopAiSpeed.Clamp(-4));Assert.Equal(2,TabletopAiSpeed.Clamp(7));
    }
}
