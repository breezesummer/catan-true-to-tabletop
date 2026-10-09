using System.Text.Json;
using System.Text.Json.Nodes;
using Catan.Core;
using Catan.Core.M2;
using Xunit;
using static Catan.Rules.Tests.M2Harness;
using Command = Catan.Core.M2.Command;
using CommandKind = Catan.Core.M2.CommandKind;
using GamePhase = Catan.Core.M2.GamePhase;

namespace Catan.Rules.Tests;

public sealed class M2PersistenceTests
{
    [Theory]
    [InlineData("SaveFormatVersion")]
    [InlineData("RulesVersion")]
    [InlineData("ScenarioId")]
    [InlineData("ScenarioVersion")]
    [InlineData("ScenarioContentHash")]
    [InlineData("InitialSeed")]
    [InlineData("Random")]
    [InlineData("Bank")]
    [InlineData("DevelopmentDeck")]
    [InlineData("PendingDecision")]
    [InlineData("ProcessedCommands")]
    [InlineData("Events")]
    public void AlteredAuthorityCannotLoadEvenWhenItLooksPlausible(string field)
    {
        var game = New();
        var view = game.GetPlayerView("P1");
        var player = view.ActivePlayerId;
        Accepted(game, Cmd(CommandKind.SetupSettlement, player, game.GetPlayerView(player).LegalVertexIds.First()));
        var save = JsonNode.Parse(game.Save())!;
        Assert.NotNull(save[field]);
        switch (field)
        {
            case "SaveFormatVersion": save[field] = 999; break;
            case "InitialSeed": save[field] = 567u; break;
            case "Random": save[field]!["State"] = 123u; break;
            case "Bank": save[field]!["wood"] = 18; break;
            case "DevelopmentDeck":
                var array = save[field]!.AsArray();
                var first = array[0]!.DeepClone();
                var other = Enumerable.Range(1, array.Count - 1).First(i => array[i]!.ToJsonString() != first.ToJsonString());
                array[0] = array[other]!.DeepClone(); array[other] = first; break;
            case "PendingDecision": save[field]!["AnchorVertexId"] = "V54"; break;
            case "ProcessedCommands": save[field]![0]!["Fingerprint"] = "changed"; break;
            case "Events": save[field]![0]!["Sequence"] = 2; break;
            default: save[field] = "unsupported"; break;
        }
        Assert.Throws<InvalidDataException>(() => BaseGameSession.Load(AcceptanceFixture.Json, save.ToJsonString()));
    }

    [Fact]
    public void SaveRestoresSetupDecisionRandomStateAndCommandDeduplication()
    {
        var game = New(seed: 123);
        var player = game.GetPlayerView("P1").ActivePlayerId;
        var command = Cmd(CommandKind.SetupSettlement, player, game.GetPlayerView(player).LegalVertexIds.First());
        var receipt = Accepted(game, command);
        var restored = BaseGameSession.Load(AcceptanceFixture.Json, game.Save());
        Assert.Equal(game.Save(), restored.Save());
        var retry = restored.Execute(command);
        Assert.True(retry.Success); Assert.True(retry.IsDuplicate); Assert.Empty(retry.NewEvents);
        Assert.Equal(RulesHarness.Canonical(receipt.Events), RulesHarness.Canonical(retry.Events));
        Assert.Equal(game.Save(), restored.Save());
        var conflict = Cmd(CommandKind.SetupSettlement, player, "V54"); conflict.Id = command.Id; Rejected(restored, conflict);
        var road = Cmd(CommandKind.SetupRoad, player, game.GetPlayerView(player).LegalEdgeIds.First());
        Accepted(game, road); Accepted(restored, road);
        Assert.Equal(game.Save(), restored.Save());
        var save = JsonNode.Parse(game.Save())!;
        Assert.Equal(BaseGameSession.RulesVersion, save["RulesVersion"]!.GetValue<string>());
        Assert.Equal(BaseGameSession.ScenarioVersion, save["ScenarioVersion"]!.GetValue<string>());
        Assert.True(save["Random"]!["Draws"]!.GetValue<long>() > 0);
        Assert.NotNull(save["Random"]!["State"]);
    }

    [Fact]
    public void TestFixtureCannotBeLoadedAsAProductionSaveAndM1SaveCannotBecomeM2()
    {
        var game = Fixture(s => Player(s).Resources = Bag(wood: 1));
        Assert.Throws<InvalidDataException>(() => BaseGameSession.Load(AcceptanceFixture.Json, game.Save()));
        Assert.Throws<InvalidDataException>(() => BaseGameSession.Load(AcceptanceFixture.Json, RulesHarness.New().Save()));
    }

    [Fact]
    public void ViewsAndPublicReceiptsExposeOnlyOwnCardsAndNoRandomDeckOrPrivateCommandHistory()
    {
        var game = Fixture(s => { Player(s).Resources = Bag(2, 1, 3, 1, 1); Player(s, "P2").Resources = Bag(ore: 7); Card(s, DevelopmentCardKind.VictoryPoint, "P2"); });
        var purchase = Cmd(CommandKind.BuyDevelopmentCard, target: "private-extra-payload");
        purchase.OtherPlayerId = "private-extra-payload";
        var receipt = Accepted(game, purchase);
        var player = RulesHarness.Node(game.GetPlayerView("P1"));
        var other = RulesHarness.Node(game.GetPlayerView("P2"));
        Assert.Single(player["ownDevelopmentCards"]!.AsArray());
        var allowedPlayer = new[] { "id", "resourceCount", "developmentCardCount", "playedKnights", "victoryPoints", "longestRoadLength", "pieces" };
        foreach (var view in new[] { player, other })
        {
            foreach (var item in view["players"]!.AsArray())
                Assert.All(item!.AsObject().Select(kv => kv.Key), key => Assert.Contains(key, allowedPlayer));
            var properties = PropertyNames(view).ToArray();
            foreach (var forbidden in new[] { "random", "state", "seed", "initialSeed", "draws", "developmentDeck", "processedCommands", "fingerprint" })
                Assert.DoesNotContain(forbidden, properties);
        }
        Assert.Equal(RulesHarness.Canonical(game.GetPlayerView("P1").Events), RulesHarness.Canonical(game.GetPlayerView("P2").Events));
        foreach (var ev in RulesHarness.Node(receipt.NewEvents).AsArray())
            Assert.All(ev!.AsObject().Select(kv => kv.Key), name => Assert.Contains(name,
                new[] { "sequence", "commandId", "kind", "playerId", "targetId", "otherPlayerId", "dice1", "dice2", "message" }));
        Assert.DoesNotContain("Knight", receipt.NewEvents.Single().Message);
        Assert.DoesNotContain("VictoryPoint", receipt.NewEvents.Single().Message);
        Assert.DoesNotContain("private-extra-payload", RulesHarness.Canonical(game.GetPlayerView("P2").Events));
    }

    [Fact]
    public void DetachedViewsCommandsReceiptsAndAuthoritativeSnapshotsCannotMutateSession()
    {
        var game = Fixture(s => { s.Settlements = new[] { Piece("V01") }; Player(s).Resources = Bag(wood: 2, brick: 2); Card(s, DevelopmentCardKind.Monopoly); });
        var command = Cmd(CommandKind.BuildRoad, target: "E02"); var receipt = Accepted(game, command);
        var before = game.Save(); var view = game.GetPlayerView("P1"); var state = game.GetAuthoritativeStateForTesting();
        view.OwnResources.Wood = 900; view.OwnDevelopmentCards[0].BoughtTurn = 999; view.Bank.Ore = 999;
        view.Board.Edges[0].Vertices[0] = "bad"; view.Board.Ports[0].Vertices[0] = "bad";
        view.Board.Roads[0].PlayerId = "P4"; view.Players[0].Pieces.Roads = 999;
        view.Events[0].Message = "changed"; receipt.Events[0].Message = "changed"; receipt.NewEvents[0].Kind = "changed";
        state.Random.State = 888; state.DevelopmentDeck[0] = DevelopmentCardKind.VictoryPoint; state.Players[0].Resources.Wood = 900;
        command.Id = "changed"; command.TargetId = "E72";
        Assert.Equal(before, game.Save()); Invariants(game);
    }

    [Theory]
    [InlineData("unknownSeat")]
    [InlineData("wrongSeat")]
    [InlineData("wrongPhase")]
    [InlineData("unknownKind")]
    [InlineData("unknownResource")]
    [InlineData("negativeAmount")]
    [InlineData("overflowAmount")]
    [InlineData("unknownLocation")]
    public void MalformedOrUnauthorizedCommandsAreAtomic(string reason)
    {
        var game = Fixture(s => Player(s).Resources = Bag(wood: 4));
        var command = Cmd(CommandKind.BankTrade); command.GiveResource = Resource.Wood; command.ReceiveResource = Resource.Ore;
        switch (reason)
        {
            case "unknownSeat": command.PlayerId = "P99"; break;
            case "wrongSeat": command.PlayerId = "P2"; break;
            case "wrongPhase": command.Kind = CommandKind.RollDice; break;
            case "unknownKind": command.Kind = (CommandKind)999; break;
            case "unknownResource": command.ReceiveResource = (Resource)999; break;
            case "negativeAmount": command.ReceiveAmount = -1; break;
            case "overflowAmount": command.ReceiveAmount = int.MaxValue; command.GiveAmount = -4; break;
            case "unknownLocation": command.Kind = CommandKind.BuildRoad; command.TargetId = "E999"; break;
        }
        Rejected(game, command);
    }

    [Fact]
    public void PreviewHasNoEffectsAndSharesExecuteValidation()
    {
        var game = Fixture(s => { s.Settlements = new[] { Piece("V01") }; Player(s).Resources = Bag(wood: 1, brick: 1); });
        var valid = Cmd(CommandKind.BuildRoad, target: "E02"); var invalid = Cmd(CommandKind.BuildRoad, target: "E72");
        var before = game.Save();
        Assert.True(game.Preview(valid).Success); Assert.False(game.Preview(invalid).Success);
        Assert.Equal(before, game.Save()); Accepted(game, valid); Rejected(game, invalid);
    }

    [Fact]
    public void PendingTradeSurvivesProductionSaveAndResolvesWithSameReceipt()
    {
        var game = New(seed: 1234); CompleteSetup(game);
        var driver = new M2ViewOnlyDriver();
        for (var i = 0; i < 20 && game.GetPlayerView("P1").Phase != GamePhase.Action; i++)
        {
            var state = game.GetPlayerView("P1");
            var actor = state.Phase == GamePhase.Discard ? state.Discards[0].PlayerId : state.ActivePlayerId;
            var command = driver.Decide(game.GetPlayerView(actor)); command.Id = $"pending-trade-prepare-{i}"; Accepted(game, command);
        }
        var current = game.GetPlayerView(game.GetPlayerView("P1").ActivePlayerId);
        Assert.Equal(GamePhase.Action, current.Phase);
        var give = Resources.First(r => current.OwnResources[r] > 0);
        var receive = Resources.First(r => r != give);
        var recipient = current.Players.First(p => p.Id != current.PlayerId).Id;
        var offer = Cmd(CommandKind.ProposeTrade, current.PlayerId); offer.OtherPlayerId = recipient; offer.Give = Bag(); offer.Give[give] = 1; offer.Receive = Bag(); offer.Receive[receive] = 1;
        Accepted(game, offer);
        var restored = BaseGameSession.Load(AcceptanceFixture.Json, game.Save());
        Assert.Equal(game.Save(), restored.Save());
        Rejected(restored, Cmd(CommandKind.EndTurn, current.PlayerId));
        var reject = Cmd(CommandKind.RejectTrade, recipient);
        var result = Accepted(game, reject); var restoredResult = Accepted(restored, reject);
        Assert.Equal(game.Save(), restored.Save());
        Assert.Equal(RulesHarness.Canonical(result.Events), RulesHarness.Canonical(restoredResult.Events));
        Assert.Null(restored.GetPlayerView(recipient).TradeOffer);
    }

    private static IEnumerable<string> PropertyNames(JsonNode node)
    {
        if (node is JsonObject obj)
            foreach (var item in obj)
            { yield return item.Key; if (item.Value != null) foreach (var child in PropertyNames(item.Value)) yield return child; }
        else if (node is JsonArray array)
            foreach (var item in array)
                if (item != null) foreach (var child in PropertyNames(item)) yield return child;
    }
}
