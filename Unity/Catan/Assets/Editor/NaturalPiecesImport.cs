using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 只导入展示资产；四色共享三件网格和自然材质。
public static class NaturalPiecesImport
{
    const string Art="Assets/Art/NaturalPieces/v001/";
    const string Root="Assets/Resources/NaturalPieces/v001/";
    static readonly string[] Colours={"red","blue","orange","beige"};
    static Color[] Tints => BoardRenderer.SeatColors;
    [MenuItem("Catan/美术/导入自然四色棋子")]
    public static void Run() => ImportKinds(new[]{"village","city","road"});

    [MenuItem("Catan/美术/导入圆门村庄")]
    public static void ImportVillage()
    {
        ImportKinds(new[]{"village"});
        StockModelPictures.ExportKinds(new[]{"settlement"});
    }

    [MenuItem("Catan/美术/导入哥特王城")]
    public static void ImportCity()
    {
        ImportKinds(new[]{"city"});
        StockModelPictures.ExportKinds(new[]{"city"});
    }

    static void ImportKinds(string[] kinds)
    {
        Directory.CreateDirectory(Root);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(string kind in kinds)
        {
            var textureImporter=(TextureImporter)AssetImporter.GetAtPath(Art+kind+"-surface.png");
            textureImporter.sRGBTexture=true;textureImporter.mipmapEnabled=true;textureImporter.maxTextureSize=2048;
            textureImporter.filterMode=FilterMode.Trilinear;textureImporter.anisoLevel=4;textureImporter.wrapMode=TextureWrapMode.Clamp;
            textureImporter.textureCompression=TextureImporterCompression.CompressedHQ;textureImporter.SaveAndReimport();
            var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+kind+"-surface.png");
            bool detailed=kind=="village" || kind=="city";
            var normal=detailed?SurfaceTexture(kind,"normal",TextureImporterType.NormalMap):null;
            var finish=detailed?SurfaceTexture(kind,"finish",TextureImporterType.Default):null;
            var wear=kind=="village"?SurfaceTexture(kind,"contact",TextureImporterType.Default):null;
            var cityShader=kind=="city"?AssetDatabase.LoadAssetAtPath<Shader>(Art+"CitySurface.shader"):null;
            if(kind=="city" && (cityShader==null || ShaderUtil.ShaderHasError(cityShader)))
                throw new InvalidOperationException("王城表面着色器导入失败");
            var neutral=MakeMaterial("Natural_Masonry_"+kind,Color.white,atlas,normal,finish,wear,cityShader);
            var contact=MakeContact();
            for(int seat=0;seat<4;seat++)
            {
                var player=MakeMaterial("Natural_Player_"+kind+"_"+Colours[seat],Tints[seat],atlas,normal,finish,wear,cityShader);
                var masonry=kind!="village" && Colours[seat]=="beige"
                    ? MakeMaterial("Natural_Masonry_"+kind+"_ivory",new Color(.76f,.80f,.81f),atlas,normal,finish,cityShader:cityShader)
                    : neutral;
                string fbx=Art+kind+".fbx";
                if(seat==0)
                {
                    var importer=(ModelImporter)AssetImporter.GetAtPath(fbx);
                    importer.importAnimation=false;importer.addCollider=false;importer.isReadable=false;
                    importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;
                    if(kind=="city")
                    {
                        importer.generateMeshLods=true;
                        importer.maximumMeshLod=4;
                    }
                    importer.meshCompression=ModelImporterMeshCompression.Off;importer.SaveAndReimport();
                    if(kind=="city")
                    {
                        var mesh=AssetDatabase.LoadAssetAtPath<GameObject>(fbx).GetComponentInChildren<MeshFilter>().sharedMesh;
                        if(mesh.lodCount<2)throw new InvalidOperationException("王城缺少棋盘远景网格");
                        var triangles=Enumerable.Range(0,mesh.lodCount).Select(lod=>
                            Enumerable.Range(0,mesh.subMeshCount).Sum(slot=>(long)mesh.GetIndexCount(slot,lod))/3);
                        Debug.Log("CATAN_CITY_MESH_LODS: "+string.Join(", ",triangles));
                    }
                }
                var root=new GameObject(kind+"-"+Colours[seat]);
                try
                {
                    var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbx),root.transform);
                    model.name="自然棋子模型";
                    var renderers=model.GetComponentsInChildren<Renderer>();
                    foreach(var renderer in renderers)
                    {
                        renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m.name.StartsWith("Natural_Player")?player:m.name.StartsWith("Natural_Contact")?contact:masonry).ToArray();
                        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;
                        renderer.receiveShadows=false;
                    }
                    var b=Bounds(renderers);float width=kind=="city"?.0205f:kind=="village"?.018f:.0396f;
                    float scale=width/Mathf.Max(b.size.x,b.size.z);
                    model.transform.localScale*=scale;model.transform.localPosition=new Vector3(-b.center.x,-b.min.y,-b.center.z)*scale;
                    b=Bounds(renderers);
                    if(Mathf.Abs(b.min.y)>.00001f||Mathf.Max(b.size.x,b.size.z)>width+.0001f||root.GetComponentsInChildren<Collider>().Length!=0)
                        throw new InvalidOperationException("自然棋子尺寸或碰撞检查失败："+kind);
                    if(kind=="city"&&(b.size.y<.025f||b.size.y>.030f))throw new InvalidOperationException("主城高度不正确");
                    if(kind=="road"&&b.size.z<b.size.x)throw new InvalidOperationException("道路长轴不正确");
                    PrefabUtility.SaveAsPrefabAsset(root,Root+kind+"-"+Colours[seat]+".prefab");
                    Debug.Log("CATAN_NATURAL_PIECE: "+kind+" "+Colours[seat]+" bounds="+b.size.ToString("F6"));
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
                var thumbnail=(TextureImporter)AssetImporter.GetAtPath(Art+"thumbnail-"+kind+"-"+Colours[seat]+".png");
                thumbnail.isReadable=true;thumbnail.alphaIsTransparency=true;thumbnail.mipmapEnabled=true;
                thumbnail.maxTextureSize=512;thumbnail.wrapMode=TextureWrapMode.Clamp;thumbnail.SaveAndReimport();
            }
        }
        AssetDatabase.SaveAssets();
        PlayerPieceColorImport.ApplyKinds(kinds);
        Debug.Log("CATAN_NATURAL_IMPORT_OK: "+string.Join(", ",kinds));
    }
    static Texture2D SurfaceTexture(string kind,string suffix,TextureImporterType type)
    {
        string path=Art+kind+"-"+suffix+".png";
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=type;importer.sRGBTexture=false;importer.mipmapEnabled=true;
        importer.alphaIsTransparency=false;importer.wrapMode=TextureWrapMode.Clamp;
        importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=4;
        importer.textureCompression=TextureImporterCompression.CompressedHQ;
        importer.maxTextureSize=type==TextureImporterType.NormalMap?2048:suffix=="contact"?1024:512;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static Bounds Bounds(Renderer[] renderers){var b=renderers[0].bounds;foreach(var r in renderers.Skip(1))b.Encapsulate(r.bounds);return b;}
    static Material MakeContact()
    {
        const string path=Art+"Natural_Contact.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Catan/自然棋子贴地柔影"));AssetDatabase.CreateAsset(material,path);}
        material.SetTexture("_BaseMap",Resources.Load<Texture2D>("PaintedMap/Coast/gravel"));material.enableInstancing=true;
        EditorUtility.SetDirty(material);return material;
    }
    static Material MakeMaterial(string name,Color tint,Texture2D atlas,Texture2D normal=null,Texture2D finish=null,Texture2D wear=null,Shader cityShader=null)
    {
        string path=Art+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
        if(cityShader!=null)
        {
            material.shader=cityShader;
            if(name.StartsWith("Natural_Player_"))material.EnableKeyword("_CITY_TINTED_SLATE");
            else material.DisableKeyword("_CITY_TINTED_SLATE");
            material.SetTexture("_DetailMask",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/PlayerColors/city-mask.png"));
        }
        if(wear!=null)
        {
            var village=AssetDatabase.LoadAssetAtPath<Shader>(Art+"VillageSurface.shader");
            if(village==null||ShaderUtil.ShaderHasError(village))throw new InvalidOperationException("村庄表面着色器导入失败");
            material.shader=village;
            if(name.StartsWith("Natural_Player_"))material.EnableKeyword("_VILLAGE_GLAZE");
            else material.DisableKeyword("_VILLAGE_GLAZE");
            material.SetTexture("_OcclusionMap",wear);
            material.SetFloat("_OcclusionStrength",.73f);
            material.SetTexture("_DetailMask",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/PlayerColors/village-mask.png"));
        }
        material.name=name;material.SetTexture("_BaseMap",atlas);material.SetColor("_BaseColor",tint);
        material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.08f);
        material.SetFloat("_SpecularHighlights",0);material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");material.enableInstancing=true;
        if(normal!=null && finish!=null)
        {
            material.SetTexture("_BumpMap",normal);material.SetFloat("_BumpScale",.75f);
            material.SetTexture("_MetallicGlossMap",finish);material.SetFloat("_Smoothness",1f);
            material.SetFloat("_SpecularHighlights",1);
            material.EnableKeyword("_NORMALMAP");material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
        }
        // 厘米级模型以烘焙遮蔽表现缝隙，避免实时阴影在瓦边产生像素黑斑。
        material.SetFloat("_ReceiveShadows",0);material.EnableKeyword("_RECEIVE_SHADOWS_OFF");
        EditorUtility.SetDirty(material);return material;
    }
}
