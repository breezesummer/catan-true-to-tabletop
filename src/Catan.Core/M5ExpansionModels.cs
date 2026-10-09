using System;
using System.Linq;
using System.Runtime.Serialization;

namespace Catan.Core.M5
{
    public enum Commodity { Cloth, Coin, Paper }
    public enum ImprovementTrack { Trade, Politics, Science }
    public enum EventDie { Barbarian, Trade, Politics, Science }
    [DataContract] public sealed class CommodityBag
    {
        [DataMember(Order=1)] public int Cloth { get; set; }
        [DataMember(Order=2)] public int Coin { get; set; }
        [DataMember(Order=3)] public int Paper { get; set; }
        public int Total => Cloth + Coin + Paper;
        public int this[Commodity c] { get { switch(c) { case Commodity.Cloth: return Cloth; case Commodity.Coin: return Coin; case Commodity.Paper: return Paper; default: throw new ArgumentOutOfRangeException(nameof(c)); } } set { switch(c) { case Commodity.Cloth: Cloth=value; break; case Commodity.Coin: Coin=value; break; case Commodity.Paper: Paper=value; break; default: throw new ArgumentOutOfRangeException(nameof(c)); } } }
        public CommodityBag Copy() => new CommodityBag { Cloth=Cloth,Coin=Coin,Paper=Paper };
    }
    [DataContract] public sealed class Knight
    {
        [DataMember(Order=1)] public string PlayerId { get; set; }
        [DataMember(Order=2)] public string LocationId { get; set; }
        [DataMember(Order=3)] public int Level { get; set; }
        [DataMember(Order=4)] public bool Active { get; set; }
        [DataMember(Order=5)] public int ActivatedTurn { get; set; }
        [DataMember(Order=6)] public int PromotedTurn { get; set; }
        [DataMember(Order=7)] public int ActedTurn { get; set; }
    }
    [DataContract] public sealed class Metropolis
    {
        [DataMember(Order=1)] public ImprovementTrack Track { get; set; }
        [DataMember(Order=2)] public string PlayerId { get; set; }
        [DataMember(Order=3)] public string LocationId { get; set; }
    }
    public sealed partial class Command
    {
        [DataMember(Order=40)] public string SourceId { get; set; }
        [DataMember(Order=41)] public ImprovementTrack Track { get; set; }
        [DataMember(Order=42)] public Commodity Commodity { get; set; }
        [DataMember(Order=43)] public CommodityBag Commodities { get; set; }
        [DataMember(Order=44)] public CommodityBag GiveCommodities { get; set; }
        [DataMember(Order=45)] public CommodityBag ReceiveCommodities { get; set; }
        [DataMember(Order=46)] public bool GiveIsCommodity { get; set; }
        [DataMember(Order=47)] public bool ReceiveIsCommodity { get; set; }
        [DataMember(Order=48)] public Commodity GiveCommodity { get; set; }
        [DataMember(Order=49)] public Commodity ReceiveCommodity { get; set; }
    }
    public sealed partial class PlayerState
    {
        [DataMember(Order=40)] public CommodityBag Commodities { get; set; } = new CommodityBag();
        [DataMember(Order=41)] public int[] Improvements { get; set; } = new int[3];
        [DataMember(Order=42)] public int DefenderPoints { get; set; }
    }
    public sealed partial class PendingDecision
    {
        [DataMember(Order=40)] public string[] Options { get; set; } = Array.Empty<string>();
        [DataMember(Order=41)] public string OtherPlayerId { get; set; }
        [DataMember(Order=42)] public int Amount { get; set; }
        [DataMember(Order=43)] public ImprovementTrack Track { get; set; }
        [DataMember(Order=44)] public Knight DisplacedKnight { get; set; }
    }
    public sealed partial class TradeOffer
    {
        [DataMember(Order=40)] public CommodityBag GiveCommodities { get; set; } = new CommodityBag();
        [DataMember(Order=41)] public CommodityBag ReceiveCommodities { get; set; } = new CommodityBag();
    }
    public sealed partial class GameState
    {
        [DataMember(Order=40)] public CommodityBag CommodityBank { get; set; } = new CommodityBag { Cloth=12,Coin=12,Paper=12 };
        [DataMember(Order=41)] public Knight[] Knights { get; set; } = Array.Empty<Knight>();
        [DataMember(Order=42)] public PiecePlacement[] Walls { get; set; } = Array.Empty<PiecePlacement>();
        [DataMember(Order=43)] public Metropolis[] Metropolises { get; set; } = Array.Empty<Metropolis>();
        [DataMember(Order=44)] public int BarbarianPosition { get; set; }
        [DataMember(Order=45)] public bool FirstBarbarianAttack { get; set; }
        [DataMember(Order=46)] public EventDie LastEventDie { get; set; }
        [DataMember(Order=47)] public PendingDecision[] DecisionQueue { get; set; } = Array.Empty<PendingDecision>();
        // If no settlement piece exists when pillaged, the city stays physically reserved, lying down.
        [DataMember(Order=48)] public PiecePlacement[] DowngradedCities { get; set; } = Array.Empty<PiecePlacement>();
        [DataMember(Order=49)] public bool ProductionPending { get; set; }
        [DataMember(Order=50)] public GamePhase DecisionReturnPhase { get; set; } = GamePhase.Action;
        [DataMember(Order=51)] public Tile[] Tiles { get; set; } = Array.Empty<Tile>();
    }
    public sealed partial class PublicPlayer
    {
        public int CommodityCount { get; internal set; }
        public int[] Improvements { get; internal set; }
        public int DefenderPoints { get; internal set; }
        public int WallSupply { get; internal set; }
        public int[] KnightSupply { get; internal set; }
    }
    public sealed partial class BoardView
    {
        public Knight[] Knights { get; internal set; }
        public PiecePlacement[] Walls { get; internal set; }
        public Metropolis[] Metropolises { get; internal set; }
    }
    public sealed partial class PlayerView
    {
        public CommodityBag OwnCommodities { get; internal set; }
        public int[] OwnImprovements { get; internal set; }
        public CommodityBag CommodityBank { get; internal set; }
        public int BarbarianPosition { get; internal set; }
        public bool FirstBarbarianAttack { get; internal set; }
        public EventDie LastEventDie { get; internal set; }
        public Command[] LegalActions { get; internal set; }
    }
}
