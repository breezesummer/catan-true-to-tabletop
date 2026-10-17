using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using C4=Catan.Core.M4;
using C5=Catan.Core.M5;

// 仅供 --card-player-verify 的隔离运行，使用正式指令和正式展示入口。
public sealed partial class InteractionPresentationVerification
{
    IEnumerator VerifyCardPlayers()
    {
        bool visual=!Environment.GetCommandLineArgs().Contains("--card-player-headless");
        foreach(string rules in new[]{"cities-knights","combined"})
        foreach(string card in new[]{"Printing","Constitution"})
        foreach(string owner in new[]{"P1","P2"})
        foreach(bool eventDraw in new[]{false,true})
        {
            stage=rules+" / "+card+" / "+owner+" / "+(eventDraw?"事件骰":"保卫奖励");
            app.StartGame(rules,"heading-for-new-shores",4,"P1",true);yield return null;yield return null;
            Action refresh;Func<string> save;Func<int> publicPoints;Func<int> authorityPoints;
            Action draw;
            if(rules=="cities-knights")
            {
                var city=GetComponent<M4App>();var host=city.VerificationHost;
                var session=(C4.CitiesKnightsGameSession)typeof(M4LocalGameHost).GetField("session",Private).GetValue(host);
                var state=session.GetAuthoritativeStateForTesting();var kind=(C4.ProgressCardKind)Enum.Parse(typeof(C4.ProgressCardKind),card);var track=C4.ProgressCatalog.Track(kind);
                state.ActivePlayerId="P1";state.Turn=9;state.SetupPlacementIndex=8;state.Phase=C4.GamePhase.PendingChoice;state.DecisionReturnPhase=C4.GamePhase.Action;
                state.PendingDecision=new C4.PendingDecision{Kind="DefenderProgress",PlayerId=owner,ReturnPhase=C4.GamePhase.Action};
                if(eventDraw){state.Phase=C4.GamePhase.ProductionAwaitRoll;state.PendingDecision=null;state.AlchemyDice1=state.AlchemyDice2=1;state.Random.State=card=="Printing"?2u:48u;state.Players.Single(p=>p.Id==owner).Improvements[(int)track]=1;}
                var deck=state.ProgressDecks.Single(d=>d.Track==track);deck.Cards=new[]{kind}.Concat(deck.Cards.Where(c=>c!=kind)).ToArray();
                typeof(C4.CitiesKnightsGameSession).GetField("state",Private).SetValue(session,state);session.AssertInvariants();city.M6Refresh();
                refresh=city.M6Refresh;save=host.ExportSave;publicPoints=()=>city.M6View.Players.Single(p=>p.Id==owner).VictoryPoints;authorityPoints=()=>host.View(owner).OwnVictoryPoints;
                draw=()=>Require(host.Submit(new C4.Command{Id=Guid.NewGuid().ToString("N"),PlayerId=eventDraw?"P1":owner,Kind=eventDraw?C4.CommandKind.RollDice:C4.CommandKind.ResolveChoice,Track=track}).Success,"城市骑士抽牌失败");
                table=city.VerificationBoard.TableScene;
            }
            else
            {
                var combined=GetComponent<M5App>();var host=combined.VerificationHost;
                var session=(C5.CombinedGameSession)typeof(M5LocalGameHost).GetField("session",Private).GetValue(host);
                var state=session.GetAuthoritativeStateForTesting();var kind=(C5.ProgressCardKind)Enum.Parse(typeof(C5.ProgressCardKind),card);var track=C5.ProgressCatalog.Track(kind);
                state.ActivePlayerId="P1";state.Turn=9;state.SetupPlacementIndex=8;state.Phase=C5.GamePhase.PendingChoice;state.DecisionReturnPhase=C5.GamePhase.Action;
                state.PendingDecision=new C5.PendingDecision{Kind="DefenderProgress",PlayerId=owner,ReturnPhase=C5.GamePhase.Action};
                if(eventDraw){state.Phase=C5.GamePhase.ProductionAwaitRoll;state.PendingDecision=null;state.AlchemyDice1=state.AlchemyDice2=1;state.Random.State=card=="Printing"?2u:48u;state.Players.Single(p=>p.Id==owner).Improvements[(int)track]=1;}
                var deck=state.ProgressDecks.Single(d=>d.Track==track);deck.Cards=new[]{kind}.Concat(deck.Cards.Where(c=>c!=kind)).ToArray();
                typeof(C5.CombinedGameSession).GetField("state",Private).SetValue(session,state);session.AssertInvariants();combined.M6Refresh();
                refresh=combined.M6Refresh;save=host.ExportSave;publicPoints=()=>combined.M6View.Players.Single(p=>p.Id==owner).VictoryPoints;authorityPoints=()=>host.View(owner).OwnVictoryPoints;
                draw=()=>Require(host.Submit(new C5.Command{Id=Guid.NewGuid().ToString("N"),PlayerId=eventDraw?"P1":owner,Kind=eventDraw?C5.CommandKind.RollDice:C5.CommandKind.ResolveChoice,Track=track}).Success,"组合规则抽牌失败");
                table=combined.VerificationBoard.TableScene;
            }
            yield return null;
            foreach(string id in new[]{"P1","P2","P3","P4"})
            {
                string name=app.PlayerDisplayName(id);Require(!PlayerIdentityModel.IsPlayerId(name),"界面仍使用玩家编号");
                Require(M6App.DisplayPlayerText("轮到 "+id)=="轮到 "+name,"玩家提示没有替换姓名");
                Require(M6App.PlayerNameStyle(new GUIStyle(),id).normal.textColor==BoardRenderer.SeatColor(id),"姓名颜色与棋子不一致");
            }
            int before=publicPoints();draw();string committed=save();refresh();
            Require(app.CardPresentationBusy&&app.TablePresentationBlocked,"自动分数卡没有阻塞展示并亮牌");
            Require(app.PresentedCard.PlayerId==owner&&app.PresentedCard.ImmediateVictory,"自动分数卡归属或类型错误");
            Require(app.PresentedCard.CardName==(card=="Printing"?"印刷术":"宪法"),"展示了错误卡名");
            Require(EntityCardAssets.ProgressFront(app.PresentedCard.ProgressIndex)!=null,"缺少分数卡正面贴图");
            Require(authorityPoints()==before+1&&publicPoints()==before,"动画未分离权威结算与显示快照");
            if(eventDraw)
            {
                Require(app.CardPresentationRoll!=null,"亮牌没有关联公开骰点");
                yield return new WaitForSecondsRealtime(.2f);if(visual)Require(app.TableDiceRolling,"亮牌前没有先播放掷骰");
            }
            yield return new WaitForSecondsRealtime(.65f);refresh();
            Require(publicPoints()==before&&save()==committed,"动画提前刷新或改写权威状态");
            if(visual&&owner=="P1"&&!eventDraw)yield return Capture(rules+"-"+card+"-立即公开");
            yield return Settle();Require(publicPoints()==before+1&&save()==committed,"亮牌结束后没有公开计分");
            refresh();Require(!app.CardPresentationBusy,"刷新重复播放分数卡");
            if(eventDraw&&visual)Require(!app.TableDiceRolling,"亮牌后重复播放同一次掷骰");
            if(visual&&owner=="P1"&&!eventDraw)yield return Capture(rules+"-"+card+"-计分后");
        }
        app.ReturnToMainMenu();
    }
}
