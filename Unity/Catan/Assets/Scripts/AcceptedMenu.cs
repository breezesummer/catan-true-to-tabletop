using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public sealed partial class M6App
{
    // 使用验收稿的画布坐标；鼠标和触屏与画面共用 GUI.matrix。
    readonly Dictionary<string,Texture2D> menuArt=new Dictionary<string,Texture2D>();
    readonly Dictionary<int,GUIStyle> menuTypography=new Dictionary<int,GUIStyle>();
    bool atlasOpen,backupExpanded,seatOptionsOpen;
    string transferSeat;
    static readonly Dictionary<string,string> mapTitles=new Dictionary<string,string>{
        {"heading-for-new-shores","驶向新海岸"},{"four-islands","四座岛屿"},{"fog-islands","迷雾岛屿"},
        {"through-the-desert","穿越沙漠"},{"forgotten-tribe","被遗忘的部落"},{"cloth-for-catan","卡坦的布匹"},
        {"pirate-islands","海盗群岛"},{"wonders-of-catan","卡坦奇观"},{"new-world","新世界"}};
    static readonly Rect panelBounds=new Rect(120,108,1394,750);
    static string MapTitle(string id)=>id!=null&&mapTitles.TryGetValue(id,out var title)?title:"标准岛屿";
    Texture2D Art(string key){
        if(!menuArt.TryGetValue(key,out var value)){value=Resources.Load<Texture2D>("AcceptedMenu/"+key);menuArt[key]=value;}
        return value;
    }
    void Picture(Rect r,string key){var t=Art(key);if(t!=null)GUI.DrawTexture(r,t);}
    GUIStyle Type(int size){
        if(menuTypography.TryGetValue(size,out var value))return value;
        value=new GUIStyle(GUI.skin.label){font=pastoralMenuFont,fontSize=Math.Abs(size),fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,clipping=TextClipping.Clip};
        value.normal.textColor=size<0?new Color(1,.95f,.80f):new Color(.21f,.12f,.06f);menuTypography[size]=value;return value;
    }
    void Caption(Rect r,string text,int size=25){GUI.Label(r,text,Type(size));}
    bool Hit(Rect r){
        bool hit=TabletopButtonFeedback.Button(r,false)&&(!MobileTabletop.Enabled||!MobileTabletop.TouchDragged);
        if(hit)TabletopAudio.Instance?.Click();return hit;
    }
    bool KeyButton(Rect r,string text,bool primary=false,bool enabled=true){
        var tint=GUI.color;
        if(!enabled)GUI.color=new Color(.78f,.76f,.70f,tint.a);
        using(var feedback=TabletopButtonFeedback.Begin(r,enabled)){
            bool hover=enabled&&GUI.enabled&&r.Contains(Event.current.mousePosition);
            if(hover)GUI.color=new Color(1,.97f,.88f,tint.a);
            
            Surface(r,primary?"primary":"control");
            if(primary&&r.height>=60&&r.width>=325){
                var mark=new Rect(r.x+20,r.center.y-9,47,18);
                Illustration(mark,"cloud");
                var right=new Rect(r.xMax-67,r.center.y-9,47,18);
                GUI.DrawTextureWithTexCoords(right,Art("cloud"),new Rect(1,0,-1,1));
            }
            Caption(r,text,primary?(r.height>=68?-36:-23):22);
        }
        GUI.color=tint;return enabled&&Hit(r);
    }
    string Edit(Rect r,string value,bool secret=false,int max=100){
        Surface(r,"field");var inner=new Rect(r.x+12,r.y+4,r.width-24,r.height-8);
        var style=Type(22);return secret?GUI.PasswordField(inner,value,'●',max,style):GUI.TextField(inner,value,max,style);
    }
    void MenuBack(){
        GUI.FocusControl(null);
        if(atlasOpen||seatOptionsOpen||transferSeat!=null){atlasOpen=false;seatOptionsOpen=false;transferSeat=null;return;}
        if(networkMenu&&networkTab==2&&lanRoomPage!=0){lanRoomPage=0;return;}
        if(networkMenu&&networkTab!=0){networkTab=0;networkScroll=Vector2.zero;return;}
        networkMenu=false;NavigateMainMenu(-1);
    }
    void AcceptedPage(bool net){
        if(pastoralMenuFont==null)pastoralMenuFont=Resources.Load<Font>("Fonts/NotoSerifCJKsc-Regular");
        PastoralMenuUI.Styles(pastoralMenuFont??font);PastoralMenuScene.Draw(Time.unscaledTime);
        var matrix=GUI.matrix;var enabled=GUI.enabled;
        GUI.matrix=matrix*Matrix4x4.Scale(new Vector3(1440f/1672,900f/941,1));
        GUI.enabled=enabled&&menuPendingAction==null&&pendingJoinRoom==null&&!atlasOpen&&!seatOptionsOpen&&transferSeat==null;
        if(net)AcceptedNetwork();
        else if(menuPage==0)AcceptedSetup(false,false);
        else if(menuPage==1)AcceptedLoad();
        else AcceptedSettings(ref menuSettingsTab,false);
        if(!net&&menuPage!=1&&!string.IsNullOrEmpty(menuInlineMessage))Caption(new Rect(250,860,1170,40),menuInlineMessage);
        GUI.enabled=enabled;
        if(atlasOpen)MapPicker();
        if(seatOptionsOpen)AiPicker();
        if(transferSeat!=null)TransferDialog();
        if(pendingJoinRoom!=null)PasswordDialog();
        if(menuPendingAction!=null)AcceptedConfirmation();
        GUI.matrix=matrix;GUI.enabled=enabled;
    }
    void AcceptedConfirmation(){
        PastoralMenuUI.Fill(new Rect(0,0,1672,941),new Color(.1f,.07f,.03f,.64f));
        Sheet(new Rect(462,254,748,435));Caption(new Rect(500,296,672,69),menuPendingCaption,32);
        Caption(new Rect(497,375,678,63),NetworkGame?"离开当前联机对局？":"当前对局正在进行",24);
        bool canSave=!NetworkGame&&menuPendingAllowSave;
        if(canSave&&KeyButton(new Rect(588,456,493,56),"保存当前局并继续",true)){
            try{WriteSave(MenuLocalSavePath);var action=menuPendingAction;menuPendingAction=null;action();}
            catch(IOException){menuInlineMessage="保存失败，当前对局已保留";menuPendingAction=null;}
            return;
        }
        if(KeyButton(new Rect(508,547,295,62),"留在当前对局")){menuPendingAction=null;return;}
        if(KeyButton(new Rect(865,547,295,62),"直接继续",!canSave)){var action=menuPendingAction;menuPendingAction=null;action();}
    }
    void BackControl(bool room=false){
        var r=new Rect(154,124,146,50);
        r=room?new Rect(154,124,190,50):r;
        Caption(r,room?"‹ 返回房间":"‹ 返回",23);if(Hit(r))MenuBack();
    }
    int lastDefaultRule=-1,lastDefaultCount,cachedVictory;
    string lastDefaultId;
    bool lastDefaultHarbor;
    void SelectRule(int next){
        if(next==menuRule)return;menuRule=next;menuScenario=0;menuVictoryPoints=null;
        if(next<2)menuThreeKingdoms=false;
    }
    void AcceptedSetup(bool net,bool edit){
        var ids=ScenarioIds();menuScenario=Mathf.Clamp(menuScenario,0,ids.Length-1);
        string id=ids[menuScenario];bool sailing=menuRule==1||menuRule==3;
        string map=sailing?MapTitle(id):"标准岛屿";
        bool harbor=Catan.Core.TradeModules.HarbormasterRules.Supports(id);
        bool fish=!sailing||Catan.Core.TradeModules.FishingScenarioCatalog.SupportsSeafaring(id);
        menuHarbormaster &= harbor;menuFishing &= fish;
        SetupFurniture(net);
        BackControl(edit);
        Illustration(new Rect(151,178,678,443),"map-"+map);
        if(sailing&&Hit(new Rect(151,178,678,443)))OpenMapPicker();
        if(KeyButton(net?new Rect(298,675,399,49):new Rect(1091,390,372,45),map+"  ▾",false,sailing))OpenMapPicker();
        bool[] active={true,sailing,menuRule>=2,menuThreeKingdoms,menuHarbormaster||menuFishing};
        for(int i=0;i<5;i++){
            float x=854+i*130;Picture(new Rect(x+45,331,38,37),active[i]?"check-on":"check-off");
            if(!Hit(new Rect(x,170,130,202)))continue;
            if(i==0)SelectRule(0);
            if(i==1)SelectRule(menuRule^1);
            if(i==2)SelectRule(menuRule^2);
            if(i==3){if(menuRule<2)SelectRule(menuRule|2);menuThreeKingdoms=!menuThreeKingdoms;}
            if(i==4){bool on=!active[i];menuHarbormaster=on&&harbor;menuFishing=on&&fish;}
            return;
        }
        if(net){
            networkRoomName=Edit(new Rect(1080,446,385,49),networkRoomName);
            if(!edit)networkPassword=Edit(new Rect(1080,501,385,49),networkPassword,true,128);
            else Caption(new Rect(1080,501,385,49),Network.Room.HasPassword?"已设置密码":"公开加入");
            Picture(new Rect(1096,562,38,38),networkListed?"check-on":"check-off");
            if(Hit(new Rect(1080,559,68,45)))networkListed=!networkListed;
            if(KeyButton(new Rect(1210,556,251,44),"电脑席位"))seatOptionsOpen=true;
        }
        int py=net?616:447;
        for(int i=3;i<=4;i++)
            if(KeyButton(new Rect(1095+(i-3)*184,py,165,45),i+" 人",menuPlayers==i,!edit||Network.Room.Seats.Skip(i).All(s=>!s.Occupied||s.IsAi)))menuPlayers=i;
        menuHuman=Mathf.Clamp(menuHuman,0,menuPlayers-1);
        int max=MaximumVictoryPoints(RuleIds[menuRule],id,menuHarbormaster);
        if(lastDefaultRule!=menuRule||lastDefaultId!=id||lastDefaultCount!=menuPlayers||lastDefaultHarbor!=menuHarbormaster){
            lastDefaultRule=menuRule;lastDefaultId=id;lastDefaultCount=menuPlayers;lastDefaultHarbor=menuHarbormaster;
            cachedVictory=MenuDefaultVictoryPoints(RuleIds[menuRule],id,menuPlayers);
        }
        int points=Mathf.Clamp(menuVictoryPoints??cachedVictory,5,max);
        if(menuVictoryPoints.HasValue)menuVictoryPoints=points;
        int y=net?677:502;
        if(KeyButton(new Rect(1115,y,65,45),"−",false,points>5))menuVictoryPoints=points-1;
        Caption(new Rect(1192,y,144,45),points.ToString(),31);
        if(KeyButton(new Rect(1350,y,65,45),"+",false,points<max))menuVictoryPoints=points+1;
        if(!net){
            if(KeyButton(new Rect(1094,562,365,46),TabletopAiSpeed.Names[menuAiSpeed]+"  ›"))menuAiSpeed=(menuAiSpeed+1)%TabletopAiSpeed.Names.Length;
            for(int i=0;i<4;i++){
                var r=new Rect(426+i*244,644,101,96);
                if(i==menuHuman)Picture(new Rect(r.center.x-82,r.center.y-60,164,120),"ring");
                Illustration(r,"house-"+i);
                if(i<menuPlayers&&Hit(r))menuHuman=i;
            }
        }
        bool canAct=!net||!networkWorking&&!Network.Busy&&(networkLan||OnlineRoomActionsAvailable)&&!networkAi.Take(menuPlayers).All(a=>a);
        if(edit&&KeyButton(new Rect(1228,122,263,48),"房间号 "+Network.Room.RoomId))GUIUtility.systemCopyBuffer=Network.Room.RoomId;
        if(KeyButton(new Rect(593,765,472,83),edit?"应用设置":net?"创建并入座":"开始游戏",true,canAct)){
            if(edit){Network.ConfigureRoom(NetworkSettings());lanRoomPage=0;}
            else if(net){if(networkLan)CreateLanRoom();else CreateOnlineRoom();}
            else RequestMenuAction(StartConfiguredGame,"开启一桌新游戏");
        }
    }
    Vector2 mapScroll;
    void OpenMapPicker(){
        mapScroll=new Vector2(0,(menuScenario/3)*345);
        atlasOpen=true;
    }
    void MapPicker(){
        Sheet(panelBounds);TitleBand("选择地图");BackControl();
        var ids=ScenarioIds();
        var view=new Rect(188,205,1260,616);
        const float rowHeight=345;
        float contentHeight=((ids.Length+2)/3)*rowHeight;
        mapScroll=GUI.BeginScrollView(view,mapScroll,new Rect(0,0,1234,contentHeight));
        int chosen=-1;
        for(int i=0;i<ids.Length;i++){
            string name=MapTitle(ids[i]);
            var tile=new Rect(5+i%3*412,i/3*rowHeight,395,264);
            Illustration(tile,"map-"+name);
            if(KeyButton(new Rect(tile.x+32,tile.yMax+8,tile.width-64,47),name,i==menuScenario)||Hit(tile))chosen=i;
        }
        GUI.EndScrollView();ScrollMenu(view,ref mapScroll,contentHeight);
        if(chosen>=0){menuScenario=chosen;menuVictoryPoints=null;atlasOpen=false;}
    }
    void ScrollMenu(Rect view,ref Vector2 scroll,float height){
        var logical=new Rect(view.x*1440/1672,view.y*900/941,view.width*1440/1672,view.height*900/941);
        Vector2 touchScroll=scroll*(900f/941);
        MobileTabletop.ScrollTouch(logical,ref touchScroll);
        scroll.y=Mathf.Clamp(touchScroll.y*(941f/900),0,Mathf.Max(0,height-view.height));
        if(height>view.height){
            var bar=new Rect(view.xMax-16,view.y,16,view.height);
            Picture(bar,"parchment");
            PastoralMenuUI.Fill(new Rect(bar.center.x-1,bar.y,2,bar.height),LineGold);
            float thumb=view.height*view.height/height;
            PastoralMenuUI.Round(new Rect(bar.center.x-4,bar.y+(view.height-thumb)*scroll.y/(height-view.height),8,thumb),new Color(.58f,.40f,.22f),4);
        }
    }
    void AiPicker(){
        Sheet(new Rect(420,245,832,420));BandedHeading(new Rect(445,291,782,56),"电脑席位",34);
        for(int i=0;i<menuPlayers;i++)if(KeyButton(new Rect(481+i*176,414,157,62),ColorLabel("P"+(i+1)),networkAi[i]))networkAi[i]=!networkAi[i];
        if(KeyButton(new Rect(696,552,280,62),"完成",true))seatOptionsOpen=false;
    }
    void AcceptedLoad(){
        RefreshMenuSavePreview();Sheet(panelBounds);BackControl();TitleBand("加载游戏");
        Caption(new Rect(235,231,265,52),"最近的保存",27);
        SaveRow(303,MenuLocalSavePath,menuPreviewText);
        if(KeyButton(new Rect(235,521,1190,59),backupExpanded?"▴  较早的保存":"▾  较早的保存"))backupExpanded=!backupExpanded;
        if(backupExpanded)SaveRow(605,MenuLocalSavePath+".bak",menuPreviewBackupText);
        Caption(new Rect(250,790,1170,38),menuInlineMessage,19);
    }
    void SaveRow(int y,string path,string description){
        Surface(new Rect(226,y-7,1186,158),"card");
        bool exists=File.Exists(path);string name=path==MenuLocalSavePath?primarySaveMap:backupSaveMap;
        if(exists&&name!=null)Picture(new Rect(244,y,230,144),"map-"+name);
        Caption(new Rect(486,y,570,57),exists?name??"本地存档":"暂无存档",28);
        if(exists)Caption(new Rect(486,y+56,570,83),description,19);
        if(KeyButton(new Rect(1108,y+35,252,65),path==MenuLocalSavePath?"继续这局":"加载",true,exists)){
            if(NetworkGame){menuInlineMessage="请从当前房间退出后加载";return;}
            RequestMenuAction(()=>{try{ReadSave(path);}catch{menuInlineMessage="无法读取这份存档";}},"加载这份存档",false);
        }
    }
    string primarySaveMap,backupSaveMap;
    void AcceptedSettings(ref int tab,bool atTable){
        Sheet(panelBounds);
        var back=new Rect(154,124,146,50);
        Caption(back,"‹ 返回",23);
        if(Hit(back)){ExitSettings(atTable);return;}
        TitleBand("设置");RuleLine(185,286,1303);RuleLine(185,741,1303);
        int count=SettingsTitles.Length+(atTable?1:0);
        float step=1272f/count;
        for(int i=0;i<count;i++){
            string title=i<SettingsTitles.Length?SettingsTitles[i]:"视角与节奏";
            if(KeyButton(new Rect(185+i*step,199,step-11,65),title,tab==i)){
                tab=i;GUI.FocusControl(null);return;
            }
        }
        if(tab==0){
            Caption(new Rect(230,379,300,57),"玩家名称",30);
            menuPlayerName=Edit(new Rect(248,466,650,65),menuPlayerName??LocalPlayerName,false,PlayerIdentityModel.MaximumNameLength);
            if(!string.IsNullOrWhiteSpace(menuPlayerName)&&LocalPlayerName!=PlayerIdentityModel.NormalizeName(menuPlayerName))LocalPlayerName=menuPlayerName;
            Picture(new Rect(930,346,520,332),"player-plaque");
            var nameRect=new Rect(1048,428,260,76);
            var nameStyle=Type(38);nameStyle.fontSize=38;
            TabletopTextFit.Label(nameRect,LocalPlayerName,nameStyle,12,false);nameStyle.fontSize=38;
        }else if(tab==1){
            var a=TabletopAudio.Instance;if(a!=null){
                if(KeyButton(new Rect(1200,304,232,54),a.Muted?"开启声音":"静音"))a.Muted=!a.Muted;
                ApplyVolume(375,"总音量",a.MasterVolume,v=>a.MasterVolume=v);ApplyVolume(433,"操作音效",a.EffectsVolume,v=>a.EffectsVolume=v);
                ApplyVolume(491,"背景音乐",a.MusicVolume,v=>a.MusicVolume=v);ApplyVolume(549,"自然环境",a.AmbientVolume,v=>a.AmbientVolume=v);
                ApplyVolume(607,"骑士语音",a.VoiceVolume,v=>a.VoiceVolume=v);
                Caption(new Rect(222,669,260,48),"语音语言");
                for(int i=0;i<2;i++)if(KeyButton(new Rect(546+i*164,665,153,52),i==0?"中文":"英语",a.VoiceLanguage==i))a.VoiceLanguage=i;
                if(KeyButton(new Rect(1039,665,317,52),"试听骑士语音"))a.PreviewVoice();
            }
        }else if(tab==2){
            for(int i=0;i<4;i++){
                var r=new Rect(165+i*336,302,315,300);
                Surface(r,"card");
                GUI.DrawTexture(new Rect(r.x+8,r.y+8,r.width-16,r.height-77),Art("theme-"+i),ScaleMode.ScaleAndCrop);
                if(StrategyHudTheme.Selected==i)Illustration(new Rect(r.xMax-48,r.y+13,33,33),"check-on");
                if(KeyButton(new Rect(r.x,r.yMax-60,r.width,57),StrategyHudTheme.Names[i],StrategyHudTheme.Selected==i)||Hit(new Rect(r.x,r.y,r.width,r.height-60)))StrategyHudTheme.Select(i);
            }
            Caption(new Rect(205,650,310,62),"棋盘美术");
            for(int i=0;i<2;i++)if(KeyButton(new Rect(590+i*344,644,310,59),boardArtNames[i],BoardArtPreference.Painted==(i==0)))SelectBoardArt(i==0);
        }else if(tab==3){
            for(int i=0;i<PastoralMenuScene.ThemeCount;i++){
                int index=i==0?PastoralMenuScene.CollectorTable:i==1?PastoralMenuScene.ClassicHarbor:i-2;
                var thumbnail=PastoralMenuScene.Preview(index);
                if(thumbnail!=null)GUI.DrawTexture(new Rect(172+i*189,293,182,102),thumbnail,ScaleMode.ScaleAndCrop);
                if(KeyButton(new Rect(172+i*189,396,182,48),PastoralMenuScene.Names[index],PastoralMenuScene.Selected==index)||Hit(new Rect(172+i*189,298,182,98)))PastoralMenuScene.Select(index);
            }
            var current=PastoralMenuScene.Selected;var tex=PastoralMenuScene.Preview(current);
            if(tex!=null)GUI.DrawTexture(new Rect(250,472,525,257),tex,ScaleMode.ScaleToFit);
            Caption(new Rect(920,500,390,73),PastoralMenuScene.Names[current],35);
            bool still=current==PastoralMenuScene.CollectorTable;
            if(KeyButton(new Rect(926,619,380,54),still?"静态原画":PastoralMenuScene.Animated?"动态风景 · 开启":"动态风景 · 关闭",false,!still))PastoralMenuScene.Animated=!PastoralMenuScene.Animated;
        }else DrawTablePauseView();
        if(KeyButton(new Rect(585,756,509,86),"完成",true))ExitSettings(atTable);
    }
    void ExitSettings(bool atTable){
        GUI.FocusControl(null);
        if(atTable)HandleTablePausePageEscape();
        else{TabletopAudio.Instance?.FlushSettings();MenuBack();}
    }
    void ApplyVolume(int y,string name,float value,Action<float> apply){
        float next=SettingsSlider(y,name,value,Mathf.RoundToInt(value*100)+"%");
        if(!Mathf.Approximately(next,value))apply(next);
    }
    float SettingsSlider(int y,string name,float value,string display){
        Caption(new Rect(203,y-20,289,45),name);
        var track=new Rect(484,y-6,732,12);
        PastoralMenuUI.Round(track,new Color(.65f,.50f,.31f),5);PastoralMenuUI.Round(new Rect(track.x+5,track.y+3,(track.width-10)*value,6),new Color(.24f,.34f,.12f),3);
        Illustration(new Rect(track.x+(track.width-34)*value,y-17,34,34),"slider-knob");
        var next=GUI.HorizontalSlider(new Rect(track.x,y-17,track.width,34),value,0,1,GUIStyle.none,GUIStyle.none);
        Caption(new Rect(1242,y-20,150,45),display);return next;
    }
}
