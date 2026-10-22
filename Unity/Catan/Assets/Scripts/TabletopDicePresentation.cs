#nullable disable
using System;
using System.Linq;
using Catan.Core;

// 所有掷骰后果共用真实展示时钟；不会改变规则结算或读取随机状态。
public sealed class TabletopDicePresentation
{
    public const double Duration = .95;
    public int First { get; private set; } = 1;
    public int Second { get; private set; } = 6;
    public int? Event { get; private set; }
    public long Sequence { get; private set; }
    public double SettlesAt => started + Duration;
    bool initialized;
    double started = double.NegativeInfinity;
    public void Observe(long rollSequence, int first, int second, int? eventDie, double now)
    {
        // 首次连接视图（含读档）直接显示已确认结果；相同点数的新掷骰仍会启动动画。
        if (initialized && rollSequence != Sequence && rollSequence > 0) started = now;
        initialized = true; Sequence = rollSequence;
        First = first > 0 ? first : 1; Second = second > 0 ? second : 6; Event = eventDie;
    }
    public bool IsRolling(double now) => now < SettlesAt;
    public float Progress(double now) => (float)Math.Max(0, Math.Min(1, (now - started) / Duration));
    public int Face(int die, double now)
    {
        if (!IsRolling(now)) return die == 0 ? First : die == 1 ? Second : Event ?? 0;
        int step = (int)((now - started) * 20) + die * 3;
        // 只生成过渡画面，不接触任何随机数生成器或最终骰点。
        return die < 2 ? step % 6 + 1 : step % 6 < 3 ? 0 : step % 6 - 2;
    }
    public static bool IsRoll(GameHistoryEntry entry)=>entry!=null&&(entry.Kind=="RollDice"||entry.Dice1>0&&entry.Dice2>0);
    public GameHistoryEntry[] KnownHistory(GameHistoryEntry[] entries)
    {
        entries=entries??Array.Empty<GameHistoryEntry>();
        // 联机历史可以早于对应 PlayerView 到达；先保留水位，等公开骰子实际开始展示。
        long future=entries.Where(e=>IsRoll(e)&&e.Sequence>Sequence).Select(e=>e.Sequence).DefaultIfEmpty(long.MaxValue).Min();
        return future==long.MaxValue?entries:entries.Where(e=>e!=null&&e.Sequence<future).ToArray();
    }
    public GameHistoryEntry[] VisibleHistory(GameHistoryEntry[] entries,double now)
    {
        var known=KnownHistory(entries);
        // 资源、商品及发展卡的公开收获叙述一同等骰子停止；权威历史始终原样保留。
        return IsRolling(now)?known.Where(e=>e!=null&&e.Sequence<Sequence).ToArray():known;
    }
}
