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

    [MenuItem("Catan/美术/导入商船、海盗船与城墙")]
    public static void ImportHarborPieces()
    {
        Import(kind=>kind=="ship"||kind=="wall"||kind=="pirate");
    }

    static void Import(Func<string,bool> include)
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        string recipe=Path.Combine(Application.dataPath,"../../../art/recipes/expansion-models-v001/几何.json");
        var parts=JsonUtility.FromJson<Manifest>("{\"items\":"+File.ReadAllText(recipe)+"}").items;
        if(include!=null)parts=parts.Where(part=>include(part.kind)).ToArray();
        var colours=new[]{"red","blue","orange","beige"};
        var contact=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/NaturalPieces/v001/Natural_Contact.mat");
        var tints=new[]{new Color(.82f,.23f,.145f),new Color(.27f,.48f,.77f),new Color(.98f,.54f,.18f),new Color(1,.98f,.89f)};
        int prefabCount=0;
        foreach(var part in parts)
        {
            bool pirate=part.kind=="pirate";
            if(part.kind.StartsWith("metro-",StringComparison.Ordinal)||part.kind=="ship"||pirate||part.kind=="wall")ImportSurfaceTexture(part.kind);
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+part.kind+"-surface.png");
            if(texture==null)throw new InvalidOperationException("缺少烘焙材质 "+part.kind);
            var neutral=Material("Expansion_Natural_"+part.kind,texture,Color.white,0,.13f);
            bool knight=part.kind.StartsWith("knight-");
            var steel=Material("Expansion_Metal_"+part.kind,texture,Color.white,knight ? .49f : .32f,knight ? .48f : .46f);
            var seats=pirate?new[]{neutral}:colours.Select((c,i)=>Material("Expansion_Seat_"+part.kind+"_"+c,texture,tints[i],0,.16f)).ToArray();
            var path=Art+part.kind+".fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.importAnimation=false;importer.addCollider=false;importer.isReadable=false;
            importer.importNormals=ModelImporterNormals.Import;importer.SaveAndReimport();
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
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
                    string output=pirate?"Assets/Resources/"+EntityBoardAssets.PirateResource+".prefab":Root+root.name+".prefab";
                    PrefabUtility.SaveAsPrefabAsset(root,output);
                    prefabCount++;
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
                if(pirate||part.kind.StartsWith("metro-")||part.kind.EndsWith("-active"))continue;
                var thumb=(TextureImporter)AssetImporter.GetAtPath(Root+"thumb-"+part.kind+"-"+colours[i]+".png");
                thumb.isReadable=true;thumb.alphaIsTransparency=true;thumb.mipmapEnabled=true;thumb.wrapMode=TextureWrapMode.Clamp;
                thumb.SaveAndReimport();
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("CATAN_EXPANSION_IMPORT_OK meshes="+parts.Length+" prefabs="+prefabCount);
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

