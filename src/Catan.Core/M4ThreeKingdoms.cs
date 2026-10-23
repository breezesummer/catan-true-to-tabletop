using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using Catan.Core.ThreeKingdoms;

namespace Catan.Core.M4
{
    public sealed partial class Command
    {
        [DataMember(Order=300, EmitDefaultValue=false)] public string[] GeneralIds { get; set; }
        [DataMember(Order=301, EmitDefaultValue=false)] public string DecisionId { get; set; }
        [DataMember(Order=302, EmitDefaultValue=false)] public string WindowId { get; set; }
        [DataMember(Order=303, EmitDefaultValue=false)] public B2SkillCommand SkillCommand { get; set; }
    }
    public sealed partial class GameState
    {
        [DataMember(Order=300, EmitDefaultValue=false)] public ThreeKingdomsState ThreeKingdoms { get; set; }
    }
    public sealed partial class PlayerView
    {
        [DataMember(EmitDefaultValue=false)] public ThreeKingdomsView ThreeKingdoms { get; internal set; }
    }
    public sealed partial class CitiesKnightsGameSession
    {
        public const string ThreeKingdomsRulesVersion = "catan-cities-knights-three-kingdoms-b1-v1";

        public const string ThreeKingdomsB2RulesVersion = "catan-cities-knights-three-kingdoms-b2-v1";
        public const string ThreeKingdomsB3RulesVersion = "catan-cities-knights-three-kingdoms-b3-v1";

        private void InitializeThreeKingdoms(GameState s, bool skillsEnabled, string packageVersion = null)
        {
            if (skillsEnabled && packageVersion == null) packageVersion = GeneralCatalog.B4PackageVersion;
            s.RulesVersion = skillsEnabled ? (packageVersion == GeneralCatalog.B4PackageVersion ? ThreeKingdomsB4RulesVersion : packageVersion == GeneralCatalog.B2PackageVersion ? ThreeKingdomsB2RulesVersion : ThreeKingdomsB3RulesVersion) : ThreeKingdomsRulesVersion;
            var seats = Enumerable.Range(0, s.Players.Length).Select(i => s.Players[(s.StartingPlayerIndex + i) % s.Players.Length].Id).ToArray();
            s.ThreeKingdoms = ThreeKingdomsEngine.Create(seats, s.InitialSeed, maximum => Draw(s.Random, maximum), skillsEnabled, packageVersion);
        }

        // 原规则待决保持原位：换将子树不会覆盖费用、弃牌义务、交易或剧本恢复位置。
        private void Apply(GameState s, Command c)
        {
            var tk = s.ThreeKingdoms;
            if (B4(s)) { ApplyB4(s, c); return; }
            if (B2(s)) { ApplyB2(s, c); return; }
            if (tk == null)
            {
                Require(c.Kind < CommandKind.ChooseGenerals, "ExpansionDisabled", "本局未开启三国争霸开发骨架。");
                ApplyBaseRules(s, c);
                return;
            }
            Require(s.Phase != GamePhase.Finished, "GameFinished", "对局已经结束。");
            Require(s.Players.Any(p => p.Id == c.PlayerId), "UnknownPlayer", "未知席位。");
            Require(c.WindowId == tk.WindowId, "StaleWindow", "操作窗口已更新，请重新选择。");
            try
            {
                if (c.Kind == CommandKind.ChooseGenerals)
                {
                    ThreeKingdomsEngine.ChooseGenerals(tk, c.PlayerId, c.GeneralIds, c.DecisionId, c.WindowId, maximum => Draw(s.Random, maximum));
                    return;
                }
                if (c.Kind == CommandKind.DeployGeneral)
                {
                    Require(c.GeneralIds != null && c.GeneralIds.Length == 1, "InvalidGeneralSelection", "必须选择一张武将登场。");
                    ThreeKingdomsEngine.DeployGeneral(tk, c.PlayerId, c.GeneralIds[0], c.DecisionId, c.WindowId, maximum => Draw(s.Random, maximum));
                    if (tk.PendingDecision == null && tk.Clock.Round > 0) s.ActivePlayerId = tk.Clock.ActivePlayerId;
                    return;
                }
                Require(tk.PendingDecision == null, "GeneralDecisionPending", "请先完成当前武将选择。");
                if (c.Kind == CommandKind.RetireGeneral)
                {
                    ThreeKingdomsEngine.RetireGeneral(tk, c.PlayerId, c.WindowId, maximum => Draw(s.Random, maximum));
                    return;
                }
                if (c.Kind == CommandKind.AdvanceThreeKingdomsPhase)
                {
                    Require(s.ActivePlayerId == c.PlayerId && s.PendingDecision == null && s.TradeOffer == null && s.DecisionQueue.Length == 0, "PendingResolution", "请先完成当前待决流程。");
                    if (tk.Clock.Phase == ThreeKingdomsPhase.End)
                    {
                        ApplyBaseRules(s, new Command { PlayerId = c.PlayerId, Kind = CommandKind.EndTurn });
                        if (s.Phase != GamePhase.Finished)
                        {
                            ThreeKingdomsEngine.CompleteNormalTurn(tk, maximum => Draw(s.Random, maximum));
                            s.ActivePlayerId = tk.Clock.ActivePlayerId;
                        }
                    }
                    else
                    {
                        Require(tk.Clock.Phase != ThreeKingdomsPhase.Play, "UseEndTurn", "请使用结束回合进入结束阶段。");
                        ThreeKingdomsEngine.AdvancePhase(tk, c.PlayerId, c.WindowId);
                    }
                    return;
                }
                Require(tk.Clock.Phase == ThreeKingdomsPhase.InitialPlacement || tk.Clock.Phase == ThreeKingdomsPhase.Roll || tk.Clock.Phase == ThreeKingdomsPhase.Play || ((B2(s) || tk.Clock.Phase == ThreeKingdomsPhase.Preparation) && s.PendingDecision != null),
                    "ThreeKingdomsPhase", "请先确认当前三国阶段。");
                if (c.Kind == CommandKind.EndTurn)
                {
                    Require(tk.Clock.Phase == ThreeKingdomsPhase.Play && s.ActivePlayerId == c.PlayerId && s.PendingDecision == null && s.TradeOffer == null, "WrongPhase", "当前不能结束出牌阶段。");
                    Require(s.Players.Single(p => p.Id == c.PlayerId).ProgressCards.Length <= 4, "ProgressHandLimit", "结束出牌阶段前须将发展卡减至四张。");
                    ThreeKingdomsEngine.AdvancePhase(tk, c.PlayerId, c.WindowId);
                    return;
                }
                ApplyBaseRules(s, c);
                if (s.Phase == GamePhase.Finished) return;
                if (tk.Clock.Phase == ThreeKingdomsPhase.InitialPlacement && s.Turn > 0 && s.Phase == GamePhase.ProductionAwaitRoll && s.PendingDecision == null)
                    ThreeKingdomsEngine.BeginNormalTurns(tk);
                else if (tk.Clock.Phase == ThreeKingdomsPhase.Roll && s.Phase == GamePhase.Action && s.PendingDecision == null && s.DecisionQueue.Length == 0)
                    ThreeKingdomsEngine.MarkDrawPhase(tk);
                else ThreeKingdomsEngine.RefreshWindow(tk);
            }
            catch (InvalidOperationException ex) { throw new RuleException("InvalidThreeKingdomsAction", ex.Message); }
        }

        private static bool ThreeKingdomsAllowsBaseCommand(GameState s)
        {
            var tk = s.ThreeKingdoms;
            return tk == null || !B2Busy(s) && tk.Automation?.PendingDecision == null && tk.PendingDecision == null && (tk.Clock.Phase == ThreeKingdomsPhase.InitialPlacement || tk.Clock.Phase == ThreeKingdomsPhase.Roll || tk.Clock.Phase == ThreeKingdomsPhase.Play || (B2(s) || tk.Clock.Phase == ThreeKingdomsPhase.Preparation) && s.PendingDecision != null);
        }

        private void StampThreeKingdomsWindow(Command command)
        {
            if (state.ThreeKingdoms != null && command.WindowId == null) command.WindowId = state.ThreeKingdoms.WindowId;
        }

        private void PopulateThreeKingdomsView(PlayerView view)
        {
            if (state.ThreeKingdoms == null) return;
            if (B2(state)) { PopulateB2View(view); return; }
            var tk = ThreeKingdomsEngine.Project(state.ThreeKingdoms, view.PlayerId);
            view.ThreeKingdoms = tk;
            if (state.Phase == GamePhase.Finished) { tk.CanRetire = false; tk.Clock.Phase = ThreeKingdomsPhase.Finished; return; }
            var phase = tk.Clock.Phase;
            bool overlay = tk.PendingDecision != null || (state.PendingDecision == null && (phase == ThreeKingdomsPhase.Preparation || phase == ThreeKingdomsPhase.Judgment || phase == ThreeKingdomsPhase.Draw || phase == ThreeKingdomsPhase.End || phase == ThreeKingdomsPhase.RoundEnd));
            if (overlay)
            {
                view.Phase = GamePhase.ThreeKingdoms;
                view.PendingDecision = new PendingDecision { Kind = tk.PendingDecision == null ? "ThreeKingdomsPhase" : "ThreeKingdoms" + tk.PendingDecision.Kind, PlayerId = tk.PendingDecision?.PlayerId ?? tk.Clock.ActivePlayerId };
                view.Discards = Array.Empty<DiscardRequirement>(); view.TradeOffer = null;
                view.LegalVertexIds = Array.Empty<string>(); view.LegalCityVertexIds = Array.Empty<string>(); view.LegalEdgeIds = Array.Empty<string>();
                view.LegalActions = Array.Empty<Command>();
            }
            var actions = new List<Command>();
            Command Make(CommandKind kind, string[] ids = null) => new Command { Kind = kind, PlayerId = view.PlayerId, GeneralIds = ids, WindowId = tk.WindowId, DecisionId = tk.PendingDecision?.DecisionId };
            var d = tk.PendingDecision;
            if (d != null && d.PlayerId == view.PlayerId)
            {
                if (d.Kind == ThreeKingdomsDecisionKind.SelectOpening || d.Kind == ThreeKingdomsDecisionKind.SelectReplacement)
                {
                    if (d.RequiredCount == 1) foreach (var id in d.CandidateIds) actions.Add(Make(CommandKind.ChooseGenerals, new[] { id }));
                    else for (int i = 0; i < d.CandidateIds.Length; i++) for (int j = i + 1; j < d.CandidateIds.Length; j++) actions.Add(Make(CommandKind.ChooseGenerals, new[] { d.CandidateIds[i], d.CandidateIds[j] }));
                }
                else foreach (var id in d.CandidateIds) actions.Add(Make(CommandKind.DeployGeneral, new[] { id }));
            }
            else if (d == null && overlay && tk.Clock.ActivePlayerId == view.PlayerId) actions.Add(Make(CommandKind.AdvanceThreeKingdomsPhase));
            if (tk.CanRetire) actions.Add(Make(CommandKind.RetireGeneral));
            view.LegalActions = view.LegalActions.Concat(actions.Where(IsLegal)).ToArray();
        }

        private void ValidateThreeKingdoms(GameState s)
        {
            if (s.ThreeKingdomsNativePayments != null) throw new InvalidDataException("原生支付快照未在命令结尾清理。");
            if (s.ThreeKingdoms == null) return;
            var issues = ThreeKingdomsEngine.Validate(s.ThreeKingdoms).Concat(B3DiceEngine.Validate(s.ThreeKingdoms)).ToArray();
            if(B2(s)) issues = issues.Concat(B2SkillEngine.Validate(CardHost(s))).ToArray();
            if (issues.Length > 0) throw new InvalidDataException(string.Join("；", issues));
            var exchange = s.ThreeKingdomsExchange;
            if (exchange != null && (!B2(s) || exchange.Length != 2 || exchange.Any(x => x == null || x.BeforeCount < 1 || x.LostCount != 1 || x.GivenCount != 1) ||
                exchange.Select(x => x.PlayerId).Distinct().Count() != 2 || exchange.Any(x => !s.Players.Any(p => p.Id == x.PlayerId)) ||
                s.PendingDecision?.Kind != "ProgressHarbor" || !exchange.Any(x => x.PlayerId == s.PendingDecision.PlayerId) || !exchange.Any(x => x.PlayerId == s.PendingDecision.OtherPlayerId)))
                throw new InvalidDataException("商港交换的未完成牌流记录无效。");
            if (!s.ThreeKingdoms.SeatPlayerIds.OrderBy(x => x).SequenceEqual(s.Players.Select(p => p.Id).OrderBy(x => x)))
                throw new InvalidDataException("三国固定座位与游戏席位不一致。");
        }

        private static GameHistoryChange[] PublicGeneralChanges(GameState before, GameState after)
        {
            if (before.ThreeKingdoms == null || after.ThreeKingdoms == null) return Array.Empty<GameHistoryChange>();
            var changes = new List<GameHistoryChange>();
            foreach (var player in after.Players)
            {
                var oldGeneral = before.ThreeKingdoms.Generals.SingleOrDefault(g => g.OwnerPlayerId == player.Id && g.Region == GeneralRegion.Active);
                var newGeneral = after.ThreeKingdoms.Generals.SingleOrDefault(g => g.OwnerPlayerId == player.Id && g.Region == GeneralRegion.Active);
                if (oldGeneral?.TenureId == newGeneral?.TenureId) continue;
                if (oldGeneral != null) changes.Add(new GameHistoryChange { PlayerId = player.Id, Category = "general", Amount = -1, Name = "隐退：" + GeneralCatalog.Get(oldGeneral.GeneralId).Name });
                if (newGeneral != null) changes.Add(new GameHistoryChange { PlayerId = player.Id, Category = "general", Amount = 1, Name = "登场：" + GeneralCatalog.Get(newGeneral.GeneralId).Name });
            }
            return changes.ToArray();
        }
    }
}
