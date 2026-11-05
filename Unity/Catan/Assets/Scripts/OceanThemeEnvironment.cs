using UnityEngine;
using UnityEngine.Rendering;

// 礁石沿公开陆地外缘落在固定世界坐标，不参与棋盘拾取。
public sealed class OceanThemeEnvironment : MonoBehaviour
{
    readonly Transform[] reefs=new Transform[4];
    readonly Vector4[] shores=new Vector4[4];
    static readonly Vector2[] Directions={new Vector2(-.8f,.6f),new Vector2(.95f,.32f),new Vector2(.65f,-.76f),new Vector2(-.72f,-.69f)};
    static readonly float[] Angles={-22,68,12,-22};
    Material stone,water;
    bool loaded;
    Quaternion sourceRotation;
    Vector3 sourceScale;
    public int ReefCount => loaded?reefs.Length:0;
    public void Initialize(Material ocean){water=ocean;}
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
    public void Place(Vector2[] land,float radius)
    {
        if(!loaded)Load();
        if(!loaded)return;
        water.SetFloat("_OceanTheme",1);
        for(int i=0;i<reefs.Length;i++)
        {
            var direction=Directions[i].normalized;
            var edge=land[0];
            foreach(var tile in land)
                if(Vector2.Dot(tile-edge,direction)>0)edge=tile;
            var anchor=edge+direction*radius*2.25f;
            var point=new Vector3(anchor.x,TabletopOcean.WaterHeight,anchor.y);
            float size=radius*(i%2==0?.84f:.67f);
            reefs[i].position=point;
            reefs[i].rotation=Quaternion.Euler(0,Angles[i],0)*sourceRotation;
            reefs[i].localScale=sourceScale*size;
            shores[i]=new Vector4(point.x,point.z,size*.73f,size*.39f);
        }
        water.SetVectorArray("_ReefShores",shores);
        water.SetVector("_ReefAngles",new Vector4(-22,68,12,-22)*Mathf.Deg2Rad);
    }
    void OnDestroy(){if(stone!=null)Destroy(stone);}
}
