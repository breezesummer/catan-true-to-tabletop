using System;
using System.IO;
using System.Linq;

namespace Catan.Core.TradeModules
{
    /// <summary>TB25.pdf 第9页六个浅蓝V口；沿用标准19格的稳定空间ID。</summary>
    public static partial class FishingScenarioCatalog
    {
        public const string CurrentVersion = "standard-fishing-map-v001";
        public static bool Supports(string scenarioId) => scenarioId == "m2-base-game" || scenarioId == "m4-cities-knights" || SupportsSeafaring(scenarioId);
        private static readonly string[][] StandardVertices =
        {
            new[] { "V04", "V08", "V12" }, new[] { "V39", "V44", "V48" },
            new[] { "V52", "V49", "V53" }, new[] { "V43", "V38", "V33" },
            new[] { "V27", "V21", "V16" }, new[] { "V02", "V05", "V01" }
        };
        internal static FishingState Create(Scenario scenario, string[] playerIds, Action<int[]> shuffle, Func<int, int> draw)
        {
            var tiles = scenario.Topology.Tiles;
            var desert = tiles.Single(t => t.Resource == "desert");
            // 正常布局完成后，有界地将外圈沙漠与随机内圈组件交换；保留全部资源及普通号码。
            if (Radius(desert) > 1)
            {
                var inner = tiles.Where(t => Radius(t) <= 1).OrderBy(t => t.Id, StringComparer.Ordinal).ToArray();
                if (inner.Length != 7) throw new InvalidDataException("捕鱼标准图必须包含七格内圈。");
                var center = inner[draw(inner.Length)];
                desert.Resource = center.Resource; desert.Number = center.Number;
                center.Resource = "desert"; center.Number = null; desert = center;
            }
            desert.Resource = "lake";
            var numbers = new[] { 4, 5, 6, 8, 9, 10 }; shuffle(numbers);
            var state = new FishingState
            {
                LakeTileId = desert.Id,
                Grounds = StandardVertices.Select((vertices, i) => new FishingGround { Id = "FG" + (i + 1), Number = numbers[i], VertexIds = vertices.ToArray() }).ToArray(),
                Holdings = playerIds.Select(id => new FishHolding { PlayerId = id }).ToArray()
            };
            Validate(state, tiles, scenario.Topology.Edges);
            return state;
        }
        private static int Radius(Tile t) => Math.Max(Math.Abs(t.Q), Math.Max(Math.Abs(t.R), Math.Abs(t.Q + t.R)));
        internal static void Validate(FishingState state, Tile[] tiles, Edge[] edges)
        {
            if (state.MapVersion != null && (state.MapVersion.StartsWith(SeafaringVersion+":", StringComparison.Ordinal)||state.MapVersion.StartsWith(ThirdWaveVersion+":", StringComparison.Ordinal))) { ValidateSeafaring(state,tiles,edges); return; }
            if (state.MapVersion != CurrentVersion || tiles.Length != 19 || tiles.Count(t => t.Resource == "lake") != 1 ||
                !tiles.Any(t => t.Id == state.LakeTileId && t.Resource == "lake" && t.Number == null && Radius(t) <= 1) ||
                state.Grounds == null || state.Grounds.Length != 6 ||
                !state.Grounds.Select(g => g.Number).OrderBy(x => x).SequenceEqual(new[] { 4, 5, 6, 8, 9, 10 }))
                throw new InvalidDataException("捕鱼标准图配置无效。");
            var coast = edges.Where(e => tiles.Count(t => e.Vertices.All(v => t.Vertices.Contains(v))) == 1).ToArray();
            for (int i = 0; i < 6; i++)
            {
                var g = state.Grounds[i];
                if (g.Id != "FG" + (i + 1) || g.VertexIds == null || !g.VertexIds.SequenceEqual(StandardVertices[i]) ||
                    !coast.Any(e => e.Vertices.Contains(g.VertexIds[0]) && e.Vertices.Contains(g.VertexIds[1])) ||
                    !coast.Any(e => e.Vertices.Contains(g.VertexIds[1]) && e.Vertices.Contains(g.VertexIds[2])))
                    throw new InvalidDataException("捕鱼渔场必须附着在指定连续海岸顶点。");
            }
        }
    }
}
