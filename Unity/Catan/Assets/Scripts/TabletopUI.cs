using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 只接收 PlayerView 中的公开席位数据；对手只展示牌背与总数。
public sealed class TabletopSeat
{
    public string Id;
    public int Points, Resources, Cards, Roads, Settlements, Cities, Ships;
    public int[] Improvements;
}

public sealed class TabletopUI
{
    public static readonly Rect ContextRect = new Rect(982, 315, 432, 390);
    public static readonly Rect EndTurnRect = new Rect(1290,842,122,43);
    static readonly Rect SpecialRect = new Rect(18,68,190,36);
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
    public bool HistoryOpen => history.IsOpen;
    public bool BlocksOverlayInput(Vector2 p) => history.BlocksInput(p)||contextVisible&&ContextRect.Contains(p);
    public void DrawHistory(Font font,Catan.Core.GameHistoryEntry[] entries,string[] players)
    {history.Draw(font,entries,players);}
    public bool DiceRolling => dice.IsRolling(Time.unscaledTimeAsDouble);
    public void ObserveDice(long sequence,int first,int second,int? eventDie)
    {dice.Observe(sequence,first,second,eventDie,Time.unscaledTimeAsDouble);}
    static readonly Color Cream = new Color(.96f,.88f,.70f), Ink = new Color(.24f,.12f,.06f);
    public bool BlocksBoardInput(Vector2 p) => history.BlocksInput(p) || hits.Any(r=>r.Contains(p)) || worldHits.Any(h=>h.Contains(p)) || contextVisible && ContextRect.Contains(p);
    internal static string[] OpponentOrder(string ownSeat,System.Collections.Generic.IEnumerable<string> ids)
    {
        var ordered=ids.OrderBy(id=>int.Parse(id.Substring(1))).ToArray();int own=Array.IndexOf(ordered,ownSeat);
        return Enumerable.Range(1,Math.Max(0,ordered.Length-1)).Select(i=>ordered[(own+i)%ordered.Length]).ToArray();
    }
    public void RegisterContext(){contextVisible=true;}
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
        Styles(font);ModernOperationUI.Styles(font);hits.Clear();worldHits.Clear();table.Objects.Begin("public");
        // 临时便签覆盖的桌面物件不能先于上层按钮响应同一次点击。
        bool tableEnabled=GUI.enabled;
        if(BlocksOverlayInput(Event.current.mousePosition))GUI.enabled=false;
        contextVisible=false;
        var others=OpponentOrder(seat,seats.Select(p=>p.Id)).Select(id=>seats.First(p=>p.Id==id)).ToArray();
        for(int i=0;i<others.Length;i++)
        {
            // 顺时针相对座位：自己的席位始终在画面前方。
            int place=others.Length==2?(i==0?0:2):i;
            DrawSeat(others[i],place,others[i].Id==actor,trade);
        }
        GUI.Label(new Rect(500,121,440,27),"第 "+turn+" 回合  ·  "+actor+"  ·  "+phase,center);
        GUI.Label(new Rect(18,842,170,38),seat+" · 你   "+ownPoints+" 分",title);
        if(!string.IsNullOrEmpty(detail))GUI.Label(new Rect(24,662,181,52),detail,small);
        var bank=table.ProjectLabel(new Rect(28,434,157,68));hits.Add(bank);
        for(int i=0;i<3;i++)
        {
            var card=table.Objects.Card("bank-card-"+i,new Rect(36+i*45,436,40,57),EntityCardAssets.ResourceFront(i));worldHits.Add(card);
        }
        TableLabel(new Rect(28,481,157,24),"银行 / 港口 · 交换",center);
        if(GUI.Button(bank,GUIContent.none,GUIStyle.none))trade?.Invoke(null);
        bool expansion=dice.Event.HasValue;
        var diceRects=expansion?new[]{new Rect(1258,548,48,48),new Rect(1333,548,48,48),new Rect(1295,485,48,48)}
            :new[]{new Rect(1258,548,48,48),new Rect(1333,548,48,48)};
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
        bool top=place==1;float x=top?456:place==0?22:1239,y=top?12:197;
        float width=top?529:180;var seatRect=table.ProjectLabel(new Rect(x,y,width,top?105:260));hits.Add(seatRect);
        Color color=BoardRenderer.SeatColor(p.Id);
        if(active){Token(table.ProjectLabel(new Rect(x-1,y+1,12,12)),new Color(1,.79f,.29f));}
        var label=new GUIStyle(center){fontSize=p.Improvements!=null&&!top?12:14,fontStyle=FontStyle.Bold};
        string levels=p.Improvements!=null&&p.Improvements.Length>=3?" · 贸"+p.Improvements[0]+" 政"+p.Improvements[1]+" 科"+p.Improvements[2]:"";
        TableLabel(new Rect(x,y-4,width,26),p.Id+"   "+p.Points+" 分"+levels,label);
        DrawStock("road",p.Roads,p.Id,x,y+27,top?200:174,top?18:25,8);
        DrawStock("settlement",p.Settlements,p.Id,top?x+203:x,y+(top?26:82),top?115:100,28,5);
        DrawStock("city",p.Cities,p.Id,top?x+320:x+95,y+(top?26:82),top?100:85,30,4);
        if(p.Ships>0)DrawStock("ship",p.Ships,p.Id,x,y+(top?65:122),top?200:172,top?22:18,8);
        float cx=top?x+433:x+32,cy=top?y+29:y+166;
        var back=EntityCardAssets.Texture(EntityCardAssets.Good(0).Back);
        int shown=Math.Min(top?3:6,p.Resources+p.Cards);
        for(int i=0;i<shown;i++)
        {
            var card=table.Objects.Card("opponent-"+p.Id+"-card-"+i,new Rect(top?cx+i*30:x+3+i*30,cy,27,39),back);worldHits.Add(card);
        }
        TableLabel(new Rect(top?cx-8:x,top?y+86:y+236,top?115:180,22),p.Resources+" 资源 · "+p.Cards+" 卡",new GUIStyle(center){fontSize=12});
        // 浮层与银行在视觉上盖住席位时，不允许整块席位热区先吞掉点击。
        bool covered=SpecialRect.Contains(Event.current.mousePosition)||history.BlocksInput(Event.current.mousePosition)
            ||table.ProjectLabel(new Rect(28,434,157,68)).Contains(Event.current.mousePosition);
        if(!covered&&GUI.Button(seatRect,GUIContent.none,GUIStyle.none))trade?.Invoke(p.Id);
    }
    void DrawStock(string kind,int count,string seat,float x,float y,float width,float size,int row)
    {
        for(int i=0;i<count;i++)
        {
            var piece=table.Objects.Piece("opponent-"+seat+"-"+kind+"-"+i,kind,seat,1,
                new Vector2(x+(i%row)*Math.Min(size,width/row)+size/2,y+(i/row)*size+size/2));
            if(piece!=null)worldHits.Add(piece);
        }
    }
    void TableLabel(Rect r,string value,GUIStyle style)
    {GUI.Label(table.ProjectLabel(r),value,new GUIStyle(style){fontSize=Mathf.Max(9,Mathf.RoundToInt(style.fontSize*table.DisplayScale))});}
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
        if(!number.HasValue&&string.IsNullOrEmpty(fallback))return;
        var r=new Rect(p.x-19,p.y-16,38,32);Token(r,Cream);
        var s=new GUIStyle(style){fontSize=number.HasValue?21:12,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
        s.normal.textColor=number==6||number==8?new Color(.69f,.10f,.04f):Ink;
        GUI.Label(r,number.HasValue?number.ToString():fallback,s);
    }
}
