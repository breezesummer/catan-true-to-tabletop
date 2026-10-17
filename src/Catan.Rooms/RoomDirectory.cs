using System.Security.Cryptography;
using System.Text;
using Catan.Core;
using Catan.Network.Contracts;

namespace Catan.Rooms;

public sealed class RoomDirectoryOptions
{
    public TimeSpan CredentialLifetime { get; set; } = TimeSpan.FromDays(180);
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(45);
    public int MaximumRooms { get; set; } = 128;
    public int MaximumCommittedCommands { get; set; } = 100000;
    public Func<DateTimeOffset> Clock { get; set; } = () => DateTimeOffset.UtcNow;
}

/// <summary>服务端权威房间入口；每个房间严格顺序执行，提交后才发布可见结果。</summary>
public sealed class RoomDirectory : IDisposable
{
    private readonly object directoryGate = new();
    private readonly Dictionary<string, Room> rooms = new(StringComparer.Ordinal);
    private readonly IRoomStorage storage;
    private readonly string baseScenario;
    private readonly RoomDirectoryOptions options;

    public RoomDirectory(string storageDirectory, string baseScenarioJson, RoomDirectoryOptions options = null)
        : this(new AtomicRoomStorage(storageDirectory), baseScenarioJson, options) { }
    public RoomDirectory(IRoomStorage storage, string baseScenarioJson, RoomDirectoryOptions options = null)
    {
        this.storage = storage; baseScenario = baseScenarioJson; this.options = options ?? new();
        if (this.options.CredentialLifetime <= TimeSpan.Zero || this.options.ConnectionTimeout <= TimeSpan.Zero) throw new ArgumentException("身份或连接有效期无效。");
        if (this.options.MaximumCommittedCommands < 1 || this.options.MaximumCommittedCommands > 100000) throw new ArgumentException("命令上限不能超过规则存档恢复上限。");
        try
        {
            foreach (var json in storage.LoadAll())
            {
                var saved = PrivateJson.Read<SavedRoom>(json);
                ValidateRecovery(saved);
                rooms.Add(saved.RoomId, new Room(this, saved));
            }
        }
        catch { storage.Dispose(); throw new InvalidDataException("房间存档无法验证；原文件已保留，请检查版本和备份。"); }
    }
    public NetworkResponse Handle(NetworkRequest request)
    {
        if (request == null) return Reject("InvalidRequest", "请求不能为空。");
        NetworkResponse response;
        try
        {
            if (request.ProtocolVersion != NetworkProtocol.Version) response = Reject("ProtocolMismatch", "联机协议不同，请更新至同一版本。");
            else if (Encoding.UTF8.GetByteCount(WireJson.Serialize(request)) > NetworkProtocol.MaximumRequestBytes) response = Reject("MessageTooLarge", "请求过大。");
            else if (request.Operation == "List") response = new() { Success = true, Rooms = ListPublicRooms() };
            else if (request.Operation == "Create") response = Create(request);
            else
            {
                Room room;
                lock (directoryGate) rooms.TryGetValue(request.RoomId ?? "", out room);
                response = room == null ? Reject("RoomNotFound", "没有找到该房间。") : room.Handle(request);
            }
        }
        catch (Exception ex) when (ex is ArgumentException || ex is System.Runtime.Serialization.SerializationException || ex is System.Xml.XmlException || ex is FormatException || ex is OverflowException || ex is NullReferenceException)
        { response = Reject("InvalidRequest", "请求字段无效，请刷新后重试。"); }
        response.RequestId = request.RequestId;
        return response;
    }
    public RoomSummary[] ListPublicRooms()
    {
        Room[] copy; lock (directoryGate) copy = rooms.Values.ToArray();
        return copy.Select(r => r.Summary()).Where(r => r.Listed).OrderBy(r => r.RoomId, StringComparer.Ordinal).ToArray();
    }
    public void Disconnect(string connectionId)
    {
        if (string.IsNullOrEmpty(connectionId)) return;
        Room[] copy; lock (directoryGate) copy = rooms.Values.ToArray();
        foreach (var room in copy) room.Disconnect(connectionId);
    }
    public Task<int> TickAiAsync(CancellationToken cancellationToken = default)
    {
        Room[] copy; lock (directoryGate) copy = rooms.Values.ToArray();
        int advanced = 0;
        foreach (var room in copy) { cancellationToken.ThrowIfCancellationRequested(); if (room.TickAi()) advanced++; }
        return Task.FromResult(advanced);
    }
    public void Dispose() => storage.Dispose();
    private NetworkResponse Create(NetworkRequest request)
    {
        var payload = WireJson.Deserialize<CreateRoomPayload>(request.Payload ?? "{}");
        ValidateSettings(payload.Settings);
        ValidateName(payload.DisplayName);
        if ((payload.Password?.Length ?? 0) > 128) return Reject("InvalidPassword", "房间密码过长。");
        var versions = RuleRoomGame.Versions(payload.Settings.RuleSet);
        if (!VersionsMatch(request, versions.Rules, versions.Scenario)) return Reject("VersionMismatch", "规则或场景版本不同，请更新至同一版本。");
        if (payload.Settings.AiSeats.Contains("P1")) return Reject("SeatOccupied", "创建者的 P1 席位不能同时设置为 AI。");
        var enrollment = EnrollmentIntent.Parse(request, payload);
        lock (directoryGate)
        {
            if (enrollment != null)
            {
                var previous = rooms.Values.FirstOrDefault(room => room.HasEnrollment(enrollment));
                if (previous != null) return previous.ReplayEnrollment(enrollment);
            }
            if (rooms.Values.Count(room => room.CountsTowardRoomLimit) >= options.MaximumRooms) return Reject("RoomLimit", "房间数量已达到此服务上限。");
            var credential = enrollment?.Credential ?? PrivateJson.Token();
            var seat = NewSeat("P1", payload.DisplayName, credential);
            var state = new SavedRoom
            {
                RoomId = Convert.ToHexString(RandomNumberGenerator.GetBytes(6)), Revision = 1,
                Settings = payload.Settings, ManagerSeatId = "P1", Seats = new() { seat },
                RulesVersion = versions.Rules, ScenarioVersion = versions.Scenario
            };
            AssignAiNames(state);
            RecordEnrollment(state, seat, enrollment);
            if (!string.IsNullOrEmpty(payload.Password))
            {
                state.PasswordSalt = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
                state.PasswordHash = PasswordHash(payload.Password, state.PasswordSalt);
            }
            try { storage.Commit(state.RoomId, PrivateJson.Write(state)); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return Reject("PersistenceFailed", "房间保存失败，未创建房间。"); }
            var room = new Room(this, state); rooms.Add(state.RoomId, room);
            return room.Created(enrollment == null ? credential : null);
        }
    }
    private SavedSeat NewSeat(string seat, string name, string credential) => new() { SeatId = seat, DisplayName = name, CredentialHash = PrivateJson.Hash(credential), ConnectionId = PrivateJson.Token(), CredentialExpiresAtUtc = options.Clock() + options.CredentialLifetime };
    private static bool VersionsMatch(NetworkRequest r, string rules, string scenario)
    {
        bool handshake = r.Operation == "Create" || r.Operation == "Join" || r.Operation == "Reconnect";
        if (handshake && (string.IsNullOrEmpty(r.ExpectedRulesVersion) || string.IsNullOrEmpty(r.ExpectedScenarioVersion))) return false;
        return (string.IsNullOrEmpty(r.ExpectedRulesVersion) || r.ExpectedRulesVersion == rules) && (string.IsNullOrEmpty(r.ExpectedScenarioVersion) || r.ExpectedScenarioVersion == scenario);
    }
    private static void ValidateName(string name) { if (string.IsNullOrWhiteSpace(name) || name.Length > 40 || name.Any(char.IsControl)) throw new ArgumentException("名称无效。"); }
    private static void ValidateSettings(RoomSettings s)
    {
        if (s == null || s.PlayerCount != 3 && s.PlayerCount != 4) throw new ArgumentException("人数无效。");
        if (s.RuleSet == "cities") s.RuleSet = "cities-knights";
        ValidateName(s.Name); RuleRoomGame.Versions(s.RuleSet);
        if (s.RuleSet == "base" || s.RuleSet == "cities-knights") { if (s.ScenarioId != "base") throw new ArgumentException("基础场景无效。"); }
        else if (s.RuleSet == "seafarers" ? !Catan.Core.M3.SeafarersScenarios.Ids.Contains(s.ScenarioId) : !new[] { "heading-for-new-shores", "through-the-desert" }.Contains(s.ScenarioId)) throw new ArgumentException("剧本无效。");
        if (s.TargetVictoryPoints.HasValue && !VictoryPointSettings.IsValid(s.TargetVictoryPoints, VictoryPointSettings.MaximumForScenario(s.ScenarioId))) throw new ArgumentException("胜利分数无效。");
        if (s.AiSeats == null || s.AiSeats.Distinct().Count() != s.AiSeats.Length || s.AiSeats.Any(id => !SeatIds(s.PlayerCount).Contains(id))) throw new ArgumentException("AI 席位无效。");
    }
    private static IEnumerable<string> SeatIds(int count) => Enumerable.Range(1, count).Select(i => "P" + i);
    private static readonly string[] AiNames = { "哈基米", "吃白饭的大肥鲸", "糖包", "我是奶龙" };
    private static void AssignAiNames(SavedRoom room)
    {
        room.AiDisplayNames ??= new(StringComparer.Ordinal);
        foreach (var id in room.AiDisplayNames.Keys.Except(room.Settings.AiSeats).ToArray()) room.AiDisplayNames.Remove(id);
        foreach (var id in room.Settings.AiSeats.Where(id => !room.AiDisplayNames.ContainsKey(id)))
        {
            var available = AiNames.Except(room.AiDisplayNames.Values).ToArray();
            room.AiDisplayNames[id] = available[RandomNumberGenerator.GetInt32(available.Length)];
        }
    }
    private static void ValidateRecovery(SavedRoom room)
    {
        if (room == null || room.Format != 1 || room.ProtocolVersion != 1 && room.ProtocolVersion != NetworkProtocol.Version || room.Revision < 1 || room.RoomId?.Length != 12 || room.Receipts == null || room.Seats == null || room.Enrollments == null) throw new InvalidDataException();
        // v2 新增的姓名/托管字段不改变权威规则存档；只迁移已知 v1 私有格式，对外不接受旧协议。
        // 原存档保留到下一次原子事务提交，不在恢复过程覆盖文件。
        room.ProtocolVersion = NetworkProtocol.Version;
        ValidateSettings(room.Settings);
        AssignAiNames(room);
        var versions = RuleRoomGame.Versions(room.Settings.RuleSet);
        if (versions.Rules != room.RulesVersion || versions.Scenario != room.ScenarioVersion) throw new InvalidDataException();
        if (room.Seats.Select(s => s.SeatId).Distinct().Count() != room.Seats.Count || room.Seats.Any(s => !SeatIds(room.Settings.PlayerCount).Contains(s.SeatId) || s.CredentialHash == null || s.CredentialHash.Length != 64 || room.Settings.AiSeats.Contains(s.SeatId))) throw new InvalidDataException();
        if (room.Seats.Count > 0 ? !room.Seats.Any(s => s.SeatId == room.ManagerSeatId) : room.ManagerSeatId != null) throw new InvalidDataException();
        if (room.Enrollments.Any(e => e.Key.Length != 64 || e.Value == null || string.IsNullOrEmpty(e.Value.EnrollmentId) || e.Value.Fingerprint?.Length != 64 || e.Value.Operation != "Create" && e.Value.Operation != "Join" || e.Value.Revision < 1 || e.Value.Revision > room.Revision)) throw new InvalidDataException();
    }
    private sealed class EnrollmentIntent
    {
        internal string Id, Credential, CredentialHash, Fingerprint, Operation;
        internal static EnrollmentIntent Parse(NetworkRequest request, object payload)
        {
            if (request.EnrollmentId == null && request.EnrollmentCredential == null) return null;
            if (string.IsNullOrWhiteSpace(request.EnrollmentId) || request.EnrollmentId.Length > 128 || request.EnrollmentId.Any(char.IsControl) || request.EnrollmentCredential?.Length != 64 || request.EnrollmentCredential.Any(c => !Uri.IsHexDigit(c))) throw new ArgumentException("首次加入的恢复标识无效。");
            return new EnrollmentIntent
            {
                Id = request.EnrollmentId, Credential = request.EnrollmentCredential, CredentialHash = PrivateJson.Hash(request.EnrollmentCredential), Operation = request.Operation,
                // payload 可能包含房间口令；不能把低熵口令变成可离线快速枚举的额外 SHA-256 校验值。
                // 客户端高熵恢复凭据只以 hash 保存，HMAC 密钥本身不落盘。
                Fingerprint = Convert.ToHexString(HMACSHA256.HashData(Convert.FromHexString(request.EnrollmentCredential), Encoding.UTF8.GetBytes(request.Operation + "\n" + (request.Operation == "Join" ? request.RoomId : "") + "\n" + request.ExpectedRulesVersion + "\n" + request.ExpectedScenarioVersion + "\n" + WireJson.SerializeObject(payload))))
            };
        }
    }
    private static void RecordEnrollment(SavedRoom state, SavedSeat seat, EnrollmentIntent enrollment)
    {
        if (enrollment == null) return;
        seat.EnrollmentId = enrollment.Id; seat.EnrollmentFingerprint = enrollment.Fingerprint;
        state.Enrollments.Add(enrollment.CredentialHash, new SavedEnrollment { EnrollmentId = enrollment.Id, Fingerprint = enrollment.Fingerprint, Operation = enrollment.Operation, Revision = state.Revision });
    }
    private static string PasswordHash(string password, string salt) => Convert.ToHexString(Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromHexString(salt), 100000, HashAlgorithmName.SHA256, 32));
    private static bool SameSecret(string a, string b) => a != null && b != null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
    private static NetworkResponse Reject(string code, string message) => new() { Success = false, Code = code, Message = message };

    private sealed class Room
    {
        private readonly object gate = new();
        private readonly RoomDirectory owner;
        private SavedRoom state;
        private RuleRoomGame game;
        private readonly Dictionary<string, DateTimeOffset> lastSeen = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DateTimeOffset> disconnectedSince = new(StringComparer.Ordinal);
        private bool paused;
        internal Room(RoomDirectory owner, SavedRoom state)
        {
            this.owner = owner; this.state = state;
            foreach (var seat in state.Seats) disconnectedSince[seat.SeatId] = owner.options.Clock();
            if (state.GameSave != null) { game = RuleRoomGame.Load(state.Settings.RuleSet, state.GameSave, owner.baseScenario); game.Save(); }
        }
        private bool Connected(SavedSeat seat) => seat.ConnectionId != null && lastSeen.TryGetValue(seat.ConnectionId, out var seen) && owner.options.Clock() - seen < owner.options.ConnectionTimeout;
        internal bool CountsTowardRoomLimit { get { lock (gate) return !state.Closed && !(game?.Finished ?? false) && (game != null || state.Seats.Any(Connected)); } }
        internal RoomSummary Summary() { lock (gate) return PublicSummary(); }
        private RoomSummary PublicSummary() => new()
        {
            RoomId = state.RoomId, Name = state.Settings.Name, RuleSet = state.Settings.RuleSet, ScenarioId = state.Settings.ScenarioId,
            RulesVersion = state.RulesVersion, ScenarioVersion = state.ScenarioVersion, PlayerCount = state.Settings.PlayerCount,
            OccupiedSeats = state.Seats.Count + state.Settings.AiSeats.Length, TargetVictoryPoints = state.Settings.TargetVictoryPoints,
            HasPassword = state.PasswordHash != null, Listed = state.Settings.Listed && !state.Closed && !(game?.Finished ?? false) && state.Seats.Any(Connected), Started = game != null, Finished = game?.Finished ?? false,
            Paused = paused, ManagerSeatId = state.ManagerSeatId, Revision = state.Revision,
            Seats = SeatIds(state.Settings.PlayerCount).Select(id =>
            {
                var human = state.Seats.SingleOrDefault(s => s.SeatId == id); bool ai = state.Settings.AiSeats.Contains(id);
                return new SeatSummary { SeatId = id, DisplayName = ai ? state.AiDisplayNames[id] : human?.DisplayName, IsAi = ai, ControlledByAi = ai || human?.ControlledByAi == true || game != null && human != null && !Connected(human), RejoinPending = human?.RejoinAfterTurnKey != null, Ready = ai || human?.Ready == true, Connected = human != null && Connected(human), Occupied = ai || human != null };
            }).ToArray()
        };
        internal NetworkResponse Created(string credential)
        {
            lock (gate) { var seat = state.Seats[0]; lastSeen[seat.ConnectionId] = owner.options.Clock(); disconnectedSince.Remove(seat.SeatId); return Snapshot(seat, credential: credential); }
        }
        internal bool HasEnrollment(EnrollmentIntent enrollment)
        {
            lock (gate) return state.Enrollments.ContainsKey(enrollment.CredentialHash) || state.Enrollments.Values.Any(e => e.EnrollmentId == enrollment.Id) || state.Seats.Any(s => SameSecret(s.CredentialHash, enrollment.CredentialHash));
        }
        internal NetworkResponse ReplayEnrollment(EnrollmentIntent enrollment)
        {
            lock (gate)
            {
                if (!state.Enrollments.TryGetValue(enrollment.CredentialHash, out var receipt) || receipt.EnrollmentId != enrollment.Id || receipt.Fingerprint != enrollment.Fingerprint || receipt.Operation != enrollment.Operation)
                    return Reject("EnrollmentConflict", "同一加入标识或恢复凭据不能用于不同的申请。");
                var seat = state.Seats.SingleOrDefault(s => SameSecret(s.CredentialHash, enrollment.CredentialHash));
                if (seat == null || seat.CredentialExpiresAtUtc <= owner.options.Clock()) return Reject("Unauthorized", "原席位凭据已失效，不能重复领取。");
                var response = Reconnect(seat);
                if (response.Success) { response.IsDuplicate = true; response.CommittedRevision = receipt.Revision; }
                return response;
            }
        }
        private NetworkResponse Snapshot(SavedSeat seat, string code = null, string message = "已同步。", bool success = true, string credential = null)
            => new() { Success = success, Code = code, Message = message, RoomId = state.RoomId, Revision = state.Revision, CommittedRevision = state.Revision, SeatId = seat?.SeatId, HumanControlAvailable = seat != null && !state.Closed && (game == null || !seat.ControlledByAi && Connected(seat)), RuleSet = state.Settings.RuleSet, Room = PublicSummary(), ViewJson = seat == null || game == null ? null : game.ViewJson(seat.SeatId), Credential = credential, ConnectionId = seat?.ConnectionId, CredentialExpiresAtUtc = seat?.CredentialExpiresAtUtc.ToString("O") };
        internal NetworkResponse Handle(NetworkRequest request)
        {
            lock (gate)
            {
                if (request.Operation == "Describe") return new NetworkResponse { Success = true, RoomId = state.RoomId, RuleSet = state.Settings.RuleSet, Revision = state.Revision, Room = PublicSummary() };
                if (request.Operation == "Join") return Join(request);
                if (!VersionsMatch(request, state.RulesVersion, state.ScenarioVersion)) return Reject("VersionMismatch", "规则或场景版本不同，请更新至同一版本。");
                var hash = string.IsNullOrEmpty(request.Credential) ? null : PrivateJson.Hash(request.Credential);
                var seat = state.Seats.SingleOrDefault(s => SameSecret(s.CredentialHash, hash));
                if (seat == null || seat.CredentialExpiresAtUtc <= owner.options.Clock()) return Reject("Unauthorized", "席位凭据无效或已到期。");
                if (state.Closed) return Reject("RoomClosed", "该大厅房间已结束，请加入其他房间或重新创建。");
                if (request.Operation == "Reconnect") return Reconnect(seat);
                if (string.IsNullOrEmpty(request.ConnectionId) || !SameSecret(seat.ConnectionId, request.ConnectionId)) return Reject("ConnectionSuperseded", "该席位已由新连接接管，请重新连接。");
                if (game != null && !Connected(seat))
                {
                    var before = PrivateJson.Copy(state);
                    seat.ControlledByAi = true; seat.RejoinAfterTurnKey = game.TurnKey; state.Revision++;
                    if (!Commit(before)) return Reject("PersistenceFailed", "重连状态保存失败，请稍后重新连接。");
                }
                lastSeen[seat.ConnectionId] = owner.options.Clock(); disconnectedSince.Remove(seat.SeatId);
                if (request.Operation == "Get") return Snapshot(seat);
                if (request.Operation == "History")
                {
                    var query = WireJson.Deserialize<HistoryPayload>(request.Payload ?? "{}");
                    if (query.AfterSequence < 0 || query.Limit < 1 || query.Limit > 200) return Snapshot(seat, "InvalidHistory", "历史分页范围无效。", false);
                    var history = game?.History(seat.SeatId, query.AfterSequence) ?? Array.Empty<GameHistoryEntry>();
                    var page = history.Take(query.Limit).ToArray();
                    var response = Snapshot(seat); response.ViewJson = null; response.HistoryJson = WireJson.Serialize(page); response.HistoryCursor = page.LastOrDefault()?.Sequence ?? query.AfterSequence; response.HasMoreHistory = history.Length > page.Length; return response;
                }
                if (request.Operation == "Command" || request.Operation == "Preview") return Command(request, seat);
                if (request.Operation == "Recover")
                {
                    if (seat.SeatId != state.ManagerSeatId) return Snapshot(seat, "ManagerRequired", "只有管理员可以恢复保存。", false);
                    try { owner.storage.Commit(state.RoomId, PrivateJson.Write(state)); paused = false; return Snapshot(seat); }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return Snapshot(seat, "PersistenceFailed", "保存仍不可用，房间继续暂停。", false); }
                }
                if (paused) return Snapshot(seat, "RoomPaused", "保存失败后房间已暂停，请管理员恢复保存或重启服务。", false);
                if (request.ExpectedRevision != state.Revision) return Snapshot(seat, "StaleRevision", "房间已更新，请根据最新局面重试。", false);
                return ChangeRoom(request, seat);
            }
        }
        private NetworkResponse Join(NetworkRequest request)
        {
            var payload = WireJson.Deserialize<JoinRoomPayload>(request.Payload ?? "{}"); ValidateName(payload.DisplayName);
            var enrollment = EnrollmentIntent.Parse(request, payload);
            if (enrollment != null && HasEnrollment(enrollment)) return ReplayEnrollment(enrollment);
            if (state.Closed) return Reject("RoomClosed", "该大厅房间已结束，请加入其他房间或重新创建。");
            if (!VersionsMatch(request, state.RulesVersion, state.ScenarioVersion)) return Reject("VersionMismatch", "规则或场景版本不同，请更新至同一版本。");
            if (paused) return Reject("RoomPaused", "房间正在等待恢复保存。");
            if (game != null) return Reject("GameStarted", "对局已开始；原玩家请使用凭据重连。");
            if ((payload.Password?.Length ?? 0) > 128 || state.PasswordHash != null && !SameSecret(state.PasswordHash, PasswordHash(payload.Password ?? "", state.PasswordSalt))) return Reject("InvalidPassword", "房间密码不正确。");
            var free = SeatIds(state.Settings.PlayerCount).Except(state.Settings.AiSeats).Except(state.Seats.Select(s => s.SeatId)).ToArray();
            var id = payload.SeatId ?? free.FirstOrDefault();
            if (id == null || !free.Contains(id)) return Reject("SeatOccupied", "该席位不可用。");
            if (state.Enrollments.Count >= 100000) return Reject("EnrollmentLimit", "本房间已达到身份申请上限，请创建新房间。");
            var before = PrivateJson.Copy(state); var credential = enrollment?.Credential ?? PrivateJson.Token(); var seat = owner.NewSeat(id, payload.DisplayName, credential);
            state.Seats.Add(seat); if (state.ManagerSeatId == null) state.ManagerSeatId = seat.SeatId; state.Revision++;
            RecordEnrollment(state, seat, enrollment);
            if (!Commit(before)) return Reject("PersistenceFailed", "加入保存失败，未分配席位。");
            lastSeen[seat.ConnectionId] = owner.options.Clock(); disconnectedSince.Remove(seat.SeatId); return Snapshot(seat, credential: enrollment == null ? credential : null);
        }
        private NetworkResponse Reconnect(SavedSeat seat)
        {
            if (state.Closed) return Reject("RoomClosed", "该大厅房间已结束，请加入其他房间或重新创建。");
            var before = PrivateJson.Copy(state); var old = seat.ConnectionId; seat.ConnectionId = PrivateJson.Token();
            // 活跃连接替换不打断当前控制；真正离线后的重连需等待下一次自身回合。
            if (game != null && !Connected(before.Seats.Single(s => s.SeatId == seat.SeatId)))
            {
                seat.ControlledByAi = true; seat.RejoinAfterTurnKey = game.TurnKey; state.Revision++;
            }
            if (!Commit(before)) return Reject("PersistenceFailed", "连接接管保存失败，请稍后重试。");
            if (old != null) lastSeen.Remove(old); lastSeen[seat.ConnectionId] = owner.options.Clock(); disconnectedSince.Remove(seat.SeatId); return Snapshot(seat);
        }
        private NetworkResponse Command(NetworkRequest request, SavedSeat seat)
        {
            if (game == null) return Snapshot(seat, "NotStarted", "对局尚未开始。", false);
            bool preview = request.Operation == "Preview";
            if ((request.RuleSet == "cities" ? "cities-knights" : request.RuleSet) != state.Settings.RuleSet) return Snapshot(seat, "RuleSetMismatch", "命令规则包不匹配。", false);
            if (string.IsNullOrWhiteSpace(request.CommandId) || request.CommandId.Length > 128 || request.CommandId.Any(char.IsControl)) return Snapshot(seat, "InvalidCommandId", "命令标识无效。", false);
            object command;
            try { command = game.ParseCommand(request.Payload, seat.SeatId, request.CommandId); }
            catch (SeatForgeryException) { return Snapshot(seat, "SeatForgery", "不能替其他席位发出命令。", false); }
            var fingerprint = PrivateJson.Hash(WireJson.SerializeObject(command));
            // 身份与连接已验证；必须先处理成功重试，再检查修订号。
            if (!preview && state.Receipts.TryGetValue(request.CommandId, out var previous))
            {
                if (previous.SeatId != seat.SeatId || previous.Fingerprint != fingerprint) return Snapshot(seat, "CommandConflict", "同一命令标识不能用于不同操作。", false);
                var duplicate = Snapshot(seat, previous.Code, previous.Message, previous.Success); duplicate.IsDuplicate = true; duplicate.CommittedRevision = previous.Revision; return duplicate;
            }
            if (seat.ControlledByAi) return Snapshot(seat, "AiTakeover", "AI 正在接管，重连后将在下一次自己的行动回合恢复操作。", false);
            if (!preview && game.CommandCount >= owner.options.MaximumCommittedCommands) return Snapshot(seat, "CommandLimit", "本房间已达到可恢复的命令上限，最后保存局面已保留。", false);
            if (paused) return Snapshot(seat, "RoomPaused", "保存失败后房间已暂停，请管理员恢复保存。", false);
            if (request.ExpectedRevision != state.Revision) return Snapshot(seat, "StaleRevision", "局面已更新，请根据最新局面重新决定。", false);
            if (preview) { var check = game.Execute(command, true); return Snapshot(seat, check.Code, check.Message, check.Success); }
            if (state.Receipts.Count >= 100000) return Snapshot(seat, "CommandLimit", "本房间已达到保存命令上限。", false);
            var before = PrivateJson.Copy(state); var result = game.Execute(command, false);
            if (result.Success) { state.Revision++; state.GameSave = game.Save(); RestoreHumanControlAtTurnBoundary(); }
            state.Receipts.Add(request.CommandId, new SavedReceipt { SeatId = seat.SeatId, Fingerprint = fingerprint, Success = result.Success, Code = result.Code, Message = result.Message, Revision = state.Revision });
            if (!Commit(before)) return Snapshot(state.Seats.Single(s => s.SeatId == seat.SeatId), "PersistenceFailed", "操作未提交；房间已恢复到最后保存局面并暂停。", false);
            return Snapshot(seat, result.Code, result.Message, result.Success);
        }
        private NetworkResponse ChangeRoom(NetworkRequest request, SavedSeat seat)
        {
            var before = PrivateJson.Copy(state);
            var originalCredential = seat.CredentialHash;
            bool manager = seat.SeatId == state.ManagerSeatId;
            switch (request.Operation)
            {
                case "Rename":
                    var name = WireJson.Deserialize<NamePayload>(request.Payload ?? "{}").DisplayName; ValidateName(name);
                    if (seat.DisplayName == name) return Snapshot(seat);
                    seat.DisplayName = name; break;
                case "Ready":
                    if (game != null) return Snapshot(seat, "GameStarted", "对局已开始。", false);
                    seat.Ready = WireJson.Deserialize<ReadyPayload>(request.Payload ?? "{}").Ready; break;
                case "Seat":
                    if (game != null) return Snapshot(seat, "GameStarted", "对局已开始。", false);
                    var id = WireJson.Deserialize<SeatPayload>(request.Payload).SeatId;
                    if (!SeatIds(state.Settings.PlayerCount).Contains(id) || state.Settings.AiSeats.Contains(id) || state.Seats.Any(s => s.SeatId == id)) return Snapshot(seat, "SeatOccupied", "该席位不可用。", false);
                    if (manager) state.ManagerSeatId = id; seat.SeatId = id; ResetReady(); break;
                case "Configure":
                    if (!manager) return Snapshot(seat, "ManagerRequired", "只有管理员可以修改设置。", false);
                    if (game != null) return Snapshot(seat, "GameStarted", "对局已开始。", false);
                    var settings = WireJson.Deserialize<RoomSettings>(request.Payload); ValidateSettings(settings);
                    if (state.Seats.Any(s => !SeatIds(settings.PlayerCount).Contains(s.SeatId) || settings.AiSeats.Contains(s.SeatId))) return Snapshot(seat, "SeatOccupied", "新配置不能移除或覆盖人类席位。", false);
                    state.Settings = settings; AssignAiNames(state); (state.RulesVersion, state.ScenarioVersion) = RuleRoomGame.Versions(settings.RuleSet); ResetReady(); break;
                case "Start":
                    if (!manager) return Snapshot(seat, "ManagerRequired", "只有管理员可以开始。", false);
                    if (game != null) return Snapshot(seat, "GameStarted", "对局已开始。", false);
                    if (state.Seats.Count + state.Settings.AiSeats.Length != state.Settings.PlayerCount || state.Seats.Any(s => !s.Ready || !Connected(s))) return Snapshot(seat, "NotReady", "需要所有席位有人或 AI，且全部人类在线并准备。", false);
                    game = RuleRoomGame.Create(state.Settings, owner.baseScenario); state.GameSave = game.Save(); break;
                case "Transfer":
                    if (!manager) return Snapshot(seat, "ManagerRequired", "只有管理员可以移交管理权。", false);
                    var target = WireJson.Deserialize<SeatPayload>(request.Payload).SeatId;
                    if (!state.Seats.Any(s => s.SeatId == target)) return Snapshot(seat, "InvalidSeat", "管理权只能移交给人类席位。", false);
                    state.ManagerSeatId = target; break;
                case "Leave":
                case "Revoke":
                    seat.ConnectionId = null;
                    if (game == null) { state.Seats.Remove(seat); ResetReady(); }
                    else
                    {
                        seat.ControlledByAi = true; seat.RejoinAfterTurnKey = null;
                        if (request.Operation == "Revoke") { seat.CredentialHash = PrivateJson.Hash(PrivateJson.Token()); seat.CredentialExpiresAtUtc = owner.options.Clock(); }
                    }
                    if (manager) state.ManagerSeatId = state.Seats.Where(s => s != seat).OrderByDescending(Connected).Select(s => s.SeatId).FirstOrDefault() ?? state.Seats.FirstOrDefault()?.SeatId;
                    // 最后一位大厅玩家离开即结束；保留私有存档和身份收据，公开列表不再展示历史房间。
                    if (game == null && state.Seats.Count == 0) state.Closed = true;
                    break;
                default: return Snapshot(seat, "UnknownOperation", "未知联机操作。", false);
            }
            state.Revision++;
            if (!Commit(before)) return Snapshot(state.Seats.SingleOrDefault(s => SameSecret(s.CredentialHash, originalCredential)), "PersistenceFailed", "房间修改未保存，已暂停。", false);
            return request.Operation == "Leave" || request.Operation == "Revoke" ? new NetworkResponse { Success = true, RoomId = state.RoomId, Revision = state.Revision, Message = "已离开房间。" } : Snapshot(seat);
        }
        private void ResetReady() { foreach (var seat in state.Seats) seat.Ready = false; }
        private bool Commit(SavedRoom before)
        {
            try { owner.storage.Commit(state.RoomId, PrivateJson.Write(state)); return true; }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                state = before; game = before.GameSave == null ? null : RuleRoomGame.Load(before.Settings.RuleSet, before.GameSave, owner.baseScenario); paused = true; return false;
            }
        }
        internal void Disconnect(string connectionId)
        {
            lock (gate)
            {
                var seat = state.Seats.SingleOrDefault(s => s.ConnectionId == connectionId); if (seat == null) return;
                lastSeen.Remove(connectionId);
                disconnectedSince[seat.SeatId] = owner.options.Clock();
                var before = PrivateJson.Copy(state);
                bool changed = false;
                if (game != null && (!seat.ControlledByAi || seat.RejoinAfterTurnKey != null))
                {
                    seat.ControlledByAi = true; seat.RejoinAfterTurnKey = null; changed = true;
                }
                if (seat.SeatId == state.ManagerSeatId)
                {
                    var next = state.Seats.FirstOrDefault(s => s != seat && Connected(s));
                    if (next != null) { state.ManagerSeatId = next.SeatId; changed = true; }
                }
                if (changed) { state.Revision++; Commit(before); }
            }
        }
        private bool CanRestoreHumanControl(SavedSeat seat) => game != null && !game.Finished && seat.ControlledByAi && Connected(seat) && seat.RejoinAfterTurnKey != null && seat.RejoinAfterTurnKey != game.TurnKey && game.ActivePlayerId == seat.SeatId;
        private bool RestoreHumanControlAtTurnBoundary()
        {
            if (game == null || game.Finished) return false;
            bool changed = false;
            foreach (var seat in state.Seats)
                if (CanRestoreHumanControl(seat))
                {
                    seat.ControlledByAi = false; seat.RejoinAfterTurnKey = null; changed = true;
                }
            return changed;
        }
        internal bool TickAi()
        {
            lock (gate)
            {
                if (state.Closed) return false;
                if (!paused)
                {
                    SavedRoom prior = null; bool changed = false;
                    void PreserveBeforeChange() => prior ??= PrivateJson.Copy(state);
                    foreach (var seat in state.Seats.ToArray())
                    {
                        if (Connected(seat)) continue;
                        if (game != null)
                        {
                            if (!seat.ControlledByAi || seat.RejoinAfterTurnKey != null) { PreserveBeforeChange(); seat.ControlledByAi = true; seat.RejoinAfterTurnKey = null; changed = true; }
                        }
                        else
                        {
                            var since = disconnectedSince.TryGetValue(seat.SeatId, out var disconnected) ? disconnected : lastSeen.TryGetValue(seat.ConnectionId ?? "", out var seen) ? seen : owner.options.Clock();
                            if (owner.options.Clock() - since >= owner.options.ConnectionTimeout) { PreserveBeforeChange(); state.Seats.Remove(seat); changed = true; }
                        }
                    }
                    if (game == null && state.Seats.Count == 0) { PreserveBeforeChange(); state.Closed = true; state.ManagerSeatId = null; changed = true; }
                    if (game == null && changed) { state.ManagerSeatId = state.Seats.OrderByDescending(Connected).Select(s => s.SeatId).FirstOrDefault(); ResetReady(); }
                    if (state.Seats.Any(CanRestoreHumanControl)) { PreserveBeforeChange(); changed |= RestoreHumanControlAtTurnBoundary(); }
                    if (changed) { state.Revision++; if (!Commit(prior)) return false; }
                    if (state.Closed) return false;
                }
                var manager = state.Seats.SingleOrDefault(s => s.SeatId == state.ManagerSeatId);
                if (!paused && manager != null && !Connected(manager))
                {
                    var next = state.Seats.FirstOrDefault(s => Connected(s));
                    if (next != null) { var prior = PrivateJson.Copy(state); state.ManagerSeatId = next.SeatId; state.Revision++; Commit(prior); }
                }
                if (paused || game == null || game.Finished) return false;
                if (game.CommandCount >= owner.options.MaximumCommittedCommands) return false;
                var actor = game.Actor(); if (!state.Settings.AiSeats.Contains(actor) && !state.Seats.Any(s => s.SeatId == actor && s.ControlledByAi)) return false;
                var command = game.Decide(actor); command.GetType().GetProperty("Id").SetValue(command, "ai-" + Guid.NewGuid().ToString("N"));
                var before = PrivateJson.Copy(state); var result = game.Execute(command, false);
                if (!result.Success) { paused = true; return false; }
                state.Revision++; state.GameSave = game.Save(); RestoreHumanControlAtTurnBoundary(); return Commit(before);
            }
        }
    }
}
