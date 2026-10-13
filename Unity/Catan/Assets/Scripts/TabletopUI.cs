using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 席位与分数均来自当前 PlayerView；对手只展示牌背、总数及公开分数。
public sealed class TabletopSeat
{
    public string Id;
    public int Points, Resources, Cards, Roads, Settlements, Cities, Ships;
    public int[] Improvements;
    public TabletopScoreBreakdown Score;
}

public sealed class TabletopUI
{
    public static readonly Rect ContextRect = new Rect(982, 315, 432, 390);
    public static readonly Rect EndTurnRect = new Rect(1290,842,122,43);
    internal static readonly Rect SpecialRect = new Rect(1278,744,144,43);
    readonly List<Rect> hits = new List<Rect>();
    readonly List<TabletopWorldObjects.Hit> worldHits = new List<TabletopWorldObjects.Hit>();
    TabletopScene table;
    public void BindTable(TabletopScene scene) { table=scene; }
    GUIStyle text, small, center, title, button;
    Vector2 scroll;
    bool contextVisible;
    static Texture2D disc;
    readonly TabletopDicePresentation dice = new TabletopDicePresentation();
    readonly TabletopHistoryUI history = new TabletopHistoryUI();
    readonly TabletopBankUI bank = new TabletopBankUI();
    readonly TabletopDefenseUI defense = new TabletopDefenseUI();
    public void BindDefense(int position,int strength,int threat) { defense.Bind(position,strength,threat); }
    public TabletopCollectionTimeline Collections => bank.Collections;
    public void BindCollectionOcclusion(Action<TabletopCollectionFlight> draw) { bank.OccludeHand=draw; }
    TabletopScoreBreakdown hoveredScore;
    Rect scoreAnchor;
    OperationSnapshot operations;
    Action purchaseDevelopment;
    Catan.Core.GameHistoryEntry[] bankHistory = Array.Empty<Catan.Core.GameHistoryEntry>();
    public void BindOperations(OperationSnapshot snapshot,Action purchase,Catan.Core.GameHistoryEntry[] entries)
    {operations=snapshot;purchaseDevelopment=purchase;bankHistory=entries??Array.Empty<Catan.Core.GameHistoryEntry>();}
    public bool HistoryOpen => history.IsOpen;
    public bool TryCloseOverlay() => history.TryClose();
    public bool BlocksOverlayInput(Vector2 p) => history.BlocksInput(p)||contextVisible&&ContextRect.Contains(p);
    public void DrawHistory(Font font,Catan.Core.GameHistoryEntry[] entries,string[] players)
    {if(!history.IsOpen)bank.DrawFlights();history.Draw(font,entries,players);if(!history.IsOpen)DrawScoreDetails();}
    public bool DiceRolling => dice.IsRolling(Time.unscaledTimeAsDouble);
    public void ObserveDice(long sequence,int first,int second,int? eventDie)
    {dice.Observe(sequence,first,second,eventDie,Time.unscaledTimeAsDouble);}
    static readonly Color Cream = new Color(.96f,.88f,.70f), Ink = new Color(.24f,.12f,.06f);
    public static readonly Color HotNumberColor = new Color(1f,.10f,.075f);
    public bool BlocksBoardInput(Vector2 p) => !TabletopScene.BoardArea.Contains(p) || history.BlocksInput(p) || defense.BlocksInput(p) || hits.Any(r=>r.Contains(p)) || worldHits.Any(h=>h.Contains(p)) || contextVisible && ContextRect.Contains(p);
    internal static string[] OpponentOrder(string ownSeat,System.Collections.Generic.IEnumerable<string> ids)
    {
        var ordered=ids.OrderBy(id=>int.Parse(id.Substring(1))).ToArray();int own=Array.IndexOf(ordered,ownSeat);
        return Enumerable.Range(1,Math.Max(0,ordered.Length-1)).Select(i=>ordered[(own+i)%ordered.Length]).ToArray();
    }
    public void RegisterContext(){contextVisible=true;}
    public static bool ContainsMapScreenPoint(Vector2 point) =>
        TabletopScene.BoardArea.Contains(new Vector2(point.x*1440/Screen.width,point.y*900/Screen.height));
    public static void DrawMapLabels(Action draw)
    {
        // 地图与桌面物件共用完整画面，文字只裁剪在屏幕边界。
        var area=TabletopScene.BoardArea;
        GUI.BeginClip(area,new Vector2(-area.x,-area.y),Vector2.zero,false);
        try{draw();}finally{GUI.EndClip();}
    }
    public static void Fill(Rect r,Color c)
    {var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
    public static void Note(Rect r)
    {Fill(new Rect(r.x+4,r.y+6,r.width,r.height),new Color(.05f,.02f,0,.32f));Fill(r,Cream);}
    public static void Token(Rect r,Color color)
    {
        if(disc==null)
        {
            disc=new Texture2D(96,96,TextureFormat.RGBA32,false);var pixels=new Color[96*96];
            for(int y=0;y<96;y++)for(int x=0;x<96;x++)
            {float d=Vector2.Distance(new Vector2(x,y),new Vector2(47.5f,47.5f));pixels[y*96+x]=new Color(1,1,1,Mathf.Clamp01(47-d));}
            disc.SetPixels(pixels);disc.Apply();
        }
        var old=GUI.color;GUI.color=new Color(0,0,0,.32f);GUI.DrawTexture(new Rect(r.x+2,r.y+3,r.width,r.height),disc);
        GUI.color=color;GUI.DrawTexture(r,disc);GUI.color=old;
    }
    void Styles(Font font)
    {
        if(text!=null)return;
        text=new GUIStyle(GUI.skin.label){font=font,fontSize=16,wordWrap=true};text.normal.textColor=Cream;
        small=new GUIStyle(text){fontSize=12};center=new GUIStyle(text){alignment=TextAnchor.MiddleCenter};
        title=new GUIStyle(center){fontSize=20,fontStyle=FontStyle.Bold};
        button=new GUIStyle(GUI.skin.button){font=font,fontSize=14,wordWrap=true};
        button.normal.textColor=Ink;button.hover.textColor=Ink;button.active.textColor=Ink;
    }
    bool ObjectButton(Rect r,string label,bool enabled=true)
    {
        hits.Add(r);return ModernOperationUI.Button(r,label,enabled,r==EndTurnRect);
    }
    public void Draw(Font font,string seat,string actor,string phase,int turn,int ownPoints,int dice1,int dice2,
        TabletopSeat[] seats,bool canRoll,bool canEnd,Action roll,Action end,Action<string> trade,
        Action special=null,string specialLabel="",string detail="")
    {
        Styles(font);ModernOperationUI.Styles(font);hits.Clear();worldHits.Clear();hoveredScore=null;table.Objects.Begin("public");
        // 临时便签覆盖的桌面物件不能先于上层按钮响应同一次点击。
        bool tableEnabled=GUI.enabled;
        if(Event.current.type!=EventType.Repaint&&BlocksOverlayInput(Event.current.mousePosition))GUI.enabled=false;
        contextVisible=false;
        var others=OpponentOrder(seat,seats.Select(p=>p.Id)).Select(id=>seats.First(p=>p.Id==id)).ToArray();
        for(int i=0;i<others.Length;i++)
        {
            // 顺时针相对座位：自己的席位始终在画面前方。
            int place=others.Length==2?(i==0?0:2):i;
            DrawSeat(others[i],place,others[i].Id==actor,trade);
        }
        GUI.Label(new Rect(302,21,288,29),turn+" 回合  ·  "+actor+"  ·  "+phase,new GUIStyle(center){fontSize=14});
        var ownScoreRect=new Rect(600,866,240,30);
        var ownScore=seats.FirstOrDefault(p=>p.Id==seat)?.Score;
        GUI.Label(ownScoreRect,seat+"   "+(ownScore==null?ownPoints:ownScore.Total)+" 分",title);
        hits.Add(ownScoreRect);ScoreHover(ownScoreRect,ownScore);
        defense.Draw();
        bank.Draw(table,operations,bankHistory,seats,trade,purchaseDevelopment,worldHits,hits);
        bool expansion=dice.Event.HasValue;
        var diceRects=expansion?new[]{new Rect(1260,246,42,42),new Rect(1342,246,42,42),new Rect(1301,196,42,42)}
            :new[]{new Rect(1260,246,42,42),new Rect(1342,246,42,42)};
        bool enabled=GUI.enabled;
        for(int i=0;i<diceRects.Length;i++)
        {
            double now=Time.unscaledTimeAsDouble;
            var die=table.Objects.Die("dice-"+i,diceRects[i],i,dice.Face(i,now),dice.Progress(now),DiceRolling);worldHits.Add(die);
            GUI.enabled=enabled&&canRoll&&!DiceRolling;
            if(GUI.enabled&&die.Contains(Event.current.mousePosition)&&Event.current.type==EventType.MouseDown&&Event.current.button==0)
            {Event.current.Use();roll?.Invoke();}
        }
        GUI.enabled=enabled;
        table.Objects.End();
        if(ObjectButton(EndTurnRect,"结束回合",canEnd&&!DiceRolling))end?.Invoke();
        if(special!=null&&ObjectButton(SpecialRect,specialLabel))special();
        GUI.enabled=tableEnabled;
    }
    void DrawSeat(TabletopSeat p,int place,bool active,Action<string> trade)
    {
        bool top=place==1;
        var layout=OpponentSeatLayout(place);var seatRect=table.ProjectLabel(layout);hits.Add(seatRect);
        var activeRect=top?new Rect(610,5,12,12):MirrorSideRect(new Rect(37,291,12,12),place);
        if(active)Token(table.ProjectLabel(activeRect),new Color(1,.79f,.29f));
        var label=new GUIStyle(center){fontSize=p.Improvements!=null&&!top?12:14,fontStyle=FontStyle.Bold};
        string levels=p.Improvements!=null&&p.Improvements.Length>=3&&seatRect.Contains(Event.current.mousePosition)?" · 贸"+p.Improvements[0]+" 政"+p.Improvements[1]+" 科"+p.Improvements[2]:"";
        var scoreRect=new Rect(layout.x,layout.y-4,layout.width,26);
        TableLabel(scoreRect,p.Id+"   "+(p.Score==null?p.Points:p.Score.PublicPoints)+" 分"+levels,label);
        ScoreHover(table.ProjectLabel(scoreRect),p.Score);
        DrawStock("road",p.Roads,p.Id,place);
        DrawStock("settlement",p.Settlements,p.Id,place);
        DrawStock("city",p.Cities,p.Id,place);
        DrawStock("ship",p.Ships,p.Id,place);
        var back=EntityCardAssets.Texture(EntityCardAssets.Good(0).Back);
        int shown=Math.Min(6,p.Resources+p.Cards);
        for(int i=0;i<shown;i++)
        {
            var card=table.Objects.Card("opponent-"+p.Id+"-card-"+i,OpponentHandCardLayout(place,i,shown),back);worldHits.Add(card);
        }
        var countRect=top?new Rect(640,69,160,22):new Rect(layout.x,482,layout.width,22);
        TableLabel(countRect,p.Resources+" 资源 · "+p.Cards+" 卡",new GUIStyle(center){fontSize=12});
        // 浮层与银行在视觉上盖住席位时，不允许整块席位热区先吞掉点击。
        bool covered=SpecialRect.Contains(Event.current.mousePosition)||history.BlocksInput(Event.current.mousePosition)
            ||table.ProjectLabel(TabletopBankUI.Area).Contains(Event.current.mousePosition);
        if(!covered&&GUI.Button(seatRect,GUIContent.none,GUIStyle.none))trade?.Invoke(p.Id);
    }
    internal static Rect OpponentSeatLayout(int place) => place==1?new Rect(420,6,600,156):MirrorSideRect(new Rect(28,286,210,222),place);
    static Rect MirrorSideRect(Rect rect,int place) => place==2?new Rect(1440-rect.xMax,rect.y,rect.width,rect.height):rect;
    internal static Rect OpponentHandCardLayout(int place,int index,int count)
    {
        float center=place==1?720:133;
        var card=new Rect(center-((count-1)*30+27)/2f+index*30,place==1?29:448,27,39);
        return place==1?card:MirrorSideRect(card,place);
    }
    internal static Vector2 OpponentStockPosition(int place,string kind,int index)
    {
        Vector2 point;
        if(place==1)
        {
            // 上方手牌靠外、库存靠内，与自己的桌边顺序上下对应。
            float x=kind=="settlement"?466:kind=="road"?546:kind=="city"?698:810;
            point=OperationDock.StockPieceLayout(new Rect(x,648,OperationDock.StockGroupWidth(kind),60),kind,index);
            point.y=819-point.y;
        }
        else
        {
            point=kind=="road"?new Vector2(70+(index%8)*18,324+(index/8)*14):
                kind=="settlement"?new Vector2(43+index*20,372):
                kind=="city"?new Vector2(148+index*29,372):
                new Vector2(69+(index%5)*32,397+(index/5)*18);
            if(place==2)point.x=1440-point.x;
        }
        return point;
    }
    void DrawStock(string kind,int count,string seat,int place)
    {
        for(int i=0;i<count;i++)
        {
            var piece=table.Objects.Piece("opponent-"+seat+"-"+kind+"-"+i,kind,seat,1,
                OpponentStockPosition(place,kind,i));
            if(piece!=null)worldHits.Add(piece);
        }
    }
    void TableLabel(Rect r,string value,GUIStyle style)
    {GUI.Label(table.ProjectLabel(r),value,new GUIStyle(style){fontSize=Mathf.Max(9,Mathf.RoundToInt(style.fontSize*table.DisplayScale))});}
    void ScoreHover(Rect anchor,TabletopScoreBreakdown score)
    {
        if(score==null||!GUI.enabled||history.IsOpen||!anchor.Contains(Event.current.mousePosition))return;
        hoveredScore=score;scoreAnchor=anchor;
    }
    void DrawScoreDetails()
    {
        if(hoveredScore==null||history.IsOpen)return;
        var score=hoveredScore;float height=118+Mathf.Max(1,score.Rows.Length)*60;
        float x=Mathf.Clamp(scoreAnchor.center.x-182,12,1440-376);
        float y=scoreAnchor.y>450?scoreAnchor.y-height-10:scoreAnchor.yMax+10;
        var panel=new Rect(x,Mathf.Clamp(y,12,900-height-12),364,height);
        ModernOperationUI.Panel(panel);
        var titleStyle=new GUIStyle(ModernOperationUI.Title){padding=new RectOffset(),alignment=TextAnchor.MiddleLeft,wordWrap=false};
        GUI.Label(new Rect(panel.x+18,panel.y+12,328,40),score.PlayerId+" · 分数构成",titleStyle);
        string total=score.IsOwn?"总分 "+score.Total+"  ·  明分 "+score.PublicPoints+"  ·  暗分 "+score.HiddenPoints:"公开得分 "+score.PublicPoints+" 分";
        var bodyStyle=new GUIStyle(ModernOperationUI.Text){padding=new RectOffset(),alignment=TextAnchor.MiddleLeft,wordWrap=false};
        var detailStyle=new GUIStyle(ModernOperationUI.Small){padding=new RectOffset(),alignment=TextAnchor.MiddleLeft,wordWrap=false};
        GUI.Label(new Rect(panel.x+18,panel.y+55,328,32),total,bodyStyle);
        float rowY=panel.y+98;
        if(score.Rows.Length==0)GUI.Label(new Rect(panel.x+18,rowY,328,32),"尚未获得胜利点",detailStyle);
        foreach(var row in score.Rows)
        {
            var nameStyle=new GUIStyle(bodyStyle);
            if(row.Hidden)nameStyle.normal.textColor=ModernOperationUI.Accent;
            GUI.Label(new Rect(panel.x+18,rowY,254,32),row.Name,nameStyle);
            GUI.Label(new Rect(panel.x+282,rowY,64,32),row.Points+" 分",new GUIStyle(nameStyle){alignment=TextAnchor.MiddleRight});
            GUI.Label(new Rect(panel.x+18,rowY+31,328,25),row.Detail,detailStyle);
            rowY+=60;
        }
    }
    public void BeginContext(string caption)
    {
        contextVisible=true;ModernOperationUI.Panel(ContextRect);
        GUI.Label(new Rect(ContextRect.x+18,ContextRect.y+12,ContextRect.width-36,30),caption,ModernOperationUI.Title);
        GUILayout.BeginArea(new Rect(ContextRect.x+16,ContextRect.y+44,ContextRect.width-32,ContextRect.height-55));
        scroll=GUILayout.BeginScrollView(scroll);
    }
    public void EndContext(){GUILayout.EndScrollView();GUILayout.EndArea();}
    public void DrawContext(string caption,Action content){BeginContext(caption);content();EndContext();}
    public static void NumberToken(Vector2 p,int? number,string fallback,GUIStyle style)
    {
        // 数字已放回棋盘的实体点数牌，服从三维深度；沙漠保留原始地貌。
        if(number.HasValue||fallback=="沙漠")return;
        if(!number.HasValue&&string.IsNullOrEmpty(fallback))return;
        var r=new Rect(p.x-19,p.y-16,38,32);Token(r,Cream);
        var s=new GUIStyle(style){fontSize=number.HasValue?21:12,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
        s.normal.textColor=number==6||number==8?HotNumberColor:Ink;
        GUI.Label(r,number.HasValue?number.ToString():fallback,s);
    }
}
