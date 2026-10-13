using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

// 桌面仅使用已公开的顶点坐标；海框、木纹与相机不参与规则和落点裁决。
// v008：地图只在中央视口内缩放；备件、银行、骰子和设施使用固定桌面相机。
public sealed class TabletopScene : MonoBehaviour
{
    public static readonly Rect BoardArea = new Rect(248, 145, 722, 448);
    public const int TableLayer = 30;
    readonly List<Material> materials = new List<Material>();
    readonly List<Mesh> meshes = new List<Mesh>();
    readonly List<Texture2D> textures = new List<Texture2D>();
    Vector3[] outline;
    Vector3 center;
    Camera tableCamera, boardCamera, mapCamera;
    RenderTexture mapTexture;
    int originalCullingMask;
    float originalDepth;
    CameraClearFlags originalClearFlags;
    Rect originalCameraRect;
    bool originalAllowMsaa;
    bool originalCameraEnabled;
    public const float SurfaceHeight = -.0075f;
    public float UnitsPerPixel { get; private set; }
    public TabletopWorldObjects Objects { get; private set; }
    public Camera TableCamera => tableCamera;
    public float DisplayScale => 1;
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
        // 桌面组件用固定米制比例，不再由地图大小或地图缩放反推个人区占地。
        UnitsPerPixel=.0005f;
        var cameraObject=new GameObject("固定个人区与银行相机 v008");
        cameraObject.transform.SetParent(transform,false);
        tableCamera=cameraObject.AddComponent<Camera>();
        tableCamera.orthographic=true;
        tableCamera.cullingMask=1<<TableLayer;
        tableCamera.clearFlags=CameraClearFlags.SolidColor;
        tableCamera.backgroundColor=new Color(.22f,.11f,.045f);
        tableCamera.nearClipPlane=.01f;tableCamera.farClipPlane=10;
        tableCamera.transform.rotation=Quaternion.Euler(40,0,0);
        var mapObject=new GameObject("透明地图纹理相机 v008");mapObject.transform.SetParent(transform,false);
        mapCamera=mapObject.AddComponent<Camera>();mapCamera.enabled=false;
    }
    public static void SetTableLayer(Transform root)
    {
        root.gameObject.layer=TableLayer;
        foreach(Transform child in root)SetTableLayer(child);
    }
    public Vector3 World(Vector2 layoutPoint)
    {
        var ray=MouseRay(layoutPoint);
        var plane=new Plane(Vector3.up,new Vector3(0,SurfaceHeight,0));
        return plane.Raycast(ray,out float distance)?ray.GetPoint(distance):Vector3.zero;
    }
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
        tableCamera.rect=new Rect(0,0,1,1);tableCamera.depth=originalDepth-1;tableCamera.aspect=aspect;
        tableCamera.orthographicSize=UnitsPerPixel*1440/aspect/2;
        // 同一桌面的观察俯角保持一致；World 每次按当前相机把周边组件放回各自屏幕分区。
        tableCamera.transform.rotation=Quaternion.Euler(topView?90:40,0,0);
        tableCamera.transform.position=new Vector3(center.x,SurfaceHeight,center.z)-tableCamera.transform.forward*2;

        boardCamera.cullingMask=originalCullingMask&~(1<<TableLayer);
        // 主相机只保留屏幕投影/拾取接口；不绑定 RT，避免 WorldToScreenPoint 变为纹理坐标。
        boardCamera.enabled=false;
        boardCamera.depth=originalDepth;
        boardCamera.rect=new Rect(BoardArea.x/1440,1-BoardArea.yMax/900,BoardArea.width/1440,BoardArea.height/900);
        boardCamera.orthographic=true;boardCamera.transform.rotation=Quaternion.Euler(topView?90:40,0,0);
        var up=boardCamera.transform.up;
        float minX=outline.Min(p=>p.x-center.x),maxX=outline.Max(p=>p.x-center.x);
        float minY=outline.Min(p=>Vector3.Dot(p-center,up));
        // 为最上沿城堡及骑士保留高度；镜头放大后仅裁剪地图，不侵入周边操作区。
        float maxY=outline.Max(p=>Vector3.Dot(p-center+Vector3.up*.048f,up));
        float boardAspect=aspect*(BoardArea.width/1440)/(BoardArea.height/900);
        boardCamera.orthographicSize=Mathf.Max((maxY-minY)/2,(maxX-minX)/(2*boardAspect))*zoom;
        boardCamera.transform.position=center+up*((minY+maxY)/2)-boardCamera.transform.forward*2;
        ConfigureMapTexture();
        if (expansionTrack != null && expansionMarkers != null)
        {
            SetTableLayer(expansionMarkers);
            float scale=135*UnitsPerPixel/EntityBoardAssets.BarbarianTrackWidth;
            expansionMarkers.localScale=markerOriginalScale*scale;
            expansionMarkers.position+=World(new Vector2(1123,75))+Vector3.up*.0002f-expansionTrack.position;
            FitExpansionDisplay();
        }
    }

    void FitExpansionDisplay()
    {
        var renderers=expansionMarkers.GetComponentsInChildren<Renderer>();
        if(renderers.Length==0)return;
        var bounds=renderers[0].bounds;
        foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
        var projected=Project(bounds);
        if(projected.yMin>=20&&projected.yMax<=137)return;
        // 俯视会展开纸板深度；只在超出上方区域时同比缩小，保留完整轨道和船。
        float scale=Mathf.Min(1,117/Mathf.Max(.001f,projected.height));
        expansionMarkers.localScale*=scale;
        expansionMarkers.position+=World(new Vector2(1123,75))+Vector3.up*.0002f-expansionTrack.position;
        bounds=renderers[0].bounds;
        foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
        projected=Project(bounds);
        float shift=projected.yMin<20?20-projected.yMin:projected.yMax>137?137-projected.yMax:0;
        expansionMarkers.position+=World(new Vector2(1123,75+shift))-World(new Vector2(1123,75));
    }

    void ConfigureMapTexture()
    {
        if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
        int width=Mathf.Max(1,Mathf.RoundToInt(Screen.width*BoardArea.width/1440));
        int height=Mathf.Max(1,Mathf.RoundToInt(Screen.height*BoardArea.height/900));
        if(mapTexture==null||mapTexture.width!=width||mapTexture.height!=height)
        {
            if(mapTexture!=null){mapTexture.Release();Destroy(mapTexture);}
            mapTexture=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){name="中央地图透明合成 v008",antiAliasing=1,filterMode=FilterMode.Bilinear};
            mapTexture.Create();
        }
        mapCamera.CopyFrom(boardCamera);mapCamera.targetTexture=mapTexture;mapCamera.rect=new Rect(0,0,1,1);
        mapCamera.aspect=boardCamera.aspect;mapCamera.allowHDR=false;mapCamera.allowMSAA=false;
        mapCamera.clearFlags=CameraClearFlags.SolidColor;mapCamera.backgroundColor=Color.clear;
        mapCamera.transform.SetPositionAndRotation(boardCamera.transform.position,boardCamera.transform.rotation);
        mapCamera.enabled=true;
    }

    void OnGUI()
    {
        GUI.depth=100;
        if(mapTexture==null||Event.current.type!=EventType.Repaint)return;
        var priorMatrix=GUI.matrix;var priorColor=GUI.color;bool priorEnabled=GUI.enabled;
        GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;
        GUI.DrawTexture(new Rect(BoardArea.x*Screen.width/1440,BoardArea.y*Screen.height/900,
            BoardArea.width*Screen.width/1440,BoardArea.height*Screen.height/900),mapTexture,ScaleMode.StretchToFill,true);
        GUI.matrix=priorMatrix;GUI.color=priorColor;GUI.enabled=priorEnabled;
    }

    // 验证工具与实际窗口使用相同的透明地图合成，主投影相机始终不改变输出目标。
    public void RenderComposite(RenderTexture target)
    {
        if(mapTexture==null||target==null)return;
        var priorTarget=tableCamera.targetTexture;var priorActive=RenderTexture.active;
        try
        {
            tableCamera.targetTexture=target;tableCamera.Render();tableCamera.targetTexture=priorTarget;
            mapCamera.Render();RenderTexture.active=target;
            GL.PushMatrix();
            try
            {
                GL.LoadPixelMatrix(0,target.width,target.height,0);
                Graphics.DrawTexture(new Rect(BoardArea.x*target.width/1440,BoardArea.y*target.height/900,
                    BoardArea.width*target.width/1440,BoardArea.height*target.height/900),mapTexture);
            }
            finally{GL.PopMatrix();}
        }
        finally{tableCamera.targetTexture=priorTarget;RenderTexture.active=priorActive;}
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
        if(mapTexture!=null){mapTexture.Release();Destroy(mapTexture);}
    }
    void OnEnable(){if(boardCamera!=null)ApplyCamera();}
    void OnDisable(){if(mapCamera!=null)mapCamera.enabled=false;RestoreBoardCamera();}
    void RestoreBoardCamera()
    {
        if(boardCamera==null)return;
        boardCamera.cullingMask=originalCullingMask;boardCamera.clearFlags=originalClearFlags;boardCamera.depth=originalDepth;boardCamera.rect=originalCameraRect;boardCamera.allowMSAA=originalAllowMsaa;boardCamera.enabled=originalCameraEnabled;
    }
}
