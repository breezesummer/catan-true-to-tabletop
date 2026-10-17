using UnityEngine;

// 资源牌共用输入：鼠标左加右减；触屏轻点增加、长按松开减少。
public static class TabletopCardInput
{
    static double pressedAt;
    static int changedFrame=-1;
    public static bool Changed=>changedFrame==Time.frameCount&&Event.current.type==EventType.Used;
    public static string Hint=>MobileTabletop.Enabled?"点击牌面增加 · 长按牌面减少":"左键点击牌面增加 · 右键点击牌面减少";
    public static int Delta(Rect face)
    {
        var e=Event.current;int id=GUIUtility.GetControlID(FocusType.Passive,face);
        if(!GUI.enabled){if(GUIUtility.hotControl==id&&e.rawType==EventType.MouseUp)GUIUtility.hotControl=0;return 0;}
        if(e.type==EventType.MouseDown&&face.Contains(e.mousePosition)&&(e.button==0||e.button==1))
        {
            if(MobileTabletop.Enabled&&e.button==0){GUIUtility.hotControl=id;pressedAt=Time.unscaledTimeAsDouble;e.Use();return 0;}
            int delta=e.button==0?1:-1;changedFrame=Time.frameCount;e.Use();return delta;
        }
        if(MobileTabletop.Enabled&&GUIUtility.hotControl==id&&e.type==EventType.MouseUp)
        {
            GUIUtility.hotControl=0;
            int delta=face.Contains(e.mousePosition)&&!MobileTabletop.TouchDragged?(Time.unscaledTimeAsDouble-pressedAt>=.5?-1:1):0;
            if(delta!=0)changedFrame=Time.frameCount;e.Use();return delta;
        }
        return 0;
    }
}
