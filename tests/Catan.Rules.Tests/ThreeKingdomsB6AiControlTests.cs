using System.Reflection;
using Catan.AI;
using Catan.Core;
using Catan.Core.ThreeKingdoms;
using M4=Catan.Core.M4;
using M5=Catan.Core.M5;
using Xunit;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB6AiControlTests
{
    [Theory][InlineData("cities")][InlineData("combined")]
    public void ControlledAiDoesNotRepeatRejectedTradeInTheSameTurn(string mode)
    {
        void Configure(dynamic state)
        {
            state.RulesVersion=mode=="cities"?M4.CitiesKnightsGameSession.ThreeKingdomsB6RulesVersion:M5.CombinedGameSession.ThreeKingdomsB6RulesVersion;
            state.ThreeKingdoms=ThreeKingdomsB6ControlTests.State();state.Players[0].Resources.Wood=2;state.Players[2].Resources.Ore=1;
        }
        dynamic game=mode=="cities"?(object)M4CoreHarness.Fixture(s=>Configure(s)):M5Harness.Fixture(s=>Configure(s));
        var proposer=(mode=="cities"?typeof(CitiesKnightsAi):typeof(CombinedAi)).GetMethod("ProposeTrade",BindingFlags.NonPublic|BindingFlags.Static)!;
        object view=game.GetPlayerView("P2");
        Assert.True(((dynamic)view).ThreeKingdoms.RequireEqualPlayerTradeCounts);
        dynamic offer=proposer.Invoke(null,new[]{view,new ResourceBag{Ore=1}})!;Assert.NotNull(offer);
        offer.PlayerId="P2";offer.ActingAsPlayerId="P1";offer.Id=ThreeKingdomsTestGame.NextId();offer.WindowId=((dynamic)view).ThreeKingdoms.WindowId;
        dynamic result=game.Execute(offer);Assert.True((bool)result.Success,(string)result.Message);
        Assert.Equal("P1",(string)result.Events[0].PlayerId);Assert.StartsWith("P1",(string)result.Events[0].Message);
        GameHistoryEntry[] history=game.GetHistory("P2",0);Assert.Equal("P1",history.Last(h=>h.Kind=="ProposeTrade").PlayerId);
        dynamic saved=game.GetAuthoritativeStateForTesting();Assert.Equal("P2",(string)saved.ProcessedCommands[0].Command.PlayerId);Assert.Equal("P1",(string)saved.ProcessedCommands[0].Command.ActingAsPlayerId);
        dynamic reject=mode=="cities"?(object)new M4.Command{Kind=M4.CommandKind.RejectTrade}:new M5.Command{Kind=M5.CommandKind.RejectTrade};
        reject.PlayerId=offer.OtherPlayerId;reject.Id=ThreeKingdomsTestGame.NextId();reject.WindowId=game.GetPlayerView(reject.PlayerId).ThreeKingdoms.WindowId;
        result=game.Execute(reject);Assert.True((bool)result.Success,(string)result.Message);
        view=game.GetPlayerView("P2");Assert.Null(proposer.Invoke(null,new[]{view,new ResourceBag{Ore=1}}));
        dynamic next=mode=="cities"?(object)new CitiesKnightsAi().Decide((M4.PlayerView)view):new CombinedAi().Decide((M5.PlayerView)view);
        Assert.NotEqual("ProposeTrade",(string)next.Kind.ToString());game.AssertInvariants();
    }

    [Theory][InlineData("cities",true)][InlineData("combined",true)][InlineData("cities",false)][InlineData("combined",false)]
    public void AiOffersRespectPublicAllianceAndBlockedGainProjection(string mode,bool alliance)
    {
        void Configure(dynamic state)
        {
            state.RulesVersion=mode=="cities"?M4.CitiesKnightsGameSession.ThreeKingdomsB6RulesVersion:M5.CombinedGameSession.ThreeKingdomsB6RulesVersion;
            ThreeKingdomsState tk=ThreeKingdomsB6ControlTests.State();state.ThreeKingdoms=tk;state.Players[0].Resources.Wood=8;state.Players[2].Resources.Ore=1;
            if(alliance)
            {
                var active=tk.Generals.Single(g=>g.OwnerPlayerId=="P3"&&g.Region==GeneralRegion.Active);var zhong=tk.Generals.Single(g=>g.GeneralId=="tk-g50");
                (active.GeneralId,zhong.GeneralId)=(zhong.GeneralId,active.GeneralId);(active.InstanceId,zhong.InstanceId)=(zhong.InstanceId,active.InstanceId);
                tk.AllianceB6=new B6AllianceState{Sources=new[]{new B6AllianceSource{SourcePlayerId="P3",TenureId=active.TenureId,Members=new[]{"P3","P2"}}}};
            }
            else tk.Skills.TimedEffects=Enumerable.Range(1,7).Select(t=>new B2TimedEffect{Id="景略"+t,Kind="b6-jinglue:"+t,PlayerId="P3",OpportunityId=tk.Clock.OpportunityId}).ToArray();
        }
        dynamic game=mode=="cities"?(object)M4CoreHarness.Fixture(s=>Configure(s)):M5Harness.Fixture(s=>Configure(s));
        object view=game.GetPlayerView("P2");ThreeKingdomsView projected=((dynamic)view).ThreeKingdoms;
        if(alliance)Assert.Empty(projected.AllowedTradePartnerPlayerIds);else Assert.Contains((int)Resource.Ore,projected.BlockedGainCardTypes);
        var proposer=(mode=="cities"?typeof(CitiesKnightsAi):typeof(CombinedAi)).GetMethod("ProposeTrade",BindingFlags.NonPublic|BindingFlags.Static)!;
        Assert.Null(proposer.Invoke(null,new[]{view,new ResourceBag{Ore=1}}));
        dynamic next=mode=="cities"?(object)new CitiesKnightsAi().Decide((M4.PlayerView)view):new CombinedAi().Decide((M5.PlayerView)view);
        if(!alliance&&next.Kind.ToString()=="BankTrade")Assert.DoesNotContain(next.ReceiveIsCommodity?5+(int)next.ReceiveCommodity:(int)next.ReceiveResource,projected.BlockedGainCardTypes);
        next.Id=ThreeKingdomsTestGame.NextId();dynamic result=game.Preview(next);Assert.True((bool)result.Success,(string)result.Message);game.AssertInvariants();
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void AiRejectsTradeWhenJinglueForbidsTheIncomingRealCard(string mode)
    {
        void Configure(dynamic state)
        {
            state.RulesVersion=mode=="cities"?M4.CitiesKnightsGameSession.ThreeKingdomsB6RulesVersion:M5.CombinedGameSession.ThreeKingdomsB6RulesVersion;
            state.ThreeKingdoms=ThreeKingdomsB6ControlTests.State();state.Players[0].Resources.Wood=2;state.Players[2].Resources.Ore=1;
            state.ThreeKingdoms.Skills.TimedEffects=new[]{new B2TimedEffect{Id="景略禁木",Kind="b6-jinglue:0",PlayerId="P2",OpportunityId=state.ThreeKingdoms.Clock.OpportunityId}};
        }
        dynamic game=mode=="cities"?(object)M4CoreHarness.Fixture(s=>Configure(s)):M5Harness.Fixture(s=>Configure(s));
        dynamic offer=mode=="cities"?(object)new M4.Command{Kind=M4.CommandKind.ProposeTrade}:new M5.Command{Kind=M5.CommandKind.ProposeTrade};
        offer.PlayerId="P2";offer.ActingAsPlayerId="P1";offer.OtherPlayerId="P3";offer.Give=new ResourceBag{Wood=1};offer.Receive=new ResourceBag{Ore=1};offer.Id=ThreeKingdomsTestGame.NextId();offer.WindowId=game.GetPlayerView("P2").ThreeKingdoms.WindowId;
        dynamic result=game.Execute(offer);Assert.True((bool)result.Success,(string)result.Message);
        object view=game.GetPlayerView("P3");dynamic next=mode=="cities"?(object)new CitiesKnightsAi().Decide((M4.PlayerView)view):new CombinedAi().Decide((M5.PlayerView)view);
        Assert.Equal("RejectTrade",(string)next.Kind.ToString());next.Id=ThreeKingdomsTestGame.NextId();result=game.Execute(next);Assert.True((bool)result.Success,(string)result.Message);game.AssertInvariants();
    }
}
