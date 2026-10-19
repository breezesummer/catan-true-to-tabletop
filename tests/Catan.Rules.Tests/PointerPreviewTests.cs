using Xunit;

namespace Catan.Rules.Tests;

public sealed class PointerPreviewTests
{
    [Theory]
    [InlineData("BuildSettlement","settlement")]
    [InlineData("BuildCity","city")]
    [InlineData("BuildWall","wall")]
    [InlineData("BuildRoad","road")]
    [InlineData("PlaceFreeRoad","road")]
    [InlineData("BuildShip","ship")]
    [InlineData("MoveShip","ship")]
    [InlineData("MoveRobber","robber")]
    [InlineData("MovePirate","pirate")]
    [InlineData("RecruitKnight","knight")]
    [InlineData("PromoteKnight","knight")]
    [InlineData("ActivateKnight","knight")]
    [InlineData("MoveKnight","knight")]
    [InlineData("DisplaceKnight","knight")]
    [InlineData("ChaseRobber","knight")]
    [InlineData("ChasePirate","knight")]
    [InlineData("PlacePort","port")]
    [InlineData("ChooseInvasionRoute","target")]
    public void EveryHeldPieceUsesItsOwnShape(string command,string expected)
        =>Assert.Equal(expected,TabletopPointerPreview.ForCommand(command).Kind);

    [Fact]
    public void SecondCitiesKnightsSetupPlacementUsesCityAndActivationUsesHelmet()
    {
        Assert.Equal("city",TabletopPointerPreview.ForCommand("SetupSettlement",setupCity:true).Kind);
        Assert.Equal("settlement",TabletopPointerPreview.ForCommand("SetupSettlement").Kind);
        var knight=TabletopPointerPreview.ForCommand("ActivateKnight",knightLevel:2);
        Assert.Equal(2,knight.Level);Assert.True(knight.Active);
        Assert.False(TabletopPointerPreview.ForCommand("RecruitKnight",knightActive:true).Active);
    }

    [Theory]
    [InlineData("Engineering","wall")]
    [InlineData("Medicine","city")]
    [InlineData("Merchant","merchant")]
    [InlineData("Invention","target")]
    [InlineData("Taxation","target")]
    [InlineData("Diplomacy","road")]
    [InlineData("Smithing","knight")]
    [InlineData("Intrigue","knight")]
    [InlineData("Treason","knight")]
    public void CardTargetsDoNotMasqueradeAsSettlementsOrRobbers(string card,string expected)
        =>Assert.Equal(expected,TabletopPointerPreview.ForEffect(card).Kind);

    [Theory]
    [InlineData("DisplacedKnight","knight")]
    [InlineData("ProgressTreasonPlace","knight")]
    [InlineData("ProgressTreasonRemove","knight")]
    [InlineData("Metropolis","metropolis")]
    [InlineData("PillageCity","target")]
    public void ForcedMapChoicesUsePublicPieceIdentity(string decision,string expected)
    {
        var preview=TabletopPointerPreview.ForEffect(null!,decision,3,true);
        Assert.Equal(expected,preview.Kind);Assert.Equal(3,preview.Level);Assert.True(preview.Active);
    }
}
