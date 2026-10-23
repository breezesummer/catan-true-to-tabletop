using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Catan.Core.ThreeKingdoms;

namespace Catan.Core.M5
{
    public sealed partial class Command
    {
        [DataMember(Order=304, EmitDefaultValue=false)] public ThreeKingdomsPhase? RetireAtPhase { get; set; }
        [DataMember(Order=305, EmitDefaultValue=false)] public bool CancelRetirementIntent { get; set; }
    }
    public sealed partial class CombinedGameSession
    {
        public const string ThreeKingdomsB4RulesVersion = "catan-seafarers-cities-knights-three-kingdoms-b4-v1";
        private static bool B4(GameState s) => B4AutomationEngine.Enabled(s.ThreeKingdoms);
        private void ApplyB4(GameState s, Command c)
        {
            var tk=s.ThreeKingdoms;
            try
            {
                Require(s.Phase!=GamePhase.Finished,"GameFinished","对局已经结束。");
                Require(c.Kind!=CommandKind.AdvanceThreeKingdomsPhase,"AutomaticPhase","本规则包由系统自动推进阶段。");
                if(BeginB4Declaration(s,c)) { PumpB4(s); return; }
                if(c.Kind==CommandKind.RetireGeneral && (c.RetireAtPhase.HasValue || c.CancelRetirementIntent))
                {
                    Require(c.WindowId==tk.WindowId,"StaleWindow","操作窗口已更新。");
                    B4AutomationEngine.SetRetirement(tk,c.PlayerId,c.RetireAtPhase,c.CancelRetirementIntent);
                    RefreshB2(s);
                }
                else if(tk.Automation?.PendingDecision!=null && tk.PendingDecision==null)
                {
                    if(c.Kind==CommandKind.RetireGeneral) ApplyB2(s,c);
                    else
                    {
                        Require(c.Kind==CommandKind.UseGeneralSkill && c.SkillCommand!=null,"SkillDecisionPending","请先回答技能询问。");
                        Require(c.WindowId==tk.WindowId,"StaleWindow","操作窗口已更新。");
                        B4AutomationEngine.Resolve(CardHost(s),c.PlayerId,c.SkillCommand);
                        FinishB2Work(s); RefreshB2(s);
                    }
                }
                else ApplyB2(s,c);
                PumpB4(s);
            }
            catch(InvalidOperationException ex) { throw new RuleException("InvalidThreeKingdomsAction",ex.Message); }
        }

        private void PumpB4(GameState s)
        {
            var tk=s.ThreeKingdoms; var seen=new HashSet<string>(); var trace=new Queue<string>();
            while(s.Phase!=GamePhase.Finished)
            {
                FinishB2Work(s); B4AutomationEngine.Refresh(tk);
                if(tk.PendingDecision!=null || B2Busy(s) || s.PendingDecision!=null || s.TradeOffer!=null || s.DecisionQueue.Length>0) return;
                if(tk.Clock.Phase==ThreeKingdomsPhase.OpeningSelection || tk.Clock.Phase==ThreeKingdomsPhase.InitialPlacement) return;
                RecalculateAwards(s); CheckVictory(s); if(s.Phase==GamePhase.Finished) return;
                if(B4AutomationEngine.TakeRetirement(tk))
                {
                    ThreeKingdomsEngine.RetireGeneral(tk,tk.Clock.ActivePlayerId,tk.WindowId,n=>Draw(s.Random,n));
                    RefreshB2(s); return;
                }
                if(tk.Clock.Phase==ThreeKingdomsPhase.Preparation) B2SkillEngine.OnB4Preparation(CardHost(s),"preparation-"+tk.Clock.TurnInstanceId);
                if(tk.Clock.Phase==ThreeKingdomsPhase.Judgment) B2SkillEngine.OnB4JudgmentStart(CardHost(s),"judgment-"+tk.Clock.TurnInstanceId);
                if(tk.Clock.Phase==ThreeKingdomsPhase.Play) B2SkillEngine.OnB4PlayStart(CardHost(s),"play-"+tk.Clock.PhaseId,tk.Clock.ActivePlayerId);
                FinishB2Work(s);
                if(B2Busy(s)||s.PendingDecision!=null) return;
                if(s.ThreeKingdomsEndPlayRequested && tk.Clock.Phase==ThreeKingdomsPhase.Play)
                { s.ThreeKingdomsEndPlayRequested=false;ApplyB2(s,new Command {Id="end-request-"+tk.Clock.PhaseId,PlayerId=tk.Clock.ActivePlayerId,Kind=CommandKind.EndTurn,WindowId=tk.WindowId});continue; }
                if(tk.Automation?.PendingDecision!=null) return;
                if(tk.Clock.Phase==ThreeKingdomsPhase.Play)
                { B4AutomationEngine.Offer(CardHost(s)); return; }
                // 重复控制位置表明调度器没有进展；明确拒绝并保留脱敏轨迹，绝不伪装为正常结束。
                var position=tk.Clock.TurnInstanceId+":"+tk.Clock.PhaseId+":"+tk.Clock.Phase+":"+s.Phase;
                trace.Enqueue(position); if(trace.Count>32) trace.Dequeue();
                if(!seen.Add(position)) throw new InvalidOperationException("自动阶段没有进展："+string.Join(" → ",trace));
                var kind=tk.Clock.Phase==ThreeKingdomsPhase.Roll?CommandKind.RollDice:CommandKind.AdvanceThreeKingdomsPhase;
                ApplyB2(s,new Command { Id="auto-"+tk.Clock.TurnInstanceId+"-"+tk.Clock.PhaseId,
                    PlayerId=tk.Clock.ActivePlayerId,Kind=kind,WindowId=tk.WindowId });
            }
        }

        private void ProjectB4Automation(PlayerView view, ThreeKingdomsView tk)
        {
            if(!B4(state)) return;
            if(state.ThreeKingdoms.PendingDecision==null && tk.SkillDecision==null)
                B4AutomationEngine.Project(CardHost(state),tk,view.PlayerId);
        }
        private IEnumerable<Command> B4RetirementActions(string player)
        {
            var tk=state.ThreeKingdoms;
            if(!B4(state)||!tk.OpeningComplete||tk.Clock.Round==0||tk.Clock.ActivePlayerId!=player||tk.PendingDecision!=null) yield break;
            foreach(var phase in new[]{ThreeKingdomsPhase.Preparation,ThreeKingdomsPhase.Judgment,ThreeKingdomsPhase.Roll,
                ThreeKingdomsPhase.Draw,ThreeKingdomsPhase.Play,ThreeKingdomsPhase.End})
                yield return new Command {Kind=CommandKind.RetireGeneral,PlayerId=player,WindowId=tk.WindowId,RetireAtPhase=phase};
            if(tk.Automation?.RetirementIntents.Any(i=>i.PlayerId==player)==true)
                yield return new Command {Kind=CommandKind.RetireGeneral,PlayerId=player,WindowId=tk.WindowId,CancelRetirementIntent=true};
        }
    }
}
