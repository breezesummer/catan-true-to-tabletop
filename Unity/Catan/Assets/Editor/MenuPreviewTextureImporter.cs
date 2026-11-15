using UnityEditor;
using UnityEngine;

// 地图示意按界面图片导入；不保留 CPU 副本，移动端使用压缩贴图。
public sealed class MenuPreviewTextureImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        bool accepted = assetPath.StartsWith("Assets/Resources/AcceptedMenu/");
        if (!accepted && !assetPath.StartsWith("Assets/Resources/MenuPreviews/v001/")) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = accepted ? 2048 : 1024;
        bool setupPlate = assetPath.StartsWith("Assets/Resources/AcceptedMenu/setup-");
        importer.textureCompression = setupPlate ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed;
        if (setupPlate) importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.isReadable = false;
    }
}
