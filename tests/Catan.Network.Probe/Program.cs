using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Catan.AI;
using Catan.Network.Contracts;
using M2 = Catan.Core.M2;
using M3 = Catan.Core.M3;
using M4 = Catan.Core.M4;
using M5 = Catan.Core.M5;

// 独立网络探针：所有测试玩家只能从实际 HTTPS/WSS 响应取得 PlayerView。
// 凭据及故障恢复材料只写到调用方指定的 .local 私有验证目录，不进入公开结果。
internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, IncludeFields = true };
    private static string Option(string[] args, string name, string fallback = "") { int n = Array.IndexOf(args, name); return n >= 0 && n + 1 < args.Length ? args[n + 1] : fallback; }
    internal static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    public static async Task<int> Main(string[] args)
    {
        try
        {
            string mode = Option(args, "--mode", "smoke"), directory = Path.GetFullPath(Option(args, "--output", ".local/m7-v001/network"));
            Directory.CreateDirectory(directory);
            if (mode == "worker") { await Worker(Option(args, "--config")); return 0; }
            string endpoint = Option(args, "--endpoint", "https://127.0.0.1:47781"), pin = File.ReadAllText(Option(args, "--fingerprint")).Trim().Replace(":", "").Replace("-", "");
            if (mode is "smoke" or "prepare") await Smoke(endpoint, pin, directory);
            else if (mode == "resume") await Resume(endpoint, pin, directory);
            else if (mode == "matrix") await Matrix(endpoint, pin, directory, args.Contains("--all"));
            else if (mode == "process-game") await ProcessGame(endpoint, pin, directory, Option(args, "--rules", "base"), Option(args, "--scenario", "heading-for-new-shores"), int.Parse(Option(args, "--seats", "3")));
            else throw new ArgumentException("未知探针模式。");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("M7_PROBE_FAILED: " + ex.GetType().Name + " / " + ex.Message); if (ex is HttpRequestException) Console.Error.WriteLine("TRANSPORT_DETAIL: " + ex.InnerException?.Message + " / " + ex.InnerException?.InnerException?.Message); return 1; }
    }
    private static async Task<(Peer[] peers, NetworkResponse start)> CreateGame(string endpoint, string pin, string rules, string scenario, int seats, string[]? ai = null)
    {
        var peers = Enumerable.Range(1, seats).Where(i => !(ai ?? []).Contains("P" + i)).Select(_ => new Peer(endpoint, pin)).ToArray();
        var created = await peers[0].Call("Create", new CreateRoomPayload { DisplayName = "网络验证席位", Settings = new RoomSettings { Name = "M7自动验证", RuleSet = rules, ScenarioId = scenario, PlayerCount = seats, AiSeats = ai ?? [] } });
        Check(created, "创建房间");
        for (int i = 1; i < peers.Length; i++) { peers[i].RoomId = created.RoomId; peers[i].RuleSet = created.RuleSet; peers[i].RulesVersion = created.Room.RulesVersion; peers[i].ScenarioVersion = created.Room.ScenarioVersion; Check(await peers[i].Call("Join", new JoinRoomPayload { DisplayName = "网络验证席位", SeatId = "P" + (i + 1) }), "加入房间"); }
        foreach (var p in peers) Check(await p.Call("Ready", new ReadyPayload { Ready = true }), "准备");
        var start = await peers[0].Call("Start"); Check(start, "开始");
        return (peers, start);
    }
    private static async Task Smoke(string endpoint, string pin, string directory)
    {
        using var unauthenticated = new Peer(endpoint, pin);
        var wrongProtocol = await unauthenticated.Send(new NetworkRequest { Operation = "List", ProtocolVersion = -1 });
        Require(!wrongProtocol.Success, "错误协议被接受");
        using (var wrongPin = new Peer(endpoint, new string('0', 64)))
        {
            bool refused = false; try { await wrongPin.Call("List"); } catch (HttpRequestException) { refused = true; }
            Require(refused, "证书指纹不符仍能建立连接");
        }
        var (peers, start) = await CreateGame(endpoint, pin, "base", "base", 3);
        using var p1 = peers[0]; using var p2 = peers[1]; using var p3 = peers[2];
        dynamic view = ReadView("base", start.ViewJson);
        var actor = peers.Single(p => p.SeatId == Actor(view, "base"));
        var latest = await actor.Call("Get");
        dynamic command = Decide("base", ReadView("base", latest.ViewJson)); command.Id = Guid.NewGuid().ToString("N");
        var request = actor.Request("Command", WireJson.SerializeObject(command)); request.CommandId = command.Id; request.ExpectedRevision = latest.Revision;
        var accepted = await actor.Send(request); Check(accepted, "提交首个建筑");
        var repeat = await actor.Send(request); Require(repeat.Success && repeat.IsDuplicate && repeat.Revision == accepted.Revision, "重复命令再次执行或丢失回执");
        dynamic changed = Decide("base", ReadView("base", latest.ViewJson)); changed.Id = command.Id; changed.TargetId = "not-a-vertex";
        var conflict = actor.Request("Command", WireJson.SerializeObject(changed)); conflict.CommandId = request.CommandId; conflict.ExpectedRevision = accepted.Revision;
        Require(!(await actor.Send(conflict)).Success, "同ID不同内容被接受");
        var other = peers.First(p => p != actor);
        var forgery = other.Request("Command", request.Payload); forgery.CommandId = Guid.NewGuid().ToString("N"); forgery.ExpectedRevision = accepted.Revision;
        Require(!(await other.Send(forgery)).Success, "伪造席位被接受");
        var after = await actor.Call("Get"); Require(after.Revision == accepted.Revision && after.ViewJson == accepted.ViewJson, "非法指令改变局面");
        var history = await actor.Call("History", new HistoryPayload { AfterSequence = 0, Limit = 1 }); Check(history, "历史分页");
        InspectPrivacy(after.ViewJson); InspectPrivacy(history.HistoryJson);
        using (var socket = new ClientWebSocket())
        {
            socket.Options.RemoteCertificateValidationCallback = (_, cert, _, _) => cert != null && Convert.ToHexString(SHA256.HashData(cert.GetRawCertData())).Equals(pin, StringComparison.OrdinalIgnoreCase);
            await socket.ConnectAsync(new Uri(endpoint.Replace("https://", "wss://") + "/ws"), CancellationToken.None);
            var bytes = Encoding.UTF8.GetBytes(WireJson.Serialize(actor.Request("Get")));
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            using var data = new MemoryStream(); var buffer = new byte[16384]; WebSocketReceiveResult result;
            do { result = await socket.ReceiveAsync(buffer, CancellationToken.None); data.Write(buffer, 0, result.Count); } while (!result.EndOfMessage);
            Check(WireJson.Deserialize<NetworkResponse>(Encoding.UTF8.GetString(data.ToArray())), "WSS读取本人视图");
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        }
        var durable = await actor.Call("Get"); Check(durable, "WSS断开后同步");
        File.WriteAllText(Path.Combine(directory, "private-resume.json"), JsonSerializer.Serialize(new ResumeState { Endpoint = endpoint, Pin = pin, Peer = actor.State(), Request = request, Revision = durable.Revision, View = durable.ViewJson }, JsonOptions));
        WriteResult(directory, "smoke", new { passed = true, checks = 10, protocolRejection = true, certificatePinMismatch = true, separateCredentials = 3, duplicate = true, commandCollision = true, seatForgery = true, unchangedInvalidState = true, history = true, https = true, wss = true });
    }
    private static async Task Resume(string endpoint, string pin, string directory)
    {
        var saved = JsonSerializer.Deserialize<ResumeState>(File.ReadAllText(Path.Combine(directory, "private-resume.json")), JsonOptions)!;
        using var peer = new Peer(endpoint, pin, saved.Peer);
        var resumed = await peer.Call("Reconnect"); Check(resumed, "重启后原席位恢复");
        // 心跳过期可在退出前转交管理权并增加房间修订号，但不能改变规则局面。
        Require(resumed.Revision >= saved.Revision && resumed.ViewJson == saved.View, "服务重启改变已提交局面");
        saved.Request.ConnectionId = peer.ConnectionId; saved.Request.Credential = peer.Credential;
        var retry = await peer.Send(saved.Request);
        Require(retry.Success, "重启后命令重试失败：" + retry.Code);
        Require(retry.IsDuplicate, "重启后命令未命中持久去重账本");
        Require(retry.CommittedRevision == saved.Request.ExpectedRevision + 1, "重启后原命令提交修订号改变");
        // 重连到重试之间，后台可把离线管理员交给该在线席位；房间修订号会增加，规则局面不能改变。
        Require(retry.Revision >= resumed.Revision && retry.ViewJson == resumed.ViewJson, "重启后重复命令改变规则局面或修订号倒退");
        var old = new NetworkRequest { Operation = "Get", RoomId = peer.RoomId, Credential = peer.Credential, ConnectionId = saved.Peer.ConnectionId };
        Require(!(await peer.Send(old)).Success, "旧连接未被取代");
        WriteResult(directory, "restart", new { passed = true, restoredSeat = resumed.SeatId, exactView = true, durableLedger = true, staleConnectionRejected = true });
    }
    private static async Task Matrix(string endpoint, string pin, string directory, bool all)
    {
        var configs = new List<(string rules, string scenario, int seats)>();
        foreach (int seats in all ? new[] { 3, 4 } : new[] { 3 })
        {
            configs.Add(("base", "base", seats)); configs.Add(("cities-knights", "base", seats));
            foreach (string scenario in all ? M3.SeafarersScenarios.Ids : new[] { "heading-for-new-shores" }) configs.Add(("seafarers", scenario, seats));
            foreach (string scenario in all ? new[] { "heading-for-new-shores", "through-the-desert" } : new[] { "through-the-desert" }) configs.Add(("combined", scenario, seats));
        }
        var results = new List<object>();
        foreach (var config in configs)
        {
            var watch = Stopwatch.StartNew(); var (peers, first) = await CreateGame(endpoint, pin, config.rules, config.scenario, config.seats);
            try
            {
                int count = 0, retries = 0; var decisions = new HashSet<string>(); var latencies = new List<double>(); var current = first;
                while (!current.Room.Finished && count < 10000)
                {
                    dynamic publicView = ReadView(config.rules, current.ViewJson); string seat = Actor(publicView, config.rules);
                    var peer = peers.Single(p => p.SeatId == seat); current = await peer.Call("Get"); Check(current, "取得行动席位视图");
                    dynamic view = ReadView(config.rules, current.ViewJson); decisions.Add(view.Phase.ToString() + "/" + (view.PendingDecision?.Kind ?? ""));
                    dynamic command = Decide(config.rules, view); command.Id = Guid.NewGuid().ToString("N");
                    var request = peer.Request("Command", WireJson.SerializeObject(command)); request.CommandId = command.Id; request.ExpectedRevision = current.Revision;
                    var latency = Stopwatch.StartNew(); current = await peer.Send(request); latencies.Add(latency.Elapsed.TotalMilliseconds); Check(current, "网络对局操作"); count++;
                    if (count % 73 == 0) { var again = await peer.Send(request); Require(again.Success && again.IsDuplicate && again.Revision == current.Revision, "整局重试改变修订号"); retries++; }
                    if (count % 100 == 0) Console.WriteLine($"M7_GAME_PROGRESS {config.rules}/{config.scenario}/{config.seats} commands={count}");
                }
                Require(current.Room.Finished, "网络整局未达到胜利");
                foreach (var p in peers) { var response = await p.Call("Get"); Check(response, "终局同步"); InspectPrivacy(response.ViewJson); Require(response.Revision == current.Revision && response.Room.Finished, "终局不同步"); }
                var ordered = latencies.Order().ToArray();
                var result = new { rules = config.rules, scenario = config.scenario, seats = config.seats, commands = count, retries, seconds = watch.Elapsed.TotalSeconds, p95CommandMs = ordered[(int)((ordered.Length - 1) * .95)], pending = decisions.Order().ToArray(), passed = true };
                results.Add(result); WriteResult(directory, "matrix", new { passed = true, completed = results.Count, requested = configs.Count, games = results });
                Console.WriteLine($"M7_GAME_OK {config.rules}/{config.scenario}/{config.seats} commands={count} seconds={watch.Elapsed.TotalSeconds:F1}");
            }
            finally { foreach (var p in peers) p.Dispose(); }
        }
    }
    private static async Task ProcessGame(string endpoint, string pin, string directory, string rules, string scenario, int seats)
    {
        var (peers, _) = await CreateGame(endpoint, pin, rules, scenario, seats); var processes = new List<Process>();
        string session = Guid.NewGuid().ToString("N"); var watch = Stopwatch.StartNew();
        try
        {
            foreach (var p in peers)
            {
                string config = Path.Combine(directory, "private-worker-" + session + "-" + p.SeatId + ".json");
                File.WriteAllText(config, JsonSerializer.Serialize(new WorkerConfig { Endpoint = endpoint, Pin = pin, Peer = p.State(), Rules = rules, Output = Path.Combine(directory, "worker-" + session + "-" + p.SeatId + ".json") }, JsonOptions));
                var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
                if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase)) start.ArgumentList.Add(typeof(Program).Assembly.Location);
                start.ArgumentList.Add("--mode"); start.ArgumentList.Add("worker"); start.ArgumentList.Add("--config"); start.ArgumentList.Add(config);
                processes.Add(Process.Start(start)!);
            }
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(30));
            await Task.WhenAll(processes.Select(p => p.WaitForExitAsync(timeout.Token)));
            Require(processes.All(p => p.ExitCode == 0), "独立客户端进程失败");
            var done = await peers[0].Call("Get"); Check(done, "独立进程终局"); Require(done.Room.Finished, "独立进程未完成对局");
            WriteResult(directory, "process-" + rules + "-" + seats, new { passed = true, rules, scenario, seats, independentClientProcesses = processes.Count, seconds = watch.Elapsed.TotalSeconds, revision = done.Revision });
        }
        finally { foreach (var p in processes) { if (!p.HasExited) p.Kill(true); p.Dispose(); } foreach (var p in peers) p.Dispose(); }
    }
    private static async Task Worker(string configPath)
    {
        var config = JsonSerializer.Deserialize<WorkerConfig>(File.ReadAllText(configPath), JsonOptions)!;
        using var peer = new Peer(config.Endpoint, config.Pin, config.Peer); int commands = 0, stale = 0; var watch = Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromMinutes(28))
        {
            var response = await peer.Call("Get"); Check(response, "子进程获取本人视图");
            if (response.Room.Finished) { File.WriteAllText(config.Output, JsonSerializer.Serialize(new { passed = true, seat = peer.SeatId, commands, stale, seconds = watch.Elapsed.TotalSeconds }, JsonOptions)); return; }
            dynamic view = ReadView(config.Rules, response.ViewJson);
            if (Actor(view, config.Rules) != peer.SeatId) { await Task.Delay(35); continue; }
            dynamic command = Decide(config.Rules, view); command.Id = Guid.NewGuid().ToString("N");
            var request = peer.Request("Command", WireJson.SerializeObject(command)); request.CommandId = command.Id; request.ExpectedRevision = response.Revision;
            var result = await peer.Send(request);
            if (!result.Success && result.Code.Contains("Revision", StringComparison.OrdinalIgnoreCase)) { stale++; continue; }
            Check(result, "子进程提交操作"); commands++;
        }
        throw new TimeoutException("独立客户端对局超时");
    }
    private static object ReadView(string rules, string json) => rules switch { "base" => WireJson.Deserialize<M2.PlayerView>(json), "seafarers" => WireJson.Deserialize<M3.PlayerView>(json), "cities-knights" => WireJson.Deserialize<M4.PlayerView>(json), _ => WireJson.Deserialize<M5.PlayerView>(json) };
    private static object Decide(string rules, object view) => rules switch { "base" => new BaseGameAi().Decide((M2.PlayerView)view), "seafarers" => new SeafarersAi().Decide((M3.PlayerView)view), "cities-knights" => new CitiesKnightsAi().Decide((M4.PlayerView)view), _ => new CombinedAi().Decide((M5.PlayerView)view) };
    private static string Actor(dynamic view, string rules)
    {
        if (view.TradeOffer != null) return view.TradeOffer.NextResponder(view.PlayerId);
        if (view.Discards.Length > 0) return view.Discards[0].PlayerId;
        if (rules is "seafarers" or "combined" && view.Phase.ToString() == "GoldChoice" && view.GoldClaims.Length > 0) return view.GoldClaims[0].PlayerId;
        return view.PendingDecision != null && !string.IsNullOrEmpty((string?)view.PendingDecision.PlayerId) ? view.PendingDecision.PlayerId : view.ActivePlayerId;
    }
    private static void InspectPrivacy(string json)
    {
        foreach (string secret in new[] { "\"RandomState\"", "\"ProcessedCommands\"", "\"Authority\"", "\"DevelopmentDeck\"", "\"ProgressDecks\"", "\"Credential\"" }) Require(!json.Contains(secret, StringComparison.Ordinal), "网络视图包含禁传字段");
    }
    private static void Check(NetworkResponse response, string action) => Require(response.Success, action + "失败：" + response.Code);
    private static void WriteResult(string directory, string name, object result) { File.WriteAllText(Path.Combine(directory, name + ".json"), JsonSerializer.Serialize(result, JsonOptions)); Console.WriteLine("M7_PROBE_OK " + name); }
    public sealed class PeerState { public string RoomId = "", Credential = "", ConnectionId = "", SeatId = "", RuleSet = "", RulesVersion = "", ScenarioVersion = ""; public long Revision; }
    public sealed class ResumeState { public string Endpoint = "", Pin = "", View = ""; public PeerState Peer = new(); public NetworkRequest Request = new(); public long Revision; }
    public sealed class WorkerConfig { public string Endpoint = "", Pin = "", Rules = "", Output = ""; public PeerState Peer = new(); }
    private sealed class Peer : IDisposable
    {
        private readonly HttpClient client;
        public string RoomId = "", Credential = "", ConnectionId = "", SeatId = "", RuleSet = "", RulesVersion = "", ScenarioVersion = "";
        public long Revision;
        public Peer(string endpoint, string pin, PeerState? state = null)
        {
            var handler = new HttpClientHandler { UseProxy = false, ServerCertificateCustomValidationCallback = (_, cert, _, _) => cert != null && Convert.ToHexString(SHA256.HashData(cert.RawData)).Equals(pin, StringComparison.OrdinalIgnoreCase) };
            client = new HttpClient(handler) { BaseAddress = new Uri(endpoint), Timeout = TimeSpan.FromSeconds(60) };
            if (state != null) { RoomId = state.RoomId; Credential = state.Credential; ConnectionId = state.ConnectionId; SeatId = state.SeatId; RuleSet = state.RuleSet; RulesVersion = state.RulesVersion; ScenarioVersion = state.ScenarioVersion; Revision = state.Revision; }
        }
        public PeerState State() => new() { RoomId = RoomId, Credential = Credential, ConnectionId = ConnectionId, SeatId = SeatId, RuleSet = RuleSet, Revision = Revision, RulesVersion = RulesVersion, ScenarioVersion = ScenarioVersion };
        public NetworkRequest Request(string operation, string? payload = null) => new() { Operation = operation, RequestId = Guid.NewGuid().ToString("N"), RoomId = RoomId, Credential = Credential, ConnectionId = ConnectionId, Payload = payload, RuleSet = RuleSet, ExpectedRevision = Revision, ExpectedRulesVersion = RulesVersion, ExpectedScenarioVersion = ScenarioVersion };
        public async Task<NetworkResponse> Call(string operation, object? payload = null)
        {
            if (operation is "Ready" or "Start" or "Configure" or "Seat" or "Leave" or "Transfer") Check(await Send(Request("Get")), "修改前同步");
            if (payload is CreateRoomPayload creation)
            {
                RuleSet = creation.Settings.RuleSet;
                (RulesVersion, ScenarioVersion) = RuleSet switch { "base" => (M2.BaseGameSession.RulesVersion, M2.BaseGameSession.ScenarioVersion), "seafarers" => (M3.SeafarersGameSession.RulesVersion, M3.SeafarersGameSession.ScenarioVersion), "cities-knights" => (M4.CitiesKnightsGameSession.RulesVersion, M4.CitiesKnightsGameSession.ScenarioVersion), _ => (M5.CombinedGameSession.RulesVersion, M5.CombinedGameSession.ScenarioVersion) };
            }
            return await Send(Request(operation, payload == null ? null : WireJson.SerializeObject(payload)));
        }
        public async Task<NetworkResponse> Send(NetworkRequest request)
        {
            using var response = await client.PostAsync("/api/request", new StringContent(WireJson.Serialize(request), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode(); var data = WireJson.Deserialize<NetworkResponse>(await response.Content.ReadAsStringAsync());
            if (data.Success) { if (!string.IsNullOrEmpty(data.RoomId)) RoomId = data.RoomId; if (!string.IsNullOrEmpty(data.Credential)) Credential = data.Credential; if (!string.IsNullOrEmpty(data.ConnectionId)) ConnectionId = data.ConnectionId; if (!string.IsNullOrEmpty(data.SeatId)) SeatId = data.SeatId; if (!string.IsNullOrEmpty(data.RuleSet)) RuleSet = data.RuleSet; if (data.Room != null) { RulesVersion = data.Room.RulesVersion; ScenarioVersion = data.Room.ScenarioVersion; } Revision = data.Revision; }
            return data;
        }
        public void Dispose() => client.Dispose();
    }
}
