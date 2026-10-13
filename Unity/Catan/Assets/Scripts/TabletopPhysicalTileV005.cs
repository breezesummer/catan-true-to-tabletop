using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

// 从已有六边厚板读取印刷面的 UV，只对显示实例重建倒角；来源 FBX/照片保持原样。
// 数值仍为估算：顶面 8 毫米、上倒角 0.45 毫米；不增加地形，也不改变规则落点。
public sealed class TabletopPhysicalTileV005 : MonoBehaviour
{
    Mesh generated;
    readonly List<Material> materials = new List<Material>();

    public static void Apply(GameObject tile)
    {
        if (tile == null) return;
        var physical = tile.AddComponent<TabletopPhysicalTileV005>();
        physical.Rebuild();
    }

    void Rebuild()
    {
        var filter = GetComponentInChildren<MeshFilter>();
        if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable) return;
        var renderer = filter.GetComponent<MeshRenderer>();
        if (renderer == null) return;
        var originals = renderer.sharedMaterials;
        int printed = System.Array.FindIndex(originals, m => m != null && m.mainTexture != null);
        int edge = System.Array.FindIndex(originals, m => m != null && m.mainTexture == null);
        if (printed < 0 || edge < 0 || printed >= filter.sharedMesh.subMeshCount) return;
        var source = filter.sharedMesh;
        var sourcePositions = source.vertices;
        var sourceUv = source.uv;
        var toRoot = transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
        var toMesh = filter.transform.worldToLocalMatrix * transform.localToWorldMatrix;
        var corners = new List<Vector3>();
        var coordinates = new List<Vector2>();
        foreach (int index in source.GetTriangles(printed).Distinct())
        {
            var p = toRoot.MultiplyPoint3x4(sourcePositions[index]);
            if (Mathf.Abs(p.y - EntityBoardAssets.BoardSurfaceHeight) > .00005f) continue;
            if (corners.Any(c => (c - p).sqrMagnitude < .0000000001f)) continue;
            corners.Add(p); coordinates.Add(sourceUv[index]);
        }
        if (corners.Count != 6) return;
        var sorted = Enumerable.Range(0, 6).OrderBy(i => Mathf.Atan2(corners[i].z, corners[i].x)).ToArray();
        var orderedCorners = sorted.Select(i => corners[i]).ToArray();
        var orderedUv = sorted.Select(i => coordinates[i]).ToArray();
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new[] { new List<int>(), new List<int>(), new List<int>() };
        float[] heights = { 0, .00035f, .00755f, .008f };
        float[] inset = { .00025f, 0, 0, .00045f };
        Vector3 Point(int corner, int ring)
        {
            var p = orderedCorners[corner];
            var direction = new Vector3(p.x, 0, p.z).normalized;
            return new Vector3(p.x, heights[ring], p.z) - direction * inset[ring];
        }
        void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, int submesh)
        {
            int start = vertices.Count;
            vertices.Add(toMesh.MultiplyPoint3x4(a)); vertices.Add(toMesh.MultiplyPoint3x4(b)); vertices.Add(toMesh.MultiplyPoint3x4(c));
            uv.Add(ua); uv.Add(ub); uv.Add(uc);
            triangles[submesh].AddRange(new[] { start, start + 1, start + 2 });
        }
        var uvCenter = orderedUv.Aggregate(Vector2.zero, (sum, p) => sum + p) / 6;
        for (int i = 0; i < 6; i++)
        {
            int next = (i + 1) % 6;
            Triangle(Vector3.up * .008f, Point(next, 3), Point(i, 3), uvCenter, orderedUv[next], orderedUv[i], 1);
            Triangle(Vector3.zero, Point(i, 0), Point(next, 0), Vector2.zero, Vector2.zero, Vector2.zero, 0);
            for (int ring = 0; ring < 3; ring++)
            {
                int material = ring == 2 ? 2 : 0;
                Triangle(Point(i, ring), Point(i, ring + 1), Point(next, ring + 1), Vector2.zero, Vector2.up, Vector2.one, material);
                Triangle(Point(i, ring), Point(next, ring + 1), Point(next, ring), Vector2.zero, Vector2.one, Vector2.right, material);
            }
        }
        generated = new Mesh { name = "六边实体厚板倒角 v005" };
        generated.SetVertices(vertices); generated.SetUVs(0, uv); generated.subMeshCount = 3;
        for (int i = 0; i < 3; i++) generated.SetTriangles(triangles[i], i);
        generated.RecalculateNormals(); generated.RecalculateTangents(); generated.RecalculateBounds();
        var side = Copy(originals[edge], "复合板厚边 v005", .20f);
        side.color = new Color(.49f, .37f, .23f);
        var top = Copy(originals[printed], "原照覆膜印刷面 v005", .30f);
        var bevel = Copy(originals[edge], "真实倒角高光 v005", .40f);
        bevel.color = new Color(.69f, .57f, .39f);
        filter.sharedMesh = generated;
        renderer.sharedMaterials = new[] { side, top, bevel };
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
    }

    Material Copy(Material original, string label, float smoothness)
    {
        var material = new Material(original) { name = label };
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", 0);
        materials.Add(material); return material;
    }

    void OnDestroy()
    {
        if (generated != null) Destroy(generated);
        foreach (var material in materials) Destroy(material);
    }
}
