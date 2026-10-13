using System;
using UnityEditor;
using UnityEngine;

// 卡牌照片按原始像素比例导入，避免宽高分别取整到二次幂后变成长条。
public sealed class EntityCardTextureImporter : AssetPostprocessor
{
    internal static bool IsCardTexture(string path)
    {
        if (path == "Assets/Resources/PhotoSamples/v001/wood-front.png" ||
            path == "Assets/Resources/PhotoSamples/v001/resource-back.png") return true;
        return path.StartsWith("Assets/Resources/EntityAssets/", StringComparison.Ordinal) &&
            path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
            (path.Contains("/resource-cards/") || path.Contains("/commodity-cards/") ||
             path.Contains("/progress-science/") || path.Contains("/progress-trade/") ||
             path.Contains("/progress-politics/"));
    }

    void OnPreprocessTexture()
    {
        if (IsCardTexture(assetPath))
            ((TextureImporter)assetImporter).npotScale = TextureImporterNPOTScale.None;
    }

    public static void VerifyImportedCards()
    {
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] {
            "Assets/Resources/PhotoSamples", "Assets/Resources/EntityAssets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!IsCardTexture(path)) continue;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null || importer.npotScale != TextureImporterNPOTScale.None ||
                width <= 0 || height <= 0 ||
                Math.Abs((double)texture.width / texture.height / ((double)width / height) - 1) > .001)
                throw new InvalidOperationException("卡牌导入宽高比与原图不一致：" + path);
            count++;
        }
        if (count == 0) throw new InvalidOperationException("未找到可验证的卡牌贴图。");
        Debug.Log("CATAN_CARD_TEXTURE_ASPECT_OK: " + count + " 张卡牌贴图保持原图宽高比。");
    }
}
