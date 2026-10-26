using Catan.Core.ThreeKingdoms;
using Xunit;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB45SelectionTests
{
    static int[] Bag(params (int type,int count)[] entries){var bag=new int[8];foreach(var e in entries)bag[e.type]=e.count;return bag;}
    static B2SkillAction Action(string option,string skill="tk-g15-s1",string? target=null,int type=-1,int count=1) => new() {Label="不含目标或费用的任意展示文字",Command=new() {SkillId=skill,Option=option,WindowId="w",TargetPlayerIds=target==null?Array.Empty<string>():new[]{target},Counts=type<0?new int[8]:Bag((type,count))}};
    static ThreeKingdomsView View(params B2SkillAction[] actions)=>new(){PackageVersion=GeneralCatalog.B45PackageVersion,WindowId="w",Clock=new(){ActivePlayerId="p1",Phase=ThreeKingdomsPhase.Play},SkillActions=actions};
    static B45SelectionContext Start(ThreeKingdomsView view,int[] hand,string? skill=null){var context=new B45SelectionContext();context.Begin(view,"p1",skill??view.SkillActions[0].Command.SkillId,hand);return context;}

    [Fact] public void CatalogCoversThirtyGeneralsAndExactlyFiftyOneSkills()
    {Assert.Equal(30,GeneralCatalog.B4Definitions.Length);Assert.Equal(51,B45SkillCatalog.All.Length);Assert.Equal(GeneralCatalog.B4Definitions.SelectMany(g=>g.Skills).Select(s=>s.Id).OrderBy(x=>x),B45SkillCatalog.All.Select(s=>s.SkillId).OrderBy(x=>x));Assert.All(B45SkillCatalog.All,s=>{Assert.NotEmpty(s.Timing);Assert.NotEmpty(s.Cost);Assert.NotEmpty(s.Limit);Assert.NotEmpty(s.CommitBoundary);});}
    [Fact] public void RendeSelectsMixedCardsThenPortraitInOnePureDraft()
    {var hand=Bag((0,3),(5,2));var view=View(Action("gift",target:"p2",type:0),Action("gift",target:"p2",type:5),Action("gift",target:"p3",type:0),Action("gift",target:"p3",type:5));var c=Start(view,hand);Assert.Single(c.Options);Assert.True(c.ToggleCard(0,2));Assert.True(c.ToggleCard(5,1));Assert.False(c.CanConfirm);Assert.True(c.TogglePlayer("p2"));var command=c.BuildCommand();Assert.NotNull(command);Assert.Equal(Bag((0,1),(5,1)),command.Counts);Assert.Equal(new[]{"p2"},command.TargetPlayerIds);Assert.Equal(Bag((0,3),(5,2)),hand);Assert.Equal(1,view.SkillActions[0].Command.Counts[0]);}
    [Fact] public void PortraitFirstAndReplacementRetainValidSelectedCards()
    {var c=Start(View(Action("gift",target:"p2",type:0),Action("gift",target:"p3",type:0)),Bag((0,3)));Assert.True(c.TogglePlayer("p2"));Assert.True(c.ToggleCard(0,2));Assert.True(c.TogglePlayer("p3"));Assert.Equal(new[]{"p3"},c.SelectedPlayers);Assert.True(c.IsHandSelected(0,2));Assert.True(c.CanConfirm);}
    [Fact] public void DisplaySortDoesNotMoveSelectionToAnotherCopy()
    {var view=View(Action("gift",target:"p2",type:0));var c=Start(view,Bag((0,4)));c.ToggleCard(0,2);c.Refresh(view,Bag((0,4)));Assert.True(c.IsHandSelected(0,2));Assert.False(c.IsHandSelected(0,0));c.Refresh(view,Bag((0,2)));Assert.Empty(c.SelectedCounts.Where(n=>n!=0));}
    [Fact] public void ZhihengAcceptsFourRealCardsAndRejectsFifth()
    {var c=Start(View(Action("exchange-hand","tk-g22-s1",type:0),Action("exchange-hand","tk-g22-s1",type:5)),Bag((0,3),(5,2)));c.ToggleCard(0,0);c.ToggleCard(0,2);c.ToggleCard(5,0);c.ToggleCard(5,1);Assert.False(c.ToggleCard(0,1));Assert.True(c.CanConfirm);Assert.Equal(4,c.BuildCommand().Counts.Sum());}
    [Fact] public void GuoseRequiresExactlyTwoBricksAndLegalPortrait()
    {var c=Start(View(Action("response-guose","tk-g05-s2","p2",1,2)),Bag((1,3),(0,4)));Assert.False(c.ToggleCard(0,0));Assert.False(c.TogglePlayer("p1"));c.ToggleCard(1,1);c.TogglePlayer("p2");Assert.False(c.CanConfirm);c.ToggleCard(1,2);Assert.True(c.CanConfirm);Assert.False(c.ToggleCard(1,0));}
    [Fact] public void BoardTargetsUseStableIdsAndIgnoreLabel()
    {var a=Action("response-liuli","tk-g05-s1",type:3);a.Command.ProgressCardId="opaque";a.SelectionBoardIds=new[]{"hex-17"};var c=Start(View(a),Bag((3,1)));Assert.False(c.ToggleTile("hex-18"));Assert.True(c.ToggleTile("hex-17"));Assert.True(c.ToggleCard(3,0));Assert.Equal("opaque",c.BuildCommand().ProgressCardId);}
    [Fact] public void MultiTargetPortraitsDoNotRequireOneButtonPerCombination()
    {var first=Action("fangzhu","tk-g29-s1");first.Command.TargetPlayerIds=new[]{"p1","p2"};var second=Action("fangzhu","tk-g29-s1");second.Command.TargetPlayerIds=new[]{"p2","p3"};var c=Start(View(first,second),new int[8]);Assert.Single(c.Options);c.TogglePlayer("p2");Assert.False(c.CanConfirm);c.TogglePlayer("p3");Assert.True(c.CanConfirm);Assert.False(c.TogglePlayer("p1"));c.TogglePlayer("p3");c.TogglePlayer("p1");Assert.Equal(new[]{"p1","p2"},c.BuildCommand().TargetPlayerIds);}
    [Fact] public void SubmitGuardPreventsDuplicateConfirmationAndReturnsDetachedCommand()
    {var c=Start(View(Action("b4-jieming","tk-g39-s2")),new int[8]);Assert.True(c.CanConfirm);var command=c.BeginSubmit();Assert.NotNull(command);Assert.False(c.CanConfirm);Assert.Null(c.BeginSubmit());Assert.False(c.TogglePlayer("p2"));c.AcknowledgeFailure();Assert.True(c.CanConfirm);command.Option="changed";Assert.Equal("b4-jieming",c.BuildCommand().Option);}
    [Fact] public void StaleDecisionClearsDraftAndAuthorizedPrivateChoices()
    {var a=Action("response-poxi-select","tk-g08-s2","p2");a.Command.CardType=4;var v=View(a);v.SkillDecision=new(){PlayerId="p1",Kind="response-poxi-select",DecisionId="d"};var c=Start(v,new int[8]);Assert.Single(c.PrivateOptions);c.TogglePrivateCard(4);v.WindowId="new";c.Refresh(v,new int[8]);Assert.False(c.Active);Assert.Empty(c.PrivateOptions);Assert.Equal(0,c.SelectedPrivateCounts.Sum());}
    [Fact] public void OtherPlayersPendingDecisionCannotExposeCandidateDraft()
    {var v=View(Action("gift",target:"p3",type:0));v.SkillDecision=new(){PlayerId="p2",DecisionId="private"};var c=Start(v,Bag((0,2)));Assert.False(c.Active);Assert.Empty(c.Options);}
    [Fact] public void MandatoryResponseReselectNeverProducesSkip()
    {var a=Action("b4-give","tk-g45-s1");a.Command.CardType=0;var v=View(a);v.SkillDecision=new(){PlayerId="p1",Kind="b4-give",DecisionId="d"};var c=Start(v,Bag((0,1)));c.ToggleCard(0,0);Assert.True(c.CanConfirm);Assert.False(c.CanCancel);Assert.Null(c.SkipCommand);c.Reselect();Assert.True(c.Active);Assert.False(c.CanConfirm);}
    [Fact] public void PureSelectionDoesNotGenerateCommandsForPassiveButtons()
    {Assert.False(B45SkillCatalog.Get("tk-g42-s1").HasActiveEntry);Assert.False(B45SkillCatalog.Get("tk-g44-s2").HasActiveEntry);Assert.False(B45SkillCatalog.Get("tk-g13-s2").HasActiveEntry);Assert.True(B45SkillCatalog.Get("tk-g15-s1").HasActiveEntry);}
    [Fact] public void ProgressCopyIdentityRemainsTheClickedOrdinal()
    {var a=Action("exchange-progress","tk-g22-s1");a.Command.ProgressCardId="Invention";var c=Start(View(a),new int[8]);Assert.True(c.ToggleProgress("Invention",2));Assert.True(c.IsProgressSelected("Invention",2));Assert.False(c.IsProgressSelected("Invention",0));Assert.Equal("Invention",c.BuildCommand().ProgressCardId);Assert.False(c.ToggleProgress("Invention",0));}
    [Fact] public void XiemuTwoSameKindCopiesRemainDistinctInDraft()
    {var a=Action("b4-xiemu","tk-g52-s2");a.Command.ProgressCardIds=new[]{"Merchant","Merchant"};a.Command.ProgressCardId="virtual-monopoly";a.VirtualCardId="ResourceMonopoly";var c=Start(View(a),new int[8]);Assert.True(c.ToggleProgress("Merchant",1));Assert.True(c.ToggleProgress("Merchant",3));Assert.False(c.ToggleProgress("Merchant",0));Assert.True(c.CanConfirm);Assert.Equal(new[]{"Merchant","Merchant"},c.BuildCommand().ProgressCardIds);}
    [Fact] public void PoxiKeepsOwnAndAuthorizedCardsSeparateAndSubmitsOnce()
    {var own=Action("response-poxi-select","tk-g08-s2","p1");own.Command.CardType=0;var others=Enumerable.Range(1,3).Select(t=>{var a=Action("response-poxi-select","tk-g08-s2","p2");a.Command.CardType=t;return a;});var v=View(new[]{own}.Concat(others).ToArray());v.SkillDecision=new(){PlayerId="p1",Kind="response-poxi-select",DecisionId="d"};var c=Start(v,Bag((0,2)));c.ToggleCard(0,1);Assert.False(c.TogglePrivateCard(0));foreach(int t in Enumerable.Range(1,3))Assert.True(c.TogglePrivateCard(t));var command=c.BuildCommand();Assert.Equal("b45-poxi-confirm",command.Option);Assert.Equal(Bag((0,1)),command.Counts);Assert.Equal(Bag((1,1),(2,1),(3,1)),command.SecondaryCounts);}
    [Fact] public void SubsetCanChooseZeroForXiashuButQuhuRequiresOne()
    {var a=Action("b4-select","tk-g26-s1");var v=View(a);v.SkillDecision=new(){PlayerId="p1",Kind="b4-reveal",DecisionId="d"};var c=Start(v,Bag((0,2)));Assert.Equal("b45-selected",c.BuildCommand().Option);v.SkillDecision.Kind="b4-subset";c=Start(v,Bag((0,2)));Assert.False(c.CanConfirm);c.ToggleCard(0,1);Assert.True(c.CanConfirm);}
    [Fact] public void LegacySubsetKeepsOriginalCommandAndDoesNotUpgradeHistory()
    {var a=Action("b4-selected","tk-g26-s1");var v=View(a);v.PackageVersion=GeneralCatalog.B4PackageVersion;v.SkillDecision=new(){PlayerId="p1",Kind="b4-reveal",DecisionId="d"};var c=Start(v,Bag((0,2)));Assert.Equal("b4-selected",c.BuildCommand().Option);}
    [Fact] public void BankBatchUsesBankSupplyAndCannotSpendOwnedCards()
    {var actions=new[]{0,2}.Select(t=>{var a=Action("b45-optional-cards","tk-g06-s1");a.Command.CardType=t;a.RequiredCardCount=4;a.AvailableCount=t==0?1:3;return a;}).ToArray();var c=Start(View(actions),Bag((1,8)));Assert.True(c.RequiresBankCards);Assert.False(c.ToggleCard(1,0));Assert.True(c.ToggleBankCard(0));Assert.False(c.ToggleBankCard(0));Assert.True(c.ToggleBankCard(2));Assert.True(c.ToggleBankCard(2));Assert.False(c.CanConfirm);Assert.True(c.ToggleBankCard(2));Assert.Equal(Bag((0,1),(2,3)),c.BuildCommand().Counts);}
    [Fact] public void ChangingOptionDoesNotInventUnselectedPlayerOrCard()
    {var a=Action("b4-daoshu","tk-g40-s1","p2");a.Command.CardType=0;var b=Action("b4-daoshu","tk-g40-s1","p3");b.Command.CardType=1;var c=Start(View(a,b),new int[8]);Assert.False(c.CanConfirm);c.ChooseOption(a);c.TogglePlayer("p2");Assert.True(c.CanConfirm);c.ChooseOption(b);Assert.Empty(c.SelectedPlayers);Assert.False(c.CanConfirm);}

    [Fact] public void PublicLibrariesOnlyContainPoolAndRetiredGeneralsAndNeverHiddenCandidates()
    {
        var state=ThreeKingdomsEngine.Create(new[]{"P1","P2","P3"},97,_=>0,true,GeneralCatalog.B45PackageVersion);
        var pools=state.Generals.Where(g=>g.Region==GeneralRegion.Pool).ToArray();
        pools[0].Region=GeneralRegion.Retired;pools[1].Region=GeneralRegion.Hidden;pools[1].OwnerPlayerId="P2";
        foreach(var viewer in state.SeatPlayerIds)
        {
            var view=ThreeKingdomsEngine.Project(state,viewer);
            Assert.Equal(state.Generals.Where(g=>g.Region==GeneralRegion.Pool).Select(g=>g.GeneralId).OrderBy(x=>x),view.PublicPoolGeneralIds);
            Assert.Equal(new[]{pools[0].GeneralId},view.RetiredGeneralIds);
            Assert.DoesNotContain(pools[1].GeneralId,view.PublicPoolGeneralIds.Concat(view.RetiredGeneralIds));
            Assert.All(state.Generals.Where(g=>g.Region==GeneralRegion.Candidate),g=>Assert.DoesNotContain(g.GeneralId,view.PublicPoolGeneralIds));
        }
        state.RulesVersion=GeneralCatalog.B4RulesVersion;state.PackageVersion=GeneralCatalog.B4PackageVersion;
        Assert.Null(ThreeKingdomsEngine.Project(state,"P1").PublicPoolGeneralIds);
    }
    [Fact] public void DiceProjectionCarriesOnlyAuthorizedCandidateValuesWithoutRolling()
    {
        var host=ThreeKingdomsB2TestHost.Create("tk-g19");host.State.PackageVersion=GeneralCatalog.B45PackageVersion;
        host.State.Dice=new(){Roll=new(){ChoiceCursor=1,Candidates=new[]{new B3DiceCandidate{Index=0,Red=3,White=6,EventFace=4},new B3DiceCandidate{Index=1,Red=2,White=1,EventFace=5}}},PendingDecision=new(){PlayerId="P1"}};
        var a=Action("select","tk-g19-s1");a.Command.CardType=1;
        B45SelectionMetadata.AnnotateDice(host,new[]{a});Assert.Equal(1,a.SelectionDiceIndex);Assert.Equal(1,a.SelectionDiceValue);Assert.Equal(0,host.RandomCalls);
        Assert.Null(a.SelectionPlayerIds);Assert.Null(a.SelectionBoardIds);
    }
    [Fact] public void DiceChoiceCannotConfirmUntilDiceAndExactCandidateAreChosen()
    {
        var a=Action("modify","tk-g13-s2");a.Command.CardType=0;a.Command.TileIds=new[]{"t1"};a.Command.Counts[0]=0;a.SelectionDiceIndex=0;a.SelectionDiceValue=5;
        var b=Action("modify","tk-g13-s2");b.Command.CardType=0;b.Command.TileIds=new[]{"t1"};b.Command.Counts[0]=1;b.SelectionDiceIndex=1;b.SelectionDiceValue=5;
        var c=Start(View(a,b),Bag((0,1)));c.ToggleCard(0,0);c.ToggleTile("t1");Assert.False(c.CanConfirm);
        Assert.True(c.SelectDie(1));c.ToggleTile("t1");Assert.True(c.CanConfirm);Assert.Equal(1,c.BuildCommand().Counts[0]);
    }

    [Fact] public void MissingProgressOrdinalIsClearedInsteadOfReassigned()
    {
        var action=Action("exchange-progress","tk-g22-s1");action.Command.ProgressCardId="Invention";var c=Start(View(action),new int[8]);
        c.ToggleProgress("Invention",2);c.RevalidateProgressCopies(new[]{"Invention","Invention","Invention"});Assert.True(c.CanConfirm);
        c.RevalidateProgressCopies(new[]{"Invention","Invention"});Assert.Empty(c.SelectedProgress);Assert.False(c.IsProgressSelected("Invention",0));Assert.False(c.CanConfirm);Assert.NotNull(c.Notice);
    }

    [Fact] public void LuoyingKeepsOneAndTwoWoodAsSeparateChoices()
    {
        var c=Start(View(Action("luoying","tk-g30-s1",type:0,count:1),Action("luoying","tk-g30-s1",type:0,count:2)),new int[8]);
        Assert.Equal(2,c.Options.Length);Assert.False(c.CanConfirm);c.ChooseOption(c.Options.Single(a=>a.Command.Counts[0]==2));Assert.Equal(2,c.BuildCommand().Counts[0]);
    }
}
