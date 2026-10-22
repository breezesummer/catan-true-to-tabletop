using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// v005 独立管线与导入配方，原始图片和旧管线不变。
public static class TabletopFinishV005
{
    public static UniversalRenderPipelineAsset Configure(UniversalRenderPipelineAsset source)
    {
        const string pipelinePath = "Assets/TabletopPipeline-v005.asset";
        const string rendererPath = "Assets/TabletopRenderer-v005.asset";
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
        if (pipeline == null)
        {
            pipeline = Object.Instantiate(source);
            AssetDatabase.CreateAsset(pipeline, pipelinePath);
        }
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (renderer == null)
        {
            renderer = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/M1Renderer.asset"));
            AssetDatabase.CreateAsset(renderer, rendererPath);
        }
        var occlusion = renderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault();
        if (occlusion == null)
        {
            occlusion = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            occlusion.name = "米制实体接触阴影 v005";
            AssetDatabase.AddObjectToAsset(occlusion, renderer);
            renderer.rendererFeatures.Add(occlusion);
        }
        var ao = new SerializedObject(occlusion);
        ao.FindProperty("m_Settings.Intensity").floatValue = 1.1f;
        ao.FindProperty("m_Settings.Radius").floatValue = .007f;
        ao.FindProperty("m_Settings.DirectLightingStrength").floatValue = .16f;
        ao.FindProperty("m_Settings.Downsample").boolValue = false;
        ao.FindProperty("m_Settings.AfterOpaque").boolValue = false;
        ao.FindProperty("m_Settings.Falloff").floatValue = 4;
        ao.ApplyModifiedPropertiesWithoutUndo();
        occlusion.SetActive(true);
        renderer.SetDirty();
        var data = new SerializedObject(pipeline);
        data.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = renderer;
        data.FindProperty("m_SoftShadowsSupported").boolValue = true;
        data.FindProperty("m_MainLightShadowsSupported").boolValue = true;
        data.ApplyModifiedPropertiesWithoutUndo();
        pipeline.renderScale = 1;
        pipeline.msaaSampleCount = 4;
        pipeline.mainLightShadowmapResolution = 4096;
        // 桌面相机离目标 2 米；不能在棋盘所在位置恰好截断阴影。
        pipeline.shadowDistance = 4;
        pipeline.shadowDepthBias = .08f;
        pipeline.shadowNormalBias = .08f;
        pipeline.shadowCascadeCount = 1;
        EditorUtility.SetDirty(occlusion);
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(pipeline);
        AssetDatabase.SaveAssets();
        return pipeline;
    }
}

public sealed class TabletopFinishTextureImporterV005 : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/TabletopUI/v005/")) return;
        bool normal = assetPath.EndsWith("-normal.png");
        var importer = (TextureImporter)assetImporter;
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = !normal;
        importer.maxTextureSize = 2048;
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 16;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings {
            name = "Standalone", overridden = true, maxTextureSize = 2048,
            format = normal ? TextureImporterFormat.BC5 : TextureImporterFormat.BC7, compressionQuality = 100
        });
    }
}
