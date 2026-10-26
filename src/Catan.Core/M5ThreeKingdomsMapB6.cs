using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Catan.Core.ThreeKingdoms;

namespace Catan.Core.M5
{
    public sealed partial class GameState
    {
        [DataMember(Order=370,EmitDefaultValue=false)] public Port[] B6MapPorts { get; set; }
        [DataMember(Order=371,EmitDefaultValue=false)] public B6LandingRecord[] B6LandingRecords { get; set; }
        [DataMember(Order=372,EmitDefaultValue=false)] public string B6MapInitialRobberTileId { get; set; }
        [DataMember(Order=373,EmitDefaultValue=false)] public string B6MapInitialPirateTileId { get; set; }
    }
    public sealed partial class CombinedGameSession
    {
        private string InitialRobberB6(GameState s) => s.B6MapInitialRobberTileId??seafaring.InitialRobberTileId;
        private string InitialPirateB6(GameState s) => s.B6MapInitialPirateTileId??seafaring.InitialPirateTileId;
        private sealed partial class B2Host : IThreeKingdomsB6MapHost
        {
            public B6MapSwap[] GetMapB6Swaps() => B6MapTransaction.Enumerate(session.MapB6Snapshot(game));
            public void ApplyMapB6Swap(string firstTileId,string secondTileId) => session.ApplyMapB6(game,firstTileId,secondTileId);
        }
        private B6MapSnapshot MapB6Snapshot(GameState s)
        {
            var pieces=new List<B6MapPiece>();
            void Add(string kind,string location,string owner) => pieces.Add(new B6MapPiece {Kind=kind,LocationId=location,OriginalLocationId=location,PlayerId=owner});
            foreach(var p in s.Settlements)Add("Settlement",p.LocationId,p.PlayerId);
            foreach(var p in s.Cities)Add("City",p.LocationId,p.PlayerId);
            foreach(var p in s.DowngradedCities)Add("DowngradedCity",p.LocationId,p.PlayerId);
            foreach(var p in s.Roads)Add("Road",p.LocationId,p.PlayerId);
            foreach(var p in s.Ships)Add("Ship",p.LocationId,p.PlayerId);
            foreach(var p in s.Knights)Add("Knight",p.LocationId,p.PlayerId);
            foreach(var p in s.Walls)Add("Wall",p.LocationId,p.PlayerId);
            foreach(var p in s.Metropolises)Add("Metropolis",p.LocationId,p.PlayerId);
            var anchoredVertices=seafaring.Wonders.SelectMany(w=>w.VertexIds).Concat(seafaring.BannedSetupVertexIds)
                .Concat(seafaring.Villages.Select(v=>v.VertexId))
                .Concat(seafaring.Fortresses.SelectMany(f=>new[]{f.VertexId,f.BeachheadVertexId,f.StartingVertexId}))
                .Concat(s.Ships.Where(p=>p.IsInvasionRoute).SelectMany(p=>scenario.Topology.Edges.Single(e=>e.Id==p.LocationId).Vertices))
                .Concat(seafaring.BonusEdgeIds.Concat(seafaring.GiftCardEdgeIds).SelectMany(id=>scenario.Topology.Edges.Single(e=>e.Id==id).Vertices))
                .Concat(seafaring.GiftPorts.SelectMany(p=>p.Vertices)).Distinct().ToArray();
            var protectedTiles=s.Tiles.Where(t=>t.Resource=="fog"||t.Vertices.Any(anchoredVertices.Contains)).Select(t=>t.Id)
                .Concat(seafaring.PiratePath).Concat(seafaring.ForbiddenSettlementTileIds).Distinct().ToArray();
            return new B6MapSnapshot {Tiles=s.Tiles,Edges=scenario.Topology.Edges,Pieces=pieces.ToArray(),RobberTileId=s.RobberTileId,PirateTileId=s.PirateTileId,MerchantTileId=s.MerchantTileId,ProtectedTileIds=protectedTiles,Ports=CurrentPorts(s).Select(p=>new B6MapPort {Id=p.Id,Vertices=p.Vertices,Resource=p.Resource}).ToArray()};
        }
        private void ApplyMapB6(GameState s,string first,string second)
        {
            var swap=B6MapTransaction.Preview(MapB6Snapshot(s),first,second);
            Require(swap!=null,"InvalidMapExchange","这两块板块无法合法交换。");
            var result=swap.Result;
            string MovedTile(string id) => id==first?second:id==second?first:id;
            s.B6MapInitialRobberTileId=MovedTile(InitialRobberB6(s));s.B6MapInitialPirateTileId=MovedTile(InitialPirateB6(s));
            EnsureMapB6LandingRecords(s);
            string At(string kind,string location) => result.Pieces.FirstOrDefault(p=>p.Kind==kind&&p.OriginalLocationId==location)?.LocationId??location;
            foreach(var p in s.Settlements)p.LocationId=At("Settlement",p.LocationId);
            foreach(var p in s.Cities)p.LocationId=At("City",p.LocationId);
            foreach(var p in s.DowngradedCities)p.LocationId=At("DowngradedCity",p.LocationId);
            foreach(var p in s.Roads)p.LocationId=At("Road",p.LocationId);
            foreach(var p in s.Knights)p.LocationId=At("Knight",p.LocationId);
            foreach(var p in s.Walls)p.LocationId=At("Wall",p.LocationId);
            foreach(var p in s.Metropolises)p.LocationId=At("Metropolis",p.LocationId);
            s.Tiles=result.Tiles;s.RobberTileId=result.RobberTileId;s.PirateTileId=result.PirateTileId;s.MerchantTileId=result.MerchantTileId;
            if(s.Special==null)s.B6MapPorts=result.Ports.Select(p=>new Port {Id=p.Id,Vertices=p.Vertices.ToArray(),Resource=p.Resource}).ToArray();
            else s.Special.Ports=result.Ports.Select(p=>new M3.Port {Id=p.Id,Vertices=p.Vertices.ToArray(),Resource=p.Resource}).ToArray();
            B6MapTransaction.MoveB5Records(s.ThreeKingdoms,result);
            foreach(var record in s.B6LandingRecords.Where(r=>!r.Revoked))
            {
                bool remains=s.Tiles.Any(t=>record.SourceTileIds.Contains(t.Id)&&IsLand(t)&&Buildings(s).Any(b=>b.PlayerId==record.PlayerId&&t.Vertices.Contains(b.LocationId)));
                if(!remains){record.Revoked=true;s.Players.Single(p=>p.Id==record.PlayerId).BonusVictoryPoints-=record.Points;}
            }
            RecalculateAwards(s);
        }
        private void EnsureMapB6LandingRecords(GameState s)
        {
            if(s.B6LandingRecords==null)s.B6LandingRecords=Array.Empty<B6LandingRecord>();
            foreach(var p in s.Players)foreach(var id in p.SettledRegions.Where(id=>!p.HomeRegions.Contains(id)))
            {
                if(s.B6LandingRecords.Any(r=>r.PlayerId==p.Id&&r.RegionId==id))continue;
                var region=seafaring.Regions.Single(r=>r.Id==id);
                var sources=s.Tiles.Where(t=>region.TileIds.Contains(t.Id)&&IsLand(t)&&Buildings(s).Any(b=>b.PlayerId==p.Id&&t.Vertices.Contains(b.LocationId))).Select(t=>t.Id).ToArray();
                s.B6LandingRecords=s.B6LandingRecords.Concat(new[]{new B6LandingRecord {PlayerId=p.Id,RegionId=id,SourceTileIds=sources,Points=seafaring.IslandBonusPoints}}).ToArray();
            }
        }
        private void RecordMapB6Landing(GameState s,string player,string region,string vertex)
        {
            if(!B2SkillEngine.B6Enabled(s.ThreeKingdoms))return;
            var records=s.B6LandingRecords??Array.Empty<B6LandingRecord>();if(records.Any(r=>r.PlayerId==player&&r.RegionId==region))return;
            var source=seafaring.Regions.Single(r=>r.Id==region);
            s.B6LandingRecords=records.Concat(new[]{new B6LandingRecord {PlayerId=player,RegionId=region,SourceTileIds=s.Tiles.Where(t=>source.TileIds.Contains(t.Id)&&IsLand(t)&&t.Vertices.Contains(vertex)).Select(t=>t.Id).ToArray(),Points=seafaring.IslandBonusPoints}}).ToArray();
        }
        private static int RevokedMapB6Points(GameState s,string player) => s.B6LandingRecords?.Where(r=>r.PlayerId==player&&r.Revoked).Sum(r=>r.Points)??0;
    }
}
