using System.Text.Json;
using Catan.AI;
using M2=Catan.Core.M2;
using M5=Catan.Core.M5;

// 从正常合法对局截取待决，不注入或伪造规则局面；权威内容仅放 .local。
string repo=Path.GetFullPath(args[0]),output=Path.GetFullPath(args[1]);
if(!output.StartsWith(Path.Combine(repo,".local")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("夹具只能写入本仓库 .local。");
Directory.CreateDirectory(output);
var observed=new List<object>();
foreach(string rules in new[]{"base","combined"})
{
    dynamic game=rules=="base"?(object)M2.BaseGameSession.Create(File.ReadAllText(Path.Combine(repo,"docs/acceptance/v0.1/scenario.json")),4,2027):M5.CombinedGameSession.Create("through-the-desert",4,2027);
    var found=new HashSet<string>();
    for(int i=0;i<5000;i++)
    {
        dynamic visible=game.GetPlayerView("P1");string phase=visible.Phase.ToString();
        if(phase=="Finished")break;
        string actor=visible.TradeOffer!=null?visible.TradeOffer.NextResponder(visible.PlayerId):phase=="Discard"?visible.Discards[0].PlayerId:phase=="GoldChoice"?visible.GoldClaims[0].PlayerId:visible.PendingDecision?.PlayerId??visible.ActivePlayerId;
        string? kind=visible.TradeOffer!=null?"交易":phase=="Discard"?"弃牌":phase.Contains("Robber")?"强盗":phase.StartsWith("Setup")&&visible.PendingDecision!=null?"开局摆放":visible.PendingDecision!=null?"其他待决":null;
        if(kind!=null&&found.Add(kind))
        {
            string path=Path.Combine(output,"pending-"+rules+"-"+found.Count+".json");
            File.WriteAllText(path,JsonSerializer.Serialize(new{Version=1,RuleSet=rules,ScenarioId="through-the-desert",PlayerCount=4,HumanSeat=actor,Authority=(string)game.Save(),Paused=true,HasAiSpeed=true,AiSpeedLevel=1}));
            // 基础版信封的剧本字段沿用现有兼容约定。
            if(rules=="base")File.WriteAllText(path,File.ReadAllText(path).Replace("through-the-desert","heading-for-new-shores"));
            observed.Add(new{rules,kind,phase,legalCommands=i,file=Path.GetFileName(path)});
        }
        dynamic view=game.GetAiPlayerView(actor);
        dynamic command=rules=="base"?(object)new BaseGameAi().Decide((M2.PlayerView)view):new CombinedAi().Decide((M5.PlayerView)view);
        command.Id="u1-fixture-"+i;
        if(!(bool)game.Execute(command).Success)throw new InvalidOperationException("合法夹具指令被拒绝。");
        game.AssertInvariants();
    }
}
if(observed.Count<4)throw new InvalidOperationException("待决覆盖不足。");
File.WriteAllText(Path.Combine(output,"pending-manifest.json"),JsonSerializer.Serialize(observed,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine("已生成 "+observed.Count+" 个合法待决夹具，公开清单不包含隐藏信息。");
