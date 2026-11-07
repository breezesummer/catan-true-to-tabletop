using UnityEditor;

public sealed class GeneralFrameTextureImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/Resources/GeneralUI/"))return;
        var importer=(TextureImporter)assetImporter;
        importer.textureType=TextureImporterType.Default;importer.alphaIsTransparency=true;
        importer.mipmapEnabled=false;importer.isReadable=false;importer.maxTextureSize=2048;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.wrapMode=UnityEngine.TextureWrapMode.Clamp;
        if(assetPath.StartsWith("Assets/Resources/GeneralUI/ZhangLiao/")||assetPath.StartsWith("Assets/Resources/GeneralUI/Dynamic/"))
        {
            importer.npotScale=TextureImporterNPOTScale.None;
            importer.maxTextureSize=4096;
            importer.textureCompression=TextureImporterCompression.Compressed;
        }
    }
}
