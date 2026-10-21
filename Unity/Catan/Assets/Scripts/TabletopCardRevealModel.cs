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
    public static bool IsVictory(string name)=>name=="印刷厂"||name=="石碑";
    public int ProgressIndex=>Array.IndexOf(Catan.Core.DevelopmentCardText.Names,CardName);
    public static IEnumerable<TabletopCardReveal> From(GameHistoryEntry entry)
    {
        if(entry.Kind=="PlayKnight"||entry.Kind=="PlayRoadBuilding"||entry.Kind=="PlayYearOfPlenty"||entry.Kind=="PlayMonopoly"||entry.Kind=="PlayProgressCard")
            yield return new TabletopCardReveal{PlayerId=entry.PlayerId,CardName=(entry.Action??"").Replace("使用「","").Replace("打出「","").TrimEnd('」'),Progress=entry.Kind=="PlayProgressCard"};
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
