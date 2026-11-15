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
            var project=new DirectoryInfo(Application.dataPath);
            while(project!=null&&!File.Exists(Path.Combine(project.FullName,"docs","PROJECT_BRIEF.md")))project=project.Parent;
            string root=project?.FullName??throw new DirectoryNotFoundException("未找到项目");
            string local=Path.GetFullPath(Path.Combine(root,".local"))+Path.DirectorySeparatorChar;
            if(!output.StartsWith(local,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("验收目录必须位于 .local");
            if(app==null||Application.isBatchMode)throw new InvalidOperationException();
            Directory.CreateDirectory(output);saveDigest=Digest(app.SavePath);
            app.MenuReviewSavePath=Path.Combine(output,"private-review-save.json");
            app.ConfigureOnlineReview(Path.Combine(output,"private-online-address.txt"));
            app.Network.UseCredentialDirectory(Path.Combine(output,"private-player"));
            var listener=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);listener.Start();
            int port=((System.Net.IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
            int discovery;using(var udp=new System.Net.Sockets.UdpClient(0))discovery=((System.Net.IPEndPoint)udp.Client.LocalEndPoint).Port;
            app.ConfigureLanReview(Path.Combine(output,"private-server"),port,discovery);
        }
        catch(Exception e){Debug.LogError("CATAN_MEDIEVAL_MENU_VERIFY_FAILED: "+e.GetType().Name);Application.Quit(1);yield break;}
        var enumerators=new Stack();enumerators.Push(Review());
        while(enumerators.Count>0)
        {
            object next=null;bool moved=false;Exception error=null;
            try{moved=((IEnumerator)enumerators.Peek()).MoveNext();if(moved)next=((IEnumerator)enumerators.Peek()).Current;}catch(Exception e){error=e;}
            if(error!=null)
            {Debug.LogError("CATAN_MEDIEVAL_MENU_VERIFY_FAILED: "+stage+" "+error.GetType().Name+"\n"+error.StackTrace);Application.Quit(1);yield break;}
            if(!moved){enumerators.Pop();continue;}if(next is IEnumerator sub){enumerators.Push(sub);continue;}yield return next;
        }
        bool interactive=Array.IndexOf(Environment.GetCommandLineArgs(),"--settings-menu-interactive")>=0;
        File.WriteAllText(Path.Combine(output,"gallery-summary.json"),"{\"status\":\"passed\",\"captures\":"+captures+",\"checks\":"+checks+",\"userSaveTouched\":false,\"manualInput\":"+(interactive?"true":"false")+"}");
        Debug.Log("CATAN_MEDIEVAL_MENU_VERIFY_OK: captures="+captures+" checks="+checks);Application.Quit(0);
    }
    IEnumerator Review()
    {
        while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
        yield return null;yield return null;
        if(Environment.GetCommandLineArgs().Contains("--pause-menu-only")){
            yield return ReviewPauseMenu();yield break;
        }
        if(Environment.GetCommandLineArgs().Contains("--settings-menu-only")){
            yield return ReviewSettings();yield break;
        }
        if(Environment.GetCommandLineArgs().Contains("--load-menu-only")){
            Screen.SetResolution(1920,1080,false);yield return new WaitForSecondsRealtime(.5f);
            Go(1);yield return Capture("加载游戏-空存档");
            foreach(bool combined in new[]{false,true}){
                app.StartGame(combined?"combined":"base",combined?"through-the-desert":"heading-for-new-shores",
                    combined?4:3,combined?"P1":"P2",true,threeKingdoms:combined,harbormaster:combined,fishing:combined);
                for(int i=0;i<240&&!app.Ready;i++)yield return null;
                Require(app.Ready,"验收存档未就绪");app.WriteSave(app.MenuReviewSavePath);
            }
            app.ShowMenu();Go(1);yield return Capture("加载游戏");
            Set("backupExpanded",true);yield return Capture("加载游戏-备份");
            foreach(string suffix in new[]{".bak",""}){
                typeof(M6App).GetMethod("LoadMenuSave",Flags).Invoke(app,new object[]{app.MenuReviewSavePath+suffix});
                var action=(Action)typeof(M6App).GetField("menuPendingAction",Flags).GetValue(app);
                Require(action!=null,"缺少读档确认");Set("menuPendingAction",null);action();
                for(int i=0;i<240&&!app.Ready;i++)yield return null;
                Require(app.Ready&&app.PlayerCount==(suffix==""?4:3)&&app.HumanSeat==(suffix==""?"P1":"P2"),"读档席位不符");
                app.ShowMenu();Go(1);
            }
            Require(Digest(app.SavePath)==saveDigest,"用户存档被修改");yield break;
        }
        if(Environment.GetCommandLineArgs().Contains("--map-picker-only")){
            Screen.SetResolution(1920,1080,false);yield return new WaitForSecondsRealtime(.5f);
            Go(0);Set("menuRule",3);Set("menuPlayers",4);Set("menuThreeKingdoms",true);
            Set("menuHarbormaster",true);Set("menuFishing",true);
            Set("menuScenario",Array.IndexOf(Catan.Core.M5.CombinedScenarios.Ids,"through-the-desert"));
            typeof(M6App).GetMethod("OpenMapPicker",Flags).Invoke(app,null);
            yield return Capture("地图选择");
            Require(Screen.width==1920&&Screen.height==1080,"截图分辨率");
            Set("mapScroll",new Vector2(0,10000));yield return Capture("地图选择-末行");
            Require(((Vector2)typeof(M6App).GetField("mapScroll",Flags).GetValue(app)).y>0,"地图列表滚动");
            Set("menuPlayers",3);Set("mapScroll",Vector2.zero);yield return Capture("地图选择-三人");
            yield break;
        }
        Require(PastoralMenuScene.ArtworkReady,"背景未打入构建");
        Require(Resources.Load<Texture2D>("PastoralMenu/v004/title")!=null,"标题未打入构建");
        Require(PastoralMenuScene.Preview(PastoralMenuScene.CollectorTable)!=null,"棋桌背景未打入构建");
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
        foreach(var size in new[]{new Vector2Int(1440,900),new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1920,900)})
        {
            Screen.SetResolution(size.x,size.y,false);yield return new WaitForSecondsRealtime(.5f);
            Go(-1);yield return Capture("home-"+size.x+"x"+size.y);
            Require(Screen.width==size.x&&Screen.height==size.y,"分辨率未生效");
        }
        Screen.SetResolution(1440,900,false);yield return new WaitForSecondsRealtime(.5f);
        Go(0);Set("menuRule",3);Set("menuScenario",Array.IndexOf(Catan.Core.M5.CombinedScenarios.Ids,"through-the-desert"));
        Set("menuPlayers",4);Set("menuThreeKingdoms",true);Set("menuHarbormaster",true);Set("menuFishing",true);
        yield return Capture("new-game-configured");
        Screen.SetResolution(1920,1080,false);yield return new WaitForSecondsRealtime(.5f);
        Require(Screen.width==1920&&Screen.height==1080,"全高清菜单尺寸未生效");
        app.SetupReviewPointer=Vector2.zero;
        yield return Capture("new-game-configured-1920x1080");
        yield return CheckStaticSetupControls();
        app.SetupReviewPointer=new Vector2(1426,387);
        yield return Capture("new-game-map-hover-1920x1080");
        app.SetupReviewPointer=new Vector2(980,808);
        yield return Capture("new-game-start-hover-1920x1080");
        app.SetupReviewPointer=Vector2.zero;
        Set("menuScenario",Array.IndexOf(Catan.Core.M5.CombinedScenarios.Ids,"heading-for-new-shores"));
        Set("menuPlayers",3);Set("menuAiSpeed",0);
        yield return Capture("new-shores-three-seats-1920x1080");
        Set("menuScenario",Array.IndexOf(Catan.Core.M5.CombinedScenarios.Ids,"through-the-desert"));
        Set("menuPlayers",4);Set("menuAiSpeed",1);Set("menuHarbormaster",true);Set("menuFishing",true);
        app.SetupReviewPointer=null;
        Screen.SetResolution(1440,900,false);yield return new WaitForSecondsRealtime(.5f);
        Set("atlasOpen",true);yield return Capture("new-game-maps");
        Set("mapScroll",new Vector2(0,10000));yield return Capture("new-game-maps-bottom");
        Require(((Vector2)typeof(M6App).GetField("mapScroll",Flags).GetValue(app)).y>0,"地图列表未滚至后续行");
        Set("atlasOpen",false);
        Go(1);yield return Capture("load-empty");
        Go(4);Set("menuSettingsTab",0);yield return Capture("settings-player");
        Set("menuSettingsTab",1);yield return Capture("settings-sound");
        Set("menuSettingsTab",2);yield return Capture("settings-appearance");
        Set("menuSettingsTab",3);yield return Capture("settings-landscape");
        Go(2);yield return Capture("lan-room-list");
        Set("networkTab",1);
        yield return Capture("lan-create-room");
        string address=(string)typeof(M6App).GetField("networkAddress",Flags).GetValue(app);
        Go(3);Require(string.IsNullOrEmpty((string)typeof(M6App).GetField("networkAddress",Flags).GetValue(app)),"在线入口继承了局域网地址");
        yield return Capture("online-connect");
        Go(2);Require((string)typeof(M6App).GetField("networkAddress",Flags).GetValue(app)==address,"局域网地址未保留");
        var modes=new[]{"base","seafarers","cities-knights","combined"};
        for(int i=0;i<4;i++)
        {
            stage=modes[i]+"-菜单隔离";
            app.StartGame(modes[i],i==3?"through-the-desert":"heading-for-new-shores",i%2==0?3:4,"P1",true,threeKingdoms:i==3,harbormaster:i==3,fishing:i==3);
            for(int wait=0;!app.Ready&&wait<240;wait++)yield return null;
            Require(app.Ready,"开局未就绪");string authority=app.ExportEnvelope();
            var tokens=FindObjectsByType<TabletopNumberTokens>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            Require(tokens.Length==1,"真实棋盘缺少数字组件");
            var numbers=tokens[0].GetComponentsInChildren<MeshRenderer>(true);
            Require(numbers.Length>0&&numbers.All(r=>r.sharedMaterial.mainTexture!=null&&r.GetComponent<JadeNumberSlip>()!=null),"真实棋盘玉签或点数贴图缺失");
            yield return Capture("board-"+modes[i]);
            if(i==3){
                app.OpenTableMenu();yield return Capture("pause-menu");
                Set("tablePausePage",1);yield return Capture("pause-settings");
                Set("tablePauseSettingsTab",1);yield return Capture("pause-audio");
                Set("tablePauseSettingsTab",4);yield return Capture("pause-view");
            }
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
    void PauseAction(int index)=>typeof(M6App).GetMethod("ActivateTablePauseEntry",Flags).Invoke(app,new object[]{index});
    IEnumerator ReviewPauseMenu(){
        stage="局内菜单-创建对局";
        Screen.SetResolution(1920,1080,false);yield return new WaitForSecondsRealtime(.5f);
        app.StartGame("combined","through-the-desert",4,"P1",true,threeKingdoms:true,harbormaster:true,fishing:true);
        for(int i=0;i<240&&!app.Ready;i++)yield return null;
        Require(app.Ready&&app.ThreeKingdoms&&app.Harbormaster&&app.Fishing,"局内菜单验收配置");
        var game=app.GetComponent<M5App>();var host=game.VerificationHost;var policy=new Catan.AI.CombinedAi();
        // 只推进隔离验收局面，一帧一条合法命令，让原型对照中有真实建筑与手牌。
        for(int i=0;i<160&&host.View("P1").Turn<4;i++){
            stage="局内菜单-准备棋盘-"+i;
            var view=host.View("P1");
            string actor=view.ThreeKingdoms?.WaitingSelectionPlayerIds?.FirstOrDefault()??M5App.Actor(view);
            var command=policy.Decide(host.AiView(actor));
            Require(command!=null,"验收对局无法推进");command.Id=Guid.NewGuid().ToString("N");
            Require(host.Submit(command).Success,"验收对局命令未成功");
            yield return null;
        }
        game.M6Refresh();yield return new WaitForSecondsRealtime(1);
        Require(host.View("P1").Board.Cities.Length+host.View("P1").Board.Settlements.Length>=4,"验收棋盘缺少建筑");
        string authority=app.ExportEnvelope(),rules=host.ExportSave();
        app.OpenTableMenu();
        Require(app.TableMenuOpen&&app.TablePresentationBlocked&&!app.StepAi(),"菜单未阻挡本地调度");
        foreach(string asset in new[]{"sheet","wood","resume"})
            Require(Resources.Load<Texture2D>("AcceptedMenu/setup-pause-"+asset)!=null,"局内菜单素材未打包");
        foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720),new Vector2Int(1440,900),new Vector2Int(1920,900)}){
            Screen.SetResolution(size.x,size.y,false);yield return new WaitForSecondsRealtime(.4f);
            Require(Screen.width==size.x&&Screen.height==size.y,"局内菜单尺寸未生效");
            yield return Capture("游戏内菜单-"+size.x+"x"+size.y);
        }
        Screen.SetResolution(1920,1080,false);yield return new WaitForSecondsRealtime(.4f);
        Require(app.ExportEnvelope()==authority,"打开菜单改变权威局面");
        PauseAction(1);
        Require(File.Exists(app.MenuReviewSavePath)&&Digest(app.SavePath)==saveDigest,"保存未隔离用户存档");
        Require(app.ExportEnvelope()==authority,"保存改变了当前局面");
        yield return Capture("游戏内菜单-保存反馈");
        PauseAction(2);
        Require(app.TablePauseSettingsOpen&&!app.StepAi(),"设置未保持对局暂停");
        yield return Capture("游戏内菜单-设置");
        app.HandleTableEscape();Require(app.TableMenuOpen&&!app.TablePauseSettingsOpen,"设置未逐层返回");
        PauseAction(3);
        Require((bool)typeof(M6App).GetField("encyclopediaOpen",Flags).GetValue(app)&&app.TablePresentationBlocked,"百科未保持菜单上下文");
        yield return Capture("游戏内菜单-百科");
        app.HandleTableEscape();Require(app.TableMenuOpen&&!(bool)typeof(M6App).GetField("encyclopediaOpen",Flags).GetValue(app),"百科未逐层返回");
        PauseAction(0);
        Require(!app.TableMenuOpen&&!app.Paused&&host.ExportSave()==rules,"继续游戏改变规则局面或没有恢复调度");
        app.SetPaused(true);app.OpenTableMenu();
        app.HandleTableEscape();Require(!app.TableMenuOpen&&app.ExportEnvelope()==authority,"Esc返回改变局面");
        app.ReadSave(app.MenuReviewSavePath);
        for(int i=0;i<240&&!app.Ready;i++)yield return null;
        Require(app.Ready&&app.ExportEnvelope()==authority,"菜单保存无法完整恢复");
        app.OpenTableMenu();int target=app.TargetVictoryPoints;PauseAction(4);
        for(int i=0;i<240&&!app.Ready;i++)yield return null;
        Require(app.Ready&&app.RuleSet=="combined"&&app.ScenarioId=="through-the-desert"&&app.PlayerCount==4&&
            app.HumanSeat=="P1"&&app.ThreeKingdoms&&app.Harbormaster&&app.Fishing&&app.TargetVictoryPoints==target,"重新开始丢失配置");
        Require(app.GetComponent<M5App>().VerificationHost.ExportSave()!=rules,"重新开始没有生成新局");
        app.OpenTableMenu();PauseAction(5);
        Require(app.MenuVisible&&!app.TableMenuOpen&&!app.Ready,"返回主菜单未结束本地对局");
        Require(Digest(app.SavePath)==saveDigest,"局内菜单验收修改了用户存档");
    }
    IEnumerator ReviewSettings(){
        var audio=TabletopAudio.Instance;
        Require(audio!=null,"声音组件未加载");
        float master=audio.MasterVolume,effects=audio.EffectsVolume,music=audio.MusicVolume,
            ambient=audio.AmbientVolume,voice=audio.VoiceVolume;
        bool muted=audio.Muted;
        try{
            Screen.SetResolution(1920,1080,false);yield return new WaitForSecondsRealtime(.5f);
            app.StartGame("combined","through-the-desert",4,"P1",true,threeKingdoms:true,harbormaster:true,fishing:true);
            for(int i=0;i<240&&!app.Ready;i++)yield return null;
            Require(app.Ready,"设置验收对局未就绪");
            app.OpenTableMenu();Set("tablePausePage",1);Set("tablePauseSettingsTab",1);
            string authority=app.ExportEnvelope();
            audio.MasterVolume=.8f;audio.EffectsVolume=.7f;audio.MusicVolume=.45f;
            audio.AmbientVolume=.6f;audio.VoiceVolume=.85f;audio.Muted=false;
            foreach(string asset in new[]{"wood","jade","rail","fill","knob","done"})
                Require(Resources.Load<Texture2D>("AcceptedMenu/setup-settings-"+asset)!=null,"设置材质未打包："+asset);
            yield return Capture("游戏设置-声音-1080p");
            if(Environment.GetCommandLineArgs().Contains("--settings-menu-interactive")){
                File.WriteAllText(Path.Combine(output,"交互就绪.signal"),"1920×1080，声音页");
                float deadline=Time.realtimeSinceStartup+600;
                while(!File.Exists(Path.Combine(output,"交互完成.signal"))&&Time.realtimeSinceStartup<deadline)yield return null;
                Require(File.Exists(Path.Combine(output,"交互完成.signal")),"设置交互验收超时");
                audio.FlushSettings();
                var observed=new SettingsObservation{
                    master=audio.MasterVolume,effects=audio.EffectsVolume,music=audio.MusicVolume,
                    ambient=audio.AmbientVolume,voice=audio.VoiceVolume,muted=audio.Muted,
                    page=(int)typeof(M6App).GetField("tablePausePage",Flags).GetValue(app),
                    tab=(int)typeof(M6App).GetField("tablePauseSettingsTab",Flags).GetValue(app),
                    savedMaster=PlayerPrefs.GetFloat("catan.audio.v1.master",-1),
                    savedEffects=PlayerPrefs.GetFloat("catan.audio.v1.effects",-1),
                    savedMuted=PlayerPrefs.GetInt("catan.audio.v1.muted",-1)};
                File.WriteAllText(Path.Combine(output,"交互结果.json"),JsonUtility.ToJson(observed));
                // 真实鼠标验收以总音量零端点、操作音效满端点、静音后点完成为终态。
                Require(observed.master<.001f&&observed.effects>.999f&&observed.muted,"鼠标拖动端点或静音未生效");
                Require(observed.page==0&&observed.tab==1,"完成未返回游戏菜单");
                Require(Mathf.Approximately(observed.master,observed.savedMaster)&&
                    Mathf.Approximately(observed.effects,observed.savedEffects)&&observed.savedMuted==1,"声音偏好未保存");
                yield return Capture("游戏设置-完成返回");
                app.OpenTableMenu();Set("tablePausePage",1);
            }
            Require(app.ExportEnvelope()==authority,"设置操作改变了权威局面");
            Set("tablePauseSettingsTab",1);
            audio.MasterVolume=0;yield return Capture("游戏设置-音量零端点");
            audio.MasterVolume=1;audio.Muted=true;yield return Capture("游戏设置-静音与满音量");
            audio.MasterVolume=.8f;audio.Muted=false;
            for(int tab=0;tab<5;tab++){
                if(tab==1)continue;
                Set("tablePauseSettingsTab",tab);yield return Capture("游戏设置-分类-"+tab);
            }
            Set("tablePauseSettingsTab",1);
            Screen.SetResolution(1280,720,false);yield return new WaitForSecondsRealtime(.5f);
            yield return Capture("游戏设置-声音-720p");
            Screen.SetResolution(1920,1080,false);yield return new WaitForSecondsRealtime(.5f);
            app.ShowMenu();Go(4);Set("menuSettingsTab",1);
            yield return Capture("主页设置-声音-1080p");
            Require(app.ExportEnvelope()==authority,"设置页导航改变了权威局面");
            Require(Digest(app.SavePath)==saveDigest,"用户存档被修改");
        }finally{
            audio.MasterVolume=master;audio.EffectsVolume=effects;audio.MusicVolume=music;
            audio.AmbientVolume=ambient;audio.VoiceVolume=voice;audio.Muted=muted;audio.FlushSettings();
        }
    }
    [Serializable]sealed class SettingsObservation{
        public float master,effects,music,ambient,voice,savedMaster,savedEffects;
        public bool muted;public int page,tab,savedMuted;
    }
    IEnumerator CheckStaticSetupControls(){
        // 指针按菜单的设计坐标移动，只比较本次控件所在区域的最终帧。
        var areas=new[]{new Rect(144,122,174,58),new Rect(854,170,130,154),
            new Rect(1073,424,375,60),new Rect(1108,496,69,45),new Rect(1346,496,69,45),
            new Rect(1075,561,374,46),new Rect(418,645,113,106)};
        yield return new WaitForEndOfFrame();
        var baseline=ScreenCapture.CaptureScreenshotAsTexture();
        try{
            foreach(var area in areas){
                app.SetupReviewPointer=area.center;
                yield return new WaitForSecondsRealtime(.2f);
                yield return new WaitForEndOfFrame();
                var hovered=ScreenCapture.CaptureScreenshotAsTexture();
                try{
                    int x=Mathf.FloorToInt(area.x*Screen.width/1672);
                    int y=Mathf.FloorToInt((941-area.yMax)*Screen.height/941);
                    int w=Mathf.CeilToInt(area.width*Screen.width/1672),h=Mathf.CeilToInt(area.height*Screen.height/941);
                    Require(baseline.GetPixels(x,y,w,h).SequenceEqual(hovered.GetPixels(x,y,w,h)),"控件悬停时画面发生变化 "+area);
                }finally{Destroy(hovered);}
            }
        }finally{Destroy(baseline);app.SetupReviewPointer=null;}
    }
    void Set(string name,object value)=>typeof(M6App).GetField(name,Flags).SetValue(app,value);
    void Require(bool condition,string reason){checks++;if(!condition)throw new InvalidOperationException(reason);}
    IEnumerator Capture(string name)
    {
        stage=name;yield return new WaitForSecondsRealtime(.4f);
        // 中文动态字体在换页时可能重建图集；确保至少三次完整重绘后记录。
        for(int frame=0;frame<3;frame++)yield return new WaitForEndOfFrame();
        var texture=ScreenCapture.CaptureScreenshotAsTexture();
        Require(texture!=null&&texture.width==Screen.width,"截图不完整");
        if(name.StartsWith("游戏内菜单"))Require(texture.GetPixel(texture.width/2,texture.height/2).maxColorComponent>.1f,"局内菜单截图为空帧");
        if((name.StartsWith("地图选择")||name.StartsWith("加载游戏")||name.StartsWith("主页设置"))&&texture.GetPixel(texture.width/2,texture.height/2).maxColorComponent<.02f){
            Destroy(texture);requestAtlasFrame=true;
            for(int frame=0;requestAtlasFrame&&frame<30;frame++)yield return new WaitForEndOfFrame();
            texture=atlasFrame;atlasFrame=null;
            Require(texture!=null&&texture.GetPixel(texture.width/2,texture.height/2).maxColorComponent>.1f,"选图页空帧");
            Debug.Log("选图页后台截图使用实际菜单离屏重绘");
        }
        File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());Destroy(texture);captures++;
    }
    bool requestAtlasFrame;
    Texture2D atlasFrame;
    void OnGUI(){
        if(!requestAtlasFrame||Event.current.type!=EventType.Repaint)return;
        requestAtlasFrame=false;
        var previous=RenderTexture.active;var matrix=GUI.matrix;
        var target=RenderTexture.GetTemporary(Screen.width,Screen.height,0,RenderTextureFormat.ARGB32);
        try{
            Graphics.SetRenderTarget(target);GL.Clear(true,true,Color.clear);
            GUI.matrix=MobileTabletop.GuiMatrix;
            typeof(M6App).GetMethod("DrawPastoralMainMenu",Flags).Invoke(app,null);
            atlasFrame=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
            atlasFrame.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);atlasFrame.Apply();
        }finally{Graphics.SetRenderTarget(previous);GUI.matrix=matrix;RenderTexture.ReleaseTemporary(target);}
    }
    static string Digest(string path)
    {if(!File.Exists(path))return "missing";using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));}
}
