using System;
using System.Linq;
using UnityEngine;

[Serializable] public class EntityAssetCatalog
{
    public Entry[] assets;
    [Serializable] public class Entry { public string id,typeId,typeLabel,label,kind,resource; public float width,depth,height; }
}

// An inspection gallery of public art files; it never reads a game or its hidden cards.
public sealed class EntityAssetGallery : MonoBehaviour
{
    public string CatalogResource="EntityAssets/v001/catalog";
    public string DisplayTitle="实体照片转化 v001";
    public EntityAssetCatalog Catalog {get; private set;}
    public GameObject Current {get; private set;}
    public int Index {get; private set;}
    public bool Back {get; private set;}
    public float Yaw {get; private set;}
    public int View {get; private set;}
    Vector2 scroll;
    string[] types;
    Font font;
    GameObject context;
    float supportHeight;

    static bool IsStandingPiece(EntityAssetCatalog.Entry entry) => entry.kind=="custom"&&entry.typeId!="barbarian-track";

    void Awake()
    {
        var text=Resources.Load<TextAsset>(CatalogResource);
        if(text==null) throw new InvalidOperationException("Entity art catalog missing");
        Catalog=JsonUtility.FromJson<EntityAssetCatalog>(text.text);
        font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},18);
        types=Catalog.assets.Select(a=>a.typeId).Distinct().ToArray();
        Show(0);
    }
    public void Show(int index)
    {
        if(Current!=null) {Current.SetActive(false);Destroy(Current);}
        Index=(index+Catalog.assets.Length)%Catalog.assets.Length;
        var entry=Catalog.assets[Index];
        var prefab=Resources.Load<GameObject>(entry.resource);
        if(prefab==null) throw new InvalidOperationException("Missing prefab "+entry.id);
        Current=Instantiate(prefab); Current.name=entry.id;
        supportHeight=entry.height;Back=false; Yaw=180; UpdateTransform(); SetView(View);
    }
    public void Rotate(float degrees) {Yaw+=degrees;UpdateTransform();}
    public void Flip() {Back=!Back;UpdateTransform();}
    public void AddInsert(string id)
    {
        var previous=Current.transform.Find("Assembly insert");
        if(previous!=null){previous.gameObject.SetActive(false);Destroy(previous.gameObject);}
        var entry=Catalog.assets.Single(a=>a.id==id);
        var model=Resources.Load<GameObject>(entry.resource);
        if(model==null)throw new InvalidOperationException("Assembly insert missing: "+id);
        var insert=Instantiate(model,Current.transform,false);insert.name="Assembly insert";
        var renderers=Current.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
        foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
        supportHeight=bounds.size.y;
        SetView(View);
    }
    void UpdateTransform()
    {
        // Turn around the card's long axis; the back UV mirrors width, not length.
        Current.transform.rotation=Quaternion.Euler(0,Yaw,Back?180:0);
        var corner=View==3&&IsStandingPiece(Catalog.assets[Index])?new Vector3(.0343f,.008f,.0198f):Vector3.zero;
        Current.transform.position=corner+new Vector3(0,Back?supportHeight:0,0);
    }
    public void SetView(int view)
    {
        View=view; var entry=Catalog.assets[Index];
        if(context!=null) {context.SetActive(false);Destroy(context);context=null;}
        UpdateTransform();
        var renderers=Current.GetComponentsInChildren<Renderer>();
        var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
        var height=bounds.size.y;
        var span=Mathf.Max(bounds.size.x,bounds.size.z,height*1.7f,.012f);
        var target=bounds.center;
        if(view==3)
        {
            context=new GameObject("Public art crowding context");
            if(IsStandingPiece(entry))
            {
                var forest=Resources.Load<GameObject>("PhotoSamples/v001/forest");
                var road=Resources.Load<GameObject>("PhotoSamples/v001/road");
                var village=Resources.Load<GameObject>("PhotoSamples/v001/village");
                if(forest!=null)Instantiate(forest,Vector3.zero,Quaternion.identity,context.transform);
                if(road!=null)Instantiate(road,new Vector3(.020f,.008f,.028f),Quaternion.Euler(0,30,0),context.transform);
                if(village!=null)Instantiate(village,new Vector3(0,.008f,.0396f),Quaternion.identity,context.transform);
                target=new Vector3(.021f,.008f+height*.4f,.025f);span=Mathf.Max(.08f,span);
            }
            else
            {
                var prefab=Resources.Load<GameObject>(entry.resource);
                for(var neighbour=-1;neighbour<=1;neighbour+=2)
                    Instantiate(prefab,new Vector3(neighbour*entry.width*1.05f,0,0),Quaternion.Euler(0,Yaw,0),context.transform);
                span=Mathf.Max(entry.width*3.2f,entry.depth);
            }
        }
        var ship=entry.typeId=="ships"||entry.typeId=="pirate"||entry.typeId=="barbarian-ship";
        var direction=view==1?new Vector3(0,1,-.0001f):view==2?(ship?new Vector3(1,.08f,0):new Vector3(0,.08f,-1)):new Vector3(0,.95f,-1);
        var camera=Camera.main;
        camera.transform.position=target+direction.normalized*span*3;
        camera.transform.LookAt(target);
        camera.orthographic=true; camera.orthographicSize=span*(view==3?.60f:.78f);
        camera.rect=new Rect(.25f,0,.75f,1);
    }
    void OnGUI()
    {
        GUI.skin.label.font=font; GUI.skin.button.font=font;
        GUI.skin.label.fontSize=18; GUI.skin.button.fontSize=17;
        GUILayout.BeginArea(new Rect(10,12,Screen.width*.24f-18,Screen.height-24),GUI.skin.box);
        GUILayout.Label(DisplayTitle);
        GUILayout.Label((Index+1)+" / "+Catalog.assets.Length+" 件");
        scroll=GUILayout.BeginScrollView(scroll);
        foreach(var type in types)
        {
            var first=Array.FindIndex(Catalog.assets,a=>a.typeId==type);
            if(GUILayout.Button(Catalog.assets[first].typeLabel,GUILayout.Height(34)))Show(first);
        }
        GUILayout.EndScrollView();
        GUILayout.Label(Catalog.assets[Index].label);
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("上一件"))Show(Index-1);
        if(GUILayout.Button("下一件"))Show(Index+1);
        GUILayout.EndHorizontal();
        if(GUILayout.Button("旋转 45°"))Rotate(45);
        if(GUILayout.Button(Back?"查看正面":"翻到背面 / 底面"))Flip();
        if(Catalog.assets[Index].typeId=="city-wall")
        {
            if(GUILayout.Button("装入城市"))AddInsert("city-red");
            if(GUILayout.Button("装入大都会"))AddInsert("metropolis-red");
            if(GUILayout.Button("只看城墙"))Show(Index);
        }
        foreach(var pair in new[]{"游戏相机","俯视","侧视","近看"}.Select((name,index)=>new{name,index}))
            if(GUILayout.Button(pair.name))SetView(pair.index);
        GUILayout.Label(CatalogResource.Contains("/v002/")?"尺寸为估算\n照片参考三维模型\n隐藏面与最终外观待确认":"尺寸为估算\n原照片校正贴图\n隐藏面与最终外观待确认");
        GUILayout.EndArea();
    }
}
