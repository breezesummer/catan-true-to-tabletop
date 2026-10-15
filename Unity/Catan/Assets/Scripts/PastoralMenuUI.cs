using UnityEngine;

// 现代田园菜单控件。与桌内主题分离，不改变任何规则和存档字段。
public static class PastoralMenuUI
{
    public static readonly Color Ink=new Color(.15f,.28f,.25f),Muted=new Color(.43f,.50f,.42f);
    public static readonly Color Forest=new Color(.20f,.39f,.31f),Sage=new Color(.88f,.91f,.83f),Paper=new Color(.985f,.973f,.925f,.97f);
    static Font currentFont;
    static Texture2D neutral,hover,selected,field;
    public static GUIStyle Text,Small,Title,Center,Control,Eyebrow,Hero,HeroSmall;
    public static void Styles(Font font=null)
    {
        if(Text!=null&&(font==null||font==currentFont))return;if(font!=null)currentFont=font;
        Text=new GUIStyle(GUI.skin.label){font=currentFont,fontSize=18,wordWrap=true,padding=new RectOffset()};Text.normal.textColor=Ink;
        Small=new GUIStyle(Text){fontSize=13};Small.normal.textColor=Muted;
        Title=new GUIStyle(Text){fontSize=27,fontStyle=FontStyle.Bold};
        Center=new GUIStyle(Text){fontSize=17,alignment=TextAnchor.MiddleCenter};
        Eyebrow=new GUIStyle(Text){fontSize=12,fontStyle=FontStyle.Bold};Eyebrow.normal.textColor=Forest;
        Hero=new GUIStyle(Text){fontSize=55,fontStyle=FontStyle.Bold,wordWrap=false};
        HeroSmall=new GUIStyle(Text){fontSize=20};HeroSmall.normal.textColor=new Color(.24f,.39f,.34f);
        if(neutral==null)
        {
            neutral=ControlTexture(new Color(.91f,.92f,.86f));hover=ControlTexture(new Color(.85f,.89f,.79f));
            selected=ControlTexture(Forest);field=ControlTexture(new Color(1,.995f,.97f));
        }
        Control=new GUIStyle(GUI.skin.button){font=currentFont,fontSize=17,wordWrap=true,border=new RectOffset(12,12,12,12),padding=new RectOffset(12,12,5,5),margin=new RectOffset(3,3,3,3),alignment=TextAnchor.MiddleCenter};
        Control.normal.background=neutral;Control.normal.textColor=Ink;Control.hover.background=hover;Control.hover.textColor=Ink;
        Control.active.background=selected;Control.active.textColor=Color.white;Control.focused.background=hover;Control.focused.textColor=Ink;
        Control.onNormal.background=selected;Control.onNormal.textColor=Color.white;Control.onHover.background=selected;Control.onHover.textColor=Color.white;Control.onActive.background=selected;Control.onActive.textColor=Color.white;
    }
    public static GUIStyle Field { get { Styles();var s=new GUIStyle(Control);s.normal.background=field;s.alignment=TextAnchor.MiddleLeft;return s; } }
    public static void Fill(Rect r,Color c){var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
    public static void Round(Rect r,Color c,float radius=16)
    {if(r.width<=0||r.height<=0)return;GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,c,0,Mathf.Min(radius,Mathf.Min(r.width,r.height)*.5f));}
    public static void Panel(Rect r)
    {
        Round(new Rect(r.x-5,r.y+14,r.width+10,r.height+4),new Color(.12f,.28f,.23f,.08f),28);
        Round(new Rect(r.x-2,r.y+6,r.width+4,r.height+2),new Color(.12f,.28f,.23f,.09f),25);
        Round(r,Paper,24);Round(new Rect(r.x+1,r.y+1,r.width-2,r.height-2),new Color(1,1,.98f,.26f),23);
    }
    public static bool Button(Rect r,string caption,bool enabled=true,bool primary=false,bool selected=false)
    {
        Styles();bool before=GUI.enabled;GUI.enabled=before&&enabled;
        bool over=GUI.enabled&&r.Contains(Event.current.mousePosition);
        Color bg=primary||selected?Forest:new Color(.915f,.931f,.877f);
        if(over)bg=Color.Lerp(bg,primary||selected?Color.white:Forest,primary||selected?.08f:.08f);
        if(!GUI.enabled)bg=new Color(.91f,.91f,.87f);
        if(primary)Round(new Rect(r.x,r.y+3,r.width,r.height),new Color(.12f,.27f,.20f,.18f),12);
        Round(r,bg,primary?13:10);
        if(selected&&!primary)Round(new Rect(r.x+9,r.center.y-2,4,4),new Color(.94f,.86f,.61f),2);
        var ink=GUI.enabled?(primary||selected?new Color(1,.99f,.94f):Ink):Muted;
        var style=new GUIStyle(Center){fontSize=primary?19:16,fontStyle=primary?FontStyle.Bold:FontStyle.Normal};style.normal.textColor=ink;
        GUI.Label(new Rect(r.x+8,r.y+1,r.width-16,r.height-2),caption,style);
        bool hit=GUI.Button(r,GUIContent.none,GUIStyle.none)&&(!MobileTabletop.Enabled||!MobileTabletop.TouchDragged);
        GUI.enabled=before;if(hit)TabletopAudio.Instance?.Click();return hit;
    }
    public static void Section(Rect r,string caption,string detail="")
    {Styles();GUI.Label(r,caption,Eyebrow);if(!string.IsNullOrEmpty(detail)){var s=new GUIStyle(Small){alignment=TextAnchor.UpperRight};GUI.Label(r,detail,s);}}
    public static int Segments(Rect r,int value,string[] options)
    {
        float gap=7,w=(r.width-gap*(options.Length-1))/options.Length;
        for(int i=0;i<options.Length;i++)if(Button(new Rect(r.x+i*(w+gap),r.y,w,r.height),options[i],true,false,value==i))value=i;return value;
    }
    public static void ScrollIndicator(Rect viewport,float contentHeight,float scrollY)
    {
        if(contentHeight<=viewport.height)return;
        // 保留原生滚动条的鼠标命中和拖动，只将显示换成与纸面一致的细轨。
        Fill(new Rect(viewport.xMax-18,viewport.y,18,viewport.height),new Color(.985f,.980f,.939f));
        var track=new Rect(viewport.xMax-8,viewport.y+7,4,viewport.height-14);
        Round(track,new Color(.78f,.82f,.72f),2);
        float thumb=Mathf.Max(28,track.height*viewport.height/contentHeight),range=contentHeight-viewport.height;
        Round(new Rect(track.x,track.y+Mathf.Clamp01(scrollY/range)*(track.height-thumb),track.width,thumb),new Color(.44f,.58f,.43f),2);
    }
    static Texture2D ControlTexture(Color color)
    {
        const int size=32;const float radius=10;var p=new Color[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {float dx=Mathf.Max(radius-x-.5f,x+.5f-(size-radius)),dy=Mathf.Max(radius-y-.5f,y+.5f-(size-radius));float d=Mathf.Sqrt(Mathf.Max(0,dx)*Mathf.Max(0,dx)+Mathf.Max(0,dy)*Mathf.Max(0,dy));var c=color;c.a=Mathf.Clamp01(radius-d+.5f);p[y*size+x]=c;}
        var t=new Texture2D(size,size,TextureFormat.RGBA32,false){name="田园菜单圆角控件 v001",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};t.SetPixels(p);t.Apply(false,true);return t;
    }
}
