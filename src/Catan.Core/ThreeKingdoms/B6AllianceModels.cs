using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Catan.Core.ThreeKingdoms
{
    [DataContract] public sealed class B6AllianceSource
    {
        [DataMember(Order=1)] public string SourcePlayerId { get; set; }
        [DataMember(Order=2)] public string TenureId { get; set; }
        [DataMember(Order=3)] public string[] Members { get; set; } = Array.Empty<string>();
    }
    [DataContract] public sealed class B6AllianceState
    {
        [DataMember(Order=1)] public B6AllianceSource[] Sources { get; set; } = Array.Empty<B6AllianceSource>();
        [DataMember(Order=2)] public string[] WinnerPlayerIds { get; set; } = Array.Empty<string>();
        [DataMember(Order=3)] public bool KnightsNeedHomeCheck { get; set; }
        [DataMember(Order=4)] public string[] ReturnHomePlayerIds { get; set; } = Array.Empty<string>();
    }
    public static class B6AllianceRules
    {
        public static bool Enabled(ThreeKingdomsState s) => s?.RulesVersion == GeneralCatalog.B6RulesVersion;
        public static string[] Members(ThreeKingdomsState s,string player)
        {
            if(!Enabled(s)||s.AllianceB6==null)return new[]{player};
            var members=new HashSet<string>{player}; bool changed;
            do { changed=false;foreach(var source in s.AllianceB6.Sources)
                if(source.Members.Any(members.Contains))foreach(var member in source.Members)changed|=members.Add(member);
            } while(changed);
            return s.SeatPlayerIds.Where(members.Contains).ToArray();
        }
        public static bool Allies(ThreeKingdomsState s,string first,string second) => first==second || Members(s,first).Contains(second);
        public static bool CanAttack(ThreeKingdomsState s,string source,string target) => source==target || !Allies(s,source,target);
        public static bool CanTrade(ThreeKingdomsState s,string first,string second) => !Enabled(s)||s.AllianceB6==null||s.AllianceB6.Sources.Length==0||Allies(s,first,second);
        public static bool CanMerge(ThreeKingdomsState s,string leader,string target) => Members(s,leader).Union(Members(s,target)).Count()<=1+s.SeatPlayerIds.Length/2;
        public static int ProgressMultiplier(ThreeKingdomsState s,string source,string target) => Enabled(s)&&source!=target&&!Allies(s,source,target)&&
            s.Generals.Any(g=>g.OwnerPlayerId==source&&g.GeneralId=="tk-g50"&&g.Region==GeneralRegion.Active)?2:1;
        public static int[] SharedImprovements(ThreeKingdomsState s,string player,Func<string,int[]> levels)
        { var members=Members(s,player);return Enumerable.Range(0,3).Select(t=>members.Max(p=>levels(p)[t])).ToArray(); }
        public static void RecordVictory(ThreeKingdomsState s,string winner)
        {if(Enabled(s)){if(s.AllianceB6==null)s.AllianceB6=new B6AllianceState();s.AllianceB6.WinnerPlayerIds=Members(s,winner);}}
    }
}
