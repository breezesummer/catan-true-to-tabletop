using System.Text.Json;
using Catan.Core.ThreeKingdoms;
using Xunit;

namespace Catan.Rules.Tests;

/// <summary>只提供有限库存与合法棋盘用途的独立夹具，军事技能全部由产品核心结算。</summary>
internal sealed class B5MilitaryHost : IThreeKingdomsB5Host
{
    public ThreeKingdomsB2TestHost Inner { get; set; } = null!;
    public ThreeKingdomsState State => Inner.State;
    public Dictionary<string,B5BoardOption[]> Options { get; set; } = new();
    public List<B5BoardOption> Applied { get; set; } = new();
    public List<(string Event,string[] Players,bool Virtual)> Invasions { get; set; } = new();
    public int[] RedTypes { get; set; } = new[] {0,2,4};
    public int[] ScriptedRandom { get; set; } = Array.Empty<int>();
    public int RandomCursor { get; set; }
    public B4TokenMove[] Moves { get; set; } = Array.Empty<B4TokenMove>();
    public List<B4TokenMove> AppliedMoves { get; set; } = new();
    public static B5MilitaryHost Create(string first,string second="tk-g09",string third="tk-g09")
    {
        var h=new B5MilitaryHost{Inner=ThreeKingdomsB2TestHost.Create(first,second,third)};
        h.State.RulesVersion=GeneralCatalog.B5RulesVersion;h.State.PackageVersion=GeneralCatalog.B5PackageVersion;h.State.PackageContentHash=GeneralCatalog.B5ContentHash;
        h.State.Military=new B5MilitaryState();h.State.Board=new B5BoardState();return h;
    }
    public int[] GetHand(string player)=>Inner.GetHand(player);
    public int[] GetBank()=>Inner.GetBank();
    public void RemoveHand(string player,int[] cards)=>Inner.RemoveHand(player,cards);
    public void AddHand(string player,int[] cards)=>Inner.AddHand(player,cards);
    public void RemoveBank(int[] cards)=>Inner.RemoveBank(cards);
    public void AddBank(int[] cards)=>Inner.AddBank(cards);
    public int DrawRandom(int max){if(RandomCursor<ScriptedRandom.Length){Inner.RandomCalls++;return ScriptedRandom[RandomCursor++]%max;}return Inner.DrawRandom(max);}
    public B2ProgressCard[] GetProgressCards(string p)=>Inner.GetProgressCards(p);
    public int GetProgressDeckCount(int color)=>Inner.GetProgressDeckCount(color);
    public void RemoveProgress(string p,string card)=>Inner.RemoveProgress(p,card);
    public B2ProgressCard DrawProgress(string p,int color)=>Inner.DrawProgress(p,color);
    public void ReturnProgress(B2ProgressCard card)=>Inner.ReturnProgress(card);
    public B2HarvestTile[] GetHarvestTiles(string p)=>Inner.GetHarvestTiles(p);
    public int FinalDiceTotal=>Inner.FinalDiceTotal;
    public int GetHandLimit(string p)=>7+B2SkillEngine.B5HandLimitBonus(State,p);
    public B4VirtualUse[] GetVirtualProgressUses(string p,string timing)=>Array.Empty<B4VirtualUse>();
    public void UseVirtualProgress(string p,B4VirtualUse use)=>throw new InvalidOperationException("本夹具没有虚拟牌用途。");
    public B4TokenMove[] GetTokenMoves(string p)=>Moves;
    public void MoveToken(string p,B4TokenMove move)=>AppliedMoves.Add(move);
    public void ReceiveExtraHarvest(string p,int total,string id)=>throw new InvalidOperationException();
    public void RequestEndPlay(string p)=>throw new InvalidOperationException();
    public bool TryCaptureProgressCard(string a,string b,B2ProgressCard card,string id)=>false;
    public B5BoardAsset[] GetBoardAssets(string? playerId=null)=>Array.Empty<B5BoardAsset>();
    public B5BoardOption[] GetBoardOptions(string p,string kind,string? argument=null,int budget=0)=>Options.GetValueOrDefault(kind,Array.Empty<B5BoardOption>());
    public void ApplyBoardOption(string p,B5BoardOption option){Applied.Add(option);if(option.Kind=="deploy-knight"||option.Kind=="return-knight")Options[option.Kind]=Array.Empty<B5BoardOption>();}
    public int GetScore(string p)=>5;
    public int[] GetImprovements(string p)=>new int[3];
    public int[] GetActualImprovements(string p)=>new int[3];
    public void ChangeImprovement(string p,int track,int delta,bool temporary=false)=>throw new InvalidOperationException();
    public void RestoreImprovements(string p){}
    public void SetCollapseProtection(string p)=>throw new InvalidOperationException();
    public void AddPermanentScore(string p,int delta)=>throw new InvalidOperationException();
    public void ResolveInvasion(string id,string[] players,bool isVirtual)=>Invasions.Add((id,(string[])players.Clone(),isVirtual));
    public int[] GetAllRedResourceTypes()=>RedTypes;
    public string[] GetProducingTileIds(string p)=>new[]{"tile-fixed"};
    public string Snapshot()=>JsonSerializer.Serialize(new{Inner,Options,Applied,Invasions=Invasions.Select(x=>new{x.Event,x.Players,x.Virtual}),RedTypes,ScriptedRandom,RandomCursor,Moves,AppliedMoves});
    public B5MilitaryHost Copy()=>new(){Inner=Inner.Copy(),Options=JsonSerializer.Deserialize<Dictionary<string,B5BoardOption[]>>(JsonSerializer.Serialize(Options))!,Applied=JsonSerializer.Deserialize<List<B5BoardOption>>(JsonSerializer.Serialize(Applied))!,Invasions=Invasions.Select(x=>(x.Event,(string[])x.Players.Clone(),x.Virtual)).ToList(),RedTypes=(int[])RedTypes.Clone(),ScriptedRandom=(int[])ScriptedRandom.Clone(),RandomCursor=RandomCursor,Moves=Moves,AppliedMoves=AppliedMoves.ToList()};
}

public sealed class ThreeKingdomsB5MilitaryTests
{
    private static B2SkillCommand Pick(IThreeKingdomsCardHost h,string player,string option,int? type=null)=>B2SkillEngine.Actions(h,player).First(a=>a.Command.Option==option&&(!type.HasValue||a.Command.CardType==type)).Command;
    private static void Choose(IThreeKingdomsCardHost h,string player,string option,int? type=null)=>B2SkillEngine.Resolve(h,player,Pick(h,player,option,type));
    private static int Counter(B5MilitaryHost h,string player,string key)=>h.State.Generals.First(g=>g.OwnerPlayerId==player).Counters.FirstOrDefault(c=>c.Key==key)?.Value??0;
    private static void Drain(B5MilitaryHost h)
    {
        for(int i=0;i<200&&(B2SkillEngine.HasPending(h.State)||B3DiceEngine.HasPending(h.State)||B2SkillEngine.HasB5Comparison(h.State));i++)
        {
            B2SkillEngine.Pump(h);var d=h.State.Skills.PendingDecision;
            if(d!=null){var a=B2SkillEngine.Actions(h,d.PlayerId);Assert.NotEmpty(a);B2SkillEngine.Resolve(h,d.PlayerId,(a.FirstOrDefault(x=>x.Command.Option=="skip")??a.First()).Command);continue;}
            B3DiceEngine.Pump(h);var dice=h.State.Dice?.PendingDecision;
            if(dice!=null){var a=B3DiceEngine.Actions(h,dice.PlayerId);Assert.NotEmpty(a);B3DiceEngine.Resolve(h,dice.PlayerId,(a.FirstOrDefault(x=>x.Command.Option=="skip")??a.First()).Command);continue;}
            B2SkillEngine.ResumeB5Comparison(h);
        }
        Assert.False(B2SkillEngine.HasPending(h.State));Assert.False(B2SkillEngine.HasB5Comparison(h.State));h.Inner.AssertConserved();
    }
    private static B5BoardOption Option(string kind,int cost=0)
    {var result=new B5BoardOption{Id=kind+"-1",Kind=kind,PlayerId="P1",TargetId="v-test",OtherPlayerId="P2",Label="合法位置"};result.Cost[3]=cost;return result;}

    [Fact]
    public void WushengUsesUniqueMapRedResourcesAndFiniteBankOnlyOnce()
    {
        var h=B5MilitaryHost.Create("tk-g18");h.RedTypes=new[]{0,0,2,4};h.Inner.EmptyBank(4,5);
        Choose(h,"P1","b5m-wusheng");Drain(h);Assert.Equal(new[]{1,0,1,0,0,0,0,0},h.GetHand("P1"));Assert.Equal(1,Counter(h,"P1","wusheng"));
        Assert.DoesNotContain(B2SkillEngine.Actions(h,"P1"),x=>x.Command.Option=="b5m-wusheng");h.Inner.AssertConserved();
    }
    [Theory][InlineData(0,1)][InlineData(1,4)][InlineData(2,1)]
    public void ShanqiUsesExactlyOneRealKnightAndSurvivesRetirement(int count,int expected)
    {
        var h=B5MilitaryHost.Create("tk-g18");Choose(h,"P1","b5m-shanqi");h.State.Generals[0].Region=GeneralRegion.Retained;
        Assert.Equal(expected,B2SkillEngine.B5EffectiveKnightLevel(h.State,"P1",1,count));Assert.Single(h.State.Military.PermanentShanqiPlayers);
        h.State.Generals[0].GeneralId="tk-g02";h.State.Generals[0].Region=GeneralRegion.Active;
        Assert.Equal(expected+1,B2SkillEngine.B5EffectiveKnightLevel(h.State,"P1",1,count));
    }
    [Fact]
    public void WushuangOpeningDeploymentWaitsForBoardAndIsNotDuplicated()
    {
        var h=B5MilitaryHost.Create("tk-g02");h.State.Clock.Round=0;var g=h.State.Generals[0];B2SkillEngine.OnB5MilitaryGeneralActivated(h.State,g);B2SkillEngine.OnB5MilitaryGeneralActivated(h.State,g);
        Assert.Single(h.State.Military.DelayedDeployments);Assert.Empty(h.State.Skills.Frames);
        h.Options["deploy-knight"]=new[]{Option("deploy-knight")};h.State.Clock.Round=1;B2SkillEngine.OnB5MilitaryTurnStart(h,"start","P1");B2SkillEngine.Pump(h);
        var copy=h.Copy();var c=Pick(h,"P1","b5m-deploy");B2SkillEngine.Resolve(h,"P1",c);B2SkillEngine.Resolve(copy,"P1",c);Assert.Equal(h.Snapshot(),copy.Snapshot());Assert.Single(h.Applied);Assert.Empty(h.State.Military.DelayedDeployments);
    }
    [Fact]
    public void WushuangWithoutSupplyOrPositionSkipsOnlyDeployment()
    {
        var h=B5MilitaryHost.Create("tk-g02");B2SkillEngine.OnB5MilitaryGeneralActivated(h.State,h.State.Generals[0]);Drain(h);Assert.Empty(h.Applied);Assert.True(B2SkillEngine.B5ImmediateKnightAction(h.State,"P1"));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void LiyuPaysOwnerOrBankWithRealWheatAndRestoresBeforeBoardAction(bool self)
    {
        var h=B5MilitaryHost.Create(self?"tk-g02":"tk-g09",self?"tk-g09":"tk-g02");var option=Option("liyu",2);option.OtherPlayerId=self?"P1":"P2";h.Options["liyu"]=new[]{option};h.Inner.Give("P1",0,0,0,2);
        Choose(h,"P1","b5m-liyu");Drain(h);Assert.Equal(0,h.GetHand("P1")[3]);Assert.Equal(self?0:2,h.GetHand("P2")[3]);Assert.Single(h.Applied);h.Inner.AssertConserved();
    }
    [Fact]
    public void LiyuInsufficientPaymentAndForgedGeneralCannotMutate()
    {
        var h=B5MilitaryHost.Create("tk-g09","tk-g02");h.Options["liyu"]=new[]{Option("liyu",2)};h.Inner.Give("P1",0,0,0,1);string before=h.Snapshot();
        Assert.DoesNotContain(B2SkillEngine.Actions(h,"P1"),a=>a.Command.Option=="b5m-liyu");Assert.Throws<InvalidOperationException>(()=>B2SkillEngine.Resolve(h,"P1",new B2SkillCommand{SkillId="tk-g02-s2",Option="b5m-liyu",WindowId=h.State.WindowId,ProgressCardId="liyu-1",TargetPlayerIds=new[]{"P2"},Counts=new[]{0,0,0,2,0,0,0,0}}));Assert.Equal(before,h.Snapshot());
    }
    [Fact]
    public void LiyuLongdanPaysActualSheepAndWheatWithoutInventingWheat()
    {
        var h=B5MilitaryHost.Create("tk-g42","tk-g02");h.Options["liyu"]=new[]{Option("liyu",2)};h.Inner.Give("P1",0,0,1,1);
        var action=Pick(h,"P1","b5m-liyu");Assert.Equal(1,action.Counts[2]);Assert.Equal(1,action.Counts[3]);B2SkillEngine.Resolve(h,"P1",action);Drain(h);
        Assert.Equal(1,h.GetHand("P2")[2]);Assert.Equal(1,h.GetHand("P2")[3]);Assert.Single(h.Applied);h.Inner.AssertConserved();
    }
    [Fact]
    public void LiyuMiewuConsumesAtMostOneConvertedCardAndRejectsTwo()
    {
        var h=B5MilitaryHost.Create("tk-g41","tk-g02");h.Options["liyu"]=new[]{Option("liyu",2)};h.Inner.Give("P1",1,1,0,1);h.State.Generals[0].Counters=new[]{new GeneralCounter{Key="sanchen",Value=1},new GeneralCounter{Key="wuku",Value=2}};
        var actions=B2SkillEngine.Actions(h,"P1").Where(a=>a.Command.Option=="b5m-liyu").ToArray();Assert.Equal(2,actions.Length);Assert.All(actions,a=>Assert.Equal(1,a.Command.Counts[3]));
        B2SkillEngine.Resolve(h,"P1",actions[0].Command);Drain(h);Assert.Equal(1,Counter(h,"P1","wuku"));Assert.Equal(1,Counter(h,"P1","miewu"));Assert.Single(h.Applied);h.Inner.AssertConserved();
    }
    [Fact]
    public void LiyuRetirementReturnsKnightAfterGeneralLeavesPlay()
    {
        var h=B5MilitaryHost.Create("tk-g02");h.Options["return-knight"]=new[]{Option("return-knight")};var g=h.State.Generals[0];B2SkillEngine.OnB5MilitaryGeneralRetired(h.State,g);g.Region=GeneralRegion.Retired;B2SkillEngine.Pump(h);Choose(h,"P1","b5m-return");Drain(h);Assert.Single(h.Applied);
    }
    [Fact]
    public void LuanjiAllPlayersMayResistAndNoVirtualBattleIsCreated()
    {
        var h=B5MilitaryHost.Create("tk-g10");h.Inner.Give("P1",0,2,0,0,1);h.Inner.Give("P2",0,0,0,0,1);h.Inner.Give("P3",0,0,0,0,1);
        Choose(h,"P1","b5m-luanji",1);foreach(string p in new[]{"P1","P2","P3"})Choose(h,p,"b5m-resist",4);Drain(h);
        Assert.Empty(h.Invasions);Assert.Equal(3,h.GetHand("P1")[3]);Assert.Equal(0,h.GetHand("P1")[1]);Assert.Equal(3,B2SkillEngine.B5HandLimitBonus(h.State,"P1"));
    }
    [Fact]
    public void LuanjiResistanceUsesEffectiveOreButReturnsRealCoinAndRestores()
    {
        var h=B5MilitaryHost.Create("tk-g10","tk-g44");h.Inner.Give("P1",0,2);h.Inner.Give("P2",0,0,0,0,0,0,1);
        Choose(h,"P1","b5m-luanji",1);Assert.Equal("P2",h.State.Skills.PendingDecision.PlayerId);var copy=h.Copy();var command=Pick(h,"P2","b5m-resist",6);
        B2SkillEngine.Resolve(h,"P2",command);B2SkillEngine.Resolve(copy,"P2",command);Drain(h);Drain(copy);Assert.Equal(h.Snapshot(),copy.Snapshot());Assert.Single(h.Invasions);Assert.Equal(new[]{"P1","P3"},h.Invasions[0].Players);Assert.True(h.Invasions[0].Virtual);Assert.Equal(1,h.GetHand("P1")[3]);
    }
    [Fact]
    public void HuoshouWinsFixedWindowOnceAndDoesNotRollOrAddZhouChuModifier()
    {
        var h=B5MilitaryHost.Create("tk-g53","tk-g20","tk-g21");h.State.Clock.Phase=ThreeKingdomsPhase.Preparation;B3DiceEngine.BeginTurn(h);Assert.Equal("P2",h.State.Dice.PendingDecision.PlayerId);
        var fixedCommand=B3DiceEngine.Actions(h,"P2").First(a=>a.Command.Option=="fixed").Command;B3DiceEngine.Resolve(h,"P2",fixedCommand);Assert.Single(h.Invasions);Assert.False(h.Invasions[0].Virtual);Assert.Equal(1,h.State.Military.ImmediateInvasionTurnId);
        h.State.Clock.Phase=ThreeKingdomsPhase.Roll;B3DiceEngine.BeginRoll(h,"P1","formal");var result=B3DiceEngine.TakeFinal(h)!;Assert.Equal(7,result.Total);Assert.Equal(0,result.TotalModifier);Assert.False(result.ActualRoll);Assert.Equal(0,h.Inner.RandomCalls);Assert.Equal(1,Counter(h,"P2","huoshou"));Assert.Empty(B3DiceEngine.Validate(h.State));
    }
    [Fact]
    public void HuoshouDeclinePreservesLimitedUseAndLetsZhijianRespond()
    {
        var h=B5MilitaryHost.Create("tk-g20","tk-g21");h.State.Clock.Phase=ThreeKingdomsPhase.Preparation;B3DiceEngine.BeginTurn(h);var skip=B3DiceEngine.Actions(h,"P1").Single(a=>a.Command.Option=="skip").Command;B3DiceEngine.Resolve(h,"P1",skip);Assert.Equal("P2",h.State.Dice.PendingDecision.PlayerId);Assert.Equal(0,Counter(h,"P1","huoshou"));Assert.Empty(h.Invasions);
    }
    [Fact]
    public void HuoshouEndMovementSurvivesRetirementAndCanBeDeclined()
    {
        var h=B5MilitaryHost.Create("tk-g20");h.Moves=new[]{new B4TokenMove{OptionId="move",TileId="t1",TokenKind="robber",Label="移动"}};B2SkillEngine.CommitB5Huoshou(h,"P1","h");h.State.Generals[0].Region=GeneralRegion.Retired;
        B2SkillEngine.OnB5MilitaryEnd(h,"end");B2SkillEngine.Pump(h);Choose(h,"P1","b5m-huoshou-move");Drain(h);Assert.Single(h.AppliedMoves);B2SkillEngine.OnB5MilitaryEnd(h,"end");Assert.Empty(h.State.Skills.Frames);
    }
    [Fact]
    public void ZaiqiOnlyOffersSatisfiedBranchesAndKeepsTileEffectAfterRetirement()
    {
        var h=B5MilitaryHost.Create("tk-g20");B2SkillEngine.OnB5RaidResolved(h,"raid",false,false,false,"tile-fixed");B2SkillEngine.Pump(h);Assert.Single(B2SkillEngine.Actions(h,"P1"));Choose(h,"P1","b5m-zaiqi-double");
        h.State.Generals[0].Region=GeneralRegion.Retired;Assert.True(B2SkillEngine.B5DoubleHarvest(h.State,"P1","tile-fixed"));B2SkillEngine.OnB5RobberMoved(h.State,"elsewhere");Assert.False(B2SkillEngine.B5DoubleHarvest(h.State,"P1","tile-fixed"));
    }
    [Fact]
    public void ZaiqiPointIsMandatoryWhenItIsTheOnlyExecutableResult()
    {
        var h=B5MilitaryHost.Create("tk-g20");B2SkillEngine.OnB5RaidResolved(h,"raid",false,true,false);B2SkillEngine.Pump(h);Assert.Single(B2SkillEngine.Actions(h,"P1"));Choose(h,"P1","b5m-zaiqi-point");Assert.Equal(-1,B2SkillEngine.B5MilitaryScoreAdjustment(h.State,"P1"));
    }
    [Fact]
    public void YaowuPermanentLimitsAndActualEffectRewardSurviveRetirement()
    {
        var h=B5MilitaryHost.Create("tk-g24");Choose(h,"P1","b5m-yaowu");Assert.Equal(4,B2SkillEngine.B5HandLimitBonus(h.State,"P1"));Assert.Equal(2,B2SkillEngine.B5ProgressLimitBonus(h.State,"P1"));Assert.True(B2SkillEngine.B5AlwaysActiveKnights(h.State,"P1"));
        h.State.Generals[0].Region=GeneralRegion.Retained;Assert.False(B2SkillEngine.B5AlwaysActiveKnights(h.State,"P1"));B2SkillEngine.OnB5ProgressAffected(h,"blue","P2","P1",1);B2SkillEngine.OnB5ProgressAffected(h,"blue","P2","P1",1);Drain(h);Assert.Equal(1,h.GetHand("P2").Sum());
        B2SkillEngine.OnB5ProgressAffected(h,"green","P2","P1",2);B2SkillEngine.Pump(h);Choose(h,"P1","card",0);Drain(h);Assert.Equal(1,h.GetHand("P1")[0]);
    }
    [Fact]
    public void TishenUsesRealTurnSnapshotCapsAtSevenAndDoesNotDiscardSurplus()
    {
        var h=B5MilitaryHost.Create("tk-g38");h.Inner.Give("P1",9);B2SkillEngine.OnB5MilitaryTurnStart(h,"start","P1");h.RemoveHand("P1",new[]{6,0,0,0,0,0,0,0});h.AddBank(new[]{6,0,0,0,0,0,0,0});
        B2SkillEngine.OnB5MilitaryHarvestStart(h,"harvest");B2SkillEngine.Pump(h);Choose(h,"P1","accept");Drain(h);Assert.Equal(7,h.GetHand("P1").Sum());B2SkillEngine.OnB5MilitaryHarvestStart(h,"other-harvest");Assert.Empty(h.State.Skills.Frames);
    }
    [Fact]
    public void TishenWithoutActualTurnSnapshotDoesNotOfferAndSnapshotSurvivesNewGeneral()
    {
        var h=B5MilitaryHost.Create("tk-g38");B2SkillEngine.OnB5MilitaryHarvestStart(h,"first");Assert.Empty(h.State.Skills.Frames);h.Inner.Give("P1",3);B2SkillEngine.OnB5MilitaryTurnStart(h,"start","P1");h.State.Generals[0].GeneralId="tk-g09";Assert.Equal(3,h.State.Military.TurnHandSnapshots.Single().Value);
    }
    [Theory][InlineData("Settlement")][InlineData("City")][InlineData("Knight")][InlineData("PromoteKnight")]
    public void XianghaiTaxesSuccessfulMilitaryBuildingAndUnlocksAfterTwoActualCards(string kind)
    {
        var h=B5MilitaryHost.Create("tk-g53");h.Inner.Give("P2",3);h.Inner.Give("P3",2);
        for(int i=0;i<2;i++){B2SkillEngine.OnB5MilitaryBuilt(h,"build"+i,"P2",kind);B2SkillEngine.Pump(h);Choose(h,"P2","b5m-tax",0);Drain(h);}
        Assert.Equal(2,Counter(h,"P1","xianghai-tax"));Assert.Equal(2,h.GetHand("P1")[0]);Assert.Equal(-1,B2SkillEngine.B5HandLimitBonus(h.State,"P2"));Assert.Contains(B2SkillEngine.Actions(h,"P1"),a=>a.Command.Option=="b5m-chuhai");
    }
    [Fact]
    public void XianghaiEmptyHandDoesNotCreateDebtAndRoadNeverPaysTax()
    {
        var h=B5MilitaryHost.Create("tk-g53");B2SkillEngine.OnB5MilitaryBuilt(h,"empty","P2","City");h.Inner.Give("P2",2);B2SkillEngine.OnB5MilitaryBuilt(h,"road","P2","Road");Assert.Empty(h.State.Skills.Frames);Assert.Equal(0,Counter(h,"P1","xianghai-tax"));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void XianghaiAddsExactlyThreeToRealFormalAndJudgmentTotals(bool judgment)
    {
        var h=B5MilitaryHost.Create("tk-g53");h.ScriptedRandom=new[]{5,5,0};h.State.Clock.Phase=ThreeKingdomsPhase.Roll;
        if(judgment)B3DiceEngine.BeginJudgment(h,"P1","judge");else B3DiceEngine.BeginRoll(h,"P1","formal");var r=B3DiceEngine.TakeFinal(h)!;
        Assert.Equal(6,r.Red);Assert.Equal(6,r.White);Assert.Equal(15,r.Total);Assert.Equal(3,r.TotalModifier);Assert.Empty(B3DiceEngine.Validate(h.State));
        var saved=B3DiceEngine.Clone(h.State.Dice);Assert.Equal(15,saved.Roll.Result.Total);
    }
    [Fact]
    public void ChuhaiWinPrivatelySelectsDiscardThenReceivesPostDiscardMissingTypes()
    {
        var h=B5MilitaryHost.Create("tk-g53");h.State.Generals[0].Counters=new[]{new GeneralCounter{Key="xianghai-tax",Value=2}};h.Inner.Give("P2",1,1);h.Inner.Give("P3",2);h.ScriptedRandom=new[]{5,5,0,0};
        Choose(h,"P1","b5m-chuhai");Assert.True(B2SkillEngine.HasB5Comparison(h.State));B2SkillEngine.ResumeB5Comparison(h);B2SkillEngine.ResumeB5Comparison(h);Assert.Equal("b5m-chuhai-discard",h.State.Skills.PendingDecision.Kind);
        var owner=new ThreeKingdomsView();B2SkillEngine.ProjectB5Military(h,owner,"P1");Assert.Single(owner.MilitaryHands);var other=new ThreeKingdomsView();B2SkillEngine.ProjectB5Military(h,other,"P3");Assert.Empty(other.MilitaryHands);
        var restored=h.Copy();var c=Pick(h,"P1","b5m-chuhai-discard",0);B2SkillEngine.Resolve(h,"P1",c);B2SkillEngine.Resolve(restored,"P1",c);Drain(h);Drain(restored);Assert.Equal(h.Snapshot(),restored.Snapshot());Assert.Equal(7,h.GetHand("P1").Sum());Assert.Equal(0,h.GetHand("P1")[1]);Assert.Equal(1,h.GetHand("P1")[0]);
    }
    [Fact]
    public void ChuhaiTieRevealsLiveHandUntilNextOpportunityAndKeepsProgressPrivate()
    {
        var h=B5MilitaryHost.Create("tk-g53");h.State.Generals[0].Counters=new[]{new GeneralCounter{Key="xianghai-tax",Value=2}};h.Inner.Give("P2",2);h.Inner.Give("P3",2);h.ScriptedRandom=new[]{0,0,1,2};
        Choose(h,"P1","b5m-chuhai");Drain(h);Assert.True(B2SkillEngine.B5IsHandPublic(h.State,"P1"));h.Inner.Give("P1",0,0,1);var view=new ThreeKingdomsView();B2SkillEngine.ProjectB5Military(h,view,"P2");Assert.Equal(1,view.MilitaryHands.Single().Cards[2]);
        B2SkillEngine.OnB5MilitaryOpportunityStart(h,2,"P3",new[]{"P1"});Assert.False(B2SkillEngine.B5IsHandPublic(h.State,"P1"));
    }
    [Fact]
    public void MilitaryActionsAreAbsentFromB4AndForgedB5CommandHasNoEffects()
    {
        var h=B5MilitaryHost.Create("tk-g18");h.State.RulesVersion=GeneralCatalog.B4RulesVersion;var before=h.Snapshot();Assert.DoesNotContain(B2SkillEngine.Actions(h,"P1"),x=>x.Command.Option.StartsWith("b5m-"));Assert.Throws<InvalidOperationException>(()=>B2SkillEngine.Resolve(h,"P1",new B2SkillCommand{SkillId="tk-g18-s1",Option="b5m-wusheng",WindowId=h.State.WindowId}));Assert.Equal(before,h.Snapshot());
    }
    [Fact]
    public void XianghaiProjectionExplainsModifierAndPreservesLastFormalTotalAfterComparison()
    {
        var h=B5MilitaryHost.Create("tk-g53");h.State.Board.LastFormalTotal=15;var view=new ThreeKingdomsView();string before=h.Snapshot();B2SkillEngine.ProjectB5Military(h,view,"P2");
        Assert.Contains(view.SkillStatus,text=>text.Contains("合计 +3"));Assert.Contains(view.SkillStatus,text=>text.Contains("最后正式骰局合计：15"));Assert.Equal(before,h.Snapshot());
    }
}
