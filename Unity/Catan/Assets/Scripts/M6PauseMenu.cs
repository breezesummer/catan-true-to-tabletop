using System;
using UnityEngine;

// 桌内菜单沿用统一铜木界面；菜单阻挡本地调度，不改写存档中的暂停设置。
public sealed partial class M6App
{
    int tablePausePage,tablePauseSettingsTab;
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
        if(pastoralMenuFont==null)pastoralMenuFont=Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
        PastoralMenuUI.Styles(pastoralMenuFont??font);
    }
    void DrawTablePauseBackdrop()
    {
        TablePauseStyles();
        PastoralMenuUI.Fill(new Rect(0,0,1440,900),new Color(.055f,.025f,.01f,.69f));
    }
    void TablePauseHeading(Rect panel,string heading)
    {
        PastoralMenuUI.Panel(panel);
        PastoralMenuUI.Label(new Rect(panel.x+28,panel.y+61,panel.width-56,48),heading,31,PastoralMenuUI.Ink,true,TextAnchor.MiddleCenter);
        PastoralMenuUI.Fill(new Rect(panel.center.x-32,panel.y+124,64,2),PastoralMenuUI.Gold);
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
                case 2:tablePausePage=1;tablePauseFeedback="";GUI.FocusControl(null);break;
                case 3:encyclopediaOpen=true;break;
                case 4:if(NetworkGame){Network.RestartRoom();tablePauseFeedback="正在重新生成地图与行动顺序…";}else RestartLocalGame();break;
                case 5:ReturnToMainMenu();break;
            }
        }
        string status=NetworkGame&&!string.IsNullOrEmpty(Network.LastError)?Network.LastError:tablePauseFeedback.Length>0?tablePauseFeedback:NetworkGame?"联机对局进行中":Fault.Length>0?"AI 已暂停："+Fault:"游戏已暂停";
        PastoralMenuUI.Label(new Rect(477,704,486,68),status,13,PastoralMenuUI.Muted,false,TextAnchor.MiddleCenter);
    }
    void DrawTablePauseSettings()
    {
        var panel=new Rect(290,69,860,762);
        TablePauseHeading(panel,"游戏设置");
        tablePauseSettingsTab=PastoralMenuUI.Segments(new Rect(337,272,766,42),tablePauseSettingsTab,new[]{"桌面外观","声音","视角与节奏"});
        if(tablePauseSettingsTab==0)DrawTablePauseAppearance();
        else if(tablePauseSettingsTab==1)DrawTablePauseAudio();
        else DrawTablePauseView();
        PastoralMenuUI.Fill(new Rect(337,728,766,1),new Color(.48f,.54f,.42f,.18f));
        if(PastoralMenuUI.Button(new Rect(526,754,388,48),"返回游戏菜单",true,true))HandleTablePausePageEscape();
    }
    void DrawTablePauseAppearance()
    {
        for(int i=0;i<StrategyHudTheme.Names.Length;i++)
        {
            float step=(766f+12)/StrategyHudTheme.Names.Length;
            var r=new Rect(337+i*step,385,step-12,180);bool chosen=StrategyHudTheme.Selected==i;
            if(PastoralMenuUI.Button(r,"",true,false,chosen))StrategyHudTheme.Select(i);
            if(i==2)JadeTabletopSkin.Panel(new Rect(r.x+20,r.y+21,r.width-40,70),StrategyHudTheme.Surface,false,true);
            else if(i==3)HandPaintedTabletopSkin.Preview(new Rect(r.x+20,r.y+21,r.width-40,70));
            else if(i==1)OceanTabletopSkin.Panel(new Rect(r.x+20,r.y+21,r.width-40,70));
            else PastoralMenuUI.Round(new Rect(r.x+20,r.y+21,r.width-40,70),new Color(.34f,.145f,.065f),8);
            PastoralMenuUI.Label(new Rect(r.x+14,r.y+113,r.width-28,36),StrategyHudTheme.Names[i],21,chosen?PastoralMenuUI.Ivory:PastoralMenuUI.Ink,true,TextAnchor.MiddleCenter);
        }
    }
    void DrawTablePauseAudio()
    {
        var audio=TabletopAudio.Instance;
        if(audio==null){PastoralMenuUI.Label(new Rect(337,390,766,35),"声音正在准备中…",18,null,false,TextAnchor.MiddleCenter);return;}
        if(PastoralMenuUI.Button(new Rect(847,333,256,36),audio.Muted?"已静音 · 开启声音":"声音开启 · 静音",true,false,!audio.Muted))audio.Muted=!audio.Muted;
        DrawTablePauseVolume(383,"总音量",audio.MasterVolume,v=>audio.MasterVolume=v);
        DrawTablePauseVolume(439,"操作音效",audio.EffectsVolume,v=>audio.EffectsVolume=v);
        DrawTablePauseVolume(495,"背景音乐",audio.MusicVolume,v=>audio.MusicVolume=v);
        DrawTablePauseVolume(551,"自然环境",audio.AmbientVolume,v=>audio.AmbientVolume=v);
        DrawTablePauseVolume(607,"骑士语音",audio.VoiceVolume,v=>audio.VoiceVolume=v);
        PastoralMenuUI.Label(new Rect(337,668,165,32),"语音语言",16,null,true,TextAnchor.MiddleCenter);
        int language=PastoralMenuUI.Segments(new Rect(526,665,300,37),audio.VoiceLanguage,new[]{"中文","英语"});
        if(language!=audio.VoiceLanguage)audio.VoiceLanguage=language;
        if(PastoralMenuUI.Button(new Rect(847,665,256,37),"试听骑士语音"))audio.PreviewVoice();
    }
    void DrawTablePauseVolume(float y,string caption,float value,Action<float> update)
    {
        PastoralMenuUI.Label(new Rect(337,y,165,32),caption,17,null,true,TextAnchor.MiddleCenter);
        float next=PastoralMenuUI.Slider(new Rect(526,y+10,466,20),value);
        PastoralMenuUI.Label(new Rect(1011,y,92,32),Mathf.RoundToInt(next*100)+"%",16,PastoralMenuUI.Forest,false,TextAnchor.MiddleCenter);
        if(Mathf.Abs(next-value)>.0001f)update(next);
    }
    void DrawTablePauseView()
    {
        PastoralMenuUI.Label(new Rect(337,343,170,42),"牌桌视角",17,null,true,TextAnchor.MiddleCenter);
        int view=TablePauseTopView?1:0;
        int nextView=PastoralMenuUI.Segments(new Rect(539,343,564,42),view,new[]{"斜视","俯视"});
        if(nextView!=view)ToggleTableCamera();
        if(settingsCamera==null||!settingsCamera.isActiveAndEnabled)settingsCamera=FindFirstObjectByType<TabletopScene>();
        if(settingsCamera!=null)
        {
            const int low=TabletopCameraModel.MinPitch,high=TabletopCameraModel.MaxPitch;
            PastoralMenuUI.Label(new Rect(337,412,170,38),"地图俯角",17,null,true,TextAnchor.MiddleCenter);
            float value=(settingsCamera.Pitch-low)/(float)(high-low);
            int pitch=Mathf.RoundToInt(low+(high-low)*PastoralMenuUI.Slider(new Rect(539,423,460,20),value));
            if(pitch!=settingsCamera.Pitch){settingsCamera.SetPitch(pitch);pitchEdited=true;}
            PastoralMenuUI.Label(new Rect(1009,412,94,38),settingsCamera.Pitch+"°",17,PastoralMenuUI.Forest,true,TextAnchor.MiddleCenter);
            PastoralMenuUI.Label(new Rect(539,460,564,28),"滚轮缩放　·　Ctrl + 滚轮旋转",14,PastoralMenuUI.Muted,false,TextAnchor.MiddleLeft);
        }
        PastoralMenuUI.Label(new Rect(337,512,170,42),"AI 行动节奏",17,null,true,TextAnchor.MiddleCenter);
        bool before=GUI.enabled;GUI.enabled=before&&!NetworkGame;
        int speed=PastoralMenuUI.Segments(new Rect(539,512,564,42),AiSpeedLevel,TabletopAiSpeed.Names);
        GUI.enabled=before;
        if(speed!=AiSpeedLevel){AiSpeedLevel=TabletopAiSpeed.Clamp(speed);nextAction=Time.unscaledTime+(float)TabletopAiSpeed.Interval(AiSpeedLevel);}
        PastoralMenuUI.Label(new Rect(337,574,766,54),NetworkGame?"联机对局由房间控制节奏":"",14,PastoralMenuUI.Muted,false,TextAnchor.MiddleCenter);
        if(RuleSet=="seafarers"&&PastoralMenuUI.Button(new Rect(526,648,388,43),"航海场景行动"))
        {CloseTablePauseMenu();GetComponent<M3App>()?.ShowScenarioActions();}
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
