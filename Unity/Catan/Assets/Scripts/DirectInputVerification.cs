using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Catan.Core.M5;
using Catan.Core.ThreeKingdoms;
using UnityEngine;

// 仅在显式验收参数下读取独立对局。外部驱动按客户区坐标点击，规则提交仍由正常客户端处理。
public sealed partial class TabletopUiVerification
{
    int directStep;
    readonly List<string> directClickLog = new List<string>();

    IEnumerable DirectClick(Vector2 point, string label, int button=0)
    {
        int step=++directStep;
        var screen=MobileTabletop.ScreenPoint(point);
        var request=Path.Combine(output,"pointer-"+step+".txt");
        File.WriteAllText(request+".tmp", Mathf.RoundToInt(screen.x)+" "+Mathf.RoundToInt(screen.y)+" "+button);
        File.Move(request+".tmp",request);
        directClickLog.Add(label+","+step+","+Mathf.RoundToInt(screen.x)+","+Mathf.RoundToInt(screen.y));
        Debug.Log("DIRECT_INPUT waiting "+step+" "+label+" "+screen);
        double until=Time.realtimeSinceStartupAsDouble+55;
        while(!File.Exists(Path.Combine(output,"done-"+step))&&Time.realtimeSinceStartupAsDouble<until)yield return null;
        Require(File.Exists(Path.Combine(output,"done-"+step)),"未收到窗口输入："+label);
        yield return new WaitForSecondsRealtime(.45f);
    }
    IEnumerator VerifyDirectInput(string[] args)
    {
        mode="combined";
        app.ReadSave(args[Array.IndexOf(args,"--direct-input")+1]);
        yield return null; yield return null;
        Require(app.ThreeKingdoms&&app.Harbormaster&&app.Fishing&&app.PlayerCount==4&&app.ScenarioId=="through-the-desert","验收局配置错误");
        var game=GetComponent<M5App>();
        // 局面来自正常开局与指令重放。只在验收副本中调换指定武将和一张实体牌。
        var session=(CombinedGameSession)typeof(M5LocalGameHost).GetField("session",PrivateInstance).GetValue(game.VerificationHost);
        var state=session.GetAuthoritativeStateForTesting();
        var own=state.Players.First(p=>p.Id==app.HumanSeat);
        var general=state.ThreeKingdoms.Generals.First(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId==own.Id);
        var liu=state.ThreeKingdoms.Generals.First(g=>g.GeneralId=="tk-g15");
        (general.GeneralId,liu.GeneralId)=(liu.GeneralId,general.GeneralId);
        if(!own.ProgressCards.Contains(ProgressCardKind.Engineering))
        {
            var deck=state.ProgressDecks.First(d=>d.Cards.Contains(ProgressCardKind.Engineering));
            int deckIndex=Array.IndexOf(deck.Cards,ProgressCardKind.Engineering);
            deck.Cards=deck.Cards.Where((card,index)=>index!=deckIndex).ToArray();
            own.ProgressCards=own.ProgressCards.Concat(new[]{ProgressCardKind.Engineering}).ToArray();
        }
        typeof(CombinedGameSession).GetField("state",PrivateInstance).SetValue(session,state);
        session.AssertInvariants();
        game.M6Refresh();
        for(int i=0;i<600&&(!Table.Ocean.MaskReady||!app.IsHumanTurn);i++)yield return null;
        yield return new WaitForSecondsRealtime(1);
        var city=game.M6View.LegalActions.First(c=>c.Kind==CommandKind.PlayProgressCard&&c.ProgressCard==ProgressCardKind.Engineering).TargetId;
        var board=game.VerificationBoard;
        var target=board.ScreenPoint(city);
        var destination=MobileTabletop.ScreenPoint(new Vector2(735,420));
        Table.PanFromScreen(new Vector2(target.x,Screen.height-target.y),new Vector2(destination.x,Screen.height-destination.y));
        Table.EndPan();
        yield return null;
        Vector2 CityPoint()=>MobileTabletop.Logical(board.ScreenPoint(city));
        var before=Authority;
        int initialCommands=state.ProcessedCommands.Length;
        long initialSequence=state.EventSequence;
        // 在修城墙前先核对同一座城市的普通操作和取消。
        foreach(var wait in DirectClick(CityPoint(),"城市操作"))yield return wait;
        Require(app.TableOverlayOpen&&Authority==before,"直接点城市没有展开操作");
        yield return new WaitForEndOfFrame();SaveOceanFrame("00-城市就地操作.png");
        foreach(var wait in DirectClick(CityPoint(),"收起城市操作",1))yield return wait;
        foreach(var wait in DirectClick(new Vector2(1226,748),"发展牌页签"))yield return wait;
        Require(Dock.VerificationHandRects.Length==Snapshot.Cards.Count,"发展牌没有显示在手牌区");
        int index=Snapshot.Cards.FindIndex(c=>c.ArtIndex==(int)ProgressCardKind.Engineering);
        var rect=Dock.VerificationHandRects[index];
        yield return new WaitForEndOfFrame();SaveOceanFrame("01-手牌里的工程师.png");
        foreach(var wait in DirectClick(new Vector2(rect.x+18,rect.center.y),"点工程师"))yield return wait;
        var draftField=typeof(M5App).GetField("draft",PrivateInstance);
        Require(((Command)draftField.GetValue(game))?.ProgressCard==ProgressCardKind.Engineering&&Authority==before,"点工程师没有拿起手牌");
        yield return new WaitForEndOfFrame();SaveOceanFrame("02-选中工程师.png");
        foreach(var wait in DirectClick(new Vector2(440,422),"点空处保留工程师"))yield return wait;
        Require(((Command)draftField.GetValue(game))?.ProgressCard==ProgressCardKind.Engineering&&Authority==before,"点空处丢失了工程师");
        foreach(var wait in DirectClick(CityPoint(),"点城市落墙"))yield return wait;
        double deadline=Time.realtimeSinceStartupAsDouble+15;
        while(!game.M6View.Board.Walls.Any(w=>w.LocationId==city)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        Require(game.M6View.Board.Walls.Any(w=>w.LocationId==city&&w.PlayerId==own.Id)&&!game.M6View.OwnProgressCards.Contains(ProgressCardKind.Engineering),"工程师没有建设城墙或手牌未消耗");
        var afterCard=session.GetAuthoritativeStateForTesting();
        Require(afterCard.ProcessedCommands.Length==initialCommands+1&&afterCard.ProcessedCommands.Last().Command.Kind==CommandKind.PlayProgressCard,"出牌指令数或种类错误");
        yield return new WaitForSecondsRealtime(.6f);
        yield return new WaitForEndOfFrame();SaveOceanFrame("03-工程师落墙.png");
        while(app.TablePresentationBlocked&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        Require(!app.TablePresentationBlocked,"工程师动画结束后不能继续使用技能");
        var gift=game.M6View.ThreeKingdoms.SkillActions.First(a=>a.Command.SkillId=="tk-g15-s1"&&a.Command.Counts.Take(8).Sum()==1);
        int good=Array.FindIndex(gift.Command.Counts,c=>c>0);
        string recipient=gift.Command.TargetPlayerIds.Single();
        int mine=Snapshot.Goods[good],theirs=game.VerificationHost.View(recipient).OwnResources.Total+game.VerificationHost.View(recipient).OwnCommodities.Total;
        before=Authority;
        foreach(var wait in DirectClick(new Vector2(211,738),"点仁德"))yield return wait;
        Require(Dock.DirectSelection?.Active==true&&Dock.DirectSelection.CanSelectGood(good,0),"仁德没有进入直接选牌");
        yield return new WaitForEndOfFrame();SaveOceanFrame("04-武将旁发动仁德.png");
        rect=Dock.HandCardRect(good,0).Value;
        foreach(var wait in DirectClick(new Vector2(rect.x+12,rect.center.y),"点赠出的手牌"))yield return wait;
        Require(Dock.DirectSelection.IsGoodSelected(good,0)&&Authority==before,"未抬起指定手牌或提前送出");
        yield return new WaitForEndOfFrame();SaveOceanFrame("05-赠牌已选.png");
        var seats=M6App.OrderedPlayers(app.HistoryPlayers);
        var avatar=TabletopUI.PlayerAvatarRect(Array.IndexOf(seats,recipient),seats.Length).center;
        foreach(var wait in DirectClick(avatar,"点收牌玩家头像"))yield return wait;
        deadline=Time.realtimeSinceStartupAsDouble+12;
        while(Snapshot.Goods[good]==mine&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        var updated=game.VerificationHost.View(recipient);
        Require(Snapshot.Goods[good]==mine-1&&updated.OwnResources.Total+updated.OwnCommodities.Total==theirs+1,"点头像后赠牌未到账");
        var afterGift=session.GetAuthoritativeStateForTesting();
        var commands=afterGift.ProcessedCommands;
        Require(commands.Length==initialCommands+2,"赠牌指令未提交或重复提交");
        Require(commands.Last().Command.Kind==CommandKind.UseGeneralSkill&&commands.Last().Command.SkillCommand.TargetPlayerIds.SequenceEqual(new[]{recipient}),"仁德提交对象不符");
        yield return new WaitForEndOfFrame();SaveOceanFrame("06-仁德赠牌到账.png");
        File.WriteAllLines(Path.Combine(output,"结算核对.csv"),new[]{"阶段,已提交指令数,事件序号","起始,"+initialCommands+","+initialSequence,"工程师,"+afterCard.ProcessedCommands.Length+","+afterCard.EventSequence,"仁德,"+commands.Length+","+afterGift.EventSequence});
        File.WriteAllLines(Path.Combine(output,"点击记录.csv"),new[]{"操作,序号,窗口X,窗口Y"}.Concat(directClickLog));
        Debug.Log("DIRECT_INPUT_OK seed="+state.InitialSeed+" turn="+state.Turn+" city="+city+" gift="+own.Id+"->"+recipient+" good="+good);
        configurations++;
    }
}
