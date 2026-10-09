using System.Text.Json.Nodes;
using Catan.Core;
using Catan.Core.M3;
using Xunit;
using CommandKind = Catan.Core.M3.CommandKind;
using GamePhase = Catan.Core.M3.GamePhase;
using static Catan.Rules.Tests.M3Harness;

namespace Catan.Rules.Tests;

public sealed class M3PersistenceTests
{
    [Fact]
    public void NewWorldPortsAreChosenInTurnAndPendingPortSaveIsAuthoritative()
    {
        var game = New("new-world"); var view = game.GetPlayerView("P1");
        Assert.Equal(GamePhase.SetupPort, view.Phase); Assert.Empty(view.Board.Ports);
        var actor = view.ActivePlayerId; view = game.GetPlayerView(actor); Assert.NotNull(view.NextSetupPort);
        var bad = Cmd(CommandKind.SetupPort, actor == "P1" ? "P2" : "P1", view.LegalPortEdgeIds.First()); Rejected(game, bad);
        var place = Cmd(CommandKind.SetupPort, actor, view.LegalPortEdgeIds.First()); Accepted(game, place);
        var restored = SeafarersGameSession.Load(game.Save()); Assert.Equal(game.Save(), restored.Save());
        view = game.GetPlayerView("P1"); Assert.NotEqual(actor, view.ActivePlayerId);
        var occupied = view.Board.Ports[0].EdgeId;
        Rejected(game, Cmd(CommandKind.SetupPort, view.ActivePlayerId, occupied));
        var next = Cmd(CommandKind.SetupPort, view.ActivePlayerId, game.GetPlayerView(view.ActivePlayerId).LegalPortEdgeIds.First());
        Accepted(game, next); Accepted(restored, next); Assert.Equal(game.Save(), restored.Save());
        CompleteSetup(game); Assert.Equal(10, game.GetPlayerView("P1").Board.Ports.Length);
    }
    [Theory]
    [InlineData("SaveFormatVersion")]
    [InlineData("RulesVersion")]
    [InlineData("ScenarioId")]
    [InlineData("ScenarioVersion")]
    [InlineData("ScenarioContentHash")]
    [InlineData("InitialSeed")]
    [InlineData("Random")]
    [InlineData("Tiles")]
    [InlineData("HiddenResources")]
    [InlineData("ShipMovedThisTurn")]
    [InlineData("PendingDecision")]
    [InlineData("ProcessedCommands")]
    public void AlteredAuthorityOrHiddenMapCannotLoad(string field)
    {
        var game = New("fog-islands");
        var who = game.GetPlayerView("P1").ActivePlayerId;
        Accepted(game, Cmd(CommandKind.SetupSettlement, who, game.GetPlayerView(who).LegalVertexIds.First()));
        var save = JsonNode.Parse(game.Save())!;
        switch (field)
        {
            case "SaveFormatVersion": save[field] = 999; break;
            case "InitialSeed": save[field] = 987u; break;
            case "Random": save[field]!["State"] = 987u; break;
            case "Tiles": save[field]![0]!["resource"] = "gold"; break;
            case "HiddenResources": save[field]![0] = "invalid"; break;
            case "ShipMovedThisTurn": save[field] = true; break;
            case "PendingDecision": save[field]!["AnchorVertexId"] = "V999999"; break;
            case "ProcessedCommands": save[field]![0]!["Fingerprint"] = "changed"; break;
            default: save[field] = "unsupported"; break;
        }
        Assert.Throws<InvalidDataException>(() => SeafarersGameSession.Load(save.ToJsonString()));
    }

    [Fact]
    public void SaveRestoresPendingSetupAndDeduplicatesShipPlacementWithoutExtraCost()
    {
        var game = New(); var actor = game.GetPlayerView("P1").ActivePlayerId; var view = game.GetPlayerView(actor);
        var vertex = view.LegalVertexIds.First(v => CoastVertex(view.Board, v));
        Accepted(game, Cmd(CommandKind.SetupSettlement, actor, vertex));
        var restored = SeafarersGameSession.Load(game.Save()); Assert.Equal(game.Save(), restored.Save());
        var ship = Cmd(CommandKind.SetupShip, actor, game.GetPlayerView(actor).LegalShipEdgeIds.First());
        var result = Accepted(game, ship); Accepted(restored, ship); Assert.Equal(game.Save(), restored.Save());
        var before = restored.Save(); var retry = restored.Execute(ship);
        Assert.True(retry.Success); Assert.True(retry.IsDuplicate); Assert.Empty(retry.NewEvents);
        Assert.Equal(RulesHarness.Canonical(result.Events), RulesHarness.Canonical(retry.Events)); Assert.Equal(before, restored.Save());
        var conflict = Cmd(CommandKind.SetupShip, actor, "E999999"); conflict.Id = ship.Id; Rejected(restored, conflict);
        var authority = restored.GetAuthoritativeStateForTesting();
        Assert.NotEmpty(authority.RulesVersion); Assert.NotEmpty(authority.ScenarioVersion); Assert.NotEmpty(authority.ScenarioContentHash);
        Assert.True(authority.Random.Draws > 0);
    }

    [Theory]
    [InlineData("fog-islands")]
    [InlineData("forgotten-tribe")]
    public void PlayerViewNeverContainsHiddenMapDeckRandomOrOtherHands(string scenario)
    {
        var game = New(scenario);
        foreach (var viewer in new[] { "P1", "P2" })
        {
            var json = RulesHarness.Node(game.GetPlayerView(viewer));
            var names = PropertyNames(json).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var forbidden in new[] { "Random", "State", "Seed", "InitialSeed", "Draws", "DevelopmentDeck", "HiddenResources", "HiddenNumbers", "GiftCards", "ProcessedCommands", "Fingerprint", "GoldReturnDecision" })
                Assert.DoesNotContain(forbidden, names);
            foreach (var player in json["players"]!.AsArray())
            {
                Assert.Null(player!["resources"]); Assert.Null(player["developmentCards"]);
                Assert.Null(player["homeRegions"]); Assert.Null(player["settledRegions"]);
            }
            if (scenario == "fog-islands")
            {
                var fog = game.GetPlayerView(viewer).Board.Tiles.Where(t => t.Resource == "fog").ToArray();
                Assert.NotEmpty(fog); Assert.All(fog, t => Assert.Null(t.Number));
            }
        }
        Assert.Equal(RulesHarness.Canonical(game.GetPlayerView("P1").Events), RulesHarness.Canonical(game.GetPlayerView("P2").Events));
    }

    [Fact]
    public void DetachedShipTileAndGoldViewsCannotAlterAuthority()
    {
        var game = New(); var actor = game.GetPlayerView("P1").ActivePlayerId; var view = game.GetPlayerView(actor);
        Accepted(game, Cmd(CommandKind.SetupSettlement, actor, view.LegalVertexIds.First(v => CoastVertex(view.Board, v))));
        var command = Cmd(CommandKind.SetupShip, actor, game.GetPlayerView(actor).LegalShipEdgeIds.First());
        var receipt = Accepted(game, command); var saved = game.Save(); view = game.GetPlayerView(actor);
        view.Board.Ships[0].BuiltTurn = 999; view.Board.Ships[0].PlayerId = "P4"; view.Board.Tiles[0].Resource = "invalid";
        view.Board.Tiles[0].Vertices[0] = "invalid"; view.OwnResources.Wood = 999; view.Players[0].Pieces.Roads = 999;
        view.Events[0].Message = "changed"; receipt.NewEvents[0].Message = "changed"; command.SourceId = "secret";
        var state = game.GetAuthoritativeStateForTesting(); state.Ships[0].IsWarship = true; state.Random.State = 99;
        Assert.Equal(saved, game.Save()); Invariants(game);
    }

    [Theory]
    [InlineData("unknownSeat")]
    [InlineData("wrongSeat")]
    [InlineData("wrongPhase")]
    [InlineData("unknownKind")]
    [InlineData("unknownResource")]
    [InlineData("negativeAmount")]
    [InlineData("overflowAmount")]
    [InlineData("unknownLocation")]
    public void MalformedOrUnauthorizedCommandsAreAtomic(string reason)
    {
        var game = Fixture(s => Player(s).Resources = Bag(wood: 4));
        var command = Cmd(CommandKind.BankTrade); command.GiveResource = Resource.Wood; command.ReceiveResource = Resource.Ore;
        switch (reason)
        {
            case "unknownSeat": command.PlayerId = "P99"; break;
            case "wrongSeat": command.PlayerId = "P2"; break;
            case "wrongPhase": command.Kind = CommandKind.RollDice; break;
            case "unknownKind": command.Kind = (CommandKind)999; break;
            case "unknownResource": command.ReceiveResource = (Resource)999; break;
            case "negativeAmount": command.ReceiveAmount = -1; break;
            case "overflowAmount": command.ReceiveAmount = int.MaxValue; command.GiveAmount = -4; break;
            case "unknownLocation": command.Kind = CommandKind.BuildShip; command.TargetId = "E999"; break;
        }
        Rejected(game, command);
    }

    [Fact]
    public void PreviewIsDetachedAndFixtureSaveCannotBeLoadedAsProduction()
    {
        var board = New().GetPlayerView("P1").Board; var edge = board.Edges.First(e => SeaEdge(board, e) && LandEdge(board, e));
        var game = Fixture(s => { s.PirateTileId = null!; s.Settlements = new[] { Piece(edge.Vertices[0]) }; Player(s).Resources = Bag(wood: 1, wool: 1); });
        var cmd = Cmd(CommandKind.BuildShip, target: edge.Id); var before = game.Save();
        Assert.True(game.Preview(cmd).Success); Assert.Equal(before, game.Save()); Accepted(game, cmd);
        Assert.Throws<InvalidDataException>(() => SeafarersGameSession.Load(game.Save()));
        Assert.Throws<InvalidDataException>(() => SeafarersGameSession.Load(M2Harness.New().Save()));
    }

    private static IEnumerable<string> PropertyNames(JsonNode node)
    {
        if (node is JsonObject obj)
            foreach (var item in obj) { yield return item.Key; if (item.Value != null) foreach (var child in PropertyNames(item.Value)) yield return child; }
        else if (node is JsonArray array)
            foreach (var item in array) if (item != null) foreach (var child in PropertyNames(item)) yield return child;
    }
}
