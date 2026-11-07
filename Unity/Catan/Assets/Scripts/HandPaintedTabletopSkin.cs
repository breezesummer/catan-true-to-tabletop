using UnityEngine;

// 纸面和木边共用九宫格切片，骰盘与手牌只画边缘，让原有物件透出。
public static class HandPaintedTabletopSkin
{
    const string Root = "TabletopUI/HandPainted/";
    static CopperTabletopSkin.ArtPatch paper;
    static Texture2D jade, leaves;
    public static Texture2D Lining { get { Warm(); return jade; } }
    public static readonly Rect LiningUv = new Rect(.22f,.23f,.56f,.52f);
    public static readonly Color DiscInk = new Color(1f,.97f,.84f);
    static readonly Color Leaf = new Color(.18f,.43f,.32f);

    static void Warm()
    {
        if (paper != null) return;
        paper = new CopperTabletopSkin.ArtPatch(Root+"paper",745,745,new Rect(0,0,745,745),52);
        jade = Resources.Load<Texture2D>(Root+"jade");
        leaves = Resources.Load<Texture2D>(Root+"leaves");
    }

    public static bool Ready { get { Warm(); return paper.Texture != null && jade != null && leaves != null; } }

    public static void Tray(Rect area)
    {
        if (Event.current.type != EventType.Repaint) return;
        Warm();
        if (jade != null)
            GUI.DrawTextureWithTexCoords(new Rect(area.x+3,area.y+3,area.width-6,area.height-6),jade,LiningUv);
        Frame(area);
    }

    public static void CornerLeaves()
    {
        if (Event.current.type != EventType.Repaint) return;
        Warm();
        if (leaves != null) GUI.DrawTexture(new Rect(-5,-5,237,79),leaves);
    }

    public static void TurnBorder(Rect area)
    {
        if (Event.current.type != EventType.Repaint) return;
        var edge = new Rect(area.x-1,area.y-1,area.width+2,area.height+2);
        GUI.DrawTexture(edge,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,new Color(.89f,.68f,.32f),3,6);
        GUI.DrawTexture(area,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,new Color(.99f,.91f,.68f),1,5);
    }

    public static Color TrackInk(int track)
    {
        if (track == 1) return new Color(.17f,.32f,.48f);
        if (track == 2) return Leaf;
        return new Color(.47f,.29f,.12f);
    }

    static void Paper(Rect area, bool fill, float corner, bool selected)
    {
        if (Event.current.type != EventType.Repaint || area.width <= 0 || area.height <= 0) return;
        Warm();
        var tint = GUI.enabled ? Color.white : new Color(.86f,.86f,.82f);
        CopperTabletopSkin.DrawPatch(area,paper,corner,fill,tint,snapToScreen:false);
        if (selected)
            GUI.DrawTexture(area,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,Leaf,2,6);
    }

    public static void Panel(Rect area, bool selected=false, bool ornaments=true)
    {
        Paper(area,true,ornaments?12:8,selected);
    }

    public static void Frame(Rect area, bool selected=false, bool card=false)
    {
        Paper(area,false,card?5:9,selected);
    }

    public static void Disc(Rect area, bool hover)
    {
        if (Event.current.type != EventType.Repaint || area.width <= 0 || area.height <= 0) return;
        Warm();
        if (jade == null) return;
        var tint = !GUI.enabled ? new Color(.72f,.73f,.69f) : hover ? new Color(1.1f,1.1f,1.04f) : Color.white;
        GUI.DrawTexture(area,jade,ScaleMode.StretchToFill,true,0,tint,0,0);
    }

    public static void Preview(Rect area)
    {
        Panel(area);
        float size = area.height*.68f;
        Disc(new Rect(area.center.x-size/2,area.center.y-size/2,size,size),false);
    }
}
