using System;
using System.Collections.Generic;
using System.Linq;

namespace Catan.Core.ThreeKingdoms
{
    // 百科 W26/W39/W40/W41/W42/W44/W45/W52；只保存实体牌及可重放步骤。
    public static partial class B2SkillEngine
    {
        public static bool B4Enabled(ThreeKingdomsState s) => s?.SkillsEnabled == true && ThreeKingdomsEngine.UsesB4Lifecycle(s);
        public static bool B5Enabled(ThreeKingdomsState s) => s?.SkillsEnabled == true && s.RulesVersion == GeneralCatalog.B5RulesVersion;
        private static B4SkillPayload CloneB4Payload(B4SkillPayload p) => p == null ? null : new B4SkillPayload { Selection = Copy(p.Selection), OtherSelection = Copy(p.OtherSelection), DeclaredType = p.DeclaredType,
            OwnerPlayerId = p.OwnerPlayerId, LockedTargets = (string[])p.LockedTargets.Clone(), VirtualUse = p.VirtualUse == null ? null : new B4VirtualUse { CardId = p.VirtualUse.CardId, OptionId = p.VirtualUse.OptionId, Label = p.VirtualUse.Label, CardType = p.VirtualUse.CardType, TargetPlayerIds = (string[])p.VirtualUse.TargetPlayerIds.Clone(), PayloadJson = p.VirtualUse.PayloadJson, SourceEventId = p.VirtualUse.SourceEventId, SourceSkillId = p.VirtualUse.SourceSkillId, RewardRandomCount = p.VirtualUse.RewardRandomCount } };

        private static void AddB4CardFrames(IThreeKingdomsCardHost host, string batch, B2HandChange c, GeneralInstance g, List<B2SkillFrame> frames)
        {
            var s = host.State; if (!B4Enabled(s)) return;
            AddResponseCardFrames(host, batch, c, g, frames);
            int gained = c.GainedCards?.Sum() ?? Math.Max(0, c.AfterCount - c.BeforeCount + c.LostCount);
            bool own = s.Clock.ActivePlayerId == c.PlayerId;
            if (c.LostProgressCount > 0) Counter(g, "lost-progress", s.Clock.TurnInstanceId).Value += c.LostProgressCount;
            if (g.GeneralId == "tk-g26" && !own)
            {
                foreach (var direction in new[] { "lost", "gained" })
                {
                    var count = Counter(g, "kuanshi-" + direction, s.Clock.TurnInstanceId); int before = count.Value;
                    count.Value += direction == "lost" ? c.LostCount : gained;
                    if (before < 2 && count.Value >= 2) frames.Add(Frame(s, g, "tk-g26-s2", "resource", 1, batch));
                }
            }
            if (g.GeneralId == "tk-g40" && own && gained > 0 && c.SourcePlayerId != null && c.SourcePlayerId != c.PlayerId && c.AfterCount < 4)
                frames.Add(Frame(s, g, "tk-g40-s1", "random", 1, batch));
            if (g.GeneralId == "tk-g52" && own && gained > 0 && c.SourceSkillId != "tk-g52-s1")
                frames.Add(Frame(s, g, "tk-g52-s1", "b4-zishu", 1, batch));
            if (g.GeneralId == "tk-g42" && !own && c.LostCount > 0 && Valid(c.LostCards))
            {
                for (int type = 0; type < 8; type++) for (int n = 0; n < c.LostCards[type]; n++)
                { var f = Frame(s, g, "tk-g42-s2", "b4-yajiao", 1, batch); f.B4 = new B4SkillPayload { DeclaredType = type }; f.Optional = true; frames.Add(f); }
            }
        }

        private static void OnB4Built(IThreeKingdomsCardHost host, string eventId, int type)
        {
            var s = host.State; if (!B4Enabled(s) || type < 0 || !Mark(s, "b4-built:" + eventId)) return;
            foreach (var g in s.Generals.Where(x => x.Region == GeneralRegion.Active && x.GeneralId == "tk-g41"))
            { Counter(g, "wuku", 0).Value = Math.Min(3, Value(g, "wuku", 0) + 1); B4SkillHistoryEngine.Begin(s, eventId + ":" + g.TenureId, g.OwnerPlayerId, "tk-g41-s1", g.TenureId, eventId, "获得武库标记"); B4SkillHistoryEngine.Complete(s, eventId + ":" + g.TenureId, "武库 " + Value(g, "wuku", 0)); }
        }
        private static void AddB4EndFrames(IThreeKingdomsCardHost host, GeneralInstance g, string eventId)
        {
            var s = host.State; if (!B4Enabled(s)) return;
            AddResponseEndFrames(host, g, eventId);
            if (g.GeneralId == "tk-g41" && Value(g, "wuku", 0) == 3 && Value(g, "sanchen", 0) == 0)
            { var f = Frame(s, g, "tk-g41-s2", "b4-sanchen", 0, eventId); Append(s, f); }
            if (g.GeneralId == "tk-g45" && s.Clock.ActivePlayerId == g.OwnerPlayerId)
            {
                int count = Enumerable.Range(0, 8).Where(t => Value(g, "wenji:" + t, s.Clock.TurnInstanceId) > 0).Sum(t => host.GetHand(g.OwnerPlayerId)[t]);
                bool lost = s.Skills.LossLedger.Any(l => l.PlayerId == g.OwnerPlayerId && l.TurnId == s.Clock.TurnInstanceId && l.LostHandCards > 0) || Value(g, "lost-progress", s.Clock.TurnInstanceId) > 0;
                if (!lost && count > 0) { var f = Frame(s, g, "tk-g45-s2", "random", count, eventId); f.Started = true; Append(s, f); }
                g.Counters = g.Counters.Where(c => !c.Key.StartsWith("wenji:", StringComparison.Ordinal)).ToArray();
            }
        }
        private static void OnB4OpportunityStart(IThreeKingdomsCardHost host, long opportunityId, string playerId) { OnResponseOpportunityStart(host, opportunityId, playerId); }
        public static void QueueB4ResourceChoice(IThreeKingdomsCardHost host, string playerId, string eventId, int count, string skillId)
        { Require(B4Enabled(host?.State) && host.State.SeatPlayerIds.Contains(playerId) && count >= 0, "额外收获来源无效。"); if (count == 0 || !Mark(host.State, "b4-resource:" + eventId)) return; Append(host.State, new B2SkillFrame { Id = Id(host.State, "frame"), PlayerId = playerId, SkillId = skillId, SourceEventId = eventId, Kind = "resource", Remaining = count, Started = true }); }
        public static void OnLuoshenCandidate(IThreeKingdomsCardHost host, string eventId, string playerId)
        {
            var s = host.State; var g = Active(s, playerId); Require(B4Enabled(s) && g?.GeneralId == "tk-g44", "洛神须由在场甄姬发动。");
            if (!Mark(s, "luoshen:" + eventId)) return;
            B4SkillHistoryEngine.Begin(s, eventId, playerId, "tk-g44-s1", g.TenureId, eventId);
            Gain(host, playerId, TakeRandom(host, playerId, 1), "tk-g44-s1", eventId); B4SkillHistoryEngine.Complete(s, eventId, "奇数奖励结算；等待采用结果或重掷");
            Pump(host);
        }

        /// <summary>真实牌不会改变身份；这里只给主动支付和交易提供一次转换的有效用途。</summary>
        public static int[] EffectiveCardTypes(ThreeKingdomsState s, string playerId, int realType)
        {
            if (realType < 0 || realType > 7) return Array.Empty<int>();
            var g = Active(s, playerId); var result = new List<int> { realType }; if (!B4Enabled(s) || g == null) return result.ToArray();
            if (g.GeneralId == "tk-g42" && realType < 4) result.Add(new[] { 1, 0, 3, 2 }[realType]);
            if (g.GeneralId == "tk-g44" && realType >= 5) result.Add(new[] { 2, 4, 0 }[realType - 5]);
            if (g.GeneralId == "tk-g41" && Value(g, "sanchen", 0) > 0 && Value(g, "wuku", 0) > 0 && Value(g, "miewu", s.Clock.TurnInstanceId) == 0) result.AddRange(Enumerable.Range(0, 8));
            return result.Distinct().ToArray();
        }
        public static int WenjiTradeRatio(ThreeKingdomsState s, string playerId, int realType)
        { var g = Active(s, playerId); return B4Enabled(s) && g?.GeneralId == "tk-g45" && realType >= 0 && realType < 8 && Value(g, "wenji:" + realType, s.Clock.TurnInstanceId) > 0 ? 2 : 0; }
        public static void OnB4VirtualProgressCompleted(IThreeKingdomsCardHost host, string playerId, B4VirtualUse use)
        {
            if (!B4Enabled(host?.State) || use == null || !Mark(host.State, "virtual-complete:" + use.SourceEventId)) return;
            if (use.RewardRandomCount > 0) Append(host.State, new B2SkillFrame { Id = Id(host.State, "reward"), PlayerId = playerId, Kind = "random", Remaining = use.RewardRandomCount, Started = true, SourceEventId = use.SourceEventId });
            Pump(host);
        }
        /// <summary>宿主先预验整项行动与实体牌费用，然后在同一原子提交中调用；不替宿主移牌。</summary>
        public static void CommitCardConversion(IThreeKingdomsCardHost host, string eventId, string playerId, int realType, int effectiveType)
        {
            var s = host.State; Require(B4Enabled(s) && realType != effectiveType && EffectiveCardTypes(s, playerId, realType).Contains(effectiveType), "该手牌不能用于所声明的牌种。");
            var g = Active(s, playerId); Require(host.GetHand(playerId)[realType] > 0, "转换的真实手牌不足。");
            if (!Mark(s, "conversion:" + eventId)) return;
            var skill = g.GeneralId + (g.GeneralId == "tk-g41" ? "-s3" : g.GeneralId == "tk-g44" ? "-s2" : "-s1");
            B4SkillHistoryEngine.Begin(s, eventId, playerId, skill, g.TenureId, eventId, "使用视为牌");
            if (g.GeneralId == "tk-g41") { Counter(g, "wuku", 0).Value--; Counter(g, "miewu", s.Clock.TurnInstanceId).Value = 1; s.Skills.TimedEffects=s.Skills.TimedEffects.Concat(new[]{new B2TimedEffect {Id=eventId,Kind="b4-conversion-reward",PlayerId=playerId,CreatedTurnId=s.Clock.TurnInstanceId}}).ToArray(); }
            B4SkillHistoryEngine.Complete(s, eventId, "按声明用途结算，实体类型不变");
        }
        public static void CompleteB4CardConversions(IThreeKingdomsCardHost host)
        {
            var s=host?.State;if(!B4Enabled(s))return;
            foreach(var e in s.Skills.TimedEffects.Where(e=>e.Kind=="b4-conversion-reward").ToArray())
            { s.Skills.TimedEffects=s.Skills.TimedEffects.Where(x=>x.Id!=e.Id).ToArray();Append(s,new B2SkillFrame{Id=Id(s,"reward"),Kind="random",PlayerId=e.PlayerId,Remaining=1,Started=true,SourceEventId=e.Id}); }
        }

        private static void AddB4Actions(IThreeKingdomsCardHost host, GeneralInstance g, List<B2SkillAction> actions)
        {
            AddResponseActions(host, g, actions);
            var s = host.State; if (!B4Enabled(s) || s.Clock.ActivePlayerId != g.OwnerPlayerId || s.Clock.Phase != ThreeKingdomsPhase.Play) return;
            var hand = host.GetHand(g.OwnerPlayerId); bool lossForbidden = IsB4HandLossForbidden(s, g.OwnerPlayerId);
            foreach (var target in s.SeatPlayerIds.Where(p => p != g.OwnerPlayerId))
            {
                if (g.GeneralId == "tk-g26" && !lossForbidden && hand.Sum() > 0 && Value(g, "xiashu", s.Clock.PhaseId) == 0) AddB4TargetAction(s, actions, "tk-g26-s1", "b4-xiashu", "下书：交出全部手牌给 " + target, target);
                if (g.GeneralId == "tk-g39" && hand.Sum() > 0) AddB4TargetAction(s, actions, "tk-g39-s1", "b4-quhu", "驱虎：声明并令 " + target + " 猜测", target);
                if (g.GeneralId == "tk-g45" && host.GetHand(target).Sum() > 0 && !IsProtected(s, target) && !IsB4HandLossForbidden(s, target)) AddB4TargetAction(s, actions, "tk-g45-s1", "b4-wenji", "问计：令 " + target + " 交给一张牌", target);
                if (g.GeneralId == "tk-g40" && Value(g, "daoshu", s.Clock.PhaseId) == 0 && host.GetHand(target).Sum() > 0 && !IsProtected(s, target) && !IsB4HandLossForbidden(s, target))
                    for (var type = 0; type < 8; type++) { var a = Action(s, "tk-g40-s1", "b4-daoshu", "盗书：猜" + CardNames[type] + "，抽取 " + target); a.Command.TargetPlayerIds = new[] { target }; a.Command.CardType = type; actions.Add(a); }
            }
            if (g.GeneralId == "tk-g39" && Value(g, "jieming", 0) == 0 && host is IThreeKingdomsB4Host b4 && hand.Sum() < b4.GetHandLimit(g.OwnerPlayerId) && !IsDrawBlocked(s, g.OwnerPlayerId))
                actions.Add(Action(s, "tk-g39-s2", "b4-jieming", "节命：补至手牌上限"));
            if (host is IThreeKingdomsB4Host virtualHost)
            {
                if (g.GeneralId == "tk-g41" && !lossForbidden && Value(g, "sanchen", 0) > 0 && Value(g, "wuku", 0) >= 3 && Value(g, "miewu", s.Clock.TurnInstanceId) == 0 && hand.Sum() > 0)
                    foreach (var use in virtualHost.GetVirtualProgressUses(g.OwnerPlayerId, "play")) foreach (int type in Enumerable.Range(0, 8).Where(t => hand[t] > 0))
                    { if (host is IThreeKingdomsB4VirtualCostHost costHost && !costHost.CanUseVirtualProgressAfterPayment(g.OwnerPlayerId, use, One(type))) continue; var a = Action(s, "tk-g41-s3", "b4-miewu-progress", "灭吴：弃" + CardNames[type] + "，" + use.Label); a.VirtualCardId=use.CardId; a.ResultCardType=use.CardType>=0?(int?)use.CardType:null; a.Command.ProgressCardId = use.OptionId; a.Command.CardType = type; actions.Add(a); }
                if (g.GeneralId == "tk-g52" && !lossForbidden && Value(g, "xiemu", s.Clock.PhaseId) == 0)
                {
                    var cards = host.GetProgressCards(g.OwnerPlayerId).Where(p => !p.PublicPoint).ToArray();
                    for (var first = 0; first < cards.Length; first++) for (var second = first + 1; second < cards.Length; second++)
                        if (cards[first].Color == 0 || cards[second].Color == 0)
                            foreach (var use in virtualHost.GetVirtualProgressUses(g.OwnerPlayerId, "play").Where(u => u.CardId == "ResourceMonopoly" || u.CardId == "TradeMonopoly"))
                            { var a = Action(s, "tk-g52-s2", "b4-xiemu", "协穆：" + use.Label); a.VirtualCardId=use.CardId; a.ResultCardType=use.CardType>=0?(int?)use.CardType:null; a.Command.ProgressCardId = use.OptionId; a.Command.ProgressCardIds = new[] { cards[first].Id, cards[second].Id }; actions.Add(a); }
                }
            }
        }
        private static void AddB4TargetAction(ThreeKingdomsState s, List<B2SkillAction> actions, string skill, string option, string label, string target)
        { var a = Action(s, skill, option, label); a.Command.TargetPlayerIds = new[] { target }; actions.Add(a); }
        private static B2SkillAction[] AnnotateB4Actions(IThreeKingdomsCardHost host, List<B2SkillAction> actions)
        {
            if (B4Enabled(host.State)) foreach (var a in actions)
            {
                var c = a.Command;
                var d=host.State.Skills.PendingDecision;var f=d==null?null:host.State.Skills.Frames.FirstOrDefault(x=>x.Id==d.FrameId);
                var owner=f?.PlayerId??host.State.Clock.ActivePlayerId;
                if (new[] { "b4-daoshu", "b4-select", "b4-give", "b4-miewu-progress" }.Contains(c.Option) && c.CardType >= 0 && c.CardType < 8)
                {
                    a.RealCardType = c.CardType; a.CardSelectionKind = c.Option == "b4-daoshu" ? "declare" : c.Option == "b4-select" ? "select" : "payment";
                    a.AvailableCount=c.Option=="b4-daoshu"?(int?)null:host.GetHand(owner)[c.CardType]-(c.Option=="b4-select"?f?.B4?.Selection[c.CardType]??0:0);
                }
                if((c.Option=="b4-select"||c.Option=="b4-selected")&&f?.B4!=null)c.Counts=Copy(f.B4.Selection);
            }
            return actions.Where(a => !B4Enabled(host.State) || !new[] { "gift", "discard", "exchange-hand", "exchange-progress" }.Contains(a.Command.Option) || !IsB4HandLossForbidden(host.State, host.State.Clock.ActivePlayerId)).ToArray();
        }
        private static bool ResolveB4Active(IThreeKingdomsCardHost host, GeneralInstance g, B2SkillCommand c)
        {
            if (ResolveResponseActive(host, g, c)) return true;
            if (c.Option == null || !c.Option.StartsWith("b4-", StringComparison.Ordinal)) return false;
            var s = host.State; var legal = new List<B2SkillAction>(); AddB4Actions(host, g, legal);
            Require(legal.Any(a => SameB4Action(a.Command, c)), "技能时机、目标、费用或次数无效。");
            var f = Frame(s, g, c.SkillId, c.Option, 0, null); f.Started = true; f.Targets = (string[])c.TargetPlayerIds.Clone(); f.B4 = new B4SkillPayload { OwnerPlayerId = g.OwnerPlayerId, DeclaredType = c.CardType };
            if (c.Option == "b4-xiashu") Counter(g, "xiashu", s.Clock.PhaseId).Value = 1;
            if (c.Option == "b4-jieming") { Counter(g, "jieming", 0).Value = 1; int count = ((IThreeKingdomsB4Host)host).GetHandLimit(g.OwnerPlayerId) - host.GetHand(g.OwnerPlayerId).Sum(); f.Remaining = count / 2; f.Step = count - f.Remaining; }
            if (c.Option == "b4-quhu") { int seat = Array.IndexOf(s.SeatPlayerIds, f.Targets[0]); f.B4.LockedTargets = new[] { s.SeatPlayerIds[(seat + s.SeatPlayerIds.Length - 1) % s.SeatPlayerIds.Length], s.SeatPlayerIds[(seat + 1) % s.SeatPlayerIds.Length] }; }
            if (c.Option == "b4-miewu-progress" || c.Option == "b4-xiemu")
            {
                f.B4.VirtualUse = ((IThreeKingdomsB4Host)host).GetVirtualProgressUses(g.OwnerPlayerId, "play").Single(u => u.OptionId == c.ProgressCardId);
                if (c.Option == "b4-miewu-progress") { Counter(g, "wuku", 0).Value -= 3; Counter(g, "miewu", s.Clock.TurnInstanceId).Value = 1; Append(s, f); B4Discard(host, g.OwnerPlayerId, One(c.CardType), f.Id + ":cost"); return true; }
                Counter(g, "xiemu", s.Clock.PhaseId).Value = 1;
                foreach (string id in c.ProgressCardIds) { var card = host.GetProgressCards(g.OwnerPlayerId).First(p => p.Id == id && !p.PublicPoint); host.RemoveProgress(g.OwnerPlayerId, id); host.ReturnProgress(card); }
                Counter(g, "lost-progress", s.Clock.TurnInstanceId).Value += 2;
            }
            Append(s, f); foreach (var target in f.Targets) Target(host, f.Id, g.OwnerPlayerId, target); return true;
        }
        private static bool SameB4Action(B2SkillCommand a, B2SkillCommand b) => a.SkillId == b.SkillId && a.Option == b.Option && a.CardType == b.CardType && a.ProgressCardId == b.ProgressCardId &&
            (a.ProgressCardIds ?? Array.Empty<string>()).SequenceEqual(b.ProgressCardIds ?? Array.Empty<string>()) && (a.TargetPlayerIds ?? Array.Empty<string>()).SequenceEqual(b.TargetPlayerIds ?? Array.Empty<string>());

        private static bool ProcessB4Frame(IThreeKingdomsCardHost host, B2SkillFrame f, out bool waiting)
        {
            if (ProcessResponseFrame(host, f, out waiting)) return true;
            waiting = false; if (!f.Kind.StartsWith("b4-", StringComparison.Ordinal)) return false;
            var s = host.State; Require(B4Enabled(s), "旧规则包不能结算 B4 技能。");
            if (f.Kind == "b4-zishu") { Remove(s, f); Gain(host, f.PlayerId, TakeRandom(host, f.PlayerId, 1), "tk-g52-s1", f.Id); return true; }
            if (f.Kind == "b4-sanchen") { var g = Active(s, f.PlayerId); if (g?.TenureId == f.TenureId && Value(g, "wuku", 0) == 3) Counter(g, "sanchen", 0).Value = 1; Remove(s, f); return true; }
            if (f.Kind == "b4-yajiao")
            {
                Remove(s, f); if (IsDrawBlocked(s, f.PlayerId)) return true;
                var bank = host.GetBank(); int total = bank.Sum(); if (total == 0) return true;
                int index = host.DrawRandom(total); int type = 0; while (index >= bank[type]) index -= bank[type++];
                if (type == f.B4.DeclaredType) { var one = One(type); host.RemoveBank(one); Gain(host, f.PlayerId, one, f.SkillId, f.Id); }
                return true;
            }
            if (f.Kind == "b4-daoshu")
            {
                var g = Active(s, f.PlayerId); var target = f.Targets[0];
                if (host.GetHand(target).Sum() == 0 || IsProtected(s, target)) { Remove(s, f); return true; }
                var card = RandomHand(host, target); Counter(g, "daoshu", s.Clock.PhaseId).Value = Array.IndexOf(card, 1) == f.B4.DeclaredType ? 0 : 1;
                B4SkillHistoryEngine.Complete(s, f.Id, "抽得" + CardNames[Array.IndexOf(card, 1)] + "；" + (Value(g, "daoshu", s.Clock.PhaseId) == 0 ? "猜中，返还次数" : "猜错，消耗本阶段次数")); Remove(s, f);
                B4Transfer(host, target, f.PlayerId, card, f.Id, f.SkillId); return true;
            }
            if (f.Kind == "b4-jieming")
            {
                if (f.Remaining > 0 && !IsDrawBlocked(s, f.PlayerId) && host.GetBank().Take(5).Sum() > 0) { Decide(s, f, "card", "节命：自选资源", "还可选择 " + f.Remaining + " 张资源。其余随机领取。"); waiting = true; return true; }
                Add(f.Cards, TakeRandom(host, f.PlayerId, f.Step)); Remove(s, f); Gain(host, f.PlayerId, f.Cards, f.SkillId, f.Id); return true;
            }
            if (f.Kind == "b4-miewu-progress" || f.Kind == "b4-xiemu")
            {
                var use = f.B4.VirtualUse; use.SourceSkillId = f.SkillId; use.SourceEventId = f.Id; use.RewardRandomCount = f.Kind == "b4-miewu-progress" ? 1 : 0; Remove(s, f);
                ((IThreeKingdomsB4Host)host).UseVirtualProgress(f.PlayerId, use);

                return true;
            }
            if (f.Kind == "b4-xiashu")
            {
                if (f.Step == 0) { f.Step = 1; B4Transfer(host, f.PlayerId, f.Targets[0], host.GetHand(f.PlayerId), f.Id + ":give", f.SkillId, true); return true; }
                if (f.Step == 1) { f.PlayerId = f.Targets[0]; Decide(s, f, "b4-reveal", "下书：选择亮出的手牌", "可亮出零张、部分或全部；未亮出的牌仍保密。"); waiting = true; return true; }
                f.PlayerId = f.B4.OwnerPlayerId; Decide(s, f, "b4-pile", "下书：选择一堆", "选择亮出的手牌或未亮出的手牌。"); waiting = true; return true;
            }
            if (f.Kind == "b4-quhu")
            {
                if (f.Step == 0) { Decide(s, f, "b4-subset", "驱虎：暗选手牌", "至少选择一张手牌，然后声明小麦数量。"); waiting = true; return true; }
                if (f.Step == 1) { Decide(s, f, "b4-declare", "驱虎：声明小麦数量", "所选牌尚未公开；声明其中真实小麦的数量。"); waiting = true; return true; }
                f.PlayerId = f.Targets[0]; Decide(s, f, "b4-guess", "驱虎：猜测声明真假", "对方暗选 " + f.B4.Selection.Sum() + " 张手牌，声明其中有 " + f.B4.DeclaredType + " 张小麦。"); waiting = true; return true;
            }
            if (f.Kind == "b4-wenji") { f.PlayerId = f.Targets[0]; if (host.GetHand(f.PlayerId).Sum() == 0 || IsProtected(s, f.PlayerId)) { Remove(s, f); return true; } Decide(s, f, "b4-give", "问计：交给一张手牌", "选择一张真实资源或商品交给 " + f.B4.OwnerPlayerId + "。"); waiting = true; return true; }
            throw new InvalidOperationException("未知 B4 效果帧：" + f.Kind);
        }
        private static bool AddB4DecisionActions(IThreeKingdomsCardHost host, B2SkillFrame f, B2SkillDecision d, List<B2SkillAction> actions)
        {
            if (d.Kind == "b4-order") { foreach (var item in B4OrderCandidates(host.State, f)) { var action = Action(host.State, f.SkillId, "b4-order", "先结算" + Name(item.SkillId), d); action.Command.ProgressCardId = item.Id; actions.Add(action); } return true; }
            if (AddResponseDecisionActions(host, f, d, actions)) return true;
            if (!d.Kind.StartsWith("b4-", StringComparison.Ordinal)) return false;
            var s = host.State;
            if (d.Kind == "b4-reveal" || d.Kind == "b4-subset")
            {
                var hand = host.GetHand(f.PlayerId); for (var type = 0; type < 8; type++) if (f.B4.Selection[type] < hand[type]) { var a = Action(s, f.SkillId, "b4-select", "选择" + CardNames[type], d); a.Command.CardType = type; actions.Add(a); }
                if (d.Kind == "b4-reveal" || f.B4.Selection.Sum() > 0) actions.Add(Action(s, f.SkillId, "b4-selected", "确认所选牌", d));
            }
            else if (d.Kind == "b4-declare") for (var number = 0; number <= f.B4.Selection.Sum(); number++) { var a = Action(s, f.SkillId, "b4-declare", "声明 " + number + " 张小麦", d); a.Command.CardType = number; actions.Add(a); }
            else if (d.Kind == "b4-guess") { actions.Add(Action(s, f.SkillId, "b4-true", "猜声明为真", d)); actions.Add(Action(s, f.SkillId, "b4-false", "猜声明为假", d)); }
            else if (d.Kind == "b4-pile") { actions.Add(Action(s, f.SkillId, "b4-revealed", "获得亮出的 " + f.B4.Selection.Sum() + " 张", d)); actions.Add(Action(s, f.SkillId, "b4-hidden", "获得未亮出的 " + (host.GetHand(f.Targets[0]).Sum() - f.B4.Selection.Sum()) + " 张", d)); }
            else if (d.Kind == "b4-give") foreach (var type in Enumerable.Range(0, 8).Where(t => host.GetHand(f.PlayerId)[t] > 0)) { var a = Action(s, f.SkillId, "b4-give", "交给" + CardNames[type], d); a.Command.CardType = type; actions.Add(a); }
            return true;
        }
        private static bool ResolveB4Decision(IThreeKingdomsCardHost host, B2SkillFrame f, B2SkillDecision d, B2SkillCommand c)
        {
            if (d.Kind == "b4-order")
            {
                var s0=host.State;var chosen=B4OrderCandidates(s0,f).FirstOrDefault(x=>x.Id==c.ProgressCardId);
                Require(c.Option=="b4-order"&&chosen!=null,"不能选择其他层或其他玩家的技能帧。");chosen.B4OrderChosen=true;
                s0.Skills.Frames=new[]{chosen}.Concat(s0.Skills.Frames.Where(x=>x.Id!=chosen.Id)).ToArray();return true;
            }
            if (ResolveResponseDecision(host, f, d, c)) return true;
            if (!d.Kind.StartsWith("b4-", StringComparison.Ordinal)) return false;
            var s = host.State; var legal = new List<B2SkillAction>(); AddB4DecisionActions(host, f, d, legal); Require(legal.Any(a => SameB4Action(a.Command, c)), "该技能选择无效。");
            if (c.Option == "b4-select") { f.B4.Selection[c.CardType]++; return true; }
            if (c.Option == "b4-selected") { Require(Contains(host.GetHand(f.PlayerId), f.B4.Selection), "选择的真实手牌已不在手中。"); if (d.Kind == "b4-reveal") { s.Skills.RevealedCards = Copy(f.B4.Selection); f.Step = 2; } else f.Step = 1; return true; }
            if (c.Option == "b4-declare") { f.B4.DeclaredType = c.CardType; f.Step = 2; return true; }
            if (d.Kind == "b4-guess")
            {
                bool truth = f.B4.Selection[3] == f.B4.DeclaredType; bool won = (c.Option == "b4-true") != truth; Remove(s, f); s.Skills.RevealedCards = Copy(f.B4.Selection);
                if (won) foreach (var target in f.B4.LockedTargets) B4DiscardRandom(host, target, 1, f.Id + ":neighbor:" + target);
                else B4DiscardRandom(host, f.B4.OwnerPlayerId, 2, f.Id + ":loss"); return true;
            }
            if (d.Kind == "b4-pile")
            {
                var take = c.Option == "b4-revealed" ? Copy(f.B4.Selection) : host.GetHand(f.Targets[0]); if (c.Option == "b4-hidden") Subtract(take, f.B4.Selection);
                Require(Contains(host.GetHand(f.Targets[0]), take), "下书选择堆已变化。"); Remove(s, f); s.Skills.RevealedCards = new int[8]; B4Transfer(host, f.Targets[0], f.B4.OwnerPlayerId, take, f.Id + ":take", f.SkillId); return true;
            }
            if (d.Kind == "b4-give")
            {
                var owner = f.B4.OwnerPlayerId; var g = Active(s, owner); if (g?.TenureId == f.TenureId) Counter(g, "wenji:" + c.CardType, s.Clock.TurnInstanceId).Value = 1;
                Remove(s, f); B4Transfer(host, f.PlayerId, owner, One(c.CardType), f.Id + ":give", f.SkillId, true); return true;
            }
            return true;
        }

        private static int[] RandomHand(IThreeKingdomsCardHost host, string player)
        { var hand = host.GetHand(player); if (hand.Sum() == 0) return new int[8]; int random = host.DrawRandom(hand.Sum()); for (int type = 0; type < 8; type++) { if (random < hand[type]) return One(type); random -= hand[type]; } throw new InvalidOperationException("真实手牌随机区间错误。"); }
        private static B2SkillFrame[] B4OrderCandidates(ThreeKingdomsState s,B2SkillFrame f) => s.Skills.Frames.TakeWhile(x=>!x.Started&&x.PlayerId==f.PlayerId&&x.SourceEventId==f.SourceEventId&&x.TenureId==f.TenureId)
            .Where(x=>!x.B4OrderChosen&&x.SkillId!=null).GroupBy(x=>x.SkillId).Select(g=>g.First()).ToArray();
        private static bool TryB4Order(ThreeKingdomsState s,B2SkillFrame f)
        {
            if(!B4Enabled(s)||f.Started||f.B4OrderChosen||f.SourceEventId==null||B4OrderCandidates(s,f).Length<2)return false;
            Decide(s,f,"b4-order","选择同层技能顺序","这些技能属于同一事件与同一任次。请选择先结算的技能。");return true;
        }
        private static void B4Transfer(IThreeKingdomsCardHost host, string from, string to, int[] cards, string eventId, string skill, bool given=false)
        {
            if (cards.Sum() == 0 || IsB4HandLossForbidden(host.State, from)) return; Require(Contains(host.GetHand(from), cards), "转移牌不足。"); int before = host.GetHand(from).Sum(), targetBefore = host.GetHand(to).Sum();
            host.RemoveHand(from, cards); host.AddHand(to, cards);
            RecordCards(host, eventId, new[] { new B2HandChange { PlayerId = from, BeforeCount = before, AfterCount = before - cards.Sum(), LostCount = cards.Sum(), GivenCount = given?cards.Sum():0, LostCards = Copy(cards), SourceSkillId = skill },
                new B2HandChange { PlayerId = to, BeforeCount = targetBefore, AfterCount = targetBefore + cards.Sum(), GainedCards = Copy(cards), SourcePlayerId = from, SourceSkillId = skill } });
        }
        private static void B4Discard(IThreeKingdomsCardHost host, string player, int[] cards, string eventId)
        {
            if (cards.Sum() == 0 || IsB4HandLossForbidden(host.State, player)) return; int before = host.GetHand(player).Sum(); Require(Contains(host.GetHand(player), cards), "弃牌费用不足。");
            host.RemoveHand(player, cards); host.AddBank(cards); HoldReturnedCards(host, player, "discard", eventId, cards);
            RecordCards(host, eventId, new[] { new B2HandChange { PlayerId = player, BeforeCount = before, AfterCount = before - cards.Sum(), LostCount = cards.Sum(), LostCards = Copy(cards) } });
        }
        private static void B4DiscardRandom(IThreeKingdomsCardHost host, string player, int count, string eventId)
        {
            if (IsProtected(host.State, player) || IsB4HandLossForbidden(host.State, player)) return; var cards = new int[8]; var hand = host.GetHand(player);
            for (int i = 0; i < count && hand.Sum() > 0; i++) { int random = host.DrawRandom(hand.Sum()), type = 0; while (random >= hand[type]) random -= hand[type++]; cards[type]++; hand[type]--; }
            B4Discard(host, player, cards, eventId);
        }
    }
}
