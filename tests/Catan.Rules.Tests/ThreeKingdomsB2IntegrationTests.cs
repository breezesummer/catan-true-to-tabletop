using Catan.Core;
using Catan.Core.ThreeKingdoms;
using M4 = Catan.Core.M4;
using Xunit;

namespace Catan.Rules.Tests;

/// <summary>独立原生规则夹具，验证技能并非只在模拟宿主中成立；此类不作为重放证据。</summary>
public sealed class ThreeKingdomsB2IntegrationTests
{
    private static M4.CitiesKnightsGameSession Fixture(string[] generals, Action<M4.GameState>? configure = null)
    {
        return M4CoreHarness.Fixture(s =>
        {
            s.RulesVersion = M4.CitiesKnightsGameSession.ThreeKingdomsB2RulesVersion;
            var tk = ThreeKingdomsEngine.Create(new[] { "P1", "P2", "P3", "P4" }, 42, _ => 0, true, GeneralCatalog.B2PackageVersion);
            while (!tk.OpeningComplete)
            {
                var d = tk.PendingDecision;
                if (d.Kind == ThreeKingdomsDecisionKind.SelectOpening)
                    ThreeKingdomsEngine.ChooseGenerals(tk, d.PlayerId, d.CandidateIds.Take(2).ToArray(), d.DecisionId, tk.WindowId, _ => 0);
                else ThreeKingdomsEngine.DeployGeneral(tk, d.PlayerId, d.CandidateIds[0], d.DecisionId, tk.WindowId, _ => 0);
            }
            ThreeKingdomsEngine.BeginNormalTurns(tk);
            ThreeKingdomsEngine.AdvancePhase(tk, "P1", tk.WindowId);
            ThreeKingdomsEngine.AdvancePhase(tk, "P1", tk.WindowId);
            ThreeKingdomsEngine.MarkDrawPhase(tk);
            ThreeKingdomsEngine.AdvancePhase(tk, "P1", tk.WindowId);
            for (int i = 0; i < generals.Length; i++)
            {
                var current = tk.Generals.Single(g => g.Region == GeneralRegion.Active && g.OwnerPlayerId == "P" + (i + 1));
                var wanted = tk.Generals.Single(g => g.GeneralId == generals[i]);
                (current.GeneralId, wanted.GeneralId) = (wanted.GeneralId, current.GeneralId);
                (current.InstanceId, wanted.InstanceId) = (wanted.InstanceId, current.InstanceId);
            }
            s.ThreeKingdoms = tk;
            configure?.Invoke(s);
        });
    }

    private static M4.Command Cmd(M4.CitiesKnightsGameSession game, M4.CommandKind kind, string player = "P1") => new()
    {
        Id = ThreeKingdomsTestGame.NextId(), Kind = kind, PlayerId = player,
        WindowId = game.GetPlayerView(player).ThreeKingdoms.WindowId
    };
    private static void Accept(M4.CitiesKnightsGameSession game, M4.Command command)
    {
        var result = game.Execute(command); Assert.True(result.Success, result.ErrorCode + ":" + result.Message); game.AssertInvariants();
    }
    private static void Resolve(M4.CitiesKnightsGameSession game, string player, int type = 0)
    {
        var view = game.GetPlayerView(player);
        var action = view.ThreeKingdoms.SkillActions.First(a => a.Command.CardType == type);
        var command = Cmd(game, M4.CommandKind.UseGeneralSkill, player); command.SkillCommand = action.Command; Accept(game, command);
    }

    [Fact]
    public void ActualRoadPaymentTriggersLianyingOnceAndCannotBorrowItsRewardForCost()
    {
        var board = M4CoreHarness.New().GetPlayerView("P1").Board;
        string vertex = board.Vertices[0].Id;
        string edge = board.Edges.First(e => e.Vertices.Contains(vertex)).Id;
        var game = Fixture(new[] { "tk-g23" }, s => {
            s.Settlements = new[] { M4CoreHarness.Piece(vertex) };
            M4CoreHarness.Player(s).Resources = new ResourceBag { Wood = 1, Brick = 1 };
        });
        var build = Cmd(game, M4.CommandKind.BuildRoad); build.TargetId = edge;
        Accept(game, build);
        Assert.Contains(game.GetAuthoritativeStateForTesting().Roads, r => r.LocationId == edge);
        Assert.Equal(0, M4CoreHarness.Player(game.GetAuthoritativeStateForTesting()).Resources.Total);
        Assert.Contains("连营", game.GetPlayerView("P1").ThreeKingdoms.SkillDecision.Title);
        Resolve(game, "P1", 2);
        Assert.Equal(2, M4CoreHarness.Player(game.GetAuthoritativeStateForTesting()).Resources.Total);
        Assert.Null(game.GetPlayerView("P1").ThreeKingdoms.SkillDecision);
        var poor = Fixture(new[] { "tk-g23" }, s => {
            s.Settlements = new[] { M4CoreHarness.Piece(vertex) };
            M4CoreHarness.Player(s).Resources.Wood = 1;
        });
        var invalid = Cmd(poor, M4.CommandKind.BuildRoad); invalid.TargetId = edge;
        string before = poor.Save(); Assert.False(poor.Execute(invalid).Success); Assert.Equal(before, poor.Save());
    }

    [Theory][InlineData(M4.ProgressCardKind.Espionage)][InlineData(M4.ProgressCardKind.GuildDues)]
    public void QianxunRejectsNativeTargetBeforeConsumingCard(M4.ProgressCardKind kind)
    {
        var game = Fixture(new[] { "tk-g22", "tk-g23" }, s => {
            M4CoreHarness.Player(s).ProgressCards = new[] { kind };
            M4CoreHarness.Player(s, "P2").DefenderPoints = 2;
            M4CoreHarness.Player(s, "P2").Resources.Wood = 2;
        });
        var command = Cmd(game, M4.CommandKind.PlayProgressCard); command.ProgressCard = kind; command.OtherPlayerId = "P2";
        string before = game.Save(); Assert.False(game.Execute(command).Success); Assert.Equal(before, game.Save());
        Assert.DoesNotContain(game.GetPlayerView("P1").LegalActions, c => c.Kind == command.Kind && c.ProgressCard == kind && c.OtherPlayerId == "P2");
    }

    [Fact]
    public void NativeGuildDuesWaitsForYijiAndCountsNewRewardBeforeTakingCards()
    {
        var game = Fixture(new[] { "tk-g22", "tk-g37" }, s => {
            M4CoreHarness.Player(s).ProgressCards = new[] { M4.ProgressCardKind.GuildDues };
            M4CoreHarness.Player(s, "P2").DefenderPoints = 2;
            M4CoreHarness.Player(s, "P2").Resources.Wood = 1;
        });
        var command = Cmd(game, M4.CommandKind.PlayProgressCard); command.ProgressCard = M4.ProgressCardKind.GuildDues; command.OtherPlayerId = "P2";
        Accept(game, command);
        Assert.Equal("P2", game.GetPlayerView("P1").ThreeKingdoms.SkillDecision.PlayerId);
        Assert.Empty(game.GetPlayerView("P1").ThreeKingdoms.SkillActions);
        Assert.NotNull(game.GetAuthoritativeStateForTesting().ThreeKingdomsDeferredCommand);
        Resolve(game, "P2", 2);
        var after = game.GetAuthoritativeStateForTesting();
        Assert.Null(after.ThreeKingdomsDeferredCommand);
        Assert.Equal("ProgressGuildDues", after.PendingDecision.Kind);
        Assert.Equal(2, after.PendingDecision.Amount);
        Assert.Empty(M4CoreHarness.Player(after).ProgressCards);
        Assert.Equal(2, M4CoreHarness.Player(after, "P2").Resources.Total);
    }

    [Fact]
    public void KejiStopsNativeMonopolyButDoesNotBlockVoluntaryTrade()
    {
        var game = Fixture(new[] { "tk-g22", "tk-g06" }, s => {
            M4CoreHarness.Player(s).ProgressCards = new[] { M4.ProgressCardKind.ResourceMonopoly };
            M4CoreHarness.Player(s).Resources.Brick = 1;
            M4CoreHarness.Player(s, "P2").Resources.Wood = 2;
            s.ThreeKingdoms.Skills.TimedEffects = new[] { new B2TimedEffect { Id = "保护夹具", Kind = "protection", PlayerId = "P2", OpportunityId = s.ThreeKingdoms.Clock.OpportunityId } };
        });
        var monopoly = Cmd(game, M4.CommandKind.PlayProgressCard); monopoly.ProgressCard = M4.ProgressCardKind.ResourceMonopoly; monopoly.Resource = Resource.Wood; Accept(game, monopoly);
        Assert.Equal(2, M4CoreHarness.Player(game.GetAuthoritativeStateForTesting(), "P2").Resources.Wood);
        var offer = Cmd(game, M4.CommandKind.ProposeTrade); offer.OtherPlayerId = "P2"; offer.Give = new ResourceBag { Brick = 1 }; offer.Receive = new ResourceBag { Wood = 1 }; Accept(game, offer);
        Accept(game, Cmd(game, M4.CommandKind.AcceptTrade, "P2"));
        Assert.Equal(1, M4CoreHarness.Player(game.GetAuthoritativeStateForTesting(), "P2").Resources.Wood);
    }

    [Fact]
    public void QicaiAllowsNativeEndWithFiveProgressCardsWithoutDiscard()
    {
        var game = Fixture(new[] { "tk-g33" }, s => M4CoreHarness.Player(s).ProgressCards = new[] { M4.ProgressCardKind.Alchemy, M4.ProgressCardKind.Crane, M4.ProgressCardKind.Irrigation, M4.ProgressCardKind.Medicine, M4.ProgressCardKind.Mining });
        Accept(game, Cmd(game, M4.CommandKind.EndTurn));
        while (game.GetPlayerView("P1").ThreeKingdoms.SkillDecision != null) Resolve(game, game.GetPlayerView("P1").ThreeKingdoms.SkillDecision.PlayerId);
        Accept(game, Cmd(game, M4.CommandKind.AdvanceThreeKingdomsPhase));
        Assert.Equal(5, M4CoreHarness.Player(game.GetAuthoritativeStateForTesting()).ProgressCards.Length);
    }

    [Fact]
    public void JizhiWaitsUntilBothFreeRoadChoicesHaveCompleted()
    {
        string vertex = M4CoreHarness.New().GetPlayerView("P1").Board.Vertices[0].Id;
        var game = Fixture(new[] { "tk-g33" }, s => {
            s.Settlements = new[] { M4CoreHarness.Piece(vertex) };
            M4CoreHarness.Player(s).ProgressCards = new[] { M4.ProgressCardKind.RoadBuilding };
        });
        var play = Cmd(game, M4.CommandKind.PlayProgressCard); play.ProgressCard = M4.ProgressCardKind.RoadBuilding; Accept(game, play);
        Assert.Equal(0, M4CoreHarness.Player(game.GetAuthoritativeStateForTesting()).Resources.Total);
        var road = game.GetPlayerView("P1").LegalActions.First(c => c.Kind == M4.CommandKind.ResolveProgressChoice && !c.Decline);
        road.Id = ThreeKingdomsTestGame.NextId(); Accept(game, road);
        Assert.Equal(0, M4CoreHarness.Player(game.GetAuthoritativeStateForTesting()).Resources.Total);
        var finish = Cmd(game, M4.CommandKind.ResolveProgressChoice); finish.Decline = true; Accept(game, finish);
        Assert.Equal(1, M4CoreHarness.Player(game.GetAuthoritativeStateForTesting()).Resources.Total);
        Assert.Null(game.GetAuthoritativeStateForTesting().ThreeKingdomsProgressUse);
    }

    [Fact]
    public void CommercialHarborExchangeOfLastResourceForCommodityDoesNotTriggerLianyingHalfway()
    {
        var game = Fixture(new[] { "tk-g23", "tk-g06" }, s => {
            M4CoreHarness.Player(s).Resources.Wood = 1;
            M4CoreHarness.Player(s).ProgressCards = new[] { M4.ProgressCardKind.CommercialHarbor };
            M4CoreHarness.Player(s, "P2").Commodities.Cloth = 1;
        });
        PlayHarbor(game);
        OfferHarbor(game, "P2", Resource.Wood);
        var halfway = game.GetAuthoritativeStateForTesting();
        Assert.Equal("ProgressHarbor", halfway.PendingDecision.Kind);
        Assert.Null(halfway.ThreeKingdoms.Skills.PendingDecision);
        Assert.Equal(0, M4CoreHarness.Player(halfway).Resources.Total);
        var response = Cmd(game, M4.CommandKind.ResolveProgressChoice, "P2"); response.Commodity = M4.Commodity.Cloth; Accept(game, response);
        var final = game.GetAuthoritativeStateForTesting();
        Assert.Equal(0, M4CoreHarness.Player(final).Resources.Total);
        Assert.Equal(1, M4CoreHarness.Player(final).Commodities.Cloth);
        Assert.Null(final.ThreeKingdoms.Skills.PendingDecision);
        Assert.Equal(1, final.ThreeKingdoms.Skills.LossLedger.Single(l => l.PlayerId == "P1" && l.TurnId == final.ThreeKingdoms.Clock.TurnInstanceId).LostHandCards);
    }

    [Fact]
    public void RendeCountsTwoSuccessfulCommercialHarborExchangesBeforeGivingOneReward()
    {
        var game = Fixture(new[] { "tk-g15", "tk-g06", "tk-g09" }, s => {
            M4CoreHarness.Player(s).Resources.Wood = 2;
            M4CoreHarness.Player(s).ProgressCards = new[] { M4.ProgressCardKind.CommercialHarbor };
            M4CoreHarness.Player(s, "P2").Commodities.Cloth = 1;
            M4CoreHarness.Player(s, "P3").Commodities.Paper = 1;
        });
        PlayHarbor(game);
        OfferHarbor(game, "P2", Resource.Wood);
        var first = Cmd(game, M4.CommandKind.ResolveProgressChoice, "P2"); first.Commodity = M4.Commodity.Cloth; Accept(game, first);
        Assert.Null(game.GetAuthoritativeStateForTesting().ThreeKingdoms.Skills.PendingDecision);
        OfferHarbor(game, "P3", Resource.Wood);
        Assert.Null(game.GetAuthoritativeStateForTesting().ThreeKingdoms.Skills.PendingDecision);
        var second = Cmd(game, M4.CommandKind.ResolveProgressChoice, "P3"); second.Commodity = M4.Commodity.Paper; Accept(game, second);
        Assert.Contains("仁德", game.GetPlayerView("P1").ThreeKingdoms.SkillDecision.Title);
        Resolve(game, "P1", 4);
        var final = M4CoreHarness.Player(game.GetAuthoritativeStateForTesting());
        Assert.Equal(1, final.Resources.Ore); Assert.Equal(0, final.Resources.Wood);
        Assert.Equal(1, final.Commodities.Cloth); Assert.Equal(1, final.Commodities.Paper);
    }

    [Fact]
    public void CommercialHarborConfirmedTargetGetsYijiBeforeResourceOrCommodityTransfers()
    {
        var game = Fixture(new[] { "tk-g22", "tk-g37" }, s => {
            M4CoreHarness.Player(s).Resources.Wood = 1;
            M4CoreHarness.Player(s).ProgressCards = new[] { M4.ProgressCardKind.CommercialHarbor };
            M4CoreHarness.Player(s, "P2").Commodities.Cloth = 1;
        });
        PlayHarbor(game);
        Assert.Null(game.GetAuthoritativeStateForTesting().ThreeKingdoms.Skills.PendingDecision);
        OfferHarbor(game, "P2", Resource.Wood);
        var targetWindow = game.GetAuthoritativeStateForTesting();
        Assert.Equal("P2", targetWindow.ThreeKingdoms.Skills.PendingDecision.PlayerId);
        Assert.Contains("遗计", targetWindow.ThreeKingdoms.Skills.PendingDecision.Title);
        Assert.Equal(1, M4CoreHarness.Player(targetWindow).Resources.Wood);
        Assert.Equal(0, M4CoreHarness.Player(targetWindow, "P2").Resources.Total);
        Assert.Equal(1, M4CoreHarness.Player(targetWindow, "P2").Commodities.Cloth);
        Resolve(game, "P2", 2);
        Assert.Equal("ProgressHarbor", game.GetAuthoritativeStateForTesting().PendingDecision.Kind);
        var response = Cmd(game, M4.CommandKind.ResolveProgressChoice, "P2"); response.Commodity = M4.Commodity.Cloth; Accept(game, response);
        var final = game.GetAuthoritativeStateForTesting();
        Assert.Equal(1, M4CoreHarness.Player(final, "P2").Resources.Wool);
        Assert.Equal(1, M4CoreHarness.Player(final, "P2").Resources.Wood);
        Assert.Equal(1, M4CoreHarness.Player(final).Commodities.Cloth);
        Assert.Null(final.ThreeKingdoms.Skills.PendingDecision);
    }

    [Theory][InlineData("tk-g06")][InlineData("tk-g09")]
    public void RetiringBeforeOptionalEndSkillIsAcceptedRemovesOldCandidateWithoutReplayingEndEvent(string general)
    {
        var game = Fixture(new[] { general });
        Accept(game, Cmd(game, M4.CommandKind.EndTurn));
        var oldDecision = game.GetAuthoritativeStateForTesting().ThreeKingdoms.Skills.PendingDecision;
        Assert.Equal("optional", oldDecision.Kind);
        var oldAction = game.GetPlayerView("P1").ThreeKingdoms.SkillActions.Single(a => a.Command.Option == "accept").Command;
        Accept(game, Cmd(game, M4.CommandKind.RetireGeneral));
        while (game.GetPlayerView("P1").ThreeKingdoms.PendingDecision is { } d)
        {
            var select = Cmd(game, d.Kind == ThreeKingdomsDecisionKind.SelectReplacement ? M4.CommandKind.ChooseGenerals : M4.CommandKind.DeployGeneral);
            select.DecisionId = d.DecisionId; select.GeneralIds = d.CandidateIds.Take(d.RequiredCount).ToArray();
            Accept(game, select);
        }
        var after = game.GetAuthoritativeStateForTesting();
        Assert.DoesNotContain(after.ThreeKingdoms.Skills.Frames, f => f.Id == oldDecision.FrameId);
        Assert.Null(after.ThreeKingdoms.Skills.PendingDecision);
        Assert.Equal(0, M4CoreHarness.Player(after).Resources.Total);
        var stale = Cmd(game, M4.CommandKind.UseGeneralSkill); stale.SkillCommand = oldAction;
        string before = game.Save(); Assert.False(game.Execute(stale).Success); Assert.Equal(before, game.Save());
    }

    private static void PlayHarbor(M4.CitiesKnightsGameSession game)
    {
        var play = Cmd(game, M4.CommandKind.PlayProgressCard); play.ProgressCard = M4.ProgressCardKind.CommercialHarbor; Accept(game, play);
    }
    private static void OfferHarbor(M4.CitiesKnightsGameSession game, string target, Resource resource)
    {
        var offer = Cmd(game, M4.CommandKind.CommercialHarborOffer); offer.OtherPlayerId = target; offer.Resource = resource; Accept(game, offer);
    }
}
