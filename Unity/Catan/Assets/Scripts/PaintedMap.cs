using System;
using System.Collections.Generic;
using UnityEngine;

// 每个棋盘共用一张地形图集。布局改变时才更新六邻格，静止帧无需遍历地图。
public sealed class PaintedMap : MonoBehaviour
{
    public const string Root = "PaintedMap/";
    const float HalfWidth = .034641016f, Row = .06f;
    static readonly string[] Regions = { "wood", "brick", "wool", "wheat", "ore", "desert", "gold", "lake", "sea", "fog" };
    static readonly Vector2Int[] Directions = {
        new Vector2Int(2,0), new Vector2Int(1,1), new Vector2Int(-1,1),
        new Vector2Int(-2,0), new Vector2Int(-1,-1), new Vector2Int(1,-1)
    };
    readonly Dictionary<Vector2Int, (Renderer renderer, int region)> cells = new Dictionary<Vector2Int, (Renderer, int)>();
    MaterialPropertyBlock block;
    Material ink;
    Material fluorescence;
    Mesh fluorescenceMesh;
    readonly Dictionary<string, MeshRenderer> glowSurfaces = new Dictionary<string, MeshRenderer>();
    void Awake() { block = new MaterialPropertyBlock(); }

    // 港口与渔场仍使用各自的轮廓和接岸锚点。共用印墨只覆盖有图案的部分。
    static readonly Dictionary<(Texture, bool), Material> CoastalMaterials = new Dictionary<(Texture, bool), Material>();
    public static void ApplyCoastalInk(GameObject marker, bool port)
    {
        foreach (var renderer in marker.GetComponentsInChildren<MeshRenderer>())
        {
            var slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
            {
                var texture = slots[i].mainTexture;
                if (!CoastalMaterials.TryGetValue((texture, port), out var material) || material == null)
                {
                    material = new Material(Resources.Load<Shader>(Root + "CoastalInk"));
                    material.mainTexture = texture;
                    material.SetFloat("_Coverage", texture == null ? 0 : 1);
                    material.renderQueue = port ? 3001 : 3000;
                    CoastalMaterials[(texture, port)] = material;
                }
                slots[i] = material;
            }
            renderer.sharedMaterials = slots;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    public static int Region(string terrain) => Array.IndexOf(Regions, terrain == "water" ? "sea" : terrain == "hidden" ? "fog" : terrain);

    public static GameObject Tile(string terrain, Vector3 center, Transform parent)
    {
        int region = Region(terrain);
        if (region < 0) return null;
        var map = parent.GetComponent<PaintedMap>() ?? parent.gameObject.AddComponent<PaintedMap>();
        if (map.ink == null)
        {
            map.ink = new Material(Resources.Load<Shader>(Root + "Terrain")) { name = "手绘棋盘" };
            map.ink.mainTexture = Resources.Load<Texture2D>(Root + "terrain-atlas");
            map.ink.SetTexture("_RoadEdge",Resources.Load<Texture2D>(CoastalDecals.Root+"road-edge"));
            map.ink.SetTexture("_RoadVerges",Resources.Load<Texture2D>(CoastalDecals.Root+"road-verges"));
            map.ink.SetTexture("_BeachEdge",Resources.Load<Texture2D>(CoastalDecals.Root+"beach-edge"));
            map.ink.SetTexture("_RobberContact",Resources.Load<Texture2D>("NeutralPieces/v001/robber-contact"));
        }
        var model = PlayerComponentAssets.Model(Root + "board-hex");
        var tile = Instantiate(model, center, model.transform.rotation, parent);
        var renderer = tile.GetComponentInChildren<MeshRenderer>();
        renderer.sharedMaterial = map.ink;
        var key = new Vector2Int(Mathf.RoundToInt(center.x / HalfWidth), Mathf.RoundToInt(center.z / Row));
        map.cells[key] = (renderer, region);
        map.enabled = true;
        if (region == 8) tile.AddComponent<OceanTileSurface>();
        return tile;
    }

    internal static readonly int RobberHex = Shader.PropertyToID("_RobberHex");
    internal static readonly int PirateHex = Shader.PropertyToID("_PirateHex");

    // z 标记目标有效，原点也能成为合法目标；边框不对应任何受掠夺的格子。
    internal static Vector4 OccupiedHex(Dictionary<string, Vector3> tiles, string id)
    {
        if (id == null || id == "frame" || !tiles.TryGetValue(id, out var center)) return Vector4.zero;
        return new Vector4(center.x, center.z, 1, 0);
    }
    internal void SetDangerHex(int property, Vector4 hex)
    {
        if (ink == null || ink.GetVector(property) == hex) return;
        ink.SetVector(property,hex);
        if (fluorescence != null && property == RobberHex) fluorescence.SetVector(property,hex);
        SetFluorescence(property==RobberHex?"强盗危险":"海盗危险",new Vector3(hex.x,0,hex.y),hex.z>.5f,false,true,
            property==PirateHex?TabletopOcean.WaterHeight+.0015f:EntityBoardAssets.BoardSurfaceHeight+.0005f);
    }

    internal void SetTileGlow(string id, bool owned, float goldAt, bool multiple = false)
    {
        var tile = transform.Find(id);
        if (tile == null) return;
        var center = tile.position;
        var key = new Vector2Int(Mathf.RoundToInt(center.x / HalfWidth), Mathf.RoundToInt(center.z / Row));
        if (!cells.TryGetValue(key, out var cell) || cell.renderer == null) return;
        cell.renderer.GetPropertyBlock(block);
        block.SetVector("_Glow", new Vector4(owned ? 1 : 0, goldAt, multiple ? 1 : 0, 0));
        cell.renderer.SetPropertyBlock(block);
        SetFluorescence(id,center,owned,multiple);
    }

    void SetFluorescence(string id, Vector3 center, bool owned, bool multiple, bool danger=false, float height=EntityBoardAssets.BoardSurfaceHeight+.0005f)
    {
        if (!glowSurfaces.TryGetValue(id,out var surface))
        {
            if (!owned) return;
            if (fluorescence == null)
            {
                fluorescence = new Material(Resources.Load<Shader>(Root+"TileFluorescence")) { name="地块荧光共享材质" };
                fluorescence.SetVector(RobberHex,ink.GetVector(RobberHex));
                fluorescenceMesh = new Mesh { name="地块荧光平面" };
                fluorescenceMesh.vertices = new[] { new Vector3(-.049f,0,-.049f),new Vector3(-.049f,0,.049f),new Vector3(.049f,0,.049f),new Vector3(.049f,0,-.049f) };
                fluorescenceMesh.triangles = new[] {0,1,2,0,2,3};
                fluorescenceMesh.RecalculateBounds();
                fluorescenceMesh.UploadMeshData(true);
            }
            var go = new GameObject((danger?"危险荧光 ":"产出荧光 ")+id);
            go.transform.SetParent(transform,false);
            go.transform.position = new Vector3(center.x,EntityBoardAssets.BoardSurfaceHeight+.0005f,center.z);
            go.AddComponent<MeshFilter>().sharedMesh=fluorescenceMesh;
            surface=go.AddComponent<MeshRenderer>();
            surface.sharedMaterial=fluorescence;
            surface.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            surface.receiveShadows=false;
            glowSurfaces[id]=surface;
        }
        surface.enabled=owned;
        surface.transform.position=new Vector3(center.x,height,center.z);
        block.Clear();
        block.SetVector("_Center",new Vector4(center.x,center.z,multiple?1:0,danger?1:0));
        surface.SetPropertyBlock(block);
    }

    int glowFrame=-1;
    internal void UpdateGlowClock()
    {
        if(ink==null||glowFrame==Time.frameCount)return;
        glowFrame=Time.frameCount;
        ink.SetFloat("_HarvestClock",Time.unscaledTime);
        if(fluorescence!=null)fluorescence.SetFloat("_FlowClock",Time.unscaledTime);
    }

    void LateUpdate()
    {
        foreach (var pair in cells)
        {
            var (renderer, region) = pair.Value;
            if (renderer == null) continue;
            Vector4 first = Vector4.zero, last = Vector4.zero;
            for (int i = 0; i < Directions.Length; i++)
            {
                int adjacent = cells.TryGetValue(pair.Key + Directions[i], out var cell) ? cell.region : -1;
                if (i < 4) first[i] = adjacent;
                else last[i - 4] = adjacent;
            }
            var center = renderer.bounds.center;
            renderer.GetPropertyBlock(block);
            block.SetVector("_Tile", new Vector4(center.x, center.z, region, 0));
            block.SetVector("_NearA", first);
            block.SetVector("_NearB", last);
            renderer.SetPropertyBlock(block);
        }
        enabled = false;
    }
    void OnDestroy() { if (ink != null) Destroy(ink); if(fluorescence!=null)Destroy(fluorescence); if(fluorescenceMesh!=null)Destroy(fluorescenceMesh); }
}
