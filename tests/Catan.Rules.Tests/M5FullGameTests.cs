using Catan.Core;
using Catan.Core.M5;
using Xunit;
using Xunit.Abstractions;
using Command = Catan.Core.M5.Command;
using CommandKind = Catan.Core.M5.CommandKind;
using GamePhase = Catan.Core.M5.GamePhase;
using PlayerView = Catan.Core.M5.PlayerView;

namespace Catan.Rules.Tests;

public sealed class M5FullGameTests
{
    private readonly ITestOutputHelper output;
    public M5FullGameTests(ITestOutputHelper output) => this.output = output;

    [Theory]
    [InlineData("heading-for-new-shores", 3, 31u)]
    [InlineData("through-the-desert", 3, 31u)]
    [InlineData("heading-for-new-shores", 4, 42u)]
    [InlineData("through-the-desert", 4, 42u)]
    public void ViewOnlyLocalSeatsFinishNormalGameAndPendingSavesResumeDeterministically(string scenario, int seats, uint seed)
    {
        var game = CombinedGameSession.Create(scenario, seats, seed);
        var driver = new M5ViewOnlyDriver();
        var checkedDecisions = new HashSet<string>();
        var commands = 0;
        var last = game.GetPlayerView("P1");
        while (last.Phase != GamePhase.Finished && commands < 12000)
        {
            // The driver receives only a detached authorized view. Authority/save are assertion-only inputs.
            var command = driver.Decide(game.GetPlayerView(Actor(last)));
            command.Id = $"m5-full-{seats}-{seed}-{commands++}";
            var result = game.Execute(command);
            Assert.True(result.Success, $"Turn {last.Turn}, {command.PlayerId}/{command.Kind}/{command.TargetId}: {result.ErrorCode} {result.Message}");
            game.AssertInvariants();
            last = game.GetPlayerView("P1");
            var decisionKey = last.PendingDecision?.Kind ?? (last.Phase == GamePhase.Discard ? "discard" : null);
            if (decisionKey != null && checkedDecisions.Add(decisionKey))
            {
                var restored = CombinedGameSession.Load( game.Save());
                Assert.Equal(game.Save(), restored.Save());
                var next = driver.Decide(game.GetPlayerView(Actor(last)));
                next.Id = $"m5-full-{seats}-{seed}-{commands++}";
                Assert.True(game.Execute(next).Success);
                Assert.True(restored.Execute(next).Success);
                Assert.Equal(game.Save(), restored.Save());
                last = game.GetPlayerView("P1");
            }
        }
        Assert.True(last.Phase == GamePhase.Finished,
            $"Seed {seed}: no winner after {commands} commands/{last.Turn} turns; points {string.Join(",", last.Players.Select(p => p.Id + "=" + p.VictoryPoints))}");
        Assert.NotNull(last.WinnerPlayerId);
        Assert.Equal(last.ActivePlayerId, last.WinnerPlayerId);
        Assert.True(game.GetPlayerView(last.WinnerPlayerId).OwnVictoryPoints >= last.TargetVictoryPoints);
        Assert.True(last.FirstBarbarianAttack);
        output.WriteLine($"scenario={scenario}, seats={seats}, seed={seed}, winner={last.WinnerPlayerId}, turn={last.Turn}, commands={commands}, save-decisions={string.Join(",", checkedDecisions.Order())}");
        var observed=game.GetAuthoritativeStateForTesting();
        var shipCommands=observed.ProcessedCommands.Count(c=>c.Command.Kind is CommandKind.SetupShip or CommandKind.BuildShip or CommandKind.MoveShip || c.Command.BuildShip);
        var knightCommands=observed.ProcessedCommands.Count(c=>c.Command.Kind is CommandKind.RecruitKnight or CommandKind.ActivateKnight or CommandKind.PromoteKnight or CommandKind.MoveKnight or CommandKind.DisplaceKnight);
        Assert.True(shipCommands>0);Assert.True(knightCommands>0);
        output.WriteLine($"ship-commands={shipCommands}, knight-commands={knightCommands}, overseas-bonus={observed.Players.Sum(p=>p.BonusVictoryPoints)}");
        Assert.Equal(game.Save(), CombinedGameSession.Load( game.Save()).Save());
        AssertIndependentConservation(game);
    }

    [Fact]
    public void NormallyReachedFirstAttackPillageSavesBeforeAnyProductionAndResumesEveryVictim()
    {
        var game = CombinedGameSession.Create("heading-for-new-shores", 4, 53u);
        var driver = new M5ViewOnlyDriver();
        var view = game.GetPlayerView("P1");
        var count = 0;
        while (view.PendingDecision?.Kind != "PillageCity" && count < 500)
        {
            var seat = game.GetPlayerView(Actor(view));
            // All seats deliberately save resources and field no knights before the first attack.
            var next = seat.Phase == GamePhase.Action
                ? new Command { PlayerId = seat.PlayerId, Kind = CommandKind.EndTurn }
                : driver.Decide(seat);
            next.Id = $"pillage-normal-{count++}";
            Assert.True(game.Execute(next).Success);
            view = game.GetPlayerView("P1");
        }
        Assert.Equal("PillageCity", view.PendingDecision?.Kind);
        Assert.True(view.FirstBarbarianAttack);
        Assert.Equal(4, view.Board.Cities.Length);
        Assert.Null(view.Board.PirateTileId);
        var restored = CombinedGameSession.Load( game.Save());
        var victims = 0;
        while (view.PendingDecision?.Kind == "PillageCity")
        {
            Assert.Null(view.Board.PirateTileId);
            var next = driver.Decide(game.GetPlayerView(Actor(view))); next.Id = $"pillage-normal-{count++}";
            Assert.True(game.Execute(next).Success);
            Assert.True(restored.Execute(next).Success);
            Assert.Equal(game.Save(), restored.Save());
            view = game.GetPlayerView("P1"); victims++;
        }
        Assert.Equal(4, victims);
        Assert.Equal("frame",view.Board.PirateTileId);
        Assert.Empty(view.Board.Cities);
        AssertIndependentConservation(game);
        output.WriteLine($"normal-pillage seed=53, turn={view.Turn}, victims={victims}, commands={count}, continued-phase={view.Phase}");
    }

    private static void AssertIndependentConservation(CombinedGameSession game)
    {
        var authority = game.GetAuthoritativeStateForTesting();
        foreach (var r in Enum.GetValues<Resource>()) Assert.Equal(19, authority.Bank[r] + authority.Players.Sum(p => p.Resources[r]));
        foreach (var c in Enum.GetValues<Commodity>()) Assert.Equal(12, authority.CommodityBank[c] + authority.Players.Sum(p => p.Commodities[c]));
        foreach (var player in authority.Players)
        {
            Assert.Equal(15, player.Pieces.Roads + authority.Roads.Count(p => p.PlayerId == player.Id));
            Assert.Equal(15, player.ShipsRemaining + authority.Ships.Count(p => p.PlayerId == player.Id));
            Assert.Equal(5, player.Pieces.Settlements + authority.Settlements.Count(p => p.PlayerId == player.Id) - authority.DowngradedCities.Count(p => p.PlayerId == player.Id));
            Assert.Equal(4, player.Pieces.Cities + authority.Cities.Count(p => p.PlayerId == player.Id) + authority.DowngradedCities.Count(p => p.PlayerId == player.Id));
        }
        Assert.Equal(54, authority.ProgressDecks.Sum(d => d.Cards.Length) + authority.Players.Sum(p => p.ProgressCards.Length + p.ProgressVictoryCards.Length));
    }

    internal static string Actor(PlayerView view) => view.Phase == GamePhase.Discard ? view.Discards.First().PlayerId : view.PendingDecision?.PlayerId ?? view.ActivePlayerId;
}

/// <summary>Local acceptance policy. No session, authority, RNG, save, or unauthorized hand access.</summary>
internal sealed class M5ViewOnlyDriver
{
    private static readonly Resource[] Resources = Enum.GetValues<Resource>();
    private static readonly Commodity[] Commodities = Enum.GetValues<Commodity>();
    private static ResourceBag Bag(int wood = 0, int brick = 0, int wool = 0, int wheat = 0, int ore = 0) => new() { Wood = wood, Brick = brick, Wool = wool, Wheat = wheat, Ore = ore };

    internal Command Decide(PlayerView view)
    {
        Command Make(CommandKind kind, string? target = null) => new() { PlayerId = view.PlayerId, Kind = kind, TargetId = target! };
        var legal = (view.LegalActions ?? Array.Empty<Command>()).ToList();
        if (view.Phase == GamePhase.SetupSettlement)
            return Make(CommandKind.SetupSettlement, view.LegalVertexIds.OrderByDescending(v => Value(view, v)).ThenBy(v => v).First());
        if (view.Phase == GamePhase.SetupRoad && view.LegalShipEdgeIds.Length > 0)
            return Make(CommandKind.SetupShip, view.LegalShipEdgeIds.First());
        if (view.Phase == GamePhase.SetupRoad)
            return Make(CommandKind.SetupRoad, view.LegalEdgeIds.OrderByDescending(id => view.Board.Edges.Single(e => e.Id == id).Vertices.Max(v => Value(view, v))).First());
        if (view.Phase == GamePhase.ProductionAwaitRoll)
            return Make(CommandKind.RollDice);
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
            return Make(CommandKind.MoveRobber, view.Board.Tiles.Where(t => t.Id != view.Board.RobberTileId && t.Resource != "sea" && t.Resource != "fog").OrderByDescending(t =>
                Buildings(view).Where(b => t.Vertices.Contains(b.LocationId)).Sum(b => b.PlayerId == view.PlayerId ? -100 : 10 + view.Players.Single(p => p.Id == b.PlayerId).ResourceCount)).First().Id);
        if (view.Phase == GamePhase.RobberSteal)
        {
            var theft = Make(CommandKind.StealResource); theft.OtherPlayerId = view.PendingDecision.EligibleVictimIds.First(); return theft;
        }
        if (view.Phase == GamePhase.PendingChoice && view.PendingDecision?.Kind == "ProgressDiscard")
        {
            var discard = Make(CommandKind.DiscardProgressCard); discard.ProgressCard = view.OwnProgressCards[0]; return discard;
        }
        if (view.Phase != GamePhase.Action)
        {
            if (legal.Count == 0) throw new InvalidOperationException($"No public legal actions for {view.PlayerId}/{view.Phase}/{view.PendingDecision?.Kind}");
            return legal[0];
        }
        legal.AddRange(view.LegalCityVertexIds.Select(v => Make(CommandKind.BuildCity, v)));
        legal.AddRange(view.LegalVertexIds.Select(v => Make(CommandKind.BuildSettlement, v)));
        legal.AddRange(view.LegalEdgeIds.Select(v => Make(CommandKind.BuildRoad, v)));
        legal.AddRange(view.LegalShipEdgeIds.Select(v => Make(CommandKind.BuildShip, v)));

        var knights = view.Board.Knights.Where(k => k.PlayerId == view.PlayerId).ToArray();
        var me = view.Players.Single(p => p.Id == view.PlayerId);
        var cityCount = view.Board.Cities.Count(b => b.PlayerId == view.PlayerId);
        var defenseGoal = Math.Max(2, cityCount + 1);
        var activate = legal.FirstOrDefault(c => c.Kind == CommandKind.ActivateKnight);
        if (activate != null) return activate;
        if (knights.Sum(k => k.Level) < defenseGoal)
        {
            var promote = legal.FirstOrDefault(c => c.Kind == CommandKind.PromoteKnight);
            if (promote != null) return promote;
            var recruit = legal.Where(c => c.Kind == CommandKind.RecruitKnight).OrderBy(c => Value(view, c.TargetId)).FirstOrDefault();
            if (recruit != null) return recruit;
        }
        var city = legal.Where(c => c.Kind == CommandKind.BuildCity).OrderByDescending(c => Value(view, c.TargetId)).FirstOrDefault();
        if (city != null) return city;
        var settlement = legal.Where(c => c.Kind == CommandKind.BuildSettlement).OrderByDescending(c => Value(view, c.TargetId)).FirstOrDefault();
        if (settlement != null) return settlement;
        var improvement = legal.Where(c => c.Kind == CommandKind.ImproveCity)
            .OrderByDescending(c => view.OwnImprovements[(int)c.Track] == 3 ? 100 : c.Track == ImprovementTrack.Science ? 40 : c.Track == ImprovementTrack.Trade ? 30 : 10).FirstOrDefault();
        if (improvement != null) return improvement;
        var card = legal.FirstOrDefault(c => c.Kind == CommandKind.PlayProgressCard);
        if (card != null) return card;
        var harbor = legal.FirstOrDefault(c => c.Kind == CommandKind.CommercialHarborOffer);
        if (harbor != null) return harbor;

        var expansion = Expansion(view);
        var desired = Bag();
        CommodityBag commodityGoal = new();
        if (knights.Any(k => !k.Active)) desired.Wheat = 1;
        else if (knights.Sum(k => k.Level) < defenseGoal) { desired.Wool = 1; desired.Ore = 1; }
        else if (me.Pieces.Cities > 0 && view.Board.Settlements.Any(b => b.PlayerId == view.PlayerId)
            && (view.OwnResources.Ore >= 2 || me.Pieces.Settlements == 0 || expansion == null)) desired = Bag(wheat: 2, ore: 3);
        else if (me.Pieces.Settlements > 0 && expansion != null) desired = expansion.Value.path.Length == 0 ? Bag(1, 1, 1, 1) : expansion.Value.ship ? Bag(wood:1,wool:1) : Bag(1, 1);
        else if (cityCount > 0)
        {
            var track = new[] { ImprovementTrack.Science, ImprovementTrack.Trade, ImprovementTrack.Politics }.FirstOrDefault(t => view.OwnImprovements[(int)t] < 5);
            commodityGoal[TrackCommodity(track)] = view.OwnImprovements[(int)track] + 1;
        }
        if (expansion is { path.Length: > 0 } && Covers(view.OwnResources, desired))
        {
            var road = legal.FirstOrDefault(c => c.Kind == (expansion.Value.ship ? CommandKind.BuildShip : CommandKind.BuildRoad) && c.TargetId == expansion.Value.path[0]);
            if (road != null) return road;
        }
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
        if (view.OwnProgressCards.Length > 4) { var discard = Make(CommandKind.DiscardProgressCard); discard.ProgressCard = view.OwnProgressCards[0]; return discard; }
        return Make(CommandKind.EndTurn);
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

    private static (string target, string[] path, bool ship)? Expansion(PlayerView view)
    {
        var buildings = Buildings(view);
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
            .SelectMany(v=>paths.Where(p=>p.Key.vertex==v.Id).Select(p=>(target:v.Id,path:p.Value.Select(x=>x.Substring(2)).ToArray(),ship:p.Value.FirstOrDefault()?.StartsWith("S:")==true))).ToArray();
        if (targets.Length == 0) return null;
        return targets.OrderByDescending(t => Value(view, t.target) - t.path.Length * 12).ThenBy(t => t.target).First();
    }
}

