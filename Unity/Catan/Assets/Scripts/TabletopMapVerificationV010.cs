using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// 可由真实 Windows 验收入口调用，核对屏幕行为，不读取权威状态或隐藏地图。
public static class TabletopMapVerificationV010
{
    public static int Check(TabletopScene scene)
    {
        int checks=0;
        Action<bool,string> require=(condition,message)=>{if(!condition)throw new InvalidOperationException(message);checks++;};
        require(scene!=null&&scene.isActiveAndEnabled,"地图桌面必须在实际运行场景中启用");
        var map=scene.TableCamera;var hud=scene.HudCamera;
        require(map!=null&&hud!=null&&map!=hud&&map.orthographic&&hud.orthographic,"地图与固定操作模型必须使用独立正交相机");
        require((map.cullingMask&(1<<TabletopScene.HudLayer))==0&&hud.cullingMask==(1<<TabletopScene.HudLayer),"地图相机不得重复绘制操作模型");
        var stack=map.GetUniversalAdditionalCameraData().cameraStack;
        require(stack!=null&&stack.Contains(hud)&&hud.GetUniversalAdditionalCameraData().renderType==CameraRenderType.Overlay,"URP 固定操作模型必须叠加在地图上");
        var logicalAnchor=new Vector2(76,744);var worldAnchor=scene.World(logicalAnchor);
        require(Vector2.Distance(scene.ProjectHud(worldAnchor),logicalAnchor)<.02f,"固定操作模型的屏幕定位必须可逆");
        var originalHud=scene.ProjectHud(worldAnchor);var originalPan=scene.PanOffset;
        var from=new Vector2(Screen.width*.50f,Screen.height*.48f);
        var to=from+new Vector2(48,24);
        var plane=new Plane(Vector3.up,new Vector3(0,EntityBoardAssets.BoardSurfaceHeight,0));
        var ray=map.ScreenPointToRay(from);
        require(plane.Raycast(ray,out float distance),"高角度相机必须能命中公开棋盘平面");
        var mapPoint=ray.GetPoint(distance);
        scene.PanFromScreen(from,to);
        var projected=map.WorldToScreenPoint(mapPoint);
        require(scene.IsPanning&&Vector2.Distance(new Vector2(projected.x,projected.y),to)<.25f,"地图拖拽必须紧随鼠标移动");
        require(Vector2.Distance(scene.ProjectHud(worldAnchor),originalHud)<.02f,"拖拽地图不得移动操作模型");
        scene.PanFromScreen(to,from);scene.EndPan();
        require(!scene.IsPanning&&Vector3.Distance(scene.PanOffset,originalPan)<.00001f,"反向拖拽必须恢复同一地图位置");
        bool top=Mathf.Abs(Mathf.DeltaAngle(map.transform.eulerAngles.x,TabletopScene.OverheadCameraAngle))<1;
        float originalZoom=1/scene.DisplayScale;
        float originalMapSize=map.orthographicSize;
        var previousMapScreen=map.WorldToScreenPoint(mapPoint);
        try
        {
            scene.Frame(map,top,originalZoom*.75f);
            require(Vector2.Distance(scene.ProjectHud(worldAnchor),originalHud)<.02f,"缩放地图不得改变操作模型的屏幕大小和位置");
            require(Mathf.Abs(map.orthographicSize/originalMapSize-.75f)<.001f,"地图缩放须作用于地图相机");
            require(Vector3.Distance(map.WorldToScreenPoint(mapPoint),previousMapScreen)>1,"地图缩放必须产生可见变化");
        }
        finally{scene.Frame(map,top,originalZoom);}
        return checks;
    }
}
