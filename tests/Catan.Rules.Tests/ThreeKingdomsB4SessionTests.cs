using Catan.Core.ThreeKingdoms;
using Xunit;
using Xunit.Abstractions;
using M5=Catan.Core.M5;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB4SessionTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> Configurations()
    {
        foreach(int seats in new[]{3,4})
        {
            yield return new object[]{"cities","base",seats};
            foreach(string map in M5.CombinedScenarios.Ids) yield return new object[]{"combined",map,seats};
        }
    }
    [Theory][MemberData(nameof(Configurations))]
    public void ProductAiFinishesNormalThirtyGeneralGameWithoutManualPhases(string rules,string map,int seats)
    {
        var game=ThreeKingdomsTestGame.Create(rules,map,seats,skills:true,packageVersion:GeneralCatalog.B4PackageVersion);
        var checkpoints=new HashSet<string>();var generals=new HashSet<string>();int steps=0,round=0;
        while(game.View().Phase.ToString()!="Finished"&&steps<18000)
        {
            var tk=game.Authority();round=Math.Max(round,tk.Clock.Round);
            foreach(var g in tk.Generals.Where(g=>g.Region==GeneralRegion.Active))generals.Add(g.GeneralId);
            dynamic command=game.Decide();command.Id="B4整局-"+steps++;
            Assert.NotEqual("AdvanceThreeKingdomsPhase",(string)command.Kind.ToString());
            Assert.NotEqual("RollDice",(string)command.Kind.ToString());
            var decision=tk.Skills.PendingDecision??tk.Dice?.PendingDecision??tk.Automation?.PendingDecision;
            var checkpoint=decision!=null?"技能:"+decision.Kind+":"+decision.Title:tk.PendingDecision!=null?"武将:"+tk.PendingDecision.Kind:null;
            var trace=Environment.GetEnvironmentVariable("CATAN_B4_TRACE");
            if(!string.IsNullOrEmpty(trace))
            {
                Directory.CreateDirectory(trace);
                File.WriteAllText(Path.Combine(trace,$"{rules}-{map}-{seats}-progress.json"),System.Text.Json.JsonSerializer.Serialize(new{steps,round,checkpoint,command=(object)command,clock=tk.Clock}));
                if(steps%100==0)File.WriteAllText(Path.Combine(trace,$"{rules}-{map}-{seats}-checkpoint.json"),game.Save());
            }
            // 各种真实选择首次出现时完整公开Load重放；常规步骤仍逐条校验守恒。
            if(checkpoint!=null&&checkpoints.Add(checkpoint))game.AcceptAndResume(command);else game.Accept(command);
        }
        Assert.Equal("Finished",(string)game.View().Phase.ToString());
        Assert.True(round>3);Assert.Equal(30,game.Authority().Generals.Length);
        Assert.Equal(game.Save(),ThreeKingdomsTestGame.Load(rules,game.Save()).Save());
        output.WriteLine($"B4 {rules}/{map}/{seats}人：{steps}条命令，{round}轮，{generals.Count}将登场，{checkpoints.Count}类待决恢复。");
    }
}
