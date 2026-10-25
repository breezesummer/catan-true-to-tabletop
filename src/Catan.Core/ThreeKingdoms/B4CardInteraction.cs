using System;
using System.Linq;

namespace Catan.Core.ThreeKingdoms
{
    // 只消费有权读取的合法动作，显示层不从权威手牌猜测目标牌种。
    public static class B4CardInteraction
    {
        public static int[] ShownCards(B2SkillAction action,int[] revealedCards=null)
        {
            var command=action?.Command;
            if(command?.Option=="b4-revealed"&&revealedCards?.Length==8)return (int[])revealedCards.Clone();
            if(command?.Option=="raid"&&command.Counts?.Length==8)return (int[])command.Counts.Clone();
            if(command?.Option=="b5-repair"&&command.Counts?.Length==8)return (int[])command.Counts.Clone();
            var cards=new int[8];int type=RealCardType(action);
            if(type>=0&&type<8)cards[type]=action?.CardSelectionKind=="select"?1:Math.Max(1,command?.Counts?.ElementAtOrDefault(type)??0);
            return cards;
        }
        public static int[] ExpandCards(int[] counts) => counts==null?Array.Empty<int>():Enumerable.Range(0,Math.Min(8,counts.Length)).SelectMany(type=>Enumerable.Repeat(type,Math.Max(0,counts[type]))).ToArray();
        public static int[] PreserveDeclarations(int[] oldReal,int[] oldUses,int[] real)
        {
            var used=new bool[oldReal?.Length??0];
            return (real??Array.Empty<int>()).Select(type=>{
                int index=Enumerable.Range(0,used.Length).Where(i=>!used[i]&&oldReal[i]==type&&i<(oldUses?.Length??0)).DefaultIfEmpty(-1).First();
                if(index<0)return type;used[index]=true;return oldUses[index]>=0&&oldUses[index]<8?oldUses[index]:type;
            }).ToArray();
        }
        public static int RealCardType(B2SkillAction action) => action?.RealCardType ?? RealCardType(action?.Command);
        public static int RealCardType(B2SkillCommand command)
        {
            if (command == null) return -1;
            if (new[] { "card", "modify" }.Contains(command.Option)) return command.CardType >= 0 && command.CardType < 8 ? command.CardType : -1;
            if (new[] { "gift", "discard", "exchange-hand", "luoying" }.Contains(command.Option))
            {
                var types = Enumerable.Range(0, Math.Min(8, command.Counts?.Length ?? 0)).Where(i => command.Counts[i] > 0).ToArray();
                return types.Length == 1 ? types[0] : -1;
            }
            return -1;
        }
        public static int[] PaymentLimits(B2SkillAction[] actions, B2SkillCommand template, int[] hand)
        {
            var limits = new int[8];
            foreach (var action in actions ?? Array.Empty<B2SkillAction>())
            {
                var command = action?.Command;
                if (command == null || command.SkillId != template.SkillId || command.Option != template.Option ||
                    !(command.TargetPlayerIds ?? Array.Empty<string>()).SequenceEqual(template.TargetPlayerIds ?? Array.Empty<string>())) continue;
                var type = RealCardType(command);
                if (type >= 0 && type < hand.Length) limits[type] = Math.Max(0, hand[type]);
            }
            return limits;
        }
        public static bool CanSubmit(int[] selected, int[] limits, int maximum, int minimum=1) => selected?.Length == 8 && limits?.Length == 8 &&
            selected.Sum() >= minimum && selected.Sum() <= maximum && Enumerable.Range(0, 8).All(i => selected[i] >= 0 && selected[i] <= limits[i]);
        public static bool Change(int[] selected, int type, int delta, int[] limits, int maximum)
        {
            if (selected?.Length != 8 || limits?.Length != 8 || type < 0 || type >= 8 || delta == 0) return false;
            var next = selected[type] + Math.Sign(delta);
            if (next < 0 || next > limits[type] || selected.Sum() + Math.Sign(delta) > maximum) return false;
            selected[type] = next; return true;
        }
    }
}
