using System;
using System.IO;
using Catan.Core.M4;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class M4Build
{
    public static void Build()
    {
        var scenario = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/m1-scenario.json");
        var session = CitiesKnightsGameSession.Create(scenario.text, 4, 20261009);
        if (session.GetPlayerView("P1").Board.Vertices.Length != 54)
            throw new Exception("M4 core did not execute in Unity");
        var restored = CitiesKnightsGameSession.Load(scenario.text, session.Save());
        if (restored.Save() != session.Save()) throw new Exception("Unity M4 save roundtrip failed");
        Debug.Log("CATAN_M4_UNITY_CORE_OK: netstandard2.1 execution and authoritative save roundtrip");

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/M1Pipeline.asset");
        if (pipeline == null) throw new Exception("URP pipeline missing");
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        var camera = new GameObject("Board Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.nearClipPlane = .001f;
        camera.farClipPlane = 10;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.025f, .07f, .085f);
        camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        var light = new GameObject("Warm key").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.5f;
        light.transform.rotation = Quaternion.Euler(50, -35, 0);
        light.shadows = LightShadows.Soft;
        light.gameObject.AddComponent<UniversalAdditionalLightData>();
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.55f, .61f, .68f);
        new GameObject("Catan Cities and Knights").AddComponent<M4App>();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), "Assets/M4.unity");
        PlayerSettings.companyName = "CatanDevelopment";
        PlayerSettings.productName = "Catan M4";
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
        PlayerSettings.defaultScreenWidth = 1440;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        var root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        var output = Path.Combine(root, ".local/m4/Builds/Windows/CatanM4.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/M4.unity" }, locationPathName = output,
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
        });
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new Exception("M4 Windows build failed");
        Debug.Log("CATAN_M4_UNITY_BUILD_OK");
    }
}
