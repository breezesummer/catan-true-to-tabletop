using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// UV3 只标记原模型中整圈相连的框条。字、徽记与铆钉即使取用同一块铜色图集也分属独立构件。
public sealed class HarborRimImport : AssetPostprocessor
{
    static readonly HashSet<string> Kinds = new HashSet<string> {"common","wool","grain","ore","lumber","brick"};
    static readonly Rect Bronze = new Rect(.25f,0,.25f,.25f);

    [MenuItem("Catan/美术/更新六港交易牌铜边")]
    public static void Import()
    {
        foreach(var kind in Kinds)
            AssetDatabase.ImportAsset("Assets/Resources/PaintedMap/Coast/"+kind+"-harbor.fbx",ImportAssetOptions.ForceUpdate);
    }

    void OnPostprocessModel(GameObject model)
    {
        if (!assetPath.StartsWith("Assets/Resources/PaintedMap/Coast/",StringComparison.Ordinal) ||
            !assetPath.EndsWith("-harbor.fbx",StringComparison.Ordinal)) return;
        var kind=Path.GetFileName(assetPath).Split('-')[0];
        if (!Kinds.Contains(kind)) return;
        var mesh=model.transform.Find("TradePlaque").GetComponent<MeshFilter>().sharedMesh;
        var positions=mesh.vertices;
        var uv=mesh.uv;
        var indices=mesh.triangles;
        var joined=new Dictionary<Vector3,int>();
        var parents=new int[positions.Length];
        for(int i=0;i<parents.Length;i++) parents[i]=i;
        int Root(int i)
        {
            while(parents[i]!=i) { parents[i]=parents[parents[i]]; i=parents[i]; }
            return i;
        }
        for(int t=0;t<indices.Length;t+=3)
        {
            int a=indices[t], b=indices[t+1], c=indices[t+2];
            if(!Bronze.Contains(uv[a]) || !Bronze.Contains(uv[b]) || !Bronze.Contains(uv[c])) continue;
            for(int j=t;j<t+3;j++)
            {
                int v=indices[j];
                if(joined.TryGetValue(positions[v],out int same)) parents[Root(v)]=Root(same);
                else joined.Add(positions[v],v);
                parents[Root(v)]=Root(a);
            }
        }
        var widths=new Dictionary<int,Vector2>();
        foreach(var pair in joined)
        {
            int root=Root(pair.Value);
            float x=pair.Key.x;
            widths[root]=widths.TryGetValue(root,out var span)
                ? new Vector2(Mathf.Min(span.x,x),Mathf.Max(span.y,x)) : new Vector2(x,x);
        }
        var mask=new Vector2[positions.Length];
        int marked=0;
        for(int i=0;i<mask.Length;i++)
        {
            if(!Bronze.Contains(uv[i]) || !widths.TryGetValue(Root(i),out var span) ||
                span.y-span.x<mesh.bounds.size.x*.9f) continue;
            mask[i]=new Vector2(1,Mathf.InverseLerp(span.x,span.y,positions[i].x));
            marked++;
        }
        if(marked==0) throw new InvalidDataException("交易牌缺少连续金属边框："+assetPath);
        mesh.uv3=mask;
        Debug.Log($"CATAN_HARBOR_RIM {kind} 框条顶点 {marked}/{mask.Length}");
    }
}
