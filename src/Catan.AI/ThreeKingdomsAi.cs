using System;
using System.Linq;
using Catan.Core.ThreeKingdoms;

namespace Catan.AI
{
    // 只读取所属席位的投影选项；不探测牌堆、随机状态或其他玩家的私密选择。
    internal static class ThreeKingdomsAi
    {
        internal static B2SkillCommand Choose(ThreeKingdomsView view, int[] hand, int[] desired, Func<string, int> handCount)
        {
            if (view == null || !view.SkillsEnabled) return null;
            bool required = view.SkillDecision != null;
            var choices = view.SkillActions ?? Array.Empty<B2SkillAction>();
            double Value(B2SkillCommand c)
            {
                if (!required)
                {
                    // 可支付的修复恢复既有资产，完成后该合法动作消失，不构成循环。
                    if (c.Option == "b5-repair") return 15;
                    // 仁德不限次是规则；本策略暂不主动赠牌，避免无限赠予循环。
                    if (c.Option == "gift" || c.Option == "exchange-progress") return double.NegativeInfinity;
                    if (c.Option == "discard") return hand.Sum() == 1 ? 15 : double.NegativeInfinity;
                    if (c.Option == "exchange-hand")
                    {
                        var counts = c.Counts ?? Array.Empty<int>();
                        return counts.Length == 8 && Enumerable.Range(0, 8).All(i => counts[i] <= Math.Max(0, hand[i] - desired[i]))
                            ? counts.Sum() : double.NegativeInfinity;
                    }
                    return double.NegativeInfinity;
                }
                switch (c.Option)
                {
                    // 尚未建立地块收益评估时保留真实牌和限定次数；允许人工完整操作。
                    case "modify": return -200;
                    case "fixed": return -200;
                    case "skip": return -100;
                    case "done": return -10;
                    case "pass": return 0;
                    case "card": return c.CardType >= 0 && c.CardType < 8 ? Math.Max(0, desired[c.CardType] - hand[c.CardType]) * 20 + 10.0 / (1 + hand[c.CardType]) : 1;
                    case "target": return (c.TargetPlayerIds ?? Array.Empty<string>()).Sum(handCount);
                    case "raid": return (c.TargetPlayerIds ?? Array.Empty<string>()).Length * 10;
                    case "color": return c.CardType == 2 ? 12 : 10;
                    default: return 5;
                }
            }
            var best = choices.Where(a => a?.Command != null).Select(a => new { a.Command, Score = Value(a.Command) })
                .Where(a => !double.IsNegativeInfinity(a.Score)).OrderByDescending(a => a.Score).FirstOrDefault();
            if (best == null) return null;
            var selected = Copy(best.Command);
            if (!required && selected.Option == "exchange-hand")
            {
                // 次数按整批消耗，把暂时无用的牌一并置换；不碰当前建设目标需要的牌。
                selected.Counts = new int[8]; int remaining = 4;
                foreach (int type in Enumerable.Range(0, 8).OrderByDescending(i => hand[i] - desired[i]))
                {
                    int count = Math.Min(remaining, Math.Max(0, hand[type] - desired[type]));
                    selected.Counts[type] = count; remaining -= count;
                }
            }
            return selected;
        }

        internal static B2SkillCommand Copy(B2SkillCommand command) => command == null ? null : new B2SkillCommand
        {
            SkillId = command.SkillId, Option = command.Option, TargetPlayerIds = command.TargetPlayerIds?.ToArray(),
            Counts = command.Counts?.ToArray(), TileIds = command.TileIds?.ToArray(), ProgressCardId = command.ProgressCardId,
            DecisionId = command.DecisionId, WindowId = command.WindowId, CardType = command.CardType,
            EffectiveCardType = command.EffectiveCardType, SecondaryCounts = command.SecondaryCounts?.ToArray(), ProgressCardIds = command.ProgressCardIds?.ToArray()
        };
    }
}
