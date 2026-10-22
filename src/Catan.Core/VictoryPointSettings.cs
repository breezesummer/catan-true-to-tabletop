using System;

namespace Catan.Core
{
    /// <summary>开局胜利分数的统一范围；剧本的额外胜利条件保持独立。</summary>
    public static class VictoryPointSettings
    {
        public const int Minimum = 5;
        public const int Maximum = 20;
        // 海盗群岛没有奖励分、布匹、分数卡或两类称号，最多五座村庄和四座城市，共十三分。
        public static int MaximumForScenario(string scenarioId) => scenarioId == "pirate-islands" ? 5 + 4 * 2 : Maximum;
        public static bool IsValid(int? points, int maximum = Maximum) => points.HasValue && points.Value >= Minimum && points.Value <= maximum;
        public static int Resolve(int? points, int scenarioDefault, int maximum = Maximum)
        {
            int value = points ?? scenarioDefault;
            if (!IsValid(value, maximum)) throw new ArgumentOutOfRangeException(nameof(points), "胜利分数须为 " + Minimum + "–" + maximum + " 分。");
            return value;
        }
    }
}
