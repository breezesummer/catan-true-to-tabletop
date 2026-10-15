using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

internal sealed class M7LanService : IDisposable
{
    [Serializable] internal sealed class Advertisement
    {
        public string serverName,certificateFingerprint;
        public int protocolVersion,httpsPort;
        [NonSerialized] public string Address;
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
    internal async Task<Advertisement[]> Discover()
    {
        var found=new Dictionary<string,Advertisement>();
        using(var udp=new UdpClient(0))
        {
            udp.EnableBroadcast=true;byte[] query=Encoding.ASCII.GetBytes("CATAN_M7_DISCOVER_V1");
            await udp.SendAsync(query,query.Length,new IPEndPoint(IPAddress.Broadcast,47772));
            var deadline=Task.Delay(1800);
            while(!deadline.IsCompleted)
            {
                var receive=udp.ReceiveAsync();if(await Task.WhenAny(receive,deadline)!=receive)break;
                var packet=await receive;if(packet.Buffer.Length>8192)continue;
                try
                {
                    var ad=JsonUtility.FromJson<Advertisement>(Encoding.UTF8.GetString(packet.Buffer));
                    if(ad.protocolVersion!=Catan.Network.Contracts.NetworkProtocol.Version||ad.httpsPort<1||ad.httpsPort>65535)continue;
                    ad.Address="https://"+packet.RemoteEndPoint.Address+":"+ad.httpsPort;found[ad.Address]=ad;
                }
                catch(Exception){}
            }
        }
        return new List<Advertisement>(found.Values).ToArray();
    }
    public void Dispose()
    {
        if(owned==null)return;
        try{if(!owned.HasExited)owned.Kill();}catch(InvalidOperationException){}finally{owned.Dispose();owned=null;}
    }
}
