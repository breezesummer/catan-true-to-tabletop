using System.Reflection;
using System.Text.Json;
using Catan.Core;
using Catan.Core.M5;
using Catan.Core.ThreeKingdoms;
using Catan.Core.TradeModules;
using Xunit;
using GamePhase=Catan.Core.M5.GamePhase;
using CommandKind=Catan.Core.M5.CommandKind;

namespace Catan.Rules.Tests;

public sealed class FishingSeafaringInteractionTests
{
    static CombinedGameSession Fixture(string map)
    {
        var game=CombinedGameSession.Create(map,4,97,tradeModules:new(){Fishing=true});
        var state=game.GetAuthoritativeStateForTesting();
        state.ActivePlayerId="P1";state.Turn=9;state.SetupPlacementIndex=8;state.Phase=GamePhase.Action;
        state.RulesVersion=CombinedGameSession.ThreeKingdomsB6RulesVersion;
        state.ThreeKingdoms=ThreeKingdomsB6ControlTests.State();state.ThreeKingdoms.ControlB6=null;
        M5Harness.SetState(game,state);return game;
    }

    [Fact]
    public void FishShipIsRealConstructionWithZeroResourceReceipt()
    {
        var game=Fixture("four-islands");var state=game.GetAuthoritativeStateForTesting();
        var edge=game.GetPlayerView("P1").Board.Edges.First(e=>state.Tiles.Count(t=>t.Resource!="sea"&&e.Vertices.All(t.Vertices.Contains))==1);
        state.Settlements=[M5Harness.Piece(edge.Vertices[0])];state.PirateTileId=null;
        var fish=state.TradeModules.Fishing;fish.Holdings[0].TokenIds=["F22","F23"];fish.Supply=fish.Supply.Except(fish.Holdings[0].TokenIds).ToArray();
        M5Harness.Balance(state);M5Harness.SetState(game,state);
        var command=game.GetPlayerView("P1").LegalActions.First(c=>c.Kind==CommandKind.SpendFish&&c.FishBuildShip);
        command.Id="三国鱼建船";M5Harness.Accepted(game,command);
        var after=game.GetAuthoritativeStateForTesting();
        Assert.Contains(after.Ships,s=>s.PlayerId=="P1"&&s.LocationId==command.TargetId);
        Assert.Equal(0,after.Players[0].Resources.Total);
        Assert.Contains(after.ThreeKingdoms.Board.Receipts,r=>r.AssetId=="Ship:"+command.TargetId&&r.PaidCount==0);
        Assert.Equal(2,after.TradeModules.Fishing.Discard.Length);
    }

    [Fact]
    public void SeaMapLakeExchangeStaysInlandAndGroundsKeepTheirVertices()
    {
        var game=Fixture("heading-for-new-shores");var state=game.GetAuthoritativeStateForTesting();
        string lake=state.TradeModules.Fishing.LakeTileId;
        var map=(B6MapSnapshot)M5Harness.Invoke(game,"MapB6Snapshot",state)!;
        bool Interior(Tile tile)=>state.Tiles.Count(t=>t!=tile&&t.Resource!="sea"&&t.Vertices.Intersect(tile.Vertices).Count()==2)==6;
        var swaps=state.Tiles.Where(t=>t.Id!=lake).Select(t=>(tile:t,swap:B6MapTransaction.Preview(map,lake,t.Id))).Where(x=>x.swap!=null).ToArray();
        var coastal=swaps.First(x=>!Interior(x.tile));string before=game.Save();
        Assert.Throws<TargetInvocationException>(()=>M5Harness.Invoke(game,"ApplyMapB6",state,lake,coastal.tile.Id));Assert.Equal(before,game.Save());
        var inland=swaps.First(x=>Interior(x.tile));string grounds=JsonSerializer.Serialize(state.TradeModules.Fishing.Grounds);
        M5Harness.Invoke(game,"ApplyMapB6",state,lake,inland.tile.Id);
        Assert.Equal(inland.tile.Id,state.TradeModules.Fishing.LakeTileId);
        Assert.Equal(grounds,JsonSerializer.Serialize(state.TradeModules.Fishing.Grounds));M5Harness.SetState(game,state);
    }
}
