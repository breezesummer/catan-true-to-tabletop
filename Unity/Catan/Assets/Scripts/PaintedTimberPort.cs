using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

// 用原画的透明外轮廓绘制、拾取并避让渔场，三者共用同一张平面网格。
public sealed class PaintedTimberPort : MonoBehaviour
{
    static readonly string[] names={"北","东北","东南","南","西南","西北"};
    // 原画中沿岸垂线的方向，以图上方为零、顺时针为正。
    static readonly float[] angles={0,32,150,180,212,330};
    static readonly Mesh[] meshes=new Mesh[6];
    static readonly Material[] materials=new Material[6];
    MeshFilter surface;

    public void Align(Vector3 sea,float length)
    {
        // 棋盘点朝上的六角形，其海岸法线相对正北偏半个扇区。
        float angle=Mathf.Atan2(sea.x,sea.z)*Mathf.Rad2Deg;
        int frame=Mathf.FloorToInt(Mathf.Repeat(angle+1,360)/60)%6;
        if(surface==null)
        {
            var obj=new GameObject("木材港原画");
            obj.transform.SetParent(transform,false);
            surface=obj.AddComponent<MeshFilter>();
            obj.AddComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        }
        if(meshes[frame]==null) Load(frame);
        surface.sharedMesh=meshes[frame];
        surface.GetComponent<MeshRenderer>().sharedMaterial=materials[frame];
        surface.transform.localScale=Vector3.one*length;
        surface.transform.localPosition=new Vector3(0,.00035f,CoastalDecals.ShoreEnd-CoastalDecals.DockInset);
    }

    static void Load(int frame)
    {
        var sprite=Resources.Load<Sprite>(CoastalDecals.Root+"Timber/"+names[frame]);
        var vertices=sprite.vertices;
        var radians=angles[frame]*Mathf.Deg2Rad;
        var sea=new Vector2(Mathf.Sin(radians),Mathf.Cos(radians));
        var across=new Vector2(-sea.y,sea.x);
        float left=vertices.Min(p=>Vector2.Dot(p,across)),right=vertices.Max(p=>Vector2.Dot(p,across));
        float width=right-left;
        var mesh=new Mesh{name="木材港-"+names[frame]};
        mesh.vertices=vertices.Select(p=>new Vector3((Vector2.Dot(p,across)-(left+right)*.5f)/width,0,-Vector2.Dot(p,sea)/width)).ToArray();
        mesh.uv=sprite.uv;
        mesh.triangles=sprite.triangles.Select(i=>(int)i).ToArray();
        mesh.RecalculateBounds();
        meshes[frame]=mesh;
        materials[frame]=new Material(Resources.Load<Shader>("FantasyPieces/FantasyInk"))
            {mainTexture=sprite.texture,renderQueue=3001};
    }

    public IEnumerable<Vector3[]> Footprint()
    {
        var mesh=surface.sharedMesh;
        var points=mesh.vertices;
        var indices=mesh.triangles;
        for(int i=0;i<indices.Length;i+=3)
            yield return new[]{surface.transform.TransformPoint(points[indices[i]]),
                surface.transform.TransformPoint(points[indices[i+1]]),surface.transform.TransformPoint(points[indices[i+2]])};
    }
}
