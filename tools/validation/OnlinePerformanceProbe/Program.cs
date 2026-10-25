using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Catan.Core;
using Catan.Network.Contracts;
using M5 = Catan.Core.M5;

// 数据全部为固定种子与合成公开历史；不读取玩家凭据或真实存档。
bool legacy = args.Contains("--legacy");
int outputAt = Array.IndexOf(args, "--output");
string output = outputAt < 0 ? ".local/online-performance/decode.json" : args[outputAt + 1];
object Decode(string json, Type type) => legacy ? WireJson.DeserializeLegacy(json, type) : WireJson.Deserialize(json, type);
var reports = new List<object>();
string view = WireJson.Serialize(M5.CombinedGameSession.Create("through-the-desert", 4, 97).GetPlayerView("P1"));
foreach (int target in new[] { 10 * 1024, 70 * 1024, 250 * 1024, 1024 * 1024 })
{
    Console.WriteLine("开始档位："+target+" 字节");
    // 最小档仅含历史；其余包含真实四席组合地图投影与同格式历史。
    var response = new NetworkResponse { Success = true, Revision = 42, RuleSet = "combined", ViewJson = target > 10240 ? view : null };
    var entries = new List<GameHistoryEntry>();
    int estimatedBytes=Encoding.UTF8.GetByteCount(WireJson.Serialize(response));
    var encodedEntries=new List<string>();
    while(estimatedBytes<target)
    {
        int n = entries.Count + 1;
        entries.Add(WireJson.Deserialize<GameHistoryEntry>("{\"Sequence\":" + n + ",\"Kind\":\"BuildRoad\",\"Action\":\"公开建造记录\",\"PlayerId\":\"P1\",\"TargetId\":\"E001\",\"Changes\":[{\"Category\":\"resource\",\"GoodIndex\":0,\"Amount\":-1,\"Name\":\"木材\"}]}"));
        string item=WireJson.Serialize(entries[^1]);encodedEntries.Add(item);
        estimatedBytes+=Encoding.UTF8.GetByteCount(item)+item.Count(c=>c=='"'||c=='\\')+1;
    }
    response.PresentationHistoryJson="["+string.Join(",",encodedEntries)+"]";
    string wire=WireJson.Serialize(response);
    void Parse()
    {
        var decoded = (NetworkResponse)Decode(wire, typeof(NetworkResponse));
        if (decoded.ViewJson != null) Decode(decoded.ViewJson, typeof(M5.PlayerView));
        Decode(decoded.PresentationHistoryJson, typeof(GameHistoryEntry[]));
    }
    Sample Measure()
    {
        long allocated = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
        Parse();
        return new Sample(Stopwatch.GetElapsedTime(start).TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - allocated);
    }
    var cold = Measure();
    Console.WriteLine("首次解析："+cold.Ms.ToString("F2")+" 毫秒");
    int samples=target>=1024*1024?3:target>=250*1024?10:30;
    for (int i = 0; i < (samples<30?1:5); i++) Parse();
    var serial = Enumerable.Range(0, samples).Select(_ => Measure()).ToArray();
    int concurrentSamples=samples<30?1:30;
    var parallel = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => Enumerable.Range(0, concurrentSamples).Select(_ => Measure()).ToArray())));
    reports.Add(new { targetBytes = target, bytes = Encoding.UTF8.GetByteCount(wire), sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(wire))), historyEntries = entries.Count, cold, serial = Summary(serial), concurrentFour = Summary(parallel.SelectMany(s => s).ToArray()) });
    Console.WriteLine("完成档位："+target);
}
var report = new { scope = "同机 .NET 10：外层回复、席位视图与完整必要历史解析；纯解析不含网络、Unity帧率及解码调度。冷样本仅为本进程首次处理各档，类型可能已预热。", decoder = legacy ? "legacy-xml" : "direct-json", runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription, processors = Environment.ProcessorCount, assemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(WireJson).Assembly.Location))), reports };
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
await File.WriteAllTextAsync(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("解码对照已保存：" + Path.GetFullPath(output));
static object Summary(Sample[] values)
{
    var ms = values.Select(v => v.Ms).Order().ToArray();
    return new { count = values.Length, p50Ms = ms[(int)Math.Ceiling(ms.Length * .5) - 1], p95Ms = ms[(int)Math.Ceiling(ms.Length * .95) - 1], p99Ms = ms[(int)Math.Ceiling(ms.Length * .99) - 1], maxMs = ms[^1], meanAllocatedBytes = values.Average(v => v.Bytes) };
}
record Sample(double Ms, long Bytes);
