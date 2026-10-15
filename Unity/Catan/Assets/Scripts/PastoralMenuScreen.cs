using System;
using System.Linq;
using UnityEngine;

public sealed partial class M6App
{
    Vector2 pastoralMenuScroll;
    partial void DrawPastoralAudioSettings(Rect rect);
    // 与现有字段和验证入口共用配置；这里只负责绘制。
    void DrawPastoralMainMenu()
    {
        PastoralMenuUI.Styles(font);PastoralMenuScene.Draw(Time.unscaledTime);
        DrawPastoralHero();
        var panel=new Rect(838,30,562,840);PastoralMenuUI.Panel(panel);
        GUI.Label(new Rect(870,56,350,22),"愿每一次出发，都有丰收相伴",PastoralMenuUI.Eyebrow);
        GUI.Label(new Rect(870,83,430,42),"开一桌新冒险",PastoralMenuUI.Title);
        GUI.Label(new Rect(1272,91,96,25),ReleaseVersion,PastoralMenuUI.Small);
        PastoralMenuUI.Fill(new Rect(870,133,498,1),new Color(.45f,.55f,.43f,.17f));
        var viewport=new Rect(866,149,506,464);
        MobileTabletop.ScrollTouch(viewport,ref pastoralMenuScroll);
        pastoralMenuScroll=GUI.BeginScrollView(viewport,pastoralMenuScroll,new Rect(0,0,488,512),false,false);
        DrawPastoralConfiguration(488);
        GUI.EndScrollView();
        PastoralMenuUI.ScrollIndicator(viewport,512,pastoralMenuScroll.y);
        // 主要入口固定可见；较矮视口或新增设置通过配置区滚动访问。
        if(PastoralMenuUI.Button(new Rect(870,620,498,49),Ready?"开始新局 · 替换未保存局面":"开始对局   →",true,true))
        {var ids=ScenarioIds();StartGame(RuleIds[menuRule],ids[menuScenario],menuPlayers,"P"+(menuHuman+1),targetVictoryPoints:menuVictoryPoints,aiSpeedLevel:menuAiSpeed);}
        if(PastoralMenuUI.Button(new Rect(870,680,Ready?243:498,38),"读取存档"))LoadGame();
        if(Ready&&PastoralMenuUI.Button(new Rect(1125,680,243,38),"返回当前局"))ResumeGame();
        if(PastoralMenuUI.Button(new Rect(870,727,498,38),"局域网 / 在线大厅    ↗"))OpenNetworkMenu();
        PastoralMenuUI.Fill(new Rect(870,777,498,1),new Color(.45f,.55f,.43f,.16f));
        DrawPastoralAudioSettings(new Rect(870,789,498,42));
        var noticeStyle=new GUIStyle(PastoralMenuUI.Small){fontSize=11};
        GUI.Label(new Rect(870,836,498,32),new GUIContent(Notice,Notice),noticeStyle);
    }
    void DrawPastoralConfiguration(float width)
    {
        PastoralMenuUI.Section(new Rect(4,0,width-8,22),"01  选择规则","四种玩法，同一座岛");
        string[] subtitles={"从村庄出发，收获第一片领土","扬帆远航，发现新的海岸","守护城邦，迎接骑士与蛮族","跨越山海，展开完整冒险"};
        for(int i=0;i<4;i++)
        {
            var r=new Rect(4+(i%2)*244,29+(i/2)*62,236,54);
            bool selected=menuRule==i;if(PastoralMenuUI.Button(r,"",true,false,selected)){menuRule=i;menuScenario=0;menuVictoryPoints=null;}
            var main=new GUIStyle(PastoralMenuUI.Text){fontSize=17,fontStyle=FontStyle.Bold};main.normal.textColor=selected?Color.white:PastoralMenuUI.Ink;
            var sub=new GUIStyle(PastoralMenuUI.Small){fontSize=10,wordWrap=false};sub.normal.textColor=selected?new Color(.84f,.90f,.78f):PastoralMenuUI.Muted;
            GUI.Label(new Rect(r.x+17,r.y+7,r.width-25,22),RuleNames[i],main);GUI.Label(new Rect(r.x+17,r.y+33,r.width-25,16),subtitles[i],sub);
        }
        var ids=ScenarioIds();menuScenario=Mathf.Clamp(menuScenario,0,ids.Length-1);
        PastoralMenuUI.Section(new Rect(4,157,width-8,21),"02  选择剧本",menuRule==1||menuRule==3?(menuScenario+1)+" / "+ids.Length:"标准岛屿");
        bool scenarios=menuRule==1||menuRule==3;
        if(scenarios&&PastoralMenuUI.Button(new Rect(4,183,38,36),"‹")){menuScenario=(menuScenario+ids.Length-1)%ids.Length;menuVictoryPoints=null;}
        PastoralMenuUI.Round(new Rect(scenarios?49:4,183,scenarios?390:480,36),new Color(.94f,.94f,.89f),9);
        GUI.Label(new Rect(scenarios?58:15,185,scenarios?372:458,33),scenarios?ScenarioName(ids[menuScenario]):menuRule==2?"标准城市与骑士地图":"标准基础地图",PastoralMenuUI.Center);
        if(scenarios&&PastoralMenuUI.Button(new Rect(446,183,38,36),"›")){menuScenario=(menuScenario+1)%ids.Length;menuVictoryPoints=null;}
        PastoralMenuUI.Section(new Rect(4,235,width-8,20),"03  同桌席位","其余席位由当前 AI 执行");
        menuPlayers=PastoralMenuUI.Segments(new Rect(4,261,480,35),menuPlayers-3,new[]{"3 人 · 你 + 2 位 AI","4 人 · 你 + 3 位 AI"})+3;
        menuHuman=Mathf.Clamp(menuHuman,0,menuPlayers-1);
        GUI.Label(new Rect(5,306,80,30),"你的席位",PastoralMenuUI.Small);
        menuHuman=PastoralMenuUI.Segments(new Rect(86,302,398,34),menuHuman,Enumerable.Range(1,menuPlayers).Select(i=>"P"+i).ToArray());
        GUI.Label(new Rect(5,350,80,30),"AI 速度",PastoralMenuUI.Small);
        menuAiSpeed=PastoralMenuUI.Segments(new Rect(86,345,398,34),menuAiSpeed,TabletopAiSpeed.Names);
        int defaultPoints=DefaultVictoryPoints(RuleIds[menuRule],ids[menuScenario],menuPlayers),maximumPoints=MaximumVictoryPoints(RuleIds[menuRule],ids[menuScenario]);
        if(menuVictoryPoints.HasValue)menuVictoryPoints=Mathf.Clamp(menuVictoryPoints.Value,Catan.Core.VictoryPointSettings.Minimum,maximumPoints);
        int points=menuVictoryPoints??defaultPoints;
        PastoralMenuUI.Section(new Rect(4,397,width-8,20),"04  胜利目标","可设 5–"+maximumPoints+" 分");
        if(PastoralMenuUI.Button(new Rect(4,425,39,38),"−",points>Catan.Core.VictoryPointSettings.Minimum))menuVictoryPoints=Math.Max(Catan.Core.VictoryPointSettings.Minimum,points-1);
        GUI.Label(new Rect(50,426,116,37),(menuVictoryPoints??defaultPoints)+" 分",PastoralMenuUI.Title);
        if(PastoralMenuUI.Button(new Rect(162,425,39,38),"+",points<maximumPoints))menuVictoryPoints=Math.Min(maximumPoints,points+1);
        if(PastoralMenuUI.Button(new Rect(226,425,258,38),"恢复剧本默认 · "+defaultPoints+" 分"))menuVictoryPoints=null;
        string hint=menuRule==1&&ids[menuScenario]=="wonders-of-catan"?"奇观四层直接获胜；分数获胜还需层数领先。":menuRule==1&&ids[menuScenario]=="pirate-islands"?"达到分数后，仍须夺回自己的堡垒。":menuRule==1&&ids[menuScenario]=="cloth-for-catan"?"布匹耗尽时，仍按剧本规则结算。":"目标分数与开局配置会随本局存档保存。";
        GUI.Label(new Rect(5,474,478,32),hint,PastoralMenuUI.Small);
    }
    void DrawPastoralHero()
    {
        PastoralMenuUI.Round(new Rect(66,58,170,30),new Color(.98f,.99f,.91f,.52f),15);
        GUI.Label(new Rect(81,63,155,22),"海风轻拂 · 沃野新生",PastoralMenuUI.Eyebrow);
        GUI.Label(new Rect(63,106,718,82),"卡坦岛之三国争霸",PastoralMenuUI.Hero);
        GUI.Label(new Rect(69,197,620,66),"在山海之间筑起村庄，\n与同伴一起，书写属于这座岛的故事。",PastoralMenuUI.HeroSmall);
        PastoralMenuUI.Round(new Rect(68,818,320,44),new Color(.97f,.97f,.87f,.88f),22);
        var s=new GUIStyle(PastoralMenuUI.Small){alignment=TextAnchor.MiddleCenter,fontSize=13};
        GUI.Label(new Rect(76,823,304,34),"海洋 · 山川 · 田野 · 每一桌新的冒险",s);
    }
}
