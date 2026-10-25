using System.Diagnostics;

namespace Catan.Rooms;

/// <summary>只传递提交水位，不包含局面、身份或私人信息。</summary>
public sealed record RoomChange(string RoomId, long Revision, long CommittedAt);

public sealed partial class RoomDirectory
{
    private readonly object subscribersGate = new();
    private readonly Dictionary<string, HashSet<ChangeSubscription>> subscribers = new(StringComparer.Ordinal);

    public IDisposable Subscribe(string roomId, Action<RoomChange> changed)
    {
        var subscription = new ChangeSubscription(this, roomId, changed);
        lock (subscribersGate)
        {
            if (!subscribers.TryGetValue(roomId, out var set)) subscribers.Add(roomId, set = new());
            set.Add(subscription);
        }
        return subscription;
    }

    private void Publish(RoomChange change)
    {
        ChangeSubscription[] copy;
        lock (subscribersGate) copy = subscribers.TryGetValue(change.RoomId, out var set) ? set.ToArray() : Array.Empty<ChangeSubscription>();
        foreach (var subscription in copy) subscription.Signal(change);
    }

    private sealed class ChangeSubscription(RoomDirectory owner, string roomId, Action<RoomChange> changed) : IDisposable
    {
        private int disposed;
        internal void Signal(RoomChange change)
        {
            if (Volatile.Read(ref disposed) != 0) return;
            try { changed(change); } catch { /* 订阅者失败不能改变已持久化结果。 */ }
        }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            lock (owner.subscribersGate)
                if (owner.subscribers.TryGetValue(roomId, out var set))
                {
                    set.Remove(this);
                    if (set.Count == 0) owner.subscribers.Remove(roomId);
                }
        }
    }
}
