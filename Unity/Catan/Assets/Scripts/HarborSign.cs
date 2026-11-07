using UnityEngine;

public sealed class HarborSign : MonoBehaviour
{
    // 锚在栈桥外端右角；沿用点数签的相机相对偏移，给桩头与货堆留出一角。
    internal static readonly Vector3 LocalAnchor = new Vector3(.013f, .017f, -.022f);
    internal static readonly Vector2 BoardOffset = new Vector2(.0074f, -.013f);
    internal JadeNumberSlip Slip { get; private set; }
    public Renderer Face { get; private set; }

    internal void Initialize(JadeNumberSlip slip)
    {
        Slip = slip;
        Face = GetComponent<Renderer>();
        Place();
    }

    internal void Place()
    {
        Slip.Place(transform.parent.TransformPoint(LocalAnchor), BoardOffset,
            JadeNumberSlip.LandWidth, .0083f, sizeFactor: 1.85f, minimumPixels: 31);
    }
}
