using System.Text.Json.Nodes;

namespace Catan.Rules.Tests;

/// <summary>
/// Reads the versioned acceptance ledger independently of the production parser.
/// Expectations always come from the frozen fixture, never from a second rules engine.
/// </summary>
internal static class AcceptanceFixture
{
    internal static string PathOnDisk => Path.Combine(AppContext.BaseDirectory, "scenario.json");
    internal static string Json => File.ReadAllText(PathOnDisk);
    internal static JsonObject Read() => JsonNode.Parse(Json)!.AsObject();
    internal static string Text(this JsonNode node, string key) => node[key]!.GetValue<string>();
    internal static int Number(this JsonNode node, string key) => node[key]!.GetValue<int>();
    internal static string[] Texts(this JsonNode node) => node.AsArray().Select(x => x!.GetValue<string>()).ToArray();
    internal static int[] ResourceVector(JsonNode node) => Read()["resourceOrder"]!.Texts()
        .Select(resource => node[resource]?.GetValue<int>() ?? 0).ToArray();

    internal static JsonNode Normalize(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var result = new JsonObject();
            foreach (var item in obj.OrderBy(x => x.Key, StringComparer.Ordinal))
                result[item.Key] = item.Value is null ? null : Normalize(item.Value);
            return result;
        }
        if (node is JsonArray array)
            return new JsonArray(array.Select(x => x is null ? null : Normalize(x)).ToArray());
        return node.DeepClone();
    }
}
