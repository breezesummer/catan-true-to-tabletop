using System;
using Catan.Core.ThreeKingdoms;
using UnityEngine;

public sealed partial class M6App
{
    ThreeKingdomsView skillView;
    B4SkillHistoryView playingSkill;
    GeneralSkinArt.Skin playingSkin;
    int nextSkill,observedCount;
    string observedTail,skillCaption;
    bool quietSkills;
    double skillStarted;
    float skillLength;

    internal string PlayingSkillEvent=>playingSkill?.EventId;
    internal float CentralSkillPhase=>playingSkill==null?0:(float)((Time.unscaledTimeAsDouble-skillStarted)/skillLength);
    internal float CentralSkillOpacity
    {
        get
        {
            if(playingSkill==null)return 0;
            float t=(float)(Time.unscaledTimeAsDouble-skillStarted);
            return Mathf.SmoothStep(0,1,Mathf.Min(t/.14f,(skillLength-t)/.18f));
        }
    }
    void ResetGeneralSkins()
    {
        skillView=null;playingSkill=null;playingSkin=null;observedCount=nextSkill=0;observedTail=null;
    }
    void UpdateGeneralSkins()
    {
        var view=ThreeKingdomsProjection;
        if(view==null){if(skillView!=null)ResetGeneralSkins();return;}
        var history=view.SkillHistory??Array.Empty<B4SkillHistoryView>();
        bool quiet=MenuVisible||NetworkGame&&Network.RestoringPlayerView;
        if(view!=skillView||quiet!=quietSkills)
        {
            bool baseline=quiet||quietSkills||skillView==null||history.Length<observedCount||
                observedCount>0&&history[observedCount-1].EventId!=observedTail;
            if(baseline){nextSkill=history.Length;playingSkill=null;}
            quietSkills=quiet;skillView=view;
            observedCount=history.Length;observedTail=observedCount==0?null:history[observedCount-1].EventId;
            foreach(var player in view.Players)
            {
                if(player.FaceDown)continue;
                var skin=GeneralSkinArt.Get(player.ActiveGeneralId);
                if(skin==null)continue;
                GeneralSkinArt.IdleReady(skin);GeneralSkinArt.AttackReady(skin);
            }
        }
        if(quiet)return;
        if(playingSkill!=null&&Time.unscaledTimeAsDouble-skillStarted<skillLength)return;
        playingSkill=null;
        // 直接沿当前玩家的公开历史推进。连锁发动按序播放，不另存一份事件队列。
        if(nextSkill>=history.Length)return;
        var entry=history[nextSkill];
        var general=GeneralCatalog.Get(entry.GeneralId);
        if(general==null||string.IsNullOrEmpty(entry.SkillId)){nextSkill++;return;}
        var selected=GeneralSkinArt.Get(entry.GeneralId);
        if(!GeneralSkinArt.AttackReady(selected))return;
        var skill=Array.Find(general.Skills,s=>s.Id==entry.SkillId);
        skillCaption=general.Name+" · "+(skill?.Name??"技能");
        playingSkill=entry;playingSkin=selected;skillStarted=Time.unscaledTimeAsDouble;
        TabletopAudio.Instance?.PlayEffectClip(GeneralSkinArt.AttackSound(selected).asset as AudioClip,1,85);
        skillLength=GeneralSkinArt.AttackSeconds(selected);
        if(history.Length-nextSkill>1)skillLength=Mathf.Min(skillLength,1.25f);
        nextSkill++;
    }
    void DrawCentralSkill()
    {
        if(playingSkill==null||Event.current.type!=EventType.Repaint)return;
        float opacity=CentralSkillOpacity;
        if(opacity<=0)return;
        var old=GUI.color;bool enabled=GUI.enabled;GUI.enabled=true;
        GUI.color=new Color(old.r,old.g,old.b,old.a*opacity);
        GeneralSkinArt.Attack(playingSkin,CentralSkillPhase);
        var area=GeneralSkinArt.CenterArea;
        GeneralFrameUI.Text(new Rect(area.x,area.yMax-48,area.width,32),skillCaption,24);
        GUI.color=old;GUI.enabled=enabled;
    }
}
