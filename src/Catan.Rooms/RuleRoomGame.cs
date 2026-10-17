using Catan.AI;
using Catan.Core;
using Catan.Network.Contracts;
using M2 = Catan.Core.M2;
using M3 = Catan.Core.M3;
using M4 = Catan.Core.M4;
using M5 = Catan.Core.M5;

namespace Catan.Rooms;

/// <summary>四规则薄适配器；命令显式选择类型，AI 只接收其本人的 PlayerView。</summary>
internal sealed class RuleRoomGame
{
    private readonly object session;
    private readonly string ruleSet;
    private bool finished;
    internal int CommandCount { get; private set; }
    internal string ActivePlayerId { get; private set; }
    // 摆放轮也有独立边界；蛇形折返时同一颜色可连续摆放两轮。
    internal string TurnKey { get; private set; }
    private RuleRoomGame(string rules, object session) { ruleSet = rules; this.session = session; Save(); }
    internal static (string Rules, string Scenario) Versions(string rules) => rules switch
    {
        "base" => (M2.BaseGameSession.RulesVersion, M2.BaseGameSession.ScenarioVersion),
        "seafarers" => (M3.SeafarersGameSession.RulesVersion, M3.SeafarersGameSession.ScenarioVersion),
        "cities-knights" => (M4.CitiesKnightsGameSession.RulesVersion, M4.CitiesKnightsGameSession.ScenarioVersion),
        "combined" => (M5.CombinedGameSession.RulesVersion, M5.CombinedGameSession.ScenarioVersion),
        _ => throw new ArgumentException("未知规则包。")
    };
    internal static RuleRoomGame Create(RoomSettings settings, string baseJson)
    {
        var seed = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(System.Security.Cryptography.RandomNumberGenerator.GetBytes(4));
        return new(settings.RuleSet, settings.RuleSet switch
        {
            "base" => M2.BaseGameSession.Create(baseJson, settings.PlayerCount, seed, settings.TargetVictoryPoints),
            "seafarers" => M3.SeafarersGameSession.Create(settings.ScenarioId, settings.PlayerCount, seed, settings.TargetVictoryPoints),
            "cities-knights" => M4.CitiesKnightsGameSession.Create(baseJson, settings.PlayerCount, seed, settings.TargetVictoryPoints),
            "combined" => M5.CombinedGameSession.Create(settings.ScenarioId, settings.PlayerCount, seed, settings.TargetVictoryPoints),
            _ => throw new ArgumentException("未知规则包。")
        });
    }
    internal static RuleRoomGame Load(string rules, string save, string baseJson) => new(rules, rules switch
    {
        "base" => M2.BaseGameSession.Load(baseJson, save), "seafarers" => M3.SeafarersGameSession.Load(save),
        "cities-knights" => M4.CitiesKnightsGameSession.Load(baseJson, save), "combined" => M5.CombinedGameSession.Load(save),
        _ => throw new ArgumentException("未知规则包。")
    });
    internal string Save()
    {
        string json = session switch { M2.BaseGameSession s => s.Save(), M3.SeafarersGameSession s => s.Save(), M4.CitiesKnightsGameSession s => s.Save(), M5.CombinedGameSession s => s.Save(), _ => throw new InvalidOperationException() };
        using var document = System.Text.Json.JsonDocument.Parse(json);
        finished = document.RootElement.TryGetProperty("WinnerPlayerId", out var winner) && winner.ValueKind == System.Text.Json.JsonValueKind.String && !string.IsNullOrEmpty(winner.GetString());
        CommandCount = document.RootElement.GetProperty("ProcessedCommands").GetArrayLength();
        ActivePlayerId = document.RootElement.GetProperty("ActivePlayerId").GetString();
        TurnKey = document.RootElement.GetProperty("Turn").GetInt32() + ":" + document.RootElement.GetProperty("SetupPlacementIndex").GetInt32() + ":" + ActivePlayerId;
        return json;
    }
    internal object View(string seat) => session switch { M2.BaseGameSession s => s.GetPlayerView(seat), M3.SeafarersGameSession s => s.GetPlayerView(seat), M4.CitiesKnightsGameSession s => s.GetPlayerView(seat), M5.CombinedGameSession s => s.GetPlayerView(seat), _ => throw new InvalidOperationException() };
    internal string ViewJson(string seat)
    {
        // 每次 GetPlayerView 返回脱离权威状态的新对象；只截断网络快照，AI 仍取得完整历史事件。
        var view = View(seat);
        var eventsProperty = view.GetType().GetProperty("Events");
        var events = (Array)eventsProperty.GetValue(view);
        // 保留最近展示事件，以及当前回合边界和各玩家最近一次报价；远程当前 AI 仍能判断本回合是否报价。
        // 最多 32 + 1 + 4 条真实公开事件，不携带完整无限增长日志。
        var elementType = events.GetType().GetElementType();
        var kindProperty = elementType.GetProperty("Kind"); var playerProperty = elementType.GetProperty("PlayerId"); var sequenceProperty = elementType.GetProperty("Sequence");
        var objects = events.Cast<object>().ToArray();
        var boundary = objects.LastOrDefault(e => (string)kindProperty.GetValue(e) == "EndTurn");
        long since = boundary == null ? 0 : (long)sequenceProperty.GetValue(boundary);
        var retained = objects.Skip(Math.Max(0, objects.Length - 32)).Concat(boundary == null ? Array.Empty<object>() : new[] { boundary })
            .Concat(objects.Where(e => (long)sequenceProperty.GetValue(e) > since && (string)kindProperty.GetValue(e) == "ProposeTrade").GroupBy(e => (string)playerProperty.GetValue(e)).Select(g => g.Last()))
            .Distinct().OrderBy(e => (long)sequenceProperty.GetValue(e)).ToArray();
        var truncated = Array.CreateInstance(elementType, retained.Length);
        for (int i = 0; i < retained.Length; i++) truncated.SetValue(retained[i], i);
        eventsProperty.SetValue(view, truncated);
        return WireJson.SerializeObject(view);
    }
    internal GameHistoryEntry[] History(string seat, long after) => session switch { M2.BaseGameSession s => s.GetHistory(seat, after), M3.SeafarersGameSession s => s.GetHistory(seat, after), M4.CitiesKnightsGameSession s => s.GetHistory(seat, after), M5.CombinedGameSession s => s.GetHistory(seat, after), _ => throw new InvalidOperationException() };
    internal object ParseCommand(string json, string seat, string commandId)
    {
        object command = ruleSet switch { "base" => WireJson.Deserialize<M2.Command>(json), "seafarers" => WireJson.Deserialize<M3.Command>(json), "cities-knights" => WireJson.Deserialize<M4.Command>(json), "combined" => WireJson.Deserialize<M5.Command>(json), _ => throw new InvalidOperationException() };
        var type = command.GetType();
        var claimed = (string)type.GetProperty("PlayerId").GetValue(command);
        if (!string.IsNullOrEmpty(claimed) && claimed != seat) throw new SeatForgeryException();
        type.GetProperty("PlayerId").SetValue(command, seat);
        type.GetProperty("Id").SetValue(command, commandId);
        return command;
    }
    internal (bool Success, string Code, string Message) Execute(object command, bool preview)
    {
        dynamic result = session switch
        {
            M2.BaseGameSession s => preview ? s.Preview((M2.Command)command) : s.Execute((M2.Command)command),
            M3.SeafarersGameSession s => preview ? s.Preview((M3.Command)command) : s.Execute((M3.Command)command),
            M4.CitiesKnightsGameSession s => preview ? s.Preview((M4.Command)command) : s.Execute((M4.Command)command),
            M5.CombinedGameSession s => preview ? s.Preview((M5.Command)command) : s.Execute((M5.Command)command),
            _ => throw new InvalidOperationException()
        };
        return ((bool)result.Success, (string)result.ErrorCode, (string)result.Message);
    }
    internal bool Finished => finished;
    internal object Decide(string seat) => View(seat) switch
    {
        M2.PlayerView v => new BaseGameAi().Decide(v), M3.PlayerView v => new SeafarersAi().Decide(v),
        M4.PlayerView v => new CitiesKnightsAi().Decide(v), M5.PlayerView v => new CombinedAi().Decide(v),
        _ => throw new InvalidOperationException()
    };
    internal string Actor()
    {
        dynamic view = View("P1");
        if (view.TradeOffer != null) return view.TradeOffer.NextResponder();
        if (view.Phase.ToString() == "Discard") return view.Discards[0].PlayerId;
        if (view.Phase.ToString() == "GoldChoice") return view.GoldClaims[0].PlayerId;
        return view.PendingDecision?.PlayerId ?? view.ActivePlayerId;
    }
}
internal sealed class SeatForgeryException : Exception { }
