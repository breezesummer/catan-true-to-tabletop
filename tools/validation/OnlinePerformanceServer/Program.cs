using Catan.Rooms;
using Catan.Server;

// 仅绑定本机回环的独立验证入口；复用正式传输、持久化、诊断与AI，不开放外部端口。
var root=new DirectoryInfo(AppContext.BaseDirectory);
while(root!=null&&!File.Exists(Path.Combine(root.FullName,"docs/acceptance/v0.1/scenario.json")))root=root.Parent;
string repo=root?.FullName??throw new DirectoryNotFoundException("没有找到仓库规则夹具。");
string local=Path.GetFullPath(Path.Combine(repo,".local"))+Path.DirectorySeparatorChar;
string data=Path.GetFullPath(Path.Combine(repo,".local/online-performance/server"));
if(!data.StartsWith(local,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("验证目录须位于本仓库.local内。");
Directory.CreateDirectory(data);
var options=new ServerOptions{Online=true,Port=47841,EnableDiscovery=false,DataDirectory=data,AiIntervalMs=300};
await using var diagnostics=new CommunicationDiagnostics(Path.Combine(data,"diagnostics"));
using var rooms=new RoomDirectory(Path.Combine(data,"private-rooms"),File.ReadAllText(Path.Combine(repo,"docs/acceptance/v0.1/scenario.json")),new RoomDirectoryOptions{Diagnostics=diagnostics.RecordRoom});
var builder=WebApplication.CreateBuilder();builder.Logging.ClearProviders();builder.WebHost.UseUrls("http://127.0.0.1:47841");
builder.Services.AddSingleton(options);builder.Services.AddSingleton(rooms);builder.Services.AddSingleton(diagnostics);builder.Services.AddSingleton<NetworkTransport>();builder.Services.AddHostedService<RoomAiService>();
var app=builder.Build();app.UseWebSockets();
app.MapGet("/health",()=>new{status="ok",scope="仅本机回环验证",protocolVersion=3});
app.MapPost("/api/request",(HttpContext context,NetworkTransport transport)=>transport.HttpAsync(context));
app.Map("/ws",(HttpContext context,NetworkTransport transport)=>transport.WebSocketAsync(context));
Console.WriteLine("独立验证服务：http://127.0.0.1:47841；Ctrl+C退出。");
await app.RunAsync();
