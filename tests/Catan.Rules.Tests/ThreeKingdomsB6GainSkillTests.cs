using Catan.Core.ThreeKingdoms;
using Xunit;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB6GainSkillTests
{
    private static ThreeKingdomsB5CivilTestHost Host(string first,string second="tk-g12",int type=0,string source="P3")
    {
        var h=ThreeKingdomsB5CivilTestHost.Create(first,second,"tk-g22");h.State.RulesVersion=GeneralCatalog.B6RulesVersion;h.State.PackageVersion=GeneralCatalog.B6PackageVersion;h.State.PackageContentHash=GeneralCatalog.B6ContentHash;
        h.State.Skills.TimedEffects=new[]{new B2TimedEffect{Id="jinglue-ban",Kind="b6-jinglue:"+type,PlayerId=source}};return h;
    }
    private static B2SkillFrame Frame(ThreeKingdomsB5CivilTestHost h,string kind,string skill) => new(){Id="resume-frame",Kind=kind,PlayerId="P1",SkillId=skill,TenureId=h.State.Generals[0].TenureId,Started=true,Targets=new[]{"P2"},B4=new B4SkillPayload{OwnerPlayerId="P1"}};

    [Fact]
    public void ForbiddenGiftResumedAfterResponseKeepsCardsAtOriginalOwner()
    {
        var h=Host("tk-g15");h.Inner.Give("P1",2);var f=Frame(h,"gift","tk-g15-s1");f.Cards[0]=1;h.State.Skills.Frames=new[]{f};var bank=h.GetBank();
        B2SkillEngine.Pump(h);Assert.Equal(2,h.GetHand("P1")[0]);Assert.Equal(0,h.GetHand("P2")[0]);Assert.Equal(bank,h.GetBank());Assert.Empty(h.State.Skills.LossLedger);h.Conserved();
    }

    [Fact]
    public void GuzhengLeavesBannedWoodInReturnBatchForLegalLuoyingClaimant()
    {
        var h=Host("tk-g21","tk-g30",source:"P2");h.Inner.Give("P3",1,1);var discarded=new[]{1,1,0,0,0,0,0,0};h.RemoveHand("P3",discarded);h.AddBank(discarded);
        B2SkillEngine.HoldReturnedCards(h,"P3","seven-discard","seven",discarded);B2SkillEngine.CompleteSevenDiscard(h,"seven");
        Assert.Equal(0,h.GetHand("P1")[0]);Assert.Equal(1,h.GetHand("P1")[1]);Assert.Equal("P2",h.State.Skills.PendingDecision.PlayerId);
        var claim=B2SkillEngine.Actions(h,"P2").Single(a=>a.Command.Option=="luoying");B2SkillEngine.Resolve(h,"P2",claim.Command);
        Assert.Equal(1,h.GetHand("P2")[0]);Assert.Empty(h.State.Skills.ReturnedCards!);h.Conserved();
    }

    [Fact]
    public void XiashuCannotSkipItsBlockedInitialGiftAndTakeOpponentsHand()
    {
        var h=Host("tk-g26");h.Inner.Give("P1",1);h.Inner.Give("P2",0,2);
        Assert.DoesNotContain(B2SkillEngine.Actions(h,"P1"),a=>a.Command.Option=="b4-xiashu"&&a.Command.TargetPlayerIds[0]=="P2");var f=Frame(h,"b4-xiashu","tk-g26-s1");h.State.Skills.Frames=new[]{f};B2SkillEngine.Pump(h);
        Assert.Null(h.State.Skills.PendingDecision);Assert.Equal(1,h.GetHand("P1")[0]);Assert.Equal(2,h.GetHand("P2")[1]);Assert.Empty(h.State.Skills.LossLedger);h.Conserved();
    }

    [Fact]
    public void WenjiWithOnlyBannedCardsEndsWithoutDeadlockOrTypeReward()
    {
        var h=Host("tk-g45");h.Inner.Give("P2",1);var action=B2SkillEngine.Actions(h,"P1").First(a=>a.Command.Option=="b4-wenji"&&a.Command.TargetPlayerIds[0]=="P2");B2SkillEngine.Resolve(h,"P1",action.Command);
        Assert.Null(h.State.Skills.PendingDecision);Assert.Equal(1,h.GetHand("P2")[0]);Assert.DoesNotContain(h.State.Generals[0].Counters,c=>c.Key=="wenji:0");h.Conserved();
    }

    [Fact]
    public void WenjiOffersOnlyReceivableActualTypes()
    {
        var h=Host("tk-g45");h.Inner.Give("P2",1,1);var action=B2SkillEngine.Actions(h,"P1").First(a=>a.Command.Option=="b4-wenji"&&a.Command.TargetPlayerIds[0]=="P2");B2SkillEngine.Resolve(h,"P1",action.Command);
        var choices=B2SkillEngine.Actions(h,"P2").Where(a=>a.Command.Option=="b4-give").ToArray();Assert.Single(choices);Assert.Equal(1,choices[0].Command.CardType);B2SkillEngine.Resolve(h,"P2",choices[0].Command);Assert.Equal(1,h.GetHand("P1")[1]);Assert.Equal(1,h.GetHand("P2")[0]);h.Conserved();
    }

    [Fact]
    public void LiyuCannotGiveForbiddenWheatAndReceiveKnightServiceForFree()
    {
        var h=Host("tk-g12","tk-g02",type:3);h.Inner.Give("P1",0,0,0,1);h.AddOption("liyu","activate",target:"P2",asset:"Knight",cost:new[]{0,0,0,1,0,0,0,0});
        Assert.DoesNotContain(B2SkillEngine.Actions(h,"P1"),a=>a.Command.Option=="b5m-liyu");Assert.Equal(1,h.GetHand("P1")[3]);Assert.Empty(h.Applied);h.Conserved();
    }

    [Fact]
    public void LiyuForgedBannedPaymentCannotReuseLegalConvertedPaymentAction()
    {
        var h=Host("tk-g42","tk-g02",type:3);h.Inner.Give("P1",0,0,1,1);h.AddOption("liyu","activate",target:"P2",asset:"Knight",cost:new[]{0,0,0,1,0,0,0,0});
        var legal=B2SkillEngine.Actions(h,"P1").Single(a=>a.Command.Option=="b5m-liyu");Assert.Equal(1,legal.Command.Counts[2]);legal.Command.Counts=new[]{0,0,0,1,0,0,0,0};var before=h.Snapshot();
        Assert.Throws<InvalidOperationException>(()=>B2SkillEngine.Resolve(h,"P1",legal.Command));Assert.Equal(before,h.Snapshot());Assert.Empty(h.Applied);h.Conserved();
    }
}
