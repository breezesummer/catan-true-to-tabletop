using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Catan.Core
{
    /// <summary>
    /// Authority boundary for the M1 slice. Input adapters submit commands; presentation and AI
    /// receive only detached PlayerView objects. All effects commit before presentation runs.
    /// </summary>
    public sealed class GameSession
    {
        public const string RulesVersion = "catan-m1-slice-v1";
        public const string RulesBaselineId = "catan-base-2025-en-v1";
        public const string ScenarioId = "m1-fixed-four-seats-v001";
        public const string ScenarioVersion = "1.0.0";
        public const int SaveFormatVersion = 1;
        // This slice supports exactly the immutable authored v001 fixture. A different scenario
        // needs a new registered version; changing terrain under the same ID is never implicit.
        public const string ScenarioContentHash = "b5289b97f4070386842bde828db3e6dbddb744f0877dfaded733a72ffc37dc5c";
        private readonly object gate = new object();
        private readonly Scenario scenario;
        private GameState state;

        private GameSession(Scenario scenario, GameState state) { this.scenario = scenario; this.state = state; }

        public static GameSession Create(string scenarioJson)
        {
            if (string.IsNullOrWhiteSpace(scenarioJson)) throw new InvalidDataException("Missing scenario.");
            Scenario scenario;
            try { scenario = Json.Read<Scenario>(scenarioJson); }
            catch (Exception ex) when (ex is System.Runtime.Serialization.SerializationException || ex is ArgumentException || ex is System.Xml.XmlException)
            { throw new InvalidDataException("Invalid scenario JSON.", ex); }
            if (scenario == null || scenario.SchemaVersion != 1 || scenario.Id != ScenarioId || scenario.Version != ScenarioVersion ||
                scenario.RulesVersion != RulesVersion || scenario.RulesBaselineId != RulesBaselineId)
                throw new InvalidDataException("Unsupported scenario or rules version.");
            var contentHash = Json.Hash(scenarioJson.Replace("\r\n", "\n"));
            if (contentHash != ScenarioContentHash) throw new InvalidDataException("Scenario content does not match its registered version.");
            var state = new GameState
            {
                SaveFormatVersion = SaveFormatVersion, RulesVersion = RulesVersion, RulesBaselineId = RulesBaselineId,
                ScenarioId = ScenarioId, ScenarioVersion = ScenarioVersion, ScenarioContentHash = contentHash,
                ActivePlayerId = scenario.Players[0], Turn = 0, Phase = GamePhase.SetupSettlement,
                Players = scenario.Players.Select(id => new PlayerState { Id = id, Resources = new ResourceBag(), Pieces = scenario.InitialPieces.Copy() }).ToArray(),
                Bank = scenario.InitialBank.Copy(), Settlements = Array.Empty<PiecePlacement>(), Roads = Array.Empty<PiecePlacement>(),
                RobberTileId = scenario.RobberTile,
                Random = new RandomState { AlgorithmId = scenario.ControlledRandom.AlgorithmId, Dice = scenario.ControlledRandom.Dice.Select(d => d.ToArray()).ToArray(), Cursor = scenario.ControlledRandom.InitialCursor },
                SecondSettlementVertices = Array.Empty<PiecePlacement>(), ProcessedCommands = Array.Empty<ProcessedCommand>(), Events = Array.Empty<GameEvent>()
            };
            var session = new GameSession(scenario, state);
            session.AssertInvariants(state);
            return session;
        }

        /// <summary>Submit every human, AI, test or remote request through this single entry.</summary>
        public CommandResult Execute(Command command)
        {
            lock (gate)
            {
                var malformed = ValidateEnvelope(command);
                if (malformed != null) return malformed;
                command = Json.Copy(command);
                var fingerprint = Json.Hash(Json.Write(command));
                var prior = state.ProcessedCommands.FirstOrDefault(p => p.CommandId == command.Id);
                if (prior != null) return PriorResult(prior, fingerprint);
                var invalid = Validate(state, command);
                if (invalid != null) return invalid;

                // Atomic transaction: failed validation or invariant checking cannot mutate authority.
                var next = Json.Copy(state);
                Apply(next, command);
                var confirmed = new GameEvent
                {
                    Sequence = ++next.EventSequence, CommandId = command.Id, Kind = command.Kind.ToString(),
                    PlayerId = command.PlayerId, TargetId = command.TargetId
                };
                if (command.Kind == CommandKind.RollDice)
                {
                    var dice = next.Random.Dice[next.Random.Cursor - 1];
                    confirmed.Dice1 = dice[0]; confirmed.Dice2 = dice[1];
                }
                if (command.Kind == CommandKind.BankTrade)
                {
                    confirmed.GiveResource = ResourceName(command.GiveResource); confirmed.ReceiveResource = ResourceName(command.ReceiveResource);
                    confirmed.GiveAmount = command.GiveAmount; confirmed.ReceiveAmount = command.ReceiveAmount;
                }
                var receipt = new[] { confirmed };
                next.Events = Append(next.Events, confirmed);
                next.ProcessedCommands = Append(next.ProcessedCommands, new ProcessedCommand { CommandId = command.Id, Fingerprint = fingerprint, Command = command, Events = receipt });
                AssertInvariants(next);
                state = next;
                return new CommandResult { Success = true, Message = "操作已确认。", Events = Json.Copy(receipt), NewEvents = Json.Copy(receipt) };
            }
        }

        /// <summary>Runs the exact same validation without committing, recording or publishing.</summary>
        public CommandResult Preview(Command command)
        {
            lock (gate)
            {
                var malformed = ValidateEnvelope(command);
                if (malformed != null) return malformed;
                var prior = state.ProcessedCommands.FirstOrDefault(p => p.CommandId == command.Id);
                if (prior != null) return PriorResult(prior, Json.Hash(Json.Write(command)));
                return Validate(state, command) ?? new CommandResult { Success = true, Message = "合法目标。" };
            }
        }

        public PlayerView GetPlayerView(string playerId)
        {
            lock (gate)
            {
                var viewer = state.Players.FirstOrDefault(p => p.Id == playerId);
                if (viewer == null) throw new ArgumentException("Unknown player.", nameof(playerId));
                var vertices = Array.Empty<string>();
                var edges = Array.Empty<string>();
                if (state.ActivePlayerId == playerId)
                {
                    if (state.Phase == GamePhase.SetupSettlement)
                        vertices = scenario.Topology.Vertices.Where(v => Validate(state, new Command { PlayerId = playerId, Kind = CommandKind.SetupSettlement, TargetId = v.Id }) == null).Select(v => v.Id).ToArray();
                    if (state.Phase == GamePhase.SetupRoad || state.Phase == GamePhase.Action)
                    {
                        var kind = state.Phase == GamePhase.SetupRoad ? CommandKind.SetupRoad : CommandKind.BuildRoad;
                        edges = scenario.Topology.Edges.Where(e => Validate(state, new Command { PlayerId = playerId, Kind = kind, TargetId = e.Id }) == null).Select(e => e.Id).ToArray();
                    }
                }
                return new PlayerView
                {
                    PlayerId = playerId, ActivePlayerId = state.ActivePlayerId, Turn = state.Turn, Phase = state.Phase,
                    PendingDecision = Json.Copy(state.PendingDecision), OwnResources = viewer.Resources.Copy(), Bank = state.Bank.Copy(),
                    Players = state.Players.Select(p => new PublicPlayer { Id = p.Id, ResourceCount = p.Resources.Total, Pieces = p.Pieces.Copy(), VictoryPoints = state.Settlements.Count(v => v.PlayerId == p.Id) }).ToArray(),
                    Board = new BoardView { Tiles = Json.Copy(scenario.Topology.Tiles), Vertices = Json.Copy(scenario.Topology.Vertices), Edges = Json.Copy(scenario.Topology.Edges), Settlements = Json.Copy(state.Settlements), Roads = Json.Copy(state.Roads), RobberTileId = state.RobberTileId },
                    LegalVertexIds = vertices, LegalEdgeIds = edges, Events = Json.Copy(state.Events),
                    UnsupportedFeatures = new[] { "城市建造", "港口交易", "玩家间交易", "发展卡", "强盗移动、掷7弃牌与偷取", "资源短缺分配", "最长道路与最大骑士军队", "胜利判定", "随机地图和无限掷骰", "扩展、AI对手和联机" }
                };
            }
        }

        /// <summary>Authority-only persistence. This is not a network PlayerView payload.</summary>
        public string Save() { lock (gate) return Json.Write(state); }
        /// <summary>Test inspection only; the returned copy cannot mutate the live session.</summary>
        public GameState GetAuthoritativeStateForTesting() { lock (gate) return Json.Copy(state); }

        public static GameSession Load(string scenarioJson, string saveJson)
        {
            var session = Create(scenarioJson);
            GameState saved;
            try { saved = Json.Read<GameState>(saveJson); }
            catch (Exception ex) when (ex is System.Runtime.Serialization.SerializationException || ex is ArgumentException || ex is System.Xml.XmlException)
            { throw new InvalidDataException("Invalid save JSON.", ex); }
            if (saved == null || saved.SaveFormatVersion != SaveFormatVersion || saved.RulesVersion != RulesVersion || saved.RulesBaselineId != RulesBaselineId ||
                saved.ScenarioId != ScenarioId || saved.ScenarioVersion != ScenarioVersion || saved.ScenarioContentHash != ScenarioContentHash)
                throw new InvalidDataException("Unsupported save, rules or scenario version.");
            if (saved.ProcessedCommands == null || saved.ProcessedCommands.Length > 10000)
                throw new InvalidDataException("Invalid command ledger.");
            // A finite M1 history is cheap to replay. Validate every saved field against the exact
            // same rule entry point, including hands, pending decisions, RNG, receipts and events.
            foreach (var record in saved.ProcessedCommands)
            {
                if (record == null || record.Command == null) throw new InvalidDataException("Invalid command ledger.");
                var result = session.Execute(record.Command);
                if (!result.Success || result.IsDuplicate) throw new InvalidDataException("Save contains an invalid command history.");
            }
            if (session.Save() != Json.Write(saved)) throw new InvalidDataException("Save state does not match its validated command history.");
            return session;
        }

        private static CommandResult ValidateEnvelope(Command c)
        {
            if (c == null || string.IsNullOrWhiteSpace(c.Id) || c.Id.Length > 128 || string.IsNullOrWhiteSpace(c.PlayerId) || c.PlayerId.Length > 128 ||
                (c.TargetId != null && c.TargetId.Length > 128))
                return Reject("InvalidCommand", "指令缺少有效标识。");
            if (!Enum.IsDefined(typeof(CommandKind), c.Kind)) return Reject("UnsupportedCommand", "本切片不支持此操作。");
            return null;
        }

        private CommandResult Validate(GameState s, Command c)
        {
            var player = s.Players.FirstOrDefault(p => p.Id == c.PlayerId);
            if (player == null) return Reject("UnknownPlayer", "未知席位。");
            if (s.ActivePlayerId != c.PlayerId) return Reject("NotActivePlayer", "请由当前活动席位操作。");
            if (s.PendingDecision != null && c.Kind != CommandKind.SetupRoad) return Reject("PendingDecision", "请先放置与新定居点相连的开局道路。");
            switch (c.Kind)
            {
                case CommandKind.SetupSettlement:
                    if (s.Phase != GamePhase.SetupSettlement) return Reject("WrongPhase", "当前不是开局定居点阶段。");
                    if (!scenario.Topology.Vertices.Any(v => v.Id == c.TargetId)) return Reject("UnknownVertex", "请选择棋盘上的顶点。");
                    if (s.Settlements.Any(v => v.LocationId == c.TargetId)) return Reject("OccupiedVertex", "这个顶点已被占用。");
                    if (scenario.Topology.Edges.Where(e => e.Vertices.Contains(c.TargetId)).Any(e => s.Settlements.Any(v => e.Vertices.Contains(v.LocationId))))
                        return Reject("DistanceRule", "定居点之间至少需要间隔两条边。");
                    if (player.Pieces.Settlements <= 0) return Reject("NoSettlements", "定居点库存不足。");
                    return null;
                case CommandKind.SetupRoad:
                    if (s.Phase != GamePhase.SetupRoad || s.PendingDecision == null) return Reject("WrongPhase", "当前没有开局道路待决。");
                    var setupEdge = scenario.Topology.Edges.FirstOrDefault(e => e.Id == c.TargetId);
                    if (setupEdge == null) return Reject("UnknownEdge", "请选择棋盘上的边。");
                    if (s.Roads.Any(e => e.LocationId == c.TargetId)) return Reject("OccupiedEdge", "这条边已有道路。");
                    if (!setupEdge.Vertices.Contains(s.PendingDecision.AnchorVertexId)) return Reject("RoadNotAtAnchor", "道路必须连接刚放下的定居点。");
                    if (player.Pieces.Roads <= 0) return Reject("NoRoads", "道路库存不足。");
                    if (s.SetupPlacementIndex == 7 && !BankCanPay(s, InitialProduction(s))) return Reject("ResourceShortageUnsupported", "银行供应不足；短缺分配将在 M2 实现。");
                    return null;
                case CommandKind.RollDice:
                    if (s.Phase == GamePhase.Action) return Reject("AlreadyRolled", "本回合已经掷过骰子。");
                    if (s.Phase != GamePhase.ProductionAwaitRoll) return Reject("WrongPhase", "请先完成开局摆放。");
                    if (s.Random.Cursor >= s.Random.Dice.Length) return Reject("TestConfigurationEnded", "受控骰子验收队列已结束，请重开切片。");
                    var total = s.Random.Dice[s.Random.Cursor].Sum();
                    if (total == 7) return Reject("UnsupportedSeven", "本切片尚未实现掷7流程。");
                    if (!BankCanPay(s, Production(s, total))) return Reject("ResourceShortageUnsupported", "银行供应不足；短缺分配将在 M2 实现。");
                    return null;
                case CommandKind.BankTrade:
                    var phaseError = RequireAction(s);
                    if (phaseError != null) return phaseError;
                    if (c.GiveAmount != 4 || c.ReceiveAmount != 1) return Reject("InvalidTradeRatio", "本切片仅支持同种资源 4 换 1。");
                    if (!Enum.IsDefined(typeof(Resource), c.GiveResource) || !Enum.IsDefined(typeof(Resource), c.ReceiveResource) || c.GiveResource == c.ReceiveResource)
                        return Reject("InvalidTradeResource", "请选择两种不同的有效资源。");
                    if (player.Resources[c.GiveResource] < 4) return Reject("InsufficientResources", "你的资源不足以完成交换。");
                    if (s.Bank[c.ReceiveResource] < 1) return Reject("BankInsufficient", "银行中没有所需资源。");
                    return null;
                case CommandKind.BuildRoad:
                    var buildPhaseError = RequireAction(s);
                    if (buildPhaseError != null) return buildPhaseError;
                    var edge = scenario.Topology.Edges.FirstOrDefault(e => e.Id == c.TargetId);
                    if (edge == null) return Reject("UnknownEdge", "请选择棋盘上的边。");
                    if (s.Roads.Any(e => e.LocationId == c.TargetId)) return Reject("OccupiedEdge", "这条边已有道路。");
                    if (!edge.Vertices.Any(v => ConnectsToPlayer(s, v, c.PlayerId))) return Reject("DisconnectedRoad", "道路必须连接自己的道路或定居点。");
                    if (player.Pieces.Roads <= 0) return Reject("NoRoads", "道路库存不足。");
                    if (Resources.Any(r => player.Resources[r] < scenario.RoadCost[r])) return Reject("InsufficientResources", "建路需要 1 木和 1 砖。");
                    return null;
                case CommandKind.EndTurn: return RequireAction(s);
                default: return Reject("UnsupportedCommand", "本切片不支持此操作。");
            }
        }

        private static CommandResult RequireAction(GameState s)
        {
            if (s.Phase == GamePhase.ProductionAwaitRoll) return Reject("MustRollFirst", "请先掷骰并结算产出。");
            if (s.Phase != GamePhase.Action) return Reject("WrongPhase", "请先完成开局摆放。");
            return null;
        }

        private bool ConnectsToPlayer(GameState s, string vertexId, string playerId)
        {
            var settlement = s.Settlements.FirstOrDefault(v => v.LocationId == vertexId);
            if (settlement != null) return settlement.PlayerId == playerId;
            return scenario.Topology.Edges.Where(e => e.Vertices.Contains(vertexId)).Any(e => s.Roads.Any(p => p.LocationId == e.Id && p.PlayerId == playerId));
        }

        private void Apply(GameState s, Command c)
        {
            var player = s.Players.Single(p => p.Id == c.PlayerId);
            switch (c.Kind)
            {
                case CommandKind.SetupSettlement:
                    var settlement = new PiecePlacement { LocationId = c.TargetId, PlayerId = c.PlayerId };
                    s.Settlements = Append(s.Settlements, settlement); player.Pieces.Settlements--;
                    if (s.SetupPlacementIndex >= 4) s.SecondSettlementVertices = Append(s.SecondSettlementVertices, settlement);
                    s.PendingDecision = new PendingDecision { Kind = "SetupRoad", PlayerId = c.PlayerId, AnchorVertexId = c.TargetId, PlacementNumber = s.SetupPlacementIndex + 1 };
                    s.Phase = GamePhase.SetupRoad;
                    break;
                case CommandKind.SetupRoad:
                    s.Roads = Append(s.Roads, new PiecePlacement { LocationId = c.TargetId, PlayerId = c.PlayerId }); player.Pieces.Roads--;
                    s.PendingDecision = null; s.SetupPlacementIndex++;
                    if (s.SetupPlacementIndex == 8)
                    {
                        PayProduction(s, InitialProduction(s)); s.ActivePlayerId = scenario.Players[0]; s.Turn = 1; s.Phase = GamePhase.ProductionAwaitRoll;
                    }
                    else
                    {
                        var nextSeat = s.SetupPlacementIndex < 4 ? s.SetupPlacementIndex : 7 - s.SetupPlacementIndex;
                        s.ActivePlayerId = scenario.Players[nextSeat]; s.Phase = GamePhase.SetupSettlement;
                    }
                    break;
                case CommandKind.RollDice:
                    PayProduction(s, Production(s, s.Random.Dice[s.Random.Cursor].Sum())); s.Random.Cursor++; s.Phase = GamePhase.Action;
                    break;
                case CommandKind.BankTrade:
                    player.Resources[c.GiveResource] -= 4; s.Bank[c.GiveResource] += 4;
                    player.Resources[c.ReceiveResource]++; s.Bank[c.ReceiveResource]--;
                    break;
                case CommandKind.BuildRoad:
                    foreach (var resource in Resources) { player.Resources[resource] -= scenario.RoadCost[resource]; s.Bank[resource] += scenario.RoadCost[resource]; }
                    player.Pieces.Roads--; s.Roads = Append(s.Roads, new PiecePlacement { LocationId = c.TargetId, PlayerId = c.PlayerId });
                    break;
                case CommandKind.EndTurn:
                    s.ActivePlayerId = scenario.Players[(Array.IndexOf(scenario.Players, c.PlayerId) + 1) % 4]; s.Turn++; s.Phase = GamePhase.ProductionAwaitRoll;
                    break;
            }
        }

        private Dictionary<string, ResourceBag> InitialProduction(GameState s)
        {
            var payouts = EmptyPayouts(s);
            foreach (var settlement in s.SecondSettlementVertices)
                foreach (var tile in scenario.Topology.Tiles.Where(t => t.Resource != null && t.Vertices.Contains(settlement.LocationId)))
                    payouts[settlement.PlayerId][ParseResource(tile.Resource)]++;
            return payouts;
        }
        private Dictionary<string, ResourceBag> Production(GameState s, int number)
        {
            var payouts = EmptyPayouts(s);
            foreach (var tile in scenario.Topology.Tiles.Where(t => t.Number == number && t.Resource != null && t.Id != s.RobberTileId))
                foreach (var settlement in s.Settlements.Where(v => tile.Vertices.Contains(v.LocationId)))
                    payouts[settlement.PlayerId][ParseResource(tile.Resource)]++;
            return payouts;
        }
        private static Dictionary<string, ResourceBag> EmptyPayouts(GameState s) { return s.Players.ToDictionary(p => p.Id, p => new ResourceBag()); }
        private static bool BankCanPay(GameState s, Dictionary<string, ResourceBag> payouts) { return Resources.All(r => s.Bank[r] >= payouts.Values.Sum(b => b[r])); }
        private static void PayProduction(GameState s, Dictionary<string, ResourceBag> payouts)
        {
            foreach (var player in s.Players)
                foreach (var resource in Resources) { var amount = payouts[player.Id][resource]; player.Resources[resource] += amount; s.Bank[resource] -= amount; }
        }

        private void AssertInvariants(GameState s)
        {
            foreach (var r in Resources)
                if (s.Bank[r] < 0 || s.Players.Any(p => p.Resources[r] < 0) || s.Bank[r] + s.Players.Sum(p => p.Resources[r]) != scenario.InitialBank[r])
                    throw new InvalidDataException("Resource conservation invariant failed.");
            foreach (var p in s.Players)
                if (p.Pieces.Roads < 0 || p.Pieces.Settlements < 0 || p.Pieces.Cities < 0 ||
                    p.Pieces.Roads + s.Roads.Count(r => r.PlayerId == p.Id) != scenario.InitialPieces.Roads ||
                    p.Pieces.Settlements + s.Settlements.Count(v => v.PlayerId == p.Id) != scenario.InitialPieces.Settlements || p.Pieces.Cities != scenario.InitialPieces.Cities)
                    throw new InvalidDataException("Piece conservation invariant failed.");
            if (s.Roads.Select(p => p.LocationId).Distinct().Count() != s.Roads.Length || s.Settlements.Select(p => p.LocationId).Distinct().Count() != s.Settlements.Length)
                throw new InvalidDataException("Piece occupancy invariant failed.");
            if (s.Roads.Any(p => !scenario.Topology.Edges.Any(e => e.Id == p.LocationId) || !scenario.Players.Contains(p.PlayerId)) ||
                s.Settlements.Any(p => !scenario.Topology.Vertices.Any(v => v.Id == p.LocationId) || !scenario.Players.Contains(p.PlayerId)))
                throw new InvalidDataException("Unknown board identifier.");
            if (scenario.Topology.Edges.Any(e => s.Settlements.Count(v => e.Vertices.Contains(v.LocationId)) > 1))
                throw new InvalidDataException("Settlement distance invariant failed.");
            if ((s.Phase == GamePhase.SetupRoad) != (s.PendingDecision != null)) throw new InvalidDataException("Pending decision invariant failed.");
            if (s.PendingDecision != null && (s.PendingDecision.PlayerId != s.ActivePlayerId || s.PendingDecision.Kind != "SetupRoad" ||
                s.PendingDecision.PlacementNumber != s.SetupPlacementIndex + 1 || !s.Settlements.Any(v => v.LocationId == s.PendingDecision.AnchorVertexId && v.PlayerId == s.ActivePlayerId)))
                throw new InvalidDataException("Pending anchor invariant failed.");
            if (s.Random.Cursor < 0 || s.Random.Cursor > s.Random.Dice.Length || s.Random.AlgorithmId != "fixture-dice-queue-v1") throw new InvalidDataException("Random continuation invariant failed.");
            if (s.ProcessedCommands.Length != s.EventSequence || s.Events.Length != s.EventSequence ||
                s.ProcessedCommands.Select(p => p.CommandId).Distinct().Count() != s.ProcessedCommands.Length ||
                s.Events.Where((e, i) => e.Sequence != i + 1).Any()) throw new InvalidDataException("Event and command ledger invariant failed.");
        }

        private static CommandResult PriorResult(ProcessedCommand prior, string fingerprint)
        {
            if (prior.Fingerprint != fingerprint) return Reject("CommandIdConflict", "此指令标识已用于不同请求。");
            return new CommandResult { Success = true, IsDuplicate = true, Message = "操作已确认。", Events = Json.Copy(prior.Events) };
        }
        private static CommandResult Reject(string code, string message) { return new CommandResult { ErrorCode = code, Message = message }; }
        private static T[] Append<T>(T[] array, T value) { var result = new T[array.Length + 1]; Array.Copy(array, result, array.Length); result[array.Length] = value; return result; }
        private static readonly Resource[] Resources = { Resource.Wood, Resource.Brick, Resource.Wool, Resource.Wheat, Resource.Ore };
        private static string ResourceName(Resource resource) { return resource.ToString().ToLowerInvariant(); }
        private static Resource ParseResource(string name)
        {
            foreach (var resource in Resources) if (ResourceName(resource) == name) return resource;
            throw new InvalidDataException("Unknown resource type in scenario.");
        }
    }
}
