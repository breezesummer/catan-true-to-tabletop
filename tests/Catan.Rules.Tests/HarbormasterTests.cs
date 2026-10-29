using System.Reflection;
using System.Text.Json.Nodes;
using Catan.Core;
using Catan.Core.TradeModules;
using Catan.Core.ThreeKingdoms;
using Xunit;
using Xunit.Abstractions;
using M2 = Catan.Core.M2;
using M3 = Catan.Core.M3;
using M4 = Catan.Core.M4;
using M5 = Catan.Core.M5;

namespace Catan.Rules.Tests;

// TB25.pdf 第4页；F0 H01—H06 的海港部分，旧靴仍属于 F2。
public sealed class HarbormasterTests(ITestOutputHelper output)
{
    static TradeModuleOptions Enabled => new() { Harbormaster = true };
    static PiecePlacement Piece(string vertex, string player) => new() { LocationId = vertex, PlayerId = player };
    static object? Invoke(object game, string method, params object[] args) => game.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)!.Invoke(game, args);
    static dynamic Create(string mode, int seats = 4, int? target = null, bool enabled = true, string map = "heading-for-new-shores", bool kingdoms = false) => mode switch
    {
        "base" => M2.BaseGameSession.Create(AcceptanceFixture.Json, seats, 97, target, enabled ? Enabled : null),
        "seafarers" => M3.SeafarersGameSession.Create(map, seats, 97, target, enabled ? Enabled : null),
        "cities" => M4.CitiesKnightsGameSession.Create(AcceptanceFixture.Json, seats, 97, target, threeKingdoms: kingdoms, tradeModules: enabled ? Enabled : null),
        "combined" => M5.CombinedGameSession.Create(map, seats, 97, target, threeKingdoms: kingdoms, tradeModules: enabled ? Enabled : null),
        _ => throw new ArgumentException(mode)
    };
    static HarborScore[] Scores(params int[] values) => HarbormasterRules.Scores(
        values.Select((v,i) => "P"+(i+1)), Enumerable.Range(0,30).Select(i=>"v"+i),
        values.SelectMany((count,i)=>Enumerable.Range(i*7,count).Select(v=>Piece("v"+v,"P"+(i+1)))), Array.Empty<PiecePlacement>());

    [Fact]
    public void H01ThresholdTieRetentionAndOvertake()
    {
        Assert.Null(HarbormasterRules.Holder(Scores(2,2,0), null));
        Assert.Equal("P1",HarbormasterRules.Holder(Scores(3,2,0), null));
        Assert.Equal("P1",HarbormasterRules.Holder(Scores(3,3,0), "P1"));
        Assert.Equal("P2",HarbormasterRules.Holder(Scores(3,4,0), "P1"));
    }
    [Fact]
    public void H02H03LossTieAndRecoveryDoNotDuplicateAward()
    {
        Assert.Equal("P1",HarbormasterRules.Holder(Scores(3,3,0), "P1"));
        Assert.Equal("P2",HarbormasterRules.Holder(Scores(2,3,0), "P1"));
        Assert.Equal("P2",HarbormasterRules.Holder(Scores(3,3,0), "P2"));
        Assert.Null(HarbormasterRules.Holder(Scores(2,3,3), "P1"));
        Assert.Equal("P2",HarbormasterRules.Holder(Scores(2,4,3), null));
        Assert.Null(HarbormasterRules.Holder(Scores(2,2,2), "P2"));
    }
    [Fact]
    public void H04EachVertexCountsOnceAndOnlyForActualOwner()
    {
        var scores=HarbormasterRules.Scores(new[]{"P1","P2"},new[]{"v1","v1","v2"},
            new[]{Piece("v3","P1")},new[]{Piece("v1","P1")});
        Assert.Equal(new[]{2,0},scores.Select(s=>s.Points));
    }
    [Theory]
    [InlineData("base",11)][InlineData("seafarers",15)][InlineData("cities",14)][InlineData("combined",17)]
    public void H06DefaultAdjustedOnceExplicitGoalAndOldSaveUnchanged(string mode,int target)
    {
        dynamic game=Create(mode); Assert.Equal(target,(int)game.GetPlayerView("P1").TargetVictoryPoints);
        Assert.Equal(16,(int)Create(mode,target:16).GetPlayerView("P1").TargetVictoryPoints);
        var saved=(string)game.Save(); Assert.Equal(saved,M6Game.Load(mode,saved).Save());
        dynamic disabled=Create(mode,enabled:false);
        Assert.DoesNotContain("TradeModules",(string)disabled.Save());
        Assert.Null((object?)disabled.GetPlayerView("P1").TradeModules);
        Assert.Equal((string)disabled.Save(),M6Game.Load(mode,(string)disabled.Save()).Save());
        Assert.Equal(M6Game.Json((object)disabled.GetAuthoritativeStateForTesting().Random),M6Game.Json((object)game.GetAuthoritativeStateForTesting().Random));
        Assert.Equal(M6Game.Json((object)disabled.GetPlayerView("P1").Board),M6Game.Json((object)game.GetPlayerView("P1").Board));
        Assert.Equal((int)disabled.GetAuthoritativeStateForTesting().SaveFormatVersion+100,(int)game.GetAuthoritativeStateForTesting().SaveFormatVersion);
        var withoutModule=JsonNode.Parse(saved)!.AsObject();withoutModule.Remove("TradeModules");
        Assert.Throws<InvalidDataException>(()=>M6Game.Load(mode,withoutModule.ToJsonString()));
        var bad=JsonNode.Parse(saved)!;bad["TradeModules"]!["Version"]="future";
        Assert.Throws<InvalidDataException>(()=>M6Game.Load(mode,bad.ToJsonString()));
        bad=JsonNode.Parse(saved)!;bad["TradeModules"]!["HarbormasterPlayerId"]="P1";
        Assert.Throws<InvalidDataException>(()=>M6Game.Load(mode,bad.ToJsonString()));
    }
    [Theory][InlineData("base")][InlineData("seafarers")][InlineData("cities")][InlineData("combined")]
    public void PreviewIllegalAndDuplicateCommandsPreserveStateAndResume(string mode)
    {
        dynamic authority=Create(mode);var game=new M6Game(mode,(object)authority);
        dynamic command=game.Decide(game.AiView(M6Game.Actor(game.View())));command.Id="海港开局";
        string before=game.Save();Assert.True((bool)authority.Preview(command).Success);Assert.Equal(before,game.Save());
        string validTarget=command.TargetId;command.TargetId="不存在";
        Assert.False((bool)game.Execute(command).Success);Assert.Equal(before,game.Save());
        command.TargetId=validTarget;game.Accept(command);string after=game.Save();
        Assert.True((bool)game.Execute(command).IsDuplicate);Assert.Equal(after,game.Save());
        var resumed=M6Game.Load(mode,after);Assert.Equal(after,resumed.Save());
        Assert.Equal(M6Game.Json((object)game.View().TradeModules),M6Game.Json((object)resumed.View().TradeModules));
        dynamic next=game.Decide(game.AiView(M6Game.Actor(game.View())));next.Id="海港续跑";
        game.Accept(next);resumed.Accept(next);Assert.Equal(game.Save(),resumed.Save());
    }
    [Theory][InlineData("seafarers")][InlineData("combined")]
    public void PirateIslandsRemainsExplicitlyClosed(string mode)
    {
        Assert.Throws<ArgumentException>(()=>Create(mode,map:"pirate-islands"));
        Assert.Null((object?)Create(mode,map:"pirate-islands",enabled:false).GetPlayerView("P1").TradeModules);
    }
    [Fact]
    public void AwardPointsAndPlayerViewAreDetachedAndWinOnlyInFreeActiveWindow()
    {
        var game=(M2.BaseGameSession)Create("base",target:5);var s=game.GetAuthoritativeStateForTesting();var ports=game.GetPlayerView("P1").Board.Ports;
        var a=ports[0].Vertices[0];var b=ports.First(p=>!p.Vertices.Contains(a)&&!game.GetPlayerView("P1").Board.Edges.Any(e=>e.Vertices.Contains(a)&&e.Vertices.Contains(p.Vertices[0]))).Vertices[0];
        s.Cities=new[]{Piece(a,"P1")};s.Settlements=new[]{Piece(b,"P1")};s.Turn=1;s.SetupPlacementIndex=8;s.ActivePlayerId="P2";s.Phase=M2.GamePhase.Action;
        M2Harness.Balance(s);M2Harness.SetState(game,s);
        Assert.Equal("P1",s.TradeModules.HarbormasterPlayerId);Assert.Equal(5,game.GetPlayerView("P1").OwnVictoryPoints);
        Assert.Equal(5,TabletopScoreModel.For(game.GetPlayerView("P1"),"P1").Total);
        Invoke(game,"CheckVictory",s);Assert.Null(s.WinnerPlayerId);
        s.ActivePlayerId="P1";s.PendingDecision=new M2.PendingDecision{Kind="Robber",PlayerId="P1"};s.Phase=M2.GamePhase.RobberMove;
        Invoke(game,"CheckVictory",s);Assert.Null(s.WinnerPlayerId);
        s.PendingDecision=null!;s.Phase=M2.GamePhase.Action;Invoke(game,"CheckVictory",s);Assert.Equal("P1",s.WinnerPlayerId);
        var detached=game.GetAuthoritativeStateForTesting();detached.TradeModules.HarbormasterPlayerId="P2";
        Assert.Equal("P1",game.GetPlayerView("P1").TradeModules.HarbormasterPlayerId);
        s.TradeModules.HarbormasterPlayerId="P2";Assert.Throws<InvalidDataException>(()=>game.AssertInvariants());
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void H02NativeCityDamageVillageCollapseAndRepairRecalculate(string mode)
    {
        dynamic game=Create(mode,kingdoms:true);dynamic state=game.GetAuthoritativeStateForTesting();
        var host=(IThreeKingdomsB5Host)Invoke((object)game,"CardHost",(object)state)!;
        dynamic board=game.GetPlayerView("P1").Board;
        string[] vertices=((Array)board.Ports).Cast<dynamic>().Select(p=>(string)p.Vertices[0]).Take(5).ToArray();
        state.Cities=new[]{Piece(vertices[0],"P1"),Piece(vertices[3],"P2")};state.Settlements=new[]{Piece(vertices[1],"P1"),Piece(vertices[2],"P1"),Piece(vertices[4],"P2")};
        Invoke((object)game,"RecalculateAwards",(object)state);Assert.Equal("P1",(string)state.TradeModules.HarbormasterPlayerId);
        host.ApplyBoardOption("P3",host.GetBoardOptions("P3","liegong").First(o=>o.TargetId=="City:"+vertices[0]&&o.AssetKind=="City"));
        Assert.Equal("P1",(string)state.TradeModules.HarbormasterPlayerId);
        var scores=(HarborScore[])Invoke((object)game,"HarborScores",(object)state)!;Assert.Equal(3,scores.Single(p=>p.PlayerId=="P1").Points);
        host.ApplyBoardOption("P3",host.GetBoardOptions("P3","liegong").First(o=>o.TargetId=="Settlement:"+vertices[1]&&o.AssetKind=="Settlement"));
        Assert.Equal("P2",(string)state.TradeModules.HarbormasterPlayerId);
        host.ApplyBoardOption("P1",host.GetBoardOptions("P1","repair").First(o=>o.TargetId=="Settlement:"+vertices[1]&&o.AssetKind=="Settlement"));
        Assert.Equal("P2",(string)state.TradeModules.HarbormasterPlayerId);
        Assert.Equal(3,((HarborScore[])Invoke((object)game,"HarborScores",(object)state)!).Single(p=>p.PlayerId=="P1").Points);
    }
    [Theory][InlineData("cities")][InlineData("combined")]
    public void H02DowngradedVillageWithItsOwnCollapseRecordCountsZero(string mode)
    {
        dynamic game=Create(mode,kingdoms:true);dynamic state=game.GetAuthoritativeStateForTesting();
        dynamic board=game.GetPlayerView("P1").Board;string vertex=board.Ports[0].Vertices[0];
        state.Settlements=new[]{Piece(vertex,"P1")};state.DowngradedCities=new[]{Piece(vertex,"P1")};
        ThreeKingdomsState tk=state.ThreeKingdoms;
        tk.Board.Damages=new[]{new B5Damage{Kind="City",PlayerId="P1",LocationId=vertex}};
        Assert.Equal(1,((HarborScore[])Invoke((object)game,"HarborScores",(object)state)!).Single(p=>p.PlayerId=="P1").Points);
        tk.Board.Damages=tk.Board.Damages.Append(new B5Damage{Kind="Settlement",PlayerId="P1",LocationId=vertex}).ToArray();
        Assert.Equal(0,((HarborScore[])Invoke((object)game,"HarborScores",(object)state)!).Single(p=>p.PlayerId=="P1").Points);
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void H05MapExchangeCountsFinalPortsAndNeverChecksIntermediateVictory(string mode)
    {
        dynamic game=Create(mode,kingdoms:true);dynamic state=game.GetAuthoritativeStateForTesting();
        var host=(IThreeKingdomsB6MapHost)Invoke((object)game,"CardHost",(object)state)!;
        dynamic board=game.GetPlayerView("P1").Board;
        string[] vertices=((Array)board.Ports).Cast<dynamic>().Select(p=>(string)p.Vertices[0]).Take(2).ToArray();
        state.Cities=new[]{Piece(vertices[0],"P1")};state.Settlements=new[]{Piece(vertices[1],"P1")};
        state.ActivePlayerId="P1";state.Turn=1;state.TargetVictoryPoints=5;
        Invoke((object)game,"RecalculateAwards",(object)state);Assert.Equal("P1",(string)state.TradeModules.HarbormasterPlayerId);
        int FinalScore(B6MapSwap candidate)
        {
            var ports=candidate.Result.Ports.SelectMany(p=>p.Vertices).ToHashSet();
            return candidate.Result.Pieces.Where(p=>p.PlayerId=="P1"&&ports.Contains(p.LocationId)).Sum(p=>p.Kind=="City"?2:p.Kind=="Settlement"?1:0);
        }
        var swap=host.GetMapB6Swaps().First(c=>FinalScore(c)<3);int expected=FinalScore(swap);
        var before=M6Game.Json((object)state.Random);host.ApplyMapB6Swap(swap.FirstTileId,swap.SecondTileId);
        Assert.Equal(before,M6Game.Json((object)state.Random));Assert.Null((object?)state.WinnerPlayerId);
        var scores=(HarborScore[])Invoke((object)game,"HarborScores",(object)state)!;
        Assert.Equal(expected,scores.Single(p=>p.PlayerId=="P1").Points);
        Assert.Null((object?)state.TradeModules.HarbormasterPlayerId);
        var snapshot=M6Game.Json((object)state);Assert.Throws<TargetInvocationException>(()=>Invoke((object)game,"ApplyMapB6",(object)state,"missing","missing"));
        Assert.Equal(snapshot,M6Game.Json((object)state));
    }

    [Theory][InlineData("cities")][InlineData("combined")]
    public void H04MetropolisDuplicatePortsAndChangedOwnershipUseActualBuildings(string mode)
    {
        dynamic game=Create(mode,kingdoms:true);dynamic state=game.GetAuthoritativeStateForTesting();
        dynamic board=game.GetPlayerView("P1").Board;string vertex=board.Ports[0].Vertices[0];
        state.Cities=new[]{Piece(vertex,"P1")};
        if(mode=="cities")
        {
            state.Metropolises=new[]{new M4.Metropolis{PlayerId="P1",LocationId=vertex,Track=M4.ImprovementTrack.Trade}};
            state.B6MapPorts=new[]{new M4.Port{Id="a",Vertices=new[]{vertex}},new M4.Port{Id="b",Vertices=new[]{vertex}}};
        }
        else
        {
            state.Metropolises=new[]{new M5.Metropolis{PlayerId="P1",LocationId=vertex,Track=M5.ImprovementTrack.Trade}};
            state.B6MapPorts=new[]{new M5.Port{Id="a",Vertices=new[]{vertex}},new M5.Port{Id="b",Vertices=new[]{vertex}}};
        }
        var scores=(HarborScore[])Invoke((object)game,"HarborScores",(object)state)!;
        Assert.Equal(new[]{2,0,0,0},scores.Select(p=>p.Points));
        state.Cities=new[]{Piece(vertex,"P2")};scores=(HarborScore[])Invoke((object)game,"HarborScores",(object)state)!;
        Assert.Equal(new[]{0,2,0,0},scores.Select(p=>p.Points));
    }
    [Fact]
    public void UnplacedTribePortsNeverEarnHarborPoints()
    {
        var game=(M3.SeafarersGameSession)Create("seafarers",map:"forgotten-tribe");var s=game.GetAuthoritativeStateForTesting();
        string vertex=game.GetPlayerView("P1").Board.Vertices[0].Id;
        s.Ports=Array.Empty<M3.Port>();s.Settlements=new[]{Piece(vertex,"P1")};
        s.Players[0].HeldPorts=new[]{new M3.Port{Id="held",Vertices=new[]{vertex}}};s.GiftPorts=s.Players[0].HeldPorts.ToArray();
        Assert.All((HarborScore[])Invoke(game,"HarborScores",s)!,p=>Assert.Equal(0,p.Points));
        s.Ports=s.GiftPorts.ToArray();Assert.Equal(1,((HarborScore[])Invoke(game,"HarborScores",s)!).Single(p=>p.PlayerId=="P1").Points);
    }
    [Fact]
    public void ProductAiTakesImmediateHarborVictoryAndLeavesViewUnchanged()
    {
        var game=(M2.BaseGameSession)Create("base",target:5);var s=game.GetAuthoritativeStateForTesting();var board=game.GetPlayerView("P1").Board;
        string a=board.Ports[0].Vertices[0];string b=board.Ports.Select(p=>p.Vertices[0]).First(v=>v!=a&&!board.Edges.Any(e=>e.Vertices.Contains(a)&&e.Vertices.Contains(v)));
        s.Settlements=new[]{Piece(a,"P1"),Piece(b,"P1")};s.Phase=M2.GamePhase.Action;s.ActivePlayerId="P1";s.Turn=1;s.SetupPlacementIndex=8;
        s.Players[0].Resources=new ResourceBag{Ore=3,Wheat=2};M2Harness.Balance(s);M2Harness.SetState(game,s);
        var view=game.GetAiPlayerView("P1");string before=game.Save();var command=new Catan.AI.BaseGameAi().Decide(view);
        Assert.Equal(M2.CommandKind.BuildCity,command.Kind);Assert.Contains(command.TargetId,new[]{a,b});Assert.Equal(before,game.Save());
        command.Id="海港抢胜";Assert.True(game.Execute(command).Success);Assert.Equal("P1",game.GetPlayerView("P1").WinnerPlayerId);
        Assert.Equal("P1",game.GetPlayerView("P1").TradeModules.HarbormasterPlayerId);
    }

    [Theory][InlineData("cities",3)][InlineData("cities",4)][InlineData("combined",3)][InlineData("combined",4)]
    public void ThreeKingdomsProductAiCompletesAndReplays(string mode,int seats)
    {
        var game=new ThreeKingdomsTestGame(mode,(object)Create(mode,seats,kingdoms:true));int commands=0;
        while(game.View().Phase.ToString()!="Finished"&&commands<15000)
        {
            game.Accept(game.Decide());commands++;
            if(commands==seats*8){string saved=game.Save();game=ThreeKingdomsTestGame.Load(mode,saved);Assert.Equal(saved,game.Save());}
        }
        Assert.Equal("Finished",(string)game.View().Phase.ToString());game.AssertInvariants();
        string final=game.Save();Assert.Equal(final,ThreeKingdomsTestGame.Load(mode,final).Save());
        output.WriteLine($"三国海港/{mode}/{seats}人：{commands}条命令，第{game.View().Turn}回合终局。");
    }

    public static IEnumerable<object[]> Games()
    {
        foreach(var mode in new[]{"base","cities","seafarers","combined"})
        foreach(int seats in new[]{3,4})
        foreach(string map in mode is "seafarers" or "combined" ? M3.SeafarersScenarios.Ids.Where(HarbormasterRules.Supports) : new[]{"heading-for-new-shores"})
            yield return new object[]{mode,map,seats};
    }
    [Theory][MemberData(nameof(Games))]
    public void ProductAiCompletesEveryOpenConfiguration(string mode,string map,int seats)
    {
        dynamic authority=Create(mode,seats,map:map);var game=new M6Game(mode,(object)authority);int commands=0;
        while(game.View().Phase.ToString()!="Finished"&&commands<10000)
        {
            dynamic view=game.AiView(M6Game.Actor(game.View()));dynamic c=game.Decide(view);c.Id="海港整局-"+commands++;
            game.Accept(c);
            if(commands==seats*4){var saved=game.Save();game=M6Game.Load(mode,saved);Assert.Equal(saved,game.Save());}
        }
        Assert.Equal("Finished",(string)game.View().Phase.ToString());game.AssertInvariants();
        string final=game.Save();Assert.Equal(final,M6Game.Load(mode,final).Save());
        output.WriteLine($"{mode}/{map}/{seats}人：{commands}条命令，第{game.View().Turn}回合终局。");
    }
}
