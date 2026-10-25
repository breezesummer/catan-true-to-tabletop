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
    GameHistoryEntry[]? observed;
    long baseline;
    readonly Dictionary<string,GameHistoryEntry> skills=new Dictionary<string,GameHistoryEntry>();
    public long Sequence { get; private set; }
    public void Reset(GameHistoryEntry[]? history=null)
    {
        Reset(history?.LastOrDefault()?.Sequence??0);
        foreach(var entry in history??Array.Empty<GameHistoryEntry>())if(entry.SkillEventId!=null)skills[entry.SkillEventId]=entry;
    }
    public void Reset(long sequence){Sequence=baseline=sequence;observed=null;skills.Clear();}
    public TabletopCardReveal[] Observe(GameHistoryEntry[] history)
    {
        if(ReferenceEquals(observed,history))return Array.Empty<TabletopCardReveal>();
        observed=history;long previousSequence=Sequence;
        var reveals=new List<TabletopCardReveal>();
        foreach(var entry in (history??Array.Empty<GameHistoryEntry>()).Where(e=>e.Sequence>previousSequence||e.SkillEventId!=null).OrderBy(e=>e.Sequence))
        {
            Sequence=Math.Max(Sequence,entry.Sequence);
            if(entry.SkillEventId==null){reveals.AddRange(TabletopCardReveal.From(entry));continue;}
            skills.TryGetValue(entry.SkillEventId,out var old);
            if(ReferenceEquals(old,entry))continue;
            skills[entry.SkillEventId]=entry;
            if(old==null&&entry.Sequence<=baseline)continue;
            // 同一发动补写时只添加尚未展示的公开结果，不重新播放原来的特写。
            var previous=old==null?new List<TabletopCardReveal>():TabletopCardReveal.From(old).ToList();
            foreach(var reveal in TabletopCardReveal.From(entry))
            {
                int same=previous.FindIndex(p=>p.PlayerId==reveal.PlayerId&&p.CardName==reveal.CardName&&p.Progress==reveal.Progress&&p.ImmediateVictory==reveal.ImmediateVictory);
                if(same>=0)previous.RemoveAt(same);else reveals.Add(reveal);
            }
        }
        return reveals.ToArray();
    }
}
// 特写是装饰等待，权威结果和完整历史已经保留；超过4条时直接恢复最新视图。
public static class TabletopCardRevealBacklog
{
    public const int Capacity=4;
    public static int Append(Queue<TabletopCardReveal> queue,TabletopCardReveal[] incoming)
    {
        if(queue.Count+incoming.Length>Capacity){int count=queue.Count+incoming.Length;queue.Clear();return count;}
        foreach(var reveal in incoming)queue.Enqueue(reveal);
        return 0;
    }
}
