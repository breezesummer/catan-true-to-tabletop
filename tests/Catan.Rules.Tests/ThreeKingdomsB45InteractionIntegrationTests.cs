using Catan.Core.ThreeKingdoms;
using Xunit;

namespace Catan.Rules.Tests;

// 真实规则投影 → 桌面草稿 → 原规则入口；测试不直接构造合法动作。
public sealed class ThreeKingdomsB45InteractionIntegrationTests
{
    static B4ResponseTestHost Host(string general)
    {
        var h=B4ResponseTestHost.Create(general,"tk-g12","tk-g22");h.State.RulesVersion=GeneralCatalog.B45RulesVersion;
        h.State.PackageVersion=GeneralCatalog.B45PackageVersion;h.State.PackageContentHash=GeneralCatalog.B45ContentHash;return h;
    }
    static ThreeKingdomsView View(B4ResponseTestHost h,string viewer)
    {var v=ThreeKingdomsEngine.Project(h.State,viewer);B2SkillEngine.Project(h,v,viewer);B3DiceEngine.Project(h,v,viewer);return v;}
    static B45SelectionContext Draft(B4ResponseTestHost h,string viewer,string? skill=null)
    {var c=new B45SelectionContext();c.Begin(View(h,viewer),viewer,skill,h.GetHand(viewer));return c;}
    static void Execute(B4ResponseTestHost h,string player,B45SelectionContext c)
    {Assert.True(c.CanConfirm);var command=c.BeginSubmit();Assert.NotNull(command);B2SkillEngine.Resolve(h,player,command);}
    [Fact] public void RendeRealProjectionDirectCardsPortraitAndOneCommit()
    {
        var h=Host("tk-g15");h.Give("P1",3,0,0,0,0,2);var before=h.Snapshot();var c=Draft(h,"P1","tk-g15-s1");
        Assert.Single(c.Options);Assert.True(c.ToggleCard(0,2));Assert.True(c.ToggleCard(5,1));Assert.True(c.TogglePlayer("P2"));Assert.Equal(before,h.Snapshot());
        Execute(h,"P1",c);Assert.Equal(2,h.GetHand("P2").Sum());Assert.Equal(3,h.GetHand("P1").Sum());Assert.False(c.CanConfirm);h.AssertConserved();
    }
    [Fact] public void ZhihengRealProjectionExchangesFourWithNoPrepayment()
    {
        var h=Host("tk-g22");h.Give("P1",3,0,0,0,0,2);var before=h.Snapshot();var c=Draft(h,"P1","tk-g22-s1");
        Assert.True(c.ToggleCard(0,2));Assert.True(c.ToggleCard(0,1));Assert.True(c.ToggleCard(5,1));Assert.True(c.ToggleCard(5,0));Assert.Equal(before,h.Snapshot());
        Execute(h,"P1",c);Assert.Equal(5,h.GetHand("P1").Sum());Assert.DoesNotContain(B2SkillEngine.Actions(h,"P1"),a=>a.Command.Option=="exchange-hand");h.AssertConserved();
    }
    [Fact] public void GuoseRealProjectionRequiresBricksAndActualTarget()
    {
        var h=Host("tk-g05");h.Give("P1",0,3);var before=h.Snapshot();var c=Draft(h,"P1","tk-g05-s2");
        c.TogglePlayer("P2");c.ToggleCard(1,2);Assert.False(c.CanConfirm);c.ToggleCard(1,1);Assert.Equal(before,h.Snapshot());
        Execute(h,"P1",c);Assert.Equal(1,h.GetHand("P1")[1]);Assert.Contains(h.State.Skills.TimedEffects,e=>e.Kind=="response-indulgence-pending"&&e.TargetPlayerId=="P2");h.AssertConserved();
    }
    [Fact] public void LiuliRealProjectionMapsOpaqueMoveToStableTileWithoutParsingLabel()
    {
        var h=Host("tk-g05");h.Give("P1",0,0,0,2);h.TokenMoves=new[]{new B4TokenMove{OptionId="opaque1",TileId="tile-stable-1",TokenKind="robber",Label="任意显示文字"}};
        var before=h.Snapshot();var c=Draft(h,"P1","tk-g05-s1");Assert.True(c.CanSelectTile("tile-stable-1"));c.ToggleTile("tile-stable-1");c.ToggleCard(3,1);Assert.Equal(before,h.Snapshot());
        Execute(h,"P1",c);Assert.Equal("tile-stable-1",Assert.Single(h.Moves).TileId);Assert.Equal(1,h.GetHand("P1")[3]);h.AssertConserved();
    }
    [Fact] public void WenjiTargetRespondsThroughTheirOwnHandWithoutOrdinaryTurnOwnership()
    {
        var h=Host("tk-g45");h.Give("P2",2,0,0,0,0,1);var c=Draft(h,"P1","tk-g45-s1");c.TogglePlayer("P2");Execute(h,"P1",c);
        var response=Draft(h,"P2");Assert.False(response.CanCancel);Assert.True(response.ToggleCard(5,0));Execute(h,"P2",response);
        Assert.Equal(1,h.GetHand("P1")[5]);Assert.Equal(2,h.GetHand("P2").Sum());h.AssertConserved();
    }
    [Theory][InlineData("tk-g06","tk-g06-s1",4)][InlineData("tk-g13","tk-g13-s1",4)]
    public void OptionalBankChoiceUsesActualBatchAndSkipsInitialActivation(string general,string skill,int count)
    {
        var h=Host(general);h.State.Clock.Phase=ThreeKingdomsPhase.End;B2SkillEngine.OnEnd(h,"结束");var before=h.Snapshot();var c=Draft(h,"P1",skill);
        Assert.True(c.RequiresBankCards);Assert.Equal(count,c.RequiredBankCount);Assert.NotNull(c.SkipCommand);Assert.DoesNotContain(c.Options,a=>a.Command.Option=="accept");
        for(int i=0;i<count;i++)Assert.True(c.ToggleBankCard(0));Assert.Equal(before,h.Snapshot());Execute(h,"P1",c);Assert.Equal(count,h.GetHand("P1")[0]);h.AssertConserved();
    }
    [Fact] public void QiceDirectResponseUsesAuthorizedVirtualOptionWithoutTargetLeak()
    {
        var h=Host("tk-g34");h.Give("P1",2,1);h.State.Clock.Phase=ThreeKingdomsPhase.Preparation;B2SkillEngine.OnB4Preparation(h,"奇策开始");
        var before=h.Snapshot();var c=Draft(h,"P1","tk-g34-s1");Assert.Single(c.Options);Assert.Equal("b45-optional-qice",c.Options[0].Command.Option);Assert.True(c.CanConfirm);Assert.Equal(before,h.Snapshot());
        Execute(h,"P1",c);Assert.Single(h.VirtualUses);Assert.Equal(0,h.GetHand("P1").Sum());h.AssertConserved();
    }
    [Fact] public void XiashuDirectSubsetAndHiddenPileKeepSeparateAuthoritySteps()
    {
        var h=Host("tk-g26");h.Give("P1",2);h.Give("P2",0,1);var active=Draft(h,"P1","tk-g26-s1");active.TogglePlayer("P2");Execute(h,"P1",active);
        var subset=Draft(h,"P2");Assert.Empty(subset.Options);Assert.True(subset.ToggleCard(0,1));Assert.Equal("b45-selected",subset.BuildCommand().Option);Execute(h,"P2",subset);
        var piles=Draft(h,"P1");Assert.Equal(new[]{"b4-revealed","b4-hidden"},piles.Options.Select(a=>a.Command.Option));piles.ChooseOption(piles.Options.Single(a=>a.Command.Option=="b4-hidden"));Execute(h,"P1",piles);Assert.Equal(2,h.GetHand("P1").Sum());h.AssertConserved();
    }
    [Fact] public void PoxiActualPrivateProjectionAndDraftDoNotRefundCommittedUse()
    {
        var h=Host("tk-g08");h.Give("P1",1,1);h.Give("P2",0,0,1,1);var active=Draft(h,"P1","tk-g08-s2");active.TogglePlayer("P2");Execute(h,"P1",active);
        Assert.Equal(1,h.State.Generals[0].Counters.Single(c=>c.Key=="poxi").Value);Assert.Empty(View(h,"P3").SkillActions);
        var c=Draft(h,"P1");var before=h.Snapshot();c.ToggleCard(0,0);c.ToggleCard(1,0);c.TogglePrivateCard(2);c.TogglePrivateCard(3);c.Reselect();Assert.Equal(before,h.Snapshot());
        c.ToggleCard(0,0);c.ToggleCard(1,0);c.TogglePrivateCard(2);c.TogglePrivateCard(3);Execute(h,"P1",c);Assert.Equal(0,h.GetHand("P1").Sum());Assert.Equal(0,h.GetHand("P2").Sum());Assert.Equal(1,h.State.Generals[0].Counters.Single(x=>x.Key=="poxi").Value);h.AssertConserved();
    }
}
