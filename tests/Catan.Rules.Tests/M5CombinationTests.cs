using System.Text.Json.Nodes;
using Catan.Core;
using Catan.Core.M5;
using Xunit;
using static Catan.Rules.Tests.M5Harness;
using Command = Catan.Core.M5.Command;
using CommandKind = Catan.Core.M5.CommandKind;
using GamePhase = Catan.Core.M5.GamePhase;
using GameState = Catan.Core.M5.GameState;
using PlayerView = Catan.Core.M5.PlayerView;
using PublicPlayer = Catan.Core.M5.PublicPlayer;

namespace Catan.Rules.Tests;

public sealed class M5CombinationTests
{
    [Theory]
    [InlineData("heading-for-new-shores", 3)] [InlineData("heading-for-new-shores", 4)]
    [InlineData("through-the-desert", 3)] [InlineData("through-the-desert", 4)]
    public void SupportedMapsKeepOfficialTopologyAndSetCombinedRules(string scenario, int players)
    {
        var g = CombinedGameSession.Create(scenario, players, 47);
        var v = g.GetPlayerView("P1"); var source = Catan.Core.M3.SeafarersScenarios.Create(scenario, players);
        Assert.Equal(16, v.TargetVictoryPoints); Assert.Equal(scenario, v.ScenarioId);
        Assert.Equal(source.Tiles.Select(t => t.Id), v.Board.Tiles.Select(t => t.Id));
        Assert.Equal(source.Tiles.Select(t => t.Resource), v.Board.Tiles.Select(t => t.Resource));
        Assert.Equal(source.Tiles.Select(t => t.Number), v.Board.Tiles.Select(t => t.Number));
        Assert.Equal(source.Edges.Select(e => e.Id), v.Board.Edges.Select(e => e.Id));
        Assert.Null(v.Board.RobberTileId); Assert.Null(v.Board.PirateTileId);
        Assert.Empty(g.GetAuthoritativeStateForTesting().DevelopmentDeck);
        Assert.Equal(54, g.GetAuthoritativeStateForTesting().ProgressDecks.Sum(d => d.Cards.Length));
        CompleteSetup(g); var s = g.GetAuthoritativeStateForTesting();
        Assert.Equal(players, s.Settlements.Length); Assert.Equal(players, s.Cities.Length);
        Assert.Equal(players * 2, s.Roads.Length + s.Ships.Length);
        Assert.Null(s.LargestArmyPlayerId); Invariants(g);
    }

    [Theory]
    [InlineData("four-islands")] [InlineData("fog-islands")] [InlineData("forgotten-tribe")]
    [InlineData("cloth-for-catan")] [InlineData("pirate-islands")] [InlineData("wonders-of-catan")]
    [InlineData("new-world")] [InlineData("unknown")]
    public void UnsupportedScenariosAreExplicitlyRejected(string scenario) =>
        Assert.ThrowsAny<ArgumentException>(() => CombinedGameSession.Create(scenario));

    [Theory] [InlineData(2)] [InlineData(5)]
    public void UnsupportedPlayerCountsAreRejected(int players) =>
        Assert.ThrowsAny<ArgumentException>(() => CombinedGameSession.Create("heading-for-new-shores", players));

    [Fact]
    public void SeaOnlyVerticesCannotHoldSettlementsAndSeaOnlyEdgesCannotHoldRoads()
    {
        var board = New().GetPlayerView("P1").Board;
        var edge = board.Edges.First(e => board.Tiles.Where(t => e.Vertices.All(t.Vertices.Contains)).All(t => t.Resource == "sea"));
        var g = Fixture(s => { Player(s).Resources = Bag(4,4,4,4,4); s.Ships = new[] { Ship(edge.Id) }; });
        foreach (var vertex in edge.Vertices.Where(v => board.Tiles.Where(t => t.Vertices.Contains(v)).All(t => t.Resource == "sea")))
            Rejected(g, Cmd(CommandKind.BuildSettlement, target: vertex));
        Rejected(g, Cmd(CommandKind.BuildRoad, target: edge.Id));
    }

    [Fact]
    public void KnightTravelsAcrossRoadShipJunctionWithoutBuildingButLongestRouteDoesNotJoin()
    {
        var b = New().GetPlayerView("P1").Board; var junction = Junction(b);
        var start = junction.road.Vertices.Single(v => v != junction.vertex);
        var end = junction.ship.Vertices.Single(v => v != junction.vertex);
        var g = Fixture(s => { s.Roads = new[] { Piece(junction.road.Id) }; s.Ships = new[] { Ship(junction.ship.Id) }; s.Knights = new[] { Knight(start) }; });
        Assert.Equal(1, g.GetPlayerView("P1").Players[0].LongestRoadLength);
        var move = Cmd(CommandKind.MoveKnight, target: end); move.SourceId = start; Accepted(g, move);
        var knight = g.GetPlayerView("P1").Board.Knights.Single(); Assert.Equal(end, knight.LocationId); Assert.False(knight.Active);
    }

    [Fact]
    public void RecruitKnightAtSeaIntersectionCostsWoolOreAndCanNotMoveWhenInactive()
    {
        var b = New().GetPlayerView("P1").Board;
        var edge = b.Edges.First(e => e.Vertices.Any(v => b.Tiles.Where(t => t.Vertices.Contains(v)).All(t => t.Resource == "sea")));
        var target = edge.Vertices.First(v => b.Tiles.Where(t => t.Vertices.Contains(v)).All(t => t.Resource == "sea"));
        var g = Fixture(s => { s.Ships = new[] { Ship(edge.Id) }; Player(s).Resources = Bag(wool:1,ore:1); });
        Accepted(g, Cmd(CommandKind.RecruitKnight, target:target));
        Assert.Equal(0, g.GetPlayerView("P1").OwnResources.Total);
        var move = Cmd(CommandKind.MoveKnight, target:edge.Vertices.Single(v => v != target)); move.SourceId=target; Rejected(g,move);
    }

    [Fact]
    public void ForeignKnightBlocksTravelAlongOwnShipChain()
    {
        var b=New().GetPlayerView("P1").Board; var chain=ShipChain(b,3);
        var g=Fixture(s => { s.Ships=chain.edges.Select(e=>Ship(e.Id)).ToArray(); s.Knights=new[]{Knight(chain.vertices[0]),Knight(chain.vertices[1],"P2")}; });
        var c=Cmd(CommandKind.MoveKnight,target:chain.vertices[3]);c.SourceId=chain.vertices[0];Rejected(g,c);
    }

    [Fact]
    public void OwnKnightClosesShipRouteAndCannotBeAbandoned()
    {
        var b=New().GetPlayerView("P1").Board; var chain=ShipChain(b,3);
        var g=Fixture(s => { s.Settlements=new[]{Piece(chain.vertices[0])};s.Ships=chain.edges.Select(e=>Ship(e.Id)).ToArray();s.Knights=new[]{Knight(chain.vertices[3])}; });
        Assert.DoesNotContain(chain.edges[2].Id,g.GetPlayerView("P1").MovableShipEdgeIds);
        var c=Cmd(CommandKind.MoveShip,target:b.Edges.First(e=>!chain.edges.Contains(e)&&Sea(b,e)).Id);c.SourceId=chain.edges[2].Id;Rejected(g,c);
    }

    [Fact]
    public void EnemyKnightCutsLongestRouteButDoesNotMakeAdjacentShipsMovable()
    {
        var b=New().GetPlayerView("P1").Board; var chain=ShipChain(b,4);
        var g=Fixture(s => {s.Settlements=new[]{Piece(chain.vertices[0])};s.Ships=chain.edges.Select(e=>Ship(e.Id)).ToArray();s.Knights=new[]{Knight(chain.vertices[2],"P2")};});
        Assert.Equal(2,g.GetPlayerView("P1").Players[0].LongestRoadLength);
        Assert.DoesNotContain(chain.edges[1].Id,g.GetPlayerView("P1").MovableShipEdgeIds);
        Assert.DoesNotContain(chain.edges[2].Id,g.GetPlayerView("P1").MovableShipEdgeIds);
    }

    [Fact]
    public void GoldCityPaysTwoResourcesAndNeverCommoditiesOrAqueduct()
    {
        var gold=New().GetPlayerView("P1").Board.Tiles.First(t=>t.Resource=="gold");
        var g=Fixture(s=>{s.Cities=new[]{Piece(gold.Vertices[0])};Player(s).Improvements[2]=3;s.LastDice1=Math.Min(6,gold.Number!.Value-1);s.LastDice2=gold.Number.Value-s.LastDice1;});
        var state=g.GetAuthoritativeStateForTesting();Invoke(g,"ResolveProduction",state);SetState(g,state);
        var v=g.GetPlayerView("P1");Assert.Equal("Gold",v.PendingDecision.Kind);Assert.Equal(2,v.PendingDecision.Amount);
        var bad=Cmd(CommandKind.ResolveChoice);bad.Resources=Bag(ore:1);bad.Commodities=new CommodityBag{Coin=1};Rejected(g,bad);
        var c=Cmd(CommandKind.ResolveChoice);c.Resources=Bag(ore:2);Accepted(g,c);
        Assert.Equal(2,g.GetPlayerView("P1").OwnResources.Ore);Assert.Equal(0,g.GetPlayerView("P1").OwnCommodities.Total);
        Assert.Null(g.GetPlayerView("P1").PendingDecision);
    }

    [Fact]
    public void MerchantCannotOccupyGoldAndTaxationCannotMovePirate()
    {
        var b=New().GetPlayerView("P1").Board;var gold=b.Tiles.First(t=>t.Resource=="gold");var sea=b.Tiles.First(t=>t.Resource=="sea");
        var g=Fixture(s=>{s.Settlements=new[]{Piece(gold.Vertices[0])};Player(s).ProgressCards=new[]{ProgressCardKind.Merchant,ProgressCardKind.Taxation};s.FirstBarbarianAttack=true;s.RobberTileId=b.Tiles.First(t=>t.Resource=="desert").Id;s.PirateTileId="frame";});
        Rejected(g,Play(ProgressCardKind.Merchant,gold.Id));Rejected(g,Play(ProgressCardKind.Taxation,sea.Id));
        Assert.Equal("frame",g.GetPlayerView("P1").Board.PirateTileId);
    }

    [Fact]
    public void RoadBuildingProgressPlacesOneRoadAndOneShipWithoutPayment()
    {
        var b=New().GetPlayerView("P1").Board;var j=Junction(b);
        var g=Fixture(s=>{s.Settlements=new[]{Piece(j.vertex)};Player(s).ProgressCards=new[]{ProgressCardKind.RoadBuilding};});
        Accepted(g,Play(ProgressCardKind.RoadBuilding));
        Accepted(g,Cmd(CommandKind.ResolveProgressChoice,target:j.road.Id));var ship=Cmd(CommandKind.ResolveProgressChoice,target:j.ship.Id);ship.BuildShip=true;Accepted(g,ship);
        var s=g.GetAuthoritativeStateForTesting();Assert.Single(s.Roads);Assert.Single(s.Ships);Assert.Equal(0,Player(s).Resources.Total);Assert.Equal(GamePhase.Action,s.Phase);
    }

    [Fact]
    public void SetupSaveAndRandomContinuationAreExactAndTamperingIsRejected()
    {
        var g=New();CompleteSetup(g);var save=g.Save();var restored=CombinedGameSession.Load(save);Assert.Equal(save,restored.Save());
        var roll=Cmd(CommandKind.RollDice,g.GetPlayerView("P1").ActivePlayerId);Accepted(g,roll);Accepted(restored,roll);Assert.Equal(g.Save(),restored.Save());
        foreach(var field in new[]{"Random","ScenarioId","RulesVersion","Ships"})
        {
            var json=JsonNode.Parse(save)!;
            if(field=="Random")json[field]!["State"]=987u;else if(field=="Ships")json["Players"]![0]!["ShipsRemaining"]=14;else json[field]="wrong";
            Assert.ThrowsAny<Exception>(()=>CombinedGameSession.Load(json.ToJsonString()));
        }
    }

    [Fact]
    public void ViewAndEventsHaveNoPrivateDeckRandomOrOpponentHand()
    {
        var g=Fixture(s=>{Player(s,"P2").Resources=Bag(ore:2);Player(s,"P2").Commodities.Coin=3;});
        var v=g.GetPlayerView("P1");Assert.Equal(5,v.Players.Single(p=>p.Id=="P2").ResourceCount);
        foreach(var field in new[]{"Random","ProgressDecks","ProcessedCommands"})Assert.Null(typeof(PlayerView).GetProperty(field));
        foreach(var field in new[]{"Resources","Commodities","ProgressCards"})Assert.Null(typeof(PublicPlayer).GetProperty(field));
        var save=g.Save();v.Board.Tiles[0].Number=99;v.OwnCommodities.Coin=99;Assert.Equal(save,g.Save());
    }

    [Fact]
    public void ShipPreviewAndDuplicateAreAtomicAndConflictingRetryRejected()
    {
        var b=New().GetPlayerView("P1").Board;var j=Junction(b);
        var g=Fixture(s=>{s.Settlements=new[]{Piece(j.vertex)};Player(s).Resources=Bag(wood:1,wool:1);});
        var c=Cmd(CommandKind.BuildShip,target:j.ship.Id);var before=g.Save();Assert.True(g.Preview(c).Success);Assert.Equal(before,g.Save());Accepted(g,c);
        var after=g.Save();Assert.True(g.Execute(c).IsDuplicate);Assert.Equal(after,g.Save());c.TargetId=j.road.Id;Rejected(g,c);
    }

    [Fact]
    public void FirstAttackCountsRemoteCitiesAndSeaKnightsBeforeProduction()
    {
        var b=New().GetPlayerView("P1").Board;
        var gold=b.Tiles.First(t=>t.Resource=="gold"); var main=b.Tiles.First(t=>t.Resource=="wheat");
        var seaVertex=b.Vertices.First(v=>b.Tiles.Where(t=>t.Vertices.Contains(v.Id)).All(t=>t.Resource=="sea"));
        var edge=b.Edges.First(e=>e.Vertices.Contains(seaVertex.Id));
        var g=Fixture(s=>{s.Cities=new[]{Piece(main.Vertices[0]),Piece(gold.Vertices[0],"P2")};s.Ships=new[]{Ship(edge.Id)};s.Knights=new[]{Knight(seaVertex.Id,level:2)};s.BarbarianPosition=6;});
        var s=g.GetAuthoritativeStateForTesting();s.BarbarianPosition=7;Invoke(g,"BarbarianAttack",s);Invoke(g,"ContinueDecisions",s);SetState(g,s);
        Assert.True(s.FirstBarbarianAttack);Assert.NotNull(s.PirateTileId);Assert.NotNull(s.RobberTileId);
        Assert.Equal(2,s.Cities.Length);Assert.Equal(1,Player(s).DefenderPoints);Assert.False(s.Knights.Single().Active);Assert.Equal(0,s.BarbarianPosition);
    }

    [Fact]
    public void PirateCannotMoveOrBeChasedBeforeFirstAttack()
    {
        var b=New().GetPlayerView("P1").Board;var sea=b.Tiles.First(t=>t.Resource=="sea");
        var g=Fixture(s=>s.Knights=new[]{Knight(sea.Vertices[0])});
        Rejected(g,Cmd(CommandKind.ChasePirate,target:sea.Vertices[0]));
        Rejected(g,Cmd(CommandKind.MovePirate,target:sea.Id));
        var s=g.GetAuthoritativeStateForTesting();s.LastDice1=3;s.LastDice2=4;Invoke(g,"ResolveProduction",s);SetState(g,s);
        Assert.Equal(GamePhase.Action,g.GetPlayerView("P1").Phase);Assert.Null(g.GetPlayerView("P1").Board.PirateTileId);
    }

    [Fact]
    public void PirateChaseDeactivatesKnightAndCannotSwitchToRobber()
    {
        var b=New().GetPlayerView("P1").Board;var seas=b.Tiles.Where(t=>t.Resource=="sea").Take(2).ToArray();
        var g=Fixture(s=>{s.FirstBarbarianAttack=true;s.PirateTileId=seas[0].Id;s.RobberTileId=b.Tiles.First(t=>t.Resource=="desert").Id;s.Knights=new[]{Knight(seas[0].Vertices[0])};});
        Accepted(g,Cmd(CommandKind.ChasePirate,target:seas[0].Vertices[0]));
        Assert.False(g.GetPlayerView("P1").Board.Knights.Single().Active);
        Rejected(g,Cmd(CommandKind.MoveRobber,target:b.Tiles.First(t=>t.Resource=="gold").Id));
        Accepted(g,Cmd(CommandKind.MovePirate,target:seas[1].Id));Assert.Equal(seas[1].Id,g.GetPlayerView("P1").Board.PirateTileId);
    }

    [Fact]
    public void DiplomacyShipBesidePirateMayRelocateButOnlyAsShip()
    {
        var b=New().GetPlayerView("P1").Board;var chain=ShipChain(b,2);
        var pirate=b.Tiles.First(t=>t.Resource=="sea"&&chain.edges[1].Vertices.All(t.Vertices.Contains));
        var g=Fixture(s=>{s.Settlements=new[]{Piece(chain.vertices[0])};s.Ships=chain.edges.Select(e=>Ship(e.Id)).ToArray();s.FirstBarbarianAttack=true;s.PirateTileId=pirate.Id;s.RobberTileId=b.Tiles.First(t=>t.Resource=="desert").Id;Player(s).ProgressCards=new[]{ProgressCardKind.Diplomacy};});
        Assert.DoesNotContain(chain.edges[1].Id,g.GetPlayerView("P1").MovableShipEdgeIds);
        Accepted(g,Play(ProgressCardKind.Diplomacy,chain.edges[1].Id));
        Rejected(g,Cmd(CommandKind.ResolveProgressChoice,target:Junction(b).road.Id));
        var view=g.GetPlayerView("P1");var place=view.LegalActions.First(c=>c.Kind==CommandKind.ResolveProgressChoice&&c.BuildShip);place.Id=Guid.NewGuid().ToString();
        Accepted(g,place);
        Assert.Equal(2,g.GetPlayerView("P1").Board.Ships.Length);Assert.Equal(0,g.GetPlayerView("P1").OwnResources.Total);
    }

    [Fact]
    public void CombinedVictoryRequiresSixteenRatherThanSingleExpansionThreshold()
    {
        var b=New().GetPlayerView("P1").Board;var vertex=b.Tiles.First(t=>t.Resource=="wheat").Vertices[0];
        var g=Fixture(s=>{s.Cities=new[]{Piece(vertex)};Player(s).DefenderPoints=13;});
        Assert.Equal(15,g.GetPlayerView("P1").OwnVictoryPoints);Rejected(g,Cmd(CommandKind.DeclareVictory));
        var s=g.GetAuthoritativeStateForTesting();Player(s).DefenderPoints=14;SetState(g,s);Accepted(g,Cmd(CommandKind.DeclareVictory));
        Assert.Equal(GamePhase.Finished,g.GetPlayerView("P1").Phase);Assert.Equal("P1",g.GetPlayerView("P1").WinnerPlayerId);
    }

    [Fact]
    public void IntrigueUsesShipConnectionToDisplaceAnEnemyKnight()
    {
        var b=New().GetPlayerView("P1").Board;var edge=ShipChain(b,1).edges[0];
        var g=Fixture(s=>{s.Ships=new[]{Ship(edge.Id)};s.Knights=new[]{Knight(edge.Vertices[1],"P2",3)};Player(s).ProgressCards=new[]{ProgressCardKind.Intrigue};});
        Accepted(g,Play(ProgressCardKind.Intrigue,edge.Vertices[1]));Assert.Empty(g.GetPlayerView("P1").Board.Knights);
    }

    [Fact]
    public void TreasonAllowsReplacementKnightAlongShipsAndRequiresVictimChoice()
    {
        var b=New().GetPlayerView("P1").Board;var edge=ShipChain(b,1).edges[0];
        var g=Fixture(s=>{s.Ships=new[]{Ship(edge.Id)};s.Knights=new[]{Knight(edge.Vertices[1],"P2",3)};Player(s).ProgressCards=new[]{ProgressCardKind.Treason};});
        var card=Play(ProgressCardKind.Treason);card.OtherPlayerId="P2";Accepted(g,card);
        Rejected(g,Cmd(CommandKind.ResolveProgressChoice,target:edge.Vertices[1]));
        Accepted(g,Cmd(CommandKind.ResolveProgressChoice,"P2",edge.Vertices[1]));
        var replacement=Cmd(CommandKind.ResolveProgressChoice,target:edge.Vertices[0]);replacement.KnightLevel=3;Accepted(g,replacement);
        var knight=Assert.Single(g.GetPlayerView("P1").Board.Knights);Assert.Equal("P1",knight.PlayerId);Assert.Equal(3,knight.Level);Assert.True(knight.Active);
    }

    [Fact]
    public void InventionCanSwapUnprotectedGoldNumberWithoutChangingResourceKind()
    {
        var b=New().GetPlayerView("P1").Board;var gold=b.Tiles.First(t=>t.Resource=="gold"&&t.Number!=2&&t.Number!=6&&t.Number!=8&&t.Number!=12);
        var other=b.Tiles.First(t=>t.Resource!="gold"&&t.Number.HasValue&&t.Number!=gold.Number&&t.Number!=2&&t.Number!=6&&t.Number!=8&&t.Number!=12);
        var g=Fixture(s=>Player(s).ProgressCards=new[]{ProgressCardKind.Invention});var c=Play(ProgressCardKind.Invention,other.Id);c.SourceId=gold.Id;Accepted(g,c);
        var changed=g.GetPlayerView("P1").Board.Tiles.Single(t=>t.Id==gold.Id);Assert.Equal("gold",changed.Resource);Assert.Equal(other.Number,changed.Number);
    }

    [Fact]
    public void PirateVictimsComeFromShipsAndTheftMayTakeCommodityWithoutPublicDisclosure()
    {
        var b=New().GetPlayerView("P1").Board;var edge=ShipChain(b,1).edges[0];var sea=b.Tiles.First(t=>t.Resource=="sea"&&edge.Vertices.All(t.Vertices.Contains));
        var coast=sea.Vertices.First(v=>b.Tiles.Any(t=>t.Resource!="sea"&&t.Vertices.Contains(v)));
        var g=Fixture(s=>{s.FirstBarbarianAttack=true;s.PirateTileId="frame";s.RobberTileId=b.Tiles.First(t=>t.Resource=="desert").Id;s.Ships=new[]{Ship(edge.Id,"P2")};s.Settlements=new[]{Piece(coast,"P3")};Player(s,"P2").Commodities.Coin=1;Player(s,"P3").Resources=Bag(ore:1);s.Phase=GamePhase.RobberMove;s.PendingDecision=new Catan.Core.M5.PendingDecision{Kind="Robber",PlayerId="P1",ReturnPhase=GamePhase.Action};});
        Accepted(g,Cmd(CommandKind.MovePirate,target:sea.Id));Assert.Equal(new[]{"P2"},g.GetPlayerView("P1").PendingDecision.EligibleVictimIds);
        var theft=Cmd(CommandKind.StealResource);theft.OtherPlayerId="P2";Accepted(g,theft);
        Assert.Equal(1,g.GetPlayerView("P1").OwnCommodities.Coin);Assert.Equal(0,g.GetPlayerView("P2").OwnCommodities.Coin);
        Assert.DoesNotContain("Coin",g.GetPlayerView("P3").Events.Last().Message);Assert.Equal(1,g.GetPlayerView("P3").OwnResources.Ore);
    }

    internal static ShipPlacement Ship(string id,string player="P1")=>new(){LocationId=id,PlayerId=player,BuiltTurn=1};
    internal static Knight Knight(string id,string player="P1",int level=1)=>new(){LocationId=id,PlayerId=player,Level=level,Active=true,ActivatedTurn=1};
    internal static Command Play(ProgressCardKind card,string? target=null){var c=Cmd(CommandKind.PlayProgressCard,target:target);c.ProgressCard=card;return c;}
    internal static bool Sea(Catan.Core.M5.BoardView b,Edge e)=>b.Tiles.Any(t=>t.Resource=="sea"&&e.Vertices.All(t.Vertices.Contains));
    internal static (string vertex,Edge road,Edge ship) Junction(Catan.Core.M5.BoardView b)
    {
        foreach(var v in b.Vertices)
        {
            var edges=b.Edges.Where(e=>e.Vertices.Contains(v.Id)).ToArray();
            var road=edges.FirstOrDefault(e=>b.Tiles.Any(t=>t.Resource!="sea"&&e.Vertices.All(t.Vertices.Contains)));
            var ship=edges.FirstOrDefault(e=>e!=road&&Sea(b,e));
            if(road!=null&&ship!=null)return(v.Id,road,ship);
        }
        throw new InvalidOperationException("No coast junction.");
    }
    internal static (Edge[] edges,string[] vertices) ShipChain(Catan.Core.M5.BoardView b,int length)
    {
        foreach(var start in b.Vertices.Where(v=>b.Tiles.Any(t=>t.Resource!="sea"&&t.Vertices.Contains(v.Id))))
        {
            var result=Search(start.Id,new List<Edge>(),new List<string>{start.Id});if(result!=null)return(result.Value.edges,result.Value.vertices);
        }
        throw new InvalidOperationException("No sea chain.");
        (Edge[] edges,string[] vertices)? Search(string current,List<Edge> path,List<string> visited)
        {
            if(path.Count==length)return(path.ToArray(),visited.ToArray());
            foreach(var e in b.Edges.Where(e=>e.Vertices.Contains(current)&&Sea(b,e)))
            {
                var next=e.Vertices.Single(v=>v!=current);if(visited.Contains(next))continue;
                var result=Search(next,path.Append(e).ToList(),visited.Append(next).ToList());if(result!=null)return result;
            }
            return null;
        }
    }
}
