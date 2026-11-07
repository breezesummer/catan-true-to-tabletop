using UnityEngine;

public sealed partial class TabletopReferenceMarkers
{
    // 拾取和逐角度验收可能在 LateUpdate 之前切换镜头。
    public void AlignPortSigns(Camera camera)
    {
        foreach (var port in ports.Values) port.Sign?.Slip.FaceCamera(camera);
        foreach (var gift in gifts.Values) gift.Sign?.Slip.FaceCamera(camera);
    }

    static Vector2 ProjectSign(Vector3 p,Transform camera) =>
        new Vector2(Vector3.Dot(p,camera.right),Vector3.Dot(p,camera.up));

    internal static Rect SignBounds(Renderer renderer,Camera camera)
    {
        var basis=camera.transform;
        var t=renderer.transform;
        var bounds=renderer.localBounds;
        var e=bounds.extents;
        var x=ProjectSign(t.TransformVector(new Vector3(e.x,0,0)),basis);
        var y=ProjectSign(t.TransformVector(new Vector3(0,e.y,0)),basis);
        var z=ProjectSign(t.TransformVector(new Vector3(0,0,e.z)),basis);
        var half=new Vector2(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),
                             Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y));
        return new Rect(ProjectSign(t.TransformPoint(bounds.center),basis)-half,half*2);
    }

}
