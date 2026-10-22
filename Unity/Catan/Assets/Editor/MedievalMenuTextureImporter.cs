using UnityEditor;
using UnityEngine;

// 版本化原画按二维背景导入；保留原始宽高比，不受实体卡牌贴图规则影响。
public sealed class MedievalMenuTextureImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(assetPath!="Assets/Resources/PastoralMenu/v002/medieval-harbor.png"&&!assetPath.StartsWith("Assets/Resources/PastoralMenu/v004/"))return;
        var importer=(TextureImporter)assetImporter;
        importer.textureType=TextureImporterType.Default;
        importer.mipmapEnabled=false;
        importer.npotScale=TextureImporterNPOTScale.None;
        importer.maxTextureSize=4096;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.wrapMode=TextureWrapMode.Clamp;
        importer.filterMode=FilterMode.Bilinear;
        importer.isReadable=false;
        importer.alphaIsTransparency=assetPath.EndsWith("/title.png");
    }
}
