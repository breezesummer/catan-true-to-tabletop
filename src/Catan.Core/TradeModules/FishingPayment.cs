using System;
using System.Linq;

namespace Catan.Core.TradeModules
{
    /// <summary>只使用授权筹码，最多枚举127种组合；最少浪费、再最少枚数、最后稳定ID。</summary>
    public static class FishingPayment
    {
        public static string[] Select(FishTokenView[] tokens, FishAction action)
        {
            var sorted = (tokens ?? Array.Empty<FishTokenView>()).OrderBy(t => t.Id, StringComparer.Ordinal).Take(FishingRules.HoldingLimit).ToArray();
            string[] best = Array.Empty<string>(); int value = int.MaxValue;
            for(int mask=1;mask<(1<<sorted.Length);mask++)
            {
                var chosen=sorted.Where((t,i)=>(mask&(1<<i))!=0).ToArray(); int sum=chosen.Sum(t=>t.Value);
                if(sum<FishingRules.Cost(action) || sum>value || sum==value && chosen.Length>=best.Length) continue;
                best=chosen.Select(t=>t.Id).ToArray();value=sum;
            }
            return best;
        }
    }
}
