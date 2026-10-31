using UnityEditor;
using UnityEngine;

// Blender 米制资产共享材质，模型无碰撞体；图标是同源模型的离线渲染。
public sealed class FishingAssetImport : AssetPostprocessor
{
    bool Ours => assetPath.StartsWith("Assets/Resources/HarborFishing/v001/");
    bool Photo => assetPath.StartsWith("Assets/Resources/HarborFishing/v002/");
    void OnPreprocessModel()
    {
        if(!Ours&&!Photo)return;
        var importer=(ModelImporter)assetImporter;
        importer.bakeAxisConversion=true;importer.globalScale=1;importer.useFileScale=true;importer.addCollider=false;
        importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;
        importer.isReadable=false;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
    }
    void OnPostprocessMaterial(Material material)
    {
        if(!Ours&&!Photo)return;
        var color=material.color;var texture=material.mainTexture;material.shader=Shader.Find("Universal Render Pipeline/Lit");
        material.SetColor("_BaseColor",Photo&&texture!=null?Color.white:color);
        if(texture!=null)material.SetTexture("_BaseMap",texture);
        material.SetFloat("_Smoothness",Photo ? .18f : .38f);
        material.SetFloat("_Metallic",Photo?0:material.name.Contains("铜")?.5f:.08f);
    }
    void OnPreprocessTexture()
    {
        if(!Ours&&!Photo)return;
        var importer=(TextureImporter)assetImporter;importer.textureType=TextureImporterType.Default;
        bool albedo=Photo&&assetPath.EndsWith("-albedo.png");
        if(albedo)importer.npotScale=TextureImporterNPOTScale.None;
        importer.alphaIsTransparency=!albedo;importer.mipmapEnabled=albedo;importer.maxTextureSize=albedo?1024:256;
        importer.textureCompression=albedo?TextureImporterCompression.CompressedHQ:TextureImporterCompression.Uncompressed;
        if(albedo){importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=4;}
    }
}
