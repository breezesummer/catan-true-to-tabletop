using System;
using System.Collections.Generic;
using UnityEngine;

// U1 的可重建几何占位。输入仅为公开资产路径；不接触规则会话、手牌或随机源。
// 有限模板按主题版本缓存，共享网格／材质；切换不会反复生成模板。
public static class BoardThemeAssets
{
    static readonly Dictionary<string,GameObject> models=new Dictionary<string,GameObject>();
    static readonly Dictionary<string,Texture2D> icons=new Dictionary<string,Texture2D>();
    static readonly Dictionary<Color,Material> playerMaterials=new Dictionary<Color,Material>();
    static Transform templates;
    static Material neutral,player,gold;
    public static int CachedModels=>models.Count;
    public static string CacheKey(string resource)=>BoardThemeController.Current.Id+"/"+BoardThemeController.Current.AssetVersion+"/"+resource;
    public static GameObject Model(string resource)
    {
        if(models.TryGetValue(resource,out var cached))return cached;
        if(templates==null)
        {
            var root=new GameObject("ultra 主题模板（有限共享缓存）");root.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(root);templates=root.transform;
            neutral=BoardRenderer.Material(new Color(.20f,.28f,.28f));neutral.name="ultra 占位中性色";
            player=BoardRenderer.Material(Color.white);player.name="ultra 玩家色";
            gold=BoardRenderer.Material(new Color(.80f,.63f,.29f),true);gold.name="ultra 占位识别线";
        }
        var obj=new GameObject((resource.EndsWith("/tile-pasture",StringComparison.Ordinal)?"ultra 草原三维样板 · ":"ultra 开发占位 · ")+resource);obj.transform.SetParent(templates,false);
        var stamp=obj.AddComponent<BoardThemeStamp>();stamp.ThemeId="ultra";stamp.AssetVersion=idVersion(resource);
        try { Build(obj.transform,resource); }
        catch {UnityEngine.Object.Destroy(obj);throw;}
        models.Add(resource,obj);return obj;
    }
    static string idVersion(string resource)=>resource.EndsWith("/tile-pasture",StringComparison.Ordinal)?UltraPastureAssets.Version:"u1-placeholder-v001";
    static void Part(Transform root,PrimitiveType shape,Vector3 position,Vector3 scale,Material material)
    {
        var part=GameObject.CreatePrimitive(shape);part.name="开发占位几何";part.transform.SetParent(root,false);
        part.transform.localPosition=position;part.transform.localScale=scale;
        part.GetComponent<Renderer>().sharedMaterial=material;
        var collider=part.GetComponent<Collider>();collider.enabled=false;UnityEngine.Object.Destroy(collider);
    }
    static void Box(Transform root,float x,float y,float z,float sx,float sy,float sz,Material material)=>Part(root,PrimitiveType.Cube,new Vector3(x,y,z),new Vector3(sx,sy,sz),material);
    static void Build(Transform root,string path)
    {
        string id=path.Substring(path.LastIndexOf('/')+1);
        bool terrain=id.StartsWith("tile-")||id=="forest";
        if(id=="tile-pasture"){UltraPastureAssets.Build(root);return;}
        if(terrain)
        {
            var tile=new GameObject("公开地形占位");tile.transform.SetParent(root,false);
            tile.AddComponent<MeshFilter>().sharedMesh=BoardRenderer.HexMesh();
            var renderer=tile.AddComponent<MeshRenderer>();renderer.sharedMaterial=BoardRenderer.Material(TerrainColor(id));
            renderer.sharedMaterial.name="ultra 地形占位 · "+id;
            // 中央为共用规则点数留出位置，U2 接入真实地形时替换此开发占位。
            for(int i=0;i<3;i++)Box(root,-.021f+i*.021f,.009f,.022f,.009f,.002f,.004f,gold);
            return;
        }
        if(id=="barbarian-track")
        {
            Box(root,0,0,0,EntityBoardAssets.BarbarianTrackWidth,.0015f,EntityBoardAssets.BarbarianTrackDepth,neutral);
            for(int i=0;i<8;i++)
            {
                var p=EntityBoardAssets.BarbarianLocalPosition(i);
                Part(root,PrimitiveType.Cylinder,p,new Vector3(.015f,.0006f,.015f),gold);
            }
            return;
        }
        if(id.StartsWith("port-")||id=="catan-victory-marker")
        {
            Part(root,PrimitiveType.Cylinder,new Vector3(0,.001f,0),new Vector3(.024f,.001f,.024f),gold);
            // U1 共用准确的公开规则图案；占位底座独立，牌面重绘另由 U7A 交付。
            var label=GameObject.CreatePrimitive(PrimitiveType.Quad);label.name="共用公开港口／分值图案";label.transform.SetParent(root,false);
            label.transform.localPosition=new Vector3(0,.0022f,0);label.transform.localRotation=Quaternion.Euler(90,0,0);label.transform.localScale=Vector3.one*.021f;
            label.GetComponent<Collider>().enabled=false;UnityEngine.Object.Destroy(label.GetComponent<Collider>());
            var face=BoardRenderer.Material(Color.white);face.name="ultra 占位公共规则图案 · "+id;
            face.SetTexture("_BaseMap",Resources.Load<Texture2D>("EntityAssets/v005/tabletop-markers/"+id+(id.StartsWith("port-")?"-front":"")));
            label.GetComponent<MeshRenderer>().sharedMaterial=face;
            return;
        }
        if(id=="road"){Box(root,0,.002f,0,.004f,.004f,.025f,player);return;}
        if(id.Contains("ship")||id.Contains("pirate"))
        {
            Box(root,0,.0025f,0,.009f,.005f,.022f,id.Contains("pirate")||id.Contains("barbarian")?neutral:player);
            Box(root,0,.010f,0,.0015f,.015f,.0015f,gold);Box(root,.003f,.013f,0,.007f,.008f,.001f,neutral);return;
        }
        if(id.Contains("knight"))
        {
            int level=id.Contains("level-3")?3:id.Contains("level-2")?2:1;
            bool active=id.EndsWith("active")||id.EndsWith("helmet");
            Part(root,PrimitiveType.Cylinder,new Vector3(0,.003f,0),new Vector3(.012f,.003f,.012f),player);
            for(int i=0;i<level;i++)Box(root,0,.008f+i*.004f,0,.008f,.003f,.008f,player);
            Part(root,active?PrimitiveType.Sphere:PrimitiveType.Cube,new Vector3(0,.010f+level*.004f,0),Vector3.one*.006f,active?gold:neutral);return;
        }
        if(id.Contains("village")||id.Contains("city")||id.Contains("metropolis"))
        {
            bool wall=id.Contains("wall"),onlyWall=id.StartsWith("city-wall"),metro=id.Contains("metropolis");
            if(wall)for(int i=0;i<4;i++)Box(root,i<2?(i==0?-.012f:.012f):0,.003f,i>=2?(i==2?-.012f:.012f):0,i<2?.002f:.026f,.006f,i<2?.026f:.002f,player);
            if(!onlyWall)
            {
                Box(root,0,.005f,0,.012f,.010f,.012f,player);
                Part(root,PrimitiveType.Cylinder,new Vector3(0,.012f,0),new Vector3(.014f,.002f,.014f),metro?gold:player);
                if(!id.Contains("village"))Box(root,.007f,.009f,0,.006f,.018f,.006f,player);
                if(metro)Box(root,-.007f,.012f,0,.006f,.024f,.006f,gold);
            }
            return;
        }
        if(id.Contains("robber")||id.Contains("merchant"))
        {
            Part(root,PrimitiveType.Capsule,new Vector3(0,.010f,0),new Vector3(.009f,.010f,.009f),neutral);
            if(id.Contains("merchant"))Box(root,0,.004f,.006f,.015f,.006f,.009f,gold);return;
        }
        throw new InvalidOperationException("未登记的 ultra 开发占位资产："+path);
    }
    static Color TerrainColor(string id)
    {
        if(id.Contains("forest"))return new Color(.13f,.40f,.23f);
        if(id.Contains("pasture"))return new Color(.44f,.67f,.24f);
        if(id.Contains("fields"))return new Color(.91f,.65f,.16f);
        if(id.Contains("hills"))return new Color(.69f,.30f,.18f);
        if(id.Contains("ore"))return new Color(.39f,.45f,.51f);
        if(id.Contains("sea"))return new Color(.10f,.40f,.50f);
        if(id.Contains("gold"))return new Color(.73f,.51f,.13f);
        return new Color(.78f,.70f,.49f);
    }
    public static void Tint(GameObject instance,Color color)
    {
        if(instance==null)return;
        if(!playerMaterials.TryGetValue(color,out var material))
        {material=BoardRenderer.Material(color);material.name="ultra 玩家色 · "+ColorUtility.ToHtmlStringRGB(color);playerMaterials.Add(color,material);}
        // 共享材质跟随 Instantiate，建造飞行副本不会丢失玩家色。
        foreach(var r in instance.GetComponentsInChildren<Renderer>())
        {
            var paints=r.sharedMaterials;
            for(int i=0;i<paints.Length;i++)if(paints[i]!=null&&paints[i].name.StartsWith("ultra 玩家色",StringComparison.Ordinal))paints[i]=material;
            r.sharedMaterials=paints;
        }
    }
    // 操作缩略图用透明几何符号标识开发态；不复用旧主题雕塑照片。
    public static Texture2D Icon(string kind,string seat,int level)
    {
        string key=kind+seat+level;if(icons.TryGetValue(key,out var texture))return texture;
        texture=new Texture2D(64,64,TextureFormat.RGBA32,false){name="ultra 开发占位图标 "+key};
        var pixels=new Color[64*64];var color=BoardRenderer.SeatColor(seat);
        for(int y=8;y<56;y++)for(int x=8;x<56;x++)
        {
            bool inside=kind=="road"?y>=25&&y<=38:kind=="ship"?y<25||x>=29&&x<=34:kind.StartsWith("knight")?x>=22&&x<42:y<40||Mathf.Abs(x-32)<56-y;
            if(inside)pixels[y*64+x]=color;
        }
        texture.SetPixels(pixels);texture.Apply();icons.Add(key,texture);return texture;
    }
}
