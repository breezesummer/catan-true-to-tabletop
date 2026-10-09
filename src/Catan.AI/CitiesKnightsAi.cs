using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core;
using Catan.Core.M4;
using Command = Catan.Core.M4.Command;
using CommandKind = Catan.Core.M4.CommandKind;
using GamePhase = Catan.Core.M4.GamePhase;
using PlayerView = Catan.Core.M4.PlayerView;
using static Catan.AI.AiHeuristics;

namespace Catan.AI
{
/// <summary>Deterministic local heuristic. Only the acting player's authorized view is accepted.</summary>
public sealed class CitiesKnightsAi
{
    private static readonly Resource[] Resources = (Resource[])Enum.GetValues(typeof(Resource));
    private static readonly Commodity[] Commodities = (Commodity[])Enum.GetValues(typeof(Commodity));
    private static ResourceBag Bag(int wood = 0, int brick = 0, int wool = 0, int wheat = 0, int ore = 0) => new() { Wood = wood, Brick = brick, Wool = wool, Wheat = wheat, Ore = ore };

    /// <summary>Choose one command. The caller supplies its unique command ID and executes it through the rule session.</summary>
    public Command Decide(PlayerView view)
    {
        if (view == null) throw new ArgumentNullException(nameof(view));
        if (view.Phase == GamePhase.Finished) throw new InvalidOperationException("The game has finished.");
        var actor = view.TradeOffer != null ? view.TradeOffer.OtherPlayerId : view.PendingDecision?.PlayerId ?? view.ActivePlayerId;
        var canDiscard = view.Phase == GamePhase.Discard && view.Discards.Any(d => d.PlayerId == view.PlayerId);
        if (view.Phase == GamePhase.Discard ? !canDiscard : actor != view.PlayerId) throw new InvalidOperationException("This seat has no pending action.");
        return Copy(Choose(view));
    }
    private Command Choose(PlayerView view)
    {
        Command Make(CommandKind kind, string target = null) => new() { PlayerId = view.PlayerId, Kind = kind, TargetId = target! };
        if (view.TradeOffer != null) return RespondToTrade(view);
        var legal = (view.LegalActions ?? Array.Empty<Command>()).ToList();
        if (view.Phase == GamePhase.SetupSettlement)
            return Make(CommandKind.SetupSettlement, view.LegalVertexIds.OrderByDescending(v => Value(view, v)).ThenBy(v => v).First());
        if (view.Phase == GamePhase.SetupRoad)
            return Make(CommandKind.SetupRoad, view.LegalEdgeIds.OrderByDescending(id => view.Board.Edges.Single(e => e.Id == id).Vertices.Max(v => Value(view, v))).First());
        if (view.Phase == GamePhase.ProductionAwaitRoll)
        {
            var alchemy = legal.FirstOrDefault(c => c.Kind == CommandKind.PlayProgressCard && c.ProgressCard == ProgressCardKind.Alchemy);
            if (alchemy != null)
            {
                var sum = Enumerable.Range(2, 11).Where(n => n != 7).OrderByDescending(n => view.Board.Tiles.Where(t => t.Number == n && t.Id != view.Board.RobberTileId)
                    .Sum(t => Buildings(view).Where(b => t.Vertices.Contains(b.LocationId)).Sum(b => b.PlayerId == view.PlayerId ? 3.0 : -0.5))).First();
                var chosen = Copy(alchemy); chosen.ChosenDice1 = Math.Min(6, sum - 1); chosen.ChosenDice2 = sum - chosen.ChosenDice1; return chosen;
            }
            return Make(CommandKind.RollDice);
        }
        if (view.Phase == GamePhase.Discard)
        {
            var discard = Make(CommandKind.DiscardResources); discard.Resources = Bag(); discard.Commodities = new();
            var amount = view.Discards.Single(d => d.PlayerId == view.PlayerId).Amount;
            while (amount-- > 0)
            {
                var resource = Resources.OrderByDescending(r => view.OwnResources[r] - discard.Resources[r]).First();
                var commodity = Commodities.OrderByDescending(c => view.OwnCommodities[c] - discard.Commodities[c]).First();
                if (view.OwnResources[resource] - discard.Resources[resource] >= view.OwnCommodities[commodity] - discard.Commodities[commodity]) discard.Resources[resource]++;
                else discard.Commodities[commodity]++;
            }
            return discard;
        }
        if (view.Phase == GamePhase.RobberMove)
            return Make(CommandKind.MoveRobber, view.Board.Tiles.Where(t => t.Id != view.Board.RobberTileId).OrderByDescending(t =>
                Buildings(view).Where(b => t.Vertices.Contains(b.LocationId)).Sum(b => b.PlayerId == view.PlayerId ? -100 : 10 + view.Players.Single(p => p.Id == b.PlayerId).ResourceCount)).First().Id);
        if (view.Phase == GamePhase.RobberSteal)
        {
            var theft = Make(CommandKind.StealResource); theft.OtherPlayerId = view.PendingDecision.EligibleVictimIds.First(); return theft;
        }
        if (view.Phase == GamePhase.PendingChoice && view.PendingDecision?.Kind == "ProgressDiscard")
        {
            var discard = Make(CommandKind.DiscardProgressCard); discard.ProgressCard = view.OwnProgressCards.OrderBy(CardValue).First(); return discard;
        }
        if (view.Phase != GamePhase.Action)
        {
            if (legal.Count == 0) throw new InvalidOperationException($"No public legal actions for {view.PlayerId}/{view.Phase}/{view.PendingDecision?.Kind}");
            return legal.OrderByDescending(c => PendingValue(view, c)).First();
        }
        legal.AddRange(view.LegalCityVertexIds.Select(v => Make(CommandKind.BuildCity, v)));
        legal.AddRange(view.LegalVertexIds.Select(v => Make(CommandKind.BuildSettlement, v)));
        legal.AddRange(view.LegalEdgeIds.Select(v => Make(CommandKind.BuildRoad, v)));

        var knights = view.Board.Knights.Where(k => k.PlayerId == view.PlayerId).ToArray();
        var me = view.Players.Single(p => p.Id == view.PlayerId);
        var cityCount = view.Board.Cities.Count(b => b.PlayerId == view.PlayerId);
        var defenseGoal = Math.Max(2, cityCount + 1);
        var activate = legal.FirstOrDefault(c => c.Kind == CommandKind.ActivateKnight);
        if (activate != null) return activate;
        if (knights.Sum(k => k.Level) < defenseGoal)
        {
            var promote = legal.FirstOrDefault(c => c.Kind == CommandKind.PromoteKnight);
            if (promote != null) return promote;
            var recruit = legal.Where(c => c.Kind == CommandKind.RecruitKnight).OrderBy(c => Value(view, c.TargetId)).FirstOrDefault();
            if (recruit != null) return recruit;
        }
        var city = legal.Where(c => c.Kind == CommandKind.BuildCity).OrderByDescending(c => Value(view, c.TargetId)).FirstOrDefault();
        if (city != null) return city;
        var settlement = legal.Where(c => c.Kind == CommandKind.BuildSettlement).OrderByDescending(c => Value(view, c.TargetId)).FirstOrDefault();
        if (settlement != null) return settlement;
        var improvement = legal.Where(c => c.Kind == CommandKind.ImproveCity)
            .OrderByDescending(c => view.OwnImprovements[(int)c.Track] == 3 ? 100 : c.Track == ImprovementTrack.Science ? 40 : c.Track == ImprovementTrack.Trade ? 30 : 10).FirstOrDefault();
        if (improvement != null) return improvement;
        var card = legal.Where(c => c.Kind == CommandKind.PlayProgressCard && ProgressValue(view, c) > 0).OrderByDescending(c => ProgressValue(view, c)).FirstOrDefault();
        if (card != null) return card;
        var harbor = legal.Where(c => c.Kind == CommandKind.CommercialHarborOffer).OrderByDescending(c => view.OwnResources[c.Resource]).FirstOrDefault();
        if (harbor != null) return harbor;

        var chase = legal.FirstOrDefault(c => c.Kind == CommandKind.ChaseRobber);
        if (chase != null && view.Board.Tiles.Any(t => t.Id == view.Board.RobberTileId && Buildings(view).Any(b => b.PlayerId == view.PlayerId && t.Vertices.Contains(b.LocationId)))) return chase;
        var wall = legal.FirstOrDefault(c => c.Kind == CommandKind.BuildWall);
        if (wall != null && view.OwnResources.Brick >= 3 && view.OwnResources.Total + view.OwnCommodities.Total >= 7) return wall;
        var expansion = Expansion(view);
        var desired = Bag();
        CommodityBag commodityGoal = new();
        if (knights.Any(k => !k.Active)) desired.Wheat = 1;
        else if (knights.Sum(k => k.Level) < defenseGoal) { desired.Wool = 1; desired.Ore = 1; }
        else if (me.Pieces.Cities > 0 && view.Board.Settlements.Any(b => b.PlayerId == view.PlayerId)
            && (view.OwnResources.Ore >= 2 || me.Pieces.Settlements == 0 || expansion == null)) desired = Bag(wheat: 2, ore: 3);
        else if (me.Pieces.Settlements > 0 && expansion != null) desired = expansion.Value.path.Length == 0 ? Bag(1, 1, 1, 1) : Bag(1, 1);
        else if (cityCount > 0)
        {
            var track = new[] { ImprovementTrack.Science, ImprovementTrack.Trade, ImprovementTrack.Politics }.FirstOrDefault(t => view.OwnImprovements[(int)t] < 5);
            commodityGoal[TrackCommodity(track)] = view.OwnImprovements[(int)track] + 1;
        }
        if ((expansion.HasValue && expansion.Value.path.Length > 0) && Covers(view.OwnResources, desired))
        {
            var road = legal.FirstOrDefault(c => c.Kind == CommandKind.BuildRoad && c.TargetId == expansion.Value.path[0]);
            if (road != null) return road;
        }
        var proposal = ProposeTrade(view, desired); if (proposal != null) return proposal;
        foreach (var need in Resources.Where(r => view.OwnResources[r] < desired[r] && view.Bank[r] > 0).Select(r => (resource: r, commodity: (Commodity?)null))
            .Concat(Commodities.Where(c => view.OwnCommodities[c] < commodityGoal[c] && view.CommodityBank[c] > 0).Select(c => (resource: default(Resource), commodity: (Commodity?)c))))
        {
            foreach (var give in Resources.Where(r => (need.commodity.HasValue || r != need.resource) && view.OwnResources[r] - desired[r] >= Rate(view, r, null)))
            {
                var trade = Make(CommandKind.BankTrade); trade.GiveResource = give; trade.GiveAmount = Rate(view, give, null);
                trade.ReceiveResource = need.resource; trade.ReceiveIsCommodity = need.commodity.HasValue; trade.ReceiveCommodity = need.commodity.GetValueOrDefault(); return trade;
            }
            foreach (var give in Commodities.Where(c => c != need.commodity && view.OwnCommodities[c] - commodityGoal[c] >= Rate(view, null, c)))
            {
                var trade = Make(CommandKind.BankTrade); trade.GiveIsCommodity = true; trade.GiveCommodity = give; trade.GiveAmount = Rate(view, null, give);
                trade.ReceiveResource = need.resource; trade.ReceiveIsCommodity = need.commodity.HasValue; trade.ReceiveCommodity = need.commodity.GetValueOrDefault(); return trade;
            }
        }
        if (view.OwnProgressCards.Length > 4) { var discard = Make(CommandKind.DiscardProgressCard); discard.ProgressCard = view.OwnProgressCards.OrderBy(CardValue).First(); return discard; }
        return Make(CommandKind.EndTurn);
    }

    private static double CardValue(ProgressCardKind card)
    {
        switch (card)
        {
            case ProgressCardKind.Medicine: case ProgressCardKind.Crane: case ProgressCardKind.Merchant: return 30;
            case ProgressCardKind.RoadBuilding: case ProgressCardKind.Smithing: case ProgressCardKind.ResourceMonopoly: return 20;
            case ProgressCardKind.Invention: case ProgressCardKind.Diplomacy: return 4;
            default: return 12;
        }
    }

    private static double ProgressValue(PlayerView view, Command c)
    {
        double value = CardValue(c.ProgressCard);
        if (c.ProgressCard == ProgressCardKind.ResourceMonopoly) return 10 + (19 - view.Bank[c.Resource] - view.OwnResources[c.Resource]) * 2;
        if (c.ProgressCard == ProgressCardKind.TradeMonopoly) return 10 + (12 - view.CommodityBank[c.Commodity] - view.OwnCommodities[c.Commodity]) * 3;
        if (c.ProgressCard == ProgressCardKind.MerchantFleet) return c.ChooseCommodity ? view.OwnCommodities[c.Commodity] * 2 : view.OwnResources[c.Resource] * 2;
        if (c.ProgressCard == ProgressCardKind.Invention)
        {
            var source = view.Board.Tiles.First(t => t.Id == c.SourceId); var target = view.Board.Tiles.First(t => t.Id == c.TargetId);
            double Ownership(Tile t) => Buildings(view).Where(b => t.Vertices.Contains(b.LocationId)).Sum(b => b.PlayerId == view.PlayerId ? 1.0 : -0.4);
            return (Ownership(source) - Ownership(target)) * (Math.Abs(7 - source.Number.Value) - Math.Abs(7 - target.Number.Value));
        }
        if (c.ProgressCard == ProgressCardKind.Diplomacy && view.Board.Roads.Any(r => r.LocationId == c.TargetId && r.PlayerId == view.PlayerId)) return -1;
        if (c.ProgressCard == ProgressCardKind.Medicine) value += Value(view, c.TargetId);
        if (c.ProgressCard == ProgressCardKind.Merchant && view.MerchantPlayerId != view.PlayerId) value += 30;
        if (c.OtherPlayerId != null) value += view.Players.Single(p => p.Id == c.OtherPlayerId).VictoryPoints;
        return value;
    }

    private static double PendingValue(PlayerView view, Command c)
    {
        if (c.Decline) return -1000;
        switch (view.PendingDecision?.Kind)
        {
            case "Aqueduct": return 10.0 / (1 + view.OwnResources[c.Resource]);
            case "PillageCity": return -Value(view, c.TargetId);
            case "Metropolis": return Value(view, c.TargetId);
            case "DefenderProgress": return c.Track == ImprovementTrack.Science ? 30 : c.Track == ImprovementTrack.Trade ? 20 : 10;
            case "DisplacedKnight": case "ProgressTreasonPlace": return c.KnightLevel * 10 - Value(view, c.TargetId);
            case "ProgressEspionage": return CardValue(c.SelectedProgressCard);
            case "ProgressHarbor": return view.OwnCommodities[c.Commodity];
            case "ProgressRoads":
                var edge = view.Board.Edges.FirstOrDefault(e => e.Id == c.TargetId);
                return edge == null ? 0 : edge.Vertices.Max(v => Value(view, v));
            default: return 0;
        }
    }
    // Detach every mutable command field before returning a projected legal action.
    private static Command Copy(Command c) => new Command
    {
        PlayerId=c.PlayerId, Kind=c.Kind, TargetId=c.TargetId, OtherPlayerId=c.OtherPlayerId,
        GiveResource=c.GiveResource, ReceiveResource=c.ReceiveResource, GiveAmount=c.GiveAmount, ReceiveAmount=c.ReceiveAmount,
        Give=c.Give?.Copy(), Receive=c.Receive?.Copy(), Resources=c.Resources?.Copy(), Resource=c.Resource,
        SourceId=c.SourceId, Track=c.Track, Commodity=c.Commodity, Commodities=c.Commodities?.Copy(),
        GiveCommodities=c.GiveCommodities?.Copy(), ReceiveCommodities=c.ReceiveCommodities?.Copy(),
        GiveIsCommodity=c.GiveIsCommodity, ReceiveIsCommodity=c.ReceiveIsCommodity, GiveCommodity=c.GiveCommodity,
        ReceiveCommodity=c.ReceiveCommodity, ProgressCard=c.ProgressCard, SelectedProgressCard=c.SelectedProgressCard,
        TargetIds=c.TargetIds?.ToArray(), ChosenDice1=c.ChosenDice1, ChosenDice2=c.ChosenDice2,
        ChooseCommodity=c.ChooseCommodity, Decline=c.Decline, KnightLevel=c.KnightLevel, 
    };

    private static Command RespondToTrade(PlayerView view)
    {
        var offer = view.TradeOffer;
        var gives = offer.GiveCommodities ?? new CommodityBag();
        var costs = offer.ReceiveCommodities ?? new CommodityBag();
        var affordable = Covers(view.OwnResources, offer.Receive) && Commodities.All(c => view.OwnCommodities[c] >= costs[c]);
        double benefit = TradeValue(view.OwnResources, offer.Give, offer.Receive);
        benefit += Commodities.Sum(c => (gives[c] - costs[c]) * (view.OwnImprovements[(int)(c == Commodity.Paper ? ImprovementTrack.Science : c == Commodity.Cloth ? ImprovementTrack.Trade : ImprovementTrack.Politics)] < 5 ? 2.5 : 1.0));
        return new Command { PlayerId = view.PlayerId, Kind = affordable && benefit >= -0.01 ? CommandKind.AcceptTrade : CommandKind.RejectTrade };
    }
    private static int Rate(PlayerView view, Resource? resource, Commodity? commodity)
    {
        var ports = view.Board.Ports.Where(p => Buildings(view).Any(b => b.PlayerId == view.PlayerId && p.Vertices.Contains(b.LocationId))).ToArray();
        if (resource.HasValue && (ports.Any(p => p.Resource == resource.Value) || view.MerchantFleetResources.Contains(resource.Value)
            || view.MerchantPlayerId == view.PlayerId && view.Board.Tiles.Any(t => t.Id == view.MerchantTileId && t.Resource == resource.Value.ToString().ToLowerInvariant()))) return 2;
        if (commodity.HasValue && (view.OwnImprovements[(int)ImprovementTrack.Trade] >= 3 || view.MerchantFleetCommodities.Contains(commodity.Value))) return 2;
        return ports.Any(p => p.Resource == null) ? 3 : 4;
    }
    private static Commodity TrackCommodity(ImprovementTrack track) => track == ImprovementTrack.Science ? Commodity.Paper : track == ImprovementTrack.Trade ? Commodity.Cloth : Commodity.Coin;
    // One voluntary offer per turn. Public receipts survive save/load, so no hidden AI memory is needed.
    private static Command ProposeTrade(PlayerView view, ResourceBag goal)
    {
        var since = view.Events.LastOrDefault(e => e.Kind == "EndTurn")?.Sequence ?? 0;
        if (view.Events.Any(e => e.Sequence > since && e.PlayerId == view.PlayerId && e.Kind == "ProposeTrade")) return null;
        var needs = Resources.Where(r => view.OwnResources[r] < goal[r]).OrderBy(r => view.OwnResources[r] - goal[r]).ToArray();
        var surplus = Resources.Where(r => view.OwnResources[r] > goal[r]).OrderByDescending(r => view.OwnResources[r] - goal[r]).ToArray();
        if (needs.Length == 0 || surplus.Length == 0) return null;
        var partner = view.Players.Where(p => p.Id != view.PlayerId && p.ResourceCount > 0)
            .OrderBy(p => p.VictoryPoints).ThenByDescending(p => p.ResourceCount).ThenBy(p => p.Id, StringComparer.Ordinal).FirstOrDefault();
        if (partner == null) return null;
        var give = Bag(); give[surplus[0]] = 1;
        var receive = Bag(); receive[needs[0]] = 1;
        return new Command { PlayerId = view.PlayerId, Kind = CommandKind.ProposeTrade, OtherPlayerId = partner.Id, Give = give, Receive = receive };
    }
    private static bool Covers(ResourceBag hand, ResourceBag cost) => Resources.All(r => hand[r] >= cost[r]);
    private static PiecePlacement[] Buildings(PlayerView view) => view.Board.Settlements.Concat(view.Board.Cities).ToArray();
    private static double Value(PlayerView view, string vertex)
    {
        var owned = Buildings(view).Where(b => b.PlayerId == view.PlayerId).Select(b => b.LocationId).ToArray();
        return view.Board.Tiles.Where(t => t.Vertices.Contains(vertex) && t.Number.HasValue).Sum(t =>
        {
            var pips = 6 - Math.Abs(7 - t.Number!.Value);
            var already = view.Board.Tiles.Count(other => other.Resource == t.Resource && other.Vertices.Intersect(owned).Any());
            return pips * (t.Resource is "ore" or "wheat" ? 1.4 : 1.0) + (already == 0 ? 3 : 0);
        });
    }

    private static (string target, string[] path)? Expansion(PlayerView view)
    {
        var buildings = Buildings(view);
        var ownRoads = view.Board.Roads.Where(r => r.PlayerId == view.PlayerId).Select(r => r.LocationId).ToHashSet();
        var otherRoads = view.Board.Roads.Where(r => r.PlayerId != view.PlayerId).Select(r => r.LocationId).ToHashSet();
        var enemyVertices = buildings.Where(b => b.PlayerId != view.PlayerId).Select(b => b.LocationId)
            .Concat(view.Board.Knights.Where(k => k.PlayerId != view.PlayerId).Select(k => k.LocationId)).ToHashSet();
        var starts = view.Board.Edges.Where(e => ownRoads.Contains(e.Id)).SelectMany(e => e.Vertices)
            .Concat(buildings.Where(b => b.PlayerId == view.PlayerId).Select(b => b.LocationId)).Where(v => !enemyVertices.Contains(v)).Distinct().ToArray();
        var paths = starts.ToDictionary(v => v, _ => Array.Empty<string>());
        var pending = starts.ToList();
        while (pending.Count > 0)
        {
            var v = pending.OrderBy(v => paths[v].Length).First(); pending.Remove(v);
            foreach (var edge in view.Board.Edges.Where(e => e.Vertices.Contains(v) && !otherRoads.Contains(e.Id)))
            {
                var next = edge.Vertices.First(x => x != v);
                if (enemyVertices.Contains(next)) continue;
                var path = ownRoads.Contains(edge.Id) ? paths[v] : paths[v].Append(edge.Id).ToArray();
                if (path.Length > 5 || (paths.TryGetValue(next, out var old) && old.Length <= path.Length)) continue;
                paths[next] = path; if (!pending.Contains(next)) pending.Add(next);
            }
        }
        var targets = view.Board.Vertices.Where(v => !buildings.Any(b => b.LocationId == v.Id) && !view.Board.Knights.Any(k => k.LocationId == v.Id)
            && !view.Board.Edges.Where(e => e.Vertices.Contains(v.Id)).Any(e => buildings.Any(b => e.Vertices.Contains(b.LocationId)))
            && paths.ContainsKey(v.Id)).Select(v => (target: v.Id, path: paths[v.Id])).ToArray();
        if (targets.Length == 0) return null;
        return targets.OrderByDescending(t => Value(view, t.target) - t.path.Length * 12).ThenBy(t => t.target).First();
    }
}

}
