using System;
using System.Linq;

namespace Catan.Core.ThreeKingdoms
{
    public static class B4PaymentAllocator
    {
        // 行是真实牌，列是本次用途；选中牌只改变支付选择，绝不改变实体供应。
        public static int[] Allocate(ThreeKingdomsState state,string player,int[] hand,int[] cost,int[] selected=null)
        {
            if(hand==null||cost==null||hand.Length!=8||cost.Length!=8||hand.Any(n=>n<0)||cost.Any(n=>n<0)) return null;
            if(selected!=null&&(selected.Length!=8||selected.Any(n=>n<0)||selected.Sum()!=cost.Sum()||Enumerable.Range(0,8).Any(i=>selected[i]>hand[i]))) return null;
            var stock=(int[])(selected??hand).Clone(); var matrix=new int[64];
            var types=Enumerable.Range(0,8).Select(t=>B2SkillEngine.EffectiveCardTypes(state,player,t)).ToArray();
            bool single=state.Generals.Any(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId==player&&g.GeneralId=="tk-g41");
            var needs=Enumerable.Range(0,8).SelectMany(t=>Enumerable.Repeat(t,cost[t]))
                .OrderBy(t=>Enumerable.Range(0,8).Count(real=>stock[real]>0&&types[real].Contains(t))).ToArray();
            bool Match(int index,int converted)
            {
                if(index==needs.Length) return true; int effective=needs[index];
                foreach(var real in Enumerable.Range(0,8).OrderBy(t=>t==effective?0:1))
                {
                    if(stock[real]<=0||!types[real].Contains(effective)||(single&&converted>0&&real!=effective)) continue;
                    stock[real]--;matrix[real*8+effective]++;
                    if(Match(index+1,converted+(real!=effective?1:0))) return true;
                    stock[real]++;matrix[real*8+effective]--;
                }
                return false;
            }
            return Match(0,0)?matrix:null;
        }
    }
}
