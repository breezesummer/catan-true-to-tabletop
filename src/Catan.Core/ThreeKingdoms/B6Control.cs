using System;
using System.Linq;
using System.Runtime.Serialization;

namespace Catan.Core.ThreeKingdoms
{
    [DataContract] public sealed class B6ControlGrant
    {
        [DataMember(Order=1)] public string ControllerPlayerId { get; set; }
        [DataMember(Order=2)] public string AssetPlayerId { get; set; }
        [DataMember(Order=3)] public string TenureId { get; set; }
        [DataMember(Order=4)] public long OpportunityId { get; set; }
    }

    public static partial class B2SkillEngine
    {
        public static bool B6Enabled(ThreeKingdomsState s) => s?.SkillsEnabled == true && s.RulesVersion == GeneralCatalog.B6RulesVersion;
        public static B6ControlGrant CloneControlB6(B6ControlGrant g) => g == null ? null : new B6ControlGrant {
            ControllerPlayerId=g.ControllerPlayerId, AssetPlayerId=g.AssetPlayerId, TenureId=g.TenureId, OpportunityId=g.OpportunityId };
        public static string ControllerB6(ThreeKingdomsState s, string assetPlayer)
        {
            var g=s?.ControlB6;
            return B6Enabled(s) && g != null && g.OpportunityId == s.Clock.OpportunityId && g.AssetPlayerId == assetPlayer
                ? g.ControllerPlayerId : assetPlayer;
        }
        public static bool CanOperateB6(ThreeKingdomsState s, string actor, string assetPlayer)
            => ControllerB6(s,assetPlayer)==actor;
        public static string ControllerForViewB6(ThreeKingdomsView view,string assetPlayer)
            => view?.ControlB6!=null && view.ControlB6.AssetPlayerId==assetPlayer ? view.ControlB6.ControllerPlayerId : assetPlayer;
        public static bool CanVoluntaryExchangeB6(ThreeKingdomsState s,string a,string b,int give,int take)
        {
            if (!B6Enabled(s)) return true;
            if (!B6AllianceRules.CanTrade(s,a,b)) return false;
            var grant=s.ControlB6;
            return grant==null || grant.OpportunityId!=s.Clock.OpportunityId || (grant.AssetPlayerId!=a && grant.AssetPlayerId!=b) || give==take;
        }
        private static void OnControlB6OpportunityStart(IThreeKingdomsCardHost host,long opportunity,string player)
        {
            var s=host.State;
            if(!B6Enabled(s)||!Mark(s,"b6-control-opportunity:"+opportunity))return;
            s.ControlB6=null;
            var frames=Order(s).Select(id=>Active(s,id)).Where(g=>g?.GeneralId=="tk-g04"&&Value(g,"huoxin",0)==0)
                .Select(g=>{var f=Frame(s,g,"tk-g04-s1","b6-huoxin",0,"opportunity:"+opportunity);f.Optional=true;f.Targets=new[]{player};return f;}).ToArray();
            Prepend(s,frames);
        }
        private static bool ProcessControlB6Frame(IThreeKingdomsCardHost host,B2SkillFrame f)
        {
            if(f.Kind!="b6-huoxin")return false;
            var s=host.State;var g=s.Generals.SingleOrDefault(x=>x.TenureId==f.TenureId);
            if(g!=null&&Value(g,"huoxin",0)==0&&s.ControlB6==null)
            {
                Counter(g,"huoxin",0).Value=1;
                s.ControlB6=new B6ControlGrant {ControllerPlayerId=f.PlayerId,AssetPlayerId=f.Targets[0],TenureId=f.TenureId,OpportunityId=s.Clock.OpportunityId};
                B4SkillHistoryEngine.Complete(s,f.Id,"代操 "+f.Targets[0]+" 的本回合");
            }
            Remove(s,f);return true;
        }
        private static void OnControlB6End(IThreeKingdomsCardHost host,string eventId)
        {
            var s=host.State;if(!B6Enabled(s))return;
            var g=Active(s,s.Clock.ActivePlayerId);
            if(g?.GeneralId=="tk-g04")Append(s,Frame(s,g,"tk-g04-s2","resource",2,eventId));
        }
        public static string[] ValidateControlB6(ThreeKingdomsState s)
        {
            var g=s?.ControlB6;if(g==null)return Array.Empty<string>();
            return !B6Enabled(s)||!s.SeatPlayerIds.Contains(g.ControllerPlayerId)||!s.SeatPlayerIds.Contains(g.AssetPlayerId)
                ||g.AssetPlayerId!=s.Clock.ActivePlayerId||g.OpportunityId!=s.Clock.OpportunityId||string.IsNullOrEmpty(g.TenureId)
                ?new[]{"惑心代操身份或期限无效。"}:Array.Empty<string>();
        }
    }
}
