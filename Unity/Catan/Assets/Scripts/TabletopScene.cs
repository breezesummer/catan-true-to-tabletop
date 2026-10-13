using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

// 桌面仅使用已公开的顶点坐标；海框、木纹与相机不参与规则和落点裁决。
// v005：固定桌面坐标、低对比微表面橡木；桌面与手牌显示分离。
public sealed class TabletopScene : MonoBehaviour
{
    public static readonly Rect BoardArea = new Rect(230, 132, 980, 510);
    readonly List<Material> materials = new List<Material>();
    readonly List<Mesh> meshes = new List<Mesh>();
    readonly List<Texture2D> textures = new List<Texture2D>();
    Vector3[] outline;
    Vector3 center;
    Camera tableCamera;
    Vector3 layoutOrigin;
    float layoutSize, layoutDepthPixel;
    public const float SurfaceHeight = -.0075f;
    public float UnitsPerPixel { get; private set; }
    public TabletopWorldObjects Objects { get; private set; }
    public float DisplayScale => layoutSize / tableCamera.orthographicSize;
    bool topView;
    float zoom = 1;
    int screenWidth, screenHeight;
    Transform expansionMarkers, expansionTrack;
    Vector3 markerOriginalPosition, markerOriginalScale;

    public static TabletopScene Create(Transform parent, IEnumerable<Vector3> publicVertices)
    {
        var root = new GameObject("烟熏橡木实体桌 v005");
        root.transform.SetParent(parent, false);
        var scene = root.AddComponent<TabletopScene>();
        scene.Initialize(publicVertices.ToArray());
        return scene;
    }

    void Initialize(Vector3[] publicVertices)
    {
        var hull = ConvexHull(publicVertices);
        center = new Vector3((hull.Min(p => p.x) + hull.Max(p => p.x)) * .5f, 0,
            (hull.Min(p => p.z) + hull.Max(p => p.z)) * .5f);
        outline = new Vector3[hull.Count];
        for (int i = 0; i < hull.Count; i++)
        {
            var direction = (hull[i] - center).normalized;
            outline[i] = hull[i] + direction * .027f;
            outline[i].y = -.001f;
        }

        ConfigureLighting();
        var wood = MakeMaterial("烟熏橡木细纹哑光清漆 v005", Color.white, .29f);
        var grain=Resources.Load<Texture2D>("TabletopUI/v005/smoked-oak-albedo");
        if(grain!=null)
        {
            grain.filterMode=FilterMode.Trilinear;grain.anisoLevel=16;
            // 纹理按 0.75 米的实际覆盖范围平铺；无等距拼接线，也不随视角放大纹理。
            wood.SetTexture("_BaseMap",grain);wood.SetTextureScale("_BaseMap",Vector2.one*(8/.75f));
            var normal=Resources.Load<Texture2D>("TabletopUI/v005/smoked-oak-normal");
            if(normal!=null)
            {
                wood.SetTexture("_BumpMap",normal);
                wood.SetFloat("_BumpScale",.32f);wood.EnableKeyword("_NORMALMAP");
            }
        }
        else wood.color = new Color(.335f,.266f,.202f);
        var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
        table.name = "细纹哑光橡木桌面"; table.transform.SetParent(transform, false);
        table.transform.position = center + new Vector3(0, -.025f, 0);
        table.transform.localScale = new Vector3(8, .035f, 8);
        Destroy(table.GetComponent<Collider>());
        table.GetComponent<Renderer>().sharedMaterial = wood;

        var rim = MakeMaterial("海框纸板侧面", new Color(.025f, .16f, .22f), .16f);
        CreatePolygon("海框厚度", outline.Select(p => new Vector3(p.x, -.0025f, p.z)).ToArray(), rim, .004f);
        var sea = MakeMaterial("深青色覆膜海框 v005", Color.white, .32f);
        var waves = SeaTexture(); textures.Add(waves); sea.SetTexture("_BaseMap", waves);
        CreatePolygon("海面边框", outline, sea, 0);
        InitializeLayout();
        Objects=gameObject.AddComponent<TabletopWorldObjects>();Objects.Initialize(this);
    }

    void ConfigureLighting()
    {
        // 用方向明确的柔和主光体现现有雕塑网格；三色环境光保留侧面层次。
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.44f,.47f,.51f);
        RenderSettings.ambientEquatorColor=new Color(.28f,.30f,.32f);
        RenderSettings.ambientGroundColor=new Color(.18f,.15f,.12f);
        foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if(light.type!=LightType.Directional)continue;
            light.intensity=1.22f;
            light.color=new Color(1,.95f,.87f);
            light.transform.rotation=Quaternion.Euler(48,-38,0);
            light.shadows=LightShadows.Soft;
            light.shadowStrength=.78f;
            light.shadowBias=.08f;light.shadowNormalBias=.08f;
            break;
        }
    }

    void InitializeLayout()
    {
        var rotation=Quaternion.Euler(40,0,0);var up=rotation*Vector3.up;var forward=rotation*Vector3.forward;
        float minX=outline.Min(p=>p.x-center.x),maxX=outline.Max(p=>p.x-center.x);
        float minY=outline.Min(p=>Vector3.Dot(p-center,up)),maxY=outline.Max(p=>Vector3.Dot(p-center,up))+.017f;
        layoutSize=Mathf.Max((maxY-minY)/(BoardArea.height/900f*2),(maxX-minX)/(BoardArea.width/1440f*1.6f*2));
        UnitsPerPixel=layoutSize*2*1.6f/1440;
        layoutDepthPixel=layoutSize*2/(900*Mathf.Sin(40*Mathf.Deg2Rad));
        var cameraPosition=center+up*((maxY+minY)*.5f)-forward*2-up*((.5f-BoardArea.center.y/900)*layoutSize*2);
        layoutOrigin=cameraPosition+forward*((SurfaceHeight-cameraPosition.y)/forward.y);
    }
    public Vector3 World(Vector2 layoutPoint) => layoutOrigin+new Vector3((layoutPoint.x-720)*UnitsPerPixel,0,(450-layoutPoint.y)*layoutDepthPixel);
    public Vector2 Project(Vector3 world)
    {
        var point=tableCamera.WorldToViewportPoint(world);return new Vector2(point.x*1440,(1-point.y)*900);
    }
    public Ray MouseRay(Vector2 logicalMouse) => tableCamera.ViewportPointToRay(new Vector3(logicalMouse.x/1440,1-logicalMouse.y/900,0));
    public Rect ProjectSurface(Vector3 position,Vector2 size,Quaternion rotation)
    {
        var points=new Vector2[4];
        for(int i=0;i<4;i++)points[i]=Project(position+rotation*new Vector3((i%2==0?-.5f:.5f)*size.x,0,(i/2==0?-.5f:.5f)*size.y));
        return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
    }
    public Rect Project(Bounds bounds)
    {
        var points=new Vector2[8];
        for(int i=0;i<8;i++)points[i]=Project(bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
        return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
    }
    public Rect ProjectLabel(Rect layout)
    {
        var point=Project(World(layout.center));
        return new Rect(point.x-layout.width*DisplayScale/2,point.y-layout.height*DisplayScale/2,layout.width*DisplayScale,layout.height*DisplayScale);
    }

    public void BindExpansionDisplay(Transform markers, Transform track)
    {
        expansionMarkers = markers; expansionTrack = track;
        if (markers == null) return;
        markerOriginalPosition = markers.position; markerOriginalScale = markers.localScale;
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
        if (!visible && expansionMarkers != null)
        {
            expansionMarkers.position = markerOriginalPosition;
            expansionMarkers.localScale = markerOriginalScale;
        }
    }

    public void Frame(Camera camera, bool top, float zoomMultiplier)
    {
        tableCamera = camera; topView = top; zoom = zoomMultiplier;
        ApplyCamera();
    }

    void LateUpdate()
    {
        if (tableCamera != null && (screenWidth != Screen.width || screenHeight != Screen.height)) ApplyCamera();
    }

    void ApplyCamera()
    {
        screenWidth = Screen.width; screenHeight = Screen.height;
        tableCamera.rect = new Rect(0, 0, 1, 1);
        tableCamera.orthographic = true;
        tableCamera.backgroundColor = new Color(.22f, .11f, .045f);
        tableCamera.transform.rotation = Quaternion.Euler(topView ? 90 : 40, 0, 0);
        float aspect = Mathf.Max(.1f, (float)screenWidth / Mathf.Max(1, screenHeight));
        float fitHeight=layoutDepthPixel*900*Mathf.Sin((topView?90:40)*Mathf.Deg2Rad)/2;
        float fitWidth=UnitsPerPixel*1440/aspect/2;
        float size=Mathf.Max(fitHeight,fitWidth)*zoom;
        tableCamera.orthographicSize = size;
        tableCamera.transform.position=layoutOrigin-tableCamera.transform.forward*2;
        if (expansionTrack != null && expansionMarkers != null)
        {
            // 固定物理占地与位置；蛮族板和地图一起缩放。
            float scale=135*UnitsPerPixel/EntityBoardAssets.BarbarianTrackWidth;
            expansionMarkers.localScale=markerOriginalScale*scale;
            expansionMarkers.position+=World(new Vector2(1123,75))+Vector3.up*.0002f-expansionTrack.position;
        }
    }

    Material MakeMaterial(string label, Color color, float smoothness)
    {
        var material = BoardRenderer.Material(color);
        material.name = label; material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", 0); materials.Add(material); return material;
    }

    void CreatePolygon(string label, Vector3[] boundary, Material material, float thickness)
    {
        var points = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
        var middle = boundary.Aggregate(Vector3.zero, (sum, p) => sum + p) / boundary.Length;
        for (int i = 0; i < boundary.Length; i++)
        {
            var a = boundary[i]; var b = boundary[(i + 1) % boundary.Length];
            int start = points.Count;
            points.Add(middle); points.Add(b); points.Add(a);
            uv.Add(new Vector2(middle.x * 8, middle.z * 8));
            uv.Add(new Vector2(b.x * 8, b.z * 8)); uv.Add(new Vector2(a.x * 8, a.z * 8));
            triangles.AddRange(new[] { start, start + 1, start + 2 });
            if (thickness <= 0) continue;
            start = points.Count;
            points.Add(a); points.Add(b); points.Add(b - Vector3.up * thickness); points.Add(a - Vector3.up * thickness);
            uv.AddRange(new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
            triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }
        var mesh = new Mesh { name = label + "（公开轮廓网格）" };
        mesh.SetVertices(points); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); meshes.Add(mesh);
        var obj = new GameObject(label); obj.transform.SetParent(transform, false);
        obj.AddComponent<MeshFilter>().sharedMesh = mesh; obj.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    static List<Vector3> ConvexHull(Vector3[] points)
    {
        var sorted = points.Select(p => new Vector3(p.x, 0, p.z)).Distinct().OrderBy(p => p.x).ThenBy(p => p.z).ToArray();
        var result = new List<Vector3>();
        foreach (var p in sorted)
        {
            while (result.Count >= 2 && Cross(result[result.Count - 2], result[result.Count - 1], p) <= 0) result.RemoveAt(result.Count - 1);
            result.Add(p);
        }
        int lower = result.Count;
        for (int i = sorted.Length - 2; i >= 0; i--)
        {
            var p = sorted[i];
            while (result.Count > lower && Cross(result[result.Count - 2], result[result.Count - 1], p) <= 0) result.RemoveAt(result.Count - 1);
            result.Add(p);
        }
        result.RemoveAt(result.Count - 1); return result;
    }

    static float Cross(Vector3 a, Vector3 b, Vector3 p) => (b.x - a.x) * (p.z - a.z) - (b.z - a.z) * (p.x - a.x);

    // 固定散列插值，不使用规则随机数；网格索引取模使纹理能够无缝重复。
    static float PeriodicNoise(float u, float v, int periodX, int periodY, uint seed)
    {
        float x = u * periodX, y = v * periodY;
        int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
        float fx = x - ix, fy = y - iy;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        float a = NoiseValue(ix % periodX, iy % periodY, seed);
        float b = NoiseValue((ix + 1) % periodX, iy % periodY, seed);
        float c = NoiseValue(ix % periodX, (iy + 1) % periodY, seed);
        float d = NoiseValue((ix + 1) % periodX, (iy + 1) % periodY, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static float NoiseValue(int x, int y, uint seed)
    {
        unchecked
        {
            uint value = (uint)x * 374761393u + (uint)y * 668265263u + seed * 1442695041u;
            value = (value ^ (value >> 13)) * 1274126177u;
            value ^= value >> 16; return (value & 65535u) / 65535f;
        }
    }

    static Texture2D SeaTexture()
    {
        const int size = 256;
        var texture = new Texture2D(size, size, TextureFormat.RGB24, true) {
            name = "深青细纹海框 v005", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 16
        };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float u = x / (float)size, v = y / (float)size;
            float broad = PeriodicNoise(u, v, 4, 4, 29) - .5f;
            float fine = PeriodicNoise(u, v, 54, 54, 79) - .5f;
            float variation = broad * .032f + fine * .012f;
            pixels[y * size + x] = new Color(.052f + variation * .5f, .246f + variation, .322f + variation);
        }
        texture.SetPixels(pixels); texture.Apply(true, true); return texture;
    }

    void OnDestroy()
    {
        foreach (var material in materials) Destroy(material);
        foreach (var mesh in meshes) Destroy(mesh);
        foreach (var texture in textures) Destroy(texture);
    }
}
