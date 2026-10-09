using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Catan.Core.M3
{
    /// <summary>Pure, authoritative base-game rules. Every adapter uses Execute; animation never changes state.</summary>
    public sealed partial class SeafarersGameSession
    {
        public const string RulesVersion = "catan-seafarers-2025-en-v1";
        public const string ScenarioVersion = "m3-seafarers-v001";
        public const int SaveFormatVersion = 3;
        private readonly object gate = new object();
        private readonly SeafarersScenario scenario;
        private GameState state;
        private static readonly Resource[] Resources = (Resource[])Enum.GetValues(typeof(Resource));
        private SeafarersGameSession(SeafarersScenario scenario, GameState state) { this.scenario = scenario; this.state = state; }

        public static SeafarersGameSession Create(string scenarioId, int playerCount = 4, uint seed = 1)
        {
            if (playerCount != 3 && playerCount != 4) throw new ArgumentOutOfRangeException(nameof(playerCount), "航海家支持 3 或 4 人。");
            var scenario = SeafarersScenarios.CreateSeeded(scenarioId, playerCount, seed);
            var random = new RandomState { State = seed == 0 ? 0x9e3779b9u : seed };
            var deck = new List<DevelopmentCardKind>();
            deck.AddRange(Enumerable.Repeat(DevelopmentCardKind.Knight, scenario.Id == "pirate-islands" && playerCount == 4 ? 19 : 14));
            if (scenario.Id != "pirate-islands") deck.AddRange(Enumerable.Repeat(DevelopmentCardKind.VictoryPoint, 5));
            foreach (var kind in new[] { DevelopmentCardKind.RoadBuilding, DevelopmentCardKind.YearOfPlenty, DevelopmentCardKind.Monopoly }) deck.AddRange(Enumerable.Repeat(kind, 2));
            Shuffle(deck, random);
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
                SaveFormatVersion = SaveFormatVersion, RulesVersion = RulesVersion, ScenarioId = scenario.Id, ScenarioVersion = ScenarioVersion,
                ScenarioContentHash = Json.Hash(Json.Write(scenario)), InitialSeed = seed, Random = random,
                Players = Enumerable.Range(1, playerCount).Select(i => new PlayerState { Id = "P" + i, Resources = new ResourceBag(), Pieces = new PieceSupply { Roads = 15, Settlements = 5, Cities = 4 } }).ToArray(),
                Bank = new ResourceBag { Wood = 19, Brick = 19, Wool = 19, Wheat = 19, Ore = 19 },
                Phase = GamePhase.SetupSettlement, ActivePlayerId = "P" + (start + 1), StartingPlayerIndex = start,
                RobberTileId = scenario.InitialRobberTileId, PirateTileId = scenario.InitialPirateTileId, DevelopmentDeck = deck.ToArray(), Tiles = Json.Copy(scenario.Tiles), Ports = Json.Copy(scenario.Ports)
            };
            var session = new SeafarersGameSession(scenario, state);
            session.InitializeSeafarers(state);
            session.AssertInvariants(state);
            return session;
        }

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
                try { Apply(next, command); }
                catch (RuleException ex) { return Reject(ex.Code, ex.Message); }
                ResolveSeafarersAfterAction(next, command);
                RecalculateAwards(next);
                CheckVictory(next);
                var ev = new GameEvent { Sequence = ++next.EventSequence, CommandId = command.Id, Kind = command.Kind.ToString(), PlayerId = command.PlayerId,
                    TargetId = PublicTarget(command), OtherPlayerId = command.Kind == CommandKind.ProposeTrade || command.Kind == CommandKind.StealResource ? command.OtherPlayerId : null, Message = PublicMessage(command) };
                if (command.Kind == CommandKind.RollDice) { ev.Dice1 = next.LastDice1; ev.Dice2 = next.LastDice2; ev.Message = command.PlayerId + " 掷出 " + ev.Dice1 + "+" + ev.Dice2 + "。"; }
                var receipt = new[] { ev };
                next.Events = Append(next.Events, ev);
                next.ProcessedCommands = Append(next.ProcessedCommands, new ProcessedCommand { CommandId = command.Id, Fingerprint = fingerprint, Command = command, Events = receipt });
                AssertInvariants(next); state = next;
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
                try { Apply(state.CopyForTransaction(), command); return new CommandResult { Success = true, Message = "合法操作。" }; }
                catch (RuleException ex) { return Reject(ex.Code, ex.Message); }
            }
        }

        public PlayerView GetPlayerView(string playerId)
        {
            lock (gate)
            {
                var own = state.Players.FirstOrDefault(p => p.Id == playerId);
                if (own == null) throw new ArgumentException("Unknown player.", nameof(playerId));
                var vertices = Array.Empty<string>(); var cities = Array.Empty<string>(); var edges = Array.Empty<string>();
                if (state.ActivePlayerId == playerId && state.TradeOffer == null)
                {
                    if (state.Phase == GamePhase.SetupSettlement || state.Phase == GamePhase.Action)
                        vertices = scenario.Vertices.Where(v => IsLegal(new Command { PlayerId = playerId, Kind = state.Phase == GamePhase.SetupSettlement ? CommandKind.SetupSettlement : CommandKind.BuildSettlement, TargetId = v.Id })).Select(v => v.Id).ToArray();
                    if (state.Phase == GamePhase.Action)
                        cities = state.Settlements.Where(v => v.PlayerId == playerId && IsLegal(new Command { PlayerId = playerId, Kind = CommandKind.BuildCity, TargetId = v.LocationId })).Select(v => v.LocationId).ToArray();
                    if (state.Phase == GamePhase.SetupRoad || state.Phase == GamePhase.Action || state.Phase == GamePhase.RoadBuilding)
                    {
                        var kind = state.Phase == GamePhase.SetupRoad ? CommandKind.SetupRoad : state.Phase == GamePhase.RoadBuilding ? CommandKind.PlaceFreeRoad : CommandKind.BuildRoad;
                        edges = scenario.Edges.Where(e => IsLegal(new Command { PlayerId = playerId, Kind = kind, TargetId = e.Id })).Select(e => e.Id).ToArray();
                    }
                }
                return new PlayerView
                {
                    ScenarioId = scenario.Id, ScenarioName = scenario.Name, TargetVictoryPoints = scenario.TargetVictoryPoints, NextSetupPort = state.Phase == GamePhase.SetupPort ? Json.Copy(state.UnplacedPorts[0]) : null, LegalInvasionRootVertexIds = LegalInvasionRoots(state, playerId),
                    LegalShipEdgeIds = LegalShips(playerId), MovableShipEdgeIds = MovableShips(playerId), GoldClaims = Json.Copy(state.GoldClaims), OwnHeldPorts = Json.Copy(own.HeldPorts), ClothSupply = state.ClothSupply, LastPirateStrength = state.LastPirateStrength, WinnerPlayerIds = state.WinnerPlayerIds.ToArray(), ShipMovedThisTurn = state.ShipMovedThisTurn, LegalPortEdgeIds = LegalPortEdges(state, playerId), Wonders = Json.Copy(scenario.Wonders),
                    PlayerId = playerId, ActivePlayerId = state.ActivePlayerId, Turn = state.Turn, Phase = state.Phase, PendingDecision = Json.Copy(state.PendingDecision),
                    Discards = Json.Copy(state.Discards), TradeOffer = Json.Copy(state.TradeOffer), OwnResources = own.Resources.Copy(), OwnDevelopmentCards = Json.Copy(own.DevelopmentCards),
                    OwnVictoryPoints = VictoryPoints(state, playerId, true), DevelopmentCardPlayedThisTurn = state.DevelopmentCardPlayedThisTurn, DevelopmentDeckCount = state.DevelopmentDeck.Length,
                    Bank = state.Bank.Copy(), Players = state.Players.Select(p => new PublicPlayer { Id = p.Id, ResourceCount = p.Resources.Total, DevelopmentCardCount = p.DevelopmentCards.Length,
                        PlayedKnights = p.PlayedKnights, Pieces = p.Pieces.Copy(), ShipsRemaining = p.ShipsRemaining, Cloth = p.Cloth, BonusVictoryPoints = p.BonusVictoryPoints, WonderId = p.WonderId, WonderLevel = p.WonderLevel, VictoryPoints = VictoryPoints(state, p.Id, state.WinnerPlayerId == p.Id), LongestRoadLength = LongestRoad(state, p.Id) }).ToArray(),
                    Board = new BoardView { Tiles = Json.Copy(state.Tiles), Vertices = Json.Copy(scenario.Vertices), Edges = Json.Copy(scenario.Edges), Ports = Json.Copy(state.Ports),
                        Settlements = Json.Copy(state.Settlements), Cities = Json.Copy(state.Cities), Roads = Json.Copy(state.Roads), RobberTileId = state.RobberTileId, Ships = Json.Copy(state.Ships), PirateTileId = state.PirateTileId, BonusEdgeIds = state.BonusEdgeIds.ToArray(), GiftCardEdgeIds = state.GiftCards.Select(g => g.EdgeId).ToArray(), GiftPorts = Json.Copy(state.GiftPorts), Villages = Json.Copy(state.Villages), Fortresses = Json.Copy(state.Fortresses) },
                    LegalVertexIds = vertices, LegalCityVertexIds = cities, LegalEdgeIds = edges, LongestRoadPlayerId = state.LongestRoadPlayerId, LargestArmyPlayerId = state.LargestArmyPlayerId,
                    WinnerPlayerId = state.WinnerPlayerId, LastDice1 = state.LastDice1, LastDice2 = state.LastDice2, Events = Json.Copy(state.Events)
                };
            }
        }
        private bool IsLegal(Command c) { try { Apply(state.CopyForTransaction(), c); return true; } catch (RuleException) { return false; } }
        public string Save() { lock (gate) return Json.Write(state); }
        public GameState GetAuthoritativeStateForTesting() { lock (gate) return Json.Copy(state); }
        public void AssertInvariants() { lock (gate) AssertInvariants(state); }
        public static SeafarersGameSession Load(string save)
        {
            GameState saved;
            try { saved = Json.Read<GameState>(save); }
            catch (Exception ex) when (ex is System.Runtime.Serialization.SerializationException || ex is ArgumentException || ex is System.Xml.XmlException) { throw new InvalidDataException("Invalid save JSON.", ex); }
            if (saved == null || saved.SaveFormatVersion != SaveFormatVersion || saved.RulesVersion != RulesVersion || saved.ScenarioVersion != ScenarioVersion || saved.Players == null || saved.ProcessedCommands == null || saved.ProcessedCommands.Length > 100000)
                throw new InvalidDataException("Unsupported or malformed save.");
            SeafarersGameSession session;
            try { session = Create(saved.ScenarioId, saved.Players.Length, saved.InitialSeed); }
            catch (ArgumentException ex) { throw new InvalidDataException("Invalid saved scenario identity.", ex); }
            foreach (var record in saved.ProcessedCommands)
            {
                if (record == null || record.Command == null) throw new InvalidDataException("Malformed command history.");
                var result = session.Execute(record.Command);
                if (!result.Success || result.IsDuplicate) throw new InvalidDataException("Invalid saved command history.");
            }
            if (session.Save() != Json.Write(saved)) throw new InvalidDataException("Saved authority differs from deterministic replay.");
            return session;
        }

        private void Apply(GameState s, Command c)
        {
            var p = s.Players.FirstOrDefault(x => x.Id == c.PlayerId); Require(p != null, "UnknownPlayer", "未知席位。");
            Require(s.Phase != GamePhase.Finished, "GameFinished", "对局已经结束。");
            if (s.TradeOffer != null)
            {
                Require(c.Kind == CommandKind.AcceptTrade || c.Kind == CommandKind.RejectTrade || c.Kind == CommandKind.CancelTrade, "TradePending", "请先接受、拒绝或取消当前报价。");
                ResolveTrade(s, p, c); return;
            }
            if (c.Kind == CommandKind.ChooseGoldResources) { ChooseGold(s, p, c); return; }
            if (c.Kind == CommandKind.PlacePort) { PlacePort(s, p, c); return; }
            if (c.Kind == CommandKind.DiscardResources) { Discard(s, p, c); return; }
            if (c.Kind == CommandKind.ProposeTrade) { ProposeTrade(s, p, c); return; }
            Require(s.ActivePlayerId == c.PlayerId, "NotActivePlayer", "请由当前回合的玩家操作。");
            if (ApplySeafarers(s, p, c)) return;
            switch (c.Kind)
            {
                case CommandKind.SetupSettlement:
                    Require(s.Phase == GamePhase.SetupSettlement, "WrongPhase", "当前不能摆放开局定居点。");
                    ValidateSettlement(s, p, c.TargetId, true); PlaceSettlement(s, p, c.TargetId);
                    RecordRegion(s, p, c.TargetId, true);
                    if (s.SetupPlacementIndex >= s.Players.Length * (scenario.SetupRounds - 1)) GrantStartingResources(s, p, c.TargetId);
                    s.PendingDecision = new PendingDecision { Kind = "SetupRoad", PlayerId = p.Id, AnchorVertexId = c.TargetId }; s.Phase = GamePhase.SetupRoad; break;
                case CommandKind.SetupRoad:
                    Require(s.Phase == GamePhase.SetupRoad && s.PendingDecision != null, "WrongPhase", "请先摆放开局定居点。");
                    ValidateRoad(s, p, c.TargetId, s.PendingDecision.AnchorVertexId); PlaceRoad(s, p, c.TargetId); AdvanceSetup(s);
                    break;
                case CommandKind.RollDice:
                    Require(s.Phase == GamePhase.ProductionAwaitRoll, "WrongPhase", "当前不能掷骰。");
                    s.LastDice1 = Draw(s.Random, 6) + 1; s.LastDice2 = Draw(s.Random, 6) + 1;
                    ResolveRoll(s); break;
                case CommandKind.BuildRoad:
                    ActionPhase(s); ValidateRoad(s, p, c.TargetId, null); Pay(s, p, new ResourceBag { Wood = 1, Brick = 1 }); PlaceRoad(s, p, c.TargetId); break;
                case CommandKind.BuildSettlement:
                    ActionPhase(s); ValidateSettlement(s, p, c.TargetId, false); Pay(s, p, new ResourceBag { Wood = 1, Brick = 1, Wool = 1, Wheat = 1 }); PlaceSettlement(s, p, c.TargetId); RecordRegion(s, p, c.TargetId, false); break;
                case CommandKind.BuildCity:
                    ActionPhase(s); Require(p.Pieces.Cities > 0, "NoCities", "城市库存不足。");
                    Require(s.Settlements.Any(x => x.PlayerId == p.Id && x.LocationId == c.TargetId), "NotOwnSettlement", "只能升级自己的定居点。");
                    Pay(s, p, new ResourceBag { Wheat = 2, Ore = 3 }); s.Settlements = s.Settlements.Where(x => x.LocationId != c.TargetId).ToArray();
                    s.Cities = Append(s.Cities, Placement(c.TargetId, p.Id)); p.Pieces.Cities--; p.Pieces.Settlements++; break;
                case CommandKind.BankTrade:
                    ActionPhase(s); Require(Enum.IsDefined(typeof(Resource), c.GiveResource) && Enum.IsDefined(typeof(Resource), c.ReceiveResource) && c.GiveResource != c.ReceiveResource, "InvalidTrade", "请选择不同的有效资源。");
                    var rate = TradeRate(s, p.Id, c.GiveResource);
                    Require(c.ReceiveAmount > 0 && c.ReceiveAmount <= 19 && c.GiveAmount > 0 && c.GiveAmount <= 19 &&
                        (c.GiveAmount == 4 * c.ReceiveAmount || c.GiveAmount == 2 * c.ReceiveAmount && rate == 2 ||
                         c.GiveAmount == 3 * c.ReceiveAmount && s.Ports.Any(port => port.Resource == null && Buildings(s).Any(b => b.PlayerId == p.Id && port.Vertices.Contains(b.LocationId)))),
                        "InvalidRatio", "可用的最优兑换比例为 " + rate + ":1。");
                    Require(p.Resources[c.GiveResource] >= c.GiveAmount && s.Bank[c.ReceiveResource] >= c.ReceiveAmount, "InsufficientResources", "手牌或银行资源不足。");
                    Transfer(p.Resources, s.Bank, c.GiveResource, c.GiveAmount); Transfer(s.Bank, p.Resources, c.ReceiveResource, c.ReceiveAmount); break;
                case CommandKind.BuyDevelopmentCard:
                    ActionPhase(s); Require(s.DevelopmentDeck.Length > 0, "EmptyDeck", "发展牌已经售完。"); Pay(s, p, new ResourceBag { Wool = 1, Wheat = 1, Ore = 1 });
                    p.DevelopmentCards = Append(p.DevelopmentCards, new DevelopmentCard { Kind = s.DevelopmentDeck[0], BoughtTurn = s.Turn }); s.DevelopmentDeck = s.DevelopmentDeck.Skip(1).ToArray(); break;
                case CommandKind.PlayKnight:
                    if (scenario.Id == "pirate-islands") { ConvertWarship(s, p); break; }
                    PlayCard(s, p, DevelopmentCardKind.Knight); p.PlayedKnights++;
                    s.PendingDecision = new PendingDecision { Kind = "Robber", PlayerId = p.Id, ReturnPhase = s.Phase }; s.Phase = GamePhase.RobberMove; break;
                case CommandKind.PlayRoadBuilding:
                    Require(HasLegalRoute(s, p), "NoLegalRoad", "没有可放置的道路。"); PlayCard(s, p, DevelopmentCardKind.RoadBuilding);
                    s.PendingDecision = new PendingDecision { Kind = "RoadBuilding", PlayerId = p.Id, RemainingRoads = 2, ReturnPhase = s.Phase }; s.Phase = GamePhase.RoadBuilding; break;
                case CommandKind.PlaceFreeRoad:
                    Require(s.Phase == GamePhase.RoadBuilding && s.PendingDecision != null, "WrongPhase", "当前没有免费道路待放置。"); ValidateRoad(s, p, c.TargetId, null); PlaceRoad(s, p, c.TargetId); s.PendingDecision.RemainingRoads--;
                    if (s.PendingDecision.RemainingRoads == 0) FinishDecision(s); break;
                case CommandKind.FinishRoadBuilding:
                    Require(s.Phase == GamePhase.RoadBuilding && s.PendingDecision != null && !HasLegalRoute(s, p), "MustPlaceRoad", "仍有合法道路时必须完成道路建设。"); FinishDecision(s); break;
                case CommandKind.PlayYearOfPlenty:
                    Require(ValidBag(c.Resources) && c.Resources.Total == Math.Min(2, s.Bank.Total), "InvalidSelection", "请选择两张资源；银行不足两张时选择剩余资源。");
                    Require(Covers(s.Bank, c.Resources), "BankShortage", "银行没有所选资源。"); PlayCard(s, p, DevelopmentCardKind.YearOfPlenty); TransferBag(s.Bank, p.Resources, c.Resources); break;
                case CommandKind.PlayMonopoly:
                    Require(Enum.IsDefined(typeof(Resource), c.Resource), "InvalidResource", "请选择有效资源。"); PlayCard(s, p, DevelopmentCardKind.Monopoly);
                    foreach (var other in s.Players.Where(x => x.Id != p.Id)) Transfer(other.Resources, p.Resources, c.Resource, other.Resources[c.Resource]); break;
                case CommandKind.MoveRobber:
                    Require(s.Phase == GamePhase.RobberMove && s.PendingDecision != null, "WrongPhase", "当前不需要移动强盗。");
                    var destination = s.Tiles.FirstOrDefault(x => x.Id == c.TargetId); Require(destination != null && IsLand(destination) && destination.Id != s.RobberTileId && !scenario.ForbiddenRobberTileIds.Contains(destination.Id) && (scenario.Id != "forgotten-tribe" || destination.Number.HasValue) && scenario.Id != "pirate-islands", "InvalidRobberTile", "强盗必须移动到另一地块。");
                    s.RobberTileId = destination.Id;
                    s.PendingDecision.Token = "robber";
                    s.PendingDecision.EligibleVictimIds = Buildings(s).Where(b => b.PlayerId != p.Id && destination.Vertices.Contains(b.LocationId)).Select(b => b.PlayerId).Distinct().OrderBy(x => x).ToArray();
                    if (s.PendingDecision.EligibleVictimIds.Length == 0) FinishDecision(s); else { s.PendingDecision.Kind = "Steal"; s.Phase = GamePhase.RobberSteal; } break;
                case CommandKind.StealResource:
                    Require(s.Phase == GamePhase.RobberSteal && s.PendingDecision != null && s.PendingDecision.EligibleVictimIds.Contains(c.OtherPlayerId), "InvalidVictim", "请选择强盗旁的另一位玩家。");
                    var victim = s.Players.Single(x => x.Id == c.OtherPlayerId);
                    if (victim.Resources.Total > 0)
                    {
                        var cardIndex = Draw(s.Random, victim.Resources.Total);
                        foreach (var resource in Resources) { if (cardIndex < victim.Resources[resource]) { Transfer(victim.Resources, p.Resources, resource, 1); break; } cardIndex -= victim.Resources[resource]; }
                    }
                    FinishDecision(s); break;
                case CommandKind.EndTurn:
                    ActionPhase(s); EndTurn(s, p); break;
                case CommandKind.DeclareVictory:
                    Require(s.Turn > 0 && HasVictory(s, p.Id), "InsufficientPoints", "尚未满足本剧本胜利条件。"); break;
                default: throw new RuleException("WrongPhase", "当前没有可处理的此类决定。");
            }
        }

        private void ValidateSettlement(GameState s, PlayerState p, string vertex, bool setup)
        {
            Require(scenario.Vertices.Any(v => v.Id == vertex), "UnknownVertex", "请选择有效顶点。");
            Require(p.Pieces.Settlements > 0, "NoSettlements", "定居点库存不足。");
            Require(!Buildings(s).Any(x => x.LocationId == vertex), "OccupiedVertex", "这个顶点已有建筑。");
            Require(!scenario.Edges.Where(e => e.Vertices.Contains(vertex)).Any(e => Buildings(s).Any(b => e.Vertices.Contains(b.LocationId))), "DistanceRule", "定居点之间必须至少间隔两条边。");
            ValidateSeafarersSettlement(s, p, vertex, setup);
            if (!setup) Require(s.Roads.Any(r => r.PlayerId == p.Id && EdgeAt(r.LocationId).Vertices.Contains(vertex)) || s.Ships.Any(r => r.PlayerId == p.Id && EdgeAt(r.LocationId).Vertices.Contains(vertex)), "DisconnectedSettlement", "定居点必须连接自己的道路。");
        }
        private void ValidateRoad(GameState s, PlayerState p, string edge, string anchor)
        {
            var location = scenario.Edges.FirstOrDefault(e => e.Id == edge); Require(location != null, "UnknownEdge", "请选择有效道路边。");
            Require(EdgeTiles(s, edge).Any(IsLand), "RoadOnSea", "道路必须放在陆地边缘。");
            Require(!s.Ships.Any(r => r.LocationId == edge), "OccupiedEdge", "这条边已有船。");
            Require(p.Pieces.Roads > 0, "NoRoads", "道路库存不足。"); Require(!s.Roads.Any(r => r.LocationId == edge), "OccupiedEdge", "这条边已有道路。");
            if (anchor != null) Require(location.Vertices.Contains(anchor), "RoadNotAtAnchor", "开局道路必须连接刚放置的定居点。");
            else Require(location.Vertices.Any(v => Buildings(s).Any(b => b.LocationId == v && b.PlayerId == p.Id) ||
                (!Buildings(s).Any(b => b.LocationId == v && b.PlayerId != p.Id) && s.Roads.Any(r => r.PlayerId == p.Id && EdgeAt(r.LocationId).Vertices.Contains(v)))), "DisconnectedRoad", "道路必须连接己方建筑或未被对方建筑截断的道路。");
        }
        private bool HasLegalRoad(GameState s, PlayerState p)
        {
            foreach (var e in scenario.Edges) { try { ValidateRoad(s, p, e.Id, null); return true; } catch (RuleException) { } } return false;
        }
        private static void PlaceSettlement(GameState s, PlayerState p, string vertex) { s.Settlements = Append(s.Settlements, Placement(vertex, p.Id)); p.Pieces.Settlements--; }
        private static void PlaceRoad(GameState s, PlayerState p, string edge) { s.Roads = Append(s.Roads, Placement(edge, p.Id)); p.Pieces.Roads--; }
        private static void FinishDecision(GameState s) { s.Phase = s.PendingDecision.ReturnPhase; s.PendingDecision = null; }
        private void Produce(GameState s, int total)
        {
            foreach (var resource in Resources)
            {
                var claims = s.Players.Select(p => new { Player = p, Amount = s.Tiles.Where(t => t.Number == total && t.Id != s.RobberTileId && t.Resource == ResourceName(resource)).Sum(t =>
                    s.Settlements.Count(b => b.PlayerId == p.Id && t.Vertices.Contains(b.LocationId)) + 2 * s.Cities.Count(b => b.PlayerId == p.Id && t.Vertices.Contains(b.LocationId))) }).Where(c => c.Amount > 0).ToArray();
                var requested = claims.Sum(c => c.Amount);
                if (s.Bank[resource] >= requested) foreach (var claim in claims) Transfer(s.Bank, claim.Player.Resources, resource, claim.Amount);
                else if (claims.Length == 1) Transfer(s.Bank, claims[0].Player.Resources, resource, s.Bank[resource]);
            }
        }
        private void Discard(GameState s, PlayerState p, Command c)
        {
            var obligation = s.Discards.FirstOrDefault(x => x.PlayerId == p.Id);
            Require(s.Phase == GamePhase.Discard && obligation != null, "NoDiscardRequired", "此席位当前不需要弃牌。");
            Require(ValidBag(c.Resources) && c.Resources.Total == obligation.Amount && Covers(p.Resources, c.Resources), "InvalidDiscard", "请恰好弃掉规定数量且自己持有的资源。");
            TransferBag(p.Resources, s.Bank, c.Resources); s.Discards = s.Discards.Where(x => x.PlayerId != p.Id).ToArray();
            if (s.Discards.Length == 0) BeginRobberOrPirate(s);
        }
        private static void ProposeTrade(GameState s, PlayerState p, Command c)
        {
            ActionPhase(s); Require(s.Players.Any(x => x.Id == c.OtherPlayerId) && c.OtherPlayerId != p.Id && (s.ActivePlayerId == p.Id || s.ActivePlayerId == c.OtherPlayerId), "InvalidTradePartner", "交易必须包含当前回合玩家及另一席位。");
            Require(ValidBag(c.Give) && ValidBag(c.Receive) && c.Give.Total > 0 && c.Receive.Total > 0 && !Resources.Any(r => c.Give[r] > 0 && c.Receive[r] > 0), "InvalidTrade", "双方均须交换资源且不能交换同种资源。");
            Require(Covers(p.Resources, c.Give), "InsufficientResources", "没有足够资源提出此报价。");
            s.TradeOffer = new TradeOffer { ProposerId = p.Id, OtherPlayerId = c.OtherPlayerId, Give = c.Give.Copy(), Receive = c.Receive.Copy() };
        }
        private static void ResolveTrade(GameState s, PlayerState p, Command c)
        {
            var offer = s.TradeOffer;
            if (c.Kind == CommandKind.CancelTrade) { Require(p.Id == offer.ProposerId, "NotProposer", "仅报价者能取消报价。"); s.TradeOffer = null; return; }
            Require(p.Id == offer.OtherPlayerId, "NotTradeRecipient", "请由报价对象接受或拒绝。");
            if (c.Kind == CommandKind.RejectTrade) { s.TradeOffer = null; return; }
            var proposer = s.Players.Single(x => x.Id == offer.ProposerId);
            Require(Covers(proposer.Resources, offer.Give) && Covers(p.Resources, offer.Receive), "InsufficientResources", "双方资源不足以完成这笔交易。");
            TransferBag(proposer.Resources, p.Resources, offer.Give); TransferBag(p.Resources, proposer.Resources, offer.Receive); s.TradeOffer = null;
        }
        public int GetBankTradeRate(string playerId, Resource resource)
        {
            lock (gate) { if (!state.Players.Any(p => p.Id == playerId) || !Enum.IsDefined(typeof(Resource), resource)) throw new ArgumentException("Invalid player/resource."); return TradeRate(state, playerId, resource); }
        }
        private int TradeRate(GameState s, string player, Resource resource)
        {
            var owned = s.Ports.Where(port => Buildings(s).Any(b => b.PlayerId == player && port.Vertices.Contains(b.LocationId))).ToArray();
            return owned.Any(p => p.Resource == resource) ? 2 : owned.Any(p => p.Resource == null) ? 3 : 4;
        }
        private static void PlayCard(GameState s, PlayerState p, DevelopmentCardKind kind)
        {
            Require(s.Phase == GamePhase.ProductionAwaitRoll || s.Phase == GamePhase.Action, "WrongPhase", "发展牌只能在自己的回合、没有待决选择时使用。");
            Require(!s.DevelopmentCardPlayedThisTurn, "CardAlreadyPlayed", "每回合只能使用一张发展牌。");
            var index = Array.FindIndex(p.DevelopmentCards, c => c.Kind == kind && c.BoughtTurn < s.Turn);
            Require(index >= 0, "NoPlayableCard", "没有可用的此类发展牌；购买当回合不能使用。");
            p.DevelopmentCards = p.DevelopmentCards.Where((c, i) => i != index).ToArray(); s.PlayedDevelopmentCards = Append(s.PlayedDevelopmentCards, kind); s.DevelopmentCardPlayedThisTurn = true;
        }
        private int LongestRoad(GameState s, string playerId)
        {
            var roads = s.Roads.Where(r => r.PlayerId == playerId).Select(r => EdgeAt(r.LocationId)).Concat(s.Ships.Where(r => r.PlayerId == playerId).Select(r => EdgeAt(r.LocationId))).ToArray();
            var shipIds = new HashSet<string>(s.Ships.Where(r => r.PlayerId == playerId).Select(r => r.LocationId));
            var blocked = new HashSet<string>(Buildings(s).Where(b => b.PlayerId != playerId).Select(b => b.LocationId));
            var used = new HashSet<string>();
            int Walk(string vertex, bool arrived, string previousEdge)
            {
                if (arrived && blocked.Contains(vertex)) return 0;
                var best = 0;
                foreach (var edge in roads.Where(e => e.Vertices.Contains(vertex) && !used.Contains(e.Id)))
                {
                    if (arrived && shipIds.Contains(previousEdge) != shipIds.Contains(edge.Id) && !Buildings(s).Any(b => b.PlayerId == playerId && b.LocationId == vertex)) continue;
                    used.Add(edge.Id); best = Math.Max(best, 1 + Walk(edge.Vertices[0] == vertex ? edge.Vertices[1] : edge.Vertices[0], true, edge.Id)); used.Remove(edge.Id);
                }
                return best;
            }
            return roads.Length == 0 ? 0 : roads.SelectMany(e => e.Vertices).Distinct().Max(v => Walk(v, false, null));
        }
        private void RecalculateAwards(GameState s)
        {
            s.LongestRoadPlayerId = !scenario.UseLongestRoute ? null : Award(s.Players.Select(p => p.Id).ToArray(), s.Players.Select(p => LongestRoad(s, p.Id)).ToArray(), 5, s.LongestRoadPlayerId);
            s.LargestArmyPlayerId = !scenario.UseLargestArmy ? null : Award(s.Players.Select(p => p.Id).ToArray(), s.Players.Select(p => p.PlayedKnights).ToArray(), 3, s.LargestArmyPlayerId);
        }
        private static string Award(string[] ids, int[] values, int threshold, string previous)
        {
            var max = values.Max(); if (max < threshold) return null;
            var leaders = ids.Where((id, i) => values[i] == max).ToArray(); return leaders.Contains(previous) ? previous : leaders.Length == 1 ? leaders[0] : null;
        }
        private static int VictoryPoints(GameState s, string player, bool includeHidden)
        {
            return s.Settlements.Count(b => b.PlayerId == player) + 2 * s.Cities.Count(b => b.PlayerId == player) + (s.LongestRoadPlayerId == player ? 2 : 0) + (s.LargestArmyPlayerId == player ? 2 : 0)
                + s.Players.Single(p => p.Id == player).BonusVictoryPoints + s.Players.Single(p => p.Id == player).Cloth / 2
                + (includeHidden ? s.Players.Single(p => p.Id == player).DevelopmentCards.Count(c => c.Kind == DevelopmentCardKind.VictoryPoint) : 0);
        }
        private void CheckVictory(GameState s)
        {
            if (s.Turn > 0 && HasVictory(s, s.ActivePlayerId)) FinishGame(s, new[] { s.ActivePlayerId });
        }

        private void AssertInvariants(GameState s)
        {
            void Check(bool valid, string message) { if (!valid) throw new InvalidDataException(message); }
            Check(s.SaveFormatVersion == SaveFormatVersion && s.RulesVersion == RulesVersion && s.ScenarioVersion == ScenarioVersion, "Invalid version identity.");
            Check(Enum.IsDefined(typeof(GamePhase), s.Phase) && s.Turn >= 0 && s.SetupPlacementIndex >= 0 && s.SetupPlacementIndex <= s.Players.Length * scenario.SetupRounds, "Invalid phase/turn.");
            Check(s.Players.Length == 3 || s.Players.Length == 4, "Invalid player count.");
            Check(s.Players.Select(p => p.Id).Distinct().Count() == s.Players.Length && s.Players.Any(p => p.Id == s.ActivePlayerId), "Invalid player identities.");
            Check(ValidBag(s.Bank) && s.Players.All(p => ValidBag(p.Resources)), "Invalid resource counts.");
            foreach (var r in Resources) Check(s.Bank[r] + s.Players.Sum(p => p.Resources[r]) == 19, "Resource conservation failed: " + r);
            Check(s.Roads.Select(r => r.LocationId).Concat(s.Ships.Select(r => r.LocationId)).Distinct().Count() == s.Roads.Length + s.Ships.Length && Buildings(s).Select(b => b.LocationId).Distinct().Count() == Buildings(s).Count(), "Duplicate placement.");
            foreach (var road in s.Roads) Check(s.Players.Any(p => p.Id == road.PlayerId) && scenario.Edges.Any(e => e.Id == road.LocationId), "Invalid road.");
            foreach (var building in Buildings(s))
            {
                Check(s.Players.Any(p => p.Id == building.PlayerId) && scenario.Vertices.Any(v => v.Id == building.LocationId), "Invalid building.");
                Check(!scenario.Edges.Where(e => e.Vertices.Contains(building.LocationId)).Any(e => Buildings(s).Any(b => b.LocationId != building.LocationId && e.Vertices.Contains(b.LocationId))), "Building distance violated.");
            }
            foreach (var p in s.Players)
            {
                Check(p.Pieces.Roads >= 0 && p.Pieces.Roads + s.Roads.Count(r => r.PlayerId == p.Id) == 15, "Road conservation failed.");
                Check(p.Pieces.Settlements >= 0 && p.Pieces.Settlements + s.Settlements.Count(b => b.PlayerId == p.Id) + s.Fortresses.Count(f => f.PlayerId == p.Id && f.Strength > 0) == 5, "Settlement conservation failed.");
                Check(p.Pieces.Cities >= 0 && p.Pieces.Cities + s.Cities.Count(b => b.PlayerId == p.Id) == 4, "City conservation failed.");
                Check(p.PlayedKnights >= 0 && p.DevelopmentCards.All(c => Enum.IsDefined(typeof(DevelopmentCardKind), c.Kind) && c.BoughtTurn > 0 && c.BoughtTurn <= s.Turn), "Invalid development card age/type.");
            }
            Check(s.DevelopmentDeck.All(c => Enum.IsDefined(typeof(DevelopmentCardKind), c)) && s.PlayedDevelopmentCards.All(c => Enum.IsDefined(typeof(DevelopmentCardKind), c)), "Invalid development card type.");
            foreach (DevelopmentCardKind kind in Enum.GetValues(typeof(DevelopmentCardKind)))
            {
                var expected = kind == DevelopmentCardKind.Knight ? (scenario.Id == "pirate-islands" && s.Players.Length == 4 ? 19 : 14) : kind == DevelopmentCardKind.VictoryPoint ? (scenario.Id == "pirate-islands" ? 0 : 5) : 2;
                Check(s.DevelopmentDeck.Count(c => c == kind) + s.PlayedDevelopmentCards.Count(c => c == kind) + s.Players.Sum(p => p.DevelopmentCards.Count(c => c.Kind == kind)) + s.GiftCards.Count(c => c.Kind == kind) == expected, "Development-card conservation failed.");
            }
            Check(s.Players.Sum(p => p.PlayedKnights) == s.PlayedDevelopmentCards.Count(c => c == DevelopmentCardKind.Knight), "Knight count mismatch.");
            Check(s.PlayedDevelopmentCards.All(c => c != DevelopmentCardKind.VictoryPoint), "Victory point cards cannot be played.");
            Check(s.Random.State != 0 && s.Random.Draws >= 0 && s.Random.AlgorithmId == "xorshift32-rejection-v1", "Invalid random state.");
            AssertSeafarersInvariants(s);
            Check(s.TradeOffer == null || s.Phase == GamePhase.Action, "Trade outside action phase.");
            if (s.TradeOffer != null)
            {
                var offer = s.TradeOffer;
                Check(offer.ProposerId != offer.OtherPlayerId && s.Players.Any(p => p.Id == offer.ProposerId) && s.Players.Any(p => p.Id == offer.OtherPlayerId) && (offer.ProposerId == s.ActivePlayerId || offer.OtherPlayerId == s.ActivePlayerId), "Invalid trade participants.");
                Check(ValidBag(offer.Give) && ValidBag(offer.Receive) && offer.Give.Total > 0 && offer.Receive.Total > 0 && !Resources.Any(r => offer.Give[r] > 0 && offer.Receive[r] > 0), "Invalid trade bags.");
            }
            var roadLengths = s.Players.Select(p => LongestRoad(s, p.Id)).ToArray();
            Check(!scenario.UseLongestRoute ? s.LongestRoadPlayerId == null : ValidAward(s.Players.Select(p => p.Id).ToArray(), roadLengths, 5, s.LongestRoadPlayerId), "Invalid longest road holder.");
            Check(!scenario.UseLargestArmy ? s.LargestArmyPlayerId == null : ValidAward(s.Players.Select(p => p.Id).ToArray(), s.Players.Select(p => p.PlayedKnights).ToArray(), 3, s.LargestArmyPlayerId), "Invalid largest army holder.");
            Check((s.Phase == GamePhase.Finished) == (s.WinnerPlayerId != null), "Winner phase mismatch.");
            if (s.WinnerPlayerId != null) Check(s.WinnerPlayerIds.Contains(s.WinnerPlayerId), "Invalid winner.");
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
        private static CommandResult ValidateEnvelope(Command c)
        {
            if (c == null || string.IsNullOrWhiteSpace(c.Id) || c.Id.Length > 128 || string.IsNullOrWhiteSpace(c.PlayerId) || c.PlayerId.Length > 128 || (c.TargetId != null && c.TargetId.Length > 128) || (c.OtherPlayerId != null && c.OtherPlayerId.Length > 128) || (c.SourceId != null && c.SourceId.Length > 128)) return Reject("InvalidCommand", "指令缺少有效标识。");
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
                case CommandKind.BuildCity: case CommandKind.PlaceFreeRoad: case CommandKind.MoveRobber: case CommandKind.SetupShip: case CommandKind.BuildShip: case CommandKind.MoveShip: case CommandKind.PlaceFreeShip: case CommandKind.MovePirate: return c.TargetId;
                default: return null;
            }
        }
        private static CommandResult Reject(string code, string message) { return new CommandResult { ErrorCode = code, Message = message }; }
        private static void Require(bool condition, string code, string message) { if (!condition) throw new RuleException(code, message); }
        private sealed class RuleException : Exception { public string Code { get; } public RuleException(string code, string message) : base(message) { Code = code; } }
        private static T[] Append<T>(T[] values, T item) { var result = new T[values.Length + 1]; Array.Copy(values, result, values.Length); result[values.Length] = item; return result; }
        private Edge EdgeAt(string id) { return scenario.Edges.Single(e => e.Id == id); }
        private static IEnumerable<PiecePlacement> Buildings(GameState s) { return s.Settlements.Concat(s.Cities); }
        private static PiecePlacement Placement(string location, string player) { return new PiecePlacement { LocationId = location, PlayerId = player }; }
        private static void ActionPhase(GameState s) { Require(s.Phase == GamePhase.Action, "WrongPhase", "请先完成掷骰及待决选择。"); }
        private static bool ValidBag(ResourceBag bag) { return bag != null && Resources.All(r => bag[r] >= 0 && bag[r] <= 19); }
        private static bool Covers(ResourceBag source, ResourceBag amount) { return Resources.All(r => source[r] >= amount[r]); }
        private static void Transfer(ResourceBag from, ResourceBag to, Resource resource, int count) { Require(count >= 0 && from[resource] >= count, "InsufficientResources", "资源不足。"); from[resource] -= count; to[resource] += count; }
        private static void TransferBag(ResourceBag from, ResourceBag to, ResourceBag amount) { foreach (var r in Resources) Transfer(from, to, r, amount[r]); }
        private static void Pay(GameState s, PlayerState p, ResourceBag cost) { Require(Covers(p.Resources, cost), "InsufficientResources", "建造所需资源不足。"); TransferBag(p.Resources, s.Bank, cost); }
        private static string ResourceName(Resource resource) { return resource.ToString().ToLowerInvariant(); }
        private static Resource ParseResource(string resource) { return (Resource)Enum.Parse(typeof(Resource), resource, true); }
    }
}
