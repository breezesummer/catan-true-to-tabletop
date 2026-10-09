using Catan.Core;
using Catan.Core.M3;
using Xunit;
using Command = Catan.Core.M3.Command;
using CommandKind = Catan.Core.M3.CommandKind;
using GamePhase = Catan.Core.M3.GamePhase;
using PlayerView = Catan.Core.M3.PlayerView;
using static Catan.Rules.Tests.M3Harness;

namespace Catan.Rules.Tests;

public sealed class SeafarersPortPersistenceTests
{
    [Fact]
    public void NormallyAcquiredGiftPortPendingRestoresAndContinuesInIndependentAuthority()
    {
        var game = New("forgotten-tribe", 4, 109);
        var driver = new M3ViewOnlyDriver();
        for (var i = 0; i < 2400; i++)
        {
            var publicView = game.GetPlayerView("P1");
            var who = M3FullGameTests.Actor(publicView); var view = game.GetPlayerView(who);
            if (view.Phase == GamePhase.PortPlacement)
            {
                var restored = SeafarersGameSession.Load(game.Save()); Assert.Equal(game.Save(), restored.Save());
                var command = Cmd(CommandKind.PlacePort, who, view.LegalPortEdgeIds.First()); command.SourceId = view.OwnHeldPorts.First().Id;
                Accepted(game, command); Accepted(restored, command); Assert.Equal(game.Save(), restored.Save());
                Assert.True(game.GetPlayerView(who).Board.Ports.Length > view.Board.Ports.Length);
                return;
            }
            Command next;
            if (view.Phase == GamePhase.SetupSettlement)
            {
                var candidates = view.LegalVertexIds.Where(v => CoastVertex(view.Board, v)).ToArray();
                if (candidates.Length == 0) candidates = view.LegalVertexIds;
                var target = candidates.OrderByDescending(v => view.Board.Tiles.Where(t => t.Vertices.Contains(v) && t.Number.HasValue).Sum(t => 6 - Math.Abs(7 - t.Number!.Value))).First();
                next = Cmd(CommandKind.SetupSettlement, who, target);
            }
            else if (view.Phase == GamePhase.SetupRoad && view.LegalShipEdgeIds.Length > 0)
                next = Cmd(CommandKind.SetupShip, who, view.LegalShipEdgeIds.First());
            else if (view.Phase == GamePhase.Action) next = SailForPort(view);
            else next = driver.Decide(view);
            next.Id = "port-history-" + i; Accepted(game, next);
        }
        Assert.Fail("A normal view-only route did not reach a gift port within the command bound.");
    }

    private static Command SailForPort(PlayerView view)
    {
        var own = view.Board.Ships.Where(s => s.PlayerId == view.PlayerId).Select(s => s.LocationId).ToHashSet();
        var occupied = view.Board.Roads.Select(r => r.LocationId).Concat(view.Board.Ships.Where(s => s.PlayerId != view.PlayerId).Select(s => s.LocationId)).ToHashSet();
        var roots = view.Board.Settlements.Concat(view.Board.Cities).Where(b => b.PlayerId == view.PlayerId).Select(b => b.LocationId)
            .Concat(view.Board.Edges.Where(e => own.Contains(e.Id)).SelectMany(e => e.Vertices)).Distinct().ToArray();
        var enemy = view.Board.Settlements.Concat(view.Board.Cities).Where(b => b.PlayerId != view.PlayerId).Select(b => b.LocationId).ToHashSet();
        var paths = roots.Where(v => !enemy.Contains(v)).ToDictionary(v => v, _ => Array.Empty<string>());
        var queue = new List<string>(paths.Keys);
        string[]? chosen = null;
        while (queue.Count > 0)
        {
            var current = queue.OrderBy(v => paths[v].Length).First(); queue.Remove(current);
            if (enemy.Contains(current)) continue;
            foreach (var edge in view.Board.Edges.Where(e => e.Vertices.Contains(current) && !occupied.Contains(e.Id)))
            {
                var tiles = view.Board.Tiles.Where(t => edge.Vertices.All(t.Vertices.Contains)).ToArray();
                if (tiles.Length > 1 && tiles.All(t => t.Resource != "sea")) continue;
                if (tiles.Any(t => t.Id == view.Board.PirateTileId)) continue;
                var path = own.Contains(edge.Id) ? paths[current] : paths[current].Append(edge.Id).ToArray();
                if (view.Board.GiftPorts.Any(p => p.EdgeId == edge.Id) && path.Length > 0 && (chosen == null || path.Length < chosen.Length)) chosen = path;
                var next = edge.Vertices.First(v => v != current);
                if (paths.TryGetValue(next, out var prior) && prior.Length <= path.Length) continue;
                paths[next] = path; if (!queue.Contains(next)) queue.Add(next);
            }
        }
        if (chosen != null && view.LegalShipEdgeIds.Contains(chosen[0])) return Cmd(CommandKind.BuildShip, view.PlayerId, chosen[0]);
        foreach (var need in new[] { Resource.Wood, Resource.Wool }.Where(r => view.OwnResources[r] == 0 && view.Bank[r] > 0))
        {
            var give = Resources.FirstOrDefault(r => r != need && view.OwnResources[r] >= 4 + (r == Resource.Wood || r == Resource.Wool ? 1 : 0));
            if (give != need && view.OwnResources[give] >= 4 + (give == Resource.Wood || give == Resource.Wool ? 1 : 0))
            { var trade = Cmd(CommandKind.BankTrade, view.PlayerId); trade.GiveResource = give; trade.ReceiveResource = need; return trade; }
        }
        return Cmd(CommandKind.EndTurn, view.PlayerId);
    }
}
