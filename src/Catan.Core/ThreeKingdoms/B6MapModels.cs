using System;
using System.Linq;
using System.Runtime.Serialization;

namespace Catan.Core.ThreeKingdoms
{
    public interface IThreeKingdomsB6MapHost : IThreeKingdomsCardHost
    {
        B6MapSwap[] GetMapB6Swaps();
        void ApplyMapB6Swap(string firstTileId, string secondTileId);
    }
    public sealed class B6MapSwap
    {
        public string FirstTileId { get; set; }
        public string SecondTileId { get; set; }
        public int FirstRotation { get; set; }
        public int SecondRotation { get; set; }
        public B6MapSnapshot Result { get; set; }
    }
    public sealed class B6MapPiece
    {
        public string Kind { get; set; }
        public string PlayerId { get; set; }
        public string LocationId { get; set; }
        public string OriginalLocationId { get; set; }
        public bool IsEdge => Kind == "Road" || Kind == "Ship";
        public bool IsBuilding => Kind == "Settlement" || Kind == "City";
        public bool Attached => Kind == "Wall" || Kind == "Metropolis" || Kind == "DowngradedCity";
    }
    public sealed class B6MapPort
    {
        public string Id { get; set; }
        public string[] Vertices { get; set; }
        public Resource? Resource { get; set; }
    }
    public sealed class B6MapSnapshot
    {
        public Tile[] Tiles { get; set; } = Array.Empty<Tile>();
        public Edge[] Edges { get; set; } = Array.Empty<Edge>();
        public B6MapPiece[] Pieces { get; set; } = Array.Empty<B6MapPiece>();
        public B6MapPort[] Ports { get; set; } = Array.Empty<B6MapPort>();
        public string[] ProtectedTileIds { get; set; } = Array.Empty<string>();
        public string RobberTileId { get; set; }
        public string PirateTileId { get; set; }
        public string MerchantTileId { get; set; }
        public B6MapSnapshot Copy() => new B6MapSnapshot
        {
            Tiles=Tiles.Select(t=>new Tile {Id=t.Id,Q=t.Q,R=t.R,Resource=t.Resource,Number=t.Number,Vertices=t.Vertices.ToArray()}).ToArray(),
            Edges=Edges, ProtectedTileIds=ProtectedTileIds, RobberTileId=RobberTileId,PirateTileId=PirateTileId,MerchantTileId=MerchantTileId,
            Pieces=Pieces.Select(p=>new B6MapPiece {Kind=p.Kind,PlayerId=p.PlayerId,LocationId=p.LocationId,OriginalLocationId=p.OriginalLocationId??p.LocationId}).ToArray(),
            Ports=Ports.Select(p=>new B6MapPort {Id=p.Id,Vertices=p.Vertices.ToArray(),Resource=p.Resource}).ToArray()
        };
    }
    [DataContract] public sealed class B6LandingRecord
    {
        [DataMember(Order=1)] public string PlayerId { get; set; }
        [DataMember(Order=2)] public string RegionId { get; set; }
        [DataMember(Order=3)] public string[] SourceTileIds { get; set; } = Array.Empty<string>();
        [DataMember(Order=4)] public int Points { get; set; }
        [DataMember(Order=5)] public bool Revoked { get; set; }
        public B6LandingRecord Copy() => new B6LandingRecord {PlayerId=PlayerId,RegionId=RegionId,SourceTileIds=SourceTileIds.ToArray(),Points=Points,Revoked=Revoked};
    }
}
