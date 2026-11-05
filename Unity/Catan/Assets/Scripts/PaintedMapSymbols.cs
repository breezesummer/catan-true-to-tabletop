using UnityEngine;

public enum MapSymbol { Village, Fortress, Beachhead, Wonder, Gift, Warship, Pirate, Flag }

// 剧本图签与地图共用手绘颜料。数量、席位和名称由当前公开视图排字。
public static class PaintedMapSymbols
{
    static Texture2D symbols, paper;
    static GUIStyle text, source;
    public static void Draw(Vector2 point, MapSymbol symbol, string caption, GUIStyle style)
    {
        if (symbols == null) symbols = Resources.Load<Texture2D>(PaintedMap.Root+"scenario-symbols");
        if (paper == null) paper = Resources.Load<Texture2D>(PaintedMap.Root+"pigment");
        if (text == null || source != style)
        {
            source = style;
            text = new GUIStyle(style) { fontSize=12, alignment=TextAnchor.MiddleCenter, wordWrap=false,
                padding=new RectOffset(2,2,0,0) };
            text.normal.textColor = new Color(.19f,.13f,.08f);
        }
        var content=new GUIContent(caption);
        float width=text.CalcSize(content).x+5;
        var rect=new Rect(point.x-(width+23)*.5f,point.y-9,width+23,18);
        GUI.DrawTexture(rect,paper);
        int index=(int)symbol;
        GUI.DrawTextureWithTexCoords(new Rect(rect.x-2,rect.y-5,27,27),symbols,
            new Rect((index%4)*.25f,index<4?.5f:0,.25f,.5f),true);
        GUI.Label(new Rect(rect.x+23,rect.y,width,rect.height),content,text);
    }
}
