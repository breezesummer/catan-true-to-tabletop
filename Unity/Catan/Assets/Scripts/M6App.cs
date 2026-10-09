using System;
using System.IO;
using System.Linq;
using UnityEngine;

// M6 owns local seat configuration and scheduling; rule authority remains in the existing hosts.
// A policy is passed only the acting seat's detached PlayerView, never a host or save payload.
public sealed partial class M6App : MonoBehaviour
{
    [Serializable]
    public sealed class SavedGame
    {
        public int Version=1;
        public string RuleSet,ScenarioId,HumanSeat,Authority;
        public int PlayerCount;
        public bool Paused;
    }

    public string RuleSet { get; private set; }="base";
    public string ScenarioId { get; private set; }="heading-for-new-shores";
    public int PlayerCount { get; private set; }=3;
    public string HumanSeat { get; private set; }="P1";
    public string InitialAuthority { get; private set; }
    public bool Paused { get; private set; }
    public string Fault { get; private set; }="";
    public string Notice { get; private set; }="选择一种规则和你的席位，其他席位由本地 AI 执行。";
    public int AiCommandsExecuted { get; private set; }
    public string LastCommandKind { get; private set; }="";
    public bool MenuVisible { get; private set; }=true;
    public bool Ready=>actor!=null;
    public bool Finished=>Ready&&finished();
    public string CurrentActor=>Ready?actor():"";
    public bool IsHumanTurn=>Ready&&!Finished&&CurrentActor==HumanSeat;
    public string WaitingText=>Fault!=""?"AI 已暂停："+Fault:Paused?"AI 已暂停。点击左上角继续。":Finished?"本局已结束。":"等待 "+CurrentActor+" · AI 正在行动。";
    public string SavePath=>Path.Combine(Application.persistentDataPath,"m6-local-ai-v1.json");

    Func<string> actor,save;
    Func<bool> finished;
    Func<string,string> decideAndSubmit;
    Action refresh,close;
    Action<bool> visible;
    MonoBehaviour presentation;
    float nextAction;
    int menuRule,menuScenario,menuPlayers=3,menuHuman;
    Font font;
    GUIStyle label,title,button,small;
    static readonly string[] RuleIds={"base","seafarers","cities-knights","combined"};
    static readonly string[] RuleNames={"基础版","航海家","城市与骑士","双扩展组合"};
    static readonly string[] CombinedIds={"heading-for-new-shores","through-the-desert"};

    void Start()
    {
        Application.targetFrameRate=60;
        font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},18);
        gameObject.AddComponent<M6PlayerVerification>().Initialize(this);
    }
    void Update()
    {
        if(MenuVisible||!Ready||Paused||Time.unscaledTime<nextAction)return;
        nextAction=Time.unscaledTime+.60f;
        StepAi();
    }
    // At most one decision is computed and submitted in each call/frame. No wait loop blocks input.
    public bool StepAi()
    {
        if(MenuVisible||!Ready||Paused||Finished||IsHumanTurn)return false;
        string player=CurrentActor;
        try
        {
            string result=decideAndSubmit(player);
            if(result!=null){Fault=player+" 的命令被拒绝（"+result+"）。局面保留，可保存后检查。";Paused=true;return false;}
            AiCommandsExecuted++;
            refresh();
            Notice=Finished?"本局结束。":IsHumanTurn?"轮到你完成行动或待决选择。":player+" 已完成本次操作，AI 正在继续行动。";
            return true;
        }
        catch(Exception ex)
        {
            // Do not log command payloads, cards, saves, or exception messages containing private data.
            Fault=player+" 决策失败（"+ex.GetType().Name+"）。";Paused=true;
            Debug.LogError("CATAN_M6_AI_PAUSED: "+ex.GetType().Name);return false;
        }
    }
    public void SetPaused(bool value){Paused=value;if(!value)Fault="";nextAction=Time.unscaledTime+.30f;}
    void Bind(MonoBehaviour app,Func<string> acting,Func<bool> done,Func<string,string> step,Func<string> export,Action update,Action dispose,Action<bool> show)
    {
        if(app!=presentation){dispose();return;}
        presentation=app;actor=acting;finished=done;decideAndSubmit=step;save=export;refresh=update;close=dispose;visible=show;
        InitialAuthority=null;refresh();nextAction=Time.unscaledTime+.30f;
    }
    public void StartGame(string rules,string scenario,int players,string human,bool paused=false,string authority=null)
    {
        ValidateConfiguration(rules,scenario,players,human);
        if(close!=null)close();
        else if(presentation!=null){presentation.enabled=false;Destroy(presentation);}
        actor=null;finished=null;decideAndSubmit=null;save=null;refresh=null;close=null;visible=null;
        RuleSet=rules;ScenarioId=scenario;PlayerCount=players;HumanSeat=human;Paused=paused;InitialAuthority=authority;
        Fault="";AiCommandsExecuted=0;LastCommandKind="";MenuVisible=false;Notice="AI 已就座。你的手牌始终只显示 "+human+"。";
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
        if(!Ready)throw new InvalidOperationException("No game to save");
        return JsonUtility.ToJson(new SavedGame{RuleSet=RuleSet,ScenarioId=ScenarioId,PlayerCount=PlayerCount,HumanSeat=HumanSeat,Paused=Paused,Authority=save()},true);
    }
    public void WriteSave(string path)
    {
        string payload=ExportEnvelope();Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        string temporary=path+".tmp";File.WriteAllText(temporary,payload);
        if(File.Exists(path))File.Replace(temporary,path,path+".bak");else File.Move(temporary,path);
    }
    public void SaveGame(){try{WriteSave(SavePath);Notice="已保存权威局面、待决、随机状态、人类席位和 AI 暂停设置。";}catch(Exception ex){Notice="保存失败（"+ex.GetType().Name+"），请检查存档目录。";}}
    public void LoadGame(){try{ReadSave(SavePath);}catch(Exception ex){Notice="恢复失败（"+ex.GetType().Name+"）：文件不存在、损坏或版本不兼容。当前局面保留。";}}
    public void ReadSave(string path)
    {
        var data=JsonUtility.FromJson<SavedGame>(File.ReadAllText(path));
        if(data==null||data.Version!=1||string.IsNullOrEmpty(data.Authority))throw new InvalidDataException("Unsupported M6 save");
        ValidateConfiguration(data.RuleSet,data.ScenarioId,data.PlayerCount,data.HumanSeat);
        ValidateAuthority(data); // Finish parsing before closing the current game.
        StartGame(data.RuleSet,data.ScenarioId,data.PlayerCount,data.HumanSeat,data.Paused,data.Authority);
        Notice="已恢复局面和席位配置。";
    }
    public void ShowMenu()
    {
        MenuVisible=true;if(presentation!=null)presentation.enabled=false;if(visible!=null)visible(false);
        menuRule=Array.IndexOf(RuleIds,RuleSet);menuPlayers=PlayerCount;menuHuman=int.Parse(HumanSeat.Substring(1))-1;
        var ids=ScenarioIds();menuScenario=Math.Max(0,Array.IndexOf(ids,ScenarioId));
    }
    internal void ResumeGame(){MenuVisible=false;presentation.enabled=true;visible(true);nextAction=Time.unscaledTime+.60f;}
    string[] ScenarioIds()=>menuRule==3?CombinedIds:menuRule==1?Catan.Core.M3.SeafarersScenarios.Ids:new[]{"heading-for-new-shores"};
    string ScenarioName(string id)=>Catan.Core.M3.SeafarersScenarios.Create(id,menuPlayers).Name;
    void Styles()
    {
        if(label!=null)return;
        label=new GUIStyle(GUI.skin.label){font=font,fontSize=19,wordWrap=true};label.normal.textColor=new Color(.91f,.93f,.85f);
        title=new GUIStyle(label){fontSize=30,fontStyle=FontStyle.Bold};small=new GUIStyle(label){fontSize=15};button=new GUIStyle(GUI.skin.button){font=font,fontSize=18,wordWrap=true};
    }
    static void Panel(Rect rect,Color color){var old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
    void OnGUI()
    {
        Styles();GUI.depth=-100;GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1440f,Screen.height/900f,1));
        if(MenuVisible)
        {
            Panel(new Rect(0,0,1440,900),new Color(.025f,.07f,.085f));
            GUILayout.BeginArea(new Rect(280,95,880,730));GUILayout.Label("CATAN  /  本地 AI 对手",title);
            GUILayout.Space(15);GUILayout.Label("一个本地人类席位 · 其余席位由离线启发式 AI 操作",label);GUILayout.Space(26);
            int selection=GUILayout.Toolbar(menuRule,RuleNames,button,GUILayout.Height(48));if(selection!=menuRule){menuRule=selection;menuScenario=0;}
            GUILayout.Space(20);var ids=ScenarioIds();menuScenario=Mathf.Clamp(menuScenario,0,ids.Length-1);
            if(menuRule==1||menuRule==3)
            {
                GUILayout.BeginHorizontal();if(GUILayout.Button("‹",button,GUILayout.Width(60),GUILayout.Height(44)))menuScenario=(menuScenario+ids.Length-1)%ids.Length;
                GUILayout.Label("剧本："+ScenarioName(ids[menuScenario])+"  ("+(menuScenario+1)+" / "+ids.Length+")",label,GUILayout.Height(44));
                if(GUILayout.Button("›",button,GUILayout.Width(60),GUILayout.Height(44)))menuScenario=(menuScenario+1)%ids.Length;GUILayout.EndHorizontal();
            }
            else GUILayout.Label(menuRule==0?"标准基础地图 · 10 分获胜":"标准基础地图 · 13 分获胜",label,GUILayout.Height(44));
            GUILayout.Space(16);menuPlayers=GUILayout.Toolbar(menuPlayers-3,new[]{"3 人：你 + 2 个 AI","4 人：你 + 3 个 AI"},button,GUILayout.Height(44))+3;
            menuHuman=Mathf.Clamp(menuHuman,0,menuPlayers-1);GUILayout.Space(20);GUILayout.Label("你的席位",label);
            menuHuman=GUILayout.Toolbar(menuHuman,Enumerable.Range(1,menuPlayers).Select(i=>"P"+i).ToArray(),button,GUILayout.Height(42));
            GUILayout.Space(22);GUILayout.Label("AI 只读取自己的玩家视图。轮到你的弃牌、交易或其他选择时会等待；可随时暂停 AI、保存并恢复。当前策略仍有明显取舍，供试玩迭代。",small);
            GUILayout.Space(22);if(GUILayout.Button(Ready?"开始新局（替换当前未保存局面）":"开始对局",button,GUILayout.Height(50)))StartGame(RuleIds[menuRule],ids[menuScenario],menuPlayers,"P"+(menuHuman+1));
            GUILayout.BeginHorizontal();if(GUILayout.Button("读取 M6 存档",button,GUILayout.Height(42)))LoadGame();if(Ready&&GUILayout.Button("返回当前局",button,GUILayout.Height(42)))ResumeGame();GUILayout.EndHorizontal();
            GUILayout.Space(18);GUILayout.Label(Notice,small);GUILayout.EndArea();
        }
        else
        {
            Panel(new Rect(0,0,770,87),new Color(.045f,.09f,.10f));
            GUI.Label(new Rect(25,9,440,36),"M6  /  "+RuleNames[Array.IndexOf(RuleIds,RuleSet)],title);
            GUI.Label(new Rect(25,53,455,30),"你："+HumanSeat+"  ·  "+(PlayerCount-1)+" 个本地 AI  ·  "+(IsHumanTurn?"等待你的操作":Paused?"AI 已暂停":Finished?"对局结束":"AI："+CurrentActor),small);
            if(GUI.Button(new Rect(491,23,126,40),Paused?"继续 AI":"暂停 AI",button))SetPaused(!Paused);
            if(GUI.Button(new Rect(631,23,115,40),"规则菜单",button))ShowMenu();
            Panel(new Rect(18,747,975,56),new Color(.045f,.09f,.10f,.94f));
            GUI.Label(new Rect(29,754,950,48),Fault!=""?"AI 已暂停："+Fault:Notice,small);
        }
        GUI.matrix=Matrix4x4.identity;
    }
}
