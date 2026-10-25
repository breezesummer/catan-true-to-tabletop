using System.Reflection;
using Catan.Core;
using Catan.Core.ThreeKingdoms;
using M4=Catan.Core.M4;
using M5=Catan.Core.M5;
using Xunit;

namespace Catan.Rules.Tests;

/// <summary>使用真实双宿主验证实体库存、损害、科技与入侵；指定局面不冒充正常开局重放证据。</summary>
public sealed class ThreeKingdomsB5BoardTests
{
    private static string Serialize(object value)
    {using var stream=new MemoryStream();new System.Runtime.Serialization.Json.DataContractJsonSerializer(value.GetType()).WriteObject(stream,value);return System.Text.Encoding.UTF8.GetString(stream.ToArray());}
    private sealed class BoardCase
    {
        internal readonly string Mode;
        internal dynamic Session;
        internal dynamic State;
        internal IThreeKingdomsB5Host Host;
        internal BoardCase(string mode,string map="heading-for-new-shores")
        {
            Mode=mode;Session=mode=="cities"?(object)M4CoreHarness.New():M5.CombinedGameSession.Create(map,4,42);State=Session.GetAuthoritativeStateForTesting();
            ThreeKingdomsState tk=ThreeKingdomsEngine.Create(new[]{"P1","P2","P3","P4"},42,_=>0,true,GeneralCatalog.B5PackageVersion);
            while(tk.PendingDecision!=null){var d=tk.PendingDecision;if(d.Kind==ThreeKingdomsDecisionKind.SelectOpening)ThreeKingdomsEngine.ChooseGenerals(tk,d.PlayerId,d.CandidateIds.Take(2).ToArray(),d.DecisionId,d.WindowId,_=>0);else ThreeKingdomsEngine.DeployGeneral(tk,d.PlayerId,d.CandidateIds[0],d.DecisionId,d.WindowId,_=>0);}
            ThreeKingdomsEngine.BeginNormalTurns(tk);tk.Clock.Phase=ThreeKingdomsPhase.Play;tk.Clock.PhaseId=5;tk.Clock.DrawPhaseTurnInstanceId=tk.Clock.TurnInstanceId;
            var ids=new[]{"tk-g16","tk-g06","tk-g09","tk-g22"};for(int i=0;i<4;i++){var g=tk.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P"+(i+1));var other=tk.Generals.Single(g=>g.GeneralId==ids[i]);(g.GeneralId,other.GeneralId)=(other.GeneralId,g.GeneralId);(g.InstanceId,other.InstanceId)=(other.InstanceId,g.InstanceId);g.Counters=Array.Empty<GeneralCounter>();}
            tk.Skills.Frames=Array.Empty<B2SkillFrame>();tk.Skills.PendingDecision=null;tk.Military.DelayedDeployments=Array.Empty<B5PlayerCount>();
            tk.Automation=new B4AutomationState{TurnId=tk.Clock.TurnInstanceId,Asked=tk.Generals.Where(g=>g.Region==GeneralRegion.Active).SelectMany(g=>GeneralCatalog.Get(g.GeneralId).Skills.Select(x=>tk.Clock.PhaseId+":"+g.TenureId+":"+x.Id)).ToArray()};
            State.ThreeKingdoms=tk;State.RulesVersion=mode=="cities"?M4.CitiesKnightsGameSession.ThreeKingdomsB5RulesVersion:M5.CombinedGameSession.ThreeKingdomsB5RulesVersion;State.ActivePlayerId="P1";State.Turn=9;State.SetupPlacementIndex=8;State.PendingDecision=null;
            if(mode=="cities")State.Phase=M4.GamePhase.Action;else State.Phase=M5.GamePhase.Action;
            Host=(IThreeKingdomsB5Host)((object)Session).GetType().GetMethod("CardHost",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke((object)Session,new object[]{State})!;
        }
        internal string Vertex => Mode=="cities"?((M4.GameState)State).Tiles.First(t=>t.Resource!="desert").Vertices[0]:((M5.GameState)State).Tiles.First(t=>t.Resource!="sea"&&t.Resource!="desert").Vertices[0];
        internal Edge[] Edges => Mode=="cities"?((M4.PlayerView)Session.GetPlayerView("P1")).Board.Edges:((M5.PlayerView)Session.GetPlayerView("P1")).Board.Edges;
        internal void City(string vertex,string player="P1",bool wall=false){if(State is M4.GameState a){a.Cities=a.Cities.Append(M4CoreHarness.Piece(vertex,player)).ToArray();if(wall)a.Walls=a.Walls.Append(M4CoreHarness.Piece(vertex,player)).ToArray();}else {var b=(M5.GameState)State;b.Cities=b.Cities.Append(M5Harness.Piece(vertex,player)).ToArray();if(wall)b.Walls=b.Walls.Append(M5Harness.Piece(vertex,player)).ToArray();}}
        internal void Settlement(string vertex,string player="P1"){if(State is M4.GameState a)a.Settlements=a.Settlements.Append(M4CoreHarness.Piece(vertex,player)).ToArray();else{var b=(M5.GameState)State;b.Settlements=b.Settlements.Append(M5Harness.Piece(vertex,player)).ToArray();}}
        internal void Road(string edge,string player="P1"){if(State is M4.GameState a)a.Roads=a.Roads.Append(M4CoreHarness.Piece(edge,player)).ToArray();else{var b=(M5.GameState)State;b.Roads=b.Roads.Append(M5Harness.Piece(edge,player)).ToArray();}}
        internal dynamic Player(string id="P1")=>State is M4.GameState a?(object)a.Players.Single(x=>x.Id==id):((M5.GameState)State).Players.Single(x=>x.Id==id);
        internal void General(string id){var tk=Host.State;var current=tk.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P1");var next=tk.Generals.Single(g=>g.GeneralId==id);(current.GeneralId,next.GeneralId)=(next.GeneralId,current.GeneralId);(current.InstanceId,next.InstanceId)=(next.InstanceId,current.InstanceId);tk.Automation.Asked=GeneralCatalog.Get(id).Skills.Select(x=>tk.Clock.PhaseId+":"+current.TenureId+":"+x.Id).ToArray();}
        internal void Balance(){if(State is M4.GameState a)M4CoreHarness.Balance(a);else M5Harness.Balance((M5.GameState)State);}
        internal void Check(){((object)Session).GetType().GetField("state",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue((object)Session,State);Session.AssertInvariants();}
        internal void Refresh(){State=Session.GetAuthoritativeStateForTesting();Host=(IThreeKingdomsB5Host)((object)Session).GetType().GetMethod("CardHost",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke((object)Session,new object[]{State})!;}
        internal void Execute(string kind,string target,string player="P1")
        {
            dynamic command=Mode=="cities"?(object)new M4.Command{Kind=Enum.Parse<M4.CommandKind>(kind),PlayerId=player,TargetId=target}:new M5.Command{Kind=Enum.Parse<M5.CommandKind>(kind),PlayerId=player,TargetId=target};
            command.Id=ThreeKingdomsTestGame.NextId();command.WindowId=((ThreeKingdomsState)State.ThreeKingdoms).WindowId;dynamic r=Session.Execute(command);Assert.True((bool)r.Success,(string)r.ErrorCode+":"+(string)r.Message);Session.AssertInvariants();Refresh();
        }
        internal object? Invoke(string method,params object[] args)=>((object)Session).GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke((object)Session,args);
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void CityCollapsePreservesStockAndRestoresOnlyTheCityWithoutWall(string mode)
    {
        var c=new BoardCase(mode);c.City(c.Vertex,wall:true);c.Balance();int score=c.Host.GetScore("P1");var option=c.Host.GetBoardOptions("P2","liegong").Single(o=>o.AssetKind=="City");
        c.Host.ApplyBoardOption("P2",option);Assert.Equal(score-1,c.Host.GetScore("P1"));Assert.Single(c.Host.GetBoardAssets("P1"),a=>a.Kind=="City"&&a.Damaged);Assert.DoesNotContain(c.Host.GetBoardAssets(),a=>a.Kind=="Wall");
        Assert.Empty(c.Host.GetBoardOptions("P1","free-City"));c.Host.ApplyBoardOption("P1",c.Host.GetBoardOptions("P1","repair").Single());Assert.Equal(score,c.Host.GetScore("P1"));Assert.DoesNotContain(c.Host.GetBoardAssets(),a=>a.Damaged||a.Kind=="Wall");c.Check();
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void CollapsedVillageKeepsOccupancyButLosesProductionAndScore(string mode)
    {
        var c=new BoardCase(mode);c.Settlement(c.Vertex);c.Balance();Assert.NotEmpty(c.Host.GetProducingTileIds("P1"));c.Host.ApplyBoardOption("P2",c.Host.GetBoardOptions("P2","liegong").Single(o=>o.AssetKind=="Settlement"));
        Assert.Equal(0,c.Host.GetScore("P1"));Assert.Empty(c.Host.GetProducingTileIds("P1"));Assert.Empty(c.Host.GetHarvestTiles("P1"));Assert.Single(c.Host.GetBoardAssets("P1"),a=>a.Kind=="Settlement"&&a.Damaged);c.Check();
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void OpenRoadReturnsStockClosedRoadRemainsAndRequiresRepair(string mode)
    {
        var c=new BoardCase(mode);var tile=mode=="cities"?((M4.GameState)c.State).Tiles.First(t=>t.Resource!="desert"):((M5.GameState)c.State).Tiles.First(t=>t.Resource!="sea"&&t.Resource!="desert");var loop=c.Edges.Where(e=>e.Vertices.All(tile.Vertices.Contains)).ToArray();Assert.Equal(6,loop.Length);foreach(var e in loop)c.Road(e.Id);c.Balance();
        var before=c.Host.GetBoardAssets("P1");Assert.All(before,a=>Assert.False(a.Open));int roadStock=(int)c.Player().Pieces.Roads;c.Host.ApplyBoardOption("P2",c.Host.GetBoardOptions("P2","liegong-road").First());
        Assert.Equal(roadStock,(int)c.Player().Pieces.Roads);Assert.Single(c.Host.GetBoardAssets(),a=>a.Damaged);Assert.Empty(c.Host.GetBoardOptions("P1","free-Road"));
        c.Player().Resources.Wood=1;c.Player().Resources.Brick=1;c.Balance();c.Host.ApplyBoardOption("P1",c.Host.GetBoardOptions("P1","repair-own").Single());Assert.Equal(0,(int)c.Player().Resources.Total);Assert.DoesNotContain(c.Host.GetBoardAssets(),a=>a.Damaged);c.Check();
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void SuppressionKeepsActualLevelsAndPermanentDowngradeSurvivesRestore(string mode)
    {
        var c=new BoardCase(mode);c.City(c.Vertex);c.Player().Improvements=new[]{3,2,1};c.Balance();c.Host.ChangeImprovement("P1",0,-2,true);Assert.Equal(new[]{1,2,1},c.Host.GetImprovements("P1"));Assert.Equal(new[]{3,2,1},c.Host.GetActualImprovements("P1"));
        c.Host.ChangeImprovement("P1",0,-1);Assert.Equal(2,c.Host.GetActualImprovements("P1")[0]);c.Host.RestoreImprovements("P1");Assert.Equal(new[]{2,2,1},c.Host.GetImprovements("P1"));c.Check();
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void FreeConstructionIsPreviewPureAndRecordsZeroPaidCost(string mode)
    {
        var c=new BoardCase(mode);c.Settlement(c.Vertex);c.Balance();string before=Serialize((object)c.State);var options=c.Host.GetBoardOptions("P1","free-Road");Assert.NotEmpty(options);Assert.Equal(before,Serialize((object)c.State));
        c.Host.ApplyBoardOption("P1",options[0]);Assert.Single(c.Host.GetBoardAssets("P1"),a=>a.Kind=="Road"&&a.PaidCount==0);Assert.Equal(0,c.Host.GetBoardOptions("P1","kurou").First(o=>o.AssetKind=="Road").PaidCount);c.Check();
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void CollapseProtectionDoesNotPreventDirectCityRemoval(string mode)
    {
        var c=new BoardCase(mode);c.City(c.Vertex);c.Balance();c.Host.SetCollapseProtection("P1");Assert.DoesNotContain(c.Host.GetBoardOptions("P2","liegong"),o=>o.AssetKind=="City");var burn=c.Host.GetBoardOptions("P2","burn-city","P1").Single();c.Host.ApplyBoardOption("P2",burn);Assert.Empty(c.Host.GetBoardAssets("P1"));Assert.Equal(4,(int)c.Player().Pieces.Cities);c.Check();
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void VirtualInvasionPreservesRealTrackAndFirstAttackFlag(string mode)
    {
        var c=new BoardCase(mode);c.State.BarbarianPosition=4;c.Host.ResolveInvasion("fixture-virtual",new[]{"P1"},true);Assert.Equal(4,(int)c.State.BarbarianPosition);Assert.False((bool)c.State.FirstBarbarianAttack);Assert.Equal(1,(int)c.Player().DefenderPoints);c.Invoke("ContinueDecisions",c.State);Assert.Equal(4,(int)c.State.BarbarianPosition);Assert.False((bool)c.State.FirstBarbarianAttack);
        c.Host.ResolveInvasion("fixture-virtual",new[]{"P1"},true);Assert.Equal(1,(int)c.Player().DefenderPoints);c.Check();
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void RepairAndTechPreviewRejectStaleOptionWithoutChangingAuthority(string mode)
    {
        var c=new BoardCase(mode);c.Settlement(c.Vertex);c.Balance();c.Host.ApplyBoardOption("P2",c.Host.GetBoardOptions("P2","liegong").Single());var repair=c.Host.GetBoardOptions("P1","repair").Single();c.Host.ApplyBoardOption("P1",repair);string before=Serialize((object)c.State);
        Assert.ThrowsAny<Exception>(()=>c.Host.ApplyBoardOption("P1",repair));Assert.Equal(before,Serialize((object)c.State));c.Check();
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void NativePaidRoadRecordsItsActualCostForKurou(string mode)
    {
        var c=new BoardCase(mode);c.Settlement(c.Vertex);c.Player().Resources.Wood=1;c.Player().Resources.Brick=1;c.Balance();c.Check();string edge=c.Host.GetBoardOptions("P1","free-Road").First().TargetId;c.Execute("BuildRoad",edge);
        var receipt=Assert.Single(c.Host.GetBoardAssets("P1"),a=>a.Kind=="Road");Assert.Equal(2,receipt.PaidCount);var kurou=c.Host.GetBoardOptions("P1","kurou").Single(o=>o.AssetKind=="Road");Assert.Equal(2,kurou.PaidCount);c.Host.ApplyBoardOption("P1",kurou);Assert.DoesNotContain(c.Host.GetBoardAssets(),a=>a.Kind=="Road");Assert.Equal(15,(int)c.Player().Pieces.Roads);c.Check();
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void RealInvasionKeepsSnapshotUntilAllDecisionsThenResetsTrack(string mode)
    {
        var c=new BoardCase(mode);c.City(c.Vertex,"P2");c.Balance();c.Host.ResolveInvasion("real-invasion",new[]{"P1","P2","P3","P4"},false);Assert.Equal(7,(int)c.State.BarbarianPosition);Assert.True((bool)c.State.FirstBarbarianAttack);Assert.Equal(1,(int)c.State.BarbarianAttackStrength);c.Invoke("ContinueDecisions",c.State);c.Check();
        Assert.Equal("PillageCity",(string)c.State.PendingDecision.Kind);c.Execute("ResolveChoice",c.Vertex,"P2");Assert.Equal(0,(int)c.State.BarbarianPosition);Assert.Null(c.State.BarbarianAttackStrength);Assert.Empty(c.Host.State.Board.Invasions);Assert.DoesNotContain(c.Host.GetBoardAssets("P2"),a=>a.Kind=="City");
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void MetropolisRelocationWaitsForCityOwnerAndPreservesOtherCity(string mode)
    {
        var c=new BoardCase(mode);string first=c.Vertex;string second=c.Edges.SelectMany(e=>e.Vertices).Distinct().First(v=>v!=first&&!c.Edges.Any(e=>e.Vertices.Contains(first)&&e.Vertices.Contains(v))&&(mode=="cities"||((M5.GameState)c.State).Tiles.Any(t=>t.Resource!="sea"&&t.Vertices.Contains(v))));c.City(first,"P2");c.City(second,"P2");c.Player("P2").Improvements=new[]{4,0,0};
        if(c.State is M4.GameState a)a.Metropolises=new[]{new M4.Metropolis{Track=M4.ImprovementTrack.Trade,PlayerId="P2",LocationId=first}};else ((M5.GameState)c.State).Metropolises=new[]{new M5.Metropolis{Track=M5.ImprovementTrack.Trade,PlayerId="P2",LocationId=first}};
        c.Balance();c.Host.ApplyBoardOption("P1",c.Host.GetBoardOptions("P1","liegong","City:"+first).Single());Assert.Equal(2,c.Host.GetBoardAssets("P2").Count(a=>a.Kind=="City"&&!a.Damaged));c.Invoke("ContinueDecisions",c.State);c.Check();Assert.Equal("P2",(string)c.State.PendingDecision.PlayerId);Assert.Equal("B5MetropolisRelocate",(string)c.State.PendingDecision.Kind);
        Assert.True(TabletopSelectionPrompt.IsDirectMapDecision((string)c.State.PendingDecision.Kind));
        string clicked;
        if(mode=="cities")
        {
            var owner=(M4.PlayerView)c.Session.GetPlayerView("P2");var action=Assert.Single(owner.LegalActions,a=>a.Kind==M4.CommandKind.ResolveChoice&&!a.Decline);clicked=action.TargetId;Assert.Equal("P2",action.PlayerId);
            Assert.DoesNotContain(((M4.PlayerView)c.Session.GetPlayerView("P1")).LegalActions,a=>a.Kind==M4.CommandKind.ResolveChoice);
        }
        else
        {
            var owner=(M5.PlayerView)c.Session.GetPlayerView("P2");var action=Assert.Single(owner.LegalActions,a=>a.Kind==M5.CommandKind.ResolveChoice&&!a.Decline);clicked=action.TargetId;Assert.Equal("P2",action.PlayerId);
            Assert.DoesNotContain(((M5.PlayerView)c.Session.GetPlayerView("P1")).LegalActions,a=>a.Kind==M5.CommandKind.ResolveChoice);
        }
        Assert.Equal(second,clicked);c.Execute("ResolveChoice",clicked,"P2");Assert.Single(c.Host.GetBoardAssets("P2"),a=>a.Kind=="City"&&a.Damaged);Assert.Single(c.Host.GetBoardAssets("P2"),a=>a.Kind=="City"&&!a.Damaged&&a.LocationId==second);
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void SuppressedTopTierStillCostsActualTierToRemove(string mode)
    {
        var c=new BoardCase(mode);c.City(c.Vertex);c.Player().Improvements=new[]{5,0,0};c.Balance();c.Host.ChangeImprovement("P1",0,-3,true);var option=c.Host.GetBoardOptions("P2","downgrade-tech","P1").Single();Assert.Equal(5,option.Level);Assert.Equal(2,c.Host.GetImprovements("P1")[0]);c.Host.ApplyBoardOption("P2",option);Assert.Equal(4,c.Host.GetActualImprovements("P1")[0]);Assert.Equal(1,c.Host.GetImprovements("P1")[0]);c.Check();
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void YangweiKeepsDefensiveActivationButStillRequiresPaidActionPermission(string mode)
    {
        var c=new BoardCase(mode);c.General("tk-g24");var edge=c.Edges.First(e=>e.Vertices.Contains(c.Vertex));c.Road(edge.Id);c.Player().Resources.Wheat=1;
        if(c.State is M4.GameState a)a.Knights=new[]{new M4.Knight{PlayerId="P1",LocationId=edge.Vertices[0],Level=1,Active=true}};else ((M5.GameState)c.State).Knights=new[]{new M5.Knight{PlayerId="P1",LocationId=edge.Vertices[0],Level=1,Active=true}};c.Balance();c.Check();
        dynamic command=mode=="cities"?(object)new M4.Command{Kind=M4.CommandKind.MoveKnight,PlayerId="P1",SourceId=edge.Vertices[0],TargetId=edge.Vertices[1]}:new M5.Command{Kind=M5.CommandKind.MoveKnight,PlayerId="P1",SourceId=edge.Vertices[0],TargetId=edge.Vertices[1]};command.Id=ThreeKingdomsTestGame.NextId();command.WindowId=c.Host.State.WindowId;dynamic r=c.Session.Execute(command);Assert.True((bool)r.Success,(string)r.Message);c.Refresh();
        Assert.True(c.Host.GetBoardAssets("P1").Single(a=>a.Kind=="Knight").Active);Assert.False((bool)c.State.Knights[0].Active);c.Execute("ActivateKnight",edge.Vertices[1]);Assert.Equal(0,(int)c.Player().Resources.Wheat);Assert.True((bool)c.State.Knights[0].Active);
        command.Id=ThreeKingdomsTestGame.NextId();command.SourceId=edge.Vertices[1];command.TargetId=edge.Vertices[0];command.WindowId=c.Host.State.WindowId;r=c.Session.Execute(command);Assert.False((bool)r.Success);c.Session.AssertInvariants();
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void ShanqiAndWushuangChangeEffectiveLevelWithoutChangingPhysicalSupply(string mode)
    {
        var c=new BoardCase(mode);c.General("tk-g02");var edge=c.Edges.First(e=>e.Vertices.Contains(c.Vertex));c.Road(edge.Id);if(c.State is M4.GameState a)a.Knights=new[]{new M4.Knight{PlayerId="P1",LocationId=edge.Vertices[0],Level=1,Active=true}};else ((M5.GameState)c.State).Knights=new[]{new M5.Knight{PlayerId="P1",LocationId=edge.Vertices[0],Level=1,Active=true}};
        c.Host.State.Military.PermanentShanqiPlayers=new[]{"P1"};c.Balance();var knight=c.Host.GetBoardAssets("P1").Single(a=>a.Kind=="Knight");Assert.Equal(1,knight.Level);Assert.Equal(5,knight.EffectiveLevel);var hire=c.Host.GetBoardOptions("P2","liyu").First(o=>o.PayloadJson==null);Assert.Equal(5,hire.Cost[3]);c.Host.ApplyBoardOption("P2",hire);Assert.Single(c.Host.GetBoardAssets("P1"),a=>a.Kind=="Knight"&&a.Level==1);Assert.Empty(c.Host.GetBoardOptions("P2","liyu"));c.Check();
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void ProductAiChoosesAffordableRepairAndDoesNotRepeatItAfterRepair(string mode)
    {
        var c=new BoardCase(mode);var tile=mode=="cities"?((M4.GameState)c.State).Tiles.First(t=>t.Resource!="desert"):((M5.GameState)c.State).Tiles.First(t=>t.Resource!="sea"&&t.Resource!="desert");foreach(var edge in c.Edges.Where(e=>e.Vertices.All(tile.Vertices.Contains)))c.Road(edge.Id);c.Player().Resources.Wood=1;c.Player().Resources.Brick=1;c.Balance();c.Host.ApplyBoardOption("P2",c.Host.GetBoardOptions("P2","liegong-road").First());c.Check();
        dynamic command=mode=="cities"?(object)new Catan.AI.CitiesKnightsAi().Decide((M4.PlayerView)c.Session.GetPlayerView("P1")):new Catan.AI.CombinedAi().Decide((M5.PlayerView)c.Session.GetPlayerView("P1"));Assert.Equal("b5-repair",(string)command.SkillCommand.Option);command.Id=ThreeKingdomsTestGame.NextId();dynamic result=c.Session.Execute(command);Assert.True((bool)result.Success,(string)result.Message);c.Session.AssertInvariants();c.Refresh();Assert.DoesNotContain(c.Host.GetBoardAssets(),a=>a.Damaged);B2SkillAction[] actions=c.Session.GetPlayerView("P1").ThreeKingdoms.SkillActions;Assert.DoesNotContain(actions,a=>a.Command.Option=="b5-repair");
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void OffTurnTokenControlRejectsTurnOwnerAndReturnsControlAfterSteal(string mode)
    {
        var c=new BoardCase(mode);c.Settlement(c.Vertex,"P3");c.Player("P3").Resources.Wood=1;c.Host.State.Board.TokenController="P2";c.State.FirstBarbarianAttack=true;
        if(c.State is M4.GameState a){a.RobberTileId=a.Tiles.First(t=>t.Vertices.Contains(c.Vertex)).Id;a.Phase=M4.GamePhase.RobberSteal;a.PendingDecision=new M4.PendingDecision{Kind="Steal",PlayerId="P2",ReturnPhase=M4.GamePhase.Action,EligibleVictimIds=new[]{"P3"}};}
        else {var b=(M5.GameState)c.State;b.RobberTileId=b.Tiles.First(t=>t.Resource!="sea"&&t.Vertices.Contains(c.Vertex)).Id;b.PirateTileId=b.Tiles.First(t=>t.Resource=="sea").Id;b.Phase=M5.GamePhase.RobberSteal;b.PendingDecision=new M5.PendingDecision{Kind="Steal",PlayerId="P2",ReturnPhase=M5.GamePhase.Action,EligibleVictimIds=new[]{"P3"}};}
        c.Balance();c.Check();dynamic command=mode=="cities"?(object)new M4.Command{Kind=M4.CommandKind.StealResource,PlayerId="P1",OtherPlayerId="P3"}:new M5.Command{Kind=M5.CommandKind.StealResource,PlayerId="P1",OtherPlayerId="P3"};command.Id=ThreeKingdomsTestGame.NextId();command.WindowId=c.Host.State.WindowId;string before=c.Session.Save();dynamic r=c.Session.Execute(command);Assert.False((bool)r.Success);Assert.Equal(before,(string)c.Session.Save());
        command.Id=ThreeKingdomsTestGame.NextId();command.PlayerId="P2";r=c.Session.Execute(command);Assert.True((bool)r.Success,(string)r.Message);c.Session.AssertInvariants();c.Refresh();Assert.Null(c.Host.State.Board.TokenController);Assert.Equal("P1",(string)c.State.ActivePlayerId);Assert.Equal(1,(int)c.Player("P2").Resources.Wood);Assert.Equal(0,(int)c.Player("P3").Resources.Wood);
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void ImmuneWeakestDefenderDoesNotRedirectPillageToNextWeakestPlayer(string mode)
    {
        var c=new BoardCase(mode);string first=c.Vertex;string second=c.Edges.SelectMany(e=>e.Vertices).Distinct().First(v=>v!=first&&!c.Edges.Any(e=>e.Vertices.Contains(first)&&e.Vertices.Contains(v))&&(mode=="cities"||((M5.GameState)c.State).Tiles.Any(t=>t.Resource!="sea"&&t.Vertices.Contains(v))));string knight=c.Edges.SelectMany(e=>e.Vertices).First(v=>v!=first&&v!=second);c.City(first);c.City(second,"P2");
        if(c.State is M4.GameState a)a.Knights=new[]{new M4.Knight{PlayerId="P2",LocationId=knight,Level=1,Active=true}};else ((M5.GameState)c.State).Knights=new[]{new M5.Knight{PlayerId="P2",LocationId=knight,Level=1,Active=true}};c.Balance();c.Host.SetCollapseProtection("P1");c.Host.ResolveInvasion("weakest-protected",new[]{"P1","P2"},true);
        Assert.Equal(2,c.Host.GetBoardAssets().Count(a=>a.Kind=="City"));Assert.DoesNotContain((IEnumerable<dynamic>)c.State.DecisionQueue,d=>(string)d.Kind=="PillageCity");c.Invoke("ContinueDecisions",c.State);Assert.Equal(2,c.Host.GetBoardAssets().Count(a=>a.Kind=="City"));c.Check();
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void DiplomacyRemovingDamagedOpenRoadClearsDamageAndRefundReceipt(string mode)
    {
        var c=new BoardCase(mode);var edge=c.Edges.First(e=>e.Vertices.Contains(c.Vertex));c.Settlement(c.Vertex,"P2");c.Road(edge.Id,"P2");
        c.Host.State.Board.Damages=new[]{new B5Damage{PlayerId="P2",Kind="Road",LocationId=edge.Id,Priority=true}};
        c.Host.State.Board.Receipts=new[]{new B5ConstructionReceipt{PlayerId="P2",AssetId="Road:"+edge.Id,Level=1,PaidCount=2}};
        if(c.State is M4.GameState a)a.Players[0].ProgressCards=new[]{M4.ProgressCardKind.Diplomacy};else ((M5.GameState)c.State).Players[0].ProgressCards=new[]{M5.ProgressCardKind.Diplomacy};
        c.Balance();c.Check();dynamic command=mode=="cities"?(object)new M4.Command{Kind=M4.CommandKind.PlayProgressCard,PlayerId="P1",ProgressCard=M4.ProgressCardKind.Diplomacy,TargetId=edge.Id}:new M5.Command{Kind=M5.CommandKind.PlayProgressCard,PlayerId="P1",ProgressCard=M5.ProgressCardKind.Diplomacy,TargetId=edge.Id};
        command.Id=ThreeKingdomsTestGame.NextId();command.WindowId=c.Host.State.WindowId;dynamic result=c.Session.Execute(command);Assert.True((bool)result.Success,(string)result.Message);c.Refresh();c.Check();
        Assert.Empty(c.Host.State.Board.Damages);Assert.Empty(c.Host.State.Board.Receipts);Assert.Equal(15,(int)c.Player("P2").Pieces.Roads);Assert.Empty(c.Host.GetBoardOptions("P2","repair-own"));
        c.Host.ApplyBoardOption("P2",c.Host.GetBoardOptions("P2","free-Road").Single(o=>o.TargetId==edge.Id));
        Assert.Equal(0,c.Host.GetBoardAssets("P2").Single(a=>a.Kind=="Road").PaidCount);Assert.Equal(14,(int)c.Player("P2").Pieces.Roads);c.Check();
    }
    [Fact]
    public void DiplomacyRemovingDamagedShipReturnsSupplyAndClearsPriorityAndReceipt()
    {
        var c=new BoardCase("combined");var s=(M5.GameState)c.State;var edge=c.Edges.First(e=>s.Tiles.Any(t=>t.Resource=="sea"&&e.Vertices.All(t.Vertices.Contains))&&s.Tiles.Any(t=>t.Resource!="sea"&&e.Vertices.All(t.Vertices.Contains)));c.Settlement(edge.Vertices[0],"P2");s.Ships=new[]{new M5.ShipPlacement{PlayerId="P2",LocationId=edge.Id,BuiltTurn=1}};
        c.Host.State.Board.Damages=new[]{new B5Damage{PlayerId="P2",Kind="Ship",LocationId=edge.Id,Priority=true}};c.Host.State.Board.Receipts=new[]{new B5ConstructionReceipt{PlayerId="P2",AssetId="Ship:"+edge.Id,Level=1,PaidCount=2}};s.Players[0].ProgressCards=new[]{M5.ProgressCardKind.Diplomacy};c.Balance();c.Check();
        var command=new M5.Command{Id=ThreeKingdomsTestGame.NextId(),Kind=M5.CommandKind.PlayProgressCard,PlayerId="P1",ProgressCard=M5.ProgressCardKind.Diplomacy,TargetId=edge.Id,WindowId=c.Host.State.WindowId};var result=((M5.CombinedGameSession)c.Session).Execute(command);Assert.True(result.Success,result.Message);c.Refresh();c.Check();
        Assert.Empty(c.Host.State.Board.Damages);Assert.Empty(c.Host.State.Board.Receipts);Assert.Equal(15,(int)c.Player("P2").ShipsRemaining);Assert.Empty(c.Host.GetBoardOptions("P2","repair-own"));
        c.Host.ApplyBoardOption("P2",c.Host.GetBoardOptions("P2","free-Ship").Single(o=>o.TargetId==edge.Id));Assert.Equal(0,c.Host.GetBoardAssets("P2").Single(a=>a.Kind=="Ship").PaidCount);Assert.Equal(14,(int)c.Player("P2").ShipsRemaining);c.Check();
    }
    [Fact]
    public void FortressLossRemovesDamagedShipRecordTogetherWithPhysicalPiece()
    {
        var c=new BoardCase("combined","pirate-islands");var s=(M5.GameState)c.State;var fortress=s.Special.Fortresses.Single(f=>f.PlayerId=="P1");var edge=c.Edges.First(e=>e.Vertices.Contains(fortress.InvasionStartingVertexId)&&s.Tiles.Any(t=>t.Resource=="sea"&&e.Vertices.All(t.Vertices.Contains)));
        s.Ships=new[]{new M5.ShipPlacement{PlayerId="P1",LocationId=edge.Id,BuiltTurn=1,IsInvasionRoute=true}};c.Host.State.Board.Damages=new[]{new B5Damage{PlayerId="P1",Kind="Ship",LocationId=edge.Id,Priority=true}};c.Host.State.Board.Receipts=new[]{new B5ConstructionReceipt{PlayerId="P1",AssetId="Ship:"+edge.Id,Level=1,PaidCount=2}};c.Balance();foreach(var p in s.Players)p.Pieces.Settlements-=s.Special.Fortresses.Count(f=>f.PlayerId==p.Id&&f.Strength>0);c.Check();
        c.Invoke("AttackFortress",s,s.Players[0]);Assert.Empty(s.Ships);Assert.Equal(15,s.Players[0].ShipsRemaining);Assert.Empty(c.Host.State.Board.Damages);Assert.Empty(c.Host.State.Board.Receipts);c.Check();
    }
}
