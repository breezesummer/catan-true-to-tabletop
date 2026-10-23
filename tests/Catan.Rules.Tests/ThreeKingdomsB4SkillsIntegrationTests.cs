using System.Reflection;
using System.Runtime.Serialization.Json;
using Catan.Core;
using Catan.Core.ThreeKingdoms;
using M4=Catan.Core.M4;
using M5=Catan.Core.M5;
using Xunit;

namespace Catan.Rules.Tests;

/// <summary>独立原生状态注入后经 Execute 验证费用/虚拟牌；只作为宿主集成证据。</summary>
public sealed class ThreeKingdomsB4SkillsIntegrationTests
{
    private static ThreeKingdomsState Generals(params string[] requested)
    {
        var tk=ThreeKingdomsEngine.Create(new[]{"P1","P2","P3","P4"},42,_=>0,true,GeneralCatalog.B4PackageVersion);
        while(tk.PendingDecision!=null)
        {
            var d=tk.PendingDecision;
            if(d.Kind==ThreeKingdomsDecisionKind.SelectOpening) ThreeKingdomsEngine.ChooseGenerals(tk,d.PlayerId,d.CandidateIds.Take(2).ToArray(),d.DecisionId,d.WindowId,_=>0);
            else ThreeKingdomsEngine.DeployGeneral(tk,d.PlayerId,d.CandidateIds[0],d.DecisionId,d.WindowId,_=>0);
        }
        ThreeKingdomsEngine.BeginNormalTurns(tk);tk.Clock.Phase=ThreeKingdomsPhase.Play;tk.Clock.PhaseId=5;tk.Clock.DrawPhaseTurnInstanceId=tk.Clock.TurnInstanceId;
        for(int i=0;i<requested.Length;i++)
        {
            var current=tk.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P"+(i+1));var desired=tk.Generals.Single(g=>g.GeneralId==requested[i]);
            (current.GeneralId,desired.GeneralId)=(desired.GeneralId,current.GeneralId);(current.InstanceId,desired.InstanceId)=(desired.InstanceId,current.InstanceId);
        }
        tk.Automation=new B4AutomationState {TurnId=tk.Clock.TurnInstanceId,Asked=tk.Generals.Where(g=>g.Region==GeneralRegion.Active).SelectMany(g=>GeneralCatalog.Get(g.GeneralId).Skills.Select(s=>tk.Clock.PhaseId+":"+g.TenureId+":"+s.Id)).ToArray()};
        return tk;
    }
    private static dynamic Fixture(string mode,string general,Action<dynamic>? configure=null)
    {
        if(mode=="cities")return M4CoreHarness.Fixture(s=>{s.RulesVersion=M4.CitiesKnightsGameSession.ThreeKingdomsB4RulesVersion;s.ThreeKingdoms=Generals(general,"tk-g06","tk-g09","tk-g22");configure?.Invoke(s);});
        return M5Harness.Fixture(s=>{s.RulesVersion=M5.CombinedGameSession.ThreeKingdomsB4RulesVersion;s.ThreeKingdoms=Generals(general,"tk-g06","tk-g09","tk-g22");configure?.Invoke(s);});
    }
    private static dynamic Command(dynamic game,string mode,string kind,string player="P1")
    {
        dynamic c=mode=="cities"?(object)new M4.Command {Kind=Enum.Parse<M4.CommandKind>(kind),PlayerId=player}:(object)new M5.Command {Kind=Enum.Parse<M5.CommandKind>(kind),PlayerId=player};
        c.Id=ThreeKingdomsTestGame.NextId();c.WindowId=game.GetPlayerView(player).ThreeKingdoms.WindowId;return c;
    }
    private static void Accept(dynamic game,dynamic command){var r=game.Execute(command);Assert.True((bool)r.Success,(string)r.ErrorCode+":"+(string)r.Message);game.AssertInvariants();}
    private static void Skill(dynamic game,string mode,string player,string option,Func<B2SkillAction,bool>? filter=null)
    {B2SkillAction[] actions=game.GetPlayerView(player).ThreeKingdoms.SkillActions;var a=actions.First(a=>a.Command.Option==option&&(filter==null||filter(a)));dynamic c=Command(game,mode,"UseGeneralSkill",player);c.SkillCommand=a.Command;Accept(game,c);}
    private static dynamic Owner(dynamic game,string player="P1") {dynamic s=game.GetAuthoritativeStateForTesting();return modePlayer(s,player);}
    private static dynamic modePlayer(dynamic s,string player) {if(s is M4.GameState a)return a.Players.Single(p=>p.Id==player);return ((M5.GameState)s).Players.Single(p=>p.Id==player);}
    private static void PlaceSettlement(dynamic s,string vertex)
    {if(s is M4.GameState a)a.Settlements=new[]{M4CoreHarness.Piece(vertex)};else ((M5.GameState)s).Settlements=new[]{M5Harness.Piece(vertex)};}
    private static string Vertex(string mode)=>mode=="cities"?M4CoreHarness.New().GetPlayerView("P1").Board.Vertices[0].Id:M5Harness.New().GetPlayerView("P1").Board.Tiles.First(t=>t.Resource!="sea").Vertices[0];
    private static string Road(dynamic game,string vertex){dynamic view=game.GetPlayerView("P1");if(view is M4.PlayerView a)return a.Board.Edges.First(e=>e.Vertices.Contains(vertex)).Id;M5.PlayerView b=view;return b.Board.Edges.First(e=>e.Vertices.Contains(vertex)&&b.Board.Tiles.Any(t=>t.Resource!="sea"&&e.Vertices.All(t.Vertices.Contains))).Id;}
    private static dynamic Restore(dynamic game,string mode)
    {
        object s=game.GetAuthoritativeStateForTesting();using var stream=new MemoryStream();var serializer=new DataContractJsonSerializer(s.GetType());serializer.WriteObject(stream,s);stream.Position=0;var copy=serializer.ReadObject(stream)!;
        object restored=mode=="cities"?(object)M4CoreHarness.New():M5Harness.New();restored.GetType().GetField("state",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(restored,copy);return restored;
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void LongdanNativeRoadPaysTwoRealBricksAndRejectsInsufficientEntity(string mode)
    {
        string vertex=Vertex(mode);dynamic game=Fixture(mode,"tk-g42",s=>{PlaceSettlement(s,vertex);modePlayer(s,"P1").Resources.Brick=2;});dynamic c=Command(game,mode,"BuildRoad");c.TargetId=Road(game,vertex);Accept(game,c);
        Assert.Equal(0,(int)Owner(game).Resources.Total);Assert.Equal(19,(int)game.GetAuthoritativeStateForTesting().Bank.Brick);Assert.Equal(19,(int)game.GetAuthoritativeStateForTesting().Bank.Wood);
        ThreeKingdomsState linked=game.GetAuthoritativeStateForTesting().ThreeKingdoms;
        var conversion=Assert.Single(linked.SkillHistory.Where(e=>e.SkillId=="tk-g42-s1"));
        Assert.Contains(conversion.RelatedCardFlowIds,id=>id.StartsWith("tk-pay-"));
        dynamic poor=Fixture(mode,"tk-g42",s=>{PlaceSettlement(s,vertex);modePlayer(s,"P1").Resources.Brick=1;});dynamic bad=Command(poor,mode,"BuildRoad");bad.TargetId=Road(poor,vertex);string before=poor.Save();Assert.False((bool)poor.Execute(bad).Success);Assert.Equal(before,(string)poor.Save());
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void QingguoNativePaymentReturnsPaperToCommodityBank(string mode)
    {
        string vertex=Vertex(mode);dynamic game=Fixture(mode,"tk-g44",s=>{PlaceSettlement(s,vertex);modePlayer(s,"P1").Resources.Brick=1;modePlayer(s,"P1").Commodities.Paper=1;});
        dynamic c=Command(game,mode,"BuildRoad");c.TargetId=Road(game,vertex);c.PaymentCards=new[]{0,1,0,0,0,0,0,1};Accept(game,c);Assert.Equal(0,(int)Owner(game).Commodities.Paper);Assert.Equal(12,(int)game.GetAuthoritativeStateForTesting().CommodityBank.Paper);Assert.Equal(19,(int)game.GetAuthoritativeStateForTesting().Bank.Wood);
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void MiewuPaymentHistoryIdsDoNotEncodeRealOrEffectiveCardTypes(string mode)
    {
        string vertex=Vertex(mode);var publicIds=new List<string>();
        foreach(var choice in new[]{(Real:1,Track:0),(Real:4,Track:0),(Real:4,Track:1)})
        {
            dynamic game=Fixture(mode,"tk-g41",s=>
            {
                if(s is M4.GameState a)a.Cities=new[]{M4CoreHarness.Piece(vertex)};else((M5.GameState)s).Cities=new[]{M5Harness.Piece(vertex)};
                ResourceBag hand=modePlayer(s,"P1").Resources;hand[(Resource)choice.Real]=1;
                ThreeKingdomsState tk=s.ThreeKingdoms;tk.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P1").Counters=new[]{new GeneralCounter{Key="wuku",Value=1},new GeneralCounter{Key="sanchen",Value=1}};
            });
            dynamic c=Command(game,mode,"ImproveCity");c.Id="conversion-payment-privacy";
            if(mode=="cities")c.Track=(M4.ImprovementTrack)choice.Track;else c.Track=(M5.ImprovementTrack)choice.Track;
            var payment=new int[8];payment[choice.Real]=1;c.PaymentCards=payment;
            dynamic restored=Restore(game,mode);Accept(game,c);Accept(restored,c);Assert.Equal((string)game.Save(),(string)restored.Save());
            ThreeKingdomsState authority=game.GetAuthoritativeStateForTesting().ThreeKingdoms;
            var actual=Assert.Single(authority.SkillHistory,e=>e.SkillId=="tk-g41-s3");
            Assert.Matches(@"^tk-pay-\d+:conversion-0$",actual.EventId);
            Assert.Contains(actual.EventId.Split(':')[0],actual.RelatedCardFlowIds);
            B4SkillHistoryView[] projected=game.GetPlayerView("P3").ThreeKingdoms.SkillHistory;
            Assert.Equal(actual.EventId,Assert.Single(projected,e=>e.SkillId=="tk-g41-s3").EventId);
            Assert.Equal(actual.EventId,Assert.Single((GameHistoryEntry[])game.GetHistory("P3",0),e=>e.SkillId=="tk-g41-s3").SkillEventId);
            string settled=game.Save();Assert.True((bool)game.Execute(c).IsDuplicate);Assert.Equal(settled,(string)game.Save());
            publicIds.Add(actual.EventId);
        }
        // 改变真实底牌或有效用途，不应改变旁观者看到的转换编号。
        Assert.Single(publicIds.Distinct());
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void MiewuVirtualRoadBuildingRestoresPendingAndRewardsOnlyAfterCompletion(string mode)
    {
        string vertex=Vertex(mode);dynamic game=Fixture(mode,"tk-g41",s=>{PlaceSettlement(s,vertex);modePlayer(s,"P1").Resources.Wheat=1;ThreeKingdomsState tk=s.ThreeKingdoms;tk.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P1").Counters=new[]{new GeneralCounter{Key="wuku",Value=3},new GeneralCounter{Key="sanchen",Value=1}};});
        Skill(game,mode,"P1","b4-miewu-progress",(Func<B2SkillAction,bool>)(a=>a.VirtualCardId=="RoadBuilding"));Assert.Equal("ProgressRoads",(string)game.GetAuthoritativeStateForTesting().PendingDecision.Kind);Assert.Equal(0,(int)Owner(game).Resources.Total);Assert.Equal(0,(int)Owner(game).ProgressCards.Length);
        dynamic copy=Restore(game,mode);dynamic done=Command(game,mode,"ResolveProgressChoice");done.Decline=true;Accept(game,done);Accept(copy,done);Assert.Equal(1,(int)Owner(game).Resources.Total);Assert.Null(game.GetAuthoritativeStateForTesting().ThreeKingdomsVirtualUse);Assert.Equal((string)game.Save(),(string)copy.Save());
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void XiemuNativeMonopolyUsesTwoRealCardsAndNoVirtualDeckEntity(string mode)
    {
        dynamic game=Fixture(mode,"tk-g52",s=>{if(s is M4.GameState a)a.Players[0].ProgressCards=new[]{M4.ProgressCardKind.ResourceMonopoly,M4.ProgressCardKind.Crane};else ((M5.GameState)s).Players[0].ProgressCards=new[]{M5.ProgressCardKind.ResourceMonopoly,M5.ProgressCardKind.Crane};modePlayer(s,"P2").Resources.Wood=2;});
        Skill(game,mode,"P1","b4-xiemu",(Func<B2SkillAction,bool>)(a=>a.VirtualCardId=="ResourceMonopoly"));
        Assert.Equal(0,(int)Owner(game).ProgressCards.Length);Assert.Equal(3,(int)Owner(game).Resources.Total);Assert.Null(game.GetAuthoritativeStateForTesting().ThreeKingdomsVirtualUse);game.AssertInvariants();
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void WenjiMarksTrueResourceAndNativeTwoForOneTradeConsumesExactlyTwo(string mode)
    {
        dynamic game=Fixture(mode,"tk-g45",s=>modePlayer(s,"P2").Resources.Wood=2);
        Skill(game,mode,"P1","b4-wenji");Skill(game,mode,"P2","b4-give",(Func<B2SkillAction,bool>)(a=>a.Command.CardType==0));Skill(game,mode,"P1","b4-wenji");Skill(game,mode,"P2","b4-give",(Func<B2SkillAction,bool>)(a=>a.Command.CardType==0));
        dynamic c=Command(game,mode,"BankTrade");c.GiveResource=Resource.Wood;c.GiveAmount=2;c.ReceiveResource=Resource.Wool;c.ReceiveAmount=1;Accept(game,c);
        Assert.Equal(0,(int)Owner(game).Resources.Wood);Assert.Equal(1,(int)Owner(game).Resources.Wool);Assert.Equal(19,(int)game.GetAuthoritativeStateForTesting().Bank.Wood);
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void DaoshuObservesNativePlayerTradeExactlyOnce(string mode)
    {
        dynamic game=Fixture(mode,"tk-g40",s=>{modePlayer(s,"P1").Resources.Wood=1;modePlayer(s,"P2").Resources.Wool=1;});dynamic c=Command(game,mode,"ProposeTrade");c.OtherPlayerId="P2";c.Give=new ResourceBag{Wood=1};c.Receive=new ResourceBag{Wool=1};Accept(game,c);Accept(game,Command(game,mode,"AcceptTrade","P2"));Assert.Equal(2,(int)Owner(game).Resources.Total);game.AssertInvariants();
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void ConvertedBulkBankTradePaysOneTrueCommodityBatchAndReceivesTwoKinds(string mode)
    {
        dynamic game=Fixture(mode,"tk-g44",s=>modePlayer(s,"P1").Commodities.Paper=8);dynamic c=Command(game,mode,"BankTrade");
        c.GiveIsCommodity=true;if(mode=="cities")c.GiveCommodity=M4.Commodity.Paper;else c.GiveCommodity=M5.Commodity.Paper;c.GiveEffectiveCardType=0;c.GiveAmount=8;c.Receive=new ResourceBag{Wool=1,Wheat=1};c.PaymentCards=new[]{0,0,0,0,0,0,0,8};Accept(game,c);
        Assert.Equal(0,(int)Owner(game).Commodities.Paper);Assert.Equal(2,(int)Owner(game).Resources.Total);Assert.Equal(12,(int)game.GetAuthoritativeStateForTesting().CommodityBank.Paper);Assert.Equal(19,(int)game.GetAuthoritativeStateForTesting().Bank.Wood);game.AssertInvariants();
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void MiewuOneCardConversionCannotConvertFourCardsForBankTrade(string mode)
    {
        dynamic game=Fixture(mode,"tk-g41",s=>{modePlayer(s,"P1").Resources.Brick=4;ThreeKingdomsState tk=s.ThreeKingdoms;tk.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P1").Counters=new[]{new GeneralCounter{Key="wuku",Value=3},new GeneralCounter{Key="sanchen",Value=1}};});
        dynamic c=Command(game,mode,"BankTrade");c.GiveResource=Resource.Brick;c.GiveAmount=4;c.GiveEffectiveCardType=0;c.ReceiveResource=Resource.Wool;c.ReceiveAmount=1;string before=game.Save();Assert.False((bool)game.Execute(c).Success);Assert.Equal(before,(string)game.Save());
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void VirtualMedicineIsRecheckedAfterConsumingBottomCards(string mode)
    {
        string vertex=Vertex(mode);dynamic game=Fixture(mode,"tk-g34",s=>{PlaceSettlement(s,vertex);modePlayer(s,"P1").Resources.Wheat=1;modePlayer(s,"P1").Resources.Ore=2;ThreeKingdomsState tk=s.ThreeKingdoms;tk.Clock.Phase=ThreeKingdomsPhase.Preparation;if(s is M4.GameState a)a.Phase=M4.GamePhase.ProductionAwaitRoll;else((M5.GameState)s).Phase=M5.GamePhase.ProductionAwaitRoll;});
        object state=game.GetAuthoritativeStateForTesting();object session=game;var host=(IThreeKingdomsB4Host)session.GetType().GetMethod("CardHost",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(session,new[]{state})!;
        var medicine=host.GetVirtualProgressUses("P1","prepare").First(u=>u.CardId=="Medicine");Assert.False(((IThreeKingdomsB4VirtualCostHost)host).CanUseVirtualProgressAfterPayment("P1",medicine,host.GetHand("P1")));Assert.True(((IThreeKingdomsB4VirtualCostHost)host).CanUseVirtualProgressAfterPayment("P1",medicine,new int[8]));
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void MiewuCityImprovementPaysTrueResourceAsCommodityAndRewardsAfterBuilding(string mode)
    {
        string vertex=Vertex(mode);dynamic game=Fixture(mode,"tk-g41",s=>
        {
            if(s is M4.GameState a)a.Cities=new[]{M4CoreHarness.Piece(vertex)};else((M5.GameState)s).Cities=new[]{M5Harness.Piece(vertex)};
            modePlayer(s,"P1").Resources.Brick=1;ThreeKingdomsState tk=s.ThreeKingdoms;tk.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P1").Counters=new[]{new GeneralCounter{Key="wuku",Value=3},new GeneralCounter{Key="sanchen",Value=1}};
        });
        dynamic c=Command(game,mode,"ImproveCity");if(mode=="cities")c.Track=M4.ImprovementTrack.Trade;else c.Track=M5.ImprovementTrack.Trade;c.PaymentCards=new[]{0,1,0,0,0,0,0,0};Accept(game,c);
        Assert.Equal(1,(int)Owner(game).Improvements[0]);Assert.Equal(0,(int)Owner(game).Commodities.Total);Assert.Equal(1,(int)Owner(game).Resources.Total);Assert.Equal(12,(int)game.GetAuthoritativeStateForTesting().CommodityBank.Cloth);
        ThreeKingdomsState result=game.GetAuthoritativeStateForTesting().ThreeKingdoms;var g=result.Generals.Single(x=>x.Region==GeneralRegion.Active&&x.OwnerPlayerId=="P1");Assert.Equal(2,g.Counters.Single(x=>x.Key=="wuku").Value);Assert.Equal(1,g.Counters.Single(x=>x.Key=="miewu").Value);Assert.DoesNotContain(result.Skills.TimedEffects,e=>e.Kind=="b4-conversion-reward");
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void ForbiddenProgressUseIsRejectedBeforeTargetCanGainFromYiji(string mode)
    {
        dynamic game=Fixture(mode,"tk-g06",s=>
        {
            s.ThreeKingdoms=Generals("tk-g06","tk-g37","tk-g09","tk-g22");ThreeKingdomsState tk=s.ThreeKingdoms;
            tk.Skills.TimedEffects=new[]{new B2TimedEffect{Id="test-indulgence",Kind="response-indulgence-active",PlayerId="P3",TargetPlayerId="P1",CreatedTurnId=tk.Clock.TurnInstanceId}};
            if(s is M4.GameState a)a.Players[0].ProgressCards=new[]{M4.ProgressCardKind.ResourceMonopoly};else((M5.GameState)s).Players[0].ProgressCards=new[]{M5.ProgressCardKind.ResourceMonopoly};
        });
        dynamic c=Command(game,mode,"PlayProgressCard");if(mode=="cities")c.ProgressCard=M4.ProgressCardKind.ResourceMonopoly;else c.ProgressCard=M5.ProgressCardKind.ResourceMonopoly;c.Resource=Resource.Wood;
        string before=game.Save();Assert.False((bool)game.Preview(c).Success);Assert.Equal(before,(string)game.Save());Assert.False((bool)game.Execute(c).Success);Assert.Equal(before,(string)game.Save());Assert.Null(game.GetAuthoritativeStateForTesting().ThreeKingdomsDeferredCommand);game.AssertInvariants();
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void PreparationVirtualUsesIncludeAllAlchemyNumbersAndAllInventionPairs(string mode)
    {
        dynamic game=Fixture(mode,"tk-g34",s=>{ThreeKingdomsState tk=s.ThreeKingdoms;tk.Clock.Phase=ThreeKingdomsPhase.Preparation;if(s is M4.GameState a)a.Phase=M4.GamePhase.ProductionAwaitRoll;else((M5.GameState)s).Phase=M5.GamePhase.ProductionAwaitRoll;});
        object state=game.GetAuthoritativeStateForTesting();object session=game;var host=(IThreeKingdomsB4Host)session.GetType().GetMethod("CardHost",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(session,new[]{state})!;
        string before=game.Save();var uses=host.GetVirtualProgressUses("P1","prepare");var alchemy=uses.Where(u=>u.CardId=="Alchemy").ToArray();Assert.Equal(36,alchemy.Length);Assert.All(alchemy,u=>Assert.Contains("炼金术士",u.Label));
        int tiles=state is M4.GameState a4?a4.Tiles.Count(t=>t.Number.HasValue&&!new[]{2,6,8,12}.Contains(t.Number.Value)):((M5.GameState)state).Tiles.Count(t=>t.Number.HasValue&&!new[]{2,6,8,12}.Contains(t.Number.Value));
        Assert.Equal(tiles*(tiles-1)/2,uses.Count(u=>u.CardId=="Invention"));Assert.Equal(before,(string)game.Save());
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void QiceAlchemyPreservesChosenNumbersThroughAutomaticRollAndUsesNoEntityCard(string mode)
    {
        dynamic game=Fixture(mode,"tk-g34",s=>{modePlayer(s,"P1").Resources.Wood=2;ThreeKingdomsState tk=s.ThreeKingdoms;tk.Clock.Phase=ThreeKingdomsPhase.Preparation;tk.Clock.DrawPhaseTurnInstanceId=0;if(s is M4.GameState a)a.Phase=M4.GamePhase.ProductionAwaitRoll;else((M5.GameState)s).Phase=M5.GamePhase.ProductionAwaitRoll;});
        object state=game.GetAuthoritativeStateForTesting();object session=game;session.GetType().GetMethod("PumpB4",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(session,new[]{state});session.GetType().GetField("state",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(session,state);
        Skill(game,mode,"P1","accept");Skill(game,mode,"P1","response-qice-use",(Func<B2SkillAction,bool>)(a=>a.VirtualCardId=="Alchemy"&&a.Label.EndsWith("1 + 1")));
        Assert.Equal(1,(int)game.GetAuthoritativeStateForTesting().LastDice1);Assert.Equal(1,(int)game.GetAuthoritativeStateForTesting().LastDice2);Assert.Equal(0,(int)Owner(game).ProgressCards.Length);Assert.Equal(0,(int)Owner(game).Resources.Total);Assert.Null(game.GetAuthoritativeStateForTesting().ThreeKingdomsVirtualUse);game.AssertInvariants();
    }
}
