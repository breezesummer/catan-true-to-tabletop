using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Catan.Core.TradeModules;
using Catan.Core.ThreeKingdoms;

namespace Catan.Core.M4
{
    /// <summary>Pure, authoritative base-game rules. Every adapter uses Execute; animation never changes state.</summary>
    public sealed partial class CitiesKnightsGameSession
    {
        public const string RulesVersion = "catan-cities-knights-2025-en-v1";
        public const string ScenarioVersion = "m4-random-board-v001";
        public const int SaveFormatVersion = 4;
        private readonly object gate = new object();
        private readonly Scenario scenario;
        private readonly Port[] ports;
        private GameState state;
        private readonly GameHistoryJournal history = new GameHistoryJournal();
        public GameHistoryEntry[] GetHistory(string playerId, long afterSequence = 0)
        {
            lock (gate)
            {
                if (!state.Players.Any(p => p.Id == playerId)) throw new ArgumentException("未知席位。", nameof(playerId));
                return history.Project(playerId, afterSequence);
            }
        }
        private static readonly Resource[] Resources = (Resource[])Enum.GetValues(typeof(Resource));
        private CitiesKnightsGameSession(Scenario scenario, Port[] ports, GameState state) { this.scenario = scenario; this.ports = ports; this.state = state; }

        public static CitiesKnightsGameSession Create(string topologyScenarioJson, int playerCount = 4, uint seed = 1, int? targetVictoryPoints = null, bool threeKingdoms = false, bool threeKingdomsSkills = true, string threeKingdomsPackageVersion = null, TradeModuleOptions tradeModules = null)
        {
            if (playerCount != 3 && playerCount != 4) throw new ArgumentOutOfRangeException(nameof(playerCount), "基础版支持 3 或 4 人。");
            // Reuse the registered immutable topology, not the M1 controlled random sequence or rules.
            Catan.Core.GameSession.Create(topologyScenarioJson);
            var scenario = Json.Read<Scenario>(topologyScenarioJson);
            var modules = HarbormasterRules.Create(tradeModules, "m4-cities-knights");
            var random = new RandomState { State = seed == 0 ? 0x9e3779b9u : seed };
            RandomizeBoard(scenario, random);
            var ports = MakePorts(scenario, random);
            var deck = new List<DevelopmentCardKind>();
            var contenders = Enumerable.Range(0, playerCount).ToArray();
            while (contenders.Length > 1)
            {
                var rolls = contenders.Select(i => new { Player = i, Total = Draw(random, 6) + Draw(random, 6) + 2 }).ToArray();
                var high = rolls.Max(r => r.Total);
                contenders = rolls.Where(r => r.Total == high).Select(r => r.Player).ToArray();
            }
            var start = contenders[0];
            var state = new GameState
            {
                TradeModules = modules, SaveFormatVersion = HarbormasterRules.SaveFormat(SaveFormatVersion, modules), RulesVersion = RulesVersion, ScenarioId = "m4-cities-knights", ScenarioVersion = ScenarioVersion,
                ScenarioContentHash = Json.Hash(topologyScenarioJson.Replace("\r\n", "\n")), InitialSeed = seed, Random = random, TargetVictoryPoints = VictoryPointSettings.Resolve(targetVictoryPoints, HarbormasterRules.DefaultTarget(13, modules)),
                Players = Enumerable.Range(1, playerCount).Select(i => new PlayerState { Id = "P" + i, Resources = new ResourceBag(), Pieces = new PieceSupply { Roads = 15, Settlements = 5, Cities = 4 } }).ToArray(),
                Bank = new ResourceBag { Wood = 19, Brick = 19, Wool = 19, Wheat = 19, Ore = 19 },
                Phase = GamePhase.SetupSettlement, ActivePlayerId = "P" + (start + 1), StartingPlayerIndex = start,
                RobberTileId = null, DevelopmentDeck = deck.ToArray(), Tiles = Json.Copy(scenario.Topology.Tiles)
            };
            var session = new CitiesKnightsGameSession(scenario, ports, state);
            session.InitializeProgress(state);
            if (threeKingdoms) session.InitializeThreeKingdoms(state, threeKingdomsSkills, threeKingdomsPackageVersion);
            session.AssertInvariants(state);
            return session;
        }

        private bool legacyInteractionReplay;

        public CommandResult Execute(Command command)
        {
            lock (gate)
            {
                var invalid = ValidateEnvelope(command); if (invalid != null) return invalid;
                command = Json.Copy(command);
                var fingerprint = Json.Hash(Json.Write(command));
                var prior = state.ProcessedCommands.FirstOrDefault(x => x.CommandId == command.Id);
                if (prior != null) return PriorResult(prior, fingerprint);
                var next = state.CopyForTransaction();
                Command effective;
                try { effective=AuthorizeControlB6(next,command); ValidateB6Trade(next,effective); Apply(next, effective); }
                catch (RuleException ex) { return Reject(ex.Code, ex.Message); }
                RecalculateAwards(next);
                CheckVictory(next);
                if (next.Phase == GamePhase.Finished && next.ThreeKingdoms != null) Catan.Core.ThreeKingdoms.ThreeKingdomsEngine.Finish(next.ThreeKingdoms);
                var ev = new GameEvent { Sequence = ++next.EventSequence, CommandId = command.Id, Kind = B3HistoryKind(state, next, effective), PlayerId = IsB3DiceCommit(state, next) ? next.ThreeKingdoms.Clock.ActivePlayerId : effective.PlayerId,
                    TargetId = PublicTarget(effective), OtherPlayerId = effective.Kind == CommandKind.ProposeTrade || effective.Kind == CommandKind.StealResource ? effective.OtherPlayerId : null, Message = PublicMessage(effective) };
                if (ev.Kind == "RollDice") { ev.Dice1 = next.LastDice1; ev.Dice2 = next.LastDice2; ev.Message = ev.PlayerId + " 掷出 " + ev.Dice1 + "+" + ev.Dice2 + "。"; }
                var receipt = new[] { ev };
                next.Events = Append(next.Events, ev);
                next.ProcessedCommands = Append(next.ProcessedCommands, new ProcessedCommand { CommandId = command.Id, Fingerprint = fingerprint, Command = command, Events = receipt, InteractionRevision = !legacyInteractionReplay && (command.Kind == CommandKind.ProposeTrade || command.Kind == CommandKind.DiscardProgressCard) ? 1 : 0 });
                AssertInvariants(next);
                history.RecordAction(GameHistoryCapture.From(state), GameHistoryCapture.From(next), new HistoryAction
                {
                    PlayerId = ev.PlayerId, OtherPlayerId = string.IsNullOrEmpty(effective.OtherPlayerId) ? null : effective.OtherPlayerId,
                    Kind = ev.Kind, PublicTarget = PublicTarget(effective), PendingKind = state.PendingDecision?.Kind, PublicGeneralChanges = PublicGeneralChanges(state, next),
                    Card = effective.Kind == CommandKind.PlayProgressCard ? effective.ProgressCard.ToString() : null
                });
                state = next;
                return new CommandResult { Success = true, Message = state.Phase == GamePhase.Finished ? state.WinnerPlayerId + " 获胜！" : "操作已确认。", Events = Json.Copy(receipt), NewEvents = Json.Copy(receipt) };
            }
        }

        public CommandResult Preview(Command command)
        {
            lock (gate)
            {
                var invalid = ValidateEnvelope(command); if (invalid != null) return invalid;
                var prior = state.ProcessedCommands.FirstOrDefault(x => x.CommandId == command.Id);
                if (prior != null) return PriorResult(prior, Json.Hash(Json.Write(command)));
                try { var probe=state.CopyForTransaction();var effective=AuthorizeControlB6(probe,command);ValidateB6Trade(probe,effective);Apply(probe,effective); return new CommandResult { Success = true, Message = "合法操作。" }; }
                catch (RuleException ex) { return Reject(ex.Code, ex.Message); }
            }
        }

        /// <summary>由持有权威会话的 AI 调度入口调用；普通显示和网络仍使用 GetPlayerView。</summary>
        public PlayerView GetAiPlayerView(string playerId)
        {
            lock (gate)
            {
                var view = GetPlayerView(playerId);
                view.OpponentHands = state.Players.Where(p => p.Id != view.PlayerId).Select(p => new OpponentHandView
                {
                    PlayerId = p.Id, Resources = p.Resources.Copy(), DevelopmentCardCount = p.DevelopmentCards.Length,
                    Commodities = new CommodityHandCounts { Cloth = p.Commodities.Cloth, Coin = p.Commodities.Coin, Paper = p.Commodities.Paper },
                    ProgressCardCounts = Enumerable.Range(0, 3).Select(track => p.ProgressCards.Count(card => (int)ProgressCatalog.Track(card) == track)).ToArray(),
                }).ToArray();
                return view;
            }
        }

        private PlayerView GetAssetViewB6(string playerId)
        {
            lock (gate)
            {
                var own = state.Players.FirstOrDefault(p => p.Id == playerId);
                if (own == null) throw new ArgumentException("Unknown player.", nameof(playerId));
                var vertices = Array.Empty<string>(); var cities = Array.Empty<string>(); var edges = Array.Empty<string>();
                if (state.ActivePlayerId == playerId && state.TradeOffer == null)
                {
                    if (state.Phase == GamePhase.SetupSettlement || state.Phase == GamePhase.Action)
                        vertices = scenario.Topology.Vertices.Where(v => IsLegal(new Command { PlayerId = playerId, Kind = state.Phase == GamePhase.SetupSettlement ? CommandKind.SetupSettlement : CommandKind.BuildSettlement, TargetId = v.Id })).Select(v => v.Id).ToArray();
                    if (state.Phase == GamePhase.Action)
                        cities = state.Settlements.Where(v => v.PlayerId == playerId && IsLegal(new Command { PlayerId = playerId, Kind = CommandKind.BuildCity, TargetId = v.LocationId })).Select(v => v.LocationId).ToArray();
                    if (state.Phase == GamePhase.SetupRoad || state.Phase == GamePhase.Action || state.Phase == GamePhase.RoadBuilding)
                    {
                        var kind = state.Phase == GamePhase.SetupRoad ? CommandKind.SetupRoad : state.Phase == GamePhase.RoadBuilding ? CommandKind.PlaceFreeRoad : CommandKind.BuildRoad;
                        edges = scenario.Topology.Edges.Where(e => IsLegal(new Command { PlayerId = playerId, Kind = kind, TargetId = e.Id })).Select(e => e.Id).ToArray();
                    }
                }
                var view = new PlayerView
                {
                    PlayerId = playerId, ActivePlayerId = state.ActivePlayerId, StartingPlayerId = state.Players[state.StartingPlayerIndex].Id, Turn = state.Turn, Phase = state.Phase, PendingDecision = Json.Copy(state.PendingDecision),
                    Discards = Json.Copy(state.Discards), TradeOffer = Json.Copy(state.TradeOffer), OwnResources = own.Resources.Copy(), OwnDevelopmentCards = Json.Copy(own.DevelopmentCards),
                    OwnVictoryPoints = VictoryPoints(state, playerId, true), TargetVictoryPoints = state.TargetVictoryPoints.Value, DevelopmentCardPlayedThisTurn = state.DevelopmentCardPlayedThisTurn, DevelopmentDeckCount = state.DevelopmentDeck.Length,
                    Bank = state.Bank.Copy(), Players = state.Players.Select(p => new PublicPlayer { Id = p.Id, ResourceCount = HandCount(p), DevelopmentCardCount = p.DevelopmentCards.Length,
                        PlayedKnights = p.PlayedKnights, Pieces = p.Pieces.Copy(), VictoryPoints = VictoryPoints(state, p.Id, state.WinnerPlayerId == p.Id), LongestRoadLength = LongestRoad(state, p.Id) }).ToArray(),
                    Board = new BoardView { Tiles = Json.Copy(state.Tiles), Vertices = Json.Copy(scenario.Topology.Vertices), Edges = Json.Copy(scenario.Topology.Edges), Ports = Json.Copy(CurrentPortsB6(state)),
                        Settlements = Json.Copy(state.Settlements), Cities = Json.Copy(state.Cities), Roads = Json.Copy(state.Roads), RobberTileId = state.RobberTileId },
                    TradeModules = HarbormasterRules.View(state.TradeModules, HarborScores(state)),
                    LegalVertexIds = vertices, LegalCityVertexIds = cities, LegalEdgeIds = edges, LongestRoadPlayerId = state.LongestRoadPlayerId, LargestArmyPlayerId = state.LargestArmyPlayerId,
                    WinnerPlayerId = state.WinnerPlayerId, LastDice1 = state.LastDice1, LastDice2 = state.LastDice2, Events = Json.Copy(state.Events),
                    OwnCommodities = own.Commodities.Copy(), OwnImprovements = own.Improvements.ToArray(), CommodityBank = state.CommodityBank.Copy(),
                    BarbarianPosition = state.BarbarianPosition, FirstBarbarianAttack = state.FirstBarbarianAttack, LastEventDie = state.LastEventDie,
                    DefenderStrength = state.BarbarianDefenseStrength ?? state.Knights.Where(k => k.Active || B5(state)&&B2SkillEngine.B5AlwaysActiveKnights(state.ThreeKingdoms,k.PlayerId)).Sum(k => B5KnightLevel(state,k)),
                    BarbarianStrength = state.BarbarianAttackStrength ?? state.Cities.Length
                };
                if(view.TradeOffer!=null) { view.TradeOffer.B4RealGive=null;view.TradeOffer.B4ConversionPayment=false;view.TradeOffer.B4DeclarationHistoryEventId=null; }
                view.Board.Knights = Json.Copy(state.Knights); view.Board.Walls = Json.Copy(state.Walls); view.Board.Metropolises = Json.Copy(state.Metropolises);
                foreach (var pub in view.Players) { var actual = state.Players.Single(p => p.Id == pub.Id); pub.CommodityCount = 0; pub.Improvements = actual.Improvements.ToArray(); pub.DefenderPoints = actual.DefenderPoints; pub.WallSupply = 3-state.Walls.Count(w=>w.PlayerId==pub.Id); pub.KnightSupply = Enumerable.Range(1,3).Select(n=>2-state.Knights.Count(k=>k.PlayerId==pub.Id&&k.Level==n)-(state.PendingDecision?.DisplacedKnight is Knight displaced&&displaced.PlayerId==pub.Id&&displaced.Level==n?1:0)-state.DecisionQueue.Count(d=>d.DisplacedKnight!=null&&d.DisplacedKnight.PlayerId==pub.Id&&d.DisplacedKnight.Level==n)).ToArray(); }
                PopulateProgressView(state, own, view);
                view.LegalActions = LegalExpansionActions(playerId).ToArray();
                PopulateThreeKingdomsView(view);
                return view;
            }
        }
        private bool IsLegal(Command c)
        {
            if (state.ThreeKingdoms != null && c.Kind < CommandKind.ChooseGenerals && !ThreeKingdomsAllowsBaseCommand(state)) return false;
            StampThreeKingdomsWindow(c);
            // 合法动作枚举尚无命令 ID；费用批次仍须有来源，临时身份仅存在于隔离探针。
            var probe = c;
            if (B3(state) && string.IsNullOrEmpty(c.Id)) { probe = Json.Copy(c); probe.Id = "tk-legality-preview-" + state.EventSequence; }
            try { var next=state.CopyForTransaction();ValidateB6Trade(next,probe);Apply(next,probe); return true; } catch (RuleException) { return false; }
        }
        public string Save() { lock (gate) return Json.Write(state); }
        public GameState GetAuthoritativeStateForTesting() { lock (gate) return Json.Copy(state); }
        public void AssertInvariants() { lock (gate) AssertInvariants(state); }
        public static CitiesKnightsGameSession Load(string topologyScenarioJson, string save)
        {
            GameState saved;
            try { saved = Json.Read<GameState>(save); }
            catch (Exception ex) when (ex is System.Runtime.Serialization.SerializationException || ex is ArgumentException || ex is System.Xml.XmlException) { throw new InvalidDataException("Invalid save JSON.", ex); }
            if (saved == null || saved.SaveFormatVersion != HarbormasterRules.SaveFormat(SaveFormatVersion, saved.TradeModules) || saved.RulesVersion != ThreeKingdomsVersionFor(saved.ThreeKingdoms) || saved.ScenarioVersion != ScenarioVersion || saved.Players == null || saved.ProcessedCommands == null || saved.ProcessedCommands.Length > 100000)
                throw new InvalidDataException("Unsupported or malformed save.");
            if (saved.ThreeKingdoms != null && !Catan.Core.ThreeKingdoms.ThreeKingdomsEngine.HasSupportedPackage(saved.ThreeKingdoms))
                throw new InvalidDataException("三国规则版本或武将包不兼容。");
            saved.TargetVictoryPoints = saved.TargetVictoryPoints ?? 13;
            if (!VictoryPointSettings.IsValid(saved.TargetVictoryPoints)) throw new InvalidDataException("存档中的胜利分数无效。");
            var session = Create(topologyScenarioJson, saved.Players.Length, saved.InitialSeed, saved.TargetVictoryPoints, saved.ThreeKingdoms != null, saved.ThreeKingdoms?.SkillsEnabled == true, saved.ThreeKingdoms?.PackageVersion, HarbormasterRules.Restore(saved.TradeModules, saved.ScenarioId));
            foreach (var record in saved.ProcessedCommands)
            {
                if (record == null || record.Command == null) throw new InvalidDataException("Malformed command history.");
                if (record.InteractionRevision < 0 || record.InteractionRevision > 1) throw new InvalidDataException("未知的交互规则修订。");
                session.legacyInteractionReplay = record.InteractionRevision == 0;
                var result = session.Execute(record.Command);
                if (!result.Success || result.IsDuplicate) throw new InvalidDataException("Invalid saved command history.");
            }
            // 旧存档没有战斗快照；只能从完整指令重放补回，不能用已拆城后的数量推算。
            if (!saved.BarbarianAttackStrength.HasValue && !saved.BarbarianDefenseStrength.HasValue)
            {
                saved.BarbarianAttackStrength = session.state.BarbarianAttackStrength;
                saved.BarbarianDefenseStrength = session.state.BarbarianDefenseStrength;
            }
            session.legacyInteractionReplay = false;
            if (session.Save() != Json.Write(saved)) throw new InvalidDataException("Saved authority differs from deterministic replay.");
            return session;
        }

        private void ApplyBaseRules(GameState s, Command c)
        {
            var p = s.Players.FirstOrDefault(x => x.Id == c.PlayerId); Require(p != null, "UnknownPlayer", "未知席位。");
            Require(s.Phase != GamePhase.Finished, "GameFinished", "对局已经结束。");
            if (s.TradeOffer != null)
            {
                Require(c.Kind == CommandKind.AcceptTrade || c.Kind == CommandKind.RejectTrade || c.Kind == CommandKind.CancelTrade, "TradePending", "请先接受、拒绝或取消当前报价。");
                ResolveTrade(s, p, c); return;
            }
            if (c.Kind == CommandKind.DiscardResources) { Discard(s, p, c); return; }
            if (c.Kind == CommandKind.ProposeTrade) { ProposeTrade(s, p, c); return; }
            if (ApplyProgressCommand(s, p, c)) return;
            if (c.Kind == CommandKind.ResolveChoice) { ResolveChoice(s,p,c); return; }
            Require(s.ActivePlayerId == c.PlayerId || B5(s)&&B5Board(s).TokenController==c.PlayerId&&(c.Kind==CommandKind.MoveRobber||c.Kind==CommandKind.StealResource), "NotActivePlayer", "请由当前回合的玩家操作。");
            switch (c.Kind)
            {
                case CommandKind.SetupSettlement:
                    Require(s.Phase == GamePhase.SetupSettlement, "WrongPhase", "当前不能摆放开局村庄。");
                    ValidateSettlement(s, p, c.TargetId, true);
                    if (s.SetupPlacementIndex < s.Players.Length) PlaceSettlement(s,p,c.TargetId);
                    else { s.Cities=Append(s.Cities,Placement(c.TargetId,p.Id)); p.Pieces.Cities--; }
                    if (s.SetupPlacementIndex >= s.Players.Length)
                        foreach (var tile in scenario.Topology.Tiles.Where(t => t.Vertices.Contains(c.TargetId) && t.Resource != "desert")) Transfer(s.Bank, p.Resources, ParseResource(tile.Resource), 1);
                    s.PendingDecision = new PendingDecision { Kind = "SetupRoad", PlayerId = p.Id, AnchorVertexId = c.TargetId }; s.Phase = GamePhase.SetupRoad; break;
                case CommandKind.SetupRoad:
                    Require(s.Phase == GamePhase.SetupRoad && s.PendingDecision != null, "WrongPhase", "请先摆放开局村庄。");
                    ValidateRoad(s, p, c.TargetId, s.PendingDecision.AnchorVertexId); PlaceRoad(s, p, c.TargetId); s.PendingDecision = null; s.SetupPlacementIndex++;
                    if (s.SetupPlacementIndex == s.Players.Length * 2) { s.ActivePlayerId = s.Players[s.StartingPlayerIndex].Id; s.Turn = 1; s.Phase = GamePhase.ProductionAwaitRoll; }
                    else { var offset = s.SetupPlacementIndex < s.Players.Length ? s.SetupPlacementIndex : 2 * s.Players.Length - s.SetupPlacementIndex - 1; s.ActivePlayerId = s.Players[(s.StartingPlayerIndex + offset) % s.Players.Length].Id; s.Phase = GamePhase.SetupSettlement; }
                    break;
                case CommandKind.RollDice:
                    Require(s.Phase == GamePhase.ProductionAwaitRoll, "WrongPhase", "当前不能掷骰。");
                    RollCitiesKnights(s); break;
                case CommandKind.BuildRoad:
                    ActionPhase(s); ValidateRoad(s, p, c.TargetId, null); PayB6Construction(s, p, new ResourceBag { Wood = 1, Brick = 1 }, new ResourceBag { Wood = 1, Brick = 1 }); PlaceRoad(s, p, c.TargetId); break;
                case CommandKind.BuildSettlement:
                    ActionPhase(s); ValidateSettlement(s, p, c.TargetId, false); PayB6Construction(s, p, new ResourceBag { Wood = 1, Brick = 1, Wool = 1, Wheat = 1 }, new ResourceBag { Wood = 1, Brick = 1, Wool = 1, Wheat = 1 }); PlaceSettlement(s, p, c.TargetId); break;
                case CommandKind.BuildCity:
                    ActionPhase(s); BuildCity(s,p,c.TargetId); break;
                case CommandKind.BankTrade:
                    ActionPhase(s); BankTrade(s,p,c); break;
                case CommandKind.MoveRobber:
                    Require(s.Phase == GamePhase.RobberMove && s.PendingDecision != null && (!B5(s)||s.PendingDecision.PlayerId==p.Id), "WrongPhase", "当前不需要移动强盗。");
                    var destination = scenario.Topology.Tiles.FirstOrDefault(x => x.Id == c.TargetId); Require(destination != null && destination.Id != s.RobberTileId, "InvalidRobberTile", "强盗必须移动到另一地块。");
                    s.RobberTileId = destination.Id; B2SkillEngine.OnB5RobberMoved(s.ThreeKingdoms,destination.Id);
                    s.PendingDecision.EligibleVictimIds = Buildings(s).Where(b => b.PlayerId != p.Id && destination.Vertices.Contains(b.LocationId)).Select(b => b.PlayerId).Distinct().OrderBy(x => x).ToArray();
                    if (s.PendingDecision.EligibleVictimIds.Length == 0) FinishDecision(s); else { s.PendingDecision.Kind = "Steal"; s.Phase = GamePhase.RobberSteal; } break;
                case CommandKind.StealResource:
                    Require(s.Phase == GamePhase.RobberSteal && s.PendingDecision != null && (!B5(s)||s.PendingDecision.PlayerId==p.Id) && s.PendingDecision.EligibleVictimIds.Contains(c.OtherPlayerId), "InvalidVictim", "请选择强盗旁的另一位玩家。");
                    var victim = s.Players.Single(x => x.Id == c.OtherPlayerId);
                    StealHandCard(s,p,victim);
                    FinishDecision(s); break;
                case CommandKind.EndTurn:
                    ActionPhase(s); Require((B2Unlimited(s,p.Id)||B2Protected(s,p.Id)||p.ProgressCards.Length<=B5ProgressLimit(s,p.Id)),"ProgressHandLimit","结束回合前须将发展卡减至当前上限。"); ClearProgressTurn(s); s.ActivePlayerId = s.Players[(Array.FindIndex(s.Players, x => x.Id == p.Id) + 1) % s.Players.Length].Id; s.Turn++; s.Phase = GamePhase.ProductionAwaitRoll; break;
                case CommandKind.BuildWall: ActionPhase(s); BuildWall(s,p,c.TargetId); break;
                case CommandKind.ImproveCity: ActionPhase(s); BuildImprovement(s,p,c.Track); break;
                case CommandKind.RecruitKnight: ActionPhase(s); RecruitKnight(s,p,c.TargetId); break;
                case CommandKind.PromoteKnight: ActionPhase(s); PromoteKnight(s,p,c.TargetId); break;
                case CommandKind.ActivateKnight: ActionPhase(s); ActivateKnight(s,p,c.TargetId); break;
                case CommandKind.MoveKnight: case CommandKind.DisplaceKnight: ActionPhase(s); MoveKnight(s,p,c); break;
                case CommandKind.ChaseRobber: ActionPhase(s); ChaseRobber(s,p,c.TargetId); break;
                case CommandKind.DeclareVictory:
                    Require(s.Turn > 0 && VictoryPoints(s, p.Id, true) >= s.TargetVictoryPoints.Value, "InsufficientPoints", "尚未达到 " + s.TargetVictoryPoints.Value + " 分。"); break;
                default: throw new RuleException("WrongPhase", "当前没有可处理的此类决定。");
            }
        }

        private void ValidateSettlement(GameState s, PlayerState p, string vertex, bool setup)
        {
            if(!setup)B5ValidateNative(s,new Command{PlayerId=p.Id,Kind=CommandKind.BuildSettlement,TargetId=vertex});
            Require(scenario.Topology.Vertices.Any(v => v.Id == vertex), "UnknownVertex", "请选择有效顶点。");
            Require(p.Pieces.Settlements > 0, "NoSettlements", "村庄库存不足。");
            Require(!Occupied(s,vertex), "OccupiedVertex", "这个顶点已有建筑或骑士。");
            Require(!scenario.Topology.Edges.Where(e => e.Vertices.Contains(vertex)).Any(e => Buildings(s).Any(b => e.Vertices.Contains(b.LocationId))), "DistanceRule", "村庄之间必须至少间隔两条边。");
            if (!setup) Require(s.Roads.Any(r => r.PlayerId == p.Id && B5Working(s,"Road",r.LocationId) && EdgeAt(r.LocationId).Vertices.Contains(vertex)), "DisconnectedSettlement", "村庄必须连接自己的道路。");
        }
        private void ValidateRoad(GameState s, PlayerState p, string edge, string anchor)
        {
            if(anchor==null)B5ValidateNative(s,new Command{PlayerId=p.Id,Kind=CommandKind.BuildRoad,TargetId=edge});
            var location = scenario.Topology.Edges.FirstOrDefault(e => e.Id == edge); Require(location != null, "UnknownEdge", "请选择有效道路边。");
            Require(p.Pieces.Roads > 0, "NoRoads", "道路库存不足。"); Require(!s.Roads.Any(r => r.LocationId == edge), "OccupiedEdge", "这条边已有道路。");
            if (anchor != null) Require(location.Vertices.Contains(anchor), "RoadNotAtAnchor", "开局道路必须连接刚放置的村庄。");
            else Require(location.Vertices.Any(v => Buildings(s).Any(b => b.LocationId == v && b.PlayerId == p.Id) ||
                (!Blocked(s,p.Id,v) && s.Roads.Any(r => r.PlayerId == p.Id && B5Working(s,"Road",r.LocationId) && EdgeAt(r.LocationId).Vertices.Contains(v)))), "DisconnectedRoad", "道路必须连接己方建筑或未被对方建筑与骑士截断的道路。");
        }
        private bool HasLegalRoad(GameState s, PlayerState p)
        {
            foreach (var e in scenario.Topology.Edges) { try { ValidateRoad(s, p, e.Id, null); return true; } catch (RuleException) { } } return false;
        }
        private static void PlaceSettlement(GameState s, PlayerState p, string vertex) { s.Settlements = Append(s.Settlements, Placement(vertex, p.Id)); p.Pieces.Settlements--; }
        private static void PlaceRoad(GameState s, PlayerState p, string edge) { s.Roads = Append(s.Roads, Placement(edge, p.Id)); p.Pieces.Roads--; }
        private static void FinishDecision(GameState s) { s.Phase = s.PendingDecision.ReturnPhase; s.PendingDecision = null;if(B5(s))B5Board(s).TokenController=null; }
        private void Produce(GameState s, int total)
        {
            foreach (var resource in Resources)
            {
                var claims = s.Players.Select(p => new { Player = p, Amount = s.Tiles.Where(t => t.Number == total && t.Id != s.RobberTileId && t.Resource == ResourceName(resource)).Sum(t =>
                    s.Settlements.Count(b => b.PlayerId == p.Id && t.Vertices.Contains(b.LocationId)) + (resource==Resource.Brick||resource==Resource.Wheat?2:1) * s.Cities.Count(b => b.PlayerId == p.Id && t.Vertices.Contains(b.LocationId))) }).Where(c => c.Amount > 0).ToArray();
                var requested = claims.Sum(c => c.Amount);
                if (s.Bank[resource] >= requested) foreach (var claim in claims) Transfer(s.Bank, claim.Player.Resources, resource, claim.Amount);
                else if (claims.Length == 1) Transfer(s.Bank, claims[0].Player.Resources, resource, s.Bank[resource]);
            }
            foreach(var commodity in Commodities)
            {
                var resource=commodity==Commodity.Cloth?Resource.Wool:commodity==Commodity.Coin?Resource.Ore:Resource.Wood;
                var claims=s.Players.Select(p=>new {Player=p,Amount=s.Tiles.Where(t=>t.Number==total&&t.Id!=s.RobberTileId&&t.Resource==ResourceName(resource)).Sum(t=>s.Cities.Count(b=>b.PlayerId==p.Id&&t.Vertices.Contains(b.LocationId)))}).Where(c=>c.Amount>0).ToArray();
                if(s.CommodityBank[commodity]>=claims.Sum(c=>c.Amount)) foreach(var claim in claims)Transfer(s.CommodityBank,claim.Player.Commodities,commodity,claim.Amount);
                else if(claims.Length==1)Transfer(s.CommodityBank,claims[0].Player.Commodities,commodity,s.CommodityBank[commodity]);
            }
        }
        private void Discard(GameState s, PlayerState p, Command c)
        {
            var obligation = s.Discards.FirstOrDefault(x => x.PlayerId == p.Id);
            Require(s.Phase == GamePhase.Discard && obligation != null, "NoDiscardRequired", "此席位当前不需要弃牌。");
            var resources=c.Resources??new ResourceBag();var commodities=c.Commodities??new CommodityBag();
            Require(ValidBag(resources)&&ValidBag(commodities)&&resources.Total+commodities.Total==obligation.Amount&&Covers(p.Resources,resources)&&Covers(p.Commodities,commodities), "InvalidDiscard", "请恰好弃掉规定数量且自己持有的资源和商品。");
            TransferBag(p.Resources, s.Bank, resources);TransferBag(p.Commodities,s.CommodityBank,commodities); s.Discards = s.Discards.Where(x => x.PlayerId != p.Id).ToArray();
            if (s.Discards.Length == 0) {if(s.FirstBarbarianAttack)s.Phase=GamePhase.RobberMove;else {s.Phase=GamePhase.Action;s.PendingDecision=null;}}
        }
        private void ProposeTrade(GameState s, PlayerState p, Command c)
        {
            ActionPhase(s);
            ValidateB6Trade(s,c);
            bool broadcast = c.OtherPlayerId == TradeOffer.AllPlayers;
            Require(broadcast ? s.ActivePlayerId == p.Id : s.Players.Any(x => x.Id == c.OtherPlayerId) && c.OtherPlayerId != p.Id && (s.ActivePlayerId == p.Id || s.ActivePlayerId == c.OtherPlayerId), "InvalidTradePartner", "仅当前回合玩家能向所有人报价；定向交易必须包含当前回合玩家。");
            var give=c.Give??new ResourceBag();var receive=c.Receive??new ResourceBag();var gc=c.GiveCommodities??new CommodityBag();var rc=c.ReceiveCommodities??new CommodityBag();
            int[] realGive=null;
            if(B4(s)&&s.B4DeclaredPayment!=null)
            {
                Require(ValidateDeclarationTrade(s,c,s.B4DeclaredPayment),"InvalidDeclaration","报价真实牌与声明不一致。");
                realGive=Enumerable.Range(0,8).Select(r=>Enumerable.Range(0,8).Sum(u=>s.B4DeclaredPayment[r*8+u])).ToArray();
                var uses=Enumerable.Range(0,8).Select(u=>Enumerable.Range(0,8).Sum(r=>s.B4DeclaredPayment[r*8+u])).ToArray();
                give=new ResourceBag();gc=new CommodityBag();
                for(int i=0;i<5;i++)give[(Resource)i]=uses[i];
                for(int i=0;i<3;i++)gc[(Commodity)i]=uses[i+5];
                Require(Enumerable.Range(0,8).All(i=>CardHost(s).GetHand(p.Id)[i]>=realGive[i]),"InsufficientResources","没有足够真实牌提出此报价。");
                s.B4DeclaredPaymentConsumed=true;
            }
            bool conversionPayment=B4(s)&&c.PaymentCards!=null&&s.B4DeclaredPayment==null;
            if(conversionPayment)
            {
                var needs=Enumerable.Range(0,8).Select(i=>i<5?give[(Resource)i]:gc[(Commodity)(i-5)]).ToArray();
                Require(B4PaymentAllocator.Allocate(s.ThreeKingdoms,p.Id,CardHost(s).GetHand(p.Id),needs,c.PaymentCards)!=null,"InvalidPayment","真实手牌不能满足报价用途。");
                realGive=(int[])c.PaymentCards.Clone();
            }
            Require(ValidBag(give)&&ValidBag(receive)&&ValidBag(gc)&&ValidBag(rc)&&give.Total+gc.Total>0&&receive.Total+rc.Total>0&&!Resources.Any(r=>give[r]>0&&receive[r]>0)&&!Commodities.Any(r=>gc[r]>0&&rc[r]>0), "InvalidTrade", "双方均须交换不同种类的手牌。");
            Require(realGive!=null||Covers(p.Resources,give)&&Covers(p.Commodities,gc), "InsufficientResources", "没有足够手牌提出此报价。");
            s.TradeOffer = new TradeOffer { B4DeclarationHistoryEventId=s.B4DeclarationHistoryEventId, B4RealGive=realGive, B4ConversionPayment=conversionPayment, ProposerId = p.Id, OtherPlayerId = c.OtherPlayerId, Give = give.Copy(), Receive = receive.Copy(), GiveCommodities=gc.Copy(),ReceiveCommodities=rc.Copy(), RecipientPlayerIds = broadcast ? s.Players.Where(x => x.Id != p.Id && B2SkillEngine.CanVoluntaryExchangeB6(s.ThreeKingdoms,p.Id,x.Id,give.Total+gc.Total,receive.Total+rc.Total)).Select(x => x.Id).ToArray() : null };
            if (legacyInteractionReplay) return;
            // 无力支付的接收者立即以普通拒绝处理，不公开缺少的牌种或数量。
            var unavailable = s.Players.Where(x => x.Id != p.Id && (broadcast || x.Id == c.OtherPlayerId) &&
                (!Covers(x.Resources, s.TradeOffer.Receive) || !Covers(x.Commodities, s.TradeOffer.ReceiveCommodities))).Select(x => x.Id).ToArray();
            if (broadcast) s.TradeOffer.RejectedPlayerIds = unavailable;
            if (broadcast ? s.TradeOffer.PendingRecipientIds.Length == 0 : unavailable.Length > 0) s.TradeOffer = null;
        }
        private void ResolveTrade(GameState s, PlayerState p, Command c)
        {
            var offer = s.TradeOffer;
            if (c.Kind == CommandKind.CancelTrade) { Require(p.Id == offer.ProposerId, "NotProposer", "仅报价者能取消报价。"); s.TradeOffer = null; return; }
            Require(offer.CanRespond(p.Id), "NotTradeRecipient", "仅尚未拒绝的报价对象能接受或拒绝。");
            if (c.Kind == CommandKind.RejectTrade)
            {
                if (offer.IsBroadcast)
                {
                    offer.RejectedPlayerIds = (offer.RejectedPlayerIds ?? Array.Empty<string>()).Concat(new[] { p.Id }).ToArray();
                    if (offer.PendingRecipientIds.Length == 0) s.TradeOffer = null;
                }
                else s.TradeOffer = null;
                return;
            }
            ValidateB6Trade(s,c);
            var proposer = s.Players.Single(x => x.Id == offer.ProposerId);
            var give=offer.Give;var gc=offer.GiveCommodities;
            if(offer.B4RealGive!=null)
            {
                give=new ResourceBag();gc=new CommodityBag();
                for(int i=0;i<5;i++)give[(Resource)i]=offer.B4RealGive[i];
                for(int i=0;i<3;i++)gc[(Commodity)i]=offer.B4RealGive[i+5];
            }
            Require(B2SkillEngine.CanGainCardsB6(s.ThreeKingdoms,p.Id,Enumerable.Range(0,8).Select(i=>i<5?give[(Resource)i]:gc[(Commodity)(i-5)]).ToArray()) && B2SkillEngine.CanGainCardsB6(s.ThreeKingdoms,proposer.Id,Enumerable.Range(0,8).Select(i=>i<5?offer.Receive[(Resource)i]:offer.ReceiveCommodities[(Commodity)(i-5)]).ToArray()),"GainForbidden","当前不能交换这些牌。");
            Require(Covers(proposer.Resources,give)&&Covers(p.Resources,offer.Receive)&&Covers(proposer.Commodities,gc)&&Covers(p.Commodities,offer.ReceiveCommodities), "InsufficientResources", "双方手牌不足以完成这笔交易。");
            if(B4(s))
            {
                Require(!B2SkillEngine.IsB4HandLossForbidden(s.ThreeKingdoms,proposer.Id)&&!B2SkillEngine.IsB4HandLossForbidden(s.ThreeKingdoms,p.Id),"HandLossForbidden","本次出牌阶段不能失去牌。");
                if(offer.B4ConversionPayment)
                {
                    var needs=Enumerable.Range(0,8).Select(i=>i<5?offer.Give[(Resource)i]:offer.GiveCommodities[(Commodity)(i-5)]).ToArray();
                    var matrix=B4PaymentAllocator.Allocate(s.ThreeKingdoms,proposer.Id,CardHost(s).GetHand(proposer.Id),needs,offer.B4RealGive);
                    Require(matrix!=null,"InvalidPayment","报价的转换用途已失效。");
                    var historyIds=new List<string>();
                    for(int real=0;real<8;real++)for(int use=0;use<8;use++)if(real!=use&&matrix[real*8+use]>0)
                    {
                        // 该编号会投影给所有席位；仅用批内序号标识转换。
                        var id="trade-conversion-"+c.Id+":conversion-"+historyIds.Count;
                        B2SkillEngine.CommitCardConversion(CardHost(s),id,proposer.Id,real,use);historyIds.Add(id);
                    }
                    s.B4TradeConversionHistoryIds=historyIds.ToArray();
                }
            }
            TransferBag(proposer.Resources, p.Resources, give); TransferBag(p.Resources, proposer.Resources, offer.Receive);TransferBag(proposer.Commodities,p.Commodities,gc);TransferBag(p.Commodities,proposer.Commodities,offer.ReceiveCommodities); s.TradeOffer = null;
        }
        public int GetBankTradeRate(string playerId, Resource resource)
        {
            lock (gate) { if (!state.Players.Any(p => p.Id == playerId) || !Enum.IsDefined(typeof(Resource), resource)) throw new ArgumentException("Invalid player/resource."); return TradeRate(state, playerId, resource); }
        }
        private int TradeRate(GameState s, string player, Resource resource)
        {
            var owned = CurrentPortsB6(s).Where(port => Buildings(s).Any(b => B6AllianceRules.Allies(s.ThreeKingdoms,player,b.PlayerId) && port.Vertices.Contains(b.LocationId))).ToArray();
            int rate=owned.Any(p => p.Resource == resource) ? 2 : owned.Any(p => p.Resource == null) ? 3 : 4;
            return Math.Min(rate,ProgressTradeRate(s,s.Players.Single(p=>p.Id==player),resource,null));
        }
        private int LongestRoad(GameState s, string playerId)
        {
            var roads = s.Roads.Where(r => r.PlayerId == playerId && B5Working(s,"Road",r.LocationId)).Select(r => EdgeAt(r.LocationId)).ToArray();
            var blocked = new HashSet<string>(Buildings(s).Where(b => b.PlayerId != playerId).Select(b => b.LocationId).Concat(s.Knights.Where(k=>k.PlayerId!=playerId).Select(k=>k.LocationId)));
            var used = new HashSet<string>();
            int Walk(string vertex, bool arrived)
            {
                if (arrived && blocked.Contains(vertex)) return 0;
                var best = 0;
                foreach (var edge in roads.Where(e => e.Vertices.Contains(vertex) && !used.Contains(e.Id)))
                {
                    used.Add(edge.Id); best = Math.Max(best, 1 + Walk(edge.Vertices[0] == vertex ? edge.Vertices[1] : edge.Vertices[0], true)); used.Remove(edge.Id);
                }
                return best;
            }
            return roads.Length == 0 ? 0 : roads.SelectMany(e => e.Vertices).Distinct().Max(v => Walk(v, false));
        }
        private HarborScore[] HarborScores(GameState s) => s.TradeModules == null ? Array.Empty<HarborScore>() :
            HarbormasterRules.Scores(s.Players.Select(p => p.Id), CurrentPortsB6(s).SelectMany(p => p.Vertices), s.Settlements.Where(b => B5Working(s, "Settlement", b.LocationId)), s.Cities);
        private void RecalculateAwards(GameState s)
        {
            HarbormasterRules.Recalculate(s.TradeModules, HarborScores(s));
            s.LongestRoadPlayerId = Award(s.Players.Select(p => p.Id).ToArray(), s.Players.Select(p => LongestRoad(s, p.Id)).ToArray(), 5, s.LongestRoadPlayerId);
            s.LargestArmyPlayerId = null;
        }
        private static string Award(string[] ids, int[] values, int threshold, string previous)
        {
            var max = values.Max(); if (max < threshold) return null;
            var leaders = ids.Where((id, i) => values[i] == max).ToArray(); return leaders.Contains(previous) ? previous : leaders.Length == 1 ? leaders[0] : null;
        }
        private static int VictoryPoints(GameState s, string player, bool includeHidden)
        {
            return HarbormasterRules.VictoryPoints(s.TradeModules, player) + Math.Max(0,B5BoardRules.Score(s.ThreeKingdoms,player)+(B5(s)?B2SkillEngine.B5MilitaryScoreAdjustment(s.ThreeKingdoms,player):0)+s.Settlements.Count(b => b.PlayerId == player && B5Working(s,"Settlement",b.LocationId)) + 2 * s.Cities.Count(b => b.PlayerId == player) + (s.LongestRoadPlayerId == player ? 2 : 0)
                + 2*s.Metropolises.Count(m=>m.PlayerId==player)+s.Players.Single(p=>p.Id==player).DefenderPoints+s.Players.Single(p=>p.Id==player).ProgressVictoryCards.Length+(s.MerchantPlayerId==player?1:0));
        }
        private static void CheckVictory(GameState s)
        {
            if (s.TradeModules != null && (s.PendingDecision != null || s.TradeOffer != null || s.DecisionQueue.Length > 0 || s.ProductionPending || (s.Phase != GamePhase.Action && s.Phase != GamePhase.ProductionAwaitRoll && s.Phase != GamePhase.Finished))) return;
            if(B4(s)&&(B2Busy(s)||s.ThreeKingdomsProgressResolutions?.Length>0||s.ThreeKingdomsVirtualUse!=null))return;
            if (s.Turn > 0 && VictoryPoints(s, s.ActivePlayerId, true) >= s.TargetVictoryPoints.Value) { s.WinnerPlayerId = s.ActivePlayerId; B6AllianceRules.RecordVictory(s.ThreeKingdoms,s.WinnerPlayerId); s.Phase = GamePhase.Finished; s.PendingDecision = null; s.DecisionQueue=Array.Empty<PendingDecision>();s.ProductionPending=false;s.Discards = Array.Empty<DiscardRequirement>(); s.TradeOffer = null; }
        }

        private void AssertInvariants(GameState s)
        {
            HarbormasterRules.Validate(s.TradeModules, s.ScenarioId, HarborScores(s));
            ValidateThreeKingdoms(s);
            Check(VictoryPointSettings.IsValid(s.TargetVictoryPoints), "胜利分数无效。");
            void Check(bool valid, string message) { if (!valid) throw new InvalidDataException(message); }
            Check(s.SaveFormatVersion == HarbormasterRules.SaveFormat(SaveFormatVersion, s.TradeModules) && s.RulesVersion == ThreeKingdomsVersionFor(s.ThreeKingdoms) && s.ScenarioVersion == ScenarioVersion, "Invalid version identity.");
            Check(Enum.IsDefined(typeof(GamePhase), s.Phase) && s.Turn >= 0 && s.SetupPlacementIndex >= 0 && s.SetupPlacementIndex <= s.Players.Length * 2, "Invalid phase/turn.");
            Check(s.Players.Length == 3 || s.Players.Length == 4, "Invalid player count.");
            Check(s.Players.Select(p => p.Id).Distinct().Count() == s.Players.Length && s.Players.Any(p => p.Id == s.ActivePlayerId), "Invalid player identities.");
            Check(ValidBag(s.Bank) && s.Players.All(p => ValidBag(p.Resources)), "Invalid resource counts.");
            foreach (var r in Resources) Check(s.Bank[r] + s.Players.Sum(p => p.Resources[r]) + B2Escrow(s,(int)r) == 19, "Resource conservation failed: " + r);
            Check(s.Roads.Select(r => r.LocationId).Distinct().Count() == s.Roads.Length && Buildings(s).Select(b => b.LocationId).Distinct().Count() == Buildings(s).Count(), "Duplicate placement.");
            foreach (var road in s.Roads) Check(s.Players.Any(p => p.Id == road.PlayerId) && scenario.Topology.Edges.Any(e => e.Id == road.LocationId), "Invalid road.");
            foreach (var building in Buildings(s))
            {
                Check(s.Players.Any(p => p.Id == building.PlayerId) && scenario.Topology.Vertices.Any(v => v.Id == building.LocationId), "Invalid building.");
                Check(!scenario.Topology.Edges.Where(e => e.Vertices.Contains(building.LocationId)).Any(e => Buildings(s).Any(b => b.LocationId != building.LocationId && e.Vertices.Contains(b.LocationId))), "Building distance violated.");
            }
            foreach (var p in s.Players)
            {
                Check(p.Pieces.Roads >= 0 && p.Pieces.Roads + s.Roads.Count(r => r.PlayerId == p.Id) == 15, "Road conservation failed.");
                Check(p.Pieces.Settlements >= 0 && p.Pieces.Settlements + s.Settlements.Count(b => b.PlayerId == p.Id) - s.DowngradedCities.Count(b=>b.PlayerId==p.Id) == 5, "Settlement conservation failed.");
                Check(p.Pieces.Cities >= 0 && p.Pieces.Cities + s.Cities.Count(b => b.PlayerId == p.Id) + s.DowngradedCities.Count(b=>b.PlayerId==p.Id) == 4, "City conservation failed.");
                Check(p.PlayedKnights >= 0 && p.DevelopmentCards.All(c => Enum.IsDefined(typeof(DevelopmentCardKind), c.Kind) && c.BoughtTurn > 0 && c.BoughtTurn <= s.Turn), "Invalid development card age/type.");
            }
            Check(s.DevelopmentDeck.Length==0 && s.PlayedDevelopmentCards.Length==0 && s.Players.All(p=>p.DevelopmentCards.Length==0&&p.PlayedKnights==0) && s.LargestArmyPlayerId==null, "Base development cards/army are excluded.");
            ValidateExpansion(s); ValidateProgress(s);
            Check(s.Random.State != 0 && s.Random.Draws >= 0 && s.Random.AlgorithmId == "xorshift32-rejection-v1", "Invalid random state.");
            Check(s.FirstBarbarianAttack ? scenario.Topology.Tiles.Any(t => t.Id == s.RobberTileId) : s.RobberTileId==null, "Invalid robber location.");
            Check((s.Phase == GamePhase.SetupRoad || s.Phase == GamePhase.Discard || s.Phase == GamePhase.RobberMove || s.Phase == GamePhase.RobberSteal || s.Phase == GamePhase.RoadBuilding || s.Phase == GamePhase.PendingChoice) == (s.PendingDecision != null), "Invalid pending decision.");
            Check((s.Phase == GamePhase.Discard) == (s.Discards.Length > 0), "Discard phase mismatch.");
            Check(s.Discards.Select(d => d.PlayerId).Distinct().Count() == s.Discards.Length && s.Discards.All(d => d.Amount > 0 && s.Players.Any(p => p.Id == d.PlayerId && (B2(s) || HandCount(p) / 2 == d.Amount))), "Invalid discard obligation.");
            if (s.PendingDecision != null)
            {
                Check(s.Phase==GamePhase.PendingChoice?s.Players.Any(p=>p.Id==s.PendingDecision.PlayerId):s.PendingDecision.PlayerId == s.ActivePlayerId || B5(s)&&B5Board(s).TokenController==s.PendingDecision.PlayerId, "Pending actor mismatch.");
                if (s.Phase == GamePhase.SetupRoad) Check(s.PendingDecision.Kind == "SetupRoad" && Buildings(s).Any(b => b.PlayerId == s.ActivePlayerId && b.LocationId == s.PendingDecision.AnchorVertexId), "Invalid setup road anchor.");
                else if(s.Phase!=GamePhase.PendingChoice) Check(s.PendingDecision.ReturnPhase == GamePhase.Action || s.PendingDecision.ReturnPhase == GamePhase.ProductionAwaitRoll, "Invalid decision return phase.");
                if (s.Phase == GamePhase.RoadBuilding) Check(s.PendingDecision.RemainingRoads >= 1 && s.PendingDecision.RemainingRoads <= 2, "Invalid free road allowance.");
                if (s.Phase == GamePhase.RobberSteal)
                {
                    var tile = scenario.Topology.Tiles.Single(t => t.Id == s.RobberTileId);
                    var victims = Buildings(s).Where(b => b.PlayerId != (B5(s)&&B5Board(s).TokenController!=null?B5Board(s).TokenController:s.ActivePlayerId) && tile.Vertices.Contains(b.LocationId)).Select(b => b.PlayerId).Distinct().OrderBy(x => x).ToArray();
                    Check(victims.Length > 0 && victims.SequenceEqual(s.PendingDecision.EligibleVictimIds), "Invalid robber victim selection.");
                }
            }
            Check(s.TradeOffer == null || s.Phase == GamePhase.Action, "Trade outside action phase.");
            if (s.TradeOffer != null)
            {
                var offer = s.TradeOffer;
                Check(s.Players.Any(p => p.Id == offer.ProposerId), "报价者不是有效席位。");
                if (offer.IsBroadcast)
                {
                    var recipients = s.Players.Where(p => p.Id != offer.ProposerId).Select(p => p.Id).ToArray();
                    var rejected = offer.RejectedPlayerIds ?? Array.Empty<string>();
                    Check(offer.ProposerId == s.ActivePlayerId && offer.RecipientPlayerIds != null && recipients.SequenceEqual(offer.RecipientPlayerIds), "广播报价必须包含所有其他席位。");
                    Check(rejected.Distinct().Count() == rejected.Length && rejected.All(recipients.Contains) && offer.PendingRecipientIds.Length > 0, "广播报价拒绝记录无效。");
                }
                else Check(offer.ProposerId != offer.OtherPlayerId && s.Players.Any(p => p.Id == offer.OtherPlayerId) && (offer.ProposerId == s.ActivePlayerId || offer.OtherPlayerId == s.ActivePlayerId) && (offer.RecipientPlayerIds == null || offer.RecipientPlayerIds.Length == 0) && (offer.RejectedPlayerIds == null || offer.RejectedPlayerIds.Length == 0), "定向报价参与者无效。");
                Check(ValidBag(offer.Give) && ValidBag(offer.Receive) && offer.Give.Total+offer.GiveCommodities.Total > 0 && offer.Receive.Total+offer.ReceiveCommodities.Total > 0 && !Resources.Any(r => offer.Give[r] > 0 && offer.Receive[r] > 0), "Invalid trade bags.");
            }
            var roadLengths = s.Players.Select(p => LongestRoad(s, p.Id)).ToArray();
            Check(ValidAward(s.Players.Select(p => p.Id).ToArray(), roadLengths, 5, s.LongestRoadPlayerId), "Invalid longest road holder.");
            Check(ValidAward(s.Players.Select(p => p.Id).ToArray(), s.Players.Select(p => p.PlayedKnights).ToArray(), 3, s.LargestArmyPlayerId), "Invalid largest army holder.");
            Check((s.Phase == GamePhase.Finished) == (s.WinnerPlayerId != null), "Winner phase mismatch.");
            if (s.WinnerPlayerId != null) Check(s.WinnerPlayerId == s.ActivePlayerId && VictoryPoints(s, s.WinnerPlayerId, true) >= s.TargetVictoryPoints.Value, "Invalid winner.");
            Check(s.Events.Length == s.ProcessedCommands.Length && s.EventSequence == s.Events.Length, "Invalid event ledger length.");
        }
        private static bool ValidAward(string[] ids, int[] scores, int threshold, string holder)
        {
            var highest = scores.Max(); var leaders = ids.Where((id, i) => scores[i] == highest).ToArray();
            return highest < threshold ? holder == null : leaders.Length == 1 ? holder == leaders[0] : holder == null || leaders.Contains(holder);
        }

        private static int Draw(RandomState random, int maximum)
        {
            if (maximum <= 0) throw new ArgumentOutOfRangeException(nameof(maximum));
            // xorshift32 visits every NONZERO uint, so reject the tail after translating to [0, 2^32-2].
            var limit = uint.MaxValue - uint.MaxValue % (uint)maximum;
            uint n;
            do { var x = random.State; x ^= x << 13; x ^= x >> 17; x ^= x << 5; random.State = x; random.Draws++; n = x - 1; } while (n >= limit);
            return (int)(n % (uint)maximum);
        }
        private static void Shuffle<T>(IList<T> list, RandomState random) { for (var i = list.Count - 1; i > 0; i--) { var j = Draw(random, i + 1); var item = list[i]; list[i] = list[j]; list[j] = item; } }
        private static void RandomizeBoard(Scenario scenario, RandomState random)
        {
            var terrain = new List<string>(); terrain.AddRange(Enumerable.Repeat("wood", 4)); terrain.AddRange(Enumerable.Repeat("wool", 4)); terrain.AddRange(Enumerable.Repeat("wheat", 4)); terrain.AddRange(Enumerable.Repeat("brick", 3)); terrain.AddRange(Enumerable.Repeat("ore", 3)); terrain.Add("desert"); Shuffle(terrain, random);
            for (var i = 0; i < scenario.Topology.Tiles.Length; i++) { scenario.Topology.Tiles[i].Resource = terrain[i]; scenario.Topology.Tiles[i].Number = null; }
            // Alphabetical A-R spiral counterclockwise, outer ring first, skipping the desert.
            var numbers = new[] { 5, 2, 6, 3, 8, 10, 9, 12, 11, 4, 8, 10, 9, 4, 5, 6, 3, 11 };
            var order = scenario.Topology.Tiles.OrderByDescending(t => Math.Max(Math.Abs(t.Q), Math.Max(Math.Abs(t.R), Math.Abs(t.Q + t.R))))
                .ThenBy(t => Math.Atan2(-1.5 * t.R, Math.Sqrt(3) * (t.Q + t.R / 2.0))).ToArray();
            var n = 0; foreach (var tile in order) if (tile.Resource != "desert") tile.Number = numbers[n++];
        }
        private static Port[] MakePorts(Scenario scenario, RandomState random)
        {
            var coast = scenario.Topology.Edges.Where(e => scenario.Topology.Tiles.Count(t => e.Vertices.All(v => t.Vertices.Contains(v))) == 1)
                .ToArray();
            // A frame covers five directed coast edges. Start at corner V01 toward V04,
            // then follow the coast: six equal corner-to-corner modules, never a mid-frame cut.
            var start = scenario.Topology.Vertices.Where(v => coast.Any(e => e.Vertices.Contains(v.Id))).OrderBy(v => v.Y).ThenBy(v => v.X).First();
            var first = coast.Where(e => e.Vertices.Contains(start.Id)).OrderBy(e => scenario.Topology.Vertices.Single(v => v.Id == e.Vertices.Single(id => id != start.Id)).X).First();
            var directedCoast = new List<Edge>();
            var currentVertex = start.Id; var currentEdge = first;
            do
            {
                directedCoast.Add(currentEdge);
                currentVertex = currentEdge.Vertices.Single(v => v != currentVertex);
                if (currentVertex == start.Id) break;
                currentEdge = coast.Single(e => e.Id != currentEdge.Id && e.Vertices.Contains(currentVertex));
            } while (directedCoast.Count < coast.Length);
            if (directedCoast.Count != 30 || currentVertex != start.Id) throw new InvalidDataException("Invalid six-frame coastline.");

            // Official 2025 p.3 depicts these six pieces (left to right); p.11 shuffles
            // whole sea frames. Port edge offsets run 0..4 from male to female connector.
            var frames = new List<Tuple<int, Resource?>[]>
            {
                new[] { Tuple.Create<int, Resource?>(2, null) },
                new[] { Tuple.Create<int, Resource?>(2, Resource.Wood) },
                new[] { Tuple.Create<int, Resource?>(1, Resource.Wool), Tuple.Create<int, Resource?>(4, null) },
                new[] { Tuple.Create<int, Resource?>(1, Resource.Brick), Tuple.Create<int, Resource?>(4, null) },
                new[] { Tuple.Create<int, Resource?>(2, Resource.Ore) },
                new[] { Tuple.Create<int, Resource?>(1, Resource.Wheat), Tuple.Create<int, Resource?>(4, null) }
            };
            Shuffle(frames, random);
            var result = new List<Port>();
            for (var frame = 0; frame < frames.Count; frame++)
                foreach (var port in frames[frame])
                    result.Add(new Port { Id = "H" + (result.Count + 1).ToString("00"), Vertices = directedCoast[frame * 5 + port.Item1].Vertices.ToArray(), Resource = port.Item2 });
            return result.ToArray();
        }
        private static CommandResult ValidateEnvelope(Command c)
        {
            if (c == null || string.IsNullOrWhiteSpace(c.Id) || c.Id.Length > 128 || string.IsNullOrWhiteSpace(c.PlayerId) || c.PlayerId.Length > 128 || (c.TargetId != null && c.TargetId.Length > 128) || (c.OtherPlayerId != null && c.OtherPlayerId.Length > 128)) return Reject("InvalidCommand", "指令缺少有效标识。");
            if ((c.GeneralIds != null && (c.GeneralIds.Length > 5 || c.GeneralIds.Any(id => id == null || id.Length > 128))) || (c.DecisionId != null && c.DecisionId.Length > 128) || (c.WindowId != null && c.WindowId.Length > 128)) return Reject("InvalidCommand", "三国指令字段无效。");
            var skill = c.SkillCommand;
            if (skill != null && (skill.Counts == null || skill.Counts.Length != 8 || skill.Counts.Any(n => n < 0 || n > 19) || skill.TargetPlayerIds == null || skill.TargetPlayerIds.Length > 3 || skill.TargetPlayerIds.Any(id => id == null || id.Length > 128) || skill.TileIds == null || skill.TileIds.Length > 2 || skill.TileIds.Any(id => id == null || id.Length > 128) || new[] { skill.SkillId, skill.Option, skill.ProgressCardId, skill.DecisionId, skill.WindowId }.Any(value => value != null && value.Length > 128))) return Reject("InvalidCommand", "技能指令字段无效。");
            if (!Enum.IsDefined(typeof(CommandKind), c.Kind)) return Reject("UnsupportedCommand", "不支持此操作。");
            return null;
        }
        private static CommandResult PriorResult(ProcessedCommand prior, string fingerprint)
        {
            return prior.Fingerprint == fingerprint ? new CommandResult { Success = true, IsDuplicate = true, Message = "此操作已经确认。", Events = Json.Copy(prior.Events) } : Reject("CommandIdConflict", "相同指令 ID 不能用于不同操作。");
        }
        private static string PublicMessage(Command c)
        {
            // Never serialize command Resources or a purchased/stolen card to this public receipt.
            switch (c.Kind)
            {
                case CommandKind.ChooseGenerals: return c.PlayerId + " 完成私密选将。";
                case CommandKind.DeployGeneral: return c.PlayerId + " 确认武将登场选择。";
                case CommandKind.RetireGeneral: return c.PlayerId + " 发起武将轮换。";
                case CommandKind.AdvanceThreeKingdomsPhase: return c.PlayerId + " 完成当前阶段。";
                case CommandKind.UseGeneralSkill: return c.PlayerId + " 完成武将技能选择。";
                case CommandKind.BuyDevelopmentCard: return c.PlayerId + " 购买了一张发展牌。";
                case CommandKind.DiscardResources: return c.PlayerId + " 完成弃牌。";
                case CommandKind.StealResource: return c.PlayerId + " 完成强盗偷取。";
                default: return c.PlayerId + "：" + c.Kind;
            }
        }
        private static string PublicTarget(Command c)
        {
            switch (c.Kind)
            {
                case CommandKind.SetupSettlement: case CommandKind.SetupRoad: case CommandKind.BuildRoad: case CommandKind.BuildSettlement:
                case CommandKind.BuildCity: case CommandKind.PlaceFreeRoad: case CommandKind.MoveRobber: return c.TargetId;
                default: return null;
            }
        }
        private static CommandResult Reject(string code, string message) { return new CommandResult { ErrorCode = code, Message = message }; }
        private static void Require(bool condition, string code, string message) { if (!condition) throw new RuleException(code, message); }
        private sealed class RuleException : Exception { public string Code { get; } public RuleException(string code, string message) : base(message) { Code = code; } }
        private static T[] Append<T>(T[] values, T item) { var result = new T[values.Length + 1]; Array.Copy(values, result, values.Length); result[values.Length] = item; return result; }
        private Edge EdgeAt(string id) { return scenario.Topology.Edges.Single(e => e.Id == id); }
        private static IEnumerable<PiecePlacement> Buildings(GameState s) { return s.Settlements.Concat(s.Cities); }
        private static PiecePlacement Placement(string location, string player) { return new PiecePlacement { LocationId = location, PlayerId = player }; }
        private static void ActionPhase(GameState s) { Require(s.Phase == GamePhase.Action, "WrongPhase", "请先完成掷骰及待决选择。"); }
        private static bool ValidBag(ResourceBag bag) { return bag != null && Resources.All(r => bag[r] >= 0 && bag[r] <= 19); }
        private static bool Covers(ResourceBag source, ResourceBag amount) { return Resources.All(r => source[r] >= amount[r]); }
        private static void Transfer(ResourceBag from, ResourceBag to, Resource resource, int count) { Require(count >= 0 && from[resource] >= count, "InsufficientResources", "资源不足。"); from[resource] -= count; to[resource] += count; }
        private static void TransferBag(ResourceBag from, ResourceBag to, ResourceBag amount) { foreach (var r in Resources) Transfer(from, to, r, amount[r]); }
        private void Pay(GameState s, PlayerState p, ResourceBag cost)
        {
            if (B4(s)) { PayB4(s,p,cost); return; }
            int before = p.Resources.Total + p.Commodities.Total;
            Require(Covers(p.Resources, cost), "InsufficientResources", "建造所需资源不足。"); TransferBag(p.Resources, s.Bank, cost);
            if (B3(s) && cost.Total > 0)
            {
                // 实体费用先离开可抽供应；同次行动后续奖励不能拿回已进入截获区的牌。
                RecordB3NativePayment(s, p, cost, before);
            }
        }
        private static string ResourceName(Resource resource) { return resource.ToString().ToLowerInvariant(); }
        private static Resource ParseResource(string resource) { return (Resource)Enum.Parse(typeof(Resource), resource, true); }
    }
}
