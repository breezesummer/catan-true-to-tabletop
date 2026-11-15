using System;
using System.Linq;
using UnityEngine;
using Catan.Network.Contracts;

public sealed partial class M6App
{
    void AcceptedNetwork(){
        var room=MenuNetworkRoom;
        if(room?.RoomId!=networkPresentedRoom){
            networkPresentedRoom=room?.RoomId;networkScroll=Vector2.zero;
            if(room!=null){networkTab=2;lanRoomPage=0;}else if(networkTab==2)networkTab=0;
            atlasOpen=false;seatOptionsOpen=false;transferSeat=null;
        }
        if(!networkLan&&pendingJoinRoom!=null&&OnlineLobbyState==M7LobbyConnectionState.Offline)CancelJoinPassword();
        if(networkTab==2&&room!=null&&lanRoomPage==1&&(room.Started||room.ManagerSeatId!=Network.SeatId))lanRoomPage=0;
        if(networkTab==1)AcceptedSetup(true,false);
        else if(networkTab==2&&room!=null&&lanRoomPage==1){SyncNetworkEditor();AcceptedSetup(true,true);}
        else if(networkTab==2&&room!=null&&lanRoomPage==0)SeatTable(room);
        else if(networkTab==3&&!networkLan)ConnectionSettingsPage();
        else LobbyPage();
        if(!string.IsNullOrEmpty(Network.LastError)||!string.IsNullOrEmpty(networkMessage)){
            var r=new Rect(335,861,1002,39);
            Surface(r,"control");Caption(r,DisplayPlayerText(string.IsNullOrEmpty(Network.LastError)?networkMessage:Network.LastError),17);
        }
    }
    void NetworkPageTitle(string title){
        Sheet(panelBounds);BackControl();TitleBand(title);
    }
    M7LanService.Advertisement[] displayedHosts;
    readonly System.Collections.Generic.List<(RoomSummary room,M7LanService.Advertisement host)> lanRows=
        new System.Collections.Generic.List<(RoomSummary,M7LanService.Advertisement)>();
    void RefreshLanRows(){
        if(displayedHosts==networkFound)return;
        displayedHosts=networkFound;lanRows.Clear();
        var seen=new System.Collections.Generic.HashSet<(string,string)>();
        foreach(var host in networkFound)
            foreach(var room in host.Rooms)
                if(seen.Add((host.certificateFingerprint,room.RoomId)))lanRows.Add((room,host));
    }
    enum LobbyFinish { Jade, Wood, Red }
    GUIStyle lobbyJade,lobbyPrint,lobbyCard,lobbyField;

    M7LobbyConnectionState HeaderConnectionState=>CanUseMenuEndpoint?Network.LobbyConnectionState:M7LobbyConnectionState.Unconfigured;
    string HeaderConnectionLabel(){
        var state=HeaderConnectionState;
        if(state==M7LobbyConnectionState.Online)return "已连接";
        if(networkLan&&networkWorking)return "搜索中";
        return state==M7LobbyConnectionState.Connecting?"连接中":"未连接";
    }
    void ConnectionBadge(Rect r){
        string label=HeaderConnectionLabel();
        Color dot=label=="已连接"?new Color32(56,132,62,255):
            label=="未连接"?new Color32(143,130,106,255):new Color32(177,127,47,255);
        PastoralMenuUI.Round(new Rect(r.x,r.center.y-8,16,16),dot,8);
        Caption(new Rect(r.x+24,r.y,r.width-24,r.height),label,21,TextAnchor.MiddleLeft);
    }
    void OpenOnlineConnectionSettings(){
        if(networkLan)return;
        networkOnlineDraft=menuOnlineAddress;networkTab=3;
    }
    void ReturnToCurrentRoom(){
        networkTab=2;lanRoomPage=0;
        if(!Network.Connected&&!Network.Busy&&Network.EndpointConfigured&&!string.IsNullOrEmpty(Network.RoomId))Network.Reconnect();
    }

    void LobbyText(Rect r,string label,int size,TextAnchor align,Color ink,bool gilded=false,bool strengthen=false){
        if(Event.current.type!=EventType.Repaint)return;
        if(lobbyPrint==null)lobbyPrint=new GUIStyle(Type(size)){fontStyle=FontStyle.Normal,richText=false};
        lobbyPrint.fontSize=size;lobbyPrint.alignment=align;
        lobbyPrint.fontStyle=FontStyle.Normal;
        TabletopTextFit.Fit(lobbyPrint,label,r,18);
        if(gilded){
            var shadow=r;shadow.y+=1f;
            lobbyPrint.normal.textColor=new Color32(82,45,16,230);
            GUI.Label(shadow,label,lobbyPrint);
        }
        lobbyPrint.normal.textColor=ink;
        var edge=r;edge.x+=strengthen ? 1f : gilded ? .45f : .25f;
        GUI.Label(edge,label,lobbyPrint);
        GUI.Label(r,label,lobbyPrint);
    }
    static void LobbyRule(float x,float y,float width){
        PastoralMenuUI.Fill(new Rect(x,y,width,1),new Color32(151,98,47,140));
    }
    void LobbyCard(Rect r){
        if(Event.current.type!=EventType.Repaint)return;
        if(lobbyCard==null){
            lobbyCard=new GUIStyle{border=new RectOffset(16,16,13,13)};
            lobbyCard.normal.background=Art("setup-lobby-card");
        }
        lobbyCard.Draw(r,GUIContent.none,false,false,false,false);
    }
    void LobbyFieldFace(Rect r){
        if(Event.current.type==EventType.Repaint){
            if(lobbyField==null){
                lobbyField=new GUIStyle{border=new RectOffset(20,20,17,17)};
                lobbyField.normal.background=Art("setup-lobby-field");
            }
            lobbyField.Draw(r,GUIContent.none,false,false,false,false);
        }
    }
    string LobbyCodeInput(Rect r,string value){
        LobbyFieldFace(r);
        using(var pixels=new PastoralMenuUI.PixelText(InsetMenuRect(r,12),22)){
            GUI.SetNextControlName("lobby-code");
            return GUI.TextField(pixels.Area,value,100,Type(pixels.Size));
        }
    }

    bool LobbyButton(Rect r,string label,LobbyFinish finish,int size,bool available=true){
        var tint=GUI.color;
        if(!available)GUI.color=new Color(.73f,.70f,.63f,tint.a*.8f);
        if(finish==LobbyFinish.Red)Picture(InsetMenuRect(r,-10),"setup-load-continue");
        else if(finish==LobbyFinish.Wood)Picture(r,"setup-atlas-wood");
        else if(Event.current.type==EventType.Repaint){
            if(lobbyJade==null){
                lobbyJade=new GUIStyle{border=new RectOffset(26,26,11,11)};
                lobbyJade.normal.background=Art("setup-lobby-jade");
            }
            lobbyJade.Draw(r,GUIContent.none,false,false,false,false);
        }
        GUI.color=tint;
        var text=new Rect(r.x+21,r.y,r.width-42,r.height-3);
        bool metal=finish!=LobbyFinish.Wood;
        Color ink=metal?(available?new Color32(255,223,147,255):new Color32(233,217,180,255)):MenuInk;
        LobbyText(text,label,size,TextAnchor.MiddleCenter,ink,metal);
        return available&&Hit(r,false);
    }
    static string LobbyMap(RoomSummary room)=>room.RuleSet=="base"||room.RuleSet=="cities-knights"?"标准岛屿":MapTitle(room.ScenarioId);
    void LobbyMapPreview(Rect r,RoomSummary room){
        string map=LobbyMap(room);
        string id=map=="标准岛屿"?"standard":room.ScenarioId;
        if(id=="pirate-islands"||id=="new-world"||id=="wonders-of-catan")id+="-"+(room.PlayerCount==3?3:4);
        var key="lobby-map-"+id;
        Illustration(r,key);
    }
    void JoinLobbyCode(){
        int navigation=networkNavigation;
        Network.DescribeRoom(networkRoomId,r=>{
            if(navigation==networkNavigation&&networkMenu&&!networkLan)
                RequestJoinRoom(r,null,Network.HasRoomIdentity(r.RoomId));
        });
    }
    void LobbyPage(){
        bool lan=networkLan,online=lan||OnlineLobbyState==M7LobbyConnectionState.Online;
        Sheet(panelBounds);BackControl();
        string title=lan?"局域网联机":"在线游戏";
        float center=panelBounds.center.x,gap=lan?145:119;
        float right=lan?1071:1063;
        LobbyRule(center-gap-128,156,128);LobbyRule(right+5,156,110);
        Illustration(new Rect(center-gap-5,151,10,10),"diamond");
        Illustration(new Rect(right-5,151,10,10),"diamond");
        LobbyText(new Rect(center-190,121,380,65),title,40,TextAnchor.MiddleCenter,MenuInk);
        ConnectionBadge(new Rect(lan?946:934,132,118,44));
        var current=MenuNetworkRoom;
        if(!lan){
            Caption(new Rect(202,200,184,61),"房间代码",29);
            var input=new Rect(398,204,378,59);
            networkRoomId=LobbyCodeInput(input,networkRoomId);
            if(string.IsNullOrEmpty(networkRoomId)&&GUI.GetNameOfFocusedControl()!="lobby-code")
                Caption(new Rect(425,205,330,57),"输入房间代码",23,TextAnchor.MiddleLeft);
            if(LobbyButton(new Rect(796,205,187,57),"加入",LobbyFinish.Jade,28,online&&!Network.Busy&&!string.IsNullOrWhiteSpace(networkRoomId))){
                JoinLobbyCode();
            }
            LobbyRule(172,289,1306);
            LobbyText(new Rect(183,292,400,50),"我的对局",32,TextAnchor.MiddleLeft,MenuInk);
            Illustration(new Rect(162,311,13,13),"diamond");
            var row=new Rect(167,349,1308,84);
            LobbyCard(row);
            if(current!=null){
                LobbyMapPreview(new Rect(201,348,245,85),current);
                LobbyText(new Rect(443,355,751,72),current.Name,34,TextAnchor.MiddleLeft,MenuInk);
                if(LobbyButton(new Rect(1235,364,213,54),"回到房间",LobbyFinish.Jade,24))ReturnToCurrentRoom();
            }else Caption(new Rect(206,355,1030,70),"暂无进行中的对局",22,TextAnchor.MiddleLeft);
        }
        Rect create=lan?new Rect(619,744,435,83):new Rect(1084,195,357,79);
        if(LobbyButton(create,lan?"创建本机房间":"创建房间",LobbyFinish.Red,37,online&&!Network.Busy)){
            networkTab=1;networkScroll=Vector2.zero;GUI.FocusControl(null);
        }
        int section=lan?193:454;

        LobbyText(new Rect(186,section,498,56),lan?"本地房间列表":"公开房间",35,TextAnchor.MiddleLeft,MenuInk);
        if(!lan)Illustration(new Rect(162,section+22,13,13),"diamond");
        if(LobbyButton(new Rect(1260,section+3,215,49),lan?"重新搜索":"刷新房间",lan?LobbyFinish.Wood:LobbyFinish.Jade,
            23,!Network.Busy&&!networkWorking)){
            if(lan)DiscoverLan();else RefreshOnlineLobby();
        }
        int header=lan?266:520,rowHeight=lan?136:96;
        const int left=165,width=1310;
        LobbyCard(new Rect(left,header,width,47));
        float[] stops=lan?LanLobbyStops:OnlineLobbyStops;
        var titles=lan?LanLobbyTitles:OnlineLobbyTitles;
        for(int i=0;i<titles.Length;i++){
            Caption(new Rect(left+stops[i],header,stops[i+1]-stops[i],47),titles[i],24);
            if(i>0)PastoralMenuUI.Fill(new Rect(left+stops[i],header+12,1,23),LineGold);
        }
        var view=new Rect(left,header+49,width,rowHeight*(lan?3:2));
        if(lan)RefreshLanRows();
        int count=lan?lanRows.Count:online?Network.Rooms.Length:0;
        float height=Mathf.Max(view.height,count*rowHeight);
        networkScroll.y=Mathf.Clamp(networkScroll.y,0,height-view.height);
        networkScroll=GUI.BeginScrollView(view,networkScroll,new Rect(0,0,width-20,height));
        // 只绘制可见房间及其相邻行，长列表不会逐帧解码所有地图。
        int first=Mathf.Max(0,Mathf.FloorToInt(networkScroll.y/rowHeight)-1);
        int end=Mathf.Min(count,Mathf.CeilToInt((networkScroll.y+view.height)/rowHeight)+1);
        for(int i=first;i<end;i++){
            if(lan){
                var entry=lanRows[i];
                LobbyRow(entry.room,entry.host,entry.host.RememberedRooms.Contains(entry.room.RoomId),i*rowHeight,rowHeight);
            }else{
                var room=Network.Rooms[i];
                LobbyRow(room,null,Network.HasRoomIdentity(room.RoomId),i*rowHeight,rowHeight);
            }
        }
        if(count==0)Caption(new Rect(12,(view.height-68)/2,width-24,68),networkWorking||Network.Busy?(lan?"正在搜索房间…":"正在连接…"):online?"暂无开放房间":"大厅未连接",22,pixelText:false);
        GUI.EndScrollView();ScrollMenu(view,ref networkScroll,height);
        if(count==0)LobbyRule(left,view.yMax,width);
        if(!lan&&LobbyButton(new Rect(1252,782,222,47),"连接设置",LobbyFinish.Wood,22))OpenOnlineConnectionSettings();
    }
    static readonly float[] LanLobbyStops={0,600,837,1041,1310};
    static readonly float[] OnlineLobbyStops={0,253,691,865,1092,1310};
    static readonly string[] LanLobbyTitles={"房间名","房主","玩家","操作"};
    static readonly string[] OnlineLobbyTitles={"房间名","地图","玩家","状态","操作"};
    void LobbyRow(RoomSummary room,M7LanService.Advertisement server,bool remembered,int y,int h){
        bool lan=networkLan;
        var stops=lan?LanLobbyStops:OnlineLobbyStops;
        for(int i=1;i<stops.Length-1;i++)
            PastoralMenuUI.Fill(new Rect(stops[i],y+h*.18f,1,h*.64f),LineGold);
        LobbyRule(0,y+h-1,1290);
        if(lan){
            LobbyMapPreview(new Rect(20,y+1,290,h-2),room);
            LobbyText(new Rect(325,y,261,h),room.Name,36,TextAnchor.MiddleLeft,MenuInk);
            var manager=room.Seats?.FirstOrDefault(seat=>seat.SeatId==room.ManagerSeatId);
            Caption(new Rect(611,y,215,h),manager?.DisplayName??"—",26,pixelText:false);
        }else{
            LobbyText(new Rect(15,y,226,h),room.Name,30,TextAnchor.MiddleLeft,MenuInk);
            LobbyMapPreview(new Rect(264,y+1,218,h-2),room);
            Caption(new Rect(491,y,191,h),LobbyMap(room),23,pixelText:false);
            Caption(new Rect(872,y,213,h),room.Started?"对局中":room.HasPassword?"密码房间":"等待加入",23,pixelText:false);
        }
        Caption(new Rect(stops[2],y,stops[3]-stops[2],h),room.OccupiedSeats+"/"+room.PlayerCount,28,pixelText:false);
        bool joined=JoinedRoom(room,server);
        var button=new Rect(lan?1090:1128,y+(h-55)/2,lan?186:161,55);
        if(LobbyButton(button,joined?"回到房间":remembered&&room.Started?"回到原席":"入座",LobbyFinish.Jade,25,
            !Network.Busy&&!networkWorking&&(joined||remembered||!room.Started))){
            if(joined)ReturnToCurrentRoom();else RequestJoinRoom(room,server,remembered);
        }
    }
    readonly System.Collections.Generic.Dictionary<string,GUIStyle> roomPlates=new System.Collections.Generic.Dictionary<string,GUIStyle>();
    void RoomPlate(Rect r,string asset,int side=18){
        if(Event.current.type!=EventType.Repaint)return;
        if(!roomPlates.TryGetValue(asset,out var style)){
            style=new GUIStyle{border=new RectOffset(side,side,8,8)};
            style.normal.background=Art(asset);roomPlates[asset]=style;
        }
        style.Draw(r,GUIContent.none,false,false,false,false);
    }
    void RoomName(Rect r,string name,bool empty=false,string suffix=""){
        RoomPlate(r,empty?"setup-atlas-wood":"setup-room-name");
        string label=name+suffix;
        var measure=Type(18);
        int length=name.Length;
        while(length>0&&measure.CalcSize(new GUIContent(label)).x>r.width-38){
            length--;
            if(length>0&&char.IsHighSurrogate(name[length-1]))length--;
            label=name.Substring(0,length)+"…"+suffix;
        }
        LobbyText(new Rect(r.x+19,r.y,r.width-38,r.height-3),label,25,TextAnchor.MiddleCenter,
            empty?MenuInk:new Color32(255,225,163,255),!empty);
    }
    void RoomSeatAction(RoomSummary room,SeatSummary seat){
        if(seat==null||Network.Busy||!(networkLan||OnlineRoomActionsAvailable))return;
        if(!room.Started&&!seat.Occupied&&!seat.IsAi)Network.SelectSeat(seat.SeatId);
        else if(!room.Started&&seat.SeatId==Network.SeatId)Network.Ready(!seat.Ready);
        else if(room.ManagerSeatId==Network.SeatId&&seat.Occupied&&!seat.IsAi&&seat.SeatId!=Network.SeatId)
            transferSeat=seat.SeatId;
    }
    void RoomSeat(RoomSummary room,int index,float center){
        var seat=room.Seats.FirstOrDefault(s=>s.SeatId=="P"+(index+1));
        bool own=seat!=null&&seat.SeatId==Network.SeatId;
        bool empty=seat!=null&&!seat.Occupied&&!seat.IsAi;
        var tint=GUI.color;
        if(seat==null)GUI.color=new Color(.82f,.79f,.72f,tint.a*.65f);
        var baseRect=new Rect(center-56,633,112,36);
        if(own)Picture(new Rect(baseRect.x-20,baseRect.y-20,152,76),"setup-room-seat-glow");
        Picture(baseRect,own?"setup-seat-active":"setup-seat");
        // 屋脚固定在底座前沿；空席使用本白木屋，已占用席保留真实玩家颜色。
        var piece=Art("setup-seated-house-"+(empty?3:index));
        if(piece!=null){
            float ratio=(float)piece.width/piece.height*Mathf.Abs(GUI.matrix.m11/GUI.matrix.m00);
            float height=Mathf.Min(85,97/ratio),width=height*ratio;
            GUI.DrawTexture(new Rect(center-width/2,baseRect.y+24-height,width,height),piece);
        }
        GUI.color=tint;
        var name=new Rect(center-105,675,210,42);
        RoomName(name,seat==null?"未开放":empty?"入座":DisplayPlayerText(seat.DisplayName),empty||seat==null,own?" · 你":"");
        if(seat!=null&&!empty){
            bool host=seat.SeatId==room.ManagerSeatId;
            string status=seat.RejoinPending?"下回合接回":seat.ControlledByAi?"AI 托管":seat.IsAi?"AI":
                !seat.Connected?"离线":host?(seat.Ready||room.Started?"房主":"房主 · 未准备"):seat.Ready?"已准备":"未准备";
            bool green=seat.Connected&&seat.Ready&&!host&&!seat.ControlledByAi&&!seat.IsAi&&!seat.RejoinPending;
            var state=new Rect(center-82,726,164,38);
            RoomPlate(state,host&&seat.Connected&&!seat.ControlledByAi&&!seat.RejoinPending?"setup-room-host":green?"setup-room-ready":"setup-room-plain");
            LobbyText(new Rect(state.x+13,state.y+2,state.width-26,state.height-4),status,20,TextAnchor.MiddleCenter,
                green?new Color32(255,225,163,255):MenuInk,green);
        }
        if(Hit(new Rect(center-111,579,222,186),false))RoomSeatAction(room,seat);
    }
    void RoomSummaryRow(float y,string title,string value){
        FieldLabel(859,y,title);
        LobbyText(new Rect(1095,y,357,46),value,27,TextAnchor.MiddleCenter,MenuInk);
        LobbyRule(859,y+49,608);
    }
    void SeatTable(RoomSummary room){
        Sheet(panelBounds);BackControl();
        LobbyText(new Rect(350,120,540,63),DisplayPlayerText(room.Name),34,TextAnchor.MiddleCenter,MenuInk);
        ConnectionBadge(new Rect(914,130,135,43));
        var identity=new Rect(1121,130,262,43);
        RoomName(identity,networkLan?room.Seats.Count(s=>s.Occupied||s.IsAi)+"/"+room.PlayerCount+" 人":"房间号 "+room.RoomId);
        if(!networkLan&&SmallPlate(new Rect(1398,130,86,43),"setup-atlas-wood","复制",22))GUIUtility.systemCopyBuffer=room.RoomId;

        string map=LobbyMap(room);
        string id=map=="标准岛屿"?"standard":room.ScenarioId;
        if(map=="标准岛屿")Illustration(new Rect(163,186,644,338),"lobby-map-standard");
        else Illustration(new Rect(163,186,644,338),"atlas-"+id+"-"+room.PlayerCount);
        RoomName(new Rect(365,526,240,38),map);
        PastoralMenuUI.Fill(new Rect(830,190,1,365),LineGold);
        bool sailing=room.RuleSet=="seafarers"||room.RuleSet=="combined";
        bool[] modes={true,sailing,room.RuleSet=="cities-knights"||room.RuleSet=="combined",!string.IsNullOrEmpty(room.ThreeKingdomsPackageVersion),room.Harbormaster||room.Fishing};
        for(int i=0;i<ModeIcons.Length;i++){
            float x=851+i*128;
            Illustration(new Rect(x+2,205,122,106),ModeIcons[i]);
            Picture(new Rect(x,317,125,39),modes[i]?"setup-jade":"setup-neutral");
            PlateLettering(new Rect(x+4,317,117,35),ModeTitles[i],17,modes[i]?PlateInk.Jade:PlateInk.Dark);
        }
        int target=room.TargetVictoryPoints??DefaultVictoryPoints(room.RuleSet,room.ScenarioId,room.PlayerCount)+(room.Harbormaster?1:0);
        RoomSummaryRow(387,"人数",room.PlayerCount+" 人");
        RoomSummaryRow(443,"胜利分数",target.ToString());
        // 协议没有房间 AI 节奏字段；不把本地单人节奏误显示为服务端设置。
        RoomSummaryRow(499,"房间状态",room.Finished?"已结束":room.Started?"对局中":"等待开局");
        for(int i=0;i<4;i++)RoomSeat(room,i,393+i*299);

        bool manager=room.ManagerSeatId==Network.SeatId,online=networkLan||OnlineRoomActionsAvailable;
        var own=room.Seats.FirstOrDefault(s=>s.SeatId==Network.SeatId);
        bool ready=room.Seats.Length==room.PlayerCount&&room.Seats.All(s=>s.IsAi||s.Occupied&&s.Connected&&s.Ready);
        var primary=new Rect(584,770,504,76);
        if(room.Started){
            if(SetupPlaque(primary,"返回联机对局",true,Ready&&NetworkGame)){networkMenu=false;ResumeGame();}
        }else if(manager&&own!=null&&own.Ready){
            if(SetupPlaque(primary,"开始游戏",true,ready&&online&&!Network.Busy))Network.StartRoom();
        }else if(own!=null&&SetupPlaque(primary,own.Ready?"取消准备":"准备",true,online&&!Network.Busy))Network.Ready(!own.Ready);
        if(manager&&!room.Started&&LobbyButton(new Rect(309,785,200,48),"房间设置",LobbyFinish.Jade,25,online&&!Network.Busy)){
            networkEditorConfiguration=null;lanRoomPage=1;
        }
        if(SmallPlate(new Rect(1164,785,185,48),"setup-atlas-wood","退出房间",24)&&!Network.Busy)Network.Leave();
    }
    void ConnectionSettingsPage(){
        Sheet(panelBounds);BackControl();
        const float center=817;
        LobbyRule(center-119-128,156,128);LobbyRule(1068,156,110);
        Illustration(new Rect(center-124,151,10,10),"diamond");
        Illustration(new Rect(1058,151,10,10),"diamond");
        LobbyText(new Rect(center-190,121,380,65),"连接设置",40,TextAnchor.MiddleCenter,MenuInk);
        ConnectionBadge(new Rect(934,132,118,44));
        LobbyText(new Rect(433,306,804,56),"服务器地址",30,TextAnchor.MiddleLeft,MenuInk);
        networkOnlineDraft=ConnectionAddressInput(new Rect(433,375,804,69),networkOnlineDraft);
        if(ConnectionSaveButton(new Rect(575,716,516,96),!Network.Busy&&!string.IsNullOrWhiteSpace(networkOnlineDraft)))SaveOnlineServerAddress();
    }
    GUIStyle connectionAddressText;
    string ConnectionAddressInput(Rect r,string value){
        LobbyFieldFace(r);
        if(connectionAddressText==null)connectionAddressText=new GUIStyle(Type(27)){
            alignment=TextAnchor.MiddleLeft,richText=false,wordWrap=false
        };
        using(var pixels=new PastoralMenuUI.PixelText(new Rect(r.x+30,r.y+5,r.width-60,r.height-10),27)){
            connectionAddressText.fontSize=pixels.Size;
            GUI.SetNextControlName("online-server-address");
            return GUI.TextField(pixels.Area,value,512,connectionAddressText);
        }
    }
    bool ConnectionSaveButton(Rect r,bool available){
        var tint=GUI.color;
        if(!available)GUI.color=new Color(.78f,.76f,.70f,tint.a);
        Picture(InsetMenuRect(r,-14),"setup-connection-save");
        GUI.color=tint;
        LobbyText(new Rect(r.x+105,r.y,r.width-210,r.height-4),"保存并连接",42,TextAnchor.MiddleCenter,
            available?new Color32(255,223,139,255):new Color32(233,217,180,255),true,true);
        return available&&Hit(r,false);
    }
    void TransferDialog(){
        var room=MenuNetworkRoom;
        var seat=room?.Seats.FirstOrDefault(s=>s.SeatId==transferSeat&&s.Occupied&&!s.IsAi);
        if(seat==null||room.ManagerSeatId!=Network.SeatId){transferSeat=null;return;}
        PastoralMenuUI.Fill(new Rect(0,0,1672,941),new Color(.12f,.085f,.045f,.64f));
        Sheet(new Rect(487,300,699,356));
        Caption(new Rect(545,360,580,75),seat.DisplayName,33);
        if(KeyButton(new Rect(547,514,267,64),"返回")){transferSeat=null;return;}
        if(KeyButton(new Rect(842,514,294,64),"转交房主",true,!Network.Busy)){
            Network.Transfer(transferSeat);transferSeat=null;
        }
    }
    void PasswordDialog(){
        PastoralMenuUI.Fill(new Rect(0,0,1672,941),new Color(.12f,.085f,.045f,.64f));
        Sheet(new Rect(487,300,699,380));Caption(new Rect(550,327,568,74),pendingJoinRoom.Name,37);
        Caption(new Rect(525,418,200,63),"房间密码");GUI.SetNextControlName("lan-join-password");
        joinPassword=Edit(new Rect(738,419,394,65),joinPassword,true,128);
        if(KeyButton(new Rect(547,532,267,75),"返回")){CancelJoinPassword();return;}
        if(KeyButton(new Rect(842,530,294,77),"加入",true,!Network.Busy&&joinPassword.Length>0))ConfirmJoinPassword();
        if(Event.current.type==EventType.KeyDown&&(Event.current.keyCode==KeyCode.Return||Event.current.keyCode==KeyCode.KeypadEnter)){ConfirmJoinPassword();Event.current.Use();}
    }
}
