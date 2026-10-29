using System.Text.Json;
using Catan.AI;
using Catan.Core.ThreeKingdoms;
using Catan.Core.TradeModules;
using Catan.Network.Contracts;
using Catan.Rooms;
using Xunit;
using M2=Catan.Core.M2;
using M4=Catan.Core.M4;

namespace Catan.Network.Tests;

public sealed class FishingRoomTests(Xunit.Abstractions.ITestOutputHelper output)
{
    sealed class Storage:IRoomStorage
    {
        public readonly Dictionary<string,string> Documents=new();
        public IEnumerable<string> LoadAll()=>Documents.Values.ToArray();
        public void Commit(string id,string data)=>Documents[id]=data;
        public void Delete(string id)=>Documents.Remove(id);
        public void Dispose(){}
    }
    sealed class Fixture:IDisposable
    {
        public readonly Storage Store=new();public RoomDirectory Directory;
        public readonly string Rules;public readonly bool Kingdoms;public NetworkResponse[] Clients=[];
        public string Scenario=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"scenario.json"));
        public Fixture(string rules,bool kingdoms=false)
        {
            Rules=rules;Kingdoms=kingdoms;Directory=new(Store,Scenario);
            var create=Request("Create");create.Payload=WireJson.Serialize(new CreateRoomPayload{Settings=Settings()});var owner=Directory.Handle(create);Assert.True(owner.Success,owner.Message);
            var clients=new List<NetworkResponse>{owner};for(int i=1;i<4;i++){var req=Request("Join",owner);req.Payload=WireJson.Serialize(new JoinRoomPayload());var joined=Directory.Handle(req);Assert.True(joined.Success,joined.Message);clients.Add(joined);}Clients=clients.ToArray();
        }
        public RoomSettings Settings()=>new(){RuleSet=Rules,PlayerCount=4,ScenarioId="base",Fishing=true,Harbormaster=true,TradeModulesVersion=NetworkProtocol.TradeModulesVersion,
            ThreeKingdomsPackageVersion=Kingdoms?GeneralCatalog.B6PackageVersion:null,ThreeKingdomsContentHash=Kingdoms?GeneralCatalog.B6ContentHash:null};
        public NetworkRequest Request(string op,NetworkResponse? c=null)=>new(){Operation=op,RequestId=Guid.NewGuid().ToString("N"),RoomId=c?.RoomId,Credential=c?.Credential,ConnectionId=c?.ConnectionId,ExpectedRevision=c?.Revision??0,
            RuleSet=Rules,ExpectedRulesVersion=NetworkProtocol.RulesVersion(Rules),ExpectedScenarioVersion=NetworkProtocol.ScenarioVersion(Rules),SupportedTradeModulesVersion=NetworkProtocol.TradeModulesVersion,
            ExpectedThreeKingdomsPackageVersion=Kingdoms?GeneralCatalog.B6PackageVersion:null,ExpectedThreeKingdomsContentHash=Kingdoms?GeneralCatalog.B6ContentHash:null};
        // 使用产品 WebSocket 的 Sync 完整历史；Get 的64条滚动历史不是增量基准。
        public NetworkResponse Get(NetworkResponse c){var r=Directory.Handle(Request("Sync",c));Assert.True(r.Success,r.Message);r.Credential=c.Credential;return r;}
        public void Start(){foreach(var c in Clients){var req=Request("Ready",Get(c));req.Payload=WireJson.Serialize(new ReadyPayload{Ready=true});Assert.True(Directory.Handle(req).Success);}Assert.True(Directory.Handle(Request("Start",Get(Clients[0]))).Success);}
        public string Save=>JsonDocument.Parse(Store.Documents.Values.Single()).RootElement.GetProperty("GameSave").GetString()!;
        public void Dispose()=>Directory.Dispose();
    }
    [Fact]
    public void OldClientCannotCreateJoinReconnectOrPrepareEnabledRoom()
    {
        using var f=new Fixture("base");
        foreach(string op in new[]{"Join","Reconnect","Ready","Get"}){var req=f.Request(op,f.Clients[0]);req.SupportedTradeModulesVersion=null;req.Payload="{}";Assert.Equal("VersionMismatch",f.Directory.Handle(req).Code);}
        var create=f.Request("Create");create.SupportedTradeModulesVersion="old";create.Payload=WireJson.Serialize(new CreateRoomPayload{Settings=f.Settings()});Assert.Equal("VersionMismatch",f.Directory.Handle(create).Code);
        var invalid=f.Settings();invalid.RuleSet="combined";invalid.ScenarioId="heading-for-new-shores";create.SupportedTradeModulesVersion=NetworkProtocol.TradeModulesVersion;create.Payload=WireJson.Serialize(new CreateRoomPayload{Settings=invalid});Assert.Equal("InvalidRequest",f.Directory.Handle(create).Code);
    }
    [Fact]
    public void ConfigurationClearsReadyAndRestartPreservesModules()
    {
        using var f=new Fixture("base");var ready=f.Request("Ready",f.Get(f.Clients[1]));ready.Payload=WireJson.Serialize(new ReadyPayload{Ready=true});Assert.True(f.Directory.Handle(ready).Success);
        var setting=f.Settings();setting.Harbormaster=false;var req=f.Request("Configure",f.Get(f.Clients[0]));req.Payload=WireJson.Serialize(setting);Assert.True(f.Directory.Handle(req).Success);
        Assert.All(f.Get(f.Clients[0]).Room.Seats,s=>Assert.False(s.Ready));f.Start();
        var before=WireJson.Deserialize<M2.PlayerView>(f.Get(f.Clients[0]).ViewJson);Assert.False(before.TradeModules.Harbormaster);Assert.NotNull(before.TradeModules.Fishing);
        Assert.True(f.Directory.Handle(f.Request("Restart",f.Get(f.Clients[0]))).Success);
        var after=WireJson.Deserialize<M2.PlayerView>(f.Get(f.Clients[0]).ViewJson);Assert.NotNull(after.TradeModules.Fishing);Assert.False(after.TradeModules.Harbormaster);
    }
    [Theory][InlineData("base",false)][InlineData("cities-knights",false)][InlineData("cities-knights",true)]
    public void FourSeatCommandsPrivacyDeltaRetryAndServiceRecovery(string rules,bool kingdoms)
    {
        using var f=new Fixture(rules,kingdoms);f.Start();int spend=0,steps=0;long deltaBytes=0,fullBytes=0;double commandMs=0;
        while(spend<2&&steps++<1200)
        {
            var snapshots=f.Clients.Select(f.Get).ToArray();
            dynamic first=rules=="base"?(object)WireJson.Deserialize<M2.PlayerView>(snapshots[0].ViewJson):WireJson.Deserialize<M4.PlayerView>(snapshots[0].ViewJson);
            ThreeKingdomsView? tk=rules=="base"?null:first.ThreeKingdoms;
            string actor=tk?.WaitingSelectionPlayerIds?.FirstOrDefault()??tk?.PendingDecision?.PlayerId??tk?.SkillDecision?.PlayerId??(first.TradeOffer!=null?first.TradeOffer.NextResponder():first.Discards.Length>0?first.Discards[0].PlayerId:first.PendingDecision?.PlayerId??first.ActivePlayerId);
            actor=B2SkillEngine.ControllerForViewB6(tk,actor);var own=snapshots.Single(c=>c.SeatId==actor);
            dynamic view=rules=="base"?(object)WireJson.Deserialize<M2.PlayerView>(own.ViewJson):WireJson.Deserialize<M4.PlayerView>(own.ViewJson);
            dynamic command=rules=="base"?(object)new BaseGameAi().Decide(view):new CitiesKnightsAi().Decide(view);command.Id=Guid.NewGuid().ToString("N");
            var request=f.Request("Command",own);request.CommandId=command.Id;request.Payload=WireJson.SerializeObject(command);
            bool fish=command.Kind.ToString()=="SpendFish";
            if(fish)
            {
                var preview=f.Request("Preview",own);preview.CommandId=command.Id;preview.Payload=request.Payload;string saved=f.Save;Assert.True(f.Directory.Handle(preview).Success);Assert.Equal(saved,f.Save);
            }
            var started=System.Diagnostics.Stopwatch.GetTimestamp();var result=f.Directory.Handle(request);if(fish)commandMs+=System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;Assert.True(result.Success,$"{command.Kind}: {result.Code} {result.Message}");
            if(!fish)continue;spend++;string committed=f.Save;Assert.True(f.Directory.Handle(request).IsDuplicate);Assert.Equal(committed,f.Save);
            foreach(var old in snapshots)
            {
                var fresh=f.Get(f.Clients.Single(c=>c.SeatId==old.SeatId));
                Assert.DoesNotContain("\"Supply\"",fresh.ViewJson);Assert.DoesNotContain("\"Random\"",fresh.ViewJson);Assert.DoesNotContain("\"QueueIndex\"",fresh.ViewJson);
                using var doc=JsonDocument.Parse(fresh.ViewJson);var fishView=doc.RootElement.GetProperty("TradeModules").GetProperty("Fishing");
                Assert.All(fishView.GetProperty("Players").EnumerateArray(),p=>Assert.False(p.TryGetProperty("TokenIds",out _)));
                var sender=new SeatStateSender();var receiver=new SeatStateReceiver(old.RoomId,old.SeatId,old.ConnectionId);
                receiver.Prepare(sender.Encode(old)).Commit();var wire=sender.Encode(fresh);var applied=receiver.Prepare(wire);
                Type type=rules=="base"?typeof(M2.PlayerView):typeof(M4.PlayerView);
                Assert.Equal(WireJson.SerializeObject(WireJson.Deserialize(fresh.ViewJson,type)),WireJson.SerializeObject(WireJson.Deserialize(applied.Response.ViewJson,type)));applied.Commit();
                Assert.False(WireJson.Deserialize<SeatStatePacket>(wire.ViewJson).Full);
                deltaBytes+=System.Text.Encoding.UTF8.GetByteCount(WireJson.SerializeDirect(wire));fullBytes+=System.Text.Encoding.UTF8.GetByteCount(WireJson.SerializeDirect(fresh));
            }
            if(spend<2)continue;
            f.Directory.Dispose();f.Directory=new RoomDirectory(f.Store,f.Scenario);Assert.Equal(committed,f.Save);
            for(int i=0;i<f.Clients.Length;i++){var response=f.Directory.Handle(f.Request("Reconnect",f.Clients[i]));Assert.True(response.Success,response.Message);response.Credential=f.Clients[i].Credential;f.Clients[i]=response;}
        }
        Assert.Equal(2,spend);Assert.True(deltaBytes<fullBytes);
        output.WriteLine($"{rules}/三国{kingdoms}：2次鱼支付×4席；差量{deltaBytes}字节，完整视图及历史{fullBytes}字节；两次权威提交合计{commandMs:F1}ms。同进程测量，非外网延迟。");
    }
    [Fact]
    public async Task PendingReplacementReconnectsWithoutChangingTokensAndAiCanContinueAfterRecovery()
    {
        using var f=new Fixture("base");f.Start();NetworkResponse? pending=null;
        for(int step=0;step<1600;step++)
        {
            var snapshot=f.Get(f.Clients[0]);var first=WireJson.Deserialize<M2.PlayerView>(snapshot.ViewJson);
            if(first.Phase==M2.GamePhase.FishReplacement){pending=snapshot;break;}
            string actor=first.TradeOffer?.NextResponder()??(first.Discards.Length>0?first.Discards[0].PlayerId:first.PendingDecision?.PlayerId??first.ActivePlayerId);
            var own=f.Get(f.Clients.Single(c=>c.SeatId==actor));var view=WireJson.Deserialize<M2.PlayerView>(own.ViewJson);var command=new BaseGameAi().Decide(view);
            // 保留筹码以稳定触达7枚上限；其余选择使用产品AI。不是强度测试。
            if(command.Kind==M2.CommandKind.SpendFish||command.Kind==M2.CommandKind.PassOldBoot)
                command=new(){PlayerId=actor,Kind=view.Phase==M2.GamePhase.ProductionAwaitRoll?M2.CommandKind.RollDice:M2.CommandKind.EndTurn};
            command.Id=Guid.NewGuid().ToString("N");var req=f.Request("Command",own);req.CommandId=command.Id;req.Payload=WireJson.Serialize(command);
            var result=f.Directory.Handle(req);Assert.True(result.Success,result.Message);
        }
        Assert.NotNull(pending);var before=WireJson.Deserialize<M2.PlayerView>(pending.ViewJson);string player=before.PendingDecision.PlayerId;
        var client=f.Clients.Single(c=>c.SeatId==player);string saved=f.Save;
        var reconnect=f.Directory.Handle(f.Request("Reconnect",client));Assert.True(reconnect.Success);reconnect.Credential=client.Credential;Assert.Equal(saved,f.Save);
        var ownView=WireJson.Deserialize<M2.PlayerView>(reconnect.ViewJson);Assert.Equal(before.TradeModules.Fishing.PendingDecisionId,ownView.TradeModules.Fishing.PendingDecisionId);Assert.Equal(7,ownView.TradeModules.Fishing.OwnTokens.Length);
        var unauthorized=f.Request("Command",f.Get(f.Clients.First(c=>c.SeatId!=player)));var choice=new M2.Command{Id="wrong-fish-owner",PlayerId=player,Kind=M2.CommandKind.ResolveFishReplacement,DecisionId=ownView.TradeModules.Fishing.PendingDecisionId,ExpectedEventSequence=ownView.TradeModules.Fishing.StateSequence};unauthorized.CommandId=choice.Id;unauthorized.Payload=WireJson.Serialize(choice);Assert.False(f.Directory.Handle(unauthorized).Success);Assert.Equal(saved,f.Save);
        f.Directory.Dispose();f.Directory=new RoomDirectory(f.Store,f.Scenario);Assert.Equal(saved,f.Save);
        reconnect=f.Directory.Handle(f.Request("Reconnect",reconnect));Assert.True(reconnect.Success);reconnect.Credential=client.Credential;
        var recovered=WireJson.Deserialize<M2.PlayerView>(reconnect.ViewJson);Assert.Equal(JsonSerializer.Serialize(ownView.TradeModules.Fishing),JsonSerializer.Serialize(recovered.TradeModules.Fishing));
        Assert.Equal(1,await f.Directory.TickAiAsync());
        var continued=f.Get(reconnect);Assert.NotEqual(before.TradeModules.Fishing.PendingDecisionId,WireJson.Deserialize<M2.PlayerView>(continued.ViewJson).TradeModules.Fishing.PendingDecisionId);
    }

}
