using System;
using System.IO;
using System.Linq;
using Catan.Core.M2;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 显式运行的真实GameView主题验证；结束恢复外观偏好，不写用户存档。
[InitializeOnLoad]
public static class JadeThemeVerification
{
    const string Key="Catan.JadeVerification";
    static int stage,original;
    static double next;
    static string authority;
    static readonly BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
    static JadeThemeVerification(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        original=StrategyHudTheme.Selected;SessionState.SetInt(Key+".original",original);
        SessionState.SetString(Key,Path.GetFullPath(Path.Combine(Application.dataPath,"../../../.local/jade-theme")));
        EditorSceneManager.OpenScene("Assets/M6.unity",OpenSceneMode.Single);
        typeof(B45DockEditorVerification).GetMethod("SetSize",Flags).Invoke(null,new object[]{1672,941});
        EditorApplication.isPlaying=true;
    }
    static void Tick()
    {
        string output=SessionState.GetString(Key,"");if(output.Length==0||!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
        try
        {
            Directory.CreateDirectory(output);var app=UnityEngine.Object.FindAnyObjectByType<M6App>();if(app==null)return;
            if(stage==0){app.StartGame("base","heading-for-new-shores",4,"P1",true);stage++;next=EditorApplication.timeSinceStartup+4;return;}
            if(stage==1)
            {
                var basic=app.GetComponent<M2App>();var host=(M2LocalGameHost)typeof(M2App).GetProperty("VerificationHost",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(basic);
                host.NewGame(4,20261031);
                for(int i=0;i<20;i++)
                {
                    var view=host.View("P1");if(view.Phase!=GamePhase.SetupSettlement&&view.Phase!=GamePhase.SetupRoad)break;
                    view=host.View(view.ActivePlayerId);bool village=view.Phase==GamePhase.SetupSettlement;
                    var result=host.Submit(new Command{Id=Guid.NewGuid().ToString("N"),PlayerId=view.ActivePlayerId,Kind=village?CommandKind.SetupSettlement:CommandKind.SetupRoad,TargetId=(village?view.LegalVertexIds:view.LegalEdgeIds).First()});
                    if(!result.Success)throw new Exception("验证开局失败："+result.ErrorCode);
                }
                typeof(M2App).GetMethod("M6Refresh",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(basic,null);
                authority=app.ExportEnvelope();
                for(int i=0;i<StrategyHudTheme.Names.Length;i++)
                {
                    StrategyHudTheme.Select(i);
                    if(StrategyHudTheme.Selected!=i||app.ExportEnvelope()!=authority)throw new Exception("主题切换影响权威状态或选择失败");
                }
                if(!JadeTabletopSkin.Ready)throw new Exception("玉石图集缺失");
                StrategyHudTheme.Select(3);stage++;next=EditorApplication.timeSinceStartup+3;return;
            }
            if(stage==2)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(output,"青玉鎏金-实机.png"));
                typeof(B45DockEditorVerification).GetMethod("Send",Flags).Invoke(null,new object[]{new Event{type=EventType.MouseDown,button=0,mousePosition=new Vector2(1420,875)}});
                stage++;next=EditorApplication.timeSinceStartup+1;return;
            }
            if(stage==3)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(output,"整理手牌-按下.png"));
                typeof(B45DockEditorVerification).GetMethod("Send",Flags).Invoke(null,new object[]{new Event{type=EventType.MouseUp,button=0,mousePosition=new Vector2(1420,875)}});
                stage++;next=EditorApplication.timeSinceStartup+1;return;
            }
            if(app.ExportEnvelope()!=authority)throw new Exception("界面点击改变权威存档");
            File.WriteAllText(Path.Combine(output,"验证.txt"),"通过：Unity "+Application.unityVersion+"；四主题即时切换，权威存档保持一致，玉石图集加载成功；1672×941实际GameView截图。\n");
            Finish(0);
        }
        catch(Exception ex){File.WriteAllText(Path.Combine(output,"失败.txt"),ex.ToString());Debug.LogException(ex);Finish(1);}
    }
    static void Finish(int code)
    {
        StrategyHudTheme.Select(SessionState.GetInt(Key+".original",0));SessionState.EraseString(Key);
        EditorApplication.isPlaying=false;EditorApplication.delayCall+=()=>EditorApplication.Exit(code);
    }
}
