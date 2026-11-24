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
        bool responses=Environment.GetCommandLineArgs().Contains("--skill-response-review");
        if(!responses&&!Environment.GetCommandLineArgs().Contains("--feedback-visual-only")){
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
        if(responses){yield return VerifySkillResponses(game);yield break;}
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

    IEnumerator VerifySkillResponses(M5App game)
    {
        var host=game.VerificationHost;
        var session=(Catan.Core.M5.CombinedGameSession)typeof(M5LocalGameHost).GetField("session",Private).GetValue(host);
        var state=session.GetAuthoritativeStateForTesting();
        MatrixPrepare(session,state,new MatrixCase("自立","tk-g50","tk-g50-s1",null));
        var tk=state.ThreeKingdoms;
        var owner=tk.Generals.Single(g=>g.OwnerPlayerId=="P1"&&g.Region==GeneralRegion.Active);
        var cards=(IThreeKingdomsCardHost)typeof(Catan.Core.M5.CombinedGameSession).GetMethod("CardHost",Private).Invoke(session,new object[]{state});
        tk.Skills.ProcessedEvents=tk.Skills.ProcessedEvents.Where(id=>id!="b6-alliance-entry:"+owner.TenureId).ToArray();
        B2SkillEngine.OnAllianceB6GeneralActivated(tk,owner);
        B2SkillEngine.Pump(cards);
        var first=B2SkillEngine.Actions(cards,"P1").Single(a=>a.Label.Contains("自立"));
        B2SkillEngine.Resolve(cards,"P1",first.Command);
        string applicant=tk.Skills.PendingDecision.PlayerId;
        void ShowSeat(string id)
        {
            typeof(M6App).GetProperty("HumanSeat").SetValue(app,id);
            game.M6Refresh();
        }
        IEnumerator ClickStep(string name,Func<bool> complete)
        {
            stage=name;yield return SettleFeedback();yield return new WaitForSecondsRealtime(.4f);
            yield return Capture(name);
            double until=Time.realtimeSinceStartupAsDouble+55;
            while(!complete()&&Time.realtimeSinceStartupAsDouble<until)yield return null;
            Require(complete(),"点击后未完成"+name);
        }
        string Kind()=>game.M6View.ThreeKingdoms.SkillDecision?.Kind;
        int wood=host.View("P1").OwnResources.Wood;
        ShowSeat(applicant);
        yield return ClickStep("01-请求结盟",()=>game.M6View.ThreeKingdoms.SkillDecision?.PlayerId=="P1");
        ShowSeat("P1");
        yield return ClickStep("02-赠牌条件",()=>Kind()=="alliance-b6-terms");
        yield return ClickStep("03-选择木头",()=>game.M6View.ThreeKingdoms.SkillActions.Any(a=>a.Command.Option=="alliance-b6-offer"));
        Require(host.View("P1").OwnResources.Wood==wood,"提出条件前提前转移了手牌");
        yield return ClickStep("04-提出条件",()=>game.M6View.ThreeKingdoms.SkillDecision?.PlayerId==applicant);
        ShowSeat(applicant);
        yield return ClickStep("05-同意条件",()=>game.M6View.ThreeKingdoms.AllianceMembers.Contains("P1"));
        Require(host.View("P1").OwnResources.Wood==wood-1,"结盟未按约定转移木头");
        session.AssertInvariants();

        state=session.GetAuthoritativeStateForTesting();
        MatrixPrepare(session,state,new MatrixCase("落英","tk-g30","tk-g30-s1",null){Owner="P2"});
        cards=(IThreeKingdomsCardHost)typeof(Catan.Core.M5.CombinedGameSession).GetMethod("CardHost",Private).Invoke(session,new object[]{state});
        B2SkillEngine.HoldReturnedCards(cards,"P1","payment","落英界面验收",new[]{2,0,0,0,0,0,0,0});
        B2SkillEngine.Pump(cards);ShowSeat("P2");
        int before=host.View("P2").OwnResources.Wood;
        yield return ClickStep("06-落英",()=>game.M6View.OwnResources.Wood==before+2);
        session.AssertInvariants();
        // 同一正式卡牌控件的多选场景，只在验收投影内检查草稿和牌面。
        var view=game.M6View.ThreeKingdoms;
        view.SkillDecision=new B2SkillDecision{PlayerId="P2",Kind="shelie",DecisionId="涉猎牌列验收"};
        view.SkillActions=Enumerable.Range(0,5).Select(type=>new B2SkillAction{
            Command=new B2SkillCommand{SkillId="tk-g28-s1",Option="card",CardType=type}
        }).Concat(new[]{new B2SkillAction{Command=new B2SkillCommand{SkillId="tk-g28-s1",Option="done"}}}).ToArray();
        var selection=(B45SelectionContext)typeof(M6App).GetField("generalSelection",Private).GetValue(app);
        selection.Begin(view,"P2",null,new int[8]);
        yield return ClickStep("07-涉猎牌列",()=>selection.IsOptionSelected(view.SkillActions[0]));
        yield return Capture("08-涉猎选中木头");
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
