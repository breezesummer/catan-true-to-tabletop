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

    static void LoadPier()
    {
        if (timber != null) return;
        var library = Resources.Load<GameObject>(Root + "pier");
        foreach (var part in library.GetComponentsInChildren<MeshFilter>())
            parts[part.name] = part;
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
        if(!BoardArtPreference.Painted) AddPier(root);
        else if(cargo=="wood") root.gameObject.AddComponent<PaintedTimberPort>();
        else AddPaintedDock(root,cargo);
        var markers = parent != null ? parent.GetComponentInParent<TabletopReferenceMarkers>() : null;
        var assets = JadeNumberAssets.For(markers != null ? markers.transform.parent : parent != null ? parent : root);
        var slip = assets.CreateSlip(root, "港口贸易玉签", Root+"sign-"+cargo);
        slip.gameObject.AddComponent<HarborSign>().Initialize(slip);
        return root.gameObject;
    }

    static readonly Dictionary<string,Material> paintedMaterials=new Dictionary<string,Material>();
    static Mesh paintedDock;
    static readonly Rect[] PaintedSections={
        Rect.MinMaxRect(-.0105f,SeaEnd,.0105f,-.00027f),
        Rect.MinMaxRect(-.0034f,-.00027f,.0034f,ShoreEnd)
    };
    static void AddPaintedDock(Transform root,string cargo)
    {
        if(paintedDock==null)
        {
            var points=new List<Vector3>();
            var uv=new List<Vector2>();
            var indices=new List<int>();
            foreach(var r in PaintedSections)
            {
                int start=points.Count;
                foreach(var p in new[]{XZ(r.xMin,r.yMin),XZ(r.xMin,r.yMax),XZ(r.xMax,r.yMax),XZ(r.xMax,r.yMin)})
                {
                    points.Add(p);
                    uv.Add(new Vector2(.5f+p.x/.021f,(p.z-SeaEnd)/DockLength));
                }
                foreach(int index in Quad)indices.Add(start+index);
            }
            paintedDock=Surface("港口画稿轮廓",points.ToArray(),uv.ToArray(),indices.ToArray());
        }
        if(!paintedMaterials.TryGetValue(cargo,out var material))
        {
            material=new Material(Resources.Load<Shader>("FantasyPieces/FantasyInk"))
                {mainTexture=Resources.Load<Texture2D>(Root+"port-"+cargo),renderQueue=3001};
            paintedMaterials[cargo]=material;
        }
        var face=new GameObject("港口原画");
        face.transform.SetParent(root,false);
        face.transform.localPosition=Vector3.up*.00035f;
        face.AddComponent<MeshFilter>().sharedMesh=paintedDock;
        var renderer=face.AddComponent<MeshRenderer>();
        renderer.sharedMaterial=material;
        renderer.shadowCastingMode=ShadowCastingMode.Off;
    }

    static void AddPier(Transform root)
    {
        LoadPier();
        Place(root, "PierDeck", timber);
        var shadow = new GameObject("栈桥水影").transform;
        shadow.SetParent(root,false);
        shadow.localScale = new Vector3(.038f,1,.038f);
        shadow.localPosition = Vector3.up*(TabletopOcean.WaterHeight+.00008f-EntityBoardAssets.BoardSurfaceHeight);
        shadow.gameObject.AddComponent<MeshFilter>().sharedMesh = Rectangle();
        var renderer = shadow.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = waterShadow;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    // 避让渔场与取景只需桥面投影；每块木板、缆绳和货物都在这两段轮廓内。
    public static IEnumerable<Vector3[]> Footprint(Transform root)
    {
        if(root.TryGetComponent<PaintedTimberPort>(out var timberPort))
        {
            foreach(var triangle in timberPort.Footprint()) yield return triangle;
            yield break;
        }
        foreach (var rect in root.Find("港口原画")!=null ? PaintedSections : DeckSections)
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
