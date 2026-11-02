using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class TabletopAudio
{
    [Serializable] public sealed class GeneralVoiceEntry { public string skillId; public string[] clips; }
    [Serializable] public sealed class GeneralVoiceManifest { public GeneralVoiceEntry[] entries; }
    readonly GeneralVoiceCursor generalVoiceCursor=new GeneralVoiceCursor();
    readonly Dictionary<string,string[]> generalVoicePaths=new Dictionary<string,string[]>();
    readonly Dictionary<string,AudioClip> generalVoiceClips=new Dictionary<string,AudioClip>();
    readonly Queue<GeneralSpeech> generalSpeech=new Queue<GeneralSpeech>();
    readonly System.Random generalVoiceRandom=new System.Random();
    struct GeneralSpeech { public string Path; public double At; }
    ResourceRequest generalVoiceLoad;
    GeneralSpeech loadingSpeech;
    bool generalVoiceManifestLoaded;
    public int PlayedGeneralVoiceCount { get; private set; }
    void ResetGeneralVoices()
    {
        generalVoiceCursor.Reset();generalSpeech.Clear();generalVoiceLoad=null;
        if(speechSource!=null)speechSource.Stop();
    }
    void ObserveGeneralVoices(double now,bool quiet)
    {
        if(!generalVoiceManifestLoaded)
        {
            generalVoiceManifestLoaded=true;
            var text=Resources.Load<TextAsset>("GeneralUI/v001/skill-voices");
            if(text!=null)foreach(var entry in JsonUtility.FromJson<GeneralVoiceManifest>(text.text).entries)
                generalVoicePaths[entry.skillId]=entry.clips;
        }
        generalVoiceCursor.Observe(owner.ThreeKingdomsProjection?.SkillHistory,quiet,skill=>
        {
            if(muted||applicationPaused||master<=0||speech<=0||!generalVoicePaths.TryGetValue(skill,out var paths)||paths.Length==0)return;
            if(generalSpeech.Count>=32)generalSpeech.Dequeue();
            generalSpeech.Enqueue(new GeneralSpeech{Path=paths[generalVoiceRandom.Next(paths.Length)],At=now});
        });
        if(quiet||muted||applicationPaused||master<=0||speech<=0)
        {generalSpeech.Clear();generalVoiceLoad=null;return;}
        if(speechSource==null||speechSource.isPlaying)return;
        if(generalVoiceLoad!=null)
        {
            if(!generalVoiceLoad.isDone)return;
            var clip=generalVoiceLoad.asset as AudioClip;generalVoiceLoad=null;
            if(clip!=null){generalVoiceClips[loadingSpeech.Path]=clip;if(now-loadingSpeech.At<12)PlayGeneralClip(clip);}
            return;
        }
        while(generalSpeech.Count>0)
        {
            var next=generalSpeech.Dequeue();if(now-next.At>=12)continue;
            if(generalVoiceClips.TryGetValue(next.Path,out var clip))PlayGeneralClip(clip);
            else {loadingSpeech=next;generalVoiceLoad=Resources.LoadAsync<AudioClip>(next.Path);}
            break;
        }
    }
    void PlayGeneralClip(AudioClip clip)
    {
        speechSource.clip=clip;speechSource.pitch=1;ApplyVolumes();speechSource.Play();PlayedGeneralVoiceCount++;
    }
}
