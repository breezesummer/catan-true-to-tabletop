using UnityEngine;

// 只适配触控、安全区与字体；手机和 PC 共用完整桌面 UI，不按平台切换布局。
public static class MobileTabletop
{
    static readonly bool desktopPreview=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-catan-mobile-ui")>=0;
    static int gestureFrame=-1;
    static Vector2 touchStart;
    static bool touchDragged;
    public static bool TouchDragged
    {
        get
        {
            if(gestureFrame==Time.frameCount)return touchDragged;
            gestureFrame=Time.frameCount;
            if(Input.touchCount==0){touchDragged=false;return false;}
            var touch=Input.GetTouch(0);
            if(touch.phase==TouchPhase.Began){touchStart=Logical(new Vector2(touch.position.x,Screen.height-touch.position.y));touchDragged=false;}
            var point=Logical(new Vector2(touch.position.x,Screen.height-touch.position.y));
            var delta=point-touchStart;if(!MobileGestureMath.IsTap(delta.x,delta.y))touchDragged=true;
            if(Input.touchCount>1)touchDragged=true;
            return touchDragged;
        }
    }
    public static bool Enabled => Application.isMobilePlatform || desktopPreview;
    public static MobileViewport Viewport
    {
        get
        {
            var safe=Enabled?Screen.safeArea:new Rect(0,0,Screen.width,Screen.height);
            return MobileViewport.Fit(Screen.width,Screen.height,safe.x,safe.y,safe.width,safe.height);
        }
    }
    public static Matrix4x4 GuiMatrix
    {get{var v=Viewport;return Matrix4x4.TRS(new Vector3(v.Left,v.Top,0),Quaternion.identity,new Vector3(v.Width/1440,v.Height/900,1));}}
    public static Vector2 Logical(Vector2 topScreen)=>new Vector2(Viewport.ToLogicalX(topScreen.x),Viewport.ToLogicalY(topScreen.y));
    public static Vector2 ScreenPoint(Vector2 logical)=>new Vector2(Viewport.ToScreenX(logical.x),Viewport.ToScreenY(logical.y));
    public static Vector2 Pointer=>Input.touchCount>0?new Vector2(Input.GetTouch(0).position.x,Screen.height-Input.GetTouch(0).position.y):new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y);
    public static float PickRadius(float desktopLogical)=>Enabled?Mathf.Max(desktopLogical,48)*Mathf.Min(Viewport.Width/1440,Viewport.Height/900):desktopLogical*Screen.width/1440;
    public static Font LoadFont(int size=18)
    {
        var bundled=Resources.Load<Font>("Fonts/NotoSerifCJKsc-Regular");if(bundled!=null)return bundled;
        return Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Noto Sans CJK SC","Noto Sans CJK","Droid Sans Fallback","Arial"},size);
    }
    // IMGUI 原生滚轮之外补单指滑动，滚动区域不抢占按钮的短按。
    public static void ScrollTouch(Rect area,ref Vector2 scroll)
    {
        if(!Enabled||Input.touchCount!=1||Event.current.type!=EventType.Repaint)return;
        var touch=Input.GetTouch(0);
        if(touch.phase!=TouchPhase.Moved||!TouchDragged||!area.Contains(touchStart)||!area.Contains(Logical(Pointer)))return;
        scroll.y=Mathf.Max(0,scroll.y+touch.deltaPosition.y*900/Viewport.Height);
    }
}
