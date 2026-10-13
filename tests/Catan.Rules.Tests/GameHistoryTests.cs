using System.Text.Json;
using Catan.AI;
using Catan.Core;
using Xunit;
using M2 = Catan.Core.M2;
using M3 = Catan.Core.M3;
using M4 = Catan.Core.M4;
using M5 = Catan.Core.M5;

namespace Catan.Rules.Tests;

public sealed class GameHistoryTests
{
    [Fact]
    public void BuildingHistoryRecordsExactCostAndPieceWithoutFullHand()
    {
        var game = M2Harness.Fixture(s =>
        {
            s.Settlements = [M2Harness.Piece("V01")];
            M2Harness.Player(s).Resources = M2Harness.Bag(wood: 3, brick: 2, ore: 4);
        });
        var command = M2Harness.Cmd(M2.CommandKind.BuildRoad, target: game.GetPlayerView("P1").LegalEdgeIds.First());
        M2Harness.Accepted(game, command);
        var entry = Assert.Single(game.GetHistory("P2"));
        Assert.Equal("建造道路", entry.Action);
        Assert.Equal(9, entry.Turn);
        Assert.Contains(entry.Changes, c => c.Category == "goods" && c.GoodIndex == 0 && c.Amount == -1);
        Assert.Contains(entry.Changes, c => c.Category == "goods" && c.GoodIndex == 1 && c.Amount == -1);
        Assert.Contains(entry.Changes, c => c.Category == "piece" && c.Name == "道路" && c.Amount == 1 && c.LocationId == command.TargetId);
        Assert.DoesNotContain(entry.Changes, c => c.GoodIndex == 4);
        Assert.True(entry.Involves("P1")); Assert.False(entry.Involves("P3"));
        Assert.Throws<ArgumentException>(() => game.GetHistory("P9"));
    }

    [Fact]
    public void InvalidPreviewAndRepeatedCommandsDoNotAppendHistory()
    {
        var game = M2Harness.New();
        var view = game.GetPlayerView(game.GetPlayerView("P1").ActivePlayerId);
        var legal = M2Harness.Cmd(M2.CommandKind.SetupSettlement, view.PlayerId, view.LegalVertexIds.First());
        Assert.True(game.Preview(legal).Success); Assert.Empty(game.GetHistory("P1"));
        var illegal = M2Harness.Cmd(M2.CommandKind.SetupSettlement, view.PlayerId, "不存在的顶点");
        M2Harness.Rejected(game, illegal); Assert.Empty(game.GetHistory("P1"));
        M2Harness.Accepted(game, legal);
        Assert.True(game.Execute(legal).IsDuplicate); Assert.Single(game.GetHistory("P1"));
        legal.TargetId = "更改同一指令"; Assert.False(game.Execute(legal).Success); Assert.Single(game.GetHistory("P1"));
        Assert.Empty(game.GetHistory("P1", game.GetHistory("P1")[0].Sequence));
    }

    [Fact]
    public void DevelopmentPurchaseOnlyRevealsCardIdentityToOwner()
    {
        var game = M2Harness.Fixture(s => M2Harness.Player(s).Resources = M2Harness.Bag(wool: 1, wheat: 1, ore: 1));
        M2Harness.Accepted(game, M2Harness.Cmd(M2.CommandKind.BuyDevelopmentCard));
        var own = Assert.Single(game.GetHistory("P1"));
        var other = Assert.Single(game.GetHistory("P2"));
        Assert.Contains(own.Changes, c => c.Category == "development" && c.Name == "骑士" && c.IsPrivate && c.Amount == 1);
        Assert.Contains(other.Changes, c => c.Category == "development" && c.Name == "发展卡" && c.Amount == 1);
        Assert.DoesNotContain(other.Changes, c => c.Name == "骑士");
        Assert.Equal(3, other.Changes.Count(c => c.Category == "goods" && c.Amount == -1));
    }

    [Fact]
    public void MonopolyNamesPlayedCardAndEveryAffectedPlayersResourceChange()
    {
        var game = M2Harness.Fixture(s =>
        {
            M2Harness.Card(s, M2.DevelopmentCardKind.Monopoly);
            M2Harness.Player(s, "P2").Resources.Ore = 2; M2Harness.Player(s, "P3").Resources.Ore = 3;
        });
        var command = M2Harness.Cmd(M2.CommandKind.PlayMonopoly); command.Resource = Resource.Ore;
        M2Harness.Accepted(game, command);
        var entry = Assert.Single(game.GetHistory("P4"));
        Assert.Equal("打出「垄断」", entry.Action);
        Assert.Contains(entry.Changes, c => c.PlayerId == "P1" && c.GoodIndex == 4 && c.Amount == 5);
        Assert.Contains(entry.Changes, c => c.PlayerId == "P2" && c.GoodIndex == 4 && c.Amount == -2);
        Assert.Contains(entry.Changes, c => c.PlayerId == "P3" && c.GoodIndex == 4 && c.Amount == -3);
        Assert.True(entry.Involves("P2")); Assert.True(entry.Involves("P3"));
    }

    [Fact]
    public void StolenResourceIdentityIsVisibleOnlyToThiefAndVictim()
    {
        var game = M2Harness.Fixture(s =>
        {
            s.Phase = M2.GamePhase.RobberSteal;
            s.Settlements = [M2Harness.Piece(M2Harness.New().GetPlayerView("P1").Board.Tiles.Single(t => t.Id == s.RobberTileId).Vertices[0], "P2")];
            s.PendingDecision = new M2.PendingDecision { Kind = "Steal", PlayerId = "P1", EligibleVictimIds = ["P2"], ReturnPhase = M2.GamePhase.Action };
            M2Harness.Player(s, "P2").Resources.Ore = 1;
        });
        var command = M2Harness.Cmd(M2.CommandKind.StealResource); command.OtherPlayerId = "P2";
        M2Harness.Accepted(game, command);
        foreach (var viewer in new[] { "P1", "P2" }) Assert.Equal(2, game.GetHistory(viewer)[0].Changes.Count(c => c.GoodIndex == 4));
        var observer = game.GetHistory("P3")[0];
        Assert.All(observer.Changes, c => { Assert.Equal("resource-count", c.Category); Assert.Equal(-1, c.GoodIndex); });
        Assert.Equal(new[] { 1, -1 }, observer.Changes.Select(c => c.Amount));
        Assert.DoesNotContain("矿石", JsonSerializer.Serialize(observer));
    }

    [Fact]
    public void ForcedResourceDiscardOnlyShowsCountToOtherPlayers()
    {
        var game = M2Harness.Fixture(s =>
        {
            s.Phase = M2.GamePhase.Discard;
            s.PendingDecision = new M2.PendingDecision { Kind = "Robber", PlayerId = "P1", ReturnPhase = M2.GamePhase.Action };
            s.Discards = [new M2.DiscardRequirement { PlayerId = "P2", Amount = 4 }];
            M2Harness.Player(s, "P2").Resources.Ore = 9;
        });
        var command = M2Harness.Cmd(M2.CommandKind.DiscardResources, "P2"); command.Resources = M2Harness.Bag(ore: 4);
        M2Harness.Accepted(game, command);
        Assert.Contains(game.GetHistory("P2")[0].Changes, c => c.GoodIndex == 4 && c.Amount == -4);
        var publicChange = Assert.Single(game.GetHistory("P1")[0].Changes);
        Assert.Equal("resource-count", publicChange.Category); Assert.Equal(-4, publicChange.Amount); Assert.Equal(-1, publicChange.GoodIndex);
    }

    [Fact]
    public void ThreeDiceHistoryIncludesEventFaceAndCityResourceCommodityProduction()
    {
        var tile = M4CoreHarness.New().GetPlayerView("P1").Board.Tiles.First(t => t.Resource == "wood");
        int red = Math.Min(6, tile.Number!.Value - 1), white = tile.Number.Value - red;
        var game = M4CoreHarness.Fixture(s =>
        {
            s.Phase = M4.GamePhase.ProductionAwaitRoll; s.AlchemyDice1 = red; s.AlchemyDice2 = white;
            s.Cities = [M4CoreHarness.Piece(tile.Vertices[0])];
        });
        M4CoreHarness.Accepted(game, M4CoreHarness.Cmd(M4.CommandKind.RollDice));
        var entry = Assert.Single(game.GetHistory("P2"));
        Assert.Equal(red, entry.Dice1); Assert.Equal(white, entry.Dice2);
        Assert.Equal(game.GetPlayerView("P2").LastEventDie.ToString(), entry.EventDie);
        Assert.Contains(entry.Changes, c => c.PlayerId == "P1" && c.GoodIndex == 0 && c.Amount == 1);
        Assert.Contains(entry.Changes, c => c.PlayerId == "P1" && c.GoodIndex == 7 && c.Amount == 1);
    }

    [Fact]
    public void EspionageLogsTransferWithoutExposingCardToUnrelatedPlayers()
    {
        var game = M4ProgressHarness.Fixture(M4.ProgressCardKind.Espionage, s => M4ProgressHarness.P(s, "P2").ProgressCards = [M4.ProgressCardKind.Medicine]);
        var play = M4ProgressHarness.Play(M4.ProgressCardKind.Espionage); play.OtherPlayerId = "P2";
        M4ProgressHarness.Accepted(game, play);
        var choose = M4ProgressHarness.Cmd(M4.CommandKind.ResolveProgressChoice); choose.SelectedProgressCard = M4.ProgressCardKind.Medicine;
        choose.OtherPlayerId = "P3"; // 被规则忽略的恶意字段不得为第三人授予历史知情权。
        M4ProgressHarness.Accepted(game, choose);
        Assert.Equal("打出「间谍」", game.GetHistory("P3")[0].Action);
        foreach (var viewer in new[] { "P1", "P2" }) Assert.Equal(2, game.GetHistory(viewer)[1].Changes.Count(c => c.Name == "医学"));
        Assert.DoesNotContain(game.GetHistory("P3")[1].Changes, c => c.Name == "医学");
        Assert.Equal(new[] { 1, -1 }, game.GetHistory("P3")[1].Changes.Select(c => c.Amount));
    }

    [Fact]
    public void SabotageCasterCannotReadOpponentsPrivateDiscard()
    {
        var game = M4ProgressHarness.Fixture(M4.ProgressCardKind.Sabotage, s => M4ProgressHarness.P(s, "P2").Commodities.Paper = 4);
        M4ProgressHarness.Accepted(game, M4ProgressHarness.Play(M4.ProgressCardKind.Sabotage));
        var discard = M4ProgressHarness.Cmd(M4.CommandKind.ResolveProgressChoice, "P2"); discard.Commodities = new M4.CommodityBag { Paper = 2 };
        M4ProgressHarness.Accepted(game, discard);
        Assert.Contains(game.GetHistory("P2")[1].Changes, c => c.GoodIndex == 7 && c.Amount == -2);
        Assert.DoesNotContain(game.GetHistory("P1")[1].Changes, c => c.GoodIndex == 7);
        Assert.Contains(game.GetHistory("P1")[1].Changes, c => c.Category == "resource-count" && c.Amount == -2);
    }

    [Theory]
    [InlineData(M4.ProgressCardKind.GuildDues)][InlineData(M4.ProgressCardKind.Wedding)]
    public void ForgedCounterpartyCannotGrantAccessToPrivateProgressTransfers(M4.ProgressCardKind card)
    {
        var game = M4ProgressHarness.Fixture(card, s =>
        {
            s.Cities = [M4ProgressHarness.Piece(M4ProgressHarness.FirstVertex, "P2")];
            M4ProgressHarness.P(s, "P2").Resources.Ore = 2;
        });
        var play = M4ProgressHarness.Play(card); play.OtherPlayerId = "P2"; M4ProgressHarness.Accepted(game, play);
        var choose = M4ProgressHarness.Cmd(M4.CommandKind.ResolveProgressChoice, card == M4.ProgressCardKind.Wedding ? "P2" : "P1");
        choose.OtherPlayerId = "P3"; choose.Resources = new ResourceBag { Ore = 2 };
        M4ProgressHarness.Accepted(game, choose);
        foreach (var viewer in new[] { "P1", "P2" }) Assert.Equal(2, game.GetHistory(viewer)[1].Changes.Count(c => c.GoodIndex == 4));
        Assert.DoesNotContain(game.GetHistory("P3")[1].Changes, c => c.GoodIndex == 4);
        Assert.False(game.GetHistory("P3")[1].Involves("P3"));
    }

    [Fact]
    public void FreeCraneUpgradeStillRecordsTrackAndNewLevel()
    {
        var game = M4ProgressHarness.Fixture(M4.ProgressCardKind.Crane, s => s.Cities = [M4ProgressHarness.Piece(M4ProgressHarness.FirstVertex)]);
        var command = M4ProgressHarness.Play(M4.ProgressCardKind.Crane); command.Track = M4.ImprovementTrack.Science;
        M4ProgressHarness.Accepted(game, command);
        var entry = Assert.Single(game.GetHistory("P2"));
        Assert.Equal("打出「起重机」", entry.Action);
        Assert.Contains(entry.Changes, c => c.Category == "status" && c.Amount == 1 && c.Name == "科学改良（现为 1 级）");
        Assert.DoesNotContain(entry.Changes, c => c.Category == "goods");
    }

    [Fact]
    public void ClothTheftRecordsPublicTokenTransfer()
    {
        var board = M3Harness.New("cloth-for-catan").GetPlayerView("P1").Board;
        var sea = board.Tiles.First(t => t.Resource == "sea");
        var edge = board.Edges.First(e => e.Vertices.All(sea.Vertices.Contains));
        var game = M3Harness.Fixture(s =>
        {
            s.PirateTileId = sea.Id; s.Ships = [M3Harness.Ship(edge.Id, "P2")];
            M3Harness.Player(s, "P2").Cloth = 1; s.ClothSupply--;
            s.Phase = M3.GamePhase.RobberSteal;
            s.PendingDecision = new M3.PendingDecision { Kind = "Steal", PlayerId = "P1", Token = "pirate", EligibleVictimIds = ["P2"], ReturnPhase = M3.GamePhase.Action };
        }, "cloth-for-catan");
        var command = M3Harness.Cmd(M3.CommandKind.StealCloth); command.OtherPlayerId = "P2";
        M3Harness.Accepted(game, command);
        var entry = Assert.Single(game.GetHistory("P3"));
        Assert.Contains(entry.Changes, c => c.PlayerId == "P1" && c.Category == "count" && c.Name == "布匹筹码" && c.Amount == 1);
        Assert.Contains(entry.Changes, c => c.PlayerId == "P2" && c.Category == "count" && c.Amount == -1);
    }

    [Fact]
    public void TradeProposalRecordsQuoteAndAcceptanceRecordsActualExchange()
    {
        var game = M2Harness.Fixture(s => { M2Harness.Player(s).Resources.Wood = 2; M2Harness.Player(s, "P2").Resources.Ore = 1; });
        var offer = M2Harness.Cmd(M2.CommandKind.ProposeTrade); offer.OtherPlayerId = "P2";
        offer.Give = M2Harness.Bag(wood: 2); offer.Receive = M2Harness.Bag(ore: 1); M2Harness.Accepted(game, offer);
        Assert.Contains(game.GetHistory("P3")[0].Changes, c => c.Category == "offer" && c.GoodIndex == 0 && c.Amount == -2);
        Assert.Contains(game.GetHistory("P3")[0].Changes, c => c.Category == "offer" && c.GoodIndex == 4 && c.Amount == 1);
        M2Harness.Accepted(game, M2Harness.Cmd(M2.CommandKind.AcceptTrade, "P2"));
        var accepted = game.GetHistory("P3")[1]; Assert.True(accepted.Involves("P1")); Assert.True(accepted.Involves("P2"));
        Assert.Equal(4, accepted.Changes.Length);
        Assert.Contains(accepted.Changes, c => c.PlayerId == "P1" && c.GoodIndex == 0 && c.Amount == -2);
        Assert.Contains(accepted.Changes, c => c.PlayerId == "P2" && c.GoodIndex == 0 && c.Amount == 2);
    }

    [Theory]
    [InlineData("base")][InlineData("seafarers")][InlineData("cities")][InlineData("combined")]
    public void AllRuleSetsRebuildSameHistoryFromExistingAuthoritativeSave(string rules)
    {
        dynamic session = New(rules);
        for (int i = 0; i < 45; i++)
        {
            dynamic state = session.GetPlayerView("P1");
            dynamic view = session.GetPlayerView(M6Game.Actor(state));
            dynamic command = Decide(rules, view); command.Id = "历史恢复测试-" + i;
            Assert.True((bool)session.Execute(command).Success);
        }
        string save = session.Save();
        dynamic restored = Load(rules, save);
        foreach (string player in new[] { "P1", "P2", "P3", "P4" })
        {
            GameHistoryEntry[] original = session.GetHistory(player);
            GameHistoryEntry[] loaded = restored.GetHistory(player);
            Assert.Equal(45, original.Length);
            Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(loaded));
        }
        // 投影数组与权威历史断开：修改返回值不能影响下次读取。
        GameHistoryEntry[] projected = restored.GetHistory("P1"); projected[0].Changes[0] = null!; projected[0] = null!;
        GameHistoryEntry[] fresh = restored.GetHistory("P1"); Assert.NotNull(fresh[0]); Assert.NotNull(fresh[0].Changes[0]);
        Assert.Equal(save, (string)restored.Save());
        dynamic latest = session.GetPlayerView("P1");
        dynamic next = Decide(rules, session.GetPlayerView(M6Game.Actor(latest))); next.Id = "恢复后下一步";
        Assert.True((bool)session.Execute(next).Success); Assert.True((bool)restored.Execute(next).Success);
        Assert.Equal(JsonSerializer.Serialize((GameHistoryEntry[])session.GetHistory("P1")), JsonSerializer.Serialize((GameHistoryEntry[])restored.GetHistory("P1")));
    }

    private static object New(string rules) => rules switch
    {
        "base" => M2.BaseGameSession.Create(AcceptanceFixture.Json, 4, 97),
        "seafarers" => M3.SeafarersGameSession.Create("heading-for-new-shores", 4, 97),
        "cities" => M4.CitiesKnightsGameSession.Create(AcceptanceFixture.Json, 4, 97),
        _ => M5.CombinedGameSession.Create("heading-for-new-shores", 4, 97)
    };
    private static object Load(string rules, string save) => rules switch
    {
        "base" => M2.BaseGameSession.Load(AcceptanceFixture.Json, save), "seafarers" => M3.SeafarersGameSession.Load(save),
        "cities" => M4.CitiesKnightsGameSession.Load(AcceptanceFixture.Json, save), _ => M5.CombinedGameSession.Load(save)
    };
    private static object Decide(string rules, object view) => rules switch
    {
        "base" => new BaseGameAi().Decide((M2.PlayerView)view), "seafarers" => new SeafarersAi().Decide((M3.PlayerView)view),
        "cities" => new CitiesKnightsAi().Decide((M4.PlayerView)view), _ => new CombinedAi().Decide((M5.PlayerView)view)
    };
}
