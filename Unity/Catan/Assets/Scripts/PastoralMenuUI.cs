using UnityEngine;
using System.Collections.Generic;

// 中世纪田园菜单控件。与桌内主题分离，不改变规则和存档字段。
public static class PastoralMenuUI
{
    public static readonly Color Ink=new Color(.15f,.28f,.25f),Muted=new Color(.43f,.50f,.42f);
    public static readonly Color Forest=new Color(.20f,.39f,.31f),Sage=new Color(.88f,.91f,.83f),Paper=new Color(.985f,.973f,.925f,.97f);
    public static readonly Color Gold=new Color(.76f,.63f,.38f),Ivory=new Color(.98f,.96f,.88f);
    static Font currentFont;
    static Texture2D neutral,hover,selected,field;
    static readonly Dictionary<int,GUIStyle> labelStyles=new Dictionary<int,GUIStyle>();
    static GUIStyle fieldStyle,sliderTrack,sliderThumb;
    public static GUIStyle Text,Small,Title,Center,Control,Eyebrow,Hero,HeroSmall;
    public static void Styles(Font font=null)
    {
        if(Text!=null&&(font==null||font==currentFont))return;if(font!=null)currentFont=font;
        labelStyles.Clear();
        WarmFont();
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
        fieldStyle=new GUIStyle(Control);fieldStyle.normal.background=field;fieldStyle.alignment=TextAnchor.MiddleLeft;
        sliderTrack=new GUIStyle(GUI.skin.horizontalSlider){fixedHeight=6,border=new RectOffset(8,8,2,2)};
        sliderTrack.normal.background=neutral;
        sliderThumb=new GUIStyle(GUI.skin.horizontalSliderThumb){fixedWidth=18,fixedHeight=18};
        sliderThumb.normal.background=selected;sliderThumb.hover.background=hover;sliderThumb.active.background=selected;
    }
    public static GUIStyle Field { get { Styles();return fieldStyle; } }
    static void WarmFont()
    {
        if(currentFont==null||!currentFont.dynamic)return;
        // 菜单独立使用随包中文字体。先准备各字号，避免换页时图集扩容使前面的标题暂时消失。
        const string common="0123456789P /·→‹›×−+% 开始游戏加载局域网联机在线设置选择玩法地图同桌人数你的席位当前AI行动节奏慢速观摩标准快速下一步上完成启程房间信息规则与地图加入创建连接服务声音外观总量操作效海岸环境静开启关闭恢复推荐胜利目标保存取消继续旅返回主页";
        foreach(int size in new[]{11,12,13,14,15,16,17,18,19,20,22,23,24,25,27,28,31})
        {
            currentFont.RequestCharactersInTexture(common,size,FontStyle.Normal);
            currentFont.RequestCharactersInTexture(common,size,FontStyle.Bold);
        }
        currentFont.RequestCharactersInTexture("卡坦岛",67,FontStyle.Bold);
        currentFont.RequestCharactersInTexture("0123456789",66,FontStyle.Bold);
        currentFont.RequestCharactersInTexture("开一桌新冒险回到熟悉的岛屿设置局域网联机在线游戏",31,FontStyle.Bold);
        currentFont.RequestCharactersInTexture("基础版航海家城市与骑士双扩展组合驶向新海岸四座岛屿迷雾岛屿穿越沙漠被遗忘的部落卡坦的布匹海盗群岛卡坦奇观新世界",22,FontStyle.Bold);
    }
    public static void Fill(Rect r,Color c){var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
    public static void Round(Rect r,Color c,float radius=16)
    {if(r.width<=0||r.height<=0)return;GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,c,0,Mathf.Min(radius,Mathf.Min(r.width,r.height)*.5f));}
    public static void Panel(Rect r)
    {
        Round(new Rect(r.x-5,r.y+14,r.width+10,r.height+4),new Color(.12f,.28f,.23f,.08f),28);
        Round(new Rect(r.x-2,r.y+6,r.width+4,r.height+2),new Color(.12f,.28f,.23f,.09f),25);
        Round(r,Paper,24);Round(new Rect(r.x+1,r.y+1,r.width-2,r.height-2),new Color(1,1,.98f,.26f),23);
    }
    public static bool Button(Rect r,string caption,bool enabled=true,bool primary=false,bool selected=false,Color? seatColor=null)
    {
        Styles();bool before=GUI.enabled;GUI.enabled=before&&enabled;
        bool over=GUI.enabled&&r.Contains(Event.current.mousePosition);
        Color bg=primary||selected?Forest:new Color(.915f,.931f,.877f);
        if(over)bg=Color.Lerp(bg,primary||selected?Color.white:Forest,primary||selected?.08f:.08f);
        if(!GUI.enabled)bg=new Color(.91f,.91f,.87f);
        if(primary)Round(new Rect(r.x,r.y+3,r.width,r.height),new Color(.12f,.27f,.20f,.18f),12);
        Round(r,bg,primary?13:10);
        var ink=GUI.enabled?(primary||selected?new Color(1,.99f,.94f):Ink):Muted;
        if(seatColor.HasValue)ink=seatColor.Value;
        Label(new Rect(r.x+8,r.y+1,r.width-16,r.height-2),caption,primary?19:16,ink,primary,TextAnchor.MiddleCenter);
        bool hit=GUI.Button(r,GUIContent.none,GUIStyle.none)&&(!MobileTabletop.Enabled||!MobileTabletop.TouchDragged);
        GUI.enabled=before;if(hit)TabletopAudio.Instance?.Click();return hit;
    }
    public static bool ReturnButton(Rect r,Font font=null)
    {
        Styles();bool over=GUI.enabled&&r.Contains(Event.current.mousePosition);
        var trim=GUI.enabled?new Color(.79f,.68f,.43f,.92f):new Color(.43f,.47f,.36f,.65f);
        var face=over?new Color(.20f,.34f,.27f,.98f):new Color(.105f,.22f,.18f,.96f);
        Round(new Rect(r.x,r.y+4,r.width,r.height),new Color(.025f,.065f,.045f,.30f),14);
        Round(r,trim,14);
        Round(new Rect(r.x+1,r.y+1,r.width-2,r.height-2),face,13);
        Round(new Rect(r.x+9,r.y+3,r.width-18,1),new Color(1,.96f,.77f,over?.24f:.12f),1);
        Fill(new Rect(r.x+30,r.center.y,r.width*.10f,1),new Color(trim.r,trim.g,trim.b,.48f));
        Fill(new Rect(r.xMax-30-r.width*.10f,r.center.y,r.width*.10f,1),new Color(trim.r,trim.g,trim.b,.48f));
        Round(new Rect(r.x+21,r.center.y-2,4,4),trim,2);Round(new Rect(r.xMax-25,r.center.y-2,4,4),trim,2);
        var style=new GUIStyle(Center){font=font??currentFont,fontSize=17,fontStyle=FontStyle.Bold,wordWrap=false};
        style.normal.textColor=GUI.enabled?Ivory:Muted;
        GUI.Label(new Rect(r.x+44,r.y+1,r.width-88,r.height-2),"返回主菜单",style);
        bool hit=GUI.Button(r,GUIContent.none,GUIStyle.none)&&(!MobileTabletop.Enabled||!MobileTabletop.TouchDragged);
        if(hit)TabletopAudio.Instance?.Click();return hit;
    }
    public static void Section(Rect r,string caption,string detail="")
    {Styles();GUI.Label(r,caption,Eyebrow);if(!string.IsNullOrEmpty(detail)){var s=new GUIStyle(Small){alignment=TextAnchor.UpperRight};GUI.Label(r,detail,s);}}
    public static void Label(Rect r,string text,int size=18,Color? color=null,bool bold=false,TextAnchor align=TextAnchor.UpperLeft)
    {
        Styles();int key=size*32+(int)align*2+(bold?1:0);
        if(!labelStyles.TryGetValue(key,out var s))
        {
            s=new GUIStyle(Text){fontSize=size,fontStyle=bold?FontStyle.Bold:FontStyle.Normal,alignment=align};
            labelStyles.Add(key,s);
        }
        s.normal.textColor=color??Ink;GUI.Label(r,text,s);
    }
    public static bool Nav(Rect r,string text,string detail,bool selected,float blend)
    {
        bool over=GUI.enabled&&r.Contains(Event.current.mousePosition);
        float value=selected?1:blend;
        Round(r,new Color(.97f,.96f,.87f,.04f+.12f*value),9);
        if(selected)Fill(new Rect(r.x,r.y+13,2,r.height-26),Gold);
        Label(new Rect(r.x+23+4*value,r.y+10,r.width-58,30),text,24,Ivory,selected);
        if(value>.05f)Label(new Rect(r.x+25,r.y+43,r.width-56,20),detail,11,new Color(.87f,.88f,.76f,value));
        Label(new Rect(r.xMax-33,r.y+15,24,30),selected||over?"›":"·",23,new Color(.91f,.83f,.63f,.7f));
        bool hit=GUI.Button(r,GUIContent.none,GUIStyle.none);
        if(hit)TabletopAudio.Instance?.Click();return hit;
    }
    public static bool Card(Rect r,string heading,string detail,bool selected,string number="")
    {
        bool hit=Button(r,"",true,false,selected);
        Color ink=selected?Ivory:Ink,sub=selected?new Color(.80f,.87f,.76f):Muted;
        if(number.Length>0)Label(new Rect(r.x+22,r.y+15,r.width-44,22),number,12,selected?Gold:Muted);
        Label(new Rect(r.x+22,r.y+(number.Length>0?42:18),r.width-44,34),heading,22,ink,true);
        Label(new Rect(r.x+22,r.y+(number.Length>0?84:58),r.width-44,r.height-65),detail,13,sub);
        return hit;
    }
    public static float Slider(Rect r,float value)
    {
        // 原生滑块保留键鼠/触控输入，轨道与滑块使用统一的森林配色。
        Styles();
        return GUI.HorizontalSlider(r,value,0,1,sliderTrack,sliderThumb);
    }
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
