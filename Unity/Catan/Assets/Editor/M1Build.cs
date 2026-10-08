using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Catan.Core;

public static class M1Build
{
    [Serializable] public sealed class ArtManifest {public ArtMaterial[] materials;}
    [Serializable] public sealed class ArtMaterial {public string name;public float[] baseColorLinearRgba;public float roughness;public float metallic;}
    public static void Build()
    {
        var scenario=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/m1-scenario.json");
        var session=GameSession.Create(scenario.text);
        if(session.GetPlayerView("P1").Board.Vertices.Length!=54)throw new Exception("Core did not execute in Unity");
        var sample=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/mountain-tile-v001.fbx");
        if(sample==null)throw new Exception("Mountain FBX missing");
        var model=UnityEngine.Object.Instantiate(sample);
        var manifest=JsonUtility.FromJson<ArtManifest>(File.ReadAllText("Assets/Models/mountain-manifest.json"));
        var renderers=model.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
        foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        if(Mathf.Abs(bounds.min.y)>.0001f||bounds.max.y>.0361f||Mathf.Abs(Mathf.Max(bounds.size.x,bounds.size.z)-.08f)>.001f)
            throw new Exception("Mountain scale/pivot mismatch "+bounds);
        foreach(var r in renderers)
        {
            var original=r.sharedMaterials;var converted=new Material[original.Length];
            for(int i=0;i<original.Length;i++)
            {
                string name=original[i].name.Replace(" (Instance)","");string path="Assets/ArtMaterials/"+name+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(mat==null){mat=BoardRenderer.Material(original[i].color);AssetDatabase.CreateAsset(mat,path);}
                var spec=manifest.materials.Single(m=>m.name==name);var c=spec.baseColorLinearRgba;
                mat.color=new Color(c[0],c[1],c[2],c[3]).gamma;mat.SetFloat("_Metallic",spec.metallic);mat.SetFloat("_Smoothness",1-spec.roughness);EditorUtility.SetDirty(mat);
                converted[i]=mat;
            }
            r.sharedMaterials=converted;
        }
        PrefabUtility.SaveAsPrefabAsset(model,"Assets/Resources/mountain-tile-v001.prefab");UnityEngine.Object.DestroyImmediate(model);
        Debug.Log("CATAN_M1_UNITY_IMPORT_OK: netstandard2.1 execution + FBX bounds "+bounds);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/M1Renderer.asset");
        if(renderer==null){renderer=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(renderer,"Assets/M1Renderer.asset");}
        var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/M1Pipeline.asset");
        if(pipeline==null){pipeline=UniversalRenderPipelineAsset.Create(renderer);AssetDatabase.CreateAsset(pipeline,"Assets/M1Pipeline.asset");}
        pipeline.msaaSampleCount=4; pipeline.shadowDistance=2;
        GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
        var camera=new GameObject("Board Camera").AddComponent<Camera>();camera.tag="MainCamera";
        camera.nearClipPlane=.001f;camera.farClipPlane=10;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.07f,.085f);
        camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        var light=new GameObject("Warm key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;
        light.transform.rotation=Quaternion.Euler(50,-35,0);light.shadows=LightShadows.Soft;light.gameObject.AddComponent<UniversalAdditionalLightData>();
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.61f,.68f);
        new GameObject("Catan local slice").AddComponent<CatanApp>();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),"Assets/M1.unity");
        PlayerSettings.companyName="CatanDevelopment";PlayerSettings.productName="Catan M1";
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone,ApiCompatibilityLevel.NET_Standard);
        PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
        PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=true;
        PlayerSettings.colorSpace=ColorSpace.Linear;
        var root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
        var output=Path.Combine(root,".local/m1/Builds/Windows/CatanM1.exe");Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/M1.unity"},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("M1 Windows build failed");
        Debug.Log("CATAN_M1_UNITY_BUILD_OK");
    }
}
