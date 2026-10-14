using UnityEngine;

// 地图点击在松键时确认；超过逻辑像素阈值的拖动只改变观察位置。
public sealed class MapPanInput
{
    Vector2 start, previous;
    bool captured, dragging;
    int button;
    public bool IsDragging => dragging;

    public bool Update(TabletopScene scene, Vector2 screenMouse, bool canStart, bool allowPan)
    {
        if(scene==null)return false;
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

    public void Cancel(TabletopScene scene)
    {captured=false;dragging=false;if(scene!=null)scene.EndPan();}
}
