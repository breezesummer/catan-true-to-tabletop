using System.Diagnostics;
using Catan.Network.Contracts;

namespace Catan.Server;

public sealed record OutboundMessage(byte[] Bytes, NetworkResponse Response, CommunicationEvent Diagnostic, bool IsPush)
{
    public long QueuedAt { get; } = Stopwatch.GetTimestamp();
}

/// <summary>回复按顺序保留；未发送的全量推送只保留最新一份，不因旧快照堆积断开客户端。</summary>
public sealed class SocketOutbox(int responseCapacity = 8) : IDisposable
{
    private readonly object gate = new();
    private readonly Queue<OutboundMessage> responses = new();
    private readonly SemaphoreSlim available = new(0);
    private OutboundMessage? latestPush;
    public int Count { get { lock (gate) return responses.Count + (latestPush == null ? 0 : 1); } }
    public bool TryWrite(OutboundMessage message, out bool coalesced)
    {
        lock (gate)
        {
            coalesced = false;
            if (message.IsPush)
            {
                coalesced = latestPush != null;
                latestPush = message;
                if (!coalesced) available.Release();
                return true;
            }
            if (responses.Count >= responseCapacity) return false;
            responses.Enqueue(message);
            available.Release();
            return true;
        }
    }
    public async Task<OutboundMessage> ReadAsync(CancellationToken cancellation)
    {
        await available.WaitAsync(cancellation);
        lock (gate)
        {
            if (responses.Count != 0) return responses.Dequeue();
            var message = latestPush!;
            latestPush = null;
            return message;
        }
    }
    public void Dispose() => available.Dispose();
}
