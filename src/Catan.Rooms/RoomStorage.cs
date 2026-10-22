using System.Security.Cryptography;
using System.Text.Json;
using Catan.Network.Contracts;

namespace Catan.Rooms;

/// <summary>完整房间（权威存档、修订号、去重账本）作为一个原子提交单元。只供服务端使用。</summary>
public interface IRoomStorage : IDisposable
{
    IEnumerable<string> LoadAll();
    void Commit(string roomId, string privateDocument);
    void Delete(string roomId);
}

public sealed class AtomicRoomStorage : IRoomStorage
{
    private readonly string directory;
    private readonly FileStream ownership;
    public AtomicRoomStorage(string directory)
    {
        this.directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(this.directory);
        // 阻止两个服务实例同时恢复并写同一目录。残留 lock 文件不妨碍进程退出后的恢复。
        ownership = new FileStream(Path.Combine(this.directory, ".writer.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    public IEnumerable<string> LoadAll() => Directory.EnumerateFiles(directory, "*.room.json").Select(File.ReadAllText).ToArray();
    public void Commit(string roomId, string privateDocument)
    {
        if (roomId.Length != 12 || roomId.Any(c => !char.IsAsciiLetterOrDigit(c))) throw new InvalidDataException("房间标识无效。");
        var target = Path.Combine(directory, roomId + ".room.json");
        var temporary = Path.Combine(directory, ".pending-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.WriteThrough))
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(privateDocument);
                stream.Write(bytes);
                stream.Flush(true);
            }
            if (File.Exists(target)) File.Replace(temporary, target, null);
            else File.Move(temporary, target);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public void Delete(string roomId)
    {
        if (roomId.Length != 12 || roomId.Any(c => !char.IsAsciiLetterOrDigit(c))) throw new InvalidDataException("房间标识无效。");
        File.Delete(Path.Combine(directory, roomId + ".room.json"));
    }
    public void Dispose() => ownership.Dispose();
}

internal sealed class SavedRoom
{
    public int Format { get; set; } = 1;
    public int ProtocolVersion { get; set; } = NetworkProtocol.Version;
    public string RoomId { get; set; }
    public long Revision { get; set; }
    public RoomSettings Settings { get; set; }
    public string PasswordSalt { get; set; }
    public string PasswordHash { get; set; }
    public string ManagerSeatId { get; set; }
    public string RulesVersion { get; set; }
    public string ScenarioVersion { get; set; }
    public string GameSave { get; set; }
    public int GameNumber { get; set; }
    public bool Closed { get; set; }
    public Dictionary<string, string> AiDisplayNames { get; set; } = new(StringComparer.Ordinal);
    public List<SavedSeat> Seats { get; set; } = new();
    public Dictionary<string, SavedReceipt> Receipts { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, SavedEnrollment> Enrollments { get; set; } = new(StringComparer.Ordinal);
}
internal sealed class SavedSeat
{
    public string SeatId { get; set; }
    public string DisplayName { get; set; }
    public string CredentialHash { get; set; }
    public bool Ready { get; set; }
    public string ConnectionId { get; set; }
    public DateTimeOffset CredentialExpiresAtUtc { get; set; }
    public string EnrollmentId { get; set; }
    public string EnrollmentFingerprint { get; set; }
    public bool ControlledByAi { get; set; }
    public string RejoinAfterTurnKey { get; set; }
}
internal sealed class SavedEnrollment
{
    public string EnrollmentId { get; set; }
    public string Fingerprint { get; set; }
    public string Operation { get; set; }
    public long Revision { get; set; }
}
internal sealed class SavedReceipt
{
    public string SeatId { get; set; }
    public string Fingerprint { get; set; }
    public bool Success { get; set; }
    public string Code { get; set; }
    public string Message { get; set; }
    public long Revision { get; set; }
}
internal static class PrivateJson
{
    private static readonly JsonSerializerOptions Options = new() { IncludeFields = true };
    internal static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    internal static T Read<T>(string value) => JsonSerializer.Deserialize<T>(value, Options);
    internal static T Copy<T>(T value) => Read<T>(Write(value));
    internal static string Hash(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
    internal static string Token() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}
