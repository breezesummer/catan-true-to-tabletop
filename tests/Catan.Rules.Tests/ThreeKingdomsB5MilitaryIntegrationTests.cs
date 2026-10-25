using System.Reflection;
using System.Runtime.Serialization.Json;
using Catan.Core;
using Catan.Core.ThreeKingdoms;
using Xunit;
using M4=Catan.Core.M4;
using M5=Catan.Core.M5;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB5MilitaryIntegrationTests
{
    private static ThreeKingdomsState Generals(params string[] requested)
    {
        var tk=ThreeKingdomsEngine.Create(new[]{"P1","P2","P3","P4"},42,_=>0,true,GeneralCatalog.B5PackageVersion);
        while(tk.PendingDecision!=null)
        {
            var d=tk.PendingDecision;
            if(d.Kind==ThreeKingdomsDecisionKind.SelectOpening)ThreeKingdomsEngine.ChooseGenerals(tk,d.PlayerId,d.CandidateIds.Take(2).ToArray(),d.DecisionId,d.WindowId,_=>0);
            else ThreeKingdomsEngine.DeployGeneral(tk,d.PlayerId,d.CandidateIds[0],d.DecisionId,d.WindowId,_=>0);
        }
        ThreeKingdomsEngine.BeginNormalTurns(tk);tk.Clock.Phase=ThreeKingdomsPhase.Play;tk.Clock.PhaseId=5;tk.Clock.DrawPhaseTurnInstanceId=tk.Clock.TurnInstanceId;
        for(int i=0;i<requested.Length;i++)
        {
            var g=tk.Generals.Single(x=>x.Region==GeneralRegion.Active&&x.OwnerPlayerId=="P"+(i+1));var desired=tk.Generals.Single(x=>x.GeneralId==requested[i]);
            (g.GeneralId,desired.GeneralId)=(desired.GeneralId,g.GeneralId);(g.InstanceId,desired.InstanceId)=(desired.InstanceId,g.InstanceId);g.Counters=Array.Empty<GeneralCounter>();
        }
        tk.Skills=new B2SkillState();tk.Response=null;tk.Military=new B5MilitaryState();tk.CivilB5=new B5CivilState();
        tk.Automation=new B4AutomationState{TurnId=tk.Clock.TurnInstanceId,Asked=tk.Generals.Where(g=>g.Region==GeneralRegion.Active).SelectMany(g=>GeneralCatalog.Get(g.GeneralId).Skills.Select(s=>tk.Clock.PhaseId+":"+g.TenureId+":"+s.Id)).ToArray()};return tk;
    }
    private static dynamic Fixture(string mode,string general,Action<dynamic>? configure=null)
    {
        string[] list={general,"tk-g12","tk-g09","tk-g22"};
        if(mode=="cities")return M4CoreHarness.Fixture(s=>{s.RulesVersion=M4.CitiesKnightsGameSession.ThreeKingdomsB5RulesVersion;s.ThreeKingdoms=Generals(list);configure?.Invoke(s);});
        return M5Harness.Fixture(s=>{s.RulesVersion=M5.CombinedGameSession.ThreeKingdomsB5RulesVersion;s.ThreeKingdoms=Generals(list);configure?.Invoke(s);});
    }
    private static dynamic Player(dynamic s,string p="P1")=>s is M4.GameState a?(object)a.Players.Single(x=>x.Id==p):((M5.GameState)s).Players.Single(x=>x.Id==p);
    private static dynamic Command(dynamic game,string mode,string kind,string player="P1")
    {dynamic c=mode=="cities"?(object)new M4.Command{Kind=Enum.Parse<M4.CommandKind>(kind),PlayerId=player}:new M5.Command{Kind=Enum.Parse<M5.CommandKind>(kind),PlayerId=player};c.Id=ThreeKingdomsTestGame.NextId();c.WindowId=game.GetPlayerView(player).ThreeKingdoms.WindowId;return c;}
    private static void Accept(dynamic game,dynamic c){dynamic r=game.Execute(c);Assert.True((bool)r.Success,(string)r.ErrorCode+":"+(string)r.Message);game.AssertInvariants();}
    private static void Skill(dynamic game,string mode,string player,string option,Func<B2SkillAction,bool>? predicate=null)
    {B2SkillAction[] a=game.GetPlayerView(player).ThreeKingdoms.SkillActions;var selected=a.First(x=>x.Command.Option==option&&(predicate==null||predicate(x)));dynamic c=Command(game,mode,"UseGeneralSkill",player);c.SkillCommand=selected.Command;Accept(game,c);}
    private static dynamic Restore(dynamic game,string mode)
    {
        object state=game.GetAuthoritativeStateForTesting();using var stream=new MemoryStream();var serializer=new DataContractJsonSerializer(state.GetType());serializer.WriteObject(stream,state);stream.Position=0;
        object restored=mode=="cities"?(object)M4CoreHarness.New():M5Harness.New();restored.GetType().GetField("state",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(restored,serializer.ReadObject(stream));return restored;
    }
    private static object Host(dynamic game,dynamic state)=>game.GetType().GetMethod("CardHost",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(game,new object[]{state})!;
    private static void Put(dynamic game,dynamic state)
    {if(state is M4.GameState a)M4CoreHarness.SetState((M4.CitiesKnightsGameSession)game,a);else M5Harness.SetState((M5.CombinedGameSession)game,(M5.GameState)state);}

    [Theory][InlineData("cities")][InlineData("combined")]
    public void NativeLuanjiAllResistDoesNotMoveRealBarbarianTrackAndRestoresEveryDecision(string mode)
    {
        dynamic game=Fixture(mode,"tk-g10",s=>{s.BarbarianPosition=4;foreach(string p in new[]{"P1","P2","P3","P4"})Player(s,p).Resources.Ore=1;Player(s).Resources.Brick=2;});
        Skill((object)game,mode,"P1","b5m-luanji",a=>a.Command.CardType==1);
        foreach(string player in new[]{"P1","P2","P3","P4"})
        {
            dynamic restored=Restore(game,mode);B2SkillAction[] actions=game.GetPlayerView(player).ThreeKingdoms.SkillActions;dynamic c=Command(game,mode,"UseGeneralSkill",player);c.SkillCommand=actions.First(a=>a.Command.Option=="b5m-resist").Command;
            Accept(game,c);Accept(restored,c);Assert.Equal((string)game.Save(),(string)restored.Save());
        }
        dynamic after=game.GetAuthoritativeStateForTesting();Assert.Equal(4,(int)after.BarbarianPosition);Assert.Equal(4,(int)Player(after).Resources.Wheat);Assert.False((bool)after.FirstBarbarianAttack);Assert.Empty((B5InvasionRecord[])after.ThreeKingdoms.Board.Invasions);
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void NativeYaowuRaisesBothVisibleCapacitiesAndDuplicateCommandDoesNotStack(string mode)
    {
        dynamic game=Fixture(mode,"tk-g24");B2SkillAction[] actions=game.GetPlayerView("P1").ThreeKingdoms.SkillActions;dynamic c=Command(game,mode,"UseGeneralSkill");c.SkillCommand=actions.Single(a=>a.Command.Option=="b5m-yaowu").Command;
        string before=game.Save();Assert.True((bool)game.Preview(c).Success);Assert.Equal(before,(string)game.Save());Accept(game,c);string after=game.Save();Assert.True((bool)game.Execute(c).IsDuplicate);Assert.Equal(after,(string)game.Save());
        dynamic view=game.GetPlayerView("P1");Assert.Equal(6,(int)view.ThreeKingdoms.ProgressHandLimit);var s=game.GetAuthoritativeStateForTesting();Assert.Equal(11,((IThreeKingdomsB5Host)Host(game,s)).GetHandLimit("P1"));
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void NativeChuhaiCompletesBothDiceAndNeverBecomesProduction(string mode)
    {
        dynamic game=Fixture(mode,"tk-g53",s=>{Player(s,"P2").Resources.Brick=2;Player(s,"P3").Resources.Wood=2;s.LastDice1=2;s.LastDice2=4;s.Random.State=(uint)1;ThreeKingdomsState tk=s.ThreeKingdoms;tk.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P1").Counters=new[]{new GeneralCounter{Key="xianghai-tax",Value=2}};});
        Skill((object)game,mode,"P1","b5m-chuhai",a=>a.Command.TargetPlayerIds[0]=="P2");
        ThreeKingdomsState tk=game.GetAuthoritativeStateForTesting().ThreeKingdoms;
        Assert.Null(tk.Military.ComparisonSource);Assert.Equal(B3DicePurpose.Comparison,tk.Dice.Roll.Purpose);Assert.True(tk.Dice.Roll.Submitted);
        dynamic state=game.GetAuthoritativeStateForTesting();Assert.Equal(2,(int)state.LastDice1);Assert.Equal(4,(int)state.LastDice2);Assert.Equal(ThreeKingdomsPhase.Play,tk.Clock.Phase);
        if(tk.Skills.PendingDecision?.Kind=="b5m-chuhai-discard")
        {dynamic saved=Restore(game,mode);B2SkillAction[] actions=game.GetPlayerView("P1").ThreeKingdoms.SkillActions;dynamic c=Command(game,mode,"UseGeneralSkill");c.SkillCommand=actions.First(a=>a.Command.Option=="b5m-chuhai-discard").Command;Accept(game,c);Accept(saved,c);Assert.Equal((string)game.Save(),(string)saved.Save());}
        Assert.DoesNotContain((B4SkillHistoryView[])game.GetPlayerView("P3").ThreeKingdoms.SkillHistory,h=>h.Result.Contains("弃置目标的砖"));
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void NativeWushuangDeploysActivatedKnightAfterStateOnlyLifecycleHook(string mode)
    {
        dynamic game=Fixture(mode,"tk-g02");dynamic s=game.GetAuthoritativeStateForTesting();string edge;string endpoint;
        if(s is M4.GameState a){var e=((M4.PlayerView)game.GetPlayerView("P1")).Board.Edges.First();edge=e.Id;endpoint=e.Vertices[0];a.Roads=new[]{M4CoreHarness.Piece(edge)};M4CoreHarness.Balance(a);}
        else{var b=(M5.GameState)s;var board=((M5.PlayerView)game.GetPlayerView("P1")).Board;var e=board.Edges.First(e=>board.Tiles.Any(t=>t.Resource!="sea"&&e.Vertices.All(t.Vertices.Contains)));edge=e.Id;endpoint=e.Vertices[0];b.Roads=new[]{M5Harness.Piece(edge)};M5Harness.Balance(b);}
        ThreeKingdomsState tk=s.ThreeKingdoms;var general=tk.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P1");B2SkillEngine.OnB5MilitaryGeneralActivated(tk,general);B2SkillEngine.Pump((IThreeKingdomsCardHost)Host(game,s));Put(game,s);
        Skill((object)game,mode,"P1","b5m-deploy");dynamic after=game.GetAuthoritativeStateForTesting();Assert.Single((Array)after.Knights);var knight=((Array)after.Knights).GetValue(0)!;Assert.Equal(true,knight.GetType().GetProperty("Active")!.GetValue(knight));Assert.Equal(1,knight.GetType().GetProperty("Level")!.GetValue(knight));
    }
}
