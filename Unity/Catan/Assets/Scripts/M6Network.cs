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
    int networkPresentedGame=-1;
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
        InitializeNetworkDiagnostics();
        // 显式验收入口使用独立配置，不读取玩家保存的服务器地址。
        if(!OnlineReviewMode)
        {
            menuOnlineAddress=PlayerPrefs.GetString(OnlineAddressPreference,M7OnlineEndpoint.DefaultEndpoint);
            if(string.IsNullOrWhiteSpace(menuOnlineAddress))menuOnlineAddress=M7OnlineEndpoint.DefaultEndpoint;
        }
        networkOnlineDraft=menuOnlineAddress;
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
        using var applyCost=FrameCost.Measure(FrameCost.Stage.PresentationApply);
        if(Network.Room==null||!Network.Room.Started||string.IsNullOrEmpty(Network.SeatId))return;
        if(!NetworkGame||networkPresentedGame!=Network.Room.GameNumber)
        {
            var room=Network.Room;networkPresentedGame=room.GameNumber;networkEntering=true;Network.Playing=true;networkMenu=false;
            try{StartGame(room.RuleSet=="cities"?"cities-knights":room.RuleSet,room.RuleSet=="base"||room.RuleSet=="cities"||room.RuleSet=="cities-knights"?"heading-for-new-shores":room.ScenarioId,room.PlayerCount,Network.SeatId,targetVictoryPoints:room.TargetVictoryPoints,threeKingdoms:room.ThreeKingdomsPackageVersion!=null);}
            finally{networkEntering=false;}
            Notice="已连接房间 "+room.RoomId+"。离线时 AI 接手，重连后在下次自己的回合恢复操作。";
        }
        else if(refresh!=null)refresh();
    }
    void BindNetwork(MonoBehaviour app,Func<string> acting,Func<bool> done,Action update,Action dispose,Action<bool> show)
    {
        BindHistory(()=>Network.History,()=>Network.HistoryRevision);
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
        networkPresentedGame=-1;
        close?.Invoke();presentation=null;actor=null;finished=null;decideAndSubmit=null;save=null;refresh=null;close=null;visible=null;MenuVisible=true;tableMenu=false;networkMenu=true;
    }
    RoomSettings NetworkSettings()=>new RoomSettings{Name=networkRoomName,RuleSet=RuleIds[menuRule],ScenarioId=menuRule==0||menuRule==2?"base":ScenarioIds()[Mathf.Clamp(menuScenario,0,ScenarioIds().Length-1)],PlayerCount=menuPlayers,TargetVictoryPoints=menuVictoryPoints,Listed=networkListed,AiSeats=Enumerable.Range(0,menuPlayers).Where(i=>networkAi[i]).Select(i=>"P"+(i+1)).ToArray(),ThreeKingdomsPackageVersion=menuThreeKingdoms&&(menuRule==2||menuRule==3)?Catan.Core.ThreeKingdoms.GeneralCatalog.B5PackageVersion:null,ThreeKingdomsContentHash=menuThreeKingdoms&&(menuRule==2||menuRule==3)?Catan.Core.ThreeKingdoms.GeneralCatalog.B5ContentHash:null};
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
            if(!networkLan)networkAddress=M7OnlineEndpoint.Normalize(networkAddress);
            Network.ConfigureEndpoint(networkAddress,networkLan?networkPin:"");Network.ListRooms();networkMessage="";
        }
        catch(Exception ex){networkMessage=ex.Message;}
    }
    int networkTab,networkSettingsPage,networkNavigation;
    string menuLanAddress="https://127.0.0.1:47771",menuLanPin="",menuOnlineAddress="",networkPresentedRoom;
    const string OnlineAddressPreference="catan.online-server-address";
    string networkOnlineDraft="";
    internal string OnlineReviewSettingsPath;
    bool OnlineReviewMode=>OnlineReviewSettingsPath!=null||Environment.GetCommandLineArgs().Contains("--online-lobby-verify");
    internal M7LobbyConnectionState OnlineLobbyState=>!networkLan&&CanUseMenuEndpoint?Network.LobbyConnectionState:M7LobbyConnectionState.Unconfigured;
    internal bool OnlineRoomActionsAvailable=>!networkLan&&OnlineLobbyState==M7LobbyConnectionState.Online&&!Network.Busy;
    string NetworkPlayerName=>OnlineReviewMode?"联机大厅验收玩家":LocalPlayerName;
    RoomSummary MenuNetworkRoom=>networkLan||CanUseMenuEndpoint?Network.Room:null;
    internal void ConfigureOnlineReview(string settingsPath)
    {
        OnlineReviewSettingsPath=settingsPath;
        menuOnlineAddress=File.Exists(settingsPath)?File.ReadAllText(settingsPath).Trim():"";
        networkOnlineDraft=menuOnlineAddress;
    }
    void SaveOnlineServerAddress()
    {
        Network.ClearError();
        try
        {
            string address=M7OnlineEndpoint.Normalize(networkOnlineDraft);
            if(OnlineReviewSettingsPath!=null)File.WriteAllText(OnlineReviewSettingsPath,address);
            else if(!OnlineReviewMode){PlayerPrefs.SetString(OnlineAddressPreference,address);PlayerPrefs.Save();}
            menuOnlineAddress=networkAddress=networkOnlineDraft=address;networkPin="";
            networkTab=0;networkScroll=Vector2.zero;networkNavigation++;CancelJoinPassword();
            ConnectNetworkEndpoint();
        }
        catch(Exception ex){networkMessage=M7NetworkErrors.Describe("保存服务器地址",ex);}
    }
    void RefreshOnlineLobby()
    {
        if(string.IsNullOrWhiteSpace(menuOnlineAddress)){networkTab=3;return;}
        networkAddress=menuOnlineAddress;networkPin="";networkMessage="";
        if(CanUseMenuEndpoint&&Network.Busy)return;
        if(!CanUseMenuEndpoint)ConnectNetworkEndpoint();else Network.ListRooms();
    }
    bool CanUseMenuEndpoint=>Network.EndpointConfigured&&!string.IsNullOrEmpty(Network.Endpoint)&&
        string.Equals(Network.Endpoint,(networkAddress??"").Trim().TrimEnd('/'),StringComparison.OrdinalIgnoreCase)&&
        (networkLan?M7Client.NormalizeFingerprint(networkPin)==Network.Fingerprint:string.IsNullOrEmpty(Network.Fingerprint));
    void OpenSeparatedNetworkMenu(bool local)
    {
        networkNavigation++;CancelJoinPassword();
        if(networkLan){menuLanAddress=networkAddress;menuLanPin=networkPin;}else menuOnlineAddress=networkAddress;
        networkLan=local;networkAddress=local?menuLanAddress:menuOnlineAddress;networkPin=local?menuLanPin:"";
        networkTab=0;networkScroll=Vector2.zero;networkMessage="";networkOnlineDraft=menuOnlineAddress;
        OpenNetworkMenu();
        if(local)DiscoverLan();else if(!string.IsNullOrWhiteSpace(menuOnlineAddress))RefreshOnlineLobby();
    }
    int lanRoomPage;
    void DrawLanShell()
    {
        if(pastoralMenuFont==null)pastoralMenuFont=Resources.Load<Font>("Fonts/NotoSerifCJKsc-Regular");
        PastoralMenuUI.Styles(pastoralMenuFont??font);PastoralMenuScene.Draw(Time.unscaledTime);
        PastoralMenuUI.Label(new Rect(78,76,300,28),"卡坦岛 · 之三国争霸",18,PastoralMenuUI.Gold);
        PastoralMenuUI.Fill(new Rect(79,120,275,1),new Color(.86f,.69f,.37f,.5f));
        PastoralMenuUI.Label(new Rect(72,150,350,80),networkLan?"局域网联机":"在线大厅",43,PastoralMenuUI.Ivory,true);
        PastoralMenuUI.Label(new Rect(79,254,300,62),"玩家："+NetworkPlayerName,18,PastoralMenuUI.Ivory);
        if(PastoralMenuUI.Nav(new Rect(65,350,322,68),"房间列表",networkLan?"自动寻找身边的同伴":"浏览大厅，与好友同桌",networkTab==0,0)){networkTab=0;networkScroll=Vector2.zero;}
        if(PastoralMenuUI.Nav(new Rect(65,435,322,68),"创建房间","选择玩法，等待好友入座",networkTab==1,0)){networkTab=1;networkSettingsPage=0;networkScroll=Vector2.zero;}
        if(MenuNetworkRoom!=null&&PastoralMenuUI.Nav(new Rect(65,520,322,68),"当前房间",MenuNetworkRoom.Name,networkTab==2,0)){networkTab=2;networkScroll=Vector2.zero;}
        if(!networkLan&&PastoralMenuUI.Nav(new Rect(65,MenuNetworkRoom==null?520:605,322,68),"连接设置","",networkTab==3,0)){networkTab=3;networkScroll=Vector2.zero;}
        if(PastoralMenuUI.ReturnButton(new Rect(79,762,290,42))){networkMenu=false;menuPage=-1;}
        PastoralMenuUI.Label(new Rect(79,840,310,25),ReleaseVersion,12,new Color(.76f,.69f,.54f));
    }
    void DrawLanNetworkMenu()
    {
        if(!networkLan&&pendingJoinRoom!=null&&OnlineLobbyState==M7LobbyConnectionState.Offline)CancelJoinPassword();
        if(MenuNetworkRoom?.RoomId!=networkPresentedRoom)
        {
            networkPresentedRoom=MenuNetworkRoom?.RoomId;
            if(MenuNetworkRoom!=null){networkTab=2;lanRoomPage=0;}else if(networkTab==2)networkTab=0;
            networkScroll=Vector2.zero;
        }
        bool shellEnabled=GUI.enabled;GUI.enabled=shellEnabled&&pendingJoinRoom==null;DrawLanShell();GUI.enabled=shellEnabled;if(!networkMenu)return;
        bool before=GUI.enabled;GUI.enabled=before&&menuPendingAction==null&&pendingJoinRoom==null;
        PastoralMenuUI.Panel(new Rect(454,56,920,788));
        MenuPageHeader(networkLan?"近 处 的 同 伴":"远 方 的 旅 伴",networkTab==0?"房间列表":networkTab==1?"创建房间":networkTab==3?"服务器地址":"当前房间",
            networkTab==0?(networkLan?"自动搜索同一网络的房间，选中后即可加入。":"选择大厅中的房间加入，或创建你们自己的游戏。"):
            networkTab==1?"配置这一桌的玩法与同伴。":networkTab==3?"首次保存后，每次打开在线大厅都会自动连接。":"选择建筑颜色、准备或恢复原来的对局。");
        if(networkTab==0)
        {
            if(networkLan)
            {if(PastoralMenuUI.Button(new Rect(1123,216,210,39),networkWorking?"正在搜索…":"重新搜索",!networkWorking,true))DiscoverLan();}
            else if(PastoralMenuUI.Button(new Rect(1123,216,210,39),Network.Busy?"正在刷新…":OnlineLobbyState==M7LobbyConnectionState.Unconfigured?"设置服务器":"刷新房间",!Network.Busy,true))RefreshOnlineLobby();
            PastoralMenuUI.Label(new Rect(498,222,585,27),"选择房间加入 · 身份按房间保留，可随时换桌",13,PastoralMenuUI.Muted);
        }
        else if(networkTab==2)
        {
            SyncNetworkEditor();lanRoomPage=PastoralMenuUI.Segments(new Rect(495,216,838,39),lanRoomPage,new[]{"同桌玩家","房间设置","连接与恢复"});
        }
        var savedLabel=label;var savedTitle=title;var savedSmall=small;var savedButton=button;
        label=PastoralMenuUI.Text;title=PastoralMenuUI.Title;small=PastoralMenuUI.Small;button=PastoralMenuUI.Control;
        bool onlineUnavailable=!networkLan&&networkTab!=3&&OnlineLobbyState!=M7LobbyConnectionState.Online;
        GUILayout.BeginArea(new Rect(495,282,838,370));networkScroll=GUILayout.BeginScrollView(networkScroll);
        if(onlineUnavailable)GUILayout.Space(1);
        else if(networkTab==0){if(networkLan)DrawLanRooms();else DrawOnlineRooms();}
        else if(networkTab==1)DrawNetworkSettings();
        else if(networkTab==3)DrawOnlineServerSettings();
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
        MobileTabletop.ScrollTouch(new Rect(495,282,838,370),ref networkScroll);
        label=savedLabel;title=savedTitle;small=savedSmall;button=savedButton;
        if(onlineUnavailable)DrawOnlineLobbyMessage();
        PastoralMenuUI.Fill(new Rect(495,701,838,1),new Color(.48f,.54f,.42f,.20f));
        DrawNetworkStatus();
        if(networkTab==1)
        {
            if(networkSettingsPage>0&&PastoralMenuUI.Button(new Rect(956,779,124,40),"上一步"))networkSettingsPage--;
            if(PastoralMenuUI.Button(new Rect(1093,779,240,40),networkWorking||!networkLan&&Network.Busy?"正在创建…":networkSettingsPage<2?"下一步  →":"创建并加入房间",!networkWorking&&(networkLan||OnlineRoomActionsAvailable),true))
            {if(networkSettingsPage<2)networkSettingsPage++;else if(networkLan)CreateLanRoom();else CreateOnlineRoom();}
        }
        else if(networkTab==3)
        {if(PastoralMenuUI.Button(new Rect(1093,779,240,40),"保存并连接大厅  →",!string.IsNullOrWhiteSpace(networkOnlineDraft),true))SaveOnlineServerAddress();}
        else if(networkTab==2&&Network.Room!=null)
        {
            var room=Network.Room;
            if(lanRoomPage==0&&!room.Started)
            {
                var own=room.Seats.FirstOrDefault(s=>s.SeatId==Network.SeatId);
                if(PastoralMenuUI.Button(new Rect(1093,779,240,40),own!=null&&own.Ready?"取消准备":"我准备好了",!Network.Busy&&!onlineUnavailable,true))Network.Ready(own==null||!own.Ready);
                bool allReady=room.Seats.Length==room.PlayerCount&&room.Seats.All(s=>s.IsAi||s.Occupied&&s.Connected&&s.Ready);
                if(room.ManagerSeatId==Network.SeatId&&PastoralMenuUI.Button(new Rect(839,779,240,40),allReady?"开始游戏":room.Seats.Any(s=>!s.Occupied&&!s.IsAi)?"等待同伴入座":"等待玩家准备",allReady&&!Network.Busy&&!onlineUnavailable))Network.StartRoom();
            }
            if(lanRoomPage==1&&room.ManagerSeatId==Network.SeatId&&!room.Started&&PastoralMenuUI.Button(new Rect(1093,779,240,40),"应用房间配置",!Network.Busy&&!onlineUnavailable,true))Network.ConfigureRoom(NetworkSettings());
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
            PastoralMenuUI.RoomRow(new Rect(r.x,r.y+2,r.width-6,79));
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
    string NetworkMenuStatus=>!networkLan&&OnlineLobbyState==M7LobbyConnectionState.Unconfigured&&string.IsNullOrEmpty(networkMessage)?"填写并保存服务器公网 IP 后即可联机。":
        (!string.IsNullOrEmpty(Network.LastError)?Network.LastError:!string.IsNullOrEmpty(networkMessage)?networkMessage:Network.Status)+(networkLan?(lanPreparing?"\n本机托管服务准备中，可继续搜索和加入好友房间。":string.IsNullOrEmpty(lanPreparationIssue)?"":"\n"+lanPreparationIssue):"");
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
        if(!networkLan&&!OnlineRoomActionsAvailable)return;
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
        if(PastoralMenuUI.Button(new Rect(940,316,38,34),"×"))CancelJoinPassword();
        if(pendingJoinRoom!=null&&PastoralMenuUI.Button(new Rect(822,519,156,43),"确认加入",!Network.Busy&&(networkLan||OnlineRoomActionsAvailable)&&joinPassword.Length>0,true))
            ConfirmJoinPassword();
        if(Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.Escape){CancelJoinPassword();Event.current.Use();}
        else if(Event.current.type==EventType.KeyDown&&(Event.current.keyCode==KeyCode.Return||Event.current.keyCode==KeyCode.KeypadEnter)){ConfirmJoinPassword();Event.current.Use();}
    }
    void ConfirmJoinPassword()
    {
        if(pendingJoinRoom==null||Network.Busy||joinPassword.Length==0||!networkLan&&!OnlineRoomActionsAvailable)return;
        var room=pendingJoinRoom;var server=pendingJoinServer;string password=joinPassword;
        CancelJoinPassword();JoinSelectedRoom(server,room.RoomId,password);
    }
    void JoinSelectedRoom(M7LanService.Advertisement server,string roomId,string password)
    {
        if(!networkLan&&!OnlineRoomActionsAvailable)return;
        try
        {
            if(server!=null){networkAddress=server.Address;networkPin=server.certificateFingerprint;}
            networkMessage="";
            if(!CanUseMenuEndpoint)Network.ConfigureEndpoint(networkAddress,networkLan?networkPin:"",false);
            Network.Join(roomId,NetworkPlayerName,password);
        }
        catch(Exception ex){networkMessage=M7NetworkErrors.Describe("加入房间",ex);}
    }
    async void CreateLanRoom()
    {
        if(networkWorking)return;
        var settings=NetworkSettings();string name=NetworkPlayerName,password=networkPassword;int navigation=networkNavigation;
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
    void DrawNetworkMenu()=>DrawLanNetworkMenu();
    void NetworkField(string caption,ref string value,bool password=false)
    {
        GUILayout.Label(caption,small);
        value=password?GUILayout.PasswordField(value,'●',PastoralMenuUI.Field,GUILayout.Height(38)):
            GUILayout.TextField(value,PastoralMenuUI.Field,GUILayout.Height(38));
        GUILayout.Space(12);
    }
    void DrawOnlineServerSettings()
    {
        GUILayout.Label("填写云服务器的公网 IP",label);GUILayout.Space(12);
        GUILayout.Label("可附带端口；未填写端口时使用 47771。地址只需在本设备保存一次。",small);GUILayout.Space(22);
        NetworkField("服务器公网 IP 与端口",ref networkOnlineDraft);
        GUILayout.Space(10);
        GUILayout.Label("保存后进入大厅，所有玩家都能在这里创建或加入房间。",small);
        if(!string.IsNullOrEmpty(menuOnlineAddress))
        {GUILayout.Space(14);GUILayout.Label("已保存："+menuOnlineAddress,small);}
        if(Network.Room!=null)
        {GUILayout.Space(14);GUILayout.Label("切换服务器地址后，将离开当前房间并连接新的大厅。",small);}
    }
    void DrawOnlineLobbyMessage()
    {
        string heading,detail;
        switch(OnlineLobbyState)
        {
            case M7LobbyConnectionState.Connecting:
                heading="正在连接联机服务器…";detail="连接成功后，将自动显示可以加入的房间。";break;
            case M7LobbyConnectionState.Offline:
                heading="联机服务器不在线";detail="暂时无法连接服务器，恢复连接后会自动刷新房间。";break;
            default:
                heading="尚未配置联机服务器";detail="填写云服务器公网 IP，保存后即可进入在线大厅。";break;
        }
        PastoralMenuUI.Label(new Rect(515,374,798,49),heading,29,null,true,TextAnchor.MiddleCenter);
        PastoralMenuUI.Label(new Rect(515,432,798,34),detail,16,PastoralMenuUI.Muted,false,TextAnchor.MiddleCenter);
        if(!string.IsNullOrEmpty(menuOnlineAddress))PastoralMenuUI.Label(new Rect(515,472,798,30),menuOnlineAddress,13,PastoralMenuUI.Muted,false,TextAnchor.MiddleCenter);
        bool unconfigured=OnlineLobbyState==M7LobbyConnectionState.Unconfigured;
        if(!unconfigured&&PastoralMenuUI.Button(new Rect(717,528,188,42),OnlineLobbyState==M7LobbyConnectionState.Connecting?"正在连接…":"立即重试",!Network.Busy&&OnlineLobbyState!=M7LobbyConnectionState.Connecting,true))RefreshOnlineLobby();
        if(PastoralMenuUI.Button(new Rect(unconfigured?820:923,528,188,42),unconfigured?"设置服务器地址":"修改服务器地址",true,unconfigured)){networkTab=3;networkScroll=Vector2.zero;}
    }
    void CreateOnlineRoom()
    {
        if(!OnlineRoomActionsAvailable)return;
        networkMessage="";Network.Create(NetworkSettings(),NetworkPlayerName,networkPassword);
    }
    void DrawOnlineRooms()
    {
        if(Network.Rooms.Length==0)
        {
            var r=GUILayoutUtility.GetRect(790,260);
            PastoralMenuUI.Label(new Rect(r.x+15,r.y+73,r.width-30,45),"还没有开放房间",27,null,true,TextAnchor.MiddleCenter);
            PastoralMenuUI.Label(new Rect(r.x+15,r.y+127,r.width-30,31),"创建第一桌游戏，邀请好友一起加入。",16,PastoralMenuUI.Muted,false,TextAnchor.MiddleCenter);
            if(PastoralMenuUI.Button(new Rect(r.center.x-104,r.y+184,208,42),"创建房间",OnlineRoomActionsAvailable,true)){networkTab=1;networkSettingsPage=0;networkScroll=Vector2.zero;}
        }
        foreach(var room in Network.Rooms)
        {
            var r=GUILayoutUtility.GetRect(790,86);
            PastoralMenuUI.RoomRow(new Rect(r.x,r.y+2,r.width-6,79));
            PastoralMenuUI.Label(new Rect(r.x+18,r.y+10,r.width-157,28),room.Name+"  ·  "+room.OccupiedSeats+"/"+room.PlayerCount+" 人",18,null,true);
            bool remembered=Network.HasRoomIdentity(room.RoomId),joined=JoinedRoom(room);
            string status=joined?"我的房间 · "+(room.Started?"对局中":"等待开局"):remembered&&room.Started?"我的进行中对局 · 可恢复原玩家":room.Started?"对局中 · 原玩家可恢复":"等待开局";
            PastoralMenuUI.Label(new Rect(r.x+18,r.y+43,r.width-155,26),NetworkRuleCaption(room.RuleSet)+" · "+status+(M7RoomEntry.NeedsPassword(room.HasPassword,remembered,room.Started)?" · 需要密码":" · 直接加入"),13,PastoralMenuUI.Muted);
            if(joined)PastoralMenuUI.Label(new Rect(r.xMax-122,r.y+23,104,37),"已加入",16,PastoralMenuUI.Muted,false,TextAnchor.MiddleCenter);
            else if(PastoralMenuUI.Button(new Rect(r.xMax-122,r.y+23,104,37),remembered&&room.Started?"重连":"加入",OnlineRoomActionsAvailable))RequestJoinRoom(room,null,remembered);
        }
        GUILayout.Space(15);GUILayout.Label("按房间号加入",small);GUILayout.Space(6);
        GUILayout.BeginHorizontal();networkRoomId=GUILayout.TextField(networkRoomId,PastoralMenuUI.Field,GUILayout.Height(38));
        bool enabled=GUI.enabled;GUI.enabled=enabled&&OnlineRoomActionsAvailable&&!string.IsNullOrWhiteSpace(networkRoomId);
        if(GUILayout.Button("查找并加入",button,GUILayout.Width(150),GUILayout.Height(38)))
        {int navigation=networkNavigation;Network.DescribeRoom(networkRoomId,room=>{if(navigation==networkNavigation&&networkMenu&&!networkLan)RequestJoinRoom(room,null,Network.HasRoomIdentity(room.RoomId));});}
        GUI.enabled=enabled;GUILayout.EndHorizontal();
    }
    void DrawNetworkSeats()
    {
        var room=Network.Room;
        GUILayout.Label(room.Name+"  ·  房间号 "+room.RoomId,label);
        GUILayout.BeginHorizontal();
        int target=room.TargetVictoryPoints??DefaultVictoryPoints(room.RuleSet,room.ScenarioId,room.PlayerCount);
        GUILayout.Label(NetworkRuleCaption(room.RuleSet)+" · "+room.PlayerCount+" 人 · "+target+" 分 · 你的颜色："+ColorLabel(Network.SeatId),small);
        if(GUILayout.Button("复制房间号",button,GUILayout.Width(125),GUILayout.Height(28)))GUIUtility.systemCopyBuffer=room.RoomId;
        GUILayout.EndHorizontal();GUILayout.Space(12);
        for(int row=0;row<room.Seats.Length;row+=2)
        {
            var line=GUILayoutUtility.GetRect(790,140);
            for(int column=0;column<2&&row+column<room.Seats.Length;column++)
            {
                var seat=room.Seats[row+column];float width=(line.width-20)*.5f;
                var r=new Rect(line.x+column*(width+14),line.y+2,width,130);
                PastoralMenuUI.RoomRow(r);
                var seatColor=BoardRenderer.SeatColor(seat.SeatId);
                PastoralMenuUI.Hex(new Vector2(r.x+27,r.y+29),11,seatColor);
                PastoralMenuUI.Label(new Rect(r.x+50,r.y+14,r.width-64,28),ColorLabel(seat.SeatId)+(seat.SeatId==Network.SeatId?" · 你":"")+(room.ManagerSeatId==seat.SeatId?" · 房主":""),16,PastoralMenuUI.Muted);
                string name=seat.Occupied||seat.IsAi?seat.DisplayName:"等待同伴入座";
                var nameStyle=new GUIStyle(PastoralMenuUI.Text){fontSize=20,fontStyle=FontStyle.Bold,wordWrap=false,clipping=TextClipping.Clip};
                GUI.Label(new Rect(r.x+18,r.y+45,r.width-36,32),new GUIContent(name,name),nameStyle);
                PastoralMenuUI.Label(new Rect(r.x+18,r.y+91,r.width-145,26),seat.RejoinPending?"AI 托管 · 下回合接回":seat.ControlledByAi?"AI 操作中":seat.Ready?"已准备":!seat.Occupied?"空席":seat.Connected?"在线 · 未准备":"离线",13,seat.Ready?PastoralMenuUI.Forest:PastoralMenuUI.Muted);
                if(!room.Started&&!seat.Occupied&&!seat.IsAi&&PastoralMenuUI.Button(new Rect(r.xMax-117,r.y+86,100,33),"坐这里",!Network.Busy))Network.SelectSeat(seat.SeatId);
                if(room.ManagerSeatId==Network.SeatId&&seat.Occupied&&!seat.IsAi&&seat.SeatId!=Network.SeatId&&PastoralMenuUI.Button(new Rect(r.xMax-117,r.y+86,100,33),"转交房主",!Network.Busy))Network.Transfer(seat.SeatId);
            }
        }
    }
    static string NetworkRuleCaption(string rule)=>rule=="seafarers"?"航海家":rule=="cities-knights"?"城市与骑士":rule=="combined"?"双扩展组合":"基础版";
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
            GUILayout.Label("玩家："+NetworkPlayerName+"（在设置中修改）",small);GUILayout.Space(12);
            NetworkField("房间名称",ref networkRoomName);
            if(Network.Room==null||networkTab==1)NetworkField("加入密码  ·  留空即可直接加入",ref networkPassword,true);
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
            if(menuRule==2||menuRule==3)menuThreeKingdoms=GUILayout.Toggle(menuThreeKingdoms,"三国争霸 B5 · 49将棋盘状态包",button,GUILayout.Height(38));
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
        string key=room.RoomId+"|"+room.Name+"|"+room.RuleSet+"|"+room.ScenarioId+"|"+room.PlayerCount+"|"+room.TargetVictoryPoints+"|"+room.Listed+"|"+room.ThreeKingdomsPackageVersion+"|"+string.Join(",",room.Seats.Where(s=>s.IsAi).Select(s=>s.SeatId));
        if(key==networkEditorConfiguration)return;networkEditorConfiguration=key;
        menuRule=Math.Max(0,Array.IndexOf(RuleIds,room.RuleSet=="cities"?"cities-knights":room.RuleSet));menuPlayers=room.PlayerCount;menuScenario=Math.Max(0,Array.IndexOf(ScenarioIds(),room.ScenarioId));menuVictoryPoints=room.TargetVictoryPoints;
        networkRoomName=room.Name;networkListed=room.Listed;menuThreeKingdoms=room.ThreeKingdomsPackageVersion!=null;
        for(int i=0;i<networkAi.Length;i++)networkAi[i]=room.Seats.Any(s=>s.SeatId=="P"+(i+1)&&s.IsAi);
    }
    void OnDestroy(){StopNetworkDiagnostics();lanService.Dispose();}
}
