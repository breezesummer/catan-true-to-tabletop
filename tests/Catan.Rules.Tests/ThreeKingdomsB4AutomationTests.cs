using Catan.Core.ThreeKingdoms;
using Xunit;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB4AutomationTests
{
    [Fact]
    public void RetirementIntentsArePerSeatAndWaitForThatPlayersActualBoundary()
    {
        var s=new ThreeKingdomsState { RulesVersion=GeneralCatalog.B4RulesVersion, OpeningComplete=true,
            Clock=new ThreeKingdomsClock { ActivePlayerId="P1",Round=2,TurnInstanceId=5,Phase=ThreeKingdomsPhase.Play } };
        B4AutomationEngine.SetRetirement(s,"P1",ThreeKingdomsPhase.Preparation,false);
        Assert.False(B4AutomationEngine.TakeRetirement(s));
        s.Clock.ActivePlayerId="P2";s.Clock.TurnInstanceId=6;
        B4AutomationEngine.SetRetirement(s,"P2",ThreeKingdomsPhase.End,false);
        Assert.Equal(2,B4AutomationEngine.Clone(s.Automation).RetirementIntents.Length);
        Assert.Throws<InvalidOperationException>(()=>B4AutomationEngine.SetRetirement(s,"P1",null,true));
        s.Clock.Phase=ThreeKingdomsPhase.Preparation;Assert.False(B4AutomationEngine.TakeRetirement(s));
        s.Clock.Phase=ThreeKingdomsPhase.End;Assert.True(B4AutomationEngine.TakeRetirement(s));
        Assert.False(B4AutomationEngine.TakeRetirement(s));Assert.Single(s.Automation.RetirementIntents);
        s.Clock.ActivePlayerId="P1";s.Clock.Phase=ThreeKingdomsPhase.Preparation;s.Clock.TurnInstanceId=9;
        Assert.True(B4AutomationEngine.TakeRetirement(s));Assert.Empty(s.Automation.RetirementIntents);
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void OpeningIndependentWindowsAndAutomaticStagesResumeWithoutManualAdvance(string rules)
    {
        var game=ThreeKingdomsTestGame.Create(rules,seats:3,skills:true,packageVersion:GeneralCatalog.B4PackageVersion);
        var authority=game.Authority();
        var commands=authority.SeatPlayerIds.Select(id=>game.Tk(id)).Select(v=>new { View=v,Decision=v.PendingDecision }).ToArray();
        Assert.All(commands,x=>Assert.NotNull(x.Decision));
        foreach(var x in commands.Reverse())
        {
            dynamic c=game.View(x.Decision.PlayerId).LegalActions[0]; c.Id=ThreeKingdomsTestGame.NextId();
            Assert.Equal(x.Decision.WindowId,(string)c.WindowId);
            game.AcceptAndResume(c);
        }
        while(!game.Tk().OpeningComplete) game.AcceptAndResume(game.Decide());
        int steps=0;
        while(game.Authority().Clock.TurnInstanceId<3 && steps++<450)
        {
            dynamic c=game.Decide();
            Assert.NotEqual("AdvanceThreeKingdomsPhase",(string)c.Kind.ToString());
            Assert.NotEqual("RollDice",(string)c.Kind.ToString());
            game.Accept(c);
            var tk=game.Authority();
            if(tk.Clock.Phase is ThreeKingdomsPhase.Preparation or ThreeKingdomsPhase.Judgment or ThreeKingdomsPhase.Draw or ThreeKingdomsPhase.End)
                Assert.True(tk.PendingDecision!=null||tk.Skills.PendingDecision!=null||tk.Dice?.PendingDecision!=null||tk.Automation?.PendingDecision!=null||game.View().PendingDecision!=null);
        }
        Assert.True(steps<450);
        Assert.Equal(game.Save(),ThreeKingdomsTestGame.Load(rules,game.Save()).Save());
        game.AssertInvariants();
    }
    [Fact]
    public void AllocatorPreservesRealTypesAndCannotSpendOneCardTwice()
    {
        var s=new ThreeKingdomsState {RulesVersion=GeneralCatalog.B4RulesVersion,SkillsEnabled=true,
            Generals=new[]{new GeneralInstance {GeneralId="tk-g42",Region=GeneralRegion.Active,OwnerPlayerId="P1",RemainingLife=3}}};
        var hand=new[]{0,2,0,0,0,0,0,0};var cost=new[]{1,1,0,0,0,0,0,0};
        var result=B4PaymentAllocator.Allocate(s,"P1",hand,cost);
        Assert.NotNull(result);Assert.Equal(1,result[8]);Assert.Equal(1,result[9]);Assert.Equal(2,result.Sum());
        Assert.Null(B4PaymentAllocator.Allocate(s,"P1",new[]{0,1,0,0,0,0,0,0},cost));
        Assert.Equal(new[]{0,2,0,0,0,0,0,0},hand);
    }
    [Fact]
    public void CommodityConversionIsOneWayAndSelectionMustPayExactly()
    {
        var s=new ThreeKingdomsState {RulesVersion=GeneralCatalog.B4RulesVersion,SkillsEnabled=true,
            Generals=new[]{new GeneralInstance {GeneralId="tk-g44",Region=GeneralRegion.Active,OwnerPlayerId="P1",RemainingLife=3}}};
        var hand=new[]{0,0,0,0,0,1,1,1};var cost=new[]{1,0,1,0,1,0,0,0};
        var result=B4PaymentAllocator.Allocate(s,"P1",hand,cost,hand);
        Assert.NotNull(result);Assert.Equal(1,result[7*8]);Assert.Equal(1,result[5*8+2]);Assert.Equal(1,result[6*8+4]);
        Assert.Null(B4PaymentAllocator.Allocate(s,"P1",cost,hand));
        Assert.Null(B4PaymentAllocator.Allocate(s,"P1",hand,cost,new int[8]));
    }
}
