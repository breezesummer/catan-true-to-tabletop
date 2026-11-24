using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 同一棋盘上的同点数字共用透明字面与材质。
public sealed class JadeNumberAssets : MonoBehaviour
{
    readonly Dictionary<int, Material[]> faces = new Dictionary<int, Material[]>();
    Shader shader;

    internal static JadeNumberAssets For(Transform board)
    {
        var assets = board.GetComponent<JadeNumberAssets>();
        if (assets != null) return assets;
        assets = board.gameObject.AddComponent<JadeNumberAssets>();
        assets.shader = Resources.Load<Shader>("TabletopUI/JadeNumbers/NumberInk");
        return assets;
    }

    internal JadeNumberSlip CreateSlip(Transform parent, string label, int number)
    {
        if (!faces.TryGetValue(number, out var inks))
        {
            var face = new Material(shader) { name = "棋盘点数 " + number };
            face.mainTexture = Resources.Load<Texture2D>(JadeNumberSlip.FacePath + number);
            face.SetFloat("_BoardHeight", EntityBoardAssets.BoardSurfaceHeight + .001f);
            var soft = new Material(face) { name = "遮挡中的点数 " + number, renderQueue = face.renderQueue + 1 };
            soft.EnableKeyword("SOFT_NUMBER_INK");
            soft.SetInt("_DepthTest", (int)CompareFunction.Always);
            soft.SetInt("_StencilRef", 0);
            soft.SetInt("_ReadMask", 2);
            soft.SetInt("_WriteMask", 0);
            inks = new[] { face, soft };
            faces.Add(number, inks);
        }
        var obj = new GameObject(label);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<MeshFilter>().sharedMesh = CoastalDecals.Rectangle();
        var renderer = obj.AddComponent<MeshRenderer>();
        // 同一张四边形先按深度与光效画清字，再在被遮住的像素补虚字。
        renderer.sharedMaterials = inks;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return obj.AddComponent<JadeNumberSlip>();
    }

    void OnDestroy()
    {
        foreach (var inks in faces.Values)
            foreach (var ink in inks) Destroy(ink);
    }
}
