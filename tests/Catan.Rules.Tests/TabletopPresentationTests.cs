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
    [InlineData("PillageCity","降级为村庄","城市")]
    [InlineData("ProgressTreasonRemove","叛变","己方骑士")]
    [InlineData("ProgressTreasonPlace","安置","替补骑士")]
    [InlineData("DisplacedKnight","安置","被驱逐的骑士")]
    [InlineData("Metropolis","大都会","城市")]
    public void UnresolvedPieceChoicesRemainVisibleUntilTheDecisionChanges(string kind,string action,string piece)
    {
        var sequence=new TabletopPromptSequence();
        string instruction=TabletopSelectionPrompt.Decision(kind);
        Assert.Contains(action,instruction);Assert.Contains(piece,instruction);
        sequence.RequireSelection(instruction);
        sequence.Show(10,"你的回合",instruction);
        Assert.Equal(instruction,sequence.At(10));Assert.Equal(instruction,sequence.At(120));
        // 保存、错误点击等短暂通知不能盖掉尚未完成的选择。
        sequence.Show(120,"游戏已保存");Assert.Equal(instruction,sequence.At(120));Assert.Equal(instruction,sequence.At(600));
        sequence.RequireSelection("请选择合法位置放下这艘船");Assert.Equal("请选择合法位置放下这艘船",sequence.At(600));
        sequence.RequireSelection("");Assert.Equal("",sequence.At(600));
        sequence.Show(600,"操作完成");Assert.Equal("操作完成",sequence.At(600));Assert.Equal("",sequence.At(602));
    }
    [Theory]
    [InlineData("SetupSettlement")]
    [InlineData("SetupRoad")]
    [InlineData("Discard")]
    [InlineData("RobberMove")]
    [InlineData("RobberSteal")]
    [InlineData("RoadBuilding")]
    [InlineData("GoldChoice")]
    [InlineData("PortPlacement")]
    [InlineData("SetupPort")]
    [InlineData("PendingChoice")]
    public void EveryAuthoritySelectionPhaseRequiresAContinuingPrompt(string phase)
        =>Assert.True(TabletopSelectionPrompt.RequiredPhase(phase));
    [Theory]
    [InlineData("Action")]
    [InlineData("ProductionAwaitRoll")]
    [InlineData("Finished")]
    public void OrdinaryTurnPromptsRetainTheirOriginalDuration(string phase)
        =>Assert.False(TabletopSelectionPrompt.RequiredPhase(phase));
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
    [InlineData(282,409)]
    [InlineData(682,919)]
    [InlineData(992,764)]
    public void GrowingHandsOverlapWithoutShrinkingOrLeavingTheirScreenRegion(float width,float center)
    {
        foreach(int count in new[]{1,3,8,20,60,120})
        {
            var first=TabletopHandLayout.Card(0,count,center,width,false);
            var last=TabletopHandLayout.Card(count-1,count,center,width,false);
            Assert.True(first.X>=center-width/2-.001f);
            Assert.True(last.X+TabletopHandLayout.CardWidth<=center+width/2+.001f);
            Assert.Equal(center,(first.X+last.X+TabletopHandLayout.CardWidth)/2,3);
            Assert.Equal(108,TabletopHandLayout.CardWidth);Assert.Equal(151,TabletopHandLayout.CardHeight);
            for(int i=0;i<count;i++)
            {
                var pose=TabletopHandLayout.Card(i,count,center,width,false);
                Assert.Equal(0,pose.Angle);Assert.Equal(714,pose.Y);
                if(i>0)Assert.True(pose.X>TabletopHandLayout.Card(i-1,count,center,width,false).X);
                Assert.Equal(pose.Y-22,TabletopHandLayout.Card(i,count,center,width,true).Y);
            }
            if(count>width/TabletopHandLayout.CardWidth)
                Assert.True(TabletopHandLayout.Card(1,count,center,width,false).X-first.X<TabletopHandLayout.CardWidth);
        }
    }
    [Fact]
    public void ASingleHandStaysAtTheFrontCenter()
    {
        Assert.Equal(764,TabletopHandLayout.ResourceCenter(false));Assert.Equal(764,TabletopHandLayout.SpecialCenter(false));
        Assert.Equal(TabletopHandLayout.SpecialCenter(true),TabletopHandLayout.ResourceCenter(true));
        Assert.Equal(TabletopHandLayout.SpecialWidth(true),TabletopHandLayout.ResourceWidth(true));
    }

    [Theory]
    [InlineData(false,false)]
    [InlineData(false,true)]
    [InlineData(true,false)]
    [InlineData(true,true)]
    public void ExpandedHandsLeaveTheStatusBoxAndEndTurnControlsUncovered(bool special,bool mixed)
    {
        float center=special?TabletopHandLayout.SpecialCenter(mixed):TabletopHandLayout.ResourceCenter(mixed);
        float width=special?TabletopHandLayout.SpecialWidth(mixed):TabletopHandLayout.ResourceWidth(mixed);
        foreach(int count in new[]{1,8,20,60,120})
        {
            var first=TabletopHandLayout.Card(0,count,center,width,false);
            var last=TabletopHandLayout.Card(count-1,count,center,width,false);
            // 留出纸影空间；状态框右边为238，取消选择和结束回合从1290开始。
            Assert.True(first.X-TabletopHandLayout.HoverSpread>=244-.001f);
            Assert.True(last.X+TabletopHandLayout.CardWidth+TabletopHandLayout.HoverSpread<=1284+.001f);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CollectionFlightDestinationsStayInsideTheSameReservedHandRegion(bool mixed)
    {
        float center=TabletopHandLayout.ResourceCenter(mixed),width=TabletopHandLayout.ResourceWidth(mixed);
        foreach(double arrivingWeight in new[]{.001,.25,.5,1})
        {
            var slots=Enumerable.Range(0,60).Select(index=>new TabletopResourceSlot{Good=index%5,Ordinal=index/5,Weight=index==30?arrivingWeight:1}).ToArray();
            for(int index=0;index<slots.Length;index++)
            {
                var pose=TabletopHandLayout.Resource(slots,index,center,width,false);
                Assert.True(pose.X>=268-.001f);
                Assert.True(pose.X+TabletopHandLayout.CardWidth<=1260+.001f);
            }
        }
    }
}
