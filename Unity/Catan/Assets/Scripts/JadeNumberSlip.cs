using UnityEngine;

public sealed class JadeNumberSlip : MonoBehaviour
{
    public const string FacePath = "TabletopUI/v008/NumberTokens/number-";
    // 900 像素高的全盘画面采用 32 像素字图，实际墨字约 13 像素高。
    const float FullViewWidth = 32f * 2 / 900;
    Camera boardCamera;
    Renderer numberFace;
    Vector3 anchor;
    Vector2 offset;
    Quaternion lastRotation;
    float lastViewSize;

    void Awake() => numberFace = GetComponent<Renderer>();

    void OnEnable()
    {
        Show(TabletopMapMarks.Visible);
        TabletopMapMarks.Changed += Show;
    }

    void OnDisable() => TabletopMapMarks.Changed -= Show;
    void Show(bool visible) => numberFace.enabled = visible;

    // 偏移以整张字图的宽为单位；湖泊四个数字共用一个地块中心。
    public void Place(Vector3 center, Vector2 faceOffset)
    {
        anchor = center;
        offset = faceOffset;
        boardCamera = Camera.main;
        if (boardCamera != null) Align();
    }

    void LateUpdate()
    {
        if (boardCamera != null && (lastRotation != boardCamera.transform.rotation ||
            lastViewSize != boardCamera.orthographicSize)) Align();
    }

    void Align()
    {
        lastViewSize = boardCamera.orthographicSize;
        lastRotation = boardCamera.transform.rotation;
        float width = Mathf.Clamp(lastViewSize * FullViewWidth, .009f, .024f);
        var shift = lastRotation * new Vector3(offset.x, offset.y, 0) * width;
        transform.SetPositionAndRotation(anchor + shift, lastRotation * Quaternion.Euler(-90, 0, 0));
        transform.localScale = Vector3.one * width;
    }
}
