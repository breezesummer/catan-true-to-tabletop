using UnityEditor;
using UnityEngine;

// 显式锁定二维纸牌贴图；不能让缺失 textureShape 的简化 meta 被解释成立方体纹理。
public sealed class TabletopNumberTextureImporterV008 : AssetPostprocessor
{
    public override uint GetVersion(){return 1;}
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/Resources/TabletopUI/v008/NumberTokens/"))return;
        var importer=(TextureImporter)assetImporter;
        importer.textureType=TextureImporterType.Default;importer.textureShape=TextureImporterShape.Texture2D;
        importer.sRGBTexture=true;importer.maxTextureSize=512;importer.mipmapEnabled=true;
        importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=8;importer.wrapMode=TextureWrapMode.Clamp;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
    }
}
