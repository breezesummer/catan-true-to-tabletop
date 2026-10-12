using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 桌面仅使用已公开的顶点坐标；海框、木纹与相机不参与规则和落点裁决。
// v003：米制、版本化橡木贴图；生成位图只作为桌面材质，原有实体资产和枢轴保持不变。
public sealed class TabletopScene : MonoBehaviour
{
    public static readonly Rect BoardArea = new Rect(230, 132, 980, 510);
    readonly List<Material> materials = new List<Material>();
    readonly List<Mesh> meshes = new List<Mesh>();
    readonly List<Texture2D> textures = new List<Texture2D>();
    Vector3[] outline;
    Vector3 center;
    Camera tableCamera;
    Material oakMaterial;
    float oakSpan = .8f;
    bool topView;
    float zoom = 1;
    int screenWidth, screenHeight;
    Transform expansionMarkers, expansionTrack;
    Vector3 markerOriginalPosition, markerOriginalScale;

    public static TabletopScene Create(Transform parent, IEnumerable<Vector3> publicVertices)
    {
        var root = new GameObject("完整橡木桌 v003");
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

        var wood = MakeMaterial("烟熏橡木哑光清漆 v003", new Color(.88f,.88f,.86f), .38f);
        var grain=Resources.Load<Texture2D>("TabletopUI/v003/oak-table-albedo");
        if(grain!=null)
        {
            // 至少覆盖中央 0.8 米；相机确定后扩大至完整可见桌面，避免边缘拉伸。
            wood.SetTexture("_BaseMap",grain);wood.SetTextureScale("_BaseMap",new Vector2(10,10));
            wood.SetTextureOffset("_BaseMap",new Vector2(-4.5f,-4.5f));
            oakMaterial=wood;
        }
        else
        {
            Texture2D normal;grain=OakTexture(out normal);textures.Add(grain);textures.Add(normal);
            wood.SetTexture("_BaseMap",grain);wood.SetTextureScale("_BaseMap",new Vector2(10,10));
            wood.SetTexture("_BumpMap",normal);wood.SetTextureScale("_BumpMap",new Vector2(10,10));
            wood.SetFloat("_BumpScale",.38f);wood.EnableKeyword("_NORMALMAP");
        }
        var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
        table.name = "大幅橡木拼板桌"; table.transform.SetParent(transform, false);
        table.transform.position = center + new Vector3(0, -.025f, 0);
        table.transform.localScale = new Vector3(8, .035f, 8);
        Destroy(table.GetComponent<Collider>());
        table.GetComponent<Renderer>().sharedMaterial = wood;

        var rim = MakeMaterial("海框纸板侧面", new Color(.025f, .16f, .22f), .16f);
        CreatePolygon("海框厚度", outline.Select(p => new Vector3(p.x, -.0025f, p.z)).ToArray(), rim, .004f);
        var sea = MakeMaterial("海面边框 v002", Color.white, .24f);
        var waves = SeaTexture(); textures.Add(waves); sea.SetTexture("_BaseMap", waves);
        CreatePolygon("海面边框", outline, sea, 0);
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
        var right = tableCamera.transform.right; var up = tableCamera.transform.up;
        float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
        foreach (var p in outline)
        {
            float x = Vector3.Dot(p - center, right), y = Vector3.Dot(p - center, up);
            minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
            minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
        }
        // 上方留出棋子高度；地图占用比例对应统一 1440 × 900 桌面坐标。
        maxY += .017f;
        float aspect = Mathf.Max(.1f, (float)screenWidth / Mathf.Max(1, screenHeight));
        float fitHeight = (maxY - minY) / (BoardArea.height / 900f * 2);
        float fitWidth = (maxX - minX) / (BoardArea.width / 1440f * aspect * 2);
        float size = Mathf.Max(fitHeight, fitWidth) * zoom;
        tableCamera.orthographicSize = size;
        var focus = center + up * ((maxY + minY) * .5f);
        float centerX = BoardArea.center.x / 1440f - .5f;
        float centerY = .5f - BoardArea.center.y / 900f;
        tableCamera.transform.position = focus - tableCamera.transform.forward * 2
            - right * (centerX * size * 2 * aspect) - up * (centerY * size * 2);
        if(oakMaterial!=null)
        {
            var plane=new Plane(Vector3.up,new Vector3(0,-.0075f,0));
            float span=oakSpan;
            for(int corner=0;corner<4;corner++)
            {
                var ray=tableCamera.ViewportPointToRay(new Vector3(corner%2,corner/2,0));
                if(!plane.Raycast(ray,out float distance))continue;
                var point=ray.GetPoint(distance)-center;
                span=Mathf.Max(span,2.1f*Mathf.Max(Mathf.Abs(point.x),Mathf.Abs(point.z)));
            }
            if(span>oakSpan)
            {
                oakSpan=span;float scale=8/span;
                oakMaterial.SetTextureScale("_BaseMap",new Vector2(scale,scale));
                oakMaterial.SetTextureOffset("_BaseMap",Vector2.one*(1-scale)/2);
            }
        }
        if (expansionTrack != null && expansionMarkers != null)
        {
            // 蛮族行进板放在上方右侧空桌面；投影宽度保持 135 像素，不挤压地图。
            var plane = new Plane(Vector3.up, Vector3.zero);
            var ray = tableCamera.ViewportPointToRay(new Vector3(1123f / 1440f, 1 - 75f / 900f, 0));
            if (plane.Raycast(ray, out float distance))
            {
                float scale = 135f / 1440f * size * 2 * aspect / EntityBoardAssets.BarbarianTrackWidth;
                expansionMarkers.localScale = markerOriginalScale * scale;
                expansionMarkers.position += ray.GetPoint(distance) - expansionTrack.position;
            }
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

    static Texture2D OakTexture(out Texture2D normal)
    {
        const int size = 1024;
        var texture = new Texture2D(size, size, TextureFormat.RGB24, true) {
            name = "烟熏橡木色彩 v003", wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear, anisoLevel = 8
        };
        var pixels = new Color[size * size];
        var heights = new float[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float u = x / (float)size, v = y / (float)size;
            // 宽拼板、缓慢弯曲的年轮、沿纹理的导管与细小木射线；确定性生成。
            int plank=Mathf.Min(5,Mathf.FloorToInt(v*6));float across=v*6-plank;
            float broad=PeriodicNoise(u,v,3,19,11),fibre=PeriodicNoise(u,v,16,420,63);
            float bend=Mathf.Sin(u*Mathf.PI*2+plank*1.7f)*.028f
                +Mathf.Sin(u*Mathf.PI*4+plank*.7f)*.011f+(broad-.5f)*.048f;
            float coordinate=v+bend;
            float ring=.5f+.5f*Mathf.Sin(coordinate*Mathf.PI*112);
            float fine=.5f+.5f*Mathf.Sin(coordinate*Mathf.PI*380+Mathf.Sin(u*Mathf.PI*8));
            float pores=PeriodicNoise(u,coordinate-Mathf.Floor(coordinate),44,512,87);
            float rays=Mathf.Pow(PeriodicNoise(u,v,110,37,193),15)*.12f;
            float join=1-Mathf.SmoothStep(0,.007f,Mathf.Min(across,1-across));
            float boardTone=(NoiseValue(plank,0,41)-.5f)*.09f;
            float tone=.72f+boardTone+(broad-.5f)*.20f-Mathf.Pow(ring,9)*.105f
                -Mathf.Pow(fine,13)*.05f+(fibre-.5f)*.11f-(pores<.30f?.035f:0)+rays-join*.18f;
            pixels[y*size+x]=Color.Lerp(new Color(.29f,.195f,.105f),new Color(.68f,.51f,.31f),Mathf.Clamp01(tone));
            heights[y*size+x]=.5f+(fibre-.5f)*.055f-Mathf.Pow(fine,13)*.015f-join*.06f;
        }
        normal=new Texture2D(size,size,TextureFormat.RGBA32,true,true){name="橡木细导管法线 v003",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=8};
        var normals=new Color[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float dx=heights[y*size+(x+1)%size]-heights[y*size+(x+size-1)%size];
            float dy=heights[((y+1)%size)*size+x]-heights[((y+size-1)%size)*size+x];
            var n=new Vector3(-dx*3,-dy*3,1).normalized;
            normals[y*size+x]=new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f,1);
        }
        normal.SetPixels(normals);normal.Apply(true,true);
        texture.SetPixels(pixels); texture.Apply(true, true); return texture;
    }

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
        var texture = new Texture2D(size, size, TextureFormat.RGB24, true) { name = "固定海纹 v002", wrapMode = TextureWrapMode.Repeat };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float u = x / (float)size, v = y / (float)size;
            float noise = PeriodicNoise(u, v, 9, 13, 29);
            float wave = Mathf.Sin(Mathf.PI * 2 * (6 * v + .21f * Mathf.Sin(Mathf.PI * 6 * u)
                + .15f * Mathf.Sin(Mathf.PI * 2 * (2 * u + v))));
            float foam = Mathf.Pow(Mathf.Max(0, wave), 18) * .085f;
            pixels[y * size + x] = Color.Lerp(new Color(.035f, .22f, .36f), new Color(.12f, .44f, .60f), noise) + new Color(foam, foam, foam, 0);
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
