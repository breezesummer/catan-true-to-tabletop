using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core;

// 纯展示模型：仅消费按当前观看者权限投影的历史，绝不访问权威局面或规则随机数。
public enum TabletopAudioCue
{
    UiClick, UiBack, CardPick, ResourceCollect, DiceRoll,
    RoadBuild, SettlementBuild, CityBuild, WallBuild, ShipBuild, ShipMove, PortPlace,
    TradeOffer, TradeComplete, CardDraw, CardPlay, RobberMove, PirateMove,
    KnightRecruit, KnightPromote, KnightActivate, KnightMove, KnightCombat,
    BarbarianAdvance, BarbarianInvasion, BuildingCollapse, FacilityUpgrade, WonderBuild,
    TurnEnd, YourTurn, Victory, Decision, Error, Save, Network
}

public readonly struct TabletopAudioEvent
{
    public readonly TabletopAudioCue Cue;
    public readonly double Delay;
    public TabletopAudioEvent(TabletopAudioCue cue, double delay = 0) { Cue = cue; Delay = delay; }
}

public sealed class TabletopAudioModel
{
    string scope = "";
    bool initialized;
    public long Cursor { get; private set; }

    public void Reset() { initialized = false; scope = ""; Cursor = 0; }

    // 首次绑定、读取存档、换房与重连只建立水位，禁止重放旧操作。
    public TabletopAudioEvent[] Observe(string playerScope, GameHistoryEntry[] entries, long publicBaselineSequence = 0)
    {
        entries = entries ?? Array.Empty<GameHistoryEntry>();
        long latest = entries.Where(e => e != null).Select(e => e.Sequence).DefaultIfEmpty(0).Max();
        if (!initialized || scope != playerScope)
        {
            scope = playerScope; initialized = true; Cursor = Math.Max(latest, publicBaselineSequence);
            return Array.Empty<TabletopAudioEvent>();
        }
        // 历史网络请求可能比 PlayerView 迟到或重试；同一作用域的水位只能前进。
        if (latest < Cursor) return Array.Empty<TabletopAudioEvent>();
        var added = entries.Where(e => e != null && e.Sequence > Cursor).OrderBy(e => e.Sequence).ToArray();
        Cursor = latest;
        // 网络追帧只读最近操作；同一批不积累数十秒的音效队列。
        return added.Skip(Math.Max(0, added.Length - 8)).SelectMany(Classify).ToArray();
    }

    public static TabletopAudioEvent[] Classify(GameHistoryEntry entry)
    {
        if (entry == null) return Array.Empty<TabletopAudioEvent>();
        var result = new List<TabletopAudioEvent>();
        TabletopAudioCue cue;
        var changes = entry.Changes ?? Array.Empty<GameHistoryChange>();
        switch (entry.Kind)
        {
            case "RollDice": cue = TabletopAudioCue.DiceRoll; break;
            case "SetupRoad": case "BuildRoad": case "PlaceFreeRoad": cue = TabletopAudioCue.RoadBuild; break;
            case "SetupSettlement": case "BuildSettlement":
                cue = changes.Any(c => c != null && c.Category == "piece" && c.Amount > 0 && c.Name == "城市") ? TabletopAudioCue.CityBuild : TabletopAudioCue.SettlementBuild; break;
            case "BuildCity": cue = TabletopAudioCue.CityBuild; break;
            case "BuildWall": cue = TabletopAudioCue.WallBuild; break;
            case "SetupShip": case "BuildShip": case "PlaceFreeShip": cue = TabletopAudioCue.ShipBuild; break;
            case "MoveShip": cue = TabletopAudioCue.ShipMove; break;
            case "SetupPort": case "PlacePort": cue = TabletopAudioCue.PortPlace; break;
            case "BankTrade": case "AcceptTrade": case "CommercialHarborOffer": cue = TabletopAudioCue.TradeComplete; break;
            case "ProposeTrade": cue = TabletopAudioCue.TradeOffer; break;
            case "RejectTrade": case "CancelTrade": cue = TabletopAudioCue.UiBack; break;
            case "BuyDevelopmentCard": cue = TabletopAudioCue.CardDraw; break;
            case "MoveRobber": case "StealResource": case "StealCloth": cue = TabletopAudioCue.RobberMove; break;
            case "MovePirate": case "ChooseInvasionRoute": cue = TabletopAudioCue.PirateMove; break;
            case "RecruitKnight": cue = TabletopAudioCue.KnightRecruit; break;
            case "PromoteKnight": cue = TabletopAudioCue.KnightPromote; break;
            case "ActivateKnight": cue = TabletopAudioCue.KnightActivate; break;
            case "MoveKnight": cue = TabletopAudioCue.KnightMove; break;
            case "DisplaceKnight": case "ChaseRobber": case "ChasePirate": case "AttackFortress": case "PlayKnight": cue = TabletopAudioCue.KnightCombat; break;
            case "ImproveCity": cue = TabletopAudioCue.FacilityUpgrade; break;
            case "ClaimWonder": case "BuildWonder": cue = TabletopAudioCue.WonderBuild; break;
            case "EndTurn": cue = TabletopAudioCue.TurnEnd; break;
            case "DeclareVictory": cue = TabletopAudioCue.Victory; break;
            case "DiscardResources": case "DiscardProgressCard": cue = TabletopAudioCue.CardPick; break;
            default: cue = entry.Kind != null && entry.Kind.StartsWith("Play", StringComparison.Ordinal) ? TabletopAudioCue.CardPlay : TabletopAudioCue.Decision; break;
        }
        result.Add(new TabletopAudioEvent(cue));
        // 未知资源和隐藏卡牌一律使用同一种收牌声；不依赖牌名、GoodIndex、数量或持牌人。
        if (changes.Any(c => c != null && c.Amount > 0 && (c.Category == "goods" || c.Category == "resource-count")))
            result.Add(new TabletopAudioEvent(TabletopAudioCue.ResourceCollect, entry.Kind == "RollDice" ? .78 : .13));
        if (cue != TabletopAudioCue.CardDraw && changes.Any(c => c != null && c.Amount > 0 && (c.Category == "development" || c.Category == "progress")))
            result.Add(new TabletopAudioEvent(TabletopAudioCue.CardDraw, .18));
        // 城市升级移除的是村庄，只有公开城市减少且同点变回村庄才表示倒塌。
        bool collapsed = changes.Any(c => c != null && c.Category == "piece" && c.Name == "城市" && c.Amount < 0 &&
            changes.Any(d => d != null && d.Category == "piece" && d.Name == "村庄" && d.Amount > 0 && d.PlayerId == c.PlayerId && d.LocationId == c.LocationId));
        if (collapsed) result.Add(new TabletopAudioEvent(TabletopAudioCue.BuildingCollapse, .10));
        if (entry.Kind == "RollDice" && entry.EventDie == "Barbarian")
            result.Add(new TabletopAudioEvent(TabletopAudioCue.BarbarianAdvance, .62));
        if (entry.Kind == "ResolveChoice" || entry.Kind == "ResolveProgressChoice")
        {
            if (changes.Any(c => c != null && c.Category == "piece" && c.Name != null && c.Name.Contains("骑士")))
                result.Add(new TabletopAudioEvent(TabletopAudioCue.KnightCombat, .10));
            if (changes.Any(c => c != null && c.Category == "piece" && c.Amount > 0 && c.Name != null && c.Name.Contains("大都会")))
                result.Add(new TabletopAudioEvent(TabletopAudioCue.FacilityUpgrade, .10));
        }
        return result.ToArray();
    }
}

// 无 Unity 依赖的限流器，保护高速 AI 和联机补帧时的混音。
public sealed class TabletopAudioLimiter
{
    readonly Dictionary<TabletopAudioCue, double> last = new Dictionary<TabletopAudioCue, double>();
    public bool Admit(TabletopAudioCue cue, double now)
    {
        double gap = cue == TabletopAudioCue.UiClick || cue == TabletopAudioCue.CardPick ? .055 :
            cue == TabletopAudioCue.ResourceCollect || cue == TabletopAudioCue.CardDraw ? .22 :
            cue == TabletopAudioCue.BarbarianInvasion || cue == TabletopAudioCue.BuildingCollapse || cue == TabletopAudioCue.Victory ? .9 : .13;
        if (last.TryGetValue(cue, out double previous) && now >= previous && now - previous < gap) return false;
        last[cue] = now; return true;
    }
    public void Reset() { last.Clear(); }
}

// 公开棋盘快照补充自动入侵等没有独立玩家命令的展示事件。
public sealed class TabletopAudioPublicModel
{
    public bool Initialized { get; private set; }
    bool wasHuman, wasFinished;
    int previousBarbarian = -1;
    string previousPirate = "";
    public void Reset() { Initialized = false; previousBarbarian = -1; previousPirate = ""; }
    public TabletopAudioEvent[] Observe(bool human, bool finished, int barbarian, string pirate, bool pirateIslands, int newBarbarianAdvances = 0)
    {
        var result = new List<TabletopAudioEvent>();
        if (Initialized)
        {
            if (human && !wasHuman && !finished) result.Add(new TabletopAudioEvent(TabletopAudioCue.YourTurn, .12));
            if (finished && !wasFinished) result.Add(new TabletopAudioEvent(TabletopAudioCue.Victory, .18));
            if (previousBarbarian >= 0 && (barbarian == 7 && previousBarbarian < 7 || barbarian == 0 && (previousBarbarian == 6 || previousBarbarian + newBarbarianAdvances >= 7)))
                result.Add(new TabletopAudioEvent(TabletopAudioCue.BarbarianInvasion, .67));
            if (pirateIslands && !string.IsNullOrEmpty(previousPirate) && !string.IsNullOrEmpty(pirate) && pirate != previousPirate)
                result.Add(new TabletopAudioEvent(TabletopAudioCue.PirateMove, .64));
        }
        previousBarbarian = barbarian; previousPirate = pirate ?? ""; wasHuman = human; wasFinished = finished; Initialized = true;
        return result.ToArray();
    }
}
