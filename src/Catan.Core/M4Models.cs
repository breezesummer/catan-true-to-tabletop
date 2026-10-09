using System;
using System.Linq;
using System.Runtime.Serialization;

namespace Catan.Core.M4
{
    public enum CommandKind { SetupSettlement, SetupRoad, RollDice, BuildRoad, BuildSettlement, BuildCity, BankTrade, ProposeTrade, AcceptTrade, RejectTrade, CancelTrade, BuyDevelopmentCard, PlayKnight, PlayRoadBuilding, PlaceFreeRoad, FinishRoadBuilding, PlayYearOfPlenty, PlayMonopoly, DiscardResources, MoveRobber, StealResource, EndTurn, DeclareVictory, BuildWall, ImproveCity, RecruitKnight, PromoteKnight, ActivateKnight, MoveKnight, DisplaceKnight, ChaseRobber, ResolveChoice, PlayProgressCard, ResolveProgressChoice, DiscardProgressCard, CommercialHarborOffer }
    public enum GamePhase { SetupSettlement, SetupRoad, ProductionAwaitRoll, Action, Discard, RobberMove, RobberSteal, RoadBuilding, Finished, PendingChoice }
    public enum DevelopmentCardKind { Knight, RoadBuilding, YearOfPlenty, Monopoly, VictoryPoint }

    [DataContract] public sealed partial class Command
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
    }
    [DataContract] public sealed partial class DevelopmentCard
    {
        [DataMember(Order=1)] public DevelopmentCardKind Kind { get; set; }
        [DataMember(Order=2)] public int BoughtTurn { get; set; }
    }
    [DataContract] public sealed partial class PlayerState
    {
        [DataMember(Order=1)] public string Id { get; set; }
        [DataMember(Order=2)] public ResourceBag Resources { get; set; }
        [DataMember(Order=3)] public PieceSupply Pieces { get; set; }
        [DataMember(Order=4)] public DevelopmentCard[] DevelopmentCards { get; set; } = Array.Empty<DevelopmentCard>();
        [DataMember(Order=5)] public int PlayedKnights { get; set; }
    }
    [DataContract] public sealed partial class Port
    {
        [DataMember(Order=1)] public string Id { get; set; }
        [DataMember(Order=2)] public string[] Vertices { get; set; }
        // null = generic 3:1; otherwise a 2:1 resource port.
        [DataMember(Order=3)] public Resource? Resource { get; set; }
    }
    [DataContract] public sealed partial class PendingDecision
    {
        [DataMember(Order=1)] public string Kind { get; set; }
        [DataMember(Order=2)] public string PlayerId { get; set; }
        [DataMember(Order=3)] public string AnchorVertexId { get; set; }
        [DataMember(Order=4)] public int RemainingRoads { get; set; }
        [DataMember(Order=5)] public string[] EligibleVictimIds { get; set; } = Array.Empty<string>();
        [DataMember(Order=6)] public GamePhase ReturnPhase { get; set; }
    }
    [DataContract] public sealed partial class DiscardRequirement
    {
        [DataMember(Order=1)] public string PlayerId { get; set; }
        [DataMember(Order=2)] public int Amount { get; set; }
    }
    [DataContract] public sealed partial class TradeOffer
    {
        [DataMember(Order=1)] public string ProposerId { get; set; }
        [DataMember(Order=2)] public string OtherPlayerId { get; set; }
        [DataMember(Order=3)] public ResourceBag Give { get; set; }
        [DataMember(Order=4)] public ResourceBag Receive { get; set; }
    }
    [DataContract] public sealed partial class GameEvent
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
    [DataContract] public sealed partial class ProcessedCommand
    {
        [DataMember(Order=1)] public string CommandId { get; set; }
        [DataMember(Order=2)] public string Fingerprint { get; set; }
        [DataMember(Order=3)] public Command Command { get; set; }
        [DataMember(Order=4)] public GameEvent[] Events { get; set; }
    }
    [DataContract] public sealed partial class RandomState
    {
        [DataMember(Order=1)] public string AlgorithmId { get; set; } = "xorshift32-rejection-v1";
        [DataMember(Order=2)] public uint State { get; set; }
        [DataMember(Order=3)] public long Draws { get; set; }
    }
    /// <summary>Authority only. Never send this type to a UI, AI, public logger or network player.</summary>
    [DataContract] public sealed partial class GameState
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
        internal GameState CopyForTransaction()
        {
            var lightweight = (GameState)MemberwiseClone();
            lightweight.ProcessedCommands = Array.Empty<ProcessedCommand>(); lightweight.Events = Array.Empty<GameEvent>();
            var copy = Json.Copy(lightweight);
            copy.ProcessedCommands = ProcessedCommands; copy.Events = Events;
            return copy;
        }
    }
    public sealed partial class CommandResult
    {
        public bool Success { get; internal set; }
        public string ErrorCode { get; internal set; }
        public string Message { get; internal set; }
        public bool IsDuplicate { get; internal set; }
        public GameEvent[] Events { get; internal set; } = Array.Empty<GameEvent>();
        public GameEvent[] NewEvents { get; internal set; } = Array.Empty<GameEvent>();
    }
    public sealed partial class PublicPlayer
    {
        public string Id { get; internal set; }
        public int ResourceCount { get; internal set; }
        public int DevelopmentCardCount { get; internal set; }
        public int PlayedKnights { get; internal set; }
        public int VictoryPoints { get; internal set; }
        public int LongestRoadLength { get; internal set; }
        public PieceSupply Pieces { get; internal set; }
    }
    public sealed partial class BoardView
    {
        public Tile[] Tiles { get; internal set; }
        public Vertex[] Vertices { get; internal set; }
        public Edge[] Edges { get; internal set; }
        public Port[] Ports { get; internal set; }
        public PiecePlacement[] Settlements { get; internal set; }
        public PiecePlacement[] Cities { get; internal set; }
        public PiecePlacement[] Roads { get; internal set; }
        public string RobberTileId { get; internal set; }
    }
    public sealed partial class PlayerView
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
    }
}
