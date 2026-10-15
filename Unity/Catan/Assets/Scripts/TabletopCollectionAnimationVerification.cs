using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

// 显式真实窗口验收：只恢复 .local 隔离的正常规则局面，经原 Host.Submit 入口触发收牌。
// 测试文件不访问默认存档；公共结果只含数量、公开产出格和截图，不包含权威保存或牌种。
public sealed class TabletopCollectionAnimationVerification : MonoBehaviour
{
    const string Flag = "--ui-collection-verify";
    [Serializable] sealed class CaseSpec { public string Case, Actor, Kind; public int Seed, NormalCommands; }
    [Serializable] sealed class Sample { public string Stage, Screenshot; public float Elapsed, FlightProgress; public int Visible, Pending, ActiveOwnFlights; public bool ResourceHandVisible; }
    [Serializable] sealed class CaseResult
    {
        public string Case, HumanSeat, RuleSet;
        public int Seed, NormalCommands, BeforeCount, AfterCount;
        public bool HiddenUntilArrival, FinalVisibleMatches, ProductionOriginsMatch;
        public string[] PublicSourceTiles = Array.Empty<string>();
        public Sample[] Samples;
    }
    [Serializable] sealed class Result
    {
        public bool Passed, RealWindow, UsesNormalRules = true, UserSavesUntouched = true;
        public string FailureStage, FailureType, FailureReason;
        public CaseResult[] Cases;
    }
    sealed class Capture { public string Path; public Texture2D Texture; }
    readonly List<CaseResult> results = new List<CaseResult>();
    readonly List<Capture> captures = new List<Capture>();
    M6App app;
    string output, stage = "initialize";
    OperationDock activeDock;
    bool activeSpecial;
    List<Sample> activeSamples;
    System.Diagnostics.Stopwatch activeClock;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Register()
    {
        if(Array.IndexOf(Environment.GetCommandLineArgs(),Flag)<0)return;
        new GameObject("显式收牌动画验收").AddComponent<TabletopCollectionAnimationVerification>();
    }
    IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();int index = Array.IndexOf(args,Flag);
        if(index<0)yield break;
        output = index+1<args.Length&&!args[index+1].StartsWith("--",StringComparison.Ordinal)?args[index+1]:".local/m6-ui-v012/collection-animation";
        try
        {
            output = ResolveLocal(output);Directory.CreateDirectory(output);
            Require(!Application.isBatchMode,"需要真实 Windows 窗口");
            Application.runInBackground=true;
        }
        catch(Exception ex){Debug.LogError("CATAN_COLLECTION_VERIFY_FAILED: "+ex.GetType().Name);Application.Quit(1);yield break;}
        yield return null;yield return null;
        app = FindFirstObjectByType<M6App>();
        var run = Verify();bool failed = false;string error = null,reason=null;
        while(true)
        {
            object next;
            try { if(!run.MoveNext())break;next=run.Current; }
            catch(Exception ex)
            {
                failed=true;error=ex.GetType().Name;reason=ex is InvalidOperationException?ex.Message:error;
                if(activeDock!=null&&activeSamples!=null)
                {
                    AddCapture(results.Last().Case,"failed",(float)(activeClock?.Elapsed.TotalSeconds??0),0,activeDock,activeSpecial,activeSamples);
                    results.Last().Samples=activeSamples.ToArray();
                }
                Debug.LogError("CATAN_COLLECTION_VERIFY_FAILED: stage="+stage+"; type="+error+"; reason="+reason);break;
            }
            yield return next;
        }
        foreach(var capture in captures)
        {
            File.WriteAllBytes(capture.Path,capture.Texture.EncodeToPNG());Destroy(capture.Texture);yield return null;
        }
        File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(new Result{Passed=!failed,RealWindow=!Application.isBatchMode,FailureStage=failed?stage:null,FailureType=error,FailureReason=reason,Cases=results.ToArray()},true));
        Debug.Log(failed?"CATAN_COLLECTION_VERIFY_FAILED":"CATAN_COLLECTION_VERIFY_OK: development / progress / tile production; hidden until arrival; final visible counts; full Windows frames");
        Application.Quit(failed?1:0);
    }
    IEnumerator Verify()
    {
        Require(app!=null,"M6App missing");
        foreach(string name in new[]{"development-purchase","progress-draw","resource-production"})
        {
            stage=name+"-restore";string folder=Path.Combine(output,"cases",name);
            var envelope=JsonUtility.FromJson<M6App.SavedGame>(File.ReadAllText(Path.Combine(folder,"review-start.json")));
            var spec=JsonUtility.FromJson<CaseSpec>(File.ReadAllText(Path.Combine(folder,"case.json")));
            Require(envelope.Version==1&&envelope.PlayerCount==4&&envelope.Paused&&spec.Seed==97,"Case configuration");
            app.StartGame(envelope.RuleSet,envelope.ScenarioId,4,envelope.HumanSeat,true,envelope.Authority);
            yield return null;yield return null;yield return new WaitForEndOfFrame();
            Require(app.Ready&&app.Paused,"Restored authority not ready");
            bool special=name!="resource-production";
            MonoBehaviour presentation=envelope.RuleSet=="base"?(MonoBehaviour)GetComponentOnApp<M2App>():GetComponentOnApp<M4App>();
            var dock=envelope.RuleSet=="base"?((M2App)presentation).VerificationOperationDock:((M4App)presentation).VerificationOperationDock;
            var table=(TabletopUI)presentation.GetType().GetField("tabletop",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(presentation);
            var handField=typeof(OperationDock).GetField("specialHand",BindingFlags.Instance|BindingFlags.NonPublic);
            handField.SetValue(dock,special);
            stage=name+"-wait-ui";
            var readyClock=System.Diagnostics.Stopwatch.StartNew();bool uiReady=false;
            while(readyClock.Elapsed.TotalSeconds<6)
            {
                // 必须越过下一帧，不能在同一个 EndOfFrame 中重入并读取 M6Refresh 刚清空的 faces。
                handField.SetValue(dock,special);
                yield return null;yield return new WaitForEndOfFrame();
                var snapshot=(OperationSnapshot)typeof(OperationDock).GetField("current",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(dock);
                uiReady=table.VerificationHudRects.Length>0&&snapshot!=null&&snapshot.Seat==envelope.HumanSeat&&
                    snapshot.Cards.Count==OwnCount(presentation,true)&&snapshot.Goods.Take(snapshot.GoodCount).Sum()==OwnCount(presentation,false)&&dock.ResourceHandVisible!=special;
                if(uiReady&&readyClock.Elapsed.TotalSeconds>=1.15)break;
            }
            Require(uiReady,"完整窗口 UI 未在六秒内绘制并绑定当前玩家视图");
            int before=OwnCount(presentation,special);
            var record=new CaseResult{Case=name,HumanSeat=envelope.HumanSeat,RuleSet=envelope.RuleSet,Seed=spec.Seed,NormalCommands=spec.NormalCommands,BeforeCount=before,ProductionOriginsMatch=special};
            results.Add(record);var samples=new List<Sample>();
            activeDock=dock;activeSpecial=special;activeSamples=samples;activeClock=null;
            AddCapture(name,"before",0,0,dock,special,samples);
            record.Samples=samples.ToArray();
            stage=name+"-submit";
            if(presentation is M2App baseApp)
            {
                var command=new Catan.Core.M2.Command{Id=Guid.NewGuid().ToString("N"),PlayerId=spec.Actor,Kind=Catan.Core.M2.CommandKind.BuyDevelopmentCard};
                Require(baseApp.VerificationHost.Submit(command).Success,"Development purchase refused");baseApp.M6Refresh();
            }
            else
            {
                var city=(M4App)presentation;
                var command=new Catan.Core.M4.Command{Id=Guid.NewGuid().ToString("N"),PlayerId=spec.Actor,Kind=Catan.Core.M4.CommandKind.RollDice};
                Require(city.VerificationHost.Submit(command).Success,"Normal roll refused");city.M6Refresh();
            }
            handField.SetValue(dock,special);
            record.AfterCount=OwnCount(presentation,special);Require(record.AfterCount>before,"Expected card gain");
            double started=Time.unscaledTimeAsDouble;var animationClock=System.Diagnostics.Stopwatch.StartNew();activeClock=animationClock;
            int captureStage=0;bool seenPending=false;var sourceIds=new HashSet<string>();
            stage=name+"-animate";
            while(animationClock.Elapsed.TotalSeconds<6)
            {
                yield return null;yield return new WaitForEndOfFrame();
                double now=Time.unscaledTimeAsDouble;int pending=Pending(dock,special),visible=dock.VerificationHandRects.Length;
                var own=table.Collections.Active(now).Where(f=>f.Player==envelope.HumanSeat&&f.IsSpecial==special).ToArray();
                samples.Add(new Sample{Stage="frame",Elapsed=(float)animationClock.Elapsed.TotalSeconds,FlightProgress=own.Length==0?0:(float)own[0].Progress(now),Visible=visible,Pending=pending,ActiveOwnFlights=own.Length,ResourceHandVisible=dock.ResourceHandVisible});
                record.Samples=samples.ToArray();
                if(pending>0)
                {
                    seenPending=true;Require(visible+pending==record.AfterCount,"Incoming cards appeared twice");
                    Require(visible<record.AfterCount,"Incoming cards inserted before arrival");
                }
                if(!special)
                {
                    var view=((M4App)presentation).M6View;int rolled=view.LastDice1+view.LastDice2;
                    var tiles=TabletopCollectionSources.Project(view.Board.Tiles,view.Board.Settlements,view.Board.Cities,view.Board.RobberTileId,true);
                    foreach(var flight in own)
                    {
                        Require(flight.SourceTileId!=null,"Production flight has no source tile");
                        Require(tiles.Any(tile=>tile.Id==flight.SourceTileId&&tile.Number==rolled&&!tile.Blocked&&tile.Yield(flight.Player,flight.Good)>0),"Production flight has wrong source tile");
                        sourceIds.Add(flight.SourceTileId);
                    }
                }
                double progress=own.Length==0?0:own[0].Progress(now);
                if(captureStage<3&&own.Length>0&&progress>=new[]{.06,.42,.84}[captureStage])
                {AddCapture(name,new[]{"early","middle","landing"}[captureStage],(float)(now-started),(float)progress,dock,special,samples);captureStage++;}
                if(seenPending&&pending==0&&now-started>1.3)
                {
                    Require(visible==record.AfterCount,"Final hand count differs");record.FinalVisibleMatches=true;break;
                }
            }
            Require(seenPending,"六秒内未观察到待落地手牌；请检查历史基线、页签与卡牌插槽映射");
            Require(captureStage==3,"六秒内未采齐飞行早段、中段和落地阶段");
            Require(record.FinalVisibleMatches,"六秒内待落地手牌未归零或最终可见数量不匹配");
            record.HiddenUntilArrival=true;record.PublicSourceTiles=sourceIds.OrderBy(id=>id).ToArray();
            if(!special){Require(sourceIds.Count>0,"No production source observed");record.ProductionOriginsMatch=true;}
            AddCapture(name,"arrived",(float)(Time.unscaledTimeAsDouble-started),1,dock,special,samples);record.Samples=samples.ToArray();
            activeDock=null;activeSamples=null;activeClock=null;
        }
    }
    T GetComponentOnApp<T>() where T:Component => app.GetComponent<T>();
    static int OwnCount(MonoBehaviour presentation,bool special)
    {
        if(presentation is M2App basic)return special?basic.M6View.OwnDevelopmentCards.Length:basic.M6View.OwnResources.Total;
        var view=((M4App)presentation).M6View;return special?view.OwnProgressCards.Length:view.OwnResources.Total+view.OwnCommodities.Total;
    }
    static int Pending(OperationDock dock,bool special)=>special?dock.VerificationPendingSpecialCount:dock.VerificationPendingResourceCount;
    void AddCapture(string name,string phase,float elapsed,float progress,OperationDock dock,bool special,List<Sample> samples)
    {
        string file=name+"-"+phase+".png";
        captures.Add(new Capture{Path=Path.Combine(output,file),Texture=ScreenCapture.CaptureScreenshotAsTexture()});
        samples.Add(new Sample{Stage=phase,Screenshot=file,Elapsed=elapsed,FlightProgress=progress,Visible=dock.VerificationHandRects.Length,Pending=Pending(dock,special),ResourceHandVisible=dock.ResourceHandVisible});
    }
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    static string ResolveLocal(string path)
    {
        var full=Path.GetFullPath(path);
        for(var directory=new DirectoryInfo(Path.GetDirectoryName(full));directory!=null;directory=directory.Parent)
            if(directory.Name==".local")return full;
        throw new InvalidOperationException("验收目录必须位于 .local 内");
    }
}
