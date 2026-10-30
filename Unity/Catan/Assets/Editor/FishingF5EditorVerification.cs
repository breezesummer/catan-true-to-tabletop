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
public static class FishingF5EditorVerification
{
    const string Key="Catan.FishingF5Review";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static IEnumerator work;static double next,deadline;static string output;static M6App app;
    static readonly List<string> images=new List<string>();
    static FishingF5EditorVerification(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--fishing-f5-verify");
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
    static IEnumerator Review()
    {
        Size(1440,900);yield return 1.0;
        string[] maps={"forgotten-tribe","cloth-for-catan","wonders-of-catan","new-world"};
        foreach(string mode in new[]{"seafarers","combined"})foreach(string map in maps)
        {
            app.StartGame(mode,map,4,"P1",true,harbormaster:true,fishing:true);yield return 1.0;
            Require(app.Fishing&&app.Harbormaster,"第三批附加开关未传入宿主");
            string saved=Path.Combine(output,mode+"-"+map+".json");app.WriteSave(saved);app.ReadSave(saved);yield return .5;
            object presentation=mode=="seafarers"?(object)app.GetComponent<M3App>():app.GetComponent<M5App>();
            object game=Get(Get(presentation,"host"),"session");
            if(map=="new-world")
            {
                yield return Capture(mode+"-新世界待放港口");
                for(int i=0;i<10;i++)
                {
                    if(game is Catan.Core.M3.SeafarersGameSession sea)
                    {
                        Require(sea.GetPlayerView("P1").TradeModules.Fishing.Grounds.Length==0,"港口未完成就出现渔场");
                        var first=sea.GetPlayerView("P1");var command=new Catan.AI.SeafarersAi().Decide(sea.GetAiPlayerView(first.ActivePlayerId));command.Id="F5港口-"+i;
                        Require(sea.Execute(command).Success,"航海放港口失败");
                    }
                    else
                    {
                        var city=(Catan.Core.M5.CombinedGameSession)game;
                        Require(city.GetPlayerView("P1").TradeModules.Fishing.Grounds.Length==0,"双扩展港口未完成就出现渔场");
                        var first=city.GetPlayerView("P1");var command=new Catan.AI.CombinedAi().Decide(city.GetAiPlayerView(first.ActivePlayerId));command.Id="F5港口-"+i;
                        Require(city.Execute(command).Success,"双扩展放港口失败");
                    }
                    yield return .05;
                }
                presentation.GetType().GetMethod("M6Refresh",Flags).Invoke(presentation,null);
                app.WriteSave(saved);app.ReadSave(saved);yield return .5;
                presentation=mode=="seafarers"?(object)app.GetComponent<M3App>():app.GetComponent<M5App>();game=Get(Get(presentation,"host"),"session");
            }
            var fish=game is Catan.Core.M3.SeafarersGameSession basic?basic.GetPlayerView("P1").TradeModules.Fishing:((Catan.Core.M5.CombinedGameSession)game).GetPlayerView("P1").TradeModules.Fishing;
            Require(fish.Grounds.Length==6,"第三批渔场数量不正确");
            Require((fish.LakeTileId!=null)==(map=="forgotten-tribe"),"第三批湖泊配置不正确");
            game.GetType().GetMethod("AssertInvariants",new Type[0]).Invoke(game,null);
            yield return Capture(mode+"-"+map);app.ShowMenu();
        }
        Set(app,"menuPage",0);Set(app,"menuSetupStep",2);Set(app,"menuRule",1);Set(app,"menuScenario",6);Set(app,"menuFishing",true);Set(app,"menuHarbormaster",true);Set(app,"menuVictoryPoints",15);
        yield return Capture("海盗群岛海港与捕鱼禁用");
        Require(!(bool)Get(app,"menuFishing")&&(bool)Get(app,"menuHarbormaster"),"海盗群岛开关不一致");
        Require((int?)Get(app,"menuVictoryPoints")==15,"海港目标被旧13分上限截断");
        app.StartGame("seafarers","pirate-islands",4,"P1",true,targetVictoryPoints:15,harbormaster:true);yield return 1.0;
        Require(app.Harbormaster&&!app.Fishing,"海盗群岛海港入口错误");yield return Capture("海盗群岛15分海港开局");
    }
    [Serializable] sealed class Result{public string 状态,说明;public string[] 截图;}
    static void Finish(Exception error)
    {
        File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(new Result{状态=error==null?"通过":"失败",说明=error==null?"四图航海与双扩展本地入口、保存恢复、渔场与湖泊显示通过；新世界10次合法港口指令后生成六渔场并恢复。海盗群岛海港可用、捕鱼禁用及15分开局通过。使用真实GameView和产品AI港口命令，不计人工鼠标、真机或外网验收。截图需另行审看。":error.ToString(),截图=images.ToArray()},true));
        SessionState.EraseString(Key);if(error!=null)Debug.LogException(error);EditorApplication.Exit(error==null?0:1);
    }
}
