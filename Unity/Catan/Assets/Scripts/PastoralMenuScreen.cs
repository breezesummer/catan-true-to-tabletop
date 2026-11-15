using System;
using System.IO;
using System.Linq;
using UnityEngine;

public sealed partial class M6App
{
    // -1 为纯风景主页，五个入口分别拥有独立内容；开局配置只在三个步骤内共享。
    int menuPage=-1,menuSettingsTab=1;
    Font pastoralMenuFont;
    Texture2D collectorTitle;
    readonly float[] menuNavBlend=new float[5];
    float menuPageAt,menuNavAt;
    Action menuPendingAction;
    bool menuPendingAllowSave;
    string menuPendingCaption,menuInlineMessage="";
    string menuPlayerName;
    string menuPreviewPath;
    SavePreview recentSave,earlierSave;
    float nextSavePreviewCheck;
    long menuPreviewStamp=-1,menuPreviewBackupStamp=-1;
    internal string MenuReviewSavePath;
    string MenuLocalSavePath=>MenuReviewSavePath??SavePath;
    static readonly string[] MenuEntries={"开始游戏","加载游戏","局域网联机","在线游戏","设置"};

    void NavigateMainMenu(int page)
    {
        atlasOpen=false;seatOptionsOpen=false;GUI.FocusControl(null);menuInlineMessage="";menuPageAt=Time.unscaledTime;
        if(page==2||page==3){OpenSeparatedNetworkMenu(page==2);return;}
        networkMenu=false;menuPage=page;
        if(page==1){backupExpanded=false;nextSavePreviewCheck=0;}
    }
    void HandleMainMenuEscape()
    {
        if(pendingJoinRoom!=null){CancelJoinPassword();return;}
        if(menuPendingAction!=null){menuPendingAction=null;return;}
        if(networkMenu||menuPage>=0){MenuBack();return;}
        if(Ready)ResumeGame();
    }
    void DrawPastoralShell(int selectedPage)
    {
        if(pastoralMenuFont==null)pastoralMenuFont=Resources.Load<Font>("Fonts/NotoSerifCJKsc-Regular");
        PastoralMenuUI.Styles(pastoralMenuFont??font);PastoralMenuScene.Draw(Time.unscaledTime);
        bool before=GUI.enabled;GUI.enabled=before&&menuPendingAction==null;
        if(collectorTitle==null)collectorTitle=Resources.Load<Texture2D>("PastoralMenu/v004/title");
        // 单独的透明标题资产，按物理像素比例显示；菜单与命中区域完全独立于背景。
        var logo=new Rect(45,70,365,255);
        if(collectorTitle!=null)
        {
            float h=logo.width*Mathf.Abs(GUI.matrix.m00/GUI.matrix.m11)*collectorTitle.height/collectorTitle.width;
            GUI.DrawTexture(new Rect(logo.x,logo.center.y-h*.5f,logo.width,h),collectorTitle);
        }
        else PastoralMenuUI.Label(logo,"卡坦岛",67,PastoralMenuUI.Ivory,true);
        if(GUI.Button(logo,GUIContent.none,GUIStyle.none))NavigateMainMenu(-1);
        float delta=Mathf.Min(.05f,Time.unscaledTime-menuNavAt);
        for(int i=0;i<5;i++)
        {
            var r=new Rect(57,356+i*70,330,64);
            if(Event.current.type==EventType.Repaint)menuNavBlend[i]=Mathf.MoveTowards(menuNavBlend[i],r.Contains(Event.current.mousePosition)?1:0,delta*7);
            if(PastoralMenuUI.Nav(r,MenuEntries[i],selectedPage==i||(selectedPage<0&&i==0),menuNavBlend[i]))NavigateMainMenu(i);
        }
        if(Event.current.type==EventType.Repaint)menuNavAt=Time.unscaledTime;
        if(Ready&&!NetworkGame&&PastoralMenuUI.Button(new Rect(79,762,290,42),"继续当前对局   →",homeStyle:true)){networkMenu=false;ResumeGame();}
        PastoralMenuUI.Label(new Rect(27,849,340,25),ReleaseVersion,12,new Color(.76f,.69f,.54f));
        GUI.enabled=before;
    }
    void DrawPastoralMainMenu()
    {
        if(menuPage<0){DrawPastoralShell(-1);DrawMenuConfirmation();return;}
        AcceptedPage(false);
    }
    void StartConfiguredGame()
    {
        var ids=ScenarioIds();
        StartGame(RuleIds[menuRule],ids[Mathf.Clamp(menuScenario,0,ids.Length-1)],menuPlayers,"P"+(Mathf.Clamp(menuHuman,0,menuPlayers-1)+1),targetVictoryPoints:menuVictoryPoints,aiSpeedLevel:menuAiSpeed,threeKingdoms:menuRule>=2&&menuThreeKingdoms,harbormaster:menuHarbormaster&&Catan.Core.TradeModules.HarbormasterRules.Supports(ids[menuScenario]),fishing:menuFishing&&(menuRule==0||menuRule==2||Catan.Core.TradeModules.FishingScenarioCatalog.SupportsSeafaring(ids[menuScenario])));
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
        if(canSave&&PastoralMenuUI.Button(new Rect(496,455,472,44),"保存当前局并继续",true,true,homeStyle:true))
        {
            try{WriteSave(MenuLocalSavePath);var next=menuPendingAction;menuPendingAction=null;next();}
            catch(Exception){menuInlineMessage="保存失败，当前对局已保留。";menuPendingAction=null;}
        }
        if(PastoralMenuUI.Button(new Rect(496,canSave?514:466,230,40),NetworkGame?"离开并继续":"直接继续",homeStyle:true))
        {var next=menuPendingAction;menuPendingAction=null;next();}
        if(PastoralMenuUI.Button(new Rect(738,canSave?514:466,230,40),"留在当前对局",homeStyle:true))menuPendingAction=null;
    }
    void RefreshMenuSavePreview()
    {
        if(Time.unscaledTime<nextSavePreviewCheck)return;
        nextSavePreviewCheck=Time.unscaledTime+.5f;
        string path=MenuLocalSavePath,backup=path+".bak";
        long stamp=File.Exists(path)?File.GetLastWriteTimeUtc(path).Ticks:0;
        long backupStamp=File.Exists(backup)?File.GetLastWriteTimeUtc(backup).Ticks:0;
        if(path!=menuPreviewPath||stamp!=menuPreviewStamp)recentSave=ReadSavePreview(path,stamp);
        if(path!=menuPreviewPath||backupStamp!=menuPreviewBackupStamp)earlierSave=ReadSavePreview(backup,backupStamp);
        menuPreviewPath=path;menuPreviewStamp=stamp;menuPreviewBackupStamp=backupStamp;
    }
    sealed class SavePreview{
        public string Map,Artwork,Timestamp;
        public int Players,Seat;
        public bool[] Modes;
    }
    // 只投影权威存档里的模块标记，兼容原有存档封装。
    [Serializable] sealed class PreviewAuthority{public PreviewTrade TradeModules;}
    [Serializable] sealed class PreviewTrade{public bool Harbormaster;public PreviewFishing Fishing;}
    [Serializable] sealed class PreviewFishing{public string Version;}
    SavePreview ReadSavePreview(string path,long stamp){
        if(stamp==0)return null;
        try{
            var data=JsonUtility.FromJson<SavedGame>(File.ReadAllText(path));
            if(data==null||data.Version!=1||string.IsNullOrEmpty(data.Authority))throw new InvalidDataException();
            ValidateConfiguration(data.RuleSet,data.ScenarioId,data.PlayerCount,data.HumanSeat);
            bool sea=data.RuleSet=="seafarers"||data.RuleSet=="combined";
            var trade=JsonUtility.FromJson<PreviewAuthority>(data.Authority)?.TradeModules;
            return new SavePreview{
                Map=sea?MapTitle(data.ScenarioId):"标准岛屿",
                Artwork=sea?"atlas-"+data.ScenarioId+"-"+data.PlayerCount:"map-标准岛屿",
                Players=data.PlayerCount,Seat=data.HumanSeat[1]-'1',
                Timestamp="保存于 "+new DateTime(stamp,DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd  HH:mm"),
                Modes=new[]{true,sea,data.RuleSet=="cities-knights"||data.RuleSet=="combined",data.ThreeKingdoms,
                    trade!=null&&(trade.Harbormaster||!string.IsNullOrEmpty(trade.Fishing?.Version))}
            };
        }catch{return new SavePreview{Map="无法读取这份存档"};}
    }
}
