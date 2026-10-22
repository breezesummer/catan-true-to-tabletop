using System;
using System.Linq;
using System.Runtime.Serialization;

namespace Catan.Core.M5
{
    /// <summary>九张航海家地图共用的组合入口；专属机制与项目变体由会话执行。</summary>
    public static partial class CombinedScenarios
    {
        public static string[] Ids => M3.SeafarersScenarios.Ids;
        public static M3.SeafarersScenario Create(string id, int playerCount)
        {
            if (!Ids.Contains(id)) throw new ArgumentException("Unsupported Cities & Knights / Seafarers combination.", nameof(id));
            var scenario = M3.SeafarersScenarios.Create(id, playerCount);
            scenario.TargetVictoryPoints += 2;
            scenario.Version = CombinedGameSession.LegacyScenarioVersion;
            scenario.Description = "2025 城市与骑士推荐组合；16 分获胜，保留每个异乡地区首次定居的 2 分奖励。";
            if (id != "heading-for-new-shores" && id != "through-the-desert") scenario.Description = "保留航海家专属机制的城市骑士组合；部落奖励与战舰升级采用项目变体。";
            scenario.Name += " · 城市与骑士";
            return scenario;
        }
    }
    [DataContract] public sealed class ShipPlacement
    {
        [DataMember(Order=1)] public string LocationId { get; set; }
        [DataMember(Order=2)] public string PlayerId { get; set; }
        [DataMember(Order=3)] public int BuiltTurn { get; set; }
        [DataMember(Order=4, EmitDefaultValue=false)] public bool IsWarship { get; set; }
        [DataMember(Order=5, EmitDefaultValue=false)] public bool IsInvasionRoute { get; set; }
    }
    public sealed partial class Command
    {
        [DataMember(Order=80)] public bool BuildShip { get; set; }
    }
    public sealed partial class PlayerState
    {
        [DataMember(Order=80)] public int ShipsRemaining { get; set; } = 15;
        [DataMember(Order=81)] public int BonusVictoryPoints { get; set; }
        [DataMember(Order=82)] public string[] HomeRegions { get; set; } = Array.Empty<string>();
        [DataMember(Order=83)] public string[] SettledRegions { get; set; } = Array.Empty<string>();
    }
    public sealed partial class PendingDecision
    {
        [DataMember(Order=80)] public string Token { get; set; }
        [DataMember(Order=81)] public bool ShipsOnly { get; set; }
        [DataMember(Order=82)] public bool RoadsOnly { get; set; }
    }
    public sealed partial class GameState
    {
        [DataMember(Order=80)] public ShipPlacement[] Ships { get; set; } = Array.Empty<ShipPlacement>();
        [DataMember(Order=81)] public string PirateTileId { get; set; }
        [DataMember(Order=82)] public bool ShipMovedThisTurn { get; set; }
    }
    public sealed partial class PublicPlayer
    {
        public int ShipsRemaining { get; internal set; }
        public int BonusVictoryPoints { get; internal set; }
    }
    public sealed partial class BoardView
    {
        public ShipPlacement[] Ships { get; internal set; }
        public string PirateTileId { get; internal set; }
    }
    public sealed partial class PlayerView
    {
        public string ScenarioId { get; internal set; }
        public string ScenarioName { get; internal set; }
        public int TargetVictoryPoints { get; internal set; }
        public string[] LegalShipEdgeIds { get; internal set; }
        public string[] MovableShipEdgeIds { get; internal set; }
        public bool ShipMovedThisTurn { get; internal set; }
    }
}
