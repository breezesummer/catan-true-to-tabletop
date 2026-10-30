using System.Reflection;
using System.Text.Json;
using Catan.Core;
using Catan.Core.TradeModules;
using Xunit;
using M3=Catan.Core.M3;
using M5=Catan.Core.M5;

namespace Catan.Rules.Tests;

// FSF.pdf 第2—3页；F0 D05及F5地图适配，固定案例独立于产品AI完整局。
public sealed class FishingThirdWaveTests(Xunit.Abstractions.ITestOutputHelper output)
{
    public static readonly string[] Maps=["forgotten-tribe","cloth-for-catan","wonders-of-catan","new-world"];
    public static IEnumerable<object[]> Configurations()
    {
        foreach(var map in Maps) foreach(int seats in new[]{3,4}) foreach(bool combined in new[]{false,true})
            foreach(bool kingdoms in combined?new[]{false,true}:new[]{false}) foreach(bool harbor in new[]{false,true})
                yield return new object[]{map,seats,combined,kingdoms,harbor};
    }
    [Theory][MemberData(nameof(Configurations))]
    public void EveryThirdWaveConfigurationCompletesAndReplays(string map,int seats,bool combined,bool kingdoms,bool harbor)
        => new FishingSeafaringTests(output).EveryOpenConfigurationCompletesAndReplays(map,seats,combined,kingdoms,harbor);

    static dynamic Create(string map,bool combined,int seats=4,uint seed=97,bool fishing=true,bool harbor=true,int? target=null)
        => combined ? M5.CombinedGameSession.Create(map,seats,seed,target,tradeModules:new(){Fishing=fishing,Harbormaster=harbor}) : M3.SeafarersGameSession.Create(map,seats,seed,target,tradeModules:new(){Fishing=fishing,Harbormaster=harbor});
    static object? Invoke(object game,string method,params object[] args)=>game.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(game,args);
    static void Set(object game,object state)
    {
        if(state is M3.GameState a){M3Harness.Balance(a);M3Harness.SetState((M3.SeafarersGameSession)game,a);}
        else {M5Harness.Balance((M5.GameState)state);M5Harness.SetState((M5.CombinedGameSession)game,(M5.GameState)state);}
    }
    static dynamic ActionState(dynamic game,bool combined)
    {
        dynamic s=game.GetAuthoritativeStateForTesting();s.Turn=9;s.SetupPlacementIndex=8;s.ActivePlayerId="P1";s.PendingDecision=null;
        if(combined)s.Phase=M5.GamePhase.Action;else s.Phase=M3.GamePhase.Action;
        return s;
    }
    static dynamic Command(bool combined,dynamic state,string kind)
    {
        dynamic c=combined?(object)new M5.Command {Kind=Enum.Parse<M5.CommandKind>(kind)}:new M3.Command {Kind=Enum.Parse<M3.CommandKind>(kind)};
        c.Id=Guid.NewGuid().ToString("N");c.PlayerId=state.ActivePlayerId;c.ExpectedEventSequence=state.EventSequence;return c;
    }
    static void GiveFish(FishingState fish,params string[] tokens)
    {
        fish.Supply=fish.Supply.Except(tokens).ToArray();fish.Holdings[0].TokenIds=tokens;
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void ThirdWaveMapsAreBoundedDeterministicAndPreserveComponents(bool combined)
    {
        foreach(var map in Maps) foreach(int seats in new[]{3,4}) foreach(uint seed in Enumerable.Range(1,24).Select(i=>(uint)i))
        {
            dynamic g=Create(map,combined,seats,seed);dynamic s=g.GetAuthoritativeStateForTesting();FishingState f=s.TradeModules.Fishing;Tile[] tiles=s.Tiles;
            Assert.Equal((string)g.Save(),(string)Create(map,combined,seats,seed).Save());g.AssertInvariants();
            Assert.Equal(TradeModuleState.ThirdWaveVersion,(string)s.TradeModules.Version);
            Assert.Equal(FishingScenarioCatalog.ThirdWaveVersion+":"+map,f.MapVersion);
            if(map=="new-world") {Assert.True(f.GroundsPendingSetup);Assert.Empty(f.Grounds);continue;}
            Assert.Equal(18,f.Grounds.SelectMany(x=>x.VertexIds).Distinct().Count());
            var reference=M3.SeafarersScenarios.Create(map,seats);
            if(map=="forgotten-tribe")
            {
                var lake=Assert.Single(tiles,t=>t.Resource=="lake");Assert.Null(lake.Number);
                Assert.Equal(6,tiles.Count(t=>t!=lake&&t.Resource!="sea"&&t.Vertices.Intersect(lake.Vertices).Count()==2));
                Assert.DoesNotContain(tiles,t=>t.Number==12);
                Assert.All(f.Grounds,g=>Assert.All(g.VertexIds,v=>Assert.Contains(tiles,t=>reference.StartingTileIds.Contains(t.Id)&&t.Vertices.Contains(v))));
                Assert.Equal(reference.Tiles.Count(t=>t.Resource=="wheat")-1,tiles.Count(t=>t.Resource=="wheat"));
                Assert.Equal(reference.Tiles.Where(t=>t.Number.HasValue&&t.Number!=12).Select(t=>t.Number).Order(),tiles.Where(t=>t.Number.HasValue).Select(t=>t.Number).Order());
                Assert.All(tiles.Where(t=>reference.StartingTileIds.Contains(t.Id)&&t.Q==tiles.Where(x=>x.R==t.R&&reference.StartingTileIds.Contains(x.Id)).Max(x=>x.Q)),t=>Assert.DoesNotContain(t.Number,new int?[]{5,6,8,9}));
            }
            else Assert.DoesNotContain(tiles,t=>t.Resource=="lake");
            if(map=="cloth-for-catan")
            {
                var islands=reference.Regions.Where(r=>r.TileIds.Any(reference.StartingTileIds.Contains)).ToArray();Assert.Equal(2,islands.Length);
                foreach(var island in islands)Assert.Equal(3,f.Grounds.Count(g=>g.VertexIds.All(v=>tiles.Any(t=>island.TileIds.Contains(t.Id)&&t.Vertices.Contains(v)))));
            }
        }
        output.WriteLine($"双扩展{combined}：4图×2人数×24种子，192个地图配置确定性与组件检查通过。");
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void NewWorldGroundsAppearOnlyAfterAllPortsAndReplayPendingSetup(bool combined)
    {
        foreach(uint seed in Enumerable.Range(1,24).Select(i=>(uint)i)) foreach(int seats in new[]{3,4})
        {
            dynamic g=Create("new-world",combined,seats,seed);var game=new M6Game(combined?"combined":"seafarers",g);
            for(int i=0;i<10;i++)
            {
                Assert.Empty((FishingGround[])game.View().TradeModules.Fishing.Grounds);
                dynamic c=game.Decide(game.AiView(M6Game.Actor(game.View())));Assert.Equal("SetupPort",c.Kind.ToString());c.Id="新世界港口-"+i;
                string before=game.Save();var restored=M6Game.Load(combined?"combined":"seafarers",before);Assert.True((bool)g.Preview(c).Success);Assert.Equal(before,game.Save());
                game.Accept(c);restored.Accept(c);Assert.Equal(game.Save(),restored.Save());
                string after=game.Save();Assert.True((bool)game.Execute(c).IsDuplicate);Assert.Equal(after,game.Save());
            }
            Assert.Equal("SetupSettlement",game.View().Phase.ToString());Assert.Equal(6,((FishingGround[])game.View().TradeModules.Fishing.Grounds).Length);
            g.AssertInvariants();
        }
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void ClothFishPirateRemovalNeedsTradeRouteAndDoesNotSteal(bool combined)
    {
        dynamic g=Create("cloth-for-catan",combined);dynamic s=ActionState(g,combined);
        if(combined)s.FirstBarbarianAttack=true;
        s.PirateTileId=((Tile[])s.Tiles).First(t=>t.Resource=="sea").Id;GiveFish(s.TradeModules.Fishing,"F22");Set(g,s);
        dynamic c=Command(combined,s,"SpendFish");c.FishAction=FishAction.RemoveRobber;c.FishRemovePirate=true;c.FishTokenIds=new[]{"F22"};
        string before=g.Save();Assert.Equal("NoClothRoute",(string)g.Execute(c).ErrorCode);Assert.Equal(before,(string)g.Save());
        if(combined)s.Special.Villages[0].TradingPlayerIds=new[]{"P1"};else s.Villages[0].TradingPlayerIds=new[]{"P1"};Set(g,s);
        Assert.True((bool)g.Execute(c).Success);Assert.Null((string?)g.GetAuthoritativeStateForTesting().PirateTileId);
        Assert.Equal(0,(int)g.GetPlayerView("P1").OwnResources.Total);Assert.Equal(0,(int)g.GetPlayerView("P1").Players[0].Cloth);g.AssertInvariants();
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void WonderBootRaisesOnlyPersonalScorePathAndNeverBlocksCompletion(bool combined)
    {
        foreach(bool finish in new[]{false,true})
        {
            dynamic g=Create("wonders-of-catan",combined,target:5);dynamic s=ActionState(g,combined);
            s.Players[0].WonderId="great-wall";s.Players[0].WonderLevel=finish?3:1;if(combined)s.Players[0].DefenderPoints=5;else s.Players[0].BonusVictoryPoints=5;
            FishingState f=s.TradeModules.Fishing;f.Supply=f.Supply.Where(t=>t!=FishingRules.OldBootId).ToArray();f.OldBootPlayerId="P1";
            s.Players[0].Resources=new ResourceBag{Brick=3,Wood=1,Wheat=1};Set(g,s);
            dynamic c=Command(combined,s,"DeclareVictory");string before=g.Save();Assert.False((bool)g.Execute(c).Success);Assert.Equal(before,(string)g.Save());
            if(finish){c=Command(combined,s,"BuildWonder");c.TargetId="great-wall";}
            else {if(combined)s.Players[0].DefenderPoints=6;else s.Players[0].BonusVictoryPoints=6;Set(g,s);c=Command(combined,s,"DeclareVictory");}
            Assert.True((bool)g.Execute(c).Success);Assert.Equal("P1",(string)g.GetPlayerView("P1").WinnerPlayerId);g.AssertInvariants();
        }
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void ClothOnlyLastSetupBuildingDrawsOneFish(bool combined)
    {
        dynamic g=Create("cloth-for-catan",combined);dynamic s=g.GetAuthoritativeStateForTesting();
        FishingState fish=s.TradeModules.Fishing;string vertex=fish.Grounds[0].VertexIds[0];
        fish.Supply=new[]{"F22"}.Concat(fish.Supply.Where(t=>t!="F22")).ToArray();
        for(int round=0;round<3;round++)
        {
            s.SetupPlacementIndex=round*4;
            Invoke((object)g,"GrantSetupFish",(object)s,"P1",vertex);
            Assert.Equal(round==2?1:0,fish.Holdings[0].TokenIds.Length);
        }
        Assert.Equal(new[]{"F22"},fish.Holdings[0].TokenIds);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void FishShipCollectsPortAndPlacementSharesGroundWithoutDoublePayment(bool combined)
    {
        dynamic g=Create("forgotten-tribe",combined);dynamic s=ActionState(g,combined);dynamic board=g.GetPlayerView("P1").Board;
        Tile[] tiles=s.Tiles;Edge[] edges=board.Edges;
        M3.Port[] gifts=combined?s.Special.GiftPorts:s.GiftPorts;var port=gifts[0];var reward=edges.Single(e=>e.Id==port.EdgeId);
        bool Sea(Edge e)=>tiles.Count(t=>e.Vertices.All(t.Vertices.Contains))==1||tiles.Any(t=>t.Resource=="sea"&&e.Vertices.All(t.Vertices.Contains));
        var neighbor=edges.First(e=>e.Id!=reward.Id&&Sea(e)&&e.Vertices.Intersect(reward.Vertices).Any());
        FishingState fish=s.TradeModules.Fishing;var coast=edges.First(e=>Sea(e)&&tiles.Any(t=>t.Resource!="sea"&&e.Vertices.All(t.Vertices.Contains))&&fish.Grounds.Any(f=>e.Vertices.All(f.VertexIds.Contains)));
        s.Settlements=new[]{new PiecePlacement{PlayerId="P1",LocationId=coast.Vertices[0]}};
        if(combined)s.Ships=new[]{new M5.ShipPlacement{PlayerId="P1",LocationId=neighbor.Id,BuiltTurn=1}};
        else s.Ships=new[]{new M3.ShipPlacement{PlayerId="P1",LocationId=neighbor.Id,BuiltTurn=1}};
        s.PirateTileId=null;GiveFish(fish,"F22","F23");Set(g,s);
        dynamic c=Command(combined,s,"SpendFish");c.FishAction=FishAction.BuildRoad;c.FishBuildShip=true;c.TargetId=reward.Id;c.FishTokenIds=new[]{"F22","F23"};
        Assert.True((bool)g.Execute(c).Success);dynamic after=g.GetAuthoritativeStateForTesting();
        Assert.Equal("PortPlacement",after.Phase.ToString());Assert.Equal(2,((FishingState)after.TradeModules.Fishing).Discard.Length);
        Assert.Equal(0,((HarborScore[])Invoke((object)g,"HarborScores",(object)after)!).Single(x=>x.PlayerId=="P1").Points);
        string before=g.Save();dynamic place=Command(combined,after,"PlacePort");place.SourceId=port.Id;place.TargetId="missing";
        Assert.False((bool)g.Execute(place).Success);Assert.Equal(before,(string)g.Save());
        place.TargetId=coast.Id;Assert.True((bool)g.Execute(place).Success);
        after=g.GetAuthoritativeStateForTesting();Assert.Equal("Action",after.Phase.ToString());
        Assert.Equal(1,((HarborScore[])Invoke((object)g,"HarborScores",(object)after)!).Single(x=>x.PlayerId=="P1").Points);
        Assert.Equal(2,((FishingState)after.TradeModules.Fishing).Discard.Length);
        Assert.Equal(JsonSerializer.Serialize(fish.Grounds),JsonSerializer.Serialize((FishingGround[])after.TradeModules.Fishing.Grounds));g.AssertInvariants();
    }
    [Theory][InlineData(3,false)][InlineData(4,false)][InlineData(3,true)][InlineData(4,true)]
    public void PirateHarborTargetCeilingHasLegalBuildingWitness(int seats,bool combined)
    {
        var g=M3.SeafarersGameSession.Create("pirate-islands",seats,97,15,new(){Harbormaster=true});
        var s=g.GetAuthoritativeStateForTesting();var map=M3.SeafarersScenarios.Create("pirate-islands",seats);var board=g.GetPlayerView("P1").Board;
        var fort=s.Fortresses.Single(f=>f.PlayerId=="P1");
        bool Adjacent(string a,string b)=>a==b||board.Edges.Any(e=>e.Vertices.Contains(a)&&e.Vertices.Contains(b));
        var chosen=new List<string>{fort.StartingVertexId,fort.BeachheadVertexId,fort.VertexId};
        var rivals=s.Settlements.Where(b=>b.PlayerId!="P1").Select(b=>b.LocationId).ToArray();
        var ports=s.Ports.SelectMany(p=>p.Vertices).ToHashSet();
        var candidates=board.Vertices.Select(v=>v.Id).Where(v=>s.Tiles.Any(t=>map.StartingTileIds.Contains(t.Id)&&t.Vertices.Contains(v))&&!rivals.Any(r=>Adjacent(v,r)))
            .OrderByDescending(ports.Contains).ThenBy(v=>v,StringComparer.Ordinal).ToArray();
        int budget=200000;
        bool Choose(int start)
        {
            if(chosen.Count==9)return chosen.Count(ports.Contains)>=2;
            if(--budget<0)return false;
            for(int i=start;i<candidates.Length;i++)if(!chosen.Any(v=>Adjacent(v,candidates[i])))
            {chosen.Add(candidates[i]);if(Choose(i+1))return true;chosen.RemoveAt(chosen.Count-1);}
            return false;
        }
        Assert.True(Choose(0));var cities=chosen.Where(v=>v!=fort.VertexId).OrderByDescending(ports.Contains).Take(4).ToArray();
        s.Cities=cities.Select(v=>new PiecePlacement{LocationId=v,PlayerId="P1"}).ToArray();
        s.Settlements=s.Settlements.Where(b=>b.PlayerId!="P1").Concat(chosen.Except(cities).Select(v=>new PiecePlacement{LocationId=v,PlayerId="P1"})).ToArray();
        s.Turn=9;s.ActivePlayerId="P1";s.Phase=M3.GamePhase.Action;s.SetupPlacementIndex=seats*2;s.PendingDecision=null;fort.Strength=0;
        M3Harness.Balance(s);M3Harness.SetState(g,s);Assert.Equal(15,g.GetPlayerView("P1").OwnVictoryPoints);
        M3Harness.Accepted(g,M3Harness.Cmd(M3.CommandKind.DeclareVictory));Assert.Equal("P1",g.GetPlayerView("P1").WinnerPlayerId);
        Assert.Throws<ArgumentOutOfRangeException>(()=>M3.SeafarersGameSession.Create("pirate-islands",seats,97,16,new(){Harbormaster=true}));
        Assert.Throws<ArgumentOutOfRangeException>(()=>M3.SeafarersGameSession.Create("pirate-islands",seats,97,14));
        if(combined)
        {
            var city=M5.CombinedGameSession.Create("pirate-islands",seats,97,20,tradeModules:new(){Harbormaster=true});var cs=city.GetAuthoritativeStateForTesting();
            cs.Turn=9;cs.ActivePlayerId="P1";cs.Phase=M5.GamePhase.Action;cs.SetupPlacementIndex=seats*2;cs.PendingDecision=null;
            cs.Cities=s.Cities;cs.Settlements=s.Settlements;cs.Players[0].DefenderPoints=6;
            M5Harness.Balance(cs);
            foreach(var player in cs.Players)player.Pieces.Settlements-=cs.Special.Fortresses.Count(f=>f.PlayerId==player.Id&&f.Strength>0);
            // 先保持要塞未收复：为驻堡村庄预留库存，避免超出五村。
            cs.Settlements=cs.Settlements.Where(b=>b.LocationId!=fort.VertexId).ToArray();cs.Players[0].Pieces.Settlements++;
            typeof(M5.CombinedGameSession).GetField("state",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(city,cs);
            Invoke(city,"RecalculateAwards",cs);city.AssertInvariants();Assert.Equal(20,city.GetPlayerView("P1").OwnVictoryPoints);
            Assert.False(city.Execute(M5Harness.Cmd(M5.CommandKind.DeclareVictory)).Success);
            cs.Special.Fortresses.Single(f=>f.PlayerId=="P1").Strength=0;
            cs.Settlements=s.Settlements;cs.Players[0].DefenderPoints=5;M5Harness.Balance(cs);
            foreach(var player in cs.Players)player.Pieces.Settlements-=cs.Special.Fortresses.Count(f=>f.PlayerId==player.Id&&f.Strength>0);
            Invoke(city,"RecalculateAwards",cs);city.AssertInvariants();Assert.Equal(20,city.GetPlayerView("P1").OwnVictoryPoints);
            Assert.True(city.Execute(M5Harness.Cmd(M5.CommandKind.DeclareVictory)).Success);city.AssertInvariants();Assert.Equal("P1",city.GetPlayerView("P1").WinnerPlayerId);
        }
        output.WriteLine($"{seats}人15分构造：城市{string.Join(",",cities)}；村庄{string.Join(",",chosen.Except(cities))}；港口分{g.GetPlayerView("P1").TradeModules.HarborScores.Single(h=>h.PlayerId=="P1").Points}。");
    }
    [Fact]
    public void ImpossibleGroundTopologyFailsExplicitlyWithoutUnboundedRetry()
    {
        var map=M3.SeafarersScenarios.Create("new-world",4);map.Edges=[];
        int shuffles=0;Action<int[]> shuffle=_=>shuffles++;
        var method=typeof(FishingScenarioCatalog).GetMethod("CreateSeafaring",BindingFlags.Static|BindingFlags.NonPublic)!;
        var ex=Assert.Throws<TargetInvocationException>(()=>method.Invoke(null,new object[]{map,new[]{"P1","P2","P3","P4"},shuffle,true}));
        Assert.IsType<InvalidDataException>(ex.InnerException);Assert.Equal(2,shuffles);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void NaturalTribePortDecisionRestoresWithoutRepeatingFishPayment(bool combined)
    {
        dynamic g=Create("forgotten-tribe",combined,seed:1,target:20);var game=new M6Game(combined?"combined":"seafarers",g);int count=0;
        while(game.View().Phase.ToString()!="PortPlacement"&&game.View().Phase.ToString()!="Finished"&&count<4000)
        {
            dynamic command=game.Decide(game.AiView(M6Game.Actor(game.View())));command.Id="部落恢复-"+count++;game.Accept(command);
        }
        Assert.Equal("PortPlacement",game.View().Phase.ToString());string before=game.Save();var restored=M6Game.Load(combined?"combined":"seafarers",before);
        dynamic place=game.Decide(game.AiView(M6Game.Actor(game.View())));place.Id="部落港口落地";Assert.Equal("PlacePort",place.Kind.ToString());
        Assert.True((bool)g.Preview(place).Success);Assert.Equal(before,game.Save());
        game.Accept(place);restored.Accept(place);Assert.Equal(game.Save(),restored.Save());game.AssertInvariants();
        output.WriteLine($"双扩展{combined}：正常产品AI第{count}指令进入港口待决，恢复并提交后权威存档完全相同。");
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void WonderCustomGoalStillRequiresLeadWithOldBoot(bool combined)
    {
        dynamic g=Create("wonders-of-catan",combined,target:16);dynamic s=ActionState(g,combined);
        s.Players[0].WonderId="great-wall";s.Players[0].WonderLevel=2;s.Players[1].WonderId="great-bridge";s.Players[1].WonderLevel=2;
        if(combined)s.Players[0].DefenderPoints=17;else s.Players[0].BonusVictoryPoints=17;
        FishingState f=s.TradeModules.Fishing;f.Supply=f.Supply.Where(t=>t!=FishingRules.OldBootId).ToArray();f.OldBootPlayerId="P1";Set(g,s);
        Assert.Equal(17,(int)g.GetPlayerView("P1").TradeModules.Fishing.OwnVictoryTarget);
        dynamic declare=Command(combined,s,"DeclareVictory");string before=g.Save();Assert.False((bool)g.Execute(declare).Success);Assert.Equal(before,(string)g.Save());
        s.Players[0].WonderLevel=3;Set(g,s);Assert.True((bool)g.Execute(declare).Success);g.AssertInvariants();
    }
    [Theory][InlineData("four-islands","P1")][InlineData("four-islands","P2")]
    [InlineData("forgotten-tribe","P1")][InlineData("forgotten-tribe","P2")]
    public void ProductionCanEnterFullFishReplacementForActiveOrOtherPlayer(string map,string recipient)
    {
        var g=M3.SeafarersGameSession.Create(map,4,97,tradeModules:new(){Fishing=true});var s=g.GetAuthoritativeStateForTesting();var f=s.TradeModules.Fishing;
        s.Turn=9;s.SetupPlacementIndex=8;s.ActivePlayerId="P1";s.Phase=M3.GamePhase.ProductionAwaitRoll;s.Random.State=1;
        s.Settlements=[new PiecePlacement{PlayerId=recipient,LocationId=f.Grounds.Single(g=>g.Number==4).VertexIds[0]}];
        var tokens=Enumerable.Range(1,7).Select(i=>"F"+i.ToString("00")).ToArray();f.Holdings.Single(h=>h.PlayerId==recipient).TokenIds=tokens;
        f.Supply=new[]{"F22"}.Concat(f.Supply.Except(tokens).Where(t=>t!="F22")).ToArray();M3Harness.Balance(s);M3Harness.SetState(g,s);
        var roll=M3Harness.Cmd(M3.CommandKind.RollDice);M3Harness.Accepted(g,roll);
        var view=g.GetPlayerView(recipient);Assert.Equal(4,view.LastDice1+view.LastDice2);Assert.Equal(M3.GamePhase.FishReplacement,view.Phase);Assert.Equal(recipient,view.PendingDecision.PlayerId);
        var invalid=M3Harness.Cmd(M3.CommandKind.EndTurn);M3Harness.Rejected(g,invalid);
        var choice=new M3.Command{Id="满额换抽确认",PlayerId=recipient,Kind=M3.CommandKind.ResolveFishReplacement,TargetId="F01",DecisionId=view.TradeModules.Fishing.PendingDecisionId,ExpectedEventSequence=view.TradeModules.Fishing.StateSequence};
        M3Harness.Accepted(g,choice);Assert.Equal(M3.GamePhase.Action,g.GetPlayerView(recipient).Phase);
        Assert.Contains(g.GetPlayerView(recipient).TradeModules.Fishing.OwnTokens,t=>t.Id=="F22");Assert.Equal(7,g.GetPlayerView(recipient).TradeModules.Fishing.OwnTokens.Length);
        string after=g.Save();Assert.True(g.Execute(choice).IsDuplicate);Assert.Equal(after,g.Save());
    }
    [Theory][InlineData(false,3)][InlineData(false,4)][InlineData(true,3)][InlineData(true,4)]
    [InlineData(true,3,true)][InlineData(true,4,true)]
    public void PirateHarborDefaultGameCompletesWithFortressAndReplays(bool combined,int seats,bool kingdoms=false)
    {
        dynamic g=kingdoms?M5.CombinedGameSession.Create("pirate-islands",seats,97,threeKingdoms:true,tradeModules:new(){Harbormaster=true}):Create("pirate-islands",combined,seats,fishing:false);Assert.Equal(combined?13:11,(int)g.GetPlayerView("P1").TargetVictoryPoints);
        var game=new M6Game(combined?"combined":"seafarers",g);int count=0;
        while(game.View().Phase.ToString()!="Finished"&&count<15000){dynamic c=kingdoms?new ThreeKingdomsTestGame("combined",g).Decide():game.Decide(game.AiView(M6Game.Actor(game.View())));c.Id="海盗海港-"+count++;game.Accept(c);}
        Assert.Equal("Finished",game.View().Phase.ToString());dynamic s=g.GetAuthoritativeStateForTesting();
        M3.FortressState[] forts=combined?s.Special.Fortresses:s.Fortresses;Assert.Equal(0,forts.Single(f=>f.PlayerId==(string)s.WinnerPlayerId).Strength);
        Assert.Equal(game.Save(),M6Game.Load(combined?"combined":"seafarers",game.Save()).Save());g.AssertInvariants();
        output.WriteLine($"海盗群岛/{seats}人/双扩展{combined}/三国{kingdoms}：{count}指令，{game.View().Turn}回合，夺堡并自然胜利。");
        Assert.Throws<ArgumentException>(()=>Create("pirate-islands",combined,seats));
    }
}
