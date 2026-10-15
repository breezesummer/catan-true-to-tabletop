using System.Collections.Concurrent;

namespace Catan.Server;

// 只按源地址保存计数，不保存昵称、口令、令牌或请求内容。
public sealed class PeerRateLimiter(int maxPeers = 4096)
{
    private sealed class Window { public long Start; public int Count; }
    private readonly ConcurrentDictionary<string, Window> peers = new(StringComparer.Ordinal);

    public bool Allow(string peer, int limit, int seconds)
    {
        var now = Environment.TickCount64;
        if (peers.Count >= maxPeers && !peers.ContainsKey(peer))
        {
            foreach (var entry in peers) if (now - Volatile.Read(ref entry.Value.Start) > 120_000) peers.TryRemove(entry.Key, out _);
            if (peers.Count >= maxPeers) return false;
        }
        var window = peers.GetOrAdd(peer, _ => new Window { Start = now });
        lock (window)
        {
            if (now - window.Start >= seconds * 1000L) { window.Start = now; window.Count = 0; }
            return ++window.Count <= limit;
        }
    }
}
