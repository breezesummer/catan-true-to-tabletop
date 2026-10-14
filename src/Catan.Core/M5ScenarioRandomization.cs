using System;
using System.Collections.Generic;
using System.Linq;

namespace Catan.Core.M5
{
    public static partial class CombinedScenarios
    {
        // 2025 航海家第 5、11 页：主岛与海外区域各自使用原组件池，海陆轮廓和沙漠屏障不变。
        public static M3.SeafarersScenario CreateSeeded(string id, int playerCount, uint seed)
        {
            var scenario = Create(id, playerCount);
            scenario.Version = CombinedGameSession.ScenarioVersion;
            var main = new HashSet<string>(scenario.StartingTileIds);
            var groups = new[]
            {
                scenario.Tiles.Where(t => main.Contains(t.Id)).ToArray(),
                scenario.Tiles.Where(t => !main.Contains(t.Id) && t.Resource != "sea" && t.Resource != "desert").ToArray()
            };
            var numbers = groups.Select(g => g.Where(t => t.Number.HasValue).Select(t => t.Number.Value).ToArray()).ToArray();
            foreach (var group in groups)
            {
                var resources = group.Select(t => t.Resource).ToArray();
                Shuffle(resources, ref seed);
                for (int i = 0; i < group.Length; i++) { group[i].Resource = resources[i]; group[i].Number = null; }
            }

            var candidates = groups.Select(g => g.Where(t => t.Resource != "desert" &&
                (id != "through-the-desert" || t.Resource != "gold")).ToArray()).ToArray();
            foreach (var group in candidates) Shuffle(group, ref seed);
            var redCounts = numbers.Select(pool => pool.Count(IsRed)).ToArray();
            var redTiles = new List<Tile>();
            // 有限回溯选出互不相邻的红点位置；不靠无限重抽，也不会返回违规的备用地图。
            if (!ChooseRedTiles(candidates, redCounts, redTiles, 0, 0, redCounts[0]))
                throw new InvalidOperationException("无法为组合地图放置分离的 6／8 点数。");
            int cursor = 0;
            for (int group = 0; group < groups.Length; group++)
            {
                var red = numbers[group].Where(IsRed).ToArray();
                var black = numbers[group].Where(n => !IsRed(n)).ToArray();
                Shuffle(red, ref seed); Shuffle(black, ref seed);
                foreach (var number in red) redTiles[cursor++].Number = number;
                int next = 0;
                foreach (var tile in groups[group].Where(t => t.Resource != "desert" && !t.Number.HasValue)) tile.Number = black[next++];
            }
            var ports = scenario.Ports.Select(p => p.Resource).ToArray();
            Shuffle(ports, ref seed);
            for (int i = 0; i < ports.Length; i++) scenario.Ports[i].Resource = ports[i];
            if (id == "heading-for-new-shores")
                scenario.InitialRobberTileId = scenario.Tiles.Single(t => playerCount == 3 ? t.Number == 12 : t.Resource == "desert").Id;
            return scenario;
        }

        private static bool IsRed(int number) => number == 6 || number == 8;
        private static bool ChooseRedTiles(Tile[][] candidates, int[] counts, List<Tile> chosen, int group, int start, int remaining)
        {
            if (group == candidates.Length) return true;
            if (remaining == 0) return ChooseRedTiles(candidates, counts, chosen, group + 1, 0, group + 1 < counts.Length ? counts[group + 1] : 0);
            for (int i = start; i <= candidates[group].Length - remaining; i++)
            {
                var tile = candidates[group][i];
                if (chosen.Any(other => Math.Max(Math.Abs(tile.Q - other.Q), Math.Max(Math.Abs(tile.R - other.R), Math.Abs(tile.Q - other.Q + tile.R - other.R))) == 1)) continue;
                chosen.Add(tile);
                if (ChooseRedTiles(candidates, counts, chosen, group, i + 1, remaining - 1)) return true;
                chosen.RemoveAt(chosen.Count - 1);
            }
            return false;
        }

        private static void Shuffle<T>(T[] values, ref uint seed)
        {
            if (seed == 0) seed = 0x9e3779b9u;
            for (int i = values.Length - 1; i > 0; i--)
            {
                uint size = (uint)(i + 1), limit = uint.MaxValue - uint.MaxValue % size, value;
                do { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; value = seed - 1; } while (value >= limit);
                int j = (int)(value % size); var item = values[i]; values[i] = values[j]; values[j] = item;
            }
        }
    }
}
