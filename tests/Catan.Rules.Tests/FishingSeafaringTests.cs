using Catan.Core;
using Catan.Core.TradeModules;
using Xunit;
using M3=Catan.Core.M3;
using M5=Catan.Core.M5;

namespace Catan.Rules.Tests;

public sealed class FishingSeafaringTests(Xunit.Abstractions.ITestOutputHelper output)
{
    public static IEnumerable<object[]> Configurations()
    {
        foreach(var map in new[]{"heading-for-new-shores","four-islands","fog-islands","through-the-desert"})
            foreach(int seats in new[]{3,4}) foreach(bool combined in new[]{false,true})
                foreach(bool kingdoms in combined?new[]{false,true}:new[]{false}) foreach(bool harbor in new[]{false,true})
                    yield return new object[]{map,seats,combined,kingdoms,harbor};
    }
    [Theory][MemberData(nameof(Configurations))]
    public void EveryOpenConfigurationCompletesAndReplays(string map,int seats,bool combined,bool kingdoms,bool harbor)
    {
        dynamic session=combined ? M5.CombinedGameSession.Create(map,seats,97,threeKingdoms:kingdoms,tradeModules:new(){Fishing=true,Harbormaster=harbor}) : M3.SeafarersGameSession.Create(map,seats,97,tradeModules:new(){Fishing=true,Harbormaster=harbor});
        var game=new M6Game(combined?"combined":"seafarers",session);int count=0,spends=0,replacements=0;
        while(game.View().Phase.ToString()!="Finished"&&count<15000)
        {
            dynamic c=combined ? new ThreeKingdomsTestGame("combined",session).Decide() : game.Decide(game.AiView(M6Game.Actor(game.View())));c.Id="F4产品-"+count++;
            if(c.Kind.ToString()=="SpendFish")spends++;
            bool replace=c.Kind.ToString()=="ResolveFishReplacement";
            if(replace)
            {
                replacements++;var checkpoint=game.Save();var restored=M6Game.Load(combined?"combined":"seafarers",checkpoint);
                game.Accept(c);restored.Accept(c);Assert.Equal(game.Save(),restored.Save());
            }
            else game.Accept(c);
            session.AssertInvariants();
        }
        Assert.Equal("Finished",game.View().Phase.ToString());Assert.True(spends>0);
        Assert.Equal(game.Save(),M6Game.Load(combined?"combined":"seafarers",game.Save()).Save());
        output.WriteLine($"{map}/{seats}人/双扩展{combined}/三国{kingdoms}/海港{harbor}：{count}指令，{game.View().Turn}回合，支付{spends}，换抽{replacements}。");
    }

    [Theory][InlineData("heading-for-new-shores")][InlineData("four-islands")][InlineData("fog-islands")][InlineData("through-the-desert")]
    public void MapsAreDeterministicAndConserveSpecifiedComponents(string map)
    {
        foreach(int seats in new[]{3,4}) foreach(uint seed in Enumerable.Range(1,24).Select(i=>(uint)i))
        {
            var g=M3.SeafarersGameSession.Create(map,seats,seed,tradeModules:new(){Fishing=true});g.AssertInvariants();
            var state=g.GetAuthoritativeStateForTesting();var f=state.TradeModules.Fishing;
            Assert.Equal(g.Save(),M3.SeafarersGameSession.Create(map,seats,seed,tradeModules:new(){Fishing=true}).Save());
            Assert.Equal(18,f.Grounds.SelectMany(x=>x.VertexIds).Distinct().Count());
            if(map=="fog-islands") {Assert.Null(f.LakeTileId);Assert.Single(state.HiddenResources,r=>r=="lake");Assert.Equal(9,state.HiddenNumbers.Length);Assert.DoesNotContain(state.HiddenNumbers,n=>n==12);}
            else if(map=="four-islands")Assert.Null(f.LakeTileId);
            else
            {
                var lake=Assert.Single(state.Tiles,t=>t.Resource=="lake");Assert.Null(lake.Number);
                Assert.Equal(6,state.Tiles.Count(t=>t!=lake&&t.Resource!="sea"&&t.Vertices.Intersect(lake.Vertices).Count()==2));
                if(map=="through-the-desert") {Assert.DoesNotContain(state.Tiles,t=>t.Number==2);Assert.Equal(3,state.Tiles.Count(t=>t.Resource=="desert"));}
            }
        }
    }
    static dynamic Fixture(bool combined)
    {
        dynamic session=combined ? M5.CombinedGameSession.Create("four-islands",4,97,tradeModules:new(){Fishing=true}) : M3.SeafarersGameSession.Create("four-islands",4,97,tradeModules:new(){Fishing=true});
        var game=new M6Game(combined?"combined":"seafarers",session);
        for(int i=0;i<16;i++) {dynamic c=game.Decide(game.AiView(M6Game.Actor(game.View())));c.Id="开局-"+i;game.Accept(c);}
        dynamic state=session.GetAuthoritativeStateForTesting();
        state.Turn=9;state.SetupPlacementIndex=8;state.PendingDecision=null;
        if(combined) state.Phase=M5.GamePhase.Action;else state.Phase=M3.GamePhase.Action;
        Tile[] tiles=state.Tiles;Edge[] edges=session.GetPlayerView("P1").Board.Edges;
        var coastal=edges.Where(e=>tiles.Count(t=>e.Vertices.All(t.Vertices.Contains)&&t.Resource!="sea")==1).SelectMany(e=>e.Vertices).ToHashSet();
        state.ActivePlayerId=((IEnumerable<PiecePlacement>)state.Settlements).Concat((IEnumerable<PiecePlacement>)state.Cities).First(b=>coastal.Contains(b.LocationId)).PlayerId;
        FishingState f=state.TradeModules.Fishing;
        f.Supply=f.Supply.Concat(f.Holdings.SelectMany(h=>h.TokenIds)).ToArray();foreach(var h in f.Holdings)h.TokenIds=[];
        var tokens=new[]{"F22","F23","F24"};f.Supply=f.Supply.Except(tokens).ToArray();f.Holdings.Single(h=>h.PlayerId==(string)state.ActivePlayerId).TokenIds=tokens;
        ((object)session).GetType().GetField("state",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(session,state);
        return session;
    }
    static dynamic Fish(bool combined,dynamic session,FishAction action)
    {
        dynamic state=session.GetAuthoritativeStateForTesting();
        dynamic c=combined ? new M5.Command {Kind=M5.CommandKind.SpendFish} : new M3.Command {Kind=M3.CommandKind.SpendFish};
        c.Id=Guid.NewGuid().ToString("N");c.PlayerId=state.ActivePlayerId;c.ExpectedEventSequence=state.EventSequence;c.FishAction=action;c.FishTokenIds=new[]{"F22","F23"};return c;
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void FiveFishBuildsRealShipWithoutResourcesAndRetryIsIdempotent(bool combined)
    {
        dynamic g=Fixture(combined);dynamic c=Fish(combined,g,FishAction.BuildRoad);c.FishBuildShip=true;
        dynamic view=g.GetPlayerView(c.PlayerId);string target=null!;
        var errors=new List<string>();foreach(var edge in view.Board.Edges){c.TargetId=edge.Id;dynamic preview=g.Preview(c);if(preview.Success){target=edge.Id;break;}errors.Add((string)preview.ErrorCode);}
        Assert.True(target!=null,string.Join(",",errors.Distinct()));c.TargetId=target;string before=g.Save();Assert.True((bool)g.Preview(c).Success);Assert.Equal(before,(string)g.Save());
        int resources=g.GetPlayerView(c.PlayerId).OwnResources.Total;Assert.True((bool)g.Execute(c).Success);
        dynamic after=g.GetAuthoritativeStateForTesting();var ships=((System.Collections.IEnumerable)after.Ships).Cast<dynamic>().ToArray();Assert.Contains(ships,s=>(string)s.LocationId==target&&(string)s.PlayerId==(string)c.PlayerId);
        Assert.Equal(resources,(int)g.GetPlayerView(c.PlayerId).OwnResources.Total);Assert.Equal(2,((FishingState)after.TradeModules.Fishing).Discard.Length);
        string saved=g.Save();Assert.True((bool)g.Execute(c).IsDuplicate);Assert.Equal(saved,(string)g.Save());g.AssertInvariants();
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void PirateRemovalRejectsAbsentTargetAndDoesNotSteal(bool combined)
    {
        dynamic g=Fixture(combined);dynamic c=Fish(combined,g,FishAction.RemoveRobber);c.FishRemovePirate=true;
        string before=g.Save();Assert.False((bool)g.Execute(c).Success);Assert.Equal(before,(string)g.Save());
        dynamic state=g.GetAuthoritativeStateForTesting();state.PirateTileId=((IEnumerable<Tile>)state.Tiles).First(t=>t.Resource=="sea").Id;
        ((object)g).GetType().GetField("state",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(g,state);
        int hand=g.GetPlayerView(c.PlayerId).OwnResources.Total;Assert.True((bool)g.Execute(c).Success);Assert.Null((string?)g.GetAuthoritativeStateForTesting().PirateTileId);
        Assert.Equal(hand,(int)g.GetPlayerView(c.PlayerId).OwnResources.Total);g.AssertInvariants();
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void IllegalAndStaleFishActionsLeaveAuthorityUnchanged(bool combined)
    {
        dynamic g=Fixture(combined);dynamic c=Fish(combined,g,FishAction.BuildRoad);c.TargetId="missing";c.FishBuildShip=true;
        var before=(string)g.Save();Assert.False((bool)g.Execute(c).Success);Assert.Equal(before,(string)g.Save());
        c=Fish(combined,g,FishAction.TakeResource);c.ExpectedEventSequence=-1L;Assert.False((bool)g.Execute(c).Success);Assert.Equal(before,(string)g.Save());
        c=Fish(combined,g,FishAction.TakeResource);c.FishTokenIds=new[]{"F22","F22"};Assert.False((bool)g.Execute(c).Success);Assert.Equal(before,(string)g.Save());
    }

}
