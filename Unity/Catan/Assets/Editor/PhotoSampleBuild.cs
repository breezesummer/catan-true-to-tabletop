using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class PhotoSampleBuild
{
    const string Runtime="Assets/Resources/PhotoSamples/v001/";
    const string Models="Assets/Art/PhotoSamples/v001/";
    [Serializable] public class Manifest {public Asset[] assets;}
    [Serializable] public class Asset {public string id;public BoundsSpec expectedUnityBounds;public MaterialSpec[] materials;}
    [Serializable] public class BoundsSpec {public float[] min,max;}
    [Serializable] public class MaterialSpec {public string name,texture;public float[] baseColorLinearRgba;public float roughness;}
    static Vector3 Vector(float[] v)=>new Vector3(v[0],v[1],v[2]);
    public static void Build()
    {
        var repo=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
        var export=Path.Combine(repo,"art/exports/photo-samples-v001");
        var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(export,"manifest.json")));
        Directory.CreateDirectory(Runtime);Directory.CreateDirectory(Models+"Materials");
        foreach(var texture in Directory.GetFiles(Path.Combine(export,"textures"),"*.png"))
            File.Copy(texture,Runtime+Path.GetFileName(texture),true);
        foreach(var asset in manifest.assets)File.Copy(Path.Combine(export,asset.id+".fbx"),Models+asset.id+".fbx",true);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(var filename in new[]{"wood-front","resource-back","forest-top"})
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(Runtime+filename+".png");
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;
            importer.alphaIsTransparency=filename=="forest-top";importer.mipmapEnabled=true;
            importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Trilinear;
            importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        foreach(var asset in manifest.assets)
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath(Models+asset.id+".fbx");
            importer.globalScale=1;importer.useFileScale=true;importer.importAnimation=false;
            importer.addCollider=false;importer.isReadable=true;importer.SaveAndReimport();
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Models+asset.id+".fbx");
            var instance=new GameObject(asset.id);
            // FBX single-mesh roots carry Blender-to-Unity conversion. Preserve it beneath a neutral prefab root.
            var visual=UnityEngine.Object.Instantiate(source,instance.transform,false);
            // Face the printed artwork and village entrance toward the shipped game camera.
            if(asset.id!="road")visual.transform.Rotate(Vector3.up,180,Space.World);
            var renderers=instance.GetComponentsInChildren<Renderer>();
            var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            if((bounds.min-Vector(asset.expectedUnityBounds.min)).magnitude>.00001f || (bounds.max-Vector(asset.expectedUnityBounds.max)).magnitude>.00001f)
                throw new Exception(asset.id+" Unity bounds differ: "+bounds);
            foreach(var r in renderers)
            {
                var converted=r.sharedMaterials.Select(original=>
                {
                    var spec=asset.materials.Single(m=>m.name==original.name.Replace(" (Instance)",""));
                    var path=Models+"Materials/"+spec.name+".mat";
                    var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
                    var c=spec.baseColorLinearRgba;
                    material.color=new Color(c[0],c[1],c[2],c[3]).gamma;
                    material.SetFloat("_Smoothness",1-spec.roughness);material.SetFloat("_Metallic",0);
                    material.mainTexture=string.IsNullOrEmpty(spec.texture)?null:AssetDatabase.LoadAssetAtPath<Texture2D>(Runtime+spec.texture);
                    if(!string.IsNullOrEmpty(spec.texture)&&material.mainTexture==null)throw new Exception("Missing texture "+spec.texture);
                    EditorUtility.SetDirty(material);return material;
                }).ToArray();
                r.sharedMaterials=converted;
            }
            if(asset.id=="road"||asset.id=="village")
            {
                // Many sculpted details share one plastic material; batch them into a single runtime renderer.
                var plastic=renderers[0].sharedMaterial;
                var combined=new Mesh{name=asset.id+"-combined"};
                combined.CombineMeshes(instance.GetComponentsInChildren<MeshFilter>().Select(f=>new CombineInstance{
                    mesh=f.sharedMesh,transform=instance.transform.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);
                var meshPath=Models+asset.id+"-combined.asset";
                var stored=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if(stored==null){AssetDatabase.CreateAsset(combined,meshPath);stored=combined;}
                else{EditorUtility.CopySerialized(combined,stored);UnityEngine.Object.DestroyImmediate(combined);EditorUtility.SetDirty(stored);}
                UnityEngine.Object.DestroyImmediate(visual);
                var part=new GameObject("Sculpted visual");part.transform.SetParent(instance.transform,false);
                part.AddComponent<MeshFilter>().sharedMesh=stored;part.AddComponent<MeshRenderer>().sharedMaterial=plastic;
            }
            // Picking stays with the stable logical map IDs, independent of render meshes.
            PrefabUtility.SaveAsPrefabAsset(instance,Runtime+asset.id+".prefab");UnityEngine.Object.DestroyImmediate(instance);
            Debug.Log("CATAN_PHOTO_UNITY_IMPORT_OK "+asset.id+" "+bounds);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        GraphicsSettings.defaultRenderPipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/M1Pipeline.asset");
        QualitySettings.renderPipeline=GraphicsSettings.defaultRenderPipeline;
        var camera=new GameObject("Sample Camera").AddComponent<Camera>();camera.tag="MainCamera";
        camera.nearClipPlane=.001f;camera.farClipPlane=10;camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.025f,.07f,.085f);camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        var light=new GameObject("Sample Light").AddComponent<Light>();light.type=LightType.Directional;
        light.intensity=1.2f;light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(50,-35,0);
        light.gameObject.AddComponent<UniversalAdditionalLightData>();
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.5f,.55f,.6f);
        new GameObject("Photo sample gallery").AddComponent<PhotoSampleGallery>();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),"Assets/PhotoSamples-v001.unity");
        var output=Path.Combine(repo,".local/art-photo-samples/Builds/Windows/CatanArtSamples-v001.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/PhotoSamples-v001.unity","Assets/M6.unity"},locationPathName=output,
            target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Art sample Windows build failed");
        Debug.Log("CATAN_PHOTO_UNITY_BUILD_OK");
    }
}
