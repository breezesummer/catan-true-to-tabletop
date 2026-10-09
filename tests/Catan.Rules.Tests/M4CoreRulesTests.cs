using Catan.Core;
using Catan.Core.M4;
using Xunit;
using static Catan.Rules.Tests.M4CoreHarness;
using Command = Catan.Core.M4.Command;
using CommandKind = Catan.Core.M4.CommandKind;
using GamePhase = Catan.Core.M4.GamePhase;
using GameState = Catan.Core.M4.GameState;

namespace Catan.Rules.Tests;

public class M4CoreRulesTests
{
    private static string[] Sites(int count)
    {
        var board=New().GetPlayerView("P1").Board;var sites=new List<string>();
        foreach(var v in board.Vertices) if(!sites.Any(s=>board.Edges.Any(e=>e.Vertices.Contains(s)&&e.Vertices.Contains(v.Id)))) {sites.Add(v.Id);if(sites.Count==count)return sites.ToArray();}
        throw new Exception("No sites");
    }
    private static void Roll(GameState s,int total,bool barbarian=true)
    {
        s.Phase=GamePhase.ProductionAwaitRoll;s.AlchemyDice1=Math.Min(6,total-1);s.AlchemyDice2=total-s.AlchemyDice1;
        for(uint seed=1;;seed++) {uint n=seed;n^=n<<13;n^=n>>17;n^=n<<5;if(((n-1)%6<3)==barbarian){s.Random.State=seed;break;}}
    }
    [Theory][InlineData(3)][InlineData(4)]
    public void SetupPlacesOneSettlementOneCityAndOnlyResources(int count)
    {
        var game=New(count);CompleteSetup(game);var s=game.GetAuthoritativeStateForTesting();
        Assert.All(s.Players,p=> {Assert.Single(s.Settlements,x=>x.PlayerId==p.Id);Assert.Single(s.Cities,x=>x.PlayerId==p.Id);Assert.Equal(0,p.Commodities.Total);Assert.Equal(3,game.GetPlayerView(p.Id).OwnVictoryPoints);});
        Assert.Null(s.RobberTileId);Assert.False(s.FirstBarbarianAttack);Assert.Empty(s.DevelopmentDeck);Assert.Null(s.LargestArmyPlayerId);
        Assert.Equal(54,s.ProgressDecks.Sum(d=>d.Cards.Length));Assert.Equal(4,s.SaveFormatVersion);
    }
    [Theory][InlineData("wood",Resource.Wood,Commodity.Paper)][InlineData("wool",Resource.Wool,Commodity.Cloth)][InlineData("ore",Resource.Ore,Commodity.Coin)]
    public void CityProducesOneResourceAndOneCommodity(string terrain,Resource resource,Commodity commodity)
    {
        var tile=New().GetPlayerView("P1").Board.Tiles.First(t=>t.Resource==terrain);
        var game=Fixture(s=> {s.Cities=new[]{Piece(tile.Vertices[0])};});var before=game.GetAuthoritativeStateForTesting();
        Invoke(game,"Produce",before,tile.Number!.Value);Balance(before);SetState(game,before);
        int adjacent=before.Tiles.Count(t=>t.Number==tile.Number&&t.Resource==terrain&&t.Vertices.Contains(tile.Vertices[0]));
        Assert.Equal(adjacent,Player(before).Resources[resource]);Assert.Equal(adjacent,Player(before).Commodities[commodity]);
    }
    [Theory][InlineData("brick",Resource.Brick)][InlineData("wheat",Resource.Wheat)]
    public void BrickAndWheatCityStillProduceTwoResources(string terrain,Resource resource)
    {
        var tile=New().GetPlayerView("P1").Board.Tiles.First(t=>t.Resource==terrain);var game=Fixture(s=>s.Cities=new[]{Piece(tile.Vertices[0])});var s=game.GetAuthoritativeStateForTesting();
        Invoke(game,"Produce",s,tile.Number!.Value);int n=s.Tiles.Count(t=>t.Number==tile.Number&&t.Resource==terrain&&t.Vertices.Contains(tile.Vertices[0]));Assert.Equal(n*2,Player(s).Resources[resource]);
    }
    [Fact] public void InactiveRobberDoesNotBlockProductionAndSevenDoesNotSteal()
    {
        var game=Fixture(s=>Roll(s,7));Accepted(game,Cmd(CommandKind.RollDice));Assert.Equal(GamePhase.Action,game.GetPlayerView("P1").Phase);Rejected(game,Cmd(CommandKind.MoveRobber,"P1","T01"));
    }
    [Fact] public void WallsProtectCombinedHandAndDiscardRemovesCommodities()
    {
        string site=Sites(1)[0];var game=Fixture(s=>{s.Cities=new[]{Piece(site)};s.Walls=new[]{Piece(site)};Player(s).Resources=Bag(wood:4);Player(s).Commodities.Cloth=5;Roll(s,7);});
        Accepted(game,Cmd(CommandKind.RollDice));Assert.Equal(GamePhase.Action,game.GetPlayerView("P1").Phase);
        var second=Fixture(s=>{s.Cities=new[]{Piece(site)};s.Walls=new[]{Piece(site)};Player(s).Resources=Bag(wood:5);Player(s).Commodities.Cloth=5;Roll(s,7);});
        Accepted(second,Cmd(CommandKind.RollDice));Assert.Equal(5,second.GetPlayerView("P1").Discards.Single().Amount);
        var bad=Cmd(CommandKind.DiscardResources);bad.Commodities=new CommodityBag {Cloth=4};Rejected(second,bad);
        var discard=Cmd(CommandKind.DiscardResources);discard.Commodities=new CommodityBag {Cloth=5};Accepted(second,discard);Assert.Equal(GamePhase.Action,second.GetPlayerView("P1").Phase);
    }
    [Fact] public void WallCostsTwoBrickAndCannotDuplicateOrUseSettlement()
    {
        var v=Sites(2);var game=Fixture(s=>{s.Cities=new[]{Piece(v[0])};s.Settlements=new[]{Piece(v[1])};Player(s).Resources=Bag(brick:4);});
        Accepted(game,Cmd(CommandKind.BuildWall,"P1",v[0]));Assert.Equal(2,game.GetPlayerView("P1").OwnResources.Brick);Rejected(game,Cmd(CommandKind.BuildWall,"P1",v[0]));Rejected(game,Cmd(CommandKind.BuildWall,"P1",v[1]));
    }
    [Theory][InlineData(ImprovementTrack.Trade,Commodity.Cloth)][InlineData(ImprovementTrack.Politics,Commodity.Coin)][InlineData(ImprovementTrack.Science,Commodity.Paper)]
    public void ImprovementsPayNextLevelCommodityAndRequireCity(ImprovementTrack track,Commodity commodity)
    {
        var game=Fixture(s=>{s.Cities=new[]{Piece(Sites(1)[0])};Player(s).Commodities[commodity]=6;});var c=Cmd(CommandKind.ImproveCity);c.Track=track;Accepted(game,c);
        c=Cmd(CommandKind.ImproveCity);c.Track=track;Accepted(game,c);Assert.Equal(2,game.GetPlayerView("P1").OwnImprovements[(int)track]);Assert.Equal(3,game.GetPlayerView("P1").OwnCommodities[commodity]);
        var none=Fixture(s=>Player(s).Commodities[commodity]=6);c=Cmd(CommandKind.ImproveCity);c.Track=track;Rejected(none,c);
    }
    [Fact] public void MetropolisFourCanBeStolenAtFiveAndPendingOwnerChoosesCity()
    {
        var sites=Sites(2);var game=Fixture(s=>{s.Cities=new[]{Piece(sites[0]),Piece(sites[1],"P2")};Player(s).Improvements[0]=3;Player(s).Commodities.Cloth=4;Player(s,"P2").Improvements[0]=4;Player(s,"P2").Commodities.Cloth=5;});
        Accepted(game,Cmd(CommandKind.ImproveCity));Assert.Equal("Metropolis",game.GetPlayerView("P1").PendingDecision.Kind);
        Rejected(game,Cmd(CommandKind.ResolveChoice,"P2",sites[0]));Accepted(game,Cmd(CommandKind.ResolveChoice,"P1",sites[0]));Assert.Equal(4,game.GetPlayerView("P1").OwnVictoryPoints);
        var s=game.GetAuthoritativeStateForTesting();s.ActivePlayerId="P2";SetState(game,s);Accepted(game,Cmd(CommandKind.ImproveCity,"P2"));Accepted(game,Cmd(CommandKind.ResolveChoice,"P2",sites[1]));Assert.Equal("P2",game.GetPlayerView("P1").Board.Metropolises.Single().PlayerId);
    }
    [Fact] public void TradeAbilityAllowsTwoCommoditiesForOneResource()
    {
        var game=Fixture(s=>{Player(s).Improvements[0]=3;Player(s).Commodities.Cloth=2;});var c=Cmd(CommandKind.BankTrade);c.GiveIsCommodity=true;c.GiveCommodity=Commodity.Cloth;c.ReceiveResource=Resource.Ore;c.GiveAmount=2;Accepted(game,c);Assert.Equal(1,game.GetPlayerView("P1").OwnResources.Ore);
    }
    [Fact] public void DomesticTradeCanExchangeMixedResourcesAndCommodities()
    {
        var game=Fixture(s=>{Player(s).Resources=Bag(wood:1);Player(s).Commodities.Paper=1;Player(s,"P2").Commodities.Coin=2;});
        var c=Cmd(CommandKind.ProposeTrade);c.OtherPlayerId="P2";c.Give=Bag(wood:1);c.GiveCommodities=new CommodityBag {Paper=1};c.ReceiveCommodities=new CommodityBag {Coin=2};Accepted(game,c);Accepted(game,Cmd(CommandKind.AcceptTrade,"P2"));Assert.Equal(2,game.GetPlayerView("P1").OwnCommodities.Coin);Assert.Equal(1,game.GetPlayerView("P2").OwnResources.Wood);
    }
    [Fact] public void RecruitmentActivationPromotionCostsAndSameTurnActionRestriction()
    {
        var edge=New().GetPlayerView("P1").Board.Edges[0];var game=Fixture(s=>{s.Roads=new[]{Piece(edge.Id)};Player(s).Resources=Bag(wool:2,ore:2,wheat:2);});
        Accepted(game,Cmd(CommandKind.RecruitKnight,"P1",edge.Vertices[0]));Accepted(game,Cmd(CommandKind.ActivateKnight,"P1",edge.Vertices[0]));Accepted(game,Cmd(CommandKind.PromoteKnight,"P1",edge.Vertices[0]));
        var move=Cmd(CommandKind.MoveKnight,"P1",edge.Vertices[1]);move.SourceId=edge.Vertices[0];Rejected(game,move);Rejected(game,Cmd(CommandKind.PromoteKnight,"P1",edge.Vertices[0]));Assert.True(game.GetPlayerView("P1").Board.Knights.Single().Active);
        Assert.Equal(0,game.GetPlayerView("P1").OwnResources.Wool);Assert.Equal(1,game.GetPlayerView("P1").OwnResources.Wheat);
    }
    [Fact] public void MightyPromotionRequiresFortress()
    {
        var site=Sites(1)[0];var game=Fixture(s=>{s.Knights=new[]{new Knight {PlayerId="P1",LocationId=site,Level=2}};Player(s).Resources=Bag(wool:1,ore:1);});Rejected(game,Cmd(CommandKind.PromoteKnight,"P1",site));var state=game.GetAuthoritativeStateForTesting();Player(state).Improvements[1]=3;SetState(game,state);Accepted(game,Cmd(CommandKind.PromoteKnight,"P1",site));Assert.Equal(3,game.GetPlayerView("P1").Board.Knights.Single().Level);
    }
    [Fact] public void KnightMovesThenMayReactivateButCannotMoveAgain()
    {
        var edge=New().GetPlayerView("P1").Board.Edges[0];var game=Fixture(s=>{s.Roads=new[]{Piece(edge.Id)};s.Knights=new[]{new Knight {PlayerId="P1",LocationId=edge.Vertices[0],Level=1,Active=true}};Player(s).Resources=Bag(wheat:1);});
        var move=Cmd(CommandKind.MoveKnight,"P1",edge.Vertices[1]);move.SourceId=edge.Vertices[0];Accepted(game,move);Accepted(game,Cmd(CommandKind.ActivateKnight,"P1",edge.Vertices[1]));move=Cmd(CommandKind.MoveKnight,"P1",edge.Vertices[0]);move.SourceId=edge.Vertices[1];Rejected(game,move);
    }
    [Fact] public void KnightsBlockSettlementSitesAndEnemyRoadContinuation()
    {
        var board=New().GetPlayerView("P1").Board;var pivot=board.Vertices.First(v=>board.Edges.Count(e=>e.Vertices.Contains(v.Id))==3);var edges=board.Edges.Where(e=>e.Vertices.Contains(pivot.Id)).Take(2).ToArray();
        var game=Fixture(s=>{s.Roads=new[]{Piece(edges[0].Id)};s.Knights=new[]{new Knight {PlayerId="P2",LocationId=pivot.Id,Level=1}};Player(s).Resources=Bag(wood:2,brick:2,wool:1,wheat:1);});Rejected(game,Cmd(CommandKind.BuildRoad,"P1",edges[1].Id));Rejected(game,Cmd(CommandKind.BuildSettlement,"P1",pivot.Id));
    }
    [Fact] public void BarbarianPillagePrecedesProductionAndActivatesRobber()
    {
        var tile=New().GetPlayerView("P1").Board.Tiles.First(t=>t.Resource=="wood");var game=Fixture(s=>{s.Cities=new[]{Piece(tile.Vertices[0])};s.BarbarianPosition=6;Roll(s,tile.Number!.Value);});
        Accepted(game,Cmd(CommandKind.RollDice));var view=game.GetPlayerView("P1");Assert.Equal("PillageCity",view.PendingDecision.Kind);Assert.Equal(0,view.OwnResources.Total);Assert.True(view.FirstBarbarianAttack);
        Accepted(game,Cmd(CommandKind.ResolveChoice,"P1",tile.Vertices[0]));Assert.Empty(game.GetPlayerView("P1").Board.Cities);Assert.Equal(0,game.GetPlayerView("P1").OwnCommodities.Total);Assert.True(game.GetPlayerView("P1").OwnResources.Wood>0);
    }
    [Fact] public void BarbarianDefeatSkipsMetropolisOnlyAndPillagesAllTiedVulnerablePlayers()
    {
        var sites=Sites(3);var game=Fixture(s=>{s.Cities=new[]{Piece(sites[0]),Piece(sites[1],"P2"),Piece(sites[2],"P3")};Player(s).Improvements[0]=4;s.Metropolises=new[]{new Metropolis {Track=ImprovementTrack.Trade,PlayerId="P1",LocationId=sites[0]}};s.BarbarianPosition=6;Roll(s,7);});
        Accepted(game,Cmd(CommandKind.RollDice));Assert.Equal("P2",game.GetPlayerView("P1").PendingDecision.PlayerId);Accepted(game,Cmd(CommandKind.ResolveChoice,"P2",sites[1]));Assert.Equal("P3",game.GetPlayerView("P1").PendingDecision.PlayerId);Accepted(game,Cmd(CommandKind.ResolveChoice,"P3",sites[2]));Assert.Single(game.GetPlayerView("P1").Board.Cities);Assert.Equal(GamePhase.RobberMove,game.GetPlayerView("P1").Phase);
    }
    [Fact] public void DefenderWinAwardsOnePointAndDeactivatesAllKnights()
    {
        var sites=Sites(2);var game=Fixture(s=>{s.Cities=new[]{Piece(sites[0])};s.Knights=new[]{new Knight {PlayerId="P2",LocationId=sites[1],Level=1,Active=true}};s.BarbarianPosition=6;Roll(s,7);});Accepted(game,Cmd(CommandKind.RollDice));Assert.Equal(1,game.GetPlayerView("P2").OwnVictoryPoints);Assert.False(game.GetPlayerView("P1").Board.Knights.Single().Active);Assert.Equal(0,game.GetPlayerView("P1").BarbarianPosition);
    }
    [Fact] public void TiedDefendersChooseProgressInTurnOrderBeforeProduction()
    {
        var sites=Sites(3);var game=Fixture(s=>{s.Cities=new[]{Piece(sites[0])};s.Knights=new[]{new Knight {PlayerId="P1",LocationId=sites[1],Level=1,Active=true},new Knight {PlayerId="P2",LocationId=sites[2],Level=1,Active=true}};s.BarbarianPosition=6;Roll(s,7);});Accepted(game,Cmd(CommandKind.RollDice));Assert.Equal("P1",game.GetPlayerView("P1").PendingDecision.PlayerId);Accepted(game,Cmd(CommandKind.ResolveChoice));Assert.Equal("P2",game.GetPlayerView("P1").PendingDecision.PlayerId);Accepted(game,Cmd(CommandKind.ResolveChoice,"P2"));Assert.Equal(GamePhase.RobberMove,game.GetPlayerView("P1").Phase);
    }
    [Fact] public void AqueductGrantsOneResourceAfterNoProduction()
    {
        var game=Fixture(s=>{Player(s).Improvements[2]=3;Roll(s,2);});Accepted(game,Cmd(CommandKind.RollDice));Assert.Equal("Aqueduct",game.GetPlayerView("P1").PendingDecision.Kind);var c=Cmd(CommandKind.ResolveChoice);c.Resource=Resource.Ore;Accepted(game,c);Assert.Equal(1,game.GetPlayerView("P1").OwnResources.Ore);
    }
    [Fact] public void WinsAtThirteenOnlyOnOwnTurn()
    {
        var game=Fixture(s=>Player(s,"P2").DefenderPoints=13);Accepted(game,Cmd(CommandKind.EndTurn));Assert.Equal("P2",game.GetPlayerView("P1").WinnerPlayerId);var low=Fixture(s=>Player(s).DefenderPoints=12);Rejected(low,Cmd(CommandKind.DeclareVictory));
    }
    [Fact] public void DevelopmentCardsAndLargestArmyAreUnavailable()
    {
        var game=Fixture(s=>Player(s).Resources=Bag(wool:1,wheat:1,ore:1));Rejected(game,Cmd(CommandKind.BuyDevelopmentCard));Rejected(game,Cmd(CommandKind.PlayKnight));Assert.Null(game.GetPlayerView("P1").LargestArmyPlayerId);
    }
    [Fact] public void DisplacedKnightMayRetreatToVacatedAttackerSourceAlongOwnRoads()
    {
        var board=New().GetPlayerView("P1").Board;var tile=board.Tiles[0];var attack=board.Edges.First(e=>e.Vertices.All(v=>tile.Vertices.Contains(v)));string source=attack.Vertices[0],target=attack.Vertices[1];
        var otherEdges=board.Edges.Where(e=>e.Id!=attack.Id&&e.Vertices.All(v=>tile.Vertices.Contains(v))).ToArray();
        var game=Fixture(s=>{s.Roads=new[]{Piece(attack.Id)}.Concat(otherEdges.Select(e=>Piece(e.Id,"P2"))).ToArray();s.Knights=new[]{new Knight {PlayerId="P1",LocationId=source,Level=2,Active=true},new Knight {PlayerId="P2",LocationId=target,Level=1,Active=true}};});
        var c=Cmd(CommandKind.DisplaceKnight,"P1",target);c.SourceId=source;Accepted(game,c);Assert.Contains(source,game.GetPlayerView("P2").PendingDecision.Options);Assert.Equal(1,game.GetPlayerView("P1").Players.Single(p=>p.Id=="P2").KnightSupply[0]);
        Accepted(game,Cmd(CommandKind.ResolveChoice,"P2",source));Assert.True(game.GetPlayerView("P1").Board.Knights.Single(k=>k.PlayerId=="P2").Active);Assert.False(game.GetPlayerView("P1").Board.Knights.Single(k=>k.PlayerId=="P1").Active);
    }
    [Fact] public void TrappedDisplacedKnightReturnsToSupplyWithoutPending()
    {
        var edge=New().GetPlayerView("P1").Board.Edges[0];var game=Fixture(s=>{s.Roads=new[]{Piece(edge.Id)};s.Knights=new[]{new Knight {PlayerId="P1",LocationId=edge.Vertices[0],Level=2,Active=true},new Knight {PlayerId="P2",LocationId=edge.Vertices[1],Level=1}};});var c=Cmd(CommandKind.DisplaceKnight,"P1",edge.Vertices[1]);c.SourceId=edge.Vertices[0];Accepted(game,c);Assert.Null(game.GetPlayerView("P1").PendingDecision);Assert.Single(game.GetPlayerView("P1").Board.Knights);Assert.Equal(2,game.GetPlayerView("P1").Players.Single(p=>p.Id=="P2").KnightSupply[0]);
    }
    [Fact] public void PillageWithNoSettlementSupplyReservesCityAndRequiresItsRestorationFirst()
    {
        var sites=Sites(6);var game=Fixture(s=>{s.Cities=new[]{Piece(sites[0])};s.Walls=new[]{Piece(sites[0])};s.Settlements=sites.Skip(1).Select(v=>Piece(v)).ToArray();Player(s).Resources=Bag(wheat:2,ore:3);s.BarbarianPosition=6;Roll(s,2);});Accepted(game,Cmd(CommandKind.RollDice));Accepted(game,Cmd(CommandKind.ResolveChoice,"P1",sites[0]));var state=game.GetAuthoritativeStateForTesting();Assert.Single(state.DowngradedCities);Assert.Empty(state.Walls);Assert.Equal(3,Player(state).Pieces.Cities);Assert.Equal(0,Player(state).Pieces.Settlements);
        Rejected(game,Cmd(CommandKind.BuildCity,"P1",sites[1]));Accepted(game,Cmd(CommandKind.BuildCity,"P1",sites[0]));Assert.Empty(game.GetAuthoritativeStateForTesting().DowngradedCities);Assert.Equal(3,game.GetPlayerView("P1").Players.Single(p=>p.Id=="P1").Pieces.Cities);
    }
    [Fact] public void CommodityShortageWithMultipleClaimantsDoesNotBlockResourceProduction()
    {
        var tile=New().GetPlayerView("P1").Board.Tiles.First(t=>t.Resource=="wood");var game=Fixture(s=>{s.Cities=new[]{Piece(tile.Vertices[0]),Piece(tile.Vertices[3],"P2")};Player(s,"P3").Commodities.Paper=11;});var s=game.GetAuthoritativeStateForTesting();Invoke(game,"Produce",s,tile.Number!.Value);Assert.Equal(0,Player(s).Commodities.Paper);Assert.Equal(0,Player(s,"P2").Commodities.Paper);Assert.True(Player(s).Resources.Wood>0);Assert.True(Player(s,"P2").Resources.Wood>0);
    }
    [Fact] public void RobberBlocksBothKindsAndTheftCanTakeCommodity()
    {
        var tile=New().GetPlayerView("P1").Board.Tiles.First(t=>t.Resource=="wood");var game=Fixture(s=>{s.Cities=new[]{Piece(tile.Vertices[0])};s.FirstBarbarianAttack=true;s.RobberTileId=tile.Id;});var s=game.GetAuthoritativeStateForTesting();Invoke(game,"Produce",s,tile.Number!.Value);Assert.Equal(0,Player(s).Commodities.Paper);Assert.Equal(0,Player(s).Resources.Wood);
        var theft=Fixture(s=>{Player(s,"P2").Commodities.Coin=1;});s=theft.GetAuthoritativeStateForTesting();Invoke(theft,"StealHandCard",s,Player(s),Player(s,"P2"));Assert.Equal(1,Player(s).Commodities.Coin);Assert.Equal(0,Player(s,"P2").Commodities.Coin);
    }
    [Fact] public void BasicKnightSupplyLimitedToTwoAndNoVictoryPoints()
    {
        var board=New().GetPlayerView("P1").Board;var edges=board.Edges.Take(3).ToArray();var vertices=edges.SelectMany(e=>e.Vertices).Distinct().Take(3).ToArray();var game=Fixture(s=>{s.Roads=edges.Select(e=>Piece(e.Id)).ToArray();Player(s).Resources=Bag(wool:3,ore:3);});Accepted(game,Cmd(CommandKind.RecruitKnight,"P1",vertices[0]));Accepted(game,Cmd(CommandKind.RecruitKnight,"P1",vertices[1]));Rejected(game,Cmd(CommandKind.RecruitKnight,"P1",vertices[2]));Assert.Equal(0,game.GetPlayerView("P1").OwnVictoryPoints);
    }
    [Fact] public void ActiveKnightChasesRobberOnlyWhenAdjacent()
    {
        var board=New().GetPlayerView("P1").Board;var tile=board.Tiles[0];var far=board.Vertices.First(v=>!tile.Vertices.Contains(v.Id));var game=Fixture(s=>{s.FirstBarbarianAttack=true;s.RobberTileId=tile.Id;s.Knights=new[]{new Knight {PlayerId="P1",LocationId=tile.Vertices[0],Level=1,Active=true},new Knight {PlayerId="P1",LocationId=far.Id,Level=1,Active=true}};});Rejected(game,Cmd(CommandKind.ChaseRobber,"P1",far.Id));Accepted(game,Cmd(CommandKind.ChaseRobber,"P1",tile.Vertices[0]));Assert.Equal(GamePhase.RobberMove,game.GetPlayerView("P1").Phase);Assert.False(game.GetPlayerView("P1").Board.Knights.Single(k=>k.LocationId==tile.Vertices[0]).Active);
    }
    [Fact] public void BarbarianReturnAndKnightDeactivationWaitForAllRewardChoices()
    {
        var sites=Sites(3);var game=Fixture(s=>{s.Cities=new[]{Piece(sites[0])};s.Knights=new[]{new Knight {PlayerId="P1",LocationId=sites[1],Level=1,Active=true},new Knight {PlayerId="P2",LocationId=sites[2],Level=1,Active=true}};s.BarbarianPosition=6;Roll(s,7);});
        Accepted(game,Cmd(CommandKind.RollDice));Assert.Equal(7,game.GetPlayerView("P1").BarbarianPosition);Assert.All(game.GetPlayerView("P1").Board.Knights,k=>Assert.True(k.Active));
        Accepted(game,Cmd(CommandKind.ResolveChoice));Assert.Equal(7,game.GetPlayerView("P1").BarbarianPosition);Assert.All(game.GetPlayerView("P1").Board.Knights,k=>Assert.True(k.Active));
        Accepted(game,Cmd(CommandKind.ResolveChoice,"P2"));Assert.Equal(0,game.GetPlayerView("P1").BarbarianPosition);Assert.All(game.GetPlayerView("P1").Board.Knights,k=>Assert.False(k.Active));Assert.Equal(GamePhase.RobberMove,game.GetPlayerView("P1").Phase);
    }
    [Fact] public void WinningDefenderRewardEndsBeforeBarbarianReturnAndProduction()
    {
        var sites=Sites(2);var game=Fixture(s=>{s.Cities=new[]{Piece(sites[0])};Player(s).DefenderPoints=10;s.Knights=new[]{new Knight {PlayerId="P1",LocationId=sites[1],Level=1,Active=true}};s.BarbarianPosition=6;Roll(s,7);});Accepted(game,Cmd(CommandKind.RollDice));Assert.Equal(GamePhase.Finished,game.GetPlayerView("P1").Phase);Assert.Equal(7,game.GetPlayerView("P1").BarbarianPosition);Assert.True(game.GetPlayerView("P1").Board.Knights.Single().Active);Assert.Empty(game.GetPlayerView("P1").Discards);
    }
}
