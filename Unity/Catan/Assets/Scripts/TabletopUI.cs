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
    public static readonly Rect ContextRect = new Rect(1010, 365, 404, 265);
    readonly List<Rect> hits = new List<Rect>();
    GUIStyle text, small, center, title, button;
    Vector2 scroll;
    bool contextVisible;
    static Texture2D disc;
    static Texture2D dieSurface;
    readonly TabletopDicePresentation dice = new TabletopDicePresentation();
    public bool DiceRolling => dice.IsRolling(Time.unscaledTimeAsDouble);
    public void ObserveDice(long sequence,int first,int second,int? eventDie)
    {dice.Observe(sequence,first,second,eventDie,Time.unscaledTimeAsDouble);}
    static readonly Color Cream = new Color(.96f,.88f,.70f), Ink = new Color(.24f,.12f,.06f);
    public bool BlocksBoardInput(Vector2 p) => hits.Any(r=>r.Contains(p)) || contextVisible && ContextRect.Contains(p);
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
        hits.Add(r);Note(r);bool prior=GUI.enabled;GUI.enabled=prior&&enabled;
        var style=new GUIStyle(center);style.normal.textColor=enabled?Ink:new Color(.52f,.43f,.34f);
        bool clicked=GUI.Button(r,label,style);GUI.enabled=prior;return clicked;
    }
    public void Draw(Font font,string seat,string actor,string phase,int turn,int ownPoints,int dice1,int dice2,
        TabletopSeat[] seats,bool canRoll,bool canEnd,Action roll,Action end,Action<string> trade,
        Action special=null,string specialLabel="",string detail="")
    {
        Styles(font);hits.Clear();
        // 临时便签覆盖的桌面物件不能先于上层按钮响应同一次点击。
        bool tableEnabled=GUI.enabled;
        if(contextVisible&&ContextRect.Contains(Event.current.mousePosition))GUI.enabled=false;
        contextVisible=false;
        var others=OpponentOrder(seat,seats.Select(p=>p.Id)).Select(id=>seats.First(p=>p.Id==id)).ToArray();
        for(int i=0;i<others.Length;i++)
        {
            // 顺时针相对座位：自己的席位始终在画面前方。
            int place=others.Length==2?(i==0?0:2):i;
            DrawSeat(others[i],place,others[i].Id==actor,trade);
        }
        GUI.Label(new Rect(500,121,440,27),"第 "+turn+" 回合  ·  "+actor+"  ·  "+phase,center);
        GUI.Label(new Rect(18,828,126,51),seat+" · 你   "+ownPoints+" 分",title);
        if(!string.IsNullOrEmpty(detail))GUI.Label(new Rect(24,662,181,52),detail,small);
        var bank=new Rect(28,532,157,68);hits.Add(bank);
        for(int i=0;i<3;i++)
        {
            var r=new Rect(36+i*40,534-i*3,43,49);Note(r);
            OperationIconAssets.DrawGood(new Rect(r.x+6,r.y+7,31,31),i,false);
        }
        GUI.Label(new Rect(28,579,157,24),"银行 / 港口 · 交换",center);
        if(GUI.Button(bank,GUIContent.none,GUIStyle.none))trade?.Invoke(null);
        bool expansion=dice.Event.HasValue;
        var diceRects=expansion?new[]{new Rect(1234,551,50,50),new Rect(1295,560,50,50),new Rect(1355,545,50,50)}
            :new[]{new Rect(1247,545,59,59),new Rect(1330,558,59,59)};
        bool enabled=GUI.enabled;
        for(int i=0;i<diceRects.Length;i++)
        {
            hits.Add(diceRects[i]);
            DrawAnimatedDie(diceRects[i],i,expansion);
            GUI.enabled=enabled&&canRoll&&!DiceRolling;
            if(GUI.Button(diceRects[i],GUIContent.none,GUIStyle.none))roll?.Invoke();
        }
        GUI.enabled=enabled;
        if(ObjectButton(new Rect(1248,479,153,43),"交出回合  →",canEnd&&!DiceRolling))end?.Invoke();
        if(special!=null&&ObjectButton(new Rect(28,482,159,36),specialLabel))special();
        GUI.enabled=tableEnabled;
    }
    void DrawSeat(TabletopSeat p,int place,bool active,Action<string> trade)
    {
        bool top=place==1;float x=top?456:place==0?22:1239,y=top?12:197;
        float width=top?529:180;var seatRect=new Rect(x,y,width,top?105:260);hits.Add(seatRect);
        Color color=BoardRenderer.SeatColor(p.Id);
        if(active){Token(new Rect(x-1,y+1,12,12),new Color(1,.79f,.29f));}
        var label=new GUIStyle(center){fontSize=p.Improvements!=null&&!top?12:14,fontStyle=FontStyle.Bold};
        string levels=p.Improvements!=null&&p.Improvements.Length>=3?" · 贸"+p.Improvements[0]+" 政"+p.Improvements[1]+" 科"+p.Improvements[2]:"";
        GUI.Label(new Rect(x,y-4,width,26),p.Id+"   "+p.Points+" 分"+levels,label);
        DrawStock("road",p.Roads,p.Id,x,y+27,top?200:174,top?18:25,8);
        DrawStock("settlement",p.Settlements,p.Id,top?x+203:x,y+(top?26:82),top?115:100,28,5);
        DrawStock("city",p.Cities,p.Id,top?x+320:x+95,y+(top?26:82),top?100:85,30,4);
        if(p.Ships>0)DrawStock("ship",p.Ships,p.Id,x,y+(top?65:122),top?200:172,top?22:18,8);
        float cx=top?x+433:x+32,cy=top?y+29:y+166;
        var back=EntityCardAssets.Texture(EntityCardAssets.Good(0).Back);
        int shown=Math.Min(6,p.Resources+p.Cards);
        for(int i=0;i<shown;i++)
        {
            var r=new Rect(cx+i*12,cy+Math.Abs(i-2)*2,40,56);Note(r);
            if(back!=null)GUI.DrawTexture(new Rect(r.x+1,r.y+1,r.width-2,r.height-2),back,ScaleMode.StretchToFill);
        }
        GUI.Label(new Rect(top?cx-8:x,top?y+86:y+236,top?115:180,22),p.Resources+" 资源 · "+p.Cards+" 卡",new GUIStyle(center){fontSize=12});
        if(GUI.Button(seatRect,GUIContent.none,GUIStyle.none))trade?.Invoke(p.Id);
    }
    static void DrawStock(string kind,int count,string seat,float x,float y,float width,float size,int row)
    {
        var texture=OperationPieceAssets.Texture(kind,seat,1);if(texture==null)return;
        for(int i=0;i<count;i++)GUI.DrawTexture(new Rect(x+(i%row)*Math.Min(size,width/row),y+(i/row)*size*.65f,size+8,size+8),texture,ScaleMode.ScaleToFit);
    }
    void DrawAnimatedDie(Rect r,int index,bool expansion)
    {
        double now=Time.unscaledTimeAsDouble;
        float progress=dice.Progress(now),motion=DiceRolling?1-progress:0;
        float spin=DiceRolling?(1-Mathf.Pow(1-progress,3))*720:0;
        r.position+=new Vector2(Mathf.Sin(progress*Mathf.PI*6+index)*motion*10,-Mathf.Abs(Mathf.Sin(progress*Mathf.PI*5))*motion*19);
        var matrix=GUI.matrix;
        GUIUtility.RotateAroundPivot((index==0?-9:index==1?12:-5)+spin*(index%2==0?1:-1),r.center);
        Color color=index==0?new Color(.72f,.12f,.08f):index==1?expansion?new Color(.98f,.96f,.88f):new Color(.97f,.76f,.28f):new Color(.97f,.94f,.83f);
        DrawDieSurface(r,color);
        int value=dice.Face(index,now);
        if(index==2){DrawEventSymbol(r,value);GUI.matrix=matrix;return;}
        var dots=value==1?new[]{4}:value==2?new[]{0,8}:value==3?new[]{0,4,8}:value==4?new[]{0,2,6,8}:value==5?new[]{0,2,4,6,8}:new[]{0,2,3,5,6,8};
        Color pip=index==0?new Color(.99f,.95f,.85f):new Color(.16f,.11f,.07f);
        foreach(int d in dots)Token(new Rect(r.x+r.width*(.19f+(d%3)*.25f),r.y+r.height*(.19f+(d/3)*.25f),r.width*.12f,r.height*.12f),pip);
        GUI.matrix=matrix;
    }
    static void DrawDieSurface(Rect r,Color color)
    {
        if(dieSurface==null)
        {
            const int size=128;dieSurface=new Texture2D(size,size,TextureFormat.RGBA32,false){name="圆角骰面 v003"};
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float dx=Mathf.Max(0,Mathf.Abs(x-63.5f)-48),dy=Mathf.Max(0,Mathf.Abs(y-63.5f)-48);
                float edge=15-Mathf.Sqrt(dx*dx+dy*dy),alpha=Mathf.Clamp01(edge);
                float shade=.84f+.12f*y/size+.06f*Mathf.Clamp01(edge/5);
                pixels[y*size+x]=new Color(shade,shade,shade,alpha);
            }
            dieSurface.SetPixels(pixels);dieSurface.Apply();
        }
        var old=GUI.color;
        GUI.color=new Color(0,0,0,.26f);GUI.DrawTexture(new Rect(r.x+3,r.y+6,r.width,r.height),dieSurface);
        GUI.color=color*.78f;GUI.DrawTexture(new Rect(r.x,r.y+3,r.width,r.height),dieSurface);
        GUI.color=color;GUI.DrawTexture(r,dieSurface);GUI.color=old;
    }
    static void DrawEventSymbol(Rect r,int value)
    {
        float unit=r.width/50,x=r.x+10*unit,y=r.y+10*unit;
        Color ink=value==1?new Color(.79f,.52f,.045f):value==2?new Color(.10f,.28f,.69f):value==3?new Color(.10f,.49f,.22f):new Color(.16f,.13f,.10f);
        if(value==0)
        {
            // 蛮族船：船体、桅杆和两片帆。
            Fill(new Rect(x+2*unit,y+21*unit,26*unit,4*unit),ink);
            Fill(new Rect(x+5*unit,y+25*unit,20*unit,3*unit),ink);
            Fill(new Rect(x+14*unit,y,2*unit,23*unit),ink);
            for(int row=0;row<16;row++)
            {Fill(new Rect(x+(14-row*.6f)*unit,y+(row+2)*unit,(row*.6f)*unit,unit),ink);Fill(new Rect(x+17*unit,y+(row+2)*unit,(15-row)*.55f*unit,unit),ink);}
        }
        else
        {
            // 黄、蓝、绿城堡分别对应贸易、政治、科学。
            Fill(new Rect(x+3*unit,y+9*unit,24*unit,18*unit),ink);
            for(int i=0;i<3;i++)Fill(new Rect(x+(3+i*9)*unit,y+3*unit,6*unit,8*unit),ink);
            Fill(new Rect(x+12*unit,y+18*unit,6*unit,9*unit),new Color(.97f,.94f,.83f));
            Fill(new Rect(x+7*unit,y+12*unit,3*unit,5*unit),new Color(.97f,.94f,.83f));
            Fill(new Rect(x+20*unit,y+12*unit,3*unit,5*unit),new Color(.97f,.94f,.83f));
        }
    }
    public void BeginContext(string caption)
    {
        contextVisible=true;Note(ContextRect);
        var inkTitle=new GUIStyle(title);inkTitle.normal.textColor=Ink;
        GUI.Label(new Rect(ContextRect.x+12,ContextRect.y+7,ContextRect.width-24,30),caption,inkTitle);
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
