using Catan.Core.ThreeKingdoms;
using Xunit;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB5CivilSkillsTests
{
    private static B2SkillCommand Find(ThreeKingdomsB5CivilTestHost h,string player,string option,int? type=null,string? id=null)=>B2SkillEngine.Actions(h,player).First(a=>a.Command.Option==option&&(!type.HasValue||a.Command.CardType==type)&&(id==null||a.Command.ProgressCardId==id)).Command;
    private static void Do(ThreeKingdomsB5CivilTestHost h,string player,string option,int? type=null,string? id=null)=>B2SkillEngine.Resolve(h,player,Find(h,player,option,type,id));
    private static void Drain(ThreeKingdomsB5CivilTestHost h,bool accept=false)
    {for(int i=0;i<180&&B2SkillEngine.HasPending(h.State);i++){B2SkillEngine.Pump(h);var d=h.State.Skills.PendingDecision;if(d==null)continue;var a=B2SkillEngine.Actions(h,d.PlayerId);Assert.NotEmpty(a);var c=a.FirstOrDefault(a=>a.Command.Option==(accept?"accept":"skip"))??a.FirstOrDefault(a=>a.Command.Option=="civil-fencheng-burn")??a.FirstOrDefault(a=>a.Command.Option=="civil-finish-copy")??a.First();B2SkillEngine.Resolve(h,d.PlayerId,c.Command);}Assert.False(B2SkillEngine.HasPending(h.State));}
    private static void Entry(ThreeKingdomsB5CivilTestHost h){B2SkillEngine.OnCivilGeneralActivated(h.State,h.State.Generals[0]);B2SkillEngine.Pump(h);}
    private static int Counter(ThreeKingdomsB5CivilTestHost h,string key)=>h.State.Generals[0].Counters.FirstOrDefault(c=>c.Key==key)?.Value??0;

    [Fact] public void YingziAddsChosenCommodityAndOneRandomEvenWithoutProduction()
    {
        var h=ThreeKingdomsB5CivilTestHost.Create("tk-g03");h.State.Clock.Phase=ThreeKingdomsPhase.Draw;
        B2SkillEngine.BeginHarvest(h,Array.Empty<B2HarvestAllocation>());Do(h,"P1","card",7);Drain(h);
        Assert.Equal(2,h.GetHand("P1").Sum());Assert.Equal(1,h.GetHand("P1")[7]);Assert.Equal(1,h.Inner.RandomCalls);h.Conserved();
    }
    [Fact] public void FanjianWrongGuessStillGivesCardWhenNoRoadAndConsumesOnlyOnePhaseUse()
    {
        var h=ThreeKingdomsB5CivilTestHost.Create("tk-g03");h.Inner.Give("P1",1);
        Do(h,"P1","civil-fanjian");Assert.Equal("P2",h.State.Skills.PendingDecision.PlayerId);Assert.Empty(B2SkillEngine.Actions(h,"P3"));
        var copy=h.Copy();Do(h,"P2","civil-declare-type",1);Do(copy,"P2","civil-declare-type",1);Drain(h);Drain(copy);
        Assert.Equal(copy.Snapshot(),h.Snapshot());Assert.Equal(1,h.GetHand("P2")[0]);Assert.Equal(0,h.GetHand("P1").Sum());
        Assert.DoesNotContain(B2SkillEngine.Actions(h,"P1"),a=>a.Command.Option=="civil-fanjian");h.Conserved();
    }
    [Fact] public void FanjianWrongGuessLetsOwnerChooseRoadAfterPublicTransfer()
    {
        var h=ThreeKingdomsB5CivilTestHost.Create("tk-g03");h.Inner.Give("P1",0,1);h.AddOption("damage-road","road-p2");
        Do(h,"P1","civil-fanjian");Do(h,"P2","civil-declare-type",0);Assert.Equal("P1",h.State.Skills.PendingDecision.PlayerId);Do(h,"P1","civil-road");Drain(h);
        Assert.Single(h.Applied);Assert.Equal(1,h.State.Skills.RevealedCards[1]);h.Conserved();
    }
    [Fact] public void KurouUsesRecordedPaidLayerAndFreeConstructionPaysZero()
    {
        var h=ThreeKingdomsB5CivilTestHost.Create("tk-g16");h.AddOption("kurou","paid","P1",paid:2);h.AddOption("kurou","free","P1",paid:0);
        Do(h,"P1","civil-kurou",id:"paid");Do(h,"P1","card",5);Do(h,"P1","card",7);Drain(h);Assert.Equal(2,h.GetHand("P1").Sum());
        Do(h,"P1","civil-kurou",id:"free");Drain(h);Assert.Equal(2,h.GetHand("P1").Sum());Assert.Equal(2,h.Applied.Count);h.Conserved();
    }
    [Fact] public void QixiSharesThreeUsesAcrossHandsProgressAndTechnologyAndUsesActualLevelCost()
    {
        var h=ThreeKingdomsB5CivilTestHost.Create("tk-g17");h.Inner.Give("P1",0,8);h.Inner.Give("P2",2);h.DrawProgress("P2",1);h.AddOption("downgrade-tech","tech","P2",3,asset:"Improvement");
        Do(h,"P1","civil-qixi-hand");Drain(h);Do(h,"P1","civil-qixi-progress");Drain(h);Do(h,"P1","civil-qixi-tech");Drain(h);
        Assert.Equal(3,Counter(h,"qixi"));Assert.Equal(2,h.GetHand("P1")[1]);Assert.Single(h.Applied);Assert.Empty(h.GetProgressCards("P2"));
        Assert.DoesNotContain(B2SkillEngine.Actions(h,"P1"),a=>a.Command.Option.StartsWith("civil-qixi-"));h.Conserved();
    }
    [Fact] public void QixiInsufficientBrickOrEmptyTargetNeverOffersPhantomPayment()
    {
        var h=ThreeKingdomsB5CivilTestHost.Create("tk-g17");h.Inner.Give("P1",0,1);h.DrawProgress("P2",0);h.AddOption("downgrade-tech","tech",level:2,asset:"Improvement");
        Assert.DoesNotContain(B2SkillEngine.Actions(h,"P1"),a=>a.Command.Option.StartsWith("civil-qixi-"));h.Conserved();
    }
    [Fact] public void LuanwuLocksSeatOrderRequiresTwoBricksAndNoRoadPenaltyCanFinish()
    {
        var h=ThreeKingdomsB5CivilTestHost.Create("tk-g11");h.Inner.Give("P2",0,2);h.Inner.Give("P3",0,1);
        Do(h,"P1","civil-luanwu");Assert.Equal("P2",h.State.Skills.PendingDecision.PlayerId);Do(h,"P2","civil-pay-bricks");
        Assert.Equal("P3",h.State.Skills.PendingDecision.PlayerId);Assert.DoesNotContain(B2SkillEngine.Actions(h,"P3"),a=>a.Command.Option=="civil-pay-bricks");Do(h,"P3","civil-penalty");Drain(h);
        Assert.Equal(0,h.GetHand("P2").Sum());Assert.Equal(1,h.GetHand("P3").Sum());Assert.Equal(1,Counter(h,"luanwu"));h.Conserved();
    }
    [Fact] public void JianshuMakesWinnerDiscardAndLoserCannotDrawWhileSelectionRemainsPrivate()
    {
        var h=ThreeKingdomsB5CivilTestHost.Create("tk-g11");h.Inner.Give("P2",1,0,0,0,1);h.Inner.Give("P3",1);
        Do(h,"P1","civil-jianshu");Drain(h);Assert.Equal(2,h.State.CivilB5.Jianshu.Length);
        h.State.Clock.ActivePlayerId="P2";h.State.Clock.Phase=ThreeKingdomsPhase.Draw;h.State.Clock.TurnInstanceId++;
        B2SkillEngine.BeginHarvest(h,new[]{new B2HarvestAllocation {PlayerId="P2",Cards=new[]{1,0,0,0,0,0,0,0}}});
        Do(h,"P2","civil-select",4);Do(h,"P2","civil-selected");Do(h,"P2","civil-stone-declare",1);
        var other=new ThreeKingdomsView();B2SkillEngine.Project(h,other,"P1");Assert.Empty(other.SkillActions);Assert.All(other.RevealedSkillCards,n=>Assert.Equal(0,n));
        var restored=h.Copy();Do(h,"P3","civil-true");Do(restored,"P3","civil-true");Drain(h);Drain(restored);
        Assert.Equal(restored.Snapshot(),h.Snapshot());Assert.True(B2SkillEngine.IsDrawBlocked(h.State,"P2"));Assert.Equal(0,h.GetHand("P3").Sum());Assert.Equal(2,h.GetHand("P2").Sum());Assert.Single(h.State.CivilB5.Jianshu);h.Conserved();
    }
    [Fact] public void WeimuIsPassiveWhileActiveAndOptionalOneUseAfterRetirement()
    {
        var h=ThreeKingdomsB5CivilTestHost.Create("tk-g11");Assert.True(B2SkillEngine.CivilBlueImmune(h.State,"P1","use-a"));
        var g=h.State.Generals[0];B2SkillEngine.OnCivilGeneralRetired(h.State,g);g.Region=GeneralRegion.Retired;
        B2SkillEngine.OnCivilProgressTargeted(h,"use-b","P2","P1",1);B2SkillEngine.Pump(h);Do(h,"P1","skip");Assert.False(B2SkillEngine.CivilBlueImmune(h.State,"P1","use-b"));
        B2SkillEngine.OnCivilProgressTargeted(h,"use-c","P2","P1",1);B2SkillEngine.Pump(h);Do(h,"P1","accept");Drain(h);
        Assert.True(B2SkillEngine.CivilBlueImmune(h.State,"P1","use-c"));Assert.False(B2SkillEngine.CivilBlueImmune(h.State,"P1","use-d"));
    }
    [Fact] public void DongzhuoOwnDrawDoesNotCancelOtherPlayersProduction()
    {
        var h=ThreeKingdomsB5CivilTestHost.Create("tk-g25");h.State.Clock.Phase=ThreeKingdomsPhase.Draw;
        B2SkillEngine.BeginHarvest(h,new[]{new B2HarvestAllocation {PlayerId="P1",Cards=new[]{2,0,0,0,0,0,0,0}},new B2HarvestAllocation {PlayerId="P2",Cards=new[]{2,0,0,0,0,0,0,0}}});Drain(h);
        Assert.Equal(0,h.GetHand("P1").Sum());Assert.Equal(2,h.GetHand("P2").Sum());h.Conserved();
    }
    [Fact] public void HengzhengReplacesWholeAllocationThenTakesOthersOnce()
    {
        var h=ThreeKingdomsB5CivilTestHost.Create("tk-g25");h.Inner.Give("P2",1);h.Inner.Give("P3",1);h.State.Clock.ActivePlayerId="P2";h.State.Clock.Phase=ThreeKingdomsPhase.Draw;
        B2SkillEngine.BeginHarvest(h,new[]{new B2HarvestAllocation {PlayerId="P1",Cards=new[]{3,0,0,0,0,0,0,0}}});Do(h,"P1","accept");Drain(h);
        Assert.Equal(2,h.GetHand("P1").Sum());Assert.Equal(0,h.GetHand("P2").Sum());Assert.Equal(0,h.GetHand("P3").Sum());h.Conserved();
    }
    [Fact] public void BenghuaiAllowsOwnRoadWhenNotUniqueTopScore()
    {
        var h=ThreeKingdomsB5CivilTestHost.Create("tk-g25");h.AddOption("damage-road","own","P1");B2SkillEngine.OnCivilEnd(h,"end");B2SkillEngine.Pump(h);Do(h,"P1","accept");Do(h,"P1","civil-road");Drain(h);Assert.Equal("P1",h.Applied.Single().OtherPlayerId);
    }
    [Fact] public void JieyinSelectsOtherPlayerCopiesConstructionAndDropsAtRetirement()
    {
        var h=ThreeKingdomsB5CivilTestHost.Create("tk-g27");Entry(h);Do(h,"P1","civil-relation");Assert.Equal("P2",h.State.CivilB5.Relations.Single().TargetPlayerId);
        h.AddOption("free-City","city","P1",asset:"City");B2SkillEngine.OnCivilBuilt(h,"build-1","P2","City",2);B2SkillEngine.Pump(h);Do(h,"P1","accept");Do(h,"P1","civil-copy");Drain(h);Assert.Single(h.Applied);
        B2SkillEngine.OnCivilGeneralRetired(h.State,h.State.Generals[0]);Assert.Empty(h.State.CivilB5.Relations);
    }
    [Fact] public void JieyinDiscardIsBatchBasedDiscardsOnlyAvailableAndXiaojiOncePerTurn()
    {
        var h=ThreeKingdomsB5CivilTestHost.Create("tk-g27");Entry(h);Do(h,"P1","civil-relation");h.Inner.Give("P1",1,1);
        B2SkillEngine.OnCardsMoved(h,"partner-loss",new[]{new B2HandChange {PlayerId="P2",BeforeCount=5,AfterCount=0,LostCount=5}});Drain(h);Assert.Equal(0,h.GetHand("P1").Sum());
        B2SkillEngine.OnCardsMoved(h,"progress-1",new[]{new B2HandChange {PlayerId="P1",LostProgressCount=2}});Drain(h);Assert.Equal(3,h.GetHand("P1").Sum());
        B2SkillEngine.OnCardsMoved(h,"progress-2",new[]{new B2HandChange {PlayerId="P1",LostProgressCount=1}});Drain(h);Assert.Equal(3,h.GetHand("P1").Sum());h.Conserved();
    }
    [Fact] public void FenchengRequiresIncreasingFullCostAndResetsAfterNoAssets()
    {
        var h=ThreeKingdomsB5CivilTestHost.Create("tk-g31");h.Inner.Give("P1",2);h.Inner.Give("P2",1);h.Inner.Give("P3",1);
        Do(h,"P1","civil-fencheng");Do(h,"P1","civil-fencheng-hand");Do(h,"P1","civil-select",0);Do(h,"P1","civil-fencheng-commit");
        Assert.DoesNotContain(B2SkillEngine.Actions(h,"P2"),a=>a.Command.Option=="civil-fencheng-hand");Do(h,"P2","civil-fencheng-burn");
        Assert.Contains(B2SkillEngine.Actions(h,"P3"),a=>a.Command.Option=="civil-fencheng-hand");Do(h,"P3","civil-fencheng-hand");Do(h,"P3","civil-select",0);Do(h,"P3","civil-fencheng-commit");Drain(h);
        Assert.Equal(1,h.GetHand("P1").Sum());Assert.Equal(1,h.GetHand("P2").Sum());Assert.Equal(0,h.GetHand("P3").Sum());h.Conserved();
    }
    [Fact] public void JueceIsOwnEndOnlyAndTargetSelectsUnusedProgress()
    {
        var h=ThreeKingdomsB5CivilTestHost.Create("tk-g31");h.DrawProgress("P2",0);h.State.Clock.ActivePlayerId="P3";B2SkillEngine.OnCivilEnd(h,"end-other");Assert.Empty(h.State.Skills.Frames);
        h.State.Clock.ActivePlayerId="P1";B2SkillEngine.OnCivilEnd(h,"end-own");B2SkillEngine.Pump(h);Do(h,"P1","accept");Assert.Equal("P2",h.State.Skills.PendingDecision.PlayerId);Do(h,"P2","civil-juece-progress");Drain(h);Assert.Empty(h.GetProgressCards("P2"));
    }
    [Fact] public void PojunUsesSharedBudgetEffectiveLevelsAndRestoresOnlyTargetPlay()
    {
        var h=ThreeKingdomsB5CivilTestHost.Create("tk-g32");h.Technology["P1"]=new[]{3,3,3};h.Technology["P2"]=new[]{4,3,3};h.Inner.Give("P2",2);
        B2SkillEngine.OnCivilProgressTargeted(h,"use","P1","P2",0);B2SkillEngine.Pump(h);Do(h,"P1","accept");Do(h,"P1","civil-pojun-track",0);Do(h,"P1","civil-pojun-done");Drain(h);
        Assert.Equal(3,h.GetImprovements("P2")[0]);Assert.Equal(4,h.GetActualImprovements("P2")[0]);Assert.Equal(2,B2SkillEngine.CivilProgressMultiplier(h,"P1","P2"));
        B2SkillEngine.OnCivilPlayStart(h,"start-p3","P3");Assert.Equal(3,h.GetImprovements("P2")[0]);B2SkillEngine.OnCivilPlayStart(h,"start-p2","P2");Assert.Equal(4,h.GetImprovements("P2")[0]);Assert.Equal(1,B2SkillEngine.CivilProgressMultiplier(h,"P1","P2"));h.Conserved();
    }
    [Fact] public void YichengPaysTwoActualCardsForOneVirtualBishopAndConsumesLimitedUse()
    {
        var h=ThreeKingdomsB5CivilTestHost.Create("tk-g32");h.Inner.Give("P1",1,1);h.VirtualUses.Add(new B4VirtualUse {CardId="Taxation",OptionId="bishop",Label="主教"});
        Do(h,"P1","civil-yicheng");Do(h,"P1","civil-select",0);Do(h,"P1","civil-select",1);var saved=h.Copy();Do(h,"P1","civil-yicheng-use");Do(saved,"P1","civil-yicheng-use");Drain(h);Drain(saved);
        Assert.Equal(saved.Snapshot(),h.Snapshot());Assert.Single(h.Used);Assert.Equal(0,h.GetHand("P1").Sum());Assert.Equal(1,Counter(h,"yicheng"));h.Conserved();
    }
    [Fact] public void LianhuanCannotUseZeroOrForeignGeneralAndReturnsRealOreToBank()
    {
        var h=ThreeKingdomsB5CivilTestHost.Create("tk-g35");h.Inner.Give("P1",0,0,0,0,2);var a=B2SkillEngine.Actions(h,"P1").Where(a=>a.Command.Option=="civil-lianhuan").ToArray();Assert.Equal(2,a.Length);Assert.All(a,x=>Assert.True(x.Command.Counts[4]>0));
        B2SkillEngine.Resolve(h,"P1",a[1].Command);Drain(h);Assert.Equal(2,h.GetHand("P1").Sum());h.Conserved();
    }
    [Fact] public void NiepanDropsTwoDistinctTopLevelsAllCardsAndGainsPermanentPoint()
    {
        var h=ThreeKingdomsB5CivilTestHost.Create("tk-g35");h.Technology["P1"]=new[]{4,2,0};h.Inner.Give("P1",1,1);h.DrawProgress("P1",2);
        B2SkillEngine.OnCivilPlayStart(h,"play","P1");B2SkillEngine.Pump(h);Do(h,"P1","accept");Do(h,"P1","civil-niepan-track",0);Do(h,"P1","civil-niepan-track",1);Drain(h);
        Assert.Equal(new[]{3,1,0},h.GetImprovements("P1"));Assert.Equal(0,h.GetHand("P1").Sum());Assert.Empty(h.GetProgressCards("P1"));Assert.Equal(3,h.GetScore("P1"));Assert.Equal(1,Counter(h,"niepan"));h.Conserved();
    }
    [Fact] public void XianfuPaysResultingLevelBudgetAndChouceUsesPartnerTwoRandomCards()
    {
        var h=ThreeKingdomsB5CivilTestHost.Create("tk-g36");Entry(h);Do(h,"P1","civil-relation");h.Inner.Give("P2",3);
        h.AddOption("free-military","knight2","P1",level:2,asset:"Knight");h.AddOption("free-military","tech1","P1",level:1,asset:"Improvement");
        B2SkillEngine.OnCivilBuilt(h,"build","P2","Knight",3);B2SkillEngine.Pump(h);Do(h,"P1","accept");Do(h,"P1","civil-copy",id:"knight2");Do(h,"P1","civil-copy",id:"tech1");Drain(h);Assert.Equal(2,h.Applied.Count);
        B2SkillEngine.OnCivilTargeted(h,"target","P3","P1","skill");B2SkillEngine.Pump(h);Do(h,"P1","civil-chouce");Drain(h);Assert.Equal(1,h.GetHand("P2").Sum());h.Conserved();
    }
    [Fact] public void LiegongTracksEffectiveKindsAcrossTurnsAndRequiresExactlyTwoRoads()
    {
        var h=ThreeKingdomsB5CivilTestHost.Create("tk-g51");B2SkillEngine.OnCivilPaid(h,"payment","P1",Enumerable.Range(0,6).ToArray());h.State.Clock.TurnInstanceId++;B2SkillEngine.OnCivilPaid(h,"progress","P1",Array.Empty<int>(),"Taxation");
        h.AddOption("liegong-road","r1");Assert.DoesNotContain(B2SkillEngine.Actions(h,"P1"),a=>a.Command.Option=="civil-liegong-roads");h.AddOption("liegong-road","r2","P3");
        Do(h,"P1","civil-liegong-roads");Do(h,"P1","civil-pick-road",id:"r1");var saved=h.Copy();Do(h,"P1","civil-pick-road",id:"r2");Do(saved,"P1","civil-pick-road",id:"r2");Drain(h);Drain(saved);
        Assert.Equal(saved.Snapshot(),h.Snapshot());Assert.Equal(2,h.Applied.Count);Assert.DoesNotContain(h.State.Generals[0].Counters,c=>c.Key.StartsWith("liegong:"));
    }
    [Fact] public void QingnangMatchingCostRefundsOpportunityOwnRepairDrawsAndNonmatchEndsPhaseUse()
    {
        var h=ThreeKingdomsB5CivilTestHost.Create("tk-g54");h.Inner.Give("P1",2,1);h.AddOption("repair","own","P1",asset:"Road",cost:new[]{1,1});h.AddOption("repair","other","P2",asset:"City",cost:new[]{0,0,0,2,3});
        Do(h,"P1","civil-qingnang",0,"own");Do(h,"P1","card",7);Drain(h);Assert.Equal(0,Counter(h,"qingnang"));Assert.Equal(1,h.GetHand("P1")[7]);
        Do(h,"P1","civil-qingnang",0,"other");Drain(h);Assert.Equal(1,Counter(h,"qingnang"));Assert.Equal(2,h.Applied.Count);h.Conserved();
    }
    [Fact] public void ChujiaSurvivesRetirementAndDoesNotRequireDamage()
    {
        var h=ThreeKingdomsB5CivilTestHost.Create("tk-g54");Do(h,"P1","civil-chujia");Drain(h);B2SkillEngine.OnCivilGeneralRetired(h.State,h.State.Generals[0]);h.State.Generals[0].Region=GeneralRegion.Retired;Assert.Contains("P1",h.Protected);
    }
    [Theory][InlineData("tk-g03")][InlineData("tk-g11")][InlineData("tk-g16")][InlineData("tk-g17")][InlineData("tk-g25")][InlineData("tk-g27")][InlineData("tk-g31")][InlineData("tk-g32")][InlineData("tk-g35")][InlineData("tk-g36")][InlineData("tk-g51")][InlineData("tk-g54")]
    public void CivilActionsAreAbsentOutsideOwnPlayAndFromOldB4(string general)
    {
        var h=ThreeKingdomsB5CivilTestHost.Create(general);h.Inner.Give("P1",3,6,3,3,3,3,3,3);h.State.Clock.ActivePlayerId="P2";Assert.DoesNotContain(B2SkillEngine.Actions(h,"P1"),a=>a.Command.Option.StartsWith("civil-"));
        h.State.Clock.ActivePlayerId="P1";h.State.RulesVersion=GeneralCatalog.B4RulesVersion;Assert.DoesNotContain(B2SkillEngine.Actions(h,"P1"),a=>a.Command.Option.StartsWith("civil-"));
    }
}
