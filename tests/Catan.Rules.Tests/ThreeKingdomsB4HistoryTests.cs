using System.Text.Json;
using Catan.Core;
using Catan.Core.ThreeKingdoms;
using Xunit;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB4HistoryTests
{
    private static ThreeKingdomsState State()
    {
        var state=ThreeKingdomsB2TestHost.Create("tk-g15").State;
        state.RulesVersion=GeneralCatalog.B4RulesVersion;
        state.PackageVersion=GeneralCatalog.B4PackageVersion;
        return state;
    }
    [Fact]
    public void OrdinaryProductionAndDeclinedReplacementDoNotPretendToActivateSkills()
    {
        var host=ThreeKingdomsB2TestHost.Create("tk-g28");
        host.State.RulesVersion=GeneralCatalog.B4RulesVersion;host.State.PackageVersion=GeneralCatalog.B4PackageVersion;
        B2SkillEngine.BeginHarvest(host,[new(){PlayerId="P1",Cards=[1,0,0,0,0,0,0,0]}]);
        Assert.Empty(host.State.SkillHistory??[]);
        var pass=B2SkillEngine.Actions(host,"P1").Single(a=>a.Command.Option=="pass").Command;
        B2SkillEngine.Resolve(host,"P1",pass);
        Assert.Empty(host.State.SkillHistory??[]);Assert.Equal(1,host.GetHand("P1")[0]);
    }
    [Fact]
    public void RefusingLuoyingDoesNotRecordAnActivation()
    {
        var host=ThreeKingdomsB2TestHost.Create("tk-g30","tk-g13","tk-g09");
        host.State.RulesVersion=GeneralCatalog.B4RulesVersion;host.State.PackageVersion=GeneralCatalog.B4PackageVersion;host.State.Clock.ActivePlayerId="P2";
        B2SkillEngine.HoldReturnedCards(host,"P2","payment","独立退牌",[1,0,0,0,0,0,0,0]);B2SkillEngine.Pump(host);
        Assert.Empty(host.State.SkillHistory??[]);
        var skip=B2SkillEngine.Actions(host,"P1").Single(a=>a.Command.Option=="skip").Command;
        B2SkillEngine.Resolve(host,"P1",skip);Assert.Empty(host.State.SkillHistory??[]);
    }
    [Theory] [InlineData("cities")] [InlineData("combined")]
    public void AuthoritySkillEventsProduceOneNamedHistoryEntryAndReplayIdentically(string rules)
    {
        var game=ThreeKingdomsTestGame.Create(rules,seats:3,skills:true,packageVersion:GeneralCatalog.B4PackageVersion);
        for(int step=0;step<400;step++)
        {
            game.Accept(game.Decide());
            var events=game.Authority().SkillHistory;
            if(events==null||!events.Any(e=>e.Complete))continue;
            var history=(GameHistoryEntry[])game.History("P1");
            foreach(var item in events)
            {
                var entry=Assert.Single(history,e=>e.SkillEventId==item.EventId);
                Assert.Equal(item.PlayerId,entry.PlayerId);Assert.Equal(item.TenureId,entry.GeneralTenureId);
                Assert.Contains(GeneralCatalog.Get(item.GeneralId).Name,entry.Action);
                Assert.Contains(GeneralCatalog.Get(item.GeneralId).Skills.Single(s=>s.Id==item.SkillId).Name,entry.Action);
            }
            var restored=ThreeKingdomsTestGame.Load(rules,game.Save());
            Assert.Equal(JsonSerializer.Serialize(history),JsonSerializer.Serialize((GameHistoryEntry[])restored.History("P1")));
            return;
        }
        Assert.Fail("正常对局未触发可核查的技能历史。");
    }
    [Fact]
    public void ActualCardFlowsLinkOnceAndChildRewardsContinueOneActivation()
    {
        var state=State();
        B4SkillHistoryEngine.Begin(state,"父帧","P1","tk-g15-s1","任次");
        B2HandChange[] given=[new(){PlayerId="P1",BeforeCount=3,AfterCount=1,LostCount=2,GivenCount=2},new(){PlayerId="P2",BeforeCount=0,AfterCount=2}];
        B4SkillHistoryEngine.LinkCardFlow(state,"父帧","父帧:give",given);
        B4SkillHistoryEngine.LinkCardFlow(state,"父帧","父帧:give",given);
        B4SkillHistoryEngine.Complete(state,"父帧");
        B4SkillHistoryEngine.Begin(state,"奖励子帧","P1","tk-g15-s1","任次","父帧:give");
        B4SkillHistoryEngine.LinkCardFlow(state,"奖励子帧","领取批次",[new(){PlayerId="P1",BeforeCount=1,AfterCount=2,GainedCards=[0,0,0,0,0,0,0,1]}]);
        B4SkillHistoryEngine.Complete(state,"奖励子帧");
        var entry=Assert.Single(state.SkillHistory);
        Assert.Equal(["父帧:give","领取批次"],entry.RelatedCardFlowIds);
        Assert.Contains("P1交出2张",entry.PublicResult);Assert.Contains("P2获得2张",entry.PublicResult);Assert.Contains("P1获得1张",entry.PublicResult);
        Assert.DoesNotContain("纸张",JsonSerializer.Serialize(B4SkillHistoryEngine.Project(state,"P3")));
        Assert.True(entry.Complete);
    }
    [Theory] [InlineData("cities")] [InlineData("combined")]
    public void PendingDeclarationDoesNotReadRealCardsFromTheSavedProposalFallback(string mode)
    {
        object state;
        if(mode=="cities")
        {
            var s=M4CoreHarness.New().GetAuthoritativeStateForTesting();
            s.ProcessedCommands=[new(){Command=new(){Kind=Catan.Core.M4.CommandKind.ProposeTrade,Give=new(){Ore=1},Receive=new(){Brick=1},DeclaredRealTypes=[4],DeclaredEffectiveTypes=[0]}}];state=s;
        }
        else
        {
            var s=M5Harness.New().GetAuthoritativeStateForTesting();
            s.ProcessedCommands=[new(){Command=new(){Kind=Catan.Core.M5.CommandKind.ProposeTrade,Give=new(){Ore=1},Receive=new(){Brick=1},DeclaredRealTypes=[4],DeclaredEffectiveTypes=[0]}}];state=s;
        }
        var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
        var capture=typeof(GameHistoryEntry).Assembly.GetType("Catan.Core.GameHistoryCapture")!.GetMethods(flags).Single(m=>m.Name=="From"&&m.GetParameters()[0].ParameterType==state.GetType()).Invoke(null,[state])!;
        var give=(int[])capture.GetType().GetField("OfferGive",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(capture)!;
        Assert.Equal(new int[8],give);
    }
    [Theory] [InlineData("cities")] [InlineData("combined")]
    public void RealTradePaymentOnlyAppearsToTheTwoParticipants(string mode)
    {
        dynamic game=mode=="cities"?(object)M4CoreHarness.Fixture(s=>{
            s.Players.Single(p=>p.Id=="P1").Resources.Ore=1;s.Players.Single(p=>p.Id=="P2").Resources.Brick=1;
            s.TradeOffer=new Catan.Core.M4.TradeOffer {ProposerId="P1",OtherPlayerId="P2",Give=new(){Wood=1},Receive=new(){Brick=1},GiveCommodities=new(),ReceiveCommodities=new(),B4RealGive=[0,0,0,0,1,0,0,0]};
        }):M5Harness.Fixture(s=>{
            s.Players.Single(p=>p.Id=="P1").Resources.Ore=1;s.Players.Single(p=>p.Id=="P2").Resources.Brick=1;
            s.TradeOffer=new Catan.Core.M5.TradeOffer {ProposerId="P1",OtherPlayerId="P2",Give=new(){Wood=1},Receive=new(){Brick=1},GiveCommodities=new(),ReceiveCommodities=new(),B4RealGive=[0,0,0,0,1,0,0,0]};
        });
        dynamic command=mode=="cities"?(object)new Catan.Core.M4.Command{Kind=Catan.Core.M4.CommandKind.AcceptTrade,PlayerId="P2"}:new Catan.Core.M5.Command{Kind=Catan.Core.M5.CommandKind.AcceptTrade,PlayerId="P2"};
        command.Id=ThreeKingdomsTestGame.NextId();Assert.True((bool)game.Execute(command).Success);
        foreach(string viewer in new[]{"P1","P2"})Assert.Contains((GameHistoryEntry[])game.GetHistory(viewer,0),e=>e.Changes.Any(c=>c.GoodIndex==4));
        Assert.DoesNotContain("矿石",JsonSerializer.Serialize((GameHistoryEntry[])game.GetHistory("P3",0)));
        Assert.All(((GameHistoryEntry[])game.GetHistory("P3",0)).SelectMany(e=>e.Changes),c=>Assert.Equal(-1,c.GoodIndex));
    }
    [Fact]
    public void OneActivationSurvivesMultipleStepsAndRepeatedCompletion()
    {
        var state=State();
        B4SkillHistoryEngine.Begin(state,"赠予批次1","P1","tk-g15-s1","任次-0","出牌命令1");
        B4SkillHistoryEngine.Begin(state,"赠予批次1","P1","tk-g15-s1","任次-0","出牌命令1");
        B4SkillHistoryEngine.Complete(state,"赠予批次1","交出2张手牌",["牌流1"]);
        B4SkillHistoryEngine.Complete(state,"赠予批次1","错误重复结果",["牌流2"]);
        var item=Assert.Single(state.SkillHistory);
        Assert.Equal("交出2张手牌",item.PublicResult);
        Assert.Equal("任次-0",item.TenureId);Assert.Equal("出牌命令1",item.SourceEventId);
        Assert.Equal(["牌流1"],item.RelatedCardFlowIds);
        Assert.True(item.Complete);
        var restored=JsonSerializer.Deserialize<ThreeKingdomsState>(JsonSerializer.Serialize(state))!;
        Assert.Equal(JsonSerializer.Serialize(B4SkillHistoryEngine.Project(state,"P2")),JsonSerializer.Serialize(B4SkillHistoryEngine.Project(restored,"P2")));
    }
    [Fact]
    public void SkippedUnstartedAndOldPackageEffectsNeverInventActivations()
    {
        var state=State();
        B4SkillHistoryEngine.Complete(state,"已放弃","完成");
        Assert.Null(state.SkillHistory);
        state.PackageVersion=GeneralCatalog.B3PackageVersion;
        B4SkillHistoryEngine.Begin(state,"旧事件","P1","tk-g15-s1");
        Assert.Null(state.SkillHistory);
    }
    [Fact]
    public void PrivateResultsReachOnlyAuthorizedSeatsAndProjectionsAreDetached()
    {
        var state=State();
        B4SkillHistoryEngine.Begin(state,"发动1","P1","tk-g15-s1");
        B4SkillHistoryEngine.Complete(state,"发动1","交出2张手牌");
        B4SkillHistoryEngine.AddPrivateResult(state,"发动1","木材与纸张","P1","P2");
        Assert.Contains("木材与纸张",Assert.Single(B4SkillHistoryEngine.Project(state,"P2")).Result);
        Assert.DoesNotContain("木材",JsonSerializer.Serialize(B4SkillHistoryEngine.Project(state,"P3")));
        Assert.DoesNotContain("木材",JsonSerializer.Serialize(B4SkillHistoryEngine.Project(state,null!)));
        var clone=B4SkillHistoryEngine.Clone(state.SkillHistory);
        clone[0].Secrets[0].ViewerIds[0]="P3";
        Assert.DoesNotContain("木材",JsonSerializer.Serialize(B4SkillHistoryEngine.Project(state,"P3")));
        Assert.Throws<InvalidOperationException>(()=>B4SkillHistoryEngine.Begin(state,"发动1","P2","tk-g15-s1"));
    }
    [Fact]
    public void InvalidAndOverLimitCardSelectionNeverMutatesDraft()
    {
        int[] selected=new int[8],limits=[2,0,0,0,0,1,0,0];
        Assert.False(B4CardInteraction.Change(selected,1,1,limits,2));
        Assert.False(B4CardInteraction.Change(selected,0,-1,limits,2));
        Assert.True(B4CardInteraction.Change(selected,0,1,limits,2));
        Assert.True(B4CardInteraction.Change(selected,5,1,limits,2));
        Assert.False(B4CardInteraction.Change(selected,0,1,limits,2));
        Assert.True(B4CardInteraction.CanSubmit(selected,limits,2));
        Assert.False(B4CardInteraction.CanSubmit(selected,limits,1));
        selected[7]=1;Assert.False(B4CardInteraction.CanSubmit(selected,limits,4));
    }
    [Theory]
    [InlineData("color",2,-1)]
    [InlineData("fixed",0,-1)]
    [InlineData("select",1,-1)]
    [InlineData("card",7,7)]
    [InlineData("modify",5,5)]
    [InlineData("b4-declare",4,-1)]
    public void DiceColorsAndQuantityDeclarationsAreNotMistakenForResources(string option,int type,int expected)
    {
        Assert.Equal(expected,B4CardInteraction.RealCardType(new B2SkillCommand {Option=option,CardType=type}));
    }
    [Fact]
    public void MixedRaidCostsRenderEveryRealCardAndDeclarationsPreserveEachCopy()
    {
        var action=new B2SkillAction {Command=new(){Option="raid",Counts=[1,0,0,2,0,0,0,0]}};
        Assert.Equal([1,0,0,2,0,0,0,0],B4CardInteraction.ShownCards(action));
        var real=B4CardInteraction.ExpandCards(action.Command.Counts);
        Assert.Equal([0,3,3],real);
        Assert.Equal([1,4,5,7],B4CardInteraction.PreserveDeclarations([0,3,3],[1,4,5],[0,3,3,7]));
        Assert.Equal([4,5],B4CardInteraction.PreserveDeclarations(real,[1,4,5],[3,3]));
        action.Command.Option="color";Assert.Equal(new int[8],B4CardInteraction.ShownCards(action));
        action.Command.Option="b4-revealed";Assert.Equal([0,0,1,1,0,0,0,0],B4CardInteraction.ShownCards(action,[0,0,1,1,0,0,0,0]));
        action.Command.Option="b4-hidden";Assert.Equal(new int[8],B4CardInteraction.ShownCards(action,[0,0,1,1,0,0,0,0]));
    }
    [Fact]
    public void PaymentLimitsFollowLegalTypesAndTargetNotJustOwnedCards()
    {
        var template=new B2SkillCommand {SkillId="tk-g15-s1",Option="gift",TargetPlayerIds=["P2"],Counts=[1,0,0,0,0,0,0,0]};
        var other=new B2SkillCommand {SkillId="tk-g15-s1",Option="gift",TargetPlayerIds=["P3"],Counts=[0,1,0,0,0,0,0,0]};
        var limits=B4CardInteraction.PaymentLimits([new(){Command=template},new(){Command=other}],template,[3,3,3,3,3,3,3,3]);
        Assert.Equal([3,0,0,0,0,0,0,0],limits);
        Assert.Equal(7,B4CardInteraction.RealCardType(new B2SkillAction { RealCardType=7,EffectiveCardType=0,Command=new(){Option="b4-convert"} }));
    }
}
