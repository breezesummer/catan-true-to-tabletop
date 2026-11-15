using System;
using System.Collections;
using System.Linq;
using Catan.Core.ThreeKingdoms;
using UnityEngine;

// 显式验收入口，界面草稿只改本机投影；对局由隔离的本地宿主创建。
public sealed partial class InteractionPresentationVerification
{
    IEnumerator HoldCurrentFeedback()
    {
        if(!Environment.GetCommandLineArgs().Contains("--feedback-review-interactive"))yield break;
        while(!Input.GetKeyDown(KeyCode.Space))yield return null;
    }
    IEnumerator VerifyCurrentFeedback()
    {
        while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
        if(Environment.GetCommandLineArgs().Contains("--selection-matrix-review")){yield return VerifySelectionMatrix();yield break;}
        if(Environment.GetCommandLineArgs().Contains("--hand-selection-review")){yield return VerifyHandSelectionReview();yield break;}
        if(!Environment.GetCommandLineArgs().Contains("--feedback-visual-only")){
            yield return VerifyBasicCardAction();
            if(!Environment.GetCommandLineArgs().Contains("--feedback-preview-only")){
                yield return VerifyImmediateProgressActions();
                yield return VerifyFeedback4();yield return VerifyFeedback5();
            }
        }
        stage="全模式界面反馈";
        app.StartGame("combined","through-the-desert",4,"P1",true,threeKingdoms:true,harbormaster:true,fishing:true);
        yield return null;yield return null;
        var game=GetComponent<M5App>();var host=game.VerificationHost;var policy=new Catan.AI.CombinedAi();
        for(int i=0;i<250;i++)
        {
            var v=host.View("P1");
            if(v.Turn>=4&&v.ActivePlayerId=="P1"&&v.Phase==Catan.Core.M5.GamePhase.Action&&v.ThreeKingdoms.SkillDecision==null)break;
            var actor=v.ThreeKingdoms.WaitingSelectionPlayerIds.FirstOrDefault()??M5App.Actor(v);
            var command=policy.Decide(host.AiView(actor));Require(command!=null,"验收局无法推进");command.Id=Guid.NewGuid().ToString("N");
            Require(host.Submit(command).Success,"验收命令失败");yield return null;
        }
        game.M6Refresh();table=game.VerificationBoard.TableScene;yield return SettleFeedback();
        typeof(M6App).GetMethod("ResetGeneralSkins",Private).Invoke(app,null);yield return null;
        string authority=host.ExportSave();
        app.OpenTableMenu();yield return HoldCurrentFeedback();yield return Capture("局内菜单-缩小三分之一");app.HandleTableEscape();
        var selection=(B45SelectionContext)typeof(M6App).GetField("generalSelection",Private).GetValue(app);
        var view=game.M6View.ThreeKingdoms;
        view.PendingDecision=null;
        foreach(int count in new[]{5,8})
        {
            view.SkillDecision=new B2SkillDecision{PlayerId="P1",Kind="mixed",DecisionId="review-bank",Title="取牌"};
            view.SkillActions=Enumerable.Range(0,count).Select(t=>new B2SkillAction{Command=new B2SkillCommand{SkillId="tk-g20-s2",Option="card",CardType=t,WindowId=view.WindowId,DecisionId="review-bank"},RequiredCardCount=2,AvailableCount=12}).ToArray();
            selection.Begin(view,"P1",null,new int[8]);yield return null;
            Require(selection.RequiresBankCards&&selection.RequiredBankCount==2,"共用取牌草稿不正确");
            yield return HoldCurrentFeedback();yield return Capture("共用取牌框-"+count+"种");
        }
        selection.Reset();game.M6Refresh();yield return null;
        Require(authority==host.ExportSave(),"界面验收改变了权威存档");
        var audio=Resources.Load<AudioClip>(TabletopAudio.EffectPath(TabletopAudioCue.RoadBuild));
        var legacy=Resources.Load<AudioClip>("TabletopAudio/v003/Effects/RoadBuild");
        Require(audio!=null&&legacy!=null&&audio.samples==legacy.samples&&audio.length>.3f,"修路实录资源未统一");
        var gallery=gameObject.AddComponent<CurrentFeedbackCardGallery>();
        for(int page=0;page<3;page++){gallery.Page=page;yield return null;yield return HoldCurrentFeedback();yield return Capture("发展牌排版-"+(page+1));}
        Destroy(gallery);
    }
}

public sealed class CurrentFeedbackCardGallery:MonoBehaviour
{
    public int Page;
    void OnGUI()
    {
        GUI.depth=-29000;var matrix=GUI.matrix;
        GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1440f,Screen.height/900f,1));
        GUI.DrawTexture(new Rect(0,0,1440,900),Texture2D.blackTexture);
        for(int i=0;i<10;i++)
        {
            int kind=Page*10+i;
            var face=kind<25?EntityCardAssets.ProgressFront(kind):RedrawnCardAssets.Base(kind-25);
            EntityCardAssets.DrawCardFace(new Rect(56+i%5*273,24+i/5*440,244,408),face);
        }
        GUI.matrix=matrix;
    }
}
