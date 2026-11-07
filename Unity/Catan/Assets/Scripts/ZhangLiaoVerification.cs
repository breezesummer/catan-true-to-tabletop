using System;
using System.Collections;
using System.IO;
using System.Linq;
using Catan.Core.M5;
using UnityEngine;

public sealed partial class TabletopUiVerification
{
    IEnumerator VerifyZhangLiao(string[] args)
    {
        mode="combined";
        int flag=Array.IndexOf(args,"--zhang-liao");
        auditFixtures=args[flag+1];
        app.ReadSave(args[flag+2]);yield return null;yield return null;
        Require(app.RuleSet=="combined"&&app.ThreeKingdoms&&app.Harbormaster&&app.Fishing&&
            app.PlayerCount==4&&app.ScenarioId=="through-the-desert","张辽验收对局配置错误");
        auditGame=GetComponent<M5App>();
        auditSession=(CombinedGameSession)typeof(M5LocalGameHost).GetField("session",PrivateInstance).GetValue(auditGame.VerificationHost);
        var state=Fixture<GameState>("tk-g01-s1","input");
        ActivateAudit(state);
        if(args.Contains("--skin-catalog"))
        {
            var check=VerifySkinCatalog(args.Contains("--skin-clicks"));
            while(check.MoveNext())yield return check.Current;
        }
        double timeout=Time.realtimeSinceStartupAsDouble+15;
        var zhang=GeneralSkinArt.Get("tk-g01");
        while((!zhang.useStatic&&!GeneralSkinArt.IdleReady(zhang))||!GeneralSkinArt.AttackReady(zhang))
        {
            Require(Time.realtimeSinceStartupAsDouble<timeout,"张辽动皮未加载");
            yield return null;
        }
        var view=app.ThreeKingdomsProjection;
        var own=view.Players.Single(p=>p.PlayerId==app.HumanSeat);
        Require(own.ActiveGeneralId==zhang.id&&!own.FaceDown,"本席未明置张辽");
        int option=Array.FindIndex(view.SkillActions,a=>a.Command.Option=="raid"&&a.Command.Counts.Sum()==2);
        Require(option>=0,"缺少少收两张的突袭行动");
        while(app.OpeningOrderMessage.Length>0)yield return null;
        bool sent=false,seen=false,returned=false;
        var audio=TabletopAudio.Instance;
        bool audible=audio!=null&&!audio.Muted&&audio.MasterVolume>0&&audio.EffectsVolume>0;
        var attackClip=GeneralSkinArt.AttackSound(zhang).asset as AudioClip;
        bool heard=false;
        var frames=Directory.CreateDirectory(Path.Combine(output,"frames"));
        double start=Time.realtimeSinceStartupAsDouble,next=start;
        int frame=0;
        while(Time.realtimeSinceStartupAsDouble-start<6.7)
        {
            yield return new WaitForEndOfFrame();
            double now=Time.realtimeSinceStartupAsDouble;
            if(now<next)continue;
            next=now+1/20.0;
            if(!sent&&now-start>2.7)
            {
                app.VerificationChooseGeneralSkill(option);
                sent=true;
            }
            var shot=ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                File.WriteAllBytes(Path.Combine(frames.FullName,$"{frame++:D3}-{(int)((now-start)*1000):D4}.jpg"),shot.EncodeToJPG(90));
            }
            finally{Destroy(shot);}
            float opacity=app.CentralSkillOpacity;
            if(sent&&!heard&&audible)heard=audio.GetComponents<AudioSource>().Any(s=>s.isPlaying&&s.clip==attackClip);
            if(frame==1)SaveOceanFrame("01-框内待机.png");
            if(!seen&&opacity>.99f){SaveOceanFrame("02-突袭出框.png");seen=true;}
            if(seen&&!returned&&opacity<=0){SaveOceanFrame("03-回到待机.png");returned=true;}
        }
        var history=auditSession.GetAuthoritativeStateForTesting().ThreeKingdoms.SkillHistory;
        Require(seen&&returned,"突袭未完成出框与回框");
        Require(!audible||heard,"突袭音效未在效果声道播放");
        if(heard)Debug.Log("CATAN_SKIN_AUDIO_OK: 张辽攻击原声随突袭播放。");
        Require(history.Skip(view.SkillHistory.Length).Count(h=>h.PlayerId==own.PlayerId&&
            h.SkillId=="tk-g01-s1"&&h.Complete&&h.PublicResult.Contains("实际获得 2 张"))==1,"突袭未按一次发动取得两张手牌");
        Debug.Log("张辽动皮实机通过：四人双扩展三国海港捕鱼穿越沙漠，突袭实际获得两张手牌，已回到待机。");
        configurations++;
    }

    IEnumerator VerifySkinCatalog(bool click)
    {
        int count=0;
        foreach(var general in Catan.Core.ThreeKingdoms.GeneralCatalog.B6Definitions)
        {
            var skin=GeneralSkinArt.Get(general.Id);
            if(skin==null)continue;
            bool saved=skin.useStatic;
            try
            {
                GeneralSkinArt.SetStatic(skin,false);
                double until=Time.realtimeSinceStartupAsDouble+15;
                while(!GeneralSkinArt.IdleReady(skin)||!GeneralSkinArt.AttackReady(skin))
                {
                    Require(Time.realtimeSinceStartupAsDouble<until,"动皮资源缺失 "+skin.id);yield return null;
                }
                Require(skin.idleTime>0&&skin.attackTime>0&&skin.idleFrames>0,"动皮时长或帧数异常 "+skin.id);
                Require(GeneralSkinArt.AttackSound(skin).asset is AudioClip sound&&sound.length>0,"攻击音效缺失 "+skin.id);
                count++;
                if(skin.id!="tk-g31")continue;
                var detail=typeof(M6App).GetField("detailGeneralId",PrivateInstance);
                detail.SetValue(app,skin.id);yield return null;yield return new WaitForEndOfFrame();
                SaveOceanFrame("李儒-动皮.png");
                if(click){foreach(var step in DirectClick(new Vector2(557,286),"切换静皮"))yield return step;}
                else GeneralSkinArt.SetStatic(skin,true);
                Require(skin.useStatic&&!GeneralSkinArt.IdleReady(skin)&&GeneralSkinArt.AttackReady(skin),"静皮切换影响公开技能特效");
                yield return null;yield return new WaitForEndOfFrame();SaveOceanFrame("李儒-静皮.png");
                if(click)
                {
                    foreach(var step in DirectClick(new Vector2(557,286),"切换动皮"))yield return step;
                    Require(!skin.useStatic&&GeneralSkinArt.IdleReady(skin),"动皮按钮未恢复待机");
                }
                detail.SetValue(app,null);
            }
            finally{GeneralSkinArt.SetStatic(skin,saved);}
        }
        Require(count==33,"匹配动皮数量错误");
        Debug.Log("CATAN_GENERAL_SKINS_OK: 33 套待机与攻击资源通过，李儒动静皮切换通过。");
    }
}
