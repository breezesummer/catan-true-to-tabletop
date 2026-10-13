using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 周边物件放在共同米制桌面上，缩放只改变相机。这里只接收已授权的显示数据。
public sealed class TabletopWorldObjects : MonoBehaviour
{
    public sealed class Hit
    {
        internal TabletopScene Scene;
        internal Transform Root;
        internal Vector2 Size;
        internal float Thickness;
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
        public Mesh[] OwnedMeshes;
        public Vector3 OriginalScale;
    }
    readonly Dictionary<string, Item> items = new Dictionary<string, Item>();
    readonly Dictionary<string, Material> faceMaterials = new Dictionary<string, Material>();
    readonly Dictionary<int, Texture2D> diceTextures = new Dictionary<int, Texture2D>();
    readonly List<Mesh> meshes = new List<Mesh>();
    TabletopScene scene;
    Material paperEdge, paperShadow;
    Texture2D paperShadowTexture;
    string group;
    int generation;
    public int ActiveCount => items.Values.Count(i => i.Object.activeSelf);
    public void Initialize(TabletopScene owner) { scene = owner; }
    public void Begin(string name) { group = name + "/"; generation++; }
    public void End()
    {
        foreach (var key in items.Keys.Where(k => k.StartsWith(group) && items[k].Generation != generation).ToArray())
        {
            Release(items[key]); items.Remove(key);
        }
    }
    void Release(Item item)
    {
        // 只释放本组件在创建此物件时登记的运行时网格；导入棋子共享网格不在此列表。
        foreach(var mesh in item.OwnedMeshes)
        {meshes.Remove(mesh);Destroy(mesh);}
        Destroy(item.Object);
    }
    Item Get(string key, string signature, Func<GameObject> create)
    {
        key = group + key;
        if (items.TryGetValue(key, out var prior) && prior.Signature != signature)
        { Release(prior); items.Remove(key); }
        if (!items.TryGetValue(key, out var item))
        {
            int firstOwnedMesh=meshes.Count;
            var obj = create(); obj.name = key; obj.transform.SetParent(transform, true);
            TabletopScene.SetTableLayer(obj.transform);
            foreach (var collider in obj.GetComponentsInChildren<Collider>()) { collider.enabled = false; Destroy(collider); }
            item = new Item { Object = obj, OriginalScale=obj.transform.localScale, Signature = signature, Hit = new Hit { Scene = scene, Root = obj.transform }, OwnedMeshes=meshes.Skip(firstOwnedMesh).ToArray() };
            items.Add(key, item);
        }
        item.Generation = generation; item.Object.SetActive(true); return item;
    }
    public Hit Piece(string key, string kind, string seat, int level, Vector2 position, bool raised = false, float yaw = float.NaN)
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
        item.Object.transform.localScale=item.OriginalScale;
        item.Object.transform.position = scene.World(position);
        item.Object.transform.rotation = Quaternion.Euler(0, float.IsNaN(yaw)?(kind == "road" || kind == "ship" ? 90 : 180):yaw, 0);
        if(item.Hit.Renderers==null)item.Hit.Renderers=item.Object.GetComponentsInChildren<Renderer>();
        var bounds=PieceBounds(item.Hit.Renderers);
        // 库存按真实网格占地排放：船横向，城墙保留完整间隙。基准比例不依赖当前缩放。
        float width=kind=="road"?18:kind=="settlement"?20:kind=="city"?28:kind=="ship"?30:kind=="wall"?34:22;
        float depth=kind=="road"?8:kind=="settlement"?20:kind=="city"?24:kind=="ship"?16:kind=="wall"?30:22;
        float scale=Mathf.Min(1,width*scene.UnitsPerPixel/Mathf.Max(.00001f,bounds.size.x),
            depth*scene.UnitsPerPixel/Mathf.Sin(40*Mathf.Deg2Rad)/Mathf.Max(.00001f,bounds.size.z));
        item.Object.transform.localScale=item.OriginalScale*scale;
        bounds=PieceBounds(item.Hit.Renderers);
        item.Object.transform.position+=new Vector3(scene.World(position).x-bounds.center.x,
            TabletopScene.SurfaceHeight+(raised?.003f:.0002f)-bounds.min.y,scene.World(position).z-bounds.center.z);
        return item.Hit;
    }
    static Bounds PieceBounds(Renderer[] renderers)
    {
        var bounds=renderers[0].bounds;
        foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
        return bounds;
    }
    Material Face(Texture2D texture, Rect uv, Color color, float smoothness = .24f)
    {
        string key = (texture == null ? 0 : texture.GetInstanceID()) + ":" + uv + ":" + color + ":" + smoothness;
        if (faceMaterials.TryGetValue(key, out var cached)) return cached;
        var material = BoardRenderer.Material(color); material.name = "桌面纸张面";
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", 0);
        if (texture != null)
        {
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", uv.size); material.SetTextureOffset("_BaseMap", uv.position);
        }
        faceMaterials.Add(key, material); return material;
    }
    GameObject Panel(Vector2 dimensions,float thickness,float radiusRatio)
    {
        var root = new GameObject("圆角纸板实体 v007");
        if(paperEdge==null)
        {
            paperEdge=BoardRenderer.Material(new Color(.78f,.70f,.53f));paperEdge.name="原色卡纸纸芯 v007";
            paperEdge.SetFloat("_Smoothness",.11f);paperEdge.SetFloat("_Metallic",0);
        }
        float radius=Mathf.Min(dimensions.x,dimensions.y)*radiusRatio;
        float bevel=Mathf.Min(thickness*.22f,Mathf.Min(dimensions.x,dimensions.y)*.006f);
        var boundary=RoundedBoundary(dimensions,radius);
        var inset=RoundedBoundary(dimensions-Vector2.one*(2*bevel),Mathf.Max(radius-bevel,bevel));
        int count=boundary.Length;
        var vertices=new List<Vector3>();var indices=new List<int>();
        // 真实侧壁和上下倒角。纸芯不使用方形 Cube，以免圆角背后再次露出方角。
        for(int ring=0;ring<4;ring++)
        {
            var contour=ring==0||ring==3?inset:boundary;
            float y=ring==0?0:ring==1?bevel:ring==2?thickness-bevel:thickness;
            for(int i=0;i<count;i++)vertices.Add(new Vector3(contour[i].x,y,contour[i].y));
        }
        for(int ring=0;ring<3;ring++)for(int i=0;i<count;i++)
        {
            int next=(i+1)%count,a=ring*count+i,b=ring*count+next,c=(ring+1)*count+i,d=(ring+1)*count+next;
            indices.AddRange(new[]{a,c,d,a,d,b});
        }
        int center=vertices.Count;vertices.Add(new Vector3(0,0,0));
        for(int i=0;i<count;i++)indices.AddRange(new[]{center,i,(i+1)%count});
        var edgeMesh=new Mesh{name="圆角卡纸厚边网格 v007"};edgeMesh.SetVertices(vertices);edgeMesh.SetTriangles(indices,0);edgeMesh.RecalculateNormals();edgeMesh.RecalculateBounds();meshes.Add(edgeMesh);
        AddMesh(root,"倒角纸芯",edgeMesh,paperEdge);

        vertices.Clear();indices.Clear();var uv=new List<Vector2>();
        vertices.Add(new Vector3(0,thickness+.000006f,0));uv.Add(new Vector2(.5f,.5f));
        for(int i=0;i<count;i++)
        {
            vertices.Add(new Vector3(inset[i].x,thickness+.000006f,inset[i].y));
            uv.Add(new Vector2(inset[i].x/dimensions.x+.5f,inset[i].y/dimensions.y+.5f));
        }
        for(int i=0;i<count;i++)indices.AddRange(new[]{0,(i+1)%count+1,i+1});
        var faceMesh=new Mesh{name="圆角原照片卡面网格 v007"};faceMesh.SetVertices(vertices);faceMesh.SetUVs(0,uv);faceMesh.SetTriangles(indices,0);faceMesh.RecalculateNormals();faceMesh.RecalculateBounds();meshes.Add(faceMesh);
        AddContactShadow(root,dimensions);
        AddMesh(root,"卡面",faceMesh,null);return root;
    }
    static Vector2[] RoundedBoundary(Vector2 dimensions,float radius)
    {
        const int steps=10;var points=new Vector2[4*(steps+1)];int index=0;
        for(int corner=0;corner<4;corner++)
        {
            float angle=corner*90;
            var center=new Vector2((corner==0||corner==3?1:-1)*(dimensions.x/2-radius),(corner<2?1:-1)*(dimensions.y/2-radius));
            for(int step=0;step<=steps;step++)
            {
                float radians=(angle+step*90f/steps)*Mathf.Deg2Rad;
                points[index++]=center+new Vector2(Mathf.Cos(radians),Mathf.Sin(radians))*radius;
            }
        }
        return points;
    }
    static void AddMesh(GameObject root,string name,Mesh mesh,Material material)
    {
        var child=new GameObject(name);child.transform.SetParent(root.transform,false);
        child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=material;
    }
    void AddContactShadow(GameObject root,Vector2 dimensions)
    {
        if(paperShadow==null)
        {
            const int size=96;paperShadowTexture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="纸板接触软阴影 v007",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                var point=new Vector2(Mathf.Abs((x+.5f)/size-.5f),Mathf.Abs((y+.5f)/size-.5f));
                var outside=new Vector2(Mathf.Max(point.x-.405f,0),Mathf.Max(point.y-.405f,0));
                float distance=outside.magnitude-.035f;
                float alpha=Mathf.Clamp01((.045f-distance)/.085f);
                pixels[y*size+x]=new Color(.015f,.009f,.003f,alpha*alpha*.30f);
            }
            paperShadowTexture.SetPixels32(pixels);paperShadowTexture.Apply(false,true);
            paperShadow=new Material(Resources.Load<Shader>("TabletopUI/v007/PaperContactShadow")){name="卡纸接触阴影材质 v007",renderQueue=2995};
            paperShadow.SetTexture("_BaseMap",paperShadowTexture);paperShadow.SetColor("_BaseColor",Color.white);
        }
        var mesh=new Mesh{name="卡纸接触阴影平面 v007"};float halfWidth=dimensions.x*.56f,halfDepth=dimensions.y*.55f;
        mesh.vertices=new[]{new Vector3(-halfWidth,.000012f,-halfDepth),new Vector3(-halfWidth,.000012f,halfDepth),new Vector3(halfWidth,.000012f,halfDepth),new Vector3(halfWidth,.000012f,-halfDepth)};
        mesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateNormals();meshes.Add(mesh);
        AddMesh(root,"接触软阴影",mesh,paperShadow);
        var renderer=root.transform.Find("接触软阴影").GetComponent<MeshRenderer>();renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
    }
    public Hit Card(string key, Rect layout, Texture2D texture, bool raised = false, Rect? uv = null, float elevation = 0)
    {
        var size = new Vector2(layout.width, layout.height) * scene.UnitsPerPixel;
        var position=scene.World(layout.center)+Vector3.up*((raised?.002f:.0001f)+elevation);
        return Surface(key,position,size,texture,uv??new Rect(0,0,1,1));
    }
    Hit Surface(string key, Vector3 position, Vector2 size, Texture2D texture, Rect uv,float thickness=.00045f,float radiusRatio=.095f)
    {
        var item = Get(key, size.ToString("F6")+":"+thickness+":"+radiusRatio, () => Panel(size,thickness,radiusRatio));
        item.Object.transform.position = position;
        item.Object.GetComponentsInChildren<MeshRenderer>().Last().sharedMaterial = Face(texture, uv, Color.white);
        item.Hit.Size = size;item.Hit.Thickness=thickness; item.Hit.Renderers = null; return item.Hit;
    }
    public Hit Facility(string key, Rect layout, int track, int level)
    {
        var texture = EntityCardAssets.Texture(EntityCardAssets.Guidance(false).Front);
        float[] cuts = { 0, .337f, .668f, 1 };
        var size=new Vector2(layout.width,layout.height)*scene.UnitsPerPixel;
        var position=scene.World(layout.center)+Vector3.up*.0001f;
        return Surface(key,position,size,texture,new Rect(cuts[track],0,cuts[track+1]-cuts[track],1),.0022f,.035f);
    }
    // 小翻板沿用原照布局；逐片覆盖正面／背面，整张建设卡始终完整。
    public static readonly Rect[] FacilitySlots = {
        new Rect(.505f,.645f,.362f,.268f), new Rect(.155f,.645f,.348f,.268f),
        new Rect(.155f,.362f,.348f,.268f), new Rect(.155f,.090f,.348f,.268f), new Rect(.508f,.090f,.355f,.268f)
    };
    public Hit FacilitySlot(string key, Hit board, int track, int level, bool built)
    {
        var slot = FacilitySlots[level-1];
        var location = board.Root.position + new Vector3((slot.center.x-.5f)*board.Size.x,board.Thickness+.00006f,(.5f-slot.center.y)*board.Size.y);
        return Surface(key, location, new Vector2(slot.width*board.Size.x,slot.height*board.Size.y),
            built ? EntityCardAssets.ImprovementBack(track,level) : EntityCardAssets.ImprovementFront(track,level), new Rect(0,0,1,1),.00075f,.045f);
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
        item.Object.GetComponent<MeshRenderer>().sharedMaterials=values.Select(v=>Face(DieTexture(index,v),new Rect(0,0,1,1),Color.white,.50f)).ToArray();
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
        if(paperShadow!=null)Destroy(paperShadow);
        if(paperShadowTexture!=null)Destroy(paperShadowTexture);
    }
}
