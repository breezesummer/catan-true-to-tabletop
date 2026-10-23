using Catan.Network.Contracts;
using Catan.AI;
using Catan.Core.ThreeKingdoms;
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
        foreach (var name in new[] { "GeneralIds", "DecisionId", "WindowId", "SkillCommand", "PaymentCards", "GiveEffectiveCardType", "CancelRetirementIntent", "RetireAtPhase" }) Assert.DoesNotContain(name, command);
        Assert.Equal(view, WireJson.Serialize(WireJson.Deserialize<M5.PlayerView>(view)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PrivateCandidateIdsOnlyReachTheirOwnerAndAiHasNoExtraGeneralPermission(bool skills)
    {
        var game = M5.CombinedGameSession.Create("heading-for-new-shores", 4, 419, threeKingdoms: true, threeKingdomsSkills: skills,threeKingdomsPackageVersion:skills?GeneralCatalog.B3PackageVersion:GeneralCatalog.PackageVersion);
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

    [Fact]
    public void SkillCommandsRoundTripAllParametersWithoutAllowingNewRootTypes()
    {
        var skill = new B2SkillCommand { SkillId = "tk-g01-s1", Option = "raid", TargetPlayerIds = ["P2", "P3"], Counts = [1, 0, 1, 0, 0, 0, 0, 0],
            TileIds = ["tile-a", "tile-b"], ProgressCardId = "card-a", DecisionId = "skill-choice-1", WindowId = "skill-window-1", CardType = 4 };
        var city = new M4.Command { Id = "skill-a", PlayerId = "P1", Kind = M4.CommandKind.UseGeneralSkill, SkillCommand = skill, WindowId = "window-1" };
        var combined = new M5.Command { Id = "skill-b", PlayerId = "P1", Kind = M5.CommandKind.UseGeneralSkill, SkillCommand = skill, WindowId = "window-1" };
        Assert.Equal(WireJson.Serialize(city), WireJson.Serialize(WireJson.Deserialize<M4.Command>(WireJson.Serialize(city))));
        Assert.Equal(WireJson.Serialize(combined), WireJson.Serialize(WireJson.Deserialize<M5.Command>(WireJson.Serialize(combined))));
        var restored = WireJson.Deserialize<M5.Command>(WireJson.Serialize(combined));
        restored.SkillCommand.Counts[0]++;
        restored.SkillCommand.TargetPlayerIds[0] = "P4";
        Assert.Equal(1, skill.Counts[0]);
        Assert.Equal("P2", skill.TargetPlayerIds[0]);
        Assert.ThrowsAny<Exception>(() => WireJson.Serialize(skill));
        Assert.ThrowsAny<Exception>(() => WireJson.Deserialize<M5.Command>("{\"SkillCommand\":{\"AuthoritativeHand\":[]}}"));
    }

    [Fact]
    public void NormalSkillChoiceOnlyPublishesActionsToDecisionOwnerAndResumesIdentically()
    {
        var game = M5.CombinedGameSession.Create("heading-for-new-shores", 4, 419, threeKingdoms: true,threeKingdomsPackageVersion:GeneralCatalog.B3PackageVersion);
        var policy = new CombinedAi();
        for (int step = 0; step < 512; step++)
        {
            var publicView = game.GetPlayerView("P1");
            string actor = publicView.ThreeKingdoms.PendingDecision?.PlayerId ?? publicView.ThreeKingdoms.SkillDecision?.PlayerId ??
                (publicView.TradeOffer != null ? publicView.TradeOffer.NextResponder() : publicView.Discards.FirstOrDefault()?.PlayerId ?? publicView.PendingDecision?.PlayerId ?? publicView.ActivePlayerId);
            if (publicView.ThreeKingdoms.SkillDecision != null)
            {
                foreach (var player in publicView.Players)
                {
                    var view = game.GetPlayerView(player.Id);
                    string wire = WireJson.Serialize(view);
                    Assert.Equal(wire, WireJson.Serialize(WireJson.Deserialize<M5.PlayerView>(wire)));
                    foreach (string forbidden in new[] { "\"Frames\"", "\"Harvest\"", "\"TimedEffects\"", "\"ProcessedEvents\"", "\"GeneralDrawOrder\"", "\"RandomAlgorithmId\"" }) Assert.DoesNotContain(forbidden, wire);
                    if (player.Id == actor) Assert.NotEmpty(view.ThreeKingdoms.SkillActions);
                    else
                    {
                        Assert.Empty(view.ThreeKingdoms.SkillActions);
                        Assert.Empty(game.GetAiPlayerView(player.Id).ThreeKingdoms.SkillActions);
                        Assert.DoesNotContain(view.LegalActions, c => c.Kind == M5.CommandKind.UseGeneralSkill);
                    }
                }
                var restored = M5.CombinedGameSession.Load(game.Save());
                var command = policy.Decide(game.GetAiPlayerView(actor));
                Assert.Equal(M5.CommandKind.UseGeneralSkill, command.Kind);
                Assert.Equal(WireJson.Serialize(command), WireJson.Serialize(policy.Decide(restored.GetAiPlayerView(actor))));
                command.Id = "resume-skill";
                Assert.True(game.Execute(command).Success);
                Assert.True(restored.Execute(command).Success);
                Assert.Equal(game.Save(), restored.Save());
                return;
            }
            var next = policy.Decide(game.GetAiPlayerView(actor)); next.Id = "seek-skill-" + step;
            var result = game.Execute(next);
            Assert.True(result.Success, $"步骤 {step}，{actor}，{next.Kind}：{result.Message}");
        }
        Assert.Fail("正常 B2 对局未在预算内到达技能选择窗口。");
    }
}
