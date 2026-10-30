using UnityEditor;
using UnityEngine;

// Blender 米制资产共享材质，模型无碰撞体；图标是同源模型的离线渲染。
public sealed class FishingAssetImport : AssetPostprocessor
{
    bool Ours => assetPath.StartsWith("Assets/Resources/HarborFishing/v001/");
    void OnPreprocessModel()
    {
        if(!Ours)return;
        var importer=(ModelImporter)assetImporter;
        importer.bakeAxisConversion=true;importer.globalScale=1;importer.useFileScale=true;importer.addCollider=false;
        importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;
        importer.isReadable=false;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
    }
    void OnPostprocessMaterial(Material material)
    {
        if(!Ours)return;
        var color=material.color;material.shader=Shader.Find("Universal Render Pipeline/Lit");
        material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.38f);
        material.SetFloat("_Metallic",material.name.Contains("铜")?.5f:.08f);
    }
    void OnPreprocessTexture()
    {
        if(!Ours)return;
        var importer=(TextureImporter)assetImporter;importer.textureType=TextureImporterType.Default;
        importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=256;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
    }
}
