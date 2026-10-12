using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 所有物件共用地图相机与米制桌面。这里只接收已授权的显示数据，拾取不裁决规则。
public sealed class TabletopWorldObjects : MonoBehaviour
{
    public sealed class Hit
    {
        internal TabletopScene Scene;
        internal Transform Root;
        internal Vector2 Size;
        internal Renderer[] Renderers;
        public Rect Rect
        {
            get
            {
                if (Renderers != null && Renderers.Length > 0)
                {
                    var bounds = Renderers[0].bounds;
                    foreach (var r in Renderers.Skip(1)) bounds.Encapsulate(r.bounds);
                    return Scene.Project(bounds);
                }
                return Scene.ProjectSurface(Root.position, Size, Root.rotation);
            }
        }
        public bool Contains(Vector2 logicalMouse)
        {
            if (Root == null || !Root.gameObject.activeInHierarchy) return false;
            var ray = Scene.MouseRay(logicalMouse);
            if (Renderers != null)
                return Renderers.Any(r => r.bounds.IntersectRay(ray));
            var plane = new Plane(Root.up, Root.position);
            if (!plane.Raycast(ray, out float distance)) return false;
            var local = Root.InverseTransformPoint(ray.GetPoint(distance));
            return Mathf.Abs(local.x) <= Size.x / 2 && Mathf.Abs(local.z) <= Size.y / 2;
        }
    }
    sealed class Item
    {
        public GameObject Object;
        public Hit Hit;
        public string Signature;
        public int Generation;
    }
    readonly Dictionary<string, Item> items = new Dictionary<string, Item>();
    readonly Dictionary<string, Material> faceMaterials = new Dictionary<string, Material>();
    readonly Dictionary<int, Texture2D> diceTextures = new Dictionary<int, Texture2D>();
    readonly List<Mesh> meshes = new List<Mesh>();
    TabletopScene scene;
    Material paperEdge;
    string group;
    int generation;
    public int ActiveCount => items.Values.Count(i => i.Object.activeSelf);
    public void Initialize(TabletopScene owner) { scene = owner; }
    public void Begin(string name) { group = name + "/"; generation++; }
    public void End()
    {
        foreach (var key in items.Keys.Where(k => k.StartsWith(group) && items[k].Generation != generation).ToArray())
        {
            Destroy(items[key].Object); items.Remove(key);
        }
    }
    Item Get(string key, string signature, Func<GameObject> create)
    {
        key = group + key;
        if (items.TryGetValue(key, out var prior) && prior.Signature != signature)
        { Destroy(prior.Object); items.Remove(key); }
        if (!items.TryGetValue(key, out var item))
        {
            var obj = create(); obj.name = key; obj.transform.SetParent(transform, true);
            foreach (var collider in obj.GetComponentsInChildren<Collider>()) { collider.enabled = false; Destroy(collider); }
            item = new Item { Object = obj, Signature = signature, Hit = new Hit { Scene = scene, Root = obj.transform } };
            items.Add(key, item);
        }
        item.Generation = generation; item.Object.SetActive(true); return item;
    }
    public Hit Piece(string key, string kind, string seat, int level, Vector2 position, bool raised = false)
    {
        string resource = kind == "ship" ? EntityBoardAssets.ShipResource(seat) : kind == "road" ? "PhotoSamples/v001/road" :
            kind == "knight" ? PlayerComponentAssets.KnightResource(seat, level, false) :
            PlayerComponentAssets.BuildingResource(kind == "settlement" ? "village" : kind == "wall" ? "city-wall" : kind, seat);
        var model = PlayerComponentAssets.Model(resource);
        if (model == null) return null;
        var item = Get(key, resource+seat, () => {
            var instance=Instantiate(model);
            if(kind=="road")
            {
                var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",BoardRenderer.SeatColor(seat));
                foreach(var renderer in instance.GetComponentsInChildren<Renderer>())renderer.SetPropertyBlock(block);
            }
            return instance;
        });
        item.Object.transform.position = scene.World(position) + Vector3.up * (raised ? .003f : .0002f);
        item.Object.transform.rotation = Quaternion.Euler(0, kind == "road" ? 90 : 180, 0);
        if(item.Hit.Renderers==null)item.Hit.Renderers=item.Object.GetComponentsInChildren<Renderer>();return item.Hit;
    }
    Material Face(Texture2D texture, Rect uv, Color color)
    {
        string key = (texture == null ? 0 : texture.GetInstanceID()) + ":" + uv + ":" + color;
        if (faceMaterials.TryGetValue(key, out var cached)) return cached;
        var material = BoardRenderer.Material(color); material.name = "桌面纸张面";
        material.SetFloat("_Smoothness", .16f);
        if (texture != null)
        {
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", uv.size); material.SetTextureOffset("_BaseMap", uv.position);
        }
        faceMaterials.Add(key, material); return material;
    }
    GameObject Panel(Vector2 dimensions)
    {
        var root = new GameObject("薄卡片网格");
        if (paperEdge == null) { paperEdge = BoardRenderer.Material(new Color(.89f,.83f,.65f)); paperEdge.SetFloat("_Smoothness", .12f); }
        var edge = GameObject.CreatePrimitive(PrimitiveType.Cube); edge.transform.SetParent(root.transform, false);
        edge.transform.localPosition = Vector3.up * .00018f;
        edge.transform.localScale = new Vector3(dimensions.x, .00036f, dimensions.y);
        edge.GetComponent<Renderer>().sharedMaterial = paperEdge;
        var mesh = new Mesh { name = "桌面卡片顶面" };
        mesh.vertices = new[] { new Vector3(-dimensions.x/2,.0004f,-dimensions.y/2), new Vector3(-dimensions.x/2,.0004f,dimensions.y/2),
            new Vector3(dimensions.x/2,.0004f,dimensions.y/2), new Vector3(dimensions.x/2,.0004f,-dimensions.y/2) };
        mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
        mesh.triangles = new[] { 0,1,2,0,2,3 }; mesh.RecalculateNormals(); meshes.Add(mesh);
        var face = new GameObject("卡面"); face.transform.SetParent(root.transform, false);
        face.AddComponent<MeshFilter>().sharedMesh = mesh; face.AddComponent<MeshRenderer>(); return root;
    }
    public Hit Card(string key, Rect layout, Texture2D texture, bool raised = false, Rect? uv = null)
    {
        var size = new Vector2(layout.width, layout.height) * scene.UnitsPerPixel;
        return Surface(key, scene.World(layout.center) + Vector3.up * (raised ? .002f : .0001f), size, texture, uv ?? new Rect(0,0,1,1));
    }
    Hit Surface(string key, Vector3 position, Vector2 size, Texture2D texture, Rect uv)
    {
        var item = Get(key, size.ToString("F6"), () => Panel(size));
        item.Object.transform.position = position;
        item.Object.GetComponentsInChildren<MeshRenderer>().Last().sharedMaterial = Face(texture, uv, Color.white);
        item.Hit.Size = size; item.Hit.Renderers = null; return item.Hit;
    }
    public Hit Facility(string key, Rect layout, int track, int level)
    {
        var texture = EntityCardAssets.Texture(EntityCardAssets.Guidance(false).Front);
        float[] cuts = { 0, .337f, .668f, 1 };
        return Card(key, layout, texture, false, new Rect(cuts[track], 0, cuts[track+1]-cuts[track], 1));
    }
    // 小翻板沿用原照布局；逐片覆盖正面／背面，整张建设卡始终完整。
    public static readonly Rect[] FacilitySlots = {
        new Rect(.505f,.645f,.362f,.268f), new Rect(.155f,.645f,.348f,.268f),
        new Rect(.155f,.362f,.348f,.268f), new Rect(.155f,.090f,.348f,.268f), new Rect(.508f,.090f,.355f,.268f)
    };
    public Hit FacilitySlot(string key, Hit board, int track, int level, bool built)
    {
        var slot = FacilitySlots[level-1];
        var location = board.Root.position + new Vector3((slot.center.x-.5f)*board.Size.x,.0006f,(.5f-slot.center.y)*board.Size.y);
        return Surface(key, location, new Vector2(slot.width*board.Size.x,slot.height*board.Size.y),
            built ? EntityCardAssets.ImprovementBack(track,level) : EntityCardAssets.ImprovementFront(track,level), new Rect(0,0,1,1));
    }
    public Hit Die(string key, Rect layout, int index, int value, float progress, bool rolling)
    {
        float side = layout.width * scene.UnitsPerPixel;
        var item = Get(key, side.ToString("F6"), () => RoundedDie(side));
        float motion = rolling ? 1-progress : 0;
        var position = scene.World(layout.center) + Vector3.up * (side/2+.0002f + Mathf.Abs(Mathf.Sin(progress*Mathf.PI*5))*motion*side*.6f);
        position.x += Mathf.Sin(progress*Mathf.PI*6+index)*motion*side*.15f;
        item.Object.transform.position = position;
        item.Object.transform.rotation = Quaternion.Euler(0, rolling ? (1-Mathf.Pow(1-progress,3))*720*(index%2==0?1:-1) : 0, 0);
        int[] values;
        if(index==2)
        {
            // 事件骰保留三面船和黄／蓝／绿各一面，只把权威结果换到顶面。
            values=new[]{0,0,0,1,3,2};int topFace=Array.IndexOf(values,value);
            values[topFace]=values[0];values[0]=value;
        }
        else
        {
            int front=Enumerable.Range(1,6).First(n=>n!=value&&n!=7-value);
            int right=Enumerable.Range(1,6).First(n=>n!=value&&n!=7-value&&n!=front&&n!=7-front);
            values=new[]{value,7-value,7-front,front,7-right,right};
        }
        item.Object.GetComponent<MeshRenderer>().sharedMaterials=values.Select(v=>Face(DieTexture(index,v),new Rect(0,0,1,1),Color.white)).ToArray();
        item.Hit.Renderers = item.Object.GetComponentsInChildren<Renderer>(); return item.Hit;
    }
    GameObject RoundedDie(float side)
    {
        // 六面网格的边缘投向内缩圆角，形成真实倒角轮廓；无物理骰点。
        var mesh = new Mesh { name = "倒角骰子网格 v004" };
        var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var faces = new List<int[]>();
        var axes = new[] { Vector3.up,Vector3.down,Vector3.forward,Vector3.back,Vector3.left,Vector3.right };
        const int segments = 12; float inner = side*.40f, radius=side*.10f;
        foreach (var axis in axes)
        {
            var triangles=new List<int>();
            var right = axis.y != 0 ? Vector3.right : Vector3.Cross(Vector3.up,axis);
            var up = Vector3.Cross(axis,right); int start=vertices.Count;
            for(int y=0;y<=segments;y++)for(int x=0;x<=segments;x++)
            {
                var p=(axis+right*(2f*x/segments-1)+up*(2f*y/segments-1))*side/2;
                var inset=new Vector3(Mathf.Clamp(p.x,-inner,inner),Mathf.Clamp(p.y,-inner,inner),Mathf.Clamp(p.z,-inner,inner));
                vertices.Add(inset+(p-inset).normalized*radius); uv.Add(new Vector2(x/(float)segments,axis==Vector3.up?1-y/(float)segments:y/(float)segments));
            }
            for(int y=0;y<segments;y++)for(int x=0;x<segments;x++)
            {
                int a=start+y*(segments+1)+x,b=a+1,c=a+segments+1,d=c+1;
                triangles.AddRange(new[]{a,b,d,a,d,c});
            }
            faces.Add(triangles.ToArray());
        }
        mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.subMeshCount=6;
        for(int face=0;face<faces.Count;face++)mesh.SetTriangles(faces[face],face);
        mesh.RecalculateNormals();meshes.Add(mesh);
        var obj=new GameObject("倒角实体骰子");obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>();return obj;
    }
    Texture2D DieTexture(int index,int value)
    {
        int key=index*10+value;if(diceTextures.TryGetValue(key,out var known))return known;
        const int size=256;var texture=new Texture2D(size,size,TextureFormat.RGBA32,true){name="公开骰面",filterMode=FilterMode.Trilinear,anisoLevel=16};
        var pixels=new Color32[size*size];Color surface=index==0?new Color(.73f,.035f,.025f):new Color(.99f,.97f,.87f);
        Color ink=index==0?new Color(.99f,.96f,.86f):new Color(.12f,.09f,.06f);
        int[] dots=value==1?new[]{4}:value==2?new[]{0,8}:value==3?new[]{0,4,8}:value==4?new[]{0,2,6,8}:value==5?new[]{0,2,4,6,8}:new[]{0,2,3,5,6,8};
        if(index==2)ink=value==1?new Color(.81f,.54f,.025f):value==2?new Color(.035f,.23f,.68f):value==3?new Color(.06f,.43f,.12f):ink;
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float u=x/(float)size,v=y/(float)size;bool mark;
            if(index<2)mark=dots.Any(d=>Vector2.Distance(new Vector2(u,v),new Vector2(.25f+d%3*.25f,.25f+d/3*.25f))<.064f);
            else if(value==0)
                mark=(u>.23f&&u<.77f&&v>.20f&&v<.28f)||(u>.49f&&u<.53f&&v>.25f&&v<.80f)||
                    (v>.36f&&v<.76f&&u>.53f&&u<.78f-(v-.36f)*.45f)||(v>.38f&&v<.70f&&u<.47f&&u>.24f+(v-.38f)*.3f);
            else
                mark=(u>.23f&&u<.77f&&v>.22f&&v<.65f&&!(u>.43f&&u<.57f&&v<.43f))||
                    (v>.62f&&v<.79f&&((u>.23f&&u<.35f)||(u>.44f&&u<.56f)||(u>.65f&&u<.77f)));
            pixels[y*size+x]=mark?ink:surface;
        }
        texture.SetPixels32(pixels);texture.Apply(true,true);diceTextures.Add(key,texture);return texture;
    }
    void OnDestroy()
    {
        foreach(var m in faceMaterials.Values)Destroy(m);
        foreach(var m in meshes)Destroy(m);
        foreach(var t in diceTextures.Values)Destroy(t);
        if(paperEdge!=null)Destroy(paperEdge);
    }
}
