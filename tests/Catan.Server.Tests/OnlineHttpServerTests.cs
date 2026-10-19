using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Catan.Network.Contracts;
using Catan.Server;
using Xunit;

namespace Catan.Server.Tests;

public sealed class OnlineHttpServerTests
{
    [Fact]
    public void OnlinePrivateDirectoryNeverLoadsOrCreatesCertificateFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), "catan-online-files-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var staleCertificate = Path.Combine(directory, "lan-server.pfx");
        File.WriteAllText(staleCertificate, "旧局域网证书损坏也不影响在线模式");
        try
        {
            using var files = new PrivateServerFiles(new ServerOptions { DataDirectory = directory, Online = true, CertificatePath = "不存在的证书.pfx" });
            Assert.Null(files.Certificate);
            Assert.Empty(files.Fingerprint);
            Assert.Equal("旧局域网证书损坏也不影响在线模式", File.ReadAllText(staleCertificate));
            Assert.False(File.Exists(Path.Combine(directory, "lan-certificate.key")));
            Assert.False(File.Exists(Path.Combine(directory, "server-fingerprint.txt")));
            Assert.ThrowsAny<IOException>(() => new PrivateServerFiles(new ServerOptions { DataDirectory = directory, Online = true }));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task RealOnlineServerSupportsIpHttpLobbyCreateJoinAndWebSocketWithoutCertificate()
    {
        await using var server = await OnlineServer.StartAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var health = await server.Client.GetAsync("health", deadline.Token);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        using (var data = JsonDocument.Parse(await health.Content.ReadAsStringAsync(deadline.Token)))
        {
            Assert.Equal("ok", data.RootElement.GetProperty("status").GetString());
            Assert.Equal("http", data.RootElement.GetProperty("transport").GetString());
            Assert.Equal(server.Client.BaseAddress!.Port, data.RootElement.GetProperty("httpPort").GetInt32());
            Assert.Equal(0, data.RootElement.GetProperty("httpsPort").GetInt32());
            Assert.Equal("", data.RootElement.GetProperty("certificateFingerprint").GetString());
        }
        Assert.DoesNotContain(Directory.EnumerateFiles(server.DataDirectory), path => path.EndsWith(".pfx") || path.EndsWith(".key") || path.EndsWith("server-fingerprint.txt"));
        var empty = await server.Exchange(new NetworkRequest { Operation = "List" }, deadline.Token);
        Assert.True(empty.Success);
        Assert.Empty(empty.Rooms);

        var create = Enroll("Create", new CreateRoomPayload { DisplayName = "房主", Password = "房间口令", Settings = new RoomSettings { Name = "公网 IP 好友桌", PlayerCount = 3 } });
        var host = await server.Exchange(create, deadline.Token);
        Assert.True(host.Success, host.Message);
        Assert.Null(host.Credential);
        var listing = await server.Exchange(new NetworkRequest { Operation = "List" }, deadline.Token);
        var room = Assert.Single(listing.Rooms);
        Assert.Equal(host.RoomId, room.RoomId);
        Assert.Equal("公网 IP 好友桌", room.Name);
        Assert.True(room.HasPassword);
        var publicJson = WireJson.Serialize(listing);
        Assert.DoesNotContain("房间口令", publicJson);
        Assert.DoesNotContain(create.EnrollmentCredential, publicJson);
        Assert.Null(listing.ViewJson);

        var wrongPassword = Enroll("Join", new JoinRoomPayload { Password = "错误口令" }, host.RoomId);
        Assert.Equal("InvalidPassword", (await server.Exchange(wrongPassword, deadline.Token)).Code);
        var join = Enroll("Join", new JoinRoomPayload { DisplayName = "HTTP 玩家", Password = "房间口令" }, host.RoomId);
        var guest = await server.Exchange(join, deadline.Token);
        Assert.True(guest.Success, guest.Message);
        Assert.NotEqual(host.SeatId, guest.SeatId);
        var denied = await server.Exchange(new NetworkRequest { Operation = "Get", RoomId = host.RoomId, Credential = "伪造身份", ConnectionId = host.ConnectionId }, deadline.Token);
        Assert.Equal("Unauthorized", denied.Code);
        Assert.Null(denied.ViewJson);

        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new UriBuilder(server.Client.BaseAddress!) { Scheme = "ws", Path = "/ws" }.Uri, deadline.Token);
        var socketListing = await ExchangeSocket(socket, new NetworkRequest { Operation = "List" }, deadline.Token);
        Assert.Equal(host.RoomId, Assert.Single(socketListing.Rooms).RoomId);
        var socketJoin = Enroll("Join", new JoinRoomPayload { DisplayName = "WS 玩家", Password = "房间口令" }, host.RoomId);
        var socketGuest = await ExchangeSocket(socket, socketJoin, deadline.Token);
        Assert.True(socketGuest.Success, socketGuest.Message);
        Assert.Equal(3, socketGuest.Room.OccupiedSeats);
        var ready = await ExchangeSocket(socket, new NetworkRequest
        {
            Operation = "Ready", RoomId = host.RoomId, Credential = socketJoin.EnrollmentCredential,
            ConnectionId = socketGuest.ConnectionId, ExpectedRevision = socketGuest.Revision,
            Payload = WireJson.Serialize(new ReadyPayload { Ready = true })
        }, deadline.Token);
        Assert.True(ready.Success, ready.Message);
        var stale = await ExchangeSocket(socket, new NetworkRequest { Operation = "Get", RoomId = host.RoomId, Credential = create.EnrollmentCredential, ConnectionId = host.ConnectionId }, deadline.Token);
        Assert.Equal("ConnectionMismatch", stale.Code);
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "验证完成", deadline.Token);
    }

    [Fact]
    public async Task RealOnlineHttpStillRejectsBrowserOriginsAndQueryCredentials()
    {
        await using var server = await OnlineServer.StartAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var origin = new HttpRequestMessage(HttpMethod.Post, "api/request")
        {
            Content = new StringContent(WireJson.Serialize(Enroll("Create", new CreateRoomPayload())), Encoding.UTF8, "application/json")
        };
        origin.Headers.Add("Origin", "https://untrusted.example");
        using var denied = await server.Client.SendAsync(origin, deadline.Token);
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        using var query = await server.Client.GetAsync("health?credential=不得进入查询参数", deadline.Token);
        Assert.Equal(HttpStatusCode.BadRequest, query.StatusCode);
        using var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Origin", "https://untrusted.example");
        await Assert.ThrowsAsync<WebSocketException>(() => socket.ConnectAsync(new UriBuilder(server.Client.BaseAddress!) { Scheme = "ws", Path = "/ws" }.Uri, deadline.Token));
        Assert.Empty((await server.Exchange(new NetworkRequest { Operation = "List" }, deadline.Token)).Rooms);
    }

    [Fact]
    public async Task RealOnlineHttpKeepsRequestRateLimit()
    {
        await using var server = await OnlineServer.StartAsync(requestLimit: 2);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        Assert.True((await server.Exchange(new NetworkRequest { Operation = "List" }, deadline.Token)).Success);
        Assert.True((await server.Exchange(new NetworkRequest { Operation = "List" }, deadline.Token)).Success);
        using var response = await server.Client.PostAsync("api/request", new StringContent(WireJson.Serialize(new NetworkRequest { Operation = "List" }), Encoding.UTF8, "application/json"), deadline.Token);
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("RateLimited", WireJson.Deserialize<NetworkResponse>(await response.Content.ReadAsStringAsync(deadline.Token)).Code);
    }

    private static NetworkRequest Enroll(string operation, object payload, string? roomId = null) => new()
    {
        Operation = operation, RoomId = roomId,
        ExpectedRulesVersion = NetworkProtocol.RulesVersion("base"), ExpectedScenarioVersion = NetworkProtocol.ScenarioVersion("base"),
        EnrollmentId = Guid.NewGuid().ToString("N"), EnrollmentCredential = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), Payload = WireJson.SerializeObject(payload)
    };

    private static async Task<NetworkResponse> ExchangeSocket(ClientWebSocket socket, NetworkRequest request, CancellationToken cancellation)
    {
        request.RequestId = Guid.NewGuid().ToString("N");
        await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(WireJson.Serialize(request))), WebSocketMessageType.Text, true, cancellation);
        var buffer = new byte[8192];
        while (true)
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult part;
            do
            {
                part = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellation);
                Assert.Equal(WebSocketMessageType.Text, part.MessageType);
                message.Write(buffer, 0, part.Count);
            } while (!part.EndOfMessage);
            var response = WireJson.Deserialize<NetworkResponse>(Encoding.UTF8.GetString(message.ToArray()));
            if (response.RequestId == request.RequestId) return response;
        }
    }

    private sealed class OnlineServer : IAsyncDisposable
    {
        private readonly Process process;
        private readonly Task<string> output;
        private readonly Task<string> errors;
        public string DataDirectory { get; }
        public HttpClient Client { get; }

        private OnlineServer(Process process, string directory, int port)
        {
            this.process = process;
            output = process.StandardOutput.ReadToEndAsync();
            errors = process.StandardError.ReadToEndAsync();
            DataDirectory = directory;
            Client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}/"), Timeout = TimeSpan.FromSeconds(2) };
        }

        public static async Task<OnlineServer> StartAsync(int requestLimit = 300)
        {
            int port;
            using (var reservation = new TcpListener(IPAddress.Loopback, 0)) { reservation.Start(); port = ((IPEndPoint)reservation.LocalEndpoint).Port; }
            var directory = Path.Combine(Path.GetTempPath(), "catan-online-process-" + Guid.NewGuid().ToString("N"));
            var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
            };
            start.ArgumentList.Add(typeof(ServerOptions).Assembly.Location);
            foreach (var argument in new[] { "--mode", "online", "--port", port.ToString(), "--data-dir", directory, "--request-limit-per-10s", requestLimit.ToString() }) start.ArgumentList.Add(argument);
            // 不借用开发机证书，也不把外部环境的证书口令传给在线服务。
            start.Environment.Remove("CATAN_CERTIFICATE_PASSWORD");
            var process = Process.Start(start) ?? throw new InvalidOperationException("无法启动独立房间服务进程。");
            var server = new OnlineServer(process, directory, port);
            try
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                while (!process.HasExited)
                {
                    try
                    {
                        using var response = await server.Client.GetAsync("health", deadline.Token);
                        if (response.IsSuccessStatusCode) return server;
                    }
                    catch (HttpRequestException) { }
                    await Task.Delay(50, deadline.Token);
                }
                throw new InvalidOperationException("独立房间服务启动失败：" + await server.errors + await server.output);
            }
            catch { await server.DisposeAsync(); throw; }
        }

        public async Task<NetworkResponse> Exchange(NetworkRequest request, CancellationToken cancellation)
        {
            request.RequestId = Guid.NewGuid().ToString("N");
            using var response = await Client.PostAsync("api/request", new StringContent(WireJson.Serialize(request), Encoding.UTF8, "application/json"), cancellation);
            response.EnsureSuccessStatusCode();
            return WireJson.Deserialize<NetworkResponse>(await response.Content.ReadAsStringAsync(cancellation));
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            process.Dispose();
            if (Directory.Exists(DataDirectory)) Directory.Delete(DataDirectory, true);
        }
    }
}
