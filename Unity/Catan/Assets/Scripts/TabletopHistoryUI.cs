using System;
using System.Linq;
using Catan.Core;
using UnityEngine;

// 只渲染已按当前观看者权限投影的历史，不能访问权威状态。
public sealed partial class TabletopHistoryUI
{
    internal static readonly Rect Feed = new Rect(1182,327,246,355);
    static readonly Rect Collapsed=new Rect(1326,327,102,30);
    static readonly Rect Window = new Rect(210,144,1020,560);
    Vector2 feedScroll, historyScroll, diceScroll;
    string filter;
    long lastSequence = -1;
    bool follow = true, showDice;
    bool collapsed;
    TabletopWorldObjects diceArt;
    public bool IsOpen { get; private set; }
    public void Open(){IsOpen=true;}
    public bool TryClose()
    {if(!IsOpen)return false;IsOpen=false;return true;}
    public bool BlocksInput(Vector2 p) => IsOpen || (collapsed?Collapsed:Feed).Contains(p);

    public void Draw(Font font,GameHistoryEntry[] entries,string[] players,TabletopWorldObjects objects)
    {
        diceArt=objects;
        ModernOperationUI.Styles(font);
        entries=entries??Array.Empty<GameHistoryEntry>();players=players??Array.Empty<string>();
        if(!ReferenceEquals(cachedEntries,entries)){cachedEntries=entries;lines.Clear();}
        long sequence=entries.Length==0?0:entries[entries.Length-1].Sequence;
        if(sequence!=lastSequence)
        {
            if(sequence<lastSequence){filter=null;historyScroll=diceScroll=Vector2.zero;follow=true;}
            if(follow)feedScroll.y=float.MaxValue;
            lastSequence=sequence;
        }
        if(!IsOpen)
        {
        if(collapsed)
        {if(ModernOperationUI.Button(Collapsed,"历史 ‹"))collapsed=false;return;}
        ModernOperationUI.Round(Feed,new Color(ModernOperationUI.Background.r,ModernOperationUI.Background.g,ModernOperationUI.Background.b,.42f));
        if(ModernOperationUI.Button(new Rect(Feed.x+10,Feed.y+5,76,25),"历史"))IsOpen=true;
        if(ModernOperationUI.Button(new Rect(Feed.xMax-39,Feed.y+5,29,25),"›")){collapsed=true;return;}
        var recent=entries.Skip(Math.Max(0,entries.Length-40)).ToArray();
        var area=new Rect(Feed.x+8,Feed.y+36,Feed.width-16,Feed.height-44);
        if((Event.current.type==EventType.ScrollWheel||MobileTabletop.TouchDragged)&&Feed.Contains(Event.current.mousePosition))follow=false;
        feedScroll=DrawRows(area,recent,feedScroll,true);
        float feedHeight=recent.Sum(e=>Height(e,area.width-22,true));
        if(feedScroll.y>=Math.Max(0,feedHeight-area.height)-3)follow=true;
        if(!follow&&ModernOperationUI.Button(new Rect(Feed.xMax-128,Feed.y+5,78,25),"最新 ↓")){follow=true;feedScroll.y=float.MaxValue;}
        }
        if(!IsOpen)return;
        ModernOperationUI.Fill(new Rect(0,0,1440,900),new Color(0,0,0,.50f));
        ModernOperationUI.Panel(Window);
        ModernOperationUI.Label(new Rect(Window.x+24,Window.y+18,112,35),"历史",ModernOperationUI.Title);
        if(ModernOperationUI.Button(new Rect(Window.x+142,Window.y+18,108,34),"操作",true,false,!showDice))showDice=false;
        if(ModernOperationUI.Button(new Rect(Window.x+258,Window.y+18,108,34),"掷骰",true,false,showDice))showDice=true;
        if(ModernOperationUI.Button(new Rect(Window.xMax-78,Window.y+18,52,32),"关闭")){IsOpen=false;return;}
        float x=Window.x+24;
        if(ModernOperationUI.Button(new Rect(x,Window.y+70,108,34),"全部玩家",true,false,filter==null)){filter=null;historyScroll=diceScroll=Vector2.zero;}x+=118;
        foreach(var player in players)
        {
            if(ModernOperationUI.Button(new Rect(x,Window.y+70,108,34),M6App.PlayerLabel(player),true,false,filter==player,player)){filter=player;historyScroll=diceScroll=Vector2.zero;}x+=118;
        }
        if(showDice)
        {
            var dice=new TabletopDiceHistory(entries,filter);
            DrawDiceStatistics(new Rect(Window.x+24,Window.y+126,420,Window.height-168),dice);
            diceScroll=DrawDiceRows(new Rect(Window.x+468,Window.y+126,Window.width-492,Window.height-168),dice.Rolls,diceScroll);
            ModernOperationUI.Label(new Rect(Window.x+24,Window.yMax-30,650,22),"共 "+dice.Total+" 次掷骰"+(filter==null?"":" · 与 "+M6App.PlayerLabel(filter)+" 相关"),ModernOperationUI.Small);
            if(ModernOperationUI.Button(new Rect(Window.xMax-132,Window.yMax-34,106,25),"最新 ↓"))diceScroll.y=float.MaxValue;
            return;
        }
        var visible=entries.Where(e=>filter==null||e.Involves(filter)).ToArray();
        historyScroll=DrawRows(new Rect(Window.x+20,Window.y+124,Window.width-40,Window.height-166),visible,historyScroll,false);
        ModernOperationUI.Label(new Rect(Window.x+24,Window.yMax-30,650,22),"共 "+visible.Length+" 条",ModernOperationUI.Small);
        if(ModernOperationUI.Button(new Rect(Window.xMax-132,Window.yMax-34,106,25),"最新 ↓"))historyScroll.y=float.MaxValue;
    }
    static void DrawDiceStatistics(Rect r,TabletopDiceHistory history)
    {
        ModernOperationUI.Label(new Rect(r.x,r.y,45,24),"点数",ModernOperationUI.Small);
        ModernOperationUI.Label(new Rect(r.x+259,r.y,52,24),"次数",ModernOperationUI.Small);
        ModernOperationUI.Label(new Rect(r.x+318,r.y,102,24),"本局频率",ModernOperationUI.Small);
        for(int total=2;total<=12;total++)
        {
            float y=r.y+34+(total-2)*30;
            ModernOperationUI.Label(new Rect(r.x,y,40,25),total.ToString(),ModernOperationUI.Center);
            ModernOperationUI.Round(new Rect(r.x+48,y+8,188,8),ModernOperationUI.Surface);
            if(history.Count(total)>0)ModernOperationUI.Round(new Rect(r.x+48,y+8,188f*history.Count(total)/Math.Max(1,history.MaximumCount),8),ModernOperationUI.Accent);
            ModernOperationUI.Label(new Rect(r.x+247,y,64,25),history.Count(total).ToString(),ModernOperationUI.Center);
            ModernOperationUI.Label(new Rect(r.x+317,y,80,25),history.Total==0?"—":(history.Frequency(total)*100).ToString("0.0")+"%",ModernOperationUI.Center);
        }
    }
    Vector2 DrawDiceRows(Rect viewport,GameHistoryEntry[] entries,Vector2 position)
    {
        MobileTabletop.ScrollTouch(viewport,ref position);
        float width=viewport.width-22,total=entries.Length*44;
        position.y=Mathf.Clamp(position.y,0,Math.Max(0,total-viewport.height));
        position=GUI.BeginScrollView(viewport,position,new Rect(0,0,width,Math.Max(viewport.height,total)),false,false);
        for(int i=0;i<entries.Length;i++)
        {
            float y=i*44;if(y+44<position.y||y>position.y+viewport.height)continue;
            var entry=entries[i];
            ModernOperationUI.Fill(new Rect(0,y+8,3,22),BoardRenderer.SeatColor(entry.PlayerId));
            DrawCardLine(new Rect(12,y+5,185,30),"第 "+entry.Turn+" 回合 · ",entry.PlayerId,"",Array.Empty<GameHistoryChange>(),null,new GUIStyle(ModernOperationUI.Text){wordWrap=false});
            Die(new Rect(211,y+5,28,28),entry.Dice1,new Color(.64f,.11f,.08f),false);
            Die(new Rect(245,y+5,28,28),entry.Dice2,new Color(.94f,.90f,.80f),false);
            if(!string.IsNullOrEmpty(entry.EventDie))Die(new Rect(279,y+5,28,28),EventFace(entry.EventDie),new Color(.21f,.25f,.27f),true);
            ModernOperationUI.Label(new Rect(332,y+5,98,30),(entry.Dice1+entry.Dice2)+" 点",ModernOperationUI.Center);
            ModernOperationUI.Fill(new Rect(8,y+41,width-16,1),new Color(.24f,.32f,.31f,.45f));
        }
        if(entries.Length==0)ModernOperationUI.Label(new Rect(8,12,width-16,36),"尚未掷骰",ModernOperationUI.Small);
        GUI.EndScrollView();return position;
    }
    static GameHistoryEntry[] cachedEntries;
    static readonly System.Collections.Generic.Dictionary<GameHistoryEntry,TabletopHistoryLine[]> lines=new System.Collections.Generic.Dictionary<GameHistoryEntry,TabletopHistoryLine[]>();
    static TabletopHistoryLine[] Lines(GameHistoryEntry entry)
    {
        if(!lines.TryGetValue(entry,out var value))lines[entry]=value=TabletopHistoryModel.Lines(entry);
        return value;
    }
    static float Height(GameHistoryEntry entry,float width,bool compact)=>Lines(entry).Sum(line=>line.Trade&&line.ReceivedCards.Length>0?64:32)+30;
    Vector2 DrawRows(Rect viewport,GameHistoryEntry[] entries,Vector2 position,bool compact)
    {
        MobileTabletop.ScrollTouch(viewport,ref position);
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
    void DrawRow(Rect r,GameHistoryEntry entry,bool compact)
    {
        var turnStyle=new GUIStyle(ModernOperationUI.Small){wordWrap=false,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(0,0,0,0)};
        ModernOperationUI.Label(new Rect(r.x+9,r.y,r.width-18,26),"第 "+entry.Turn+" 回合",turnStyle);
        float y=r.y+26;
        foreach(var line in Lines(entry))
        {
            var style=new GUIStyle(ModernOperationUI.Text){fontSize=compact?11:15,wordWrap=false};
            DrawCardLine(new Rect(r.x+9,y,r.width-18,30),"",line.Player," "+line.Prefix,line.Cards,line.Suffix,style,
                line.Quote?line.ReceivedCards:null,line.Roll?entry:null,line.Trade?null:line.Partner,line.PartnerAction,line.Emphasis,line.Track);y+=32;
            if(line.Trade&&line.ReceivedCards.Length>0)
            {
                DrawCardLine(new Rect(r.x+9,y,r.width-18,30),"跟",line.Partner,"交易了",line.ReceivedCards,null,
                    new GUIStyle(ModernOperationUI.Text){fontSize=compact?11:15,wordWrap=false});y+=32;
            }
        }
        ModernOperationUI.Fill(new Rect(r.x+7,r.yMax-3,r.width-14,1),new Color(.24f,.32f,.31f,.45f));
    }
    void DrawCardLine(Rect r,string beforeName,string player,string action,GameHistoryChange[] cards,string suffix,GUIStyle style,
        GameHistoryChange[] requested=null,GameHistoryEntry roll=null,string partner=null,string partnerAction=null,string emphasis=null,int track=-1)
    {
        // 中文衬线字体使用完整行高，避免继承默认标签内边距后被短文本框裁掉笔画。
        style.padding=new RectOffset(0,0,0,0);style.alignment=TextAnchor.MiddleLeft;
        style.richText=false;
        var metrics=new GUIStyle(style);
        string name=PlayerIdentityModel.IsPlayerId(player)?M6App.PlayerLabel(player):player??"";
        float TextWidth(string value)=>metrics.CalcSize(new GUIContent(value??"")).x;
        float CardsWidth(GameHistoryChange[] values)=>values.Sum(c=>c.GoodIndex<0?TextWidth(Math.Abs(c.Amount)+"张手牌")+5:
            24+(Math.Abs(c.Amount)>1?TextWidth("×"+Math.Abs(c.Amount))+4:0));
        int diceCount=roll==null?0:string.IsNullOrEmpty(roll.EventDie)?2:3;
        string partnerName=PlayerIdentityModel.IsPlayerId(partner)?M6App.PlayerLabel(partner):partner??"";
        float width=TextWidth(beforeName)+TextWidth(name)+TextWidth(action)+5+TextWidth(partnerName)+TextWidth(partnerAction)+CardsWidth(cards)+TextWidth(suffix)+diceCount*32;
        if(requested!=null)width+=TextWidth(" → ")+CardsWidth(requested);
        float scale=Math.Min(1,r.width/Math.Max(1,width));
        style.fontSize=Math.Max(7,(int)(style.fontSize*scale));float x=r.x;
        // 姓名独立绘制；动作、数量、箭头及其余文字始终使用主题默认文字色。
        void Text(string value,GUIStyle textStyle,float padding=0)
        {
            float span=(TextWidth(value)+padding)*scale;
            ModernOperationUI.Label(new Rect(x,r.y,span,30),value??"",textStyle);x+=span;
        }
        void ColoredText(string value)
        {
            value=value??"";int at=string.IsNullOrEmpty(emphasis)?-1:value.IndexOf(emphasis,StringComparison.Ordinal);
            if(at<0||track<0){Text(value,style);return;}
            Text(value.Substring(0,at),style);
            var colored=new GUIStyle(style);colored.normal.textColor=track==0?new Color(.94f,.73f,.25f):track==1?new Color(.40f,.67f,.94f):new Color(.49f,.79f,.42f);
            Text(emphasis,colored);Text(value.Substring(at+emphasis.Length),style);
        }
        void Cards(GameHistoryChange[] values)
        {
            foreach(var card in values)
            {
                if(card.GoodIndex<0){Text(Math.Abs(card.Amount)+"张手牌",style,5);continue;}
                EntityCardAssets.DrawCardFace(new Rect(x,r.y+2,20*scale,28*scale),EntityCardAssets.ResourceOrCommodityFront(card.GoodIndex));x+=24*scale;
                if(Math.Abs(card.Amount)>1)Text("×"+Math.Abs(card.Amount),style,4);
            }
        }
        Text(beforeName,style);Text(name,M6App.PlayerNameStyle(style,player));ColoredText(action);Text("",style,5);
        if(partner!=null){Text(partnerName,M6App.PlayerNameStyle(style,partner));Text(partnerAction,style);}
        Cards(cards);
        if(requested!=null){Text(" → ",style);Cards(requested);}
        ColoredText(suffix);
        if(roll!=null)
        {
            Die(new Rect(x,r.y+2,26*scale,26*scale),roll.Dice1,new Color(.64f,.11f,.08f),false);x+=32*scale;
            Die(new Rect(x,r.y+2,26*scale,26*scale),roll.Dice2,new Color(.94f,.90f,.80f),false);x+=32*scale;
            if(diceCount==3)Die(new Rect(x,r.y+2,26*scale,26*scale),EventFace(roll.EventDie),new Color(.21f,.25f,.27f),true);
        }
    }
    // 骰面使用真实圆点与事件符号；基础版只有两骰，不伪造第三骰。
    static int EventFace(string value) => value=="Trade"?1:value=="Politics"?2:value=="Science"?3:0;
    void Die(Rect r,int value,Color color,bool eventDie)
    {
        ModernOperationUI.Round(new Rect(r.x+1,r.y+2,r.width,r.height),new Color(0,0,0,.45f));
        if(eventDie)
        {
            GUI.DrawTexture(r,diceArt.EventDieTexture(value),ScaleMode.StretchToFill,true,0,Color.white,0,Mathf.Min(8,Mathf.Min(r.width,r.height)/2));return;
        }
        ModernOperationUI.Round(r,color);
        Color ink=color.r>.8f?new Color(.15f,.14f,.13f):new Color(.99f,.95f,.85f);
        float dotSize=r.width*4/26;
        Action<float,float> dot=(x,y)=>TabletopUI.Token(new Rect(r.x+r.width*x-dotSize/2,r.y+r.height*y-dotSize/2,dotSize,dotSize),ink);
        if(value%2==1)dot(.5f,.5f);
        if(value>=2){dot(.25f,.25f);dot(.75f,.75f);}
        if(value>=4){dot(.75f,.25f);dot(.25f,.75f);}
        if(value==6){dot(.25f,.5f);dot(.75f,.5f);}
    }
}
