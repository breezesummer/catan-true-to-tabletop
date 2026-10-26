using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Catan.Core.ThreeKingdoms;

namespace Catan.Core.M4
{
    public sealed partial class GameState
    {
        [DataMember(Order=370,EmitDefaultValue=false)] public Port[] B6MapPorts { get; set; }
    }
    public sealed partial class CitiesKnightsGameSession
    {
        private Port[] CurrentPortsB6(GameState s) => s.B6MapPorts??ports;
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
            foreach(var p in s.Knights)Add("Knight",p.LocationId,p.PlayerId);
            foreach(var p in s.Walls)Add("Wall",p.LocationId,p.PlayerId);
            foreach(var p in s.Metropolises)Add("Metropolis",p.LocationId,p.PlayerId);
            return new B6MapSnapshot {Tiles=s.Tiles,Edges=scenario.Topology.Edges,Pieces=pieces.ToArray(),RobberTileId=s.RobberTileId,MerchantTileId=s.MerchantTileId,Ports=CurrentPortsB6(s).Select(p=>new B6MapPort {Id=p.Id,Vertices=p.Vertices,Resource=p.Resource}).ToArray()};
        }
        private void ApplyMapB6(GameState s,string first,string second)
        {
            var swap=B6MapTransaction.Preview(MapB6Snapshot(s),first,second);
            Require(swap!=null,"InvalidMapExchange","这两块板块无法合法交换。");
            var result=swap.Result;
            string At(string kind,string location) => result.Pieces.FirstOrDefault(p=>p.Kind==kind&&p.OriginalLocationId==location)?.LocationId??location;
            foreach(var p in s.Settlements)p.LocationId=At("Settlement",p.LocationId);
            foreach(var p in s.Cities)p.LocationId=At("City",p.LocationId);
            foreach(var p in s.DowngradedCities)p.LocationId=At("DowngradedCity",p.LocationId);
            foreach(var p in s.Roads)p.LocationId=At("Road",p.LocationId);
            foreach(var p in s.Knights)p.LocationId=At("Knight",p.LocationId);
            foreach(var p in s.Walls)p.LocationId=At("Wall",p.LocationId);
            foreach(var p in s.Metropolises)p.LocationId=At("Metropolis",p.LocationId);
            s.Tiles=result.Tiles;s.RobberTileId=result.RobberTileId;s.MerchantTileId=result.MerchantTileId;
            s.B6MapPorts=result.Ports.Select(p=>new Port {Id=p.Id,Vertices=p.Vertices.ToArray(),Resource=p.Resource}).ToArray();
            B6MapTransaction.MoveB5Records(s.ThreeKingdoms,result);RecalculateAwards(s);
        }
    }
}
