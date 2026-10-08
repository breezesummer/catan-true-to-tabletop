using System;
using System.Linq;
using System.Runtime.Serialization;

namespace Catan.Core
{
    public enum Resource { Wood, Brick, Wool, Wheat, Ore }
    public enum CommandKind { SetupSettlement, SetupRoad, RollDice, BankTrade, BuildRoad, EndTurn }
    public enum GamePhase { SetupSettlement, SetupRoad, ProductionAwaitRoll, Action }

    [DataContract]
    public sealed class ResourceBag
    {
        [DataMember(Name = "wood", Order = 1)] public int Wood { get; set; }
        [DataMember(Name = "brick", Order = 2)] public int Brick { get; set; }
        [DataMember(Name = "wool", Order = 3)] public int Wool { get; set; }
        [DataMember(Name = "wheat", Order = 4)] public int Wheat { get; set; }
        [DataMember(Name = "ore", Order = 5)] public int Ore { get; set; }
        public int Total { get { return Wood + Brick + Wool + Wheat + Ore; } }
        public int this[Resource resource]
        {
            get { switch (resource) { case Resource.Wood: return Wood; case Resource.Brick: return Brick; case Resource.Wool: return Wool; case Resource.Wheat: return Wheat; case Resource.Ore: return Ore; default: throw new ArgumentOutOfRangeException(nameof(resource)); } }
            set { switch (resource) { case Resource.Wood: Wood = value; break; case Resource.Brick: Brick = value; break; case Resource.Wool: Wool = value; break; case Resource.Wheat: Wheat = value; break; case Resource.Ore: Ore = value; break; default: throw new ArgumentOutOfRangeException(nameof(resource)); } }
        }
        public ResourceBag Copy() { return new ResourceBag { Wood = Wood, Brick = Brick, Wool = Wool, Wheat = Wheat, Ore = Ore }; }
    }

    [DataContract]
    public sealed class PieceSupply
    {
        [DataMember(Name = "roads", Order = 1)] public int Roads { get; set; }
        [DataMember(Name = "settlements", Order = 2)] public int Settlements { get; set; }
        [DataMember(Name = "cities", Order = 3)] public int Cities { get; set; }
        public PieceSupply Copy() { return new PieceSupply { Roads = Roads, Settlements = Settlements, Cities = Cities }; }
    }

    [DataContract]
    public sealed class Command
    {
        [DataMember(Name = "id", Order = 1)] public string Id { get; set; }
        [DataMember(Name = "playerId", Order = 2)] public string PlayerId { get; set; }
        [DataMember(Name = "kind", Order = 3)] public CommandKind Kind { get; set; }
        [DataMember(Name = "targetId", Order = 4)] public string TargetId { get; set; }
        [DataMember(Name = "giveResource", Order = 5)] public Resource GiveResource { get; set; }
        [DataMember(Name = "receiveResource", Order = 6)] public Resource ReceiveResource { get; set; }
        [DataMember(Name = "giveAmount", Order = 7)] public int GiveAmount { get; set; } = 4;
        [DataMember(Name = "receiveAmount", Order = 8)] public int ReceiveAmount { get; set; } = 1;
    }

    [DataContract]
    public sealed class Tile
    {
        [DataMember(Name = "id", Order = 1)] public string Id { get; set; }
        [DataMember(Name = "q", Order = 2)] public int Q { get; set; }
        [DataMember(Name = "r", Order = 3)] public int R { get; set; }
        [DataMember(Name = "resource", Order = 4)] public string Resource { get; set; }
        [DataMember(Name = "number", Order = 5)] public int? Number { get; set; }
        [DataMember(Name = "vertices", Order = 6)] public string[] Vertices { get; set; }
    }

    [DataContract]
    public sealed class Vertex
    {
        [DataMember(Name = "id", Order = 1)] public string Id { get; set; }
        [DataMember(Name = "x", Order = 2)] public int X { get; set; }
        [DataMember(Name = "y", Order = 3)] public int Y { get; set; }
    }

    [DataContract]
    public sealed class Edge
    {
        [DataMember(Name = "id", Order = 1)] public string Id { get; set; }
        [DataMember(Name = "vertices", Order = 2)] public string[] Vertices { get; set; }
    }

    [DataContract]
    public sealed class PiecePlacement
    {
        [DataMember(Name = "locationId", Order = 1)] public string LocationId { get; set; }
        [DataMember(Name = "playerId", Order = 2)] public string PlayerId { get; set; }
    }

    [DataContract]
    public sealed class PendingDecision
    {
        [DataMember(Name = "kind", Order = 1)] public string Kind { get; set; }
        [DataMember(Name = "playerId", Order = 2)] public string PlayerId { get; set; }
        [DataMember(Name = "anchorVertexId", Order = 3)] public string AnchorVertexId { get; set; }
        [DataMember(Name = "placementNumber", Order = 4)] public int PlacementNumber { get; set; }
    }

    [DataContract]
    public sealed class GameEvent
    {
        [DataMember(Name = "sequence", Order = 1)] public long Sequence { get; set; }
        [DataMember(Name = "commandId", Order = 2)] public string CommandId { get; set; }
        [DataMember(Name = "kind", Order = 3)] public string Kind { get; set; }
        [DataMember(Name = "playerId", Order = 4)] public string PlayerId { get; set; }
        [DataMember(Name = "targetId", Order = 5)] public string TargetId { get; set; }
        [DataMember(Name = "dice1", Order = 6)] public int Dice1 { get; set; }
        [DataMember(Name = "dice2", Order = 7)] public int Dice2 { get; set; }
        // Only the publicly observable exchange, never resulting private hands.
        [DataMember(Name = "giveResource", Order = 8)] public string GiveResource { get; set; }
        [DataMember(Name = "receiveResource", Order = 9)] public string ReceiveResource { get; set; }
        [DataMember(Name = "giveAmount", Order = 10)] public int GiveAmount { get; set; }
        [DataMember(Name = "receiveAmount", Order = 11)] public int ReceiveAmount { get; set; }
    }

    public sealed class CommandResult
    {
        public bool Success { get; internal set; }
        public string ErrorCode { get; internal set; }
        public string Message { get; internal set; }
        public bool IsDuplicate { get; internal set; }
        /// <summary>The original confirmed event receipt; retries return this same receipt.</summary>
        public GameEvent[] Events { get; internal set; } = Array.Empty<GameEvent>();
        /// <summary>Publish only these events. Empty on retries and validation previews.</summary>
        public GameEvent[] NewEvents { get; internal set; } = Array.Empty<GameEvent>();
    }

    [DataContract]
    public sealed class PlayerState
    {
        [DataMember(Name = "id", Order = 1)] public string Id { get; set; }
        [DataMember(Name = "resources", Order = 2)] public ResourceBag Resources { get; set; }
        [DataMember(Name = "pieces", Order = 3)] public PieceSupply Pieces { get; set; }
    }

    [DataContract]
    public sealed class RandomState
    {
        [DataMember(Name = "algorithmId", Order = 1)] public string AlgorithmId { get; set; }
        [DataMember(Name = "dice", Order = 2)] public int[][] Dice { get; set; }
        [DataMember(Name = "cursor", Order = 3)] public int Cursor { get; set; }
    }

    [DataContract]
    public sealed class ProcessedCommand
    {
        [DataMember(Name = "commandId", Order = 1)] public string CommandId { get; set; }
        [DataMember(Name = "fingerprint", Order = 2)] public string Fingerprint { get; set; }
        [DataMember(Name = "command", Order = 3)] public Command Command { get; set; }
        [DataMember(Name = "events", Order = 4)] public GameEvent[] Events { get; set; }
    }

    /// <summary>Authority-only snapshot. Never send to a player, renderer or AI.</summary>
    [DataContract]
    public sealed class GameState
    {
        [DataMember(Name = "saveFormatVersion", Order = 1)] public int SaveFormatVersion { get; set; }
        [DataMember(Name = "rulesVersion", Order = 2)] public string RulesVersion { get; set; }
        [DataMember(Name = "rulesBaselineId", Order = 3)] public string RulesBaselineId { get; set; }
        [DataMember(Name = "scenarioId", Order = 4)] public string ScenarioId { get; set; }
        [DataMember(Name = "scenarioVersion", Order = 5)] public string ScenarioVersion { get; set; }
        [DataMember(Name = "scenarioContentHash", Order = 6)] public string ScenarioContentHash { get; set; }
        [DataMember(Name = "activePlayerId", Order = 7)] public string ActivePlayerId { get; set; }
        [DataMember(Name = "turn", Order = 8)] public int Turn { get; set; }
        [DataMember(Name = "phase", Order = 9)] public GamePhase Phase { get; set; }
        [DataMember(Name = "pendingDecision", Order = 10)] public PendingDecision PendingDecision { get; set; }
        [DataMember(Name = "players", Order = 11)] public PlayerState[] Players { get; set; }
        [DataMember(Name = "bank", Order = 12)] public ResourceBag Bank { get; set; }
        [DataMember(Name = "settlements", Order = 13)] public PiecePlacement[] Settlements { get; set; }
        [DataMember(Name = "roads", Order = 14)] public PiecePlacement[] Roads { get; set; }
        [DataMember(Name = "robberTileId", Order = 15)] public string RobberTileId { get; set; }
        [DataMember(Name = "random", Order = 16)] public RandomState Random { get; set; }
        [DataMember(Name = "setupPlacementIndex", Order = 17)] public int SetupPlacementIndex { get; set; }
        [DataMember(Name = "secondSettlementVertices", Order = 18)] public PiecePlacement[] SecondSettlementVertices { get; set; }
        [DataMember(Name = "processedCommands", Order = 19)] public ProcessedCommand[] ProcessedCommands { get; set; }
        [DataMember(Name = "events", Order = 20)] public GameEvent[] Events { get; set; }
        [DataMember(Name = "eventSequence", Order = 21)] public long EventSequence { get; set; }
    }

    /// <summary>This public-player type deliberately has no resource-by-type hand field.</summary>
    public sealed class PublicPlayer
    {
        public string Id { get; internal set; }
        public int ResourceCount { get; internal set; }
        public PieceSupply Pieces { get; internal set; }
        public int VictoryPoints { get; internal set; }
        public string PlayerId { get { return Id; } }
        public PieceSupply RemainingPieces { get { return Pieces; } }
    }

    public sealed class BoardView
    {
        public Tile[] Tiles { get; internal set; }
        public Vertex[] Vertices { get; internal set; }
        public Edge[] Edges { get; internal set; }
        public PiecePlacement[] Settlements { get; internal set; }
        public PiecePlacement[] Roads { get; internal set; }
        public string RobberTileId { get; internal set; }
    }

    public sealed class PlayerView
    {
        public string PlayerId { get; internal set; }
        public string ActivePlayerId { get; internal set; }
        public int Turn { get; internal set; }
        public GamePhase Phase { get; internal set; }
        public PendingDecision PendingDecision { get; internal set; }
        public ResourceBag OwnResources { get; internal set; }
        public ResourceBag Bank { get; internal set; }
        public PublicPlayer[] Players { get; internal set; }
        public BoardView Board { get; internal set; }
        public string[] LegalVertexIds { get; internal set; }
        public string[] LegalEdgeIds { get; internal set; }
        public GameEvent[] Events { get; internal set; }
        public string[] UnsupportedFeatures { get; internal set; }
    }
}
