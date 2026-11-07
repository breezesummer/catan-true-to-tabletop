using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core;
using UnityEngine;

// 仅接收 PlayerView 已公开的地图、港口与剧本奖励；不读取权威状态或隐藏手牌。
public sealed partial class TabletopReferenceMarkers : MonoBehaviour
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
        public HarborSign Sign;
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
    // 使用实际网格三角形保留港口凹口；包围盒会把渔港空闲的另一臂误判为占用。
    public Vector3[][] PublicPortFootprints {get;private set;}=Array.Empty<Vector3[]>();
    public int PortLayoutRevision {get;private set;}
    string portLayoutKey;
    string oceanLayoutKey;
    TabletopScene outlinedTable;
    int outlineRevision = -1;
    readonly List<Vector3> oceanOutline=new List<Vector3>();
    void RefreshPortFootprints()
    {
        string key=string.Join("|",ports.Values.Concat(gifts.Values).Where(p=>p.Object!=null).Select(p=>p.Resource+":"+p.Object.transform.localToWorldMatrix.ToString("F6")));
        if(key==portLayoutKey)return;
        portLayoutKey=key;PortLayoutRevision++;
        PublicPortFootprints=ports.Values.Concat(gifts.Values).Where(p=>p.Object!=null)
        .SelectMany(p=>CoastalDecals.Footprint(p.Object.transform)).ToArray();
    }
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
        display.oceanRoot = display.Group("地图外围公开装饰海格");
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
        RefreshOceanTiles();
        if (table != null && table.isActiveAndEnabled) table.Ocean.Refresh(tiles, vertices);
        RefreshPorts(ports, portRoot, publicPorts, "港口 ");
        RefreshPorts(gifts, giftRoot, publicGiftPorts, "港礼 ");
        RefreshPortFootprints();
        if(table!=null && (outlinedTable!=table || outlineRevision!=PortLayoutRevision))
        {
            table.SetPresentationOutline(PublicPortFootprints.SelectMany(points=>points));
            outlinedTable=table; outlineRevision=PortLayoutRevision;
        }
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
            // 桥头略搭上岸边，海侧平台和水下桩随公开海岸边一起转动。
            var bottom = midpoint + normal * (CoastalDecals.ShoreEnd - CoastalDecals.DockInset);
            bottom.y = EntityBoardAssets.BoardSurfaceHeight;
            item.Object.transform.SetPositionAndRotation(bottom, Quaternion.LookRotation(-normal, Vector3.up));
            item.Sign?.Place();
            wanted.Add(port.Id);
        }
        Prune(existing, wanted);
    }
    void RefreshOceanTiles()
    {
        string layout=string.Join("|",tiles.Select(t=>t.Id+":"+string.Join(",",t.Vertices)));
        if(layout==oceanLayoutKey)return;
        oceanLayoutKey=layout;
        var wanted=new HashSet<string>(StringComparer.Ordinal);
        var displayOutline=oceanOutline;displayOutline.Clear();
        // 所有公开板块的外边界补一圈；已有海格和迷雾同样属于地图，不能只按港口补格。
        foreach(var edge in edges.Values)
        {
            var endpoints=edge.Vertices;
            if(!TryEndpoints(endpoints,out var a,out var b))continue;
            var adjacent=tiles.Where(t=>endpoints.All(t.Vertices.Contains)).ToArray();
            if(adjacent.Length!=1)continue;
            var midpoint=(a+b)/2;var normal=midpoint-TileCenter(adjacent[0]);normal.y=0;normal.Normalize();
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
                obj.name="外围公开装饰海格 "+key;
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
    public Vector3 KnightLandInset(string vertex)
    {
        if(tiles==null||!vertices.TryGetValue(vertex,out var point))return Vector3.zero;
        Vector3 inward=Vector3.zero;int count=0;
        foreach(var tile in tiles)
        {
            if(tile.Resource=="sea"||tile.Resource=="water"||tile.Resource=="fog"||tile.Resource=="hidden"||
                tile.Vertices==null||Array.IndexOf(tile.Vertices,vertex)<0)continue;
            if(++count==3)return Vector3.zero;
            inward+=TileCenter(tile)-point;
        }
        inward.y=0;return inward.normalized*.0045f;
    }

    // 仅使用公开的港口与地形。泊位始终在所属六角内，同一局面重连后保持一致。
    public (Vector3 shift, Quaternion facing) PiratePose(Vector3 center)
    {
        Vector3 openWater=Vector3.zero;
        foreach(var tile in tiles)
        {
            if(tile.Resource=="sea" || tile.Resource=="fog" || tile.Resource=="hidden")continue;
            var delta=center-TileCenter(tile);delta.y=0;
            if(delta.sqrMagnitude<.006f)openWater+=delta;
        }
        int direction=openWater.sqrMagnitude>.000001f?FantasyPieceFrames.Heading(openWater):3;
        var facing=Quaternion.Euler(0,60*direction,0);
        var standard=Vector3.right*.012f;
        float Clearance(Vector3 candidate)
        {
            float nearest=.04f*.04f;
            foreach(var group in new[]{ports,gifts})
            foreach(var marker in group.Values)
            {
                if(marker.Sign==null)continue;
                // 将签面中心投影回默认 66° 视角下的水面。
                var anchor=marker.Object.transform.TransformPoint(HarborSign.LocalAnchor);
                var sign=new Vector2(anchor.x+HarborSign.BoardOffset.x,
                    anchor.z+HarborSign.BoardOffset.y+(anchor.y-TabletopOcean.WaterHeight)/Mathf.Tan(TabletopCameraModel.DefaultAngle*Mathf.Deg2Rad));
                nearest=Mathf.Min(nearest,(sign-new Vector2(center.x+candidate.x,center.z+candidate.z)).sqrMagnitude);
            }
            return nearest;
        }
        var shift=standard;
        float best=Clearance(shift);
        if(best<.026f*.026f)
            for(int i=0;i<6;i++)
            {
                var offset=Quaternion.Euler(0,60*i,0)*Vector3.forward*.019f;
                float distance=Clearance(offset);
                if(distance>best){best=distance;shift=offset;}
            }
        return (shift-standard,facing);
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
        var item = new Marker { Object = instance, Resource = resource, Sign=instance.GetComponentInChildren<HarborSign>() };
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
    void LateUpdate()
    {
        PositionAwards();
    }
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
        AlignPortSigns(camera);
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
                if(filter.transform==pair.Value.Sign?.transform && !pair.Value.Sign.Face.enabled)continue;
                if(TryHitMesh(ray,filter,out float candidate)&&candidate<distance)
                {distance=candidate;nearest=pair.Key;}
            }
        }
        return nearest;
    }
    static readonly Dictionary<Mesh, (Vector3[] vertices, int[] triangles)> pickMeshes = new Dictionary<Mesh, (Vector3[], int[])>();
    static bool TryHitMesh(Ray worldRay,MeshFilter filter,out float distance)
    {
        distance=float.PositiveInfinity;
        if(filter.name=="栈桥水影"||filter.sharedMesh==null)return false;
        var mesh=CoastalDecals.HitMesh(filter.sharedMesh);
        if(!mesh.isReadable)return false;
        var origin=filter.transform.InverseTransformPoint(worldRay.origin);
        var direction=filter.transform.InverseTransformVector(worldRay.direction);
        if (!mesh.bounds.IntersectRay(new Ray(origin,direction))) return false;
        if (!pickMeshes.TryGetValue(mesh,out var data))
            pickMeshes[mesh]=data=(mesh.vertices,mesh.triangles);
        var positions=data.vertices;var triangles=data.triangles;
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
        var renderers = instance.GetComponentsInChildren<MeshRenderer>();
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
