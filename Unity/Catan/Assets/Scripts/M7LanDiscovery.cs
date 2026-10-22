#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

internal static class M7LanDiscovery
{
    internal static IPAddress[] Targets()
    {
        var targets=new HashSet<IPAddress>{IPAddress.Broadcast,IPAddress.Loopback};
        foreach(var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if(adapter.OperationalStatus!=OperationalStatus.Up||adapter.NetworkInterfaceType==NetworkInterfaceType.Loopback||adapter.NetworkInterfaceType==NetworkInterfaceType.Tunnel)continue;
            foreach(var address in adapter.GetIPProperties().UnicastAddresses)
            {
                if(address.Address.AddressFamily!=AddressFamily.InterNetwork||address.IPv4Mask==null)continue;
                var bytes=address.Address.GetAddressBytes();var mask=address.IPv4Mask.GetAddressBytes();
                if(!(bytes[0]==10||bytes[0]==192&&bytes[1]==168||bytes[0]==172&&bytes[1]>=16&&bytes[1]<=31||bytes[0]==169&&bytes[1]==254))continue;
                targets.Add(new IPAddress(bytes.Select((value,i)=>(byte)(value|~mask[i])).ToArray()));
            }
        }
        return targets.ToArray();
    }

    internal static async Task<UdpReceiveResult[]> Search(int port,Action<Exception> issue,int milliseconds=1800,IPAddress[] targets=null)
    {
        var packets=new List<UdpReceiveResult>();var udp=new UdpClient(AddressFamily.InterNetwork);
        Task<UdpReceiveResult> receive=null;
        try
        {
            udp.Client.Bind(new IPEndPoint(IPAddress.Any,0));udp.EnableBroadcast=true;
            byte[] query=Encoding.ASCII.GetBytes("CATAN_M7_DISCOVER_V1");int sent=0;Exception last=null;
            foreach(var address in targets??Targets())
            {
                try{await udp.SendAsync(query,query.Length,new IPEndPoint(address,port));sent++;}
                catch(SocketException ex){last=ex;issue?.Invoke(ex);}
            }
            if(sent==0)throw last??new M7NetworkFailure("NO_NETWORK","没有可用的局域网接口，请连接有线网络或 Wi-Fi。");
            var deadline=Task.Delay(milliseconds);
            while(!deadline.IsCompleted&&packets.Count<128)
            {
                receive=udp.ReceiveAsync();if(await Task.WhenAny(receive,deadline)!=receive)break;
                try{var packet=await receive;if(packet.Buffer.Length<=8192)packets.Add(packet);}
                // Windows 对无人监听的本机 UDP 探测会返回 ICMP 错误，继续等待其他主机。
                catch(SocketException ex) when(ex.SocketErrorCode==SocketError.ConnectionReset||ex.SocketErrorCode==SocketError.ConnectionRefused){}
                receive=null;
            }
        }
        finally
        {
            udp.Dispose();
            if(receive!=null){try{await receive;}catch(Exception){/* 关闭超时接收并观察其结果。 */}}
        }
        return packets.ToArray();
    }
}
