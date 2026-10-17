using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

// 显式开发参数启用；只读公开菜单素材，临时设置在成功或失败退出时恢复。
public sealed class AudioVisualVerification : MonoBehaviour
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    M6App app;
    string output, stage = "启动";
    int checks;
    bool capturesEnabled;
    readonly List<string> captures = new List<string>();
    readonly List<int> animatedPixels = new List<int>();
    readonly Dictionary<string, object> audioPreferences = new Dictionary<string, object>();
    int originalTheme;
    bool originalMotion, preferencesCaptured;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "--audio-visual-verify") >= 0)
            new GameObject("音画资源专项验证").AddComponent<AudioVisualVerification>();
    }

    IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();
        int flag = Array.IndexOf(args, "--audio-visual-verify");
        try
        {
            if (flag + 1 >= args.Length) throw new InvalidDataException("缺少独立输出目录");
            output = Path.GetFullPath(args[flag + 1]);
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../..", ".local")) + Path.DirectorySeparatorChar;
            if (!output.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("验收输出须位于项目.local");
            Directory.CreateDirectory(output);
            capturesEnabled = Array.IndexOf(args, "--audio-visual-no-capture") < 0;
            app = FindFirstObjectByType<M6App>();
            Require(app != null && TabletopAudio.Instance != null, "游戏与声音组件未初始化");
        }
        catch (Exception e) { Fail(e); yield break; }
        var stack = new Stack<IEnumerator>();
        stack.Push(Verify());
        while (stack.Count > 0)
        {
            object next = null; bool moved = false; Exception error = null;
            try { moved = stack.Peek().MoveNext(); if (moved) next = stack.Peek().Current; }
            catch (Exception e) { error = e; }
            if (error != null) { Fail(error); yield break; }
            if (!moved) { stack.Pop(); continue; }
            if (next is IEnumerator nested) { stack.Push(nested); continue; }
            yield return next;
        }
        RestorePreferences();
        File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(new Result {
            status = "passed", version = M6App.BuildVersion, checks = checks, captures = captures.ToArray(),
            animatedPixels = animatedPixels.ToArray(), fullUiPixelsCaptured = capturesEnabled,
            userSaveTouched = false, humanAudioListening = false,
            limitations = "自动运行与采样校验不等于真人听感、安卓真机或长时间性能验收。"
        }, true));
        Debug.Log("CATAN_AUDIO_VISUAL_OK: checks=" + checks + " captures=" + captures.Count);
        Application.Quit(0);
    }

    IEnumerator Verify()
    {
        while (!UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
        yield return null;
        var audio = TabletopAudio.Instance;
        Require(audio.Ready, "音频未就绪");
        foreach (string name in new[] { "MasterVolume", "EffectsVolume", "AmbientVolume", "MusicVolume", "VoiceVolume", "VoiceLanguage", "Muted" })
        {
            var p = typeof(TabletopAudio).GetProperty(name);
            Require(p != null, "缺少声音设置 " + name);
            audioPreferences[name] = p.GetValue(audio);
        }
        originalTheme = PastoralMenuScene.Selected;
        originalMotion = PastoralMenuScene.Animated;
        preferencesCaptured = true;
        audio.Muted = true;
        stage = "音频片段";
        foreach (TabletopAudioCue cue in Enum.GetValues(typeof(TabletopAudioCue))) CheckClip(audio.GetClip(cue), cue.ToString());
        CheckClip(audio.GetMusicClip(), "背景音乐");
        Require(audio.GetMusicClip().length > 45, "完整乐句时长不足");
        Require(audio.GetComponents<AudioSource>().Any(s => s.clip == audio.GetMusicClip() && s.loop), "背景音乐没有开启循环");
        Require(audio.VoiceClipCount == 8, "中英语音不齐全");
        for (int theme = 0; theme < 5; theme++) CheckClip(audio.GetAmbienceClip(theme), "主题环境 " + theme);
        for (int language = 0; language < 2; language++)
        foreach (var cue in new[] { TabletopAudioCue.KnightRecruit, TabletopAudioCue.KnightActivate })
        for (int variant = 0; variant < 2; variant++) CheckClip(audio.GetVoiceClip(language, cue, variant), "骑士语音 " + language + "/" + cue + "/" + variant);
        foreach (string key in new[] { "MusicVolume", "VoiceVolume" })
        {
            var p = typeof(TabletopAudio).GetProperty(key);
            p.SetValue(audio, float.NaN); Require((float)p.GetValue(audio) == 0, key + "非数值没有限制");
            p.SetValue(audio, 2f); Require((float)p.GetValue(audio) == 1, key + "超限没有限制");
            p.SetValue(audio, audioPreferences[key]);
        }
        audio.VoiceLanguage = 1; audio.MusicVolume = .37f; audio.VoiceVolume = .61f; audio.FlushSettings();
        Require(PlayerPrefs.GetInt("catan.audio.v1.language", -1) == 1 &&
            Math.Abs(PlayerPrefs.GetFloat("catan.audio.v1.music", -1) - .37f) < .001f &&
            Math.Abs(PlayerPrefs.GetFloat("catan.audio.v1.voice", -1) - .61f) < .001f, "新增声音设置没有持久化");
        stage = "地图与扩展完整覆盖";
        string[] scenarios = { "heading-for-new-shores", "four-islands", "fog-islands", "through-the-desert", "forgotten-tribe", "cloth-for-catan", "pirate-islands", "wonders-of-catan", "new-world" };
        var allMaps = new HashSet<Texture2D>();
        for (int rule = 0; rule < 4; rule++)
        {
            CheckTexture(MenuMapPreviews.RuleTexture(rule), "扩展封面 " + rule);
            string[] ids = rule == 0 || rule == 2 ? new[] { "standard" } : rule == 3 ? new[] { scenarios[0], scenarios[3] } : scenarios;
            foreach (string id in ids) foreach (int players in new[] { 3, 4 })
            {
                var texture = MenuMapPreviews.ScenarioTexture(rule, id, players);
                CheckTexture(texture, rule + "/" + id + "/" + players);
                Require(allMaps.Add(texture), "地图人数映射重复引用");
            }
        }
        Require(allMaps.Count == 26, "地图配置覆盖数不符");
        Require(MenuMapPreviews.ScenarioCaption(1, "fog-islands", 4).Contains("云雾"), "迷雾地图缺少说明");
        stage = "五套动态主页";
        var landscapeShader = Resources.Load<Shader>("PastoralMenu/v003/LivingLandscape");
        Require(landscapeShader != null && landscapeShader.isSupported, "动态风景着色器缺失或不支持");
        Go(-1);
        PastoralMenuScene.Animated = true;
        for (int theme = 0; theme < 5; theme++)
        {
            PastoralMenuScene.Select(theme);
            yield return new WaitForSecondsRealtime(.4f);
            Require(PastoralMenuScene.ArtworkReady, "主题原画缺失 " + theme);
            Require(audio.MenuTheme == theme, "主题环境音没有同步 " + theme);
            CheckTexture(PastoralMenuScene.Preview(theme), "主题预览 " + theme);
            if (capturesEnabled)
            {
                yield return Capture("theme-" + theme + "-a");
                yield return new WaitForSecondsRealtime(1.2f);
                yield return Capture("theme-" + theme + "-b");
                int difference = ChangedPixels(captures[captures.Count - 2], captures[captures.Count - 1]);
                animatedPixels.Add(difference);
                Require(difference > 100, "主题两帧没有实际动画 " + theme);
            }
        }
        Go(4); Set("menuSettingsTab", 1); yield return Capture("settings-sound");
        Set("menuSettingsTab", 2); yield return Capture("settings-appearance");
        Set("menuSettingsTab", 3); yield return Capture("settings-landscapes");
        Go(0); Set("menuSetupStep", 0); yield return Capture("rule-covers");
        Set("menuSetupStep", 1);
        for (int rule = 0; rule < 4; rule++)
        {
            Set("menuRule", rule); Set("menuScenario", 0);
            yield return Capture("maps-" + rule);
        }
        // 只展示创建页，不调用连接、发现或创建房间入口。
        Set("networkMenu", true); Set("networkLan", true); Set("networkTab", 1); Set("networkSettingsPage", 1);
        yield return Capture("network-map-preview");
        Require(!app.Network.Connected, "预览不应连接网络服务");
        Set("networkMenu", false);
        stage = "展示与权威状态隔离";
        app.StartGame("cities-knights", "heading-for-new-shores", 4, "P1", true);
        for (int frame = 0; !app.Ready && frame < 240; frame++) yield return null;
        Require(app.Ready, "隔离对局未就绪");
        app.ShowMenu(); string authority = app.ExportEnvelope();
        for (int theme = 0; theme < 5; theme++)
        {
            PastoralMenuScene.Select(theme); yield return null;
            Require(app.ExportEnvelope() == authority && !app.StepAi(), "主题改变了对局");
        }
        PastoralMenuScene.Animated = false;
        Require(PlayerPrefs.GetInt("Catan.Menu.Animated.v003", -1) == 0, "动态开关没有保存");
        Require(app.ExportEnvelope() == authority, "关闭动态改变了对局");
    }

    void CheckClip(AudioClip clip, string name)
    {
        Require(clip != null && clip.samples > 0 && clip.frequency >= 16000, "片段缺失 " + name);
        var data = new float[clip.samples * clip.channels];
        Require(clip.GetData(data, 0) && data.Any(v => Math.Abs(v) > .0001f), "片段静音或不可读 " + name);
        Require(data.All(v => !float.IsNaN(v) && !float.IsInfinity(v) && Math.Abs(v) <= .701f), "片段无效或超出混音余量 " + name + " 峰值=" + data.Max(v => Math.Abs(v)));
    }
    void CheckTexture(Texture2D texture, string name) => Require(texture != null && texture.width >= 256 && texture.height >= 128, "图片缺失 " + name);
    void Go(int page) => typeof(M6App).GetMethod("NavigateMainMenu", Private).Invoke(app, new object[] { page });
    void Set(string key, object value) => typeof(M6App).GetField(key, Private).SetValue(app, value);
    void Require(bool condition, string reason) { checks++; if (!condition) throw new InvalidOperationException(reason); }
    IEnumerator Capture(string name)
    {
        if (!capturesEnabled) yield break;
        stage = name;
        yield return new WaitForSecondsRealtime(.3f);
        for (int frame = 0; frame < 3; frame++) yield return new WaitForEndOfFrame();
        var texture = ScreenCapture.CaptureScreenshotAsTexture();
        Require(texture != null && texture.width == Screen.width, "截图未生成");
        var pixels = texture.GetPixels32();
        Require(pixels.Count(c => c.r > 24 || c.g > 24 || c.b > 24) > pixels.Length / 8, "窗口未绘制，不能将黑帧算为验收");
        string filename = name + ".png";
        File.WriteAllBytes(Path.Combine(output, filename), texture.EncodeToPNG());
        Destroy(texture); captures.Add(filename);
    }
    int ChangedPixels(string first, string second)
    {
        var a = new Texture2D(2, 2); var b = new Texture2D(2, 2);
        a.LoadImage(File.ReadAllBytes(Path.Combine(output, first))); b.LoadImage(File.ReadAllBytes(Path.Combine(output, second)));
        var left = a.GetPixels32(); var right = b.GetPixels32(); int changed = 0;
        for (int i = 0; i < Math.Min(left.Length, right.Length); i++)
            if (Math.Abs(left[i].r - right[i].r) + Math.Abs(left[i].g - right[i].g) + Math.Abs(left[i].b - right[i].b) > 15) changed++;
        Destroy(a); Destroy(b); return changed;
    }
    void RestorePreferences()
    {
        if (!preferencesCaptured) return;
        preferencesCaptured = false;
        var audio = TabletopAudio.Instance;
        if (audio != null)
        {
            foreach (var p in audioPreferences) typeof(TabletopAudio).GetProperty(p.Key).SetValue(audio, p.Value);
            audio.FlushSettings();
        }
        PastoralMenuScene.Animated = originalMotion;
        PastoralMenuScene.Select(originalTheme);
    }
    void OnDestroy() => RestorePreferences();
    void Fail(Exception e)
    {
        RestorePreferences();
        Debug.LogError("CATAN_AUDIO_VISUAL_FAILED: " + stage + " / " + e.GetBaseException().Message);
        Application.Quit(1);
    }
    [Serializable] sealed class Result
    {
        public string status, version, limitations;
        public int checks;
        public string[] captures;
        public int[] animatedPixels;
        public bool fullUiPixelsCaptured, userSaveTouched, humanAudioListening;
    }
}
