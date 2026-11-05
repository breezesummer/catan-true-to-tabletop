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
    public void Initialize(Tile[] tiles,Edge[] edges,Dictionary<string,Vector3> vertices)
    {
        foreach(var e in edges)
        {
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
    public Vector3 Offset(string edge)=>normals.TryGetValue(edge,out var normal)
        ? normal*(HasDock(edge)?CoastalDecals.DockLength+.0075f:.0075f):Vector3.zero;
}

