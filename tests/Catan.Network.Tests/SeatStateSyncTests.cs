using Catan.AI;
using Catan.Core.ThreeKingdoms;
using Catan.Network.Contracts;
using Xunit;
using M2 = Catan.Core.M2;
using M3 = Catan.Core.M3;
using M4 = Catan.Core.M4;
using M5 = Catan.Core.M5;

namespace Catan.Network.Tests;

public sealed class SeatStateSyncTests
{
    [Fact]
    public void DirectWriterPreservesEscapesOptionalFieldsAndStrictLegacyCompatibility()
    {
        var source = new NetworkResponse { Success = true, Message = "中文 / \\\"\n\r\t\b\f\0 😀", Room = new RoomSummary { ThreeKingdomsPackageVersion = null, Seats = new[] { new SeatSummary { DisplayName = "名称" } } } };
        var encoded = WireJson.SerializeDirect(source);
        var restored = WireJson.DeserializeLegacy<NetworkResponse>(encoded);
        Assert.Equal(WireJson.Serialize(source), WireJson.Serialize(restored));
        Assert.DoesNotContain("ThreeKingdomsPackageVersion", encoded);
        Assert.ThrowsAny<Exception>(() => WireJson.SerializeObjectDirect(new { Authority = true }));
    }
    public static IEnumerable<object[]> Maps()
    {
        yield return new object[] { "base", "base" };
        yield return new object[] { "cities-knights", "base" };
        foreach (var map in M3.SeafarersScenarios.Ids) yield return new object[] { "seafarers", map };
        foreach (var map in M5.CombinedScenarios.Ids) yield return new object[] { "combined", map };
    }
    [Theory, MemberData(nameof(Maps))]
    public void FourSeatViewsAndHistoryRemainIdenticalAcrossMaps(string rules, string map)
    {
        var scenario = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "scenario.json"));
        dynamic game = rules switch
        {
            "base" => (object)M2.BaseGameSession.Create(scenario, 4, 97),
            "cities-knights" => M4.CitiesKnightsGameSession.Create(scenario, 4, 97),
            "seafarers" => M3.SeafarersGameSession.Create(map, 4, 97),
            _ => M5.CombinedGameSession.Create(map, 4, 97)
        };
        dynamic policy = rules switch { "base" => (object)new BaseGameAi(), "cities-knights" => new CitiesKnightsAi(), "seafarers" => new SeafarersAi(), _ => new CombinedAi() };
        var senders = Enumerable.Range(1, 4).Select(_ => new SeatStateSender()).ToArray();
        var readers = Enumerable.Range(1, 4).Select(n => new SeatStateReceiver("room", "P" + n, "c" + n)).ToArray();
        for (int step = 0; step <= 16; step++)
        {
            for (int n = 1; n <= 4; n++)
            {
                object view = game.GetPlayerView("P" + n);
                var source = Source(step + 1, WireJson.SerializeObject(view), WireJson.SerializeObject((object)game.GetHistory("P" + n, 0L)), "P" + n, "c" + n, rules);
                var wire = senders[n - 1].Encode(source);
                var incoming = WireJson.Deserialize<NetworkResponse>(WireJson.Serialize(wire));
                var application = readers[n - 1].Prepare(incoming);
                var restored = WireJson.Deserialize(application.Response.ViewJson, view.GetType());
                Assert.Equal(source.ViewJson, WireJson.SerializeObject(restored));
                foreach (string forbidden in new[] { "RandomState", "ProcessedCommands", "FogTileContents", "OpponentHands", "GameSave", "CredentialHash" }) Assert.DoesNotContain(forbidden, wire.ViewJson);
                application.Commit();
            }
            if (step == 16) break;
            dynamic first = game.GetPlayerView("P1");
            string actor = first.PendingDecision?.PlayerId ?? first.ActivePlayerId;
            dynamic command = policy.Decide(game.GetPlayerView(actor)); command.Id = "sync-" + step;
            Assert.True((bool)game.Execute(command).Success);
        }
    }

    [Theory]
    [InlineData("cities-knights", false)][InlineData("combined", false)]
    [InlineData("cities-knights", true)][InlineData("combined", true)]
    public void B4AndB5PrivateChoicesAndCommandRetriesWorkWithBothReplyShapes(string rules, bool b5)
    {
        using var f = new ThreeKingdomsB4RoomTests.Fixture(rules, 4, package: b5 ? GeneralCatalog.B5PackageVersion : GeneralCatalog.B4PackageVersion);
        var senders = f.Clients.Select(_ => new SeatStateSender()).ToArray();
        var readers = f.Clients.Select(c => new SeatStateReceiver(c.RoomId, c.SeatId, c.ConnectionId)).ToArray();
        for (int step = 0; step < 36; step++)
        {
            var snapshots = f.Clients.Select(f.Snapshot).ToArray();
            for (int n = 0; n < snapshots.Length; n++)
            {
                var source = f.Directory.Handle(f.Request("Sync", f.Clients[n]));
                var wire = senders[n].Encode(source);
                var application = readers[n].Prepare(wire);
                Type type = rules == "combined" ? typeof(M5.PlayerView) : typeof(M4.PlayerView);
                Assert.Equal(source.ViewJson, WireJson.SerializeObject(WireJson.Deserialize(application.Response.ViewJson, type)));
                Assert.DoesNotContain("GeneralDrawOrder", wire.ViewJson);
                var otherCandidates = snapshots.Where((_, index) => index != n).SelectMany(s => f.Tk(s).PendingDecision?.CandidateIds ?? Array.Empty<string>()).ToArray();
                foreach (var candidate in otherCandidates) Assert.DoesNotContain("\\\"" + candidate + "\\\"", wire.ViewJson);
                application.Commit();
            }
            var own = snapshots.FirstOrDefault(s => f.Tk(s).PendingDecision?.PlayerId == s.SeatId || f.Tk(s).SkillDecision?.PlayerId == s.SeatId);
            if (own == null)
            {
                dynamic v = rules == "combined" ? (object)WireJson.Deserialize<M5.PlayerView>(snapshots[0].ViewJson) : WireJson.Deserialize<M4.PlayerView>(snapshots[0].ViewJson);
                string actor = v.Discards.Length > 0 ? v.Discards[0].PlayerId : v.TradeOffer?.NextResponder() ?? v.PendingDecision?.PlayerId ?? v.ActivePlayerId;
                own = snapshots.Single(s => s.SeatId == actor);
            }
            var command = f.Decide(own);
            var result = f.Submit(own, command, own.Revision); Assert.True(result.Success, result.Message);
            Assert.True(f.Submit(own, command, own.Revision).IsDuplicate);
        }
    }

    [Fact]
    public void CoalescingPreservesMultipleEventsAndReplacementsAndDetectsGapsWithoutAdvancing()
    {
        var sender = new SeatStateSender(); var reader = new SeatStateReceiver("room", "P1", "c1");
        var initial = sender.Encode(Source(1, View("a", "b"), "[{\"Sequence\":1,\"SkillEventId\":\"a\",\"Action\":\"开始\"},{\"Sequence\":1,\"SkillEventId\":\"b\"}]"));
        var first = reader.Prepare(initial); first.Commit();
        // 没有发送中间快照，编码时直接从实际发送的第一份状态计算差量。
        var source = Source(3, View("b", "c"), "[{\"Sequence\":1,\"SkillEventId\":\"a\",\"Action\":\"完成\"},{\"Sequence\":1,\"SkillEventId\":\"b\"},{\"Sequence\":2,\"SkillEventId\":\"c\"}]");
        var update = sender.Encode(source);
        var packet = WireJson.Deserialize<SeatStatePacket>(update.ViewJson);
        Assert.False(packet.Full);
        Assert.Contains(packet.Changes, c => c.Key == "a" && c.Remove);
        Assert.Contains(packet.Changes, c => c.Key == "c" && !c.Remove);
        Assert.Equal(2, WireJson.Deserialize<Catan.Core.GameHistoryEntry[]>(packet.EventUpdatesJson).Length);
        var invalid = update.Copy(); packet.BaseRevision = 2; invalid.ViewJson = WireJson.Serialize(packet);
        Assert.ThrowsAny<Exception>(() => reader.Prepare(invalid));
        var pending = reader.Prepare(update);
        Assert.Equal(View("b", "c"), pending.Response.ViewJson); pending.Commit();
        Assert.Null(reader.Prepare(update).Response.ViewJson);
        Assert.Null(reader.Prepare(initial).Response.ViewJson);
        var next = sender.Encode(Source(4, View("c"), source.PresentationHistoryJson));
        Assert.ThrowsAny<Exception>(() => new SeatStateReceiver("room", "P1", "c1").Prepare(next));
        var recovered = new SeatStateReceiver("room", "P1", "c1").Prepare(new SeatStateSender().Encode(Source(4, View("c"), source.PresentationHistoryJson)));
        Assert.NotNull(recovered.Response.ViewJson);
    }

    [Theory]
    [InlineData("other", "P1", "c1")][InlineData("room", "P2", "c1")][InlineData("room", "P1", "old")]
    public void FullSnapshotsCannotCrossRoomSeatOrConnection(string room, string seat, string connection)
    {
        var wire = new SeatStateSender().Encode(Source(1, View("a")));
        Assert.ThrowsAny<Exception>(() => new SeatStateReceiver(room, seat, connection).Prepare(wire));
    }

    [Fact]
    public void RestartRequiresFullSnapshotAndLatePreviousGameCannotRollBack()
    {
        var sender = new SeatStateSender(); var reader = new SeatStateReceiver("room", "P1", "c1");
        var old = sender.Encode(Source(1, View("a"))); var first = reader.Prepare(old); first.Commit();
        var restarted = Source(2, View("b")); restarted.Room.GameNumber = 2;
        var next = sender.Encode(restarted);
        Assert.True(WireJson.Deserialize<SeatStatePacket>(next.ViewJson).Full);
        var accepted = reader.Prepare(next); accepted.Commit();
        Assert.Null(reader.Prepare(old).Response.ViewJson);
    }

    [Theory]
    [InlineData("hash")][InlineData("map")][InlineData("events")][InlineData("version")][InlineData("unknown")]
    public void InvalidPacketDoesNotReplaceAcceptedBaseline(string damage)
    {
        var sender = new SeatStateSender(); var reader = new SeatStateReceiver("room", "P1", "c1");
        var initial = reader.Prepare(sender.Encode(Source(1, View("a")))); initial.Commit();
        var valid = sender.Encode(Source(2, View("b"), "[{\"Sequence\":2}]"));
        var broken = valid.Copy(); var packet = WireJson.Deserialize<SeatStatePacket>(broken.ViewJson);
        switch (damage) { case "hash": packet.ViewHash = "错误"; break; case "map": packet.MapVersion = "错误"; break; case "events": packet.EventBaseVersion = 42; break; case "version": packet.Version = 99; break; }
        broken.ViewJson = WireJson.Serialize(packet);
        if (damage == "unknown") broken.ViewJson = broken.ViewJson.Insert(1, "\"Authority\":{},");
        Assert.ThrowsAny<Exception>(() => reader.Prepare(broken));
        var accepted = reader.Prepare(valid); accepted.Commit(); Assert.Equal(View("b"), accepted.Response.ViewJson);
    }

    internal static NetworkResponse Source(long revision, string view, string history = "[]", string seat = "P1", string connection = "c1", string rules = "base") => new()
    {
        Success = true, Revision = revision, CommittedRevision = revision, RoomId = "room", SeatId = seat, ConnectionId = connection, RuleSet = rules,
        Room = new RoomSummary { GameNumber = 1, Revision = revision, Started = true, RuleSet = rules }, ViewJson = view, PresentationHistoryJson = history
    };
    static string View(params string[] ids) => "{\"Board\":{\"Tiles\":[" + string.Join(",", ids.Select(id => "{\"id\":\"" + id + "\",\"resource\":\"ore\"}")) + "]},\"Padding\":\"" + new string('x', 4096) + "\"}";
}
