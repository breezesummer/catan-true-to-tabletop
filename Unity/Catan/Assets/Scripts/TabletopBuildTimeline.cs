#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;

// 仅接收 PlayerView 已公开的棋子快照；本时间轴不执行指令、不读取手牌或随机状态。
public sealed class TabletopBuildPiece
{
    public string Kind, PlayerId, LocationId;
    public int Level=1;
    public bool Active;
    // 仅本次增量携带的旧公开棋子；不进入存档或规则身份。
    public TabletopBuildPiece Previous;
    public string Key => Kind+":"+LocationId;
    public string Identity => Key+":"+PlayerId+":"+Level;
}

public static class TabletopAiSpeed
{
    public static readonly string[] Names={"慢速 · 观摩","标准","快速"};
    public static int Clamp(int level)=>Math.Max(0,Math.Min(2,level));
    public static double Interval(int level)=>level==0?1.7:level==2?.55:1.05;
    public static double FlightDuration(int level)=>level==0?1.15:level==2?.36:.72;
}

public sealed class TabletopBuildTimeline
{
    readonly Dictionary<string,TabletopBuildPiece> previous=new Dictionary<string,TabletopBuildPiece>();
    readonly Dictionary<string,TabletopBuildPiece> highlighted=new Dictionary<string,TabletopBuildPiece>();
    bool initialized;
    string context;
    public TabletopBuildPiece[] Highlighted=>highlighted.Values.ToArray();
    public bool Contains(TabletopBuildPiece piece)=>piece!=null&&previous.TryGetValue(piece.Key,out var current)&&current.Identity==piece.Identity;

    public TabletopBuildPiece[] Observe(int turn,string activePlayer,IEnumerable<TabletopBuildPiece> pieces)
    {
        var current=pieces.ToDictionary(piece=>piece.Key);
        string nextContext=turn+":"+activePlayer;
        if(context!=nextContext){highlighted.Clear();context=nextContext;}
        var added=initialized?current.Values.Where(piece=>(!previous.TryGetValue(piece.Key,out var old)||old.Identity!=piece.Identity)&&!IsDowngrade(piece)).ToArray():Array.Empty<TabletopBuildPiece>();
        var removed=previous.Values.Where(old=>!current.ContainsKey(old.Key)).ToList();
        foreach(var piece in added)
        {
            piece.Previous=previous.Values.FirstOrDefault(old=>old.PlayerId==piece.PlayerId&&old.LocationId==piece.LocationId&&
                (BuildingRank(old.Kind)>0&&BuildingRank(piece.Kind)>BuildingRank(old.Kind)||old.Kind=="knight"&&piece.Kind=="knight"&&old.Level<piece.Level));
            if(piece.Previous==null&&(piece.Kind=="knight"||piece.Kind=="ship"||piece.Kind=="road"))
            {
                var candidates=removed.Where(old=>old.Kind==piece.Kind&&old.PlayerId==piece.PlayerId&&old.Level==piece.Level).ToArray();
                // 合并多个网络动作而无法唯一配对时，不猜测棋子来源。
                if(candidates.Length==1&&added.Count(p=>p.Kind==piece.Kind&&p.PlayerId==piece.PlayerId&&p.Level==piece.Level)==1)
                {piece.Previous=candidates[0];removed.Remove(candidates[0]);}
            }
        }
        foreach(var key in highlighted.Keys.Where(key=>!current.TryGetValue(key,out var now)||now.Identity!=highlighted[key].Identity).ToArray())highlighted.Remove(key);
        foreach(var piece in added)if(piece.PlayerId==activePlayer)highlighted[piece.Key]=piece;
        previous.Clear();foreach(var pair in current)previous.Add(pair.Key,new TabletopBuildPiece{Kind=pair.Value.Kind,PlayerId=pair.Value.PlayerId,LocationId=pair.Value.LocationId,Level=pair.Value.Level,Active=pair.Value.Active});
        initialized=true;
        return added;
    }
    bool IsDowngrade(TabletopBuildPiece piece)
    {
        int rank=BuildingRank(piece.Kind);
        if(rank>0)return previous.Values.Any(old=>old.LocationId==piece.LocationId&&old.PlayerId==piece.PlayerId&&BuildingRank(old.Kind)>rank);
        return piece.Kind=="knight"&&previous.TryGetValue(piece.Key,out var knight)&&knight.PlayerId==piece.PlayerId&&knight.Level>piece.Level;
    }
    static int BuildingRank(string kind)=>kind=="settlement"?1:kind=="city"?2:kind=="city-with-wall"?3:kind=="metropolis"?4:kind=="metropolis-with-wall"?5:0;
}

// 公开道路／船线上的确定性最短路径；对手建筑阻断中间节点。
public static class TabletopMovementPath
{
    public static string[] Find(string from,string to,string player,Catan.Core.Edge[] edges,Catan.Core.PiecePlacement[] routes,Catan.Core.PiecePlacement[] buildings)
    {
        if(from==to)return new[]{from};
        var owned=new HashSet<string>(routes.Where(r=>r.PlayerId==player).Select(r=>r.LocationId));
        var blocked=new HashSet<string>(buildings.Where(b=>b.PlayerId!=player).Select(b=>b.LocationId));
        var graph=new Dictionary<string,List<string>>();
        foreach(var edge in edges.Where(e=>owned.Contains(e.Id)).OrderBy(e=>e.Id,StringComparer.Ordinal))
        {
            if(edge.Vertices.Length!=2)continue;
            for(int i=0;i<2;i++){string a=edge.Vertices[i],b=edge.Vertices[1-i];if(!graph.TryGetValue(a,out var next))graph[a]=next=new List<string>();next.Add(b);}
        }
        var queue=new Queue<string>();var previous=new Dictionary<string,string>{{from,null}};queue.Enqueue(from);
        while(queue.Count>0)
        {
            string vertex=queue.Dequeue();
            if(vertex==to){var path=new List<string>();for(string v=to;v!=null;v=previous[v])path.Add(v);path.Reverse();return path.ToArray();}
            if(vertex!=from&&blocked.Contains(vertex)||!graph.TryGetValue(vertex,out var neighbors))continue;
            foreach(var neighbor in neighbors)if(!previous.ContainsKey(neighbor)){previous[neighbor]=vertex;queue.Enqueue(neighbor);}
        }
        return Array.Empty<string>();
    }
}
