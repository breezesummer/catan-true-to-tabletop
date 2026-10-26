using System;
using System.Collections.Generic;
using System.Linq;

namespace Catan.Core.ThreeKingdoms
{
    public static partial class ThreeKingdomsEngine
    {
        public static bool UsesB4Lifecycle(ThreeKingdomsState state) => GeneralCatalog.UsesB4Rules(state?.RulesVersion);

        /// <summary>选将命令必须以本席令牌验证；他人的提交不会使此令牌过期。</summary>
        public static ThreeKingdomsDecision GetPendingDecision(ThreeKingdomsState state, string playerId) =>
            UsesB4Lifecycle(state) ? state.PendingDecisions?.FirstOrDefault(d => d.PlayerId == playerId)
                : state?.PendingDecision?.PlayerId == playerId ? state.PendingDecision : null;

        public static void RequireOpeningPoolSize(int generalCount, int playerCount, bool simultaneous)
        {
            Require(playerCount == 3 || playerCount == 4, "三国选将只支持三人或四人。");
            Require(generalCount >= (simultaneous ? 4 * playerCount + 1 : 2 * (playerCount - 1) + 5),
                simultaneous ? "完整武将不足以同时发放全员候选（三人至少13张、四人至少17张）。" : "将包不足以完成正常串行选将。");
        }

        private static void BeginOpeningBatch(ThreeKingdomsState state, Func<int, int> draw)
        {
            BeginBatch(state, state.SeatPlayerIds);
            for (int i = 0; i < state.SeatPlayerIds.Length; i++)
            {
                int count = i == state.SeatPlayerIds.Length - 1 ? 5 : 4;
                SetBatchDecision(state, state.SeatPlayerIds[i], ThreeKingdomsDecisionKind.SelectOpening,
                    DrawB4(state, state.SeatPlayerIds[i], count, draw), 2);
            }
        }

        private static void BeginBatch(ThreeKingdomsState state, string[] players)
        {
            Require(state.PendingDecision == null && (state.PendingDecisions == null || state.PendingDecisions.Length == 0),
                "必须先完成已有武将选择批次。");
            state.SelectionBatchId = NewId(state, "selection-batch");
            state.SelectionBatchPlayerIds = state.SeatPlayerIds.Where(players.Contains).ToArray();
            state.PendingDecisions = Array.Empty<ThreeKingdomsDecision>();
            RefreshWindow(state);
        }

        private static void SetBatchDecision(ThreeKingdomsState state, string playerId, ThreeKingdomsDecisionKind kind, string[] candidates, int required)
        {
            var decision = new ThreeKingdomsDecision
            {
                Kind = kind, PlayerId = playerId, DecisionId = NewId(state, "decision"), WindowId = NewId(state, "selection-window"),
                CandidateIds = candidates, CandidateCount = candidates.Length, RequiredCount = required
            };
            state.PendingDecisions = state.PendingDecisions.Where(d => d.PlayerId != playerId).Concat(new[] { decision })
                .OrderBy(d => Array.IndexOf(state.SeatPlayerIds, d.PlayerId)).ToArray();
            SynchronizePendingSelection(state);
        }

        // 单待决字段只作旧宿主的“尚未完成”哨兵，不再担任 B4 的命令鉴权入口。
        private static void SynchronizePendingSelection(ThreeKingdomsState state) =>
            state.PendingDecision = state.PendingDecisions.FirstOrDefault();

        private static void ChooseBatchGenerals(ThreeKingdomsState state, string playerId, string[] generalIds, string decisionId, string windowId)
        {
            var pending = RequireDecision(state, playerId, decisionId, windowId);
            Require(pending.Kind == ThreeKingdomsDecisionKind.SelectOpening || pending.Kind == ThreeKingdomsDecisionKind.SelectReplacement,
                "当前需要选择登场武将。");
            Require(generalIds != null && generalIds.Length == pending.RequiredCount && generalIds.Distinct().Count() == generalIds.Length
                && generalIds.All(pending.CandidateIds.Contains), "选择数量或候选无效。");
            foreach (var id in pending.CandidateIds)
            {
                var general = Find(state, id);
                if (generalIds.Contains(id)) general.Region = GeneralRegion.Hidden;
                else ReturnToPool(general);
            }
            if (pending.Kind == ThreeKingdomsDecisionKind.SelectReplacement)
                state.EffectFrames.Single(f => f.SkillId == "tk-lifecycle-replacement" && f.PlayerId == playerId).Step = 2;
            SetBatchDecision(state, playerId,
                pending.Kind == ThreeKingdomsDecisionKind.SelectOpening ? ThreeKingdomsDecisionKind.DeployOpening : ThreeKingdomsDecisionKind.DeployReplacement,
                Owned(state, playerId, GeneralRegion.Hidden).Select(g => g.GeneralId).OrderBy(x => x, StringComparer.Ordinal).ToArray(), 1);
        }

        private static void DeployBatchGeneral(ThreeKingdomsState state, string playerId, string generalId, string decisionId, string windowId, Func<int, int> draw)
        {
            Require(draw != null, "缺少权威随机流。");
            var pending = RequireDecision(state, playerId, decisionId, windowId);
            Require(pending.Kind == ThreeKingdomsDecisionKind.DeployOpening || pending.Kind == ThreeKingdomsDecisionKind.DeployReplacement,
                "当前需要先保留武将。");
            Require(generalId != null && pending.CandidateIds.Contains(generalId), "登场选择无效。");
            var general = Find(state, generalId);
            Require(general.Region == GeneralRegion.Hidden && general.OwnerPlayerId == playerId, "登场武将不在自己的武将库中。");
            general.Region = GeneralRegion.ReservedDeployment;
            state.PendingDecisions = state.PendingDecisions.Where(d => d.PlayerId != playerId).ToArray();
            SynchronizePendingSelection(state);
            if (pending.Kind == ThreeKingdomsDecisionKind.DeployOpening)
            {
                state.Players.Single(p => p.PlayerId == playerId).SelectionComplete = true;
                state.OpeningSeatCursor++;
            }
            else state.EffectFrames.Single(f => f.SkillId == "tk-lifecycle-replacement" && f.PlayerId == playerId).Step = 3;
            if (state.PendingDecisions.Length != 0) return;

            bool opening = pending.Kind == ThreeKingdomsDecisionKind.DeployOpening;
            int enteredRound = opening ? 1 : state.ReplacementReturnPhase == ThreeKingdomsPhase.RoundEnd ? state.Clock.Round + 1 : state.Clock.Round;
            foreach (var id in state.SelectionBatchPlayerIds)
                Activate(state, Owned(state, id, GeneralRegion.ReservedDeployment).Single(), enteredRound);
            state.SelectionBatchId = null;
            state.SelectionBatchPlayerIds = Array.Empty<string>();
            if (opening)
            {
                state.OpeningComplete = true;
                SetPhase(state, ThreeKingdomsPhase.InitialPlacement);
                return;
            }
            state.ReplacementInProgress = false;
            state.ReplacementPlayerId = null;
            state.EffectFrames = state.EffectFrames.Where(f => f.SkillId != "tk-lifecycle-replacement").ToArray();
            SetPhase(state, state.ReplacementReturnPhase);
            if (state.Clock.Phase == ThreeKingdomsPhase.RoundEnd) ContinueRoundBoundary(state, draw);
        }

        private static void BeginReplacementBatch(ThreeKingdomsState state, string[] playerIds, Func<int, int> draw)
        {
            Require(draw != null, "缺少权威随机流。");
            BeginBatch(state, playerIds);
            state.ReplacementInProgress = true;
            state.ReplacementReturnPhase = state.Clock.Phase;
            state.ReplacementPlayerId = state.SelectionBatchPlayerIds.Length == 1 ? state.SelectionBatchPlayerIds[0] : null;
            string parentEffectId = state.EffectFrames.LastOrDefault()?.EffectId;
            // 同批先全部隐退，再固定座位预留所有候选；早提交的弃选永远不能进入同批他人的候选。
            foreach (var playerId in state.SelectionBatchPlayerIds)
            {
                var previous = Owned(state, playerId, GeneralRegion.Active).Single();
                state.EffectFrames = state.EffectFrames.Concat(new[] { new ThreeKingdomsEffectFrame
                {
                    EffectId = NewId(state, "effect"), ParentEffectId = parentEffectId,
                    RootEventId = state.SelectionBatchId, SourceTenureId = previous.TenureId,
                    SkillId = "tk-lifecycle-replacement", PlayerId = playerId, Step = 1,
                    TurnInstanceId = state.Clock.TurnInstanceId, PhaseId = state.Clock.PhaseId, ReturnWindowId = state.WindowId
                } }).ToArray();
                B2SkillEngine.OnB4GeneralRetired(state, previous);
                B2SkillEngine.OnAllianceB6GeneralRetired(state,previous);
                B2SkillEngine.OnB5MilitaryGeneralRetired(state, previous);
                B2SkillEngine.OnCivilGeneralRetired(state, previous);
                bool retain = GeneralCatalog.UsesB5Rules(state.RulesVersion) && new[] { "tk-g18", "tk-g24", "tk-g35" }.Contains(previous.GeneralId);
                ReturnToPool(previous);
                previous.Region = retain ? GeneralRegion.Retained : GeneralRegion.Retired;
                if (retain) previous.OwnerPlayerId = playerId;
            }
            foreach (var playerId in state.SelectionBatchPlayerIds)
            {
                int count = Math.Min(2, state.Generals.Count(g => g.Region == GeneralRegion.Pool || g.Region == GeneralRegion.Retired));
                if (count > 0)
                    SetBatchDecision(state, playerId, ThreeKingdomsDecisionKind.SelectReplacement, DrawB4(state, playerId, count, draw), 1);
                else
                {
                    string[] hidden = Owned(state, playerId, GeneralRegion.Hidden).Select(g => g.GeneralId).OrderBy(x => x, StringComparer.Ordinal).ToArray();
                    Require(hidden.Length > 0, "公共库、隐退库与暗置武将均为空，无法恢复正常局面。");
                    state.EffectFrames.Single(f => f.SkillId == "tk-lifecycle-replacement" && f.PlayerId == playerId).Step = 2;
                    SetBatchDecision(state, playerId, ThreeKingdomsDecisionKind.DeployReplacement, hidden, 1);
                }
            }
        }

        private static string[] DrawB4(ThreeKingdomsState state, string playerId, int count, Func<int, int> draw)
        {
            var result = new List<string>();
            for (int i = 0; i < count; i++)
            {
                if (!state.Generals.Any(g => g.Region == GeneralRegion.Pool))
                {
                    var retired = state.Generals.Where(g => g.Region == GeneralRegion.Retired).ToArray();
                    Require(retired.Length > 0, "公共库与隐退库均已耗尽。");
                    foreach (var general in retired) ReturnToPool(general);
                    state.RetirementRecycleCount++;
                }
                // 每张仍只消耗一次权威抽样；回收本身不洗牌、不额外消耗随机数。
                result.AddRange(Draw(state, playerId, 1, draw));
            }
            return result.ToArray();
        }

        private static string[] ValidateB4Batch(ThreeKingdomsState state)
        {
            var errors = new List<string>();
            if (state.PendingDecisions == null || state.SelectionBatchPlayerIds == null)
                return new[] { "B4存档缺少武将批次状态。" };
            var decisions = state.PendingDecisions;
            var batchPlayers = state.SelectionBatchPlayerIds;
            if (decisions.Any(d => d == null)) return new[] { "B4武将待决为空。" };
            if (state.RetirementRecycleCount < 0) errors.Add("隐退库回收批次无效。");
            if (batchPlayers.Distinct().Count() != batchPlayers.Length || batchPlayers.Any(id => !state.SeatPlayerIds.Contains(id))
                || !batchPlayers.SequenceEqual(state.SeatPlayerIds.Where(batchPlayers.Contains))) errors.Add("武将批次名单或顺序无效。");
            if (decisions.Select(d => d.PlayerId).Distinct().Count() != decisions.Length
                || decisions.Select(d => d.DecisionId).Distinct().Count() != decisions.Length
                || decisions.Select(d => d.WindowId).Distinct().Count() != decisions.Length
                || !decisions.Select(d => d.PlayerId).SequenceEqual(state.SeatPlayerIds.Where(id => decisions.Any(d => d.PlayerId == id))))
                errors.Add("各席武将待决身份或令牌重复。");
            var first = decisions.FirstOrDefault();
            if (first == null ? state.PendingDecision != null : state.PendingDecision == null
                || first.PlayerId != state.PendingDecision.PlayerId || first.Kind != state.PendingDecision.Kind
                || first.WindowId != state.PendingDecision.WindowId || first.DecisionId != state.PendingDecision.DecisionId
                || first.RequiredCount != state.PendingDecision.RequiredCount || first.CandidateCount != state.PendingDecision.CandidateCount
                || !(first.CandidateIds ?? Array.Empty<string>()).SequenceEqual(state.PendingDecision.CandidateIds ?? Array.Empty<string>()))
                errors.Add("兼容待决哨兵与独立待决不一致。");
            bool hasBatch = batchPlayers.Length != 0;
            if (hasBatch != !string.IsNullOrWhiteSpace(state.SelectionBatchId) || hasBatch != (decisions.Length != 0))
                errors.Add("武将选择批次缺少屏障或待决。");
            if (!state.OpeningComplete && (!batchPlayers.SequenceEqual(state.SeatPlayerIds) || state.ReplacementInProgress
                || state.OpeningSeatCursor != state.Players.Count(p => p.SelectionComplete))) errors.Add("开局并行选择状态不一致。");
            if (state.OpeningComplete && hasBatch != state.ReplacementInProgress) errors.Add("补将批次状态不一致。");
            if (state.ReplacementInProgress && (state.ReplacementPlayerId != (batchPlayers.Length == 1 ? batchPlayers[0] : null)
                || state.ReplacementReturnPhase != state.Clock.Phase)) errors.Add("补将返回阶段或名单不一致。");
            foreach (var pending in decisions)
            {
                bool opening = pending.Kind == ThreeKingdomsDecisionKind.SelectOpening || pending.Kind == ThreeKingdomsDecisionKind.DeployOpening;
                bool choosing = pending.Kind == ThreeKingdomsDecisionKind.SelectOpening || pending.Kind == ThreeKingdomsDecisionKind.SelectReplacement;
                if (!Enum.IsDefined(typeof(ThreeKingdomsDecisionKind), pending.Kind) || !batchPlayers.Contains(pending.PlayerId)
                    || opening == state.OpeningComplete || string.IsNullOrWhiteSpace(pending.DecisionId) || string.IsNullOrWhiteSpace(pending.WindowId)
                    || pending.CandidateIds == null || pending.CandidateIds.Length != pending.CandidateCount
                    || pending.CandidateIds.Distinct().Count() != pending.CandidateIds.Length
                    || pending.RequiredCount != (opening && choosing ? 2 : 1) || pending.CandidateCount < pending.RequiredCount)
                { errors.Add("B4武将待决标识或数量无效。"); continue; }
                var expected = Owned(state, pending.PlayerId, choosing ? GeneralRegion.Candidate : GeneralRegion.Hidden).Select(g => g.GeneralId);
                if (!expected.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(pending.CandidateIds.OrderBy(x => x, StringComparer.Ordinal)))
                    errors.Add("B4武将待决候选与实体区域不一致。");
                if (opening && choosing && pending.CandidateCount != (pending.PlayerId == state.SeatPlayerIds.Last() ? 5 : 4))
                    errors.Add("正常开局候选数量不足。");
            }
            foreach (var playerId in state.SeatPlayerIds)
            {
                var pending = decisions.FirstOrDefault(d => d.PlayerId == playerId);
                int reserved = Owned(state, playerId, GeneralRegion.ReservedDeployment).Count();
                bool member = batchPlayers.Contains(playerId);
                if (reserved != (member && pending == null ? 1 : 0)) errors.Add("武将拟登场区与批次提交不一致。");
                if (Owned(state, playerId, GeneralRegion.Candidate).Any() && (pending == null
                    || pending.Kind != ThreeKingdomsDecisionKind.SelectOpening && pending.Kind != ThreeKingdomsDecisionKind.SelectReplacement))
                    errors.Add("候选区缺少本席待决。");
                if (!state.OpeningComplete && state.Players.Single(p => p.PlayerId == playerId).SelectionComplete != (reserved == 1))
                    errors.Add("开局提交标志与拟登场实体不一致。");
                var frames = state.EffectFrames.Where(f => f != null && f.SkillId == "tk-lifecycle-replacement" && f.PlayerId == playerId).ToArray();
                if (frames.Length != (member && state.ReplacementInProgress ? 1 : 0)) errors.Add("补将缺少本席效果帧。");
                else if (frames.Length == 1 && (frames[0].RootEventId != state.SelectionBatchId || frames[0].Step !=
                    (pending == null ? 3 : pending.Kind == ThreeKingdomsDecisionKind.SelectReplacement ? 1 : 2))) errors.Add("补将效果帧步骤不一致。");
            }
            return errors.Distinct().ToArray();
        }
    }
}
