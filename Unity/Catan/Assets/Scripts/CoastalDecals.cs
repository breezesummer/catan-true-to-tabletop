using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 棋盘沿岸的立体栈桥与浮网平面。
public static class CoastalDecals
{
    public const string Root = PaintedMap.Root + "Coast/";
    public const float ShoreEnd=.0115f;
    const float SeaEnd=-.0215f;
    public const float DockLength=ShoreEnd-SeaEnd;
    public const float DockInset=.0016f;
    static float pierModelLength;
    public static float PierModelLength
    {
        get
        {
            if(pierModelLength>0)return pierModelLength;
            LoadPier();
            var deck=parts["PierDeck"];
            var bounds=deck.sharedMesh.bounds;
            var matrix=deck.transform.localToWorldMatrix;
            pierModelLength=Mathf.Abs(matrix.m20)*bounds.size.x+Mathf.Abs(matrix.m21)*bounds.size.y+Mathf.Abs(matrix.m22)*bounds.size.z;
            return pierModelLength;
        }
    }
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
    static Material timber, waterShadow;
    static Texture2D linen;
    static Vector3 printCenter, printWidth, printHeight;

    static void LoadPier()
    {
        if (timber != null) return;
        var library = Resources.Load<GameObject>(Root + "harbor-house");
        foreach (var part in library.GetComponentsInChildren<MeshFilter>())
            parts[part.name == "CoastalWalkway" ? "PierDeck" : part.name] = part;
        // 读取一次布面四边中点。模型导入的轴向、比例由变换矩阵处理。
        var mesh = parts["HarborFlag"].sharedMesh;
        var vertices = mesh.vertices;
        var uv = mesh.uv;
        Vector3 PointAt(Vector2 target)
        {
            int best = 0;
            for (int i=1; i<uv.Length; i++)
                if ((uv[i]-target).sqrMagnitude < (uv[best]-target).sqrMagnitude) best=i;
            return vertices[best];
        }
        var top = PointAt(new Vector2(.5f,1));
        var bottom = PointAt(new Vector2(.5f,0));
        printCenter = (top+bottom)*.5f;
        printHeight = top-bottom;
        printWidth = PointAt(new Vector2(1,.5f))-PointAt(new Vector2(0,.5f));
        linen = Resources.Load<Texture2D>(Root+"flag-linen");
        timber = new Material(Resources.Load<Shader>(Root + "PierTimber"));
        timber.mainTexture = Resources.Load<Texture2D>("TabletopUI/v003/oak-table-albedo");
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
        renderer.shadowCastingMode = ShadowCastingMode.On;
        return obj;
    }

    public static GameObject Dock(string resource, string label, Vector3 bottom, Quaternion rotation, Transform parent)
    {
        string cargo = resource.Substring(resource.LastIndexOf("port-", System.StringComparison.Ordinal) + 5);
        var root = new GameObject(label).transform;
        root.SetParent(parent, false);
        root.SetPositionAndRotation(bottom, rotation);
        AddPier(root);
        Place(root, "HarborHouse", timber);
        var rig = new GameObject("吊旗").transform;
        rig.SetParent(root, false);
        rig.localPosition = new Vector3(-.0146f, 0, -.0208f);
        Place(rig, "HarborMast", timber);
        if (!flagMaterials.TryGetValue(cargo, out var cloth))
        {
            cloth = new Material(Resources.Load<Shader>(Root + "HarborCloth"));
            cloth.mainTexture = Resources.Load<Texture2D>(Root + "flag-" + cargo);
            cloth.SetTexture("_Linen", linen);
            cloth.SetVector("_PrintCenter", printCenter);
            cloth.SetVector("_PrintWidth", printWidth);
            cloth.SetVector("_PrintHeight", printHeight);
            flagMaterials[cargo] = cloth;
        }
        var flag = Place(rig, "HarborFlag", cloth);
        flag.gameObject.AddComponent<HarborSign>().Initialize();
        return root.gameObject;
    }

    static readonly Dictionary<string, Material> flagMaterials = new Dictionary<string, Material>();

    static void AddPier(Transform root)
    {
        LoadPier();
        Place(root, "PierDeck", timber);
        var shadow = new GameObject("栈桥水影").transform;
        shadow.SetParent(root,false);
        shadow.localScale = new Vector3(.068f,1,.068f);
        shadow.localPosition = Vector3.up*(TabletopOcean.WaterHeight+.00008f-EntityBoardAssets.BoardSurfaceHeight);
        shadow.gameObject.AddComponent<MeshFilter>().sharedMesh = Rectangle();
        var renderer = shadow.gameObject.AddComponent<MeshRenderer>();
        var markers = root.GetComponentInParent<TabletopReferenceMarkers>();
        renderer.sharedMaterial = markers != null && markers.PierWaterMaterial != null ? markers.PierWaterMaterial : waterShadow;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

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
        Rect.MinMaxRect(-.00572f,SeaEnd,.00572f,ShoreEnd),
        Rect.MinMaxRect(-.0152f,-.0214f,-.0140f,-.0202f)
    };
}
