using System.Reflection;
using System.Text.Json;
using Catan.Core;
using Catan.Core.M4;
using Catan.Core.TradeModules;
using Catan.Core.ThreeKingdoms;
using Xunit;
using Command=Catan.Core.M4.Command;
using CommandKind=Catan.Core.M4.CommandKind;
using GamePhase=Catan.Core.M4.GamePhase;
using GameState=Catan.Core.M4.GameState;
using static Catan.Rules.Tests.M4CoreHarness;

namespace Catan.Rules.Tests;

public sealed class FishingCitiesTests
{
    internal static CitiesKnightsGameSession NewFish(int players=4,bool kingdoms=false) => CitiesKnightsGameSession.Create(AcceptanceFixture.Json,players,97,threeKingdoms:kingdoms,tradeModules:new(){Fishing=true});
    static FishHolding Hand(GameState s,string p="P1")=>s.TradeModules.Fishing.Holdings.Single(h=>h.PlayerId==p);
    static void Give(GameState s,string p,params string[] tokens){s.TradeModules.Fishing.Supply=s.TradeModules.Fishing.Supply.Except(tokens).ToArray();Hand(s,p).TokenIds=tokens;}
    static CitiesKnightsGameSession FixtureFish(Action<GameState>? configure=null,bool tk=false)
    {
        var g=NewFish();var s=g.GetAuthoritativeStateForTesting();s.ActivePlayerId="P1";s.Turn=9;s.SetupPlacementIndex=8;s.Phase=GamePhase.Action;
        if(tk){s.RulesVersion=CitiesKnightsGameSession.ThreeKingdomsB6RulesVersion;s.ThreeKingdoms=ThreeKingdomsB6ControlTests.State();s.ThreeKingdoms.ControlB6=null;}
        configure?.Invoke(s);Balance(s);SetState(g,s);return g;
    }
    static Command FishCommand(CitiesKnightsGameSession g,CommandKind kind=CommandKind.SpendFish,string p="P1")
    {var c=Cmd(kind,p);var s=g.GetAuthoritativeStateForTesting();c.ExpectedEventSequence=s.EventSequence;c.WindowId=s.ThreeKingdoms?.WindowId;return c;}
    static Command Spend(CitiesKnightsGameSession g,FishAction action,params string[] tokens)
    {var c=FishCommand(g);c.FishAction=action;c.FishTokenIds=tokens;return c;}
    static void CheckFish(GameState s)
    {
        var f=s.TradeModules.Fishing;var all=f.Supply.Concat(f.Discard).Concat(f.Holdings.SelectMany(h=>h.TokenIds)).Concat(f.OldBootPlayerId==null?[]:[FishingRules.OldBootId]).ToArray();
        Assert.Equal(30,all.Length);Assert.Equal(30,all.Distinct().Count());Assert.Equal(55,all.Sum(FishingRules.Value));
    }
    [Theory][InlineData(3)][InlineData(4)]
    public void OpeningCityGivesOnlyOneTokenAndStandardIdentityRestores(int players)
    {
        var g=NewFish(players);Assert.Equal(TradeModuleState.ProductVersion,g.GetPlayerView("P1").TradeModules.Version);
        for(int i=0;i<players*2;i++)
        {
            var s=g.GetAuthoritativeStateForTesting();var v=g.GetPlayerView(s.ActivePlayerId);var sources=v.TradeModules.Fishing.Grounds.SelectMany(x=>x.VertexIds).Concat(v.Board.Tiles.Single(t=>t.Resource=="lake").Vertices);
            var vertex=v.LegalVertexIds.First(sources.Contains);int before=s.TradeModules.Fishing.Supply.Length;
            Accepted(g,Cmd(CommandKind.SetupSettlement,s.ActivePlayerId,vertex));Assert.Equal(before-(i>=players?1:0),g.GetAuthoritativeStateForTesting().TradeModules.Fishing.Supply.Length);
            Accepted(g,Cmd(CommandKind.SetupRoad,s.ActivePlayerId,g.GetPlayerView(s.ActivePlayerId).LegalEdgeIds.First()));
        }
        Assert.Equal(g.Save(),CitiesKnightsGameSession.Load(AcceptanceFixture.Json,g.Save()).Save());CheckFish(g.GetAuthoritativeStateForTesting());
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void LakeOnlyIncomeStillReceivesAqueductBeforeFish(bool kingdoms)
    {
        var g=FixtureFish(s=>{s.Cities=[Piece(s.Tiles.Single(t=>t.Resource=="lake").Vertices[0])];s.Players[0].Improvements[2]=3;s.LastDice1=1;s.LastDice2=1;},kingdoms);
        var s=g.GetAuthoritativeStateForTesting();
        if(kingdoms)Invoke(g,"BeginB2Harvest",s);else Invoke(g,"ResolveProduction",s);
        Assert.NotNull(s.FishProductionClaims);Assert.Empty(Hand(s).TokenIds);
        if(kingdoms){Invoke(g,"FinishB2Work",s);Invoke(g,"RefreshB2",s);}
        SetState(g,s);
        for(int i=0;i<60&&g.GetAuthoritativeStateForTesting().FishProductionClaims!=null;i++)
        {
            var wrap=new ThreeKingdomsTestGame("cities",g);var c=wrap.Decide();wrap.Accept(c);
        }
        Assert.Null(g.GetAuthoritativeStateForTesting().FishProductionClaims);Assert.True(Hand(g.GetAuthoritativeStateForTesting()).TokenIds.Length>=1);
        Assert.True(g.GetPlayerView("P1").OwnResources.Total>0);CheckFish(g.GetAuthoritativeStateForTesting());
    }
    [Theory][InlineData(FishAction.RemoveRobber)][InlineData(FishAction.StealResource)][InlineData(FishAction.TakeResource)][InlineData(FishAction.BuildRoad)][InlineData(FishAction.BuyDevelopmentCard)]
    public void FiveActionsAreAtomicBeforeRollAndRetryDoesNotPayTwice(FishAction action)
    {
        var g=FixtureFish(s=>{Give(s,"P1","F22","F23","F24");s.Phase=GamePhase.ProductionAwaitRoll;s.FirstBarbarianAttack=true;s.RobberTileId=s.TradeModules.Fishing.LakeTileId;s.Settlements=[Piece("V01")];s.Players[1].Commodities.Coin=1;});
        var c=Spend(g,action,"F22","F23","F24");c.Resource=Resource.Wood;c.OtherPlayerId="P2";c.Track=ImprovementTrack.Science;
        if(action==FishAction.BuildRoad)c.TargetId=g.GetPlayerView("P1").Board.Edges.First(e=>e.Vertices.Contains("V01")).Id;
        string before=g.Save();Assert.True(g.Preview(c).Success);Assert.Equal(before,g.Save());Accepted(g,c);string after=g.Save();Assert.True(g.Execute(c).IsDuplicate);Assert.Equal(after,g.Save());
        Assert.Empty(Hand(g.GetAuthoritativeStateForTesting()).TokenIds);CheckFish(g.GetAuthoritativeStateForTesting());
        if(action==FishAction.StealResource)Assert.Equal(1,g.GetPlayerView("P1").OwnCommodities.Coin);
        if(action==FishAction.RemoveRobber){Assert.Null(g.GetPlayerView("P1").Board.RobberTileId);Assert.DoesNotContain(g.GetPlayerView("P1").LegalActions,a=>a.Kind==CommandKind.ChaseRobber);}
    }
    [Fact]
    public void FishTheftHidesCommodityIdentityFromOtherSeats()
    {
        var g=FixtureFish(s=>{Give(s,"P1","F22");s.Players[1].Commodities.Coin=1;});
        var c=Spend(g,FishAction.StealResource,"F22");c.OtherPlayerId="P2";Accepted(g,c);
        foreach(var player in new[]{"P3","P4"})
            Assert.All(g.GetHistory(player).Single().Changes,change=>{Assert.Equal("resource-count",change.Category);Assert.Equal(-1,change.GoodIndex);});
        Assert.Contains(g.GetHistory("P1").Single().Changes,change=>change.GoodIndex==6);
        Assert.Contains(g.GetHistory("P2").Single().Changes,change=>change.GoodIndex==6);
    }
    [Fact]
    public void MalformedPaymentStaleVersionAndEmptyTargetsNeverChangeState()
    {
        var g=FixtureFish(s=>Give(s,"P1","F22","F23","F24"));
        var invalid=Spend(g,FishAction.TakeResource,"F22","F22");Rejected(g,invalid);
        invalid=Spend(g,FishAction.TakeResource,"BOOT","F22");Rejected(g,invalid);
        invalid=Spend(g,FishAction.TakeResource,"F22","F23");invalid.ExpectedEventSequence=-1;Rejected(g,invalid);
        invalid=Spend(g,FishAction.StealResource,"F22");invalid.OtherPlayerId="P2";Rejected(g,invalid);
        invalid=Spend(g,FishAction.BuildRoad,"F22","F23");invalid.TargetId="E01";Rejected(g,invalid);
        invalid=Spend(g,FishAction.RemoveRobber,"F22");Rejected(g,invalid);
        invalid=Spend(g,FishAction.BuyDevelopmentCard,"F22","F23","F24");invalid.Track=(ImprovementTrack)9;Rejected(g,invalid);
    }
    [Fact]
    public void BanRejectsChosenResourceButRandomForbiddenCardIsNotRerolledOrRefunded()
    {
        var g=FixtureFish(s=>{Give(s,"P1","F22","F23","F24");s.Players[1].Resources.Wood=1;s.ThreeKingdoms.Skills.TimedEffects=[new(){Id="ban",Kind="b6-jinglue:0",PlayerId="P2",OpportunityId=1}];},true);
        var c=Spend(g,FishAction.TakeResource,"F22","F23");c.Resource=Resource.Wood;Rejected(g,c);
        c=Spend(g,FishAction.StealResource,"F22");c.OtherPlayerId="P2";Accepted(g,c);
        Assert.Equal(1,g.GetPlayerView("P2").OwnResources.Wood);Assert.Equal(0,g.GetPlayerView("P1").OwnResources.Wood);Assert.DoesNotContain("F22",Hand(g.GetAuthoritativeStateForTesting()).TokenIds);
    }
    [Fact]
    public void ControlledFishingUsesAssetTokensAndAuthorizedPrivateView()
    {
        var g=FixtureFish(s=>{Give(s,"P1","F22","F23");Give(s,"P2","F01");s.ThreeKingdoms.ControlB6=ThreeKingdomsB6ControlTests.State().ControlB6;},true);
        var controller=g.GetPlayerView("P2");Assert.Equal("P1",controller.PlayerId);Assert.Equal(["F22","F23"],controller.TradeModules.Fishing.OwnTokens.Select(t=>t.Id));
        Assert.Empty(g.GetPlayerView("P3").TradeModules.Fishing.OwnTokens);
        var c=Spend(g,FishAction.TakeResource,"F22","F23");c.Resource=Resource.Brick;Rejected(g,c);
        c.PlayerId="P2";c.ActingAsPlayerId="P1";Accepted(g,c);
        var s=g.GetAuthoritativeStateForTesting();Assert.Empty(Hand(s).TokenIds);Assert.Equal(["F01"],Hand(s,"P2").TokenIds);Assert.Equal(1,s.Players[0].Resources.Brick);
    }
    [Fact]
    public void FishRoadRecordsZeroResourcePaymentAndNoJiaoziCharge()
    {
        var g=FixtureFish(s=>{Give(s,"P1","F22","F23");s.Settlements=[Piece("V01")];},true);
        var c=Spend(g,FishAction.BuildRoad,"F22","F23");c.TargetId=g.GetPlayerView("P1").Board.Edges.First(e=>e.Vertices.Contains("V01")).Id;
        Accepted(g,c);var s=g.GetAuthoritativeStateForTesting();Assert.Equal(0,s.Players[0].Resources.Total);Assert.Contains(s.ThreeKingdoms.Board.Receipts,r=>r.AssetId=="Road:"+c.TargetId&&r.PaidCount==0);
    }
    [Fact]
    public void LakeMapSwapStaysInlandAndGroundsStayInSpace()
    {
        var g=FixtureFish(tk:true);var s=g.GetAuthoritativeStateForTesting();var fish=s.TradeModules.Fishing;string lake=fish.LakeTileId;
        string outer=s.Tiles.First(t=>Math.Max(Math.Abs(t.Q),Math.Max(Math.Abs(t.R),Math.Abs(t.Q+t.R)))==2).Id;
        string before=g.Save();Assert.Throws<TargetInvocationException>(()=>Invoke(g,"ApplyMapB6",s,lake,outer));Assert.Equal(before,g.Save());
        string inner=s.Tiles.First(t=>t.Id!=lake&&Math.Max(Math.Abs(t.Q),Math.Max(Math.Abs(t.R),Math.Abs(t.Q+t.R)))<=1).Id;
        string grounds=JsonSerializer.Serialize(fish.Grounds);Invoke(g,"ApplyMapB6",s,lake,inner);Assert.Equal(inner,fish.LakeTileId);Assert.Equal(grounds,JsonSerializer.Serialize(fish.Grounds));SetState(g,s);
    }
    [Fact]
    public void ProductPaymentUsesMinimumWasteThenFewestTokensWithoutMutatingView()
    {
        var g=FixtureFish(s=>Give(s,"P1","F01","F02","F12","F22"));var v=g.GetPlayerView("P1");string before=g.Save();
        Assert.Equal(["F12"],FishingPayment.Select(v.TradeModules.Fishing.OwnTokens,FishAction.RemoveRobber));
        Assert.Equal(["F01","F22"],FishingPayment.Select(v.TradeModules.Fishing.OwnTokens,FishAction.TakeResource));Assert.Equal(before,g.Save());
    }
    [Fact]
    public void SevenFishUsesProgressDeckPublicPointsAndOriginalHandLimit()
    {
        var g=FixtureFish(s=>{Give(s,"P1","F22","F23","F24");s.Players[0].ProgressCards=[ProgressCardKind.Alchemy,ProgressCardKind.Crane,ProgressCardKind.Irrigation,ProgressCardKind.Mining];});
        var c=Spend(g,FishAction.BuyDevelopmentCard,"F22","F23","F24");c.Track=ImprovementTrack.Trade;Accepted(g,c);
        Assert.Equal(5,g.GetPlayerView("P1").OwnProgressCards.Length);Rejected(g,Cmd(CommandKind.EndTurn));
        var discard=Cmd(CommandKind.DiscardProgressCard);discard.ProgressCard=g.GetPlayerView("P1").OwnProgressCards[0];Accepted(g,discard);Assert.Equal(4,g.GetPlayerView("P1").OwnProgressCards.Length);
        g=FixtureFish(s=>{Give(s,"P1","F22","F23","F24");s.TargetVictoryPoints=5;s.Players[0].DefenderPoints=4;s.TradeModules.Fishing.Supply=s.TradeModules.Fishing.Supply.Where(t=>t!="BOOT").ToArray();s.TradeModules.Fishing.OldBootPlayerId="P1";});
        var state=g.GetAuthoritativeStateForTesting();var deck=state.ProgressDecks.Single(d=>d.Track==ImprovementTrack.Science);deck.Cards=new[]{ProgressCardKind.Printing}.Concat(deck.Cards.Where(t=>t!=ProgressCardKind.Printing)).ToArray();SetState(g,state);
        c=Spend(g,FishAction.BuyDevelopmentCard,"F22","F23","F24");c.Track=ImprovementTrack.Science;Accepted(g,c);
        Assert.Equal(5,g.GetPlayerView("P1").OwnVictoryPoints);Assert.Equal(6,g.GetPlayerView("P1").TradeModules.Fishing.OwnVictoryTarget);Assert.Null(g.GetPlayerView("P1").WinnerPlayerId);Assert.Empty(g.GetPlayerView("P1").OwnProgressCards);
    }
    [Fact]
    public void ReplacementIsBoundedPrivateAndDeclineKeepsAllowance()
    {
        var g=FixtureFish(s=>{Give(s,"P2","F01","F02","F03","F04","F05","F06","F07");s.Cities=[Piece(s.TradeModules.Fishing.Grounds[0].VertexIds[0],"P2")];});
        var state=g.GetAuthoritativeStateForTesting();Invoke(g,"QueueFishProduction",state,state.TradeModules.Fishing.Grounds[0].Number);Invoke(g,"ResumeFishing",state);SetState(g,state);
        var fish=state.TradeModules.Fishing;Assert.Equal("P2",fish.PendingPlayerId);Assert.Equal(GamePhase.FishReplacement,g.GetPlayerView("P2").Phase);
        Assert.Empty(g.GetPlayerView("P1").TradeModules.Fishing.OwnTokens);Assert.Empty(g.GetPlayerView("P1").LegalActions.Where(c=>c.Kind==CommandKind.ResolveFishReplacement));
        var c=FishCommand(g,CommandKind.ResolveFishReplacement,"P1");c.DecisionId=fish.PendingDecisionId;Rejected(g,c);
        c=FishCommand(g,CommandKind.ResolveFishReplacement,"P2");c.DecisionId="stale";Rejected(g,c);
        c.DecisionId=fish.PendingDecisionId;Accepted(g,c);state=g.GetAuthoritativeStateForTesting();Assert.False(Hand(state,"P2").ReplacementUsed);Assert.Equal(7,Hand(state,"P2").TokenIds.Length);Assert.Empty(state.TradeModules.Fishing.Queue);CheckFish(state);
    }
    [Fact]
    public void AllyBootDoesNotRaiseActivePlayersThresholdAndAllianceCannotBeFishVictim()
    {
        var g=FixtureFish(s=>{Give(s,"P1","F22");s.Players[1].Resources.Ore=1;s.TargetVictoryPoints=5;s.Players[0].DefenderPoints=5;
            s.TradeModules.Fishing.Supply=s.TradeModules.Fishing.Supply.Where(t=>t!="BOOT").ToArray();s.TradeModules.Fishing.OldBootPlayerId="P2";
            var tk=s.ThreeKingdoms;tk.AllianceB6=new B6AllianceState{Sources=[new B6AllianceSource{SourcePlayerId="P1",TenureId=tk.Generals.Single(g=>g.OwnerPlayerId=="P1"&&g.Region==GeneralRegion.Active).TenureId,Members=["P1","P2"]}]};},true);
        var c=Spend(g,FishAction.StealResource,"F22");c.OtherPlayerId="P2";Rejected(g,c);
        var win=Cmd(CommandKind.DeclareVictory);win.WindowId=g.GetPlayerView("P1").ThreeKingdoms.WindowId;Accepted(g,win);
        Assert.Equal("P1",g.GetPlayerView("P1").WinnerPlayerId);Assert.Equal(new[]{"P1","P2"},g.GetPlayerView("P1").ThreeKingdoms.AllianceWinnerPlayerIds);
    }

}
