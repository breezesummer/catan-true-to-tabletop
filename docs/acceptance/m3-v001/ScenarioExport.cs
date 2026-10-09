using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Catan.Core.M3;

// Export only static scenario metadata. This program never opens authority saves.
var output = args.Length > 0 ? args[0] : ".local/m3/scenario-audit/actual.json";
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
var scenarios = SeafarersScenarios.Ids.SelectMany(id => new[] { 3, 4 }.Select(n => SeafarersScenarios.CreateSeeded(id, n, 2025))).ToArray();
File.WriteAllText(output, JsonSerializer.Serialize(scenarios, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("Exported " + scenarios.Length + " public scenario definitions to " + output);
