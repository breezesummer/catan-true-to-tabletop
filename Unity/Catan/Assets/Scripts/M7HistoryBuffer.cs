using System;
using System.Collections.Generic;
using Catan.Core;

// 按稳定事件键更新；同序号多个技能均保留，原事件补充结果会使内容版本递增。
internal sealed class M7HistoryBuffer
{
    readonly List<GameHistoryEntry> ordered=new List<GameHistoryEntry>();
    readonly Dictionary<string,GameHistoryEntry> byKey=new Dictionary<string,GameHistoryEntry>(StringComparer.Ordinal);
    GameHistoryEntry[] snapshot=Array.Empty<GameHistoryEntry>();
    internal GameHistoryEntry[] Snapshot=>snapshot??(snapshot=ordered.ToArray());
    internal long LastSequence=>ordered.Count==0?0:ordered[ordered.Count-1].Sequence;
    internal void Clear(){ordered.Clear();byKey.Clear();snapshot=Array.Empty<GameHistoryEntry>();}
    internal bool Merge(GameHistoryEntry[] entries)
    {
        bool changed=false;
        foreach(var entry in entries??Array.Empty<GameHistoryEntry>())
        {
            string key=entry.SkillEventId==null?"command:"+entry.Sequence:"skill:"+entry.SkillEventId;
            if(byKey.TryGetValue(key,out var previous))
            {
                if(Same(previous,entry))continue;
                int index=ordered.IndexOf(previous);
                if(previous.Sequence==entry.Sequence){ordered[index]=entry;byKey[key]=entry;changed=true;continue;}
                ordered.RemoveAt(index);
            }
            int low=0,high=ordered.Count;
            while(low<high){int middle=(low+high)/2;if(ordered[middle].Sequence<=entry.Sequence)low=middle+1;else high=middle;}
            ordered.Insert(low,entry);byKey[key]=entry;changed=true;
        }
        if(changed)snapshot=null;
        return changed;
    }
    static bool Same(GameHistoryEntry a,GameHistoryEntry b)
    {
        if(a.Sequence!=b.Sequence||a.Turn!=b.Turn||a.PlayerId!=b.PlayerId||a.OtherPlayerId!=b.OtherPlayerId||a.Kind!=b.Kind||a.Action!=b.Action||a.TargetId!=b.TargetId||a.Dice1!=b.Dice1||a.Dice2!=b.Dice2||a.EventDie!=b.EventDie||a.SkillEventId!=b.SkillEventId||a.GeneralId!=b.GeneralId||a.SkillId!=b.SkillId||a.GeneralTenureId!=b.GeneralTenureId||a.Changes.Length!=b.Changes.Length)return false;
        for(int i=0;i<a.Changes.Length;i++)
        {
            var x=a.Changes[i];var y=b.Changes[i];
            if(x.PlayerId!=y.PlayerId||x.Category!=y.Category||x.GoodIndex!=y.GoodIndex||x.Amount!=y.Amount||x.Name!=y.Name||x.LocationId!=y.LocationId||x.CardTrack!=y.CardTrack||x.IsPrivate!=y.IsPrivate)return false;
        }
        return true;
    }
}
