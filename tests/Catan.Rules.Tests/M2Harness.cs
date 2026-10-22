using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Catan.Core;
using Catan.Core.M2;
using Xunit;
using Command = Catan.Core.M2.Command;
using CommandKind = Catan.Core.M2.CommandKind;
using CommandResult = Catan.Core.M2.CommandResult;
using GamePhase = Catan.Core.M2.GamePhase;
using GameState = Catan.Core.M2.GameState;
using PlayerState = Catan.Core.M2.PlayerState;

namespace Catan.Rules.Tests;

/// <summary>Test-owned fixture injection. Production Create/Load never accepts arbitrary fixture states.</summary>
internal static class M2Harness
{
    internal static readonly Resource[] Resources = Enum.GetValues<Resource>();
    private static long sequence;
    internal static BaseGameSession New(int players = 4, uint seed = 42) => BaseGameSession.Create(AcceptanceFixture.Json, players, seed);
    internal static Command Cmd(CommandKind kind, string player = "P1", string? target = null) => new()
    { Id = $"test-{Interlocked.Increment(ref sequence)}", PlayerId = player, Kind = kind, TargetId = target! };
    internal static PlayerState Player(GameState state, string player = "P1") => state.Players.Single(p => p.Id == player);
    internal static PiecePlacement Piece(string location, string player = "P1") => new() { LocationId = location, PlayerId = player };
    internal static ResourceBag Bag(int wood = 0, int brick = 0, int wool = 0, int wheat = 0, int ore = 0) =>
        new() { Wood = wood, Brick = brick, Wool = wool, Wheat = wheat, Ore = ore };

    internal static BaseGameSession Fixture(Action<GameState>? configure = null)
    {
        var game = New();
        var state = game.GetAuthoritativeStateForTesting();
        state.ActivePlayerId = "P1";
        state.Turn = 9;
        state.Phase = GamePhase.Action;
        state.SetupPlacementIndex = state.Players.Length * 2;
        state.PendingDecision = null!;
        configure?.Invoke(state);
        Balance(state);
        SetState(game, state);
        return game;
    }

    internal static void Balance(GameState state)
    {
        foreach (var resource in Resources)
            state.Bank[resource] = 19 - state.Players.Sum(p => p.Resources[resource]);
        foreach (var p in state.Players)
        {
            p.Pieces.Roads = 15 - state.Roads.Count(x => x.PlayerId == p.Id);
            p.Pieces.Settlements = 5 - state.Settlements.Count(x => x.PlayerId == p.Id);
            p.Pieces.Cities = 4 - state.Cities.Count(x => x.PlayerId == p.Id);
        }
        var cards = Deck().ToList();
        foreach (var card in state.Players.SelectMany(p => p.DevelopmentCards).Select(c => c.Kind).Concat(state.PlayedDevelopmentCards))
            Assert.True(cards.Remove(card), $"Fixture exceeds official card count for {card}");
        state.DevelopmentDeck = cards.ToArray();
    }

    internal static DevelopmentCardKind[] Deck() =>
        Enumerable.Repeat(DevelopmentCardKind.Knight, 14).Concat(Enumerable.Repeat(DevelopmentCardKind.VictoryPoint, 5))
        .Concat(Enumerable.Repeat(DevelopmentCardKind.RoadBuilding, 2)).Concat(Enumerable.Repeat(DevelopmentCardKind.YearOfPlenty, 2))
        .Concat(Enumerable.Repeat(DevelopmentCardKind.Monopoly, 2)).ToArray();

    internal static void Card(GameState state, DevelopmentCardKind kind, string player = "P1", int boughtTurn = 1)
    {
        var p = Player(state, player);
        p.DevelopmentCards = p.DevelopmentCards.Append(new DevelopmentCard { Kind = kind, BoughtTurn = boughtTurn }).ToArray();
    }

    internal static void SetState(BaseGameSession game, GameState state)
    {
        typeof(BaseGameSession).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, state);
        Invoke(game, "RecalculateAwards", state);
        Invariants(game);
    }

    internal static object? Invoke(BaseGameSession game, string method, params object[] args) =>
        typeof(BaseGameSession).GetMethod(method, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(game, args);

    internal static CommandResult Accepted(BaseGameSession game, Command command)
    {
        var result = game.Execute(command);
        Assert.True(result.Success, $"{command.Kind}/{command.PlayerId}/{command.TargetId}: {result.ErrorCode} {result.Message}");
        Assert.False(result.IsDuplicate);
        Assert.NotEmpty(result.NewEvents);
        Invariants(game);
        return result;
    }

    internal static void Rejected(BaseGameSession game, Command command)
    {
        var before = game.Save();
        var result = game.Execute(command);
        Assert.False(result.Success, $"Unexpected acceptance: {command.Kind}/{command.PlayerId}/{command.TargetId}");
        Assert.NotEmpty(result.ErrorCode);
        Assert.Empty(result.NewEvents);
        Assert.Equal(before, game.Save());
        Invariants(game);
    }

    internal static void Invariants(BaseGameSession game)
    {
        game.AssertInvariants();
        var state = game.GetAuthoritativeStateForTesting();
        foreach (var resource in Resources)
        {
            Assert.InRange(state.Bank[resource], 0, 19);
            Assert.All(state.Players, p => Assert.InRange(p.Resources[resource], 0, 19));
            Assert.Equal(19, state.Bank[resource] + state.Players.Sum(p => p.Resources[resource]));
        }
        foreach (var p in state.Players)
        {
            Assert.Equal(15, p.Pieces.Roads + state.Roads.Count(x => x.PlayerId == p.Id));
            Assert.Equal(5, p.Pieces.Settlements + state.Settlements.Count(x => x.PlayerId == p.Id));
            Assert.Equal(4, p.Pieces.Cities + state.Cities.Count(x => x.PlayerId == p.Id));
            Assert.InRange(p.Pieces.Roads, 0, 15);
            Assert.InRange(p.Pieces.Settlements, 0, 5);
            Assert.InRange(p.Pieces.Cities, 0, 4);
        }
        var held = state.Players.SelectMany(p => p.DevelopmentCards).Select(c => c.Kind);
        Assert.Equal(Deck().Order(), state.DevelopmentDeck.Concat(state.PlayedDevelopmentCards).Concat(held).Order());
        Assert.Equal(state.Roads.Length, state.Roads.Select(p => p.LocationId).Distinct().Count());
        var buildings = state.Settlements.Concat(state.Cities).ToArray();
        Assert.Equal(buildings.Length, buildings.Select(p => p.LocationId).Distinct().Count());
        var board = game.GetPlayerView("P1").Board;
        Assert.All(state.Roads, r => Assert.Contains(board.Edges, e => e.Id == r.LocationId));
        Assert.All(buildings, b => Assert.Contains(board.Vertices, v => v.Id == b.LocationId));
    }

    internal static void CompleteSetup(BaseGameSession game)
    {
        for (var i = 0; i < 16; i++)
        {
            var publicView = game.GetPlayerView("P1");
            if (publicView.Phase != GamePhase.SetupSettlement && publicView.Phase != GamePhase.SetupRoad) return;
            var view = game.GetPlayerView(publicView.ActivePlayerId);
            var isVillage = view.Phase == GamePhase.SetupSettlement;
            Accepted(game, Cmd(isVillage ? CommandKind.SetupSettlement : CommandKind.SetupRoad, view.PlayerId,
                isVillage ? view.LegalVertexIds.First() : view.LegalEdgeIds.First()));
        }
        Assert.Equal(GamePhase.ProductionAwaitRoll, game.GetPlayerView("P1").Phase);
    }
}
