using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 显式运行真实 GameView 的外观验收。只用本地测试局；不会连接服务器或写用户存档。
[InitializeOnLoad]
public static class HarborF1EditorVerification
{
    const string Key="Catan.HarborF1Review";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static IEnumerator work;static double next,deadline;static string output;static M6App app;
    static readonly List<string> images=new List<string>();
    static HarborF1EditorVerification(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--harbor-f1-verify");
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
    static IEnumerator Review()
    {
        Size(1920,1080);yield return 1.0;
        Set(app,"menuPage",0);Set(app,"menuSetupStep",2);Set(app,"menuRule",0);Set(app,"menuHarbormaster",true);Set(app,"menuVictoryPoints",null);
        var defaults=typeof(M6App).GetMethod("MenuDefaultVictoryPoints",Flags);
        Require((int)defaults.Invoke(app,new object[]{"base","heading-for-new-shores",4})==11,"海港默认目标未增加一次");
        yield return Capture("01-海港开局配置");
        Set(app,"menuVictoryPoints",16);Set(app,"menuHarbormaster",false);Set(app,"menuHarbormaster",true);
        Require((int)Get(app,"menuVictoryPoints")==16,"开关改变了手动目标");
        yield return Capture("02-手动目标保持16");
        foreach(string mode in new[]{"base","seafarers","cities-knights","combined"})
        {
            app.StartGame(mode,"heading-for-new-shores",4,"P1",true,harbormaster:true);yield return 1.5;
            Require(app.Harbormaster,"本地宿主没有开启海港大师");
            int target=app.TargetVictoryPoints;string save=Path.Combine(output,mode+".json");app.WriteSave(save);app.ReadSave(save);yield return 1.5;
            Require(app.Harbormaster&&app.TargetVictoryPoints==target,"存档未恢复海港开关或目标");
            yield return Capture("03-"+mode+"-恢复后奖励区");
            app.ShowMenu();Require(Get(app,"menuVictoryPoints")==null,"默认目标读档后被误记成手动目标");
        }
        app.StartGame("base","heading-for-new-shores",4,"P1",true,targetVictoryPoints:16,harbormaster:true);yield return 1.5;
        string manual=Path.Combine(output,"manual.json");app.WriteSave(manual);app.ReadSave(manual);yield return 1.5;
        app.ShowMenu();Require((int)Get(app,"menuVictoryPoints")==16,"手动目标读档后丢失");
        app.StartGame("base","heading-for-new-shores",4,"P1",true,harbormaster:true);yield return 1.5;
        var baseApp=app.GetComponent<M2App>();var host=(M2LocalGameHost)Get(baseApp,"host");var game=(Catan.Core.M2.BaseGameSession)Get(host,"session");
        var state=game.GetAuthoritativeStateForTesting();var view=game.GetPlayerView("P1");
        string first=view.Board.Ports[0].Vertices[0];string second=null;
        foreach(var port in view.Board.Ports)
        {
            string candidate=port.Vertices[0];bool adjacent=candidate==first;
            foreach(var edge in view.Board.Edges)if(System.Array.IndexOf(edge.Vertices,first)>=0&&System.Array.IndexOf(edge.Vertices,candidate)>=0)adjacent=true;
            if(!adjacent){second=candidate;break;}
        }
        Require(second!=null,"缺少展示用独立港口");
        state.Cities=new[]{new Catan.Core.PiecePlacement{LocationId=first,PlayerId="P1"}};
        state.Settlements=new[]{new Catan.Core.PiecePlacement{LocationId=second,PlayerId="P1"}};
        state.Players[0].Pieces.Cities=3;state.Players[0].Pieces.Settlements=4;
        state.ActivePlayerId="P1";state.Turn=1;state.SetupPlacementIndex=8;state.Phase=Catan.Core.M2.GamePhase.Action;state.PendingDecision=null;
        Set(game,"state",state);typeof(Catan.Core.M2.BaseGameSession).GetMethod("RecalculateAwards",Flags).Invoke(game,new object[]{state});game.AssertInvariants();
        typeof(M2App).GetMethod("M6Refresh",Flags).Invoke(baseApp,null);
        Require(game.GetPlayerView("P1").OwnVictoryPoints==5,"海港奖励未纳入总分");
        yield return Capture("04-持有海港奖励夹具");
        app.ShowMenu();Set(app,"menuPage",0);Set(app,"menuSetupStep",2);Set(app,"menuRule",1);
        Set(app,"menuScenario",System.Array.IndexOf(Catan.Core.M3.SeafarersScenarios.Ids,"pirate-islands"));
        yield return Capture("05-海盗群岛禁用");
    }
    [Serializable] sealed class Result{public string 状态,说明;public string[] 截图;}
    static void Finish(Exception error)
    {
        File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(new Result{状态=error==null?"通过":"失败",说明=error==null?"四模式开关、目标和存档恢复通过；奖励截图含明确构造的规则夹具，截图仍需人工审看。":error.ToString(),截图=images.ToArray()},true));
        SessionState.EraseString(Key);if(error!=null)Debug.LogException(error);EditorApplication.Exit(error==null?0:1);
    }
}
