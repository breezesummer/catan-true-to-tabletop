using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Catan.Server;

public sealed class LanDiscoveryService(ServerOptions options, Func<int, object> publicAdvertisement) : BackgroundService
{
    private readonly PeerRateLimiter limiter = new(1024);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.EnableDiscovery) return;
        using var listener = new UdpClient(AddressFamily.InterNetwork);
        listener.Client.ExclusiveAddressUse = true;
        try { listener.Client.Bind(new IPEndPoint(IPAddress.Any, options.DiscoveryPort)); }
        catch (SocketException error)
        {
            Console.Error.WriteLine(error.SocketErrorCode==SocketError.AddressAlreadyInUse
                ?$"CATAN_SERVER_WARNING UDP_PORT_IN_USE：UDP {options.DiscoveryPort} 已被占用，好友可能无法发现本机房间，请关闭重复服务或旧版游戏后重新进入局域网。"
                :$"CATAN_SERVER_WARNING UDP_{error.NativeErrorCode}：系统拒绝监听发现端口 UDP {options.DiscoveryPort}，请检查网络权限与端口保留设置。");
            return;
        }
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var query = await listener.ReceiveAsync(stoppingToken);
                // 限制发现于局域网源地址，拒绝广播/互联网源地址，避免反射放大。
                if (!IsPrivate(query.RemoteEndPoint.Address) || query.Buffer.Length != ServerOptions.DiscoveryQuery.Length || Encoding.ASCII.GetString(query.Buffer) != ServerOptions.DiscoveryQuery) continue;
                if (!limiter.Allow(query.RemoteEndPoint.Address.ToString(), 4, 10)) continue;
                var jsonOptions = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                for (var roomCount = 3; roomCount >= 0; --roomCount)
                {
                    var json = JsonSerializer.SerializeToUtf8Bytes(publicAdvertisement(roomCount), jsonOptions);
                    if (json.Length > 1200) continue;
                    await listener.SendAsync(json, query.RemoteEndPoint, stoppingToken);
                    break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (SocketException) { /* 网络接口切换时，下一次发现仍可重试。 */ }
        }
    }

    private static bool IsPrivate(IPAddress address)
    {
        var bytes = address.MapToIPv4().GetAddressBytes();
        return bytes[0] == 127 || bytes[0] == 10 || bytes[0] == 192 && bytes[1] == 168 || bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31 || bytes[0] == 169 && bytes[1] == 254;
    }
}
