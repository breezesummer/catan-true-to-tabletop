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
        var actor = view.TradeOffer != null ? view.TradeOffer.OtherPlayerId : view.Phase == GamePhase.GoldChoice ? view.GoldClaims.First().PlayerId : view.PendingDecision?.PlayerId ?? view.ActivePlayerId;
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
                return Make(CommandKind.SetupSettlement, view.LegalVertexIds.OrderByDescending(v => Value(view, v)).ThenBy(v => v).First());
            case GamePhase.SetupRoad:
                return view.LegalEdgeIds.Length > 0 ? Make(CommandKind.SetupRoad, BestRoad(view, view.LegalEdgeIds)) : Make(CommandKind.SetupShip, view.LegalShipEdgeIds.First());
            case GamePhase.GoldChoice:
                var gold = Make(CommandKind.ChooseGoldResources); gold.Resources = Bag();
                for (var i = 0; i < Math.Min(view.GoldClaims.First(g => g.PlayerId == view.PlayerId).Amount, view.Bank.Total); i++)
                { var r = Resources.Where(r => view.Bank[r] > gold.Resources[r]).OrderBy(r => view.OwnResources[r] + gold.Resources[r]).First(); gold.Resources[r]++; }
                return gold;
            case GamePhase.PortPlacement:
                var port = Make(CommandKind.PlacePort, view.LegalPortEdgeIds.First()); port.SourceId = view.OwnHeldPorts.First().Id; return port;
            case GamePhase.Discard:
                var cards = new ResourceBag(); var remaining = view.Discards.Single(d => d.PlayerId == view.PlayerId).Amount;
                while (remaining-- > 0)
                { var r = Resources.OrderByDescending(r => view.OwnResources[r] - cards[r]).First(); cards[r]++; }
                var discard = Make(CommandKind.DiscardResources); discard.Resources = cards; return discard;
            case GamePhase.RobberMove:
                var destination = view.Board.Tiles.Where(t => t.Id != view.Board.RobberTileId && t.Resource != "sea" && t.Resource != "fog"
                    && (view.ScenarioId != "forgotten-tribe" || t.Number.HasValue)
                    && (view.ScenarioId != "cloth-for-catan" || !view.Board.Villages.Any(v => t.Vertices.Contains(v.VertexId)))).OrderByDescending(t =>
                    Buildings(view).Where(b => t.Vertices.Contains(b.LocationId)).Sum(b => b.PlayerId == view.PlayerId ? -100 : 10 + view.Players.Single(p => p.Id == b.PlayerId).ResourceCount)).First();
                return Make(CommandKind.MoveRobber, destination.Id);
            case GamePhase.RobberSteal:
                var theft = Make(CommandKind.StealResource); theft.OtherPlayerId = view.PendingDecision.EligibleVictimIds.OrderByDescending(id => view.Players.Single(p => p.Id == id).ResourceCount).First(); return theft;
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
        if (view.LegalCityVertexIds.Length > 0)
            return Make(CommandKind.BuildCity, view.LegalCityVertexIds.OrderByDescending(v => Value(view, v)).First());
        if (view.LegalVertexIds.Length > 0)
            return Make(CommandKind.BuildSettlement, view.LegalVertexIds.OrderByDescending(v => Value(view, v)).First());
        var expansion = Expansion(view);
        var me = view.Players.Single(p => p.Id == view.PlayerId);
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
        if (me.Pieces.Cities > 0 && view.Board.Settlements.Any(b => b.PlayerId == view.PlayerId)
            && (view.OwnResources.Ore >= 2 || me.Pieces.Settlements == 0 || expansion == null))
        { goal = City; goalKind = CommandKind.BuildCity; target = view.Board.Settlements.Where(b => b.PlayerId == view.PlayerId).OrderByDescending(b => Value(view, b.LocationId)).First().LocationId; }
        else if (me.Pieces.Settlements > 0 && expansion != null)
        { goal = expansion.Value.path.Length == 0 ? Settlement : expansion.Value.ship ? Ship : Road; goalKind = expansion.Value.path.Length == 0 ? CommandKind.BuildSettlement : expansion.Value.ship ? CommandKind.BuildShip : CommandKind.BuildRoad; target = expansion.Value.path.Length == 0 ? expansion.Value.target : expansion.Value.path[0]; }
        else { goal = Development; goalKind = CommandKind.BuyDevelopmentCard; }

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
        if (view.DevelopmentDeckCount > 0 && Covers(view.OwnResources, Development)) return Make(CommandKind.BuyDevelopmentCard);
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
    // Reuse an idle open end only when the destination is visibly more productive.
    // MovableShipEdgeIds supplies source permissions; connectivity is checked after removal.
    private static Command RepositionShip(PlayerView view)
    {
        if (view.ShipMovedThisTurn || view.ScenarioId == "pirate-islands") return null;
        foreach (var targetId in view.LegalShipEdgeIds.OrderByDescending(id => view.Board.Edges.Single(e => e.Id == id).Vertices.Max(v => Value(view, v))))
        {
            var target = view.Board.Edges.Single(e => e.Id == targetId);
            var targetValue = target.Vertices.Max(v => Value(view, v));
            foreach (var sourceId in view.MovableShipEdgeIds)
            {
                var source = view.Board.Edges.Single(e => e.Id == sourceId);
                if (targetValue <= source.Vertices.Max(v => Value(view, v)) + 2) continue;
                bool connected = target.Vertices.Any(vertex =>
                    Buildings(view).Any(b => b.PlayerId == view.PlayerId && b.LocationId == vertex) ||
                    !Buildings(view).Any(b => b.PlayerId != view.PlayerId && b.LocationId == vertex) &&
                    view.Board.Ships.Any(s => s.PlayerId == view.PlayerId && s.LocationId != sourceId && view.Board.Edges.Single(e => e.Id == s.LocationId).Vertices.Contains(vertex)));
                if (connected) return new Command { PlayerId=view.PlayerId, Kind=CommandKind.MoveShip, SourceId=sourceId, TargetId=targetId };
            }
        }
        return null;
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
    private static (string target, string[] path, bool ship)? Expansion(PlayerView view)
    {
        var buildings = Buildings(view);
        var ownRoads = view.Board.Roads.Where(r => r.PlayerId == view.PlayerId).Select(r => r.LocationId).ToHashSet();
        var ownShips = view.Board.Ships.Where(r => r.PlayerId == view.PlayerId).Select(r => r.LocationId).ToHashSet();
        var occupiedEdges = view.Board.Roads.Select(r=>r.LocationId).Concat(view.Board.Ships.Select(s=>s.LocationId)).ToHashSet();
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
        var targets = view.Board.Vertices.Where(v => !buildings.Any(b => b.LocationId == v.Id)
            && !view.Board.Edges.Where(e => e.Vertices.Contains(v.Id)).Any(e => buildings.Any(b => e.Vertices.Contains(b.LocationId)))
            && view.Board.Tiles.Any(t=>t.Number.HasValue&&t.Vertices.Contains(v.Id)))
            .SelectMany(v=>paths.Where(p=>p.Key.vertex==v.Id).Select(p=>(target:v.Id,path:p.Value.Select(x=>x.Substring(2)).ToArray(),ship:p.Value.FirstOrDefault()?.StartsWith("S:")==true))).ToArray();
        if (targets.Length == 0) return null;
        return targets.OrderByDescending(t => Value(view, t.target) - t.path.Length * 12).ThenBy(t => t.target).First();
    }
}
}
