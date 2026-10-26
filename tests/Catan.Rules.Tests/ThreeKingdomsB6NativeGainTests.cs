using System.Reflection;
using Catan.Core;
using Catan.Core.ThreeKingdoms;
using M4=Catan.Core.M4;
using M5=Catan.Core.M5;
using Xunit;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB6NativeGainTests
{
    private static void Configure(dynamic s,string mode,string kind)
    {
        s.RulesVersion=mode=="cities"?M4.CitiesKnightsGameSession.ThreeKingdomsB6RulesVersion:M5.CombinedGameSession.ThreeKingdomsB6RulesVersion;
        s.ThreeKingdoms=ThreeKingdomsB6ControlTests.State();s.ThreeKingdoms.ControlB6=null;
        s.ThreeKingdoms.Skills.TimedEffects=new[]{new B2TimedEffect{Id="ban",Kind="b6-jinglue:0",PlayerId="P2",OpportunityId=1}};
        s.Players[0].Resources.Wood=1;s.Players[1].Resources.Wood=2;
        if(kind=="monopoly")s.Players[0].ProgressCards=(dynamic)(mode=="cities"?(object)new[]{M4.ProgressCardKind.ResourceMonopoly}:new[]{M5.ProgressCardKind.ResourceMonopoly});
        if(kind=="dues")s.Players[0].ProgressCards=(dynamic)(mode=="cities"?(object)new[]{M4.ProgressCardKind.GuildDues}:new[]{M5.ProgressCardKind.GuildDues});
        if(kind=="dues"){s.Players[1].Resources.Brick=1;s.Players[1].DefenderPoints=2;}
        if(kind=="aqueduct")
        {
            s.Phase=(dynamic)(mode=="cities"?(object)M4.GamePhase.PendingChoice:M5.GamePhase.PendingChoice);
            s.PendingDecision=(dynamic)(mode=="cities"?(object)new M4.PendingDecision{Kind="Aqueduct",PlayerId="P1"}:new M5.PendingDecision{Kind="Aqueduct",PlayerId="P1"});
        }
    }
    private static dynamic Game(string mode,string kind)=>mode=="cities"?(object)M4CoreHarness.Fixture(s=>Configure(s,mode,kind)):M5Harness.Fixture(s=>Configure(s,mode,kind));
    private static dynamic Cmd(dynamic game,string mode,string kind)
    {dynamic c=(dynamic)(mode=="cities"?(object)new M4.Command{Kind=Enum.Parse<M4.CommandKind>(kind)}:new M5.Command{Kind=Enum.Parse<M5.CommandKind>(kind)});c.Id=ThreeKingdomsTestGame.NextId();c.PlayerId="P1";c.WindowId=game.GetPlayerView("P1").ThreeKingdoms.WindowId;return c;}
    [Theory][InlineData("cities")][InlineData("combined")]
    public void MonopolyLeavesForbiddenCardsWithEveryVictim(string mode)
    {
        dynamic game=Game(mode,"monopoly"),c=Cmd(game,mode,"PlayProgressCard");c.ProgressCard=(dynamic)(mode=="cities"?(object)M4.ProgressCardKind.ResourceMonopoly:M5.ProgressCardKind.ResourceMonopoly);c.Resource=Resource.Wood;
        dynamic result=game.Execute(c);Assert.True((bool)result.Success,(string)result.Message);
        Assert.Equal(1,(int)game.GetPlayerView("P1").OwnResources.Wood);Assert.Equal(2,(int)game.GetPlayerView("P2").OwnResources.Wood);game.AssertInvariants();
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void GuildDuesCapsSelectionToGainableRealCardsWithoutStealingForbiddenOnes(string mode)
    {
        dynamic game=Game(mode,"dues"),c=Cmd(game,mode,"PlayProgressCard");c.ProgressCard=(dynamic)(mode=="cities"?(object)M4.ProgressCardKind.GuildDues:M5.ProgressCardKind.GuildDues);c.OtherPlayerId="P2";
        dynamic r=game.Execute(c);Assert.True((bool)r.Success,(string)r.Message);Assert.Equal(1,(int)game.GetPlayerView("P1").PendingDecision.Amount);
        dynamic answer=Cmd(game,mode,"ResolveProgressChoice");answer.Resources=new ResourceBag{Brick=1};r=game.Execute(answer);Assert.True((bool)r.Success,(string)r.Message);
        Assert.Equal(1,(int)game.GetPlayerView("P1").OwnResources.Brick);Assert.Equal(2,(int)game.GetPlayerView("P2").OwnResources.Wood);game.AssertInvariants();
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void AqueductRejectsForbiddenChoiceAndStillCompletesWithAllowedCard(string mode)
    {
        dynamic game=Game(mode,"aqueduct"),c=Cmd(game,mode,"ResolveChoice");c.Resource=Resource.Wood;
        string saved=game.Save();Assert.Equal("GainForbidden",(string)game.Execute(c).ErrorCode);Assert.Equal(saved,(string)game.Save());
        c.Id=ThreeKingdomsTestGame.NextId();c.Resource=Resource.Brick;dynamic r=game.Execute(c);Assert.True((bool)r.Success,(string)r.Message);game.AssertInvariants();
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void RandomRobberDrawLeavesForbiddenCardAtSource(string mode)
    {
        dynamic game=Game(mode,"robber"),s=game.GetAuthoritativeStateForTesting();
        game.GetType().GetMethod("StealHandCard",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(game,new object[]{s,s.Players[0],s.Players[1]});
        Assert.Equal(1,(int)s.Players[0].Resources.Wood);Assert.Equal(2,(int)s.Players[1].Resources.Wood);
    }
}
