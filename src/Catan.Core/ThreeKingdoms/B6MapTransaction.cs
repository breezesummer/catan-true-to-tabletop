using System;
using System.Collections.Generic;
using System.Linq;

namespace Catan.Core.ThreeKingdoms
{
    /// <summary>榻谟的纯预检；不持有宿主随机源，所有棋子与港口都在副本内形成完整结果。</summary>
    public static class B6MapTransaction
    {
        public static bool Land(Tile t) => t.Resource != "sea" && t.Resource != "fog";
        private static bool Blank(Tile t) => t.Resource == "sea" || t.Resource == "desert";
        public static B6MapSwap[] Enumerate(B6MapSnapshot map)
        {
            var result=new List<B6MapSwap>();var ids=map.Tiles.Select(t=>t.Id).OrderBy(x=>x,StringComparer.Ordinal).ToArray();var components=Components(map);var portOrder=PortOrder(map);
            for(int i=0;i<ids.Length;i++)for(int j=i+1;j<ids.Length;j++)
            { var swap=Preview(map,ids[i],ids[j],components,portOrder); if(swap!=null)result.Add(swap); }
            return result.ToArray();
        }
        public static B6MapSwap Preview(B6MapSnapshot map,string first,string second)
            => map==null?null:Preview(map,first,second,Components(map),PortOrder(map));
        private static B6MapSwap Preview(B6MapSnapshot map,string first,string second,B6MapPiece[][] components,Dictionary<string,Edge[]> portOrder)
        {
            if(map==null||first==second)return null;
            var a=map.Tiles.FirstOrDefault(t=>t.Id==first);var b=map.Tiles.FirstOrDefault(t=>t.Id==second);
            if(a==null||b==null||a.Vertices.Length!=6||b.Vertices.Length!=6||a.Resource=="gold"||b.Resource=="gold"||a.Resource=="fog"||b.Resource=="fog"||map.ProtectedTileIds.Contains(first)||map.ProtectedTileIds.Contains(second))return null;
            if((Blank(a)||Blank(b))&&(a.Number==6||a.Number==8||b.Number==6||b.Number==8))return null;
            var movableA=Movable(map,a,b,components);var movableB=Movable(map,b,a,components);
            for(int ra=0;ra<6;ra++)for(int rb=0;rb<6;rb++)
            {
                var next=map.Copy();var na=next.Tiles.Single(t=>t.Id==first);var nb=next.Tiles.Single(t=>t.Id==second);
                na.Resource=b.Resource;na.Number=b.Number;nb.Resource=a.Resource;nb.Number=a.Number;
                if(!Move(next,a,b,ra,movableA)||!Move(next,b,a,rb,movableB))continue;
                next.RobberTileId=Swap(next.RobberTileId,first,second);next.PirateTileId=Swap(next.PirateTileId,first,second);next.MerchantTileId=Swap(next.MerchantTileId,first,second);
                if(!Legal(next)||!RelocatePorts(next,portOrder))continue;
                return new B6MapSwap {FirstTileId=first,SecondTileId=second,FirstRotation=ra,SecondRotation=rb,Result=next};
            }
            return null;
        }
        private static string Swap(string value,string a,string b) => value==a?b:value==b?a:value;
        private static string[] Vertices(B6MapSnapshot map,B6MapPiece p) => p.IsEdge?map.Edges.Single(e=>e.Id==p.LocationId).Vertices:new[]{p.LocationId};
        private static B6MapPiece[][] Components(B6MapSnapshot map)
        {
            var remaining=map.Pieces.Where(p=>!p.Attached).ToList();var result=new List<B6MapPiece[]>();
            while(remaining.Count>0)
            {
                var component=new List<B6MapPiece>{remaining[0]};remaining.RemoveAt(0);var vertices=new HashSet<string>(Vertices(map,component[0]));
                bool changed=true;while(changed){changed=false;foreach(var piece in remaining.ToArray())if(Vertices(map,piece).Any(vertices.Contains)){component.Add(piece);remaining.Remove(piece);vertices.UnionWith(Vertices(map,piece));changed=true;}}
                result.Add(component.ToArray());
            }
            return result.ToArray();
        }
        private static HashSet<string> Movable(B6MapSnapshot map,Tile own,Tile other,B6MapPiece[][] components)
        {
            var result=new HashSet<string>();
            foreach(var component in components)
            {
                if(component.Any(p=>p.Kind=="Ship"))continue;
                var vs=component.SelectMany(p=>Vertices(map,p)).Distinct().ToArray();
                if(!vs.All(own.Vertices.Contains)||vs.Any(v=>map.Tiles.Any(t=>t.Id!=own.Id&&Land(t)&&t.Vertices.Contains(v))))continue;
                foreach(var p in component)result.Add(p.Kind+":"+p.LocationId);
                foreach(var p in map.Pieces.Where(p=>p.Attached&&vs.Contains(p.LocationId)))result.Add(p.Kind+":"+p.LocationId);
            }
            return result;
        }
        private static bool Move(B6MapSnapshot next,Tile from,Tile to,int rotation,HashSet<string> ids)
        {
            string V(string id){int index=Array.IndexOf(from.Vertices,id);return index<0?null:to.Vertices[(index+rotation)%6];}
            foreach(var p in next.Pieces.Where(p=>ids.Contains(p.Kind+":"+p.OriginalLocationId)))
            {
                if(p.IsEdge){var original=next.Edges.Single(e=>e.Id==p.OriginalLocationId);var ends=original.Vertices.Select(V).ToArray();var edge=next.Edges.FirstOrDefault(e=>e.Vertices.All(ends.Contains));if(edge==null)return false;p.LocationId=edge.Id;}
                else {var vertex=V(p.OriginalLocationId);if(vertex==null)return false;p.LocationId=vertex;}
            }
            return true;
        }
        private static bool Legal(B6MapSnapshot map)
        {
            if(map.Pieces.Where(p=>p.IsEdge).GroupBy(p=>p.LocationId).Any(g=>g.Count()>1))return false;
            if(map.Pieces.Where(p=>!p.IsEdge&&!p.Attached).GroupBy(p=>p.LocationId).Any(g=>g.Count()>1))return false;
            var buildings=map.Pieces.Where(p=>p.IsBuilding).ToArray();
            foreach(var p in map.Pieces.Where(p=>!p.Attached))
            {
                var vs=Vertices(map,p);var tiles=map.Tiles.Where(t=>vs.All(t.Vertices.Contains)).ToArray();
                if(p.Kind=="Ship"?!(tiles.Length==1||tiles.Any(t=>!Land(t))):!tiles.Any(Land))return false;
                if(p.IsBuilding&&map.Edges.Any(e=>e.Vertices.Contains(p.LocationId)&&buildings.Any(q=>q!=p&&e.Vertices.Contains(q.LocationId))))return false;
            }
            if(map.RobberTileId!=null&&map.RobberTileId!="frame"&&!map.Tiles.Any(t=>t.Id==map.RobberTileId&&Land(t)))return false;
            if(map.PirateTileId!=null&&map.PirateTileId!="frame"&&!map.Tiles.Any(t=>t.Id==map.PirateTileId&&t.Resource=="sea"))return false;
            if(map.MerchantTileId!=null&&!map.Tiles.Any(t=>t.Id==map.MerchantTileId&&Land(t)&&t.Resource!="gold"))return false;
            return true;
        }
        private static bool Coast(B6MapSnapshot map,Edge edge)
        {var ts=map.Tiles.Where(t=>edge.Vertices.All(t.Vertices.Contains)).ToArray();return ts.Any(Land)&&(ts.Length==1||ts.Any(t=>t.Resource=="sea"));}
        // 几何最短距与地形无关，同一轮候选枚举只计算一次，避免每对板块重复广搜。
        private static Dictionary<string,Edge[]> PortOrder(B6MapSnapshot map)
        {
            var result=new Dictionary<string,Edge[]>();
            var neighbors=map.Edges.SelectMany(e=>e.Vertices.Select(v=>new {Vertex=v,Other=e.Vertices[0]==v?e.Vertices[1]:e.Vertices[0]})).GroupBy(x=>x.Vertex).ToDictionary(g=>g.Key,g=>g.Select(x=>x.Other).Distinct().ToArray());
            foreach(var port in map.Ports)
            {
                var distances=new Dictionary<string,int>();var queue=new Queue<string>();foreach(var v in port.Vertices){distances[v]=0;queue.Enqueue(v);}
                while(queue.Count>0){var v=queue.Dequeue();if(!neighbors.TryGetValue(v,out var adjacent))continue;foreach(var w in adjacent)if(!distances.ContainsKey(w)){distances[w]=distances[v]+1;queue.Enqueue(w);}}
                result[port.Id]=map.Edges.Where(e=>e.Vertices.Any(distances.ContainsKey)).OrderBy(e=>e.Vertices.Min(v=>distances.TryGetValue(v,out int d)?d:int.MaxValue)).ThenBy(e=>e.Id,StringComparer.Ordinal).ToArray();
            }
            return result;
        }
        private static bool RelocatePorts(B6MapSnapshot map,Dictionary<string,Edge[]> portOrder)
        {
            var occupied=new HashSet<string>();var pending=new List<B6MapPort>();
            foreach(var port in map.Ports.OrderBy(p=>p.Id,StringComparer.Ordinal))
            {var edge=map.Edges.FirstOrDefault(e=>e.Vertices.All(port.Vertices.Contains));if(edge!=null&&Coast(map,edge)&&!edge.Vertices.Any(occupied.Contains))occupied.UnionWith(edge.Vertices);else pending.Add(port);}
            foreach(var port in pending)
            {
                var edge=portOrder[port.Id].FirstOrDefault(e=>!e.Vertices.Any(occupied.Contains)&&Coast(map,e));
                if(edge==null)return false;port.Vertices=edge.Vertices.ToArray();occupied.UnionWith(edge.Vertices);
            }
            return true;
        }
        public static void MoveB5Records(ThreeKingdomsState state,B6MapSnapshot map)
        {
            if(state?.Board==null)return;
            string Location(string kind,string id) => map.Pieces.FirstOrDefault(p=>(p.Kind==kind||kind=="City"&&p.Kind=="Settlement")&&p.OriginalLocationId==id)?.LocationId??id;
            foreach(var damage in state.Board.Damages)damage.LocationId=Location(damage.Kind,damage.LocationId);
            foreach(var receipt in state.Board.Receipts){int colon=receipt.AssetId.IndexOf(':');if(colon<0)continue;var kind=receipt.AssetId.Substring(0,colon);receipt.AssetId=kind+":"+Location(kind,receipt.AssetId.Substring(colon+1));}
        }
    }
}
