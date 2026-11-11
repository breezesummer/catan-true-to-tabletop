using System;
using UnityEngine;

// 桌内菜单沿用统一铜木界面；菜单阻挡本地调度，不改写存档中的暂停设置。
public sealed partial class M6App
{
    int tablePausePage,tablePauseSettingsTab=2;
    string tablePauseFeedback="";
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
        tablePausePage=0;tablePauseFeedback="";encyclopediaOpen=false;
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
        var panel=new Rect(440,100,560,700);
        TablePauseHeading(panel,"游戏菜单");
        string[] entries={"继续游戏","保存游戏","游戏设置","游戏百科","重新开始","返回主菜单"};
        for(int i=0;i<entries.Length;i++)
        {
            bool restart=NetworkGame&&i==4;
            if(!PastoralMenuUI.Button(new Rect(526,302+i*62,388,58),entries[i],!restart||Network.CanRestartRoom,true))continue;
            switch(i)
            {
                case 0:CloseTablePauseMenu(true);break;
                case 1:SaveGame();tablePauseFeedback=Notice;break;
                case 2:menuPlayerName=LocalPlayerName;tablePausePage=1;tablePauseFeedback="";GUI.FocusControl(null);break;
                case 3:encyclopediaOpen=true;break;
                case 4:if(NetworkGame){Network.RestartRoom();tablePauseFeedback="正在重新生成地图与行动顺序…";}else RestartLocalGame();break;
                case 5:ReturnToMainMenu();break;
            }
        }
        string status=NetworkGame&&!string.IsNullOrEmpty(Network.LastError)?Network.LastError:tablePauseFeedback.Length>0?tablePauseFeedback:NetworkGame?"联机对局进行中":Fault.Length>0?"AI 已暂停："+Fault:"游戏已暂停";
        PastoralMenuUI.Label(new Rect(477,704,486,68),status,13,new Color(.39f,.27f,.17f),false,TextAnchor.MiddleCenter);
    }
    void DrawTablePauseSettings()
    {
        var matrix=GUI.matrix;
        GUI.matrix=matrix*Matrix4x4.Scale(new Vector3(1440f/1672,900f/941,1));
        try{AcceptedSettings(ref tablePauseSettingsTab,true);}
        finally{GUI.matrix=matrix;}
    }
    static readonly string[] boardArtNames={"2D 绘图","3D 模型"};

    internal void SelectBoardArt(bool painted)
    {
        BoardArtPreference.Select(painted);
        // 仅重绘公开的沿岸摆件，保留手中的棋子和正在进行的操作。
        switch(presentation)
        {
            case M2App basic when basic.M6View!=null: basic.VerificationBoard.RefreshReferenceMarkers(basic.M6View);break;
            case M3App sailing when sailing.M6View!=null:
                sailing.VerificationBoard.RefreshReferenceMarkers(sailing.M6View);
                sailing.VerificationBoard.RefreshSeafarers(sailing.M6View.Board.Ships,sailing.M6View.Board.PirateTileId);break;
            case M4App cities when cities.M6View!=null: cities.VerificationBoard.RefreshReferenceMarkers(cities.M6View);break;
            case M5App combined when combined.M6View!=null:
                combined.VerificationBoard.RefreshReferenceMarkers(combined.M6View);
                combined.VerificationBoard.RefreshSeafarers(combined.M6View.Board.Ships,combined.M6View.Board.PirateTileId);break;
        }
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
