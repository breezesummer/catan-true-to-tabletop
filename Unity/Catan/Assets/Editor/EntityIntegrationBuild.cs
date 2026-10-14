using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Import new frozen versions without replacing the earlier photo conversion outputs.
public static class EntityIntegrationBuild
{
    public static void BuildTabletopMarkers()
    {
        Import("v005","tabletop-markers","tabletop-markers-v005","unity-import.json");
        M6Build.Build();
        Debug.Log("CATAN_TABLETOP_MARKERS_BUILD_OK");
    }

    public static void Build()
    {
        Import("v003","progress-politics","entity-integration-v003","unity-import-v003.json");
        Import("v004","ore-tile","ore-tile-v004","unity-import.json");
        M6Build.Build();
        Debug.Log("CATAN_ENTITY_INTEGRATION_BUILD_OK");
    }

    static void Import(string version,string type,string evidenceDirectory,string evidenceName)
    {
        var repo=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
        var export=Path.Combine(repo,"art/exports/entity-conversion-"+version+"/"+type);
        if(JsonUtility.FromJson<EntityAssetBuild.Verification>(File.ReadAllText(Path.Combine(export,"verification.json"))).status!="passed")
            throw new Exception(type+" "+version+" mesh verification has not passed");
        var manifest=JsonUtility.FromJson<EntityAssetBuild.Manifest>(File.ReadAllText(Path.Combine(export,"manifest.json")));
        var runtime="Assets/Resources/EntityAssets/"+version+"/"+type+"/";
        var models="Assets/Art/EntityAssets/"+version+"/"+type+"/";
        Directory.CreateDirectory(runtime);Directory.CreateDirectory(models+"Materials");
        foreach(var texture in Directory.GetFiles(Path.Combine(export,"textures"),"*.png"))File.Copy(texture,runtime+Path.GetFileName(texture),true);
        foreach(var asset in manifest.assets)File.Copy(Path.Combine(export,asset.id+".fbx"),models+asset.id+".fbx",true);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(var texture in Directory.GetFiles(runtime,"*.png"))
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(texture.Replace('\\','/'));
            importer.textureType=TextureImporterType.Default;importer.textureShape=TextureImporterShape.Texture2D;
            importer.sRGBTexture=true;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;
            importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=8;importer.maxTextureSize=2048;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        }
        var evidence=new System.Collections.Generic.List<EntityAssetBuild.ImportRecord>();
        foreach(var asset in manifest.assets)
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath(models+asset.id+".fbx");
            importer.globalScale=1;importer.useFileScale=true;importer.importAnimation=false;
            importer.addCollider=false;importer.isReadable=true;importer.SaveAndReimport();
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(models+asset.id+".fbx");
            if(source==null)throw new Exception("Missing "+version+" FBX "+asset.id);
            var root=new GameObject(asset.id);UnityEngine.Object.Instantiate(source,root.transform,false);
            var renderers=root.GetComponentsInChildren<Renderer>();
            if(renderers.Length==0)throw new Exception("No renderers "+asset.id);
            var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            var expected=asset.expectedUnityBounds;
            if((bounds.min-new Vector3(expected.min[0],expected.min[1],expected.min[2])).magnitude>.000015f||
               (bounds.max-new Vector3(expected.max[0],expected.max[1],expected.max[2])).magnitude>.000015f)
                throw new Exception(version+" Unity bounds differ "+asset.id);
            foreach(var renderer in renderers)
                renderer.sharedMaterials=renderer.sharedMaterials.Select(original=>
                {
                    if(original==null)throw new Exception("Missing material "+asset.id);
                    var spec=asset.materials.Single(m=>m.name==original.name.Replace(" (Instance)",""));
                    var path=models+"Materials/"+spec.name+".mat";
                    var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
                    var c=spec.baseColorLinearRgba;material.color=new Color(c[0],c[1],c[2],c[3]).gamma;
                    material.SetFloat("_Smoothness",1-spec.roughness);material.SetFloat("_Metallic",0);
                    material.mainTexture=string.IsNullOrEmpty(spec.texture)?null:AssetDatabase.LoadAssetAtPath<Texture2D>(runtime+spec.texture);
                    if(!string.IsNullOrEmpty(spec.texture)&&material.mainTexture==null)throw new Exception("Missing PNG "+spec.texture);
                    EditorUtility.SetDirty(material);return material;
                }).ToArray();
            PrefabUtility.SaveAsPrefabAsset(root,runtime+asset.id+".prefab");
            evidence.Add(new EntityAssetBuild.ImportRecord{id=asset.id,typeId=manifest.typeId,verified=true,
                dimensionsMetres=new[]{bounds.size.x,bounds.size.y,bounds.size.z},renderers=renderers.Length,
                triangles=root.GetComponentsInChildren<MeshFilter>().Sum(m=>m.sharedMesh.triangles.Length/3)});
            UnityEngine.Object.DestroyImmediate(root);
        }
        AssetDatabase.SaveAssets();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var output=Path.Combine(repo,"docs/verification/"+evidenceDirectory);Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output,evidenceName),JsonUtility.ToJson(new EntityAssetBuild.Evidence{
            unityVersion=Application.unityVersion,assets=evidence.ToArray()},true));
    }
}
