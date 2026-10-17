using System;
using System.IO;
using System.Linq;
using UnityEngine;

public sealed partial class M6App
{
    // -1 为纯风景主页，五个入口分别拥有独立内容；开局配置只在三个步骤内共享。
    int menuPage=-1,menuSetupStep,menuSettingsTab;
    Font pastoralMenuFont;
    readonly float[] menuNavBlend=new float[5];
    float menuPageAt,menuNavAt;
    Action menuPendingAction;
    bool menuPendingAllowSave;
    string menuPendingCaption,menuInlineMessage="";
    string menuPlayerName;
    string menuPreviewPath,menuPreviewText,menuPreviewBackupText;
    long menuPreviewStamp=-1,menuPreviewBackupStamp=-1;
    internal string MenuReviewSavePath;
    string MenuLocalSavePath=>MenuReviewSavePath??SavePath;
    static readonly string[] MenuEntries={"开始游戏","加载游戏","局域网联机","在线游戏","设置"};
    static readonly string[] MenuHints={"让一段新的故事启程","回到上一次的岛屿时光","与同一网络的好友相聚","连接远方的旅伴","按自己的节奏享受游戏"};
    static readonly string[] RuleDescriptions={
        "采集、交易与建造。\n从第一座村庄开始经营岛屿。",
        "带上船只与行囊。\n沿海岸探索不同的岛屿剧本。",
        "发展城邦，召集骑士。\n迎接蛮族与进步的时代。",
        "让船帆与骑士同行。\n在兼容剧本中体验双扩展。"};

    void NavigateMainMenu(int page)
    {
        GUI.FocusControl(null);menuInlineMessage="";menuPageAt=Time.unscaledTime;
        if(page==2||page==3){OpenSeparatedNetworkMenu(page==2);return;}
        networkMenu=false;menuPage=page;
    }
    void HandleMainMenuEscape()
    {
        if(pendingJoinRoom!=null){CancelJoinPassword();return;}
        if(menuPendingAction!=null){menuPendingAction=null;return;}
        if(networkMenu){if(NetworkGame&&Ready){networkMenu=false;ResumeGame();}else{networkMenu=false;menuPage=-1;}return;}
        if(menuPage==0&&menuSetupStep>0){menuSetupStep--;return;}
        if(menuPage>=0){NavigateMainMenu(-1);return;}
        if(Ready)ResumeGame();
    }
    void DrawPastoralShell(int selectedPage)
    {
        if(pastoralMenuFont==null)pastoralMenuFont=Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
        PastoralMenuUI.Styles(pastoralMenuFont??font);PastoralMenuScene.Draw(Time.unscaledTime);
        bool before=GUI.enabled;GUI.enabled=before&&menuPendingAction==null;
        PastoralMenuUI.Label(new Rect(78,57,300,24),"山海之间  ·  一桌好时光",12,new Color(.86f,.87f,.74f));
        PastoralMenuUI.Fill(new Rect(78,99,44,2),PastoralMenuUI.Gold);
        PastoralMenuUI.Label(new Rect(72,128,350,85),"卡坦岛",67,PastoralMenuUI.Ivory,true);
        PastoralMenuUI.Label(new Rect(78,217,330,45),"之 三 国 争 霸",28,new Color(.93f,.87f,.70f));
        PastoralMenuUI.Label(new Rect(79,280,300,25),"山海之间，筑起属于你的城邦。",13,new Color(.83f,.86f,.77f));
        if(GUI.Button(new Rect(65,115,350,155),GUIContent.none,GUIStyle.none))NavigateMainMenu(-1);
        float delta=Mathf.Min(.05f,Time.unscaledTime-menuNavAt);
        for(int i=0;i<5;i++)
        {
            var r=new Rect(65,350+i*75,322,68);
            if(Event.current.type==EventType.Repaint)menuNavBlend[i]=Mathf.MoveTowards(menuNavBlend[i],r.Contains(Event.current.mousePosition)?1:0,delta*7);
            if(PastoralMenuUI.Nav(r,MenuEntries[i],MenuHints[i],selectedPage==i,menuNavBlend[i]))NavigateMainMenu(i);
        }
        if(Event.current.type==EventType.Repaint)menuNavAt=Time.unscaledTime;
        if(Ready&&!NetworkGame&&PastoralMenuUI.Button(new Rect(79,762,290,42),"继续当前对局   →")){networkMenu=false;ResumeGame();}
        PastoralMenuUI.Label(new Rect(79,840,310,25),ReleaseVersion+"  ·  愿每次出发都有丰收",11,new Color(.83f,.85f,.74f));
        GUI.enabled=before;
    }
    void DrawPastoralMainMenu()
    {
        DrawPastoralShell(menuPage);
        if(networkMenu)return;
        bool before=GUI.enabled;GUI.enabled=before&&menuPendingAction==null;
        if(menuPage<0)
        {
            PastoralMenuUI.Round(new Rect(1004,768,340,79),new Color(.08f,.18f,.15f,.48f),12);
            PastoralMenuUI.Label(new Rect(1027,782,300,23),PastoralMenuScene.Names[PastoralMenuScene.Selected],19,PastoralMenuUI.Ivory);
            PastoralMenuUI.Label(new Rect(1027,816,295,20),PastoralMenuScene.Descriptions[PastoralMenuScene.Selected],12,new Color(.90f,.89f,.76f));
        }
        else
        {
            var matrix=GUI.matrix;var tint=GUI.color;
            float progress=Mathf.Clamp01((Time.unscaledTime-menuPageAt)/.24f);
            GUI.matrix=matrix*Matrix4x4.Translate(new Vector3((1-Mathf.SmoothStep(0,1,progress))*12,0,0));
            GUI.color=new Color(tint.r,tint.g,tint.b,tint.a*(.35f+.65f*progress));
            PastoralMenuUI.Panel(new Rect(454,56,920,788));
            if(menuPage==0)DrawNewGamePages();
            else if(menuPage==1)DrawLoadGamePage();
            else if(menuPage==4)DrawMainSettings();
            GUI.matrix=matrix;GUI.color=tint;
        }
        GUI.enabled=before;
        if(menuInlineMessage.Length>0)
        {
            PastoralMenuUI.Round(new Rect(488,854,850,34),PastoralMenuUI.Paper,8);
            PastoralMenuUI.Label(new Rect(500,860,826,25),menuInlineMessage,13,PastoralMenuUI.Forest);
        }
        DrawMenuConfirmation();
    }
    void MenuPageHeader(string eyebrow,string heading,string subtitle)
    {
        PastoralMenuUI.Label(new Rect(495,84,770,21),eyebrow,12,PastoralMenuUI.Forest,true);
        PastoralMenuUI.Label(new Rect(493,118,795,44),heading,31,null,true);
        PastoralMenuUI.Label(new Rect(495,171,790,34),subtitle,14,PastoralMenuUI.Muted);
        if(PastoralMenuUI.Button(new Rect(1313,84,30,30),"×"))NavigateMainMenu(-1);
    }
    void DrawNewGamePages()
    {
        MenuPageHeader("启 程 之 前","开一桌新冒险","选好玩法与同伴，让新的岛屿故事从这里开始。");
        string[] steps={"选择玩法","选择地图","同桌设置"};
        for(int i=0;i<3;i++)
        {
            var r=new Rect(495+i*285,216,266,36);
            if(PastoralMenuUI.Button(r,(i+1).ToString("00")+"   "+steps[i],true,false,menuSetupStep==i)){menuSetupStep=i;GUI.FocusControl(null);}
        }
        if(menuSetupStep==0)
        {
            for(int i=0;i<4;i++)
            {
                var r=new Rect(495+(i%2)*428,278+(i/2)*175,410,157);
                bool chosen=menuRule==i;
                if(PastoralMenuUI.Button(r,"",true,false,chosen)&&!chosen){menuRule=i;menuScenario=0;menuVictoryPoints=null;}
                MenuMapPreviews.DrawRule(new Rect(r.x+9,r.y+15,162,124),i);
                PastoralMenuUI.Label(new Rect(r.x+182,r.y+22,211,33),RuleNames[i],21,chosen?PastoralMenuUI.Ivory:PastoralMenuUI.Ink,true);
                PastoralMenuUI.Label(new Rect(r.x+182,r.y+71,211,68),RuleDescriptions[i],13,chosen?new Color(.84f,.90f,.80f):PastoralMenuUI.Muted);
            }
            PastoralMenuUI.Label(new Rect(498,645,816,36),"每种玩法都保留完整的独立规则；接下来选择本局地图。",14,PastoralMenuUI.Muted);
        }
        else if(menuSetupStep==1)DrawMapChoices();
        else DrawTableChoices();
        PastoralMenuUI.Fill(new Rect(495,714,838,1),new Color(.48f,.54f,.42f,.20f));
        var ids=ScenarioIds();menuScenario=Mathf.Clamp(menuScenario,0,ids.Length-1);
        string map=menuRule==0?"标准岛屿":menuRule==2?"标准城邦":ScenarioName(ids[menuScenario]);
        PastoralMenuUI.Label(new Rect(497,737,510,26),RuleNames[menuRule]+"  /  "+map,16,null,true);
        PastoralMenuUI.Label(new Rect(497,774,500,23),menuPlayers+" 人同桌  ·  你的建筑 "+PlayerIdentityModel.ColorNames[menuHuman]+"  ·  "+(menuVictoryPoints??DefaultVictoryPoints(RuleIds[menuRule],ids[menuScenario],menuPlayers))+" 分获胜",13,PastoralMenuUI.Muted);
        if(menuSetupStep>0&&PastoralMenuUI.Button(new Rect(1000,751,104,46),"上一步"))menuSetupStep--;
        if(PastoralMenuUI.Button(new Rect(1117,751,216,46),menuSetupStep==2?"启程 · 开始游戏":"下一步   →",true,true))
        {
            if(menuSetupStep<2){menuSetupStep++;GUI.FocusControl(null);}
            else RequestMenuAction(StartConfiguredGame,"开启一桌新游戏");
        }
    }
    void DrawMapChoices()
    {
        var ids=ScenarioIds();menuScenario=Mathf.Clamp(menuScenario,0,ids.Length-1);
        if(menuRule==0||menuRule==2)
        {
            PastoralMenuUI.Round(new Rect(495,280,838,225),new Color(.85f,.88f,.78f),12);
            MenuMapPreviews.DrawScenario(new Rect(496,281,836,223),menuRule,ids[menuScenario],menuPlayers);
            PastoralMenuUI.Label(new Rect(500,533,820,38),menuRule==0?"标准岛屿":"标准城市与骑士地图",25,null,true);
            PastoralMenuUI.Label(new Rect(500,588,810,70),MenuMapPreviews.ScenarioCaption(menuRule,ids[menuScenario],menuPlayers)+"\n地形、点数与港口随新局生成。",16,PastoralMenuUI.Muted);
            return;
        }
        for(int i=0;i<ids.Length;i++)
        {
            var r=new Rect(495+(i%3)*285,274+(i/3)*133,267,124);
            bool chosen=menuScenario==i;
            if(PastoralMenuUI.Button(r,"",true,false,chosen)){menuScenario=i;menuVictoryPoints=null;}
            MenuMapPreviews.DrawScenarioThumbnail(new Rect(r.x+7,r.y+8,135,108),menuRule,ids[i],menuPlayers);
            PastoralMenuUI.Label(new Rect(r.x+151,r.y+23,108,48),ScenarioName(ids[i]),18,chosen?PastoralMenuUI.Ivory:PastoralMenuUI.Ink,true);
            PastoralMenuUI.Label(new Rect(r.x+151,r.y+83,108,30),menuPlayers+" 人 · 示意",12,chosen?new Color(.84f,.90f,.80f):PastoralMenuUI.Muted);
        }
        PastoralMenuUI.Label(new Rect(499,678,824,28),MenuMapPreviews.ScenarioCaption(menuRule,ids[menuScenario],menuPlayers),12,PastoralMenuUI.Muted);
    }
    void DrawTableChoices()
    {
        menuHuman=Mathf.Clamp(menuHuman,0,menuPlayers-1);
        PastoralMenuUI.Label(new Rect(499,284,400,31),"邀请同伴入座",22,null,true);
        PastoralMenuUI.Label(new Rect(499,324,400,24),"其余席位交给当前 AI，一起经营这座岛。",13,PastoralMenuUI.Muted);
        menuPlayers=PastoralMenuUI.Segments(new Rect(499,372,394,45),menuPlayers-3,new[]{"3 人同桌","4 人同桌"})+3;
        menuHuman=Mathf.Clamp(menuHuman,0,menuPlayers-1);
        PastoralMenuUI.Label(new Rect(499,447,390,24),"你的建筑颜色 · 行动顺序由开局掷骰决定",14,PastoralMenuUI.Muted);
        menuHuman=PastoralMenuUI.Segments(new Rect(499,484,394,43),menuHuman,PlayerIdentityModel.ColorNames.Take(menuPlayers).ToArray());
        PastoralMenuUI.Label(new Rect(499,565,390,24),"AI 行动节奏",14,PastoralMenuUI.Muted);
        menuAiSpeed=PastoralMenuUI.Segments(new Rect(499,603,394,43),menuAiSpeed,TabletopAiSpeed.Names);
        PastoralMenuUI.Fill(new Rect(926,286,1,374),new Color(.48f,.54f,.42f,.18f));
        var ids=ScenarioIds();int defaults=DefaultVictoryPoints(RuleIds[menuRule],ids[menuScenario],menuPlayers),maximum=MaximumVictoryPoints(RuleIds[menuRule],ids[menuScenario]);
        if(menuVictoryPoints.HasValue)menuVictoryPoints=Mathf.Clamp(menuVictoryPoints.Value,5,maximum);
        int points=menuVictoryPoints??defaults;
        PastoralMenuUI.Label(new Rect(966,285,330,35),"本局胜利目标",22,null,true);
        PastoralMenuUI.Label(new Rect(966,327,330,30),"按喜欢的节奏，决定这一桌的长度。",13,PastoralMenuUI.Muted);
        if(PastoralMenuUI.Button(new Rect(969,414,49,49),"−",points>5))menuVictoryPoints=points-1;
        PastoralMenuUI.Label(new Rect(1029,390,208,94),(menuVictoryPoints??defaults)+"",66,PastoralMenuUI.Forest,true,TextAnchor.MiddleCenter);
        PastoralMenuUI.Label(new Rect(1054,482,165,24),"胜 利 分 数",13,PastoralMenuUI.Muted,false,TextAnchor.MiddleCenter);
        if(PastoralMenuUI.Button(new Rect(1250,414,49,49),"+",points<maximum))menuVictoryPoints=points+1;
        if(PastoralMenuUI.Button(new Rect(969,544,330,39),"使用剧本推荐 · "+defaults+" 分"))menuVictoryPoints=null;
        string hint=menuRule==1&&ids[menuScenario]=="wonders-of-catan"?"奇观的特殊胜利条件仍适用。":menuRule==1&&ids[menuScenario]=="pirate-islands"?"获胜仍须夺回自己的堡垒。":menuRule==1&&ids[menuScenario]=="cloth-for-catan"?"布匹耗尽时仍按剧本规则结算。":"设置将随本局存档保留。";
        PastoralMenuUI.Label(new Rect(969,608,330,58),"可选 5–"+maximum+" 分。"+hint,14,PastoralMenuUI.Muted);
    }
    void StartConfiguredGame()
    {
        var ids=ScenarioIds();
        StartGame(RuleIds[menuRule],ids[Mathf.Clamp(menuScenario,0,ids.Length-1)],menuPlayers,"P"+(Mathf.Clamp(menuHuman,0,menuPlayers-1)+1),targetVictoryPoints:menuVictoryPoints,aiSpeedLevel:menuAiSpeed);
    }
    void RequestMenuAction(Action action,string caption,bool allowSave=true)
    {
        if(Ready){menuPendingAction=action;menuPendingCaption=caption;menuPendingAllowSave=allowSave;}
        else action();
    }
    void DrawMenuConfirmation()
    {
        if(menuPendingAction==null)return;
        PastoralMenuUI.Fill(new Rect(0,0,1440,900),new Color(.025f,.06f,.045f,.66f));
        PastoralMenuUI.Panel(new Rect(458,271,550,326));
        PastoralMenuUI.Label(new Rect(496,305,472,42),menuPendingCaption,27,null,true);
        PastoralMenuUI.Label(new Rect(496,367,472,65),NetworkGame?"这将离开当前联机对局，服务器保留已确认的进度。":menuPendingAllowSave?"当前对局仍在进行。请确认是否先保存，再离开这一桌。":"加载会替换当前对局，尚未保存的进度将丢失。",17,PastoralMenuUI.Muted);
        bool canSave=!NetworkGame&&menuPendingAllowSave;
        if(canSave&&PastoralMenuUI.Button(new Rect(496,455,472,44),"保存当前局并继续",true,true))
        {
            try{WriteSave(MenuLocalSavePath);var next=menuPendingAction;menuPendingAction=null;next();}
            catch(Exception){menuInlineMessage="保存失败，当前对局已保留。";menuPendingAction=null;}
        }
        if(PastoralMenuUI.Button(new Rect(496,canSave?514:466,230,40),NetworkGame?"离开并继续":"直接继续"))
        {var next=menuPendingAction;menuPendingAction=null;next();}
        if(PastoralMenuUI.Button(new Rect(738,canSave?514:466,230,40),"留在当前对局"))menuPendingAction=null;
    }
    void RefreshMenuSavePreview()
    {
        string path=MenuLocalSavePath;long stamp=File.Exists(path)?File.GetLastWriteTimeUtc(path).Ticks:0;
        string backup=path+".bak";long backupStamp=File.Exists(backup)?File.GetLastWriteTimeUtc(backup).Ticks:0;
        if(path==menuPreviewPath&&stamp==menuPreviewStamp&&backupStamp==menuPreviewBackupStamp)return;
        menuPreviewPath=path;menuPreviewStamp=stamp;menuPreviewBackupStamp=backupStamp;
        menuPreviewText=MenuSaveDescription(path);menuPreviewBackupText=MenuSaveDescription(backup);
    }
    string MenuSaveDescription(string path)
    {
        if(!File.Exists(path))return "还没有留下这座岛的故事。";
        try
        {
            var data=JsonUtility.FromJson<SavedGame>(File.ReadAllText(path));
            if(data==null||data.Version!=1||string.IsNullOrEmpty(data.Authority))throw new InvalidDataException();
            ValidateConfiguration(data.RuleSet,data.ScenarioId,data.PlayerCount,data.HumanSeat);
            return RuleNames[Array.IndexOf(RuleIds,data.RuleSet)]+"  ·  "+data.PlayerCount+" 人  ·  "+ColorLabel(data.HumanSeat)+"建筑"+"\n保存于 "+File.GetLastWriteTime(path).ToString("yyyy-MM-dd  HH:mm");
        }
        catch{return "此存档无法预览，可能损坏或版本不兼容。";}
    }
    void DrawLoadGamePage()
    {
        RefreshMenuSavePreview();
        MenuPageHeader("旧 日 旅 程","回到熟悉的岛屿","恢复上次的地图、手牌、待决选择与 AI 行动节奏。");
        DrawSaveSlot(new Rect(495,259,838,177),"最近一次保存",menuPreviewText,MenuLocalSavePath);
        DrawSaveSlot(new Rect(495,463,838,177),"上一次完整备份",menuPreviewBackupText,MenuLocalSavePath+".bak");
        PastoralMenuUI.Label(new Rect(499,681,810,42),"联机对局由服务器保管，请从对应的联机入口恢复原席位。",14,PastoralMenuUI.Muted);
        PastoralMenuUI.Label(new Rect(499,756,810,42),menuInlineMessage,14,PastoralMenuUI.Forest);
    }
    void DrawSaveSlot(Rect r,string heading,string description,string path)
    {
        PastoralMenuUI.Round(r,new Color(.91f,.925f,.86f),12);
        PastoralMenuUI.Label(new Rect(r.x+26,r.y+22,r.width-250,35),heading,22,null,true);
        PastoralMenuUI.Label(new Rect(r.x+26,r.y+74,r.width-260,65),description,15,PastoralMenuUI.Muted);
        if(PastoralMenuUI.Button(new Rect(r.xMax-190,r.y+65,162,46),"继续旅程   →",File.Exists(path),true))
            RequestMenuAction(()=>{
                try{if(NetworkGame){menuInlineMessage="请先返回联机房间退出，再加载本地存档。";return;}ReadSave(path);}
                catch{menuInlineMessage="无法恢复：文件损坏或版本不兼容，当前对局已保留。";}
            },"加载这份存档",false);
    }
    void DrawMainSettings()
    {
        MenuPageHeader("舒 适 地 游 玩","设置","让声音与桌面，配合你喜欢的游戏时光。");
        menuSettingsTab=PastoralMenuUI.Segments(new Rect(495,216,838,39),menuSettingsTab,new[]{"玩家","声音","桌面外观","主页风景"});
        if(menuSettingsTab==0)
        {
            if(menuPlayerName==null)menuPlayerName=LocalPlayerName;
            PastoralMenuUI.Label(new Rect(499,302,800,36),"同桌伙伴眼中的你",24,null,true);
            PastoralMenuUI.Label(new Rect(499,353,800,36),"本地游戏与联机共用这个名字，新加入房间时自动使用。",14,PastoralMenuUI.Muted);
            PastoralMenuUI.Label(new Rect(499,422,220,28),"玩家名称",18,null,true);
            menuPlayerName=GUI.TextField(new Rect(499,465,600,49),menuPlayerName,PlayerIdentityModel.MaximumNameLength,PastoralMenuUI.Field);
            if(!string.IsNullOrWhiteSpace(menuPlayerName)&&LocalPlayerName!=PlayerIdentityModel.NormalizeName(menuPlayerName))LocalPlayerName=menuPlayerName;
            PastoralMenuUI.Label(new Rect(499,539,800,32),"最多 20 个字。建筑颜色在开局或房间中选择，行动顺序由开局掷骰决定。",14,PastoralMenuUI.Muted);
        }
        else if(menuSettingsTab==1)
        {
            var audio=TabletopAudio.Instance;
            PastoralMenuUI.Label(new Rect(499,296,600,36),"听见海岸与每一次落子",24,null,true);
            PastoralMenuUI.Label(new Rect(499,336,600,25),"音量、语言与静音会自动保存。",14,PastoralMenuUI.Muted);
            if(audio==null){PastoralMenuUI.Label(new Rect(499,405,800,30),"声音正在准备中…");return;}
            if(PastoralMenuUI.Button(new Rect(1155,299,174,39),audio.Muted?"已静音 · 开启":"声音开启 · 静音",true,false,!audio.Muted))
            {audio.Muted=!audio.Muted;if(!audio.Muted)audio.Click();}
            DrawMenuVolume(365,"总音量","游戏的整体音量",audio.MasterVolume,v=>audio.MasterVolume=v);
            DrawMenuVolume(422,"操作音效","交易、建造、骑士与卡牌",audio.EffectsVolume,v=>audio.EffectsVolume=v);
            DrawMenuVolume(479,"背景音乐","舒缓的循环旋律",audio.MusicVolume,v=>audio.MusicVolume=v);
            DrawMenuVolume(536,"自然环境","随主页风景切换的环境声",audio.AmbientVolume,v=>audio.AmbientVolume=v);
            DrawMenuVolume(593,"骑士语音","招募和激活骑士的应答",audio.VoiceVolume,v=>audio.VoiceVolume=v);
            PastoralMenuUI.Label(new Rect(499,663,225,28),"语音语言",17,null,true);
            int voiceLanguage=PastoralMenuUI.Segments(new Rect(794,656,310,38),audio.VoiceLanguage,new[]{"中文","英语"});
            if(voiceLanguage!=audio.VoiceLanguage)audio.VoiceLanguage=voiceLanguage;
            if(PastoralMenuUI.Button(new Rect(1120,656,213,38),"试听骑士语音"))audio.PreviewVoice();
        }
        else if(menuSettingsTab==2)
        {
            PastoralMenuUI.Label(new Rect(499,302,800,36),"选择桌面氛围",24,null,true);
            PastoralMenuUI.Label(new Rect(499,351,800,32),"进入对局后的桌面配色，随时也可在游戏菜单中切换。",14,PastoralMenuUI.Muted);
            Color[] colors={new Color(.40f,.27f,.17f),new Color(.12f,.33f,.40f),new Color(.25f,.34f,.23f)};
            for(int i=0;i<StrategyHudTheme.Names.Length;i++)
            {
                Rect r=new Rect(499+i*284,421,264,183);
                bool hit=PastoralMenuUI.Card(r,StrategyHudTheme.Names[i],"桌面主题",StrategyHudTheme.Selected==i);
                PastoralMenuUI.Round(new Rect(r.x+22,r.y+117,r.width-44,40),colors[i%3],7);
                if(hit)StrategyHudTheme.Select(i);
            }
            PastoralMenuUI.Label(new Rect(499,652,800,44),"想更换主页风景，请打开上方「主页风景」。",14,PastoralMenuUI.Muted);
        }
        else DrawMenuLandscapeSettings();
        PastoralMenuUI.Fill(new Rect(495,714,838,1),new Color(.48f,.54f,.42f,.20f));
        if(PastoralMenuUI.Button(new Rect(1117,751,216,46),"完成   →",true,true))
        {if(menuPlayerName!=null){LocalPlayerName=menuPlayerName;menuPlayerName=LocalPlayerName;}TabletopAudio.Instance?.FlushSettings();NavigateMainMenu(-1);}
    }
    void DrawMenuVolume(float y,string name,string detail,float value,Action<float> update)
    {
        PastoralMenuUI.Label(new Rect(499,y,260,25),name,17,null,true);
        PastoralMenuUI.Label(new Rect(499,y+27,280,21),detail,12,PastoralMenuUI.Muted);
        float next=PastoralMenuUI.Slider(new Rect(794,y+13,449,20),value);
        PastoralMenuUI.Label(new Rect(1265,y+6,64,31),Mathf.RoundToInt(next*100)+"%",16,PastoralMenuUI.Forest,false,TextAnchor.MiddleRight);
        if(Mathf.Abs(next-value)>.0001f)update(next);
    }
    void DrawMenuLandscapeSettings()
    {
        PastoralMenuUI.Label(new Rect(499,276,800,30),"把喜欢的风景留在主页",23,null,true);
        for(int i=0;i<PastoralMenuScene.ThemeCount;i++)
        {
            var r=new Rect(495+i*170,320,158,124);bool chosen=PastoralMenuScene.Selected==i;
            if(PastoralMenuUI.Button(r,"",true,false,chosen))PastoralMenuScene.Select(i);
            var image=PastoralMenuScene.Preview(i);
            if(image!=null)GUI.DrawTexture(new Rect(r.x+6,r.y+6,146,86),image,ScaleMode.ScaleAndCrop);
            PastoralMenuUI.Label(new Rect(r.x+5,r.y+97,r.width-10,22),PastoralMenuScene.Names[i],15,chosen?PastoralMenuUI.Ivory:PastoralMenuUI.Ink,chosen,TextAnchor.MiddleCenter);
        }
        var selected=PastoralMenuScene.Preview(PastoralMenuScene.Selected);
        PastoralMenuUI.Round(new Rect(495,463,401,226),new Color(.10f,.22f,.18f),10);
        if(selected!=null)GUI.DrawTexture(new Rect(500,468,391,216),selected,ScaleMode.ScaleAndCrop);
        PastoralMenuUI.Label(new Rect(923,475,410,33),PastoralMenuScene.Names[PastoralMenuScene.Selected],24,null,true);
        PastoralMenuUI.Label(new Rect(923,523,410,35),PastoralMenuScene.Descriptions[PastoralMenuScene.Selected],15,PastoralMenuUI.Muted);
        if(PastoralMenuUI.Button(new Rect(923,577,410,44),PastoralMenuScene.Animated?"动态风景 · 已开启":"动态风景 · 已关闭",true,false,PastoralMenuScene.Animated))
            PastoralMenuScene.Animated=!PastoralMenuScene.Animated;
        PastoralMenuUI.Label(new Rect(923,638,410,50),"切换即生效，环境声随风景变化。\n关闭动态后保留静态原画。",13,PastoralMenuUI.Muted);
    }
}
