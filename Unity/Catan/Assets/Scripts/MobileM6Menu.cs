using System;
using System.Linq;
using UnityEngine;

public sealed partial class M6App
{
    Vector2 mobileMenuScroll;
    void DrawMobileMenus()
    {
        ModernOperationUI.Styles(font);
        if(MenuVisible)
        {
            Panel(new Rect(0,0,1440,900),ModernOperationUI.Background);
            ModernOperationUI.Label(new Rect(28,16,1380,68),"卡坦岛之三国争霸 · "+ReleaseVersion,ModernOperationUI.Title);
            for(int i=0;i<RuleIds.Length;i++)
                if(ModernOperationUI.Button(new Rect(28+i*350,103,334,96),RuleNames[i],true,false,menuRule==i)){menuRule=i;menuScenario=0;menuVictoryPoints=null;}
            var ids=ScenarioIds();menuScenario=Mathf.Clamp(menuScenario,0,ids.Length-1);
            ModernOperationUI.Panel(new Rect(28,222,812,644));
            var content=new Rect(48,244,772,598);MobileTabletop.ScrollTouch(content,ref mobileMenuScroll);
            GUILayout.BeginArea(content);mobileMenuScroll=GUILayout.BeginScrollView(mobileMenuScroll);
            if(menuRule==1||menuRule==3)
            {
                ModernOperationUI.Label(GUILayoutUtility.GetRect(740,76),"剧本："+ScenarioName(ids[menuScenario]),ModernOperationUI.Text);
                GUILayout.BeginHorizontal();if(ModernOperationUI.LayoutButton("上一个剧本")){menuScenario=(menuScenario+ids.Length-1)%ids.Length;menuVictoryPoints=null;}
                if(ModernOperationUI.LayoutButton("下一个剧本")){menuScenario=(menuScenario+1)%ids.Length;menuVictoryPoints=null;}GUILayout.EndHorizontal();
            }
            else GUILayout.Label("标准基础地图",ModernOperationUI.Text,GUILayout.Height(72));
            GUILayout.Space(18);GUILayout.BeginHorizontal();
            if(ModernOperationUI.LayoutButton("3 人 · 你 + 2 AI",true,menuPlayers==3))menuPlayers=3;
            if(ModernOperationUI.LayoutButton("4 人 · 你 + 3 AI",true,menuPlayers==4))menuPlayers=4;GUILayout.EndHorizontal();
            GUILayout.Space(18);GUILayout.Label("你的席位",ModernOperationUI.Small);GUILayout.BeginHorizontal();
            menuHuman=Mathf.Clamp(menuHuman,0,menuPlayers-1);
            for(int i=0;i<menuPlayers;i++)if(ModernOperationUI.LayoutButton("P"+(i+1),true,menuHuman==i))menuHuman=i;GUILayout.EndHorizontal();
            int normal=DefaultVictoryPoints(RuleIds[menuRule],ids[menuScenario],menuPlayers),maximum=MaximumVictoryPoints(RuleIds[menuRule],ids[menuScenario]);
            if(menuVictoryPoints.HasValue)menuVictoryPoints=Mathf.Clamp(menuVictoryPoints.Value,Catan.Core.VictoryPointSettings.Minimum,maximum);
            int points=menuVictoryPoints??normal;
            GUILayout.Space(18);GUILayout.Label("胜利分数 · "+points+" 分",ModernOperationUI.Text);GUILayout.BeginHorizontal();
            if(ModernOperationUI.LayoutButton("− 1 分",points>Catan.Core.VictoryPointSettings.Minimum))menuVictoryPoints=points-1;
            if(ModernOperationUI.LayoutButton("+ 1 分",points<maximum))menuVictoryPoints=points+1;
            if(ModernOperationUI.LayoutButton("剧本默认"))menuVictoryPoints=null;GUILayout.EndHorizontal();
            GUILayout.EndScrollView();GUILayout.EndArea();
            if(ModernOperationUI.Button(new Rect(870,222,542,122),Ready?"开始新局 · 替换当前局面":"开始对局",true,true))StartGame(RuleIds[menuRule],ids[menuScenario],menuPlayers,"P"+(menuHuman+1),targetVictoryPoints:menuVictoryPoints);
            if(ModernOperationUI.Button(new Rect(870,364,542,110),"读取存档"))LoadGame();
            if(Ready&&ModernOperationUI.Button(new Rect(870,494,542,110),"返回当前对局"))ResumeGame();
            ModernOperationUI.Label(new Rect(886,626,510,210),Notice+"\n单指点击 / 平移，双指缩放。",ModernOperationUI.Small);
            return;
        }
        if(tableMenu)
        {
            Panel(new Rect(0,0,1440,900),new Color(0,0,0,.78f));ModernOperationUI.Panel(new Rect(28,28,1384,844));
            ModernOperationUI.Label(new Rect(58,44,1300,70),"游戏菜单 · "+ReleaseVersion,ModernOperationUI.Title);
            if(ModernOperationUI.Button(new Rect(58,138,650,108),"返回游戏",true,true)){tableMenu=false;nextAction=Time.unscaledTime+.6f;}
            if(ModernOperationUI.Button(new Rect(732,138,650,108),"放回棋子 / 取消选择"))
            {tableMenu=false;HandleTableEscape();tableMenu=false;}
            if(ModernOperationUI.Button(new Rect(58,268,650,100),"保存游戏"))SaveGame();
            if(ModernOperationUI.Button(new Rect(732,268,650,100),"恢复存档")){tableMenu=false;LoadGame();}
            if(ModernOperationUI.Button(new Rect(58,390,650,100),Paused?"继续 AI":"暂停 AI"))SetPaused(!Paused);
            if(ModernOperationUI.Button(new Rect(732,390,650,100),"切换俯视"))ToggleTableCamera();
            for(int i=0;i<StrategyHudTheme.Names.Length;i++)if(ModernOperationUI.Button(new Rect(58+i*446,512,430,92),StrategyHudTheme.Names[i],true,false,StrategyHudTheme.Selected==i))StrategyHudTheme.Select(i);
            if(ModernOperationUI.Button(new Rect(58,626,650,100),"规则 / 开始新局")){tableMenu=false;ShowMenu();}
            if(RuleSet=="seafarers"&&ModernOperationUI.Button(new Rect(732,626,650,100),"航海场景行动")){tableMenu=false;GetComponent<M3App>()?.ShowScenarioActions();}
            ModernOperationUI.Label(new Rect(58,754,1300,94),Notice,ModernOperationUI.Small);
            return;
        }
        string prompt=CurrentTablePrompt;
        if(prompt.Length>0)
        {
            ModernOperationUI.Round(new Rect(250,174,940,86),new Color(0,0,0,.72f));
            ModernOperationUI.Label(new Rect(270,184,900,66),prompt,ModernOperationUI.Center);
        }
    }
}
