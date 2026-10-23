using System.Text.Json.Nodes;
using Catan.Core;
using Catan.Core.ThreeKingdoms;
using Xunit;
using M4 = Catan.Core.M4;
using M5 = Catan.Core.M5;

namespace Catan.Rules.Tests;

/// <summary>正常会话检查真实重放；原生夹具只证明宿主结算边界，不用反射夹具冒充可重放存档。</summary>
public sealed class ThreeKingdomsB3DiceIntegrationTests
{
    [Theory][InlineData("cities")][InlineData("combined")]
    public void ReplacementSuspendsFixedDiceProjectionUntilProductAiCompletesBothChoicesAndReplay(string rules)
    {
        var game = NormalFixedGame(rules);
        for (int step = 0; game.Authority().Clock.Phase != ThreeKingdomsPhase.Preparation && step < 100; step++) game.Accept(game.Decide());
        var before = game.Authority();
        Assert.Equal(ThreeKingdomsPhase.Preparation, before.Clock.Phase);
        Assert.Equal("dice-fixed", before.Dice.PendingDecision.Kind);
        string owner = before.Dice.PendingDecision.PlayerId;
        string active = before.Clock.ActivePlayerId;
        Assert.NotEqual(active, owner);
        dynamic stale = game.Decide();
        dynamic retire = game.Command("RetireGeneral", active); dynamic preview = game.Preview(retire);
        Assert.True((bool)preview.Success, (string)preview.Message);
        game.AcceptAndResume(retire);
        foreach (var kind in new[] { ThreeKingdomsDecisionKind.SelectReplacement, ThreeKingdomsDecisionKind.DeployReplacement })
        {
            Assert.Equal(kind, game.Authority().PendingDecision.Kind);
            Assert.Equal(owner, game.Authority().Dice.PendingDecision.PlayerId);
            string snapshot = game.Save();
            foreach (string viewer in game.Authority().SeatPlayerIds)
            {
                var view = game.Tk(viewer); Assert.NotNull(view.PendingDecision); Assert.Null(view.SkillDecision); Assert.Empty(view.SkillActions);
                dynamic ai = game.AiView(viewer); Assert.Null(ai.ThreeKingdoms.SkillDecision);
            }
            Assert.Equal(snapshot, game.Save());
            game.Reject(stale);
            dynamic choice = game.Decide();
            Assert.Equal(kind == ThreeKingdomsDecisionKind.SelectReplacement ? "ChooseGenerals" : "DeployGeneral", (string)choice.Kind.ToString());
            game.AcceptAndResume(choice);
        }
        Assert.Null(game.Authority().PendingDecision);
        Assert.Equal("dice-fixed", game.Tk(owner).SkillDecision.Kind); Assert.NotEmpty(game.Tk(owner).SkillActions);
        Assert.Equal(owner, game.Authority().Dice.PendingDecision.PlayerId);
        game.Reject(stale);
        game.AcceptAndResume(game.Decide());
        Assert.Null(game.Authority().Dice.PendingDecision); Assert.False(game.Authority().Dice.StartWindowOpen);
    }

    private static ThreeKingdomsTestGame NormalFixedGame(string rules)
    {
        foreach (uint seed in new uint[] { 1296735866, 97, 42, 71, 18, 3 })
        {
            object session = rules == "cities" ? M4.CitiesKnightsGameSession.Create(AcceptanceFixture.Json, 3, seed, threeKingdoms: true, threeKingdomsSkills: true, threeKingdomsPackageVersion: GeneralCatalog.B3PackageVersion)
                : M5.CombinedGameSession.Create("heading-for-new-shores", 3, seed, threeKingdoms: true, threeKingdomsSkills: true, threeKingdomsPackageVersion: GeneralCatalog.B3PackageVersion);
            var game = new ThreeKingdomsTestGame(rules, session);
            string first = game.Authority().SeatPlayerIds[0];
            while (!game.Tk().OpeningComplete)
            {
                var d = game.Authority().PendingDecision;
                var candidates = game.Tk(d.PlayerId).PendingDecision.CandidateIds.OrderBy(id => id == "tk-g21" ? d.PlayerId == first ? 1 : -1 : 0).ToArray();
                game.Accept(game.Command(d.Kind == ThreeKingdomsDecisionKind.SelectOpening ? "ChooseGenerals" : "DeployGeneral", d.PlayerId, candidates.Take(d.RequiredCount).ToArray()));
            }
            if (game.Authority().Generals.Any(g => g.Region == GeneralRegion.Active && g.GeneralId == "tk-g21" && g.OwnerPlayerId != first)) return game;
        }
        throw new InvalidOperationException("正常候选中未找到其他玩家的张昭张弘，不能把指定将夹具伪作重放检查。");
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void ProductViewExposesPaidConstructionAndAiBuildsWithoutPreviewStateOrIdLeak(string rules)
    {
        var (game, session) = Fixture(rules, new[] { "tk-g22", "tk-g13", "tk-g09", "tk-g06" }); dynamic state = game.RawAuthority();
        string vertex;
        if (state is M4.GameState cities)
        {
            var tile = cities.Tiles.First(t => t.Number.HasValue); vertex = tile.Vertices[0];
            var board = ((M4.CitiesKnightsGameSession)session).GetPlayerView("P1").Board;
            string edge = board.Edges.First(e => e.Vertices.Contains(vertex) && e.Vertices.All(tile.Vertices.Contains)).Id;
            cities.Phase = M4.GamePhase.Action; cities.Settlements = new[] { M4CoreHarness.Piece(vertex) }; cities.Roads = new[] { M4CoreHarness.Piece(edge) };
            cities.Players[0].Resources = new ResourceBag { Wood = 2, Brick = 2, Wool = 2, Wheat = 4, Ore = 5 };
        }
        else
        {
            var combined = (M5.GameState)state; var tile = combined.Tiles.First(t => t.Number.HasValue); vertex = tile.Vertices[0];
            var board = ((M5.CombinedGameSession)session).GetPlayerView("P1").Board;
            string edge = board.Edges.First(e => e.Vertices.Contains(vertex) && e.Vertices.All(tile.Vertices.Contains)).Id;
            combined.Phase = M5.GamePhase.Action; combined.Settlements = new[] { M5Harness.Piece(vertex) }; combined.Roads = new[] { M5Harness.Piece(edge) };
            combined.Players[0].Resources = new ResourceBag { Wood = 2, Brick = 2, Wool = 2, Wheat = 4, Ore = 5 };
        }
        ((ThreeKingdomsState)state.ThreeKingdoms).Clock.Phase = ThreeKingdomsPhase.Play; Set(session, state);
        string before = game.Save(); long randomDraws = game.RawAuthority().Random.Draws;
        for (int repetition = 0; repetition < 3; repetition++)
        {
            dynamic view = game.View("P1"); Assert.Contains(vertex, (string[])view.LegalCityVertexIds); Assert.NotEmpty((string[])view.LegalEdgeIds);
            if (view is M4.PlayerView c)
            {
                Assert.Contains(c.LegalActions, a => a.Kind == M4.CommandKind.RecruitKnight);
                Assert.All(c.LegalActions, a => Assert.Null(a.Id));
            }
            else
            {
                var c5 = (M5.PlayerView)view; Assert.Contains(c5.LegalActions, a => a.Kind == M5.CommandKind.RecruitKnight);
                Assert.All(c5.LegalActions, a => Assert.Null(a.Id));
            }
            game.AiView("P1"); Assert.Equal(before, game.Save()); Assert.Equal(randomDraws, (long)game.RawAuthority().Random.Draws);
        }
        dynamic build = game.Decide(); Assert.Equal("BuildCity", (string)build.Kind.ToString());
        PreviewRepeatedly(game, build); Assert.Equal(before, game.Save()); Assert.Equal(randomDraws, (long)game.RawAuthority().Random.Draws);
        game.Accept(build); Assert.Equal(1, (int)game.RawAuthority().Cities.Length);
        Assert.Equal(2, (int)game.RawAuthority().Players[0].Resources.Wheat); Assert.Equal(2, (int)game.RawAuthority().Players[0].Resources.Ore);
        Assert.Equal(randomDraws, (long)game.RawAuthority().Random.Draws);
    }

    private static ThreeKingdomsState Tk(string[] generals)
    {
        var tk = ThreeKingdomsEngine.Create(new[] { "P1", "P2", "P3", "P4" }, 42, _ => 0, true, GeneralCatalog.B3PackageVersion);
        while (!tk.OpeningComplete)
        {
            var d = tk.PendingDecision;
            if (d.Kind == ThreeKingdomsDecisionKind.SelectOpening) ThreeKingdomsEngine.ChooseGenerals(tk, d.PlayerId, d.CandidateIds.Take(2).ToArray(), d.DecisionId, tk.WindowId, _ => 0);
            else ThreeKingdomsEngine.DeployGeneral(tk, d.PlayerId, d.CandidateIds[0], d.DecisionId, tk.WindowId, _ => 0);
        }
        ThreeKingdomsEngine.BeginNormalTurns(tk); ThreeKingdomsEngine.AdvancePhase(tk, "P1", tk.WindowId); ThreeKingdomsEngine.AdvancePhase(tk, "P1", tk.WindowId);
        for (int i = 0; i < generals.Length; i++)
        {
            var current = tk.Generals.Single(g => g.Region == GeneralRegion.Active && g.OwnerPlayerId == "P" + (i + 1));
            var wanted = tk.Generals.Single(g => g.GeneralId == generals[i]);
            (current.GeneralId, wanted.GeneralId) = (wanted.GeneralId, current.GeneralId);
            (current.InstanceId, wanted.InstanceId) = (wanted.InstanceId, current.InstanceId);
        }
        return tk;
    }

    private static (ThreeKingdomsTestGame Game, object Session) Fixture(string rules, string[] generals)
    {
        object session = rules == "cities" ? M4CoreHarness.Fixture(s => {
            s.RulesVersion = M4.CitiesKnightsGameSession.ThreeKingdomsB3RulesVersion;
            s.ThreeKingdoms = Tk(generals); s.Phase = M4.GamePhase.ProductionAwaitRoll; s.LastDice1 = 0; s.LastDice2 = 0;
        }) : M5Harness.Fixture(s => {
            s.RulesVersion = M5.CombinedGameSession.ThreeKingdomsB3RulesVersion;
            s.ThreeKingdoms = Tk(generals); s.Phase = M5.GamePhase.ProductionAwaitRoll; s.LastDice1 = 0; s.LastDice2 = 0;
        });
        return (new ThreeKingdomsTestGame(rules, session), session);
    }

    private static void Set(object session, dynamic state, bool beginTurn = false)
    {
        if (session is M4.CitiesKnightsGameSession cities)
        {
            M4CoreHarness.Balance((M4.GameState)state);
            if (beginTurn) M4CoreHarness.Invoke(cities, "B2StartOpportunity", (M4.GameState)state);
            M4CoreHarness.SetState(cities, (M4.GameState)state);
        }
        else
        {
            var combined = (M5.CombinedGameSession)session; M5Harness.Balance((M5.GameState)state);
            if (beginTurn) M5Harness.Invoke(combined, "B2StartOpportunity", (M5.GameState)state);
            M5Harness.SetState(combined, (M5.GameState)state);
        }
    }

    private static dynamic Skill(ThreeKingdomsTestGame game, string player, string option, int? card = null, int? count = null)
    {
        var action = game.Tk(player).SkillActions.First(a => a.Command.Option == option && (card == null || a.Command.CardType == card) && (count == null || a.Command.Counts[0] == count));
        dynamic command = game.Command("UseGeneralSkill", player); command.SkillCommand = action.Command; return command;
    }
    private static void PreviewRepeatedly(ThreeKingdomsTestGame game, dynamic command)
    {
        string before = game.Save(); for (int i = 0; i < 8; i++) Assert.True((bool)game.Preview(command).Success);
        Assert.Equal(before, game.Save());
    }
    private static void DuplicateDoesNothing(ThreeKingdomsTestGame game, dynamic command)
    {
        string before = game.Save(); dynamic duplicate = game.Execute(command);
        Assert.True((bool)duplicate.Success); Assert.True((bool)duplicate.IsDuplicate); Assert.Equal(before, game.Save());
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void FixedBlackSevenHasNoDiceRandomAndOnlyOneEventDiscardAndHistory(string rules)
    {
        var (game, session) = Fixture(rules, new[] { "tk-g19", "tk-g21", "tk-g13", "tk-g22" }); dynamic state = game.RawAuthority();
        state.BarbarianPosition = 2; state.Players[1].Resources.Wood = 4; state.Players[2].Resources.Wood = 8; Set(session, state, true);
        long draws = game.RawAuthority().Random.Draws; dynamic fixedCommand = Skill(game, "P2", "fixed", 1, 3);
        PreviewRepeatedly(game, fixedCommand); game.Accept(fixedCommand); Assert.Equal(draws, (long)game.RawAuthority().Random.Draws);
        dynamic roll = game.Command("RollDice", "P1"); PreviewRepeatedly(game, roll); game.Accept(roll); DuplicateDoesNothing(game, roll);
        state = game.RawAuthority(); Assert.Equal(draws, (long)state.Random.Draws); Assert.Equal(3, (int)state.BarbarianPosition);
        Assert.Equal(7, (int)(state.LastDice1 + state.LastDice2)); Assert.Equal("Discard", (string)state.Phase.ToString());
        Assert.Equal(1, (int)state.Discards.Length); Assert.Equal("P3", (string)state.Discards[0].PlayerId); Assert.Equal(4, (int)state.Discards[0].Amount);
        Assert.DoesNotContain(game.Tk("P1").SkillActions, a => a.Command.SkillId == "tk-g19-s1");
        dynamic discard = game.Command("DiscardResources", "P3"); discard.Resources = new ResourceBag { Wood = 4 }; game.Accept(discard); DuplicateDoesNothing(game, discard);
        state = game.RawAuthority(); Assert.Equal(8, (int)state.Players[1].Resources.Wood); Assert.Equal(4, (int)state.Players[2].Resources.Wood);
        Assert.Equal(0, (int)state.Discards.Length); Assert.Equal(3, (int)state.BarbarianPosition); Assert.Equal(draws, (long)state.Random.Draws);
        Assert.Single((GameHistoryEntry[])game.History(), h => h.Kind == "RollDice");
        Assert.DoesNotContain(game.Authority().Skills.ProcessedEvents, id => id.StartsWith("dice:"));
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void FixedColoredSevenUsesChosenRedDieToDrawProgressExactlyOnce(string rules)
    {
        var (game, session) = Fixture(rules, new[] { "tk-g19", "tk-g21", "tk-g13", "tk-g22" }); dynamic state = game.RawAuthority();
        state.Players[3].Improvements[2] = 2; Set(session, state, true);
        long draws = game.RawAuthority().Random.Draws; game.Accept(Skill(game, "P2", "fixed", 6, 2));
        dynamic roll = game.Command("RollDice", "P1"); game.Accept(roll); DuplicateDoesNothing(game, roll);
        state = game.RawAuthority(); Assert.Equal("Science", (string)state.LastEventDie.ToString()); Assert.Equal(2, (int)state.LastDice1); Assert.Equal(5, (int)state.LastDice2);
        Assert.Equal(1, (int)(state.Players[3].ProgressCards.Length + state.Players[3].ProgressVictoryCards.Length));
        Assert.Equal(draws, (long)state.Random.Draws); Assert.Single((GameHistoryEntry[])game.History(), h => h.Kind == "RollDice");
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void ObserveWithAlchemyDefersNativeProductionAndEventUntilFinalSelection(string rules)
    {
        var (game, session) = Fixture(rules, new[] { "tk-g19", "tk-g13", "tk-g06", "tk-g22" }); dynamic state = game.RawAuthority();
        state.AlchemyDice1 = 3; state.AlchemyDice2 = 3; state.BarbarianPosition = 2;
        string vertex;
        if (state is M4.GameState m4)
        {
            vertex = m4.Tiles.First(t => t.Number == 6 && t.Id != m4.RobberTileId && t.Resource != "gold").Vertices[0];
            m4.Settlements = new[] { M4CoreHarness.Piece(vertex, "P4") };
        }
        else
        {
            var m5 = (M5.GameState)state; vertex = m5.Tiles.First(t => t.Number == 6 && t.Id != m5.RobberTileId && t.Resource != "gold").Vertices[0];
            m5.Settlements = new[] { M5Harness.Piece(vertex, "P4") };
        }
        Set(session, state); long draws = game.RawAuthority().Random.Draws;
        dynamic roll = game.Command("RollDice", "P1"); PreviewRepeatedly(game, roll); game.Accept(roll);
        Assert.Equal(draws, (long)game.RawAuthority().Random.Draws); Assert.Equal(0, (int)game.RawAuthority().LastDice1);
        dynamic observe = Skill(game, "P1", "observe"); PreviewRepeatedly(game, observe); game.Accept(observe);
        state = game.RawAuthority(); Assert.Equal(draws + 2, (long)state.Random.Draws); Assert.Equal(2, (int)state.BarbarianPosition);
        Assert.Equal(0, (int)state.Players[3].Resources.Total); Assert.Equal(0, (int)state.LastDice1);
        Assert.DoesNotContain(((GameHistoryEntry[])game.History()), h => h.Kind == "RollDice");
        var expected = new ResourceBag();
        if (state is M4.GameState cities)
            foreach (var tile in cities.Tiles.Where(t => t.Number == 6 && t.Id != cities.RobberTileId && t.Vertices.Contains(vertex))) AddExpected(expected, tile.Resource);
        else foreach (var tile in ((M5.GameState)state).Tiles.Where(t => t.Number == 6 && t.Id != ((M5.GameState)state).RobberTileId && t.Vertices.Contains(vertex))) AddExpected(expected, tile.Resource);
        int face = game.Authority().Dice.Roll.Candidates[1].EventFace;
        dynamic select = Skill(game, "P1", "select", 1); PreviewRepeatedly(game, select); game.Accept(select); DuplicateDoesNothing(game, select);
        state = game.RawAuthority(); Assert.Equal(3, (int)state.LastDice1); Assert.Equal(3, (int)state.LastDice2);
        Assert.Equal(2 + (face <= 3 ? 1 : 0), (int)state.BarbarianPosition); Assert.Equal(draws + 2, (long)state.Random.Draws);
        Assert.Equal(ThreeKingdomsTestGame.Json(expected), ThreeKingdomsTestGame.Json((object)state.Players[3].Resources));
        Assert.Single((GameHistoryEntry[])game.History(), h => h.Kind == "RollDice");
        Assert.Single((GameHistoryEntry[])game.History(), h => h.Kind == "DiceCandidate");
        Assert.DoesNotContain(game.Authority().Skills.ProcessedEvents, id => id.StartsWith("dice:"));
    }

    private static void AddExpected(ResourceBag expected, string resource)
    {
        var type = resource == "wood" ? Resource.Wood : resource == "brick" ? Resource.Brick : resource == "wool" ? Resource.Wool : resource == "wheat" ? Resource.Wheat : Resource.Ore;
        expected[type]++;
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void NativeGhostPaymentRefreshesWindowAndRejectsStaleChoiceWithoutAnotherRandomDraw(string rules)
    {
        var (game, session) = Fixture(rules, new[] { "tk-g06", "tk-g13", "tk-g09", "tk-g22" }); dynamic state = game.RawAuthority();
        if (state is M4.GameState m4)
        {
            var tile = m4.Tiles.First(t => t.Number <= 6 && t.Id != m4.RobberTileId && t.Resource == "wood");
            m4.Settlements = new[] { M4CoreHarness.Piece(tile.Vertices[0], "P2") }; m4.Players[1].Resources.Wood = 2;
        }
        else
        {
            var m5 = (M5.GameState)state; var tile = m5.Tiles.First(t => t.Number <= 6 && t.Id != m5.RobberTileId && t.Resource == "wood");
            m5.Settlements = new[] { M5Harness.Piece(tile.Vertices[0], "P2") }; m5.Players[1].Resources.Wood = 2;
        }
        Set(session, state); game.Accept(game.Command("RollDice", "P1")); long draws = game.RawAuthority().Random.Draws;
        dynamic old = Skill(game, "P2", "modify", 0, 0); dynamic modify = Skill(game, "P2", "modify", 0, 0);
        PreviewRepeatedly(game, modify); game.Accept(modify); Assert.Equal(draws, (long)game.RawAuthority().Random.Draws);
        Assert.Equal(1, (int)game.RawAuthority().Players[1].Resources.Wood); Assert.True(game.Authority().Dice.Roll.Modifications[0].Applied);
        game.Reject(old); dynamic pass = Skill(game, "P2", "skip"); game.Accept(pass); DuplicateDoesNothing(game, pass);
        Assert.Single((GameHistoryEntry[])game.History(), h => h.Kind == "RollDice");
        Assert.Equal(draws, (long)game.RawAuthority().Random.Draws);
    }

    [Theory][InlineData("cities", false)][InlineData("combined", false)][InlineData("cities", true)][InlineData("combined", true)]
    public void FixedResultExpiresBeforeSkippedOpportunityOrRoundBoundaryReplacement(string rules, bool roundEnd)
    {
        var (game, session) = Fixture(rules, new[] { "tk-g22", "tk-g21", "tk-g13", "tk-g19" }); dynamic state = game.RawAuthority();
        string active = roundEnd ? "P4" : "P1"; var tk = (ThreeKingdomsState)state.ThreeKingdoms;
        if (roundEnd)
        {
            state.ActivePlayerId = "P4"; tk.Clock.ActivePlayerId = "P4"; tk.Clock.NormalSeatCursor = 3;
            tk.Clock.TurnInstanceId = 4; tk.Clock.OpportunityId = 4; tk.Clock.NormalOpportunitiesConsumed = 3;
            foreach (var general in tk.Generals.Where(g => g.Region == GeneralRegion.Active)) general.RemainingLife = 1;
        }
        else tk.Players.Single(p => p.PlayerId == "P2").FaceDown = true;
        Set(session, state, true); game.Accept(Skill(game, "P2", "fixed", 4, 3)); game.Accept(game.Command("RollDice", active));
        Assert.NotNull(game.Authority().Dice.FixedResult);
        game.Accept(game.Command("AdvanceThreeKingdomsPhase", active)); Assert.Equal(ThreeKingdomsPhase.Play, game.Authority().Clock.Phase);
        game.Accept(game.Command("EndTurn", active));
        while (game.Authority().Skills.PendingDecision != null)
        {
            var decision = game.Authority().Skills.PendingDecision; var actions = game.Tk(decision.PlayerId).SkillActions;
            var selected = actions.FirstOrDefault(a => a.Command.Option == "skip") ?? actions[0];
            dynamic command = game.Command("UseGeneralSkill", decision.PlayerId); command.SkillCommand = selected.Command; game.Accept(command);
        }
        game.Accept(game.Command("AdvanceThreeKingdomsPhase", active));
        Assert.Null(game.Authority().Dice.FixedResult); Assert.True(game.Authority().Dice.Roll.Submitted); Assert.Empty(B3DiceEngine.Validate(game.Authority()));
        if (roundEnd)
        {
            Assert.Equal(ThreeKingdomsPhase.RoundEnd, game.Authority().Clock.Phase); Assert.NotNull(game.Authority().PendingDecision);
            Assert.Equal(ThreeKingdomsDecisionKind.SelectReplacement, game.Authority().PendingDecision.Kind);
        }
        else
        {
            Assert.Equal("P3", game.Authority().Clock.ActivePlayerId); Assert.False(game.Authority().Players.Single(p => p.PlayerId == "P2").FaceDown);
            Assert.Contains("P2", game.Authority().Clock.SkippedOpportunityPlayerIds); Assert.Equal(2, game.Authority().Clock.NormalOpportunitiesConsumed);
        }
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void RealObserveCandidatesAndEveryChoiceRestoreByCommandReplayWithOneFinalHistory(string rules)
    {
        var game = NormalObserveGame(rules);
        for (int step = 0; game.Authority().Dice?.PendingDecision?.Kind != "dice-observe" && step < 1000; step++) game.Accept(game.Decide());
        Assert.Equal("dice-observe", game.Authority().Dice?.PendingDecision?.Kind);
        string actor = game.Authority().Dice.PendingDecision.PlayerId;
        int priorRolls = ((GameHistoryEntry[])game.History()).Count(h => h.Kind == "RollDice");
        int previousRed = game.RawAuthority().LastDice1, previousWhite = game.RawAuthority().LastDice2;
        game.AcceptAndResume(Skill(game, actor, "observe"));
        var choices = new HashSet<string>(); object? finalCommand = null;
        for (int step = 0; game.Authority().Dice.PendingDecision != null && step < 12; step++)
        {
            var dice = game.Authority().Dice;
            Assert.Equal(previousRed, (int)game.RawAuthority().LastDice1); Assert.Equal(previousWhite, (int)game.RawAuthority().LastDice2);
            choices.Add(dice.PendingDecision.Kind + ":" + dice.Roll.ChoiceCursor);
            dynamic command = game.Decide(); finalCommand = command; PreviewRepeatedly(game, command); game.AcceptAndResume(command);
        }
        Assert.True(game.Authority().Dice.Roll.Submitted); Assert.Contains("dice-select:0", choices); Assert.Contains("dice-select:1", choices); Assert.Contains("dice-select:2", choices);
        Assert.Equal(priorRolls + 1, ((GameHistoryEntry[])game.History()).Count(h => h.Kind == "RollDice"));
        DuplicateDoesNothing(game, (dynamic)finalCommand!);
        Assert.Equal(game.Save(), ThreeKingdomsTestGame.Load(rules, game.Save()).Save());
        var tampered = JsonNode.Parse(game.Save())!; tampered["ThreeKingdoms"]!["Dice"]!["Roll"]!["Candidates"]![0]!["OriginalRed"] = 6;
        if (game.Authority().Dice.Roll.Candidates[0].OriginalRed == 6) tampered["ThreeKingdoms"]!["Dice"]!["Roll"]!["Candidates"]![0]!["OriginalRed"] = 5;
        Assert.Throws<InvalidDataException>(() => ThreeKingdomsTestGame.Load(rules, tampered.ToJsonString()));
    }

    private static ThreeKingdomsTestGame NormalObserveGame(string rules)
    {
        foreach (uint seed in new uint[] { 97, 42, 71, 18, 3 })
        {
            object session = rules == "cities" ? M4.CitiesKnightsGameSession.Create(AcceptanceFixture.Json, 4, seed, threeKingdoms: true, threeKingdomsSkills: true, threeKingdomsPackageVersion: GeneralCatalog.B3PackageVersion)
                : M5.CombinedGameSession.Create("heading-for-new-shores", 4, seed, threeKingdoms: true, threeKingdomsSkills: true, threeKingdomsPackageVersion: GeneralCatalog.B3PackageVersion);
            var game = new ThreeKingdomsTestGame(rules, session);
            while (!game.Tk().OpeningComplete)
            {
                var d = game.Authority().PendingDecision;
                var candidates = game.Tk(d.PlayerId).PendingDecision.CandidateIds.OrderBy(id => id == "tk-g19" ? 0 : 1).ToArray();
                game.Accept(game.Command(d.Kind == ThreeKingdomsDecisionKind.SelectOpening ? "ChooseGenerals" : "DeployGeneral", d.PlayerId, candidates.Take(d.RequiredCount).ToArray()));
            }
            if (game.Authority().Generals.Any(g => g.Region == GeneralRegion.Active && g.GeneralId == "tk-g19")) return game;
        }
        throw new InvalidOperationException("正常候选中未找到诸葛亮，不能把指定将夹具伪作重放检查。");
    }
}
