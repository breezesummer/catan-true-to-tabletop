using System;
using System.Linq;
using System.Runtime.Serialization;

namespace Catan.Core.M3
{
    public enum CommandKind { SetupSettlement, SetupRoad, RollDice, BuildRoad, BuildSettlement, BuildCity, BankTrade, ProposeTrade, AcceptTrade, RejectTrade, CancelTrade, BuyDevelopmentCard, PlayKnight, PlayRoadBuilding, PlaceFreeRoad, FinishRoadBuilding, PlayYearOfPlenty, PlayMonopoly, DiscardResources, MoveRobber, StealResource, EndTurn, DeclareVictory, SetupShip, BuildShip, PlaceFreeShip, MoveShip, MovePirate, ChooseGoldResources, StealCloth, PlacePort, ClaimWonder, BuildWonder, AttackFortress, SetupPort, ChooseInvasionRoute }
    public enum GamePhase { SetupSettlement, SetupRoad, ProductionAwaitRoll, Action, Discard, RobberMove, RobberSteal, RoadBuilding, Finished, GoldChoice, PortPlacement, SetupPort }
    public enum DevelopmentCardKind { Knight, RoadBuilding, YearOfPlenty, Monopoly, VictoryPoint }

    [DataContract] public sealed class Command
    {
        [DataMember(Order=1)] public string Id { get; set; }
        [DataMember(Order=2)] public string PlayerId { get; set; }
        [DataMember(Order=3)] public CommandKind Kind { get; set; }
        [DataMember(Order=4)] public string TargetId { get; set; }
        [DataMember(Order=5)] public string OtherPlayerId { get; set; }
        [DataMember(Order=6)] public Resource GiveResource { get; set; }
        [DataMember(Order=7)] public Resource ReceiveResource { get; set; }
        [DataMember(Order=8)] public int GiveAmount { get; set; } = 4;
        [DataMember(Order=9)] public int ReceiveAmount { get; set; } = 1;
        // Give/Receive are public proposed exchanges. Resources is a private discard or Plenty selection.
        [DataMember(Order=10)] public ResourceBag Give { get; set; }
        [DataMember(Order=11)] public ResourceBag Receive { get; set; }
        [DataMember(Order=12)] public ResourceBag Resources { get; set; }
        [DataMember(Order=13)] public Resource Resource { get; set; }
        [DataMember(Order=14)] public string SourceId { get; set; }
    }
    [DataContract] public sealed class DevelopmentCard
    {
        [DataMember(Order=1)] public DevelopmentCardKind Kind { get; set; }
        [DataMember(Order=2)] public int BoughtTurn { get; set; }
    }
    [DataContract] public sealed class PlayerState
    {
        [DataMember(Order=1)] public string Id { get; set; }
        [DataMember(Order=2)] public ResourceBag Resources { get; set; }
        [DataMember(Order=3)] public PieceSupply Pieces { get; set; }
        [DataMember(Order=4)] public DevelopmentCard[] DevelopmentCards { get; set; } = Array.Empty<DevelopmentCard>();
        [DataMember(Order=5)] public int PlayedKnights { get; set; }
        [DataMember(Order=6)] public int ShipsRemaining { get; set; } = 15;
        [DataMember(Order=7)] public int BonusVictoryPoints { get; set; }
        [DataMember(Order=8)] public string[] HomeRegions { get; set; } = Array.Empty<string>();
        [DataMember(Order=9)] public string[] SettledRegions { get; set; } = Array.Empty<string>();
        [DataMember(Order=10)] public int Cloth { get; set; }
        [DataMember(Order=11)] public string WonderId { get; set; }
        [DataMember(Order=12)] public int WonderLevel { get; set; }
        [DataMember(Order=13)] public Port[] HeldPorts { get; set; } = Array.Empty<Port>();
    }
    [DataContract] public sealed class Port
    {
        [DataMember(Order=1)] public string Id { get; set; }
        [DataMember(Order=2)] public string[] Vertices { get; set; }
        // null = generic 3:1; otherwise a 2:1 resource port.
        [DataMember(Order=3)] public Resource? Resource { get; set; }
        [DataMember(Order=4)] public string EdgeId { get; set; }
    }
    [DataContract] public sealed class PendingDecision
    {
        [DataMember(Order=1)] public string Kind { get; set; }
        [DataMember(Order=2)] public string PlayerId { get; set; }
        [DataMember(Order=3)] public string AnchorVertexId { get; set; }
        [DataMember(Order=4)] public int RemainingRoads { get; set; }
        [DataMember(Order=5)] public string[] EligibleVictimIds { get; set; } = Array.Empty<string>();
        [DataMember(Order=6)] public GamePhase ReturnPhase { get; set; }
        [DataMember(Order=7)] public string Token { get; set; }
    }
    [DataContract] public sealed class DiscardRequirement
    {
        [DataMember(Order=1)] public string PlayerId { get; set; }
        [DataMember(Order=2)] public int Amount { get; set; }
    }
    [DataContract] public sealed class TradeOffer
    {
        [DataMember(Order=1)] public string ProposerId { get; set; }
        [DataMember(Order=2)] public string OtherPlayerId { get; set; }
        [DataMember(Order=3)] public ResourceBag Give { get; set; }
        [DataMember(Order=4)] public ResourceBag Receive { get; set; }
    }
    [DataContract] public sealed class GameEvent
    {
        [DataMember(Order=1)] public long Sequence { get; set; }
        [DataMember(Order=2)] public string CommandId { get; set; }
        [DataMember(Order=3)] public string Kind { get; set; }
        [DataMember(Order=4)] public string PlayerId { get; set; }
        [DataMember(Order=5)] public string TargetId { get; set; }
        [DataMember(Order=6)] public string OtherPlayerId { get; set; }
        [DataMember(Order=7)] public int Dice1 { get; set; }
        [DataMember(Order=8)] public int Dice2 { get; set; }
        [DataMember(Order=9)] public string Message { get; set; }
    }
    [DataContract] public sealed class ProcessedCommand
    {
        [DataMember(Order=1)] public string CommandId { get; set; }
        [DataMember(Order=2)] public string Fingerprint { get; set; }
        [DataMember(Order=3)] public Command Command { get; set; }
        [DataMember(Order=4)] public GameEvent[] Events { get; set; }
    }
    [DataContract] public sealed class RandomState
    {
        [DataMember(Order=1)] public string AlgorithmId { get; set; } = "xorshift32-rejection-v1";
        [DataMember(Order=2)] public uint State { get; set; }
        [DataMember(Order=3)] public long Draws { get; set; }
    }
    /// <summary>Authority only. Never send this type to a UI, AI, public logger or network player.</summary>
    [DataContract] public sealed class GameState
    {
        [DataMember(Order=1)] public int SaveFormatVersion { get; set; }
        [DataMember(Order=2)] public string RulesVersion { get; set; }
        [DataMember(Order=3)] public string ScenarioId { get; set; }
        [DataMember(Order=4)] public string ScenarioVersion { get; set; }
        [DataMember(Order=5)] public string ScenarioContentHash { get; set; }
        [DataMember(Order=6)] public uint InitialSeed { get; set; }
        [DataMember(Order=7)] public RandomState Random { get; set; }
        [DataMember(Order=8)] public string ActivePlayerId { get; set; }
        [DataMember(Order=9)] public int Turn { get; set; }
        [DataMember(Order=10)] public GamePhase Phase { get; set; }
        [DataMember(Order=11)] public PlayerState[] Players { get; set; }
        [DataMember(Order=12)] public ResourceBag Bank { get; set; }
        [DataMember(Order=13)] public PiecePlacement[] Settlements { get; set; } = Array.Empty<PiecePlacement>();
        [DataMember(Order=14)] public PiecePlacement[] Cities { get; set; } = Array.Empty<PiecePlacement>();
        [DataMember(Order=15)] public PiecePlacement[] Roads { get; set; } = Array.Empty<PiecePlacement>();
        [DataMember(Order=16)] public string RobberTileId { get; set; }
        [DataMember(Order=17)] public PendingDecision PendingDecision { get; set; }
        [DataMember(Order=18)] public DiscardRequirement[] Discards { get; set; } = Array.Empty<DiscardRequirement>();
        [DataMember(Order=19)] public TradeOffer TradeOffer { get; set; }
        [DataMember(Order=20)] public DevelopmentCardKind[] DevelopmentDeck { get; set; }
        [DataMember(Order=21)] public DevelopmentCardKind[] PlayedDevelopmentCards { get; set; } = Array.Empty<DevelopmentCardKind>();
        [DataMember(Order=22)] public bool DevelopmentCardPlayedThisTurn { get; set; }
        [DataMember(Order=23)] public int SetupPlacementIndex { get; set; }
        [DataMember(Order=24)] public int StartingPlayerIndex { get; set; }
        [DataMember(Order=25)] public string LongestRoadPlayerId { get; set; }
        [DataMember(Order=26)] public string LargestArmyPlayerId { get; set; }
        [DataMember(Order=27)] public string WinnerPlayerId { get; set; }
        [DataMember(Order=28)] public int LastDice1 { get; set; }
        [DataMember(Order=29)] public int LastDice2 { get; set; }
        [DataMember(Order=30)] public ProcessedCommand[] ProcessedCommands { get; set; } = Array.Empty<ProcessedCommand>();
        [DataMember(Order=31)] public GameEvent[] Events { get; set; } = Array.Empty<GameEvent>();
        [DataMember(Order=32)] public long EventSequence { get; set; }
        [DataMember(Order=33)] public Tile[] Tiles { get; set; }
        [DataMember(Order=34)] public Port[] Ports { get; set; }
        [DataMember(Order=35)] public ShipPlacement[] Ships { get; set; } = Array.Empty<ShipPlacement>();
        [DataMember(Order=36)] public string PirateTileId { get; set; }
        [DataMember(Order=37)] public bool ShipMovedThisTurn { get; set; }
        [DataMember(Order=38)] public string[] HiddenResources { get; set; } = Array.Empty<string>();
        [DataMember(Order=39)] public int[] HiddenNumbers { get; set; } = Array.Empty<int>();
        [DataMember(Order=40)] public GoldClaim[] GoldClaims { get; set; } = Array.Empty<GoldClaim>();
        [DataMember(Order=41)] public GamePhase GoldReturnPhase { get; set; }
        [DataMember(Order=42)] public PendingDecision GoldReturnDecision { get; set; }
        [DataMember(Order=43)] public string[] BonusEdgeIds { get; set; } = Array.Empty<string>();
        [DataMember(Order=44)] public GiftCard[] GiftCards { get; set; } = Array.Empty<GiftCard>();
        [DataMember(Order=45)] public Port[] GiftPorts { get; set; } = Array.Empty<Port>();
        [DataMember(Order=46)] public VillageState[] Villages { get; set; } = Array.Empty<VillageState>();
        [DataMember(Order=47)] public int ClothSupply { get; set; }
        [DataMember(Order=48)] public FortressState[] Fortresses { get; set; } = Array.Empty<FortressState>();
        [DataMember(Order=49)] public int LastPirateStrength { get; set; }
        [DataMember(Order=50)] public string[] WinnerPlayerIds { get; set; } = Array.Empty<string>();
        [DataMember(Order=51)] public bool ProductionAfterGold { get; set; }
        [DataMember(Order=52)] public Port[] UnplacedPorts { get; set; } = Array.Empty<Port>();
        [DataMember(Order=53)] public int SetupPortIndex { get; set; }
        [DataMember(Order=54)] public PendingDecision PortReturnDecision { get; set; }
        internal GameState CopyForTransaction()
        {
            var copy = (GameState)MemberwiseClone();
            copy.Random = new RandomState { AlgorithmId = Random.AlgorithmId, State = Random.State, Draws = Random.Draws };
            copy.Players = Players.Select(p => new PlayerState { Id = p.Id, Resources = p.Resources.Copy(), Pieces = p.Pieces.Copy(), DevelopmentCards = p.DevelopmentCards.ToArray(), PlayedKnights = p.PlayedKnights, ShipsRemaining = p.ShipsRemaining, BonusVictoryPoints = p.BonusVictoryPoints, HomeRegions = p.HomeRegions.ToArray(), SettledRegions = p.SettledRegions.ToArray(), Cloth = p.Cloth, WonderId = p.WonderId, WonderLevel = p.WonderLevel, HeldPorts = p.HeldPorts.ToArray() }).ToArray();
            copy.Bank = Bank.Copy();
            copy.Settlements = Settlements.ToArray(); copy.Cities = Cities.ToArray(); copy.Roads = Roads.ToArray();
            copy.PendingDecision = CopyDecision(PendingDecision);
            copy.Discards = Discards.ToArray(); copy.TradeOffer = TradeOffer;
            copy.DevelopmentDeck = DevelopmentDeck.ToArray(); copy.PlayedDevelopmentCards = PlayedDevelopmentCards.ToArray();
            // Existing receipts/placements are immutable within the engine. External reads are detached copies.
            copy.ProcessedCommands = ProcessedCommands; copy.Events = Events;
            copy.Tiles = Tiles.Select(t => new Tile { Id = t.Id, Q = t.Q, R = t.R, Resource = t.Resource, Number = t.Number, Vertices = t.Vertices }).ToArray(); copy.Ports = Ports.ToArray();
            copy.Ships = Ships.Select(s => new ShipPlacement { LocationId = s.LocationId, PlayerId = s.PlayerId, BuiltTurn = s.BuiltTurn, IsWarship = s.IsWarship, IsInvasionRoute = s.IsInvasionRoute }).ToArray();
            copy.HiddenResources = HiddenResources.ToArray(); copy.HiddenNumbers = HiddenNumbers.ToArray();
            copy.GoldClaims = GoldClaims.Select(g => new GoldClaim { PlayerId = g.PlayerId, Amount = g.Amount }).ToArray(); copy.GoldReturnDecision = CopyDecision(GoldReturnDecision);
            copy.BonusEdgeIds = BonusEdgeIds.ToArray(); copy.GiftCards = GiftCards.ToArray(); copy.GiftPorts = GiftPorts.ToArray();
            copy.Villages = Villages.Select(v => new VillageState { VertexId = v.VertexId, Number = v.Number, Cloth = v.Cloth, TradingPlayerIds = v.TradingPlayerIds }).ToArray();
            copy.Fortresses = Fortresses.Select(f => new FortressState { PlayerId = f.PlayerId, VertexId = f.VertexId, Strength = f.Strength, BeachheadVertexId = f.BeachheadVertexId, StartingVertexId = f.StartingVertexId, InvasionStartingVertexId = f.InvasionStartingVertexId }).ToArray(); copy.WinnerPlayerIds = WinnerPlayerIds.ToArray();
            copy.UnplacedPorts = UnplacedPorts.ToArray();
            copy.PortReturnDecision = CopyDecision(PortReturnDecision);
            return copy;
        }
        private static PendingDecision CopyDecision(PendingDecision p)
        {
            return p == null ? null : new PendingDecision { Kind = p.Kind, PlayerId = p.PlayerId, AnchorVertexId = p.AnchorVertexId, RemainingRoads = p.RemainingRoads, EligibleVictimIds = p.EligibleVictimIds, ReturnPhase = p.ReturnPhase, Token = p.Token };
        }
    }
    public sealed class CommandResult
    {
        public bool Success { get; internal set; }
        public string ErrorCode { get; internal set; }
        public string Message { get; internal set; }
        public bool IsDuplicate { get; internal set; }
        public GameEvent[] Events { get; internal set; } = Array.Empty<GameEvent>();
        public GameEvent[] NewEvents { get; internal set; } = Array.Empty<GameEvent>();
    }
    public sealed class PublicPlayer
    {
        public string Id { get; internal set; }
        public int ResourceCount { get; internal set; }
        public int DevelopmentCardCount { get; internal set; }
        public int PlayedKnights { get; internal set; }
        public int VictoryPoints { get; internal set; }
        public int LongestRoadLength { get; internal set; }
        public PieceSupply Pieces { get; internal set; }
        public int ShipsRemaining { get; internal set; }
        public int Cloth { get; internal set; }
        public int BonusVictoryPoints { get; internal set; }
        public string WonderId { get; internal set; }
        public int WonderLevel { get; internal set; }
    }
    public sealed class BoardView
    {
        public Tile[] Tiles { get; internal set; }
        public Vertex[] Vertices { get; internal set; }
        public Edge[] Edges { get; internal set; }
        public Port[] Ports { get; internal set; }
        public PiecePlacement[] Settlements { get; internal set; }
        public PiecePlacement[] Cities { get; internal set; }
        public PiecePlacement[] Roads { get; internal set; }
        public string RobberTileId { get; internal set; }
        public ShipPlacement[] Ships { get; internal set; }
        public string PirateTileId { get; internal set; }
        public string[] BonusEdgeIds { get; internal set; }
        public string[] GiftCardEdgeIds { get; internal set; }
        public Port[] GiftPorts { get; internal set; }
        public VillageState[] Villages { get; internal set; }
        public FortressState[] Fortresses { get; internal set; }
    }
    public sealed class PlayerView
    {
        public string PlayerId { get; internal set; }
        public string ActivePlayerId { get; internal set; }
        public int Turn { get; internal set; }
        public GamePhase Phase { get; internal set; }
        public PendingDecision PendingDecision { get; internal set; }
        public DiscardRequirement[] Discards { get; internal set; }
        public TradeOffer TradeOffer { get; internal set; }
        public ResourceBag OwnResources { get; internal set; }
        public DevelopmentCard[] OwnDevelopmentCards { get; internal set; }
        public int OwnVictoryPoints { get; internal set; }
        public bool DevelopmentCardPlayedThisTurn { get; internal set; }
        public int DevelopmentDeckCount { get; internal set; }
        public ResourceBag Bank { get; internal set; }
        public PublicPlayer[] Players { get; internal set; }
        public BoardView Board { get; internal set; }
        public string[] LegalVertexIds { get; internal set; }
        public string[] LegalCityVertexIds { get; internal set; }
        public string[] LegalEdgeIds { get; internal set; }
        public string LongestRoadPlayerId { get; internal set; }
        public string LargestArmyPlayerId { get; internal set; }
        public string WinnerPlayerId { get; internal set; }
        public int LastDice1 { get; internal set; }
        public int LastDice2 { get; internal set; }
        public GameEvent[] Events { get; internal set; }
        public string ScenarioId { get; internal set; }
        public string ScenarioName { get; internal set; }
        public int TargetVictoryPoints { get; internal set; }
        public string[] LegalShipEdgeIds { get; internal set; }
        public string[] MovableShipEdgeIds { get; internal set; }
        public GoldClaim[] GoldClaims { get; internal set; }
        public Port[] OwnHeldPorts { get; internal set; }
        public int ClothSupply { get; internal set; }
        public int LastPirateStrength { get; internal set; }
        public string[] WinnerPlayerIds { get; internal set; }
        public bool ShipMovedThisTurn { get; internal set; }
        public string[] LegalPortEdgeIds { get; internal set; }
        public WonderDefinition[] Wonders { get; internal set; }
        public Port NextSetupPort { get; internal set; }
        public string[] LegalInvasionRootVertexIds { get; internal set; }
    }

    [DataContract] public sealed class ShipPlacement
    {
        [DataMember(Order=1)] public string LocationId { get; set; }
        [DataMember(Order=2)] public string PlayerId { get; set; }
        [DataMember(Order=3)] public int BuiltTurn { get; set; }
        [DataMember(Order=4)] public bool IsWarship { get; set; }
        [DataMember(Order=5)] public bool IsInvasionRoute { get; set; }
    }
    [DataContract] public sealed class GoldClaim
    {
        [DataMember(Order=1)] public string PlayerId { get; set; }
        [DataMember(Order=2)] public int Amount { get; set; }
    }
    [DataContract] public sealed class GiftCard
    {
        [DataMember(Order=1)] public string EdgeId { get; set; }
        [DataMember(Order=2)] public DevelopmentCardKind Kind { get; set; }
    }
    [DataContract] public sealed class VillageState
    {
        [DataMember(Order=1)] public string VertexId { get; set; }
        [DataMember(Order=2)] public int Number { get; set; }
        [DataMember(Order=3)] public int Cloth { get; set; } = 5;
        [DataMember(Order=4)] public string[] TradingPlayerIds { get; set; } = Array.Empty<string>();
    }
    [DataContract] public sealed class FortressState
    {
        [DataMember(Order=1)] public string PlayerId { get; set; }
        [DataMember(Order=2)] public string VertexId { get; set; }
        [DataMember(Order=3)] public int Strength { get; set; } = 3;
        [DataMember(Order=4)] public string BeachheadVertexId { get; set; }
        [DataMember(Order=5)] public string StartingVertexId { get; set; }
        [DataMember(Order=6)] public string InvasionStartingVertexId { get; set; }
    }
}
