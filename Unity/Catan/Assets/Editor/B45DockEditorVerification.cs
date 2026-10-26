using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 需要真实图形界面；不使用-nographics，不以离屏静态代码绘图代替产品组件。
[InitializeOnLoad]
public static class B45DockEditorVerification
{
    const string Key="Catan.B45DockVerification.Output",Deadline="Catan.B45DockVerification.Deadline";
    static readonly BindingFlags Instance=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static B45DockEditorVerification(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"--b45-dock-verify");
        if(flag<0||flag+1>=args.Length)throw new InvalidOperationException("缺少--b45-dock-verify及输出目录");
        string output=Path.GetFullPath(args[flag+1]);string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../..",".local"))+Path.DirectorySeparatorChar;
        if(!output.StartsWith(root,StringComparison.Ordinal))throw new InvalidOperationException("验收输出必须位于项目.local内");
        Directory.CreateDirectory(output);string result=Path.Combine(output,"result.json");if(File.Exists(result))File.Delete(result);
        SessionState.SetString(Key,output);SessionState.SetString(Deadline,DateTime.UtcNow.AddMinutes(8).ToString("O"));
        EditorSceneManager.OpenScene("Assets/M6.unity",OpenSceneMode.Single);GameView().Focus();EditorApplication.isPlaying=true;
    }
    static EditorWindow GameView()=>EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
    static void SetSize(int width,int height)
    {
        var assembly=typeof(EditorWindow).Assembly;var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
        var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);var sizes=singleton.GetProperty("instance",BindingFlags.Public|BindingFlags.Static).GetValue(null);
        var groupType=assembly.GetType("UnityEditor.GameViewSizeGroupType");var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new[]{Enum.Parse(groupType,"Standalone")});
        var sizeType=assembly.GetType("UnityEditor.GameViewSize");var kindType=assembly.GetType("UnityEditor.GameViewSizeType");
        var size=Activator.CreateInstance(sizeType,Instance,null,new object[]{Enum.Parse(kindType,"FixedResolution"),width,height,"B45-"+width+"x"+height},null);
        group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});int index=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null)-1;
        var window=GameView();window.GetType().GetProperty("selectedSizeIndex",Instance).SetValue(window,index);window.Focus();window.Repaint();
    }
    static object ReadMember(object owner,string name)
    {
        for(var type=owner.GetType();type!=null;type=type.BaseType)
        {
            var property=type.GetProperty(name,Instance|BindingFlags.DeclaredOnly);if(property!=null)return property.GetValue(owner);
            var field=type.GetField(name,Instance|BindingFlags.DeclaredOnly);if(field!=null)return field.GetValue(owner);
        }
        return null;
    }
    static void Send(Event e)
    {
        var window=GameView();window.Focus();
        // 与 GameView.OnGUI 相同的原生队列；输入已是游戏像素，不经过编辑器工具栏和缩放。
        var queue=typeof(EditorGUIUtility).GetMethod("QueueGameViewInputEvent",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);
        if(queue==null)throw new InvalidOperationException("编辑器缺少游戏输入队列");
        Debug.Log("B45_EDITOR_POINTER: "+e.type+" target="+e.mousePosition+" via=QueueGameViewInputEvent");
        var pending=new Event(e);
        EditorApplication.delayCall+=()=>{queue.Invoke(null,new object[]{pending});EditorApplication.QueuePlayerLoopUpdate();window.Repaint();};
    }
    static void Tick()
    {
        string output=SessionState.GetString(Key,"");if(output.Length==0)return;
        if(EditorApplication.isPlaying)
        {
            B45DockVerification.ConfigureViewport=SetSize;
            B45DockVerification.SendPointer=Send;
            var app=UnityEngine.Object.FindAnyObjectByType<M6App>();if(app!=null&&app.GetComponent<B45DockVerification>()==null)app.gameObject.AddComponent<B45DockVerification>();
        }
        string result=Path.Combine(output,"result.json");
        if(File.Exists(result)){Finish(File.ReadAllText(result).Contains("\"通过\"")?0:1);return;}
        if(DateTime.TryParse(SessionState.GetString(Deadline,""),out var deadline)&&DateTime.UtcNow>deadline){Debug.LogError("CATAN_B45_DOCK_TIMEOUT");Finish(1);}
    }
    static void Finish(int code){SessionState.EraseString(Key);SessionState.EraseString(Deadline);EditorApplication.Exit(code);}
}
