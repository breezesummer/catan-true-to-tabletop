using System;
using System.Globalization;

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
