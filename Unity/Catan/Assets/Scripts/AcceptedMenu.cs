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
        value=new GUIStyle(GUI.skin.label){font=pastoralMenuFont,fontSize=Math.Abs(size),fontStyle=FontStyle.Normal,padding=new RectOffset(),alignment=TextAnchor.MiddleCenter,clipping=TextClipping.Clip};
        value.normal.textColor=size<0?new Color(1,.95f,.80f):new Color(.28f,.18f,.10f);menuTypography[size]=value;return value;
    }
    void Caption(Rect r,string text,int size=25,TextAnchor alignment=TextAnchor.MiddleCenter,bool fit=false,bool pixelText=true){
        if(!pixelText){
            // 滚动列表沿用组内坐标，让文字与按钮共用同一裁剪、滚动和按压变换。
            var label=Type(size);label.alignment=alignment;
            GUI.Label(r,text,label);label.alignment=TextAnchor.MiddleCenter;
            return;
        }
        using(var pixels=new PastoralMenuUI.PixelText(r,Math.Abs(size))){
            var style=Type(size<0?-pixels.Size:pixels.Size);
            style.alignment=alignment;
            if(fit)TabletopTextFit.Fit(style,text,pixels.Area,Mathf.Min(8,pixels.Size));
            GUI.Label(pixels.Area,text,style);
            style.fontSize=pixels.Size;style.alignment=TextAnchor.MiddleCenter;
        }
    }
    bool Hit(Rect r,bool markHover=true){
        if(markHover&&Event.current.type==EventType.Repaint&&GUI.enabled&&r.Contains(Event.current.mousePosition))
            MenuMark(r.center.x,r.yMax-4,TabletopButtonFeedback.Held(r)?15:23);
        bool hit=TabletopButtonFeedback.Button(r,false,drawOverlay:false)&&(!MobileTabletop.Enabled||!MobileTabletop.TouchDragged);
        if(hit)TabletopAudio.Instance?.Click();return hit;
    }
    bool KeyButton(Rect r,string text,bool primary=false,bool enabled=true,bool pixelText=true){
        var tint=GUI.color;
        if(!enabled)GUI.color=new Color(.78f,.76f,.70f,tint.a);
        using(var feedback=TabletopButtonFeedback.Begin(r,enabled)){
            bool hover=enabled&&GUI.enabled&&r.Contains(Event.current.mousePosition);
            if(hover)GUI.color=new Color(1,.90f,.71f,tint.a);
            
            Surface(r,primary?"primary":"control");
            if(primary&&r.height>=60&&r.width>=325){
                var mark=new Rect(r.x+20,r.center.y-9,47,18);
                Illustration(mark,"cloud");
                var right=new Rect(r.xMax-67,r.center.y-9,47,18);
                GUI.DrawTextureWithTexCoords(right,Art("cloud"),new Rect(1,0,-1,1));
            }
            Caption(r,text,primary?(r.height>=68?-36:-23):22,pixelText:pixelText);
        }
        GUI.color=tint;return enabled&&Hit(r,false);
    }
    string Edit(Rect r,string value,bool secret=false,int max=100){
        Surface(r,"field");var inner=new Rect(r.x+12,r.y+4,r.width-24,r.height-8);
        using(var pixels=new PastoralMenuUI.PixelText(inner,22)){
            var style=Type(pixels.Size);
            return secret?GUI.PasswordField(pixels.Area,value,'●',max,style):GUI.TextField(pixels.Area,value,max,style);
        }
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
        var r=new Rect(144,122,room?196:174,58);
        if(BackPlaque(r,room?"返回房间":"返回"))MenuBack();
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
        if(SetupPlaque(net?new Rect(298,675,399,49):new Rect(1066,361,391,50),map,false,sailing))OpenMapPicker();
        bool[] active={true,sailing,menuRule>=2,menuThreeKingdoms,menuHarbormaster||menuFishing};
        for(int i=0;i<5;i++){
            if(!ModePlate(i,active[i]))continue;
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
        PlayerSelector(new Rect(1073,net?612:424,375,60),edit);
        menuHuman=Mathf.Clamp(menuHuman,0,menuPlayers-1);
        int max=MaximumVictoryPoints(RuleIds[menuRule],id,menuHarbormaster);
        if(lastDefaultRule!=menuRule||lastDefaultId!=id||lastDefaultCount!=menuPlayers||lastDefaultHarbor!=menuHarbormaster){
            lastDefaultRule=menuRule;lastDefaultId=id;lastDefaultCount=menuPlayers;lastDefaultHarbor=menuHarbormaster;
            cachedVictory=MenuDefaultVictoryPoints(RuleIds[menuRule],id,menuPlayers);
        }
        int points=Mathf.Clamp(menuVictoryPoints??cachedVictory,5,max);
        if(menuVictoryPoints.HasValue)menuVictoryPoints=points;
        int y=net?671:490;
        Picture(new Rect(1074,y,374,62),"setup-stepper");
        if(ScoreKey(new Rect(1108,y+6,69,45),false,points>5))menuVictoryPoints=points-1;
        PlateLettering(new Rect(1180,y+2,161,51),points.ToString(),30,PlateInk.Dark,strengthen:true);
        if(ScoreKey(new Rect(1346,y+6,69,45),true,points<max))menuVictoryPoints=points+1;
        if(!net){
            if(SmallPlate(new Rect(1075,561,374,46),"setup-pace",TabletopAiSpeed.Names[menuAiSpeed],23))menuAiSpeed=(menuAiSpeed+1)%TabletopAiSpeed.Names.Length;
            for(int i=0;i<4;i++){
                var slot=new Rect(418+i*244,645,113,106);
                bool available=i<menuPlayers;
                var tint=GUI.color;
                if(!available)GUI.color=new Color(.78f,.74f,.68f,.65f);
                Picture(new Rect(slot.center.x-51,slot.y+64,102,34),i==menuHuman?"setup-seat-active":"setup-seat");
                var piece=Art("setup-seated-house-"+i);
                float aspect=(float)piece.width/piece.height*Mathf.Abs(GUI.matrix.m11/GUI.matrix.m00);
                float height=Mathf.Min(72,87/aspect),width=height*aspect;
                // 贴图的最后三像素（设计单位）是沿屋脚生成的接触影，固定在座面前部。
                GUI.DrawTexture(new Rect(slot.center.x-width/2,slot.y+89-height,width,height),piece);
                GUI.color=tint;
                if(available&&Hit(slot,false))menuHuman=i;
            }
        }
        bool canAct=!net||!networkWorking&&!Network.Busy&&(networkLan||OnlineRoomActionsAvailable)&&!networkAi.Take(menuPlayers).All(a=>a);
        if(edit&&KeyButton(new Rect(1228,122,263,48),"房间号 "+Network.Room.RoomId))GUIUtility.systemCopyBuffer=Network.Room.RoomId;
        var actionRect=net?new Rect(593,765,472,83):new Rect(584,761,504,90);
        bool begin=net?KeyButton(actionRect,edit?"应用设置":"创建并入座",true,canAct):SetupPlaque(actionRect,"开始游戏",true,canAct);
        if(begin){
            if(edit){Network.ConfigureRoom(NetworkSettings());lanRoomPage=0;}
            else if(net){if(networkLan)CreateLanRoom();else CreateOnlineRoom();}
            else RequestMenuAction(StartConfiguredGame,"开启一桌新游戏");
        }
    }
    Vector2 mapScroll;
    const int AtlasRowHeight=310;
    GUIStyle atlasRail,atlasThumb;
    void OpenMapPicker(){
        mapScroll=new Vector2(0,Mathf.Max(0,menuScenario/3-1)*AtlasRowHeight);
        atlasOpen=true;
    }
    void MapPicker(){
        Sheet(panelBounds);TitleBand("选择地图");BackControl();
        var ids=ScenarioIds();
        var view=new Rect(151,190,1328,AtlasRowHeight*2);
        float contentHeight=((ids.Length+2)/3)*AtlasRowHeight;
        mapScroll=GUI.BeginScrollView(view,mapScroll,new Rect(0,0,view.width,contentHeight),
            false,false,GUIStyle.none,GUIStyle.none);
        int chosen=-1;
        for(int i=0;i<ids.Length;i++){
            string name=MapTitle(ids[i]);
            bool selected=i==menuScenario;
            var tile=new Rect(8+i%3*442,i/3*AtlasRowHeight+5,426,234);
            Illustration(tile,"atlas-"+ids[i]+"-"+menuPlayers);
            var plate=new Rect(tile.center.x-(selected?137:130),tile.yMax+(selected?4:7),selected?274:260,selected?51:44);
            // 绿漆名牌的贴图四周各有 16 单位光晕，名牌和点击区域维持原尺寸。
            Picture(selected?InsetMenuRect(plate,-16):plate,selected?"setup-atlas-jade":"setup-atlas-wood");
            var label=plate;label.height-=3;
            var lettering=Type(selected?24:22);
            var originalInk=lettering.normal.textColor;
            var relief=label;relief.y+=1;
            lettering.normal.textColor=selected?new Color32(62,39,17,195):new Color32(255,232,181,155);
            GUI.Label(relief,name,lettering);
            lettering.normal.textColor=selected?new Color32(250,216,147,255):MenuInk;
            if(selected){
                var stroke=label;stroke.x+=.65f;
                GUI.Label(stroke,name,lettering);
            }
            GUI.Label(label,name,lettering);
            lettering.normal.textColor=originalInk;
            if(Hit(tile,false)||Hit(plate,false))chosen=i;
        }
        GUI.EndScrollView();
        var touchScroll=mapScroll*(900f/941);
        MobileTabletop.ScrollTouch(new Rect(view.x*1440/1672,view.y*900/941,view.width*1440/1672,view.height*900/941),ref touchScroll);
        mapScroll.y=Mathf.Clamp(touchScroll.y*941/900,0,Mathf.Max(0,contentHeight-view.height));
        if(contentHeight>view.height){
            if(atlasRail==null){
                atlasRail=new GUIStyle();
                atlasRail.normal.background=Art("setup-atlas-rail");
                atlasThumb=new GUIStyle{fixedWidth=15,fixedHeight=70};
                atlasThumb.normal.background=Art("setup-atlas-thumb");
            }
            mapScroll.y=GUI.VerticalSlider(new Rect(1482,196,17,580),mapScroll.y,0,contentHeight-view.height,atlasRail,atlasThumb);
        }
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
        int lift=backupExpanded?64:0;
        PastoralMenuUI.Fill(new Rect(956,190,1,478-lift),LineGold);
        DrawSaveInfo(recentSave,backupExpanded);
        if(recentSave?.Modes!=null){
            var button=new Rect(1000,570-lift,434,100);
            Picture(InsetMenuRect(button,-14),"setup-load-continue");
            PlateLettering(button,"继续这局",44,PlateInk.Jade,strengthen:true);
            if(Hit(button,false))LoadMenuSave(MenuLocalSavePath);
        }
        RuleLine(184,689-lift,1278);
        var fold=new Rect(218,715-lift,1227,54);
        Picture(fold,"setup-load-older");
        Caption(new Rect(252,fold.y,40,52),backupExpanded?"▾":"▸",23);
        Caption(new Rect(300,fold.y,330,50),"较早的保存",24,TextAnchor.MiddleLeft);
        if(Hit(fold,false))backupExpanded=!backupExpanded;
        if(backupExpanded){
            Caption(new Rect(241,716,345,55),earlierSave?.Map??"暂无存档",25,TextAnchor.MiddleLeft);
            if(earlierSave?.Modes!=null){
                Caption(new Rect(522,716,373,55),earlierSave.Timestamp,20,TextAnchor.MiddleLeft);
                Caption(new Rect(906,716,90,55),earlierSave.Players+" 人",22,TextAnchor.MiddleLeft);
                Illustration(new Rect(1001,720,48,44),"setup-seated-house-"+earlierSave.Seat);
                int slot=0;
                for(int i=0;i<ModeIcons.Length;i++){
                    if(!earlierSave.Modes[i])continue;
                    float x=242+slot++*165;
                    Illustration(new Rect(x,762,40,35),ModeIcons[i]);
                    Caption(new Rect(x+44,762,120,35),ModeTitles[i],17,TextAnchor.MiddleLeft);
                }
                var button=new Rect(1195,713,216,58);
                Picture(InsetMenuRect(button,-7),"setup-load-continue");
                PlateLettering(button,"加载",25,PlateInk.Gold);
                if(Hit(button,false))LoadMenuSave(MenuLocalSavePath+".bak");
            }
        }
        if(!string.IsNullOrEmpty(menuInlineMessage))Caption(new Rect(250,787,1170,40),menuInlineMessage,19);
    }
    void DrawSaveInfo(SavePreview info,bool compact){
        int lift=compact?64:0;
        Caption(new Rect(990,176,435,42),"最近的保存",25,TextAnchor.MiddleLeft);
        Caption(new Rect(990,211,502,63),info?.Map??"暂无存档",info?.Modes!=null?46:32,TextAnchor.MiddleLeft);
        if(info?.Modes==null)return;
        Illustration(new Rect(215,208,727,440-lift),info.Artwork);
        Caption(new Rect(990,compact?271:284,220,41),"模式",23,TextAnchor.MiddleLeft);
        int slot=0;
        for(int i=0;i<ModeIcons.Length;i++){
            if(!info.Modes[i])continue;
            float x=980+slot++*105;
            Illustration(new Rect(x+2,compact?306:324,97,compact?56:83),ModeIcons[i]);
            var plate=new Rect(x,compact?366:413,103,35);
            Picture(plate,"setup-jade");
            PlateLettering(plate,ModeTitles[i],16,PlateInk.Jade);
        }
        float y=compact?408:465;
        Caption(new Rect(998,y,90,50),"人数",23,TextAnchor.MiddleLeft);
        Caption(new Rect(1094,y,115,50),info.Players+" 人",29,TextAnchor.MiddleLeft);
        Caption(new Rect(1232,y,86,50),"席位",23,TextAnchor.MiddleLeft);
        Illustration(new Rect(1309,y,52,48),"setup-seated-house-"+info.Seat);
        Caption(new Rect(998,516-lift,488,42),info.Timestamp,22,TextAnchor.MiddleLeft);
    }
    void LoadMenuSave(string path){
        if(NetworkGame){menuInlineMessage="请从当前房间退出后加载";return;}
        RequestMenuAction(()=>{try{ReadSave(path);}catch{menuInlineMessage="无法读取这份存档";}},"加载这份存档",false);
    }
    void AcceptedSettings(ref int tab,bool atTable){
        Sheet(panelBounds);
        var back=new Rect(154,124,146,50);
        if(BackPlaque(back,"返回")){ExitSettings(atTable);return;}
        SettingsHeading();RuleLine(206,294,1250);
        int count=SettingsTitles.Length+(atTable?1:0);
        float step=1200f/count;
        for(int i=0;i<count;i++){
            string title=i<SettingsTitles.Length?SettingsTitles[i]:"视角与节奏";
            if(SettingsPlate(new Rect(249+i*step,216,step-20,58),title,tab==i,29)){
                tab=i;GUI.FocusControl(null);return;
            }
        }
        if(tab==0){
            Caption(new Rect(230,379,300,57),"玩家名称",30);
            menuPlayerName=Edit(new Rect(248,466,650,65),menuPlayerName??LocalPlayerName,false,PlayerIdentityModel.MaximumNameLength);
            if(!string.IsNullOrWhiteSpace(menuPlayerName)&&LocalPlayerName!=PlayerIdentityModel.NormalizeName(menuPlayerName))LocalPlayerName=menuPlayerName;
            Picture(new Rect(930,346,520,332),"player-plaque");
            var nameRect=new Rect(1048,428,260,76);
            Caption(nameRect,LocalPlayerName,38,fit:true);
        }else if(tab==1){
            var a=TabletopAudio.Instance;if(a!=null){
                if(SettingsPlate(new Rect(1252,322,156,49),a.Muted?"开启声音":"静音",a.Muted,27))a.Muted=!a.Muted;
                ApplyVolume(411,"总音量",a.MasterVolume,v=>a.MasterVolume=v);ApplyVolume(479,"操作音效",a.EffectsVolume,v=>a.EffectsVolume=v);
                ApplyVolume(547,"背景音乐",a.MusicVolume,v=>a.MusicVolume=v);ApplyVolume(615,"自然环境",a.AmbientVolume,v=>a.AmbientVolume=v);
                ApplyVolume(683,"骑士语音",a.VoiceVolume,v=>a.VoiceVolume=v);
            }
        }else if(tab==2){
            for(int i=0;i<4;i++){
                var r=new Rect(165+i*336,350,315,300);
                Surface(r,"card");
                GUI.DrawTexture(new Rect(r.x+8,r.y+8,r.width-16,r.height-77),Art("theme-"+i),ScaleMode.ScaleAndCrop);
                if(StrategyHudTheme.Selected==i)Illustration(new Rect(r.xMax-48,r.y+13,33,33),"check-on");
                if(KeyButton(new Rect(r.x,r.yMax-60,r.width,57),StrategyHudTheme.Names[i],StrategyHudTheme.Selected==i)||Hit(new Rect(r.x,r.y,r.width,r.height-60)))StrategyHudTheme.Select(i);
            }
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
        if(SettingsDone())ExitSettings(atTable);
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
}
