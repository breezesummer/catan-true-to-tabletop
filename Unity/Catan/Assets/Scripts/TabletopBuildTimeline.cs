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
