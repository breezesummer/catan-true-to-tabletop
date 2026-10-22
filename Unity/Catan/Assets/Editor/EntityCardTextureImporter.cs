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
            VerifyScreenAspect((float)width/height,path);
            count++;
        }
        if (count == 0) throw new InvalidOperationException("未找到可验证的卡牌贴图。");
        Debug.Log("CATAN_CARD_TEXTURE_ASPECT_OK: " + count + " 张卡牌贴图保持原图宽高比。");
        VerifyScreenAspect(108f/151f,"发展卡文字卡面");
        Debug.Log("CATAN_CARD_SCREEN_ASPECT_OK: 卡面在五种窗口比例和手牌／预览／出牌区域中保持屏幕像素比例。");
    }

    static void VerifyScreenAspect(float aspect,string label)
    {
        foreach(var size in new[]{new Vector2(1440,900),new Vector2(1920,1080),new Vector2(2560,1080),new Vector2(1024,768),new Vector2(2340,1080)})
        foreach(var area in new[]{new Rect(100,734,108,151),new Rect(387,178,294,435),new Rect(610,336,220,326)})
        {
            var matrix=Matrix4x4.Scale(new Vector3(size.x/1440f,size.y/900f,1));
            var rect=EntityCardAssets.FitCardRect(area,aspect,matrix);
            float screenAspect=rect.width*size.x/1440f/(rect.height*size.y/900f);
            if(Math.Abs(screenAspect/aspect-1)>.001 || rect.width<=0 || rect.height<=0 ||
                rect.xMin<area.xMin-.001f || rect.xMax>area.xMax+.001f ||
                rect.yMin<area.yMin-.001f || rect.yMax>area.yMax+.001f)
                throw new InvalidOperationException("卡面屏幕比例或完整显示检查失败："+label+"，窗口 "+size);
        }
    }
}
