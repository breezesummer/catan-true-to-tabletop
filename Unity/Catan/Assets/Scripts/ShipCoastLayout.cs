using System.Collections.Generic;
using System.Linq;
using Catan.Core;
using UnityEngine;

// 仅根据公开岸线和港口确定船的视觉泊位。规则仍使用原有边 ID。
internal sealed class ShipCoastLayout
{
    readonly Dictionary<string,Vector3> normals=new Dictionary<string,Vector3>();
    readonly Dictionary<(string,string),string> edgeIds=new Dictionary<(string,string),string>();
    readonly HashSet<string> docks=new HashSet<string>();
    readonly Dictionary<string,Edge> topology=new Dictionary<string,Edge>();
    readonly Dictionary<string,List<Edge>> incident=new Dictionary<string,List<Edge>>();
    readonly Dictionary<string,string> owners=new Dictionary<string,string>();
    readonly HashSet<(string vertex,string owner)> homes=new HashSet<(string,string)>();
    readonly HashSet<string> reversed=new HashSet<string>();
    public void Initialize(Tile[] tiles,Edge[] edges,Dictionary<string,Vector3> vertices)
    {
        foreach(var e in edges)
        {
            topology[e.Id]=e;
            foreach(var vertex in e.Vertices)
            {
                if(!incident.TryGetValue(vertex,out var neighbours))incident[vertex]=neighbours=new List<Edge>(3);
                neighbours.Add(e);
            }
            edgeIds[(e.Vertices[0],e.Vertices[1])]=e.Id;
            edgeIds[(e.Vertices[1],e.Vertices[0])]=e.Id;
        }
        var counts=new Dictionary<string,int>();
        foreach(var tile in tiles)
        {
            if(tile.Resource=="sea"||tile.Resource=="water"||tile.Resource=="fog"||tile.Resource=="hidden")continue;
            var ring=tile.Vertices;
            var center=ring.Aggregate(Vector3.zero,(sum,id)=>sum+vertices[id])/ring.Length;
            for(int i=0;i<ring.Length;i++)
            {
                var a=ring[i];var b=ring[(i+1)%ring.Length];
                if(!edgeIds.TryGetValue((a,b),out var id))continue;
                counts.TryGetValue(id,out int count);counts[id]=count+1;
                var outward=(vertices[a]+vertices[b])*.5f-center;outward.y=0;
                normals[id]=outward.normalized;
            }
        }
        foreach(var pair in counts)if(pair.Value!=1)normals.Remove(pair.Key);
    }
    public void ClearPorts()=>docks.Clear();
    public void Port(string edge,string[] endpoints)
    {
        if(!string.IsNullOrEmpty(edge))docks.Add(edge);
        else if(endpoints!=null&&endpoints.Length==2&&edgeIds.TryGetValue((endpoints[0],endpoints[1]),out var id))docks.Add(id);
    }
    public bool HasDock(string edge)=>docks.Contains(edge);
    public void SetHomes(PiecePlacement[] settlements,PiecePlacement[] cities)
    {
        homes.Clear();
        foreach(var p in settlements)homes.Add((p.LocationId,p.PlayerId));
        foreach(var p in cities)homes.Add((p.LocationId,p.PlayerId));
    }
    public void BeginShips(){owners.Clear();reversed.Clear();}
    bool StartsAtSecond(string edge,string owner)
    {
        if(!topology.TryGetValue(edge,out var link))return false;
        var a=link.Vertices[0];var b=link.Vertices[1];
        bool homeA=homes.Contains((a,owner)),homeB=homes.Contains((b,owner));
        if(homeA!=homeB)return homeB;
        bool Joins(string v)=>incident[v].Any(e=>e.Id!=edge && owners.TryGetValue(e.Id,out var seat) && seat==owner);
        return !Joins(a) && Joins(b);
    }
    public void PlaceShip(string edge,string owner)
    {
        if(StartsAtSecond(edge,owner))reversed.Add(edge);
        owners[edge]=owner;
    }
    public bool Reversed(string edge)=>reversed.Contains(edge);
    public Quaternion Facing(string edge,string owner,Quaternion along)
    {
        bool flip=owners.ContainsKey(edge)?Reversed(edge):StartsAtSecond(edge,owner);
        return flip?along*Quaternion.Euler(0,180,0):along;
    }

    public void RefreshWakes(TabletopPieceCache cache,Transform pieces,TabletopOcean ocean,
        System.Func<string,Vector3> position)
    {
        cache.Begin("ship-wakes",pieces);
        if(ocean!=null)
        foreach(var pair in owners)
        {
            var edge=topology[pair.Key];
            var joint=edge.Vertices[Reversed(edge.Id)?1:0];
            Edge next=null;
            foreach(var other in incident[joint])
            {
                if(other==edge||!owners.TryGetValue(other.Id,out var owner)||owner!=pair.Value)continue;
                if(next!=null){next=null;break;} // 分岔处各段自行收浪。
                next=other;
            }
            // 两船船艉相对时，各自画至公共顶点，避免同一片水面叠两道泡沫。
            if(next!=null && next.Vertices[Reversed(next.Id)?1:0]==joint)next=null;
            var signature=joint+":"+HasDock(edge.Id)+":"+next?.Id+":"+(next!=null&&HasDock(next.Id));
            using(var change=cache.Update(edge.Id,signature))
            {
                if(!change.Changed)continue;
                var center=position(edge.Id)+Offset(edge.Id);
                var corner=position(joint)+Offset(edge.Id);
                var end=next==null?corner:position(next.Id)+Offset(next.Id);
                ShipWake.Create(pieces,edge.Id,ocean.WakeMaterial,center,corner,end);
            }
        }
        cache.End();
    }
    public Vector3 Offset(string edge)=>normals.TryGetValue(edge,out var normal)
        ? normal*(HasDock(edge)?CoastalDecals.DockLength+.0075f:.0075f):Vector3.zero;
}

