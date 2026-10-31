using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

// 独立点数复用既有圆牌网格及原照；只在公开地图布局变化时定位。
public sealed class FishingNumberDisplay : MonoBehaviour
{
    readonly List<Material> materials=new List<Material>();
    RenderTexture water;
    Mesh mesh;
    Transform token;
    Vector3[] shore;
    Vector2 center;
    float radius;
    public bool Moved {get;private set;}
    public static FishingNumberDisplay Create(Transform root,GameObject model,int number,Vector3[] anchors)
    {
        var display=root.gameObject.AddComponent<FishingNumberDisplay>();
        display.Initialize(model,number,anchors);return display;
    }
    Vector3 Local(Vector2 uv)
    {
        return shore[0]+(shore[2]-shore[0])*uv.x+
            (shore[1]-(shore[0]+shore[2])*.5f)*((1-uv.y)/.4f);
    }
    void Initialize(GameObject model,int number,Vector3[] anchors)
    {
        shore=anchors;
        center=new Vector2(.716f,number==6||number==8||number==9?.49f:.455f);
        var texture=Resources.Load<Texture2D>(FishingTabletopAssets.PhotoRoot+"ground-"+number+"-albedo");
        var patch=new Material(Resources.Load<Shader>("HarborFishing/NumberWaterPatch"));materials.Add(patch);
        patch.SetVector("_Circle",new Vector4(center.x,center.y,.14f,.19f));
        water=new RenderTexture(texture.width,texture.height,0,RenderTextureFormat.ARGB32){name="渔港水纹 "+number};
        water.Create();Graphics.Blit(texture,water,patch);
        var renderer=model.GetComponentInChildren<Renderer>();
        var slots=renderer.sharedMaterials;
        for(int i=0;i<slots.Length;i++)if(slots[i].mainTexture!=null)
        {
            var material=new Material(slots[i]);material.SetTexture("_BaseMap",water);materials.Add(material);slots[i]=material;
        }
        renderer.sharedMaterials=slots;
        mesh=TabletopNumberTokens.CreateMesh();
        token=new GameObject("渔港点数 "+number).transform;token.SetParent(transform,false);
        token.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        var face=BoardRenderer.Material(Color.white);face.SetFloat("_Metallic",0);face.SetFloat("_Smoothness",.16f);
        face.SetTexture("_BaseMap",texture);
        face.SetTextureScale("_BaseMap",new Vector2(.218f,.29f));
        face.SetTextureOffset("_BaseMap",center-new Vector2(.109f,.145f));materials.Add(face);
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
    bool FitsBoard(Vector3 point,float r)
    {
        var polygon=new[]{new Vector2(0,1),new Vector2(.5f,.6f),new Vector2(1,1),new Vector2(1,.4f),new Vector2(.5f,0),new Vector2(0,.4f)}
            .Select(uv=>transform.TransformPoint(Local(uv))).Select(p=>new Vector2(p.x,p.z)).ToArray();
        var p=new Vector2(point.x,point.z);bool inside=false;
        for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++)
        {
            var a=polygon[j];var b=polygon[i];var d=b-a;
            float t=Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude);
            if((p-a-d*t).sqrMagnitude<r*r)return false;
            if((a.y>p.y)!=(b.y>p.y)&&p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
        }
        return inside;
    }
    public void Place(Vector3[][] footprints,Vector3 outward)
    {
        var ports=footprints.Select(Hull).ToArray();
        radius=Vector3.Distance(transform.TransformPoint(Local(center)),transform.TransformPoint(Local(center+Vector2.right*.109f)));
        var original=transform.TransformPoint(Local(center));var chosen=original;bool found=false;
        // 所有候选点位于两臂内部；绝不把数字推出渔港板。
        var candidates=new List<Vector2>{center,new Vector2(.29f,center.y),new Vector2(.25f,.50f),new Vector2(.75f,.50f),new Vector2(.32f,.48f),new Vector2(.68f,.48f)};
        for(int x=2;x<=8;x++)for(int y=2;y<=8;y++)candidates.Add(new Vector2(x*.1f,y*.1f));
        foreach(float scale in new[]{1f,.85f,.7f})
        {
            foreach(var uv in candidates)
            {
                var p=transform.TransformPoint(Local(uv));
                if(!FitsBoard(p,radius*scale+.0003f)||!Clear(p,radius*scale+.0005f,ports))continue;
                chosen=p;radius*=scale;found=true;break;
            }
            if(found)break;
        }
        chosen.y=FishingTabletopAssets.GroundBottom+.0015f+.00012f;
        if(!found)Debug.LogError("渔港板内没有点数空位："+name);
        Moved=Flat(chosen-original).sqrMagnitude>.000001f;
        token.position=chosen;
        token.rotation=Quaternion.Euler(0,-90,0);
        token.localScale=new Vector3(radius/.0105f/transform.lossyScale.x,1,radius/.0105f/transform.lossyScale.z);
    }
    static Vector3 Flat(Vector3 p){p.y=0;return p;}
    void OnDestroy()
    {
        foreach(var m in materials)if(m!=null)Destroy(m);
        if(water!=null){water.Release();Destroy(water);}
        if(mesh!=null)Destroy(mesh);
    }
}
