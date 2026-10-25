using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Catan.AI;
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
using Xunit.Abstractions;
using M5 = Catan.Core.M5;

namespace Catan.Server.Tests;

public sealed class IncrementalTransportTests(ITestOutputHelper output)
{
    [Fact]
    public async Task SameFourSeatScriptMeasuresBothWireShapesAndRestoresDisconnectedStream()
    {
        string logs = Path.Combine(Path.GetTempPath(), "catan-sync-" + Guid.NewGuid().ToString("N"));
        var diagnostics = new CommunicationDiagnostics(logs);
        using var rooms = new RoomDirectory(new Storage(), Scenario(), new RoomDirectoryOptions { Diagnostics = diagnostics.RecordRoom });
        var transport = new NetworkTransport(rooms, new ServerOptions(), diagnostics);
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build(); app.UseWebSockets(); app.Map("/ws", transport.WebSocketAsync); app.MapPost("/api/request", transport.HttpAsync);
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var socketAddress = new Uri(address.Replace("http://", "ws://") + "/ws");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var identities = new List<NetworkResponse>();
        var host = rooms.Handle(new NetworkRequest { Operation = "Create", ExpectedRulesVersion = NetworkProtocol.RulesVersion("combined"), ExpectedScenarioVersion = NetworkProtocol.ScenarioVersion("combined"), Payload = WireJson.Serialize(new CreateRoomPayload { Settings = new RoomSettings { RuleSet = "combined", ScenarioId = "heading-for-new-shores", PlayerCount = 4 } }) });
        Assert.True(host.Success); identities.Add(host);
        for (int n = 1; n < 4; n++)
        {
            var joined = rooms.Handle(new NetworkRequest { Operation = "Join", RoomId = host.RoomId, ExpectedRulesVersion = NetworkProtocol.RulesVersion("combined"), ExpectedScenarioVersion = NetworkProtocol.ScenarioVersion("combined"), Payload = "{}" });
            Assert.True(joined.Success); identities.Add(joined);
        }
        foreach (var identity in identities)
        {
            var ready = Request("Ready", identity); ready.ExpectedRevision = rooms.Handle(Request("Get", identity)).Revision; ready.Payload = "{\"Ready\":true}";
            Assert.True(rooms.Handle(ready).Success);
        }
        var start = Request("Start", host); start.ExpectedRevision = rooms.Handle(Request("Get", host)).Revision; Assert.True(rooms.Handle(start).Success);
        var legacy = identities.Select(i => new Peer(i, false)).ToArray();
        var delta = identities.Select(i => new Peer(i, true)).ToArray();
        var arrivals = new List<double>(); long sharedReceiptBytes = 0;
        try
        {
            // 同一权威桌、同一席位、同一提交；两组观察连接分别测旧形状和新形状。
            foreach (var peer in legacy.Concat(delta)) await peer.Connect(socketAddress, deadline.Token);
            long initialLegacy = legacy.Sum(p => p.Bytes), initialDelta = delta.Sum(p => p.Bytes);
            foreach (var peer in legacy.Concat(delta)) peer.ResetMeasurement();
            for (int step = 0; step < 40; step++)
            {
                var state = WireJson.Deserialize<M5.PlayerView>(delta[0].Latest.ViewJson);
                string actor = state.Discards.Length > 0 ? state.Discards[0].PlayerId : state.TradeOffer?.NextResponder() ?? state.PendingDecision?.PlayerId ?? state.ActivePlayerId;
                var identity = identities.Single(i => i.SeatId == actor);
                var current = rooms.Handle(Request("Get", identity));
                var command = new CombinedAi().Decide(WireJson.Deserialize<M5.PlayerView>(current.ViewJson)); command.Id = "同步动作-" + step;
                var watching = legacy.Concat(delta).Select(p => p.WaitFor(current.Revision + 1)).ToArray();
                var request = Request("CommandReceipt", identity); request.ExpectedRevision = current.Revision; request.CommandId = command.Id; request.Payload = WireJson.Serialize(command);
                long started = Stopwatch.GetTimestamp(); var receipt = rooms.Handle(request); Assert.True(receipt.Success, receipt.Message);
                sharedReceiptBytes += Encoding.UTF8.GetByteCount(WireJson.Serialize(receipt));
                var ticks = await Task.WhenAll(watching).WaitAsync(TimeSpan.FromSeconds(5), deadline.Token);
                arrivals.AddRange(ticks.Skip(4).Select(t => Stopwatch.GetElapsedTime(started, t).TotalMilliseconds));
                for (int n = 0; n < 4; n++)
                {
                    Assert.Equal(Normalize(legacy[n].Latest.ViewJson), Normalize(delta[n].Latest.ViewJson));
                    Assert.Equal(identities[n].SeatId, WireJson.Deserialize<M5.PlayerView>(delta[n].Latest.ViewJson).PlayerId);
                }
            }
            long oldBytes = legacy.Sum(p => p.Bytes) + sharedReceiptBytes, newBytes = delta.Sum(p => p.Bytes) + sharedReceiptBytes;
            Assert.True(newBytes < oldBytes * .3, $"旧形状{oldBytes}，新形状{newBytes}");
            Assert.All(delta, p => Assert.True(p.Deltas >= 30));
            foreach (var peer in legacy) await peer.DisposeAsync();
            await Task.Delay(50, deadline.Token);
            var fourStreamStart = DateTimeOffset.UtcNow; var fourStreamArrivals = new List<double>();
            for (int step = 40; step < 70; step++)
            {
                var state = WireJson.Deserialize<M5.PlayerView>(delta[0].Latest.ViewJson);
                string actor = state.Discards.Length > 0 ? state.Discards[0].PlayerId : state.TradeOffer?.NextResponder() ?? state.PendingDecision?.PlayerId ?? state.ActivePlayerId;
                var identity = identities.Single(i => i.SeatId == actor); var current = rooms.Handle(Request("Get", identity));
                var command = new CombinedAi().Decide(WireJson.Deserialize<M5.PlayerView>(current.ViewJson)); command.Id = "四流动作-" + step;
                var waiting = delta.Select(p => p.WaitFor(current.Revision + 1)).ToArray();
                var request = Request("CommandReceipt", identity); request.ExpectedRevision = current.Revision; request.CommandId = command.Id; request.Payload = WireJson.Serialize(command);
                long started = Stopwatch.GetTimestamp(); Assert.True(rooms.Handle(request).Success);
                var received = await Task.WhenAll(waiting).WaitAsync(TimeSpan.FromSeconds(5), deadline.Token);
                fourStreamArrivals.AddRange(received.Select(t => Stopwatch.GetElapsedTime(started, t).TotalMilliseconds));
                for (int n = 0; n < 4; n++) Assert.Equal(Normalize(rooms.Handle(Request("Get", identities[n])).ViewJson), Normalize(delta[n].Latest.ViewJson));
            }
            // 新传输连接不沿用旧流的增量基准；相同身份无需轮换或重复执行命令。
            await delta[0].DisposeAsync(); delta[0] = new Peer(identities[0], true); await delta[0].Connect(socketAddress, deadline.Token);
            Assert.True(delta[0].Fulls >= 1); Assert.Equal(Normalize(rooms.Handle(Request("Get", identities[0])).ViewJson), Normalize(delta[0].Latest.ViewJson));
            foreach (var peer in legacy.Concat(delta)) await peer.DisposeAsync();
            await app.StopAsync(); await diagnostics.DisposeAsync();
            var triggers = new List<double>(); var fourStreamTriggers = new List<double>();
            foreach (var line in Directory.EnumerateFiles(logs, "*.jsonl").SelectMany(File.ReadLines))
            {
                using var document = JsonDocument.Parse(line); var data = document.RootElement.GetProperty("data");
                if (data.GetProperty("event").GetString() == "push.triggered")
                {
                    double elapsed = data.GetProperty("elapsedMs").GetDouble(); triggers.Add(elapsed);
                    if (DateTimeOffset.Parse(document.RootElement.GetProperty("timestampUtc").GetString()!) >= fourStreamStart) fourStreamTriggers.Add(elapsed);
                }
            }
            var report = new
            {
                scope = "单机回环同一四席组合桌40条动作，两组各四条真实WS分别观察全量和增量；含相同命令回执正文；保存接口使用空存储，不测磁盘；不含TCP头、网卡出口、4Mbps整形或四设备帧率",
                commands = 40, legacyBytes = oldBytes, deltaBytes = newBytes, reduction = 1 - newBytes / (double)oldBytes,
                initialLegacyBytes = initialLegacy, initialDeltaBytes = initialDelta,
                mixedObservationCommitToQueue = Stats(triggers.Take(320).ToArray()), actionToDeltaArrival = Stats(arrivals.ToArray()),
                fourStreamsCommitToQueue = Stats(fourStreamTriggers.ToArray()), fourStreamsActionToArrival = Stats(fourStreamArrivals.ToArray())
            };
            string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }); output.WriteLine(json);
            string? destination = Environment.GetEnvironmentVariable("CATAN_SYNC_MEASUREMENT_OUTPUT"); if (destination != null) await File.WriteAllTextAsync(destination, json, deadline.Token);
        }
        finally
        {
            foreach (var peer in legacy.Concat(delta)) await peer.DisposeAsync();
            await app.StopAsync(); await diagnostics.DisposeAsync();
        }
    }
    static object Stats(double[] values)
    {
        Array.Sort(values); double P(double p) => values.Length == 0 ? 0 : values[(int)Math.Ceiling(p * values.Length) - 1];
        return new { samples = values.Length, p50 = P(.5), p95 = P(.95), p99 = P(.99), maximum = values.Length == 0 ? 0 : values[^1] };
    }
    static string Normalize(string json) => WireJson.Serialize(WireJson.Deserialize<M5.PlayerView>(json));
    static NetworkRequest Request(string operation, NetworkResponse identity) => new() { RequestId = Guid.NewGuid().ToString("N"), Operation = operation, RoomId = identity.RoomId, Credential = identity.Credential, ConnectionId = identity.ConnectionId, ExpectedRevision = identity.Revision, RuleSet = "combined" };
    sealed class Peer(NetworkResponse identity, bool delta) : IAsyncDisposable
    {
        readonly ClientWebSocket socket = new(); readonly CancellationTokenSource stopped = new(); readonly object gate = new();
        readonly SeatStateReceiver reader = new(identity.RoomId, identity.SeatId, identity.ConnectionId);
        Task? receiver; TaskCompletionSource<long>? waiting; long target; NetworkResponse latest = new(); int disposed;
        long bytes; public long Bytes => Interlocked.Read(ref bytes); public int Deltas, Fulls;
        public NetworkResponse Latest => Volatile.Read(ref latest);
        public async Task Connect(Uri address, CancellationToken cancellation)
        {
            await socket.ConnectAsync(address, cancellation); receiver = Receive();
            var wait = WaitFor(identity.Revision);
            var request = Request(delta ? "Sync" : "Get", identity);
            await socket.SendAsync(Encoding.UTF8.GetBytes(WireJson.Serialize(request)), WebSocketMessageType.Text, true, cancellation);
            await wait.WaitAsync(TimeSpan.FromSeconds(5), cancellation);
        }
        public Task<long> WaitFor(long revision)
        {
            lock (gate)
            {
                if (Latest.ViewJson != null && Latest.Revision >= revision) return Task.FromResult(Stopwatch.GetTimestamp());
                target = revision; waiting = new(TaskCreationOptions.RunContinuationsAsynchronously); return waiting.Task;
            }
        }
        public void ResetMeasurement() { Interlocked.Exchange(ref bytes, 0); Deltas = Fulls = 0; }
        async Task Receive()
        {
            try
            {
                var buffer = new byte[16384];
                while (!stopped.IsCancellationRequested)
                {
                    using var message = new MemoryStream(); WebSocketReceiveResult part;
                    do { part = await socket.ReceiveAsync(buffer, stopped.Token); if (part.MessageType != WebSocketMessageType.Text) throw new IOException("同步测量连接关闭。"); message.Write(buffer, 0, part.Count); } while (!part.EndOfMessage);
                    Interlocked.Add(ref bytes, message.Length);
                    var response = WireJson.Deserialize<NetworkResponse>(Encoding.UTF8.GetString(message.ToArray()));
                    if (response.Code == NetworkProtocol.SeatStateCode)
                    {
                        var packet = WireJson.Deserialize<SeatStatePacket>(response.ViewJson); if (packet.Full) Fulls++; else Deltas++;
                        var application = reader.Prepare(response); response = application.Response; application.Commit();
                    }
                    if (response.ViewJson == null || response.Revision < Latest.Revision) continue;
                    Volatile.Write(ref latest, response);
                    lock (gate) if (response.Revision >= target) waiting?.TrySetResult(Stopwatch.GetTimestamp());
                }
            }
            catch (Exception error) { lock (gate) waiting?.TrySetException(error); if (!stopped.IsCancellationRequested) throw; }
        }
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            stopped.Cancel(); socket.Abort(); if (receiver != null) try { await receiver; } catch { }
            socket.Dispose(); stopped.Dispose();
        }
    }
    static string Scenario()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        { string path = Path.Combine(directory.FullName, "docs", "acceptance", "v0.1", "scenario.json"); if (File.Exists(path)) return File.ReadAllText(path); }
        throw new FileNotFoundException("缺少场景夹具。");
    }
    sealed class Storage : IRoomStorage
    {
        public IEnumerable<string> LoadAll() => Array.Empty<string>(); public void Commit(string room, string json) { } public void Delete(string room) { } public void Dispose() { }
    }
}
