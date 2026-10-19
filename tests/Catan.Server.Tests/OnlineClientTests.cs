using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using Catan.Network.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Catan.Server.Tests;

public sealed class OnlineClientTests
{
    [Theory]
    [InlineData("203.0.113.10", "http://203.0.113.10:47771")]
    [InlineData(" 203.0.113.10:48888 ", "http://203.0.113.10:48888")]
    [InlineData("http://203.0.113.10:47771/", "http://203.0.113.10:47771")]
    [InlineData("[2001:db8::10]:48888", "http://[2001:db8::10]:48888")]
    [InlineData("http://[2001:db8::10]", "http://[2001:db8::10]:47771")]
    [InlineData("http://203.0.113.10:80", "http://203.0.113.10")]
    public void PublicIpAddressIsNormalizedWithoutDomainOrCertificate(string entered,string expected)
        =>Assert.Equal(expected,M7OnlineEndpoint.Normalize(entered));

    [Theory]
    [InlineData("")]
    [InlineData("game.example.com:47771")]
    [InlineData("https://203.0.113.10:47771")]
    [InlineData("http://user:secret@203.0.113.10:47771")]
    [InlineData("http://203.0.113.10:47771/api/request")]
    [InlineData("http://203.0.113.10:47771?credential=secret")]
    [InlineData("http://203.0.113.10:47771#fragment")]
    [InlineData("http://203.0.113.10:0")]
    [InlineData("http://203.0.113.10:65536")]
    public void InvalidAddressesAndSilentHttpsDowngradeAreRejected(string address)
        =>Assert.Throws<M7NetworkFailure>(()=>M7OnlineEndpoint.Normalize(address));

    [Theory]
    [InlineData("http://203.0.113.10:47771","ws://203.0.113.10:47771/ws")]
    [InlineData("https://192.168.1.2:47771","wss://192.168.1.2:47771/ws")]
    [InlineData("http://[2001:db8::10]:47771","ws://[2001:db8::10]:47771/ws")]
    public void WebSocketSchemeMatchesConfiguredTransport(string endpoint,string expected)
        =>Assert.Equal(expected,M7OnlineEndpoint.WebSocketUri(endpoint).AbsoluteUri);

    [Fact]
    public async Task EmptyOnlineLobbyAndStoppedServerHaveDistinctStatesAndRecoverAutomatically()
    {
        await using var first=Server("http://127.0.0.1:0",_=>Task.FromResult(List("first")));
        await first.StartAsync();string endpoint=Address(first);
        var client=Client();
        try
        {
            Assert.Equal(M7LobbyConnectionState.Unconfigured,client.LobbyConnectionState);
            client.ConfigureEndpoint(endpoint,"");Assert.Equal(M7LobbyConnectionState.Connecting,client.LobbyConnectionState);
            client.ListRooms();await PumpUntil(client,()=>!client.Busy);
            Assert.Equal(M7LobbyConnectionState.Online,client.LobbyConnectionState);
            Assert.Single(client.Rooms);Assert.False(client.Connected);
            await first.StopAsync();
            client.ListRooms();await PumpUntil(client,()=>!client.Busy);
            Assert.Equal(M7LobbyConnectionState.Offline,client.LobbyConnectionState);Assert.Empty(client.Rooms);
            await using var restarted=Server(endpoint,_=>Task.FromResult(List()));await restarted.StartAsync();
            // 将调度点设为已到期，验证正式 Update 的离线重试路径。
            Field("nextLobbyPollAt").SetValue(client,0f);
            await PumpUntil(client,()=>client.LobbyConnectionState==M7LobbyConnectionState.Online);
            Assert.Empty(client.Rooms);Assert.False(client.Busy);
        }
        finally {client.EndSession();}
    }

    [Fact]
    public async Task ChangingServerDiscardsOldRequestsAndOldRoomLists()
    {
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var old=Server("http://127.0.0.1:0",async _=>{entered.TrySetResult();await release.Task;return List("old");});
        await using var current=Server("http://127.0.0.1:0",_=>Task.FromResult(List("new")));
        await old.StartAsync();await current.StartAsync();var client=Client();
        try
        {
            client.ConfigureEndpoint(Address(old),"");client.ListRooms();await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            client.ConfigureEndpoint(Address(current),"");client.ListRooms();
            await PumpUntil(client,()=>!client.Busy);release.TrySetResult();
            await PumpUntil(client,()=>client.Rooms.Length==1);await Task.Delay(50);Tick(client);
            Assert.Equal(Address(current),client.Endpoint);Assert.Equal("new",Assert.Single(client.Rooms).RoomId);
            Assert.Equal(M7LobbyConnectionState.Online,client.LobbyConnectionState);
        }
        finally {release.TrySetResult();client.EndSession();}
    }

    [Fact]
    public async Task LobbyListDoesNotConfirmAnOldSeatAndIsIndependentOfSeatReconnectEpoch()
    {
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var app=Server("http://127.0.0.1:0",async _=>{entered.TrySetResult();await release.Task;return List("visible");});
        await app.StartAsync();var client=Client();
        try
        {
            client.ConfigureEndpoint(Address(app),"");Field("connectionId").SetValue(client,"old-seat-connection");
            Field("nextSyncAt").SetValue(client,float.MaxValue);
            client.ListRooms();await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Field("connectionEpoch").SetValue(client,(int)Field("connectionEpoch").GetValue(client)!+1);
            release.TrySetResult();await PumpUntil(client,()=>!client.Busy);
            Assert.Equal("visible",Assert.Single(client.Rooms).RoomId);Assert.False(client.Connected);
            Assert.Equal(M7LobbyConnectionState.Online,client.LobbyConnectionState);
        }
        finally {release.TrySetResult();client.EndSession();}
    }

    [Fact]
    public async Task BoundClientRefreshesLobbyAfterRestartEvenWhenFirstListPrecedesRoomPresence()
    {
        await using var first=Server("http://127.0.0.1:0",_=>Task.FromResult(List("before-restart")));
        await first.StartAsync();string endpoint=Address(first);var client=Client();
        try
        {
            client.ConfigureEndpoint(endpoint,"");client.ListRooms();await PumpUntil(client,()=>!client.Busy);
            Field("connectionId").SetValue(client,"retained-connection");
            Field("nextSyncAt").SetValue(client,float.MaxValue);
            await first.StopAsync();client.ListRooms();await PumpUntil(client,()=>!client.Busy);
            Assert.Equal(M7LobbyConnectionState.Offline,client.LobbyConnectionState);Assert.Empty(client.Rooms);
            bool roomPresent=false;int listRequests=0;
            await using var restarted=Server(endpoint,async context=>
            {
                using var reader=new StreamReader(context.Request.Body);var request=WireJson.Deserialize<NetworkRequest>(await reader.ReadToEndAsync());
                if(request.Operation=="List"){listRequests++;return roomPresent?List("restored-room"):List();}
                roomPresent=true;return new NetworkResponse{Success=true,Message="已同步。",Room=new RoomSummary{RoomId="restored-room"}};
            });
            await restarted.StartAsync();client.ListRooms();await PumpUntil(client,()=>!client.Busy);
            Assert.Equal(M7LobbyConnectionState.Online,client.LobbyConnectionState);Assert.Empty(client.Rooms);Assert.False(client.Connected);
            Field("nextSyncAt").SetValue(client,0f);Field("retryAt").SetValue(client,float.MaxValue);
            // 正式 Update 持续每秒 Get；这些同步不能推迟五秒一次的大厅刷新。
            await PumpUntil(client,()=>client.Rooms.Any(room=>room.RoomId=="restored-room"));
            Assert.True(roomPresent);Assert.True(listRequests>=2);Assert.True(client.Connected);
        }
        finally {client.EndSession();}
    }

    [Fact]
    public async Task ActualClientWebSocketUsesPlainHttpAndRoomPushDoesNotEraseLobbyList()
    {
        var received=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var push=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var app=Server("http://127.0.0.1:0",_=>Task.FromResult(List("other-room")));
        app.UseWebSockets();app.Map("/ws",async context=>
        {
            using var socket=await context.WebSockets.AcceptWebSocketAsync();
            try
            {
                var bytes=new byte[16384];await socket.ReceiveAsync(bytes,context.RequestAborted);received.TrySetResult();
                await push.Task.WaitAsync(context.RequestAborted);
                await socket.SendAsync(Encoding.UTF8.GetBytes(WireJson.Serialize(new NetworkResponse{Success=true,Room=new RoomSummary{RoomId="joined-room"}})),WebSocketMessageType.Text,true,context.RequestAborted);
                await socket.ReceiveAsync(bytes,context.RequestAborted);
            }
            catch(Exception) { }
        });
        await app.StartAsync();var client=Client();
        try
        {
            client.ConfigureEndpoint(Address(app),"");Method("StartSocket").Invoke(client,null);
            await PumpUntil(client,()=>received.Task.IsCompleted&&client.TransportMode=="WS 在线同步");
            Assert.Equal("",client.Fingerprint);Assert.DoesNotContain("WSS",client.TransportMode);
            client.ListRooms();await PumpUntil(client,()=>!client.Busy);Assert.Equal("other-room",Assert.Single(client.Rooms).RoomId);
            push.TrySetResult();await PumpUntil(client,()=>client.Room!=null);
            Assert.Equal("joined-room",client.Room.RoomId);Assert.Equal("other-room",Assert.Single(client.Rooms).RoomId);
        }
        finally {push.TrySetResult();client.EndSession();Tick(client);}
    }

    [Fact]
    public void LanStillRequiresHttpsWithItsPinnedCertificate()
    {
        var client=Client();
        try
        {
            string pin=new('a',64);
            client.ConfigureEndpoint("https://127.0.0.1:47771",pin,false);Assert.Equal(pin,client.Fingerprint);
            Assert.Throws<M7NetworkFailure>(()=>client.ConfigureEndpoint("http://127.0.0.1:47771",pin,false));
            Assert.Equal("https://127.0.0.1:47771",client.Endpoint);
        }
        finally {client.EndSession();}
    }

    static M7Client Client()
    {
        var client=new M7Client();client.UseCredentialDirectory(Path.Combine(Path.GetTempPath(),"catan-online-client-tests",Guid.NewGuid().ToString("N")));return client;
    }
    static NetworkResponse List(params string[] ids)=>new(){Success=true,Rooms=ids.Select(id=>new RoomSummary{RoomId=id}).ToArray()};
    static WebApplication Server(string address,Func<HttpContext,Task<NetworkResponse>> respond)
    {
        var builder=WebApplication.CreateBuilder();builder.Logging.ClearProviders();builder.WebHost.UseUrls(address);
        var app=builder.Build();app.MapPost("/api/request",async context=>await context.Response.WriteAsync(WireJson.Serialize(await respond(context))));return app;
    }
    static string Address(WebApplication app)=>app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    static MethodInfo Method(string name)=>typeof(M7Client).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!;
    static FieldInfo Field(string name)=>typeof(M7Client).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!;
    static void Tick(M7Client client)=>Method("Update").Invoke(client,null);
    static async Task PumpUntil(M7Client client,Func<bool> complete)
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        do {Tick(client);if(complete())return;await Task.Delay(10,timeout.Token);} while(true);
    }
}
