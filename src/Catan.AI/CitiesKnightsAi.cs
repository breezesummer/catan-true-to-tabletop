using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core;
using Catan.Core.M4;
using Command = Catan.Core.M4.Command;
using CommandKind = Catan.Core.M4.CommandKind;
using GamePhase = Catan.Core.M4.GamePhase;
using PlayerView = Catan.Core.M4.PlayerView;
using static Catan.AI.AiHeuristics;

namespace Catan.AI
{
/// <summary>仅根据当前玩家授权视图评估得分速度、生产回报和保城成本。</summary>
public sealed class CitiesKnightsAi
{
    private static readonly Resource[] Resources = (Resource[])Enum.GetValues(typeof(Resource));
    private static readonly Commodity[] Commodities = (Commodity[])Enum.GetValues(typeof(Commodity));
    private static ResourceBag Bag(int wood = 0, int brick = 0, int wool = 0, int wheat = 0, int ore = 0) => new() { Wood = wood, Brick = brick, Wool = wool, Wheat = wheat, Ore = ore };

    /// <summary>Choose one command. The caller supplies its unique command ID and executes it through the rule session.</summary>
    public Command Decide(PlayerView view)
    {
        if (view == null) throw new ArgumentNullException(nameof(view));
        if (view.Phase == GamePhase.Finished) throw new InvalidOperationException("The game has finished.");
        var actor = view.TradeOffer != null ? view.TradeOffer.NextResponder(view.PlayerId) : view.PendingDecision?.PlayerId ?? view.ActivePlayerId;
        var canDiscard = view.Phase == GamePhase.Discard && view.Discards.Any(d => d.PlayerId == view.PlayerId);
        if (view.Phase == GamePhase.Discard ? !canDiscard : actor != view.PlayerId) throw new InvalidOperationException("This seat has no pending action.");
        return Copy(Choose(view));
    }
    private Command Choose(PlayerView view)
    {
        Command Make(CommandKind kind, string target = null) => new() { PlayerId = view.PlayerId, Kind = kind, TargetId = target! };
        if (view.TradeOffer != null) return RespondToTrade(view);
        var legal = (view.LegalActions ?? Array.Empty<Command>()).ToList();
        if (view.Phase == GamePhase.SetupSettlement)
            return Make(CommandKind.SetupSettlement, view.LegalVertexIds.OrderByDescending(v => SetupValue(view, v)).ThenBy(v => v).First());
        if (view.Phase == GamePhase.SetupRoad)
            return Make(CommandKind.SetupRoad, view.LegalEdgeIds.OrderByDescending(id => view.Board.Edges.Single(e => e.Id == id).Vertices.Max(v => Value(view, v))).First());
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
            var goal = ResourceGoal(view, Expansion(view)); var discardCommodityGoal = CommodityGoal(view);
            var amount = view.Discards.Single(d => d.PlayerId == view.PlayerId).Amount;
            while (amount-- > 0)
            {
                var resource = Resources.Where(r => view.OwnResources[r] > discard.Resources[r]).OrderByDescending(r => view.OwnResources[r] - discard.Resources[r] - goal[r] * 3).Cast<Resource?>().FirstOrDefault();
                var commodity = Commodities.Where(c => view.OwnCommodities[c] > discard.Commodities[c]).OrderByDescending(c => view.OwnCommodities[c] - discard.Commodities[c] - discardCommodityGoal[c] * 3).Cast<Commodity?>().FirstOrDefault();
                if (resource.HasValue && (!commodity.HasValue || view.OwnResources[resource.Value] - discard.Resources[resource.Value] - goal[resource.Value] * 3 >= view.OwnCommodities[commodity.Value] - discard.Commodities[commodity.Value] - discardCommodityGoal[commodity.Value] * 3)) discard.Resources[resource.Value]++;
                else discard.Commodities[commodity.Value]++;
            }
            return discard;
        }
        if (view.Phase == GamePhase.RobberMove)
            return Make(CommandKind.MoveRobber, view.Board.Tiles.Where(t => t.Id != view.Board.RobberTileId).OrderByDescending(t => RobberValue(view, t)).ThenBy(t => t.Id).First().Id);
        if (view.Phase == GamePhase.RobberSteal)
        {
            var theft = Make(CommandKind.StealResource); theft.OtherPlayerId = view.PendingDecision.EligibleVictimIds.OrderByDescending(id => Threat(view, id)).ThenBy(id => id).First(); return theft;
        }
        if (view.Phase == GamePhase.PendingChoice && view.PendingDecision?.Kind == "ProgressDiscard")
        {
            var discard = Make(CommandKind.DiscardProgressCard); discard.ProgressCard = view.OwnProgressCards.OrderBy(CardValue).First(); return discard;
        }
        if (view.Phase != GamePhase.Action)
        {
            if (legal.Count == 0) throw new InvalidOperationException($"No public legal actions for {view.PlayerId}/{view.Phase}/{view.PendingDecision?.Kind}");
            return legal.OrderByDescending(c => PendingValue(view, c)).First();
        }
        legal.AddRange(view.LegalCityVertexIds.Select(v => Make(CommandKind.BuildCity, v)));
        legal.AddRange(view.LegalVertexIds.Select(v => Make(CommandKind.BuildSettlement, v)));
        legal.AddRange(view.LegalEdgeIds.Select(v => Make(CommandKind.BuildRoad, v)));

        // 先结算能立即结束比赛的公开确定性得分，包括两阶段的大都会归属选择。
        if (view.OwnVictoryPoints >= view.TargetVictoryPoints) return Make(CommandKind.DeclareVictory);
        var winning = legal.Where(c => view.OwnVictoryPoints + PointGain(view, c) >= view.TargetVictoryPoints)
            .OrderByDescending(c => PointGain(view, c)).ThenBy(c => c.Kind).FirstOrDefault();
        if (winning != null) return winning;
        // 对手即将终局时，先执行确有阻碍收益的蓝牌或截路；自己的立即胜利仍排在最前。
        var urgentTactic = legal.Where(c => UrgentInterference(view, c))
            .OrderByDescending(c => c.Kind == CommandKind.PlayProgressCard ? ProgressValue(view, c) : BlockValue(view, c))
            .ThenBy(c => c.TargetId, StringComparer.Ordinal).FirstOrDefault();
        if (urgentTactic != null) return urgentTactic;
        var defense = DefenseAction(view, legal, DefenseTarget(view));
        if (defense != null) return defense;
        var improvement = legal.Where(c => c.Kind == CommandKind.ImproveCity || c.Kind == CommandKind.PlayProgressCard && c.ProgressCard == ProgressCardKind.Crane)
            .Where(c => ImprovementValue(view, c.Track) > 0)
            .OrderByDescending(c => ImprovementValue(view, c.Track) + (c.Kind == CommandKind.PlayProgressCard ? 3 : 0)).FirstOrDefault();
        if (improvement != null && ImprovementValue(view, improvement.Track) >= 60) return improvement;
        var medicine = legal.Where(c => c.Kind == CommandKind.PlayProgressCard && c.ProgressCard == ProgressCardKind.Medicine)
            .OrderByDescending(c => Value(view, c.TargetId)).FirstOrDefault();
        if (medicine != null) return medicine;
        var city = legal.Where(c => c.Kind == CommandKind.BuildCity).OrderByDescending(c => Value(view, c.TargetId)).FirstOrDefault();
        if (city != null) return city;
        var settlement = legal.Where(c => c.Kind == CommandKind.BuildSettlement).OrderByDescending(c => Value(view, c.TargetId)).FirstOrDefault();
        if (settlement != null) return settlement;
        if (improvement != null) return improvement;
        var defender = DefenseAction(view, legal, DefenderTarget(view));
        if (defender != null) return defender;
        var blockade = legal.Where(c => KnightTacticValue(view, c) > 0)
            .OrderByDescending(c => KnightTacticValue(view, c)).ThenBy(c => c.TargetId, StringComparer.Ordinal).FirstOrDefault();
        if (blockade != null) return blockade;
        var card = legal.Where(c => c.Kind == CommandKind.PlayProgressCard && ProgressValue(view, c) > 0).OrderByDescending(c => ProgressValue(view, c)).FirstOrDefault();
        if (card != null) return card;
        var harbor = legal.Where(c => c.Kind == CommandKind.CommercialHarborOffer
            && view.Players.Any(p => p.Id == c.OtherPlayerId && p.CommodityCount > 0)
            && AiOpponentStrategy.TradePenalty(view, c.OtherPlayerId) <= 0).OrderByDescending(c => view.OwnResources[c.Resource]).FirstOrDefault();
        if (harbor != null) return harbor;

        var chase = legal.FirstOrDefault(c => c.Kind == CommandKind.ChaseRobber);
        if (chase != null && (view.BarbarianPosition < 4 || view.OwnResources.Wheat > 0)
            && view.Board.Tiles.Any(t => t.Id == view.Board.RobberTileId && Buildings(view).Any(b => b.PlayerId == view.PlayerId && t.Vertices.Contains(b.LocationId)))) return chase;
        var wall = legal.FirstOrDefault(c => c.Kind == CommandKind.BuildWall);
        if (wall != null && view.OwnResources.Brick >= 3 && view.OwnResources.Total + view.OwnCommodities.Total >= 7) return wall;
        var expansion = Expansion(view);
        var desired = ResourceGoal(view, expansion);
        var commodityGoal = CommodityGoal(view);
        if (expansion.HasValue && expansion.Value.path.Length > 0 && desired.Wood > 0 && desired.Brick > 0 && Covers(view.OwnResources, Bag(1, 1)))
        {
            var road = legal.FirstOrDefault(c => c.Kind == CommandKind.BuildRoad && c.TargetId == expansion.Value.path[0]);
            if (road != null) return road;
        }
        var proposal = ProposeTrade(view, desired); if (proposal != null) return proposal;
        foreach (var need in Resources.Where(r => view.OwnResources[r] < desired[r] && view.Bank[r] > 0).Select(r => (resource: r, commodity: (Commodity?)null))
            .Concat(Commodities.Where(c => view.OwnCommodities[c] < commodityGoal[c] && view.CommodityBank[c] > 0).Select(c => (resource: default(Resource), commodity: (Commodity?)c))))
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
        if (view.OwnProgressCards.Length > 4) { var discard = Make(CommandKind.DiscardProgressCard); discard.ProgressCard = view.OwnProgressCards.OrderBy(CardValue).First(); return discard; }
        return Make(CommandKind.EndTurn);
    }

    private static int Strength(PlayerView view, string player) => view.Board.Knights.Where(k => k.PlayerId == player && k.Active).Sum(k => k.Level);
    private static bool Vulnerable(PlayerView view, string player) => view.Board.Cities.Any(c => c.PlayerId == player && !view.Board.Metropolises.Any(m => m.LocationId == c.LocationId));

    // 保住城市只需要达到全岛防守线，或严格高于另一位易受劫玩家；不为无收益军备重复付费。
    private static int DefenseTarget(PlayerView view)
    {
        if (!Vulnerable(view, view.PlayerId)) return 0;
        int otherStrength = view.Players.Where(p => p.Id != view.PlayerId).Sum(p => Strength(view, p.Id));
        int collective = Math.Max(0, view.Board.Cities.Length - otherStrength);
        var rivals = view.Players.Where(p => p.Id != view.PlayerId && Vulnerable(view, p.Id)).ToArray();
        int minimum = rivals.Length == 0 ? collective : Math.Min(collective, rivals.Min(p => Strength(view, p.Id)) + 1);
        return view.FirstBarbarianAttack ? minimum : Math.Max(1, minimum);
    }

    private static int DefenderTarget(PlayerView view)
    {
        int active = Strength(view, view.PlayerId);
        if (view.BarbarianPosition < 4 || active == 0) return active;
        int strongest = view.Players.Where(p => p.Id != view.PlayerId).Max(p => Strength(view, p.Id));
        int target = Math.Max(strongest + 1, view.Board.Cities.Length - view.Players.Where(p => p.Id != view.PlayerId).Sum(p => Strength(view, p.Id)));
        // 最多多投入一次晋升，或者激活现有部队。激烈争夺时继续争经济得分。
        int inherent = view.Board.Knights.Where(k => k.PlayerId == view.PlayerId).Sum(k => k.Level);
        return target <= active + 1 || target <= inherent ? Math.Max(active, target) : active;
    }

    private static Command DefenseAction(PlayerView view, List<Command> legal, int target)
    {
        int active = Strength(view, view.PlayerId);
        if (active >= target) return null;
        var own = view.Board.Knights.Where(k => k.PlayerId == view.PlayerId).ToArray();
        var activate = legal.Where(c => c.Kind == CommandKind.ActivateKnight)
            .OrderBy(c => { int level = own.Single(k => k.LocationId == c.TargetId).Level; return level >= target - active ? level : 10 - level; }).FirstOrDefault();
        if (activate != null) return activate;
        if (view.BarbarianPosition < 5 && own.Sum(k => k.Level) >= target) return null;
        var smith = legal.Where(c => c.Kind == CommandKind.PlayProgressCard && c.ProgressCard == ProgressCardKind.Smithing)
            .OrderByDescending(c => (c.TargetIds ?? Array.Empty<string>()).Count(id => own.Any(k => k.LocationId == id && k.Active))).FirstOrDefault();
        if (smith != null && (smith.TargetIds ?? Array.Empty<string>()).Any(id => own.Any(k => k.LocationId == id && k.Active))) return smith;
        var promote = legal.Where(c => c.Kind == CommandKind.PromoteKnight && own.Any(k => k.LocationId == c.TargetId && k.Active)).FirstOrDefault();
        if (promote != null) return promote;
        if (own.Sum(k => k.Level) >= target) return null; // 缺麦时先筹备激活，避免继续堆闲置骑士。
        promote = legal.FirstOrDefault(c => c.Kind == CommandKind.PromoteKnight);
        if (promote != null) return promote;
        return legal.Where(c => c.Kind == CommandKind.RecruitKnight).OrderByDescending(c => BlockValue(view, c) - Value(view, c.TargetId)).ThenBy(c => c.TargetId).FirstOrDefault();
    }

    private static bool GainsMetropolis(PlayerView view, ImprovementTrack track)
    {
        int next = view.OwnImprovements[(int)track] + 1;
        var holder = view.Board.Metropolises.FirstOrDefault(m => m.Track == track);
        return next >= 4 && next <= 5 && (holder == null || holder.PlayerId != view.PlayerId && view.Players.Single(p => p.Id == holder.PlayerId).Improvements[(int)track] < next)
            && view.Board.Cities.Any(c => c.PlayerId == view.PlayerId && !view.Board.Metropolises.Any(m => m.LocationId == c.LocationId));
    }

    private static double ImprovementValue(PlayerView view, ImprovementTrack track)
    {
        int level = view.OwnImprovements[(int)track];
        if (level >= 5 || !view.Board.Cities.Any(c => c.PlayerId == view.PlayerId)) return -1;
        var holder = view.Board.Metropolises.FirstOrDefault(m => m.Track == track);
        if (GainsMetropolis(view, track)) return 90 + (holder == null ? 0 : Threat(view, holder.PlayerId));
        if (level >= 3)
        {
            if (holder?.PlayerId == view.PlayerId) return view.Players.Any(p => p.Id != view.PlayerId && p.Improvements[(int)track] >= 3) ? 45 : 8;
            if (!view.Board.Cities.Any(c => c.PlayerId == view.PlayerId && !view.Board.Metropolises.Any(m => m.LocationId == c.LocationId))) return -1;
            if (holder != null && view.Players.Single(p => p.Id == holder.PlayerId).Improvements[(int)track] == 5) return -1;
            return 32; // 对方四级大都会可在下一次五级升级时争取。
        }
        int rolls = RemainingRolls(view.Turn, view.Players.Max(p => p.VictoryPoints), view.TargetVictoryPoints);
        bool early = rolls >= 30;
        if (track == ImprovementTrack.Science) return early ? 60 + level * 4 : 24 + level * 5;
        if (track == ImprovementTrack.Trade) return early ? 28 + level * 3 : 40 + level * 4;
        bool knightPlan = view.Board.Knights.Where(k => k.PlayerId == view.PlayerId).Sum(k => k.Level) >= 3
            && view.Board.Tiles.Any(t => t.Resource == "ore" && Buildings(view).Any(b => b.PlayerId == view.PlayerId && t.Vertices.Contains(b.LocationId)));
        return knightPlan ? 26 + level * 3 : 4;
    }

    private static int PointGain(PlayerView view, Command c)
    {
        int points = c.Kind == CommandKind.BuildCity || c.Kind == CommandKind.BuildSettlement ? 1 : 0;
        if (c.Kind == CommandKind.ImproveCity || c.Kind == CommandKind.PlayProgressCard && c.ProgressCard == ProgressCardKind.Crane)
            return GainsMetropolis(view, c.Track) ? 2 : 0;
        if (c.Kind == CommandKind.PlayProgressCard && c.ProgressCard == ProgressCardKind.Medicine) return 1;
        if (c.Kind == CommandKind.PlayProgressCard && c.ProgressCard == ProgressCardKind.Merchant) return view.MerchantPlayerId == view.PlayerId ? 0 : 1;
        if (view.OwnVictoryPoints >= view.TargetVictoryPoints - 3 && (c.Kind == CommandKind.BuildRoad || c.Kind == CommandKind.BuildSettlement || c.Kind == CommandKind.RecruitKnight || c.Kind == CommandKind.MoveKnight || c.Kind == CommandKind.DisplaceKnight))
        {
            var lengths = view.Players.ToDictionary(p => p.Id, p => RoadLength(view, p.Id, c));
            int maximum = lengths.Values.Max();
            var leaders = lengths.Where(p => p.Value == maximum).Select(p => p.Key).ToArray();
            string holder = maximum < 5 ? null : leaders.Contains(view.LongestRoadPlayerId) ? view.LongestRoadPlayerId : leaders.Length == 1 ? leaders[0] : null;
            points += (holder == view.PlayerId ? 2 : 0) - (view.LongestRoadPlayerId == view.PlayerId ? 2 : 0);
        }
        return points;
    }

    private static int RoadLength(PlayerView view, string player, Command command)
    {
        var ids = view.Board.Roads.Where(r => r.PlayerId == player).Select(r => r.LocationId).ToHashSet();
        if (player == view.PlayerId && command.Kind == CommandKind.BuildRoad) ids.Add(command.TargetId);
        var edges = view.Board.Edges.Where(e => ids.Contains(e.Id)).ToArray();
        bool movesKnight = command.Kind == CommandKind.MoveKnight || command.Kind == CommandKind.DisplaceKnight;
        var blocked = Buildings(view).Where(b => b.PlayerId != player).Select(b => b.LocationId)
            .Concat(view.Board.Knights.Where(k => k.PlayerId != player && !(movesKnight && k.LocationId == command.SourceId)
                && !(command.Kind == CommandKind.DisplaceKnight && k.LocationId == command.TargetId)).Select(k => k.LocationId)).ToHashSet();
        if (player != view.PlayerId && (command.Kind == CommandKind.BuildSettlement || command.Kind == CommandKind.RecruitKnight || movesKnight)) blocked.Add(command.TargetId);
        int Walk(string vertex, HashSet<string> used)
        {
            if (used.Count > 0 && blocked.Contains(vertex)) return used.Count;
            int best = used.Count;
            foreach (var edge in edges.Where(e => e.Vertices.Contains(vertex) && !used.Contains(e.Id)))
            {
                used.Add(edge.Id); best = Math.Max(best, Walk(edge.Vertices.First(v => v != vertex), used)); used.Remove(edge.Id);
            }
            return best;
        }
        return edges.SelectMany(e => e.Vertices).Distinct().Select(v => Walk(v, new HashSet<string>())).DefaultIfEmpty(0).Max();
    }

    private static double KnightTacticValue(PlayerView view, Command c)
    {
        if (c.Kind != CommandKind.ActivateKnight) return BlockValue(view, c);
        var knight = view.Board.Knights.FirstOrDefault(k => k.PlayerId == view.PlayerId && k.LocationId == c.TargetId);
        if (knight == null) return 0;
        // 本回合激活只能为下回合准备，不把尚不能行动的骑士算成立即截路。
        var routes = view.Board.Roads.Where(r => r.PlayerId == view.PlayerId).Select(r => r.LocationId).ToHashSet();
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
                int routes = view.Board.Roads.Count(r => r.PlayerId == rival.Id && adjacent.Contains(r.LocationId));
                if (routes == 0) return 0;
                bool frontier = view.Board.Edges.Any(e => e.Vertices.Contains(vertex) && !view.Board.Roads.Any(r => r.LocationId == e.Id));
                bool site = view.Board.Tiles.Any(t => t.Resource != "sea" && t.Resource != "fog" && t.Vertices.Contains(vertex))
                    && !view.Board.Edges.Where(e => e.Vertices.Contains(vertex)).Any(e => Buildings(view).Any(b => e.Vertices.Contains(b.LocationId)));
                double value = frontier ? 6 + Threat(view, rival.Id) / 8 : 0;
                if (site) value += 8;
                return value;
            }
            double target = At(c.TargetId) - (moves ? At(c.SourceId) : 0);
            int projected = RoadLength(view, rival.Id, c);
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
            if (remaining < DefenseTarget(view)) benefit -= 45;
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
            RoadLength(view, p.Id, c) < p.LongestRoadLength ||
            view.Board.Edges.Any(e => e.Vertices.Contains(c.TargetId) && view.Board.Roads.Any(r => r.PlayerId == p.Id && r.LocationId == e.Id)));
    }

    private static ResourceBag ResourceGoal(PlayerView view, (string target, string[] path)? expansion)
    {
        var own = view.Board.Knights.Where(k => k.PlayerId == view.PlayerId).ToArray();
        int defense = DefenseTarget(view);
        if (Strength(view, view.PlayerId) < defense)
        {
            if (own.Sum(k => k.Level) >= defense) return Bag(wheat: 1);
            return Bag(wool: 1, ore: 1, wheat: own.Any(k => k.Active) ? 0 : 1);
        }
        var me = view.Players.Single(p => p.Id == view.PlayerId);
        if (me.Pieces.Cities > 0 && view.Board.Settlements.Any(b => b.PlayerId == view.PlayerId)
            && (view.OwnResources.Ore >= 2 || expansion == null || me.Pieces.Settlements == 0))
            return view.OwnProgressCards.Contains(ProgressCardKind.Medicine) ? Bag(wheat: 1, ore: 2) : Bag(wheat: 2, ore: 3);
        if (me.Pieces.Settlements > 0 && expansion != null) return Bag(1, 1, 1, 1);
        return !Buildings(view).Any(b => b.PlayerId == view.PlayerId) ? Bag(1, 1, 1, 1) : Bag();
    }

    private static CommodityBag CommodityGoal(PlayerView view)
    {
        var result = new CommodityBag();
        var tracks = new[] { ImprovementTrack.Science, ImprovementTrack.Trade, ImprovementTrack.Politics }
            .Where(t => ImprovementValue(view, t) >= 20).OrderByDescending(t => ImprovementValue(view, t)).ToArray();
        foreach (var track in tracks)
        {
            int level = view.OwnImprovements[(int)track];
            if (track == tracks[0] || track == ImprovementTrack.Science && level < 3 || level >= 3)
                result[TrackCommodity(track)] = level + 1;
        }
        return result;
    }

    private static double Threat(PlayerView view, string player) => AiOpponentStrategy.Threat(view, player);

    private static double RobberValue(PlayerView view, Tile tile)
    {
        double result = 0;
        foreach (var b in Buildings(view).Where(b => tile.Vertices.Contains(b.LocationId)))
        {
            double production = Pips(tile.Number) * (view.Board.Cities.Any(c => c.LocationId == b.LocationId) ? 2 : 1);
            if (b.PlayerId == view.PlayerId) { result -= 80 + production * 8; continue; }
            bool unique = !view.Board.Tiles.Any(t => t.Id != tile.Id && t.Resource == tile.Resource && Buildings(view).Any(other => other.PlayerId == b.PlayerId && t.Vertices.Contains(other.LocationId)));
            result += Threat(view, b.PlayerId) + production * (unique ? 4 : 2);
        }
        return result;
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
        // 商业港口的发起方可自由挑人，也须遵守交易禁令；被迫交商品的待决仍照规则完成。
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
            return RobberValue(view, tile) + relief + victims.Sum(id => 12 + Threat(view, id) / 4) - 1;
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
            case "Aqueduct": return Math.Max(0, ResourceGoal(view, Expansion(view))[c.Resource] - view.OwnResources[c.Resource]) * 20 + 10.0 / (1 + view.OwnResources[c.Resource]);
            case "PillageCity": return -Value(view, c.TargetId);
            case "Metropolis": return Value(view, c.TargetId);
            case "DefenderProgress": return ImprovementValue(view, c.Track);
            case "DisplacedKnight": case "ProgressTreasonPlace": return c.KnightLevel * 10 - Value(view, c.TargetId) + BlockValue(view, new Command { Kind = CommandKind.RecruitKnight, TargetId = c.TargetId });
            case "ProgressEspionage": return CardValue(c.SelectedProgressCard);
            case "ProgressHarbor": return view.OwnCommodities[c.Commodity];
            case "ProgressRoads":
                var edge = view.Board.Edges.FirstOrDefault(e => e.Id == c.TargetId);
                return edge == null ? 0 : edge.Vertices.Max(v => Value(view, v));
            default: return 0;
        }
    }
    // Detach every mutable command field before returning a projected legal action.
    private static Command Copy(Command c) => new Command
    {
        PlayerId=c.PlayerId, Kind=c.Kind, TargetId=c.TargetId, OtherPlayerId=c.OtherPlayerId,
        GiveResource=c.GiveResource, ReceiveResource=c.ReceiveResource, GiveAmount=c.GiveAmount, ReceiveAmount=c.ReceiveAmount,
        Give=c.Give?.Copy(), Receive=c.Receive?.Copy(), Resources=c.Resources?.Copy(), Resource=c.Resource,
        SourceId=c.SourceId, Track=c.Track, Commodity=c.Commodity, Commodities=c.Commodities?.Copy(),
        GiveCommodities=c.GiveCommodities?.Copy(), ReceiveCommodities=c.ReceiveCommodities?.Copy(),
        GiveIsCommodity=c.GiveIsCommodity, ReceiveIsCommodity=c.ReceiveIsCommodity, GiveCommodity=c.GiveCommodity,
        ReceiveCommodity=c.ReceiveCommodity, ProgressCard=c.ProgressCard, SelectedProgressCard=c.SelectedProgressCard,
        TargetIds=c.TargetIds?.ToArray(), ChosenDice1=c.ChosenDice1, ChosenDice2=c.ChosenDice2,
        ChooseCommodity=c.ChooseCommodity, Decline=c.Decline, KnightLevel=c.KnightLevel,
    };

    private static Command RespondToTrade(PlayerView view)
    {
        var offer = view.TradeOffer;
        var gives = offer.GiveCommodities ?? new CommodityBag();
        var costs = offer.ReceiveCommodities ?? new CommodityBag();
        var affordable = Covers(view.OwnResources, offer.Receive) && Commodities.All(c => view.OwnCommodities[c] >= costs[c]);
        double benefit = TradeValue(view.OwnResources, offer.Give, offer.Receive);
        benefit += Commodities.Sum(c => (gives[c] - costs[c]) * (view.OwnImprovements[(int)(c == Commodity.Paper ? ImprovementTrack.Science : c == Commodity.Cloth ? ImprovementTrack.Trade : ImprovementTrack.Politics)] < 5 ? 2.5 : 1.0));
        var goal = ResourceGoal(view, Expansion(view)); var commodityGoal = CommodityGoal(view);
        // 不交出将要完成建设或激活骑士的最后一张关键牌。
        benefit += Resources.Sum(r => (Math.Min(goal[r], view.OwnResources[r] + offer.Give[r] - offer.Receive[r]) - Math.Min(goal[r], view.OwnResources[r])) * 3.0);
        benefit += Commodities.Sum(c => (Math.Min(commodityGoal[c], view.OwnCommodities[c] + gives[c] - costs[c]) - Math.Min(commodityGoal[c], view.OwnCommodities[c])) * 2.0);

        return new Command { PlayerId = view.PlayerId, Kind = affordable && benefit - AiOpponentStrategy.TradePenalty(view, offer.ProposerId) >= -0.01 ? CommandKind.AcceptTrade : CommandKind.RejectTrade };
    }
    private static int Rate(PlayerView view, Resource? resource, Commodity? commodity)
    {
        var ports = view.Board.Ports.Where(p => Buildings(view).Any(b => b.PlayerId == view.PlayerId && p.Vertices.Contains(b.LocationId))).ToArray();
        if (resource.HasValue && (ports.Any(p => p.Resource == resource.Value) || view.MerchantFleetResources.Contains(resource.Value)
            || view.MerchantPlayerId == view.PlayerId && view.Board.Tiles.Any(t => t.Id == view.MerchantTileId && t.Resource == resource.Value.ToString().ToLowerInvariant()))) return 2;
        if (commodity.HasValue && (view.OwnImprovements[(int)ImprovementTrack.Trade] >= 3 || view.MerchantFleetCommodities.Contains(commodity.Value))) return 2;
        return ports.Any(p => p.Resource == null) ? 3 : 4;
    }
    private static Commodity TrackCommodity(ImprovementTrack track) => track == ImprovementTrack.Science ? Commodity.Paper : track == ImprovementTrack.Trade ? Commodity.Cloth : Commodity.Coin;
    // One voluntary offer per turn. Public receipts survive save/load, so no hidden AI memory is needed.
    private static Command ProposeTrade(PlayerView view, ResourceBag goal)
    {
        var since = view.Events.LastOrDefault(e => e.Kind == "EndTurn")?.Sequence ?? 0;
        if (view.Events.Any(e => e.Sequence > since && e.PlayerId == view.PlayerId && e.Kind == "ProposeTrade")) return null;
        var needs = Resources.Where(r => view.OwnResources[r] < goal[r]).OrderBy(r => view.OwnResources[r] - goal[r]).ToArray();
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
            return pips * (t.Resource is "ore" or "wheat" ? 1.4 : 1.0) + (already == 0 ? 3 : 0);
        });
    }

    private static (string target, string[] path)? Expansion(PlayerView view)
    {
        var buildings = Buildings(view);
        var ownRoads = view.Board.Roads.Where(r => r.PlayerId == view.PlayerId).Select(r => r.LocationId).ToHashSet();
        var otherRoads = view.Board.Roads.Where(r => r.PlayerId != view.PlayerId).Select(r => r.LocationId).ToHashSet();
        var enemyVertices = buildings.Where(b => b.PlayerId != view.PlayerId).Select(b => b.LocationId)
            .Concat(view.Board.Knights.Where(k => k.PlayerId != view.PlayerId).Select(k => k.LocationId)).ToHashSet();
        var starts = view.Board.Edges.Where(e => ownRoads.Contains(e.Id)).SelectMany(e => e.Vertices)
            .Concat(buildings.Where(b => b.PlayerId == view.PlayerId).Select(b => b.LocationId)).Where(v => !enemyVertices.Contains(v)).Distinct().ToArray();
        var paths = starts.ToDictionary(v => v, _ => Array.Empty<string>());
        var pending = starts.ToList();
        while (pending.Count > 0)
        {
            var v = pending.OrderBy(v => paths[v].Length).First(); pending.Remove(v);
            foreach (var edge in view.Board.Edges.Where(e => e.Vertices.Contains(v) && !otherRoads.Contains(e.Id)))
            {
                var next = edge.Vertices.First(x => x != v);
                if (enemyVertices.Contains(next)) continue;
                var path = ownRoads.Contains(edge.Id) ? paths[v] : paths[v].Append(edge.Id).ToArray();
                if (path.Length > 5 || (paths.TryGetValue(next, out var old) && old.Length <= path.Length)) continue;
                paths[next] = path; if (!pending.Contains(next)) pending.Add(next);
            }
        }
        var targets = view.Board.Vertices.Where(v => !buildings.Any(b => b.LocationId == v.Id) && !view.Board.Knights.Any(k => k.LocationId == v.Id)
            && !view.Board.Edges.Where(e => e.Vertices.Contains(v.Id)).Any(e => buildings.Any(b => e.Vertices.Contains(b.LocationId)))
            && paths.ContainsKey(v.Id)).Select(v => (target: v.Id, path: paths[v.Id])).ToArray();
        if (targets.Length == 0) return null;
        int remaining = RemainingRolls(view.Turn, view.Players.Max(p => p.VictoryPoints), view.TargetVictoryPoints);
        return targets.OrderByDescending(t => 16.0 / (4 + 2 * t.path.Length) + Value(view, t.target) * remaining / 180.0 - t.path.Length * 2).ThenBy(t => t.target).First();
    }
}

}
