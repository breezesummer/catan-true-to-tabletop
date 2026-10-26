using UnityEngine;

// 说明与选择共享按下点。右键/长按只看说明，松手或滑动不会继续发动技能。
public static class ThreeKingdomsDirectInput
{
    static int held;
    static Vector2 origin;
    static double pressedAt;
    static bool dragged, inspected;
    public static void Reset()
    {if(held!=0&&GUIUtility.hotControl==held)GUIUtility.hotControl=0;held=0;dragged=inspected=false;}
    // 1=选择，2=说明；不对权威状态作任何修改。
    public static int Read(Rect area, bool selectable, bool inspectable=true)
    {
        var e=Event.current;int id=GUIUtility.GetControlID(FocusType.Passive,area);
        if(e.type==EventType.MouseDown&&area.Contains(e.mousePosition))
        {
            if(e.button==1&&inspectable){e.Use();return 2;}
            if(e.button==0&&(selectable||inspectable))
            {held=id;GUIUtility.hotControl=id;origin=e.mousePosition;pressedAt=Time.unscaledTimeAsDouble;dragged=false;inspected=false;e.Use();}
        }
        if(held!=id)return 0;
        if((e.rawType==EventType.MouseDrag||e.rawType==EventType.MouseUp)&&(e.mousePosition-origin).sqrMagnitude>64||MobileTabletop.TouchDragged)dragged=true;
        if(MobileTabletop.Enabled&&!dragged&&!inspected&&inspectable&&Time.unscaledTimeAsDouble-pressedAt>=.5)
        {inspected=true;return 2;}
        if(e.rawType==EventType.MouseUp)
        {
            bool click=!dragged&&!inspected&&area.Contains(e.mousePosition)&&selectable;
            held=0;GUIUtility.hotControl=0;e.Use();return click?1:0;
        }
        return 0;
    }
}
