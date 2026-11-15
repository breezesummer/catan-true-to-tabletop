using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Catan.Network.Contracts;
using UnityEngine;

// 显式开发入口：真实服务、客户端及菜单处理器；配置、凭据和截图均保存在 .local。
public sealed class OnlineLobbyVerification : MonoBehaviour
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    M6App app;string output,onlineAddress,offlineAddress,stage="启动";
    int checks,captures;float nextStatus;TcpListener waitingServer;TcpClient waitingConnection;
    readonly List<M7Client> companions=new List<M7Client>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"--online-lobby-verify")>=0)
            new GameObject("在线大厅验收").AddComponent<OnlineLobbyVerification>();
    }
    IEnumerator Start()
    {
        Exception configurationError=null;
        try
        {
            string[] args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--online-lobby-verify");
            output=Path.GetFullPath(args[index+1]);onlineAddress=M7OnlineEndpoint.Normalize(args[index+2]);offlineAddress=M7OnlineEndpoint.Normalize(args[index+3]);
            string project=FindProjectRoot();string local=Path.Combine(project,".local")+Path.DirectorySeparatorChar;
            if(!output.StartsWith(local,StringComparison.OrdinalIgnoreCase)||Application.isBatchMode&&!Application.isEditor)
                throw new InvalidOperationException("验收需要项目 .local 独立目录和可渲染客户端。");
            if(!IPAddress.IsLoopback(IPAddress.Parse(new Uri(offlineAddress).Host.Trim('[',']'))))throw new InvalidOperationException("离线验收地址必须是本机独立端口。");
            Directory.CreateDirectory(output);
        }
        catch(Exception error){configurationError=error;}
        if(configurationError!=null){Fail(configurationError);yield break;}
#if UNITY_EDITOR
        M7CredentialStore.EnableEditorVerification(output);
#endif
        while((app=FindFirstObjectByType<M6App>())==null||app.Network==null)yield return null;
        app.ConfigureOnlineReview(Path.Combine(output,"private-server-address.txt"));
        app.Network.UseCredentialDirectory(Path.Combine(output,"private-player"));
        var stack=new Stack<IEnumerator>();stack.Push(Verify());
        while(stack.Count>0)
        {
            object next=null;bool more=false;Exception error=null;
            try{more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}catch(Exception caught){error=caught;}
            if(error!=null){Fail(error);yield break;}
            if(!more){stack.Pop();continue;}if(next is IEnumerator nested){stack.Push(nested);continue;}yield return next;
        }
        File.WriteAllText(Path.Combine(output,"summary.json"),"{\"status\":\"passed\",\"checks\":"+checks+",\"renderableCaptures\":"+captures+",\"realHttpService\":true,\"unconfiguredAndOfflineDistinct\":true,\"emptyLobbyOnline\":true,\"createAndJoin\":true,\"passwordDialog\":true,\"serverRestartRecovery\":true,\"persistedAddress\":true,\"isolatedData\":true}");
        Debug.Log("CATAN_ONLINE_LOBBY_OK checks="+checks+" captures="+captures);Application.Quit(0);
    }
    IEnumerator Verify()
    {
        Screen.SetResolution(1920,1080,false);
        yield return new WaitForSecondsRealtime(.5f);
        while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
        while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
        stage="未配置联机服务器";Call("OpenSeparatedNetworkMenu",false);
        Require(app.OnlineLobbyState==M7LobbyConnectionState.Unconfigured&&!app.OnlineRoomActionsAvailable);
        Require((string)Call("HeaderConnectionLabel")=="未连接");
        yield return Capture("01-unconfigured-lobby");

        // 本机只接收 TCP、不返回 HTTP 的端口，提供真实、稳定的连接中画面。
        stage="连接中";var offlineUri=new Uri(offlineAddress);
        waitingServer=new TcpListener(IPAddress.Parse(offlineUri.Host.Trim('[',']')),offlineUri.Port);waitingServer.Start();
        var accepted=waitingServer.AcceptTcpClientAsync();
        Set("networkOnlineDraft",offlineAddress);Call("SaveOnlineServerAddress");
        Require(app.OnlineLobbyState==M7LobbyConnectionState.Connecting&&!app.OnlineRoomActionsAvailable);
        Require((string)Call("HeaderConnectionLabel")=="连接中");
        yield return Wait(()=>accepted.IsCompleted);
        waitingConnection=accepted.Result;yield return Capture("02-connecting-lobby");
        waitingConnection.Dispose();waitingConnection=null;waitingServer.Stop();waitingServer=null;
        stage="服务器不在线";yield return Wait(()=>app.OnlineLobbyState==M7LobbyConnectionState.Offline&&!app.Network.Busy);
        Require(app.Network.Rooms.Length==0&&!app.OnlineRoomActionsAvailable);
        Require((string)Call("HeaderConnectionLabel")=="未连接");
        Call("CreateOnlineRoom");Require(app.Network.Room==null&&!app.Network.Busy);
        yield return Capture("03-server-offline");

        stage="保存公网 IP 配置";Set("networkTab",3);Set("networkOnlineDraft",onlineAddress);
        yield return Capture("04-server-address");Call("SaveOnlineServerAddress");
        yield return Wait(()=>app.OnlineLobbyState==M7LobbyConnectionState.Online&&!app.Network.Busy);
        Require(app.Network.Endpoint==onlineAddress&&app.Network.Rooms.Length==0&&app.Network.Room==null);
        Require(File.ReadAllText(app.OnlineReviewSettingsPath)==onlineAddress&&app.OnlineRoomActionsAvailable);
        Require((string)Call("HeaderConnectionLabel")=="已连接");
        yield return Capture("05-online-empty-lobby");

        stage="创建公开房间";Set("networkTab",1);Set("menuRule",3);Set("menuPlayers",4);
        Set("menuScenario",Array.IndexOf(Catan.Core.M5.CombinedScenarios.Ids,"through-the-desert"));
        Set("menuThreeKingdoms",true);Set("menuHarbormaster",true);Set("menuFishing",true);
        Set("networkRoomName","在线大厅验收房间");Set("networkPassword","");
        yield return Capture("06-create-room");Call("CreateOnlineRoom");
        yield return Wait(()=>!app.Network.Busy&&app.Network.Room!=null);
        string createdRoom=app.Network.RoomId;Require(app.Network.Room.Name=="在线大厅验收房间"&&app.Network.SeatId!=null);
        app.Network.SetDisplayName("清风");
        yield return Wait(()=>app.Network.Room.Seats.Any(s=>s.SeatId==app.Network.SeatId&&s.DisplayName=="清风"));
        yield return Capture("07-created-current-room");

        stage="房间木屋直接换座与准备";
        string firstSeat=app.Network.SeatId;
        var emptySeat=app.Network.Room.Seats.Last(s=>!s.Occupied&&!s.IsAi);
        Call("RoomSeatAction",app.Network.Room,emptySeat);
        yield return Wait(()=>!app.Network.Busy&&app.Network.SeatId==emptySeat.SeatId);
        Require(app.Network.Room.ManagerSeatId==emptySeat.SeatId);
        Call("RoomSeatAction",app.Network.Room,app.Network.Room.Seats.Single(s=>s.SeatId==app.Network.SeatId));
        yield return Wait(()=>!app.Network.Busy&&app.Network.Room.Seats.Single(s=>s.SeatId==app.Network.SeatId).Ready);
        Call("RoomSeatAction",app.Network.Room,app.Network.Room.Seats.Single(s=>s.SeatId==app.Network.SeatId));
        yield return Wait(()=>!app.Network.Busy&&!app.Network.Room.Seats.Single(s=>s.SeatId==app.Network.SeatId).Ready);
        Call("RoomSeatAction",app.Network.Room,app.Network.Room.Seats.Single(s=>s.SeatId==firstSeat));
        yield return Wait(()=>!app.Network.Busy&&app.Network.SeatId==firstSeat);
        Require(app.Network.Room.ManagerSeatId==firstSeat);

        stage="四席准备与房主菜单";
        var guests=new List<M7Client>();
        foreach(var name in new[]{"曹操","孙权","诸葛亮"}){
            var guest=MakeClient(name);guests.Add(guest);guest.Join(createdRoom,name,"");
            yield return Wait(()=>!guest.Busy&&guest.Room?.RoomId==createdRoom);
            guest.Ready(true);yield return Wait(()=>!guest.Busy);
        }
        yield return Wait(()=>!app.Network.Busy&&app.Network.Room.Seats.All(s=>s.Occupied&&(s.SeatId==app.Network.SeatId||s.Ready)));
        app.Network.Ready(true);
        yield return Wait(()=>!app.Network.Busy&&app.Network.Room.Seats.All(s=>s.Occupied&&s.Ready));
        Require(app.Network.Room.ScenarioId=="through-the-desert"&&app.Network.Room.Harbormaster&&app.Network.Room.Fishing);
        yield return Capture("07a-four-ready-seats");
        stage="大厅回房入口恢复原席";
        string retainedSeat=app.Network.SeatId;
        // 模拟自动恢复已停止的客户端状态；回房处理器经真实服务执行 Reconnect。
        typeof(M7Client).GetField("automaticReconnect",Flags).SetValue(app.Network,false);
        typeof(M7Client).GetProperty("Connected").SetValue(app.Network,false);
        Set("networkTab",0);Call("ReturnToCurrentRoom");
        yield return Wait(()=>!app.Network.Busy&&app.Network.Connected);
        Require(app.Network.RoomId==createdRoom&&app.Network.SeatId==retainedSeat);
        Require((int)Get("networkTab")==2&&(string)Call("HeaderConnectionLabel")=="已连接");
        // 用真实长玩家名核对名牌留白，房主点击其他玩家仍打开原有转交确认。
        const string longName="江上客与山海同游共赴沙漠争霸四方再相逢";
        guests[0].SetDisplayName(longName);yield return Wait(()=>!guests[0].Busy);
        yield return Wait(()=>app.Network.Room.Seats.Any(s=>s.DisplayName==longName));
        Call("RoomSeatAction",app.Network.Room,app.Network.Room.Seats.Single(s=>s.SeatId==guests[0].SeatId));
        Require((string)Get("transferSeat")==guests[0].SeatId);
        yield return Capture("07d-transfer-host-long-name");Set("transferSeat",null);
        yield return Capture("07e-long-name-seats");
        foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1440,900),new Vector2Int(1920,900)}){
            Screen.SetResolution(size.x,size.y,false);yield return new WaitForSecondsRealtime(.3f);
            yield return Capture("07f-room-"+size.x+"x"+size.y);
        }
        Screen.SetResolution(1920,1080,false);yield return new WaitForSecondsRealtime(.3f);
        Set("lanRoomPage",1);yield return Capture("07b-room-settings");
        Set("lanRoomPage",0);
        Set("networkTab",0);Call("OpenOnlineConnectionSettings");
        Require((int)Get("networkTab")==3&&(string)Get("networkOnlineDraft")==onlineAddress);
        yield return Capture("07c-connection-settings");
        foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1440,900),new Vector2Int(1920,900)}){
            Screen.SetResolution(size.x,size.y,false);yield return new WaitForSecondsRealtime(.3f);
            yield return Capture("07c-connection-settings-"+size.x+"x"+size.y);
        }
        Screen.SetResolution(1920,1080,false);yield return new WaitForSecondsRealtime(.3f);
        Set("networkOnlineDraft","");yield return Capture("07c-connection-settings-empty");
        Set("networkOnlineDraft",onlineAddress);Set("networkTab",2);
        foreach(var guest in guests){guest.Leave();yield return Wait(()=>!guest.Busy&&guest.Room==null);}
        var open=MakeClient("open");var locked=MakeClient("locked");
        open.Create(new RoomSettings{Name="沙海同行",RuleSet="combined",ScenarioId="through-the-desert",PlayerCount=4,Listed=true},"验收好友甲","");
        yield return Wait(()=>!open.Busy&&open.Room!=null);
        locked.Create(new RoomSettings{Name="群岛之夜",RuleSet="seafarers",ScenarioId="four-islands",PlayerCount=4,Listed=true},"验收好友乙","验收密码");
        yield return Wait(()=>!locked.Busy&&locked.Room!=null);
        stage="大厅显示实际服务端房间";Set("networkTab",0);Call("RefreshOnlineLobby");
        yield return Wait(()=>!app.Network.Busy&&app.Network.Rooms.Length>=3);
        var openRoom=app.Network.Rooms.Single(r=>r.RoomId==open.RoomId);var lockedRoom=app.Network.Rooms.Single(r=>r.RoomId==locked.RoomId);
        Require(app.Network.Rooms.Any(r=>r.RoomId==createdRoom));yield return Capture("08-room-list");
        foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,900)}){
            Screen.SetResolution(size.x,size.y,false);yield return new WaitForSecondsRealtime(.3f);
            yield return Capture("08a-lobby-"+size.x+"x"+size.y);
        }
        Screen.SetResolution(1920,1080,false);yield return new WaitForSecondsRealtime(.3f);

        stage="按房间代码加入";Set("networkRoomId",openRoom.RoomId);Call("JoinLobbyCode");
        Require(Get("pendingJoinRoom")==null);yield return Wait(()=>!app.Network.Busy&&app.Network.RoomId==open.RoomId&&app.Network.Room!=null);
        Require((bool)Call("JoinedRoom",openRoom,null));
        stage="密码弹窗与取消";Call("RequestJoinRoom",lockedRoom,null,false);Require(Get("pendingJoinRoom")!=null);
        Set("joinPassword","验收密码");yield return Capture("09-password-dialog");Call("CancelJoinPassword");
        Require((string)Get("joinPassword")==""&&app.Network.RoomId==open.RoomId);
        stage="错误密码";Call("RequestJoinRoom",lockedRoom,null,false);Set("joinPassword","错误密码");Call("ConfirmJoinPassword");
        yield return Wait(()=>!app.Network.Busy&&app.Network.LastError.Contains("InvalidPassword"));
        Require(!app.Network.LastError.Contains("错误密码")&&Get("pendingJoinRoom")==null);
        yield return Capture("10-wrong-password");
        stage="正确密码加入";Call("RequestJoinRoom",lockedRoom,null,false);Set("joinPassword","验收密码");Call("ConfirmJoinPassword");
        yield return Wait(()=>!app.Network.Busy&&app.Network.Room?.RoomId==locked.RoomId);
        Require(app.Network.SeatId!=null&&string.IsNullOrEmpty(app.Network.LastError));yield return Capture("11-protected-current-room");

        stage="服务停止与旧列表清除";Set("networkTab",0);app.Network.ListRooms();yield return Wait(()=>!app.Network.Busy&&app.Network.Rooms.Length>0);
        File.WriteAllText(Path.Combine(output,"server-stop.request"),"停止独立验收服务");
        yield return Wait(()=>File.Exists(Path.Combine(output,"server-stopped.signal")),90);
        app.Network.ListRooms();yield return Wait(()=>!app.Network.Busy&&app.OnlineLobbyState==M7LobbyConnectionState.Offline);
        Require(app.Network.Rooms.Length==0&&!app.OnlineRoomActionsAvailable);
        Require((string)Call("HeaderConnectionLabel")=="未连接");
        string retainedRoom=app.Network.RoomId;Call("CreateOnlineRoom");Call("RequestJoinRoom",openRoom,null,false);
        Require(!app.Network.Busy&&app.Network.RoomId==retainedRoom&&Get("pendingJoinRoom")==null);
        yield return Capture("12-stopped-server-clears-rooms");

        stage="服务重新上线";File.WriteAllText(Path.Combine(output,"server-start.request"),"重新启动独立验收服务");
        yield return Wait(()=>File.Exists(Path.Combine(output,"server-started.signal")),90);
        Call("RefreshOnlineLobby");yield return Wait(()=>!app.Network.Busy&&app.OnlineLobbyState==M7LobbyConnectionState.Online);
        app.Network.ListRooms();yield return Wait(()=>!app.Network.Busy&&app.Network.Rooms.Any(r=>r.RoomId==locked.RoomId));
        Require(app.Network.Endpoint==onlineAddress&&app.OnlineRoomActionsAvailable);yield return Capture("13-server-recovered");

        stage="再次进入大厅自动连接已保存地址";app.Network.EndSession();app.ReturnToMainMenu();
        Set("menuOnlineAddress","");app.ConfigureOnlineReview(app.OnlineReviewSettingsPath);Call("OpenSeparatedNetworkMenu",false);
        yield return Wait(()=>!app.Network.Busy&&app.OnlineLobbyState==M7LobbyConnectionState.Online);
        Require(app.Network.Endpoint==onlineAddress&&File.ReadAllText(app.OnlineReviewSettingsPath)==onlineAddress);
        yield return Capture("14-saved-address-auto-connect");

        stage="大厅后台查询保持控件和输入稳定";
        Set("networkRoomId","沙海同行");Set("networkScroll",new Vector2(0,12));
        float until=Time.realtimeSinceStartup+6;
        while(Time.realtimeSinceStartup<until){Require(!app.Network.Busy);Require((string)Get("networkRoomId")=="沙海同行");yield return null;}
        yield return Capture("15-lobby-after-background-poll");

        stage="真实 AI 托管状态条与房间标题";
        var settings=new RoomSettings{Name="沙海同盟",RuleSet="combined",ScenarioId="through-the-desert",PlayerCount=4,
            Harbormaster=true,Fishing=true,TradeModulesVersion=NetworkProtocol.TradeModulesVersion,AiSeats=new[]{"P3","P4"},
            ThreeKingdomsPackageVersion=Catan.Core.ThreeKingdoms.GeneralCatalog.B6PackageVersion,
            ThreeKingdomsContentHash=Catan.Core.ThreeKingdoms.GeneralCatalog.B6ContentHash};
        app.Network.Create(settings,"清风","");yield return Wait(()=>!app.Network.Busy&&app.Network.Room?.Name==settings.Name);
        var trustee=MakeClient("托管玩家");trustee.Join(app.Network.RoomId,"托管玩家","");yield return Wait(()=>!trustee.Busy&&trustee.Room!=null);
        trustee.Ready(true);yield return Wait(()=>!trustee.Busy&&app.Network.Room.Seats.Any(s=>s.SeatId==trustee.SeatId&&s.Ready));
        app.Network.Ready(true);yield return Wait(()=>!app.Network.Busy&&app.Network.Room.Seats.All(s=>s.IsAi||s.Ready));
        app.Network.StartRoom();yield return Wait(()=>app.Ready&&app.Network.Playing);
        Call("OpenNetworkMenu");Set("networkTab",2);Set("lanRoomPage",0);
        trustee.Leave();yield return Wait(()=>!trustee.Busy&&app.Network.Room.Seats.Any(s=>s.ControlledByAi));
        Require(app.Network.Room.Name=="沙海同盟");yield return null;Require(!app.Network.LobbyPollingEnabled);
        yield return Capture("16-room-ai-takeover");
        foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1440,900)}){
            Screen.SetResolution(size.x,size.y,false);yield return new WaitForSecondsRealtime(.3f);
            yield return Capture("16-room-ai-takeover-"+size.x+"x"+size.y);
        }
    }
    M7Client MakeClient(string name)
    {
        var client=new GameObject("在线大厅验收好友 "+name).AddComponent<M7Client>();companions.Add(client);
        client.UseCredentialDirectory(Path.Combine(output,"private-"+name));client.ConfigureEndpoint(onlineAddress,"",false);return client;
    }
    IEnumerator Wait(Func<bool> ready,float timeout=60)
    {
        float deadline=Time.realtimeSinceStartup+timeout;
        while(!ready())
        {
            if(Time.realtimeSinceStartup>nextStatus){nextStatus=Time.realtimeSinceStartup+10;Debug.Log("CATAN_ONLINE_LOBBY_WAIT "+stage+" "+app.Network.Status+" state="+app.OnlineLobbyState+" busy="+app.Network.Busy+" rooms="+app.Network.Rooms.Length);}
            if(Time.realtimeSinceStartup>deadline)throw new TimeoutException(stage);yield return null;
        }
    }
    IEnumerator Capture(string name)
    {
        yield return new WaitForSecondsRealtime(.3f);
        for(int frame=0;frame<3;frame++)
        {if(Application.isEditor)yield return null;else yield return new WaitForEndOfFrame();}
        var texture=ScreenCapture.CaptureScreenshotAsTexture();
        Require(texture!=null&&texture.width>0&&texture.height>0);
        var pixels=texture.GetPixels32();bool visible=false;
        for(int pixel=0;pixel<pixels.Length;pixel+=997)
            if(pixels[pixel].r>8||pixels[pixel].g>8||pixels[pixel].b>8){visible=true;break;}
        if(visible){File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());captures++;}
        else Debug.LogWarning("CATAN_ONLINE_LOBBY_CAPTURE_UNAVAILABLE "+name+" 黑帧不计入视觉验收");
        Destroy(texture);
    }
    void Fail(Exception error)
    {
        if(output!=null&&Directory.Exists(output))File.WriteAllText(Path.Combine(output,"summary.json"),"{\"status\":\"failed\",\"checks\":"+checks+",\"renderableCaptures\":"+captures+"}");
        Debug.LogError("CATAN_ONLINE_LOBBY_FAILED "+stage+" "+error.GetType().Name+" "+error.Message+" "+(app?.Network?.LastError??""));
        Application.Quit(1);
    }
    static string FindProjectRoot()
    {
        foreach(string start in new[]{Application.dataPath,Directory.GetCurrentDirectory()})
        {
            var folder=new DirectoryInfo(start);
            while(folder!=null){if(File.Exists(Path.Combine(folder.FullName,"docs","PROJECT_BRIEF.md")))return folder.FullName;folder=folder.Parent;}
        }
        throw new DirectoryNotFoundException("无法定位项目中的 .local 目录。");
    }
    void Require(bool condition){checks++;if(!condition)throw new InvalidOperationException(stage);}
    object Get(string name)=>typeof(M6App).GetField(name,Flags).GetValue(app);
    void Set(string name,object value)=>typeof(M6App).GetField(name,Flags).SetValue(app,value);
    object Call(string name,params object[] arguments)=>typeof(M6App).GetMethod(name,Flags).Invoke(app,arguments);
    void OnDestroy(){waitingConnection?.Dispose();waitingServer?.Stop();foreach(var client in companions)if(client!=null)Destroy(client.gameObject);}
}
