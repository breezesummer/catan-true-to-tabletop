using System;
using System.IO;
using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Linq;
using UnityEngine;

// 显式开发参数；截图读取最终帧，四规则继续使用各自的真实运行组件。
public sealed class MedievalMenuVerification : MonoBehaviour
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    M6App app;string output,saveDigest,stage="启动";int captures,checks;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"--medieval-menu-verify")>=0)
            new GameObject("中世纪菜单验收").AddComponent<MedievalMenuVerification>();
    }
    IEnumerator Start()
    {
        var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"--medieval-menu-verify");
        try
        {
            app=FindFirstObjectByType<M6App>();
            output=Path.GetFullPath(args[flag+1]);
            // 验收只接受项目中 .local 的目录，不使用或覆盖用户存档。
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../../.."));
            string local=Path.GetFullPath(Path.Combine(root,".local"))+Path.DirectorySeparatorChar;
            if(!output.StartsWith(local,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("验收目录必须位于 .local");
            if(app==null||Application.isBatchMode)throw new InvalidOperationException();
            Directory.CreateDirectory(output);saveDigest=Digest(app.SavePath);
            app.MenuReviewSavePath=Path.Combine(output,"private-review-save.json");
        }
        catch(Exception e){Debug.LogError("CATAN_MEDIEVAL_MENU_VERIFY_FAILED: "+e.GetType().Name);Application.Quit(1);yield break;}
        var enumerators=new Stack();enumerators.Push(Review());
        while(enumerators.Count>0)
        {
            object next=null;bool moved=false;Exception error=null;
            try{moved=((IEnumerator)enumerators.Peek()).MoveNext();if(moved)next=((IEnumerator)enumerators.Peek()).Current;}catch(Exception e){error=e;}
            if(error!=null)
            {Debug.LogError("CATAN_MEDIEVAL_MENU_VERIFY_FAILED: "+stage+" "+error.GetType().Name);Application.Quit(1);yield break;}
            if(!moved){enumerators.Pop();continue;}if(next is IEnumerator sub){enumerators.Push(sub);continue;}yield return next;
        }
        File.WriteAllText(Path.Combine(output,"gallery-summary.json"),"{\"status\":\"passed\",\"captures\":"+captures+",\"checks\":"+checks+",\"userSaveTouched\":false,\"manualInput\":false}");
        Debug.Log("CATAN_MEDIEVAL_MENU_VERIFY_OK: captures="+captures+" checks="+checks);Application.Quit(0);
    }
    IEnumerator Review()
    {
        while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
        yield return null;yield return null;
        Require(PastoralMenuScene.ArtworkReady,"背景未打入构建");
        foreach(int number in new[]{2,3,4,5,6,8,9,10,11,12})
            Require(Resources.Load<Texture2D>("TabletopUI/v008/NumberTokens/number-"+number)!=null,"数字贴图未打入构建");
        foreach(string id in new[]{"port-generic","port-wood","port-brick","port-wool","port-wheat","port-ore","catan-victory-marker","number-token-back"})
        {
            string key="EntityAssets/v005/tabletop-markers/"+id;
            var texture=Resources.Load<Texture2D>(key+(id.StartsWith("port-")?"-front":""));
            var prefab=Resources.Load<GameObject>(key);
            Require(texture!=null&&prefab!=null&&prefab.GetComponentsInChildren<Renderer>(true)
                .SelectMany(r=>r.sharedMaterials).Any(m=>m!=null&&m.mainTexture==texture),"实体标记贴图引用丢失");
        }
        foreach(var size in new[]{new Vector2Int(1440,900),new Vector2Int(1280,720),new Vector2Int(1920,1080)})
        {
            Screen.SetResolution(size.x,size.y,false);yield return new WaitForSecondsRealtime(.5f);
            Go(-1);yield return Capture("home-"+size.x+"x"+size.y);
            Require(Screen.width==size.x&&Screen.height==size.y,"分辨率未生效");
        }
        Screen.SetResolution(1440,900,false);yield return new WaitForSecondsRealtime(.5f);
        Go(0);Set("menuSetupStep",0);yield return Capture("new-game-rules");
        Set("menuRule",1);Set("menuSetupStep",1);yield return Capture("new-game-maps");
        Set("menuSetupStep",2);yield return Capture("new-game-table");
        Go(1);yield return Capture("load-empty");
        Go(4);Set("menuSettingsTab",0);yield return Capture("settings-sound");
        Set("menuSettingsTab",1);yield return Capture("settings-appearance");
        Go(2);yield return Capture("lan-connect");
        Set("networkTab",1);yield return Capture("lan-join");
        Set("networkTab",2);
        for(int part=0;part<3;part++){Set("networkSettingsPage",part);yield return Capture("lan-create-"+part);}
        string address=(string)typeof(M6App).GetField("networkAddress",Flags).GetValue(app);
        Go(3);Require(string.IsNullOrEmpty((string)typeof(M6App).GetField("networkAddress",Flags).GetValue(app)),"在线入口继承了局域网地址");
        yield return Capture("online-connect");
        Go(2);Require((string)typeof(M6App).GetField("networkAddress",Flags).GetValue(app)==address,"局域网地址未保留");
        var modes=new[]{"base","seafarers","cities-knights","combined"};
        for(int i=0;i<4;i++)
        {
            stage=modes[i]+"-菜单隔离";
            app.StartGame(modes[i],i==3?"through-the-desert":"heading-for-new-shores",i%2==0?3:4,"P1",true);
            for(int wait=0;!app.Ready&&wait<240;wait++)yield return null;
            Require(app.Ready,"开局未就绪");string authority=app.ExportEnvelope();
            var tokens=FindObjectsByType<TabletopNumberTokens>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            Require(tokens.Length==1,"真实棋盘缺少数字组件");
            var numbers=tokens[0].GetComponentsInChildren<MeshRenderer>(true);
            Require(numbers.Length>0&&numbers.All(r=>r.sharedMaterials[0].mainTexture!=null&&r.sharedMaterials[2].mainTexture!=null),"真实棋盘数字正反面贴图缺失");
            yield return Capture("board-"+modes[i]);
            app.ShowMenu();
            foreach(int page in new[]{0,1,2,3,4,-1})
            {Go(page);yield return null;Require(app.ExportEnvelope()==authority&&!app.StepAi(),"导航改变权威局面");}
            app.WriteSave(app.MenuReviewSavePath);
            app.ReadSave(app.MenuReviewSavePath);
            for(int wait=0;!app.Ready&&wait<240;wait++)yield return null;
            Require(app.ExportEnvelope()==authority,"独立存档恢复改变局面");
            app.ShowMenu();
        }
        Go(1);yield return Capture("load-with-isolated-save");
        string broken=Path.Combine(output,"private-corrupt.json");File.WriteAllText(broken,"{}");
        string before=app.ExportEnvelope();bool rejected=false;
        try{app.ReadSave(broken);}catch{rejected=true;}
        Require(rejected&&app.ExportEnvelope()==before,"损坏存档破坏当前局面");
        Require(Digest(app.SavePath)==saveDigest,"默认存档被修改");
    }
    void Go(int page)=>typeof(M6App).GetMethod("NavigateMainMenu",Flags).Invoke(app,new object[]{page});
    void Set(string name,object value)=>typeof(M6App).GetField(name,Flags).SetValue(app,value);
    void Require(bool condition,string reason){checks++;if(!condition)throw new InvalidOperationException(reason);}
    IEnumerator Capture(string name)
    {
        stage=name;yield return new WaitForSecondsRealtime(.4f);
        // 中文动态字体在换页时可能重建图集；确保至少三次完整重绘后记录。
        for(int frame=0;frame<3;frame++)yield return new WaitForEndOfFrame();
        var texture=ScreenCapture.CaptureScreenshotAsTexture();
        Require(texture!=null&&texture.width==Screen.width,"截图不完整");
        File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());Destroy(texture);captures++;
    }
    static string Digest(string path)
    {if(!File.Exists(path))return "missing";using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));}
}
