using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 所有声音均为 2D 展示；不会提交命令、访问权威存档或改变动画/规则时间。
public sealed class TabletopAudio : MonoBehaviour
{
    public const int VoiceLimit = 10;
    public const float EffectsHeadroom = .78f, AmbientHeadroom = .30f;
    const string PreferencePrefix = "catan.audio.v1.";
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
    public bool Muted { get => muted; set { muted = value; if (muted) { scheduled.Clear(); foreach (var voice in voices) if (voice != null) voice.Stop(); } SettingsChanged(); } }
    public bool MenuAmbienceActive => ambienceSource != null && ambienceSource.isPlaying && ambienceSource.volume > .0001f;
    readonly Dictionary<TabletopAudioCue, AudioClip> clips = new Dictionary<TabletopAudioCue, AudioClip>();
    readonly TabletopAudioModel model = new TabletopAudioModel();
    readonly TabletopAudioPublicModel publicModel = new TabletopAudioPublicModel();
    readonly TabletopAudioLimiter limiter = new TabletopAudioLimiter();
    struct Scheduled { internal TabletopAudioCue Cue; internal double At; }
    readonly List<Scheduled> scheduled = new List<Scheduled>();
    AudioSource[] voices = Array.Empty<AudioSource>();
    float[] voiceGains = Array.Empty<float>();
    AudioSource ambienceSource;
    AudioClip ambienceClip;
    M6App owner;
    AudioListener fallbackListener;
    float master, effects, ambient;
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
        muted = PlayerPrefs.GetInt(PreferencePrefix + "muted", 0) != 0;
        foreach (TabletopAudioCue cue in Enum.GetValues(typeof(TabletopAudioCue)))
        {
            float[] data = TabletopAudioSynth.Render(cue);
            var clip = AudioClip.Create("卡坦音效 " + cue, data.Length, 1, TabletopAudioSynth.SampleRate, false);
            clip.SetData(data, 0); clips.Add(cue, clip);
        }
        voices = new AudioSource[VoiceLimit];
        voiceGains = new float[VoiceLimit];
        for (int i = 0; i < voices.Length; i++) voices[i] = NewSource("音效声部 " + i, false);
        ambienceSource = NewSource("田园海岸环境", true);
        var environment = TabletopAudioSynth.RenderAmbience();
        ambienceClip = AudioClip.Create("田园海岸环境 v001", environment.Length, 1, TabletopAudioSynth.SampleRate, false);
        ambienceClip.SetData(environment, 0); ambienceSource.clip = ambienceClip; ambienceSource.volume = 0;
        fallbackListener = gameObject.AddComponent<AudioListener>(); fallbackListener.enabled = false;
        UpdateListener();
        gameObject.AddComponent<TabletopAudioGuiTap>().Initialize(this);
        Ready = true;
    }
    AudioSource NewSource(string label, bool loop)
    {
        var source = gameObject.AddComponent<AudioSource>(); source.playOnAwake = false;
        source.spatialBlend = 0; source.dopplerLevel = 0; source.loop = loop;
        source.priority = loop ? 220 : 100; source.volume = 0;
        return source;
    }
    public AudioClip GetClip(TabletopAudioCue cue) => clips.TryGetValue(cue, out var clip) ? clip : null;
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
        PlayerPrefs.SetInt(PreferencePrefix + "muted", muted ? 1 : 0); PlayerPrefs.Save(); settingsDirty = false;
    }
    void ApplyVolumes()
    {
        int count = Math.Max(1, voices.Count(v => v != null && v.isPlaying));
        for (int i = 0; i < voices.Length; i++) if (voices[i] != null)
        {
            voices[i].mute = muted || applicationPaused;
            // 最坏情况下所有采样同号，效果总和仍 <= .70*.78；环境另留 .70*.30。
            voices[i].volume = master * effects * voiceGains[i] * EffectsHeadroom / count;
        }
        if (ambienceSource != null) { ambienceSource.mute = muted || applicationPaused; ambienceSource.volume = master * ambient * ambientBlend * AmbientHeadroom; }
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
        if (!Ready || muted || applicationPaused || master <= 0 || effects <= 0 || !clips.TryGetValue(cue, out var clip)) return;
        double now = Time.unscaledTimeAsDouble;
        if (!limiter.Admit(cue, now)) return;
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
        ambienceSource.volume = master * ambient * ambientBlend * AmbientHeadroom;
        if (!ambienceSource.isPlaying && !applicationPaused) ambienceSource.Play();
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
            var publicEvents = publicModel.Observe(human, finished, barbarian, pirate, owner.ScenarioId == "pirate-islands", added.Count(e => e.Cue == TabletopAudioCue.BarbarianAdvance));
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
        var label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 12, alignment = TextAnchor.MiddleLeft };
        bool pastoral = owner != null && owner.MenuVisible && !owner.NetworkGame;
        label.normal.textColor = pastoral ? PastoralMenuUI.Ink : ModernOperationUI.TextColor;
        if (area.height < 70)
        {
            var muteRect = new Rect(area.x, area.y + 7, 66, 27);
            if (pastoral ? PastoralMenuUI.Button(muteRect, muted ? "已静音" : "声音", true, false, !muted) : ModernOperationUI.Button(muteRect, muted ? "已静音" : "声音", true, false, !muted)) { Muted = !muted; if (!muted) Click(); }
            float width = (area.width - 79) / 3;
            DrawCompactSlider(new Rect(area.x + 79, area.y + 2, width - 8, 36), "总音量", master, v => MasterVolume = v, label);
            DrawCompactSlider(new Rect(area.x + 79 + width, area.y + 2, width - 8, 36), "音效", effects, v => EffectsVolume = v, label);
            DrawCompactSlider(new Rect(area.x + 79 + width * 2, area.y + 2, width - 8, 36), "环境", ambient, v => AmbientVolume = v, label);
            return;
        }
        var title = new GUIStyle(label) { fontSize = 14, fontStyle = FontStyle.Bold };
        GUI.Label(new Rect(area.x, area.y, area.width - 100, 24), "声音设置", title);
        if (ModernOperationUI.Button(new Rect(area.xMax - 88, area.y, 88, 24), muted ? "声音已关" : "声音开启", true, false, !muted)) { Muted = !muted; if (!muted) Click(); }
        DrawSlider(new Rect(area.x, area.y + 29, area.width, 25), "总音量", master, v => MasterVolume = v, label);
        float half = (area.width - 16) / 2;
        DrawSlider(new Rect(area.x, area.y + 60, half, 25), "音效", effects, v => EffectsVolume = v, label);
        DrawSlider(new Rect(area.x + half + 16, area.y + 60, half, 25), "环境", ambient, v => AmbientVolume = v, label);
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
        if (paused) { scheduled.Clear(); FlushSettings(); if (ambienceSource != null) ambienceSource.Pause(); }
        else { ResetHistory(); if (ambienceSource != null) ambienceSource.UnPause(); }
    }
    void OnDisable() { if (Ready) FlushSettings(); }
    void OnDestroy()
    {
        FlushSettings(); if (Instance == this) Instance = null;
        foreach (var clip in clips.Values) if (clip != null) Destroy(clip);
        if (ambienceClip != null) Destroy(ambienceClip);
    }
}
