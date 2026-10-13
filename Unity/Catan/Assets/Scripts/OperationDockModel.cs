#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;

// Presentation data only. Adapters populate this from the current player's detached view.
public sealed class OperationSnapshot
{
    public string Scope, Revision, Status, Phase, Seat;
    public bool CanTrade, CanInteract;
    public int[] Goods = new int[8], Bank = new int[8], Rates = new int[8];
    public int GoodCount = 5;
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
public sealed class OperationBuild
{
    public string Id, Name, Detail, Icon;
    public int Count, Track = -1, Level;
    public int[] Cost = new int[8];
    public bool Available;
    public string UnavailableReason;
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
public sealed class OperationSelection
{
    public readonly int[] Selected = new int[8];
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
        goods = snapshot == null ? null : (int[])snapshot.Goods.Clone();
        for (int i = 0; i < 8; i++) Selected[i] = Math.Min(Selected[i], snapshot == null ? 0 : snapshot.Goods[i]);
    }
    public void Clear() { Array.Clear(Selected, 0, 8); BuildId = null; PieceOrdinal = -1; }
    public void Choose(OperationBuild build, int[] owned, int pieceOrdinal = -1)
    {
        if (pieceOrdinal < -1 || pieceOrdinal >= build.Count && pieceOrdinal >= 0) return;
        Clear(); BuildId = build.Id; PieceOrdinal = pieceOrdinal;
        for (int i = 0; i < 8; i++) Selected[i] = Math.Min(build.Cost[i], owned[i]);
    }
    public bool IsPieceSelected(string buildId, int ordinal) => BuildId == buildId && PieceOrdinal >= 0 && PieceOrdinal == ordinal;
    public void Toggle(int good, int ordinal, int owned)
    {
        if (good < 0 || good >= 8 || ordinal < 0 || owned < 0 || ordinal >= owned) return;
        BuildId = null; PieceOrdinal = -1;
        Selected[good] = ordinal < Selected[good] ? Selected[good] - 1 : Math.Min(Selected[good] + 1, owned);
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
        if (trade.Give.Sum() == 0 || trade.Receive.Sum() == 0) return "请在两侧选择资源";
        for (int i = 0; i < 8; i++)
        {
            if (i >= snapshot.GoodCount && (trade.Give[i] != 0 || trade.Receive[i] != 0)) return "当前规则不使用商品";
            if (trade.Give[i] > snapshot.Goods[i]) return "付出的资源超过手牌";
            if (trade.Give[i] > 0 && trade.Receive[i] > 0) return "同一种资源不能同时付出和获得";
        }
        if (!trade.Bank) return trade.OtherPlayer == OperationTrade.AllPlayers && snapshot.OtherPlayers.Length > 0 || snapshot.OtherPlayers.Contains(trade.OtherPlayer) ? null : "请选择交易玩家";
        if (trade.Give.Count(n => n > 0) != 1 || trade.Receive.Count(n => n > 0) != 1) return "银行每次交换一种资源";
        int give = Array.FindIndex(trade.Give, n => n > 0), receive = Array.FindIndex(trade.Receive, n => n > 0);
        if (snapshot.Rates[give] < 2 || trade.Give[give] != snapshot.Rates[give] * trade.Receive[receive]) return "数量不符合当前港口比例";
        return trade.Receive[receive] > snapshot.Bank[receive] ? "银行库存不足" : null;
    }
}
