using Catan.Network.Contracts;
using Xunit;
using M4 = Catan.Core.M4;
using M5 = Catan.Core.M5;

namespace Catan.Network.Tests;

public sealed class ThreeKingdomsWireTests
{
    [Fact]
    public void DisabledExpansionDoesNotAddFieldsToProtocolThree()
    {
        var game = M5.CombinedGameSession.Create("through-the-desert", 3, 419);
        var view = WireJson.Serialize(game.GetPlayerView("P1"));
        var command = WireJson.Serialize(new M4.Command { Id = "old", PlayerId = "P1", Kind = M4.CommandKind.RollDice });
        Assert.DoesNotContain("ThreeKingdoms", view);
        foreach (var name in new[] { "GeneralIds", "DecisionId", "WindowId" }) Assert.DoesNotContain(name, command);
        Assert.Equal(view, WireJson.Serialize(WireJson.Deserialize<M5.PlayerView>(view)));
    }

    [Fact]
    public void PrivateCandidateIdsOnlyReachTheirOwnerAndAiHasNoExtraGeneralPermission()
    {
        var game = M5.CombinedGameSession.Create("heading-for-new-shores", 4, 419, threeKingdoms: true);
        var authority = game.GetAuthoritativeStateForTesting();
        var owner = authority.ThreeKingdoms.PendingDecision.PlayerId;
        foreach (var player in authority.Players)
        {
            var view = game.GetPlayerView(player.Id);
            var wire = WireJson.Serialize(view);
            var restored = WireJson.Deserialize<M5.PlayerView>(wire);
            Assert.Equal(wire, WireJson.Serialize(restored));
            Assert.Equal(wire, WireJson.Serialize(game.GetAiPlayerView(player.Id)));
            if (player.Id == owner) Assert.Equal(4, restored.ThreeKingdoms.PendingDecision.CandidateIds.Length);
            else
            {
                Assert.Empty(restored.ThreeKingdoms.PendingDecision.CandidateIds);
                foreach (var id in authority.ThreeKingdoms.PendingDecision.CandidateIds) Assert.DoesNotContain(id, wire);
            }
            foreach (var forbidden in new[] { "GeneralDrawOrder", "ProcessedCommands", "EffectFrames", "RandomAlgorithmId", "InitialSeed" }) Assert.DoesNotContain(forbidden, wire);
        }
    }

    [Fact]
    public void NewCommandRoundTripsTokensAndRejectsUnknownFields()
    {
        var command = new M5.Command { Id = "choice", PlayerId = "P1", Kind = M5.CommandKind.ChooseGenerals, GeneralIds = new[] { "tk-g01", "tk-g02" }, DecisionId = "d1", WindowId = "w1" };
        var wire = WireJson.Serialize(command);
        var restored = WireJson.Deserialize<M5.Command>(wire);
        Assert.Equal(command.GeneralIds, restored.GeneralIds);
        Assert.Equal(command.DecisionId, restored.DecisionId);
        Assert.Equal(command.WindowId, restored.WindowId);
        Assert.ThrowsAny<Exception>(() => WireJson.Deserialize<M5.Command>("{\"ThreeKingdoms\":{}}"));
    }
}
