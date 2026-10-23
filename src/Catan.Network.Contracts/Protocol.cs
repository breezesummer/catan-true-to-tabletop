using System;
using System.Runtime.Serialization;

namespace Catan.Network.Contracts
{
    public static class NetworkProtocol
    {
        public const int Version = 3;
        public const int MaximumRequestBytes = 65536;
        public const int MaximumResponseBytes = 4194304;
        public static string RulesVersion(string ruleSet)
        {
            switch (ruleSet)
            {
                case "base": return "catan-base-2025-en-v1";
                case "seafarers": return "catan-seafarers-2025-en-v1";
                case "cities": case "cities-knights": return "catan-cities-knights-2025-en-v1";
                case "combined": return "catan-seafarers-cities-knights-2025-en-v1";
                default: throw new ArgumentException("未知规则包。");
            }
        }
        public static string ScenarioVersion(string ruleSet)
        {
            switch (ruleSet)
            {
                case "base": return "m2-random-board-v001";
                case "seafarers": return "m3-seafarers-v001";
                case "cities": case "cities-knights": return "m4-random-board-v001";
                case "combined": return "m5-combined-variable-2025-v002";
                default: throw new ArgumentException("未知规则包。");
            }
        }
    }
    public sealed class NetworkRequest
    {
        public int ProtocolVersion = NetworkProtocol.Version;
        public string RequestId;
        // List, Describe, Create, Join, Reconnect, Get, History, Rename, Seat, Ready, Configure, Start, Restart, Command, Preview, Leave, Transfer, Recover, Revoke.
        public string Operation;
        public string RoomId;
        public string Credential;
        public string ConnectionId;
        public long ExpectedRevision;
        public string CommandId;
        public string RuleSet;
        public string ExpectedRulesVersion;
        public string ExpectedScenarioVersion;
        [DataMember(EmitDefaultValue=false)] public string ExpectedThreeKingdomsPackageVersion;
        [DataMember(EmitDefaultValue=false)] public string ExpectedThreeKingdomsContentHash;
        // Create / Join 首次发送前由客户端生成并安全保存；重试必须保持编号、凭据和内容相同。
        public string EnrollmentId;
        public string EnrollmentCredential;
        public string Payload;
    }
    public sealed class NetworkResponse
    {
        public int ProtocolVersion = NetworkProtocol.Version;
        public string RequestId;
        public bool Success;
        public string Code;
        public string Message;
        public string RoomId;
        public string Credential;
        public string ConnectionId;
        public string CredentialExpiresAtUtc;
        public long Revision;
        public long CommittedRevision;
        public bool IsDuplicate;
        public string SeatId;
        // 对局中由 AI 托管或等待下一自身回合接回时为 false；读取自己的可见局面仍获允许。
        public bool HumanControlAvailable;
        public string RuleSet;
        public string ViewJson;
        public string HistoryJson;
        // 与快照同一修订、同一观看权限的近期展示记录；不推进完整历史分页游标。
        public string PresentationHistoryJson;
        public long PresentationHistoryAfterSequence;
        public long HistoryCursor;
        public bool HasMoreHistory;
        public RoomSummary[] Rooms = Array.Empty<RoomSummary>();
        public RoomSummary Room;
    }
    public sealed class RoomSettings
    {
        public string Name = "卡坦好友房间";
        public string RuleSet = "base";
        public string ScenarioId = "base";
        public int PlayerCount = 4;
        public int? TargetVictoryPoints;
        public bool Listed = true;
        public string[] AiSeats = Array.Empty<string>();
        [DataMember(EmitDefaultValue=false)] public string ThreeKingdomsPackageVersion;
        [DataMember(EmitDefaultValue=false)] public string ThreeKingdomsContentHash;
        // 掉线由 AI 托管，重连在下一自身行动回合接回；权威存档保留。
    }
    public sealed class CreateRoomPayload
    {
        public string DisplayName = "玩家";
        public string Password;
        public RoomSettings Settings = new RoomSettings();
    }
    public sealed class JoinRoomPayload { public string DisplayName = "玩家"; public string Password; public string SeatId; }
    public sealed class SeatPayload { public string SeatId; }
    public sealed class NamePayload { public string DisplayName; }
    public sealed class ReadyPayload { public bool Ready; }
    public sealed class HistoryPayload { public long AfterSequence; public int Limit = 100; }
    public sealed class RoomSummary
    {
        public string RoomId;
        public string Name;
        public string RuleSet;
        public string ScenarioId;
        public string RulesVersion;
        public string ScenarioVersion;
        [DataMember(EmitDefaultValue=false)] public string ThreeKingdomsPackageVersion;
        [DataMember(EmitDefaultValue=false)] public string ThreeKingdomsContentHash;
        public int PlayerCount;
        public int OccupiedSeats;
        public int? TargetVictoryPoints;
        public bool HasPassword;
        public bool Listed;
        public bool Started;
        public int GameNumber;
        public bool Finished;
        public bool Paused;
        public string ManagerSeatId;
        public long Revision;
        public SeatSummary[] Seats = Array.Empty<SeatSummary>();
    }
    public sealed class SeatSummary { public string SeatId; public string DisplayName; public bool IsAi; public bool ControlledByAi; public bool RejoinPending; public bool Ready; public bool Connected; public bool Occupied; }
}
