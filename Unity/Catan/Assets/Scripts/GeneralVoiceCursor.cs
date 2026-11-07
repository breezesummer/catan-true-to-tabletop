using System;
using Catan.Core.ThreeKingdoms;

// 只消费可见投影中新增的发动事件；完成状态更新不产生第二次声音。
public sealed class GeneralVoiceCursor
{
    int count;
    string tail=string.Empty;
    bool initialized;
    public void Reset() { count=0;tail=string.Empty;initialized=false; }
    public int Observe(B4SkillHistoryView[] history, bool quiet, Action<string> emit)
        => ObserveEvents(history,quiet,entry=>emit(entry.SkillId));
    public int ObserveEvents(B4SkillHistoryView[] history, bool quiet, Action<B4SkillHistoryView> emit)
    {
        history=history??Array.Empty<B4SkillHistoryView>();
        bool baseline=!initialized||quiet||history.Length<count||count>0&&history[count-1].EventId!=tail;
        int start=baseline?history.Length:count;
        count=history.Length;tail=count==0?string.Empty:history[count-1].EventId;initialized=true;
        for(int i=start;i<history.Length;i++)if(!string.IsNullOrEmpty(history[i].SkillId))emit(history[i]);
        return history.Length-start;
    }
}
