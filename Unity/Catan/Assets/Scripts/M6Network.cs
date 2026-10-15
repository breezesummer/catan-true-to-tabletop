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
    void DrawNetworkMenu()
    {
        SyncNetworkEditor();
        Panel(new Rect(0,0,1440,900),new Color(.025f,.07f,.085f));
        GUILayout.BeginArea(new Rect(220,28,1000,850));
        GUILayout.Label("卡坦岛之三国争霸 · M7 本机联机验证版",title);
        GUILayout.Label("基础版、航海家、城市与骑士及组合共用权威房间；掉线等待，服务器保留存档。",small);
        networkScroll=GUILayout.BeginScrollView(networkScroll);
        if(Network.Room==null)
        {
            GUILayout.BeginHorizontal();bool selected=GUILayout.Toolbar(networkLan?0:1,new[]{"局域网","在线大厅"},button,GUILayout.Height(36))==0;if(selected!=networkLan){networkLan=selected;networkPairConfirmed=false;}
            if(networkLan&&GUILayout.Button("启动本机服务",button,GUILayout.Height(36)))StartLan();
            if(networkLan&&GUILayout.Button("发现局域网",button,GUILayout.Height(36)))DiscoverLan();GUILayout.EndHorizontal();
            GUILayout.Label(networkLan?"服务地址（手动输入房主 IP）":"在线服务 HTTPS 地址",small);
            networkAddress=GUILayout.TextField(networkAddress,button,GUILayout.Height(34));
            if(networkLan)
            {
                GUILayout.Label("SHA-256 证书指纹：首次连接须与房主线下核对；发现报文不能代替核对。",small);
                string pin=GUILayout.TextField(networkPin,button,GUILayout.Height(34));if(pin!=networkPin){networkPin=pin;networkPairConfirmed=false;}
                networkPairConfirmed=GUILayout.Toggle(networkPairConfirmed,"我已核对上述指纹，固定此地址的证书",button,GUILayout.Height(32));
                foreach(var found in networkFound)
                    if(GUILayout.Button(found.serverName+" · "+found.Address,button,GUILayout.Height(32))){networkAddress=found.Address;networkPin=found.certificateFingerprint;networkPairConfirmed=false;}
            }
            GUILayout.BeginHorizontal();if(GUILayout.Button("连接 / 刷新大厅",button,GUILayout.Height(38)))ConnectNetworkEndpoint();
            if(!string.IsNullOrEmpty(Network.RoomId)&&GUILayout.Button("恢复原席位",button,GUILayout.Height(38)))Network.Reconnect();GUILayout.EndHorizontal();
            if(Network.EnrollmentPending&&GUILayout.Button("恢复上次未确认的创建 / 加入请求",button,GUILayout.Height(38)))Network.RetryEnrollment();
            if(Network.EnrollmentPending&&GUILayout.Button("放弃待确认申请（可能失去已经分配的席位）",button,GUILayout.Height(34)))Network.AbandonEnrollment();
            GUILayout.BeginHorizontal();GUILayout.Label("昵称",small,GUILayout.Width(60));networkName=GUILayout.TextField(networkName,button,GUILayout.Width(200));GUILayout.Label("房间密码（可空）",small,GUILayout.Width(180));networkPassword=GUILayout.PasswordField(networkPassword,'●',button);GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();networkRoomId=GUILayout.TextField(networkRoomId,button);if(GUILayout.Button("按房间号加入",button,GUILayout.Width(220),GUILayout.Height(34)))Network.Join(networkRoomId,networkName,networkPassword);GUILayout.EndHorizontal();
            foreach(var room in Network.Rooms)
            {
                GUILayout.BeginHorizontal();GUILayout.Label(room.Name+"  ["+room.RoomId+"]  "+room.RuleSet+" / "+room.ScenarioId+"  "+room.OccupiedSeats+"/"+room.PlayerCount+(room.HasPassword?" · 密码房":"")+(room.Started?" · 对局中":" · 准备中"),small);
                if(GUILayout.Button("加入",button,GUILayout.Width(100)))Network.Join(room.RoomId,networkName,networkPassword);GUILayout.EndHorizontal();
            }
            GUILayout.Space(12);GUILayout.Label("创建房间",label);networkRoomName=GUILayout.TextField(networkRoomName,button,GUILayout.Height(32));DrawNetworkSettings();
            if(GUILayout.Button("创建并加入房间",button,GUILayout.Height(40)))Network.Create(NetworkSettings(),networkName,networkPassword);
        }
        else
        {
            var room=Network.Room;
            GUILayout.Label(room.Name+"  · 房间号 "+room.RoomId,label);GUILayout.Label("地址："+Network.Endpoint+"  · "+room.RuleSet+" / "+room.ScenarioId+"  · 当前席位 "+Network.SeatId,small);
            if(networkLan&&!string.IsNullOrEmpty(Network.Fingerprint))GUILayout.Label("供其他玩家核对的证书指纹：\n"+Network.Fingerprint,small);
            foreach(var seat in room.Seats)
            {
                GUILayout.BeginHorizontal();GUILayout.Label(seat.SeatId+" · "+(seat.IsAi?"当前 AI":seat.Occupied?seat.DisplayName:"空位")+(seat.Ready?" · 已准备":" · 未准备")+(seat.Connected?" · 在线":"")+(room.ManagerSeatId==seat.SeatId?" · 房主":""),label,GUILayout.Height(36));
                if(!room.Started&&!seat.Occupied&&!seat.IsAi&&GUILayout.Button("坐这里",button,GUILayout.Width(120)))Network.SelectSeat(seat.SeatId);
                if(room.ManagerSeatId==Network.SeatId&&seat.Occupied&&!seat.IsAi&&seat.SeatId!=Network.SeatId&&GUILayout.Button("转交房主",button,GUILayout.Width(120)))Network.Transfer(seat.SeatId);GUILayout.EndHorizontal();
            }
            if(!room.Started)
            {
                GUILayout.BeginHorizontal();var own=room.Seats.FirstOrDefault(s=>s.SeatId==Network.SeatId);if(GUILayout.Button(own!=null&&own.Ready?"取消准备":"准备",button,GUILayout.Height(42)))Network.Ready(own==null||!own.Ready);
                if(room.ManagerSeatId==Network.SeatId&&GUILayout.Button("所有人准备后开始",button,GUILayout.Height(42)))Network.StartRoom();GUILayout.EndHorizontal();
                if(room.ManagerSeatId==Network.SeatId){GUILayout.Label("房主设置：更改后所有人需要重新准备",small);DrawNetworkSettings();if(GUILayout.Button("应用配置与 AI 席位",button,GUILayout.Height(38)))Network.ConfigureRoom(NetworkSettings());}
            }
            else if(NetworkGame&&Ready&&GUILayout.Button("返回对局",button,GUILayout.Height(44))){networkMenu=false;ResumeGame();}
            GUILayout.BeginHorizontal();if(GUILayout.Button("重新连接原席位",button,GUILayout.Height(38)))Network.Reconnect();if(GUILayout.Button("退出房间",button,GUILayout.Height(38)))Network.Leave();GUILayout.EndHorizontal();
        }
        GUILayout.Label(networkMessage,small);GUILayout.Label(Network.TransportMode+" · "+Network.Status,small);GUILayout.EndScrollView();
        if(Network.ControlLost&&GUILayout.Button("忘记本设备的恢复身份",button,GUILayout.Height(36)))Network.ForgetIdentity();
        if(GUILayout.Button(NetworkGame?"返回联机对局":"返回单人菜单",button,GUILayout.Height(40))){if(NetworkGame&&Ready){networkMenu=false;ResumeGame();}else networkMenu=false;}
        GUILayout.EndArea();
    }
    void DrawNetworkSettings()
    {
        int selected=GUILayout.Toolbar(menuRule,RuleNames,button,GUILayout.Height(34));if(selected!=menuRule){menuRule=selected;menuScenario=0;menuVictoryPoints=null;}
        menuPlayers=GUILayout.Toolbar(menuPlayers-3,new[]{"3 席","4 席"},button,GUILayout.Height(32))+3;
        var ids=ScenarioIds();menuScenario=Mathf.Clamp(menuScenario,0,ids.Length-1);
        if(menuRule==1||menuRule==3){GUILayout.BeginHorizontal();if(GUILayout.Button("‹",button,GUILayout.Width(60)))menuScenario=(menuScenario+ids.Length-1)%ids.Length;GUILayout.Label(ScenarioName(ids[menuScenario]),label);if(GUILayout.Button("›",button,GUILayout.Width(60)))menuScenario=(menuScenario+1)%ids.Length;GUILayout.EndHorizontal();}
        int target=menuVictoryPoints??DefaultVictoryPoints(RuleIds[menuRule],ids[menuScenario],menuPlayers);
        GUILayout.BeginHorizontal();GUILayout.Label("胜利分数："+target,label);if(GUILayout.Button("−",button,GUILayout.Width(55)))menuVictoryPoints=Math.Max(5,target-1);if(GUILayout.Button("+",button,GUILayout.Width(55)))menuVictoryPoints=Math.Min(MaximumVictoryPoints(RuleIds[menuRule],ids[menuScenario]),target+1);networkListed=GUILayout.Toggle(networkListed,"列入大厅",button);GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();GUILayout.Label("当前 AI 席位",small,GUILayout.Width(150));for(int i=0;i<menuPlayers;i++)networkAi[i]=GUILayout.Toggle(networkAi[i],"P"+(i+1),button);GUILayout.EndHorizontal();
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
