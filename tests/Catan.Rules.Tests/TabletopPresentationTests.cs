using Xunit;

namespace Catan.Rules.Tests;

// 核对可观察的拿取、屏幕边界和提示时长，不复制游戏规则。
public sealed class TabletopPresentationTests
{
    [Fact]
    public void PickingOneHouseNeverSelectsItsNeighbours()
    {
        var selection=new OperationSelection();
        var house=new OperationBuild{Id="house",Count=5};
        selection.Choose(house,new int[8],3);
        Assert.Equal(new[]{false,false,false,true,false},Enumerable.Range(0,5).Select(i=>selection.IsPieceSelected("house",i)));
        Assert.False(selection.IsPieceSelected("city",3));
        selection.Choose(house,new int[8],0);
        Assert.True(selection.IsPieceSelected("house",0));
        Assert.False(selection.IsPieceSelected("house",3));
        selection.Clear();Assert.Equal(-1,selection.PieceOrdinal);
    }
    [Theory]
    [InlineData(-2)]
    [InlineData(5)]
    [InlineData(100)]
    public void InvalidPieceIndexPreservesExistingSelection(int ordinal)
    {
        var selection=new OperationSelection();var house=new OperationBuild{Id="house",Count=5};
        selection.Choose(house,new int[8],2);selection.Choose(house,new int[8],ordinal);
        Assert.True(selection.IsPieceSelected("house",2));
    }
    [Fact]
    public void ResourceSelectionReleasesTheIndividualPiece()
    {
        var selection=new OperationSelection();selection.Choose(new OperationBuild{Id="house",Count=5},new int[8],2);
        selection.Toggle(0,0,1);Assert.Null(selection.BuildId);Assert.Equal(-1,selection.PieceOrdinal);
    }
    [Fact]
    public void OnlyTheMissingUnitsOfARepeatedCostAreRed()
    {
        var cost=new[]{3,0,1,0,1,0,0,0};var owned=new[]{2,0,1,0,0,0,0,0};
        var tokens=TabletopCostToken.For(cost,owned);
        Assert.Equal(new[]{"木头","木头","木头","羊毛","铁矿"},tokens.Select(t=>t.Name));
        Assert.Equal(new[]{false,false,true,false,true},tokens.Select(t=>t.Missing));
        Assert.Equal(2,owned[0]);Assert.Equal(3,cost[0]);
        Assert.Empty(TabletopCostToken.For(new int[8],owned));
    }
    [Fact]
    public void TurnThenRollPromptsEachLastExactlyOneAndAHalfSeconds()
    {
        var sequence=new TabletopPromptSequence();sequence.Show(10,"你的回合","请掷骰");
        Assert.Equal("你的回合",sequence.At(10));Assert.Equal("你的回合",sequence.At(11.499));
        Assert.Equal("请掷骰",sequence.At(11.5));Assert.Equal("请掷骰",sequence.At(12.999));
        Assert.Equal("",sequence.At(13));Assert.Equal("",sequence.At(60));
        sequence.Show(60,"请选择合法位置");Assert.Equal("请选择合法位置",sequence.At(60));Assert.Equal("",sequence.At(61.5));
    }
    [Fact]
    public void ANewActionReplacesStalePrompts()
    {
        var sequence=new TabletopPromptSequence();sequence.Show(10,"你的回合","请掷骰");
        sequence.Show(10.5,"操作完成");Assert.Equal("操作完成",sequence.At(10.5));Assert.Equal("",sequence.At(12));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void DiceSettleOnThePublicResultIncludingEveryEventFace(int eventFace)
    {
        var dice=new TabletopDicePresentation();dice.Observe(0,0,0,eventFace,1);
        dice.Observe(7,3,5,eventFace,2);Assert.True(dice.IsRolling(2));Assert.True(dice.IsRolling(2.94));
        Assert.False(dice.IsRolling(3));Assert.Equal(3,dice.Face(0,3));Assert.Equal(5,dice.Face(1,3));Assert.Equal(eventFace,dice.Face(2,3));
        Assert.Equal(3,dice.First);Assert.Equal(5,dice.Second);
    }
    [Fact]
    public void EqualResultsStillAnimateNewRollsAndRepaintsDoNotRestartThem()
    {
        var dice=new TabletopDicePresentation();dice.Observe(5,4,4,null,1);
        Assert.False(dice.IsRolling(1));Assert.Null(dice.Event);
        dice.Observe(6,4,4,null,2);Assert.True(dice.IsRolling(2));
        dice.Observe(6,4,4,null,2.8);Assert.False(dice.IsRolling(3));
        dice.Observe(7,4,4,null,4);Assert.True(dice.IsRolling(4));
    }
    [Fact]
    public void ReconnectingASavedViewDoesNotReplayItsOldRoll()
    {
        var dice=new TabletopDicePresentation();dice.Observe(71,2,6,2,500);
        Assert.False(dice.IsRolling(500));Assert.Equal(2,dice.Face(0,500));Assert.Equal(6,dice.Face(1,500));Assert.Equal(2,dice.Face(2,500));
    }
    [Theory]
    [InlineData(380,356,8)]
    [InlineData(684,948,18)]
    [InlineData(684,720,18)]
    [InlineData(380,720,8)]
    public void EveryRotatedCardAndItsShadowFitsInsideTheScreen(float width,float center,int maximum)
    {
        for(int count=1;count<=maximum;count++)for(int i=0;i<count;i++)foreach(bool selected in new[]{false,true})
        {
            var pose=TabletopHandLayout.Card(i,count,center,width,selected);
            float cx=pose.X+TabletopHandLayout.CardWidth/2,cy=pose.Y+TabletopHandLayout.CardHeight/2;
            Assert.InRange(cx-pose.HalfWidth,12,1428);Assert.InRange(cx+pose.HalfWidth+5,12,1428);
            Assert.InRange(cy-pose.HalfHeight,735,895);Assert.InRange(cy+pose.HalfHeight+7,735,895);
        }
        var left=TabletopHandLayout.Card(0,maximum,center,width,false);var right=TabletopHandLayout.Card(maximum-1,maximum,center,width,false);
        Assert.Equal(center,(left.X+right.X+TabletopHandLayout.CardWidth)/2,3);
    }
    [Fact]
    public void ASingleHandStaysAtTheFrontCenter()
    {
        Assert.Equal(720,TabletopHandLayout.ResourceCenter(false));Assert.Equal(720,TabletopHandLayout.SpecialCenter(false));
        Assert.True(TabletopHandLayout.SpecialCenter(true)<TabletopHandLayout.ResourceCenter(true));
    }
}
