using UnityEngine;

// 铜木美术皮肤：缓存正式PNG，九宫格保留铆钉与倒角；透明中心露出原卡面和骰子。
public static class CopperTabletopSkin
{
    static Texture2D disc, wood, track, thumb, thumbHover;
    static ArtPatch panelFrame, buttonPlate, cardFrame;
    static readonly Rect DiscUv = SourceRect(43, 38, 1168, 1171, 1254, 1254);
    static GUISkin scrollSkin;
    static int scrollVersion=-1;
    static Texture2D jadeTrack,jadeThumb,jadeHover;
    static bool loaded;
    static readonly Color Highlight=new Color(1f,.74f,.51f),Dark=new Color(.16f,.065f,.025f);
    public static void Scrollbars()
    {
        if(scrollSkin==GUI.skin&&scrollVersion==StrategyHudTheme.Version)return;scrollSkin=GUI.skin;scrollVersion=StrategyHudTheme.Version;
        if(track==null)
        {
            track=BarTexture(new Color(.12f,.045f,.018f),false);
            thumb=BarTexture(new Color(.64f,.36f,.17f),true);
            thumbHover=BarTexture(new Color(.85f,.55f,.28f),true);
        }
        if(jadeTrack==null)
        {
            jadeTrack=BarTexture(new Color(.035f,.15f,.11f),false);
            jadeThumb=BarTexture(new Color(.58f,.77f,.62f),true);
            jadeHover=BarTexture(new Color(.81f,.91f,.72f),true);
        }
        var activeTrack=StrategyHudTheme.Jade?jadeTrack:track;
        var activeThumb=StrategyHudTheme.Jade?jadeThumb:thumb;
        var activeHover=StrategyHudTheme.Jade?jadeHover:thumbHover;
        ConfigureBar(scrollSkin.horizontalScrollbar,activeTrack,activeTrack);
        ConfigureBar(scrollSkin.verticalScrollbar,activeTrack,activeTrack);
        ConfigureBar(scrollSkin.horizontalScrollbarThumb,activeThumb,activeHover);
        ConfigureBar(scrollSkin.verticalScrollbarThumb,activeThumb,activeHover);
        ConfigureBar(scrollSkin.horizontalScrollbarLeftButton,activeThumb,activeHover);
        ConfigureBar(scrollSkin.horizontalScrollbarRightButton,activeThumb,activeHover);
        ConfigureBar(scrollSkin.verticalScrollbarUpButton,activeThumb,activeHover);
        ConfigureBar(scrollSkin.verticalScrollbarDownButton,activeThumb,activeHover);
    }
    static void ConfigureBar(GUIStyle style,Texture2D normal,Texture2D hover)
    {
        style.normal.background=normal;style.hover.background=hover;style.active.background=hover;
        style.border=new RectOffset(4,4,4,4);
    }
    static Texture2D BarTexture(Color face,bool metal)
    {
        const int n=16;var pixels=new Color32[n*n];
        for(int y=0;y<n;y++)for(int x=0;x<n;x++)
        {
            int edge=Mathf.Min(Mathf.Min(x,y),Mathf.Min(n-1-x,n-1-y));
            var color=edge==0?Dark:edge==1&&metal?Highlight:Color.Lerp(face,Dark,x/(float)n*.40f);
            pixels[y*n+x]=color.linear;
        }
        var value=new Texture2D(n,n,TextureFormat.RGBA32,false){name="铜木滚动条",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
        value.SetPixels32(pixels);value.Apply(false,true);return value;
    }
    // 原图完整保留，UV只裁掉生图留白。以原图像素定义切片，再换成归一化坐标。
    sealed class ArtPatch
    {
        public readonly Texture2D Texture;
        public readonly Rect Source;
        public readonly float SliceX, SliceY;
        public ArtPatch(string name, int width, int height, Rect bounds, float corner)
        {
            Texture=Resources.Load<Texture2D>("TabletopUI/v014/"+name);
            Source=SourceRect(bounds.x,bounds.y,bounds.width,bounds.height,width,height);
            SliceX=corner/width;SliceY=corner/height;
        }
    }
    static Rect SourceRect(float x,float y,float width,float height,float canvasWidth,float canvasHeight)
    {
        return new Rect(x/canvasWidth,1-(y+height)/canvasHeight,width/canvasWidth,height/canvasHeight);
    }
    public static void Warm()
    {
        if(loaded)return;loaded=true;
        wood=Resources.Load<Texture2D>("TabletopUI/v013/walnut-inlay");
        disc=Resources.Load<Texture2D>("TabletopUI/v014/round-button");
        panelFrame=new ArtPatch("panel-frame",1254,1254,new Rect(22,25,1212,1189),310);
        buttonPlate=new ArtPatch("button-plate",2171,724,new Rect(24,99,2125,502),150);
        cardFrame=new ArtPatch("card-frame",1024,1536,new Rect(23,28,978,1471),160);
    }
    static void DrawPatch(Rect r,ArtPatch art,float corner,bool center,Color tint)
    {
        if(art.Texture==null)return;
        var before=GUI.color;bool enabled=GUI.enabled;GUI.enabled=true;
        GUI.color=before*tint;
        float c=Mathf.Min(corner,Mathf.Min(r.width,r.height)*.45f);
        for(int row=0;row<3;row++)for(int col=0;col<3;col++)
        {
            if(row==1&&col==1&&!center)continue;
            float x=col==0?r.x:col==1?r.x+c:r.xMax-c;
            float y=row==0?r.y:row==1?r.y+c:r.yMax-c;
            float w=col==1?r.width-2*c:c,h=row==1?r.height-2*c:c;
            float u=col==0?art.Source.x:col==1?art.Source.x+art.SliceX:art.Source.xMax-art.SliceX;
            float v=row==0?art.Source.yMax-art.SliceY:row==1?art.Source.y+art.SliceY:art.Source.y;
            float uw=col==1?art.Source.width-2*art.SliceX:art.SliceX;
            float vh=row==1?art.Source.height-2*art.SliceY:art.SliceY;
            GUI.DrawTextureWithTexCoords(new Rect(x,y,w,h),art.Texture,new Rect(u,v,uw,vh));
        }
        GUI.color=before;GUI.enabled=enabled;
    }
    public static void Panel(Rect r,Color face,bool hover=false,bool ornaments=true)
    {
        if(Event.current.type!=EventType.Repaint||r.width<=0||r.height<=0)return;
        if(StrategyHudTheme.Jade){JadeTabletopSkin.Panel(r,face,hover,ornaments);return;}
        Warm();
        if(!ornaments)
        {
            float brightness=Mathf.Max(.01f,Mathf.Max(face.r,Mathf.Max(face.g,face.b)));
            var tint=Color.Lerp(Color.white,new Color(face.r/brightness,face.g/brightness,face.b/brightness),.12f);
            if(hover)tint*=new Color(1.18f,1.12f,.96f);
            if(!GUI.enabled)tint*=new Color(.82f,.82f,.82f);
            DrawPatch(r,buttonPlate,14,true,tint);
            return;
        }
        bool enabled=GUI.enabled;GUI.enabled=true;
        ModernOperationUI.Round(new Rect(r.x+1,r.y+4,r.width,r.height),new Color(.035f,.015f,.005f,.48f));
        ModernOperationUI.Round(r,(hover?Color.Lerp(face,Highlight,.10f):face).linear);
        if(wood!=null)
        {
            var inset=new Rect(r.x+5,r.y+5,r.width-10,r.height-10);
            GUI.DrawTexture(inset,wood,ScaleMode.StretchToFill,true,0,new Color(.40f,.24f,.14f,.32f),0,5);
        }
        Outline(r,hover,20);GUI.enabled=enabled;
    }
    public static void Outline(Rect r,bool selected=false,float corner=16)
    {
        if(Event.current.type!=EventType.Repaint||r.width<=0||r.height<=0)return;Warm();
        if(StrategyHudTheme.Jade){JadeTabletopSkin.Frame(r,selected,false);return;}
        DrawPatch(r,panelFrame,corner,false,selected?new Color(1.15f,1.16f,.92f):Color.white);
    }
    public static void Disc(Rect r,Color face,bool hover)
    {
        if(Event.current.type!=EventType.Repaint||r.width<=0||r.height<=0)return;Warm();
        if(StrategyHudTheme.Jade){JadeTabletopSkin.Disc(r,hover);return;}
        if(disc==null)return;
        var before=GUI.color;bool enabled=GUI.enabled;GUI.enabled=true;
        GUI.color=before*new Color(0,0,0,.36f);
        GUI.DrawTextureWithTexCoords(new Rect(r.x+1,r.y+3,r.width,r.height),disc,DiscUv);
        GUI.color=before*(hover?new Color(1.12f,1.08f,.94f):Color.white);
        GUI.DrawTextureWithTexCoords(r,disc,DiscUv);
        GUI.color=before;
        // 设施席位的颜色标识仍单独绘制，不烘焙进空白按钮贴图。
        if(face.g>face.r*1.05f||face.b>face.r*1.05f)
            TabletopUI.Token(new Rect(r.x+r.width*.16f,r.y+r.height*.16f,r.width*.68f,r.height*.68f),face);
        GUI.enabled=enabled;
    }
    public static void Count(Rect r,bool selected=false)
    {
        ModernOperationUI.Round(r,(StrategyHudTheme.Jade?new Color(.70f,.85f,.70f):new Color(.89f,.75f,.53f)).linear);Outline(r,selected,9);
    }
    public static void Card(Rect r,bool selected)
    {
        if(Event.current.type!=EventType.Repaint||r.width<=0||r.height<=0)return;Warm();
        if(StrategyHudTheme.Jade){JadeTabletopSkin.Frame(r,selected,true);return;}
        // 使用独立卡框，不再叠画两圈程序描边；卡牌插画与原始比例不变。
        DrawPatch(r,cardFrame,13,false,selected?new Color(1.3f,1.5f,.78f):Color.white);
    }
}
