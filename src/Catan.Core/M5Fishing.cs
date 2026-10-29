using System;
using System.IO;
using System.Linq;
using Catan.Core.TradeModules;
using Catan.Core.ThreeKingdoms;
using System.Runtime.Serialization;
using System.Collections.Generic;

namespace Catan.Core.M5
{
    public sealed partial class Command
    {
        [DataMember(Order=400, EmitDefaultValue=false)] public FishAction? FishAction { get; set; }
        [DataMember(Order=401, EmitDefaultValue=false)] public string[] FishTokenIds { get; set; }
        [DataMember(Order=402, EmitDefaultValue=false)] public long? ExpectedEventSequence { get; set; }
        [DataMember(Order=404, EmitDefaultValue=false)] public bool FishBuildShip { get; set; }
        [DataMember(Order=405, EmitDefaultValue=false)] public bool FishRemovePirate { get; set; }
    }
    public sealed partial class GameState
    {
        [DataMember(Order=401, EmitDefaultValue=false)] public FishDrawClaim[] FishProductionClaims { get; set; }
    }
    public sealed partial class CombinedGameSession
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
            Require(s.Turn > 0 && s.ActivePlayerId == c.PlayerId && s.PendingDecision == null && s.TradeOffer == null && s.DecisionQueue.Length == 0 && !s.ProductionPending && s.FishProductionClaims == null && ThreeKingdomsAllowsBaseCommand(s) &&
                (s.Phase == GamePhase.ProductionAwaitRoll || s.Phase == GamePhase.Action), "WrongPhase", "只能在本人自由掷骰前或行动阶段使用鱼和旧靴。");
        }
        private void SpendFish(GameState s, PlayerState p, Command c)
        {
            FishActionWindow(s, c);
            Require(c.FishAction.HasValue && Enum.IsDefined(typeof(FishAction), c.FishAction.Value), "InvalidFishAction", "请选择有效捕鱼行动。");
            var action = c.FishAction.Value;
            Require((!c.FishBuildShip || action==FishAction.BuildRoad) && (!c.FishRemovePirate || action==FishAction.RemoveRobber),"InvalidFishTarget","捕鱼目标类型与行动不符。");
            Require(FishingRules.CanPay(s.TradeModules.Fishing, p.Id, c.FishTokenIds, action), "InvalidFishPayment", "请选择足够且属于自己的鱼筹码，旧靴不能支付。");
            // 先预检完整目标，随后在宿主副本中扣费及执行。失败不提交随机状态、历史或筹码。
            PlayerState victim = null;
            switch (action)
            {
                case FishAction.RemoveRobber:
                    Require(c.FishRemovePirate ? s.PirateTileId != null && s.PirateTileId != "frame" : s.RobberTileId != null && s.RobberTileId != "frame", "RobberAlreadyOffBoard", "强盗已经在棋盘外。"); break;
                case FishAction.StealResource:
                    victim = s.Players.FirstOrDefault(other => other.Id == c.OtherPlayerId && other.Id != p.Id);
                    Require(victim != null && HandCount(victim) > 0 && B6AllianceRules.CanAttack(s.ThreeKingdoms, p.Id, victim.Id) && !B2Protected(s,victim.Id) && !B3Immune(s,victim.Id) && !B2SkillEngine.IsB4HandLossForbidden(s.ThreeKingdoms,victim.Id), "InvalidVictim", "请选择持有资源的另一位玩家。"); break;
                case FishAction.TakeResource:
                    Require(Enum.IsDefined(typeof(Resource), c.Resource), "InvalidResource", "请选择有效资源。");
                    Require(s.Bank[c.Resource] > 0 && B2SkillEngine.CanGainB6(s.ThreeKingdoms,p.Id,(int)c.Resource), "BankShortage", "供应没有所选资源。"); break;
                case FishAction.BuildRoad: if(c.FishBuildShip) ValidateShip(s, p, c.TargetId, null); else ValidateRoad(s, p, c.TargetId, null); break;
                case FishAction.BuyDevelopmentCard:
                    Require(Enum.IsDefined(typeof(ImprovementTrack),c.Track) && s.ProgressDecks.Any(d => d.Track == c.Track && d.Cards.Length > 0) && !B2DrawBlocked(s,p.Id), "EmptyDeck", "所选进步牌堆不可抽取。"); break;
            }
            FishingRules.Pay(s.TradeModules.Fishing, p.Id, c.FishTokenIds);
            switch (action)
            {
                case FishAction.RemoveRobber: if(c.FishRemovePirate) s.PirateTileId = null; else s.RobberTileId = null; break;
                case FishAction.StealResource:
                    StealFishCard(s, p, victim);
                    break;
                case FishAction.TakeResource: Transfer(s.Bank, p.Resources, c.Resource, 1); break;
                case FishAction.BuildRoad: if(c.FishBuildShip) PlaceShip(s, p, c.TargetId); else PlaceRoad(s, p, c.TargetId); break;
                case FishAction.BuyDevelopmentCard:
                    DrawProgressCard(s, p, c.Track); break;
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
            var lake = s.Tiles.FirstOrDefault(t => t.Id == fish.LakeTileId);
            if (ground == null && lake?.Vertices.Contains(vertex) != true) return;
            FishingRules.StartDraw(fish, new[] { new FishDrawClaim { PlayerId = player, SourceId = ground?.Id ?? lake.Id, VertexId = vertex, Count = 1 } }, values => Shuffle(values, s.Random));
        }
        // TB25.pdf 第10页、FCK.pdf 第1页：冻结正式产出的座次与额度，资源/水渠响应结束后才抽鱼。
        private void QueueFishProduction(GameState s, int total)
        {
            var fish = s.TradeModules?.Fishing;
            if (fish == null) return;
            s.FishProductionClaims = FishingRules.Claims(fish, TurnOrder(s).Select(p => p.Id).ToArray(), s.Tiles,
                s.Settlements.Where(b => B5Working(s,"Settlement",b.LocationId)), s.Cities, total, s.RobberTileId);
        }
        private void ResumeFishing(GameState s)
        {
            if (s.FishProductionClaims == null || s.PendingDecision != null || s.DecisionQueue.Length > 0 || s.ProductionPending || B2Busy(s) || s.ThreeKingdoms?.Automation?.PendingDecision != null) return;
            var claims = s.FishProductionClaims; s.FishProductionClaims = null;
            FishingRules.StartDraw(s.TradeModules.Fishing, claims, values => Shuffle(values, s.Random));
            SyncFishDecision(s);
        }
        // 捕鱼偷牌不是强盗；不套用强盗专属免疫，也不产生成为技能目标事件。
        private static void StealFishCard(GameState s, PlayerState p, PlayerState victim)
        {
            int index = Draw(s.Random, HandCount(victim));
            foreach(var r in Resources) { if(index < victim.Resources[r]) { if(B2SkillEngine.CanGainB6(s.ThreeKingdoms,p.Id,(int)r)) Transfer(victim.Resources,p.Resources,r,1); return; } index -= victim.Resources[r]; }
            foreach(var r in Commodities) { if(index < victim.Commodities[r]) { if(B2SkillEngine.CanGainB6(s.ThreeKingdoms,p.Id,5+(int)r)) Transfer(victim.Commodities,p.Commodities,r,1); return; } index -= victim.Commodities[r]; }
        }
        private bool FishingMapAllowed(GameState s, B6MapSnapshot map)
        {
            if(s.TradeModules?.Fishing == null) return true;
            var fish = s.TradeModules.Fishing.Copy();
            fish.LakeTileId = map.Tiles.SingleOrDefault(t => t.Resource == "lake")?.Id;
            if(fish.LakeTileId != s.TradeModules.Fishing.LakeTileId && fish.LakeTileId != null && !FishingScenarioCatalog.IsInterior(map.Tiles.Single(t=>t.Id==fish.LakeTileId),map.Tiles)) return false;
            try { FishingScenarioCatalog.Validate(fish,map.Tiles,scenario.Topology.Edges); return true; }
            catch(InvalidDataException) { return false; }
        }
        private static void QueueLakeDiscovery(GameState s, string player, Tile tile, string vertex)
        {
            var fish=s.TradeModules.Fishing; fish.LakeTileId=tile.Id;
            s.FishProductionClaims=(s.FishProductionClaims ?? Array.Empty<FishDrawClaim>()).Concat(new[] { new FishDrawClaim {PlayerId=player,SourceId=tile.Id,VertexId=vertex,Count=1} }).ToArray();
        }
        private static void SyncFishDecision(GameState s)
        {
            var fish = s.TradeModules.Fishing;
            if (fish.PendingPlayerId != null)
            {
                var returnPhase=s.PendingDecision?.Kind=="FishReplacement" ? s.PendingDecision.ReturnPhase : s.Phase;
                s.Phase = GamePhase.FishReplacement;
                s.PendingDecision = new PendingDecision { Kind = "FishReplacement", PlayerId = fish.PendingPlayerId, ReturnPhase = returnPhase };
            }
            else if (s.Phase == GamePhase.FishReplacement) { s.Phase = s.PendingDecision.ReturnPhase; s.PendingDecision = null; }
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
            if (s.FishProductionClaims != null && (fish == null || s.FishProductionClaims.Length > 252 || fish.Queue.Length > 0 || s.FishProductionClaims.Any(q => q == null || q.Count < 1 || q.Count > 2 || !s.Players.Any(p => p.Id == q.PlayerId) || !scenario.Topology.Vertices.Any(v => v.Id == q.VertexId))))
                throw new InvalidDataException("冻结的捕鱼产出队列无效。");
            FishingRules.Validate(fish, s.Players.Select(p => p.Id).ToArray(), s.Tiles, scenario.Topology.Edges);
            if ((s.Phase == GamePhase.FishReplacement) != (fish?.PendingPlayerId != null) ||
                (s.Phase == GamePhase.FishReplacement && (s.PendingDecision?.Kind != "FishReplacement" || (s.PendingDecision.ReturnPhase != GamePhase.Action && s.PendingDecision.ReturnPhase != GamePhase.ProductionAwaitRoll) || s.TradeOffer != null)))
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
                default: return "取得进步牌";
            }
        }
        private IEnumerable<Command> LegalFishingActions(string player)
        {
            var fish = state.TradeModules?.Fishing;
            if(fish == null) yield break;
            Command Make(CommandKind kind) => new Command { PlayerId=player, Kind=kind, ExpectedEventSequence=state.EventSequence, WindowId=state.ThreeKingdoms?.WindowId };
            if(fish.PendingPlayerId == player)
            {
                foreach(var token in new string[] { null }.Concat(fish.Holdings.Single(h=>h.PlayerId==player).TokenIds))
                { var c=Make(CommandKind.ResolveFishReplacement);c.DecisionId=fish.PendingDecisionId;c.TargetId=token;if(IsLegal(c))yield return c; }
                yield break;
            }
            if(state.ActivePlayerId != player || state.Turn == 0 || state.PendingDecision != null || state.TradeOffer != null || !ThreeKingdomsAllowsBaseCommand(state)) yield break;
            if(fish.OldBootPlayerId == player)
                foreach(var other in state.Players.Where(p=>p.Id!=player)) { var c=Make(CommandKind.PassOldBoot);c.OtherPlayerId=other.Id;if(IsLegal(c))yield return c; }
            var tokens=FishingRules.View(fish,player,state.TargetVictoryPoints.Value,state.EventSequence).OwnTokens;
            foreach(FishAction action in Enum.GetValues(typeof(FishAction)))
            {
                var payment=FishingPayment.Select(tokens,action); if(payment.Length == 0) continue;
                var candidates=new List<Command>();
                Command Spend() { var c=Make(CommandKind.SpendFish);c.FishAction=action;c.FishTokenIds=payment;return c; }
                if(action==FishAction.StealResource) foreach(var other in state.Players.Where(p=>p.Id!=player)) {var c=Spend();c.OtherPlayerId=other.Id;candidates.Add(c);}
                else if(action==FishAction.TakeResource) foreach(var resource in Resources) {var c=Spend();c.Resource=resource;candidates.Add(c);}
                else if(action==FishAction.BuildRoad) foreach(var edge in scenario.Topology.Edges) foreach(var ship in new[] { false, true }) {var c=Spend();c.TargetId=edge.Id;c.FishBuildShip=ship;candidates.Add(c);}
                else if(action==FishAction.RemoveRobber) foreach(var pirate in new[] {false,true}) {var c=Spend();c.FishRemovePirate=pirate;candidates.Add(c);}
                else if(action==FishAction.BuyDevelopmentCard) foreach(ImprovementTrack track in Enum.GetValues(typeof(ImprovementTrack))) {var c=Spend();c.Track=track;candidates.Add(c);}
                else candidates.Add(Spend());
                foreach(var c in candidates) if(IsLegal(c))yield return c;
            }
        }
    }
}
