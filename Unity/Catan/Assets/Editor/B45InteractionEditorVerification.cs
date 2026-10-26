using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Catan.Core;
using Catan.Core.ThreeKingdoms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using M4=Catan.Core.M4;

// 只在显式编辑器验收入口启用。局面注入与鼠标驱动都留在Editor，不进入交付客户端。
[InitializeOnLoad]
public static class B45InteractionEditorVerification
{
    const string Key="Catan.B45Interaction.Output",Deadline="Catan.B45Interaction.Deadline";
    static readonly BindingFlags Instance=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly BindingFlags Static=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    [Serializable] sealed class Result
    {
        public string 状态,说明,诊断,支付目标方式;
        public int 断言数,鼠标点击,模态窗口次数;
        public string[] 场景,截图;
    }
    static Result result;
    static readonly List<string> cases=new List<string>(),images=new List<string>();
    static IEnumerator work;
    static double waitUntil;
    static string stage,output;
    static bool Integrated=>Environment.GetCommandLineArgs().Contains("--b5-direct-integration");
    static M6App app;
    static M4App city;
    static M4.CitiesKnightsGameSession session;
    static B45InteractionEditorVerification(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"--b45-interaction-verify");
        if(flag<0||flag+1>=args.Length)throw new InvalidOperationException("缺少--b45-interaction-verify及输出目录");
        string path=Path.GetFullPath(args[flag+1]);string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../..",".local"))+Path.DirectorySeparatorChar;
        if(!path.StartsWith(root,StringComparison.Ordinal))throw new InvalidOperationException("验收输出必须位于项目.local内");
        Directory.CreateDirectory(path);var file=Path.Combine(path,"result.json");if(File.Exists(file))File.Delete(file);
        SessionState.SetString(Key,path);SessionState.SetString(Deadline,DateTime.UtcNow.AddMinutes(6).ToString("O"));
        EditorSceneManager.OpenScene("Assets/M6.unity",OpenSceneMode.Single);GameView().Focus();EditorApplication.isPlaying=true;
    }
    static EditorWindow GameView()=>EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
    static void Tick()
    {
        output=SessionState.GetString(Key,"");if(output.Length==0)return;
        if(DateTime.TryParse(SessionState.GetString(Deadline,""),out var deadline)&&DateTime.UtcNow>deadline){Finish(new TimeoutException("界面验收超过六分钟"));return;}
        if(!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        if(work==null)
        {
            app=UnityEngine.Object.FindAnyObjectByType<M6App>();if(app==null)return;
            result=new Result();cases.Clear();images.Clear();work=Flatten(Verify());
        }
        if(EditorApplication.timeSinceStartup<waitUntil)return;
        try
        {
            if(!work.MoveNext()){Finish(null);return;}
            if(work.Current is double seconds)waitUntil=EditorApplication.timeSinceStartup+seconds;
        }
        catch(Exception ex){Finish(ex);}
    }
    static IEnumerator Flatten(IEnumerator root)
    {
        var stack=new Stack<IEnumerator>();stack.Push(root);
        while(stack.Count>0){var top=stack.Peek();if(!top.MoveNext()){stack.Pop();continue;}if(top.Current is IEnumerator child)stack.Push(child);else yield return top.Current;}
    }
    static object Get(object target,string name)=>target.GetType().GetField(name,Instance).GetValue(target);
    static void Set(object target,string name,object value)=>target.GetType().GetField(name,Instance).SetValue(target,value);
    static object Invoke(object target,string name,params object[] values)=>target.GetType().GetMethod(name,Instance).Invoke(target,values);
    static B45SelectionContext Selection=>(B45SelectionContext)Get(app,"generalSelection");
    static void Require(bool value,string message){result.断言数++;if(!value)throw new InvalidOperationException(stage+"："+message);}
    static M4.GameState State()=>session.GetAuthoritativeStateForTesting();
    static M4.PlayerState Player(string id="P1")=>State().Players.Single(p=>p.Id==id);
    static string Save()=>session.Save();
    static Rect[] HandRects()=>(Rect[])typeof(OperationDock).GetProperty("VerificationHandRects",Instance).GetValue(Get(city,"dock"));
    static void SetSize(int width,int height)=>typeof(B45DockEditorVerification).GetMethod("SetSize",Static).Invoke(null,new object[]{width,height});
    static Vector2 Physical(Vector2 logical)=>logical; // 固定1440×900 GameView；Editor update里的Screen尺寸属于编辑器窗口。
    static void Send(EventType type,Vector2 logical,int button=0)
    {
        typeof(B45DockEditorVerification).GetMethod("Send",Static).Invoke(null,new object[]{new Event{type=type,button=button,mousePosition=Physical(logical)}});
    }
    static IEnumerator WaitForRenderedInput()
    {
        int frame=Time.frameCount;double deadline=EditorApplication.timeSinceStartup+3;
        yield return .18;
        while(Time.frameCount<frame+3&&EditorApplication.timeSinceStartup<deadline)yield return .04;
        Require(Time.frameCount>=frame+3,"GameView未消费足够帧，不能判定鼠标事件已处理");
    }
    static IEnumerator Click(Vector2 point)
    {Send(EventType.MouseMove,point);yield return WaitForRenderedInput();Send(EventType.MouseDown,point);yield return WaitForRenderedInput();Send(EventType.MouseUp,point);yield return WaitForRenderedInput();result.鼠标点击++;}
    static IEnumerator Hand(int index)
    {var rects=HandRects();Require(index>=0&&index<rects.Length,"缺少可点击手牌"+index);yield return Click(rects[index].center);}
    static Vector2 Avatar(int index)=>((Rect)typeof(TabletopUI).GetMethod("PlayerAvatarRect",Static).Invoke(null,new object[]{Array.IndexOf(app.HistoryPlayers,"P"+(index+1)),4})).center;
    static Vector2 BoardPoint(string id)
    {
        var board=(M4BoardRenderer)Get(city,"board");var camera=board.BoardCamera;
        var p=camera.WorldToViewportPoint(board.Position(id)+Vector3.up*.005f);var r=camera.rect;
        return new Vector2((r.x+p.x*r.width)*1440,(1-r.y-p.y*r.height)*900);
    }
    static B45PaymentContext Payment=>(B45PaymentContext)Get(city,"directPayment");
    static IEnumerator Capture(string title)
    {
        string file=title+".png",path=Path.Combine(output,file);ScreenCapture.CaptureScreenshot(path);
        for(int i=0;i<20&&!File.Exists(path);i++)yield return .1;
        Require(File.Exists(path),"没有生成截图");var texture=new Texture2D(2,2);texture.LoadImage(File.ReadAllBytes(path));
        Require(texture.width==1440&&texture.height==900,"截图不是实际GameView尺寸");
        UnityEngine.Object.DestroyImmediate(texture);images.Add(file);
    }
    static void CheckNoModal()
    {bool modal=((OperationDock)Get(city,"dock")).HasOverlay;bool block=(bool)app.GetType().GetProperty("ThreeKingdomsPanelBlocksTable",Instance).GetValue(app);if(modal||block)result.模态窗口次数++;Require(!modal,"操作打开了模态窗口");Require(!block,"全屏输入拦截仍然存在");}
    static IEnumerator Verify()
    {
        stage="启动真实桌面";SetSize(1440,900);app.StartGame("cities-knights","heading-for-new-shores",4,"P1",true,threeKingdoms:true);yield return 1.5;
        city=app.GetComponent<M4App>();Require(city!=null&&app.Ready,"城市骑士主机未启动");
        string before=null;
        if(!Environment.GetCommandLineArgs().Contains("--b45-payment-only"))
        {
        stage="仁德取消与单次提交";Inject("tk-g15",new[]{2,1,0,0,0},new int[5]);yield return .6;
        before=Save();yield return Click(new Vector2(252,738));Require(Selection.Active,"点击仁德没有进入草稿");
        yield return Hand(0);yield return Click(Avatar(1));Require(Selection.CanConfirm,"牌与头像没有组成合法仁德");CheckNoModal();
        yield return Capture("仁德-直接选牌与头像");yield return Click(new Vector2(1018,679));Require(!Selection.Active&&Save()==before,"取消草稿改变权威状态");
        yield return Click(new Vector2(252,738));yield return Hand(0);yield return Click(Avatar(1));yield return Click(new Vector2(1175,679));
        Require(Player().Resources.Wood==1&&Player("P2").Resources.Wood==1,"仁德没有真实转移一张木材");
        before=Save();yield return Click(new Vector2(1175,679));Require(Save()==before,"第二次确认重复执行或误结束出牌");
        CheckNoModal();cases.Add(stage);yield return Capture("仁德-一次确认后的桌面");
        stage="制衡直接选牌";Inject("tk-g22",new[]{2,0,0,0,0},new int[5]);yield return .5;
        yield return Click(new Vector2(252,738));yield return Hand(0);yield return Hand(1);Require(Selection.CanConfirm,"制衡无法组成整批费用");
        yield return Capture("制衡-两张实体牌草稿");yield return Click(new Vector2(1175,679));
        Require(State().ThreeKingdoms.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P1").Counters.Any(c=>c.Key=="zhiheng-hand"&&c.Value==1),"制衡次数未被消耗");
        Require(Player().Resources.Total==2,"制衡真实牌数量不守恒");CheckNoModal();cases.Add(stage);
        stage="国色费用与头像";Inject("tk-g05",new[]{0,2,0,0,0},new int[5]);yield return .5;
        yield return Click(new Vector2(252,793));yield return Hand(0);yield return Hand(1);yield return Click(Avatar(1));Require(Selection.CanConfirm,"国色费用和头像不完整");
        yield return Capture("国色-双砖与目标头像");yield return Click(new Vector2(1175,679));
        Require(Player().Resources.Brick==0&&State().ThreeKingdoms.Skills.TimedEffects.Any(e=>e.Kind=="response-indulgence-pending"&&e.TargetPlayerId=="P2"),"国色没有一次支付与落延时牌");CheckNoModal();cases.Add(stage);
        stage="魄袭私密边界与批量确认";Inject("tk-g08",new[]{1,1,1,0,0},new[]{0,0,0,0,1});yield return .5;
        yield return Click(new Vector2(252,793));yield return Click(Avatar(1));yield return Click(new Vector2(1175,679));
        Require(State().ThreeKingdoms.Skills.PendingDecision?.Kind=="response-poxi-select","魄袭未进入真实私密步骤");
        Require(State().ThreeKingdoms.Generals.Single(g=>g.GeneralId=="tk-g08").Counters.Any(c=>c.Key=="poxi"&&c.Value==1),"私密信息已揭示但次数未占用");
        yield return Hand(0);yield return Hand(1);yield return Hand(2);
        var privateOptions=Selection.PrivateOptions.GroupBy(a=>a.Command.CardType).Select(g=>g.First()).ToArray();
        Require(privateOptions.Length==1&&privateOptions[0].Command.CardType==4,"获授权目标牌候选错误");
        var local=Selection.Options.Where(a=>B45SelectionContract.Players(a).Length==0&&B45SelectionContract.Board(a).Length==0&&B45SelectionContract.Cost(a).Sum()==0&&B45SelectionContract.Progress(a).Length==0||Selection.Options.Length>1).ToArray();
        yield return Click(new Vector2(350+local.Length*142+67,558));Require(Selection.CanConfirm,"魄袭没有选满四种牌");
        yield return Capture("魄袭-本人牌与授权目标牌分区");yield return Click(new Vector2(1175,679));
        Require(Player().Resources.Total==0&&Player("P2").Resources.Total==0,"魄袭没有原子弃置四种牌");CheckNoModal();cases.Add(stage);
        stage="被动左击与右击说明";Inject("tk-g43",new[]{1,0,0,0,0},new int[5]);yield return .5;
        before=Save();yield return Click(new Vector2(252,738));Require(!Selection.Active&&Save()==before,"被动按钮左击发动了技能");
        Send(EventType.MouseDown,new Vector2(252,738),1);yield return .2;Require(Get(app,"inspectionText")!=null&&Save()==before,"被动右击没有说明或改变权威状态");
        yield return Capture("被动-右键说明与无左击入口");Send(EventType.MouseUp,new Vector2(252,738),1);yield return .18;result.鼠标点击++;CheckNoModal();cases.Add(stage);
        }
        stage="龙胆普通造路的实体支付";Inject("tk-g42",new[]{0,2,0,0,0},new int[5]);Set(app,"inspectionText",null);yield return .5;
        var edge=session.GetPlayerView("P1").Board.Edges.OrderBy(e=>(BoardPoint(e.Id)-new Vector2(680,390)).sqrMagnitude).First();
        var state=State();state.Settlements=new[]{new PiecePlacement{PlayerId="P1",LocationId=edge.Vertices[0]}};state.Players.Single(p=>p.Id=="P1").Pieces.Settlements=4;
        Set(session,"state",state);Invoke(session,"RecalculateAwards",state);session.AssertInvariants();Invoke(app,"ResetBuildPresentation");Invoke(city,"M6Refresh");yield return .4;
        before=Save();var snapshot=(OperationSnapshot)Invoke(city,"Operations");var pieces=snapshot.Builds.Where(b=>b.Track<0&&b.Icon!="development").ToArray();
        int roadIndex=Array.FindIndex(pieces,b=>b.Id=="BuildRoad");Require(roadIndex>=0,"库存没有道路模型");
        yield return Click(TabletopPieceDock.PiecePosition(TabletopPieceDock.GroupLayout(pieces.Length,roadIndex)));
        Require(Payment.Active,"点击道路库存未进入真实支付草稿");yield return Click(BoardPoint(edge.Id));Require(Save()==before,"选择造路目标提前支付或落位");
        yield return Hand(0);yield return Click(new Vector2(378.5f,637));yield return Hand(1);yield return Click(new Vector2(444,637));
        Require(Payment.Ready&&Payment.SelectedCounts[1]==2&&Payment.EffectiveCounts[0]==1&&Payment.EffectiveCounts[1]==1,"龙胆两张实体砖未分别选成木与砖");
        Require(Save()==before,"选择普通支付牌或用途改变了权威状态");
        // GameView的OnGUI队列和旧Input鼠标坐标可能不同步；先在用途完整后再次尝试真实输入。
        if(string.IsNullOrEmpty(((M4.Command)Get(city,"draft")).TargetId))yield return Click(BoardPoint(edge.Id));
        result.支付目标方式="原生GameView鼠标事件";
        if(string.IsNullOrEmpty(((M4.Command)Get(city,"draft")).TargetId)&&Environment.GetCommandLineArgs().Contains("--b45-allow-target-fixture"))
        {
            result.支付目标方式="仅地图目标使用夹具调用TryStagePaymentTarget；地图鼠标命中留待Windows/Android验收";
            var targetCommand=B45PaymentContext.Copy((M4.Command)Get(city,"draft"));targetCommand.TargetId=edge.Id;
            Require((bool)Invoke(city,"TryStagePaymentTarget",targetCommand),"地图目标夹具未进入真实支付草稿");
        }
        Require(Save()==before,"选择或夹具定位地图目标改变了权威状态");
        CheckNoModal();yield return Capture("龙胆-两张实体砖与普通造路用途");
        yield return Click(new Vector2(1106,682));Require(State().Roads.Any(r=>r.PlayerId=="P1"&&r.LocationId==edge.Id)&&Player().Resources.Brick==0,"确认普通支付没有一次付砖并造路");
        CheckNoModal();cases.Add(stage);yield return Capture("龙胆-普通支付后真实道路");
    }
    static void Inject(string first,int[] own,int[] other)
    {
        var host=city.VerificationHost;host.NewGame(4,97,null,true);session=(M4.CitiesKnightsGameSession)Get(host,"session");var state=State();
        state.StartingPlayerIndex=0;state.ActivePlayerId="P1";state.Turn=9;state.Phase=M4.GamePhase.Action;state.SetupPlacementIndex=8;
        state.PendingDecision=null;state.RulesVersion=Integrated?M4.CitiesKnightsGameSession.ThreeKingdomsB5DirectRulesVersion:M4.CitiesKnightsGameSession.ThreeKingdomsB45RulesVersion;
        var tk=ThreeKingdomsEngine.Create(new[]{"P1","P2","P3","P4"},97,_=>0,true,Integrated?GeneralCatalog.B5DirectPackageVersion:GeneralCatalog.B45PackageVersion);
        Require(tk.Generals.Length==(Integrated?49:30),"交互夹具的武将包数量不匹配");
        while(tk.PendingDecision!=null){var d=tk.PendingDecision;if(d.Kind==ThreeKingdomsDecisionKind.SelectOpening)ThreeKingdomsEngine.ChooseGenerals(tk,d.PlayerId,d.CandidateIds.Take(2).ToArray(),d.DecisionId,d.WindowId,_=>0);else ThreeKingdomsEngine.DeployGeneral(tk,d.PlayerId,d.CandidateIds[0],d.DecisionId,d.WindowId,_=>0);}
        ThreeKingdomsEngine.BeginNormalTurns(tk);tk.Clock.Phase=ThreeKingdomsPhase.Play;tk.Clock.PhaseId=5;tk.Clock.DrawPhaseTurnInstanceId=tk.Clock.TurnInstanceId;
        var generals=new[]{first,"tk-g12","tk-g37","tk-g19"};
        for(int i=0;i<4;i++){var active=tk.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P"+(i+1));var wanted=tk.Generals.Single(g=>g.GeneralId==generals[i]);var id=active.GeneralId;active.GeneralId=wanted.GeneralId;wanted.GeneralId=id;id=active.InstanceId;active.InstanceId=wanted.InstanceId;wanted.InstanceId=id;active.Counters=Array.Empty<GeneralCounter>();B2SkillEngine.OnB4GeneralActivated(tk,active);}
        tk.Response=null;state.ThreeKingdoms=tk;
        var resources=(Resource[])Enum.GetValues(typeof(Resource));foreach(var player in state.Players)foreach(var resource in resources)player.Resources[resource]=0;
        for(int i=0;i<5;i++){state.Players[0].Resources[resources[i]]=own[i];state.Players[1].Resources[resources[i]]=other[i];state.Bank[resources[i]]=19-own[i]-other[i];}
        Set(session,"state",state);Invoke(session,"RecalculateAwards",state);session.AssertInvariants();
        Invoke(app,"ResetThreeKingdomsPanel");Invoke(app,"ResetCardPresentation",new object[]{null});Invoke(app,"ResetBuildPresentation");Set(app,"TableDiceRolling",false);
        Invoke(city,"M6Refresh");
    }
    static void Finish(Exception failure)
    {
        if(result==null)result=new Result();result.状态=failure==null?"通过":"失败";
        result.诊断="EditorPaused="+EditorApplication.isPaused+", focused="+(EditorWindow.focusedWindow==null?"null":EditorWindow.focusedWindow.GetType().Name)+", appFocused="+Application.isFocused+", frame="+Time.frameCount+", timeScale="+Time.timeScale+", cityEnabled="+(city!=null&&city.enabled)+", board="+(city!=null&&Get(city,"board")!=null)+", menu="+(app!=null&&app.TableMenuOpen)+", samePresentation="+(city!=null&&ReferenceEquals(city,Get(app,"presentation")));
        result.说明="Unity真实M6界面指定局面验收，局面仅测试注入；技能五项及支付选牌、用途、确认通过Unity原生GameView输入队列注入鼠标移动/按下/松手事件完成（编辑器输入模拟，非物理鼠标），断言读取权威结果。支付地图目标方式单独记录，允许显式参数启用夹具定位但绝不计作鼠标命中通过；不作为正常完整局、Android真机或用户试玩证据。"+(failure==null?"":"\n"+failure);
        result.场景=cases.ToArray();result.截图=images.ToArray();File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(result,true));
        SessionState.EraseString(Key);SessionState.EraseString(Deadline);Debug.Log("CATAN_B45_INTERACTION_"+(failure==null?"OK":"FAILED")+": "+result.说明);
        if(Environment.GetCommandLineArgs().Contains("--b45-keep-open"))
        {
            if(failure==null){Inject("tk-g15",new[]{2,1,0,0,0},new int[5]);Set(app,"inspectionText",null);}
            work=null;return;
        }
        EditorApplication.Exit(failure==null?0:1);
    }
}
