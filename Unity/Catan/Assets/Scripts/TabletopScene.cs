using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

// 桌面仅使用已公开的顶点坐标；海框、木纹与相机不参与规则和落点裁决。
// v009：全部实体共用同一相机；布局坐标只用于基准摆放，缩放不移动桌上物件。
public sealed class TabletopScene : MonoBehaviour
{
    public static readonly Rect BoardArea = new Rect(0, 0, 1440, 900);
    public static readonly Rect BoardFitArea = new Rect(260, 105, 920, 520);
    public const int TableLayer = 30;
    readonly List<Material> materials = new List<Material>();
    readonly List<Mesh> meshes = new List<Mesh>();
    readonly List<Texture2D> textures = new List<Texture2D>();
    Vector3[] outline;
    Vector3 center;
    Camera layoutCamera, boardCamera;
    float baseSize;
    int originalCullingMask;
    float originalDepth;
    CameraClearFlags originalClearFlags;
    Rect originalCameraRect;
    bool originalAllowMsaa;
    bool originalCameraEnabled;
    bool originalOrthographic;
    float originalSize, originalNear, originalFar, originalAspect;
    Color originalBackground;
    Vector3 originalPosition;
    Quaternion originalRotation;
    RenderTexture originalTarget;
    public const float SurfaceHeight = -.0075f;
    public float UnitsPerPixel { get; private set; }
    public TabletopWorldObjects Objects { get; private set; }
    public Camera TableCamera => boardCamera;
    public float DisplayScale => baseSize / Mathf.Max(.001f, boardCamera.orthographicSize);
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
        SetTableLayer(table.transform);

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
        // 此相机从不渲染。固定的斜视基准把排布像素转换成桌面米制坐标。
        var cameraObject=new GameObject("桌面摆放坐标基准 v009");
        cameraObject.transform.SetParent(transform,false);
        layoutCamera=cameraObject.AddComponent<Camera>();
        layoutCamera.enabled=false;layoutCamera.orthographic=true;
        layoutCamera.nearClipPlane=.01f;layoutCamera.farClipPlane=10;
        layoutCamera.transform.rotation=Quaternion.Euler(40,0,0);
    }
    public static void SetTableLayer(Transform root)
    {
        root.gameObject.layer=TableLayer;
        foreach(Transform child in root)SetTableLayer(child);
    }
    public Vector3 World(Vector2 layoutPoint)
    {
        var ray=layoutCamera.ViewportPointToRay(new Vector3(layoutPoint.x/1440,1-layoutPoint.y/900,0));
        var plane=new Plane(Vector3.up,new Vector3(0,SurfaceHeight,0));
        return plane.Raycast(ray,out float distance)?ray.GetPoint(distance):Vector3.zero;
    }
    public Vector2 Project(Vector3 world)
    {
        var point=boardCamera.WorldToViewportPoint(world);return new Vector2(point.x*1440,(1-point.y)*900);
    }
    public Ray MouseRay(Vector2 logicalMouse) => boardCamera.ViewportPointToRay(new Vector3(logicalMouse.x/1440,1-logicalMouse.y/900,0));
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
        SetTableLayer(markers);
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
        if(boardCamera!=null&&!visible)
        {
            RestoreBoardCamera();
        }
        if (!visible && expansionMarkers != null)
        {
            expansionMarkers.position = markerOriginalPosition;
            expansionMarkers.localScale = markerOriginalScale;
            foreach(var child in expansionMarkers.GetComponentsInChildren<Transform>(true))child.gameObject.layer=0;
        }
    }

    public void Frame(Camera camera, bool top, float zoomMultiplier)
    {
        if(boardCamera==null)
        {
            boardCamera=camera;originalCullingMask=camera.cullingMask;originalClearFlags=camera.clearFlags;originalDepth=camera.depth;originalCameraRect=camera.rect;originalAllowMsaa=camera.allowMSAA;originalCameraEnabled=camera.enabled;
            originalOrthographic=camera.orthographic;originalSize=camera.orthographicSize;
            originalNear=camera.nearClipPlane;originalFar=camera.farClipPlane;originalAspect=camera.aspect;
            originalBackground=camera.backgroundColor;originalTarget=camera.targetTexture;
            originalPosition=camera.transform.position;originalRotation=camera.transform.rotation;
        }
        topView = top; zoom = zoomMultiplier;
        ApplyCamera();
    }

    void LateUpdate()
    {
        if (boardCamera != null && (screenWidth != Screen.width || screenHeight != Screen.height)) ApplyCamera();
        // 蛮族移动会生成新船，只修正显示层，绝不更改规则状态。
        if(expansionMarkers!=null)SetTableLayer(expansionMarkers);
    }

    void ApplyCamera()
    {
        screenWidth = Screen.width; screenHeight = Screen.height;
        float aspect = Mathf.Max(.1f, (float)screenWidth / Mathf.Max(1, screenHeight));
        layoutCamera.aspect=aspect;layoutCamera.rect=new Rect(0,0,1,1);
        var up=layoutCamera.transform.up;
        float minX=outline.Min(p=>p.x-center.x),maxX=outline.Max(p=>p.x-center.x);
        float minY=outline.Min(p=>Vector3.Dot(p-center,up));
        float maxY=outline.Max(p=>Vector3.Dot(p-center+Vector3.up*.035f,up));
        baseSize=Mathf.Max((maxY-minY)/2/(BoardFitArea.height/900),
            (maxX-minX)/(2*aspect)/(BoardFitArea.width/1440));
        UnitsPerPixel=baseSize*2*aspect/1440;
        layoutCamera.orthographicSize=baseSize;
        var focus=center+up*((minY+maxY)/2-(.5f-BoardFitArea.center.y/900)*baseSize*2);
        layoutCamera.transform.position=focus-layoutCamera.transform.forward*2;

        boardCamera.cullingMask=originalCullingMask|(1<<TableLayer);
        boardCamera.enabled=true;boardCamera.targetTexture=null;
        boardCamera.rect=new Rect(0,0,1,1);boardCamera.aspect=aspect;
        boardCamera.clearFlags=CameraClearFlags.SolidColor;
        boardCamera.backgroundColor=new Color(.22f,.11f,.045f);
        boardCamera.orthographic=true;boardCamera.nearClipPlane=.01f;boardCamera.farClipPlane=10;
        boardCamera.transform.rotation=Quaternion.Euler(topView?90:40,0,0);
        // 俯视展开整桌纵深，仍保留同一世界中的实体位置和比例。
        float viewScale=topView?1/Mathf.Sin(40*Mathf.Deg2Rad):1;
        boardCamera.orthographicSize=baseSize*zoom*viewScale;
        var tableFocus=World(new Vector2(720,450));
        boardCamera.transform.position=tableFocus-boardCamera.transform.forward*2;
        if (expansionTrack != null && expansionMarkers != null)
        {
            SetTableLayer(expansionMarkers);
            float scale=130*UnitsPerPixel/EntityBoardAssets.BarbarianTrackWidth;
            expansionMarkers.localScale=markerOriginalScale*scale;
            expansionMarkers.position+=World(new Vector2(1110,108))+Vector3.up*.0002f-expansionTrack.position;
        }
    }

    // 验证截图也使用游戏实际相机；没有第二份地图或屏幕合成层。
    public void RenderComposite(RenderTexture target)
    {
        if(boardCamera==null||target==null)return;
        var priorTarget=boardCamera.targetTexture;var priorActive=RenderTexture.active;
        try { boardCamera.targetTexture=target;boardCamera.Render(); }
        finally { boardCamera.targetTexture=priorTarget;RenderTexture.active=priorActive; }
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
    void OnEnable(){if(boardCamera!=null)ApplyCamera();}
    void OnDisable(){RestoreBoardCamera();}
    void RestoreBoardCamera()
    {
        if(boardCamera==null)return;
        boardCamera.cullingMask=originalCullingMask;boardCamera.clearFlags=originalClearFlags;boardCamera.depth=originalDepth;boardCamera.rect=originalCameraRect;boardCamera.allowMSAA=originalAllowMsaa;boardCamera.enabled=originalCameraEnabled;
        boardCamera.orthographic=originalOrthographic;boardCamera.orthographicSize=originalSize;
        boardCamera.nearClipPlane=originalNear;boardCamera.farClipPlane=originalFar;boardCamera.aspect=originalAspect;
        boardCamera.backgroundColor=originalBackground;boardCamera.targetTexture=originalTarget;
        boardCamera.transform.SetPositionAndRotation(originalPosition,originalRotation);
    }
}
