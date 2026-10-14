using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core;
using UnityEngine;

// 仅接收 PlayerView 已公开的地图、港口与剧本奖励；不读取权威状态或隐藏手牌。
public sealed class TabletopReferenceMarkers : MonoBehaviour
{
    public sealed class PortDisplay
    {
        public string Id, EdgeId;
        public string[] Vertices;
        public Resource? Resource;
    }
    public sealed class ScoreDisplay
    {
        public string PlayerId;
        public int Points;
    }
    sealed class Marker
    {
        public GameObject Object;
        public string Resource, PlayerId;
        public int Ordinal;
    }
    readonly Dictionary<string, Marker> ports = new Dictionary<string, Marker>();
    readonly Dictionary<string, Marker> gifts = new Dictionary<string, Marker>();
    readonly Dictionary<string, Marker> bonuses = new Dictionary<string, Marker>();
    readonly Dictionary<string, Marker> awards = new Dictionary<string, Marker>();
    Dictionary<string, Vector3> vertices;
    Dictionary<string, Edge> edges;
    Tile[] tiles;
    Transform portRoot, giftRoot, bonusRoot, awardRoot;
    TabletopScene table;
    string observerSeat;
    string[] playerIds = Array.Empty<string>();
    Vector3 boardCenter;
    float minimumZ;

    public int PlacedPortCount => ports.Count;
    public int GiftPortCount => gifts.Count;
    public int UnclaimedVictoryMarkerCount => bonuses.Count;
    public int AwardedVictoryMarkerCount => awards.Count;

    public static TabletopReferenceMarkers Create(Transform parent, Tile[] publicTiles,
        Dictionary<string, Vector3> publicVertexPositions, Edge[] publicEdges)
    {
        var root = new GameObject("实体港口与卡坦分标记 v005");
        root.transform.SetParent(parent, false);
        var display = root.AddComponent<TabletopReferenceMarkers>();
        display.vertices = publicVertexPositions;
        display.edges = publicEdges.ToDictionary(e => e.Id, StringComparer.Ordinal);
        display.tiles = publicTiles;
        display.boardCenter = new Vector3((publicVertexPositions.Values.Min(v => v.x) + publicVertexPositions.Values.Max(v => v.x)) / 2,
            0, (publicVertexPositions.Values.Min(v => v.z) + publicVertexPositions.Values.Max(v => v.z)) / 2);
        display.minimumZ = publicVertexPositions.Values.Min(v => v.z);
        display.portRoot = display.Group("港口");
        display.giftRoot = display.Group("赠送港口");
        display.bonusRoot = display.Group("航路奖励");
        display.awardRoot = display.Group("公开奖励");
        return display;
    }
    Transform Group(string name)
    {
        var group = new GameObject(name).transform;
        group.SetParent(transform, false);
        return group;
    }
    public void Refresh(Tile[] publicTiles, PortDisplay[] publicPorts, PortDisplay[] publicGiftPorts = null,
        string[] publicBonusEdgeIds = null, ScoreDisplay[] publicScores = null, string observerSeat = null,
        TabletopScene table = null)
    {
        if (publicTiles != null) tiles = publicTiles;
        this.table = table;
        this.observerSeat = observerSeat;
        RefreshPorts(ports, portRoot, publicPorts, "港口 ");
        RefreshPorts(gifts, giftRoot, publicGiftPorts, "港礼 ");
        var wantedBonuses = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in publicBonusEdgeIds ?? Array.Empty<string>())
        {
            if (!edges.TryGetValue(id, out var edge) || !TryEndpoints(edge.Vertices, out var a, out var b)) continue;
            var item = Put(bonuses, id, EntityBoardAssets.VictoryMarkerResource, "航路奖励 " + id, bonusRoot);
            if (item == null) continue;
            item.Object.transform.position = (a + b) / 2 + Vector3.up * .0001f;
            item.Object.transform.rotation = Quaternion.Euler(0, 180, 0);
            wantedBonuses.Add(id);
        }
        Prune(bonuses, wantedBonuses);
        var scores = publicScores ?? Array.Empty<ScoreDisplay>();
        playerIds = scores.Select(p => p.PlayerId).Distinct().OrderBy(id => id, StringComparer.Ordinal).ToArray();
        var wantedAwards = new HashSet<string>(StringComparer.Ordinal);
        foreach (var score in scores)
            for (int i = 0; i < Math.Max(0, score.Points); i++)
            {
                string key = score.PlayerId + ":" + i;
                var item = Put(awards, key, EntityBoardAssets.VictoryMarkerResource, "卡坦分 " + score.PlayerId + " " + i, awardRoot);
                if (item == null) continue;
                item.PlayerId = score.PlayerId;
                item.Ordinal = i;
                wantedAwards.Add(key);
            }
        Prune(awards, wantedAwards);
        PositionAwards();
    }
    void RefreshPorts(Dictionary<string, Marker> existing, Transform parent, PortDisplay[] publicPorts, string name)
    {
        var wanted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var port in publicPorts ?? Array.Empty<PortDisplay>())
        {
            string[] endpoints = port.Vertices;
            if ((endpoints == null || endpoints.Length != 2) && !string.IsNullOrEmpty(port.EdgeId) && edges.TryGetValue(port.EdgeId, out var edge))
                endpoints = edge.Vertices;
            if (string.IsNullOrEmpty(port.Id) || !TryEndpoints(endpoints, out var a, out var b)) continue;
            var item = Put(existing, port.Id, EntityBoardAssets.PortResource(port.Resource), name + port.Id, parent);
            if (item == null) continue;
            Vector3 midpoint = (a + b) / 2, normal = CoastNormal(endpoints, midpoint, b - a);
            // 真实纸板中心置于海岸外侧，两个建筑交点与中间的路船仍保持可选取。
            var bottom = midpoint + normal * .030f;
            bool seaSurface = (tiles ?? Array.Empty<Tile>()).Any(t => t.Vertices != null && endpoints.All(t.Vertices.Contains) &&
                (t.Resource == "sea" || t.Resource == "water"));
            // 有海格时贴 8 mm 板面；外海框顶面为 -1 mm，纸板不能悬在半空。
            bottom.y = (seaSurface ? EntityBoardAssets.BoardSurfaceHeight : -.001f) + .0001f;
            item.Object.transform.position = bottom;
            // 导入纸板的沙岸在 local +Z，向海的凸柄在 local -Z。
            item.Object.transform.rotation = Quaternion.LookRotation(-normal, Vector3.up);
            wanted.Add(port.Id);
        }
        Prune(existing, wanted);
    }
    bool TryEndpoints(string[] ids, out Vector3 a, out Vector3 b)
    {
        a = b = Vector3.zero;
        return ids != null && ids.Length == 2 && vertices.TryGetValue(ids[0], out a) && vertices.TryGetValue(ids[1], out b);
    }
    Vector3 CoastNormal(string[] endpoints, Vector3 midpoint, Vector3 edgeDirection)
    {
        Vector3 normal = Vector3.Cross(Vector3.up, edgeDirection).normalized;
        var adjacent = (tiles ?? Array.Empty<Tile>()).Where(t => t.Vertices != null && endpoints.All(t.Vertices.Contains)).ToArray();
        var land = adjacent.FirstOrDefault(t => t.Resource != "sea" && t.Resource != "water" && t.Resource != "fog" && t.Resource != "hidden");
        Vector3 towardsSea;
        if (land != null)
        {
            var landCenter = land.Vertices.Select(id => vertices[id]).Aggregate(Vector3.zero, (sum, v) => sum + v) / land.Vertices.Length;
            towardsSea = midpoint - landCenter;
        }
        else
        {
            // 公开海格可确定赠送港口朝向；未知格只作为几何占位，不猜测隐藏地形。
            var sea = adjacent.FirstOrDefault(t => t.Resource == "sea" || t.Resource == "water");
            towardsSea = sea == null ? midpoint - boardCenter :
                sea.Vertices.Select(id => vertices[id]).Aggregate(Vector3.zero, (sum, v) => sum + v) / sea.Vertices.Length - midpoint;
        }
        if (Vector3.Dot(normal, towardsSea) < 0) normal = -normal;
        return normal;
    }
    static Marker Put(Dictionary<string, Marker> collection, string key, string resource, string name, Transform parent)
    {
        if (collection.TryGetValue(key, out var known))
        {
            if (known.Resource == resource && known.Object != null) return known;
            Remove(known);
            collection.Remove(key);
        }
        var instance = EntityBoardAssets.ReferenceMarker(resource, name, Vector3.zero, Quaternion.identity, parent);
        if (instance == null) return null;
        var item = new Marker { Object = instance, Resource = resource };
        collection.Add(key, item);
        return item;
    }
    static void Prune(Dictionary<string, Marker> collection, HashSet<string> wanted)
    {
        foreach (var key in collection.Keys.Where(key => !wanted.Contains(key)).ToArray())
        {
            Remove(collection[key]);
            collection.Remove(key);
        }
    }
    static void Remove(Marker item)
    {
        if (item.Object == null) return;
        item.Object.SetActive(false);
        Destroy(item.Object);
    }
    void LateUpdate() => PositionAwards();
    void PositionAwards()
    {
        var opponents = TabletopUI.OpponentOrder(observerSeat, playerIds);
        foreach (var item in awards.Values)
        {
            Vector3 position;
            if (table != null && table.isActiveAndEnabled)
            {
                int index = Array.IndexOf(opponents, item.PlayerId);
                int place = opponents.Length == 2 ? (index == 0 ? 0 : 2) : index;
                var anchor = item.PlayerId == observerSeat ? new Vector2(306, 656) :
                    place == 1 ? new Vector2(994, 105) : place == 2 ? new Vector2(1221, 432) : new Vector2(219, 432);
                position = table.World(anchor) + Vector3.up * .00015f;
            }
            else
            {
                int index = Array.IndexOf(playerIds, item.PlayerId);
                position = new Vector3(boardCenter.x + (index - (playerIds.Length - 1) / 2f) * .036f,
                    EntityBoardAssets.BoardSurfaceHeight + .00015f, minimumZ - .044f);
            }
            // 一枚实体分筹码对应一项公开奖励分；多枚轻微错开堆叠在玩家前方。
            item.Object.transform.position = position + new Vector3(item.Ordinal % 3 * .0013f, item.Ordinal * .0016f, item.Ordinal % 2 * .0011f);
            item.Object.transform.rotation = Quaternion.Euler(0, 180, 0);
        }
    }
    public bool HasPort(string id) => ports.ContainsKey(id);
    public bool HasGiftPort(string id) => gifts.ContainsKey(id);
    public bool HasBonusMarker(string edgeId) => bonuses.ContainsKey(edgeId);
    public GameObject PortObject(string id)
    {
        if (ports.TryGetValue(id, out var placed)) return placed.Object;
        return gifts.TryGetValue(id, out var gift) ? gift.Object : null;
    }
    public Vector3 PortPosition(string id) => PortObject(id) == null ? Vector3.zero : PortObject(id).transform.position;
    public Rect PortScreenRect(string id, Camera camera)
    {
        var instance = PortObject(id);
        if (instance == null || camera == null) return Rect.zero;
        var renderers = instance.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return Rect.zero;
        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        var projected = new Vector2[8];
        for (int i = 0; i < projected.Length; i++)
        {
            var point = camera.WorldToScreenPoint(bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
            projected[i] = new Vector2(point.x * 1440 / Screen.width, (Screen.height - point.y) * 900 / Screen.height);
        }
        return Rect.MinMaxRect(projected.Min(p => p.x) - 2, projected.Min(p => p.y) - 2,
            projected.Max(p => p.x) + 2, projected.Max(p => p.y) + 2);
    }
}
