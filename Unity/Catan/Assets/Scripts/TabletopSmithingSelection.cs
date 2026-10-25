#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;

// 骑士库存会随晋升释放，必须按整组指令预览，不能只数单独合法的目标。
public static class TabletopSmithingSelection
{
    public static string[][] Plans(string[] ownKnights,Func<string[],bool> legal,int maxTargets=2)
    {
        var knights=(ownKnights??Array.Empty<string>()).Where(id=>!string.IsNullOrEmpty(id)).Distinct().ToArray();
        int limit=Math.Min(Math.Max(maxTargets,0),Math.Min(4,knights.Length));
        var plans=new List<string[]>();
        void Expand(string[] prefix)
        {
            if(prefix.Length>=limit)return;
            foreach(var knight in knights.Where(id=>!prefix.Contains(id)))
            {
                var next=prefix.Concat(new[]{knight}).ToArray();
                // 每次晋升即时扣补库存；不合法的先手不会被后续目标补救。
                if(!legal(next))continue;
                plans.Add(next);Expand(next);
            }
        }
        Expand(Array.Empty<string>());return plans.ToArray();
    }
    public static string[] NextTargets(string[][] plans,string[] selected) => plans
        .Where(plan=>plan.Length>selected.Length&&plan.Take(selected.Length).SequenceEqual(selected))
        .Select(plan=>plan[selected.Length]).Distinct().ToArray();
    public static bool CanConfirm(string[][] plans,string[] selected) => selected.Length>0&&plans.Any(plan=>plan.SequenceEqual(selected));
}
