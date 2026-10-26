using System;
using System.Collections.Generic;
using System.Linq;

namespace Catan.Core.ThreeKingdoms
{
    public static partial class B2SkillEngine
    {
        public static bool CanGainB6(ThreeKingdomsState s, string playerId, int cardType) =>
            !B6Enabled(s) || cardType < 0 || cardType >= 8 || !s.Skills.TimedEffects.Any(e => e.Kind == "b6-jinglue:" + cardType && e.PlayerId != playerId);

        public static bool CanGainCardsB6(ThreeKingdomsState s, string playerId, int[] cards) =>
            cards != null && cards.Length == 8 && cards.All(n => n >= 0) && Enumerable.Range(0, 8).All(t => cards[t] == 0 || CanGainB6(s, playerId, t));

        public static void ProjectSeatTradeB6(IThreeKingdomsCardHost host, ThreeKingdomsView view, string viewer)
        {
            var s=host?.State;if(!B6Enabled(s))return;
            view.BlockedGainCardTypes=Enumerable.Range(0,8).Where(t=>!CanGainB6(s,viewer,t)).ToArray();
            view.AllowedTradePartnerPlayerIds=s.SeatPlayerIds.Where(p=>p!=viewer&&B6AllianceRules.CanTrade(s,viewer,p)).ToArray();
            view.RequireEqualPlayerTradeCounts=s.ControlB6!=null&&s.ControlB6.AssetPlayerId==viewer&&s.ControlB6.OpportunityId==s.Clock.OpportunityId;
            view.SkillStatus=(view.SkillStatus??Array.Empty<string>()).Concat(s.Skills.TimedEffects.Where(e=>e.Kind.StartsWith("b6-jinglue:",StringComparison.Ordinal)).Select(e=>e.PlayerId+" 景略：其他玩家不能获得"+CardNames[int.Parse(e.Kind.Substring(11))])).ToArray();
            var d=s.Skills.PendingDecision;
            if(d?.Kind!="seat-jinglue-card"||d.PlayerId!=viewer)return;
            var f=s.Skills.Frames.Single(x=>x.Id==d.FrameId);
            view.MilitaryHands=(view.MilitaryHands??Array.Empty<B5RevealedHand>()).Where(h=>h.PlayerId!=f.Targets[0]).Concat(new[]{new B5RevealedHand{PlayerId=f.Targets[0],Cards=host.GetHand(f.Targets[0]),Public=false}}).ToArray();
        }

        public static void OnSeatTradeB6OpportunityStart(IThreeKingdomsCardHost host, long opportunityId, string playerId, string[] skippedPlayerIds = null)
        {
            var s = host?.State; if (!B6Enabled(s)) return;
            var begun = new HashSet<string>(skippedPlayerIds ?? Array.Empty<string>()) { playerId };
            s.Skills.TimedEffects = s.Skills.TimedEffects.Where(e => !(e.Kind.StartsWith("b6-jinglue:", StringComparison.Ordinal) && begun.Contains(e.PlayerId) && e.OpportunityId < opportunityId)).ToArray();
        }

        public static int JiaoziAdjustmentB6(IThreeKingdomsCardHost host, string player)
        {
            if (!B6Enabled(host?.State) || Active(host.State, player)?.GeneralId != "tk-g46") return 0;
            int own = host.GetHand(player).Sum(); var others = host.State.SeatPlayerIds.Where(p => p != player).Select(p => host.GetHand(p).Sum()).ToArray();
            return others.All(n => own > n) ? 1 : others.All(n => own < n) ? -1 : 0;
        }

        public static int[] JiaoziCostB6(int[] discounted, int[] standard, int adjustment, int type)
        {
            Require(Valid(discounted) && Valid(standard), "建造费用无效。");
            var cost = Copy(discounted); if (adjustment == 0 || cost.Sum() == 0) return cost;
            Require((adjustment == 1 || adjustment == -1) && type >= 0 && type < 5 && standard[type] > 0, "请选择正常造价含有的资源。");
            if (adjustment < 0) { Require(cost[type] > 0, "所选资源已被其他折扣减免。"); if (cost.Sum() > 1) cost[type]--; }
            else cost[type]++;
            return cost;
        }

        public static void QueueJiaoziB6(IThreeKingdomsCardHost host, B4VirtualUse construction)
        {
            var s = host.State; var g = Active(s, s.Clock.ActivePlayerId);
            Require(B6Enabled(s) && g?.GeneralId == "tk-g46" && host is IThreeKingdomsB6SeatTradeHost, "当前不能选择骄姿费用。");
            var f = Frame(s, g, "tk-g46-s2", "seat-jiaozi", 0, construction.SourceEventId);
            f.B4 = new B4SkillPayload { OwnerPlayerId = g.OwnerPlayerId, VirtualUse = construction }; f.Started = true; Append(s, f);
        }

        private static bool SeatTradeReady(IThreeKingdomsCardHost host) => B6Enabled(host?.State) && host is IThreeKingdomsB6SeatTradeHost;
        private static bool FuqiAllowed(IThreeKingdomsCardHost host, string player, string target) => player != target &&
            ((IThreeKingdomsB6SeatTradeHost)host).AreTerritoriesAdjacent(player, target) && B6AllianceRules.CanTrade(host.State, player, target) &&
            !IsB4HandLossForbidden(host.State, player) && !IsB4HandLossForbidden(host.State, target) && !IsDrawBlocked(host.State, player) && !IsDrawBlocked(host.State, target);

        private static void AddSeatTradeB6Actions(IThreeKingdomsCardHost host, GeneralInstance g, List<B2SkillAction> actions)
        {
            if (!SeatTradeReady(host)) return; var s = host.State; var player = g.OwnerPlayerId;
            if (s.Clock.ActivePlayerId != player || s.Clock.Phase != ThreeKingdomsPhase.Play) return;
            if (g.GeneralId == "tk-g46" && Value(g, "fuqi", s.Clock.PhaseId) < 2 && host.GetHand(player).Sum() > 0)
                foreach (var target in s.SeatPlayerIds.Where(p => FuqiAllowed(host, player, p)))
                    AddB4TargetAction(s, actions, "tk-g46-s1", "seat-fuqi", "伏骑：与 " + target + " 交换", target);
            if (g.GeneralId != "tk-g48") return;
            if (Value(g, "baiyi", 0) == 0)
                for (int i = 0; i < s.SeatPlayerIds.Length; i++) for (int j = i + 1; j < s.SeatPlayerIds.Length; j++)
                { var a = Action(s, "tk-g48-s1", "seat-baiyi", "败移：交换 " + s.SeatPlayerIds[i] + " 与 " + s.SeatPlayerIds[j]); a.Command.TargetPlayerIds = new[] { s.SeatPlayerIds[i], s.SeatPlayerIds[j] }; a.SelectionPlayerIds = a.Command.TargetPlayerIds; actions.Add(a); }
            if (Value(g, "jinglue", s.Clock.PhaseId) == 0 && !IsDrawBlocked(s, player))
                foreach (var target in s.SeatPlayerIds.Where(p => p != player && host.GetHand(p).Sum() > 0 && !CivilProtected(host, p) && !IsB4HandLossForbidden(s, p) && B6AllianceRules.CanAttack(s, player, p)))
                    AddB4TargetAction(s, actions, "tk-g48-s2", "seat-jinglue", "景略：查看 " + target + " 的手牌", target);
        }

        private static bool ResolveSeatTradeB6Active(IThreeKingdomsCardHost host, GeneralInstance g, B2SkillCommand c)
        {
            if (c.Option == null || !c.Option.StartsWith("seat-", StringComparison.Ordinal)) return false;
            var legal = new List<B2SkillAction>(); AddSeatTradeB6Actions(host, g, legal); Require(legal.Any(a => SameB4Action(a.Command, c)), "技能时机、目标或次数无效。");
            var s = host.State; var f = Frame(s, g, c.SkillId, c.Option, 0, null); f.Started = true;
            f.Targets = (string[])c.TargetPlayerIds.Clone(); f.B4 = new B4SkillPayload { OwnerPlayerId = g.OwnerPlayerId };
            Append(s, f);
            if (c.Option == "seat-baiyi")
            {
                Counter(g, "baiyi", 0).Value = 1;
                int a = Array.IndexOf(s.SeatPlayerIds, f.Targets[0]), b = Array.IndexOf(s.SeatPlayerIds, f.Targets[1]);
                s.SeatPlayerIds[a] = f.Targets[1]; s.SeatPlayerIds[b] = f.Targets[0];
                // 仅交换固定座位占用；当前回合所有者、正常游标、关系及资产不改。
            }
            if (c.Option == "seat-jinglue") { Counter(g, "jinglue", s.Clock.PhaseId).Value = 1; Target(host, f.Id, g.OwnerPlayerId, f.Targets[0]); }
            return true;
        }

        private static bool ProcessSeatTradeB6Frame(IThreeKingdomsCardHost host, B2SkillFrame f, out bool waiting)
        {
            waiting = false; if (!f.Kind.StartsWith("seat-", StringComparison.Ordinal)) return false;
            Require(SeatTradeReady(host), "本规则包不能结算座次与交易技能。"); var s = host.State;
            if (f.Kind == "seat-baiyi") { Remove(s, f); return true; }
            if (f.Kind == "seat-jiaozi" && f.Step > 0)
            {
                var use = f.B4.VirtualUse; var type = f.B4.DeclaredType; Remove(s, f);
                ((IThreeKingdomsB6SeatTradeHost)host).CompleteJiaoziConstruction(use, type); return true;
            }
            if (f.Kind == "seat-fuqi")
            {
                if (!FuqiAllowed(host, f.PlayerId, f.Targets[0])) { Remove(s, f); return true; }
                Decide(s, f, f.Step == 0 ? "seat-fuqi-give" : "seat-fuqi-request", "伏骑：" + (f.Step == 0 ? "选择交出的一张牌" : "选择要求的一至两张牌"), f.Step == 0 ? "可交出一张资源或商品。" : "要求一张或两张具体资源／商品，然后发起交易。");
            }
            else if (f.Kind == "seat-jinglue")
            {
                if (CivilProtected(host, f.Targets[0]) || IsB4HandLossForbidden(s, f.Targets[0]) || IsDrawBlocked(s, f.PlayerId)) { Remove(s, f); return true; }
                Decide(s, f, "seat-jinglue-card", "景略：选择一张手牌", "取得与上一次不同类型的一张真实资源／商品。");
            }
            else if (f.Kind == "seat-jiaozi") Decide(s, f, "seat-jiaozi-card", "骄姿：选择调整的资源", "从本项正常造价含有的资源中选择一项。");
            else throw new InvalidOperationException("未知座次与交易步骤。");
            waiting = true; return true;
        }

        private static B2SkillAction SeatCardAction(ThreeKingdomsState s, B2SkillFrame f, B2SkillDecision d, string option, int type, string prefix, string kind)
        { var a = Action(s, f.SkillId, option, prefix + CardNames[type], d); a.Command.CardType = type; a.RealCardType = type; a.CardSelectionKind = kind; return a; }

        private static bool AddSeatTradeB6DecisionActions(IThreeKingdomsCardHost host, B2SkillFrame f, B2SkillDecision d, List<B2SkillAction> actions)
        {
            if (!d.Kind.StartsWith("seat-", StringComparison.Ordinal)) return false; var s = host.State;
            if (d.Kind == "seat-fuqi-give")
                foreach (int t in Enumerable.Range(0, 8).Where(t => host.GetHand(f.PlayerId)[t] > 0 && CanGainB6(s, f.Targets[0], t)))
                { var a = SeatCardAction(s, f, d, "seat-fuqi-give", t, "交出", "payment"); a.AvailableCount = host.GetHand(f.PlayerId)[t]; actions.Add(a); }
            if (d.Kind == "seat-fuqi-request")
            {
                if (f.B4.OtherSelection.Sum() < 2) foreach (int t in Enumerable.Range(0, 8).Where(t => CanGainB6(s, f.PlayerId, t)))
                    actions.Add(SeatCardAction(s, f, d, "seat-fuqi-request", t, "要求", "declare"));
                if (f.B4.OtherSelection.Sum() > 0)
                { var a = Action(s, f.SkillId, "seat-fuqi-submit", "发起 1:" + f.B4.OtherSelection.Sum() + " 交易", d); a.Command.Counts = Copy(f.B4.Selection); a.Command.SecondaryCounts = Copy(f.B4.OtherSelection); actions.Add(a); }
            }
            if (d.Kind == "seat-jinglue-card")
            {
                var g = s.Generals.FirstOrDefault(g => g.TenureId == f.TenureId); int last = g == null ? 0 : Value(g, "jinglue-last", 0);
                foreach (int t in Enumerable.Range(0, 8).Where(t => host.GetHand(f.Targets[0])[t] > 0 && t + 1 != last && CanGainB6(s, f.PlayerId, t)))
                { var a = SeatCardAction(s, f, d, "seat-jinglue-take", t, "取得", "select"); a.AvailableCount = host.GetHand(f.Targets[0])[t]; a.SelectionPlayerIds = f.Targets; actions.Add(a); }
            }
            if (d.Kind == "seat-jiaozi-card")
                foreach (int t in ((IThreeKingdomsB6SeatTradeHost)host).JiaoziChoices(f.B4.VirtualUse)) actions.Add(SeatCardAction(s, f, d, "seat-jiaozi-card", t, "调整", "declare"));
            if(d.Kind!="seat-jinglue-card"||actions.Count==0)actions.Add(Action(s, f.SkillId, "seat-cancel", d.Kind=="seat-jinglue-card"?"完成查看":"取消", d)); return true;
        }

        private static bool ResolveSeatTradeB6Decision(IThreeKingdomsCardHost host, B2SkillFrame f, B2SkillDecision d, B2SkillCommand c)
        {
            if (!d.Kind.StartsWith("seat-", StringComparison.Ordinal)) return false;
            var legal = new List<B2SkillAction>(); AddSeatTradeB6DecisionActions(host, f, d, legal); Require(legal.Any(a => SameB4Action(a.Command, c) && (a.Command.SecondaryCounts ?? new int[8]).SequenceEqual(c.SecondaryCounts ?? new int[8]) && a.Command.Counts.SequenceEqual(c.Counts)), "所选牌或交易内容无效。");
            var s = host.State;
            if (c.Option == "seat-cancel") { Remove(s, f); return true; }
            if (c.Option == "seat-fuqi-give") { f.B4.Selection = One(c.CardType); f.Step = 1; return true; }
            if (c.Option == "seat-fuqi-request") { f.B4.OtherSelection[c.CardType]++; return true; }
            if (c.Option == "seat-jiaozi-card") { f.B4.DeclaredType = c.CardType; f.Step = 1; return true; }
            var g = s.Generals.FirstOrDefault(x => x.TenureId == f.TenureId); Require(g != null, "技能任次已失效。");
            if (c.Option == "seat-jinglue-take")
            {
                Require(!CivilProtected(host, f.Targets[0]) && !IsB4HandLossForbidden(s, f.Targets[0]) && !IsDrawBlocked(s, f.PlayerId), "当前不能取得该手牌。");
                Counter(g, "jinglue", s.Clock.PhaseId).Value = 1; Counter(g, "jinglue-last", 0).Value = c.CardType + 1;
                AddTimed(s, "b6-jinglue:" + c.CardType, f.PlayerId, null, s.Clock.OpportunityId);
                Remove(s, f); B4Transfer(host, f.Targets[0], f.PlayerId, One(c.CardType), f.Id + ":take", f.SkillId); return true;
            }
            Require(c.Option == "seat-fuqi-submit" && FuqiAllowed(host, f.PlayerId, f.Targets[0]) && Value(g, "fuqi", s.Clock.PhaseId) < 2, "当前不能发起伏骑交易。");
            Counter(g, "fuqi", s.Clock.PhaseId).Value++;
            int[] give = f.B4.Selection, receive = f.B4.OtherSelection; string target = f.Targets[0]; Remove(s, f);
            if (!Contains(host.GetHand(f.PlayerId), give) || !Contains(host.GetHand(target), receive)) { B4SkillHistoryEngine.Complete(s, f.Id, "伏骑交易未成交"); return true; }
            int before = host.GetHand(f.PlayerId).Sum(), targetBefore = host.GetHand(target).Sum();
            host.RemoveHand(f.PlayerId, give); host.RemoveHand(target, receive); host.AddHand(f.PlayerId, receive); host.AddHand(target, give);
            var changes = new[] {
                new B2HandChange { PlayerId=f.PlayerId,BeforeCount=before,AfterCount=before-1+receive.Sum(),LostCount=1,GivenCount=1,LostCards=Copy(give),GainedCards=Copy(receive),SourcePlayerId=target,SourceSkillId=f.SkillId },
                new B2HandChange { PlayerId=target,BeforeCount=targetBefore,AfterCount=targetBefore-receive.Sum()+1,LostCount=receive.Sum(),GivenCount=receive.Sum(),LostCards=Copy(receive),GainedCards=Copy(give),SourcePlayerId=f.PlayerId,SourceSkillId=f.SkillId } };
            RecordCards(host, f.Id + ":exchange", changes); B4SkillHistoryEngine.LinkCardFlow(s, f.Id, f.Id + ":exchange", changes); return true;
        }
    }
}
