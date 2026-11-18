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
    bool networkViewDirty;
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
        Network.ReactionReceived+=PlayReaction;
        Network.CommandFeedback+=message=>{if(!string.IsNullOrEmpty(message)){Notice=message;ShowTablePrompt(message);}generalSelection.Reset();generalSubmitting=false;selectionView=null;};
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
    {networkViewDirty=true;Network.PresentationPending=true;}
    void FlushPendingNetworkView()
    {
        if(!networkViewDirty)return;
        networkViewDirty=false;
        try{FlushNetworkView();}finally{if(Network!=null)Network.PresentationPending=false;}
    }
    void FlushNetworkView()
    {
        using var applyCost=FrameCost.Measure(FrameCost.Stage.PresentationApply);
        if(Network.Room==null||!Network.Room.Started||string.IsNullOrEmpty(Network.SeatId))return;
        if(!NetworkGame||networkPresentedGame!=Network.Room.GameNumber)
        {
            var room=Network.Room;networkPresentedGame=room.GameNumber;networkEntering=true;Network.Playing=true;networkMenu=false;
            try{StartGame(room.RuleSet=="cities"?"cities-knights":room.RuleSet,room.RuleSet=="base"||room.RuleSet=="cities"||room.RuleSet=="cities-knights"?"heading-for-new-shores":room.ScenarioId,room.PlayerCount,Network.SeatId,targetVictoryPoints:room.TargetVictoryPoints,threeKingdoms:room.ThreeKingdomsPackageVersion!=null,harbormaster:room.Harbormaster,fishing:room.Fishing);}
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
    public void ConnectNetwork(M4App app)=>BindNetwork(app,()=>app.M6View.Discards.Any(d=>d.PlayerId==app.M6View.PlayerId)?Catan.Core.ThreeKingdoms.B2SkillEngine.ControllerForViewB6(app.M6View.ThreeKingdoms,app.M6View.PlayerId):M4App.Actor(app.M6View),()=>app.M6View.Phase==Catan.Core.M4.GamePhase.Finished,app.M6Refresh,app.M6Close,app.M6Visible);
    public void ConnectNetwork(M5App app)=>BindNetwork(app,()=>app.M6View.Discards.Any(d=>d.PlayerId==app.M6View.PlayerId)?Catan.Core.ThreeKingdoms.B2SkillEngine.ControllerForViewB6(app.M6View.ThreeKingdoms,app.M6View.PlayerId):M5App.Actor(app.M6View),()=>app.M6View.Phase==Catan.Core.M5.GamePhase.Finished,app.M6Refresh,app.M6Close,app.M6Visible);
    void OpenNetworkMenu(){networkMenu=true;MenuVisible=true;if(presentation!=null)presentation.enabled=false;visible?.Invoke(false);}
    void CloseNetworkPresentation()
    {
        ResetReactions();
        networkViewDirty=false;
        networkPresentedGame=-1;
        close?.Invoke();presentation=null;actor=null;finished=null;decideAndSubmit=null;save=null;refresh=null;close=null;visible=null;MenuVisible=true;tableMenu=false;networkMenu=true;
    }
    RoomSettings NetworkSettings()=>new RoomSettings{Harbormaster=menuHarbormaster,Fishing=menuFishing,TradeModulesVersion=menuHarbormaster||menuFishing?NetworkProtocol.TradeModulesVersion:null,Name=networkRoomName,RuleSet=RuleIds[menuRule],ScenarioId=menuRule==0||menuRule==2?"base":ScenarioIds()[Mathf.Clamp(menuScenario,0,ScenarioIds().Length-1)],PlayerCount=menuPlayers,TargetVictoryPoints=menuVictoryPoints,Listed=networkListed,AiSeats=Enumerable.Range(0,menuPlayers).Where(i=>networkAi[i]).Select(i=>"P"+(i+1)).ToArray(),ThreeKingdomsPackageVersion=menuThreeKingdoms&&(menuRule==2||menuRule==3)?Catan.Core.ThreeKingdoms.GeneralCatalog.B6PackageVersion:null,ThreeKingdomsContentHash=menuThreeKingdoms&&(menuRule==2||menuRule==3)?Catan.Core.ThreeKingdoms.GeneralCatalog.B6ContentHash:null};
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
            networkMessage="";
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
    int networkTab,networkNavigation;
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
    RoomSummary pendingJoinRoom;
    M7LanService.Advertisement pendingJoinServer;
    string joinPassword="";
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
    void DrawNetworkMenu()=>AcceptedPage(true);
    void CreateOnlineRoom()
    {
        if(!OnlineRoomActionsAvailable)return;
        networkMessage="";Network.Create(NetworkSettings(),NetworkPlayerName,networkPassword);
    }
    void SyncNetworkEditor()
    {
        var room=Network.Room;if(room==null){networkEditorConfiguration=null;return;}
        string key=room.RoomId+"|"+room.Name+"|"+room.RuleSet+"|"+room.ScenarioId+"|"+room.PlayerCount+"|"+room.TargetVictoryPoints+"|"+room.Listed+"|"+room.ThreeKingdomsPackageVersion+"|"+room.Harbormaster+"|"+room.Fishing+"|"+string.Join(",",room.Seats.Where(s=>s.IsAi).Select(s=>s.SeatId));
        if(key==networkEditorConfiguration)return;networkEditorConfiguration=key;
        menuRule=Math.Max(0,Array.IndexOf(RuleIds,room.RuleSet=="cities"?"cities-knights":room.RuleSet));menuPlayers=room.PlayerCount;menuScenario=Math.Max(0,Array.IndexOf(ScenarioIds(),room.ScenarioId));menuVictoryPoints=room.TargetVictoryPoints;
        networkRoomName=room.Name;networkListed=room.Listed;menuThreeKingdoms=room.ThreeKingdomsPackageVersion!=null;menuHarbormaster=room.Harbormaster;menuFishing=room.Fishing;
        for(int i=0;i<networkAi.Length;i++)networkAi[i]=room.Seats.Any(s=>s.SeatId=="P"+(i+1)&&s.IsAi);
    }
    void OnDestroy(){if(bookImageMaterial!=null)Destroy(bookImageMaterial);ReleaseBookMap();ResetReactions();StopNetworkDiagnostics();lanService.Dispose();}
}
