using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Catan.Core.TradeModules
{
    /// <summary>TB25.pdf 第9—10页，F0 D03/D04。共用池、支付与抽取算法，随机只由宿主注入。</summary>
    public static class FishingRules
    {
        public const string OldBootId = "BOOT";
        public const int HoldingLimit = 7;
        public static int[] LakeNumbers => new[] { 2, 3, 11, 12 };
        internal static string[] AllTokens => Enumerable.Range(1, 29).Select(i => "F" + i.ToString("00")).Concat(new[] { OldBootId }).ToArray();
        public static int Value(string id)
        {
            if (id == null || id.Length != 3 || id[0] != 'F' || !int.TryParse(id.Substring(1), out int n) || n < 1 || n > 29 || id != "F" + n.ToString("00")) return 0;
            return n <= 11 ? 1 : n <= 21 ? 2 : 3;
        }
        public static int Cost(FishAction action)
        {
            switch (action)
            {
                case FishAction.RemoveRobber: return 2;
                case FishAction.StealResource: return 3;
                case FishAction.TakeResource: return 4;
                case FishAction.BuildRoad: return 5;
                case FishAction.BuyDevelopmentCard: return 7;
                default: return int.MaxValue;
            }
        }
        internal static void InitializePool(FishingState s, Action<string[]> shuffle)
        {
            s.Supply = AllTokens; shuffle(s.Supply);
        }
        public static bool CanPay(FishingState s, string player, string[] tokens, FishAction action)
        {
            var holding = s?.Holdings.FirstOrDefault(h => h.PlayerId == player);
            return holding != null && tokens != null && tokens.Length > 0 && tokens.Length <= HoldingLimit &&
                tokens.Distinct().Count() == tokens.Length && tokens.All(t => Value(t) > 0 && holding.TokenIds.Contains(t)) && tokens.Sum(Value) >= Cost(action);
        }
        internal static void Pay(FishingState s, string player, string[] tokens)
        {
            var holding = s.Holdings.Single(h => h.PlayerId == player);
            holding.TokenIds = holding.TokenIds.Except(tokens).ToArray();
            s.Discard = s.Discard.Concat(tokens).ToArray();
        }
        internal static void NewTurn(FishingState s)
        {
            if (s != null) foreach (var h in s.Holdings) h.ReplacementUsed = false;
        }
        internal static FishDrawClaim[] Claims(FishingState s, string[] clockwisePlayers, Tile[] tiles,
            IEnumerable<PiecePlacement> settlements, IEnumerable<PiecePlacement> cities, int number, string robberTileId)
        {
            var sources = s.Grounds.Where(g => g.Number == number).Select(g => (id:g.Id, vertices:g.VertexIds)).ToList();
            if (LakeNumbers.Contains(number) && s.LakeTileId != robberTileId)
                sources.Add((s.LakeTileId, tiles.Single(t => t.Id == s.LakeTileId).Vertices));
            var buildings = settlements.Select(b => (piece:b, count:1)).Concat(cities.Select(b => (piece:b, count:2))).ToArray();
            return clockwisePlayers.SelectMany(player => sources.OrderBy(source => source.id, StringComparer.Ordinal).SelectMany(source =>
                buildings.Where(b => b.piece.PlayerId == player && source.vertices.Contains(b.piece.LocationId)).OrderBy(b => b.piece.LocationId, StringComparer.Ordinal)
                .Select(b => new FishDrawClaim { PlayerId = player, SourceId = source.id, VertexId = b.piece.LocationId, Count = b.count }))).ToArray();
        }
        internal static void StartDraw(FishingState s, FishDrawClaim[] claims, Action<string[]> shuffle)
        {
            if (s.Queue.Length != 0 || s.PendingPlayerId != null) throw new InvalidOperationException("上次捕鱼结算尚未结束。");
            s.Queue = claims; s.QueueIndex = 0; s.Remaining = claims.Length == 0 ? 0 : claims[0].Count;
            Drain(s, shuffle);
        }
        private static bool DrawOne(FishingState s, FishHolding h, Action<string[]> shuffle)
        {
            if (s.Supply.Length == 0 && s.Discard.Length > 0)
            {
                s.Supply = s.Discard; s.Discard = Array.Empty<string>(); shuffle(s.Supply);
            }
            if (s.Supply.Length == 0) return false;
            var token = s.Supply[0]; s.Supply = s.Supply.Skip(1).ToArray();
            if (token == OldBootId) s.OldBootPlayerId = h.PlayerId;
            else h.TokenIds = h.TokenIds.Concat(new[] { token }).ToArray();
            return true;
        }
        private static void Advance(FishingState s)
        {
            s.QueueIndex++; s.Remaining = s.QueueIndex < s.Queue.Length ? s.Queue[s.QueueIndex].Count : 0;
        }
        private static void SkipPlayer(FishingState s, string player)
        {
            while (s.QueueIndex < s.Queue.Length && s.Queue[s.QueueIndex].PlayerId == player) Advance(s);
        }
        private static void Drain(FishingState s, Action<string[]> shuffle)
        {
            while (s.QueueIndex < s.Queue.Length)
            {
                var h = s.Holdings.Single(x => x.PlayerId == s.Queue[s.QueueIndex].PlayerId);
                // 满额换抽可以把弃掉的筹码直接洗回，即使原供应及弃置均空也不能跳过询问。
                if (h.TokenIds.Length == HoldingLimit)
                {
                    if (h.ReplacementUsed) { SkipPlayer(s, h.PlayerId); continue; }
                    s.PendingPlayerId = h.PlayerId;
                    s.PendingDecisionId = "fish-" + (++s.DecisionSequence);
                    return;
                }
                if (!DrawOne(s, h, shuffle)) break;
                if (--s.Remaining == 0) Advance(s);
            }
            s.Queue = Array.Empty<FishDrawClaim>(); s.QueueIndex = 0; s.Remaining = 0;
            s.PendingPlayerId = null; s.PendingDecisionId = null;
        }
        internal static void ResolveReplacement(FishingState s, string discardedToken, Action<string[]> shuffle)
        {
            var h = s.Holdings.Single(x => x.PlayerId == s.PendingPlayerId);
            if (discardedToken != null)
            {
                Pay(s, h.PlayerId, new[] { discardedToken });
                if (DrawOne(s, h, shuffle)) h.ReplacementUsed = true;
            }
            SkipPlayer(s, h.PlayerId); s.PendingPlayerId = null; s.PendingDecisionId = null;
            Drain(s, shuffle);
        }
        public static int VictoryTarget(FishingState s, string player, int baseTarget) => baseTarget + (s?.OldBootPlayerId == player ? 1 : 0);
        public static FishingView View(FishingState s, string player, int baseTarget, long sequence) => s == null ? null : new FishingView
        {
            Version = s.Version, MapVersion = s.MapVersion, LakeTileId = s.LakeTileId, LakeNumbers = LakeNumbers, Grounds = s.Grounds.Select(g => g.Copy()).ToArray(),
            OwnTokens = s.Holdings.Where(h => h.PlayerId == player).SelectMany(h => h.TokenIds).Select(t => new FishTokenView { Id = t, Value = Value(t) }).ToArray(),
            Players = s.Holdings.Select(h => new FishPlayerView { PlayerId = h.PlayerId, TokenCount = h.TokenIds.Length, ReplacementUsed = h.ReplacementUsed }).ToArray(),
            SupplyCount = s.Supply.Length, DiscardValues = s.Discard.Select(Value).ToArray(), OldBootPlayerId = s.OldBootPlayerId,
            PendingPlayerId = s.PendingPlayerId, PendingDecisionId = s.PendingDecisionId, StateSequence = sequence, OwnVictoryTarget = VictoryTarget(s, player, baseTarget)
        };
        internal static void Validate(FishingState s, string[] players, Tile[] tiles, Edge[] edges)
        {
            if (s == null) return;
            void Check(bool ok, string message) { if (!ok) throw new InvalidDataException(message); }
            Check(s.Version == FishingState.CurrentVersion, "捕鱼规则版本不兼容。");
            FishingScenarioCatalog.Validate(s, tiles, edges);
            Check(s.Holdings != null && s.Holdings.Length == players.Length && s.Holdings.All(h => h != null) && s.Holdings.Select(h => h.PlayerId).SequenceEqual(players), "捕鱼席位不一致。");
            Check(s.Holdings.All(h => h.TokenIds != null && h.TokenIds.Length <= HoldingLimit && h.TokenIds.All(t => Value(t) > 0)), "捕鱼持有上限或筹码类型无效。");
            Check(s.Supply != null && s.Discard != null && s.Discard.All(t => Value(t) > 0), "捕鱼池或弃置无效。");
            Check(s.OldBootPlayerId == null || players.Contains(s.OldBootPlayerId), "旧靴归属无效。");
            var all = s.Supply.Concat(s.Discard).Concat(s.Holdings.SelectMany(h => h.TokenIds)).Concat(s.OldBootPlayerId == null ? Array.Empty<string>() : new[] { OldBootId }).ToArray();
            Check(all.Length == 30 && all.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(AllTokens.OrderBy(x => x, StringComparer.Ordinal)), "捕鱼筹码不守恒或重复归属。");
            Check(s.Queue != null && s.Queue.Length <= 252 && s.DecisionSequence >= 0, "捕鱼队列无效。");
            if (s.Queue.Length == 0)
                Check(s.QueueIndex == 0 && s.Remaining == 0 && s.PendingPlayerId == null && s.PendingDecisionId == null, "空队列残留捕鱼待决。");
            else
            {
                Check(s.Queue.All(q => q != null && players.Contains(q.PlayerId) && q.Count >= 1 && q.Count <= 2 &&
                    (q.SourceId == s.LakeTileId ? tiles.Single(t => t.Id == s.LakeTileId).Vertices.Contains(q.VertexId) : s.Grounds.Any(g => g.Id == q.SourceId && g.VertexIds.Contains(q.VertexId)))), "捕鱼产出源无效。");
                Check(s.QueueIndex >= 0 && s.QueueIndex < s.Queue.Length && s.Remaining >= 1 && s.Remaining <= s.Queue[s.QueueIndex].Count, "捕鱼游标无效。");
                Check(s.PendingPlayerId == s.Queue[s.QueueIndex].PlayerId && s.DecisionSequence > 0 && s.PendingDecisionId == "fish-" + s.DecisionSequence &&
                    s.Holdings.Any(h => h.PlayerId == s.PendingPlayerId && h.TokenIds.Length == HoldingLimit && !h.ReplacementUsed), "捕鱼换抽待决无效。");
                var groups = s.Queue.Select(q => q.PlayerId).Where((p, i) => i == 0 || p != s.Queue[i - 1].PlayerId).ToArray();
                Check(groups.Distinct().Count() == groups.Length, "捕鱼座次队列重复回访玩家。");
            }
        }
    }
}
