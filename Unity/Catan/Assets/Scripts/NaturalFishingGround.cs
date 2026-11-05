using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 只在公开海岸布局改变时写顶点，鱼群游动由 GPU 计算。
public sealed class NaturalFishingGround : MonoBehaviour
{
    public Vector3[] Shore { get; private set; }
    public Vector3[] Outer { get; private set; }
    public const int FishPerEdge=8;
    Mesh waterMesh,fishMesh;
    Transform water,fish;
    readonly Transform[] nets=new Transform[2];
    static Material waterInk,fishInk,netInk;

    static Material Ink(string shader,string texture)
    {
        var material=new Material(Resources.Load<Shader>(CoastalDecals.Root+shader));
        material.mainTexture=Resources.Load<Texture2D>(CoastalDecals.Root+texture);
        return material;
    }
    Transform Layer(string name,Mesh mesh,Material ink)
    {
        var obj=new GameObject(name).transform;
        obj.SetParent(transform,false);
        obj.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=obj.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial=ink;renderer.shadowCastingMode=ShadowCastingMode.Off;
        renderer.receiveShadows=false;
        return obj;
    }
    void Initialize()
    {
        if(waterInk==null)waterInk=Ink("FishingWater","gravel");
        if(fishInk==null)fishInk=Ink("SwimmingFish","fish");
        if(netInk==null)netInk=Ink("FloatingNet","loose-net");
        waterMesh=new Mesh{name="双边浅水"};
        fishMesh=new Mesh{name="近岸鱼群"};
        water=Layer("浅水",waterMesh,waterInk);
        fish=Layer("游鱼",fishMesh,fishInk);
        for(int i=0;i<2;i++)nets[i]=Layer("散网"+i,CoastalDecals.Rectangle(),netInk);
    }
    public Vector3 Refresh(Vector3 a,Vector3 b,Vector3 c,Vector3 n1,Vector3 n2)
    {
        if(water==null)Initialize();
        float length=(Vector3.Distance(a,b)+Vector3.Distance(b,c))*.5f;
        float width=length*.59f;
        var bisector=(n1+n2).normalized;
        Shore=new[]{a,b,c};
        Outer=new[]{a+n1*width,b+bisector*(width/Vector3.Dot(bisector,n1)),c+n2*width};
        var vertices=new Vector3[8];var uv=new Vector2[8];var ends=new List<Vector2>();
        for(int side=0;side<2;side++)
        {
            int k=side*4;
            var points=new[]{Shore[side],Outer[side],Outer[side+1],Shore[side+1]};
            for(int j=0;j<4;j++)
            {
                points[j].y=.0062f;vertices[k+j]=transform.InverseTransformPoint(points[j]);
                ends.Add(new Vector2(side==0?1:0,side==1?1:0));
            }
            uv[k]=Vector2.zero;uv[k+1]=Vector2.up;uv[k+2]=Vector2.one;uv[k+3]=Vector2.right;
            var along=(Shore[side+1]-Shore[side]).normalized;
            var normal=side==0?n1:n2;
            var center=Vector3.Lerp(Shore[side],Shore[side+1],side==0?.32f:.72f)+normal*width*.16f;
            center.y=.0086f;
            nets[side].SetPositionAndRotation(center,Quaternion.LookRotation(Vector3.Cross(along,Vector3.up),Vector3.up));
            nets[side].localScale=new Vector3(length*.49f/transform.lossyScale.x,1,width*.56f/transform.lossyScale.z);
        }
        waterMesh.vertices=vertices;waterMesh.uv=uv;waterMesh.SetUVs(1,ends);
        waterMesh.triangles=new[]{0,1,2,0,2,3,4,5,6,4,6,7};waterMesh.RecalculateBounds();
        var block=new MaterialPropertyBlock();
        block.SetVector("_ShoreA",new Vector4(a.x,a.z,b.x,b.z));
        block.SetVector("_ShoreB",new Vector4(b.x,b.z,c.x,c.z));
        block.SetFloat("_Width",width);
        water.GetComponent<Renderer>().SetPropertyBlock(block);
        FillFish(length);
        return Outer[1];
    }
    void FillFish(float length)
    {
        var vertices=new List<Vector3>();var uv=new List<Vector2>();
        var centers=new List<Vector4>();var courses=new List<Vector4>();var triangles=new List<int>();
        for(int side=0;side<2;side++)
        {
            var tangent=(Shore[side+1]-Shore[side]).normalized;
            for(int i=0;i<FishPerEdge;i++)
            {
                float u=.12f+i*.106f;
                float depth=.24f+(i%2)*.37f+.085f*Mathf.Sin(i*2.4f+side);
                var center=Vector3.Lerp(Vector3.Lerp(Shore[side],Shore[side+1],u),Vector3.Lerp(Outer[side],Outer[side+1],u),depth);
                center.y=.0068f;
                float size=length*(.14f+.045f*Mathf.Repeat(i*.43f,1));
                float phase=(i+side*FishPerEdge)*2.39996f;
                int index=vertices.Count;
                foreach(var corner in new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right})
                {
                    vertices.Add(transform.InverseTransformPoint(center));
                    uv.Add(corner);
                    centers.Add(new Vector4(center.x,center.z,phase,size));
                    courses.Add(new Vector4(tangent.x,tangent.z,depth,length));
                }
                triangles.AddRange(new[]{index,index+1,index+2,index,index+2,index+3});
            }
        }
        fishMesh.SetVertices(vertices);fishMesh.SetUVs(0,uv);fishMesh.SetUVs(1,centers);fishMesh.SetUVs(2,courses);
        fishMesh.SetTriangles(triangles,0);
        fishMesh.RecalculateBounds();
        var bounds=fishMesh.bounds;bounds.Expand(length*.7f/transform.lossyScale.x);fishMesh.bounds=bounds;
    }
    void OnDestroy()
    {
        if(waterMesh!=null)Destroy(waterMesh);
        if(fishMesh!=null)Destroy(fishMesh);
    }
}
