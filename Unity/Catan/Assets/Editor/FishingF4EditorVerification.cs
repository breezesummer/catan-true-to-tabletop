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
public static class FishingF4EditorVerification
{
    const string Key="Catan.FishingF4Review";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static IEnumerator work;static double next,deadline;static string output;static M6App app;
    static readonly List<string> images=new List<string>();
    static FishingF4EditorVerification(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--fishing-f4-verify");
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
            if(work==null){app=UnityEngine.Object.FindFirstObjectByType<M6App>();if(app==null)return;images.Clear();work=Flatten(Review());deadline=EditorApplication.timeSinceStartup+360;}
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
    static IEnumerator Click(float x,float y,int button=0)
    {
        foreach(var type in new[]{EventType.MouseMove,EventType.MouseDown,EventType.MouseUp})
        {
            typeof(B45DockEditorVerification).GetMethod("Send",Flags).Invoke(null,new object[]{new Event{type=type,button=button,mousePosition=new Vector2(x,y)}});
            int frame=Time.frameCount;yield return .2;for(int i=0;i<40&&Time.frameCount<frame+3;i++)yield return .05;
            Require(Time.frameCount>=frame+3,"GameView未处理输入帧");
        }
    }
    static IEnumerator Review()
    {
        Size(1440,900);yield return 1.0;
        Set(app,"menuPage",0);Set(app,"menuSetupStep",2);Set(app,"menuRule",3);Set(app,"menuHarbormaster",true);Set(app,"menuFishing",true);Set(app,"menuVictoryPoints",null);
        yield return Capture("01-双扩展捕鱼配置");
        foreach(string mode in new[]{"seafarers","combined"})
        {
            app.StartGame(mode,"heading-for-new-shores",4,"P1",true,harbormaster:true,fishing:true);yield return 1.5;
            Require(app.Fishing&&app.Harbormaster,"本地宿主没有传入两个独立开关");
            string saved=Path.Combine(output,mode+".json");app.WriteSave(saved);app.ReadSave(saved);yield return 1.5;
            Require(app.Fishing&&app.Harbormaster,"恢复后附加玩法丢失");
            object presentation=mode=="seafarers"?(object)app.GetComponent<M3App>():app.GetComponent<M5App>();
            object host=Get(presentation,"host"),game=Get(host,"session");
            var state=game.GetType().GetMethod("GetAuthoritativeStateForTesting").Invoke(game,null);
            Catan.Core.TradeModules.FishingState fish;
            if(state is Catan.Core.M3.GameState basic){basic.ActivePlayerId="P1";basic.Turn=9;basic.SetupPlacementIndex=8;basic.Phase=Catan.Core.M3.GamePhase.Action;fish=basic.TradeModules.Fishing;}
            else{var city=(Catan.Core.M5.GameState)state;city.ActivePlayerId="P1";city.Turn=9;city.SetupPlacementIndex=8;city.Phase=Catan.Core.M5.GamePhase.Action;fish=city.TradeModules.Fishing;}
            var ids=new[]{"F01","F12","F22"};fish.Supply=System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Except(fish.Supply,ids));fish.Holdings[0].TokenIds=ids;
            Set(game,"state",state);game.GetType().GetMethod("AssertInvariants",new Type[0]).Invoke(game,null);
            presentation.GetType().GetMethod("M6Refresh",Flags).Invoke(presentation,null);yield return 1.0;
            Require(app.IsHumanTurn,"夹具没有进入本人自由行动窗口");
            string before=(string)game.GetType().GetMethod("Save").Invoke(game,null);
            yield return Click(778,624);yield return Click(685,528);yield return Click(405,567);
            yield return Capture("02-"+mode+"-支付预览");
            Require((string)game.GetType().GetMethod("Save").Invoke(game,null)==before,"预览修改了权威状态");
            yield return Click(1010,567);Require((string)game.GetType().GetMethod("Save").Invoke(game,null)==before,"取消扣除了鱼");
            yield return Click(778,624);yield return Click(685,528);yield return Click(405,567);yield return Click(907,567);yield return 1.0;
            var after=game.GetType().GetMethod("GetAuthoritativeStateForTesting").Invoke(game,null);
            int wood;string[] remaining;
            if(after is Catan.Core.M3.GameState basicAfter){wood=basicAfter.Players[0].Resources.Wood;remaining=basicAfter.TradeModules.Fishing.Holdings[0].TokenIds;}
            else{var cityAfter=(Catan.Core.M5.GameState)after;wood=cityAfter.Players[0].Resources.Wood;remaining=cityAfter.TradeModules.Fishing.Holdings[0].TokenIds;}
            Require(wood==1&&remaining.Length==1&&remaining[0]=="F12","鱼筹码确认未完成预期支付及资源取得");
            yield return Capture("03-"+mode+"-支付完成");app.ShowMenu();
        }
        yield return ReviewSeaTargets();
        yield return ReviewKingdoms();
        Set(app,"menuPage",0);Set(app,"menuSetupStep",2);Set(app,"menuRule",1);Set(app,"menuScenario",4);
        yield return Capture("04-F5捕鱼保持禁用");
    }
    static Vector2 MapPoint(M3BoardRenderer board,string id)
    {
        // EditorApplication.update中的Screen尺寸不一定等于GameView；验收窗口固定1440×900。
        var v=board.BoardCamera.WorldToViewportPoint(board.Position(id)+Vector3.up*.005f);var r=board.BoardCamera.rect;
        return new Vector2((r.x+v.x*r.width)*1440,(1-r.y-v.y*r.height)*900);
    }
    static IEnumerator ReviewSeaTargets()
    {
        app.StartGame("seafarers","four-islands",4,"P1",true,fishing:true);yield return 1.0;
        var presentation=app.GetComponent<M3App>();var host=(M3LocalGameHost)Get(presentation,"host");host.NewGame("four-islands",4,97,fishing:true);
        var game=(Catan.Core.M3.SeafarersGameSession)Get(host,"session");var policy=new Catan.AI.SeafarersAi();
        for(int step=0;step<16;step++)
        {
            var first=game.GetPlayerView("P1");var view=game.GetPlayerView(first.ActivePlayerId);var c=policy.Decide(view);c.Id="F4海图开局-"+step;
            if(view.Phase==Catan.Core.M3.GamePhase.SetupSettlement)
            {
                var coast=view.Board.Edges.Where(e=>view.Board.Tiles.Count(t=>e.Vertices.All(t.Vertices.Contains)&&t.Resource!="sea")==1).SelectMany(e=>e.Vertices).ToArray();
                c.TargetId=view.LegalVertexIds.First(coast.Contains);
            }
            Require(game.Execute(c).Success,"海图界面开局失败");
        }
        var state=game.GetAuthoritativeStateForTesting();state.ActivePlayerId="P1";state.Phase=Catan.Core.M3.GamePhase.Action;state.PendingDecision=null;state.Turn=9;
        var fish=state.TradeModules.Fishing;fish.Supply=fish.Supply.Concat(fish.Holdings.SelectMany(h=>h.TokenIds)).Where(t=>t!="BOOT").ToArray();foreach(var h in fish.Holdings)h.TokenIds=Array.Empty<string>();
        fish.OldBootPlayerId="P1";fish.Holdings[0].TokenIds=new[]{"F22","F23","F24"};fish.Supply=fish.Supply.Except(fish.Holdings[0].TokenIds).ToArray();
        state.PirateTileId=null;
        Set(game,"state",state);game.AssertInvariants();presentation.GetType().GetMethod("M6Refresh",Flags).Invoke(presentation,null);yield return 1.0;
        var board=(M3BoardRenderer)Get(presentation,"board");var viewNow=game.GetPlayerView("P1");
        var ship=viewNow.LegalFishingActions.First(c=>c.FishBuildShip);
        var shipEdge=viewNow.Board.Edges.Single(e=>e.Id==ship.TargetId);
        state.PirateTileId=state.Tiles.First(t=>t.Resource=="sea"&&!shipEdge.Vertices.All(t.Vertices.Contains)).Id;
        Set(game,"state",state);presentation.GetType().GetMethod("M6Refresh",Flags).Invoke(presentation,null);yield return .5;
        int before=state.Ships.Length;var point=MapPoint(board,ship.TargetId);
        bool both=viewNow.LegalFishingActions.Any(c=>c.FishAction==Catan.Core.TradeModules.FishAction.BuildRoad&&!c.FishBuildShip&&c.TargetId==ship.TargetId);
        yield return Click(778,624);yield return Click(814,528);
        point=MapPoint(board,ship.TargetId);
        Debug.Log("F4_TARGET: "+ship.TargetId+" point="+point+" both="+both);
        yield return Click(point.x+(both?18:0),point.y);
        Debug.Log("F4_CHOSEN: "+Get(Get(presentation,"fishingDock"),"chosen"));
        yield return Capture("07-棋盘直接选船支付");yield return Click(907,567);yield return 1.0;
        Require(game.GetAuthoritativeStateForTesting().Ships.Length==before+1,"棋盘直接选船未提交");
        var pirate=MapPoint(board,state.PirateTileId);
        yield return Click(778,624);yield return Click(424,528);yield return Click(pirate.x,pirate.y);
        yield return Capture("08-棋盘直接选海盗支付");yield return Click(907,567);yield return 1.0;
        Require(game.GetAuthoritativeStateForTesting().PirateTileId==null,"海盗未移出棋盘");
        yield return Click(778,624);yield return Click(1044,528);
        presentation.GetType().GetMethod("TableSeatClick",Flags).Invoke(presentation,new object[]{"P2"});yield return .5;
        yield return Capture("09-头像入口转交旧靴");yield return Click(907,567);yield return 1.0;
        Require(game.GetAuthoritativeStateForTesting().TradeModules.Fishing.OldBootPlayerId=="P2","头像入口没有完成旧靴转交");
        app.ShowMenu();
    }
    static IEnumerator ReviewKingdoms()
    {
        app.StartGame("combined","heading-for-new-shores",4,"P1",true,threeKingdoms:true,harbormaster:false,fishing:true);yield return 1.0;
        var presentation=app.GetComponent<M5App>();var host=(M5LocalGameHost)Get(presentation,"host");host.NewGame("heading-for-new-shores",4,97,threeKingdoms:true,harbormaster:false,fishing:true);
        var game=(Catan.Core.M5.CombinedGameSession)Get(host,"session");var policy=new Catan.AI.CombinedAi();bool reached=false;
        for(int step=0;step<600;step++)
        {
            var first=game.GetPlayerView("P1");var tk=first.ThreeKingdoms;
            if(first.ActivePlayerId=="P1"&&first.Phase==Catan.Core.M5.GamePhase.Action&&tk.Clock.Phase==Catan.Core.ThreeKingdoms.ThreeKingdomsPhase.Play&&tk.PendingDecision==null&&tk.SkillDecision==null&&first.PendingDecision==null){reached=true;break;}
            string actor=tk.WaitingSelectionPlayerIds?.FirstOrDefault()??tk.PendingDecision?.PlayerId??tk.SkillDecision?.PlayerId??(first.TradeOffer!=null?first.TradeOffer.NextResponder():first.Discards.Length>0?first.Discards[0].PlayerId:first.PendingDecision?.PlayerId??first.ActivePlayerId);
            actor=Catan.Core.ThreeKingdoms.B2SkillEngine.ControllerForViewB6(tk,actor);var command=policy.Decide(game.GetAiPlayerView(actor));command.Id="F4界面推进-"+step;
            var result=game.Execute(command);Require(result.Success,result.ErrorCode);if(step%20==0)yield return .05;
        }
        Require(reached,"三国界面未进入本人自由出牌窗口");
        var state=game.GetAuthoritativeStateForTesting();var fish=state.TradeModules.Fishing;var own=fish.Holdings.Single(h=>h.PlayerId=="P1");
        fish.Supply=fish.Supply.Concat(own.TokenIds).Where(t=>t!="BOOT").ToArray();fish.OldBootPlayerId="P2";own.TokenIds=fish.Supply.Where(t=>t!="BOOT").OrderByDescending(Catan.Core.TradeModules.FishingRules.Value).Take(3).ToArray();fish.Supply=fish.Supply.Except(own.TokenIds).ToArray();
        Set(game,"state",state);game.AssertInvariants();int before=game.GetPlayerView("P1").OwnResources.Total;
        Require(game.GetPlayerView("P1").LegalActions.Any(c=>c.FishAction==Catan.Core.TradeModules.FishAction.TakeResource),"三国测试局面没有可用取资源行动");
        presentation.GetType().GetMethod("M6Refresh",Flags).Invoke(presentation,null);yield return 2.0;
        for(int i=0;i<30&&app.TablePresentationBlocked;i++)yield return .5;
        yield return Click(778,624);yield return Click(685,528);yield return Click(405,567);
        yield return Capture("05-三国双扩展支付预览");yield return Click(907,567);yield return 1.0;
        Require(game.GetPlayerView("P1").OwnResources.Total==before+1,"三国技能底栏遮挡了鱼支付操作");
        Require(!((TabletopDefenseUI)Get(Get(presentation,"tabletop"),"defense")).MobileSummary.Contains("海港大师"),"单开捕鱼错误展示了海港奖励");
        yield return Capture("06-三国双扩展支付完成");app.ShowMenu();
    }
    [Serializable] sealed class Result{public string 状态,说明;public string[] 截图;}
    static void Finish(Exception error)
    {
        File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(new Result{状态=error==null?"通过":"失败",说明=error==null?"航海与双扩展开关、存档恢复、支付预览、取消及确认通过；三国双扩展技能底栏与鱼支付共同显示且可确认。采用测试局面和Unity原生GameView鼠标事件，非物理鼠标、Android真机或用户试玩；包含原生棋盘点击建船、移海盗及头像处理入口转交旧靴；头像入口由反射调用，不计物理头像点击。截图需另行审看。":error.ToString(),截图=images.ToArray()},true));
        SessionState.EraseString(Key);if(error!=null)Debug.LogException(error);EditorApplication.Exit(error==null?0:1);
    }
}
