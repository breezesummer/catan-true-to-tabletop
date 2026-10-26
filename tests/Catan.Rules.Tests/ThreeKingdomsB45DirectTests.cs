using System.Reflection;
using System.Runtime.Serialization.Json;
using Catan.Core;
using Catan.Core.ThreeKingdoms;
using Xunit;
using M4 = Catan.Core.M4;
using M5 = Catan.Core.M5;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB45DirectTests
{
    private static ThreeKingdomsB2TestHost Host(string general)
    {
        var host = ThreeKingdomsB2TestHost.Create(general, "tk-g12", "tk-g22");
        host.State.RulesVersion = GeneralCatalog.B45RulesVersion; host.State.PackageVersion = GeneralCatalog.B45PackageVersion;
        host.State.PackageContentHash = GeneralCatalog.B45ContentHash; return host;
    }
    [Theory] [InlineData("tk-g15")] [InlineData("tk-g22")] [InlineData("tk-g05")]
    public void FreePlayOfferIsPureAndNeverCreatesAnActiveSkillQuestion(string general)
    {
        var host = Host(general); host.Give("P1", 2, 2, 1, 1);
        var before = host.Snapshot();
        Assert.NotEmpty(B2SkillEngine.Actions(host, "P1"));
        Assert.False(B4AutomationEngine.Offer(host)); Assert.False(B4AutomationEngine.Offer(host));
        Assert.Equal(before, host.Snapshot()); Assert.Null(host.State.Automation);
        host.State.RulesVersion = GeneralCatalog.B4RulesVersion; host.State.PackageVersion = GeneralCatalog.B4PackageVersion;
        host.State.PackageContentHash = GeneralCatalog.B4ContentHash;
        Assert.True(B4AutomationEngine.Offer(host)); Assert.Equal("automation-offer", host.State.Automation.PendingDecision.Kind);
        Assert.Equal(new[] { "automation-activate", "automation-skip" },
            ProjectLegacy(host).SkillActions.Select(a => a.Command.Option));
    }
    private static ThreeKingdomsView ProjectLegacy(ThreeKingdomsB2TestHost host)
    { var view = new ThreeKingdomsView(); B4AutomationEngine.Project(host, view, "P1"); return view; }

    [Theory] [InlineData("tk-g06")] [InlineData("tk-g13")]
    public void OptionalBankResponseChoosesAllCardsInOneCommandWithoutInitialAcceptance(string general)
    {
        var host = Host(general); host.State.Clock.Phase = ThreeKingdomsPhase.End;
        B2SkillEngine.OnEnd(host, "回合结束");
        var before = host.Snapshot(); var actions = B2SkillEngine.Actions(host, "P1");
        Assert.Equal(before, host.Snapshot()); Assert.DoesNotContain(actions, a => a.Command.Option == "accept");
        var command = actions.First(a => a.Command.Option == "b45-optional-cards").Command;
        Assert.Equal(4, actions[0].RequiredCardCount); command.CardType = -1; command.Counts = new[] { 1, 1, 1, 1, 0, 0, 0, 0 };
        B2SkillEngine.Resolve(host, "P1", command);
        Assert.Null(host.State.Skills.PendingDecision); Assert.Equal(command.Counts, host.GetHand("P1"));
        if (general == "tk-g13") Assert.Single(host.State.Clock.ExtraTurnQueue);
        else Assert.Contains(host.State.Skills.TimedEffects, e => e.Kind == "draw-block" && e.PlayerId == "P1");
        host.AssertConserved();
    }
    [Theory] [InlineData("tk-g09")] [InlineData("tk-g08")]
    public void OptionalTargetResponseUsesTheTargetDirectlyAndDeclineDoesNotReopen(string general)
    {
        var host = Host(general); host.Give("P2", 3); host.State.Clock.Phase = ThreeKingdomsPhase.End;
        B2SkillEngine.OnEnd(host, "真实结束");
        var actions = B2SkillEngine.Actions(host, "P1");
        Assert.DoesNotContain(actions, a => a.Command.Option == "accept");
        var chosen = actions.First(a => a.Command.Option == "b45-optional-target" && a.Command.TargetPlayerIds[0] == "P2").Command;
        var declined = host.Copy(); B2SkillEngine.Resolve(declined, "P1", B2SkillEngine.Actions(declined, "P1").Single(a => a.Command.Option == "skip").Command);
        B2SkillEngine.OnEnd(declined, "真实结束"); Assert.Null(declined.State.Skills.PendingDecision);
        B2SkillEngine.Resolve(host, "P1", chosen); Assert.Null(host.State.Skills.PendingDecision);
        Assert.Contains(host.State.Skills.TimedEffects, e => e.TargetPlayerId == "P2" && e.Kind == (general == "tk-g09" ? "zhenggu" : "response-jieying-pending"));
        host.AssertConserved();
    }
    [Fact]
    public void InvalidOptionalPaymentAndStaleTokenLeaveAllStateUntouched()
    {
        var host = Host("tk-g06"); host.State.Clock.Phase = ThreeKingdomsPhase.End; B2SkillEngine.OnEnd(host, "结束");
        var command = B2SkillEngine.Actions(host, "P1").First(a => a.Command.Option == "b45-optional-cards").Command;
        var before = host.Snapshot(); command.Counts = new[] { 0, 0, 0, 0, 0, 4, 0, 0 };
        Assert.Throws<InvalidOperationException>(() => B2SkillEngine.Resolve(host, "P1", command)); Assert.Equal(before, host.Snapshot());
        command.Counts = new[] { 4, 0, 0, 0, 0, 0, 0, 0 }; command.DecisionId = "旧令牌";
        Assert.Throws<InvalidOperationException>(() => B2SkillEngine.Resolve(host, "P1", command)); Assert.Equal(before, host.Snapshot());
    }
    [Fact]
    public void ForcedCardBatchCannotSkipAndLimitedBankCompletesWithoutRandomCalls()
    {
        var host = Host("tk-g12"); host.EmptyBank();
        host.Hands["P3"][0] -= 1; host.Bank[0] += 1;
        host.State.Skills.Frames = new[] { new B2SkillFrame { Id = "必选资源", Kind = "resource", Remaining = 3, Started = true, PlayerId = "P1", SkillId = "tk-g12-s1" } };
        B2SkillEngine.Pump(host); var actions = B2SkillEngine.Actions(host, "P1"); var action = Assert.Single(actions);
        Assert.Equal("b45-card-batch", action.Command.Option); Assert.Equal(1, action.RequiredCardCount);
        var invalid = new B2SkillCommand { SkillId = action.Command.SkillId, Option = "skip", WindowId = action.Command.WindowId, DecisionId = action.Command.DecisionId };
        var before = host.Snapshot(); Assert.Throws<InvalidOperationException>(() => B2SkillEngine.Resolve(host, "P1", invalid)); Assert.Equal(before, host.Snapshot());
        B2SkillEngine.Resolve(host, "P1", action.Command); Assert.Null(host.State.Skills.PendingDecision);
        Assert.Equal(1, host.GetHand("P1")[0]); Assert.Equal(0, host.RandomCalls); host.AssertConserved();
    }
    [Fact]
    public void DiceOpportunityStaysPendingUntilPlayerRespondsAndSurvivesSave()
    {
        var host = Host("tk-g19"); host.State.Clock.Phase = ThreeKingdomsPhase.Roll;
        B3DiceEngine.BeginRoll(host, "P1", "真实正式骰");
        Assert.NotNull(host.State.Dice.PendingDecision); Assert.Equal(0, host.RandomCalls);
        var before = host.Snapshot(); Assert.False(B4AutomationEngine.Offer(host)); Assert.Equal(before, host.Snapshot());
        var restored = host.Copy(); var command = B3DiceEngine.Actions(host, "P1").First(a => a.Command.Option == "observe").Command;
        B3DiceEngine.Resolve(host, "P1", command); B3DiceEngine.Resolve(restored, "P1", command);
        Assert.Equal(host.Snapshot(), restored.Snapshot()); Assert.Equal(6, host.RandomCalls);
        Assert.NotNull(host.State.Dice.PendingDecision); Assert.Null(B3DiceEngine.TakeFinal(host));
    }
    [Theory] [InlineData("tk-g26")] [InlineData("tk-g39")]
    public void PrivateSubsetBatchRejectsOverdraftAndKeepsUnrevealedCardsPrivate(string general)
    {
        var host = Host(general); host.Give("P1", 2, 1); host.Give("P2", 0, 1);
        var start = B2SkillEngine.Actions(host, "P1").First(a => a.Command.Option == (general == "tk-g26" ? "b4-xiashu" : "b4-quhu") && a.Command.TargetPlayerIds[0] == "P2").Command;
        B2SkillEngine.Resolve(host, "P1", start); var decision = host.State.Skills.PendingDecision;
        var command = new B2SkillCommand { SkillId = start.SkillId, Option = "b45-selected", WindowId = decision.WindowId,
            DecisionId = decision.DecisionId, Counts = new[] { 3, 0, 0, 0, 0, 0, 0, 0 } };
        var before = host.Snapshot(); Assert.Throws<InvalidOperationException>(() => B2SkillEngine.Resolve(host, decision.PlayerId, command)); Assert.Equal(before, host.Snapshot());
        command.Counts[0] = 1; var restored = host.Copy();
        B2SkillEngine.Resolve(host, decision.PlayerId, command); B2SkillEngine.Resolve(restored, decision.PlayerId, command);
        Assert.Equal(host.Snapshot(), restored.Snapshot());
        Assert.Equal(general == "tk-g26" ? "b4-pile" : "b4-declare", host.State.Skills.PendingDecision.Kind);
        var observer = new ThreeKingdomsView(); B2SkillEngine.Project(host, observer, "P3"); Assert.Empty(observer.SkillActions);
        host.AssertConserved();
    }
    [Fact]
    public void LegacyB4RejectsNewBatchOptionsWithoutMutatingItsSavedDecision()
    {
        var host=Host("tk-g26");host.State.RulesVersion=GeneralCatalog.B4RulesVersion;host.State.PackageVersion=GeneralCatalog.B4PackageVersion;host.State.PackageContentHash=GeneralCatalog.B4ContentHash;
        host.Give("P1",1);var start=B2SkillEngine.Actions(host,"P1").First(a=>a.Command.Option=="b4-xiashu").Command;
        B2SkillEngine.Resolve(host,"P1",start);var decision=host.State.Skills.PendingDecision;
        var command=new B2SkillCommand{SkillId=start.SkillId,Option="b45-selected",WindowId=decision.WindowId,DecisionId=decision.DecisionId};
        string before=host.Snapshot();Assert.Throws<InvalidOperationException>(()=>B2SkillEngine.Resolve(host,decision.PlayerId,command));Assert.Equal(before,host.Snapshot());
        command=B2SkillEngine.Actions(host,decision.PlayerId).Single(a=>a.Command.Option=="b4-selected").Command;
        B2SkillEngine.Resolve(host,decision.PlayerId,command);Assert.Equal("b4-pile",host.State.Skills.PendingDecision.Kind);
    }
    private static ThreeKingdomsState Generals(params string[] ids)
    {
        var tk = ThreeKingdomsEngine.Create(new[] { "P1", "P2", "P3", "P4" }, 42, _ => 0, true, GeneralCatalog.B45PackageVersion);
        while (tk.PendingDecision != null)
        {
            var d = tk.PendingDecision;
            if (d.Kind == ThreeKingdomsDecisionKind.SelectOpening) ThreeKingdomsEngine.ChooseGenerals(tk, d.PlayerId, d.CandidateIds.Take(2).ToArray(), d.DecisionId, d.WindowId, _ => 0);
            else ThreeKingdomsEngine.DeployGeneral(tk, d.PlayerId, d.CandidateIds[0], d.DecisionId, d.WindowId, _ => 0);
        }
        ThreeKingdomsEngine.BeginNormalTurns(tk); tk.Clock.Phase = ThreeKingdomsPhase.Play; tk.Clock.PhaseId = 5;
        tk.Clock.DrawPhaseTurnInstanceId = tk.Clock.TurnInstanceId;
        for (int i = 0; i < ids.Length; i++)
        {
            var g = tk.Generals.Single(g => g.Region == GeneralRegion.Active && g.OwnerPlayerId == "P" + (i + 1));
            var other = tk.Generals.Single(g => g.GeneralId == ids[i]);
            (g.GeneralId, other.GeneralId) = (other.GeneralId, g.GeneralId); (g.InstanceId, other.InstanceId) = (other.InstanceId, g.InstanceId);
            g.Counters = Array.Empty<GeneralCounter>(); B2SkillEngine.OnB4GeneralActivated(tk, g);
        }
        tk.Response = null; return tk;
    }
    private static dynamic Fixture(string mode, string first, Action<dynamic>? configure = null)
    {
        if (mode == "cities") return M4CoreHarness.Fixture(s => { s.RulesVersion = M4.CitiesKnightsGameSession.ThreeKingdomsB45RulesVersion; s.ThreeKingdoms = Generals(first, "tk-g12", "tk-g37", "tk-g19"); configure?.Invoke(s); });
        return M5Harness.Fixture(s => { s.RulesVersion = M5.CombinedGameSession.ThreeKingdomsB45RulesVersion; s.ThreeKingdoms = Generals(first, "tk-g12", "tk-g37", "tk-g19"); configure?.Invoke(s); });
    }
    private static dynamic Player(dynamic state, string player = "P1") => state is M4.GameState city ? (object)city.Players.Single(p => p.Id == player) : ((M5.GameState)state).Players.Single(p => p.Id == player);
    private static dynamic Command(dynamic game, string mode, string kind, string player = "P1")
    {
        dynamic command = mode == "cities" ? (object)new M4.Command { Kind = Enum.Parse<M4.CommandKind>(kind), PlayerId = player } : new M5.Command { Kind = Enum.Parse<M5.CommandKind>(kind), PlayerId = player };
        command.Id = ThreeKingdomsTestGame.NextId(); command.WindowId = game.GetPlayerView(player).ThreeKingdoms.WindowId; return command;
    }
    private static void Accept(dynamic game, dynamic command)
    { dynamic result = game.Execute(command); Assert.True((bool)result.Success, (string)result.ErrorCode + ":" + (string)result.Message); game.AssertInvariants(); }
    private static void InvokePump(dynamic game)
    { var state = game.GetAuthoritativeStateForTesting(); game.GetType().GetMethod("PumpB4", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(game, new object[] { state }); game.GetType().GetField("state", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(game, state); }

    private static string PaymentVertex(string mode)=>mode=="cities"?M4CoreHarness.New().GetPlayerView("P1").Board.Vertices[0].Id:M5Harness.New().GetPlayerView("P1").Board.Tiles.First(t=>t.Resource!="sea").Vertices[0];
    private static void PaymentBuilding(dynamic state,string vertex,bool city)
    {
        if(state is M4.GameState a){if(city)a.Cities=new[]{M4CoreHarness.Piece(vertex)};else a.Settlements=new[]{M4CoreHarness.Piece(vertex)};}
        else {M5.GameState b=state;if(city)b.Cities=new[]{M5Harness.Piece(vertex)};else b.Settlements=new[]{M5Harness.Piece(vertex)};}
    }
    private static string PaymentRoad(dynamic game,string vertex)
    {
        dynamic view=game.GetPlayerView("P1");if(view is M4.PlayerView a)return a.Board.Edges.First(e=>e.Vertices.Contains(vertex)).Id;
        M5.PlayerView b=view;return b.Board.Edges.First(e=>e.Vertices.Contains(vertex)&&b.Board.Tiles.Any(t=>t.Resource!="sea"&&e.Vertices.All(t.Vertices.Contains))).Id;
    }
    private static void TradeTrack(dynamic command,string mode)
    {if(mode=="cities")command.Track=M4.ImprovementTrack.Trade;else command.Track=M5.ImprovementTrack.Trade;}

    [Theory] [InlineData("cities",false)] [InlineData("combined",false)] [InlineData("cities",true)] [InlineData("combined",true)]
    public void DeclaredConstructionPreviewIsPureAndOnlyBuildsAfterAllPrivateTrustResponses(string mode,bool improvement)
    {
        string vertex=PaymentVertex(mode);int count=improvement?1:2;
        dynamic game=Fixture(mode,"tk-g07",s=>{PaymentBuilding(s,vertex,improvement);Player(s).Resources.Ore=count;});
        dynamic command=Command(game,mode,improvement?"ImproveCity":"BuildRoad");
        if(improvement)TradeTrack(command,mode);else command.TargetId=PaymentRoad(game,vertex);
        command.PaymentCards=new[]{0,0,0,0,count,0,0,0};command.DeclaredRealTypes=Enumerable.Repeat(4,count).ToArray();command.DeclaredEffectiveTypes=improvement?new[]{5}:new[]{0,1};
        string before=game.Save();Assert.True((bool)game.Preview(command).Success);Assert.Equal(before,(string)game.Save());
        Accept(game,command);dynamic pending=game.GetAuthoritativeStateForTesting();Assert.Equal(count,(int)Player(pending).Resources.Ore);
        Assert.Equal(0,((Array)pending.Roads).Length);Assert.Equal(0,(int)Player(pending).Improvements[0]);Assert.NotNull(pending.ThreeKingdoms.Response.Declaration);
        for(int card=0;card<count;card++)foreach(string player in new[]{"P2","P3","P4"})
        {
            B2SkillAction[] actions=game.GetPlayerView(player).ThreeKingdoms.SkillActions;dynamic trust=Command(game,mode,"UseGeneralSkill",player);
            trust.SkillCommand=actions.Single(a=>a.Command.Option=="response-trust").Command;Accept(game,trust);
        }
        dynamic state=game.GetAuthoritativeStateForTesting();Assert.Null(state.ThreeKingdoms.Response.Declaration);Assert.Equal(0,(int)Player(state).Resources.Total);
        if(improvement){Assert.Equal(1,(int)Player(state).Improvements[0]);Assert.Empty((Array)state.Roads);}else Assert.Equal(1,((Array)state.Roads).Length);
        string complete=game.Save();Assert.True((bool)game.Execute(command).IsDuplicate);Assert.Equal(complete,(string)game.Save());
    }
    [Theory] [InlineData("cities")] [InlineData("combined")]
    public void ExplicitLongdanConstructionUsesRealBricksAndPreviewDoesNotReserveThem(string mode)
    {
        string vertex=PaymentVertex(mode);dynamic game=Fixture(mode,"tk-g42",s=>{PaymentBuilding(s,vertex,false);Player(s).Resources.Brick=2;});
        dynamic command=Command(game,mode,"BuildRoad");command.TargetId=PaymentRoad(game,vertex);command.PaymentCards=new[]{0,2,0,0,0,0,0,0};
        string before=game.Save();Assert.True((bool)game.Preview(command).Success);Assert.Equal(before,(string)game.Save());Accept(game,command);
        dynamic state=game.GetAuthoritativeStateForTesting();Assert.Equal(0,(int)Player(state).Resources.Total);Assert.Equal(1,((Array)state.Roads).Length);Assert.Equal(19,(int)state.Bank.Brick);
    }
    [Theory] [InlineData("cities")] [InlineData("combined")]
    public void ExplicitAwakenedMiewuImprovementPaysOnceAndGetsOneConversionReward(string mode)
    {
        string vertex=PaymentVertex(mode);dynamic game=Fixture(mode,"tk-g41",s=>
        {
            PaymentBuilding(s,vertex,true);Player(s).Resources.Brick=1;ThreeKingdomsState tk=s.ThreeKingdoms;
            tk.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P1").Counters=new[]{new GeneralCounter{Key="wuku",Value=3},new GeneralCounter{Key="sanchen",Value=1}};
        });
        dynamic command=Command(game,mode,"ImproveCity");TradeTrack(command,mode);command.PaymentCards=new[]{0,1,0,0,0,0,0,0};
        string before=game.Save();Assert.True((bool)game.Preview(command).Success);Assert.Equal(before,(string)game.Save());Accept(game,command);
        dynamic state=game.GetAuthoritativeStateForTesting();Assert.Equal(1,(int)Player(state).Improvements[0]);Assert.Equal(1,(int)Player(state).Resources.Total);
        ThreeKingdomsState tkAfter=state.ThreeKingdoms;var general=tkAfter.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P1");
        Assert.Equal(2,general.Counters.Single(c=>c.Key=="wuku").Value);Assert.Equal(1,general.Counters.Single(c=>c.Key=="miewu").Value);
        string complete=game.Save();Assert.True((bool)game.Execute(command).IsDuplicate);Assert.Equal(complete,(string)game.Save());
    }

    [Theory] [InlineData("cities")] [InlineData("combined")]
    public void NativeHostCanTradeBeforeSkillsOrEndWithZeroSkillUses(string mode)
    {
        dynamic game = Fixture(mode, "tk-g22", s => { Player(s).Resources.Wood = 5; Player(s).Resources.Wheat = 1; Player(s).Resources.Ore = 1; });
        InvokePump(game); ThreeKingdomsView view = game.GetPlayerView("P1").ThreeKingdoms;
        Assert.NotEmpty(view.SkillActions); Assert.Null(view.SkillDecision);
        var before = (string)game.Save(); dynamic end = Command(game, mode, "EndTurn");
        Assert.True((bool)game.Preview(end).Success); Assert.Equal(before, (string)game.Save());
        Accept(game, end); Assert.NotEqual(1, (long)game.GetAuthoritativeStateForTesting().ThreeKingdoms.Clock.TurnInstanceId);
        dynamic buildingGame = Fixture(mode, "tk-g22", s => { Player(s).Resources.Wood = 4; Player(s).Resources.Wheat = 1; Player(s).Resources.Ore = 1; });
        InvokePump(buildingGame);
        dynamic build = Command(buildingGame, mode, "BankTrade"); build.GiveResource = Resource.Wood; build.ReceiveResource = Resource.Brick;
        Accept(buildingGame, build); Assert.Null(buildingGame.GetPlayerView("P1").ThreeKingdoms.SkillDecision);
        Assert.Equal(1, (int)Player(buildingGame.GetAuthoritativeStateForTesting()).Resources.Brick);
        // 交易后技能仍可主动选择；预览、失败与真正提交共用权威入口。
        B2SkillAction[] actions = buildingGame.GetPlayerView("P1").ThreeKingdoms.SkillActions;
        dynamic skill = Command(buildingGame, mode, "UseGeneralSkill"); skill.SkillCommand = actions.First(a => a.Command.Option == "exchange-hand").Command;
        var snapshot = (string)buildingGame.Save(); Assert.True((bool)buildingGame.Preview(skill).Success); Assert.Equal(snapshot, (string)buildingGame.Save());
        Accept(buildingGame, skill); Assert.Null(buildingGame.GetPlayerView("P1").ThreeKingdoms.SkillDecision);
    }
    [Theory] [InlineData("cities")] [InlineData("combined")]
    public void GuoseDirectCommandCostsOnceAndDoesNotCreateAnotherActivePrompt(string mode)
    {
        dynamic game = Fixture(mode, "tk-g05", s => Player(s).Resources.Brick = 2); InvokePump(game);
        B2SkillAction[] actions = game.GetPlayerView("P1").ThreeKingdoms.SkillActions;
        dynamic command = Command(game, mode, "UseGeneralSkill"); command.SkillCommand = actions.First(a => a.Command.Option == "response-guose").Command;
        var before = (string)game.Save(); Assert.True((bool)game.Preview(command).Success); Assert.Equal(before, (string)game.Save());
        Accept(game, command); var after = (string)game.Save(); dynamic duplicate = game.Execute(command);
        Assert.True((bool)duplicate.IsDuplicate); Assert.Equal(after, (string)game.Save()); Assert.Equal(0, (int)Player(game.GetAuthoritativeStateForTesting()).Resources.Brick);
        Assert.Null(game.GetPlayerView("P1").ThreeKingdoms.SkillDecision);
    }
    [Theory] [InlineData("cities")] [InlineData("combined")]
    public void PoxiBatchIsAtomicAndCannotRefundTheAlreadyUsedPrivateLook(string mode)
    {
        dynamic game = Fixture(mode, "tk-g08", s => { Player(s).Resources.Wood = 1; Player(s).Resources.Brick = 1; Player(s).Resources.Wool = 1; Player(s, "P2").Resources.Ore = 1; });
        B2SkillAction[] actions = game.GetPlayerView("P1").ThreeKingdoms.SkillActions;
        dynamic start = Command(game, mode, "UseGeneralSkill"); start.SkillCommand = actions.First(a => a.Command.Option == "response-poxi" && a.Command.TargetPlayerIds[0] == "P2").Command; Accept(game, start);
        ThreeKingdomsView view = game.GetPlayerView("P1").ThreeKingdoms; var token = view.SkillDecision;
        Assert.Equal("response-poxi-select", token.Kind);
        dynamic finish = Command(game, mode, "UseGeneralSkill"); finish.SkillCommand = new B2SkillCommand { SkillId = "tk-g08-s2", Option = "b45-poxi-confirm", WindowId = token.WindowId, DecisionId = token.DecisionId, Counts = new[] { 1, 1, 1, 0, 0, 0, 0, 0 }, SecondaryCounts = new[] { 0, 0, 0, 0, 1, 0, 0, 0 } };
        var before = (string)game.Save(); Assert.True((bool)game.Preview(finish).Success); Assert.Equal(before, (string)game.Save());
        finish.SkillCommand.SecondaryCounts[0] = 1; Assert.False((bool)game.Execute(finish).Success); Assert.Equal(before, (string)game.Save());
        finish.SkillCommand.SecondaryCounts[0] = 0; Accept(game, finish); Assert.Equal(0, (int)Player(game.GetAuthoritativeStateForTesting()).Resources.Total);
        Assert.Equal(0, (int)Player(game.GetAuthoritativeStateForTesting(), "P2").Resources.Total);
        ThreeKingdomsState tk = game.GetAuthoritativeStateForTesting().ThreeKingdoms;
        Assert.Equal(1, tk.Generals.Single(g => g.GeneralId == "tk-g08").Counters.Single(c => c.Key == "poxi").Value);
        Assert.Equal("card", tk.Skills.PendingDecision.Kind);
    }
    [Theory] [InlineData("cities")] [InlineData("combined")]
    public void QicePreparationUsesOneAtomicChoiceAndKeepsCostAfterNestedResponses(string mode)
    {
        dynamic game = Fixture(mode, "tk-g34", s =>
        {
            Player(s).Resources.Brick = 1; Player(s, "P2").Resources.Wood = 2;
            s.ThreeKingdoms.Clock.Phase = ThreeKingdomsPhase.Preparation;
            if (s is M4.GameState city) city.Phase = M4.GamePhase.ProductionAwaitRoll; else ((M5.GameState)s).Phase = M5.GamePhase.ProductionAwaitRoll;
        });
        InvokePump(game);var timer=System.Diagnostics.Stopwatch.StartNew();ThreeKingdomsView view = game.GetPlayerView("P1").ThreeKingdoms;timer.Stop();
        Assert.True(timer.Elapsed<TimeSpan.FromSeconds(15),"奇策候选重复完整试执行："+timer.Elapsed);
        dynamic forged=Command(game,mode,"UseGeneralSkill");forged.SkillCommand=B45SelectionContract.Copy(view.SkillActions.First(a=>a.Command.Option=="b45-optional-qice").Command);forged.SkillCommand.ProgressCardId="不存在的虚拟用途";
        string unchanged=game.Save();Assert.False((bool)game.Execute(forged).Success);Assert.Equal(unchanged,(string)game.Save());
        Assert.Equal("optional", view.SkillDecision.Kind); Assert.DoesNotContain(view.SkillActions, a => a.Command.Option == "accept");
        var selected = view.SkillActions.First(a => a.Command.Option == "b45-optional-qice" && a.VirtualCardId == "ResourceMonopoly");
        dynamic command = Command(game, mode, "UseGeneralSkill"); command.SkillCommand = selected.Command;
        var before = (string)game.Save(); Assert.True((bool)game.Preview(command).Success); Assert.Equal(before, (string)game.Save());
        Accept(game, command); ThreeKingdomsState state = game.GetAuthoritativeStateForTesting().ThreeKingdoms;
        Assert.DoesNotContain(state.Skills.Frames, f => f.Kind == "response-qice");
        Assert.Contains(state.SkillHistory, e => e.SkillId == "tk-g34-s1");
        Assert.Equal(0, (int)Player(game.GetAuthoritativeStateForTesting()).Resources.Brick);
    }
    [Theory] [InlineData("cities")] [InlineData("combined")]
    public void NewDefaultIdentityIsDistinctAndLegacySaveRoundTripsExactly(string mode)
    {
        dynamic normal = mode == "cities" ? (object)M4.CitiesKnightsGameSession.Create(AcceptanceFixture.Json, threeKingdoms: true) : M5.CombinedGameSession.Create("heading-for-new-shores", threeKingdoms: true);
        Assert.Equal(GeneralCatalog.B6PackageVersion, (string)normal.GetPlayerView("P1").ThreeKingdoms.PackageVersion);
        Assert.NotEqual(GeneralCatalog.B4ContentHash, GeneralCatalog.B45ContentHash);
        foreach (string package in new[] { GeneralCatalog.B4PackageVersion, GeneralCatalog.B45PackageVersion, GeneralCatalog.B5PackageVersion, GeneralCatalog.B5DirectPackageVersion })
        {
            var game = ThreeKingdomsTestGame.Create(mode, seats: 3, skills: true, packageVersion: package);
            for (int i = 0; i < 12; i++) game.AcceptAndResume(game.Decide());
            Assert.Equal(package, game.Authority().PackageVersion); Assert.Equal(game.Save(), ThreeKingdomsTestGame.Load(mode, game.Save()).Save());
        }
    }
}
