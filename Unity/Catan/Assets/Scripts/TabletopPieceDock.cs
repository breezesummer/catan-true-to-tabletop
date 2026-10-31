using UnityEngine;

// 库存使用原模型的透明缩略图：图标和数字独立排布，地图缩放不改变尺寸。
public static class TabletopPieceDock
{
    public static Rect GroupLayout(int pieceCount,int index)
    {
        // 八类库存沿左侧排成一列，保留骰盘下沿与武将区上沿。
        return new Rect(14,632-(pieceCount-1-index)*48,140,44);
    }
    public static Vector2 PiecePosition(Rect group) => new Vector2(group.x + 31, group.y + 22);
    public static Rect ModelArea(Rect group) => new Rect(group.x + 7, group.y + 3, 48, 38);
    public static Rect CountArea(Rect group) => new Rect(group.x + 84, group.y + 5, 48, 34);

    public static void DrawFrame(Rect area, bool selected, bool available)
    {
        if(selected)CopperTabletopSkin.Outline(area,true,12);
        else ModernOperationUI.Fill(new Rect(area.x+9,area.yMax-1,area.width-18,1),ModernOperationUI.Line);
        CopperTabletopSkin.Count(CountArea(area),selected);
    }

    public static void DrawCount(Rect group, int count, GUIStyle source)
    {
        bool enabled=GUI.enabled;GUI.enabled=true;
        var area=CountArea(group);
        var value=new GUIStyle(source){fontSize=23,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,
            wordWrap=false,padding=new RectOffset(),clipping=TextClipping.Overflow};
        value.normal.textColor=StrategyHudTheme.Jade?new Color(.97f,.94f,.78f):new Color(.24f,.11f,.055f);
        TabletopTextFit.Label(new Rect(area.x+7,area.y+4,area.width-14,area.height-8),count.ToString(),value,10);GUI.enabled=enabled;
    }
}
