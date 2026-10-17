using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Catan.Core;
using UnityEngine;
using C4=Catan.Core.M4;

// 只在显式 --interaction-verify 下运行隔离显示夹具。不会读取或覆盖玩家的常规存档。
public sealed class InteractionPresentationVerification:MonoBehaviour
{
    M6App app;
    string output,stage;
    int checks,images;
    RenderTexture capture;
    RenderTexture previousTarget;
    string captureName;
    bool began;
    TabletopScene table;
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    void Require(bool value,string message){checks++;if(!value)throw new InvalidOperationException(message);}
    IEnumerator Start()
    {
        var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"--interaction-verify");if(flag<0)yield break;
        output=Path.GetFullPath(args[flag+1]);
        if(!output.StartsWith(Path.GetFullPath(".local")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)){Application.Quit(1);yield break;}
        Directory.CreateDirectory(output);app=GetComponent<M6App>();
        gameObject.AddComponent<InteractionCaptureBegin>().Owner=this;gameObject.AddComponent<InteractionCaptureEnd>().Owner=this;
        yield return null;var run=args.Contains("--resource-layout-review")?VerifyResourceLayouts():Verify();
        while(true)
        {
            object next;
            try{if(!run.MoveNext())break;next=run.Current;}
            catch(Exception ex){Debug.LogError("CATAN_INTERACTION_VERIFY_FAILED: "+stage+" / "+ex);Application.Quit(1);yield break;}
            yield return next;
        }
        File.WriteAllText(Path.Combine(output,"result.json"),"{\"状态\":\"通过\",\"断言数\":"+checks+",\"界面图数\":"+images+"}");
        Debug.Log("CATAN_INTERACTION_VERIFY_OK: checks="+checks+"; images="+images);Application.Quit(0);
    }
    IEnumerator VerifyResourceLayouts()
    {
        stage="丰收资源牌布局";app.StartGame("base","heading-for-new-shores",4,"P1",true);yield return null;yield return null;
        var basic=GetComponent<M2App>();var host=basic.VerificationHost;
        var session=(Catan.Core.M2.BaseGameSession)typeof(M2LocalGameHost).GetField("session",Private).GetValue(host);
        var state=session.GetAuthoritativeStateForTesting();state.ActivePlayerId="P1";state.Phase=Catan.Core.M2.GamePhase.Action;state.SetupPlacementIndex=8;state.Turn=9;state.PendingDecision=null;
        state.Players[0].DevelopmentCards=new[]{new Catan.Core.M2.DevelopmentCard{Kind=Catan.Core.M2.DevelopmentCardKind.YearOfPlenty,BoughtTurn=1}};
        var deck=state.DevelopmentDeck.ToList();deck.Remove(Catan.Core.M2.DevelopmentCardKind.YearOfPlenty);state.DevelopmentDeck=deck.ToArray();
        typeof(Catan.Core.M2.BaseGameSession).GetField("state",Private).SetValue(session,state);session.AssertInvariants();basic.M6Refresh();
        table=basic.VerificationBoard.TableScene;yield return null;yield return new WaitForEndOfFrame();
        basic.VerificationOperationSnapshot.PrepareCard(new OperationHandCard{Id=((int)Catan.Core.M2.DevelopmentCardKind.YearOfPlenty)+":0",Name="丰收"});
        yield return Capture("丰收牌面选择");yield return new WaitForSecondsRealtime(40);
        stage="水渠单种资源布局";app.StartGame("cities-knights","heading-for-new-shores",4,"P1",true);yield return null;yield return null;
        var city=GetComponent<M4App>();var citySession=(C4.CitiesKnightsGameSession)typeof(M4LocalGameHost).GetField("session",Private).GetValue(city.VerificationHost);
        var cityState=citySession.GetAuthoritativeStateForTesting();cityState.ActivePlayerId="P1";cityState.Phase=C4.GamePhase.PendingChoice;cityState.SetupPlacementIndex=8;cityState.Turn=9;
        cityState.PendingDecision=new C4.PendingDecision{Kind="Aqueduct",PlayerId="P1",ReturnPhase=C4.GamePhase.Action};cityState.DecisionReturnPhase=C4.GamePhase.Action;
        typeof(C4.CitiesKnightsGameSession).GetField("state",Private).SetValue(citySession,cityState);citySession.AssertInvariants();city.M6Refresh();
        table=city.VerificationBoard.TableScene;yield return null;yield return Capture("水渠单种资源选择");yield return new WaitForSecondsRealtime(40);
        Require(app.Ready,"资源布局检查意外退出对局");app.ReturnToMainMenu();
    }
    IEnumerator Verify()
    {
        stage="城市骑士公开夹具";
        app.StartGame("cities-knights","heading-for-new-shores",4,"P1",true);yield return null;yield return null;
        var city=GetComponent<M4App>();var host=city.VerificationHost;
        var session=(C4.CitiesKnightsGameSession)typeof(M4LocalGameHost).GetField("session",Private).GetValue(host);
        var state=session.GetAuthoritativeStateForTesting();var board=city.M6View.Board;
        var e1=board.Edges[0];string a=e1.Vertices[0],b=e1.Vertices[1];
        var e2=board.Edges.First(e=>e.Id!=e1.Id&&e.Vertices.Contains(b));string c=e2.Vertices.First(v=>v!=b);
        string site=board.Tiles.First(t=>t.Resource=="wheat").Vertices.First(v=>v!=a&&v!=b&&v!=c);
        state.ActivePlayerId="P1";state.Turn=9;state.Phase=C4.GamePhase.Action;state.SetupPlacementIndex=8;state.PendingDecision=null;
        state.Settlements=new[]{Piece(site)};state.Roads=new[]{Piece(e1.Id),Piece(e2.Id)};
        state.Knights=new[]{new C4.Knight{PlayerId="P1",LocationId=a,Level=1,Active=true}};
        state.Players[0].Resources=new ResourceBag{Wool=3,Wheat=4,Ore=5};
        state.Players[0].ProgressCards=new[]{C4.ProgressCardKind.Irrigation};
        foreach(var player in state.Players)
        {player.Pieces.Roads=15-state.Roads.Count(r=>r.PlayerId==player.Id);player.Pieces.Settlements=5-state.Settlements.Count(s=>s.PlayerId==player.Id);}
        foreach(Resource resource in Enum.GetValues(typeof(Resource)))state.Bank[resource]=19-state.Players.Sum(p=>p.Resources[resource]);
        foreach(var deck in state.ProgressDecks)deck.Cards=deck.Cards.Where(card=>card!=C4.ProgressCardKind.Irrigation).Concat(deck.Track==C4.ProgressCatalog.Track(C4.ProgressCardKind.Irrigation)?Enumerable.Repeat(C4.ProgressCardKind.Irrigation,C4.ProgressCatalog.Count(C4.ProgressCardKind.Irrigation)-1):Array.Empty<C4.ProgressCardKind>()).ToArray();
        typeof(C4.CitiesKnightsGameSession).GetField("state",Private).SetValue(session,state);session.AssertInvariants();city.M6Refresh();
        table=city.VerificationBoard.TableScene;yield return null;yield return new WaitForEndOfFrame();
        stage="城市替换";Require(city.Submit(Command(C4.CommandKind.BuildCity,site)).Success,"升级城市失败");
        yield return null;yield return new WaitForEndOfFrame();
        Require(app.VerificationReplacementOldCount==1,"城市升级没有保留旧模型退场");Require(app.VerificationBuildHiddenRenderers>0,"城市目标提前显现");
        string authority=host.ExportSave();yield return Capture("01-城市替换");yield return Settle();Require(authority==host.ExportSave(),"城市动画改变权威状态");
        stage="骑士升级";Require(city.Submit(Command(C4.CommandKind.PromoteKnight,a)).Success,"晋升失败");
        yield return null;yield return new WaitForEndOfFrame();Require(app.VerificationReplacementOldCount==1,"骑士升级缺少替换");yield return Capture("02-骑士替换");yield return Settle();
        stage="骑士沿路移动";var move=Command(C4.CommandKind.MoveKnight,c);move.SourceId=a;Require(city.Submit(move).Success,"移动失败");
        yield return null;yield return new WaitForEndOfFrame();Require(app.VerificationMovementWaypoints==3,"移动没有经过道路拐点");
        authority=host.ExportSave();yield return new WaitForSecondsRealtime(.33f);
        Require(app.VerificationMovementPosition.y>city.VerificationBoard.Position(a).y,"移动棋子没有浮起");yield return Capture("03-沿路移动");yield return Settle();Require(authority==host.ExportSave(),"移动动画改变权威状态");
        stage="进步卡先展示后效果";int before=city.M6View.OwnResources.Wheat;
        var play=Command(C4.CommandKind.PlayProgressCard);play.ProgressCard=C4.ProgressCardKind.Irrigation;
        Require(city.Submit(play).Success,"出牌失败");Require(app.CardPresentationBusy,"没有出牌公告");
        string committed=host.ExportSave();Require(city.Submit(Command(C4.CommandKind.EndTurn))==null&&committed==host.ExportSave(),"出牌展示期间仍然能提交后续命令");
        Require(host.View("P1").OwnResources.Wheat>before&&city.M6View.OwnResources.Wheat==before,"规则效果与展示快照没有分离");
        authority=host.ExportSave();yield return new WaitForSecondsRealtime(.65f);
        Require(city.M6View.OwnResources.Wheat==before,"卡片退场前提前播放效果");yield return Capture("04-出牌公告");yield return Settle();
        Require(city.M6View.OwnResources.Wheat>before&&authority==host.ExportSave(),"退场后没有衔接效果或动画改写规则");
        stage="银行点牌数量";yield return new WaitForEndOfFrame();city.VerificationOperationDock.OpenTrade();yield return Capture("05a-银行点牌数量");
        if(Environment.GetCommandLineArgs().Contains("--resource-card-review"))yield return new WaitForSecondsRealtime(45);
        city.VerificationOperationDock.Reset();
        stage="交易发起";yield return new WaitForEndOfFrame();city.VerificationOperationDock.OpenTrade("P2");yield return Capture("05-发起交易");if(Environment.GetCommandLineArgs().Contains("--resource-card-review"))yield return new WaitForSecondsRealtime(45);city.VerificationOperationDock.Reset();
        stage="接收交易方向与缺牌";
        app.StartGame("cities-knights","heading-for-new-shores",4,"P2",true);yield return null;yield return null;
        city=GetComponent<M4App>();host=city.VerificationHost;
        session=(C4.CitiesKnightsGameSession)typeof(M4LocalGameHost).GetField("session",Private).GetValue(host);state=session.GetAuthoritativeStateForTesting();
        state.ActivePlayerId="P1";state.Phase=C4.GamePhase.Action;state.SetupPlacementIndex=8;state.Turn=9;state.PendingDecision=null;state.Players[0].Resources.Wood=1;state.Bank.Wood=18;
        typeof(C4.CitiesKnightsGameSession).GetField("state",Private).SetValue(session,state);session.AssertInvariants();
        var offer=Command(C4.CommandKind.ProposeTrade);offer.OtherPlayerId="P2";offer.Give=new ResourceBag{Wood=1};offer.Receive=new ResourceBag{Wool=2};
        Require(host.Submit(offer).Success,"报价夹具失败");city.M6Refresh();table=city.VerificationBoard.TableScene;
        yield return null;yield return new WaitForEndOfFrame();
        var sides=OperationOfferPerspective.For(city.VerificationOperationSnapshot);Require(sides.OwnGive[2]==2&&sides.OtherGive[0]==1,"收到交易的两侧颠倒");yield return Capture("06-接收交易缺牌红色");
        stage="金矿居中八牌";app.StartGame("seafarers","heading-for-new-shores",4,"P1",true);yield return null;yield return null;
        var sea=GetComponent<M3App>();var seaSession=(Catan.Core.M3.SeafarersGameSession)typeof(M3LocalGameHost).GetField("session",Private).GetValue(sea.VerificationHost);
        var seaState=seaSession.GetAuthoritativeStateForTesting();seaState.ActivePlayerId="P1";seaState.Phase=Catan.Core.M3.GamePhase.GoldChoice;seaState.SetupPlacementIndex=8;seaState.Turn=9;seaState.PendingDecision=null;
        seaState.GoldClaims=new[]{new Catan.Core.M3.GoldClaim{PlayerId="P1",Amount=2}};seaState.GoldReturnPhase=Catan.Core.M3.GamePhase.Action;
        seaState.PendingDecision=new Catan.Core.M3.PendingDecision{Kind="Gold",PlayerId="P1",ReturnPhase=Catan.Core.M3.GamePhase.Action};
        typeof(Catan.Core.M3.SeafarersGameSession).GetField("state",Private).SetValue(seaSession,seaState);seaSession.AssertInvariants();sea.M6Refresh();table=sea.VerificationBoard.TableScene;
        yield return null;yield return Capture("07-金矿居中八牌");
        if(Environment.GetCommandLineArgs().Contains("--interaction-review")||Environment.GetCommandLineArgs().Contains("--resource-card-review"))yield return new WaitForSecondsRealtime(45);
        stage="保存退出与重新加载";app.StartGame("base","heading-for-new-shores",4,"P1",true);yield return null;yield return null;
        string saved=app.ExportEnvelope(),path=Path.Combine(output,"隔离保存.json");app.MenuReviewSavePath=path;app.SaveAndReturnToMainMenu();
        Require(app.MenuVisible&&!app.Ready,"保存后仍保留可继续的当前对局");Require(File.ReadAllText(path)==saved,"保存内容不一致");
        app.ReadSave(path);yield return null;yield return null;Require(app.Ready&&app.ExportEnvelope()==saved,"退出后不能独立读档");
        string blocker=Path.Combine(output,"目录阻断");File.WriteAllText(blocker,"测试");app.MenuReviewSavePath=Path.Combine(blocker,"失败.json");app.SaveAndReturnToMainMenu();
        Require(app.Ready&&!app.MenuVisible,"保存失败却丢弃当前游戏");app.MenuReviewSavePath=null;app.ReturnToMainMenu();Require(!app.Ready&&app.MenuVisible,"返回主菜单未清理对局");
    }
    static PiecePlacement Piece(string location)=>new PiecePlacement{PlayerId="P1",LocationId=location};
    static C4.Command Command(C4.CommandKind kind,string target=null)=>new C4.Command{Id=Guid.NewGuid().ToString("N"),PlayerId="P1",Kind=kind,TargetId=target};
    IEnumerator Settle(){double deadline=Time.unscaledTimeAsDouble+8;while(app.TablePresentationBlocked&&Time.unscaledTimeAsDouble<deadline)yield return null;Require(!app.TablePresentationBlocked,"动画未在期限内结束");yield return new WaitForEndOfFrame();}
    IEnumerator Capture(string name)
    {
        yield return new WaitForEndOfFrame();captureName=name;capture=new RenderTexture(1440,900,24,RenderTextureFormat.ARGB32);capture.Create();table.RenderComposite(capture);
        double deadline=Time.unscaledTimeAsDouble+2;while(capture!=null&&Time.unscaledTimeAsDouble<deadline)yield return null;
        if(capture!=null){capture.Release();Destroy(capture);capture=null;Debug.LogWarning("CATAN_INTERACTION_GUI_UNAVAILABLE: "+name);}
    }
    internal void BeginCapture(){if(capture==null||Event.current.type!=EventType.Repaint)return;previousTarget=RenderTexture.active;Graphics.SetRenderTarget(capture);began=true;}
    internal void EndCapture()
    {
        if(!began||capture==null||Event.current.type!=EventType.Repaint)return;
        RenderTexture.active=capture;var pixels=new Texture2D(1440,900,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1440,900),0,0);pixels.Apply();
        File.WriteAllBytes(Path.Combine(output,captureName+".png"),pixels.EncodeToPNG());images++;
        RenderTexture.active=previousTarget;began=false;Destroy(pixels);capture.Release();Destroy(capture);capture=null;
    }
}
public sealed class InteractionCaptureBegin:MonoBehaviour
{
    internal InteractionPresentationVerification Owner;
    void OnGUI(){GUI.depth=30000;Owner?.BeginCapture();}
}
public sealed class InteractionCaptureEnd:MonoBehaviour
{
    internal InteractionPresentationVerification Owner;
    void OnGUI(){GUI.depth=-30000;Owner?.EndCapture();}
}
