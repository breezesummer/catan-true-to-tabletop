using System;
using System.Collections;
using System.IO;
using System.Linq;
using Catan.Core.M5;
using Catan.Core.TradeModules;
using UnityEngine;
using Catan.Core.ThreeKingdoms;

public sealed partial class TabletopUiVerification
{
    IEnumerator VerifyHudEffects(string[] args)
    {
        mode="combined";
        app.ReadSave(args[Array.IndexOf(args,"--hud-effects")+1]);
        yield return null;yield return null;
        Require(app.PlayerCount==4 && app.Harbormaster && app.Fishing && app.ThreeKingdoms && app.ScenarioId=="through-the-desert","验收局面错误");
        auditGame=GetComponent<M5App>();
        auditSession=(CombinedGameSession)typeof(M5LocalGameHost).GetField("session",PrivateInstance).GetValue(auditGame.VerificationHost);
        var state=auditSession.GetAuthoritativeStateForTesting();
        if(args.Contains("--guan-yu-portrait"))
        {
            var active=state.ThreeKingdoms.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId==app.HumanSeat);
            var guan=state.ThreeKingdoms.Generals.Single(g=>g.GeneralId=="tk-g18");
            (active.GeneralId,guan.GeneralId)=(guan.GeneralId,active.GeneralId);
            (active.InstanceId,guan.InstanceId)=(guan.InstanceId,active.InstanceId);
            ActivateAudit(state);
            var guanSkin=GeneralSkinArt.Get("tk-g18");
            double deadline=Time.realtimeSinceStartupAsDouble+20;
            while(!GeneralSkinArt.IdleReady(guanSkin)||app.OpeningOrderMessage.Length>0)
            {Require(Time.realtimeSinceStartupAsDouble<deadline,"关羽动皮未就绪");yield return null;}
            yield return new WaitForEndOfFrame();SaveOceanFrame("关羽待机.png");configurations++;yield break;
        }
        if(args.Contains("--fixed-event-faces"))
        {
            var active=state.ThreeKingdoms.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId==app.HumanSeat);
            var fixedGeneral=state.ThreeKingdoms.Generals.Single(g=>g.GeneralId=="tk-g21");
            (active.GeneralId,fixedGeneral.GeneralId)=(fixedGeneral.GeneralId,active.GeneralId);
            (active.InstanceId,fixedGeneral.InstanceId)=(fixedGeneral.InstanceId,active.InstanceId);
            var host=(IThreeKingdomsCardHost)typeof(CombinedGameSession).GetMethod("CardHost",PrivateInstance).Invoke(auditSession,new object[]{state});
            state.ThreeKingdoms.Dice.OpenedTurnId=-1;
            B3DiceEngine.BeginTurn(host);
            ActivateAudit(state);
            yield return new WaitForSecondsRealtime(1);
            app.SyncThreeKingdomsSelection();
            var picker=(B45SelectionContext)typeof(M6App).GetField("generalSelection",PrivateInstance).GetValue(app);
            if(picker.AwaitingActivation)picker.OpenActivationChoices();
            yield return new WaitForEndOfFrame();
            Require(picker.Options.Where(a=>a.Command.Option=="fixed").Select(a=>a.Command.CardType).Distinct().Count()==4,"直谏四个候选未就绪");
            string before=Authority;
            int redBefore=app.FixedDieValue(0);
            if(args.Contains("--dice-drag"))
            {
                var start=new Vector2(62,223);
                var faces=TabletopWorldObjects.DieFaces(0,redBefore);
                var drags=new[]{Vector2.down,Vector2.up,Vector2.right,Vector2.left};
                int[] expected={faces[3],redBefore,faces[4],redBefore};
                for(int i=0;i<drags.Length;i++)
                {
                    foreach(var step in DirectClick(start,"拖动红骰",0,start+36*drags[i]))yield return step;
                    Require(app.FixedDieValue(0)==expected[i]&&app.FixedDieValue(1)==7-expected[i],"拖骰方向或联动骰面错误 "+i);
                }
            }
            foreach(var step in DirectClick(new Vector2(62,223),"拖转红骰"))yield return step;
            Require(app.FixedDieValue(0)!=redBefore&&app.FixedDieValue(0)+app.FixedDieValue(1)==7,"红白骰未联动");
            var eventPick=typeof(M6App).GetField("fixedChoiceEvent",PrivateInstance);
            int nextSymbol=(app.FixedDieValue(2)+1)%4;
            int expectedFace=nextSymbol==0?1:nextSymbol+3;
            foreach(var step in DirectClick(new Vector2(98,184),"转动事件骰"))yield return step;
            Require((int)eventPick.GetValue(app)==expectedFace,"事件骰未切换到下一种骰面");
            Require(Authority==before,"确认前改变了权威局面");
            yield return new WaitForEndOfFrame();
            SaveOceanFrame("直谏事件骰.png");
            int red=app.FixedDieValue(0);
            foreach(var step in DirectClick(TabletopUI.EndTurnRect.center,"确认直谏"))yield return step;
            var result=auditSession.GetAuthoritativeStateForTesting().ThreeKingdoms.Dice.FixedResult;
            Require(result!=null&&result.Red==red&&result.White==7-red&&result.EventFace==expectedFace,"直谏确认结果错误");
            configurations++;
            yield break;
        }
        if(args.Contains("--dice-selection"))
        {
            var active=state.ThreeKingdoms.Generals.Single(g=>g.Region==GeneralRegion.Active && g.OwnerPlayerId==app.HumanSeat);
            var ghost=state.ThreeKingdoms.Generals.Single(g=>g.GeneralId=="tk-g13");
            (active.GeneralId,ghost.GeneralId)=(ghost.GeneralId,active.GeneralId);
            (active.InstanceId,ghost.InstanceId)=(ghost.InstanceId,active.InstanceId);
            var host=(IThreeKingdomsCardHost)typeof(CombinedGameSession).GetMethod("CardHost",PrivateInstance).Invoke(auditSession,new object[]{state});
            B3DiceEngine.BeginJudgment(host,app.HumanSeat,"骰子光效验收");
            ActivateAudit(state);
            yield return new WaitForSecondsRealtime(1);
            app.SyncThreeKingdomsSelection();
            var picker=(B45SelectionContext)typeof(M6App).GetField("generalSelection",PrivateInstance).GetValue(app);
            Require(picker.DiceIndices.Contains(0)&&picker.DiceIndices.Contains(1),"缺少红白候选骰子");
            Require(picker.SelectDie(0),"无法选中红骰");
            string before=Authority;
            for(int i=0;i<2;i++)
            {
                yield return new WaitForSecondsRealtime(.35f);
                yield return new WaitForEndOfFrame();
                SaveOceanFrame("骰子选择-"+i+".png");
            }
            Require(app.DieVisualState(0)==2&&app.DieVisualState(1)==1&&Authority==before,"选骰光效状态错误");
            configurations++;
            yield break;
        }
        var fish=state.TradeModules.Fishing;
        if(args.Contains("--fish-road-prompt"))
        {
            var held=fish.Holdings.Single(h=>h.PlayerId==app.HumanSeat);
            var payment=new[]{2,3}.Select(value=>fish.Supply.First(id=>FishingRules.Value(id)==value)).ToArray();
            var discard=fish.Supply.Except(payment).Where(id=>FishingRules.Value(id)>0).Take(4).ToArray();
            fish.Supply=fish.Supply.Except(payment).Except(discard).ToArray();
            fish.Discard=fish.Discard.Concat(discard).ToArray();
            held.TokenIds=held.TokenIds.Concat(payment).ToArray();
            ActivateAudit(state);
            double readyUntil=Time.realtimeSinceStartupAsDouble+30;
            while(auditGame.VerificationOperationDock.Fishing==null||app.OpeningOrderMessage.Length>0)
            {
                Require(Time.realtimeSinceStartupAsDouble<readyUntil,"鱼手牌区尚未显示");
                yield return new WaitForEndOfFrame();
            }
            Require(auditGame.M6View.LegalActions.Any(c=>c.FishAction==FishAction.BuildRoad),"验收局缺少可修建道路");
            var dock=auditGame.VerificationOperationDock.Fishing;
            var original=Authority;
            dock.Show();
            yield return new WaitForEndOfFrame();
            SaveOceanFrame("鱼资源.png");
            typeof(FishingDock).GetField("action",PrivateInstance).SetValue(dock,FishAction.BuildRoad);
            yield return new WaitForEndOfFrame();
            SaveOceanFrame("鱼资源选路.png");
            Require(dock.Active&&Authority==original,"选择道路时改变了对局");
            configurations++;
            yield break;
        }
        Require(fish.Supply.Contains(FishingRules.OldBootId),"验收起点未保留旧靴");
        fish.Supply=fish.Supply.Where(t=>t!=FishingRules.OldBootId).ToArray();
        fish.OldBootPlayerId=app.HumanSeat;
        ActivateAudit(state);
        var projection=app.ThreeKingdomsProjection;
        var own=projection.Players.Single(p=>p.PlayerId==app.HumanSeat);
        Require(projection.Clock.ActivePlayerId==own.PlayerId,"本席不是行动席");
        var skin=GeneralSkinArt.Get(own.ActiveGeneralId);
        Require(skin!=null,"本席缺少动皮");
        bool oldStatic=skin.useStatic;
        int theme=StrategyHudTheme.Selected;
        try
        {
            StrategyHudTheme.Select(1);GeneralSkinArt.SetStatic(skin,false);
            double until=Time.realtimeSinceStartupAsDouble+15;
            while(!GeneralSkinArt.IdleReady(skin)||app.OpeningOrderMessage.Length>0)
            {Require(Time.realtimeSinceStartupAsDouble<until,"武将牌未就绪");yield return null;}
            var authority=Authority;
            if(args.Contains("--portrait-glow"))
            {
                foreach(bool fixedSkin in new[]{false,true})
                {
                    GeneralSkinArt.SetStatic(skin,fixedSkin);
                    var seen=new bool[10];
                    double deadline=Time.realtimeSinceStartupAsDouble+12;
                    while(seen.Any(value=>!value))
                    {
                        yield return new WaitForEndOfFrame();
                        Require(Time.realtimeSinceStartupAsDouble<deadline,"未捕获完整武将光效周期");
                        int frame=Mathf.FloorToInt(Mathf.Repeat(Time.unscaledTime,.9f)*10f/.9f);
                        if(seen[frame])continue;
                        seen[frame]=true;
                        var screen=ScreenCapture.CaptureScreenshotAsTexture();
                        int w=Mathf.RoundToInt(Screen.width*224f/1920),h=Mathf.RoundToInt(Screen.height*282f/1080);
                        var crop=new Texture2D(w,h,TextureFormat.RGB24,false);
                        try
                        {
                            crop.SetPixels(screen.GetPixels(Mathf.RoundToInt(Screen.width*16f/1920),0,w,h));
                            crop.Apply();
                            File.WriteAllBytes(Path.Combine(output,(fixedSkin?"静皮":"动皮")+frame+".png"),crop.EncodeToPNG());
                            if(!fixedSkin)
                            {
                                int sw=Mathf.RoundToInt(Screen.width*184f/1920),sh=Mathf.RoundToInt(Screen.height*292f/1080);
                                var seat=new Texture2D(sw,sh,TextureFormat.RGB24,false);
                                try
                                {
                                    seat.SetPixels(screen.GetPixels(Screen.width-sw,Screen.height-sh,sw,sh));
                                    seat.Apply();
                                    File.WriteAllBytes(Path.Combine(output,"席位"+frame+".png"),seat.EncodeToPNG());
                                }
                                finally{Destroy(seat);}
                            }
                        }
                        finally {Destroy(crop);Destroy(screen);}
                    }
                }
                Require(Authority==authority,"切换武将框光效改变对局");
                configurations++;
                yield break;
            }
            yield return null;yield return new WaitForEndOfFrame();
            foreach(var kind in new[]{"settlement","city","road","ship","wall","knight-1","knight-2","knight-3"})
            {
                int level=kind.StartsWith("knight")?kind[7]-'0':1;
                var tex=OperationPieceAssets.Texture(kind,own.PlayerId,level);
                Require(tex!=null,"缺少库存图 "+kind);
                Require(tex==Resources.Load<Texture2D>("StockModels/"+kind+"-"+PlayerComponentAssets.ColourKey(own.PlayerId)),"库存未切到实体模型 "+kind);
            }
            SaveOceanFrame("模型库存与外光.png");
            Require(authority==Authority,"库存检查改变对局");
            configurations++;
        }
        finally {GeneralSkinArt.SetStatic(skin,oldStatic);StrategyHudTheme.Select(theme);}
    }
}
