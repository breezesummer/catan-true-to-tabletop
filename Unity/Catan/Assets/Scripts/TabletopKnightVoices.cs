using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class TabletopAudio
{
    readonly AudioClip[] knightVoiceClips = new AudioClip[KnightVoiceCatalog.ClipCount];
    readonly HashSet<AudioClip> knightVoiceAssets = new HashSet<AudioClip>();
    readonly KnightVoiceVariants knightVoiceVariants = new KnightVoiceVariants();
    readonly KnightVoiceQueue knightVoiceQueue = new KnightVoiceQueue();
    public bool KnightVoicesLoaded { get; private set; }
    public int VoiceClipCount => knightVoiceClips.Count(clip => clip != null);
    public int PlayedKnightVoiceCount { get; private set; }
    public int PendingKnightVoiceCount => knightVoiceQueue.Count;
    public string LastKnightVoicePath { get; private set; }
    float SpeechClipGain => speechSource != null && speechSource.clip != null && knightVoiceAssets.Contains(speechSource.clip) ? KnightVoiceCatalog.Gain : 1;
    IEnumerator LoadKnightVoices()
    {
        for (int level = 1; level <= 3; level++)
        for (int action = 1; action <= 6; action++)
        for (int variant = 0; variant < 2; variant++)
        {
            var kind = (KnightVoiceAction)action;
            var load = Resources.LoadAsync<AudioClip>(KnightVoiceCatalog.Path(kind, level, variant));
            yield return load;
            var clip = load.asset as AudioClip;
            knightVoiceClips[KnightVoiceCatalog.Index(kind, level, variant)] = clip;
            if (clip != null) knightVoiceAssets.Add(clip);
        }
        KnightVoicesLoaded = true;
    }
    public AudioClip GetKnightVoiceClip(KnightVoiceAction action, int level, int variant = 0)
    {
        int index = KnightVoiceCatalog.Index(action, level, variant);
        return index < 0 ? null : knightVoiceClips[index];
    }
    void ResetKnightVoices() => knightVoiceQueue.Clear();
    internal void QueueKnightVoice(KnightVoiceSelection voice, double now)
    {
        if (!Ready || muted || applicationPaused || master <= 0 || speech <= 0) return;
        knightVoiceQueue.Enqueue(voice, now);
    }
    void PumpKnightVoices(double now)
    {
        if (muted || applicationPaused || master <= 0 || speech <= 0) { ResetKnightVoices(); return; }
        if (!KnightVoicesLoaded || speechSource == null || speechSource.isPlaying) return;
        if (!knightVoiceQueue.TryTake(now, out var next)) return;
        var voice = next.Voice;
        int variant = knightVoiceVariants.Take(voice.Action, voice.Level);
        var clip = GetKnightVoiceClip(voice.Action, voice.Level, variant);
        if (clip == null) return;
        speechSource.clip = clip; speechSource.pitch = 1;
        musicDuck = Mathf.Min(musicDuck, .4f); ApplyVolumes(); speechSource.Play();
        LastKnightVoicePath = KnightVoiceCatalog.Path(voice.Action, voice.Level, variant);
        PlayedKnightVoiceCount++;
    }
}
