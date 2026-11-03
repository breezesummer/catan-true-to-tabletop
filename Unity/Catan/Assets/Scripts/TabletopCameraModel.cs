using System;

// 缩放与观察角度属于本地展示偏好，不进入规则、AI 视图或权威存档。
public static class TabletopCameraModel
{
    public const float ClosestZoom = .45f, DefaultZoom = 1, FarthestZoom = 1.65f;
    public const float CloseAngle = 66, DefaultAngle = 66, FarAngle = 66, OverheadOffset = 16;

    public static float Angle(float zoom, bool overhead)
    {
        // 滚轮只改变正交尺寸，两个观察模式均保持棋盘形状稳定。
        return DefaultAngle + (overhead ? OverheadOffset : 0);
    }
}
