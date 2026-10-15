using System;
using UnityEditor;
using UnityEngine;

// 仅设置 Android 覆盖；源照片和 Standalone 的 BC7/BC5 设置不变。
public sealed class AndroidTexturePolicy : AssetPostprocessor
{
    public override int GetPostprocessOrder() { return 1000; }

    void OnPreprocessTexture()
    {
        if (IsGameTexture(assetPath)) Configure((TextureImporter)assetImporter, assetPath);
    }

    static bool IsGameTexture(string path)
    {
        return path.StartsWith("Assets/Resources/", StringComparison.Ordinal);
    }

    static void Configure(TextureImporter importer, string path)
    {
        var settings = importer.GetPlatformTextureSettings("Android");
        settings.name = "Android";
        settings.overridden = true;
        settings.maxTextureSize = MaxSize(path);
        settings.format = TextureImporterFormat.ETC2_RGBA8;
        settings.compressionQuality = 50;
        settings.crunchedCompression = false;
        importer.SetPlatformTextureSettings(settings);
    }

    static int MaxSize(string path)
    {
        if (EntityCardTextureImporter.IsCardTexture(path)) return 2048;
        if (path.Contains("/NumberTokens/")) return 256;
        if (path.Contains("oak-albedo")) return 2048;
        return 1024;
    }

    public static void ApplyExistingTextures()
    {
        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            var settings = importer.GetPlatformTextureSettings("Android");
            if (settings.overridden && settings.maxTextureSize == MaxSize(path) &&
                settings.format == TextureImporterFormat.ETC2_RGBA8 && !settings.crunchedCompression) continue;
            Configure(importer, path);
            importer.SaveAndReimport();
            changed++;
        }
        Debug.Log("CATAN_ANDROID_TEXTURE_POLICY_OK: " + changed + " 张贴图更新 Android ETC2 覆盖。");
    }
}
