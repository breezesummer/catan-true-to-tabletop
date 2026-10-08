using System.Text.Json.Nodes;
using Catan.Core;
using Xunit;
using static Catan.Rules.Tests.RulesHarness;

namespace Catan.Rules.Tests;

public sealed class BoundaryTests
{
    [Fact]
    public void RuntimeTopologyPreservesAllFrozenIdsCoordinatesAndAdjacencies()
    {
        var board = Node(New().GetPlayerView("P1").Board);
        var expected = AcceptanceFixture.Read()["topology"]!;
        foreach (var name in new[] { "tiles", "vertices", "edges" })
            Assert.Equal(AcceptanceFixture.Normalize(expected[name]!).ToJsonString(),
                AcceptanceFixture.Normalize(board[name]!).ToJsonString());
        Assert.Equal(19, board["tiles"]!.AsArray().Count);
        Assert.Equal(54, board["vertices"]!.AsArray().Count);
        Assert.Equal(72, board["edges"]!.AsArray().Count);

        var uses = board["edges"]!.AsArray().ToDictionary(edge => edge!.Text("id"), _ => 0);
        foreach (var tile in board["tiles"]!.AsArray())
        {
            var vertices = tile!["vertices"]!.Texts();
            for (var i = 0; i < 6; i++)
            {
                var ends = new[] { vertices[i], vertices[(i + 1) % 6] }.Order().ToArray();
                var edge = Assert.Single(board["edges"]!.AsArray(), e => e!["vertices"]!.Texts().Order().SequenceEqual(ends));
                uses[edge!.Text("id")]++;
            }
        }
        Assert.Equal(30, uses.Values.Count(x => x == 1));
        Assert.Equal(42, uses.Values.Count(x => x == 2));
        Assert.All(uses.Values, x => Assert.InRange(x, 1, 2));
    }

    [Theory]
    [InlineData(17, "afterRoll")]
    [InlineData(18, "afterTrade")]
    public void EverySeatsProjectionContainsOnlyOwnTypedHandAndPublicOpponentCounts(int through, string checkpoint)
    {
        var game = Through(through);
        var fixture = AcceptanceFixture.Read();
        var expected = fixture["expected"]![checkpoint]!;
        foreach (var viewer in fixture["players"]!.Texts())
        {
            var view = Node(game.GetPlayerView(viewer));
            Assert.Equal(viewer, view.Text("playerId"));
            AssertResources(expected["hands"]![viewer]!, view["ownResources"]!);
            foreach (var item in view["players"]!.AsArray())
            {
                var player = item!;
                var id = player.Text("id");
                Assert.Equal(AcceptanceFixture.ResourceVector(expected["hands"]![id]!).Sum(), player.Number("resourceCount"));
                Assert.All(player.AsObject().Select(p => p.Key), key => Assert.Contains(key,
                    new[] { "id", "playerId", "resourceCount", "pieces", "remainingPieces", "victoryPoints" }));
            }
            var names = PropertyNames(view).ToArray();
            Assert.DoesNotContain(names, name => new[] { "random", "rng", "seed", "cursor", "dice", "deck", "deckOrder", "hands", "resources", "processedCommands", "secondSettlementVertices" }.Contains(name));
            Assert.Equal(1, names.Count(name => name == "ownResources"));
            Assert.NotEmpty(view["unsupportedFeatures"]!.AsArray());
        }
    }

    [Fact]
    public void PublicEventSchemaContainsOnlyConfirmedObservableFacts()
    {
        var game = Through(21);
        var events = Node(game.GetPlayerView("P1").Events).AsArray();
        Assert.NotEmpty(events);
        var permitted = new[] { "sequence", "commandId", "kind", "playerId", "targetId", "dice1", "dice2", "giveResource", "receiveResource", "giveAmount", "receiveAmount" };
        foreach (var entry in events)
        {
            Assert.All(entry!.AsObject().Select(property => property.Key), name => Assert.Contains(name, permitted));
            Assert.All(entry.AsObject().Select(property => property.Value), value => Assert.True(value is null || value is JsonValue));
        }
        var sequences = events.Select(e => e!["sequence"]!.GetValue<long>()).ToArray();
        Assert.Equal(sequences.Length, sequences.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, sequences.Length).Select(n => (long)n), sequences);
        var roll = Assert.Single(events, entry => entry!.Text("commandId") == "C17");
        Assert.Equal(new[] { 2, 3 }, new[] { roll!.Number("dice1"), roll!.Number("dice2") });
        var probe = Assert.Single(events, entry => entry!.Text("commandId") == "C21");
        Assert.Equal(new[] { 1, 1 }, new[] { probe!.Number("dice1"), probe!.Number("dice2") });
        foreach (var player in AcceptanceFixture.Read()["players"]!.Texts())
            Assert.Equal(Canonical(game.GetPlayerView("P1").Events), Canonical(game.GetPlayerView(player).Events));
    }

    [Fact]
    public void ViewsSnapshotsAndResultReceiptsCannotMutateTheAuthorityByAliasing()
    {
        var game = Through(16);
        var result = Accepted(game, MainCommands()[16]);
        var before = game.Save();
        var view = game.GetPlayerView("P1");
        view.OwnResources.Wood = 900;
        view.Bank.Ore = 900;
        view.Players[0].Pieces.Roads = 900;
        view.Board.Tiles[0].Vertices[0] = "BROKEN";
        view.Board.Vertices[0].X = 999;
        view.Board.Roads[0].PlayerId = "P4";
        view.Events[0].PlayerId = "P4";
        result.Events[0].PlayerId = "P4";
        result.NewEvents[0].Dice1 = 6;
        var authorityCopy = game.GetAuthoritativeStateForTesting();
        authorityCopy.Random.Cursor = 900;
        authorityCopy.Random.Dice[0][0] = 6;
        authorityCopy.Players[0].Resources.Wood = 900;
        authorityCopy.ProcessedCommands[0].Command.TargetId = "BROKEN";
        Assert.Equal(before, game.Save());
        Ledger(game, "afterRoll");
        var pristine = AcceptanceFixture.Read()["topology"]!["tiles"]![0]!["vertices"]![0]!.GetValue<string>();
        Assert.Equal(pristine, game.GetPlayerView("P1").Board.Tiles[0].Vertices[0]);
    }

    [Fact]
    public void AcceptedCommandReceiptDoesNotAliasCallerOwnedCommand()
    {
        var game = Through(18);
        var command = MainCommands()[18];
        Accepted(game, command);
        var before = game.Save();
        command.TargetId = "E70";
        command.Id = "CHANGED";
        Assert.Equal(before, game.Save());
    }

    [Theory]
    [InlineData("P0")]
    [InlineData("")]
    public void UnknownSeatCannotAcquirePlayerView(string playerId)
    {
        var game = Through(17);
        Assert.ThrowsAny<ArgumentException>(() => game.GetPlayerView(playerId));
    }

    private static IEnumerable<string> PropertyNames(JsonNode node)
    {
        if (node is JsonObject obj)
            foreach (var property in obj)
            {
                yield return property.Key;
                if (property.Value is not null)
                    foreach (var child in PropertyNames(property.Value)) yield return child;
            }
        else if (node is JsonArray array)
            foreach (var item in array)
                if (item is not null)
                    foreach (var child in PropertyNames(item)) yield return child;
    }
}
