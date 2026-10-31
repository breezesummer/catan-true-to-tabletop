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
        public float InlandReach;
    }
    readonly Dictionary<string, Marker> ports = new Dictionary<string, Marker>();
    readonly Dictionary<string, Marker> gifts = new Dictionary<string, Marker>();
    readonly Dictionary<string, Marker> bonuses = new Dictionary<string, Marker>();
    readonly Dictionary<string, Marker> awards = new Dictionary<string, Marker>();
    readonly Dictionary<string, GameObject> oceanTiles = new Dictionary<string, GameObject>();
    Dictionary<string, Vector3> vertices;
    Dictionary<string, Edge> edges;
    Tile[] tiles;
    Transform portRoot, giftRoot, bonusRoot, awardRoot, oceanRoot;
    TabletopScene table;
    string observerSeat;
    string[] playerIds = Array.Empty<string>();
    Vector3 boardCenter;
    float minimumZ;

    public Vector3[] PublicPortPositions => ports.Values.Concat(gifts.Values).Where(p=>p.Object!=null).Select(p=>p.Object.transform.position).ToArray();
    public int PlacedPortCount => ports.Count;
    public int GiftPortCount => gifts.Count;
    public int UnclaimedVictoryMarkerCount => bonuses.Count;
    public int AwardedVictoryMarkerCount => awards.Count;
    public int SupplementalOceanTileCount => oceanTiles.Count;

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
        display.oceanRoot = display.Group("港口外侧公开装饰海格 v010");
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
        RefreshOceanTiles((publicPorts??Array.Empty<PortDisplay>()).Concat(publicGiftPorts??Array.Empty<PortDisplay>()));
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
            // 将原照片纸板的沙岸实际轮廓贴到海岸边，留下 0.35 mm 的实体板缝。
            // 不凭纸板中心猜测距离，避免不同港口照片的外形产生可见空隙。
            var bottom = midpoint + normal * (item.InlandReach+.00035f);
            // 渔港底面高出地形 0.5 mm、板厚 1.5 mm；港口再留 0.1 mm 间隙，固定显示在上层。
            bottom.y = FishingTabletopAssets.GroundBottom+.0015f+.0001f;
            item.Object.transform.position = bottom;
            // 导入纸板的沙岸在 local +Z，向海的凸柄在 local -Z。
            item.Object.transform.rotation = Quaternion.LookRotation(-normal, Vector3.up);
            wanted.Add(port.Id);
        }
        Prune(existing, wanted);
    }
    void RefreshOceanTiles(IEnumerable<PortDisplay> publicPorts)
    {
        var wanted=new HashSet<string>(StringComparer.Ordinal);
        var displayOutline=new List<Vector3>();
        foreach(var port in publicPorts)
        {
            var endpoints=port.Vertices;
            if((endpoints==null||endpoints.Length!=2)&&!string.IsNullOrEmpty(port.EdgeId)&&edges.TryGetValue(port.EdgeId,out var edge))endpoints=edge.Vertices;
            if(!TryEndpoints(endpoints,out var a,out var b))continue;
            var midpoint=(a+b)/2;var normal=CoastNormal(endpoints,midpoint,b-a);
            // 完整相邻六边形中心距海岸半个格宽，边长来自已公开的海岸几何。
            float side=Vector3.Distance(a,b);
            var seaCenter=midpoint+normal*(side*Mathf.Sqrt(3)/2);seaCenter.y=0;
            bool occupied=(tiles??Array.Empty<Tile>()).Any(t=>t.Vertices!=null&&t.Vertices.All(vertices.ContainsKey)&&
                (TileCenter(t)-seaCenter).sqrMagnitude<.000001f);
            // 已有海格、陆格或未揭示占位时均不叠加，也不猜测隐藏地形。
            if(occupied)continue;
            string key=Mathf.RoundToInt(seaCenter.x*100000)+":"+Mathf.RoundToInt(seaCenter.z*100000);
            wanted.Add(key);
            if(!oceanTiles.ContainsKey(key))
            {
                var obj=EntityBoardAssets.Terrain("sea",seaCenter,oceanRoot);
                if(obj==null)continue;
                obj.name="港口公开装饰海格 "+key;
                // 原实体海板按边长 40 mm 制作，保持公开拓扑尺度；没有 collider 或规则 ID。
                obj.transform.localScale=Vector3.one*(side/.04f);
                oceanTiles.Add(key,obj);
            }
            for(int i=0;i<6;i++)
            {
                float angle=(30+i*60)*Mathf.Deg2Rad;
                displayOutline.Add(seaCenter+new Vector3(Mathf.Cos(angle)*side,EntityBoardAssets.BoardSurfaceHeight,Mathf.Sin(angle)*side));
            }
        }
        foreach(var key in oceanTiles.Keys.Where(key=>!wanted.Contains(key)).ToArray())
        {
            var obj=oceanTiles[key];obj.SetActive(false);Destroy(obj);oceanTiles.Remove(key);
        }
        if(table!=null)table.SetPresentationOutline(displayOutline);
    }
    Vector3 TileCenter(Tile tile)
    {
        var point=tile.Vertices.Select(id=>vertices[id]).Aggregate(Vector3.zero,(sum,v)=>sum+v)/tile.Vertices.Length;
        point.y=0;return point;
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
        float inlandReach=0;
        foreach(var filter in instance.GetComponentsInChildren<MeshFilter>())
        {
            if(filter.sharedMesh==null)continue;
            var bounds=filter.sharedMesh.bounds;
            for(int i=0;i<8;i++)
            {
                var local=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                inlandReach=Mathf.Max(inlandReach,instance.transform.InverseTransformPoint(filter.transform.TransformPoint(local)).z);
            }
        }
        var item = new Marker { Object = instance, Resource = resource, InlandReach=inlandReach };
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
        foreach (var item in awards.Values)
        {
            // 对手桌边筹码由右上角公开计分 HUD 代替；地图上的公开奖励标记仍然保留。
            item.Object.SetActive(table==null||item.PlayerId==observerSeat);
            Vector3 position;
            if (table != null && table.isActiveAndEnabled)
            {
                TabletopScene.SetHudLayer(item.Object.transform);
                var anchor = new Vector2(128,700);
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
    // 输入为左上原点的实际屏幕像素，仅命中已摆放的公开港口；赠送中的港口不能提前交易。
    public string PickPort(Vector2 screenMouse,Camera camera)
    {
        if(camera==null)return null;
        var logical=new Vector2(screenMouse.x*1440/Screen.width,screenMouse.y*900/Screen.height);
        var ray=camera.ScreenPointToRay(new Vector3(screenMouse.x,Screen.height-screenMouse.y,0));
        string nearest=null;float distance=float.PositiveInfinity;
        foreach(var pair in ports)
        {
            if(pair.Value.Object==null||!pair.Value.Object.activeInHierarchy)continue;
            var rect=PortScreenRect(pair.Key,camera);
            if(!rect.Contains(logical))continue;
            // 投影包围盒只用于粗筛。斜向海岸的包围盒会包含岸边道路/船，必须命中真实纸板轮廓。
            foreach(var filter in pair.Value.Object.GetComponentsInChildren<MeshFilter>())
            {
                if(TryHitMesh(ray,filter,out float candidate)&&candidate<distance)
                {distance=candidate;nearest=pair.Key;}
            }
        }
        return nearest;
    }
    static bool TryHitMesh(Ray worldRay,MeshFilter filter,out float distance)
    {
        distance=float.PositiveInfinity;
        var mesh=filter.sharedMesh;
        if(mesh==null||!mesh.isReadable)return false;
        var origin=filter.transform.InverseTransformPoint(worldRay.origin);
        var direction=filter.transform.InverseTransformVector(worldRay.direction);
        var positions=mesh.vertices;var triangles=mesh.triangles;
        for(int i=0;i+2<triangles.Length;i+=3)
        {
            var a=positions[triangles[i]];var edge1=positions[triangles[i+1]]-a;var edge2=positions[triangles[i+2]]-a;
            var cross=Vector3.Cross(direction,edge2);float determinant=Vector3.Dot(edge1,cross);
            if(Mathf.Abs(determinant)<.00000000001f)continue;
            float inverse=1/determinant;var fromVertex=origin-a;
            float u=Vector3.Dot(fromVertex,cross)*inverse;
            if(u<-.00001f||u>1.00001f)continue;
            var secondCross=Vector3.Cross(fromVertex,edge1);
            float v=Vector3.Dot(direction,secondCross)*inverse;
            if(v<-.00001f||u+v>1.00001f)continue;
            // 方向不归一化，保留世界射线参数；缩放后的模型也能按同一世界距离比较前后。
            float candidate=Vector3.Dot(edge2,secondCross)*inverse;
            if(candidate>=0&&candidate<distance)distance=candidate;
        }
        return !float.IsPositiveInfinity(distance);
    }
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
