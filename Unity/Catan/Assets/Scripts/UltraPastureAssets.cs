using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

// Blender 生成的米制真实网格。只有公开的模块名称进入资产层；不读取规则会话。
public static class UltraPastureAssets
{
    [Serializable] sealed class Bundle { public string version; public Level[] lods; }
    [Serializable] sealed class Level { public MeshPart[] parts; }
    [Serializable] sealed class MeshPart { public string name; public float[] vertices,colors; public int[] triangles,paintTriangles; }
    static readonly Dictionary<string,Mesh[]> meshes=new Dictionary<string,Mesh[]>();
    static readonly Dictionary<Color,Material> colors=new Dictionary<Color,Material>();
    static Material natural;
    static bool loaded;
    public const string Version="u2-pasture-v003";
    static void Load()
    {
        if(loaded)return;
        var source=Resources.Load<TextAsset>("Ultra/v003/pasture-mesh");
        if(source==null)throw new InvalidOperationException("缺少 U2 草原网格，请运行 Build-UltraPasture.ps1。");
        Bundle data;
        using(var compressed=new MemoryStream(source.bytes))
        using(var stream=new GZipStream(compressed,CompressionMode.Decompress))
        using(var reader=new StreamReader(stream))data=JsonUtility.FromJson<Bundle>(reader.ReadToEnd());
        if(data.version!=Version||data.lods.Length!=2)throw new InvalidOperationException("U2 草原资产版本不匹配。");
        var shader=Resources.Load<Shader>("Ultra/v003/Pasture");
        if(shader==null||!shader.isSupported)throw new InvalidOperationException("当前设备不支持 U2 草原材质。");
        natural=new Material(shader){name="ultra 草原自然色"};natural.SetColor("_BaseColor",Color.white);
        for(int lod=0;lod<2;lod++)foreach(var p in data.lods[lod].parts)
        {
            var vertices=new Vector3[p.vertices.Length/3];var tint=new Color[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {vertices[i]=new Vector3(p.vertices[i*3],p.vertices[i*3+1],p.vertices[i*3+2]);tint[i]=new Color(p.colors[i*4],p.colors[i*4+1],p.colors[i*4+2],p.colors[i*4+3]);}
            var mesh=new Mesh{name="U2 "+p.name+" LOD"+lod};
            mesh.indexFormat=vertices.Length>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices=vertices;mesh.colors=tint;mesh.subMeshCount=2;
            mesh.SetTriangles(p.triangles,0);mesh.SetTriangles(p.paintTriangles,1);mesh.RecalculateNormals();mesh.RecalculateBounds();
            if(!meshes.ContainsKey(p.name))meshes.Add(p.name,new Mesh[2]);meshes[p.name][lod]=mesh;
        }
        loaded=true;
    }
    public static GameObject Part(string id,Transform parent,Color color)
    {
        Load();if(!meshes.TryGetValue(id,out var levels))throw new InvalidOperationException("缺少 U2 模块："+id);
        if(!colors.TryGetValue(color,out var paint))
        {paint=new Material(natural){name="ultra 牧场棚布 "+ColorUtility.ToHtmlStringRGB(color)};paint.SetColor("_BaseColor",color);colors.Add(color,paint);}
        var root=new GameObject(id);root.transform.SetParent(parent,false);var lods=new LOD[2];
        for(int i=0;i<2;i++)
        {
            var child=new GameObject("LOD"+i);child.transform.SetParent(root.transform,false);
            child.AddComponent<MeshFilter>().sharedMesh=levels[i];var r=child.AddComponent<MeshRenderer>();r.sharedMaterials=new[]{natural,paint};
            r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;
            lods[i]=new LOD(i==0?.19f:0,new Renderer[]{r});
        }
        var group=root.AddComponent<LODGroup>();group.SetLODs(lods);group.localReferencePoint=new Vector3(0,.010f,0);group.size=.079f;
        return root;
    }
    public static void Build(Transform root)
    {
        Part("ground",root,Color.white);
        for(int i=0;i<6;i++)Part("wild-"+i,root,Color.white);
        root.gameObject.AddComponent<UltraPastureTile>();
    }
}
