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
        public int PlayerCount=3,MinimumTurn=3,TimeoutSeconds=180,DiscoveryPort=47772;
        public bool SkipLanDiscovery;
    }
    [Serializable] sealed class Report
    {
        public string RuleSet,ScenarioId,Result,Stage,Transport;
        public int PlayerCount,Turn,HumanCommands,ClientAiCommands;
        public long Revision;
        public bool LocalAuthorityAbsent,LocalSaveRejected,ReconnectConfirmed,OwnSeatOnly,RoomSwitchConfirmed,OriginalRoomRestored,LanDiscoveryConfirmed,LanDiscoverySkipped,NameUpdateConfirmed;
    }
    M6App app;
    Configuration config;
    readonly Report report=new Report();
    float deadline,nextStatus;
    public void Initialize(M6App value){app=value;StartCoroutine(Run());}
    IEnumerator Run()
    {
        var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--m7-verify");
        string configuration=index>=0&&index+1<args.Length?args[index+1]:null;
#if UNITY_ANDROID && !UNITY_EDITOR
        if(configuration==null)
            using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
            using(var intent=activity.Call<AndroidJavaObject>("getIntent"))
            {
                configuration=intent.Call<string>("getStringExtra","catan_m7_verify");
                if(configuration!=null)intent.Call("removeExtra","catan_m7_verify");
            }
#endif
        if(string.IsNullOrEmpty(configuration))yield break;
        try
        {
            config=JsonUtility.FromJson<Configuration>(File.ReadAllText(configuration));Directory.CreateDirectory(config.OutputDirectory);app.Network.EnableVerification();
            report.RuleSet=config.RuleSet;report.ScenarioId=config.ScenarioId;report.PlayerCount=config.PlayerCount;deadline=Time.realtimeSinceStartup+config.TimeoutSeconds;
            app.Network.UseCredentialDirectory(Path.Combine(config.OutputDirectory,"private-client"));app.Network.ConfigureEndpoint(config.Endpoint,config.CertificateFingerprint);
        }
        catch(Exception ex){Debug.LogError("CATAN_M7_VERIFY_FAILED: config "+ex.GetType().Name);Application.Quit(1);yield break;}
        var work=Verify();
        while(true)
        {
            bool more;
            try{more=work.MoveNext();}
            catch(Exception ex){Debug.LogError("CATAN_M7_VERIFICATION_EXCEPTION: "+ex.GetType().Name+" "+ex.StackTrace);report.Result="failed:"+ex.GetType().Name;Finish(false);yield break;}
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
        string originalRoom=client.RoomId,originalSeat=client.SeatId;
        report.Stage="direct room switch";
        client.Create(new RoomSettings{Name="切房恢复验证",Listed=true,RuleSet=config.RuleSet,ScenarioId=config.ScenarioId,PlayerCount=config.PlayerCount,AiSeats=config.PlayerCount==3?new[]{"P2","P3"}:new[]{"P2","P3","P4"}},"切房验证玩家",null);
        while(Waiting(!client.Busy&&client.Room!=null&&client.RoomId!=originalRoom))yield return null;
        string secondRoom=client.RoomId;report.RoomSwitchConfirmed=true;
        if(!config.SkipLanDiscovery)
        {
            report.Stage="LAN room discovery";var discovery=new M7LanService().Discover(config.DiscoveryPort,client.RememberedRoomIds);
            while(Waiting(discovery.IsCompleted))yield return null;
            Require(!discovery.IsFaulted&&Array.Exists(discovery.Result,s=>Array.Exists(s.Rooms,r=>r.RoomId==secondRoom)),"automatic LAN discovery returns live room");report.LanDiscoveryConfirmed=true;
            Require(Array.Exists(discovery.Result,s=>Array.Exists(s.Rooms,r=>r.RoomId==originalRoom)),"own offline room remains recoverable without public history rooms");
        }
        else report.LanDiscoverySkipped=true;
        report.Stage="restore first room after switch";
        // 相同证书和房间在不同主机地址下仍须找到同一份恢复身份。
        client.ConfigureEndpoint(config.Endpoint.Replace("127.0.0.1","localhost"),config.CertificateFingerprint,false);
        client.Join(originalRoom,"验证人类",null);
        while(Waiting(!client.Busy&&client.Connected&&app.NetworkGame&&app.Ready&&client.RoomId==originalRoom&&client.Room!=null&&client.Room.RoomId==originalRoom))yield return null;
        Require(client.SeatId==originalSeat&&OwnSeat()==originalSeat&&AuthorityAbsent(),"per-room credential restores original seat");
        report.OriginalRoomRestored=true;
        report.Stage="shared player name update";client.SetDisplayName("重连后的新名字");
        while(Waiting(Array.Exists(client.Room.Seats,s=>s.SeatId==originalSeat&&s.DisplayName=="重连后的新名字")))yield return null;
        report.NameUpdateConfirmed=true;
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
