using Catan.Core;
using Xunit;

namespace Catan.Rules.Tests;

public sealed class OnlinePresentationPerformanceTests
{
    static T Data<T>(object fields) where T:new()
    {var value=new T();foreach(var property in fields.GetType().GetProperties())typeof(T).GetProperty(property.Name)!.SetValue(value,property.GetValue(fields));return value;}
    static GameHistoryEntry Entry(int sequence,string? action=null,int dice1=0,int dice2=0,string? skill=null,int amount=0)=>Data<GameHistoryEntry>(new{Sequence=(long)sequence,Action=action,Dice1=dice1,Dice2=dice2,SkillEventId=skill,Changes=amount==0?Array.Empty<GameHistoryChange>():new[]{Data<GameHistoryChange>(new{Category="goods",GoodIndex=0,PlayerId="P1",Amount=amount})}});
    [Fact]
    public void HistoryViewportOnlyMeasuresNewOrReplacedRowsAndFindsVisibleRange()
    {
        var rows=Enumerable.Range(1,2000).Select(n=>Entry(n)).ToArray();
        var layout=new TabletopHistoryLayout();int measured=0;
        float Height(GameHistoryEntry entry,float width){measured++;return 20;}
        layout.Bind(rows,300,Height);Assert.Equal(2000,measured);Assert.Equal(40000,layout.Total);
        for(int frame=0;frame<120;frame++)layout.Bind(rows,300,Height);
        Assert.Equal(2000,measured);Assert.Equal(1000,layout.FirstVisible(20000));
        var replaced=(GameHistoryEntry[])rows.Clone();replaced[1000]=Entry(1001,"补充技能结果");
        layout.Bind(replaced,300,Height);Assert.Equal(2001,measured);
        layout.Bind(replaced,400,Height);Assert.Equal(4001,measured);
    }
    [Fact]
    public void DiceAndOperationHistoryCachesInvalidateOnContentReplacementWithoutSequenceChange()
    {
        var history=new[]{Entry(1),Entry(2,dice1:2,dice2:3)};
        var snapshot=new OperationSnapshot{PublicSequence=1};var filtered=snapshot.PresentedHistory(history);
        Assert.Same(filtered,snapshot.PresentedHistory(history));
        var dice=new TabletopDicePresentation();dice.Observe(1,1,1,null,0);
        var known=dice.KnownHistory(history);Assert.Same(known,dice.KnownHistory(history));
        dice.Observe(2,2,3,null,1);Assert.Same(history,dice.KnownHistory(history));
        var rolling=dice.VisibleHistory(history,1.1);Assert.Same(rolling,dice.VisibleHistory(history,1.2));
        Assert.Same(history,dice.VisibleHistory(history,3));
        var updated=new[]{Entry(1,"补充"),history[1]};
        Assert.NotSame(filtered,snapshot.PresentedHistory(updated));
    }
    [Fact]
    public void SameCommandNewSkillAndUpdatedSkillOnlyEnqueueNewVisibleGains()
    {
        var timeline=new TabletopCollectionTimeline();
        var command=Entry(1);timeline.Observe("局1",new[]{command},0);
        GameHistoryEntry Skill(int amount)=>Entry(1,skill:"skill-1",amount:amount);
        var skill=Skill(1);timeline.Observe("局1",new[]{command,skill},1);Assert.Single(timeline.Active(1));
        var updated=Skill(2);var history=new[]{command,updated};timeline.Observe("局1",history,1.1);Assert.Equal(2,timeline.Active(1.1).Length);
        timeline.Observe("局1",history,1.2);Assert.Equal(2,timeline.Active(1.2).Length);
        timeline.Observe("局1",new[]{command,Skill(2)},1.3);Assert.Equal(2,timeline.Active(1.3).Length);
    }
    [Fact]
    public void LargeAnimationBacklogRecoversToCurrentCountsWithBoundedDecorationQueue()
    {
        var timeline=new TabletopCollectionTimeline();timeline.Observe("局1",Array.Empty<GameHistoryEntry>(),0);
        var gain=Entry(1,amount:1000);
        timeline.Observe("局1",new[]{gain},1);Assert.Empty(timeline.Active(1));Assert.True(timeline.RecoveryUntil>1);
        Assert.Equal(1000,timeline.HandSlots("P1",new[]{1000,0,0,0,0,0,0,0},5,2).Length);
    }
}
