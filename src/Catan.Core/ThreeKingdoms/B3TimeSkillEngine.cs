using System;
using System.Collections.Generic;
using System.Linq;

namespace Catan.Core.ThreeKingdoms
{
    // B3 W13/W21/W29/W30：真实牌去向、翻面与额外机会均保存在既有权威队列中。
    public static partial class B2SkillEngine
    {
        private static readonly string[] ReturnCauses = { "seven-discard", "discard", "payment", "play" };
        private static bool B3Enabled(ThreeKingdomsState s) => s?.SkillsEnabled == true && (s.RulesVersion == GeneralCatalog.B3RulesVersion || ThreeKingdomsEngine.UsesB4Lifecycle(s));
        private static B3ReturnedCards[] Returned(ThreeKingdomsState s) => s?.Skills?.ReturnedCards ?? Array.Empty<B3ReturnedCards>();

        /// <summary>宿主已把真实牌移入银行后调用；七点全部弃完前只保留牌，不产生技能待决。</summary>
        public static void HoldReturnedCards(IThreeKingdomsCardHost host, string playerId, string cause, string eventId, int[] cards)
        {
            if (!Enabled(host)) return;
            if (!B3Enabled(host.State)) { HoldReturnedCards(host, cards); return; }
            var s = host.State;
            Require(s.SeatPlayerIds.Contains(playerId) && ReturnCauses.Contains(cause) && !string.IsNullOrEmpty(eventId) && Valid(cards), "退牌来源、原因或实体牌无效。");
            if (cards.Sum() == 0 || !Mark(s, "returned:" + eventId + ":" + playerId + ":" + cause)) return;
            host.RemoveBank(cards);
            s.Skills.ReturnedCards = Returned(s).Concat(new[] { new B3ReturnedCards { PlayerId = playerId, Cause = cause, EventId = eventId, Cards = Copy(cards) } }).ToArray();
            if (cause != "seven-discard") QueueReturnClaims(host, eventId, false);
        }

        /// <summary>整个七点批次的所有强制弃牌完成后调用一次，包含零弃牌批次；同批所有来源共用 eventId。</summary>
        public static void CompleteSevenDiscard(IThreeKingdomsCardHost host, string eventId)
        {
            if (!B3Enabled(host?.State)) return;
            Require(!string.IsNullOrEmpty(eventId), "七点弃牌缺少批次身份。");
            if (!Mark(host.State, "seven-complete:" + eventId)) return;
            QueueReturnClaims(host, eventId, true); Pump(host);
        }

        private static void QueueReturnClaims(IThreeKingdomsCardHost host, string eventId, bool seven)
        {
            var s = host.State; var batches = Returned(s).Where(r => r.EventId == eventId).ToArray();
            if (batches.Length == 0) return;
            var frames = new List<B2SkillFrame>();
            foreach (var id in Order(s))
            {
                var g = Active(s, id); if (g == null) continue;
                if (seven && g.GeneralId == "tk-g21") frames.Add(Frame(s, g, "tk-g21-s1", "guzheng", 0, eventId));
                if (g.GeneralId == "tk-g30" && s.Clock.ActivePlayerId != id)
                    foreach (var source in batches.Where(r => r.PlayerId != id && r.Cards[0] > 0).Select(r => r.PlayerId).Distinct())
                    {
                        var f = Frame(s, g, "tk-g30-s1", "luoying", 0, eventId); f.Targets = new[] { source }; frames.Add(f);
                    }
            }
            frames.Add(new B2SkillFrame { Id = Id(s, "return"), Kind = "return-cards", SourceEventId = eventId, Started = true });
            // 原弃牌发生过即满足颂威；截获后临时区为空不会消掉这次触发。
            if (seven && batches.Sum(r => r.Cards.Sum()) > 0)
                foreach (var id in Order(s))
                {
                    var g = Active(s, id); if (g?.GeneralId == "tk-g29") frames.Add(Frame(s, g, "tk-g29-s2", "mixed", 1, eventId));
                }
            // 七点截获在整批弃牌的失牌反应之后；主动费用退牌则插在父技能的后续效果之前。
            if (seven) foreach (var frame in frames) Append(s, frame);
            else Prepend(s, frames);
        }

        /// <summary>仅在自己的正常行动机会开始调用，且在翻面跳过检查之前调用；额外机会不刷新三次额度。</summary>
        public static void ResetNormalOpportunity(ThreeKingdomsState state, string playerId)
        {
            if (!B3Enabled(state)) return;
            var g = Active(state, playerId); if (g?.GeneralId != "tk-g30") return;
            g.Counters = g.Counters.Where(c => c.Key != "jiushi-extra").ToArray();
        }

        private static void AddTimeEndFrames(IThreeKingdomsCardHost host, GeneralInstance g, string eventId)
        {
            var s = host.State; if (!B3Enabled(s)) return;
            if (g.GeneralId == "tk-g13" && Value(g, "lianpo", 0) == 0)
            { var f = Frame(s, g, "tk-g13-s1", "lianpo", 4, eventId); f.Optional = true; Append(s, f); }
            if (CanJiushiExtra(host, g))
            { var f = Frame(s, g, "tk-g30-s2", "jiushi-extra", 0, eventId); f.Optional = true; Append(s, f); }
        }

        private static bool CanJiushiExtra(IThreeKingdomsCardHost host, GeneralInstance g) => g?.GeneralId == "tk-g30" &&
            host.State.Clock.ActivePlayerId != g.OwnerPlayerId && host.State.Players.Single(p => p.PlayerId == g.OwnerPlayerId).FaceDown &&
            Value(g, "jiushi-extra", 0) < 3 && host.GetHand(g.OwnerPlayerId)[0] >= 7;

        private static void ValidateTimeOptional(IThreeKingdomsCardHost host, B2SkillFrame f)
        {
            if (f.Kind != "lianpo" && f.Kind != "jiushi-extra") return;
            var g = Active(host.State, f.PlayerId);
            Require(B3Enabled(host.State) && g?.TenureId == f.TenureId &&
                (f.Kind == "lianpo" ? Value(g, "lianpo", 0) == 0 : CanJiushiExtra(host, g)), "技能次数或发动条件已不满足。");
        }

        private static bool ProcessTimeFrame(IThreeKingdomsCardHost host, B2SkillFrame f, out bool waiting)
        {
            var s = host.State; waiting = false;
            if (f.Kind == "lianpo")
            {
                var g = s.Generals.FirstOrDefault(x => x.TenureId == f.TenureId);
                if (g != null) Counter(g, "lianpo", 0).Value = 1;
                ThreeKingdomsEngine.QueueExtraTurn(s, f.PlayerId); f.Kind = "mixed"; return true;
            }
            if (f.Kind == "jiushi-extra")
            {
                var g = Active(s, f.PlayerId); Require(CanJiushiExtra(host, g), "酒诗要求背面、回合外次数及七张木头。");
                var cost = One(0); cost[0] = 7; var before = host.GetHand(f.PlayerId).Sum();
                Counter(g, "jiushi-extra", 0).Value++; host.RemoveHand(f.PlayerId, cost); host.AddBank(cost); Remove(s, f);
                // 原牌文没有先翻回；已排入的背面额外机会照常跳过并翻回。
                ThreeKingdomsEngine.QueueExtraTurn(s, f.PlayerId);
                HoldReturnedCards(host, f.PlayerId, "discard", f.Id + ":cost", cost);
                RecordCards(host, f.Id + ":cost", new[] { Change(f.PlayerId, before, before - 7, 7, 0) }); return true;
            }
            if (f.Kind == "guzheng")
            {
                var cards = new int[8];
                foreach (var batch in Returned(s).Where(r => r.EventId == f.SourceEventId)) { Add(cards, batch.Cards); Array.Clear(batch.Cards, 0, 8); }
                Remove(s, f); Gain(host, f.PlayerId, cards, f.SkillId, f.Id); return true;
            }
            if (f.Kind == "luoying")
            {
                if (AvailableLuoying(host, f) <= 0) { Remove(s, f); return true; }
                Decide(s, f, "luoying", "落英：收取木头", "可从 " + f.Targets[0] + " 的退牌中收取木头，每名来源玩家每回合最多两张。"); waiting = true; return true;
            }
            if (f.Kind == "return-cards")
            {
                foreach (var batch in Returned(s).Where(r => r.EventId == f.SourceEventId)) host.AddBank(batch.Cards);
                s.Skills.ReturnedCards = Returned(s).Where(r => r.EventId != f.SourceEventId).ToArray(); Remove(s, f); return true;
            }
            if (f.Kind == "fangzhu")
            {
                foreach (var id in f.Targets)
                {
                    var player = s.Players.Single(p => p.PlayerId == id);
                    if (!player.FaceDown && id != f.PlayerId && Active(s, id)?.GeneralId == "tk-g19" && host.GetHand(id).Sum() == 0) continue;
                    player.FaceDown = !player.FaceDown;
                }
                Remove(s, f); return true;
            }
            return false;
        }

        private static int AvailableLuoying(IThreeKingdomsCardHost host, B2SkillFrame f)
        {
            var s = host.State; var g = Active(s, f.PlayerId);
            if (g?.TenureId != f.TenureId || s.Clock.ActivePlayerId == f.PlayerId) return 0;
            return Math.Min(Math.Max(0, 2 - Value(g, "luoying:" + f.Targets[0], s.Clock.TurnInstanceId)),
                Returned(s).Where(r => r.EventId == f.SourceEventId && r.PlayerId == f.Targets[0]).Sum(r => r.Cards[0]));
        }

        private static bool AddTimeDecisionActions(IThreeKingdomsCardHost host, B2SkillFrame f, B2SkillDecision d, List<B2SkillAction> actions)
        {
            if (d.Kind != "luoying") return false;
            for (var count = 1; count <= AvailableLuoying(host, f); count++)
            { var a = Action(host.State, f.SkillId, "luoying", "落英：获得" + count + "木", d); a.Command.Counts[0] = count; actions.Add(a); }
            actions.Add(Action(host.State, f.SkillId, "skip", "放弃落英", d)); return true;
        }

        private static bool ResolveTimeDecision(IThreeKingdomsCardHost host, B2SkillFrame f, B2SkillDecision d, B2SkillCommand c)
        {
            if (d.Kind != "luoying") return false;
            if (c.Option == "skip") { Remove(host.State, f); return true; }
            Require(c.Option == "luoying" && Valid(c.Counts) && c.Counts.Skip(1).Sum() == 0 && c.Counts[0] >= 1 && c.Counts[0] <= AvailableLuoying(host, f), "落英数量、来源或额度无效。");
            var count = c.Counts[0]; var remaining = count;
            if (B4Enabled(host.State)) B4SkillHistoryEngine.Begin(host.State, f.Id, f.PlayerId, f.SkillId, f.TenureId, f.SourceEventId);
            foreach (var batch in Returned(host.State).Where(r => r.EventId == f.SourceEventId && r.PlayerId == f.Targets[0]))
            { var take = Math.Min(remaining, batch.Cards[0]); batch.Cards[0] -= take; remaining -= take; }
            Counter(Active(host.State, f.PlayerId), "luoying:" + f.Targets[0], host.State.Clock.TurnInstanceId).Value += count;
            Remove(host.State, f); Gain(host, f.PlayerId, c.Counts, f.SkillId, f.Id); return true;
        }

        private static void AddTimeActions(IThreeKingdomsCardHost host, GeneralInstance g, List<B2SkillAction> actions)
        {
            var s = host.State; if (!B3Enabled(s) || s.Clock.ActivePlayerId != g.OwnerPlayerId || s.Clock.Phase != ThreeKingdomsPhase.Play) return;
            if (g.GeneralId == "tk-g29" && Value(g, "fangzhu", 0) == 0)
                for (var first = 0; first < s.SeatPlayerIds.Length; first++) for (var second = first + 1; second < s.SeatPlayerIds.Length; second++)
                {
                    var targets = new[] { s.SeatPlayerIds[first], s.SeatPlayerIds[second] };
                    var a = Action(s, "tk-g29-s1", "fangzhu", "放逐：翻面 " + string.Join("、", targets)); a.Command.TargetPlayerIds = targets; actions.Add(a);
                }
            if (g.GeneralId == "tk-g30" && Value(g, "jiushi", s.Clock.PhaseId) == 0)
                actions.Add(Action(s, "tk-g30-s2", "jiushi", "酒诗：翻面并摸三张资源"));
        }

        private static bool ResolveTimeActive(IThreeKingdomsCardHost host, GeneralInstance g, B2SkillCommand c)
        {
            if (c.Option != "fangzhu" && c.Option != "jiushi") return false;
            var s = host.State;
            Require(B3Enabled(s) && s.Clock.ActivePlayerId == g.OwnerPlayerId && s.Clock.Phase == ThreeKingdomsPhase.Play, "当前不能发动此翻面技能。");
            if (c.Option == "fangzhu")
            {
                Require(g.GeneralId == "tk-g29" && c.SkillId == "tk-g29-s1" && Value(g, "fangzhu", 0) == 0 &&
                    c.TargetPlayerIds?.Length == 2 && c.TargetPlayerIds.Distinct().Count() == 2 && c.TargetPlayerIds.All(s.SeatPlayerIds.Contains), "放逐需要两名不同合法玩家且限定次数未用。");
                Counter(g, "fangzhu", 0).Value = 1;
                // 先完成成为目标的响应；两人翻面同一原子提交，当前回合不会被中断。
                var effect = Frame(s, g, c.SkillId, "fangzhu", 0, null); effect.Started = true; effect.Targets = (string[])c.TargetPlayerIds.Clone(); Append(s, effect);
                foreach (var id in c.TargetPlayerIds) Target(host, effect.Id, g.OwnerPlayerId, id);
                return true;
            }
            Require(g.GeneralId == "tk-g30" && c.SkillId == "tk-g30-s2" && Value(g, "jiushi", s.Clock.PhaseId) == 0, "酒诗每个自己的出牌阶段限一次。");
            Counter(g, "jiushi", s.Clock.PhaseId).Value = 1;
            var owner = s.Players.Single(p => p.PlayerId == g.OwnerPlayerId); owner.FaceDown = !owner.FaceDown;
            var reward = Frame(s, g, c.SkillId, "resource", 3, null); reward.Started = true; Append(s, reward); return true;
        }
    }
}
