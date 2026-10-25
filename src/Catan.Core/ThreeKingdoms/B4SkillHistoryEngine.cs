using System;
using System.Linq;
using System.Runtime.Serialization;

namespace Catan.Core.ThreeKingdoms
{
    [DataContract] public sealed class B4SkillHistorySecret
    {
        [DataMember(Order=1)] public string[] ViewerIds { get; set; } = Array.Empty<string>();
        [DataMember(Order=2)] public string Result { get; set; }
    }
    // 发动事件独立于命令；一条命令可触发多人技能，一个技能也可跨多个命令完成。
    [DataContract] public sealed class B4SkillHistoryEvent
    {
        [DataMember(Order=1)] public string EventId { get; set; }
        [DataMember(Order=2)] public string PlayerId { get; set; }
        [DataMember(Order=3)] public string GeneralId { get; set; }
        [DataMember(Order=4)] public string SkillId { get; set; }
        [DataMember(Order=5)] public string TenureId { get; set; }
        [DataMember(Order=6)] public string SourceEventId { get; set; }
        [DataMember(Order=7)] public long TurnInstanceId { get; set; }
        [DataMember(Order=8)] public long PhaseId { get; set; }
        [DataMember(Order=9)] public string PublicResult { get; set; }
        [DataMember(Order=10)] public bool Complete { get; set; }
        [DataMember(Order=11)] public string[] RelatedCardFlowIds { get; set; } = Array.Empty<string>();
        [DataMember(Order=12)] public B4SkillHistorySecret[] Secrets { get; set; } = Array.Empty<B4SkillHistorySecret>();
        [DataMember(Order=13)] public string[] FrameIds { get; set; } = Array.Empty<string>();
    }
    [DataContract] public sealed class B4SkillHistoryView
    {
        [DataMember(Order=1)] public string EventId { get; set; }
        [DataMember(Order=2)] public string PlayerId { get; set; }
        [DataMember(Order=3)] public string GeneralId { get; set; }
        [DataMember(Order=4)] public string SkillId { get; set; }
        [DataMember(Order=5)] public string TenureId { get; set; }
        [DataMember(Order=6)] public string Result { get; set; }
        [DataMember(Order=7)] public bool Complete { get; set; }
        [DataMember(Order=8)] public long TurnInstanceId { get; set; }
    }
    public static class B4SkillHistoryEngine
    {
        // Begin 由实际发动点调用；询问、预览、放弃与非法命令不得调用此接口。
        public static void Begin(ThreeKingdomsState state, string eventId, string playerId, string skillId,
            string tenureId = null, string sourceEventId = null, string publicResult = null)
        {
            if (!GeneralCatalog.IsAutomaticPackage(state?.PackageVersion) || string.IsNullOrEmpty(skillId)) return;
            if (string.IsNullOrEmpty(eventId) || !state.SeatPlayerIds.Contains(playerId)) throw new InvalidOperationException("技能历史缺少合法事件或玩家。");
            var history = state.SkillHistory ?? Array.Empty<B4SkillHistoryEvent>();
            var previous = history.FirstOrDefault(e => e.EventId == eventId || (e.FrameIds??Array.Empty<string>()).Contains(eventId));
            if (previous != null)
            {
                if (previous.PlayerId != playerId || previous.SkillId != skillId) throw new InvalidOperationException("技能历史事件身份冲突。");
                return;
            }
            // 同一发动的明确来源子帧（例如仁德领取奖励）只延续原记录。
            var parent=history.LastOrDefault(e=>e.PlayerId==playerId&&e.SkillId==skillId&&(tenureId==null||e.TenureId==tenureId)&&MatchesFrame(e,sourceEventId));
            if(parent!=null){parent.FrameIds=(parent.FrameIds??Array.Empty<string>()).Concat(new[]{eventId}).Distinct().ToArray();parent.Complete=false;return;}
            var definition = GeneralCatalog.Definitions.FirstOrDefault(g => g.Skills.Any(s => s.Id == skillId));
            if (definition == null) return; // 生命周期或纯牌流内部帧不是武将发动。
            var general = state.Generals.FirstOrDefault(g => g.OwnerPlayerId == playerId && g.GeneralId == definition.Id &&
                (tenureId != null ? g.TenureId == tenureId : g.Region == GeneralRegion.Active));
            state.SkillHistory = history.Concat(new[] { new B4SkillHistoryEvent
            {
                EventId = eventId, PlayerId = playerId, GeneralId = definition.Id, SkillId = skillId,
                TenureId = tenureId ?? general?.TenureId, SourceEventId = sourceEventId,
                TurnInstanceId = state.Clock.TurnInstanceId, PhaseId = state.Clock.PhaseId,
                PublicResult = publicResult ?? "正在结算", FrameIds=new[]{eventId}
            } }).ToArray();
        }
        public static void Complete(ThreeKingdomsState state, string eventId, string publicResult = null, string[] relatedCardFlowIds = null)
        {
            var item = state?.SkillHistory?.FirstOrDefault(e => e.EventId == eventId || (e.FrameIds??Array.Empty<string>()).Contains(eventId));
            if (item == null || item.Complete) return;
            item.Complete = true; item.PublicResult = publicResult ?? (item.PublicResult=="正在结算"?"结算完成":item.PublicResult);
            if(relatedCardFlowIds!=null)item.RelatedCardFlowIds=item.RelatedCardFlowIds.Concat(relatedCardFlowIds).Distinct().ToArray();
        }
        private static bool MatchesFrame(B4SkillHistoryEvent item,string source) => source!=null&&new[]{item.EventId}.Concat(item.FrameIds??Array.Empty<string>()).Any(id=>source==id||source.StartsWith(id+":",StringComparison.Ordinal));
        public static void LinkCardFlow(ThreeKingdomsState state,string skillEventId,string flowId,B2HandChange[] changes=null)
        {
            var item=state?.SkillHistory?.FirstOrDefault(e=>MatchesFrame(e,skillEventId));
            if(item==null||string.IsNullOrEmpty(flowId)||item.RelatedCardFlowIds.Contains(flowId))return;
            item.RelatedCardFlowIds=item.RelatedCardFlowIds.Concat(new[]{flowId}).ToArray();
            var results=(changes??Array.Empty<B2HandChange>()).Select(c=>{
                int lost=Math.Max(c.LostCount,Math.Max(0,c.BeforeCount-c.AfterCount));
                int gained=Math.Max(c.GainedCards?.Sum()??0,Math.Max(0,c.AfterCount-c.BeforeCount+lost));
                return string.Join("，",new[]{lost>0?c.PlayerId+(c.GivenCount>0?"交出":"失去")+lost+"张手牌":null,gained>0?c.PlayerId+"获得"+gained+"张手牌":null}.Where(x=>x!=null));
            }).Where(x=>x.Length>0).ToArray();
            if(results.Length==0)return;
            var previous=item.PublicResult=="正在结算"||item.PublicResult=="结算完成"?null:item.PublicResult;
            item.PublicResult=string.Join("；",new[]{previous}.Concat(results).Where(x=>!string.IsNullOrEmpty(x)));
        }
        // 只按命令明确携带的帧ID/后缀匹配，绝不把最近一次同技能误当作来源。
        internal static void LinkFrameCardFlow(ThreeKingdomsState state,string flowId,B2HandChange[] changes)
        {
            if(!GeneralCatalog.IsAutomaticPackage(state?.PackageVersion))return;
            var source=state.Skills?.Frames.FirstOrDefault(f=>f.Started&&f.SkillId!=null&&(flowId==f.Id||flowId.StartsWith(f.Id+":",StringComparison.Ordinal)));
            if(source!=null)Begin(state,source.Id,source.B4?.OwnerPlayerId??source.PlayerId,source.SkillId,source.TenureId,source.SourceEventId);
            LinkCardFlow(state,flowId,flowId,changes);
        }
        public static void AddPrivateResult(ThreeKingdomsState state, string eventId, string result, params string[] viewerIds)
        {
            var item = state?.SkillHistory?.FirstOrDefault(e => e.EventId == eventId || (e.FrameIds??Array.Empty<string>()).Contains(eventId));
            if (item == null || string.IsNullOrEmpty(result)) return;
            var viewers = (viewerIds ?? Array.Empty<string>()).Where(state.SeatPlayerIds.Contains).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();
            if (viewers.Length == 0) throw new InvalidOperationException("私密技能结果必须明确授权观看者。");
            if (item.Secrets.Any(s => s.Result == result && s.ViewerIds.SequenceEqual(viewers))) return;
            item.Secrets = item.Secrets.Concat(new[] { new B4SkillHistorySecret { ViewerIds = viewers, Result = result } }).ToArray();
        }
        public static B4SkillHistoryView[] Project(ThreeKingdomsState state, string viewerId) =>
            (state?.SkillHistory ?? Array.Empty<B4SkillHistoryEvent>()).Select(e => Project(e, viewerId)).ToArray();
        public static B4SkillHistoryView Project(B4SkillHistoryEvent item, string viewerId) => new B4SkillHistoryView
        {
            EventId = item.EventId, PlayerId = item.PlayerId, GeneralId = item.GeneralId, SkillId = item.SkillId,
            TenureId = item.TenureId, TurnInstanceId = item.TurnInstanceId, Complete = item.Complete,
            Result = string.Join("；", new[] { item.PublicResult }.Concat((item.Secrets ?? Array.Empty<B4SkillHistorySecret>())
                .Where(s => viewerId != null && s.ViewerIds.Contains(viewerId)).Select(s => s.Result)).Where(x => !string.IsNullOrEmpty(x)))
        };
        public static B4SkillHistoryEvent[] Clone(B4SkillHistoryEvent[] events) => events?.Select(e => new B4SkillHistoryEvent
        {
            EventId=e.EventId, PlayerId=e.PlayerId, GeneralId=e.GeneralId, SkillId=e.SkillId, TenureId=e.TenureId,
            SourceEventId=e.SourceEventId, TurnInstanceId=e.TurnInstanceId, PhaseId=e.PhaseId, PublicResult=e.PublicResult,
            Complete=e.Complete, RelatedCardFlowIds=(e.RelatedCardFlowIds??Array.Empty<string>()).ToArray(),FrameIds=(e.FrameIds??Array.Empty<string>()).ToArray(),
            Secrets=(e.Secrets??Array.Empty<B4SkillHistorySecret>()).Select(s=>new B4SkillHistorySecret { ViewerIds=s.ViewerIds.ToArray(), Result=s.Result }).ToArray()
        }).ToArray();
    }
}
