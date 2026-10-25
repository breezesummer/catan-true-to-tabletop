#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;

// Presentation data only. Adapters populate this from the current player's detached view.
public sealed class OperationSnapshot
{
    public string Scope, Revision, Status, Phase, Seat;
    public long PublicSequence=long.MaxValue;
    Catan.Core.GameHistoryEntry[] historyInput,historyOutput;
    long historyWatermark=-1;
    public Catan.Core.GameHistoryEntry[] PresentedHistory(Catan.Core.GameHistoryEntry[] entries)
    {
        entries=entries??Array.Empty<Catan.Core.GameHistoryEntry>();
        if(ReferenceEquals(historyInput,entries)&&historyWatermark==PublicSequence)return historyOutput;
        historyInput=entries;historyWatermark=PublicSequence;
        return historyOutput=entries.Length==0||entries.All(e=>e.Sequence<=PublicSequence)?entries:entries.Where(e=>e.Sequence<=PublicSequence).ToArray();
    }
    public bool CanTrade, CanInteract, RequiresSelection;
    public int[] Goods = new int[8], Bank = new int[8], Rates = new int[8];
    public int GoodCount = 5;
    public TabletopCollectionTile[] CollectionTiles = Array.Empty<TabletopCollectionTile>();
    public Func<string,TabletopCardPose> CollectionPosition;
    public int ResourceHandLimit = 7, SpecialHandLimit;
    public OperationDiscard Discard;
    public string[] OtherPlayers = Array.Empty<string>();
    public readonly List<OperationBuild> Builds = new List<OperationBuild>();
    public readonly List<OperationHandCard> Cards = new List<OperationHandCard>();
    public OperationOffer Offer;
    public Action<OperationBuild> PrepareBuild;
    public Action<OperationHandCard> PrepareCard;
    public Action Cancel;
    public Action<string> Notify;
    public Func<OperationTrade, string> Trade;
    public Action<string> Respond;
}
// 只描述当前观看者自己的强制交牌；最终合法性仍由规则入口验证。
public sealed class OperationDiscard
{
    public int Amount;
    public bool Special;
    public string Prompt = "弃牌";
    public Action<int[], OperationHandCard> Confirm;
    public bool Ready(int[] selected, int[] owned, OperationHandCard card)
    {
        if (Amount <= 0) return false;
        if (Special) return card != null && Amount == 1;
        return selected != null && owned != null && selected.Length == owned.Length &&
            selected.All(n => n >= 0) && selected.Sum() == Amount &&
            Enumerable.Range(0, selected.Length).All(i => selected[i] <= owned[i]);
    }
    public string Caption(bool ready) => ready ? "确认" : "选择弃牌";
}
public sealed class OperationBuild
{
    public string Id, Name, Detail, Icon;
    public int Count, Track = -1, Level;
    public int[] Cost = new int[8];
    public bool Available;
    public string UnavailableReason;
    // 缓存只保存当前规则视图的条件；菜单和展示动画的临时阻塞每帧单独判断。
    public void ConfigureAvailability(bool legal, int[] owned, bool requiresSupply = true)
    {
        bool enough = Enumerable.Range(0, 8).All(i => owned[i] >= Cost[i]);
        bool stocked = !requiresSupply || Count > 0;
        Available = stocked && legal && enough;
        UnavailableReason = !stocked ? "库存已用尽" : !enough ? "缺少所需资源" : !legal ? "当前阶段或连接条件没有合法目标" : null;
    }
    public bool CanPrepare(OperationSnapshot snapshot) => Available && snapshot != null && snapshot.CanInteract;
}
public sealed class OperationHandCard
{
    public string Id, Name, Detail, UnavailableReason;
    public int ArtIndex = -1;
    public bool Progress, Playable;
}
public sealed class OperationOffer
{
    public string Proposer, Recipient;
    public bool IsBroadcast;
    public string[] PendingRecipients = Array.Empty<string>(), RejectedRecipients = Array.Empty<string>();
    public int[] Give = new int[8], Receive = new int[8];
    public bool CanAccept, CanReject, CanCancel;
}
public sealed class OperationTrade
{
    public const string AllPlayers = "*";
    public bool Bank;
    public string OtherPlayer;
    public int[] Give = new int[8], Receive = new int[8];
}
// 只从所属玩家的公开授权视图中挑选面板内容，不读取其他玩家手牌。
public static class OperationPanelOptions
{
    public static OperationBuild Facility(OperationSnapshot snapshot, int track)
    {
        return snapshot == null || track < 0 || track > 2 ? null : snapshot.Builds.FirstOrDefault(build => build.Track == track);
    }
    public static int[] PaymentGoods(OperationSnapshot snapshot, int? portGood = null)
    {
        if (snapshot == null || snapshot.Goods == null) return Array.Empty<int>();
        int count = Math.Max(0, Math.Min(8, Math.Min(snapshot.GoodCount, snapshot.Goods.Length)));
        if (portGood.HasValue && (portGood.Value < 0 || portGood.Value >= 5)) return Array.Empty<int>();
        return Enumerable.Range(0, count).Where(good => snapshot.Goods[good] > 0 && (!portGood.HasValue || good == portGood.Value)).ToArray();
    }
    public static int PaymentChoice(int[] visibleGoods, int previous)
    {
        return visibleGoods == null || visibleGoods.Length == 0 ? -1 : visibleGoods.Contains(previous) ? previous : visibleGoods[0];
    }
}
public sealed class OperationSelection
{
    public readonly int[] Selected = new int[8];
    readonly HashSet<int>[] selectedOrdinals = Enumerable.Range(0, 8).Select(_ => new HashSet<int>()).ToArray();
    public string BuildId;
    public int PieceOrdinal = -1;
    string scope, revision, seat;
    int[] goods;
    public void Sync(OperationSnapshot snapshot)
    {
        if (snapshot == null || snapshot.Scope != scope || snapshot.Revision != revision || snapshot.Seat != seat || goods == null || !goods.SequenceEqual(snapshot.Goods)) Clear();
        scope = snapshot == null ? null : snapshot.Scope;
        revision = snapshot == null ? null : snapshot.Revision;
        seat = snapshot == null ? null : snapshot.Seat;
        if(snapshot==null)goods=null;
        else if(goods==null)goods=(int[])snapshot.Goods.Clone();
        else Array.Copy(snapshot.Goods,goods,goods.Length);
        for (int i = 0; i < 8; i++) Selected[i] = Math.Min(Selected[i], snapshot == null ? 0 : snapshot.Goods[i]);
    }
    public void Clear()
    {
        Array.Clear(Selected, 0, 8);
        foreach (var ordinals in selectedOrdinals) ordinals.Clear();
        BuildId = null; PieceOrdinal = -1;
    }
    public void Choose(OperationBuild build, int[] owned, int pieceOrdinal = -1)
    {
        if (pieceOrdinal < -1 || pieceOrdinal >= build.Count && pieceOrdinal >= 0) return;
        Clear(); BuildId = build.Id; PieceOrdinal = pieceOrdinal;
        for (int i = 0; i < 8; i++)
        {
            Selected[i] = Math.Min(build.Cost[i], owned[i]);
            for (int ordinal = 0; ordinal < Selected[i]; ordinal++) selectedOrdinals[i].Add(ordinal);
        }
    }
    public bool IsPieceSelected(string buildId, int ordinal) => BuildId == buildId && PieceOrdinal >= 0 && PieceOrdinal == ordinal;
    public bool IsCardSelected(int good, int ordinal) => good >= 0 && good < 8 && selectedOrdinals[good].Contains(ordinal);
    public void Toggle(int good, int ordinal, int owned)
    {
        if (good < 0 || good >= 8 || ordinal < 0 || owned < 0 || ordinal >= owned) return;
        BuildId = null; PieceOrdinal = -1;
        if (!selectedOrdinals[good].Remove(ordinal)) selectedOrdinals[good].Add(ordinal);
        Selected[good] = selectedOrdinals[good].Count;
    }
    public void SelectCard(int good, int ordinal, int owned)
    {
        if (good < 0 || good >= 8 || ordinal < 0 || ordinal >= owned) return;
        BuildId = null; PieceOrdinal = -1;
        selectedOrdinals[good].Add(ordinal);
        Selected[good] = selectedOrdinals[good].Count;
    }
    // 交易的数量按钮保留已点中的实体牌，只在数量增减时补选或放回。
    public void SetCount(int good, int count, int owned)
    {
        if (good < 0 || good >= 8 || owned < 0) return;
        count = Math.Max(0, Math.Min(count, owned));
        var ordinals = selectedOrdinals[good];
        foreach (int ordinal in ordinals.Where(n => n >= owned).ToArray()) ordinals.Remove(ordinal);
        foreach (int ordinal in ordinals.OrderByDescending(n => n).Take(Math.Max(0, ordinals.Count - count)).ToArray()) ordinals.Remove(ordinal);
        for (int ordinal = 0; ordinals.Count < count && ordinal < owned; ordinal++) ordinals.Add(ordinal);
        Selected[good] = ordinals.Count;
    }
    public bool Matches(OperationBuild build)
    {
        return Selected.Sum() > 0 && build.Cost.Sum() > 0 && Enumerable.Range(0, 8).All(i => Selected[i] >= build.Cost[i]);
    }
    public static string ValidateTrade(OperationSnapshot snapshot, OperationTrade trade)
    {
        if (snapshot == null || !snapshot.CanTrade || !snapshot.CanInteract || snapshot.Offer != null) return "当前阶段不能发起交易";
        if (trade == null || trade.Give == null || trade.Receive == null || trade.Give.Length != 8 || trade.Receive.Length != 8) return "交易内容无效";
        if (trade.Give.Any(n => n < 0) || trade.Receive.Any(n => n < 0)) return "数量不能为负数";
        for (int i = 0; i < 8; i++) if (trade.Give[i] > (i < 5 ? 19 : 12) || trade.Receive[i] > (i < 5 ? 19 : 12)) return "数量超过资源供应";
        if (!snapshot.Goods.Take(snapshot.GoodCount).Any(n => n > 0)) return "当前没有可付出的资源或商品";
        if (trade.Give.Sum() == 0 || trade.Receive.Sum() == 0) return "请在两侧选择资源";
        for (int i = 0; i < 8; i++)
        {
            if (i >= snapshot.GoodCount && (trade.Give[i] != 0 || trade.Receive[i] != 0)) return "当前规则不使用商品";
            if (trade.Give[i] > snapshot.Goods[i]) return "付出的资源超过手牌";
            if (trade.Give[i] > 0 && trade.Receive[i] > 0) return "同一种资源不能同时付出和获得";
        }
        if (!trade.Bank) return trade.OtherPlayer == OperationTrade.AllPlayers && snapshot.OtherPlayers.Length > 0 || snapshot.OtherPlayers.Contains(trade.OtherPlayer) ? null : "请选择交易玩家";
        if (trade.Give.Count(n => n > 0) != 1) return "请选择一种付出资源";
        int give = Array.FindIndex(trade.Give, n => n > 0), receive = Array.FindIndex(trade.Receive, n => n > 0);
        if (snapshot.Rates[give] < 2 || trade.Give[give] != snapshot.Rates[give] * trade.Receive.Sum()) return "数量不符合当前港口比例";
        return Enumerable.Range(0, snapshot.GoodCount).Any(i => trade.Receive[i] > snapshot.Bank[i]) ? "银行库存不足" : null;
    }
}

// 所有交易均以观看者为左侧；只判断观看者自己的库存，不推测他人的隐藏手牌。
public sealed class OperationOfferPerspective
{
    public bool OwnOffer, Involved;
    public string OtherSeat;
    public int[] OwnGive, OtherGive;
    public static OperationOfferPerspective For(OperationSnapshot s)
    {
        var o=s.Offer;bool own=o.Proposer==s.Seat;
        bool involved=own||o.Recipient==s.Seat||o.IsBroadcast&&s.OtherPlayers.Contains(o.Proposer);
        return new OperationOfferPerspective{OwnOffer=own,Involved=involved,OtherSeat=own?(o.IsBroadcast?"*":o.Recipient):o.Proposer,
            OwnGive=involved?(own?o.Give:o.Receive):new int[8],OtherGive=involved?(own?o.Receive:o.Give):o.Give};
    }
}
public static class OperationResourceChoice
{
    public static string Add(int good,int amount,Catan.Core.ResourceBag selected,Catan.Core.ResourceBag bank)
        =>Change(good,1,amount,selected,bank);
    public static string Change(int good,int delta,int amount,Catan.Core.ResourceBag selected,Catan.Core.ResourceBag bank)
    {
        if(good<0||good>=5)return "金矿只能领取资源，不能选择商品。";
        if(delta==-1){var r=(Catan.Core.Resource)good;selected[r]=Math.Max(0,selected[r]-1);return null;}
        if(delta!=1)return "无效的资源选择操作。";
        if(selected.Total>=Math.Min(amount,bank.Total))return "已达到可选数量，不允许继续选择。";
        var resource=(Catan.Core.Resource)good;
        if(selected[resource]>=bank[resource])return "银行库存不足，不允许选择。";
        selected[resource]++;return null;
    }
}

// 只管理显示草稿，最终交易与领取仍由原规则入口验证。
public static class OperationCardQuantity
{
    public static string Change(int[] selected,int good,int delta,int maximum,int totalLimit=int.MaxValue,bool singleKind=false)
    {
        if(selected==null||good<0||good>=selected.Length||delta!=1&&delta!=-1)return "无效的资源选择操作。";
        if(delta<0){selected[good]=Math.Max(0,selected[good]-1);return null;}
        if(selected[good]>=maximum)return "数量已达到上限，不允许继续选择。";
        int total=singleKind?selected[good]:selected.Sum();
        if(total>=totalLimit)return "已达到可选数量，不允许继续选择。";
        if(singleKind)for(int i=0;i<selected.Length;i++)if(i!=good)selected[i]=0;
        selected[good]++;return null;
    }
}

// 根据展示数量选择可容纳的最大卡面，短行与整组始终居中，计数保留独立空间。
public readonly struct OperationTradeCardSlot
{
    public readonly float X,Y,Width,Height;
    public const float CaptionHeight=22;
    public float CaptionY=>Y+Height;
    public OperationTradeCardSlot(float x,float y,float width,float height)
    {X=x;Y=y;Width=width;Height=height;}
}
public static class OperationTradeCardLayout
{
    public const float Aspect=108f/151f;
    public static OperationTradeCardSlot[] Arrange(int count,float width,float height)
    {
        if(count<=0||width<=0||height<=OperationTradeCardSlot.CaptionHeight)return Array.Empty<OperationTradeCardSlot>();
        const float horizontalGap=12,verticalGap=10,maxWidth=112;
        float bestWidth=0;int columns=1,bestEmpty=int.MaxValue;
        for(int candidate=1;candidate<=Math.Min(count,8);candidate++)
        {
            int rows=(count+candidate-1)/candidate;
            float availableHeight=(height-(rows-1)*verticalGap)/rows-OperationTradeCardSlot.CaptionHeight;
            float cardWidth=Math.Min(maxWidth,Math.Min((width-(candidate-1)*horizontalGap)/candidate,availableHeight*Aspect));
            int empty=candidate*rows-count;
            if(cardWidth>bestWidth+.01f||Math.Abs(cardWidth-bestWidth)<=.01f&&empty<bestEmpty)
            {bestWidth=cardWidth;columns=candidate;bestEmpty=empty;}
        }
        if(bestWidth<=0)return Array.Empty<OperationTradeCardSlot>();
        int rowCount=(count+columns-1)/columns;
        float cardHeight=bestWidth/Aspect,rowHeight=cardHeight+OperationTradeCardSlot.CaptionHeight;
        float top=(height-rowCount*rowHeight-(rowCount-1)*verticalGap)/2;
        var slots=new OperationTradeCardSlot[count];
        for(int index=0;index<count;index++)
        {
            int row=index/columns,inRow=Math.Min(columns,count-row*columns);
            float left=(width-inRow*bestWidth-(inRow-1)*horizontalGap)/2;
            slots[index]=new OperationTradeCardSlot(left+index%columns*(bestWidth+horizontalGap),top+row*(rowHeight+verticalGap),bestWidth,cardHeight);
        }
        return slots;
    }
}
