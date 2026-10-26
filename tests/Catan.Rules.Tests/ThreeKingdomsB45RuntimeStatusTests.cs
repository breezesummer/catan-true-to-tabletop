using System.Text.Json;
using Catan.Core.ThreeKingdoms;
using Xunit;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB45RuntimeStatusTests
{
    static ThreeKingdomsB2TestHost Host(string id)
    {var h=ThreeKingdomsB2TestHost.Create(id,"tk-g12","tk-g22");h.State.RulesVersion=GeneralCatalog.B45RulesVersion;h.State.PackageVersion=GeneralCatalog.B45PackageVersion;h.State.PackageContentHash=GeneralCatalog.B45ContentHash;return h;}
    static void Count(ThreeKingdomsB2TestHost h,string key,long scope,int count)=>h.State.Generals[0].Counters=h.State.Generals[0].Counters.Concat(new[]{new GeneralCounter{Key=key,TurnInstanceId=scope,Value=count}}).ToArray();
    static ThreeKingdomsView Project(ThreeKingdomsB2TestHost h,string player="P1")
    {var v=ThreeKingdomsEngine.Project(h.State,player);B2SkillEngine.Project(h,v,player);return v;}
    [Fact] public void ZhihengCountsOnlyCurrentTurnAndShowsBothBranches()
    {var h=Host("tk-g22");Count(h,"zhiheng-hand",h.State.Clock.TurnInstanceId-1,1);Count(h,"zhiheng-progress",h.State.Clock.TurnInstanceId,1);var before=h.Snapshot();var v=Project(h);var status=Assert.Single(v.SkillRuntimeStatus);Assert.Equal("手牌 1 · 发展 0",status.Summary);Assert.Equal(before,h.Snapshot());h.State.Clock.TurnInstanceId++;Assert.Equal("手牌 1 · 发展 1",Assert.Single(Project(h).SkillRuntimeStatus).Summary);}
    [Theory][InlineData("tk-g05","liuli","tk-g05-s1")][InlineData("tk-g05","guose","tk-g05-s2")][InlineData("tk-g08","poxi","tk-g08-s2")][InlineData("tk-g23","qianxun","tk-g23-s2")]
    public void PhaseCountersExpireWithoutMutatingTheLedger(string id,string key,string skill)
    {var h=Host(id);Count(h,key,h.State.Clock.PhaseId,1);var first=Project(h).SkillRuntimeStatus.Single(s=>s.SkillId==skill);Assert.Contains("0 / 1",first.Summary);Assert.Contains("阶段",first.UnavailableReason);h.State.Clock.PhaseId++;var second=Project(h).SkillRuntimeStatus.Single(s=>s.SkillId==skill);Assert.Contains("1 / 1",second.Summary);Assert.Equal(1,h.State.Generals[0].Counters.Single(c=>c.Key==key).Value);}
    [Fact] public void WukuAwakeningAndMiewuUseExactIndependentScopes()
    {var h=Host("tk-g41");Count(h,"wuku",0,2);Count(h,"sanchen",0,1);Count(h,"miewu",h.State.Clock.TurnInstanceId,1);var v=Project(h);Assert.Equal("武库 2 / 3",v.SkillRuntimeStatus.Single(s=>s.SkillId=="tk-g41-s1").Summary);Assert.Contains("已觉醒",v.SkillRuntimeStatus.Single(s=>s.SkillId=="tk-g41-s2").Summary);Assert.Equal("武库 2 · 剩余 0",v.SkillRuntimeStatus.Single(s=>s.SkillId=="tk-g41-s3").Summary);h.State.Clock.TurnInstanceId++;Assert.Equal("武库 2 · 剩余 1",Project(h).SkillRuntimeStatus.Single(s=>s.SkillId=="tk-g41-s3").Summary);}
    [Fact] public void RuntimeStatusDoesNotExposeUnknownKeysOrWenjiCardTypesToAnyone()
    {var h=Host("tk-g45");Count(h,"wenji:7",h.State.Clock.TurnInstanceId,1);Count(h,"secret-test-key",0,98);foreach(var viewer in h.State.SeatPlayerIds){var v=Project(h,viewer);string text=JsonSerializer.Serialize(v.SkillRuntimeStatus)+JsonSerializer.Serialize(v.SkillStatus);Assert.DoesNotContain("wenji",text);Assert.DoesNotContain("secret",text);Assert.DoesNotContain("奋音上次建筑类别",text);Assert.All(v.SkillRuntimeStatus,s=>Assert.StartsWith(GeneralCatalog.Get(h.State.Generals.Single(g=>g.OwnerPlayerId==viewer).GeneralId).Id,s.SkillId));}}
    [Fact] public void LegacyProjectionLeavesNewRuntimeFieldAbsent()
    {var h=Host("tk-g41");h.State.RulesVersion=GeneralCatalog.B4RulesVersion;h.State.PackageVersion=GeneralCatalog.B4PackageVersion;Assert.Null(Project(h).SkillRuntimeStatus);}
    [Fact] public void RendeRemainderAndJiushiExtraUsesAreNotHistoricalTurnTotals()
    {var h=Host("tk-g15");Count(h,"rende",h.State.Clock.TurnInstanceId-1,1);Assert.Equal("赠牌余数 0 / 2",Assert.Single(Project(h).SkillRuntimeStatus).Summary);var j=Host("tk-g30");Count(j,"jiushi",j.State.Clock.PhaseId-1,1);Count(j,"jiushi-extra",0,2);Assert.Equal("翻面 1 · 额外 1",Project(j).SkillRuntimeStatus.Single(s=>s.SkillId=="tk-g30-s2").Summary);}
    [Fact] public void FieldAndQinzhengIndicatorsUseKnownCountersOnly()
    {var h=Host("tk-g49");Assert.All(Project(h).SkillRuntimeStatus,s=>Assert.Equal("田 2 / 3",s.Summary));Count(h,"tian",0,0);Assert.Contains("没有",Project(h).SkillRuntimeStatus.Single(s=>s.SkillId=="tk-g49-s2").UnavailableReason);var q=Host("tk-g43");Count(q,"qinqin",0,4);Assert.Equal("下一项：发展牌",Assert.Single(Project(q).SkillRuntimeStatus).Summary);}
    [Fact] public void AllThirtyGeneralsHaveExactlyTheirOwnRuntimeSkillEntries()
    {foreach(var general in GeneralCatalog.B4Definitions){var h=Host(general.Id);var before=h.Snapshot();var view=Project(h);Assert.Equal(general.Skills.Select(s=>s.Id),view.SkillRuntimeStatus.Select(s=>s.SkillId));Assert.All(view.SkillRuntimeStatus,s=>Assert.False(string.IsNullOrEmpty(s.Summary)));Assert.Equal(before,h.Snapshot());}}
    [Fact] public void CurrentLegalActionsWinOverGenericUnavailableReasons()
    {var h=Host("tk-g05");h.Give("P1",0,2);var status=Project(h).SkillRuntimeStatus.Single(s=>s.SkillId=="tk-g05-s2");Assert.True(status.HasLegalAction);Assert.Null(status.UnavailableReason);}
}
