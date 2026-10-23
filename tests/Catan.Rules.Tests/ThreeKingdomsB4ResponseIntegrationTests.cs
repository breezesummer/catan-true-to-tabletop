using System.Reflection;
using System.Runtime.Serialization.Json;
using Catan.Core;
using Catan.Core.ThreeKingdoms;
using M4=Catan.Core.M4;
using M5=Catan.Core.M5;
using Xunit;

namespace Catan.Rules.Tests;

/// <summary>指定局面只验证正式 Execute 与宿主边界，恢复使用权威状态序列化，不冒充正常局的历史重放。</summary>
public sealed class ThreeKingdomsB4ResponseIntegrationTests
{
    private static ThreeKingdomsState Generals(params string[] requested)
    {
        var tk=ThreeKingdomsEngine.Create(new[]{"P1","P2","P3","P4"},42,_=>0,true,GeneralCatalog.B4PackageVersion);
        while(tk.PendingDecision!=null){var d=tk.PendingDecision;if(d.Kind==ThreeKingdomsDecisionKind.SelectOpening)ThreeKingdomsEngine.ChooseGenerals(tk,d.PlayerId,d.CandidateIds.Take(2).ToArray(),d.DecisionId,d.WindowId,_=>0);else ThreeKingdomsEngine.DeployGeneral(tk,d.PlayerId,d.CandidateIds[0],d.DecisionId,d.WindowId,_=>0);}
        ThreeKingdomsEngine.BeginNormalTurns(tk);tk.Clock.Phase=ThreeKingdomsPhase.Play;tk.Clock.PhaseId=5;tk.Clock.DrawPhaseTurnInstanceId=tk.Clock.TurnInstanceId;
        for(int i=0;i<requested.Length;i++){var g=tk.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P"+(i+1));var other=tk.Generals.Single(g=>g.GeneralId==requested[i]);(g.GeneralId,other.GeneralId)=(other.GeneralId,g.GeneralId);(g.InstanceId,other.InstanceId)=(other.InstanceId,g.InstanceId);g.Counters=Array.Empty<GeneralCounter>();B2SkillEngine.OnB4GeneralActivated(tk,g);}
        tk.Response=null;
        tk.Automation=new B4AutomationState{TurnId=tk.Clock.TurnInstanceId,Asked=tk.Generals.Where(g=>g.Region==GeneralRegion.Active).SelectMany(g=>GeneralCatalog.Get(g.GeneralId).Skills.Select(s=>tk.Clock.PhaseId+":"+g.TenureId+":"+s.Id)).ToArray()};return tk;
    }
    private static dynamic Fixture(string mode,string[] generals,Action<dynamic>? configure=null)
    {
        if(mode=="cities")return M4CoreHarness.Fixture(s=>{s.RulesVersion=M4.CitiesKnightsGameSession.ThreeKingdomsB4RulesVersion;s.ThreeKingdoms=Generals(generals);configure?.Invoke(s);});
        return M5Harness.Fixture(s=>{s.RulesVersion=M5.CombinedGameSession.ThreeKingdomsB4RulesVersion;s.ThreeKingdoms=Generals(generals);configure?.Invoke(s);});
    }
    private static dynamic Player(dynamic s,string id="P1")=>s is M4.GameState m4?(object)m4.Players.Single(p=>p.Id==id):((M5.GameState)s).Players.Single(p=>p.Id==id);
    private static dynamic Command(dynamic game,string mode,string kind,string player="P1")
    {dynamic c=mode=="cities"?(object)new M4.Command{Kind=Enum.Parse<M4.CommandKind>(kind),PlayerId=player}:new M5.Command{Kind=Enum.Parse<M5.CommandKind>(kind),PlayerId=player};c.Id=ThreeKingdomsTestGame.NextId();c.WindowId=game.GetPlayerView(player).ThreeKingdoms.WindowId;return c;}
    private static void Accept(dynamic game,dynamic c){dynamic r=game.Execute(c);Assert.True((bool)r.Success,(string)r.ErrorCode+":"+(string)r.Message);game.AssertInvariants();}
    private static void Skill(dynamic game,string mode,string player,string option,Func<B2SkillAction,bool>? predicate=null)
    {B2SkillAction[] actions=game.GetPlayerView(player).ThreeKingdoms.SkillActions;var a=actions.First(a=>a.Command.Option==option&&(predicate==null||predicate(a)));dynamic c=Command(game,mode,"UseGeneralSkill",player);c.SkillCommand=a.Command;Accept(game,c);}
    private static void Set(dynamic game,dynamic state)
    {if(state is M4.GameState a)M4CoreHarness.SetState((M4.CitiesKnightsGameSession)game,a);else M5Harness.SetState((M5.CombinedGameSession)game,(M5.GameState)state);}
    private static void Invoke(dynamic game,string method,dynamic state)=>game.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(game,new object[]{state});
    private static dynamic Restore(dynamic game,string mode)
    {
        object s=game.GetAuthoritativeStateForTesting();using var stream=new MemoryStream();var serializer=new DataContractJsonSerializer(s.GetType());serializer.WriteObject(stream,s);stream.Position=0;var copy=serializer.ReadObject(stream)!;
        object restored=mode=="cities"?(object)M4CoreHarness.New():M5Harness.New();restored.GetType().GetField("state",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(restored,copy);return restored;
    }
    private static void DeclineAutomatic(dynamic game,string mode)
    {
        for(int i=0;i<10;i++){ThreeKingdomsState tk=game.GetAuthoritativeStateForTesting().ThreeKingdoms;if(tk.Automation?.PendingDecision==null)return;Skill((object)game,mode,tk.Automation.PendingDecision.PlayerId,"automation-skip");}
        Assert.Fail("主动提示未结束。");
    }
    private static void ForbidLoss(ThreeKingdomsState tk,string player) => tk.Skills.TimedEffects=new[]{new B2TimedEffect{Id="fixture-indulgence",Kind="response-indulgence-active",PlayerId="P4",TargetPlayerId=player,CreatedTurnId=tk.Clock.TurnInstanceId}};

    [Theory][InlineData("cities")][InlineData("combined")]
    public void LiuliUsesNativeRobberMovementAndKeepsBankConserved(string mode)
    {
        dynamic game=Fixture(mode,new[]{"tk-g05","tk-g12","tk-g09","tk-g22"},s=>{s.FirstBarbarianAttack=true;if(s is M4.GameState a)a.RobberTileId=a.Tiles.First(t=>t.Resource!="sea").Id;else{var b=(M5.GameState)s;b.RobberTileId=b.Tiles.First(t=>t.Resource!="sea").Id;b.PirateTileId=b.Tiles.First(t=>t.Resource=="sea").Id;}Player(s).Resources.Wheat=1;});
        B2SkillAction[] actions=game.GetPlayerView("P1").ThreeKingdoms.SkillActions;var move=actions.First(a=>a.Command.Option=="response-liuli");
        string target=move.Label.Split(' ').Last();string before=game.Save();dynamic c=Command(game,mode,"UseGeneralSkill");c.SkillCommand=move.Command;
        Assert.True((bool)game.Preview(c).Success);Assert.Equal(before,(string)game.Save());Accept(game,c);
        Assert.Equal(target,(string)game.GetAuthoritativeStateForTesting().RobberTileId);Assert.Equal(0,(int)Player(game.GetAuthoritativeStateForTesting()).Resources.Wheat);Assert.Equal(19,(int)game.GetAuthoritativeStateForTesting().Bank.Wheat);
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void GuoseAutomaticallyJudgesNextTurnAndBlocksNativeBankPayment(string mode)
    {
        dynamic game=Fixture(mode,new[]{"tk-g05","tk-g12","tk-g09","tk-g22"},s=>{Player(s).Resources.Brick=2;Player(s,"P2").Resources.Wood=4;});
        Skill((object)game,mode,"P1","response-guose",a=>a.Command.TargetPlayerIds[0]=="P2");dynamic state=game.GetAuthoritativeStateForTesting();state.Random.State=(uint)1;Set(game,state);
        Accept(game,Command(game,mode,"EndTurn"));ThreeKingdomsState tk=game.GetAuthoritativeStateForTesting().ThreeKingdoms;
        Assert.Equal("P2",tk.Clock.ActivePlayerId);Assert.Equal(ThreeKingdomsPhase.Play,tk.Clock.Phase);Assert.Null(tk.Response?.JudgmentSource);Assert.True(B2SkillEngine.IsB4HandLossForbidden(tk,"P2"));
        dynamic trade=Command(game,mode,"BankTrade","P2");trade.GiveResource=Resource.Wood;trade.ReceiveResource=Resource.Wool;string before=game.Save();Assert.False((bool)game.Execute(trade).Success);Assert.Equal(before,(string)game.Save());
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void IndulgenceRejectsCommercialHarborResourcePaymentWithoutMutation(string mode)
    {
        dynamic game=Fixture(mode,new[]{"tk-g12","tk-g22","tk-g09","tk-g05"},s=>{Player(s).Resources.Wood=1;Player(s,"P2").Commodities.Cloth=1;s.CommercialHarborPlayers=new[]{"P2"};ForbidLoss(s.ThreeKingdoms,"P1");});
        dynamic c=Command(game,mode,"CommercialHarborOffer");c.OtherPlayerId="P2";c.Resource=Resource.Wood;string before=game.Save();Assert.False((bool)game.Preview(c).Success);Assert.False((bool)game.Execute(c).Success);Assert.Equal(before,(string)game.Save());
    }

    [Theory]
    [InlineData("cities","ProgressHarbor")][InlineData("combined","ProgressHarbor")]
    [InlineData("cities","ProgressWedding")][InlineData("combined","ProgressWedding")]
    [InlineData("cities","ProgressSabotage")][InlineData("combined","ProgressSabotage")]
    [InlineData("cities","ProgressGuildDues")][InlineData("combined","ProgressGuildDues")]
    [InlineData("cities","ProgressEspionage")][InlineData("combined","ProgressEspionage")]
    public void IndulgenceSkipsRestoredNativeHandLossWithoutExposingPrivateCards(string mode,string kind)
    {
        string owner=kind=="ProgressGuildDues"||kind=="ProgressEspionage"?"P2":"P1";
        dynamic game=Fixture(mode,new[]{"tk-g12","tk-g22","tk-g09","tk-g05"},s=>
        {
            Player(s).Resources.Wood=2;Player(s).Resources.Brick=1;Player(s).Commodities.Coin=1;ForbidLoss(s.ThreeKingdoms,"P1");
            if(s is M4.GameState a){a.Players[0].ProgressCards=new[]{M4.ProgressCardKind.Irrigation};a.Phase=M4.GamePhase.PendingChoice;a.DecisionReturnPhase=M4.GamePhase.Action;a.PendingDecision=new M4.PendingDecision{Kind=kind,PlayerId=owner,OtherPlayerId=owner=="P1"?"P2":"P1",Amount=1,ReturnPhase=M4.GamePhase.Action};}
            else{var b=(M5.GameState)s;b.Players[0].ProgressCards=new[]{M5.ProgressCardKind.Irrigation};b.Phase=M5.GamePhase.PendingChoice;b.DecisionReturnPhase=M5.GamePhase.Action;b.PendingDecision=new M5.PendingDecision{Kind=kind,PlayerId=owner,OtherPlayerId=owner=="P1"?"P2":"P1",Amount=1,ReturnPhase=M5.GamePhase.Action};}
        });
        foreach(string player in new[]{"P1","P2","P3","P4"})
        {dynamic view=game.GetPlayerView(player);Assert.Empty((Array)view.PrivateTargetProgressCards);Assert.True(view.PrivateTargetResources==null||(int)view.PrivateTargetResources.Total==0);Assert.True(view.PrivateTargetCommodities==null||(int)view.PrivateTargetCommodities.Total==0);}
        dynamic copy=Restore(game,mode);dynamic c=Command(game,mode,"ResolveProgressChoice",owner);Accept(game,c);Accept(copy,c);Assert.Equal((string)game.Save(),(string)copy.Save());
        dynamic state=game.GetAuthoritativeStateForTesting();Assert.Null(state.PendingDecision);Assert.Equal(3,(int)Player(state).Resources.Total);Assert.Equal(1,(int)Player(state).Commodities.Coin);Assert.Equal("Irrigation",((Array)Player(state).ProgressCards).GetValue(0)!.ToString());Assert.Equal(0,(int)Player(state,"P2").Resources.Total);Assert.Empty((Array)Player(state,"P2").ProgressCards);
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void GuhuoBankDeclarationReservesEntirePaymentAndRestoresEachPrivateReply(string mode)
    {
        dynamic game=Fixture(mode,new[]{"tk-g07","tk-g12","tk-g09","tk-g22"},s=>Player(s).Resources.Wood=4);
        dynamic command=Command(game,mode,"BankTrade");command.GiveResource=Resource.Wood;command.GiveEffectiveCardType=1;command.GiveAmount=4;command.ReceiveResource=Resource.Ore;command.ReceiveAmount=1;
        command.DeclaredRealTypes=new[]{0,0,0,0};command.DeclaredEffectiveTypes=new[]{1,1,1,1};Accept(game,command);
        Assert.Equal(4,(int)Player(game.GetAuthoritativeStateForTesting()).Resources.Wood);
        for(int card=0;card<4;card++)foreach(string player in new[]{"P2","P3","P4"})
        {
            dynamic copy=Restore(game,mode);B2SkillAction[] actions=game.GetPlayerView(player).ThreeKingdoms.SkillActions;var choice=actions.Single(a=>a.Command.Option=="response-trust");
            dynamic c=Command(game,mode,"UseGeneralSkill",player);c.SkillCommand=choice.Command;Accept(game,c);Accept(copy,c);Assert.Equal((string)game.Save(),(string)copy.Save());
        }
        Assert.Equal(0,(int)Player(game.GetAuthoritativeStateForTesting()).Resources.Wood);Assert.Equal(1,(int)Player(game.GetAuthoritativeStateForTesting()).Resources.Ore);Assert.Equal(19,(int)game.GetAuthoritativeStateForTesting().Bank.Wood);
        Assert.Null(game.GetAuthoritativeStateForTesting().ThreeKingdoms.Response.Declaration);
        ThreeKingdomsState tk=game.GetAuthoritativeStateForTesting().ThreeKingdoms;
        var history=Assert.Single(tk.SkillHistory,e=>e.SkillId=="tk-g07-s1");
        Assert.Contains(history.RelatedCardFlowIds,id=>id.StartsWith("guhuo-payment-",StringComparison.Ordinal));
        Assert.Contains("P1失去4张",history.PublicResult);
        Assert.DoesNotContain(((GameHistoryEntry[])game.GetHistory("P3",0)).SelectMany(e=>e.Changes),c=>c.GoodIndex==0);
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void GuhuoFailedClaimKeepsUnusedRealCardsAndCancelsBankReceipt(string mode)
    {
        dynamic game=Fixture(mode,new[]{"tk-g07","tk-g12","tk-g09","tk-g22"},s=>Player(s).Resources.Wood=4);
        dynamic command=Command(game,mode,"BankTrade");command.GiveResource=Resource.Wood;command.GiveEffectiveCardType=1;command.ReceiveResource=Resource.Ore;command.DeclaredRealTypes=new[]{0,0,0,0};command.DeclaredEffectiveTypes=new[]{1,1,1,1};Accept(game,command);
        Skill((object)game,mode,"P2","response-challenge");Skill((object)game,mode,"P3","response-trust");Skill((object)game,mode,"P4","response-trust");
        Assert.Equal(3,(int)Player(game.GetAuthoritativeStateForTesting()).Resources.Wood);Assert.Equal(0,(int)Player(game.GetAuthoritativeStateForTesting()).Resources.Ore);Assert.Equal(1,(int)Player(game.GetAuthoritativeStateForTesting(),"P2").Resources.Total);
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void GuhuoPlayerOfferHidesRealPaymentAndRestoredAcceptanceTransfersPhysicalCard(string mode)
    {
        dynamic game=Fixture(mode,new[]{"tk-g07","tk-g12","tk-g09","tk-g22"},s=>{Player(s).Resources.Wood=1;Player(s,"P2").Resources.Wool=1;});
        dynamic offer=Command(game,mode,"ProposeTrade");offer.OtherPlayerId="P2";offer.Give=new ResourceBag{Wood=1};offer.Receive=new ResourceBag{Wool=1};offer.DeclaredRealTypes=new[]{0};offer.DeclaredEffectiveTypes=new[]{1};Accept(game,offer);
        Assert.DoesNotContain(((GameHistoryEntry[])game.GetHistory("P3",0)).SelectMany(e=>e.Changes),c=>c.Category=="offer");
        foreach(string player in new[]{"P2","P3","P4"})Skill((object)game,mode,player,"response-trust");
        foreach(string player in new[]{"P1","P2","P3","P4"})
        {
            dynamic view=game.GetPlayerView(player);Assert.Equal(1,(int)view.TradeOffer.Give.Brick);Assert.Equal(0,(int)view.TradeOffer.Give.Wood);Assert.Null(view.TradeOffer.B4RealGive);
        }
        Assert.Equal(1,(int)Player(game.GetAuthoritativeStateForTesting()).Resources.Wood);Assert.Equal(1,((int[])game.GetAuthoritativeStateForTesting().TradeOffer.B4RealGive)[0]);
        dynamic restored=Restore(game,mode);dynamic accept=Command(game,mode,"AcceptTrade","P2");Accept(game,accept);Accept(restored,accept);Assert.Equal((string)game.Save(),(string)restored.Save());
        Assert.Equal(0,(int)Player(game.GetAuthoritativeStateForTesting()).Resources.Wood);Assert.Equal(1,(int)Player(game.GetAuthoritativeStateForTesting()).Resources.Wool);
        Assert.Equal(1,(int)Player(game.GetAuthoritativeStateForTesting(),"P2").Resources.Wood);Assert.Equal(0,(int)Player(game.GetAuthoritativeStateForTesting(),"P2").Resources.Brick);Assert.Equal(0,(int)Player(game.GetAuthoritativeStateForTesting(),"P2").Resources.Wool);
        ThreeKingdomsState tk=game.GetAuthoritativeStateForTesting().ThreeKingdoms;
        var history=Assert.Single(tk.SkillHistory,e=>e.SkillId=="tk-g07-s1");
        Assert.Contains("native-"+(string)accept.Id,history.RelatedCardFlowIds);
        var observer=((GameHistoryEntry[])game.GetHistory("P3",0)).SelectMany(e=>e.Changes).ToArray();
        Assert.Contains(observer,c=>c.Category=="offer"&&c.GoodIndex==1);
        Assert.DoesNotContain(observer,c=>c.GoodIndex==0);
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void CancellingGuhuoPlayerOfferReleasesReservedCardWithoutDiscard(string mode)
    {
        dynamic game=Fixture(mode,new[]{"tk-g07","tk-g12","tk-g09","tk-g22"},s=>{Player(s).Resources.Wood=1;Player(s,"P2").Resources.Wool=1;});
        dynamic offer=Command(game,mode,"ProposeTrade");offer.OtherPlayerId="P2";offer.Give=new ResourceBag{Wood=1};offer.Receive=new ResourceBag{Wool=1};offer.DeclaredRealTypes=new[]{0};offer.DeclaredEffectiveTypes=new[]{1};Accept(game,offer);
        foreach(string player in new[]{"P2","P3","P4"})Skill((object)game,mode,player,"response-trust");
        Accept(game,Command(game,mode,"CancelTrade"));Assert.Null(game.GetAuthoritativeStateForTesting().TradeOffer);Assert.Equal(1,(int)Player(game.GetAuthoritativeStateForTesting()).Resources.Wood);Assert.Equal(1,(int)Player(game.GetAuthoritativeStateForTesting(),"P2").Resources.Wool);Assert.Equal(18,(int)game.GetAuthoritativeStateForTesting().Bank.Wood);
    }

    [Theory][InlineData("cities","tk-g42",1)][InlineData("combined","tk-g42",1)][InlineData("cities","tk-g44",7)][InlineData("combined","tk-g44",7)]
    public void ConvertedPlayerOfferIsPubliclyEffectiveButRestoresAndTransfersOnlyPhysicalCard(string mode,string general,int realType)
    {
        dynamic game=Fixture(mode,new[]{general,"tk-g12","tk-g09","tk-g22"},s=>{if(realType==1)Player(s).Resources.Brick=1;else Player(s).Commodities.Paper=1;Player(s,"P2").Resources.Wool=1;});
        var payment=new int[8];payment[realType]=1;dynamic offer=Command(game,mode,"ProposeTrade");offer.OtherPlayerId="P2";offer.Give=new ResourceBag{Wood=1};offer.Receive=new ResourceBag{Wool=1};offer.PaymentCards=payment;Accept(game,offer);
        foreach(string player in new[]{"P1","P2","P3","P4"}){dynamic view=game.GetPlayerView(player);Assert.Equal(1,(int)view.TradeOffer.Give.Wood);Assert.Null(view.TradeOffer.B4RealGive);}
        Assert.Equal(1,realType==1?(int)Player(game.GetAuthoritativeStateForTesting()).Resources.Brick:(int)Player(game.GetAuthoritativeStateForTesting()).Commodities.Paper);
        dynamic copy=Restore(game,mode);dynamic accept=Command(game,mode,"AcceptTrade","P2");Accept(game,accept);Accept(copy,accept);Assert.Equal((string)game.Save(),(string)copy.Save());
        ThreeKingdomsState linked=game.GetAuthoritativeStateForTesting().ThreeKingdoms;
        Assert.Contains(linked.SkillHistory,e=>e.PlayerId=="P1"&&e.GeneralId==general&&e.RelatedCardFlowIds.Contains("native-"+(string)accept.Id));
        dynamic state=game.GetAuthoritativeStateForTesting();Assert.Equal(1,realType==1?(int)Player(state,"P2").Resources.Brick:(int)Player(state,"P2").Commodities.Paper);Assert.Equal(0,(int)Player(state,"P2").Resources.Wood);Assert.Equal(1,(int)Player(state).Resources.Wool);
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void MiewuTradeHistoryIdsDoNotEncodeRealOrEffectiveCardTypes(string mode)
    {
        var publicIds=new List<string>();
        foreach(var choice in new[]{(Real:1,Effective:0),(Real:4,Effective:0),(Real:4,Effective:1)})
        {
            dynamic game=Fixture(mode,new[]{"tk-g41","tk-g12","tk-g09","tk-g22"},s=>
            {
                ResourceBag hand=Player(s).Resources;hand[(Resource)choice.Real]=1;Player(s,"P2").Resources.Wool=1;
                ThreeKingdomsState tk=s.ThreeKingdoms;tk.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P1").Counters=new[]{new GeneralCounter{Key="sanchen",Value=1},new GeneralCounter{Key="wuku",Value=1}};
            });
            dynamic offer=Command(game,mode,"ProposeTrade");offer.Id="conversion-privacy-offer";offer.OtherPlayerId="P2";
            var uses=new ResourceBag();uses[(Resource)choice.Effective]=1;offer.Give=uses;offer.Receive=new ResourceBag{Wool=1};
            var payment=new int[8];payment[choice.Real]=1;offer.PaymentCards=payment;Accept(game,offer);
            dynamic observed=game.GetPlayerView("P3");Assert.Null(observed.TradeOffer.B4RealGive);Assert.False((bool)observed.TradeOffer.B4ConversionPayment);Assert.Null(observed.TradeOffer.B4DeclarationHistoryEventId);
            dynamic restored=Restore(game,mode);dynamic accept=Command(game,mode,"AcceptTrade","P2");accept.Id="conversion-privacy-accept";
            Accept(game,accept);Accept(restored,accept);Assert.Equal((string)game.Save(),(string)restored.Save());
            ThreeKingdomsState authority=game.GetAuthoritativeStateForTesting().ThreeKingdoms;
            var actual=Assert.Single(authority.SkillHistory,e=>e.SkillId=="tk-g41-s3");
            Assert.Equal("trade-conversion-conversion-privacy-accept:conversion-0",actual.EventId);
            Assert.Contains("native-conversion-privacy-accept",actual.RelatedCardFlowIds);
            B4SkillHistoryView[] projected=game.GetPlayerView("P3").ThreeKingdoms.SkillHistory;
            Assert.Equal(actual.EventId,Assert.Single(projected,e=>e.SkillId=="tk-g41-s3").EventId);
            Assert.Equal(actual.EventId,Assert.Single((GameHistoryEntry[])game.GetHistory("P3",0),e=>e.SkillId=="tk-g41-s3").SkillEventId);
            string settled=game.Save();Assert.True((bool)game.Execute(accept).IsDuplicate);Assert.Equal(settled,(string)game.Save());
            publicIds.Add(actual.EventId);
        }
        Assert.Single(publicIds.Distinct());
    }

    [Theory][InlineData("cities",false)][InlineData("combined",false)][InlineData("cities",true)][InlineData("combined",true)]
    public void MiewuTradeSpendsMarkAndRewardsOnlyOnAcceptance(string mode,bool cancel)
    {
        dynamic game=Fixture(mode,new[]{"tk-g41","tk-g12","tk-g09","tk-g22"},s=>
        {
            Player(s).Resources.Brick=1;Player(s,"P2").Resources.Wool=1;ThreeKingdomsState tk=s.ThreeKingdoms;
            tk.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P1").Counters=new[]{new GeneralCounter{Key="sanchen",Value=1},new GeneralCounter{Key="wuku",Value=1}};
        });
        dynamic offer=Command(game,mode,"ProposeTrade");offer.OtherPlayerId="P2";offer.Give=new ResourceBag{Wood=1};offer.Receive=new ResourceBag{Wool=1};offer.PaymentCards=new[]{0,1,0,0,0,0,0,0};Accept(game,offer);
        ThreeKingdomsState tk=game.GetAuthoritativeStateForTesting().ThreeKingdoms;Assert.Equal(1,tk.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P1").Counters.Single(c=>c.Key=="wuku").Value);Assert.Empty(tk.SkillHistory??Array.Empty<B4SkillHistoryEvent>());
        dynamic copy=Restore(game,mode);dynamic c=Command(game,mode,cancel?"CancelTrade":"AcceptTrade",cancel?"P1":"P2");Accept(game,c);Accept(copy,c);Assert.Equal((string)game.Save(),(string)copy.Save());
        dynamic state=game.GetAuthoritativeStateForTesting();tk=state.ThreeKingdoms;var g=tk.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P1");Assert.Equal(cancel?1:0,g.Counters.Single(c=>c.Key=="wuku").Value);Assert.Equal(cancel?1:2,(int)Player(state).Resources.Total);Assert.Equal(cancel?0:1,(int)Player(state,"P2").Resources.Brick);
        if(cancel)Assert.DoesNotContain(g.Counters,c=>c.Key=="miewu"&&c.Value>0);else Assert.Equal(1,g.Counters.Single(c=>c.Key=="miewu").Value);
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void TuntianAfterNativeOutOfTurnTradeJudgesOnceAndObservesQinqin(string mode)
    {
        dynamic game=Fixture(mode,new[]{"tk-g12","tk-g49","tk-g43","tk-g22"},s=>{Player(s).Resources.Wood=1;Player(s,"P2").Resources.Wool=1;s.Random.State=(uint)2;});
        int finalBefore=game.GetAuthoritativeStateForTesting().LastDice1+game.GetAuthoritativeStateForTesting().LastDice2;
        dynamic offer=Command(game,mode,"ProposeTrade");offer.OtherPlayerId="P2";offer.Give=new ResourceBag{Wood=1};offer.Receive=new ResourceBag{Wool=1};Accept(game,offer);
        dynamic restored=Restore(game,mode);dynamic accept=Command(game,mode,"AcceptTrade","P2");Accept(game,accept);Accept(restored,accept);Assert.Equal((string)game.Save(),(string)restored.Save());
        ThreeKingdomsState tk=game.GetAuthoritativeStateForTesting().ThreeKingdoms;Assert.Null(tk.Response?.JudgmentSource);Assert.Equal(finalBefore,(int)(game.GetAuthoritativeStateForTesting().LastDice1+game.GetAuthoritativeStateForTesting().LastDice2));
        Assert.Equal(3,tk.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P2").Counters.Single(c=>c.Key=="tian").Value);
        Assert.Single(tk.SkillHistory,e=>e.SkillId=="tk-g49-s1");Assert.Equal(1,(int)Player(game.GetAuthoritativeStateForTesting(),"P3").Resources.Total);
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void JianxiongCapturesPhysicalNativeProgressAfterEffectAndNotDiscard(string mode)
    {
        dynamic game=Fixture(mode,new[]{"tk-g12","tk-g14","tk-g09","tk-g22"},s=>{if(s is M4.GameState a)a.Players[0].ProgressCards=new[]{M4.ProgressCardKind.Mining,M4.ProgressCardKind.Crane,M4.ProgressCardKind.Irrigation,M4.ProgressCardKind.Medicine,M4.ProgressCardKind.RoadBuilding,M4.ProgressCardKind.Alchemy};else ((M5.GameState)s).Players[0].ProgressCards=new[]{M5.ProgressCardKind.Mining,M5.ProgressCardKind.Crane,M5.ProgressCardKind.Irrigation,M5.ProgressCardKind.Medicine,M5.ProgressCardKind.RoadBuilding,M5.ProgressCardKind.Alchemy};});
        dynamic c=Command(game,mode,"PlayProgressCard");if(c is M4.Command c4)c4.ProgressCard=M4.ProgressCardKind.Mining;else((M5.Command)c).ProgressCard=M5.ProgressCardKind.Mining;Accept(game,c);
        Assert.Equal("Mining",((Array)Player(game.GetAuthoritativeStateForTesting(),"P2").ProgressCards).GetValue(0)!.ToString());
        dynamic discard=Command(game,mode,"DiscardProgressCard");if(discard is M4.Command d4)d4.ProgressCard=M4.ProgressCardKind.Crane;else((M5.Command)discard).ProgressCard=M5.ProgressCardKind.Crane;Accept(game,discard);
        Assert.Equal(1,(int)Player(game.GetAuthoritativeStateForTesting(),"P2").ProgressCards.Length);Assert.Empty((Array)game.GetAuthoritativeStateForTesting().ThreeKingdomsProgressResolutions);
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void JianxiongResidualDeclinesThenRestoresPendingCaptureWithPhysicalEscrow(string mode)
    {
        dynamic game=Fixture(mode,new[]{"tk-g12","tk-g14","tk-g09","tk-g22"},s=>
        {
            if(s is M4.GameState a)a.Players[0].ProgressCards=new[]{M4.ProgressCardKind.Mining,M4.ProgressCardKind.Irrigation};else ((M5.GameState)s).Players[0].ProgressCards=new[]{M5.ProgressCardKind.Mining,M5.ProgressCardKind.Irrigation};
            ThreeKingdomsState tk=s.ThreeKingdoms;var g=tk.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P2");B2SkillEngine.OnB4GeneralRetired(tk,g);
            var other=tk.Generals.Single(g=>g.GeneralId=="tk-g05");(g.GeneralId,other.GeneralId)=(other.GeneralId,g.GeneralId);(g.InstanceId,other.InstanceId)=(other.InstanceId,g.InstanceId);g.TenureId="fixture-next-tenure";g.Counters=Array.Empty<GeneralCounter>();B2SkillEngine.OnB4GeneralActivated(tk,g);
        });
        dynamic c=Command(game,mode,"PlayProgressCard");if(c is M4.Command c4)c4.ProgressCard=M4.ProgressCardKind.Mining;else((M5.Command)c).ProgressCard=M5.ProgressCardKind.Mining;Accept(game,c);
        Assert.Single((Array)game.GetAuthoritativeStateForTesting().ThreeKingdomsProgressResolutions);Skill((object)game,mode,"P2","skip");Assert.Empty((Array)game.GetAuthoritativeStateForTesting().ThreeKingdomsProgressResolutions);Assert.Single((B4CaoResidual[])game.GetAuthoritativeStateForTesting().ThreeKingdoms.Response.CaoResiduals);
        c=Command(game,mode,"PlayProgressCard");if(c is M4.Command c42)c42.ProgressCard=M4.ProgressCardKind.Irrigation;else((M5.Command)c).ProgressCard=M5.ProgressCardKind.Irrigation;Accept(game,c);
        dynamic copy=Restore(game,mode);B2SkillAction[] actions=game.GetPlayerView("P2").ThreeKingdoms.SkillActions;dynamic confirm=Command(game,mode,"UseGeneralSkill","P2");confirm.SkillCommand=actions.Single(a=>a.Command.Option=="accept").Command;Accept(game,confirm);Accept(copy,confirm);Assert.Equal((string)game.Save(),(string)copy.Save());
        Assert.Equal("Irrigation",((Array)Player(game.GetAuthoritativeStateForTesting(),"P2").ProgressCards).GetValue(0)!.ToString());Assert.Empty((B4CaoResidual[])game.GetAuthoritativeStateForTesting().ThreeKingdoms.Response.CaoResiduals);Assert.Empty((Array)game.GetAuthoritativeStateForTesting().ThreeKingdomsProgressResolutions);
    }

    [Theory][InlineData("cities","Printing")][InlineData("combined","Printing")][InlineData("cities","Constitution")][InlineData("combined","Constitution")]
    public void JianxiongCapturesAutomaticallyRevealedVictoryCardBeforeCheckingWinner(string mode,string name)
    {
        dynamic game=Fixture(mode,new[]{"tk-g12","tk-g14","tk-g09","tk-g22"},s=>
        {
            Player(s).DefenderPoints=(int)s.TargetVictoryPoints-1;
            if(s is M4.GameState a){a.Phase=M4.GamePhase.PendingChoice;a.DecisionReturnPhase=M4.GamePhase.Action;a.PendingDecision=new M4.PendingDecision{Kind="DefenderProgress",PlayerId="P1",ReturnPhase=M4.GamePhase.Action};}
            else{var b=(M5.GameState)s;b.Phase=M5.GamePhase.PendingChoice;b.DecisionReturnPhase=M5.GamePhase.Action;b.PendingDecision=new M5.PendingDecision{Kind="DefenderProgress",PlayerId="P1",ReturnPhase=M5.GamePhase.Action};}
        });
        dynamic state=game.GetAuthoritativeStateForTesting();dynamic c=Command(game,mode,"ResolveChoice");
        if(state is M4.GameState s4){var card=Enum.Parse<M4.ProgressCardKind>(name);var track=M4.ProgressCatalog.Track(card);var deck=s4.ProgressDecks.Single(d=>d.Track==track);deck.Cards=new[]{card}.Concat(deck.Cards.Where(p=>p!=card)).ToArray();((M4.Command)c).Track=track;}
        else{var s5=(M5.GameState)state;var card=Enum.Parse<M5.ProgressCardKind>(name);var track=M5.ProgressCatalog.Track(card);var deck=s5.ProgressDecks.Single(d=>d.Track==track);deck.Cards=new[]{card}.Concat(deck.Cards.Where(p=>p!=card)).ToArray();((M5.Command)c).Track=track;}
        Set(game,state);Accept(game,c);state=game.GetAuthoritativeStateForTesting();Assert.Null(state.WinnerPlayerId);Assert.Empty((Array)Player(state).ProgressVictoryCards);Assert.Equal(name,((Array)Player(state,"P2").ProgressVictoryCards).GetValue(0)!.ToString());Assert.Empty((Array)Player(state,"P2").ProgressCards);
        foreach(string player in new[]{"P1","P2","P3","P4"}){dynamic view=game.GetPlayerView(player);foreach(dynamic pub in (Array)view.Players)if((string)pub.Id=="P2")Assert.Equal(name,((Array)pub.ProgressVictoryCards).GetValue(0)!.ToString());}
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void QiceVirtualNativeEffectPaysWholeHandAndCannotBeCaptured(string mode)
    {
        dynamic game=Fixture(mode,new[]{"tk-g34","tk-g14","tk-g12","tk-g22"},s=>{Player(s).Resources.Wood=2;s.ThreeKingdoms.Clock.Phase=ThreeKingdomsPhase.Preparation;s.ThreeKingdoms.Clock.DrawPhaseTurnInstanceId=(long)0;if(s is M4.GameState a)a.Phase=M4.GamePhase.ProductionAwaitRoll;else((M5.GameState)s).Phase=M5.GamePhase.ProductionAwaitRoll;});
        dynamic state=game.GetAuthoritativeStateForTesting();Invoke(game,"PumpB4",state);Set(game,state);Skill((object)game,mode,"P1","accept");Skill((object)game,mode,"P1","response-qice-use",a=>a.VirtualCardId=="Mining");
        Assert.Equal(0,(int)Player(game.GetAuthoritativeStateForTesting()).Resources.Total);Assert.Equal(0,(int)Player(game.GetAuthoritativeStateForTesting(),"P2").ProgressCards.Length);game.AssertInvariants();
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void PoxiOneSelfDiscardEntersNormalEndAndStillOffersJieying(string mode)
    {
        dynamic game=Fixture(mode,new[]{"tk-g08","tk-g12","tk-g09","tk-g22"},s=>{Player(s).Resources.Wood=1;Player(s,"P2").Resources.Brick=1;Player(s,"P2").Resources.Wool=1;Player(s,"P2").Resources.Wheat=1;});
        Skill((object)game,mode,"P1","response-poxi",a=>a.Command.TargetPlayerIds[0]=="P2");
        for(int type=0;type<4;type++){int current=type;Skill((object)game,mode,"P1","response-poxi-select",a=>a.Command.CardType==current);}
        Skill((object)game,mode,"P1","response-poxi-confirm");ThreeKingdomsState tk=game.GetAuthoritativeStateForTesting().ThreeKingdoms;Assert.Equal(ThreeKingdomsPhase.End,tk.Clock.Phase);Assert.Equal("tk-g08-s1",tk.Skills.Frames[0].SkillId);Assert.Equal("optional",tk.Skills.PendingDecision.Kind);
    }
}
