using System.Net.WebSockets;
using System.Text;
using Catan.Network.Contracts;
using Catan.Rooms;
using Catan.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Catan.Server.Tests;

public sealed class RoomSwitchTransportTests
{
    [Fact]
    public async Task BoundWebSocketCanDescribeJoinCreateAndReconnectAnotherRoomWithoutLeaving()
    {
        using var rooms = new RoomDirectory(new MemoryStorage(), Scenario());
        var target = rooms.Handle(Create("目标房间")); Assert.True(target.Success);
        var transport = new NetworkTransport(rooms, new ServerOptions());
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build(); app.UseWebSockets(); app.Map("/ws", transport.WebSocketAsync);
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var socket = new ClientWebSocket();
        try
        {
            await socket.ConnectAsync(new Uri(address.Replace("http://", "ws://") + "/ws"), deadline.Token);
            var first = await Exchange(socket, Create("原房间"), deadline.Token); Assert.True(first.Success);
            var description = await Exchange(socket, new() { Operation = "Describe", RoomId = target.RoomId }, deadline.Token);
            Assert.True(description.Success); Assert.Null(description.ViewJson); Assert.Null(description.ConnectionId);
            var enrollmentCredential = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            var joined = await Exchange(socket, new()
            {
                Operation = "Join", RoomId = target.RoomId, ExpectedRulesVersion = NetworkProtocol.RulesVersion("base"), ExpectedScenarioVersion = NetworkProtocol.ScenarioVersion("base"),
                Credential = first.Credential, EnrollmentId = Guid.NewGuid().ToString("N"), EnrollmentCredential = enrollmentCredential,
                Payload = WireJson.Serialize(new JoinRoomPayload { DisplayName = "切房玩家" })
            }, deadline.Token);
            Assert.True(joined.Success); Assert.Equal(target.RoomId, joined.RoomId); Assert.Null(joined.Credential);
            joined.Credential = enrollmentCredential;
            Assert.DoesNotContain(rooms.ListPublicRooms(), r => r.RoomId == first.RoomId);
            var ready = await Exchange(socket, new() { Operation = "Ready", RoomId = joined.RoomId, Credential = joined.Credential, ConnectionId = joined.ConnectionId, ExpectedRevision = joined.Revision, Payload = WireJson.Serialize(new ReadyPayload { Ready = true }) }, deadline.Token);
            Assert.True(ready.Success);
            // 切房成功后旧绑定不能再用同一 WebSocket 发授权操作。
            var stale = await Exchange(socket, new() { Operation = "Get", RoomId = first.RoomId, Credential = first.Credential, ConnectionId = first.ConnectionId }, deadline.Token);
            Assert.Equal("ConnectionMismatch", stale.Code);
            var third = await Exchange(socket, Create("再次创建"), deadline.Token); Assert.True(third.Success);
            var reconnect = await Exchange(socket, new()
            {
                Operation = "Reconnect", RoomId = target.RoomId, Credential = joined.Credential,
                ExpectedRulesVersion = NetworkProtocol.RulesVersion("base"), ExpectedScenarioVersion = NetworkProtocol.ScenarioVersion("base")
            }, deadline.Token);
            Assert.True(reconnect.Success); Assert.Equal(joined.SeatId, reconnect.SeatId); Assert.NotEqual(joined.ConnectionId, reconnect.ConnectionId);
            Assert.DoesNotContain(rooms.ListPublicRooms(), r => r.RoomId == third.RoomId);
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "验证完成", deadline.Token);
        }
        finally { await app.StopAsync(deadline.Token); }
    }

    private static NetworkRequest Create(string name) => new()
    {
        Operation = "Create", ExpectedRulesVersion = NetworkProtocol.RulesVersion("base"), ExpectedScenarioVersion = NetworkProtocol.ScenarioVersion("base"),
        Payload = WireJson.Serialize(new CreateRoomPayload { Settings = new RoomSettings { Name = name, PlayerCount = 3 } })
    };
    private static async Task<NetworkResponse> Exchange(ClientWebSocket socket, NetworkRequest request, CancellationToken cancellation)
    {
        request.RequestId = Guid.NewGuid().ToString("N");
        await socket.SendAsync(Encoding.UTF8.GetBytes(WireJson.Serialize(request)), WebSocketMessageType.Text, true, cancellation);
        while (true)
        {
            using var message = new MemoryStream(); var buffer = new byte[8192]; WebSocketReceiveResult part;
            do { part = await socket.ReceiveAsync(buffer, cancellation); message.Write(buffer, 0, part.Count); } while (!part.EndOfMessage);
            var response = WireJson.Deserialize<NetworkResponse>(Encoding.UTF8.GetString(message.ToArray()));
            if (response.RequestId == request.RequestId) return response;
        }
    }
    private static string Scenario()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "docs", "acceptance", "v0.1", "scenario.json");
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException("缺少场景夹具。");
    }
    private sealed class MemoryStorage : IRoomStorage
    {
        private readonly Dictionary<string, string> documents = new();
        public IEnumerable<string> LoadAll() => documents.Values.ToArray();
        public void Commit(string roomId, string privateDocument) => documents[roomId] = privateDocument;
        public void Delete(string roomId) => documents.Remove(roomId);
        public void Dispose() { }
    }
}
