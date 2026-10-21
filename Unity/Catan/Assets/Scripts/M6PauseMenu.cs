using System;
using UnityEngine;

// 桌内菜单沿用主页的纸面、森林绿与金色细线；菜单阻挡本地调度，不改写存档中的暂停设置。
public sealed partial class M6App
{
    int tablePausePage,tablePauseSettingsTab;
    string tablePauseFeedback="";
    internal bool TablePauseSettingsOpen=>tableMenu&&tablePausePage==1;

    void ResetTablePauseMenu()
    {
        tablePausePage=0;tablePauseFeedback="";encyclopediaOpen=false;
    }
    bool HandleTablePausePageEscape()
    {
        if(tablePausePage==0)return false;
        tablePausePage=0;TabletopAudio.Instance?.FlushSettings();return true;
    }
    void CloseTablePauseMenu(bool resumeAi=false)
    {
        tableMenu=false;encyclopediaOpen=false;tablePausePage=0;escapeDismissedInteraction=false;
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
        PastoralMenuUI.Fill(new Rect(0,0,1440,900),new Color(.035f,.10f,.075f,.69f));
    }
    void TablePauseHeading(Rect panel,string eyebrow,string heading,string subtitle)
    {
        PastoralMenuUI.Panel(panel);
        PastoralMenuUI.Label(new Rect(panel.x+28,panel.y+25,panel.width-56,22),eyebrow,12,PastoralMenuUI.Forest,true,TextAnchor.MiddleCenter);
        PastoralMenuUI.Label(new Rect(panel.x+28,panel.y+61,panel.width-56,48),heading,31,PastoralMenuUI.Ink,true,TextAnchor.MiddleCenter);
        PastoralMenuUI.Fill(new Rect(panel.center.x-32,panel.y+124,64,2),PastoralMenuUI.Gold);
        PastoralMenuUI.Label(new Rect(panel.x+28,panel.y+145,panel.width-56,38),subtitle,13,PastoralMenuUI.Muted,false,TextAnchor.MiddleCenter);
    }
    void DrawTablePauseMenu()
    {
        DrawTablePauseBackdrop();
        if(tablePausePage==1){DrawTablePauseSettings();return;}
        var panel=new Rect(440,100,560,700);
        TablePauseHeading(panel,"山 海 之 间  ·  一 桌 好 时 光","游戏菜单","卡坦岛之三国争霸 · "+ReleaseVersion);
        string[] entries=NetworkGame?new[]{"继续游戏","保存游戏","游戏设置","游戏百科","重新开始","返回主菜单"}:
            new[]{"继续游戏","保存游戏","游戏设置","游戏百科","返回主菜单"};
        for(int i=0;i<entries.Length;i++)
        {
            bool restart=NetworkGame&&i==4;
            if(!PastoralMenuUI.Button(new Rect(526,302+i*(NetworkGame?62:75),388,58),entries[i],!restart||Network.CanRestartRoom,true))continue;
            switch(i)
            {
                case 0:CloseTablePauseMenu(true);break;
                case 1:SaveGame();tablePauseFeedback=Notice;break;
                case 2:tablePausePage=1;tablePauseFeedback="";GUI.FocusControl(null);break;
                case 3:encyclopediaOpen=true;break;
                case 4:if(NetworkGame){Network.RestartRoom();tablePauseFeedback="正在重新生成地图与行动顺序…";}else ReturnToMainMenu();break;
                case 5:ReturnToMainMenu();break;
            }
        }
        string status=NetworkGame&&!string.IsNullOrEmpty(Network.LastError)?Network.LastError:tablePauseFeedback.Length>0?tablePauseFeedback:NetworkGame?"联机对局继续运行；房主可重新开始，所有玩家同步重开。":Fault.Length>0?"AI 已暂停："+Fault:"游戏已暂停，愿下一次落子带来丰收。";
        PastoralMenuUI.Label(new Rect(477,704,486,68),status,13,PastoralMenuUI.Muted,false,TextAnchor.MiddleCenter);
    }
    void DrawTablePauseSettings()
    {
        var panel=new Rect(290,69,860,762);
        TablePauseHeading(panel,"舒 适 地 游 玩","游戏设置","让声音与桌面，配合你喜欢的游戏时光。" );
        tablePauseSettingsTab=PastoralMenuUI.Segments(new Rect(337,272,766,42),tablePauseSettingsTab,new[]{"桌面外观","声音","视角与节奏"});
        if(tablePauseSettingsTab==0)DrawTablePauseAppearance();
        else if(tablePauseSettingsTab==1)DrawTablePauseAudio();
        else DrawTablePauseView();
        PastoralMenuUI.Fill(new Rect(337,728,766,1),new Color(.48f,.54f,.42f,.18f));
        if(PastoralMenuUI.Button(new Rect(526,754,388,48),"返回游戏菜单",true,true))HandleTablePausePageEscape();
    }
    void DrawTablePauseAppearance()
    {
        DrawBoardThemeSettings(new Rect(337,325,766,390));
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
        PastoralMenuUI.Label(new Rect(337,349,766,34),"按自己的节奏，俯瞰这片岛屿",23,null,true,TextAnchor.MiddleCenter);
        PastoralMenuUI.Label(new Rect(337,412,170,42),"牌桌视角",17,null,true,TextAnchor.MiddleCenter);
        int view=TablePauseTopView?1:0;
        int nextView=PastoralMenuUI.Segments(new Rect(539,412,564,42),view,new[]{"斜视","俯视"});
        if(nextView!=view)ToggleTableCamera();
        PastoralMenuUI.Label(new Rect(337,496,170,42),"AI 行动节奏",17,null,true,TextAnchor.MiddleCenter);
        bool before=GUI.enabled;GUI.enabled=before&&!NetworkGame;
        int speed=PastoralMenuUI.Segments(new Rect(539,496,564,42),AiSpeedLevel,TabletopAiSpeed.Names);
        GUI.enabled=before;
        if(speed!=AiSpeedLevel){AiSpeedLevel=TabletopAiSpeed.Clamp(speed);nextAction=Time.unscaledTime+(float)TabletopAiSpeed.Interval(AiSpeedLevel);}
        PastoralMenuUI.Label(new Rect(337,574,766,54),NetworkGame?"联机节奏由房间统一调度，打开本菜单不会暂停其他玩家。":"本菜单打开时，本地 AI 与牌桌操作暂停；返回后按所选节奏继续。",14,PastoralMenuUI.Muted,false,TextAnchor.MiddleCenter);
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
