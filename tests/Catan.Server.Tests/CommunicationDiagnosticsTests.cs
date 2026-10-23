using System.Text.Json;
using Catan.Network.Contracts;
using Catan.Server;
using Xunit;

namespace Catan.Server.Tests;

public sealed class CommunicationDiagnosticsTests
{
    [Fact]
    public async Task JsonLinesCorrelateRequestsWithoutCredentialsPayloadsOrArbitraryIdentifiers()
    {
        var directory = TemporaryDirectory();
        try
        {
            var requestId = Guid.NewGuid().ToString("N");
            var commandId = Guid.NewGuid().ToString("N");
            var secret = new string('A', 64);
            await using (var log = new CommunicationDiagnostics(directory))
            {
                var request = new NetworkRequest
                {
                    RequestId = requestId, CommandId = commandId, RoomId = "012345ABCDEF", Operation = "Command",
                    ConnectionId = secret, Credential = "不得记录恢复凭据", EnrollmentCredential = "不得记录登记凭据", Payload = "不得记录手牌和口令"
                };
                log.Record(CommunicationDiagnostics.Request("request.received", "ws", "socket", request) with { Bytes = 125 });
                log.Record(CommunicationDiagnostics.Request("request.received", "http", null, new NetworkRequest
                { RequestId = "不要记录标识内的秘密", CommandId = "not-a-generated-id", RoomId = "room-name-secret", Operation = "不要记录操作内的秘密" }));
            }
            var lines = File.ReadAllLines(Assert.Single(Directory.GetFiles(directory, "communication-*.jsonl")));
            Assert.Equal(3, lines.Length);
            var all = string.Join('\n', lines);
            Assert.Contains(requestId, all); Assert.Contains(commandId, all); Assert.Contains("012345ABCDEF", all);
            Assert.DoesNotContain(secret, all); Assert.DoesNotContain("不得记录", all); Assert.DoesNotContain("不要记录", all);
            Assert.DoesNotContain("not-a-generated-id", all); Assert.DoesNotContain("room-name-secret", all);
            foreach (var line in lines)
            {
                using var document = JsonDocument.Parse(line);
                Assert.Equal(1, document.RootElement.GetProperty("schema").GetInt32());
                Assert.True(DateTimeOffset.TryParse(document.RootElement.GetProperty("timestampUtc").GetString(), out _));
            }
            using var received = JsonDocument.Parse(lines[1]);
            Assert.StartsWith("sha256:", received.RootElement.GetProperty("data").GetProperty("connectionId").GetString());
            Assert.Equal(125, received.RootElement.GetProperty("data").GetProperty("bytes").GetInt32());
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task RotationKeepsBoundedValidJsonFiles()
    {
        var directory = TemporaryDirectory();
        try
        {
            await using (var log = new CommunicationDiagnostics(directory, capacity: 4096, maxFileBytes: 1024, maxFiles: 3))
                for (var i = 0; i < 100; i++) log.Record(new() { Event = "request.received", RequestId = Guid.NewGuid().ToString("N"), Bytes = i });
            var files = Directory.GetFiles(directory, "communication-*.jsonl");
            Assert.Equal(3, files.Length);
            Assert.All(files, file =>
            {
                Assert.InRange(new FileInfo(file).Length, 1, 1500);
                Assert.All(File.ReadAllLines(file), line => { using var json = JsonDocument.Parse(line); });
            });
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task UnavailableLogDirectoryDoesNotThrowIntoGameTransport()
    {
        var path = Path.GetTempFileName();
        try
        {
            await using var log = new CommunicationDiagnostics(path);
            for (int i = 0; i < 100; i++) log.Record(new() { Event = "request.received" });
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task SlowReceiverKeepsOnlyLatestPushAndEveryCommandReplyInOrder()
    {
        using var output = new SocketOutbox(responseCapacity: 2);
        for (int revision = 1; revision <= 100; revision++)
        {
            Assert.True(output.TryWrite(Message(revision, true), out var coalesced));
            Assert.Equal(revision > 1, coalesced);
        }
        Assert.True(output.TryWrite(Message(101, false), out _));
        Assert.True(output.TryWrite(Message(102, false), out _));
        Assert.False(output.TryWrite(Message(103, false), out _));
        Assert.Equal(3, output.Count);
        Assert.Equal(101, (await output.ReadAsync(CancellationToken.None)).Response.Revision);
        Assert.Equal(102, (await output.ReadAsync(CancellationToken.None)).Response.Revision);
        Assert.Equal(100, (await output.ReadAsync(CancellationToken.None)).Response.Revision);
        Assert.Equal(0, output.Count);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => output.ReadAsync(deadline.Token));
    }

    private static OutboundMessage Message(long revision, bool push) => new([1, 2], new NetworkResponse { Revision = revision }, new CommunicationEvent(), push);
    private static string TemporaryDirectory() => Path.Combine(Path.GetTempPath(), "catan-diagnostics-" + Guid.NewGuid().ToString("N"));
}
