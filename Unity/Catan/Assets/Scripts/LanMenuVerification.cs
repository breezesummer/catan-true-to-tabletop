using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Catan.Network.Contracts;
using UnityEngine;

// 显式开发入口：通过正式服务、客户端与菜单处理器验证，不读写玩家身份或房间存档。
public sealed class LanMenuVerification : MonoBehaviour
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    M6App app;string output,stage="启动";int checks,renderableCaptures;float deadline,nextStatus;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"--lan-menu-verify")>=0)new GameObject("局域网菜单验收").AddComponent<LanMenuVerification>();
    }
    IEnumerator Start()
    {
        var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--lan-menu-verify");
        try
        {
            output=Path.GetFullPath(args[index+1]);
            string local=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../../../.local"))+Path.DirectorySeparatorChar;
            if(!output.StartsWith(local,StringComparison.OrdinalIgnoreCase)||Application.isBatchMode)throw new InvalidOperationException("验收必须使用 .local 独立目录和可渲染客户端。");
            Directory.CreateDirectory(output);app=FindFirstObjectByType<M6App>();
            var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
            int discoveryPort;using(var udp=new UdpClient(0))discoveryPort=((IPEndPoint)udp.Client.LocalEndPoint).Port;
            app.ConfigureLanReview(Path.Combine(output,"private-server"),port,discoveryPort);
            app.Network.UseCredentialDirectory(Path.Combine(output,"private-player"));deadline=Time.realtimeSinceStartup+240;
        }
        catch(Exception ex){Debug.LogError("CATAN_LAN_MENU_FAILED 配置 "+ex.GetType().Name);Application.Quit(1);yield break;}
        var stack=new Stack();stack.Push(Verify());
        while(stack.Count>0)
        {
            object next=null;bool more=false;Exception error=null;
            try{more=((IEnumerator)stack.Peek()).MoveNext();if(more)next=((IEnumerator)stack.Peek()).Current;}catch(Exception ex){error=ex;}
            if(error!=null){Debug.LogError("CATAN_LAN_MENU_FAILED "+stage+" "+error.GetType().Name+" "+app.Network.LastError);Application.Quit(1);yield break;}
            if(!more){stack.Pop();continue;}if(next is IEnumerator sub){stack.Push(sub);continue;}yield return next;
        }
        File.WriteAllText(Path.Combine(output,"summary.json"),"{\"status\":\"passed\",\"checks\":"+checks+",\"renderableCaptures\":"+renderableCaptures+",\"automaticService\":true,\"discoveryBeforeCreatingRoom\":true,\"openRoomDirectJoin\":true,\"passwordDialog\":true,\"wrongPasswordDiagnostic\":true,\"joinedRoomLabel\":true,\"isolatedData\":true}");
        Debug.Log("CATAN_LAN_MENU_OK checks="+checks);Application.Quit(0);
    }
    IEnumerator Verify()
    {
        while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
        Call("NavigateMainMenu",2);stage="进入局域网自动服务";
        yield return Wait(()=>!(bool)Get("networkWorking")&&!(bool)Get("lanPreparing"));
        var service=(M7LanService)Get("lanService");Require(service.Running);
        Require(app.Network.Room==null);
        if(((M7LanService.Advertisement[])Get("networkFound")).Length==0){Call("DiscoverLan");yield return Wait(()=>!(bool)Get("networkWorking"));}
        Require(((M7LanService.Advertisement[])Get("networkFound")).Length>0);
        string pin=File.ReadAllText(Path.Combine(app.LanReviewDataDirectory,"server-fingerprint.txt")).Trim();
        // 重复启动应复用已验证的服务，不会因固定端口再次启动失败。
        var ready=service.Start(app.LanReviewDataDirectory);yield return Wait(()=>ready.IsCompleted);Require(!ready.IsFaulted);
        using(var reuse=new M7LanService(service.HttpsPort,service.DiscoveryPort))
        {
            var reused=reuse.Start(app.LanReviewDataDirectory);yield return Wait(()=>reused.IsCompleted);Require(!reused.IsFaulted);
        }
        Require(service.Running);
        stage="端口与目录占用诊断";
        using(var conflict=new M7LanService(service.HttpsPort,service.DiscoveryPort))
        {
            var failed=conflict.Start(Path.Combine(output,"private-port-conflict"));yield return Wait(()=>failed.IsCompleted);
            Require(failed.IsFaulted&&failed.Exception.GetBaseException().Message.Contains("TCP_PORT_IN_USE"));
        }
        using(var conflict=new M7LanService(FreeTcpPort(),service.DiscoveryPort))
        {
            var failed=conflict.Start(app.LanReviewDataDirectory);yield return Wait(()=>failed.IsCompleted);
            Require(failed.IsFaulted&&failed.Exception.GetBaseException().Message.Contains("DATA_IN_USE"));
        }
        using(var occupied=new UdpClient(AddressFamily.InterNetwork))
        {
            occupied.Client.ExclusiveAddressUse=true;occupied.Client.Bind(new IPEndPoint(IPAddress.Any,0));
            using(var warning=new M7LanService(FreeTcpPort(),((IPEndPoint)occupied.Client.LocalEndPoint).Port))
            {
                var warned=warning.Start(Path.Combine(output,"private-udp-conflict"));yield return Wait(()=>warned.IsCompleted);
                Require(!warned.IsFaulted);yield return Wait(()=>warning.HostWarning.Contains("UDP_PORT_IN_USE"));Require(warning.Running);
            }
        }
        yield return Capture("01-auto-service-no-room");
        var open=MakeClient("open",pin);var locked=MakeClient("locked",pin);
        open.Create(new RoomSettings{Name="无需密码的朋友房间",RuleSet="base",ScenarioId="base",PlayerCount=4,Listed=true},"朋友甲","");
        yield return Wait(()=>!open.Busy&&open.Room!=null);
        locked.Create(new RoomSettings{Name="需要密码的朋友房间",RuleSet="base",ScenarioId="base",PlayerCount=4,Listed=true},"朋友乙","验收密码");
        yield return Wait(()=>!locked.Busy&&locked.Room!=null);
        stage="未创建房间直接搜索";Call("DiscoverLan");yield return Wait(()=>!(bool)Get("networkWorking"));
        var servers=(M7LanService.Advertisement[])Get("networkFound");
        var server=servers.First(s=>s.Rooms.Any(r=>r.RoomId==open.RoomId));
        var openRoom=server.Rooms.First(r=>r.RoomId==open.RoomId);var lockedRoom=server.Rooms.First(r=>r.RoomId==locked.RoomId);
        Require(app.Network.Room==null);Require(Get("pendingJoinRoom")==null);yield return Capture("02-room-list-no-password-field");
        stage="无密码直接加入";Call("RequestJoinRoom",openRoom,server,false);Require(Get("pendingJoinRoom")==null);
        yield return Wait(()=>!app.Network.Busy&&app.Network.Room?.RoomId==open.RoomId);
        yield return null;Set("networkTab",0);Require((bool)Call("JoinedRoom",openRoom,server));
        yield return Capture("03-already-joined");
        stage="密码弹窗取消保留原桌";Call("RequestJoinRoom",lockedRoom,server,false);
        Require(Get("pendingJoinRoom")!=null);Require(app.Network.RoomId==open.RoomId);
        Set("joinPassword","验收密码");yield return Capture("04-password-dialog-masked");
        Call("CancelJoinPassword");Require((string)Get("joinPassword")==""&&app.Network.RoomId==open.RoomId);
        stage="错误密码提示";Call("RequestJoinRoom",lockedRoom,server,false);Set("joinPassword","错误密码");Call("ConfirmJoinPassword");
        yield return Wait(()=>!app.Network.Busy&&app.Network.LastError.Contains("InvalidPassword"));
        Require(Get("pendingJoinRoom")==null&&(string)Get("joinPassword")=="");Require(!app.Network.LastError.Contains("错误密码"));
        yield return Capture("05-wrong-password-error");
        stage="正确密码加入";Call("RequestJoinRoom",lockedRoom,server,false);Set("joinPassword","验收密码");Call("ConfirmJoinPassword");
        yield return Wait(()=>!app.Network.Busy&&app.Network.Room?.RoomId==locked.RoomId);
        Require(string.IsNullOrEmpty(app.Network.LastError));yield return null;Set("networkTab",0);
        Require((bool)Call("JoinedRoom",lockedRoom,server));yield return Capture("06-protected-room-joined");
        stage="离开大厅后重新输入密码";app.Network.Leave();yield return Wait(()=>!app.Network.Busy&&app.Network.Room==null);
        Call("RequestJoinRoom",lockedRoom,server,true);Require(Get("pendingJoinRoom")!=null);
        Set("joinPassword","验收密码");Call("ConfirmJoinPassword");
        yield return Wait(()=>!app.Network.Busy&&app.Network.Room?.RoomId==locked.RoomId);Require(app.Network.SeatId!=null);
        // 对局结束与中途返回共用同一释放路径；重复检验同地址的新房不能复用已释放的客户端。
        for(int round=0;round<2;round++)
        {
            stage="返回主页后重新创建房间 "+round;
            app.Network.Playing=true;app.ReturnToMainMenu();
            Require(!app.Network.EndpointConfigured);
            Require(!(bool)typeof(M6App).GetProperty("CanUseMenuEndpoint",Flags).GetValue(app));
            Call("OpenSeparatedNetworkMenu",true);
            yield return Wait(()=>!(bool)Get("networkWorking")&&!(bool)Get("lanPreparing"));
            Set("networkTab",1);Set("menuRule",0);Set("menuPlayers",4);
            var ai=(bool[])Get("networkAi");for(int seat=1;seat<4;seat++)ai[seat]=true;
            string previous=app.Network.RoomId;Call("CreateLanRoom");
            yield return Wait(()=>!(bool)Get("networkWorking")&&!app.Network.Busy&&app.Network.Room!=null);
            Require(app.Network.EndpointConfigured&&app.Network.RoomId!=previous&&string.IsNullOrEmpty(app.Network.LastError));
            app.Network.Ready(true);yield return Wait(()=>!app.Network.Busy);
            app.Network.StartRoom();yield return Wait(()=>app.Network.Playing);
            Require(app.Ready);
        }
        yield return Capture("07-recreated-game-without-restarting");
    }
    M7Client MakeClient(string name,string pin)
    {
        var client=new GameObject("验收房主 "+name).AddComponent<M7Client>();
        client.UseCredentialDirectory(Path.Combine(output,"private-"+name));client.ConfigureEndpoint(((M7LanService)Get("lanService")).Address,pin,false);return client;
    }
    static int FreeTcpPort()
    {var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();return port;}
    IEnumerator Wait(Func<bool> ready)
    {while(!ready()){if(Time.realtimeSinceStartup>nextStatus){nextStatus=Time.realtimeSinceStartup+10;Debug.Log("CATAN_LAN_MENU_WAIT "+stage+" "+app.Network.Status);}if(Time.realtimeSinceStartup>deadline)throw new TimeoutException(stage);yield return null;}}
    IEnumerator Capture(string name)
    {
        yield return new WaitForSecondsRealtime(.3f);for(int i=0;i<3;i++)yield return new WaitForEndOfFrame();
        var image=ScreenCapture.CaptureScreenshotAsTexture();var pixels=image.GetPixels32();bool visible=false;
        for(int i=0;i<pixels.Length;i+=997){if(pixels[i].r>8||pixels[i].g>8||pixels[i].b>8){visible=true;break;}}
        if(visible){File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());renderableCaptures++;}
        else Debug.Log("CATAN_LAN_MENU_CAPTURE_UNAVAILABLE "+name+" 隐藏窗口黑帧，不计入视觉验收");
        Destroy(image);
    }
    void Require(bool condition){checks++;if(!condition)throw new InvalidOperationException(stage);}
    object Get(string name)=>typeof(M6App).GetField(name,Flags).GetValue(app);
    void Set(string name,object value)=>typeof(M6App).GetField(name,Flags).SetValue(app,value);
    object Call(string name,params object[] arguments)=>typeof(M6App).GetMethod(name,Flags).Invoke(app,arguments);
}
