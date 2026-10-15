using System;
using System.Collections;
using System.IO;
using Catan.AI;
using Catan.Network.Contracts;
using UnityEngine;

// Opt-in player test: automated human reads only its received view and uses the shipped Submit path.
public sealed class M7PlayerVerification : MonoBehaviour
{
    [Serializable] public sealed class Configuration
    {
        public string Endpoint,CertificateFingerprint,RuleSet="base",ScenarioId="heading-for-new-shores",OutputDirectory;
        public int PlayerCount=3,MinimumTurn=3,TimeoutSeconds=180;
    }
    [Serializable] sealed class Report
    {
        public string RuleSet,ScenarioId,Result,Stage,Transport;
        public int PlayerCount,Turn,HumanCommands,ClientAiCommands;
        public long Revision;
        public bool LocalAuthorityAbsent,LocalSaveRejected,ReconnectConfirmed,OwnSeatOnly;
    }
    M6App app;
    Configuration config;
    readonly Report report=new Report();
    float deadline,nextStatus;
    public void Initialize(M6App value){app=value;StartCoroutine(Run());}
    IEnumerator Run()
    {
        int index=Array.IndexOf(Environment.GetCommandLineArgs(),"--m7-verify");if(index<0)yield break;
        try
        {
            config=JsonUtility.FromJson<Configuration>(File.ReadAllText(Environment.GetCommandLineArgs()[index+1]));Directory.CreateDirectory(config.OutputDirectory);
            report.RuleSet=config.RuleSet;report.ScenarioId=config.ScenarioId;report.PlayerCount=config.PlayerCount;deadline=Time.realtimeSinceStartup+config.TimeoutSeconds;
            app.Network.UseCredentialDirectory(Path.Combine(config.OutputDirectory,"private-client"));app.Network.ConfigureEndpoint(config.Endpoint,config.CertificateFingerprint);
        }
        catch(Exception ex){Debug.LogError("CATAN_M7_VERIFY_FAILED: config "+ex.GetType().Name);Application.Quit(1);yield break;}
        var work=Verify();
        while(true)
        {
            bool more;
            try{more=work.MoveNext();}
            catch(Exception ex){report.Result="failed:"+ex.GetType().Name;Finish(false);yield break;}
            if(!more){report.Result="passed";Finish(true);yield break;}yield return work.Current;
        }
    }
    void Require(bool value,string stage){report.Stage=stage;if(!value)throw new InvalidOperationException(stage);}
    bool Waiting(bool condition)
    {
        if(Time.realtimeSinceStartup>=nextStatus){nextStatus=Time.realtimeSinceStartup+10;Debug.Log("CATAN_M7_VERIFY_WAIT "+report.Stage+" | "+app.Network.Status);}
        if(Time.realtimeSinceStartup>deadline)throw new TimeoutException(report.Stage);
        return !condition;
    }
    IEnumerator Verify()
    {
        var client=app.Network;report.Stage="create";
        client.Create(new RoomSettings{Name="M7独立客户端验证",Listed=false,RuleSet=config.RuleSet,ScenarioId=config.ScenarioId,PlayerCount=config.PlayerCount,AiSeats=config.PlayerCount==3?new[]{"P2","P3"}:new[]{"P2","P3","P4"}},"验证人类",null);
        while(Waiting(client.Room!=null&&!client.Busy))yield return null;
        report.Stage="ready";client.Ready(true);while(Waiting(!client.Busy))yield return null;
        report.Stage="start";client.StartRoom();while(Waiting(app.NetworkGame&&app.Ready))yield return null;
        Require(AuthorityAbsent(),"network presentation must not construct authority");report.LocalAuthorityAbsent=true;
        Require(!app.StepAi()&&app.AiCommandsExecuted==0,"network client must not run AI scheduler");
        bool rejected=false;try{app.ExportEnvelope();}catch(InvalidOperationException){rejected=true;}Require(rejected,"network client must not export authority");report.LocalSaveRejected=true;
        string seat=client.SeatId;long revision=client.Revision;report.Stage="reconnect";client.Reconnect();
        while(Waiting(!client.Busy&&client.Connected))yield return null;
        Require(client.SeatId==seat&&client.Revision>=revision,"reconnect original seat");report.ReconnectConfirmed=true;
        report.Stage="network turns";
        while(Turn()<config.MinimumTurn&&!app.Finished)
        {
            if(Waiting(client.Connected&&!client.CommandPending&&!client.Busy&&app.IsHumanTurn)){yield return null;continue;}
            long before=client.Revision;
            object command;
            switch(config.RuleSet)
            {
                case "base":command=new BaseGameAi().Decide(GetComponent<M2App>().M6View);break;
                case "seafarers":command=new SeafarersAi().Decide(GetComponent<M3App>().M6View);break;
                case "cities-knights":command=new CitiesKnightsAi().Decide(GetComponent<M4App>().M6View);break;
                default:command=new CombinedAi().Decide(GetComponent<M5App>().M6View);break;
            }
            report.Stage="legal preview";
            while(Waiting(client.Preview(command).Success))yield return null;
            switch(config.RuleSet)
            {
                case "base":GetComponent<M2App>().Submit((Catan.Core.M2.Command)command);break;
                case "seafarers":GetComponent<M3App>().Submit((Catan.Core.M3.Command)command);break;
                case "cities-knights":GetComponent<M4App>().Submit((Catan.Core.M4.Command)command);break;
                default:GetComponent<M5App>().Submit((Catan.Core.M5.Command)command);break;
            }
            report.Stage="network turns";
            report.HumanCommands++;
            while(Waiting(client.Revision>before&&!client.CommandPending))yield return null;
            Require(AuthorityAbsent()&&app.AiCommandsExecuted==0,"authority and AI remain server only");
        }
        report.Turn=Turn();report.Revision=client.Revision;report.ClientAiCommands=app.AiCommandsExecuted;report.Transport=client.TransportMode;
        report.OwnSeatOnly=OwnSeat()==client.SeatId;Require(report.OwnSeatOnly,"only own seat is presented");
        yield return null;
        ScreenCapture.CaptureScreenshot(Path.Combine(config.OutputDirectory,"client.png"));yield return null;
    }
    bool AuthorityAbsent()=>config.RuleSet=="base"?GetComponent<M2App>().VerificationHost==null:config.RuleSet=="seafarers"?GetComponent<M3App>().VerificationHost==null:config.RuleSet=="cities-knights"?GetComponent<M4App>().VerificationHost==null:GetComponent<M5App>().VerificationHost==null;
    int Turn()=>config.RuleSet=="base"?GetComponent<M2App>().M6View.Turn:config.RuleSet=="seafarers"?GetComponent<M3App>().M6View.Turn:config.RuleSet=="cities-knights"?GetComponent<M4App>().M6View.Turn:GetComponent<M5App>().M6View.Turn;
    string OwnSeat()=>config.RuleSet=="base"?GetComponent<M2App>().M6View.PlayerId:config.RuleSet=="seafarers"?GetComponent<M3App>().M6View.PlayerId:config.RuleSet=="cities-knights"?GetComponent<M4App>().M6View.PlayerId:GetComponent<M5App>().M6View.PlayerId;
    void Finish(bool success)
    {
        File.WriteAllText(Path.Combine(config.OutputDirectory,"summary.json"),JsonUtility.ToJson(report,true));
        Debug.Log(success?"CATAN_M7_UNITY_PLAYER_OK":"CATAN_M7_UNITY_PLAYER_FAILED: "+report.Stage);Application.Quit(success?0:1);
    }
}
