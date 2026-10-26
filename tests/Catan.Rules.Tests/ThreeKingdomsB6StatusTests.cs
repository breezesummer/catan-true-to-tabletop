using Catan.Core.ThreeKingdoms;
using Xunit;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB6StatusTests
{
    static ThreeKingdomsB2TestHost Host(string general)
    {var h=ThreeKingdomsB2TestHost.Create(general,"tk-g12","tk-g22");h.State.RulesVersion=GeneralCatalog.B6RulesVersion;h.State.PackageVersion=GeneralCatalog.B6PackageVersion;h.State.AllianceB6=new B6AllianceState();return h;}
    static B45SkillRuntimeStatus[] Project(ThreeKingdomsB2TestHost h)
    {var view=ThreeKingdomsEngine.Project(h.State,"P1");B45SkillStatusProjection.Project(h,view,"P1");return view.SkillRuntimeStatus;}
    [Theory][InlineData("tk-g04","huoxin")][InlineData("tk-g47","tamo")][InlineData("tk-g48","baiyi")]
    public void LimitedB6SkillsDisplayPersistentRemainingUses(string id,string key)
    {
        var h=Host(id);Assert.Equal("剩余 1 / 1",Project(h).Single(s=>s.SkillId==id+"-s1").Summary);
        h.State.Generals[0].Counters=new[]{new GeneralCounter{Key=key,Value=1,TurnInstanceId=0}};h.State.Clock.PhaseId++;
        string before=h.Snapshot();var status=Project(h).Single(s=>s.SkillId==id+"-s1");Assert.Equal("剩余 0 / 1",status.Summary);Assert.Contains("本任",status.UnavailableReason);Assert.Equal(before,h.Snapshot());
    }
    [Theory][InlineData("tk-g46","fuqi","tk-g46-s1",2)][InlineData("tk-g48","jinglue","tk-g48-s2",1)]
    public void PhaseB6SkillsShowExactLimitAndRenewAtNextPhase(string id,string key,string skill,int maximum)
    {
        var h=Host(id);h.State.Generals[0].Counters=new[]{new GeneralCounter{Key=key,Value=maximum,TurnInstanceId=h.State.Clock.PhaseId}};
        Assert.Equal("剩余 0 / "+maximum,Project(h).Single(s=>s.SkillId==skill).Summary);h.State.Clock.PhaseId++;
        Assert.Equal("剩余 "+maximum+" / "+maximum,Project(h).Single(s=>s.SkillId==skill).Summary);
    }
    [Fact] public void ZhonghuiDisplaysMemberCountAndEnemyMultiplier()
    {
        var h=Host("tk-g50");h.State.AllianceB6.Sources=new[]{new B6AllianceSource{SourcePlayerId="P1",TenureId=h.State.Generals[0].TenureId,Members=new[]{"P1","P2"}}};
        var status=Project(h);Assert.Equal("盟友 1 / 1",status.Single(s=>s.SkillId=="tk-g50-s1").Summary);Assert.Equal("对敌效果 ×2",status.Single(s=>s.SkillId=="tk-g50-s2").Summary);
    }
}
