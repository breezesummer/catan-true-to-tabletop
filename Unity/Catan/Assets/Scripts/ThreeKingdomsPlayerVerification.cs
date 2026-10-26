using System;
using System.Collections;
using System.IO;
using System.Linq;
using Catan.AI;
using Catan.Core.ThreeKingdoms;
using UnityEngine;
using UnityEngine.Rendering;

// 仅 --three-kingdoms-verify 启用；用产品本地入口和所属席位投影，不把规则测试夹具注入产品。
public sealed class ThreeKingdomsPlayerVerification : MonoBehaviour
{
    [Serializable] sealed class Result
    {
        public string status,unity,note,packageVersion;
        public int configurations,saveRoundtrips,retirements,restarts,images,guiImagesWithChanges,skillChoices;
        public bool graphics,fullUiPixelsVerified;
    }
    M6App app;
    string directory,stage="启动",captureName;
    readonly Result result=new Result();
    readonly CitiesKnightsAi cityPolicy=new CitiesKnightsAi();
    readonly CombinedAi combinedPolicy=new CombinedAi();
    RenderTexture target,previousActive;
    Color32[] cameraPixels;
    bool captureBegan;
    M4App City=>GetComponent<M4App>();
    M5App Combined=>GetComponent<M5App>();
    ThreeKingdomsView View=>app.RuleSet=="cities-knights"?City.M6View.ThreeKingdoms:Combined.M6View.ThreeKingdoms;
    TabletopScene Table=>app.RuleSet=="cities-knights"?City.VerificationBoard.TableScene:Combined.VerificationBoard.TableScene;
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    void Start()
    {
        var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"--three-kingdoms-verify");
        if(flag<0)return;
        if(flag+1>=args.Length){Debug.LogError("CATAN_THREE_KINGDOMS_PLAYER_FAILED: 缺少证据目录。");Application.Quit(1);return;}
        directory=Path.GetFullPath(args[flag+1]);Directory.CreateDirectory(directory);app=GetComponent<M6App>();
        result.graphics=SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null;
        if(result.graphics)gameObject.AddComponent<ThreeKingdomsCaptureEnd>().Owner=this;
        StartCoroutine(Run());
    }
    IEnumerator Run()
    {
        var work=Verify();
        while(true)
        {
            object next;
            try{if(!work.MoveNext())break;next=work.Current;}
            catch(Exception ex){Debug.LogError("CATAN_THREE_KINGDOMS_PLAYER_FAILED: "+stage+" / "+ex.GetType().Name+" / "+ex.Message);Application.Quit(1);yield break;}
            yield return next;
        }
        result.status="通过";result.unity=Application.unityVersion;
        result.note="实际 Windows 本地入口覆盖 M4/M5 各 3/4 人、选将/登场/阶段/轮换/技能选择读档及本地重开。截图为游戏相机与 IMGUI 离屏合成，须人工查看；不代表真机或用户试玩验收。";
        File.WriteAllText(Path.Combine(directory,"result.json"),JsonUtility.ToJson(result,true));
        Debug.Log("CATAN_THREE_KINGDOMS_PLAYER_OK: 4 种本地配置；私密选将；阶段/轮换/技能恢复；重复隐退；保留开关重开。");
        Application.Quit(0);
    }
    IEnumerator Verify()
    {
        foreach(var mode in new[]{"cities-knights","combined"})
        foreach(int players in new[]{3,4})
        {
            string prefix=mode+"-"+players;stage=prefix+" 建局";
            app.StartGame(mode,"heading-for-new-shores",players,"P1",true,threeKingdoms:true);
            yield return null;yield return null;
            Require(app.Ready&&app.ThreeKingdoms&&app.Paused&&View!=null,"三国开关没有进入本地权威局面。");
            Require(View.SkillsEnabled&&View.PackageVersion==GeneralCatalog.B6PackageVersion&&GeneralCatalog.B6Definitions.Length==54,"新局没有使用完整 B6 五十四将包。");
            result.packageVersion=View.PackageVersion;
            int budget=0;
            while(View.PendingDecision?.PlayerId!=app.HumanSeat)
            {Require(++budget<20,"开局轮到人类前未完成自动选将。");Advance();yield return null;}
            CheckPrivacy();
            foreach(string checkpoint in new[]{"开局候选","开局登场"})
            {
                stage=prefix+" "+checkpoint;
                string before=SaveAndRestore(prefix+"-"+checkpoint);yield return null;yield return null;AssertRestored(before);
                if(checkpoint=="开局候选")
                {
                    app.VerificationOpenThreeKingdomsPanel();yield return null;
                    if(result.graphics){BeginCapture(prefix+"-opening.png");for(int frame=0;target!=null&&frame<12;frame++)yield return null;EndCapture();}
                }
                Advance();yield return null;
            }
            budget=0;
            while(View.Clock.Phase!=ThreeKingdomsPhase.Play||View.Clock.ActivePlayerId!=app.HumanSeat)
            {
                Require(++budget<800,"初始摆放或阶段自动推进未在预算内完成。");
                while(app.TableDiceRolling||app.TablePresentationBlocked)yield return null;
                Advance();yield return null;
            }
            stage=prefix+" 出牌阶段";
            string stageBefore=SaveAndRestore(prefix+"-阶段");yield return null;yield return null;AssertRestored(stageBefore);
            Require(View.Players.All(p=>p.ActiveGeneralId!=null&&p.RemainingLife==3)&&View.Players.All(p=>p.HiddenCount==1),"首轮公开武将与暗置数量不正确。");
            Require(View.PoolCount==54-players*2,"正常选将没有保留 B6 五十四将实体总数。");
            app.VerificationOpenThreeKingdomsPanel();yield return null;
            if(result.graphics){BeginCapture(prefix+"-public-life.png");for(int frame=0;target!=null&&frame<12;frame++)yield return null;EndCapture();}
            for(int repeat=0;repeat<2;repeat++)
            {
                stage=prefix+" 隐退 "+repeat;Require(View.CanRetire,"出牌阶段没有合法隐退入口。");Retire();yield return null;result.retirements++;
                foreach(string checkpoint in new[]{"补将","轮换登场"})
                {
                    string before=SaveAndRestore(prefix+"-"+repeat+"-"+checkpoint);yield return null;yield return null;AssertRestored(before);CheckPrivacy();
                    if(checkpoint=="轮换登场"&&repeat==1&&result.graphics)
                    {app.VerificationOpenThreeKingdomsPanel();yield return null;BeginCapture(prefix+"-replacement.png");for(int frame=0;target!=null&&frame<12;frame++)yield return null;EndCapture();}
                    Advance();yield return null;
                }
                Require(View.Clock.Phase==ThreeKingdomsPhase.Play&&View.CanRetire,"轮换没有返回原出牌阶段或错误限制重复隐退。");
            }
            stage=prefix+" 技能选择";budget=0;int skillSearchRetirements=0;
            while(!app.IsHumanTurn||View.SkillActions==null||View.SkillActions.Length==0)
            {
                Require(++budget<1500,"正常对局未在预算内出现人类技能选项。");
                while(app.TableDiceRolling||app.TablePresentationBlocked)yield return null;
                // 正常候选可能连续出现纯被动武将；使用产品允许的轮换查找可操作技能，避免为一次按钮验证跑完整局。
                if(View.CanRetire&&View.Clock.ActivePlayerId==app.HumanSeat&&View.SkillDecision==null&&View.PendingDecision==null&&skillSearchRetirements<12)
                {Retire();skillSearchRetirements++;result.retirements++;}
                else Advance();
                yield return null;
            }
            string skillBefore=SaveAndRestore(prefix+"-技能");yield return null;yield return null;AssertRestored(skillBefore);
            app.VerificationOpenThreeKingdomsPanel();yield return null;
            if(result.graphics){BeginCapture(prefix+"-skill.png");for(int frame=0;target!=null&&frame<12;frame++)yield return null;EndCapture();}
            string priorSkill=app.ExportEnvelope();app.VerificationChooseGeneralSkill(0);yield return null;
            Require(app.ExportEnvelope()!=priorSkill,"界面技能按钮没有提交真实合法动作。");result.skillChoices++;
            string skillAfter=SaveAndRestore(prefix+"-技能提交后");yield return null;yield return null;AssertRestored(skillAfter);CheckPrivacy();
            stage=prefix+" 本地重开";app.RestartLocalGame();yield return null;yield return null;
            Require(app.ThreeKingdoms&&app.PlayerCount==players&&app.RuleSet==mode&&View.Clock.Phase==ThreeKingdomsPhase.OpeningSelection,"本地重开丢失三国配置。");result.restarts++;
            result.configurations++;
        }
    }
    string SaveAndRestore(string name)
    {
        string before=app.ExportEnvelope(),path=Path.Combine(directory,name+".json");app.WriteSave(path);app.ReadSave(path);return before;
    }
    void AssertRestored(string before){Require(app.ExportEnvelope()==before&&app.ThreeKingdoms,"保存恢复改变了权威状态、阶段或三国配置。");result.saveRoundtrips++;}
    void CheckPrivacy()
    {
        string asset=app.RuleSet=="cities-knights"?City.M6View.PlayerId:Combined.M6View.PlayerId;
        Require(View.Players.Where(p=>p.PlayerId!=asset).All(p=>p.HiddenGeneralIds.Length==0),"产品视图出现对手暗置身份。");
        if(View.PendingDecision!=null&&View.PendingDecision.PlayerId!=asset)Require(View.PendingDecision.CandidateIds.Length==0,"产品视图出现对手候选。");
        if(View.SkillDecision!=null&&View.SkillDecision.PlayerId!=asset)Require(View.SkillActions.Length==0,"产品视图出现其他玩家的技能私密选项。");
    }
    void Advance()
    {
        if(app.IsHumanTurn||View.PendingDecision?.PlayerId==app.HumanSeat)
        {
            if(app.RuleSet=="cities-knights")
            {var command=cityPolicy.Decide(City.M6View);command.Id=Guid.NewGuid().ToString("N");Require(City.Submit(command)?.Success==true,"人类城市骑士入口拒绝合法命令。");}
            else
            {var command=combinedPolicy.Decide(Combined.M6View);command.Id=Guid.NewGuid().ToString("N");Require(Combined.Submit(command)?.Success==true,"人类组合入口拒绝合法命令。");}
        }
        else {app.SetPaused(false);bool advanced=app.StepAi();app.SetPaused(true);Require(advanced&&app.Fault.Length==0,"产品 AI 未推进合法三国决策。");}
        CheckPrivacy();
    }
    void Retire()
    {
        if(app.RuleSet=="cities-knights")Require(City.Submit(new Catan.Core.M4.Command{PlayerId=app.HumanSeat,Kind=Catan.Core.M4.CommandKind.RetireGeneral,WindowId=View.WindowId})?.Success==true,"主动隐退失败。");
        else Require(Combined.Submit(new Catan.Core.M5.Command{PlayerId=app.HumanSeat,Kind=Catan.Core.M5.CommandKind.RetireGeneral,WindowId=View.WindowId})?.Success==true,"主动隐退失败。");
    }
    void BeginCapture(string name)
    {
        captureName=name;target=new RenderTexture(1440,900,24,RenderTextureFormat.ARGB32);target.Create();Table.RenderComposite(target);
        var old=RenderTexture.active;RenderTexture.active=target;
        var cameraImage=new Texture2D(1440,900,TextureFormat.RGB24,false);cameraImage.ReadPixels(new Rect(0,0,1440,900),0,0);cameraImage.Apply();cameraPixels=cameraImage.GetPixels32();Destroy(cameraImage);RenderTexture.active=old;
        Require(cameraPixels.Count(p=>p.r>12||p.g>12||p.b>12)>cameraPixels.Length/2,"离屏相机为空或黑屏。");
    }
    void OnGUI()
    {
        GUI.depth=30000;if(target==null||Event.current.type!=EventType.Repaint)return;
        previousActive=RenderTexture.active;Graphics.SetRenderTarget(target);captureBegan=true;
    }
    internal void FinishGuiCapture()
    {
        if(target==null||!captureBegan||Event.current.type!=EventType.Repaint)return;
        Texture2D image=null;
        try
        {
            RenderTexture.active=target;image=new Texture2D(1440,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1440,900),0,0);image.Apply();
            var pixels=image.GetPixels32();int changed=0;
            for(int i=0;i<pixels.Length;i++)if(Math.Abs(pixels[i].r-cameraPixels[i].r)+Math.Abs(pixels[i].g-cameraPixels[i].g)+Math.Abs(pixels[i].b-cameraPixels[i].b)>45)changed++;
            if(changed>5000)result.guiImagesWithChanges++;
            File.WriteAllBytes(Path.Combine(directory,captureName),image.EncodeToPNG());result.images++;
            Debug.Log("CATAN_THREE_KINGDOMS_IMAGE: "+captureName+"; IMGUI 差异像素 "+changed+"，须人工检查。");
        }
        finally{if(image!=null)Destroy(image);EndCapture();}
    }
    void EndCapture(){if(captureBegan)RenderTexture.active=previousActive;if(target!=null){target.Release();Destroy(target);}target=null;captureBegan=false;}
}

public sealed class ThreeKingdomsCaptureEnd : MonoBehaviour
{
    internal ThreeKingdomsPlayerVerification Owner;
    void OnGUI(){GUI.depth=-30000;if(Owner!=null)Owner.FinishGuiCapture();}
}
