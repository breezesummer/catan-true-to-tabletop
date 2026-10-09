using System;
using System.IO;
using Catan.Core.M5;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class M6Build
{
    public static void Build()
    {
        var session = CombinedGameSession.Create("heading-for-new-shores", 4, 20261009);
        if (session.GetPlayerView("P1").Board.Tiles.Length <= 19)
            throw new Exception("M6 core did not execute in Unity");
        var ai = new Catan.AI.CombinedAi();
        var command = ai.Decide(session.GetPlayerView(session.GetPlayerView("P1").ActivePlayerId));
        command.Id = Guid.NewGuid().ToString("N");
        if (!session.Execute(command).Success) throw new Exception("M6 AI policy failed in Unity");
        var restored = CombinedGameSession.Load(session.Save());
        if (restored.Save() != session.Save()) throw new Exception("Unity M6 save roundtrip failed");
        Debug.Log("CATAN_M6_UNITY_CORE_OK: netstandard2.1 execution and authoritative save roundtrip");

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
        new GameObject("Catan Local AI").AddComponent<M6App>();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), "Assets/M6.unity");
        PlayerSettings.companyName = "CatanDevelopment";
        PlayerSettings.productName = "Catan M6";
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
        PlayerSettings.defaultScreenWidth = 1440;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        var root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        var output = Path.Combine(root, ".local/m6/Builds/Windows/CatanM6.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/M6.unity" }, locationPathName = output,
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
        });
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new Exception("M6 Windows build failed");
        Debug.Log("CATAN_M6_UNITY_BUILD_OK");
    }
}
