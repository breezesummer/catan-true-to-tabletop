using System.Diagnostics;
using System.Text;
using Catan.Network.Contracts;

namespace Catan.Server;

public sealed record OutboundMessage(byte[] Bytes, NetworkResponse Response, CommunicationEvent Diagnostic, bool IsPush, bool IsSync = false)
{
    public long QueuedAt { get; } = Stopwatch.GetTimestamp();
    public long BufferedBytes { get; } = IsSync ? Encoding.UTF8.GetByteCount(Response.ViewJson ?? "") + (long)Encoding.UTF8.GetByteCount(Response.PresentationHistoryJson ?? "") + 1024 : Bytes.Length;
}

/// <summary>回复按顺序保留；未发送的全量推送只保留最新一份，不因旧快照堆积断开客户端。</summary>
public sealed class SocketOutbox(int responseCapacity = 8, long byteCapacity = 8 * 1024 * 1024) : IDisposable
{
    private readonly object gate = new();
    private readonly Queue<OutboundMessage> responses = new();
    private readonly SemaphoreSlim available = new(0);
    private OutboundMessage? latestPush;
    private long bufferedBytes;
    public int Count { get { lock (gate) return responses.Count + (latestPush == null ? 0 : 1); } }
    public bool TryWrite(OutboundMessage message, out bool coalesced)
    {
        lock (gate)
        {
            coalesced = false;
            if (message.IsPush)
            {
                coalesced = latestPush != null;
                long nextBytes = bufferedBytes - (latestPush?.BufferedBytes ?? 0) + message.BufferedBytes;
                if (nextBytes > byteCapacity) return false;
                bufferedBytes = nextBytes;
                latestPush = message;
                if (!coalesced) available.Release();
                return true;
            }
            if (responses.Count >= responseCapacity || bufferedBytes + message.BufferedBytes > byteCapacity) return false;
            responses.Enqueue(message);
            bufferedBytes += message.BufferedBytes;
            available.Release();
            return true;
        }
    }
    public async Task<OutboundMessage> ReadAsync(CancellationToken cancellation)
    {
        await available.WaitAsync(cancellation);
        lock (gate)
        {
            if (responses.Count != 0) { var response = responses.Dequeue(); bufferedBytes -= response.BufferedBytes; return response; }
            var message = latestPush!;
            latestPush = null;
            bufferedBytes -= message.BufferedBytes;
            return message;
        }
    }
    public void Dispose() => available.Dispose();
}
