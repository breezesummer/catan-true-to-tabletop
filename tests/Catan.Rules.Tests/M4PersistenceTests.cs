using System.Text.Json.Nodes;
using Catan.Core;
using Catan.Core.M4;
using Xunit;
using static Catan.Rules.Tests.M4CoreHarness;
using CommandKind=Catan.Core.M4.CommandKind;
using GamePhase=Catan.Core.M4.GamePhase;
using PlayerView=Catan.Core.M4.PlayerView;
using PublicPlayer=Catan.Core.M4.PublicPlayer;
using PendingDecision=Catan.Core.M4.PendingDecision;

namespace Catan.Rules.Tests;

public class M4PersistenceTests
{
    [Theory][InlineData(3)][InlineData(4)] public void SetupSaveRestoresExactAuthorityAndDeterministicNextRoll(int count)
    {
        var game=New(count);CompleteSetup(game);string save=game.Save();var loaded=CitiesKnightsGameSession.Load(AcceptanceFixture.Json,save);Assert.Equal(save,loaded.Save());
        var view=game.GetPlayerView("P1");var c=Cmd(CommandKind.RollDice,view.ActivePlayerId);Accepted(game,c);Accepted(loaded,c);Assert.Equal(game.Save(),loaded.Save());
    }
    [Fact] public void SetupPendingDecisionRestoresAndCompletesSameRoad()
    {
        var game=New();var view=game.GetPlayerView(game.GetPlayerView("P1").ActivePlayerId);Accepted(game,Cmd(CommandKind.SetupSettlement,view.PlayerId,view.LegalVertexIds.First()));
        var loaded=CitiesKnightsGameSession.Load(AcceptanceFixture.Json,game.Save());view=game.GetPlayerView(view.PlayerId);Assert.Equal(GamePhase.SetupRoad,view.Phase);var c=Cmd(CommandKind.SetupRoad,view.PlayerId,view.LegalEdgeIds.First());Accepted(game,c);Accepted(loaded,c);Assert.Equal(game.Save(),loaded.Save());
    }
    [Fact] public void ChangedRandomPendingOrCommoditySaveIsRejected()
    {
        var game=New();CompleteSetup(game);string save=game.Save();
        foreach(var path in new[]{"random","commodity","version"})
        {
            var json=JsonNode.Parse(save)!;
            if(path=="random")json["Random"]!["State"]=123u;
            else if(path=="commodity")json["CommodityBank"]!["Cloth"]=11;
            else json["RulesVersion"]="other";
            Assert.Throws<InvalidDataException>(()=>CitiesKnightsGameSession.Load(AcceptanceFixture.Json,json.ToJsonString()));
        }
    }
    [Fact] public void DuplicateCommandDoesNotSpendTwiceAndConflictingRetryIsRejected()
    {
        var game=Fixture(s=>Player(s).Resources=Bag(wood:4));var command=Cmd(CommandKind.BankTrade);command.GiveResource=Resource.Wood;command.ReceiveResource=Resource.Ore;Accepted(game,command);string save=game.Save();var duplicate=game.Execute(command);Assert.True(duplicate.IsDuplicate);Assert.Empty(duplicate.NewEvents);Assert.Equal(save,game.Save());command.ReceiveResource=Resource.Wheat;Rejected(game,command);
    }
    [Fact] public void PreviewDoesNotMutateDeckRandomOrCommodities()
    {
        var game=New();CompleteSetup(game);string before=game.Save();var command=Cmd(CommandKind.RollDice,game.GetPlayerView("P1").ActivePlayerId);Assert.True(game.Preview(command).Success);Assert.Equal(before,game.Save());
    }
    [Fact] public void ViewsAreDetachedAndContainNoAuthoritySecrets()
    {
        var game=Fixture(s=>{Player(s,"P2").Resources=Bag(ore:2);Player(s,"P2").Commodities.Coin=3;});var view=game.GetPlayerView("P1");var opponent=view.Players.Single(p=>p.Id=="P2");Assert.Equal(5,opponent.ResourceCount);Assert.Equal(0,opponent.CommodityCount);
        Assert.Null(typeof(PlayerView).GetProperty("Random"));Assert.Null(typeof(PlayerView).GetProperty("ProgressDecks"));Assert.Null(typeof(PublicPlayer).GetProperty("Resources"));
        string before=game.Save();view.Board.Tiles[0].Number=99;view.OwnCommodities.Cloth=12;view.Players[0].Improvements[0]=5;Assert.Equal(before,game.Save());
    }
    [Fact] public void PublicEventsDoNotExposeDiscardedCommodityKinds()
    {
        var game=Fixture(s=>{Player(s).Commodities.Coin=8;s.Phase=GamePhase.Discard;s.PendingDecision=new PendingDecision {Kind="Robber",PlayerId="P1",ReturnPhase=GamePhase.Action};s.Discards=new[]{new DiscardRequirement {PlayerId="P1",Amount=4}};});
        var c=Cmd(CommandKind.DiscardResources);c.Commodities=new CommodityBag {Coin=4};Accepted(game,c);var e=game.GetPlayerView("P2").Events.Last();Assert.Null(e.TargetId);Assert.DoesNotContain("Coin",e.Message);Assert.DoesNotContain("Coin",e.Kind);
    }
}
