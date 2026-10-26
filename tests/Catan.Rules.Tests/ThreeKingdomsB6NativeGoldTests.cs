using System.Runtime.Serialization.Json;
using Catan.Core;
using Catan.Core.ThreeKingdoms;
using Catan.Core.M5;
using GameState=Catan.Core.M5.GameState;
using GamePhase=Catan.Core.M5.GamePhase;
using CommandKind=Catan.Core.M5.CommandKind;
using PendingDecision=Catan.Core.M5.PendingDecision;
using Xunit;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB6NativeGoldTests
{
    static CombinedGameSession Game(int wool,int brick=0)
    {
        return M5Harness.Fixture(s=>
        {
            s.RulesVersion=CombinedGameSession.ThreeKingdomsB6RulesVersion;s.ThreeKingdoms=ThreeKingdomsB6ControlTests.State();s.ThreeKingdoms.ControlB6=null;
            s.ThreeKingdoms.Skills.TimedEffects=new[]{new B2TimedEffect{Id="native-gold-jinglue",Kind="b6-jinglue:0",PlayerId="P2",OpportunityId=s.ThreeKingdoms.Clock.OpportunityId}};
            foreach(var p in s.Players)p.Resources=new ResourceBag();s.Players[2].Resources=new ResourceBag{Brick=19-brick,Wool=19-wool,Wheat=19,Ore=19};
            s.Settlements=new[]{M5Harness.Piece(s.Tiles.First(t=>t.Resource!="sea").Vertices[0],"P1")};
        });
    }
    static CombinedGameSession Restore(CombinedGameSession game)
    {
        using var stream=new MemoryStream(System.Text.Encoding.UTF8.GetBytes(game.Save()));var state=(GameState)new DataContractJsonSerializer(typeof(GameState)).ReadObject(stream)!;
        var copy=M5Harness.New();M5Harness.SetState(copy,state);return copy;
    }
    static void QueueGold(CombinedGameSession game,int amount)
    {
        var s=game.GetAuthoritativeStateForTesting();M5Harness.Invoke(game,"AddGold",s,"P1",amount);M5Harness.Invoke(game,"ContinueDecisions",s);M5Harness.SetState(game,s);
    }
    [Theory][InlineData(1)][InlineData(0)]
    public void NativeGoldCapsRewardToGainableSupplyAndContinuesAfterRestore(int available)
    {
        var game=Game(available);QueueGold(game,2);var view=game.GetPlayerView("P1");var restored=Restore(game);
        Assert.Equal(new[]{0},view.ThreeKingdoms.BlockedGainCardTypes);Assert.Empty(game.GetPlayerView("P2").ThreeKingdoms.BlockedGainCardTypes);
        if(available==0){Assert.Null(view.PendingDecision);Assert.Equal(GamePhase.Action,view.Phase);Assert.Equal(0,view.OwnResources.Total);}
        else
        {
            Assert.Equal("Gold",view.PendingDecision.Kind);Assert.Equal(1,view.PendingDecision.Amount);
            var choices=view.LegalActions.Where(c=>c.Kind==CommandKind.ResolveChoice).ToArray();Assert.NotEmpty(choices);Assert.All(choices,c=>{Assert.Equal(0,c.Resources.Wood);Assert.Equal(1,c.Resources.Wool);});
            var forbidden=M5Harness.Cmd(CommandKind.ResolveChoice);forbidden.WindowId=view.ThreeKingdoms.WindowId;forbidden.Resources=new ResourceBag{Wood=1};string before=game.Save();Assert.False(game.Preview(forbidden).Success);Assert.Equal(before,game.Save());
            var take=choices[0];take.Id=ThreeKingdomsTestGame.NextId();M5Harness.Accepted(game,take);M5Harness.Accepted(restored,take);
            Assert.Equal(1,game.GetPlayerView("P1").OwnResources.Wool);Assert.Null(game.GetPlayerView("P1").PendingDecision);
        }
        Assert.Equal(game.Save(),restored.Save());game.AssertInvariants();
    }
    [Fact] public void NativeGoldMixedCandidateExcludesForbiddenBankStock()
    {
        var game=Game(1,1);QueueGold(game,2);var view=game.GetPlayerView("P1");var choice=Assert.Single(view.LegalActions,c=>c.Kind==CommandKind.ResolveChoice&&c.Resources.Brick==1&&c.Resources.Wool==1);
        Assert.Equal(0,choice.Resources.Wood);choice.Id=ThreeKingdomsTestGame.NextId();M5Harness.Accepted(game,choice);Assert.Equal(2,game.GetPlayerView("P1").OwnResources.Total);
    }
    [Fact] public void NativeAqueductSkipsWhenOnlyForbiddenResourcesRemain()
    {
        var game=Game(0);var state=game.GetAuthoritativeStateForTesting();M5Harness.Invoke(game,"QueueDecision",state,new PendingDecision{Kind="Aqueduct",PlayerId="P1"});M5Harness.Invoke(game,"ContinueDecisions",state);M5Harness.SetState(game,state);
        Assert.Null(game.GetPlayerView("P1").PendingDecision);Assert.Equal(GamePhase.Action,game.GetPlayerView("P1").Phase);game.AssertInvariants();
    }
}
