using System;
using System.Collections;
using System.Linq;
using Catan.Core;
using Catan.Core.ThreeKingdoms;
using UnityEngine;
using M5=Catan.Core.M5;

// 仅显式验收参数启动；使用独立本地对局检验真实投影、手牌桥接及确认入口。
public sealed partial class InteractionPresentationVerification
{
    IEnumerator VerifyHandSelectionReview()
    {
        stage="全模式选牌基准";
        app.StartGame("combined","through-the-desert",4,"P1",true,threeKingdoms:true,harbormaster:true,fishing:true);
        yield return null;yield return null;
        var game=GetComponent<M5App>();var host=game.VerificationHost;var policy=new Catan.AI.CombinedAi();
        for(int i=0;i<350;i++)
        {
            var v=host.View("P1");
            if(v.Turn>=4&&v.ActivePlayerId=="P1"&&v.Phase==M5.GamePhase.Action&&v.ThreeKingdoms.SkillDecision==null)break;
            var actor=v.ThreeKingdoms.WaitingSelectionPlayerIds.FirstOrDefault()??M5App.Actor(v);
            var c=policy.Decide(host.AiView(actor));Require(c!=null,"基准局无法推进");c.Id=Guid.NewGuid().ToString("N");Require(host.Submit(c).Success,"基准局命令失败");yield return null;
        }
        var session=(M5.CombinedGameSession)typeof(M5LocalGameHost).GetField("session",Private).GetValue(host);
        var state=session.GetAuthoritativeStateForTesting();var tk=state.ThreeKingdoms;
        state.ActivePlayerId="P1";state.Phase=M5.GamePhase.Action;
        tk.Clock.ActivePlayerId="P1";tk.Clock.Phase=ThreeKingdomsPhase.Play;tk.Clock.DrawPhaseTurnInstanceId=tk.Clock.TurnInstanceId;
        tk.Skills.Frames=Array.Empty<B2SkillFrame>();tk.Skills.PendingDecision=null;tk.PendingDecision=null;tk.Response=null;tk.Automation=null;
        var general=tk.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P1");
        var liru=tk.Generals.Single(g=>g.GeneralId=="tk-g31");
        string oldId=general.GeneralId,oldInstance=general.InstanceId;general.GeneralId=liru.GeneralId;general.InstanceId=liru.InstanceId;liru.GeneralId=oldId;liru.InstanceId=oldInstance;general.Counters=Array.Empty<GeneralCounter>();
        var own=state.Players.Single(p=>p.Id=="P1");own.Resources=new ResourceBag{Wood=3,Brick=2};own.Commodities=new M5.CommodityBag{Cloth=2};
        foreach(Resource r in Enum.GetValues(typeof(Resource)))state.Bank[r]=19-state.Players.Sum(p=>p.Resources[r]);
        foreach(M5.Commodity c in Enum.GetValues(typeof(M5.Commodity)))state.CommodityBank[c]=12-state.Players.Sum(p=>p.Commodities[c]);
        typeof(M5.CombinedGameSession).GetField("state",Private).SetValue(session,state);session.AssertInvariants();
        Action<string> use=option=>{
            var a=host.View("P1").ThreeKingdoms.SkillActions.First(x=>x.Command.Option==option&&(option!="civil-fencheng"||x.Command.TargetPlayerIds[0]=="P1"));
            Require(host.Submit(new M5.Command{Id=Guid.NewGuid().ToString("N"),PlayerId="P1",Kind=M5.CommandKind.UseGeneralSkill,WindowId=a.Command.WindowId,DecisionId=a.Command.DecisionId,SkillCommand=a.Command}).Success,"焚城步骤失败："+option);
        };
        use("civil-fencheng");use("civil-fencheng-hand");
        game.M6Refresh();table=game.VerificationBoard.TableScene;yield return SettleFeedback();
        yield return new WaitForSecondsRealtime(5);yield return SettleFeedback();
        var selection=(B45SelectionContext)typeof(M6App).GetField("generalSelection",Private).GetValue(app);
        app.SyncThreeKingdomsSelection();Require(selection.CivilHandBatch&&!selection.ShowOptionChoices,"仍出现弃牌用途候选");
        string before=host.ExportSave();var hand=app.ThreeKingdomsHandSelection;
        double handDeadline=Time.unscaledTimeAsDouble+10;
        while(!hand.GoodAllowed(0,1)&&Time.unscaledTimeAsDouble<handDeadline)yield return null;
        Require(hand.GoodAllowed(0,1),"焚城手牌输入门禁没有开放");
        if(Environment.GetCommandLineArgs().Contains("--hand-selection-interactive"))
        {
            while(!Input.GetKeyDown(KeyCode.F10))yield return null;
            Require(host.View("P1").OwnResources.Wood==2&&host.View("P1").OwnCommodities.Cloth==1,"窗口点击确认后的弃牌数量不正确");
            var frame=session.GetAuthoritativeStateForTesting().ThreeKingdoms.Skills.Frames.FirstOrDefault(f=>f.Kind=="civil-fencheng");
            Require(frame==null||frame.Step>0,"窗口点击没有完成焚城弃牌");session.AssertInvariants();yield break;
        }
        hand.SelectGood(0,1);yield return null;Require(hand.IsGoodSelected(0,1)&&app.CanFinishGeneralAction,"选牌后未抽出或不能确认");
        yield return Capture("焚城-抽出手牌");
        hand.SelectGood(0,1);yield return null;Require(!hand.IsGoodSelected(0,1)&&!app.CanFinishGeneralAction,"再次点击未取消");
        yield return Capture("焚城-再次点击下沉");
        hand.SelectGood(0,1);hand.SelectGood(5,1);yield return null;
        Require(host.ExportSave()==before,"点击手牌提前提交权威操作");yield return Capture("焚城-资源商品混选待确认");
        app.FinishGeneralAction();yield return null;yield return SettleFeedback();
        Require(host.View("P1").OwnResources.Wood==2&&host.View("P1").OwnCommodities.Cloth==1,"确认后没有整批弃置");
        var remainingFencheng=session.GetAuthoritativeStateForTesting().ThreeKingdoms.Skills.Frames.FirstOrDefault(f=>f.Kind=="civil-fencheng");
        Require(remainingFencheng==null||remainingFencheng.Step>0,"焚城没有完成本席弃牌步骤");
        session.AssertInvariants();yield return new WaitForSecondsRealtime(5);yield return SettleFeedback();yield return Capture("焚城-确认后进入下一玩家");
        stage="发展牌选牌草稿";
        // 下列仅替换本机已脱离权威的投影，检查操作桥接，不向真实待决提交虚构命令。
        var projected=game.M6View;string committed=host.ExportSave();selection.Reset();
        projected.ThreeKingdoms.SkillDecision=null;projected.ThreeKingdoms.PendingDecision=null;
        Action<string,object> setView=(name,value)=>typeof(M5.PlayerView).GetProperty(name).SetValue(projected,value,null);
        Action sync=()=>typeof(M5App).GetMethod("SyncProgressConfirmation",Private).Invoke(game,null);
        Func<bool> ready=()=> (bool)typeof(M5App).GetProperty("ProgressConfirmReady",Private).GetValue(game,null);
        setView("PendingDecision",new M5.PendingDecision{Kind="ProgressHarbor",PlayerId="P1",Amount=1});
        setView("LegalActions",new[]{new M5.Command{Kind=M5.CommandKind.ResolveProgressChoice,Commodity=M5.Commodity.Cloth}});sync();
        var harborHand=(OperationHandSelection)typeof(M5App).GetMethod("ProgressHandSelection",Private).Invoke(game,null);
        Require(!ready(),"商港尚未选牌却能确认");harborHand.SelectGood(5,0);Require(harborHand.IsGoodSelected(5,0)&&ready(),"商港商品未保留草稿");
        yield return Capture("商港-商品抽出待确认");harborHand.SelectGood(5,0);Require(!harborHand.IsGoodSelected(5,0)&&!ready(),"商港商品无法撤选");
        setView("PendingDecision",new M5.PendingDecision{Kind="ProgressGuildDues",PlayerId="P1",Amount=2});
        setView("PrivateTargetResources",new ResourceBag{Wood=2,Brick=1});setView("PrivateTargetCommodities",new M5.CommodityBag{Cloth=1});sync();
        var privatePick=(OperationSelection)typeof(M5App).GetField("privateProgressSelection",Private).GetValue(game);
        privatePick.Toggle(0,1,2);Require(!ready(),"大商家数量不足却能确认");privatePick.Toggle(5,0,1);Require(ready(),"大商家选满后没有等待确认");
        yield return Capture("大商家-混选待确认");privatePick.Toggle(0,1,2);Require(!ready()&&!privatePick.IsCardSelected(0,1),"大商家无法撤回其中一张");
        setView("PendingDecision",null);
        var knightIds=projected.Board.Vertices.Take(2).Select(v=>v.Id).ToArray();
        setView("LegalActions",new[]{
            new M5.Command{Kind=M5.CommandKind.PlayProgressCard,ProgressCard=M5.ProgressCardKind.Smithing,TargetIds=new[]{knightIds[0]}},
            new M5.Command{Kind=M5.CommandKind.PlayProgressCard,ProgressCard=M5.ProgressCardKind.Smithing,TargetIds=new[]{knightIds[1]}},
            new M5.Command{Kind=M5.CommandKind.PlayProgressCard,ProgressCard=M5.ProgressCardKind.Smithing,TargetIds=knightIds}});
        typeof(M5App).GetField("draft",Private).SetValue(game,new M5.Command{Kind=M5.CommandKind.PlayProgressCard,ProgressCard=M5.ProgressCardKind.Smithing});sync();
        var choose=typeof(M5App).GetMethod("ChooseSmithingTarget",Private);
        choose.Invoke(game,new object[]{knightIds[0],new M5.Command{TargetIds=new[]{knightIds[0]}}});Require(ready(),"锻冶匠首名骑士不能确认");
        choose.Invoke(game,new object[]{knightIds[1],new M5.Command{TargetIds=knightIds}});Require(ready(),"锻冶匠选满后没有保留草稿");
        choose.Invoke(game,new object[]{knightIds[0],new M5.Command{TargetIds=knightIds}});
        Require(((string[])typeof(M5App).GetField("smithingSelected",Private).GetValue(game)).SequenceEqual(new[]{knightIds[1]}),"锻冶匠再次点击没有撤选");
        Require(host.ExportSave()==committed,"发展牌草稿提前改变权威状态");game.M6Refresh();
    }
}
