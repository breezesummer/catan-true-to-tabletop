using System.Collections.Generic;
using UnityEngine;

// 同一棋盘的地块、渔场与港口共用玉签，只按签面分别缓存材质。
public sealed class JadeNumberAssets : MonoBehaviour
{
    readonly Dictionary<string, Material> faces = new Dictionary<string, Material>();
    Mesh mesh;

    internal static JadeNumberAssets For(Transform board)
    {
        var assets = board.GetComponent<JadeNumberAssets>();
        if (assets != null) return assets;
        assets = board.gameObject.AddComponent<JadeNumberAssets>();
        assets.mesh = Resources.Load<GameObject>(JadeNumberSlip.ModelPath).GetComponentInChildren<MeshFilter>().sharedMesh;
        return assets;
    }

    internal JadeNumberSlip CreateSlip(Transform parent, string label, int number)
    {
        var slip = CreateSlip(parent, label, JadeNumberSlip.FacePath + number);
        slip.BindNumber();
        return slip;
    }

    internal JadeNumberSlip CreateSlip(Transform parent, string label, string texturePath)
    {
        if (!faces.TryGetValue(texturePath, out var face))
        {
            face = BoardRenderer.Material(Color.white);
            face.name = "暖白玉签 " + label;
            face.mainTexture = Resources.Load<Texture2D>(texturePath);
            face.SetFloat("_Smoothness", .26f);
            face.SetFloat("_Metallic", .04f);
            faces.Add(texturePath, face);
        }
        var obj = new GameObject(label);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = obj.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = face;
        return obj.AddComponent<JadeNumberSlip>();
    }

    void OnDestroy() { foreach (var face in faces.Values) Destroy(face); }
}
