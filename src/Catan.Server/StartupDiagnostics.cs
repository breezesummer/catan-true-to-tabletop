using System.Net.Sockets;
using System.Security.Cryptography;

namespace Catan.Server;

public static class StartupDiagnostics
{
    public static string Describe(Exception error,string stage,int port)
    {
        for(Exception? current=error;current!=null;current=current.InnerException)
        {
            if(current is ServerStartupException known)return known.Code+"："+known.Message;
            if(current is SocketException socket)
                return socket.SocketErrorCode==SocketError.AddressAlreadyInUse?$"TCP_PORT_IN_USE：TCP {port} 已被其他进程占用，请关闭旧版游戏或重复服务。":$"SOCKET_{socket.NativeErrorCode}：服务无法监听 TCP {port}，请检查系统网络权限和端口保留。";
            if(current is UnauthorizedAccessException)return "ACCESS：服务无法访问私有目录或证书，请检查目录权限。";
            if(current is CryptographicException)return "CERTIFICATE：服务证书或密钥无法读取，请恢复完整私有服务目录，并检查系统时间与证书。";
        }
        return stage switch
        {
            "OPTIONS"=>"OPTIONS：服务启动参数无效，请检查端口、模式和证书配置。",
            "PRIVATE_FILES"=>"PRIVATE_FILES：私有服务目录、证书或密钥不完整，请检查权限并恢复备份。",
            "ROOMS"=>"ROOMS：房间存档无法读取，请保留存档并检查备份与版本。",
            _=>"STARTUP：服务启动异常，请检查游戏目录完整性与服务运行环境。"
        };
    }
}

public sealed class ServerStartupException(string code,string message) : IOException(message)
{
    public string Code { get; }=code;
}
