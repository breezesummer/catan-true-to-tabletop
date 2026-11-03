using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 显式运行真实 GameView 的外观验收。只用本地测试局；不会连接服务器或写用户存档。
[InitializeOnLoad]
public static class FishingF6EditorVerification
{
    const string Key="Catan.FishingF6Review";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static IEnumerator work;static double next,deadline;static string output;static M6App app;
    static readonly List<string> images=new List<string>();
    static FishingF6EditorVerification(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--fishing-f6-verify");
        if(index<0||index+1>=args.Length)throw new InvalidOperationException("缺少外观验收输出路径");
        var path=Path.GetFullPath(args[index+1]);var root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../.local"))+Path.DirectorySeparatorChar;
        if(!path.StartsWith(root,StringComparison.Ordinal))throw new InvalidOperationException("验收路径必须位于.local内");
        Directory.CreateDirectory(path);SessionState.SetString(Key,path);
        EditorSceneManager.OpenScene("Assets/M6.unity");EditorApplication.isPlaying=true;
    }
    static void Set(object target,string field,object value)=>target.GetType().GetField(field,Flags).SetValue(target,value);
    static object Get(object target,string field)=>target.GetType().GetField(field,Flags).GetValue(target);
    static void Size(int w,int h)=>typeof(B45DockEditorVerification).GetMethod("SetSize",Flags).Invoke(null,new object[]{w,h});
    static void Tick()
    {
        output=SessionState.GetString(Key,"");if(output.Length==0||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try
        {
            if(work==null){app=UnityEngine.Object.FindFirstObjectByType<M6App>();if(app==null)return;images.Clear();work=Flatten(Review());deadline=EditorApplication.timeSinceStartup+240;}
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("外观验收超时");
            if(EditorApplication.timeSinceStartup<next)return;
            if(!work.MoveNext()){Finish(null);return;}
            if(work.Current is double wait)next=EditorApplication.timeSinceStartup+wait;
        }
        catch(Exception ex){Finish(ex);}
    }
    static IEnumerator Flatten(IEnumerator root)
    {
        var stack=new Stack<IEnumerator>();stack.Push(root);
        while(stack.Count>0){var item=stack.Peek();if(!item.MoveNext()){stack.Pop();continue;}if(item.Current is IEnumerator child)stack.Push(child);else yield return item.Current;}
    }
    static IEnumerator Capture(string name)
    {
        yield return .7;
        string path=Path.Combine(output,name+".png");if(File.Exists(path))File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        for(int i=0;i<30&&!File.Exists(path);i++)yield return .1;
        if(!File.Exists(path))throw new InvalidOperationException("没有取得实际游戏截图："+name);
        images.Add(name);
    }
    static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    static Vector2 InputPoint(float x,float y)=>MobileTabletop.Enabled?new Vector2(x*1920f/1440f,y*1080f/900f):new Vector2(x,y);
    static IEnumerator Click(float x,float y,int button=0)
    {
        foreach(var type in new[]{EventType.MouseMove,EventType.MouseDown,EventType.MouseUp})
        {
            typeof(B45DockEditorVerification).GetMethod("Send",Flags).Invoke(null,new object[]{new Event{type=type,button=button,mousePosition=InputPoint(x,y)}});
            int frame=Time.frameCount;yield return .2;for(int i=0;i<40&&Time.frameCount<frame+3;i++)yield return .05;
            Require(Time.frameCount>=frame+3,"GameView未处理输入帧");
        }
    }
    static IEnumerator Hold(float x,float y)
    {
        var send=typeof(B45DockEditorVerification).GetMethod("Send",Flags);
        send.Invoke(null,new object[]{new Event{type=EventType.MouseDown,button=0,mousePosition=InputPoint(x,y)}});
        yield return .75;
        send.Invoke(null,new object[]{new Event{type=EventType.MouseUp,button=0,mousePosition=InputPoint(x,y)}});
        yield return .2;
    }
    static IEnumerator Review()
    {
        Size(MobileTabletop.Enabled?1920:1440,MobileTabletop.Enabled?1080:900);
        yield return 1.0;
        if(Environment.GetCommandLineArgs().Contains("--general-213")){yield return ReviewGenerals213();yield break;}
        FishingF6SampleVerification.Run();
        if(!Environment.GetCommandLineArgs().Contains("--f6-sample"))FishingF6SampleVerification.VerifyAll();
        if(Environment.GetCommandLineArgs().Contains("--f6-visual-only")){yield return ReviewBoards();yield break;}
        Set(app,"menuPage",0);Set(app,"menuSetupStep",2);Set(app,"menuRule",2);Set(app,"menuHarbormaster",true);Set(app,"menuFishing",true);Set(app,"menuVictoryPoints",null);
        yield return Capture("01-城市骑士捕鱼配置");
        foreach(string mode in (Environment.GetCommandLineArgs().Contains("--f6-sample")?new[]{"base"}:new[]{"base","cities-knights"}))
        {
            app.StartGame(mode,"heading-for-new-shores",4,"P1",true,harbormaster:true,fishing:true);yield return 1.5;
            Require(app.Fishing&&app.Harbormaster,"本地宿主没有传入两个独立开关");
            string saved=Path.Combine(output,mode+".json");app.WriteSave(saved);app.ReadSave(saved);yield return 1.5;
            Require(app.Fishing&&app.Harbormaster,"恢复后附加玩法丢失");
            object presentation=mode=="base"?(object)app.GetComponent<M2App>():app.GetComponent<M4App>();
            object host=Get(presentation,"host"),game=Get(host,"session");
            var state=game.GetType().GetMethod("GetAuthoritativeStateForTesting").Invoke(game,null);
            Catan.Core.TradeModules.FishingState fish;
            if(state is Catan.Core.M2.GameState basic){basic.ActivePlayerId="P1";basic.Turn=9;basic.SetupPlacementIndex=8;basic.Phase=Catan.Core.M2.GamePhase.Action;fish=basic.TradeModules.Fishing;}
            else{var city=(Catan.Core.M4.GameState)state;city.ActivePlayerId="P1";city.Turn=9;city.SetupPlacementIndex=8;city.Phase=Catan.Core.M4.GamePhase.Action;fish=city.TradeModules.Fishing;}
            var ids=new[]{"F01","F12","F22"};fish.Supply=System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Except(fish.Supply,ids));fish.Holdings[0].TokenIds=ids;
            Set(game,"state",state);game.GetType().GetMethod("AssertInvariants",new Type[0]).Invoke(game,null);
            presentation.GetType().GetMethod("M6Refresh",Flags).Invoke(presentation,null);yield return 1.0;
            Require(app.IsHumanTurn,"夹具没有进入本人自由行动窗口");
            if(mode=="base")yield return VerifyBottomBoardInput((M2App)presentation);
            string before=(string)game.GetType().GetMethod("Save").Invoke(game,null);
            var inputDock=(FishingDock)Get(presentation,"fishingDock");
            yield return Click(1220,841);
            Require(inputDock.Selected,"鱼手牌页签未选中");
            Require(!inputDock.Contains(new Vector2(1000,710)),"鱼牌空白区域仍遮挡底部棋盘");
            Require(inputDock.Contains(new Vector2(380,755)),"实际鱼牌未拦截棋盘输入");
            Require(inputDock.Contains(new Vector2(1030,679)),"捕鱼操作栏未拦截棋盘输入");
            VerifyPointerDraft(inputDock,presentation);
            yield return Click(380,755,1);
            Require(Get(inputDock,"hint")!=null,"右键未展示鱼筹码说明");
            Require((string)game.GetType().GetMethod("Save").Invoke(game,null)==before,"说明改变了权威状态");
            inputDock.Reset();
            if(MobileTabletop.Enabled){yield return Hold(380,755);
            Require(Get(inputDock,"hint")!=null,"长按未展示鱼筹码说明");}
            Require((string)game.GetType().GetMethod("Save").Invoke(game,null)==before,"长按修改权威状态");
            inputDock.Reset();
            yield return Click(1220,748);Require(!inputDock.Selected,"资源页签没有退出鱼手牌");
            yield return Click(1220,798);Require(!inputDock.Selected,"发展页签没有退出鱼手牌");
            yield return Click(293,784);
            yield return Capture("02-"+mode+"-资源选择");
            Require((string)game.GetType().GetMethod("Save").Invoke(game,null)==before,"预览修改了权威状态");
            yield return Click(1150,679);
            Require((string)game.GetType().GetMethod("Save").Invoke(game,null)==before,"取消扣除了鱼");
            var roadOption=new FishingDock.Option{Kind="spend",Action=Catan.Core.TradeModules.FishAction.BuildRoad,Target="E-input-check",Label="新建道路"};
            int submitted=0;roadOption.Submit=payment=>{Require(payment.Length>0,"建路确认缺少鱼筹码");submitted++;};
            Set(inputDock,"action",(Catan.Core.TradeModules.FishAction?)Catan.Core.TradeModules.FishAction.BuildRoad);
            inputDock.GetType().GetMethod("Choose",Flags).Invoke(inputDock,new object[]{roadOption});
            yield return Capture("02-"+mode+"-道路确认");
            yield return Click(1040,679);
            Require(submitted==1,"道路确认按钮没有且仅有一次提交");
            Require((string)game.GetType().GetMethod("Save").Invoke(game,null)==before,"隔离输入检查修改了权威状态");
            yield return Click(293,784);yield return Click(368,420);yield return Click(800,618);yield return 1.0;
            var after=game.GetType().GetMethod("GetAuthoritativeStateForTesting").Invoke(game,null);
            int wood;string[] remaining;
            if(after is Catan.Core.M2.GameState basicAfter){wood=basicAfter.Players[0].Resources.Wood;remaining=basicAfter.TradeModules.Fishing.Holdings[0].TokenIds;}
            else{var cityAfter=(Catan.Core.M4.GameState)after;wood=cityAfter.Players[0].Resources.Wood;remaining=cityAfter.TradeModules.Fishing.Holdings[0].TokenIds;}
            Require(wood==1&&remaining.Length==1&&remaining[0]=="F12","鱼筹码确认未完成预期支付及资源取得");
            yield return Capture("03-"+mode+"-支付完成");app.ShowMenu();
        }
        if(Environment.GetCommandLineArgs().Contains("--f6-sample"))yield break;
        yield return ReviewKingdoms();
        if(!Environment.GetCommandLineArgs().Contains("--fish-hand-only"))yield return ReviewBoards();
        yield return ReviewReplacement();
        Set(app,"menuPage",0);Set(app,"menuSetupStep",2);Set(app,"menuRule",1);Set(app,"menuScenario",0);
        yield return Capture("04-航海捕鱼已开放");
    }
    static IEnumerator ReviewGenerals213()
    {
        app.StartGame("cities-knights","heading-for-new-shores",3,"P1",true,threeKingdoms:true,fishing:true);yield return 1.5;
        yield return Capture("武将选择");
        var verifier=app.GetComponent<ThreeKingdomsPlayerVerification>();Set(verifier,"app",app);
        for(int i=0;i<600;i++)
        {
            var current=(Catan.Core.ThreeKingdoms.ThreeKingdomsView)typeof(M6App).GetProperty("ThreeKingdomsProjection",Flags).GetValue(app);
            if(current.Clock.Phase==Catan.Core.ThreeKingdoms.ThreeKingdomsPhase.Play&&current.Clock.ActivePlayerId==app.HumanSeat&&current.PendingDecision==null)break;
            if((bool)Get(app,"TableDiceRolling")||app.TablePresentationBlocked){yield return .1;continue;}
            typeof(ThreeKingdomsPlayerVerification).GetMethod("Advance",Flags).Invoke(verifier,null);yield return .05;
        }
        yield return 1.0;
        yield return Click(1220,794);
        Require((bool)typeof(M6App).GetProperty("GeneralHandVisible",Flags).GetValue(app),"武将库页签未进入手牌区");
        yield return Capture("武将手牌与玩家按钮");
        yield return Click(396,790);
        Require(Get(app,"detailGeneralId")!=null,"点击手牌武将未展示详情");
        yield return Capture("手牌武将详情");
        yield return Click(894,282);
        var players=app.HistoryPlayers;var other=players.First(p=>p!=app.HumanSeat);
        int index=Array.IndexOf(players,other);
        yield return Click(1430-players.Length*119+index*119+57,118);
        var projection=(Catan.Core.ThreeKingdoms.ThreeKingdomsView)typeof(M6App).GetProperty("ThreeKingdomsProjection",Flags).GetValue(app);
        var general=projection.Players.First(p=>p.PlayerId==other).ActiveGeneralId;
        Require((string)Get(app,"detailGeneralId")==general,"玩家名字下方武将按钮未打开对应详情");
        yield return Capture("其他玩家武将详情");
        yield return Click(894,282);yield return Click(1220,702);
        Require(!(bool)typeof(M6App).GetProperty("GeneralHandVisible",Flags).GetValue(app),"资源页签未关闭武将手牌");
    }
    static void VerifyPointerDraft(FishingDock source,object presentation)
    {
        var fish=(Catan.Core.TradeModules.FishingView)Get(source,"view");
        var test=new FishingDock();int submitted=0,previews=0;
        var option=new FishingDock.Option{Action=Catan.Core.TradeModules.FishAction.BuildRoad,Target="E-test",Submit=_=>submitted++};
        test.Bind(fish,"P1",new[]{option});test.Show();Set(test,"action",(Catan.Core.TradeModules.FishAction?)Catan.Core.TradeModules.FishAction.BuildRoad);
        var at=MobileTabletop.ScreenPoint(new Vector2(720,400));
        for(int i=0;i<30;i++)test.UpdateMap(at,false,Array.Empty<Catan.Core.Edge>(),Array.Empty<Catan.Core.Tile>(),_=>at,(id,ship)=>{Require(id=="E-test"&&!ship,"道路虚影类型错误");previews++;},()=>{});
        Require(previews==30&&Get(test,"chosen")==null&&submitted==0,"静止悬停没有持续预览，或提前提交");
        test.UpdateMap(at,true,Array.Empty<Catan.Core.Edge>(),Array.Empty<Catan.Core.Tile>(),_=>at,(id,ship)=>{},()=>{});
        Require(Get(test,"chosen")!=null&&submitted==0,"选择落点必须只生成支付草稿");test.Reset();Require(submitted==0,"取消道路草稿提前扣鱼");
        var board=Get(presentation,"board");
        var view=Get(presentation,"view");var publicBoard=view.GetType().GetProperty("Board").GetValue(view);
        var vertices=(Catan.Core.Vertex[])publicBoard.GetType().GetProperty("Vertices").GetValue(publicBoard);
        string vertex=vertices[0].Id;
        var position=(Vector3)board.GetType().GetMethod("Position").Invoke(board,new object[]{vertex});
        Require(position.y>=.012f,"建筑落点没有抬至港口板面上方");
        var preview=board.GetType().GetMethod("PreviewPiece");
        for(int i=0;i<30;i++)preview.Invoke(board,new object[]{vertex,TabletopPointerPreview.ForCommand("BuildSettlement"),"P1",true});
        Require(board.GetType().GetProperty("VerificationPointerPreview",Flags).GetValue(board)!=null,"静止建筑预览丢失");
        board.GetType().GetMethod("ClearPreview").Invoke(board,null);
    }
    static IEnumerator VerifyBottomBoardInput(M2App presentation)
    {
        var dock=(OperationDock)Get(presentation,"dock");
        foreach(float x in new[]{400f,720f,1000f})foreach(float y in new[]{690f,710f,800f})
            Require(!dock.BlocksBoardInput(new Vector2(x,y)),"空手牌透明范围拦截棋盘："+x+","+y);
        Require(dock.BlocksBoardInput(new Vector2(720,870)),"可见底栏未阻挡棋盘");
        var board=(BoardRenderer)Get(presentation,"board");
        var view=(Catan.Core.M2.PlayerView)Get(presentation,"view");
        var vertices=view.Board.Vertices.OrderByDescending(v=>board.ScreenPoint(v.Id).y).Take(3).ToArray();
        foreach(var vertex in vertices)
        {
            board.TableScene.ResetPan();
            var from=board.ScreenPoint(vertex.Id);var to=MobileTabletop.ScreenPoint(new Vector2(720,710));
            board.TableScene.PanFromScreen(new Vector2(from.x,Screen.height-from.y),new Vector2(to.x,Screen.height-to.y));
            board.TableScene.EndPan();
            var point=MobileTabletop.Logical(board.ScreenPoint(vertex.Id));
            Require(Mathf.Abs(point.y-710)<2,"未将底部顶点移入原手牌拦截区");
            bool blocked=(bool)presentation.GetType().GetMethod("DockBlocksMouse",Flags).Invoke(presentation,new object[]{(Vector2?)point});
            Require(!blocked,"底部顶点被实际界面输入链拦截："+vertex.Id);
            string target=(string)presentation.GetType().GetMethod("VerificationPick",Flags).Invoke(presentation,new object[]{Catan.Core.M2.CommandKind.BuildSettlement,board.ScreenPoint(vertex.Id)});
            Require(target==vertex.Id,"底部顶点未命中："+vertex.Id);
            board.PreviewPiece(target,TabletopPointerPreview.ForCommand("BuildSettlement"),"P1",false);
            Require(board.HasPointerPreview,"底部顶点未生成建筑虚影："+vertex.Id);
            board.ClearPreview();

        }
        board.TableScene.ResetPan();
        Debug.Log("BOTTOM_BOARD_INPUT_OK: 底部三个顶点屏幕坐标输入链、拾取与虚影、九处透明空白、可见底栏命中通过");
        yield break;
    }
    static IEnumerator ReviewKingdoms()
    {
        app.StartGame("cities-knights","heading-for-new-shores",4,"P1",true,threeKingdoms:true,harbormaster:false,fishing:true);yield return 1.0;
        var presentation=app.GetComponent<M4App>();var host=(M4LocalGameHost)Get(presentation,"host");host.NewGame(4,97,threeKingdoms:true,harbormaster:false,fishing:true);
        var game=(Catan.Core.M4.CitiesKnightsGameSession)Get(host,"session");var policy=new Catan.AI.CitiesKnightsAi();bool reached=false;
        for(int step=0;step<600;step++)
        {
            var first=game.GetPlayerView("P1");var tk=first.ThreeKingdoms;
            if(first.ActivePlayerId=="P1"&&first.Phase==Catan.Core.M4.GamePhase.Action&&tk.Clock.Phase==Catan.Core.ThreeKingdoms.ThreeKingdomsPhase.Play&&tk.PendingDecision==null&&tk.SkillDecision==null&&first.PendingDecision==null){reached=true;break;}
            string actor=tk.WaitingSelectionPlayerIds?.FirstOrDefault()??tk.PendingDecision?.PlayerId??tk.SkillDecision?.PlayerId??(first.TradeOffer!=null?first.TradeOffer.NextResponder():first.Discards.Length>0?first.Discards[0].PlayerId:first.PendingDecision?.PlayerId??first.ActivePlayerId);
            actor=Catan.Core.ThreeKingdoms.B2SkillEngine.ControllerForViewB6(tk,actor);var command=policy.Decide(game.GetAiPlayerView(actor));command.Id="F6界面推进-"+step;
            var result=game.Execute(command);Require(result.Success,result.ErrorCode);if(step%20==0)yield return .05;
        }
        Require(reached,"三国界面未进入本人自由出牌窗口");
        var state=game.GetAuthoritativeStateForTesting();var fish=state.TradeModules.Fishing;var own=fish.Holdings.Single(h=>h.PlayerId=="P1");
        fish.Supply=fish.Supply.Concat(own.TokenIds).Where(t=>t!="BOOT").ToArray();fish.OldBootPlayerId="P2";own.TokenIds=fish.Supply.Where(t=>t!="BOOT").OrderByDescending(Catan.Core.TradeModules.FishingRules.Value).Take(3).ToArray();fish.Supply=fish.Supply.Except(own.TokenIds).ToArray();
        Set(game,"state",state);game.AssertInvariants();int before=game.GetPlayerView("P1").OwnResources.Total;
        Require(game.GetPlayerView("P1").LegalActions.Any(c=>c.FishAction==Catan.Core.TradeModules.FishAction.TakeResource),"三国测试局面没有可用取资源行动");
        presentation.GetType().GetMethod("M6Refresh",Flags).Invoke(presentation,null);yield return 2.0;
        for(int i=0;i<30&&app.TablePresentationBlocked;i++)yield return .5;
        yield return Click(1220,841);yield return Click(293,784);yield return Click(368,420);
        Require((float)typeof(M6App).GetProperty("SkillBarOffset",Flags).GetValue(app)<-120,"三国阶段条没有避让鱼手牌操作");
        yield return Capture("05-三国城市骑士支付预览");yield return Click(800,618);yield return 1.0;
        Require(game.GetPlayerView("P1").OwnResources.Total==before+1,"三国技能底栏遮挡了鱼支付操作");
        Require(!((TabletopDefenseUI)Get(Get(presentation,"tabletop"),"defense")).MobileSummary.Contains("海港大师"),"单开捕鱼错误展示了海港奖励");
        yield return Capture("06-三国城市骑士支付完成");app.ShowMenu();
    }
    static IEnumerator ReviewReplacement()
    {
        var game=Catan.Core.M2.BaseGameSession.Create(Resources.Load<TextAsset>("m1-scenario").text,4,97,tradeModules:new Catan.Core.TradeModules.TradeModuleOptions{Fishing=true});
        string player=null;var policy=new Catan.AI.BaseGameAi();
        for(int step=0;step<1600;step++)
        {
            var first=game.GetPlayerView("P1");
            if(first.Phase==Catan.Core.M2.GamePhase.FishReplacement){player=first.PendingDecision.PlayerId;break;}
            string actor=first.TradeOffer?.NextResponder()??(first.Discards.Length>0?first.Discards[0].PlayerId:first.PendingDecision?.PlayerId??first.ActivePlayerId);
            var view=game.GetPlayerView(actor);var command=policy.Decide(view);
            // 留鱼以稳定触达上限；仍经合法命令入口，不篡改存档或放宽重放校验。
            if(command.Kind==Catan.Core.M2.CommandKind.SpendFish||command.Kind==Catan.Core.M2.CommandKind.PassOldBoot)
                command=new Catan.Core.M2.Command{PlayerId=actor,Kind=view.Phase==Catan.Core.M2.GamePhase.ProductionAwaitRoll?Catan.Core.M2.CommandKind.RollDice:Catan.Core.M2.CommandKind.EndTurn};
            command.Id="F6换抽推进-"+step;var result=game.Execute(command);Require(result.Success,result.ErrorCode);
            if(step%30==0)yield return .01;
        }
        Require(player!=null,"正常命令未触达换抽待决");string before=game.Save();
        app.StartGame("base","heading-for-new-shores",4,player,true,before);yield return 1.0;
        string file=Path.Combine(output,"换抽私密测试局.json");app.WriteSave(file);app.ReadSave(file);yield return .8;
        var presentation=app.GetComponent<M2App>();game=(Catan.Core.M2.BaseGameSession)Get(Get(presentation,"host"),"session");
        Require(game.Save()==before,"恢复后随机状态或换抽游标改变");
        yield return Click(380,755);yield return Capture("09-满额换抽恢复");
        Require(game.Save()==before,"仅选择换抽筹码就扣除了鱼");
        yield return Click(1034,679);yield return .5;
        Require(game.GetPlayerView(player).TradeModules.Fishing.PendingPlayerId!=player,"确认换抽没有完成本人的待决");game.AssertInvariants();
        yield return Capture("10-换抽完成");app.ShowMenu();
    }
    static IEnumerator ReviewBoards()
    {
        foreach(string mode in new[]{"base","cities-knights","seafarers","combined"})
        {
            app.StartGame(mode,"heading-for-new-shores",4,"P1",true,harbormaster:true,fishing:true);yield return 1.0;
            var layer=UnityEngine.Object.FindFirstObjectByType<FishingTabletopAssets>();Require(layer!=null,"缺少渔场展示层");
            var roots=layer.transform.Cast<Transform>().Where(t=>t.name.StartsWith("渔场 ")).ToArray();
            Require(roots.Length==6,"没有生成六处实体渔场："+mode);
            Require(roots.All(t=>t.GetComponentsInChildren<Collider>().Length==0),"渔场抢占棋盘拾取");
            var ids=roots.Select(t=>t.GetInstanceID()).OrderBy(i=>i).ToArray();
            object presentation=mode=="base"?(object)app.GetComponent<M2App>():mode=="cities-knights"?app.GetComponent<M4App>():mode=="seafarers"?(object)app.GetComponent<M3App>():app.GetComponent<M5App>();
            var projected=presentation.GetType().GetProperty("M6View",Flags).GetValue(presentation);
            var modules=projected.GetType().GetProperty("TradeModules").GetValue(projected);
            var fishing=(Catan.Core.TradeModules.FishingView)modules.GetType().GetProperty("Fishing").GetValue(modules);
            var publicBoard=projected.GetType().GetProperty("Board").GetValue(projected);
            var publicTiles=(Catan.Core.Tile[])publicBoard.GetType().GetProperty("Tiles").GetValue(publicBoard);
            var displayBoard=(Component)Get(presentation,"board");
            FishingCoastVerification.Validate(mode,displayBoard.transform,fishing,publicTiles,id=>(Vector3)displayBoard.GetType().GetMethod("Position").Invoke(displayBoard,new object[]{id}));
            var refresh=presentation.GetType().GetMethod("M6Refresh",Flags);
            var elapsed=new List<double>();
            for(int i=0;i<100;i++){var start=System.Diagnostics.Stopwatch.GetTimestamp();refresh.Invoke(presentation,null);elapsed.Add((System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency);}
            Require(ids.SequenceEqual(layer.transform.Cast<Transform>().Where(t=>t.name.StartsWith("渔场 ")).Select(t=>t.GetInstanceID()).OrderBy(i=>i)),"相同视图重复创建渔场");
            yield return Capture("07-"+mode+"-默认视角");
            var board=Get(presentation,"board");board.GetType().GetMethod("SetCamera").Invoke(board,new object[]{true});yield return Capture("08-"+mode+"-俯视");
            File.WriteAllText(Path.Combine(output,mode+"-刷新性能.json"),JsonUtility.ToJson(new Performance{模式=mode,设备=SystemInfo.deviceModel,处理器=SystemInfo.processorType,显卡=SystemInfo.graphicsDeviceName,样本数=elapsed.Count,平均毫秒=elapsed.Average(),最大毫秒=elapsed.Max()},true));
            // 同一公开局面连续采样；控制显示新增渔场的开关，不能据此声称目标设备60FPS。
            if(mode=="combined")foreach(bool visible in new[]{false,true})
            {
                foreach(var root in roots)root.gameObject.SetActive(visible);
                var frames=new List<double>();int last=Time.frameCount;
                for(int i=0;i<150;){yield return null;if(Time.frameCount==last)continue;last=Time.frameCount;if(i++>=30)frames.Add(Time.unscaledDeltaTime*1000.0);}
                File.WriteAllText(Path.Combine(output,visible?"模型开启帧间隔.json":"模型关闭帧间隔.json"),JsonUtility.ToJson(new Performance{模式="双扩展相同局面、编辑器帧间隔",设备=SystemInfo.deviceModel,处理器=SystemInfo.processorType,显卡=SystemInfo.graphicsDeviceName,样本数=frames.Count,平均毫秒=frames.Average(),最大毫秒=frames.Max()},true));
            }
            app.ShowMenu();yield return .3;
        }
    }
    [Serializable] sealed class Performance{public string 模式,设备,处理器,显卡;public int 样本数;public double 平均毫秒,最大毫秒;}
    [Serializable] sealed class Result{public string 状态,说明;public string[] 截图;}
    static void Finish(Exception error)
    {
        File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(new Result{状态=error==null?"通过":"失败",说明=error==null&&Environment.GetCommandLineArgs().Contains("--general-213")?"武将库页签、手牌武将详情、其他玩家武将详情及切回资源手牌的真实GameView点击通过；四张截图需审看。":error==null&&Environment.GetCommandLineArgs().Contains("--f6-visual-only")?"模型复验：13件资产、四模式六渔场、无碰撞体、海岸拼接、100次重复刷新对象复用、默认／俯视截图与帧间隔采样通过；不计人工或设备验收。":error==null&&Environment.GetCommandLineArgs().Contains("--f6-sample")?"仅样板：尺寸、枢轴、材质、无碰撞体，以及基础版支付取消／确认、右键、移动端预览长按通过；尚未运行全模式。":error==null?"基础与城市骑士开关、存档恢复、支付预览、取消及确认通过；三国城市骑士技能底栏与鱼支付共同显示且可确认。筹码右键、适用时移动端预览长按和支付取消／确认通过；正常合法命令推进至满额换抽，真实读档及界面确认通过。采用测试局面和Unity原生GameView事件，非物理鼠标、Android真机或用户试玩；截图需另行审看。":error.ToString(),截图=images.ToArray()},true));
        SessionState.EraseString(Key);if(error!=null)Debug.LogException(error);EditorApplication.Exit(error==null?0:1);
    }
}
