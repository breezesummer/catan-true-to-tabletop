using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

// 显式图形验收入口；正常新局、同一 AI 指令入口、独立 .local 输出，不访问用户存档。
public sealed class TabletopAiAnimationVerification:MonoBehaviour
{
    M6App app;
    string output,stage="初始化";
    readonly List<Capture> captures=new List<Capture>();
    readonly StringBuilder samples=new StringBuilder("规则,速度,阶段,相对时间,飞行数,隐藏目标渲染器,本回合高亮数,飞行X,飞行Y\n");
    [Serializable] sealed class Result{public string status,unity,note;public int modes,speeds,buildAnimations,screenshots;}
    sealed class Capture{public string Name;public Texture2D Image;}
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    IEnumerator Start()
    {
        var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"--ui-ai-verify");if(flag<0)yield break;
        if(flag+1>=args.Length||SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)
        {Debug.LogError("CATAN_UI_AI_VERIFY_FAILED: 需要图形设备和显式输出目录。");Application.Quit(1);yield break;}
        try
        {
            string root=Directory.GetCurrentDirectory();
            string local=Path.GetFullPath(Path.Combine(root,".local"))+Path.DirectorySeparatorChar;
            output=Path.GetFullPath(args[flag+1]);
            if(!output.StartsWith(local,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("验收输出须位于项目 .local 子目录。");
            Directory.CreateDirectory(output);
        }
        catch(Exception ex){Debug.LogError("CATAN_UI_AI_VERIFY_FAILED: "+ex.GetType().Name);Application.Quit(1);yield break;}
        app=GetComponent<M6App>();yield return null;
        var run=Verify();
        while(true)
        {
            object next;
            try{if(!run.MoveNext())break;next=run.Current;}
            catch(Exception ex)
            {
                // 失败时也保留公开采样，避免隐藏窗口帧率影响时只剩一条结论。
                File.WriteAllText(Path.Combine(output,"公开动画采样.csv"),samples.ToString());
                Debug.LogError("CATAN_UI_AI_VERIFY_FAILED: "+stage+" / "+(ex is InvalidOperationException?ex.Message:ex.GetType().Name));Application.Quit(1);yield break;
            }
            yield return next;
        }
        for(int i=0;i<captures.Count;i++)
        {
            File.WriteAllBytes(Path.Combine(output,captures[i].Name+".png"),captures[i].Image.EncodeToPNG());
            Destroy(captures[i].Image);yield return null;
        }
        File.WriteAllText(Path.Combine(output,"公开动画采样.csv"),samples.ToString());
        File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(new Result{status="passed",unity=Application.unityVersion,modes=4,speeds=3,buildAnimations=8,screenshots=captures.Count,note="实际图形窗口：三档速度、四规则AI头像飞出建造、落地前隐藏目标、落地后恢复、本回合发光、下一席位清除与动画不回写规则。完整用户对局和全分辨率手感仍待验。"},true));
        Debug.Log("CATAN_UI_AI_VERIFY_OK: 4 modes; 3 speeds; 8 public build flights; current-turn glow; no animation authority mutation.");
        Application.Quit(0);
    }
    IEnumerator Verify()
    {
        VerifyCacheChanges();
        yield return new WaitForEndOfFrame();CaptureImage("00-开局AI速度选项");
        string[] modes={"base","seafarers","cities-knights","combined"};int[] speeds={0,1,2,1};
        for(int i=0;i<modes.Length;i++)
        {
            string mode=modes[i];int speed=speeds[i];stage=mode+"-新局";
            AcceptanceStart(mode,out string authority,out string human);
            app.StartGame(mode,"heading-for-new-shores",4,human,true,authority,aiSpeedLevel:speed);
            double readyDeadline=Time.realtimeSinceStartupAsDouble+6;
            while(!app.Ready&&Time.realtimeSinceStartupAsDouble<readyDeadline)yield return null;
            yield return null;yield return new WaitForEndOfFrame();
            Require(app.Ready&&app.AiSpeedLevel==speed,"速度设置未传入新局");
            Require(app.VerificationBuildFlightCount==0&&app.VerificationBuildHighlightCount==0,"开局错误重放历史棋子");
            var saved=JsonUtility.FromJson<M6App.SavedGame>(app.ExportEnvelope());
            Require(saved.HasAiSpeed&&saved.AiSpeedLevel==speed,"速度设置没有写入存档信封");
            for(int action=0;action<2;action++)
            {
                stage=mode+"-建造"+action;Require(!app.IsHumanTurn,"验收应由AI执行初设建造");
                app.SetPaused(false);Require(app.StepAi(),"产品AI初设指令失败");app.SetPaused(true);app.ShowTablePrompt("");
                string before=app.ExportEnvelope();double started=Time.unscaledTimeAsDouble;
                yield return new WaitForEndOfFrame();Sample(mode,speed,"起飞",started);
                Require(app.VerificationBuildFlightCount>0,"公开建造没有产生飞行模型");
                Require(app.VerificationBuildHiddenRenderers>0,"飞行时目标模型提前出现");
                VerifyUnchangedRefresh();
                CaptureImage(mode+"-"+action+"-起飞");
                yield return new WaitForSecondsRealtime((float)TabletopAiSpeed.FlightDuration(speed)*.44f);
                yield return new WaitForEndOfFrame();Sample(mode,speed,"途中",started);CaptureImage(mode+"-"+action+"-途中");
                // 隐藏窗口可能极快或被系统限帧，帧数不等于动画经过的秒数。
                double landingStarted=Time.realtimeSinceStartupAsDouble,landingGameTime=Time.unscaledTimeAsDouble;
                double landingDeadline=landingStarted+6;
                while(app.VerificationBuildFlightCount>0&&Time.realtimeSinceStartupAsDouble<landingDeadline)yield return new WaitForSecondsRealtime(.01f);
                yield return new WaitForEndOfFrame();Sample(mode,speed,"落地",started);CaptureImage(mode+"-"+action+"-落地");
                Require(app.VerificationBuildFlightCount==0,"建造飞行动画未结束（真实等待 "+(Time.realtimeSinceStartupAsDouble-landingStarted).ToString("F3",CultureInfo.InvariantCulture)+" 秒，动画时间经过 "+(Time.unscaledTimeAsDouble-landingGameTime).ToString("F3",CultureInfo.InvariantCulture)+" 秒）");
                Require(app.ExportEnvelope()==before,"动画改变了权威局面或存档配置");
                Require(action!=0||app.VerificationBuildVisibleGlowCount>0,"本回合光环网格或着色器没有进入实际构建");
                Require(action==0?app.VerificationBuildHighlightCount>0:app.VerificationBuildHighlightCount==0,action==0?"本回合建筑没有发光高亮":"初设转交下一席位后高亮没有清除");
            }
            if(mode=="cities-knights")
            {
                var board=app.GetComponent<M4App>().VerificationBoard;board.ShowComponentSamples();
                var ids=board.CommittedPieces.Cast<Transform>().Select(t=>t.GetInstanceID()).ToArray();
                board.ShowComponentSamples();
                Require(ids.SequenceEqual(board.CommittedPieces.Cast<Transform>().Select(t=>t.GetInstanceID())),"城墙、骑士、大都会重复刷新没有复用");
            }
            if(mode=="combined")
            {
                var board=app.GetComponent<M5App>().VerificationBoard;board.ShowComponentSamples();
                var ids=board.CommittedPieces.Cast<Transform>().Select(t=>t.GetInstanceID()).ToArray();
                board.ShowComponentSamples();
                Require(ids.SequenceEqual(board.CommittedPieces.Cast<Transform>().Select(t=>t.GetInstanceID())),"双扩展棋子重复刷新没有复用");
            }
        }
    }
    void VerifyCacheChanges()
    {
        var root=new GameObject("增量缓存验收");
        var cache=new TabletopPieceCache();
        void Render(string group,string key,string appearance)
        {
            cache.Begin(group,root.transform);
            using(var change=cache.Update(key,appearance))
                if(change.Changed)
                    for(int i=0;i<2;i++)new GameObject(key+i).transform.SetParent(root.transform,false);
            cache.End();
        }
        Render("棋子","V1","骑士1未激活");var original=root.transform.GetChild(0).gameObject;
        Render("船","E1","P1");var ship=root.transform.GetChild(2).gameObject;
        Render("棋子","V1","骑士1未激活");
        Require(root.transform.childCount==4&&root.transform.GetChild(0).gameObject==original,"未变化的多根模型被重建");
        Render("棋子","V1","骑士1已激活");
        Require(!original.activeSelf&&original.transform.parent==null&&ship.activeSelf&&root.transform.childCount==4,"外观改变没有局部替换或误删船");
        cache.Begin("棋子",root.transform);cache.End();
        Require(root.transform.childCount==2&&root.transform.GetChild(0).gameObject==ship,"移除棋子破坏了其他分组");
        Render("船","E2","P1");
        Require(!ship.activeSelf&&root.transform.childCount==2,"移动后残留旧船");
        Destroy(root);
    }
    void VerifyUnchangedRefresh()
    {
        Transform root;Action refresh;
        if(app.GetComponent<M2App>() is M2App basic){root=basic.VerificationBoard.CommittedPieces;refresh=basic.M6Refresh;}
        else if(app.GetComponent<M3App>() is M3App sailing){root=sailing.VerificationBoard.CommittedPieces;refresh=sailing.M6Refresh;}
        else if(app.GetComponent<M4App>() is M4App cities){root=cities.VerificationBoard.CommittedPieces;refresh=cities.M6Refresh;}
        else {var combined=app.GetComponent<M5App>();root=combined.VerificationBoard.CommittedPieces;refresh=combined.M6Refresh;}
        var ids=root.Cast<Transform>().Select(t=>t.GetInstanceID()).ToArray();
        int hidden=app.VerificationBuildHiddenRenderers,flights=app.VerificationBuildFlightCount;
        for(int i=0;i<5;i++)refresh();
        Require(ids.SequenceEqual(root.Cast<Transform>().Select(t=>t.GetInstanceID())),"相同棋盘刷新重建了棋子");
        Require(hidden==app.VerificationBuildHiddenRenderers&&flights==app.VerificationBuildFlightCount,"重复刷新破坏了飞行或目标隐藏状态");
    }
    static void AcceptanceStart(string mode,out string authority,out string human)
    {
        const uint seed=97;string active;
        string topology=Resources.Load<TextAsset>("m1-scenario").text;
        switch(mode)
        {
            case "base":var basic=Catan.Core.M2.BaseGameSession.Create(topology,4,seed);authority=basic.Save();active=basic.GetPlayerView("P1").ActivePlayerId;break;
            case "seafarers":var sailing=Catan.Core.M3.SeafarersGameSession.Create("heading-for-new-shores",4,seed);authority=sailing.Save();active=sailing.GetPlayerView("P1").ActivePlayerId;break;
            case "cities-knights":var cities=Catan.Core.M4.CitiesKnightsGameSession.Create(topology,4,seed);authority=cities.Save();active=cities.GetPlayerView("P1").ActivePlayerId;break;
            default:var combined=Catan.Core.M5.CombinedGameSession.Create("heading-for-new-shores",4,seed);authority=combined.Save();active=combined.GetPlayerView("P1").ActivePlayerId;break;
        }
        // 仅根据公开先手选择相隔两个座位的人类；起手和下一席位均为真实产品 AI。
        human="P"+((int.Parse(active.Substring(1))+1)%4+1);
    }
    void CaptureImage(string name)=>captures.Add(new Capture{Name=name,Image=ScreenCapture.CaptureScreenshotAsTexture()});
    void Sample(string mode,int speed,string phase,double started)
    {
        Vector2 point=app.VerificationBuildFlightPosition;
        samples.Append(mode).Append(',').Append(speed).Append(',').Append(phase).Append(',').Append((Time.unscaledTimeAsDouble-started).ToString("F4",CultureInfo.InvariantCulture))
            .Append(',').Append(app.VerificationBuildFlightCount).Append(',').Append(app.VerificationBuildHiddenRenderers).Append(',').Append(app.VerificationBuildHighlightCount)
            .Append(',').Append(point.x.ToString("F2",CultureInfo.InvariantCulture)).Append(',').Append(point.y.ToString("F2",CultureInfo.InvariantCulture)).Append('\n');
    }
}
