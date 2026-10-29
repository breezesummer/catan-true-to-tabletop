using System.Reflection;
using Catan.Core;
using Catan.Core.TradeModules;
using Xunit;
using M3=Catan.Core.M3;
using M5=Catan.Core.M5;

namespace Catan.Rules.Tests;
public sealed class FishingExplorationTests
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static object? Invoke(object game,string method,params object[] args)=>game.GetType().GetMethod(method,Flags)!.Invoke(game,args);
    [Theory][InlineData(false,false)][InlineData(false,true)][InlineData(true,false)][InlineData(true,true)]
    public void DiscoverLakeDrawsExactlyOneAndFullHoldingReturnsToOriginalWindow(bool combined,bool full)
    {
        dynamic g=combined ? M5.CombinedGameSession.Create("fog-islands",4,97,tradeModules:new(){Fishing=true}) : M3.SeafarersGameSession.Create("fog-islands",4,97,tradeModules:new(){Fishing=true});
        dynamic state=g.GetAuthoritativeStateForTesting();Tile[] tiles=state.Tiles;Edge[] edges=g.GetPlayerView("P1").Board.Edges;
        var edge=edges.First(e=>tiles.Count(t=>t.Resource=="fog"&&t.Vertices.Any(e.Vertices.Contains))==1);
        var lake=tiles.Single(t=>t.Resource=="fog"&&t.Vertices.Any(edge.Vertices.Contains));
        string[] resources=combined?state.Special.HiddenResources:state.HiddenResources;
        var ordered=new[]{"lake"}.Concat(resources.Where(r=>r!="lake")).ToArray();
        if(combined){state.Special.HiddenResources=ordered;state.Phase=M5.GamePhase.ProductionAwaitRoll;}
        else {state.HiddenResources=ordered;state.Phase=M3.GamePhase.ProductionAwaitRoll;}
        state.Turn=9;state.SetupPlacementIndex=8;state.ActivePlayerId="P1";state.PendingDecision=null;
        FishingState fish=state.TradeModules.Fishing;
        if(full){var ids=Enumerable.Range(1,7).Select(i=>"F"+i.ToString("00")).ToArray();fish.Supply=fish.Supply.Except(ids).ToArray();fish.Holdings[0].TokenIds=ids;}
        // 将下一枚固定为1鱼，保留完整有限供应和旧靴。
        string token=full?"F08":"F01";fish.Supply=new[]{token}.Concat(fish.Supply.Where(t=>t!=token)).ToArray();
        int before=fish.Supply.Length;int numbers=combined?state.Special.HiddenNumbers.Length:state.HiddenNumbers.Length;
        Invoke(g,"Discover",state,"P1",edge.Id);Assert.Equal("lake",lake.Resource);Assert.Null(lake.Number);Assert.Equal(lake.Id,fish.LakeTileId);
        Assert.Equal(numbers,(int)(combined?state.Special.HiddenNumbers.Length:state.HiddenNumbers.Length));
        Invoke(g,"ResumeFishing",state);
        if(full)
        {
            Assert.Equal("P1",fish.PendingPlayerId);Assert.Equal(before,fish.Supply.Length);
            ((object)g).GetType().GetField("state",Flags)!.SetValue(g,state);
            dynamic choice=combined ? new M5.Command {Kind=M5.CommandKind.ResolveFishReplacement} : new M3.Command {Kind=M3.CommandKind.ResolveFishReplacement};
            choice.Id="探索换抽";choice.PlayerId="P1";choice.DecisionId=fish.PendingDecisionId;choice.ExpectedEventSequence=state.EventSequence;choice.TargetId="F01";
            Assert.True((bool)g.Execute(choice).Success);state=g.GetAuthoritativeStateForTesting();fish=state.TradeModules.Fishing;
            Assert.True(fish.Holdings[0].ReplacementUsed);Assert.Equal(7,fish.Holdings[0].TokenIds.Length);Assert.Equal("ProductionAwaitRoll",state.Phase.ToString());
            Assert.True((bool)g.Execute(choice).IsDuplicate);
        }
        else {Assert.Equal(before-1,fish.Supply.Length);Assert.Single(fish.Holdings[0].TokenIds);((object)g).GetType().GetField("state",Flags)!.SetValue(g,state);}
        g.AssertInvariants();
        string publicView=System.Text.Json.JsonSerializer.Serialize((object)g.GetPlayerView("P2"));
        Assert.DoesNotContain("HiddenResources",publicView);Assert.DoesNotContain("QueueIndex",publicView);Assert.DoesNotContain("F08",publicView);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void ShipsNeverAddFishProductionClaims(bool combined)
    {
        dynamic g=combined ? M5.CombinedGameSession.Create("four-islands",4,97,tradeModules:new(){Fishing=true}) : M3.SeafarersGameSession.Create("four-islands",4,97,tradeModules:new(){Fishing=true});
        dynamic state=g.GetAuthoritativeStateForTesting();FishingState fish=state.TradeModules.Fishing;
        Edge[] edges=g.GetPlayerView("P1").Board.Edges;
        string edge=edges.First(e=>e.Vertices.All(fish.Grounds[0].VertexIds.Contains)).Id;
        if(combined)state.Ships=new[]{new M5.ShipPlacement {PlayerId="P1",LocationId=edge,BuiltTurn=0}};
        else state.Ships=new[]{new M3.ShipPlacement {PlayerId="P1",LocationId=edge,BuiltTurn=0}};
        Invoke(g,"QueueFishProduction",state,fish.Grounds[0].Number);
        Assert.Empty((FishDrawClaim[])state.FishProductionClaims);
    }
    [Fact]
    public void MerchantCannotConvertLakeIntoResourceTrade()
    {
        var g=M5.CombinedGameSession.Create("heading-for-new-shores",4,97,tradeModules:new(){Fishing=true});
        var state=g.GetAuthoritativeStateForTesting();var lake=state.Tiles.Single(t=>t.Resource=="lake");
        state.Turn=9;state.SetupPlacementIndex=8;state.ActivePlayerId="P1";state.Phase=M5.GamePhase.Action;
        state.Settlements=new[]{new PiecePlacement{PlayerId="P1",LocationId=lake.Vertices[0]}};state.Players[0].Pieces.Settlements--;
        var deck=state.ProgressDecks.Single(d=>d.Cards.Contains(M5.ProgressCardKind.Merchant));
        var cards=deck.Cards.ToList();cards.Remove(M5.ProgressCardKind.Merchant);deck.Cards=cards.ToArray();state.Players[0].ProgressCards=new[]{M5.ProgressCardKind.Merchant};
        g.GetType().GetField("state",Flags)!.SetValue(g,state);string before=g.Save();
        var result=g.Execute(new(){Id="湖泊不是资源",PlayerId="P1",Kind=M5.CommandKind.PlayProgressCard,ProgressCard=M5.ProgressCardKind.Merchant,TargetId=lake.Id});
        Assert.False(result.Success);Assert.Equal("InvalidMerchantHex",result.ErrorCode);Assert.Equal(before,g.Save());
    }

}
