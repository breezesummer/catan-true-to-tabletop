using System;
using System.Collections;
using System.Linq;
using Catan.Core;
using UnityEngine;

public sealed partial class InteractionPresentationVerification
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void BootFeedback()
    {
        if(!Environment.GetCommandLineArgs().Contains("--feedback-verify"))return;
        var owner=UnityEngine.Object.FindFirstObjectByType<M6App>();
        Debug.Log("CATAN_FEEDBACK_BOOT: "+(owner!=null));
        if(owner!=null&&owner.GetComponent<InteractionPresentationVerification>()==null)owner.gameObject.AddComponent<InteractionPresentationVerification>();
    }
    IEnumerator SettleFeedback(){double deadline=Time.unscaledTimeAsDouble+10;while(app.TablePresentationBlocked&&Time.unscaledTimeAsDouble<deadline)yield return null;Require(!app.TablePresentationBlocked,"动画没有结束");yield return null;}
    IEnumerator VerifyFeedback()
    {
        yield return VerifyBasicCardAction();yield return VerifyFeedback4();yield return VerifyFeedback5();yield return VerifyBroadcastRecipientClose();yield return VerifyOperationWindows();
        stage="百科与Esc返回";
        app.OpenTableMenu();
        if(Environment.GetCommandLineArgs().Contains("--feedback-screenshots"))yield return Capture("游戏菜单");
        PolishSetField(app,"tablePausePage",1);yield return null;
        Require(app.TablePauseSettingsOpen,"游戏菜单的设置页未打开");
        if(Environment.GetCommandLineArgs().Contains("--feedback-screenshots"))yield return Capture("游戏设置");
        app.HandleTableEscape();Require(app.TableMenuOpen&&!app.TablePauseSettingsOpen,"Esc未从设置返回游戏菜单");
        PolishSetField(app,"encyclopediaOpen",true);
        yield return null;yield return null;
        Require(app.TableMenuOpen&&app.TablePresentationBlocked,"百科未阻止棋盘交互");
        if(Environment.GetCommandLineArgs().Contains("--feedback-screenshots"))yield return Capture("游戏百科");
        app.HandleTableEscape();Require(app.TableMenuOpen&&!(bool)typeof(M6App).GetField("encyclopediaOpen",Private).GetValue(app),"Esc未从百科返回菜单");
        app.HandleTableEscape();Require(!app.TableMenuOpen,"Esc未从菜单返回游戏");
        Require(M5App.Cards.Length==25&&M5App.CardHelp.All(s=>!string.IsNullOrEmpty(s)),"发展卡百科缺项");
    }

    TabletopUI BindFeedbackCardHud(object game)
    {
        var hud=(TabletopUI)game.GetType().GetField("tabletop",Private).GetValue(game);
        var dock=(OperationDock)game.GetType().GetField("dock",Private).GetValue(game);
        var snapshot=(OperationSnapshot)PolishGet(game,"VerificationOperationSnapshot");
        hud.BindOperations(snapshot,dock.PrepareDevelopmentPurchase,app.HistoryEntries);hud.BindDiscard(snapshot,dock);dock.Windows=hud.OperationWindows;
        PolishSetField(dock,"current",snapshot);PolishSetField(dock,"scope",snapshot.Scope);PolishSetField(dock,"revision",snapshot.Revision);PolishSetField(dock,"specialHand",true);dock.Selection.Sync(snapshot);
        table=(TabletopScene)PolishGet(PolishGet(game,"VerificationBoard"),"TableScene");return hud;
    }
    void CheckCardPreviewHud(TabletopUI hud,OperationDock dock,OperationSnapshot snapshot,OperationHandCard card)
    {
        dock.ToggleCardPreview(snapshot,card);
        Require(dock.PreviewCard(snapshot)==card&&hud.CardActionCaption=="使用\n"+card.Name,"预览没有把所选卡牌移到右下角使用入口");
        Require(dock.BlocksBoardInput(new Vector2(720,430))&&!dock.BlocksHudInput(TabletopUI.EndTurnRect.center),"预览未阻挡棋盘或右下角按钮被遮挡");
    }
    IEnumerator VerifyBasicCardAction()
    {
        stage="基础发展卡右下角使用";app.StartGame("base","heading-for-new-shores",4,"P1",true);yield return null;yield return null;
        var game=GetComponent<M2App>();var host=game.VerificationHost;
        var session=(Catan.Core.M2.BaseGameSession)typeof(M2LocalGameHost).GetField("session",Private).GetValue(host);
        var state=session.GetAuthoritativeStateForTesting();state.ActivePlayerId="P1";state.Turn=9;state.Phase=Catan.Core.M2.GamePhase.Action;state.SetupPlacementIndex=8;state.PendingDecision=null;
        state.Players[0].DevelopmentCards=new[]{
            new Catan.Core.M2.DevelopmentCard{Kind=Catan.Core.M2.DevelopmentCardKind.Knight,BoughtTurn=1},
            new Catan.Core.M2.DevelopmentCard{Kind=Catan.Core.M2.DevelopmentCardKind.YearOfPlenty,BoughtTurn=9},
            new Catan.Core.M2.DevelopmentCard{Kind=Catan.Core.M2.DevelopmentCardKind.VictoryPoint,BoughtTurn=1}};
        var deck=state.DevelopmentDeck.ToList();foreach(var card in state.Players[0].DevelopmentCards)deck.Remove(card.Kind);state.DevelopmentDeck=deck.ToArray();
        typeof(Catan.Core.M2.BaseGameSession).GetField("state",Private).SetValue(session,state);session.AssertInvariants();game.M6Refresh();yield return null;yield return SettleFeedback();
        var snapshot=game.VerificationOperationSnapshot;var dock=game.VerificationOperationDock;var hud=BindFeedbackCardHud(game);string before=host.ExportSave();
        foreach(var card in snapshot.Cards.Where(c=>!c.Playable))
        {
            CheckCardPreviewHud(hud,dock,snapshot,card);Require(!hud.CardActionReady,"新买卡或胜利点卡错误允许使用");hud.ConfirmCardAction();
            Require(before==host.ExportSave(),"查看或尝试使用不可用卡改变了权威局面");dock.ToggleCardPreview(snapshot,card);
            Require(!dock.HasOverlay&&hud.CardActionCaption==null,"再次点击同一张牌未关闭预览");
        }
        var knight=snapshot.Cards.First(c=>c.Playable);CheckCardPreviewHud(hud,dock,snapshot,knight);
        Require(before==host.ExportSave()&&hud.CardActionReady,"合法卡牌预览提前出牌或使用入口被禁用");
        if(Environment.GetCommandLineArgs().Contains("--feedback-screenshots"))yield return Capture("基础发展卡预览");
        hud.ConfirmCardAction();yield return null;yield return SettleFeedback();
        Require(host.View("P1").Players.First(p=>p.Id=="P1").PlayedKnights==1&&host.View("P1").Phase==Catan.Core.M2.GamePhase.RobberMove,"右下角使用骑士未走正常出牌与强盗待决");
        snapshot=game.VerificationOperationSnapshot;hud=BindFeedbackCardHud(game);before=host.ExportSave();
        CheckCardPreviewHud(hud,dock,snapshot,snapshot.Cards[0]);Require(snapshot.RequiresSelection&&!hud.CardActionReady,"强盗待决仍允许使用其他卡牌");hud.ConfirmCardAction();Require(before==host.ExportSave(),"预览绕过了强盗待决");dock.Reset();session.AssertInvariants();
    }

    IEnumerator VerifyFeedback4()
    {
        int smithRuns=0;
        foreach(var card in new[]{Catan.Core.M4.ProgressCardKind.Merchant,Catan.Core.M4.ProgressCardKind.Medicine,Catan.Core.M4.ProgressCardKind.Engineering,Catan.Core.M4.ProgressCardKind.Diplomacy,Catan.Core.M4.ProgressCardKind.Intrigue,Catan.Core.M4.ProgressCardKind.Invention,Catan.Core.M4.ProgressCardKind.Smithing,Catan.Core.M4.ProgressCardKind.Smithing,Catan.Core.M4.ProgressCardKind.Treason})
        {
            stage="cities-knights 地图卡牌 "+card;
            app.StartGame("cities-knights","heading-for-new-shores",4,"P1",true);yield return null;yield return null;
            var game=GetComponent<M4App>();var host=game.VerificationHost;
            var session=(Catan.Core.M4.CitiesKnightsGameSession)typeof(M4LocalGameHost).GetField("session",Private).GetValue(host);
            var state=session.GetAuthoritativeStateForTesting();var board=game.M6View.Board;
            var renderer=game.VerificationBoard;table=renderer.TableScene;
            var e1=board.Edges.Where(e=>board.Tiles.Any(t=>t.Resource!="sea"&&t.Resource!="fog"&&e.Vertices.All(t.Vertices.Contains)))
                .OrderBy(e=>Vector2.Distance(renderer.ScreenPoint(e.Id),MobileTabletop.ScreenPoint(new Vector2(720,430)))).First();
            string a=e1.Vertices[0],b=e1.Vertices[1];var e2=board.Edges.First(e=>e.Id!=e1.Id&&e.Vertices.Contains(b));
            string c=e2.Vertices.First(v=>v!=b);
            var sites=board.Vertices.Where(v=>v.Id!=a&&v.Id!=b&&v.Id!=c&&board.Tiles.Any(t=>t.Resource!="sea"&&t.Resource!="fog"&&t.Vertices.Contains(v.Id)))
                .OrderBy(v=>Vector2.Distance(renderer.ScreenPoint(v.Id),MobileTabletop.ScreenPoint(new Vector2(720,430)))).ToArray();
            string village=sites[0].Id,city=sites.First(v=>v.Id!=village&&!board.Edges.Any(e=>e.Vertices.Contains(village)&&e.Vertices.Contains(v.Id))).Id;
            state.ActivePlayerId="P1";state.Turn=9;state.Phase=Catan.Core.M4.GamePhase.Action;state.SetupPlacementIndex=8;state.PendingDecision=null;
            state.Settlements=new[]{Piece(village)};state.Cities=new[]{Piece(city)};state.Roads=new[]{Piece(e1.Id),Piece(e2.Id)};
            state.Knights=new[]{new Catan.Core.M4.Knight{PlayerId="P1",LocationId=a,Level=1,Active=true},new Catan.Core.M4.Knight{PlayerId="P2",LocationId=b,Level=1,Active=true}};
            if(card==Catan.Core.M4.ProgressCardKind.Smithing&&++smithRuns==2)state.Knights=state.Knights.Concat(new[]{new Catan.Core.M4.Knight{PlayerId="P1",LocationId=c,Level=1,Active=true}}).ToArray();
            state.Players[0].Resources=new ResourceBag{Wheat=3,Ore=3};state.Players[0].ProgressCards=new[]{card};
            foreach(var player in state.Players){player.Pieces.Roads=15-state.Roads.Count(r=>r.PlayerId==player.Id);player.Pieces.Settlements=5-state.Settlements.Count(r=>r.PlayerId==player.Id);player.Pieces.Cities=4-state.Cities.Count(r=>r.PlayerId==player.Id);}
            foreach(Resource r in Enum.GetValues(typeof(Resource)))state.Bank[r]=19-state.Players.Sum(p=>p.Resources[r]);
            foreach(var deck in state.ProgressDecks){var list=deck.Cards.ToList();list.Remove(card);deck.Cards=list.ToArray();}
            typeof(Catan.Core.M4.CitiesKnightsGameSession).GetField("state",Private).SetValue(session,state);session.AssertInvariants();game.M6Refresh();yield return null;yield return SettleFeedback();
            var action=game.M6View.LegalActions.FirstOrDefault(x=>x.Kind==Catan.Core.M4.CommandKind.PlayProgressCard&&x.ProgressCard==card);
            Require(action!=null,"夹具没有合法用牌动作 "+card);
            var snapshot=game.VerificationOperationSnapshot;var hud=BindFeedbackCardHud(game);string previewBefore=host.ExportSave();
            CheckCardPreviewHud(hud,game.VerificationOperationDock,snapshot,snapshot.Cards.First(x=>x.ArtIndex==(int)card));
            Require(hud.CardActionReady&&previewBefore==host.ExportSave(),"进步卡预览提前消耗牌或无法使用");
            if(card==Catan.Core.M4.ProgressCardKind.Merchant&&Environment.GetCommandLineArgs().Contains("--feedback-screenshots"))yield return Capture("城市骑士发展卡预览");
            hud.ConfirmCardAction();Require(previewBefore==host.ExportSave(),"进入地图选目标时提前消耗进步卡");
            Require((bool)PolishGet(game,"DirectProgressActive"),"未进入地图用牌模式");Require(!(bool)PolishGet(game,"OperationContextVisible"),"旧菜单仍然遮挡棋盘");
            var input=typeof(M4App).GetMethod("UpdateDirectProgress",Private);
            string before=host.ExportSave();input.Invoke(game,new object[]{new Vector2(-100,-100),true});Require(before==host.ExportSave(),"非法地图点击改变状态");
            if(card==Catan.Core.M4.ProgressCardKind.Invention)input.Invoke(game,new object[]{renderer.ScreenPoint(action.SourceId),true});
            string target=card==Catan.Core.M4.ProgressCardKind.Treason?b:card==Catan.Core.M4.ProgressCardKind.Smithing?action.TargetIds[0]:action.TargetId;
            input.Invoke(game,new object[]{renderer.ScreenPoint(target),false});Require(before==host.ExportSave(),"预览提前消耗卡牌");
            input.Invoke(game,new object[]{renderer.ScreenPoint(target),true});
            if(card==Catan.Core.M4.ProgressCardKind.Smithing&&smithRuns==2)
            {
                Require(before==host.ExportSave(),"锻铁匠第一名选择提前消耗卡牌");
                string second=game.M6View.Board.Knights.First(k=>k.PlayerId=="P1"&&k.LocationId!=target).LocationId;
                input.Invoke(game,new object[]{renderer.ScreenPoint(second),true});
            }
            yield return null;yield return SettleFeedback();
            Require(host.View("P1").OwnProgressCards.Length==0,"地图点选没有消耗所选卡牌 "+card);
            var after=session.GetAuthoritativeStateForTesting();
            if(card==Catan.Core.M4.ProgressCardKind.Merchant)Require(after.MerchantTileId==target&&after.MerchantPlayerId=="P1","商人没有落在所点地块");
            if(card==Catan.Core.M4.ProgressCardKind.Medicine)Require(after.Cities.Length==2&&after.Settlements.Length==0,"医学未升级村庄");
            if(card==Catan.Core.M4.ProgressCardKind.Engineering)Require(after.Walls.Length==1,"工程学没有建墙");
            if(card==Catan.Core.M4.ProgressCardKind.Diplomacy)Require(after.Roads.Length==1,"外交没有移除道路");
            if(card==Catan.Core.M4.ProgressCardKind.Smithing)Require(after.Knights.Where(k=>k.PlayerId=="P1").All(k=>k.Level==2),"锻铁匠没有晋升全部所选骑士");
            if(card==Catan.Core.M4.ProgressCardKind.Treason)Require(after.PendingDecision.Kind=="ProgressTreasonRemove"&&after.PendingDecision.PlayerId=="P2","叛变越过对手选择权");
            session.AssertInvariants();
        }
        stage="cities-knights 海岸免费建设气泡";
        var current=GetComponent<M4App>();var currentHost=current.VerificationHost;
        var currentSession=(Catan.Core.M4.CitiesKnightsGameSession)typeof(M4LocalGameHost).GetField("session",Private).GetValue(currentHost);
        var s=currentSession.GetAuthoritativeStateForTesting();
        stage="M4 叛变受害者持续选择提示";
        var beforeTreason=currentSession.GetAuthoritativeStateForTesting();
        string betrayedKnight=s.Knights.First(k=>k.PlayerId=="P1").LocationId;
        s.Phase=Catan.Core.M4.GamePhase.PendingChoice;s.DecisionReturnPhase=Catan.Core.M4.GamePhase.Action;
        s.PendingDecision=new Catan.Core.M4.PendingDecision{Kind="ProgressTreasonRemove",PlayerId="P1",OtherPlayerId="P2",Options=new[]{betrayedKnight},ReturnPhase=Catan.Core.M4.GamePhase.Action};
        s.DecisionQueue=Array.Empty<Catan.Core.M4.PendingDecision>();
        typeof(Catan.Core.M4.CitiesKnightsGameSession).GetField("state",Private).SetValue(currentSession,s);
        current.M6Refresh();yield return null;yield return SettleFeedback();yield return VerifyRequiredChoicePrompt("叛变");
        typeof(M4App).GetMethod("UpdateDirectProgress",Private).Invoke(current,new object[]{current.VerificationBoard.ScreenPoint(betrayedKnight),true});
        yield return null;yield return SettleFeedback();
        Require(!currentSession.GetAuthoritativeStateForTesting().Knights.Any(k=>k.LocationId==betrayedKnight),"叛变地图选择未移除骑士");
        Require(!app.CurrentTablePrompt.Contains("选择要移除"),"完成叛变选择后旧提示仍在显示");
        s=beforeTreason;
        stage="M4 拆城待决地图选择";
        string pillageTarget=s.Cities.First(x=>x.PlayerId=="P1").LocationId;
        s.Phase=Catan.Core.M4.GamePhase.PendingChoice;s.DecisionReturnPhase=Catan.Core.M4.GamePhase.Action;
        s.PendingDecision=new Catan.Core.M4.PendingDecision{Kind="PillageCity",PlayerId="P1",Options=new[]{pillageTarget},ReturnPhase=Catan.Core.M4.GamePhase.Action};
        s.DecisionQueue=Array.Empty<Catan.Core.M4.PendingDecision>();
        typeof(Catan.Core.M4.CitiesKnightsGameSession).GetField("state",Private).SetValue(currentSession,s);
        current.M6Refresh();yield return null;yield return SettleFeedback();
        Require((bool)PolishGet(current,"DirectProgressActive"),"拆城未进入地图待决模式");
        yield return VerifyRequiredChoicePrompt("降级为村庄");
        typeof(M4App).GetMethod("UpdateDirectProgress",Private).Invoke(current,new object[]{current.VerificationBoard.ScreenPoint(pillageTarget),true});
        yield return null;yield return SettleFeedback();
        Require(!currentSession.GetAuthoritativeStateForTesting().Cities.Any(x=>x.LocationId==pillageTarget),"地图点选未执行拆城待决");
        Require(!app.HasRequiredTableSelection,"完成拆城后选择提示没有清除");
        currentSession.AssertInvariants();
        s=currentSession.GetAuthoritativeStateForTesting();

        var edge=current.M6View.Board.Edges.First(e=>current.M6View.Board.Tiles.Any(t=>t.Resource!="sea"&&t.Resource!="fog"&&e.Vertices.All(t.Vertices.Contains)));
        s.Phase=Catan.Core.M4.GamePhase.PendingChoice;s.DecisionReturnPhase=Catan.Core.M4.GamePhase.Action;s.PendingDecision=new Catan.Core.M4.PendingDecision{Kind="ProgressRoads",PlayerId="P1",Amount=2,ReturnPhase=Catan.Core.M4.GamePhase.Action};
        s.DecisionQueue=Array.Empty<Catan.Core.M4.PendingDecision>();s.Knights=Array.Empty<Catan.Core.M4.Knight>();s.Cities=Array.Empty<PiecePlacement>();s.Roads=Array.Empty<PiecePlacement>();s.Settlements=new[]{Piece(edge.Vertices[0])};
        foreach(var p in s.Players){p.Pieces.Roads=15;p.Pieces.Cities=4;p.Pieces.Settlements=p.Id=="P1"?4:5;}
        typeof(Catan.Core.M4.CitiesKnightsGameSession).GetField("state",Private).SetValue(currentSession,s);currentSession.AssertInvariants();current.M6Refresh();yield return null;
        string saved=currentHost.ExportSave();typeof(M4App).GetMethod("UpdateFreeRoadPlacement",Private).Invoke(current,new object[]{current.VerificationBoard.ScreenPoint(edge.Id),true});
        yield return null;yield return SettleFeedback();Require(currentHost.View("P1").Board.Roads.Length==1,"基础免费道路没有落子");
        currentSession.AssertInvariants();
    }

    IEnumerator VerifyFeedback5()
    {
        int smithRuns=0;
        foreach(var card in new[]{Catan.Core.M5.ProgressCardKind.Merchant,Catan.Core.M5.ProgressCardKind.Medicine,Catan.Core.M5.ProgressCardKind.Engineering,Catan.Core.M5.ProgressCardKind.Diplomacy,Catan.Core.M5.ProgressCardKind.Intrigue,Catan.Core.M5.ProgressCardKind.Invention,Catan.Core.M5.ProgressCardKind.Smithing,Catan.Core.M5.ProgressCardKind.Smithing,Catan.Core.M5.ProgressCardKind.Treason})
        {
            stage="combined 地图卡牌 "+card;
            app.StartGame("combined","heading-for-new-shores",4,"P1",true);yield return null;yield return null;
            var game=GetComponent<M5App>();var host=game.VerificationHost;
            var session=(Catan.Core.M5.CombinedGameSession)typeof(M5LocalGameHost).GetField("session",Private).GetValue(host);
            var state=session.GetAuthoritativeStateForTesting();var board=game.M6View.Board;
            var renderer=game.VerificationBoard;table=renderer.TableScene;
            var e1=board.Edges.Where(e=>board.Tiles.Any(t=>t.Resource!="sea"&&t.Resource!="fog"&&e.Vertices.All(t.Vertices.Contains)))
                .OrderBy(e=>Vector2.Distance(renderer.ScreenPoint(e.Id),MobileTabletop.ScreenPoint(new Vector2(720,430)))).First();
            string a=e1.Vertices[0],b=e1.Vertices[1];var e2=board.Edges.First(e=>e.Id!=e1.Id&&e.Vertices.Contains(b));
            string c=e2.Vertices.First(v=>v!=b);
            var sites=board.Vertices.Where(v=>v.Id!=a&&v.Id!=b&&v.Id!=c&&board.Tiles.Any(t=>t.Resource!="sea"&&t.Resource!="fog"&&t.Vertices.Contains(v.Id)))
                .OrderBy(v=>Vector2.Distance(renderer.ScreenPoint(v.Id),MobileTabletop.ScreenPoint(new Vector2(720,430)))).ToArray();
            string village=sites[0].Id,city=sites.First(v=>v.Id!=village&&!board.Edges.Any(e=>e.Vertices.Contains(village)&&e.Vertices.Contains(v.Id))).Id;
            state.ActivePlayerId="P1";state.Turn=9;state.Phase=Catan.Core.M5.GamePhase.Action;state.SetupPlacementIndex=8;state.PendingDecision=null;
            state.Settlements=new[]{Piece(village)};state.Cities=new[]{Piece(city)};state.Roads=new[]{Piece(e1.Id),Piece(e2.Id)};
            state.Knights=new[]{new Catan.Core.M5.Knight{PlayerId="P1",LocationId=a,Level=1,Active=true},new Catan.Core.M5.Knight{PlayerId="P2",LocationId=b,Level=1,Active=true}};
            if(card==Catan.Core.M5.ProgressCardKind.Smithing&&++smithRuns==2)state.Knights=state.Knights.Concat(new[]{new Catan.Core.M5.Knight{PlayerId="P1",LocationId=c,Level=1,Active=true}}).ToArray();
            state.Players[0].Resources=new ResourceBag{Wheat=3,Ore=3};state.Players[0].ProgressCards=new[]{card};
            foreach(var player in state.Players){player.Pieces.Roads=15-state.Roads.Count(r=>r.PlayerId==player.Id);player.Pieces.Settlements=5-state.Settlements.Count(r=>r.PlayerId==player.Id);player.Pieces.Cities=4-state.Cities.Count(r=>r.PlayerId==player.Id);}
            foreach(Resource r in Enum.GetValues(typeof(Resource)))state.Bank[r]=19-state.Players.Sum(p=>p.Resources[r]);
            foreach(var deck in state.ProgressDecks){var list=deck.Cards.ToList();list.Remove(card);deck.Cards=list.ToArray();}
            typeof(Catan.Core.M5.CombinedGameSession).GetField("state",Private).SetValue(session,state);session.AssertInvariants();game.M6Refresh();yield return null;yield return SettleFeedback();
            var action=game.M6View.LegalActions.FirstOrDefault(x=>x.Kind==Catan.Core.M5.CommandKind.PlayProgressCard&&x.ProgressCard==card);
            Require(action!=null,"夹具没有合法用牌动作 "+card);
            var snapshot=game.VerificationOperationSnapshot;var hud=BindFeedbackCardHud(game);string previewBefore=host.ExportSave();
            CheckCardPreviewHud(hud,game.VerificationOperationDock,snapshot,snapshot.Cards.First(x=>x.ArtIndex==(int)card));
            Require(hud.CardActionReady&&previewBefore==host.ExportSave(),"组合进步卡预览提前消耗牌或无法使用");hud.ConfirmCardAction();
            Require(previewBefore==host.ExportSave(),"组合进步卡进入地图选目标时提前消耗牌");
            Require((bool)PolishGet(game,"DirectProgressActive"),"未进入地图用牌模式");Require(!(bool)PolishGet(game,"OperationContextVisible"),"旧菜单仍然遮挡棋盘");
            var input=typeof(M5App).GetMethod("UpdateDirectProgress",Private);
            string before=host.ExportSave();input.Invoke(game,new object[]{new Vector2(-100,-100),true});Require(before==host.ExportSave(),"非法地图点击改变状态");
            if(card==Catan.Core.M5.ProgressCardKind.Invention)input.Invoke(game,new object[]{renderer.ScreenPoint(action.SourceId),true});
            string target=card==Catan.Core.M5.ProgressCardKind.Treason?b:card==Catan.Core.M5.ProgressCardKind.Smithing?action.TargetIds[0]:action.TargetId;
            input.Invoke(game,new object[]{renderer.ScreenPoint(target),false});Require(before==host.ExportSave(),"预览提前消耗卡牌");
            input.Invoke(game,new object[]{renderer.ScreenPoint(target),true});
            if(card==Catan.Core.M5.ProgressCardKind.Smithing&&smithRuns==2)
            {
                Require(before==host.ExportSave(),"锻铁匠第一名选择提前消耗卡牌");
                string second=game.M6View.Board.Knights.First(k=>k.PlayerId=="P1"&&k.LocationId!=target).LocationId;
                input.Invoke(game,new object[]{renderer.ScreenPoint(second),true});
            }
            yield return null;yield return SettleFeedback();
            Require(host.View("P1").OwnProgressCards.Length==0,"地图点选没有消耗所选卡牌 "+card);
            var after=session.GetAuthoritativeStateForTesting();
            if(card==Catan.Core.M5.ProgressCardKind.Merchant)Require(after.MerchantTileId==target&&after.MerchantPlayerId=="P1","商人没有落在所点地块");
            if(card==Catan.Core.M5.ProgressCardKind.Medicine)Require(after.Cities.Length==2&&after.Settlements.Length==0,"医学未升级村庄");
            if(card==Catan.Core.M5.ProgressCardKind.Engineering)Require(after.Walls.Length==1,"工程学没有建墙");
            if(card==Catan.Core.M5.ProgressCardKind.Diplomacy)Require(after.Roads.Length==1,"外交没有移除道路");
            if(card==Catan.Core.M5.ProgressCardKind.Smithing)Require(after.Knights.Where(k=>k.PlayerId=="P1").All(k=>k.Level==2),"锻铁匠没有晋升全部所选骑士");
            if(card==Catan.Core.M5.ProgressCardKind.Treason)Require(after.PendingDecision.Kind=="ProgressTreasonRemove"&&after.PendingDecision.PlayerId=="P2","叛变越过对手选择权");
            session.AssertInvariants();
        }
        stage="combined 海岸免费建设气泡";
        var current=GetComponent<M5App>();var currentHost=current.VerificationHost;
        var currentSession=(Catan.Core.M5.CombinedGameSession)typeof(M5LocalGameHost).GetField("session",Private).GetValue(currentHost);
        var s=currentSession.GetAuthoritativeStateForTesting();
        stage="M5 叛变受害者持续选择提示";
        var beforeTreason=currentSession.GetAuthoritativeStateForTesting();
        string betrayedKnight=s.Knights.First(k=>k.PlayerId=="P1").LocationId;
        s.Phase=Catan.Core.M5.GamePhase.PendingChoice;s.DecisionReturnPhase=Catan.Core.M5.GamePhase.Action;
        s.PendingDecision=new Catan.Core.M5.PendingDecision{Kind="ProgressTreasonRemove",PlayerId="P1",OtherPlayerId="P2",Options=new[]{betrayedKnight},ReturnPhase=Catan.Core.M5.GamePhase.Action};
        s.DecisionQueue=Array.Empty<Catan.Core.M5.PendingDecision>();
        typeof(Catan.Core.M5.CombinedGameSession).GetField("state",Private).SetValue(currentSession,s);
        current.M6Refresh();yield return null;yield return SettleFeedback();yield return VerifyRequiredChoicePrompt("叛变");
        typeof(M5App).GetMethod("UpdateDirectProgress",Private).Invoke(current,new object[]{current.VerificationBoard.ScreenPoint(betrayedKnight),true});
        yield return null;yield return SettleFeedback();
        Require(!currentSession.GetAuthoritativeStateForTesting().Knights.Any(k=>k.LocationId==betrayedKnight),"叛变地图选择未移除骑士");
        Require(!app.CurrentTablePrompt.Contains("选择要移除"),"完成叛变选择后旧提示仍在显示");
        s=beforeTreason;
        stage="M5 拆城待决地图选择";
        string pillageTarget=s.Cities.First(x=>x.PlayerId=="P1").LocationId;
        s.Phase=Catan.Core.M5.GamePhase.PendingChoice;s.DecisionReturnPhase=Catan.Core.M5.GamePhase.Action;
        s.PendingDecision=new Catan.Core.M5.PendingDecision{Kind="PillageCity",PlayerId="P1",Options=new[]{pillageTarget},ReturnPhase=Catan.Core.M5.GamePhase.Action};
        s.DecisionQueue=Array.Empty<Catan.Core.M5.PendingDecision>();
        typeof(Catan.Core.M5.CombinedGameSession).GetField("state",Private).SetValue(currentSession,s);
        current.M6Refresh();yield return null;yield return SettleFeedback();
        Require((bool)PolishGet(current,"DirectProgressActive"),"拆城未进入地图待决模式");
        yield return VerifyRequiredChoicePrompt("降级为村庄");
        typeof(M5App).GetMethod("UpdateDirectProgress",Private).Invoke(current,new object[]{current.VerificationBoard.ScreenPoint(pillageTarget),true});
        yield return null;yield return SettleFeedback();
        Require(!currentSession.GetAuthoritativeStateForTesting().Cities.Any(x=>x.LocationId==pillageTarget),"地图点选未执行拆城待决");
        Require(!app.HasRequiredTableSelection,"完成拆城后选择提示没有清除");
        currentSession.AssertInvariants();
        s=currentSession.GetAuthoritativeStateForTesting();

        var edge=current.M6View.Board.Edges.First(e=>current.M6View.Board.Tiles.Any(t=>t.Resource!="sea"&&t.Resource!="fog"&&e.Vertices.All(t.Vertices.Contains))&&current.M6View.Board.Tiles.Any(t=>t.Resource=="sea"&&e.Vertices.All(t.Vertices.Contains)));
        s.Phase=Catan.Core.M5.GamePhase.PendingChoice;s.DecisionReturnPhase=Catan.Core.M5.GamePhase.Action;s.PendingDecision=new Catan.Core.M5.PendingDecision{Kind="ProgressRoads",PlayerId="P1",Amount=2,ReturnPhase=Catan.Core.M5.GamePhase.Action};
        s.DecisionQueue=Array.Empty<Catan.Core.M5.PendingDecision>();s.Knights=Array.Empty<Catan.Core.M5.Knight>();s.Cities=Array.Empty<PiecePlacement>();s.Roads=Array.Empty<PiecePlacement>();s.Settlements=new[]{Piece(edge.Vertices[0])};
        foreach(var p in s.Players){p.Pieces.Roads=15;p.Pieces.Cities=4;p.Pieces.Settlements=p.Id=="P1"?4:5;}
        typeof(Catan.Core.M5.CombinedGameSession).GetField("state",Private).SetValue(currentSession,s);currentSession.AssertInvariants();current.M6Refresh();yield return null;
        string saved=currentHost.ExportSave();typeof(M5App).GetMethod("UpdateFreeRoadPlacement",Private).Invoke(current,new object[]{current.VerificationBoard.ScreenPoint(edge.Id),true});
        var choose=(TabletopChoiceBubble)current.GetType().GetField("mapChoice",Private).GetValue(current);Require(choose.Active,"海岸没有弹出道路/船选择");Require(saved==currentHost.ExportSave(),"确认前已经落子");choose.Choose(1);yield return null;yield return SettleFeedback();Require(currentHost.View("P1").Board.Ships.Length==1,"气泡船选项未建船");
        currentSession.AssertInvariants();
    }
    IEnumerator VerifyRequiredChoicePrompt(string expected)
    {
        yield return null;
        Require(app.HasRequiredTableSelection&&app.CurrentTablePrompt.Contains(expected),"次级屏幕没有说明当前棋子选择；"+RequiredChoicePromptDiagnostic(expected));
        yield return new WaitForSecondsRealtime(1.8f);
        app.ShowTablePrompt("验证提示保留");yield return null;
        Require(app.HasRequiredTableSelection&&app.CurrentTablePrompt.Contains(expected),"未完成的棋子选择提示超时或被短通知覆盖；"+RequiredChoicePromptDiagnostic(expected));
        if(Environment.GetCommandLineArgs().Contains("--feedback-screenshots"))yield return Capture(stage);
    }
    string RequiredChoicePromptDiagnostic(string expected)=>"预期="+expected+"，required="+app.HasRequiredTableSelection+"，human="+app.IsHumanTurn+"，actor="+app.CurrentActor+"，seat="+app.HumanSeat+"，blocked="+app.TablePresentationBlocked+"，prompt="+app.CurrentTablePrompt;
    IEnumerator VerifyBroadcastRecipientClose()
    {
        foreach(int players in new[]{3,4})
        {
            stage=players+"人广播报价接收者关闭";app.StartGame("base","heading-for-new-shores",players,"P2",true);yield return null;yield return null;
            var game=GetComponent<M2App>();var host=game.VerificationHost;var dock=game.VerificationOperationDock;
            var session=(Catan.Core.M2.BaseGameSession)typeof(M2LocalGameHost).GetField("session",Private).GetValue(host);
            var state=session.GetAuthoritativeStateForTesting();state.ActivePlayerId="P1";state.Turn=9;state.Phase=Catan.Core.M2.GamePhase.Action;state.SetupPlacementIndex=players*2;state.PendingDecision=null;
            foreach(var player in state.Players)player.Resources=player.Id=="P1"?new ResourceBag{Wood=1}:new ResourceBag{Wool=1};
            foreach(Resource resource in Enum.GetValues(typeof(Resource)))state.Bank[resource]=19-state.Players.Sum(p=>p.Resources[resource]);
            typeof(Catan.Core.M2.BaseGameSession).GetField("state",Private).SetValue(session,state);session.AssertInvariants();
            Require(host.Submit(new Catan.Core.M2.Command{Id=Guid.NewGuid().ToString("N"),PlayerId="P1",Kind=Catan.Core.M2.CommandKind.ProposeTrade,OtherPlayerId="*",Give=new ResourceBag{Wood=1},Receive=new ResourceBag{Wool=1}}).Success,"广播接收者夹具未能正常报价");
            game.M6Refresh();yield return null;yield return SettleFeedback();
            var hud=BindFeedbackCardHud(game);var snapshot=game.VerificationOperationSnapshot;string revision=snapshot.Revision;
            Require(snapshot.Offer!=null&&snapshot.Offer.CanReject&&dock.HasOverlay,"可回应的广播报价没有向人类接收者开放");
            int responses=0;var respond=snapshot.Respond;snapshot.Respond=choice=>{responses++;respond(choice);};
            app.HandleTableEscape();string immediate=host.ExportSave();dock.CloseWindow(snapshot);
            Require(responses==1&&immediate==host.ExportSave(),"异步关闭期间重复提交拒绝");yield return null;yield return SettleFeedback();
            Require(responses==1&&!app.TableMenuOpen,"Esc未恰好提交一次拒绝并关闭广播报价");
            var offer=host.View("P2").TradeOffer;
            Require(offer!=null&&offer.RejectedPlayerIds.Contains("P2")&&offer.PendingRecipientIds.Length==players-2,"拒绝错误撤回了其他人仍可回应的报价");
            string declined=host.ExportSave();
            hud=BindFeedbackCardHud(game);snapshot=game.VerificationOperationSnapshot;
            Require(snapshot.Revision!=revision&&!OperationDock.HasActionableOffer(snapshot)&&!dock.HasOverlay&&!dock.BlocksBoardInput(new Vector2(720,430)),"拒绝后的新版本投影仍重新弹出广播报价或阻挡桌面");
            int repeats=0;respond=snapshot.Respond;snapshot.Respond=choice=>{repeats++;respond(choice);};dock.CloseWindow(snapshot);
            Require(repeats==0&&declined==host.ExportSave(),"已拒绝的席位再次关闭时重复回应报价");
            app.HandleTableEscape();Require(app.TableMenuOpen&&declined==host.ExportSave(),"关闭广播报价后再次Esc未打开菜单");app.HandleTableEscape();
            // 非参与者看到公开报价状态时，也不能被无操作权限的模态窗口锁住或代替接收者拒绝。
            Require(host.Submit(new Catan.Core.M2.Command{Id=Guid.NewGuid().ToString("N"),PlayerId="P1",Kind=Catan.Core.M2.CommandKind.CancelTrade}).Success,"旁观报价夹具未能撤回旧报价");
            Require(host.Submit(new Catan.Core.M2.Command{Id=Guid.NewGuid().ToString("N"),PlayerId="P1",Kind=Catan.Core.M2.CommandKind.ProposeTrade,OtherPlayerId="P3",Give=new ResourceBag{Wood=1},Receive=new ResourceBag{Wool=1}}).Success,"旁观报价夹具未能建立定向报价");
            game.M6Refresh();yield return null;hud=BindFeedbackCardHud(game);snapshot=game.VerificationOperationSnapshot;
            Require(snapshot.Offer!=null&&!OperationDock.HasActionableOffer(snapshot)&&!dock.HasOverlay,"非参与者被定向报价的模态窗口锁住");
            int spectatorResponses=0;respond=snapshot.Respond;snapshot.Respond=choice=>{spectatorResponses++;respond(choice);};string watching=host.ExportSave();dock.CloseWindow(snapshot);
            Require(spectatorResponses==0&&watching==host.ExportSave(),"旁观者关闭窗口替其他人提交了拒绝");session.AssertInvariants();
        }
    }
    IEnumerator VerifyOperationWindows()
    {
        foreach(string rules in new[]{"base","seafarers","cities-knights","combined"})
        {
            stage=rules+" 操作收起与恢复";app.StartGame(rules,"heading-for-new-shores",4,"P1",true);yield return null;yield return null;
            object game=rules=="base"?(object)GetComponent<M2App>():rules=="seafarers"?GetComponent<M3App>():rules=="cities-knights"?GetComponent<M4App>():GetComponent<M5App>();
            var hud=(TabletopUI)game.GetType().GetField("tabletop",Private).GetValue(game);
            var dock=(OperationDock)game.GetType().GetField("dock",Private).GetValue(game);
            var host=PolishGet(game,"VerificationHost");
            var session=host.GetType().GetField("session",Private).GetValue(host);
            var state=session.GetType().GetMethod("GetAuthoritativeStateForTesting").Invoke(session,null);
            PolishSet(state,"ActivePlayerId","P1");PolishSet(state,"Turn",9);PolishSet(state,"SetupPlacementIndex",8);PolishSetEnum(state,"Phase","Action");
            session.GetType().GetField("state",Private).SetValue(session,state);game.GetType().GetMethod("M6Refresh",Private).Invoke(game,null);yield return null;
            table=(TabletopScene)PolishGet(PolishGet(game,"VerificationBoard"),"TableScene");
            string authority=(string)host.GetType().GetMethod("ExportSave").Invoke(host,null);
            var snapshot=(OperationSnapshot)PolishGet(game,"VerificationOperationSnapshot");
            // 隐藏窗口可能不派发 OnGUI；显式补上 Draw 的绑定，命令仍走原校验入口。
            hud.BindOperations(snapshot,dock.PrepareDevelopmentPurchase,app.HistoryEntries);dock.Windows=hud.OperationWindows;
            PolishSetField(dock,"current",snapshot);PolishSetField(dock,"scope",snapshot.Scope);PolishSetField(dock,"revision",snapshot.Revision);dock.Selection.Sync(snapshot);
            if(snapshot.GoodCount==8){dock.OpenFacility(2);Require(dock.HasOverlay,"设施面板未开启");}
            else {dock.OpenTrade();Require(dock.HasOverlay,"交易草稿未开启");}
            if(Environment.GetCommandLineArgs().Contains("--feedback-screenshots"))yield return Capture(rules+"-操作面板");
            hud.OperationWindows.Hide(snapshot.GoodCount==8?"城市设施":"继续交易");
            Require(!dock.HasOverlay&&hud.OperationHidden,"收起后仍有模态遮挡");
            yield return null;
            Require(authority==(string)host.GetType().GetMethod("ExportSave").Invoke(host,null),"收起修改了权威局面");
            if(Environment.GetCommandLineArgs().Contains("--feedback-screenshots"))yield return Capture(rules+"-收起后继续按钮");
            hud.OperationWindows.Open();Require(dock.HasOverlay,"再次开启没有恢复原操作");
            dock.CloseWindow(snapshot);
            Require(!dock.HasOverlay&&!hud.OperationWindows.Hidden&&hud.OperationWindows.Caption==null,"×关闭仍把窗口收入回合按钮");
            Require(authority==(string)host.GetType().GetMethod("ExportSave").Invoke(host,null),"×关闭草稿修改了权威局面");
            if(snapshot.GoodCount==8){dock.OpenFacility(2);Require(dock.HasOverlay,"关闭后不能从设施重新开启");dock.CloseWindow(snapshot);}
            // 掷骰前关闭设施也必须恢复掷骰按钮，不能残留“城市设施”。
            PolishSetEnum(state,"Phase","ProductionAwaitRoll");session.GetType().GetField("state",Private).SetValue(session,state);game.GetType().GetMethod("M6Refresh",Private).Invoke(game,null);yield return null;
            snapshot=(OperationSnapshot)PolishGet(game,"VerificationOperationSnapshot");hud.BindOperations(snapshot,dock.PrepareDevelopmentPurchase,app.HistoryEntries);
            PolishSetField(dock,"current",snapshot);
            if(snapshot.GoodCount==8){dock.OpenFacility(2);Require(dock.HasOverlay,"掷骰前设施不能打开");dock.CloseWindow(snapshot);Require(!hud.OperationWindows.Hidden&&hud.OperationWindows.Caption==null,"掷骰前关闭设施仍覆盖回合按钮");}
            dock.Reset();
            // 亮牌/骑士替换动画期间，其他玩家仍可查看自己的资料。
            var oldActor=(Func<string>)typeof(M6App).GetField("actor",Private).GetValue(app);
            PolishSetField(app,"actor",(Func<string>)(()=>"P2"));PolishSetField(app,"pendingNetworkPresentation",(Action)(()=>{}));
            Require(app.TablePresentationBlocked&&!app.TableInspectionBlocked,"其他玩家动画封锁了资料查看");
            PolishSetField(app,"pendingNetworkPresentation",null);PolishSetField(app,"actor",oldActor);
        }
        foreach(string rules in new[]{"cities-knights","combined"})
        {
            stage=rules+" 引水渠收起";app.StartGame(rules,"heading-for-new-shores",4,"P1",true);yield return null;yield return null;
            object game=rules=="cities-knights"?(object)GetComponent<M4App>():GetComponent<M5App>();
            var host=PolishGet(game,"VerificationHost");var session=host.GetType().GetField("session",Private).GetValue(host);
            var state=session.GetType().GetMethod("GetAuthoritativeStateForTesting").Invoke(session,null);
            PolishSet(state,"ActivePlayerId","P1");PolishSet(state,"Turn",9);PolishSet(state,"SetupPlacementIndex",8);PolishSetEnum(state,"Phase","PendingChoice");PolishSetEnum(state,"DecisionReturnPhase","Action");
            var pending=Activator.CreateInstance(state.GetType().GetProperty("PendingDecision").PropertyType);PolishSet(pending,"Kind","Aqueduct");PolishSet(pending,"PlayerId","P1");PolishSetEnum(pending,"ReturnPhase","Action");PolishSet(state,"PendingDecision",pending);
            session.GetType().GetField("state",Private).SetValue(session,state);game.GetType().GetMethod("M6Refresh",Private).Invoke(game,null);yield return null;
            var hud=(TabletopUI)game.GetType().GetField("tabletop",Private).GetValue(game);
            var snapshot=(OperationSnapshot)PolishGet(game,"VerificationOperationSnapshot");hud.BindOperations(snapshot,null,app.HistoryEntries);
            var selected=(ResourceBag)game.GetType().GetField("resourceChoiceSelection",Private).GetValue(game);selected.Wheat=1;
            string before=(string)host.GetType().GetMethod("ExportSave").Invoke(host,null);
            hud.OperationWindows.Hide("引水渠 · 选择资源");yield return null;
            Require(!(bool)PolishGet(game,"ResourceChoiceVisible")&&selected.Wheat==1,"收起未保留资源选择");
            Require(before==(string)host.GetType().GetMethod("ExportSave").Invoke(host,null),"关闭待决意外确认领取");
            hud.OperationWindows.Open();Require((bool)PolishGet(game,"ResourceChoiceVisible"),"待决界面不能再打开");
            hud.OperationWindows.Close();yield return null;
            Require(!hud.OperationWindows.Hidden&&hud.OperationWindows.Caption==null&&app.HasRequiredTableSelection,"关闭强制选择错误地进入回合按钮或清除了提示");
            hud.OperationWindows.Open();Require((bool)PolishGet(game,"ResourceChoiceVisible"),"关闭后的强制选择不能恢复");
            PolishSet(pending,"PlayerId","P2");session.GetType().GetField("state",Private).SetValue(session,state);game.GetType().GetMethod("M6Refresh",Private).Invoke(game,null);yield return null;
            Require(!(bool)PolishGet(game,"ResourceChoiceVisible"),"其他玩家的引水渠遮挡本人的历史和手牌");
        }
    }

}
