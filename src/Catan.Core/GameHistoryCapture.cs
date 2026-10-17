using System;
using System.Linq;

namespace Catan.Core
{
    internal static class GameHistoryCapture
    {
        private static int[] Goods(ResourceBag bag, int cloth = 0, int coin = 0, int paper = 0) => new[] { bag?.Wood ?? 0, bag?.Brick ?? 0, bag?.Wool ?? 0, bag?.Wheat ?? 0, bag?.Ore ?? 0, cloth, coin, paper };
        private static HistoryPiece[] Pieces(PiecePlacement[] pieces, string name) => pieces.Select(p => new HistoryPiece { PlayerId = p.PlayerId, LocationId = p.LocationId, Name = name }).ToArray();
        internal static HistorySnapshot From(M2.GameState s)
        {
            return new HistorySnapshot
            {
                Turn = s.Turn, Sequence = s.EventSequence, Dice1 = s.LastDice1, Dice2 = s.LastDice2,
                PendingKind = s.PendingDecision?.Kind, OtherPlayerId = s.TradeOffer?.ProposerId,
                OfferGive = Goods(s.TradeOffer?.Give), OfferReceive = Goods(s.TradeOffer?.Receive),
                Players = s.Players.Select(p =>
                {
                    var goods = Goods(p.Resources);
                    return new HistoryPlayer { Id = p.Id, Goods = goods, Development = p.DevelopmentCards.Select(c => c.Kind.ToString()).ToArray() };
                }).ToArray(),
                Pieces = Pieces(s.Settlements, "村庄").Concat(Pieces(s.Cities, "城市")).Concat(Pieces(s.Roads, "道路"))
                    .ToArray()
            };
        }
        internal static HistorySnapshot From(M3.GameState s)
        {
            return new HistorySnapshot
            {
                Turn = s.Turn, Sequence = s.EventSequence, Dice1 = s.LastDice1, Dice2 = s.LastDice2,
                PendingKind = s.PendingDecision?.Kind, OtherPlayerId = s.TradeOffer?.ProposerId,
                OfferGive = Goods(s.TradeOffer?.Give), OfferReceive = Goods(s.TradeOffer?.Receive),
                Players = s.Players.Select(p =>
                {
                    var goods = Goods(p.Resources);
                    return new HistoryPlayer { Id = p.Id, Goods = goods, ClothTokens = p.Cloth, WonderLevel = p.WonderLevel, Development = p.DevelopmentCards.Select(c => c.Kind.ToString()).ToArray() };
                }).ToArray(),
                Pieces = Pieces(s.Settlements, "村庄").Concat(Pieces(s.Cities, "城市")).Concat(Pieces(s.Roads, "道路"))
                    .Concat(s.Ships.Select(p => new HistoryPiece { PlayerId = p.PlayerId, LocationId = p.LocationId, Name = "船" }))
                    .ToArray()
            };
        }
        internal static HistorySnapshot From(M4.GameState s)
        {
            return new HistorySnapshot
            {
                Turn = s.Turn, Sequence = s.EventSequence, Dice1 = s.LastDice1, Dice2 = s.LastDice2,
                PendingKind = s.PendingDecision?.Kind, OtherPlayerId = s.PendingDecision?.OtherPlayerId ?? s.TradeOffer?.ProposerId,
                OfferGive = Goods(s.TradeOffer?.Give, s.TradeOffer?.GiveCommodities.Cloth ?? 0, s.TradeOffer?.GiveCommodities.Coin ?? 0, s.TradeOffer?.GiveCommodities.Paper ?? 0),
                OfferReceive = Goods(s.TradeOffer?.Receive, s.TradeOffer?.ReceiveCommodities.Cloth ?? 0, s.TradeOffer?.ReceiveCommodities.Coin ?? 0, s.TradeOffer?.ReceiveCommodities.Paper ?? 0),
                EventDie = s.LastEventDie.ToString(),
                Players = s.Players.Select(p =>
                {
                    var goods = Goods(p.Resources);
                    goods[5] = p.Commodities.Cloth; goods[6] = p.Commodities.Coin; goods[7] = p.Commodities.Paper;
                    return new HistoryPlayer { Id = p.Id, Goods = goods, Improvements = p.Improvements.ToArray(), Development = p.DevelopmentCards.Select(c => c.Kind.ToString()).ToArray(), Progress = p.ProgressCards.Select(c => c.ToString()).ToArray(), Victory = p.ProgressVictoryCards.Select(c => c.ToString()).ToArray() };
                }).ToArray(),
                Pieces = Pieces(s.Settlements, "村庄").Concat(Pieces(s.Cities, "城市")).Concat(Pieces(s.Roads, "道路"))
                    .Concat(Pieces(s.Walls, "城墙")).Concat(s.Knights.Select(p => new HistoryPiece { PlayerId = p.PlayerId, LocationId = p.LocationId, Name = p.Level + "级骑士（" + (p.Active ? "激活" : "休眠") + "）" }))
                    .Concat(s.Metropolises.Select(p => new HistoryPiece { PlayerId = p.PlayerId, LocationId = p.LocationId, Name = new[] { "贸易", "政治", "科学" }[(int)p.Track] + "大都会" }))
                    .ToArray()
            };
        }
        internal static HistorySnapshot From(M5.GameState s)
        {
            return new HistorySnapshot
            {
                Turn = s.Turn, Sequence = s.EventSequence, Dice1 = s.LastDice1, Dice2 = s.LastDice2,
                PendingKind = s.PendingDecision?.Kind, OtherPlayerId = s.PendingDecision?.OtherPlayerId ?? s.TradeOffer?.ProposerId,
                OfferGive = Goods(s.TradeOffer?.Give, s.TradeOffer?.GiveCommodities.Cloth ?? 0, s.TradeOffer?.GiveCommodities.Coin ?? 0, s.TradeOffer?.GiveCommodities.Paper ?? 0),
                OfferReceive = Goods(s.TradeOffer?.Receive, s.TradeOffer?.ReceiveCommodities.Cloth ?? 0, s.TradeOffer?.ReceiveCommodities.Coin ?? 0, s.TradeOffer?.ReceiveCommodities.Paper ?? 0),
                EventDie = s.LastEventDie.ToString(),
                Players = s.Players.Select(p =>
                {
                    var goods = Goods(p.Resources);
                    goods[5] = p.Commodities.Cloth; goods[6] = p.Commodities.Coin; goods[7] = p.Commodities.Paper;
                    return new HistoryPlayer { Id = p.Id, Goods = goods, Improvements = p.Improvements.ToArray(), Development = p.DevelopmentCards.Select(c => c.Kind.ToString()).ToArray(), Progress = p.ProgressCards.Select(c => c.ToString()).ToArray(), Victory = p.ProgressVictoryCards.Select(c => c.ToString()).ToArray() };
                }).ToArray(),
                Pieces = Pieces(s.Settlements, "村庄").Concat(Pieces(s.Cities, "城市")).Concat(Pieces(s.Roads, "道路"))
                    .Concat(s.Ships.Select(p => new HistoryPiece { PlayerId = p.PlayerId, LocationId = p.LocationId, Name = "船" }))
                    .Concat(Pieces(s.Walls, "城墙")).Concat(s.Knights.Select(p => new HistoryPiece { PlayerId = p.PlayerId, LocationId = p.LocationId, Name = p.Level + "级骑士（" + (p.Active ? "激活" : "休眠") + "）" }))
                    .Concat(s.Metropolises.Select(p => new HistoryPiece { PlayerId = p.PlayerId, LocationId = p.LocationId, Name = new[] { "贸易", "政治", "科学" }[(int)p.Track] + "大都会" }))
                    .ToArray()
            };
        }
    }
}
