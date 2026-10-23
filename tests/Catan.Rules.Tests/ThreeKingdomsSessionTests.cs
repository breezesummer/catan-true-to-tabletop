using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Catan.AI;
using Catan.Core.ThreeKingdoms;
using Xunit;
using Xunit.Abstractions;
using M4 = Catan.Core.M4;
using M5 = Catan.Core.M5;

namespace Catan.Rules.Tests;

/// <summary>B0 §3、4、9 的独立验收；技能包只验开发骨架，不宣称完成任何武将技能。</summary>
public sealed class ThreeKingdomsSessionTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> Configurations()
    {
        foreach (int seats in new[] { 3, 4 })
        {
            yield return new object[] { "cities", "base", seats };
            foreach (string map in M5.CombinedScenarios.Ids) yield return new object[] { "combined", map, seats };
        }
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void SerialPrivateSelectionThenPublicDeploymentAndSetupResumeOnEveryDecision(string rules, string map, int seats)
    {
        var game = ThreeKingdomsTestGame.Create(rules, map, seats);
        var seatOrder = game.Authority().SeatPlayerIds;
        Assert.Equal((string)game.RawAuthority().ActivePlayerId, seatOrder[0]);
        for (int seat = 0; seat < seats; seat++)
        {
            var own = game.Tk(seatOrder[seat]);
            var decision = Assert.IsType<ThreeKingdomsDecision>(own.PendingDecision);
            Assert.Equal(ThreeKingdomsDecisionKind.SelectOpening, decision.Kind);
            Assert.Equal(seatOrder[seat], decision.PlayerId);
            Assert.Equal(seat == seats - 1 ? 5 : 4, decision.CandidateIds.Length);
            Assert.Equal(2, decision.RequiredCount);
            Assert.All(own.Players, p => Assert.Null(p.ActiveGeneralId));
            Assert.Equal(0, (int)game.View().Turn);
            AssertUniqueEntities(game);
            AssertPrivateOpening(game, seatOrder[seat], decision.CandidateIds);
            var selected = decision.CandidateIds.Take(2).ToArray();
            game.AcceptAndResume(game.Command("ChooseGenerals", seatOrder[seat], selected));
            own = game.Tk(seatOrder[seat]);
            Assert.Equal(ThreeKingdomsDecisionKind.DeployOpening, own.PendingDecision.Kind);
            Assert.Equal(selected.Order(), own.PendingDecision.CandidateIds.Order());
            game.AcceptAndResume(game.Command("DeployGeneral", seatOrder[seat], new[] { selected[0] }));
            if (seat < seats - 1) Assert.All(game.Tk(seatOrder[seat + 1]).Players, p => Assert.Null(p.ActiveGeneralId));
        }
        Assert.True(game.Tk().OpeningComplete);
        Assert.Equal(seatOrder[0], (string)game.View().ActivePlayerId);
        Assert.False(game.Tk().SkillsEnabled);
        Assert.All(game.Tk().Players, p => { Assert.NotNull(p.ActiveGeneralId); Assert.Equal(3, p.RemainingLife); Assert.Equal(1, p.HiddenCount); });
        Assert.Equal(54 - seats * 2, game.Tk().PoolCount);
        for (int step = 0; (int)game.View().Turn == 0 && step < 60; step++)
        {
            Assert.All(game.Tk().Players, p => Assert.Equal(3, p.RemainingLife));
            game.AcceptAndResume(game.Decide());
        }
        Assert.Equal(1, (int)game.View().Turn);
        Assert.Equal(1, game.Tk().Clock.Round);
        Assert.All(game.Tk().Players, p => Assert.Equal(3, p.RemainingLife));
        AssertUniqueEntities(game);
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void IllegalSelectionsPreviewAndDuplicateNeverMutateAuthority(string rules)
    {
        var game = ThreeKingdomsTestGame.Create(rules);
        string owner = game.Authority().PendingDecision.PlayerId;
        var options = game.Tk(owner).PendingDecision.CandidateIds;
        var valid = game.Command("ChooseGenerals", owner, options.Take(2).ToArray());
        string before = game.Save();
        Assert.True((bool)game.Preview(valid).Success);
        Assert.Equal(before, game.Save());
        var prematureSetup = game.Command("SetupSettlement", owner);
        prematureSetup.TargetId = game.View(owner).Board.Vertices[0].Id;
        game.Reject(prematureSetup);
        game.Reject(game.Command("ChooseGenerals", owner, options.Take(1).ToArray()));
        game.Reject(game.Command("ChooseGenerals", owner, new[] { options[0], options[0] }));
        game.Reject(game.Command("ChooseGenerals", owner, new[] { "tk-g99", options[0] }));
        string opponent = game.Authority().SeatPlayerIds.First(p => p != owner);
        var forged = game.Command("ChooseGenerals", owner, options.Take(2).ToArray()); forged.PlayerId = opponent; game.Reject(forged);
        var stale = game.Command("ChooseGenerals", owner, options.Take(2).ToArray()); stale.DecisionId = "过期待决"; game.Reject(stale);
        var oldWindow = game.Command("ChooseGenerals", owner, options.Take(2).ToArray()); oldWindow.WindowId = "过期窗口"; game.Reject(oldWindow);
        game.Reject(game.Command("RetireGeneral", owner));
        game.Accept(valid);
        string accepted = game.Save();
        var duplicate = game.Execute(valid);
        Assert.True((bool)duplicate.Success); Assert.True((bool)duplicate.IsDuplicate);
        Assert.Equal(accepted, game.Save());
        valid.GeneralIds = options.Skip(1).Take(2).ToArray(); game.Reject(valid);
        var deployment = game.Command("DeployGeneral", owner, new[] { options[0] });
        game.AcceptAndResume(deployment);
        deployment.Id = ThreeKingdomsTestGame.NextId(); game.Reject(deployment);
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void CandidateAndHiddenViewsLogsAndAiDoNotLeakOtherSeats(string rules)
    {
        var game = ThreeKingdomsTestGame.Create(rules);
        string owner = game.Authority().PendingDecision.PlayerId;
        string observer = game.Authority().SeatPlayerIds.First(p => p != owner);
        string[] candidates = game.Tk(owner).PendingDecision.CandidateIds;
        AssertPrivateOpening(game, owner, candidates);
        game.Accept(game.Command("ChooseGenerals", owner, candidates.Take(2).ToArray()));
        game.Accept(game.Command("DeployGeneral", owner, new[] { candidates[0] }));
        AssertSecretsAbsent(game, observer, candidates.Take(2));
        game.FinishOpening();
        var own = game.Tk(owner).Players.Single(p => p.PlayerId == owner);
        Assert.Single(own.HiddenGeneralIds);
        AssertSecretsAbsent(game, observer, own.HiddenGeneralIds);
        Assert.Contains(own.ActiveGeneralId!, ThreeKingdomsTestGame.Json(game.View(observer)));
        var authority = game.Authority();
        var hidden = authority.Generals.Single(g => g.OwnerPlayerId == owner && g.Region == GeneralRegion.Hidden);
        var pooled = authority.Generals.First(g => g.Region == GeneralRegion.Pool);
        (hidden.GeneralId, pooled.GeneralId) = (pooled.GeneralId, hidden.GeneralId);
        // 改动脱离权威的副本：对外投影不能反向改写服务器，也不暴露池身份。
        string before = game.Save();
        game.View(observer).ThreeKingdoms.Players[0].HiddenGeneralIds = new[] { "泄漏探针" };
        Assert.Equal(before, game.Save());
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void MutatingEveryNestedReturnedCollectionCannotChangeAuthority(string rules)
    {
        var game = ThreeKingdomsTestGame.Create(rules);
        string actor = game.Authority().PendingDecision.PlayerId;
        string before = game.Save();
        var view = game.Tk(actor);
        view.PendingDecision.CandidateIds[0] = "改候选";
        view.Players[0].PlayerId = "改玩家";
        view.Clock.ExtraTurnQueue = new[] { "改额外回合" };
        var state = game.Authority();
        state.SeatPlayerIds[0] = "改座次";
        state.GeneralDrawOrder[0] = "改牌序";
        state.Generals[0].Counters = new[] { new GeneralCounter { Key = "改计数", Value = 99 } };
        state.PendingDecision.CandidateIds[0] = "改权威副本";
        state.EffectFrames = new[] { new ThreeKingdomsEffectFrame { Targets = new[] { "改效果" } } };
        Assert.Equal(before, game.Save());
        game.AssertInvariants();
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void NewSaveRejectsPackageRandomAndPrivateDecisionTampering(string rules)
    {
        var game = ThreeKingdomsTestGame.Create(rules);
        foreach (string field in new[] { "PackageContentHash", "PackageVersion", "RulesVersion", "candidate", "random" })
        {
            var json = JsonNode.Parse(game.Save())!;
            if (field == "candidate") json["ThreeKingdoms"]!["PendingDecision"]!["CandidateIds"]![0] = "tk-g99";
            else if (field == "random") json["Random"]!["State"] = 555u;
            else json["ThreeKingdoms"]![field] = "不兼容";
            Assert.Throws<InvalidDataException>(() => ThreeKingdomsTestGame.Load(rules, json.ToJsonString()));
        }
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void ActivePlayerCanReplaceWhileAnotherSeatOwesDecisionAndOldResponseCannotReplay(string rules)
    {
        var game = ThreeKingdomsTestGame.Create(rules);
        for (int steps = 0; steps < 800; steps++)
        {
            dynamic view = game.View();
            string active = view.ActivePlayerId;
            string actor = game.Tk().PendingDecision?.PlayerId ?? M6Game.Actor(view);
            if (game.Tk().Clock.Round > 0 && game.Tk().PendingDecision == null && actor != active)
            {
                string pending = ThreeKingdomsTestGame.Json((object)view.PendingDecision);
                string discards = ThreeKingdomsTestGame.Json((object)view.Discards);
                dynamic oldResponse = game.Decide();
                game.AcceptAndResume(game.Command("RetireGeneral", active));
                Assert.Equal(pending, ThreeKingdomsTestGame.Json((object)game.RawAuthority().PendingDecision));
                Assert.Equal(discards, ThreeKingdomsTestGame.Json((object)game.RawAuthority().Discards));
                while (game.Tk(active).PendingDecision != null)
                {
                    var decision = game.Tk(active).PendingDecision;
                    string kind = decision.Kind == ThreeKingdomsDecisionKind.SelectReplacement ? "ChooseGenerals" : "DeployGeneral";
                    game.AcceptAndResume(game.Command(kind, active, decision.CandidateIds.Take(1).ToArray()));
                }
                Assert.Equal(pending, ThreeKingdomsTestGame.Json((object)game.View().PendingDecision));
                Assert.Equal(discards, ThreeKingdomsTestGame.Json((object)game.View().Discards));
                game.Reject(oldResponse);
                game.AcceptAndResume(game.Decide());
                return;
            }
            game.Accept(game.Decide());
        }
        Assert.Fail("固定种子局未进入其他席位待决，未完成暂挂验收。");
    }

    public static IEnumerable<object[]> LegacyCases() => ThreeKingdomsLegacySaves.Items.Select(x => new object[] { x.Rules, x.Seats, x.GzipBase64 });

    [Theory][MemberData(nameof(LegacyCases))]
    public void FrozenPreB1SaveRestoresOldCommandsFingerprintsAndDeterministicContinuation(string rules, int seats, string encoded)
    {
        using var compressed = new MemoryStream(Convert.FromBase64String(encoded));
        using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        string oldSave = reader.ReadToEnd();
        var restored = ThreeKingdomsTestGame.Load(rules, oldSave);
        Assert.Equal(oldSave, restored.Save());
        Assert.Null(restored.View().ThreeKingdoms);
        var fresh = ThreeKingdomsTestGame.Create(rules, seats: seats, enabled: false);
        for (int step = 0; step < 24; step++) { dynamic command = fresh.Decide(); command.Id = "legacy-" + step; fresh.Accept(command); }
        Assert.Equal(oldSave, fresh.Save());
        dynamic next = fresh.Decide(); next.Id = "旧存档继续";
        fresh.Accept(next); restored.Accept(next);
        Assert.Equal(fresh.Save(), restored.Save());
    }

    [Theory][InlineData("cities", 3)][InlineData("cities", 4)][InlineData("combined", 3)][InlineData("combined", 4)]
    public void ProductAiCompletesSkeletonGameWithThreeRoundRenewalAndPendingReplay(string rules, int seats)
    {
        var game = ThreeKingdomsTestGame.Create(rules, seats: seats);
        var checkpoints = new HashSet<string>();
        var stages = new HashSet<ThreeKingdomsPhase>();
        int steps = 0, highestRound = 0;
        while (game.View().Phase.ToString() != "Finished" && steps < 10000)
        {
            var tk = game.Tk();
            stages.Add(tk.Clock.Phase); highestRound = Math.Max(highestRound, tk.Clock.Round);
            string checkpoint = tk.PendingDecision != null ? "将:" + tk.PendingDecision.Kind : "牌:" + game.View().PendingDecision?.Kind + "/" + tk.Clock.Phase;
            dynamic command = game.Decide(); command.Id = "整局-" + steps++;
            if (checkpoints.Add(checkpoint)) game.AcceptAndResume(command);
            else game.Accept(command);
            AssertUniqueEntities(game);
        }
        Assert.True(game.View().Phase.ToString() == "Finished", $"{rules}/{seats} 人超过 {steps} 步：阶段={game.View().Phase}，三国={game.Tk().Clock.Phase}，轮={highestRound}");
        Assert.True(highestRound > 3);
        Assert.Contains("将:SelectReplacement", checkpoints);
        Assert.Contains("将:DeployReplacement", checkpoints);
        foreach (var phase in new[] { ThreeKingdomsPhase.Preparation, ThreeKingdomsPhase.Judgment, ThreeKingdomsPhase.Roll, ThreeKingdomsPhase.Draw, ThreeKingdomsPhase.Play, ThreeKingdomsPhase.End }) Assert.Contains(phase, stages);
        Assert.Equal(game.Save(), ThreeKingdomsTestGame.Load(rules, game.Save()).Save());
        output.WriteLine($"{rules} / {seats} 人：{steps} 命令，第 {highestRound} 轮获胜；保存恢复点 {string.Join("，", checkpoints.Order())}");
    }

    private static void AssertPrivateOpening(ThreeKingdomsTestGame game, string owner, IEnumerable<string> ids)
    {
        foreach (string observer in game.Authority().SeatPlayerIds.Where(p => p != owner))
        {
            Assert.Empty(game.Tk(observer).PendingDecision.CandidateIds);
            AssertSecretsAbsent(game, observer, ids);
        }
    }
    private static void AssertSecretsAbsent(ThreeKingdomsTestGame game, string observer, IEnumerable<string> ids)
    {
        string publicJson = ThreeKingdomsTestGame.Json(game.View(observer));
        string aiJson = ThreeKingdomsTestGame.Json(game.AiView(observer));
        string history = ThreeKingdomsTestGame.Json(game.History(observer));
        foreach (string id in ids) { Assert.DoesNotContain(id, publicJson); Assert.DoesNotContain(id, aiJson); Assert.DoesNotContain(id, history); }
    }
    internal static void AssertUniqueEntities(ThreeKingdomsTestGame game)
    {
        var state = game.Authority();
        Assert.Equal(54, state.Generals.Length);
        Assert.Equal(54, state.Generals.Select(g => g.GeneralId).Distinct().Count());
        Assert.Equal(54, state.Generals.Select(g => g.InstanceId).Distinct().Count());
        Assert.All(state.Generals.Where(g => g.Region == GeneralRegion.Hidden), g => { Assert.Equal(0, g.RemainingLife); Assert.Null(g.TenureId); });
        game.AssertInvariants();
    }
}

internal sealed class ThreeKingdomsTestGame(string rules, object session)
{
    private static long sequence;
    internal static string NextId() => "三国测试-" + Interlocked.Increment(ref sequence);
    internal dynamic View(string player = "P1") => ((dynamic)session).GetPlayerView(player);
    internal dynamic AiView(string player = "P1") => ((dynamic)session).GetAiPlayerView(player);
    internal dynamic History(string player = "P1") => ((dynamic)session).GetHistory(player);
    internal ThreeKingdomsView Tk(string player = "P1") => View(player).ThreeKingdoms;
    internal ThreeKingdomsState Authority() => ((dynamic)session).GetAuthoritativeStateForTesting().ThreeKingdoms;
    internal dynamic RawAuthority() => ((dynamic)session).GetAuthoritativeStateForTesting();
    internal string Save() => ((dynamic)session).Save();
    internal dynamic Execute(dynamic c) => ((dynamic)session).Execute(c);
    internal dynamic Preview(dynamic c) => ((dynamic)session).Preview(c);
    internal void AssertInvariants() => ((dynamic)session).AssertInvariants();
    internal void Accept(dynamic c)
    {
        dynamic r = Execute(c);
        Assert.True((bool)r.Success, $"{rules}/{c.Kind}/{c.PlayerId}: {r.ErrorCode} {r.Message}");
        AssertInvariants();
    }
    internal void AcceptAndResume(dynamic c)
    {
        string before = Save();
        var copy = Load(rules, before);
        Assert.Equal(before, copy.Save());
        Assert.True((bool)Preview(c).Success); Assert.Equal(before, Save());
        Accept(c); copy.Accept(c); Assert.Equal(Save(), copy.Save());
    }
    internal void Reject(dynamic c)
    {
        string before = Save(); dynamic r = Execute(c);
        Assert.False((bool)r.Success); Assert.NotEmpty((string)r.ErrorCode);
        Assert.Equal(before, Save()); AssertInvariants();
    }
    internal dynamic Command(string kind, string player, string[]? ids = null)
    {
        dynamic command = rules == "cities" ? (object)new M4.Command { Kind = Enum.Parse<M4.CommandKind>(kind) } : new M5.Command { Kind = Enum.Parse<M5.CommandKind>(kind) };
        command.Id = NextId(); command.PlayerId = player;
        command.GeneralIds = ids ?? Array.Empty<string>();
        command.DecisionId = Tk(player).PendingDecision?.DecisionId;
        command.WindowId = Tk(player).WindowId;
        if (kind == "DeployGeneral" && ids?.Length > 0) command.TargetId = ids[0];
        return command;
    }
    internal dynamic Decide()
    {
        dynamic publicView = View();
        string actor = publicView.ThreeKingdoms?.PendingDecision?.PlayerId ?? publicView.ThreeKingdoms?.SkillDecision?.PlayerId ?? M6Game.Actor(publicView);
        dynamic command = rules == "cities" ? (object)new CitiesKnightsAi().Decide((M4.PlayerView)AiView(actor)) : new CombinedAi().Decide((M5.PlayerView)AiView(actor));
        command.Id = NextId(); return command;
    }
    internal void FinishOpening()
    {
        while (!Tk().OpeningComplete)
        {
            var decision = Authority().PendingDecision;
            string[] choices = Tk(decision.PlayerId).PendingDecision.CandidateIds;
            Accept(Command(decision.Kind == ThreeKingdomsDecisionKind.SelectOpening ? "ChooseGenerals" : "DeployGeneral", decision.PlayerId,
                choices.Take(decision.Kind == ThreeKingdomsDecisionKind.SelectOpening ? 2 : 1).ToArray()));
        }
    }
    internal static string Json(object? value) => value == null ? "null" : JsonSerializer.Serialize(value, value.GetType());
    internal static ThreeKingdomsTestGame Create(string rules, string map = "heading-for-new-shores", int seats = 4, bool enabled = true, bool skills = false) => new(rules,
        rules == "cities" ? (object)M4.CitiesKnightsGameSession.Create(AcceptanceFixture.Json, seats, 97, threeKingdoms: enabled, threeKingdomsSkills: skills) : M5.CombinedGameSession.Create(map, seats, 97, threeKingdoms: enabled, threeKingdomsSkills: skills));
    internal static ThreeKingdomsTestGame Load(string rules, string save) => new(rules,
        rules == "cities" ? (object)M4.CitiesKnightsGameSession.Load(AcceptanceFixture.Json, save) : M5.CombinedGameSession.Load(save));
}
