using System;
using System.Collections.Generic;
using System.Linq;

namespace Catan.Core.ThreeKingdoms
{
    public enum B45SkillKind { Active, Triggered, Passive, Mixed }

    public sealed class B45SkillInfo
    {
        public string SkillId { get; internal set; }
        public B45SkillKind Kind { get; internal set; }
        public bool HasActiveEntry => Kind == B45SkillKind.Active || Kind == B45SkillKind.Mixed;
        public string Timing { get; internal set; }
        public string Cost { get; internal set; }
        public string Limit { get; internal set; }
        public string Surface { get; internal set; }
        public string CommitBoundary { get; internal set; }
        public SkillDefinition Definition => SkillId.StartsWith("tk-",StringComparison.Ordinal) ? GeneralCatalog.Get(SkillId.Substring(0,6))?.Skills.FirstOrDefault(s=>s.Id==SkillId) : null;
        public string Name => Definition?.Name ?? (SkillId==B45SkillCatalog.RepairSkillId?"常规修复":SkillId);
        public string Description => (Definition?.OriginalText ?? "") + "\n时机：" + Timing + "\n费用：" + Cost + "\n次数：" + Limit + "\n提交：" + CommitBoundary;
    }

    // 名称和牌文只用于展示；分类与选择由稳定技能身份及合法命令字段确定。
    public static class B45SkillCatalog
    {
        static B45SkillInfo S(string id,B45SkillKind kind,string timing,string cost,string limit,string surface,string boundary="确认提交后结算，未提交选择可撤销") => new B45SkillInfo {SkillId="tk-"+id,Kind=kind,Timing=timing,Cost=cost,Limit=limit,Surface=surface,CommitBoundary=boundary};
        static readonly B45SkillInfo[] items = {
            S("g04-s1",B45SkillKind.Triggered,"任意玩家回合开始前","限定机会","每任一次","响应条"),
            S("g04-s2",B45SkillKind.Triggered,"自己回合结束","无","每次结束事件","银行牌带"),
            S("g46-s1",B45SkillKind.Active,"自己出牌阶段","交出1张，要求1至2张","每阶段共两次","头像、手牌、声明牌带"),
            S("g46-s2",B45SkillKind.Passive,"建筑新建或升级支付前","唯一最多加1；唯一最少减1","每次建造","资源牌带"),
            S("g47-s1",B45SkillKind.Active,"自己出牌阶段","限定机会","每任一次","两个地块"),
            S("g47-s2",B45SkillKind.Triggered,"自己回合结束","随机均分双方手牌","每次结束事件","头像"),
            S("g48-s1",B45SkillKind.Active,"自己出牌阶段","限定机会","每任一次","两个头像"),
            S("g48-s2",B45SkillKind.Active,"自己出牌阶段","查看并取得一张手牌","每阶段一次；牌种与上次不同","头像、私密牌带","提交查看目标即使用本次机会"),
            S("g50-s1",B45SkillKind.Triggered,"登场时","可协商即时转让条件","每次登场","头像、资源牌带"),
            S("g50-s2",B45SkillKind.Passive,"登场获得蓝黄牌；对敌发展牌效果翻倍","无","持续","发展牌与说明卡"),
            S("g01-s1",B45SkillKind.Triggered,"摸牌阶段替代","放弃普通产出","每次主分配","头像与候选带"),
            S("g05-s1",B45SkillKind.Active,"自己出牌阶段","1麦","每出牌阶段一次","手牌、地块"),
            S("g05-s2",B45SkillKind.Active,"自己出牌阶段","2砖","每出牌阶段一次","手牌、头像"),
            S("g06-s1",B45SkillKind.Triggered,"自己结束阶段","后续禁摸","每次结束事件","响应条、银行牌带"),
            S("g07-s1",B45SkillKind.Mixed,"使用、支付、交易真实牌时","真实底牌","按每张声明","手牌支付、声明带、各席响应条"),
            S("g08-s1",B45SkillKind.Triggered,"自己结束阶段","无","每次结束事件","头像"),
            S("g08-s2",B45SkillKind.Active,"自己出牌阶段","双方共四种真实牌各1张","每出牌阶段一次","头像、本人手牌、授权私密牌带","提交目标并进入私密选牌即占用次数；之后不可退回次数或已见信息"),
            S("g09-s1",B45SkillKind.Triggered,"自己结束阶段","无","每次结束事件","头像"),
            S("g12-s1",B45SkillKind.Triggered,"自己出牌建造类型变化","无","每次合格建造","银行牌带"),
            S("g13-s1",B45SkillKind.Triggered,"任意回合结束","限定机会","每任一次","响应条、银行牌带"),
            S("g13-s2",B45SkillKind.Triggered,"正式骰或判定改骰窗口","所选地块对应真实牌1张","每组依窗口规则","手牌、骰子、地块"),
            S("g14-s1",B45SkillKind.Triggered,"实体发展牌完整生效后","无","每任最多4；隐退残留一次","自动结算或响应条"),
            S("g15-s1",B45SkillKind.Mixed,"自己的合法回合窗口；赠牌奖励事件","赠出任意正数手牌","赠予不限；每累计2张获奖励","手牌、头像、银行牌带"),
            S("g19-s1",B45SkillKind.Triggered,"正式投掷前和候选选择","无","每次正式骰窗口","骰子与候选带"),
            S("g19-s2",B45SkillKind.Passive,"空手时持续生效","无","持续","说明卡"),
            S("g21-s1",B45SkillKind.Triggered,"七点整批弃牌后","无","每次七点批次","自动结算"),
            S("g21-s2",B45SkillKind.Triggered,"正式骰固定结果窗口","无","每任最多3次","骰子与候选带"),
            S("g22-s1",B45SkillKind.Active,"自己出牌阶段","1至4张手牌；或1张非分值发展牌","两分支各每回合一次","手牌、发展牌"),
            S("g23-s1",B45SkillKind.Triggered,"失去最后手牌后","无","每次真实牌流","银行牌带"),
            S("g23-s2",B45SkillKind.Mixed,"指定效果免疫；出牌阶段弃牌","主动分支1张手牌","弃牌每出牌阶段一次","手牌；免疫说明卡"),
            S("g26-s1",B45SkillKind.Active,"自己出牌阶段","交出全部手牌","每出牌阶段一次","头像、本人手牌分堆、授权候选带"),
            S("g26-s2",B45SkillKind.Triggered,"他人回合失去或获得达到阈值","无","失去和获得各跨2张一次","自动结算"),
            S("g28-s1",B45SkillKind.Triggered,"摸牌阶段替代","放弃普通产出","每次主分配","公开候选牌带"),
            S("g28-s2",B45SkillKind.Triggered,"未失牌的回合末","无","每次合格回合末","自动结算"),
            S("g29-s1",B45SkillKind.Active,"自己出牌阶段","限定机会","每任一次","两个头像"),
            S("g29-s2",B45SkillKind.Triggered,"七点弃牌事件后","无","每次合格批次","银行牌带"),
            S("g30-s1",B45SkillKind.Triggered,"回合外其他玩家退牌","无","每来源每回合最多2木","退牌候选带"),
            S("g30-s2",B45SkillKind.Mixed,"主动翻面；回合外结束事件","主动无费用；额外机会7木","翻面每出牌一次；额外机会每正常机会最多3次","技能条、手牌、响应条"),
            S("g33-s1",B45SkillKind.Triggered,"最终骰点满足条件；自己用发展牌后","无","每次合格事件","银行牌带或自动结算"),
            S("g33-s2",B45SkillKind.Passive,"持有和使用发展牌时","无","持续","说明卡"),
            S("g34-s1",B45SkillKind.Triggered,"自己回合开始","全部手牌","每次真实开始事件","虚拟用途带、棋盘或头像"),
            S("g34-s2",B45SkillKind.Triggered,"回合外他人用牌成功后","后续可能令对方弃1","每次合格用牌","响应条、公开展示带、弃牌手牌"),
            S("g37-s1",B45SkillKind.Triggered,"摸牌阶段替代","放弃普通产出","每次主分配","两个地块、银行牌带"),
            S("g37-s2",B45SkillKind.Triggered,"成为他人技能或发展牌目标后","无","每次合格目标事件","银行牌带"),
            S("g39-s1",B45SkillKind.Active,"自己出牌阶段","声明手牌子集；胜负弃牌","不限次数","头像、手牌、声明带、猜测响应条"),
            S("g39-s2",B45SkillKind.Active,"自己出牌阶段","限定机会","每任一次","一次确认、银行牌带"),
            S("g40-s1",B45SkillKind.Mixed,"主动猜牌偷取；自己回合从他人获牌","无","每出牌一次；猜中返次数","头像、声明类型带；被动自动结算"),
            S("g41-s1",B45SkillKind.Triggered,"合格建筑新建或升级后","无","标记上限3","自动结算与技能标记"),
            S("g41-s2",B45SkillKind.Triggered,"任意回合末有3武库","无","每任觉醒一次","自动结算"),
            S("g41-s3",B45SkillKind.Mixed,"支付、交易或自己出牌虚拟牌用途","1武库转资源用途；3武库加1底牌转发展牌","每回合共用一次","手牌、用途带、棋盘或头像"),
            S("g42-s1",B45SkillKind.Passive,"主动使用、支付、交易","真实牌按配对转用途","按本次支付","手牌支付用途"),
            S("g42-s2",B45SkillKind.Triggered,"回合外每张真实失牌","无","逐张响应","可选响应条"),
            S("g43-s1",B45SkillKind.Triggered,"最终骰点3／5／6／8／10／12","无","按循环标记","自动结算或银行／颜色候选带"),
            S("g44-s1",B45SkillKind.Triggered,"自己的正式骰奇数结果","无","每次奇数可继续","骰子、重掷响应条"),
            S("g44-s2",B45SkillKind.Passive,"支付、使用或交易商品","真实商品视为对应资源","按本次支付","手牌支付用途"),
            S("g45-s1",B45SkillKind.Active,"自己出牌阶段","目标交1手牌","不限次数","头像、目标本人手牌"),
            S("g45-s2",B45SkillKind.Triggered,"自己回合结束且未失牌","无","每次合格结束事件","自动结算"),
            S("g49-s1",B45SkillKind.Triggered,"回合外真实失牌批次","无","每批判定；田最多3","自动判定、骰子响应"),
            S("g49-s2",B45SkillKind.Active,"自己出牌阶段","1田","依剩余田","头像"),
            S("g52-s1",B45SkillKind.Triggered,"自己回合实际获牌且非自书来源","无","每批一次","自动结算"),
            S("g52-s2",B45SkillKind.Active,"自己出牌阶段","两张非分值发展牌，至少1黄","每出牌阶段一次","发展牌、垄断类型候选带")
        };
        public const string RepairSkillId = "b5-repair";
        // B5已有规则枚举决定是否可用，目录只补足底栏入口和原牌文说明。
        static readonly string[] b5Active = { "tk-g02-s2", "tk-g03-s2", "tk-g10-s1", "tk-g11-s2", "tk-g11-s3", "tk-g16-s1", "tk-g17-s1", "tk-g18-s1", "tk-g18-s2", "tk-g24-s1", "tk-g31-s1", "tk-g32-s2", "tk-g35-s1", "tk-g51-s1", "tk-g53-s2", "tk-g54-s1", "tk-g54-s2" };
        static readonly B45SkillInfo[] later = GeneralCatalog.B5Definitions.SelectMany(g=>g.Skills).Where(s=>!items.Any(i=>i.SkillId==s.Id))
            .Select(s=>new B45SkillInfo {SkillId=s.Id,Kind=b5Active.Contains(s.Id)?B45SkillKind.Mixed:s.OriginalText.StartsWith("锁定技",StringComparison.Ordinal)?B45SkillKind.Passive:B45SkillKind.Triggered,
                Timing="依原牌文及当前合法时机",Cost="依当前合法用途",Limit="依原牌文与剩余次数",Surface="底栏、手牌、头像与候选带",CommitBoundary="选择只保留草稿；确认后由规则校验并结算"}).ToArray();
        static readonly B45SkillInfo repair = new B45SkillInfo {SkillId=RepairSkillId,Kind=B45SkillKind.Active,Timing="自己的出牌阶段",Cost="所选损害对象的修复费用",Limit="依当前合法修复对象",Surface="底栏与手牌",CommitBoundary="确认后支付并修复"};
        public static B45SkillInfo[] AvailableEntries(ThreeKingdomsView view,string playerId)
        {
            var own=view?.Players?.FirstOrDefault(p=>p.PlayerId==playerId);
            var ids=string.IsNullOrEmpty(own?.ActiveGeneralId)?Array.Empty<string>():GeneralCatalog.Get(own.ActiveGeneralId).Skills.Select(s=>s.Id).ToArray();
            if(view?.SkillDecision==null&&view?.PendingDecision==null)
                ids=ids.Concat((view?.SkillActions??Array.Empty<B2SkillAction>()).Select(a=>a.Command.Option==RepairSkillId?RepairSkillId:a.Command.SkillId).Where(id=>id!=null)).Distinct().ToArray();
            return ids.Select(Get).Where(info=>info!=null).ToArray();
        }
        public static B45SkillInfo[] All => items.Where(i=>GeneralCatalog.IsB4Enabled(i.SkillId.Substring(0,6))).ToArray();
        public static B45SkillInfo Get(string skillId) => skillId==RepairSkillId?repair:items.FirstOrDefault(s=>s.SkillId==skillId)??later.FirstOrDefault(s=>s.SkillId==skillId);
    }

    public sealed class B45SelectionContract
    {
        public string SkillId { get; internal set; }
        public string ViewerId { get; internal set; }
        public string WindowId { get; internal set; }
        public string DecisionId { get; internal set; }
        public string DecisionKind { get; internal set; }
        public bool IsResponse { get; internal set; }
        public bool DirectBatch { get; internal set; }
        public string Description { get; internal set; }
        public B2SkillAction[] Actions { get; internal set; } = Array.Empty<B2SkillAction>();
        public int[] Hand { get; internal set; } = new int[8];
        public bool CanCancel => !IsResponse;
        public B2SkillCommand SkipCommand => Actions.FirstOrDefault(a=>IsSkip(a.Command))?.Command;
        public static bool IsSkip(B2SkillCommand c) => c!=null && new[]{"skip","automation-skip","keep","cancel","decline","luoshen-stop","pass"}.Contains(c.Option);
        public static B45SelectionContract Build(ThreeKingdomsView view,string viewerId,string skillId,int[] hand)
        {
            var d=view?.SkillDecision;
            var actions=(view?.SkillActions??Array.Empty<B2SkillAction>()).Where(a=>a?.Command!=null && (skillId==null||skillId==B45SkillCatalog.RepairSkillId&&a.Command.Option==B45SkillCatalog.RepairSkillId||a.Command.SkillId==skillId)).ToArray();
            if(d!=null&&d.PlayerId!=viewerId)actions=Array.Empty<B2SkillAction>();
            return new B45SelectionContract {SkillId=skillId??actions.FirstOrDefault()?.Command.SkillId,ViewerId=viewerId,WindowId=view?.WindowId,DecisionId=d?.PlayerId==viewerId?d.DecisionId:null,DecisionKind=d?.PlayerId==viewerId?d.Kind:null,IsResponse=d?.PlayerId==viewerId,DirectBatch=GeneralCatalog.UsesDirectPackage(view?.PackageVersion),
                Description=d?.PlayerId==viewerId?d.Description:B45SkillCatalog.Get(skillId)?.Description??"选择牌与目标后确认。",Actions=actions,Hand=hand?.Length==8?(int[])hand.Clone():new int[8]};
        }
        public static B2SkillCommand Copy(B2SkillCommand c) => c==null?null:new B2SkillCommand {SkillId=c.SkillId,Option=c.Option,WindowId=c.WindowId,DecisionId=c.DecisionId,CardType=c.CardType,EffectiveCardType=c.EffectiveCardType,ProgressCardId=c.ProgressCardId,
            Counts=c.Counts==null?null:(int[])c.Counts.Clone(),SecondaryCounts=c.SecondaryCounts==null?null:(int[])c.SecondaryCounts.Clone(),TargetPlayerIds=(c.TargetPlayerIds??Array.Empty<string>()).ToArray(),TileIds=(c.TileIds??Array.Empty<string>()).ToArray(),ProgressCardIds=c.ProgressCardIds?.ToArray()};
        public static string[] Players(B2SkillAction a) => a.SelectionPlayerIds??a.Command.TargetPlayerIds??Array.Empty<string>();
        public static string[] Board(B2SkillAction a) => a.SelectionBoardIds??a.Command.TileIds??Array.Empty<string>();
        public static string[] Progress(B2SkillAction a) => a.Command.ProgressCardIds??((a.Command.Option=="exchange-progress"||a.Command.Option=="civil-juece-progress")?new[]{a.Command.ProgressCardId}:Array.Empty<string>());
        public static bool Batch(B2SkillAction a) => a.Command.Option=="gift"||a.Command.Option=="exchange-hand";
        public static int[] Cost(B2SkillAction a)
        {
            var c=a.Command;var cost=new int[8];
            if(a.SelectionCost!=null)return (int[])a.SelectionCost.Clone();
            if(new[]{"b5-repair","b5m-liyu","b5m-luanji","civil-lianhuan","civil-pay-bricks"}.Contains(c.Option))return (int[])(c.Counts??cost).Clone();
            if(new[]{"b5m-resist","b5m-tax","civil-select","civil-qingnang"}.Contains(c.Option)&&c.CardType>=0&&c.CardType<8){cost[c.CardType]=1;return cost;}
            if(c.Option=="civil-qixi-hand"||c.Option=="civil-qixi-progress"){cost[1]=c.Option=="civil-qixi-hand"?1:2;return cost;}
            if(new[]{"gift","exchange-hand","discard","response-guose","response-liuli"}.Contains(c.Option))return (c.Counts??cost).ToArray();
            if(new[]{"modify","b4-miewu-progress","b4-give","response-zhiyu-discard"}.Contains(c.Option)&&c.CardType>=0&&c.CardType<8)cost[c.CardType]=1;
            return cost;
        }
        public static string OptionKey(B2SkillAction a)
        {
            var c=a.Command;bool real=Cost(a).Sum()>0||c.Option=="b4-select";
            string scalar=real||a.RequiredCardCount.HasValue?"":c.CardType.ToString();
            string progress=c.Option=="exchange-progress"||c.ProgressCardIds?.Length>0?"":c.ProgressCardId??"";
            if(a.VirtualCardId!=null) progress=a.VirtualCardId+":"+a.ResultCardType+(Board(a).Length>0||Players(a).Length>0?"":":"+progress);
            if(c.Option=="response-liuli")progress="";
            if(c.Option=="civil-qingnang")scalar+=":"+c.EffectiveCardType;
            return c.Option+"|"+scalar+"|"+progress+"|"+(c.Option=="fixed"||c.Option=="modify"||c.Option=="raid"||c.Option=="luoying"?string.Join(",",c.Counts??Array.Empty<int>()):"");
        }
    }
}
