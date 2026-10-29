using System.Reflection;
using System.Runtime.Serialization.Json;
using Catan.Core;
using Catan.Core.ThreeKingdoms;
using M4=Catan.Core.M4;
using M5=Catan.Core.M5;
using Xunit;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB6AllianceIntegrationTests
{
    private static ThreeKingdomsState Generals(params string[] requested)
    {
        var tk=ThreeKingdomsEngine.Create(new[]{"P1","P2","P3","P4"},42,_=>0,true,GeneralCatalog.B6PackageVersion);
        while(tk.PendingDecision!=null){var d=tk.PendingDecision;if(d.Kind==ThreeKingdomsDecisionKind.SelectOpening)ThreeKingdomsEngine.ChooseGenerals(tk,d.PlayerId,d.CandidateIds.Take(2).ToArray(),d.DecisionId,d.WindowId,_=>0);else ThreeKingdomsEngine.DeployGeneral(tk,d.PlayerId,d.CandidateIds[0],d.DecisionId,d.WindowId,_=>0);}
        ThreeKingdomsEngine.BeginNormalTurns(tk);tk.Clock.Phase=ThreeKingdomsPhase.Play;tk.Clock.PhaseId=5;tk.Clock.DrawPhaseTurnInstanceId=tk.Clock.TurnInstanceId;
        for(int i=0;i<requested.Length;i++){var g=tk.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P"+(i+1));var other=tk.Generals.Single(g=>g.GeneralId==requested[i]);(g.GeneralId,other.GeneralId)=(other.GeneralId,g.GeneralId);(g.InstanceId,other.InstanceId)=(other.InstanceId,g.InstanceId);g.Counters=Array.Empty<GeneralCounter>();}
        tk.AllianceB6=new B6AllianceState();tk.Skills=new B2SkillState();tk.CivilB5=new B5CivilState();tk.Board=new B5BoardState();tk.Response=null;
        tk.Automation=new B4AutomationState {TurnId=tk.Clock.TurnInstanceId,Asked=tk.Generals.Where(g=>g.Region==GeneralRegion.Active).SelectMany(g=>GeneralCatalog.Get(g.GeneralId).Skills.Select(s=>tk.Clock.PhaseId+":"+g.TenureId+":"+s.Id)).ToArray()};return tk;
    }
    private static dynamic Fixture(string mode,string[] generals,Action<dynamic>? configure=null)
    {if(mode=="cities")return M4CoreHarness.Fixture(s=>{s.RulesVersion=M4.CitiesKnightsGameSession.ThreeKingdomsB6RulesVersion;s.ThreeKingdoms=Generals(generals);configure?.Invoke(s);});return M5Harness.Fixture(s=>{s.RulesVersion=M5.CombinedGameSession.ThreeKingdomsB6RulesVersion;s.ThreeKingdoms=Generals(generals);configure?.Invoke(s);});}
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

    private static void Allies(dynamic state,params string[] members)
    {ThreeKingdomsState tk=state.ThreeKingdoms;tk.AllianceB6=new B6AllianceState {Sources=new[]{new B6AllianceSource {SourcePlayerId="P1",TenureId=tk.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P1").TenureId,Members=members}}};}

    [Theory][InlineData("cities")][InlineData("combined")]
    public void SharedPortsAndEventTechnologyDoNotTransferAssets(string mode)
    {
        dynamic template=Fixture(mode,new[]{"tk-g50","tk-g12","tk-g09","tk-g22"});dynamic view=template.GetPlayerView("P1");string vertex=view.Board.Ports[0].Vertices[0];Resource resource=view.Board.Ports[0].Resource??Resource.Wood;
        dynamic game=Fixture(mode,new[]{"tk-g50","tk-g12","tk-g09","tk-g22"},s=>{Allies(s,"P1","P2");Settlement(s,vertex,"P2");Player(s,"P2").Improvements=new[]{0,2,0};});
        Assert.Equal((int)game.GetBankTradeRate("P2",resource),(int)game.GetBankTradeRate("P1",resource));dynamic state=game.GetAuthoritativeStateForTesting();object track=mode=="cities"?(object)M4.ImprovementTrack.Politics:M5.ImprovementTrack.Politics;
        game.GetType().GetMethod("EnqueueEventProgress",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(game,new object[]{state,track,3});
        string[] recipients=state is M4.GameState a?a.DecisionQueue.Select(d=>d.PlayerId).ToArray():((M5.GameState)state).DecisionQueue.Select(d=>d.PlayerId).ToArray();Assert.Equal(new[]{"P1","P2"},recipients);Assert.Equal(0,(int)Player(state).Improvements[1]);Assert.Equal("P2",(string)state.Settlements[0].PlayerId);
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void PersonalThresholdWinsForWholeAllianceWithoutAddingMemberScores(string mode)
    {
        dynamic game=Fixture(mode,new[]{"tk-g50","tk-g12","tk-g09","tk-g22"},s=>{Allies(s,"P1","P2");Player(s).DefenderPoints=16;});Accept(game,Command(game,mode,"DeclareVictory"));dynamic state=game.GetAuthoritativeStateForTesting();Assert.Equal("P1",(string)state.WinnerPlayerId);ThreeKingdomsState tk=state.ThreeKingdoms;Assert.Equal(new[]{"P1","P2"},tk.AllianceB6.WinnerPlayerIds);Assert.Equal(0,(int)Player(state,"P2").DefenderPoints);
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void CrossFactionTradeRejectedWithoutChangingEitherHand(string mode)
    {
        dynamic game=Fixture(mode,new[]{"tk-g50","tk-g12","tk-g09","tk-g22"},s=>{Allies(s,"P1","P2");Player(s).Resources.Wood=1;Player(s,"P3").Resources.Brick=1;});dynamic c=Command(game,mode,"ProposeTrade");c.OtherPlayerId="P3";c.Give=new ResourceBag{Wood=1};c.Receive=new ResourceBag{Brick=1};string before=game.Save();Assert.False((bool)game.Execute(c).Success);Assert.Equal(before,(string)game.Save());
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void SharedKnightActivationAndPromotionChargeOperatorAndKeepEntityOwner(string mode)
    {
        string site=Vertex(mode);dynamic game=Fixture(mode,new[]{"tk-g50","tk-g12","tk-g09","tk-g22"},s=>{
            Allies(s,"P1","P2");Player(s).Resources.Wheat=1;Player(s).Resources.Wool=1;Player(s).Resources.Ore=1;Player(s,"P2").Improvements=new[]{0,3,0};
            if(s is M4.GameState a)a.Knights=new[]{new M4.Knight{PlayerId="P2",LocationId=site,Level=2,PromotedTurn=-1}};else((M5.GameState)s).Knights=new[]{new M5.Knight{PlayerId="P2",LocationId=site,Level=2,PromotedTurn=-1}};
        });
        dynamic view=game.GetPlayerView("P1");bool visible=mode=="cities"?((M4.PlayerView)view).LegalActions.Any(c=>c.Kind==M4.CommandKind.ActivateKnight&&c.TargetId==site):((M5.PlayerView)view).LegalActions.Any(c=>c.Kind==M5.CommandKind.ActivateKnight&&c.TargetId==site);Assert.True(visible);
        dynamic activate=Command(game,mode,"ActivateKnight");activate.TargetId=site;Accept(game,activate);dynamic promote=Command(game,mode,"PromoteKnight");promote.TargetId=site;Accept(game,promote);
        dynamic state=game.GetAuthoritativeStateForTesting();Assert.Equal("P2",(string)state.Knights[0].PlayerId);Assert.Equal(3,(int)state.Knights[0].Level);Assert.Equal(0,(int)Player(state).Resources.Total);Assert.Equal(0,(int)Player(state,"P2").Resources.Total);Assert.Equal(0,(int)Player(state).Improvements[1]);
        dynamic duplicate=Command(game,mode,"PromoteKnight");duplicate.TargetId=site;Assert.False((bool)game.Preview(duplicate).Success);
    }
    [Fact]
    public void CityAiDefenseEvaluatesLegalAlliedKnightWithoutAssumingOwnership()
    {
        string site=Vertex("cities");var game=(M4.CitiesKnightsGameSession)Fixture("cities",new[]{"tk-g50","tk-g12","tk-g09","tk-g22"},s=>{
            Allies(s,"P1","P2");Player(s).Resources.Wheat=1;
            ((M4.GameState)s).Knights=new[]{new M4.Knight{PlayerId="P2",LocationId=site,Level=2}};
        });
        var view=game.GetAiPlayerView("P1");var legal=view.LegalActions.ToList();string before=game.Save();
        var choice=(M4.Command)typeof(Catan.AI.CitiesKnightsAi).GetMethod("DefenseAction",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object[]{view,legal,1})!;
        Assert.Equal(M4.CommandKind.ActivateKnight,choice.Kind);Assert.Equal(site,choice.TargetId);Assert.Equal(before,game.Save());
        choice.Id=ThreeKingdomsTestGame.NextId();Assert.True(game.Execute(choice).Success);
        Assert.Equal("P2",game.GetPlayerView("P1").Board.Knights.Single().PlayerId);
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void SmithingUsesAlliedKnightSupplyAndSharedFortress(string mode)
    {
        string site=Vertex(mode);dynamic game=Fixture(mode,new[]{"tk-g50","tk-g12","tk-g09","tk-g22"},s=>{Allies(s,"P1","P2");GiveProgress(s,"P1","Smithing");Player(s,"P2").Improvements=new[]{0,3,0};if(s is M4.GameState a)a.Knights=new[]{new M4.Knight{PlayerId="P2",LocationId=site,Level=2,PromotedTurn=-1}};else((M5.GameState)s).Knights=new[]{new M5.Knight{PlayerId="P2",LocationId=site,Level=2,PromotedTurn=-1}};});
        dynamic c=Play(game,mode,"Smithing");c.TargetIds=new[]{site};Accept(game,c);dynamic state=game.GetAuthoritativeStateForTesting();Assert.Equal("P2",(string)state.Knights[0].PlayerId);Assert.Equal(3,(int)state.Knights[0].Level);Assert.Equal(0,(int)Player(state).Improvements[1]);Assert.Empty((Array)Player(state).ProgressCards);
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void InvasionCountsSharedKnightExactlyOnceAndRewardsEntityOwner(string mode)
    {
        string site=Vertex(mode);dynamic game=Fixture(mode,new[]{"tk-g50","tk-g12","tk-g09","tk-g22"},s=>{Allies(s,"P1","P2");if(s is M4.GameState a)a.Knights=new[]{new M4.Knight{PlayerId="P2",LocationId=site,Level=2,Active=true}};else((M5.GameState)s).Knights=new[]{new M5.Knight{PlayerId="P2",LocationId=site,Level=2,Active=true}};});
        dynamic state=game.GetAuthoritativeStateForTesting();game.GetType().GetMethod("B5Invasion",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(game,new object[]{state,"alliance-invasion",new[]{"P1","P2","P3","P4"},true});ThreeKingdomsState tk=state.ThreeKingdoms;Assert.Equal(2,tk.Board.Invasions.Single().DefenseStrength);Assert.Equal(1,(int)Player(state,"P2").DefenderPoints);Assert.Equal(0,(int)Player(state).DefenderPoints);
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void RetiringAllianceReturnsStrandedKnightThroughOwnerDecision(string mode)
    {
        dynamic template=Fixture(mode,new[]{"tk-g50","tk-g12","tk-g09","tk-g22"});dynamic view=template.GetPlayerView("P1");var edges=mode=="cities"?((M4.PlayerView)view).Board.Edges:((M5.PlayerView)view).Board.Edges;var first=edges.First();var second=edges.First(e=>!e.Vertices.Intersect(first.Vertices).Any());string start=first.Vertices[0];
        dynamic game=Fixture(mode,new[]{"tk-g50","tk-g12","tk-g09","tk-g22"},s=>{Allies(s,"P1","P2");if(s is M4.GameState a){a.Roads=new[]{M4CoreHarness.Piece(first.Id,"P1"),M4CoreHarness.Piece(second.Id,"P2")};a.Knights=new[]{new M4.Knight{PlayerId="P2",LocationId=start,Level=1}};}else{var b=(M5.GameState)s;b.Roads=new[]{M5Harness.Piece(first.Id,"P1"),M5Harness.Piece(second.Id,"P2")};b.Knights=new[]{new M5.Knight{PlayerId="P2",LocationId=start,Level=1}};}});
        dynamic state=game.GetAuthoritativeStateForTesting();ThreeKingdomsState tk=state.ThreeKingdoms;B2SkillEngine.OnAllianceB6GeneralRetired(tk,tk.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P1"));game.GetType().GetMethod("B6ReturnAllianceKnights",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(game,new object[]{state});Set(game,state);Assert.Equal("P2",(string)state.PendingDecision.PlayerId);Assert.Equal("DisplacedKnight",(string)state.PendingDecision.Kind);
        dynamic copy=Restore(game,mode);dynamic c=Command(game,mode,"ResolveChoice","P2");c.TargetId=second.Vertices[0];Accept(game,c);Accept(copy,c);Assert.Equal((string)game.Save(),(string)copy.Save());dynamic after=game.GetAuthoritativeStateForTesting();Assert.Equal("P2",(string)after.Knights[0].PlayerId);Assert.Equal(second.Vertices[0],(string)after.Knights[0].LocationId);
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void MultipleAllianceReturnsRecheckRemainingSpaceAndLeaveEnemiesAlone(string mode)
    {
        dynamic template=Fixture(mode,new[]{"tk-g50","tk-g12","tk-g09","tk-g22"});dynamic view=template.GetPlayerView("P1");var edges=mode=="cities"?((M4.PlayerView)view).Board.Edges:((M5.PlayerView)view).Board.Edges;var foreign=edges.First();var own=edges.First(e=>!e.Vertices.Intersect(foreign.Vertices).Any());string outsider=edges.SelectMany(e=>e.Vertices).First(v=>!foreign.Vertices.Contains(v)&&!own.Vertices.Contains(v));
        dynamic game=Fixture(mode,new[]{"tk-g50","tk-g12","tk-g09","tk-g22"},s=>{Allies(s,"P1","P2");if(s is M4.GameState a){a.Roads=new[]{M4CoreHarness.Piece(foreign.Id,"P1"),M4CoreHarness.Piece(own.Id,"P2")};a.Settlements=new[]{M4CoreHarness.Piece(own.Vertices[1],"P2")};a.Knights=new[]{new M4.Knight{PlayerId="P2",LocationId=foreign.Vertices[0],Level=1},new M4.Knight{PlayerId="P2",LocationId=foreign.Vertices[1],Level=1},new M4.Knight{PlayerId="P3",LocationId=outsider,Level=1}};}else{var b=(M5.GameState)s;b.Roads=new[]{M5Harness.Piece(foreign.Id,"P1"),M5Harness.Piece(own.Id,"P2")};b.Settlements=new[]{M5Harness.Piece(own.Vertices[1],"P2")};b.Knights=new[]{new M5.Knight{PlayerId="P2",LocationId=foreign.Vertices[0],Level=1},new M5.Knight{PlayerId="P2",LocationId=foreign.Vertices[1],Level=1},new M5.Knight{PlayerId="P3",LocationId=outsider,Level=1}};}});
        dynamic state=game.GetAuthoritativeStateForTesting();ThreeKingdomsState tk=state.ThreeKingdoms;B2SkillEngine.OnAllianceB6GeneralRetired(tk,tk.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P1"));game.GetType().GetMethod("B6ReturnAllianceKnights",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(game,new object[]{state});Set(game,state);dynamic copy=Restore(game,mode);dynamic c=Command(game,mode,"ResolveChoice","P2");c.TargetId=own.Vertices[0];Accept(game,c);Accept(copy,c);Assert.Equal((string)game.Save(),(string)copy.Save());dynamic after=game.GetAuthoritativeStateForTesting();Assert.Null(after.PendingDecision);var knights=((Array)after.Knights).Cast<dynamic>().ToArray();Assert.Equal(2,knights.Length);Assert.Single(knights,k=>(string)k.PlayerId=="P2");Assert.Single(knights,k=>(string)k.PlayerId=="P3"&&(string)k.LocationId==outsider);
    }

    [Fact]
    public void CombinedRetireCommandAutomaticallyQueuesStrandedAlliedKnightReturn()
    {
        const string mode="combined";M5.CombinedGameSession template=Fixture(mode,new[]{"tk-g50","tk-g12","tk-g09","tk-g22"});var board=template.GetPlayerView("P1").Board;
        var landEdges=board.Edges.Where(e=>board.Tiles.Any(t=>t.Resource!="sea"&&e.Vertices.All(t.Vertices.Contains))).ToArray();var foreign=landEdges.First();var own=landEdges.First(e=>!e.Vertices.Intersect(foreign.Vertices).Any());
        M5.CombinedGameSession game=Fixture(mode,new[]{"tk-g50","tk-g12","tk-g09","tk-g22"},s=>{Allies(s,"P1","P2");var native=(M5.GameState)s;native.Roads=new[]{M5Harness.Piece(foreign.Id,"P1"),M5Harness.Piece(own.Id,"P2")};native.Knights=new[]{new M5.Knight{PlayerId="P2",LocationId=foreign.Vertices[0],Level=1}};});
        var retire=(M5.Command)Command(game,mode,"RetireGeneral");var before=game.Save();Assert.True(game.Preview(retire).Success);Assert.Equal(before,game.Save());Accept(game,retire);
        var after=game.GetAuthoritativeStateForTesting();Assert.Empty(after.ThreeKingdoms.AllianceB6.Sources);Assert.NotNull(after.PendingDecision);Assert.True(after.PendingDecision.AllianceReturnHome);Assert.Equal("P2",after.PendingDecision.PlayerId);Assert.Equal("DisplacedKnight",after.PendingDecision.Kind);Assert.Empty(after.Knights);
        for(int guard=0;game.GetAuthoritativeStateForTesting().ThreeKingdoms.PendingDecision!=null&&guard<4;guard++)
        {
            var d=game.GetAuthoritativeStateForTesting().ThreeKingdoms.PendingDecision;var c=(M5.Command)Command(game,mode,d.Kind==ThreeKingdomsDecisionKind.DeployReplacement?"DeployGeneral":"ChooseGenerals",d.PlayerId);c.DecisionId=d.DecisionId;c.WindowId=d.WindowId;c.GeneralIds=d.CandidateIds.Take(Math.Max(1,d.RequiredCount)).ToArray();Accept(game,c);
        }
        Drain(game,mode);var restored=(M5.CombinedGameSession)Restore(game,mode);var home=(M5.Command)Command(game,mode,"ResolveChoice","P2");home.TargetId=own.Vertices[0];Accept(game,home);Accept(restored,home);Assert.Equal(game.Save(),restored.Save());Assert.Equal(own.Vertices[0],game.GetAuthoritativeStateForTesting().Knights.Single(k=>k.PlayerId=="P2").LocationId);Assert.True(game.Execute(home).IsDuplicate);
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void SharedKnightMovesAlongAlliedRoutesAndDoesNotChangeOwnership(string mode)
    {
        dynamic template=Fixture(mode,new[]{"tk-g50","tk-g12","tk-g09","tk-g22"});string start=Vertex(mode),road=Road(template,start);dynamic view=template.GetPlayerView("P1");string end=mode=="cities"?((M4.PlayerView)view).Board.Edges.Single(e=>e.Id==road).Vertices.Single(v=>v!=start):((M5.PlayerView)view).Board.Edges.Single(e=>e.Id==road).Vertices.Single(v=>v!=start);
        dynamic game=Fixture(mode,new[]{"tk-g50","tk-g12","tk-g09","tk-g22"},s=>{Allies(s,"P1","P2");if(s is M4.GameState a){a.Roads=new[]{M4CoreHarness.Piece(road,"P1")};a.Knights=new[]{new M4.Knight{PlayerId="P2",LocationId=start,Level=1,Active=true,ActivatedTurn=1}};}else{var b=(M5.GameState)s;b.Roads=new[]{M5Harness.Piece(road,"P1")};b.Knights=new[]{new M5.Knight{PlayerId="P2",LocationId=start,Level=1,Active=true,ActivatedTurn=1}};}});
        dynamic move=Command(game,mode,"MoveKnight");move.SourceId=start;move.TargetId=end;Accept(game,move);dynamic after=game.GetAuthoritativeStateForTesting();Assert.Equal("P2",(string)after.Knights[0].PlayerId);Assert.Equal(end,(string)after.Knights[0].LocationId);Assert.False((bool)after.Knights[0].Active);
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void SharedTradeTechnologyAppliesWithoutCopyingFacilities(string mode)
    {
        dynamic game=Fixture(mode,new[]{"tk-g50","tk-g12","tk-g09","tk-g22"},s=>{Allies(s,"P1","P2");Player(s,"P2").Improvements=new[]{3,0,0};Player(s).Commodities.Cloth=2;});
        int rate=mode=="cities"?game.GetCommodityTradeRate("P1",M4.Commodity.Cloth):game.GetCommodityTradeRate("P1",M5.Commodity.Cloth);Assert.Equal(2,rate);
        dynamic c=Command(game,mode,"BankTrade");c.GiveIsCommodity=true;c.GiveAmount=2;c.ReceiveAmount=1;c.ReceiveResource=Resource.Wood;if(mode=="cities")c.GiveCommodity=M4.Commodity.Cloth;else c.GiveCommodity=M5.Commodity.Cloth;Accept(game,c);
        dynamic after=game.GetAuthoritativeStateForTesting();Assert.Equal(1,(int)Player(after).Resources.Wood);Assert.Equal(0,(int)Player(after).Improvements[0]);Assert.Equal(3,(int)Player(after,"P2").Improvements[0]);
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void PaiyiDoublesEnemyMonopolyAndSkipsAlliedHands(string mode)
    {
        dynamic game=Fixture(mode,new[]{"tk-g50","tk-g12","tk-g09","tk-g22"},s=>{Allies(s,"P1","P2");GiveProgress(s,"P1","ResourceMonopoly");Player(s,"P2").Resources.Wood=5;Player(s,"P3").Resources.Wood=5;Player(s,"P4").Resources.Wood=5;});
        dynamic c=Play(game,mode,"ResourceMonopoly");c.Resource=Resource.Wood;Accept(game,c);Drain(game,mode);dynamic after=game.GetAuthoritativeStateForTesting();Assert.Equal(8,(int)Player(after).Resources.Wood);Assert.Equal(5,(int)Player(after,"P2").Resources.Wood);Assert.Equal(1,(int)Player(after,"P3").Resources.Wood);Assert.Equal(1,(int)Player(after,"P4").Resources.Wood);
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void AllianceNegotiationRestoresAndUsesRealProgressDecks(string mode)
    {
        dynamic game=Fixture(mode,new[]{"tk-g50","tk-g12","tk-g09","tk-g22"});dynamic state=game.GetAuthoritativeStateForTesting();ThreeKingdomsState tk=state.ThreeKingdoms;var g=tk.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P1");B2SkillEngine.OnAllianceB6GeneralActivated(tk,g);var host=(IThreeKingdomsCardHost)game.GetType().GetMethod("CardHost",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(game,new object[]{state})!;B2SkillEngine.Pump(host);Set(game,state);
        Skill(game,mode,"P1","b4-order",(Func<B2SkillAction,bool>)(a=>a.Label.Contains("自立")));Skill(game,mode,"P2","alliance-b6-join");dynamic copy=Restore(game,mode);SameSkill(game,copy,mode,"P1","alliance-b6-accept");SameSkill(game,copy,mode,"P3","alliance-b6-reject");SameSkill(game,copy,mode,"P4","alliance-b6-reject");Assert.Equal((string)game.Save(),(string)copy.Save());state=game.GetAuthoritativeStateForTesting();Assert.Equal(2,(int)Player(state).ProgressCards.Length+(int)Player(state).ProgressVictoryCards.Length);Assert.Equal(new[]{"P1","P2"},(string[])game.GetPlayerView("P1").ThreeKingdoms.AllianceMembers);
    }
}
