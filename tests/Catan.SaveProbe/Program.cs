using System.Text.Json.Nodes;
using Catan.Core;

// This deliberately runs as two separate OS processes: the creator exits before
// a new loader restores the authoritative file and submits the fixture's C21.
if (args.Length != 5) throw new ArgumentException("Usage: write|read fixture save roundtrip result");
var scenario = File.ReadAllText(args[1]);
var fixture = JsonNode.Parse(scenario)!;
if (args[0] == "write")
{
    var session = GameSession.Create(scenario);
    foreach (var entry in fixture["setup"]!.AsArray())
    {
        Apply(session, new Command { Id = Text(entry!, "settlementCommandId"), PlayerId = Text(entry!, "player"), Kind = CommandKind.SetupSettlement, TargetId = Text(entry!, "vertex") });
        Apply(session, new Command { Id = Text(entry!, "roadCommandId"), PlayerId = Text(entry!, "player"), Kind = CommandKind.SetupRoad, TargetId = Text(entry!, "edge") });
    }
    foreach (var action in fixture["actions"]!.AsArray()) Apply(session, ReadCommand(action!));
    File.WriteAllText(args[2], session.Save());
}
else if (args[0] == "read")
{
    var session = GameSession.Load(scenario, File.ReadAllText(args[2]));
    File.WriteAllText(args[3], session.Save());
    Apply(session, ReadCommand(fixture["restoreProbe"]!));
    File.WriteAllText(args[4], session.Save());
}
else throw new ArgumentException("Unknown probe mode.");
Console.WriteLine($"Persistence probe {args[0]} completed in process {Environment.ProcessId}.");

static string Text(JsonNode node, string property) => node[property]!.GetValue<string>();
static void Apply(GameSession session, Command command)
{
    var result = session.Execute(command);
    if (!result.Success) throw new InvalidOperationException($"{command.Id} rejected: {result.ErrorCode}");
}
static Command ReadCommand(JsonNode node)
{
    var command = new Command { Id = Text(node, "commandId"), PlayerId = Text(node, "player"), Kind = Enum.Parse<CommandKind>(Text(node, "kind")), TargetId = node["edge"]?.GetValue<string>() };
    if (node["give"] is JsonObject give)
    {
        command.GiveResource = Enum.Parse<Resource>(give.Single().Key, true);
        command.GiveAmount = give.Single().Value!.GetValue<int>();
        command.ReceiveResource = Enum.Parse<Resource>(node["receive"]!.AsObject().Single().Key, true);
        command.ReceiveAmount = node["receive"]!.AsObject().Single().Value!.GetValue<int>();
    }
    return command;
}
