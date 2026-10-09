using Catan.Core;
using Catan.Core.M2;
using Xunit;
using static Catan.Rules.Tests.M2Harness;
using Command = Catan.Core.M2.Command;
using CommandKind = Catan.Core.M2.CommandKind;
using GamePhase = Catan.Core.M2.GamePhase;
using GameState = Catan.Core.M2.GameState;

namespace Catan.Rules.Tests;

public sealed class M2RulesTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void NormalSetupHasFiniteOfficialSupplySnakeOrderAndSecondVillageIncome(int count)
    {
        var game = New(count);
        var initial = game.GetPlayerView("P1");
        Assert.Equal(19, initial.Board.Tiles.Length);
        Assert.Equal(54, initial.Board.Vertices.Length);
        Assert.Equal(72, initial.Board.Edges.Length);
        Assert.Equal(9, initial.Board.Ports.Length);
        Assert.Equal(4, initial.Board.Ports.Count(p => p.Resource == null));
        Assert.Equal(Resources.Order(), initial.Board.Ports.Where(p => p.Resource.HasValue).Select(p => p.Resource!.Value).Order());
        Assert.Equal(25, initial.DevelopmentDeckCount);
        var ids = initial.Players.Select(p => p.Id).ToArray();
        var start = Array.IndexOf(ids, initial.ActivePlayerId);
        var order = Enumerable.Range(0, count).Select(n => ids[(n + start) % count]).ToArray();
        foreach (var (player, index) in order.Concat(order.Reverse()).Select((p, i) => (p, i)))
        {
            var view = game.GetPlayerView(player);
            Assert.Equal(player, view.ActivePlayerId);
            Assert.Equal(GamePhase.SetupSettlement, view.Phase);
            var vertex = view.LegalVertexIds.First();
            var before = view.OwnResources.Copy();
            Accepted(game, Cmd(CommandKind.SetupSettlement, player, vertex));
            var roadView = game.GetPlayerView(player);
            var road = roadView.LegalEdgeIds.First();
            Assert.Contains(vertex, roadView.Board.Edges.Single(e => e.Id == road).Vertices);
            Accepted(game, Cmd(CommandKind.SetupRoad, player, road));
            var after = game.GetPlayerView(player).OwnResources;
            foreach (var resource in Resources)
            {
                var gain = index < count ? 0 : view.Board.Tiles.Count(t => t.Vertices.Contains(vertex)
                    && string.Equals(t.Resource, resource.ToString(), StringComparison.OrdinalIgnoreCase));
                Assert.Equal(before[resource] + gain, after[resource]);
            }
        }
        var final = game.GetPlayerView("P1");
        Assert.Equal(order[0], final.ActivePlayerId);
        Assert.Equal(GamePhase.ProductionAwaitRoll, final.Phase);
        Assert.Equal(count * 2, final.Board.Settlements.Length);
        Assert.Equal(count * 2, final.Board.Roads.Length);
        Assert.All(final.Players, p => { Assert.Equal(2, p.VictoryPoints); Assert.Equal(3, p.Pieces.Settlements); Assert.Equal(13, p.Pieces.Roads); });
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public void UnsupportedSeatCountIsRejected(int players) => Assert.ThrowsAny<ArgumentException>(() => New(players));

    [Fact]
    public void VariableSetupPreservesTerrainAndNumberSupplyAndIsSeedDeterministic()
    {
        var a = New(seed: 123).GetPlayerView("P1");
        var b = New(seed: 123).GetPlayerView("P1");
        var c = New(seed: 456).GetPlayerView("P1");
        Assert.Equal(RulesHarness.Canonical(a.Board), RulesHarness.Canonical(b.Board));
        Assert.NotEqual(RulesHarness.Canonical(a.Board.Tiles), RulesHarness.Canonical(c.Board.Tiles));
        foreach (var (resource, count) in new[] { ("wood", 4), ("brick", 3), ("wool", 4), ("wheat", 4), ("ore", 3) })
            Assert.Equal(count, a.Board.Tiles.Count(t => t.Resource == resource));
        var desert = Assert.Single(a.Board.Tiles, t => t.Number == null);
        Assert.Equal(desert.Id, a.Board.RobberTileId);
        Assert.Equal(new[] { 2, 3, 3, 4, 4, 5, 5, 6, 6, 8, 8, 9, 9, 10, 10, 11, 11, 12 }, a.Board.Tiles.Where(t => t.Number.HasValue).Select(t => t.Number!.Value).Order());
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(42u)]
    [InlineData(1234u)]
    [InlineData(uint.MaxValue)]
    public void VariablePortsShuffleSixPhysicalFramesPreservingTheirPrintedPortPositions(uint seed)
    {
        // Independently transcribed from official 2025 rulebook p.3 component image.
        // Walk male -> female with the land on the left (project Y points down).
        // A sea frame spans five edges: single ports at edge 2; double ports at 1 and 4.
        var printedFrames = new[]
        {
            "-,-,generic,-,-", "-,-,wood,-,-", "-,wool,-,-,generic",
            "-,brick,-,-,generic", "-,-,ore,-,-", "-,wheat,-,-,generic"
        };
        // Frozen board graph, starting at V01 and continuing via V04. This is test data,
        // not a call to the production frame builder or a copy of its shuffle result.
        var coast = new[]
        {
            "E01", "E07", "E11", "E19", "E24", "E34", "E40", "E50", "E55", "E63",
            "E67", "E68", "E69", "E70", "E71", "E72", "E66", "E62", "E54", "E49",
            "E39", "E33", "E23", "E18", "E10", "E06", "E05", "E04", "E03", "E02"
        };
        var board = New(seed: seed).GetPlayerView("P1").Board;
        var tokens = coast.ToDictionary(e => e, _ => "-");
        foreach (var port in board.Ports)
        {
            var edge = Assert.Single(board.Edges, e => e.Vertices.Order().SequenceEqual(port.Vertices.Order()));
            Assert.True(tokens.ContainsKey(edge.Id), "Every port must occupy a coastal edge.");
            Assert.Equal("-", tokens[edge.Id]);
            tokens[edge.Id] = port.Resource?.ToString().ToLowerInvariant() ?? "generic";
        }
        var actualFrames = Enumerable.Range(0, 6).Select(frame => string.Join(",", coast.Skip(frame * 5).Take(5).Select(e => tokens[e]))).ToArray();
        Assert.Equal(printedFrames.Order(), actualFrames.Order());
        Assert.Equal(9, board.Ports.Length);
        Assert.Equal(18, board.Ports.SelectMany(p => p.Vertices).Distinct().Count());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProductionPaysVillageOneAndCityTwoExceptRobber(bool blocked)
    {
        var reference = New().GetPlayerView("P1").Board;
        var tile = reference.Tiles.Single(t => t.Number == 2);
        var resource = Enum.Parse<Resource>(tile.Resource, true);
        var game = Fixture(s =>
        {
            s.Settlements = new[] { Piece(tile.Vertices[0]) };
            s.Cities = new[] { Piece(tile.Vertices[2], "P2") };
            if (blocked) s.RobberTileId = tile.Id;
        });
        var state = game.GetAuthoritativeStateForTesting();
        Invoke(game, "Produce", state, 2);
        Assert.Equal(blocked ? 0 : 1, Player(state).Resources[resource]);
        Assert.Equal(blocked ? 0 : 2, Player(state, "P2").Resources[resource]);
        SetState(game, state);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShortageWithMultipleRecipientsPaysNoneAndSingleRecipientGetsRemainder(bool multiple)
    {
        var tile = New().GetPlayerView("P1").Board.Tiles.Single(t => t.Number == 2);
        var resource = Enum.Parse<Resource>(tile.Resource, true);
        var game = Fixture(s =>
        {
            s.Cities = new[] { Piece(tile.Vertices[0]) };
            if (multiple) s.Settlements = new[] { Piece(tile.Vertices[2], "P2") };
            Player(s, "P3").Resources[resource] = 18;
        });
        var state = game.GetAuthoritativeStateForTesting();
        Invoke(game, "Produce", state, 2);
        Assert.Equal(multiple ? 0 : 1, Player(state).Resources[resource]);
        Assert.Equal(0, Player(state, "P2").Resources[resource]);
        Assert.Equal(multiple ? 1 : 0, state.Bank[resource]);
        SetState(game, state);
    }

    [Fact]
    public void SevenDiscardsFloorHalfOnlyAboveSevenBeforeMovingAndNeverCountsDevelopmentCards()
    {
        var game = Fixture(s =>
        {
            s.Phase = GamePhase.ProductionAwaitRoll; s.Random.State = 256;
            Player(s).Resources = Bag(wood: 7); Card(s, DevelopmentCardKind.Knight);
            Player(s, "P2").Resources = Bag(brick: 9);
            Player(s, "P3").Resources = Bag(wool: 8);
        });
        Accepted(game, Cmd(CommandKind.RollDice));
        var view = game.GetPlayerView("P1");
        Assert.Equal(7, view.LastDice1 + view.LastDice2);
        Assert.Equal(GamePhase.Discard, view.Phase);
        Assert.Equal(new[] { "P2", "P3" }, view.Discards.Select(d => d.PlayerId).Order());
        Assert.All(view.Discards, d => Assert.Equal(4, d.Amount));
        Rejected(game, Cmd(CommandKind.MoveRobber, target: "T01"));
        var wrong = Cmd(CommandKind.DiscardResources, "P2"); wrong.Resources = Bag(brick: 3); Rejected(game, wrong);
        wrong = Cmd(CommandKind.DiscardResources, "P2"); wrong.Resources = Bag(wood: 4); Rejected(game, wrong);
        var discard = Cmd(CommandKind.DiscardResources, "P3"); discard.Resources = Bag(wool: 4); Accepted(game, discard);
        Assert.Equal(GamePhase.Discard, game.GetPlayerView("P1").Phase);
        discard = Cmd(CommandKind.DiscardResources, "P2"); discard.Resources = Bag(brick: 4); Accepted(game, discard);
        Assert.Equal(GamePhase.RobberMove, game.GetPlayerView("P1").Phase);
        Assert.Equal(7, game.GetPlayerView("P1").OwnResources.Total);
        Assert.Equal(5, game.GetPlayerView("P2").OwnResources.Total);
    }

    [Fact]
    public void KnightBeforeRollMovesRobberWithoutDiscardAndStealsOnlyEligibleResource()
    {
        var game = Fixture(s =>
        {
            s.Phase = GamePhase.ProductionAwaitRoll; s.RobberTileId = "T10";
            s.Settlements = new[] { Piece("V01", "P2"), Piece("V09", "P3") };
            Player(s, "P2").Resources = Bag(ore: 9); Card(s, DevelopmentCardKind.Knight);
            Card(s, DevelopmentCardKind.VictoryPoint, "P2");
        });
        Accepted(game, Cmd(CommandKind.PlayKnight));
        Assert.Equal(GamePhase.RobberMove, game.GetPlayerView("P1").Phase);
        Assert.Empty(game.GetPlayerView("P1").Discards);
        Rejected(game, Cmd(CommandKind.MoveRobber, target: "T10"));
        Accepted(game, Cmd(CommandKind.MoveRobber, target: "T01"));
        Rejected(game, Cmd(CommandKind.StealResource, "P2", "P1"));
        var theft = Cmd(CommandKind.StealResource); theft.OtherPlayerId = "P4"; Rejected(game, theft);
        theft.OtherPlayerId = "P2"; Accepted(game, theft);
        Assert.Equal(1, game.GetPlayerView("P1").OwnResources.Ore);
        Assert.Equal(8, game.GetPlayerView("P2").OwnResources.Ore);
        Assert.Single(game.GetPlayerView("P2").OwnDevelopmentCards);
        Assert.Equal(GamePhase.ProductionAwaitRoll, game.GetPlayerView("P1").Phase);
    }

    [Fact]
    public void EmptyHandAdjacentVictimIsSelectableAndNoVictimEndsRobberMove()
    {
        var game = Fixture(s => { s.Settlements = new[] { Piece("V01", "P2") }; s.RobberTileId = "T10"; Card(s, DevelopmentCardKind.Knight); });
        Accepted(game, Cmd(CommandKind.PlayKnight));
        Accepted(game, Cmd(CommandKind.MoveRobber, target: "T01"));
        if (game.GetPlayerView("P1").Phase == GamePhase.RobberSteal)
        { var steal = Cmd(CommandKind.StealResource); steal.OtherPlayerId = "P2"; Accepted(game, steal); }
        Assert.Equal(GamePhase.Action, game.GetPlayerView("P1").Phase);
        Assert.Equal(0, game.GetPlayerView("P1").OwnResources.Total);
        game = Fixture(s => { s.RobberTileId = "T10"; Card(s, DevelopmentCardKind.Knight); });
        Accepted(game, Cmd(CommandKind.PlayKnight));
        Accepted(game, Cmd(CommandKind.MoveRobber, target: "T01"));
        Assert.Equal(GamePhase.Action, game.GetPlayerView("P1").Phase);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(3)]
    [InlineData(2)]
    public void BankAndOwnedPortsUseCorrectRatio(int ratio)
    {
        var board = New().GetPlayerView("P1").Board;
        var port = board.Ports.First(p => ratio == 2 ? p.Resource.HasValue : p.Resource == null);
        var resource = ratio == 2 ? port.Resource!.Value : Resource.Wood;
        var receive = Resources.First(r => r != resource);
        var game = Fixture(s => { Player(s).Resources[resource] = ratio; if (ratio < 4) s.Settlements = new[] { Piece(port.Vertices[0]) }; });
        var trade = Cmd(CommandKind.BankTrade); trade.GiveResource = resource; trade.ReceiveResource = receive; trade.GiveAmount = ratio; trade.ReceiveAmount = 1;
        Accepted(game, trade);
        Assert.Equal(0, game.GetPlayerView("P1").OwnResources[resource]);
        Assert.Equal(1, game.GetPlayerView("P1").OwnResources[receive]);
    }

    [Fact]
    public void PortsRequireOwnedBuildingAndCorrectResourceAndBankStock()
    {
        var board = New().GetPlayerView("P1").Board;
        var port = board.Ports.First(p => p.Resource.HasValue);
        var wrongResource = Resources.First(r => r != port.Resource);
        var receive = Resources.First(r => r != wrongResource);
        var game = Fixture(s => { Player(s).Resources[wrongResource] = 4; s.Roads = new[] { Piece(board.Edges.First(e => e.Vertices.Contains(port.Vertices[0])).Id) }; });
        var trade = Cmd(CommandKind.BankTrade); trade.GiveResource = wrongResource; trade.ReceiveResource = receive; trade.GiveAmount = 2; Rejected(game, trade);
        game = Fixture(s => { Player(s).Resources[wrongResource] = 4; s.Settlements = new[] { Piece(port.Vertices[0]) }; });
        Rejected(game, trade);
        trade.GiveAmount = 4; trade.ReceiveResource = wrongResource; Rejected(game, trade);
        game = Fixture(s => { Player(s).Resources[wrongResource] = 4; Player(s, "P2").Resources[receive] = 19; });
        trade.ReceiveResource = receive; Rejected(game, trade);
    }

    [Fact]
    public void OtherSeatMayOfferTradeToActiveSeatAndAcceptanceIsAtomic()
    {
        var game = Fixture(s => { Player(s).Resources = Bag(wood: 2); Player(s, "P2").Resources = Bag(ore: 1); });
        var offer = Cmd(CommandKind.ProposeTrade, "P2"); offer.OtherPlayerId = "P1"; offer.Give = Bag(ore: 1); offer.Receive = Bag(wood: 2);
        Accepted(game, offer);
        Assert.Equal(2, game.GetPlayerView("P1").OwnResources.Wood);
        Rejected(game, Cmd(CommandKind.AcceptTrade, "P3"));
        Accepted(game, Cmd(CommandKind.AcceptTrade));
        Assert.Equal(new[] { 0, 1 }, new[] { game.GetPlayerView("P1").OwnResources.Wood, game.GetPlayerView("P1").OwnResources.Ore });
        Assert.Equal(new[] { 2, 0 }, new[] { game.GetPlayerView("P2").OwnResources.Wood, game.GetPlayerView("P2").OwnResources.Ore });
        Assert.Null(game.GetPlayerView("P1").TradeOffer);
    }

    [Theory]
    [InlineData("bystanders")]
    [InlineData("gift")]
    [InlineData("overlap")]
    [InlineData("overdraft")]
    [InlineData("negative")]
    public void InvalidPlayerTradesDoNotMutateAnything(string reason)
    {
        var game = Fixture(s => { Player(s).Resources = Bag(wood: 3); Player(s, "P2").Resources = Bag(ore: 2); });
        var offer = Cmd(CommandKind.ProposeTrade); offer.OtherPlayerId = "P2"; offer.Give = Bag(wood: 1); offer.Receive = Bag(ore: 1);
        switch (reason)
        {
            case "bystanders": offer.PlayerId = "P3"; break;
            case "gift": offer.Receive = Bag(); break;
            case "overlap": offer.Receive.Wood = 1; break;
            case "overdraft": offer.Give.Wood = 4; break;
            case "negative": offer.Give.Wood = -1; break;
        }
        Rejected(game, offer);
    }

    [Fact]
    public void BuildingCostsDistanceConnectionCityReplacementAndSupplyAreExact()
    {
        var game = Fixture(s => { s.Settlements = new[] { Piece("V01") }; Player(s).Resources = Bag(3, 3, 1, 3, 3); });
        Rejected(game, Cmd(CommandKind.BuildRoad, target: "E72"));
        Accepted(game, Cmd(CommandKind.BuildRoad, target: "E02"));
        Rejected(game, Cmd(CommandKind.BuildSettlement, target: "V05"));
        Accepted(game, Cmd(CommandKind.BuildRoad, target: "E03"));
        Accepted(game, Cmd(CommandKind.BuildSettlement, target: "V02"));
        Assert.Equal(Bag(wheat: 2, ore: 3).Total, game.GetPlayerView("P1").OwnResources.Total);
        Accepted(game, Cmd(CommandKind.BuildCity, target: "V02"));
        Assert.Equal(0, game.GetPlayerView("P1").OwnResources.Total);
        var p = game.GetPlayerView("P1").Players.Single(p => p.Id == "P1");
        Assert.Equal(4, p.Pieces.Settlements); Assert.Equal(3, p.Pieces.Cities); Assert.Equal(3, p.VictoryPoints);
        Rejected(game, Cmd(CommandKind.BuildCity, target: "V02"));
    }

    [Fact]
    public void OpponentBuildingBlocksRoadContinuationAndCannotBeUpgraded()
    {
        var game = Fixture(s => { s.Roads = new[] { Piece("E02") }; s.Settlements = new[] { Piece("V05", "P2") }; Player(s).Resources = Bag(2, 2, 0, 2, 3); });
        Rejected(game, Cmd(CommandKind.BuildRoad, target: "E03"));
        Rejected(game, Cmd(CommandKind.BuildCity, target: "V05"));
        Accepted(game, Cmd(CommandKind.BuildRoad, target: "E01"));
    }

    [Theory]
    [InlineData(CommandKind.PlayKnight, DevelopmentCardKind.Knight)]
    [InlineData(CommandKind.PlayRoadBuilding, DevelopmentCardKind.RoadBuilding)]
    [InlineData(CommandKind.PlayYearOfPlenty, DevelopmentCardKind.YearOfPlenty)]
    [InlineData(CommandKind.PlayMonopoly, DevelopmentCardKind.Monopoly)]
    public void NewDevelopmentCardsAndSecondCardInSameTurnAreRejected(CommandKind kind, DevelopmentCardKind card)
    {
        var command = Cmd(kind); command.Resources = Bag(wood: 2); command.Resource = Resource.Wood;
        var game = Fixture(s => { s.Settlements = new[] { Piece("V01") }; Card(s, card, boughtTurn: 9); });
        Rejected(game, command);
        game = Fixture(s => { s.Settlements = new[] { Piece("V01") }; Card(s, card); s.DevelopmentCardPlayedThisTurn = true; });
        Rejected(game, command);
        game = Fixture(s => { s.Phase = GamePhase.RobberMove; s.PendingDecision = new() { Kind = "RobberMove", PlayerId = "P1", ReturnPhase = GamePhase.Action }; Card(s, card); });
        Rejected(game, command);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InventionTakesTwoResourcesAndInsufficientSelectionIsAtomic(bool same)
    {
        var game = Fixture(s => { Card(s, DevelopmentCardKind.YearOfPlenty); Player(s, "P2").Resources.Wood = 18; });
        var command = Cmd(CommandKind.PlayYearOfPlenty); command.Resources = Bag(wood: 2); Rejected(game, command);
        command.Resources = same ? Bag(ore: 2) : Bag(wood: 1, ore: 1); Accepted(game, command);
        Assert.Equal(2, game.GetPlayerView("P1").OwnResources.Total);
        Assert.Equal(same ? 2 : 1, game.GetPlayerView("P1").OwnResources.Ore);
        Assert.Empty(game.GetPlayerView("P1").OwnDevelopmentCards);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void MonopolyCollectsOnlyNamedResourceIncludingZeroCase(int amount)
    {
        var game = Fixture(s => { Card(s, DevelopmentCardKind.Monopoly); Player(s).Resources = Bag(wood: 1); Player(s, "P2").Resources = Bag(wood: amount, ore: 2); Player(s, "P3").Resources.Wood = amount; });
        var command = Cmd(CommandKind.PlayMonopoly); command.Resource = Resource.Wood; Accepted(game, command);
        Assert.Equal(1 + amount * 2, game.GetPlayerView("P1").OwnResources.Wood);
        Assert.Equal(0, game.GetPlayerView("P2").OwnResources.Wood);
        Assert.Equal(2, game.GetPlayerView("P2").OwnResources.Ore);
    }

    [Fact]
    public void RoadBuildingPlacesTwoFreeConnectedRoadsAndBlocksOtherCommandsUntilFinished()
    {
        var game = Fixture(s => { s.Settlements = new[] { Piece("V01") }; Card(s, DevelopmentCardKind.RoadBuilding); s.Phase = GamePhase.ProductionAwaitRoll; });
        Accepted(game, Cmd(CommandKind.PlayRoadBuilding));
        Assert.Equal(GamePhase.RoadBuilding, game.GetPlayerView("P1").Phase);
        Rejected(game, Cmd(CommandKind.EndTurn)); Rejected(game, Cmd(CommandKind.PlaceFreeRoad, target: "E72"));
        Accepted(game, Cmd(CommandKind.PlaceFreeRoad, target: "E02"));
        Accepted(game, Cmd(CommandKind.PlaceFreeRoad, target: "E03"));
        Assert.Equal(GamePhase.ProductionAwaitRoll, game.GetPlayerView("P1").Phase);
        Assert.Equal(0, game.GetPlayerView("P1").OwnResources.Total);
        Assert.Equal(13, game.GetPlayerView("P1").Players.Single(p => p.Id == "P1").Pieces.Roads);
    }

    [Theory]
    [InlineData(14)]
    [InlineData(15)]
    public void RoadBuildingRespectsRemainingRoadPieceSupply(int used)
    {
        var ids = New().GetPlayerView("P1").Board.Edges.Take(used).Select(e => e.Id).ToArray();
        var game = Fixture(s => { s.Roads = ids.Select(id => Piece(id)).ToArray(); Card(s, DevelopmentCardKind.RoadBuilding); });
        if (used == 15) { Rejected(game, Cmd(CommandKind.PlayRoadBuilding)); return; }
        Accepted(game, Cmd(CommandKind.PlayRoadBuilding));
        var view = game.GetPlayerView("P1");
        Accepted(game, Cmd(CommandKind.PlaceFreeRoad, target: view.LegalEdgeIds.First()));
        Assert.Equal(GamePhase.Action, game.GetPlayerView("P1").Phase);
        Assert.Equal(0, game.GetPlayerView("P1").Players.Single(p => p.Id == "P1").Pieces.Roads);
    }

    [Theory]
    [InlineData("E02,E03,E08", 2)]
    [InlineData("E01,E02,E07,E08,E12,E13", 6)]
    [InlineData("E01,E02,E03,E04,E05", 5)]
    [InlineData("E01,E02,E07,E08,E12,E13,E03,E04", 8)]
    public void LongestRoadCountsTrailWithoutRepeatingEdgesOrSummingBranches(string edges, int expected)
    {
        var game = Fixture(s => s.Roads = edges.Split(',').Select(e => Piece(e)).ToArray());
        Assert.Equal(expected, game.GetPlayerView("P1").Players.Single(p => p.Id == "P1").LongestRoadLength);
        Assert.Equal(expected >= 5 ? "P1" : null, game.GetPlayerView("P1").LongestRoadPlayerId);
    }

    [Fact]
    public void OpponentSettlementCutsLongestRoadAndTiedHolderRetainsAward()
    {
        var line = new[] { "E01", "E02", "E03", "E04", "E05" };
        var other = new[] { "E67", "E68", "E69", "E70", "E71" };
        var game = Fixture(s => { s.Roads = line.Select(e => Piece(e)).Concat(other.Select(e => Piece(e, "P2"))).ToArray(); s.LongestRoadPlayerId = "P1"; });
        Assert.Equal("P1", game.GetPlayerView("P1").LongestRoadPlayerId);
        var state = game.GetAuthoritativeStateForTesting(); state.Settlements = new[] { Piece("V05", "P3") }; Balance(state); SetState(game, state);
        Assert.Equal(3, game.GetPlayerView("P1").Players.Single(p => p.Id == "P1").LongestRoadLength);
        Assert.Equal("P2", game.GetPlayerView("P1").LongestRoadPlayerId);
        game = Fixture(s => s.Roads = line.Select(e => Piece(e)).Concat(other.Select(e => Piece(e, "P2"))).ToArray());
        Assert.Null(game.GetPlayerView("P1").LongestRoadPlayerId);
    }

    [Fact]
    public void LargestArmyRequiresThreePreservesTieAndTransfersOnlyOnStrictLead()
    {
        var game = Fixture(s => { Player(s).PlayedKnights = 3; Player(s, "P2").PlayedKnights = 3; s.PlayedDevelopmentCards = Enumerable.Repeat(DevelopmentCardKind.Knight, 6).ToArray(); s.LargestArmyPlayerId = "P1"; });
        Assert.Equal("P1", game.GetPlayerView("P1").LargestArmyPlayerId);
        var state = game.GetAuthoritativeStateForTesting(); Player(state, "P2").PlayedKnights = 4; state.PlayedDevelopmentCards = Enumerable.Repeat(DevelopmentCardKind.Knight, 7).ToArray(); Balance(state); SetState(game, state);
        Assert.Equal("P2", game.GetPlayerView("P1").LargestArmyPlayerId);
        game = Fixture(s => { Player(s).PlayedKnights = 2; s.PlayedDevelopmentCards = Enumerable.Repeat(DevelopmentCardKind.Knight, 2).ToArray(); });
        Assert.Null(game.GetPlayerView("P1").LargestArmyPlayerId);
    }

    [Fact]
    public void BreakingAwardHoldersRoadReturnsTileToSupplyWhenOtherLeadersTie()
    {
        var game = Fixture(s =>
        {
            s.ActivePlayerId = "P4";
            s.Roads = new[] { "E01", "E02", "E03", "E04", "E05", "E06" }.Select(e => Piece(e))
                .Concat(new[] { "E67", "E68", "E69", "E70", "E71" }.Select(e => Piece(e, "P2")))
                .Concat(new[] { "E18", "E23", "E32", "E31", "E30" }.Select(e => Piece(e, "P3")))
                .Append(Piece("E08", "P4")).ToArray();
            Player(s, "P4").Resources = Bag(1, 1, 1, 1);
        });
        Assert.Equal("P1", game.GetPlayerView("P1").LongestRoadPlayerId);
        Accepted(game, Cmd(CommandKind.BuildSettlement, "P4", "V05"));
        var view = game.GetPlayerView("P1");
        Assert.Equal(4, view.Players.Single(p => p.Id == "P1").LongestRoadLength);
        Assert.Equal(5, view.Players.Single(p => p.Id == "P2").LongestRoadLength);
        Assert.Equal(5, view.Players.Single(p => p.Id == "P3").LongestRoadLength);
        Assert.Null(view.LongestRoadPlayerId);
        Assert.Equal(0, view.Players.Single(p => p.Id == "P1").VictoryPoints);
    }

    [Fact]
    public void HiddenVictoryPointsCanWinImmediatelyButOnlyOnTheirOwnersTurn()
    {
        void Winning(GameState s)
        {
            s.Cities = new[] { "V01", "V03", "V17", "V21" }.Select(v => Piece(v)).ToArray(); s.Settlements = new[] { Piece("V54") };
            Card(s, DevelopmentCardKind.VictoryPoint, boughtTurn: 9);
        }
        var game = Fixture(Winning);
        Assert.Equal(10, game.GetPlayerView("P1").OwnVictoryPoints);
        Assert.Equal(9, game.GetPlayerView("P2").Players.Single(p => p.Id == "P1").VictoryPoints);
        Accepted(game, Cmd(CommandKind.DeclareVictory));
        Assert.Equal("P1", game.GetPlayerView("P2").WinnerPlayerId);
        Assert.Equal(GamePhase.Finished, game.GetPlayerView("P2").Phase);
        Rejected(game, Cmd(CommandKind.EndTurn));
        game = Fixture(s => { Winning(s); s.ActivePlayerId = "P4"; });
        Rejected(game, Cmd(CommandKind.DeclareVictory));
        Assert.Null(game.GetPlayerView("P1").WinnerPlayerId);
        Accepted(game, Cmd(CommandKind.EndTurn, "P4"));
        Assert.Equal("P1", game.GetPlayerView("P1").WinnerPlayerId);
    }

    [Fact]
    public void NewlyBoughtVictoryPointWinsImmediatelyAndDevelopmentDeckCannotOverdraw()
    {
        var game = Fixture(s =>
        {
            s.Cities = new[] { "V01", "V03", "V17", "V21" }.Select(v => Piece(v)).ToArray(); s.Settlements = new[] { Piece("V54") };
            Player(s).Resources = Bag(wool: 1, wheat: 1, ore: 1);
        });
        var state = game.GetAuthoritativeStateForTesting();
        state.DevelopmentDeck = state.DevelopmentDeck.OrderBy(c => c == DevelopmentCardKind.VictoryPoint ? 0 : 1).ToArray(); SetState(game, state);
        Accepted(game, Cmd(CommandKind.BuyDevelopmentCard));
        Assert.Equal("P1", game.GetPlayerView("P1").WinnerPlayerId);
        game = Fixture(s => { Player(s).Resources = Bag(wool: 1, wheat: 1, ore: 1); Player(s, "P2").DevelopmentCards = Deck().Select(k => new DevelopmentCard { Kind = k, BoughtTurn = 1 }).ToArray(); });
        Rejected(game, Cmd(CommandKind.BuyDevelopmentCard));
    }

    [Theory]
    [InlineData("roads")]
    [InlineData("settlements")]
    [InlineData("cities")]
    public void FinitePieceSupplyRejectsConstructionEvenWithEnoughResources(string supply)
    {
        var game = Fixture(s =>
        {
            Player(s).Resources = Bag(2, 2, 2, 3, 3);
            if (supply == "roads") s.Roads = Enumerable.Range(1, 15).Select(n => Piece($"E{n:00}")).ToArray();
            if (supply == "settlements")
            { s.Settlements = new[] { "V01", "V03", "V17", "V21", "V54" }.Select(v => Piece(v)).ToArray(); s.Roads = new[] { Piece("E36") }; }
            if (supply == "cities")
            { s.Cities = new[] { "V01", "V03", "V17", "V21" }.Select(v => Piece(v)).ToArray(); s.Settlements = new[] { Piece("V54") }; }
        });
        Rejected(game, Cmd(supply == "roads" ? CommandKind.BuildRoad : supply == "settlements" ? CommandKind.BuildSettlement : CommandKind.BuildCity,
            target: supply == "roads" ? "E16" : supply == "settlements" ? "V30" : "V54"));
    }

    [Fact]
    public void FifthRoadAndThirdKnightAwardExactlyTwoPointsThroughNormalCommands()
    {
        var game = Fixture(s => { s.Roads = new[] { "E01", "E02", "E03", "E04" }.Select(e => Piece(e)).ToArray(); Player(s).Resources = Bag(wood: 1, brick: 1); });
        Assert.Null(game.GetPlayerView("P1").LongestRoadPlayerId);
        Accepted(game, Cmd(CommandKind.BuildRoad, target: "E05"));
        Assert.Equal("P1", game.GetPlayerView("P1").LongestRoadPlayerId);
        Assert.Equal(2, game.GetPlayerView("P1").OwnVictoryPoints);
        game = Fixture(s => { Player(s).PlayedKnights = 2; s.PlayedDevelopmentCards = Enumerable.Repeat(DevelopmentCardKind.Knight, 2).ToArray(); Card(s, DevelopmentCardKind.Knight); });
        Assert.Null(game.GetPlayerView("P1").LargestArmyPlayerId);
        Accepted(game, Cmd(CommandKind.PlayKnight));
        Assert.Equal("P1", game.GetPlayerView("P1").LargestArmyPlayerId);
        Assert.Equal(2, game.GetPlayerView("P1").OwnVictoryPoints);
    }

    [Fact]
    public void RejectedTradeAndCancelledTradeReturnToActionWithoutTransferringCards()
    {
        var game = Fixture(s => Player(s).Resources = Bag(wood: 1));
        var offer = Cmd(CommandKind.ProposeTrade); offer.OtherPlayerId = "P2"; offer.Give = Bag(wood: 1); offer.Receive = Bag(ore: 1);
        Accepted(game, offer);
        Rejected(game, Cmd(CommandKind.AcceptTrade, "P2"));
        Rejected(game, Cmd(CommandKind.CancelTrade, "P2"));
        Accepted(game, Cmd(CommandKind.RejectTrade, "P2"));
        Assert.Null(game.GetPlayerView("P1").TradeOffer);
        Assert.Equal(1, game.GetPlayerView("P1").OwnResources.Wood);
        offer.Id = Cmd(CommandKind.ProposeTrade).Id; Accepted(game, offer);
        Accepted(game, Cmd(CommandKind.CancelTrade));
        Assert.Null(game.GetPlayerView("P1").TradeOffer);
        Assert.Equal(1, game.GetPlayerView("P1").OwnResources.Wood);
    }

    [Fact]
    public void DiscardAndTheftReceiptsDoNotPublishResourceTypesOrClientSuppliedSecrets()
    {
        var game = Fixture(s => { s.Phase = GamePhase.ProductionAwaitRoll; s.Random.State = 256; Player(s, "P2").Resources = Bag(ore: 9); s.Settlements = new[] { Piece("V01", "P2") }; s.RobberTileId = "T10"; });
        Accepted(game, Cmd(CommandKind.RollDice));
        var discard = Cmd(CommandKind.DiscardResources, "P2", "PRIVATE-ORE-HAND"); discard.Resources = Bag(ore: 4); discard.OtherPlayerId = "PRIVATE-ORE-HAND";
        var receipt = Accepted(game, discard);
        Assert.DoesNotContain("ore", RulesHarness.Canonical(receipt.Events), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PRIVATE", RulesHarness.Canonical(receipt.Events));
        Accepted(game, Cmd(CommandKind.MoveRobber, target: "T01"));
        var theft = Cmd(CommandKind.StealResource, target: "PRIVATE-ORE-HAND"); theft.OtherPlayerId = "P2";
        receipt = Accepted(game, theft);
        Assert.DoesNotContain("ore", RulesHarness.Canonical(receipt.Events), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PRIVATE", RulesHarness.Canonical(receipt.Events));
        Assert.Equal(1, game.GetPlayerView("P1").OwnResources.Ore);
    }
}
