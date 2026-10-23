using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core.ThreeKingdoms;

namespace Catan.Core.M4
{
    public sealed partial class CitiesKnightsGameSession
    {
        private void RefreshB2(GameState s)
        {
            ThreeKingdomsEngine.RefreshWindow(s.ThreeKingdoms);
            B2SkillEngine.RefreshDecision(s.ThreeKingdoms);
            B3DiceEngine.RefreshDecision(s.ThreeKingdoms);
        }
        private void ApplyB2(GameState s, Command c)
        {
            var tk = s.ThreeKingdoms; var host = CardHost(s);
            Require(s.Phase != GamePhase.Finished, "GameFinished", "对局已经结束。");
            Require(s.Players.Any(p => p.Id == c.PlayerId), "UnknownPlayer", "未知席位。");
            Require(c.WindowId == tk.WindowId, "StaleWindow", "操作窗口已更新，请重新选择。");
            try
            {
                if (c.Kind == CommandKind.ChooseGenerals)
                {
                    ThreeKingdomsEngine.ChooseGenerals(tk, c.PlayerId, c.GeneralIds, c.DecisionId, c.WindowId, n => Draw(s.Random, n));
                    if (B3(s)) { B2SkillEngine.RefreshDecision(tk); B3DiceEngine.RefreshDecision(tk); }
                    return;
                }
                if (c.Kind == CommandKind.DeployGeneral)
                {
                    Require(c.GeneralIds?.Length == 1, "InvalidGeneralSelection", "必须选择一张武将登场。");
                    long previousOpportunity = tk.Clock.OpportunityId;
                    ThreeKingdomsEngine.DeployGeneral(tk, c.PlayerId, c.GeneralIds[0], c.DecisionId, c.WindowId, n => Draw(s.Random, n));
                    if (tk.PendingDecision == null && tk.Clock.Round > 0)
                    {
                        s.ActivePlayerId = tk.Clock.ActivePlayerId;
                        if (tk.Clock.OpportunityId != previousOpportunity) B2StartOpportunity(s);
                        B2SkillEngine.RefreshDecision(tk);
                        CheckB2Capacity(s);
                    }
                    if (tk.PendingDecision == null) FinishB2Work(s);
                    if (B3(s)) { B2SkillEngine.RefreshDecision(tk); B3DiceEngine.RefreshDecision(tk); }
                    return;
                }
                Require(tk.PendingDecision == null, "GeneralDecisionPending", "请先完成当前武将选择。");
                if (c.Kind == CommandKind.RetireGeneral)
                {
                    ThreeKingdomsEngine.RetireGeneral(tk, c.PlayerId, c.WindowId, n => Draw(s.Random, n));
                    B2SkillEngine.RefreshDecision(tk); if (B3(s)) { FinishB2Work(s); RefreshB2(s); } return;
                }
                if (c.Kind == CommandKind.UseGeneralSkill)
                {
                    Require(c.SkillCommand != null, "MissingSkill", "请选择技能操作。");
                    Require(tk.Skills.PendingDecision != null || tk.Dice?.PendingDecision != null || (s.PendingDecision == null && s.TradeOffer == null), "PendingResolution", "请先完成当前牌或交易响应。");
                    if (tk.Skills.PendingDecision == null && tk.Dice?.PendingDecision != null) B3DiceEngine.Resolve(host, c.PlayerId, c.SkillCommand);
                    else B2SkillEngine.Resolve(host, c.PlayerId, c.SkillCommand);
                    FinishB2Work(s);
                    RefreshB2(s); return;
                }
                Require(!B2Busy(s), "SkillDecisionPending", "请先完成武将技能选择。");
                if (c.Kind == CommandKind.AdvanceThreeKingdomsPhase)
                {
                    Require(s.ActivePlayerId == c.PlayerId && s.PendingDecision == null && s.TradeOffer == null && s.DecisionQueue.Length == 0, "PendingResolution", "请先完成当前待决流程。");
                    if (tk.Clock.Phase == ThreeKingdomsPhase.End)
                    {
                        B2SkillEngine.OnOpportunityEnd(host, tk.Clock.OpportunityId, c.PlayerId);
                        CheckB2Capacity(s);
                        if (s.PendingDecision != null) { RefreshB2(s); return; }
                        ApplyBaseRules(s, new Command { PlayerId = c.PlayerId, Kind = CommandKind.EndTurn });
                        if (s.Phase != GamePhase.Finished)
                        {
                            ThreeKingdomsEngine.CompleteNormalTurn(tk, n => Draw(s.Random, n));
                            s.ActivePlayerId = tk.Clock.ActivePlayerId;
                            if (tk.PendingDecision == null) B2StartOpportunity(s);
                        }
                    }
                    else
                    {
                        Require(tk.Clock.Phase != ThreeKingdomsPhase.Play, "UseEndTurn", "请使用结束回合进入结束阶段。");
                        ThreeKingdomsEngine.AdvancePhase(tk, c.PlayerId, c.WindowId);
                        if (tk.Clock.Phase == ThreeKingdomsPhase.Play)
                            B2SkillEngine.OnPlayStart(host, "play-" + tk.Clock.PhaseId, c.PlayerId);
                    }
                    FinishB2Work(s); RefreshB2(s); return;
                }
                bool nativePending = s.PendingDecision != null || s.TradeOffer != null;
                Require(tk.Clock.Phase == ThreeKingdomsPhase.InitialPlacement || tk.Clock.Phase == ThreeKingdomsPhase.Roll || tk.Clock.Phase == ThreeKingdomsPhase.Play || nativePending,
                    "ThreeKingdomsPhase", "请先确认当前三国阶段。");
                if (c.Kind == CommandKind.EndTurn )
                {
                    Require(tk.Clock.Phase == ThreeKingdomsPhase.Play && s.ActivePlayerId == c.PlayerId && !nativePending, "WrongPhase", "当前不能结束出牌阶段。");

                    Require(B2Unlimited(s, c.PlayerId) || B2Protected(s, c.PlayerId) || s.Players.Single(p => p.Id == c.PlayerId).ProgressCards.Length <= 4, "ProgressHandLimit", "结束出牌阶段前须将发展卡减至四张。");
                    ThreeKingdomsEngine.AdvancePhase(tk, c.PlayerId, c.WindowId);
                    B2SkillEngine.OnEnd(host, "end-" + tk.Clock.TurnInstanceId);
                    FinishB2Work(s); RefreshB2(s); return;
                }
                if (c.Kind == CommandKind.RollDice)
                {
                    s.ThreeKingdomsDiceRecipients = Array.Empty<string>();
                    if (B3(s))
                    {
                        Require(s.ActivePlayerId == c.PlayerId && s.Phase == GamePhase.ProductionAwaitRoll && s.PendingDecision == null && s.TradeOffer == null, "WrongPhase", "当前不能掷骰。");
                        B3DiceEngine.BeginRoll(host, c.PlayerId, "dice-" + tk.Clock.TurnInstanceId, s.AlchemyDice1, s.AlchemyDice2);
                        s.AlchemyDice1 = 0; s.AlchemyDice2 = 0;
                        FinishB2Work(s); RefreshB2(s); return;
                    }
                }
                // 合法性预检在隔离副本执行，不消耗权威随机或费用；目标响应先于主体。
                var targets = B2TargetPlayers(s, c);
                if (targets.Length > 0)
                {
                    var validation = s.CopyForTransaction(); ApplyBaseRules(validation, c);
                    foreach (var target in targets) B2SkillEngine.OnTargeted(host, c.Id, c.PlayerId, target, c.Kind == CommandKind.CommercialHarborOffer ? "CommercialHarbor" : c.ProgressCard.ToString());
                    if (B2Busy(s)) { s.ThreeKingdomsDeferredCommand = Json.Copy(c); RefreshB2(s); return; }
                }
                ApplyB2Native(s, c);
                FinishB2Work(s);
                RefreshB2(s);
            }
            catch (InvalidOperationException ex) { throw new RuleException("InvalidThreeKingdomsAction", ex.Message); }
        }

        private void B2StartOpportunity(GameState s)
        {
            var tk = s.ThreeKingdoms;
            var skipped = tk.Clock.SkippedOpportunityPlayerIds.Skip(tk.Skills.ProcessedSkippedCount).ToArray();
            tk.Skills.ProcessedSkippedCount = tk.Clock.SkippedOpportunityPlayerIds.Length;
            B2SkillEngine.OnOpportunityStart(CardHost(s), tk.Clock.OpportunityId, tk.Clock.ActivePlayerId, skipped);
            if (B3(s)) B3DiceEngine.BeginTurn(CardHost(s));
            CheckB2Capacity(s);
        }
        private void FinishB2Work(GameState s)
        {
            if (s.Phase == GamePhase.Finished) return;
            var tk = s.ThreeKingdoms; var host = CardHost(s);
            FinishB3Dice(s);
            if (!B2Busy(s) && s.ThreeKingdomsDeferredCommand != null)
            {
                var deferred = s.ThreeKingdomsDeferredCommand; s.ThreeKingdomsDeferredCommand = null;
                ApplyB2Native(s, deferred);
            }
            if (tk.Clock.Phase == ThreeKingdomsPhase.InitialPlacement && s.Turn > 0 && s.Phase == GamePhase.ProductionAwaitRoll && s.PendingDecision == null)
            {
                ThreeKingdomsEngine.BeginNormalTurns(tk); B2StartOpportunity(s);
            }
            if ((!B3(s) || !B2Busy(s)) && tk.Clock.Phase == ThreeKingdomsPhase.Roll && s.Phase == GamePhase.Action && s.PendingDecision == null && s.DecisionQueue.Length == 0)
            {
                // 在产出预留完成之前，骰后技能不得抢走其他席位的主分配。
                ThreeKingdomsEngine.MarkDrawPhase(tk);
                BeginB2Harvest(s);
                B2SkillEngine.OnDiceResolved(host, "dice-" + tk.Clock.TurnInstanceId, s.LastEventDie == EventDie.Barbarian, s.ThreeKingdomsDiceRecipients ?? Array.Empty<string>(), !B3(s) || tk.Dice?.Roll?.Result?.ActualRoll == true);
            }
            if (!B2Busy(s) && s.PendingDecision == null && s.DecisionQueue.Length == 0 && s.ThreeKingdomsProgressUse != null)
            {
                var use = s.ThreeKingdomsProgressUse; s.ThreeKingdomsProgressUse = null;
                if (tk.Generals.Any(g => g.Region == GeneralRegion.Active && g.TenureId == use[2])) B2SkillEngine.OnProgressUsed(host, use[0], use[1]);
            }
            B2SkillEngine.Pump(host);
            CheckB2Capacity(s);
        }

        private void ApplyB2Native(GameState s, Command c)
        {
            var host = CardHost(s); var before = s.Players.ToDictionary(p => p.Id, p => host.GetHand(p.Id));
            var offer = s.TradeOffer;
            var pending = s.PendingDecision;
            var buildings = B2Buildings(s);
            Require(!B3(s) || c.Kind != CommandKind.PlayProgressCard || c.ProgressCard != ProgressCardKind.Alchemy || s.ThreeKingdoms.Dice?.FixedResult == null, "FixedDiceLocked", "直谏已固定本回合骰局，不能再使用炼金术士。");
            ApplyBaseRules(s, c);
            var payments = s.ThreeKingdomsNativePayments ?? Array.Empty<B3NativePayment>();
            // Pay 已以真实前后手牌发出失牌事件；这里只记录命令其余步骤，避免同一费用重复入账。
            var remainingBefore = before.ToDictionary(x => x.Key, x => Enumerable.Range(0, 8)
                .Select(i => x.Value[i] - payments.Where(p => p.PlayerId == x.Key).Sum(p => p.Cards[i])).ToArray());
            var changes = s.Players.Select(p => {
                int[] after = host.GetHand(p.Id);
                int lost = Enumerable.Range(0, 8).Sum(i => Math.Max(0, remainingBefore[p.Id][i] - after[i]));
                int given = 0;
                if (c.Kind == CommandKind.AcceptTrade && offer != null)
                {
                    if (p.Id == offer.ProposerId) given = offer.Give.Total + offer.GiveCommodities.Total;
                    else if (p.Id == c.PlayerId) given = offer.Receive.Total + offer.ReceiveCommodities.Total;
                    lost = Math.Max(lost, given);
                }
                if (c.Kind == CommandKind.ResolveProgressChoice && pending?.Kind == "ProgressWedding" && p.Id == c.PlayerId) given = lost;
                return new B2HandChange { PlayerId = p.Id, BeforeCount = remainingBefore[p.Id].Sum(), AfterCount = after.Sum(), LostCount = lost, GivenCount = given };
            }).ToArray();
            if (c.Kind == CommandKind.DiscardResources || c.Kind == CommandKind.ResolveProgressChoice && pending?.Kind == "ProgressSabotage")
            {
                var returned = B3(s) ? Enumerable.Range(0, 8).Select(i => Math.Max(0, before[c.PlayerId][i] - host.GetHand(c.PlayerId)[i])).ToArray() :
                    Enumerable.Range(0, 8).Select(i => i < 5 ? (c.Resources ?? new ResourceBag())[(Resource)i] : (c.Commodities ?? new CommodityBag())[(Commodity)(i - 5)]).ToArray();
                if (B3(s)) B2SkillEngine.HoldReturnedCards(host, c.PlayerId, c.Kind == CommandKind.DiscardResources ? "seven-discard" : "discard", c.Kind == CommandKind.DiscardResources ? "dice-" + s.ThreeKingdoms.Clock.TurnInstanceId : c.Id, returned);
                else B2SkillEngine.HoldReturnedCards(host, returned);
            }
            if (B3(s))
            {
                if (IsB3BankPayment(c, pending) && !payments.Any(p => p.PlayerId == c.PlayerId))
                {
                    int[] returned = Enumerable.Range(0, 8).Select(i => Math.Max(0, before[c.PlayerId][i] - host.GetHand(c.PlayerId)[i])).ToArray();
                    B2SkillEngine.HoldReturnedCards(host, c.PlayerId, "payment", c.Id, returned);
                }
            }
            // 商港的资源给付与商品选择属于同一次交换；整笔完成后再判空手及交牌计数。
            if (c.Kind == CommandKind.CommercialHarborOffer && s.PendingDecision?.Kind == "ProgressHarbor")
            {
                s.ThreeKingdomsExchange = new[] { c.PlayerId, c.OtherPlayerId }.Select(id => new B2HandChange
                { PlayerId = id, BeforeCount = before[id].Sum(), LostCount = 1, GivenCount = 1 }).ToArray();
                changes = changes.Where(change => !s.ThreeKingdomsExchange.Any(x => x.PlayerId == change.PlayerId)).ToArray();
            }
            else if (c.Kind == CommandKind.ResolveProgressChoice && pending?.Kind == "ProgressHarbor" && s.ThreeKingdomsExchange != null)
            {
                var exchange = s.ThreeKingdomsExchange; s.ThreeKingdomsExchange = null;
                foreach (var change in exchange) change.AfterCount = host.GetHand(change.PlayerId).Sum();
                changes = changes.Where(change => !exchange.Any(x => x.PlayerId == change.PlayerId)).Concat(exchange).ToArray();
            }
            B2SkillEngine.OnCardsMoved(host, "native-" + c.Id, changes);
            if (B3(s) && c.Kind == CommandKind.DiscardResources && s.Discards.Length == 0)
                B2SkillEngine.CompleteSevenDiscard(host, "dice-" + s.ThreeKingdoms.Clock.TurnInstanceId);
            if (s.ThreeKingdoms.Clock.Phase == ThreeKingdomsPhase.Play && IsB2Construction(c, pending))
                foreach (var entry in B2Buildings(s).Where(x => !buildings.ContainsKey(x.Key)))
                    B2SkillEngine.OnBuilt(host, "build-" + c.Id + "-" + entry.Key, entry.Value.Item1, entry.Value.Item2);
            if (c.Kind == CommandKind.PlayProgressCard) s.ThreeKingdomsProgressUse = new[] { "progress-" + c.Id, c.PlayerId, s.ThreeKingdoms.Generals.Single(g => g.Region == GeneralRegion.Active && g.OwnerPlayerId == c.PlayerId).TenureId };
            s.ThreeKingdomsNativePayments = null;
        }
        private static bool IsB2Construction(Command c, PendingDecision pending) => c.Kind == CommandKind.BuildRoad || c.Kind == CommandKind.BuildSettlement || c.Kind == CommandKind.BuildCity || c.Kind == CommandKind.BuildWall || c.Kind == CommandKind.PlaceFreeRoad || c.Kind == CommandKind.PlayProgressCard && (c.ProgressCard == ProgressCardKind.Medicine || c.ProgressCard == ProgressCardKind.Engineering) || c.Kind == CommandKind.ResolveProgressChoice && pending?.Kind == "ProgressRoads" && !c.Decline;
        private Dictionary<string, Tuple<string, string>> B2Buildings(GameState s)
        {
            var result = new Dictionary<string, Tuple<string, string>>();
            foreach (var x in s.Roads) result["Road:" + x.LocationId] = Tuple.Create(x.PlayerId, "Road");
            foreach (var x in s.Settlements) result["Settlement:" + x.LocationId] = Tuple.Create(x.PlayerId, "Settlement");
            foreach (var x in s.Cities) result["City:" + x.LocationId] = Tuple.Create(x.PlayerId, "City");
            // 恢复此前劫掠的城市属于修复，不创造奋音建筑事件。
            foreach (var x in s.DowngradedCities) result["City:" + x.LocationId] = Tuple.Create(x.PlayerId, "City");
            foreach (var x in s.Walls) result["Wall:" + x.LocationId] = Tuple.Create(x.PlayerId, "CityWall");

            return result;
        }
        private string[] B2TargetPlayers(GameState s, Command c)
        {
            if (c.Kind == CommandKind.CommercialHarborOffer) return new[] { c.OtherPlayerId };
            if (c.Kind != CommandKind.PlayProgressCard) return Array.Empty<string>();
            if (c.ProgressCard == ProgressCardKind.Espionage || c.ProgressCard == ProgressCardKind.GuildDues || c.ProgressCard == ProgressCardKind.Treason)
            {
                Require(!B2SkillEngine.IsTargetForbidden(s.ThreeKingdoms, c.OtherPlayerId, c.ProgressCard.ToString()), "GeneralTargetForbidden", "该武将不能成为此效果的目标。");
                return new[] { c.OtherPlayerId };
            }
            if (c.ProgressCard == ProgressCardKind.Wedding || c.ProgressCard == ProgressCardKind.Sabotage)
                return ProgressTurnOrder(s).Where(p => p.Id != c.PlayerId && (c.ProgressCard == ProgressCardKind.Wedding ? VictoryPoints(s, p.Id, true) > VictoryPoints(s, c.PlayerId, true) : VictoryPoints(s, p.Id, true) >= VictoryPoints(s, c.PlayerId, true))).Select(p => p.Id).ToArray();
            if (c.ProgressCard == ProgressCardKind.ResourceMonopoly || c.ProgressCard == ProgressCardKind.TradeMonopoly)
                return ProgressTurnOrder(s).Where(p => p.Id != c.PlayerId).Select(p => p.Id).ToArray();
            if (c.ProgressCard == ProgressCardKind.Taxation) return Buildings(s).Where(b => b.PlayerId != c.PlayerId && s.Tiles.Any(t => t.Id == c.TargetId && t.Vertices.Contains(b.LocationId))).Select(b => b.PlayerId).Distinct().ToArray();
            if (c.ProgressCard == ProgressCardKind.Diplomacy) return s.Roads.Where(r => r.LocationId == c.TargetId && r.PlayerId != c.PlayerId).Select(r => r.PlayerId).ToArray();
            if (c.ProgressCard == ProgressCardKind.Intrigue) return s.Knights.Where(k => k.LocationId == c.TargetId && k.PlayerId != c.PlayerId).Select(k => k.PlayerId).ToArray();
            return Array.Empty<string>();
        }

        private void PopulateB2View(PlayerView view)
        {
            var tk = ThreeKingdomsEngine.Project(state.ThreeKingdoms, view.PlayerId);
            view.ThreeKingdoms = tk;
            B2SkillEngine.Project(CardHost(state), tk, view.PlayerId);
            if (B3(state) && state.ThreeKingdoms.Skills.PendingDecision == null) B3DiceEngine.Project(CardHost(state), tk, view.PlayerId);
            // 换将会暂挂原技能帧；产品视图只开放最上层生命周期决策。
            if (B3(state) && tk.PendingDecision != null) { tk.SkillDecision = null; tk.SkillActions = Array.Empty<B2SkillAction>(); }
            if (tk.SkillDecision == null && (state.PendingDecision != null || state.TradeOffer != null)) tk.SkillActions = Array.Empty<B2SkillAction>();
            if (state.Phase == GamePhase.Finished) { tk.CanRetire = false; tk.SkillActions = Array.Empty<B2SkillAction>(); tk.Clock.Phase = ThreeKingdomsPhase.Finished; return; }
            bool overlay = tk.PendingDecision != null || tk.SkillDecision != null || (state.PendingDecision == null && (tk.Clock.Phase == ThreeKingdomsPhase.Preparation || tk.Clock.Phase == ThreeKingdomsPhase.Judgment || tk.Clock.Phase == ThreeKingdomsPhase.Draw || tk.Clock.Phase == ThreeKingdomsPhase.End || tk.Clock.Phase == ThreeKingdomsPhase.RoundEnd));
            if (overlay)
            {
                view.Phase = GamePhase.ThreeKingdoms;
                view.PendingDecision = new PendingDecision { Kind = tk.PendingDecision != null ? "ThreeKingdoms" + tk.PendingDecision.Kind : tk.SkillDecision != null ? "ThreeKingdomsSkill" : "ThreeKingdomsPhase", PlayerId = tk.PendingDecision?.PlayerId ?? tk.SkillDecision?.PlayerId ?? tk.Clock.ActivePlayerId };
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
            else if (d == null)
            {
                if (tk.SkillDecision == null && overlay && state.PendingDecision == null && tk.Clock.ActivePlayerId == view.PlayerId) actions.Add(Make(CommandKind.AdvanceThreeKingdomsPhase));
                foreach (var skill in tk.SkillActions ?? Array.Empty<B2SkillAction>())
                {
                    var action = Make(CommandKind.UseGeneralSkill); action.SkillCommand = Json.Copy(skill.Command); actions.Add(action);
                }
            }
            if (tk.CanRetire) actions.Add(Make(CommandKind.RetireGeneral));
            view.LegalActions = view.LegalActions.Concat(actions.Where(IsLegal)).ToArray();
        }
    }
}
