using UnityEngine;

// 世界坐标锚定归属地块；只有镜头转向或锚点改变时才更新签面变换。
public sealed class JadeNumberSlip : MonoBehaviour
{
    public const string ModelPath = "TabletopUI/JadeNumbers/jade-slip";
    public const string FacePath = "TabletopUI/v008/NumberTokens/number-";
    public const float LandWidth = .015f;
    Camera boardCamera;
    Quaternion lastRotation;
    Vector3 anchor;
    Vector2 offset;
    float width, minimumWidth, lastViewSize, sizeFactor, minimumPixels;
    int lastPixelHeight;
    Renderer numberFace;

    internal void BindNumber()
    {
        numberFace = GetComponent<Renderer>();
        if (isActiveAndEnabled) OnEnable();
    }

    void OnEnable()
    {
        if (numberFace == null) return;
        Show(TabletopMapMarks.Visible);
        TabletopMapMarks.Changed += Show;
    }

    void OnDisable() => TabletopMapMarks.Changed -= Show;
    void Show(bool visible) => numberFace.enabled = visible;

    public void Place(Vector3 center, Vector2 boardOffset, float width, float detailWidth = 0,
        float sizeFactor = 1, float minimumPixels = 0)
    {
        anchor = center;
        offset = boardOffset;
        this.width = width;
        this.sizeFactor = sizeFactor;
        this.minimumPixels = minimumPixels;
        minimumWidth = detailWidth > 0 ? detailWidth : width;
        if (boardCamera == null) boardCamera = Camera.main;
        Resize(boardCamera);
        Align(boardCamera == null ? Quaternion.identity : boardCamera.transform.rotation);
    }

    void LateUpdate()
    {
        if (boardCamera != null) FaceCamera(boardCamera);
    }

    internal void FaceCamera(Camera camera)
    {
        if (lastViewSize != camera.orthographicSize || lastPixelHeight != camera.pixelHeight)
            Resize(camera);
        if (camera.transform.rotation != lastRotation) Align(camera.transform.rotation);
    }

    void Resize(Camera camera)
    {
        lastViewSize = camera == null ? 0 : camera.orthographicSize;
        lastPixelHeight = camera == null ? 0 : camera.pixelHeight;
        float scale = sizeFactor * Mathf.Clamp(lastViewSize * .036f, minimumWidth, width);
        float readableWidth = minimumPixels * 2 * lastViewSize / Mathf.Max(1, lastPixelHeight);
        transform.localScale = Vector3.one * Mathf.Max(scale, readableWidth);
    }

    void Align(Quaternion rotation)
    {
        lastRotation = rotation;
        var right = rotation * Vector3.right;
        var away = Vector3.Cross(right, Vector3.up);
        transform.SetPositionAndRotation(anchor + right * offset.x + away * offset.y,
            rotation * Quaternion.Euler(0, 180, 0));
    }
}
