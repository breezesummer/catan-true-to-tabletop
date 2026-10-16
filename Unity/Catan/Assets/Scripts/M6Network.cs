using System;
using System.IO;
using System.Linq;
using Catan.Network.Contracts;
using UnityEngine;

public sealed partial class M6App
{
    public M7Client Network { get; private set; }
    public bool NetworkGame=>Network!=null&&Network.Playing;
    readonly M7LanService lanService=new M7LanService();
    bool networkMenu,networkEntering,networkLan=true,networkPairConfirmed,networkWorking;
    string networkAddress="https://127.0.0.1:47771",networkPin="",networkName="玩家",networkRoomId="",networkPassword="",networkRoomName="卡坦好友房间",networkMessage="";
    bool networkListed=true;
    readonly bool[] networkAi={false,false,false,false};
    M7LanService.Advertisement[] networkFound=Array.Empty<M7LanService.Advertisement>();
    Vector2 networkScroll;
    string networkEditorConfiguration;
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
            Notice="已连接房间 "+room.RoomId+"，你控制 "+Network.SeatId+"。服务器自动保存，掉线时等待原席位。";
        }
        else if(refresh!=null)refresh();
    }
    void BindNetwork(MonoBehaviour app,Func<string> acting,Func<bool> done,Action update,Action dispose,Action<bool> show)
    {
        BindHistory(()=>Network.History,()=>Network.History.Length);
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
        try{networkPin=await lanService.Start(Path.Combine(Application.persistentDataPath,"m7-server"));networkAddress="https://127.0.0.1:47771";networkLan=true;networkPairConfirmed=false;networkMessage="服务已启动。请核对下方指纹并固定；其他玩家填写本机局域网 IP。关闭本程序会停止这台主机的服务。";}
        catch(Exception ex){networkMessage=ex.Message;}
        finally{networkWorking=false;}
    }
    async void DiscoverLan()
    {
        if(networkWorking)return;networkWorking=true;networkMessage="正在搜索同网段服务…";
        try{networkFound=await lanService.Discover();networkMessage=networkFound.Length==0?"未发现服务，可手动填写 IP；请检查防火墙与 Wi-Fi 客户端隔离。":"请选择服务，并与房主展示的证书指纹逐字核对。";}
        catch(Exception){networkMessage="局域网发现失败，可手动填写 IP 与房主提供的指纹。";}
        finally{networkWorking=false;}
    }
    void ConnectNetworkEndpoint()
    {
        try
        {
            if(networkLan&&!networkPairConfirmed){networkMessage="请先与房主核对指纹，再勾选固定此证书。";return;}
            Network.ConfigureEndpoint(networkAddress,networkLan?networkPin:"");Network.ListRooms();networkMessage="";
        }
        catch(Exception ex){networkMessage=ex.Message;}
    }
    int networkTab,networkSettingsPage;
    string menuLanAddress="https://127.0.0.1:47771",menuLanPin="",menuOnlineAddress="",networkPresentedRoom;
    bool CanUseMenuEndpoint=>!string.IsNullOrEmpty(Network.Endpoint)&&
        string.Equals(Network.Endpoint,(networkAddress??"").Trim().TrimEnd('/'),StringComparison.OrdinalIgnoreCase)&&
        (networkLan?M7Client.NormalizeFingerprint(networkPin)==Network.Fingerprint:string.IsNullOrEmpty(Network.Fingerprint));
    void OpenSeparatedNetworkMenu(bool local)
    {
        if(Network.Room!=null&&local!=networkLan){networkMessage="请先退出当前房间，再切换联机方式。";OpenNetworkMenu();return;}
        if(networkLan){menuLanAddress=networkAddress;menuLanPin=networkPin;}else menuOnlineAddress=networkAddress;
        if(local!=networkLan)networkPairConfirmed=false;
        networkLan=local;networkAddress=local?menuLanAddress:menuOnlineAddress;networkPin=local?menuLanPin:"";
        networkTab=0;networkScroll=Vector2.zero;
        OpenNetworkMenu();
    }
    void DrawNetworkMenu()
    {
        if(Network.Room?.RoomId!=networkPresentedRoom){networkPresentedRoom=Network.Room?.RoomId;networkTab=0;networkScroll=Vector2.zero;}
        SyncNetworkEditor();DrawPastoralShell(networkLan?2:3);
        if(!networkMenu)return;
        bool before=GUI.enabled;GUI.enabled=before&&menuPendingAction==null;
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
        string status=string.IsNullOrEmpty(networkMessage)?Network.Status:networkMessage;
        PastoralMenuUI.Label(new Rect(498,720,815,32),status,12,PastoralMenuUI.Muted);
        if(Network.Room==null)
        {
            if(networkTab==0&&PastoralMenuUI.Button(new Rect(1093,779,240,40),networkWorking||Network.Busy?"正在连接…":"连接并进入大厅  →",!networkWorking&&!Network.Busy,true))
            {ConnectNetworkEndpoint();if(CanUseMenuEndpoint)networkTab=1;}
            if(networkTab==1&&PastoralMenuUI.Button(new Rect(1093,779,240,40),"刷新房间列表",CanUseMenuEndpoint&&!Network.Busy,true))Network.ListRooms();
            if(networkTab==2)
            {
                if(networkSettingsPage>0&&PastoralMenuUI.Button(new Rect(956,779,124,40),"上一步"))networkSettingsPage--;
                if(PastoralMenuUI.Button(new Rect(1093,779,240,40),networkSettingsPage<2?"下一步  →":"创建并加入房间",networkSettingsPage<2||CanUseMenuEndpoint&&!Network.Busy,true))
                {if(networkSettingsPage<2)networkSettingsPage++;else Network.Create(NetworkSettings(),networkName,networkPassword);}
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
        GUI.enabled=before;DrawMenuConfirmation();
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
            string pin=networkPin;NetworkField("主机证书指纹  ·  请与房主展示的 SHA-256 指纹核对",ref pin);
            if(pin!=networkPin){networkPin=pin;networkPairConfirmed=false;}
            networkPairConfirmed=GUILayout.Toggle(networkPairConfirmed,"已与房主核对指纹，固定此地址的证书",button,GUILayout.Height(33));
            foreach(var found in networkFound)
                if(GUILayout.Button(found.serverName+"  ·  "+found.Address,button,GUILayout.Height(36)))
                {networkAddress=found.Address;networkPin=found.certificateFingerprint;networkPairConfirmed=false;}
        }
        else {GUILayout.Space(12);GUILayout.Label("连接后，可浏览房间、输入房间号或创建自己的房间。",small);}
    }
    void DrawNetworkJoin()
    {
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();NetworkField("你的昵称",ref networkName);GUILayout.EndVertical();
        GUILayout.Space(20);GUILayout.BeginVertical();NetworkField("房间密码  ·  无密码时留空",ref networkPassword,true);GUILayout.EndVertical();
        GUILayout.EndHorizontal();GUILayout.Space(4);
        GUILayout.Label("通过房间号赴约",label);GUILayout.Space(10);
        GUILayout.BeginHorizontal();networkRoomId=GUILayout.TextField(networkRoomId,PastoralMenuUI.Field,GUILayout.Height(39));
        bool enabled=GUI.enabled;GUI.enabled=enabled&&CanUseMenuEndpoint&&!Network.Busy;
        if(GUILayout.Button("加入房间",button,GUILayout.Width(171),GUILayout.Height(39)))Network.Join(networkRoomId,networkName,networkPassword);
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
            if(PastoralMenuUI.Button(new Rect(r.xMax-122,r.y+20,104,37),"加入",!Network.Busy))Network.Join(room.RoomId,networkName,networkPassword);
        }
    }
    void DrawNetworkSeats()
    {
        var room=Network.Room;
        GUILayout.Label(room.Name+"  ·  房间号 "+room.RoomId,label);
        GUILayout.Label("你的席位 "+Network.SeatId+"  ·  "+room.PlayerCount+" 人  ·  "+room.TargetVictoryPoints+" 分",small);
        GUILayout.Space(17);
        foreach(var seat in room.Seats)
        {
            var r=GUILayoutUtility.GetRect(790,66);
            PastoralMenuUI.Round(new Rect(r.x,r.y+1,r.width-5,59),new Color(.91f,.93f,.86f),9);
            PastoralMenuUI.Label(new Rect(r.x+17,r.y+16,410,30),seat.SeatId+"   "+(seat.IsAi?"当前 AI":seat.Occupied?seat.DisplayName:"等待同伴入座")+(room.ManagerSeatId==seat.SeatId?" · 房主":""),18,null,true);
            PastoralMenuUI.Label(new Rect(r.x+420,r.y+21,190,25),seat.Ready?"已准备":seat.Connected?"在线 · 未准备":"等待连接",13,PastoralMenuUI.Muted);
            if(!room.Started&&!seat.Occupied&&!seat.IsAi&&PastoralMenuUI.Button(new Rect(r.xMax-129,r.y+12,110,36),"坐这里",!Network.Busy))Network.SelectSeat(seat.SeatId);
            if(room.ManagerSeatId==Network.SeatId&&seat.Occupied&&!seat.IsAi&&seat.SeatId!=Network.SeatId&&PastoralMenuUI.Button(new Rect(r.xMax-129,r.y+12,110,36),"转交房主",!Network.Busy))Network.Transfer(seat.SeatId);
        }
    }
    void DrawNetworkRecovery()
    {
        GUILayout.Label("连接与原席位恢复",label);GUILayout.Space(10);
        GUILayout.Label(Network.Endpoint??"尚未连接服务",small);GUILayout.Space(15);
        if(networkLan&&!string.IsNullOrEmpty(Network.Fingerprint))
        {GUILayout.Label("供同伴核对的主机证书指纹",small);GUILayout.TextArea(Network.Fingerprint,PastoralMenuUI.Field,GUILayout.Height(60));GUILayout.Space(17);}
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
            if(Network.Room==null)NetworkField("你的昵称",ref networkName);
            NetworkField("房间名称",ref networkRoomName);
            if(Network.Room==null)NetworkField("加入密码  ·  留空即可直接加入",ref networkPassword,true);
            else {GUILayout.Label(Network.Room.HasPassword?"此房间需要加入密码。":"此房间可直接加入。",small);GUILayout.Space(15);}
            networkListed=GUILayout.Toggle(networkListed,"在大厅中展示这张桌子",button,GUILayout.Height(32));
        }
        else if(networkSettingsPage==1)
        {
            GUILayout.Label("选择这一桌的玩法",label);GUILayout.Space(13);
            int selected=GUILayout.Toolbar(menuRule,RuleNames,button,GUILayout.Height(48));
            if(selected!=menuRule){menuRule=selected;menuScenario=0;menuVictoryPoints=null;}
            GUILayout.Space(28);GUILayout.Label("本局地图",small);
            var ids=ScenarioIds();menuScenario=Mathf.Clamp(menuScenario,0,ids.Length-1);
            GUILayout.BeginHorizontal();
            if(menuRule==1||menuRule==3)
            {
                if(GUILayout.Button("‹",button,GUILayout.Width(48),GUILayout.Height(42))){menuScenario=(menuScenario+ids.Length-1)%ids.Length;menuVictoryPoints=null;}
                GUILayout.Label(ScenarioName(ids[menuScenario]),PastoralMenuUI.Center,GUILayout.Height(42));
                if(GUILayout.Button("›",button,GUILayout.Width(48),GUILayout.Height(42))){menuScenario=(menuScenario+1)%ids.Length;menuVictoryPoints=null;}
            }
            else GUILayout.Label(menuRule==0?"标准岛屿":"标准城市与骑士地图",label,GUILayout.Height(42));
            GUILayout.EndHorizontal();GUILayout.Space(25);GUILayout.Label(RuleDescriptions[menuRule],small);
        }
        else
        {
            GUILayout.Label("同桌人数",small);
            menuPlayers=GUILayout.Toolbar(menuPlayers-3,new[]{"3 人同桌","4 人同桌"},button,GUILayout.Height(42))+3;
            GUILayout.Space(20);GUILayout.Label("由当前 AI 控制的席位  ·  留出至少一席给你",small);
            GUILayout.BeginHorizontal();for(int i=0;i<menuPlayers;i++)networkAi[i]=GUILayout.Toggle(networkAi[i],"P"+(i+1),button,GUILayout.Height(39));GUILayout.EndHorizontal();
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
