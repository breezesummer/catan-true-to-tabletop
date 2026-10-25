using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace Catan.Core.M5 { public sealed partial class CombinedGameSession {
private void Discover(GameState s, string player, string edge)
        {
            if (seafaring.Id != "fog-islands") return;
            var e = EdgeAt(edge); var p = s.Players.Single(x => x.Id == player);
            foreach (var tile in s.Tiles.Where(t => t.Resource == "fog" && t.Vertices.Any(e.Vertices.Contains)).OrderBy(t => t.Id))
            {
                Require(s.Special.HiddenResources.Length > 0, "EmptyExplorationDeck", "探索地块已耗尽。");
                tile.Resource = s.Special.HiddenResources[0]; s.Special.HiddenResources = s.Special.HiddenResources.Skip(1).ToArray();
                if (tile.Resource != "sea")
                {
                    var n = Draw(s.Random, s.Special.HiddenNumbers.Length); tile.Number = s.Special.HiddenNumbers[n]; s.Special.HiddenNumbers = s.Special.HiddenNumbers.Where((v, i) => i != n).ToArray();
                    if (tile.Resource == "gold") AddGold(s, player, 1);
                    else if (s.Bank[ParseResource(tile.Resource)] > 0) Transfer(s.Bank, p.Resources, ParseResource(tile.Resource), 1);
                }
            }
        }
private HashSet<string> ReachByShips(GameState s, string player, IEnumerable<string> starts)
        {
            var seen = new HashSet<string>(starts); var queue = new Queue<string>(seen);
            while (queue.Count > 0)
            {
                var v = queue.Dequeue();
                if (Blocked(s, player, v)) continue;
                foreach (var ship in s.Ships.Where(x => x.PlayerId == player && EdgeAt(x.LocationId).Vertices.Contains(v)))
                    foreach (var end in EdgeAt(ship.LocationId).Vertices) if (seen.Add(end)) queue.Enqueue(end);
            }
            return seen;
        }
private void ConnectVillages(GameState s)
        {
            foreach (var p in s.Players)
            {
                var reach = ReachByShips(s, p.Id, Buildings(s).Where(b => b.PlayerId == p.Id).Select(b => b.LocationId));
                foreach (var village in s.Special.Villages.Where(v => !v.TradingPlayerIds.Contains(p.Id) && reach.Contains(v.VertexId)))
                { village.TradingPlayerIds = Append(village.TradingPlayerIds, p.Id); if (village.Cloth > 0) { village.Cloth--; p.Cloth++; } }
            }
        }
private string[] LegalPortEdges(GameState s, string player)
        {
            bool setup = s.Phase == GamePhase.SetupPort && s.ActivePlayerId == player;
            if (!setup && !s.Players.Any(p => p.Id == player && s.Special.HeldPorts.Any(h => h.PlayerId == p.Id))) return Array.Empty<string>();
            return seafaring.Edges.Where(e => SeaEdge(s, e.Id) && EdgeTiles(s, e.Id).Any(IsLand) && (setup || Buildings(s).Any(b => b.PlayerId == player && e.Vertices.Contains(b.LocationId))) && !s.Special.Ports.Any(p => p.Vertices.Any(e.Vertices.Contains))).Select(e => e.Id).ToArray();
        }
private bool WonderRequirement(GameState s, PlayerState p, M3.WonderDefinition w)
        {
            switch (w.Requirement)
            {
                case "two-cities": return s.Cities.Count(b => b.PlayerId == p.Id) >= 2;
                case "city-six-vp": return s.Cities.Any(b => b.PlayerId == p.Id) && VictoryPoints(s, p.Id, true) >= 6;
                case "port-city-route-five": return s.Cities.Any(b => b.PlayerId == p.Id && s.Special.Ports.Any(port => port.Vertices.Contains(b.LocationId))) && LongestRoad(s, p.Id) >= 5;
                case "marker-building": return Buildings(s).Any(b => b.PlayerId == p.Id && w.VertexIds.Contains(b.LocationId));
                default: throw new InvalidDataException("Unknown wonder requirement: " + w.Requirement);
            }
        }
private ShipPlacement[] InvasionShips(GameState s, string player)
        {
            var f = s.Special.Fortresses.Single(x => x.PlayerId == player);
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
            var current = s.Special.Fortresses.Single(x => x.PlayerId == player).InvasionStartingVertexId;
            foreach (var ship in InvasionShips(s, player)) current = EdgeAt(ship.LocationId).Vertices.Single(x => x != current);
            return current;
        }
private bool IsInvasionExtension(GameState s, string player, string edge)
        {
            var f = seafaring.Fortresses.Single(x => x.PlayerId == player);
            var end = InvasionEnd(s, player);
            return EdgeAt(edge).Vertices.Contains(end) && end != f.VertexId;
        }
private string[] LegalInvasionRoots(GameState s, string player)
        {
            if (seafaring.Id != "pirate-islands" || s.Phase != GamePhase.Action || s.ActivePlayerId != player || s.TradeOffer != null) return Array.Empty<string>();
            if (s.Ships.Any(x => x.PlayerId == player && x.IsInvasionRoute && !EdgeTiles(s, x.LocationId).Any(t => seafaring.StartingTileIds.Contains(t.Id)))) return Array.Empty<string>();
            return Buildings(s).Where(b => b.PlayerId == player && s.Tiles.Any(t => t.Vertices.Contains(b.LocationId) && seafaring.StartingTileIds.Contains(t.Id)) && seafaring.Edges.Any(e => e.Vertices.Contains(b.LocationId) && SeaEdge(s, e.Id))).Select(b => b.LocationId).Distinct().ToArray();
        }
private void SelectInvasionRoot(GameState s, string player, string vertex)
        {
            var f = s.Special.Fortresses.Single(x => x.PlayerId == player); f.InvasionStartingVertexId = vertex;
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
                foreach (var edge in seafaring.Edges.Where(e => e.Vertices.Contains(v) && SeaEdge(s, e.Id)))
                    foreach (var next in edge.Vertices) if (!result.ContainsKey(next)) { result[next] = result[v] + 1; q.Enqueue(next); }
            }
            return result;
        }
private void ValidatePirateRoute(GameState s, PlayerState p, string edge)
        {
            var f = seafaring.Fortresses.Single(x => x.PlayerId == p.Id);
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
                Require(EdgeTiles(s, edge).Any(t => seafaring.StartingTileIds.Contains(t.Id)), "InvasionBranch", "海盗群岛只允许一条不分叉的西行航线。");
            }
        }
private bool CanAttackFortress(GameState s, string player)
        {
            var f = s.Special.Fortresses.Single(x => x.PlayerId == player);
            return f.Strength > 0 && InvasionEnd(s, player) == f.VertexId;
        }
private void AttackFortress(GameState s, PlayerState p)
        {
            var f = s.Special.Fortresses.Single(x => x.PlayerId == p.Id); var ships = InvasionShips(s, p.Id);
            var power = ships.Count(x => x.IsWarship); var die = Draw(s.Random, 6) + 1; s.Special.LastPirateStrength = die;
            if (power > die)
            {
                f.Strength--;
                if (f.Strength == 0) { s.Settlements = Append(s.Settlements, Placement(f.VertexId, p.Id)); if (s.Special.Fortresses.All(x => x.Strength == 0)) s.PirateTileId = null; }
            }
            else if(!B3Immune(s,p.Id))
            {
                var loss = ships.Reverse().Take(power == die ? 1 : 2).Select(x => x.LocationId).ToArray();
                s.Ships = s.Ships.Where(x => !loss.Contains(x.LocationId)).ToArray(); p.ShipsRemaining += loss.Length;
                foreach(var location in loss)B5ForgetAsset(s,"Ship",location);
            }
        }
} }
