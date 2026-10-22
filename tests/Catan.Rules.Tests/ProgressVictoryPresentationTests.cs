using Catan.Core;
using Xunit;
using M4=Catan.Core.M4;
using M5=Catan.Core.M5;

namespace Catan.Rules.Tests;

// 来源：catan-cities-knights-2025-en.pdf，第6、10、14、16页。
public sealed class ProgressVictoryPresentationTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void VictoryCardRevealKeepsHarvestQueuedUntilItsNewViewAppliesAfterTheDice(bool combined)
    {
        GameHistoryEntry[] history;long appliedSequence;int[] owned;
        if(combined)
        {
            var tile=M5Harness.New().GetPlayerView("P1").Board.Tiles.First(t=>t.Resource=="wood");
            var game=M5Harness.Fixture(s=>
            {
                s.Phase=M5.GamePhase.ProductionAwaitRoll;s.Random.State=2;
                s.AlchemyDice1=Math.Max(1,tile.Number!.Value-6);s.AlchemyDice2=tile.Number.Value-s.AlchemyDice1;
                s.Cities=[M5Harness.Piece(tile.Vertices[0])];M5Harness.Player(s,"P2").Improvements[(int)M5.ImprovementTrack.Science]=5;
            });
            var state=game.GetAuthoritativeStateForTesting();var deck=state.ProgressDecks.Single(d=>d.Track==M5.ImprovementTrack.Science);
            deck.Cards=new[]{M5.ProgressCardKind.Printing}.Concat(deck.Cards.Where(c=>c!=M5.ProgressCardKind.Printing)).ToArray();M5Harness.SetState(game,state);
            M5Harness.Accepted(game,M5Harness.Cmd(M5.CommandKind.RollDice));
            var view=game.GetPlayerView("P1");history=game.GetHistory("P1");appliedSequence=view.Events.Last().Sequence;
            owned=[view.OwnResources.Wood,view.OwnResources.Brick,view.OwnResources.Wool,view.OwnResources.Wheat,view.OwnResources.Ore,view.OwnCommodities.Cloth,view.OwnCommodities.Coin,view.OwnCommodities.Paper];
        }
        else
        {
            var tile=M4CoreHarness.New().GetPlayerView("P1").Board.Tiles.First(t=>t.Resource=="wood");
            var game=M4CoreHarness.Fixture(s=>
            {
                s.Phase=M4.GamePhase.ProductionAwaitRoll;s.Random.State=2;
                s.AlchemyDice1=Math.Max(1,tile.Number!.Value-6);s.AlchemyDice2=tile.Number.Value-s.AlchemyDice1;
                s.Cities=[M4CoreHarness.Piece(tile.Vertices[0])];M4CoreHarness.Player(s,"P2").Improvements[(int)M4.ImprovementTrack.Science]=5;
            });
            var state=game.GetAuthoritativeStateForTesting();var deck=state.ProgressDecks.Single(d=>d.Track==M4.ImprovementTrack.Science);
            deck.Cards=new[]{M4.ProgressCardKind.Printing}.Concat(deck.Cards.Where(c=>c!=M4.ProgressCardKind.Printing)).ToArray();M4CoreHarness.SetState(game,state);
            M4CoreHarness.Accepted(game,M4CoreHarness.Cmd(M4.CommandKind.RollDice));
            var view=game.GetPlayerView("P1");history=game.GetHistory("P1");appliedSequence=view.Events.Last().Sequence;
            owned=[view.OwnResources.Wood,view.OwnResources.Brick,view.OwnResources.Wool,view.OwnResources.Wheat,view.OwnResources.Ore,view.OwnCommodities.Cloth,view.OwnCommodities.Coin,view.OwnCommodities.Paper];
        }
        var entry=Assert.Single(history);var reveal=Assert.Single(TabletopCardReveal.From(entry));
        Assert.Equal("P2",reveal.PlayerId);Assert.Equal("印刷术",reveal.CardName);Assert.Same(entry,reveal.Roll);
        var oldSnapshot=new OperationSnapshot{Scope="game:P1",Seat="P1",GoodCount=8,PublicSequence=0};
        var currentSnapshot=new OperationSnapshot{Scope="game:P1",Seat="P1",GoodCount=8,PublicSequence=appliedSequence,Goods=owned};
        var dice=new TabletopDicePresentation();dice.Observe(0,0,0,0,0);dice.Observe(entry.Sequence,entry.Dice1,entry.Dice2,3,10);
        var collections=new TabletopCollectionTimeline();collections.Observe(oldSnapshot.Scope,[],0);
        foreach(double now in new[]{10,10.95,12})
        {
            Assert.Empty(oldSnapshot.PresentedHistory(history));
            collections.Observe(oldSnapshot.Scope,oldSnapshot.PresentedHistory(history),now,outcomesReadyAt:dice.SettlesAt);
            Assert.Empty(collections.Active(now));Assert.Empty(collections.HandSlots("P1",oldSnapshot.Goods,8,now));
        }
        // 卡牌退场后应用真正的新视图，飞牌此时才进入队列，不能被旧手牌数量取消。
        Assert.False(dice.IsRolling(13.1));
        collections.Observe(currentSnapshot.Scope,currentSnapshot.PresentedHistory(history),13.1,outcomesReadyAt:dice.SettlesAt);
        Assert.NotEmpty(collections.Active(13.1));
        var slots=collections.HandSlots("P1",owned,8,13.1);Assert.Equal(owned.Sum(),slots.Length);Assert.All(slots,slot=>Assert.NotNull(slot.Flight));
        Assert.DoesNotContain(collections.Active(13.1),flight=>flight.CardName=="印刷术");
        Assert.Equal(history,dice.VisibleHistory(currentSnapshot.PresentedHistory(history),13.1));
    }
    [Theory]
    [InlineData(false,"Printing","P1")] [InlineData(false,"Printing","P2")]
    [InlineData(false,"Constitution","P1")] [InlineData(false,"Constitution","P2")]
    [InlineData(true,"Printing","P1")] [InlineData(true,"Printing","P2")]
    [InlineData(true,"Constitution","P1")] [InlineData(true,"Constitution","P2")]
    public void DrawingEitherVictoryCardImmediatelyScoresRevealsAndAnimatesEvenWithAFullHand(bool combined,string card,string owner)
    {
        var probe=combined?DrawCombined(card,owner):DrawCities(card,owner);
        Assert.Equal(1,probe.Points);Assert.Equal(4,probe.Hand);Assert.False(probe.DiscardPending);
        foreach(var viewer in new[]{"P1","P2","P3","P4"})
        {
            var history=probe.History(viewer);var entry=Assert.Single(history);
            var gain=Assert.Single(entry.Changes,c=>c.Category=="progress"&&c.PlayerId==owner&&c.Amount>0);
            Assert.False(gain.IsPrivate);Assert.Equal(card=="Printing"?"印刷术":"宪法",gain.Name);
            var reveal=Assert.Single(new TabletopCardRevealTimeline().Observe(history));
            Assert.Equal(owner,reveal.PlayerId);Assert.True(reveal.ImmediateVictory);Assert.True(reveal.Progress);
            Assert.Equal(card=="Printing"?7:21,reveal.ProgressIndex);
            var flights=new TabletopCollectionTimeline();flights.Observe(viewer,[],0);flights.Observe(viewer,history,10);
            Assert.DoesNotContain(flights.Active(10.5),f=>f.CardName==gain.Name);
        }
        string committed=probe.Save();probe.RejectPlayAndDiscard();Assert.Equal(committed,probe.Save());
    }

    sealed record DrawProbe(int Points,int Hand,bool DiscardPending,Func<string,GameHistoryEntry[]> History,Func<string> Save,Action RejectPlayAndDiscard);
    static DrawProbe DrawCities(string name,string owner)
    {
        var card=Enum.Parse<M4.ProgressCardKind>(name);var track=M4.ProgressCatalog.Track(card);
        var game=M4CoreHarness.Fixture(s=>
        {
            s.Phase=M4.GamePhase.PendingChoice;s.DecisionReturnPhase=M4.GamePhase.Action;
            s.PendingDecision=new(){Kind="DefenderProgress",PlayerId=owner,ReturnPhase=M4.GamePhase.Action};
            M4CoreHarness.Player(s,owner).ProgressCards=Enumerable.Repeat(M4.ProgressCardKind.Merchant,4).ToArray();
        });
        var state=game.GetAuthoritativeStateForTesting();var deck=state.ProgressDecks.Single(d=>d.Track==track);
        deck.Cards=new[]{card}.Concat(deck.Cards.Where(c=>c!=card)).ToArray();M4CoreHarness.SetState(game,state);
        var command=M4CoreHarness.Cmd(M4.CommandKind.ResolveChoice,owner);command.Track=track;M4CoreHarness.Accepted(game,command);
        var view=game.GetPlayerView(owner);Assert.All(view.Players.Where(p=>p.Id==owner),p=>Assert.Equal(new[]{card},p.ProgressVictoryCards));
        return new(view.OwnVictoryPoints,view.OwnProgressCards.Length,view.PendingDecision?.Kind=="ProgressDiscard",id=>game.GetHistory(id),game.Save,()=>
        {
            var play=M4CoreHarness.Cmd(M4.CommandKind.PlayProgressCard,owner);play.ProgressCard=card;M4CoreHarness.Rejected(game,play);
            var discard=M4CoreHarness.Cmd(M4.CommandKind.DiscardProgressCard,owner);discard.ProgressCard=card;M4CoreHarness.Rejected(game,discard);
        });
    }
    static DrawProbe DrawCombined(string name,string owner)
    {
        var card=Enum.Parse<M5.ProgressCardKind>(name);var track=M5.ProgressCatalog.Track(card);
        var game=M5Harness.Fixture(s=>
        {
            s.Phase=M5.GamePhase.PendingChoice;s.DecisionReturnPhase=M5.GamePhase.Action;
            s.PendingDecision=new(){Kind="DefenderProgress",PlayerId=owner,ReturnPhase=M5.GamePhase.Action};
            M5Harness.Player(s,owner).ProgressCards=Enumerable.Repeat(M5.ProgressCardKind.Merchant,4).ToArray();
        });
        var state=game.GetAuthoritativeStateForTesting();var deck=state.ProgressDecks.Single(d=>d.Track==track);
        deck.Cards=new[]{card}.Concat(deck.Cards.Where(c=>c!=card)).ToArray();M5Harness.SetState(game,state);
        var command=M5Harness.Cmd(M5.CommandKind.ResolveChoice,owner);command.Track=track;M5Harness.Accepted(game,command);
        var view=game.GetPlayerView(owner);Assert.All(view.Players.Where(p=>p.Id==owner),p=>Assert.Equal(new[]{card},p.ProgressVictoryCards));
        return new(view.OwnVictoryPoints,view.OwnProgressCards.Length,view.PendingDecision?.Kind=="ProgressDiscard",id=>game.GetHistory(id),game.Save,()=>
        {
            var play=M5Harness.Cmd(M5.CommandKind.PlayProgressCard,owner);play.ProgressCard=card;M5Harness.Rejected(game,play);
            var discard=M5Harness.Cmd(M5.CommandKind.DiscardProgressCard,owner);discard.ProgressCard=card;M5Harness.Rejected(game,discard);
        });
    }

    [Fact]
    public void BatchedDiceDrawsUseTheActualRecipientAndDoNotReplayAfterRefreshOrReconnect()
    {
        var draw=Entry(2,"RollDice","P3",Change("P4","印刷术",false),Change("P2","宪法",false),Change("P3","采矿",true));
        var timeline=new TabletopCardRevealTimeline();
        var reveals=timeline.Observe([Entry(1,"EndTurn","P2"),draw]);
        Assert.Equal(new[]{"P4","P2"},reveals.Select(c=>c.PlayerId));Assert.All(reveals,c=>Assert.True(c.ImmediateVictory));
        Assert.Empty(timeline.Observe([draw]));timeline.Reset([draw]);Assert.Empty(timeline.Observe([draw]));
        Assert.Single(timeline.Observe([Entry(3,"ResolveChoice","P1",Change("P1","印刷术",false))]));
    }
    [Theory]
    [InlineData(false,"Printing",2u)] [InlineData(false,"Constitution",48u)]
    [InlineData(true,"Printing",2u)] [InlineData(true,"Constitution",48u)]
    public void EventDiceDrawsRevealTheNonActiveRecipientBeforeTheyHaveAnAction(bool combined,string name,uint randomState)
    {
        GameHistoryEntry[] history;
        if(combined)
        {
            var card=Enum.Parse<M5.ProgressCardKind>(name);var track=M5.ProgressCatalog.Track(card);
            var game=M5Harness.Fixture(s=>{s.Phase=M5.GamePhase.ProductionAwaitRoll;s.AlchemyDice1=1;s.AlchemyDice2=1;s.Random.State=randomState;M5Harness.Player(s,"P2").Improvements[(int)track]=1;});
            var state=game.GetAuthoritativeStateForTesting();var deck=state.ProgressDecks.Single(d=>d.Track==track);deck.Cards=new[]{card}.Concat(deck.Cards.Where(c=>c!=card)).ToArray();M5Harness.SetState(game,state);
            M5Harness.Accepted(game,M5Harness.Cmd(M5.CommandKind.RollDice));
            Assert.Equal(1,game.GetPlayerView("P2").OwnVictoryPoints);history=game.GetHistory("P3");
        }
        else
        {
            var card=Enum.Parse<M4.ProgressCardKind>(name);var track=M4.ProgressCatalog.Track(card);
            var game=M4CoreHarness.Fixture(s=>{s.Phase=M4.GamePhase.ProductionAwaitRoll;s.AlchemyDice1=1;s.AlchemyDice2=1;s.Random.State=randomState;M4CoreHarness.Player(s,"P2").Improvements[(int)track]=1;});
            var state=game.GetAuthoritativeStateForTesting();var deck=state.ProgressDecks.Single(d=>d.Track==track);deck.Cards=new[]{card}.Concat(deck.Cards.Where(c=>c!=card)).ToArray();M4CoreHarness.SetState(game,state);
            M4CoreHarness.Accepted(game,M4CoreHarness.Cmd(M4.CommandKind.RollDice));
            Assert.Equal(1,game.GetPlayerView("P2").OwnVictoryPoints);history=game.GetHistory("P3");
        }
        var entry=Assert.Single(history);Assert.Equal("RollDice",entry.Kind);
        var reveal=Assert.Single(new TabletopCardRevealTimeline().Observe(history));Assert.Equal("P2",reveal.PlayerId);Assert.True(reveal.ImmediateVictory);Assert.Same(entry,reveal.Roll);
    }
    [Fact]
    public void HiddenCardsAndBaseVictoryCardsNeverCreateAnAutomaticPublicReveal()
    {
        Assert.Empty(TabletopCardReveal.From(Entry(1,"RollDice","P1",Change("P2","印刷术",true),Change("P3","发展卡",false),Change("P4","胜利点",true))));
    }
    static GameHistoryEntry Entry(long sequence,string kind,string player,params GameHistoryChange[] changes)
    {var entry=new GameHistoryEntry();Set(entry,"Sequence",sequence);Set(entry,"Kind",kind);Set(entry,"PlayerId",player);Set(entry,"Changes",changes);return entry;}
    static GameHistoryChange Change(string player,string name,bool secret)
    {var change=new GameHistoryChange();Set(change,"PlayerId",player);Set(change,"Name",name);Set(change,"Category","progress");Set(change,"Amount",1);Set(change,"IsPrivate",secret);return change;}
    static void Set(object target,string property,object value)=>target.GetType().GetProperty(property)!.SetValue(target,value);
}
