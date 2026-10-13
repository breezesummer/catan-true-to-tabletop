using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Art-only importer: no rules, saves, PlayerView, map selection or collider changes.
public static class PlayerComponentBuild
{
    const string Runtime = "Assets/Resources/EntityAssets/v002/";
    const string Models = "Assets/Art/EntityAssets/v002/";
    [Serializable] public class Manifest { public string typeId, label; public Asset[] assets; }
    [Serializable] public class Asset { public string id,label,kind; public int triangles; public BoundsSpec expectedUnityBounds; public MaterialSpec[] materials; }
    [Serializable] public class BoundsSpec { public float[] min,max; }
    [Serializable] public class MaterialSpec { public string name,texture; public float[] baseColorLinearRgba; public float roughness; }
    [Serializable] public class Evidence { public string unityVersion; public ImportRecord[] assets; }
    [Serializable] public class Verification {public string status;}
    [Serializable] public class ImportRecord { public string id,typeId; public bool verified; public float[] dimensionsMetres; public int renderers,triangles; }
    static Vector3 Vector(float[] v) => new Vector3(v[0],v[1],v[2]);

    public static void BuildAndGame()
    {
        Build();
        M6Build.Build();
    }

    public static void Build()
    {
        var repo = Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
        var exports = Path.Combine(repo,"art/exports/entity-conversion-v002");
        var entries = new List<EntityAssetCatalog.Entry>();
        var evidence = new List<ImportRecord>();
        foreach (var directory in Directory.GetDirectories(exports).OrderBy(p=>p))
        {
            if (!File.Exists(Path.Combine(directory,"manifest.json")) || !File.Exists(Path.Combine(directory,"verification.json"))) continue;
            if (JsonUtility.FromJson<Verification>(File.ReadAllText(Path.Combine(directory,"verification.json"))).status!="passed") continue;
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(directory,"manifest.json")));
            var runtime = Runtime + manifest.typeId + "/";
            var models = Models + manifest.typeId + "/";
            Directory.CreateDirectory(runtime); Directory.CreateDirectory(models+"Materials");
            foreach (var texture in Directory.GetFiles(Path.Combine(directory,"textures"),"*.png"))
                File.Copy(texture,runtime+Path.GetFileName(texture),true);
            foreach (var asset in manifest.assets) File.Copy(Path.Combine(directory,asset.id+".fbx"),models+asset.id+".fbx",true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var path in Directory.GetFiles(runtime,"*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
                importer.textureType=TextureImporterType.Default; importer.sRGBTexture=true;
                importer.mipmapEnabled=true; importer.wrapMode=TextureWrapMode.Clamp;
                importer.filterMode=FilterMode.Trilinear; importer.maxTextureSize=2048;
                importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            foreach (var asset in manifest.assets)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(models+asset.id+".fbx");
                importer.globalScale=1; importer.useFileScale=true; importer.importAnimation=false;
                importer.addCollider=false; importer.isReadable=true; importer.SaveAndReimport();
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(models+asset.id+".fbx");
                if (source==null) throw new Exception("Missing imported FBX "+asset.id);
                var instance = new GameObject(asset.id);
                var visual = UnityEngine.Object.Instantiate(source,instance.transform,false);
                var renderers=instance.GetComponentsInChildren<Renderer>();
                if (renderers.Length==0) throw new Exception("No renderers "+asset.id);
                var bounds=renderers[0].bounds; foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                if ((bounds.min-Vector(asset.expectedUnityBounds.min)).magnitude>.000015f ||
                    (bounds.max-Vector(asset.expectedUnityBounds.max)).magnitude>.000015f)
                    throw new Exception(asset.id+" actual Unity bounds differ: "+bounds);
                foreach (var renderer in renderers)
                {
                    renderer.sharedMaterials=renderer.sharedMaterials.Select(original=>
                    {
                        if(original==null) throw new Exception("Null imported material "+asset.id);
                        var spec=asset.materials.Single(m=>m.name==original.name.Replace(" (Instance)",""));
                        var path=models+"Materials/"+spec.name+".mat";
                        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                        if(material==null) { material=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material,path); }
                        var c=spec.baseColorLinearRgba;
                        material.color=new Color(c[0],c[1],c[2],c[3]).gamma;
                        material.SetFloat("_Smoothness",1-spec.roughness); material.SetFloat("_Metallic",0);
                        material.mainTexture=string.IsNullOrEmpty(spec.texture)?null:AssetDatabase.LoadAssetAtPath<Texture2D>(runtime+spec.texture);
                        if(!string.IsNullOrEmpty(spec.texture)&&material.mainTexture==null) throw new Exception("Missing bound PNG "+spec.texture);
                        EditorUtility.SetDirty(material); return material;
                    }).ToArray();
                }
                // Keep FBX conversion under a neutral metre-scale root. No interaction colliders.
                var triangles=instance.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3);
                if(triangles!=asset.triangles) throw new Exception("Unity triangle mismatch "+asset.id);
                if(instance.GetComponentsInChildren<Collider>().Length!=0) throw new Exception("Unexpected collider "+asset.id);
                PrefabUtility.SaveAsPrefabAsset(instance,runtime+asset.id+".prefab");
                entries.Add(new EntityAssetCatalog.Entry { id=asset.id,typeId=manifest.typeId,typeLabel=manifest.label,
                    label=asset.label,kind=asset.kind,resource="EntityAssets/v002/"+manifest.typeId+"/"+asset.id,
                    width=bounds.size.x,depth=bounds.size.z,height=bounds.size.y });
                evidence.Add(new ImportRecord {id=asset.id,typeId=manifest.typeId,verified=true,
                    dimensionsMetres=new[]{bounds.size.x,bounds.size.y,bounds.size.z},renderers=renderers.Length,triangles=triangles});
                UnityEngine.Object.DestroyImmediate(instance);
                Debug.Log("CATAN_ENTITY_UNITY_IMPORT_OK "+manifest.typeId+" "+asset.id);
            }
        }
        if(entries.Count!=36 || entries.Select(e=>e.id).Distinct().Count()!=36) throw new Exception("Expected 36 unique new player component assets");
        File.WriteAllText(Runtime+"catalog.json",JsonUtility.ToJson(new EntityAssetCatalog {assets=entries.ToArray()},true));
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport); AssetDatabase.SaveAssets();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        GraphicsSettings.defaultRenderPipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/M1Pipeline.asset");
        QualitySettings.renderPipeline=GraphicsSettings.defaultRenderPipeline;
        var camera=new GameObject("Entity Art Camera").AddComponent<Camera>(); camera.tag="MainCamera";
        camera.nearClipPlane=.0001f; camera.farClipPlane=10;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.035f,.07f,.08f);
        camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        var light=new GameObject("Entity Art Light").AddComponent<Light>(); light.type=LightType.Directional;
        light.intensity=1.25f; light.shadows=LightShadows.Soft; light.transform.rotation=Quaternion.Euler(45,-30,0);
        light.gameObject.AddComponent<UniversalAdditionalLightData>();
        RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.58f,.60f,.64f);
        var gallery=new GameObject("Entity Art Gallery"); var browser=gallery.AddComponent<EntityAssetGallery>(); browser.CatalogResource="EntityAssets/v002/catalog"; browser.DisplayTitle="三色玩家组件 v002"; gallery.AddComponent<EntityAssetPlayerVerification>(); gallery.AddComponent<PlayerComponentVerification>();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),"Assets/PlayerComponents-v002.unity");
        var output=Path.Combine(repo,".local/art-pipeline/Builds/Windows/CatanPlayerComponents-v002.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{"Assets/PlayerComponents-v002.unity"},
            locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Entity gallery Windows build failed");
        var target=Path.Combine(repo,"docs/verification/entity-conversion-v002"); Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target,"unity-import.json"),JsonUtility.ToJson(new Evidence {unityVersion=Application.unityVersion,assets=evidence.ToArray()},true));
        Debug.Log("CATAN_PLAYER_COMPONENT_BUILD_OK assets="+entries.Count);
    }
}
