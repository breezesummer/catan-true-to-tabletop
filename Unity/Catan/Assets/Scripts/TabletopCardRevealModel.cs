#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core;

// 只消费按观看者权限投影的历史；自动公开的分数卡与手动出牌共用展示队列。
public sealed class TabletopCardReveal
{
    public string PlayerId="", CardName="";
    public bool Progress, ImmediateVictory;
    public GameHistoryEntry? Roll;
    public static bool IsVictory(string name)=>name=="印刷术"||name=="宪法";
    public int ProgressIndex=>Array.IndexOf(new[]{"炼金术","起重机","工程学","发明","灌溉","医学","采矿","印刷术","道路建设","锻造","商业港口","行会征费","商人","商船队","资源垄断","商品垄断","外交","间谍","鼓舞","谋略","征税","宪法","叛变","婚礼","破坏"},CardName);
    public static IEnumerable<TabletopCardReveal> From(GameHistoryEntry entry)
    {
        if(entry.Kind=="PlayKnight"||entry.Kind=="PlayRoadBuilding"||entry.Kind=="PlayYearOfPlenty"||entry.Kind=="PlayMonopoly"||entry.Kind=="PlayProgressCard")
            yield return new TabletopCardReveal{PlayerId=entry.PlayerId,CardName=(entry.Action??"").Replace("打出「","").TrimEnd('」'),Progress=entry.Kind=="PlayProgressCard"};
        foreach(var gain in (entry.Changes??Array.Empty<GameHistoryChange>()).Where(c=>!c.IsPrivate&&c.Amount>0&&(c.Category=="defender"||c.Category=="progress"&&IsVictory(c.Name))))
            for(int i=0;i<gain.Amount;i++)
                yield return new TabletopCardReveal{PlayerId=gain.PlayerId,CardName=gain.Name,Progress=true,ImmediateVictory=true,Roll=entry.Kind=="RollDice"?entry:null};
    }
}

public sealed class TabletopCardRevealTimeline
{
    public long Sequence { get; private set; }
    public void Reset(GameHistoryEntry[]? history=null)=>Sequence=history?.LastOrDefault()?.Sequence??0;
    public void Reset(long sequence)=>Sequence=sequence;
    public TabletopCardReveal[] Observe(GameHistoryEntry[] history)
    {
        var reveals=new List<TabletopCardReveal>();
        foreach(var entry in (history??Array.Empty<GameHistoryEntry>()).Where(e=>e.Sequence>Sequence).OrderBy(e=>e.Sequence))
        {Sequence=entry.Sequence;reveals.AddRange(TabletopCardReveal.From(entry));}
        return reveals.ToArray();
    }
}
