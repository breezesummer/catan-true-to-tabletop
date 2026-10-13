using System;
using System.Linq;
using Catan.Core;
using UnityEngine;

// 只渲染已按当前观看者权限投影的历史，不能访问权威状态。
public sealed class TabletopHistoryUI
{
    static readonly Rect Feed = new Rect(18,516,336,126), Window = new Rect(210,144,1020,560);
    Vector2 feedScroll, historyScroll;
    string filter;
    long lastSequence = -1;
    bool follow = true;
    public bool IsOpen { get; private set; }
    public bool BlocksInput(Vector2 p) => IsOpen || Feed.Contains(p);

    public void Draw(Font font,GameHistoryEntry[] entries,string[] players)
    {
        ModernOperationUI.Styles(font);
        entries=entries??Array.Empty<GameHistoryEntry>();players=players??Array.Empty<string>();
        long sequence=entries.Length==0?0:entries[entries.Length-1].Sequence;
        if(sequence!=lastSequence)
        {
            if(sequence<lastSequence){filter=null;historyScroll=Vector2.zero;follow=true;}
            if(follow)feedScroll.y=float.MaxValue;
            lastSequence=sequence;
        }
        if(!IsOpen)
        {
        ModernOperationUI.Panel(Feed);
        ModernOperationUI.Label(new Rect(Feed.x+12,Feed.y+7,120,23),"对局动态",ModernOperationUI.Small);
        if(ModernOperationUI.Button(new Rect(Feed.xMax-106,Feed.y+5,94,25),"查看历史"))IsOpen=true;
        var recent=entries.Skip(Math.Max(0,entries.Length-40)).ToArray();
        var area=new Rect(Feed.x+8,Feed.y+36,Feed.width-16,Feed.height-44);
        if(Event.current.type==EventType.ScrollWheel&&Feed.Contains(Event.current.mousePosition))follow=false;
        feedScroll=DrawRows(area,recent,feedScroll,true);
        float feedHeight=recent.Sum(e=>Height(e,area.width-22,true));
        if(feedScroll.y>=Math.Max(0,feedHeight-area.height)-3)follow=true;
        if(!follow&&ModernOperationUI.Button(new Rect(Feed.x+130,Feed.y+5,84,25),"最新 ↓")){follow=true;feedScroll.y=float.MaxValue;}
        }
        if(!IsOpen)return;
        if(Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.Escape){IsOpen=false;Event.current.Use();return;}
        ModernOperationUI.Fill(new Rect(0,0,1440,900),new Color(0,0,0,.50f));
        ModernOperationUI.Panel(Window);
        ModernOperationUI.Label(new Rect(Window.x+24,Window.y+18,500,35),"对局历史",ModernOperationUI.Title);
        ModernOperationUI.Label(new Rect(Window.x+24,Window.y+54,800,24),"按行动者或受影响的玩家筛选 · 私密变化仅向有权查看的玩家显示",ModernOperationUI.Small);
        if(ModernOperationUI.Button(new Rect(Window.xMax-78,Window.y+18,52,32),"关闭")){IsOpen=false;return;}
        float x=Window.x+24;
        if(ModernOperationUI.Button(new Rect(x,Window.y+89,108,34),"全部玩家",true,false,filter==null)){filter=null;historyScroll=Vector2.zero;}x+=118;
        foreach(var player in players)
        {
            if(ModernOperationUI.Button(new Rect(x,Window.y+89,108,34),player,true,false,filter==player)){filter=player;historyScroll=Vector2.zero;}x+=118;
        }
        var visible=entries.Where(e=>filter==null||e.Involves(filter)).ToArray();
        historyScroll=DrawRows(new Rect(Window.x+20,Window.y+140,Window.width-40,Window.height-182),visible,historyScroll,false);
        ModernOperationUI.Label(new Rect(Window.x+24,Window.yMax-30,650,22),"共 "+visible.Length+" 条记录 · 随对局存档恢复",ModernOperationUI.Small);
        if(ModernOperationUI.Button(new Rect(Window.xMax-132,Window.yMax-34,106,25),"跳到最新 ↓"))historyScroll.y=float.MaxValue;
    }
    static float Height(GameHistoryEntry entry,float width,bool compact)
    {
        float header=ModernOperationUI.Text.CalcHeight(new GUIContent(Caption(entry)),Math.Max(100,width-14));
        int changes=(entry.Changes??Array.Empty<GameHistoryChange>()).Length;
        int columns=Math.Max(1,(int)(width/(compact?140:210)));
        return Math.Max(compact?22:28,header)+(entry.Dice1>0?34:0)+((changes+columns-1)/columns)*32+14;
    }
    static string Caption(GameHistoryEntry entry)=>"第 "+entry.Turn+" 回合 · "+entry.PlayerId+"  "+entry.Action;
    Vector2 DrawRows(Rect viewport,GameHistoryEntry[] entries,Vector2 position,bool compact)
    {
        float width=viewport.width-22,total=entries.Sum(e=>Height(e,width,compact));
        position.y=Mathf.Clamp(position.y,0,Math.Max(0,total-viewport.height));
        position=GUI.BeginScrollView(viewport,position,new Rect(0,0,width,Math.Max(viewport.height,total)),false,false);
        float y=0;
        foreach(var entry in entries)
        {
            float height=Height(entry,width,compact);
            if(y+height>=position.y&&y<=position.y+viewport.height)DrawRow(new Rect(0,y,width,height),entry,compact);
            y+=height;
        }
        if(entries.Length==0)ModernOperationUI.Label(new Rect(8,12,width-16,36),"尚无操作记录",ModernOperationUI.Small);
        GUI.EndScrollView();return position;
    }
    static void DrawRow(Rect r,GameHistoryEntry entry,bool compact)
    {
        var style=new GUIStyle(ModernOperationUI.Text){fontSize=compact?12:15};
        float titleHeight=Math.Max(compact?22:28,ModernOperationUI.Text.CalcHeight(new GUIContent(Caption(entry)),r.width-14));
        ModernOperationUI.Fill(new Rect(r.x,r.y+3,3,Math.Max(14,titleHeight-5)),BoardRenderer.SeatColor(entry.PlayerId));
        ModernOperationUI.Label(new Rect(r.x+9,r.y,r.width-12,titleHeight),Caption(entry),style);
        float y=r.y+titleHeight;
        if(entry.Dice1>0)
        {
            ModernOperationUI.Label(new Rect(r.x+8,y,70,28),(entry.Dice1+entry.Dice2)+" 点 （",style);
            Die(new Rect(r.x+76,y,26,26),entry.Dice1,new Color(.64f,.11f,.08f),false);
            Die(new Rect(r.x+110,y,26,26),entry.Dice2,new Color(.94f,.90f,.80f),false);
            if(!string.IsNullOrEmpty(entry.EventDie))Die(new Rect(r.x+144,y,26,26),EventFace(entry.EventDie),new Color(.21f,.25f,.27f),true);
            ModernOperationUI.Label(new Rect(r.x+(!string.IsNullOrEmpty(entry.EventDie)?177:143),y,25,28),"）",style);y+=34;
        }
        var changes=entry.Changes??Array.Empty<GameHistoryChange>();int columns=Math.Max(1,(int)(r.width/(compact?140:210)));float cell=r.width/columns;
        for(int i=0;i<changes.Length;i++)
        {
            var change=changes[i];float x=r.x+(i%columns)*cell+9,cy=y+(i/columns)*32;
            var label=new GUIStyle(ModernOperationUI.Small);label.normal.textColor=change.Amount<0?new Color(.95f,.64f,.51f):new Color(.60f,.83f,.68f);
            string amount=(change.Amount>0?"+":"")+change.Amount;
            ModernOperationUI.Label(new Rect(x,cy,38,27),change.PlayerId,label);
            if(change.GoodIndex>=0&&change.GoodIndex<8)
            {
                OperationIconAssets.DrawGood(new Rect(x+33,cy+1,25,25),change.GoodIndex);
                ModernOperationUI.Label(new Rect(x+64,cy,cell-74,28),amount,label);
            }
            else
            {
                string name=!string.IsNullOrEmpty(change.Name)?change.Name:change.Category=="resource-count"?"手牌":"卡牌";
                ModernOperationUI.Label(new Rect(x+33,cy,cell-45,29),amount+" "+name,label);
            }
        }
        ModernOperationUI.Fill(new Rect(r.x+7,r.yMax-5,r.width-14,1),new Color(.24f,.32f,.31f,.45f));
    }
    // 骰面使用真实圆点与事件符号；基础版只有两骰，不伪造第三骰。
    static int EventFace(string value) => value=="Trade"?1:value=="Politics"?2:value=="Science"?3:0;
    static void Die(Rect r,int value,Color color,bool eventDie)
    {
        ModernOperationUI.Round(new Rect(r.x+1,r.y+2,r.width,r.height),new Color(0,0,0,.45f));ModernOperationUI.Round(r,color);
        if(eventDie)
        {
            var style=new GUIStyle(ModernOperationUI.Center){fontSize=15,fontStyle=FontStyle.Bold};
            style.normal.textColor=value==0?ModernOperationUI.TextColor:value==1?new Color(.96f,.77f,.28f):value==2?new Color(.41f,.64f,.97f):new Color(.37f,.87f,.53f);
            GUI.Label(r,value==0?"船":value==1?"贸":value==2?"政":"科",style);return;
        }
        Color ink=color.r>.8f?new Color(.15f,.14f,.13f):new Color(.99f,.95f,.85f);
        Action<float,float> dot=(x,y)=>TabletopUI.Token(new Rect(r.x+r.width*x-2,r.y+r.height*y-2,4,4),ink);
        if(value%2==1)dot(.5f,.5f);
        if(value>=2){dot(.25f,.25f);dot(.75f,.75f);}
        if(value>=4){dot(.75f,.25f);dot(.25f,.75f);}
        if(value==6){dot(.25f,.5f);dot(.75f,.5f);}
    }
}
