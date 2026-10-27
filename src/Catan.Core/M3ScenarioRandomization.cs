using System;
using System.Linq;

namespace Catan.Core.M3
{
    public static partial class SeafarersScenarios
    {
        // 航海家2025规则书 PDF 第7、9、13、15、18页的可变布局。
        // 固定海陆轮廓、迷雾、村庄、奖励和奇观位置，只洗各剧本允许的组件池。
        internal static void RandomizeVariableLayout(SeafarersScenario map, uint seed)
        {
            var tiles = map.Tiles.Where(t => map.StartingTileIds.Contains(t.Id) && t.Number.HasValue).ToArray();
            var resources = tiles.Select(t => t.Resource).ToArray();
            var numbers = tiles.Select(t => t.Number.Value).ToArray();
            Shuffle(resources, ref seed);
            for (int i = 0; i < tiles.Length; i++) { tiles[i].Resource = resources[i]; tiles[i].Number = null; }
            Shuffle(tiles, ref seed);
            Shuffle(numbers, ref seed);
            var east = tiles.Where(t => t.Q == map.Tiles.Where(x => x.R == t.R && map.StartingTileIds.Contains(x.Id)).Max(x => x.Q)).Select(t => t.Id).ToArray();
            Func<Tile, int, bool> allowed = (tile, number) =>
            {
                if (map.Id == "four-islands" && (tile.Resource == "wood" || tile.Resource == "wool") && (number == 2 || number == 3 || number == 11 || number == 12)) return false;
                if (map.Id == "forgotten-tribe" && east.Contains(tile.Id) && (number == 5 || number == 6 || number == 8 || number == 9)) return false;
                if (map.Id == "wonders-of-catan" && IsRedNumber(number) && map.Tiles.Any(t => t.Resource == "desert" && Adjacent(tile, t))) return false;
                return true;
            };
            // 优先安置限制最严的数字，有限回溯；不存在无限重抽或违规备用布局。
            numbers = numbers.OrderBy(n => tiles.Count(t => allowed(t, n))).ThenBy(n => IsRedNumber(n) ? 0 : 1).ToArray();
            int budget = 200000;
            if (!PlaceNumbers(tiles, numbers, 0, allowed, map.Id != "fog-islands", ref budget))
                throw new InvalidOperationException("无法满足剧本地图数字约束。");
            if (map.Id == "four-islands" || map.Id == "fog-islands" || map.Id == "cloth-for-catan")
                map.InitialRobberTileId = map.Tiles.First(t => t.Number == 12).Id;
            var ports = map.Ports.Select(p => p.Resource).ToArray();
            Shuffle(ports, ref seed);
            for (int i = 0; i < ports.Length; i++) map.Ports[i].Resource = ports[i];
        }

        private static bool IsRedNumber(int n) => n == 6 || n == 8;
        private static bool Adjacent(Tile a, Tile b) => a.Vertices.Intersect(b.Vertices).Count() == 2;
        private static bool PlaceNumbers(Tile[] tiles, int[] numbers, int index, Func<Tile, int, bool> allowed, bool separateRed, ref int budget)
        {
            if (index == numbers.Length) return true;
            if (--budget < 0) return false;
            int number = numbers[index];
            foreach (var tile in tiles)
            {
                if (tile.Number.HasValue || !allowed(tile, number)) continue;
                if (separateRed && IsRedNumber(number) && tiles.Any(t => t.Number.HasValue && IsRedNumber(t.Number.Value) && Adjacent(tile, t))) continue;
                tile.Number = number;
                if (PlaceNumbers(tiles, numbers, index + 1, allowed, separateRed, ref budget)) return true;
                tile.Number = null;
            }
            return false;
        }

        internal static SeafarersScenario CreateVariable(string id, int players, uint seed)
        {
            var map = CreateSeeded(id, players, seed);
            if (id == "heading-for-new-shores" || id == "through-the-desert")
            {
                var generated = M5.CombinedScenarios.CreateSeededLegacy(id, players, seed);
                generated.Name = map.Name; generated.Description = map.Description;
                generated.Version = map.Version; generated.TargetVictoryPoints = map.TargetVictoryPoints;
                return generated;
            }
            if (id != "new-world" && id != "pirate-islands") RandomizeVariableLayout(map, seed);
            return map;
        }
    }
}
