using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Catan.Network.Contracts;
using Catan.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Catan.Server.Tests;

public sealed class LanDiscoveryRegressionTests
{
    [Fact]
    public async Task NoLocalServiceIsAnEmptyResultAndRepeatedSearchStillWorks()
    {
        int port;using(var reserve=new UdpClient(0))port=((IPEndPoint)reserve.Client.LocalEndPoint!).Port;
        for(int i=0;i<2;i++)
        {
            var issues=new List<Exception>();
            var packets=await M7LanDiscovery.Search(port,issues.Add,200,new[]{IPAddress.Loopback});
            Assert.Empty(packets);Assert.Empty(issues);
        }
    }

    [Fact]
    public async Task UnansweredLocalProbeDoesNotAbortDelayedResponseFromAnotherHost()
    {
        var other=IPAddress.Parse("127.0.0.2");
        using var server=new UdpClient(new IPEndPoint(other,0));
        int port=((IPEndPoint)server.Client.LocalEndPoint!).Port;
        var reply=Task.Run(async()=>
        {
            var query=await server.ReceiveAsync();
            Assert.Equal("CATAN_M7_DISCOVER_V1",Encoding.ASCII.GetString(query.Buffer));
            await Task.Delay(100);
            await server.SendAsync(Encoding.UTF8.GetBytes("远端房间"),query.RemoteEndPoint);
        });
        var packets=await M7LanDiscovery.Search(port,_=>{},500,new[]{IPAddress.Loopback,other});
        await reply.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Contains(packets,p=>p.RemoteEndPoint.Address.Equals(other)&&Encoding.UTF8.GetString(p.Buffer)=="远端房间");
    }

    [Fact]
    public async Task DiscoveryKeepsHealthyRoomsAndReportsTheHostWhoseHttpsListFails()
    {
        using var key=RSA.Create(2048);
        var request=new CertificateRequest("CN=localhost",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        using var generated=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddDays(1));
        using var certificate=X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx),null,OperatingSystem.IsWindows()?X509KeyStorageFlags.UserKeySet|X509KeyStorageFlags.PersistKeySet:X509KeyStorageFlags.EphemeralKeySet);
        string pin=certificate.GetCertHashString(HashAlgorithmName.SHA256);
        var builder=WebApplication.CreateBuilder();builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options=>options.Listen(IPAddress.Loopback,0,listen=>listen.UseHttps(certificate)));
        await using var app=builder.Build();
        app.MapPost("/api/request",()=>Results.Text(WireJson.Serialize(new NetworkResponse{Success=true,Rooms=new[]{new RoomSummary{RoomId="healthy",Name="可加入的房间"}}}),"application/json"));
        await app.StartAsync();
        string address=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        int httpsPort=new Uri(address).Port;
        int closedPort;using(var reserve=new TcpListener(IPAddress.Loopback,0)){reserve.Start();closedPort=((IPEndPoint)reserve.LocalEndpoint).Port;}
        using var udp=new UdpClient(new IPEndPoint(IPAddress.Loopback,0));
        int discoveryPort=((IPEndPoint)udp.Client.LocalEndPoint!).Port;
        var replies=Task.Run(async()=>
        {
            var query=await udp.ReceiveAsync();
            foreach(int port in new[]{httpsPort,closedPort})
            {
                var data=JsonSerializer.SerializeToUtf8Bytes(new{protocolVersion=NetworkProtocol.Version,httpsPort=port,certificateFingerprint=pin,serverName="验证服务"});
                await udp.SendAsync(data,query.RemoteEndPoint);
            }
        });
        using var client=new M7LanService();
        var found=await client.Discover(discoveryPort);
        await replies.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Contains(found,s=>s.Rooms.Any(r=>r.RoomId=="healthy"));
        Assert.Contains(found,s=>s.httpsPort==closedPort&&!string.IsNullOrEmpty(s.Error));
        Assert.Contains(closedPort.ToString(),client.DiscoveryIssues);
        Assert.Contains("读取房间列表",client.DiscoveryIssues);
        await app.StopAsync();
    }

    [Theory]
    [InlineData(true,false,false,true)]
    [InlineData(false,false,false,false)]
    [InlineData(true,true,true,false)]
    [InlineData(true,true,false,true)]
    public void PasswordIsRequestedOnlyForProtectedRoomsWithoutRecovery(bool protectedRoom,bool remembered,bool started,bool expected)
        =>Assert.Equal(expected,M7RoomEntry.NeedsPassword(protectedRoom,remembered,started));

    [Fact]
    public void JoinedRoomUsesServerIdentityAndRoomIdAndRequiresASeat()
    {
        Assert.True(M7RoomEntry.IsJoined("room","https://192.168.1.2:47771","abc","room","https://127.0.0.1:47771","ABC",true));
        Assert.False(M7RoomEntry.IsJoined("room","https://192.168.1.3:47771","other","room","https://127.0.0.1:47771","abc",true));
        Assert.False(M7RoomEntry.IsJoined("different","https://192.168.1.2:47771","abc","room","https://127.0.0.1:47771","abc",true));
        Assert.False(M7RoomEntry.IsJoined("room","https://192.168.1.2:47771","abc","room","https://127.0.0.1:47771","abc",false));
    }

    [Fact]
    public void ErrorMessagesShowStageAndCauseWithoutEchoingExceptionSecrets()
    {
        const string secret="private-token-password-path";
        string socket=M7NetworkErrors.Describe("搜索",new HttpRequestException(secret,new SocketException((int)SocketError.ConnectionRefused)));
        Assert.Contains("搜索失败",socket);Assert.Contains("主机未接受连接",socket);Assert.DoesNotContain(secret,socket);
        Assert.Contains("[TLS]",M7NetworkErrors.Describe("加入",new HttpRequestException(secret,new AuthenticationException(secret))));
        Assert.Contains("[TIMEOUT]",M7NetworkErrors.Describe("加入",new TaskCanceledException(secret)));
        Assert.Contains("[HTTP 429]",M7NetworkErrors.Describe("列表",Assert.Throws<M7NetworkFailure>(()=>M7NetworkErrors.RequireHttp(HttpStatusCode.TooManyRequests))));
        Assert.DoesNotContain(secret,StartupDiagnostics.Describe(new IOException(secret),"ROOMS",47771));
        Assert.Contains("TCP 47771",StartupDiagnostics.Describe(new IOException(secret,new SocketException((int)SocketError.AddressAlreadyInUse)),"LISTEN",47771));
    }
}
