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
            map.ink.SetTexture("_Gravel",Resources.Load<Texture2D>(CoastalDecals.Root+"gravel"));
            map.ink.SetTexture("_Sand",Resources.Load<Texture2D>(CoastalDecals.Root+"sand"));
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
            block.SetVector("_Tile", new Vector4(center.x, center.z, region, 0));
            block.SetVector("_NearA", first);
            block.SetVector("_NearB", last);
            renderer.SetPropertyBlock(block);
        }
        enabled = false;
    }
    void OnDestroy() { if (ink != null) Destroy(ink); }
}
