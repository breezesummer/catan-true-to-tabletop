using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ExpansionPiecesImport
{
    const string Art="Assets/Art/ExpansionPieces/v001/";
    const string Root="Assets/Resources/ExpansionPieces/v001/";
    [Serializable] class Geometry { public string kind; public float[] size; public int triangles; public float baseY; }
    [Serializable] class Manifest { public Geometry[] items; }

    [MenuItem("Catan/美术/导入扩展棋子")]
    public static void Run()
    {
        Import(null);
    }

    [MenuItem("Catan/美术/导入骑士")]
    public static void ImportKnights()
    {
        Import(kind=>kind.StartsWith("knight-",StringComparison.Ordinal));
    }

    [MenuItem("Catan/美术/导入大都会")]
    public static void ImportMetropolises()
    {
        Import(kind=>kind.StartsWith("metro-",StringComparison.Ordinal));
    }

    [MenuItem("Catan/美术/导入穹顶观星学宫")]
    public static void ImportScience() => Import(kind=>kind=="metro-science");

    [MenuItem("Catan/美术/导入哥特商会之都")]
    public static void ImportTrade() => Import(kind=>kind=="metro-trade");

    [MenuItem("Catan/美术/导入冠冕议政宫")]
    public static void ImportPolitics() => Import(kind=>kind=="metro-politics");

    [MenuItem("Catan/美术/导入王城石墙")]
    public static void ImportWall()
    {
        Import(kind=>kind=="wall");
        StockModelPictures.ExportKinds(new[]{"wall"});
    }

    [MenuItem("Catan/美术/导入商船、海盗船与城墙")]
    public static void ImportHarborPieces()
    {
        Import(kind=>kind=="ship"||kind=="wall"||kind=="pirate");
    }

    [MenuItem("Catan/美术/导入海盗船")]
    public static void ImportPirate()
    {
        Import(kind=>kind=="pirate");
    }

    static void Import(Func<string,bool> include)
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        string recipe=Path.Combine(Application.dataPath,"../../../art/recipes/expansion-models-v001/几何.json");
        var parts=JsonUtility.FromJson<Manifest>("{\"items\":"+File.ReadAllText(recipe)+"}").items;
        if(include!=null)parts=parts.Where(part=>include(part.kind)).ToArray();
        var colours=new[]{"red","blue","orange","beige"};
        var contact=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/NaturalPieces/v001/Natural_Contact.mat");
        var tints=BoardRenderer.SeatColors;
        int prefabCount=0;
        foreach(var part in parts)
        {
            if(part.kind=="ship"){AndaluciaShipImport.Prepare();continue;}
            bool pirate=part.kind=="pirate";
            if(part.kind.StartsWith("metro-",StringComparison.Ordinal)||part.kind=="ship"||pirate||part.kind=="wall")ImportSurfaceTexture(part.kind);
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+part.kind+"-surface.png");
            if(texture==null)throw new InvalidOperationException("缺少烘焙材质 "+part.kind);
            var neutral=Material("Expansion_Natural_"+part.kind,texture,Color.white,0,.13f);
            bool knight=part.kind.StartsWith("knight-");
            var steel=Material("Expansion_Metal_"+part.kind,texture,Color.white,knight ? .49f : .32f,knight ? .48f : .46f);
            var seats=pirate?new[]{neutral}:colours.Select((c,i)=>Material("Expansion_Seat_"+part.kind+"_"+c,texture,tints[i],0,.16f)).ToArray();
            bool detailed=part.kind.StartsWith("metro-",StringComparison.Ordinal)||part.kind=="wall";
            if(detailed)BuildingSurfaces(part.kind,neutral,seats,texture);
            var path=Art+part.kind+".fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.importAnimation=false;importer.addCollider=false;importer.isReadable=false;
            if(detailed)
            {
                importer.generateMeshLods=true;
                importer.maximumMeshLod=4;
                importer.importTangents=ModelImporterTangents.CalculateMikk;
            }
            importer.importNormals=ModelImporterNormals.Import;importer.SaveAndReimport();
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(detailed)
            {
                var mesh=source.GetComponentInChildren<MeshFilter>().sharedMesh;
                if(mesh.subMeshCount!=2 || mesh.lodCount<2 || !mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord1))
                    throw new InvalidOperationException("建筑缺少材质分区、遮蔽坐标或远景网格 "+part.kind);
                var counts=Enumerable.Range(0,mesh.lodCount).Select(lod=>
                    Enumerable.Range(0,mesh.subMeshCount).Sum(slot=>(long)mesh.GetIndexCount(slot,lod))/3);
                string track=part.kind.Replace("metro-","").ToUpperInvariant();
                Debug.Log("CATAN_"+track+"_LODS "+string.Join(", ",counts));
            }
            for(int i=0;i<seats.Length;i++)
            {
                var root=new GameObject(pirate?"pirate-black":part.kind+"-"+colours[i]);
                try
                {
                    var model=UnityEngine.Object.Instantiate(source,root.transform,false);
                    model.name="模型";
                    var renderer=model.GetComponentsInChildren<MeshRenderer>().Single();
                    renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m.name.StartsWith("Expansion_Seat")?seats[i]:
                        m.name.StartsWith("Expansion_Metal")?steel:m.name.StartsWith("Natural_Contact")?contact:neutral).ToArray();
                    var bounds=renderer.bounds;
                    float scale=Mathf.Max(part.size[0],part.size[1])/Mathf.Max(bounds.size.x,bounds.size.z);
                    model.transform.localScale*=scale;
                    model.transform.localPosition+=Vector3.up*(part.baseY-renderer.bounds.min.y);
                    if(Mathf.Abs(renderer.bounds.size.y-part.size[2])>.00015f||Mathf.Abs(renderer.bounds.min.y-part.baseY)>.00005f)
                        throw new InvalidOperationException("尺寸或落点异常 "+part.kind);
                    if(pirate)model.transform.localScale*=EntityBoardAssets.PirateLength/Mathf.Max(renderer.bounds.size.x,renderer.bounds.size.z);
                    string output=pirate?"Assets/Resources/"+EntityBoardAssets.PirateResource+".prefab":Root+root.name+".prefab";
                    PrefabUtility.SaveAsPrefabAsset(root,output);
                    prefabCount++;
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
                if(pirate||part.kind.StartsWith("metro-")||part.kind.EndsWith("-active"))continue;
                var thumb=(TextureImporter)AssetImporter.GetAtPath(Art+"thumb-"+part.kind+"-"+colours[i]+".png");
                thumb.isReadable=true;thumb.alphaIsTransparency=true;thumb.mipmapEnabled=true;thumb.wrapMode=TextureWrapMode.Clamp;
                thumb.SaveAndReimport();
            }
        }
        AssetDatabase.SaveAssets();
        PlayerPieceColorImport.ApplyKinds(parts.Select(p=>p.kind).Where(k=>k!="pirate"&&k!="ship").ToArray());
        Debug.Log("CATAN_EXPANSION_IMPORT_OK meshes="+parts.Length+" prefabs="+prefabCount);
    }

    static void BuildingSurfaces(string kind,Material masonry,Material[] coloured,Texture2D atlas)
    {
        // 学宫石工沿用王城图集，商会与议政宫分别使用自己的建筑图集。
        var city=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/NaturalPieces/v001/Natural_Masonry_city.mat");
        if(city==null||!city.HasProperty("_DetailMask")||ShaderUtil.ShaderHasError(city.shader))
            throw new InvalidOperationException("建筑表面材质缺失 "+kind);
        masonry.shader=city.shader;
        masonry.CopyPropertiesFromMaterial(city);
        bool metropolis=kind.StartsWith("metro-",StringComparison.Ordinal);
        if(metropolis)masonry.EnableKeyword("_METROPOLIS_FINISH");
        EditorUtility.SetDirty(masonry);
        Texture2D SurfaceMap(string suffix,TextureImporterType type)
        {
            string path=Art+kind+"-"+suffix+".png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=type;importer.sRGBTexture=false;importer.mipmapEnabled=true;
            importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=4;
            importer.alphaIsTransparency=false;importer.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        var normal=SurfaceMap("normal",TextureImporterType.NormalMap);
        var finish=SurfaceMap("finish",TextureImporterType.Default);
        var mask=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/PlayerColors/"+kind+"-mask.png");
        if(mask==null)throw new InvalidOperationException("建筑缺少玩家色遮罩 "+kind);
        void ApplyAtlas(Material material)
        {
            material.SetTexture("_BaseMap",atlas);
            material.SetTexture("_BumpMap",normal);
            material.SetTexture("_MetallicGlossMap",finish);
            material.SetTexture("_DetailMask",mask);
        }
        if(kind!="metro-science")ApplyAtlas(masonry);
        if(kind=="metro-politics")masonry.EnableKeyword("_PALACE_COURTYARD");
        for(int seat=0;seat<coloured.Length;seat++)
        {
            var surface=coloured[seat];
            surface.shader=city.shader;
            surface.CopyPropertiesFromMaterial(city);
            if(metropolis)surface.EnableKeyword("_METROPOLIS_FINISH");
            ApplyAtlas(surface);
            surface.SetColor("_BaseColor",BoardRenderer.SeatColors[seat]);
            surface.EnableKeyword("_CITY_TINTED_SLATE");
            EditorUtility.SetDirty(surface);
        }
    }

    static void ImportSurfaceTexture(string kind)
    {
        // 连续篷布、浅色琉璃瓦和铜穹顶的大色面在 PC 近景中容易显出压缩方块。
        var importer=(TextureImporter)AssetImporter.GetAtPath(Art+kind+"-surface.png");
        var pc=importer.GetPlatformTextureSettings("Standalone");
        if(pc.overridden&&pc.format==TextureImporterFormat.BC7&&importer.filterMode==FilterMode.Trilinear)return;
        pc.overridden=true;
        pc.format=TextureImporterFormat.BC7;
        pc.compressionQuality=85;
        importer.SetPlatformTextureSettings(pc);
        importer.filterMode=FilterMode.Trilinear;
        importer.SaveAndReimport();
    }

    static Material Material(string name,Texture2D texture,Color tint,float metallic,float smooth)
    {
        var path=Art+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
        material.SetTexture("_BaseMap",texture);material.SetColor("_BaseColor",tint);
        material.SetFloat("_Metallic",metallic);material.SetFloat("_Smoothness",smooth);
        material.SetFloat("_ReceiveShadows",0);material.EnableKeyword("_RECEIVE_SHADOWS_OFF");
        material.enableInstancing=true;EditorUtility.SetDirty(material);
        return material;
    }
}

