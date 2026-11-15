using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Catan.Core;
using Catan.Core.ThreeKingdoms;
using UnityEngine;
using M5=Catan.Core.M5;

// 显式隔离验收入口。局面由测试装配，动作仍由实际鼠标、正式UI与权威命令入口完成。
public sealed partial class InteractionPresentationVerification
{
    sealed class MatrixCase
    {
        public string Name,General,Skill,Kind,Pending,Owner="P1";
        public int Count=2,OwnDelta=-2,OtherDelta;
        public bool Active,Progress;
        public MatrixCase(string name,string general,string skill,string kind)
        {Name=name;General=general;Skill=skill;Kind=kind;}
    }
    static MatrixCase[] MatrixCases()=>new[]{
        new MatrixCase("孙尚香-结姻","tk-g27","tk-g27-s1","civil-jieyin-discard"),
        new MatrixCase("戏志才-筹策","tk-g36","tk-g36-s2","civil-chouce"),
        new MatrixCase("徐盛-疑城","tk-g32","tk-g32-s2","civil-yicheng"),
        new MatrixCase("贾诩-间书","tk-g11","tk-g11-s3","civil-jianshu-duel"){OwnDelta=0},
        new MatrixCase("甘宁-魄袭","tk-g08","tk-g08-s2","response-poxi"){OtherDelta=-2},
        new MatrixCase("庞统-连环","tk-g35","tk-g35-s1",null){Active=true,OwnDelta=0},
        new MatrixCase("麹义-伏骑","tk-g46","tk-g46-s1","seat-fuqi"){OwnDelta=1,OtherDelta=-1},
        new MatrixCase("陆逊-谦逊","tk-g23","tk-g23-s2","qianxun-response"){Count=1,OwnDelta=-1},
        new MatrixCase("荀攸-智愚","tk-g34","tk-g34-s2","response-zhiyu"){Owner="P2",Count=1,OwnDelta=-1},
        new MatrixCase("刘琦-问计","tk-g45","tk-g45-s1","b4-wenji"){Owner="P2",Count=1,OwnDelta=-1,OtherDelta=1},
        new MatrixCase("周处-乡害","tk-g53","tk-g53-s1","b5m-tax"){Owner="P2",Count=1,OwnDelta=-1,OtherDelta=1},
        new MatrixCase("袁绍-乱击发动","tk-g10","tk-g10-s1",null){Active=true},
        new MatrixCase("袁绍-乱击抵御","tk-g10","tk-g10-s1","b5m-luanji"){Owner="P2",Count=1,OwnDelta=-1,OtherDelta=1},
        new MatrixCase("发展牌-商港","tk-g12",null,null){Pending="ProgressHarbor",Count=1,OwnDelta=-1,OtherDelta=1},
        new MatrixCase("发展牌-大商家","tk-g12",null,null){Pending="ProgressGuildDues",OwnDelta=2,OtherDelta=-2},
        new MatrixCase("发展牌-锻冶匠","tk-g12",null,null){Kind="smithing",OwnDelta=0},
        new MatrixCase("发展牌-婚礼","tk-g12",null,null){Pending="ProgressWedding",OtherDelta=2},
        new MatrixCase("发展牌-破坏者","tk-g12",null,null){Pending="ProgressSabotage"},
        new MatrixCase("发展牌-超限弃牌","tk-g12",null,null){Pending="ProgressDiscard",Count=1,OwnDelta=0,Progress=true},
        new MatrixCase("孙权-制衡手牌","tk-g22","tk-g22-s1",null){Active=true,OwnDelta=0},
        new MatrixCase("孙权-制衡发展牌","tk-g22","tk-g22-s1",null){Active=true,OwnDelta=0,Progress=true,Count=1}
    };
    static int MatrixHand(M5.GameState s,string player)=>s.Players.Single(p=>p.Id==player).Resources.Total+s.Players.Single(p=>p.Id==player).Commodities.Total;
    static void MatrixGeneral(ThreeKingdomsState tk,string player,string id)
    {
        var a=tk.Generals.Single(g=>g.OwnerPlayerId==player&&g.Region==GeneralRegion.Active);var b=tk.Generals.Single(g=>g.GeneralId==id);
        string old=a.GeneralId,instance=a.InstanceId;a.GeneralId=b.GeneralId;a.InstanceId=b.InstanceId;b.GeneralId=old;b.InstanceId=instance;a.Counters=Array.Empty<GeneralCounter>();
    }
    static void MatrixBalance(M5.GameState s)
    {
        foreach(Resource r in Enum.GetValues(typeof(Resource)))s.Bank[r]=19-s.Players.Sum(p=>p.Resources[r]);
        foreach(M5.Commodity c in Enum.GetValues(typeof(M5.Commodity)))s.CommodityBank[c]=12-s.Players.Sum(p=>p.Commodities[c]);
        foreach(var p in s.Players){p.Pieces.Roads=15-s.Roads.Count(x=>x.PlayerId==p.Id);p.Pieces.Settlements=5-s.Settlements.Count(x=>x.PlayerId==p.Id)+s.DowngradedCities.Count(x=>x.PlayerId==p.Id);}
        foreach(var d in s.ProgressDecks)
        {var cards=M5.ProgressCatalog.Deck(d.Track).ToList();foreach(var c in s.Players.SelectMany(p=>p.ProgressCards.Concat(p.ProgressVictoryCards)).Where(c=>M5.ProgressCatalog.Track(c)==d.Track))if(!cards.Remove(c))throw new Exception("验收牌库不足");d.Cards=cards.ToArray();}
    }
    void MatrixPrepare(M5.CombinedGameSession session,M5.GameState s,MatrixCase test)
    {
        s.ActivePlayerId="P1";s.Phase=M5.GamePhase.Action;s.PendingDecision=null;s.DecisionQueue=Array.Empty<M5.PendingDecision>();s.TradeOffer=null;
        var tk=s.ThreeKingdoms;tk.Clock.ActivePlayerId="P1";tk.Clock.Phase=ThreeKingdomsPhase.Play;tk.Clock.DrawPhaseTurnInstanceId=tk.Clock.TurnInstanceId;
        tk.Skills.Frames=Array.Empty<B2SkillFrame>();tk.Skills.PendingDecision=null;tk.Skills.TimedEffects=Array.Empty<B2TimedEffect>();tk.PendingDecision=null;tk.Response=null;tk.Automation=null;tk.ControlB6=null;tk.AllianceB6=new B6AllianceState();tk.CivilB5=new B5CivilState();
        MatrixGeneral(tk,"P1","tk-g12");MatrixGeneral(tk,"P2","tk-g16");MatrixGeneral(tk,"P3","tk-g37");MatrixGeneral(tk,"P4","tk-g22");MatrixGeneral(tk,test.Owner,test.General);
        foreach(var p in s.Players){p.Resources=new ResourceBag();p.Commodities=new M5.CommodityBag();p.ProgressCards=Array.Empty<M5.ProgressCardKind>();p.PrintedProgressCards=Array.Empty<M5.ProgressCardKind>();}
        var own=s.Players.Single(p=>p.Id=="P1");var other=s.Players.Single(p=>p.Id=="P2");
        own.Resources=new ResourceBag{Wood=2,Brick=1,Ore=2};own.Commodities=new M5.CommodityBag{Cloth=1};
        other.Resources=new ResourceBag{Wood=2,Wool=2,Ore=2};other.Commodities=new M5.CommodityBag{Cloth=2};
        if(test.Kind=="response-zhiyu")other.Resources=new ResourceBag{Wood=2};
        if(test.Kind=="response-zhiyu")other.Commodities=new M5.CommodityBag();
        if(test.Pending=="ProgressSabotage"){own.Resources=new ResourceBag{Wood=2,Brick=1};own.Commodities=new M5.CommodityBag{Cloth=1};}
        if(test.Progress)own.ProgressCards=test.Pending=="ProgressDiscard"?new[]{M5.ProgressCardKind.Crane,M5.ProgressCardKind.Engineering,M5.ProgressCardKind.Mining,M5.ProgressCardKind.CommercialHarbor,M5.ProgressCardKind.Invention}:new[]{M5.ProgressCardKind.Mining};
        if(test.Kind=="seat-fuqi")
        {
            var occupied=s.Settlements.Concat(s.Cities).Select(p=>p.LocationId).ToArray();
            var tile=s.Tiles.First(t=>t.Resource!="sea"&&Enumerable.Range(0,3).Any(i=>!occupied.Contains(t.Vertices[i])&&!occupied.Contains(t.Vertices[i+3])));
            int i=Enumerable.Range(0,3).First(n=>!occupied.Contains(tile.Vertices[n])&&!occupied.Contains(tile.Vertices[n+3]));
            s.Settlements=s.Settlements.Concat(new[]{new PiecePlacement{PlayerId="P1",LocationId=tile.Vertices[i]},new PiecePlacement{PlayerId="P2",LocationId=tile.Vertices[i+3]}}).ToArray();
        }
        if(test.Kind=="smithing")
        {
            own.ProgressCards=new[]{M5.ProgressCardKind.Smithing};
            var occupied=s.Settlements.Concat(s.Cities).Select(p=>p.LocationId).ToArray();
            var board=session.GetPlayerView("P1").Board;var sites=new List<string>();var routes=s.Roads.Select(r=>r.LocationId).Concat(s.Ships.Select(r=>r.LocationId)).ToArray();
            foreach(var edge in board.Edges.Where(e=>!routes.Contains(e.Id)&&s.Tiles.Any(t=>t.Resource!="sea"&&e.Vertices.All(t.Vertices.Contains))))
            {var id=edge.Vertices.FirstOrDefault(v=>!occupied.Contains(v)&&!sites.Contains(v));if(id==null)continue;sites.Add(id);s.Roads=s.Roads.Concat(new[]{new PiecePlacement{PlayerId="P1",LocationId=edge.Id}}).ToArray();if(sites.Count==2)break;}
            Require(sites.Count==2,"锻冶匠缺少两个真实骑士位置");s.Knights=s.Knights.Where(k=>k.PlayerId!="P1").Concat(sites.Select(id=>new M5.Knight{PlayerId="P1",LocationId=id,Level=1,PromotedTurn=-1})).ToArray();
        }
        MatrixBalance(s);
        if(test.Pending!=null)
        {s.Phase=M5.GamePhase.PendingChoice;s.DecisionReturnPhase=M5.GamePhase.Action;s.PendingDecision=new M5.PendingDecision{Kind=test.Pending,PlayerId="P1",OtherPlayerId="P2",Amount=test.Count,ReturnPhase=M5.GamePhase.Action};}
        else if(test.Kind!=null&&test.Kind!="smithing")
        {
            var g=tk.Generals.Single(x=>x.OwnerPlayerId==test.Owner&&x.Region==GeneralRegion.Active);
            var f=new B2SkillFrame{Id="界面验收-"+test.Name,PlayerId="P1",Kind=test.Kind,SkillId=test.Skill,TenureId=g.TenureId,Started=true,Remaining=test.Count,Targets=new[]{test.Owner=="P1"?"P2":"P1"},
                B4=new B4SkillPayload{OwnerPlayerId=test.Owner},CivilB5=new B5CivilPayload{OwnerPlayerId=test.Owner,OtherPlayerId=test.Kind=="civil-chouce"?"P1":null}};
            if(test.Kind=="civil-jianshu-duel")f.Targets=new[]{"P1","P2"};
            if(test.Kind=="b5m-tax")f.Targets=new[]{"P2"};
            if(test.Kind=="response-zhiyu")f.Step=1;
            if(test.Kind=="b5m-luanji")f.Remaining=0;
            tk.Skills.Frames=new[]{f};
            var host=(IThreeKingdomsCardHost)typeof(M5.CombinedGameSession).GetMethod("CardHost",Private).Invoke(session,new object[]{s});B2SkillEngine.Pump(host);
            Require(tk.Skills.PendingDecision?.PlayerId=="P1","没有生成本人真实待决："+test.Name);
        }
        typeof(M5.CombinedGameSession).GetField("state",Private).SetValue(session,s);session.AssertInvariants();
    }
    string MatrixSignature(M5App game,B45SelectionContext selection)
    {
        var privatePick=(OperationSelection)typeof(M5App).GetField("privateProgressSelection",Private).GetValue(game);
        var selectedKnights=(string[])typeof(M5App).GetField("smithingSelected",Private).GetValue(game);
        int harbor=(int)typeof(M5App).GetField("harborOrdinal",Private).GetValue(game);
        return string.Join(",",selection.SelectedCounts)+"|"+string.Join(",",selection.SelectedBankCounts)+"|"+string.Join(",",selection.SelectedPrivateCounts)+"|"+string.Join(",",selection.SelectedProgress)+"|"+
            string.Join(",",game.VerificationOperationDock.Selection.Selected)+"|"+string.Join(",",privatePick.Selected)+"|"+string.Join(",",selectedKnights)+"|"+harbor+"|"+game.VerificationOperationDock.SelectedDiscardCard?.Id;
    }
    IEnumerator MatrixPhoto(string name)
    {
        yield return new WaitForSecondsRealtime(.35f);yield return new WaitForEndOfFrame();
        var shot=ScreenCapture.CaptureScreenshotAsTexture();Require(shot!=null&&shot.GetPixels32().Count(p=>p.r>20||p.g>20||p.b>20)>shot.width*shot.height/8,"矩阵截图黑屏");
        File.WriteAllBytes(Path.Combine(output,name+".png"),shot.EncodeToPNG());Destroy(shot);images++;
    }
    IEnumerator VerifySelectionMatrix()
    {
        stage="选牌矩阵基准";app.StartGame("combined","through-the-desert",4,"P1",true,threeKingdoms:true,harbormaster:true,fishing:true);yield return null;yield return null;
        var game=GetComponent<M5App>();var host=game.VerificationHost;var policy=new Catan.AI.CombinedAi();
        for(int i=0;i<350;i++)
        {var v=host.View("P1");if(v.Turn>=4&&v.ActivePlayerId=="P1"&&v.Phase==M5.GamePhase.Action&&v.ThreeKingdoms.SkillDecision==null)break;var actor=v.ThreeKingdoms.WaitingSelectionPlayerIds.FirstOrDefault()??M5App.Actor(v);var c=policy.Decide(host.AiView(actor));Require(c!=null,"矩阵基准无法推进");c.Id=Guid.NewGuid().ToString("N");Require(host.Submit(c).Success,"矩阵基准命令失败");yield return null;}
        string baseline=host.ExportSave();var cases=MatrixCases();var args=Environment.GetCommandLineArgs();int startFlag=Array.IndexOf(args,"--matrix-start"),start=startFlag<0?0:int.Parse(args[startFlag+1]);
        int endFlag=Array.IndexOf(args,"--matrix-end"),end=endFlag<0?cases.Length-1:int.Parse(args[endFlag+1]);
        for(int index=start;index<cases.Length&&index<=end;index++)
        {
            var test=cases[index];stage=test.Name;host.ImportSave(baseline);var session=(M5.CombinedGameSession)typeof(M5LocalGameHost).GetField("session",Private).GetValue(host);var state=session.GetAuthoritativeStateForTesting();MatrixPrepare(session,state,test);
            typeof(M6App).GetMethod("ResetThreeKingdomsPanel",Private).Invoke(app,null);game.M6Refresh();table=game.VerificationBoard.TableScene;yield return SettleFeedback();yield return new WaitForSecondsRealtime(2);
            if(test.Active)typeof(M6App).GetMethod("BeginGeneralSkill",Private).Invoke(app,new object[]{test.Skill});
            if(test.Progress)game.VerificationOperationDock.FocusGeneralHand(true);
            if(test.Kind=="smithing")typeof(M5App).GetMethod("PrepareProgressCard",Private).Invoke(game,new object[]{game.VerificationOperationSnapshot.Cards.First(c=>c.ArtIndex==(int)M5.ProgressCardKind.Smithing)});
            yield return null;var selection=(B45SelectionContext)typeof(M6App).GetField("generalSelection",Private).GetValue(app);
            if(args.Contains("--matrix-prepare-only"))
            {Require(test.Pending!=null?game.M6View.LegalActions.Length>0:game.M6View.ThreeKingdoms.SkillActions.Length>0||test.Kind=="smithing","待决没有合法动作："+test.Name);Debug.Log("CATAN_MATRIX_PREPARED: "+index+" "+test.Name);continue;}
            string before=host.ExportSave(),initial=MatrixSignature(game,selection),prior=initial;int ownBefore=MatrixHand(state,"P1"),otherBefore=MatrixHand(state,"P2"),cardsBefore=state.Players.Single(p=>p.Id=="P1").ProgressCards.Length;bool selected=false,cancelled=false,readyShot=false,done=false;
            yield return MatrixPhoto(test.Name+"-01待选择");Debug.Log("CATAN_MATRIX_READY: "+index+" "+test.Name);
            while(true)
            {
                string signature=MatrixSignature(game,selection);bool unchanged=host.ExportSave()==before;
                if(unchanged&&signature!=prior)
                {
                    if(!selected&&signature!=initial){selected=true;yield return MatrixPhoto(test.Name+"-02选中");}
                    else if(selected&&!cancelled&&signature==initial){cancelled=true;yield return MatrixPhoto(test.Name+"-03撤选");}
                    else if(cancelled&&!readyShot&&signature!=initial){readyShot=true;yield return MatrixPhoto(test.Name+"-04重新选择");}
                    prior=signature;
                }
                var after=session.GetAuthoritativeStateForTesting();
                bool effect=MatrixHand(after,"P1")==ownBefore+test.OwnDelta&&MatrixHand(after,"P2")==otherBefore+test.OtherDelta;
                if(test.Kind=="civil-jianshu-duel")effect=after.ThreeKingdoms.Skills.Frames.Any(f=>f.Kind==test.Kind&&f.Step==1&&f.CivilB5.Selection.Sum()>=1);
                if(test.Pending=="ProgressDiscard")effect=after.Players.Single(p=>p.Id=="P1").ProgressCards.Length==cardsBefore-1;
                if(test.Progress&&test.Active)effect=after.Players.Single(p=>p.Id=="P1").ProgressCards.Length==cardsBefore;
                if(test.Kind=="smithing")effect=after.Knights.Where(k=>k.PlayerId=="P1").Sum(k=>k.Level)==4;
                if(!unchanged&&effect&&!done){Require(selected&&cancelled&&readyShot,"缺少实际选中、撤选与重选步骤："+test.Name);session.AssertInvariants();done=true;yield return MatrixPhoto(test.Name+"-05确认生效");Debug.Log("CATAN_MATRIX_PASS: "+index+" "+test.Name);File.AppendAllText(Path.Combine(output,"逐项结果.jsonl"),"{\"序号\":"+index+",\"项目\":\""+test.Name+"\",\"选择撤选重选\":true,\"权威效果\":true}\n");}
                File.WriteAllText(Path.Combine(output,"当前项目.json"),"{\"序号\":"+index+",\"项目\":\""+test.Name+"\",\"已选中\":"+selected.ToString().ToLowerInvariant()+",\"已撤选\":"+cancelled.ToString().ToLowerInvariant()+",\"已重选\":"+readyShot.ToString().ToLowerInvariant()+",\"权威已变化\":"+(!unchanged).ToString().ToLowerInvariant()+",\"完成\":"+done.ToString().ToLowerInvariant()+"}");
                if(Input.GetKeyDown(KeyCode.RightBracket)){Require(done,"当前项目尚未完成权威效果确认："+test.Name);break;}yield return null;
            }
        }
    }
}
