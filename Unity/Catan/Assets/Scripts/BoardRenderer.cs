using System.Collections.Generic;
using System.Linq;
using Catan.Core;
using UnityEngine;

public sealed class BoardRenderer : MonoBehaviour
{
    public Camera BoardCamera { get; private set; }
    public bool TopView { get; private set; }
    public float Zoom { get; private set; } = .255f;
    public static readonly Color[] SeatColors = { new Color(.85f,.24f,.16f), new Color(.23f,.56f,.78f), new Color(.94f,.74f,.3f), new Color(.79f,.79f,.72f) };
    private readonly Dictionary<string, Vector3> vertices = new Dictionary<string, Vector3>();
    private readonly Dictionary<string, Vector3> edges = new Dictionary<string, Vector3>();
    private readonly Dictionary<string, Quaternion> rotations = new Dictionary<string, Quaternion>();
    private Transform pieces;
    private GameObject ghost;
    private readonly List<GameObject> mountains = new List<GameObject>();
    private readonly Dictionary<Color, Material> pieceMaterials = new Dictionary<Color, Material>();
    private readonly List<Material> terrainMaterials = new List<Material>();
    public static Color SeatColor(string id) => SeatColors[int.Parse(id.Substring(1)) - 1];
    public static Material Material(Color color, bool metallic = false)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.color = color; m.SetFloat("_Smoothness", .35f); m.SetFloat("_Metallic", metallic ? .45f : .05f); return m;
    }
    public void Initialize(PlayerView view)
    {
        BoardCamera = Camera.main;
        foreach (var v in view.Board.Vertices) vertices[v.Id] = new Vector3(v.X * Mathf.Sqrt(3) * .02f, .008f, -v.Y * .02f);
        foreach (var e in view.Board.Edges)
        {
            var a = vertices[e.Vertices[0]]; var b = vertices[e.Vertices[1]];
            edges[e.Id] = (a+b)*.5f; rotations[e.Id] = Quaternion.LookRotation(b-a);
        }
        foreach (var tile in view.Board.Tiles)
        {
            var center = tile.Vertices.Select(v=>vertices[v]).Aggregate(Vector3.zero,(s,v)=>s+v)/6f;
            center.y = 0;
            GameObject obj;
            if (tile.Resource == "ore")
            {
                obj = Instantiate(Resources.Load<GameObject>("mountain-tile-v001"), center, Quaternion.identity, transform);
                mountains.Add(obj);
            }
            else
            {
                obj = new GameObject(tile.Id); obj.transform.SetParent(transform); obj.transform.position=center;
                obj.AddComponent<MeshFilter>().sharedMesh=HexMesh();
                var material=Material(TerrainColor(tile.Resource));terrainMaterials.Add(material);
                obj.AddComponent<MeshRenderer>().sharedMaterial=material;
            }
            obj.name = tile.Id;
        }
        var table = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        table.name="Felt table"; table.transform.SetParent(transform); table.transform.position=new Vector3(0,-.012f,0);
        table.transform.localScale=new Vector3(.65f,.01f,.65f); var felt=Material(new Color(.055f,.16f,.18f));terrainMaterials.Add(felt);table.GetComponent<Renderer>().sharedMaterial=felt;
        pieces = new GameObject("Committed pieces").transform; pieces.SetParent(transform);
        Refresh(view); SetCamera(false);
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
        foreach(Transform t in pieces) Destroy(t.gameObject);
        foreach(var p in view.Board.Settlements) Piece(false,p.LocationId,SeatColor(p.PlayerId),pieces);
        foreach(var p in view.Board.Roads) Piece(true,p.LocationId,SeatColor(p.PlayerId),pieces);
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
    public Vector3 Position(string id) => id.StartsWith("V")?vertices[id]:edges[id];
    public Vector2 ScreenPoint(string id)
    {
        var p=BoardCamera.WorldToScreenPoint(Position(id)+Vector3.up*.005f); return new Vector2(p.x,Screen.height-p.y);
    }
    public void Preview(string id, bool road, bool legal)
    {
        ClearPreview(); if(string.IsNullOrEmpty(id))return;
        ghost=Piece(road,id,legal?new Color(.3f,1f,.75f):new Color(1f,.22f,.2f),transform);
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
    void OnDestroy() {foreach(var m in pieceMaterials.Values)Destroy(m);foreach(var m in terrainMaterials)Destroy(m);}
}
