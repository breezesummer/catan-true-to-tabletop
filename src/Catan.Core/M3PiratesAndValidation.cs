using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Catan.Core.M3
{
    public sealed partial class SeafarersGameSession
    {
        private ShipPlacement[] InvasionShips(GameState s, string player)
        {
            var f = s.Fortresses.Single(x => x.PlayerId == player);
            var result = new List<ShipPlacement>(); var current = f.InvasionStartingVertexId;
            while (true)
            {
                var next = s.Ships.FirstOrDefault(x => x.PlayerId == player && x.IsInvasionRoute && !result.Contains(x) && EdgeAt(x.LocationId).Vertices.Contains(current));
                if (next == null) break;
                result.Add(next); current = EdgeAt(next.LocationId).Vertices.Single(x => x != current);
            }
            return result.ToArray();
        }
        private string InvasionEnd(GameState s, string player)
        {
            var current = s.Fortresses.Single(x => x.PlayerId == player).InvasionStartingVertexId;
            foreach (var ship in InvasionShips(s, player)) current = EdgeAt(ship.LocationId).Vertices.Single(x => x != current);
            return current;
        }
        private bool IsInvasionExtension(GameState s, string player, string edge)
        {
            var f = scenario.Fortresses.Single(x => x.PlayerId == player);
            var end = InvasionEnd(s, player);
            return EdgeAt(edge).Vertices.Contains(end) && end != f.VertexId;
        }
        private string[] LegalInvasionRoots(GameState s, string player)
        {
            if (scenario.Id != "pirate-islands" || s.Phase != GamePhase.Action || s.ActivePlayerId != player || s.TradeOffer != null) return Array.Empty<string>();
            if (s.Ships.Any(x => x.PlayerId == player && x.IsInvasionRoute && !EdgeTiles(s, x.LocationId).Any(t => scenario.StartingTileIds.Contains(t.Id)))) return Array.Empty<string>();
            return Buildings(s).Where(b => b.PlayerId == player && s.Tiles.Any(t => t.Vertices.Contains(b.LocationId) && scenario.StartingTileIds.Contains(t.Id)) && scenario.Edges.Any(e => e.Vertices.Contains(b.LocationId) && SeaEdge(s, e.Id))).Select(b => b.LocationId).Distinct().ToArray();
        }
        private void SelectInvasionRoot(GameState s, string player, string vertex)
        {
            var f = s.Fortresses.Single(x => x.PlayerId == player); f.InvasionStartingVertexId = vertex;
            foreach (var ship in s.Ships.Where(x => x.PlayerId == player)) ship.IsInvasionRoute = false;
            var dist = SeaDistances(s, f.BeachheadVertexId); var current = vertex;
            while (true)
            {
                var next = s.Ships.Where(x => x.PlayerId == player && !x.IsInvasionRoute && EdgeAt(x.LocationId).Vertices.Contains(current))
                    .FirstOrDefault(x => EdgeAt(x.LocationId).Vertices.Any(v => dist.ContainsKey(v) && dist.ContainsKey(current) && dist[v] == dist[current] - 1));
                if (next == null) break;
                next.IsInvasionRoute = true; current = EdgeAt(next.LocationId).Vertices.Single(v => v != current);
            }
        }
        private Dictionary<string, int> SeaDistances(GameState s, string start)
        {
            var result = new Dictionary<string, int> { [start] = 0 }; var q = new Queue<string>(); q.Enqueue(start);
            while (q.Count > 0)
            {
                var v = q.Dequeue();
                foreach (var edge in scenario.Edges.Where(e => e.Vertices.Contains(v) && SeaEdge(s, e.Id)))
                    foreach (var next in edge.Vertices) if (!result.ContainsKey(next)) { result[next] = result[v] + 1; q.Enqueue(next); }
            }
            return result;
        }
        private void ValidatePirateRoute(GameState s, PlayerState p, string edge)
        {
            var f = scenario.Fortresses.Single(x => x.PlayerId == p.Id);
            var path = InvasionShips(s, p.Id); var end = InvasionEnd(s, p.Id);
            var vertices = EdgeAt(edge).Vertices;
            if (IsInvasionExtension(s, p.Id, edge))
            {
                var passedBeachhead = path.Any(ship => EdgeAt(ship.LocationId).Vertices.Contains(f.BeachheadVertexId));
                var goal = passedBeachhead ? f.VertexId : f.BeachheadVertexId;
                var dist = SeaDistances(s, goal); var other = vertices.Single(v => v != end);
                Require(dist.ContainsKey(end) && dist.ContainsKey(other) && dist[other] == dist[end] - 1, "Detour", "前往滩头与要塞的航线必须沿最短路径延伸。");
                Require(!path.Any(ship => EdgeAt(ship.LocationId).Vertices.Contains(other)), "RouteLoop", "进攻航线不能形成回路。");
            }
            else
            {
                // Additional routes are allowed around the eastern main island, never western branches.
                Require(EdgeTiles(s, edge).Any(t => scenario.StartingTileIds.Contains(t.Id)), "InvasionBranch", "海盗群岛只允许一条不分叉的西行航线。");
            }
        }
        private void ConvertWarship(GameState s, PlayerState p)
        {
            var ship = InvasionShips(s, p.Id).FirstOrDefault(x => !x.IsWarship);
            Require(ship != null, "NoNormalShip", "进攻航线没有可升级的普通船。");
            PlayCard(s, p, DevelopmentCardKind.Knight); p.PlayedKnights++; ship.IsWarship = true;
        }
        private bool CanAttackFortress(GameState s, string player)
        {
            var f = s.Fortresses.Single(x => x.PlayerId == player);
            return f.Strength > 0 && InvasionEnd(s, player) == f.VertexId;
        }
        private void AttackFortress(GameState s, PlayerState p)
        {
            var f = s.Fortresses.Single(x => x.PlayerId == p.Id); var ships = InvasionShips(s, p.Id);
            var power = ships.Count(x => x.IsWarship); var die = Draw(s.Random, 6) + 1; s.LastPirateStrength = die;
            if (power > die)
            {
                f.Strength--;
                if (f.Strength == 0) { s.Settlements = Append(s.Settlements, Placement(f.VertexId, p.Id)); if (s.Fortresses.All(x => x.Strength == 0)) s.PirateTileId = null; }
            }
            else
            {
                var loss = ships.Reverse().Take(power == die ? 1 : 2).Select(x => x.LocationId).ToArray();
                s.Ships = s.Ships.Where(x => !loss.Contains(x.LocationId)).ToArray(); p.ShipsRemaining += loss.Length;
            }
        }

        private void AssertSeafarersInvariants(GameState s)
        {
            void Check(bool valid, string message) { if (!valid) throw new InvalidDataException(message); }
            Check(s.Tiles != null && s.Tiles.Length == scenario.Tiles.Length && s.Tiles.Select(t => t.Id).SequenceEqual(scenario.Tiles.Select(t => t.Id)), "Tile identity changed.");
            Check(s.Tiles.All(t => t.Resource == "sea" || t.Resource == "fog" || t.Resource == "desert" || t.Resource == "gold" || Resources.Any(r => ResourceName(r) == t.Resource)), "Invalid terrain.");
            Check(s.HiddenResources.Length == s.Tiles.Count(t => t.Resource == "fog"), "Fog conservation failed.");
            Check(s.HiddenResources.Count(r => r != "sea") == s.HiddenNumbers.Length, "Fog number conservation failed.");
            Check(s.RobberTileId == null ? scenario.Id == "pirate-islands" : s.RobberTileId == "frame" && scenario.Id == "new-world" || s.Tiles.Any(t => t.Id == s.RobberTileId && IsLand(t)), "Invalid robber location.");
            Check(s.PirateTileId == null || s.PirateTileId == "frame" || s.Tiles.Any(t => t.Id == s.PirateTileId && t.Resource == "sea"), "Invalid pirate location.");
            foreach (var p in s.Players)
            {
                Check(p.ShipsRemaining >= 0 && p.ShipsRemaining + s.Ships.Count(x => x.PlayerId == p.Id) == 15, "Ship conservation failed.");
                Check(p.Cloth >= 0 && p.BonusVictoryPoints >= 0 && p.WonderLevel >= 0 && p.WonderLevel <= 4, "Invalid scenario counters.");
                Check(p.WonderId == null ? p.WonderLevel == 0 : scenario.Wonders.Any(w => w.Id == p.WonderId), "Invalid wonder.");
            }
            Check(s.Players.Where(p => p.WonderId != null).Select(p => p.WonderId).Distinct().Count() == s.Players.Count(p => p.WonderId != null), "Duplicate wonder claim.");
            foreach (var ship in s.Ships)
                Check(s.Players.Any(p => p.Id == ship.PlayerId) && scenario.Edges.Any(e => e.Id == ship.LocationId) && ship.BuiltTurn >= 0 && ship.BuiltTurn <= s.Turn, "Invalid ship.");
            Check(s.ClothSupply >= 0 && s.Villages.All(v => v.Cloth >= 0 && v.Cloth <= 5 && v.TradingPlayerIds.Distinct().Count() == v.TradingPlayerIds.Length && v.TradingPlayerIds.All(id => s.Players.Any(p => p.Id == id))), "Invalid cloth ledger.");
            Check(s.ClothSupply + s.Villages.Sum(v => v.Cloth) + s.Players.Sum(p => p.Cloth) == (scenario.Villages.Length > 0 ? 50 : 0), "Cloth conservation failed.");
            Check(s.Fortresses.All(f => f.Strength >= 0 && f.Strength <= 3 && s.Players.Any(p => p.Id == f.PlayerId)), "Invalid fortress.");
            Check(s.GiftCards.All(g => scenario.GiftCardEdgeIds.Contains(g.EdgeId)) && s.BonusEdgeIds.All(scenario.BonusEdgeIds.Contains), "Invalid island gift.");
            var allPorts = s.Ports.Concat(s.GiftPorts).Concat(s.UnplacedPorts).Concat(s.Players.SelectMany(p => p.HeldPorts)).ToArray();
            Check(allPorts.Length == scenario.Ports.Length + scenario.GiftPorts.Length && allPorts.Select(p => p.Id).Distinct().Count() == allPorts.Length, "Port conservation failed.");
            bool needsDecision = s.Phase == GamePhase.SetupPort || s.Phase == GamePhase.SetupRoad || s.Phase == GamePhase.Discard || s.Phase == GamePhase.RobberMove || s.Phase == GamePhase.RobberSteal || s.Phase == GamePhase.RoadBuilding || s.Phase == GamePhase.GoldChoice || s.Phase == GamePhase.PortPlacement;
            Check(needsDecision == (s.PendingDecision != null), "Invalid pending decision.");
            Check((s.Phase == GamePhase.GoldChoice) == (s.GoldClaims.Length > 0), "Gold pending mismatch.");
            Check(s.GoldClaims.All(g => g.Amount > 0 && s.Players.Any(p => p.Id == g.PlayerId)) && s.GoldClaims.Select(g => g.PlayerId).Distinct().Count() == s.GoldClaims.Length, "Invalid gold claims.");
            Check((s.Phase == GamePhase.Discard) == (s.Discards.Length > 0), "Discard phase mismatch.");
            Check(s.Discards.All(d => d.Amount > 0 && s.Players.Any(p => p.Id == d.PlayerId && p.Resources.Total / 2 == d.Amount)), "Invalid discard obligation.");
            if (s.PendingDecision != null)
            {
                Check(s.PendingDecision.PlayerId == (s.Phase == GamePhase.GoldChoice ? s.GoldClaims[0].PlayerId : s.ActivePlayerId), "Pending actor mismatch.");
                if (s.Phase == GamePhase.SetupRoad) Check(s.Settlements.Any(b => b.PlayerId == s.ActivePlayerId && b.LocationId == s.PendingDecision.AnchorVertexId), "Invalid setup anchor.");
                if (s.Phase == GamePhase.RoadBuilding) Check(s.PendingDecision.RemainingRoads >= 1 && s.PendingDecision.RemainingRoads <= 2, "Invalid free route count.");
                if (s.Phase == GamePhase.RobberSteal) Check(s.PendingDecision.EligibleVictimIds.Length > 0 && s.PendingDecision.EligibleVictimIds.All(id => id != s.ActivePlayerId && s.Players.Any(p => p.Id == id)), "Invalid theft targets.");
            }
        }
    }
}
