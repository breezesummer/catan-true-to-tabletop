using System;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading.Tasks;

// 仅展示受控说明与错误码，异常原文可能含凭据、请求或私有路径。
internal static class M7NetworkErrors
{
    internal static string Describe(string stage,Exception error)
    {
        for(var current=error;current!=null;current=current.InnerException)
        {
            if(current is M7NetworkFailure known)return stage+"失败 ["+known.Code+"]："+known.Message;
            if(current is AuthenticationException||current.GetType().FullName=="Mono.Security.Interface.TlsException")
                return stage+"失败 [TLS]：主机证书校验或加密握手失败，请重新搜索，并确认双方游戏版本和系统时间。";
            if(current is SocketException socket)
            {
                string advice;
                switch(socket.SocketErrorCode)
                {
                    case SocketError.AddressAlreadyInUse:advice="端口已被占用，请关闭旧版游戏或重复启动的房间服务。";break;
                    case SocketError.AccessDenied:advice="系统拒绝网络访问，请检查游戏的网络权限、安全软件或端口保留设置。";break;
                    case SocketError.NetworkUnreachable:case SocketError.HostUnreachable:case SocketError.NetworkDown:advice="网络或服务器不可达，请检查本机网络、服务器公网 IP、VPN／虚拟网卡。";break;
                    case SocketError.ConnectionRefused:advice="服务器未接受连接，请确认服务已启动、公网 IP 和 TCP 端口正确且已放行。";break;
                    case SocketError.ConnectionReset:advice="服务器或系统中断了连接，请稍后重试；局域网可重新搜索主机。";break;
                    case SocketError.HostNotFound:advice="找不到主机地址，请检查地址或网络 DNS。";break;
                    case SocketError.TimedOut:advice="主机响应超时，请确认房主在线、网络可达和端口放行。";break;
                    default:advice="网络通信异常，请检查网络后重试。";break;
                }
                return stage+"失败 [Socket "+socket.NativeErrorCode+"]："+advice;
            }
        }
        if(error is TaskCanceledException||error is TimeoutException)return stage+"失败 [TIMEOUT]：等待服务响应超时，请确认房主在线、网络可达后重试。";
        if(error is UriFormatException)return stage+"失败 [ADDRESS]：服务地址格式无效；在线填写服务器 IP:端口，局域网请重新搜索。";
        if(error is UnauthorizedAccessException)return stage+"失败 [ACCESS]：无法访问本机服务或身份保存目录，请检查目录权限。";
        if(error is FileNotFoundException)return stage+"失败 [MISSING_FILE]：缺少随包房间服务文件，请使用完整游戏目录。";
        if(error is IOException)return stage+"失败 [IO]：本机文件或连接无法读写，请检查目录权限和服务状态。";
        if(error is HttpRequestException)return stage+"失败 [HTTP]：无法连接房间服务器，请确认服务器在线、公网 IP 正确且 TCP 端口已放行。";
        return stage+"失败 ["+error.GetType().Name+"]：请重试；若持续发生，请提供此错误码与发生步骤。";
    }

    internal static void RequireHttp(System.Net.HttpStatusCode status)
    {
        int code=(int)status;if(code>=200&&code<300)return;
        throw new M7NetworkFailure("HTTP "+code,code==429?"请求过于频繁，请稍候再试。":code==503?"房间服务暂时不可用，请稍候重试。":"房间服务返回异常响应，请确认服务地址和版本。");
    }
}

internal sealed class M7NetworkFailure : Exception
{
    internal string Code { get; }
    internal M7NetworkFailure(string code,string message):base(message){Code=code;}
}
