using System.Reflection;
using Catan.Core;
using Catan.Core.ThreeKingdoms;
using M4 = Catan.Core.M4;
using M5 = Catan.Core.M5;
using Xunit;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB3ReviewTests
{
    private static ThreeKingdomsState Generals() => (ThreeKingdomsState)typeof(ThreeKingdomsB3ImmunityTests)
        .GetMethod("Generals", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;

    [Fact]
    public void ImmunizedDeferredDiscardCannotCreateInterceptableCardsFromRequestedSelection()
    {
        var game = M4CoreHarness.Fixture(s => {
            s.RulesVersion = M4.CitiesKnightsGameSession.ThreeKingdomsB3RulesVersion;
            s.ThreeKingdoms = Generals();
            var current = s.ThreeKingdoms.Generals.Single(g => g.Region == GeneralRegion.Active && g.OwnerPlayerId == "P3");
            var wanted = s.ThreeKingdoms.Generals.Single(g => g.GeneralId == "tk-g30");
            (current.GeneralId, wanted.GeneralId) = (wanted.GeneralId, current.GeneralId);
            (current.InstanceId, wanted.InstanceId) = (wanted.InstanceId, current.InstanceId);
            s.Phase = M4.GamePhase.PendingChoice;
            s.PendingDecision = new M4.PendingDecision { Kind = "ProgressSabotage", PlayerId = "P2", OtherPlayerId = "P1", Amount = 1 };
        });
        var command = new M4.Command { Id = ThreeKingdomsTestGame.NextId(), PlayerId = "P2", Kind = M4.CommandKind.ResolveProgressChoice,
            Resources = new ResourceBag { Wood = 1 }, WindowId = game.GetPlayerView("P2").ThreeKingdoms.WindowId };
        var result = game.Execute(command); Assert.True(result.Success, result.ErrorCode + ":" + result.Message);
        var state = game.GetAuthoritativeStateForTesting();
        Assert.Null(state.ThreeKingdoms.Skills.PendingDecision);
        Assert.Equal(19, state.Bank.Wood);
        Assert.All(state.Players, p => Assert.Equal(0, p.Resources.Wood));
        game.AssertInvariants();
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void FangzhuKongchengOnlyPreventsTurningFaceDownAndAllowsTurningFaceUp(bool initiallyFaceDown)
    {
        var host = ThreeKingdomsB2TestHost.Create("tk-g29", "tk-g19", "tk-g22");
        host.State.RulesVersion = GeneralCatalog.B3RulesVersion;
        host.State.PackageVersion = GeneralCatalog.B3PackageVersion; host.State.PackageContentHash = GeneralCatalog.B3ContentHash;
        host.State.Players.Single(p => p.PlayerId == "P2").FaceDown = initiallyFaceDown;
        var action = B2SkillEngine.Actions(host, "P1").Single(a => a.Command.Option == "fangzhu" && a.Command.TargetPlayerIds.SequenceEqual(new[] { "P2", "P3" }));
        B2SkillEngine.Resolve(host, "P1", action.Command);
        Assert.False(host.State.Players.Single(p => p.PlayerId == "P2").FaceDown);
        Assert.True(host.State.Players.Single(p => p.PlayerId == "P3").FaceDown);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void PirateLootImmediateOrDelayedByNativeDecisionOffersLuoyingExactlyOnce(bool delayed)
    {
        var game = M5.CombinedGameSession.Create("pirate-islands", 4, 42);
        var state = game.GetAuthoritativeStateForTesting();
        var map = M5.CombinedScenarios.Create("pirate-islands", 4);
        state.RulesVersion = M5.CombinedGameSession.ThreeKingdomsB3RulesVersion; state.ThreeKingdoms = Generals();
        var current = state.ThreeKingdoms.Generals.Single(g => g.Region == GeneralRegion.Active && g.OwnerPlayerId == "P3");
        var wanted = state.ThreeKingdoms.Generals.Single(g => g.GeneralId == "tk-g30");
        (current.GeneralId, wanted.GeneralId) = (wanted.GeneralId, current.GeneralId); (current.InstanceId, wanted.InstanceId) = (wanted.InstanceId, current.InstanceId);
        state.ActivePlayerId = "P1"; state.Turn = 9; state.SetupPlacementIndex = 8;
        var target = state.Settlements.Single(p => p.PlayerId == "P2");
        int targetIndex = Array.FindIndex(map.PiratePath, id => state.Tiles.Single(t => t.Id == id).Vertices.Contains(target.LocationId));
        Assert.True(targetIndex >= 0);
        state.PirateTileId = map.PiratePath[(targetIndex + map.PiratePath.Length - 1) % map.PiratePath.Length];
        state.FirstBarbarianAttack = true; state.LastDice1 = 1; state.LastDice2 = 1;
        M5Harness.Player(state, "P2").Resources.Wood = 1;
        if(delayed) {
            state.Phase = M5.GamePhase.PendingChoice; state.ProductionPending = true; state.DecisionReturnPhase = M5.GamePhase.Action;
            state.PendingDecision = new M5.PendingDecision { Kind = "DefenderProgress", PlayerId = "P1" };
        } else {
            state.ThreeKingdoms.Clock.Phase=ThreeKingdomsPhase.Roll; state.Phase=M5.GamePhase.ProductionAwaitRoll;
            state.ThreeKingdoms.Dice=new B3DiceState { OpenedTurnId=state.ThreeKingdoms.Clock.TurnInstanceId,
                FixedResult=new B3DiceResult { PlayerId="P1", Purpose=B3DicePurpose.Formal, Red=1, White=6, EventFace=4 } };
        }
        M5Harness.Balance(state);
        foreach(var p in state.Players)p.Pieces.Settlements-=state.Special.Fortresses.Count(f=>f.PlayerId==p.Id&&f.Strength>0);
        typeof(M5.CombinedGameSession).GetField("state",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(game,state);
        M5Harness.Invoke(game,"RecalculateAwards",state); game.AssertInvariants();
        var command = new M5.Command { Id = ThreeKingdomsTestGame.NextId(), PlayerId = "P1", Kind = delayed?M5.CommandKind.ResolveChoice:M5.CommandKind.RollDice,
            Track = M5.ImprovementTrack.Science, WindowId = state.ThreeKingdoms.WindowId };
        var result = game.Execute(command); Assert.True(result.Success, result.ErrorCode + ":" + result.Message);
        var after = game.GetAuthoritativeStateForTesting();
        Assert.Equal(0, M5Harness.Player(after, "P2").Resources.Wood);
        Assert.NotNull(after.ThreeKingdoms.Skills.PendingDecision);
        Assert.Equal("luoying", after.ThreeKingdoms.Skills.PendingDecision.Kind);
        Assert.Equal("P3", after.ThreeKingdoms.Skills.PendingDecision.PlayerId);
        var action=game.GetPlayerView("P3").ThreeKingdoms.SkillActions.Single(a=>a.Command.Option=="luoying"&&a.Command.Counts[0]==1);
        var take=new M5.Command {Id=ThreeKingdomsTestGame.NextId(),PlayerId="P3",Kind=M5.CommandKind.UseGeneralSkill,WindowId=after.ThreeKingdoms.WindowId,SkillCommand=action.Command};
        Assert.True(game.Execute(take).Success); after=game.GetAuthoritativeStateForTesting();
        Assert.Equal(1,M5Harness.Player(after,"P3").Resources.Wood); Assert.Equal(18,after.Bank.Wood);
        Assert.Null(after.ThreeKingdoms.Skills.PendingDecision);
        if(!delayed) { Assert.Equal(M5.GamePhase.RobberSteal,after.Phase); Assert.Equal("any",after.PendingDecision.Token); }
        game.AssertInvariants();
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void FortressCounterattackRespectsEmptyHandImmunityWithoutChangingTheAttackRoll(bool holdsCard)
    {
        var game=M5.CombinedGameSession.Create("pirate-islands",4,42);
        var state=game.GetAuthoritativeStateForTesting(); state.RulesVersion=M5.CombinedGameSession.ThreeKingdomsB3RulesVersion; state.ThreeKingdoms=Generals();
        var owner=M5Harness.Player(state,"P2");
        if(holdsCard) { owner.Resources.Wood=1;state.Bank.Wood--; }
        int before=state.Ships.Count(p=>p.PlayerId=="P2"), supply=owner.ShipsRemaining;
        long draws=state.Random.Draws;int fortress=state.Special.Fortresses.Single(f=>f.PlayerId=="P2").Strength;
        // 初始航线只有普通船，战舰强度0；任意真实骰点都必然触发反击损船分支。
        M5Harness.Invoke(game,"AttackFortress",state,owner);
        Assert.Equal(draws+1,state.Random.Draws);Assert.InRange(state.Special.LastPirateStrength,1,6);
        Assert.Equal(fortress,state.Special.Fortresses.Single(f=>f.PlayerId=="P2").Strength);
        int loss=holdsCard?Math.Min(2,before):0;
        Assert.Equal(before-loss,state.Ships.Count(p=>p.PlayerId=="P2"));Assert.Equal(supply+loss,owner.ShipsRemaining);
    }

    [Fact]
    public void PirateDiscardReturnedAfterLuoyingDeclineSuppliesTheFollowingProduction()
    {
        var game=M5.CombinedGameSession.Create("pirate-islands",4,42);
        var state=game.GetAuthoritativeStateForTesting(); var map=M5.CombinedScenarios.Create("pirate-islands",4);
        state.RulesVersion=M5.CombinedGameSession.ThreeKingdomsB3RulesVersion;state.ThreeKingdoms=Generals();
        var active=state.ThreeKingdoms.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P3");
        var caozhi=state.ThreeKingdoms.Generals.Single(g=>g.GeneralId=="tk-g30");
        (active.GeneralId,caozhi.GeneralId)=(caozhi.GeneralId,active.GeneralId);(active.InstanceId,caozhi.InstanceId)=(caozhi.InstanceId,active.InstanceId);
        // 唯一的 2 点木地块只给新村庄产一张木，银行起初零木；海盗弃木必须先完成退牌再建立产出。
        var site=state.Tiles.Where(t=>t.Resource=="wood").SelectMany(t=>t.Vertices.Select(v=>new {Tile=t,Vertex=v}))
            .First(x=>!state.Settlements.Any(b=>x.Tile.Vertices.Contains(b.LocationId))&&
                !state.Settlements.Any(b=>b.LocationId==x.Vertex||map.Edges.Any(e=>e.Vertices.Contains(x.Vertex)&&e.Vertices.Contains(b.LocationId)))&&
                !state.Special.Fortresses.Any(f=>f.VertexId==x.Vertex));
        var two=state.Tiles.Single(t=>t.Number==2);(two.Number,site.Tile.Number)=(site.Tile.Number,two.Number);
        state.Settlements=state.Settlements.Append(M5Harness.Piece(site.Vertex,"P1")).ToArray();
        var victim=state.Settlements.Single(p=>p.PlayerId=="P2");
        int targetIndex=Array.FindIndex(map.PiratePath,id=>state.Tiles.Single(t=>t.Id==id).Vertices.Contains(victim.LocationId));
        Assert.True(targetIndex>=0);
        state.PirateTileId=map.PiratePath[(targetIndex+map.PiratePath.Length-1)%map.PiratePath.Length];
        state.FirstBarbarianAttack=true;state.ActivePlayerId="P1";state.Turn=9;state.SetupPlacementIndex=8;
        M5Harness.Player(state,"P2").Resources.Wood=1;M5Harness.Player(state,"P4").Resources.Wood=18;
        state.ThreeKingdoms.Clock.Phase=ThreeKingdomsPhase.Roll;state.ThreeKingdoms.Clock.DrawPhaseTurnInstanceId=0;state.Phase=M5.GamePhase.ProductionAwaitRoll;
        state.AlchemyDice1=1;state.AlchemyDice2=1;
        state.ThreeKingdoms.Dice=new B3DiceState {OpenedTurnId=state.ThreeKingdoms.Clock.TurnInstanceId};
        M5Harness.Balance(state);foreach(var p in state.Players)p.Pieces.Settlements-=state.Special.Fortresses.Count(f=>f.PlayerId==p.Id&&f.Strength>0);
        typeof(M5.CombinedGameSession).GetField("state",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(game,state);
        M5Harness.Invoke(game,"RecalculateAwards",state);game.AssertInvariants();
        var result=game.Execute(new M5.Command {Id=ThreeKingdomsTestGame.NextId(),PlayerId="P1",Kind=M5.CommandKind.RollDice,WindowId=state.ThreeKingdoms.WindowId});
        Assert.True(result.Success,result.ErrorCode+":"+result.Message);
        var after=game.GetAuthoritativeStateForTesting();Assert.Equal("luoying",after.ThreeKingdoms.Skills.PendingDecision?.Kind);
        var skip=game.GetPlayerView("P3").ThreeKingdoms.SkillActions.Single(a=>a.Command.Option=="skip");
        result=game.Execute(new M5.Command {Id=ThreeKingdomsTestGame.NextId(),PlayerId="P3",Kind=M5.CommandKind.UseGeneralSkill,WindowId=after.ThreeKingdoms.WindowId,SkillCommand=skip.Command});
        Assert.True(result.Success,result.ErrorCode+":"+result.Message);after=game.GetAuthoritativeStateForTesting();
        Assert.Equal(1,M5Harness.Player(after,"P1").Resources.Wood);Assert.Equal(0,after.Bank.Wood);game.AssertInvariants();
    }

    [Theory][InlineData(false,false)][InlineData(false,true)][InlineData(true,false)][InlineData(true,true)]
    public void EndingWithAReachedFortressResolvesCounterattackOnceAfterSerializedEndPhase(bool holdsCard,bool explicitAttack)
    {
        var game=M5.CombinedGameSession.Create("pirate-islands",4,42);var state=game.GetAuthoritativeStateForTesting();
        state.RulesVersion=M5.CombinedGameSession.ThreeKingdomsB3RulesVersion;state.ThreeKingdoms=Generals();
        var zhuge=state.ThreeKingdoms.Generals.Single(g=>g.GeneralId=="tk-g19");
        var current=state.ThreeKingdoms.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P1");
        (zhuge.GeneralId,current.GeneralId)=(current.GeneralId,zhuge.GeneralId);(zhuge.InstanceId,current.InstanceId)=(current.InstanceId,zhuge.InstanceId);
        var board=Catan.Core.M3.SeafarersGameSession.Create("pirate-islands",4,42).GetPlayerView("P1").Board;
        var fortress=board.Fortresses.Single(f=>f.PlayerId=="P1");
        var route=M3SpecialRulesTests.ShortestSeaPath(board,fortress.StartingVertexId,fortress.BeachheadVertexId)
            .Concat(M3SpecialRulesTests.ShortestSeaPath(board,fortress.BeachheadVertexId,fortress.VertexId)).ToArray();
        state.Ships=state.Ships.Where(s=>s.PlayerId!="P1").Concat(route.Select(id=>new M5.ShipPlacement {PlayerId="P1",LocationId=id,BuiltTurn=1,IsInvasionRoute=true})).ToArray();
        state.ActivePlayerId="P1";state.Turn=9;state.SetupPlacementIndex=8;state.Phase=M5.GamePhase.Action;
        if(holdsCard)M5Harness.Player(state,"P1").Resources.Wood=1;
        M5Harness.Balance(state);foreach(var p in state.Players)p.Pieces.Settlements-=state.Special.Fortresses.Count(f=>f.PlayerId==p.Id&&f.Strength>0);
        typeof(M5.CombinedGameSession).GetField("state",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(game,state);
        M5Harness.Invoke(game,"RecalculateAwards",state);game.AssertInvariants();long draws=state.Random.Draws;
        var result=game.Execute(new M5.Command {Id=ThreeKingdomsTestGame.NextId(),PlayerId="P1",Kind=explicitAttack?M5.CommandKind.AttackFortress:M5.CommandKind.EndTurn,WindowId=state.ThreeKingdoms.WindowId});
        Assert.True(result.Success,result.ErrorCode+":"+result.Message);
        var ending=game.GetAuthoritativeStateForTesting();Assert.Equal(ThreeKingdomsPhase.End,ending.ThreeKingdoms.Clock.Phase);
        Assert.Equal(draws,ending.Random.Draws);Assert.Equal(route.Length,ending.Ships.Count(s=>s.PlayerId=="P1"));
        // 夹具没有合法的全局历史，按真实存档序列化权威状态后注入另一会话，验证尾步数据可恢复。
        var serializer=new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(M5.GameState));
        using var stream=new MemoryStream(System.Text.Encoding.UTF8.GetBytes(game.Save()));
        var restoredState=(M5.GameState)serializer.ReadObject(stream)!;
        var restored=M5.CombinedGameSession.Create("pirate-islands",4,42);
        typeof(M5.CombinedGameSession).GetField("state",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(restored,restoredState);restored.AssertInvariants();
        var finish=new M5.Command {Id=ThreeKingdomsTestGame.NextId(),PlayerId="P1",Kind=M5.CommandKind.AdvanceThreeKingdomsPhase,WindowId=ending.ThreeKingdoms.WindowId};
        result=game.Execute(finish);Assert.True(result.Success,result.ErrorCode+":"+result.Message);
        Assert.True(restored.Execute(finish).Success);Assert.Equal(game.Save(),restored.Save());
        var after=game.GetAuthoritativeStateForTesting();Assert.Equal(draws+1,after.Random.Draws);
        Assert.Equal(route.Length-(holdsCard?2:0),after.Ships.Count(s=>s.PlayerId=="P1"));
        Assert.Equal(3,after.Special.Fortresses.Single(f=>f.PlayerId=="P1").Strength);Assert.Equal("P2",after.ActivePlayerId);
        Assert.True(game.Execute(finish).IsDuplicate);Assert.Equal(after.Random.Draws,game.GetAuthoritativeStateForTesting().Random.Draws);game.AssertInvariants();
    }

    [Theory][InlineData("luoying")][InlineData("lianying")][InlineData("automatic")]
    public void PirateLossResponsesFinishBeforeSevenLocksDiscardAmountsWithoutMovingThePirateTwice(string response)
    {
        var game=M5.CombinedGameSession.Create("pirate-islands",4,42);var state=game.GetAuthoritativeStateForTesting();
        var map=M5.CombinedScenarios.Create("pirate-islands",4);
        state.RulesVersion=M5.CombinedGameSession.ThreeKingdomsB3RulesVersion;state.ThreeKingdoms=Generals();
        if(response!="automatic") {
            var current=state.ThreeKingdoms.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId==(response=="luoying"?"P3":"P2"));
            var wanted=state.ThreeKingdoms.Generals.Single(g=>g.GeneralId==(response=="luoying"?"tk-g30":"tk-g23"));
            (current.GeneralId,wanted.GeneralId)=(wanted.GeneralId,current.GeneralId);(current.InstanceId,wanted.InstanceId)=(wanted.InstanceId,current.InstanceId);
        }
        state.ActivePlayerId="P1";state.Turn=9;state.SetupPlacementIndex=8;state.FirstBarbarianAttack=true;
        var victim=state.Settlements.Single(s=>s.PlayerId=="P2");
        int targetIndex=Array.FindIndex(map.PiratePath,id=>state.Tiles.Single(t=>t.Id==id).Vertices.Contains(victim.LocationId));
        Assert.True(targetIndex>=0);string destination=map.PiratePath[targetIndex];
        state.PirateTileId=map.PiratePath[(targetIndex+map.PiratePath.Length-1)%map.PiratePath.Length];
        M5Harness.Player(state,"P2").Resources.Wood=1;
        if(response=="luoying")M5Harness.Player(state,"P3").Resources.Wood=7;
        state.ThreeKingdoms.Clock.Phase=ThreeKingdomsPhase.Roll;state.ThreeKingdoms.Clock.DrawPhaseTurnInstanceId=0;state.Phase=M5.GamePhase.ProductionAwaitRoll;
        state.ThreeKingdoms.Dice=new B3DiceState {OpenedTurnId=state.ThreeKingdoms.Clock.TurnInstanceId,
            FixedResult=new B3DiceResult {PlayerId="P1",Purpose=B3DicePurpose.Formal,Red=1,White=6,EventFace=4}};
        M5Harness.Balance(state);foreach(var p in state.Players)p.Pieces.Settlements-=state.Special.Fortresses.Count(f=>f.PlayerId==p.Id&&f.Strength>0);
        typeof(M5.CombinedGameSession).GetField("state",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(game,state);
        M5Harness.Invoke(game,"RecalculateAwards",state);game.AssertInvariants();
        var result=game.Execute(new M5.Command {Id=ThreeKingdomsTestGame.NextId(),PlayerId="P1",Kind=M5.CommandKind.RollDice,WindowId=state.ThreeKingdoms.WindowId});
        Assert.True(result.Success,result.ErrorCode+":"+result.Message);var after=game.GetAuthoritativeStateForTesting();
        Assert.Equal(destination,after.PirateTileId);
        if(response!="automatic") {
            Assert.True(after.ProductionPending);Assert.True(after.Special.PirateProductionResolved);
            Assert.Empty(after.Discards);Assert.Null(after.PendingDecision);
            string player=response=="luoying"?"P3":"P2";
            var action=game.GetPlayerView(player).ThreeKingdoms.SkillActions.Single(a=>response=="luoying"?a.Command.Option=="luoying"&&a.Command.Counts[0]==1:a.Command.Option=="card"&&a.Command.CardType==1);
            result=game.Execute(new M5.Command {Id=ThreeKingdomsTestGame.NextId(),PlayerId=player,Kind=M5.CommandKind.UseGeneralSkill,WindowId=after.ThreeKingdoms.WindowId,SkillCommand=action.Command});
            Assert.True(result.Success,result.ErrorCode+":"+result.Message);after=game.GetAuthoritativeStateForTesting();
        }
        Assert.False(after.ProductionPending);Assert.False(after.Special.PirateProductionResolved);Assert.Equal(destination,after.PirateTileId);
        if(response=="luoying") {
            Assert.Equal(8,M5Harness.Player(after,"P3").Resources.Wood);Assert.Equal(M5.GamePhase.Discard,after.Phase);
            Assert.Equal("P3",after.Discards.Single().PlayerId);Assert.Equal(4,after.Discards.Single().Amount);
        } else {
            Assert.Equal(M5.GamePhase.RobberSteal,after.Phase);Assert.Empty(after.Discards);
            Assert.Equal(response=="lianying"?2:0,M5Harness.Player(after,"P2").Resources.Total);
            Assert.Null(after.ThreeKingdoms.Skills.PendingDecision);
        }
        game.AssertInvariants();
    }

    [Fact]
    public void AutomaticPirateReturnFinishesSevenBeforePostDiceJizhiCanIncreaseASevenCardHand()
    {
        var game=M5.CombinedGameSession.Create("pirate-islands",4,42);var state=game.GetAuthoritativeStateForTesting();var map=M5.CombinedScenarios.Create("pirate-islands",4);
        state.RulesVersion=M5.CombinedGameSession.ThreeKingdomsB3RulesVersion;state.ThreeKingdoms=Generals();
        var current=state.ThreeKingdoms.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P3");
        var huang=state.ThreeKingdoms.Generals.Single(g=>g.GeneralId=="tk-g33");
        (current.GeneralId,huang.GeneralId)=(huang.GeneralId,current.GeneralId);(current.InstanceId,huang.InstanceId)=(huang.InstanceId,current.InstanceId);
        state.ActivePlayerId="P1";state.Turn=9;state.SetupPlacementIndex=8;state.FirstBarbarianAttack=true;
        var victim=state.Settlements.Single(s=>s.PlayerId=="P2");
        int targetIndex=Array.FindIndex(map.PiratePath,id=>state.Tiles.Single(t=>t.Id==id).Vertices.Contains(victim.LocationId));Assert.True(targetIndex>=0);
        state.PirateTileId=map.PiratePath[(targetIndex+map.PiratePath.Length-1)%map.PiratePath.Length];
        M5Harness.Player(state,"P2").Resources.Wood=1;M5Harness.Player(state,"P3").Resources.Wood=7;
        state.ThreeKingdoms.Clock.Phase=ThreeKingdomsPhase.Roll;state.ThreeKingdoms.Clock.DrawPhaseTurnInstanceId=0;state.Phase=M5.GamePhase.ProductionAwaitRoll;
        // 已知权威随机状态产生真实红 1、白 6、事件 4；七点不触发勤政，使用集智检测骰后奖励次序。
        state.Random.State=359;state.ThreeKingdoms.Dice=new B3DiceState {OpenedTurnId=state.ThreeKingdoms.Clock.TurnInstanceId};
        M5Harness.Balance(state);foreach(var p in state.Players)p.Pieces.Settlements-=state.Special.Fortresses.Count(f=>f.PlayerId==p.Id&&f.Strength>0);
        typeof(M5.CombinedGameSession).GetField("state",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(game,state);M5Harness.Invoke(game,"RecalculateAwards",state);game.AssertInvariants();
        var result=game.Execute(new M5.Command {Id=ThreeKingdomsTestGame.NextId(),PlayerId="P1",Kind=M5.CommandKind.RollDice,WindowId=state.ThreeKingdoms.WindowId});
        Assert.True(result.Success,result.ErrorCode+":"+result.Message);var after=game.GetAuthoritativeStateForTesting();
        Assert.Equal(7,after.LastDice1+after.LastDice2);Assert.Equal(M5.GamePhase.RobberSteal,after.Phase);
        Assert.Null(after.ThreeKingdoms.Skills.PendingDecision);Assert.Equal(ThreeKingdomsPhase.Roll,after.ThreeKingdoms.Clock.Phase);
        Assert.Empty(after.Discards);Assert.Equal(7,M5Harness.Player(after,"P3").Resources.Wood);Assert.False(after.ProductionPending);
        result=game.Execute(new M5.Command {Id=ThreeKingdomsTestGame.NextId(),PlayerId="P1",Kind=M5.CommandKind.StealResource,OtherPlayerId="P4",WindowId=after.ThreeKingdoms.WindowId});
        Assert.True(result.Success,result.ErrorCode+":"+result.Message);after=game.GetAuthoritativeStateForTesting();
        var gain=game.GetPlayerView("P3").ThreeKingdoms.SkillActions.Single(a=>a.Command.Option=="card"&&a.Command.CardType==0);
        result=game.Execute(new M5.Command {Id=ThreeKingdomsTestGame.NextId(),PlayerId="P3",Kind=M5.CommandKind.UseGeneralSkill,WindowId=after.ThreeKingdoms.WindowId,SkillCommand=gain.Command});
        Assert.True(result.Success,result.ErrorCode+":"+result.Message);after=game.GetAuthoritativeStateForTesting();
        Assert.Equal(8,M5Harness.Player(after,"P3").Resources.Wood);Assert.Empty(after.Discards);Assert.Equal(M5.GamePhase.Action,after.Phase);game.AssertInvariants();
    }
}
