using Catan.AI;
using Catan.Core.M5;
using Xunit;
namespace Catan.Rules.Tests;
public sealed class CombinedAllScenarioTests
{
    static void SetState(CombinedGameSession g,GameState s) { typeof(CombinedGameSession).GetField("state",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(g,s); M5Harness.Invoke(g,"RecalculateAwards",s);g.AssertInvariants(); }
    static CombinedGameSession Fixture(string id,Action<GameState> configure)
    {
        var g=CombinedGameSession.Create(id,4,97);var s=g.GetAuthoritativeStateForTesting();s.Phase=GamePhase.Action;s.PendingDecision=null;s.Turn=2;s.ActivePlayerId="P1";
        configure(s);foreach(Catan.Core.Resource r in Enum.GetValues<Catan.Core.Resource>())s.Bank[r]=19-s.Players.Sum(p=>p.Resources[r]);
        SetState(g,s);return g;
    }
    [Fact] public void TribeRewardUsesChosenProgressColorAndRejectsInvalidColorWithoutMutation()
    {
        var g=Fixture("forgotten-tribe",s=>{});var s=g.GetAuthoritativeStateForTesting();string edge=s.Special.GiftCardEdgeIds[0];
        M5Harness.Invoke(g,"CollectSpecialGifts",s,"P1",edge);M5Harness.Invoke(g,"ContinueDecisions",s);SetState(g,s);
        var before=g.Save();Assert.False(g.Execute(new Command{Id="invalid-color",PlayerId="P1",Kind=CommandKind.ResolveChoice,Track=(ImprovementTrack)99}).Success);Assert.Equal(before,g.Save());
        Assert.True(g.Execute(new Command{Id="gift",PlayerId="P1",Kind=CommandKind.ResolveChoice,Track=ImprovementTrack.Science}).Success);
        var after=g.GetAuthoritativeStateForTesting();Assert.Equal(1,after.Players[0].ProgressCards.Length+after.Players[0].ProgressVictoryCards.Length);Assert.DoesNotContain(edge,after.Special.GiftCardEdgeIds);g.AssertInvariants();
    }
    [Fact] public void WarshipConversionDeactivatesOneKnightAndIllegalRetryDoesNotChangeState()
    {
        var g=Fixture("pirate-islands",s=>{
            var map=CombinedScenarios.Create("pirate-islands",4);var vertex=map.Vertices.First(v=>!s.Settlements.Any(b=>b.LocationId==v.Id)&&!s.Special.Fortresses.Any(f=>f.VertexId==v.Id)).Id;
            s.Knights=new[]{new Knight{PlayerId="P1",LocationId=vertex,Level=1,Active=true,ActivatedTurn=1}};
        });
        var v=g.GetPlayerView("P1");var c=v.LegalActions.First(x=>x.Kind==CommandKind.ConvertWarship);c.Id="convert";
        var before=g.Save();Assert.True(g.Preview(c).Success);Assert.Equal(before,g.Save());Assert.True(g.Execute(c).Success);
        Assert.Single(g.GetPlayerView("P1").Board.Ships,s=>s.IsWarship);Assert.False(g.GetPlayerView("P1").Board.Knights.Single().Active);
        before=g.Save();c.Id="convert-again";Assert.False(g.Execute(c).Success);Assert.Equal(before,g.Save());g.AssertInvariants();
    }
    [Fact] public void FourthWonderLevelWinsBelowPointThreshold()
    {
        var g=Fixture("wonders-of-catan",s=>{var p=s.Players[0];p.WonderId="grand-theater";p.WonderLevel=3;p.Resources=new Catan.Core.ResourceBag{Brick=1,Wood=1,Wool=3};});
        Assert.True(g.Execute(new Command{Id="wonder",PlayerId="P1",Kind=CommandKind.BuildWonder,TargetId="grand-theater"}).Success);
        Assert.Equal(GamePhase.Finished,g.GetPlayerView("P1").Phase);Assert.Equal("P1",g.GetPlayerView("P1").WinnerPlayerId);g.AssertInvariants();
    }
    [Fact] public void FiveExhaustedVillagesEndGameAndKeepClothSeparateFromCommodities()
    {
        var g=Fixture("cloth-for-catan",s=>{foreach(var village in s.Special.Villages.Take(5)){s.Special.ClothSupply+=village.Cloth;village.Cloth=0;}});
        Assert.True(g.Execute(new Command{Id="end-cloth",PlayerId="P1",Kind=CommandKind.EndTurn}).Success);
        Assert.Equal(GamePhase.Finished,g.GetPlayerView("P1").Phase);Assert.Equal(12,g.GetAuthoritativeStateForTesting().CommodityBank.Cloth);g.AssertInvariants();
    }
    [Fact] public void FogExplorationConservesTilesAndDoesNotExposeRemainingPool()
    {
        var g=Fixture("fog-islands",s=>{});var s=g.GetAuthoritativeStateForTesting();var map=CombinedScenarios.Create("fog-islands",4);
        var edge=map.Edges.First(e=>s.Tiles.Any(t=>t.Resource=="fog"&&t.Vertices.Any(e.Vertices.Contains)));int before=s.Special.HiddenResources.Length;
        M5Harness.Invoke(g,"Discover",s,"P1",edge.Id);if(s.DecisionQueue.Length>0)M5Harness.Invoke(g,"ContinueDecisions",s);SetState(g,s);
        Assert.True(g.GetAuthoritativeStateForTesting().Special.HiddenResources.Length<before);g.AssertInvariants();
        Assert.DoesNotContain("HiddenResources",System.Text.Json.JsonSerializer.Serialize(g.GetAiPlayerView("P2")));
    }
    [Fact] public void PirateRewardFinishesBeforeSevenDiscardIsCalculated()
    {
        string recipient="";
        var g=Fixture("pirate-islands",s=>{
            var map=CombinedScenarios.Create("pirate-islands",4);
            int target=Array.FindIndex(map.PiratePath,id=>map.Tiles.Single(t=>t.Id==id).Vertices.Any(v=>s.Settlements.Any(b=>b.LocationId==v)));
            var tile=map.Tiles.Single(t=>t.Id==map.PiratePath[target]);recipient=s.Settlements.First(b=>tile.Vertices.Contains(b.LocationId)).PlayerId;
            s.FirstBarbarianAttack=true;s.PirateTileId=map.PiratePath[(target+map.PiratePath.Length-1)%map.PiratePath.Length];
            var p=s.Players.Single(p=>p.Id==recipient);p.Resources.Wood=7;
            s.Ships.First(x=>x.PlayerId==recipient).IsWarship=true;
            s.Ships=s.Ships.Append(new ShipPlacement{PlayerId=recipient,LocationId=map.Edges.First(e=>!s.Ships.Any(x=>x.LocationId==e.Id)).Id,IsWarship=true}).ToArray();p.ShipsRemaining--;
            s.LastDice1=1;s.LastDice2=6;
        });
        var s=g.GetAuthoritativeStateForTesting();M5Harness.Invoke(g,"ResolveProduction",s);SetState(g,s);
        Assert.Equal("Gold",g.GetPlayerView(recipient).PendingDecision.Kind);Assert.Empty(s.Discards);
        Assert.True(g.Execute(new Command{Id="raid-gold",PlayerId=recipient,Kind=CommandKind.ResolveChoice,Resources=new Catan.Core.ResourceBag{Ore=1}}).Success);
        Assert.Equal(GamePhase.Discard,g.GetPlayerView(recipient).Phase);Assert.Equal(4,g.GetPlayerView(recipient).Discards.Single(d=>d.PlayerId==recipient).Amount);g.AssertInvariants();
    }
    [Fact] public void TreasonCannotPlaceKnightOnNeutralVillageEvenWithStaleOption()
    {
        string village="";
        var g=Fixture("cloth-for-catan",s=>{village=s.Special.Villages[0].VertexId;s.Phase=GamePhase.PendingChoice;s.PendingDecision=new PendingDecision{PlayerId="P1",Kind="ProgressTreasonPlace",Options=new[]{village},KnightLevel=1};});
        string before=g.Save();var result=g.Execute(new Command{Id="neutral-knight",PlayerId="P1",Kind=CommandKind.ResolveProgressChoice,TargetId=village,KnightLevel=1});
        Assert.False(result.Success);Assert.Equal(before,g.Save());g.AssertInvariants();
    }
    public static IEnumerable<object[]> Maps()=>CombinedScenarios.Ids.SelectMany(id=>new[]{3,4}.Select(n=>new object[]{id,n}));
    [Theory][MemberData(nameof(Maps))]
    public void AllMapsSetUpResumeAndRejectForeignCommands(string id,int seats)
    {
        var g=CombinedGameSession.Create(id,seats,97);
        Assert.Equal(Catan.Core.M3.SeafarersScenarios.Create(id,seats).TargetVictoryPoints+2,g.GetPlayerView("P1").TargetVictoryPoints);
        var policy=new CombinedAi();int steps=0;
        while(g.GetPlayerView("P1").Turn==0&&steps++<60) {
            var v=g.GetAiPlayerView(g.GetPlayerView("P1").PendingDecision?.PlayerId??g.GetPlayerView("P1").ActivePlayerId);
            var c=policy.Decide(v);c.Id="setup-"+steps;
            var result=g.Execute(c);Assert.True(result.Success,id+": "+result.ErrorCode+" "+result.Message);g.AssertInvariants();
            Assert.Equal(g.Save(),CombinedGameSession.Load(g.Save()).Save());
        }
        Assert.True(g.GetPlayerView("P1").Turn>0,id);
        var before=g.Save();var bad=g.Execute(new Command {Id="wrong-player",PlayerId="P99",Kind=CommandKind.RollDice});Assert.False(bad.Success);Assert.Equal(before,g.Save());
        var json=System.Text.Json.JsonSerializer.Serialize(g.GetAiPlayerView("P1"));
        Assert.DoesNotContain("HiddenResources",json);Assert.DoesNotContain("HiddenNumbers",json);Assert.DoesNotContain("Random",json);
    }
}
