using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Catan.Core.M5
{
    /// <summary>Pure, authoritative 2025 Cities & Knights / Seafarers rules. Every adapter uses Execute.</summary>
    public sealed partial class CombinedGameSession
    {
        public const string RulesVersion = "catan-seafarers-cities-knights-2025-en-v1";
        public const string ScenarioVersion = "m5-combined-official-2025-v001";
        public const int SaveFormatVersion = 5;
        private readonly object gate = new object();
        private readonly Scenario scenario;
        private readonly Port[] ports;
        private readonly M3.SeafarersScenario seafaring;
        private GameState state;
        private static readonly Resource[] Resources = (Resource[])Enum.GetValues(typeof(Resource));
        private CombinedGameSession(Scenario scenario, Port[] ports, M3.SeafarersScenario seafaring, GameState state) { this.scenario = scenario; this.ports = ports; this.seafaring = seafaring; this.state = state; }

        public static CombinedGameSession Create(string scenarioId, int playerCount = 4, uint seed = 1)
        {
            if (playerCount != 3 && playerCount != 4) throw new ArgumentOutOfRangeException(nameof(playerCount), "双扩展组合支持 3 或 4 人。");
            var seafaring = CombinedScenarios.Create(scenarioId, playerCount);
            var scenario = new Scenario { Id = scenarioId, Version = ScenarioVersion, Topology = new Topology { Tiles = seafaring.Tiles, Vertices = seafaring.Vertices, Edges = seafaring.Edges } };
            var random = new RandomState { State = seed == 0 ? 0x9e3779b9u : seed };
            var ports = seafaring.Ports.Select(p => new Port { Id = p.Id, Vertices = p.Vertices.ToArray(), Resource = p.Resource }).ToArray();
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
                SaveFormatVersion = SaveFormatVersion, RulesVersion = RulesVersion, ScenarioId = scenarioId, ScenarioVersion = ScenarioVersion,
                ScenarioContentHash = Json.Hash(Json.Write(seafaring)), InitialSeed = seed, Random = random,
                Players = Enumerable.Range(1, playerCount).Select(i => new PlayerState { Id = "P" + i, Resources = new ResourceBag(), Pieces = new PieceSupply { Roads = 15, Settlements = 5, Cities = 4 } }).ToArray(),
                Bank = new ResourceBag { Wood = 19, Brick = 19, Wool = 19, Wheat = 19, Ore = 19 },
                Phase = GamePhase.SetupSettlement, ActivePlayerId = "P" + (start + 1), StartingPlayerIndex = start,
                RobberTileId = null, DevelopmentDeck = deck.ToArray(), Tiles = Json.Copy(scenario.Topology.Tiles)
            };
            var session = new CombinedGameSession(scenario, ports, seafaring, state);
            session.InitializeProgress(state);
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
                    PlayerId = playerId, ActivePlayerId = state.ActivePlayerId, Turn = state.Turn, Phase = state.Phase, PendingDecision = Json.Copy(state.PendingDecision),
                    Discards = Json.Copy(state.Discards), TradeOffer = Json.Copy(state.TradeOffer), OwnResources = own.Resources.Copy(), OwnDevelopmentCards = Json.Copy(own.DevelopmentCards),
                    OwnVictoryPoints = VictoryPoints(state, playerId, true), DevelopmentCardPlayedThisTurn = state.DevelopmentCardPlayedThisTurn, DevelopmentDeckCount = state.DevelopmentDeck.Length,
                    Bank = state.Bank.Copy(), Players = state.Players.Select(p => new PublicPlayer { Id = p.Id, ResourceCount = HandCount(p), DevelopmentCardCount = p.DevelopmentCards.Length,
                        PlayedKnights = p.PlayedKnights, Pieces = p.Pieces.Copy(), VictoryPoints = VictoryPoints(state, p.Id, state.WinnerPlayerId == p.Id), LongestRoadLength = LongestRoad(state, p.Id) }).ToArray(),
                    Board = new BoardView { Tiles = Json.Copy(state.Tiles), Vertices = Json.Copy(scenario.Topology.Vertices), Edges = Json.Copy(scenario.Topology.Edges), Ports = Json.Copy(ports),
                        Settlements = Json.Copy(state.Settlements), Cities = Json.Copy(state.Cities), Roads = Json.Copy(state.Roads), RobberTileId = state.RobberTileId },
                    LegalVertexIds = vertices, LegalCityVertexIds = cities, LegalEdgeIds = edges, LongestRoadPlayerId = state.LongestRoadPlayerId, LargestArmyPlayerId = state.LargestArmyPlayerId,
                    WinnerPlayerId = state.WinnerPlayerId, LastDice1 = state.LastDice1, LastDice2 = state.LastDice2, Events = Json.Copy(state.Events),
                    OwnCommodities = own.Commodities.Copy(), OwnImprovements = own.Improvements.ToArray(), CommodityBank = state.CommodityBank.Copy(),
                    BarbarianPosition = state.BarbarianPosition, FirstBarbarianAttack = state.FirstBarbarianAttack, LastEventDie = state.LastEventDie
                };
                view.Board.Knights = Json.Copy(state.Knights); view.Board.Walls = Json.Copy(state.Walls); view.Board.Metropolises = Json.Copy(state.Metropolises);
                foreach (var pub in view.Players) { var actual = state.Players.Single(p => p.Id == pub.Id); pub.CommodityCount = 0; pub.Improvements = actual.Improvements.ToArray(); pub.DefenderPoints = actual.DefenderPoints; pub.WallSupply = 3-state.Walls.Count(w=>w.PlayerId==pub.Id); pub.KnightSupply = Enumerable.Range(1,3).Select(n=>2-state.Knights.Count(k=>k.PlayerId==pub.Id&&k.Level==n)-(state.PendingDecision?.DisplacedKnight is Knight displaced&&displaced.PlayerId==pub.Id&&displaced.Level==n?1:0)-state.DecisionQueue.Count(d=>d.DisplacedKnight!=null&&d.DisplacedKnight.PlayerId==pub.Id&&d.DisplacedKnight.Level==n)).ToArray(); }
                PopulateProgressView(state, own, view);
                PopulateSeafaringView(own,view);
                view.LegalActions = LegalExpansionActions(playerId).ToArray();
                return view;
            }
        }
        private bool IsLegal(Command c) { try { Apply(state.CopyForTransaction(), c); return true; } catch (RuleException) { return false; } }
        public string Save() { lock (gate) return Json.Write(state); }
        public GameState GetAuthoritativeStateForTesting() { lock (gate) return Json.Copy(state); }
        public void AssertInvariants() { lock (gate) AssertInvariants(state); }
        public static CombinedGameSession Load(string save)
        {
            GameState saved;
            try { saved = Json.Read<GameState>(save); }
            catch (Exception ex) when (ex is System.Runtime.Serialization.SerializationException || ex is ArgumentException || ex is System.Xml.XmlException) { throw new InvalidDataException("Invalid save JSON.", ex); }
            if (saved == null || saved.SaveFormatVersion != SaveFormatVersion || saved.RulesVersion != RulesVersion || saved.ScenarioVersion != ScenarioVersion || saved.Players == null || saved.ProcessedCommands == null || saved.ProcessedCommands.Length > 100000)
                throw new InvalidDataException("Unsupported or malformed save.");
            var session = Create(saved.ScenarioId, saved.Players.Length, saved.InitialSeed);
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
            if (c.Kind == CommandKind.DiscardResources) { Discard(s, p, c); return; }
            if (c.Kind == CommandKind.ProposeTrade) { ProposeTrade(s, p, c); return; }
            if (ApplyProgressCommand(s, p, c)) return;
            if (c.Kind == CommandKind.ResolveChoice || c.Kind == CommandKind.ChooseGoldResources) { ResolveChoice(s,p,c); return; }
            Require(s.ActivePlayerId == c.PlayerId, "NotActivePlayer", "请由当前回合的玩家操作。");
            if (ApplySeafaring(s,p,c)) return;
            switch (c.Kind)
            {
                case CommandKind.SetupSettlement:
                    Require(s.Phase == GamePhase.SetupSettlement, "WrongPhase", "当前不能摆放开局定居点。");
                    ValidateSettlement(s, p, c.TargetId, true);
                    if (s.SetupPlacementIndex < s.Players.Length) PlaceSettlement(s,p,c.TargetId);
                    else { s.Cities=Append(s.Cities,Placement(c.TargetId,p.Id)); p.Pieces.Cities--; }
                    RecordRegion(s,p,c.TargetId,true);
                    s.PendingDecision = new PendingDecision { Kind = "SetupRoad", PlayerId = p.Id, AnchorVertexId = c.TargetId }; s.Phase = GamePhase.SetupRoad;
                    if (s.SetupPlacementIndex >= s.Players.Length) GrantStartingResources(s,p,c.TargetId); break;
                case CommandKind.SetupRoad:
                    Require(s.Phase == GamePhase.SetupRoad && s.PendingDecision != null, "WrongPhase", "请先摆放开局定居点。");
                    ValidateRoad(s, p, c.TargetId, s.PendingDecision.AnchorVertexId); PlaceRoad(s, p, c.TargetId); s.PendingDecision = null; s.SetupPlacementIndex++;
                    if (s.SetupPlacementIndex == s.Players.Length * 2) { s.ActivePlayerId = s.Players[s.StartingPlayerIndex].Id; s.Turn = 1; s.Phase = GamePhase.ProductionAwaitRoll; }
                    else { var offset = s.SetupPlacementIndex < s.Players.Length ? s.SetupPlacementIndex : 2 * s.Players.Length - s.SetupPlacementIndex - 1; s.ActivePlayerId = s.Players[(s.StartingPlayerIndex + offset) % s.Players.Length].Id; s.Phase = GamePhase.SetupSettlement; }
                    break;
                case CommandKind.RollDice:
                    Require(s.Phase == GamePhase.ProductionAwaitRoll, "WrongPhase", "当前不能掷骰。");
                    RollCitiesKnights(s); break;
                case CommandKind.BuildRoad:
                    ActionPhase(s); ValidateRoad(s, p, c.TargetId, null); Pay(s, p, new ResourceBag { Wood = 1, Brick = 1 }); PlaceRoad(s, p, c.TargetId); break;
                case CommandKind.BuildSettlement:
                    ActionPhase(s); ValidateSettlement(s, p, c.TargetId, false); Pay(s, p, new ResourceBag { Wood = 1, Brick = 1, Wool = 1, Wheat = 1 }); PlaceSettlement(s, p, c.TargetId); RecordRegion(s,p,c.TargetId,false); break;
                case CommandKind.BuildCity:
                    ActionPhase(s); BuildCity(s,p,c.TargetId); break;
                case CommandKind.BankTrade:
                    ActionPhase(s); BankTrade(s,p,c); break;
                case CommandKind.MoveRobber:
                    Require(s.Phase == GamePhase.RobberMove && s.PendingDecision != null, "WrongPhase", "当前不需要移动强盗。");
                    var destination = scenario.Topology.Tiles.FirstOrDefault(x => x.Id == c.TargetId); Require(s.FirstBarbarianAttack && destination != null && IsLand(destination) && destination.Id != s.RobberTileId && s.PendingDecision.Token != "pirate-only", "InvalidRobberTile", "强盗必须移动到另一地块。");
                    s.RobberTileId = destination.Id; s.PendingDecision.Token = "robber";
                    s.PendingDecision.EligibleVictimIds = Buildings(s).Where(b => b.PlayerId != p.Id && destination.Vertices.Contains(b.LocationId)).Select(b => b.PlayerId).Distinct().OrderBy(x => x).ToArray();
                    if (s.PendingDecision.EligibleVictimIds.Length == 0) FinishDecision(s); else { s.PendingDecision.Kind = "Steal"; s.Phase = GamePhase.RobberSteal; } break;
                case CommandKind.StealResource:
                    Require(s.Phase == GamePhase.RobberSteal && s.PendingDecision != null && s.PendingDecision.EligibleVictimIds.Contains(c.OtherPlayerId), "InvalidVictim", "请选择强盗旁的另一位玩家。");
                    var victim = s.Players.Single(x => x.Id == c.OtherPlayerId);
                    StealHandCard(s,p,victim);
                    FinishDecision(s); break;
                case CommandKind.EndTurn:
                    ActionPhase(s); Require(p.ProgressCards.Length<=4,"ProgressHandLimit","结束回合前须将进步牌减至四张。"); ClearProgressTurn(s); s.ShipMovedThisTurn = false; s.ActivePlayerId = s.Players[(Array.FindIndex(s.Players, x => x.Id == p.Id) + 1) % s.Players.Length].Id; s.Turn++; s.Phase = GamePhase.ProductionAwaitRoll; break;
                case CommandKind.BuildWall: ActionPhase(s); BuildWall(s,p,c.TargetId); break;
                case CommandKind.ImproveCity: ActionPhase(s); BuildImprovement(s,p,c.Track); break;
                case CommandKind.RecruitKnight: ActionPhase(s); RecruitKnight(s,p,c.TargetId); break;
                case CommandKind.PromoteKnight: ActionPhase(s); PromoteKnight(s,p,c.TargetId); break;
                case CommandKind.ActivateKnight: ActionPhase(s); ActivateKnight(s,p,c.TargetId); break;
                case CommandKind.MoveKnight: case CommandKind.DisplaceKnight: ActionPhase(s); MoveKnight(s,p,c); break;
                case CommandKind.ChaseRobber: ActionPhase(s); ChaseRobber(s,p,c.TargetId); break;
                case CommandKind.DeclareVictory:
                    Require(s.Turn > 0 && VictoryPoints(s, p.Id, true) >= 16, "InsufficientPoints", "尚未达到 16 分。"); break;
                default: throw new RuleException("WrongPhase", "当前没有可处理的此类决定。");
            }
        }

        private void ValidateSettlement(GameState s, PlayerState p, string vertex, bool setup)
        {
            Require(scenario.Topology.Vertices.Any(v => v.Id == vertex), "UnknownVertex", "请选择有效顶点。");
            ValidateSeafaringSettlement(s,p,vertex,setup);
            Require(p.Pieces.Settlements > 0, "NoSettlements", "定居点库存不足。");
            Require(!Occupied(s,vertex), "OccupiedVertex", "这个顶点已有建筑或骑士。");
            Require(!scenario.Topology.Edges.Where(e => e.Vertices.Contains(vertex)).Any(e => Buildings(s).Any(b => e.Vertices.Contains(b.LocationId))), "DistanceRule", "定居点之间必须至少间隔两条边。");
            if (!setup) Require(OwnRouteEdges(s,p.Id).Any(e => e.Vertices.Contains(vertex)), "DisconnectedSettlement", "定居点必须连接自己的道路。");
        }
        private void ValidateRoad(GameState s, PlayerState p, string edge, string anchor)
        {
            var location = scenario.Topology.Edges.FirstOrDefault(e => e.Id == edge); Require(location != null, "UnknownEdge", "请选择有效道路边。");
            Require(p.Pieces.Roads > 0, "NoRoads", "道路库存不足。"); Require(!s.Roads.Any(r => r.LocationId == edge) && !s.Ships.Any(r => r.LocationId == edge), "OccupiedEdge", "这条边已有道路。");
            Require(EdgeTiles(s,edge).Any(IsLand), "RoadAtSea", "道路只能放在陆地边缘。 ");
            if (anchor != null) Require(location.Vertices.Contains(anchor), "RoadNotAtAnchor", "开局道路必须连接刚放置的定居点。");
            else Require(location.Vertices.Any(v => Buildings(s).Any(b => b.LocationId == v && b.PlayerId == p.Id) ||
                (!Blocked(s,p.Id,v) && s.Roads.Any(r => r.PlayerId == p.Id && EdgeAt(r.LocationId).Vertices.Contains(v)))), "DisconnectedRoad", "道路必须连接己方建筑或未被对方建筑与骑士截断的道路。");
        }
        private bool HasLegalRoad(GameState s, PlayerState p)
        {
            foreach (var e in scenario.Topology.Edges) { try { ValidateRoad(s, p, e.Id, null); return true; } catch (RuleException) { } } return false;
        }
        private static void PlaceSettlement(GameState s, PlayerState p, string vertex) { s.Settlements = Append(s.Settlements, Placement(vertex, p.Id)); p.Pieces.Settlements--; }
        private static void PlaceRoad(GameState s, PlayerState p, string edge) { s.Roads = Append(s.Roads, Placement(edge, p.Id)); p.Pieces.Roads--; }
        private static void FinishDecision(GameState s) { s.Phase = s.PendingDecision.ReturnPhase; s.PendingDecision = null; }
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
        private static void ProposeTrade(GameState s, PlayerState p, Command c)
        {
            ActionPhase(s); Require(s.Players.Any(x => x.Id == c.OtherPlayerId) && c.OtherPlayerId != p.Id && (s.ActivePlayerId == p.Id || s.ActivePlayerId == c.OtherPlayerId), "InvalidTradePartner", "交易必须包含当前回合玩家及另一席位。");
            var give=c.Give??new ResourceBag();var receive=c.Receive??new ResourceBag();var gc=c.GiveCommodities??new CommodityBag();var rc=c.ReceiveCommodities??new CommodityBag();
            Require(ValidBag(give)&&ValidBag(receive)&&ValidBag(gc)&&ValidBag(rc)&&give.Total+gc.Total>0&&receive.Total+rc.Total>0&&!Resources.Any(r=>give[r]>0&&receive[r]>0)&&!Commodities.Any(r=>gc[r]>0&&rc[r]>0), "InvalidTrade", "双方均须交换不同种类的手牌。");
            Require(Covers(p.Resources,give)&&Covers(p.Commodities,gc), "InsufficientResources", "没有足够手牌提出此报价。");
            s.TradeOffer = new TradeOffer { ProposerId = p.Id, OtherPlayerId = c.OtherPlayerId, Give = give.Copy(), Receive = receive.Copy(), GiveCommodities=gc.Copy(),ReceiveCommodities=rc.Copy() };
        }
        private static void ResolveTrade(GameState s, PlayerState p, Command c)
        {
            var offer = s.TradeOffer;
            if (c.Kind == CommandKind.CancelTrade) { Require(p.Id == offer.ProposerId, "NotProposer", "仅报价者能取消报价。"); s.TradeOffer = null; return; }
            Require(p.Id == offer.OtherPlayerId, "NotTradeRecipient", "请由报价对象接受或拒绝。");
            if (c.Kind == CommandKind.RejectTrade) { s.TradeOffer = null; return; }
            var proposer = s.Players.Single(x => x.Id == offer.ProposerId);
            Require(Covers(proposer.Resources,offer.Give)&&Covers(p.Resources,offer.Receive)&&Covers(proposer.Commodities,offer.GiveCommodities)&&Covers(p.Commodities,offer.ReceiveCommodities), "InsufficientResources", "双方手牌不足以完成这笔交易。");
            TransferBag(proposer.Resources, p.Resources, offer.Give); TransferBag(p.Resources, proposer.Resources, offer.Receive);TransferBag(proposer.Commodities,p.Commodities,offer.GiveCommodities);TransferBag(p.Commodities,proposer.Commodities,offer.ReceiveCommodities); s.TradeOffer = null;
        }
        public int GetBankTradeRate(string playerId, Resource resource)
        {
            lock (gate) { if (!state.Players.Any(p => p.Id == playerId) || !Enum.IsDefined(typeof(Resource), resource)) throw new ArgumentException("Invalid player/resource."); return TradeRate(state, playerId, resource); }
        }
        private int TradeRate(GameState s, string player, Resource resource)
        {
            var owned = ports.Where(port => Buildings(s).Any(b => b.PlayerId == player && port.Vertices.Contains(b.LocationId))).ToArray();
            int rate=owned.Any(p => p.Resource == resource) ? 2 : owned.Any(p => p.Resource == null) ? 3 : 4;
            return Math.Min(rate,ProgressTradeRate(s,s.Players.Single(p=>p.Id==player),resource,null));
        }
        private int LongestRoad(GameState s, string playerId)
        {
            var edges = OwnRouteEdges(s,playerId).ToArray();
            var ownBuildings = new HashSet<string>(Buildings(s).Where(b=>b.PlayerId==playerId).Select(b=>b.LocationId));
            var ships = new HashSet<string>(s.Ships.Where(x=>x.PlayerId==playerId).Select(x=>x.LocationId));
            var used = new HashSet<string>();
            int Walk(string vertex, string previous)
            {
                if(previous!=null && Blocked(s,playerId,vertex)) return 0;
                int best=0;
                foreach(var edge in edges.Where(e=>e.Vertices.Contains(vertex)&&!used.Contains(e.Id)))
                {
                    if(previous!=null && ships.Contains(previous)!=ships.Contains(edge.Id) && !ownBuildings.Contains(vertex)) continue;
                    used.Add(edge.Id); best=Math.Max(best,1+Walk(edge.Vertices.Single(v=>v!=vertex),edge.Id)); used.Remove(edge.Id);
                }
                return best;
            }
            return edges.Length==0?0:edges.SelectMany(e=>e.Vertices).Distinct().Max(v=>Walk(v,null));
        }
        private void RecalculateAwards(GameState s)
        {
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
            return s.Settlements.Count(b => b.PlayerId == player) + 2 * s.Cities.Count(b => b.PlayerId == player) + (s.LongestRoadPlayerId == player ? 2 : 0)
                + s.Players.Single(p=>p.Id==player).BonusVictoryPoints + 2*s.Metropolises.Count(m=>m.PlayerId==player)+s.Players.Single(p=>p.Id==player).DefenderPoints+s.Players.Single(p=>p.Id==player).ProgressVictoryCards.Length+(s.MerchantPlayerId==player?1:0);
        }
        private static void CheckVictory(GameState s)
        {
            if (s.Turn > 0 && VictoryPoints(s, s.ActivePlayerId, true) >= 16) { s.WinnerPlayerId = s.ActivePlayerId; s.Phase = GamePhase.Finished; s.PendingDecision = null; s.DecisionQueue=Array.Empty<PendingDecision>();s.ProductionPending=false;s.Discards = Array.Empty<DiscardRequirement>(); s.TradeOffer = null; }
        }

        private void AssertInvariants(GameState s)
        {
            void Check(bool valid, string message) { if (!valid) throw new InvalidDataException(message); }
            Check(s.SaveFormatVersion == SaveFormatVersion && s.RulesVersion == RulesVersion && s.ScenarioVersion == ScenarioVersion, "Invalid version identity.");
            Check(Enum.IsDefined(typeof(GamePhase), s.Phase) && s.Turn >= 0 && s.SetupPlacementIndex >= 0 && s.SetupPlacementIndex <= s.Players.Length * 2, "Invalid phase/turn.");
            Check(s.Players.Length == 3 || s.Players.Length == 4, "Invalid player count.");
            Check(s.Players.Select(p => p.Id).Distinct().Count() == s.Players.Length && s.Players.Any(p => p.Id == s.ActivePlayerId), "Invalid player identities.");
            Check(ValidBag(s.Bank) && s.Players.All(p => ValidBag(p.Resources)), "Invalid resource counts.");
            foreach (var r in Resources) Check(s.Bank[r] + s.Players.Sum(p => p.Resources[r]) == 19, "Resource conservation failed: " + r);
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
            ValidateExpansion(s); ValidateProgress(s); ValidateSeafaring(s);
            Check(s.Random.State != 0 && s.Random.Draws >= 0 && s.Random.AlgorithmId == "xorshift32-rejection-v1", "Invalid random state.");
            Check(s.FirstBarbarianAttack ? scenario.Topology.Tiles.Any(t => t.Id == s.RobberTileId && IsLand(t)) : s.RobberTileId==null, "Invalid robber location.");
            Check((s.Phase == GamePhase.SetupRoad || s.Phase == GamePhase.Discard || s.Phase == GamePhase.RobberMove || s.Phase == GamePhase.RobberSteal || s.Phase == GamePhase.RoadBuilding || s.Phase == GamePhase.PendingChoice) == (s.PendingDecision != null), "Invalid pending decision.");
            Check((s.Phase == GamePhase.Discard) == (s.Discards.Length > 0), "Discard phase mismatch.");
            Check(s.Discards.Select(d => d.PlayerId).Distinct().Count() == s.Discards.Length && s.Discards.All(d => d.Amount > 0 && s.Players.Any(p => p.Id == d.PlayerId && HandCount(p) / 2 == d.Amount)), "Invalid discard obligation.");
            if (s.PendingDecision != null)
            {
                Check(s.Phase==GamePhase.PendingChoice?s.Players.Any(p=>p.Id==s.PendingDecision.PlayerId):s.PendingDecision.PlayerId == s.ActivePlayerId, "Pending actor mismatch.");
                if (s.Phase == GamePhase.SetupRoad) Check(s.PendingDecision.Kind == "SetupRoad" && Buildings(s).Any(b => b.PlayerId == s.ActivePlayerId && b.LocationId == s.PendingDecision.AnchorVertexId), "Invalid setup road anchor.");
                else if(s.Phase!=GamePhase.PendingChoice) Check(s.PendingDecision.ReturnPhase == GamePhase.Action || s.PendingDecision.ReturnPhase == GamePhase.ProductionAwaitRoll, "Invalid decision return phase.");
                if (s.Phase == GamePhase.RoadBuilding) Check(s.PendingDecision.RemainingRoads >= 1 && s.PendingDecision.RemainingRoads <= 2, "Invalid free road allowance.");
                if (s.Phase == GamePhase.RobberSteal)
                {
                    var victims = s.PendingDecision.Token == "pirate" ? PirateVictims(s,s.ActivePlayerId,s.PirateTileId) : Buildings(s).Where(b => b.PlayerId != s.ActivePlayerId && s.Tiles.Single(t=>t.Id==s.RobberTileId).Vertices.Contains(b.LocationId)).Select(b => b.PlayerId).Distinct().OrderBy(x => x).ToArray();
                    Check(victims.Length > 0 && victims.SequenceEqual(s.PendingDecision.EligibleVictimIds), "Invalid robber victim selection.");
                }
            }
            Check(s.TradeOffer == null || s.Phase == GamePhase.Action, "Trade outside action phase.");
            if (s.TradeOffer != null)
            {
                var offer = s.TradeOffer;
                Check(offer.ProposerId != offer.OtherPlayerId && s.Players.Any(p => p.Id == offer.ProposerId) && s.Players.Any(p => p.Id == offer.OtherPlayerId) && (offer.ProposerId == s.ActivePlayerId || offer.OtherPlayerId == s.ActivePlayerId), "Invalid trade participants.");
                Check(ValidBag(offer.Give) && ValidBag(offer.Receive) && offer.Give.Total+offer.GiveCommodities.Total > 0 && offer.Receive.Total+offer.ReceiveCommodities.Total > 0 && !Resources.Any(r => offer.Give[r] > 0 && offer.Receive[r] > 0), "Invalid trade bags.");
            }
            var roadLengths = s.Players.Select(p => LongestRoad(s, p.Id)).ToArray();
            Check(ValidAward(s.Players.Select(p => p.Id).ToArray(), roadLengths, 5, s.LongestRoadPlayerId), "Invalid longest road holder.");
            Check(ValidAward(s.Players.Select(p => p.Id).ToArray(), s.Players.Select(p => p.PlayedKnights).ToArray(), 3, s.LargestArmyPlayerId), "Invalid largest army holder.");
            Check((s.Phase == GamePhase.Finished) == (s.WinnerPlayerId != null), "Winner phase mismatch.");
            if (s.WinnerPlayerId != null) Check(s.WinnerPlayerId == s.ActivePlayerId && VictoryPoints(s, s.WinnerPlayerId, true) >= 16, "Invalid winner.");
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
            if (c == null || string.IsNullOrWhiteSpace(c.Id) || c.Id.Length > 128 || string.IsNullOrWhiteSpace(c.PlayerId) || c.PlayerId.Length > 128 || (c.TargetId != null && c.TargetId.Length > 128) || (c.OtherPlayerId != null && c.OtherPlayerId.Length > 128)) return Reject("InvalidCommand", "指令缺少有效标识。");
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
        private static void Pay(GameState s, PlayerState p, ResourceBag cost) { Require(Covers(p.Resources, cost), "InsufficientResources", "建造所需资源不足。"); TransferBag(p.Resources, s.Bank, cost); }
        private static string ResourceName(Resource resource) { return resource.ToString().ToLowerInvariant(); }
        private static Resource ParseResource(string resource) { return (Resource)Enum.Parse(typeof(Resource), resource, true); }
    }
}
