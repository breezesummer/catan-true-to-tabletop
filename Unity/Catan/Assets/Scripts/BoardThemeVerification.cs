using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Catan.AI;
using Catan.Network.Contracts;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 只在显式命令行验收时安装；报告不含权威存档、手牌或隐藏地图。
public sealed class BoardThemeVerification : MonoBehaviour
{
    [Serializable] sealed class Sample
    {
        public int cycle,objects,materials,meshes,textures,audio,cameras,templates;
        public long allocated;
        public string theme;
    }
    [Serializable] sealed class Report
    {
        public string version=M6App.BuildVersion,result,mode;
        public int configurations,switches,commands,turn,pendingCases;
        public long revision;
        public long observedSwitchResidentBytes;
        public bool authorityUnchanged,cameraPreserved,rollback,persistence,reconnected;
        public List<Sample> samples=new List<Sample>();
    }
    M6App app;
    string output,fixtures,role,priorPreference;
    bool hadPreference;
    Report report=new Report();
    float deadline;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        var args=Environment.GetCommandLineArgs();
        if(args.Contains("--ultra-theme-verify"))new GameObject("U1 显式验收").AddComponent<BoardThemeVerification>();
    }
    static string Arg(string name,string fallback="")
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:fallback;
    }
    void Start()
    {
        output=Path.GetFullPath(Arg("--ultra-theme-verify"));Directory.CreateDirectory(output);
        fixtures=Arg("--ultra-theme-fixtures");role=Arg("--ultra-theme-role");
        hadPreference=PlayerPrefs.HasKey(BoardThemeController.PreferenceKey);priorPreference=PlayerPrefs.GetString(BoardThemeController.PreferenceKey);
        report.mode=role.Length==0?"本机四规则包及生命周期":"本机双进程联机 "+role;
        deadline=Time.realtimeSinceStartup+360;
        StartCoroutine(Guard(role.Length==0?Local():Network()));
    }
    IEnumerator Guard(IEnumerator work)
    {
        var stack=new Stack<IEnumerator>();stack.Push(work);
        while(stack.Count>0)
        {
            object current=null;bool more=false;
            try {Require(Time.realtimeSinceStartup<deadline,"验收超时");more=stack.Peek().MoveNext();if(more)current=stack.Peek().Current;}
            catch(Exception ex){Debug.LogError("CATAN_U1_FAILED: "+ex.Message+"\n"+ex.StackTrace);Finish(false);yield break;}
            if(!more){stack.Pop();continue;}
            if(current is IEnumerator nested){stack.Push(nested);continue;}
            yield return current;
        }
        Finish(true);
    }
    void Finish(bool ok)
    {
        if(hadPreference)PlayerPrefs.SetString(BoardThemeController.PreferenceKey,priorPreference);else PlayerPrefs.DeleteKey(BoardThemeController.PreferenceKey);
        PlayerPrefs.Save();report.result=ok?"通过":"失败";
        File.WriteAllText(Path.Combine(output,(role.Length>0?role:"local")+"-report.json"),JsonUtility.ToJson(report,true));
        Debug.Log(ok?"CATAN_U1_OK":"CATAN_U1_FAILED");Application.Quit(ok?0:1);
    }
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    IEnumerator Frames(int count=5){for(int i=0;i<count;i++)yield return null;}
    IEnumerator Switch(string id)
    {
        app.RequestBoardTheme(id);yield return Frames();
        Require(BoardThemeController.Current.Id==id&&!BoardThemeController.Switching,"主题没有完成切换："+id);
        Require(PlayerPrefs.GetString(BoardThemeController.PreferenceKey)==id,"本地偏好没有持久化");report.persistence=true;report.switches++;
        report.observedSwitchResidentBytes=Math.Max(report.observedSwitchResidentBytes,app.ThemeSwitchResidentBytes);
    }
    IEnumerator Local()
    {
        yield return Frames();app=FindFirstObjectByType<M6App>();Require(app!=null,"缺少游戏入口");
        // 先恢复没有主题字段的历史信封，随后遍历实际支持的 26 个规则／人数配置。
        foreach(string fixture in new[]{"ordinary","maximum","crowded","fog"})
        {
            app.ReadSave(Path.Combine(fixtures,fixture+".json"));yield return Frames();
            yield return CompareSwitch();
        }
        string pendingDirectory=Arg("--ultra-theme-pending");
        if(pendingDirectory.Length>0)foreach(string pending in Directory.GetFiles(pendingDirectory,"pending-*.json").Where(p=>!p.EndsWith("manifest.json")))
        {
            app.ReadSave(pending);yield return Frames();yield return CompareSwitch();yield return CompareSwitch();
            if(app.RuleSet=="base")
            {
                var basic=app.GetComponent<M2App>();var command=new BaseGameAi().Decide(basic.M6View);command.Id=Guid.NewGuid().ToString("N");
                Require(basic.Submit(command)?.Success==true,"切换后基础版待决指令被拒绝："+Path.GetFileName(pending));
            }
            else
            {
                var combined=app.GetComponent<M5App>();var command=new CombinedAi().Decide(combined.M6View);command.Id=Guid.NewGuid().ToString("N");
                Require(combined.Submit(command)?.Success==true,"切换后组合待决指令被拒绝："+Path.GetFileName(pending));
            }
            report.pendingCases++;yield return Frames();
        }
        foreach(string rules in new[]{"base","seafarers","cities-knights","combined"})
        foreach(int count in new[]{3,4})
        foreach(string scenario in rules=="seafarers"?Catan.Core.M3.SeafarersScenarios.Ids:rules=="combined"?Catan.Core.M5.CombinedScenarios.Ids:new[]{"heading-for-new-shores"})
        {
            app.StartGame(rules,scenario,count,"P1",true);yield return Frames();
            Require(app.Ready,"新局未就绪");yield return CompareSwitch();report.configurations++;
        }
        app.ReadSave(Path.Combine(fixtures,"crowded.json"));yield return Frames();
        yield return Switch("tabletop");yield return Switch("ultra");yield return Switch("tabletop");
        string authority=app.ExportEnvelope();
        for(int i=0;i<22;i++)
        {
            yield return Switch(i%2==0?"ultra":"tabletop");
            Require(authority==app.ExportEnvelope(),"连续切换修改了权威状态");
            Require(FindObjectsByType<TabletopScene>(FindObjectsSortMode.None).Length==1,"残留重复桌面");
            Require(app.VerificationBuildFlightCount==0&&app.VerificationBuildHiddenRenderers==0,"切换重播建造或遗留隐藏棋子");
            GC.Collect();yield return Frames(2);report.samples.Add(Census(i));
            if(i<2)Capture(Path.Combine(output,i==0?"ultra-board.png":"tabletop-board.png"));
        }
        var stable=report.samples.Where(s=>s.cycle>=4&&s.theme=="tabletop").ToArray();
        Require(stable.Select(s=>s.objects).Max()-stable.Select(s=>s.objects).Min()<=2,"场景对象持续增长");
        Require(stable.Select(s=>s.materials).Max()-stable.Select(s=>s.materials).Min()<=2,"材质持续增长");
        Require(stable.Select(s=>s.meshes).Max()-stable.Select(s=>s.meshes).Min()<=2,"网格持续增长");
        Require(stable.Select(s=>s.audio).Distinct().Count()==1&&stable.Select(s=>s.cameras).Distinct().Count()==1,"音源或相机持续增长");
        Require(stable.Last().allocated-stable.First().allocated<8*1024*1024,"稳定区间内存增长超过 8 MiB");
        app.VerificationFailThemeLoad=true;app.RequestBoardTheme("ultra");yield return Frames();
        Require(BoardThemeController.Current.Id=="tabletop"&&authority==app.ExportEnvelope()&&PlayerPrefs.GetString(BoardThemeController.PreferenceKey)=="tabletop","加载失败未完整回退");report.rollback=true;
    }
    IEnumerator CompareSwitch()
    {
        var table=FindFirstObjectByType<TabletopScene>();Require(table!=null,"棋盘缺少桌面");
        var pose=table.CaptureView();pose.Pan=new Vector3(.013f,0,-.017f);table.RestoreView(pose);
        string authority=app.ExportEnvelope();
        yield return Switch(BoardThemeController.IsUltra?"tabletop":"ultra");
        table=FindFirstObjectByType<TabletopScene>();var after=table.CaptureView();
        Require(authority==app.ExportEnvelope(),"切换改变存档、随机状态或待决选择");report.authorityUnchanged=true;
        Require((pose.Pan-after.Pan).sqrMagnitude<.00000001f&&pose.Top==after.Top&&Mathf.Abs(pose.Zoom-after.Zoom)<.0001f,"相机未恢复");report.cameraPreserved=true;
        Require(table.Objects!=null&&table.HudCamera!=null,"缺少操作模型或相机");
        var stamps=table.transform.parent.GetComponentsInChildren<BoardThemeStamp>(true);
        Require(BoardThemeController.IsUltra?stamps.Length>0:stamps.Length==0,"主题对象混用");
        Require(!BoardThemeController.IsUltra||table.transform.parent.GetComponentsInChildren<TabletopPhysicalTileV005>().Length==0,"ultra 被旧厚板处理");
    }
    static Sample Census(int cycle)=>new Sample{cycle=cycle,theme=BoardThemeController.Current.Id,
        objects=FindObjectsByType<Transform>(FindObjectsSortMode.None).Length,materials=Resources.FindObjectsOfTypeAll<Material>().Length,
        meshes=Resources.FindObjectsOfTypeAll<Mesh>().Length,textures=Resources.FindObjectsOfTypeAll<Texture2D>().Length,
        audio=FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length,cameras=FindObjectsByType<Camera>(FindObjectsSortMode.None).Length,
        allocated=Profiler.GetTotalAllocatedMemoryLong(),templates=BoardThemeAssets.CachedModels};
    static void Capture(string path)
    {
        var camera=FindFirstObjectByType<TabletopScene>().TableCamera;var previous=RenderTexture.active;
        var target=new RenderTexture(1440,900,24);var image=new Texture2D(1440,900,TextureFormat.RGB24,false);
        try{target.Create();RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1440,900),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
        finally{RenderTexture.active=previous;target.Release();Destroy(target);Destroy(image);}
    }
    IEnumerator Network()
    {
        yield return Frames();app=FindFirstObjectByType<M6App>();var client=app.Network;
        string theme=role=="host"?"tabletop":"ultra";
        // 即使默认选择相同，也显式保存本测试客户端的偏好。
        BoardThemeController.Select(BoardThemeDefinition.Find(theme));BoardThemeController.Persist();
        client.EnableVerification();client.UseCredentialDirectory(Path.Combine(output,"private-"+role));
        client.ConfigureEndpoint(Arg("--ultra-theme-endpoint","http://127.0.0.1:47881"),null);
        string rendezvous=Path.Combine(output,"room-id.txt");
        if(role=="host")
        {
            client.Create(new RoomSettings{Name="U1双主题验证",Listed=false,RuleSet="base",PlayerCount=3,AiSeats=new[]{"P3"}},"主题验证一",null);
            while(client.Room==null||client.Busy)yield return null;
            File.WriteAllText(rendezvous+".writing",client.RoomId);File.Move(rendezvous+".writing",rendezvous);client.Ready(true);
            while(client.Busy||!client.Room.Seats.All(s=>s.IsAi||s.Occupied&&s.Ready))yield return null;
            client.StartRoom();
        }
        else
        {
            while(!File.Exists(rendezvous))yield return null;
            client.Join(File.ReadAllText(rendezvous),"主题验证二",null);
            while(client.Room==null||client.Busy)yield return null;client.Ready(true);
        }
        while(!app.Ready||!app.NetworkGame)yield return null;
        while(true)
        {
            var view=client.View<Catan.Core.M2.PlayerView>();
            Require(app.GetComponent<M2App>().VerificationHost==null,"联机客户端创建了权威会话");
            Require(BoardThemeController.Current.Id==theme,"另一个客户端改变了本机主题");
            if(view.Turn>=3){report.turn=view.Turn;report.revision=client.Revision;break;}
            if(!report.reconnected&&view.Turn>=1&&!client.Busy&&!client.CommandPending)
            {
                string seat=client.SeatId;client.Reconnect();while(client.Busy||!client.Connected)yield return null;
                Require(client.SeatId==seat,"重连丢失席位");report.reconnected=true;
                yield return Switch(theme=="ultra"?"tabletop":"ultra");yield return Switch(theme);continue;
            }
            if(app.IsHumanTurn&&client.CanInteract)
            {
                var command=new BaseGameAi().Decide(app.GetComponent<M2App>().M6View);
                if(!client.Preview(command).Success){yield return null;continue;}
                long revision=client.Revision;app.GetComponent<M2App>().Submit(command);report.commands++;
                while(client.CommandPending||client.Revision<=revision)yield return null;
            }
            yield return null;
        }
        string done=Path.Combine(output,role+"-done.txt");File.WriteAllText(done+".writing",theme);File.Move(done+".writing",done);
        while(!File.Exists(Path.Combine(output,(role=="host"?"guest":"host")+"-done.txt")))yield return null;
        Require(File.ReadAllText(Path.Combine(output,"host-done.txt"))!=File.ReadAllText(Path.Combine(output,"guest-done.txt")),"两个客户端没有保持不同主题");
    }
}
