using System;
using UnityEngine;

// 桌内菜单沿用统一铜木界面；菜单阻挡本地调度，不改写存档中的暂停设置。
public sealed partial class M6App
{
    int tablePausePage,tablePauseSettingsTab=1;
    string tablePauseFeedback="";
    float tablePauseFeedbackUntil;
    static readonly string[] TablePauseEntries={"继续游戏","保存游戏","游戏设置","游戏百科","重新开始","返回主菜单"};
    static readonly Rect[] TablePauseButtons={new Rect(610,219,447,97),new Rect(626,334,416,64),
        new Rect(626,413,416,64),new Rect(626,492,416,64),new Rect(626,617,416,64),new Rect(626,697,416,64)};
    TabletopScene settingsCamera;
    bool pitchEdited;
    void SaveCameraSettings()
    {
        if(!pitchEdited)return;
        PlayerPrefs.Save();
        pitchEdited=false;
    }
    internal bool TablePauseSettingsOpen=>tableMenu&&tablePausePage==1;

    void ResetTablePauseMenu()
    {
        tablePausePage=0;tablePauseFeedback="";tablePauseFeedbackUntil=0;encyclopediaOpen=false;
    }
    bool HandleTablePausePageEscape()
    {
        if(tablePausePage==0)return false;
        tablePausePage=0;SaveCameraSettings();TabletopAudio.Instance?.FlushSettings();return true;
    }
    void CloseTablePauseMenu(bool resumeAi=false)
    {
        tableMenu=false;encyclopediaOpen=false;tablePausePage=0;escapeDismissedInteraction=false;
        SaveCameraSettings();
        // “继续游戏”可恢复人工暂停；故障仍保留，避免吞掉排查信息后反复执行失败指令。
        if(resumeAi&&!NetworkGame&&Fault.Length==0)Paused=false;
        nextAction=Time.unscaledTime+(float)TabletopAiSpeed.Interval(AiSpeedLevel);
        TabletopAudio.Instance?.FlushSettings();
    }
    void TablePauseStyles()
    {
        if(pastoralMenuFont==null)pastoralMenuFont=Resources.Load<Font>("Fonts/NotoSerifCJKsc-Regular");
        PastoralMenuUI.Styles(pastoralMenuFont??font);
    }
    void DrawTablePauseBackdrop()
    {
        TablePauseStyles();
        PastoralMenuUI.Fill(new Rect(0,0,1440,900),new Color(.055f,.025f,.01f,.69f));
    }
    void TablePauseHeading(Rect panel,string heading)
    {
        Sheet(panel);
        BandedHeading(new Rect(panel.x+20,panel.y+61,panel.width-40,48),heading,31);
    }
    void DrawTablePauseMenu()
    {
        DrawTablePauseBackdrop();
        if(tablePausePage==1){DrawTablePauseSettings();return;}
        int clicked=-1;
        var matrix=GUI.matrix;
        GUI.matrix=matrix*Matrix4x4.Translate(new Vector3(720,450,0))*Matrix4x4.Scale(new Vector3(2f/3,2f/3,1))*Matrix4x4.Translate(new Vector3(-720,-450,0))*Matrix4x4.Scale(new Vector3(1440f/1672,900f/941,1));
        try{
            Picture(new Rect(555,98,554,747),"setup-pause-sheet");
            PauseMenuRule(598,155,96);PauseMenuRule(975,155,95);
            Caption(new Rect(710,117,249,66),"游戏菜单",48);
            PauseMenuRule(626,586,416);
            for(int i=0;i<TablePauseEntries.Length;i++){
                string text=i==1&&Time.unscaledTime<tablePauseFeedbackUntil?tablePauseFeedback:TablePauseEntries[i];
                if(PauseMenuButton(TablePauseButtons[i],text,i==0,TablePauseEntryEnabled(i)))clicked=i;
            }
        }finally{GUI.matrix=matrix;}
        if(clicked>=0)ActivateTablePauseEntry(clicked);
    }
    void PauseMenuRule(float x,float y,float width){
        PastoralMenuUI.Fill(new Rect(x,y,width,1),new Color32(157,99,44,220));
        PastoralMenuUI.Fill(new Rect(x,y+1,width,1),new Color32(255,238,197,130));
    }
    bool PauseMenuButton(Rect r,string text,bool resume,bool available){
        var tint=GUI.color;
        if(!available)GUI.color=new Color(.80f,.77f,.71f,tint.a);
        Picture(InsetMenuRect(r,resume?-12:-8),resume?"setup-pause-resume":"setup-pause-wood");
        GUI.color=tint;
        var lettering=new Rect(r.x+(resume?96:30),r.y-2,r.width-(resume?192:60),r.height);
        PlateLettering(lettering,text,resume?44:36,resume?PlateInk.Gold:PlateInk.Wood,strengthen:true);
        return available&&Hit(r,false);
    }
    bool TablePauseEntryEnabled(int index)=>index>=0&&index<TablePauseEntries.Length&&
        (index!=4||!NetworkGame||Network.CanRestartRoom);
    void ActivateTablePauseEntry(int index){
        if(!TablePauseEntryEnabled(index))return;
        switch(index){
            case 0:CloseTablePauseMenu(true);break;
            case 1:
                SaveGame(MenuLocalSavePath);
                tablePauseFeedback=Notice.StartsWith("保存失败",StringComparison.Ordinal)?"保存失败":NetworkGame?"自动保存":"已保存";
                tablePauseFeedbackUntil=Time.unscaledTime+2;
                break;
            case 2:menuPlayerName=LocalPlayerName;tablePausePage=1;tablePauseFeedback="";GUI.FocusControl(null);break;
            case 3:OpenBook();break;
            case 4:if(NetworkGame)Network.RestartRoom();else RestartLocalGame();break;
            case 5:ReturnToMainMenu();break;
        }
    }
    void DrawTablePauseSettings()
    {
        var matrix=GUI.matrix;
        GUI.matrix=matrix*Matrix4x4.Scale(new Vector3(1440f/1672,900f/941,1));
        try{AcceptedSettings(ref tablePauseSettingsTab,true);}
        finally{GUI.matrix=matrix;}
    }
    void DrawTablePauseView()
    {
        Caption(new Rect(203,328,289,56),"牌桌视角");
        bool top=TablePauseTopView;
        if(KeyButton(new Rect(505,328,303,56),"斜视",!top)&&top)ToggleTableCamera();
        if(KeyButton(new Rect(827,328,303,56),"俯视",top)&&!top)ToggleTableCamera();

        if(settingsCamera==null||!settingsCamera.isActiveAndEnabled)settingsCamera=FindFirstObjectByType<TabletopScene>();
        if(settingsCamera!=null){
            const int low=TabletopCameraModel.MinPitch,high=TabletopCameraModel.MaxPitch;
            float value=(settingsCamera.Pitch-low)/(float)(high-low);
            float next=SettingsSlider(456,"地图俯角",value,settingsCamera.Pitch+"°");
            int pitch=Mathf.RoundToInt(Mathf.Lerp(low,high,next));
            if(pitch!=settingsCamera.Pitch){settingsCamera.SetPitch(pitch);pitchEdited=true;}
        }
        Caption(new Rect(203,533,289,56),"AI 行动节奏");
        float step=900f/TabletopAiSpeed.Names.Length;
        for(int i=0;i<TabletopAiSpeed.Names.Length;i++){
            if(KeyButton(new Rect(505+i*step,533,step-12,56),TabletopAiSpeed.Names[i],AiSpeedLevel==i,!NetworkGame)&&AiSpeedLevel!=i){
                AiSpeedLevel=TabletopAiSpeed.Clamp(i);
                nextAction=Time.unscaledTime+(float)TabletopAiSpeed.Interval(AiSpeedLevel);
            }
        }
        if(RuleSet=="seafarers"&&KeyButton(new Rect(616,651,398,52),"航海场景行动")){
            CloseTablePauseMenu();GetComponent<M3App>()?.ShowScenarioActions();
        }
    }
    bool TablePauseTopView
    {
        get
        {
            var b=FindFirstObjectByType<BoardRenderer>();if(b!=null)return b.TopView;
            var s=FindFirstObjectByType<M3BoardRenderer>();if(s!=null)return s.TopView;
            var c=FindFirstObjectByType<M4BoardRenderer>();if(c!=null)return c.TopView;
            var x=FindFirstObjectByType<M5BoardRenderer>();return x!=null&&x.TopView;
        }
    }
}
