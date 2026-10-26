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
    [Theory][InlineData("cities",1)][InlineData("combined",1)][InlineData("cities",-1)][InlineData("combined",-1)]
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
}
