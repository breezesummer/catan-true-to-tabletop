using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Catan.EnvironmentProbe;

public static class EnvironmentSmoke
{
    public static void Build()
    {
        if (Probe.RoundTrip("卡坦岛环境验证") != "卡坦岛环境验证") throw new Exception(".NET Standard probe failed in Unity.");
        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/environment-cube-v001.fbx");
        if (model == null) throw new Exception("FBX did not import.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var sample = UnityEngine.Object.Instantiate(model);
        var bounds = sample.GetComponentInChildren<MeshRenderer>().bounds;
        if (Vector3.Distance(bounds.size, new Vector3(0.08f, 0.01f, 0.08f)) > 0.0001f || Mathf.Abs(bounds.min.y) > 0.0001f)
            throw new Exception("FBX dimensions or bottom pivot incorrect: " + bounds);
        Debug.Log("CATAN_ENVIRONMENT_UNITY_IMPORT_OK: " + bounds);
        // Preserve the renderer asset referenced by an active pipeline on repeat runs.
        // Replacing it first briefly leaves the existing pipeline without a renderer.
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/EnvironmentRenderer.asset");
        if (renderer == null)
        {
            renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, "Assets/EnvironmentRenderer.asset");
        }
        var pipeline = UniversalRenderPipelineAsset.Create(renderer);
        AssetDatabase.CreateAsset(pipeline, "Assets/EnvironmentPipeline.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        var camera = new GameObject("Camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(0.13f, 0.13f, -0.13f);
        camera.transform.LookAt(new Vector3(0, 0.005f, 0));
        camera.nearClipPlane = 0.001f;
        camera.farClipPlane = 10;
        camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        var light = new GameObject("Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(50, -30, 0);
        light.gameObject.AddComponent<UniversalAdditionalLightData>();
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = new Color(0.25f, 0.6f, 0.35f);
        AssetDatabase.CreateAsset(material, "Assets/EnvironmentMaterial.mat");
        sample.GetComponentInChildren<MeshRenderer>().sharedMaterial = material;
        new GameObject("Runtime validation").AddComponent<SmokeRuntime>();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), "Assets/EnvironmentSmoke.unity");
        PlayerSettings.companyName = "CatanDevelopment";
        PlayerSettings.productName = "Catan Environment Smoke";
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
        PlayerSettings.defaultScreenWidth = 800;
        PlayerSettings.defaultScreenHeight = 600;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.runInBackground = true;
        var destination = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Builds/Windows/EnvironmentSmoke.exe"));
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] {"Assets/EnvironmentSmoke.unity"}, locationPathName = destination,
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
        });
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new Exception("Windows build failed: " + report.summary.result);
        Debug.Log("CATAN_ENVIRONMENT_UNITY_BUILD_OK");
    }
}
