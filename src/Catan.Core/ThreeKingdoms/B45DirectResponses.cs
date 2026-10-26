using System;
using System.Collections.Generic;
using System.Linq;

namespace Catan.Core.ThreeKingdoms
{
    // B4.5只合并不涉及新信息揭示的选择；提交前不取牌、不掷骰、不占次数。
    public static partial class B2SkillEngine
    {
        private static bool DirectResponses(ThreeKingdomsState state) => GeneralCatalog.UsesDirectRules(state.RulesVersion);
        private static int BatchChoiceCount(IThreeKingdomsCardHost host, B2SkillFrame frame)
            => IsDrawBlocked(host.State, frame.PlayerId) ? 0 : Math.Min(frame.Remaining, ChoiceTypes(frame).Where(t=>CanGainB6(host.State,frame.PlayerId,t)).Sum(t => host.GetBank()[t]));
        private static B2SkillFrame OptionalChoiceFrame(B2SkillFrame frame)
        {
            if (frame.Kind != "lianpo") return frame;
            return new B2SkillFrame { Kind = "mixed", PlayerId = frame.PlayerId, Remaining = frame.Remaining };
        }
        private static bool AddB45OptionalActions(IThreeKingdomsCardHost host, B2SkillFrame frame, B2SkillDecision decision, List<B2SkillAction> actions)
        {
            var state = host.State;
            if (!DirectResponses(state)) return false;
            if (frame.Kind == "zhenggu" || frame.Kind == "response-jieying")
            {
                foreach (var target in state.SeatPlayerIds.Where(p => p != frame.PlayerId))
                {
                    var action = Action(state, frame.SkillId, "b45-optional-target", "选择目标并响应", decision);
                    action.Command.TargetPlayerIds = new[] { target }; actions.Add(action);
                }
            }
            else if (frame.Kind == "jushou" || frame.Kind == "lianpo")
            {
                var choice = OptionalChoiceFrame(frame);
                if (BatchChoiceCount(host, choice) == 0) actions.Add(Action(state, frame.SkillId, "accept", "发动", decision));
                else AddB45BatchCardActions(host, choice, frame, decision, "b45-optional-cards", actions);
            }
            else if (frame.Kind == "response-qice")
            {
                var direct = new B2SkillDecision { Kind = "response-qice-use", PlayerId = decision.PlayerId,
                    DecisionId = decision.DecisionId, WindowId = decision.WindowId, FrameId = decision.FrameId };
                AddResponseDecisionActions(host, frame, direct, actions);
                foreach (var action in actions) action.Command.Option = "b45-optional-qice";
            }
            else return false;
            actions.Add(Action(state, frame.SkillId, "skip", "放弃本次", decision));
            return true;
        }
        private static void AddB45BatchCardActions(IThreeKingdomsCardHost host, B2SkillFrame choice, B2SkillFrame frame,
            B2SkillDecision decision, string option, List<B2SkillAction> actions)
        {
            var bank = host.GetBank(); var count = BatchChoiceCount(host, choice);
            foreach (var type in ChoiceTypes(choice).Where(t => bank[t] > 0 && CanGainB6(host.State,choice.PlayerId,t)))
            {
                var action = Action(host.State, frame.SkillId, option, "选择" + count + "张银行牌", decision);
                action.Command.CardType = type; action.RealCardType = type; action.CardSelectionKind = "bank";
                action.AvailableCount = bank[type]; action.RequiredCardCount = count;
                // 给AI一个完整合法批次；UI可将整批Counts替换为自己的选择。
                int remaining = count;
                foreach (var t in new[] { type }.Concat(ChoiceTypes(choice).Where(t => t != type && CanGainB6(host.State,choice.PlayerId,t))))
                { int take = Math.Min(remaining, bank[t]); action.Command.Counts[t] = take; remaining -= take; }
                actions.Add(action);
            }
        }
        private static void ValidateB45BatchCards(IThreeKingdomsCardHost host, B2SkillFrame frame, int[] cards)
        {
            var bank = host.GetBank(); var allowed = ChoiceTypes(frame);
            Require(cards != null && cards.Length == 8 && cards.All(n => n >= 0)
                && Enumerable.Range(0, 8).All(t => cards[t] <= bank[t] && (cards[t] == 0 || allowed.Contains(t) && CanGainB6(host.State,frame.PlayerId,t)))
                && cards.Sum() == BatchChoiceCount(host, frame), "银行选牌数量、类型或库存已变化。");
        }
        private static bool ResolveB45Optional(IThreeKingdomsCardHost host, B2SkillFrame frame, B2SkillDecision decision, B2SkillCommand command)
        {
            var state = host.State;
            if (!DirectResponses(state) || command.Option?.StartsWith("b45-optional-", StringComparison.Ordinal) != true) return false;
            var actions = new List<B2SkillAction>();
            Require(AddB45OptionalActions(host, frame, decision, actions), "当前响应不支持直接选择。");
            Require(actions.Any(a => command.Option == "b45-optional-cards" ? a.Command.Option == command.Option : SameB4Action(a.Command, command)), "响应的目标或用途无效。");
            if (command.Option == "b45-optional-cards") ValidateB45BatchCards(host, OptionalChoiceFrame(frame), command.Counts);
            ValidateTimeOptional(host, frame);
            frame.Optional = false; frame.Started = true;
            B4SkillHistoryEngine.Begin(state, frame.Id, frame.B4?.OwnerPlayerId ?? frame.PlayerId, frame.SkillId, frame.TenureId, frame.SourceEventId);
            if (command.Option == "b45-optional-cards")
            {
                if (frame.Kind == "lianpo") ProcessTimeFrame(host, frame, out _);
                host.RemoveBank(command.Counts); Add(frame.Cards, command.Counts); frame.Remaining -= command.Counts.Sum();
            }
            else if (command.Option == "b45-optional-target")
            {
                frame.Targets = (string[])command.TargetPlayerIds.Clone();
                if (frame.Kind == "zhenggu") { frame.Step = 1; Target(host, frame.Id, frame.PlayerId, frame.Targets[0]); }
                else
                {
                    B4SkillHistoryEngine.Complete(state, frame.Id, "指定 " + frame.Targets[0] + " 为劫营目标");
                    Remove(state, frame); AddTimed(state, "response-jieying-pending", frame.PlayerId, frame.Targets[0], 0);
                }
            }
            else
            {
                frame.B4.VirtualUse = ((IThreeKingdomsB4Host)host).GetVirtualProgressUses(frame.PlayerId, "prepare").Single(u => u.OptionId == command.ProgressCardId);
                frame.Step = 1; B4Discard(host, frame.PlayerId, host.GetHand(frame.PlayerId), frame.Id + ":cost");
            }
            return true;
        }
    }
}
