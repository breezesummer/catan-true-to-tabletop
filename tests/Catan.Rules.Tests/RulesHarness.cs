using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Catan.Core;
using Xunit;

namespace Catan.Rules.Tests;

internal static class RulesHarness
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        IncludeFields = true,
        Converters = { new JsonStringEnumConverter() }
    };

    internal static JsonNode Node(object data) => JsonSerializer.SerializeToNode(data, JsonOptions)!;
    internal static string Canonical(object data) => AcceptanceFixture.Normalize(Node(data)).ToJsonString();
    internal static JsonNode State(GameSession game) => Node(game.GetAuthoritativeStateForTesting());
    internal static GameSession New() => GameSession.Create(AcceptanceFixture.Json);

    internal static Command CommandFrom(JsonNode data)
    {
        var command = new Command
        {
            Id = data["commandId"]?.GetValue<string>() ?? data.Text("id"),
            PlayerId = data.Text("player"),
            Kind = data.Text("kind") switch
            {
                "PlaceSettlement" => CommandKind.SetupSettlement,
                "PlaceSetupRoad" => CommandKind.SetupRoad,
                var kind => Enum.Parse<CommandKind>(kind)
            },
            TargetId = data["vertex"]?.GetValue<string>() ?? data["edge"]?.GetValue<string>()
        };
        if (data["give"] is JsonObject give)
        {
            var item = give.Single();
            command.GiveResource = Enum.Parse<Resource>(item.Key, true);
            command.GiveAmount = item.Value!.GetValue<int>();
        }
        if (data["receive"] is JsonObject receive)
        {
            var item = receive.Single();
            command.ReceiveResource = Enum.Parse<Resource>(item.Key, true);
            command.ReceiveAmount = item.Value!.GetValue<int>();
        }
        return command;
    }

    internal static IReadOnlyList<Command> MainCommands()
    {
        var fixture = AcceptanceFixture.Read();
        var commands = new List<Command>();
        foreach (var item in fixture["setup"]!.AsArray())
        {
            var placement = item!;
            commands.Add(new Command { Id = placement.Text("settlementCommandId"), PlayerId = placement.Text("player"), Kind = CommandKind.SetupSettlement, TargetId = placement.Text("vertex") });
            commands.Add(new Command { Id = placement.Text("roadCommandId"), PlayerId = placement.Text("player"), Kind = CommandKind.SetupRoad, TargetId = placement.Text("edge") });
        }
        commands.AddRange(fixture["actions"]!.AsArray().Select(action => CommandFrom(action!)));
        commands.Add(CommandFrom(fixture["restoreProbe"]!));
        return commands;
    }

    internal static GameSession Through(int commandCount)
    {
        var game = New();
        foreach (var command in MainCommands().Take(commandCount))
            Accepted(game, command);
        return game;
    }

    internal static CommandResult Accepted(GameSession game, Command command)
    {
        var result = game.Execute(command);
        Assert.True(result.Success, $"{command.Id}: {result.ErrorCode}: {result.Message}");
        Assert.False(result.IsDuplicate);
        Assert.NotEmpty(result.NewEvents);
        Invariants(game);
        return result;
    }

    internal static void Invariants(GameSession game)
    {
        var fixture = AcceptanceFixture.Read();
        var state = State(game);
        var players = state["players"]!.AsArray();
        foreach (var resource in fixture["resourceOrder"]!.Texts())
        {
            var bank = state["bank"]!.Number(resource);
            Assert.True(bank >= 0);
            var held = players.Select(player => player!["resources"]!.Number(resource)).ToArray();
            Assert.All(held, number => Assert.True(number >= 0));
            Assert.Equal(fixture["initialBank"]!.Number(resource), bank + held.Sum());
        }
        foreach (var player in players)
        {
            var id = player!.Text("id");
            var pieces = player!["pieces"]!;
            var roadCount = state["roads"]!.AsArray().Count(piece => piece!.Text("playerId") == id);
            var settlementCount = state["settlements"]!.AsArray().Count(piece => piece!.Text("playerId") == id);
            Assert.True(pieces.Number("roads") >= 0);
            Assert.True(pieces.Number("settlements") >= 0);
            Assert.True(pieces.Number("cities") >= 0);
            Assert.Equal(fixture["initialPieces"]!.Number("roads"), roadCount + pieces.Number("roads"));
            Assert.Equal(fixture["initialPieces"]!.Number("settlements"), settlementCount + pieces.Number("settlements"));
            Assert.Equal(fixture["initialPieces"]!.Number("cities"), pieces.Number("cities"));
        }
        foreach (var collection in new[] { "roads", "settlements" })
        {
            var ids = state[collection]!.AsArray().Select(piece => piece!.Text("locationId")).ToArray();
            Assert.Equal(ids.Length, ids.Distinct().Count());
            var topology = fixture["topology"]![collection == "roads" ? "edges" : "vertices"]!.AsArray();
            Assert.All(ids, id => Assert.Contains(topology, element => element!.Text("id") == id));
        }
        Assert.Equal(fixture.Text("robberTile"), state.Text("robberTileId"));
    }

    internal static void Ledger(GameSession game, string checkpoint)
    {
        var expected = AcceptanceFixture.Read()["expected"]![checkpoint]!;
        var state = State(game);
        Assert.Equal(expected.Text("activePlayer"), state.Text("activePlayerId"));
        Assert.Equal(expected.Number("turn"), state.Number("turn"));
        Assert.Equal(expected.Text("phase"), state.Text("phase"));
        Assert.Equal(expected.Number("rngCursor"), state["random"]!.Number("cursor"));
        Assert.Null(state["pendingDecision"]);
        AssertResources(expected["bank"]!, state["bank"]!);
        foreach (var id in AcceptanceFixture.Read()["players"]!.Texts())
        {
            var player = state["players"]!.AsArray().Single(p => p!.Text("id") == id)!;
            AssertResources(expected["hands"]![id]!, player["resources"]!);
            AssertPieces(expected["remainingPieces"]![id]!, player["pieces"]!);
            var view = Node(game.GetPlayerView(id));
            var publicPlayer = view["players"]!.AsArray().Single(p => p!.Text("id") == id)!;
            Assert.Equal(expected["publicVictoryPoints"]![id]!.GetValue<int>(), publicPlayer.Number("victoryPoints"));
        }
        Invariants(game);
    }

    internal static void AssertResources(JsonNode expected, JsonNode actual) =>
        Assert.Equal(AcceptanceFixture.ResourceVector(expected), AcceptanceFixture.ResourceVector(actual));

    internal static void AssertPieces(JsonNode expected, JsonNode actual)
    {
        foreach (var name in new[] { "roads", "settlements", "cities" })
            Assert.Equal(expected.Number(name), actual.Number(name));
    }
}
