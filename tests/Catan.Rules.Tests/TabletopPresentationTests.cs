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
    [InlineData("B5MetropolisRelocate","迁移大都会","己方城市")]
    public void UnresolvedPieceChoicesRemainVisibleUntilTheDecisionChanges(string kind,string action,string piece)
    {
        var sequence=new TabletopPromptSequence();
        string instruction=TabletopSelectionPrompt.Decision(kind);
        Assert.True(TabletopSelectionPrompt.IsDirectMapDecision(kind));
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
    static Catan.Core.GameHistoryEntry History(long sequence,string kind)
    {
        var entry=new Catan.Core.GameHistoryEntry();
        typeof(Catan.Core.GameHistoryEntry).GetProperty("Sequence")!.SetValue(entry,sequence);
        typeof(Catan.Core.GameHistoryEntry).GetProperty("Kind")!.SetValue(entry,kind);
        return entry;
    }
    [Fact]
    public void HarvestNarrationAndFollowingChoicesWaitForTheActualDiceToSettle()
    {
        var dice=new TabletopDicePresentation();dice.Observe(1,4,3,1,0);
        var before=History(1,"RollDice");var roll=History(2,"RollDice");var choice=History(3,"ResolveChoice");
        var entries=new[]{before,roll,choice};
        dice.Observe(2,5,3,2,10);
        Assert.Equal(10.95,dice.SettlesAt,6);
        Assert.Equal(new[]{before},dice.VisibleHistory(entries,10));
        Assert.Equal(new[]{before},dice.VisibleHistory(entries,10.949));
        Assert.Equal(entries,dice.VisibleHistory(entries,10.95));
        // 重绘不能刷新截止时刻；原投影对象与数组保持不变。
        dice.Observe(2,5,3,2,10.8);Assert.Equal(10.95,dice.SettlesAt,6);Assert.Equal(3,entries.Length);
    }
    [Theory]
    [InlineData("RollDice")]
    [InlineData("PlayProgressCard")]
    public void HistoryArrivingBeforeItsNetworkViewCannotShowOrConsumeFutureRollOutcomes(string kind)
    {
        var dice=new TabletopDicePresentation();dice.Observe(1,3,2,null,0);
        var before=History(1,"RollDice");var roll=History(5,kind);var choice=History(6,"ChooseGoldResources");
        typeof(Catan.Core.GameHistoryEntry).GetProperty("Dice1")!.SetValue(roll,4);
        typeof(Catan.Core.GameHistoryEntry).GetProperty("Dice2")!.SetValue(roll,4);
        var entries=new[]{before,roll,choice};
        Assert.Equal(new[]{before},dice.KnownHistory(entries));
        Assert.Equal(new[]{before},dice.VisibleHistory(entries,10));
        dice.Observe(5,4,4,null,20);
        Assert.Equal(entries,dice.KnownHistory(entries));
        Assert.Equal(new[]{before},dice.VisibleHistory(entries,20.9));
        Assert.Equal(entries,dice.VisibleHistory(entries,20.95));
        var restored=new TabletopDicePresentation();restored.Observe(5,4,4,null,100);
        Assert.Equal(entries,restored.VisibleHistory(entries,100));
    }
    [Fact]
    public void DiceHandCountsHoldBothCategoriesAndEachSeatUntilSettledAndResetBetweenGames()
    {
        var counts=new TabletopHandCountPresentation();
        Assert.Equal(3,counts.Observe("game1","P1",false,3,false));
        Assert.Equal(1,counts.Observe("game1","P1",true,1,false));
        Assert.Equal(6,counts.Observe("game1","P2",false,6,false));
        Assert.Equal(3,counts.Observe("game1","P1",false,5,true));
        Assert.Equal(1,counts.Observe("game1","P1",true,2,true));
        Assert.Equal(6,counts.Observe("game1","P2",false,8,true));
        Assert.Equal(3,counts.Observe("game1","P1",false,5,true));
        Assert.Equal(5,counts.Observe("game1","P1",false,5,false));
        Assert.Equal(2,counts.Observe("game1","P1",true,2,false));
        Assert.Equal(8,counts.Observe("game1","P2",false,8,false));
        Assert.Equal(0,counts.Observe("game2","P1",false,0,true));
        Assert.Equal(4,counts.Observe("game2","P2",false,4,true));
        // 首次连接已在途的局面只建立当前计数基线，不从其他席位猜测隐藏手牌。
        Assert.Equal(7,counts.Observe("game2","P3",false,7,true));
    }
    [Fact]
    public void EmptyBankMarkerWaitsForDiceSettlementAndDoesNotBorrowAnotherResourceCount()
    {
        var counts=new TabletopHandCountPresentation();
        Assert.Equal(1,counts.Observe("game","bank:0",false,1,false));
        Assert.Equal(4,counts.Observe("game","bank:1",false,4,false));
        Assert.Equal(1,counts.Observe("game","bank:0",false,0,true));
        Assert.Equal(4,counts.Observe("game","bank:1",false,2,true));
        Assert.Equal(0,counts.Observe("game","bank:0",false,0,false));
        Assert.Equal(2,counts.Observe("game","bank:1",false,2,false));
    }
    [Theory]
    [InlineData(282,409)]
    [InlineData(682,919)]
    [InlineData(824,754)]
    public void HandsStartAtTheSameLeftEdgeAndNeverHavePositiveGaps(float width,float center)
    {
        foreach(int count in new[]{1,3,8,20,60,120})
        {
            var first=TabletopHandLayout.Card(0,count,center,width,false);
            Assert.Equal(center-width/2,first.X,3);
            Assert.Equal(108,TabletopHandLayout.CardWidth);Assert.Equal(151,TabletopHandLayout.CardHeight);
            for(int i=0;i<count;i++)
            {
                var pose=TabletopHandLayout.Card(i,count,center,width,false);
                Assert.Equal(0,pose.Angle);Assert.Equal(714,pose.Y);
                if(i>0)
                {
                    float step=pose.X-TabletopHandLayout.Card(i-1,count,center,width,false).X;
                    Assert.InRange(step,TabletopHandLayout.MinimumExposure-.001f,TabletopHandLayout.CardWidth+.001f);
                    if(count*TabletopHandLayout.CardWidth<=width)Assert.Equal(TabletopHandLayout.CardWidth,step,3);
                }
                Assert.Equal(pose.Y-22,TabletopHandLayout.Card(i,count,center,width,true).Y);
            }
            float scroll=TabletopHandLayout.MaximumScroll(count,width);
            var last=TabletopHandLayout.Card(count-1,count,center,width,false,scroll);
            Assert.True(last.X+TabletopHandLayout.CardWidth<=center+width/2+.001f);
            if(scroll>0)Assert.Equal(center+width/2,last.X+TabletopHandLayout.CardWidth,3);
        }
    }
    [Fact]
    public void ResourceAndSpecialHandsReserveTheSameLeftAlignedArea()
    {
        Assert.Equal(754,TabletopHandLayout.ResourceCenter(false));Assert.Equal(754,TabletopHandLayout.SpecialCenter(false));
        Assert.Equal(342,TabletopHandLayout.Card(0,1,754,824,false).X);
        Assert.Equal(TabletopHandLayout.SpecialCenter(true),TabletopHandLayout.ResourceCenter(true));
        Assert.Equal(TabletopHandLayout.SpecialWidth(true),TabletopHandLayout.ResourceWidth(true));
        Assert.Equal(0,TabletopHandLayout.HoverSpread);
        Assert.Equal(0,TabletopHandLayout.ContentWidth(0,824));
    }
    [Fact]
    public void ScrollIsClampedAndEveryCardCanBeReachedWithAClickableStrip()
    {
        const int count=120;const float width=824,center=754;
        Assert.Equal(0,TabletopHandLayout.ClampScroll(-100,count,width));
        Assert.Equal(TabletopHandLayout.MaximumScroll(count,width),TabletopHandLayout.ClampScroll(float.MaxValue,count,width));
        for(int index=0;index<count;index++)
        {
            float scroll=TabletopHandLayout.ClampScroll(index*TabletopHandLayout.MinimumExposure,count,width);
            var pose=TabletopHandLayout.Card(index,count,center,width,false,scroll);
            Assert.InRange(pose.X,TabletopHandLayout.RestLeft-.001f,TabletopHandLayout.RestRight-TabletopHandLayout.MinimumExposure+.001f);
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CollectionAndStaticHandGeometryShareExactPositionsAndScroll(bool mixed)
    {
        float center=TabletopHandLayout.ResourceCenter(mixed),width=TabletopHandLayout.ResourceWidth(mixed);
        foreach(int count in new[]{1,5,20,60})
        {
            var slots=Enumerable.Range(0,count).Select(index=>new TabletopResourceSlot{Good=index%5,Ordinal=index/5}).ToArray();
            foreach(float scroll in new[]{0,TabletopHandLayout.MaximumScroll(count,width)})
            for(int index=0;index<count;index++)
            {
                var pose=TabletopHandLayout.Resource(slots,index,center,width,false,scroll);
                Assert.Equal(TabletopHandLayout.Card(index,count,center,width,false,scroll).X,pose.X,3);
            }
        }
    }
}
