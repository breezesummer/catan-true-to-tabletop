using System.Collections.Concurrent;
using Catan.AI;
using Catan.Network.Contracts;
using Catan.Rooms;
using Xunit;
using M2 = Catan.Core.M2;

namespace Catan.Network.Tests;

public sealed class RoomDirectoryTests
{
    [Fact]
    public void RecentPresentationHistoryMatchesSeatProjectionAndSurvivesDuplicateCommands()
    {
        using var f=new Fixture();var clients=f.StartHumans();
        for(int i=0;i<8;i++)
        {
            var actor=f.Actor(clients);
            var command=new BaseGameAi().Decide(WireJson.Deserialize<M2.PlayerView>(actor.ViewJson));
            command.Id="presentation-"+i;
            var request=f.Command(actor,command);
            var committed=f.Directory.Handle(request);Assert.True(committed.Success);
            var duplicate=f.Directory.Handle(request);Assert.True(duplicate.IsDuplicate);
            Assert.Equal(committed.PresentationHistoryJson,duplicate.PresentationHistoryJson);
            foreach(var client in clients)
            {
                var snapshot=f.Directory.Handle(Fixture.Request("Get",client));
                var page=f.Send(client,"History",new HistoryPayload{Limit=200});
                Assert.Equal(page.HistoryJson,snapshot.PresentationHistoryJson);
                Assert.Equal(0,snapshot.PresentationHistoryAfterSequence);
                Assert.DoesNotContain("Random",snapshot.PresentationHistoryJson);
                Assert.Equal(snapshot.ViewJson,f.Directory.Handle(Fixture.Request("Get",client)).ViewJson);
            }
        }
    }

    [Theory][InlineData(3,new[]{"P1"},"P2")][InlineData(4,new[]{"P1","P2","P3"},"P4")]
    public void RedCanBeAiAndCreatorUsesFirstHumanColor(int seats,string[] ai,string manager)
    {
        using var f=new Fixture();var host=f.Create(seats:seats,aiSeats:ai);
        Assert.True(host.Success);Assert.Equal(manager,host.SeatId);Assert.Equal(manager,host.Room.ManagerSeatId);
        Assert.True(host.Room.Seats.Single(s=>s.SeatId=="P1").IsAi);
        f.Restart();var restored=f.Directory.Handle(Fixture.Request("Reconnect",host));Assert.True(restored.Success);Assert.Equal(manager,restored.SeatId);
    }
    [Fact] public void AssigningHostColorToAiMovesOnlyHostAndKeepsIdentity()
    {
        using var f=new Fixture();var host=f.Create();var other=f.Join(host);
        var updated=f.Send(host,"Configure",new RoomSettings{PlayerCount=3,AiSeats=["P1"]});
        Assert.True(updated.Success);Assert.Equal("P3",updated.SeatId);Assert.Equal("P3",updated.Room.ManagerSeatId);
        Assert.Equal(other.SeatId,updated.Room.Seats.Single(s=>s.SeatId==other.SeatId).SeatId);
        var rejected=f.Send(host,"Configure",new RoomSettings{PlayerCount=3,AiSeats=["P1","P2","P3"]});
        Assert.False(rejected.Success);Assert.Equal(updated.Revision,rejected.Revision);Assert.Equal("P3",rejected.SeatId);
        Assert.False(f.Create(aiSeats:["P1","P2","P3"]).Success);
    }
    [Fact]
    public void PasswordPublicListAndVersionChecksAreEnforced()
    {
        using var setup = new Fixture();
        var created = setup.Create(password: "room-secret");
        Assert.True(created.Success);
        var listing = setup.Directory.Handle(new() { Operation = "List" });
        Assert.True(listing.Rooms.Single().HasPassword);
        string wire = WireJson.Serialize(listing);
        Assert.DoesNotContain("room-secret", wire); Assert.DoesNotContain(created.Credential, wire);
        Assert.Equal("InvalidPassword", setup.Join(created, password: "wrong").Code);
        Assert.True(setup.Join(created, password: "room-secret").Success);
        var incompatible = Fixture.Request("Join", created); incompatible.ExpectedRulesVersion = "future"; incompatible.Payload = "{}";
        Assert.Equal("VersionMismatch", setup.Directory.Handle(incompatible).Code);
        incompatible.ExpectedRulesVersion = null;
        Assert.Equal("VersionMismatch", setup.Directory.Handle(incompatible).Code);
        incompatible.ProtocolVersion = 98;
        Assert.Equal("ProtocolMismatch", setup.Directory.Handle(incompatible).Code);
    }

    [Fact]
    public void PrepareConfigureAndSeatChangesRequirePreparationAgain()
    {
        using var f = new Fixture(); var one = f.Create(aiSeats: new[] { "P3" }); var two = f.Join(one);
        Assert.Equal("NotReady", f.Send(one, "Start").Code);
        Assert.Equal("ManagerRequired", f.Send(two, "Configure", new RoomSettings { PlayerCount = 3, AiSeats = new[] { "P3" } }).Code);
        Assert.True(f.Send(one, "Ready", new ReadyPayload { Ready = true }).Success);
        Assert.True(f.Send(two, "Ready", new ReadyPayload { Ready = true }).Success);
        var change = f.Send(one, "Configure", new RoomSettings { Name = "设置重置", PlayerCount = 3, AiSeats = new[] { "P3" }, TargetVictoryPoints = 8 });
        Assert.True(change.Success); Assert.All(change.Room.Seats.Where(s => !s.IsAi), s => Assert.False(s.Ready));
        Assert.Equal("NotReady", f.Send(one, "Start").Code);
        f.Send(one, "Ready", new ReadyPayload { Ready = true }); f.Send(two, "Ready", new ReadyPayload { Ready = true });
        Assert.True(f.Send(one, "Start").Success);
        Assert.Equal("GameStarted", f.Send(two, "Seat", new SeatPayload { SeatId = "P3" }).Code);
    }

    [Fact]
    public void CredentialIsRoomBoundAndReconnectRevokesOldConnection()
    {
        using var f = new Fixture(); var first = f.Create(); var second = f.Create();
        var cross = Fixture.Request("Get", first); cross.RoomId = second.RoomId;
        Assert.Equal("Unauthorized", f.Directory.Handle(cross).Code);
        var reconnect = f.Directory.Handle(Fixture.Request("Reconnect", first));
        Assert.True(reconnect.Success); Assert.NotEqual(first.ConnectionId, reconnect.ConnectionId);
        Assert.Equal("ConnectionSuperseded", f.Directory.Handle(Fixture.Request("Get", first)).Code);
        reconnect.Credential = first.Credential;
        Assert.True(f.Directory.Handle(Fixture.Request("Get", reconnect)).Success);
    }

    [Fact]
    public void DuplicatePrecedesRevisionCheckAndConflictingContentCannotReplay()
    {
        using var f = new Fixture(); var clients = f.StartHumans(); var actor = f.Actor(clients);
        var view = WireJson.Deserialize<M2.PlayerView>(actor.ViewJson); var command = new BaseGameAi().Decide(view); command.Id = "stable-command";
        var request = f.Command(actor, command); var accepted = f.Directory.Handle(request);
        Assert.True(accepted.Success);
        var retry = f.Directory.Handle(request); Assert.True(retry.Success); Assert.True(retry.IsDuplicate); Assert.Equal(accepted.Revision, retry.Revision); Assert.Equal(accepted.CommittedRevision, retry.CommittedRevision);
        command.TargetId = "not-the-same"; request.Payload = WireJson.Serialize(command);
        var conflict = f.Directory.Handle(request); Assert.Equal("CommandConflict", conflict.Code); Assert.Equal(accepted.Revision, conflict.Revision);
    }

    [Fact]
    public void ForgedSeatInvalidActionsAndStaleRevisionDoNotChangeCommittedState()
    {
        using var f = new Fixture(); var clients = f.StartHumans(); var actor = f.Actor(clients);
        var command = new BaseGameAi().Decide(WireJson.Deserialize<M2.PlayerView>(actor.ViewJson)); command.Id = "forged";
        var request = f.Command(actor, command); command.PlayerId = clients.First(c => c.SeatId != actor.SeatId).SeatId; request.Payload = WireJson.Serialize(command);
        var denied = f.Directory.Handle(request); Assert.Equal("SeatForgery", denied.Code); Assert.Equal(actor.Revision, denied.Revision);
        command.PlayerId = actor.SeatId; command.TargetId = "INVALID"; request.Payload = WireJson.Serialize(command);
        denied = f.Directory.Handle(request); Assert.False(denied.Success); Assert.Equal(actor.Revision, denied.Revision);
        Assert.Equal(actor.ViewJson, denied.ViewJson);
        command = new BaseGameAi().Decide(WireJson.Deserialize<M2.PlayerView>(actor.ViewJson)); command.Id = "stale"; request = f.Command(actor, command); request.ExpectedRevision--;
        Assert.Equal("StaleRevision", f.Directory.Handle(request).Code);
    }

    [Fact]
    public void SimultaneousCommandsFromSameSnapshotCommitExactlyOne()
    {
        using var f = new Fixture(); var actor = f.Actor(f.StartHumans());
        var command = new BaseGameAi().Decide(WireJson.Deserialize<M2.PlayerView>(actor.ViewJson)); command.Id = "click-a"; var a = f.Command(actor, command);
        command.Id = "click-b"; var b = f.Command(actor, command);
        var results = new ConcurrentBag<NetworkResponse>(); Parallel.Invoke(() => results.Add(f.Directory.Handle(a)), () => results.Add(f.Directory.Handle(b)));
        Assert.Single(results, r => r.Success); Assert.Single(results, r => r.Code == "StaleRevision");
        Assert.Single(results.Select(r => r.Revision).Distinct());
    }

    [Fact]
    public void FailedAtomicSaveRestoresSnapshotAndPausesWithoutAcknowledgingSuccess()
    {
        using var f = new Fixture(); var actor = f.Actor(f.StartHumans());
        var command = new BaseGameAi().Decide(WireJson.Deserialize<M2.PlayerView>(actor.ViewJson)); command.Id = "fault"; var request = f.Command(actor, command);
        f.Storage.Fail = true; var failed = f.Directory.Handle(request);
        Assert.False(failed.Success); Assert.Equal("PersistenceFailed", failed.Code); Assert.Equal(actor.Revision, failed.Revision); Assert.Equal(actor.ViewJson, failed.ViewJson); Assert.True(failed.Room.Paused);
        Assert.Equal("RoomPaused", f.Directory.Handle(request).Code);
        f.Storage.Fail = false; var manager = f.Clients.Single(c => c.SeatId == "P1"); Assert.True(f.Send(manager, "Recover").Success);
        var retry = f.Directory.Handle(request); Assert.True(retry.Success); Assert.False(retry.IsDuplicate);
    }

    [Fact]
    public void RestartRecoversPendingDecisionAndCommandReceipt()
    {
        using var f = new Fixture(); var actor = f.Actor(f.StartHumans());
        var command = new BaseGameAi().Decide(WireJson.Deserialize<M2.PlayerView>(actor.ViewJson)); command.Id = "before-restart"; var request = f.Command(actor, command);
        var accepted = f.Directory.Handle(request); Assert.True(accepted.Success); var view = WireJson.Deserialize<M2.PlayerView>(accepted.ViewJson); Assert.NotNull(view.PendingDecision);
        f.Restart(); var resumed = f.Directory.Handle(Fixture.Request("Reconnect", actor)); Assert.True(resumed.Success); Assert.Equal(accepted.Revision + 1, resumed.Revision); Assert.Equal(accepted.ViewJson, resumed.ViewJson); Assert.False(resumed.HumanControlAvailable);
        request.ConnectionId = resumed.ConnectionId; var retry = f.Directory.Handle(request); Assert.True(retry.IsDuplicate); Assert.Equal(accepted.CommittedRevision, retry.CommittedRevision);
    }

    [Fact]
    public async Task RestartDuplicateKeepsOriginalReceiptWhenBackgroundManagementTransferRaisesRevision()
    {
        using var f = new Fixture(); var clients = f.StartHumans(); var actor = f.Actor(clients);
        var command = new BaseGameAi().Decide(WireJson.Deserialize<M2.PlayerView>(actor.ViewJson)); command.Id = "resume-with-manager-transfer";
        var request = f.Command(actor, command); var accepted = f.Directory.Handle(request); Assert.True(accepted.Success);
        var manager = clients.Single(c => c.SeatId == "P1"); var offlineManager = clients.First(c => c.SeatId != actor.SeatId);
        Assert.True(f.Send(manager, "Transfer", new SeatPayload { SeatId = offlineManager.SeatId }).Success);

        f.Restart(); var resumed = f.Directory.Handle(Fixture.Request("Reconnect", actor)); Assert.True(resumed.Success);
        Assert.Equal(offlineManager.SeatId, resumed.Room.ManagerSeatId);
        await f.Directory.TickAiAsync(); // 只有刚恢复的席位在线，后台在重试前转交管理权。
        request.ConnectionId = resumed.ConnectionId; var retry = f.Directory.Handle(request);
        Assert.True(retry.Success); Assert.True(retry.IsDuplicate); Assert.True(retry.Revision > resumed.Revision);
        Assert.Equal(actor.SeatId, retry.Room.ManagerSeatId); Assert.Equal(accepted.CommittedRevision, retry.CommittedRevision);
        Assert.NotNull(retry.ViewJson); Assert.False(retry.HumanControlAvailable);
    }

    [Fact]
    public void LobbyLastLeaveClosesRoomAndNeverRelistsHistoryAfterRestart()
    {
        using var f = new Fixture(); var player = f.Create(); Assert.True(f.Send(player, "Leave").Success);
        Assert.Equal("RoomNotFound", f.Directory.Handle(Fixture.Request("Reconnect", player)).Code);
        Assert.Empty(f.Directory.ListPublicRooms()); Assert.Empty(f.Storage.Documents);
        f.Restart(); Assert.Empty(f.Directory.ListPublicRooms()); Assert.Equal("RoomNotFound", f.Join(player).Code);
        Assert.True(f.Create().Success);
    }

    [Fact]
    public async Task ExplicitLeaveAndNetworkTimeoutTransferManagementWithoutMovingAuthority()
    {
        var clock = DateTimeOffset.UtcNow;
        using var f = new Fixture(new RoomDirectoryOptions { Clock = () => clock, ConnectionTimeout = TimeSpan.FromSeconds(10) });
        var one = f.Create(); var two = f.Join(one);
        clock += TimeSpan.FromSeconds(11); f.Directory.Handle(Fixture.Request("Get", two));
        await f.Directory.TickAiAsync(); var state = f.Directory.Handle(Fixture.Request("Get", two)); Assert.Equal(two.SeatId, state.Room.ManagerSeatId);
        Assert.Equal("InvalidSeat", f.Send(two, "Transfer", new SeatPayload { SeatId = one.SeatId }).Code);
        Assert.Equal("Unauthorized", f.Send(one, "Leave").Code); Assert.Equal(two.SeatId, f.Directory.Handle(Fixture.Request("Get", two)).Room.ManagerSeatId);
        var three = f.Join(two); Assert.True(three.Success); Assert.True(f.Send(two, "Leave").Success);
        Assert.Equal(three.SeatId, f.Directory.Handle(Fixture.Request("Get", three)).Room.ManagerSeatId);
    }

    [Fact]
    public void ExpiryAndExplicitRevocationPreventSeatRecovery()
    {
        var clock = DateTimeOffset.UtcNow; using var f = new Fixture(new RoomDirectoryOptions { Clock = () => clock, CredentialLifetime = TimeSpan.FromMinutes(2) });
        var one = f.Create(); clock += TimeSpan.FromMinutes(3); Assert.Equal("Unauthorized", f.Directory.Handle(Fixture.Request("Reconnect", one)).Code);
        var clients = f.StartHumans(); var revoke = clients[0]; Assert.True(f.Send(revoke, "Revoke").Success); Assert.Equal("Unauthorized", f.Directory.Handle(Fixture.Request("Reconnect", revoke)).Code);
    }

    [Fact]
    public void PreviewDoesNotMutateAndPrivateHistoryIsPaged()
    {
        using var f = new Fixture(); var clients = f.StartHumans(); var actor = f.Actor(clients);
        var command = new BaseGameAi().Decide(WireJson.Deserialize<M2.PlayerView>(actor.ViewJson)); command.Id = "preview"; var request = f.Command(actor, command); request.Operation = "Preview";
        var preview = f.Directory.Handle(request); Assert.True(preview.Success); Assert.Equal(actor.Revision, preview.Revision); Assert.Equal(actor.ViewJson, preview.ViewJson);
        request.Operation = "Command"; Assert.True(f.Directory.Handle(request).Success);
        var history = f.Send(actor, "History", new HistoryPayload { Limit = 1 }); Assert.True(history.Success); Assert.Null(history.ViewJson); var entries = WireJson.Deserialize<Catan.Core.GameHistoryEntry[]>(history.HistoryJson); Assert.Single(entries); Assert.Equal(entries[0].Sequence, history.HistoryCursor);
        Assert.DoesNotContain("CredentialHash", history.HistoryJson); Assert.DoesNotContain("Random", history.HistoryJson);
    }

    public static IEnumerable<object[]> Configurations()
    {
        foreach (int seats in new[] { 3, 4 })
        {
            yield return new object[] { "base", "base", seats }; yield return new object[] { "cities-knights", "base", seats };
            foreach (var scenario in Catan.Core.M3.SeafarersScenarios.Ids) yield return new object[] { "seafarers", scenario, seats };
            foreach (var scenario in Catan.Core.M5.CombinedScenarios.Ids) yield return new object[] { "combined", scenario, seats };
        }
    }
    [Theory, MemberData(nameof(Configurations))]
    public async Task EverySupportedConfigurationContinuesWithAiAfterLastHumanDisconnects(string rules, string scenario, int seats)
    {
        using var f = new Fixture(); var host = f.Create(rules, scenario, seats, Enumerable.Range(2, seats - 1).Select(i => "P" + i).ToArray()); Assert.True(host.Success);
        f.Send(host, "Ready", new ReadyPayload { Ready = true }); var started = f.Send(host, "Start"); Assert.True(started.Success); Assert.NotNull(started.ViewJson);
        Assert.DoesNotContain("Random", started.ViewJson); Assert.DoesNotContain("ProcessedCommands", started.ViewJson); Assert.DoesNotContain("InitialSeed", started.ViewJson); Assert.DoesNotContain("DevelopmentDeck\"", started.ViewJson);
        for (int i = 0; i < 12; i++) if (await f.Directory.TickAiAsync() == 0) break;
        var before = f.Directory.Handle(Fixture.Request("Get", host)); f.Directory.Disconnect(host.ConnectionId); Assert.Empty(f.Directory.ListPublicRooms());
        Assert.Equal(1, await f.Directory.TickAiAsync());
        var resume = f.Directory.Handle(Fixture.Request("Reconnect", host)); Assert.True(resume.Success); Assert.True(resume.Revision > before.Revision);
        Assert.False(resume.HumanControlAvailable); Assert.True(resume.Room.Seats.Single(s => s.SeatId == host.SeatId).RejoinPending); Assert.False(resume.Room.Paused);
    }

    [Fact]
    public void AtomicStoreEnforcesSingleWriterAndKeepsDurableSnapshot()
    {
        var directory = Path.Combine(Path.GetTempPath(), "catan-m7-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var store = new AtomicRoomStorage(directory))
            {
                Assert.Throws<IOException>(() => new AtomicRoomStorage(directory));
                store.Commit("ABCDEF123456", "first"); store.Commit("ABCDEF123456", "second"); Assert.Equal("second", store.LoadAll().Single());
                Assert.Throws<InvalidDataException>(() => store.Commit("../traverse", "no"));
            }
            using var reopened = new AtomicRoomStorage(directory); Assert.Equal("second", reopened.LoadAll().Single());
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void FailedRevokeKeepsOriginalCredentialAndRoomConfiguration()
    {
        using var f = new Fixture(); var host = f.StartHumans()[0]; f.Storage.Fail = true;
        var failure = f.Send(host, "Revoke"); Assert.Equal("PersistenceFailed", failure.Code);
        Assert.True(f.Directory.Handle(Fixture.Request("Get", host)).Success);
        f.Storage.Fail = false; Assert.True(f.Send(host, "Recover").Success);
        Assert.True(f.Directory.Handle(Fixture.Request("Reconnect", host)).Success);
    }

    [Fact]
    public void UnknownSaveFormatFailsClosedAndPreservesPrivateDocument()
    {
        using var f = new Fixture(); var room = f.Create();
        string corrupt = f.Storage.Documents[room.RoomId].Replace("\"Format\":1", "\"Format\":999"); f.Storage.Documents[room.RoomId] = corrupt;
        Assert.Throws<InvalidDataException>(() => f.Restart()); Assert.Equal(corrupt, f.Storage.Documents[room.RoomId]);
    }

    [Fact]
    public void LegacyPrivateRoomMigratesOnNextCommitWhileLegacyWireClientIsRejected()
    {
        using var f = new Fixture(); var host = f.Create(aiSeats: new[] { "P2", "P3" });
        var legacy = System.Text.Json.Nodes.JsonNode.Parse(f.Storage.Documents[host.RoomId])!.AsObject();
        legacy["ProtocolVersion"] = 1; legacy.Remove("Closed"); legacy.Remove("AiDisplayNames");
        foreach (var seat in legacy["Seats"]!.AsArray()) { seat!.AsObject().Remove("ControlledByAi"); seat.AsObject().Remove("RejoinAfterTurnKey"); }
        var legacyDocument = legacy.ToJsonString(); f.Storage.Documents[host.RoomId] = legacyDocument;
        f.Restart(); Assert.Equal(legacyDocument, f.Storage.Documents[host.RoomId]);
        var denied = f.Directory.Handle(new() { Operation = "List", ProtocolVersion = 1 }); Assert.Equal("ProtocolMismatch", denied.Code); Assert.Equal(NetworkProtocol.Version, denied.ProtocolVersion);
        var restored = f.Directory.Handle(Fixture.Request("Reconnect", host)); restored.Credential = host.Credential;
        Assert.True(restored.Success); Assert.Equal(NetworkProtocol.Version, restored.ProtocolVersion); Assert.Equal(host.RoomId, restored.RoomId);
        Assert.All(restored.Room.Seats.Where(s => s.IsAi), s => Assert.Contains(s.DisplayName, new[] { "哈基米", "吃白饭的大肥鲸", "糖包", "我是奶龙" }));
        using var upgraded = System.Text.Json.JsonDocument.Parse(f.Storage.Documents[host.RoomId]);
        Assert.Equal(NetworkProtocol.Version, upgraded.RootElement.GetProperty("ProtocolVersion").GetInt32());
        Assert.Equal(2, upgraded.RootElement.GetProperty("AiDisplayNames").EnumerateObject().Count());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(999)]
    public void UnknownPrivateProtocolVersionsFailClosedWithoutOverwritingSave(int version)
    {
        using var f = new Fixture(); var room = f.Create();
        var document = System.Text.Json.Nodes.JsonNode.Parse(f.Storage.Documents[room.RoomId])!.AsObject(); document["ProtocolVersion"] = version;
        var corrupt = document.ToJsonString(); f.Storage.Documents[room.RoomId] = corrupt;
        Assert.Throws<InvalidDataException>(() => f.Restart()); Assert.Equal(corrupt, f.Storage.Documents[room.RoomId]);
    }

    [Fact]
    public void HiddenRoomCanBeDescribedWithoutPrivateDataAndJoinedByNumber()
    {
        using var f = new Fixture(); var host = f.Create();
        Assert.True(f.Send(host, "Configure", new RoomSettings { Name = "按号码加入", PlayerCount = 3, Listed = false }).Success);
        Assert.Empty(f.Directory.ListPublicRooms());
        var description = f.Directory.Handle(new NetworkRequest { Operation = "Describe", RoomId = host.RoomId });
        Assert.True(description.Success); Assert.Null(description.ViewJson); Assert.Null(description.Credential); Assert.Null(description.ConnectionId);
        Assert.True(f.Join(host).Success);
    }

    [Fact]
    public void TotalCommittedCommandLimitStillAllowsIdenticalRetryAndRestart()
    {
        using var f = new Fixture(new RoomDirectoryOptions { MaximumCommittedCommands = 1 });
        var clients = f.StartHumans(); var actor = f.Actor(clients);
        var command = new BaseGameAi().Decide(WireJson.Deserialize<M2.PlayerView>(actor.ViewJson)); command.Id = "limit-last";
        var request = f.Command(actor, command); var accepted = f.Directory.Handle(request); Assert.True(accepted.Success);
        Assert.True(f.Directory.Handle(request).IsDuplicate);
        var next = new BaseGameAi().Decide(WireJson.Deserialize<M2.PlayerView>(accepted.ViewJson)); next.Id = "limit-exceeded";
        request = f.Command(actor, next); request.ExpectedRevision = accepted.Revision;
        Assert.Equal("CommandLimit", f.Directory.Handle(request).Code);
        f.Restart(); var restored = f.Directory.Handle(Fixture.Request("Reconnect", actor)); Assert.True(restored.Success); Assert.Equal(accepted.Revision + 1, restored.Revision);
    }

    [Fact]
    public async Task AiAlsoStopsAtRecoverableCommittedCommandLimit()
    {
        using var f = new Fixture(new RoomDirectoryOptions { MaximumCommittedCommands = 1 });
        var host = f.Create(aiSeats: new[] { "P2", "P3" }); f.Send(host, "Ready", new ReadyPayload { Ready = true }); var started = f.Send(host, "Start");
        var view = WireJson.Deserialize<M2.PlayerView>(started.ViewJson);
        if (view.ActivePlayerId == host.SeatId)
        {
            var command = new BaseGameAi().Decide(view); command.Id = "human-boundary"; var request = f.Command(host, command); request.ExpectedRevision = started.Revision;
            Assert.True(f.Directory.Handle(request).Success);
        }
        else Assert.Equal(1, await f.Directory.TickAiAsync());
        Assert.Equal(0, await f.Directory.TickAiAsync()); var committed = f.Directory.Handle(Fixture.Request("Get", host)); Assert.Equal(started.Revision + 1, committed.Revision);
        f.Restart(); var restored = f.Directory.Handle(Fixture.Request("Reconnect", host)); Assert.True(restored.Success); Assert.Equal(committed.Revision + 1, restored.Revision);
    }

    private static NetworkRequest Enrollment(string operation, string? roomId = null, object? payload = null)
        => new() { Operation = operation, RoomId = roomId, EnrollmentId = Guid.NewGuid().ToString("N"), EnrollmentCredential = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)), ExpectedRulesVersion = NetworkProtocol.RulesVersion("base"), ExpectedScenarioVersion = NetworkProtocol.ScenarioVersion("base"), Payload = WireJson.SerializeObject(payload ?? (operation == "Create" ? (object)new CreateRoomPayload { Settings = new RoomSettings { PlayerCount = 3 } } : new JoinRoomPayload())) };

    [Fact]
    public void LostCreateReceiptReplaysSameRoomAfterRestartWithoutRetransmittingToken()
    {
        using var f = new Fixture(); var request = Enrollment("Create");
        var lost = f.Directory.Handle(request); Assert.True(lost.Success); Assert.Null(lost.Credential);
        Assert.DoesNotContain(request.EnrollmentCredential, f.Storage.Documents.Values.Single());
        f.Restart(); request.RequestId = "new-transport-correlation";
        var replay = f.Directory.Handle(request); Assert.True(replay.Success); Assert.True(replay.IsDuplicate); Assert.Null(replay.Credential);
        Assert.Equal(lost.RoomId, replay.RoomId); Assert.Equal(lost.SeatId, replay.SeatId); Assert.Equal(lost.Revision, replay.Revision); Assert.Equal(lost.Revision, replay.CommittedRevision); Assert.Single(f.Directory.ListPublicRooms());
        Assert.NotEqual(lost.ConnectionId, replay.ConnectionId);
        replay.Credential = request.EnrollmentCredential; Assert.True(f.Directory.Handle(Fixture.Request("Get", replay)).Success);
    }

    [Fact]
    public void LostJoinReceiptReplaysOriginalSeatEvenAfterSettingsAndSeatChange()
    {
        using var f = new Fixture(); var host = f.Create(); var request = Enrollment("Join", host.RoomId, new JoinRoomPayload { DisplayName = "预存恢复身份" });
        var lost = f.Directory.Handle(request); Assert.True(lost.Success); lost.Credential = request.EnrollmentCredential;
        Assert.True(f.Send(lost, "Seat", new SeatPayload { SeatId = "P3" }).Success);
        Assert.True(f.Send(host, "Configure", new RoomSettings { RuleSet = "cities-knights", ScenarioId = "base", PlayerCount = 3 }).Success);
        f.Restart(); var replay = f.Directory.Handle(request);
        Assert.True(replay.Success); Assert.True(replay.IsDuplicate); Assert.Equal("P3", replay.SeatId); Assert.Equal("cities-knights", replay.RuleSet); Assert.Equal(2, replay.Room.OccupiedSeats); Assert.Null(replay.Credential);
    }

    [Theory]
    [InlineData("Create")]
    [InlineData("Join")]
    public void EnrollmentIdTokenAndContentConflictsCannotAllocateAnotherSeat(string operation)
    {
        using var f = new Fixture(); var host = f.Create(); var request = Enrollment(operation, operation == "Join" ? host.RoomId : null);
        var original = f.Directory.Handle(request); Assert.True(original.Success);
        string originalToken = request.EnrollmentCredential; string originalId = request.EnrollmentId; string originalPayload = request.Payload;
        request.EnrollmentCredential = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        Assert.Equal("EnrollmentConflict", f.Directory.Handle(request).Code);
        request.EnrollmentCredential = originalToken; request.EnrollmentId = Guid.NewGuid().ToString("N");
        Assert.Equal("EnrollmentConflict", f.Directory.Handle(request).Code);
        request.EnrollmentId = originalId; request.Payload = operation == "Join" ? WireJson.Serialize(new JoinRoomPayload { DisplayName = "另一个名字" }) : WireJson.Serialize(new CreateRoomPayload { DisplayName = "另一个名字", Settings = new RoomSettings { PlayerCount = 3 } });
        Assert.Equal("EnrollmentConflict", f.Directory.Handle(request).Code);
        request.Payload = originalPayload; var replay = f.Directory.Handle(request); Assert.True(replay.IsDuplicate); Assert.Equal(original.Room.OccupiedSeats, replay.Room.OccupiedSeats);
    }

    [Theory]
    [InlineData("Create")]
    [InlineData("Join")]
    public void EnrollmentCommitFailureCanRetryWithPreSavedToken(string operation)
    {
        using var f = new Fixture(); var host = f.Create(); var request = Enrollment(operation, operation == "Join" ? host.RoomId : null);
        f.Storage.Fail = true; Assert.Equal("PersistenceFailed", f.Directory.Handle(request).Code); f.Storage.Fail = false;
        if (operation == "Join") Assert.True(f.Send(host, "Recover").Success);
        var retry = f.Directory.Handle(request); Assert.True(retry.Success); Assert.False(retry.IsDuplicate); Assert.Null(retry.Credential);
        var again = f.Directory.Handle(request); Assert.True(again.IsDuplicate); Assert.Equal(retry.RoomId, again.RoomId); Assert.Equal(retry.SeatId, again.SeatId);
    }

    [Theory]
    [InlineData("Create")]
    [InlineData("Join")]
    public void LeftLobbyEnrollmentCannotResurrectRevokedIdentity(string operation)
    {
        using var f = new Fixture(); var host = f.Create(); var request = Enrollment(operation, operation == "Join" ? host.RoomId : null);
        var client = f.Directory.Handle(request); client.Credential = request.EnrollmentCredential;
        Assert.True(f.Send(client, "Leave").Success); f.Restart();
        var replay = f.Directory.Handle(request);
        if(operation=="Create")
        {
            // 最后一人退出后房间及登记账本已销毁；再次创建可以得到新房，但不能恢复原房。
            Assert.True(replay.Success);Assert.False(replay.IsDuplicate);Assert.NotEqual(client.RoomId,replay.RoomId);
            Assert.Equal("RoomNotFound",f.Directory.Handle(Fixture.Request("Reconnect",client)).Code);
        }
        else {Assert.False(replay.Success);Assert.Equal("Unauthorized",replay.Code);Assert.Null(replay.Room);}
        Assert.Null(replay.Credential);
    }

    [Fact]
    public void ExpiredEnrollmentCannotBypassCredentialLifetimeAndWrongRoomCannotClaimExistingSeat()
    {
        var clock = DateTimeOffset.UtcNow; using var f = new Fixture(new RoomDirectoryOptions { Clock = () => clock, CredentialLifetime = TimeSpan.FromSeconds(5) });
        var first = f.Create(password: "first"); var second = f.Create(password: "second");
        var request = Enrollment("Join", first.RoomId, new JoinRoomPayload { Password = "first" }); var joined = f.Directory.Handle(request); Assert.True(joined.Success);
        request.RoomId = second.RoomId; Assert.Equal("InvalidPassword", f.Directory.Handle(request).Code);
        request.RoomId = first.RoomId; clock += TimeSpan.FromSeconds(6); Assert.Equal("Unauthorized", f.Directory.Handle(request).Code);
    }

    [Fact]
    public void EnrollmentRejectsMalformedTokenAndDoesNotDuplicateLegacyCredential()
    {
        using var f = new Fixture(); var host = f.Create(); var request = Enrollment("Join", host.RoomId);
        request.EnrollmentCredential = "not-a-256-bit-token"; Assert.Equal("InvalidRequest", f.Directory.Handle(request).Code);
        request.EnrollmentCredential = host.Credential; Assert.Equal("EnrollmentConflict", f.Directory.Handle(request).Code);
        Assert.Single(f.Directory.ListPublicRooms().Single().Seats, s => s.Occupied);
    }

    [Fact]
    public async Task AbandonedLobbyExpiresWithoutBlockingNewRoomsAndCannotResurrectAfterRestart()
    {
        var clock = DateTimeOffset.UtcNow;
        using var f = new Fixture(new RoomDirectoryOptions { Clock = () => clock, ConnectionTimeout = TimeSpan.FromSeconds(10), MaximumRooms = 1 });
        var abandoned = f.Create(); f.Directory.Disconnect(abandoned.ConnectionId);
        Assert.Empty(f.Directory.ListPublicRooms());
        var replacement = f.Create(); Assert.True(replacement.Success);
        clock += TimeSpan.FromSeconds(11); Assert.True(f.Directory.Handle(Fixture.Request("Get", replacement)).Success);
        await f.Directory.TickAiAsync();
        Assert.Equal("RoomNotFound", f.Directory.Handle(Fixture.Request("Reconnect", abandoned)).Code);
        Assert.Equal("RoomNotFound", f.Join(abandoned).Code);
        f.Restart(); Assert.Empty(f.Directory.ListPublicRooms());
        Assert.Equal("RoomNotFound", f.Join(abandoned).Code);
        var restored = f.Directory.Handle(Fixture.Request("Reconnect", replacement)); Assert.True(restored.Success);
        Assert.Single(f.Directory.ListPublicRooms()); Assert.Equal(replacement.RoomId, f.Directory.ListPublicRooms()[0].RoomId);
    }

    [Fact]
    public async Task ReconnectedHumanCannotInterruptPendingSetupAndReturnsAtNextOwnPlacement()
    {
        using var f = new Fixture(); var clients = f.StartHumans(); var actor = f.Actor(clients);
        var setup = new BaseGameAi().Decide(WireJson.Deserialize<M2.PlayerView>(actor.ViewJson)); setup.Id = "before-ai-setup";
        Assert.True(f.Directory.Handle(f.Command(actor, setup)).Success);
        foreach (var client in clients) f.Directory.Disconnect(client.ConnectionId);
        var resumed = f.Directory.Handle(Fixture.Request("Reconnect", actor)); resumed.Credential = actor.Credential;
        Assert.False(resumed.HumanControlAvailable); Assert.True(resumed.Room.Seats.Single(s => s.SeatId == actor.SeatId).RejoinPending);
        var road = new BaseGameAi().Decide(WireJson.Deserialize<M2.PlayerView>(resumed.ViewJson)); road.Id = "cannot-interrupt-ai";
        var denied = f.Directory.Handle(f.Command(resumed, road)); Assert.Equal("AiTakeover", denied.Code); Assert.Equal(resumed.ViewJson, denied.ViewJson);
        var preview = f.Command(resumed, road); preview.Operation = "Preview"; Assert.Equal("AiTakeover", f.Directory.Handle(preview).Code);
        // 重试已提交的旧命令仍返回原回执，不能被托管状态误判为新行动。
        var retry = f.Command(resumed, setup); Assert.True(f.Directory.Handle(retry).IsDuplicate);
        // 托管与待接回边界随私有房间一起保存；重启不能让真人打断原待决。
        f.Restart(); resumed = f.Directory.Handle(Fixture.Request("Reconnect", resumed)); resumed.Credential = actor.Credential;
        Assert.True(resumed.Success); Assert.False(resumed.HumanControlAvailable);
        Assert.True(resumed.Room.Seats.Single(s => s.SeatId == actor.SeatId).RejoinPending);
        Assert.Equal(1, await f.Directory.TickAiAsync());
        var current = f.Directory.Handle(Fixture.Request("Get", resumed)); Assert.False(current.HumanControlAvailable);
        Assert.NotEqual(actor.SeatId, WireJson.Deserialize<M2.PlayerView>(current.ViewJson).ActivePlayerId);
        for (int n = 0; n < 30 && !current.HumanControlAvailable; n++)
        {
            Assert.Equal(1, await f.Directory.TickAiAsync()); current = f.Directory.Handle(Fixture.Request("Get", resumed));
        }
        Assert.True(current.HumanControlAvailable); Assert.False(current.Room.Seats.Single(s => s.SeatId == actor.SeatId).RejoinPending);
        var own = WireJson.Deserialize<M2.PlayerView>(current.ViewJson); Assert.Equal(actor.SeatId, own.ActivePlayerId); Assert.Equal(M2.GamePhase.SetupSettlement, own.Phase);
        current.Credential = actor.Credential; var next = new BaseGameAi().Decide(own); next.Id = "human-returned";
        Assert.True(f.Directory.Handle(f.Command(current, next)).Success);
    }

    [Fact]
    public async Task ReconnectionDuringNormalTurnWaitsUntilPlayersNextFullTurn()
    {
        using var f = new Fixture(); var clients = f.StartHumans();
        for (int n = 0; n < 12; n++)
        {
            var actor = f.Actor(clients); var command = new BaseGameAi().Decide(WireJson.Deserialize<M2.PlayerView>(actor.ViewJson)); command.Id = "complete-setup-" + n;
            Assert.True(f.Directory.Handle(f.Command(actor, command)).Success);
        }
        var human = f.Actor(clients); var initial = WireJson.Deserialize<M2.PlayerView>(human.ViewJson); Assert.Equal(1, initial.Turn);
        foreach (var client in clients) f.Directory.Disconnect(client.ConnectionId);
        var resumed = f.Directory.Handle(Fixture.Request("Reconnect", human)); resumed.Credential = human.Credential;
        var current = resumed;
        for (int n = 0; n < 300 && !current.HumanControlAvailable; n++)
        {
            Assert.Equal(1, await f.Directory.TickAiAsync()); current = f.Directory.Handle(Fixture.Request("Get", resumed));
            var visible = WireJson.Deserialize<M2.PlayerView>(current.ViewJson);
            if (visible.Turn == initial.Turn || visible.ActivePlayerId != human.SeatId) Assert.False(current.HumanControlAvailable);
        }
        Assert.True(current.HumanControlAvailable); var returned = WireJson.Deserialize<M2.PlayerView>(current.ViewJson);
        Assert.True(returned.Turn > initial.Turn); Assert.Equal(human.SeatId, returned.ActivePlayerId); Assert.Equal(M2.GamePhase.ProductionAwaitRoll, returned.Phase);
    }

    [Fact]
    public async Task SnakeSetupReturnsControlAtNextPlacementEvenWhenActiveColorDoesNotChange()
    {
        using var f = new Fixture(); var clients = f.StartHumans();
        for (int n = 0; n < 5; n++)
        {
            var current = f.Actor(clients); var command = new BaseGameAi().Decide(WireJson.Deserialize<M2.PlayerView>(current.ViewJson)); command.Id = "snake-setup-" + n;
            Assert.True(f.Directory.Handle(f.Command(current, command)).Success);
        }
        var human = f.Actor(clients); Assert.Equal(M2.GamePhase.SetupRoad, WireJson.Deserialize<M2.PlayerView>(human.ViewJson).Phase);
        foreach (var client in clients) f.Directory.Disconnect(client.ConnectionId);
        var resumed = f.Directory.Handle(Fixture.Request("Reconnect", human)); resumed.Credential = human.Credential; Assert.False(resumed.HumanControlAvailable);
        Assert.Equal(1, await f.Directory.TickAiAsync());
        var returned = f.Directory.Handle(Fixture.Request("Get", resumed)); var visible = WireJson.Deserialize<M2.PlayerView>(returned.ViewJson);
        Assert.True(returned.HumanControlAvailable); Assert.Equal(human.SeatId, visible.ActivePlayerId); Assert.Equal(M2.GamePhase.SetupSettlement, visible.Phase);
    }

    [Fact]
    public async Task LateHeartbeatThenLastOnlinePlayerLeaveClosesWithoutLeakingPrivateState()
    {
        var clock = DateTimeOffset.UtcNow;
        using var f = new Fixture(new RoomDirectoryOptions { Clock = () => clock, ConnectionTimeout = TimeSpan.FromSeconds(10) });
        var clients = f.StartHumans(); var actor = f.Actor(clients);
        clock += TimeSpan.FromSeconds(11);
        var late = f.Directory.Handle(Fixture.Request("Get", actor));
        Assert.False(late.HumanControlAvailable); Assert.True(late.Room.Seats.Single(s => s.SeatId == actor.SeatId).RejoinPending);
        Assert.True(f.Send(actor, "Leave").Success); Assert.Empty(f.Directory.ListPublicRooms());
        Assert.Equal(0, await f.Directory.TickAiAsync());
        var description = f.Directory.Handle(new() { Operation = "Describe", RoomId = actor.RoomId });
        Assert.Equal("RoomNotFound",description.Code);Assert.Null(description.Room); Assert.Null(description.ViewJson); Assert.Null(description.Credential);
        var publicWire = WireJson.Serialize(description);
        foreach (var forbidden in new[] { "CredentialHash", "RejoinAfterTurnKey", "RandomState", "OwnResources", "GameSave" }) Assert.DoesNotContain(forbidden, publicWire);
        var resumed = f.Directory.Handle(Fixture.Request("Reconnect", actor)); Assert.False(resumed.Success);Assert.Equal("RoomNotFound",resumed.Code);
    }

    [Fact]
    public void ConfiguredAiNamesAreDistinctAndPersistAcrossRestartAndConfiguration()
    {
        using var f = new Fixture(); var host = f.Create(aiSeats: new[] { "P2", "P3" });
        var expected = host.Room.Seats.Where(s => s.IsAi).ToDictionary(s => s.SeatId, s => s.DisplayName);
        Assert.Equal(2, expected.Values.Distinct().Count());
        foreach (var name in expected.Values) Assert.Contains(name, new[] { "哈基米", "吃白饭的大肥鲸", "糖包", "我是奶龙" });
        Assert.True(f.Send(host, "Configure", new RoomSettings { Name = "保留名字", PlayerCount = 4, AiSeats = new[] { "P2", "P3", "P4" } }).Success);
        f.Restart(); var resumed = f.Directory.Handle(Fixture.Request("Reconnect", host)); Assert.True(resumed.Success);
        foreach (var pair in expected) Assert.Equal(pair.Value, resumed.Room.Seats.Single(s => s.SeatId == pair.Key).DisplayName);
        Assert.Equal(3, resumed.Room.Seats.Where(s => s.IsAi).Select(s => s.DisplayName).Distinct().Count());
    }

    [Fact]
    public void AuthenticatedPlayerCanRenameOnlyOwnSeatBeforeAndAfterStartingAndNameSurvivesRestart()
    {
        using var f = new Fixture(); var host = f.Create(); var guest = f.Join(host); var third = f.Join(host);
        var before = f.Directory.Handle(Fixture.Request("Get", guest));
        var renamed = f.Send(guest, "Rename", new NamePayload { DisplayName = "赵云🀄" });
        Assert.True(renamed.Success); Assert.Equal(before.Revision + 1, renamed.Revision);
        Assert.Equal("赵云🀄", renamed.Room.Seats.Single(s => s.SeatId == guest.SeatId).DisplayName);
        Assert.Equal(host.Room.Seats.Single(s => s.SeatId == host.SeatId).DisplayName, renamed.Room.Seats.Single(s => s.SeatId == host.SeatId).DisplayName);
        var repeated = f.Send(guest, "Rename", new NamePayload { DisplayName = "赵云🀄" }); Assert.True(repeated.Success); Assert.Equal(renamed.Revision, repeated.Revision);
        foreach (var client in new[] { host, guest, third }) Assert.True(f.Send(client, "Ready", new ReadyPayload { Ready = true }).Success);
        Assert.True(f.Send(host, "Start").Success);
        var gameBefore = f.Directory.Handle(Fixture.Request("Get", guest));
        var duringGame = f.Send(guest, "Rename", new NamePayload { DisplayName = "新玩家名称" });
        Assert.True(duringGame.Success); Assert.Equal(gameBefore.Revision + 1, duringGame.Revision); Assert.Equal(gameBefore.ViewJson, duringGame.ViewJson);
        f.Restart(); var restored = f.Directory.Handle(Fixture.Request("Reconnect", guest)); Assert.True(restored.Success);
        Assert.Equal("新玩家名称", restored.Room.Seats.Single(s => s.SeatId == guest.SeatId).DisplayName);
    }

    [Fact]
    public void RenameRequiresValidOwnCredentialConnectionRevisionAndNameAndFailedSaveRollsBack()
    {
        using var f = new Fixture(); var clients = f.StartHumans(); var client = clients[1];
        var before = f.Directory.Handle(Fixture.Request("Get", client));
        var request = Fixture.Request("Rename", client); request.ExpectedRevision = before.Revision; request.Payload = WireJson.Serialize(new NamePayload { DisplayName = "合法名字" });
        request.Credential = null; Assert.Equal("Unauthorized", f.Directory.Handle(request).Code);
        request.Credential = client.Credential; request.ConnectionId = clients[0].ConnectionId; Assert.Equal("ConnectionSuperseded", f.Directory.Handle(request).Code);
        request.ConnectionId = client.ConnectionId; request.ExpectedRevision--; Assert.Equal("StaleRevision", f.Directory.Handle(request).Code);
        request.ExpectedRevision = before.Revision;
        foreach (var name in new[] { "", "  ", "换行\n名字", new string('名', 41) })
        {
            request.Payload = WireJson.Serialize(new NamePayload { DisplayName = name }); Assert.Equal("InvalidRequest", f.Directory.Handle(request).Code);
        }
        request.Payload = "{\"DisplayName\":\"伪造\",\"SeatId\":\"P1\"}"; Assert.Equal("InvalidRequest", f.Directory.Handle(request).Code);
        var unchanged = f.Directory.Handle(Fixture.Request("Get", client)); Assert.Equal(before.Revision, unchanged.Revision); Assert.Equal(before.ViewJson, unchanged.ViewJson);
        request.Payload = WireJson.Serialize(new NamePayload { DisplayName = "保存失败的名字" }); f.Storage.Fail = true;
        var failed = f.Directory.Handle(request); Assert.Equal("PersistenceFailed", failed.Code); Assert.Equal(before.Revision, failed.Revision);
        Assert.Equal(before.Room.Seats.Single(s => s.SeatId == client.SeatId).DisplayName, failed.Room.Seats.Single(s => s.SeatId == client.SeatId).DisplayName);
        f.Storage.Fail = false; Assert.True(f.Send(clients[0], "Recover").Success); Assert.True(f.Directory.Handle(request).Success);
        f.Restart(); var restored = f.Directory.Handle(Fixture.Request("Reconnect", client)); Assert.True(restored.Success);
        Assert.Equal("保存失败的名字", restored.Room.Seats.Single(s => s.SeatId == client.SeatId).DisplayName);
    }

    private sealed class MemoryStorage : IRoomStorage
    {
        public bool Fail; public readonly Dictionary<string, string> Documents = new();
        public IEnumerable<string> LoadAll() => Documents.Values.ToArray();
        public void Commit(string roomId, string privateDocument) { if (Fail) throw new IOException("模拟写入失败"); Documents[roomId] = privateDocument; }
        public void Delete(string roomId) { if (Fail) throw new IOException("模拟删除失败"); Documents.Remove(roomId); }
        public void Dispose() { }
    }
    private sealed class Fixture : IDisposable
    {
        public readonly MemoryStorage Storage = new(); public RoomDirectory Directory; private readonly RoomDirectoryOptions? options;
        public NetworkResponse[] Clients = Array.Empty<NetworkResponse>();
        public Fixture(RoomDirectoryOptions? options = null) { this.options = options; Directory = new RoomDirectory(Storage, File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "scenario.json")), options); }
        public void Restart() { Directory.Dispose(); Directory = new RoomDirectory(Storage, File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "scenario.json")), options); }
        public NetworkResponse Create(string rules = "base", string scenario = "base", int seats = 3, string[]? aiSeats = null, string? password = null)
        {
            var request = new NetworkRequest { Operation = "Create", ExpectedRulesVersion = NetworkProtocol.RulesVersion(rules), ExpectedScenarioVersion = NetworkProtocol.ScenarioVersion(rules), Payload = WireJson.Serialize(new CreateRoomPayload { Password = password, Settings = new RoomSettings { RuleSet = rules, ScenarioId = scenario, PlayerCount = seats, AiSeats = aiSeats ?? Array.Empty<string>() } }) };
            return Directory.Handle(request);
        }
        public NetworkResponse Join(NetworkResponse room, string? password = null) { var request = Request("Join", room); request.Payload = WireJson.Serialize(new JoinRoomPayload { Password = password }); return Directory.Handle(request); }
        public NetworkResponse Send(NetworkResponse client, string operation, object? payload = null)
        {
            var current = Directory.Handle(Request("Get", client)); var request = Request(operation, client); request.ExpectedRevision = current.Revision; request.Payload = payload == null ? null : WireJson.SerializeObject(payload); return Directory.Handle(request);
        }
        public static NetworkRequest Request(string operation, NetworkResponse client) => new() { Operation = operation, RoomId = client.RoomId, Credential = client.Credential, ConnectionId = client.ConnectionId, RuleSet = client.RuleSet, ExpectedRulesVersion = NetworkProtocol.RulesVersion(client.RuleSet), ExpectedScenarioVersion = NetworkProtocol.ScenarioVersion(client.RuleSet), ExpectedRevision = client.Revision };
        public NetworkResponse[] StartHumans()
        {
            var first = Create(); Clients = new[] { first, Join(first), Join(first) }; foreach (var c in Clients) Assert.True(Send(c, "Ready", new ReadyPayload { Ready = true }).Success); Assert.True(Send(first, "Start").Success); return Clients;
        }
        public NetworkResponse Actor(NetworkResponse[] clients)
        {
            var first = Directory.Handle(Request("Get", clients[0])); var view = WireJson.Deserialize<M2.PlayerView>(first.ViewJson); var original = clients.Single(c => c.SeatId == view.ActivePlayerId); var fresh = Directory.Handle(Request("Get", original)); fresh.Credential = original.Credential; return fresh;
        }
        public NetworkRequest Command(NetworkResponse client, M2.Command command) { var request = Request("Command", client); request.Payload = WireJson.Serialize(command); request.CommandId = command.Id; return request; }
        public void Dispose() => Directory.Dispose();
    }
}
