using System;
using System.Linq;
using Catan.Core;
using Catan.Core.TradeModules;
using M2=Catan.Core.M2;
using M4=Catan.Core.M4;
using M3=Catan.Core.M3;
using M5=Catan.Core.M5;

namespace Catan.AI
{
    /// <summary>只读所属投影；保存筹码组合价值，优先解除己方封锁、补短缺与扩路。</summary>
    internal static class FishingAi
    {
        internal static double VertexValue(FishingView fish, Tile[] tiles, string vertex) => fish == null ? 0 :
            fish.Grounds.Where(g=>g.VertexIds.Contains(vertex)).Sum(g=>6-Math.Abs(7-g.Number))*.8 +
            (tiles.Any(t=>t.Id==fish.LakeTileId&&t.Vertices.Contains(vertex)) ? 4.8 : 0);
        static double Value(FishAction action, Resource resource, string edge, string victim, ResourceBag hand, PiecePlacement[] buildings,
            Tile[] tiles, string robber, string player, Edge[] edges, string[] settlements, int victimScore, int tokenCount)
        {
            if(action==FishAction.RemoveRobber) return tiles.Any(t=>t.Id==robber&&buildings.Any(b=>b.PlayerId==player&&t.Vertices.Contains(b.LocationId))) ? 100 : -1;
            if(action==FishAction.TakeResource) return hand[resource]<2 ? 40-hand[resource]*8 : tokenCount>=6 ? 5 : -1;
            if(action==FishAction.BuildRoad) return edges.Any(e=>e.Id==edge&&e.Vertices.Any(v=>!settlements.Contains(v))) ? 28 : -1;
            if(action==FishAction.BuyDevelopmentCard) return 20;
            return tokenCount>=6 ? 10+victimScore : -1;
        }
        internal static M2.Command Choose(M2.PlayerView v)
        {
            var f=v.TradeModules?.Fishing;if(f==null)return null;
            var legal=v.LegalFishingActions;
            if(f.PendingPlayerId==v.PlayerId) return legal.Where(c=>c.Kind==M2.CommandKind.ResolveFishReplacement)
                .OrderBy(c=>c.TargetId==null?1.5:f.OwnTokens.Single(t=>t.Id==c.TargetId).Value).FirstOrDefault();
            var boot=legal.Where(c=>c.Kind==M2.CommandKind.PassOldBoot).OrderByDescending(c=>v.Players.Single(p=>p.Id==c.OtherPlayerId).VictoryPoints).FirstOrDefault();if(boot!=null)return boot;
            return legal.Where(c=>c.Kind==M2.CommandKind.SpendFish).Select(c=>(c,value:Value(c.FishAction.Value,c.Resource,c.TargetId,c.OtherPlayerId,v.OwnResources,
                v.Board.Settlements.Concat(v.Board.Cities).ToArray(),v.Board.Tiles,v.Board.RobberTileId,v.PlayerId,v.Board.Edges,v.Board.Settlements.Concat(v.Board.Cities).Select(b=>b.LocationId).ToArray(),v.Players.FirstOrDefault(p=>p.Id==c.OtherPlayerId)?.VictoryPoints??0,f.OwnTokens.Length)))
                .Where(x=>x.value>0).OrderByDescending(x=>x.value).Select(x=>x.c).FirstOrDefault();
        }
        internal static M4.Command Choose(M4.PlayerView v)
        {
            var f=v.TradeModules?.Fishing;if(f==null)return null;
            var legal=v.LegalActions;
            if(f.PendingPlayerId==v.PlayerId) return legal.Where(c=>c.Kind==M4.CommandKind.ResolveFishReplacement)
                .OrderBy(c=>c.TargetId==null?1.5:f.OwnTokens.Single(t=>t.Id==c.TargetId).Value).FirstOrDefault();
            var boot=legal.Where(c=>c.Kind==M4.CommandKind.PassOldBoot).OrderByDescending(c=>v.Players.Single(p=>p.Id==c.OtherPlayerId).VictoryPoints).FirstOrDefault();if(boot!=null)return boot;
            return legal.Where(c=>c.Kind==M4.CommandKind.SpendFish).Select(c=>(c,value:Value(c.FishAction.Value,c.Resource,c.TargetId,c.OtherPlayerId,v.OwnResources,
                v.Board.Settlements.Concat(v.Board.Cities).ToArray(),v.Board.Tiles,v.Board.RobberTileId,v.PlayerId,v.PlayerId==null?Array.Empty<Edge>():v.Board.Edges,v.Board.Settlements.Concat(v.Board.Cities).Select(b=>b.LocationId).ToArray(),v.Players.FirstOrDefault(p=>p.Id==c.OtherPlayerId)?.VictoryPoints??0,f.OwnTokens.Length)))
                .Where(x=>x.value>0).OrderByDescending(x=>x.value).Select(x=>x.c).FirstOrDefault();
        }
        internal static M3.Command Choose(M3.PlayerView v)
        {
            var f=v.TradeModules?.Fishing;if(f==null)return null;
            var legal=v.LegalFishingActions;
            if(f.PendingPlayerId==v.PlayerId) return legal.Where(c=>c.Kind==M3.CommandKind.ResolveFishReplacement)
                .OrderBy(c=>c.TargetId==null?1.5:f.OwnTokens.Single(t=>t.Id==c.TargetId).Value).FirstOrDefault();
            var boot=legal.Where(c=>c.Kind==M3.CommandKind.PassOldBoot).OrderByDescending(c=>v.Players.Single(p=>p.Id==c.OtherPlayerId).VictoryPoints).FirstOrDefault();if(boot!=null)return boot;
            return legal.Where(c=>c.Kind==M3.CommandKind.SpendFish).Select(c=>(c,value:Value(c.FishAction.Value,c.Resource,c.TargetId,c.OtherPlayerId,v.OwnResources,
                v.Board.Settlements.Concat(v.Board.Cities).ToArray(),v.Board.Tiles,v.Board.RobberTileId,v.PlayerId,v.Board.Edges,v.Board.Settlements.Concat(v.Board.Cities).Select(b=>b.LocationId).ToArray(),v.Players.FirstOrDefault(p=>p.Id==c.OtherPlayerId)?.VictoryPoints??0,f.OwnTokens.Length)))
                .Where(x=>x.value>0).OrderByDescending(x=>x.value).Select(x=>x.c).FirstOrDefault();
        }
        internal static M5.Command Choose(M5.PlayerView v)
        {
            var f=v.TradeModules?.Fishing;if(f==null)return null;
            var legal=v.LegalActions;
            if(f.PendingPlayerId==v.PlayerId) return legal.Where(c=>c.Kind==M5.CommandKind.ResolveFishReplacement)
                .OrderBy(c=>c.TargetId==null?1.5:f.OwnTokens.Single(t=>t.Id==c.TargetId).Value).FirstOrDefault();
            var boot=legal.Where(c=>c.Kind==M5.CommandKind.PassOldBoot).OrderByDescending(c=>v.Players.Single(p=>p.Id==c.OtherPlayerId).VictoryPoints).FirstOrDefault();if(boot!=null)return boot;
            return legal.Where(c=>c.Kind==M5.CommandKind.SpendFish).Select(c=>(c,value:Value(c.FishAction.Value,c.Resource,c.TargetId,c.OtherPlayerId,v.OwnResources,
                v.Board.Settlements.Concat(v.Board.Cities).ToArray(),v.Board.Tiles,v.Board.RobberTileId,v.PlayerId,v.PlayerId==null?Array.Empty<Edge>():v.Board.Edges,v.Board.Settlements.Concat(v.Board.Cities).Select(b=>b.LocationId).ToArray(),v.Players.FirstOrDefault(p=>p.Id==c.OtherPlayerId)?.VictoryPoints??0,f.OwnTokens.Length)))
                .Where(x=>x.value>0).OrderByDescending(x=>x.value).Select(x=>x.c).FirstOrDefault();
        }
    }
}
