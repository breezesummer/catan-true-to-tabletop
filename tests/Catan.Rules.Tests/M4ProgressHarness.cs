using System.Reflection;
using Catan.Core;
using Catan.Core.M4;
using Xunit;
using Command = Catan.Core.M4.Command;
using CommandKind = Catan.Core.M4.CommandKind;
using GamePhase = Catan.Core.M4.GamePhase;
using GameState = Catan.Core.M4.GameState;
using PlayerState = Catan.Core.M4.PlayerState;

namespace Catan.Rules.Tests;

internal static class M4ProgressHarness
{
    private static long sequence;
    internal static CitiesKnightsGameSession New(uint seed = 42) => CitiesKnightsGameSession.Create(AcceptanceFixture.Json, 4, seed);
    internal static Command Cmd(CommandKind kind, string player = "P1", string? target = null) => new() { Id = $"progress-{Interlocked.Increment(ref sequence)}", Kind = kind, PlayerId = player, TargetId = target! };
    internal static Command Play(ProgressCardKind card, string? target = null, string player = "P1") { var c = Cmd(CommandKind.PlayProgressCard, player, target); c.ProgressCard = card; return c; }
    internal static PlayerState P(GameState s, string id = "P1") => s.Players.Single(p => p.Id == id);
    internal static PiecePlacement Piece(string location, string player = "P1") => new() { LocationId = location, PlayerId = player };
    internal static Knight Knight(string location, string player = "P1", int level = 1, bool active = false) => new() { LocationId = location, PlayerId = player, Level = level, Active = active, ActivatedTurn = 1, PromotedTurn = 1, ActedTurn = 1 };
    internal static CitiesKnightsGameSession Fixture(ProgressCardKind card, Action<GameState>? configure = null)
    {
        var g = New(); var s = g.GetAuthoritativeStateForTesting();
        s.ActivePlayerId = "P1"; s.Turn = 9; s.Phase = GamePhase.Action; s.SetupPlacementIndex = 8; s.PendingDecision = null!;
        P(s).ProgressCards = [card]; configure?.Invoke(s); Set(g, s); return g;
    }
    internal static void Set(CitiesKnightsGameSession g, GameState s)
    {
        foreach (var r in Enum.GetValues<Resource>()) s.Bank[r] = 19 - s.Players.Sum(p => p.Resources[r]);
        foreach (var c in Enum.GetValues<Commodity>()) s.CommodityBank[c] = 12 - s.Players.Sum(p => p.Commodities[c]);
        foreach (var p in s.Players)
        {
            p.Pieces.Roads = 15 - s.Roads.Count(r => r.PlayerId == p.Id);
            p.Pieces.Settlements = 5 - s.Settlements.Count(b => b.PlayerId == p.Id);
            p.Pieces.Cities = 4 - s.Cities.Count(b => b.PlayerId == p.Id);
        }
        foreach (var deck in s.ProgressDecks)
        {
            var cards = ProgressCatalog.Deck(deck.Track).ToList();
            foreach (var card in s.Players.SelectMany(p => p.ProgressCards.Concat(p.ProgressVictoryCards)).Where(c => ProgressCatalog.Track(c) == deck.Track)) Assert.True(cards.Remove(card));
            deck.Cards = cards.ToArray();
        }
        typeof(CitiesKnightsGameSession).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(g, s);
        Invoke(g, "RecalculateAwards", s); g.AssertInvariants();
    }
    internal static void Invoke(CitiesKnightsGameSession g, string method, params object[] args) => typeof(CitiesKnightsGameSession).GetMethod(method, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(g, args);
    internal static void Accepted(CitiesKnightsGameSession g, Command c)
    {
        var r = g.Execute(c); Assert.True(r.Success, $"{c.Kind}/{c.ProgressCard}/{c.TargetId}: {r.ErrorCode} {r.Message}"); g.AssertInvariants();
    }
    internal static void Rejected(CitiesKnightsGameSession g, Command c)
    {
        var before = g.Save(); var r = g.Execute(c); Assert.False(r.Success); Assert.Equal(before, g.Save()); g.AssertInvariants();
    }
    internal static string FirstVertex => New().GetPlayerView("P1").Board.Vertices[0].Id;
}
