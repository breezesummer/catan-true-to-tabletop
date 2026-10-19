using System;
using System.Net;

// 在线大厅直接使用服务器 IP；不自动将已填写的 HTTPS 地址降级为明文。
public static class M7OnlineEndpoint
{
    public const int DefaultPort=47771;
    public const string DefaultEndpoint="http://175.178.212.142:47771";
    public static string Normalize(string address)
    {
        string value=(address??"").Trim();
        if(value.Length==0)throw InvalidAddress();
        if(value.IndexOf("://",StringComparison.Ordinal)<0)value="http://"+value;
        if(!Uri.TryCreate(value,UriKind.Absolute,out var uri)||uri.Scheme!="http"||
            !string.IsNullOrEmpty(uri.UserInfo)||uri.AbsolutePath!="/"||
            !string.IsNullOrEmpty(uri.Query)||!string.IsNullOrEmpty(uri.Fragment))throw InvalidAddress();
        string host=uri.Host.Trim('[',']');
        if(!IPAddress.TryParse(host,out var ip)||host.IndexOf('%')>=0||
            uri.HostNameType!=UriHostNameType.IPv4&&uri.HostNameType!=UriHostNameType.IPv6)throw InvalidAddress();
        // 未填写端口时使用游戏端口；显式 :80 仍保持为 80。
        string authority=value.Substring(value.IndexOf("://",StringComparison.Ordinal)+3).TrimEnd('/');
        bool explicitPort=authority.StartsWith("[",StringComparison.Ordinal)?authority.IndexOf("]:",StringComparison.Ordinal)>=0:authority.IndexOf(':')>=0;
        int port=explicitPort?uri.Port:DefaultPort;
        if(port<1||port>65535)throw InvalidAddress();
        return new UriBuilder("http",ip.ToString(),port).Uri.GetLeftPart(UriPartial.Authority);
    }
    public static Uri WebSocketUri(string endpoint)
    {
        var uri=new Uri(endpoint);
        if(uri.Scheme!="http"&&uri.Scheme!="https")throw InvalidAddress();
        return new UriBuilder(uri){Scheme=uri.Scheme=="https"?"wss":"ws",Port=uri.Port,Path="/ws",Query="",Fragment=""}.Uri;
    }
    static M7NetworkFailure InvalidAddress()=>new M7NetworkFailure("ADDRESS","请填写服务器 IP:端口，例如 203.0.113.10:47771，也可填写 http://IP:端口；无需域名或证书。");
}

public enum M7LobbyConnectionState {Unconfigured,Connecting,Online,Offline}
