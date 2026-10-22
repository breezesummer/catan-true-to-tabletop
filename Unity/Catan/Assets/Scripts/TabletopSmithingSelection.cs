#nullable disable
using System;
using System.Linq;

// 骑士库存会随晋升释放，必须按整组指令预览，不能只数单独合法的目标。
public static class TabletopSmithingSelection
{
    public static string[][] Plans(string[] ownKnights,Func<string[],bool> legal)
    {
        var pairs=ownKnights.SelectMany(first=>ownKnights.Where(second=>second!=first).Select(second=>new[]{first,second})).Where(legal).ToArray();
        return pairs.Length>0?pairs:ownKnights.Select(id=>new[]{id}).Where(legal).ToArray();
    }
}
