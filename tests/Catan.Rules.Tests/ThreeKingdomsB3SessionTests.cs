using Catan.Core.ThreeKingdoms;
using Xunit;
using Xunit.Abstractions;
using M5 = Catan.Core.M5;

namespace Catan.Rules.Tests;

/// <summary>B3 正常 16 将包验收；每个动作来自产品 AI，不改变胜利条件或发将数量。</summary>
public sealed class ThreeKingdomsB3SessionTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> OpeningConfigurations()
    {
        foreach (int seats in new[] { 3, 4 })
        {
            yield return new object[] { "cities", "base", seats };
            foreach (string map in M5.CombinedScenarios.Ids) yield return new object[] { "combined", map, seats };
        }
    }

    [Theory][MemberData(nameof(OpeningConfigurations))]
    public void CompleteSixteenGeneralPackageUsesOriginalFourFiveCandidateDealAndRecoversAtEverySelection(string rules, string map, int seats)
    {
        var game = ThreeKingdomsTestGame.Create(rules, map, seats, skills: true, packageVersion: GeneralCatalog.B3PackageVersion);
        Assert.True(game.Tk().SkillsEnabled);
        Assert.Equal(GeneralCatalog.B3PackageVersion, game.Tk().PackageVersion);
        Assert.Equal(GeneralCatalog.B3ContentHash, game.Tk().PackageContentHash);
        string[] expected = GeneralCatalog.EnabledDefinitions.Select(g => g.Id).Order().ToArray();
        Assert.Equal(16, expected.Length);
        Assert.Equal(expected, game.Authority().Generals.Select(g => g.GeneralId).Order());
        int seat = 0;
        while (!game.Tk().OpeningComplete)
        {
            var decision = game.Authority().PendingDecision;
            string[] ownCandidates = game.Tk(decision.PlayerId).PendingDecision.CandidateIds;
            if (decision.Kind == ThreeKingdomsDecisionKind.SelectOpening)
            {
                Assert.Equal(seat == seats - 1 ? 5 : 4, ownCandidates.Length);
                Assert.Equal(2, decision.RequiredCount);
                foreach (string observer in game.Authority().SeatPlayerIds.Where(p => p != decision.PlayerId))
                    Assert.Empty(game.Tk(observer).PendingDecision.CandidateIds);
                seat++;
            }
            Assert.All(ownCandidates, id => Assert.Contains(id, expected));
            game.AcceptAndResume(game.Decide());
            AssertPackageInvariants(game);
        }
        Assert.Equal(seats, seat);
        Assert.Equal(16 - seats * 2, game.Tk().PoolCount);
        Assert.All(game.Tk().Players, p => Assert.Equal(3, p.RemainingLife));
    }

    [Theory][InlineData("cities", 3)][InlineData("cities", 4)][InlineData("combined", 3)][InlineData("combined", 4)]
    public void ProductAiCompletesNormalB3GameAndReplaysEveryDistinctSkillDecision(string rules, int seats)
    {
        var game = ThreeKingdomsTestGame.Create(rules, seats: seats, skills: true, packageVersion: GeneralCatalog.B3PackageVersion);
        var checkpoints = new HashSet<string>();
        var activeGenerals = new HashSet<string>();
        var decisions = new HashSet<string>();
        int steps = 0, highestRound = 0;
        while (game.View().Phase.ToString() != "Finished" && steps < 18000)
        {
            var state = game.Authority();
            highestRound = Math.Max(highestRound, state.Clock.Round);
            foreach (var general in state.Generals.Where(g => g.Region == GeneralRegion.Active)) activeGenerals.Add(general.GeneralId);
            var skillDecision = state.Skills?.PendingDecision ?? state.Dice?.PendingDecision;
            string checkpoint = skillDecision != null ? "技能:" + skillDecision.Title + "/" + skillDecision.Kind :
                state.PendingDecision != null ? "武将:" + state.PendingDecision.Kind : "原模式:" + game.View().PendingDecision?.Kind + "/" + state.Clock.Phase;
            if (skillDecision != null) decisions.Add(checkpoint);
            dynamic command = game.Decide(); command.Id = "B3整局-" + steps++;
            string? trace = Environment.GetEnvironmentVariable("CATAN_B3_TRACE");
            if (!string.IsNullOrEmpty(trace))
            {
                Directory.CreateDirectory(trace);
                File.WriteAllText(Path.Combine(trace, rules + "-" + seats + "-progress.json"),
                    System.Text.Json.JsonSerializer.Serialize(new { steps, highestRound, checkpoint, clock = state.Clock, command = (object)command }));
                if (steps % 100 == 0) File.WriteAllText(Path.Combine(trace, rules + "-" + seats + "-checkpoint.json"), game.Save());
            }
            if (checkpoints.Add(checkpoint)) game.AcceptAndResume(command); else game.Accept(command);
            AssertPackageInvariants(game);
        }
        Assert.Equal("Finished", (string)game.View().Phase.ToString());
        Assert.True(highestRound > 3, "完整局须经历至少一次三轮到期轮换。");
        Assert.Contains("武将:SelectReplacement", checkpoints);
        Assert.Contains("武将:DeployReplacement", checkpoints);
        Assert.NotEmpty(decisions);
        Assert.True(activeGenerals.Count >= seats, "不能指定少量固定将代替正常抽将。");
        Assert.Equal(game.Save(), ThreeKingdomsTestGame.Load(rules, game.Save()).Save());
        output.WriteLine($"B3 {rules}/{seats} 人：正常胜利，{steps} 命令，{highestRound} 轮；{activeGenerals.Count} 将实际登场；技能待决恢复 {string.Join("，", decisions.Order())}");
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void RealSkillDecisionRejectsForgedActorStaleWindowAndSaveTamperingWhileExactRetryIsIdempotent(string rules)
    {
        var game = ThreeKingdomsTestGame.Create(rules, skills: true, packageVersion: GeneralCatalog.B3PackageVersion);
        int steps = 0;
        while (game.Authority().Skills.PendingDecision == null && steps++ < 600) game.Accept(game.Decide());
        var decision = game.Authority().Skills.PendingDecision;
        Assert.NotNull(decision);
        dynamic actorForgery = game.Decide();
        actorForgery.PlayerId = game.Authority().SeatPlayerIds.First(p => p != decision.PlayerId);
        game.Reject(actorForgery);
        dynamic staleWindow = game.Decide(); staleWindow.SkillCommand.WindowId = "过期技能窗口"; game.Reject(staleWindow);
        dynamic staleDecision = game.Decide(); staleDecision.SkillCommand.DecisionId = "过期技能待决"; game.Reject(staleDecision);
        var tampered = System.Text.Json.Nodes.JsonNode.Parse(game.Save())!;
        tampered["ThreeKingdoms"]!["Skills"]!["PendingDecision"]!["DecisionId"] = "篡改私密选择";
        Assert.Throws<InvalidDataException>(() => ThreeKingdomsTestGame.Load(rules, tampered.ToJsonString()));
        dynamic valid = game.Decide(); game.AcceptAndResume(valid);
        string accepted = game.Save(); dynamic duplicate = game.Execute(valid);
        Assert.True((bool)duplicate.Success); Assert.True((bool)duplicate.IsDuplicate); Assert.Equal(accepted, game.Save());
        valid.SkillCommand.Option = "同ID变更内容"; game.Reject(valid);
        AssertPackageInvariants(game);
    }

    private static void AssertPackageInvariants(ThreeKingdomsTestGame game)
    {
        var state = game.Authority();
        Assert.True(state.SkillsEnabled);
        Assert.Equal(16, state.Generals.Length);
        Assert.Equal(16, state.Generals.Select(g => g.InstanceId).Distinct().Count());
        Assert.Equal(16, state.Generals.Select(g => g.GeneralId).Distinct().Count());
        Assert.All(state.Generals, g => Assert.True(GeneralCatalog.Get(g.GeneralId).SkillsEnabled));
        Assert.All(state.Generals.Where(g => g.Region == GeneralRegion.Hidden), g => { Assert.Equal(0, g.RemainingLife); Assert.Null(g.TenureId); });
        game.AssertInvariants();
    }
}
