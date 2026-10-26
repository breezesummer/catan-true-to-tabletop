using System.Reflection;
using System.Text.Json;
using Catan.Core;
using Catan.Core.ThreeKingdoms;
using M4=Catan.Core.M4;
using M5=Catan.Core.M5;
using Xunit;
using Xunit.Abstractions;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB6MapTests(ITestOutputHelper output)
{
    private static Tile Hex(string id,string resource,int? number) => new(){Id=id,Resource=resource,Number=number,Vertices=Enumerable.Range(0,6).Select(i=>id+i).ToArray()};
    private static B6MapSnapshot Map(params Tile[] tiles) => new(){Tiles=tiles,Edges=tiles.SelectMany(t=>Enumerable.Range(0,6).Select(i=>new Edge{Id=t.Id+"e"+i,Vertices=new[]{t.Vertices[i],t.Vertices[(i+1)%6]}})).ToArray()};
    private static B6MapPiece Piece(string kind,string location,string player="P1") => new(){Kind=kind,LocationId=location,OriginalLocationId=location,PlayerId=player};
    private static T Invoke<T>(object game,string method,params object[] args) => (T)game.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(game,args)!;

    [Fact]
    public void ExchangeMovesOnlyContentsAndIsolatedComponentsWithAttachments()
    {
        var map=Map(Hex("A","wood",5),Hex("B","sea",null));
        map.Pieces=new[]{Piece("City","A0"),Piece("Wall","A0"),Piece("Metropolis","A0"),Piece("Road","Ae0"),Piece("Knight","A3")};map.RobberTileId="A";map.PirateTileId="B";map.MerchantTileId="A";
        var before=JsonSerializer.Serialize(map);var swap=B6MapTransaction.Preview(map,"A","B");Assert.NotNull(swap);var next=swap.Result;
        Assert.Equal(before,JsonSerializer.Serialize(map));Assert.Equal("sea",next.Tiles[0].Resource);Assert.Null(next.Tiles[0].Number);Assert.Equal("wood",next.Tiles[1].Resource);Assert.Equal(5,next.Tiles[1].Number);
        Assert.Equal(new[]{"B0","B0","B0","Be0","B3"},next.Pieces.Select(p=>p.LocationId));Assert.Equal("B",next.RobberTileId);Assert.Equal("A",next.PirateTileId);Assert.Equal("B",next.MerchantTileId);
        Assert.Equal(map.Tiles.Select(t=>t.Id),next.Tiles.Select(t=>t.Id));Assert.Equal(map.Tiles.SelectMany(t=>t.Vertices),next.Tiles.SelectMany(t=>t.Vertices));
    }

    [Theory][InlineData("gold",5,"wood",4)][InlineData("wood",6,"sea",null)][InlineData("desert",null,"ore",8)][InlineData("fog",null,"wood",4)]
    public void ForbiddenPairsHaveNoMutation(string first,int? n1,string second,int? n2)
    {var map=Map(Hex("A",first,n1),Hex("B",second,n2));var before=JsonSerializer.Serialize(map);Assert.Null(B6MapTransaction.Preview(map,"A","B"));Assert.Equal(before,JsonSerializer.Serialize(map));}

    [Fact]
    public void OrdinaryRedNumberLandExchangeIsAllowed()
    {var map=Map(Hex("A","wood",6),Hex("B","ore",8));Assert.NotNull(B6MapTransaction.Preview(map,"A","B"));}

    [Fact]
    public void SharedThirdLandAssetsStayAndCanInvalidateSeaExchange()
    {
        var a=Hex("A","wood",5);var b=Hex("B","sea",null);var c=Hex("C","brick",4);c.Vertices[0]="A0";
        var map=Map(a,b,c);map.Pieces=new[]{Piece("Settlement","A0")};var swap=B6MapTransaction.Preview(map,"A","B");Assert.NotNull(swap);Assert.Equal("A0",swap.Result.Pieces.Single().LocationId);
        c.Vertices[1]="B0";c.Vertices[2]="B1";map.Pieces=new[]{Piece("Ship","Be0")};Assert.Null(B6MapTransaction.Preview(map,"A","B"));
    }

    [Fact]
    public void RotationSearchUsesFirstWholeLegalCombinationAndRollsBackWhenAllBlocked()
    {
        var a=Hex("A","wood",5);var b=Hex("B","ore",4);var c=Hex("C","brick",3);c.Vertices=b.Vertices.ToArray();var map=Map(a,b,c);
        map.Pieces=new[]{Piece("Settlement","A0"),Piece("Road","Ae0"),Piece("Settlement","B0","P2")};
        var swap=B6MapTransaction.Preview(map,"A","B");Assert.NotNull(swap);Assert.Equal(2,swap.FirstRotation);Assert.Equal(0,swap.SecondRotation);Assert.Equal("B2",swap.Result.Pieces[0].LocationId);
        map.Pieces=new[]{Piece("Settlement","A0")}.Concat(Enumerable.Range(0,6).Select(i=>Piece("Knight","B"+i,"P2"))).ToArray();var before=JsonSerializer.Serialize(map);
        Assert.Null(B6MapTransaction.Preview(map,"A","B"));Assert.Equal(before,JsonSerializer.Serialize(map));
    }

    [Fact]
    public void PortRelocationUsesShortestGraphDistanceAndStableEdgeTieBreak()
    {
        var map=Map(Hex("A","wood",5),Hex("B","sea",null));map.Edges=map.Edges.Concat(new[]{new Edge{Id="bridge",Vertices=new[]{"A0","B0"}}}).ToArray();map.Ports=new[]{new B6MapPort{Id="port",Vertices=new[]{"A0","A1"},Resource=Resource.Wood}};
        var swap=B6MapTransaction.Preview(map,"A","B");Assert.NotNull(swap);Assert.Equal(new[]{"B0","B1"},swap.Result.Ports.Single().Vertices);Assert.Equal(Resource.Wood,swap.Result.Ports.Single().Resource);
    }

    [Fact]
    public void ProtectedScenarioAnchorIsNeverAnExchangeEndpoint()
    {var map=Map(Hex("A","wood",5),Hex("B","ore",4));map.ProtectedTileIds=new[]{"A"};Assert.Empty(B6MapTransaction.Enumerate(map));}

    public static IEnumerable<object[]> Maps() => M5.CombinedScenarios.Ids.SelectMany(map=>new[]{3,4}.Select(n=>new object[]{map,n}));

    [Theory][MemberData(nameof(Maps))]
    public void EveryCombinedMapSupportsAtomicExchangeWithoutMovingScenarioAnchors(string map,int players)
    {
        var game=M5.CombinedGameSession.Create(map,players,42,threeKingdoms:true,threeKingdomsPackageVersion:GeneralCatalog.B6PackageVersion);var state=game.GetAuthoritativeStateForTesting();var host=Invoke<IThreeKingdomsB6MapHost>(game,"CardHost",state);
        var before=JsonSerializer.Serialize(state);var clock=System.Diagnostics.Stopwatch.StartNew();var choices=host.GetMapB6Swaps();clock.Stop();output.WriteLine("{0}/{1}：枚举 {2} 项，用时 {3} 毫秒",map,players,choices.Length,clock.ElapsedMilliseconds);Assert.NotEmpty(choices);Assert.Equal(before,JsonSerializer.Serialize(state));
        var scenario=M5.CombinedScenarios.Create(map,players);var forbidden=state.Tiles.Where(t=>t.Resource=="fog").Select(t=>t.Id).Concat(scenario.PiratePath).Concat(scenario.ForbiddenSettlementTileIds).ToArray();
        Assert.All(choices,c=>{Assert.DoesNotContain(c.FirstTileId,forbidden);Assert.DoesNotContain(c.SecondTileId,forbidden);});
        var chosen=choices.First(c=>state.Tiles.Single(t=>t.Id==c.FirstTileId).Resource!=state.Tiles.Single(t=>t.Id==c.SecondTileId).Resource);var original=state.Tiles.Single(t=>t.Id==chosen.FirstTileId).Resource;
        host.ApplyMapB6Swap(chosen.FirstTileId,chosen.SecondTileId);Assert.Equal(original,state.Tiles.Single(t=>t.Id==chosen.SecondTileId).Resource);
        Assert.Equal(choices[0].Result.Tiles.Select(t=>t.Id),state.Tiles.Select(t=>t.Id));typeof(M5.CombinedGameSession).GetField("state",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(game,state);game.AssertInvariants();
        if(state.Special!=null){Assert.Equal(scenario.Villages.Select(v=>v.VertexId),state.Special.Villages.Select(v=>v.VertexId));Assert.Equal(scenario.Fortresses.Select(f=>f.VertexId),state.Special.Fortresses.Select(f=>f.VertexId));}
    }

    [Fact]
    public void CitiesHostExchangePreservesTopologyAndRandomState()
    {
        var game=M4.CitiesKnightsGameSession.Create(AcceptanceFixture.Json,4,42,threeKingdoms:true,threeKingdomsPackageVersion:GeneralCatalog.B6PackageVersion);var state=game.GetAuthoritativeStateForTesting();var host=Invoke<IThreeKingdomsB6MapHost>(game,"CardHost",state);uint random=state.Random.State;long draws=state.Random.Draws;
        var tiles=state.Tiles.Select(t=>t.Id).ToArray();var swap=host.GetMapB6Swaps().First();host.ApplyMapB6Swap(swap.FirstTileId,swap.SecondTileId);Assert.Equal(tiles,state.Tiles.Select(t=>t.Id));Assert.Equal(random,state.Random.State);Assert.Equal(draws,state.Random.Draws);M4CoreHarness.SetState(game,state);game.AssertInvariants();
    }

    private static ThreeKingdomsState Generals()
    {
        var state=ThreeKingdomsEngine.Create(new[]{"P1","P2","P3","P4"},42,_=>0,true,GeneralCatalog.B6PackageVersion);
        while(state.PendingDecision!=null){var d=state.PendingDecision;if(d.Kind==ThreeKingdomsDecisionKind.SelectOpening)ThreeKingdomsEngine.ChooseGenerals(state,d.PlayerId,d.CandidateIds.Take(2).ToArray(),d.DecisionId,d.WindowId,_=>0);else ThreeKingdomsEngine.DeployGeneral(state,d.PlayerId,d.CandidateIds[0],d.DecisionId,d.WindowId,_=>0);}
        ThreeKingdomsEngine.BeginNormalTurns(state);state.Clock.Phase=ThreeKingdomsPhase.Play;state.Clock.PhaseId=5;state.Clock.DrawPhaseTurnInstanceId=state.Clock.TurnInstanceId;
        string[] selected={"tk-g47","tk-g12","tk-g09","tk-g22"};for(int i=0;i<selected.Length;i++){var g=state.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P"+(i+1));var desired=state.Generals.Single(g=>g.GeneralId==selected[i]);(g.GeneralId,desired.GeneralId)=(desired.GeneralId,g.GeneralId);(g.InstanceId,desired.InstanceId)=(desired.InstanceId,g.InstanceId);g.Counters=Array.Empty<GeneralCounter>();}
        state.Skills=new B2SkillState();state.Response=null;state.Automation=new B4AutomationState{TurnId=state.Clock.TurnInstanceId};return state;
    }

    [Fact]
    public void TamoNativeCommandIsLimitedIdempotentAndPreviewHasNoRandomOrBoardMutation()
    {
        var game=M4CoreHarness.Fixture(s=>{s.RulesVersion=M4.CitiesKnightsGameSession.ThreeKingdomsB6RulesVersion;s.ThreeKingdoms=Generals();});
        var a=game.GetPlayerView("P1").ThreeKingdoms.SkillActions.First(a=>a.Command.Option=="map-tamo");var c=new M4.Command{Id="b6-map-native",PlayerId="P1",Kind=M4.CommandKind.UseGeneralSkill,WindowId=a.Command.WindowId,SkillCommand=a.Command};
        var before=game.Save();Assert.True(game.Preview(c).Success);Assert.Equal(before,game.Save());Assert.True(game.Execute(c).Success);game.AssertInvariants();var after=game.Save();Assert.True(game.Execute(c).IsDuplicate);Assert.Equal(after,game.Save());Assert.DoesNotContain(game.GetPlayerView("P1").ThreeKingdoms.SkillActions,a=>a.Command.Option=="map-tamo");
        var st=game.GetAuthoritativeStateForTesting();Assert.Equal(1,st.ThreeKingdoms.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P1").Counters.Single(x=>x.Key=="tamo").Value);
    }

    [Fact]
    public void InvalidMapCommandLeavesLimitedChargeAndSnapshotUntouched()
    {
        var game=M4CoreHarness.Fixture(s=>{s.RulesVersion=M4.CitiesKnightsGameSession.ThreeKingdomsB6RulesVersion;s.ThreeKingdoms=Generals();});var window=game.GetPlayerView("P1").ThreeKingdoms.WindowId;var before=game.Save();
        var c=new M4.Command{Id="b6-map-invalid",PlayerId="P1",Kind=M4.CommandKind.UseGeneralSkill,WindowId=window,SkillCommand=new B2SkillCommand{SkillId="tk-g47-s1",Option="map-tamo",TileIds=new[]{"missing","missing"},WindowId=window}};
        Assert.False(game.Execute(c).Success);Assert.Equal(before,game.Save());
    }

    [Fact]
    public void FirstNativeBarbarianAttackFindsDesertAfterTamoMovedIt()
    {
        var game=M4CoreHarness.Fixture(s=>{s.RulesVersion=M4.CitiesKnightsGameSession.ThreeKingdomsB6RulesVersion;s.ThreeKingdoms=Generals();});var state=game.GetAuthoritativeStateForTesting();var old=state.Tiles.Single(t=>t.Resource=="desert").Id;var host=Invoke<IThreeKingdomsB6MapHost>(game,"CardHost",state);var swap=host.GetMapB6Swaps().First(x=>x.FirstTileId==old||x.SecondTileId==old);host.ApplyMapB6Swap(swap.FirstTileId,swap.SecondTileId);var desert=state.Tiles.Single(t=>t.Resource=="desert").Id;Assert.NotEqual(old,desert);
        game.GetType().GetMethod("B5Invasion",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(game,new object[]{state,"first-after-map",state.Players.Select(p=>p.Id).ToArray(),false});Assert.Equal(desert,state.RobberTileId);Assert.True(state.FirstBarbarianAttack);
    }

    [Fact]
    public void CombinedPendingRobberStartMovesWithBoardAndSurvivesAuthoritySnapshot()
    {
        var game=M5Harness.Fixture(s=>{s.RulesVersion=M5.CombinedGameSession.ThreeKingdomsB6RulesVersion;s.ThreeKingdoms=Generals();});var state=game.GetAuthoritativeStateForTesting();var scenario=M5.CombinedScenarios.CreateSeeded("heading-for-new-shores",4,42);var original=scenario.InitialRobberTileId;var host=Invoke<IThreeKingdomsB6MapHost>(game,"CardHost",state);
        var swap=host.GetMapB6Swaps().First(x=>(x.FirstTileId==original&&state.Tiles.Single(t=>t.Id==x.SecondTileId).Resource=="sea")||(x.SecondTileId==original&&state.Tiles.Single(t=>t.Id==x.FirstTileId).Resource=="sea"));host.ApplyMapB6Swap(swap.FirstTileId,swap.SecondTileId);Assert.Null(state.RobberTileId);Assert.Null(state.PirateTileId);Assert.NotEqual(original,state.B6MapInitialRobberTileId);M5Harness.SetState(game,state);
        using var stream=new MemoryStream();var serializer=new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(M5.GameState));serializer.WriteObject(stream,state);stream.Position=0;var copy=(M5.GameState)serializer.ReadObject(stream)!;var restored=M5Harness.New();M5Harness.SetState(restored,copy);Assert.Equal(state.B6MapInitialRobberTileId,copy.B6MapInitialRobberTileId);Assert.Equal("sea",copy.Tiles.Single(t=>t.Id==original).Resource);
        restored.GetType().GetMethod("B5Invasion",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(restored,new object[]{copy,"first-after-map",copy.Players.Select(p=>p.Id).ToArray(),false});Assert.Equal(state.B6MapInitialRobberTileId,copy.RobberTileId);Assert.NotEqual("sea",copy.Tiles.Single(t=>t.Id==copy.RobberTileId).Resource);
    }

    private static ThreeKingdomsB5CivilTestHost ZhimengHost()
    {
        var host=ThreeKingdomsB5CivilTestHost.Create("tk-g47","tk-g12","tk-g22");host.State.RulesVersion=GeneralCatalog.B6RulesVersion;host.State.PackageVersion=GeneralCatalog.B6PackageVersion;host.State.PackageContentHash=GeneralCatalog.B6ContentHash;host.State.Clock.Phase=ThreeKingdomsPhase.End;return host;
    }
    private static void Choose(IThreeKingdomsCardHost h,string option,string? target=null)
    {var action=B2SkillEngine.Actions(h,"P1").First(a=>a.Command.Option==option&&(target==null||a.Command.TargetPlayerIds.Contains(target)));B2SkillEngine.Resolve(h,"P1",action.Command);}

    [Fact]
    public void ZhimengRestoresAtTargetChoiceAndSplitsRealResourcesAndCommodities()
    {
        var host=ZhimengHost();host.Inner.Give("P1",2,0,0,0,0,0,0,1);host.Inner.Give("P2",0,0,0,0,1,1,1);host.Inner.Give("P3",0,1);var third=host.GetHand("P3");
        B2SkillEngine.OnEnd(host,"map-end");Choose(host,"accept");int draws=host.Inner.RandomCalls;var before=host.Snapshot();B2SkillEngine.Actions(host,"P1");Assert.Equal(before,host.Snapshot());Assert.Equal(draws,host.Inner.RandomCalls);
        var copy=host.Copy();Choose(host,"map-zhimeng-target","P2");Choose(copy,"map-zhimeng-target","P2");Assert.Equal(host.Snapshot(),copy.Snapshot());Assert.Equal(3,host.GetHand("P1").Sum());Assert.Equal(3,host.GetHand("P2").Sum());Assert.Equal(third,host.GetHand("P3"));host.Conserved();Assert.Null(host.State.Skills.PendingDecision);
    }

    [Fact]
    public void ZhimengOddCountFavorsOwnerAndEmptyPairDoesNotAsk()
    {
        var empty=ZhimengHost();B2SkillEngine.OnEnd(empty,"map-empty");Assert.Null(empty.State.Skills.PendingDecision);
        var host=ZhimengHost();host.Inner.Give("P2",0,0,0,3);B2SkillEngine.OnEnd(host,"map-odd");Choose(host,"accept");Choose(host,"map-zhimeng-target","P2");Assert.Equal(2,host.GetHand("P1").Sum());Assert.Single(host.GetHand("P2"),n=>n==1);host.Conserved();
    }

    [Fact]
    public void ZhimengHonorsGainBanBeforeRandomAndDoesNotReofferAfterDecline()
    {
        var host=ZhimengHost();host.Inner.Give("P2",1);host.State.Skills.TimedEffects=new[]{new B2TimedEffect{Id="ban",Kind="b6-jinglue:0",PlayerId="P3"}};B2SkillEngine.OnEnd(host,"map-block");Assert.Null(host.State.Skills.PendingDecision);Assert.Equal(0,host.Inner.RandomCalls);
        host.State.Skills.TimedEffects=Array.Empty<B2TimedEffect>();B2SkillEngine.OnEnd(host,"map-decline");Choose(host,"skip");B2SkillEngine.OnEnd(host,"map-decline");Assert.Null(host.State.Skills.PendingDecision);Assert.Equal(0,host.Inner.RandomCalls);
    }

    [Fact]
    public void LandingRewardRevocationKeepsVisitedRegionAndNeverGrantsRelocationAgain()
    {
        var definition=M5.CombinedScenarios.CreateSeeded("heading-for-new-shores",4,42);var source=definition.Tiles.First(t=>t.Resource!="sea"&&t.Resource!="gold"&&t.Resource!="desert"&&t.Number!=6&&t.Number!=8&&t.Vertices.Any(v=>definition.Tiles.Count(t2=>t2.Resource!="sea"&&t2.Vertices.Contains(v))==1));var region=definition.Regions.Single(r=>r.TileIds.Contains(source.Id));var vertex=source.Vertices.First(v=>definition.Tiles.Count(t=>t.Resource!="sea"&&t.Vertices.Contains(v))==1);
        var game=M5Harness.Fixture(s=>{s.RulesVersion=M5.CombinedGameSession.ThreeKingdomsB6RulesVersion;s.ThreeKingdoms=Generals();s.Settlements=new[]{M5Harness.Piece(vertex)};s.Players[0].SettledRegions=new[]{region.Id};s.Players[0].BonusVictoryPoints=2;s.B6LandingRecords=new[]{new B6LandingRecord{PlayerId="P1",RegionId=region.Id,SourceTileIds=new[]{source.Id},Points=2}};});
        var state=game.GetAuthoritativeStateForTesting();var host=Invoke<IThreeKingdomsB6MapHost>(game,"CardHost",state);var swap=host.GetMapB6Swaps().First(x=>(x.FirstTileId==source.Id&&state.Tiles.Single(t=>t.Id==x.SecondTileId).Resource=="sea")||(x.SecondTileId==source.Id&&state.Tiles.Single(t=>t.Id==x.FirstTileId).Resource=="sea"));
        host.ApplyMapB6Swap(swap.FirstTileId,swap.SecondTileId);Assert.Equal(0,state.Players[0].BonusVictoryPoints);Assert.Contains(region.Id,state.Players[0].SettledRegions);Assert.True(state.B6LandingRecords!.Single().Revoked);M5Harness.SetState(game,state);
        host.ApplyMapB6Swap(swap.FirstTileId,swap.SecondTileId);Assert.Equal(0,state.Players[0].BonusVictoryPoints);Assert.Single(state.B6LandingRecords);M5Harness.SetState(game,state);game.AssertInvariants();
    }

    [Fact]
    public void ZhimengRecordsCrossOwnerSameTypeCardsWithoutInventingLossForKeptCards()
    {
        var host=ZhimengHost();host.Inner.Give("P1",1);host.Inner.Give("P2",1);host.Inner.RandomState=1;
        B2SkillEngine.OnEnd(host,"map-entities");Choose(host,"accept");Choose(host,"map-zhimeng-target","P2");
        // 固定洗牌本次保留双方原牌：拥有者未变，不伪造失牌或重新获得。
        Assert.Equal(1,host.GetHand("P1")[0]);Assert.Equal(1,host.GetHand("P2")[0]);Assert.DoesNotContain(host.State.Skills.LossLedger,l=>l.LostHandCards>0);host.Conserved();
        host=ZhimengHost();host.Inner.Give("P1",1);host.Inner.Give("P2",1);host.Inner.RandomState=2;
        B2SkillEngine.OnEnd(host,"map-entities-swap");Choose(host,"accept");Choose(host,"map-zhimeng-target","P2");
        Assert.Equal(1,host.GetHand("P1")[0]);Assert.Equal(1,host.GetHand("P2")[0]);Assert.Equal(2,host.State.Skills.LossLedger.Sum(l=>l.LostHandCards));host.Conserved();
    }
}
