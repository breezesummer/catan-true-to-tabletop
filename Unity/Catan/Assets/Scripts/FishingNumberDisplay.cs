using UnityEngine;

public sealed class FishingNumberDisplay : MonoBehaviour
{
    Transform mount;
    JadeNumberSlip token;

    public static FishingNumberDisplay Create(Transform root, int number, JadeNumberAssets tokens)
    {
        var display = root.gameObject.AddComponent<FishingNumberDisplay>();
        display.mount = new GameObject("渔场点数定位").transform;
        display.mount.SetParent(root, false);
        display.token = tokens.CreateSlip(display.mount, "渔港点数 " + number, number);
        return display;
    }

    public void Place(Vector3 center)
    {
        // 浮网随岸线伸缩，字面保持和陆地点数相同的比例。
        var scale = transform.lossyScale;
        mount.localScale = new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z);
        center.y = FishingTabletopAssets.GroundBottom;
        token.Place(center, Vector2.zero);
    }
}
