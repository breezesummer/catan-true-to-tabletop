using UnityEngine;

// 海洋概念图的独立图集；只绘制，不创建交互区域或改动布局。
public static class OceanTabletopSkin
{
    static Texture2D atlas,smallDisc,panelFill,compassArt;
    public static bool Ready { get { Warm(); return atlas != null&&compassArt != null; } }
    static void Warm()
    {
        if(atlas==null)atlas=Resources.Load<Texture2D>("TabletopUI/v017/ocean-atlas");
        if(compassArt==null)compassArt=Resources.Load<Texture2D>("TabletopUI/v018/ocean-compass");
    }
    // 实际生图为1254方图；按可见轮廓定位，不能假定生图遵守要求的像素尺寸。
    static Rect Region(int slot)
    {
        Rect p=slot==0?new Rect(44,47,538,502):slot==1?new Rect(663,17,550,552):slot==2?new Rect(44,670,539,528):new Rect(669,669,545,528);
        return new Rect(p.x/1254,1-p.yMax/1254,p.width/1254,p.height/1254);
    }
    static void Patch(Rect r,int slot,bool center,float corner,Color tint)
    {
        if(Event.current.type!=EventType.Repaint||r.width<=0||r.height<=0)return;
        Warm();if(atlas==null)return;
        Rect uv=Region(slot);float sx=68f/1254,sy=68f/1254;
        float c=Mathf.Min(corner,Mathf.Min(r.width,r.height)*.44f);
        Color before=GUI.color;bool enabled=GUI.enabled;GUI.enabled=true;
        GUI.color=before*tint*(enabled?Color.white:new Color(.65f,.72f,.75f,1));
        for(int y=0;y<3;y++)for(int x=0;x<3;x++)
        {
            if(!center&&x==1&&y==1)continue;
            var dest=new Rect(x==0?r.x:x==1?r.x+c:r.xMax-c,y==0?r.y:y==1?r.y+c:r.yMax-c,x==1?r.width-2*c:c,y==1?r.height-2*c:c);
            var source=new Rect(x==0?uv.x:x==1?uv.x+sx:uv.xMax-sx,y==0?uv.yMax-sy:y==1?uv.y+sy:uv.y,x==1?uv.width-2*sx:sx,y==1?uv.height-2*sy:sy);
            // 将切片边界对齐到物理像素，缩放时不产生缝隙或双层暗线。
            var a=GUIUtility.GUIToScreenPoint(dest.min);var b=GUIUtility.GUIToScreenPoint(dest.max);
            a=GUIUtility.ScreenToGUIPoint(new Vector2(Mathf.Round(a.x),Mathf.Round(a.y)));
            b=GUIUtility.ScreenToGUIPoint(new Vector2(Mathf.Round(b.x),Mathf.Round(b.y)));
            GUI.DrawTextureWithTexCoords(Rect.MinMaxRect(a.x,a.y,b.x,b.y),atlas,source);
        }
        GUI.color=before;GUI.enabled=enabled;
    }
    public static void Panel(Rect r,bool selected=false,bool ornaments=true)
    {
        if(Event.current.type!=EventType.Repaint)return;
        ModernOperationUI.Round(new Rect(r.x,r.y+2,r.width,r.height),new Color(0,.025f,.04f,.3f));
        if(panelFill==null)
        {
            panelFill=new Texture2D(1,64,TextureFormat.RGBA32,false){name="海洋通透渐变",hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp};
            for(int y=0;y<64;y++)panelFill.SetPixel(0,y,Color.Lerp(new Color(.018f,.085f,.12f,.94f),new Color(.035f,.19f,.23f,.88f),y/63f));
            panelFill.Apply(false,true);
        }
        GUI.DrawTexture(new Rect(r.x+3,r.y+3,r.width-6,r.height-6),panelFill);
        Bevel(r);
        Patch(r,ornaments?0:3,false,ornaments?20:12,selected?new Color(1.08f,1.15f,1.12f):Color.white);
        if(selected)Glow(r);
    }
    public static void Frame(Rect r,bool selected=false,bool card=false)
    {
        if(Event.current.type==EventType.Repaint)Bevel(r);
        Patch(r,card?2:0,false,card?13:18,Color.white);
        if(selected)Glow(r);
    }
    static void Bevel(Rect r)
    {
        // 两层固定描边形成金属明暗，沿原边界绘制，不扩大命中区域。
        GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,new Color(.32f,.22f,.09f,.95f),3,7);
        GUI.DrawTexture(new Rect(r.x+1,r.y+1,r.width-2,r.height-2),Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,new Color(.96f,.83f,.53f,.85f),1,6);
    }
    static void Glow(Rect r)
    {
        if(Event.current.type!=EventType.Repaint)return;
        // 固定细边而非每帧纹理生成；让玩家色及卡面保持原色。
        for(int i=3;i>=0;i--)
        {
            var c=new Color(.25f,1f,.88f,i==0?.85f:.10f);
            GUI.DrawTexture(new Rect(r.x-i,r.y-i,r.width+2*i,r.height+2*i),Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,c,1,8+i);
        }
    }
    public static void Disc(Rect r,bool hover,bool compass=false)
    {
        if(Event.current.type!=EventType.Repaint)return;
        Warm();if(atlas==null)return;
        var before=GUI.color;bool enabled=GUI.enabled;GUI.enabled=true;
        GUI.color=before*(hover?new Color(1.08f,1.14f,1.1f):enabled?Color.white:new Color(.65f,.72f,.75f));
        if(r.width<100)
        {
            if(smallDisc==null)CreateSmallDisc();
            GUI.DrawTexture(r,smallDisc);
        }
        else if(compassArt!=null)GUI.DrawTexture(r,compassArt,ScaleMode.ScaleToFit);
        else GUI.DrawTextureWithTexCoords(r,atlas,Region(1));
        GUI.color=before;GUI.enabled=enabled;
    }
    static void CreateSmallDisc()
    {
        const int size=192;var pixels=new Color32[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float dx=(x+.5f-size/2f)/(size/2f),dy=(y+.5f-size/2f)/(size/2f),d=Mathf.Sqrt(dx*dx+dy*dy);
            float ring=Mathf.Max(Mathf.Clamp01((.99f-d)*90)*Mathf.Clamp01((d-.88f)*90),1-Mathf.Abs(d-.83f)/.014f);
            Color metal=Color.Lerp(new Color(.43f,.29f,.12f),new Color(1,.89f,.60f),Mathf.Clamp01(.58f+dy*.28f-dx*.17f));
            metal*=.72f+.28f*Mathf.Cos((d-.94f)*65);
            Color color=Color.Lerp(Color.Lerp(new Color(.018f,.08f,.12f),new Color(.04f,.22f,.25f),dy*.5f+.5f),metal,Mathf.Clamp01(ring));
            color.a=Mathf.Clamp01((1-d)*size*.5f);pixels[y*size+x]=color;
        }
        smallDisc=new Texture2D(size,size,TextureFormat.RGBA32,false){name="海洋细黄铜圆片",hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp};
        smallDisc.SetPixels32(pixels);smallDisc.Apply(false,true);
    }
}
