using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core;
using Catan.Core.M5;
using Command = Catan.Core.M5.Command;
using CommandKind = Catan.Core.M5.CommandKind;
using GamePhase = Catan.Core.M5.GamePhase;
using PlayerView = Catan.Core.M5.PlayerView;
using static Catan.AI.AiHeuristics;

namespace Catan.AI
{
/// <summary>根据当前玩家的合法视图，确定性地选择行动；以尽快获胜及保住早期城市为目标。</summary>
public sealed partial class CombinedAi
{
    private static readonly Resource[] Resources = (Resource[])Enum.GetValues(typeof(Resource));
    private static readonly Commodity[] Commodities = (Commodity[])Enum.GetValues(typeof(Commodity));
    private static ResourceBag Bag(int wood = 0, int brick = 0, int wool = 0, int wheat = 0, int ore = 0) => new() { Wood = wood, Brick = brick, Wool = wool, Wheat = wheat, Ore = ore };

    /// <summary>Choose one command. The caller supplies its unique command ID and executes it through the rule session.</summary>
    public Command Decide(PlayerView view)
    {
        if (view == null) throw new ArgumentNullException(nameof(view));
        if (view.Phase == GamePhase.Finished) throw new InvalidOperationException("The game has finished.");
        var actor = view.ThreeKingdoms?.PendingDecision?.PlayerId ?? view.ThreeKingdoms?.SkillDecision?.PlayerId ?? (view.TradeOffer != null ? view.TradeOffer.NextResponder(view.PlayerId) : view.PendingDecision?.PlayerId ?? view.ActivePlayerId);
        var canDiscard = view.Phase == GamePhase.Discard && view.Discards.Any(d => d.PlayerId == view.PlayerId);
        if (view.ThreeKingdoms?.PendingDecision != null || view.ThreeKingdoms?.SkillDecision != null ? actor != view.PlayerId : view.Phase == GamePhase.Discard ? !canDiscard : actor != view.PlayerId) throw new InvalidOperationException("This seat has no pending action.");
        var command = Copy(Choose(view));
        if (view.ThreeKingdoms != null) command.WindowId = view.ThreeKingdoms.WindowId;
        return CombinedGameSession.StampControlB6(view,command);
    }
    private Command Choose(PlayerView view)
    {
        Command Make(CommandKind kind, string target = null) => new() { PlayerId = view.PlayerId, Kind = kind, TargetId = target! };
        // 优先完成强制选将与技能待决；主动隐退保留给玩家，不作循环刷取策略。
        var legal = (view.LegalActions ?? Array.Empty<Command>()).Where(c => c.Kind != CommandKind.RetireGeneral).ToList();
        var generalChoice = legal.FirstOrDefault(c => c.Kind == CommandKind.ChooseGenerals || c.Kind == CommandKind.DeployGeneral || c.Kind == CommandKind.AdvanceThreeKingdomsPhase);
        if (view.ThreeKingdoms?.SkillDecision != null)
        {
            var choiceGoal = SpecialGoal(view) ?? (view.Board.Settlements.Any(b => b.PlayerId == view.PlayerId) ? Bag(wheat: 2, ore: 3) : Bag(1, 1, 1, 1));
            var skill = ChooseGeneralSkill(view, choiceGoal, new CommodityBag { Paper = view.OwnImprovements[(int)ImprovementTrack.Science] < 3 ? view.OwnImprovements[(int)ImprovementTrack.Science] + 1 : 0 });
            if (skill != null) return skill;
            throw new InvalidOperationException("武将技能待决没有可用合法动作。");
        }
        if (generalChoice != null) return generalChoice;
        if (view.TradeOffer != null) return RespondToTrade(view);
        if (view.Phase == GamePhase.SetupSettlement)
            return Make(CommandKind.SetupSettlement, view.LegalVertexIds.OrderByDescending(v => SetupValue(view, v)).ThenBy(v => v).First());
        if (view.Phase == GamePhase.SetupRoad && view.LegalShipEdgeIds.Length > 0)
            return Make(CommandKind.SetupShip, view.LegalShipEdgeIds.OrderByDescending(id => SetupRouteValue(view, id)).ThenBy(id => id, StringComparer.Ordinal).First());
        if (view.Phase == GamePhase.SetupRoad)
            return Make(CommandKind.SetupRoad, view.LegalEdgeIds.OrderByDescending(id => SetupRouteValue(view, id)).ThenBy(id => id, StringComparer.Ordinal).First());
        if (view.Phase == GamePhase.ProductionAwaitRoll)
        {
            var alchemy = legal.FirstOrDefault(c => c.Kind == CommandKind.PlayProgressCard && c.ProgressCard == ProgressCardKind.Alchemy);
            if (alchemy != null)
            {
                var sum = Enumerable.Range(2, 11).Where(n => n != 7).OrderByDescending(n => view.Board.Tiles.Where(t => t.Number == n && t.Id != view.Board.RobberTileId)
                    .Sum(t => Buildings(view).Where(b => t.Vertices.Contains(b.LocationId)).Sum(b => b.PlayerId == view.PlayerId ? 3.0 : -0.5))).First();
                var chosen = Copy(alchemy); chosen.ChosenDice1 = Math.Min(6, sum - 1); chosen.ChosenDice2 = sum - chosen.ChosenDice1; return chosen;
            }
            return Make(CommandKind.RollDice);
        }
        if (view.Phase == GamePhase.Discard)
        {
            var discard = Make(CommandKind.DiscardResources); discard.Resources = Bag(); discard.Commodities = new();
            var amount = view.Discards.Single(d => d.PlayerId == view.PlayerId).Amount;
            while (amount-- > 0)
            {
                var resource = Resources.OrderByDescending(r => view.OwnResources[r] - discard.Resources[r]).First();
                var commodity = Commodities.OrderByDescending(c => view.OwnCommodities[c] - discard.Commodities[c]).First();
                if (view.OwnResources[resource] - discard.Resources[resource] >= view.OwnCommodities[commodity] - discard.Commodities[commodity]) discard.Resources[resource]++;
                else discard.Commodities[commodity]++;
            }
            return discard;
        }
        if (view.Phase == GamePhase.RobberMove)
        {
            var blockers = legal.Where(c => c.Kind == CommandKind.MovePirate || c.Kind == CommandKind.MoveRobber).ToList();
            return blockers.OrderByDescending(c => TokenValue(view, c)).ThenBy(c => c.TargetId, StringComparer.Ordinal).First();
        }
        if (view.Phase == GamePhase.RobberSteal)
        {
            var theft = Make(CommandKind.StealResource); theft.OtherPlayerId = view.PendingDecision.EligibleVictimIds.OrderByDescending(id => Threat(view, id)).ThenBy(id => id, StringComparer.Ordinal).First(); return theft;
        }
        if (view.Phase == GamePhase.PendingChoice && view.PendingDecision?.Kind == "ProgressDiscard")
        {
            var discard = Make(CommandKind.DiscardProgressCard); discard.ProgressCard = view.OwnProgressCards.OrderBy(CardValue).First(); return discard;
        }
        if (view.Phase != GamePhase.Action)
        {
            if (legal.Count == 0) throw new InvalidOperationException($"No public legal actions for {view.PlayerId}/{view.Phase}/{view.PendingDecision?.Kind}");
            if (view.PendingDecision?.Kind == "ProgressRoads")
            {
                var freeExpansion = Expansion(view);
                return legal.OrderByDescending(c =>
                {
                    if (c.Decline || c.TargetId == null) return -1000;
                    var projected = new Command { PlayerId = view.PlayerId, Kind = c.BuildShip ? CommandKind.BuildShip : CommandKind.BuildRoad, TargetId = c.TargetId };
                    if (view.OwnVictoryPoints + PointGain(view, projected) >= view.TargetVictoryPoints) return 10000;
                    if (freeExpansion.HasValue && freeExpansion.Value.path.Length > 0 && freeExpansion.Value.path[0] == c.TargetId && freeExpansion.Value.ship == c.BuildShip) return 1000;
                    return PendingValue(view, c);
                }).First();
            }
            return legal.OrderByDescending(c => PendingValue(view, c)).First();
        }
        legal.AddRange(view.LegalCityVertexIds.Select(v => Make(CommandKind.BuildCity, v)));
        legal.AddRange(view.LegalVertexIds.Select(v => Make(CommandKind.BuildSettlement, v)));
        legal.AddRange(view.LegalEdgeIds.Select(v => Make(CommandKind.BuildRoad, v)));
        legal.AddRange(view.LegalShipEdgeIds.Select(v => Make(CommandKind.BuildShip, v)));

        // 立即终局优先于保城、发展生产和所有干扰行动，包括领取大都会的下一步待决。
        if (view.OwnVictoryPoints >= view.TargetVictoryPoints && view.ScenarioId != "pirate-islands" && view.ScenarioId != "wonders-of-catan") return Make(CommandKind.DeclareVictory);
        var special = SpecialAction(view, legal); if (special != null) return special;
        var winning = legal.Where(c => view.OwnVictoryPoints + PointGain(view, c) >= view.TargetVictoryPoints)
            .OrderByDescending(c => ActionValue(view, c)).ThenBy(c => c.TargetId, StringComparer.Ordinal).FirstOrDefault();
        if (winning != null) return winning;
        // 对手即将终局时，先执行确有阻碍收益的蓝牌或截路；自己的立即胜利仍排在最前。
        var urgentTactic = legal.Where(c => UrgentInterference(view, c))
            .OrderByDescending(c => c.Kind == CommandKind.PlayProgressCard ? ProgressValue(view, c) : BlockValue(view, c))
            .ThenBy(c => c.TargetId, StringComparer.Ordinal).FirstOrDefault();
        if (urgentTactic != null) return urgentTactic;

        var knights = view.Board.Knights.Where(k => k.PlayerId == view.PlayerId).ToArray();
        var me = view.Players.Single(p => p.Id == view.PlayerId);
        var cityCount = view.Board.Cities.Count(b => b.PlayerId == view.PlayerId);
        var defenseGoal = DefenseGoal(view);
        var activeStrength = knights.Where(k => k.Active).Sum(k => k.Level);
        if (activeStrength < defenseGoal)
        {
            var defense = DefenseAction(view, legal);
            if (defense != null) return defense;
        }
        var scoring = legal.Select(c => (command: c, value: ActionValue(view, c)))
            .Where(x => x.value > 0).OrderByDescending(x => x.value).ThenBy(x => x.command.TargetId, StringComparer.Ordinal).FirstOrDefault();
        if (scoring.command != null) return scoring.command;
        var card = legal.Where(c => c.Kind == CommandKind.PlayProgressCard && ProgressValue(view, c) > 0).OrderByDescending(c => ProgressValue(view, c)).FirstOrDefault();
        if (card != null) return card;
        var harbor = legal.Where(c => c.Kind == CommandKind.CommercialHarborOffer
            && view.Players.Any(p => p.Id == c.OtherPlayerId && p.CommodityCount > 0)
            && AiOpponentStrategy.TradePenalty(view, c.OtherPlayerId) <= 0).OrderByDescending(c => view.OwnResources[c.Resource]).FirstOrDefault();
        if (harbor != null) return harbor;

        // 只投入一张激活麦或一次晋升争取近期唯一最高兵力，避免四人局中无止境军备竞赛。
        var defender = DefenderOpportunity(view, legal);
        if (defender != null) return defender;

        var chase = legal.FirstOrDefault(c => c.Kind == CommandKind.ChaseRobber);
        if (chase != null && view.BarbarianPosition < 5 && view.Board.Tiles.Any(t => t.Id == view.Board.RobberTileId && Buildings(view).Any(b => b.PlayerId == view.PlayerId && t.Vertices.Contains(b.LocationId)))) return chase;
        var chasePirate = legal.FirstOrDefault(c => c.Kind == CommandKind.ChasePirate);
        if (chasePirate != null && (view.BarbarianPosition < 4 || view.OwnResources.Wheat > 0))
        {
            double current = TokenValue(view, new Command { Kind = CommandKind.MovePirate, TargetId = view.Board.PirateTileId });
            double best = view.Board.Tiles.Where(t => t.Resource == "sea" && t.Id != view.Board.PirateTileId)
                .Select(t => TokenValue(view, new Command { Kind = CommandKind.MovePirate, TargetId = t.Id })).DefaultIfEmpty(0).Max();
            if (best - current > 20) return chasePirate;
        }
        var wall = legal.FirstOrDefault(c => c.Kind == CommandKind.BuildWall);
        if (wall != null && view.OwnResources.Brick >= 3 && view.OwnResources.Total + view.OwnCommodities.Total >= 7) return wall;
        var expansion = Expansion(view);
        var desired = Bag();
        CommodityBag commodityGoal = new();
        var pursueExpansion = false;
        if (activeStrength < defenseGoal && knights.Any(k => !k.Active)) desired.Wheat = 1;
        else if (activeStrength < defenseGoal) { desired.Wool = 1; desired.Ore = 1; desired.Wheat = 1; }
        else if (me.Pieces.Cities > 0 && view.Board.Settlements.Any(b => b.PlayerId == view.PlayerId)
            && (me.Pieces.Settlements == 0 || expansion == null ||
                SettlementPoints(view, expansion.Value.target) == 1 && view.OwnResources.Ore >= 2 ||
                expansion.Value.path.Length > 2 && Missing(view.OwnResources, Bag(wheat: 2, ore: 3)) <= 1)) desired = Bag(wheat: 2, ore: 3);
        else if (me.Pieces.Settlements > 0 && expansion != null)
        {
            pursueExpansion = true;
            desired = expansion.Value.path.Length == 0 ? Bag(1, 1, 1, 1) : expansion.Value.ship ? Bag(wood:1,wool:1) : Bag(1, 1);
        }
        else if (cityCount > 0)
        {
            var tracks = PreferredTracks(view).Where(t => view.OwnImprovements[(int)t] < 5 && ImprovementValue(view, t) > 0).ToArray();
            if (tracks.Length > 0) commodityGoal[TrackCommodity(tracks[0])] = view.OwnImprovements[(int)tracks[0]] + 1;
        }
        else if (!Buildings(view).Any(b => b.PlayerId == view.PlayerId)) desired = Bag(1, 1, 1, 1);
        // 已接近排水渠、可争大都会或后期贸易三级时，允许通过兑换完成设施。
        if (cityCount > 0 && activeStrength >= defenseGoal)
        {
            var track = PreferredTracks(view).FirstOrDefault(t => view.OwnImprovements[(int)t] < 5 &&
                view.OwnCommodities[TrackCommodity(t)] >= view.OwnImprovements[(int)t] &&
                ImprovementValue(view, t) >= 58);
            if (view.OwnImprovements[(int)track] < 5 && view.OwnCommodities[TrackCommodity(track)] >= view.OwnImprovements[(int)track] && ImprovementValue(view, track) >= 58)
            {
                desired = Bag(); commodityGoal[TrackCommodity(track)] = view.OwnImprovements[(int)track] + 1; pursueExpansion = false;
            }
        }
        if (pursueExpansion)
        {
            var reposition = RepositionShip(view, expansion); if (reposition != null) return reposition;
        }
        if (pursueExpansion && expansion.HasValue && expansion.Value.path.Length > 0 && Covers(view.OwnResources, desired))
        {
            var road = legal.FirstOrDefault(c => c.Kind == (expansion.Value.ship ? CommandKind.BuildShip : CommandKind.BuildRoad) && c.TargetId == expansion.Value.path[0]);
            if (road != null) return road;
        }
        var specialGoal=SpecialGoal(view);if(specialGoal!=null){desired=specialGoal;commodityGoal=new();}
        var activeSkill = ChooseGeneralSkill(view, desired, commodityGoal); if (activeSkill != null) return activeSkill;
        var proposal = ProposeTrade(view, desired); if (proposal != null) return proposal;
        foreach (var need in Resources.Where(r => view.OwnResources[r] < desired[r] && view.Bank[r] > 0 && CanReceive(view,(int)r)).Select(r => (resource: r, commodity: (Commodity?)null))
            .Concat(Commodities.Where(c => view.OwnCommodities[c] < commodityGoal[c] && view.CommodityBank[c] > 0 && CanReceive(view,5+(int)c)).Select(c => (resource: default(Resource), commodity: (Commodity?)c))))
        {
            foreach (var give in Resources.Where(r => (need.commodity.HasValue || r != need.resource) && view.OwnResources[r] - desired[r] >= Rate(view, r, null)))
            {
                var trade = Make(CommandKind.BankTrade); trade.GiveResource = give; trade.GiveAmount = Rate(view, give, null);
                trade.ReceiveResource = need.resource; trade.ReceiveIsCommodity = need.commodity.HasValue; trade.ReceiveCommodity = need.commodity.GetValueOrDefault(); return trade;
            }
            foreach (var give in Commodities.Where(c => c != need.commodity && view.OwnCommodities[c] - commodityGoal[c] >= Rate(view, null, c)))
            {
                var trade = Make(CommandKind.BankTrade); trade.GiveIsCommodity = true; trade.GiveCommodity = give; trade.GiveAmount = Rate(view, null, give);
                trade.ReceiveResource = need.resource; trade.ReceiveIsCommodity = need.commodity.HasValue; trade.ReceiveCommodity = need.commodity.GetValueOrDefault(); return trade;
            }
        }
        if (view.ThreeKingdoms?.HandLossForbidden != true && view.OwnProgressCards.Length > (view.ThreeKingdoms?.ProgressHandLimit ?? 4)) { var discard = Make(CommandKind.DiscardProgressCard); discard.ProgressCard = view.OwnProgressCards.OrderBy(CardValue).First(); return discard; }
        return Make(CommandKind.EndTurn);
    }

    private static int RollsLeft(PlayerView view) => RemainingRolls(view.Turn, Math.Max(view.OwnVictoryPoints, view.Players.Max(p => p.VictoryPoints)), view.TargetVictoryPoints);
    private static int SettlementPoints(PlayerView view, string vertex) => SeafaringPlanning.SettlementPoints(view.ScenarioId,
        view.Board.Tiles, Buildings(view).Where(b => b.PlayerId == view.PlayerId).ToArray(), vertex);
    private static double SettlementValue(PlayerView view, string vertex, int length) => SeafaringPlanning.SettlementValue(view.ScenarioId,
        view.Board.Tiles, view.Board.Edges, Buildings(view), view.PlayerId, vertex, length, RollsLeft(view), view.OwnVictoryPoints, view.TargetVictoryPoints);
    private static int Missing(ResourceBag hand, ResourceBag cost) => Resources.Sum(r => Math.Max(0, cost[r] - hand[r]));

    private static double Threat(PlayerView view, string player) => AiOpponentStrategy.Threat(view, player);

    private static double TokenValue(PlayerView view, Command c)
    {
        var tile = view.Board.Tiles.FirstOrDefault(t => t.Id == c.TargetId);
        if (tile == null) return 0;
        if (c.Kind == CommandKind.MovePirate)
            return SeafaringPlanning.PirateBlockadeValue(view.Board.Tiles, view.Board.Edges, Buildings(view), view.Board.Roads,
                view.Board.Ships.Select(ship => (ship.PlayerId, ship.LocationId)), view.PlayerId, c.TargetId, id => Threat(view, id));
        return Buildings(view).Where(b => tile.Vertices.Contains(b.LocationId)).Sum(b =>
        {
            if (b.PlayerId == view.PlayerId) return -100 - Pips(tile.Number) * 8.0;
            bool unique = !view.Board.Tiles.Any(t => t.Id != tile.Id && t.Resource == tile.Resource &&
                Buildings(view).Any(other => other.PlayerId == b.PlayerId && t.Vertices.Contains(other.LocationId)));
            int productionMultiplier = view.Board.Cities.Any(city => city.LocationId == b.LocationId) ? 2 : 1;
            return Threat(view, b.PlayerId) + Pips(tile.Number) * productionMultiplier * (unique ? 4 : 2);
        });
    }

    private static int DefenseGoal(PlayerView view)
    {
        bool Vulnerable(string player) => view.Board.Cities.Any(c => c.PlayerId == player && !view.Board.Metropolises.Any(m => m.LocationId == c.LocationId));
        if (!Vulnerable(view.PlayerId)) return 0;
        int Strength(string player) => view.Board.Knights.Where(k => k.PlayerId == player && k.Active).Sum(k => k.Level);
        var otherStrength = view.Players.Where(p => p.Id != view.PlayerId).Sum(p => Strength(p.Id));
        var collective = Math.Max(0, view.Board.Cities.Length - otherStrength);
        var exposedOpponents = view.Players.Where(p => p.Id != view.PlayerId && Vulnerable(p.Id)).ToArray();
        var minimum = exposedOpponents.Length == 0 ? collective : Math.Min(collective, exposedOpponents.Min(p => Strength(p.Id)) + 1);
        // 首次入侵前至少保留一名激活骑士，避免完全依赖其他玩家不移动或驱逐强盗。
        return !view.FirstBarbarianAttack ? Math.Max(1, minimum) : minimum;
    }

    private static Command DefenseAction(PlayerView view, List<Command> legal)
    {
        var activate = legal.Where(c => c.Kind == CommandKind.ActivateKnight)
            .OrderByDescending(c => view.Board.Knights.Single(k => k.LocationId == c.TargetId).Level).FirstOrDefault();
        if (activate != null) return activate;
        var smith = legal.FirstOrDefault(c => c.Kind == CommandKind.PlayProgressCard && c.ProgressCard == ProgressCardKind.Smithing &&
            (c.TargetIds ?? Array.Empty<string>()).Any(id => view.Board.Knights.Any(k => k.LocationId == id && k.Active)));
        if (smith != null) return smith;
        // 兵临城下而缺麦时，一次已激活骑士晋升能立即提高防线；早期则先筹较便宜的激活麦。
        if (view.BarbarianPosition >= 5)
        {
            var urgent = legal.FirstOrDefault(c => c.Kind == CommandKind.PromoteKnight && view.Board.Knights.Any(k => k.LocationId == c.TargetId && k.Active));
            if (urgent != null) return urgent;
        }
        if (view.Board.Knights.Where(k => k.PlayerId == view.PlayerId).Sum(k => k.Level) >= DefenseGoal(view)) return null;
        var promote = legal.Where(c => c.Kind == CommandKind.PromoteKnight)
            .OrderByDescending(c => view.Board.Knights.Single(k => k.LocationId == c.TargetId).Active).FirstOrDefault();
        if (promote != null) return promote;
        // 避免把必需骑士放在己方最有价值的未来村庄位置；同成本优先阻断领跑者。
        return legal.Where(c => c.Kind == CommandKind.RecruitKnight)
            .OrderByDescending(c => BlockValue(view, c) - Value(view, c.TargetId)).ThenBy(c => c.TargetId, StringComparer.Ordinal).FirstOrDefault();
    }

    private static Command DefenderOpportunity(PlayerView view, List<Command> legal)
    {
        if (view.BarbarianPosition < 5) return null;
        var strength = view.Board.Knights.Where(k => k.PlayerId == view.PlayerId && k.Active).Sum(k => k.Level);
        var rival = view.Players.Where(p => p.Id != view.PlayerId).Max(p => view.Board.Knights.Where(k => k.PlayerId == p.Id && k.Active).Sum(k => k.Level));
        if (strength > rival) return null;
        return legal.Where(c => c.Kind == CommandKind.ActivateKnight || c.Kind == CommandKind.PromoteKnight)
            .Where(c =>
            {
                var knight = view.Board.Knights.Single(k => k.LocationId == c.TargetId);
                int gain = c.Kind == CommandKind.ActivateKnight ? knight.Level : knight.Active ? 1 : 0;
                return strength + gain > rival && view.Board.Knights.Where(k => k.Active).Sum(k => k.Level) + gain >= view.Board.Cities.Length;
            }).OrderBy(c => c.Kind == CommandKind.ActivateKnight ? 1 : 2).FirstOrDefault();
    }

    private static bool GainsMetropolis(PlayerView view, ImprovementTrack track)
    {
        int next = view.OwnImprovements[(int)track] + 1;
        var holder = view.Board.Metropolises.FirstOrDefault(m => m.Track == track);
        return next >= 4 && next <= 5 && (holder == null || holder.PlayerId != view.PlayerId &&
            view.Players.Single(p => p.Id == holder.PlayerId).Improvements[(int)track] < next) &&
            view.Board.Cities.Any(c => c.PlayerId == view.PlayerId && !view.Board.Metropolises.Any(m => m.LocationId == c.LocationId));
    }

    private static IEnumerable<ImprovementTrack> PreferredTracks(PlayerView view) =>
        new[] { ImprovementTrack.Science, ImprovementTrack.Trade, ImprovementTrack.Politics }.OrderByDescending(t => ImprovementValue(view, t));

    private static double ImprovementValue(PlayerView view, ImprovementTrack track)
    {
        int level = view.OwnImprovements[(int)track];
        if (level >= 5) return -1;
        if (GainsMetropolis(view, track)) return 90;
        // 五级才能锁定大都会；对手接近四级时防守有意义，不能把所有四级都当作已不可夺取。
        if (level == 4 && view.Board.Metropolises.Any(m => m.Track == track && m.PlayerId == view.PlayerId))
            return view.Players.Any(p => p.Id != view.PlayerId && p.Improvements[(int)track] >= 3) ? 64 : 8;
        int remaining = RollsLeft(view);
        if (track == ImprovementTrack.Science && level < 3) return level == 2 && remaining >= 18 ? 70 : remaining >= 18 ? 30 : 5;
        if (track == ImprovementTrack.Trade && level < 3) return level == 2 && (remaining < 36 || view.OwnCommodities.Total >= 5) ? 60 : 18;
        if (track == ImprovementTrack.Politics && level == 2 && view.BarbarianPosition >= 4 &&
            view.Board.Knights.Any(k => k.PlayerId == view.PlayerId && k.Level == 2) && DefenseGoal(view) > 2) return 34;
        var rivalMetropolis = view.Board.Metropolises.FirstOrDefault(m => m.Track == track && m.PlayerId != view.PlayerId);
        if (level == 3 && rivalMetropolis != null && view.Players.Single(p => p.Id == rivalMetropolis.PlayerId).Improvements[(int)track] == 4 &&
            view.Board.Cities.Any(c => c.PlayerId == view.PlayerId && !view.Board.Metropolises.Any(m => m.LocationId == c.LocationId))) return 20;
        // 没有可夺取的大都会且技能已取得时，不为等级本身囤积昂贵商品。
        return level >= 3 ? -1 : 6;
    }

    private static double PointGain(PlayerView view, Command c)
    {
        double points = c.Kind == CommandKind.BuildSettlement ? SettlementPoints(view, c.TargetId) : c.Kind == CommandKind.BuildCity ? 1 : 0;
        if (c.Kind == CommandKind.ImproveCity && GainsMetropolis(view, c.Track)) points += 2;
        if (c.Kind == CommandKind.PlayProgressCard)
        {
            if (c.ProgressCard == ProgressCardKind.Medicine) points++;
            if (c.ProgressCard == ProgressCardKind.Merchant && view.MerchantPlayerId != view.PlayerId) points++;
            if (c.ProgressCard == ProgressCardKind.Crane && GainsMetropolis(view, c.Track)) points += 2;
        }
        if (view.OwnVictoryPoints >= view.TargetVictoryPoints - 5 &&
            (c.Kind == CommandKind.BuildRoad || c.Kind == CommandKind.BuildShip || c.Kind == CommandKind.BuildSettlement || c.Kind == CommandKind.RecruitKnight || c.Kind == CommandKind.MoveKnight || c.Kind == CommandKind.DisplaceKnight))
            points += RouteAwardGain(view, c);
        return points;
    }

    private static double ActionValue(PlayerView view, Command c)
    {
        switch (c.Kind)
        {
            case CommandKind.BuildCity: return 55 + Math.Min(8, Value(view, c.TargetId) * RollsLeft(view) / 180);
            case CommandKind.BuildSettlement: return (SettlementPoints(view, c.TargetId) > 1 ? 110 : 50) + Math.Min(9, SettlementValue(view, c.TargetId, 0) / 8) + BlockValue(view, c);
            case CommandKind.ImproveCity: return ImprovementValue(view, c.Track);
            case CommandKind.PlayProgressCard:
                if (c.ProgressCard == ProgressCardKind.Medicine) return 75 + Value(view, c.TargetId) / 10;
                if (c.ProgressCard == ProgressCardKind.Crane) return ImprovementValue(view, c.Track) > 0 ? ImprovementValue(view, c.Track) + 4 : -1;
                if (c.ProgressCard == ProgressCardKind.Merchant && view.MerchantPlayerId != view.PlayerId) return 70;
                return 0;
            case CommandKind.BuildRoad:
            case CommandKind.BuildShip:
                if (view.OwnVictoryPoints < view.TargetVictoryPoints - 3 || view.LongestRoadPlayerId == view.PlayerId) return 0;
                if (RouteAwardGain(view, c) > 0) return 85;
                int length = ProjectedRouteLength(view, view.PlayerId, c);
                int needed = Math.Max(5, view.Players.Where(p => p.Id != view.PlayerId).Max(p => p.LongestRoadLength) + 1);
                return length > view.Players.Single(p => p.Id == view.PlayerId).LongestRoadLength && needed - length == 1 ? 22 : 0;
            case CommandKind.RecruitKnight: case CommandKind.MoveKnight: case CommandKind.DisplaceKnight: case CommandKind.ActivateKnight: return KnightTacticValue(view, c);
            default: return 0;
        }
    }

    private static double KnightTacticValue(PlayerView view, Command c)
    {
        if (c.Kind != CommandKind.ActivateKnight) return BlockValue(view, c);
        var knight = view.Board.Knights.FirstOrDefault(k => k.PlayerId == view.PlayerId && k.LocationId == c.TargetId);
        if (knight == null) return 0;
        // 本回合激活只能为下回合准备，不把尚不能行动的骑士算成立即截路。
        var routes = view.Board.Roads.Where(r => r.PlayerId == view.PlayerId).Select(r => r.LocationId).Concat(view.Board.Ships.Where(ship => ship.PlayerId == view.PlayerId).Select(ship => ship.LocationId)).ToHashSet();
        var seen = new HashSet<string> { knight.LocationId }; var queue = new Queue<string>(); queue.Enqueue(knight.LocationId);
        double best = 0;
        while (queue.Count > 0)
        {
            string vertex = queue.Dequeue();
            foreach (var edge in view.Board.Edges.Where(e => routes.Contains(e.Id) && e.Vertices.Contains(vertex)))
            {
                string next = edge.Vertices.Single(v => v != vertex);
                if (!seen.Add(next) || Buildings(view).Any(b => b.PlayerId != view.PlayerId && b.LocationId == next)) continue;
                var occupant = view.Board.Knights.FirstOrDefault(k => k.LocationId == next);
                if (occupant == null || occupant.PlayerId == view.PlayerId) queue.Enqueue(next);
                if (Buildings(view).Any(b => b.LocationId == next) || occupant != null && (occupant.PlayerId == view.PlayerId || occupant.Level >= knight.Level)) continue;
                best = Math.Max(best, BlockValue(view, new Command { Kind = occupant == null ? CommandKind.MoveKnight : CommandKind.DisplaceKnight,
                    PlayerId = view.PlayerId, SourceId = knight.LocationId, TargetId = next }));
            }
        }
        return best >= 20 ? best * 0.65 - 3 : 0;
    }

    // catan-cities-knights-2025-en.pdf 第 9–10 页：敌骑士截断道路；第 12 页同样适用于船线。
    // 只评估规则视图给出的合法招募/移动/驱逐，不自行生成跨越敌棋子的移动。
    private static double BlockValue(PlayerView view, Command c)
    {
        bool moves = c.Kind == CommandKind.MoveKnight || c.Kind == CommandKind.DisplaceKnight;
        if (!moves && c.Kind != CommandKind.RecruitKnight && c.Kind != CommandKind.BuildSettlement) return 0;
        double benefit = 0;
        foreach (var rival in view.Players.Where(p => p.Id != view.PlayerId))
        {
            double At(string vertex)
            {
                if (vertex == null) return 0;
                var adjacent = view.Board.Edges.Where(e => e.Vertices.Contains(vertex)).Select(e => e.Id).ToArray();
                int routes = view.Board.Roads.Count(r => r.PlayerId == rival.Id && adjacent.Contains(r.LocationId)) + view.Board.Ships.Count(r => r.PlayerId == rival.Id && adjacent.Contains(r.LocationId));
                if (routes == 0) return 0;
                bool frontier = view.Board.Edges.Any(e => e.Vertices.Contains(vertex) && !view.Board.Roads.Any(r => r.LocationId == e.Id) && !view.Board.Ships.Any(ship => ship.LocationId == e.Id));
                bool site = view.Board.Tiles.Any(t => t.Resource != "sea" && t.Resource != "fog" && t.Vertices.Contains(vertex))
                    && !view.Board.Edges.Where(e => e.Vertices.Contains(vertex)).Any(e => Buildings(view).Any(b => e.Vertices.Contains(b.LocationId)));
                double value = frontier ? 6 + Threat(view, rival.Id) / 8 : 0;
                if (site && view.Board.Ships.Any(ship => ship.PlayerId == rival.Id && adjacent.Contains(ship.LocationId))) value += 30 + Threat(view, rival.Id) / 2;
                else if (site) value += 8;
                return value;
            }
            double target = At(c.TargetId) - (moves ? At(c.SourceId) : 0);
            int projected = ProjectedRouteLength(view, rival.Id, c);
            if (view.LongestRoadPlayerId == rival.Id && projected < rival.LongestRoadLength)
            {
                bool losesAward = projected < 5 || view.Players.Any(p => p.Id != rival.Id && p.LongestRoadLength > projected);
                target += (losesAward ? 45 : 16) + Threat(view, rival.Id) / 2;
            }
            benefit = Math.Max(benefit, target);
        }
        if (moves && view.BarbarianPosition >= 4 && view.OwnResources.Wheat == 0)
        {
            int remaining = view.Board.Knights.Where(k => k.PlayerId == view.PlayerId && k.Active && k.LocationId != c.SourceId).Sum(k => k.Level);
            if (remaining < DefenseGoal(view)) benefit -= 45;
        }
        return Math.Max(0, benefit);
    }

    private static IEnumerable<string> AffectedOpponents(PlayerView view, Command c)
    {
        if (c.OtherPlayerId != null && c.OtherPlayerId != view.PlayerId) return new[] { c.OtherPlayerId };
        if (c.ProgressCard == ProgressCardKind.Taxation)
            return Buildings(view).Where(b => b.PlayerId != view.PlayerId && view.Board.Tiles.Any(t => t.Id == c.TargetId && t.Vertices.Contains(b.LocationId))).Select(b => b.PlayerId).Distinct();
        if (c.ProgressCard == ProgressCardKind.Diplomacy)
            return view.Board.Roads.Where(r => r.LocationId == c.TargetId && r.PlayerId != view.PlayerId).Select(r => r.PlayerId);
        if (c.ProgressCard == ProgressCardKind.Intrigue)
            return view.Board.Knights.Where(k => k.LocationId == c.TargetId && k.PlayerId != view.PlayerId).Select(k => k.PlayerId);
        if (c.ProgressCard == ProgressCardKind.Sabotage || c.ProgressCard == ProgressCardKind.Wedding)
            return view.Players.Where(p => p.Id != view.PlayerId && p.ResourceCount + p.CommodityCount > 0
                && (c.ProgressCard == ProgressCardKind.Sabotage ? p.VictoryPoints >= view.OwnVictoryPoints : p.VictoryPoints > view.OwnVictoryPoints)).Select(p => p.Id);
        return Array.Empty<string>();
    }

    private static bool UrgentInterference(PlayerView view, Command c)
    {
        bool urgent(string id) => AiOpponentStrategy.MinimumWinningActions(view, id) <= 2;
        if (c.Kind == CommandKind.PlayProgressCard && ProgressCatalog.Track(c.ProgressCard) == ImprovementTrack.Politics)
            return AffectedOpponents(view, c).Any(urgent) && ProgressValue(view, c) > 0;
        if (BlockValue(view, c) <= 0) return false;
        return view.Players.Where(p => p.Id != view.PlayerId && urgent(p.Id)).Any(p =>
            ProjectedRouteLength(view, p.Id, c) < p.LongestRoadLength ||
            view.Board.Edges.Any(e => e.Vertices.Contains(c.TargetId) && view.Board.Roads.Any(r => r.PlayerId == p.Id && r.LocationId == e.Id)) || view.Board.Edges.Any(e => e.Vertices.Contains(c.TargetId) && view.Board.Ships.Any(ship => ship.PlayerId == p.Id && ship.LocationId == e.Id)));
    }

    private static int RouteAwardGain(PlayerView view, Command c)
    {
        int own = ProjectedRouteLength(view, view.PlayerId, c);
        if (own < 5) return 0;
        var lengths = view.Players.ToDictionary(p => p.Id, p => p.Id == view.PlayerId ? own :
            c.Kind == CommandKind.BuildSettlement || c.Kind == CommandKind.RecruitKnight || c.Kind == CommandKind.MoveKnight || c.Kind == CommandKind.DisplaceKnight ? ProjectedRouteLength(view, p.Id, c) : p.LongestRoadLength);
        int maximum = lengths.Values.Max();
        var leaders = lengths.Where(x => x.Value == maximum).Select(x => x.Key).ToArray();
        string holder = leaders.Contains(view.LongestRoadPlayerId) ? view.LongestRoadPlayerId : leaders.Length == 1 ? leaders[0] : null;
        return (holder == view.PlayerId ? 2 : 0) - (view.LongestRoadPlayerId == view.PlayerId ? 2 : 0);
    }

    // 在公开道路图上评估一次建造的最长航线；道路/船只能在自己的建筑处接续。
    private static int ProjectedRouteLength(PlayerView view, string player, Command c)
    {
        var roads = view.Board.Roads.Where(r => r.PlayerId == player).Select(r => r.LocationId).ToHashSet();
        var ships = view.Board.Ships.Where(s => s.PlayerId == player).Select(s => s.LocationId).ToHashSet();
        var anchors = Buildings(view).Where(b => b.PlayerId == player).Select(b => b.LocationId).ToHashSet();
        bool movesKnight = c.Kind == CommandKind.MoveKnight || c.Kind == CommandKind.DisplaceKnight;
        var blocked = Buildings(view).Where(b => b.PlayerId != player).Select(b => b.LocationId)
            .Concat(view.Board.Knights.Where(k => k.PlayerId != player && !(movesKnight && k.LocationId == c.SourceId)
                && !(c.Kind == CommandKind.DisplaceKnight && k.LocationId == c.TargetId)).Select(k => k.LocationId)).ToHashSet();
        if (player == view.PlayerId)
        {
            if (c.Kind == CommandKind.BuildRoad) roads.Add(c.TargetId);
            if (c.Kind == CommandKind.BuildShip) ships.Add(c.TargetId);
            if (c.Kind == CommandKind.BuildSettlement) anchors.Add(c.TargetId);
        }
        else if (c.Kind == CommandKind.BuildSettlement || c.Kind == CommandKind.RecruitKnight || movesKnight) blocked.Add(c.TargetId);
        var edges = view.Board.Edges.Where(e => roads.Contains(e.Id) || ships.Contains(e.Id)).ToArray();
        var adjacent = edges.SelectMany(e => e.Vertices).Distinct().ToDictionary(v => v, v => edges.Where(e => e.Vertices.Contains(v)).ToArray());
        var used = new HashSet<string>();
        int Walk(string vertex, string previous)
        {
            if (previous != null && blocked.Contains(vertex)) return 0;
            int best = 0;
            foreach (var edge in adjacent[vertex])
            {
                if (used.Contains(edge.Id) || previous != null && ships.Contains(previous) != ships.Contains(edge.Id) && !anchors.Contains(vertex)) continue;
                used.Add(edge.Id); best = Math.Max(best, 1 + Walk(edge.Vertices.Single(v => v != vertex), edge.Id)); used.Remove(edge.Id);
            }
            return best;
        }
        return adjacent.Count == 0 ? 0 : adjacent.Keys.Max(v => Walk(v, null));
    }

    private static double CardValue(ProgressCardKind card)
    {
        switch (card)
        {
            case ProgressCardKind.Medicine: case ProgressCardKind.Crane: case ProgressCardKind.Merchant: return 30;
            case ProgressCardKind.RoadBuilding: case ProgressCardKind.Smithing: case ProgressCardKind.ResourceMonopoly: return 20;
            case ProgressCardKind.Invention: case ProgressCardKind.Diplomacy: return 4;
            default: return 12;
        }
    }

    private static double ProgressValue(PlayerView view, Command c)
    {
        double value = CardValue(c.ProgressCard);
        // 商港的发起方可自由挑人，也须遵守交易禁令；被迫交商品的待决仍照规则完成。
        if (c.ProgressCard == ProgressCardKind.CommercialHarbor)
            return view.OwnResources.Total > 0 && view.Players.Any(p => p.Id != view.PlayerId && p.CommodityCount > 0
                && AiOpponentStrategy.TradePenalty(view, p.Id) <= 0) ? value : -1;
        if (c.ProgressCard == ProgressCardKind.Taxation)
        {
            var tile = view.Board.Tiles.FirstOrDefault(t => t.Id == c.TargetId);
            if (tile == null) return -1;
            var victims = AffectedOpponents(view, c).Where(id => view.Players.Any(p => p.Id == id && p.ResourceCount + p.CommodityCount > 0)).ToArray();
            double relief = view.Board.Tiles.Where(t => t.Id == view.Board.RobberTileId)
                .Sum(t => Buildings(view).Where(b => b.PlayerId == view.PlayerId && t.Vertices.Contains(b.LocationId)).Sum(b => Pips(t.Number) * 8.0));
            return TokenValue(view, new Command { Kind = CommandKind.MoveRobber, TargetId = c.TargetId }) + relief + victims.Sum(id => 12 + Threat(view, id) / 4) - 1;
        }
        if (c.ProgressCard == ProgressCardKind.Diplomacy || c.ProgressCard == ProgressCardKind.Intrigue || c.ProgressCard == ProgressCardKind.Sabotage || c.ProgressCard == ProgressCardKind.Wedding)
            value += AffectedOpponents(view, c).Sum(id => Threat(view, id));
        if (c.ProgressCard == ProgressCardKind.Crane) return ImprovementValue(view, c.Track);
        if (c.ProgressCard == ProgressCardKind.ResourceMonopoly) return 10 + (19 - view.Bank[c.Resource] - view.OwnResources[c.Resource]) * 2;
        if (c.ProgressCard == ProgressCardKind.TradeMonopoly) return 10 + (12 - view.CommodityBank[c.Commodity] - view.OwnCommodities[c.Commodity]) * 3;
        if (c.ProgressCard == ProgressCardKind.MerchantFleet) return c.ChooseCommodity ? view.OwnCommodities[c.Commodity] * 2 : view.OwnResources[c.Resource] * 2;
        if (c.ProgressCard == ProgressCardKind.Invention)
        {
            var source = view.Board.Tiles.First(t => t.Id == c.SourceId); var target = view.Board.Tiles.First(t => t.Id == c.TargetId);
            double Ownership(Tile t) => Buildings(view).Where(b => t.Vertices.Contains(b.LocationId)).Sum(b => b.PlayerId == view.PlayerId ? 1.0 : -0.4);
            return (Ownership(source) - Ownership(target)) * (Math.Abs(7 - source.Number.Value) - Math.Abs(7 - target.Number.Value));
        }
        if (c.ProgressCard == ProgressCardKind.Diplomacy && view.Board.Roads.Any(r => r.LocationId == c.TargetId && r.PlayerId == view.PlayerId)) return -1;
        if (c.ProgressCard == ProgressCardKind.Medicine) value += Value(view, c.TargetId);
        if (c.ProgressCard == ProgressCardKind.Merchant && view.MerchantPlayerId != view.PlayerId) value += 30;
        if (c.OtherPlayerId != null) value += Threat(view, c.OtherPlayerId);
        return value;
    }

    private static double PendingValue(PlayerView view, Command c)
    {
        if (c.Decline) return -1000;
        switch (view.PendingDecision?.Kind)
        {
            case "Gold": return Resources.Sum(r => (c.Resources?[r] ?? 0) * 10.0 / (1 + view.OwnResources[r]));
            case "Aqueduct": return 10.0 / (1 + view.OwnResources[c.Resource]);
            case "PillageCity": return -Value(view, c.TargetId);
            case "Metropolis": return Value(view, c.TargetId);
            case "DefenderProgress": return c.Track == ImprovementTrack.Science ? 30 : c.Track == ImprovementTrack.Trade ? 20 : 10;
            case "DisplacedKnight": case "ProgressTreasonPlace": return c.KnightLevel * 10 - Value(view, c.TargetId) + BlockValue(view, new Command { Kind = CommandKind.RecruitKnight, TargetId = c.TargetId });
            case "ProgressEspionage": return CardValue(c.SelectedProgressCard);
            case "ProgressHarbor": return view.OwnCommodities[c.Commodity];
            case "ProgressRoads":
                var edge = view.Board.Edges.FirstOrDefault(e => e.Id == c.TargetId);
                return edge == null ? 0 : edge.Vertices.Max(v => Value(view, v));
            default: return 0;
        }
    }
    private static Command ChooseGeneralSkill(PlayerView view, ResourceBag goal, CommodityBag commodityGoal)
    {
        var selected = ThreeKingdomsAi.Choose(view.ThreeKingdoms,
            Resources.Select(r => view.OwnResources[r]).Concat(Commodities.Select(c => view.OwnCommodities[c])).ToArray(),
            Resources.Select(r => goal[r]).Concat(Commodities.Select(c => commodityGoal[c])).ToArray(),
            id => view.Players.Where(p => p.Id == id).Select(p => p.ResourceCount + p.CommodityCount).FirstOrDefault());
        return selected == null ? null : new Command { PlayerId = view.PlayerId, Kind = CommandKind.UseGeneralSkill,
            SkillCommand = selected, DecisionId = selected.DecisionId, WindowId = view.ThreeKingdoms.WindowId };
    }
    // Detach every mutable command field before returning a projected legal action.
    private static Command Copy(Command c) => new Command
    {
        PlayerId=c.PlayerId, Kind=c.Kind, TargetId=c.TargetId, OtherPlayerId=c.OtherPlayerId,
        ActingAsPlayerId=c.ActingAsPlayerId, JiaoziCardType=c.JiaoziCardType, GeneralIds=c.GeneralIds?.ToArray(), DecisionId=c.DecisionId, WindowId=c.WindowId, SkillCommand=ThreeKingdomsAi.Copy(c.SkillCommand),
        PaymentCards=c.PaymentCards?.ToArray(),GiveEffectiveCardType=c.GiveEffectiveCardType,RetireAtPhase=c.RetireAtPhase,CancelRetirementIntent=c.CancelRetirementIntent,
        DeclaredRealTypes=c.DeclaredRealTypes?.ToArray(),DeclaredEffectiveTypes=c.DeclaredEffectiveTypes?.ToArray(),
        GiveResource=c.GiveResource, ReceiveResource=c.ReceiveResource, GiveAmount=c.GiveAmount, ReceiveAmount=c.ReceiveAmount,
        Give=c.Give?.Copy(), Receive=c.Receive?.Copy(), Resources=c.Resources?.Copy(), Resource=c.Resource,
        SourceId=c.SourceId, Track=c.Track, Commodity=c.Commodity, Commodities=c.Commodities?.Copy(),
        GiveCommodities=c.GiveCommodities?.Copy(), ReceiveCommodities=c.ReceiveCommodities?.Copy(),
        GiveIsCommodity=c.GiveIsCommodity, ReceiveIsCommodity=c.ReceiveIsCommodity, GiveCommodity=c.GiveCommodity,
        ReceiveCommodity=c.ReceiveCommodity, ProgressCard=c.ProgressCard, SelectedProgressCard=c.SelectedProgressCard,
        TargetIds=c.TargetIds?.ToArray(), ChosenDice1=c.ChosenDice1, ChosenDice2=c.ChosenDice2,
        ChooseCommodity=c.ChooseCommodity, Decline=c.Decline, KnightLevel=c.KnightLevel, BuildShip = c.BuildShip,
    };

    private static Command RespondToTrade(PlayerView view)
    {
        if(view.ThreeKingdoms?.HandLossForbidden==true) return new Command {PlayerId=view.PlayerId,Kind=view.TradeOffer.ProposerId==view.PlayerId?CommandKind.CancelTrade:CommandKind.RejectTrade};
        if(view.ThreeKingdoms?.AllowedTradePartnerPlayerIds!=null&&!(view.LegalActions??Array.Empty<Command>()).Any(c=>c.Kind==CommandKind.AcceptTrade))
            return new Command {PlayerId=view.PlayerId,Kind=view.TradeOffer.ProposerId==view.PlayerId?CommandKind.CancelTrade:CommandKind.RejectTrade};
        var offer = view.TradeOffer;
        var gives = offer.GiveCommodities ?? new CommodityBag();
        var costs = offer.ReceiveCommodities ?? new CommodityBag();
        var affordable = Covers(view.OwnResources, offer.Receive) && Commodities.All(c => view.OwnCommodities[c] >= costs[c]);
        double benefit = TradeValue(view.OwnResources, offer.Give, offer.Receive);
        benefit += Commodities.Sum(c => (gives[c] - costs[c]) * (view.OwnImprovements[(int)(c == Commodity.Paper ? ImprovementTrack.Science : c == Commodity.Cloth ? ImprovementTrack.Trade : ImprovementTrack.Politics)] < 5 ? 2.5 : 1.0));
        return new Command { PlayerId = view.PlayerId, Kind = affordable && benefit - AiOpponentStrategy.TradePenalty(view, offer.ProposerId) >= -0.01 ? CommandKind.AcceptTrade : CommandKind.RejectTrade };
    }
    // 先调动闲置船节省两张牌；来源由公开权限给出，移走后仍须保留目标连接。
    private static Command RepositionShip(PlayerView view, (string target, string[] path, bool ship)? expansion)
    {
        if (view.ShipMovedThisTurn || view.LongestRoadPlayerId == view.PlayerId || !expansion.HasValue || !expansion.Value.ship || expansion.Value.path.Length == 0) return null;
        foreach (var targetId in view.LegalShipEdgeIds.Where(id => id == expansion.Value.path[0]))
        {
            var target = view.Board.Edges.Single(e => e.Id == targetId);
            foreach (var sourceId in view.MovableShipEdgeIds)
            {
                bool connected = target.Vertices.Any(vertex =>
                    Buildings(view).Any(b => b.PlayerId == view.PlayerId && b.LocationId == vertex) ||
                    !Buildings(view).Any(b => b.PlayerId != view.PlayerId && b.LocationId == vertex) && !view.Board.Knights.Any(k => k.PlayerId != view.PlayerId && k.LocationId == vertex) &&
                    view.Board.Ships.Any(s => s.PlayerId == view.PlayerId && s.LocationId != sourceId && view.Board.Edges.Single(e => e.Id == s.LocationId).Vertices.Contains(vertex)));
                if (connected) return new Command { PlayerId=view.PlayerId, Kind=CommandKind.MoveShip, SourceId=sourceId, TargetId=targetId };
            }
        }
        return null;
    }
    private static int Rate(PlayerView view, Resource? resource, Commodity? commodity)
    {
        if(view.ThreeKingdoms?.HandLossForbidden==true)return int.MaxValue;
        if(view.ThreeKingdoms?.BankTradeRates?.Length==8)return view.ThreeKingdoms.BankTradeRates[commodity.HasValue?5+(int)commodity.Value:(int)resource.Value];
        var ports = view.Board.Ports.Where(p => Buildings(view).Any(b => b.PlayerId == view.PlayerId && p.Vertices.Contains(b.LocationId))).ToArray();
        if (resource.HasValue && (ports.Any(p => p.Resource == resource.Value) || view.MerchantFleetResources.Contains(resource.Value)
            || view.MerchantPlayerId == view.PlayerId && view.Board.Tiles.Any(t => t.Id == view.MerchantTileId && t.Resource == resource.Value.ToString().ToLowerInvariant()))) return 2;
        if (commodity.HasValue && (view.OwnImprovements[(int)ImprovementTrack.Trade] >= 3 || view.MerchantFleetCommodities.Contains(commodity.Value))) return 2;
        return ports.Any(p => p.Resource == null) ? 3 : 4;
    }
    private static Commodity TrackCommodity(ImprovementTrack track) => track == ImprovementTrack.Science ? Commodity.Paper : track == ImprovementTrack.Trade ? Commodity.Cloth : Commodity.Coin;
    // One voluntary offer per turn. Public receipts survive save/load, so no hidden AI memory is needed.
    private static bool CanReceive(PlayerView view,int type)=>view.ThreeKingdoms?.BlockedGainCardTypes?.Contains(type)!=true;
    private static Command ProposeTrade(PlayerView view, ResourceBag goal)
    {
        if(view.ThreeKingdoms?.HandLossForbidden==true)return null;
        var since = view.Events.LastOrDefault(e => e.Kind == "EndTurn")?.Sequence ?? 0;
        if (view.Events.Any(e => e.Sequence > since && e.PlayerId == view.PlayerId && e.Kind == "ProposeTrade")) return null;
        var needs = Resources.Where(r => view.OwnResources[r] < goal[r] && CanReceive(view,(int)r)).OrderBy(r => view.OwnResources[r] - goal[r]).ToArray();
        var surplus = Resources.Where(r => view.OwnResources[r] > goal[r]).OrderByDescending(r => view.OwnResources[r] - goal[r]).ToArray();
        if (needs.Length == 0 || surplus.Length == 0) return null;
        // 只向确实持有所需资源的安全对象报价；近终局交易须带来足够大的自身收益。
        foreach (var need in needs)
        foreach (var surplusResource in surplus)
        {
            var give = Bag(); give[surplusResource] = 1;
            var receive = Bag(); receive[need] = 1;
            double benefit = TradeValue(view.OwnResources, receive, give) + 2;
            // 自己也只差一分且此报价补齐当前目标时，才重新考虑两三步可赢的对手。
            // 一步可赢和领先六分的禁令仍以无穷大门槛保持绝对优先。
            if (view.OwnVictoryPoints >= view.TargetVictoryPoints - 1
                && Resources.All(r => view.OwnResources[r] + receive[r] - give[r] >= goal[r])) benefit += 16;
            var partner = view.Players.Where(p => p.Id != view.PlayerId && p.ResourceCount > 0
                && (view.ThreeKingdoms?.AllowedTradePartnerPlayerIds==null||view.ThreeKingdoms.AllowedTradePartnerPlayerIds.Contains(p.Id))
                && AiOpponentStrategy.CanSupply(view.OpponentHands, p.Id, need)
                && benefit >= AiOpponentStrategy.TradePenalty(view, p.Id))
                .OrderBy(p => AiOpponentStrategy.TradePenalty(view, p.Id)).ThenBy(p => p.VictoryPoints)
                .ThenByDescending(p => p.ResourceCount).ThenBy(p => p.Id, StringComparer.Ordinal).FirstOrDefault();
            if (partner != null)
                return new Command { PlayerId = view.PlayerId, Kind = CommandKind.ProposeTrade, OtherPlayerId = partner.Id, Give = give, Receive = receive };
        }
        return null;
    }
    private static bool Covers(ResourceBag hand, ResourceBag cost) => Resources.All(r => hand[r] >= cost[r]);
    private static PiecePlacement[] Buildings(PlayerView view) => view.Board.Settlements.Concat(view.Board.Cities).ToArray();
    private static double Value(PlayerView view, string vertex)
    {
        var owned = Buildings(view).Where(b => b.PlayerId == view.PlayerId).Select(b => b.LocationId).ToArray();
        return view.Board.Tiles.Where(t => t.Vertices.Contains(vertex) && t.Number.HasValue).Sum(t =>
        {
            var pips = 6 - Math.Abs(7 - t.Number!.Value);
            var already = view.Board.Tiles.Count(other => other.Resource == t.Resource && other.Vertices.Intersect(owned).Any());
            return pips * (t.Resource == "wool" ? 1.5 : t.Resource == "wood" ? 1.4 : t.Resource is "ore" or "wheat" ? 1.15 : 1.0) + (already == 0 ? 3 : 0);
        });
    }

    private static double SetupRouteValue(PlayerView view, string edgeId)
    {
        var edge = view.Board.Edges.Single(e => e.Id == edgeId);
        var anchor = view.PendingDecision?.AnchorVertexId;
        var frontier = edge.Vertices.FirstOrDefault(v => v != anchor) ?? edge.Vertices[0];
        // 港边起航也考虑方向：优先面向公开的新地区，而非按边编号随意选择。
        var foreign = view.Board.Vertices.Where(v => SettlementPoints(view, v.Id) > 1).ToArray();
        if (foreign.Length == 0) return Value(view, frontier);
        var vertex = view.Board.Vertices.Single(v => v.Id == frontier);
        return -foreign.Min(v => Math.Abs(v.X - vertex.X) + Math.Abs(v.Y - vertex.Y)) + Value(view, frontier) / 8;
    }

    private static (string target, string[] path, bool ship)? Expansion(PlayerView view)
    {
        var buildings = Buildings(view);
        var me = view.Players.Single(p => p.Id == view.PlayerId);
        var ownRoads = view.Board.Roads.Where(r => r.PlayerId == view.PlayerId).Select(r => r.LocationId).ToHashSet();
        var ownShips = view.Board.Ships.Where(r => r.PlayerId == view.PlayerId).Select(r => r.LocationId).ToHashSet();
        var occupiedEdges = view.Board.Roads.Select(r=>r.LocationId).Concat(view.Board.Ships.Select(s=>s.LocationId)).ToHashSet();
        var enemyVertices = buildings.Where(b => b.PlayerId != view.PlayerId).Select(b => b.LocationId)
            .Concat(view.Board.Knights.Where(k => k.PlayerId != view.PlayerId).Select(k => k.LocationId)).ToHashSet();
        var ownBuildings=buildings.Where(b=>b.PlayerId==view.PlayerId).Select(b=>b.LocationId).ToHashSet();
        var paths = new Dictionary<(string vertex,bool ship),string[]>();
        foreach(var edge in view.Board.Edges)
            foreach(var vertex in edge.Vertices.Where(v=>!enemyVertices.Contains(v)))
            {
                if(ownRoads.Contains(edge.Id)) paths[(vertex,false)]=Array.Empty<string>();
                if(ownShips.Contains(edge.Id)) paths[(vertex,true)]=Array.Empty<string>();
            }
        foreach(var vertex in ownBuildings){paths[(vertex,false)]=Array.Empty<string>();paths[(vertex,true)]=Array.Empty<string>();}
        var pending=paths.Keys.ToList();
        while (pending.Count > 0)
        {
            var v = pending.OrderBy(v => paths[v].Length).First(); pending.Remove(v);
            foreach (var edge in view.Board.Edges.Where(e => e.Vertices.Contains(v.vertex)))
            {
                var next = edge.Vertices.First(x => x != v.vertex);
                if (enemyVertices.Contains(next)) continue;
                foreach(bool ship in new[]{false,true})
                {
                    if(ship!=v.ship&&!ownBuildings.Contains(v.vertex))continue;
                    var own=ship?ownShips:ownRoads;
                    if(occupiedEdges.Contains(edge.Id)&&!own.Contains(edge.Id))continue;
                    var tiles=view.Board.Tiles.Where(t=>edge.Vertices.All(t.Vertices.Contains)).ToArray();
                    if(ship ? !tiles.Any(t=>t.Resource=="sea")&&tiles.Length!=1 : !tiles.Any(t=>t.Resource!="sea"))continue;
                    if(ship&&tiles.Any(t=>t.Id==view.Board.PirateTileId))continue;
                    var path=own.Contains(edge.Id)?paths[v]:paths[v].Append((ship?"S:":"R:")+edge.Id).ToArray();
                    var key=(next,ship);
                    if(path.Length>6||(paths.TryGetValue(key,out var old)&&old.Length<=path.Length))continue;
                    paths[key]=path;if(!pending.Contains(key))pending.Add(key);
                }
            }
        }
        var targets = view.Board.Vertices.Where(v => !buildings.Any(b => b.LocationId == v.Id) && !view.Board.Knights.Any(k => k.LocationId == v.Id)
            && !view.Board.Edges.Where(e => e.Vertices.Contains(v.Id)).Any(e => buildings.Any(b => e.Vertices.Contains(b.LocationId)))
            && view.Board.Tiles.Any(t=>t.Resource!="sea"&&t.Vertices.Contains(v.Id)))
            .SelectMany(v=>paths.Where(p=>p.Key.vertex==v.Id && p.Value.Count(x => x.StartsWith("S:")) <= me.ShipsRemaining && p.Value.Count(x => x.StartsWith("R:")) <= me.Pieces.Roads)
                .Select(p=>(target:v.Id,path:p.Value.Select(x=>x.Substring(2)).ToArray(),ship:p.Value.FirstOrDefault()?.StartsWith("S:")==true))).ToArray();
        if (targets.Length == 0) return null;
        return targets.OrderByDescending(t => SettlementValue(view, t.target, t.path.Length)).ThenBy(t => t.path.Length).ThenBy(t => t.target, StringComparer.Ordinal).First();
    }
}


}
