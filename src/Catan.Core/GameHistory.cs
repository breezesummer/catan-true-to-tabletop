using System;
using System.Collections.Generic;
using System.Linq;

namespace Catan.Core
{
    /// <summary>已按观看者权限投影的历史。只记录变化，不携带完整手牌、牌库或随机状态。</summary>
    public sealed class GameHistoryEntry
    {
        public long Sequence { get; internal set; }
        public int Turn { get; internal set; }
        public string PlayerId { get; internal set; }
        public string OtherPlayerId { get; internal set; }
        public string Kind { get; internal set; }
        public string Action { get; internal set; }
        public string TargetId { get; internal set; }
        public int Dice1 { get; internal set; }
        public int Dice2 { get; internal set; }
        // 城市与骑士的事件骰使用 Barbarian / Trade / Politics / Science；基础版为空。
        public string EventDie { get; internal set; }
        public GameHistoryChange[] Changes { get; internal set; } = Array.Empty<GameHistoryChange>();
        public bool Involves(string playerId) => PlayerId == playerId || OtherPlayerId == playerId || Changes.Any(c => c.PlayerId == playerId);
        internal GameHistoryEntry Copy() => (GameHistoryEntry)MemberwiseClone();
    }

    public sealed class GameHistoryChange
    {
        public string PlayerId { get; internal set; }
        // goods / development / progress / piece；未知资源只有数量，GoodIndex 为 -1。
        public string Category { get; internal set; }
        public int GoodIndex { get; internal set; } = -1;
        public int Amount { get; internal set; }
        public string Name { get; internal set; }
        public string LocationId { get; internal set; }
        public bool IsPrivate { get; internal set; }
        internal GameHistoryChange Copy() => (GameHistoryChange)MemberwiseClone();
    }

    // 历史从权威存档已有的已确认命令确定性重建；无需更改存档格式，也不信任外部日志。
    internal sealed class GameHistoryJournal
    {
        internal sealed class Group
        {
            internal GameHistoryChange[] Details;
            internal string[] Viewers;
        }
        private sealed class Record
        {
            internal GameHistoryEntry Entry;
            internal Group[] Groups;
        }
        private readonly List<Record> records = new List<Record>();
        internal GameHistoryEntry[] Project(string viewer, long afterSequence)
        {
            return records.Where(r => r.Entry.Sequence > afterSequence).Select(r =>
            {
                var entry = r.Entry.Copy();
                entry.Changes = r.Groups.SelectMany(g =>
                {
                    if (g.Viewers == null || g.Viewers.Contains(viewer)) return g.Details.Select(d => d.Copy());
                    // 正负变化分开汇总，不能用净值掩盖一次收到/失去的隐藏卡牌。
                    return g.Details.GroupBy(d => Math.Sign(d.Amount)).Select(x => new GameHistoryChange
                    {
                        PlayerId = g.Details[0].PlayerId, Category = g.Details[0].Category == "goods" ? "resource-count" : g.Details[0].Category, Amount = x.Sum(d => d.Amount),
                        Name = g.Details[0].Category == "goods" ? "手牌" : g.Details[0].Category == "development" ? "发展卡" : "发展卡"
                    });
                }).ToArray();
                return entry;
            }).ToArray();
        }
        internal void RecordAction(HistorySnapshot before, HistorySnapshot after, HistoryAction action)
        {
            var groups = new List<Group>();
            bool secretGoods = action.Kind == "StealResource" || action.Kind == "DiscardResources" || action.Kind == "ResolveProgressChoice" || action.Card == "Taxation";
            // 待决目标来自权威待决，不得信任这类指令里未被规则使用的 OtherPlayerId。
            string counterpart = action.Kind == "ResolveProgressChoice" || action.Kind == "ResolveChoice" ? before.OtherPlayerId : action.OtherPlayerId ?? before.OtherPlayerId;
            bool sharedTransfer = action.Kind == "StealResource" || action.Kind == "ResolveProgressChoice" &&
                (before.PendingKind == "ProgressGuildDues" || before.PendingKind == "ProgressWedding" || before.PendingKind == "ProgressHarbor");
            foreach (var player in after.Players)
            {
                var previous = before.Players.Single(p => p.Id == player.Id);
                var changes = Enumerable.Range(0, player.Goods.Length).Where(i => player.Goods[i] != previous.Goods[i]).Select(i => new GameHistoryChange
                {
                    PlayerId = player.Id, Category = "goods", GoodIndex = i, Amount = player.Goods[i] - previous.Goods[i], Name = GoodNames[i], IsPrivate = secretGoods
                }).ToArray();
                if (changes.Length > 0) groups.Add(new Group { Details = changes, Viewers = !secretGoods ? null :
                    sharedTransfer ? new[] { action.PlayerId, counterpart, player.Id }.Where(x => x != null).Distinct().ToArray() :
                    action.Card == "Taxation" ? new[] { action.PlayerId, player.Id } : new[] { player.Id } });
                AddCards(groups, previous.Development, player.Development, player.Id, "development", action, counterpart, false);
                AddCards(groups, previous.Progress, player.Progress, player.Id, "progress", action, counterpart, false);
                AddCards(groups, previous.Victory, player.Victory, player.Id, "progress", action, counterpart, true);
                if (player.DefenderPoints != previous.DefenderPoints) AddStatus(groups, player.Id, "defender", "卡坦保卫者", player.DefenderPoints - previous.DefenderPoints);
                if (player.ClothTokens != previous.ClothTokens) AddStatus(groups, player.Id, "count", "布匹筹码", player.ClothTokens - previous.ClothTokens);
                if (player.WonderLevel != previous.WonderLevel) AddStatus(groups, player.Id, "status", "奇迹（现为 " + player.WonderLevel + " 级）", player.WonderLevel - previous.WonderLevel);
                for (int i = 0; i < player.Improvements.Length; i++)
                    if (player.Improvements[i] != previous.Improvements[i]) AddStatus(groups, player.Id, "status", new[] { "贸易", "政治", "科学" }[i] + "改良（现为 " + player.Improvements[i] + " 级）", player.Improvements[i] - previous.Improvements[i]);
            }
            var oldPieces = before.Pieces.ToDictionary(p => p.Key, p => p);
            var newPieces = after.Pieces.ToDictionary(p => p.Key, p => p);
            foreach (var piece in before.Pieces.Where(p => !newPieces.ContainsKey(p.Key))) AddPiece(groups, piece, -1);
            foreach (var piece in after.Pieces.Where(p => !oldPieces.ContainsKey(p.Key))) AddPiece(groups, piece, 1);
            if (action.Kind == "ProposeTrade")
            {
                for (int i = 0; i < 8; i++)
                {
                    if (after.OfferGive[i] > 0) groups.Add(new Group { Details = new[] { new GameHistoryChange { PlayerId = action.PlayerId, Category = "offer", GoodIndex = i, Amount = -after.OfferGive[i], Name = "报价付出：" + GoodNames[i] } } });
                    if (after.OfferReceive[i] > 0) groups.Add(new Group { Details = new[] { new GameHistoryChange { PlayerId = action.PlayerId, Category = "offer", GoodIndex = i, Amount = after.OfferReceive[i], Name = "报价请求：" + GoodNames[i] } } });
                }
            }
            var entry = new GameHistoryEntry
            {
                Sequence = after.Sequence, Turn = before.Turn, PlayerId = action.PlayerId, OtherPlayerId = counterpart,
                Kind = action.Kind, Action = ActionName(action, before.PendingKind), TargetId = action.PublicTarget,
                Dice1 = action.Kind == "RollDice" ? after.Dice1 : 0, Dice2 = action.Kind == "RollDice" ? after.Dice2 : 0,
                EventDie = action.Kind == "RollDice" ? after.EventDie : null
            };
            records.Add(new Record { Entry = entry, Groups = groups.ToArray() });
        }
        private static void AddPiece(List<Group> groups, HistoryPiece piece, int amount)
        {
            groups.Add(new Group { Details = new[] { new GameHistoryChange { PlayerId = piece.PlayerId, Category = "piece", Name = piece.Name, LocationId = piece.LocationId, Amount = amount } } });
        }
        private static void AddStatus(List<Group> groups, string player, string category, string name, int amount)
        {
            groups.Add(new Group { Details = new[] { new GameHistoryChange { PlayerId = player, Category = category, Name = name, Amount = amount } } });
        }
        private static void AddCards(List<Group> groups, string[] before, string[] after, string player, string category, HistoryAction action, string counterpart, bool isPublic)
        {
            var changes = before.Union(after).Select(card => new { Card = card, Count = after.Count(c => c == card) - before.Count(c => c == card) }).Where(c => c.Count != 0);
            foreach (var c in changes)
            {
                bool played = c.Count < 0 && action.PlayerId == player && (action.Kind == "PlayProgressCard" && action.Card == c.Card || action.Kind == "Play" + c.Card);
                bool visible = isPublic || played;
                groups.Add(new Group
                {
                    Details = new[] { new GameHistoryChange { PlayerId = player, Category = category, Amount = c.Count, Name = CardName(c.Card), IsPrivate = !visible } },
                    Viewers = visible ? null : action.Kind == "ResolveProgressChoice" && action.PendingKind == "ProgressEspionage" ? new[] { action.PlayerId, counterpart, player } : new[] { player }
                });
            }
        }
        internal static readonly string[] GoodNames = { "木材", "砖块", "羊毛", "粮食", "矿石", "布匹", "钱币", "纸张" };
        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            {"SetupSettlement","摆放开局建筑"},{"SetupRoad","建造了开局道路"},{"SetupShip","建造了开局船"},{"SetupPort","摆放开局港口"},
            {"RollDice","掷骰"},{"BuildRoad","建造道路"},{"BuildSettlement","建造村庄"},{"BuildCity","升级城市"},{"BuildShip","建造船"},
            {"BankTrade","完成银行／港口交易"},{"ProposeTrade","提出交易"},{"AcceptTrade","接受交易"},{"RejectTrade","拒绝交易"},{"CancelTrade","取消交易"},
            {"BuyDevelopmentCard","购买发展卡"},{"PlayKnight","使用「骑士」"},{"PlayRoadBuilding","使用「道路建设」"},{"PlayYearOfPlenty","使用「丰收」"},{"PlayMonopoly","使用「垄断」"},
            {"PlaceFreeRoad","免费建造道路"},{"PlaceFreeShip","免费建造船"},{"FinishRoadBuilding","完成道路建设"},{"DiscardResources","弃置手牌"},
            {"MoveRobber","移动强盗"},{"StealResource","偷取手牌"},{"EndTurn","结束回合"},{"DeclareVictory","宣告胜利"},
            {"MoveShip","移动船"},{"MovePirate","移动海盗"},{"ChooseGoldResources","领取金矿资源"},{"StealCloth","偷取布匹"},
            {"PlacePort","安置港口"},{"ClaimWonder","认领奇迹"},{"BuildWonder","建造奇迹"},{"AttackFortress","攻击海盗要塞"},{"ChooseInvasionRoute","选择海盗路线"},
            {"BuildWall","建造城墙"},{"ImproveCity","升级城市设施"},{"RecruitKnight","招募骑士"},{"PromoteKnight","晋升骑士"},{"ActivateKnight","激活骑士"},
            {"MoveKnight","移动骑士"},{"DisplaceKnight","驱逐骑士"},{"ChaseRobber","骑士驱赶强盗"},{"ChasePirate","骑士驱赶海盗"},{"DiscardProgressCard","弃置发展卡"},{"CommercialHarborOffer","使用商业港口"},
            {"ResolveChoice","完成选择"},{"ResolveProgressChoice","完成发展卡选择"}
        };
        private static readonly Dictionary<string, string> PendingNames = new Dictionary<string, string>
        {
            {"ProgressRoads","选择免费道路／船"},{"ProgressGuildDues","结算行会征费"},{"ProgressEspionage","结算间谍"},{"ProgressHarbor","完成商业港口交换"},
            {"ProgressWedding","交出婚礼礼物"},{"ProgressSabotage","弃置破坏要求的手牌"},{"ProgressTreasonRemove","移除被叛变的骑士"},{"ProgressTreasonPlace","安置叛变骑士"},
            {"ProgressDraw","抽取发展卡"},{"ProgressDiscard","弃置超额发展卡"},{"DefenderProgress","领取保卫奖励"},{"Aqueduct","领取水渠资源"},
            {"PillageCity","选择降级城市"},{"DisplacedKnight","安置被驱逐的骑士"},{"Metropolis","安置大都会"}
        };
        private static string ActionName(HistoryAction action, string pending)
        {
            if (action.Kind == "PlayProgressCard") return "使用「" + CardName(action.Card) + "」";
            if ((action.Kind == "ResolveChoice" || action.Kind == "ResolveProgressChoice") && pending != null && PendingNames.TryGetValue(pending, out var choice)) return choice;
            return Names.TryGetValue(action.Kind, out var name) ? name : "完成操作";
        }
        internal static string CardName(string card)
        {
            string[] ids = {"Knight","RoadBuilding","YearOfPlenty","Monopoly","VictoryPoint","Alchemy","Crane","Engineering","Invention","Irrigation","Medicine","Mining","Printing","Smithing","CommercialHarbor","GuildDues","Merchant","MerchantFleet","ResourceMonopoly","TradeMonopoly","Diplomacy","Espionage","Encouragement","Intrigue","Taxation","Constitution","Treason","Wedding","Sabotage"};
            string[] names = {"骑士","道路建设","丰收","垄断","胜利点","炼金术","起重机","工程学","发明","灌溉","医学","采矿","印刷术","锻铁匠","商业港口","行会征费","商人","商船队","资源垄断","商品垄断","外交","间谍","鼓舞","谋略","征税","宪法","叛变","婚礼","破坏"};
            int index = Array.IndexOf(ids, card); return index >= 0 ? names[index] : "卡牌";
        }
    }
    internal sealed class HistoryAction
    {
        internal string PlayerId, OtherPlayerId, Kind, PublicTarget, Card, PendingKind;
    }
    internal sealed class HistoryPlayer
    {
        internal string Id;
        internal int[] Goods;
        internal int ClothTokens, WonderLevel, DefenderPoints;
        internal int[] Improvements = Array.Empty<int>();
        internal string[] Development, Progress = Array.Empty<string>(), Victory = Array.Empty<string>();
    }
    internal sealed class HistoryPiece
    {
        internal string PlayerId, Name, LocationId;
        internal string Key => PlayerId + ":" + Name + ":" + LocationId;
    }
    internal sealed class HistorySnapshot
    {
        internal int Turn, Dice1, Dice2;
        internal long Sequence;
        internal string EventDie, OtherPlayerId, PendingKind;
        internal HistoryPlayer[] Players;
        internal HistoryPiece[] Pieces;
        internal int[] OfferGive, OfferReceive;
    }
}
