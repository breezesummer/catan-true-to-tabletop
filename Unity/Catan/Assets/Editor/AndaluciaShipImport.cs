using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class AndaluciaShipImport
{
    const string Art="Assets/Art/Andalucia/";
    static readonly string[] Colours={"red","blue","orange","beige"};

    static Material Finish(Material original,int seat)
    {
        string id=original.name;
        bool flag=id=="归属旗布";
        bool canvas=id=="本白亚麻"||id=="受风亚麻";
        bool timber=id=="船壳橡木"||id=="焦油护舷"||id=="磨亮扶栏"||id=="晒旧甲板"||id=="艉楼雕木"||id=="分色舷板"||id=="磨亮护舷";
        string path=Art+id+(flag||(canvas&&seat!=3)?"-"+Colours[seat]:"")+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader=canvas||flag ? AssetDatabase.LoadAssetAtPath<Shader>(Art+"Sailcloth.shader") : Shader.Find("Universal Render Pipeline/Lit");
        if(shader==null || ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("船材着色器未成功导入："+id);
        if(mat==null)
        {
            mat=new Material(shader);
            AssetDatabase.CreateAsset(mat,path);
        }
        else if(mat.shader!=shader)mat.shader=shader;
        Color tint=id switch {
            "船壳橡木"=>new Color(.82f,.79f,.72f), "焦油护舷"=>new Color(.49f,.45f,.43f),
            "磨亮扶栏"=>new Color(.99f,.95f,.88f), "晒旧甲板"=>new Color(.98f,1,1),
            "艉楼雕木"=>new Color(.85f,.76f,.71f),
            "分色舷板"=>new Color(.94f,.91f,.85f), "磨亮护舷"=>new Color(1f,.97f,.87f),
            "归属旗布" or "本白亚麻" or "受风亚麻"=>EntityBoardAssets.SailColour(seat), "帆边缝线"=>new Color(.68f,.59f,.48f),
            "浅麻绳"=>new Color(.60f,.50f,.39f), "焦油索具"=>new Color(.28f,.23f,.18f),
            "锻铁"=>new Color(.25f,.23f,.21f), "炮窗内壁"=>new Color(.14f,.10f,.07f),
            _=>throw new InvalidOperationException("未识别的船材："+id)
        };
        mat.SetColor("_BaseColor",tint);
        mat.SetFloat("_Smoothness",id switch {
            "锻铁"=>.35f,"磨亮扶栏"=>.39f,"船壳橡木" or "分色舷板"=>.34f,
            "晒旧甲板" or "焦油护舷"=>.20f,_=>timber?.27f:canvas||flag?.16f:.08f});
        mat.SetFloat("_Metallic",id=="锻铁"?.62f:0);
        mat.SetFloat("_Cull",canvas||flag?0:2);
        mat.doubleSidedGI=canvas||flag;
        mat.enableInstancing=true;
        if(timber||canvas)
            mat.SetTexture("_BaseMap",Texture(id switch {
                "分色舷板"=>"舷板分色顺纹","磨亮护舷"=>"护舷磨亮木面","受风亚麻"=>"横帆裁片原色",
                _=>canvas?"亚麻原布":"橡木顺纹"}));
        if(id=="磨亮护舷")
        {
            mat.SetTexture("_MetallicGlossMap",Texture("护舷磨耗光滑度"));
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.SetFloat("_Smoothness",1);
        }
        if(timber||canvas||flag)
        {
            mat.SetTexture("_BumpMap",Texture(id switch {
                "分色舷板"=>"舷板导管法线","受风亚麻"=>"帆脚张力法线",_=>timber?"橡木细纹法线":"亚麻经纬法线"}));
            mat.SetFloat("_BumpScale",id switch {
                "受风亚麻"=>.94f,"磨亮护舷"=>.35f,"分色舷板"=>.65f,_=>timber?.52f:.48f});
            mat.EnableKeyword("_NORMALMAP");
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Texture2D Texture(string name) =>
        AssetDatabase.LoadAssetAtPath<Texture2D>(Art+name+".png") ?? throw new InvalidOperationException("缺少船体贴图："+name);

    public static void Prepare()
    {
        foreach(string name in new[]{"橡木细纹法线","亚麻经纬法线","舷板导管法线","帆脚张力法线"})
        {
            var normal=(TextureImporter)AssetImporter.GetAtPath(Art+name+".png");
            if(normal.textureType==TextureImporterType.NormalMap)continue;
            normal.textureType=TextureImporterType.NormalMap;
            normal.SaveAndReimport();
        }
        var wear=(TextureImporter)AssetImporter.GetAtPath(Art+"护舷磨耗光滑度.png");
        if(wear.sRGBTexture||wear.alphaIsTransparency)
        {
            wear.sRGBTexture=false;
            wear.alphaIsTransparency=false;
            wear.SaveAndReimport();
        }
        foreach(string name in new[]{"舷板分色顺纹","舷板导管法线","护舷磨亮木面","护舷磨耗光滑度","横帆裁片原色","帆脚张力法线"})
        {
            var texture=(TextureImporter)AssetImporter.GetAtPath(Art+name+".png");
            if(texture.filterMode==FilterMode.Trilinear && texture.anisoLevel==8)continue;
            texture.filterMode=FilterMode.Trilinear;
            texture.anisoLevel=8;
            texture.SaveAndReimport();
        }
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"安达卢西亚号.fbx");
        for(int seat=0;seat<4;seat++){
            var root=new GameObject("ship-"+Colours[seat]);
            var body=UnityEngine.Object.Instantiate(model,root.transform);
            var renderer=body.GetComponentInChildren<MeshRenderer>();
            var filter=renderer.GetComponent<MeshFilter>();
            var vertices=filter.sharedMesh.vertices.Select(v=>filter.transform.TransformPoint(v)).ToArray();
            var b=renderer.bounds;
            bool alongX=b.size.x>b.size.z;
            float Along(Vector3 v)=>alongX?v.x:v.z;
            var midpoint=Along(b.center);
            var extent=Along(b.extents);
            // 以艉楼木材的位置辨认船尾，帆形变化不会翻转船首。
            int sternSlot=Array.FindIndex(renderer.sharedMaterials,m=>m.name=="艉楼雕木");
            var stern=filter.sharedMesh.GetTriangles(sternSlot).Average(i=>Along(vertices[i]));
            if(Mathf.Abs(stern-midpoint)<extent*.25f)throw new Exception("艉楼位置不明确");
            var bow=(alongX?Vector3.right:Vector3.forward)*(stern>midpoint?-1:1);
            body.transform.rotation=Quaternion.FromToRotation(bow,Vector3.forward)*body.transform.rotation;
            body.transform.localScale*=EntityBoardAssets.ShipLength/(2*extent);
            b=renderer.bounds;
            body.transform.position-=new Vector3(b.center.x,b.min.y,b.center.z);
            int deckSlot=Array.FindIndex(renderer.sharedMaterials,m=>m.name=="晒旧甲板");
            var positions=filter.sharedMesh.vertices;
            float deckY=filter.sharedMesh.GetTriangles(deckSlot).Select(i=>filter.transform.TransformPoint(positions[i]).y).Min();
            var datum=new GameObject("主甲板最低点").transform;
            datum.SetParent(root.transform,false);
            datum.localPosition=Vector3.up*deckY;
            root.transform.localScale=new Vector3(1,EntityBoardAssets.ShipDeckAboveKeel/deckY,1);
            renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>Finish(m,seat)).ToArray();
            if(!renderer.sharedMaterials.Any(m=>m.name.Contains("归属旗布")))throw new Exception("帆材质未匹配");
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/"+PlayerComponentAssets.ExpansionRoot+root.name+".prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }
        string piratePath="Assets/Resources/"+EntityBoardAssets.PirateResource+".prefab";
        var pirate=PrefabUtility.LoadPrefabContents(piratePath);
        try{
            var child=pirate.transform.GetChild(0);
            var b=child.GetComponentInChildren<Renderer>().bounds;
            child.localScale*=EntityBoardAssets.PirateLength/Mathf.Max(b.size.x,b.size.z);
            PrefabUtility.SaveAsPrefabAsset(pirate,piratePath);
        }finally{PrefabUtility.UnloadPrefabContents(pirate);}
        AssetDatabase.SaveAssets();
        Debug.Log($"安达卢西亚号尺寸：港口模型 {CoastalDecals.PierModelLength*1000:F3} 毫米，船 {EntityBoardAssets.ShipLength*1000:F3} 毫米");
        StockModelPictures.ExportShips();
    }
}

