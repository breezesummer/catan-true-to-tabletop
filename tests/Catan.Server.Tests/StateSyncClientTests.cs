using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using Catan.Network.Contracts;
using Catan.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Catan.Server.Tests;

public sealed class StateSyncClientTests
{
    [Fact]
    public async Task ReconnectDoesNotUnlockUntilTheNewConnectionHasCompleteEvents()
    {
        var client = new M7Client(); Field("connectionId").SetValue(client, "c1"); Field("automaticReconnect").SetValue(client, false);
        try
        {
            await Apply(client, new SeatStateSender(), new SeatStateReceiver("room", "P1", "c1"), State(1, "开始"));
            var handshake = State(1, "开始"); handshake.ConnectionId = "c2"; handshake.Code = NetworkProtocol.CompactRepliesCode;
            Method("Apply").Invoke(client, new object[] { handshake, true, false }); Assert.False(client.CanInteract);
            var complete = State(1, "完成"); complete.ConnectionId = "c2";
            await Apply(client, new SeatStateSender(), new SeatStateReceiver("room", "P1", "c2"), complete);
            Assert.True(client.CanInteract); Assert.Equal("完成", client.History.Single(e => e.SkillEventId == "a").Action);
        }
        finally { client.EndSession(); }
    }
    [Fact]
    public void LobbyRecoveryDoesNotWaitForNonexistentGameViewOrHistory()
    {
        var client = new M7Client(); Field("connectionId").SetValue(client, "c1");
        Field("syncRecovery").SetValue(client, true); Field("recoverEventStream").SetValue(client, true);
        Field("recoveryHistoryPaging").SetValue(client, true);
        try
        {
            Method("Apply").Invoke(client, new object[] { new NetworkResponse { Success = true, RoomId = "room", SeatId = "P1", Revision = 1, HumanControlAvailable = true, Room = new RoomSummary { Started = false } }, false, false });
            Assert.True(client.CanInteract);
        }
        finally { client.EndSession(); }
    }
    [Fact]
    public async Task HttpRecoveryWaitsForCompleteEventsEvenWhenTheStateRevisionIsAlreadyApplied()
    {
        var client = new M7Client(); Field("connectionId").SetValue(client, "c1"); Field("automaticReconnect").SetValue(client, false);
        try
        {
            await Apply(client, new SeatStateSender(), new SeatStateReceiver("room", "P1", "c1"), State(1, "开始")); Assert.True(client.CanInteract);
            Field("syncRecovery").SetValue(client, true); Field("recoverEventStream").SetValue(client, true);
            var decoder = (M7ResponseDecoder)Field("responseDecoder").GetValue(client)!;
            var ordinary = State(2, "开始"); ordinary.PresentationHistoryJson = "[{\"Sequence\":2}]";
            var partial = await decoder.Decode(Encoding.UTF8.GetBytes(WireJson.Serialize(ordinary)), "base", "Get", null!, CancellationToken.None);
            Method("Apply").Invoke(client, new object[] { partial, false, false }); Assert.False(client.CanInteract);
            var complete = State(2, "完成"); complete.Code = NetworkProtocol.FullEventsCode;
            complete.PresentationHistoryJson = "[{\"Sequence\":1,\"SkillEventId\":\"a\",\"Action\":\"完成\"},{\"Sequence\":2}]";
            var recovered = await decoder.Decode(Encoding.UTF8.GetBytes(WireJson.Serialize(complete)), "base", "Sync", null!, CancellationToken.None);
            Method("Apply").Invoke(client, new object[] { recovered, false, false });
            Assert.True(client.CanInteract); Assert.Equal("完成", client.History.Single(e => e.SkillEventId == "a").Action);
        }
        finally { client.EndSession(); }
    }
    static FieldInfo Field(string name) => typeof(M7Client).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    static MethodInfo Method(string name) => typeof(M7Client).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    static NetworkResponse State(long revision, string action) => new()
    {
        Success = true, RoomId = "room", SeatId = "P1", ConnectionId = "c1", HumanControlAvailable = true, Revision = revision, RuleSet = "base",
        Room = new RoomSummary { Started = true, GameNumber = 1, RuleSet = "base" },
        ViewJson = "{\"PlayerId\":\"P1\",\"Events\":[{\"Sequence\":" + revision + "}]}",
        PresentationHistoryJson = "[{\"Sequence\":1,\"SkillEventId\":\"a\",\"Action\":\"" + action + "\"}]"
    };
    static async Task Apply(M7Client client, SeatStateSender sender, SeatStateReceiver reader, NetworkResponse source)
    {
        var decoder = (M7ResponseDecoder)Field("responseDecoder").GetValue(client)!;
        var response = await decoder.Decode(Encoding.UTF8.GetBytes(WireJson.Serialize(sender.Encode(source))), "base", "Push", null!, CancellationToken.None, reader);
        Method("Apply").Invoke(client, new object[] { response, false, false });
    }

    [Fact]
    public async Task LateHistoryPageCannotUndoSkillResultAlreadyAppliedByReliableStream()
    {
        var client = new M7Client(); client.ConfigureEndpoint("http://127.0.0.1:47771", "");
        Field("connectionId").SetValue(client, "c1");
        var handler = new HistoryHandler(); ((HttpClient)Field("http").GetValue(client)!).Dispose(); Field("http").SetValue(client, new HttpClient(handler));
        var sender = new SeatStateSender(); var reader = new SeatStateReceiver("room", "P1", "c1");
        try
        {
            await Apply(client, sender, reader, State(1, "开始"));
            Field("historyDirty").SetValue(client, true); Method("FetchHistory").Invoke(client, null);
            await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Field("automaticReconnect").SetValue(client, false);
            client.Playing = true;
            var next = State(2, "完成");
            next.PresentationHistoryJson = "[{\"Sequence\":1,\"SkillEventId\":\"a\",\"Action\":\"完成\"},{\"Sequence\":2}]";
            await Apply(client, sender, reader, next);
            Assert.Equal("完成", client.History.Single(e => e.SkillEventId == "a").Action);
            handler.Release.TrySetResult(true);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            do { await Task.Delay(5, deadline.Token); Method("Update").Invoke(client, null); } while ((bool)Field("historyBusy").GetValue(client)!);
            Assert.Equal("完成", client.History.Single(e => e.SkillEventId == "a").Action);
            Assert.Equal(2, client.History.Last().Sequence);
        }
        finally { handler.Release.TrySetResult(true); client.EndSession(); }
    }

    [Fact]
    public async Task NewClientFallsBackToOldSocketOperationWithoutWaitingOneHeartbeatInterval()
    {
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build(); app.UseWebSockets();
        var operations = new List<string>(); var complete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Map("/ws", async context =>
        {
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            try
            {
                var buffer = new byte[8192];
                for (int i = 0; i < 2; i++)
                {
                    using var message = new MemoryStream(); WebSocketReceiveResult part;
                    do { part = await socket.ReceiveAsync(buffer, context.RequestAborted); message.Write(buffer, 0, part.Count); } while (!part.EndOfMessage);
                    var request = WireJson.Deserialize<NetworkRequest>(Encoding.UTF8.GetString(message.ToArray())); operations.Add(request.Operation);
                    var response = State(1, "开始"); response.RequestId = request.RequestId; response.Success = request.Operation != "Sync"; response.Code = response.Success ? null! : "UnknownOperation";
                    await socket.SendAsync(Encoding.UTF8.GetBytes(WireJson.Serialize(response)), WebSocketMessageType.Text, true, context.RequestAborted);
                }
                complete.TrySetResult();
                await Task.Delay(Timeout.Infinite, context.RequestAborted);
            }
            catch (OperationCanceledException) { }
        });
        await app.StartAsync(); var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var client = new M7Client(); client.ConfigureEndpoint(address, ""); Field("connectionId").SetValue(client, "c1"); Field("credential").SetValue(client, "private-credential");
        Method("Apply").Invoke(client, new object[] { State(1, "开始"), false, false });
        try
        {
            Method("StartSocket").Invoke(client, null);
            await complete.Task.WaitAsync(TimeSpan.FromSeconds(4));
            Assert.Equal(new[] { "Sync", "Get" }, operations);
        }
        finally { client.EndSession(); await app.StopAsync(); }
    }

    [Fact]
    public async Task DeferredSnapshotsHaveAByteBoundAndCoalescingKeepsTheLatestBaseline()
    {
        using var output = new SocketOutbox(responseCapacity: 8, byteCapacity: 4096);
        OutboundMessage Message(long revision, int size, bool push) => new(Array.Empty<byte>(), new NetworkResponse { Revision = revision, ViewJson = new string('x', size) }, new CommunicationEvent(), push, true);
        Assert.True(output.TryWrite(Message(1, 2048, true), out _));
        Assert.True(output.TryWrite(Message(2, 2048, true), out var coalesced)); Assert.True(coalesced);
        Assert.False(output.TryWrite(Message(3, 2048, false), out _));
        Assert.Equal(2, (await output.ReadAsync(CancellationToken.None)).Response.Revision);
        Assert.True(output.TryWrite(Message(4, 2048, false), out _));
        Assert.Equal(4, (await output.ReadAsync(CancellationToken.None)).Response.Revision);
    }
    sealed class HistoryHandler : HttpMessageHandler
    {
        public TaskCompletionSource<bool> Entered = new(TaskCreationOptions.RunContinuationsAsynchronously), Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            Entered.TrySetResult(true); await Release.Task.WaitAsync(cancellation);
            var response = State(1, "开始"); response.ViewJson = response.PresentationHistoryJson = null!; response.HistoryJson = "[{\"Sequence\":1,\"SkillEventId\":\"a\",\"Action\":\"开始\"}]"; response.HistoryCursor = 1;
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(WireJson.Serialize(response)) };
        }
    }
}
