using Catan.Core.ThreeKingdoms;
using Xunit;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB6AllianceTests
{
    private static ThreeKingdomsB2TestHost Host()
    {
        var h=ThreeKingdomsB2TestHost.Create("tk-g50","tk-g06","tk-g09");var s=h.State;
        s.RulesVersion=GeneralCatalog.B6RulesVersion;s.PackageVersion=GeneralCatalog.B6PackageVersion;s.PackageContentHash=GeneralCatalog.B6ContentHash;s.AllianceB6=new B6AllianceState();
        B2SkillEngine.OnAllianceB6GeneralActivated(s,s.Generals[0]);B2SkillEngine.Pump(h);var order=B2SkillEngine.Actions(h,"P1").Single(a=>a.Command.Option=="b4-order"&&a.Label.Contains("自立"));B2SkillEngine.Resolve(h,"P1",order.Command);return h;
    }
    private static void Choose(ThreeKingdomsB2TestHost h,string option,int? type=null)
    {
        var d=h.State.Skills.PendingDecision!;var action=B2SkillEngine.Actions(h,d.PlayerId).First(a=>a.Command.Option=="alliance-b6-"+option&&(!type.HasValue||a.Command.CardType==type));
        B2SkillEngine.Resolve(h,d.PlayerId,action.Command);
    }
    [Fact] public void EntryNegotiatesInSeatOrderAndReceivesBlueYellowOnlyOnce()
    {
        var h=Host();Assert.Equal("P2",h.State.Skills.PendingDecision.PlayerId);Choose(h,"join");Assert.Equal("P1",h.State.Skills.PendingDecision.PlayerId);Choose(h,"accept");
        Assert.Null(h.State.Skills.PendingDecision);Assert.Equal(new[]{"P1","P2"},B6AllianceRules.Members(h.State,"P2"));Assert.False(B6AllianceRules.CanTrade(h.State,"P2","P3"));Assert.False(B6AllianceRules.CanAttack(h.State,"P1","P2"));
        Assert.Equal(new[]{0,1},h.Progress["P1"].Select(c=>c.Color).OrderBy(x=>x));B2SkillEngine.OnAllianceB6GeneralActivated(h.State,h.State.Generals[0]);B2SkillEngine.Pump(h);Assert.Equal(2,h.Progress["P1"].Count);
    }
    [Fact] public void RejectionLeavesBothEnemyPlayersInSeparateFactions()
    {
        var h=Host();Choose(h,"reject");Assert.Equal("P3",h.State.Skills.PendingDecision.PlayerId);Choose(h,"reject");Assert.False(B6AllianceRules.CanTrade(h.State,"P2","P3"));Assert.True(B6AllianceRules.CanAttack(h.State,"P2","P3"));
    }
    [Fact] public void ConditionalJoinTransfersOnlyAfterConfirmationAndRestores()
    {
        var h=Host();h.Hands["P2"][5]=2;h.Bank[5]-=2;Choose(h,"join");Choose(h,"demand");Choose(h,"card",5);Choose(h,"card",5);Choose(h,"offer");
        Assert.Equal(0,h.Hands["P1"][5]);Assert.False(B6AllianceRules.Allies(h.State,"P1","P2"));var copy=h.Copy();Choose(h,"confirm");Choose(copy,"confirm");Assert.Equal(h.Snapshot(),copy.Snapshot());Assert.Equal(2,h.Hands["P1"][5]);Assert.Equal(0,h.Hands["P2"][5]);
    }
    [Theory][InlineData("P1")][InlineData("P2")]
    public void ControlledAllianceMayJoinWithoutTermsButCannotOfferUnilateralPayment(string asset)
    {
        var h=Host();h.Hands["P1"][0]=1;h.Bank[0]--;h.State.ControlB6=new B6ControlGrant{AssetPlayerId=asset,ControllerPlayerId="P3",OpportunityId=h.State.Clock.OpportunityId};Choose(h,"join");
        var actions=B2SkillEngine.Actions(h,"P1");Assert.DoesNotContain(actions,a=>a.Command.Option=="alliance-b6-demand"||a.Command.Option=="alliance-b6-gift");Assert.Contains(actions,a=>a.Command.Option=="alliance-b6-accept");Choose(h,"accept");Assert.True(B6AllianceRules.Allies(h.State,"P1","P2"));Assert.Equal(1,h.Hands["P1"][0]);
    }
    [Theory][InlineData("P1")][InlineData("P2")]
    public void ControlledAllianceCannotConfirmPreviouslyOfferedUnilateralPayment(string asset)
    {
        var h=Host();h.Hands["P2"][0]=1;h.Bank[0]--;Choose(h,"join");Choose(h,"demand");Choose(h,"card",0);Choose(h,"offer");var forged=B2SkillEngine.Actions(h,"P2").Single(a=>a.Command.Option=="alliance-b6-confirm").Command;
        h.State.ControlB6=new B6ControlGrant{AssetPlayerId=asset,ControllerPlayerId="P3",OpportunityId=h.State.Clock.OpportunityId};Assert.DoesNotContain(B2SkillEngine.Actions(h,"P2"),a=>a.Command.Option=="alliance-b6-confirm");Assert.Throws<InvalidOperationException>(()=>B2SkillEngine.Resolve(h,"P2",forged));Assert.Equal(1,h.Hands["P2"][0]);Assert.Equal(0,h.Hands["P1"][0]);Assert.False(B6AllianceRules.Allies(h.State,"P1","P2"));
    }
    [Fact] public void UnsatisfiedTermsNeverTakePartialPaymentOrRevealInventoryToLeader()
    {
        var h=Host();Choose(h,"join");Choose(h,"demand");var before=B2SkillEngine.Actions(h,"P1").Select(a=>a.Label).ToArray();h.Hands["P2"][0]=1;Assert.Equal(before,B2SkillEngine.Actions(h,"P1").Select(a=>a.Label));
        Choose(h,"card",0);Choose(h,"card",0);Choose(h,"offer");Assert.DoesNotContain(B2SkillEngine.Actions(h,"P2"),a=>a.Command.Option=="alliance-b6-confirm");Choose(h,"reject");Assert.Equal(1,h.Hands["P2"][0]);Assert.False(B6AllianceRules.Allies(h.State,"P1","P2"));
    }
    [Fact] public void RetiringSourceRevokesSharingButKeepsCardsAndSavedWinner()
    {
        var h=Host();Choose(h,"join");Choose(h,"accept");B6AllianceRules.RecordVictory(h.State,"P2");B2SkillEngine.OnAllianceB6GeneralRetired(h.State,h.State.Generals[0]);
        Assert.False(B6AllianceRules.Allies(h.State,"P1","P2"));Assert.True(B6AllianceRules.CanTrade(h.State,"P2","P3"));Assert.Equal(2,h.Progress["P1"].Count);Assert.Equal(new[]{"P1","P2"},h.State.AllianceB6.WinnerPlayerIds);Assert.True(h.State.AllianceB6.KnightsNeedHomeCheck);
    }
    [Fact] public void CloneDoesNotShareSourcesMembersOrWinners()
    {
        var h=Host();Choose(h,"join");Choose(h,"accept");B6AllianceRules.RecordVictory(h.State,"P1");var clone=B2SkillEngine.CloneB6Alliance(h.State.AllianceB6);clone.Sources[0].Members[0]="P3";clone.WinnerPlayerIds[0]="P3";Assert.Equal("P1",h.State.AllianceB6.Sources[0].Members[0]);Assert.Equal("P1",h.State.AllianceB6.WinnerPlayerIds[0]);
    }
    [Fact] public void SharedTechnologyUsesMaximumWithoutMutatingPersonalLevels()
    {
        var h=Host();Choose(h,"join");Choose(h,"accept");var a=new[]{1,2,0};var b=new[]{3,0,4};Assert.Equal(new[]{3,2,4},B6AllianceRules.SharedImprovements(h.State,"P1",id=>id=="P1"?a:b));Assert.Equal(new[]{1,2,0},a);
    }
    [Fact] public void EmptyProgressDeckDoesNotCreateCards()
    {
        var h=Host();h.Decks[0].Clear();h.Decks[1].Clear();Choose(h,"reject");Choose(h,"reject");Assert.Empty(h.Progress["P1"]);
    }
    [Theory][InlineData("tk-g17","civil-qixi-hand")][InlineData("tk-g53","b5m-chuhai")][InlineData("tk-g03","civil-fanjian")]
    public void HarmfulTargetEnumerationExcludesAllies(string general,string option)
    {
        var h=ThreeKingdomsB5CivilTestHost.Create(general);var s=h.State;s.RulesVersion=GeneralCatalog.B6RulesVersion;s.PackageVersion=GeneralCatalog.B6PackageVersion;s.PackageContentHash=GeneralCatalog.B6ContentHash;
        s.AllianceB6=new B6AllianceState{Sources=new[]{new B6AllianceSource{SourcePlayerId="P1",TenureId="alliance-source",Members=new[]{"P1","P2"}}}};
        h.Inner.Hands["P1"][1]=3;h.Inner.Hands["P2"][0]=2;h.Inner.Hands["P3"][0]=2;s.Generals[0].Counters=new[]{new GeneralCounter{Key="xianghai-tax",Value=2}};
        var actions=B2SkillEngine.Actions(h,"P1").Where(a=>a.Command.Option==option).ToArray();Assert.NotEmpty(actions);Assert.All(actions,a=>Assert.DoesNotContain("P2",a.Command.TargetPlayerIds));Assert.Contains(actions,a=>a.Command.TargetPlayerIds.Contains("P3"));
    }
    [Fact] public void AllianceGraphRemainsTransitiveAndDoesNotMergeOverLimit()
    {
        var h=Host();h.State.SeatPlayerIds=new[]{"P1","P2","P3","P4"};h.State.AllianceB6.Sources=new[]{new B6AllianceSource{SourcePlayerId="P1",TenureId="one",Members=new[]{"P1","P2"}},new B6AllianceSource{SourcePlayerId="P2",TenureId="two",Members=new[]{"P2","P3"}}};
        Assert.Equal(new[]{"P1","P2","P3"},B6AllianceRules.Members(h.State,"P3"));Assert.False(B6AllianceRules.CanMerge(h.State,"P1","P4"));B2SkillEngine.OnAllianceB6GeneralRetired(h.State,new GeneralInstance{TenureId="one"});Assert.Equal(new[]{"P2","P3"},B6AllianceRules.Members(h.State,"P3"));Assert.Equal(new[]{"P1"},B6AllianceRules.Members(h.State,"P1"));
    }
}
