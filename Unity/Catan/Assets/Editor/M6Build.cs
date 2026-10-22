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
        BuildForTarget(false);
    }

    public static void BuildAndroid()
    {
        try { BuildForTarget(true); }
        finally { AndroidBuildConfiguration.ClearSigningSecrets(); }
    }

    static void VerifyPresentationTextures()
    {
        var paths = new System.Collections.Generic.List<string> {
            "Assets/Resources/PastoralMenu/v002/medieval-harbor.png",
            "Assets/Resources/TabletopUI/v003/oak-table-albedo.png"
        };
        foreach (string directory in new[] {
            "Assets/Resources/TabletopUI/v008/NumberTokens",
            "Assets/Resources/EntityAssets/v005/tabletop-markers",
            "Assets/Resources/OperationUI/v001/pieces" })
            paths.AddRange(Directory.GetFiles(directory, "*.png"));
        foreach (string path in paths)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null || texture.width < 32 || texture.height < 32)
                throw new InvalidOperationException("关键界面贴图未正确导入，停止构建：" + path);
        }
        Debug.Log("CATAN_PRESENTATION_TEXTURES_OK: " + paths.Count + " 张主页、数字、港口及棋子贴图已导入。");
    }

    static void BuildForTarget(bool android)
    {
        if (android && !BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            throw new Exception("请安装当前 Unity 版本的 Android Build Support。");
        if (android) AndroidTexturePolicy.ApplyExistingTextures();
        if (!Array.Exists(Environment.GetCommandLineArgs(), argument => argument == "--skip-build-verification"))
        {
        EntityCardTextureImporter.VerifyImportedCards();
        VerifyPresentationTextures();
        AudioVisualBuildValidation.Verify();
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
        }

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/M1Pipeline.asset");
        if (pipeline == null) throw new Exception("URP pipeline missing");
        pipeline=android ? AndroidBuildConfiguration.ConfigurePipeline(pipeline) : TabletopFinishV005.Configure(pipeline);
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        QualitySettings.globalTextureMipmapLimit=0;
        QualitySettings.anisotropicFiltering=android ? AnisotropicFiltering.Enable : AnisotropicFiltering.ForceEnable;
        var camera = new GameObject("Board Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.nearClipPlane = .001f;
        camera.farClipPlane = 10;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.025f, .07f, .085f);
        camera.allowMSAA=true;
        camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        var light = new GameObject("Warm key").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.5f;
        light.transform.rotation = Quaternion.Euler(50, -35, 0);
        light.shadows = android ? LightShadows.Hard : LightShadows.Soft;
        light.gameObject.AddComponent<UniversalAdditionalLightData>();
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.55f, .61f, .68f);
        var app=new GameObject("Catan Local AI");
        app.AddComponent<M6App>();
        app.AddComponent<EntityIntegrationVerification>();
        app.AddComponent<PlayerComponentVerification>();
        app.AddComponent<TabletopMarkerVerification>();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), "Assets/M6.unity");
        PlayerSettings.companyName = "CatanDevelopment";
        PlayerSettings.productName = M6App.ProductFileName;
        PlayerSettings.bundleVersion = M6App.BuildVersion;
        // U0 构建后基线需要真实 CPU/GPU 时间；采样器仅在显式验证参数下运行。
        PlayerSettings.enableFrameTimingStats = true;
        // 在线大厅按用户要求直接连接公网 IP，正式包同样允许 HTTP / WS。
        PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
        PlayerSettings.defaultScreenWidth = 1440;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        if (android) AndroidBuildConfiguration.ConfigurePlayer();
        var root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        var args=Environment.GetCommandLineArgs();int outputFlag=Array.IndexOf(args,"--m6-output-root");
        var buildRoot=outputFlag>=0&&outputFlag+1<args.Length?Path.GetFullPath(args[outputFlag+1]):Path.Combine(root,".local/release-1.6");
        var output = Path.Combine(buildRoot, "Builds", android ? "Android" : "Windows", M6App.ProductFileName + (android ? ".apk" : ".exe"));
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/M6.unity" }, locationPathName = output,
            target = android ? BuildTarget.Android : BuildTarget.StandaloneWindows64,
            options = Array.IndexOf(args, "--ultra-development-build") >= 0 ? BuildOptions.Development : BuildOptions.None
        });
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new Exception(android ? "M6 Android build failed" : "M6 Windows build failed");
        if (android) AndroidBuildConfiguration.WriteBuildInfo(Path.GetDirectoryName(output));
        Debug.Log(android ? "CATAN_M6_ANDROID_BUILD_OK" : "CATAN_M6_UNITY_BUILD_OK");
    }
}
