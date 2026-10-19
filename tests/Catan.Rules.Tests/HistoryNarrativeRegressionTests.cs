using Catan.Core;
using Xunit;
using M2 = Catan.Core.M2;
using M3 = Catan.Core.M3;
using M4 = Catan.Core.M4;
using M5 = Catan.Core.M5;

namespace Catan.Rules.Tests;

public sealed class HistoryNarrativeRegressionTests
{
    [Theory]
    [InlineData("base", false)][InlineData("base", true)]
    [InlineData("seafarers", false)][InlineData("seafarers", true)]
    [InlineData("cities", false)][InlineData("cities", true)]
    [InlineData("combined", false)][InlineData("combined", true)]
    public void AutomaticallyRejectedTradeRetainsBothPublicQuoteSides(string rules, bool broadcast)
    {
        dynamic game = rules switch
        {
            "base" => M2Harness.Fixture(s => M2Harness.Player(s).Resources.Wood = 2),
            "seafarers" => M3Harness.Fixture(s => M3Harness.Player(s).Resources.Wood = 2),
            "cities" => M4CoreHarness.Fixture(s => M4CoreHarness.Player(s).Resources.Wood = 2),
            _ => M5Harness.Fixture(s => M5Harness.Player(s).Resources.Wood = 2)
        };
        dynamic command = rules switch
        {
            "base" => M2Harness.Cmd(M2.CommandKind.ProposeTrade),
            "seafarers" => M3Harness.Cmd(M3.CommandKind.ProposeTrade),
            "cities" => M4CoreHarness.Cmd(M4.CommandKind.ProposeTrade),
            _ => M5Harness.Cmd(M5.CommandKind.ProposeTrade)
        };
        command.OtherPlayerId = broadcast ? "*" : "P2";
        command.Give = new ResourceBag { Wood = 2 };
        command.Receive = new ResourceBag { Ore = 1 };
        Assert.True((bool)game.Execute(command).Success);
        Assert.Null((object?)game.GetPlayerView("P1").TradeOffer);
        foreach (string viewer in new[] { "P1", "P2", "P3", "P4" })
        {
            GameHistoryEntry[] history = game.GetHistory(viewer);
            var entry = Assert.Single(history);
            Assert.Equal(2, entry.Changes.Length);
            var line = Assert.Single(TabletopHistoryModel.Lines(entry));
            Assert.True(line.Quote);
            Assert.Equal(-2, Assert.Single(line.Cards).Amount);
            Assert.Equal(0, line.Cards[0].GoodIndex);
            Assert.Equal(1, Assert.Single(line.ReceivedCards).Amount);
            Assert.Equal(4, line.ReceivedCards[0].GoodIndex);
        }
    }

    [Fact]
    public void FreeEngineeringWallIsConstructedAndCardUsesRequestedVerb()
    {
        string site = M4ProgressHarness.FirstVertex;
        var game = M4ProgressHarness.Fixture(M4.ProgressCardKind.Engineering, s => s.Cities = [M4ProgressHarness.Piece(site)]);
        M4ProgressHarness.Accepted(game, M4ProgressHarness.Play(M4.ProgressCardKind.Engineering, site));
        var lines = TabletopHistoryModel.Lines(Assert.Single(game.GetHistory("P2")));
        Assert.Equal(new[] { "使用「工程学」", "建造了城墙" }, lines.Select(l => l.Prefix));
    }

    [Theory][InlineData(false, false)][InlineData(false, true)][InlineData(true, false)][InlineData(true, true)]
    public void PillagedCityHasOneCompletedSentence(bool combined, bool wall)
    {
        dynamic game;
        string site;
        if (combined)
        {
            site = M5Harness.New().GetPlayerView("P1").Board.Vertices[0].Id;
            game = M5Harness.Fixture(s =>
            {
                s.Cities = [M5Harness.Piece(site)];
                if (wall) s.Walls = [M5Harness.Piece(site)];
                s.Phase = M5.GamePhase.PendingChoice;
                s.PendingDecision = new() { Kind = "PillageCity", PlayerId = "P1", Options = [site] };
                s.DecisionReturnPhase = M5.GamePhase.Action;
            });
            M5Harness.Accepted(game, M5Harness.Cmd(M5.CommandKind.ResolveChoice, target: site));
        }
        else
        {
            site = M4ProgressHarness.FirstVertex;
            game = M4CoreHarness.Fixture(s =>
            {
                s.Cities = [M4CoreHarness.Piece(site)];
                if (wall) s.Walls = [M4CoreHarness.Piece(site)];
                s.Phase = M4.GamePhase.PendingChoice;
                s.PendingDecision = new() { Kind = "PillageCity", PlayerId = "P1", Options = [site] };
                s.DecisionReturnPhase = M4.GamePhase.Action;
            });
            M4CoreHarness.Accepted(game, M4CoreHarness.Cmd(M4.CommandKind.ResolveChoice, target: site));
        }
        GameHistoryEntry[] history = game.GetHistory("P2");
        var line = Assert.Single(TabletopHistoryModel.Lines(Assert.Single(history)));
        Assert.Equal("的城市被海盗掠夺，已" + (wall ? "失去城墙并" : "") + "降级为村庄", line.Prefix);
    }

    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public void PrivateProgressDrawOnlyPublishesBackColor(int track)
    {
        var game = M4CoreHarness.Fixture(s =>
        {
            s.Phase = M4.GamePhase.PendingChoice;
            s.PendingDecision = new() { Kind = "DefenderProgress", PlayerId = "P1" };
            s.DecisionReturnPhase = M4.GamePhase.Action;
        });
        var choose = M4CoreHarness.Cmd(M4.CommandKind.ResolveChoice);
        choose.Track = (M4.ImprovementTrack)track;
        M4CoreHarness.Accepted(game, choose);
        var own = Assert.Single(game.GetHistory("P1")).Changes.Single(c => c.Category == "progress");
        var projected = Assert.Single(game.GetHistory("P2"));
        var card = Assert.Single(projected.Changes);
        Assert.Equal("发展卡", card.Name);
        Assert.Equal(track, card.CardTrack);
        Assert.DoesNotContain(projected.Changes, c => c.Name == own.Name);
        var line = TabletopHistoryModel.Lines(projected).Single(l => l.Emphasis == "发展卡");
        Assert.Equal(track, line.Track);
        Assert.Equal("收获发展卡", line.Prefix);
    }

    [Theory][InlineData(M4.ProgressCardKind.Printing, "印刷术")][InlineData(M4.ProgressCardKind.Constitution, "宪法")]
    public void PublicVictoryProgressCardIncludesOnePoint(M4.ProgressCardKind card, string name)
    {
        var game = M4CoreHarness.Fixture(s =>
        {
            s.Phase = M4.GamePhase.PendingChoice;
            s.PendingDecision = new() { Kind = "DefenderProgress", PlayerId = "P1" };
            s.DecisionReturnPhase = M4.GamePhase.Action;
        });
        var state = game.GetAuthoritativeStateForTesting();
        var deck = state.ProgressDecks.Single(d => d.Track == M4.ProgressCatalog.Track(card));
        deck.Cards = new[] { card }.Concat(deck.Cards.Where(c => c != card)).ToArray();
        M4CoreHarness.SetState(game, state);
        var choose = M4CoreHarness.Cmd(M4.CommandKind.ResolveChoice);
        choose.Track = deck.Track;
        M4CoreHarness.Accepted(game, choose);
        var line = TabletopHistoryModel.Lines(Assert.Single(game.GetHistory("P2"))).Single(l => l.Emphasis == name);
        Assert.Equal("收获" + name + "（1分）", line.Prefix);
    }

    [Fact]
    public void DefenderAwardIncludesOnePoint()
    {
        var board = M4CoreHarness.New().GetPlayerView("P1").Board;
        var game = M4CoreHarness.Fixture(s =>
        {
            s.Cities = [M4CoreHarness.Piece(board.Vertices[0].Id)];
            s.Knights = [new() { PlayerId = "P2", LocationId = board.Vertices[^1].Id, Level = 1, Active = true }];
            s.BarbarianPosition = 6; s.Phase = M4.GamePhase.ProductionAwaitRoll;
            s.AlchemyDice1 = 6; s.AlchemyDice2 = 1; s.Random.State = 1;
        });
        M4CoreHarness.Accepted(game, M4CoreHarness.Cmd(M4.CommandKind.RollDice));
        Assert.Contains(TabletopHistoryModel.Lines(Assert.Single(game.GetHistory("P1"))), l => l.Player == "P2" && l.Prefix == "收获卡坦保卫者（1分）");
    }

    [Fact]
    public void SabotageDiscardIsOneColoredCountSentenceForEveryViewer()
    {
        var game = M4ProgressHarness.Fixture(M4.ProgressCardKind.Sabotage, s => M4ProgressHarness.P(s, "P2").Commodities.Paper = 4);
        M4ProgressHarness.Accepted(game, M4ProgressHarness.Play(M4.ProgressCardKind.Sabotage));
        var command = M4ProgressHarness.Cmd(M4.CommandKind.ResolveProgressChoice, "P2");
        command.Commodities = new() { Paper = 2 };
        M4ProgressHarness.Accepted(game, command);
        foreach (string viewer in new[] { "P1", "P2", "P3" })
        {
            var line = Assert.Single(TabletopHistoryModel.Lines(game.GetHistory(viewer)[1]));
            Assert.Equal("因「破坏」弃置了2张手牌", line.Prefix);
            Assert.Equal("破坏", line.Emphasis); Assert.Equal(1, line.Track); Assert.Empty(line.Cards);
        }
    }

    [Fact]
    public void SevenDiscardIsOneCountSentenceWithoutUsedCards()
    {
        var game = M2Harness.Fixture(s =>
        {
            s.Phase = M2.GamePhase.Discard;
            s.PendingDecision = new() { Kind = "Robber", PlayerId = "P1", ReturnPhase = M2.GamePhase.Action };
            s.Discards = [new() { PlayerId = "P2", Amount = 4 }];
            M2Harness.Player(s, "P2").Resources.Ore = 9;
        });
        var command = M2Harness.Cmd(M2.CommandKind.DiscardResources, "P2"); command.Resources = new() { Ore = 4 };
        M2Harness.Accepted(game, command);
        foreach (string viewer in new[] { "P1", "P2", "P3" })
        {
            var line = Assert.Single(TabletopHistoryModel.Lines(Assert.Single(game.GetHistory(viewer))));
            Assert.Equal("因掷出7弃置了4张手牌", line.Prefix); Assert.Empty(line.Cards);
        }
    }

    [Fact]
    public void ResourceMonopolyUsesHarvestForEachAffectedPlayer()
    {
        var game = M4ProgressHarness.Fixture(M4.ProgressCardKind.ResourceMonopoly, s =>
        {
            M4ProgressHarness.P(s, "P2").Resources.Brick = 2;
            M4ProgressHarness.P(s, "P3").Resources.Brick = 3;
        });
        var command = M4ProgressHarness.Play(M4.ProgressCardKind.ResourceMonopoly); command.Resource = Resource.Brick;
        M4ProgressHarness.Accepted(game, command);
        var lines = TabletopHistoryModel.Lines(Assert.Single(game.GetHistory("P4")));
        Assert.Equal("使用「资源垄断」", lines[0].Prefix);
        Assert.Equal(2, lines.Count(l => l.Prefix == "收获" && l.Partner != null));
        Assert.DoesNotContain(lines, l => l.Prefix.Contains("获得了"));
    }
}
