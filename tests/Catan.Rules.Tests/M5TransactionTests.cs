using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using Catan.Core;
using Catan.Core.M5;
using Xunit;
using static Catan.Rules.Tests.M5Harness;
using static Catan.Rules.Tests.M5CombinationTests;
using CommandKind = Catan.Core.M5.CommandKind;
using GamePhase = Catan.Core.M5.GamePhase;
using GameState = Catan.Core.M5.GameState;
using PendingDecision = Catan.Core.M5.PendingDecision;
using TradeOffer = Catan.Core.M5.TradeOffer;

namespace Catan.Rules.Tests;

public sealed class M5TransactionTests
{
    [Fact]
    public void TransactionCopyMatchesSerializationAndDetachesAllMutableAuthorityBranches()
    {
        // Populate optional zones so a forgotten nested array or bag cannot hide
        // behind an empty default. This tests copying, not a legal game fixture.
        var state=New().GetAuthoritativeStateForTesting();
        var p=Player(state);var vertex=state.Tiles[0].Vertices[0];
        p.Resources=Bag(1,2,3,4,5);p.Commodities=new CommodityBag {Cloth=1,Coin=2,Paper=3};
        p.Improvements=new[]{1,2,3};p.HomeRegions=new[]{"home"};p.SettledRegions=new[]{"home","overseas"};
        p.ProgressCards=new[]{ProgressCardKind.Invention};p.ProgressVictoryCards=new[]{ProgressCardKind.Printing};
        p.DevelopmentCards=new[]{new Catan.Core.M5.DevelopmentCard {Kind=DevelopmentCardKind.Knight,BoughtTurn=1}};
        state.Settlements=new[]{Piece(vertex)};state.Cities=new[]{Piece(vertex)};state.Roads=new[]{Piece("edge")};
        state.Walls=new[]{Piece(vertex)};state.DowngradedCities=new[]{Piece(vertex)};
        state.Ships=new[]{Ship("ship")};state.Knights=new[]{Knight(vertex)};
        state.Metropolises=new[]{new Metropolis {PlayerId="P1",LocationId=vertex,Track=ImprovementTrack.Science}};
        state.PendingDecision=new PendingDecision {Kind="DisplacedKnight",PlayerId="P1",Options=new[]{vertex},EligibleVictimIds=new[]{"P2"},DisplacedKnight=Knight(vertex)};
        state.DecisionQueue=new[]{new PendingDecision {Kind="Gold",PlayerId="P2",Amount=2,Options=new[]{vertex},DisplacedKnight=Knight(vertex,"P2")}};
        state.Discards=new[]{new Catan.Core.M5.DiscardRequirement {PlayerId="P1",Amount=3}};
        state.TradeOffer=new TradeOffer {ProposerId="P1",OtherPlayerId="P2",Give=Bag(wood:1),Receive=Bag(ore:1),GiveCommodities=new CommodityBag{Cloth=1},ReceiveCommodities=new CommodityBag{Coin=1}};
        state.DevelopmentDeck=new[]{DevelopmentCardKind.Knight};state.PlayedDevelopmentCards=new[]{DevelopmentCardKind.Monopoly};
        state.MerchantFleetResources=new[]{Resource.Wood};state.MerchantFleetCommodities=new[]{Commodity.Paper};state.CommercialHarborPlayers=new[]{"P2"};
        var serialized=Serialize(state);var copy=Copy(state);
        Assert.Equal(serialized,Serialize(copy));
        AssertDetachedGraph(state,copy,"authority");
        copy.PendingDecision.DisplacedKnight.LocationId="changed";copy.DecisionQueue[0].Options[0]="changed";
        copy.Players[0].Resources.Ore=0;copy.Players[0].Commodities.Coin=0;copy.Players[0].Improvements[0]=5;
        copy.ProgressDecks[0].Cards[0]=ProgressCardKind.Merchant;copy.Tiles[0].Vertices[0]="changed";
        copy.TradeOffer.Give.Wood=0;copy.TradeOffer.ReceiveCommodities.Coin=0;copy.Random.State=999;
        Assert.Equal(serialized,Serialize(state));
    }

    [Fact]
    public void SuccessfulInventionPreviewDoesNotChangeNumbersCardsOrDeckOrder()
    {
        var game=Fixture(s=>Player(s).ProgressCards=new[]{ProgressCardKind.Invention});
        var tiles=game.GetPlayerView("P1").Board.Tiles.Where(t=>t.Number.HasValue&&t.Number!=2&&t.Number!=6&&t.Number!=8&&t.Number!=12).GroupBy(t=>t.Number).Take(2).Select(g=>g.First()).ToArray();
        var command=Play(ProgressCardKind.Invention,tiles[1].Id);command.SourceId=tiles[0].Id;
        var before=game.Save();Assert.True(game.Preview(command).Success);Assert.Equal(before,game.Save());
        Accepted(game,command);
        Assert.Equal(tiles[1].Number,game.GetPlayerView("P1").Board.Tiles.Single(t=>t.Id==tiles[0].Id).Number);
        Assert.Empty(game.GetPlayerView("P1").OwnProgressCards);
    }

    [Fact]
    public void SmithingFailureAfterFirstPromotionRollsBackKnightAndReturnedCard()
    {
        var vertex=New().GetPlayerView("P1").Board.Vertices[0].Id;
        var game=Fixture(s=>{s.Knights=new[]{Knight(vertex)};Player(s).ProgressCards=new[]{ProgressCardKind.Smithing};});
        var command=Play(ProgressCardKind.Smithing);command.TargetIds=new[]{vertex,"unknown-knight"};
        var before=game.Save();Assert.False(game.Preview(command).Success);Assert.Equal(before,game.Save());
        Rejected(game,command);
        Assert.Equal(1,game.GetPlayerView("P1").Board.Knights.Single().Level);
        Assert.Equal(new[]{ProgressCardKind.Smithing},game.GetPlayerView("P1").OwnProgressCards);
    }

    [Fact]
    public void DisplacedKnightPreviewDoesNotMutatePendingKnightOrAdvanceQueue()
    {
        var board=New().GetPlayerView("P1").Board;var destination=board.Vertices[1].Id;
        var game=Fixture(s=>
        {
            s.Phase=GamePhase.PendingChoice;
            s.PendingDecision=new PendingDecision {Kind="DisplacedKnight",PlayerId="P2",DisplacedKnight=Knight(board.Vertices[0].Id,"P2"),Options=new[]{destination}};
            s.DecisionQueue=new[]{new PendingDecision {Kind="Aqueduct",PlayerId="P1"}};
        });
        var command=Cmd(CommandKind.ResolveChoice,"P2",destination);
        var before=game.Save();Assert.True(game.Preview(command).Success);Assert.Equal(before,game.Save());
        Accepted(game,command);
        Assert.Equal(destination,game.GetPlayerView("P1").Board.Knights.Single().LocationId);
        Assert.Equal("Aqueduct",game.GetPlayerView("P1").PendingDecision.Kind);
    }

    [Fact]
    public void GoldPreviewKeepsBankHandAndFollowingDecisionUntilCommit()
    {
        var game=Fixture(s=>
        {
            s.Cities=new[]{Piece(s.Tiles.First(t=>t.Resource=="gold").Vertices[0])};
            s.Phase=GamePhase.PendingChoice;s.PendingDecision=new PendingDecision {Kind="Gold",PlayerId="P1",Amount=2};
            s.DecisionQueue=new[]{new PendingDecision {Kind="Aqueduct",PlayerId="P2"}};
        });
        var command=Cmd(CommandKind.ChooseGoldResources);command.Resources=Bag(ore:2);
        var before=game.Save();Assert.True(game.Preview(command).Success);Assert.Equal(before,game.Save());
        Accepted(game,command);
        Assert.Equal(2,game.GetPlayerView("P1").OwnResources.Ore);Assert.Equal(17,game.GetPlayerView("P1").Bank.Ore);
        Assert.Equal("P2",game.GetPlayerView("P1").PendingDecision.PlayerId);
    }

    private static GameState Copy(GameState state) => (GameState)typeof(GameState).GetMethod("CopyForTransaction",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(state,null)!;
    private static string Serialize(GameState state)
    {
        using var stream=new MemoryStream();new DataContractJsonSerializer(typeof(GameState)).WriteObject(stream,state);return Encoding.UTF8.GetString(stream.ToArray());
    }
    private static void AssertDetachedGraph(object? source,object? copy,string path)
    {
        if(source==null){Assert.Null(copy);return;}
        Assert.NotNull(copy);var type=source.GetType();
        if(type.IsValueType||source is string)return;
        if(source is Array array)
        {
            var copied=(Array)copy!;Assert.Equal(array.Length,copied.Length);
            if(array.Length>0)Assert.False(ReferenceEquals(source,copy),path);
            for(int i=0;i<array.Length;i++)AssertDetachedGraph(array.GetValue(i),copied.GetValue(i),path+"["+i+"]");
            return;
        }
        Assert.False(ReferenceEquals(source,copy),path);
        foreach(var property in type.GetProperties().Where(p=>p.GetCustomAttribute<DataMemberAttribute>()!=null))
        {
            // The core contract treats these two ledgers as append-only. Every
            // other serialized mutable reference must be detached automatically.
            if(source is GameState && (property.Name=="ProcessedCommands"||property.Name=="Events"))continue;
            AssertDetachedGraph(property.GetValue(source),property.GetValue(copy),path+"."+property.Name);
        }
    }
}
