using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Catan.AI;
using M2 = Catan.Core.M2;
using M3 = Catan.Core.M3;
using M5 = Catan.Core.M5;

// 离线夹具生成器。AI 只取得专用投影；唯一变更入口是 Session.Execute。
// 权威存档只能输出到 .local；可提交清单仅含人类 BoardView 统计、摘要与合成夹具文件校验值。
if (args.Length != 2) throw new ArgumentException("用法：UltraBaselineFixtures <仓库目录> <.local 下的夹具目录>");
string repo = Path.GetFullPath(args[0]), directory = Path.GetFullPath(args[1]);
string allowed = Path.Combine(repo, ".local") + Path.DirectorySeparatorChar;
if (!directory.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("权威夹具必须保留在本仓库 .local 内。");
Directory.CreateDirectory(directory);
string topology = File.ReadAllText(Path.Combine(repo, "docs/acceptance/v0.1/scenario.json"));
var specs = new[]
{
    new Spec("ordinary", "base", "heading-for-new-shores", 97, false),
    new Spec("maximum", "seafarers", "wonders-of-catan", 97, false),
    new Spec("crowded", "combined", "through-the-desert", 2027, true),
    new Spec("fog", "seafarers", "fog-islands", 97, false)
};
var reports = new List<object>();
foreach (var spec in specs)
{
    var first = Generate(spec, topology);
    var second = Generate(spec, topology);
    Require(first.Save == second.Save && first.Commands == second.Commands, "同种子合法重放不一致。");
    var restored = Game.Load(spec.Rules, first.Save, topology);
    Require(restored.Save() == first.Save, "权威恢复未保持完全一致。");
    restored.Invariants();
    using var publicBoard = JsonDocument.Parse(JsonSerializer.Serialize((object)restored.View().Board));
    int fog = publicBoard.RootElement.GetProperty("Tiles").EnumerateArray().Count(t => t.GetProperty("Resource").GetString() == "fog");
    Require(!spec.Crowded || first.Pieces >= 48, "拥挤夹具未达到至少 48 个公开组件。");
    Require(spec.Id != "fog" || fog > 0, "迷雾夹具没有保留未知地块。");
    Require(spec.Id != "maximum" || Count(publicBoard.RootElement, "Tiles") == 49, "最大地图夹具不再是 49 格，请重新审核支持范围。");
    string envelope = JsonSerializer.Serialize(new
    {
        Version = 1, RuleSet = spec.Rules, ScenarioId = spec.Scenario, HumanSeat = "P1",
        Authority = first.Save, PlayerCount = 4, Paused = true, AiSpeedLevel = 1,
        HasAiSpeed = true, PlayerNames = new[] { "基线席位一", "基线席位二", "基线席位三", "基线席位四" }
    });
    byte[] envelopeBytes = Encoding.UTF8.GetBytes(envelope);
    File.WriteAllBytes(Path.Combine(directory, spec.Id + ".json"), envelopeBytes);
    reports.Add(new
    {
        id = spec.Id, rules = spec.Rules, scenario = spec.Scenario,
        fileSha256 = Convert.ToHexString(SHA256.HashData(envelopeBytes)).ToLowerInvariant(),
        mapId = spec.Rules == "base" ? "standard" : spec.Scenario, players = 4, seed = spec.Seed,
        policy = spec.Crowded ? "合法完整局中公开组件最多的无待决非终局；同数取首次" : "合法开局摆放结束后的首个稳定阶段",
        acceptedCommands = first.Commands, legalCommandsChecked = first.TotalCommands,
        deterministicReplay = true, exactSaveRoundtrip = true, authorityCommitted = false,
        turn = (int)restored.View().Turn, phase = restored.View().Phase.ToString(),
        tiles = Count(publicBoard.RootElement, "Tiles"), vertices = Count(publicBoard.RootElement, "Vertices"),
        edges = Count(publicBoard.RootElement, "Edges"), fogTiles = fog,
        settlements = Count(publicBoard.RootElement, "Settlements"), cities = Count(publicBoard.RootElement, "Cities"),
        roads = Count(publicBoard.RootElement, "Roads"), ships = Count(publicBoard.RootElement, "Ships"),
        knights = Count(publicBoard.RootElement, "Knights"), primaryPieces = first.Pieces,
        primaryPiecesDefinition = "村庄、城市、道路、船和骑士；不含附着的城墙及大都会",
        walls = Count(publicBoard.RootElement, "Walls"), metropolises = Count(publicBoard.RootElement, "Metropolises"),
        humanBoardSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(publicBoard.RootElement.GetRawText()))).ToLowerInvariant()
    });
    Console.WriteLine($"{spec.Id}：{first.Commands} 条合法指令，{first.Pieces} 个公开组件，迷雾 {fog} 格；确定性重放与恢复通过。");
}
File.WriteAllText(Path.Combine(directory, "public-manifest.json"), JsonSerializer.Serialize(new
{
    schema = "ultra-baseline-fixtures-v1", date = "2026-10-21",
    privacy = "仅人类公开棋盘统计及人工合成基线夹具的 SHA256；不输出权威内容、牌库、隐藏地图或随机状态；不用于真实玩家存档",
    scenarios = reports
}, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

static Result Generate(Spec spec, string topology)
{
    var game = Game.Create(spec, topology);
    string? selected = null;
    int selectedCommands = 0, selectedPieces = -1, commands = 0;
    while (commands < 10000)
    {
        dynamic view = game.View();
        string phase = view.Phase.ToString();
        if (phase == "Finished") break;
        bool stable = (phase == "Action" || phase == "ProductionAwaitRoll" || phase == "AwaitRoll")
            && view.PendingDecision == null && view.TradeOffer == null;
        if (stable)
        {
            using var board = JsonDocument.Parse(JsonSerializer.Serialize((object)view.Board));
            int pieces = new[] { "Settlements", "Cities", "Roads", "Ships", "Knights" }.Sum(name => Count(board.RootElement, name));
            if (pieces > selectedPieces)
            { selected = game.Save(); selectedCommands = commands; selectedPieces = pieces; }
            if (!spec.Crowded) break;
        }
        string actor = Game.Actor(view);
        dynamic command = game.Decide(game.AiView(actor));
        command.Id = "ultra-u0-" + spec.Id + "-" + commands;
        dynamic result = game.Execute(command);
        Require((bool)result.Success, "夹具合法指令被拒绝：" + spec.Id + "/" + commands);
        game.Invariants();
        commands++;
    }
    Require(selected != null && commands < 10000, "夹具未找到稳定局面：" + spec.Id);
    return new Result(selected!, selectedCommands, selectedPieces, commands);
}
static int Count(JsonElement element, string name) => element.TryGetProperty(name, out var values) ? values.GetArrayLength() : 0;
static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
internal sealed record Spec(string Id, string Rules, string Scenario, uint Seed, bool Crowded);
internal sealed record Result(string Save, int Commands, int Pieces, int TotalCommands);
internal sealed class Game(string rules, object session)
{
    internal dynamic View() => ((dynamic)session).GetPlayerView("P1");
    internal dynamic AiView(string seat) => ((dynamic)session).GetAiPlayerView(seat);
    internal string Save() => ((dynamic)session).Save();
    internal void Invariants() => ((dynamic)session).AssertInvariants();
    internal dynamic Execute(object command) => ((dynamic)session).Execute((dynamic)command);
    internal dynamic Decide(object view) => rules switch
    {
        "base" => new BaseGameAi().Decide((M2.PlayerView)view),
        "seafarers" => new SeafarersAi().Decide((M3.PlayerView)view),
        "combined" => new CombinedAi().Decide((M5.PlayerView)view),
        _ => throw new ArgumentException(rules)
    };
    internal static string Actor(dynamic view)
    {
        if (view.TradeOffer != null) return view.TradeOffer.NextResponder(view.PlayerId);
        if (view.Phase.ToString() == "Discard") return view.Discards[0].PlayerId;
        if (view.Phase.ToString() == "GoldChoice") return view.GoldClaims[0].PlayerId;
        return view.PendingDecision?.PlayerId ?? view.ActivePlayerId;
    }
    internal static Game Create(Spec spec, string topology) => new(spec.Rules, spec.Rules switch
    {
        "base" => M2.BaseGameSession.Create(topology, 4, spec.Seed),
        "seafarers" => M3.SeafarersGameSession.Create(spec.Scenario, 4, spec.Seed),
        "combined" => M5.CombinedGameSession.Create(spec.Scenario, 4, spec.Seed),
        _ => throw new ArgumentException(spec.Rules)
    });
    internal static Game Load(string rules, string save, string topology) => new(rules, rules switch
    {
        "base" => M2.BaseGameSession.Load(topology, save),
        "seafarers" => M3.SeafarersGameSession.Load(save),
        "combined" => M5.CombinedGameSession.Load(save),
        _ => throw new ArgumentException(rules)
    });
}
