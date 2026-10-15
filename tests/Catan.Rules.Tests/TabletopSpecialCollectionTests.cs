using Catan.Core;
using Xunit;
using M2=Catan.Core.M2;
using M4=Catan.Core.M4;

namespace Catan.Rules.Tests;

public sealed class TabletopSpecialCollectionTests
{
    [Fact]
    public void ResourceAndCommodityFromTheSameCityStartAtTheirProducingTile()
    {
        var tile=M4CoreHarness.New().GetPlayerView("P1").Board.Tiles.First(t=>t.Resource=="wood");
        var game=M4CoreHarness.Fixture(s=>
        {
            s.Phase=M4.GamePhase.ProductionAwaitRoll;
            s.AlchemyDice1=Math.Min(6,tile.Number!.Value-1);s.AlchemyDice2=tile.Number.Value-s.AlchemyDice1;
            s.Cities=[M4CoreHarness.Piece(tile.Vertices[0],"P2")];
        });
        var timeline=Baseline("P1",game.GetHistory("P1"));
        M4CoreHarness.Accepted(game,M4CoreHarness.Cmd(M4.CommandKind.RollDice));
        var board=game.GetPlayerView("P1").Board;
        timeline.Observe("P1",game.GetHistory("P1"),10,TabletopCollectionSources.Project(board.Tiles,board.Settlements,board.Cities,board.RobberTileId,true));
        var flights=timeline.Active(11.6).Where(f=>!f.IsSpecial).ToArray();
        Assert.Equal(2,flights.Length);
        Assert.All(flights,f=>Assert.Equal(tile.Id,f.SourceTileId));
        Assert.Equal(new[]{0,7},flights.Select(f=>f.Good));
    }

    [Fact]
    public void PublicSourceProjectionExcludesFogAndKeepsRobberBlockingSeparateFromCardEffects()
    {
        Tile[] tiles=[new(){Id="forest",Resource="wood",Number=6,Vertices=["V1"]},new(){Id="hidden",Resource="fog",Vertices=["V2"]}];
        var sources=TabletopCollectionSources.Project(tiles,[],[new(){PlayerId="P1",LocationId="V1"}],"forest",true);
        var source=Assert.Single(sources);
        Assert.Equal("forest",source.Id);Assert.True(source.Blocked);
        Assert.Equal(1,source.Yield("P1",0));Assert.Equal(1,source.Yield("P1",7));
        Assert.Equal(0,source.Yield("P2",0));Assert.Equal("fog",tiles[1].Resource);
    }

    [Fact]
    public void BankTradeKeepsItsBankOriginEvenWithAMatchingPublicTerrain()
    {
        var game=M2Harness.Fixture(s=>M2Harness.Player(s).Resources.Wood=4);
        var timeline=Baseline("P1",game.GetHistory("P1"));
        var trade=M2Harness.Cmd(M2.CommandKind.BankTrade);trade.GiveResource=Resource.Wood;trade.GiveAmount=4;trade.ReceiveResource=Resource.Ore;trade.ReceiveAmount=1;
        M2Harness.Accepted(game,trade);var board=game.GetPlayerView("P1").Board;
        timeline.Observe("P1",game.GetHistory("P1"),10,TabletopCollectionSources.Project(board.Tiles,board.Settlements,board.Cities,board.RobberTileId,false));
        Assert.Null(Assert.Single(timeline.Active(10.5)).SourceTileId);
    }

    [Fact]
    public void MergedRollAndKnightRobberMoveUseThePublicRobberPositionAtProductionTime()
    {
        var initial=M4CoreHarness.New().GetPlayerView("P1").Board;
        var tile=initial.Tiles.First(t=>t.Resource=="wood");var desert=initial.Tiles.Single(t=>t.Resource=="desert");
        string knightVertex=desert.Vertices.First(v=>v!=tile.Vertices[0]);
        var game=M4CoreHarness.Fixture(s=>
        {
            s.Phase=M4.GamePhase.ProductionAwaitRoll;s.FirstBarbarianAttack=true;s.RobberTileId=desert.Id;
            s.AlchemyDice1=Math.Min(6,tile.Number!.Value-1);s.AlchemyDice2=tile.Number.Value-s.AlchemyDice1;
            s.Cities=[M4CoreHarness.Piece(tile.Vertices[0])];
            s.Knights=[M4ProgressHarness.Knight(knightVertex,active:true)];
        });
        var timeline=new TabletopCollectionTimeline();timeline.Observe("P1",game.GetHistory("P1"),0,Sources(game));
        M4CoreHarness.Accepted(game,M4CoreHarness.Cmd(M4.CommandKind.RollDice));
        M4CoreHarness.Accepted(game,M4CoreHarness.Cmd(M4.CommandKind.ChaseRobber,target:knightVertex));
        M4CoreHarness.Accepted(game,M4CoreHarness.Cmd(M4.CommandKind.MoveRobber,target:tile.Id));
        var finalSources=Sources(game);Assert.True(finalSources.Single(t=>t.Id==tile.Id).Blocked);
        timeline.Observe("P1",game.GetHistory("P1"),10,finalSources);
        var flights=timeline.Active(11.6).Where(f=>!f.IsSpecial).ToArray();
        Assert.Equal(new[]{0,7},flights.Select(f=>f.Good));Assert.All(flights,f=>Assert.Equal(tile.Id,f.SourceTileId));
        Assert.True(finalSources.Single(t=>t.Id==tile.Id).Blocked);
    }

    [Fact]
    public void MergedRollAndCityUpgradeDoNotApplyTheNewCityYieldToEarlierProduction()
    {
        var wheat=M4CoreHarness.New().GetPlayerView("P1").Board.Tiles.Where(t=>t.Resource=="wheat").OrderBy(t=>t.Id).Take(2).ToArray();
        string a=wheat[0].Vertices.First(v=>!wheat[1].Vertices.Contains(v)),b=wheat[1].Vertices.First(v=>!wheat[0].Vertices.Contains(v));
        var game=M4CoreHarness.Fixture(s=>
        {
            s.Phase=M4.GamePhase.ProductionAwaitRoll;s.AlchemyDice1=2;s.AlchemyDice2=3;
            foreach(var tile in s.Tiles.Where(t=>wheat.Any(w=>w.Id==t.Id)&&t.Number!=5))
            {var donor=s.Tiles.First(t=>t.Number==5&&!wheat.Any(w=>w.Id==t.Id));var number=tile.Number;tile.Number=5;donor.Number=number;}
            s.Settlements=[M4CoreHarness.Piece(a),M4CoreHarness.Piece(b)];
            M4CoreHarness.Player(s).Resources.Wheat=2;M4CoreHarness.Player(s).Resources.Ore=3;
        });
        var timeline=new TabletopCollectionTimeline();timeline.Observe("P1",game.GetHistory("P1"),0,Sources(game));
        M4CoreHarness.Accepted(game,M4CoreHarness.Cmd(M4.CommandKind.RollDice));
        M4CoreHarness.Accepted(game,M4CoreHarness.Cmd(M4.CommandKind.BuildCity,target:a));
        timeline.Observe("P1",game.GetHistory("P1"),10,Sources(game));
        var flights=timeline.Active(11.6).Where(f=>f.Good==3).ToArray();
        Assert.Equal(new[]{wheat[0].Id,wheat[1].Id},flights.Select(f=>f.SourceTileId));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void AMergedNumberSwapKeepsProductionOnTheTileNumberedAtTheTimeOfTheRoll(bool swapBeforeRoll)
    {
        var board=M4CoreHarness.New().GetPlayerView("P1").Board;
        int[] swappable=[3,4,5,9,10,11];
        var tile=board.Tiles.First(t=>t.Number.HasValue&&swappable.Contains(t.Number.Value));
        var other=board.Tiles.First(t=>t.Number.HasValue&&swappable.Contains(t.Number.Value)&&t.Number!=tile.Number);
        int total=(swapBeforeRoll?other.Number:tile.Number)!.Value;
        var game=M4CoreHarness.Fixture(s=>
        {
            s.Phase=swapBeforeRoll?M4.GamePhase.Action:M4.GamePhase.ProductionAwaitRoll;
            if(!swapBeforeRoll){s.AlchemyDice1=Math.Min(6,total-1);s.AlchemyDice2=total-s.AlchemyDice1;}
            s.Cities=[M4CoreHarness.Piece(tile.Vertices.First(v=>!other.Vertices.Contains(v)))];
            M4CoreHarness.Player(s).ProgressCards=[M4.ProgressCardKind.Invention];
            if(swapBeforeRoll)M4CoreHarness.Player(s,"P2").ProgressCards=[M4.ProgressCardKind.Alchemy];
        });
        var timeline=new TabletopCollectionTimeline();timeline.Observe("P1",game.GetHistory("P1"),0,Sources(game));
        var swap=M4CoreHarness.Cmd(M4.CommandKind.PlayProgressCard,target:other.Id);swap.SourceId=tile.Id;swap.ProgressCard=M4.ProgressCardKind.Invention;
        if(swapBeforeRoll)
        {
            M4CoreHarness.Accepted(game,swap);M4CoreHarness.Accepted(game,M4CoreHarness.Cmd(M4.CommandKind.EndTurn));
            var alchemy=M4CoreHarness.Cmd(M4.CommandKind.PlayProgressCard,"P2");alchemy.ProgressCard=M4.ProgressCardKind.Alchemy;
            alchemy.ChosenDice1=Math.Min(6,total-1);alchemy.ChosenDice2=total-alchemy.ChosenDice1;M4CoreHarness.Accepted(game,alchemy);
        }
        M4CoreHarness.Accepted(game,M4CoreHarness.Cmd(M4.CommandKind.RollDice,swapBeforeRoll?"P2":"P1"));
        if(!swapBeforeRoll)M4CoreHarness.Accepted(game,swap);
        timeline.Observe("P1",game.GetHistory("P1"),10,Sources(game));
        var flights=timeline.Active(11.6).Where(f=>!f.IsSpecial).ToArray();
        Assert.NotEmpty(flights);Assert.All(flights,f=>Assert.Equal(tile.Id,f.SourceTileId));
    }

    [Fact]
    public void BuyingDevelopmentCardAnimatesItsAuthorizedIdentityOnlyForTheOwner()
    {
        var game=PurchaseFixture();var owner=Baseline("P1",game.GetHistory("P1"));var observer=Baseline("P2",game.GetHistory("P2"));
        M2Harness.Accepted(game,M2Harness.Cmd(M2.CommandKind.BuyDevelopmentCard));
        owner.Observe("P1",game.GetHistory("P1"),10);observer.Observe("P2",game.GetHistory("P2"),10);
        var visible=Assert.Single(owner.Active(10.5));var concealed=Assert.Single(observer.Active(10.5));
        Assert.True(visible.IsSpecial);Assert.Equal("development",visible.Category);Assert.Equal("骑士",visible.CardName);
        Assert.Equal("发展卡",concealed.CardName);Assert.Equal(-1,concealed.ProgressIndex);Assert.Null(concealed.CardId);
        Assert.Single(game.GetPlayerView("P1").OwnDevelopmentCards);
        owner.Observe("P1",game.GetHistory("P1"),10.6);Assert.Same(visible,Assert.Single(owner.Active(10.6)));
    }

    [Fact]
    public void NewDevelopmentCardMakesRoomNearArrivalAndNeverAppearsTwice()
    {
        var game=PurchaseFixture();var timeline=Baseline("P1",game.GetHistory("P1"));
        M2Harness.Accepted(game,M2Harness.Cmd(M2.CommandKind.BuyDevelopmentCard));timeline.Observe("P1",game.GetHistory("P1"),10);
        OperationHandCard[] cards=[new(){Id="0:0",Name="骑士"}];
        var early=Assert.Single(timeline.SpecialSlots("P1",cards,10.5));Assert.NotNull(early.Flight);Assert.Equal(0,early.Weight);
        var near=Assert.Single(timeline.SpecialSlots("P1",cards,10.9));Assert.NotNull(near.Flight);Assert.InRange(near.Weight,.01,.99);
        var landed=Assert.Single(timeline.SpecialSlots("P1",cards,11.051));Assert.Null(landed.Flight);Assert.Equal(1,landed.Weight);
        Assert.Single(cards);Assert.Single(game.GetPlayerView("P1").OwnDevelopmentCards);
    }

    [Fact]
    public void RemovedIncomingSpecialCardCancelsItsFlightWithoutRestoringIt()
    {
        var game=PurchaseFixture();var timeline=Baseline("P1",game.GetHistory("P1"));
        M2Harness.Accepted(game,M2Harness.Cmd(M2.CommandKind.BuyDevelopmentCard));timeline.Observe("P1",game.GetHistory("P1"),10);
        Assert.NotNull(Assert.Single(timeline.SpecialSlots("P1",[new(){Id="0:0",Name="骑士"}],10.1)).Flight);
        Assert.Empty(timeline.SpecialSlots("P1",[],10.2));Assert.Empty(timeline.Active(10.3));
    }

    [Fact]
    public void PlayingAnOlderKnightRebindsTheBoughtKnightWithoutLandingItOrRestartingItsFlight()
    {
        var game=M2Harness.Fixture(s=>
        {
            M2Harness.Card(s,M2.DevelopmentCardKind.Knight);
            var p=M2Harness.Player(s);p.Resources.Wool=1;p.Resources.Wheat=1;p.Resources.Ore=1;
        });
        var timeline=Baseline("P1",game.GetHistory("P1"));
        Assert.Null(Assert.Single(timeline.SpecialSlots("P1",KnightCards(game),9)).Flight);
        M2Harness.Accepted(game,M2Harness.Cmd(M2.CommandKind.BuyDevelopmentCard));timeline.Observe("P1",game.GetHistory("P1"),10);
        var before=timeline.SpecialSlots("P1",KnightCards(game),10.1);
        Assert.Null(before[0].Flight);var flight=Assert.IsType<TabletopCollectionFlight>(before[1].Flight);
        Assert.Equal("0:1",flight.CardId);Assert.Equal(10,flight.Started);
        M2Harness.Accepted(game,M2Harness.Cmd(M2.CommandKind.PlayKnight));timeline.Observe("P1",game.GetHistory("P1"),10.25);
        string saved=game.Save();var remaining=Assert.Single(timeline.SpecialSlots("P1",KnightCards(game),10.3));
        Assert.Same(flight,remaining.Flight);Assert.Equal("0:0",flight.CardId);Assert.Equal(0,remaining.Weight);
        Assert.Equal(10,flight.Started);Assert.Same(flight,Assert.Single(timeline.Active(10.4)));
        Assert.Null(Assert.Single(timeline.SpecialSlots("P1",KnightCards(game),11.051)).Flight);
        Assert.Empty(timeline.Active(11.1));Assert.Equal(saved,game.Save());
    }

    [Fact]
    public void TwoIncomingKnightsKeepTheirOrderAndArrivalTimesWhenAnOlderKnightIsPlayed()
    {
        var game=M2Harness.Fixture(s=>
        {
            M2Harness.Card(s,M2.DevelopmentCardKind.Knight);
            var p=M2Harness.Player(s);p.Resources.Wool=2;p.Resources.Wheat=2;p.Resources.Ore=2;
        });
        var timeline=Baseline("P1",game.GetHistory("P1"));
        timeline.SpecialSlots("P1",KnightCards(game),9);
        M2Harness.Accepted(game,M2Harness.Cmd(M2.CommandKind.BuyDevelopmentCard));timeline.Observe("P1",game.GetHistory("P1"),10);
        timeline.SpecialSlots("P1",KnightCards(game),10.01);
        M2Harness.Accepted(game,M2Harness.Cmd(M2.CommandKind.BuyDevelopmentCard));timeline.Observe("P1",game.GetHistory("P1"),10.1);
        var incoming=timeline.SpecialSlots("P1",KnightCards(game),10.2).Skip(1).Select(slot=>slot.Flight).ToArray();
        Assert.Equal(2,incoming.Length);Assert.Equal(new[]{10d,10.1},incoming.Select(f=>f!.Started));
        M2Harness.Accepted(game,M2Harness.Cmd(M2.CommandKind.PlayKnight));timeline.Observe("P1",game.GetHistory("P1"),10.25);
        var renamed=timeline.SpecialSlots("P1",KnightCards(game),10.3);
        Assert.Equal(incoming,renamed.Select(slot=>slot.Flight));Assert.Equal(new[]{"0:0","0:1"},renamed.Select(slot=>slot.Flight!.CardId));
        var firstArrival=timeline.SpecialSlots("P1",KnightCards(game),11.051);
        Assert.Null(firstArrival[0].Flight);Assert.Same(incoming[1],firstArrival[1].Flight);
        Assert.All(timeline.SpecialSlots("P1",KnightCards(game),11.151),slot=>Assert.Null(slot.Flight));
        Assert.Equal(2,game.GetPlayerView("P1").OwnDevelopmentCards.Length);
    }

    [Fact]
    public void DefenderProgressRewardFliesFromTheCorrectTrackWithoutLeakingOpponentCard()
    {
        var game=M4CoreHarness.Fixture(s=>
        {
            s.Phase=M4.GamePhase.PendingChoice;s.DecisionReturnPhase=M4.GamePhase.Action;
            s.PendingDecision=new M4.PendingDecision{Kind="DefenderProgress",PlayerId="P1",ReturnPhase=M4.GamePhase.Action};
        });
        var owner=Baseline("P1",game.GetHistory("P1"));var observer=Baseline("P2",game.GetHistory("P2"));
        var draw=M4CoreHarness.Cmd(M4.CommandKind.ResolveChoice);draw.Track=M4.ImprovementTrack.Trade;M4CoreHarness.Accepted(game,draw);
        owner.Observe("P1",game.GetHistory("P1"),10);observer.Observe("P2",game.GetHistory("P2"),10);
        var own=Assert.Single(owner.Active(10.5));var other=Assert.Single(observer.Active(10.5));
        Assert.Equal("progress",own.Category);Assert.Equal(10,own.ProgressIndex);Assert.Equal(0,own.Track);Assert.Equal("商业港口",own.CardName);
        Assert.Equal("进步卡",other.CardName);Assert.Equal(-1,other.ProgressIndex);Assert.Equal(-1,other.Track);
        var slot=Assert.Single(owner.SpecialSlots("P1",[new(){Id="10:0",Name="商业港口",Progress=true,ArtIndex=10}],10.5));
        Assert.Same(own,slot.Flight);Assert.Equal(0,slot.Weight);
        Assert.Null(Assert.Single(owner.SpecialSlots("P1",[slot.Card],11.1)).Flight);
    }

    [Fact]
    public void InvalidDevelopmentPurchaseCannotStartAnAnimation()
    {
        var game=M2Harness.Fixture();var timeline=Baseline("P1",game.GetHistory("P1"));
        M2Harness.Rejected(game,M2Harness.Cmd(M2.CommandKind.BuyDevelopmentCard));
        timeline.Observe("P1",game.GetHistory("P1"),10);Assert.Empty(timeline.Active(10.5));Assert.Empty(timeline.SpecialSlots("P1",[],10.5));
    }

    static M2.BaseGameSession PurchaseFixture()=>M2Harness.Fixture(s=>
    {var p=M2Harness.Player(s);p.Resources.Wool=1;p.Resources.Wheat=1;p.Resources.Ore=1;});
    static OperationHandCard[] KnightCards(M2.BaseGameSession game)=>game.GetPlayerView("P1").OwnDevelopmentCards.Select((card,index)=>
    {Assert.Equal(M2.DevelopmentCardKind.Knight,card.Kind);return new OperationHandCard{Id="0:"+index,Name="骑士"};}).ToArray();
    static TabletopCollectionTile[] Sources(M4.CitiesKnightsGameSession game)
    {var board=game.GetPlayerView("P1").Board;return TabletopCollectionSources.Project(board.Tiles,board.Settlements,board.Cities,board.RobberTileId,true);}
    static TabletopCollectionTimeline Baseline(string player,GameHistoryEntry[] entries)
    {var timeline=new TabletopCollectionTimeline();timeline.Observe(player,entries,0);return timeline;}
}
