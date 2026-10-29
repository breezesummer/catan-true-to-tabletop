using System.Reflection;
using System.Runtime.Serialization.Json;
using Catan.Core;
using Catan.Core.ThreeKingdoms;
using M4=Catan.Core.M4;
using M5=Catan.Core.M5;
using Xunit;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB5CivilIntegrationTests
{
    private static ThreeKingdomsState Generals(params string[] requested)
    {
        var tk=ThreeKingdomsEngine.Create(new[]{"P1","P2","P3","P4"},42,_=>0,true,GeneralCatalog.B5PackageVersion);
        while(tk.PendingDecision!=null){var d=tk.PendingDecision;if(d.Kind==ThreeKingdomsDecisionKind.SelectOpening)ThreeKingdomsEngine.ChooseGenerals(tk,d.PlayerId,d.CandidateIds.Take(2).ToArray(),d.DecisionId,d.WindowId,_=>0);else ThreeKingdomsEngine.DeployGeneral(tk,d.PlayerId,d.CandidateIds[0],d.DecisionId,d.WindowId,_=>0);}
        ThreeKingdomsEngine.BeginNormalTurns(tk);tk.Clock.Phase=ThreeKingdomsPhase.Play;tk.Clock.PhaseId=5;tk.Clock.DrawPhaseTurnInstanceId=tk.Clock.TurnInstanceId;
        for(int i=0;i<requested.Length;i++){var g=tk.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P"+(i+1));var other=tk.Generals.Single(g=>g.GeneralId==requested[i]);(g.GeneralId,other.GeneralId)=(other.GeneralId,g.GeneralId);(g.InstanceId,other.InstanceId)=(other.InstanceId,g.InstanceId);g.Counters=Array.Empty<GeneralCounter>();}
        tk.Skills=new B2SkillState();tk.CivilB5=new B5CivilState();tk.Board=new B5BoardState();tk.Response=null;
        tk.Automation=new B4AutomationState {TurnId=tk.Clock.TurnInstanceId,Asked=tk.Generals.Where(g=>g.Region==GeneralRegion.Active).SelectMany(g=>GeneralCatalog.Get(g.GeneralId).Skills.Select(s=>tk.Clock.PhaseId+":"+g.TenureId+":"+s.Id)).ToArray()};return tk;
    }
    private static dynamic Fixture(string mode,string[] generals,Action<dynamic>? configure=null)
    {if(mode=="cities")return M4CoreHarness.Fixture(s=>{s.RulesVersion=M4.CitiesKnightsGameSession.ThreeKingdomsB5RulesVersion;s.ThreeKingdoms=Generals(generals);configure?.Invoke(s);});return M5Harness.Fixture(s=>{s.RulesVersion=M5.CombinedGameSession.ThreeKingdomsB5RulesVersion;s.ThreeKingdoms=Generals(generals);configure?.Invoke(s);});}
    private static dynamic Player(dynamic s,string id="P1")=>s is M4.GameState m4?(object)m4.Players.Single(p=>p.Id==id):((M5.GameState)s).Players.Single(p=>p.Id==id);
    private static dynamic Command(dynamic game,string mode,string kind,string player="P1")
    {dynamic c=mode=="cities"?(object)new M4.Command{Kind=Enum.Parse<M4.CommandKind>(kind),PlayerId=player}:new M5.Command{Kind=Enum.Parse<M5.CommandKind>(kind),PlayerId=player};c.Id=ThreeKingdomsTestGame.NextId();c.WindowId=game.GetPlayerView(player).ThreeKingdoms.WindowId;return c;}
    private static void Accept(dynamic game,dynamic c){dynamic r=game.Execute(c);Assert.True((bool)r.Success,(string)r.ErrorCode+":"+(string)r.Message);game.AssertInvariants();}
    private static void Skill(dynamic game,string mode,string player,string option,Func<B2SkillAction,bool>? predicate=null)
    {B2SkillAction[] actions=game.GetPlayerView(player).ThreeKingdoms.SkillActions;var a=actions.First(a=>a.Command.Option==option&&(predicate==null||predicate(a)));dynamic c=Command(game,mode,"UseGeneralSkill",player);c.SkillCommand=a.Command;Accept(game,c);}
    private static void SameSkill(dynamic game,dynamic restored,string mode,string player,string option)
    {B2SkillAction[] actions=game.GetPlayerView(player).ThreeKingdoms.SkillActions;dynamic c=Command(game,mode,"UseGeneralSkill",player);c.SkillCommand=actions.First(a=>a.Command.Option==option).Command;Accept(game,c);Accept(restored,c);}
    private static void Set(dynamic game,dynamic s){if(s is M4.GameState a)M4CoreHarness.SetState((M4.CitiesKnightsGameSession)game,a);else M5Harness.SetState((M5.CombinedGameSession)game,(M5.GameState)s);}
    private static void Drain(dynamic game,string mode,bool accept=false)
    {
        for(int i=0;i<80;i++)
        {
            ThreeKingdomsState tk=game.GetAuthoritativeStateForTesting().ThreeKingdoms;var d=tk.Skills.PendingDecision??tk.Automation?.PendingDecision;if(d==null)return;
            B2SkillAction[] actions=game.GetPlayerView(d.PlayerId).ThreeKingdoms.SkillActions;
            var a=actions.FirstOrDefault(a=>a.Command.Option=="automation-skip")??actions.FirstOrDefault(a=>a.Command.Option==(accept?"accept":"skip"))??actions.First();dynamic c=Command(game,mode,"UseGeneralSkill",d.PlayerId);c.SkillCommand=a.Command;Accept(game,c);
        }
        Assert.Fail("B5 待决未结束。");
    }
    private static dynamic Restore(dynamic game,string mode)
    {object s=game.GetAuthoritativeStateForTesting();using var stream=new MemoryStream();var serializer=new DataContractJsonSerializer(s.GetType());serializer.WriteObject(stream,s);stream.Position=0;var copy=serializer.ReadObject(stream)!;object restored=mode=="cities"?(object)M4CoreHarness.New():M5Harness.New();restored.GetType().GetField("state",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(restored,copy);return restored;}
    private static void GiveProgress(dynamic s,string player,params string[] cards)
    {if(s is M4.GameState a)a.Players.Single(p=>p.Id==player).ProgressCards=cards.Select(Enum.Parse<M4.ProgressCardKind>).ToArray();else ((M5.GameState)s).Players.Single(p=>p.Id==player).ProgressCards=cards.Select(Enum.Parse<M5.ProgressCardKind>).ToArray();}
    private static dynamic Play(dynamic game,string mode,string card,string target="P2")
    {dynamic c=Command(game,mode,"PlayProgressCard");if(mode=="cities")c.ProgressCard=Enum.Parse<M4.ProgressCardKind>(card);else c.ProgressCard=Enum.Parse<M5.ProgressCardKind>(card);c.OtherPlayerId=target;return c;}
    private static string Vertex(string mode)=>mode=="cities"?M4CoreHarness.New().GetPlayerView("P1").Board.Vertices[0].Id:M5Harness.New().GetPlayerView("P1").Board.Tiles.First(t=>t.Resource!="sea").Vertices[0];
    private static void Settlement(dynamic s,string vertex,string player="P1")
    {if(s is M4.GameState a)a.Settlements=a.Settlements.Concat(new[]{M4CoreHarness.Piece(vertex,player)}).ToArray();else {var b=(M5.GameState)s;b.Settlements=b.Settlements.Concat(new[]{M5Harness.Piece(vertex,player)}).ToArray();}}
    private static string Road(dynamic game,string vertex)
    {dynamic view=game.GetPlayerView("P1");if(view is M4.PlayerView a)return a.Board.Edges.First(e=>e.Vertices.Contains(vertex)).Id;M5.PlayerView b=view;return b.Board.Edges.First(e=>e.Vertices.Contains(vertex)&&b.Board.Tiles.Any(t=>t.Resource!="sea"&&e.Vertices.All(t.Vertices.Contains))).Id;}

    [Theory][InlineData("cities")][InlineData("combined")]
    public void GuildDuesKeepsValidatedTargetWhenPojunResponseChangesScores(string mode)
    {
        string vertex=Vertex(mode);
        dynamic game=Fixture(mode,new[]{"tk-g32","tk-g12","tk-g09","tk-g22"},s=>{
            GiveProgress(s,"P1","GuildDues");Player(s,"P1").DefenderPoints=2;
            Player(s,"P2").Improvements=new[]{4,0,0};Player(s,"P2").Resources.Wood=1;
            if(s is M4.GameState a){a.Cities=new[]{M4CoreHarness.Piece(vertex,"P2")};a.Metropolises=new[]{new M4.Metropolis{PlayerId="P2",LocationId=vertex,Track=M4.ImprovementTrack.Trade}};}
            else {var b=(M5.GameState)s;b.Cities=new[]{M5Harness.Piece(vertex,"P2")};b.Metropolises=new[]{new M5.Metropolis{PlayerId="P2",LocationId=vertex,Track=M5.ImprovementTrack.Trade}};}
        });
        var initial=game.GetAuthoritativeStateForTesting();Player(initial,"P1").DefenderPoints=4;Set(game,initial);
        var invalid=Play(game,mode,"GuildDues");string before=game.Save();
        Assert.Equal("NotMoreVictoryPoints",(string)game.Execute(invalid).ErrorCode);Assert.Equal(before,(string)game.Save());
        Player(initial,"P1").DefenderPoints=2;Set(game,initial);
        Accept(game,Play(game,mode,"GuildDues"));Skill(game,mode,"P1","accept");
        // 扣置一层贸易设施会撤销目标的大都会，两人的公开分数变为相同。
        dynamic restored=Restore(game,mode);SameSkill(game,restored,mode,"P1","civil-pojun-track");
        Assert.Equal((string)game.Save(),(string)restored.Save());
        Assert.Empty((Array)game.GetAuthoritativeStateForTesting().Metropolises);
        Assert.Equal("ProgressGuildDues",(string)game.GetAuthoritativeStateForTesting().PendingDecision.Kind);
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void PojunDefersMonopolyUntilSuppressionAndDoublesOnlyQualifiedTarget(string mode)
    {
        dynamic game=Fixture(mode,new[]{"tk-g32","tk-g12","tk-g09","tk-g22"},s=>{GiveProgress(s,"P1","ResourceMonopoly");Player(s,"P1").Improvements=new[]{3,0,0};Player(s,"P2").Improvements=new[]{4,0,0};Player(s,"P2").Resources.Wood=4;Player(s,"P3").Resources.Wood=4;Player(s,"P3").Improvements=new[]{4,0,0};});
        dynamic c=Play(game,mode,"ResourceMonopoly");c.Resource=Resource.Wood;Accept(game,c);Assert.Equal(0,(int)Player(game.GetAuthoritativeStateForTesting()).Resources.Wood);
        Skill(game,mode,"P1","accept");Skill(game,mode,"P1","civil-pojun-track",(Func<B2SkillAction,bool>)(a=>a.Command.CardType==0));Skill(game,mode,"P1","civil-pojun-done");
        dynamic saved=Restore(game,mode);SameSkill(game,saved,mode,"P1","skip");
        Assert.Equal((string)game.Save(),(string)saved.Save());dynamic state=game.GetAuthoritativeStateForTesting();Assert.Equal(6,(int)Player(state).Resources.Wood);Assert.Equal(0,(int)Player(state,"P2").Resources.Wood);Assert.Equal(2,(int)Player(state,"P3").Resources.Wood);
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void ActiveWeimuPreventsBlueEspionageFromOpeningPrivateTargetCards(string mode)
    {
        dynamic game=Fixture(mode,new[]{"tk-g12","tk-g11","tk-g09","tk-g22"},s=>{GiveProgress(s,"P1","Espionage");GiveProgress(s,"P2","Irrigation");});
        dynamic c=Play(game,mode,"Espionage");Accept(game,c);Drain(game,mode);
        dynamic state=game.GetAuthoritativeStateForTesting();Assert.Null(state.PendingDecision);Assert.Equal(1,(int)Player(state,"P2").ProgressCards.Length);Assert.Equal(0,(int)Player(state,"P1").ProgressCards.Length);
        ThreeKingdomsState tk=state.ThreeKingdoms;Assert.Single(tk.SkillHistory,e=>e.SkillId=="tk-g11-s1");string snapshot=game.Save();
        foreach(string viewer in new[]{"P1","P3"})Assert.Empty((Array)game.GetPlayerView(viewer).PrivateTargetProgressCards);
        Assert.Equal(snapshot,(string)game.Save());
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void RetiredWeimuResponseRestoresAndConsumesOnlyAcceptedBlueUse(string mode)
    {
        dynamic game=Fixture(mode,new[]{"tk-g12","tk-g06","tk-g09","tk-g22"},s=>{GiveProgress(s,"P1","Espionage");GiveProgress(s,"P2","Irrigation");ThreeKingdomsState tk=s.ThreeKingdoms;tk.CivilB5.Markers=new[]{new B5CivilMarker {PlayerId="P2",Key="weimu-residual",Value="retired-jia-tenure"}};});
        dynamic c=Play(game,mode,"Espionage");Accept(game,c);Assert.Equal("P2",(string)game.GetAuthoritativeStateForTesting().ThreeKingdoms.Skills.PendingDecision.PlayerId);
        dynamic saved=Restore(game,mode);SameSkill(game,saved,mode,"P2","accept");Assert.Equal((string)game.Save(),(string)saved.Save());
        dynamic state=game.GetAuthoritativeStateForTesting();Assert.Null(state.PendingDecision);Assert.Equal(1,(int)Player(state,"P2").ProgressCards.Length);ThreeKingdomsState tk2=state.ThreeKingdoms;Assert.DoesNotContain(tk2.CivilB5.Markers,m=>m.Key=="weimu-residual");
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void NativeRoadReceiptMakesKurouRefundActualTwoCards(string mode)
    {
        string vertex=Vertex(mode);dynamic game=Fixture(mode,new[]{"tk-g16","tk-g12","tk-g09","tk-g22"},s=>{Settlement(s,vertex);Player(s).Resources.Wood=1;Player(s).Resources.Brick=1;});
        string road=Road(game,vertex);dynamic c=Command(game,mode,"BuildRoad");c.TargetId=road;Accept(game,c);Drain(game,mode);
        ThreeKingdomsState tk=game.GetAuthoritativeStateForTesting().ThreeKingdoms;Assert.Equal(2,tk.Board.Receipts.Single(r=>r.AssetId=="Road:"+road).PaidCount);
        Skill(game,mode,"P1","civil-kurou",(Func<B2SkillAction,bool>)(a=>a.Label.Contains(road)));Skill(game,mode,"P1","card",(Func<B2SkillAction,bool>)(a=>a.Command.CardType==7));Skill(game,mode,"P1","card",(Func<B2SkillAction,bool>)(a=>a.Command.CardType==7));Drain(game,mode);
        dynamic state=game.GetAuthoritativeStateForTesting();Assert.Equal(2,(int)Player(state).Commodities.Paper);Assert.Equal(0,(int)state.Roads.Length);Assert.Equal(15,(int)Player(state).Pieces.Roads);
    }
    [Theory][InlineData("cities",false)][InlineData("combined",false)][InlineData("cities",true)][InlineData("combined",true)]
    public void NativeYichengUsesRealBishopPipelineAndNoProgressCardEntity(string mode,bool hasPaidVirtualCandidate)
    {
        dynamic game=Fixture(mode,new[]{"tk-g32","tk-g12","tk-g09","tk-g22"},s=>{s.FirstBarbarianAttack=true;Player(s).Resources.Wood=1;Player(s).Resources.Brick=1;if(hasPaidVirtualCandidate){Player(s).Improvements=new[]{1,0,0};Player(s).Commodities.Cloth=1;}if(s is M4.GameState a){a.RobberTileId=a.Tiles.First(t=>t.Resource!="sea").Id;if(hasPaidVirtualCandidate)a.Cities=new[]{M4CoreHarness.Piece(Vertex(mode))};}else {var b=(M5.GameState)s;b.RobberTileId=b.Tiles.First(t=>t.Resource!="sea").Id;b.PirateTileId=b.Tiles.First(t=>t.Resource=="sea").Id;if(hasPaidVirtualCandidate)b.Cities=new[]{M5Harness.Piece(Vertex(mode))};}});
        Skill(game,mode,"P1","civil-yicheng");Skill(game,mode,"P1","civil-select",(Func<B2SkillAction,bool>)(a=>a.Command.CardType==0));Skill(game,mode,"P1","civil-select",(Func<B2SkillAction,bool>)(a=>a.Command.CardType==1));
        string snapshot=game.Save();game.GetPlayerView("P1");game.GetPlayerView("P2");Assert.Equal(snapshot,(string)game.Save());Assert.DoesNotContain("SuppressPump",snapshot);
        string before=game.GetAuthoritativeStateForTesting().RobberTileId;dynamic restored=Restore(game,mode);SameSkill(game,restored,mode,"P1","civil-yicheng-use");Assert.Equal((string)game.Save(),(string)restored.Save());Drain(game,mode);dynamic state=game.GetAuthoritativeStateForTesting();Assert.Equal(0,(int)Player(state).Resources.Total);Assert.Equal(hasPaidVirtualCandidate?1:0,(int)Player(state).Commodities.Cloth);Assert.Equal(hasPaidVirtualCandidate?1:0,(int)Player(state).Improvements[0]);Assert.Equal(0,(int)Player(state).ProgressCards.Length);Assert.Null(state.ThreeKingdomsVirtualUse);Assert.NotEqual(before,(string)state.RobberTileId);
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void NativeProgressUseTriggersXiaojiOnceAndKeepsBankAndDeckConserved(string mode)
    {
        dynamic game=Fixture(mode,new[]{"tk-g27","tk-g12","tk-g09","tk-g22"},s=>GiveProgress(s,"P1","ResourceMonopoly"));dynamic c=Play(game,mode,"ResourceMonopoly");c.Resource=Resource.Wood;Accept(game,c);Drain(game,mode);
        dynamic state=game.GetAuthoritativeStateForTesting();Assert.Equal(3,(int)Player(state).Resources.Total);Assert.Equal(0,(int)Player(state).ProgressCards.Length);ThreeKingdomsState tk=state.ThreeKingdoms;Assert.Single(tk.SkillHistory,e=>e.SkillId=="tk-g27-s2");
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void EmptyEspionageTargetDoesNotConsumeOrOfferResidualWeimu(string mode)
    {
        dynamic game=Fixture(mode,new[]{"tk-g12","tk-g06","tk-g09","tk-g22"},s=>{GiveProgress(s,"P1","Espionage");ThreeKingdomsState tk=s.ThreeKingdoms;tk.CivilB5.Markers=new[]{new B5CivilMarker {PlayerId="P2",Key="weimu-residual",Value="retired-jia"}};});
        dynamic c=Play(game,mode,"Espionage");Accept(game,c);ThreeKingdomsState state=game.GetAuthoritativeStateForTesting().ThreeKingdoms;
        Assert.Null(state.Skills.PendingDecision);Assert.Contains(state.CivilB5.Markers,m=>m.Key=="weimu-residual");Assert.DoesNotContain(state.SkillHistory??Array.Empty<B4SkillHistoryEvent>(),e=>e.SkillId=="tk-g11-s1");
    }
    [Theory][InlineData("cities",false)][InlineData("combined",false)][InlineData("cities",true)][InlineData("combined",true)]
    public void WeimuBlocksBlueDiplomacyForOwnAndOtherUsers(string mode,bool ownUse)
    {
        dynamic template=Fixture(mode,new[]{"tk-g12","tk-g06","tk-g09","tk-g22"});string road=Road(template,Vertex(mode));string owner=ownUse?"P1":"P2";
        dynamic game=Fixture(mode,ownUse?new[]{"tk-g11","tk-g12","tk-g09","tk-g22"}:new[]{"tk-g12","tk-g11","tk-g09","tk-g22"},s=>{GiveProgress(s,"P1","Diplomacy");if(s is M4.GameState a)a.Roads=new[]{M4CoreHarness.Piece(road,owner)};else((M5.GameState)s).Roads=new[]{M5Harness.Piece(road,owner)};});
        dynamic c=Play(game,mode,"Diplomacy");c.TargetId=road;string before=game.Save();Assert.True((bool)game.Preview(c).Success);Assert.Equal(before,(string)game.Save());Accept(game,c);Drain(game,mode);dynamic state=game.GetAuthoritativeStateForTesting();Assert.Equal(1,(int)state.Roads.Length);Assert.Equal(0,(int)Player(state).ProgressCards.Length);
    }
    [Theory][InlineData("cities",false)][InlineData("combined",false)][InlineData("cities",true)][InlineData("combined",true)]
    public void YaowuRewardsOnlyActualChangesToOwnedProducingTiles(string mode,bool changesNumber)
    {
        string source="",target="";
        dynamic game=Fixture(mode,new[]{"tk-g12","tk-g24","tk-g09","tk-g22"},s=>
        {
            GiveProgress(s,"P1","Invention");ThreeKingdomsState tk=s.ThreeKingdoms;tk.Military.PermanentYaowuPlayers=new[]{"P2"};
            if(s is M4.GameState a){var one=a.Tiles.First(t=>t.Number==3);var two=a.Tiles.First(t=>t.Id!=one.Id&&t.Number==(changesNumber?4:3));source=one.Id;target=two.Id;Settlement(s,one.Vertices[0],"P2");}
            else{var b=(M5.GameState)s;var one=b.Tiles.First(t=>t.Number==3);var two=b.Tiles.First(t=>t.Id!=one.Id&&t.Number==(changesNumber?4:3));source=one.Id;target=two.Id;Settlement(s,one.Vertices[0],"P2");}
        });
        dynamic c=Play(game,mode,"Invention");c.SourceId=source;c.TargetId=target;Accept(game,c);Drain(game,mode);dynamic after=game.GetAuthoritativeStateForTesting();Assert.Equal(changesNumber?1:0,(int)Player(after,"P2").Resources.Total);
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void PojunSmithingEnumeratesFourTargetsAndPromotesEachOnlyOnceWithRealSupply(string mode)
    {
        string[] sites=mode=="cities"?M4CoreHarness.New().GetPlayerView("P1").Board.Vertices.Take(4).Select(v=>v.Id).ToArray():M5Harness.New().GetPlayerView("P1").Board.Tiles.Where(t=>t.Resource!="sea").SelectMany(t=>t.Vertices).Distinct().Take(4).ToArray();
        dynamic game=Fixture(mode,new[]{"tk-g32","tk-g12","tk-g09","tk-g22"},s=>
        {
            GiveProgress(s,"P1","Smithing","Smithing");Player(s).Improvements=new[]{0,3,0};
            if(s is M4.GameState a)a.Knights=sites.Select((id,i)=>new M4.Knight {PlayerId="P1",LocationId=id,Level=i<2?1:2,PromotedTurn=-1}).ToArray();
            else((M5.GameState)s).Knights=sites.Select((id,i)=>new M5.Knight {PlayerId="P1",LocationId=id,Level=i<2?1:2,PromotedTurn=-1}).ToArray();
        });
        dynamic view=game.GetPlayerView("P1");bool hasFour=mode=="cities"?((M4.PlayerView)view).LegalActions.Any(c=>c.Kind==M4.CommandKind.PlayProgressCard&&c.ProgressCard==M4.ProgressCardKind.Smithing&&c.TargetIds.Length==4):((M5.PlayerView)view).LegalActions.Any(c=>c.Kind==M5.CommandKind.PlayProgressCard&&c.ProgressCard==M5.ProgressCardKind.Smithing&&c.TargetIds.Length==4);Assert.True(hasFour);
        dynamic over=Play(game,mode,"Smithing");over.TargetIds=sites.Concat(new[]{sites[0]}).ToArray();Assert.False((bool)game.Preview(over).Success);
        dynamic use=Play(game,mode,"Smithing");use.TargetIds=sites;Accept(game,use);Drain(game,mode);dynamic state=game.GetAuthoritativeStateForTesting();int[] levels=state is M4.GameState m4?m4.Knights.Select(k=>k.Level).ToArray():((M5.GameState)state).Knights.Select(k=>k.Level).ToArray();Assert.Equal(new[]{2,2,3,3},levels);Assert.Equal(0,(int)Player(state).Resources.Total);
        dynamic again=Play(game,mode,"Smithing");again.TargetIds=new[]{sites[0]};Assert.False((bool)game.Preview(again).Success);
    }
    [Theory][InlineData("cities",false)][InlineData("combined",false)][InlineData("cities",true)][InlineData("combined",true)]
    public void PojunHarborPersistsTargetQuotasAndChargesOneResourcePerExchange(string mode,bool enoughForBoth)
    {
        dynamic game=Fixture(mode,new[]{"tk-g32","tk-g12","tk-g09","tk-g22"},s=>{GiveProgress(s,"P1","CommercialHarbor");Player(s).Resources.Wood=enoughForBoth?2:1;Player(s,"P2").Commodities.Cloth=2;Player(s,"P3").Improvements=new[]{1,0,0};});
        Accept(game,Play(game,mode,"CommercialHarbor"));Drain(game,mode);string[] quotas=game.GetAuthoritativeStateForTesting().CommercialHarborPlayers;Assert.Equal(2,quotas.Count(id=>id=="P2"));Assert.Equal(1,quotas.Count(id=>id=="P3"));
        dynamic offer=Command(game,mode,"CommercialHarborOffer");offer.OtherPlayerId="P2";offer.Resource=Resource.Wood;Accept(game,offer);dynamic restored=Restore(game,mode);dynamic choose=Command(game,mode,"ResolveProgressChoice","P2");if(mode=="cities")choose.Commodity=M4.Commodity.Cloth;else choose.Commodity=M5.Commodity.Cloth;Accept(game,choose);Accept(restored,choose);Assert.Equal((string)game.Save(),(string)restored.Save());
        dynamic after=game.GetAuthoritativeStateForTesting();Assert.Equal(1,(int)Player(after).Commodities.Cloth);Assert.Equal(enoughForBoth?1:0,(int)Player(after).Resources.Wood);quotas=after.CommercialHarborPlayers;Assert.Equal(1,quotas.Count(id=>id=="P2"));
        if(enoughForBoth)
        {
            offer=Command(game,mode,"CommercialHarborOffer");offer.OtherPlayerId="P2";offer.Resource=Resource.Wood;Accept(game,offer);choose=Command(game,mode,"ResolveProgressChoice","P2");if(mode=="cities")choose.Commodity=M4.Commodity.Cloth;else choose.Commodity=M5.Commodity.Cloth;Accept(game,choose);after=game.GetAuthoritativeStateForTesting();quotas=after.CommercialHarborPlayers;Assert.DoesNotContain("P2",quotas);Assert.Equal(2,(int)Player(after).Commodities.Cloth);Assert.Equal(0,(int)Player(after).Resources.Total);
        }
        else
        {
            dynamic view=game.GetPlayerView("P1");bool canOffer=mode=="cities"?((M4.PlayerView)view).LegalActions.Any(c=>c.Kind==M4.CommandKind.CommercialHarborOffer):((M5.PlayerView)view).LegalActions.Any(c=>c.Kind==M5.CommandKind.CommercialHarborOffer);Assert.False(canOffer);Accept(game,Command(game,mode,"EndTurn"));Drain(game,mode);Assert.Null(game.GetAuthoritativeStateForTesting().PendingDecision);
        }
    }
}
