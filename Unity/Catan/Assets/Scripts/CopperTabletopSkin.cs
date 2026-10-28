using UnityEngine;

// 铜边桌游皮肤：贴图只初始化一次；透明镶边可覆盖三维骰盘与库存而不遮住棋子。
public static class CopperTabletopSkin
{
    static Texture2D frame, disc, wood, track, thumb, thumbHover;
    static GUISkin scrollSkin;
    static bool loaded;
    static readonly Color Highlight=new Color(1f,.74f,.51f),Dark=new Color(.16f,.065f,.025f);
    public static void Scrollbars()
    {
        if(scrollSkin==GUI.skin)return;scrollSkin=GUI.skin;
        if(track==null)
        {
            track=BarTexture(new Color(.12f,.045f,.018f),false);
            thumb=BarTexture(new Color(.64f,.36f,.17f),true);
            thumbHover=BarTexture(new Color(.85f,.55f,.28f),true);
        }
        ConfigureBar(scrollSkin.horizontalScrollbar,track,track);
        ConfigureBar(scrollSkin.verticalScrollbar,track,track);
        ConfigureBar(scrollSkin.horizontalScrollbarThumb,thumb,thumbHover);
        ConfigureBar(scrollSkin.verticalScrollbarThumb,thumb,thumbHover);
        ConfigureBar(scrollSkin.horizontalScrollbarLeftButton,thumb,thumbHover);
        ConfigureBar(scrollSkin.horizontalScrollbarRightButton,thumb,thumbHover);
        ConfigureBar(scrollSkin.verticalScrollbarUpButton,thumb,thumbHover);
        ConfigureBar(scrollSkin.verticalScrollbarDownButton,thumb,thumbHover);
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
    public static void Warm()
    {
        if(loaded)return;loaded=true;
        wood=Resources.Load<Texture2D>("TabletopUI/v013/walnut-inlay");
        frame=Make(false);disc=Make(true);
    }
    static Texture2D Make(bool circle)
    {
        const int n=128;var pixels=new Color32[n*n];
        for(int y=0;y<n;y++)for(int x=0;x<n;x++)
        {
            float px=x+.5f,py=y+.5f,dx=px-64,dy=py-64;
            float qx=Mathf.Abs(dx)-47,qy=Mathf.Abs(dy)-47;
            float distance=circle?Mathf.Sqrt(dx*dx+dy*dy)-61:
                new Vector2(Mathf.Max(qx,0),Mathf.Max(qy,0)).magnitude+Mathf.Min(Mathf.Max(qx,qy),0)-14;
            float depth=-distance;
            Color color=Color.clear;
            if(depth>=-1&&depth<=13)
            {
                float band=depth<2?.12f:depth<4?.9f:depth<7?.46f:depth<9?.16f:depth<11?.95f:.22f;
                float lighting=.84f+(-dx+dy)/350;
                color=Color.Lerp(Dark,Highlight,Mathf.Clamp01(band*lighting));
                color.a=Mathf.Clamp01(depth+1)*Mathf.Clamp01(13-depth);
            }
            if(circle&&depth>13)
            {
                float light=Mathf.Clamp01(.48f+dy/180-dx/300);
                color=Color.Lerp(new Color(.20f,.075f,.032f),new Color(.49f,.23f,.11f),light);
                float grain=Mathf.Sin(py*.7f+Mathf.Sin(px*.12f))*.012f;
                color.r+=grain;color.g+=grain;color.b+=grain;
            }
            // 四角的铜铆钉，圆片改为四个斜向铆钉。
            float pos=circle?22:14;
            float rx=Mathf.Min(Mathf.Abs(px-pos),Mathf.Abs(px-(128-pos)));
            float ry=Mathf.Min(Mathf.Abs(py-pos),Mathf.Abs(py-(128-pos)));
            float rivet=Mathf.Sqrt(rx*rx+ry*ry);
            if(rivet<5.5f)
            {
                float ring=rivet>4.5f?.1f:rivet>3.4f?.95f:rivet>2.2f?.35f:.72f;
                color=Color.Lerp(Dark,Highlight,ring);color.a=1;
            }
            pixels[y*n+x]=color.linear;
        }
        var texture=new Texture2D(n,n,TextureFormat.RGBA32,false){name=circle?"铜木圆形按钮 v013":"铜质九宫格透明镶边 v013",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
        texture.SetPixels32(pixels);texture.Apply(false,true);return texture;
    }
    public static void Panel(Rect r,Color face,bool hover=false,bool ornaments=true)
    {
        if(Event.current.type!=EventType.Repaint||r.width<=0||r.height<=0)return;
        Warm();bool enabled=GUI.enabled;GUI.enabled=true;
        ModernOperationUI.Round(new Rect(r.x+1,r.y+4,r.width,r.height),new Color(.035f,.015f,.005f,.48f));
        ModernOperationUI.Round(r,(hover?Color.Lerp(face,Highlight,.13f):face).linear);
        if(wood!=null)
        {
            var inset=new Rect(r.x+5,r.y+5,r.width-10,r.height-10);
            GUI.DrawTexture(inset,wood,ScaleMode.StretchToFill,true,0,new Color(.40f,.24f,.14f,.32f),0,5);
        }
        Outline(r,hover,ornaments?20:10);GUI.enabled=enabled;
    }
    public static void Outline(Rect r,bool selected=false,float corner=16)
    {
        if(Event.current.type!=EventType.Repaint||r.width<=0||r.height<=0)return;Warm();
        var before=GUI.color;bool enabled=GUI.enabled;GUI.enabled=true;
        GUI.color=selected?new Color(1.35f,1.18f,.68f,1):Color.white;
        // 固定四角，中心透明。细小控件收缩角饰，不将整张边框拉伸。
        float c=Mathf.Min(corner,Mathf.Min(r.width,r.height)*.45f);const float uv=.25f;
        for(int row=0;row<3;row++)for(int col=0;col<3;col++)
        {
            if(row==1&&col==1)continue;
            float x=col==0?r.x:col==1?r.x+c:r.xMax-c,y=row==0?r.y:row==1?r.y+c:r.yMax-c;
            float w=col==1?r.width-2*c:c,h=row==1?r.height-2*c:c;
            float u=col==0?0:col==1?uv:1-uv,v=row==0?1-uv:row==1?uv:0;
            GUI.DrawTextureWithTexCoords(new Rect(x,y,w,h),frame,new Rect(u,v,col==1?1-2*uv:uv,row==1?1-2*uv:uv));
        }
        GUI.color=before;GUI.enabled=enabled;
    }
    public static void Disc(Rect r,Color face,bool hover)
    {
        if(Event.current.type!=EventType.Repaint)return;Warm();
        var before=GUI.color;bool enabled=GUI.enabled;GUI.enabled=true;
        GUI.color=new Color(0,0,0,.4f);GUI.DrawTexture(new Rect(r.x+1,r.y+4,r.width,r.height),disc);
        GUI.color=hover?new Color(1.12f,1.07f,.91f):Color.white;GUI.DrawTexture(r,disc);
        GUI.color=before;
        // 木色内盘保持统一，设施与席位以独立的小色盘标识。
        if(face.g>face.r*1.05f||face.b>face.r*1.05f)
            TabletopUI.Token(new Rect(r.x+r.width*.16f,r.y+r.height*.16f,r.width*.68f,r.height*.68f),face);
        GUI.enabled=enabled;
    }
    public static void Count(Rect r,bool selected=false)
    {
        ModernOperationUI.Round(r,new Color(.89f,.75f,.53f).linear);Outline(r,selected,9);
    }
    public static void Card(Rect r,bool selected)
    {
        Outline(r,selected,11);
        if(selected)
        {
            var old=GUI.color;GUI.color=new Color(1,.84f,.22f,.8f);
            Outline(new Rect(r.x+2,r.y+2,r.width-4,r.height-4),true,9);GUI.color=old;
        }
    }
}
