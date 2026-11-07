using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 只缓存公开棋子对应的显示对象；保持根节点名称和层级，供拾取、移动与高亮共用。
public sealed class TabletopPieceCache
{
    sealed class Entry
    {
        public string Appearance;
        public GameObject[] Roots;
        public MeshFilter[] OutlineMeshes;
    }
    readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>();
    readonly HashSet<string> touched=new HashSet<string>();
    Transform parent;
    string prefix;

    public void Begin(string group,Transform root)
    {
        parent=root;prefix=group+"/";touched.Clear();
    }
    public Change Update(string key,string appearance)
    {
        key=prefix+key;touched.Add(key);
        if(entries.TryGetValue(key,out var old)&&old.Appearance==appearance&&old.Roots.All(r=>r!=null))
            return new Change(null,null,null,0);
        Remove(key);
        return new Change(this,key,appearance,parent.childCount);
    }
    public void End()
    {
        foreach(var key in entries.Keys.Where(k=>k.StartsWith(prefix,StringComparison.Ordinal)&&!touched.Contains(k)).ToArray())Remove(key);
    }
    static Material outline,paintedOutline;
    static MaterialPropertyBlock inkProperties;
    bool FindPiece(string target,out Entry entry)
    {
        entry=null;
        if(string.IsNullOrEmpty(target))return false;
        var loc=Catan.Core.ThreeKingdoms.B45SelectionContract.BoardLocationId(target);
        if(!loc.StartsWith("V",StringComparison.Ordinal))return false;
        if(target.StartsWith("Knight:",StringComparison.Ordinal))return entries.TryGetValue("expansion/knight:"+loc,out entry);
        return entries.TryGetValue("basic/settlement:"+loc,out entry)||entries.TryGetValue("basic/city:"+loc,out entry)
            ||entries.TryGetValue("expansion/wall:"+loc,out entry)||entries.TryGetValue("expansion/metropolis:"+loc,out entry)
            ||entries.TryGetValue("expansion/knight:"+loc,out entry);
    }
    public bool HasPiece(string target)=>FindPiece(target,out _);
    public void DrawOutline(string target,Camera camera)
    {
        if(!FindPiece(target,out var piece)||camera==null)return;
        if(outline==null){
            var shader=Resources.Load<Shader>("PieceTargetOutline");
            if(shader==null)return;
            outline=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
        }
        if(piece.OutlineMeshes==null)piece.OutlineMeshes=piece.Roots.SelectMany(r=>r.GetComponentsInChildren<MeshFilter>()).ToArray();
        foreach(var filter in piece.OutlineMeshes){
            var mesh=filter==null?null:filter.sharedMesh;
            if(mesh==null||!filter.gameObject.activeInHierarchy)continue;
            var r=filter.GetComponent<MeshRenderer>();
            if(r==null||!r.enabled||r.forceRenderingOff)continue;
            var source=r.sharedMaterial;var material=outline;
            MaterialPropertyBlock properties=null;
            if(source!=null&&source.shader.name=="Catan/FantasyInk"){
                if(paintedOutline==null){
                    var shader=Resources.Load<Shader>("PaintedPieceTargetOutline");
                    if(shader==null)continue;
                    paintedOutline=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
                }
                if(inkProperties==null)inkProperties=new MaterialPropertyBlock();
                inkProperties.SetTexture("_BaseMap",source.mainTexture);
                inkProperties.SetFloat("_Atlas",source.GetFloat("_Atlas"));
                material=paintedOutline;properties=inkProperties;
            }
            for(int sub=0;sub<mesh.subMeshCount;sub++)
                Graphics.DrawMesh(mesh,filter.transform.localToWorldMatrix,material,filter.gameObject.layer,camera,sub,properties,UnityEngine.Rendering.ShadowCastingMode.Off,false);
        }
    }
    public string BuildingUnderRay(Ray ray)
    {
        string closest=null;
        float distance=float.PositiveInfinity;
        foreach(var item in entries)
        {
            string key=item.Key;
            if(!(key.StartsWith("basic/settlement:")||key.StartsWith("basic/city:")||
                 key.StartsWith("expansion/wall:")||key.StartsWith("expansion/metropolis:")))continue;
            foreach(var root in item.Value.Roots)
            {
                if(root==null||!root.activeInHierarchy)continue;
                foreach(var part in root.GetComponentsInChildren<MeshFilter>())
                {
                    if(part.sharedMesh==null)continue;
                    var renderer=part.GetComponent<Renderer>();
                    if(renderer!=null && (!renderer.enabled || renderer.forceRenderingOff))continue;
                    var toLocal=part.transform.worldToLocalMatrix;
                    var local=new Ray(toLocal.MultiplyPoint3x4(ray.origin),toLocal.MultiplyVector(ray.direction));
                    if(!part.sharedMesh.bounds.IntersectRay(local,out float hit))continue;
                    var point=part.transform.TransformPoint(local.GetPoint(hit));
                    float depth=Vector3.Dot(point-ray.origin,ray.direction);
                    if(depth<0||depth>=distance)continue;
                    distance=depth;closest=key.Substring(key.IndexOf(':')+1);
                }
            }
        }
        return closest;
    }
    void Remove(string key)
    {
        if(!entries.TryGetValue(key,out var old))return;
        foreach(var root in old.Roots)if(root!=null)
        {
            root.SetActive(false);root.transform.SetParent(null,true);UnityEngine.Object.Destroy(root);
        }
        entries.Remove(key);
    }
    public sealed class Change : IDisposable
    {
        readonly TabletopPieceCache owner;
        readonly string key,appearance;
        readonly int first;
        public bool Changed=>owner!=null;
        internal Change(TabletopPieceCache owner,string key,string appearance,int first)
        {this.owner=owner;this.key=key;this.appearance=appearance;this.first=first;}
        public void Dispose()
        {
            if(owner==null)return;
            var roots=new List<GameObject>();
            for(int i=first;i<owner.parent.childCount;i++)roots.Add(owner.parent.GetChild(i).gameObject);
            owner.entries[key]=new Entry{Appearance=appearance,Roots=roots.ToArray()};
        }
    }
}
