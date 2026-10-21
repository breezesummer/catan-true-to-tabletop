using System;
using System.IO;
using System.Linq;
using UnityEngine;

// M6 owns local seat configuration and scheduling; rule authority remains in the existing hosts.
// A policy is passed only the acting seat's detached PlayerView, never a host or save payload.
public sealed partial class M6App : MonoBehaviour
{
    public const string BuildVersion="1.7.1";
    public const string ProductFileName="Catan"+BuildVersion;
    public const string ReleaseVersion="内测版"+BuildVersion;
    [Serializable]
    public sealed class SavedGame
    {
        public int Version=1;
        public string RuleSet,ScenarioId,HumanSeat,Authority;
        public int PlayerCount;
        public bool Paused;
        public int AiSpeedLevel=1;
        public bool HasAiSpeed;
        public string[] PlayerNames;
    }

    public string RuleSet { get; private set; }="base";
    public string ScenarioId { get; private set; }="heading-for-new-shores";
    public int PlayerCount { get; private set; }=4;
    public int TargetVictoryPoints { get; private set; }=10;
    public string HumanSeat { get; private set; }="P1";
    public string InitialAuthority { get; private set; }
    public bool Paused { get; private set; }
    public string Fault { get; private set; }="";
    public string Notice { get; private set; }="选择一种规则和你的建筑颜色，其他玩家由本地 AI 执行。";
    public int AiSpeedLevel { get; private set; }=1;
    public int AiCommandsExecuted { get; private set; }
    public string LastCommandKind { get; private set; }="";
    public bool MenuVisible { get; private set; }=true;
    public bool Ready=>actor!=null;
    public bool Finished=>Ready&&finished();
    public string CurrentActor=>Ready?actor():"";
    public bool IsHumanTurn=>Ready&&!Finished&&!TablePresentationBlocked&&!TableDiceRolling&&CurrentActor==HumanSeat&&(!NetworkGame||Network.CanInteract);
    public string WaitingText=>NetworkGame?(Network.CanInteract?"等待 "+PlayerDisplayName(CurrentActor)+" 完成操作。":Network.Status):Fault!=""?"AI 已暂停："+Fault:Paused?"AI 已暂停。按 Esc 打开菜单继续。":Finished?"本局已结束。":"等待 "+PlayerDisplayName(CurrentActor)+" · AI 正在行动。";
    public string SavePath=>Path.Combine(Application.persistentDataPath,"m6-local-ai-v1.json");

    Func<string> actor,save;
    Func<bool> finished;
    Func<string,string> decideAndSubmit;
    Action refresh,close;
    Action<bool> visible;
    MonoBehaviour presentation;
    float nextAction;
    readonly TabletopPromptSequence tablePrompts=new TabletopPromptSequence();
    internal string RoadPlacementPrompt { get; private set; }="";
    internal static readonly Color PromptColor=new Color(.96f,.90f,.68f);
    string promptContext,lastPromptStatus,lastPromptInstruction;
    bool promptWasHuman;
    internal bool TableDiceRolling;
    internal bool TableOverlayOpen;
    int menuRule,menuScenario,menuPlayers=4,menuHuman,menuAiSpeed=1;
    int? menuVictoryPoints;
    bool tableMenu, escapeDismissedInteraction;
    public bool TableMenuOpen=>tableMenu;
    public void OpenTableMenu(){if(!Ready)return;ResetTablePauseMenu();tableMenu=true;escapeDismissedInteraction=false;}
    public bool BlocksTableInput(Vector2 p)=>MenuVisible||TablePresentationBlocked||TableDiceRolling||ClosedTableSelection&&SelectionPromptArea.Contains(p);
    void ToggleTableCamera()
    {
        var b=FindFirstObjectByType<BoardRenderer>();if(b!=null)b.SetCamera(!b.TopView);
        var s=FindFirstObjectByType<M3BoardRenderer>();if(s!=null)s.SetCamera(!s.TopView);
        var c=FindFirstObjectByType<M4BoardRenderer>();if(c!=null)c.SetCamera(!c.TopView);
        var x=FindFirstObjectByType<M5BoardRenderer>();if(x!=null)x.SetCamera(!x.TopView);
    }
    Font font;
    GUIStyle label,title,button,small;
    static readonly string[] RuleIds={"base","seafarers","cities-knights","combined"};
    static readonly string[] RuleNames={"基础版","航海家","城市与骑士","双扩展组合"};
    static readonly string[] CombinedIds={"heading-for-new-shores","through-the-desert"};

    void Start()
    {
        Application.targetFrameRate=60;
        StartCoroutine(ApplyGameWindowTitle());
        font=MobileTabletop.LoadFont();
        InitializeTabletopAudio();
        gameObject.AddComponent<MenuAudioVerification>().Initialize(this);
        InitializeNetwork();
        gameObject.AddComponent<M7PlayerVerification>().Initialize(this);
        if(MobileTabletop.Enabled){Input.simulateMouseWithTouches=true;Screen.sleepTimeout=SleepTimeout.NeverSleep;}
        gameObject.AddComponent<M6PlayerVerification>().Initialize(this);
        gameObject.AddComponent<OperationUiVerification>();
        gameObject.AddComponent<TabletopUiVerification>();
        gameObject.AddComponent<CombinedMapVerification>();
        gameObject.AddComponent<TabletopV005Review>();
        gameObject.AddComponent<TabletopAiAnimationVerification>();
        if(GetComponent<InteractionPresentationVerification>()==null)gameObject.AddComponent<InteractionPresentationVerification>();
        gameObject.AddComponent<MobileVerification>().Initialize(this);
    }
    void Update()
    {
        using var frameCost=FrameCost.Measure(FrameCost.Stage.GameUpdate);
        UpdateCardPresentation();
        if(MobileTabletop.Enabled)_=MobileTabletop.TouchDragged;
        if(Input.GetMouseButtonDown(0)||Input.GetMouseButtonDown(1))escapeDismissedInteraction=false;
        if(Input.GetKeyDown(KeyCode.Escape))HandleTableEscape();
        if(NetworkGame||MenuVisible||TablePresentationBlocked||!Ready||Paused||TableDiceRolling||Time.unscaledTime<nextAction)return;
        nextAction=Time.unscaledTime+(float)TabletopAiSpeed.Interval(AiSpeedLevel);
        StepAi();
    }
    internal void HandleTableEscape()
    {
        if(MenuVisible){HandleMainMenuEscape();return;}
        if(!Ready)return;
        if(encyclopediaOpen){encyclopediaOpen=false;return;}
        if(tableMenu){if(HandleTablePausePageEscape())return;CloseTablePauseMenu();escapeDismissedInteraction=false;return;}
        // 第一遍放回本地草稿；规则要求的待决面板即使自动出现，下一遍仍能打开菜单。
        if(!escapeDismissedInteraction)
        {
            bool cancelled=presentation is M2App basic?basic.TryCancelTableInteraction():
                presentation is M3App sailing?sailing.TryCancelTableInteraction():
                presentation is M4App cities?cities.TryCancelTableInteraction():
                presentation is M5App combined&&combined.TryCancelTableInteraction();
            if(cancelled){escapeDismissedInteraction=true;return;}
        }
        OpenTableMenu();
    }
    // At most one decision is computed and submitted in each call/frame. No wait loop blocks input.
    public bool StepAi()
    {
        if(NetworkGame||MenuVisible||TablePresentationBlocked||TableDiceRolling||!Ready||Paused||Finished||IsHumanTurn)return false;
        string player=CurrentActor;
        try
        {
            string result=decideAndSubmit(player);
            if(result!=null){Fault=PlayerDisplayName(player)+" 的命令被拒绝（"+result+"）。局面保留，可保存后检查。";Paused=true;ShowTablePrompt("AI 已暂停，按 Esc 查看菜单");return false;}
            AiCommandsExecuted++;
            refresh();
            Notice=Finished?"本局结束。":IsHumanTurn?"轮到你完成行动或待决选择。":PlayerDisplayName(player)+" 已完成本次操作，AI 正在继续行动。";
            return true;
        }
        catch(Exception ex)
        {
            // Do not log command payloads, cards, saves, or exception messages containing private data.
            Fault=PlayerDisplayName(player)+" 决策失败（"+ex.GetType().Name+"）。";Paused=true;
            ShowTablePrompt("AI 已暂停，按 Esc 查看菜单");
            Debug.LogError("CATAN_M6_AI_PAUSED: "+ex.GetType().Name);return false;
        }
    }
    public void SetPaused(bool value){if(NetworkGame){Notice="联机 AI 由服务器调度；其他玩家断线时等待。";return;}Paused=value;if(!value)Fault="";nextAction=Time.unscaledTime+(float)TabletopAiSpeed.Interval(AiSpeedLevel);ShowTablePrompt(value?"AI 已暂停":"AI 已继续");}
    public void ShowTablePrompt(string value)=>tablePrompts.Show(Time.unscaledTimeAsDouble,value);
    internal string CurrentTablePrompt=>tablePrompts.At(Time.unscaledTimeAsDouble);
    internal bool HasRequiredTableSelection=>tablePrompts.RequiredSelection.Length>0;
    internal void UpdateTablePrompt(int turn,string phase,bool awaitingRoll,string instruction,string status,bool requiresSelection=false)
    {
        tablePrompts.RequireSelection(IsHumanTurn&&requiresSelection?instruction:"");
        bool roadPlacement=instruction.StartsWith("请在地图上点击合法位置新建道路 ",StringComparison.Ordinal);
        bool roadFinished=RoadPlacementPrompt.Length>0&&!roadPlacement;
        RoadPlacementPrompt=IsHumanTurn&&roadPlacement?instruction:"";
        string context=turn+":"+phase+":"+CurrentActor;
        if(context!=promptContext)
        {
            if(IsHumanTurn)
            {
                string next=awaitingRoll?"请掷骰":instruction;
                if(!promptWasHuman||awaitingRoll)tablePrompts.Show(Time.unscaledTimeAsDouble,"你的回合",next);
                else tablePrompts.Show(Time.unscaledTimeAsDouble,roadFinished?TabletopRoadPlacement.Instruction(0,HistoryEntries):next);
            }
            else if(Finished)ShowTablePrompt(instruction);
            else tablePrompts.Show(Time.unscaledTimeAsDouble);
        }
        else if(IsHumanTurn&&(status!=lastPromptStatus||instruction!=lastPromptInstruction))
            ShowTablePrompt(status!=lastPromptStatus?status:instruction);
        promptContext=context;promptWasHuman=IsHumanTurn;lastPromptStatus=status;lastPromptInstruction=instruction;
    }
    void Bind(MonoBehaviour app,Func<string> acting,Func<bool> done,Func<string,string> step,Func<string> export,Action update,Action dispose,Action<bool> show)
    {
        if(app!=presentation){dispose();return;}
        presentation=app;actor=acting;finished=done;decideAndSubmit=step;save=export;refresh=update;close=dispose;visible=show;
        InitialAuthority=null;refresh();nextAction=Time.unscaledTime+(float)TabletopAiSpeed.Interval(AiSpeedLevel);
    }
    public void StartGame(string rules,string scenario,int players,string human,bool paused=false,string authority=null,int? targetVictoryPoints=null,int? aiSpeedLevel=null)
    {
        ValidateConfiguration(rules,scenario,players,human);
        if(!networkEntering&&Network!=null&&(Network.Playing||Network.Busy||Network.RoomId!=null||Network.EnrollmentPending))Network.EndSession();
        int target=Catan.Core.VictoryPointSettings.Resolve(targetVictoryPoints,DefaultVictoryPoints(rules,scenario,players),MaximumVictoryPoints(rules,scenario));
        if(close!=null)close();
        else if(presentation!=null){presentation.enabled=false;Destroy(presentation);}
        actor=null;finished=null;decideAndSubmit=null;save=null;refresh=null;close=null;visible=null;
        RuleSet=rules;ScenarioId=scenario;PlayerCount=players;HumanSeat=human;Paused=paused;InitialAuthority=authority;
        ResetCardPresentation();ResetPlayerIdentity();
        TargetVictoryPoints=target;AiSpeedLevel=TabletopAiSpeed.Clamp(aiSpeedLevel??AiSpeedLevel);ResetBuildPresentation();
        networkMenu=false;menuPendingAction=null;tableMenu=false;encyclopediaOpen=false;escapeDismissedInteraction=false;Fault="";AiCommandsExecuted=0;LastCommandKind="";MenuVisible=false;Notice="AI 已就座。你使用 "+ColorLabel(human)+" 建筑。行动顺序由开局掷骰决定。";
        promptContext=null;promptWasHuman=false;RoadPlacementPrompt="";lastPromptStatus=lastPromptInstruction=null;TableDiceRolling=false;tablePrompts.RequireSelection("");tablePrompts.Show(Time.unscaledTimeAsDouble);
        switch(rules)
        {
            case "base":presentation=gameObject.AddComponent<M2App>();break;
            case "seafarers":presentation=gameObject.AddComponent<M3App>();break;
            case "cities-knights":presentation=gameObject.AddComponent<M4App>();break;
            default:presentation=gameObject.AddComponent<M5App>();break;
        }
    }
    static void ValidateConfiguration(string rules,string scenario,int players,string human)
    {
        if(!RuleIds.Contains(rules)||(players!=3&&players!=4)||!Enumerable.Range(1,players).Select(i=>"P"+i).Contains(human))throw new InvalidDataException("Invalid M6 seat configuration");
        if(rules!="seafarers"&&rules!="combined"&&scenario!="heading-for-new-shores")throw new InvalidDataException("Invalid M6 standard map");
        if(rules=="seafarers"&&!Catan.Core.M3.SeafarersScenarios.Ids.Contains(scenario)||rules=="combined"&&!CombinedIds.Contains(scenario))throw new InvalidDataException("Invalid M6 scenario");
    }
    public string ExportEnvelope()
    {
        if(NetworkGame)throw new InvalidOperationException("联机存档保存在服务器，客户端不能导出权威存档。");
        if(!Ready)throw new InvalidOperationException("No game to save");
        return JsonUtility.ToJson(new SavedGame{RuleSet=RuleSet,ScenarioId=ScenarioId,PlayerCount=PlayerCount,HumanSeat=HumanSeat,Paused=Paused,AiSpeedLevel=AiSpeedLevel,HasAiSpeed=true,PlayerNames=localPlayerNames.ToArray(),Authority=save()},true);
    }
    public void WriteSave(string path)
    {
        string payload=ExportEnvelope();Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        string temporary=path+".tmp";File.WriteAllText(temporary,payload);
        if(File.Exists(path))
        {
            if(Application.platform!=RuntimePlatform.Android)File.Replace(temporary,path,path+".bak");
            else
            {
                // Android 的 .NET 文件实现不保证 Replace；保留可恢复的上一份完整权威存档。
                File.Copy(path,path+".bak",true);
                try{File.Delete(path);File.Move(temporary,path);}
                catch{if(!File.Exists(path))File.Copy(path+".bak",path);throw;}
            }
        }
        else File.Move(temporary,path);
    }
    bool CanAutoSave => !NetworkGame&&Application.platform==RuntimePlatform.Android&&Ready&&!GetComponent<MobileVerification>().VerificationActive&&!Environment.GetCommandLineArgs().Any(arg=>arg.IndexOf("verify",StringComparison.OrdinalIgnoreCase)>=0);
    void OnApplicationPause(bool paused){if(paused&&CanAutoSave)SaveGame();}
    void OnApplicationQuit(){if(CanAutoSave)SaveGame();}
    public void SaveGame(){if(NetworkGame){Notice="每条已确认操作均由服务器自动保存；重启后使用原席位重连。";ShowTablePrompt("服务器自动保存");return;}try{WriteSave(SavePath);Notice="已保存权威局面、待决、随机状态、人类席位和 AI 暂停设置。";ShowTablePrompt("游戏已保存");}catch(Exception ex){Notice="保存失败（"+ex.GetType().Name+"），请检查存档目录。";ShowTablePrompt("保存失败");}}
    public void LoadGame()
    {
        if(NetworkGame){Network.Reconnect();return;}
        try
        {
            try{ReadSave(SavePath);}
            catch when(Application.platform==RuntimePlatform.Android&&File.Exists(SavePath+".bak"))
            {ReadSave(SavePath+".bak");Notice="已从上一次完整备份恢复游戏。";}
            ShowTablePrompt("游戏已恢复");
        }
        catch(Exception ex){Notice="恢复失败（"+ex.GetType().Name+"）：文件不存在、损坏或版本不兼容。当前局面保留。";ShowTablePrompt("存档无法读取");}
    }
    public void ReadSave(string path)
    {
        if(NetworkGame)throw new InvalidOperationException("联机模式只能恢复服务器房间。");
        var data=JsonUtility.FromJson<SavedGame>(File.ReadAllText(path));
        if(data==null||data.Version!=1||string.IsNullOrEmpty(data.Authority))throw new InvalidDataException("Unsupported M6 save");
        ValidateConfiguration(data.RuleSet,data.ScenarioId,data.PlayerCount,data.HumanSeat);
        ValidateAuthority(data); // Finish parsing before closing the current game.
        StartGame(data.RuleSet,data.ScenarioId,data.PlayerCount,data.HumanSeat,data.Paused,data.Authority,aiSpeedLevel:data.HasAiSpeed?data.AiSpeedLevel:1);
        if(data.PlayerNames!=null&&data.PlayerNames.Length==data.PlayerCount)localPlayerNames=data.PlayerNames.Select(PlayerIdentityModel.NormalizeName).ToArray();
        Notice="已恢复局面、玩家姓名和颜色配置。";
    }
    public void ShowMenu()
    {
        if(NetworkGame){OpenNetworkMenu();return;}
        MenuVisible=true;NavigateMainMenu(-1);if(presentation!=null)presentation.enabled=false;if(visible!=null)visible(false);
        menuRule=Array.IndexOf(RuleIds,RuleSet);menuPlayers=PlayerCount;menuHuman=int.Parse(HumanSeat.Substring(1))-1;
        var ids=ScenarioIds();menuScenario=Math.Max(0,Array.IndexOf(ids,ScenarioId));
        menuVictoryPoints=TargetVictoryPoints;menuAiSpeed=AiSpeedLevel;
    }
    public void SaveAndReturnToMainMenu()
    {
        try
        {
            if(!NetworkGame)WriteSave(MenuLocalSavePath);
            ReturnToMainMenu();Notice="已保存游戏并返回主页，可从加载游戏继续。";
        }
        catch(Exception ex){Notice="保存失败（"+ex.GetType().Name+"），当前对局保留。";ShowTablePrompt("保存失败");}
    }
    public void ReturnToMainMenu()
    {
        ResetCardPresentation();ResetBuildPresentation();
        if(NetworkGame)Network.Leave();
        close?.Invoke();presentation=null;actor=null;finished=null;decideAndSubmit=null;save=null;refresh=null;close=null;visible=null;
        historySource=null;historyEntries=Array.Empty<Catan.Core.GameHistoryEntry>();lastHistoryRevision=-1;
        tableMenu=false;MenuVisible=true;TableDiceRolling=false;NavigateMainMenu(-1);
    }
    internal void ResumeGame(){MenuVisible=false;presentation.enabled=true;visible(true);nextAction=Time.unscaledTime+(float)TabletopAiSpeed.Interval(AiSpeedLevel);}
    string[] ScenarioIds()=>menuRule==3?CombinedIds:menuRule==1?Catan.Core.M3.SeafarersScenarios.Ids:new[]{"heading-for-new-shores"};
    string ScenarioName(string id)=>Catan.Core.M3.SeafarersScenarios.Create(id,menuPlayers).Name;
    static int DefaultVictoryPoints(string rules,string scenario,int players)=>rules=="base"?10:rules=="cities-knights"?13:rules=="combined"?16:Catan.Core.M3.SeafarersScenarios.Create(scenario,players).TargetVictoryPoints;
    static int MaximumVictoryPoints(string rules,string scenario)=>rules=="seafarers"?Catan.Core.VictoryPointSettings.MaximumForScenario(scenario):Catan.Core.VictoryPointSettings.Maximum;
    void Styles()
    {
        if(label!=null)return;
        label=new GUIStyle(GUI.skin.label){font=font,fontSize=19,wordWrap=true};label.normal.textColor=new Color(.91f,.93f,.85f);
        title=new GUIStyle(label){fontSize=30,fontStyle=FontStyle.Bold};small=new GUIStyle(label){fontSize=15};button=new GUIStyle(GUI.skin.button){font=font,fontSize=18,wordWrap=true};
    }
    static void Panel(Rect rect,Color color){var old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
    void OnGUI()
    {
        using var frameCost=FrameCost.Measure(FrameCost.Stage.Gui);
        Styles();GUI.depth=-100;GUI.matrix=MobileTabletop.GuiMatrix;
        if(encyclopediaOpen&&tableMenu){DrawEncyclopedia();GUI.matrix=Matrix4x4.identity;return;}
        if(networkMenu){DrawNetworkMenu();GUI.matrix=Matrix4x4.identity;return;}
        if(MenuVisible)
        {
            DrawPastoralMainMenu();
        }
        else
        {
            if(tableMenu)
            {
                DrawTablePauseMenu();
            }
            if(!tableMenu&&NetworkGame&&!Network.CanInteract)GUI.Label(new Rect(300,82,840,56),DisplayPlayerText(Network.Status),small);
            DrawOpeningOrder();
            string prompt=DisplayPlayerText(RoadPlacementPrompt.Length>0?RoadPlacementPrompt:CurrentTablePrompt);
            if(prompt.Length>0&&!TablePresentationBlocked&&!TableMapChoiceOpen&&(!TableOverlayOpen||HasRequiredTableSelection))
            {
                bool primary=prompt=="你的回合"||Finished;
                var promptStyle=new GUIStyle(title){fontSize=primary?46:26,alignment=TextAnchor.MiddleCenter,wordWrap=true};
                promptStyle.normal.textColor=PromptColor;
                if(ClosedTableSelection)prompt+=" · 点击继续选择";
                var area=primary?new Rect(180,375,1080,150):SelectionPromptArea;
                area.height=Mathf.Max(area.height,promptStyle.CalcHeight(new GUIContent(prompt),area.width));
                if(!primary&&OpeningOrderMessage.Length>0)area.y+=48;
                bool before=GUI.enabled;GUI.enabled=true;
                var shadow=new GUIStyle(promptStyle);shadow.normal.textColor=new Color(.10f,.025f,.01f,.55f);
                GUI.Label(new Rect(area.x+1,area.y+2,area.width,area.height),prompt,shadow);
                GUI.Label(area,prompt,promptStyle);
                if(ClosedTableSelection&&GUI.Button(area,GUIContent.none,GUIStyle.none))CurrentTableWindows.Open();
                GUI.enabled=before;
            }
        }
        if(!MenuVisible&&!tableMenu)DrawPlayedCard();
        GUI.matrix=Matrix4x4.identity;
    }
}
