using System;
using System.Linq;
using Catan.Core;
using UnityEngine;

public sealed partial class TabletopHistoryUI
{
    void DrawMobile(GameHistoryEntry[] entries,string[] players)
    {
        if(!IsOpen)return;
        ModernOperationUI.Fill(new Rect(0,0,1440,900),new Color(0,0,0,.8f));ModernOperationUI.Panel(new Rect(12,12,1416,876));
        ModernOperationUI.Label(new Rect(40,24,500,70),"历史记录",ModernOperationUI.Title);
        if(ModernOperationUI.Button(new Rect(1210,26,190,82),"关闭")){IsOpen=false;return;}
        if(ModernOperationUI.Button(new Rect(40,120,220,80),"操作",true,false,!showDice))showDice=false;
        if(ModernOperationUI.Button(new Rect(280,120,220,80),"掷骰统计",true,false,showDice))showDice=true;
        if(ModernOperationUI.Button(new Rect(520,120,190,80),"全部玩家",true,false,filter==null))filter=null;
        for(int i=0;i<players.Length;i++)if(ModernOperationUI.Button(new Rect(730+i*170,120,156,80),players[i],true,false,filter==players[i]))filter=players[i];
        var viewport=new Rect(40,224,1360,628);
        var visible=entries.Where(e=>filter==null||e.Involves(filter)).ToArray();
        if(showDice)
        {
            var dice=new TabletopDiceHistory(entries,filter);
            MobileTabletop.ScrollTouch(viewport,ref diceScroll);
            diceScroll=GUI.BeginScrollView(viewport,diceScroll,new Rect(0,0,1320,1056));
            for(int total=2;total<=12;total++)
            {
                float y=(total-2)*92;
                ModernOperationUI.Label(new Rect(12,y,1270,82),total+" 点    "+dice.Count(total)+" 次    "+(dice.Frequency(total)*100).ToString("0.0")+"%",ModernOperationUI.Text);
                ModernOperationUI.Fill(new Rect(12,y+86,1270,2),ModernOperationUI.Line);
            }
            GUI.EndScrollView();return;
        }
        float[] heights=visible.Select(e=>Mathf.Max(128,ModernOperationUI.Text.CalcHeight(new GUIContent(Caption(e)),1260)+82)).ToArray();
        float height=Mathf.Max(viewport.height,heights.Sum());MobileTabletop.ScrollTouch(viewport,ref historyScroll);
        historyScroll=GUI.BeginScrollView(viewport,historyScroll,new Rect(0,0,1320,height));
        float row=0;
        for(int i=0;i<visible.Length;i++)
        {
            var entry=visible[i];ModernOperationUI.Label(new Rect(12,row,1270,heights[i]-50),Caption(entry),ModernOperationUI.Text);
            if(entry.Dice1>0)ModernOperationUI.Label(new Rect(12,row+heights[i]-52,1270,44),"骰子 "+entry.Dice1+" + "+entry.Dice2+" = "+(entry.Dice1+entry.Dice2),ModernOperationUI.Small);
            ModernOperationUI.Fill(new Rect(12,row+heights[i]-3,1270,2),ModernOperationUI.Line);row+=heights[i];
        }
        if(visible.Length==0)ModernOperationUI.Label(new Rect(20,20,1200,100),"尚无操作记录。",ModernOperationUI.Text);
        GUI.EndScrollView();
    }
}
