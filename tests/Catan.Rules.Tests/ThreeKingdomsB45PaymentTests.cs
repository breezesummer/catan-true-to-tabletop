using Catan.Core;
using Catan.Core.ThreeKingdoms;
using Xunit;
using M4=Catan.Core.M4;
using M5=Catan.Core.M5;
namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB45PaymentTests
{
    static int[] Bag(params (int type,int count)[] values){var result=new int[8];foreach(var (t,n) in values)result[t]=n;return result;}
    static ThreeKingdomsView View(string general="tk-g42")=>new(){PackageVersion=GeneralCatalog.B45PackageVersion,WindowId="w",Players=new[]{new ThreeKingdomsPlayerView{PlayerId="P1",ActiveGeneralId=general}},EffectiveCardTypes=Enumerable.Range(0,8).Select(t=>t==0||t==1?new[]{0,1}:new[]{t}).ToArray()};
    static B45PaymentContext Start(ThreeKingdomsView view,int[] hand,int count,int[]? cost=null){var c=new B45PaymentContext();c.Begin(view,"P1",hand,count,cost,"测试支付");return c;}
    [Fact] public void RealCopiesAndEveryUseRequireExplicitSelection()
    {var c=Start(View(),Bag((1,3)),2,Bag((0,1),(1,1)));c.ToggleCard(1,2);Assert.Equal(-1,c.Current.Use);Assert.False(c.Ready);c.ChooseUse(0);c.ToggleCard(1,0);c.ChooseUse(1);Assert.True(c.Ready);Assert.True(c.IsSelected(1,2));Assert.False(c.IsSelected(1,1));Assert.False(c.CanSelect(1,1));}
    [Fact] public void PaymentDraftCannotSubstituteAnUnownedOrDisappearedCopy()
    {var v=View();var c=Start(v,Bag((1,3)),2);Assert.False(c.CanSelect(0,0));c.ToggleCard(1,2);c.ChooseUse(0);c.Refresh(v,Bag((1,2)));Assert.Empty(c.Cards);Assert.False(c.IsSelected(1,0));Assert.NotNull(c.Notice);}
    [Fact] public void UsesCannotEscapeAuthorizedConversionOrRequiredCost()
    {var c=Start(View(),Bag((1,2)),2,Bag((0,1),(1,1)));c.ToggleCard(1,0);c.ChooseUse(4);Assert.Equal(-1,c.Current.Use);c.ChooseUse(0);c.ToggleCard(1,1);c.ChooseUse(0);Assert.False(c.Ready);c.ChooseUse(1);Assert.True(c.Ready);}
    [Fact] public void GuhuoIsExplicitAndChangingModeNeverKeepsAnImplicitClaim()
    {var c=Start(View("tk-g07"),Bag((0,2)),2,Bag((6,2)));c.ToggleCard(0,0);c.ChooseUse(0);Assert.False(c.Declaring);c.SetDeclaring(true);Assert.Equal(-1,c.Current.Use);c.ChooseUse(6);c.ToggleCard(0,1);Assert.False(c.Ready);c.ChooseUse(6);Assert.True(c.Ready);Assert.Equal(new[]{0,0},c.RealTypes);Assert.Equal(new[]{6,6},c.EffectiveTypes);c.SetDeclaring(false);Assert.All(c.Cards,card=>Assert.Equal(-1,card.Use));}
    [Fact] public void MiewuCannotAllocateMoreThanOneConversionInOneBatch()
    {var c=Start(View("tk-g41"),Bag((1,2)),2,Bag((0,2)));c.ToggleCard(1,0);c.ChooseUse(0);c.ToggleCard(1,1);c.ChooseUse(0);Assert.False(c.Ready);}
    [Fact] public void OldPackageNeverStartsNewOrdinaryPaymentFlow()
    {var v=View();v.PackageVersion=GeneralCatalog.B4PackageVersion;Assert.False(Start(v,Bag((1,2)),2).Active);}
    [Fact] public void StaleWindowClearsPrivateOrdinalsAndDeclaredUses()
    {var v=View("tk-g07");var c=Start(v,Bag((0,2)),2);c.SetDeclaring(true);c.ToggleCard(0,1);c.ChooseUse(7);v.WindowId="new";c.Refresh(v,Bag((0,2)));Assert.False(c.Active);Assert.Empty(c.RealTypes);Assert.Empty(c.EffectiveTypes);}
    [Fact] public void SubmitLockAndCancellationNeverMutateViewOrHand()
    {var v=View();var hand=Bag((1,1));var c=Start(v,hand,1,Bag((0,1)));c.ToggleCard(1,0);c.ChooseUse(0);Assert.True(c.Lock());Assert.False(c.Lock());c.ToggleCard(1,0);Assert.Single(c.Cards);c.Unlock("重试");c.ClearSelections();Assert.Empty(c.Cards);Assert.Equal(1,hand[1]);Assert.Equal(new[]{0,1},v.EffectiveCardTypes[1]);}

    [Fact] public void SameWindowSeatChangeClearsPrivatePaymentAndNeverRebindsCards()
    {var v=View("tk-g07");var c=Start(v,Bag((0,2)),2);c.SetDeclaring(true);c.ToggleCard(0,1);c.ChooseUse(7);c.Refresh(v,"P2",Bag((0,2)));Assert.False(c.Active);Assert.Empty(c.Cards);Assert.Empty(c.EffectiveTypes);Assert.NotNull(c.Notice);}
    [Fact] public void SpecificPortConstrainsUseAndRatioWithoutConstrainingRealCards()
    {var c=Start(View(),Bag((0,2)),2);c.ConfigureBankTrade(2,1,1);c.ToggleCard(0,1);Assert.Equal(new[]{1},c.AllowedUses);c.ChooseUse(0);Assert.Equal(-1,c.Current.Use);c.ChooseUse(1);c.ToggleCard(0,0);c.ChooseUse(1);Assert.True(c.Ready);Assert.Equal(Bag((0,2)),c.SelectedCounts);c.ConfigureBankTrade(2,2,1);Assert.False(c.Ready);Assert.Contains("2:1",c.BankConstraintReason);}
    [Fact] public void GenericPortRequiresOneUseAndPreservesItsThreeToOneRate()
    {var c=Start(View(),Bag((0,2),(1,1)),3);c.ConfigureBankTrade(3,1);c.ToggleCard(0,1);c.ChooseUse(0);c.ToggleCard(0,0);c.ChooseUse(0);c.ToggleCard(1,0);c.ChooseUse(1);Assert.False(c.Ready);c.ChooseUse(0);Assert.True(c.Ready);c.ConfigureBankTrade(2,1);Assert.False(c.Ready);}
    static ThreeKingdomsState Generals(string general)
    {
        var tk=ThreeKingdomsEngine.Create(new[]{"P1","P2","P3","P4"},42,_=>0,true,GeneralCatalog.B45PackageVersion);
        while(tk.PendingDecision!=null){var d=tk.PendingDecision;if(d.Kind==ThreeKingdomsDecisionKind.SelectOpening)ThreeKingdomsEngine.ChooseGenerals(tk,d.PlayerId,d.CandidateIds.Take(2).ToArray(),d.DecisionId,d.WindowId,_=>0);else ThreeKingdomsEngine.DeployGeneral(tk,d.PlayerId,d.CandidateIds[0],d.DecisionId,d.WindowId,_=>0);}
        ThreeKingdomsEngine.BeginNormalTurns(tk);tk.Clock.Phase=ThreeKingdomsPhase.Play;tk.Clock.PhaseId=5;tk.Clock.DrawPhaseTurnInstanceId=tk.Clock.TurnInstanceId;
        var ids=new[]{general,"tk-g12","tk-g37","tk-g19"};for(int i=0;i<4;i++){var current=tk.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P"+(i+1));var desired=tk.Generals.Single(g=>g.GeneralId==ids[i]);(current.GeneralId,desired.GeneralId)=(desired.GeneralId,current.GeneralId);(current.InstanceId,desired.InstanceId)=(desired.InstanceId,current.InstanceId);}tk.Response=null;return tk;
    }
    static dynamic Player(dynamic state,string id="P1")=>state is M4.GameState city?(object)city.Players.Single(p=>p.Id==id):((M5.GameState)state).Players.Single(p=>p.Id==id);
    static dynamic Fixture(string mode,string general,Action<dynamic> setup)
    {if(mode=="cities")return M4CoreHarness.Fixture(s=>{s.RulesVersion=M4.CitiesKnightsGameSession.ThreeKingdomsB45RulesVersion;s.ThreeKingdoms=Generals(general);setup(s);});return M5Harness.Fixture(s=>{s.RulesVersion=M5.CombinedGameSession.ThreeKingdomsB45RulesVersion;s.ThreeKingdoms=Generals(general);setup(s);});}
    static dynamic Command(dynamic game,string mode,string kind,string player="P1")
    {dynamic c=mode=="cities"?(object)new M4.Command{PlayerId=player,Kind=Enum.Parse<M4.CommandKind>(kind)}:new M5.Command{PlayerId=player,Kind=Enum.Parse<M5.CommandKind>(kind)};c.Id=ThreeKingdomsTestGame.NextId();c.WindowId=game.GetPlayerView(player).ThreeKingdoms.WindowId;return c;}
    static void Execute(dynamic game,dynamic command){var r=game.Execute(command);Assert.True((bool)r.Success,(string)r.ErrorCode+":"+(string)r.Message);game.AssertInvariants();}
    [Theory][InlineData("cities")][InlineData("combined")]
    public void GuhuoBankDraftProducesPrivateBatchAndPreviewDoesNotConsume(string mode)
    {
        dynamic game=Fixture(mode,"tk-g07",s=>Player(s).Resources.Wood=4);ThreeKingdomsView v=game.GetPlayerView("P1").ThreeKingdoms;var c=Start(v,Bag((0,4)),4);c.SetDeclaring(true);
        foreach(int ordinal in new[]{3,1,0,2}){c.ToggleCard(0,ordinal);c.ChooseUse(1);}Assert.True(c.Ready);
        dynamic command=Command(game,mode,"BankTrade");command.GiveResource=Resource.Wood;command.GiveEffectiveCardType=1;command.GiveAmount=4;command.ReceiveResource=Resource.Ore;command.ReceiveAmount=1;command.PaymentCards=c.SelectedCounts;command.DeclaredRealTypes=c.RealTypes;command.DeclaredEffectiveTypes=c.EffectiveTypes;
        string before=game.Save();Assert.True((bool)game.Preview(command).Success);Assert.Equal(before,(string)game.Save());Execute(game,command);Assert.Equal(4,(int)Player(game.GetAuthoritativeStateForTesting()).Resources.Wood);
        for(int card=0;card<4;card++)foreach(string player in new[]{"P2","P3","P4"}){B2SkillAction[] actions=game.GetPlayerView(player).ThreeKingdoms.SkillActions;dynamic trust=Command(game,mode,"UseGeneralSkill",player);trust.SkillCommand=actions.Single(a=>a.Command.Option=="response-trust").Command;Execute(game,trust);}
        Assert.Equal(0,(int)Player(game.GetAuthoritativeStateForTesting()).Resources.Wood);Assert.Equal(1,(int)Player(game.GetAuthoritativeStateForTesting()).Resources.Ore);
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void LongdanPlayerTradeUsesChosenRealCardsAndPublicEffectiveQuote(string mode)
    {
        dynamic game=Fixture(mode,"tk-g42",s=>{Player(s).Resources.Brick=1;Player(s,"P2").Resources.Wool=1;});ThreeKingdomsView v=game.GetPlayerView("P1").ThreeKingdoms;var c=Start(v,Bag((1,1)),1);c.ToggleCard(1,0);c.ChooseUse(0);Assert.True(c.Ready);
        dynamic command=Command(game,mode,"ProposeTrade");command.OtherPlayerId="P2";command.Give=new ResourceBag{Wood=1};command.Receive=new ResourceBag{Wool=1};command.PaymentCards=c.SelectedCounts;
        string before=game.Save();Assert.True((bool)game.Preview(command).Success);Assert.Equal(before,(string)game.Save());Execute(game,command);dynamic observed=game.GetPlayerView("P3");Assert.Equal(1,(int)observed.TradeOffer.Give.Wood);Assert.Null(observed.TradeOffer.B4RealGive);
        Execute(game,Command(game,mode,"AcceptTrade","P2"));Assert.Equal(1,(int)Player(game.GetAuthoritativeStateForTesting(),"P2").Resources.Brick);Assert.Equal(1,(int)Player(game.GetAuthoritativeStateForTesting()).Resources.Wool);
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void OversizedOrWrongPurposeBankPaymentFailsWithoutChangingState(string mode)
    {
        dynamic game=Fixture(mode,"tk-g42",s=>Player(s).Resources.Brick=4);dynamic command=Command(game,mode,"BankTrade");command.GiveResource=Resource.Brick;command.GiveEffectiveCardType=4;command.GiveAmount=4;command.ReceiveResource=Resource.Wool;command.PaymentCards=Bag((1,4));string before=game.Save();Assert.False((bool)game.Preview(command).Success);Assert.False((bool)game.Execute(command).Success);Assert.Equal(before,(string)game.Save());
    }
}
