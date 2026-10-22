using System;
using System.Collections;
using System.IO;
using System.Linq;
using Catan.AI;
using Catan.Core.M4;
using UnityEngine;

// 显式开发入口：由正常规则和自身 PlayerView 的 AI 推进，绝不读取或写入用户存档。
public sealed class TabletopV005Review : MonoBehaviour
{
    const string Flag="--tabletop-v005-review";
    const int CommandLimit=1500;
    const uint ReviewSeed=97;
    M6App app;
    M4App reviewPresentation;
    string output;
    int captureIndex;
    bool ready,capturing;
    bool sequenceArmed;
    long sequenceBaseline;
    int sequenceCapture;

    IEnumerator Start()
    {
        var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,Flag);
        if(index<0)yield break;
        if(Application.isBatchMode||index+1>=args.Length)
        {Debug.LogError("CATAN_V005_REVIEW_REFUSED: 请在实际窗口中指定 .local 验收目录。");yield break;}
        try{output=ReviewDirectory(args[index+1]);Directory.CreateDirectory(output);}
        catch(Exception exception){Debug.LogError("CATAN_V005_REVIEW_REFUSED: 验收目录无效（"+exception.GetType().Name+"）。");yield break;}
        app=GetComponent<M6App>();if(app==null)yield break;
        string reviewStart=Path.Combine(output,"review-start.json");
        if(File.Exists(reviewStart))
        {
            bool loaded=false;
            try
            {
                // 只接受显式 .local 验收目录内的固定文件，绝不回退到用户 SavePath。
                var envelope=JsonUtility.FromJson<M6App.SavedGame>(File.ReadAllText(reviewStart));
                if(envelope==null||envelope.Version!=1||envelope.RuleSet!="cities-knights"||envelope.PlayerCount!=4||!envelope.Paused)
                    throw new InvalidDataException("验收局配置不匹配。");
                app.ReadSave(reviewStart);loaded=true;
            }
            catch(Exception exception){Debug.LogError("CATAN_V005_REVIEW_FAILED: 验收缓存无法恢复（"+exception.GetType().Name+"）。");}
            if(loaded)yield return CompleteStart(reviewStart,false,true);
            yield break;
        }
        var topology=Resources.Load<TextAsset>("m1-scenario");
        if(topology==null){Debug.LogError("CATAN_V005_REVIEW_FAILED: 缺少测试拓扑。");yield break;}
        var session=CitiesKnightsGameSession.Create(topology.text,4,ReviewSeed);
        var policy=new CitiesKnightsAi();string bestSave=null,bestSeat=null;int bestScore=-1;
        bool desired=false,failed=false;
        for(int step=0;step<CommandLimit;step++)
        {
            try
            {
                var publicView=session.GetPlayerView("P1");if(publicView.Phase==GamePhase.Finished)break;
                string actor=Actor(publicView);var view=session.GetPlayerView(actor);
                if(view.Phase==GamePhase.Action&&view.PendingDecision==null&&view.TradeOffer==null&&view.OwnProgressCards.Length>0)
                {
                    int goods=view.OwnResources.Total+view.OwnCommodities.Total;
                    bool playable=view.LegalActions.Any(command=>command.Kind==CommandKind.PlayProgressCard);
                    int score=goods+(playable?100:0)+(view.Board.Knights.Any(k=>k.PlayerId==actor)?10:0);
                    if(score>bestScore){bestScore=score;bestSave=session.Save();bestSeat=actor;}
                    if(goods>=20&&playable){desired=true;break;}
                }
                var command=policy.Decide(view);command.Id="v005-review-"+step;
                if(command.PlayerId!=actor||!session.Execute(command).Success){failed=true;break;}
            }
            catch(Exception exception)
            {Debug.LogError("CATAN_V005_REVIEW_FAILED: 正常规则推进失败（"+exception.GetType().Name+"）。");failed=true;break;}
            // 每四条规则指令归还主线程，窗口保持响应；总指令数有明确上限。
            if(step%4==3)yield return null;
        }
        if(failed||bestSave==null)
        {Debug.LogError("CATAN_V005_REVIEW_FAILED: 未得到满足验收条件的正常对局。");yield break;}
        app.StartGame("cities-knights","heading-for-new-shores",4,bestSeat,true,bestSave);
        bestSave=null;
        yield return CompleteStart(reviewStart,desired,false);
    }

    IEnumerator CompleteStart(string reviewStart,bool desired,bool restored)
    {
        for(int frame=0;frame<120&&!app.Ready;frame++)yield return null;
        if(!app.Ready){Debug.LogError("CATAN_V005_REVIEW_FAILED: 游戏窗口初始化未完成。");yield break;}
        if(app.RuleSet!="cities-knights"||app.PlayerCount!=4||!app.Paused)
        {Debug.LogError("CATAN_V005_REVIEW_FAILED: 已恢复的验收局配置不匹配。");yield break;}
        reviewPresentation=GetComponent<M4App>();ready=reviewPresentation!=null;
        if(!ready)yield break;
        if(!restored&&!File.Exists(reviewStart))
        {
            try
            {
                // CreateNew 保证即使同时产生缓存，也不会覆盖原有验收局。
                using(var file=new FileStream(reviewStart,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                using(var writer=new StreamWriter(file))writer.Write(app.ExportEnvelope());
            }
            catch(IOException){Debug.LogWarning("CATAN_V005_REVIEW_CACHE_UNCHANGED: 保留原有缓存，当前验收局可继续使用。");}
        }
        app.ShowTablePrompt("开发验收局已就绪 · F8 保存完整界面截图");
        Debug.Log("CATAN_V005_REVIEW_READY: "+(restored?"已恢复同一开发验收局":"正常规则生成")+"；AI 暂停；F8 截图已启用。"+(desired?"已满足大量手牌验收目标。":""));
    }

    static string Actor(PlayerView view)
    {
        if(view.TradeOffer!=null)return view.TradeOffer.NextResponder(view.PlayerId);
        if(view.Discards.Length>0)return view.Discards[0].PlayerId;
        return view.PendingDecision!=null&&!string.IsNullOrEmpty(view.PendingDecision.PlayerId)?view.PendingDecision.PlayerId:view.ActivePlayerId;
    }

    void Update()
    {
        if(!ready||capturing||app==null||!app.Ready||app.RuleSet!="cities-knights"||reviewPresentation==null||GetComponent<M4App>()!=reviewPresentation)return;
        if(Input.GetKeyDown(KeyCode.F8))StartCoroutine(CaptureReview());
        if(Input.GetKeyDown(KeyCode.F9))
        {
            sequenceArmed=true;
            sequenceBaseline=app.HistoryEntries.LastOrDefault()?.Sequence??0;
            app.ShowTablePrompt("连续截图已就绪 · 下一次操作后记录收牌过程");
        }
        if(sequenceArmed&&(app.HistoryEntries.LastOrDefault()?.Sequence??0)>sequenceBaseline)
        {sequenceArmed=false;StartCoroutine(CaptureSequence());}
    }

    IEnumerator CaptureSequence()
    {
        capturing=true;
        string folder;
        do{folder=Path.Combine(output,"收牌连续帧-v007-"+(++sequenceCapture).ToString("D3"));}while(Directory.Exists(folder));
        Directory.CreateDirectory(folder);
        var rows=new System.Text.StringBuilder("帧号,时间秒,可见资源牌,待到手资源牌\n");
        var frames=new System.Collections.Generic.List<Texture2D>();
        double started=Time.unscaledTimeAsDouble;
        for(int frame=0;frame<48;frame++)
        {
            yield return new WaitForEndOfFrame();
            // 动画结束后再压缩写盘，避免 PNG 编码造成验收画面自身卡顿。
            frames.Add(ScreenCapture.CaptureScreenshotAsTexture());
            var dock=reviewPresentation.VerificationOperationDock;
            rows.Append(frame).Append(',').Append((Time.unscaledTimeAsDouble-started).ToString("F4",System.Globalization.CultureInfo.InvariantCulture))
                .Append(',').Append(dock.VerificationVisibleResourceCount).Append(',').Append(dock.VerificationPendingResourceCount).Append('\n');
            yield return null;yield return null;
        }
        for(int frame=0;frame<frames.Count;frame++)
        {
            var texture=frames[frame];
            File.WriteAllBytes(Path.Combine(folder,"帧-"+frame.ToString("D3")+".png"),texture.EncodeToPNG());
            Destroy(texture);yield return null;
        }
        File.WriteAllText(Path.Combine(folder,"显示计数.csv"),rows.ToString());
        capturing=false;
        app.ShowTablePrompt("收牌连续帧已保存");
        Debug.Log("CATAN_V007_SEQUENCE_CAPTURED: "+Path.GetFileName(folder));
    }

    IEnumerator CaptureReview()
    {
        capturing=true;string path;
        do{path=Path.Combine(output,"界面验收-v005-"+(++captureIndex).ToString("D3")+".png");}while(File.Exists(path));
        // 游戏自身的完整渲染帧，包含 IMGUI；不读取电脑桌面或其他应用。
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(path,1);
        bool saved=false;
        for(int frame=0;frame<300;frame++)
        {if(File.Exists(path)&&new FileInfo(path).Length>0){saved=true;break;}yield return null;}
        capturing=false;
        if(saved){app.ShowTablePrompt("已保存本次界面验收截图");Debug.Log("CATAN_V005_REVIEW_CAPTURED: "+Path.GetFileName(path));}
        else Debug.LogWarning("CATAN_V005_REVIEW_CAPTURE_PENDING: 截图尚未确认写入。");
    }

    static string ReviewDirectory(string supplied)
    {
        string root=FindProjectRoot(Application.dataPath)??FindProjectRoot(Directory.GetCurrentDirectory());
        if(root==null)throw new InvalidOperationException("未找到项目根目录。");
        string local=Path.GetFullPath(Path.Combine(root,".local"))+Path.DirectorySeparatorChar;
        string full=Path.GetFullPath(Path.IsPathRooted(supplied)?supplied:Path.Combine(root,supplied));
        if(!full.StartsWith(local,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("验收输出必须位于项目 .local 的子目录。");
        return full;
    }

    static string FindProjectRoot(string path)
    {
        for(var directory=new DirectoryInfo(path);directory!=null;directory=directory.Parent)
            if(File.Exists(Path.Combine(directory.FullName,"docs","PROJECT_BRIEF.md"))&&Directory.Exists(Path.Combine(directory.FullName,"Unity","Catan")))return directory.FullName;
        return null;
    }
}
