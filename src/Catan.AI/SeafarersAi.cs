using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core;
using Catan.Core.M3;
using Command = Catan.Core.M3.Command;
using CommandKind = Catan.Core.M3.CommandKind;
using GamePhase = Catan.Core.M3.GamePhase;
using PlayerView = Catan.Core.M3.PlayerView;
using static Catan.AI.AiHeuristics;

namespace Catan.AI
{
/// <summary>Deterministic local heuristic. Only the acting player's authorized view is accepted.</summary>
public sealed class SeafarersAi
{
    private static readonly ResourceBag City = Bag(wheat: 2, ore: 3);
    private static readonly ResourceBag Settlement = Bag(1, 1, 1, 1);
    private static readonly ResourceBag Road = Bag(1, 1);
    private static readonly ResourceBag Ship = Bag(wood: 1, wool: 1);
    private static readonly ResourceBag Development = Bag(wool: 1, wheat: 1, ore: 1);

    /// <summary>Choose one command. The caller supplies its unique command ID and executes it through the rule session.</summary>
    public Command Decide(PlayerView view)
    {
        if (view == null) throw new ArgumentNullException(nameof(view));
        if (view.Phase == GamePhase.Finished) throw new InvalidOperationException("The game has finished.");
        var actor = view.TradeOffer != null ? view.TradeOffer.NextResponder(view.PlayerId) : view.Phase == GamePhase.GoldChoice ? view.GoldClaims.First().PlayerId : view.PendingDecision?.PlayerId ?? view.ActivePlayerId;
        var canDiscard = view.Phase == GamePhase.Discard && view.Discards.Any(d => d.PlayerId == view.PlayerId);
        if (view.Phase == GamePhase.Discard ? !canDiscard : actor != view.PlayerId) throw new InvalidOperationException("This seat has no pending action.");
        return Choose(view);
    }
    private Command Choose(PlayerView view)
    {
        Command Make(CommandKind kind, string target = null) => new() { PlayerId = view.PlayerId, Kind = kind, TargetId = target! };
        switch (view.Phase)
        {
            case GamePhase.SetupPort:
                return Make(CommandKind.SetupPort, view.LegalPortEdgeIds.First());
            case GamePhase.SetupSettlement:
                return Make(CommandKind.SetupSettlement, view.LegalVertexIds.OrderByDescending(v => SetupValue(view, v)).ThenBy(v => v).First());
            case GamePhase.SetupRoad:
                var opening = Expansion(view);
                if (opening.HasValue && opening.Value.ship && opening.Value.path.Length > 0 && view.LegalShipEdgeIds.Contains(opening.Value.path[0]))
                    return Make(CommandKind.SetupShip, opening.Value.path[0]);
                return view.LegalEdgeIds.Length > 0 ? Make(CommandKind.SetupRoad, BestRoad(view, view.LegalEdgeIds)) : Make(CommandKind.SetupShip, view.LegalShipEdgeIds.First());
            case GamePhase.GoldChoice:
                var gold = Make(CommandKind.ChooseGoldResources); gold.Resources = Bag();
                var goldRoute = Expansion(view);
                var goldGoal = goldRoute.HasValue ? goldRoute.Value.path.Length == 0 ? Settlement : goldRoute.Value.ship ? Ship : Road : City;
                for (var i = 0; i < Math.Min(view.GoldClaims.First(g => g.PlayerId == view.PlayerId).Amount, view.Bank.Total); i++)
                { var r = Resources.Where(r => view.Bank[r] > gold.Resources[r]).OrderByDescending(r => goldGoal[r] - view.OwnResources[r] - gold.Resources[r]).ThenBy(r => view.OwnResources[r] + gold.Resources[r]).First(); gold.Resources[r]++; }
                return gold;
            case GamePhase.PortPlacement:
                var port = Make(CommandKind.PlacePort, view.LegalPortEdgeIds.First()); port.SourceId = view.OwnHeldPorts.First().Id; return port;
            case GamePhase.Discard:
                var cards = new ResourceBag(); var remaining = view.Discards.Single(d => d.PlayerId == view.PlayerId).Amount;
                while (remaining-- > 0)
                { var r = Resources.OrderByDescending(r => view.OwnResources[r] - cards[r]).First(); cards[r]++; }
                var discard = Make(CommandKind.DiscardResources); discard.Resources = cards; return discard;
            case GamePhase.RobberMove:
                return MoveThreat(view);
            case GamePhase.RobberSteal:
                var theft = Make(CommandKind.StealResource); theft.OtherPlayerId = view.PendingDecision.EligibleVictimIds.OrderByDescending(id => TheftValue(view, id)).ThenBy(id => id).First(); return theft;
            case GamePhase.RoadBuilding:
                var route = Expansion(view);
                if (route.HasValue && route.Value.path.Length > 0 && route.Value.ship && view.LegalShipEdgeIds.Contains(route.Value.path[0]))
                    return Make(CommandKind.PlaceFreeShip, route.Value.path[0]);
                return view.LegalEdgeIds.Length > 0 ? Make(CommandKind.PlaceFreeRoad, BestRoad(view, view.LegalEdgeIds)) :
                    view.LegalShipEdgeIds.Length > 0 ? Make(CommandKind.PlaceFreeShip, BestRoad(view, view.LegalShipEdgeIds)) : Make(CommandKind.FinishRoadBuilding);
            case GamePhase.ProductionAwaitRoll:
                return CanPlay(view, DevelopmentCardKind.Knight) && (view.ScenarioId != "pirate-islands" || view.Board.Ships.Any(s => s.PlayerId == view.PlayerId && s.IsInvasionRoute && !s.IsWarship)) ? Make(CommandKind.PlayKnight) : Make(CommandKind.RollDice);
            case GamePhase.Action: break;
            default: throw new InvalidOperationException("Unexpected phase " + view.Phase);
        }
        if (view.TradeOffer != null)
        { var offer = view.TradeOffer; return Make(Covers(view.OwnResources, offer.Receive) && TradeValue(view.OwnResources, offer.Give, offer.Receive) >= -0.01 ? CommandKind.AcceptTrade : CommandKind.RejectTrade); }
        var me = view.Players.Single(p => p.Id == view.PlayerId);
        var win = view.LegalVertexIds.Where(v => view.OwnVictoryPoints + SettlementPoints(view, v) >= view.TargetVictoryPoints)
            .OrderByDescending(v => SettlementPoints(view, v)).ThenBy(v => v).FirstOrDefault();
        if (win != null) return Make(CommandKind.BuildSettlement, win);
        if (view.OwnVictoryPoints + 1 >= view.TargetVictoryPoints && view.LegalCityVertexIds.Length > 0)
            return Make(CommandKind.BuildCity, view.LegalCityVertexIds.OrderByDescending(v => Value(view, v)).First());
        var award = CheapLongestRoute(view);
        if (award != null && view.OwnVictoryPoints + 2 >= view.TargetVictoryPoints) return award;
        var expansion = Expansion(view);
        if (view.ScenarioId == "pirate-islands" && view.Board.Fortresses.Single(f => f.PlayerId == view.PlayerId).Strength > 0)
        {
            var ownShips = view.Board.Ships.Where(s => s.PlayerId == view.PlayerId && s.IsInvasionRoute).ToArray();
            if (CanPlay(view, DevelopmentCardKind.Knight) && ownShips.Any(s => !s.IsWarship)) return Make(CommandKind.PlayKnight);
            var extension = PirateExtension(view);
            var buyShip = extension != null && (ownShips.Length < 3 || ownShips.Count(s => s.IsWarship) >= Math.Min(4, ownShips.Length) || view.DevelopmentDeckCount == 0 || !ownShips.Any(s => !s.IsWarship));
            var pirateGoal = buyShip ? Ship : Development;
            if (buyShip && Covers(view.OwnResources, Ship) && view.LegalShipEdgeIds.Contains(extension!)) return Make(CommandKind.BuildShip, extension);
            if (!buyShip && view.DevelopmentDeckCount > 0 && Covers(view.OwnResources, Development)) return Make(CommandKind.BuyDevelopmentCard);
            var navyTrade = TradeFor(view, pirateGoal); if (navyTrade != null) return navyTrade;
        }
        if (view.ScenarioId == "wonders-of-catan")
        {
            if (me.WonderId == null && view.Board.Cities.Count(c => c.PlayerId == view.PlayerId) >= 2)
            {
                var available = view.Wonders.FirstOrDefault(w => w.Requirement == "two-cities" && !view.Players.Any(p => p.WonderId == w.Id));
                if (available != null) return Make(CommandKind.ClaimWonder, available.Id);
            }
            if (me.WonderId != null)
            {
                var wonder = view.Wonders.Single(w => w.Id == me.WonderId);
                if (Covers(view.OwnResources, wonder.Cost)) return Make(CommandKind.BuildWonder, me.WonderId);
                var wonderTrade = TradeFor(view, wonder.Cost); if (wonderTrade != null) return wonderTrade;
            }
        }
        ResourceBag goal;
        CommandKind goalKind;
        string target = null;
        var bestCity = view.Board.Settlements.Where(b => b.PlayerId == view.PlayerId)
            .OrderByDescending(b => SeafaringPlanning.Production(view.Board.Tiles, b.LocationId)).FirstOrDefault();
        var cityValue = bestCity == null ? double.NegativeInfinity : 12 + SeafaringPlanning.Production(view.Board.Tiles, bestCity.LocationId) * Horizon(view) - 5;
        if (me.Pieces.Cities > 0 && bestCity != null
            && (me.Pieces.Settlements == 0 || expansion == null || cityValue >= SettlementValue(view, expansion.Value.target, expansion.Value.path.Length)))
        { goal = City; goalKind = CommandKind.BuildCity; target = bestCity.LocationId; }
        else if (me.Pieces.Settlements > 0 && expansion != null)
        { goal = expansion.Value.path.Length == 0 ? Settlement : expansion.Value.ship ? Ship : Road; goalKind = expansion.Value.path.Length == 0 ? CommandKind.BuildSettlement : expansion.Value.ship ? CommandKind.BuildShip : CommandKind.BuildRoad; target = expansion.Value.path.Length == 0 ? expansion.Value.target : expansion.Value.path[0]; }
        else { goal = Development; goalKind = CommandKind.BuyDevelopmentCard; }

        if (award != null && view.OwnVictoryPoints >= view.TargetVictoryPoints - 3) return award;

        if (CanPlay(view, DevelopmentCardKind.YearOfPlenty) && view.Bank.Total > 0)
        {
            var selection = Bag();
            for (var i = 0; i < Math.Min(2, view.Bank.Total); i++)
            {
                var r = Resources.Where(r => view.Bank[r] > selection[r]).OrderByDescending(r => goal[r] - view.OwnResources[r] - selection[r]).ThenBy(r => view.OwnResources[r] + selection[r]).First();
                selection[r]++;
            }
            var card = Make(CommandKind.PlayYearOfPlenty); card.Resources = selection; return card;
        }
        if (CanPlay(view, DevelopmentCardKind.Monopoly))
        {
            var card = Make(CommandKind.PlayMonopoly);
            card.Resource = Resources.OrderByDescending(r => (19 - view.Bank[r] - view.OwnResources[r]) * 2 + Math.Max(0, goal[r] - view.OwnResources[r])).First(); return card;
        }
        if (CanPlay(view, DevelopmentCardKind.RoadBuilding) && expansion.HasValue && expansion.Value.path.Length > 0
            && (expansion.Value.ship ? me.ShipsRemaining > 0 : me.Pieces.Roads > 0))
            return Make(CommandKind.PlayRoadBuilding);
        if (Covers(view.OwnResources, goal) && (goalKind == CommandKind.BuyDevelopmentCard ? view.DevelopmentDeckCount > 0 :
            goalKind == CommandKind.BuildShip ? view.LegalShipEdgeIds.Contains(target) :
            goalKind == CommandKind.BuildRoad ? view.LegalEdgeIds.Contains(target) :
            goalKind == CommandKind.BuildCity ? view.LegalCityVertexIds.Contains(target) : view.LegalVertexIds.Contains(target)))
            return Make(goalKind, target);
        var reposition = RepositionShip(view); if (reposition != null) return reposition;
        var proposal = ProposeTrade(view, goal); if (proposal != null) return proposal;
        var bankTrade = TradeFor(view, goal); if (bankTrade != null) return bankTrade;
        if (view.DevelopmentDeckCount > 0 && Resources.All(r => view.OwnResources[r] >= goal[r] + Development[r])) return Make(CommandKind.BuyDevelopmentCard);
        return Make(CommandKind.EndTurn);
    }

    private static Command TradeFor(PlayerView view, ResourceBag goal)
    {
        foreach (var need in Resources.Where(r => view.OwnResources[r] < goal[r] && view.Bank[r] > 0).OrderBy(r => view.OwnResources[r] - goal[r]))
        {
            var give = Resources.Where(r => r != need && view.OwnResources[r] - goal[r] >= Rate(view, r)).OrderByDescending(r => view.OwnResources[r] - goal[r]).ToArray();
            if (give.Length > 0) return new Command { PlayerId = view.PlayerId, Kind = CommandKind.BankTrade, GiveResource = give[0], GiveAmount = Rate(view, give[0]), ReceiveResource = need, ReceiveAmount = 1 };
        }
        return null;
    }

    private static string PirateExtension(PlayerView view)
    {
        var fortress = view.Board.Fortresses.Single(f => f.PlayerId == view.PlayerId);
        var current = fortress.InvasionStartingVertexId;
        var used = new HashSet<string>(); var passedBeachhead = current == fortress.BeachheadVertexId;
        while (true)
        {
            var ship = view.Board.Ships.FirstOrDefault(s => s.PlayerId == view.PlayerId && s.IsInvasionRoute && !used.Contains(s.LocationId) && view.Board.Edges.Single(e => e.Id == s.LocationId).Vertices.Contains(current));
            if (ship == null) break;
            used.Add(ship.LocationId); current = view.Board.Edges.Single(e => e.Id == ship.LocationId).Vertices.First(v => v != current);
            passedBeachhead |= current == fortress.BeachheadVertexId;
        }
        if (current == fortress.VertexId) return null;
        var goal = passedBeachhead ? fortress.VertexId : fortress.BeachheadVertexId;
        var distances = new Dictionary<string, int> { [goal] = 0 }; var queue = new Queue<string>(); queue.Enqueue(goal);
        while (queue.Count > 0)
        {
            var vertex = queue.Dequeue();
            foreach (var edge in view.Board.Edges.Where(e => e.Vertices.Contains(vertex) && SailingEdge(view, e)))
                foreach (var next in edge.Vertices) if (!distances.ContainsKey(next)) { distances[next] = distances[vertex] + 1; queue.Enqueue(next); }
        }
        return view.Board.Edges.Where(e => e.Vertices.Contains(current) && SailingEdge(view, e)
            && !view.Board.Ships.Any(s => s.LocationId == e.Id) && !view.Board.Roads.Any(r => r.LocationId == e.Id)
            && e.Vertices.Any(v => distances.TryGetValue(v, out var d) && d == distances[current] - 1))
            .OrderBy(e => e.Id).Select(e => e.Id).FirstOrDefault();
    }

    private static bool SailingEdge(PlayerView view, Edge edge)
    {
        var tiles = view.Board.Tiles.Where(t => edge.Vertices.All(t.Vertices.Contains)).ToArray();
        return tiles.Length == 1 || tiles.Any(t => t.Resource is "sea" or "fog");
    }

    private static bool CanPlay(PlayerView view, DevelopmentCardKind kind) => !view.DevelopmentCardPlayedThisTurn && view.OwnDevelopmentCards.Any(c => c.Kind == kind && c.BoughtTurn < view.Turn);
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
    private static int Horizon(PlayerView view) => RemainingRolls(view.Turn, Math.Max(view.OwnVictoryPoints, view.Players.Max(p => p.VictoryPoints)), view.TargetVictoryPoints);
    private static int SettlementPoints(PlayerView view, string vertex) => SeafaringPlanning.SettlementPoints(view.ScenarioId, view.Board.Tiles,
        Buildings(view).Where(b => b.PlayerId == view.PlayerId).ToArray(), vertex);
    private static double SettlementValue(PlayerView view, string vertex, int length) => SeafaringPlanning.SettlementValue(view.ScenarioId,
        view.Board.Tiles, view.Board.Edges, Buildings(view), view.PlayerId, vertex, length, Horizon(view), view.OwnVictoryPoints, view.TargetVictoryPoints);

    private static double TheftValue(PlayerView view, string playerId)
    {
        var player = view.Players.Single(p => p.Id == playerId);
        return player.ResourceCount == 0 ? 0 : 3 + player.VictoryPoints * 0.8 + Math.Min(2, player.ResourceCount * 0.2);
    }

    // 威胁评分只使用公开的产出点数、建筑和航线；优先压制领跑者并避免阻断自己。
    private static Command MoveThreat(PlayerView view)
    {
        var buildings = Buildings(view);
        double RobberValue(Tile tile) => buildings.Where(b => tile.Vertices.Contains(b.LocationId)).Sum(b =>
            b.PlayerId == view.PlayerId ? -10 - Pips(tile.Number) * (view.Board.Cities.Contains(b) ? 2 : 1) :
            Pips(tile.Number) * (view.Board.Cities.Contains(b) ? 2 : 1) * (0.6 + view.Players.Single(p => p.Id == b.PlayerId).VictoryPoints * 0.1))
            + buildings.Where(b => b.PlayerId != view.PlayerId && tile.Vertices.Contains(b.LocationId)).Select(b => TheftValue(view, b.PlayerId)).DefaultIfEmpty(0).Max();
        var destination = view.Board.Tiles.Where(t => t.Id != view.Board.RobberTileId && t.Resource != "sea" && t.Resource != "fog"
            && (view.ScenarioId != "forgotten-tribe" && view.ScenarioId != "cloth-for-catan" || t.Number.HasValue))
            .OrderByDescending(RobberValue).ThenBy(t => t.Id).First();
        var command = new Command { PlayerId = view.PlayerId, Kind = CommandKind.MoveRobber, TargetId = destination.Id };
        if (view.ScenarioId == "wonders-of-catan" || view.ScenarioId == "pirate-islands" ||
            view.ScenarioId == "cloth-for-catan" && !view.Board.Villages.Any(v => v.TradingPlayerIds.Contains(view.PlayerId))) return command;
        double PirateValue(Tile tile)
        {
            var ships = view.Board.Ships.Where(s => view.Board.Edges.Any(e => e.Id == s.LocationId && e.Vertices.All(tile.Vertices.Contains))).ToArray();
            return ships.Sum(s => s.PlayerId == view.PlayerId ? -12 : 2 + view.Players.Single(p => p.Id == s.PlayerId).VictoryPoints * 0.5)
                + ships.Where(s => s.PlayerId != view.PlayerId).Select(s => TheftValue(view, s.PlayerId)).DefaultIfEmpty(0).Max();
        }
        var sea = view.Board.Tiles.Where(t => t.Resource == "sea" && t.Id != view.Board.PirateTileId).OrderByDescending(PirateValue).ThenBy(t => t.Id).FirstOrDefault();
        return sea != null && PirateValue(sea) > RobberValue(destination)
            ? new Command { PlayerId = view.PlayerId, Kind = CommandKind.MovePirate, TargetId = sea.Id } : command;
    }

    // 仅考察一条可立即建造的路/船夺奖，避免四人局投入大量资源长期争路。
    private static Command CheapLongestRoute(PlayerView view)
    {
        if (view.ScenarioId == "cloth-for-catan" || view.ScenarioId == "pirate-islands" || view.LongestRoadPlayerId == view.PlayerId || view.OwnVictoryPoints < view.TargetVictoryPoints - 3) return null;
        var required = Math.Max(5, view.Players.Where(p => p.Id != view.PlayerId).Max(p => p.LongestRoadLength) + 1);
        var buildings = Buildings(view);
        var ownVertices = buildings.Where(b => b.PlayerId == view.PlayerId).Select(b => b.LocationId).ToHashSet();
        var blocked = buildings.Where(b => b.PlayerId != view.PlayerId).Select(b => b.LocationId).ToHashSet();
        var owned = view.Board.Roads.Where(r => r.PlayerId == view.PlayerId).Select(r => r.LocationId)
            .Concat(view.Board.Ships.Where(s => s.PlayerId == view.PlayerId).Select(s => s.LocationId)).ToHashSet();
        // 一条路可以接通两段现有路线，不能用“当前最长长度加一”作上限。
        if (owned.Count + 1 < required) return null;
        foreach (var candidate in view.LegalEdgeIds.Select(id => (id, ship: false)).Concat(view.LegalShipEdgeIds.Select(id => (id, ship: true))))
        {
            var edges = view.Board.Edges.Where(e => owned.Contains(e.Id) || e.Id == candidate.id).ToArray();
            var ships = view.Board.Ships.Where(s => s.PlayerId == view.PlayerId).Select(s => s.LocationId).ToHashSet();
            if (candidate.ship) ships.Add(candidate.id);
            var used = new HashSet<string>();
            bool Reaches(string vertex, string previous, int count)
            {
                if (count >= required) return true;
                if (previous != null && blocked.Contains(vertex)) return false;
                foreach (var edge in edges.Where(e => e.Vertices.Contains(vertex) && !used.Contains(e.Id)))
                {
                    if (previous != null && ships.Contains(previous) != ships.Contains(edge.Id) && !ownVertices.Contains(vertex)) continue;
                    used.Add(edge.Id);
                    if (Reaches(edge.Vertices.First(v => v != vertex), edge.Id, count + 1)) return true;
                    used.Remove(edge.Id);
                }
                return false;
            }
            if (edges.SelectMany(e => e.Vertices).Distinct().Any(v => Reaches(v, null, 0)))
                return new Command { PlayerId = view.PlayerId, Kind = candidate.ship ? CommandKind.BuildShip : CommandKind.BuildRoad, TargetId = candidate.id };
        }
        return null;
    }
    // 仅挪动视图授权的开放船尾；先移除源船，再找仍然连通且能降低登岛成本的目标。
    private static Command RepositionShip(PlayerView view)
    {
        if (view.ShipMovedThisTurn || view.ScenarioId == "pirate-islands") return null;
        var current = Expansion(view);
        var bestValue = current.HasValue ? SettlementValue(view, current.Value.target, current.Value.path.Length) : double.NegativeInfinity;
        Command best = null;
        foreach (var sourceId in view.MovableShipEdgeIds.OrderBy(id => id))
        {
            var next = Expansion(view, sourceId);
            if (!next.HasValue || !next.Value.ship || next.Value.path.Length == 0 || next.Value.path[0] == sourceId) continue;
            var value = SettlementValue(view, next.Value.target, next.Value.path.Length - 1);
            if (value <= bestValue + 0.01) continue;
            bestValue = value;
            best = new Command { PlayerId = view.PlayerId, Kind = CommandKind.MoveShip, SourceId = sourceId, TargetId = next.Value.path[0] };
        }
        return best;
    }
    private static int Rate(PlayerView view, Resource resource)
    {
        var ports = view.Board.Ports.Where(p => Buildings(view).Any(b => b.PlayerId == view.PlayerId && p.Vertices.Contains(b.LocationId))).ToArray();
        return ports.Any(p => p.Resource == resource) ? 2 : ports.Any(p => p.Resource == null) ? 3 : 4;
    }
    private static double Value(PlayerView view, string vertex)
    {
        var owned = Buildings(view).Where(b => b.PlayerId == view.PlayerId).Select(b => b.LocationId).ToArray();
        return view.Board.Tiles.Where(t => t.Vertices.Contains(vertex) && t.Number.HasValue).Sum(t =>
        {
            var pips = 6 - Math.Abs(7 - t.Number!.Value);
            var already = view.Board.Tiles.Count(other => other.Resource == t.Resource && other.Vertices.Intersect(owned).Any());
            return pips * (t.Resource == "ore" || t.Resource == "wheat" ? 1.3 : 1.0) + (already == 0 ? 3 : 0);
        });
    }
    private static string BestRoad(PlayerView view, string[] legal)
    {
        var expansion = Expansion(view);
        if (expansion.HasValue && expansion.Value.path.Length > 0 && legal.Contains(expansion.Value.path[0])) return expansion.Value.path[0];
        return legal.OrderByDescending(id => view.Board.Edges.Single(e => e.Id == id).Vertices.Max(v => Value(view, v))).ThenBy(e => e).First();
    }
    private static (string target, string[] path, bool ship)? Expansion(PlayerView view, string removedShipId = null)
    {
        var buildings = Buildings(view);
        var ownRoads = view.Board.Roads.Where(r => r.PlayerId == view.PlayerId).Select(r => r.LocationId).ToHashSet();
        var ownShips = view.Board.Ships.Where(r => r.PlayerId == view.PlayerId && r.LocationId != removedShipId).Select(r => r.LocationId).ToHashSet();
        var occupiedEdges = view.Board.Roads.Select(r=>r.LocationId).Concat(view.Board.Ships.Where(s => s.LocationId != removedShipId).Select(s=>s.LocationId)).ToHashSet();
        var enemyVertices = buildings.Where(b => b.PlayerId != view.PlayerId).Select(b => b.LocationId).ToHashSet();
        var ownBuildings=buildings.Where(b=>b.PlayerId==view.PlayerId).Select(b=>b.LocationId).ToHashSet();
        var paths = new Dictionary<(string vertex,bool ship),string[]>();
        foreach(var edge in view.Board.Edges)
            foreach(var vertex in edge.Vertices.Where(v=>!enemyVertices.Contains(v)))
            {
                if(ownRoads.Contains(edge.Id)) paths[(vertex,false)]=Array.Empty<string>();
                if(ownShips.Contains(edge.Id)) paths[(vertex,true)]=Array.Empty<string>();
            }
        foreach(var vertex in ownBuildings){paths[(vertex,false)]=Array.Empty<string>();paths[(vertex,true)]=Array.Empty<string>();}
        var pending=paths.Keys.ToList();
        while (pending.Count > 0)
        {
            var v = pending.OrderBy(v => paths[v].Length).First(); pending.Remove(v);
            foreach (var edge in view.Board.Edges.Where(e => e.Vertices.Contains(v.vertex)))
            {
                var next = edge.Vertices.First(x => x != v.vertex);
                if (enemyVertices.Contains(next)) continue;
                foreach(bool ship in view.ScenarioId == "pirate-islands" ? new[]{false} : new[]{false,true})
                {
                    if(ship!=v.ship&&!ownBuildings.Contains(v.vertex))continue;
                    var own=ship?ownShips:ownRoads;
                    if(occupiedEdges.Contains(edge.Id)&&!own.Contains(edge.Id))continue;
                    var tiles=view.Board.Tiles.Where(t=>edge.Vertices.All(t.Vertices.Contains)).ToArray();
                    if(ship ? !tiles.Any(t=>t.Resource=="sea"||t.Resource=="fog")&&tiles.Length!=1 : !tiles.Any(t=>t.Resource!="sea"&&t.Resource!="fog"))continue;
                    if(ship&&tiles.Any(t=>t.Id==view.Board.PirateTileId))continue;
                    var path=own.Contains(edge.Id)?paths[v]:paths[v].Append((ship?"S:":"R:")+edge.Id).ToArray();
                    var key=(next,ship);
                    if(path.Length>6||(paths.TryGetValue(key,out var old)&&old.Length<=path.Length))continue;
                    paths[key]=path;if(!pending.Contains(key))pending.Add(key);
                }
            }
        }
        var me = view.Players.Single(p => p.Id == view.PlayerId);
        // 海盗群岛只能在主岛或自己的指定滩头建村；收复堡垒不开放其他西侧顶点。
        // 用公开起点所属的陆地连通区域识别主岛，避免把舰队途经的肥地当成建设目标。
        HashSet<string> pirateMainland = null;
        string pirateBeachhead = null;
        if (view.ScenarioId == "pirate-islands")
        {
            var fortress = view.Board.Fortresses.Single(f => f.PlayerId == view.PlayerId);
            pirateBeachhead = fortress.BeachheadVertexId;
            var mainland = view.Board.Tiles.Where(t => t.Resource != "sea" && t.Resource != "fog" && t.Vertices.Contains(fortress.StartingVertexId)).ToList();
            pirateMainland = mainland.Select(t => t.Id).ToHashSet();
            for (var i = 0; i < mainland.Count; i++)
                foreach (var tile in view.Board.Tiles.Where(t => t.Resource != "sea" && t.Resource != "fog" && !pirateMainland.Contains(t.Id) && t.Vertices.Intersect(mainland[i].Vertices).Count() >= 2))
                { pirateMainland.Add(tile.Id); mainland.Add(tile); }
        }
        var targets = view.Board.Vertices.Where(v => !buildings.Any(b => b.LocationId == v.Id)
            && !view.Board.Edges.Where(e => e.Vertices.Contains(v.Id)).Any(e => buildings.Any(b => e.Vertices.Contains(b.LocationId)))
            && view.Board.Tiles.Any(t=>t.Number.HasValue&&t.Vertices.Contains(v.Id))
            && (pirateMainland == null || v.Id == pirateBeachhead || view.Board.Tiles.Any(t => pirateMainland.Contains(t.Id) && t.Vertices.Contains(v.Id)))
            && !view.Board.Fortresses.Any(f => f.Strength > 0 && f.VertexId == v.Id))
            .SelectMany(v=>paths.Where(p=>p.Key.vertex==v.Id && p.Value.Count(x => x.StartsWith("S:")) <= me.ShipsRemaining + (removedShipId == null ? 0 : 1) && p.Value.Count(x => x.StartsWith("R:")) <= me.Pieces.Roads)
                .Select(p=>(target:v.Id,path:p.Value.Select(x=>x.Substring(2)).ToArray(),ship:p.Value.FirstOrDefault()?.StartsWith("S:")==true))).ToArray();
        if (targets.Length == 0) return null;
        return targets.OrderByDescending(t => SettlementValue(view, t.target, t.path.Length)).ThenBy(t => t.path.Length).ThenBy(t => t.target).First();
    }
}

/// <summary>从已授权的公开棋盘计算登岛收益与回本时间，供单扩展和组合 AI 共用。</summary>
internal static class SeafaringPlanning
{
    internal static int SettlementPoints(string scenarioId, Tile[] tiles, PiecePlacement[] ownBuildings, string target)
    {
        var bonus = scenarioId == "heading-for-new-shores" || scenarioId == "four-islands" || scenarioId == "through-the-desert" ? 2 :
            scenarioId == "wonders-of-catan" || scenarioId == "new-world" ? 1 : 0;
        if (bonus == 0) return 1;
        var land = tiles.Where(t => t.Resource != "sea" && t.Resource != "fog" && (scenarioId != "through-the-desert" || t.Resource != "desert")).ToArray();
        var visited = new HashSet<string>();
        var extra = 0;
        foreach (var start in land.Where(t => t.Vertices.Contains(target)))
        {
            if (!visited.Add(start.Id)) continue;
            var region = new List<Tile> { start };
            for (var i = 0; i < region.Count; i++)
                foreach (var next in land.Where(t => !visited.Contains(t.Id) && t.Vertices.Intersect(region[i].Vertices).Count() >= 2))
                { visited.Add(next.Id); region.Add(next); }
            if (!region.Any(t => ownBuildings.Any(b => t.Vertices.Contains(b.LocationId)))) extra += bonus;
        }
        return 1 + extra;
    }

    internal static double Production(Tile[] tiles, string vertex) => tiles.Where(t => t.Vertices.Contains(vertex)).Sum(t => Pips(t.Number) / 36.0);

    internal static double SettlementValue(string scenarioId, Tile[] tiles, Edge[] edges, PiecePlacement[] buildings, string playerId,
        string target, int pathLength, int remainingRolls, int ownPoints, int targetPoints)
    {
        var points = SettlementPoints(scenarioId, tiles, buildings.Where(b => b.PlayerId == playerId).ToArray(), target);
        if (pathLength == 0 && ownPoints + points >= targetPoints) return 10000 + points;
        // 每段路或船花两张牌，村庄花四张；按平均每轮一段施工预留四次掷骰的延迟。
        // 同分落点只有额外产出能偿还绕行费用时，才值得多造船追逐肥地。
        var payback = Production(tiles, target) * Math.Max(0, remainingRolls - pathLength * 4);
        return points * 12 + payback - (4 + pathLength * 2);
    }
}
}
