using System.Collections.Generic;
using System.Linq;
using Catan.Core;
using PlayerView = Catan.Core.M4.PlayerView;
using UnityEngine;

public sealed class M4BoardRenderer : MonoBehaviour
{
    public Camera BoardCamera { get; private set; }
    public bool TopView { get; private set; }
    public float Zoom { get; private set; } = .255f;
    public static readonly Color[] SeatColors = { new Color(.85f,.24f,.16f), new Color(.23f,.56f,.78f), new Color(.94f,.74f,.3f), new Color(.79f,.79f,.72f) };
    private readonly Dictionary<string, Vector3> vertices = new Dictionary<string, Vector3>();
    private readonly Dictionary<string, Vector3> edges = new Dictionary<string, Vector3>();
    private readonly Dictionary<string, Vector3> tiles = new Dictionary<string, Vector3>();
    private readonly Dictionary<string, string> terrainTypes = new Dictionary<string, string>();
    private readonly Dictionary<string, Quaternion> rotations = new Dictionary<string, Quaternion>();
    private Transform pieces;
    private GameObject ghost;
    private readonly List<GameObject> mountains = new List<GameObject>();
    private readonly Dictionary<Color, Material> pieceMaterials = new Dictionary<Color, Material>();
    private readonly List<Material> terrainMaterials = new List<Material>();
    private readonly List<Mesh> terrainMeshes = new List<Mesh>();
    public static Color SeatColor(string id) => SeatColors[int.Parse(id.Substring(1)) - 1];
    public static Material Material(Color color, bool metallic = false)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.color = color; m.SetFloat("_Smoothness", .35f); m.SetFloat("_Metallic", metallic ? .45f : .05f); return m;
    }
    public void Initialize(PlayerView view)
    {
        Initialize(view.Board.Tiles, view.Board.Vertices, view.Board.Edges);
        Refresh(view);
    }
    // Geometry and expansion markers are public projected board data.
    public void Initialize(Tile[] boardTiles, Vertex[] boardVertices, Edge[] boardEdges)
    {
        BoardCamera = Camera.main;
        foreach (var v in boardVertices) vertices[v.Id] = new Vector3(v.X * Mathf.Sqrt(3) * .02f, .008f, -v.Y * .02f);
        foreach (var e in boardEdges)
        {
            var a = vertices[e.Vertices[0]]; var b = vertices[e.Vertices[1]];
            edges[e.Id] = (a+b)*.5f; rotations[e.Id] = Quaternion.LookRotation(b-a);
        }
        foreach (var tile in boardTiles)
        {
            var center = tile.Vertices.Select(v=>vertices[v]).Aggregate(Vector3.zero,(s,v)=>s+v)/6f;
            center.y = 0;
            tiles[tile.Id] = center;
            terrainTypes[tile.Id] = tile.Resource;
            GameObject obj;
            if (tile.Resource == "ore")
            {
                obj = Instantiate(Resources.Load<GameObject>("mountain-tile-v001"), center, Quaternion.identity, transform);
                mountains.Add(obj);
            }
            else
            {
                obj = new GameObject(tile.Id); obj.transform.SetParent(transform); obj.transform.position=center;
                var mesh=HexMesh();terrainMeshes.Add(mesh);obj.AddComponent<MeshFilter>().sharedMesh=mesh;
                var material=Material(TerrainColor(tile.Resource));terrainMaterials.Add(material);
                obj.AddComponent<MeshRenderer>().sharedMaterial=material;
            }
            obj.name = tile.Id;
        }
        var table = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        table.name="Felt table"; table.transform.SetParent(transform); table.transform.position=new Vector3(0,-.012f,0);
        table.transform.localScale=new Vector3(.65f,.01f,.65f); var felt=Material(new Color(.055f,.16f,.18f));terrainMaterials.Add(felt);table.GetComponent<Renderer>().sharedMaterial=felt;
        pieces = new GameObject("Committed pieces").transform; pieces.SetParent(transform);
        SetCamera(false);
    }
    public static Mesh HexMesh()
    {
        var verts = new List<Vector3>(); var tris = new List<int>();
        for(int i=0;i<6;i++)
        {
            float a=(30+i*60)*Mathf.Deg2Rad, b=(30+(i+1)*60)*Mathf.Deg2Rad;
            Vector3 p=new Vector3(Mathf.Cos(a)*.0395f,0,Mathf.Sin(a)*.0395f),q=new Vector3(Mathf.Cos(b)*.0395f,0,Mathf.Sin(b)*.0395f);
            int k=verts.Count; verts.Add(Vector3.up*.008f); verts.Add(q+Vector3.up*.008f); verts.Add(p+Vector3.up*.008f);
            verts.Add(p); verts.Add(p+Vector3.up*.008f); verts.Add(q+Vector3.up*.008f); verts.Add(q);
            tris.AddRange(new[]{k,k+1,k+2,k+3,k+4,k+5,k+3,k+5,k+6});
        }
        var mesh=new Mesh {name="Pointy hex 40mm estimate"}; mesh.SetVertices(verts); mesh.SetTriangles(tris,0); mesh.RecalculateNormals(); return mesh;
    }
    static Color TerrainColor(string r)
    {
        switch(r) { case "wood":return new Color(.16f,.36f,.25f); case "brick":return new Color(.62f,.28f,.18f); case "wool":return new Color(.49f,.62f,.3f); case "wheat":return new Color(.78f,.59f,.25f); default:return new Color(.68f,.58f,.41f); }
    }
    public void Refresh(PlayerView view)
    {
        Refresh(view.Board.Settlements, view.Board.Roads, view.Board.Cities, view.Board.RobberTileId);
        RenderExpansion(view.Board.Walls,view.Board.Knights,view.Board.Metropolises,view.MerchantTileId);
    }
    void RenderExpansion(PiecePlacement[] walls,Catan.Core.M4.Knight[] knights,Catan.Core.M4.Metropolis[] metropolises,string merchant)
    {
        foreach(var wall in walls)
        {
            var root = MarkerRoot("Wall "+wall.LocationId, Position(wall.LocationId));
            for(int i=0;i<4;i++)
            {
                float angle=i*90*Mathf.Deg2Rad;
                var part=Marker(PrimitiveType.Cube,root,new Vector3(Mathf.Cos(angle)*.012f,.004f,Mathf.Sin(angle)*.012f),new Vector3(.028f,.008f,.003f),SeatColor(wall.PlayerId));
                part.transform.localRotation=Quaternion.Euler(0,90-i*90,0);
            }
        }
        foreach(var k in knights)
        {
            var root=MarkerRoot("Knight L"+k.Level+" "+k.LocationId,Position(k.LocationId));
            Marker(PrimitiveType.Cylinder,root,new Vector3(0,.002f,0),new Vector3(.015f,.002f,.015f),SeatColor(k.PlayerId));
            for(int i=0;i<k.Level;i++)Marker(PrimitiveType.Cylinder,root,new Vector3(0,.006f+i*.004f,0),new Vector3(.010f,.0015f,.010f),SeatColor(k.PlayerId));
            Marker(PrimitiveType.Sphere,root,new Vector3(0,.010f+k.Level*.004f,0),new Vector3(.006f,.006f,.006f),k.Active?new Color(.96f,.88f,.48f):new Color(.12f,.14f,.17f));
        }
        foreach(var m in metropolises)
        {
            var root=MarkerRoot("Metropolis "+m.Track,Position(m.LocationId));
            var color=new[]{new Color(.93f,.67f,.2f),new Color(.26f,.51f,.92f),new Color(.26f,.75f,.39f)}[(int)m.Track];
            for(int i=0;i<3;i++)Marker(PrimitiveType.Cube,root,new Vector3((i-1)*.009f,.032f,0),new Vector3(.005f,.014f,.005f),color);
            Marker(PrimitiveType.Cube,root,new Vector3(0,.027f,0),new Vector3(.027f,.005f,.008f),color);
        }
        RefreshMerchant(merchant);
    }
    // Isolated visual sample arrangement. It never changes a game session or its PlayerView.
    public void ShowComponentSamples()
    {
        var nearby=vertices.OrderBy(p=>p.Value.sqrMagnitude).Take(8).Select(p=>p.Key).ToArray();
        var cities=Enumerable.Range(0,3).Select(i=>new PiecePlacement{PlayerId="P"+(i+1),LocationId=nearby[i*2]}).ToArray();
        Refresh(new PiecePlacement[0],new PiecePlacement[0],cities,null);
        var knights=Enumerable.Range(0,3).Select(i=>new Catan.Core.M4.Knight{PlayerId="P"+(i+1),LocationId=nearby[i*2+1],Level=i+1,Active=i!=1}).ToArray();
        var metros=Enumerable.Range(0,3).Select(i=>new Catan.Core.M4.Metropolis{PlayerId=cities[i].PlayerId,LocationId=cities[i].LocationId,Track=(Catan.Core.M4.ImprovementTrack)i}).ToArray();
        RenderExpansion(cities,knights,metros,tiles.OrderBy(t=>t.Value.sqrMagnitude).First().Key);
    }
    Transform MarkerRoot(string label,Vector3 position)
    {
        var root=new GameObject(label).transform;root.SetParent(pieces);root.position=position;return root;
    }
    GameObject Marker(PrimitiveType type,Transform root,Vector3 position,Vector3 size,Color color)
    {
        var obj=GameObject.CreatePrimitive(type);Destroy(obj.GetComponent<Collider>());obj.transform.SetParent(root,false);obj.transform.localPosition=position;obj.transform.localScale=size;
        if(!pieceMaterials.TryGetValue(color,out var material)){material=Material(color);pieceMaterials.Add(color,material);}obj.GetComponent<Renderer>().sharedMaterial=material;return obj;
    }
    void RefreshMerchant(string merchant)
    {
        // Added by the progress-card view projection; no authoritative state enters rendering.
        if(!string.IsNullOrEmpty(merchant))
        {
            var root=MarkerRoot("Merchant",Position(merchant)+new Vector3(-.013f,0,0));
            Marker(PrimitiveType.Capsule,root,new Vector3(0,.019f,0),new Vector3(.008f,.016f,.008f),new Color(.8f,.4f,.85f));
        }
    }
    public void Refresh(PiecePlacement[] settlements, PiecePlacement[] roads, PiecePlacement[] cities, string robberTileId)
    {
        foreach(Transform t in pieces) Destroy(t.gameObject);
        foreach(var p in settlements) Piece(false,p.LocationId,SeatColor(p.PlayerId),pieces);
        foreach(var p in roads) Piece(true,p.LocationId,SeatColor(p.PlayerId),pieces);
        foreach(var p in cities)
        {
            var city=Piece(false,p.LocationId,SeatColor(p.PlayerId),pieces);
            city.name="City "+p.LocationId;
            city.transform.localScale=new Vector3(.02f,.023f,.015f);
            city.transform.position=Position(p.LocationId)+Vector3.up*.012f;
        }
        if(!string.IsNullOrEmpty(robberTileId)) Robber(robberTileId,new Color(.08f,.075f,.09f),pieces);
    }
    GameObject Robber(string tileId, Color color, Transform parent)
    {
        var marker=GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Destroy(marker.GetComponent<Collider>());marker.name="Robber "+tileId;
        marker.transform.SetParent(parent);marker.transform.position=Position(tileId)+new Vector3(.012f,.035f,0);
        marker.transform.localScale=new Vector3(.009f,.018f,.009f);
        if(!pieceMaterials.TryGetValue(color,out var material)){material=Material(color);pieceMaterials.Add(color,material);}
        marker.GetComponent<Renderer>().sharedMaterial=material;
        return marker;
    }
    GameObject Piece(bool road,string id,Color color,Transform parent)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(go.GetComponent<Collider>()); go.transform.SetParent(parent);
        go.name=id; go.transform.position=Position(id)+Vector3.up*(road?.0025f:.006f);
        go.transform.localScale=road?new Vector3(.005f,.005f,.026f):new Vector3(.012f,.012f,.012f);
        if(road)go.transform.rotation=rotations[id];
        if(!pieceMaterials.TryGetValue(color,out var material)) {material=Material(color);pieceMaterials.Add(color,material);}
        go.GetComponent<Renderer>().sharedMaterial=material;
        if(!road)
        {
            var roof=GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(roof.GetComponent<Collider>());
            roof.transform.SetParent(go.transform,false); roof.transform.localPosition=new Vector3(0,.5f,0);
            roof.transform.localRotation=Quaternion.Euler(0,0,45); roof.transform.localScale=new Vector3(.7f,.7f,.95f);
            roof.GetComponent<Renderer>().sharedMaterial=go.GetComponent<Renderer>().sharedMaterial;
        }
        return go;
    }
    public Vector3 Position(string id) => id.StartsWith("V")?vertices[id]:id.StartsWith("E")?edges[id]:tiles[id];
    public string TerrainResource(string tileId) => terrainTypes[tileId];
    public Vector2 ScreenPoint(string id)
    {
        var p=BoardCamera.WorldToScreenPoint(Position(id)+Vector3.up*.005f); return new Vector2(p.x,Screen.height-p.y);
    }
    public void Preview(string id, bool road, bool legal)
    {
        ClearPreview(); if(string.IsNullOrEmpty(id))return;
        ghost=Piece(road,id,legal?new Color(.3f,1f,.75f):new Color(1f,.22f,.2f),transform);
    }
    public void PreviewRobber(string id,bool legal)
    {
        ClearPreview();if(string.IsNullOrEmpty(id))return;
        ghost=Robber(id,legal?new Color(.3f,1f,.75f):new Color(1f,.22f,.2f),transform);
    }
    public void ClearPreview() { if(ghost!=null)Destroy(ghost); ghost=null; }
    public void SetCamera(bool top)
    {
        TopView=top; BoardCamera.orthographic=true; BoardCamera.orthographicSize=Zoom;
        BoardCamera.transform.position=top?new Vector3(0,.65f,-.001f):new Vector3(0,.48f,-.4f);
        BoardCamera.transform.LookAt(new Vector3(0,.005f,0));
        BoardCamera.rect=new Rect(.0f,.10f,.74f,.80f);
    }
    public void ChangeZoom(float delta) { Zoom=Mathf.Clamp(Zoom+delta,.19f,.32f); SetCamera(TopView); }
    public void ShowTerrain(bool show) { foreach(var m in mountains) m.SetActive(show); }
    void OnDestroy() {foreach(var m in pieceMaterials.Values)Destroy(m);foreach(var m in terrainMaterials)Destroy(m);foreach(var m in terrainMeshes)Destroy(m);}
}
