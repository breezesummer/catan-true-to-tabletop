#nullable disable
using System;
using System.Linq;

// 纯显示数据与时间轴；不读取权威状态，不生成或裁决骰点。
public static class TabletopHandLayout
{
    public const float CardWidth = 108, CardHeight = 151, Baseline = 734;
    public static TabletopCardPose Card(int index, int count, float center, float width, bool selected)
    {
        // 卡牌始终使用屏幕逻辑像素；空间不足只压缩间距，不缩小卡面或分页。
        float step = count <= 1 ? 0 : Math.Min(CardWidth + 10, Math.Max(0, width - CardWidth) / (count - 1));
        return new TabletopCardPose {
            X = center - (CardWidth + (count - 1) * step) / 2 + index * step,
            Y = Baseline - (selected ? 22 : 0), Angle = 0
        };
    }
    public static int ResourceCapacity(bool hasSpecialCards) => int.MaxValue;
    public static int SpecialCapacity(bool hasResources) => int.MaxValue;
    public static float ResourceCenter(bool hasSpecialCards) => hasSpecialCards ? 930 : 720;
    public static float SpecialCenter(bool hasResources) => hasResources ? 380 : 720;
}
public sealed class TabletopCardPose
{
    public float X, Y, Angle;
    public float HalfWidth => (float)(Math.Abs(Math.Cos(Angle * Math.PI / 180)) * TabletopHandLayout.CardWidth
        + Math.Abs(Math.Sin(Angle * Math.PI / 180)) * TabletopHandLayout.CardHeight) / 2;
    public float HalfHeight => (float)(Math.Abs(Math.Sin(Angle * Math.PI / 180)) * TabletopHandLayout.CardWidth
        + Math.Abs(Math.Cos(Angle * Math.PI / 180)) * TabletopHandLayout.CardHeight) / 2;
}
public sealed class TabletopCostToken
{
    public string Name;
    public bool Missing;
    public static TabletopCostToken[] For(int[] cost, int[] owned)
    {
        string[] names = { "木头", "砖块", "羊毛", "小麦", "铁矿", "布匹", "钱币", "纸张" };
        return Enumerable.Range(0, 8).SelectMany(good => Enumerable.Range(0, Math.Max(0, cost[good]))
            .Select(ordinal => new TabletopCostToken { Name = names[good], Missing = ordinal >= owned[good] })).ToArray();
    }
}
public sealed class TabletopPromptSequence
{
    public const double Duration = 1.5;
    string[] messages = Array.Empty<string>();
    double started;
    public void Show(double now, params string[] values)
    { started = now; messages = values.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray(); }
    public string At(double now)
    {
        int index = (int)Math.Floor((now - started) / Duration);
        return index < 0 || index >= messages.Length ? "" : messages[index];
    }
}
public sealed class TabletopDicePresentation
{
    public const double Duration = .95;
    public int First { get; private set; } = 1;
    public int Second { get; private set; } = 6;
    public int? Event { get; private set; }
    long sequence;
    bool initialized;
    double started = double.NegativeInfinity;
    public void Observe(long rollSequence, int first, int second, int? eventDie, double now)
    {
        // 首次连接视图（含读档）直接显示已确认结果；相同点数的新掷骰仍会启动动画。
        if (initialized && rollSequence != sequence && rollSequence > 0) started = now;
        initialized = true; sequence = rollSequence;
        First = first > 0 ? first : 1; Second = second > 0 ? second : 6; Event = eventDie;
    }
    public bool IsRolling(double now) => now - started < Duration;
    public float Progress(double now) => (float)Math.Max(0, Math.Min(1, (now - started) / Duration));
    public int Face(int die, double now)
    {
        if (!IsRolling(now)) return die == 0 ? First : die == 1 ? Second : Event ?? 0;
        int step = (int)((now - started) * 20) + die * 3;
        // 只生成过渡画面，不接触任何随机数生成器或最终骰点。
        return die < 2 ? step % 6 + 1 : step % 6 < 3 ? 0 : step % 6 - 2;
    }
}
