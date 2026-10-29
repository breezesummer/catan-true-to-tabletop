using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 独立美术入口：尺寸和材质适配，不修改规则、棋盘ID或拾取碰撞。
public static class CourtyardImport
{
    public const string Art="Assets/Art/Courtyard/v003/";
    public const string Resource="Assets/Resources/ThreeKingdoms/v003/";
    [Serializable] class Catalog {public Entry[] materials;}
    [Serializable] class Entry {public string name,baseColor,metalRough,normal;public float[] color;public float metallic,roughness,normalScale;}
    [Serializable] class PlayerColours { public PlayerColour[] colours; }
    [Serializable] class PlayerColour { public string key; public float[] srgb,linear; }
    [MenuItem("Catan/美术/导入四色庭院")]
    public static void Run()
    {
        Directory.CreateDirectory(Resource);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText(Art+"materials.json"));
        var normals=new HashSet<string>(catalog.materials.Select(m=>m.normal));
        var data=new HashSet<string>(catalog.materials.Select(m=>m.metalRough));
        foreach(var file in Directory.GetFiles(Art,"tex_*.png"))
        {
            var path=file.Replace('\\','/');var ti=(TextureImporter)AssetImporter.GetAtPath(path);string name=Path.GetFileName(path);
            ti.textureType=normals.Contains(name)?TextureImporterType.NormalMap:TextureImporterType.Default;
            ti.sRGBTexture=!normals.Contains(name)&&!data.Contains(name);ti.isReadable=data.Contains(name);
            ti.mipmapEnabled=true;ti.maxTextureSize=4096;ti.wrapMode=TextureWrapMode.Repeat;ti.filterMode=FilterMode.Trilinear;ti.anisoLevel=4;
            ti.textureCompression=TextureImporterCompression.CompressedHQ;ti.SaveAndReimport();
        }
        var materials=new Dictionary<string,Material>();
        foreach(var entry in catalog.materials)
        {
            string name=entry.name.StartsWith("v003_red.")?"Village_Seat_Courtyard_Red":entry.name;
            string path=Art+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",new Color(entry.color[0],entry.color[1],entry.color[2],entry.color[3]));
            if(!string.IsNullOrEmpty(entry.baseColor))mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+entry.baseColor));
            mat.SetFloat("_Metallic",entry.metallic);mat.SetFloat("_Smoothness",1-entry.roughness);
            if(!string.IsNullOrEmpty(entry.normal))
            {mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+entry.normal));mat.SetFloat("_BumpScale",entry.normalScale);mat.EnableKeyword("_NORMALMAP");}
            if(!string.IsNullOrEmpty(entry.metalRough))
            {
                string packed=Art+Path.GetFileNameWithoutExtension(entry.metalRough)+"-metal-smooth.png";
                if(!File.Exists(packed))
                {
                    var source=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+entry.metalRough);var pixels=source.GetPixels32();
                    for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32((byte)Mathf.RoundToInt(pixels[i].b*entry.metallic),0,0,(byte)Mathf.RoundToInt(255-pixels[i].g*entry.roughness));
                    var texture=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false,true);texture.SetPixels32(pixels);texture.Apply();File.WriteAllBytes(packed,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(packed,ImportAssetOptions.ForceSynchronousImport);
                    var ti=(TextureImporter)AssetImporter.GetAtPath(packed);ti.sRGBTexture=false;ti.maxTextureSize=4096;ti.mipmapEnabled=true;ti.textureCompression=TextureImporterCompression.CompressedHQ;ti.SaveAndReimport();
                }
                mat.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(packed));mat.SetFloat("_Smoothness",1);mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            EditorUtility.SetDirty(mat);materials.Add(entry.name,mat);
        }
        string fbx=Art+"courtyard.fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(fbx);
        importer.importAnimation=false;importer.addCollider=false;importer.isReadable=false;importer.meshCompression=ModelImporterMeshCompression.Off;importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;importer.SaveAndReimport();
        var root=new GameObject("courtyard-red");var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbx),root.transform);model.name="庭院模型";
        foreach(var renderer in model.GetComponentsInChildren<Renderer>())renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>materials.TryGetValue(m.name,out var mapped)?mapped:throw new InvalidOperationException("未映射庭院材质："+m.name)).ToArray();
        var renderers=model.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        float scale=.018f/Mathf.Max(bounds.size.x,bounds.size.z);model.transform.localScale*=scale;model.transform.localPosition=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z)*scale;
        bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        if(Mathf.Abs(bounds.min.y)>.00001f||bounds.size.x>.0181f||bounds.size.z>.0181f||root.GetComponentsInChildren<Collider>().Length!=0)throw new InvalidOperationException("庭院尺寸/碰撞验收失败："+bounds);
        PrefabUtility.SaveAsPrefabAsset(root,Resource+"courtyard-red.prefab");UnityEngine.Object.DestroyImmediate(root);
        string thumbnail=Resource+"thumbnail-red.png";var imageImporter=(TextureImporter)AssetImporter.GetAtPath(thumbnail);imageImporter.isReadable=true;imageImporter.alphaIsTransparency=true;imageImporter.maxTextureSize=1024;imageImporter.wrapMode=TextureWrapMode.Clamp;imageImporter.SaveAndReimport();
        ImportPlayerColours();
        AssetDatabase.SaveAssets();Debug.Log("CATAN_COURTYARD_IMPORT_OK bounds="+bounds.size.ToString("F6")+" materials="+materials.Count);
    }
    static void ImportPlayerColours()
    {
        var colours=JsonUtility.FromJson<PlayerColours>(File.ReadAllText(Art+"player-colours.json"));
        var red=AssetDatabase.LoadAssetAtPath<GameObject>(Resource+"courtyard-red.prefab");
        foreach(var colour in colours.colours)
        {
            var root=UnityEngine.Object.Instantiate(red);root.name="courtyard-"+colour.key;
            var cache=new Dictionary<Material,Material>();
            foreach(var renderer in root.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterials=renderer.sharedMaterials.Select(source=>
                {
                    bool lacquer=source.name=="Village_Seat_Courtyard_Red";
                    bool dark=source.name.StartsWith("v003_red_dark.");
                    bool cloth=source.name.StartsWith("v003_cloth.");
                    if(!lacquer&&!dark&&!cloth)return source;
                    if(cache.TryGetValue(source,out var existing))return existing;
                    string name="Village_Seat_Courtyard_"+colour.key+(dark?"_dark":cloth?"_cloth":"");
                    string path=Art+name+".mat";
                    var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(mat==null){mat=new Material(source);AssetDatabase.CreateAsset(mat,path);}
                    else mat.CopyPropertiesFromMaterial(source);
                    mat.name=name;
                    if(lacquer)
                    {
                        mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"player-"+colour.key+".png"));
                        mat.SetColor("_BaseColor",Color.white);
                    }
                    else
                    {
                        float factor=dark?.48f:1f;
                        var linear=new Color(colour.linear[0]*factor,colour.linear[1]*factor,colour.linear[2]*factor,1);
                        mat.SetColor("_BaseColor",linear.gamma);
                    }
                    EditorUtility.SetDirty(mat);cache.Add(source,mat);return mat;
                }).ToArray();
            }
            PrefabUtility.SaveAsPrefabAsset(root,Resource+"courtyard-"+colour.key+".prefab");UnityEngine.Object.DestroyImmediate(root);
        }
        foreach(string key in new[]{"red","blue","orange","beige"})
        {
            var ti=(TextureImporter)AssetImporter.GetAtPath(Resource+"thumbnail-"+key+".png");
            ti.isReadable=true;ti.alphaIsTransparency=true;ti.maxTextureSize=1024;ti.wrapMode=TextureWrapMode.Clamp;ti.SaveAndReimport();
        }
    }

}
