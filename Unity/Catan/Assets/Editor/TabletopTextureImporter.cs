using UnityEditor;
using UnityEngine;

// 只配置本轮版本资源，旧贴图和照片来源保持原样。
public sealed class TabletopTextureImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(assetPath!="Assets/Resources/TabletopUI/v004/oak-albedo-8k.png")return;
        var importer=(TextureImporter)assetImporter;
        importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;
        importer.maxTextureSize=8192;importer.mipmapEnabled=true;
        importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=16;
        importer.wrapMode=TextureWrapMode.Repeat;
        importer.textureCompression=TextureImporterCompression.CompressedHQ;
        importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{
            name="Standalone",overridden=true,maxTextureSize=8192,format=TextureImporterFormat.BC7,compressionQuality=100
        });
    }
}
