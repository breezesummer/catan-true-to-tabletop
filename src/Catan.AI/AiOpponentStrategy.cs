using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core;

namespace Catan.AI
{
// 只从投影视图估算近期得分机会，不猜具体隐藏卡。步数是保守的建设距离，
// 一步可赢时即使尚缺材料也拒绝资助；岛屿奖励、大都会和最长路不能按一分一动作计算。
internal static class AiOpponentStrategy
{
    private sealed class Seat
    {
        internal string Id;
        internal int Points, Length, Cities, Villages, Roads, Ships, WonderLevel, PlayedKnights, DevelopmentCards, BasicKnights;
        internal int[] Levels = Array.Empty<int>();
    }
    private sealed class Guard
    {
        internal string Player, Vertex;
        internal bool Ready;
    }
    private sealed class Position
    {
        internal string Scenario, Holder, PirateTile, ArmyHolder;
        internal int Target;
        internal Tile[] Tiles;
        internal Edge[] Edges;
        internal PiecePlacement[] Buildings, Cities, Routes, Ships, Knights;
        internal Seat[] Seats;
        internal Guard[] Guards = Array.Empty<Guard>();
        internal string[] MetropolisOwners = Array.Empty<string>();
        internal string[] MetropolisVertices = Array.Empty<string>();
    }
    private static Position Read(Catan.Core.M2.PlayerView v) => new Position
    {
        Scenario = null, Holder = v.LongestRoadPlayerId, Target = v.TargetVictoryPoints, ArmyHolder = v.LargestArmyPlayerId,
        Tiles = v.Board.Tiles, Edges = v.Board.Edges,
        Buildings = v.Board.Settlements.Concat(v.Board.Cities).ToArray(), Cities = v.Board.Cities,
        Routes = v.Board.Roads, Ships = Array.Empty<PiecePlacement>(),
        Knights = Array.Empty<PiecePlacement>(),
        Seats = v.Players.Select(p => new Seat { Id = p.Id, Points = p.VictoryPoints, Length = p.LongestRoadLength,
            Cities = p.Pieces.Cities, Villages = p.Pieces.Settlements, Roads = p.Pieces.Roads, PlayedKnights = p.PlayedKnights, DevelopmentCards = p.DevelopmentCardCount }).ToArray()
    };
    internal static int MinimumWinningActions(Catan.Core.M2.PlayerView v, string player) => Steps(Read(v), player);
    internal static double Threat(Catan.Core.M2.PlayerView v, string player) => Threat(Read(v), player);
    internal static double TradePenalty(Catan.Core.M2.PlayerView v, string player) => TradePenalty(Read(v), player);
    private static Position Read(Catan.Core.M3.PlayerView v) => new Position
    {
        Scenario = v.ScenarioId, Holder = v.LongestRoadPlayerId, Target = v.TargetVictoryPoints, PirateTile = v.Board.PirateTileId, ArmyHolder = v.LargestArmyPlayerId,
        Tiles = v.Board.Tiles, Edges = v.Board.Edges,
        Buildings = v.Board.Settlements.Concat(v.Board.Cities).ToArray(), Cities = v.Board.Cities,
        Routes = v.Board.Roads, Ships = v.Board.Ships.Select(x => new PiecePlacement { PlayerId = x.PlayerId, LocationId = x.LocationId }).ToArray(),
        Knights = Array.Empty<PiecePlacement>(),
        Seats = v.Players.Select(p => new Seat { Id = p.Id, Points = p.VictoryPoints, Length = p.LongestRoadLength,
            Cities = p.Pieces.Cities, Villages = p.Pieces.Settlements, Roads = p.Pieces.Roads, Ships = p.ShipsRemaining, WonderLevel = p.WonderLevel, PlayedKnights = p.PlayedKnights, DevelopmentCards = p.DevelopmentCardCount }).ToArray()
    };
    internal static int MinimumWinningActions(Catan.Core.M3.PlayerView v, string player) => Steps(Read(v), player);
    internal static double Threat(Catan.Core.M3.PlayerView v, string player) => Threat(Read(v), player);
    internal static double TradePenalty(Catan.Core.M3.PlayerView v, string player) => TradePenalty(Read(v), player);
    private static Position Read(Catan.Core.M4.PlayerView v) => new Position
    {
        Scenario = null, Holder = v.LongestRoadPlayerId, Target = v.TargetVictoryPoints,
        Tiles = v.Board.Tiles, Edges = v.Board.Edges,
        Buildings = v.Board.Settlements.Concat(v.Board.Cities).ToArray(), Cities = v.Board.Cities,
        Routes = v.Board.Roads, Ships = Array.Empty<PiecePlacement>(),
        Knights = v.Board.Knights.Select(x => new PiecePlacement { PlayerId = x.PlayerId, LocationId = x.LocationId }).ToArray(),
        Seats = v.Players.Select(p => new Seat { Id = p.Id, Points = p.VictoryPoints, Length = p.LongestRoadLength,
            Cities = p.Pieces.Cities, Villages = p.Pieces.Settlements, Roads = p.Pieces.Roads, Levels = p.Improvements, BasicKnights = p.KnightSupply[0] }).ToArray(),
        Guards = v.Board.Knights.Select(k => new Guard { Player = k.PlayerId, Vertex = k.LocationId, Ready = k.Active && k.ActivatedTurn != v.Turn }).ToArray(),
        MetropolisOwners = Enumerable.Range(0, 3).Select(t => v.Board.Metropolises.FirstOrDefault(m => (int)m.Track == t)?.PlayerId).ToArray(),
        MetropolisVertices = v.Board.Metropolises.Select(m => m.LocationId).ToArray()
    };
    internal static int MinimumWinningActions(Catan.Core.M4.PlayerView v, string player) => Steps(Read(v), player);
    internal static double Threat(Catan.Core.M4.PlayerView v, string player) => Threat(Read(v), player);
    internal static double TradePenalty(Catan.Core.M4.PlayerView v, string player) => TradePenalty(Read(v), player);
    private static Position Read(Catan.Core.M5.PlayerView v) => new Position
    {
        Scenario = v.ScenarioId, Holder = v.LongestRoadPlayerId, Target = v.TargetVictoryPoints, PirateTile = v.Board.PirateTileId,
        Tiles = v.Board.Tiles, Edges = v.Board.Edges,
        Buildings = v.Board.Settlements.Concat(v.Board.Cities).ToArray(), Cities = v.Board.Cities,
        Routes = v.Board.Roads, Ships = v.Board.Ships.Select(x => new PiecePlacement { PlayerId = x.PlayerId, LocationId = x.LocationId }).ToArray(),
        Knights = v.Board.Knights.Select(x => new PiecePlacement { PlayerId = x.PlayerId, LocationId = x.LocationId }).ToArray(),
        Seats = v.Players.Select(p => new Seat { Id = p.Id, Points = p.VictoryPoints, Length = p.LongestRoadLength,
            Cities = p.Pieces.Cities, Villages = p.Pieces.Settlements, Roads = p.Pieces.Roads, Ships = p.ShipsRemaining, Levels = p.Improvements, BasicKnights = p.KnightSupply[0] }).ToArray(),
        Guards = v.Board.Knights.Select(k => new Guard { Player = k.PlayerId, Vertex = k.LocationId, Ready = k.Active && k.ActivatedTurn != v.Turn }).ToArray(),
        MetropolisOwners = Enumerable.Range(0, 3).Select(t => v.Board.Metropolises.FirstOrDefault(m => (int)m.Track == t)?.PlayerId).ToArray(),
        MetropolisVertices = v.Board.Metropolises.Select(m => m.LocationId).ToArray()
    };
    internal static int MinimumWinningActions(Catan.Core.M5.PlayerView v, string player) => Steps(Read(v), player);
    internal static double Threat(Catan.Core.M5.PlayerView v, string player) => Threat(Read(v), player);
    internal static double TradePenalty(Catan.Core.M5.PlayerView v, string player) => TradePenalty(Read(v), player);

    internal static bool CanSupply(OpponentHandView[] hands, string player, Resource resource) =>
        hands.FirstOrDefault(h => h.PlayerId == player) is var hand && (hand == null || hand.Resources[resource] > 0);

    private static double TradePenalty(Position v, string player)
    {
        var p = v.Seats.Single(s => s.Id == player);
        // “领先六分”比较次高分，不能误写为总分六分，或只领先某一名落后者六分。
        if (p.Points - v.Seats.Where(s => s.Id != player).Max(s => s.Points) >= 6) return double.PositiveInfinity;
        int steps = Steps(v, player);
        return steps <= 1 ? double.PositiveInfinity : steps == 2 ? 12 : steps == 3 ? 6 : 0;
    }
    private static double Threat(Position v, string player)
    {
        var p = v.Seats.Single(s => s.Id == player);
        int steps = Steps(v, player);
        return p.Points + (steps <= 1 ? 100 : steps == 2 ? 55 : steps == 3 ? 25 : 0);
    }
    private static int Steps(Position v, string player)
    {
        var p = v.Seats.Single(s => s.Id == player);
        int gap = Math.Max(0, v.Target - p.Points);
        // 奇观四级直接获胜；海盗群岛堡垒和奇观分数胜利的附加条件仍作保守风险估算。
        int best = v.Scenario == "wonders-of-catan" && p.WonderLevel > 0 ? Math.Min(gap, 4 - p.WonderLevel) : gap;
        if (best <= 1) return best;
        if (gap > 5) return best;
        // 基础发展卡具体内容仍隐藏；已有足够出骑记录且手中有卡时，保守防范下一张骑士夺取两分。
        if (v.Scenario != "pirate-islands" && p.DevelopmentCards > 0 && v.ArmyHolder != player && p.PlayedKnights + 1 >= 3 &&
            p.PlayedKnights + 1 > v.Seats.Where(s => s.Id != player).Max(s => s.PlayedKnights))
            best = Math.Min(best, Math.Max(1, gap - 1));
        // 公开的改良等级足以识别下一次改良夺取两分的威胁，不能假定持有某张发展卡。
        if (v.Cities.Any(c => c.PlayerId == player && !v.MetropolisVertices.Contains(c.LocationId)))
            for (int track = 0; track < p.Levels.Length; track++)
            {
                if (p.Levels[track] < 3 || p.Levels[track] >= 5) continue;
                var holder = v.MetropolisOwners.ElementAtOrDefault(track);
                if (holder == player) continue;
                int rival = v.Seats.Where(s => s.Id != player).Max(s => s.Levels[track]);
                if (p.Levels[track] + 1 > rival) best = Math.Min(best, Math.Max(1, gap - 1));
            }
        if (p.Villages > 0 && v.Scenario != null)
        {
            var own = v.Buildings.Where(b => b.PlayerId == player).ToArray();
            var enemy = v.Buildings.Concat(v.Knights).Where(b => b.PlayerId != player).Select(b => b.LocationId).ToHashSet();
            var ownRoutes = v.Routes.Concat(v.Ships).Where(r => r.PlayerId == player).Select(r => r.LocationId).ToHashSet();
            var connected = v.Edges.Where(e => ownRoutes.Contains(e.Id)).SelectMany(e => e.Vertices).Where(x => !enemy.Contains(x)).ToHashSet();
            foreach (var vertex in connected)
            {
                if (v.Buildings.Concat(v.Knights).Any(b => b.LocationId == vertex) ||
                    v.Edges.Where(e => e.Vertices.Contains(vertex)).Any(e => v.Buildings.Any(b => e.Vertices.Contains(b.LocationId)))) continue;
                int gain = SeafaringPlanning.SettlementPoints(v.Scenario, v.Tiles, own, vertex);
                best = Math.Min(best, Math.Max(1, gap - gain + 1));
            }
        }
        if (v.Scenario != "cloth-for-catan" && v.Scenario != "pirate-islands" && v.Holder != player &&
            v.Routes.Concat(v.Ships).Count(r => r.PlayerId == player) >= 4)
        {
            // 一条边可能连接两段线路；只按线路总数剪枝，再计算真实长度，不能用当前最长长度加一。
            var occupied = v.Routes.Concat(v.Ships).Select(r => r.LocationId).ToHashSet();
            var owned = v.Routes.Concat(v.Ships).Where(r => r.PlayerId == player).Select(r => r.LocationId).ToHashSet();
            var blocked = v.Buildings.Concat(v.Knights).Where(b => b.PlayerId != player).Select(b => b.LocationId).ToHashSet();
            var starts = v.Edges.Where(e => owned.Contains(e.Id)).SelectMany(e => e.Vertices).Where(x => !blocked.Contains(x)).ToHashSet();
            int rival = v.Seats.Where(s => s.Id != player).Max(s => s.Length);
            foreach (var edge in v.Edges.Where(e => !occupied.Contains(e.Id) && e.Vertices.Any(starts.Contains)))
            {
                var adjacent = v.Tiles.Where(t => edge.Vertices.All(t.Vertices.Contains)).ToArray();
                bool land = p.Roads > 0 && adjacent.Any(t => t.Resource != "sea" && t.Resource != "fog");
                bool sea = p.Ships > 0 && adjacent.Any(t => t.Resource == "sea") && !adjacent.Any(t => t.Id == v.PirateTile);
                bool joins = edge.Vertices.Any(x => !blocked.Contains(x) &&
                    (v.Buildings.Any(b => b.PlayerId == player && b.LocationId == x) ||
                    v.Edges.Where(e => e.Vertices.Contains(x)).Any(e =>
                        land && v.Routes.Any(r => r.PlayerId == player && r.LocationId == e.Id) ||
                        sea && v.Ships.Any(r => r.PlayerId == player && r.LocationId == e.Id))));
                if (!joins) continue;
                var routes = v.Edges.Where(e => owned.Contains(e.Id)).Append(edge).ToArray();
                var shipIds = v.Ships.Where(r => r.PlayerId == player).Select(r => r.LocationId).ToHashSet();
                if (land && RouteLength(v, player, routes, shipIds, blocked) >= Math.Max(5, rival + 1) ||
                    sea && WithShip()) { best = Math.Min(best, Math.Max(1, gap - 1)); break; }
                bool WithShip() { shipIds.Add(edge.Id); return RouteLength(v, player, routes, shipIds, blocked) >= Math.Max(5, rival + 1); }
            }
        }
        if (v.Scenario != "cloth-for-catan" && v.Scenario != "pirate-islands" && v.Holder != player && p.Length >= 5)
            best = Math.Min(best, Math.Max(1, gap - BlockingAwardGain(v, p) + 1));
        return best;
    }
    // 自己已有五段线路时，建村或骑士占点切断领跑者也可能立刻取得最长路。
    // 这里只预测公开棋盘上的单次占点；不猜发展卡、未来骰子或多轮路线组合。
    private static int BlockingAwardGain(Position v, Seat p)
    {
        var ownedIds = v.Routes.Concat(v.Ships).Where(r => r.PlayerId == p.Id).Select(r => r.LocationId).ToHashSet();
        var ownEdges = v.Edges.Where(e => ownedIds.Contains(e.Id)).ToArray();
        var occupied = v.Buildings.Concat(v.Knights).Select(b => b.LocationId).ToHashSet();
        var candidates = ownEdges.SelectMany(e => e.Vertices).Distinct().Where(vertex => !occupied.Contains(vertex) &&
            v.Seats.Any(other => other.Id != p.Id && v.Edges.Count(e => e.Vertices.Contains(vertex) &&
                v.Routes.Concat(v.Ships).Any(r => r.PlayerId == other.Id && r.LocationId == e.Id)) >= 2));
        int best = 0;
        foreach (var vertex in candidates)
        {
            bool settle = p.Villages > 0 && v.Tiles.Any(t => t.Resource != "sea" && t.Resource != "fog" && t.Vertices.Contains(vertex)) &&
                !v.Edges.Where(e => e.Vertices.Contains(vertex)).Any(e => v.Buildings.Any(b => e.Vertices.Contains(b.LocationId)));
            if ((settle || p.BasicKnights > 0) && TakesAward(vertex, null))
                best = Math.Max(best, 2 + (settle ? SeafaringPlanning.SettlementPoints(v.Scenario, v.Tiles, v.Buildings.Where(b => b.PlayerId == p.Id).ToArray(), vertex) : 0));
            if (best >= 2 || p.Levels.Length == 0) continue;
            foreach (var guard in v.Guards.Where(k => k.Player == p.Id && k.Ready))
            {
                var seen = new HashSet<string> { guard.Vertex }; var queue = new Queue<string>(); queue.Enqueue(guard.Vertex);
                while (queue.Count > 0)
                {
                    string current = queue.Dequeue();
                    foreach (var edge in ownEdges.Where(e => e.Vertices.Contains(current)))
                    {
                        string next = edge.Vertices.First(x => x != current);
                        if (!seen.Add(next) || v.Buildings.Concat(v.Knights).Any(b => b.PlayerId != p.Id && b.LocationId == next)) continue;
                        queue.Enqueue(next);
                    }
                }
                if (seen.Contains(vertex) && TakesAward(vertex, guard.Vertex)) { best = Math.Max(best, 2); break; }
            }
        }
        return best;
        bool TakesAward(string vertex, string movingFrom)
        {
            foreach (var rival in v.Seats.Where(s => s.Id != p.Id))
            {
                var ids = v.Routes.Concat(v.Ships).Where(r => r.PlayerId == rival.Id).Select(r => r.LocationId).ToHashSet();
                var ships = v.Ships.Where(r => r.PlayerId == rival.Id).Select(r => r.LocationId).ToHashSet();
                var blocked = v.Buildings.Concat(v.Knights.Where(k => k.PlayerId != p.Id || k.LocationId != movingFrom))
                    .Where(b => b.PlayerId != rival.Id).Select(b => b.LocationId).ToHashSet();
                blocked.Add(vertex);
                if (RouteLength(v, rival.Id, v.Edges.Where(e => ids.Contains(e.Id)).ToArray(), ships, blocked) >= p.Length) return false;
            }
            return true;
        }
    }
    private static int RouteLength(Position v, string player, Edge[] routes, HashSet<string> ships, HashSet<string> blocked)
    {
        var joins = v.Buildings.Where(b => b.PlayerId == player).Select(b => b.LocationId).ToHashSet();
        int Walk(string vertex, HashSet<string> used, bool? ship)
        {
            if (used.Count > 0 && blocked.Contains(vertex)) return used.Count;
            int best = used.Count;
            foreach (var edge in routes.Where(e => e.Vertices.Contains(vertex) && !used.Contains(e.Id)))
            {
                bool nextShip = ships.Contains(edge.Id);
                if (ship.HasValue && ship != nextShip && !joins.Contains(vertex)) continue;
                used.Add(edge.Id); best = Math.Max(best, Walk(edge.Vertices.First(x => x != vertex), used, nextShip)); used.Remove(edge.Id);
            }
            return best;
        }
        return routes.SelectMany(e => e.Vertices).Distinct().Select(x => Walk(x, new HashSet<string>(), null)).DefaultIfEmpty(0).Max();
    }
}
}
