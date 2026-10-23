namespace Catan.Rooms;

/// <summary>房间阶段计时；无权威存档、玩家视图、凭据或消息正文。</summary>
public sealed record RoomDiagnostic
{
    public string Event { get; init; }
    public string RoomId { get; init; }
    public string RequestId { get; init; }
    public string CommandId { get; init; }
    public string Operation { get; init; }
    public long Revision { get; init; }
    public bool Success { get; init; } = true;
    public double ElapsedMs { get; init; }
    public double? WaitMs { get; init; }
    public long? Bytes { get; init; }
    public string ExceptionType { get; init; }
}
