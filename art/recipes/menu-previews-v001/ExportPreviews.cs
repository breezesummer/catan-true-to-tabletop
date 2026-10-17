using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using Catan.Core.M3;
using Catan.Core.M5;

// 只导出公开地图定义，绝不创建对局、读取存档或导出隐藏资源池及随机状态。
var root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
var output = Path.Combine(root, "art/recipes/menu-previews-v001/public-layouts.json");
var maps = new List<object>();
foreach (int rule in new[] { 1, 3 })
foreach (string id in rule == 1 ? SeafarersScenarios.Ids : CombinedScenarios.Ids)
foreach (int players in new[] { 3, 4 })
{
    var s = rule == 1 ? SeafarersScenarios.Create(id, players) : CombinedScenarios.Create(id, players);
    maps.Add(new {
        rule, id, players, name = s.Name, sourceFile = "catan-seafarers-2025-en.pdf", sourcePage = s.SourcePage,
        randomExample = id == "new-world" || rule == 3,
        tiles = s.Tiles.Select(t => new { id = t.Id, q = t.Q, r = t.R, resource = t.Resource, number = t.Resource == "fog" ? null : t.Number }),
        villages = s.Villages.Select(v => s.Vertices.Single(x => x.Id == v.VertexId)).Select(v => new { x = v.X, y = v.Y }),
        fortresses = s.Fortresses.Select(v => s.Vertices.Single(x => x.Id == v.VertexId)).Select(v => new { x = v.X, y = v.Y }),
        wonders = s.Wonders.SelectMany(w => w.VertexIds).Distinct().Select(id => s.Vertices.Single(x => x.Id == id)).Select(v => new { x = v.X, y = v.Y })
    });
}
using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Unity/Catan/Assets/Resources/m1-scenario.json")));
foreach (int rule in new[] { 0, 2 })
foreach (int players in new[] { 3, 4 })
{
    maps.Add(new {
        rule, id = "standard", players, name = rule == 0 ? "标准岛屿" : "标准城邦",
        sourceFile = "catan-base-2025-en.pdf", sourcePage = 3, randomExample = true,
        tiles = fixture.RootElement.GetProperty("topology").GetProperty("tiles").EnumerateArray().Select(t => new {
            id = t.GetProperty("id").GetString(), q = t.GetProperty("q").GetInt32(), r = t.GetProperty("r").GetInt32(),
            resource = t.GetProperty("resource").GetString() ?? "desert", number = t.TryGetProperty("number", out var n) && n.ValueKind == JsonValueKind.Number ? (int?)n.GetInt32() : null
        }).ToArray(), villages = Array.Empty<object>(), fortresses = Array.Empty<object>(), wonders = Array.Empty<object>()
    });
}
File.WriteAllText(output, JsonSerializer.Serialize(new { schemaVersion = 1, privacy = "固定公开示意；迷雾仅显示迷雾，不包含隐藏资源池、真实局面或随机状态", maps }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("已导出 " + maps.Count + " 张公开地图示意定义。");
