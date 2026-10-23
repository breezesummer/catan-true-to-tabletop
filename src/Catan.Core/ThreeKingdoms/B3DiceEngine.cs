using System;
using System.Collections.Generic;
using System.Linq;

namespace Catan.Core.ThreeKingdoms
{
    /// <summary>投掷、候选修改与最终提交分离；只有宿主消费最终结果后才结算棋盘。</summary>
    public static class B3DiceEngine
    {
        private static readonly string[] CardNames = { "木", "砖", "羊", "麦", "矿", "布", "币", "纸" };
        private static bool Enabled(ThreeKingdomsState s) => s?.SkillsEnabled == true && (s.RulesVersion == GeneralCatalog.B3RulesVersion || s.RulesVersion == GeneralCatalog.B4RulesVersion);
        public static bool HasPending(ThreeKingdomsState s) => Enabled(s) && s.Dice != null &&
            (s.Dice.StartWindowOpen || s.Dice.PendingDecision != null || (s.Dice.Roll != null && !s.Dice.Roll.Submitted));

        // 回合机会通过翻面检查后调用，早于准备阶段的其他可选操作；不能为跳过的机会调用。
        public static void BeginTurn(IThreeKingdomsCardHost host)
        {
            var s = host.State; if (!Enabled(s)) return;
            var dice = s.Dice ?? (s.Dice = new B3DiceState());
            if (dice.OpenedTurnId == s.Clock.TurnInstanceId) return;
            Require(!HasPending(s), "上一次骰子选择尚未完成。");
            dice.OpenedTurnId = s.Clock.TurnInstanceId; dice.FixedResult = null;
            var owners = Order(s).Select(p => Active(s, p)).Where(g => g?.GeneralId == "tk-g21" && Used(g) < 3).ToArray();
            dice.FixedResponders = owners.Select(g => g.OwnerPlayerId).ToArray();
            dice.FixedResponderTenures = owners.Select(g => g.TenureId).ToArray();
            dice.FixedResponderCursor = 0; dice.StartWindowOpen = owners.Length > 0; Pump(host);
        }

        public static void BeginRoll(IThreeKingdomsCardHost host, string playerId, string eventId, int fixedRed = 0, int fixedWhite = 0)
        {
            var s = host.State;
            Require(Enabled(s) && s.OpeningComplete && s.Clock.Phase == ThreeKingdomsPhase.Roll && s.Clock.ActivePlayerId == playerId,
                "当前不是你的正式掷骰阶段。");
            Require((fixedRed == 0 && fixedWhite == 0) || (Die(fixedRed) && Die(fixedWhite)), "固定数字骰必须同时在 1 至 6 之间。");
            ValidateBegin(host, playerId, eventId);
            var dice = s.Dice ?? (s.Dice = new B3DiceState());
            Require(dice.FormalRollTurnId != s.Clock.TurnInstanceId, "本回合的正式骰局已经开始。");
            Require(dice.FixedResult == null || fixedRed == 0, "直谏已固定本回合结果，不能再覆盖数字骰。");
            dice.FormalRollTurnId = s.Clock.TurnInstanceId;
            if (dice.FixedResult != null)
            {
                var result = CopyResult(dice.FixedResult); result.EventId = eventId; result.PlayerId = playerId;
                dice.Roll = new B3DiceRoll { EventId = eventId, PlayerId = playerId, Purpose = B3DicePurpose.Formal, Stage = "ready", Result = result,
                    FixedRed = result.Red, FixedWhite = result.White, TurnInstanceId = s.Clock.TurnInstanceId };
                return;
            }
            Begin(host, playerId, eventId, B3DicePurpose.Formal, fixedRed, fixedWhite);
        }

        public static void BeginJudgment(IThreeKingdomsCardHost host, string playerId, string eventId, bool comparison = false)
        {
            ValidateBegin(host, playerId, eventId);
            if (host.State.Dice == null) host.State.Dice = new B3DiceState();
            Begin(host, playerId, eventId, comparison ? B3DicePurpose.Comparison : B3DicePurpose.Judgment, 0, 0);
        }

        private static void ValidateBegin(IThreeKingdomsCardHost host, string playerId, string eventId)
        {
            var s = host.State;
            Require(Enabled(s) && s.SeatPlayerIds.Contains(playerId) && !string.IsNullOrWhiteSpace(eventId), "骰局来源或玩家无效。");
            Require(s.PendingDecision == null && !s.ReplacementInProgress && !B2SkillEngine.HasPending(s) && !HasPending(s), "请先完成当前待决。");
            Require(s.Dice?.Roll?.EventId != eventId, "同一骰局不能重复开始。");
        }

        private static void Begin(IThreeKingdomsCardHost host, string playerId, string eventId, B3DicePurpose purpose, int fixedRed, int fixedWhite)
        {
            var s = host.State; var g = Active(s, playerId);
            var observe = purpose == B3DicePurpose.Formal && g?.GeneralId == "tk-g19";
            s.Dice.Roll = new B3DiceRoll { EventId = eventId, PlayerId = playerId, Purpose = purpose, FixedRed = fixedRed, FixedWhite = fixedWhite,
                ObserverTenure = observe ? g.TenureId : null, Stage = observe ? "observe" : "rolling", TurnInstanceId = s.Clock.TurnInstanceId };
            Pump(host);
        }

        // 提供后续重掷技能复用；旧尝试保留供存档核查，不发最终结果观察事件。
        public static void Reroll(IThreeKingdomsCardHost host, string eventId)
        {
            var s = host.State; var roll = s?.Dice?.Roll;
            Require(Enabled(s) && roll != null && roll.EventId == eventId && roll.Stage == "ready" && !roll.Submitted &&
                roll.Result.NumericActualRoll && s.PendingDecision == null && !B2SkillEngine.HasPending(s), "当前骰局不能重掷。");
            roll.Attempt++; roll.CandidateCursor = 0; roll.Result = null; roll.SelectedIndices = new[] { -1, -1, -1 }; roll.ChoiceCursor = 0;
            roll.Stage = "rolling"; Pump(host);
        }

        public static B3DiceResult TakeFinal(IThreeKingdomsCardHost host)
        {
            var s = host.State; var roll = s?.Dice?.Roll;
            if (!Enabled(s) || roll == null || roll.Stage != "ready" || roll.Submitted || s.Dice.PendingDecision != null || B2SkillEngine.HasPending(s) || s.PendingDecision != null) return null;
            if (OpenLuoshen(host)) return null;
            roll.Submitted = true; return CopyResult(roll.Result);
        }

        public static void RefreshDecision(ThreeKingdomsState s)
        {
            var d = s?.Dice?.PendingDecision;
            if (d != null && d.WindowId != s.WindowId) { d.WindowId = s.WindowId; d.DecisionId = Id(s, "decision"); }
        }

        public static void Pump(IThreeKingdomsCardHost host)
        {
            var s = host.State; if (!Enabled(s) || s.Dice == null || s.PendingDecision != null || B2SkillEngine.HasPending(s)) return;
            var dice = s.Dice;
            if (dice.Roll != null)
                foreach (var modification in dice.Roll.Modifications.Where(m => !m.Applied))
                {
                    var candidate = dice.Roll.Candidates.Single(c => c.Attempt == modification.Attempt && c.Index == modification.CandidateIndex);
                    if (modification.DieIndex == 0) candidate.Red = modification.After; else candidate.White = modification.After;
                    modification.Applied = true; dice.Roll.ConsecutivePasses = 0;
                    dice.Roll.ResponderCursor = (dice.Roll.ResponderCursor + 1) % dice.Roll.Responders.Length;
                }
            RefreshDecision(s);
            if (dice.PendingDecision != null)
            {
                var d = dice.PendingDecision; var g = Active(s, d.PlayerId);
                if (d.Kind == "dice-fixed" && (g?.TenureId != dice.FixedResponderTenures[dice.FixedResponderCursor] || g.GeneralId != "tk-g21" || Used(g) >= 3))
                { dice.PendingDecision = null; dice.FixedResponderCursor++; }
                else if (d.Kind == "dice-observe" && g?.TenureId != dice.Roll.ObserverTenure)
                { dice.PendingDecision = null; dice.Roll.Stage = "rolling"; }
                else if (d.Kind == "dice-modify" && g?.TenureId != dice.Roll.ResponderTenures[dice.Roll.ResponderCursor])
                { dice.PendingDecision = null; Pass(dice.Roll); }
                else if (d.Kind == "dice-luoshen" && (g?.GeneralId != "tk-g44" || g.OwnerPlayerId != dice.Roll.PlayerId)) dice.PendingDecision = null;
                else return;
            }
            while (true)
            {
                if (dice.StartWindowOpen)
                {
                    if (dice.FixedResponderCursor >= dice.FixedResponders.Length) { dice.StartWindowOpen = false; return; }
                    var player = dice.FixedResponders[dice.FixedResponderCursor]; var general = Active(s, player);
                    if (general?.GeneralId != "tk-g21" || general.TenureId != dice.FixedResponderTenures[dice.FixedResponderCursor] || Used(general) >= 3)
                    { dice.FixedResponderCursor++; continue; }
                    SetDecision(s, player, "dice-fixed", "直谏", "可指定本回合为任意事件颜色的 7；选择红骰后白骰补足 7。"); return;
                }
                var r = dice.Roll; if (r == null || r.Submitted) return; if (r.Stage == "ready") { OpenLuoshen(host); return; }
                if (r.Stage == "observe")
                { SetDecision(s, r.PlayerId, "dice-observe", "观星", "可为每颗实际投掷的骰子投出两个候选，再逐颗选择。"); return; }
                if (r.Stage == "rolling")
                {
                    var c = new B3DiceCandidate { Attempt = r.Attempt, Index = r.CandidateCursor };
                    c.Red = c.OriginalRed = r.FixedRed != 0 ? r.FixedRed : host.DrawRandom(6) + 1;
                    c.White = c.OriginalWhite = r.FixedWhite != 0 ? r.FixedWhite : host.DrawRandom(6) + 1;
                    c.EventFace = c.OriginalEventFace = r.Purpose == B3DicePurpose.Formal ? host.DrawRandom(6) + 1 : 0;
                    r.Candidates = r.Candidates.Concat(new[] { c }).ToArray();
                    var owners = r.FixedRed != 0 ? Array.Empty<GeneralInstance>() : Order(s).Select(p => Active(s, p)).Where(g => g?.GeneralId == "tk-g13").ToArray();
                    r.Responders = owners.Select(g => g.OwnerPlayerId).ToArray(); r.ResponderTenures = owners.Select(g => g.TenureId).ToArray();
                    r.ResponderCursor = 0; r.ConsecutivePasses = 0; r.Stage = "modify";
                }
                if (r.Stage == "modify")
                {
                    if (r.ConsecutivePasses < r.Responders.Length)
                    {
                        var player = r.Responders[r.ResponderCursor]; var g = Active(s, player);
                        if (g?.GeneralId != "tk-g13" || g.TenureId != r.ResponderTenures[r.ResponderCursor] || !ModificationChoices(host, player).Any()) { Pass(r); continue; }
                        var c = Current(r);
                        SetDecision(s, player, "dice-modify", "鬼才", "第 " + r.Attempt + " 次尝试，候选 " + (r.CandidateCursor + 1) + "：红 " + c.Red + "、白 " + c.White + "。可弃 1 张真实手牌改一颗数字骰；候选落选不退费用。"); return;
                    }
                    r.CandidateCursor++;
                    if (r.CandidateCursor < r.CandidateCount) { r.Stage = "rolling"; continue; }
                    if (r.CandidateCount == 1) { r.SelectedIndices = new[] { 0, 0, 0 }; Finish(r); OpenLuoshen(host); return; }
                    r.ChoiceCursor = r.FixedRed == 0 ? 0 : 2; r.Stage = "select";
                }
                if (r.Stage == "select")
                {
                    if (r.ChoiceCursor >= 3) { Finish(r); OpenLuoshen(host); return; }
                    SetDecision(s, r.PlayerId, "dice-select", "观星", "选择" + new[] { "红色数字骰", "白色数字骰", "事件骰" }[r.ChoiceCursor] + "的最终候选。"); return;
                }
            }
        }

        private static void Finish(B3DiceRoll r)
        {
            var candidates = r.Candidates.Where(c => c.Attempt == r.Attempt).OrderBy(c => c.Index).ToArray();
            r.Result = new B3DiceResult { EventId = r.EventId, PlayerId = r.PlayerId, Purpose = r.Purpose, Attempt = r.Attempt,
                Red = r.FixedRed != 0 ? r.FixedRed : candidates[r.SelectedIndices[0]].Red,
                White = r.FixedWhite != 0 ? r.FixedWhite : candidates[r.SelectedIndices[1]].White,
                EventFace = r.Purpose == B3DicePurpose.Formal ? candidates[r.SelectedIndices[2]].EventFace : 0,
                NumericActualRoll = r.FixedRed == 0, ActualRoll = r.FixedRed == 0, EventActualRoll = r.Purpose == B3DicePurpose.Formal };
            r.Stage = "ready";
        }

        private static bool OpenLuoshen(IThreeKingdomsCardHost host)
        {
            var s = host.State; var r = s.Dice?.Roll; var g = r == null ? null : Active(s, r.PlayerId);
            if (!B2SkillEngine.B4Enabled(s) || r == null || r.Submitted || r.Stage != "ready" || r.Purpose != B3DicePurpose.Formal || !r.Result.NumericActualRoll || r.Result.Total % 2 == 0 || g?.GeneralId != "tk-g44" || s.Clock.ActivePlayerId != r.PlayerId || r.LuoshenRewardedAttempt == r.Attempt) return false;
            r.LuoshenRewardedAttempt = r.Attempt;
            B2SkillEngine.OnLuoshenCandidate(host, r.EventId + ":luoshen:" + r.Attempt, r.PlayerId);
            SetDecision(s, r.PlayerId, "dice-luoshen", "洛神", "本次奇数奖励已结算。可以重掷整组数字与事件骰，或采用当前结果。"); return true;
        }

        public static B2SkillAction[] Actions(IThreeKingdomsCardHost host, string playerId)
        {
            var s = host.State; var dice = s?.Dice; var d = dice?.PendingDecision;
            if (!Enabled(s) || d == null || d.PlayerId != playerId || s.PendingDecision != null || B2SkillEngine.HasPending(s)) return Array.Empty<B2SkillAction>();
            var actions = new List<B2SkillAction>();
            if (d.Kind == "dice-fixed")
            {
                var g = Active(s, playerId);
                if (g?.GeneralId != "tk-g21" || g.TenureId != dice.FixedResponderTenures[dice.FixedResponderCursor] || Used(g) >= 3) return actions.ToArray();
                actions.Add(Action(s, "tk-g21-s2", "skip", "放弃直谏"));
                foreach (var face in new[] { 1, 4, 5, 6 }) for (var red = 1; red <= 6; red++)
                { var a = Action(s, "tk-g21-s2", "fixed", "直谏：" + EventName(face) + " 7（红 " + red + "，白 " + (7 - red) + "）"); a.Command.CardType = face; a.Command.Counts[0] = red; actions.Add(a); }
            }
            else if (d.Kind == "dice-observe")
            {
                if (Active(s, playerId)?.TenureId != dice.Roll.ObserverTenure) return actions.ToArray();
                actions.Add(Action(s, "tk-g19-s1", "observe", "发动观星：每颗骰子投两次")); actions.Add(Action(s, "tk-g19-s1", "skip", "放弃观星：正常投掷"));
            }
            else if (d.Kind == "dice-modify")
            {
                var r = dice.Roll; var g = Active(s, playerId);
                if (g?.GeneralId != "tk-g13" || g.TenureId != r.ResponderTenures[r.ResponderCursor]) return actions.ToArray();
                actions.Add(Action(s, "tk-g13-s2", "skip", "放弃鬼才，确认此候选"));
                foreach (var choice in ModificationChoices(host, playerId)) for (var die = 0; die < 2; die++)
                { var a = Action(s, "tk-g13-s2", "modify", "鬼才：弃" + CardNames[choice.Item2] + "，" + (die == 0 ? "红" : "白") + "骰改为 " + choice.Item1.Number); a.Command.CardType = choice.Item2; a.Command.TileIds = new[] { choice.Item1.TileId }; a.Command.Counts[0] = die; actions.Add(a); }
            }
            else if (d.Kind == "dice-select")
            {
                var r = dice.Roll;
                foreach (var c in r.Candidates.Where(c => c.Attempt == r.Attempt))
                { var value = r.ChoiceCursor == 0 ? c.Red.ToString() : r.ChoiceCursor == 1 ? c.White.ToString() : EventName(c.EventFace); var a = Action(s, "tk-g19-s1", "select", "选择候选 " + (c.Index + 1) + "：" + value); a.Command.CardType = c.Index; actions.Add(a); }
            }
            else if (d.Kind == "dice-luoshen") { actions.Add(Action(s, "tk-g44-s1", "luoshen-stop", "采用当前骰点")); actions.Add(Action(s, "tk-g44-s1", "luoshen-reroll", "洛神：重掷整组骰子")); }
            return actions.ToArray();
        }

        public static void Resolve(IThreeKingdomsCardHost host, string playerId, B2SkillCommand command)
        {
            var s = host.State;
            Require(command != null && !string.IsNullOrEmpty(command.WindowId) && command.WindowId == s.WindowId &&
                Actions(host, playerId).Any(a => SameCommand(a.Command, command)), "骰子选择无效、越权或窗口已经过期。");
            var dice = s.Dice; var d = dice.PendingDecision; var r = dice.Roll;
            if (d.Kind == "dice-fixed")
            {
                if (command.Option == "skip") dice.FixedResponderCursor++;
                else
                {
                    var g = Active(s, playerId); var counter = g.Counters.SingleOrDefault(c => c.Key == "zhijian");
                    if (counter == null) { counter = new GeneralCounter { Key = "zhijian" }; g.Counters = g.Counters.Concat(new[] { counter }).ToArray(); }
                    counter.Value++;
                    RecordHistory(s, "fixed:" + s.Clock.TurnInstanceId + ":" + g.TenureId, playerId, "tk-g21-s2", "指定本回合为 " + EventName(command.CardType) + " 7");
                    dice.FixedResult = new B3DiceResult { PlayerId = s.Clock.ActivePlayerId, Purpose = B3DicePurpose.Formal, Red = command.Counts[0], White = 7 - command.Counts[0], EventFace = command.CardType };
                    dice.StartWindowOpen = false;
                }
            }
            else if (d.Kind == "dice-observe") { r.CandidateCount = command.Option == "observe" ? 2 : 1; r.Stage = "rolling"; if (command.Option == "observe") RecordHistory(s, r.EventId + ":observe", playerId, "tk-g19-s1", "为每颗骰子生成两个候选"); }
            else if (d.Kind == "dice-modify")
            {
                if (command.Option == "skip") Pass(r);
                else
                {
                    var tile = host.GetHarvestTiles(playerId).Single(t => t.TileId == command.TileIds[0]);
                    var candidate = Current(r); var cards = new int[8]; cards[command.CardType] = 1; var before = host.GetHand(playerId).Sum();
                    host.RemoveHand(playerId, cards); host.AddBank(cards);
                    var batchId = Id(s, "ghost-payment");
                    RecordHistory(s, batchId, playerId, "tk-g13-s2", "弃置真实手牌修改数字骰");
                    r.Modifications = r.Modifications.Concat(new[] { new B3DiceModification { PlayerId = playerId, TenureId = Active(s, playerId).TenureId,
                        PaymentEventId = batchId, Attempt = r.Attempt, CandidateIndex = candidate.Index, DieIndex = command.Counts[0], CardType = command.CardType,
                        TileId = tile.TileId, Before = command.Counts[0] == 0 ? candidate.Red : candidate.White, After = tile.Number } }).ToArray();
                    dice.PendingDecision = null;
                    B2SkillEngine.HoldReturnedCards(host, playerId, "discard", batchId, cards);
                    B2SkillEngine.OnCardsMoved(host, batchId, new[] { new B2HandChange { PlayerId = playerId, BeforeCount = before, AfterCount = before - 1, LostCount = 1, LostCards = B2SkillEngine.B4Enabled(s) ? cards : null } });
                    Pump(host); return;
                }
            }
            else if (d.Kind == "dice-select") { r.SelectedIndices[r.ChoiceCursor] = command.CardType; r.ChoiceCursor++; }
            else if (d.Kind == "dice-luoshen") { dice.PendingDecision = null; if (command.Option == "luoshen-reroll") { Reroll(host, r.EventId); return; } }
            dice.PendingDecision = null; Pump(host);
        }

        public static void Project(IThreeKingdomsCardHost host, ThreeKingdomsView view, string viewerId)
        {
            if (!Enabled(host.State) || !HasPending(host.State) || B2SkillEngine.HasPending(host.State)) return;
            var d = host.State.Dice.PendingDecision;
            view.SkillActions = Actions(host, viewerId);
            view.SkillDecision = d == null ? null : new B2SkillDecision { PlayerId = d.PlayerId, Title = d.Title,
                Description = d.PlayerId == viewerId ? d.Description : "等待该玩家完成骰子选择。", DecisionId = d.PlayerId == viewerId ? d.DecisionId : null,
                WindowId = d.PlayerId == viewerId ? d.WindowId : null, Kind = d.PlayerId == viewerId ? d.Kind : null };
        }

        private static IEnumerable<Tuple<B2HarvestTile, int>> ModificationChoices(IThreeKingdomsCardHost host, string playerId)
        {
            var hand = host.GetHand(playerId);
            foreach (var tile in host.GetHarvestTiles(playerId).Where(t => Die(t.Number)))
                foreach (var type in tile.AllowedCardTypes.Distinct().Where(t => t >= 0 && t < 8 && hand[t] > 0)) yield return Tuple.Create(tile, type);
        }
        private static B3DiceCandidate Current(B3DiceRoll r) => r.Candidates.Single(c => c.Attempt == r.Attempt && c.Index == r.CandidateCursor);
        private static void Pass(B3DiceRoll r) { r.ConsecutivePasses++; if (r.Responders.Length > 0) r.ResponderCursor = (r.ResponderCursor + 1) % r.Responders.Length; }
        private static bool Die(int value) => value >= 1 && value <= 6;
        private static int Used(GeneralInstance g) => g.Counters.FirstOrDefault(c => c.Key == "zhijian")?.Value ?? 0;
        private static GeneralInstance Active(ThreeKingdomsState s, string playerId) => s.Generals.SingleOrDefault(g => g.OwnerPlayerId == playerId && g.Region == GeneralRegion.Active);
        private static IEnumerable<string> Order(ThreeKingdomsState s) { var start = Array.IndexOf(s.SeatPlayerIds, s.Clock.ActivePlayerId); if (start < 0) start = 0; return Enumerable.Range(0, s.SeatPlayerIds.Length).Select(i => s.SeatPlayerIds[(start + i) % s.SeatPlayerIds.Length]); }
        private static string EventName(int face) => face <= 3 ? "黑色" : face == 4 ? "贸易" : face == 5 ? "政治" : "科学";
        private static string Id(ThreeKingdomsState s, string kind) => "tk-dice-" + kind + "-" + s.NextId++;
        private static void SetDecision(ThreeKingdomsState s, string player, string kind, string title, string description)
        { s.Dice.PendingDecision = new B2SkillDecision { PlayerId = player, Kind = kind, Title = title, Description = description, DecisionId = Id(s, "decision"), WindowId = s.WindowId }; }
        private static B2SkillAction Action(ThreeKingdomsState s, string skill, string option, string label) => new B2SkillAction
        { Label = label, Command = new B2SkillCommand { SkillId = skill, Option = option, WindowId = s.WindowId, DecisionId = s.Dice.PendingDecision.DecisionId } };
        private static bool SameCommand(B2SkillCommand a, B2SkillCommand b) => a.SkillId == b.SkillId && a.Option == b.Option && a.CardType == b.CardType &&
            a.DecisionId == b.DecisionId && a.WindowId == b.WindowId && a.ProgressCardId == b.ProgressCardId && b.Counts != null && a.Counts.SequenceEqual(b.Counts) &&
            b.TileIds != null && a.TileIds.SequenceEqual(b.TileIds) && b.TargetPlayerIds != null && a.TargetPlayerIds.SequenceEqual(b.TargetPlayerIds);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void RecordHistory(ThreeKingdomsState s, string id, string player, string skill, string result) { if (!B2SkillEngine.B4Enabled(s)) return; B4SkillHistoryEngine.Begin(s, id, player, skill, Active(s, player)?.TenureId, s.Dice?.Roll?.EventId, result); B4SkillHistoryEngine.Complete(s, id, result); }

        public static string[] Validate(ThreeKingdomsState s)
        {
            var errors = new List<string>(); var d = s?.Dice;
            if (d == null) return errors.ToArray();
            if (!Enabled(s)) return new[] { "旧武将规则包不能包含 B3 骰子状态。" };
            void Check(bool valid, string message) { if (!valid) errors.Add(message); }
            bool Player(string p) => p != null && s.SeatPlayerIds.Contains(p);
            bool Result(B3DiceResult result, B3DicePurpose purpose) => result != null && result.Purpose == purpose && Die(result.Red) && Die(result.White) &&
                (purpose == B3DicePurpose.Formal ? Die(result.EventFace) : result.EventFace == 0) && result.NumericActualRoll == result.ActualRoll &&
                (!result.EventActualRoll || purpose == B3DicePurpose.Formal);
            Check(d.OpenedTurnId >= -1 && d.OpenedTurnId <= s.Clock.TurnInstanceId && d.FormalRollTurnId >= -1 && d.FormalRollTurnId <= s.Clock.TurnInstanceId, "骰子回合标识超出当前时钟。");
            if (d.FixedResponders == null || d.FixedResponderTenures == null) return new[] { "直谏响应名单缺失。" };
            Check(d.FixedResponders.Length == d.FixedResponderTenures.Length && d.FixedResponders.All(Player) && d.FixedResponders.Distinct().Count() == d.FixedResponders.Length &&
                d.FixedResponderTenures.All(t => !string.IsNullOrEmpty(t)), "直谏响应者或任次快照无效。");
            Check(d.FixedResponderCursor >= 0 && d.FixedResponderCursor <= d.FixedResponders.Length, "直谏响应游标无效。");
            Check(!d.StartWindowOpen || d.OpenedTurnId == s.Clock.TurnInstanceId && d.FixedResponderCursor < d.FixedResponders.Length && d.FixedResult == null, "直谏窗口不属于本回合或已固定结果。");
            if (d.FixedResult != null) Check(Result(d.FixedResult, B3DicePurpose.Formal) && d.FixedResult.Total == 7 && !d.FixedResult.ActualRoll && !d.FixedResult.EventActualRoll &&
                d.OpenedTurnId == s.Clock.TurnInstanceId && d.FixedResult.PlayerId == s.Clock.ActivePlayerId && !d.StartWindowOpen, "直谏固定结果或作用回合无效。");
            var r = d.Roll;
            if (r != null)
            {
                Check(!string.IsNullOrEmpty(r.EventId) && Player(r.PlayerId) && Enum.IsDefined(typeof(B3DicePurpose), r.Purpose), "骰局身份、操作者或用途无效。");
                Check(r.TurnInstanceId >= 0 && r.TurnInstanceId <= s.Clock.TurnInstanceId && (r.Submitted || r.TurnInstanceId == s.Clock.TurnInstanceId), "未提交骰局不属于当前回合。");
                Check(r.Purpose != B3DicePurpose.Formal || d.FormalRollTurnId == r.TurnInstanceId, "正式骰局与回合去重标识不一致。");
                Check(new[] { "observe", "rolling", "modify", "select", "ready" }.Contains(r.Stage) && (!r.Submitted || r.Stage == "ready"), "骰局阶段或提交标识无效。");
                Check(r.Attempt >= 1 && (r.CandidateCount == 1 || r.CandidateCount == 2) && r.CandidateCursor >= 0 && r.CandidateCursor <= r.CandidateCount && r.ChoiceCursor >= 0 && r.ChoiceCursor <= 3, "骰局尝试或候选游标无效。");
                Check(r.LuoshenRewardedAttempt >= 0 && r.LuoshenRewardedAttempt <= r.Attempt && (B2SkillEngine.B4Enabled(s) || r.LuoshenRewardedAttempt == 0), "洛神奖励尝试号或规则版本无效。");
                Check(r.FixedRed == 0 && r.FixedWhite == 0 || Die(r.FixedRed) && Die(r.FixedWhite), "固定数字骰面无效。");
                if (r.Candidates == null || r.SelectedIndices == null || r.Responders == null || r.ResponderTenures == null || r.Modifications == null)
                { errors.Add("骰局候选、选择或修改记录缺失。"); return errors.ToArray(); }
                Check(r.SelectedIndices.Length == 3 && r.SelectedIndices.All(i => i >= -1 && i < r.CandidateCount), "逐颗候选选择索引无效。");
                Check(r.Responders.Length == r.ResponderTenures.Length && r.Responders.All(Player) && r.Responders.Distinct().Count() == r.Responders.Length && r.ResponderTenures.All(t => !string.IsNullOrEmpty(t)) &&
                    r.ResponderCursor >= 0 && (r.Responders.Length == 0 ? r.ResponderCursor == 0 : r.ResponderCursor < r.Responders.Length) && r.ConsecutivePasses >= 0 && r.ConsecutivePasses <= r.Responders.Length, "鬼才响应轮次无效。");
                Check(r.Candidates.All(c => c != null && c.Attempt >= 1 && c.Attempt <= r.Attempt && c.Index >= 0 && c.Index < r.CandidateCount && Die(c.Red) && Die(c.White) && Die(c.OriginalRed) && Die(c.OriginalWhite) &&
                    (r.Purpose == B3DicePurpose.Formal ? Die(c.EventFace) && Die(c.OriginalEventFace) : c.EventFace == 0 && c.OriginalEventFace == 0) && c.EventFace == c.OriginalEventFace), "骰子候选的骰面、事件或尝试号无效。");
                if (r.Candidates.Any(c => c == null) || r.Modifications.Any(m => m == null)) { errors.Add("骰子记录含空条目。"); return errors.ToArray(); }
                Check(r.Candidates.Select(c => c.Attempt + ":" + c.Index).Distinct().Count() == r.Candidates.Length, "骰子候选重复。");
                Check(r.Modifications.Count(m => !m.Applied) <= 1 && r.Modifications.Select(m => m.PaymentEventId).Distinct().Count() == r.Modifications.Length, "鬼才支付或待应用记录重复。");
                foreach (var m in r.Modifications)
                    Check(Player(m.PlayerId) && !string.IsNullOrEmpty(m.TenureId) && !string.IsNullOrEmpty(m.PaymentEventId) && !string.IsNullOrEmpty(m.TileId) && m.CardType >= 0 && m.CardType < 8 &&
                        (m.DieIndex == 0 || m.DieIndex == 1) && Die(m.Before) && Die(m.After) && r.FixedRed == 0 &&
                        r.Candidates.Any(c => c.Attempt == m.Attempt && c.Index == m.CandidateIndex) &&
                        (s.Skills?.ProcessedEvents ?? Array.Empty<string>()).Contains("move:" + m.PaymentEventId) &&
                        (m.Applied || !r.Submitted && m.Attempt == r.Attempt && m.CandidateIndex == r.CandidateCursor), "鬼才改点记录没有有效候选或已付费用批次。");
                if (r.Stage == "ready")
                {
                    Check(Result(r.Result, r.Purpose) && r.Result.EventId == r.EventId && r.Result.PlayerId == r.PlayerId && r.Modifications.All(m => m.Applied), "最终骰局结果不完整或费用子链未完成。");
                    Check(r.Result == null || r.Result.NumericActualRoll == (r.FixedRed == 0), "最终骰局的真实数字投掷资格不一致。");
                    if (r.Result != null && r.Result.NumericActualRoll)
                        Check(r.Result.Attempt == r.Attempt && r.Candidates.Count(c => c.Attempt == r.Attempt) == r.CandidateCount && r.SelectedIndices.All(i => i >= 0), "最终真实骰局没有完整候选选择。");
                    if (r.Result != null && r.Candidates.Length > 0 && r.SelectedIndices.Length == 3)
                    {
                        var red = r.Candidates.FirstOrDefault(c => c.Attempt == r.Attempt && c.Index == r.SelectedIndices[0]);
                        var white = r.Candidates.FirstOrDefault(c => c.Attempt == r.Attempt && c.Index == r.SelectedIndices[1]);
                        var color = r.Candidates.FirstOrDefault(c => c.Attempt == r.Attempt && c.Index == r.SelectedIndices[2]);
                        Check((r.FixedRed != 0 ? r.Result.Red == r.FixedRed && r.Result.White == r.FixedWhite : red != null && white != null && r.Result.Red == red.Red && r.Result.White == white.White) &&
                            (r.Purpose != B3DicePurpose.Formal || color != null && r.Result.EventFace == color.EventFace), "最终骰局与选定候选不一致。");
                    }
                }
                else Check(r.Result == null, "未完成候选已提前产生最终结果。");
            }
            var pending = d.PendingDecision;
            if (pending != null)
            {
                Check(Player(pending.PlayerId) && !string.IsNullOrEmpty(pending.DecisionId) && pending.WindowId == s.WindowId, "骰子待决权限或窗口失效。");
                var kinds = new[] { "dice-fixed", "dice-observe", "dice-modify", "dice-select", "dice-luoshen" };
                Check(kinds.Contains(pending.Kind), "未知骰子待决类型。");
                if (pending.Kind == "dice-fixed") Check(d.StartWindowOpen && d.FixedResponderCursor < d.FixedResponders.Length && d.FixedResponderCursor >= 0 && d.FixedResponders[d.FixedResponderCursor] == pending.PlayerId, "直谏待决与响应位置不一致。");
                else if (r == null) errors.Add("骰子待决没有所属骰局。");
                else if (pending.Kind == "dice-luoshen") Check(B2SkillEngine.B4Enabled(s) && r.Stage == "ready" && r.PlayerId == pending.PlayerId && !r.Submitted && r.LuoshenRewardedAttempt == r.Attempt && r.Result?.Total % 2 == 1, "洛神待决与奇数尝试不一致。");
                else if (pending.Kind == "dice-modify") Check(r.Stage == "modify" && r.ResponderCursor < r.Responders.Length && r.ResponderCursor >= 0 && r.Responders[r.ResponderCursor] == pending.PlayerId && !r.Submitted, "鬼才待决与候选响应位置不一致。");
                else Check(r.Stage == (pending.Kind == "dice-observe" ? "observe" : "select") && r.PlayerId == pending.PlayerId && !r.Submitted, "观星待决与骰局阶段不一致。");
            }
            return errors.ToArray();
        }

        private static B3DiceResult CopyResult(B3DiceResult r) => r == null ? null : new B3DiceResult { EventId = r.EventId, PlayerId = r.PlayerId, Purpose = r.Purpose,
            Red = r.Red, White = r.White, EventFace = r.EventFace, ActualRoll = r.ActualRoll, NumericActualRoll = r.NumericActualRoll, EventActualRoll = r.EventActualRoll, Attempt = r.Attempt };
        public static B3DiceState Clone(B3DiceState d)
        {
            if (d == null) return null; var r = d.Roll; var p = d.PendingDecision;
            return new B3DiceState { OpenedTurnId = d.OpenedTurnId, FormalRollTurnId = d.FormalRollTurnId,
                FixedResponders = (string[])d.FixedResponders.Clone(), FixedResponderTenures = (string[])d.FixedResponderTenures.Clone(), FixedResponderCursor = d.FixedResponderCursor,
                StartWindowOpen = d.StartWindowOpen, FixedResult = CopyResult(d.FixedResult), PendingDecision = p == null ? null : new B2SkillDecision { PlayerId = p.PlayerId, DecisionId = p.DecisionId,
                    WindowId = p.WindowId, Title = p.Title, Description = p.Description, Kind = p.Kind, FrameId = p.FrameId },
                Roll = r == null ? null : new B3DiceRoll { EventId = r.EventId, PlayerId = r.PlayerId, Purpose = r.Purpose, Stage = r.Stage, Attempt = r.Attempt,
                    CandidateCount = r.CandidateCount, CandidateCursor = r.CandidateCursor, SelectedIndices = (int[])r.SelectedIndices.Clone(), ChoiceCursor = r.ChoiceCursor,
                    FixedRed = r.FixedRed, FixedWhite = r.FixedWhite, Responders = (string[])r.Responders.Clone(), ResponderTenures = (string[])r.ResponderTenures.Clone(),
                    ResponderCursor = r.ResponderCursor, ConsecutivePasses = r.ConsecutivePasses, Result = CopyResult(r.Result), Submitted = r.Submitted, ObserverTenure = r.ObserverTenure, TurnInstanceId = r.TurnInstanceId, LuoshenRewardedAttempt = r.LuoshenRewardedAttempt,
                    Modifications = r.Modifications.Select(m => new B3DiceModification { PlayerId = m.PlayerId, TenureId = m.TenureId, PaymentEventId = m.PaymentEventId,
                        Attempt = m.Attempt, CandidateIndex = m.CandidateIndex, DieIndex = m.DieIndex, CardType = m.CardType, TileId = m.TileId, Before = m.Before, After = m.After, Applied = m.Applied }).ToArray(),
                    Candidates = r.Candidates.Select(c => new B3DiceCandidate { Attempt = c.Attempt, Index = c.Index, OriginalRed = c.OriginalRed, OriginalWhite = c.OriginalWhite,
                        OriginalEventFace = c.OriginalEventFace, Red = c.Red, White = c.White, EventFace = c.EventFace }).ToArray() } };
        }
    }
}
