using System;
using System.Linq;
using System.Runtime.Serialization;

namespace Catan.Core.ThreeKingdoms
{
    [DataContract] public sealed class B4RetirementIntent
    {
        [DataMember(Order=1)] public string PlayerId { get; set; }
        [DataMember(Order=2)] public ThreeKingdomsPhase Phase { get; set; }
        [DataMember(Order=3)] public long CreatedTurnId { get; set; }
    }
    [DataContract] public sealed class B4AutomationState
    {
        [DataMember(Order=1)] public long TurnId { get; set; }
        [DataMember(Order=2)] public string[] Asked { get; set; } = Array.Empty<string>();
        [DataMember(Order=3)] public B2SkillDecision PendingDecision { get; set; }
        [DataMember(Order=4)] public string PendingSkillId { get; set; }
        [DataMember(Order=5)] public string PendingTenureId { get; set; }
        [DataMember(Order=6)] public bool Accepted { get; set; }
        [DataMember(Order=7)] public B4RetirementIntent[] RetirementIntents { get; set; } = Array.Empty<B4RetirementIntent>();
    }

    // 只管理主动技能的首次提示与可保存的换将意图，强制/事件技能仍使用原效果队列。
    public static class B4AutomationEngine
    {
        public static bool Enabled(ThreeKingdomsState s) => s?.RulesVersion == GeneralCatalog.B4RulesVersion;
        public static B4AutomationState Clone(B4AutomationState a) => a == null ? null : new B4AutomationState {
            TurnId = a.TurnId, Asked = (string[])a.Asked.Clone(), PendingSkillId = a.PendingSkillId,
            PendingTenureId = a.PendingTenureId, Accepted = a.Accepted,
            PendingDecision = a.PendingDecision == null ? null : new B2SkillDecision { PlayerId=a.PendingDecision.PlayerId,
                DecisionId=a.PendingDecision.DecisionId, WindowId=a.PendingDecision.WindowId, Title=a.PendingDecision.Title,
                Description=a.PendingDecision.Description, Kind=a.PendingDecision.Kind, FrameId=a.PendingDecision.FrameId },
            RetirementIntents = a.RetirementIntents.Select(i=>new B4RetirementIntent { PlayerId=i.PlayerId,
                Phase=i.Phase, CreatedTurnId=i.CreatedTurnId }).ToArray()
        };
        private static B4AutomationState State(ThreeKingdomsState s) => s.Automation ?? (s.Automation = new B4AutomationState());
        private static string Key(ThreeKingdomsState s, string tenure, string skill) => s.Clock.PhaseId + ":" + tenure + ":" + skill;
        private static string Id(ThreeKingdomsState s) => "auto-decision-" + s.NextId++;
        public static void Refresh(ThreeKingdomsState s)
        {
            if (!Enabled(s)) return;
            var a = State(s);
            if (a.TurnId != s.Clock.TurnInstanceId) { a.TurnId=s.Clock.TurnInstanceId; a.Asked=Array.Empty<string>(); Clear(a); }
            if (a.PendingDecision != null && !s.Generals.Any(g => g.Region==GeneralRegion.Active && g.TenureId==a.PendingTenureId)) Clear(a);
            if (a.PendingDecision != null && a.PendingDecision.WindowId != s.WindowId)
            { a.PendingDecision.WindowId=s.WindowId; a.PendingDecision.DecisionId=Id(s); }
        }
        public static bool Offer(IThreeKingdomsCardHost host)
        {
            var s=host.State; if (!Enabled(s)) return false; Refresh(s); var a=State(s);
            if (a.PendingDecision != null) return true;
            var owner=s.Clock.ActivePlayerId;
            var general=s.Generals.SingleOrDefault(g => g.Region==GeneralRegion.Active && g.OwnerPlayerId==owner);
            if (general==null) return false;
            var view=new ThreeKingdomsView(); B2SkillEngine.Project(host, view, owner);
            var action=(view.SkillActions??Array.Empty<B2SkillAction>()).FirstOrDefault(x => !string.IsNullOrEmpty(x.Command.SkillId) &&
                !a.Asked.Contains(Key(s,general.TenureId,x.Command.SkillId)));
            if (action==null) return false;
            var skill=GeneralCatalog.Get(general.GeneralId).Skills.First(x => x.Id==action.Command.SkillId);
            a.PendingSkillId=skill.Id; a.PendingTenureId=general.TenureId;
            a.PendingDecision=new B2SkillDecision { PlayerId=owner,DecisionId=Id(s),WindowId=s.WindowId,
                Kind="automation-offer",Title="是否发动"+skill.Name,Description="发动后选择目标或费用；放弃本次提示后仍可从技能入口发动。" };
            return true;
        }
        public static void Resolve(IThreeKingdomsCardHost host, string playerId, B2SkillCommand c)
        {
            var s=host.State; var a=State(s); var d=a.PendingDecision;
            if (d==null || d.PlayerId!=playerId || c.DecisionId!=d.DecisionId || c.WindowId!=d.WindowId || c.SkillId!=a.PendingSkillId)
                throw new InvalidOperationException("技能询问身份或窗口已失效。");
            if (!a.Asked.Contains(Key(s,a.PendingTenureId,a.PendingSkillId))) a.Asked=a.Asked.Concat(new[]{Key(s,a.PendingTenureId,a.PendingSkillId)}).ToArray();
            if (c.Option=="automation-skip") { Clear(a); return; }
            if (!a.Accepted)
            {
                if(c.Option!="automation-activate") throw new InvalidOperationException("请选择发动或放弃。");
                a.Accepted=true; d.Kind="automation-action"; d.Title="发动技能：选择用途"; d.DecisionId=Id(s); return;
            }
            // 实际技能入口自行校验任次、费用和目标；询问令牌不冒充原技能决策令牌。
            var command=Json.Copy(c); command.DecisionId=null; command.WindowId=s.WindowId;
            Clear(a); B2SkillEngine.Resolve(host,playerId,command);
        }
        public static void Project(IThreeKingdomsCardHost host, ThreeKingdomsView view, string viewer)
        {
            var s=host.State; var a=s.Automation; if (!Enabled(s)||a?.PendingDecision==null) return;
            var d=a.PendingDecision;
            view.SkillDecision=new B2SkillDecision { PlayerId=d.PlayerId,DecisionId=viewer==d.PlayerId?d.DecisionId:null,
                WindowId=viewer==d.PlayerId?d.WindowId:null,Title=viewer==d.PlayerId?d.Title:"等待玩家选择技能",Kind=d.Kind,
                Description=viewer==d.PlayerId?d.Description:null };
            if(viewer!=d.PlayerId) { view.SkillActions=Array.Empty<B2SkillAction>(); return; }
            B2SkillAction Make(string option,string label) => new B2SkillAction { Label=label,Command=new B2SkillCommand {
                SkillId=a.PendingSkillId,Option=option,DecisionId=d.DecisionId,WindowId=d.WindowId } };
            if(!a.Accepted) view.SkillActions=new[]{Make("automation-activate","发动"),Make("automation-skip","放弃")};
            else
            {
                var raw=new ThreeKingdomsView(); B2SkillEngine.Project(host,raw,viewer);
                view.SkillActions=(raw.SkillActions??Array.Empty<B2SkillAction>()).Where(x=>x.Command.SkillId==a.PendingSkillId).ToArray();
                foreach(var action in view.SkillActions) { action.Command.DecisionId=d.DecisionId; action.Command.WindowId=d.WindowId; }
                view.SkillActions=view.SkillActions.Concat(new[]{Make("automation-skip","取消发动")}).ToArray();
            }
        }
        public static void SetRetirement(ThreeKingdomsState s,string player,ThreeKingdomsPhase? phase,bool cancel)
        {
            if(!Enabled(s)||s.Clock.ActivePlayerId!=player||!s.OpeningComplete||s.Clock.Round==0)
                throw new InvalidOperationException("只能在自己的实际回合设置隐退意图。");
            var a=State(s);
            if(cancel) { if(!a.RetirementIntents.Any(i=>i.PlayerId==player)) throw new InvalidOperationException("没有可取消的隐退意图。"); a.RetirementIntents=a.RetirementIntents.Where(i=>i.PlayerId!=player).ToArray(); return; }
            if(!phase.HasValue || phase<ThreeKingdomsPhase.Preparation || phase>ThreeKingdomsPhase.End)
                throw new InvalidOperationException("请选择合法的隐退阶段。");
            a.RetirementIntents=a.RetirementIntents.Where(i=>i.PlayerId!=player).Concat(new[]{new B4RetirementIntent { PlayerId=player,Phase=phase.Value,CreatedTurnId=s.Clock.TurnInstanceId }}).ToArray();
        }
        public static bool TakeRetirement(ThreeKingdomsState s)
        {
            var a=s.Automation; var intent=a?.RetirementIntents.FirstOrDefault(i=>i.PlayerId==s.Clock.ActivePlayerId);
            if(intent==null || intent.PlayerId!=s.Clock.ActivePlayerId || intent.Phase!=s.Clock.Phase) return false;
            a.RetirementIntents=a.RetirementIntents.Where(i=>i.PlayerId!=intent.PlayerId).ToArray(); return true;
        }
        public static string[] Validate(ThreeKingdomsState s)
        {
            if(!Enabled(s)||s.Automation==null) return Array.Empty<string>();
            var a=s.Automation;
            if(a.Asked==null||a.RetirementIntents==null||a.Asked.Distinct().Count()!=a.Asked.Length
                ||a.RetirementIntents.Any(i=>i==null||!s.SeatPlayerIds.Contains(i.PlayerId)||i.Phase<ThreeKingdomsPhase.Preparation
                    ||i.Phase>ThreeKingdomsPhase.End||i.CreatedTurnId<1||i.CreatedTurnId>s.Clock.TurnInstanceId)
                ||a.RetirementIntents.Select(i=>i.PlayerId).Distinct().Count()!=a.RetirementIntents.Length)
                return new[]{"自动提示或各席隐退意图无效。"};
            var d=a.PendingDecision;
            if(d!=null&&(string.IsNullOrWhiteSpace(d.DecisionId)||string.IsNullOrWhiteSpace(d.WindowId)
                ||!s.SeatPlayerIds.Contains(d.PlayerId)||string.IsNullOrWhiteSpace(a.PendingSkillId)
                ||string.IsNullOrWhiteSpace(a.PendingTenureId))) return new[]{"自动技能询问身份不完整。"};
            return Array.Empty<string>();
        }
        private static void Clear(B4AutomationState a) { a.PendingDecision=null;a.PendingSkillId=null;a.PendingTenureId=null;a.Accepted=false; }
    }
}
