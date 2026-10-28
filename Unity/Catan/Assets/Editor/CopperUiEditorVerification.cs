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
public static class CopperUiEditorVerification
{
    const string Key="Catan.CopperReview";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static IEnumerator work;static double next,deadline;static string output;static M6App app;
    static readonly List<string> images=new List<string>();
    static CopperUiEditorVerification(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--copper-ui-verify");
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
    static IEnumerator Review()
    {
        Size(1920,1080);yield return 1.0;
        yield return Capture("01-主菜单");
        Set(app,"menuPage",0);yield return Capture("02-开局配置");
        Set(app,"menuSetupStep",1);yield return Capture("02b-地图配置");
        Set(app,"menuSetupStep",2);yield return Capture("02c-同桌配置");
        Set(app,"menuPage",1);yield return Capture("02d-加载游戏");
        Set(app,"menuPage",4);yield return Capture("03-设置");
        app.StartGame("combined","heading-for-new-shores",4,"P1",true);yield return 3.2;
        yield return Capture("04-双扩展桌面");
        app.OpenTableMenu();yield return Capture("05-暂停菜单");
        Set(app,"tablePausePage",1);yield return Capture("06-桌内设置");
        typeof(M6App).GetMethod("HandleTableEscape",Flags).Invoke(app,null);typeof(M6App).GetMethod("HandleTableEscape",Flags).Invoke(app,null);
        // 八类库存不得侵入骰盘或下方武将区；最后一类必须留在屏内。
        for(int i=0;i<8;i++)
        {
            var r=TabletopPieceDock.GroupLayout(8,i);
            if(r.y<282||r.yMax>704||r.x<0||r.xMax>180)throw new InvalidOperationException("库存侵入骰盘、棋盘或武将区");
        }
        var combined=app.GetComponent<M5App>();
        var hud=(TabletopUI)Get(combined,"tabletop");
        var history=(TabletopHistoryUI)Get(hud,"history");history.Open();
        yield return Capture("07-历史窗口");history.TryClose();
        // 沿规则入口推进到可交易阶段，使用已有自动玩家，不直接构造展示数据。
        int steps=0;
        while(!((OperationSnapshot)typeof(M5App).GetProperty("VerificationOperationSnapshot",Flags).GetValue(combined)).CanTrade&&steps++<600)
        {
            if(!app.IsHumanTurn){app.SetPaused(false);app.StepAi();app.SetPaused(true);}
            else {var command=new Catan.AI.CombinedAi().Decide(((Catan.Core.M5.PlayerView)typeof(M5App).GetProperty("M6View",Flags).GetValue(combined)));command.Id=Guid.NewGuid().ToString("N");combined.Submit(command);}
            yield return .10;
        }
        if(!((OperationSnapshot)typeof(M5App).GetProperty("VerificationOperationSnapshot",Flags).GetValue(combined)).CanTrade)throw new InvalidOperationException("未推进到可交易阶段");
        yield return .7;((OperationDock)Get(combined,"dock")).OpenTrade();
        yield return Capture("08-银行交易");((OperationDock)Get(combined,"dock")).Reset();
        ((OperationDock)Get(combined,"dock")).OpenFacility(0);yield return Capture("09-城市设施");((OperationDock)Get(combined,"dock")).Reset();yield return Capture("12-行动态桌面");
        app.StartGame("cities-knights","heading-for-new-shores",4,"P1",true,threeKingdoms:true);yield return 1.5;
        yield return Capture("10-三国选将");
        Size(2560,1080);yield return Capture("11-宽屏选将");
    }
    [Serializable] sealed class Result{public string 状态,说明;public string[] 截图;}
    static void Finish(Exception error)
    {
        File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(new Result{状态=error==null?"通过":"失败",说明=error==null?"真实GameView截图与库存遮挡边界通过；截图仍需人工审看。":error.ToString(),截图=images.ToArray()},true));
        SessionState.EraseString(Key);if(error!=null)Debug.LogException(error);EditorApplication.Exit(error==null?0:1);
    }
}
