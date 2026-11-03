using System.Reflection;
using System.Runtime.Serialization.Json;
using Catan.Core;
using Catan.Core.ThreeKingdoms;
using M4=Catan.Core.M4;
using M5=Catan.Core.M5;
using Xunit;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB6SeatTradeIntegrationTests
{
    private static ThreeKingdomsState Generals(string general)
    {
        var tk=ThreeKingdomsEngine.Create(new[]{"P1","P2","P3","P4"},42,_=>0,true,GeneralCatalog.B6PackageVersion);
        while(tk.PendingDecision!=null){var d=tk.PendingDecision;if(d.Kind==ThreeKingdomsDecisionKind.SelectOpening)ThreeKingdomsEngine.ChooseGenerals(tk,d.PlayerId,d.CandidateIds.Take(2).ToArray(),d.DecisionId,d.WindowId,_=>0);else ThreeKingdomsEngine.DeployGeneral(tk,d.PlayerId,d.CandidateIds[0],d.DecisionId,d.WindowId,_=>0);}
        ThreeKingdomsEngine.BeginNormalTurns(tk);tk.Clock.Phase=ThreeKingdomsPhase.Play;tk.Clock.PhaseId=5;tk.Clock.DrawPhaseTurnInstanceId=tk.Clock.TurnInstanceId;
        var requested=new[]{general,"tk-g12","tk-g09","tk-g22"};
        for(int i=0;i<requested.Length;i++){var g=tk.Generals.Single(x=>x.Region==GeneralRegion.Active&&x.OwnerPlayerId=="P"+(i+1));var other=tk.Generals.Single(x=>x.GeneralId==requested[i]);(g.GeneralId,other.GeneralId)=(other.GeneralId,g.GeneralId);(g.InstanceId,other.InstanceId)=(other.InstanceId,g.InstanceId);g.Counters=Array.Empty<GeneralCounter>();}
        tk.Skills=new B2SkillState();tk.CivilB5=new B5CivilState();tk.Board=new B5BoardState();tk.Response=null;tk.Automation=new B4AutomationState{TurnId=tk.Clock.TurnInstanceId};return tk;
    }
    private static dynamic Fixture(string mode,string general,Action<dynamic> configure)
    {
        if(mode=="cities")return M4CoreHarness.Fixture(s=>{s.RulesVersion=M4.CitiesKnightsGameSession.ThreeKingdomsB6RulesVersion;s.ThreeKingdoms=Generals(general);configure(s);});
        return M5Harness.Fixture(s=>{s.RulesVersion=M5.CombinedGameSession.ThreeKingdomsB6RulesVersion;s.ThreeKingdoms=Generals(general);configure(s);});
    }
    private static dynamic Player(dynamic s,string id="P1")=>s is M4.GameState a?(object)a.Players.Single(p=>p.Id==id):((M5.GameState)s).Players.Single(p=>p.Id==id);
    private static dynamic Cmd(dynamic game,string mode,string kind,string player="P1")
    {dynamic c=mode=="cities"?(object)new M4.Command{Kind=Enum.Parse<M4.CommandKind>(kind),PlayerId=player}:new M5.Command{Kind=Enum.Parse<M5.CommandKind>(kind),PlayerId=player};c.Id=ThreeKingdomsTestGame.NextId();c.WindowId=game.GetPlayerView(player).ThreeKingdoms.WindowId;return c;}
    private static void Accept(dynamic game,dynamic c){dynamic r=game.Execute(c);Assert.True((bool)r.Success,(string)r.ErrorCode+":"+(string)r.Message);game.AssertInvariants();}
    private static dynamic Skill(dynamic game,string mode,string option,int type=-1)
    {B2SkillAction[] actions=game.GetPlayerView("P1").ThreeKingdoms.SkillActions;var a=actions.First(a=>a.Command.Option==option&&(type<0||a.Command.CardType==type)&&(a.Command.TargetPlayerIds.Length!=1||a.Command.TargetPlayerIds[0]=="P2"));dynamic c=Cmd(game,mode,"UseGeneralSkill");c.SkillCommand=a.Command;Accept(game,c);return c;}
    private static dynamic Restore(dynamic game,string mode)
    {
        object s=game.GetAuthoritativeStateForTesting();using var stream=new MemoryStream();var serializer=new DataContractJsonSerializer(s.GetType());serializer.WriteObject(stream,s);stream.Position=0;var copy=serializer.ReadObject(stream)!;
        object restored=mode=="cities"?(object)M4CoreHarness.New():M5Harness.New();restored.GetType().GetField("state",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(restored,copy);return restored;
    }
    private static string[] LandVertices(dynamic s)
    {if(s is M4.GameState a)return a.Tiles.First(t=>t.Resource!="sea").Vertices;return ((M5.GameState)s).Tiles.First(t=>t.Resource!="sea"&&t.Number.HasValue).Vertices;}
    private static void Settlements(dynamic s,params (string vertex,string owner)[] pieces)
    {if(s is M4.GameState a)a.Settlements=pieces.Select(p=>M4CoreHarness.Piece(p.vertex,p.owner)).ToArray();else ((M5.GameState)s).Settlements=pieces.Select(p=>M5Harness.Piece(p.vertex,p.owner)).ToArray();}

    [Theory][InlineData("cities")][InlineData("combined")]
    public void RealTerritoryFuqiExchangesMixedCommoditiesAndRestoresPending(string mode)
    {
        dynamic game=Fixture(mode,"tk-g46",s=>{string[] vertices=LandVertices(s);Settlements(s,(vertices[0],"P1"),(vertices[3],"P2"));Player(s).Resources.Wood=1;Player(s,"P2").Commodities.Cloth=1;Player(s,"P2").Commodities.Coin=1;});
        Skill(game,mode,"seat-fuqi");Skill(game,mode,"seat-fuqi-give",0);Skill(game,mode,"seat-fuqi-request",5);Skill(game,mode,"seat-fuqi-request",6);
        dynamic restored=Restore(game,mode);dynamic c=Skill(game,mode,"seat-fuqi-submit");Accept(restored,c);Assert.Equal((string)game.Save(),(string)restored.Save());
        dynamic state=game.GetAuthoritativeStateForTesting();Assert.Equal(2,(int)Player(state).Commodities.Total);Assert.Equal(1,(int)Player(state,"P2").Resources.Wood);
    }
    [Theory][InlineData("cities",1)][InlineData("combined",1)]
    public void NativeRoadWaitsForJiaoziResourceBeforePaymentAndRestores(string mode,int adjustment)
    {
        string anchor="";dynamic game=Fixture(mode,"tk-g46",s=>{anchor=LandVertices(s)[0];Settlements(s,(anchor,"P1"));Player(s).Resources.Wood=2;Player(s).Resources.Brick=1;if(adjustment<0)foreach(string id in new[]{"P2","P3","P4"})Player(s,id).Resources.Wheat=4;});
        dynamic view=game.GetPlayerView("P1");Catan.Core.Edge[] edges=view.Board.Edges;string road=edges.First(e=>e.Vertices.Contains(anchor)).Id;
        dynamic build=Cmd(game,mode,"BuildRoad");build.TargetId=road;string before=game.Save();Assert.True((bool)game.Preview(build).Success);Assert.Equal(before,(string)game.Save());Accept(game,build);
        dynamic state=game.GetAuthoritativeStateForTesting();Assert.Equal(0,(int)state.Roads.Length);Assert.Equal(3,(int)Player(state).Resources.Total);Assert.Equal("seat-jiaozi-card",(string)state.ThreeKingdoms.Skills.PendingDecision.Kind);
        dynamic restored=Restore(game,mode);dynamic c=Skill(game,mode,"seat-jiaozi-card",0);Accept(restored,c);Assert.Equal((string)game.Save(),(string)restored.Save());state=game.GetAuthoritativeStateForTesting();
        Assert.Equal(1,(int)state.Roads.Length);Assert.Equal(3-(2+adjustment),(int)Player(state).Resources.Total);ThreeKingdomsState tk=state.ThreeKingdoms;Assert.Equal(2+adjustment,tk.Board.Receipts.Single(r=>r.AssetId=="Road:"+road).PaidCount);
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void NativeDiscountMedicineAppliesJiaoziAfterItsDiscount(string mode)
    {
        string site="";dynamic game=Fixture(mode,"tk-g46",s=>{site=LandVertices(s)[0];Settlements(s,(site,"P1"));Player(s).Resources.Wheat=2;Player(s).Resources.Ore=2;
            if(s is M4.GameState a)a.Players[0].ProgressCards=new[]{M4.ProgressCardKind.Medicine};else((M5.GameState)s).Players[0].ProgressCards=new[]{M5.ProgressCardKind.Medicine};});
        dynamic build=Cmd(game,mode,"PlayProgressCard");if(mode=="cities")build.ProgressCard=M4.ProgressCardKind.Medicine;else build.ProgressCard=M5.ProgressCardKind.Medicine;build.TargetId=site;Accept(game,build);Skill(game,mode,"seat-jiaozi-card",3);
        dynamic state=game.GetAuthoritativeStateForTesting();Assert.Equal(1,(int)state.Cities.Length);Assert.Equal(0,(int)Player(state).Resources.Total);Assert.Equal(0,(int)Player(state).ProgressCards.Length);
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void JingluePrivateHandAndNativeBlockedGainDoNotMoveSourceCards(string mode)
    {
        dynamic game=Fixture(mode,"tk-g48",s=>{Player(s,"P2").Resources.Wood=2;Player(s).Resources.Brick=4;});Skill(game,mode,"seat-jinglue");
        foreach(string viewer in new[]{"P1","P2","P3"}){ThreeKingdomsView view=game.GetPlayerView(viewer).ThreeKingdoms;Assert.Equal(viewer=="P1",view.MilitaryHands?.Any(h=>h.PlayerId=="P2"&&!h.Public)==true);}
        dynamic restored=Restore(game,mode);dynamic take=Skill(game,mode,"seat-jinglue-take",0);Accept(restored,take);Assert.Equal((string)game.Save(),(string)restored.Save());
        // 景略玩家可继续取得该真实牌种；其他玩家即使持有足够支付牌也不得银行换入。
        dynamic state=game.GetAuthoritativeStateForTesting();state.ActivePlayerId="P2";state.ThreeKingdoms.Clock.ActivePlayerId="P2";Player(state,"P2").Resources.Brick=4;
        if(state is M4.GameState a){M4CoreHarness.Balance(a);M4CoreHarness.SetState((M4.CitiesKnightsGameSession)game,a);}else {M5Harness.Balance((M5.GameState)state);M5Harness.SetState((M5.CombinedGameSession)game,(M5.GameState)state);}
        dynamic trade=Cmd(game,mode,"BankTrade","P2");trade.GiveResource=Resource.Brick;trade.ReceiveResource=Resource.Wood;trade.GiveAmount=4;trade.ReceiveAmount=1;
        string snapshot=game.Save();Assert.False((bool)game.Execute(trade).Success);Assert.Equal(snapshot,(string)game.Save());
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void JiaoziBuildsKnightDirectlyWithOnlyOre(string mode)
    {
        string anchor="";dynamic game=Fixture(mode,"tk-g46",s=>{anchor=LandVertices(s)[0];Settlements(s,(anchor,"P1"));Player(s).Resources.Ore=1;foreach(string id in new[]{"P2","P3","P4"})Player(s,id).Resources.Wheat=2;});
        Catan.Core.Edge[] edges=game.GetPlayerView("P1").Board.Edges;var edge=edges.First(e=>e.Vertices.Contains(anchor));
        dynamic state=game.GetAuthoritativeStateForTesting();
        if(state is M4.GameState a)a.Roads=new[]{M4CoreHarness.Piece(edge.Id)};else ((M5.GameState)state).Roads=new[]{M5Harness.Piece(edge.Id)};
        if(state is M4.GameState balanced)M4CoreHarness.Balance(balanced);else M5Harness.Balance((M5.GameState)state);
        game.GetType().GetField("state",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(game,state);
        dynamic command=Cmd(game,mode,"RecruitKnight");command.TargetId=edge.Vertices.First(v=>v!=anchor);
        dynamic preview=game.Preview(command);Assert.True((bool)preview.Success,(string)preview.Message);
        dynamic restored=Restore(game,mode);Accept(game,command);Accept(restored,command);
        Assert.Equal((string)game.Save(),(string)restored.Save());state=game.GetAuthoritativeStateForTesting();
        Assert.Equal(1,(int)state.Knights.Length);Assert.Equal(0,(int)Player(state).Resources.Total);Assert.Null(state.ThreeKingdoms.Skills.PendingDecision);
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void QicePrintsOneCardPerKindWithoutTargetsAndRestores(string mode)
    {
        dynamic game=Fixture(mode,"tk-g34",s=>{Player(s).Resources.Brick=1;s.ThreeKingdoms.Clock.DrawPhaseTurnInstanceId=0;s.ThreeKingdoms.Clock.Phase=ThreeKingdomsPhase.Preparation;if(s is M4.GameState a)a.Phase=M4.GamePhase.ProductionAwaitRoll;else ((M5.GameState)s).Phase=M5.GamePhase.ProductionAwaitRoll;});
        dynamic state=game.GetAuthoritativeStateForTesting();game.GetType().GetMethod("PumpB4",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(game,new object[]{state});game.GetType().GetField("state",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(game,state);
        B2SkillAction[] actions=game.GetPlayerView("P1").ThreeKingdoms.SkillActions;
        var choices=actions.Where(a=>a.Command.Option=="b45-optional-qice").ToArray();
        Assert.Equal(Enum.GetValues<M4.ProgressCardKind>().Length,choices.Length);Assert.All(choices,a=>Assert.Empty(a.SelectionBoardIds??Array.Empty<string>()));
        dynamic command=Cmd(game,mode,"UseGeneralSkill");command.SkillCommand=choices.Single(a=>a.VirtualCardId=="Medicine").Command;
        dynamic preview=game.Preview(command);Assert.True((bool)preview.Success,(string)preview.Message);
        dynamic restored=Restore(game,mode);Accept(game,command);Accept(restored,command);Assert.Equal((string)game.Save(),(string)restored.Save());
        state=game.GetAuthoritativeStateForTesting();Assert.Equal(0,(int)Player(state).Resources.Brick);Assert.Equal(1,(int)Player(state).ProgressCards.Length);Assert.Equal(1,(int)Player(state).PrintedProgressCards.Length);
        // 无城市目标、弃完费用仍能获得药物；牌库不减少。
        Assert.Equal(54,((System.Collections.IEnumerable)state.ProgressDecks).Cast<dynamic>().Sum(d=>(int)d.Cards.Length));
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void PrintedCardsSurviveSaveAndDisappearAfterUseOrDiscard(string mode)
    {
        dynamic game=Fixture(mode,"tk-g34",s=>{});dynamic state=game.GetAuthoritativeStateForTesting();
        var host=(IThreeKingdomsB4Host)game.GetType().GetMethod("CardHost",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(game,new object[]{state});
        for(int i=0;i<5;i++)host.UseVirtualProgress("P1",host.GetVirtualProgressUses("P1","play").Single(u=>u.CardId=="RoadBuilding"));
        game.GetType().GetField("state",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(game,state);game.AssertInvariants();
        dynamic discard=Cmd(game,mode,"DiscardProgressCard");if(mode=="cities")discard.ProgressCard=M4.ProgressCardKind.RoadBuilding;else discard.ProgressCard=M5.ProgressCardKind.RoadBuilding;
        dynamic restored=Restore(game,mode);Accept(game,discard);Accept(restored,discard);Assert.Equal((string)game.Save(),(string)restored.Save());
        dynamic play=Cmd(game,mode,"PlayProgressCard");if(mode=="cities")play.ProgressCard=M4.ProgressCardKind.RoadBuilding;else play.ProgressCard=M5.ProgressCardKind.RoadBuilding;
        Accept(game,play);if(game.GetAuthoritativeStateForTesting().PendingDecision!=null){dynamic done=Cmd(game,mode,"ResolveProgressChoice");done.Decline=true;Accept(game,done);}
        state=game.GetAuthoritativeStateForTesting();Assert.Equal(3,(int)Player(state).PrintedProgressCards.Length);Assert.Equal(3,(int)Player(state).ProgressCards.Length);
        Assert.Equal(54,((System.Collections.IEnumerable)state.ProgressDecks).Cast<dynamic>().Sum(d=>(int)d.Cards.Length));
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void MiewuPrintsWithoutImmediateTargetsAndKeepsReward(string mode)
    {
        dynamic game=Fixture(mode,"tk-g41",s=>{Player(s).Resources.Brick=1;ThreeKingdomsState tk=s.ThreeKingdoms;tk.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P1").Counters=new[]{new GeneralCounter{Key="wuku",Value=3},new GeneralCounter{Key="sanchen",Value=1}};});
        B2SkillAction[] actions=game.GetPlayerView("P1").ThreeKingdoms.SkillActions;var choices=actions.Where(a=>a.Command.Option=="b4-miewu-progress").ToArray();
        Assert.Equal(23,choices.Length);dynamic command=Cmd(game,mode,"UseGeneralSkill");command.SkillCommand=choices.Single(a=>a.VirtualCardId=="Medicine").Command;Accept(game,command);
        dynamic state=game.GetAuthoritativeStateForTesting();Assert.Equal(1,(int)Player(state).ProgressCards.Length);Assert.Equal(1,(int)Player(state).Resources.Total);Assert.Null(state.PendingDecision);Assert.Equal(1,(int)Player(state).PrintedProgressCards.Length);
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void YichengPrintsBishopWithoutMovingRobber(string mode)
    {
        dynamic game=Fixture(mode,"tk-g32",s=>{Player(s).Resources.Wood=1;Player(s).Resources.Brick=1;});
        string before=game.GetAuthoritativeStateForTesting().RobberTileId;
        Skill(game,mode,"civil-yicheng");Skill(game,mode,"civil-select",0);Skill(game,mode,"civil-select",1);Skill(game,mode,"civil-yicheng-use");
        dynamic state=game.GetAuthoritativeStateForTesting();Assert.Equal(before,(string)state.RobberTileId);Assert.Equal(1,(int)Player(state).ProgressCards.Length);Assert.Equal(0,(int)Player(state).Resources.Total);Assert.Equal(1,(int)Player(state).PrintedProgressCards.Length);
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void PrintedCardTransferPreservesOriginAndNeverReturnsToDeck(string mode)
    {
        dynamic game=Fixture(mode,"tk-g34",s=>{});dynamic state=game.GetAuthoritativeStateForTesting();
        var host=(IThreeKingdomsB4Host)game.GetType().GetMethod("CardHost",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(game,new object[]{state});
        host.UseVirtualProgress("P1",host.GetVirtualProgressUses("P1","play").Single(u=>u.CardId=="RoadBuilding"));
        var card=Assert.Single(host.GetProgressCards("P1"));Assert.True(card.Printed);host.RemoveProgress("P1",card.Id);
        if(state is M4.GameState a)a.ThreeKingdomsProgressResolutions=new[]{new M4.B4ProgressResolution{EventId="printed-capture",PlayerId="P1",Card=card}};
        else ((M5.GameState)state).ThreeKingdomsProgressResolutions=new[]{new M5.B4ProgressResolution{EventId="printed-capture",PlayerId="P1",Card=card}};
        Assert.True(((IThreeKingdomsB4ResponseHost)host).TryCaptureProgressCard("P1","P2",card,"printed-capture"));
        var transferred=Assert.Single(host.GetProgressCards("P2"));Assert.True(transferred.Printed);host.RemoveProgress("P2",transferred.Id);host.ReturnProgress(transferred);
        game.GetType().GetField("state",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(game,state);game.AssertInvariants();
        Assert.Equal(54,((System.Collections.IEnumerable)state.ProgressDecks).Cast<dynamic>().Sum(d=>(int)d.Cards.Length));
    }

}
