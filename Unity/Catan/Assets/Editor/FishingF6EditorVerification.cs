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
    static Vector2 InputPoint(float x,float y)=>MobileTabletop.Enabled?new Vector2(96+x*1.2f,y*1.2f):new Vector2(x,y);
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
        Size(MobileTabletop.Enabled?1920:1440,MobileTabletop.Enabled?1080:900);yield return 1.0;
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
            string before=(string)game.GetType().GetMethod("Save").Invoke(game,null);
            var inputDock=(FishingDock)Get(presentation,"fishingDock");
            yield return Click(832,622,1);
            Require(Get(inputDock,"hint")!=null,"右键未展示鱼筹码说明");
            Require((string)game.GetType().GetMethod("Save").Invoke(game,null)==before,"说明改变了权威状态");
            inputDock.Reset();
            if(MobileTabletop.Enabled){yield return Hold(832,622);
            Require(Get(inputDock,"hint")!=null,"长按未展示鱼筹码说明");}
            Require((string)game.GetType().GetMethod("Save").Invoke(game,null)==before,"长按修改权威状态");
            inputDock.Reset();
            yield return Click(778,624);yield return Click(685,528);yield return Click(405,567);
            yield return Capture("02-"+mode+"-支付预览");
            Require((string)game.GetType().GetMethod("Save").Invoke(game,null)==before,"预览修改了权威状态");
            yield return Click(1010,567);Require((string)game.GetType().GetMethod("Save").Invoke(game,null)==before,"取消扣除了鱼");
            yield return Click(778,624);yield return Click(685,528);yield return Click(405,567);yield return Click(907,567);yield return 1.0;
            var after=game.GetType().GetMethod("GetAuthoritativeStateForTesting").Invoke(game,null);
            int wood;string[] remaining;
            if(after is Catan.Core.M2.GameState basicAfter){wood=basicAfter.Players[0].Resources.Wood;remaining=basicAfter.TradeModules.Fishing.Holdings[0].TokenIds;}
            else{var cityAfter=(Catan.Core.M4.GameState)after;wood=cityAfter.Players[0].Resources.Wood;remaining=cityAfter.TradeModules.Fishing.Holdings[0].TokenIds;}
            Require(wood==1&&remaining.Length==1&&remaining[0]=="F12","鱼筹码确认未完成预期支付及资源取得");
            yield return Capture("03-"+mode+"-支付完成");app.ShowMenu();
        }
        if(Environment.GetCommandLineArgs().Contains("--f6-sample"))yield break;
        yield return ReviewKingdoms();
        yield return ReviewBoards();
        yield return ReviewReplacement();
        Set(app,"menuPage",0);Set(app,"menuSetupStep",2);Set(app,"menuRule",1);Set(app,"menuScenario",0);
        yield return Capture("04-航海捕鱼已开放");
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
        yield return Click(778,624);yield return Click(685,528);yield return Click(405,567);
        yield return Capture("05-三国城市骑士支付预览");yield return Click(907,567);yield return 1.0;
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
        yield return Click(832,622);yield return Capture("09-满额换抽恢复");
        Require(game.Save()==before,"仅选择换抽筹码就扣除了鱼");
        yield return Click(910,530);yield return .5;
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
            for(int i=0;i<roots.Length;i++)for(int j=0;j<i;j++)Require(Vector3.Distance(roots[i].position,roots[j].position)>=.028f,"窄水道渔场重叠");
            var ids=roots.Select(t=>t.GetInstanceID()).OrderBy(i=>i).ToArray();
            object presentation=mode=="base"?(object)app.GetComponent<M2App>():mode=="cities-knights"?app.GetComponent<M4App>():mode=="seafarers"?(object)app.GetComponent<M3App>():app.GetComponent<M5App>();
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
        File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(new Result{状态=error==null?"通过":"失败",说明=error==null&&Environment.GetCommandLineArgs().Contains("--f6-visual-only")?"模型复验：13件资产、四模式六渔场、无碰撞体、最小间距、100次重复刷新对象复用、默认／俯视截图与帧间隔采样通过；不计人工或设备验收。":error==null&&Environment.GetCommandLineArgs().Contains("--f6-sample")?"仅样板：尺寸、枢轴、材质、无碰撞体，以及基础版支付取消／确认、右键、移动端预览长按通过；尚未运行全模式。":error==null?"基础与城市骑士开关、存档恢复、支付预览、取消及确认通过；三国城市骑士技能底栏与鱼支付共同显示且可确认。筹码右键、适用时移动端预览长按和支付取消／确认通过；正常合法命令推进至满额换抽，真实读档及界面确认通过。采用测试局面和Unity原生GameView事件，非物理鼠标、Android真机或用户试玩；截图需另行审看。":error.ToString(),截图=images.ToArray()},true));
        SessionState.EraseString(Key);if(error!=null)Debug.LogException(error);EditorApplication.Exit(error==null?0:1);
    }
}
