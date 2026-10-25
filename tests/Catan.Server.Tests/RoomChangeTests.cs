using Catan.Network.Contracts;
using Catan.Rooms;
using Xunit;

namespace Catan.Server.Tests;

public sealed class RoomChangeTests
{
    [Fact]
    public void CommittedChangeIsPublishedOutsideRoomLockAndRejectedRequestsDoNotPublish()
    {
        using var storage = new Storage();
        using var rooms = new RoomDirectory(storage, "{}");
        var host = rooms.Handle(new NetworkRequest { Operation = "Create", ExpectedRulesVersion = NetworkProtocol.RulesVersion("base"), ExpectedScenarioVersion = NetworkProtocol.ScenarioVersion("base"), Payload = "{}" });
        Assert.True(host.Success);
        var changes = new List<RoomChange>();
        bool savedBeforeNotice = false, outsideLock = false;
        using var subscription = rooms.Subscribe(host.RoomId, change =>
        {
            changes.Add(change);
            savedBeforeNotice = storage.Commits > 1;
            // 另一线程能取得房间锁，证明通知没有在提交锁内调用。
            outsideLock = Task.Run(() => rooms.Handle(Request("Get", host))).Wait(TimeSpan.FromSeconds(2));
        });
        var ready = Request("Ready", host); ready.Payload = WireJson.Serialize(new ReadyPayload { Ready = true });
        var response = rooms.Handle(ready);
        Assert.True(response.Success);
        Assert.True(savedBeforeNotice); Assert.True(outsideLock);
        Assert.Equal(response.Revision, Assert.Single(changes).Revision);
        Assert.True(changes[0].CommittedAt > 0);
        rooms.Handle(Request("Unknown", host));
        rooms.Handle(Request("Get", host));
        Assert.Single(changes);
        subscription.Dispose();
        ready.ExpectedRevision = response.Revision; ready.Payload = WireJson.Serialize(new ReadyPayload { Ready = false });
        Assert.True(rooms.Handle(ready).Success);
        Assert.Single(changes);
    }

    [Fact]
    public void FailedStorageOnlyPublishesPausedSavedState()
    {
        using var storage = new Storage();
        using var rooms = new RoomDirectory(storage, "{}");
        var host = rooms.Handle(new NetworkRequest { Operation = "Create", ExpectedRulesVersion = NetworkProtocol.RulesVersion("base"), ExpectedScenarioVersion = NetworkProtocol.ScenarioVersion("base"), Payload = "{}" });
        RoomChange? published = null;
        using var subscription = rooms.Subscribe(host.RoomId, change => published = change);
        storage.Fail = true;
        var ready = Request("Ready", host); ready.Payload = "{\"Ready\":true}";
        var response = rooms.Handle(ready);
        Assert.False(response.Success);
        Assert.True(response.Room.Paused);
        Assert.Equal(host.Revision, published!.Revision);
        Assert.Equal(host.Revision, response.Revision);
        Assert.False(response.Room.Seats.Single(s => s.SeatId == host.SeatId).Ready);
    }

    private static NetworkRequest Request(string operation, NetworkResponse host) => new() { Operation = operation, RoomId = host.RoomId, Credential = host.Credential, ConnectionId = host.ConnectionId, ExpectedRevision = host.Revision };
    private sealed class Storage : IRoomStorage
    {
        public bool Fail; public int Commits;
        public IEnumerable<string> LoadAll() => Array.Empty<string>();
        public void Commit(string roomId, string json) { if (Fail) throw new IOException(); Commits++; }
        public void Delete(string roomId) { }
        public void Dispose() { }
    }
}
