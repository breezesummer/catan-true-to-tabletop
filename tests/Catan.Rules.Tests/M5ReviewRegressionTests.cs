using Catan.Core;
using Catan.Core.M5;
using Xunit;
using static Catan.Rules.Tests.M5Harness;
using static Catan.Rules.Tests.M5CombinationTests;
using BoardView = Catan.Core.M5.BoardView;
using CommandKind = Catan.Core.M5.CommandKind;
using GamePhase = Catan.Core.M5.GamePhase;

namespace Catan.Rules.Tests;

/// <summary>Independent fixtures derived from official ship-loop FAQ and finite-bank production ordering.</summary>
public sealed class M5ReviewRegressionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SingleSettlementShipLoopHasTwoOpenEndsForMovementAndDiplomacy(bool diplomacy)
    {
        var board = New().GetPlayerView("P1").Board;
        var (tile, anchor, ring) = CoastalRing(board);
        var expectedOpen = ring.Where(e => e.Vertices.Contains(anchor)).Select(e => e.Id).Order().ToArray();
        Assert.Equal(6, ring.Length);
        Assert.Equal(2, expectedOpen.Length);
        var game = Fixture(s =>
        {
            s.Settlements = new[] { Piece(anchor) };
            s.Ships = ring.Select(e => Ship(e.Id)).ToArray();
            if (diplomacy) Player(s).ProgressCards = new[] { ProgressCardKind.Diplomacy };
        });

        // FAQ: a route which returns to its only building has an open end at either side of that building.
        Assert.Equal(expectedOpen, game.GetPlayerView("P1").MovableShipEdgeIds.Order().ToArray());
        if (diplomacy)
        {
            Accepted(game, Play(ProgressCardKind.Diplomacy, expectedOpen[0]));
            Assert.Equal(5, game.GetPlayerView("P1").Board.Ships.Length);
            var pending = game.GetPlayerView("P1").PendingDecision;
            Assert.Equal("ProgressRoads", pending.Kind);
            Assert.True(pending.ShipsOnly);
            Assert.False(pending.RoadsOnly);
        }
        else
        {
            var move = board.Edges.Where(e => !ring.Any(r => r.Id == e.Id)).Select(e =>
            {
                var c = Cmd(CommandKind.MoveShip, target: e.Id);
                c.SourceId = expectedOpen[0];
                return c;
            }).First(c => game.Preview(c).Success);
            Accepted(game, move);
            Assert.Equal(6, game.GetPlayerView("P1").Board.Ships.Length);
            Assert.True(game.GetPlayerView("P1").ShipMovedThisTurn);
            Assert.DoesNotContain(game.GetPlayerView("P1").Board.Ships, ship => ship.LocationId == expectedOpen[0]);
        }
        Invariants(game);
    }

    [Fact]
    public void ShipLoopWithoutFriendlyAnchorHasEveryShipOpen()
    {
        var board = New().GetPlayerView("P1").Board;
        var (_, _, ring) = CoastalRing(board);
        var game = Fixture(s => s.Ships = ring.Select(e => Ship(e.Id)).ToArray());

        // This is the FAQ's second explicit loop case; a degree-one test would incorrectly return none.
        Assert.Equal(ring.Select(e => e.Id).Order(), game.GetPlayerView("P1").MovableShipEdgeIds.Order());
    }

    [Fact]
    public void KnightAsSecondAnchorClosesBothPathsAroundShipLoop()
    {
        var board = New().GetPlayerView("P1").Board;
        var (tile, anchor, ring) = CoastalRing(board);
        var opposite = tile.Vertices[(Array.IndexOf(tile.Vertices, anchor) + 3) % 6];
        var game = Fixture(s =>
        {
            s.Settlements = new[] { Piece(anchor) };
            s.Ships = ring.Select(e => Ship(e.Id)).ToArray();
            s.Knights = new[] { Knight(opposite) };
            Player(s).ProgressCards = new[] { ProgressCardKind.Diplomacy };
        });

        // C&K knights are also anchors. The two arcs now connect distinct friendly anchors.
        Assert.Empty(game.GetPlayerView("P1").MovableShipEdgeIds);
        foreach (var edge in ring) Rejected(game, Play(ProgressCardKind.Diplomacy, edge.Id));
    }

    [Fact]
    public void GoldTakingLastBankResourceDoesNotLeaveAqueductWithoutAnyLegalChoice()
    {
        var board = New().GetPlayerView("P1").Board;
        var gold = board.Tiles.First(t => t.Resource == "gold");
        var vertex = gold.Vertices.First(v => !board.Tiles.Any(t => t.Id != gold.Id && t.Number == gold.Number && t.Vertices.Contains(v)));
        var game = Fixture(s =>
        {
            s.Settlements = new[] { Piece(vertex) };
            Player(s, "P2").Improvements[(int)ImprovementTrack.Science] = 3;
            Player(s, "P4").Resources = Bag(19, 19, 19, 19, 18);
            s.LastDice1 = Math.Min(6, gold.Number!.Value - 1);
            s.LastDice2 = gold.Number.Value - s.LastDice1;
        });
        var state = game.GetAuthoritativeStateForTesting();
        Invoke(game, "ResolveProduction", state);
        SetState(game, state);
        Assert.Equal("Gold", game.GetPlayerView("P1").PendingDecision.Kind);
        Assert.Equal(1, game.GetPlayerView("P1").Bank.Total);
        var choose = Cmd(CommandKind.ChooseGoldResources);
        choose.Resources = Bag(ore: 1);
        Accepted(game, choose);

        Assert.Equal(0, game.GetPlayerView("P1").Bank.Total);
        Assert.Equal(1, game.GetPlayerView("P1").OwnResources.Ore);
        Assert.Equal(0, game.GetPlayerView("P2").OwnResources.Total);
        Assert.Equal(GamePhase.Action, game.GetPlayerView("P1").Phase);
        Assert.Null(game.GetPlayerView("P1").PendingDecision);
        Assert.Empty(game.GetAuthoritativeStateForTesting().DecisionQueue);
    }

    [Fact]
    public void SeveralAqueductClaimsCannotDeadlockWhenFirstTakesFinalResource()
    {
        var game = Fixture(s =>
        {
            Player(s).Improvements[(int)ImprovementTrack.Science] = 3;
            Player(s, "P2").Improvements[(int)ImprovementTrack.Science] = 3;
            Player(s, "P4").Resources = Bag(19, 19, 19, 19, 18);
            s.LastDice1 = 2;
            s.LastDice2 = 3;
        });
        var state = game.GetAuthoritativeStateForTesting();
        Invoke(game, "ResolveProduction", state);
        SetState(game, state);
        Assert.Equal("Aqueduct", game.GetPlayerView("P1").PendingDecision.Kind);
        var choose = Cmd(CommandKind.ResolveChoice);
        choose.Resource = Resource.Ore;
        Accepted(game, choose);

        Assert.Equal(1, game.GetPlayerView("P1").OwnResources.Ore);
        Assert.Equal(0, game.GetPlayerView("P2").OwnResources.Total);
        Assert.Equal(GamePhase.Action, game.GetPlayerView("P1").Phase);
        Assert.Null(game.GetPlayerView("P1").PendingDecision);
        Assert.Equal(0, game.GetPlayerView("P1").Bank.Total);
    }

    private static (Tile tile, string anchor, Edge[] ring) CoastalRing(BoardView board)
    {
        var tile = board.Tiles.First(t => t.Resource == "sea" && t.Vertices.Any(v =>
            board.Tiles.Any(land => land.Resource != "sea" && land.Vertices.Contains(v))));
        var anchor = tile.Vertices.First(v => board.Tiles.Any(t => t.Resource != "sea" && t.Vertices.Contains(v)));
        var ring = board.Edges.Where(e => e.Vertices.All(tile.Vertices.Contains)).ToArray();
        return (tile, anchor, ring);
    }
}
