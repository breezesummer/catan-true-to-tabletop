using System;
using System.IO;
using System.Linq;
namespace Catan.Core.M5
{
    public sealed partial class CombinedGameSession
    {
        private void ValidateSpecialSettlement(GameState s,PlayerState p,string vertex,bool setup)
        {
            var tiles=s.Tiles.Where(t=>t.Vertices.Contains(vertex)&&IsLand(t)).ToArray();
            bool beach=seafaring.Fortresses.Any(f=>f.PlayerId==p.Id&&f.BeachheadVertexId==vertex);
            Require(beach||tiles.Any(t=>!seafaring.ForbiddenSettlementTileIds.Contains(t.Id)),"ForbiddenIsland","该岛不能建村。");
            Require(!s.Special.Fortresses.Any(f=>f.Strength>0&&f.VertexId==vertex)&&!s.Special.Villages.Any(v=>v.VertexId==vertex),"SpecialOccupied","该点已有村庄或要塞。");
            if(setup)Require(!seafaring.BannedSetupVertexIds.Contains(vertex),"InvalidStartingRegion","奇观预留点不能开局定居。");
            if(seafaring.Id=="forgotten-tribe")Require(tiles.Any(t=>t.Number.HasValue),"UnnumberedIsland","部落岛不可定居。");
            if(seafaring.Id=="pirate-islands"&&!tiles.Any(t=>seafaring.StartingTileIds.Contains(t.Id)))Require(beach,"NotBeachhead","只能在己方滩头定居。");
        }
        private void PopulateSpecialView(PlayerView view)
        {
            if(state.Special==null)return;
            view.Board.Villages=Json.Copy(state.Special.Villages);view.Board.Fortresses=Json.Copy(state.Special.Fortresses);
            view.Board.BonusEdgeIds=state.Special.BonusEdgeIds.ToArray();view.Board.GiftCardEdgeIds=state.Special.GiftCardEdgeIds.ToArray();view.Board.GiftPorts=Json.Copy(state.Special.GiftPorts);
            view.Wonders=Json.Copy(seafaring.Wonders);view.HeldPorts=Json.Copy(state.Special.HeldPorts.Where(h=>h.PlayerId==view.PlayerId).Select(h=>h.Port).ToArray());
            foreach(var player in view.Players){var p=state.Players.Single(p=>p.Id==player.Id);player.Cloth=p.Cloth;player.WonderId=p.WonderId;player.WonderLevel=p.WonderLevel;}
        }
        private void ValidateSpecial(GameState s)
        {
            if(s.Special==null)return;
            void Check(bool valid,string message){if(!valid)throw new InvalidDataException(message);}
            var x=s.Special;
            Check(!s.Knights.Any(k=>x.Villages.Any(v=>v.VertexId==k.LocationId)||x.Fortresses.Any(f=>f.Strength>0&&f.VertexId==k.LocationId)),"骑士不能占据中立村庄或未收复要塞。");
            Check(x.HiddenResources.Length==s.Tiles.Count(t=>t.Resource=="fog")&&x.HiddenNumbers.Length==x.HiddenResources.Count(r=>r!="sea"),"迷雾组件不守恒。");
            Check(s.Tiles.Select(t=>t.Resource).Concat(x.HiddenResources).Where(r=>r!="fog").OrderBy(r=>r).SequenceEqual(seafaring.Tiles.Select(t=>t.Resource).Concat(seafaring.HiddenResources).Where(r=>r!="fog").OrderBy(r=>r)),"地形组件不守恒。");
            Check(x.ClothSupply>=0&&x.Villages.All(v=>v.Cloth>=0&&v.Cloth<=5&&v.TradingPlayerIds.Distinct().Count()==v.TradingPlayerIds.Length&&v.TradingPlayerIds.All(id=>s.Players.Any(p=>p.Id==id)))&&s.Players.All(p=>p.Cloth>=0),"布匹状态无效。");
            Check(x.ClothSupply+x.Villages.Sum(v=>v.Cloth)+s.Players.Sum(p=>p.Cloth)==(seafaring.Villages.Length>0?50:0),"布匹不守恒。");
            Check(x.Fortresses.Length==seafaring.Fortresses.Length&&x.Fortresses.All(f=>f.Strength>=0&&f.Strength<=3&&seafaring.Fortresses.Any(o=>o.PlayerId==f.PlayerId&&o.VertexId==f.VertexId)),"要塞状态无效。");
            Check(s.Players.All(p=>p.WonderLevel>=0&&p.WonderLevel<=4&&(p.WonderId==null?p.WonderLevel==0:seafaring.Wonders.Any(w=>w.Id==p.WonderId)))&&s.Players.Where(p=>p.WonderId!=null).Select(p=>p.WonderId).Distinct().Count()==s.Players.Count(p=>p.WonderId!=null),"奇观状态无效。");
            var all=x.Ports.Concat(x.GiftPorts).Concat(x.UnplacedPorts).Concat(x.HeldPorts.Select(h=>h.Port)).ToArray();
            Check(all.Length==seafaring.Ports.Length+seafaring.GiftPorts.Length&&all.Select(p=>p.Id).Distinct().Count()==all.Length,"港口不守恒。");
            Check(x.GiftCardEdgeIds.Distinct().Count()==x.GiftCardEdgeIds.Length&&x.GiftCardEdgeIds.All(seafaring.GiftCardEdgeIds.Contains)&&x.BonusEdgeIds.Distinct().Count()==x.BonusEdgeIds.Length&&x.BonusEdgeIds.All(seafaring.BonusEdgeIds.Contains),"部落奖励状态无效。");
            if(seafaring.Id=="forgotten-tribe")Check(x.BonusEdgeIds.Length+s.Players.Sum(p=>p.BonusVictoryPoints)==seafaring.BonusEdgeIds.Length,"部落分数不守恒。");
        }
    }
}
