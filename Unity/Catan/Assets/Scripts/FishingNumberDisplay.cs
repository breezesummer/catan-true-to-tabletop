using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

// 渔场画面与点数独立；仅在公开海岸布局改变时重新定位。
public sealed class FishingNumberDisplay : MonoBehaviour
{
    readonly List<Material> materials=new List<Material>();
    Mesh mesh;
    Transform token;
    public bool Moved {get;private set;}
    public static FishingNumberDisplay Create(Transform root,int number)
    {
        var display=root.gameObject.AddComponent<FishingNumberDisplay>();
        display.Initialize(number);return display;
    }
    void Initialize(int number)
    {
        mesh=TabletopNumberTokens.CreateMesh();
        token=new GameObject("渔港点数 "+number).transform;token.SetParent(transform,false);
        token.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        var face=BoardRenderer.Material(Color.white);face.SetFloat("_Metallic",0);face.SetFloat("_Smoothness",.16f);
        face.SetTexture("_BaseMap",Resources.Load<Texture2D>("TabletopUI/v008/NumberTokens/number-"+number));
        materials.Add(face);
        var edge=BoardRenderer.Material(new Color(.23f,.35f,.52f));edge.SetFloat("_Metallic",0);materials.Add(edge);
        var tokenRenderer=token.gameObject.AddComponent<MeshRenderer>();tokenRenderer.sharedMaterials=new[]{face,edge,edge};
        tokenRenderer.shadowCastingMode=ShadowCastingMode.Off;
    }
    static float Cross(Vector2 a,Vector2 b,Vector2 c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
    static Vector2[] Hull(Vector3[] corners)
    {
        var points=corners.Select(p=>new Vector2(p.x,p.z)).Distinct().OrderBy(p=>p.x).ThenBy(p=>p.y).ToArray();
        var hull=new List<Vector2>();
        foreach(var p in points){while(hull.Count>=2&&Cross(hull[hull.Count-2],hull[hull.Count-1],p)<=0)hull.RemoveAt(hull.Count-1);hull.Add(p);}
        int lower=hull.Count;
        for(int i=points.Length-2;i>=0;i--){var p=points[i];while(hull.Count>lower&&Cross(hull[hull.Count-2],hull[hull.Count-1],p)<=0)hull.RemoveAt(hull.Count-1);hull.Add(p);}
        if(hull.Count>1)hull.RemoveAt(hull.Count-1);return hull.ToArray();
    }
    static bool Clear(Vector3 point,float r,Vector2[][] ports)
    {
        var p=new Vector2(point.x,point.z);
        foreach(var polygon in ports)
        {
            bool inside=true;
            for(int i=0;i<polygon.Length;i++)
            {
                var a=polygon[i];var b=polygon[(i+1)%polygon.Length];
                if(Cross(a,b,p)<0)inside=false;
                var d=b-a;float t=d.sqrMagnitude<1e-12f?0:Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude);
                if((p-a-d*t).sqrMagnitude<r*r)return false;
            }
            if(inside&&polygon.Length>=3)return false;
        }
        return true;
    }
    public void Place(Vector3 fold,Vector3 outward,float side,Vector3[][] footprints)
    {
        float radius=side*.174f;
        var original=fold+outward*(radius*.62f);
        var chosen=original;
        var ports=footprints.Select(Hull).ToArray();
        var tangent=Vector3.Cross(outward,Vector3.up).normalized;
        for(int step=1;step<=8&&!Clear(chosen,radius+.0004f,ports);step++)
        {
            float gap=step*radius*.40f;
            // 沿海岸折角向两侧避让，给码头与侧牌保留圆牌半径的净距。
            var forward=original+outward*(gap*.62f);
            var left=forward+tangent*gap;var right=forward-tangent*gap;
            if(Clear(left,radius+.0004f,ports)){chosen=left;break;}
            if(Clear(right,radius+.0004f,ports)){chosen=right;break;}
            chosen=original+outward*gap;
        }
        Moved=(chosen-original).sqrMagnitude>.000001f;
        chosen.y=FishingTabletopAssets.GroundBottom+.0005f;
        token.position=chosen;token.rotation=Quaternion.identity;
        token.localScale=new Vector3(radius/.0105f/transform.lossyScale.x,1,radius/.0105f/transform.lossyScale.z);
    }
    void OnDestroy()
    {
        foreach(var m in materials)if(m!=null)Destroy(m);
        if(mesh!=null)Destroy(mesh);
    }
}
