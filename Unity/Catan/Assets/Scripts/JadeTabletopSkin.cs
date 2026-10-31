using UnityEngine;

// 单张图集、九宫格云纹边角；只在首次使用时加载，不创建逐帧纹理。
public static class JadeTabletopSkin
{
    static Texture2D atlas;
    public static bool Ready { get { Warm();return atlas!=null; } }
    static void Warm(){if(atlas==null)atlas=Resources.Load<Texture2D>("TabletopUI/v015/jade-atlas");}
    static Rect Region(int slot)
    {
        Rect r=slot==0?new Rect(8,8,610,580):slot==1?new Rect(668,30,560,543):slot==2?new Rect(22,640,580,570):new Rect(636,637,610,578);
        return new Rect(r.x/1254,1-r.yMax/1254,r.width/1254,r.height/1254);
    }
    static void Patch(Rect r,int slot,bool center,Color tint,float corner=22)
    {
        Warm();if(atlas==null||Event.current.type!=EventType.Repaint)return;
        var uv=Region(slot);float slice=.10f,c=Mathf.Min(corner,Mathf.Min(r.width,r.height)*.40f);
        var old=GUI.color;GUI.color*=tint;
        for(int y=0;y<3;y++)for(int x=0;x<3;x++)
        {
            if(x==1&&y==1&&!center)continue;
            var dest=new Rect(x==0?r.x:x==1?r.x+c:r.xMax-c,y==0?r.y:y==1?r.y+c:r.yMax-c,x==1?r.width-2*c:c,y==1?r.height-2*c:c);
            var source=new Rect(x==0?uv.x:x==1?uv.x+slice:uv.xMax-slice,y==0?uv.yMax-slice:y==1?uv.y+slice:uv.y,x==1?uv.width-2*slice:slice,y==1?uv.height-2*slice:slice);
            GUI.DrawTextureWithTexCoords(dest,atlas,source);
        }
        GUI.color=old;
    }
    public static void Panel(Rect r,Color face,bool hover,bool ornaments)
    {
        ModernOperationUI.Round(new Rect(r.x+1,r.y+4,r.width,r.height),new Color(.01f,.05f,.025f,.45f));
        ModernOperationUI.Round(r,face.linear);
        if(ornaments)
        {
            Warm();var old=GUI.color;GUI.color*=hover?new Color(.22f,.39f,.28f):new Color(.16f,.29f,.21f);
            var uv=Region(2);uv=new Rect(uv.x+.10f,uv.y+.10f,uv.width-.20f,uv.height-.20f);
            if(atlas!=null)GUI.DrawTextureWithTexCoords(new Rect(r.x+5,r.y+5,r.width-10,r.height-10),atlas,uv);
            GUI.color=old;Frame(r,hover,false);
        }
        else Patch(r,2,true,hover?new Color(.65f,.80f,.65f):new Color(.46f,.65f,.51f),12);
    }
    public static void LightPanel(Rect r,bool selected)
    {
        ModernOperationUI.Round(new Rect(r.x+1,r.y+4,r.width,r.height),new Color(0,.025f,.01f,.4f));
        ModernOperationUI.Round(r,new Color(.57f,.77f,.60f).linear);
        Frame(r,selected,false);
    }
    public static void Frame(Rect r,bool selected,bool card)
    {Patch(r,card?3:0,false,selected?new Color(1.25f,1.15f,.75f):Color.white,card?13:22);}
    public static void Disc(Rect r,bool hover)
    {
        Warm();if(atlas==null)return;var old=GUI.color;
        GUI.color*=new Color(0,0,0,.35f);GUI.DrawTextureWithTexCoords(new Rect(r.x+1,r.y+4,r.width,r.height),atlas,Region(1));
        GUI.color=old*(hover?new Color(1.13f,1.12f,1.03f):Color.white);GUI.DrawTextureWithTexCoords(r,atlas,Region(1));GUI.color=old;
    }
}
