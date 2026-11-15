using Catan.Core.ThreeKingdoms;
using Xunit;

namespace Catan.Rules.Tests;

public sealed class HandSelectionConfirmationTests
{
    static ThreeKingdomsB5CivilTestHost Pending(string kind,string decision,int required=2)
    {
        string general=kind=="civil-yicheng"?"tk-g32":kind=="civil-jieyin-discard"?"tk-g27":kind=="civil-chouce"?"tk-g36":kind=="civil-jianshu-duel"?"tk-g11":"tk-g31";
        var h=ThreeKingdomsB5CivilTestHost.Create(general);
        h.State.RulesVersion=GeneralCatalog.B5DirectRulesVersion;h.State.PackageVersion=GeneralCatalog.B5DirectPackageVersion;h.State.PackageContentHash=GeneralCatalog.B5DirectContentHash;
        h.Inner.Give("P1",3,1,0,0,0,2);
        var g=h.State.Generals.First(x=>x.OwnerPlayerId=="P1"&&x.Region==GeneralRegion.Active);
        var f=new B2SkillFrame{Id="整批选牌",Kind=kind,SkillId=g.GeneralId+"-s1",PlayerId="P1",TenureId=g.TenureId,Started=true,Remaining=required,
            Targets=kind=="civil-fencheng"?new[]{"P1","P2","P3"}:new[]{"P1","P2"},
            B4=new(){OwnerPlayerId="P1"},CivilB5=new(){OwnerPlayerId="P1",OtherPlayerId=kind=="civil-chouce"?"P1":null,BoardKind=kind=="civil-fencheng"?"discard":null,Budget=required-1}};
        h.State.Skills.Frames=new[]{f};B2SkillEngine.Pump(h);
        Assert.Equal(decision,h.State.Skills.PendingDecision.Kind);return h;
    }
    static ThreeKingdomsView View(ThreeKingdomsB5CivilTestHost h)
    {var v=new ThreeKingdomsView{PackageVersion=h.State.PackageVersion,WindowId=h.State.WindowId,Clock=h.State.Clock};B2SkillEngine.Project(h,v,"P1");return v;}
    static B45SelectionContext Draft(ThreeKingdomsB5CivilTestHost h)
    {var c=new B45SelectionContext();c.Begin(View(h),"P1",null,h.GetHand("P1"));return c;}

    [Theory]
    [InlineData("civil-fencheng","civil-fencheng-discard")]
    [InlineData("civil-jieyin-discard","civil-hand-select")]
    [InlineData("civil-chouce","civil-hand-select")]
    [InlineData("civil-yicheng","civil-hand-select")]
    [InlineData("civil-jianshu-duel","civil-subset")]
    public void PickingAndRemovingCopiesIsPureAndNeverAutoSubmits(string kind,string decision)
    {
        var h=Pending(kind,decision);string before=h.Snapshot();var c=Draft(h);
        Assert.True(c.CivilHandBatch);Assert.False(c.ShowOptionChoices);
        Assert.True(c.ToggleHand(0,2));Assert.True(c.ToggleHand(5,1));Assert.True(c.CanConfirm);
        Assert.Null(c.FinishDirectPick());Assert.True(c.ToggleHand(0,2));Assert.False(c.IsHandSelected(0,2));
        Assert.True(c.ToggleHand(0,0));Assert.True(c.CanConfirm);
        Assert.Equal(before,h.Snapshot());Assert.Equal("civil-selected-batch",c.BuildCommand().Option);
        Assert.Equal(new[]{1,0,0,0,0,1,0,0},c.BuildCommand().Counts);
        Assert.NotNull(c.BeginSubmit());Assert.Null(c.BeginSubmit());
    }
    [Fact] public void FenchengCommitsChosenBatchOnceAndRaisesNextPlayersMinimum()
    {
        var h=Pending("civil-fencheng","civil-fencheng-discard");h.Inner.Give("P2",4);
        var c=Draft(h);Assert.False(c.CanConfirm);c.ToggleHand(0,0);Assert.False(c.CanConfirm);c.ToggleHand(5,1);
        var command=c.BeginSubmit();var restored=h.Copy();B2SkillEngine.Resolve(h,"P1",command);B2SkillEngine.Resolve(restored,"P1",command);
        Assert.Equal(restored.Snapshot(),h.Snapshot());Assert.Equal(new[]{2,1,0,0,0,1,0,0},h.GetHand("P1"));
        var next=B2SkillEngine.Actions(h,"P2").Single(a=>a.Command.Option=="civil-fencheng-hand").Command;
        B2SkillEngine.Resolve(h,"P2",next);Assert.All(B2SkillEngine.Actions(h,"P2").Where(a=>a.Command.Option=="civil-select"),a=>Assert.Equal(3,a.MinimumHandCount));
        string before=h.Snapshot();Assert.Throws<InvalidOperationException>(()=>B2SkillEngine.Resolve(h,"P1",command));Assert.Equal(before,h.Snapshot());h.Conserved();
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(7)][InlineData(-1)]
    public void InvalidBatchNeverMutatesPendingOrCards(int count)
    {
        var h=Pending("civil-jieyin-discard","civil-hand-select");var command=B45SelectionContract.Copy(B2SkillEngine.Actions(h,"P1").First().Command);
        command.Option="civil-selected-batch";command.Counts=new[]{count,0,0,0,0,0,0,0};string before=h.Snapshot();
        Assert.Throws<InvalidOperationException>(()=>B2SkillEngine.Resolve(h,"P1",command));Assert.Equal(before,h.Snapshot());
    }
    [Fact] public void LegacyFullySelectedFenchengCanBeRechosenAfterReconnect()
    {
        var h=Pending("civil-fencheng","civil-fencheng-discard");
        for(int i=0;i<6;i++){var a=B2SkillEngine.Actions(h,"P1").First(a=>a.Command.Option=="civil-select");B2SkillEngine.Resolve(h,"P1",a.Command);}
        var c=Draft(h);Assert.True(c.CivilHandBatch);Assert.False(c.ShowOptionChoices);Assert.False(c.CanConfirm);
        Assert.True(c.ToggleHand(0,1));Assert.True(c.ToggleHand(5,1));B2SkillEngine.Resolve(h,"P1",c.BeginSubmit());Assert.Equal(4,h.GetHand("P1").Sum());h.Conserved();
    }
    [Theory][InlineData("civil-jieyin-discard")][InlineData("civil-chouce")]
    public void ExactDiscardIsSettledOnlyAfterConfirmation(string kind)
    {
        var h=Pending(kind,"civil-hand-select");var c=Draft(h);c.ToggleHand(0,2);c.ToggleHand(5,0);
        Assert.False(c.ToggleHand(1,0));B2SkillEngine.Resolve(h,"P1",c.BeginSubmit());Assert.Equal(4,h.GetHand("P1").Sum());h.Conserved();
    }
    [Fact] public void YichengSingleUseSettlesWithTheCostConfirmation()
    {
        var h=Pending("civil-yicheng","civil-hand-select");h.VirtualUses.Add(new(){CardId="Taxation",OptionId="主教用途",Label="主教"});
        var c=Draft(h);c.ToggleHand(0,1);c.ToggleHand(5,0);B2SkillEngine.Resolve(h,"P1",c.BeginSubmit());
        Assert.Single(h.Used);Assert.Equal(new[]{2,1,0,0,0,1,0,0},h.GetHand("P1"));h.Conserved();
    }
    [Fact] public void YichengMultipleUsesRetainTheChosenCostsUntilARealChoice()
    {
        var h=Pending("civil-yicheng","civil-hand-select");h.VirtualUses.Add(new(){CardId="Taxation",OptionId="主教用途一",Label="用途一"});h.VirtualUses.Add(new(){CardId="Taxation",OptionId="主教用途二",Label="用途二"});
        var c=Draft(h);c.ToggleHand(0,1);c.ToggleHand(5,0);B2SkillEngine.Resolve(h,"P1",c.BeginSubmit());
        Assert.Empty(h.Used);Assert.Equal(6,h.GetHand("P1").Sum());Assert.Equal("civil-yicheng-use",h.State.Skills.PendingDecision.Kind);
        var use=B2SkillEngine.Actions(h,"P1").First(a=>a.Command.Option=="civil-yicheng-use").Command;
        B2SkillEngine.Resolve(h,"P1",use);Assert.Single(h.Used);Assert.Equal(new[]{2,1,0,0,0,1,0,0},h.GetHand("P1"));h.Conserved();
    }
    [Fact] public void YichengMissingUseRejectsWithoutChangingCardsOrDecision()
    {
        var h=Pending("civil-yicheng","civil-hand-select");var c=Draft(h);c.ToggleHand(0,1);c.ToggleHand(5,0);var command=c.BeginSubmit();string before=h.Snapshot();
        Assert.Throws<InvalidOperationException>(()=>B2SkillEngine.Resolve(h,"P1",command));Assert.Equal(before,h.Snapshot());
    }
    [Fact] public void JianshuBatchRemainsHiddenUntilTheLaterGuessIsResolved()
    {
        var h=Pending("civil-jianshu-duel","civil-subset");var c=Draft(h);c.ToggleHand(0,1);c.ToggleHand(5,0);
        B2SkillEngine.Resolve(h,"P1",c.BeginSubmit());Assert.Equal("civil-stone-declare",h.State.Skills.PendingDecision.Kind);
        var declarations=B2SkillEngine.Actions(h,"P1").Where(a=>a.Command.Option=="civil-stone-declare").ToArray();
        Assert.Equal(new[]{0,1,2},declarations.Select(a=>a.Command.CardType));Assert.All(declarations,a=>{Assert.Null(a.RealCardType);Assert.Equal("number",a.CardSelectionKind);});
        var other=new ThreeKingdomsView();B2SkillEngine.Project(h,other,"P3");Assert.Empty(other.SkillActions);Assert.All(other.RevealedSkillCards,n=>Assert.Equal(0,n));
        Assert.Equal(6,h.GetHand("P1").Sum());h.Conserved();
    }
}
