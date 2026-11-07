using System;
using System.Collections.Generic;
using System.Linq;

namespace Catan.Core
{
    internal interface ITileHarvestHost
    {
        void RecordTileHarvest(string player, int total, int[] received, int[] reservedGold = null);
    }

    // 一次发放单独计数。库存不足时按稳定地块 ID 归属实际发出的牌。
    internal sealed class TileHarvest
    {
        Dictionary<string, HashSet<string>> multiple;
        internal string[] For(string seat) => multiple != null && multiple.TryGetValue(seat, out var tiles) ? tiles.OrderBy(t => t, StringComparer.Ordinal).ToArray() : null;

        internal void Paid(string seat, IEnumerable<Tile> eligible, int[] received, Func<Tile,int,int> yield, Func<Tile,int> goldYield = null, int gold = 0)
        {
            var remaining = (int[])received.Clone();
            var tiles = eligible.OrderBy(t => t.Id, StringComparer.Ordinal).ToArray();
            foreach (var tile in tiles)
            {
                int count = 0;
                for (int card = 0; card < remaining.Length; card++)
                {
                    if (remaining[card] == 0) continue;
                    int n = Math.Min(remaining[card], yield(tile, card));
                    remaining[card] -= n;
                    count += n;
                }
                Mark(seat, tile.Id, count);
            }
            if (goldYield == null) return;
            foreach (var tile in tiles.Where(t => t.Resource == "gold"))
            {
                int n = Math.Min(gold, goldYield(tile));
                Mark(seat, tile.Id, n);
                gold -= n;
            }
        }
        internal void Normal(string player, IEnumerable<Tile> tiles, PiecePlacement[] settlements, PiecePlacement[] cities, int[] received, bool expanded)
        {
            if (received.Sum() < 2) return;
            Paid(player, tiles, received, (tile, card) =>
            {
                string terrain = card < 5 ? Terrain[card] : card == 5 ? "wool" : card == 6 ? "ore" : "wood";
                if (tile.Resource != terrain) return 0;
                int city = cities.Count(b => b.PlayerId == player && tile.Vertices.Contains(b.LocationId));
                if (card >= 5) return city;
                int village = settlements.Count(b => b.PlayerId == player && tile.Vertices.Contains(b.LocationId));
                return village + (!expanded || card == 1 || card == 3 ? 2 : 1) * city;
            });
        }
        internal void Gold(string seat, IEnumerable<Tile> tiles, PiecePlacement[] villages, PiecePlacement[] cities, int received)
        {
            if (received < 2) return;
            Paid(seat, tiles.Where(t => t.Resource == "gold"), new[] { received }, (t, card) => 0,
                t => villages.Count(b => b.PlayerId == seat && t.Vertices.Contains(b.LocationId)) +
                     2 * cities.Count(b => b.PlayerId == seat && t.Vertices.Contains(b.LocationId)), received);
        }
        internal static readonly string[] Terrain = { "wood", "brick", "wool", "wheat", "ore" };
        internal bool Any => multiple != null;
        void Mark(string seat, string tile, int count)
        {
            if (count < 2) return;
            if (multiple == null) multiple = new Dictionary<string, HashSet<string>>();
            if (!multiple.TryGetValue(seat, out var tiles)) multiple.Add(seat, tiles = new HashSet<string>());
            tiles.Add(tile);
        }
    }
}
