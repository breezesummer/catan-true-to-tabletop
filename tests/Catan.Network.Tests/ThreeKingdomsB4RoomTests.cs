using Catan.AI;
using System.Text.Json;
using Catan.Core;
using Catan.Core.ThreeKingdoms;
using Catan.Network.Contracts;
using Catan.Rooms;
using Xunit;
using M4=Catan.Core.M4;
using M5=Catan.Core.M5;

namespace Catan.Network.Tests;

public sealed class ThreeKingdomsB4RoomTests
{
    [Theory]
    [InlineData("cities-knights",3)] [InlineData("cities-knights",4)]
    [InlineData("combined",3)] [InlineData("combined",4)]
    public void EverySeatHasPrivateStableDecisionAndOldRoomRevisionDoesNotInvalidateOtherChoices(string rules,int count)
    {
        using var f=new Fixture(rules,count);
        var initial=f.Clients.Select(f.Snapshot).ToArray();
        var views=initial.Select(f.Tk).ToArray();
        Assert.Equal(count,views.Length);
        Assert.Equal(count*4+1,views.Sum(v=>v.PendingDecision.CandidateIds.Length));
        Assert.Equal(count*4+1,views.SelectMany(v=>v.PendingDecision.CandidateIds).Distinct().Count());
        var commands=initial.Select(f.Decide).ToArray();
        for(int i=count-1;i>=0;i--)
        {
            var result=f.Submit(f.Clients[i],commands[i],initial[i].Revision);
            Assert.True(result.Success,result.Code+":"+result.Message);
            var retry=f.Submit(f.Clients[i],commands[i],initial[i].Revision);
            Assert.True(retry.Success);Assert.True(retry.IsDuplicate);Assert.Equal(result.Revision,retry.Revision);
            for(int j=0;j<i;j++)Assert.Equal(views[j].PendingDecision.CandidateIds,f.Tk(f.Snapshot(f.Clients[j])).PendingDecision.CandidateIds);
        }
        var deployed=f.Clients.Select(f.Snapshot).ToArray();
        for(int i=0;i<count;i++)Assert.True(f.Submit(f.Clients[i],f.Decide(deployed[i]),deployed[i].Revision).Success);
        Assert.All(f.Clients,c=>Assert.True(f.Tk(f.Snapshot(c)).OpeningComplete));
    }
    [Theory] [InlineData("cities-knights")] [InlineData("combined")]
    public void PartialSimultaneousSelectionSurvivesServiceRestartAndReconnectWithoutRevealingOthers(string rules)
    {
        using var f=new Fixture(rules,4);
        var owner=f.Clients[1];var before=f.Snapshot(owner);var command=f.Decide(before);
        Assert.True(f.Submit(owner,command,before.Revision).Success);
        var pending=f.Tk(f.Snapshot(owner)).PendingDecision;
        var unsubmitted=f.Tk(f.Snapshot(f.Clients[2])).PendingDecision;
        f.Restart();
        var restored=f.Reconnect(owner);
        Assert.True(restored.Success,restored.Message);Assert.True(restored.HumanControlAvailable);
        Assert.Equal(pending.CandidateIds,f.Tk(restored).PendingDecision.CandidateIds);
        Assert.Equal(pending.DecisionId,f.Tk(restored).PendingDecision.DecisionId);
        foreach(string hidden in unsubmitted.CandidateIds)Assert.DoesNotContain(hidden,restored.ViewJson);
        Assert.True(f.Submit(restored,f.Decide(restored),restored.Revision).Success);
    }
    [Fact]
    public void RetriedEnrollmentCannotChangeTheNegotiatedGeneralPackage()
    {
        using var f=new Fixture("combined",3);
        var request=f.JoinRequests[0];
        var exact=f.Directory.Handle(request);Assert.True(exact.Success);Assert.True(exact.IsDuplicate);
        request.ExpectedThreeKingdomsContentHash="changed";
        Assert.Equal("EnrollmentConflict",f.Directory.Handle(request).Code);
        request.ExpectedThreeKingdomsContentHash=null;request.ExpectedThreeKingdomsPackageVersion=null;
        Assert.Equal("EnrollmentConflict",f.Directory.Handle(request).Code);
    }
    [Fact]
    public void MissingOrWrongPackageHandshakeAndForgedSeatsAreRejected()
    {
        using var f=new Fixture("combined",3);
        var reconnect=f.Request("Reconnect",f.Clients[0]);reconnect.ExpectedThreeKingdomsContentHash="different";
        Assert.Equal("VersionMismatch",f.Directory.Handle(reconnect).Code);
        reconnect.ExpectedThreeKingdomsPackageVersion=null;reconnect.ExpectedThreeKingdomsContentHash=null;
        Assert.Equal("VersionMismatch",f.Directory.Handle(reconnect).Code);
        var own=f.Snapshot(f.Clients[0]);var other=f.Decide(f.Snapshot(f.Clients[1]));
        Assert.Equal("SeatForgery",f.Submit(f.Clients[0],other,own.Revision).Code);
        var actual=(M5.Command)f.Decide(own);actual.WindowId="过期待决";
        Assert.False(f.Submit(f.Clients[0],actual,own.Revision).Success);
    }
    [Theory] [InlineData("cities-knights")] [InlineData("combined")]
    public async Task AiCanCompleteItsIndependentChoicesWhileHumanHasNotSubmitted(string rules)
    {
        using var f=new Fixture(rules,4,["P2","P3","P4"]);
        var human=f.Tk(f.Snapshot(f.Clients[0])).PendingDecision;
        for(int i=0;i<6;i++)Assert.Equal(1,await f.Directory.TickAiAsync());
        var after=f.Tk(f.Snapshot(f.Clients[0]));
        Assert.Equal(human.DecisionId,after.PendingDecision.DecisionId);
        Assert.Equal(human.CandidateIds,after.PendingDecision.CandidateIds);
        Assert.Equal(["P1"],after.WaitingSelectionPlayerIds);
    }
    [Theory] [InlineData("cities-knights")] [InlineData("combined")]
    public void SkillDecisionIsPrivateResumesFromAuthorityAndCommandRetriesDoNotAddHistory(string rules)
    {
        using var f=new Fixture(rules,3);
        for(int step=0;step<350;step++)
        {
            var snapshots=f.Clients.Select(f.Snapshot).ToArray();
            var own=snapshots.FirstOrDefault(s=>f.Tk(s).PendingDecision?.PlayerId==s.SeatId||f.Tk(s).SkillDecision?.PlayerId==s.SeatId);
            if(own==null)
            {
                dynamic view=rules=="combined"?(object)WireJson.Deserialize<M5.PlayerView>(snapshots[0].ViewJson):WireJson.Deserialize<M4.PlayerView>(snapshots[0].ViewJson);
                string actor=view.Discards.Length>0?view.Discards[0].PlayerId:view.TradeOffer?.NextResponder()??view.PendingDecision?.PlayerId??view.ActivePlayerId;
                own=snapshots.Single(s=>s.SeatId==actor);
            }
            var command=f.Decide(own);
            if(f.Tk(own).SkillDecision!=null)
            {
                foreach(var observer in snapshots.Where(s=>s.SeatId!=own.SeatId))Assert.Empty(f.Tk(observer).SkillActions);
                var saved=f.GameSave;
                var result=f.Submit(own,command,own.Revision);Assert.True(result.Success,result.Message);
                var history=result.PresentationHistoryJson;
                var duplicate=f.Submit(own,command,own.Revision);Assert.True(duplicate.IsDuplicate);Assert.Equal(history,duplicate.PresentationHistoryJson);
                if(rules=="combined")
                {
                    var replay=M5.CombinedGameSession.Load(saved);Assert.True(replay.Execute((M5.Command)command).Success);Assert.Equal(replay.Save(),f.GameSave);
                }
                else
                {
                    var replay=M4.CitiesKnightsGameSession.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"scenario.json")),saved);Assert.True(replay.Execute((M4.Command)command).Success);Assert.Equal(replay.Save(),f.GameSave);
                }
                return;
            }
            var next=f.Submit(own,command,own.Revision);Assert.True(next.Success,$"步骤{step}：{next.Code} {next.Message}");
        }
        Assert.Fail("正常联机对局未到达技能待决。");
    }
    private sealed class MemoryStorage:IRoomStorage
    {
        public readonly Dictionary<string,string> Documents=new();
        public IEnumerable<string> LoadAll()=>Documents.Values.ToArray();
        public void Commit(string id,string data)=>Documents[id]=data;
        public void Delete(string id)=>Documents.Remove(id);
        public void Dispose(){}
    }
    internal sealed class Fixture:IDisposable
    {
        readonly MemoryStorage storage=new();readonly string rules;readonly string scenario=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"scenario.json"));
        readonly string packageVersion;readonly string packageHash;
        public RoomDirectory Directory;public NetworkResponse[] Clients;
        public readonly List<NetworkRequest> JoinRequests=new();
        public string GameSave=>JsonDocument.Parse(storage.Documents.Values.Single()).RootElement.GetProperty("GameSave").GetString()!;
        public Fixture(string rules,int count,string[]? ai=null,string? package=null)
        {
            packageVersion=package??GeneralCatalog.B4PackageVersion;
            packageHash=packageVersion==GeneralCatalog.B6PackageVersion?GeneralCatalog.B6ContentHash:packageVersion==GeneralCatalog.B5PackageVersion?GeneralCatalog.B5ContentHash:GeneralCatalog.B4ContentHash;
            this.rules=rules;Directory=new RoomDirectory(storage,scenario);
            var create=Request("Create",null);create.Payload=WireJson.Serialize(new CreateRoomPayload { Settings=new RoomSettings { RuleSet=rules,
                ScenarioId=rules=="combined"?"heading-for-new-shores":"base",PlayerCount=count,AiSeats=ai??[],
                ThreeKingdomsPackageVersion=packageVersion,ThreeKingdomsContentHash=packageHash }});
            var owner=Directory.Handle(create);Assert.True(owner.Success,owner.Code+":"+owner.Message);
            var clients=new List<NetworkResponse>{owner};
            for(int i=1;i<count-(ai?.Length??0);i++){var join=Request("Join",owner);join.Payload=WireJson.Serialize(new JoinRoomPayload());join.EnrollmentId=Guid.NewGuid().ToString("N");join.EnrollmentCredential=Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));JoinRequests.Add(join);var joined=Directory.Handle(join);Assert.True(joined.Success,joined.Message);joined.Credential=join.EnrollmentCredential;clients.Add(joined);}
            Clients=clients.ToArray();
            foreach(var client in Clients){var ready=Request("Ready",client);ready.ExpectedRevision=Snapshot(client).Revision;ready.Payload=WireJson.Serialize(new ReadyPayload {Ready=true});Assert.True(Directory.Handle(ready).Success);}
            var start=Request("Start",owner);start.ExpectedRevision=Snapshot(owner).Revision;var begun=Directory.Handle(start);Assert.True(begun.Success,begun.Code+":"+begun.Message);
        }
        public NetworkRequest Request(string operation,NetworkResponse? client)=>new(){Operation=operation,RoomId=client?.RoomId,Credential=client?.Credential,ConnectionId=client?.ConnectionId,
            RuleSet=rules,ExpectedRevision=client?.Revision??0,ExpectedRulesVersion=NetworkProtocol.RulesVersion(rules),ExpectedScenarioVersion=NetworkProtocol.ScenarioVersion(rules),
            ExpectedThreeKingdomsPackageVersion=packageVersion,ExpectedThreeKingdomsContentHash=packageHash};
        public NetworkResponse Snapshot(NetworkResponse client){var response=Directory.Handle(Request("Get",client));response.Credential=client.Credential;return response;}
        public ThreeKingdomsView Tk(NetworkResponse response)=>rules=="combined"?WireJson.Deserialize<M5.PlayerView>(response.ViewJson).ThreeKingdoms:WireJson.Deserialize<M4.PlayerView>(response.ViewJson).ThreeKingdoms;
        public object Decide(NetworkResponse response)
        {
            if(rules=="combined"){var command=new CombinedAi().Decide(WireJson.Deserialize<M5.PlayerView>(response.ViewJson));command.Id=Guid.NewGuid().ToString("N");return command;}
            else{var command=new CitiesKnightsAi().Decide(WireJson.Deserialize<M4.PlayerView>(response.ViewJson));command.Id=Guid.NewGuid().ToString("N");return command;}
        }
        public NetworkResponse Submit(NetworkResponse client,object command,long revision)
        {
            var request=Request("Command",client);request.ExpectedRevision=revision;request.Payload=WireJson.SerializeObject(command);
            request.CommandId=command is M4.Command city?city.Id:((M5.Command)command).Id;return Directory.Handle(request);
        }
        public void Restart(){Directory.Dispose();Directory=new RoomDirectory(storage,scenario);}
        public NetworkResponse Reconnect(NetworkResponse client){var response=Directory.Handle(Request("Reconnect",client));response.Credential=client.Credential;return response;}
        public void Dispose()=>Directory.Dispose();
    }
}
