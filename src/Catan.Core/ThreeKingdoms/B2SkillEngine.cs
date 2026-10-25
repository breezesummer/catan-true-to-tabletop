using System;
using System.Collections.Generic;
using System.Linq;

namespace Catan.Core.ThreeKingdoms
{
    /// <summary>与棋盘、网络、显示无关的 B2 技能队列。选择、暂存实体和已付成本全部属于权威存档。</summary>
    public static partial class B2SkillEngine
    {
        private static readonly string[] CardNames = { "木", "砖", "羊", "麦", "矿", "布", "币", "纸" };
        public static bool HasPending(ThreeKingdomsState state) => state?.SkillsEnabled == true &&
            (state.Skills.PendingDecision != null || state.Skills.Frames.Length > 0 || state.Skills.HarvestActive);
        public static bool IsDrawBlocked(ThreeKingdomsState state, string playerId) => state?.SkillsEnabled == true &&
            (state.Skills.TimedEffects.Any(e => e.Kind == "draw-block" && e.PlayerId == playerId) || CivilDrawBlocked(state, playerId));
        public static bool IsProtected(ThreeKingdomsState state, string playerId) => state?.SkillsEnabled == true &&
            state.Skills.TimedEffects.Any(e => e.Kind == "protection" && e.PlayerId == playerId && e.OpportunityId == state.Clock.OpportunityId);
        public static bool HasUnlimitedProgress(ThreeKingdomsState state, string playerId) => Active(state, playerId)?.GeneralId == "tk-g33";
        public static bool CanTarget(ThreeKingdomsState state, string playerId, string effectId) =>
            Active(state, playerId)?.GeneralId != "tk-g23" || !new[] { "Espionage", "GuildDues", "Indulgence", "Snatch", "tk-g05-s2", "tk-g49-s2" }.Contains(effectId);
        public static bool IsTargetForbidden(ThreeKingdomsState state, string playerId, string effectId) => !CanTarget(state, playerId, effectId);
        public static void RefreshDecision(ThreeKingdomsState state)
        { var d = state?.Skills?.PendingDecision; if (d != null && d.WindowId != state.WindowId) { d.WindowId = state.WindowId; d.DecisionId = Id(state, "skill-decision"); } }
        public static B2ProgressCard[] EscrowProgressCards(ThreeKingdomsState state) => state?.Skills?.Frames
            .Concat(state.Response?.SuspendedFrames ?? Array.Empty<B2SkillFrame>()).Concat(state.Military?.SuspendedFrames ?? Array.Empty<B2SkillFrame>()).Where(f => f.ProgressCard != null && f.Kind != "response-jianxiong")
            .Select(f => f.ProgressCard).ToArray() ?? Array.Empty<B2ProgressCard>();
        public static void HoldReturnedCards(IThreeKingdomsCardHost host, int[] cards)
        { if (!Enabled(host) || cards.Sum() == 0) return; host.RemoveBank(cards); Append(host.State, new B2SkillFrame { Id = Id(host.State, "settlement"), Kind = "discard", Cards = Copy(cards) }); }

        public static B2SkillState Clone(B2SkillState state)
        {
            if (state == null) return null;
            var d = state.PendingDecision;
            return new B2SkillState
            {
                SuppressPump = state.SuppressPump,
                PendingDecision = d == null ? null : new B2SkillDecision { PlayerId = d.PlayerId, DecisionId = d.DecisionId, WindowId = d.WindowId, Title = d.Title, Description = d.Description, Kind = d.Kind, FrameId = d.FrameId },
                Frames = state.Frames.Select(CloneFrame).ToArray(),
                TimedEffects = state.TimedEffects.Select(e => new B2TimedEffect { Id = e.Id, Kind = e.Kind, PlayerId = e.PlayerId, TargetPlayerId = e.TargetPlayerId, OpportunityId = e.OpportunityId, CreatedTurnId = e.CreatedTurnId, SourceTenureId = e.SourceTenureId }).ToArray(),
                LossLedger = state.LossLedger.Select(l => new B2PlayerLedger { PlayerId = l.PlayerId, TurnId = l.TurnId, LostHandCards = l.LostHandCards }).ToArray(),
                ProcessedEvents = (string[])state.ProcessedEvents.Clone(),
                Harvest = state.Harvest.Select(a => new B2HarvestAllocation { PlayerId = a.PlayerId, Cards = Copy(a.Cards), Completed = a.Completed, ActuallyReceived = a.ActuallyReceived,
                    TenureId = a.TenureId, GeneralId = a.GeneralId, GoldCount = a.GoldCount, AqueductEligible = a.AqueductEligible, ChoiceComplete = a.ChoiceComplete,
                    ReplacementKind = a.ReplacementKind, ForgoneCards = Copy(a.ForgoneCards), Targets = (string[])a.Targets.Clone() }).ToArray(),
                HarvestTurnId = state.HarvestTurnId, HarvestActive = state.HarvestActive, RevealedCards = Copy(state.RevealedCards),
                DeferredCapacityPlayers = (string[])state.DeferredCapacityPlayers.Clone(), HarvestStage = state.HarvestStage,
                AfterGoldFrames = state.AfterGoldFrames.Select(CloneFrame).ToArray(), ProcessedSkippedCount = state.ProcessedSkippedCount,
                ReturnedCards = state.ReturnedCards?.Select(r => new B3ReturnedCards { PlayerId = r.PlayerId, Cause = r.Cause, EventId = r.EventId, Cards = Copy(r.Cards) }).ToArray()
            };
        }
        private static B2SkillFrame CloneFrame(B2SkillFrame f) => new B2SkillFrame { Id = f.Id, SkillId = f.SkillId, PlayerId = f.PlayerId, TenureId = f.TenureId, Kind = f.Kind, Step = f.Step,
            Remaining = f.Remaining, Cards = Copy(f.Cards), Targets = (string[])f.Targets.Clone(), ProgressCard = f.ProgressCard == null ? null : new B2ProgressCard { Id = f.ProgressCard.Id, Color = f.ProgressCard.Color, PublicPoint = f.ProgressCard.PublicPoint },
            Tiles = f.Tiles.Select(t => new B2HarvestTile { TileId = t.TileId, Number = t.Number, AllowedCardTypes = (int[])t.AllowedCardTypes.Clone() }).ToArray(),
            Optional = f.Optional, SourceEventId = f.SourceEventId, SelectedCards = Copy(f.SelectedCards), Started = f.Started, B4 = CloneB4Payload(f.B4), B4OrderChosen = f.B4OrderChosen,
            CivilB5 = CloneB5CivilPayload(f.CivilB5) };

        public static void Project(IThreeKingdomsCardHost host, ThreeKingdomsView view, string viewerId)
        {
            var state = host.State;
            view.SkillDecision = null;
            view.EffectiveCardTypes = B4Enabled(state) ? Enumerable.Range(0, 8).Select(t => EffectiveCardTypes(state, viewerId, t)).ToArray() : null;
            if (state?.SkillsEnabled != true) return;
            var d = state.Skills.PendingDecision;
            if (d != null) view.SkillDecision = new B2SkillDecision { PlayerId = d.PlayerId, Title = d.Title,
                Description = d.PlayerId == viewerId ? d.Description : "等待该玩家完成技能选择。",
                DecisionId = d.PlayerId == viewerId ? d.DecisionId : null, WindowId = d.PlayerId == viewerId ? d.WindowId : null,
                Kind = d.PlayerId == viewerId ? d.Kind : null, FrameId = d.PlayerId == viewerId ? d.FrameId : null };
            view.SkillActions = Actions(host, viewerId);
            view.RevealedSkillCards = Copy(state.Skills.RevealedCards);
            view.SkillStatus = state.Skills.TimedEffects.Select(e => e.PlayerId + "：" +
                (e.Kind == "draw-block" ? "据守禁摸至下次行动机会" : e.Kind == "protection" ? "克己保护（行动机会 " + e.OpportunityId + "）" : "镇骨等待 " + e.TargetPlayerId + " 出牌"))
                .Concat(state.Generals.Where(g => g.Region == GeneralRegion.Active).SelectMany(g => g.Counters.Select(c =>
                    g.OwnerPlayerId + "：" + CounterLabel(c.Key) + " " + c.Value))).ToArray();
            ProjectB5Military(host, view, viewerId);
            ProjectCivilB5(host, view, viewerId);
            if (B5Enabled(state)) view.ProgressHandLimit = HasUnlimitedProgress(state, viewerId) ? (int?)null : 4 + B5ProgressLimitBonus(state, viewerId);
        }

        private static string CounterLabel(string key) => key == "rende" ? "仁德累计余数" : key == "zhiheng-hand" ? "制衡手牌已用" :
            key == "zhiheng-progress" ? "制衡发展牌已用" : key == "qianxun" ? "谦逊弃牌已用" : key == "qinqin" ? "勤政循环位置" :
            key == "lianpo" ? "连破已用" : key == "fangzhu" ? "放逐已用" : key == "jiushi" ? "酒诗翻面已用" : key == "jiushi-extra" ? "酒诗额外机会已用" : key == "zhijian" ? "直谏已用次数" :
            key.StartsWith("luoying:", StringComparison.Ordinal) ? "落英已收 " + key.Substring(8) : "奋音上次建筑类别";

        public static void OnCardsMoved(IThreeKingdomsCardHost host, string batchId, B2HandChange[] changes)
        { if (!Enabled(host)) return; RecordCards(host, batchId, changes); Pump(host); }

        private static void RecordCards(IThreeKingdomsCardHost host, string batchId, B2HandChange[] changes)
        {
            var s = host.State; if (!Mark(s, "move:" + batchId)) return;
            B4SkillHistoryEngine.LinkFrameCardFlow(s,batchId,changes);
            var frames = new List<B2SkillFrame>();
            foreach (var playerId in Order(s))
            {
                var c = changes.SingleOrDefault(x => x.PlayerId == playerId); if (c == null) continue;
                if (c.LostCount > 0)
                {
                    var ledger = s.Skills.LossLedger.SingleOrDefault(x => x.PlayerId == playerId && x.TurnId == s.Clock.TurnInstanceId);
                    if (ledger == null) { ledger = new B2PlayerLedger { PlayerId = playerId, TurnId = s.Clock.TurnInstanceId }; s.Skills.LossLedger = s.Skills.LossLedger.Concat(new[] { ledger }).ToArray(); }
                    ledger.LostHandCards += c.LostCount;
                }
                var g = Active(s, playerId); if (g == null) continue;
                AddB4CardFrames(host, batchId, c, g, frames);
                if (g.GeneralId == "tk-g23" && c.BeforeCount > 0 && c.AfterCount == 0 && c.LostCount > 0)
                    frames.Add(Frame(s, g, "tk-g23-s1", "lianying", 1, batchId));
                if (g.GeneralId == "tk-g15" && s.Clock.ActivePlayerId == playerId && c.GivenCount > 0)
                {
                    var counter = Counter(g, "rende", s.Clock.TurnInstanceId); var n = counter.Value + c.GivenCount;
                    counter.Value = n % 2;
                    // 实际交付已提交，整批赚得的每份奖励都属于旧效果，不能因领到一半换将被取消。
                    for (var i = 0; i < n / 2; i++) { var reward = Frame(s, g, "tk-g15-s1", "mixed", 1, batchId); reward.Started = true; frames.Add(reward); }
                }
            }
            Prepend(s, frames);
            OnCivilCardsMoved(host, batchId, changes);
        }

        public static void OnBuilt(IThreeKingdomsCardHost host, string eventId, string playerId, string buildingType)
        {
            if (!Enabled(host)) return;
            var s = host.State; var g = Active(s, playerId);
            var types = new[] { "Road", "Ship", "Settlement", "City", "CityWall" };
            var type = Array.IndexOf(types, buildingType == "Wall" ? "CityWall" : buildingType);
            OnB4Built(host, eventId, type);
            if (g?.GeneralId != "tk-g12" || s.Clock.Phase != ThreeKingdomsPhase.Play || s.Clock.ActivePlayerId != playerId || type < 0 || !Mark(s, "built:" + eventId)) return;
            var c = Counter(g, "fenyin", s.Clock.PhaseId); var previous = c.Value; c.Value = type + 1;
            if (previous != 0 && previous != c.Value) Append(s, Frame(s, g, "tk-g12-s1", "resource", 1, eventId));
            Pump(host);
        }

        public static void OnEnd(IThreeKingdomsCardHost host, string eventId)
        {
            if (!Enabled(host)) return; var s = host.State; if (!Mark(s, "end:" + eventId)) return;
            foreach (var id in Order(s))
            {
                var g = Active(s, id); if (g == null) continue;
                if (g.GeneralId == "tk-g28" && !s.Skills.LossLedger.Any(x => x.PlayerId == id && x.TurnId == s.Clock.TurnInstanceId && x.LostHandCards > 0))
                {
                    Append(s, Frame(s, g, "tk-g28-s2", "keji", 1, eventId));
                }
                if (B3Enabled(s) && id == s.Clock.ActivePlayerId)
                {
                    if (g.GeneralId == "tk-g06") { var f = Frame(s, g, "tk-g06-s1", "jushou", 4, eventId); f.Optional = true; Append(s, f); }
                    if (g.GeneralId == "tk-g09") { var f = Frame(s, g, "tk-g09-s1", "zhenggu", 0, eventId); f.Optional = true; Append(s, f); }
                }
                AddTimeEndFrames(host, g, eventId);
                AddB4EndFrames(host, g, eventId);
            }
            var active = Active(s, s.Clock.ActivePlayerId);
            if (!B3Enabled(s) && active?.GeneralId == "tk-g06") { var f = Frame(s, active, "tk-g06-s1", "jushou", 4, eventId); f.Optional = true; Append(s, f); }
            if (!B3Enabled(s) && active?.GeneralId == "tk-g09") { var f = Frame(s, active, "tk-g09-s1", "zhenggu", 0, eventId); f.Optional = true; Append(s, f); }
            OnB5MilitaryEnd(host, eventId);
            OnCivilEnd(host, eventId);
            Pump(host);
        }

        public static void OnPlayStart(IThreeKingdomsCardHost host, string eventId, string playerId)
        {
            if (!Enabled(host)) return; var s = host.State; if (!Mark(s, "play:" + eventId)) return;
            OnCivilPlayStart(host, eventId, playerId);
            foreach (var e in s.Skills.TimedEffects.Where(x => x.Kind == "zhenggu" && x.TargetPlayerId == playerId).ToArray())
            {
                Append(s, new B2SkillFrame { Id = Id(s, "frame"), Kind = "zhenggu-later", PlayerId = e.PlayerId, SkillId = "tk-g09-s1", Targets = new[] { playerId } });
                s.Skills.TimedEffects = s.Skills.TimedEffects.Where(x => x.Id != e.Id).ToArray();
            }
            Pump(host);
        }

        public static void OnOpportunityStart(IThreeKingdomsCardHost host, long opportunityId, string playerId, string[] skippedPlayerIds = null)
        {
            if (!Enabled(host)) return; var s = host.State;
            OnB4OpportunityStart(host, opportunityId, playerId);
            OnB5MilitaryOpportunityStart(host, opportunityId, playerId, skippedPlayerIds);
            OnB5MilitaryTurnStart(host, "turn-start-" + s.Clock.TurnInstanceId, playerId);
            var begun = new HashSet<string>(skippedPlayerIds ?? Array.Empty<string>()) { playerId };
            s.Skills.TimedEffects = s.Skills.TimedEffects.Where(e => !(e.Kind == "draw-block" && begun.Contains(e.PlayerId)) &&
                !(e.Kind == "protection" && e.OpportunityId < opportunityId)).ToArray();
        }
        public static void OnOpportunityEnd(IThreeKingdomsCardHost host, long opportunityId, string playerId)
        {
            if (!Enabled(host)) return;
            host.State.Skills.TimedEffects = host.State.Skills.TimedEffects.Where(e => !(e.Kind == "protection" && e.OpportunityId <= opportunityId)).ToArray();
        }

        public static void OnTargeted(IThreeKingdomsCardHost host, string useId, string sourcePlayerId, string targetPlayerId, string effectId)
        { if (!Enabled(host)) return; Require(CanTarget(host.State, targetPlayerId, effectId), "谦逊禁止成为该效果的目标。"); Target(host, useId, sourcePlayerId, targetPlayerId); Pump(host); }

        private static void Target(IThreeKingdomsCardHost host, string useId, string sourcePlayerId, string targetPlayerId)
        {
            var s = host.State;
            OnCivilTargeted(host, useId, sourcePlayerId, targetPlayerId, null);
            if (sourcePlayerId == targetPlayerId || !Mark(s, "target:" + useId + ":" + targetPlayerId)) return;
            var g = Active(s, targetPlayerId);
            if (g?.GeneralId == "tk-g37") Prepend(s, new[] { Frame(s, g, "tk-g37-s2", "resource", 1, useId) });
        }

        public static void OnDiceResolved(IThreeKingdomsCardHost host, string eventId, bool isBlack, string[] progressRecipients,
            bool actualRoll = true, bool formalRoll = true, bool finalResult = true, int? observedTotal = null)
        {
            if (!Enabled(host) || !actualRoll || !finalResult) return; var s = host.State;
            if (!Mark(s, "dice:" + eventId)) return;
            var frames = new List<B2SkillFrame>();
            foreach (var player in Order(s))
            {
                var g = Active(s, player); if (g == null) continue;
                if (g.GeneralId == "tk-g33" && formalRoll && !isBlack && !(progressRecipients ?? Array.Empty<string>()).Contains(player))
                    frames.Add(Frame(s, g, "tk-g33-s1", "resource", 1, eventId));
                if (g.GeneralId == "tk-g43" && new[] { 3, 5, 6, 8, 10, 12 }.Contains(observedTotal ?? host.FinalDiceTotal))
                {
                    var counter = Counter(g, "qinqin", 0); var step = counter.Value; counter.Value = (step + 1) % 5;
                    frames.Add(Frame(s, g, "tk-g43-s1", new[] { "random", "resource", "commodity", "resource", "progress-choice" }[step], 1, eventId));
                }
            }
            if (s.Skills.HarvestActive && s.Skills.HarvestStage == 0) s.Skills.AfterGoldFrames = s.Skills.AfterGoldFrames.Concat(frames).ToArray();
            else foreach (var frame in frames) Append(s, frame);
            Pump(host);
        }
        public static void OnProgressUsed(IThreeKingdomsCardHost host, string eventId, string playerId)
        {
            if (!Enabled(host)) return; var s = host.State; var g = Active(s, playerId);
            if (g?.GeneralId != "tk-g33" || !Mark(s, "progress-used:" + eventId)) return;
            Append(s, Frame(s, g, "tk-g33-s1", "random", 1, eventId)); Pump(host);
        }

        public static void Pump(IThreeKingdomsCardHost host)
        {
            if (!Enabled(host) || host.State.PendingDecision != null || host.State.Skills.SuppressPump) return;
            var s = host.State;
            if (s.Skills.PendingDecision != null)
            {
                var pendingFrame = s.Skills.Frames.Single(f => f.Id == s.Skills.PendingDecision.FrameId);
                if ((pendingFrame.Optional || s.Skills.PendingDecision.Kind == "harvest" || s.Skills.PendingDecision.Kind == "luoying") && !IsB4ResidualOptional(s,pendingFrame) && !CivilResidualOptional(s,pendingFrame) && Active(s, pendingFrame.PlayerId)?.TenureId != pendingFrame.TenureId)
                {
                    if (s.Skills.PendingDecision.Kind == "harvest") Allocation(s, pendingFrame.PlayerId).ChoiceComplete = true;
                    Remove(s, pendingFrame); s.Skills.PendingDecision = null;
                }
                else
                {
                if (s.Skills.PendingDecision.WindowId != s.WindowId) { s.Skills.PendingDecision.WindowId = s.WindowId; s.Skills.PendingDecision.DecisionId = Id(s, "skill-decision"); }
                return;
                }
            }
            for (var guard = 0; guard < 10000; guard++)
            {
                if (s.Skills.Frames.Length == 0)
                {
                    if (s.Skills.HarvestActive) { ContinueHarvest(host); if (s.Skills.Frames.Length == 0 && !s.Skills.HarvestActive) return; }
                    else return;
                }
                var frame = s.Skills.Frames.First();
                if (TryB4Order(s, frame)) return;
                if (frame.Optional)
                {
                    if (!IsB4ResidualOptional(s,frame) && !CivilResidualOptional(s,frame) && Active(s, frame.PlayerId)?.TenureId != frame.TenureId) { Remove(s, frame); continue; }
                    Decide(s, frame, "optional", "发动" + Name(frame.SkillId), "可选择发动或放弃。"); return;
                }
                if (!frame.Started && frame.TenureId != null && frame.Kind != "harvest" && frame.Kind != "claim" && frame.Kind != "raid" && frame.Kind != "gold" && Active(s, frame.PlayerId)?.TenureId != frame.TenureId)
                { Remove(s, frame); continue; }
                frame.Started = true;
                if (B4Enabled(s) && frame.SkillId != null && !new[] { "harvest", "gold", "claim", "raid", "luoying" }.Contains(frame.Kind)) B4SkillHistoryEngine.Begin(s, frame.Id, frame.B4?.OwnerPlayerId ?? frame.PlayerId, frame.SkillId, frame.TenureId, frame.SourceEventId);
                if (ProcessFrame(host, frame)) return;
            }
            throw new InvalidOperationException("技能结算超过安全步数，请检查事件循环。");
        }

        private static bool ProcessFrame(IThreeKingdomsCardHost host, B2SkillFrame f)
        {
            var s = host.State;
            if (ProcessB5MilitaryFrame(host, f, out var militaryWaiting)) return militaryWaiting;
            if (ProcessCivilFrame(host, f, out var civilWaiting)) return civilWaiting;
            if (ProcessB4Frame(host, f, out var b4Waiting)) return b4Waiting;
            if (ProcessTimeFrame(host, f, out var waiting)) return waiting;
            if (new[] { "resource", "mixed", "commodity", "lianying", "jushou" }.Contains(f.Kind))
            {
                var types = ChoiceTypes(f).Where(t => host.GetBank()[t] > 0).ToArray();
                if (f.Remaining > 0 && types.Length > 0 && !IsDrawBlocked(s, f.PlayerId))
                { Decide(s, f, "card", Name(f.SkillId) + "：选牌", "请选择一张可用牌，还需选择 " + f.Remaining + " 张。"); return true; }
                if (f.Kind == "lianying") Add(f.Cards, TakeRandom(host, f.PlayerId, 1));
                Remove(s, f); Gain(host, f.PlayerId, f.Cards, f.SkillId, HistorySource(f));
                if (f.Kind == "jushou" && !s.Skills.TimedEffects.Any(e => e.Kind == "draw-block" && e.PlayerId == f.PlayerId))
                    AddTimed(s, "draw-block", f.PlayerId, f.PlayerId, 0);
                return false;
            }
            if (f.Kind == "random") { Remove(s, f); Gain(host, f.PlayerId, TakeRandom(host, f.PlayerId, f.Remaining), f.SkillId, HistorySource(f)); return false; }
            if (f.Kind == "keji")
            {
                Remove(s, f); Gain(host, f.PlayerId, TakeRandom(host, f.PlayerId, 1), f.SkillId, HistorySource(f));
                if (!s.Skills.TimedEffects.Any(e => e.Kind == "protection" && e.PlayerId == f.PlayerId && e.OpportunityId == s.Clock.OpportunityId + 1))
                    AddTimed(s, "protection", f.PlayerId, null, s.Clock.OpportunityId + 1);
                return false;
            }
            if (f.Kind == "zhenggu" && f.Step == 0)
            { Decide(s, f, "target", "镇骨：选择目标", "选择另一名玩家，现在与其下次实际出牌阶段各追平一次手牌。"); return true; }
            if (f.Kind == "zhenggu" || f.Kind == "zhenggu-later")
            {
                var amount = Math.Max(0, host.GetHand(f.Targets[0]).Sum() - host.GetHand(f.PlayerId).Sum());
                Remove(s, f); Gain(host, f.PlayerId, TakeRandom(host, f.PlayerId, amount), f.SkillId, HistorySource(f));
                if (f.Kind == "zhenggu") AddTimed(s, "zhenggu", f.PlayerId, f.Targets[0], 0);
                return false;
            }
            if (f.Kind == "gift")
            {
                var before = host.GetHand(f.PlayerId).Sum(); var targetBefore = host.GetHand(f.Targets[0]).Sum();
                Require(Contains(host.GetHand(f.PlayerId), f.Cards), "原赠予费用已不再持有。");
                host.RemoveHand(f.PlayerId, f.Cards); host.AddHand(f.Targets[0], f.Cards); Remove(s, f);
                var loss = Change(f.PlayerId, before, host.GetHand(f.PlayerId).Sum(), f.Cards.Sum(), f.Cards.Sum());
                var gain = Change(f.Targets[0], targetBefore, host.GetHand(f.Targets[0]).Sum(), 0, 0);
                if (B4Enabled(s)) { loss.LostCards = Copy(f.Cards); gain.GainedCards = Copy(f.Cards); gain.SourcePlayerId = f.PlayerId; }
                RecordCards(host, f.Id, new[] { loss, gain }); return false;
            }
            if (f.Kind == "exchange-hand" || f.Kind == "discard")
            {
                if (B3Enabled(s) && f.PlayerId != null)
                {
                    var exchange = f.Kind == "exchange-hand"; host.AddBank(f.Cards); Remove(s, f);
                    if (exchange) Append(s, new B2SkillFrame { Id = Id(s, "frame"), Kind = "random", PlayerId = f.PlayerId, Remaining = f.Remaining, Started = true, SourceEventId = B4Enabled(s)?f.Id:null });
                    HoldReturnedCards(host, f.PlayerId, "discard", f.Id, f.Cards); return false;
                }
                host.AddBank(f.Cards); Remove(s, f);
                if (f.Kind == "exchange-hand") Gain(host, f.PlayerId, TakeRandom(host, f.PlayerId, f.Remaining), f.SkillId, HistorySource(f));
                return false;
            }
            if (f.Kind == "exchange-progress")
            {
                Remove(s, f);
                if (!IsDrawBlocked(s, f.PlayerId) && host.GetProgressDeckCount(f.ProgressCard.Color) > 0) host.DrawProgress(f.PlayerId, f.ProgressCard.Color);
                host.ReturnProgress(f.ProgressCard); return false;
            }
            if (f.Kind == "progress-choice")
            {
                if (IsDrawBlocked(s, f.PlayerId) || Enumerable.Range(0, 3).All(c => host.GetProgressDeckCount(c) == 0)) { Remove(s, f); return false; }
                Decide(s, f, "color", "勤政：选择发展牌颜色", "选择一个非空颜色，领取该颜色牌库顶牌。"); return true;
            }
            return ProcessHarvestFrame(host, f);
        }

        public static B2SkillAction[] Actions(IThreeKingdomsCardHost host, string playerId)
        {
            if (!Enabled(host) || host.State.PendingDecision != null || !host.State.SeatPlayerIds.Contains(playerId)) return Array.Empty<B2SkillAction>();
            var s = host.State; var result = new List<B2SkillAction>(); var d = s.Skills.PendingDecision;
            if (d != null)
            {
                if (d.PlayerId != playerId) return result.ToArray();
                var f = s.Skills.Frames.Single(x => x.Id == d.FrameId);
                if (d.Kind == "optional") { result.Add(Action(s, f.SkillId, "accept", "发动", d)); result.Add(Action(s, f.SkillId, "skip", "放弃", d)); }
                else if (d.Kind == "card")
                    foreach (var t in ChoiceTypes(f).Where(t => host.GetBank()[t] > 0)) result.Add(CardAction(s, f, d, t));
                else if (d.Kind == "target")
                    foreach (var p in s.SeatPlayerIds.Where(p => p != playerId)) { var a = Action(s, f.SkillId, "target", "选择 " + p, d); a.Command.TargetPlayerIds = new[] { p }; result.Add(a); }
                else if (d.Kind == "color")
                    foreach (var color in Enumerable.Range(0, 3).Where(c => host.GetProgressDeckCount(c) > 0)) { var a = Action(s, f.SkillId, "color", new[] { "黄色贸易", "蓝色政治", "绿色科学" }[color], d); a.Command.CardType = color; result.Add(a); }
                else if (!AddB5MilitaryDecisionActions(host,f,d,result) && !AddCivilDecisionActions(host,f,d,result) && !AddB4DecisionActions(host, f, d, result) && !AddTimeDecisionActions(host, f, d, result)) AddHarvestActions(host, f, d, result);
                return AnnotateB4Actions(host, result);
            }
            if (s.Skills.Frames.Length != 0 || !s.OpeningComplete || s.Clock.Round == 0 || s.ReplacementInProgress) return result.ToArray();
            var g = Active(s, playerId); if (g == null) return result.ToArray();
            var ownTurn = s.Clock.ActivePlayerId == playerId;
            var hand = host.GetHand(playerId);
            AddTimeActions(host, g, result);
            AddB4Actions(host, g, result);
            AddB5MilitaryActions(host, g, result);
            AddCivilActions(host, g, result);
            if (B5Enabled(s) && ownTurn && s.Clock.Phase == ThreeKingdomsPhase.Play && host is IThreeKingdomsB5Host board)
                foreach (var option in board.GetBoardOptions(playerId, "repair-own"))
                {
                    var a = Action(s, null, "b5-repair", option.Label+"；费用用途："+string.Join("、",Enumerable.Range(0,8).Where(t=>option.Cost[t]>0).Select(t=>CardNames[t]+"×"+option.Cost[t])));
                    a.Command.ProgressCardId = option.Id;
                    var matrix=B4PaymentAllocator.Allocate(s,playerId,hand,option.Cost);
                    a.Command.Counts=Enumerable.Range(0,8).Select(t=>Enumerable.Range(0,8).Sum(u=>matrix[t*8+u])).ToArray();
                    a.CardSelectionKind="payment";result.Add(a);
                }
            if (g.GeneralId == "tk-g15" && ownTurn && s.Clock.Phase != ThreeKingdomsPhase.RoundEnd && s.Clock.Phase != ThreeKingdomsPhase.Finished)
                foreach (var target in s.SeatPlayerIds.Where(p => p != playerId)) foreach (var t in Enumerable.Range(0, 8).Where(t => hand[t] > 0))
                { var a = Action(s, "tk-g15-s1", "gift", "仁德：给 " + target + " " + CardNames[t]); a.Command.TargetPlayerIds = new[] { target }; a.Command.Counts[t] = 1; result.Add(a); }
            if (s.Clock.Phase == ThreeKingdomsPhase.Play && g.GeneralId == "tk-g23" && Value(g, "qianxun", s.Clock.PhaseId) == 0)
                foreach (var t in Enumerable.Range(0, 8).Where(t => hand[t] > 0)) { var a = Action(s, "tk-g23-s2", "discard", "谦逊：弃" + CardNames[t]); a.Command.Counts[t] = 1; result.Add(a); }
            if (ownTurn && s.Clock.Phase == ThreeKingdomsPhase.Play && g.GeneralId == "tk-g22" && !IsDrawBlocked(s, playerId))
            {
                if (Value(g, "zhiheng-hand", s.Clock.TurnInstanceId) == 0)
                    foreach (var t in Enumerable.Range(0, 8).Where(t => hand[t] > 0))
                    { var a = Action(s, "tk-g22-s1", "exchange-hand", "制衡：弃" + CardNames[t] + "换资源"); a.Command.Counts[t] = 1; result.Add(a); }
                if (Value(g, "zhiheng-progress", s.Clock.TurnInstanceId) == 0)
                    foreach (var card in host.GetProgressCards(playerId).Where(c => !c.PublicPoint && host.GetProgressDeckCount(c.Color) > 0).GroupBy(c => c.Id).Select(x => x.First()))
                    { var a = Action(s, "tk-g22-s1", "exchange-progress", "制衡：同色换抽 " + card.Id); a.Command.ProgressCardId = card.Id; result.Add(a); }
            }
            return AnnotateB4Actions(host, result);
        }

        public static void Resolve(IThreeKingdomsCardHost host, string playerId, B2SkillCommand command)
        {
            Require(Enabled(host) && command != null && host.State.PendingDecision == null, "当前不能操作三国技能。");
            var s = host.State; Require(command.WindowId == s.WindowId && !string.IsNullOrEmpty(command.WindowId), "技能操作窗口已经过期。");
            var d = s.Skills.PendingDecision;
            if (d != null)
            {
                Require(d.PlayerId == playerId && d.DecisionId == command.DecisionId && d.WindowId == command.WindowId, "不是你的技能选择或选择已经过期。");
                var f = s.Skills.Frames.Single(x => x.Id == d.FrameId); Require(command.SkillId == f.SkillId, "技能身份不匹配。");
                if (d.Kind == "optional")
                {
                    Require(command.Option == "accept" || command.Option == "skip", "无效的发动选择。");
                    if (command.Option == "skip") Remove(s, f); else { ValidateTimeOptional(host, f); f.Optional = false; f.Started = true; }
                }
                else if (d.Kind == "card")
                {
                    Require(command.Option == "card" && ChoiceTypes(f).Contains(command.CardType) && host.GetBank()[command.CardType] > 0 && !IsDrawBlocked(s, playerId), "无法领取所选牌。");
                    var selected = One(command.CardType); host.RemoveBank(selected); Add(f.Cards, selected); f.Remaining--;
                }
                else if (d.Kind == "target")
                {
                    Require(command.Option == "target" && command.TargetPlayerIds?.Length == 1 && command.TargetPlayerIds[0] != playerId && s.SeatPlayerIds.Contains(command.TargetPlayerIds[0]), "镇骨目标无效。");
                    f.Targets = (string[])command.TargetPlayerIds.Clone(); f.Step = 1; Target(host, f.Id, playerId, f.Targets[0]);
                }
                else if (d.Kind == "color")
                {
                    Require(command.Option == "color" && command.CardType >= 0 && command.CardType < 3 && host.GetProgressDeckCount(command.CardType) > 0 && !IsDrawBlocked(s, playerId), "该发展牌颜色不可领取。");
                    Remove(s, f); host.DrawProgress(playerId, command.CardType);
                }
                else if (!ResolveB5MilitaryDecision(host,f,d,command) && !ResolveCivilDecision(host,f,d,command) && !ResolveB4Decision(host, f, d, command) && !ResolveTimeDecision(host, f, d, command)) ResolveHarvest(host, f, d, command);
                s.Skills.PendingDecision = null;
            }
            else ResolveActive(host, playerId, command);
            Refresh(s); Pump(host);
        }

        private static void ResolveActive(IThreeKingdomsCardHost host, string playerId, B2SkillCommand c)
        {
            var s = host.State; Require(s.Skills.Frames.Length == 0 && !s.ReplacementInProgress && s.OpeningComplete && s.Clock.Round > 0, "技能结算期间不可另开主动技能。");
            var g = Active(s, playerId); Require(g != null, "没有在场武将。");
            Require(!B4Enabled(s) || !new[] { "gift", "discard", "exchange-hand", "exchange-progress" }.Contains(c.Option) || !IsB4HandLossForbidden(s, playerId), "本次出牌阶段禁止失去手牌或发展牌。");
            var own = s.Clock.ActivePlayerId == playerId; var play = s.Clock.Phase == ThreeKingdomsPhase.Play;
            if (c.Option == "b5-repair")
            {
                Require(B5Enabled(s) && own && play && host is IThreeKingdomsB5Host && c.SkillId == null, "当前不能修复建筑。");
                var board = (IThreeKingdomsB5Host)host;
                var option = board.GetBoardOptions(playerId,"repair-own").SingleOrDefault(o=>o.Id==c.ProgressCardId);
                Require(option != null, "修复目标、费用或库存已经变化。"); board.ApplyBoardOption(playerId,option); return;
            }
            if (ResolveB5MilitaryActive(host, g, c)) return;
            if (ResolveCivilActive(host, g, c)) return;
            if (ResolveTimeActive(host, g, c)) return;
            if (ResolveB4Active(host, g, c)) return;
            if (c.Option == "gift")
            {
                Require(g.GeneralId == "tk-g15" && c.SkillId == "tk-g15-s1" && own && s.Clock.Phase != ThreeKingdomsPhase.RoundEnd && s.Clock.Phase != ThreeKingdomsPhase.Finished, "当前不能发动仁德。");
                Require(c.TargetPlayerIds?.Length == 1 && c.TargetPlayerIds[0] != playerId && s.SeatPlayerIds.Contains(c.TargetPlayerIds[0]), "赠予目标无效。");
                Require(Valid(c.Counts) && c.Counts.Sum() > 0 && Contains(host.GetHand(playerId), c.Counts), "赠予手牌不足或数量无效。");
                var f = Frame(s, g, c.SkillId, "gift", 0, null); f.Started = true; f.Cards = Copy(c.Counts); f.Targets = (string[])c.TargetPlayerIds.Clone(); Append(s, f); Target(host, f.Id, playerId, f.Targets[0]); return;
            }
            if (c.Option == "discard" || c.Option == "exchange-hand")
            {
                var discard = c.Option == "discard";
                Require(play && (discard ? g.GeneralId == "tk-g23" && c.SkillId == "tk-g23-s2" : g.GeneralId == "tk-g22" && c.SkillId == "tk-g22-s1" && own && !IsDrawBlocked(s, playerId)), "当前不能发动此弃牌技能。");
                var key = discard ? "qianxun" : "zhiheng-hand"; var clock = discard ? s.Clock.PhaseId : s.Clock.TurnInstanceId;
                Require(Value(g, key, clock) == 0 && Valid(c.Counts) && c.Counts.Sum() >= 1 && c.Counts.Sum() <= (discard ? 1 : 4) && Contains(host.GetHand(playerId), c.Counts), "次数已用、费用不足或弃牌数量无效。");
                var f = Frame(s, g, c.SkillId, c.Option, c.Counts.Sum(), null); f.Started = true; f.Cards = Copy(c.Counts);
                Counter(g, key, clock).Value = 1; var before = host.GetHand(playerId).Sum(); host.RemoveHand(playerId, c.Counts); Append(s, f);
                RecordCards(host, f.Id + ":cost", new[] { Change(playerId, before, host.GetHand(playerId).Sum(), c.Counts.Sum(), 0) }); return;
            }
            if (c.Option == "exchange-progress")
            {
                Require(play && own && g.GeneralId == "tk-g22" && c.SkillId == "tk-g22-s1" && !IsDrawBlocked(s, playerId) && Value(g, "zhiheng-progress", s.Clock.TurnInstanceId) == 0, "当前不能同色换抽发展牌。");
                var card = host.GetProgressCards(playerId).FirstOrDefault(x => x.Id == c.ProgressCardId && !x.PublicPoint);
                Require(card != null && host.GetProgressDeckCount(card.Color) > 0, "所选发展牌或对应色库无效。");
                var f = Frame(s, g, c.SkillId, "exchange-progress", 0, null); f.Started = true; f.ProgressCard = card;
                Counter(g, "zhiheng-progress", s.Clock.TurnInstanceId).Value = 1; host.RemoveProgress(playerId, card.Id); Append(s, f); return;
            }
            throw new InvalidOperationException("未知或不可用的主动技能。");
        }

        public static string[] Validate(IThreeKingdomsCardHost host)
        {
            var errors = new List<string>(); var s = host.State?.Skills; if (s == null) return new[] { "缺少技能存档。" };
            if (s.Frames == null || s.TimedEffects == null || s.LossLedger == null || s.ProcessedEvents == null || s.Harvest == null || !Valid(s.RevealedCards)) return new[] { "技能存档缺少队列或临时区。" };
            if (s.Frames.Any(f => f == null || string.IsNullOrEmpty(f.Id) || !Valid(f.Cards) || !Valid(f.SelectedCards) || f.Targets == null || f.Tiles == null || f.Remaining < 0) || s.Frames.Select(f => f.Id).Distinct().Count() != s.Frames.Length) errors.Add("技能效果帧无效或重复。");
            if (s.Frames.Where(f => f?.B4 != null).Any(f => !B4Enabled(host.State) || !Valid(f.B4.Selection) || !Valid(f.B4.OtherSelection) || f.B4.LockedTargets == null || f.B4.LockedTargets.Any(p => !host.State.SeatPlayerIds.Contains(p)) || f.B4.OwnerPlayerId != null && !host.State.SeatPlayerIds.Contains(f.B4.OwnerPlayerId))) errors.Add("B4 技能选择、所有者或规则版本无效。");
            if (s.Harvest.Any(h => h == null || !Valid(h.Cards) || h.GoldCount < 0 || h.ActuallyReceived < 0) || s.Harvest.Select(h => h.PlayerId).Distinct().Count() != s.Harvest.Length) errors.Add("技能产出预留无效。");
            if (s.PendingDecision != null && (s.Frames.All(f => f.Id != s.PendingDecision.FrameId) || !host.State.SeatPlayerIds.Contains(s.PendingDecision.PlayerId) || string.IsNullOrEmpty(s.PendingDecision.DecisionId))) errors.Add("技能待决没有对应效果帧。");
            if (s.TimedEffects.Any(e => e == null || !host.State.SeatPlayerIds.Contains(e.PlayerId) || e.OpportunityId < 0) || s.LossLedger.Any(e => e == null || e.LostHandCards < 0)) errors.Add("技能期限或失牌账本无效。");
            if (!Valid(host.GetBank()) || host.State.SeatPlayerIds.Any(id => !Valid(host.GetHand(id)))) errors.Add("宿主手牌或供应无效。");
            if (s.ProcessedSkippedCount < 0 || s.ProcessedSkippedCount > host.State.Clock.SkippedOpportunityPlayerIds.Length) errors.Add("技能跳过机会游标无效。");
            if (Returned(host.State).Any(r => r == null || !Valid(r.Cards) || !host.State.SeatPlayerIds.Contains(r.PlayerId) || string.IsNullOrEmpty(r.EventId) || !ReturnCauses.Contains(r.Cause))) errors.Add("弃牌临时区来源或实体牌无效。");
            errors.AddRange(ValidateB4Response(host));
            errors.AddRange(ValidateB5Military(host.State));
            errors.AddRange(ValidateCivil(host.State));
            return errors.ToArray();
        }

        public static int[] EscrowCards(ThreeKingdomsState state)
        {
            var cards = new int[8]; if (state?.Skills == null) return cards;
            foreach (var h in state.Skills.Harvest.Where(h => !h.Completed)) Add(cards, h.Cards);
            foreach (var f in state.Skills.Frames.Concat(state.Response?.SuspendedFrames ?? Array.Empty<B2SkillFrame>()).Concat(state.Military?.SuspendedFrames ?? Array.Empty<B2SkillFrame>()).Where(f => f.Kind != "gift" && f.Kind != "raid" && f.Kind != "harvest")) { Add(cards, f.Cards); Add(cards, f.SelectedCards); }
            foreach (var r in Returned(state)) Add(cards, r.Cards);
            return cards;
        }
        private static bool Enabled(IThreeKingdomsCardHost host) => host?.State?.SkillsEnabled == true;
        private static GeneralInstance Active(ThreeKingdomsState s, string id) => s?.SkillsEnabled == true ? s.Generals.FirstOrDefault(g => g.Region == GeneralRegion.Active && g.OwnerPlayerId == id && g.RemainingLife > 0) : null;
        private static string Id(ThreeKingdomsState s, string kind) => "tk-b2-" + kind + "-" + s.NextId++;
        private static void Refresh(ThreeKingdomsState s) { s.WindowId = Id(s, "window"); }
        private static bool Mark(ThreeKingdomsState s, string id) { if (s.Skills.ProcessedEvents.Contains(id)) return false; s.Skills.ProcessedEvents = s.Skills.ProcessedEvents.Concat(new[] { id }).ToArray(); return true; }
        private static string[] Order(ThreeKingdomsState s) { var index = Array.IndexOf(s.SeatPlayerIds, s.Clock.ActivePlayerId); if (index < 0) index = 0; return s.SeatPlayerIds.Skip(index).Concat(s.SeatPlayerIds.Take(index)).ToArray(); }
        private static B2SkillFrame Frame(ThreeKingdomsState s, GeneralInstance g, string skill, string kind, int count, string eventId) => new B2SkillFrame { Id = Id(s, "frame"), PlayerId = g.OwnerPlayerId, TenureId = g.TenureId, SkillId = skill, Kind = kind, Remaining = count, SourceEventId = eventId };
        private static void Append(ThreeKingdomsState s, B2SkillFrame frame) { s.Skills.Frames = s.Skills.Frames.Concat(new[] { frame }).ToArray(); }
        private static void Prepend(ThreeKingdomsState s, IEnumerable<B2SkillFrame> frames) { s.Skills.Frames = frames.Concat(s.Skills.Frames).ToArray(); }
        private static void Remove(ThreeKingdomsState s, B2SkillFrame f) { s.Skills.Frames = s.Skills.Frames.Where(x => x.Id != f.Id).ToArray(); if (B4Enabled(s) && f.SkillId != null) B4SkillHistoryEngine.Complete(s, f.Id, null); }
        private static void Decide(ThreeKingdomsState s, B2SkillFrame f, string kind, string title, string description)
        { s.Skills.PendingDecision = new B2SkillDecision { PlayerId = f.PlayerId, FrameId = f.Id, Kind = kind, Title = title, Description = description, WindowId = s.WindowId, DecisionId = Id(s, "skill-decision") }; }
        private static B2SkillAction Action(ThreeKingdomsState s, string skill, string option, string label, B2SkillDecision d = null) => new B2SkillAction { Label = label, Command = new B2SkillCommand { SkillId = skill, Option = option, WindowId = s.WindowId, DecisionId = d?.DecisionId } };
        private static B2SkillAction CardAction(ThreeKingdomsState s, B2SkillFrame f, B2SkillDecision d, int type) { var a = Action(s, f.SkillId, "card", "选择" + CardNames[type], d); a.Command.CardType = type; return a; }
        private static IEnumerable<int> ChoiceTypes(B2SkillFrame f) => f.Kind == "mixed" ? Enumerable.Range(0, 8) : f.Kind == "commodity" ? Enumerable.Range(5, 3) : Enumerable.Range(0, 5);
        private static int Value(GeneralInstance g, string key, long clock) => g.Counters.FirstOrDefault(c => c.Key == key && c.TurnInstanceId == clock)?.Value ?? 0;
        private static GeneralCounter Counter(GeneralInstance g, string key, long clock)
        { var c = g.Counters.FirstOrDefault(x => x.Key == key && x.TurnInstanceId == clock); if (c != null) return c; c = new GeneralCounter { Key = key, TurnInstanceId = clock }; g.Counters = g.Counters.Where(x => x.Key != key).Concat(new[] { c }).ToArray(); return c; }
        private static void AddTimed(ThreeKingdomsState s, string kind, string player, string target, long opportunity) { s.Skills.TimedEffects = s.Skills.TimedEffects.Concat(new[] { new B2TimedEffect { Id = Id(s, "timed"), Kind = kind, PlayerId = player, TargetPlayerId = target, OpportunityId = opportunity, CreatedTurnId = s.Clock.TurnInstanceId, SourceTenureId = B4Enabled(s) ? Active(s, player)?.TenureId : null } }).ToArray(); }
        private static B2HandChange Change(string id, int before, int after, int lost, int given) => new B2HandChange { PlayerId = id, BeforeCount = before, AfterCount = after, LostCount = lost, GivenCount = given };
        private static bool Valid(int[] cards) => cards != null && cards.Length == 8 && cards.All(c => c >= 0);
        private static bool Contains(int[] cards, int[] cost) => Valid(cost) && Enumerable.Range(0, 8).All(i => cards[i] >= cost[i]);
        private static int[] Copy(int[] cards) => (int[])cards.Clone();
        private static int[] One(int index) { var cards = new int[8]; cards[index] = 1; return cards; }
        private static void Add(int[] to, int[] from) { for (var i = 0; i < 8; i++) to[i] += from[i]; }
        private static void Subtract(int[] to, int[] from) { for (var i = 0; i < 8; i++) to[i] -= from[i]; }
        private static string HistorySource(B2SkillFrame frame) => frame.SkillId==null?frame.SourceEventId:frame.Id;
        private static void Gain(IThreeKingdomsCardHost host, string player, int[] cards, string sourceSkillId = null, string sourceEventId = null)
        {
            if (cards.Sum() <= 0) return; var before = host.GetHand(player).Sum(); host.AddHand(player, cards);
            if(B4Enabled(host.State))
            {
                string flowId=Id(host.State,"gain");var changes=new[]{new B2HandChange {PlayerId=player,BeforeCount=before,AfterCount=before+cards.Sum(),GainedCards=Copy(cards),SourceSkillId=sourceSkillId}};
                RecordCards(host,flowId,changes);B4SkillHistoryEngine.LinkCardFlow(host.State,sourceEventId,flowId,changes);
            }
        }
        private static int[] TakeRandom(IThreeKingdomsCardHost host, string player, int count)
        { var result = new int[8]; if (IsDrawBlocked(host.State, player)) return result; for (var n = 0; n < count; n++) { var bank = host.GetBank(); var total = bank.Take(5).Sum(); if (total == 0) break; var index = host.DrawRandom(total); for (var t = 0; t < 5; t++) { if (index < bank[t]) { var one = One(t); host.RemoveBank(one); Add(result, one); break; } index -= bank[t]; } } return result; }
        private static string Name(string skill) => skill == "tk-g13-s1" ? "连破" : skill == "tk-g21-s1" ? "固症" : skill == "tk-g29-s1" ? "放逐" : skill == "tk-g29-s2" ? "颂威" : skill == "tk-g30-s1" ? "落英" : skill == "tk-g30-s2" ? "酒诗" : skill == "tk-g01-s1" ? "突袭" : skill == "tk-g06-s1" ? "据守" : skill == "tk-g09-s1" ? "镇骨" : skill == "tk-g12-s1" ? "奋音" : skill == "tk-g15-s1" ? "仁德" : skill == "tk-g22-s1" ? "制衡" : skill == "tk-g23-s1" ? "连营" : skill == "tk-g23-s2" ? "谦逊" : skill == "tk-g28-s1" ? "涉猎" : skill == "tk-g28-s2" ? "克己" : skill == "tk-g37-s1" ? "天妒" : skill == "tk-g37-s2" ? "遗计" : skill == "tk-g33-s1" ? "集智" : skill == "tk-g43-s1" ? "勤政" : GeneralCatalog.Definitions.SelectMany(g => g.Skills).FirstOrDefault(s => s.Id == skill)?.Name ?? "摸牌";
        private static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
    }
}
