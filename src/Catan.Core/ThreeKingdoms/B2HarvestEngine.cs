using System;
using System.Collections.Generic;
using System.Linq;

namespace Catan.Core.ThreeKingdoms
{
    public static partial class B2SkillEngine
    {
        /// <summary>宿主先按真实短缺规则剔除禁摸并计算普通分配。本方法一次将全部分配移入存档预留区。</summary>
        public static void BeginHarvest(IThreeKingdomsCardHost host, B2HarvestAllocation[] allocations, bool pump = true)
        {
            if (!Enabled(host)) return; var s = host.State;
            Require(!s.Skills.HarvestActive && s.Skills.HarvestTurnId != s.Clock.TurnInstanceId, "本真实回合已建立摸牌主分配。");
            Require(allocations != null && allocations.All(a => a != null && s.SeatPlayerIds.Contains(a.PlayerId) && Valid(a.Cards) && a.GoldCount >= 0) &&
                allocations.Select(a => a.PlayerId).Distinct().Count() == allocations.Length, "主分配玩家或数量无效。");
            var complete = Order(s).Select(id =>
            {
                var input = allocations.FirstOrDefault(a => a.PlayerId == id); var general = Active(s, id); var blocked = IsDrawBlocked(s, id);
                return new B2HarvestAllocation { PlayerId = id, Cards = blocked || input == null ? new int[8] : Copy(input.Cards),
                    GoldCount = blocked ? 0 : input?.GoldCount ?? 0, AqueductEligible = !blocked && input?.AqueductEligible == true,
                    GeneralId = general?.GeneralId, TenureId = general?.TenureId };
            }).ToArray();
            var total = new int[8]; foreach (var a in complete) Add(total, a.Cards);
            Require(Contains(host.GetBank(), total), "普通产出预留超过有限银行供应。");
            host.RemoveBank(total); s.Skills.Harvest = complete; s.Skills.HarvestActive = true;
            s.Skills.HarvestTurnId = s.Clock.TurnInstanceId; s.Skills.HarvestStage = 0;
            OnCivilHarvestStart(host, "harvest-" + s.Clock.TurnInstanceId);
            OnB5MilitaryHarvestStart(host, "harvest-" + s.Clock.TurnInstanceId);
            if (pump) Pump(host);
        }

        private static void ContinueHarvest(IThreeKingdomsCardHost host)
        {
            var s = host.State;
            if (s.Skills.HarvestStage == 0)
            {
                var gold = s.Skills.Harvest.FirstOrDefault(a => a.GoldCount > 0);
                if (gold != null) { Append(s, HarvestFrame(s, gold, "gold")); return; }
                s.Skills.HarvestStage = 1;
                if (s.Skills.AfterGoldFrames.Length > 0)
                {
                    Prepend(s, s.Skills.AfterGoldFrames); s.Skills.AfterGoldFrames = Array.Empty<B2SkillFrame>(); return;
                }
            }
            if (s.Skills.HarvestStage == 1)
            {
                var next = s.Skills.Harvest.FirstOrDefault(a => !a.ChoiceComplete);
                if (next != null) { Append(s, HarvestFrame(s, next, "harvest")); return; }
                s.Skills.HarvestStage = 2;
            }
            if (s.Skills.HarvestStage == 2)
            {
                var next = s.Skills.Harvest.FirstOrDefault(a => !a.Completed);
                if (next != null) { Append(s, HarvestFrame(s, next, next.ReplacementKind == "raid" ? "raid" : "claim")); return; }
                s.Skills.HarvestStage = 3;
                foreach (var a in s.Skills.Harvest.Where(a => a.ActuallyReceived == 0 && a.AqueductEligible && host.FinalDiceTotal != 7 && !IsDrawBlocked(s, a.PlayerId)))
                    Append(s, new B2SkillFrame { Id = Id(s, "aqueduct"), SkillId = "tk-aqueduct", Kind = "resource", PlayerId = a.PlayerId, Remaining = 1 });
            }
            s.Skills.HarvestActive = false;
        }

        private static B2SkillFrame HarvestFrame(ThreeKingdomsState s, B2HarvestAllocation a, string kind) => new B2SkillFrame
        { Id = Id(s, "harvest"), Kind = kind, PlayerId = a.PlayerId, TenureId = a.TenureId, SkillId =
            a.GeneralId == "tk-g01" ? "tk-g01-s1" : a.GeneralId == "tk-g28" ? "tk-g28-s1" : a.GeneralId == "tk-g37" ? "tk-g37-s1" : "tk-harvest" };
        private static B2HarvestAllocation Allocation(ThreeKingdomsState s, string player) => s.Skills.Harvest.Single(a => a.PlayerId == player);

        private static bool ProcessHarvestFrame(IThreeKingdomsCardHost host, B2SkillFrame f)
        {
            var s = host.State; var a = Allocation(s, f.PlayerId);
            if (f.Kind == "gold")
            {
                if (a.GoldCount > 0 && host.GetBank().Take(5).Sum() > 0 && !IsDrawBlocked(s, f.PlayerId))
                { Decide(s, f, "gold", "金矿：选择资源", "为主分配预留金矿资源，尚余 " + a.GoldCount + " 张。"); return true; }
                a.GoldCount = 0; Remove(s, f); return false;
            }
            if (f.Kind == "harvest")
            {
                var active = Active(s, f.PlayerId);
                var available = active?.TenureId == a.TenureId && !IsDrawBlocked(s, f.PlayerId) &&
                    (a.GeneralId == "tk-g28" || a.GeneralId == "tk-g37" && TilePairs(host, f.PlayerId).Any() || a.GeneralId == "tk-g01" && a.Cards.Sum() > 0 && RaidTargets(host, f.PlayerId).Length > 0);
                if (available) { Decide(s, f, "harvest", Name(f.SkillId) + "：摸牌替代", "可以保留普通产出，或使用本将替代本次主分配。"); return true; }
                a.ChoiceComplete = true; Remove(s, f); return false;
            }
            if (f.Kind == "shelie")
            {
                if (f.SelectedCards.Sum() == 3 || !Enumerable.Range(0, 5).Any(t => f.Cards[t] > 0 && f.SelectedCards[t] == 0))
                { FinishShelie(host, f, a); return false; }
                Decide(s, f, "shelie", "涉猎：选择不同资源", "公开亮出的牌中最多选择三种资源，每种至多一张，也可直接完成。"); return true;
            }
            if (f.Kind == "tiandu")
            {
                if (f.Step >= f.Tiles.Length)
                { a.Cards = Copy(f.Cards); f.Cards = new int[8]; a.ChoiceComplete = true; Remove(s, f); return false; }
                var allowed = f.Tiles[f.Step].AllowedCardTypes.Where(t => host.GetBank()[t] > 0).ToArray();
                if (allowed.Length == 0 || IsDrawBlocked(s, f.PlayerId)) { f.Step++; return false; }
                Decide(s, f, "tiandu-card", "天妒：地块 " + f.Tiles[f.Step].TileId, "按已确认地块的生产资格选择一张可用牌。"); return true;
            }
            if (f.Kind == "claim")
            {
                Remove(s, f); a.Completed = true;
                if (IsDrawBlocked(s, f.PlayerId)) { host.AddBank(a.Cards); a.Cards = new int[8]; a.ActuallyReceived = 0; }
                else { a.ActuallyReceived = a.Cards.Sum(); Gain(host, f.PlayerId, a.Cards); a.Cards = new int[8]; }
                return false;
            }
            if (f.Kind == "raid")
            {
                if (f.Step == 0)
                {
                    f.Step = 1;
                    foreach (var target in a.Targets.Reverse()) Target(host, f.Id, f.PlayerId, target);
                    return false;
                }
                if (IsDrawBlocked(s, f.PlayerId))
                { host.AddBank(a.Cards); a.Cards = new int[8]; a.Completed = true; Remove(s, f); return false; }
                var valid = RaidTargets(host, f.PlayerId);
                if (a.Targets.Any(t => !valid.Contains(t)))
                { Decide(s, f, "raid-revalidate", "突袭：重选或减少目标", "目标状态已经变化。可重选有效目标，或取消替代领取原分配。"); return true; }
                CommitRaid(host, f, a); return false;
            }
            throw new InvalidOperationException("无法恢复未知技能步骤：" + f.Kind);
        }

        private static void AddHarvestActions(IThreeKingdomsCardHost host, B2SkillFrame f, B2SkillDecision d, List<B2SkillAction> result)
        {
            var s = host.State; var a = Allocation(s, f.PlayerId);
            if (d.Kind == "gold")
            { foreach (var t in Enumerable.Range(0, 5).Where(t => host.GetBank()[t] > 0)) result.Add(CardAction(s, f, d, t)); return; }
            if (d.Kind == "tiandu-card")
            { foreach (var t in f.Tiles[f.Step].AllowedCardTypes.Where(t => host.GetBank()[t] > 0)) result.Add(CardAction(s, f, d, t)); return; }
            if (d.Kind == "shelie")
            {
                foreach (var t in Enumerable.Range(0, 5).Where(t => f.Cards[t] > 0 && f.SelectedCards[t] == 0)) result.Add(CardAction(s, f, d, t));
                result.Add(Action(s, f.SkillId, "done", "完成涉猎（已选 " + f.SelectedCards.Sum() + " 张）", d)); return;
            }
            result.Add(Action(s, f.SkillId, "pass", "保留普通产出", d));
            if (a.GeneralId == "tk-g28" && d.Kind == "harvest") result.Add(Action(s, f.SkillId, "shelie", "发动涉猎：亮出最多四张资源", d));
            if (a.GeneralId == "tk-g37" && d.Kind == "harvest")
                foreach (var pair in TilePairs(host, f.PlayerId)) { var action = Action(s, f.SkillId, "tiles", "天妒：" + pair[0].TileId + " ＋ " + pair[1].TileId, d); action.Command.TileIds = pair.Select(t => t.TileId).ToArray(); result.Add(action); }
            if (a.GeneralId == "tk-g01")
            {
                var targets = RaidTargets(host, f.PlayerId);
                var maximum = d.Kind == "raid-revalidate" ? a.ForgoneCards.Sum() : a.Cards.Sum();
                foreach (var subset in Subsets(targets).Where(t => t.Length <= maximum))
                    foreach (var cost in CardSelections(a.Cards, subset.Length))
                    { var action = Action(s, f.SkillId, "raid", "突袭：少领 " + Describe(cost) + "；取自 " + string.Join("、", subset), d); action.Command.TargetPlayerIds = subset; action.Command.Counts = cost; result.Add(action); }
            }
        }

        private static void ResolveHarvest(IThreeKingdomsCardHost host, B2SkillFrame f, B2SkillDecision d, B2SkillCommand c)
        {
            var s = host.State; var a = Allocation(s, f.PlayerId);
            if (d.Kind == "gold")
            {
                Require(c.Option == "card" && c.CardType >= 0 && c.CardType < 5 && a.GoldCount > 0 && host.GetBank()[c.CardType] > 0 && !IsDrawBlocked(s, f.PlayerId), "金矿资源不可领取。");
                var cards = One(c.CardType); host.RemoveBank(cards); Add(a.Cards, cards); a.GoldCount--; return;
            }
            if (d.Kind == "tiandu-card")
            {
                Require(c.Option == "card" && f.Tiles[f.Step].AllowedCardTypes.Contains(c.CardType) && host.GetBank()[c.CardType] > 0 && !IsDrawBlocked(s, f.PlayerId), "不符合天妒地块生产资格或库存。");
                var cards = One(c.CardType); host.RemoveBank(cards); Add(f.Cards, cards); f.Step++; return;
            }
            if (d.Kind == "shelie")
            {
                if (c.Option == "done") { FinishShelie(host, f, a); return; }
                Require(c.Option == "card" && c.CardType >= 0 && c.CardType < 5 && f.Cards[c.CardType] > 0 && f.SelectedCards[c.CardType] == 0 && f.SelectedCards.Sum() < 3, "涉猎只能选择至多三种不同的亮出资源。");
                f.Cards[c.CardType]--; f.SelectedCards[c.CardType]++; return;
            }
            Require(d.Kind == "harvest" || d.Kind == "raid-revalidate", "未知的摸牌待决。");
            if (c.Option == "pass")
            {
                a.ChoiceComplete = true; a.ReplacementKind = null; a.ForgoneCards = new int[8]; a.Targets = Array.Empty<string>();
                if (d.Kind == "raid-revalidate") f.Kind = "claim"; else Remove(s, f); return;
            }
            Require(!IsDrawBlocked(s, f.PlayerId), "禁摸期间不能使用摸牌替代。");
            if (c.Option == "raid")
            {
                Require(a.GeneralId == "tk-g01" && Valid(c.Counts) && c.Counts.Sum() > 0 && Contains(a.Cards, c.Counts) && c.TargetPlayerIds != null &&
                    (d.Kind != "raid-revalidate" || c.Counts.Sum() <= a.ForgoneCards.Sum()) &&
                    c.TargetPlayerIds.Length == c.Counts.Sum() && c.TargetPlayerIds.Distinct().Count() == c.TargetPlayerIds.Length && c.TargetPlayerIds.All(t => RaidTargets(host, f.PlayerId).Contains(t)), "突袭额度、少领牌或目标无效。");
                a.ForgoneCards = Copy(c.Counts); a.Targets = (string[])c.TargetPlayerIds.Clone(); a.ReplacementKind = "raid"; a.ChoiceComplete = true;
                if (B4Enabled(s)) B4SkillHistoryEngine.Begin(s, "harvest:" + s.Skills.HarvestTurnId + ":" + f.TenureId, f.PlayerId, f.SkillId, f.TenureId, f.Id);
                if (d.Kind == "raid-revalidate")
                { f.Step = 1; foreach (var target in a.Targets.Reverse()) Target(host, f.Id, f.PlayerId, target); }
                else Remove(s, f);
                return;
            }
            if (c.Option == "shelie")
            {
                Require(a.GeneralId == "tk-g28" && d.Kind == "harvest", "当前不可发动涉猎。");
                if (B4Enabled(s)) B4SkillHistoryEngine.Begin(s, f.Id, f.PlayerId, f.SkillId, f.TenureId, f.Id);
                host.AddBank(a.Cards); a.Cards = new int[8]; a.ReplacementKind = "shelie";
                f.Kind = "shelie"; f.Cards = TakeRandom(host, f.PlayerId, 4); s.Skills.RevealedCards = Copy(f.Cards); return;
            }
            if (c.Option == "tiles")
            {
                Require(a.GeneralId == "tk-g37" && d.Kind == "harvest" && c.TileIds != null && c.TileIds.Length == 2 && c.TileIds.Distinct().Count() == 2, "天妒需要两个不同的合法地块。");
                var pair = TilePairs(host, f.PlayerId).FirstOrDefault(p => p.Select(t => t.TileId).OrderBy(t => t, StringComparer.Ordinal).SequenceEqual(c.TileIds.OrderBy(t => t, StringComparer.Ordinal)));
                Require(pair != null, "天妒地块不具生产资格或数字合计不符。");
                if (B4Enabled(s)) B4SkillHistoryEngine.Begin(s, f.Id, f.PlayerId, f.SkillId, f.TenureId, f.Id);
                f.Tiles = pair.OrderBy(t => t.TileId, StringComparer.Ordinal).Select(t => new B2HarvestTile { TileId = t.TileId, Number = t.Number, AllowedCardTypes = (int[])t.AllowedCardTypes.Clone() }).ToArray();
                host.AddBank(a.Cards); a.Cards = new int[8]; a.ReplacementKind = "tiandu"; f.Kind = "tiandu"; f.Step = 0; return;
            }
            throw new InvalidOperationException("无效的摸牌替代选择。");
        }

        private static void FinishShelie(IThreeKingdomsCardHost host, B2SkillFrame f, B2HarvestAllocation a)
        {
            host.AddBank(f.Cards); a.Cards = Copy(f.SelectedCards); f.Cards = new int[8]; f.SelectedCards = new int[8];
            a.ChoiceComplete = true; host.State.Skills.RevealedCards = new int[8]; Remove(host.State, f);
        }

        private static void CommitRaid(IThreeKingdomsCardHost host, B2SkillFrame f, B2HarvestAllocation a)
        {
            var s = host.State; var changes = new List<B2HandChange>(); var gain = Copy(a.Cards); Subtract(gain, a.ForgoneCards);
            var ownBefore = host.GetHand(f.PlayerId).Sum();
            foreach (var target in a.Targets)
            {
                var hand = host.GetHand(target); var before = hand.Sum(); var random = host.DrawRandom(before); var type = 0;
                for (; type < 8; type++) { if (random < hand[type]) break; random -= hand[type]; }
                var card = One(type); host.RemoveHand(target, card); Add(gain, card);
                var loss = Change(target, before, before - 1, 1, 0); if (B4Enabled(s)) loss.LostCards = Copy(card); changes.Add(loss);
            }
            host.AddBank(a.ForgoneCards); host.AddHand(f.PlayerId, gain);
            var received = Change(f.PlayerId, ownBefore, host.GetHand(f.PlayerId).Sum(), 0, 0); if (B4Enabled(s)) { received.GainedCards = Copy(gain); received.SourcePlayerId = a.Targets.FirstOrDefault(); } changes.Add(received);
            a.Cards = new int[8]; a.ForgoneCards = new int[8]; a.Completed = true; a.ActuallyReceived = gain.Sum(); Remove(s, f);
            RecordCards(host, f.Id + ":atomic", changes.ToArray());
            if (B4Enabled(s)) B4SkillHistoryEngine.Complete(s, "harvest:" + s.Skills.HarvestTurnId + ":" + f.TenureId, "突袭实际获得 " + gain.Sum() + " 张手牌", new[] { f.Id + ":atomic" });
        }

        private static string[] RaidTargets(IThreeKingdomsCardHost host, string source) => host.State.SeatPlayerIds.Where(p => p != source && host.GetHand(p).Sum() > 0 && !IsProtected(host.State, p)).ToArray();
        private static IEnumerable<B2HarvestTile[]> TilePairs(IThreeKingdomsCardHost host, string player)
        {
            var tiles = host.GetHarvestTiles(player).Where(t => t != null && t.Number > 0 && t.AllowedCardTypes != null && t.AllowedCardTypes.Length > 0 && t.AllowedCardTypes.All(c => c >= 0 && c < 8)).OrderBy(t => t.TileId, StringComparer.Ordinal).ToArray();
            for (var i = 0; i < tiles.Length; i++) for (var j = i + 1; j < tiles.Length; j++)
                if (tiles[i].TileId != tiles[j].TileId && tiles[i].Number + tiles[j].Number == host.FinalDiceTotal) yield return new[] { tiles[i], tiles[j] };
        }
        private static IEnumerable<string[]> Subsets(string[] source)
        { for (var mask = 1; mask < 1 << source.Length; mask++) yield return source.Where((value, index) => (mask & 1 << index) != 0).ToArray(); }
        private static IEnumerable<int[]> CardSelections(int[] available, int count)
        {
            return SelectCards(available, count, 0, new int[8]);
        }
        private static IEnumerable<int[]> SelectCards(int[] available, int count, int index, int[] current)
        {
            if (index == 8) { if (count == 0) yield return Copy(current); yield break; }
            for (var n = 0; n <= Math.Min(available[index], count); n++)
            { current[index] = n; foreach (var choice in SelectCards(available, count - n, index + 1, current)) yield return choice; }
            current[index] = 0;
        }
        private static string Describe(int[] cards) => string.Join("、", Enumerable.Range(0, 8).Where(t => cards[t] > 0).Select(t => CardNames[t] + cards[t]));
    }
}
