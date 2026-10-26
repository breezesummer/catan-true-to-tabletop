using Catan.Core.ThreeKingdoms;
using Xunit;
using Xunit.Abstractions;
using M5=Catan.Core.M5;

namespace Catan.Rules.Tests;

public sealed class ThreeKingdomsB6SessionTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> Configurations()
    {
        foreach(string package in new[]{GeneralCatalog.B6PackageVersion})
        foreach(int seats in new[]{3,4})
        {
            yield return new object[]{"cities","base",seats,package};
            foreach(string map in M5.CombinedScenarios.Ids) yield return new object[]{"combined",map,seats,package};
        }
    }
    [Theory][MemberData(nameof(Configurations))]
    public void ProductAiFinishesNormalFiftyFourGeneralGameWithoutManualPhases(string rules,string map,int seats,string package)
    {
        var game=ThreeKingdomsTestGame.Create(rules,map,seats,skills:true,packageVersion:package);
        var checkpoints=new HashSet<string>();var generals=new HashSet<string>();int steps=0,round=0;
        var trace=Environment.GetEnvironmentVariable("CATAN_B6_TRACE");
        var timer=System.Diagnostics.Stopwatch.StartNew();
        while(game.View().Phase.ToString()!="Finished"&&steps<18000)
        {
            var tk=game.Authority();Assert.Null(tk.Automation?.PendingDecision);round=Math.Max(round,tk.Clock.Round);
            foreach(var g in tk.Generals.Where(g=>g.Region==GeneralRegion.Active))generals.Add(g.GeneralId);
            dynamic command=game.Decide();command.Id="B6整局-"+steps++;
            Assert.NotEqual("AdvanceThreeKingdomsPhase",(string)command.Kind.ToString());
            Assert.NotEqual("RollDice",(string)command.Kind.ToString());
            var decision=tk.Skills.PendingDecision??tk.Dice?.PendingDecision??tk.Automation?.PendingDecision;
            var checkpoint=decision!=null?"技能:"+decision.Kind+":"+decision.Title:tk.PendingDecision!=null?"武将:"+tk.PendingDecision.Kind:null;
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
        Assert.True(round>3);Assert.Equal(54,game.Authority().Generals.Length);
        Assert.Equal(game.Save(),ThreeKingdomsTestGame.Load(rules,game.Save()).Save());
        if(!string.IsNullOrEmpty(trace))File.WriteAllText(Path.Combine(trace,$"{rules}-{map}-{seats}-result.json"),System.Text.Json.JsonSerializer.Serialize(new{rules,map,seats,package,steps,round,generals=generals.Count,checkpoints=checkpoints.Count,seconds=timer.Elapsed.TotalSeconds,status="通过"}));
        output.WriteLine($"{package} {rules}/{map}/{seats}人：{steps}条命令，{round}轮，{generals.Count}将登场，{checkpoints.Count}类待决恢复。");
    }
}
