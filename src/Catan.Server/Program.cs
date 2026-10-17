using Catan.Network.Contracts;
using Catan.Rooms;
using Catan.Server;
using Microsoft.AspNetCore.Server.Kestrel.Core;

Console.OutputEncoding = new System.Text.UTF8Encoding(false);
string startupStage="OPTIONS";int startupPort=47771;
try
{
    var options = ServerOptions.Parse(args);
    startupPort=options.Port;startupStage="PRIVATE_FILES";
    using var privateFiles = new PrivateServerFiles(options);
    startupStage="ROOMS";
    var scenarioFile = Path.Combine(AppContext.BaseDirectory, "base-scenario.json");
    using var rooms = new RoomDirectory(Path.Combine(options.DataDirectory, "rooms"), File.ReadAllText(scenarioFile));
    startupStage="LISTEN";
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = Array.Empty<string>(), ContentRootPath = AppContext.BaseDirectory });
    // 默认关闭框架请求日志，禁止请求、令牌、口令和异常中的权威内容进入公开日志。
    builder.Logging.ClearProviders();
    builder.WebHost.ConfigureKestrel(kestrel =>
    {
        kestrel.AddServerHeader = false;
        kestrel.Limits.MaxRequestBodySize = options.MaxRequestBytes;
        kestrel.Limits.MaxConcurrentConnections = options.MaxConnections * 2;
        kestrel.Limits.MaxConcurrentUpgradedConnections = options.MaxConnections;
        kestrel.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
        kestrel.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(40);
        kestrel.ListenAnyIP(options.Port, listen => { listen.Protocols = HttpProtocols.Http1AndHttp2; listen.UseHttps(privateFiles.Certificate); });
    });
    builder.Services.AddSingleton(options);
    builder.Services.AddSingleton(rooms);
    builder.Services.AddSingleton<NetworkTransport>();
    builder.Services.AddHostedService<RoomAiService>();
    object Advertisement(int roomCount) => new
    {
        game = "卡坦岛之三国争霸", protocolVersion = NetworkProtocol.Version, serverName = options.ServerName,
        httpsPort = options.Port, certificateFingerprint = privateFiles.Fingerprint,
        rooms = rooms.ListPublicRooms().Take(roomCount).Select(room => new { roomId = room.RoomId, name = room.Name, ruleSet = room.RuleSet, scenarioId = room.ScenarioId, playerCount = room.PlayerCount, occupiedSeats = room.OccupiedSeats, started = room.Started, hasPassword = room.HasPassword }).ToArray()
    };
    builder.Services.AddSingleton<IHostedService>(_ => new LanDiscoveryService(options, Advertisement));
    var app = builder.Build();
    app.Use(async (context, next) =>
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (!context.Request.IsHttps || context.Request.QueryString.HasValue || context.Request.Headers.ContainsKey("Origin")) { context.Response.StatusCode = 400; return; }
        try { await next(context); }
        catch when (!context.Response.HasStarted) { context.Response.StatusCode = 503; await context.Response.WriteAsync("服务暂不可用，请稍后重新连接。"); }
    });
    app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(15), KeepAliveTimeout = TimeSpan.FromSeconds(15) });
    app.MapGet("/health", () => Results.Json(new { status = "ok", protocolVersion = NetworkProtocol.Version, serverName = options.ServerName, httpsPort = options.Port, certificateFingerprint = privateFiles.Fingerprint }));
    app.MapPost("/api/request", (HttpContext context, NetworkTransport transport) => transport.HttpAsync(context));
    app.Map("/ws", (HttpContext context, NetworkTransport transport) => transport.WebSocketAsync(context));
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        Console.WriteLine($"卡坦 M7 房间服务已启动，HTTPS 端口：{options.Port}；模式：{(options.Online ? "在线" : "局域网")}。");
        Console.WriteLine($"局域网证书 SHA-256 指纹：{privateFiles.Fingerprint}");
    });
    await app.RunAsync();
}
catch(Exception error)
{
    Console.Error.WriteLine("CATAN_SERVER_ERROR "+StartupDiagnostics.Describe(error,startupStage,startupPort));
    Environment.ExitCode = 1;
}
