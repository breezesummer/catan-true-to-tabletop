using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

// Explicit development flag only; normal play never auto-controls the human.
public sealed class OperationUiVerification : MonoBehaviour
{
    M6App app;
    string mode;
    int checks;
    string reviewDirectory;
    int screenshotNumber;
    void Update()
    {
        if(reviewDirectory!=null&&Input.GetKeyDown(KeyCode.F8))
            ScreenCapture.CaptureScreenshot(Path.Combine(reviewDirectory,"interactive-"+(++screenshotNumber)+".png"));
    }
    OperationSnapshot Snapshot => mode=="base"?GetComponent<M2App>().VerificationOperationSnapshot:
        mode=="seafarers"?GetComponent<M3App>().VerificationOperationSnapshot:
        mode=="cities-knights"?GetComponent<M4App>().VerificationOperationSnapshot:GetComponent<M5App>().VerificationOperationSnapshot;
    OperationDock Dock => mode=="base"?GetComponent<M2App>().VerificationOperationDock:
        mode=="seafarers"?GetComponent<M3App>().VerificationOperationDock:
        mode=="cities-knights"?GetComponent<M4App>().VerificationOperationDock:GetComponent<M5App>().VerificationOperationDock;
    IEnumerator Start()
    {
        var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"--operation-ui-verify");
        int demo=Array.IndexOf(args,"--operation-ui-open");
        app=GetComponent<M6App>();
        if(demo>=0){reviewDirectory=Path.GetDirectoryName(Path.GetFullPath(args[demo+1]));yield return null;app.ReadSave(args[demo+1]);yield break;}
        if(flag<0)yield break;
        var run=Verify(args[flag+1]);
        while(true)
        {
            object next;
            try{if(!run.MoveNext())break;next=run.Current;}
            catch(Exception ex){Debug.LogError("CATAN_OPERATION_UI_FAILED: "+mode+" / "+ex);Application.Quit(1);yield break;}
            yield return next;
        }
        Debug.Log("CATAN_OPERATION_UI_OK: modes=4 checks="+checks);Application.Quit(0);
    }
    void Require(bool pass,string reason){checks++;if(!pass)throw new InvalidOperationException(reason);}
    IEnumerator Verify(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach(string rules in new[]{"base","seafarers","cities-knights","combined"})
        {
            mode=rules;StartVerificationGame();
            yield return null;yield return null;
            var initial=Snapshot;Require(initial!=null&&initial.Seat==app.HumanSeat,"Wrong private snapshot seat");
            Require(initial.Builds.Count>=4,"Inventory missing");
            Require(initial.GoodCount==(mode=="base"||mode=="seafarers"?5:8),"Goods mismatch");
            Require(initial.Builds.Count(b=>b.Track>=0)==(initial.GoodCount==8?3:0),"Facilities mismatch");
            CheckCachedAvailabilityAfterMenu();
            string before=app.ExportEnvelope();
            var build=initial.Builds.First();Dock.Selection.Sync(initial);Dock.Selection.Choose(build,initial.Goods);
            Require(before==app.ExportEnvelope(),"Selection mutated authority");Dock.Reset();
            Require(Dock.Selection.BuildId==null&&Dock.Selection.Selected.Sum()==0,"Cancel did not clear selection");
            int steps=0;
            while(steps++<900)
            {
                var s=Snapshot;
                if(app.IsHumanTurn&&s.CanTrade&&s.Goods.Sum()>=4)break;
                Advance();
                if(steps%12==0)yield return null;
            }
            Require(steps<900,"No normal action hand reached");
            var action=Snapshot;
            Require(action.CanInteract&&action.CanTrade,"Human action not enabled");
            Require(action.Rates.Take(action.GoodCount).All(n=>n>=2&&n<=4),"Trade rates invalid");
            foreach(var b in action.Builds)
            {
                Require(b.Cost.Length==8&&b.Cost.All(n=>n>=0),"Cost malformed");
                if(b.Available)Require(Enumerable.Range(0,8).All(i=>b.Cost[i]<=action.Goods[i]),"Unaffordable enabled build");
            }
            before=app.ExportEnvelope();
            var invalid=new OperationTrade{Bank=true};invalid.Give[0]=19;invalid.Receive[1]=1;
            Require(OperationSelection.ValidateTrade(action,invalid)!=null,"Invalid exchange accepted by draft validation");
            Require(before==app.ExportEnvelope(),"Trade draft changed authority");
            app.WriteSave(Path.Combine(directory,mode+"-review.json"));
            var choice=action.Builds.FirstOrDefault(b=>b.Cost.Sum()>0&&!b.Available)??action.Builds.First();
            Dock.Selection.Choose(choice,action.Goods);
            yield return null;yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,mode+"-ui.png"));yield return null;yield return null;
            Dock.Reset();
            if(action.OtherPlayers.Length>0)
            {
                int g=Array.FindIndex(action.Goods,n=>n>0),r=(g+1)%action.GoodCount;
                var offer=new OperationTrade{Bank=false,OtherPlayer=action.OtherPlayers[0]};offer.Give[g]=1;offer.Receive[r]=1;
                Require(OperationSelection.ValidateTrade(action,offer)==null,"Valid proposal draft rejected");
                Require(action.Trade(offer)==null,"Normal proposal submission failed");
                Require(Snapshot.Offer!=null&&Snapshot.Offer.CanCancel,"Proposer cannot retract own offer");
                Snapshot.Respond("cancel");Require(Snapshot.Offer==null,"Retract failed");
            }
            app.ShowMenu();Require(!app.StepAi(),"Menu advanced AI");app.ResumeGame();yield return null;
        }
        File.WriteAllText(Path.Combine(directory,"result.json"),"{\"status\":\"passed\",\"modes\":4,\"checks\":"+checks+",\"unity\":\""+Application.unityVersion+"\"}");
    }
    void StartVerificationGame()
    {
        // 正常开局会随机决定先手；验收选先手的建筑颜色，不能假定 P1 总是先行动。
        string authority,human;const uint seed=20261017;
        switch(mode)
        {
            case "base":
                var basic=Catan.Core.M2.BaseGameSession.Create(Resources.Load<TextAsset>("m1-scenario").text,3,seed);
                human=basic.GetPlayerView("P1").ActivePlayerId;authority=basic.Save();break;
            case "seafarers":
                var sailing=Catan.Core.M3.SeafarersGameSession.Create("heading-for-new-shores",3,seed);
                human=sailing.GetPlayerView("P1").ActivePlayerId;authority=sailing.Save();break;
            case "cities-knights":
                var cities=Catan.Core.M4.CitiesKnightsGameSession.Create(Resources.Load<TextAsset>("m1-scenario").text,3,seed);
                human=cities.GetPlayerView("P1").ActivePlayerId;authority=cities.Save();break;
            default:
                var combined=Catan.Core.M5.CombinedGameSession.Create("heading-for-new-shores",3,seed);
                human=combined.GetPlayerView("P1").ActivePlayerId;authority=combined.Save();break;
        }
        app.StartGame(mode,"heading-for-new-shores",3,human,true,authority);
    }
    void CheckCachedAvailabilityAfterMenu()
    {
        Require(app.IsHumanTurn,"缓存恢复验收必须从正常的人类先手回合开始");
        string before=app.ExportEnvelope();
        app.OpenTableMenu();
        // 强制在展示阻塞期间首次投影同一个规则视图，复现曾经缓存为不可用的情况。
        switch(mode)
        {
            case "base":GetComponent<M2App>().M7PreviewRefresh();break;
            case "seafarers":GetComponent<M3App>().M7PreviewRefresh();break;
            case "cities-knights":GetComponent<M4App>().M7PreviewRefresh();break;
            default:GetComponent<M5App>().M7PreviewRefresh();break;
        }
        var cached=Snapshot;
        var build=cached.Builds.FirstOrDefault(b=>b.Id=="SetupSettlement"&&b.Available);
        Require(build!=null,"展示阻塞被缓存进合法建造的可用性");
        Require(!build.CanPrepare(cached),"暂停菜单期间仍可准备建造");
        app.HandleTableEscape();
        Require(ReferenceEquals(cached,Snapshot),"关闭菜单不应依赖新规则视图");
        Require(build.CanPrepare(Snapshot),"关闭菜单后合法建造未恢复，仍依赖交易刷新");
        Require(before==app.ExportEnvelope(),"刷新建造可用性改变了权威状态");
    }
    void Advance()
    {
        if(!app.IsHumanTurn){app.SetPaused(false);bool ok=app.StepAi();app.SetPaused(true);Require(ok,"AI stopped");return;}
        bool result;
        switch(mode)
        {
            case "base":var b=GetComponent<M2App>();var bc=new Catan.AI.BaseGameAi().Decide(b.M6View);bc.Id=Guid.NewGuid().ToString("N");result=b.Submit(bc)?.Success==true;break;
            case "seafarers":var s=GetComponent<M3App>();var sc=new Catan.AI.SeafarersAi().Decide(s.M6View);sc.Id=Guid.NewGuid().ToString("N");result=s.Submit(sc)?.Success==true;break;
            case "cities-knights":var c=GetComponent<M4App>();var cc=new Catan.AI.CitiesKnightsAi().Decide(c.M6View);cc.Id=Guid.NewGuid().ToString("N");result=c.Submit(cc)?.Success==true;break;
            default:var x=GetComponent<M5App>();var xc=new Catan.AI.CombinedAi().Decide(x.M6View);xc.Id=Guid.NewGuid().ToString("N");result=x.Submit(xc)?.Success==true;break;
        }
        Require(result,"Human policy command rejected");
    }
}
