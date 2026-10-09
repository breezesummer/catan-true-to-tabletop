using System.Linq;

namespace Catan.Core.M5
{
    public sealed partial class GameState
    {
        internal GameState CopyForTransaction()
        {
            // Preview evaluates hundreds of board targets. A serialization round trip
            // for each target is expensive in Unity/Mono; detach the complete mutable
            // state graph directly, preserving exactly the same authority values.
            var copy=(GameState)MemberwiseClone();
            copy.Random=new RandomState {AlgorithmId=Random.AlgorithmId,State=Random.State,Draws=Random.Draws};
            copy.Players=Players.Select(p=>p.CopyForTransaction()).ToArray();
            copy.Bank=Bank.Copy();copy.CommodityBank=CommodityBank.Copy();
            copy.Settlements=CopyPlacements(Settlements);copy.Cities=CopyPlacements(Cities);copy.Roads=CopyPlacements(Roads);
            copy.Walls=CopyPlacements(Walls);copy.DowngradedCities=CopyPlacements(DowngradedCities);
            copy.Knights=Knights.Select(CopyKnight).ToArray();
            copy.Metropolises=Metropolises.Select(m=>new Metropolis {Track=m.Track,PlayerId=m.PlayerId,LocationId=m.LocationId}).ToArray();
            copy.Ships=Ships.Select(s=>new ShipPlacement {LocationId=s.LocationId,PlayerId=s.PlayerId,BuiltTurn=s.BuiltTurn}).ToArray();
            copy.PendingDecision=PendingDecision?.CopyForTransaction();
            copy.DecisionQueue=DecisionQueue.Select(d=>d.CopyForTransaction()).ToArray();
            copy.Discards=Discards.Select(d=>new DiscardRequirement {PlayerId=d.PlayerId,Amount=d.Amount}).ToArray();
            copy.TradeOffer=TradeOffer?.CopyForTransaction();
            copy.DevelopmentDeck=DevelopmentDeck.ToArray();copy.PlayedDevelopmentCards=PlayedDevelopmentCards.ToArray();
            copy.ProgressDecks=ProgressDecks.Select(d=>new ProgressDeck {Track=d.Track,Cards=d.Cards.ToArray()}).ToArray();
            copy.Tiles=Tiles.Select(t=>new Tile {Id=t.Id,Q=t.Q,R=t.R,Resource=t.Resource,Number=t.Number,Vertices=t.Vertices.ToArray()}).ToArray();
            copy.MerchantFleetResources=MerchantFleetResources.ToArray();copy.MerchantFleetCommodities=MerchantFleetCommodities.ToArray();
            copy.CommercialHarborPlayers=CommercialHarborPlayers.ToArray();
            // These two ledgers are append-only inside Execute. Existing records are
            // never edited; Append allocates a new array, and external access is still
            // through detached serialized snapshots and permission-filtered views.
            copy.ProcessedCommands=ProcessedCommands;copy.Events=Events;
            return copy;
        }
        private static PiecePlacement[] CopyPlacements(PiecePlacement[] values) => values.Select(p=>new PiecePlacement {LocationId=p.LocationId,PlayerId=p.PlayerId}).ToArray();
        internal static Knight CopyKnight(Knight knight) => knight==null?null:new Knight
        {
            PlayerId=knight.PlayerId,LocationId=knight.LocationId,Level=knight.Level,Active=knight.Active,
            ActivatedTurn=knight.ActivatedTurn,PromotedTurn=knight.PromotedTurn,ActedTurn=knight.ActedTurn
        };
    }
    public sealed partial class PlayerState
    {
        internal PlayerState CopyForTransaction()
        {
            var copy=(PlayerState)MemberwiseClone();
            copy.Resources=Resources.Copy();copy.Commodities=Commodities.Copy();copy.Pieces=Pieces.Copy();
            copy.DevelopmentCards=DevelopmentCards.Select(c=>new DevelopmentCard {Kind=c.Kind,BoughtTurn=c.BoughtTurn}).ToArray();
            copy.Improvements=Improvements.ToArray();copy.HomeRegions=HomeRegions.ToArray();copy.SettledRegions=SettledRegions.ToArray();
            copy.ProgressCards=ProgressCards.ToArray();copy.ProgressVictoryCards=ProgressVictoryCards.ToArray();
            return copy;
        }
    }
    public sealed partial class PendingDecision
    {
        internal PendingDecision CopyForTransaction()
        {
            var copy=(PendingDecision)MemberwiseClone();
            copy.EligibleVictimIds=EligibleVictimIds.ToArray();copy.Options=Options.ToArray();
            copy.DisplacedKnight=GameState.CopyKnight(DisplacedKnight);
            return copy;
        }
    }
    public sealed partial class TradeOffer
    {
        internal TradeOffer CopyForTransaction()
        {
            var copy=(TradeOffer)MemberwiseClone();
            copy.Give=Give.Copy();copy.Receive=Receive.Copy();
            copy.GiveCommodities=GiveCommodities.Copy();copy.ReceiveCommodities=ReceiveCommodities.Copy();
            return copy;
        }
    }
}
