using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 棋盘沿岸的立体栈桥与浮网平面。
public static class CoastalDecals
{
    public const string Root = PaintedMap.Root + "Coast/";
    public const float ShoreEnd=.0115f;
    const float SeaEnd=-.0157f;
    public const float DockLength=ShoreEnd-SeaEnd;
    public const float DockInset=.0016f;
    static Mesh rectangle;
    static Mesh Surface(string name, Vector3[] points, Vector2[] uv, int[] triangles)
    {
        var mesh = new Mesh { name=name, vertices=points, uv=uv, triangles=triangles };
        mesh.RecalculateBounds();
        return mesh;
    }
    static readonly int[] Quad = {0,1,2,0,2,3};
    static Vector3 XZ(float x,float z) => new Vector3(x,0,z);
    public static Mesh Rectangle()
    {
        if(rectangle==null) rectangle=Surface("平面图签",
            new[]{XZ(-.5f,-.5f),XZ(-.5f,.5f),XZ(.5f,.5f),XZ(.5f,-.5f)},
            new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right},Quad);
        return rectangle;
    }
    static readonly Dictionary<string, MeshFilter> parts = new Dictionary<string, MeshFilter>();
    static readonly Dictionary<Mesh, Mesh> cargoHitMeshes = new Dictionary<Mesh, Mesh>();
    static readonly string[] contactCells={"wood","brick","ore","wheat","wool","generic"};
    static Material timber, cargoFinish, waterShadow;

    static void LoadPier()
    {
        if (timber != null) return;
        var library = Resources.Load<GameObject>(Root + "pier");
        foreach (var part in library.GetComponentsInChildren<MeshFilter>())
            parts[part.name] = part;
        timber = new Material(Resources.Load<Shader>(Root + "PierTimber"));
        timber.mainTexture = Resources.Load<Texture2D>("TabletopUI/v003/oak-table-albedo");
        timber.SetTexture("_CargoContact",Resources.Load<Texture2D>(Root+"cargo-contact"));
        cargoFinish = new Material(Resources.Load<Shader>(Root + "HarborCargo"));
        cargoFinish.mainTexture = Resources.Load<Texture2D>(Root + "cargo-pigments");
        cargoFinish.SetTexture("_SurfaceMap",Resources.Load<Texture2D>(Root + "cargo-surface"));
        foreach (var part in parts)
            if (part.Key.StartsWith("Cargo_", System.StringComparison.Ordinal))
                cargoHitMeshes.Add(part.Value.sharedMesh,parts["Pick_" + part.Key.Substring(6)].sharedMesh);
        waterShadow = new Material(Resources.Load<Shader>(Root + "PierWaterShadow"));
    }

    static Transform Place(Transform parent, string partName, Material material)
    {
        var source = parts[partName];
        var obj = new GameObject(partName).transform;
        obj.SetParent(parent, false);
        obj.localPosition = source.transform.position;
        obj.localRotation = source.transform.rotation;
        obj.localScale = source.transform.lossyScale;
        obj.gameObject.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
        var renderer = obj.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return obj;
    }

    public static GameObject Dock(string resource, string label, Vector3 bottom, Quaternion rotation, Transform parent)
    {
        LoadPier();
        string cargo = resource.Substring(resource.LastIndexOf("port-", System.StringComparison.Ordinal) + 5);
        var root = new GameObject(label).transform;
        root.SetParent(parent, false);
        root.SetPositionAndRotation(bottom, rotation);
        var deck = Place(root, "PierDeck", timber);
        int cell=System.Array.IndexOf(contactCells,cargo);
        var block=new MaterialPropertyBlock();
        block.SetVector("_ContactCell",new Vector4(cell%3,1-cell/3,0,0));
        deck.GetComponent<Renderer>().SetPropertyBlock(block);
        Place(root,"Cargo_"+cargo,cargoFinish);
        var markers = parent != null ? parent.GetComponentInParent<TabletopReferenceMarkers>() : null;
        var assets = JadeNumberAssets.For(markers != null ? markers.transform.parent : parent != null ? parent : root);
        var slip = assets.CreateSlip(root, "港口贸易玉签", Root+"sign-"+cargo);
        slip.gameObject.AddComponent<HarborSign>().Initialize(slip);
        var shadow = new GameObject("栈桥水影").transform;
        shadow.SetParent(root,false);
        shadow.localScale = new Vector3(.038f,1,.038f);
        shadow.localPosition = Vector3.up*(TabletopOcean.WaterHeight+.00008f-EntityBoardAssets.BoardSurfaceHeight);
        shadow.gameObject.AddComponent<MeshFilter>().sharedMesh = Rectangle();
        var renderer = shadow.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = waterShadow;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return root.gameObject;
    }

    // 货堆用保留主要外形的拾取网格，纤维、铆钉和麦芒只参与绘制。
    public static Mesh HitMesh(Mesh visible) =>
        cargoHitMeshes.TryGetValue(visible,out var proxy) ? proxy : visible;

    // 避让渔场与取景只需桥面投影；每块木板、缆绳和货物都在这两段轮廓内。
    public static IEnumerable<Vector3[]> Footprint(Transform root)
    {
        foreach (var rect in DeckSections)
        {
            var a = root.TransformPoint(XZ(rect.xMin,rect.yMin));
            var b = root.TransformPoint(XZ(rect.xMin,rect.yMax));
            var c = root.TransformPoint(XZ(rect.xMax,rect.yMax));
            var d = root.TransformPoint(XZ(rect.xMax,rect.yMin));
            yield return new[] { a,b,c };
            yield return new[] { a,c,d };
        }
    }
    static readonly Rect[] DeckSections = {
        Rect.MinMaxRect(-.0124f,SeaEnd,.0124f,-.00027f),
        Rect.MinMaxRect(-.0037f,-.00027f,.0037f,.01152f),
        Rect.MinMaxRect(.00510f,-.00027f,.01220f,.00169f)
    };
}
