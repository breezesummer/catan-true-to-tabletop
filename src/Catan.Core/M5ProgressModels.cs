using System;
using System.Linq;
using System.Runtime.Serialization;

namespace Catan.Core.M5
{
    // Official 2025 English names, 25 distinct effects and 54 physical cards.
    public enum ProgressCardKind
    {
        Alchemy, Crane, Engineering, Invention, Irrigation, Medicine, Mining, Printing, RoadBuilding, Smithing,
        CommercialHarbor, GuildDues, Merchant, MerchantFleet, ResourceMonopoly, TradeMonopoly,
        Diplomacy, Espionage, Encouragement, Intrigue, Taxation, Constitution, Treason, Wedding, Sabotage
    }

    public static class ProgressCatalog
    {
        public static ImprovementTrack Track(ProgressCardKind card)
        {
            if (!Enum.IsDefined(typeof(ProgressCardKind), card)) throw new ArgumentOutOfRangeException(nameof(card));
            return card <= ProgressCardKind.Smithing ? ImprovementTrack.Science : card <= ProgressCardKind.TradeMonopoly ? ImprovementTrack.Trade : ImprovementTrack.Politics;
        }
        public static bool IsVictoryPoint(ProgressCardKind card) { return card == ProgressCardKind.Printing || card == ProgressCardKind.Constitution; }
        public static int Count(ProgressCardKind card)
        {
            if (card == ProgressCardKind.Engineering || IsVictoryPoint(card)) return 1;
            if (card == ProgressCardKind.Merchant) return 6;
            if (card == ProgressCardKind.ResourceMonopoly) return 4;
            if (card == ProgressCardKind.Espionage) return 3;
            return 2;
        }
        public static ProgressCardKind[] Deck(ImprovementTrack track)
        {
            return ((ProgressCardKind[])Enum.GetValues(typeof(ProgressCardKind))).Where(c => Track(c) == track).SelectMany(c => Enumerable.Repeat(c, Count(c))).ToArray();
        }
    }

    [DataContract] public sealed class ProgressDeck
    {
        [DataMember(Order=1)] public ImprovementTrack Track { get; set; }
        [DataMember(Order=2)] public ProgressCardKind[] Cards { get; set; } = Array.Empty<ProgressCardKind>();
    }
    public sealed partial class Command
    {
        [DataMember(Order=100)] public ProgressCardKind ProgressCard { get; set; }
        [DataMember(Order=101)] public ProgressCardKind SelectedProgressCard { get; set; }
        [DataMember(Order=102)] public string[] TargetIds { get; set; } = Array.Empty<string>();
        [DataMember(Order=103)] public int ChosenDice1 { get; set; }
        [DataMember(Order=104)] public int ChosenDice2 { get; set; }
        [DataMember(Order=105)] public bool ChooseCommodity { get; set; }
        [DataMember(Order=106)] public bool Decline { get; set; }
        [DataMember(Order=107)] public int KnightLevel { get; set; } = 1;
    }
    public sealed partial class PlayerState
    {
        [DataMember(Order=100)] public ProgressCardKind[] ProgressCards { get; set; } = Array.Empty<ProgressCardKind>();
        [DataMember(Order=101)] public ProgressCardKind[] ProgressVictoryCards { get; set; } = Array.Empty<ProgressCardKind>();
    }
    public sealed partial class GameState
    {
        [DataMember(Order=100)] public ProgressDeck[] ProgressDecks { get; set; } = Array.Empty<ProgressDeck>();
        [DataMember(Order=101)] public string MerchantPlayerId { get; set; }
        [DataMember(Order=102)] public string MerchantTileId { get; set; }
        [DataMember(Order=103)] public Resource[] MerchantFleetResources { get; set; } = Array.Empty<Resource>();
        [DataMember(Order=104)] public Commodity[] MerchantFleetCommodities { get; set; } = Array.Empty<Commodity>();
        [DataMember(Order=105)] public int AlchemyDice1 { get; set; }
        [DataMember(Order=106)] public int AlchemyDice2 { get; set; }
        [DataMember(Order=107)] public string[] CommercialHarborPlayers { get; set; } = Array.Empty<string>();
    }
    public sealed partial class PendingDecision
    {
        [DataMember(Order=100)] public bool KnightActive { get; set; }
        [DataMember(Order=101)] public int KnightLevel { get; set; }
    }
    public sealed partial class PublicPlayer
    {
        public int ProgressCardCount { get; internal set; }
        public ProgressCardKind[] ProgressVictoryCards { get; internal set; }
    }
    public sealed partial class PlayerView
    {
        public ProgressCardKind[] OwnProgressCards { get; internal set; }
        public string MerchantPlayerId { get; internal set; }
        public string MerchantTileId { get; internal set; }
        public Resource[] MerchantFleetResources { get; internal set; }
        public Commodity[] MerchantFleetCommodities { get; internal set; }
        public string[] CommercialHarborPlayers { get; internal set; }
        public int AlchemyDice1 { get; internal set; }
        public int AlchemyDice2 { get; internal set; }
        // Populated only for the seat resolving its authorized Espionage or Guild Dues choice.
        public ProgressCardKind[] PrivateTargetProgressCards { get; internal set; }
        public ResourceBag PrivateTargetResources { get; internal set; }
        public CommodityBag PrivateTargetCommodities { get; internal set; }
    }
}
