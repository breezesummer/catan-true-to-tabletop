using System;
using System.IO;
using System.Linq;
using Catan.Core.TradeModules;

namespace Catan.Core.M2
{
    public sealed partial class BaseGameSession
    {
        private TradeModuleView TradeView(GameState s, string player)
        {
            var view = HarbormasterRules.View(s.TradeModules, HarborScores(s));
            if (view != null) view.Fishing = FishingRules.View(s.TradeModules.Fishing, player, s.TargetVictoryPoints.Value, s.EventSequence);
            return view;
        }
        private static int PersonalVictoryTarget(GameState s, string player) => FishingRules.VictoryTarget(s.TradeModules?.Fishing, player, s.TargetVictoryPoints.Value);
        private static void FishCommandVersion(GameState s, Command c)
        {
            Require(s.TradeModules?.Fishing != null, "FishingDisabled", "本局未开启捕鱼。");
            Require(c.ExpectedEventSequence == s.EventSequence, "StaleState", "局面已经变化，请重新选择。");
        }
        private static void FishActionWindow(GameState s, Command c)
        {
            FishCommandVersion(s, c);
            Require(s.Turn > 0 && s.ActivePlayerId == c.PlayerId && s.PendingDecision == null && s.TradeOffer == null &&
                (s.Phase == GamePhase.ProductionAwaitRoll || s.Phase == GamePhase.Action), "WrongPhase", "只能在本人自由掷骰前或行动阶段使用鱼和旧靴。");
        }
        private void SpendFish(GameState s, PlayerState p, Command c)
        {
            FishActionWindow(s, c);
            Require(c.FishAction.HasValue && Enum.IsDefined(typeof(FishAction), c.FishAction.Value), "InvalidFishAction", "请选择有效捕鱼行动。");
            var action = c.FishAction.Value;
            Require(FishingRules.CanPay(s.TradeModules.Fishing, p.Id, c.FishTokenIds, action), "InvalidFishPayment", "请选择足够且属于自己的鱼筹码，旧靴不能支付。");
            // 先预检完整目标，随后在宿主副本中扣费及执行。失败不提交随机状态、历史或筹码。
            PlayerState victim = null;
            switch (action)
            {
                case FishAction.RemoveRobber:
                    Require(s.RobberTileId != null, "RobberAlreadyOffBoard", "强盗已经在棋盘外。"); break;
                case FishAction.StealResource:
                    victim = s.Players.FirstOrDefault(other => other.Id == c.OtherPlayerId && other.Id != p.Id);
                    Require(victim != null && victim.Resources.Total > 0, "InvalidVictim", "请选择持有资源的另一位玩家。"); break;
                case FishAction.TakeResource:
                    Require(Enum.IsDefined(typeof(Resource), c.Resource), "InvalidResource", "请选择有效资源。");
                    Require(s.Bank[c.Resource] > 0, "BankShortage", "供应没有所选资源。"); break;
                case FishAction.BuildRoad: ValidateRoad(s, p, c.TargetId, null); break;
                case FishAction.BuyDevelopmentCard:
                    Require(s.DevelopmentDeck.Length > 0, "EmptyDeck", "发展牌已经售完。"); break;
            }
            FishingRules.Pay(s.TradeModules.Fishing, p.Id, c.FishTokenIds);
            switch (action)
            {
                case FishAction.RemoveRobber: s.RobberTileId = null; break;
                case FishAction.StealResource:
                    var index = Draw(s.Random, victim.Resources.Total);
                    foreach (var r in Resources) { if (index < victim.Resources[r]) { Transfer(victim.Resources, p.Resources, r, 1); break; } index -= victim.Resources[r]; }
                    break;
                case FishAction.TakeResource: Transfer(s.Bank, p.Resources, c.Resource, 1); break;
                case FishAction.BuildRoad: PlaceRoad(s, p, c.TargetId); break;
                case FishAction.BuyDevelopmentCard:
                    p.DevelopmentCards = Append(p.DevelopmentCards, new DevelopmentCard { Kind = s.DevelopmentDeck[0], BoughtTurn = s.Turn });
                    s.DevelopmentDeck = s.DevelopmentDeck.Skip(1).ToArray(); break;
            }
        }
        private static void PassOldBoot(GameState s, PlayerState p, Command c)
        {
            FishActionWindow(s, c);
            Require(s.TradeModules.Fishing.OldBootPlayerId == p.Id, "NotBootOwner", "只有旧靴持有人能够转交。");
            Require(c.OtherPlayerId != p.Id && s.Players.Any(other => other.Id == c.OtherPlayerId) && VictoryPoints(s, c.OtherPlayerId, false) >= VictoryPoints(s, p.Id, false),
                "InvalidBootRecipient", "只能交给公开分数不少于自己的另一位玩家。");
            s.TradeModules.Fishing.OldBootPlayerId = c.OtherPlayerId;
        }
        private void GrantSetupFish(GameState s, string player, string vertex)
        {
            var fish = s.TradeModules?.Fishing;
            if (fish == null || s.SetupPlacementIndex < s.Players.Length) return;
            var ground = fish.Grounds.FirstOrDefault(g => g.VertexIds.Contains(vertex));
            var lake = scenario.Topology.Tiles.Single(t => t.Id == fish.LakeTileId);
            if (ground == null && !lake.Vertices.Contains(vertex)) return;
            FishingRules.StartDraw(fish, new[] { new FishDrawClaim { PlayerId = player, SourceId = ground?.Id ?? lake.Id, VertexId = vertex, Count = 1 } }, values => Shuffle(values, s.Random));
        }
        private void ProduceFish(GameState s, int total)
        {
            var fish = s.TradeModules?.Fishing;
            if (fish == null) return;
            int active = Array.FindIndex(s.Players, p => p.Id == s.ActivePlayerId);
            var order = Enumerable.Range(0, s.Players.Length).Select(i => s.Players[(active + i) % s.Players.Length].Id).ToArray();
            FishingRules.StartDraw(fish, FishingRules.Claims(fish, order, scenario.Topology.Tiles, s.Settlements, s.Cities, total, s.RobberTileId), values => Shuffle(values, s.Random));
            SyncFishDecision(s);
        }
        private static void SyncFishDecision(GameState s)
        {
            var fish = s.TradeModules.Fishing;
            if (fish.PendingPlayerId != null)
            {
                s.Phase = GamePhase.FishReplacement;
                s.PendingDecision = new PendingDecision { Kind = "FishReplacement", PlayerId = fish.PendingPlayerId, ReturnPhase = GamePhase.Action };
            }
            else if (s.Phase == GamePhase.FishReplacement) { s.PendingDecision = null; s.Phase = GamePhase.Action; }
        }
        private static void ResolveFishReplacement(GameState s, Command c)
        {
            FishCommandVersion(s, c);
            var fish = s.TradeModules.Fishing;
            Require(s.Phase == GamePhase.FishReplacement && fish.PendingPlayerId == c.PlayerId && fish.PendingDecisionId == c.DecisionId,
                "InvalidFishDecision", "只能由待决玩家处理当前换抽选择。");
            Require(c.TargetId == null || fish.Holdings.Single(h => h.PlayerId == c.PlayerId).TokenIds.Contains(c.TargetId), "InvalidFishToken", "只能弃掉自己的一枚鱼筹码，或放弃换抽。");
            FishingRules.ResolveReplacement(fish, c.TargetId, values => Shuffle(values, s.Random));
            SyncFishDecision(s);
        }
        private void AssertFishingInvariants(GameState s)
        {
            var fish = s.TradeModules?.Fishing;
            FishingRules.Validate(fish, s.Players.Select(p => p.Id).ToArray(), scenario.Topology.Tiles, scenario.Topology.Edges);
            if ((s.Phase == GamePhase.FishReplacement) != (fish?.PendingPlayerId != null) ||
                (s.Phase == GamePhase.FishReplacement && (s.PendingDecision?.Kind != "FishReplacement" || s.PendingDecision.ReturnPhase != GamePhase.Action || s.TradeOffer != null)))
                throw new InvalidDataException("捕鱼阶段与待决不一致。");
        }
        private static string FishActionName(FishAction action)
        {
            switch (action)
            {
                case FishAction.RemoveRobber: return "移出强盗";
                case FishAction.StealResource: return "偷取一张资源";
                case FishAction.TakeResource: return "取得一张资源";
                case FishAction.BuildRoad: return "修建道路";
                default: return "取得发展牌";
            }
        }
    }
}
