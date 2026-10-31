using UnityEngine;

// 专用玉面、金边和玩家底板共享一张缓存图集；九宫格保留倒角与角饰。
public static class JadeTabletopSkin
{
    static Texture2D atlas,playerPanel;
    public static bool Ready { get { Warm();return atlas!=null&&playerPanel!=null; } }
    static void Warm()
    {
        if(atlas==null)atlas=Resources.Load<Texture2D>("TabletopUI/v016/jade-refined-atlas");
        if(playerPanel==null)playerPanel=Resources.Load<Texture2D>("TabletopUI/v016/player-panel");
    }
    static Rect Region(int slot)
    {
        Rect r=slot==0?new Rect(26,30,600,573):slot==1?new Rect(657,33,573,568):slot==2?new Rect(25,628,602,572):new Rect(652,627,581,572);
        return new Rect(r.x/1254,1-r.yMax/1254,r.width/1254,r.height/1254);
    }
    static void Patch(Rect r,int slot,bool center,Color tint,float corner)
    {
        Warm();if(atlas==null||Event.current.type!=EventType.Repaint)return;
        var uv=Region(slot);float slice=.085f,c=Mathf.Min(corner,Mathf.Min(r.width,r.height)*.40f);
        var old=GUI.color;
        for(int y=0;y<3;y++)for(int x=0;x<3;x++)
        {
            bool middle=x==1&&y==1;if(middle&&!center)continue;
            GUI.color=old*tint;
            var dest=new Rect(x==0?r.x:x==1?r.x+c:r.xMax-c,y==0?r.y:y==1?r.y+c:r.yMax-c,x==1?r.width-2*c:c,y==1?r.height-2*c:c);
            var source=new Rect(x==0?uv.x:x==1?uv.x+slice:uv.xMax-slice,y==0?uv.yMax-slice:y==1?uv.y+slice:uv.y,x==1?uv.width-2*slice:slice,y==1?uv.height-2*slice:slice);
            // 子像素缩放下覆盖半像素接缝，防止背景漏成黑线。
            dest.x-=.25f;dest.y-=.25f;dest.width+=.5f;dest.height+=.5f;
            GUI.DrawTextureWithTexCoords(dest,atlas,source);
        }
        GUI.color=old;
    }
    public static void Panel(Rect r,Color face,bool hover,bool ornaments)
    {
        if(Event.current.type!=EventType.Repaint)return;
        ModernOperationUI.Round(new Rect(r.x+1,r.y+4,r.width,r.height),new Color(.01f,.03f,.015f,.42f));
        // 同一九宫格连续调光，保留玉面与金属之间原有的明暗关系。
        Patch(r,ornaments?0:3,true,hover?new Color(.77f,.85f,.73f):new Color(.65f,.76f,.63f),ornaments?20:12);
    }
    public static void LightPanel(Rect r,bool selected)
    {
        if(Event.current.type!=EventType.Repaint)return;
        ModernOperationUI.Round(new Rect(r.x+1,r.y+4,r.width,r.height),new Color(0,.025f,.01f,.4f));
        Warm();var old=GUI.color;GUI.color*=new Color(.93f,.99f,.91f);
        if(playerPanel!=null)GUI.DrawTextureWithTexCoords(r,playerPanel,new Rect(63f/971,1-1573f/1619,844f/971,1533f/1619));
        GUI.color=old;
        if(selected)GUI.DrawTexture(new Rect(r.x+12,r.yMax-4,r.width-24,1),Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,new Color(.92f,.74f,.31f,.8f),0,0);
    }
    public static Color PlayerInk(string id)
    {
        switch(id){case "P1":return new Color(.62f,.06f,.035f);case "P2":return new Color(.025f,.24f,.48f);case "P3":return new Color(.66f,.31f,.015f);default:return new Color(.10f,.23f,.17f);}
    }
    public static void Frame(Rect r,bool selected,bool card)
    {Patch(r,card?2:0,false,selected?new Color(1.10f,1.03f,.76f):Color.white,card?10:20);}
    public static void Disc(Rect r,bool hover)
    {
        Warm();if(atlas==null||Event.current.type!=EventType.Repaint)return;var old=GUI.color;
        GUI.color=old*new Color(0,0,0,.40f);GUI.DrawTextureWithTexCoords(new Rect(r.x+1,r.y+4,r.width,r.height),atlas,Region(1));
        GUI.color=old*(hover?new Color(1.03f,1.04f,.96f):new Color(.87f,.94f,.84f));GUI.DrawTextureWithTexCoords(r,atlas,Region(1));GUI.color=old;
    }
}
