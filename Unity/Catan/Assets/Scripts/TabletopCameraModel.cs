using System;

// 缩放与观察角度属于本地展示偏好，不进入规则、AI 视图或权威存档。
public static class TabletopCameraModel
{
    public const float DetailSize = .046f, DefaultZoom = 1, FarthestZoom = 1.18f;
    public const int MinPitch = 30, MaxPitch = 90;
    public const float WheelDegrees = 15;
    public const float CloseAngle = 66, DefaultAngle = 66, FarAngle = 66, OverheadOffset = 16;

    public static float MinimumZoom(float fullViewSize)
    {
        return DetailSize / Math.Max(DetailSize, fullViewSize);
    }
    public static float Angle(float zoom, bool overhead) => DefaultAngle + (overhead ? OverheadOffset : 0);
}
