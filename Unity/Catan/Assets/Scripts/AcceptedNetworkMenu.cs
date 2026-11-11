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
        else if(networkTab==3||networkTab==4||networkTab==2)RecoveryPage(networkTab==3);
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
    void LobbyPage(){
        NetworkPageTitle(networkLan?"局域网联机":"在线游戏");
        bool online=networkLan||OnlineLobbyState==M7LobbyConnectionState.Online;
        if(!networkLan){
            Caption(new Rect(276,212,185,54),"房间代码",25);
            var input=new Rect(472,212,399,54);
            GUI.SetNextControlName("lobby-code");
            networkRoomId=Edit(input,networkRoomId);
            if(string.IsNullOrEmpty(networkRoomId)&&GUI.GetNameOfFocusedControl()!="lobby-code")
                Caption(input,"输入房间代码",19);
            if(KeyButton(new Rect(900,212,190,54),"加入",true,online&&!Network.Busy&&!string.IsNullOrWhiteSpace(networkRoomId))){
                int navigation=networkNavigation;
                Network.DescribeRoom(networkRoomId,r=>{if(navigation==networkNavigation&&networkMenu&&!networkLan)RequestJoinRoom(r,null,Network.HasRoomIdentity(r.RoomId));});
            }
        }
        Rect create=networkLan?new Rect(619,745,396,73):new Rect(1120,212,250,54);
        if(KeyButton(create,networkLan?"创建本机房间":"创建房间",true,online&&!Network.Busy)){
            networkTab=1;networkScroll=Vector2.zero;GUI.FocusControl(null);
        }
        if(networkLan&&MenuNetworkRoom!=null){
            var current=new Rect(250,774,205,44);
            Caption(current,"‹ 回到我的房间",19);
            if(Hit(current)){networkTab=2;lanRoomPage=0;}
        }
        const float left=233,width=1168;
        if(!networkLan){
            Surface(new Rect(left,291,width,115),"card");
            LobbyHeading(294,"rooms","我的对局");RuleLine(left+3,344,width-6);
            var current=MenuNetworkRoom;
            Caption(new Rect(left+52,346,735,55),current==null?"暂无进行中的对局":current.Name,22);
            if(current!=null&&KeyButton(new Rect(1139,351,221,45),"回到房间",true)){networkTab=2;lanRoomPage=0;}
        }
        int top=networkLan?227:428;
        LobbyHeading(top,networkLan?"lan":"public",networkLan?"本地房间列表":"公开房间");
        var refresh=new Rect(1205,top+4,192,39);
        Caption(refresh,networkLan?"重新搜索":"刷新房间",18);
        if(!Network.Busy&&!networkWorking&&Hit(refresh)){
            if(networkLan)DiscoverLan();else RefreshOnlineLobby();
        }
        int head=top+54;
        if(networkLan)RefreshLanRows();
        int count=networkLan?lanRows.Count:Network.Rooms.Length;
        var view=new Rect(left,head+51,width,Mathf.Clamp(count*64,192,networkLan?367:244));
        Surface(new Rect(left,head,width,51),"card");
        LobbyColumns(head,0,true,null,null);
        float height=Mathf.Max(view.height,count*64);
        networkScroll=GUI.BeginScrollView(view,networkScroll,new Rect(0,0,width-20,height));
        int row=0;
        if(networkLan){
            foreach(var entry in lanRows)
                LobbyRow(entry.room,entry.host,entry.host.RememberedRooms.Contains(entry.room.RoomId),row++);
        }else if(online){
            foreach(var r in Network.Rooms)LobbyRow(r,null,Network.HasRoomIdentity(r.RoomId),row++);
        }
        if(row==0)Caption(new Rect(12,(view.height-68)/2,width-24,68),networkWorking||Network.Busy?(networkLan?"正在搜索房间…":"正在连接…"):online?"暂无开放房间":"大厅未连接",22);
        GUI.EndScrollView();ScrollMenu(view,ref networkScroll,height);
        PastoralMenuUI.Fill(new Rect(left,view.y,1,view.height),LineGold);
        PastoralMenuUI.Fill(new Rect(left+width-1,view.y,1,view.height),LineGold);
        RuleLine(left,view.yMax,width);
        var recovery=new Rect(1190,774,200,44);
        Caption(recovery,"连接与恢复  ›",18);if(Hit(recovery))networkTab=4;
    }
    void LobbyHeading(int y,string icon,string title){
        Illustration(new Rect(248,y+8,33,33),icon);
        var style=Type(25);var old=style.alignment;style.alignment=TextAnchor.MiddleLeft;
        GUI.Label(new Rect(295,y,480,49),title,style);style.alignment=old;
    }
    void LobbyColumns(float y,float x,bool header,RoomSummary room,string detail){
        if(header)x+=233;
        int size=header?20:22;
        if(!header)Illustration(new Rect(x+25,y+17,33,30),"rooms");
        Caption(new Rect(x+65,y,280,50),header?"房间名":room.Name,size);
        Caption(new Rect(x+353,y,276,50),header?(networkLan?"房主":"地图 / 扩展"):detail,size);
        Caption(new Rect(x+647,y,110,50),header?"玩家":room.OccupiedSeats+"/"+room.PlayerCount,size);
        if(!networkLan)Caption(new Rect(x+766,y,167,50),header?"状态":room.Started?"对局中":room.HasPassword?"密码房间":"等待加入",size);
        if(header&&networkLan)Caption(new Rect(x+945,y,180,50),"操作",size);
    }
    void LobbyRow(RoomSummary room,M7LanService.Advertisement server,bool remembered,int row){
        int y=row*64;
        RuleLine(2,y+63,1144);
        var manager=room.Seats?.FirstOrDefault(seat=>seat.SeatId==room.ManagerSeatId);
        LobbyColumns(y+5,0,false,room,networkLan?manager?.DisplayName??"—":MapTitle(room.ScenarioId));
        bool joined=JoinedRoom(room,server);
        if(KeyButton(new Rect(945,y+4,181,50),joined?"回到房间":remembered&&room.Started?"回到原席":"入座",true,!Network.Busy&&!networkWorking&&(joined||remembered||!room.Started))){
            if(joined){networkTab=2;lanRoomPage=0;}else RequestJoinRoom(room,server,remembered);
        }
    }
    void SeatTable(RoomSummary room){
        Sheet(new Rect(75,95,1522,803));BackControl();
        RuleLine(101,183,1463);
        GUI.DrawTexture(new Rect(94,194,1481,578),Art("walnut-table"));
        RuleLine(104,778,1458);
        var map=MapTitle(room.ScenarioId);
        Illustration(new Rect(470,222,721,478),"map-"+map);
        Caption(new Rect(485,115,612,60),map,39);
        if(KeyButton(new Rect(1112,118,279,55),"房间号 "+room.RoomId))GUIUtility.systemCopyBuffer=room.RoomId;
        Caption(new Rect(1412,120,130,49),room.Seats.Count(s=>s.Occupied||s.IsAi)+"/"+room.PlayerCount);
        bool manager=room.ManagerSeatId==Network.SeatId,online=networkLan||OnlineRoomActionsAvailable;
        for(int i=0;i<4;i++){
            float x=i%2==0?204:1218,y=i<2?263:474;
            var s=room.Seats.FirstOrDefault(a=>a.SeatId=="P"+(i+1));
            Surface(new Rect(x-3,y,251,156),"card");
            Illustration(new Rect(x+(i%2==0?180:-12),y+124,72,66),"house-"+i);
            Illustration(new Rect(x+16,y+18,35,38),"person");
            Caption(new Rect(x+52,y+7,189,72),s==null?"未开放":s.IsAi||s.Occupied?s.DisplayName+(s.SeatId==Network.SeatId?"（你）":""):"空席",25);
            if(s==null)continue;
            var action=new Rect(x+13,y+81,217,48);
            if(!s.Occupied&&!s.IsAi&&!room.Started){if(KeyButton(action,"坐这里",false,online&&!Network.Busy))Network.SelectSeat(s.SeatId);}
            else{
                string state=s.RejoinPending?"下回合接回":s.ControlledByAi||s.IsAi?"AI 托管":s.SeatId==room.ManagerSeatId?"房主":s.Ready?"已准备":s.Connected?"未准备":"离线";
                Surface(action,s.Ready||s.SeatId==room.ManagerSeatId?"primary":"card");
                Caption(action,state,s.Ready||s.SeatId==room.ManagerSeatId?-22:22);
                if(manager&&!s.IsAi&&s.Occupied&&s.SeatId!=Network.SeatId&&Hit(new Rect(x+7,y+7,230,65)))transferSeat=s.SeatId;
            }
        }
        var own=room.Seats.FirstOrDefault(s=>s.SeatId==Network.SeatId);
        bool ready=room.Seats.Length==room.PlayerCount&&room.Seats.All(s=>s.IsAi||s.Occupied&&s.Connected&&s.Ready);
        if(room.Started){
            if(KeyButton(new Rect(369,795,401,68),"返回联机对局",true,Ready&&NetworkGame)){networkMenu=false;ResumeGame();}
        }else if(manager&&own!=null&&own.Ready){
            if(KeyButton(new Rect(369,795,401,68),ready?"开始游戏":"等待同伴准备",true,ready&&online&&!Network.Busy))Network.StartRoom();
        }else if(own!=null&&KeyButton(new Rect(369,795,401,68),own.Ready?"取消准备":"准备",true,online&&!Network.Busy))Network.Ready(!own.Ready);
        if(manager&&!room.Started&&KeyButton(new Rect(790,797,217,64),"房间设置")){networkEditorConfiguration=null;lanRoomPage=1;}
        if(KeyButton(new Rect(1022,797,211,64),"连接与恢复"))lanRoomPage=2;
        if(KeyButton(new Rect(1248,797,211,64),"退出房间",false,!Network.Busy))Network.Leave();
    }
    void RecoveryPage(bool editing){
        if(editing)NetworkPageTitle("连接设置");
        else{
            NetworkPageTitle("连接与恢复");
            Surface(new Rect(181,206,1311,160),"card");
            Illustration(new Rect(227,214,155,140),"lamp");
            Caption(new Rect(406,228,340,52),"服务器连接",30);
            Caption(new Rect(521,372,630,65),"我的对局",32);
            Surface(new Rect(192,438,1288,224),"card");
        }
        if(editing){
            Caption(new Rect(249,345,266,69),"服务器地址");
            networkOnlineDraft=Edit(new Rect(544,345,785,69),networkOnlineDraft);
            if(KeyButton(new Rect(590,740,492,70),"保存并连接大厅",true,!string.IsNullOrWhiteSpace(networkOnlineDraft)))SaveOnlineServerAddress();
            return;
        }
        
        Caption(new Rect(411,284,339,58),networkLan?(Network.Room!=null?"已连接":"局域网"):OnlineLobbyState==M7LobbyConnectionState.Online?"已连接":"未连接");
        Caption(new Rect(738,241,410,76),networkLan?Network.Endpoint:menuOnlineAddress,20);
        if(!networkLan&&KeyButton(new Rect(1174,248,229,62),"连接设置")){networkOnlineDraft=menuOnlineAddress;networkTab=3;}
        var room=MenuNetworkRoom;
        if(room!=null){
            Picture(new Rect(239,425,402,247),"map-"+MapTitle(room.ScenarioId));
            Caption(new Rect(674,426,660,64),room.Name+" · "+MapTitle(room.ScenarioId),30);
            Caption(new Rect(674,513,660,61),NetworkPlayerName+" · "+ColorLabel(Network.SeatId)+"席位");
            var own=room.Seats.FirstOrDefault(s=>s.SeatId==Network.SeatId);
            if(own!=null&&(own.ControlledByAi||own.RejoinPending))Caption(new Rect(674,578,660,65),"AI 托管 · 下次自己的回合接手",21);
        }else Caption(new Rect(323,437,1026,64),string.IsNullOrEmpty(Network.RoomId)?"暂无可恢复的席位":"房间号 "+Network.RoomId);
        if(Network.EnrollmentPending){
            if(KeyButton(new Rect(353,660,470,49),"恢复上次申请"))Network.RetryEnrollment();
            if(KeyButton(new Rect(839,660,470,49),"放弃待确认申请"))Network.AbandonEnrollment();
        }
        if(Network.ControlLost&&KeyButton(new Rect(250,723,256,54),"忘记恢复身份"))Network.ForgetIdentity();
        if(!string.IsNullOrEmpty(Network.RoomId)){
            if(KeyButton(new Rect(582,747,507,72),"回到原席",true,!Network.Busy))Network.Reconnect();
        }else if(KeyButton(new Rect(582,747,507,72),networkLan?"重新搜索":"重试连接",true,!Network.Busy)){
            networkTab=0;if(networkLan)DiscoverLan();else RefreshOnlineLobby();
        }
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
