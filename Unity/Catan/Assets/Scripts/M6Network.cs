using System;
using System.IO;
using System.Linq;
using Catan.Network.Contracts;
using UnityEngine;

public sealed partial class M6App
{
    public M7Client Network { get; private set; }
    public bool NetworkGame=>Network!=null&&Network.Playing;
    M7LanService lanService=new M7LanService();
    bool networkMenu,networkEntering,networkLan=true,networkWorking;
    string networkAddress="https://127.0.0.1:47771",networkPin="",networkRoomId="",networkPassword="",networkRoomName="卡坦好友房间",networkMessage="";
    bool networkListed=true;
    readonly bool[] networkAi={false,false,false,false};
    M7LanService.Advertisement[] networkFound=Array.Empty<M7LanService.Advertisement>();
    Vector2 networkScroll;
    string networkEditorConfiguration;
    internal string LanReviewDataDirectory;
    internal void ConfigureLanReview(string directory,int port,int discoveryPort)
    {lanService.Dispose();lanService=new M7LanService(port,discoveryPort);LanReviewDataDirectory=directory;}
    string LanDataDirectory=>LanReviewDataDirectory??Path.Combine(Application.persistentDataPath,"m7-server");
    bool lanPreparing;
    string lanPreparationIssue="";
    async void PrepareLanService()
    {
        if(!lanService.CanHost||lanPreparing)return;
        lanPreparing=true;lanPreparationIssue="";
        try{await lanService.Start(LanDataDirectory);lanPreparationIssue=lanService.HostWarning;}
        catch(Exception ex){lanPreparationIssue=M7NetworkErrors.Describe("本机托管服务",ex)+" 仍可加入朋友的房间。";}
        finally{lanPreparing=false;}
    }
    void InitializeNetwork()
    {
        Network=gameObject.AddComponent<M7Client>();Network.ViewChanged+=ApplyNetworkView;
        Network.SessionEnded+=CloseNetworkPresentation;
        Network.CommandFeedback+=message=>{Notice=message;ShowTablePrompt(message);};
        Network.PreviewChanged+=()=>
        {
            // A late preview updates hover colours and cached action availability without cancelling drafts.
            if(presentation is M2App basic)basic.M7PreviewRefresh();
            else if(presentation is M3App sailing)sailing.M7PreviewRefresh();
            else if(presentation is M4App cities)cities.M7PreviewRefresh();
            else if(presentation is M5App combined)combined.M7PreviewRefresh();
        };
    }
    void ApplyNetworkView()
    {
        if(Network.Room==null||!Network.Room.Started||string.IsNullOrEmpty(Network.SeatId))return;
        if(!NetworkGame)
        {
            var room=Network.Room;networkEntering=true;Network.Playing=true;networkMenu=false;
            try{StartGame(room.RuleSet=="cities"?"cities-knights":room.RuleSet,room.RuleSet=="base"||room.RuleSet=="cities"||room.RuleSet=="cities-knights"?"heading-for-new-shores":room.ScenarioId,room.PlayerCount,Network.SeatId,targetVictoryPoints:room.TargetVictoryPoints);}
            finally{networkEntering=false;}
            Notice="已连接房间 "+room.RoomId+"。离线时 AI 接手，重连后在下次自己的回合恢复操作。";
        }
        else if(refresh!=null)refresh();
    }
    void BindNetwork(MonoBehaviour app,Func<string> acting,Func<bool> done,Action update,Action dispose,Action<bool> show)
    {
        BindHistory(()=>Network.History,()=>Network.History.Length);
        // 重新入桌直接展示当前本人快照；旧历史稍后分页到达，不应阻塞初始棋盘或重播整局出牌。
        switch(Network.Room.RuleSet)
        {
            case "base":cardReveals.Reset(Network.View<Catan.Core.M2.PlayerView>().Events.LastOrDefault()?.Sequence??0);break;
            case "seafarers":cardReveals.Reset(Network.View<Catan.Core.M3.PlayerView>().Events.LastOrDefault()?.Sequence??0);break;
            case "cities":case "cities-knights":cardReveals.Reset(Network.View<Catan.Core.M4.PlayerView>().Events.LastOrDefault()?.Sequence??0);break;
            default:cardReveals.Reset(Network.View<Catan.Core.M5.PlayerView>().Events.LastOrDefault()?.Sequence??0);break;
        }
        Bind(app,acting,done,_=>"联机模式由服务端调度 AI。",()=>throw new InvalidOperationException("联机权威存档由服务端保管。"),update,dispose,show);
    }
    public void ConnectNetwork(M2App app)=>BindNetwork(app,()=>app.M6View.Discards.Any(d=>d.PlayerId==HumanSeat)?HumanSeat:Actor(app.M6View),()=>app.M6View.Phase==Catan.Core.M2.GamePhase.Finished,app.M6Refresh,app.M6Close,app.M6Visible);
    public void ConnectNetwork(M3App app)=>BindNetwork(app,()=>app.M6View.Discards.Any(d=>d.PlayerId==HumanSeat)||app.M6View.GoldClaims.Any(d=>d.PlayerId==HumanSeat)?HumanSeat:Actor(app.M6View),()=>app.M6View.Phase==Catan.Core.M3.GamePhase.Finished,app.M6Refresh,app.M6Close,app.M6Visible);
    public void ConnectNetwork(M4App app)=>BindNetwork(app,()=>app.M6View.Discards.Any(d=>d.PlayerId==HumanSeat)?HumanSeat:M4App.Actor(app.M6View),()=>app.M6View.Phase==Catan.Core.M4.GamePhase.Finished,app.M6Refresh,app.M6Close,app.M6Visible);
    public void ConnectNetwork(M5App app)=>BindNetwork(app,()=>app.M6View.Discards.Any(d=>d.PlayerId==HumanSeat)?HumanSeat:M5App.Actor(app.M6View),()=>app.M6View.Phase==Catan.Core.M5.GamePhase.Finished,app.M6Refresh,app.M6Close,app.M6Visible);
    void OpenNetworkMenu(){networkMenu=true;MenuVisible=true;if(presentation!=null)presentation.enabled=false;visible?.Invoke(false);}
    void CloseNetworkPresentation()
    {
        close?.Invoke();presentation=null;actor=null;finished=null;decideAndSubmit=null;save=null;refresh=null;close=null;visible=null;MenuVisible=true;tableMenu=false;networkMenu=true;
    }
    RoomSettings NetworkSettings()=>new RoomSettings{Name=networkRoomName,RuleSet=RuleIds[menuRule],ScenarioId=menuRule==0||menuRule==2?"base":ScenarioIds()[Mathf.Clamp(menuScenario,0,ScenarioIds().Length-1)],PlayerCount=menuPlayers,TargetVictoryPoints=menuVictoryPoints,Listed=networkListed,AiSeats=Enumerable.Range(0,menuPlayers).Where(i=>networkAi[i]).Select(i=>"P"+(i+1)).ToArray()};
    async void StartLan()
    {
        if(networkWorking)return;networkWorking=true;networkMessage="正在启动随包局域网服务…";
        try{networkPin=await lanService.Start(LanDataDirectory);networkAddress=lanService.Address;networkLan=true;networkMessage="本机房间服务已启动，可直接创建房间。";}
        catch(Exception ex){networkMessage=M7NetworkErrors.Describe("本机托管服务",ex);}
        finally{networkWorking=false;}
    }
    async void DiscoverLan()
    {
        if(networkWorking)return;networkWorking=true;Network.ClearError();int navigation=networkNavigation;
        PrepareLanService();
        networkMessage="";
        try
        {
            var found=await lanService.Discover(lanService.DiscoveryPort,Network.RememberedRoomIds);
            if(navigation!=networkNavigation||!networkMenu||!networkLan)return;
            networkFound=found;
            int count=networkFound.SelectMany(s=>s.Rooms.Select(r=>s.certificateFingerprint+"|"+r.RoomId)).Distinct().Count();
            networkMessage=count==0?"搜索完成，没有发现开放房间。请确认朋友已创建房间，且双方在同一局域网；访客 Wi-Fi、设备隔离或 VPN 可能阻断发现。":"已发现 "+count+" 个房间，选择后即可加入。";
            if(!string.IsNullOrEmpty(lanService.DiscoveryIssues))networkMessage+="\n"+lanService.DiscoveryIssues;
        }
        catch(Exception ex){if(navigation==networkNavigation)networkMessage=M7NetworkErrors.Describe("局域网搜索",ex);}
        finally{networkWorking=false;if(navigation!=networkNavigation&&networkMenu&&networkLan&&networkTab==0)DiscoverLan();}
    }
    void ConnectNetworkEndpoint()
    {
        try
        {
            Network.ConfigureEndpoint(networkAddress,networkLan?networkPin:"");Network.ListRooms();networkMessage="";
        }
        catch(Exception ex){networkMessage=ex.Message;}
    }
    int networkTab,networkSettingsPage,networkNavigation;
    string menuLanAddress="https://127.0.0.1:47771",menuLanPin="",menuOnlineAddress="",networkPresentedRoom;
    bool CanUseMenuEndpoint=>Network.EndpointConfigured&&!string.IsNullOrEmpty(Network.Endpoint)&&
        string.Equals(Network.Endpoint,(networkAddress??"").Trim().TrimEnd('/'),StringComparison.OrdinalIgnoreCase)&&
        (networkLan?M7Client.NormalizeFingerprint(networkPin)==Network.Fingerprint:string.IsNullOrEmpty(Network.Fingerprint));
    void OpenSeparatedNetworkMenu(bool local)
    {
        networkNavigation++;CancelJoinPassword();
        if(networkLan){menuLanAddress=networkAddress;menuLanPin=networkPin;}else menuOnlineAddress=networkAddress;
        networkLan=local;networkAddress=local?menuLanAddress:menuOnlineAddress;networkPin=local?menuLanPin:"";
        networkTab=0;networkScroll=Vector2.zero;
        OpenNetworkMenu();
        if(local)DiscoverLan();
    }
    int lanRoomPage;
    void DrawLanShell()
    {
        if(pastoralMenuFont==null)pastoralMenuFont=Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
        PastoralMenuUI.Styles(pastoralMenuFont??font);PastoralMenuScene.Draw(Time.unscaledTime);
        PastoralMenuUI.Label(new Rect(78,65,300,28),"与同一网络的好友相聚",14,PastoralMenuUI.Ivory);
        PastoralMenuUI.Label(new Rect(72,127,350,80),"局域网联机",43,PastoralMenuUI.Ivory,true);
        PastoralMenuUI.Label(new Rect(79,234,300,45),"玩家："+LocalPlayerName,18,PastoralMenuUI.Ivory);
        if(PastoralMenuUI.Nav(new Rect(65,350,322,68),"房间列表","自动寻找身边的同伴",networkTab==0,0)){networkTab=0;networkScroll=Vector2.zero;}
        if(PastoralMenuUI.Nav(new Rect(65,435,322,68),"创建房间","选择玩法，等待好友入座",networkTab==1,0)){networkTab=1;networkSettingsPage=0;networkScroll=Vector2.zero;}
        if(Network.Room!=null&&PastoralMenuUI.Nav(new Rect(65,520,322,68),"当前房间",Network.Room.Name,networkTab==2,0)){networkTab=2;networkScroll=Vector2.zero;}
        if(PastoralMenuUI.ReturnButton(new Rect(79,762,290,42))){networkMenu=false;menuPage=-1;}
        PastoralMenuUI.Label(new Rect(79,840,310,25),ReleaseVersion,11,PastoralMenuUI.Ivory);
    }
    void DrawLanNetworkMenu()
    {
        if(Network.Room?.RoomId!=networkPresentedRoom)
        {
            networkPresentedRoom=Network.Room?.RoomId;
            if(Network.Room!=null){networkTab=2;lanRoomPage=0;}else if(networkTab==2)networkTab=0;
            networkScroll=Vector2.zero;
        }
        bool shellEnabled=GUI.enabled;GUI.enabled=shellEnabled&&pendingJoinRoom==null;DrawLanShell();GUI.enabled=shellEnabled;if(!networkMenu)return;
        bool before=GUI.enabled;GUI.enabled=before&&menuPendingAction==null&&pendingJoinRoom==null;
        PastoralMenuUI.Panel(new Rect(454,56,920,788));
        MenuPageHeader("近 处 的 同 伴",networkTab==0?"房间列表":networkTab==1?"创建房间":"当前房间",
            networkTab==0?"自动搜索同一网络的房间，选中后即可加入。":networkTab==1?"配置这一桌的玩法与同伴。":"选择建筑颜色、准备或恢复原来的对局。");
        if(networkTab==0)
        {
            if(PastoralMenuUI.Button(new Rect(1123,216,210,39),networkWorking?"正在搜索…":"重新搜索",!networkWorking,true))DiscoverLan();
            PastoralMenuUI.Label(new Rect(498,222,585,27),"选择房间加入 · 身份按房间保留，可随时换桌",13,PastoralMenuUI.Muted);
        }
        else if(networkTab==2)
        {
            SyncNetworkEditor();lanRoomPage=PastoralMenuUI.Segments(new Rect(495,216,838,39),lanRoomPage,new[]{"同桌玩家","房间设置","连接与恢复"});
        }
        var savedLabel=label;var savedTitle=title;var savedSmall=small;var savedButton=button;
        label=PastoralMenuUI.Text;title=PastoralMenuUI.Title;small=PastoralMenuUI.Small;button=PastoralMenuUI.Control;
        GUILayout.BeginArea(new Rect(495,282,838,370));networkScroll=GUILayout.BeginScrollView(networkScroll);
        if(networkTab==0)DrawLanRooms();
        else if(networkTab==1)DrawNetworkSettings();
        else if(Network.Room!=null)
        {
            if(lanRoomPage==0)DrawNetworkSeats();
            else if(lanRoomPage==1)
            {
                bool state=GUI.enabled;GUI.enabled=state&&Network.Room.ManagerSeatId==Network.SeatId&&!Network.Room.Started;DrawNetworkSettings();GUI.enabled=state;
            }
            else DrawNetworkRecovery();
        }
        GUILayout.EndScrollView();GUILayout.EndArea();
        label=savedLabel;title=savedTitle;small=savedSmall;button=savedButton;
        PastoralMenuUI.Fill(new Rect(495,701,838,1),new Color(.48f,.54f,.42f,.20f));
        DrawNetworkStatus();
        if(networkTab==1)
        {
            if(networkSettingsPage>0&&PastoralMenuUI.Button(new Rect(956,779,124,40),"上一步"))networkSettingsPage--;
            if(PastoralMenuUI.Button(new Rect(1093,779,240,40),networkWorking?"正在创建…":networkSettingsPage<2?"下一步  →":"创建并加入房间",!networkWorking,true))
            {if(networkSettingsPage<2)networkSettingsPage++;else CreateLanRoom();}
        }
        else if(networkTab==2&&Network.Room!=null)
        {
            var room=Network.Room;
            if(lanRoomPage==0&&!room.Started)
            {
                var own=room.Seats.FirstOrDefault(s=>s.SeatId==Network.SeatId);
                if(PastoralMenuUI.Button(new Rect(1093,779,240,40),own!=null&&own.Ready?"取消准备":"我准备好了",!Network.Busy,true))Network.Ready(own==null||!own.Ready);
                if(room.ManagerSeatId==Network.SeatId&&PastoralMenuUI.Button(new Rect(839,779,240,40),"所有人准备后开始",!Network.Busy))Network.StartRoom();
            }
            if(lanRoomPage==1&&room.ManagerSeatId==Network.SeatId&&!room.Started&&PastoralMenuUI.Button(new Rect(1093,779,240,40),"应用房间配置",!Network.Busy,true))Network.ConfigureRoom(NetworkSettings());
            if(room.Started&&NetworkGame&&Ready&&PastoralMenuUI.Button(new Rect(1093,779,240,40),"返回联机对局  →",true,true)){networkMenu=false;ResumeGame();}
        }
        GUI.enabled=before;DrawMenuConfirmation();DrawJoinPassword();
    }
    void DrawLanRooms()
    {
        var rooms=networkFound.SelectMany(server=>server.Rooms.Select(room=>new{server,room})).GroupBy(item=>item.server.certificateFingerprint+"|"+item.room.RoomId).Select(group=>group.First()).ToArray();
        if(rooms.Length==0&&!networkWorking)GUILayout.Label("没有找到开放的房间。请重新搜索，或从左侧创建房间。",small);
        foreach(var item in rooms)
        {
            var room=item.room;var r=GUILayoutUtility.GetRect(790,86);
            PastoralMenuUI.Round(new Rect(r.x,r.y+2,r.width-6,79),new Color(.91f,.93f,.86f),10);
            PastoralMenuUI.Label(new Rect(r.x+18,r.y+10,r.width-157,28),room.Name+"  ·  "+room.OccupiedSeats+"/"+room.PlayerCount+" 人",18,null,true);
            bool remembered=item.server.RememberedRooms.Contains(room.RoomId);
            bool joined=JoinedRoom(room,item.server);
            PastoralMenuUI.Label(new Rect(r.x+18,r.y+43,r.width-155,26),joined?"我的房间 · "+(room.Started?"对局中":"等待开局"):(remembered&&room.Started?"我的进行中对局 · 可恢复原玩家":room.Started?"对局中 · 原玩家可恢复":"等待开局")+(M7RoomEntry.NeedsPassword(room.HasPassword,remembered,room.Started)?"  ·  需要密码":"  ·  直接加入"),13,PastoralMenuUI.Muted);
            if(joined)PastoralMenuUI.Label(new Rect(r.xMax-122,r.y+23,104,37),"已加入",16,PastoralMenuUI.Muted,false,TextAnchor.MiddleCenter);
            else if(PastoralMenuUI.Button(new Rect(r.xMax-122,r.y+23,104,37),remembered&&room.Started?"重连":"加入",!networkWorking&&!Network.Busy))RequestJoinRoom(room,item.server,remembered);
        }
    }
    RoomSummary pendingJoinRoom;
    M7LanService.Advertisement pendingJoinServer;
    string joinPassword="";
    Vector2 networkStatusScroll;
    string NetworkMenuStatus=>(!string.IsNullOrEmpty(Network.LastError)?Network.LastError:!string.IsNullOrEmpty(networkMessage)?networkMessage:Network.Status)+(networkLan?(lanPreparing?"\n本机托管服务准备中，可继续搜索和加入好友房间。":string.IsNullOrEmpty(lanPreparationIssue)?"":"\n"+lanPreparationIssue):"");
    void DrawNetworkStatus()
    {
        if(networkLan&&networkTab==0&&networkWorking)return;
        GUILayout.BeginArea(new Rect(498,703,815,67));networkStatusScroll=GUILayout.BeginScrollView(networkStatusScroll);
        GUILayout.Label(DisplayPlayerText(NetworkMenuStatus),new GUIStyle(PastoralMenuUI.Small){wordWrap=true});GUILayout.EndScrollView();GUILayout.EndArea();
    }
    bool JoinedRoom(RoomSummary room,M7LanService.Advertisement server=null)=>M7RoomEntry.IsJoined(room.RoomId,server?.Address??Network.Endpoint,server?.certificateFingerprint??"",Network.Room?.RoomId,Network.Endpoint,Network.Fingerprint,!string.IsNullOrEmpty(Network.SeatId));
    void CancelJoinPassword(){pendingJoinRoom=null;pendingJoinServer=null;joinPassword="";}
    void RequestJoinRoom(RoomSummary room,M7LanService.Advertisement server=null,bool remembered=false)
    {
        if(JoinedRoom(room,server))return;
        if(M7RoomEntry.NeedsPassword(room.HasPassword,remembered,room.Started))
        {pendingJoinRoom=room;pendingJoinServer=server;joinPassword="";return;}
        JoinSelectedRoom(server,room.RoomId,"");
    }
    void DrawJoinPassword()
    {
        if(pendingJoinRoom==null)return;
        PastoralMenuUI.Fill(new Rect(0,0,1440,900),new Color(.08f,.15f,.1f,.60f));
        PastoralMenuUI.Panel(new Rect(420,285,600,322));
        PastoralMenuUI.Label(new Rect(458,318,520,38),"输入房间密码",26,null,true);
        PastoralMenuUI.Label(new Rect(458,367,520,32),pendingJoinRoom.Name,16,PastoralMenuUI.Muted);
        GUI.SetNextControlName("lan-join-password");
        joinPassword=GUI.PasswordField(new Rect(458,414,520,44),joinPassword,'●',128,PastoralMenuUI.Field);
        if(PastoralMenuUI.Button(new Rect(654,519,150,43),"取消"))CancelJoinPassword();
        if(pendingJoinRoom!=null&&PastoralMenuUI.Button(new Rect(822,519,156,43),"确认加入",!Network.Busy&&joinPassword.Length>0,true))
            ConfirmJoinPassword();
        if(Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.Escape){CancelJoinPassword();Event.current.Use();}
        else if(Event.current.type==EventType.KeyDown&&(Event.current.keyCode==KeyCode.Return||Event.current.keyCode==KeyCode.KeypadEnter)){ConfirmJoinPassword();Event.current.Use();}
    }
    void ConfirmJoinPassword()
    {
        if(pendingJoinRoom==null||Network.Busy||joinPassword.Length==0)return;
        var room=pendingJoinRoom;var server=pendingJoinServer;string password=joinPassword;
        CancelJoinPassword();JoinSelectedRoom(server,room.RoomId,password);
    }
    void JoinSelectedRoom(M7LanService.Advertisement server,string roomId,string password)
    {
        try
        {
            if(server!=null){networkAddress=server.Address;networkPin=server.certificateFingerprint;}
            networkMessage="";
            if(!CanUseMenuEndpoint)Network.ConfigureEndpoint(networkAddress,networkPin,false);
            Network.Join(roomId,LocalPlayerName,password);
        }
        catch(Exception ex){networkMessage=M7NetworkErrors.Describe("加入房间",ex);}
    }
    async void CreateLanRoom()
    {
        if(networkWorking)return;
        var settings=NetworkSettings();string name=LocalPlayerName,password=networkPassword;int navigation=networkNavigation;
        networkWorking=true;Network.ClearError();networkMessage="正在准备本机房间服务…";
        try
        {
            string pin=await lanService.Start(LanDataDirectory);
            lanPreparationIssue=lanService.HostWarning;
            if(navigation!=networkNavigation||!networkMenu||!networkLan||networkTab!=1)return;
            networkPin=pin;networkAddress=lanService.Address;
            if(!CanUseMenuEndpoint)Network.ConfigureEndpoint(networkAddress,networkPin,false);
            Network.Create(settings,name,password);networkMessage="";
        }
        catch(Exception ex){networkMessage=M7NetworkErrors.Describe("创建房间",ex);}
        finally{networkWorking=false;}
    }
    void DrawNetworkMenu()
    {
        if(networkLan){DrawLanNetworkMenu();return;}
        if(Network.Room?.RoomId!=networkPresentedRoom){networkPresentedRoom=Network.Room?.RoomId;networkTab=0;networkScroll=Vector2.zero;}
        SyncNetworkEditor();DrawPastoralShell(networkLan?2:3);
        if(!networkMenu)return;
        bool before=GUI.enabled;GUI.enabled=before&&menuPendingAction==null&&pendingJoinRoom==null;
        PastoralMenuUI.Panel(new Rect(454,56,920,788));
        MenuPageHeader(networkLan?"近 处 的 同 伴":"远 方 的 旅 伴",networkLan?"局域网联机":"在线游戏",
            networkLan?"在同一个网络中，与好友共建一座岛屿。":"连接已部署的在线服务，与远方好友相聚。");
        string[] tabs=Network.Room==null?new[]{"连接服务","加入房间","创建房间"}:new[]{"同桌席位","房间设置","连接与恢复"};
        int tab=PastoralMenuUI.Segments(new Rect(495,216,838,39),networkTab,tabs);
        if(tab!=networkTab){networkTab=tab;networkScroll=Vector2.zero;GUI.FocusControl(null);}
        var savedLabel=label;var savedTitle=title;var savedSmall=small;var savedButton=button;
        label=PastoralMenuUI.Text;title=PastoralMenuUI.Title;small=PastoralMenuUI.Small;button=PastoralMenuUI.Control;
        GUILayout.BeginArea(new Rect(495,282,838,400));
        networkScroll=GUILayout.BeginScrollView(networkScroll);
        if(Network.Room==null)
        {
            if(networkTab==0)DrawNetworkConnection();
            else if(networkTab==1)DrawNetworkJoin();
            else DrawNetworkSettings();
        }
        else if(networkTab==0)DrawNetworkSeats();
        else if(networkTab==1)
        {
            bool editable=Network.Room.ManagerSeatId==Network.SeatId&&!Network.Room.Started;
            GUILayout.Label(editable?"配置更改后，同桌玩家需要重新准备。":"由房主配置本局；对局开始后配置固定。",small);
            bool state=GUI.enabled;GUI.enabled=state&&editable;DrawNetworkSettings();GUI.enabled=state;
        }
        else DrawNetworkRecovery();
        GUILayout.EndScrollView();GUILayout.EndArea();
        label=savedLabel;title=savedTitle;small=savedSmall;button=savedButton;
        PastoralMenuUI.Fill(new Rect(495,701,838,1),new Color(.48f,.54f,.42f,.20f));
        DrawNetworkStatus();
        if(Network.Room==null)
        {
            if(networkTab==0&&PastoralMenuUI.Button(new Rect(1093,779,240,40),networkWorking||Network.Busy?"正在连接…":"连接并进入大厅  →",!networkWorking&&!Network.Busy,true))
            {ConnectNetworkEndpoint();if(CanUseMenuEndpoint)networkTab=1;}
            if(networkTab==1&&PastoralMenuUI.Button(new Rect(1093,779,240,40),"刷新房间列表",CanUseMenuEndpoint&&!Network.Busy,true))Network.ListRooms();
            if(networkTab==2)
            {
                if(networkSettingsPage>0&&PastoralMenuUI.Button(new Rect(956,779,124,40),"上一步"))networkSettingsPage--;
                if(PastoralMenuUI.Button(new Rect(1093,779,240,40),networkSettingsPage<2?"下一步  →":"创建并加入房间",networkSettingsPage<2||CanUseMenuEndpoint&&!Network.Busy,true))
                {if(networkSettingsPage<2)networkSettingsPage++;else Network.Create(NetworkSettings(),LocalPlayerName,networkPassword);}
            }
        }
        else
        {
            var room=Network.Room;
            if(networkTab==0&&!room.Started)
            {
                var own=room.Seats.FirstOrDefault(s=>s.SeatId==Network.SeatId);
                if(PastoralMenuUI.Button(new Rect(1093,779,240,40),own!=null&&own.Ready?"取消准备":"我准备好了",!Network.Busy,true))Network.Ready(own==null||!own.Ready);
                if(room.ManagerSeatId==Network.SeatId&&PastoralMenuUI.Button(new Rect(839,779,240,40),"所有人准备后开始",!Network.Busy))Network.StartRoom();
            }
            if(networkTab==1&&room.ManagerSeatId==Network.SeatId&&!room.Started&&PastoralMenuUI.Button(new Rect(1093,779,240,40),"应用房间配置",!Network.Busy,true))Network.ConfigureRoom(NetworkSettings());
            if(room.Started&&NetworkGame&&Ready&&PastoralMenuUI.Button(new Rect(1093,779,240,40),"返回联机对局  →",true,true)){networkMenu=false;ResumeGame();}
        }
        PastoralMenuUI.Label(new Rect(498,788,320,25),CanUseMenuEndpoint?Network.TransportMode:"请先连接当前服务",12,PastoralMenuUI.Muted);
        GUI.enabled=before;DrawMenuConfirmation();DrawJoinPassword();
    }
    void NetworkField(string caption,ref string value,bool password=false)
    {
        GUILayout.Label(caption,small);
        value=password?GUILayout.PasswordField(value,'●',PastoralMenuUI.Field,GUILayout.Height(38)):
            GUILayout.TextField(value,PastoralMenuUI.Field,GUILayout.Height(38));
        GUILayout.Space(12);
    }
    void DrawNetworkConnection()
    {
        if(networkLan)
        {
            GUILayout.Label("选择一台电脑，作为这次相聚的主机",label);GUILayout.Space(13);
            GUILayout.BeginHorizontal();
            bool before=GUI.enabled;GUI.enabled=before&&!networkWorking;
            if(GUILayout.Button(networkWorking?"正在准备…":"在本机开启服务",button,GUILayout.Height(38)))StartLan();
            if(GUILayout.Button("寻找同网段主机",button,GUILayout.Height(38)))DiscoverLan();
            GUI.enabled=before;GUILayout.EndHorizontal();GUILayout.Space(15);
        }
        else {GUILayout.Label("填写好友提供的在线服务地址",label);GUILayout.Space(8);GUILayout.Label("需要已有的 HTTPS 房间服务。目前尚未配置公共服务器。",small);GUILayout.Space(14);}
        NetworkField(networkLan?"房主地址  ·  https://主机IP:47771":"在线服务地址  ·  https://主机:端口",ref networkAddress);
        if(networkLan)
        {
            foreach(var found in networkFound)
                if(GUILayout.Button(found.serverName+"  ·  "+found.Address,button,GUILayout.Height(36)))
                {networkAddress=found.Address;networkPin=found.certificateFingerprint;}
        }
        else {GUILayout.Space(12);GUILayout.Label("连接后，可浏览房间、输入房间号或创建自己的房间。",small);}
    }
    void DrawNetworkJoin()
    {
        GUILayout.Label("玩家："+LocalPlayerName+"（在设置中修改）",small);GUILayout.Space(14);
        GUILayout.Label("通过房间号赴约",label);GUILayout.Space(10);
        GUILayout.BeginHorizontal();networkRoomId=GUILayout.TextField(networkRoomId,PastoralMenuUI.Field,GUILayout.Height(39));
        bool enabled=GUI.enabled;GUI.enabled=enabled&&CanUseMenuEndpoint&&!Network.Busy;
        if(GUILayout.Button("加入房间",button,GUILayout.Width(171),GUILayout.Height(39)))
        {int navigation=networkNavigation;Network.DescribeRoom(networkRoomId,room=>{if(navigation==networkNavigation&&networkMenu)RequestJoinRoom(room,null,Network.HasRoomIdentity(room.RoomId));});}
        GUI.enabled=enabled;GUILayout.EndHorizontal();GUILayout.Space(24);
        GUILayout.Label("当前服务的房间",label);GUILayout.Space(10);
        if(!CanUseMenuEndpoint)GUILayout.Label("先在「连接服务」中建立连接，再查看这里的房间。",small);
        else if(Network.Rooms.Length==0)GUILayout.Label("这里还很安静。刷新列表，或创建第一桌游戏。",small);
        else foreach(var room in Network.Rooms)
        {
            var r=GUILayoutUtility.GetRect(790,80);
            PastoralMenuUI.Round(new Rect(r.x,r.y+2,r.width-6,73),new Color(.91f,.93f,.86f),10);
            PastoralMenuUI.Label(new Rect(r.x+18,r.y+10,r.width-156,29),room.Name+"  ·  "+room.OccupiedSeats+"/"+room.PlayerCount+" 人",18,null,true);
            PastoralMenuUI.Label(new Rect(r.x+18,r.y+43,r.width-155,25),"房间号 "+room.RoomId+(room.HasPassword?"  ·  需密码":"  ·  可直接加入")+(room.Started?"  ·  对局中":""),12,PastoralMenuUI.Muted);
            if(JoinedRoom(room))PastoralMenuUI.Label(new Rect(r.xMax-122,r.y+20,104,37),"已加入",16,PastoralMenuUI.Muted,false,TextAnchor.MiddleCenter);
            else if(PastoralMenuUI.Button(new Rect(r.xMax-122,r.y+20,104,37),"加入",!Network.Busy))RequestJoinRoom(room,null,Network.HasRoomIdentity(room.RoomId));
        }
    }
    void DrawNetworkSeats()
    {
        var room=Network.Room;
        GUILayout.Label(room.Name+"  ·  房间号 "+room.RoomId,label);
        GUILayout.Label("你的建筑颜色："+ColorLabel(Network.SeatId)+"  ·  "+room.PlayerCount+" 人  ·  "+room.TargetVictoryPoints+" 分",small);
        GUILayout.Space(17);
        foreach(var seat in room.Seats)
        {
            var r=GUILayoutUtility.GetRect(790,66);
            PastoralMenuUI.Round(new Rect(r.x,r.y+1,r.width-5,59),new Color(.91f,.93f,.86f),9);
            PastoralMenuUI.Label(new Rect(r.x+17,r.y+16,410,30),ColorLabel(seat.SeatId)+"   "+(seat.Occupied||seat.IsAi?seat.DisplayName:"等待同伴入座")+(room.ManagerSeatId==seat.SeatId?" · 房主":""),18,BoardRenderer.SeatColor(seat.SeatId),true);
            PastoralMenuUI.Label(new Rect(r.x+420,r.y+21,190,25),seat.RejoinPending?"AI 托管 · 下回合接回":seat.ControlledByAi?"AI 操作中":seat.Ready?"已准备":seat.Connected?"在线 · 未准备":"等待连接",13,PastoralMenuUI.Muted);
            if(!room.Started&&!seat.Occupied&&!seat.IsAi&&PastoralMenuUI.Button(new Rect(r.xMax-129,r.y+12,110,36),"坐这里",!Network.Busy,seatColor:BoardRenderer.SeatColor(seat.SeatId)))Network.SelectSeat(seat.SeatId);
            if(room.ManagerSeatId==Network.SeatId&&seat.Occupied&&!seat.IsAi&&seat.SeatId!=Network.SeatId&&PastoralMenuUI.Button(new Rect(r.xMax-129,r.y+12,110,36),"转交房主",!Network.Busy))Network.Transfer(seat.SeatId);
        }
    }
    void DrawNetworkRecovery()
    {
        GUILayout.Label("连接与原席位恢复",label);GUILayout.Space(10);
        GUILayout.Label(Network.Endpoint??"尚未连接服务",small);GUILayout.Space(15);
        GUILayout.Label("离线或退出后由 AI 接手，重连后到下一次自己的回合恢复操作。",small);GUILayout.Space(17);
        if(!string.IsNullOrEmpty(Network.RoomId)&&GUILayout.Button("恢复本设备原席位",button,GUILayout.Height(40)))Network.Reconnect();
        if(Network.EnrollmentPending&&GUILayout.Button("恢复上次未确认的创建 / 加入请求",button,GUILayout.Height(40)))Network.RetryEnrollment();
        if(Network.EnrollmentPending&&GUILayout.Button("放弃待确认申请（可能失去已分配的席位）",button,GUILayout.Height(36)))Network.AbandonEnrollment();
        GUILayout.Space(17);
        if(GUILayout.Button("退出当前房间",button,GUILayout.Height(40)))Network.Leave();
        if(Network.ControlLost&&GUILayout.Button("忘记本设备的恢复身份",button,GUILayout.Height(36)))Network.ForgetIdentity();
    }
    void DrawNetworkSettings()
    {
        int section=GUILayout.Toolbar(networkSettingsPage,new[]{"房间信息","规则与地图","同桌设置"},button,GUILayout.Height(37));
        if(section!=networkSettingsPage){networkSettingsPage=section;GUI.FocusControl(null);}
        GUILayout.Space(22);
        if(networkSettingsPage==0)
        {
            GUILayout.Label("玩家："+LocalPlayerName+"（在设置中修改）",small);GUILayout.Space(12);
            NetworkField("房间名称",ref networkRoomName);
            if(Network.Room==null||networkLan&&networkTab==1)NetworkField("加入密码  ·  留空即可直接加入",ref networkPassword,true);
            else {GUILayout.Label(Network.Room.HasPassword?"此房间需要加入密码。":"此房间可直接加入。",small);GUILayout.Space(15);}
            networkListed=GUILayout.Toggle(networkListed,"在大厅中展示这张桌子",button,GUILayout.Height(32));
        }
        else if(networkSettingsPage==1)
        {
            // 与本地开局使用相同预览；整个区域保持在首屏视口内。
            var previewArea=GUILayoutUtility.GetRect(790,288);
            int selected=GUI.Toolbar(new Rect(previewArea.x,previewArea.y,previewArea.width,42),menuRule,RuleNames,button);
            if(selected!=menuRule){menuRule=selected;menuScenario=0;menuVictoryPoints=null;}
            MenuMapPreviews.DrawRule(new Rect(previewArea.x,previewArea.y+49,96,60),menuRule);
            PastoralMenuUI.Label(new Rect(previewArea.x+113,previewArea.y+55,previewArea.width-125,52),RuleDescriptions[menuRule],14,PastoralMenuUI.Muted);
            var ids=ScenarioIds();menuScenario=Mathf.Clamp(menuScenario,0,ids.Length-1);
            if(menuRule==1||menuRule==3)
            {
                if(PastoralMenuUI.Button(new Rect(previewArea.x,previewArea.y+119,43,36),"‹")){menuScenario=(menuScenario+ids.Length-1)%ids.Length;menuVictoryPoints=null;}
                PastoralMenuUI.Label(new Rect(previewArea.x+57,previewArea.y+119,previewArea.width-114,36),ScenarioName(ids[menuScenario]),18,null,true,TextAnchor.MiddleCenter);
                if(PastoralMenuUI.Button(new Rect(previewArea.xMax-43,previewArea.y+119,43,36),"›")){menuScenario=(menuScenario+1)%ids.Length;menuVictoryPoints=null;}
            }
            else PastoralMenuUI.Label(new Rect(previewArea.x,previewArea.y+119,previewArea.width,36),menuRule==0?"标准岛屿":"标准城市与骑士地图",18,null,true,TextAnchor.MiddleCenter);
            MenuMapPreviews.DrawScenarioThumbnail(new Rect(previewArea.x+6,previewArea.y+164,175,124),menuRule,ids[menuScenario],menuPlayers);
            PastoralMenuUI.Label(new Rect(previewArea.x+208,previewArea.y+192,previewArea.width-220,80),MenuMapPreviews.ScenarioCaption(menuRule,ids[menuScenario],menuPlayers),15,PastoralMenuUI.Muted);
        }
        else
        {
            GUILayout.Label("同桌人数",small);
            menuPlayers=GUILayout.Toolbar(menuPlayers-3,new[]{"3 人同桌","4 人同桌"},button,GUILayout.Height(42))+3;
            GUILayout.Space(20);GUILayout.Label("由当前 AI 控制的席位  ·  留出至少一席给你",small);
            GUILayout.BeginHorizontal();
            for(int i=0;i<menuPlayers;i++)
            {
                var colorButton=new GUIStyle(button);var color=BoardRenderer.SeatColor("P"+(i+1));
                colorButton.normal.textColor=colorButton.hover.textColor=colorButton.active.textColor=color;
                colorButton.onNormal.textColor=colorButton.onHover.textColor=colorButton.onActive.textColor=color;
                networkAi[i]=GUILayout.Toggle(networkAi[i],ColorLabel("P"+(i+1)),colorButton,GUILayout.Height(39));
            }
            GUILayout.EndHorizontal();
            if(Enumerable.Range(0,menuPlayers).All(i=>networkAi[i]))GUILayout.Label("请为房主保留至少一种颜色。",small);
            var ids=ScenarioIds();int defaults=DefaultVictoryPoints(RuleIds[menuRule],ids[menuScenario],menuPlayers),maximum=MaximumVictoryPoints(RuleIds[menuRule],ids[menuScenario]);
            if(menuVictoryPoints.HasValue)menuVictoryPoints=Mathf.Clamp(menuVictoryPoints.Value,5,maximum);
            int target=menuVictoryPoints??defaults;GUILayout.Space(26);GUILayout.Label("胜利目标",small);
            GUILayout.BeginHorizontal();
            bool state=GUI.enabled;GUI.enabled=state&&target>5;if(GUILayout.Button("−",button,GUILayout.Width(54),GUILayout.Height(40)))menuVictoryPoints=target-1;
            GUI.enabled=state;GUILayout.Label((menuVictoryPoints??defaults)+" 分",PastoralMenuUI.Center,GUILayout.Width(100),GUILayout.Height(40));
            GUI.enabled=state&&target<maximum;if(GUILayout.Button("+",button,GUILayout.Width(54),GUILayout.Height(40)))menuVictoryPoints=target+1;
            GUI.enabled=state;if(GUILayout.Button("恢复推荐 · "+defaults+" 分",button,GUILayout.Height(40)))menuVictoryPoints=null;
            GUILayout.EndHorizontal();
        }
    }

    void SyncNetworkEditor()
    {
        var room=Network.Room;if(room==null){networkEditorConfiguration=null;return;}
        string key=room.RoomId+"|"+room.Name+"|"+room.RuleSet+"|"+room.ScenarioId+"|"+room.PlayerCount+"|"+room.TargetVictoryPoints+"|"+room.Listed+"|"+string.Join(",",room.Seats.Where(s=>s.IsAi).Select(s=>s.SeatId));
        if(key==networkEditorConfiguration)return;networkEditorConfiguration=key;
        menuRule=Math.Max(0,Array.IndexOf(RuleIds,room.RuleSet=="cities"?"cities-knights":room.RuleSet));menuPlayers=room.PlayerCount;menuScenario=Math.Max(0,Array.IndexOf(ScenarioIds(),room.ScenarioId));menuVictoryPoints=room.TargetVictoryPoints;
        networkRoomName=room.Name;networkListed=room.Listed;
        for(int i=0;i<networkAi.Length;i++)networkAi[i]=room.Seats.Any(s=>s.SeatId=="P"+(i+1)&&s.IsAi);
    }
    void OnDestroy(){lanService.Dispose();}
}
