using System.Reflection;
using System.Runtime.Serialization.Json;
using Catan.Core;
using Catan.Core.ThreeKingdoms;
using M4=Catan.Core.M4;
using M5=Catan.Core.M5;
using Xunit;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB6ControlTests
{
    [Fact] public void CompletePackageHasAllNinetySevenSkillEntriesAndIndependentControlClone()
    {
        Assert.Null(B2SkillEngine.ControllerForViewB6(null,null));Assert.Null(B2SkillEngine.ControllerForViewB6(new ThreeKingdomsView(),null));Assert.Equal("P1",B2SkillEngine.ControllerForViewB6(null,"P1"));
        Assert.Equal(54,GeneralCatalog.B6Definitions.Length);Assert.Equal(97,GeneralCatalog.B6Definitions.Sum(g=>g.Skills.Length));
        Assert.All(GeneralCatalog.B6Definitions.SelectMany(g=>g.Skills),skill=>Assert.NotNull(B45SkillCatalog.Get(skill.Id)));
        var state=State();var copy=ThreeKingdomsEngine.Clone(state);copy.ControlB6.AssetPlayerId="P3";
        Assert.Equal("P1",state.ControlB6.AssetPlayerId);
    }
    private static ThreeKingdomsB2TestHost Host()
    {
        var h=ThreeKingdomsB2TestHost.Create("tk-g04","tk-g12","tk-g22");
        h.State.RulesVersion=GeneralCatalog.B6RulesVersion;h.State.PackageVersion=GeneralCatalog.B6PackageVersion;
        h.State.PackageContentHash=GeneralCatalog.B6ContentHash;return h;
    }
    [Fact] public void HuoxinOffersOnceAndRestoresAcceptedGrantWithoutChangingAssets()
    {
        var h=Host();h.State.Clock.ActivePlayerId="P2";h.State.Clock.Phase=ThreeKingdomsPhase.Preparation;
        B2SkillEngine.OnOpportunityStart(h,1,"P2");B2SkillEngine.Pump(h);
        Assert.Equal("P1",h.State.Skills.PendingDecision.PlayerId);
        var saved=h.Copy();var action=B2SkillEngine.Actions(h,"P1").Single(a=>a.Command.Option=="accept").Command;
        B2SkillEngine.Resolve(h,"P1",action);B2SkillEngine.Resolve(saved,"P1",action);
        Assert.Equal(h.Snapshot(),saved.Snapshot());Assert.Equal("P1",B2SkillEngine.ControllerB6(h.State,"P2"));
        Assert.False(B2SkillEngine.CanOperateB6(h.State,"P2","P2"));Assert.True(B2SkillEngine.CanOperateB6(h.State,"P1","P1"));
        Assert.Equal(1,h.State.Generals.Single(g=>g.GeneralId=="tk-g04").Counters.Single(c=>c.Key=="huoxin").Value);
        B2SkillEngine.OnOpportunityStart(h,1,"P2");Assert.Null(h.State.Skills.PendingDecision);
        B2SkillEngine.OnOpportunityEnd(h,1,"P2");Assert.Null(h.State.ControlB6);
        B2SkillEngine.OnOpportunityStart(h,2,"P3");B2SkillEngine.Pump(h);Assert.Null(h.State.Skills.PendingDecision);
    }
    [Fact] public void SkippingHuoxinDoesNotConsumeLimitOrReofferAtSameOpportunity()
    {
        var h=Host();B2SkillEngine.OnOpportunityStart(h,1,"P1");B2SkillEngine.Pump(h);
        B2SkillEngine.Resolve(h,"P1",B2SkillEngine.Actions(h,"P1").Single(a=>a.Command.Option=="skip").Command);
        B2SkillEngine.OnOpportunityStart(h,1,"P1");B2SkillEngine.Pump(h);Assert.Null(h.State.Skills.PendingDecision);Assert.Null(h.State.ControlB6);
        Assert.DoesNotContain(h.State.SkillHistory??Array.Empty<B4SkillHistoryEvent>(),e=>e.SkillId=="tk-g04-s1");
        h.State.Clock.OpportunityId=2;B2SkillEngine.OnOpportunityStart(h,2,"P1");B2SkillEngine.Pump(h);Assert.NotNull(h.State.Skills.PendingDecision);
    }
    [Fact] public void BiyueUsesTurnOwnerAndSelectsTwoRealResourcesWithEmptyBankAndBan()
    {
        var h=Host();h.State.Clock.ActivePlayerId="P2";B2SkillEngine.OnEnd(h,"other");Assert.DoesNotContain(h.State.Skills.Frames,f=>f.SkillId=="tk-g04-s2");
        h.State.Clock.ActivePlayerId="P1";h.State.Skills.TimedEffects=new[]{new B2TimedEffect {Id="禁木",Kind="b6-jinglue:0",PlayerId="P2",OpportunityId=1}};
        B2SkillEngine.OnEnd(h,"own");var actions=B2SkillEngine.Actions(h,"P1");Assert.NotEmpty(actions);
        Assert.All(actions,a=>{Assert.Equal(0,a.Command.Counts[0]);Assert.Equal(0,a.Command.Counts.Skip(5).Sum());});
        B2SkillEngine.Resolve(h,"P1",actions[0].Command);Assert.Equal(2,h.GetHand("P1").Sum());h.AssertConserved();
        var empty=Host();empty.EmptyBank();B2SkillEngine.OnEnd(empty,"empty");Assert.Null(empty.State.Skills.PendingDecision);Assert.Empty(empty.State.Skills.Frames);
    }
    internal static ThreeKingdomsState State()
    {
        var tk=ThreeKingdomsEngine.Create(new[]{"P1","P2","P3","P4"},42,_=>0,true,GeneralCatalog.B6PackageVersion);
        while(tk.PendingDecision!=null){var d=tk.PendingDecision;if(d.Kind==ThreeKingdomsDecisionKind.SelectOpening)ThreeKingdomsEngine.ChooseGenerals(tk,d.PlayerId,d.CandidateIds.Take(2).ToArray(),d.DecisionId,d.WindowId,_=>0);else ThreeKingdomsEngine.DeployGeneral(tk,d.PlayerId,d.CandidateIds[0],d.DecisionId,d.WindowId,_=>0);}
        ThreeKingdomsEngine.BeginNormalTurns(tk);tk.Clock.Phase=ThreeKingdomsPhase.Play;tk.Clock.PhaseId=5;tk.Clock.DrawPhaseTurnInstanceId=tk.Clock.TurnInstanceId;
        string[] ids={"tk-g15","tk-g04","tk-g12","tk-g22"};
        for(int i=0;i<4;i++){var g=tk.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P"+(i+1));var x=tk.Generals.Single(g=>g.GeneralId==ids[i]);(g.GeneralId,x.GeneralId)=(x.GeneralId,g.GeneralId);(g.InstanceId,x.InstanceId)=(x.InstanceId,g.InstanceId);g.Counters=Array.Empty<GeneralCounter>();}
        var diao=tk.Generals.Single(g=>g.GeneralId=="tk-g04");diao.Counters=new[]{new GeneralCounter{Key="huoxin",Value=1}};
        tk.Skills=new B2SkillState();tk.Response=null;tk.CivilB5=new B5CivilState();tk.Board=new B5BoardState();tk.Automation=new B4AutomationState{TurnId=tk.Clock.TurnInstanceId};
        tk.ControlB6=new B6ControlGrant{ControllerPlayerId="P2",AssetPlayerId="P1",TenureId=diao.TenureId,OpportunityId=tk.Clock.OpportunityId};return tk;
    }
    private static dynamic Game(string mode)
    {
        if(mode=="cities")return M4CoreHarness.Fixture(s=>{s.RulesVersion=M4.CitiesKnightsGameSession.ThreeKingdomsB6RulesVersion;s.ThreeKingdoms=State();s.Players[0].Resources.Wood=4;s.Players[1].Resources.Ore=1;});
        return M5Harness.Fixture(s=>{s.RulesVersion=M5.CombinedGameSession.ThreeKingdomsB6RulesVersion;s.ThreeKingdoms=State();s.Players[0].Resources.Wood=4;s.Players[1].Resources.Ore=1;});
    }
    private static dynamic Command(string mode,string kind,string actor="P2",string? asset="P1")
    {dynamic c=mode=="cities"?(object)new M4.Command{Kind=Enum.Parse<M4.CommandKind>(kind)}:new M5.Command{Kind=Enum.Parse<M5.CommandKind>(kind)};c.Id=ThreeKingdomsTestGame.NextId();c.PlayerId=actor;c.ActingAsPlayerId=asset;return c;}
    private static dynamic Restore(dynamic game,string mode)
    {
        object s=game.GetAuthoritativeStateForTesting();using var stream=new MemoryStream();var serializer=new DataContractJsonSerializer(s.GetType());serializer.WriteObject(stream,s);stream.Position=0;var copy=serializer.ReadObject(stream)!;
        object restored=mode=="cities"?(object)M4CoreHarness.New():M5Harness.New();restored.GetType().GetField("state",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(restored,copy);return restored;
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void AuthoritativeProxyProjectionPaymentRestoreAndRevocation(string mode)
    {
        dynamic game=Game(mode),view=game.GetPlayerView("P2");Assert.Equal("P1",(string)view.PlayerId);Assert.Equal("P2",(string)view.ViewerPlayerId);Assert.Equal(4,(int)view.OwnResources.Wood);
        Assert.Empty((Array)game.GetPlayerView("P1").LegalActions);Assert.Equal(0,(int)game.GetPlayerView("P3").OwnResources.Wood);
        ThreeKingdomsState tk=game.GetAuthoritativeStateForTesting().ThreeKingdoms;
        Assert.Equal(tk.Generals.Where(g=>g.Region==GeneralRegion.Hidden&&g.OwnerPlayerId=="P1").Select(g=>g.GeneralId),((ThreeKingdomsView)view.ThreeKingdoms).Players.Single(p=>p.PlayerId=="P1").HiddenGeneralIds);
        Assert.Empty(((ThreeKingdomsView)game.GetPlayerView("P3").ThreeKingdoms).Players.Single(p=>p.PlayerId=="P1").HiddenGeneralIds);
        dynamic c=Command(mode,"BankTrade");c.WindowId=tk.WindowId;c.GiveResource=Resource.Wood;c.ReceiveResource=Resource.Brick;c.GiveAmount=4;c.ReceiveAmount=1;
        dynamic restored=Restore(game,mode);Assert.True((bool)game.Preview(c).Success);Assert.True((bool)game.Execute(c).Success);Assert.True((bool)restored.Execute(c).Success);Assert.Equal((string)game.Save(),(string)restored.Save());Assert.True((bool)game.Execute(c).IsDuplicate);
        var after=game.GetPlayerView("P2");Assert.Equal(1,(int)after.OwnResources.Brick);Assert.Equal(0,(int)after.OwnResources.Wood);
        dynamic forged=Command(mode,"EndTurn","P3");forged.WindowId=after.ThreeKingdoms.WindowId;Assert.Equal("ControlPermission",(string)game.Execute(forged).ErrorCode);
        dynamic owner=Command(mode,"EndTurn","P1",null);owner.WindowId=after.ThreeKingdoms.WindowId;Assert.Equal("ControlPermission",(string)game.Execute(owner).ErrorCode);
        dynamic end=Command(mode,"EndTurn");end.WindowId=after.ThreeKingdoms.WindowId;Assert.True((bool)game.Execute(end).Success);
        Assert.Null(game.GetAuthoritativeStateForTesting().ThreeKingdoms.ControlB6);Assert.Equal("P2",(string)game.GetPlayerView("P2").PlayerId);
        c.Id=ThreeKingdomsTestGame.NextId();c.WindowId=game.GetPlayerView("P2").ThreeKingdoms.WindowId;Assert.Equal("ControlPermission",(string)game.Execute(c).ErrorCode);game.AssertInvariants();
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void ProxyCannotGiftOrUnequallyTradeButBankTradeRemainsNormal(string mode)
    {
        dynamic game=Game(mode),view=game.GetPlayerView("P2");Assert.DoesNotContain((B2SkillAction[])view.ThreeKingdoms.SkillActions,a=>a.Command.Option=="gift");
        dynamic c=Command(mode,"ProposeTrade");c.WindowId=view.ThreeKingdoms.WindowId;c.OtherPlayerId="P3";c.Give=new ResourceBag{Wood=2};c.Receive=new ResourceBag{Brick=1};
        Assert.Equal("TradeForbidden",(string)game.Preview(c).ErrorCode);
        c.Give=new ResourceBag{Wood=1};Assert.True((bool)game.Preview(c).Success);
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void ControlSurvivesEndOfTurnCapacityChoiceUntilActualHandover(string mode)
    {
        void Configure(dynamic s)
        {
            s.RulesVersion=mode=="cities"?M4.CitiesKnightsGameSession.ThreeKingdomsB6RulesVersion:M5.CombinedGameSession.ThreeKingdomsB6RulesVersion;s.ThreeKingdoms=State();
            s.ThreeKingdoms.Skills.TimedEffects=new[]{new B2TimedEffect{Id="safe",Kind="protection",PlayerId="P1",OpportunityId=s.ThreeKingdoms.Clock.OpportunityId}};
            string[] cards={"Crane","Medicine","Engineering","Irrigation","Mining"};
            if(mode=="cities")s.Players[0].ProgressCards=cards.Select(Enum.Parse<M4.ProgressCardKind>).ToArray();else s.Players[0].ProgressCards=cards.Select(Enum.Parse<M5.ProgressCardKind>).ToArray();
        }
        dynamic game=mode=="cities"?(object)M4CoreHarness.Fixture(s=>Configure(s)):M5Harness.Fixture(s=>Configure(s));
        dynamic c=Command(mode,"EndTurn");c.WindowId=game.GetPlayerView("P2").ThreeKingdoms.WindowId;
        dynamic r=game.Execute(c);Assert.True((bool)r.Success,(string)r.Message);
        dynamic view=game.GetPlayerView("P2");Assert.Equal("P1",(string)view.PlayerId);Assert.NotNull(view.ThreeKingdoms.ControlB6);Assert.Equal("ProgressDiscard",(string)view.PendingDecision.Kind);
        dynamic discard=Command(mode,"DiscardProgressCard");discard.WindowId=view.ThreeKingdoms.WindowId;
        if(mode=="cities")discard.ProgressCard=M4.ProgressCardKind.Crane;else discard.ProgressCard=M5.ProgressCardKind.Crane;
        r=game.Execute(discard);Assert.True((bool)r.Success,(string)r.Message);Assert.Null(game.GetAuthoritativeStateForTesting().ThreeKingdoms.ControlB6);game.AssertInvariants();
    }
}
