using Catan.Core;
using Catan.Core.M4;
using Xunit;
using static Catan.Rules.Tests.M4ProgressHarness;
using CommandKind = Catan.Core.M4.CommandKind;
using GamePhase = Catan.Core.M4.GamePhase;

namespace Catan.Rules.Tests;

public sealed class M4ProgressCardTests
{
    [Fact] public void OfficialDeckHasTwentyFiveEffectsAndEighteenCardsPerTrack()
    {
        Assert.Equal(25, Enum.GetValues<ProgressCardKind>().Length);
        foreach (var track in Enum.GetValues<ImprovementTrack>()) Assert.Equal(18, ProgressCatalog.Deck(track).Length);
        Assert.Equal(6, ProgressCatalog.Deck(ImprovementTrack.Trade).Count(c => c == ProgressCardKind.Merchant));
        Assert.Equal(3, ProgressCatalog.Deck(ImprovementTrack.Politics).Count(c => c == ProgressCardKind.Espionage));
    }
    [Fact] public void AlchemySetsBothProductionDiceButStillDrawsOneRandomEventDie()
    {
        var g = Fixture(ProgressCardKind.Alchemy, s => s.Phase = GamePhase.ProductionAwaitRoll);
        var c = Play(ProgressCardKind.Alchemy); c.ChosenDice1 = 2; c.ChosenDice2 = 3; Accepted(g, c);
        var draws = g.GetAuthoritativeStateForTesting().Random.Draws;
        Accepted(g, Cmd(CommandKind.RollDice)); var after = g.GetAuthoritativeStateForTesting();
        Assert.Equal(2, after.LastDice1); Assert.Equal(3, after.LastDice2); Assert.Equal(draws + 1, after.Random.Draws);
        Assert.Equal(0, after.AlchemyDice1);
    }
    [Fact] public void AlchemyRejectsActionPhaseAndInvalidDiceWithoutConsumingCard()
    {
        var g = Fixture(ProgressCardKind.Alchemy); var c = Play(ProgressCardKind.Alchemy); c.ChosenDice1 = 1; c.ChosenDice2 = 6; Rejected(g, c);
        g = Fixture(ProgressCardKind.Alchemy, s => s.Phase = GamePhase.ProductionAwaitRoll); c.ChosenDice1 = 0; Rejected(g, c);
    }
    [Fact] public void CraneBuildsFirstScienceLevelForZeroPaper()
    {
        var g = Fixture(ProgressCardKind.Crane, s => s.Cities = [Piece(FirstVertex)]);
        var c = Play(ProgressCardKind.Crane); c.Track = ImprovementTrack.Science; Accepted(g, c);
        Assert.Equal(1, P(g.GetAuthoritativeStateForTesting()).Improvements[2]); Assert.Equal(12, g.GetPlayerView("P1").CommodityBank.Paper);
    }
    [Fact] public void EngineeringBuildsOneFreeWallAndRejectsSecondWallOnSameCity()
    {
        var g = Fixture(ProgressCardKind.Engineering, s => s.Cities = [Piece(FirstVertex)]); Accepted(g, Play(ProgressCardKind.Engineering, FirstVertex));
        Assert.Single(g.GetPlayerView("P1").Board.Walls); Assert.Equal(19, g.GetPlayerView("P1").Bank.Brick);
        g = Fixture(ProgressCardKind.Engineering, s => { s.Cities = [Piece(FirstVertex)]; s.Walls = [Piece(FirstVertex)]; }); Rejected(g, Play(ProgressCardKind.Engineering, FirstVertex));
    }
    [Fact] public void InventionSwapsAllowedNumbersWithoutMovingRobber()
    {
        var g = Fixture(ProgressCardKind.Invention, s => { s.FirstBarbarianAttack = true; s.RobberTileId = s.Tiles.First(t => t.Number == 3).Id; });
        var before = g.GetAuthoritativeStateForTesting(); var a = before.Tiles.First(t => t.Number == 3); var b = before.Tiles.First(t => t.Number == 5);
        var c = Play(ProgressCardKind.Invention, b.Id); c.SourceId = a.Id; Accepted(g, c);
        var s = g.GetAuthoritativeStateForTesting(); Assert.Equal(5, s.Tiles.Single(t => t.Id == a.Id).Number); Assert.Equal(3, s.Tiles.Single(t => t.Id == b.Id).Number); Assert.Equal(a.Id, s.RobberTileId);
    }
    [Theory] [InlineData(2)] [InlineData(6)] [InlineData(8)] [InlineData(12)]
    public void InventionCannotSwapProtectedNumbers(int number)
    {
        var g = Fixture(ProgressCardKind.Invention); var tiles = g.GetPlayerView("P1").Board.Tiles;
        var c = Play(ProgressCardKind.Invention, tiles.First(t => t.Number == number).Id); c.SourceId = tiles.First(t => t.Number == 3).Id; Rejected(g, c);
    }
    [Theory] [InlineData(ProgressCardKind.Irrigation, Resource.Wheat)] [InlineData(ProgressCardKind.Mining, Resource.Ore)]
    public void HarvestGrantsTwoPerDistinctHexRegardlessOfMultipleBuildingsOrRobber(ProgressCardKind card, Resource resource)
    {
        var g = Fixture(card, s => { var t = s.Tiles.First(x => x.Resource == resource.ToString().ToLowerInvariant()); s.Cities = [Piece(t.Vertices[0]), Piece(t.Vertices[2])]; s.RobberTileId = t.Id; s.FirstBarbarianAttack = true; });
        var before = g.GetAuthoritativeStateForTesting();
        var matching = before.Tiles.Count(t => t.Resource == resource.ToString().ToLowerInvariant() && t.Vertices.Any(v => before.Cities.Any(b => b.LocationId == v)));
        Accepted(g, Play(card)); Assert.Equal(2 * matching, g.GetPlayerView("P1").OwnResources[resource]);
    }
    [Fact] public void IrrigationTakesRemainingSupplyWhenOnlyOneWheatRemains()
    {
        var g = Fixture(ProgressCardKind.Irrigation, s => { s.Cities = [Piece(s.Tiles.First(t => t.Resource == "wheat").Vertices[0])]; P(s, "P2").Resources.Wheat = 18; });
        Accepted(g, Play(ProgressCardKind.Irrigation)); Assert.Equal(1, g.GetPlayerView("P1").OwnResources.Wheat); Assert.Equal(0, g.GetPlayerView("P1").Bank.Wheat);
    }
    [Fact] public void MedicineUpgradesSettlementForExactlyOneWheatTwoOre()
    {
        var g = Fixture(ProgressCardKind.Medicine, s => { s.Settlements = [Piece(FirstVertex)]; P(s).Resources.Wheat = 1; P(s).Resources.Ore = 2; });
        Accepted(g, Play(ProgressCardKind.Medicine, FirstVertex)); var view = g.GetPlayerView("P1"); Assert.Empty(view.Board.Settlements); Assert.Single(view.Board.Cities); Assert.Equal(0, view.OwnResources.Total);
    }
    [Theory] [InlineData(ProgressCardKind.Printing, ImprovementTrack.Science)] [InlineData(ProgressCardKind.Constitution, ImprovementTrack.Politics)]
    public void VictoryPointCardsImmediatelyBecomePublicOutsideOwnersTurn(ProgressCardKind card, ImprovementTrack track)
    {
        var g = Fixture(ProgressCardKind.Merchant); var s = g.GetAuthoritativeStateForTesting(); var deck = s.ProgressDecks.Single(d => d.Track == track);
        deck.Cards = new[] { card }.Concat(deck.Cards.Where(c => c != card)).ToArray();
        Invoke(g, "DrawProgressCard", s, P(s, "P2"), track);
        Assert.Equal(new[] { card }, P(s, "P2").ProgressVictoryCards); Assert.Empty(P(s, "P2").ProgressCards);
        Set(g, s); Assert.Contains(card, g.GetPlayerView("P1").Players.Single(p => p.Id == "P2").ProgressVictoryCards);
    }
    [Fact] public void RoadBuildingPlacesTwoFreeRoadsSequentiallyAndPreservesPieceSupply()
    {
        var g = Fixture(ProgressCardKind.RoadBuilding, s => s.Settlements = [Piece(FirstVertex)]); Accepted(g, Play(ProgressCardKind.RoadBuilding));
        for (var i = 0; i < 2; i++)
        {
            var edge = g.GetPlayerView("P1").Board.Edges.First(e => g.Preview(Cmd(CommandKind.ResolveProgressChoice, target: e.Id)).Success);
            Accepted(g, Cmd(CommandKind.ResolveProgressChoice, target: edge.Id));
        }
        var view = g.GetPlayerView("P1"); Assert.Equal(2, view.Board.Roads.Length); Assert.Equal(13, view.Players.Single(p => p.Id == "P1").Pieces.Roads); Assert.Equal(GamePhase.Action, view.Phase); Assert.Equal(0, view.OwnResources.Total);
    }
    [Fact] public void SmithingPromotesTwoKnightsForFreeAndCannotPromoteSameKnightTwice()
    {
        var board = New().GetPlayerView("P1").Board; var edges = board.Edges.Take(2).ToArray();
        var g = Fixture(ProgressCardKind.Smithing, s => { s.Roads = edges.Select(e => Piece(e.Id)).ToArray(); s.Knights = edges.Select(e => e.Vertices[0]).Distinct().Take(2).Select(v => Knight(v)).ToArray(); });
        var c = Play(ProgressCardKind.Smithing); c.TargetIds = g.GetPlayerView("P1").Board.Knights.Select(k => k.LocationId).ToArray(); Accepted(g, c);
        Assert.All(g.GetPlayerView("P1").Board.Knights, k => Assert.Equal(2, k.Level));
        var state = g.GetAuthoritativeStateForTesting(); P(state).ProgressCards = [ProgressCardKind.Smithing]; Set(g, state); c.Id = Cmd(CommandKind.PlayProgressCard).Id; Rejected(g, c);
    }
    [Fact] public void CommercialHarborCanBeUsedLaterAndReceiverChoosesCommodity()
    {
        var g = Fixture(ProgressCardKind.CommercialHarbor, s => { P(s).Resources.Wood = 2; P(s, "P2").Commodities.Coin = 1; P(s, "P2").Commodities.Cloth = 1; });
        Accepted(g, Play(ProgressCardKind.CommercialHarbor)); Assert.Equal(GamePhase.Action, g.GetPlayerView("P1").Phase);
        var c = Cmd(CommandKind.CommercialHarborOffer); c.OtherPlayerId = "P2"; c.Resource = Resource.Wood; Accepted(g, c);
        Assert.Equal("P2", g.GetPlayerView("P1").PendingDecision.PlayerId);
        var choice = Cmd(CommandKind.ResolveProgressChoice, "P2"); choice.Commodity = Commodity.Cloth; Accepted(g, choice);
        Assert.Equal(1, g.GetPlayerView("P1").OwnCommodities.Cloth); Assert.Equal(1, g.GetPlayerView("P2").OwnResources.Wood);
        c.Id = Cmd(CommandKind.CommercialHarborOffer).Id; Rejected(g, c); c.OtherPlayerId = "P3"; Accepted(g, c); Assert.Equal(1, g.GetPlayerView("P1").OwnResources.Wood);
    }
    [Fact] public void GuildDuesRevealsOnlyToActorAndTakesTwoSpecifiedMixedCards()
    {
        var g = Fixture(ProgressCardKind.GuildDues, s => { s.Cities = [Piece(FirstVertex, "P2")]; P(s, "P2").Resources.Ore = 3; P(s, "P2").Commodities.Paper = 1; });
        var c = Play(ProgressCardKind.GuildDues); c.OtherPlayerId = "P2"; Accepted(g, c);
        Assert.Equal(3, g.GetPlayerView("P1").PrivateTargetResources.Ore); Assert.Null(g.GetPlayerView("P3").PrivateTargetResources);
        var choice = Cmd(CommandKind.ResolveProgressChoice); choice.Resources = new ResourceBag { Ore = 1 }; choice.Commodities = new CommodityBag { Paper = 1 }; Accepted(g, choice);
        Assert.Equal(1, g.GetPlayerView("P1").OwnResources.Ore); Assert.Equal(1, g.GetPlayerView("P1").OwnCommodities.Paper); Assert.Null(g.GetPlayerView("P1").PrivateTargetResources);
    }
    [Fact] public void MerchantGrantsOneVictoryPointAndTwoToOneResourceTrade()
    {
        var g = Fixture(ProgressCardKind.Merchant, s => { var t = s.Tiles.First(t => t.Resource == "wood"); s.Settlements = [Piece(t.Vertices[0])]; P(s).Resources.Wood = 2; });
        var t = g.GetPlayerView("P1").Board.Tiles.First(t => t.Resource == "wood"); Accepted(g, Play(ProgressCardKind.Merchant, t.Id));
        Assert.Equal(2, g.GetPlayerView("P1").OwnVictoryPoints);
        var trade = Cmd(CommandKind.BankTrade); trade.GiveResource = Resource.Wood; trade.GiveAmount = 2; trade.ReceiveResource = Resource.Ore; Accepted(g, trade); Assert.Equal(1, g.GetPlayerView("P1").OwnResources.Ore);
    }
    [Fact] public void MerchantFleetEnablesCommodityTradeOnlyUntilTurnEnds()
    {
        var g = Fixture(ProgressCardKind.MerchantFleet, s => P(s).Commodities.Cloth = 4);
        var c = Play(ProgressCardKind.MerchantFleet); c.ChooseCommodity = true; c.Commodity = Commodity.Cloth; Accepted(g, c);
        var trade = Cmd(CommandKind.BankTrade); trade.GiveIsCommodity = true; trade.GiveCommodity = Commodity.Cloth; trade.GiveAmount = 2; trade.ReceiveResource = Resource.Wheat; Accepted(g, trade);
        Assert.Equal(1, g.GetPlayerView("P1").OwnResources.Wheat); Accepted(g, Cmd(CommandKind.EndTurn)); Assert.Empty(g.GetPlayerView("P1").MerchantFleetCommodities);
    }
    [Fact] public void ResourceMonopolyTakesAtMostTwoFromEachOpponent()
    {
        var g = Fixture(ProgressCardKind.ResourceMonopoly, s => { P(s, "P2").Resources.Ore = 5; P(s, "P3").Resources.Ore = 1; }); var c = Play(ProgressCardKind.ResourceMonopoly); c.Resource = Resource.Ore; Accepted(g, c);
        Assert.Equal(3, g.GetPlayerView("P1").OwnResources.Ore); Assert.Equal(3, g.GetPlayerView("P2").OwnResources.Ore); Assert.Equal(0, g.GetPlayerView("P3").OwnResources.Ore);
    }
    [Fact] public void TradeMonopolyTakesAtMostOneFromEachOpponent()
    {
        var g = Fixture(ProgressCardKind.TradeMonopoly, s => { P(s, "P2").Commodities.Paper = 5; P(s, "P3").Commodities.Paper = 1; }); var c = Play(ProgressCardKind.TradeMonopoly); c.Commodity = Commodity.Paper; Accepted(g, c);
        Assert.Equal(2, g.GetPlayerView("P1").OwnCommodities.Paper); Assert.Equal(4, g.GetPlayerView("P2").OwnCommodities.Paper);
    }
    [Fact] public void DiplomacyRemovesEnemyOpenRoadButNotRoadBetweenOwnKnightAndBuilding()
    {
        var e = New().GetPlayerView("P1").Board.Edges[0];
        var g = Fixture(ProgressCardKind.Diplomacy, s => { s.Roads = [Piece(e.Id, "P2")]; s.Settlements = [Piece(e.Vertices[0], "P2")]; }); Accepted(g, Play(ProgressCardKind.Diplomacy, e.Id)); Assert.Empty(g.GetPlayerView("P1").Board.Roads);
        g = Fixture(ProgressCardKind.Diplomacy, s => { s.Roads = [Piece(e.Id, "P2")]; s.Settlements = [Piece(e.Vertices[0], "P2")]; s.Knights = [Knight(e.Vertices[1], "P2")]; }); Rejected(g, Play(ProgressCardKind.Diplomacy, e.Id));
    }
    [Fact] public void DiplomacyMovesOwnOpenRoadForFree()
    {
        var e = New().GetPlayerView("P1").Board.Edges[0]; var g = Fixture(ProgressCardKind.Diplomacy, s => { s.Roads = [Piece(e.Id)]; s.Settlements = [Piece(e.Vertices[0])]; });
        Accepted(g, Play(ProgressCardKind.Diplomacy, e.Id)); Assert.Equal("ProgressRoads", g.GetPlayerView("P1").PendingDecision.Kind);
        Accepted(g, Cmd(CommandKind.ResolveProgressChoice, target: e.Id)); Assert.Single(g.GetPlayerView("P1").Board.Roads);
    }
    [Fact] public void EspionageOnlyRevealsProgressHandToActorAndCannotStealPublicVictoryCard()
    {
        var g = Fixture(ProgressCardKind.Espionage, s => { P(s, "P2").ProgressCards = [ProgressCardKind.Medicine, ProgressCardKind.Crane]; P(s, "P2").ProgressVictoryCards = [ProgressCardKind.Printing]; });
        var c = Play(ProgressCardKind.Espionage); c.OtherPlayerId = "P2"; Accepted(g, c);
        Assert.Equal(2, g.GetPlayerView("P1").PrivateTargetProgressCards.Length); Assert.Empty(g.GetPlayerView("P3").PrivateTargetProgressCards);
        var choice = Cmd(CommandKind.ResolveProgressChoice); choice.SelectedProgressCard = ProgressCardKind.Printing; Rejected(g, choice);
        choice.SelectedProgressCard = ProgressCardKind.Crane; Accepted(g, choice); Assert.Contains(ProgressCardKind.Crane, g.GetPlayerView("P1").OwnProgressCards); Assert.Single(g.GetPlayerView("P2").OwnProgressCards);
    }
    [Fact] public void EncouragementActivatesInactiveKnightsAndPreservesExistingActiveTiming()
    {
        var e = New().GetPlayerView("P1").Board.Edges[0]; var g = Fixture(ProgressCardKind.Encouragement, s => { s.Roads = [Piece(e.Id)]; s.Knights = [Knight(e.Vertices[0]), Knight(e.Vertices[1], active: true)]; });
        Accepted(g, Play(ProgressCardKind.Encouragement)); var ks = g.GetPlayerView("P1").Board.Knights; Assert.All(ks, k => Assert.True(k.Active)); Assert.Equal(9, ks[0].ActivatedTurn); Assert.Equal(1, ks[1].ActivatedTurn);
    }
    [Fact] public void IntrigueDisplacesOpponentWithoutAnAttackingKnight()
    {
        var e = New().GetPlayerView("P1").Board.Edges[0]; var g = Fixture(ProgressCardKind.Intrigue, s => { s.Roads = [Piece(e.Id)]; s.Knights = [Knight(e.Vertices[1], "P2", 3, true)]; });
        Accepted(g, Play(ProgressCardKind.Intrigue, e.Vertices[1])); Assert.Empty(g.GetPlayerView("P1").Board.Knights);
    }
    [Fact] public void TaxationStealsOneCombinedHandCardFromEachDistinctAdjacentOpponent()
    {
        var g = Fixture(ProgressCardKind.Taxation, s => { var t = s.Tiles.First(t => t.Resource == "wood"); s.FirstBarbarianAttack = true; s.RobberTileId = s.Tiles.Single(t => t.Resource == "desert").Id; s.Settlements = [Piece(t.Vertices[0], "P2"), Piece(t.Vertices[2], "P2"), Piece(t.Vertices[4], "P3")]; P(s, "P2").Commodities.Coin = 2; P(s, "P3").Resources.Brick = 2; });
        var tile = g.GetPlayerView("P1").Board.Tiles.First(t => t.Resource == "wood"); Accepted(g, Play(ProgressCardKind.Taxation, tile.Id));
        Assert.Equal(1, g.GetPlayerView("P1").OwnCommodities.Coin); Assert.Equal(1, g.GetPlayerView("P1").OwnResources.Brick); Assert.Equal(tile.Id, g.GetPlayerView("P1").Board.RobberTileId);
    }
    [Fact] public void TaxationCannotActivateRobberBeforeFirstAttack()
    {
        var g = Fixture(ProgressCardKind.Taxation); Rejected(g, Play(ProgressCardKind.Taxation, g.GetPlayerView("P1").Board.Tiles.First().Id));
    }
    [Fact] public void TreasonVictimChoosesAndActorMayReplaceMightyWithoutPoliticsThree()
    {
        var e = New().GetPlayerView("P1").Board.Edges[0]; var g = Fixture(ProgressCardKind.Treason, s => { s.Roads = [Piece(e.Id)]; s.Knights = [Knight(e.Vertices[1], "P2", 3, true)]; });
        var c = Play(ProgressCardKind.Treason); c.OtherPlayerId = "P2"; Accepted(g, c);
        Accepted(g, Cmd(CommandKind.ResolveProgressChoice, "P2", e.Vertices[1]));
        var choice = Cmd(CommandKind.ResolveProgressChoice, target: e.Vertices[0]); choice.KnightLevel = 3; Accepted(g, choice);
        var knight = Assert.Single(g.GetPlayerView("P1").Board.Knights); Assert.Equal("P1", knight.PlayerId); Assert.Equal(3, knight.Level); Assert.True(knight.Active);
    }
    [Fact] public void TreasonStillRemovesVictimKnightWhenActorHasNoPlacement()
    {
        var g = Fixture(ProgressCardKind.Treason, s => s.Knights = [Knight(FirstVertex, "P2")]); var c = Play(ProgressCardKind.Treason); c.OtherPlayerId = "P2"; Accepted(g, c);
        Accepted(g, Cmd(CommandKind.ResolveProgressChoice, "P2", FirstVertex)); Assert.Empty(g.GetPlayerView("P1").Board.Knights); Assert.Equal(GamePhase.Action, g.GetPlayerView("P1").Phase);
    }
    [Fact] public void WeddingQueuesHigherScorersInTurnOrderAndTheyChooseMixedCards()
    {
        var b = New().GetPlayerView("P1").Board; var g = Fixture(ProgressCardKind.Wedding, s => { s.Cities = [Piece(b.Vertices[0].Id, "P2"), Piece(b.Vertices[10].Id, "P3")]; P(s, "P2").Resources.Wood = 1; P(s, "P2").Commodities.Coin = 2; P(s, "P3").Resources.Ore = 1; P(s, "P4").Resources.Brick = 2; });
        Accepted(g, Play(ProgressCardKind.Wedding)); Assert.Equal("P2", g.GetPlayerView("P1").PendingDecision.PlayerId);
        var c = Cmd(CommandKind.ResolveProgressChoice, "P2"); c.Resources = new ResourceBag { Wood = 1 }; c.Commodities = new CommodityBag { Coin = 1 }; Accepted(g, c);
        Assert.Equal("P3", g.GetPlayerView("P1").PendingDecision.PlayerId); c = Cmd(CommandKind.ResolveProgressChoice, "P3"); c.Resources = new ResourceBag { Ore = 1 }; Accepted(g, c);
        Assert.Equal(3, g.GetPlayerView("P1").OwnResources.Total + g.GetPlayerView("P1").OwnCommodities.Total); Assert.Equal(2, g.GetPlayerView("P4").OwnResources.Brick);
    }
    [Fact] public void SabotageDiscardsHalfForEqualOrHigherScorersAndIncludesCommodities()
    {
        var g = Fixture(ProgressCardKind.Sabotage, s => { P(s, "P2").Resources.Wood = 2; P(s, "P2").Commodities.Paper = 3; P(s).Resources.Wood = 4; });
        Accepted(g, Play(ProgressCardKind.Sabotage)); var d = g.GetPlayerView("P1").PendingDecision; Assert.Equal("P2", d.PlayerId); Assert.Equal(2, d.Amount);
        var c = Cmd(CommandKind.ResolveProgressChoice, "P2"); c.Commodities = new CommodityBag { Paper = 2 }; Accepted(g, c); Assert.Equal(3, g.GetPlayerView("P2").OwnResources.Total + g.GetPlayerView("P2").OwnCommodities.Total); Assert.Equal(4, g.GetPlayerView("P1").OwnResources.Wood);
    }
    [Fact] public void NoBenefitMonopolyStillReturnsPlayedCardToBottomOfItsDeck()
    {
        var g = Fixture(ProgressCardKind.ResourceMonopoly); Accepted(g, Play(ProgressCardKind.ResourceMonopoly)); var s = g.GetAuthoritativeStateForTesting();
        Assert.Empty(P(s).ProgressCards); Assert.Equal(ProgressCardKind.ResourceMonopoly, s.ProgressDecks.Single(d => d.Track == ImprovementTrack.Trade).Cards.Last());
    }
    [Fact] public void EndTurnRefusesFiveProgressCardsAndVoluntaryDiscardRestoresLimit()
    {
        var g = Fixture(ProgressCardKind.Merchant, s => P(s).ProgressCards = Enumerable.Repeat(ProgressCardKind.Merchant, 5).ToArray()); Rejected(g, Cmd(CommandKind.EndTurn));
        var c = Cmd(CommandKind.DiscardProgressCard); c.ProgressCard = ProgressCardKind.Merchant; Accepted(g, c); Accepted(g, Cmd(CommandKind.EndTurn));
    }
    [Fact] public void WrongSeatAndWrongCountDuringWeddingRollbackEverything()
    {
        var g = Fixture(ProgressCardKind.Wedding, s => { s.Cities = [Piece(FirstVertex, "P2")]; P(s, "P2").Resources.Ore = 3; }); Accepted(g, Play(ProgressCardKind.Wedding));
        var c = Cmd(CommandKind.ResolveProgressChoice, "P3"); c.Resources = new ResourceBag { Ore = 2 }; Rejected(g, c); c.PlayerId = "P2"; c.Resources.Ore = 1; Rejected(g, c);
    }
    [Fact] public void EventDrawWaitsForOverflowDiscardBeforeNextPlayerDrawsRecycledLastCard()
    {
        var g = Fixture(ProgressCardKind.Merchant); var s = g.GetAuthoritativeStateForTesting();
        var remaining = ProgressCatalog.Deck(ImprovementTrack.Science).ToList();
        P(s, "P2").ProgressCards = [ProgressCardKind.Alchemy, ProgressCardKind.Alchemy, ProgressCardKind.Crane, ProgressCardKind.Crane];
        P(s, "P4").ProgressVictoryCards = [ProgressCardKind.Printing];
        foreach (var card in P(s, "P2").ProgressCards.Concat(P(s, "P4").ProgressVictoryCards).Append(ProgressCardKind.Engineering)) Assert.True(remaining.Remove(card));
        P(s).ProgressCards = remaining.ToArray(); P(s, "P2").Improvements[2] = 1; P(s, "P3").Improvements[2] = 1;
        s.ProductionPending = true; s.LastDice1 = 1; s.LastDice2 = 2; Set(g, s);
        Invoke(g, "EnqueueEventProgress", s, ImprovementTrack.Science, 1); Invoke(g, "ContinueDecisions", s);
        Assert.Equal("ProgressDiscard", s.PendingDecision.Kind); Assert.Equal("P2", s.PendingDecision.PlayerId);
        Assert.Empty(P(s, "P3").ProgressCards); Assert.Empty(s.ProgressDecks.Single(d => d.Track == ImprovementTrack.Science).Cards);
        var discard = Cmd(CommandKind.DiscardProgressCard, "P2"); discard.ProgressCard = ProgressCardKind.Crane; Accepted(g, discard);
        Assert.Equal(new[] { ProgressCardKind.Crane }, g.GetPlayerView("P3").OwnProgressCards); Assert.Equal(GamePhase.Action, g.GetPlayerView("P1").Phase);
    }
    [Fact] public void ActiveVictoryPointDrawStopsFurtherDrawsAndProductionImmediately()
    {
        var g = Fixture(ProgressCardKind.Merchant); var s = g.GetAuthoritativeStateForTesting();
        P(s).DefenderPoints = 12; P(s).Improvements[2] = 1; P(s, "P2").Improvements[2] = 1;
        s.ProductionPending = true; s.LastDice1 = 1; s.LastDice2 = 2; Set(g, s);
        var deck = s.ProgressDecks.Single(d => d.Track == ImprovementTrack.Science); deck.Cards = new[] { ProgressCardKind.Printing }.Concat(deck.Cards.Where(c => c != ProgressCardKind.Printing)).ToArray();
        Invoke(g, "EnqueueEventProgress", s, ImprovementTrack.Science, 1); Invoke(g, "ContinueDecisions", s);
        Assert.Equal(GamePhase.Finished, s.Phase); Assert.Equal("P1", s.WinnerPlayerId); Assert.Empty(P(s, "P2").ProgressCards);
        Assert.False(s.ProductionPending); Assert.Empty(s.DecisionQueue); g.AssertInvariants();
    }
    [Fact] public void SmithingCannotPromoteToMightyWithoutFortressAndRollsBackFirstPromotion()
    {
        var e = New().GetPlayerView("P1").Board.Edges[0]; var g = Fixture(ProgressCardKind.Smithing, s => { s.Roads = [Piece(e.Id)]; s.Knights = [Knight(e.Vertices[0]), Knight(e.Vertices[1], level: 2)]; });
        var c = Play(ProgressCardKind.Smithing); c.TargetIds = e.Vertices.ToArray(); Rejected(g, c);
        Assert.Equal(new[] { 1, 2 }, g.GetPlayerView("P1").Board.Knights.Select(k => k.Level));
    }
    [Fact] public void ProgressPendingChoicesAppearInOwningPlayerLegalActionsOnly()
    {
        var g = Fixture(ProgressCardKind.Espionage, s => P(s, "P2").ProgressCards = [ProgressCardKind.Medicine]);
        var c = Play(ProgressCardKind.Espionage); c.OtherPlayerId = "P2"; Accepted(g, c);
        Assert.Contains(g.GetPlayerView("P1").LegalActions, action => action.Kind == CommandKind.ResolveProgressChoice && !action.Decline && action.SelectedProgressCard == ProgressCardKind.Medicine);
        Assert.DoesNotContain(g.GetPlayerView("P3").LegalActions, action => action.Kind == CommandKind.ResolveProgressChoice);
        Assert.DoesNotContain("Medicine", System.Text.Json.JsonSerializer.Serialize(g.GetPlayerView("P3").Events));
    }
    [Fact] public void DefenderRewardVictoryPointStopsProductionBeforeTheDicePayOut()
    {
        var g = Fixture(ProgressCardKind.Merchant); var s = g.GetAuthoritativeStateForTesting();
        P(s).DefenderPoints = 12; s.Phase = GamePhase.PendingChoice; s.PendingDecision = new Catan.Core.M4.PendingDecision { Kind = "DefenderProgress", PlayerId = "P1" };
        s.ProductionPending = true; s.LastDice1 = 1; s.LastDice2 = 2;
        var producing = s.Tiles.First(t => t.Number == 3); s.Settlements = [Piece(producing.Vertices[0], "P2")]; Set(g, s);
        var deck = s.ProgressDecks.Single(d => d.Track == ImprovementTrack.Science); deck.Cards = new[] { ProgressCardKind.Printing }.Concat(deck.Cards.Where(c => c != ProgressCardKind.Printing)).ToArray();
        var c = Cmd(CommandKind.ResolveChoice); c.Track = ImprovementTrack.Science; Accepted(g, c);
        Assert.Equal(GamePhase.Finished, g.GetPlayerView("P1").Phase); Assert.Equal(0, g.GetPlayerView("P2").OwnResources.Total);
    }
    [Fact] public void NewlyActivatedByEncouragementKnightCannotMoveThisTurn()
    {
        var e = New().GetPlayerView("P1").Board.Edges[0]; var g = Fixture(ProgressCardKind.Encouragement, s => { s.Roads = [Piece(e.Id)]; s.Knights = [Knight(e.Vertices[0])]; });
        Accepted(g, Play(ProgressCardKind.Encouragement)); var c = Cmd(CommandKind.MoveKnight, target: e.Vertices[1]); c.SourceId = e.Vertices[0]; Rejected(g, c);
    }
    [Fact] public void ProgressCardCommandRetryDoesNotTakeAnotherOpponentCard()
    {
        var g = Fixture(ProgressCardKind.ResourceMonopoly, s => P(s, "P2").Resources.Ore = 5); var c = Play(ProgressCardKind.ResourceMonopoly); c.Resource = Resource.Ore; Accepted(g, c);
        var before = g.Save(); var result = g.Execute(c); Assert.True(result.IsDuplicate); Assert.Empty(result.NewEvents); Assert.Equal(before, g.Save()); Assert.Equal(2, g.GetPlayerView("P1").OwnResources.Ore);
    }
}
