namespace Catan.Server;

public sealed record ServerOptions
{
    public string DataDirectory { get; init; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CatanThreeKingdoms", "M7Server");
    public string ServerName { get; init; } = "卡坦岛之三国争霸";
    public int Port { get; init; } = 47771;
    public int DiscoveryPort { get; init; } = 47772;
    public bool EnableDiscovery { get; init; } = true;
    public bool Online { get; init; }
    public string? CertificatePath { get; init; }
    public int MaxConnections { get; init; } = 64;
    public int MaxRequestBytes { get; init; } = 65536;
    public int AiIntervalMs { get; init; } = 300;
    public int RequestLimitPer10Seconds { get; init; } = 300;
    public const string DiscoveryQuery = "CATAN_M7_DISCOVER_V1";

    public static ServerOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; ++i)
        {
            if (args[i] == "--disable-discovery" || args[i] == "--no-discovery") values["--disable-discovery"] = "true";
            else if (args[i].StartsWith("--", StringComparison.Ordinal) && i + 1 < args.Length) values[args[i]] = args[++i];
            else throw new ArgumentException("启动参数错误，请查看 docs/M7_SERVER.md。");
        }
        string Read(string key, string fallback) => values.GetValueOrDefault(key, fallback);
        int PortValue(string key, int fallback) => int.TryParse(Read(key, fallback.ToString(System.Globalization.CultureInfo.InvariantCulture)), out var p) && p is >= 1 and <= 65535 ? p : throw new ArgumentException("端口必须在 1–65535 之间。");
        var mode = Read("--mode", "lan");
        if (mode != "lan" && mode != "online") throw new ArgumentException("运行模式须为 lan 或 online。");
        var known = new HashSet<string> { "--data-dir", "--server-name", "--port", "--discovery-port", "--disable-discovery", "--mode", "--certificate", "--ai-interval-ms", "--request-limit-per-10s" };
        if (values.Keys.Any(key => !known.Contains(key))) throw new ArgumentException("含不支持的启动参数。");
        var options = new ServerOptions
        {
            DataDirectory = Path.GetFullPath(Read("--data-dir", new ServerOptions().DataDirectory)),
            ServerName = Read("--server-name", "卡坦岛之三国争霸"),
            Port = PortValue("--port", 47771),
            DiscoveryPort = PortValue("--discovery-port", 47772),
            EnableDiscovery = mode == "lan" && !values.ContainsKey("--disable-discovery"),
            Online = mode == "online",
            CertificatePath = values.GetValueOrDefault("--certificate"),
            AiIntervalMs = int.TryParse(Read("--ai-interval-ms", "300"), out var interval) && interval >= 10 && interval <= 10000 ? interval : throw new ArgumentException("AI 间隔须在 10–10000 毫秒之间。"),
            RequestLimitPer10Seconds = int.TryParse(Read("--request-limit-per-10s", "300"), out var rate) && rate >= 1 && rate <= 100000 ? rate : throw new ArgumentException("每十秒请求上限须在 1–100000 之间。")
        };
        if (options.ServerName.Length is < 1 or > 64) throw new ArgumentException("服务名称须为 1–64 个字符。");
        if (options.Online && options.CertificatePath != null) throw new ArgumentException("在线服务使用 HTTP / WebSocket，无需证书；请移除 --certificate 参数。");
        return options;
    }
}
