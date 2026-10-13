#nullable disable
using System;
using System.Linq;

// 纯显示数据与时间轴；不读取权威状态，不生成或裁决骰点。
public static class TabletopHandLayout
{
    public const float CardWidth = 108, CardHeight = 151, Baseline = 714;
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
    public static TabletopCardPose Resource(TabletopResourceSlot[] slots,int index,float center,float width,bool selected)
    {
        // 飞牌落下前才逐渐撑开插槽，已有手牌随之让位；同一插槽同时供飞牌和手牌绘制使用。
        double count=slots.Sum(slot=>slot.Weight),before=slots.Take(index).Sum(slot=>slot.Weight);
        double step=count<=1?0:Math.Min(CardWidth+10,Math.Max(0,width-CardWidth)/(count-1));
        return new TabletopCardPose {
            X=(float)(center-CardWidth/2+(before+slots[index].Weight/2-count/2)*step),
            Y=Baseline-(selected?22:0),Angle=0
        };
    }
}
public sealed class TabletopResourceSlot
{
    public int Good,Ordinal;
    public double Weight=1;
    public TabletopCollectionFlight Flight;
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

public sealed class TabletopCollectionFlight
{
    public string Player;
    public int Good;
    public double Started;
    public const double Duration = 1.05;
    public double Progress(double now) => Math.Max(0,Math.Min(1,(now-Started)/Duration));
    public double Insertion(double now)
    {
        double t=Math.Max(0,Math.Min(1,(Progress(now)-.60)/.40));
        return t*t*(3-2*t);
    }
}

// 只从已授权历史中识别银行净流出的资源；玩家之间转移、报价和私密变化均不当作银行收取。
public sealed class TabletopCollectionTimeline
{
    readonly System.Collections.Generic.List<TabletopCollectionFlight> flights = new System.Collections.Generic.List<TabletopCollectionFlight>();
    string scope;
    long sequence;
    bool initialized;
    public void Observe(string playerScope,Catan.Core.GameHistoryEntry[] entries,double now)
    {
        entries=entries??Array.Empty<Catan.Core.GameHistoryEntry>();
        long latest=entries.Length==0?0:entries.Max(e=>e.Sequence);
        if(!initialized||scope!=playerScope||latest<sequence)
        {
            // 首次绑定、换局和读档均只接收当前历史基线，不能重播旧的收牌。
            flights.Clear();initialized=true;scope=playerScope;sequence=latest;return;
        }
        foreach(var entry in entries.Where(e=>e.Sequence>sequence).OrderBy(e=>e.Sequence))
        {
            var changes=(entry.Changes??Array.Empty<Catan.Core.GameHistoryChange>()).Where(c=>c.Category=="goods"&&!c.IsPrivate&&c.GoodIndex>=0&&c.GoodIndex<8).ToArray();
            int ordinal=0;
            foreach(var group in changes.GroupBy(c=>c.GoodIndex))
            {
                int fromBank=group.Sum(c=>c.Amount);
                if(fromBank<=0)continue;
                foreach(var gain in group.Where(c=>c.Amount>0))
                {
                    int amount=Math.Min(fromBank,gain.Amount);fromBank-=amount;
                    for(int i=0;i<amount;i++)flights.Add(new TabletopCollectionFlight {
                        Player=gain.PlayerId,Good=gain.GoodIndex,Started=now+(entry.Dice1>0?TabletopDicePresentation.Duration:0)+Math.Min(.55,ordinal++*.06)
                    });
                }
            }
        }
        sequence=latest;flights.RemoveAll(f=>now-f.Started>=TabletopCollectionFlight.Duration);
    }
    public TabletopCollectionFlight[] Active(double now)
    {
        flights.RemoveAll(f=>now-f.Started>=TabletopCollectionFlight.Duration);
        return flights.Where(f=>now>=f.Started).ToArray();
    }
    public TabletopResourceSlot[] HandSlots(string player,int[] owned,int goodCount,double now)
    {
        flights.RemoveAll(f=>now-f.Started>=TabletopCollectionFlight.Duration);
        var result=new System.Collections.Generic.List<TabletopResourceSlot>();
        for(int good=0;good<goodCount;good++)
        {
            int count=Math.Max(0,owned[good]);
            var incoming=flights.Where(f=>f.Player==player&&f.Good==good).OrderBy(f=>f.Started).ToArray();
            // 已到账资源可能立即被花费；取消超出当前持有数的展示，绝不能凭动画补回规则资源。
            foreach(var canceled in incoming.Skip(count))flights.Remove(canceled);
            incoming=incoming.Take(count).ToArray();
            int visible=count-incoming.Length;
            for(int ordinal=0;ordinal<count;ordinal++)
            {
                var flight=ordinal<visible?null:incoming[ordinal-visible];
                result.Add(new TabletopResourceSlot{Good=good,Ordinal=ordinal,Flight=flight,Weight=flight==null?1:flight.Insertion(now)});
            }
        }
        return result.ToArray();
    }
}
