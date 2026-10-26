using Catan.Core.ThreeKingdoms;
using Xunit;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB45DevIntegrationTests
{
    static ThreeKingdomsB5CivilTestHost Host(string general)
    {
        var h=ThreeKingdomsB5CivilTestHost.Create(general);
        h.State.RulesVersion=GeneralCatalog.B5DirectRulesVersion;h.State.PackageVersion=GeneralCatalog.B5DirectPackageVersion;h.State.PackageContentHash=GeneralCatalog.B5DirectContentHash;
        return h;
    }
    static ThreeKingdomsView View(ThreeKingdomsB5CivilTestHost h)
    {var v=ThreeKingdomsEngine.Project(h.State,"P1");B2SkillEngine.Project(h,v,"P1");return v;}

    [Fact]
    public void IntegratedPackageKeepsFortyNineGeneralsAndRejectsMismatchedLegacyIdentity()
    {
        var state=ThreeKingdomsEngine.Create(new[]{"P1","P2","P3","P4"},42,_=>0,true,GeneralCatalog.B5DirectPackageVersion);
        Assert.Equal(49,state.Generals.Length);Assert.True(ThreeKingdomsEngine.HasSupportedPackage(state));
        Assert.NotNull(state.Board);Assert.NotNull(state.Military);Assert.NotNull(state.CivilB5);
        Assert.True(GeneralCatalog.IsNetworkPackage(state.PackageVersion,state.PackageContentHash));
        Assert.False(GeneralCatalog.IsNetworkPackage(state.PackageVersion,GeneralCatalog.B5ContentHash));
        state.PackageContentHash=GeneralCatalog.B5ContentHash;Assert.False(ThreeKingdomsEngine.HasSupportedPackage(state));
        Assert.Equal(87,GeneralCatalog.B5Definitions.SelectMany(g=>g.Skills).Count());
        Assert.All(GeneralCatalog.B5Definitions.SelectMany(g=>g.Skills),s=>Assert.Equal(s.Name,B45SkillCatalog.Get(s.Id).Name));
    }
    [Fact]
    public void B5BoardSkillCanBeSelectedDirectlyWithoutAutomaticPromptOrStateMutation()
    {
        var h=Host("tk-g16");h.AddOption("kurou","E000","P1",paid:2);
        string before=h.Snapshot();var v=View(h);
        Assert.False(B4AutomationEngine.Offer(h));Assert.Equal(before,h.Snapshot());
        Assert.Contains(B45SkillCatalog.AvailableEntries(v,"P1"),s=>s.SkillId=="tk-g16-s1"&&s.HasActiveEntry);
        var selection=new B45SelectionContext();selection.Begin(v,"P1","tk-g16-s1",h.GetHand("P1"));
        Assert.Null(selection.BuildCommand());Assert.True(selection.ToggleTile("E000"));
        var command=selection.BuildCommand();Assert.NotNull(command);Assert.Equal("civil-kurou",command.Option);Assert.Equal("E000",command.ProgressCardId);
        Assert.Equal(before,h.Snapshot());B2SkillEngine.Resolve(h,"P1",command);Assert.Single(h.Applied);
    }
    [Fact]
    public void B5DirectPaymentUsesOwnRealCardsAndPreservesConservation()
    {
        var h=Host("tk-g17");h.Inner.Give("P1",1,1);h.Inner.Give("P2",1);var before=h.Snapshot();var view=View(h);
        var action=view.SkillActions.Single(a=>a.Command.Option=="civil-qixi-hand"&&a.Command.TargetPlayerIds[0]=="P2");
        var selection=new B45SelectionContext();selection.Begin(view,"P1","tk-g17-s1",h.GetHand("P1"));selection.ChooseOption(action);
        Assert.False(selection.ToggleHand(0,0));Assert.True(selection.ToggleHand(1,0));Assert.True(selection.TogglePlayer("P2"));
        var command=selection.BuildCommand();Assert.NotNull(command);Assert.Equal(before,h.Snapshot());
        B2SkillEngine.Resolve(h,"P1",command);Assert.Equal(0,h.GetHand("P1")[1]);h.Conserved();
    }
    [Fact]
    public void NormalRepairRemainsReachableAlongsideGeneralSkillsAndRequiresItsExactPayment()
    {
        var h=Host("tk-g19");h.Inner.Give("P1",1,1);h.AddOption("repair-own","E001","P1",cost:new[]{1,1});var view=View(h);
        Assert.Contains(B45SkillCatalog.AvailableEntries(view,"P1"),s=>s.SkillId==B45SkillCatalog.RepairSkillId&&s.HasActiveEntry);
        var selection=new B45SelectionContext();selection.Begin(view,"P1",B45SkillCatalog.RepairSkillId,h.GetHand("P1"));
        Assert.True(selection.ToggleTile("E001"));Assert.True(selection.ToggleHand(0,0));Assert.Null(selection.BuildCommand());Assert.True(selection.ToggleHand(1,0));
        var command=selection.BuildCommand();Assert.NotNull(command);Assert.Null(command.SkillId);Assert.Equal("b5-repair",command.Option);Assert.Equal(new[]{1,1,0,0,0,0,0,0},command.Counts);
    }
    [Fact]
    public void TechnologyTrackSelectionDoesNotBecomeAnInventedHandPayment()
    {
        var action=new B2SkillAction {CardSelectionKind="payment",RealCardType=1,Command=new B2SkillCommand {Option="civil-pojun-track",CardType=1}};
        Assert.Equal(new int[8],B45SelectionContract.Cost(action));
    }
    [Fact]
    public void B5LegacyKeepsItsAutomaticOfferWhileIntegratedFreePlayIsOptional()
    {
        var h=ThreeKingdomsB5CivilTestHost.Create("tk-g16");h.AddOption("kurou","E002","P1",paid:2);
        Assert.True(B4AutomationEngine.Offer(h));Assert.Equal("automation-offer",h.State.Automation.PendingDecision.Kind);
        var direct=Host("tk-g16");direct.AddOption("kurou","E002","P1",paid:2);Assert.False(B4AutomationEngine.Offer(direct));Assert.Null(direct.State.Automation);
    }
    [Fact]
    public void QingnangKeepsTheSelectedRealCardAndDeclaredUse()
    {
        var h=Host("tk-g54");h.Inner.Give("P1",1,1);h.AddOption("repair","E003","P1",cost:new[]{1,0});var view=View(h);
        var action=view.SkillActions.Single(a=>a.Command.Option=="civil-qingnang"&&a.Command.CardType==1);
        var selection=new B45SelectionContext();selection.Begin(view,"P1","tk-g54-s1",h.GetHand("P1"));selection.ChooseOption(action);
        Assert.True(selection.ToggleTile("E003"));Assert.True(selection.ToggleHand(1,0));var command=selection.BuildCommand();Assert.NotNull(command);
        Assert.Equal(1,command.CardType);Assert.Equal(1,command.EffectiveCardType);B2SkillEngine.Resolve(h,"P1",command);Assert.Equal(0,h.GetHand("P1")[1]);h.Conserved();
    }
}
