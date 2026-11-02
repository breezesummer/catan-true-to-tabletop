using System;
using System.Linq;
using Catan.Core.ThreeKingdoms;
using UnityEditor;
using UnityEngine;

public static class GeneralMediaVerification
{
    public static void Run()
    {
        var manifest=JsonUtility.FromJson<TabletopAudio.GeneralVoiceManifest>(Resources.Load<TextAsset>("GeneralUI/v001/skill-voices").text);
        var skills=GeneralCatalog.B6Definitions.SelectMany(g=>g.Skills).ToArray();
        if(manifest.entries.Length!=skills.Length)throw new Exception("技能语音数量不匹配");
        int clips=0;
        foreach(var g in GeneralCatalog.B6Definitions)
        {
            var t=Resources.Load<Texture2D>("GeneralUI/v001/Portraits/"+g.Id);
            if(t==null||t.width<100||t.height<100)throw new Exception("武将皮肤缺失："+g.Id);
        }
        foreach(var skill in skills)
        {
            var entry=manifest.entries.Single(e=>e.skillId==skill.Id);
            if(entry.clips.Length==0)throw new Exception("技能无语音："+skill.Id);
            foreach(var path in entry.clips)
            {
                var clip=Resources.Load<AudioClip>(path);
                if(clip==null||clip.length<=0||clip.samples<=0)throw new Exception("语音导入失败："+path);
                clips++;
            }
        }
        Debug.Log("CATAN_GENERAL_MEDIA_OK: 54张皮肤，"+skills.Length+"技能，"+clips+"段语音");
    }
}
