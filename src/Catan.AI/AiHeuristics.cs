using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core;

namespace Catan.AI
{
    internal static class AiHeuristics
    {
        internal static readonly Resource[] Resources = (Resource[])Enum.GetValues(typeof(Resource));
        internal static ResourceBag Bag(int wood = 0, int brick = 0, int wool = 0, int wheat = 0, int ore = 0) =>
            new ResourceBag { Wood = wood, Brick = brick, Wool = wool, Wheat = wheat, Ore = ore };

        internal static int Pips(int? number) => number.HasValue && number != 7 && number >= 2 && number <= 12
            ? 6 - Math.Abs(7 - number.Value) : 0;

        // 回合指一次玩家掷骰；80～110只是先验，领跑者接近终点时必须缩短回本窗口。
        internal static int RemainingRolls(int turn, int leadingPoints, int targetPoints)
        {
            var prior = Math.Max(8, 100 - turn);
            var pace = Math.Max(2.0, (double)Math.Max(12, turn) / Math.Max(1, leadingPoints - 2));
            var race = Math.Max(4, (targetPoints - leadingPoints) * pace);
            return (int)Math.Min(80, Math.Min(prior, race));
        }

        internal static double SetupValue(Catan.Core.M2.PlayerView view, string vertex) => SetupValue(
            view.Board.Tiles, view.Board.Edges, view.Board.Settlements.Concat(view.Board.Cities),
            view.Board.Ports.Select(p => (p.Resource, p.Vertices)), view.PlayerId, vertex, false, false,
            OpeningSeat(view.Players.Select(p => p.Id).ToArray(), view.StartingPlayerId, view.PlayerId));

        internal static double SetupValue(Catan.Core.M3.PlayerView view, string vertex) => SetupValue(
            view.Board.Tiles, view.Board.Edges, view.Board.Settlements.Concat(view.Board.Cities),
            view.Board.Ports.Select(p => (p.Resource, p.Vertices)), view.PlayerId, vertex, true, false,
            OpeningSeat(view.Players.Select(p => p.Id).ToArray(), view.StartingPlayerId, view.PlayerId));

        internal static double SetupValue(Catan.Core.M4.PlayerView view, string vertex) => SetupValue(
            view.Board.Tiles, view.Board.Edges, view.Board.Settlements.Concat(view.Board.Cities),
            view.Board.Ports.Select(p => (p.Resource, p.Vertices)), view.PlayerId, vertex, false, true,
            OpeningSeat(view.Players.Select(p => p.Id).ToArray(), view.StartingPlayerId, view.PlayerId));

        internal static double SetupValue(Catan.Core.M5.PlayerView view, string vertex) => SetupValue(
            view.Board.Tiles, view.Board.Edges, view.Board.Settlements.Concat(view.Board.Cities),
            view.Board.Ports.Select(p => (p.Resource, p.Vertices)), view.PlayerId, vertex, true, true,
            OpeningSeat(view.Players.Select(p => p.Id).ToArray(), view.StartingPlayerId, view.PlayerId));

        private static double SetupValue(Tile[] tiles, Edge[] edges, IEnumerable<PiecePlacement> buildings,
            IEnumerable<(Resource? resource, string[] vertices)> ports, string player, string vertex,
            bool seafaring, bool citiesKnights, int openingSeat)
        {
            var allBuildings = buildings.ToArray();
            var owned = allBuildings.Where(b => b.PlayerId == player).Select(b => b.LocationId).ToArray();
            var existing = tiles.Where(t => t.Vertices.Intersect(owned).Any() && Pips(t.Number) > 0).ToArray();
            var local = tiles.Where(t => t.Vertices.Contains(vertex) && Pips(t.Number) > 0).ToArray();
            var initialCity = citiesKnights && owned.Length == 1;
            double Weight(string resource) => seafaring
                ? resource == "wool" ? 1.55 : resource == "wood" ? 1.4 : resource == "brick" ? 0.85 : 1.15
                : citiesKnights ? resource == "wood" ? 1.35 : resource == "wool" ? 1.25 : resource == "brick" ? 0.9 : 1.3
                : resource == "ore" || resource == "wheat" ? 1.3 : 1.0;
            double value = local.Sum(t => Pips(t.Number) * Weight(t.Resource));
            // 优先让两处开局建筑至少覆盖一块6或8；已有高频地块后再补资源和骰点。
            if (!existing.Any(t => t.Number == 6 || t.Number == 8) && local.Any(t => t.Number == 6 || t.Number == 8)) value += 24;
            value += local.Select(t => t.Resource).Distinct().Count(r => !existing.Any(t => t.Resource == r)) * 2.5;
            value += local.Select(t => t.Number).Distinct().Count(n => !existing.Any(t => t.Number == n)) * 0.6;
            if (citiesKnights && !existing.Any(t => t.Resource == "wheat") && local.Any(t => t.Resource == "wheat")) value += 3;
            // 第二次摆放为城市，木地产纸，支持尽早获得排水渠。
            if (initialCity) value += local.Where(t => t.Resource == "wood").Sum(t => Pips(t.Number) * 0.9);
            if (seafaring && tiles.Any(t => t.Resource == "sea" && t.Vertices.Contains(vertex))) value += 2;

            // 四人蛇形初设的中间席位适度抢占集中木砖；只影响首轮，肥沃多资源点仍可胜出。
            var contestOpening = owned.Length == 0 && (openingSeat == 2 || openingSeat == 3);
            if (contestOpening)
                value += new[] { "brick", "wood" }.Max(resource =>
                {
                    var matching = local.Where(t => t.Resource == resource).ToArray();
                    var production = matching.Sum(t => Pips(t.Number));
                    if (matching.Length >= 2 && production >= 7) return Math.Min(6.0, production * 0.65);
                    return matching.Any(t => Pips(t.Number) >= 4) ? 4.5 : 0;
                });

            foreach (var port in ports)
            {
                var onPort = port.vertices.Contains(vertex);
                // 只有仍可定居的邻近港口才作为后续计划，不穿过别人的建筑。
                var nearPort = !onPort && port.vertices.Any(p => !allBuildings.Any(b => b.LocationId == p)
                    && !edges.Where(e => e.Vertices.Contains(p)).Any(e => allBuildings.Any(b => e.Vertices.Contains(b.LocationId)))
                    && !edges.Any(e => e.Vertices.Contains(vertex) && e.Vertices.Contains(p))
                    && edges.Where(e => e.Vertices.Contains(vertex)).SelectMany(e => e.Vertices)
                        .Any(mid => !allBuildings.Any(b => b.LocationId == mid && b.PlayerId != player)
                            && edges.Any(e => e.Vertices.Contains(mid) && e.Vertices.Contains(p))));
                var alreadyPort = port.vertices.Intersect(owned).Any();
                if (!onPort && !nearPort && !alreadyPort) continue;
                var production = port.resource.HasValue
                    ? existing.Concat(local).Distinct().Where(t => t.Resource == port.resource.Value.ToString().ToLowerInvariant()).Sum(t => Pips(t.Number))
                    : 0;
                var preference = port.resource == Resource.Brick ? 1.65 : port.resource == Resource.Wheat ? 1.35 : 1.0;
                // 缺少对应产出时港口没有额外价值，避免为了砖港占贫瘠地。
                var benefit = port.resource.HasValue ? Math.Max(0, production - 3) * preference : 2.0;
                if (alreadyPort && !local.Any(t => port.resource.HasValue && t.Resource == port.resource.Value.ToString().ToLowerInvariant())) continue;
                value += benefit * (nearPort ? 0.25 : 1.0);
                if (contestOpening && port.resource == Resource.Brick && onPort
                    && local.Sum(t => Pips(t.Number)) >= 4
                    && tiles.Where(t => t.Resource == "brick").Sum(t => Pips(t.Number)) >= 8)
                    value += 2.5;

            }
            return value;
        }

        private static int OpeningSeat(string[] players, string startingPlayer, string player)
        {
            if (players.Length != 4) return 0;
            var start = Array.IndexOf(players, startingPlayer);
            var index = Array.IndexOf(players, player);
            return start < 0 || index < 0 ? 0 : (index - start + players.Length) % players.Length + 1;
        }

        // Marginal value declines as a hand accumulates a resource. This is computed from
        // this seat's hand only; opposing hands are neither required nor reconstructed.
        internal static double TradeValue(ResourceBag hand, ResourceBag gain, ResourceBag cost)
        {
            return Resources.Sum(r => Marginal(hand[r], gain[r]) - Marginal(hand[r] - cost[r], cost[r]));
        }

        private static double Marginal(int held, int count)
        {
            double value = 0;
            for (var i = 0; i < count; i++) value += 1.0 + 2.0 / (1 + held + i);
            return value;
        }
    }
}
