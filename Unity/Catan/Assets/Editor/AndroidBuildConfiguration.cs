using System;
using System.IO;
using System.Security.Cryptography;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 同一工程、同一 M6 场景。移动端只覆盖构建和画质，规则与 AI 程序集保持共用。
public static class AndroidBuildConfiguration
{
    public const string PackageId = "com.catandevelopment.threekingdoms";
    public static int VersionCode
    {
        get{var version=Version.Parse(M6App.BuildVersion);return version.Major*10000+version.Minor*100+Math.Max(0,version.Build);}
    }

    public static UniversalRenderPipelineAsset ConfigurePipeline(UniversalRenderPipelineAsset source)
    {
        const string pipelinePath = "Assets/TabletopPipeline-Android-v001.asset";
        const string rendererPath = "Assets/TabletopRenderer-Android-v001.asset";
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
        if (pipeline == null)
        {
            pipeline = UnityEngine.Object.Instantiate(source);
            AssetDatabase.CreateAsset(pipeline, pipelinePath);
        }
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (renderer == null)
        {
            renderer = UnityEngine.Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/M1Renderer.asset"));
            AssetDatabase.CreateAsset(renderer, rendererPath);
        }
        // 移动版不启用屏幕空间 AO；桌面 4K 阴影和 4 倍抗锯齿配置独立保留。
        foreach (var feature in renderer.rendererFeatures) feature.SetActive(false);
        renderer.SetDirty();
        var data = new SerializedObject(pipeline);
        data.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = renderer;
        data.FindProperty("m_SoftShadowsSupported").boolValue = false;
        data.FindProperty("m_MainLightShadowsSupported").boolValue = true;
        data.ApplyModifiedPropertiesWithoutUndo();
        pipeline.supportsHDR = false;
        pipeline.renderScale = 1;
        pipeline.msaaSampleCount = 2;
        pipeline.mainLightShadowmapResolution = 1024;
        pipeline.shadowDistance = 4;
        pipeline.shadowDepthBias = .08f;
        pipeline.shadowNormalBias = .08f;
        pipeline.shadowCascadeCount = 1;
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(pipeline);
        AssetDatabase.SaveAssets();
        return pipeline;
    }

    public static void ConfigurePlayer()
    {
        string keystore = Environment.GetEnvironmentVariable("CATAN_ANDROID_KEYSTORE");
        string password = Environment.GetEnvironmentVariable("CATAN_ANDROID_KEYSTORE_PASS");
        if (string.IsNullOrEmpty(keystore) || !File.Exists(keystore) || string.IsNullOrEmpty(password))
            throw new InvalidOperationException("Android 签名未准备，请通过 tools/development/Build-Android.ps1 构建。");
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Standard);
        PlayerSettings.productName = M6App.ProductFileName;
        // 跨程序集规则、AI 与存档入口使用保守的最小裁剪级别。
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Minimal);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "--m6-android-arm64-only") < 0)
            PlayerSettings.Android.targetArchitectures |= AndroidArchitecture.X86_64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel36;
        PlayerSettings.Android.bundleVersionCode = VersionCode;
        PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
        PlayerSettings.Android.optimizedFramePacing = true;
        PlayerSettings.Android.renderOutsideSafeArea = true;
        PlayerSettings.Android.forceInternetPermission = true;
        PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.use32BitDisplayBuffer = true;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        // ETC2 与 OpenGL ES 3.0 可覆盖普通 Android 手机和本机模拟器。
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = keystore;
        PlayerSettings.Android.keyaliasName = "catan-internal";
        PlayerSettings.Android.keystorePass = password;
        PlayerSettings.Android.keyaliasPass = password;
    }

    public static void ClearSigningSecrets()
    {
        PlayerSettings.Android.keystorePass = "";
        PlayerSettings.Android.keyaliasPass = "";
        // 只在进程中传入本机路径；不将其写入受版本控制的项目设置。
        PlayerSettings.Android.keystoreName = "";
    }

    public static void WriteBuildInfo(string outputDirectory)
    {
        var info = new AndroidBuildInfo {
            version = PlayerSettings.bundleVersion,
            packageId = PackageId,
            versionCode = VersionCode,
            minApi = 26,
            targetApi = 36,
            architectures = (PlayerSettings.Android.targetArchitectures & AndroidArchitecture.X86_64) != 0
                ? new[] { "arm64-v8a", "x86_64" } : new[] { "arm64-v8a" },
            graphicsApi = "OpenGLES3",
            scriptingBackend = "IL2CPP",
            ui = "shared-pc-tabletop",
            coreAssemblySha256 = AssemblyHash("Catan.Core.dll"),
            aiAssemblySha256 = AssemblyHash("Catan.AI.dll")
        };
        File.WriteAllText(Path.Combine(outputDirectory, "build-info.json"), JsonUtility.ToJson(info, true));
    }

    static string AssemblyHash(string fileName)
    {
        using (var input = File.OpenRead(Path.Combine(Application.dataPath, "Plugins", fileName)))
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
    }

    [Serializable]
    sealed class AndroidBuildInfo
    {
        public string version;
        public string packageId;
        public int versionCode;
        public int minApi;
        public int targetApi;
        public string[] architectures;
        public string graphicsApi;
        public string scriptingBackend;
        public string ui;
        public string coreAssemblySha256;
        public string aiAssemblySha256;
    }
}

// Unity 的 HTTP 选项只控制引擎请求；Android 平台清单也须允许公网 IP 的明文连接。
public sealed class OnlineLobbyAndroidManifest : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 1000;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string manifest=Path.Combine(path,"src/main/AndroidManifest.xml");
        var document=XDocument.Load(manifest,LoadOptions.PreserveWhitespace);
        var application=document.Root?.Element("application");
        if(application==null)throw new InvalidOperationException("Android 应用清单缺少 application，停止构建。");
        XNamespace android="http://schemas.android.com/apk/res/android";
        application.SetAttributeValue(android+"usesCleartextTraffic","true");
        document.Save(manifest);
        Debug.Log("CATAN_ONLINE_HTTP_MANIFEST_OK: Android 正式包允许 HTTP / WS 在线大厅。");
    }
}
