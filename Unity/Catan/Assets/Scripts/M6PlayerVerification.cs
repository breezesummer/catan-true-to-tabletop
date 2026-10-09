using System;
using System.Collections;
using System.IO;
using System.Linq;
using Catan.AI;
using Catan.Core;
using UnityEngine;

// Opt-in Windows acceptance. Test humans use the same shipped policy, then the normal UI Submit path.
// AI opponents run through M6App.StepAi; no authority injection or privileged policy inputs are used.
public sealed class M6PlayerVerification : MonoBehaviour
{
    M6App app;
    string stage="startup";
    readonly BaseGameAi basePolicy=new BaseGameAi();
    readonly SeafarersAi seaPolicy=new SeafarersAi();
    readonly CitiesKnightsAi cityPolicy=new CitiesKnightsAi();
    readonly CombinedAi combinedPolicy=new CombinedAi();
    public void Initialize(M6App value){app=value;StartCoroutine(Run());}
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    M2App Base=>GetComponent<M2App>();
    M3App Sea=>GetComponent<M3App>();
    M4App City=>GetComponent<M4App>();
    M5App Combined=>GetComponent<M5App>();
    int Turn=>app.RuleSet=="base"?Base.M6View.Turn:app.RuleSet=="seafarers"?Sea.M6View.Turn:app.RuleSet=="cities-knights"?City.M6View.Turn:Combined.M6View.Turn;
    string Phase=>app.RuleSet=="base"?Base.M6View.Phase.ToString():app.RuleSet=="seafarers"?Sea.M6View.Phase.ToString():app.RuleSet=="cities-knights"?City.M6View.Phase.ToString():Combined.M6View.Phase.ToString();
    string Authority()=>app.RuleSet=="base"?Base.VerificationHost.ExportSave():app.RuleSet=="seafarers"?Sea.VerificationHost.ExportSave():app.RuleSet=="cities-knights"?City.VerificationHost.ExportSave():Combined.VerificationHost.ExportSave();
    void CheckPrivacy()
    {
        string opponent=app.HumanSeat=="P1"?"P2":"P1";
        switch(app.RuleSet)
        {
            case "base":Base.SwitchSeat(opponent);Require(Base.VerificationSeat==app.HumanSeat&&Base.M6View.PlayerId==app.HumanSeat&&!Base.VerificationCurtain,"Base leaked AI seat");break;
            case "seafarers":Sea.SwitchSeat(opponent);Require(Sea.VerificationSeat==app.HumanSeat&&Sea.M6View.PlayerId==app.HumanSeat&&!Sea.VerificationCurtain,"Sea leaked AI seat");break;
            case "cities-knights":City.SwitchSeat(opponent);Require(City.VerificationSeat==app.HumanSeat&&City.M6View.PlayerId==app.HumanSeat&&!City.VerificationCurtain,"City leaked AI seat");break;
            default:Combined.SwitchSeat(opponent);Require(Combined.VerificationSeat==app.HumanSeat&&Combined.M6View.PlayerId==app.HumanSeat&&!Combined.VerificationCurtain,"Combined leaked AI seat");break;
        }
    }
    void CheckHumanBoundary()
    {
        string before=Authority(),wrong=app.HumanSeat=="P1"?"P2":"P1";
        switch(app.RuleSet)
        {
            case "base":Require(Base.Submit(new Catan.Core.M2.Command{PlayerId=wrong,Kind=Catan.Core.M2.CommandKind.EndTurn})==null,"Base accepted wrong UI seat");break;
            case "seafarers":Require(Sea.Submit(new Catan.Core.M3.Command{PlayerId=wrong,Kind=Catan.Core.M3.CommandKind.EndTurn})==null,"Sea accepted wrong UI seat");break;
            case "cities-knights":Require(City.Submit(new Catan.Core.M4.Command{PlayerId=wrong,Kind=Catan.Core.M4.CommandKind.EndTurn})==null,"City accepted wrong UI seat");break;
            default:Require(Combined.Submit(new Catan.Core.M5.Command{PlayerId=wrong,Kind=Catan.Core.M5.CommandKind.EndTurn})==null,"Combined accepted wrong UI seat");break;
        }
        Require(before==Authority(),"Wrong UI seat changed authority");
        app.SetPaused(true);Require(!app.StepAi()&&before==Authority(),"Pause advanced authority");
    }
    void CheckHumanWait()
    {
        Require(app.IsHumanTurn,"Expected human choice");string before=Authority();app.SetPaused(false);
        Require(!app.StepAi(),"AI consumed a human decision");app.SetPaused(true);Require(before==Authority(),"Human wait changed authority");
    }
    void Advance()
    {
        Require(!app.Finished,"Unexpected early game end");
        if(app.IsHumanTurn)
        {
            CheckHumanWait();
            switch(app.RuleSet)
            {
                case "base":var b=basePolicy.Decide(Base.M6View);b.Id=Guid.NewGuid().ToString("N");Require(Base.Submit(b)?.Success==true,"Human base command rejected");break;
                case "seafarers":var s=seaPolicy.Decide(Sea.M6View);s.Id=Guid.NewGuid().ToString("N");Require(Sea.Submit(s)?.Success==true,"Human sea command rejected");break;
                case "cities-knights":var c=cityPolicy.Decide(City.M6View);c.Id=Guid.NewGuid().ToString("N");Require(City.Submit(c)?.Success==true,"Human city command rejected");break;
                default:var x=combinedPolicy.Decide(Combined.M6View);x.Id=Guid.NewGuid().ToString("N");Require(Combined.Submit(x)?.Success==true,"Human combined command rejected");break;
            }
        }
        else
        {
            app.SetPaused(false);bool advanced=app.StepAi();app.SetPaused(true);Require(advanced&&app.Fault=="","Product AI failed");
        }
        CheckPrivacy();
    }
    IEnumerator Run()
    {
        var args=Environment.GetCommandLineArgs();int save=Array.IndexOf(args,"--m6-verify-save"),load=Array.IndexOf(args,"--m6-verify-load");
        if(save<0&&load<0)yield break;
        string directory=args[(save>=0?save:load)+1];Directory.CreateDirectory(directory);
        yield return null;
        var work=Verify(directory,save>=0);
        while(true)
        {
            object next;
            try{if(!work.MoveNext())break;next=work.Current;}
            catch(Exception ex){Debug.LogError("CATAN_M6_PLAYER_VERIFY_FAILED: stage="+stage+"; type="+ex.GetType().Name);Application.Quit(1);yield break;}
            yield return next;
        }
        Debug.Log(save>=0?"CATAN_M6_PLAYER_SAVE_OK: four rule families; mixed human/AI seats; pause; fixed private view; UI ownership boundary; human wait; AI trade response; versioned authority+seat saves":"CATAN_M6_PLAYER_LOAD_OK: independent process; four rule families; exact authority+seat roundtrip; continued product AI; human choice and trade restored");
        Application.Quit(0);
    }
    IEnumerator Verify(string directory,bool saving)
    {
        var rules=new[]{"base","seafarers","cities-knights","combined"};
        var scenarios=new[]{"heading-for-new-shores","new-world","heading-for-new-shores","through-the-desert"};
        var humans=new[]{"P2","P3","P1","P4"};
        for(int i=0;i<rules.Length;i++)
        {
            stage=rules[i]+"-initialize";string path=Path.Combine(directory,rules[i]+".json");
            if(saving)app.StartGame(rules[i],scenarios[i],i%2==0?3:4,humans[i],true);else app.ReadSave(path);
            // New and previous components are settled by Unity before the adapter is inspected.
            yield return null;yield return null;
            Require(app.Ready&&app.RuleSet==rules[i]&&app.HumanSeat==humans[i]&&app.Paused,"Configuration lost");
            if(!saving)Require(app.ExportEnvelope()==File.ReadAllText(path),"Process roundtrip differs");
            CheckPrivacy();CheckHumanBoundary();
            string beforeMenu=app.ExportEnvelope();app.ShowMenu();Require(!app.StepAi(),"Menu advanced AI");app.ResumeGame();Require(beforeMenu==app.ExportEnvelope(),"Menu altered configuration");
            int humanSteps=0,steps=0;bool humanChoiceSaved=false;
            stage=rules[i]+"-product-play";
            while(steps<240)
            {
                if(saving&&i==0&&!humanChoiceSaved&&app.IsHumanTurn)
                {app.WriteSave(Path.Combine(directory,"human-choice.json"));humanChoiceSaved=true;}
                if(app.IsHumanTurn)humanSteps++;
                Advance();steps++;
                if(steps%4==0)yield return null;
                if(humanSteps>=3&&app.AiCommandsExecuted>=4&&app.IsHumanTurn&&Turn>=3&&(Phase=="Action"||Phase=="ProductionAwaitRoll"))break;
            }
            Require(steps<240&&app.AiCommandsExecuted>0&&humanSteps>0,"Mixed seats did not progress");
            if(saving)app.WriteSave(path);
            Debug.Log("CATAN_M6_MIXED_SEATS_OK: "+rules[i]+"; players="+app.PlayerCount+"; human="+app.HumanSeat+"; AI commands="+app.AiCommandsExecuted+"; public turn="+Turn);
            if(saving&&i==0)
            {
                stage="base-trade";
                for(int step=0;step<240&&!(app.IsHumanTurn&&Phase=="Action"&&Base.M6View.TradeOffer==null&&Base.M6View.ActivePlayerId==app.HumanSeat&&Base.M6View.OwnResources.Total>0);step++){Advance();if(step%4==0)yield return null;}
                Require(app.IsHumanTurn&&Phase=="Action"&&Base.M6View.TradeOffer==null&&Base.M6View.ActivePlayerId==app.HumanSeat&&Base.M6View.OwnResources.Total>0,"No natural trade opportunity");
                var v=Base.M6View;Resource give=Enumerable.Range(0,5).Select(n=>(Resource)n).First(r=>v.OwnResources[r]>0);Resource receive=(Resource)(((int)give+1)%5);
                var trade=new Catan.Core.M2.Command{Id=Guid.NewGuid().ToString("N"),PlayerId=app.HumanSeat,Kind=Catan.Core.M2.CommandKind.ProposeTrade,OtherPlayerId=v.Players.First(p=>p.Id!=app.HumanSeat).Id,Give=new ResourceBag(),Receive=new ResourceBag()};
                trade.Give[give]=1;trade.Receive[receive]=1;Require(Base.Submit(trade)?.Success==true,"Normal human trade failed");
                Require(!app.IsHumanTurn&&Base.M6View.TradeOffer!=null,"Trade actor not routed to AI");app.WriteSave(Path.Combine(directory,"ai-trade.json"));
                CheckHumanBoundary();Advance();Require(Base.M6View.TradeOffer==null,"AI trade reply did not resolve");
            }
        }
        if(saving)
        {
            stage="failed-load-transaction";string before=app.ExportEnvelope();var corrupt=JsonUtility.FromJson<M6App.SavedGame>(before);corrupt.PlayerCount=corrupt.PlayerCount==3?4:3;
            string bad=Path.Combine(directory,"incompatible.json");File.WriteAllText(bad,JsonUtility.ToJson(corrupt));bool rejected=false;
            try{app.ReadSave(bad);}catch(Exception){rejected=true;}
            Require(rejected&&before==app.ExportEnvelope(),"Failed restore replaced running game");
        }
        else
        {
            stage="restore-human-choice";app.ReadSave(Path.Combine(directory,"human-choice.json"));yield return null;yield return null;
            Require(app.Paused&&app.IsHumanTurn,"Human pending seat lost");CheckHumanWait();Advance();
            stage="restore-ai-trade";app.ReadSave(Path.Combine(directory,"ai-trade.json"));yield return null;yield return null;
            Require(app.Paused&&!app.IsHumanTurn&&Base.M6View.TradeOffer!=null,"AI trade pending seat lost");Advance();Require(Base.M6View.TradeOffer==null,"Restored AI trade did not resolve");
        }
    }
}
