#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Catan.Network.Contracts;

internal sealed class M7LanService : IDisposable
{
    [Serializable] internal sealed class Advertisement
    {
        public string serverName="",certificateFingerprint="";
        public int protocolVersion=0,httpsPort=0;
        [NonSerialized] public string Address;
        [NonSerialized] public RoomSummary[] Rooms=Array.Empty<RoomSummary>();
        [NonSerialized] public string[] RememberedRooms=Array.Empty<string>();
        [NonSerialized] public string Error;
    }
    Process owned;
    Task<string> startup;
    volatile string startupError;
    volatile string hostWarning="";
    internal string HostWarning=>hostWarning;
    bool disposed;
    internal int HttpsPort { get; }
    internal int DiscoveryPort { get; }
    internal string Address=>"https://127.0.0.1:"+HttpsPort;
    internal M7LanService(int httpsPort=47771,int discoveryPort=47772){HttpsPort=httpsPort;DiscoveryPort=discoveryPort;}
    internal string DiscoveryIssues { get; private set; }="";
    internal bool CanHost=>Application.platform==RuntimePlatform.WindowsPlayer||Application.platform==RuntimePlatform.WindowsEditor;
    internal bool Running=>owned!=null&&!owned.HasExited;
    internal Task<string> Start(string dataDirectory)
    {
        if(startup==null||startup.IsCompleted)startup=StartCore(dataDirectory);
        return startup;
    }
    async Task<bool> Healthy(string fingerprint)
    {
        using(var handler=new HttpClientHandler{UseProxy=false,AllowAutoRedirect=false})
        {
            handler.ServerCertificateCustomValidationCallback=(request,certificate,chain,errors)=>certificate!=null&&M7Client.CertificateMatches(certificate.GetRawCertData(),fingerprint);
            using(var client=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(2)})
            {
                try
                {
                    using(var health=await client.GetAsync(Address+"/health"))
                    {
                        if(!health.IsSuccessStatusCode)return false;
                        var info=JsonUtility.FromJson<Advertisement>(await health.Content.ReadAsStringAsync());
                        if(info.protocolVersion!=NetworkProtocol.Version)throw new M7NetworkFailure("PROTOCOL","本机旧版服务仍在运行，请关闭旧版游戏后重试。");
                        return true;
                    }
                }
                catch(HttpRequestException){return false;}catch(TaskCanceledException){return false;}
            }
        }
    }
    async Task<string> StartCore(string dataDirectory)
    {
        if(disposed)throw new ObjectDisposedException(nameof(M7LanService));
        if(!CanHost)throw new M7NetworkFailure("HOST_PLATFORM","当前平台可搜索并加入电脑上的房间，托管服务需要 Windows。");
        string fingerprintPath=Path.Combine(dataDirectory,"server-fingerprint.txt");
        string fingerprint=File.Exists(fingerprintPath)?M7Client.NormalizeFingerprint(File.ReadAllText(fingerprintPath)):"";
        // 仅复用与本机保存证书一致且协议兼容的服务，不接管或终止其他进程。
        if(fingerprint.Length==64&&await Healthy(fingerprint))return fingerprint;
        if(disposed)throw new ObjectDisposedException(nameof(M7LanService));
        string file=Path.Combine(Path.GetDirectoryName(Application.dataPath),"Server","Catan.Server.exe");
        if(!File.Exists(file))throw new FileNotFoundException("缺少随包 Server/Catan.Server.exe，请使用完整发布目录。");
        if(!Running)
        {
            Directory.CreateDirectory(dataDirectory);
            owned?.Dispose();startupError=null;hostWarning="";
            owned=new Process{StartInfo=new ProcessStartInfo{FileName=file,Arguments="--mode lan --data-dir \""+dataDirectory+"\" --port "+HttpsPort+" --discovery-port "+DiscoveryPort,UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardError=true,WorkingDirectory=Path.GetDirectoryName(file)}};
            owned.ErrorDataReceived+=(sender,args)=>
            {
                const string failure="CATAN_SERVER_ERROR ",warning="CATAN_SERVER_WARNING ";
                if(args.Data==null)return;
                if(args.Data.StartsWith(failure,StringComparison.Ordinal))startupError=args.Data.Substring(failure.Length);
                else if(args.Data.StartsWith(warning,StringComparison.Ordinal))hostWarning=args.Data.Substring(warning.Length);
            };
            owned.Start();owned.BeginErrorReadLine();
        }
        for(int i=0;i<60;i++)
        {
            if(owned.HasExited)
            {
                owned.WaitForExit();
                throw new M7NetworkFailure("SERVER_EXIT_"+owned.ExitCode,string.IsNullOrEmpty(startupError)?"随包服务提前退出，请确认游戏目录完整、目录可写；也可能是旧服务占用 TCP "+HttpsPort+"。":startupError);
            }
            if(File.Exists(fingerprintPath))
            {
                fingerprint=M7Client.NormalizeFingerprint(File.ReadAllText(fingerprintPath));
                if(fingerprint.Length==64&&await Healthy(fingerprint)&&!owned.HasExited)return fingerprint;
            }
            await Task.Delay(250);
        }
        throw new M7NetworkFailure("SERVER_TIMEOUT","本机服务未及时响应 TCP "+HttpsPort+" 健康检查，请检查服务进程、证书与端口。仍可搜索朋友的房间。");
    }
    internal async Task<Advertisement[]> Discover(int discoveryPort=47772,Func<string,string[]> rememberedRooms=null)
    {
        var found=new Dictionary<string,Advertisement>();var issues=new List<string>();DiscoveryIssues="";
        var packets=await M7LanDiscovery.Search(discoveryPort,ex=>issues.Add(M7NetworkErrors.Describe("发送搜索请求",ex)));
        foreach(var packet in packets)
        {
            try
            {
                var ad=JsonUtility.FromJson<Advertisement>(Encoding.UTF8.GetString(packet.Buffer));
                if(ad==null||ad.httpsPort<1||ad.httpsPort>65535||M7Client.NormalizeFingerprint(ad.certificateFingerprint).Length!=64)continue;
                if(ad.protocolVersion!=NetworkProtocol.Version){issues.Add("发现其他版本的房间服务，请让所有玩家更新至同一版本。[PROTOCOL]");continue;}
                ad.certificateFingerprint=M7Client.NormalizeFingerprint(ad.certificateFingerprint);
                ad.Address="https://"+packet.RemoteEndPoint.Address+":"+ad.httpsPort;found[ad.Address]=ad;
                if(found.Count>=32)break;
            }
            catch(Exception){/* 忽略无效广播包，不影响其他服务。 */}
        }
        var services=found.Values.ToArray();
        var remembered=new Dictionary<string,string[]>();
        foreach(var service in services)
        {
            string pin=M7Client.NormalizeFingerprint(service.certificateFingerprint);
            if(!remembered.TryGetValue(pin,out var ids)){ids=rememberedRooms?.Invoke(pin)??Array.Empty<string>();remembered[pin]=ids;}
            service.RememberedRooms=ids;
        }
        await Task.WhenAll(services.Select(ReadRooms));
        issues.AddRange(services.Where(ad=>!string.IsNullOrEmpty(ad.Error)).Select(ad=>ad.Address+"："+ad.Error));
        DiscoveryIssues=string.Join("\n",issues.Distinct().Take(3));
        return services;
    }
    // 局域网发现直接绑定广播返回的证书，玩家无需复制或核对指纹。
    static async Task ReadRooms(Advertisement ad)
    {
        try
        {
            string pin=M7Client.NormalizeFingerprint(ad.certificateFingerprint);
            using(var handler=new HttpClientHandler{UseProxy=false,AllowAutoRedirect=false})
            {
                handler.ServerCertificateCustomValidationCallback=(request,certificate,chain,errors)=>certificate!=null&&M7Client.CertificateMatches(certificate.GetRawCertData(),pin);
                using(var client=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(3)})
                using(var content=new StringContent(WireJson.Serialize(new NetworkRequest{Operation="List",RequestId=Guid.NewGuid().ToString("N")}),Encoding.UTF8,"application/json"))
                using(var response=await client.PostAsync(ad.Address+"/api/request",content))
                {
                    M7NetworkErrors.RequireHttp(response.StatusCode);
                    if(response.Content.Headers.ContentLength>NetworkProtocol.MaximumResponseBytes)throw new M7NetworkFailure("RESPONSE_SIZE","房间列表超过大小限制，请联系房主。");
                    using(var stream=await response.Content.ReadAsStreamAsync())using(var bytes=new MemoryStream())
                    {
                        var buffer=new byte[8192];int count;
                        while((count=await stream.ReadAsync(buffer,0,buffer.Length))>0){if(bytes.Length+count>NetworkProtocol.MaximumResponseBytes)throw new M7NetworkFailure("RESPONSE_SIZE","房间列表超过大小限制。");bytes.Write(buffer,0,count);}
                        var result=WireJson.Deserialize<NetworkResponse>(Encoding.UTF8.GetString(bytes.ToArray()));
                        if(result==null)throw new M7NetworkFailure("RESPONSE_FORMAT","服务返回无效的房间列表。");
                        if(result.ProtocolVersion!=NetworkProtocol.Version)throw new M7NetworkFailure("PROTOCOL","房主与本机联机版本不同，请更新至同一版本。");
                        if(!result.Success)throw new M7NetworkFailure(result.Code??"LIST_REJECTED",result.Message??"服务拒绝读取房间列表。");
                        if(result.Success&&result.ProtocolVersion==NetworkProtocol.Version)
                        {
                            ad.Rooms=result.Rooms??Array.Empty<RoomSummary>();
                            // 全员离线的房间不公开广播；只为本设备已保存的身份取回仍在进行的原桌。
                            var recovery=await Task.WhenAll(ad.RememberedRooms.Except(ad.Rooms.Select(room=>room.RoomId)).Select(async roomId=>
                            {
                                try
                                {
                                    using(var body=new StringContent(WireJson.Serialize(new NetworkRequest{Operation="Describe",RoomId=roomId,RequestId=Guid.NewGuid().ToString("N")}),Encoding.UTF8,"application/json"))
                                    using(var described=await client.PostAsync(ad.Address+"/api/request",body))
                                    {
                                        if(!described.IsSuccessStatusCode||described.Content.Headers.ContentLength>NetworkProtocol.MaximumResponseBytes)return null;
                                        using(var source=await described.Content.ReadAsStreamAsync())using(var target=new MemoryStream())
                                        {
                                            var chunk=new byte[8192];int length;
                                            while((length=await source.ReadAsync(chunk,0,chunk.Length))>0){if(target.Length+length>NetworkProtocol.MaximumResponseBytes)return null;target.Write(chunk,0,length);}
                                            var detail=WireJson.Deserialize<NetworkResponse>(Encoding.UTF8.GetString(target.ToArray()));
                                            return detail.Success&&detail.ProtocolVersion==NetworkProtocol.Version&&detail.Room!=null&&detail.Room.Started&&!detail.Room.Finished?detail.Room:null;
                                        }
                                    }
                                }
                                catch(Exception){return null;}
                            }));
                            ad.Rooms=ad.Rooms.Concat(recovery.Where(room=>room!=null)).ToArray();
                        }
                    }
                }
            }
        }
        catch(Exception ex){ad.Rooms=Array.Empty<RoomSummary>();ad.Error=M7NetworkErrors.Describe("读取房间列表",ex);}
    }
    public void Dispose()
    {
        disposed=true;
        if(owned==null)return;
        try{if(!owned.HasExited)owned.Kill();}catch(InvalidOperationException){}finally{owned.Dispose();owned=null;}
    }
}
