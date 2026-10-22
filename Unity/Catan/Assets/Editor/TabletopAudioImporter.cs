using System;
using UnityEditor;
using UnityEngine;

// 这批 WAV 已按混音预算整形；有损编解码会重建出超过源峰值的采样，必须保持 PCM。
public sealed class TabletopAudioImporter : AssetPostprocessor
{
    public override uint GetVersion() => 3;

    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/Resources/TabletopAudio/", StringComparison.Ordinal)) return;
        var importer = (AudioImporter)assetImporter;
        var settings = importer.defaultSampleSettings;
        settings.compressionFormat = AudioCompressionFormat.PCM;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        settings.preloadAudioData = true;
        importer.defaultSampleSettings = settings;
        importer.forceToMono = false;
        importer.loadInBackground = false;
        importer.ambisonic = false;
        // 两端共用无损设置；旧的移动端/桌面覆盖不能悄悄重新启用 Vorbis。
        importer.ClearSampleSettingOverride("Standalone");
        importer.ClearSampleSettingOverride("Android");
    }
}
