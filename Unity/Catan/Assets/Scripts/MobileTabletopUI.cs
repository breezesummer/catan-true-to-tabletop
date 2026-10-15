using System;
using System.Linq;
using UnityEngine;

public sealed partial class TabletopUI
{
    bool mobileFacilities;
    void DrawMobile(string seat,string actor,string phase,int turn,int dice1,int dice2,TabletopSeat[] seats,bool canRoll,bool canEnd,Action roll,Action end,Action<string> trade,Action special,string specialLabel,Action<int> facility)
    {
        bool prior=GUI.enabled;
        GUI.enabled=prior&&(Event.current.type==EventType.Repaint||!BlocksOverlayInput(Event.current.mousePosition));
        contextVisible=false;
        ModernOperationUI.Round(new Rect(0,0,1440,96),ModernOperationUI.Background);
        var menu=new Rect(12,8,170,80);hits.Add(menu);
        if(ModernOperationUI.Button(menu,"菜单"))UnityEngine.Object.FindFirstObjectByType<M6App>()?.OpenTableMenu();
        if(ObjectButton(new Rect(192,8,180,80),"银行"))trade?.Invoke(null);
        if(ObjectButton(new Rect(382,8,210,80),"玩家交易"))trade?.Invoke("");
        var own=seats.FirstOrDefault(p=>p.Id==seat);
        if(ObjectButton(new Rect(602,8,180,80),"城市设施",own?.Improvements!=null))mobileFacilities=!mobileFacilities;
        if(ObjectButton(new Rect(792,8,210,80),"历史记录"))history.Open();
        if(ObjectButton(new Rect(1012,8,416,80),specialLabel==""?"当前行动":specialLabel,special!=null))special?.Invoke();
        var banner=new Rect(12,104,610,58);hits.Add(banner);
        ModernOperationUI.Round(banner,ModernOperationUI.Background);
        ModernOperationUI.Label(banner,"第 "+turn+" 回合 · "+actor+(actor==seat?" 你的行动":" AI 行动"),ModernOperationUI.Center);
        float width=790f/Math.Max(1,seats.Length);
        for(int i=0;i<seats.Length;i++)
        {
            var p=seats[i];var area=new Rect(638+i*width,104,width-8,58);hits.Add(area);
            int points=p.Score==null?p.Points:p.Id==seat?p.Score.Total:p.Score.PublicPoints;
            if(ModernOperationUI.Button(area,p.Id+" · "+points+" 分",p.Id!=seat))trade?.Invoke(p.Id);
        }
        var help=new Rect(238,674,926,48);hits.Add(help);
        ModernOperationUI.Round(help,new Color(0,0,0,.55f));
        ModernOperationUI.Label(help,"单指拖动地图 · 双指缩放 · 点骑士 / 船操作",ModernOperationUI.Small);
        string summaryText=(dice1>0?"骰子 "+dice1+" + "+dice2+" = "+(dice1+dice2)+"\n":"")+defense.MobileSummary;
        float summaryHeight=Mathf.Max(54,ModernOperationUI.Small.CalcHeight(new GUIContent(summaryText),436));
        var summary=new Rect(14,174,460,summaryHeight+16);hits.Add(summary);ModernOperationUI.Round(summary,new Color(0,0,0,.5f));
        ModernOperationUI.Label(new Rect(26,182,436,summaryHeight),summaryText,ModernOperationUI.Small);
        var turnButton=new Rect(1180,620,248,104);hits.Add(turnButton);
        if(ModernOperationUI.Button(turnButton,canRoll?"掷骰":canEnd?"结束回合":"等待操作",(canRoll||canEnd)&&!DiceRolling,true))
        {if(canRoll)roll?.Invoke();else end?.Invoke();}
        if(mobileFacilities&&own?.Improvements!=null)
        {
            var area=new Rect(602,174,600,330);hits.Add(area);ModernOperationUI.Panel(area);
            string[] names={"贸易设施","政治设施","科学设施"};
            for(int i=0;i<3;i++)if(ObjectButton(new Rect(618,190+i*100,568,88),names[i]+" · "+own.Improvements[i]+" 级")){mobileFacilities=false;facility?.Invoke(i);}
        }
        GUI.enabled=prior;
    }
}
