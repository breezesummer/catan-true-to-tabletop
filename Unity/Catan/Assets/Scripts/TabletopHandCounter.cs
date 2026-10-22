using System;
using System.Globalization;
using System.Collections.Generic;

// 只读手牌计数的展示值；阈值由当前 PlayerView 的公开规则配置提供。
// 资源阈值表示掷出 7 时的保护线，并不阻止玩家继续获得资源。
public readonly struct TabletopHandCounter
{
    public readonly int Count, Limit;
    public TabletopHandCounter(int count, int limit)
    { Count = Math.Max(0, count); Limit = Math.Max(0, limit); }
    public bool OverLimit => Limit > 0 && Count > Limit;
    public string Caption => Count.ToString(CultureInfo.InvariantCulture) + "/" + (Limit > 0 ? Limit.ToString(CultureInfo.InvariantCulture) : "∞");
}

// 保留掷骰前的公开手牌计数，直到骰子实际落定；换局与不同席位分别建立基线。
public sealed class TabletopHandCountPresentation
{
    readonly Dictionary<string,int> counts=new Dictionary<string,int>();
    string scope="";
    public int Observe(string playerScope,string player,bool special,int count,bool hold)
    {
        if(scope!=playerScope){scope=playerScope;counts.Clear();}
        string key=(player??"")+(special?":special":":resources");
        if(hold&&counts.TryGetValue(key,out var previous))return previous;
        return counts[key]=Math.Max(0,count);
    }
}
