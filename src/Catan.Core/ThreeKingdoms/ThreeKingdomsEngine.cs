using System;
using System.Collections.Generic;
using System.Linq;

namespace Catan.Core.ThreeKingdoms
{
    /// <summary>纯规则生命周期。宿主以同一事务提交本状态及传入的权威随机流。</summary>
    public static class ThreeKingdomsEngine
    {
        public static ThreeKingdomsState Create(string[] seatPlayerIds, uint initialSeed, Func<int, int> draw)
        {
            Require(seatPlayerIds != null && (seatPlayerIds.Length == 3 || seatPlayerIds.Length == 4)
                && seatPlayerIds.All(x => !string.IsNullOrWhiteSpace(x)) && seatPlayerIds.Distinct().Count() == seatPlayerIds.Length,
                "三国开发骨架需要三个或四个不同席位。");
            Require(draw != null, "缺少权威随机流。");
            var state = new ThreeKingdomsState
            {
                RulesVersion = GeneralCatalog.RulesVersion, PackageVersion = GeneralCatalog.PackageVersion,
                PackageContentHash = GeneralCatalog.ContentHash, SkillsEnabled = false, InitialSeed = initialSeed,
                SeatPlayerIds = (string[])seatPlayerIds.Clone(),
                Players = seatPlayerIds.Select(id => new ThreeKingdomsPlayer { PlayerId = id }).ToArray(),
                Generals = GeneralCatalog.Definitions.Select(g => new GeneralInstance
                    { GeneralId = g.Id, InstanceId = "tk-card-" + g.Id.Substring(4), Region = GeneralRegion.Pool }).ToArray(),
                Clock = new ThreeKingdomsClock { Phase = ThreeKingdomsPhase.OpeningSelection, ActivePlayerId = seatPlayerIds[0] }
            };
            Require(state.Generals.Length >= 2 * (seatPlayerIds.Length - 1) + 5, "将包不足以完成正常串行选将。");
            DealOpening(state, draw);
            return state;
        }

        // 合法位置预览频繁复制；逐字段复制仍保留与存档相同的完整可变状态。
        public static ThreeKingdomsState Clone(ThreeKingdomsState state)
        {
            if (state == null) return null;
            return new ThreeKingdomsState
            {
                RulesVersion = state.RulesVersion, PackageVersion = state.PackageVersion,
                PackageContentHash = state.PackageContentHash, SkillsEnabled = state.SkillsEnabled,
                Generals = state.Generals?.Select(g => g == null ? null : new GeneralInstance
                {
                    InstanceId = g.InstanceId, GeneralId = g.GeneralId, Region = g.Region, OwnerPlayerId = g.OwnerPlayerId,
                    TenureId = g.TenureId, RemainingLife = g.RemainingLife, EnteredRound = g.EnteredRound,
                    LastLifeDecrementRound = g.LastLifeDecrementRound,
                    Counters = g.Counters?.Select(c => c == null ? null : new GeneralCounter
                    { Key = c.Key, Value = c.Value, TurnInstanceId = c.TurnInstanceId }).ToArray()
                }).ToArray(),
                Players = state.Players?.Select(p => p == null ? null : new ThreeKingdomsPlayer
                { PlayerId = p.PlayerId, SelectionComplete = p.SelectionComplete, FaceDown = p.FaceDown }).ToArray(),
                Clock = CopyClock(state.Clock), PendingDecision = CopyDecision(state.PendingDecision),
                WindowId = state.WindowId, NextId = state.NextId, OpeningComplete = state.OpeningComplete,
                OpeningSeatCursor = state.OpeningSeatCursor, ReplacementInProgress = state.ReplacementInProgress,
                ReplacementReturnPhase = state.ReplacementReturnPhase, ReplacementPlayerId = state.ReplacementPlayerId,
                RoundEndTenureSnapshot = CopyArray(state.RoundEndTenureSnapshot), ExpiredPlayerQueue = CopyArray(state.ExpiredPlayerQueue),
                EffectFrames = state.EffectFrames?.Select(f => f == null ? null : new ThreeKingdomsEffectFrame
                {
                    EffectId = f.EffectId, ParentEffectId = f.ParentEffectId, RootEventId = f.RootEventId,
                    SourceTenureId = f.SourceTenureId, SkillId = f.SkillId, PlayerId = f.PlayerId,
                    Step = f.Step, CostPaid = f.CostPaid, CompletedBranches = CopyArray(f.CompletedBranches),
                    Targets = CopyArray(f.Targets), TurnInstanceId = f.TurnInstanceId, PhaseId = f.PhaseId,
                    ReturnWindowId = f.ReturnWindowId
                }).ToArray(),
                ProcessedTriggerIds = CopyArray(state.ProcessedTriggerIds), InitialSeed = state.InitialSeed,
                RandomAlgorithmId = state.RandomAlgorithmId, GeneralDrawOrder = CopyArray(state.GeneralDrawOrder),
                SeatPlayerIds = CopyArray(state.SeatPlayerIds)
            };
        }

        private static T[] CopyArray<T>(T[] values) => values == null ? null : (T[])values.Clone();
        private static ThreeKingdomsClock CopyClock(ThreeKingdomsClock clock) => clock == null ? null : new ThreeKingdomsClock
        {
            Phase = clock.Phase, Round = clock.Round, NormalSeatCursor = clock.NormalSeatCursor,
            ActivePlayerId = clock.ActivePlayerId, TurnInstanceId = clock.TurnInstanceId, OpportunityId = clock.OpportunityId,
            PhaseId = clock.PhaseId, IsExtraTurn = clock.IsExtraTurn, ExtraTurnQueue = CopyArray(clock.ExtraTurnQueue),
            NormalOpportunitiesConsumed = clock.NormalOpportunitiesConsumed, ExtraOpportunitiesConsumed = clock.ExtraOpportunitiesConsumed,
            SkippedOpportunityPlayerIds = CopyArray(clock.SkippedOpportunityPlayerIds), DrawPhaseTurnInstanceId = clock.DrawPhaseTurnInstanceId
        };
        private static ThreeKingdomsDecision CopyDecision(ThreeKingdomsDecision decision) => decision == null ? null : new ThreeKingdomsDecision
        {
            Kind = decision.Kind, PlayerId = decision.PlayerId, DecisionId = decision.DecisionId, WindowId = decision.WindowId,
            CandidateIds = CopyArray(decision.CandidateIds), RequiredCount = decision.RequiredCount, CandidateCount = decision.CandidateCount
        };

        public static void ChooseGenerals(ThreeKingdomsState state, string playerId, string[] generalIds,
            string decisionId, string windowId, Func<int, int> draw)
        {
            var pending = RequireDecision(state, playerId, decisionId, windowId);
            Require(pending.Kind == ThreeKingdomsDecisionKind.SelectOpening || pending.Kind == ThreeKingdomsDecisionKind.SelectReplacement,
                "当前需要选择登场武将。");
            Require(generalIds != null && generalIds.Length == pending.RequiredCount && generalIds.Distinct().Count() == generalIds.Length
                && generalIds.All(id => pending.CandidateIds.Contains(id)), "选择数量或候选无效。");
            foreach (var id in pending.CandidateIds)
            {
                var general = Find(state, id);
                if (generalIds.Contains(id)) { general.Region = GeneralRegion.Hidden; }
                else ReturnToPool(general);
            }
            if (pending.Kind == ThreeKingdomsDecisionKind.SelectReplacement)
                state.EffectFrames.Single(f => f.SkillId == "tk-lifecycle-replacement").Step = 2;
            SetDecision(state, playerId,
                pending.Kind == ThreeKingdomsDecisionKind.SelectOpening ? ThreeKingdomsDecisionKind.DeployOpening : ThreeKingdomsDecisionKind.DeployReplacement,
                Owned(state, playerId, GeneralRegion.Hidden).Select(x => x.GeneralId).OrderBy(x => x, StringComparer.Ordinal).ToArray(), 1);
        }

        public static void DeployGeneral(ThreeKingdomsState state, string playerId, string generalId,
            string decisionId, string windowId, Func<int, int> draw)
        {
            Require(draw != null, "缺少权威随机流。");
            var pending = RequireDecision(state, playerId, decisionId, windowId);
            Require(pending.Kind == ThreeKingdomsDecisionKind.DeployOpening || pending.Kind == ThreeKingdomsDecisionKind.DeployReplacement,
                "当前需要先保留武将。");
            Require(generalId != null && pending.CandidateIds.Contains(generalId), "登场选择无效。");
            var general = Find(state, generalId);
            Require(general.Region == GeneralRegion.Hidden && general.OwnerPlayerId == playerId, "登场武将不在自己的武将库中。");
            state.PendingDecision = null;
            if (pending.Kind == ThreeKingdomsDecisionKind.DeployOpening)
            {
                general.Region = GeneralRegion.ReservedDeployment;
                state.Players.Single(p => p.PlayerId == playerId).SelectionComplete = true;
                state.OpeningSeatCursor++;
                if (state.OpeningSeatCursor < state.SeatPlayerIds.Length) DealOpening(state, draw);
                else
                {
                    // 所有人完成之前不创建公开任次；统一按固定座位公开。
                    foreach (var id in state.SeatPlayerIds) Activate(state, Owned(state, id, GeneralRegion.ReservedDeployment).Single(), 1);
                    state.OpeningComplete = true;
                    SetPhase(state, ThreeKingdomsPhase.InitialPlacement);
                }
                return;
            }
            Activate(state, general, state.ReplacementReturnPhase == ThreeKingdomsPhase.RoundEnd ? state.Clock.Round + 1 : state.Clock.Round);
            state.ReplacementInProgress = false;
            state.ReplacementPlayerId = null;
            state.EffectFrames = state.EffectFrames.Where(f => f.SkillId != "tk-lifecycle-replacement").ToArray();
            SetPhase(state, state.ReplacementReturnPhase);
            if (state.Clock.Phase == ThreeKingdomsPhase.RoundEnd) ContinueRoundBoundary(state, draw);
        }

        public static void RetireGeneral(ThreeKingdomsState state, string playerId, string windowId, Func<int, int> draw)
        {
            Require(draw != null, "缺少权威随机流。");
            Require(state != null && state.OpeningComplete && state.Clock.Round > 0 && state.Clock.Phase != ThreeKingdomsPhase.Finished,
                "初始摆放或结束后不能主动隐退。");
            Require(state.Clock.ActivePlayerId == playerId, "只能在自己的真实回合主动隐退。");
            Require(state.Clock.Phase != ThreeKingdomsPhase.RoundEnd && !state.ReplacementInProgress && state.PendingDecision == null,
                "请先完成当前武将选择。");
            RequireWindow(state, windowId);
            Require(Owned(state, playerId, GeneralRegion.Active).Count() == 1, "当前没有可隐退的在场武将。");
            StartReplacement(state, playerId, draw);
        }

        public static void BeginNormalTurns(ThreeKingdomsState state)
        {
            Require(state != null && state.OpeningComplete && state.PendingDecision == null && state.Clock.Phase == ThreeKingdomsPhase.InitialPlacement,
                "全部初始摆放完成后才能开始正常轮。");
            state.Clock.Round = 1;
            state.Clock.NormalSeatCursor = 0;
            state.Clock.IsExtraTurn = false;
            StartOpportunity(state, state.SeatPlayerIds[0], null);
        }

        public static void AdvancePhase(ThreeKingdomsState state, string playerId, string windowId)
        {
            Require(state != null && state.PendingDecision == null && state.Clock.ActivePlayerId == playerId, "当前不能推进阶段。");
            RequireWindow(state, windowId);
            ThreeKingdomsPhase next;
            switch (state.Clock.Phase)
            {
                case ThreeKingdomsPhase.Preparation: next = ThreeKingdomsPhase.Judgment; break;
                case ThreeKingdomsPhase.Judgment: next = ThreeKingdomsPhase.Roll; break;
                case ThreeKingdomsPhase.Draw: next = ThreeKingdomsPhase.Play; break;
                case ThreeKingdomsPhase.Play: next = ThreeKingdomsPhase.End; break;
                default: throw new InvalidOperationException("当前阶段须先完成正式规则结算。");
            }
            SetPhase(state, next);
        }

        public static void MarkDrawPhase(ThreeKingdomsState state)
        {
            Require(state != null && state.PendingDecision == null && state.Clock.Phase == ThreeKingdomsPhase.Roll,
                "正式骰局及其待决结束后才能进入摸牌阶段。");
            Require(state.Clock.DrawPhaseTurnInstanceId != state.Clock.TurnInstanceId, "本回合已经产生摸牌阶段。");
            state.Clock.DrawPhaseTurnInstanceId = state.Clock.TurnInstanceId;
            SetPhase(state, ThreeKingdomsPhase.Draw);
        }

        public static void CompleteNormalTurn(ThreeKingdomsState state, Func<int, int> draw)
        {
            Require(draw != null, "缺少权威随机流。");
            Require(state != null && state.PendingDecision == null && !state.ReplacementInProgress && state.Clock.Phase == ThreeKingdomsPhase.End,
                "结束阶段及待决完成后才能结束行动机会。");
            ConsumeOpportunity(state, draw);
        }

        // 后批处理器可排入已获准的额外机会；B1 没有给玩家暴露此入口。
        public static void QueueExtraTurn(ThreeKingdomsState state, string playerId)
        {
            Require(state != null && state.OpeningComplete && state.Clock.Round > 0 && state.SeatPlayerIds.Contains(playerId)
                && state.Clock.Phase != ThreeKingdomsPhase.Finished && state.Clock.Phase != ThreeKingdomsPhase.RoundEnd, "无法加入额外行动机会。");
            state.Clock.ExtraTurnQueue = state.Clock.ExtraTurnQueue.Concat(new[] { playerId }).ToArray();
        }

        public static void Finish(ThreeKingdomsState state)
        {
            if (state == null) return;
            state.PendingDecision = null;
            SetPhase(state, ThreeKingdomsPhase.Finished);
        }

        public static void RefreshWindow(ThreeKingdomsState state)
        {
            if (state == null) return;
            state.WindowId = NewId(state, "window");
            if (state.PendingDecision != null)
            {
                state.PendingDecision.WindowId = state.WindowId;
                state.PendingDecision.DecisionId = NewId(state, "decision");
            }
        }

        public static ThreeKingdomsView Project(ThreeKingdomsState state, string viewerId)
        {
            if (state == null) return null;
            var decision = CopyDecision(state.PendingDecision);
            if (decision != null && decision.PlayerId != viewerId)
            {
                decision.CandidateIds = Array.Empty<string>();
                decision.DecisionId = null;
                decision.WindowId = null;
            }
            return new ThreeKingdomsView
            {
                PackageVersion = state.PackageVersion, PackageContentHash = state.PackageContentHash,
                PackageLabel = GeneralCatalog.PackageLabel, SkillsEnabled = false, WindowId = state.WindowId,
                Clock = CopyClock(state.Clock), PendingDecision = decision, OpeningComplete = state.OpeningComplete,
                PoolCount = state.Generals.Count(g => g.Region == GeneralRegion.Pool),
                CanRetire = state.OpeningComplete && state.Clock.Round > 0 && state.Clock.ActivePlayerId == viewerId
                    && state.PendingDecision == null && !state.ReplacementInProgress
                    && state.Clock.Phase != ThreeKingdomsPhase.RoundEnd && state.Clock.Phase != ThreeKingdomsPhase.Finished,
                Players = state.SeatPlayerIds.Select(id =>
                {
                    var player = state.Players.Single(p => p.PlayerId == id);
                    var active = Owned(state, id, GeneralRegion.Active).SingleOrDefault();
                    var hidden = Owned(state, id, GeneralRegion.Hidden).Concat(Owned(state, id, GeneralRegion.ReservedDeployment)).ToArray();
                    return new ThreeKingdomsPlayerView
                    {
                        PlayerId = id, ActiveGeneralId = active?.GeneralId, ActiveTenureId = active?.TenureId,
                        RemainingLife = active?.RemainingLife ?? 0, HiddenCount = hidden.Length,
                        HiddenGeneralIds = id == viewerId ? hidden.Select(g => g.GeneralId).OrderBy(x => x, StringComparer.Ordinal).ToArray() : Array.Empty<string>(),
                        SelectionComplete = player.SelectionComplete, FaceDown = player.FaceDown
                    };
                }).ToArray()
            };
        }

        private static void DealOpening(ThreeKingdomsState state, Func<int, int> draw)
        {
            var playerId = state.SeatPlayerIds[state.OpeningSeatCursor];
            var count = state.OpeningSeatCursor == state.SeatPlayerIds.Length - 1 ? 5 : 4;
            Require(state.Generals.Count(g => g.Region == GeneralRegion.Pool) >= count, "将池无法完成正常发将。");
            SetDecision(state, playerId, ThreeKingdomsDecisionKind.SelectOpening, Draw(state, playerId, count, draw), 2);
        }

        private static string[] Draw(ThreeKingdomsState state, string playerId, int count, Func<int, int> draw)
        {
            Require(draw != null, "缺少权威随机流。");
            var result = new List<string>();
            for (var i = 0; i < count; i++)
            {
                var pool = state.Generals.Where(g => g.Region == GeneralRegion.Pool).OrderBy(g => g.GeneralId, StringComparer.Ordinal).ToArray();
                var index = draw(pool.Length);
                Require(index >= 0 && index < pool.Length, "权威随机抽样结果无效。");
                var general = pool[index];
                general.Region = GeneralRegion.Candidate;
                general.OwnerPlayerId = playerId;
                result.Add(general.GeneralId);
            }
            state.GeneralDrawOrder = state.GeneralDrawOrder.Concat(result).ToArray();
            return result.ToArray();
        }

        private static void StartReplacement(ThreeKingdomsState state, string playerId, Func<int, int> draw)
        {
            var previous = Owned(state, playerId, GeneralRegion.Active).Single();
            state.ReplacementInProgress = true;
            state.ReplacementReturnPhase = state.Clock.Phase;
            state.ReplacementPlayerId = playerId;
            state.EffectFrames = state.EffectFrames.Concat(new[] { new ThreeKingdomsEffectFrame
            {
                EffectId = NewId(state, "effect"), ParentEffectId = state.EffectFrames.LastOrDefault()?.EffectId,
                RootEventId = NewId(state, "retirement"), SourceTenureId = previous.TenureId,
                SkillId = "tk-lifecycle-replacement", PlayerId = playerId, Step = 1,
                TurnInstanceId = state.Clock.TurnInstanceId, PhaseId = state.Clock.PhaseId, ReturnWindowId = state.WindowId
            } }).ToArray();
            ReturnToPool(previous);
            var count = Math.Min(2, state.Generals.Count(g => g.Region == GeneralRegion.Pool));
            if (count > 0) SetDecision(state, playerId, ThreeKingdomsDecisionKind.SelectReplacement, Draw(state, playerId, count, draw), 1);
            else
            {
                var reserve = Owned(state, playerId, GeneralRegion.Hidden).Select(g => g.GeneralId).ToArray();
                Require(reserve.Length > 0, "将池与暗置武将均为空，无法恢复正常局面。");
                state.EffectFrames.Single(f => f.SkillId == "tk-lifecycle-replacement").Step = 2;
                SetDecision(state, playerId, ThreeKingdomsDecisionKind.DeployReplacement, reserve, 1);
            }
        }

        private static void Activate(ThreeKingdomsState state, GeneralInstance general, int enteredRound)
        {
            general.Region = GeneralRegion.Active;
            general.TenureId = NewId(state, "tenure");
            general.RemainingLife = 3;
            general.EnteredRound = enteredRound;
            general.LastLifeDecrementRound = 0;
            general.Counters = Array.Empty<GeneralCounter>();
        }

        private static void ReturnToPool(GeneralInstance general)
        {
            general.Region = GeneralRegion.Pool;
            general.OwnerPlayerId = null;
            general.TenureId = null;
            general.RemainingLife = 0;
            general.EnteredRound = 0;
            general.LastLifeDecrementRound = 0;
            general.Counters = Array.Empty<GeneralCounter>();
        }

        private static void StartOpportunity(ThreeKingdomsState state, string playerId, Func<int, int> draw)
        {
            state.Clock.ActivePlayerId = playerId;
            state.Clock.OpportunityId++;
            var player = state.Players.Single(p => p.PlayerId == playerId);
            if (player.FaceDown)
            {
                player.FaceDown = false;
                state.Clock.SkippedOpportunityPlayerIds = state.Clock.SkippedOpportunityPlayerIds.Concat(new[] { playerId }).ToArray();
                ConsumeOpportunity(state, draw);
                return;
            }
            state.Clock.TurnInstanceId++;
            SetPhase(state, ThreeKingdomsPhase.Preparation);
        }

        private static void ConsumeOpportunity(ThreeKingdomsState state, Func<int, int> draw)
        {
            if (state.Clock.IsExtraTurn) state.Clock.ExtraOpportunitiesConsumed++;
            else state.Clock.NormalOpportunitiesConsumed++;
            if (state.Clock.ExtraTurnQueue.Length > 0)
            {
                var playerId = state.Clock.ExtraTurnQueue[0];
                state.Clock.ExtraTurnQueue = state.Clock.ExtraTurnQueue.Skip(1).ToArray();
                state.Clock.IsExtraTurn = true;
                StartOpportunity(state, playerId, draw);
                return;
            }
            state.Clock.IsExtraTurn = false;
            state.Clock.NormalSeatCursor++;
            if (state.Clock.NormalSeatCursor < state.SeatPlayerIds.Length)
            {
                StartOpportunity(state, state.SeatPlayerIds[state.Clock.NormalSeatCursor], draw);
                return;
            }
            SetPhase(state, ThreeKingdomsPhase.RoundEnd);
            var snapshot = state.SeatPlayerIds.Select(id => Owned(state, id, GeneralRegion.Active).Single()).ToArray();
            state.RoundEndTenureSnapshot = snapshot.Select(g => g.TenureId).ToArray();
            foreach (var general in snapshot)
            {
                if (general.LastLifeDecrementRound == state.Clock.Round) continue;
                general.RemainingLife--;
                general.LastLifeDecrementRound = state.Clock.Round;
            }
            state.ExpiredPlayerQueue = snapshot.Where(g => g.RemainingLife == 0).Select(g => g.OwnerPlayerId).ToArray();
            ContinueRoundBoundary(state, draw);
        }

        private static void ContinueRoundBoundary(ThreeKingdomsState state, Func<int, int> draw)
        {
            if (state.ExpiredPlayerQueue.Length > 0)
            {
                var playerId = state.ExpiredPlayerQueue[0];
                state.ExpiredPlayerQueue = state.ExpiredPlayerQueue.Skip(1).ToArray();
                StartReplacement(state, playerId, draw);
                return;
            }
            state.RoundEndTenureSnapshot = Array.Empty<string>();
            state.Clock.Round++;
            state.Clock.NormalSeatCursor = 0;
            state.Clock.IsExtraTurn = false;
            StartOpportunity(state, state.SeatPlayerIds[0], draw);
        }

        private static void SetDecision(ThreeKingdomsState state, string playerId, ThreeKingdomsDecisionKind kind, string[] candidates, int required)
        {
            RefreshWindow(state);
            state.PendingDecision = new ThreeKingdomsDecision { Kind = kind, PlayerId = playerId, DecisionId = NewId(state, "decision"),
                WindowId = state.WindowId, CandidateIds = candidates, RequiredCount = required, CandidateCount = candidates.Length };
        }
        private static void SetPhase(ThreeKingdomsState state, ThreeKingdomsPhase phase)
        { state.Clock.Phase = phase; state.Clock.PhaseId++; RefreshWindow(state); }
        private static string NewId(ThreeKingdomsState state, string kind) => "tk-" + kind + "-" + state.NextId++;
        private static GeneralInstance Find(ThreeKingdomsState state, string id) => state.Generals.Single(g => g.GeneralId == id);
        private static IEnumerable<GeneralInstance> Owned(ThreeKingdomsState state, string playerId, GeneralRegion region) =>
            state.Generals.Where(g => g.OwnerPlayerId == playerId && g.Region == region);
        private static void RequireWindow(ThreeKingdomsState state, string windowId) =>
            Require(!string.IsNullOrEmpty(windowId) && state.WindowId == windowId, "此操作窗口已经过期。");
        private static ThreeKingdomsDecision RequireDecision(ThreeKingdomsState state, string playerId, string decisionId, string windowId)
        {
            Require(state?.PendingDecision != null && state.PendingDecision.PlayerId == playerId, "当前不是你的武将选择。");
            RequireWindow(state, windowId);
            Require(!string.IsNullOrEmpty(decisionId) && state.PendingDecision.DecisionId == decisionId, "此选择已经过期。");
            return state.PendingDecision;
        }
        private static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }

        public static string[] Validate(ThreeKingdomsState state)
        {
            var errors = new List<string>();
            if (state == null) return errors.ToArray();
            if (state.RulesVersion != GeneralCatalog.RulesVersion || state.PackageVersion != GeneralCatalog.PackageVersion
                || state.PackageContentHash != GeneralCatalog.ContentHash || state.SkillsEnabled)
                errors.Add("三国规则版本或开发包内容不一致。");
            if (state.SeatPlayerIds == null || state.Players == null || state.Generals == null || state.Clock == null
                || state.EffectFrames == null || state.ExpiredPlayerQueue == null || state.RoundEndTenureSnapshot == null
                || state.GeneralDrawOrder == null || state.ProcessedTriggerIds == null)
                return new[] { "三国存档缺少必需状态。" };
            if ((state.SeatPlayerIds.Length != 3 && state.SeatPlayerIds.Length != 4) || state.SeatPlayerIds.Any(string.IsNullOrWhiteSpace)
                || state.SeatPlayerIds.Distinct().Count() != state.SeatPlayerIds.Length
                || state.Players.Length != state.SeatPlayerIds.Length || state.Players.Any(p => p == null || !state.SeatPlayerIds.Contains(p.PlayerId))
                || state.Players.Select(p => p?.PlayerId).Distinct().Count() != state.Players.Length)
                errors.Add("三国固定座位或玩家身份不合法。");
            if (state.Generals.Length != GeneralCatalog.Definitions.Length || state.Generals.Any(g => g == null)
                || state.Generals.Select(g => g?.GeneralId).Distinct().Count() != state.Generals.Length
                || state.Generals.Select(g => g?.InstanceId).Distinct().Count() != state.Generals.Length)
                return errors.Concat(new[] { "武将实体缺失或重复。" }).ToArray();
            foreach (var general in state.Generals)
            {
                if (GeneralCatalog.Get(general.GeneralId) == null || general.InstanceId != "tk-card-" + general.GeneralId.Substring(4)
                    || !Enum.IsDefined(typeof(GeneralRegion), general.Region)) errors.Add("武将实体或所在区域无效。");
                if (general.Region == GeneralRegion.Pool ? general.OwnerPlayerId != null : !state.SeatPlayerIds.Contains(general.OwnerPlayerId))
                    errors.Add("武将归属无效。");
                if (general.Region == GeneralRegion.Active)
                {
                    if (string.IsNullOrWhiteSpace(general.TenureId) || general.RemainingLife < 0 || general.RemainingLife > 3
                        || (general.RemainingLife == 0 && state.Clock.Phase != ThreeKingdomsPhase.RoundEnd)
                        || general.LastLifeDecrementRound > state.Clock.Round || general.EnteredRound > state.Clock.Round + 1)
                        errors.Add("在场武将任次或寿命无效。");
                }
                else if (general.TenureId != null || general.RemainingLife != 0 || general.EnteredRound != 0 || general.LastLifeDecrementRound != 0)
                    errors.Add("未登场武将不能带有任次或寿命。");
                if (general.Counters == null || general.Counters.Any(c => c == null || c.Value < 0)
                    || (general.Region != GeneralRegion.Active && general.Counters.Length != 0)) errors.Add("武将任次计数无效。");
                if (general.Region == GeneralRegion.Active && general.LastLifeDecrementRound > 0
                    && general.LastLifeDecrementRound < general.EnteredRound) errors.Add("武将寿命扣减早于本任次登场。");
                if (state.OpeningComplete && general.Region == GeneralRegion.ReservedDeployment) errors.Add("开局公开完成后仍残留拟登场武将。");
            }
            if (state.Generals.Where(g => g.Region == GeneralRegion.Active).Select(g => g.TenureId).Distinct().Count()
                != state.Generals.Count(g => g.Region == GeneralRegion.Active)) errors.Add("在场任次重复。");
            foreach (var playerId in state.SeatPlayerIds)
            {
                var activeCount = Owned(state, playerId, GeneralRegion.Active).Count();
                var hiddenCount = Owned(state, playerId, GeneralRegion.Hidden).Count();
                var reservedCount = Owned(state, playerId, GeneralRegion.ReservedDeployment).Count();
                if (activeCount > 1 || reservedCount > 1 || hiddenCount + activeCount + reservedCount > 2) errors.Add("武将库超过生命周期允许数量。");
                if (state.OpeningComplete && activeCount != (state.ReplacementInProgress && state.ReplacementPlayerId == playerId ? 0 : 1))
                    errors.Add("正常局缺少在场武将。");
            }
            if (state.PendingDecision != null)
            {
                var pending = state.PendingDecision;
                if (!Enum.IsDefined(typeof(ThreeKingdomsDecisionKind), pending.Kind) || !state.SeatPlayerIds.Contains(pending.PlayerId) || string.IsNullOrWhiteSpace(pending.DecisionId)
                    || pending.WindowId != state.WindowId || pending.CandidateIds == null || pending.CandidateCount != pending.CandidateIds.Length
                    || pending.RequiredCount < 1 || pending.RequiredCount > pending.CandidateCount || pending.CandidateIds.Distinct().Count() != pending.CandidateIds.Length)
                    errors.Add("武将待决标识或数量无效。");
                else
                {
                    var region = pending.Kind == ThreeKingdomsDecisionKind.SelectOpening || pending.Kind == ThreeKingdomsDecisionKind.SelectReplacement
                        ? GeneralRegion.Candidate : GeneralRegion.Hidden;
                    if (!Owned(state, pending.PlayerId, region).Select(g => g.GeneralId).OrderBy(x => x, StringComparer.Ordinal)
                        .SequenceEqual(pending.CandidateIds.OrderBy(x => x, StringComparer.Ordinal))) errors.Add("武将待决候选与实体区域不一致。");
                }
            }
            else if (state.Generals.Any(g => g.Region == GeneralRegion.Candidate)) errors.Add("候选区缺少对应待决。");
            if (state.ReplacementInProgress != (state.PendingDecision != null &&
                (state.PendingDecision.Kind == ThreeKingdomsDecisionKind.SelectReplacement || state.PendingDecision.Kind == ThreeKingdomsDecisionKind.DeployReplacement)))
                errors.Add("武将轮换待决不完整。");
            if (state.ReplacementInProgress && (state.ReplacementPlayerId != state.PendingDecision?.PlayerId
                || state.EffectFrames.Count(f => f != null && f.SkillId == "tk-lifecycle-replacement") != 1))
                errors.Add("武将轮换缺少对应父子效果步骤。");
            if (state.EffectFrames.Any(f => f == null || string.IsNullOrWhiteSpace(f.EffectId) || f.Step < 0
                || f.CompletedBranches == null || f.Targets == null)
                || state.EffectFrames.Select(f => f?.EffectId).Distinct().Count() != state.EffectFrames.Length)
                errors.Add("效果帧缺失、重复或步骤无效。");
            if (state.OpeningSeatCursor < 0 || state.OpeningSeatCursor > state.SeatPlayerIds.Length
                || (state.OpeningComplete && state.OpeningSeatCursor != state.SeatPlayerIds.Length)
                || (!state.OpeningComplete && (state.Clock.Phase != ThreeKingdomsPhase.OpeningSelection || state.Clock.Round != 0
                    || state.OpeningSeatCursor >= state.SeatPlayerIds.Length || state.PendingDecision == null
                    || state.PendingDecision.PlayerId != state.SeatPlayerIds[state.OpeningSeatCursor])))
                errors.Add("开局串行选择顺序或阶段不一致。");
            if (state.Clock.ExtraTurnQueue == null || state.Clock.SkippedOpportunityPlayerIds == null || state.Clock.ExtraTurnQueue.Any(id => !state.SeatPlayerIds.Contains(id))
                || state.Clock.NormalSeatCursor < 0 || state.Clock.NormalSeatCursor > state.SeatPlayerIds.Length
                || !state.SeatPlayerIds.Contains(state.Clock.ActivePlayerId) || state.Clock.Round < 0 || state.Clock.OpportunityId < 0
                || !Enum.IsDefined(typeof(ThreeKingdomsPhase), state.Clock.Phase)) errors.Add("正常行动游标或阶段时钟无效。");
            if (state.Clock.Phase == ThreeKingdomsPhase.RoundEnd)
            {
                if (state.Clock.NormalSeatCursor != state.SeatPlayerIds.Length || state.RoundEndTenureSnapshot.Length != state.SeatPlayerIds.Length
                    || state.RoundEndTenureSnapshot.Distinct().Count() != state.RoundEndTenureSnapshot.Length
                    || state.ExpiredPlayerQueue.Distinct().Count() != state.ExpiredPlayerQueue.Length
                    || state.ExpiredPlayerQueue.Any(id => !Owned(state, id, GeneralRegion.Active).Any(g => g.RemainingLife == 0))
                    || state.Generals.Any(g => g.Region == GeneralRegion.Active && g.RemainingLife == 0 && !state.ExpiredPlayerQueue.Contains(g.OwnerPlayerId)))
                    errors.Add("轮界寿命快照或到期队列不一致。");
            }
            else if (state.RoundEndTenureSnapshot.Length != 0 || state.ExpiredPlayerQueue.Length != 0)
                errors.Add("轮界外仍残留寿命快照或到期队列。");
            if (state.Clock.NormalOpportunitiesConsumed < 0 || state.Clock.ExtraOpportunitiesConsumed < 0
                || state.Clock.NormalOpportunitiesConsumed + state.Clock.ExtraOpportunitiesConsumed > state.Clock.OpportunityId
                || state.Clock.OpportunityId - state.Clock.NormalOpportunitiesConsumed - state.Clock.ExtraOpportunitiesConsumed > 1
                || state.Clock.TurnInstanceId < 0 || state.Clock.TurnInstanceId > state.Clock.OpportunityId
                || state.Clock.DrawPhaseTurnInstanceId > state.Clock.TurnInstanceId)
                errors.Add("行动机会消费次数或真实回合时钟不一致。");
            if (state.RandomAlgorithmId != "xorshift32-rejection-v1" || state.GeneralDrawOrder.Any(id => GeneralCatalog.Get(id) == null))
                errors.Add("武将随机算法或抽取记录无效。");
            if (string.IsNullOrWhiteSpace(state.WindowId) || state.NextId < 1) errors.Add("三国窗口序号无效。");
            return errors.Distinct().ToArray();
        }
    }
}
