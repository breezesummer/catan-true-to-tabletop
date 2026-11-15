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
            if (view.SkillDecision?.Kind == "b4-reveal")
            {
                var confirm = choices.FirstOrDefault(a => a?.Command?.Option == "b4-selected");
                if (confirm != null)
                {
                    var pile = SplitXiashu(hand, desired);
                    var reveal = Copy(confirm.Command);
                    if (GeneralCatalog.UsesDirectPackage(view.PackageVersion))
                    {
                        reveal.Option = "b45-selected"; reveal.Counts = pile; return reveal;
                    }
                    // 旧包逐张提交；依据已投影的选中数量继续，读档后也不会选到整手。
                    var next = choices.FirstOrDefault(a => a?.Command?.Option == "b4-select" &&
                        a.Command.CardType >= 0 && a.Command.CardType < 8 &&
                        a.Command.Counts[a.Command.CardType] < pile[a.Command.CardType]);
                    return Copy(next?.Command ?? confirm.Command);
                }
            }
            if (view.SkillDecision?.Kind == "b4-pile")
            {
                // 暗堆只使用公开张数估值，绝不读取对方私密牌种。
                var pile = choices.Where(a => a?.Command != null &&
                    (a.Command.Option == "b4-revealed" || a.Command.Option == "b4-hidden"))
                    .OrderByDescending(a => a.Command.Option == "b4-revealed"
                        ? HandValue(view.RevealedSkillCards, hand, desired)
                        : (a.AvailableCount ?? 0) * 1.75).FirstOrDefault();
                if (pile != null) return Copy(pile.Command);
            }
            double Value(B2SkillCommand c)
            {
                if (!required)
                {
                    // 可支付的修复恢复既有资产，完成后该合法动作消失，不构成循环。
                    if (c.Option == "b5-repair") return 15;
                    if(GeneralCatalog.UsesB6Package(view.PackageVersion))
                    {
                        if(c.Option=="seat-jinglue")return 14;
                        if(c.Option=="seat-fuqi")return 8;
                        if(c.Option=="map-tamo"||c.Option=="seat-baiyi")return 2;
                    }
                    // 仁德不限次是规则；本策略暂不主动赠牌，避免无限赠予循环。
                    if (c.Option == "gift" || c.Option == "exchange-progress") return double.NegativeInfinity;
                    if (c.Option == "discard") return hand.Sum() == 1 ? 15 : double.NegativeInfinity;
                    if (c.Option == "exchange-hand")
                    {
                        var counts = c.Counts ?? Array.Empty<int>();
                        return counts.Length == 8 && Enumerable.Range(0, 8).All(i => counts[i] <= Math.Max(0, hand[i] - desired[i]))
                            ? counts.Sum() : double.NegativeInfinity;
                    }
                    // B4.5没有首次询问；AI同样从合法投影主动选择有限次数的技能。
                    // 不为无限次赠牌、驱虎或未知效果构造自动循环。
                    if (GeneralCatalog.UsesDirectPackage(view.PackageVersion))
                    {
                        if (c.Option == "b4-jieming") return 20;
                        if (c.Option == "b4-xiashu")
                        {
                            // 预计对手合理分堆；计入交出全部手牌和失去建设材料的代价。
                            int other = (c.TargetPlayerIds ?? Array.Empty<string>()).Sum(handCount);
                            double gain = (other + hand.Sum()) * .5 * 1.75 - HandValue(hand, new int[8], desired);
                            return gain > 0 ? gain : double.NegativeInfinity;
                        }
                        if (c.Option == "response-jixi") return 12;
                        if (c.Option == "b4-daoshu") return c.CardType >= 0 && c.CardType < 8 ? Math.Max(0, desired[c.CardType] - hand[c.CardType]) + 4 : 1;
                    }
                    return double.NegativeInfinity;
                }
                switch (c.Option)
                {
                    // 尚未建立地块收益评估时保留真实牌和限定次数；允许人工完整操作。
                    case "seat-cancel": return -50;
                    case "alliance-b6-reject": return -40;
                    case "alliance-b6-reset": return -50;
                    case "alliance-b6-offer": return 20;
                    case "seat-fuqi-submit": return c.SecondaryCounts?.Sum()==2?30:1;
                    case "seat-jinglue-take":
                    case "seat-fuqi-request": return c.CardType>=0&&c.CardType<8?Math.Max(0,desired[c.CardType]-hand[c.CardType])*20+6:5;
                    case "modify": return -200;
                    case "fixed": return -200;
                    case "discard": return hand.Sum() == 1 ? 15 : -150;
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

        private static double HandValue(int[] cards, int[] held, int[] desired)
        {
            double value = 0;
            for (int type = 0; type < 8; type++)
                value += cards[type] + 2 * Math.Min(cards[type], Math.Max(0, desired[type] - held[type]));
            return value;
        }

        private static int[] SplitXiashu(int[] hand, int[] desired)
        {
            // 同种牌先均分，再把余牌按建设价值放到较弱的一堆，保护最差情况下的剩余收益。
            var shown = hand.Select(n => n / 2).ToArray();
            var hidden = (int[])shown.Clone();
            foreach (int type in Enumerable.Range(0, 8).Where(i => hand[i] % 2 != 0)
                .OrderByDescending(i => desired[i] > shown[i] ? 3 : 1))
            {
                double left = HandValue(shown, new int[8], desired), right = HandValue(hidden, new int[8], desired);
                var destination = left < right || left == right && shown.Sum() <= hidden.Sum() ? shown : hidden;
                destination[type]++;
            }
            return shown;
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
