using System;
using UnityEditor;
using UnityEngine;

// 在两端打包前核验整套音画资源，避免缺图或语音漏打包后静默回退。
public static class AudioVisualBuildValidation
{
    public static void Verify()
    {
        for (int theme = 0; theme < PastoralMenuScene.ThemeCount; theme++)
            Texture(PastoralMenuScene.Preview(theme), "主页主题 " + theme);
        string[] scenarios = { "heading-for-new-shores", "four-islands", "fog-islands", "through-the-desert", "forgotten-tribe", "cloth-for-catan", "pirate-islands", "wonders-of-catan", "new-world" };
        int count = 0;
        for (int rule = 0; rule < 4; rule++)
        {
            Texture(MenuMapPreviews.RuleTexture(rule), "玩法封面 " + rule);
            var ids = rule == 0 || rule == 2 ? new[] { "standard" } : rule == 3 ? new[] { scenarios[0], scenarios[3] } : scenarios;
            foreach (string id in ids) foreach (int players in new[] { 3, 4 })
            { Texture(MenuMapPreviews.ScenarioTexture(rule, id, players), rule + "/" + id + "/" + players); count++; }
        }
        const string audioRoot = "Assets/Resources/TabletopAudio/v003/";
        foreach (TabletopAudioCue cue in Enum.GetValues(typeof(TabletopAudioCue))) Audio(audioRoot + "Effects/" + cue + ".wav");
        for (int theme = 0; theme < 5; theme++) Audio(audioRoot + "Ambience/Theme" + theme + ".wav");
        Audio(audioRoot + "Music/SeaBreezeHome.wav");
        foreach (string language in new[] { "zh", "en" }) foreach (string action in new[] { "KnightRecruit", "KnightActivate" })
        for (int variant = 1; variant <= 2; variant++) Audio(audioRoot + "Voices/" + language + "/" + action + variant.ToString("D2") + ".wav");
        Texture(Resources.Load<Texture2D>("PastoralMenu/v004/title"),"独立菜单标题");
        Debug.Log("CATAN_AUDIO_VISUAL_TEXTURES_OK: " + count + " 张地图、4 张玩法封面与"+PastoralMenuScene.ThemeCount+"张主页原画、独立标题完整。");
    }
    static void Texture(Texture2D value, string name)
    {
        if (value == null || value.width < 256 || value.height < 128)
            throw new InvalidOperationException("音画资源缺失或尺寸不符：" + name);
    }
    static void Audio(string path)
    {
        var value = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (value == null || value.samples <= 0 || value.frequency < 16000)
            throw new InvalidOperationException("音频资源未正确导入：" + path);
    }
}
