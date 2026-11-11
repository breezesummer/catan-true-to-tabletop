using UnityEngine;

public sealed class HarborSign : MonoBehaviour
{
    // 锚在栈桥外端右角；沿用点数签的相机相对偏移，给桩头与货堆留出一角。
    internal static readonly Vector3 LocalAnchor = new Vector3(.013f, .017f, -.022f);
    internal static readonly Vector2 BoardOffset = new Vector2(.0074f, -.013f);
    internal bool OnDeck { get; private set; }
    internal const float DeckWidth = .0106f, DeckHeight = .00122f, DeckCenterZ = -.0078f;
    internal JadeNumberSlip Slip { get; private set; }
    public Renderer Face { get; private set; }

    internal void Initialize(JadeNumberSlip slip)
    {
        Slip = slip;
        Face = GetComponent<Renderer>();
        OnDeck = transform.parent.Find("PierDeck") != null;
        if (OnDeck) Slip.enabled = false;
        Place();
    }

    internal void Place()
    {
        if (OnDeck)
        {
            var bounds = GetComponent<MeshFilter>().sharedMesh.bounds;
            float scale = DeckWidth / bounds.size.x;
            transform.localScale = Vector3.one * scale;
            transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            var center = new Vector3(0, DeckHeight + bounds.extents.z * scale, DeckCenterZ);
            transform.localPosition = center - transform.localRotation * (bounds.center * scale);
            return;
        }
        Slip.Place(transform.parent.TransformPoint(LocalAnchor), BoardOffset,
            JadeNumberSlip.LandWidth, .0083f, sizeFactor: 1.85f, minimumPixels: 31);
    }
}
