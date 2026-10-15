using UnityEngine;

// 地图点击在松键时确认；超过逻辑像素阈值的拖动只改变观察位置。
public sealed class MapPanInput
{
    Vector2 start, previous;
    bool captured, dragging;
    int button;
    public bool IsDragging => dragging;
    bool touchCaptured,multiTouch;
    int touchId;
    float pinchDistance;
    Vector2 pinchCenter;
    public float ZoomDelta { get; private set; }
    public bool MultiTouchActive => multiTouch;

    public bool Update(TabletopScene scene, Vector2 screenMouse, bool canStart, bool allowPan)
    {
        ZoomDelta=0;
        if(scene==null)return false;
        if(MobileTabletop.Enabled&&(Input.touchCount>0||touchCaptured||multiTouch))return UpdateTouch(scene,canStart,allowPan);
        if(!captured&&canStart&&(Input.GetMouseButtonDown(0)||allowPan&&Input.GetMouseButtonDown(2)))
        {
            captured=true;dragging=false;button=Input.GetMouseButtonDown(2)?2:0;
            start=previous=screenMouse;
        }
        if(!captured)return false;
        if(allowPan&&Input.GetMouseButton(button))
        {
            Vector2 delta=screenMouse-start;
            var logical=new Vector2(delta.x*1440f/Screen.width,delta.y*900f/Screen.height);
            if(!dragging&&logical.sqrMagnitude>36)dragging=true;
            if(dragging)scene.PanFromScreen(new Vector2(previous.x,Screen.height-previous.y),new Vector2(screenMouse.x,Screen.height-screenMouse.y));
            previous=screenMouse;
        }
        if(!Input.GetMouseButtonUp(button))return false;
        var releasedDelta=screenMouse-start;
        var releasedLogical=new Vector2(releasedDelta.x*1440f/Screen.width,releasedDelta.y*900f/Screen.height);
        if(allowPan&&releasedLogical.sqrMagnitude>36)
        {
            if(!dragging)scene.PanFromScreen(new Vector2(start.x,Screen.height-start.y),new Vector2(screenMouse.x,Screen.height-screenMouse.y));
            dragging=true;
        }
        bool click=button==0&&!dragging&&canStart;
        captured=false;dragging=false;scene.EndPan();return click;
    }

    bool UpdateTouch(TabletopScene scene,bool canStart,bool allowPan)
    {
        if(Input.touchCount==0){touchCaptured=false;multiTouch=false;pinchDistance=0;dragging=false;scene.EndPan();return false;}
        if(Input.touchCount>=2)
        {
            if(!multiTouch&&!canStart&&!touchCaptured)return false;
            var a=Input.GetTouch(0);var b=Input.GetTouch(1);
            var middle=(a.position+b.position)*.5f;float distance=Vector2.Distance(a.position,b.position);
            if(multiTouch&&pinchDistance>0)
            {
                ZoomDelta=MobileGestureMath.PinchZoomDelta(pinchDistance,distance);
                scene.PanFromScreen(pinchCenter,middle);
            }
            multiTouch=true;touchCaptured=false;dragging=false;pinchDistance=distance;pinchCenter=middle;return false;
        }
        // 双指手势剩余的一指必须抬起后，下一次触摸才可放置棋子。
        if(multiTouch)return false;
        var touch=Input.GetTouch(0);var point=new Vector2(touch.position.x,Screen.height-touch.position.y);
        if(!touchCaptured&&touch.phase==TouchPhase.Began&&canStart)
        {touchCaptured=true;touchId=touch.fingerId;start=previous=point;dragging=false;}
        if(!touchCaptured||touch.fingerId!=touchId)return false;
        var delta=MobileTabletop.Logical(point)-MobileTabletop.Logical(start);
        if(allowPan&&!MobileGestureMath.IsTap(delta.x,delta.y))dragging=true;
        if(dragging)scene.PanFromScreen(new Vector2(previous.x,Screen.height-previous.y),touch.position);
        previous=point;
        if(touch.phase!=TouchPhase.Ended&&touch.phase!=TouchPhase.Canceled)return false;
        bool click=touch.phase==TouchPhase.Ended&&!dragging&&canStart;
        touchCaptured=false;dragging=false;scene.EndPan();return click;
    }

    public void Cancel(TabletopScene scene)
    {captured=false;dragging=false;touchCaptured=false;multiTouch=false;pinchDistance=0;ZoomDelta=0;if(scene!=null)scene.EndPan();}
}
