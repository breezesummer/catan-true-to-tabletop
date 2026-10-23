using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Catan.Network.Contracts;
using Catan.Rooms;

namespace Catan.Server;

/// <summary>白名单通信元数据；不接受消息正文、口令、身份凭据、玩家名称或异常文本。</summary>
public sealed record CommunicationEvent
{
    public string Event { get; init; } = "";
    public string? Transport { get; init; }
    public string? SocketId { get; init; }
    public string? RequestId { get; init; }
    public string? CommandId { get; init; }
    public string? RoomId { get; init; }
    public string? ConnectionId { get; init; }
    public string? Operation { get; init; }
    public string? Code { get; init; }
    public string? ExceptionType { get; init; }
    public bool? Success { get; init; }
    public bool? Duplicate { get; init; }
    public long? Revision { get; init; }
    public long? ExpectedRevision { get; init; }
    public long? Bytes { get; init; }
    public long? Dropped { get; init; }
    public int? QueueDepth { get; init; }
    public double? ElapsedMs { get; init; }
    public double? WaitMs { get; init; }
}

/// <summary>有界后台 JSONL 日志。磁盘故障只停用诊断，不阻塞通信或权威提交。</summary>
public sealed class CommunicationDiagnostics : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private readonly Channel<(DateTimeOffset Time, CommunicationEvent Entry)> queue;
    private readonly Task writer;
    private readonly string directory;
    private readonly long maxFileBytes;
    private readonly int maxFiles;
    private readonly string session = Guid.NewGuid().ToString("N")[..12];
    private long dropped;
    private int failed;

    public CommunicationDiagnostics(string directory, int capacity = 4096, long maxFileBytes = 16 * 1024 * 1024, int maxFiles = 8)
    {
        if (capacity < 1 || maxFileBytes < 1024 || maxFiles < 2) throw new ArgumentOutOfRangeException(nameof(capacity));
        this.directory = Path.GetFullPath(directory);
        this.maxFileBytes = maxFileBytes;
        this.maxFiles = maxFiles;
        queue = Channel.CreateBounded<(DateTimeOffset, CommunicationEvent)>(new BoundedChannelOptions(capacity)
        { SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait });
        writer = Task.Run(WriteLoop);
        Record(new() { Event = "diagnostics.started", Code = "Schema1" });
    }

    public void Record(CommunicationEvent entry)
    {
        if (Volatile.Read(ref failed) != 0) return;
        if (!queue.Writer.TryWrite((DateTimeOffset.UtcNow, entry))) Interlocked.Increment(ref dropped);
    }

    public void RecordRoom(RoomDiagnostic entry) => Record(new()
    {
        Event = entry.Event, RoomId = Identifier(entry.RoomId), RequestId = Identifier(entry.RequestId),
        CommandId = Identifier(entry.CommandId), Operation = OperationName(entry.Operation),
        // 房间阶段正常返回也可能是规则拒绝，具体成功状态只以 request.dispatched 为准。
        Revision = entry.Revision, Success = entry.Event == "room.handle" ? null : entry.Success, ElapsedMs = entry.ElapsedMs, WaitMs = entry.WaitMs,
        Bytes = entry.Bytes, ExceptionType = entry.ExceptionType
    });

    public static CommunicationEvent Request(string eventName, string transport, string? socketId, NetworkRequest? request, NetworkResponse? response = null) => new()
    {
        Event = eventName, Transport = transport, SocketId = socketId,
        RequestId = Identifier(request?.RequestId ?? response?.RequestId), CommandId = Identifier(request?.CommandId),
        RoomId = Identifier(response?.RoomId ?? request?.RoomId), ConnectionId = Fingerprint(response?.ConnectionId ?? request?.ConnectionId),
        Operation = OperationName(request?.Operation), Code = response?.Code, Success = response?.Success,
        Duplicate = response?.IsDuplicate, Revision = response?.Revision, ExpectedRevision = request?.ExpectedRevision
    };

    // 标识异常时只保存摘要，避免将恶意构造的自由文本写入日志。
    public static string? Identifier(string? value) => value == null ? null :
        (value.Length is 12 or 32 && value.All(Uri.IsHexDigit)) || Guid.TryParseExact(value, "D", out _) ? value : Fingerprint(value);
    public static string? Fingerprint(string? value) => string.IsNullOrEmpty(value) ? null : "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16];
    public static string? OperationName(string? value) => value == null ? null : value is
        "List" or "Describe" or "Create" or "Join" or "Reconnect" or "Get" or "History" or "Command" or "Preview" or
        "Rename" or "Ready" or "Seat" or "Configure" or "Start" or "Restart" or "Transfer" or "Leave" or "Revoke" or "Recover" ? value : "Unknown";

    private async Task WriteLoop()
    {
        StreamWriter? file = null;
        string? day = null;
        long fileBytes = 0;
        int sequence = 0;
        try
        {
            Directory.CreateDirectory(directory);
            await foreach (var item in queue.Reader.ReadAllAsync())
            {
                var nextDay = item.Time.UtcDateTime.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
                if (file == null || day != nextDay || fileBytes >= maxFileBytes)
                {
                    if (file != null) await file.DisposeAsync();
                    var path = Path.Combine(directory, $"communication-{nextDay}-{session}-{sequence++:D4}.jsonl");
                    file = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 65536, FileOptions.Asynchronous), new UTF8Encoding(false));
                    day = nextDay; fileBytes = 0;
                    foreach (var old in new DirectoryInfo(directory).GetFiles("communication-*.jsonl").OrderByDescending(f => f.LastWriteTimeUtc).ThenByDescending(f => f.Name).Skip(maxFiles))
                        old.Delete();
                }
                var lost = Interlocked.Exchange(ref dropped, 0);
                if (lost > 0)
                {
                    var notice = Encode(item.Time, new() { Event = "diagnostics.dropped", Dropped = lost });
                    await file.WriteLineAsync(notice); fileBytes += Encoding.UTF8.GetByteCount(notice) + 1;
                }
                var line = Encode(item.Time, item.Entry);
                await file.WriteLineAsync(line); fileBytes += Encoding.UTF8.GetByteCount(line) + 1;
                // 仅后台刷新，在线读取可及时看见事件；不对每条诊断进行磁盘持久化屏障。
                if (!queue.Reader.TryPeek(out _)) await file.FlushAsync();
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Interlocked.Exchange(ref failed, 1);
            Console.Error.WriteLine("CATAN_DIAGNOSTICS_UNAVAILABLE " + error.GetType().Name);
        }
        finally
        {
            if (file != null)
                try { await file.DisposeAsync(); } catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
    }

    private string Encode(DateTimeOffset timestamp, CommunicationEvent entry) =>
        JsonSerializer.Serialize(new { timestampUtc = timestamp.ToString("O"), schema = 1, sessionId = session, data = entry }, JsonOptions);

    public async ValueTask DisposeAsync()
    {
        queue.Writer.TryComplete();
        await writer;
    }
}
