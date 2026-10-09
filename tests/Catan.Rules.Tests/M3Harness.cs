using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Catan.Core;
using Catan.Core.M3;
using Xunit;
using Command = Catan.Core.M3.Command;
using CommandKind = Catan.Core.M3.CommandKind;
using CommandResult = Catan.Core.M3.CommandResult;
using GamePhase = Catan.Core.M3.GamePhase;
using GameState = Catan.Core.M3.GameState;
using PlayerState = Catan.Core.M3.PlayerState;
using BoardView = Catan.Core.M3.BoardView;

namespace Catan.Rules.Tests;

/// <summary>Test-owned fixture injection. Production Create/Load never accepts arbitrary fixture states.</summary>
internal static class M3Harness
{
    internal static readonly Resource[] Resources = Enum.GetValues<Resource>();
    private static long sequence;
    internal static SeafarersGameSession New(int players = 4, uint seed = 42) => SeafarersGameSession.Create("heading-for-new-shores", players, seed);
    internal static SeafarersGameSession New(string scenario, int players = 4, uint seed = 42) => SeafarersGameSession.Create(scenario, players, seed);
    internal static Command Cmd(CommandKind kind, string player = "P1", string? target = null) => new()
    { Id = $"test-{Interlocked.Increment(ref sequence)}", PlayerId = player, Kind = kind, TargetId = target! };
    internal static PlayerState Player(GameState state, string player = "P1") => state.Players.Single(p => p.Id == player);
    internal static PiecePlacement Piece(string location, string player = "P1") => new() { LocationId = location, PlayerId = player };
    internal static ShipPlacement Ship(string location, string player = "P1", int built = 1, bool warship = false) =>
        new() { LocationId = location, PlayerId = player, BuiltTurn = built, IsWarship = warship };
    internal static ResourceBag Bag(int wood = 0, int brick = 0, int wool = 0, int wheat = 0, int ore = 0) =>
        new() { Wood = wood, Brick = brick, Wool = wool, Wheat = wheat, Ore = ore };

    internal static SeafarersGameSession Fixture(Action<GameState>? configure = null, string scenario = "heading-for-new-shores")
    {
        var game = New(scenario);
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
            p.ShipsRemaining = 15 - state.Ships.Count(x => x.PlayerId == p.Id);
            p.Pieces.Settlements -= state.Fortresses.Count(f => f.PlayerId == p.Id && f.Strength > 0);
        }
        var cards = Deck().ToList();
        if (state.ScenarioId == "pirate-islands" && state.Players.Length == 3)
            cards.RemoveAll(c => c == DevelopmentCardKind.VictoryPoint);
        if (state.ScenarioId == "pirate-islands" && state.Players.Length == 4)
            cards = cards.Select(c => c == DevelopmentCardKind.VictoryPoint ? DevelopmentCardKind.Knight : c).ToList();
        foreach (var card in state.Players.SelectMany(p => p.DevelopmentCards).Select(c => c.Kind).Concat(state.PlayedDevelopmentCards).Concat(state.GiftCards.Select(c => c.Kind)))
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

    internal static void SetState(SeafarersGameSession game, GameState state)
    {
        typeof(SeafarersGameSession).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, state);
        Invoke(game, "RecalculateAwards", state);
        Invariants(game);
    }

    internal static object? Invoke(SeafarersGameSession game, string method, params object[] args) =>
        typeof(SeafarersGameSession).GetMethod(method, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(game, args);

    internal static CommandResult Accepted(SeafarersGameSession game, Command command)
    {
        var result = game.Execute(command);
        Assert.True(result.Success, $"{command.Kind}/{command.PlayerId}/{command.TargetId}: {result.ErrorCode} {result.Message}");
        Assert.False(result.IsDuplicate);
        Assert.NotEmpty(result.NewEvents);
        Invariants(game);
        return result;
    }

    internal static void Rejected(SeafarersGameSession game, Command command)
    {
        var before = game.Save();
        var result = game.Execute(command);
        Assert.False(result.Success, $"Unexpected acceptance: {command.Kind}/{command.PlayerId}/{command.TargetId}");
        Assert.NotEmpty(result.ErrorCode);
        Assert.Empty(result.NewEvents);
        Assert.Equal(before, game.Save());
        Invariants(game);
    }

    internal static void Invariants(SeafarersGameSession game)
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
            Assert.Equal(5, p.Pieces.Settlements + state.Settlements.Count(x => x.PlayerId == p.Id) + state.Fortresses.Count(f => f.PlayerId == p.Id && f.Strength > 0));
            Assert.Equal(4, p.Pieces.Cities + state.Cities.Count(x => x.PlayerId == p.Id));
            Assert.Equal(15, p.ShipsRemaining + state.Ships.Count(x => x.PlayerId == p.Id));
            Assert.InRange(p.ShipsRemaining, 0, 15);
            Assert.InRange(p.Pieces.Roads, 0, 15);
            Assert.InRange(p.Pieces.Settlements, 0, 5);
            Assert.InRange(p.Pieces.Cities, 0, 4);
        }
        var held = state.Players.SelectMany(p => p.DevelopmentCards).Select(c => c.Kind);
        var expectedDeck = Deck().Where(c => !(state.ScenarioId == "pirate-islands" && state.Players.Length == 3 && c == DevelopmentCardKind.VictoryPoint));
        if (state.ScenarioId == "pirate-islands" && state.Players.Length == 4)
            expectedDeck = expectedDeck.Select(c => c == DevelopmentCardKind.VictoryPoint ? DevelopmentCardKind.Knight : c);
        Assert.Equal(expectedDeck.Order(), state.DevelopmentDeck.Concat(state.PlayedDevelopmentCards).Concat(held).Concat(state.GiftCards.Select(c => c.Kind)).Order());
        Assert.Equal(state.Roads.Length + state.Ships.Length, state.Roads.Select(p => p.LocationId).Concat(state.Ships.Select(p => p.LocationId)).Distinct().Count());
        if (state.ScenarioId == "cloth-for-catan") Assert.Equal(50, state.ClothSupply + state.Villages.Sum(v => v.Cloth) + state.Players.Sum(p => p.Cloth));
        var buildings = state.Settlements.Concat(state.Cities).ToArray();
        Assert.Equal(buildings.Length, buildings.Select(p => p.LocationId).Distinct().Count());
        var board = game.GetPlayerView("P1").Board;
        Assert.All(state.Roads, r => Assert.Contains(board.Edges, e => e.Id == r.LocationId));
        Assert.All(buildings, b => Assert.Contains(board.Vertices, v => v.Id == b.LocationId));
    }

    internal static void CompleteSetup(SeafarersGameSession game)
    {
        for (var i = 0; i < 40; i++)
        {
            var publicView = game.GetPlayerView("P1");
            if (publicView.Phase == GamePhase.SetupPort)
            {
                Accepted(game, Cmd(CommandKind.SetupPort, publicView.ActivePlayerId, game.GetPlayerView(publicView.ActivePlayerId).LegalPortEdgeIds.First()));
                continue;
            }
            if (publicView.Phase == GamePhase.GoldChoice)
            {
                var gold = Cmd(CommandKind.ChooseGoldResources, publicView.GoldClaims[0].PlayerId);
                gold.Resources = Bag(wood: publicView.GoldClaims[0].Amount);
                Accepted(game, gold); continue;
            }
            if (publicView.Phase != GamePhase.SetupSettlement && publicView.Phase != GamePhase.SetupRoad) return;
            var view = game.GetPlayerView(publicView.ActivePlayerId);
            var isVillage = view.Phase == GamePhase.SetupSettlement;
            Accepted(game, Cmd(isVillage ? CommandKind.SetupSettlement : CommandKind.SetupRoad, view.PlayerId,
                isVillage ? view.LegalVertexIds.First() : view.LegalEdgeIds.First()));
        }
        Assert.Equal(GamePhase.ProductionAwaitRoll, game.GetPlayerView("P1").Phase);
    }

    internal static bool SeaEdge(BoardView board, Edge edge) => board.Tiles.Count(t => edge.Vertices.All(t.Vertices.Contains)) == 1 || board.Tiles.Any(t => (t.Resource == "sea" || t.Resource == "fog") && edge.Vertices.All(t.Vertices.Contains));
    internal static bool LandEdge(BoardView board, Edge edge) => board.Tiles.Any(t => t.Resource != "sea" && t.Resource != "fog" && edge.Vertices.All(t.Vertices.Contains));
    internal static bool CoastVertex(BoardView board, string vertex) => board.Tiles.Any(t => t.Resource == "sea" && t.Vertices.Contains(vertex)) && board.Tiles.Any(t => t.Resource != "sea" && t.Resource != "fog" && t.Vertices.Contains(vertex));
}

