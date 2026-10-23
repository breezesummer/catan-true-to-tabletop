using System.Text.Json;
using Catan.Core.ThreeKingdoms;
using Xunit;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB4ResponseTests
{
    private static B4ResponseTestHost Host(string first, string second="tk-g12", string third="tk-g09") => B4ResponseTestHost.Create(first,second,third);
    private static void Act(B4ResponseTestHost h,string option,string player="P1",Func<B2SkillAction,bool>? predicate=null)
    {
        var a=B2SkillEngine.Actions(h,player).First(a=>a.Command.Option==option&&(predicate==null||predicate(a)));
        B2SkillEngine.Resolve(h,player,a.Command);
    }
    private static void Drain(B4ResponseTestHost h)
    {
        for(int i=0;i<100;i++)
        {
            if(B2SkillEngine.HasB4Judgment(h.State))
            {
                var dice=h.State.Dice?.PendingDecision;
                if(dice!=null){var a=B3DiceEngine.Actions(h,dice.PlayerId).First(x=>x.Command.Option=="dice-pass");B3DiceEngine.Resolve(h,dice.PlayerId,a.Command);}
                B2SkillEngine.ResumeB4Judgment(h);continue;
            }
            if(h.State.Skills.PendingDecision is { } d)
            {var actions=B2SkillEngine.Actions(h,d.PlayerId);var a=actions.FirstOrDefault(x=>x.Command.Option=="skip")??actions[0];B2SkillEngine.Resolve(h,d.PlayerId,a.Command);continue;}
            if(!B2SkillEngine.HasPending(h.State))return;
            B2SkillEngine.Pump(h);
        }
        Assert.Fail("测试技能队列未收敛。");
    }

    [Theory][InlineData(0,0,2)][InlineData(5,5,3)]
    public void TuntianJudgesOncePerLossBatchAndUsesFinalDice(int red,int white,int expected)
    {
        var h=Host("tk-g49");h.State.Clock.ActivePlayerId="P2";h.Give("P1",3);
        h.RandomValues=new[]{red,white};h.RemoveHand("P1",new[]{3,0,0,0,0,0,0,0});h.AddBank(new[]{3,0,0,0,0,0,0,0});
        B2SkillEngine.OnCardsMoved(h,"batch",new[]{new B2HandChange{PlayerId="P1",BeforeCount=3,AfterCount=0,LostCount=3,LostCards=new[]{3,0,0,0,0,0,0,0}}});
        Assert.True(B2SkillEngine.HasB4Judgment(h.State));Assert.Equal(2,h.RandomCursor);
        var restore=h.Copy();Drain(h);Drain(restore);Assert.Equal(h.Snapshot(),restore.Snapshot());
        Assert.Equal(expected,h.State.Generals[0].Counters.Single(c=>c.Key=="tian").Value);
        B2SkillEngine.OnCardsMoved(h,"batch",new[]{new B2HandChange{PlayerId="P1",BeforeCount=3,AfterCount=0,LostCount=3}});
        Assert.Equal(2,h.RandomCursor);h.AssertConserved();
    }
    [Fact]
    public void TuntianFinalJudgmentNotifiesQinqinWithoutChangingFormalTotal()
    {
        var h=Host("tk-g49","tk-g12","tk-g43");h.State.Clock.ActivePlayerId="P2";h.Base.FinalDiceTotal=7;h.RandomValues=new[]{5,5,0};h.Give("P1",1);
        h.RemoveHand("P1",new[]{1,0,0,0,0,0,0,0});h.AddBank(new[]{1,0,0,0,0,0,0,0});
        B2SkillEngine.OnCardsMoved(h,"lost",new[]{new B2HandChange{PlayerId="P1",BeforeCount=1,AfterCount=0,LostCount=1,LostCards=new[]{1,0,0,0,0,0,0,0}}});Drain(h);
        Assert.Equal(7,h.FinalDiceTotal);Assert.Equal(1,h.GetHand("P3").Sum());Assert.Equal(1,h.State.Generals[2].Counters.Single(c=>c.Key=="qinqin").Value);h.AssertConserved();
    }
    [Fact]
    public void JixiPaysOneFieldPerUseRejectsQianxunAndResetsOnNewTenure()
    {
        var h=Host("tk-g49","tk-g23","tk-g12");h.Give("P2",2);h.Give("P3",3);
        Assert.DoesNotContain(B2SkillEngine.Actions(h,"P1"),a=>a.Command.Option=="response-jixi"&&a.Command.TargetPlayerIds.Contains("P2"));
        Act(h,"response-jixi",predicate:a=>a.Command.TargetPlayerIds[0]=="P3");Act(h,"response-jixi",predicate:a=>a.Command.TargetPlayerIds[0]=="P3");
        Assert.Equal(2,h.GetHand("P1").Sum());Assert.DoesNotContain(B2SkillEngine.Actions(h,"P1"),a=>a.Command.Option=="response-jixi");
        B2SkillEngine.OnB4GeneralRetired(h.State,h.State.Generals[0]);h.State.Generals[0].Counters=Array.Empty<GeneralCounter>();B2SkillEngine.OnB4GeneralActivated(h.State,h.State.Generals[0]);
        Assert.Equal(2,h.State.Generals[0].Counters.Single(c=>c.Key=="tian").Value);h.AssertConserved();
    }
    [Fact]
    public void LiuliPaysWheatOnlyWhenHostHasLegalMoveAndInvokesHostOnce()
    {
        var h=Host("tk-g05");h.Give("P1",0,0,0,1);h.TokenMoves=Array.Empty<B4TokenMove>();
        Assert.DoesNotContain(B2SkillEngine.Actions(h,"P1"),a=>a.Command.Option=="response-liuli");
        h.TokenMoves=new[]{new B4TokenMove{OptionId="港口强盗",TokenKind="robber",TileId="T9",VictimPlayerId="P2",PayloadJson="{}",Label="T9"}};
        var command=B2SkillEngine.Actions(h,"P1").Single(a=>a.Command.Option=="response-liuli").Command;
        B2SkillEngine.Resolve(h,"P1",command);Assert.Equal("港口强盗",Assert.Single(h.Moves).OptionId);Assert.Equal(0,h.GetHand("P1")[3]);
        string before=h.Snapshot();Assert.Throws<InvalidOperationException>(()=>B2SkillEngine.Resolve(h,"P1",command));Assert.Equal(before,h.Snapshot());h.AssertConserved();
    }
    [Theory][InlineData(0,0,true)][InlineData(5,5,false)]
    public void GuoseJudgmentAppliesOnlyToTargetsNextActualPlay(int red,int white,bool prohibited)
    {
        var h=Host("tk-g05");h.Give("P1",0,2);Act(h,"response-guose",predicate:a=>a.Command.TargetPlayerIds[0]=="P2");
        Assert.Single(h.State.Skills.TimedEffects,e=>e.Kind=="response-indulgence-pending");
        Assert.DoesNotContain(B2SkillEngine.Actions(h,"P1"),a=>a.Command.Option=="response-guose");
        h.State.Clock.ActivePlayerId="P2";h.State.Clock.TurnInstanceId++;h.State.Clock.Phase=ThreeKingdomsPhase.Judgment;h.RandomValues=new[]{red,white};
        B2SkillEngine.OnB4JudgmentStart(h,"判定1");Drain(h);Assert.DoesNotContain(h.State.Skills.TimedEffects,e=>e.Kind=="response-indulgence-pending");
        h.State.Clock.Phase=ThreeKingdomsPhase.Play;Assert.Equal(prohibited,B2SkillEngine.IsB4HandLossForbidden(h.State,"P2"));Assert.False(B2SkillEngine.IsB4HandLossForbidden(h.State,"P1"));
        h.State.Clock.Phase=ThreeKingdomsPhase.End;Assert.False(B2SkillEngine.IsB4HandLossForbidden(h.State,"P2"));h.AssertConserved();
    }
    [Fact]
    public void GuoseRejectsProtectedOrQianxunTargetsAndDoesNotStack()
    {
        var h=Host("tk-g05","tk-g23");h.Give("P1",0,6);
        Assert.DoesNotContain(B2SkillEngine.Actions(h,"P1"),a=>a.Command.Option=="response-guose"&&a.Command.TargetPlayerIds.Contains("P2"));
        Act(h,"response-guose",predicate:a=>a.Command.TargetPlayerIds[0]=="P3");h.State.Clock.PhaseId++;
        Assert.DoesNotContain(B2SkillEngine.Actions(h,"P1"),a=>a.Command.Option=="response-guose");
        Assert.Equal(4,h.GetHand("P1")[1]);h.AssertConserved();
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void GuhuoBindsWholePaymentAndDefersTruthUntilEveryCommitment(bool falseClaim)
    {
        var h=Host("tk-g07");h.Give("P1",2);h.Give("P2",0,1);h.Give("P3",0,1);h.RandomValues=new[]{0,0};
        B2SkillEngine.BeginB4DeclarationBatch(h,"P1","用途1",new[]{0,0},new[]{falseClaim?1:0,0},"{\"kind\":\"建设\"}");
        Assert.Equal(2,h.GetHand("P1")[0]);Act(h,"response-challenge","P2");Assert.Equal(2,h.GetHand("P1")[0]);Assert.Equal(1,h.GetHand("P2")[1]);Assert.Equal(0,h.State.Skills.RevealedCards.Sum());
        var restored=h.Copy();Act(h,"response-challenge","P3");Act(restored,"response-challenge","P3");
        if(!falseClaim){Act(h,"response-trust","P2");Act(h,"response-trust","P3");Act(restored,"response-trust","P2");Act(restored,"response-trust","P3");}
        Drain(h);Drain(restored);Assert.Equal(h.Snapshot(),restored.Snapshot());
        var result=B2SkillEngine.TakeB4DeclarationResult(h)!;Assert.Equal(!falseClaim,result.Success);Assert.Equal("{\"kind\":\"建设\"}",result.OperationJson);
        Assert.Equal(falseClaim?1:2,h.GetHand("P1")[0]);Assert.Equal(falseClaim?2:0,h.GetHand("P2").Sum());Assert.Equal(falseClaim?2:0,h.GetHand("P3").Sum());Assert.All(h.State.SkillHistory,e=>Assert.Equal("P1",e.PlayerId));h.AssertConserved();
    }
    [Fact]
    public void GuhuoUnchallengedFalseDeclarationDoesNotRevealAndInvalidReservationDoesNotMutate()
    {
        var h=Host("tk-g07");h.Give("P1",1);string before=h.Snapshot();
        Assert.Throws<InvalidOperationException>(()=>B2SkillEngine.BeginB4DeclarationBatch(h,"P1","不足",new[]{0,0},new[]{1,1},"{}"));Assert.Equal(before,h.Snapshot());
        B2SkillEngine.BeginB4DeclarationBatch(h,"P1","无质疑",new[]{0},new[]{1},"{}");Act(h,"response-trust","P2");Act(h,"response-trust","P3");
        Assert.True(B2SkillEngine.TakeB4DeclarationResult(h)!.Success);Assert.Equal(0,h.State.Skills.RevealedCards.Sum());Assert.Equal(1,h.GetHand("P1")[0]);h.AssertConserved();
    }
    [Fact]
    public void JianxiongCapturesFourPhysicalCardsButNeverVirtualCards()
    {
        var h=Host("tk-g14");for(int i=0;i<5;i++)h.CompleteCard("P2","用牌"+i);
        Assert.Equal(4,h.GetProgressCards("P1").Length);Assert.Equal(4,h.State.Generals[0].Counters.Single(c=>c.Key=="jianxiong").Value);
        B2SkillEngine.OnB4ProgressCompleted(h,"虚拟","P2",null,true);Assert.Equal(4,h.Captures.Count);
        B2SkillEngine.OnB4ProgressCompleted(h,"用牌0","P2",h.Captures[0],false);Assert.Equal(4,h.Captures.Count);
    }
    [Fact]
    public void JianxiongRetirementKeepsOnlyOneOpportunityAndNewTenureRemovesResidual()
    {
        var h=Host("tk-g14");h.CompleteCard("P2","第一张");var g=h.State.Generals[0];B2SkillEngine.OnB4GeneralRetired(h.State,g);g.GeneralId="tk-g12";g.TenureId="新任次";g.Counters=Array.Empty<GeneralCounter>();
        h.CompleteCard("P2","隐退一张");Act(h,"accept");h.CompleteCard("P2","不能再取");Assert.Equal(2,h.Captures.Count);Assert.Empty(h.State.Response.CaoResiduals);
        g.GeneralId="tk-g14";B2SkillEngine.OnB4GeneralActivated(h.State,g);h.CompleteCard("P2","新任次重置");Assert.Equal(3,h.Captures.Count);
        Assert.Equal(1,g.Counters.Single(c=>c.Key=="jianxiong").Value);
    }
    [Fact]
    public void JianxiongResidualCanDeclineWithoutUseAndRestoredAcceptanceConsumesOnlyOnce()
    {
        var h=Host("tk-g14");var g=h.State.Generals[0];B2SkillEngine.OnB4GeneralRetired(h.State,g);g.GeneralId="tk-g12";g.TenureId="新任次";g.Counters=Array.Empty<GeneralCounter>();
        h.CompleteCard("P2","可放弃");Act(h,"skip");Assert.Single(h.State.Response.CaoResiduals);Assert.Empty(h.Captures);Assert.Empty(h.State.SkillHistory??Array.Empty<B4SkillHistoryEvent>());
        h.CompleteCard("P2","稍后发动");var copy=h.Copy();B2SkillEngine.Pump(copy);Assert.Equal("optional",copy.State.Skills.PendingDecision.Kind);
        Act(h,"accept");Act(copy,"accept");Assert.Equal(h.Snapshot(),copy.Snapshot());Assert.Single(h.Captures);Assert.Empty(h.State.Response.CaoResiduals);Assert.Single(h.State.SkillHistory!);
        h.CompleteCard("P2","不能再次发动");Assert.Null(h.State.Skills.PendingDecision);Assert.Single(h.Captures);
    }
    [Fact]
    public void JianxiongPublicPointCaptureKeepsPublicPointIdentity()
    {
        var h=Host("tk-g14");var card=new B2ProgressCard{Id="公开一分",Color=1,PublicPoint=true};h.Held.Add(card);
        B2SkillEngine.OnB4ProgressCompleted(h,"一分事件","P2",card);Assert.True(Assert.Single(h.Captures).PublicPoint);Assert.Empty(h.Held);
    }
    [Fact]
    public void QiceRequiresHandValidatesVirtualUseBeforeCostAndRestoresAfterChoice()
    {
        var h=Host("tk-g34");h.State.Clock.Phase=ThreeKingdomsPhase.Preparation;
        B2SkillEngine.OnB4Preparation(h,"空手");Assert.Null(h.State.Skills.PendingDecision);
        h.Give("P1",1,2);B2SkillEngine.OnB4Preparation(h,"奇策");Act(h,"accept");
        var restore=h.Copy();Act(h,"response-qice-use");Act(restore,"response-qice-use");Drain(h);Drain(restore);Assert.Equal(h.Snapshot(),restore.Snapshot());
        Assert.Equal(0,h.GetHand("P1").Sum());Assert.Equal("tk-g34-s1",Assert.Single(h.VirtualUses).SourceSkillId);h.AssertConserved();
    }
    [Fact]
    public void ZhiyuDrawsAndRevealsThenLetsUserDiscardEvenWithEmptyBank()
    {
        var h=Host("tk-g34");h.State.Clock.ActivePlayerId="P2";h.Give("P1",1);h.Give("P2",0,1);h.Base.EmptyBank(0,5);
        B2SkillEngine.OnB4ProgressCompleted(h,"外回合用牌","P2",null,true);Act(h,"accept");
        Assert.Equal(1,h.State.Skills.RevealedCards[0]);Assert.Equal("P2",h.State.Skills.PendingDecision.PlayerId);Act(h,"response-zhiyu-discard","P2");Assert.Equal(0,h.GetHand("P2").Sum());h.AssertConserved();
    }
    [Fact]
    public void ZhiyuDeclineCreatesNoSkillHistoryAndDoesNotConsumeRandom()
    {
        var h=Host("tk-g34");h.State.Clock.ActivePlayerId="P2";B2SkillEngine.OnB4ProgressCompleted(h,"可放弃","P2",null,true);Act(h,"skip");
        Assert.Equal(0,h.Base.RandomCalls);Assert.Empty(h.State.SkillHistory??Array.Empty<B4SkillHistoryEvent>());
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)]
    public void PoxiRequiresFourDistinctRealTypesAndAppliesEachSelfDiscardTier(int self)
    {
        var h=Host("tk-g08");var own=new int[8];var other=new int[8];for(int i=0;i<4;i++)if(i<self)own[i]=1;else other[i]=1;h.Give("P1",own);h.Give("P2",other);
        Act(h,"response-poxi",predicate:a=>a.Command.TargetPlayerIds[0]=="P2");
        for(int type=0;type<4;type++){int chosen=type;string owner=type<self?"P1":"P2";Act(h,"response-poxi-select",predicate:a=>a.Command.CardType==chosen&&a.Command.TargetPlayerIds[0]==owner);}
        var restore=h.Copy();Act(h,"response-poxi-confirm");Act(restore,"response-poxi-confirm");Drain(h);Drain(restore);Assert.Equal(h.Snapshot(),restore.Snapshot());
        Assert.Equal(self==0?1:0,B2SkillEngine.B4HandLimitPenalty(h.State,"P1"));Assert.Equal(self==1?1:0,h.EndPlayCount);Assert.Equal(self==4?1:0,h.ExtraHarvestCount);Assert.Equal(self==3?1:0,h.GetHand("P1").Sum());Assert.Equal(0,h.GetHand("P2").Sum());h.AssertConserved();
    }
    [Fact]
    public void PoxiCannotInspectWhenFewerThanFourTypesAndCannotRepeatAfterInspect()
    {
        var h=Host("tk-g08");h.Give("P1",3);h.Give("P2",0,3);Assert.DoesNotContain(B2SkillEngine.Actions(h,"P1"),a=>a.Command.Option=="response-poxi");
        h.Give("P1",0,0,1,1);Act(h,"response-poxi",predicate:a=>a.Command.TargetPlayerIds[0]=="P2");Assert.Equal(1,h.State.Generals[0].Counters.Single(c=>c.Key=="poxi").Value);
        Assert.DoesNotContain(B2SkillEngine.Actions(h,"P3"),a=>a.Command.Option=="response-poxi-select");
    }
    [Fact]
    public void JieyingSurvivesRetirementWaitsForRealPlayAndTransfersAtTargetEnd()
    {
        var h=Host("tk-g08","tk-g15");h.State.Clock.Phase=ThreeKingdomsPhase.End;B2SkillEngine.OnEnd(h,"甘宁结束");Act(h,"accept");Act(h,"response-jieying-target",predicate:a=>a.Command.TargetPlayerIds[0]=="P2");
        var g=h.State.Generals[0];B2SkillEngine.OnB4GeneralRetired(h.State,g);g.GeneralId="tk-g12";g.TenureId="换将";
        h.State.Clock.ActivePlayerId="P2";h.State.Clock.TurnInstanceId++;h.State.Clock.Phase=ThreeKingdomsPhase.Preparation;
        Assert.False(B2SkillEngine.IsB4SettlementForbidden(h.State,"P2"));h.State.Clock.Phase=ThreeKingdomsPhase.Play;
        B2SkillEngine.OnB4PlayStart(h,"目标出牌","P2");Drain(h);Assert.True(B2SkillEngine.IsB4SettlementForbidden(h.State,"P2"));Assert.Equal(1,h.GetHand("P2").Sum());
        h.Give("P2",1);h.State.Clock.Phase=ThreeKingdomsPhase.End;B2SkillEngine.OnEnd(h,"目标结束");Drain(h);Assert.Equal(2,h.GetHand("P1").Sum());Assert.Equal(0,h.GetHand("P2").Sum());Assert.DoesNotContain(h.State.SkillHistory,e=>e.SkillId=="tk-g15-s1");Assert.False(B2SkillEngine.IsB4SettlementForbidden(h.State,"P2"));h.AssertConserved();
    }
}

internal sealed class B4ResponseTestHost : IThreeKingdomsB4ResponseHost
{
    public ThreeKingdomsB2TestHost Base { get;set; }=new();
    public ThreeKingdomsState State=>Base.State;
    public int[] RandomValues{get;set;}=Array.Empty<int>();public int RandomCursor{get;set;}
    public B4TokenMove[] TokenMoves{get;set;}=Array.Empty<B4TokenMove>();public List<B4TokenMove> Moves{get;set;}=new();
    public List<B4VirtualUse> VirtualUses{get;set;}=new();public List<B2ProgressCard> Held{get;set;}=new();public List<B2ProgressCard> Captures{get;set;}=new();
    public int EndPlayCount{get;set;}public int ExtraHarvestCount{get;set;}
    public static B4ResponseTestHost Create(string first,string second,string third)
    {
        var h=new B4ResponseTestHost{Base=ThreeKingdomsB2TestHost.Create(first,second,third)};
        h.State.RulesVersion=GeneralCatalog.B4RulesVersion;h.State.PackageVersion=GeneralCatalog.B4PackageVersion;h.State.PackageContentHash=GeneralCatalog.B4ContentHash;
        foreach(var g in h.State.Generals)B2SkillEngine.OnB4GeneralActivated(h.State,g);return h;
    }
    public int[] GetHand(string p)=>Base.GetHand(p);public int[] GetBank()=>Base.GetBank();public void RemoveHand(string p,int[] c)=>Base.RemoveHand(p,c);public void AddHand(string p,int[]c)=>Base.AddHand(p,c);
    public void RemoveBank(int[]c)=>Base.RemoveBank(c);public void AddBank(int[]c)=>Base.AddBank(c);public int DrawRandom(int max)=>RandomCursor<RandomValues.Length?RandomValues[RandomCursor++]%max:Base.DrawRandom(max);
    public B2ProgressCard[] GetProgressCards(string p)=>Base.GetProgressCards(p);public int GetProgressDeckCount(int c)=>Base.GetProgressDeckCount(c);public void RemoveProgress(string p,string id)=>Base.RemoveProgress(p,id);
    public B2ProgressCard DrawProgress(string p,int c)=>Base.DrawProgress(p,c);public void ReturnProgress(B2ProgressCard c)=>Base.ReturnProgress(c);public B2HarvestTile[] GetHarvestTiles(string p)=>Base.GetHarvestTiles(p);public int FinalDiceTotal=>Base.FinalDiceTotal;
    public int GetHandLimit(string p)=>7-B2SkillEngine.B4HandLimitPenalty(State,p);
    public B4VirtualUse[] GetVirtualProgressUses(string p,string timing)=>new[]{new B4VirtualUse{OptionId="合法虚拟用牌",CardId="RoadBuilding",Label="修路",PayloadJson="{}"}};
    public void UseVirtualProgress(string p,B4VirtualUse use)=>VirtualUses.Add(use);
    public B4TokenMove[] GetTokenMoves(string p)=>TokenMoves;public void MoveToken(string p,B4TokenMove m)=>Moves.Add(m);
    public void ReceiveExtraHarvest(string p,int total,string id)=>ExtraHarvestCount++;public void RequestEndPlay(string p)=>EndPlayCount++;
    public bool TryCaptureProgressCard(string from,string to,B2ProgressCard card,string id)
    {var held=Held.FirstOrDefault(c=>c.Id==card.Id);if(held==null)return false;Held.Remove(held);Base.Progress[to].Add(held);Captures.Add(held);return true;}
    public void CompleteCard(string p,string id){var card=Base.Decks[0][0];Base.Decks[0].RemoveAt(0);Held.Add(card);B2SkillEngine.OnB4ProgressCompleted(this,id,p,card);}
    public void Give(string p,params int[] cards)=>Base.Give(p,cards);public void AssertConserved(){for(int t=0;t<8;t++)Assert.Equal(30,Base.Bank[t]+Base.Hands.Values.Sum(h=>h[t])+B2SkillEngine.EscrowCards(State)[t]);}
    public string Snapshot()=>JsonSerializer.Serialize(this);public B4ResponseTestHost Copy()=>JsonSerializer.Deserialize<B4ResponseTestHost>(Snapshot())!;
}
