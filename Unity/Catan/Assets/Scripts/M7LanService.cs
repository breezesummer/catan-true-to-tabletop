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
        public string serverName,certificateFingerprint;
        public int protocolVersion,httpsPort;
        [NonSerialized] public string Address;
        [NonSerialized] public RoomSummary[] Rooms=Array.Empty<RoomSummary>();
        [NonSerialized] public string[] RememberedRooms=Array.Empty<string>();
    }
    Process owned;
    internal bool Running=>owned!=null&&!owned.HasExited;
    internal async Task<string> Start(string dataDirectory)
    {
        if(Application.platform!=RuntimePlatform.WindowsPlayer&&Application.platform!=RuntimePlatform.WindowsEditor)throw new InvalidOperationException("随包局域网房主服务目前支持 Windows。");
        string file=Path.Combine(Path.GetDirectoryName(Application.dataPath),"Server","Catan.Server.exe");
        if(!File.Exists(file))throw new FileNotFoundException("缺少随包 Server/Catan.Server.exe，请使用完整发布目录。");
        if(!Running)
        {
            Directory.CreateDirectory(dataDirectory);
            owned=Process.Start(new ProcessStartInfo{FileName=file,Arguments="--mode lan --data-dir \""+dataDirectory+"\" --port 47771 --discovery-port 47772",UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Path.GetDirectoryName(file)});
        }
        string fingerprintPath=Path.Combine(dataDirectory,"server-fingerprint.txt");
        for(int i=0;i<60;i++)
        {
            if(owned.HasExited)throw new InvalidOperationException("房间服务启动失败，端口可能被占用；请检查服务日志与防火墙。");
            if(File.Exists(fingerprintPath))
            {
                string fingerprint=M7Client.NormalizeFingerprint(File.ReadAllText(fingerprintPath));
                if(fingerprint.Length==64)
                {
                    using(var handler=new HttpClientHandler{UseProxy=false,AllowAutoRedirect=false})
                    {
                        handler.ServerCertificateCustomValidationCallback=(request,certificate,chain,errors)=>
                        {
                            var actual=request.RequestUri.IsAbsoluteUri?request.RequestUri:new Uri(new Uri("https://127.0.0.1:47771"),request.RequestUri);
                            return actual.Scheme=="https"&&actual.Host=="127.0.0.1"&&actual.Port==47771&&certificate!=null&&M7Client.CertificateMatches(certificate.GetRawCertData(),fingerprint);
                        };
                        using(var client=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(2)})
                        {
                            try{using(var health=await client.GetAsync("https://127.0.0.1:47771/health")){if(health.IsSuccessStatusCode&&!owned.HasExited)return fingerprint;}}
                            catch(HttpRequestException){}catch(TaskCanceledException){}
                        }
                    }
                }
            }
            await Task.Delay(250);
        }
        throw new TimeoutException("服务未能及时启动，请检查目录权限与端口。");
    }
    internal async Task<Advertisement[]> Discover(int discoveryPort=47772,Func<string,string[]> rememberedRooms=null)
    {
        var found=new Dictionary<string,Advertisement>();
        using(var udp=new UdpClient(0))
        {
            udp.EnableBroadcast=true;byte[] query=Encoding.ASCII.GetBytes("CATAN_M7_DISCOVER_V1");
            await udp.SendAsync(query,query.Length,new IPEndPoint(IPAddress.Broadcast,discoveryPort));
            await udp.SendAsync(query,query.Length,new IPEndPoint(IPAddress.Loopback,discoveryPort));
            var deadline=Task.Delay(1800);
            while(!deadline.IsCompleted)
            {
                var receive=udp.ReceiveAsync();if(await Task.WhenAny(receive,deadline)!=receive)break;
                var packet=await receive;if(packet.Buffer.Length>8192)continue;
                try
                {
                    var ad=JsonUtility.FromJson<Advertisement>(Encoding.UTF8.GetString(packet.Buffer));
                    if(ad.protocolVersion!=NetworkProtocol.Version||ad.httpsPort<1||ad.httpsPort>65535||M7Client.NormalizeFingerprint(ad.certificateFingerprint).Length!=64)continue;
                    ad.Address="https://"+packet.RemoteEndPoint.Address+":"+ad.httpsPort;found[ad.Address]=ad;
                    if(found.Count>=32)break;
                }
                catch(Exception){}
            }
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
                    response.EnsureSuccessStatusCode();
                    if(response.Content.Headers.ContentLength>NetworkProtocol.MaximumResponseBytes)return;
                    using(var stream=await response.Content.ReadAsStreamAsync())using(var bytes=new MemoryStream())
                    {
                        var buffer=new byte[8192];int count;
                        while((count=await stream.ReadAsync(buffer,0,buffer.Length))>0){if(bytes.Length+count>NetworkProtocol.MaximumResponseBytes)return;bytes.Write(buffer,0,count);}
                        var result=WireJson.Deserialize<NetworkResponse>(Encoding.UTF8.GetString(bytes.ToArray()));
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
        catch(Exception){ad.Rooms=Array.Empty<RoomSummary>();}
    }
    public void Dispose()
    {
        if(owned==null)return;
        try{if(!owned.HasExited)owned.Kill();}catch(InvalidOperationException){}finally{owned.Dispose();owned=null;}
    }
}
