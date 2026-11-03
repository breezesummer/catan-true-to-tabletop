using UnityEngine;
using UnityEngine.Rendering;

// 礁石只锚定屏幕边缘，不进入公开棋盘边界、不带碰撞体，缩放和平移不占用棋盘空间。
public sealed class OceanThemeEnvironment : MonoBehaviour
{
    readonly Transform[] reefs=new Transform[4];
    readonly Vector4[] shores=new Vector4[4];
    static readonly Vector3[] Anchors={new Vector3(.018f,.977f,145),new Vector3(.995f,.095f,180),new Vector3(.87f,-.012f,155),new Vector3(-.006f,.005f,125)};
    Material stone,water;
    TabletopScene table;
    bool loaded;
    Quaternion sourceRotation;
    Vector3 sourceScale;
    public int ReefCount => loaded?reefs.Length:0;
    public void Initialize(TabletopScene owner,Material ocean){table=owner;water=ocean;}
    void Load()
    {
        var model=Resources.Load<GameObject>("Ocean/v003/ocean-reef-cluster");
        var shader=Resources.Load<Shader>("Ocean/v002/OceanReef");
        if(model==null||shader==null)return;
        sourceRotation=model.transform.localRotation;sourceScale=model.transform.localScale;
        stone=new Material(shader){name="海蚀灰岩与湿润礁底"};
        stone.SetTexture("_RockDetail",Resources.Load<Texture2D>("Ocean/v002/reef-detail"));
        for(int i=0;i<reefs.Length;i++)
        {
            var reef=Instantiate(model,transform);reef.name="边缘礁石 "+i;reefs[i]=reef.transform;
            foreach(var renderer in reef.GetComponentsInChildren<Renderer>())
            {renderer.sharedMaterial=stone;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;}
            TabletopScene.SetTableLayer(reef.transform);
        }
        loaded=true;
        Debug.Log("CATAN_OCEAN_REEFS_READY: instances=4 sourceRotation="+sourceRotation.eulerAngles+" sourceScale="+sourceScale);
    }
    void LateUpdate()
    {
        if(water==null||table==null)return;
        bool active=true;
        water.SetFloat("_OceanTheme",active?1:0);
        if(active&&!loaded)Load();
        var camera=table.TableCamera;
        if(!loaded||camera==null)return;
        var plane=new Plane(Vector3.up,new Vector3(0,TabletopOcean.WaterHeight,0));
        for(int i=0;i<reefs.Length;i++)
        {
            reefs[i].gameObject.SetActive(active);
            if(!active){shores[i]=Vector4.zero;continue;}
            var anchor=Anchors[i];var ray=camera.ViewportPointToRay(anchor);
            if(!plane.Raycast(ray,out float distance))continue;
            var point=ray.GetPoint(distance);
            float size=camera.orthographicSize*2/900*anchor.z/1.7f;
            reefs[i].position=point;
            reefs[i].rotation=Quaternion.Euler(0,i==1?68:i==2?12:-22,0)*sourceRotation;
            reefs[i].localScale=sourceScale*size;
            shores[i]=new Vector4(point.x,point.z,size*.73f,size*.39f);
        }
        water.SetVectorArray("_ReefShores",shores);
        water.SetVector("_ReefAngles",new Vector4(-22,68,12,-22)*Mathf.Deg2Rad);
    }
    void OnDestroy(){if(stone!=null)Destroy(stone);}
}
