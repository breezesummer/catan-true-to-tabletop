using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Catan.Core;
using UnityEngine;
using UnityEngine.Rendering;

// 仅消费公开地形；遮罩分帧生成，波浪全部在 GPU 上运行，不进入规则、拾取或网络。
public sealed class TabletopOcean : MonoBehaviour
{
    public const float WaterHeight = .0045f;
    public bool MaskReady { get; private set; }
    public int MaskRevision { get; private set; }
    OceanThemeEnvironment scenery;
    Material material;
    Texture2D seaWash;
    int themeVersion=-1;
    internal Material WakeMaterial { get; private set; }
    Texture2D mask;
    string layoutKey;
    Tile[] lastTiles;
    Dictionary<string, Vector3> lastVertices;
    Coroutine baking;
    readonly List<Renderer> hiddenSea = new List<Renderer>();
    struct Hex { public Vector2 Center; public float Radius; public bool Sea; }

    public void Initialize(Vector3 center)
    {
        var model = Resources.Load<GameObject>("Ocean/v001/" + (Application.isMobilePlatform ? "ocean-mobile" : "ocean-desktop"));
        var surface = Instantiate(model, center, model.transform.rotation, transform);
        surface.name = "连续海面：轻浪与反光";
        material = new Material(Resources.Load<Shader>("Ocean/v001/ContinuousOcean"));
        material.name = "连续海面 v001";
        WakeMaterial=new Material(Resources.Load<Shader>("Ocean/v001/ShipWake"));
        material.SetFloat("_WaterHeight", WaterHeight);
        material.SetTexture("_OceanDetail",Resources.Load<Texture2D>("Ocean/v003/ocean-detail"));
        material.SetTexture("_Gravel",Resources.Load<Texture2D>(CoastalDecals.Root+"gravel"));
        material.SetTexture("_Sand",Resources.Load<Texture2D>(CoastalDecals.Root+"sand"));
        RefreshTheme();
        material.SetVector("_MaskRect", new Vector4(0,0,.00001f,.00001f));
        foreach (var renderer in surface.GetComponentsInChildren<Renderer>())
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        TabletopScene.SetTableLayer(surface.transform);
        var islands=new GameObject("近岸礁石");islands.transform.SetParent(transform,false);
        scenery=islands.AddComponent<OceanThemeEnvironment>();scenery.Initialize(material);
    }

    internal void SetPirateHex(Vector4 hex)
    {
        if (material != null && material.GetVector(PaintedMap.PirateHex) != hex)
            material.SetVector(PaintedMap.PirateHex,hex);
    }

    void Update()
    {
        if(material!=null && themeVersion!=StrategyHudTheme.Version) RefreshTheme();
    }

    void RefreshTheme()
    {
        themeVersion=StrategyHudTheme.Version;
        if(StrategyHudTheme.HandPainted && seaWash==null)
        {
            seaWash=Resources.Load<Texture2D>("TabletopUI/HandPainted/sea-wash");
            material.SetTexture("_SeaWash",seaWash);
        }
        material.SetFloat("_PaintedSea",StrategyHudTheme.HandPainted && seaWash!=null?1:0);
    }

    public void Refresh(Tile[] tiles, Dictionary<string, Vector3> vertices)
    {
        lastTiles=tiles; lastVertices=vertices;
        string key = string.Join("|", tiles.Select(t => t.Id + ":" + t.Resource + ":" + string.Join(",", t.Vertices)));
        if (key == layoutKey) return;
        layoutKey = key;
        foreach (var tile in transform.parent.GetComponentsInChildren<OceanTileSurface>(true))
            foreach (var renderer in tile.GetComponentsInChildren<Renderer>())
                if (renderer.enabled) { renderer.enabled = false; hiddenSea.Add(renderer); }
        var hexes = tiles.Where(t => t.Vertices.Length > 0 && t.Vertices.All(vertices.ContainsKey)).Select(t =>
        {
            var points = t.Vertices.Select(id => vertices[id]).ToArray();
            var center = points.Aggregate(Vector3.zero, (sum, p) => sum + p) / points.Length;
            return new Hex { Center = new Vector2(center.x, center.z), Radius = Vector2.Distance(new Vector2(points[0].x, points[0].z), new Vector2(center.x, center.z)), Sea = t.Resource == "sea" || t.Resource == "water" };
        }).ToArray();
        var land = hexes.Where(h => !h.Sea).ToArray();
        if (land.Length > 0)
        {
            var coast = land.Select(h => h.Center).ToArray();
            scenery.Place(coast, land.Max(h => h.Radius));
        }
        if (baking != null) StopCoroutine(baking);
        baking = StartCoroutine(Bake(hexes));
    }

    IEnumerator Bake(Hex[] hexes)
    {
        MaskReady = false;
        const int size = 512;
        float minX = hexes.Min(h => h.Center.x - h.Radius) - .10f;
        float minZ = hexes.Min(h => h.Center.y - h.Radius) - .10f;
        float width = hexes.Max(h => h.Center.x + h.Radius) + .10f - minX;
        float depth = hexes.Max(h => h.Center.y + h.Radius) + .10f - minZ;
        var pixels = new Color32[size * size];
        var clock = System.Diagnostics.Stopwatch.StartNew();
        for (int y = 0; y < size; y++)
        {
            float z = minZ + (y + .5f) / size * depth;
            for (int x = 0; x < size; x++)
            {
                float wx = minX + (x + .5f) / size * width;
                float shore = .08f, grid = 0;
                foreach (var hex in hexes)
                {
                    float dx = wx - hex.Center.x, dz = z - hex.Center.y;
                    float d = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(.5f * dx + .8660254f * dz), Mathf.Abs(-.5f * dx + .8660254f * dz)) - hex.Radius * .8660254f;
                    if (hex.Sea)
                    {
                        if (d <= 0) grid = Mathf.Max(grid, 1 - Mathf.Clamp01(-d / .0016f));
                    }
                    else shore = Mathf.Min(shore, Mathf.Max(0, d));
                }
                pixels[y * size + x] = new Color(shore / .08f, grid, 0, 1);
            }
            // 每批最多约 3ms；大航海地图也不把整张遮罩计算塞进一帧。
            if (clock.ElapsedMilliseconds >= 3) { yield return null; clock.Restart(); }
        }
        var next = new Texture2D(size, size, TextureFormat.RGBA32, false, true) { name = "公开海岸与航海格遮罩", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        next.SetPixels32(pixels); next.Apply(false, true);
        material.SetTexture("_CoastMask", next);
        material.SetVector("_MaskRect", new Vector4(minX, minZ, width, depth));
        WakeMaterial.SetTexture("_CoastMask",next);
        WakeMaterial.SetVector("_MaskRect",new Vector4(minX,minZ,width,depth));
        if (mask != null) Destroy(mask);
        mask = next; MaskReady = true; MaskRevision++; baking = null;
        Debug.Log("CATAN_OCEAN_READY: mask=512 revision="+MaskRevision+" mobile="+Application.isMobilePlatform);
    }

    void OnEnable() { if(lastTiles!=null) Refresh(lastTiles,lastVertices); }

    void OnDisable()
    {
        if (baking != null) StopCoroutine(baking);
        baking = null; layoutKey = null;
        foreach (var renderer in hiddenSea) if (renderer != null) renderer.enabled = true;
        hiddenSea.Clear();
    }
    void OnDestroy() { if (material != null) Destroy(material); if (WakeMaterial!=null) Destroy(WakeMaterial); if (mask != null) Destroy(mask); }
}
