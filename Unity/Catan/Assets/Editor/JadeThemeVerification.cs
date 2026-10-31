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
    static string[] originalNames;
    static readonly BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
    static JadeThemeVerification(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        original=StrategyHudTheme.Selected;SessionState.SetInt(Key+".original",original);
        SessionState.SetString(Key,Path.GetFullPath(Path.Combine(Application.dataPath,"../../../.local/jade-theme")));
        EditorSceneManager.OpenScene("Assets/M6.unity",OpenSceneMode.Single);
        typeof(B45DockEditorVerification).GetMethod("SetSize",Flags).Invoke(null,new object[]{1440,900});
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
                originalNames=(string[])typeof(M6App).GetField("localPlayerNames",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(app);
                typeof(M6App).GetField("localPlayerNames",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(app,new[]{"玩家","Martire · Azure","羽扇纶巾卧龙先生天下三分运筹帷幄之中天下","奥古斯都"});
                authority=app.ExportEnvelope();
                for(int i=0;i<StrategyHudTheme.Names.Length;i++)
                {
                    StrategyHudTheme.Select(i);
                    if(StrategyHudTheme.Selected!=i||app.ExportEnvelope()!=authority)throw new Exception("主题切换影响权威状态或选择失败");
                }
                if(!JadeTabletopSkin.Ready)throw new Exception("玉石图集缺失");
                StrategyHudTheme.Select(3);stage++;next=EditorApplication.timeSinceStartup+3;return;
            }
            // 截图和后续输入分隔至不同帧，避免异步截图记录到下一个状态。
            switch(stage)
            {
                case 2:
                    CheckText("羽扇纶巾卧龙先生天下三分运筹帷幄之中天下",92,27,12,true);
                    CheckText("Martire · Azure",92,27,12,true);
                    CheckText("999/999",51,17,14,false);
                    CheckText("整理手牌",57,22,11,false);
                    CheckText("999",34,26,23,false);
                    Send(EventType.MouseMove,new Vector2(600,600));Advance();return;
                case 3: Capture(output,"青玉鎏金-实机.png");Advance();return;
                case 4: Send(EventType.MouseMove,new Vector2(1226,846));Send(EventType.MouseDown,new Vector2(1226,846));Advance();return;
                case 5: Capture(output,"整理手牌-按下.png");Advance();return;
                case 6: Send(EventType.MouseUp,new Vector2(1226,846));Advance();return;
                case 7: Capture(output,"整理手牌-释放.png");Advance();return;
                case 8: Size(1280,720);Advance(2);return;
                case 9: Capture(output,"青玉鎏金-1280x720.png");Advance();return;
                case 10:
                    if(app.ExportEnvelope()!=authority)throw new Exception("主题或整理手牌改变权威存档");
                    typeof(M6App).GetField("localPlayerNames",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(app,originalNames);
                    authority=app.ExportEnvelope();Size(1672,941);Advance(2);return;
                case 11: Capture(output,"青玉鎏金-1672x941.png");Advance();return;
                case 12: Size(1440,900);Advance(2);return;
                case 13: Send(EventType.MouseMove,new Vector2(56,60));Send(EventType.MouseDown,new Vector2(56,60));Advance();return;
                case 14: Capture(output,"圆钮-按下.png");Advance();return;
                case 15: Send(EventType.MouseDrag,new Vector2(500,250));Advance();return;
                case 16: Capture(output,"圆钮-移出取消.png");Advance();return;
                case 17: Send(EventType.MouseUp,new Vector2(500,250));Advance();return;
            }
            if(app.ExportEnvelope()!=authority)throw new Exception("界面点击改变权威存档");
            File.WriteAllText(Path.Combine(output,"验证.txt"),"通过：Unity "+Application.unityVersion+"；四主题即时切换，权威存档保持一致，玉石图集加载成功；1440×900、1280×720及1672×941实际GameView截图；长中文名、英文名、按下和释放截图。\n");
            Finish(0);
        }
        catch(Exception ex){File.WriteAllText(Path.Combine(output,"失败.txt"),ex.ToString());Debug.LogException(ex);Finish(1);}
    }
    static void Advance(double seconds=1){stage++;next=EditorApplication.timeSinceStartup+seconds;}
    static void Capture(string output,string name)=>ScreenCapture.CaptureScreenshot(Path.Combine(output,name));
    static void Size(int width,int height)=>typeof(B45DockEditorVerification).GetMethod("SetSize",Flags).Invoke(null,new object[]{width,height});
    static void Send(EventType type,Vector2 point)
    {typeof(B45DockEditorVerification).GetMethod("Send",Flags).Invoke(null,new object[]{new Event{type=type,button=0,mousePosition=point}});}
    static void CheckText(string text,float width,float height,int size,bool wrap)
    {
        var style=new GUIStyle(ModernOperationUI.Center){fontSize=size};var r=new Rect(0,0,width,height);
        TabletopTextFit.Fit(style,text,r,8,wrap);var content=new GUIContent(text);
        if(style.CalcHeight(content,width)>height+.1f||!wrap&&style.CalcSize(content).x>width+.1f)throw new Exception("文字超出内容区："+text);
    }
    static void Finish(int code)
    {
        StrategyHudTheme.Select(SessionState.GetInt(Key+".original",0));SessionState.EraseString(Key);
        EditorApplication.isPlaying=false;EditorApplication.delayCall+=()=>EditorApplication.Exit(code);
    }
}
