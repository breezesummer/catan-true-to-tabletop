using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace Catan.Core.TradeModules
{
    public sealed class TradeModuleOptions
    {
        public bool Harbormaster { get; set; }
        public bool Fishing { get; set; }
    }

    [DataContract]
    public sealed class TradeModuleState
    {
        public const string CurrentVersion = "harbor-fishing-f1-v001";
        [DataMember(Order=1)] public string Version { get; set; } = CurrentVersion;
        [DataMember(Order=2)] public bool Harbormaster { get; set; }
        [DataMember(Order=3)] public string HarbormasterPlayerId { get; set; }
        public const string SeafaringVersion = "harbor-fishing-f4-v001";
        public const string ProductVersion = "harbor-fishing-f3-v001";
        public const string FishingVersion = "harbor-fishing-f2-v001";
        [DataMember(Order=4, EmitDefaultValue=false)] public FishingState Fishing { get; set; }
        public TradeModuleState Copy()
        {
            var copy = (TradeModuleState)MemberwiseClone(); copy.Fishing = Fishing?.Copy(); return copy;
        }
    }

    public sealed class HarborScore
    {
        public string PlayerId { get; internal set; }
        public int Points { get; internal set; }
    }

    public sealed class TradeModuleView
    {
        public string Version { get; internal set; }
        public bool Harbormaster { get; internal set; }
        public FishingView Fishing { get; internal set; }
        public string HarbormasterPlayerId { get; internal set; }
        public HarborScore[] HarborScores { get; internal set; } = Array.Empty<HarborScore>();
    }

    /// <summary>TB25.pdf 第4页；损失裁决及有效建筑见 F0 D01。只接收规则数据。</summary>
    public static class HarbormasterRules
    {
        public static bool Supports(string scenarioId) => scenarioId != "pirate-islands";
        public static TradeModuleState Create(TradeModuleOptions options, string scenarioId)
        {
            if (options?.Harbormaster != true && options?.Fishing != true) return null;
            if (options.Fishing && !FishingScenarioCatalog.Supports(scenarioId)) throw new ArgumentException("此地图的捕鱼尚未开放。");
            if (!Supports(scenarioId)) throw new ArgumentException("海盗群岛暂未开放海港大师，需完成专项可达性验证。");
            return new TradeModuleState { Harbormaster = options.Harbormaster, Version = options.Fishing ? (FishingScenarioCatalog.SupportsSeafaring(scenarioId) ? TradeModuleState.SeafaringVersion : scenarioId == "m4-cities-knights" ? TradeModuleState.ProductVersion : TradeModuleState.FishingVersion) : TradeModuleState.CurrentVersion, Fishing = options.Fishing ? new FishingState() : null };
        }
        public static TradeModuleOptions Restore(TradeModuleState state, string scenarioId)
        {
            ValidateIdentity(state, scenarioId);
            return state == null ? null : new TradeModuleOptions { Harbormaster = state.Harbormaster, Fishing = state.Fishing != null };
        }
        // 启用模块的存档必须让旧程序显式拒绝，不能忽略未知字段后静默关闭规则。
        public static int SaveFormat(int legacyFormat, TradeModuleState state) => legacyFormat + (state == null ? 0 : 100);
        public static int DefaultTarget(int scenarioDefault, TradeModuleState state) => scenarioDefault + (state?.Harbormaster == true ? 1 : 0);
        public static int VictoryPoints(TradeModuleState state, string playerId) => state?.HarbormasterPlayerId == playerId && state != null ? 2 : 0;

        public static HarborScore[] Scores(IEnumerable<string> players, IEnumerable<string> portVertices,
            IEnumerable<PiecePlacement> settlements, IEnumerable<PiecePlacement> cities)
        {
            var vertices = new HashSet<string>(portVertices, StringComparer.Ordinal);
            var buildings = settlements.Select(p => (piece:p, points:1)).Concat(cities.Select(p => (piece:p, points:2)))
                .Where(p => vertices.Contains(p.piece.LocationId)).ToArray();
            return players.Select(id => new HarborScore { PlayerId = id, Points = buildings.Where(p => p.piece.PlayerId == id)
                .GroupBy(p => p.piece.LocationId).Sum(g => g.Max(p => p.points)) }).ToArray();
        }
        public static string Holder(HarborScore[] scores, string previous)
        {
            int maximum = scores.Length == 0 ? 0 : scores.Max(p => p.Points);
            if (maximum < 3) return null;
            var leaders = scores.Where(p => p.Points == maximum).Select(p => p.PlayerId).ToArray();
            return leaders.Contains(previous) ? previous : leaders.Length == 1 ? leaders[0] : null;
        }
        public static void Recalculate(TradeModuleState state, HarborScore[] scores)
        {
            if (state != null) state.HarbormasterPlayerId = state.Harbormaster ? Holder(scores, state.HarbormasterPlayerId) : null;
        }
        public static TradeModuleView View(TradeModuleState state, HarborScore[] scores) => state == null ? null :
            new TradeModuleView { Version = state.Version, Harbormaster = state.Harbormaster, HarbormasterPlayerId = state.HarbormasterPlayerId, HarborScores = state.Harbormaster ? scores : Array.Empty<HarborScore>() };
        public static void Validate(TradeModuleState state, string scenarioId, HarborScore[] scores)
        {
            ValidateIdentity(state, scenarioId);
            if (state != null && (state.Harbormaster ? Holder(scores, state.HarbormasterPlayerId) : null) != state.HarbormasterPlayerId)
                throw new InvalidDataException("海港大师归属与有效港口建筑不一致。");
        }
        private static void ValidateIdentity(TradeModuleState state, string scenarioId)
        {
            if (state != null && (state.Version != (state.Fishing == null ? TradeModuleState.CurrentVersion : FishingScenarioCatalog.SupportsSeafaring(scenarioId) ? TradeModuleState.SeafaringVersion : scenarioId == "m4-cities-knights" ? TradeModuleState.ProductVersion : TradeModuleState.FishingVersion) || (!state.Harbormaster && state.Fishing == null) || !Supports(scenarioId) || (state.Fishing != null && !FishingScenarioCatalog.Supports(scenarioId))))
                throw new InvalidDataException("附加玩法版本或场景不兼容。");
        }
    }
}
