using System;
using System.Collections.Generic;
using Catan.Core;

// 行高与累计位置只随历史内容、宽度或字体变化更新；滚动用二分定位可见行。
public sealed class TabletopHistoryLayout
{
    GameHistoryEntry[] entries;
    float width;
    readonly Dictionary<GameHistoryEntry,float> heights=new Dictionary<GameHistoryEntry,float>();
    float[] offsets={0};
    public float Total=>offsets[offsets.Length-1];
    public float Top(int index)=>offsets[index];
    public float Height(int index)=>offsets[index+1]-offsets[index];
    public void Reset(){entries=null;heights.Clear();offsets=new float[]{0};}
    public void Bind(GameHistoryEntry[] value,float availableWidth,Func<GameHistoryEntry,float,float> measure)
    {
        if(ReferenceEquals(entries,value)&&width==availableWidth)return;
        if(width!=availableWidth)heights.Clear();
        width=availableWidth;entries=value;offsets=new float[value.Length+1];
        var retained=new HashSet<GameHistoryEntry>(value);
        var remove=new List<GameHistoryEntry>();foreach(var key in heights.Keys)if(!retained.Contains(key))remove.Add(key);
        foreach(var key in remove)heights.Remove(key);
        for(int i=0;i<value.Length;i++)
        {
            if(!heights.TryGetValue(value[i],out float height))heights[value[i]]=height=Math.Max(1,measure(value[i],availableWidth));
            offsets[i+1]=offsets[i]+height;
        }
    }
    public int FirstVisible(float scroll)
    {
        int found=Array.BinarySearch(offsets,scroll);
        return Math.Max(0,Math.Min(offsets.Length-2,found>=0?found:~found-1));
    }
}
