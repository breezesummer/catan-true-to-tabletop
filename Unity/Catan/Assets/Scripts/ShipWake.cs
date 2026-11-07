using UnityEngine;
using UnityEngine.Rendering;

// 每个占用边缓存一条水面带，泡沫推进和消散全部由共享材质绘制。
public sealed class ShipWake : MonoBehaviour
{
    Mesh strip;
    internal static void Create(Transform parent,string edge,Material material,Vector3 ship,Vector3 corner,Vector3 end)
    {
        const int steps=16;
        ship.y=corner.y=end.y=TabletopOcean.WaterHeight;
        var aft=(corner-ship).normalized;
        var start=ship+aft*.0047f;
        bool connected=(end-corner).sqrMagnitude>.000001f;
        var finish=connected?Vector3.MoveTowards(end,corner,.0048f):corner;
        var points=new Vector3[(steps+1)*2];
        var uv=new Vector2[points.Length];
        var indices=new int[steps*6];
        for(int i=0;i<=steps;i++)
        {
            float t=(float)i/steps;
            var a=Vector3.Lerp(start,corner,t);
            var b=Vector3.Lerp(corner,finish,t);
            var center=connected?Vector3.Lerp(a,b,t):Vector3.Lerp(start,finish,t);
            var tangent=connected?(b-a).normalized:aft;
            var side=Vector3.Cross(Vector3.up,tangent)*Mathf.Lerp(.0023f,.0046f,t);
            center.y=TabletopOcean.WaterHeight;
            int k=i*2;
            points[k]=center-side;points[k+1]=center+side;
            uv[k]=new Vector2(-1,t);uv[k+1]=new Vector2(1,t);
            if(i==steps)continue;
            int j=i*6;
            indices[j]=k;indices[j+1]=k+2;indices[j+2]=k+1;
            indices[j+3]=k+1;indices[j+4]=k+2;indices[j+5]=k+3;
        }
        var root=new GameObject("船尾浪 "+edge);root.transform.SetParent(parent,true);
        TabletopScene.SetTableLayer(root.transform);
        var wake=root.AddComponent<ShipWake>();
        wake.strip=new Mesh{name="船尾水纹",vertices=points,uv=uv,triangles=indices};
        wake.strip.RecalculateBounds();
        root.AddComponent<MeshFilter>().sharedMesh=wake.strip;
        var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
    }
    void OnDestroy(){if(strip!=null)Destroy(strip);}
}
