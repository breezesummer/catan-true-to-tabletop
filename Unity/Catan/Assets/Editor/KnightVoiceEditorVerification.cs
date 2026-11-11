using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 仅由显式验收入口启用；使用独立本地四人测试局，恢复声音偏好，不写用户存档。
[InitializeOnLoad]
public static class KnightVoiceEditorVerification
{
    const string Key = "Catan.KnightVoiceReview";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    static IEnumerator work;
    static string output;
    static double next, deadline;
    static M6App app;
    static TabletopAudio audio;
    static int checks;
    static string stage;
    static readonly Dictionary<string, object> preferences = new Dictionary<string, object>();
    static KnightVoiceEditorVerification() { EditorApplication.update += Tick; }
    public static void Run()
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "--knight-voice-verify");
        if (index < 0 || index + 1 >= args.Length) throw new InvalidOperationException("缺少骑士语音验收输出路径。");
        output = Path.GetFullPath(args[index + 1]);
        string local = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../.local")) + Path.DirectorySeparatorChar;
        if (!output.StartsWith(local, StringComparison.Ordinal)) throw new InvalidOperationException("验收目录须位于.local内。");
        Directory.CreateDirectory(output);
        foreach (var file in new[] { "result.json", "failure.txt" })
            if (File.Exists(Path.Combine(output, file))) File.Delete(Path.Combine(output, file));
        SessionState.SetString(Key, output);
        EditorSceneManager.OpenScene("Assets/M6.unity");
        typeof(B45DockEditorVerification).GetMethod("SetSize", Flags).Invoke(null, new object[] { 1440, 900 });
        EditorApplication.isPaused = false; EditorApplication.isPlaying = true;
    }
    static object Get(object target, string field) => target.GetType().GetField(field, Flags).GetValue(target);
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
    static void Require(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }
    static void Tick()
    {
        output = SessionState.GetString(Key, "");
        if (output.Length == 0 || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (work == null)
            {
                app = UnityEngine.Object.FindFirstObjectByType<M6App>(); audio = TabletopAudio.Instance;
                if (app == null || audio == null || !audio.Ready) return;
                deadline = EditorApplication.timeSinceStartup + 180; work = Review();
            }
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("骑士语音验收超时：" + stage + "，已加载=" + audio.VoiceClipCount + "，帧=" + Time.frameCount + "，编辑器暂停=" + EditorApplication.isPaused);
            if (EditorApplication.timeSinceStartup < next) return;
            if (!work.MoveNext()) { Finish(null); return; }
            if (work.Current is double wait) next = EditorApplication.timeSinceStartup + wait;
        }
        catch (Exception error) { Finish(error); }
    }
    static IEnumerator Review()
    {
        Stage("加载分级语音");
        while (!audio.KnightVoicesLoaded) yield return .05;
        Require(audio.VoiceClipCount == 36, "36句语音未全部加载。");
        foreach (var name in new[] { "MasterVolume", "EffectsVolume", "VoiceVolume", "Muted" })
            preferences[name] = typeof(TabletopAudio).GetProperty(name).GetValue(audio);
        audio.Muted = true;
        Stage("启动基准四人对局");
        app.StartGame("combined", "through-the-desert", 4, "P1", true, threeKingdoms: true, harbormaster: true, fishing: true);
        while (!app.Ready) yield return .05;
        Require(app.ThreeKingdoms && app.Harbormaster && app.Fishing && app.PlayerCount == 4, "验收基准未正确开启。");
        for (int step = 0; step < 128; step++)
        {
            var presentation = Get(app, "presentation") as M5App;
            var view = presentation == null ? null : (Catan.Core.M5.PlayerView)typeof(M5App).GetProperty("M6View", Flags).GetValue(presentation);
            if (view != null && view.Board.Settlements.Length + view.Board.Cities.Length >= 8 && !view.Phase.ToString().StartsWith("Setup", StringComparison.Ordinal)) break;
            var decide = (Func<string, string>)Get(app, "decideAndSubmit");
            Require(decide(app.CurrentActor) == null, "测试局开局操作未成功。");
            ((Action)Get(app, "refresh"))(); yield return .01;
        }
        string authority = app.ExportEnvelope();
        Stage("检查36句实际播放");
        audio.ResetHistory(); audio.MasterVolume = .001f; audio.EffectsVolume = 0; audio.VoiceVolume = 1; audio.Muted = false;
        yield return .05;
        var queueVoice = typeof(TabletopAudio).GetMethod("QueueKnightVoice", Flags);
        var pump = typeof(TabletopAudio).GetMethod("PumpKnightVoices", Flags);
        Action<KnightVoiceSelection> play = voice =>
        {
            typeof(TabletopAudio).GetMethod("ResetKnightVoices", Flags).Invoke(audio, null);
            foreach (var source in audio.GetComponents<AudioSource>().Where(source => source.clip != null && source.clip.name.StartsWith("Knight", StringComparison.Ordinal))) source.Stop();
            double at = Time.unscaledTimeAsDouble;
            queueVoice.Invoke(audio, new object[] { voice, at }); pump.Invoke(audio, new object[] { at });
        };
        for (int level = 1; level <= 3; level++)
        {
            for (int variant = 0; variant < 2; variant++) for (int action = 1; action <= 6; action++)
            {
                var kind = (KnightVoiceAction)action;
                var clip = audio.GetKnightVoiceClip(kind, level, variant);
                Require(clip != null && clip.frequency == 24000 && clip.channels == 1, "语音资源格式不符。");
                var data = new float[clip.samples]; Require(clip.GetData(data, 0) && data.Any(value => Mathf.Abs(value) > .001f), "语音为空。");
                Require(data.All(value => Mathf.Abs(value * KnightVoiceCatalog.Gain) <= .701f), "语音超过混音余量。");
                int before = audio.PlayedKnightVoiceCount; play(new KnightVoiceSelection(kind, level));
                Require(audio.PlayedKnightVoiceCount == before + 1, "语音未实际启动。");
                Require(audio.LastKnightVoicePath == KnightVoiceCatalog.Path(kind, level, variant), "动作、等级或A/B语音映射错误。");
                Require(audio.GetComponents<AudioSource>().Any(source => source.clip == clip && source.isPlaying && source.pitch == 1), "声音没有按原音色播放。");
                yield return .025;
            }
        }
        audio.ResetHistory();
        // 先让真实 Update 建立新历史水位，再检查随后的增量事件。
        yield return .05;
        foreach (var source in audio.GetComponents<AudioSource>().Where(source => source.clip != null && source.clip.name.StartsWith("Knight", StringComparison.Ordinal))) source.Stop();
        Stage("检查同帧战斗语音顺序");
        var queue = typeof(TabletopAudio).GetMethod("Queue", Flags);
        var expected = new[] { new KnightVoiceSelection(KnightVoiceAction.ChasePirate, 1), new KnightVoiceSelection(KnightVoiceAction.ChaseRobber, 2), new KnightVoiceSelection(KnightVoiceAction.Attack, 3) };
        double now = Time.unscaledTimeAsDouble;
        foreach (var voice in expected) queue.Invoke(audio, new object[] { new TabletopAudioEvent(TabletopAudioCue.KnightCombat, knightVoice: voice), now });
        for (int index = 0; index < expected.Length; index++)
        {
            int before = audio.PlayedKnightVoiceCount;
            double until = EditorApplication.timeSinceStartup + 5;
            while (audio.PlayedKnightVoiceCount == before && EditorApplication.timeSinceStartup < until) yield return .025;
            Require(audio.PlayedKnightVoiceCount == before + 1, "同帧战斗语音未开始播放。");
            var voice = expected[index];
            Require(audio.LastKnightVoicePath == KnightVoiceCatalog.Path(voice.Action, voice.Level, 0), "同帧战斗动作被合并或倒序。");
            Require(audio.PendingKnightVoiceCount == expected.Length - index - 1, "待播队列数量异常。");
            foreach (var source in audio.GetComponents<AudioSource>().Where(source => source.clip != null && source.clip.name.StartsWith("Knight", StringComparison.Ordinal))) source.Stop();
        }
        int count = audio.PlayedKnightVoiceCount;
        Stage("检查静音与暂停");
        audio.Muted = true; play(new KnightVoiceSelection(KnightVoiceAction.Move, 1)); yield return .05;
        Require(audio.PlayedKnightVoiceCount == count && audio.PendingKnightVoiceCount == 0, "静音后仍触发语音。");
        audio.Muted = false; audio.VoiceVolume = 0; play(new KnightVoiceSelection(KnightVoiceAction.Move, 1)); yield return .05;
        Require(audio.PlayedKnightVoiceCount == count, "语音音量为零仍触发播放。");
        audio.VoiceVolume = 1;
        typeof(TabletopAudio).GetMethod("OnApplicationPause", Flags).Invoke(audio, new object[] { true });
        play(new KnightVoiceSelection(KnightVoiceAction.Move, 1)); Require(audio.PlayedKnightVoiceCount == count, "后台暂停仍触发语音。");
        typeof(TabletopAudio).GetMethod("OnApplicationPause", Flags).Invoke(audio, new object[] { false });
        Require(app.ExportEnvelope() == authority, "声音操作改变了权威局面。");
        Stage("检查声音设置画面");
        typeof(B45DockEditorVerification).GetMethod("SetSize", Flags).Invoke(null, new object[] { 1440, 900 });
        app.OpenTableMenu(); Set(app, "tablePausePage", 1); Set(app, "tablePauseSettingsTab", 1);
        yield return .7;
        string capture = Path.Combine(output, "双扩展三国捕鱼-骑士语音设置.png"); ScreenCapture.CaptureScreenshot(capture);
        for (int frame = 0; frame < 50 && !File.Exists(capture); frame++) yield return .05;
        Require(File.Exists(capture), "没有取得基准游戏画面。");
        File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(new Result { status = "passed", version = M6App.BuildVersion, checks = checks, assets = audio.VoiceClipCount, played = audio.PlayedKnightVoiceCount, authorityUnchanged = true }, true));
        Debug.Log("CATAN_KNIGHT_VOICES_OK: checks=" + checks + " assets=36 played=" + audio.PlayedKnightVoiceCount);
    }
    static void Stage(string value) { stage = value; Debug.Log("CATAN_KNIGHT_VOICES_STAGE: " + value); }
    [Serializable] sealed class Result { public string status, version; public int checks, assets, played; public bool authorityUnchanged; }
    static void Finish(Exception error)
    {
        if (audio != null)
        {
            foreach (var preference in preferences) typeof(TabletopAudio).GetProperty(preference.Key).SetValue(audio, preference.Value);
            audio.FlushSettings();
        }
        SessionState.EraseString(Key);
        if (error != null) { Debug.LogException(error); File.WriteAllText(Path.Combine(output, "failure.txt"), error.ToString()); }
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
