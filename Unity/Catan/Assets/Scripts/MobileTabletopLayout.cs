using System;

// 独立于 Unity 的屏幕坐标约定，安全区外不得触发棋盘或按钮。
public readonly struct MobileViewport
{
    public readonly float Left, Top, Width, Height;
    public MobileViewport(float left,float top,float width,float height)
    {Left=left;Top=top;Width=width;Height=height;}
    public static MobileViewport Fit(float screenWidth,float screenHeight,float safeLeft,float safeBottom,float safeWidth,float safeHeight)
    {
        screenWidth=Math.Max(1,screenWidth);screenHeight=Math.Max(1,screenHeight);
        float left=Math.Max(0,Math.Min(screenWidth-1,safeLeft));
        float bottom=Math.Max(0,Math.Min(screenHeight-1,safeBottom));
        float width=safeWidth>0?Math.Min(safeWidth,screenWidth-left):screenWidth-left;
        float height=safeHeight>0?Math.Min(safeHeight,screenHeight-bottom):screenHeight-bottom;
        return new MobileViewport(left,screenHeight-bottom-height,Math.Max(1,width),Math.Max(1,height));
    }
    public float ToLogicalX(float screenX)=>(screenX-Left)*1440/Width;
    public float ToLogicalY(float screenTopY)=>(screenTopY-Top)*900/Height;
    public float ToScreenX(float logicalX)=>Left+logicalX*Width/1440;
    public float ToScreenY(float logicalY)=>Top+logicalY*Height/900;
}
public static class MobileGestureMath
{
    public static bool IsTap(float dx,float dy)=>dx*dx+dy*dy<=18*18;
    public static float PinchZoomDelta(float previousDistance,float distance)
    {
        if(previousDistance<=0||distance<=0||float.IsNaN(previousDistance)||float.IsNaN(distance)||float.IsInfinity(previousDistance)||float.IsInfinity(distance))return 0;
        return Math.Max(-.08f,Math.Min(.08f,(previousDistance-distance)/previousDistance*.32f));
    }
}
