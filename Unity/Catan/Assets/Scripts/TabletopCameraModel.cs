using System;

// 缩放与观察角度属于本地展示偏好，不进入规则、AI 视图或权威存档。
public static class TabletopCameraModel
{
    public const float ClosestZoom = .45f, DefaultZoom = 1, FarthestZoom = 1.65f;
    public const float CloseAngle = 32, DefaultAngle = 58, FarAngle = 68, OverheadOffset = 10;

    public static float Angle(float zoom, bool overhead)
    {
        if (float.IsNaN(zoom)) zoom = DefaultZoom;
        zoom = Math.Max(ClosestZoom, Math.Min(FarthestZoom, zoom));
        float angle = zoom <= DefaultZoom
            ? Mix(CloseAngle, DefaultAngle, (zoom - ClosestZoom) / (DefaultZoom - ClosestZoom))
            : Mix(DefaultAngle, FarAngle, (zoom - DefaultZoom) / (FarthestZoom - DefaultZoom));
        return angle + (overhead ? OverheadOffset : 0);
    }

    static float Mix(float from, float to, float value)
    {
        // 两段曲线在默认观察距离处共享零斜率，滚轮越过该点不会产生角度跳变。
        float smooth = value * value * (3 - 2 * value);
        return from + (to - from) * smooth;
    }
}
