using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 所有声音均为 2D 展示；不会提交命令、访问权威存档或改变动画/规则时间。
public sealed class TabletopAudio : MonoBehaviour
{
    public const int VoiceLimit = 10;
    public const float EffectsHeadroom = TabletopAudioMix.Effects, AmbientHeadroom = TabletopAudioMix.Ambience,
        MusicHeadroom = TabletopAudioMix.Music, SpeechHeadroom = TabletopAudioMix.Speech;
    const string PreferencePrefix = "catan.audio.v1.";
    const string AssetRoot = "TabletopAudio/v003/";
    public static TabletopAudio Instance { get; private set; }
    public bool Ready { get; private set; }
    public int ClipCount => clips.Count;
    public int PlayedCueCount { get; private set; }
    public long HistoryCursor => model.Cursor;
    public int ActiveVoices => voices.Count(v => v != null && v.isPlaying);
    public IEnumerable<TabletopAudioCue> AvailableCues => clips.Keys;
    public float MasterVolume { get => master; set { master = Volume(value); SettingsChanged(); } }
    public float EffectsVolume { get => effects; set { effects = Volume(value); SettingsChanged(); } }
    public float AmbientVolume { get => ambient; set { ambient = Volume(value); SettingsChanged(); } }
    public float MusicVolume { get => music; set { music = Volume(value); SettingsChanged(); } }
    public float VoiceVolume { get => speech; set { speech = Volume(value); SettingsChanged(); } }
    public int VoiceLanguage { get => voiceLanguage; set { voiceLanguage = Mathf.Clamp(value, 0, 1); SettingsChanged(); } }
    public int MenuTheme => menuTheme;
    public bool Muted { get => muted; set { muted = value; if (muted) { scheduled.Clear(); foreach (var voice in voices) if (voice != null) voice.Stop(); if (speechSource != null) speechSource.Stop(); } SettingsChanged(); } }
    public bool MenuAmbienceActive => environmentSources.Any(s => s != null && s.isPlaying && s.volume > .0001f);
    public bool MusicActive => musicSource != null && musicSource.isPlaying && musicSource.volume > .0001f;
    public int VoiceClipCount => speechClips.Count(c => c != null);
    readonly Dictionary<TabletopAudioCue, AudioClip> clips = new Dictionary<TabletopAudioCue, AudioClip>();
    readonly TabletopAudioModel model = new TabletopAudioModel();
    readonly TabletopAudioPublicModel publicModel = new TabletopAudioPublicModel();
    readonly TabletopAudioLimiter limiter = new TabletopAudioLimiter();
    struct Scheduled { internal TabletopAudioCue Cue; internal double At; }
    readonly List<Scheduled> scheduled = new List<Scheduled>();
    AudioSource[] voices = Array.Empty<AudioSource>();
    float[] voiceGains = Array.Empty<float>();
    readonly AudioSource[] environmentSources = new AudioSource[5];
    readonly AudioClip[] environmentClips = new AudioClip[5];
    readonly float[] environmentWeights = new float[5];
    readonly AudioClip[] speechClips = new AudioClip[8];
    readonly List<AudioClip> generatedClips = new List<AudioClip>();
    AudioSource musicSource, speechSource;
    AudioClip musicClip;
    M6App owner;
    AudioListener fallbackListener;
    float master, effects, ambient, music, speech, musicDuck = 1;
    int voiceLanguage, menuTheme, speechVariant;
    bool muted, settingsDirty, applicationPaused;
    double saveSettingsAt, reconnectQuietUntil, nextListenerCheck;
    bool wasConnected;
    string previousNotice, previousScope;
    int uiFrame = -1;
    float ambientBlend;
    int pointerButton = -1;
    Vector2 pointerStart;
    bool pointerDragged;

    public void Initialize(M6App app)
    {
        owner = app; Instance = this;
        master = Volume(PlayerPrefs.GetFloat(PreferencePrefix + "master", .85f));
        effects = Volume(PlayerPrefs.GetFloat(PreferencePrefix + "effects", .82f));
        ambient = Volume(PlayerPrefs.GetFloat(PreferencePrefix + "ambient", .34f));
        music = Volume(PlayerPrefs.GetFloat(PreferencePrefix + "music", .52f));
        speech = Volume(PlayerPrefs.GetFloat(PreferencePrefix + "voice", .90f));
        voiceLanguage = Mathf.Clamp(PlayerPrefs.GetInt(PreferencePrefix + "language", 0), 0, 1);
        menuTheme = Mathf.Clamp(PlayerPrefs.GetInt(PreferencePrefix + "theme", 0), 0, 4);
        muted = PlayerPrefs.GetInt(PreferencePrefix + "muted", 0) != 0;
        foreach (TabletopAudioCue cue in Enum.GetValues(typeof(TabletopAudioCue)))
        {
            var clip = Resources.Load<AudioClip>(AssetRoot + "Effects/" + cue);
            if (clip == null) clip = GeneratedClip("卡坦音效 " + cue, TabletopAudioSynth.Render(cue));
            clips.Add(cue, clip);
        }
        voices = new AudioSource[VoiceLimit];
        voiceGains = new float[VoiceLimit];
        for (int i = 0; i < voices.Length; i++) voices[i] = NewSource("音效声部 " + i, false);
        for (int i = 0; i < 5; i++)
        {
            environmentClips[i] = Resources.Load<AudioClip>(AssetRoot + "Ambience/Theme" + i);
            if (environmentClips[i] == null) environmentClips[i] = GeneratedClip("主题环境 " + i, TabletopAudioSynth.RenderAmbience(i));
            environmentSources[i] = NewSource("主题环境 " + i, true); environmentSources[i].clip = environmentClips[i];
        }
        environmentWeights[menuTheme] = 1;
        musicClip = Resources.Load<AudioClip>(AssetRoot + "Music/SeaBreezeHome");
        if (musicClip == null) musicClip = GeneratedClip("海风归途", TabletopAudioSynth.RenderMusic());
        musicSource = NewSource("循环背景音乐", true); musicSource.clip = musicClip;
        speechSource = NewSource("骑士语音", false); speechSource.priority = 40;
        for (int language = 0; language < 2; language++) for (int action = 0; action < 2; action++) for (int variant = 0; variant < 2; variant++)
            speechClips[language * 4 + action * 2 + variant] = Resources.Load<AudioClip>(AssetRoot + "Voices/" + (language == 0 ? "zh/" : "en/") +
                (action == 0 ? "KnightRecruit" : "KnightActivate") + (variant + 1).ToString("D2"));
        fallbackListener = gameObject.AddComponent<AudioListener>(); fallbackListener.enabled = false;
        UpdateListener();
        gameObject.AddComponent<TabletopAudioGuiTap>().Initialize(this);
        Ready = true;
    }
    AudioClip GeneratedClip(string label, float[] data)
    {
        var clip = AudioClip.Create(label, data.Length, 1, TabletopAudioSynth.SampleRate, false);
        clip.SetData(data, 0); generatedClips.Add(clip); return clip;
    }
    AudioSource NewSource(string label, bool loop)
    {
        var source = gameObject.AddComponent<AudioSource>(); source.playOnAwake = false;
        source.spatialBlend = 0; source.dopplerLevel = 0; source.loop = loop;
        source.priority = loop ? 220 : 100; source.volume = 0;
        return source;
    }
    public AudioClip GetClip(TabletopAudioCue cue) => clips.TryGetValue(cue, out var clip) ? clip : null;
    public AudioClip GetMusicClip() => musicClip;
    public AudioClip GetAmbienceClip(int theme) => theme >= 0 && theme < 5 ? environmentClips[theme] : null;
    public AudioClip GetVoiceClip(int language, TabletopAudioCue cue, int variant = 0)
    {
        if (language < 0 || language > 1 || variant < 0 || variant > 1 || (cue != TabletopAudioCue.KnightRecruit && cue != TabletopAudioCue.KnightActivate)) return null;
        return speechClips[language * 4 + (cue == TabletopAudioCue.KnightRecruit ? 0 : 2) + variant];
    }
    public void SetMenuTheme(int index) { menuTheme = Mathf.Clamp(index, 0, 4); SettingsChanged(); }
    public void PreviewVoice() { PlaySpeech(TabletopAudioCue.KnightRecruit, true); }
    void PlaySpeech(TabletopAudioCue cue, bool preview = false)
    {
        if (!Ready || muted || applicationPaused || master <= 0 || speech <= 0 || speechSource == null) return;
        if (!preview && speechSource.isPlaying) return;
        var clip = GetVoiceClip(voiceLanguage, cue, speechVariant++ % 2); if (clip == null) return;
        speechSource.Stop(); speechSource.clip = clip; speechSource.pitch = 1; ApplyVolumes(); speechSource.Play();
    }
    static float Volume(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Clamp01(value);
    void SettingsChanged()
    {
        settingsDirty = true; saveSettingsAt = Time.unscaledTimeAsDouble + .55;
        ApplyVolumes();
    }
    public void FlushSettings()
    {
        if (!settingsDirty) return;
        PlayerPrefs.SetFloat(PreferencePrefix + "master", master);
        PlayerPrefs.SetFloat(PreferencePrefix + "effects", effects);
        PlayerPrefs.SetFloat(PreferencePrefix + "ambient", ambient);
        PlayerPrefs.SetFloat(PreferencePrefix + "music", music);
        PlayerPrefs.SetFloat(PreferencePrefix + "voice", speech);
        PlayerPrefs.SetInt(PreferencePrefix + "language", voiceLanguage);
        PlayerPrefs.SetInt(PreferencePrefix + "theme", menuTheme);
        PlayerPrefs.SetInt(PreferencePrefix + "muted", muted ? 1 : 0); PlayerPrefs.Save(); settingsDirty = false;
    }
    void ApplyVolumes()
    {
        int count = Math.Max(1, voices.Count(v => v != null && v.isPlaying));
        for (int i = 0; i < voices.Length; i++) if (voices[i] != null)
        {
            voices[i].mute = muted || applicationPaused;
            // 音效、环境、音乐、语音总线最坏峰值上界 .70*(.66+.16+.26+.32)=.98。
            voices[i].volume = master * effects * voiceGains[i] * EffectsHeadroom / count;
        }
        for (int i = 0; i < 5; i++) if (environmentSources[i] != null)
        { environmentSources[i].mute = muted || applicationPaused; environmentSources[i].volume = master * ambient * ambientBlend * environmentWeights[i] * AmbientHeadroom; }
        if (musicSource != null) { musicSource.mute = muted || applicationPaused; musicSource.volume = master * music * musicDuck * MusicHeadroom; }
        if (speechSource != null) { speechSource.mute = muted || applicationPaused; speechSource.volume = master * speech * SpeechHeadroom; }
    }
    public void ResetHistory()
    {
        model.Reset(); publicModel.Reset(); scheduled.Clear(); limiter.Reset();
        previousScope = null;
    }
    public void Click(bool cancel = false)
    {
        if (uiFrame == Time.frameCount || MobileTabletop.TouchDragged) return;
        uiFrame = Time.frameCount;
        Play(cancel ? TabletopAudioCue.UiBack : TabletopAudioCue.UiClick);
    }
    internal void ObserveGuiPointerDown(int button)
    {
        pointerButton = button; pointerStart = Input.mousePosition; pointerDragged = false;
    }
    internal void ObserveGuiPointerUp(int button)
    {
        if (pointerButton != button) return;
        pointerButton = -1;
        float threshold = MobileTabletop.Enabled ? 18 : 7;
        if (!pointerDragged && ((Vector2)Input.mousePosition - pointerStart).sqrMagnitude <= threshold * threshold && !MobileTabletop.TouchDragged) Click(button == 1);
    }
    public void Play(TabletopAudioCue cue, float gain = 1)
    {
        if (!Ready || muted || applicationPaused || master <= 0 || !clips.TryGetValue(cue, out var clip)) return;
        double now = Time.unscaledTimeAsDouble;
        if (!limiter.Admit(cue, now)) return;
        if (cue == TabletopAudioCue.KnightRecruit || cue == TabletopAudioCue.KnightActivate) PlaySpeech(cue);
        if (effects <= 0) return;
        var source = voices.FirstOrDefault(v => !v.isPlaying);
        if (source == null)
        {
            // 紧急事件可以替换最旧普通声，普通动作不会切掉警报或胜利乐句。
            source = voices.Where(v => v.priority >= Priority(cue)).OrderByDescending(v => v.time).FirstOrDefault();
            if (source == null) return;
        }
        source.Stop(); source.clip = clip; source.pitch = 1;
        voiceGains[Array.IndexOf(voices, source)] = Volume(gain);
        source.priority = Priority(cue); source.volume = master * effects * Volume(gain) * EffectsHeadroom / Math.Max(1, ActiveVoices + 1);
        source.mute = false; source.Play(); ApplyVolumes(); PlayedCueCount++;
    }
    static int Priority(TabletopAudioCue cue) => cue == TabletopAudioCue.BarbarianInvasion || cue == TabletopAudioCue.BuildingCollapse || cue == TabletopAudioCue.Victory ? 35 :
        cue == TabletopAudioCue.DiceRoll || cue == TabletopAudioCue.YourTurn ? 65 : cue == TabletopAudioCue.UiClick ? 120 : 100;
    void Queue(TabletopAudioEvent e, double now)
    {
        if (scheduled.Count >= 32) return;
        // 同一帧同类收牌/卡牌事件合并，既不暴露手牌数量，也不会堆叠增益。
        if (scheduled.Any(s => s.Cue == e.Cue && Math.Abs(s.At - now - e.Delay) < .04)) return;
        scheduled.Add(new Scheduled { Cue = e.Cue, At = now + e.Delay });
    }
    void Update()
    {
        if (!Ready || owner == null) return;
        double now = Time.unscaledTimeAsDouble;
        // 旧 IMGUI 在不同 MonoBehaviour 之间不会可靠保留 Used；输入作为全局兜底。
        // 所有短按均给柔和反馈（包括空白），实际控件可显式 Click 并按帧去重。
        if (Input.GetMouseButtonDown(0)) ObserveGuiPointerDown(0);
        if (Input.GetMouseButtonDown(1)) ObserveGuiPointerDown(1);
        if (pointerButton >= 0)
        {
            float threshold = MobileTabletop.Enabled ? 18 : 7;
            if (((Vector2)Input.mousePosition - pointerStart).sqrMagnitude > threshold * threshold || MobileTabletop.TouchDragged) pointerDragged = true;
        }
        if (pointerButton >= 0 && Input.GetMouseButtonUp(pointerButton))
            ObserveGuiPointerUp(pointerButton);
        if (settingsDirty && now >= saveSettingsAt) FlushSettings();
        if (now >= nextListenerCheck) { UpdateListener(); nextListenerCheck = now + 1; }
        float environmentTarget = owner.MenuVisible || owner.TableMenuOpen ? 1 : .28f;
        ambientBlend = Mathf.MoveTowards(ambientBlend, environmentTarget, Time.unscaledDeltaTime * 1.5f);
        TabletopAudioMix.FadeTheme(environmentWeights, menuTheme, Time.unscaledDeltaTime * .55f);
        for (int i = 0; i < 5; i++)
        {
            if (environmentWeights[i] > .00001f && !applicationPaused && !environmentSources[i].isPlaying) environmentSources[i].Play();
            else if (environmentWeights[i] <= .00001f && environmentSources[i].isPlaying) environmentSources[i].Stop();
        }
        // 音乐贯穿主页与对局，骑士讲话时自动降低，语音结束后平滑恢复。
        musicDuck = Mathf.MoveTowards(musicDuck, speechSource.isPlaying ? .40f : 1f, Time.unscaledDeltaTime * 2.5f);
        if (!musicSource.isPlaying && !applicationPaused) musicSource.Play();
        ApplyVolumes();
        bool connected = owner.NetworkGame && owner.Network != null && owner.Network.Connected;
        if (owner.NetworkGame && connected != wasConnected)
        {
            ResetHistory(); reconnectQuietUntil = now + 1.25;
            if (connected) Play(TabletopAudioCue.Network);
        }
        wasConnected = connected;
        if (owner.Ready)
        {
            var scope = owner.AudioScope;
            if (scope != previousScope) { ResetHistory(); previousScope = scope; }
            // 历史请求与 PlayerView 同步是异步的；重连短暂静默期间持续建立基线。
            if (now < reconnectQuietUntil) model.Reset();
            var added = model.Observe(scope, owner.HistoryEntries, owner.AudioPublicSequence);
            if (now >= reconnectQuietUntil) foreach (var e in added) Queue(e, now);
            // 网络 Busy 只是传输状态；不能把每次请求完成误报为新的人类回合。
            bool human = owner.CurrentActor == owner.HumanSeat, finished = owner.Finished;
            int barbarian = owner.AudioBarbarianPosition;
            string pirate = owner.AudioPirateTile;
            var publicEvents = publicModel.Observe(human, finished, barbarian, pirate, owner.ScenarioId == "pirate-islands", added.Count(e => e.Cue == TabletopAudioCue.BarbarianAdvance), owner.AudioActiveKnights,owner.AudioOpportunity);
            if (now >= reconnectQuietUntil) foreach (var e in publicEvents) Queue(e, now);
        }
        else { if (publicModel.Initialized) ResetHistory(); }
        string notice = owner.Notice;
        if (previousNotice != null && notice != previousNotice)
        {
            if (notice != null && (notice.Contains("失败") || notice.Contains("拒绝") || notice.Contains("无法"))) Play(TabletopAudioCue.Error);
            else if (notice != null && (notice.Contains("已保存") || notice.Contains("已恢复"))) Play(TabletopAudioCue.Save);
        }
        previousNotice = notice;
        for (int i = scheduled.Count - 1; i >= 0; i--)
            if (scheduled[i].At <= now) { var cue = scheduled[i].Cue; scheduled.RemoveAt(i); Play(cue); }
        if (Input.GetKeyDown(KeyCode.Escape)) Click(true);
    }
    void UpdateListener()
    {
        if (fallbackListener == null) return;
        bool other = FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Any(l => l != fallbackListener && l.enabled && l.gameObject.activeInHierarchy);
        fallbackListener.enabled = !other;
    }
    public void DrawSettings(Rect area, Font font)
    {
        var label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 11, alignment = TextAnchor.MiddleLeft };
        bool pastoral = owner != null && owner.MenuVisible && !owner.NetworkGame;
        label.normal.textColor = pastoral ? PastoralMenuUI.Ink : ModernOperationUI.TextColor;
        GUI.Label(new Rect(area.x, area.y, 70, 24), "声音设置", label);
        if (ModernOperationUI.Button(new Rect(area.xMax - 254, area.y, 77, 24), muted ? "已静音" : "声音开启", true, false, !muted)) { Muted = !muted; if (!muted) Click(); }
        if (ModernOperationUI.Button(new Rect(area.xMax - 169, area.y, 89, 24), voiceLanguage == 0 ? "真人：应答" : "真人：战呼")) VoiceLanguage = 1 - VoiceLanguage;
        if (ModernOperationUI.Button(new Rect(area.xMax - 72, area.y, 72, 24), "语音试听")) PreviewVoice();
        float width = area.width / 5;
        DrawCompactSlider(new Rect(area.x, area.y + 30, width - 10, 36), "总音量", master, v => MasterVolume = v, label);
        DrawCompactSlider(new Rect(area.x + width, area.y + 30, width - 10, 36), "音效", effects, v => EffectsVolume = v, label);
        DrawCompactSlider(new Rect(area.x + width * 2, area.y + 30, width - 10, 36), "环境", ambient, v => AmbientVolume = v, label);
        DrawCompactSlider(new Rect(area.x + width * 3, area.y + 30, width - 10, 36), "音乐", music, v => MusicVolume = v, label);
        DrawCompactSlider(new Rect(area.x + width * 4, area.y + 30, width - 2, 36), "语音", speech, v => VoiceVolume = v, label);
    }
    void DrawCompactSlider(Rect rect, string caption, float value, Action<float> set, GUIStyle label)
    {
        GUI.Label(new Rect(rect.x, rect.y, rect.width, 18), caption + "  " + Mathf.RoundToInt(value * 100), label);
        float next = GUI.HorizontalSlider(new Rect(rect.x, rect.y + 22, rect.width - 2, 14), value, 0, 1);
        if (Mathf.Abs(next - value) > .0001f) set(next);
    }
    void DrawSlider(Rect rect, string caption, float value, Action<float> set, GUIStyle label)
    {
        GUI.Label(new Rect(rect.x, rect.y, 54, rect.height), caption, label);
        GUI.Label(new Rect(rect.xMax - 32, rect.y, 32, rect.height), Mathf.RoundToInt(value * 100).ToString(), label);
        float next = GUI.HorizontalSlider(new Rect(rect.x + 56, rect.y + 7, rect.width - 96, 16), value, 0, 1);
        if (Mathf.Abs(next - value) > .0001f) set(next);
    }
    void OnApplicationPause(bool paused)
    {
        applicationPaused = paused; ApplyVolumes();
        if (paused)
        {
            scheduled.Clear(); FlushSettings(); foreach (var source in environmentSources) if (source != null) source.Pause();
            if (musicSource != null) musicSource.Pause(); if (speechSource != null) speechSource.Stop();
            foreach (var source in voices) if (source != null) source.Stop();
        }
        else { ResetHistory(); foreach (var source in environmentSources) if (source != null) source.UnPause(); if (musicSource != null) musicSource.UnPause(); }
    }
    void OnDisable() { if (Ready) FlushSettings(); }
    void OnDestroy()
    {
        FlushSettings(); if (Instance == this) Instance = null;
        foreach (var clip in generatedClips) if (clip != null) Destroy(clip);
    }
}
