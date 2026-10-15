using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

// 仅显式开发参数启用。截图来自本游戏帧缓冲，存档只存在于指定的 .local 验收目录。
public sealed class MenuAudioVerification : MonoBehaviour
{
    const string Flag="--menu-audio-verify";
    const BindingFlags PrivateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
    M6App app;
    string output,stage="启动";
    int checks;
    readonly List<CaptureRecord> captures=new List<CaptureRecord>();
    readonly List<string> audioNotes=new List<string>();
    int animatedPixelChanges;

    public void Initialize(M6App owner){app=owner;}

    [Serializable] sealed class CaptureRecord
    {
        public string file,scene;
        public int width,height;
        public double elapsed;
    }
    [Serializable] sealed class Result
    {
        public string status,unity,generatedUtc,stage,errorType;
        public int checks,animatedPixelChanges;
        public bool userSaveTouched,fullUiPixelsCaptured,humanVisualReview,humanAudioListening;
        public CaptureRecord[] captures;
        public string[] audioChecks,unverified;
    }

    [Serializable] sealed class Observation
    {
        public string utc,ruleSet,humanSeat;
        public bool menuVisible,tableMenuOpen,ready,muted;
        public int playedCues,activeVoices,width,height;
        public long historyCursor;
        public float master,effects,ambient,mixerPeak;
    }

    IEnumerator Start()
    {
        var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,Flag);
        if(index<0)
        {
            int watch=Array.IndexOf(args,"--menu-audio-observe");
            if(watch<0)yield break;
            try
            {
                if(watch+1>=args.Length)throw new InvalidDataException("缺少观察目录。");
                output=ReviewDirectory(args[watch+1]);Directory.CreateDirectory(output);
            }
            catch(Exception exception){Debug.LogError("CATAN_MENU_AUDIO_OBSERVE_REFUSED: "+exception.GetType().Name);yield break;}
            // 被动记录公开展示状态；实际输入由鼠标完成，不自动开局、保存或修改音量。
            var samples=new float[1024];
            while(true)
            {
                yield return new WaitForSecondsRealtime(.25f);
                var audio=TabletopAudio.Instance;if(app==null||audio==null)continue;
                AudioListener.GetOutputData(samples,0);float peak=0;
                for(int i=0;i<samples.Length;i++)peak=Mathf.Max(peak,Mathf.Abs(samples[i]));
                File.WriteAllText(Path.Combine(output,"observation.json"),JsonUtility.ToJson(new Observation{
                    utc=DateTime.UtcNow.ToString("o"),ruleSet=app.RuleSet,humanSeat=app.HumanSeat,
                    menuVisible=app.MenuVisible,tableMenuOpen=app.TableMenuOpen,ready=app.Ready,muted=audio.Muted,
                    playedCues=audio.PlayedCueCount,activeVoices=audio.ActiveVoices,historyCursor=audio.HistoryCursor,
                    master=audio.MasterVolume,effects=audio.EffectsVolume,ambient=audio.AmbientVolume,mixerPeak=peak,
                    width=Screen.width,height=Screen.height
                },true));
            }
        }
        try
        {
            if(index+1>=args.Length)throw new InvalidDataException("缺少验收目录。");
            output=ReviewDirectory(args[index+1]);Directory.CreateDirectory(output);
        }
        catch(Exception exception)
        {Debug.LogError("CATAN_MENU_AUDIO_VERIFY_REFUSED: "+exception.GetType().Name);Application.Quit(1);yield break;}
        if(Application.isBatchMode||SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)
        {
            WriteResult("failed","GraphicsUnavailable");
            Debug.LogError("CATAN_MENU_AUDIO_VERIFY_FAILED: 需要实际图形窗口，禁止 batchmode / nographics。");
            Application.Quit(1);yield break;
        }
        app=GetComponent<M6App>();
        var stack=new Stack<IEnumerator>();stack.Push(Verify());
        while(stack.Count>0)
        {
            object next=null;bool moved=false;Exception failure=null;
            try{moved=stack.Peek().MoveNext();if(moved)next=stack.Peek().Current;}
            catch(Exception exception){failure=exception;}
            if(failure!=null)
            {
                WriteResult("failed",failure.GetType().Name);
                Debug.LogError("CATAN_MENU_AUDIO_VERIFY_FAILED: stage="+stage+"; type="+failure.GetType().Name);
                Application.Quit(1);yield break;
            }
            if(!moved){stack.Pop();continue;}
            if(next is IEnumerator nested){stack.Push(nested);continue;}
            yield return next;
        }
        WriteResult("passed",null);
        Debug.Log("CATAN_MENU_AUDIO_VERIFY_OK: checks="+checks+"; captures="+captures.Count+"; animatedPixels="+animatedPixelChanges);
        Application.Quit(0);
    }

    IEnumerator Verify()
    {
        yield return null;yield return null;
        Require(app!=null&&app.MenuVisible&&!app.Ready,"启动菜单不符合预期");
        var vectorShader=Resources.Load<Shader>("PastoralMenu/PastoralMenuVector");
        Require(vectorShader!=null&&vectorShader.isSupported,"田园背景矢量着色器缺失或不受支持");
        yield return SetResolution(1440,900);
        stage="启动菜单与动态背景";
        yield return Capture("01-menu-1440x900.png","首次启动菜单");
        yield return new WaitForSecondsRealtime(1.1f);
        yield return Capture("02-menu-1440x900-animated.png","同景 1 秒后");
        animatedPixelChanges=ChangedPixels(Path.Combine(output,captures[0].file),Path.Combine(output,captures[1].file));
        Require(animatedPixelChanges>100,"背景两帧没有足够可见变化");
        stage="菜单屏幕比例";
        yield return SetResolution(1280,720);
        yield return Capture("03-menu-1280x720.png","16:9 较小窗口");
        yield return SetResolution(1920,1080);
        yield return Capture("04-menu-1920x1080.png","16:9 高清窗口");
        var phone=MobileViewport.Fit(2400,1080,72,0,2256,1080);
        Require(phone.Left==72&&phone.Width==2256&&phone.Top==0&&phone.Height==1080,"手机安全区逻辑视口错误");
        Require(Math.Abs(phone.ToLogicalX(phone.ToScreenX(1440))-1440)<.01f&&Math.Abs(phone.ToLogicalY(phone.ToScreenY(900))-900)<.01f,"手机逻辑坐标往返错误");
        ExpectConfigurationRejected("base","heading-for-new-shores",2,"P1");
        ExpectConfigurationRejected("base","heading-for-new-shores",5,"P1");
        ExpectConfigurationRejected("base","heading-for-new-shores",3,"P4");
        ExpectConfigurationRejected("base","heading-for-new-shores",4,"P0");
        ExpectPointsRejected("base","heading-for-new-shores",3,"P1",4);
        ExpectPointsRejected("base","heading-for-new-shores",3,"P1",21);
        ExpectPointsRejected("seafarers","pirate-islands",4,"P1",14);
        Require(!app.Ready&&app.MenuVisible,"非法配置改变启动菜单");
        yield return SetResolution(1440,900);
        var modes=new[]{"base","seafarers","cities-knights","combined"};
        var scenarios=new[]{"heading-for-new-shores","new-world","heading-for-new-shores","through-the-desert"};
        for(int i=0;i<modes.Length;i++)
        {
            stage=modes[i]+"-菜单与局面隔离";
            int players=i%2==0?3:4;string seat="P"+players;
            app.StartGame(modes[i],scenarios[i],players,seat,true,targetVictoryPoints:i%2==0?5:20);
            for(int frame=0;frame<180&&!app.Ready;frame++)yield return null;
            yield return null;yield return null;
            Require(app.Ready&&app.PlayerCount==players&&app.HumanSeat==seat&&app.Paused,"四规则开局配置未生效");
            Require(app.TargetVictoryPoints>=5&&app.TargetVictoryPoints<=20,"菜单胜利分数越界");
            Require(i%2!=0||app.TargetVictoryPoints==5,"最小胜利分数未限制到 5");
            string authority=app.ExportEnvelope();
            ExpectConfigurationRejected(modes[i],scenarios[i],3,"P4");
            ExpectPointsRejected(modes[i],scenarios[i],players,seat,4);
            ExpectPointsRejected(modes[i],scenarios[i],players,seat,21);
            Require(app.ExportEnvelope()==authority,"非法席位配置改变当前局面");
            app.ShowMenu();
            Require(app.MenuVisible&&!app.StepAi(),"菜单中 AI 仍执行操作");
            Require(Field<int>("menuPlayers")==players&&Field<int>("menuHuman")==players-1,"菜单人数或席位回填错误");
            Require(Field<int?>("menuVictoryPoints")==app.TargetVictoryPoints,"菜单分数回填错误");
            Require(app.ExportEnvelope()==authority,"打开菜单改变权威局面");
            yield return Capture((5+i).ToString("D2")+"-menu-"+modes[i]+".png",modes[i]+" 返回配置菜单");
            app.ResumeGame();
            Require(!app.MenuVisible&&app.Ready&&app.ExportEnvelope()==authority,"返回游戏改变权威局面");
            // 不调用 SaveGame / LoadGame，不触碰用户的默认存档。
            string savePath=Path.Combine(output,modes[i]+"-private-review.json");
            app.WriteSave(savePath);
            Require(File.Exists(savePath)&&File.ReadAllText(savePath)==authority,"隔离验收存档未保持局面");
            app.ShowMenu();
        }
        stage="联机大厅与返回菜单配置";
        string beforeLobby=app.ExportEnvelope(),menuBeforeLobby=MenuConfiguration();
        var openLobby=typeof(M6App).GetMethod("OpenNetworkMenu",PrivateInstance);
        Require(openLobby!=null,"缺少联机菜单入口");
        openLobby.Invoke(app,null);
        yield return null;yield return null;
        Require(Field<bool>("networkMenu")&&app.MenuVisible&&!app.NetworkGame,"大厅打开时状态错误");
        Require(MenuConfiguration()==menuBeforeLobby&&app.ExportEnvelope()==beforeLobby,"打开大厅改变单人配置或局面");
        yield return Capture("09-menu-lobby.png","未连接服务的联机大厅");
        // 执行返回单人菜单按钮使用的同一局部状态赋值；没有伪造鼠标事件或连接服务。
        typeof(M6App).GetField("networkMenu",PrivateInstance).SetValue(app,false);
        yield return null;
        Require(!Field<bool>("networkMenu")&&app.MenuVisible&&MenuConfiguration()==menuBeforeLobby&&app.ExportEnvelope()==beforeLobby,"大厅返回后单人配置或局面丢失");
        yield return Capture("10-menu-after-lobby.png","联机大厅返回单人菜单");
        stage="音频运行检查";
        VerifyAudioRuntime();
    }

    string MenuConfiguration()=>Field<int>("menuRule")+"|"+Field<int>("menuScenario")+"|"+Field<int>("menuPlayers")+"|"+Field<int>("menuHuman")+"|"+Field<int?>("menuVictoryPoints")+"|"+Field<int>("menuAiSpeed");

    void VerifyAudioRuntime()
    {
        // 音效组件由并行任务接入；先用反射保持该验证器可独立编译。
        var audioType=typeof(M6App).Assembly.GetType("TabletopAudio");
        Require(audioType!=null,"未发现音效组件");
        const BindingFlags all=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        object instance=audioType.GetProperty("Instance",all)?.GetValue(null)??audioType.GetField("Instance",all)?.GetValue(null);
        Require(instance!=null,"音效单例未初始化");
        var ready=audioType.GetProperty("Ready",all);
        Require(ready!=null&&(bool)ready.GetValue(instance),"音效组件未就绪");
        var available=audioType.GetProperty("AvailableCues",all)?.GetValue(instance) as IEnumerable;
        var getter=audioType.GetMethod("GetClip",all);
        Require(available!=null&&getter!=null,"音效运行验证接口不完整");
        int clipCount=0;
        foreach(var cue in available)
        {
            var clip=getter.Invoke(instance,new[]{cue}) as AudioClip;
            Require(clip!=null&&clip.samples>0&&clip.frequency>=16000&&clip.channels>=1,"音效缺少有效样本");
            var samples=new float[Math.Min(clip.samples*clip.channels,32768)];
            Require(clip.GetData(samples,0)&&samples.Any(v=>Math.Abs(v)>.0001f),"音效片段为空或完全静音");
            Require(samples.All(v=>!float.IsNaN(v)&&!float.IsInfinity(v)&&Math.Abs(v)<=1.001f),"音效样本无效或削波");
            clipCount++;
        }
        Require(clipCount>0&&clipCount==Enum.GetValues(getter.GetParameters()[0].ParameterType).Length,"未找到全部音效片段");
        int voiceLimit=(int)audioType.GetField("VoiceLimit",all).GetRawConstantValue();
        int activeVoices=(int)audioType.GetProperty("ActiveVoices",all).GetValue(instance);
        Require(voiceLimit<=10&&activeVoices>=0&&activeVoices<=voiceLimit,"同时播放声部超出限制");
        audioNotes.Add("已检查 "+clipCount+" 个已生成音效片段：非空、采样率、可读非静音样本及数值范围。");
        audioNotes.Add("未改变用户音量偏好；片段检查不能代表扬声器输出或人工听感。");
    }

    T Field<T>(string name)
    {
        var field=typeof(M6App).GetField(name,PrivateInstance);
        Require(field!=null,"缺少菜单配置字段");return (T)field.GetValue(app);
    }
    void ExpectConfigurationRejected(string mode,string scenario,int players,string seat)
    {
        bool rejected=false;
        try{app.StartGame(mode,scenario,players,seat,true);}
        catch(InvalidDataException){rejected=true;}
        Require(rejected,"非法菜单配置未拒绝");
    }
    void ExpectPointsRejected(string mode,string scenario,int players,string seat,int points)
    {
        bool rejected=false;
        try{app.StartGame(mode,scenario,players,seat,true,targetVictoryPoints:points);}
        catch(ArgumentOutOfRangeException){rejected=true;}
        Require(rejected,"非法菜单胜利分数未拒绝");
    }
    IEnumerator SetResolution(int width,int height)
    {
        Screen.SetResolution(width,height,FullScreenMode.Windowed);
        for(int frame=0;frame<120&&(Screen.width!=width||Screen.height!=height);frame++)yield return null;
        yield return null;yield return null;
        Require(Screen.width==width&&Screen.height==height,"实际窗口尺寸不符");
    }
    IEnumerator Capture(string filename,string scene)
    {
        string path=Path.Combine(output,filename);
        if(File.Exists(path))File.Delete(path);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(path,1);
        bool written=false;
        for(int frame=0;frame<300;frame++)
        {
            yield return null;
            if(File.Exists(path)&&new FileInfo(path).Length>100){written=true;break;}
        }
        Require(written,"完整界面截图未写入");
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        bool loaded=ImageConversion.LoadImage(texture,File.ReadAllBytes(path));
        int width=texture.width,height=texture.height;Destroy(texture);
        Require(loaded&&width==Screen.width&&height==Screen.height,"截图尺寸不符");
        captures.Add(new CaptureRecord{file=filename,scene=scene,width=width,height=height,elapsed=Time.unscaledTimeAsDouble});
    }
    static int ChangedPixels(string beforePath,string afterPath)
    {
        var before=new Texture2D(2,2,TextureFormat.RGBA32,false);var after=new Texture2D(2,2,TextureFormat.RGBA32,false);
        ImageConversion.LoadImage(before,File.ReadAllBytes(beforePath));ImageConversion.LoadImage(after,File.ReadAllBytes(afterPath));
        var a=before.GetPixels32();var b=after.GetPixels32();int changes=0;
        if(a.Length==b.Length)for(int i=0;i<a.Length;i++)if(Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>12)changes++;
        Destroy(before);Destroy(after);return changes;
    }
    void Require(bool passed,string message){checks++;if(!passed)throw new InvalidOperationException(message);}
    void WriteResult(string status,string errorType)
    {
        if(string.IsNullOrEmpty(output))return;
        File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(new Result{
            status=status,unity=Application.unityVersion,generatedUtc=DateTime.UtcNow.ToString("o"),stage=stage,errorType=errorType,
            checks=checks,animatedPixelChanges=animatedPixelChanges,userSaveTouched=false,fullUiPixelsCaptured=captures.Count>0,
            humanVisualReview=false,humanAudioListening=false,captures=captures.ToArray(),audioChecks=audioNotes.ToArray(),
            unverified=new[]{"截图的最终外观需要人工逐张查看。","未执行真实鼠标、触屏的全部菜单链路。","手机仅验证宽屏安全区逻辑坐标，未完成 Android 真机运行。","未验证系统音量、扬声器输出、主观听感及长期性能。","独立验收局面包含完整权威数据，仅保留在 .local；公开证据只复制汇总与合成截图。"}
        },true));
    }
    static string ReviewDirectory(string supplied)
    {
        string root=FindProjectRoot(Application.dataPath)??FindProjectRoot(Directory.GetCurrentDirectory());
        if(root==null)throw new InvalidOperationException("未找到项目根目录。");
        string local=Path.GetFullPath(Path.Combine(root,".local"))+Path.DirectorySeparatorChar;
        string full=Path.GetFullPath(Path.IsPathRooted(supplied)?supplied:Path.Combine(root,supplied));
        if(!full.StartsWith(local,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("验收输出必须位于项目 .local 子目录。");
        return full;
    }
    static string FindProjectRoot(string path)
    {
        for(var directory=new DirectoryInfo(path);directory!=null;directory=directory.Parent)
            if(File.Exists(Path.Combine(directory.FullName,"docs","PROJECT_BRIEF.md"))&&Directory.Exists(Path.Combine(directory.FullName,"unity","Catan")))return directory.FullName;
        return null;
    }
}
