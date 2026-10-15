using System;

namespace Catan.Network.Contracts
{
    public static class NetworkProtocol
    {
        public const int Version = 1;
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
        // List, Create, Join, Reconnect, Get, History, Seat, Ready, Configure, Start, Command, Preview, Leave, Transfer.
        public string Operation;
        public string RoomId;
        public string Credential;
        public string ConnectionId;
        public long ExpectedRevision;
        public string CommandId;
        public string RuleSet;
        public string ExpectedRulesVersion;
        public string ExpectedScenarioVersion;
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
        public string RuleSet;
        public string ViewJson;
        public string HistoryJson;
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
        // 已确认掉线等待，不自动托管；已保存房间不自动删除。
    }
    public sealed class CreateRoomPayload
    {
        public string DisplayName = "玩家";
        public string Password;
        public RoomSettings Settings = new RoomSettings();
    }
    public sealed class JoinRoomPayload { public string DisplayName = "玩家"; public string Password; public string SeatId; }
    public sealed class SeatPayload { public string SeatId; }
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
        public int PlayerCount;
        public int OccupiedSeats;
        public int? TargetVictoryPoints;
        public bool HasPassword;
        public bool Listed;
        public bool Started;
        public bool Finished;
        public bool Paused;
        public string ManagerSeatId;
        public long Revision;
        public SeatSummary[] Seats = Array.Empty<SeatSummary>();
    }
    public sealed class SeatSummary { public string SeatId; public string DisplayName; public bool IsAi; public bool Ready; public bool Connected; public bool Occupied; }
}
