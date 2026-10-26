using System;
using System.Linq;
using System.Runtime.Serialization;

namespace Catan.Core.ThreeKingdoms
{
    [DataContract] public sealed class B45SkillRuntimeStatus
    {
        [DataMember(Order=1)] public string SkillId {get;set;}
        [DataMember(Order=2)] public string Summary {get;set;}
        [DataMember(Order=3)] public string UnavailableReason {get;set;}
        [DataMember(Order=4)] public bool HasLegalAction {get;set;}
    }
    // 只读取本席在场将及公开计数；不复用带任意键名的内部账本，更不暴露问计牌种等信息。
    public static class B45SkillStatusProjection
    {
        static int Value(GeneralInstance g,string key,long scope) => Math.Max(0,g.Counters.FirstOrDefault(c=>c.Key==key&&c.TurnInstanceId==scope)?.Value??0);
        static int Left(GeneralInstance g,string key,long scope,int maximum=1) => Math.Max(0,maximum-Value(g,key,scope));
        static string Remaining(GeneralInstance g,string key,long scope) => "剩余 "+Left(g,key,scope)+" / 1";
        public static void Project(IThreeKingdomsCardHost host,ThreeKingdomsView view,string viewerId)
        {
            var state=host.State;if(!GeneralCatalog.UsesDirectPackage(state?.PackageVersion)){view.SkillRuntimeStatus=null;return;}
            var general=state.Generals.FirstOrDefault(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId==viewerId);
            if(general==null){view.SkillRuntimeStatus=Array.Empty<B45SkillRuntimeStatus>();return;}
            var phase=state.Clock.PhaseId;var turn=state.Clock.TurnInstanceId;
            view.SkillRuntimeStatus=GeneralCatalog.Get(general.GeneralId).Skills.Select(def=>
            {
                string id=def.Id;var info=B45SkillCatalog.Get(id);string summary=info?.Limit??"依当前规则";
                bool legal=(view.SkillActions??Array.Empty<B2SkillAction>()).Any(a=>a.Command.SkillId==id&&!B45SelectionContract.IsSkip(a.Command));
                string specific=null;
                switch(id)
                {
                    case "tk-g04-s1":summary=Remaining(general,"huoxin",0);if(Left(general,"huoxin",0)==0)specific="本任惑心次数已用。";break;
                    case "tk-g46-s1":summary="剩余 "+Left(general,"fuqi",phase,2)+" / 2";if(Left(general,"fuqi",phase,2)==0)specific="本出牌阶段伏骑次数已用。";break;
                    case "tk-g46-s2":int adjustment=B2SkillEngine.JiaoziAdjustmentB6(host,viewerId);summary=adjustment>0?"建造费用 +1":adjustment<0?"建造费用 −1":"建造费用不变";break;
                    case "tk-g47-s1":summary=Remaining(general,"tamo",0);if(Left(general,"tamo",0)==0)specific="本任榻谟次数已用。";break;
                    case "tk-g48-s1":summary=Remaining(general,"baiyi",0);if(Left(general,"baiyi",0)==0)specific="本任败移次数已用。";break;
                    case "tk-g48-s2":summary=Remaining(general,"jinglue",phase);if(Left(general,"jinglue",phase)==0)specific="本出牌阶段景略次数已用。";break;
                    case "tk-g50-s1":summary="盟友 "+(B6AllianceRules.Members(state,viewerId).Length-1)+" / "+(state.SeatPlayerIds.Length/2);break;
                    case "tk-g50-s2":summary="对敌效果 ×2";break;
                    case "tk-g05-s1":summary=Remaining(general,"liuli",phase);if(Left(general,"liuli",phase)==0)specific="本出牌阶段流离次数已用。";else if(host.GetHand(viewerId)[3]<1)specific="需要1张真实麦子。";break;
                    case "tk-g05-s2":summary=Remaining(general,"guose",phase);if(Left(general,"guose",phase)==0)specific="本出牌阶段国色次数已用。";else if(host.GetHand(viewerId)[1]<2)specific="需要2张真实砖。";break;
                    case "tk-g08-s2":summary=Remaining(general,"poxi",phase);if(Left(general,"poxi",phase)==0)specific="本阶段已提交魄袭目标，次数不会因重选退回。";break;
                    case "tk-g12-s1":int previous=Value(general,"fenyin",phase);summary=previous>=1&&previous<=5?"上次："+new[]{"道路","船","村庄","城市","城墙"}[previous-1]:"本阶段尚无建造记录";break;
                    case "tk-g13-s1":summary=Remaining(general,"lianpo",0);if(Left(general,"lianpo",0)==0)specific="本任连破限定次数已用。";break;
                    case "tk-g14-s1":summary="可截获 "+Left(general,"jianxiong",0,4)+" / 4";break;
                    case "tk-g15-s1":summary="赠牌余数 "+Value(general,"rende",turn)+" / 2";break;
                    case "tk-g21-s2":summary="剩余 "+Left(general,"zhijian",0,3)+" / 3";break;
                    case "tk-g22-s1":int hand=Left(general,"zhiheng-hand",turn),progress=Left(general,"zhiheng-progress",turn);summary="手牌 "+hand+" · 发展 "+progress;if(hand==0&&progress==0)specific="本回合制衡两种用途均已使用。";break;
                    case "tk-g23-s2":summary="弃牌 "+Left(general,"qianxun",phase)+" / 1 · 免疫持续";if(Left(general,"qianxun",phase)==0)specific="本出牌阶段弃牌分支已使用；免疫仍有效。";break;
                    case "tk-g26-s1":summary=Remaining(general,"xiashu",phase);if(Left(general,"xiashu",phase)==0)specific="本出牌阶段下书次数已用。";break;
                    case "tk-g26-s2":summary="失去 "+Math.Min(2,Value(general,"kuanshi-lost",turn))+" / 2 · 获得 "+Math.Min(2,Value(general,"kuanshi-gained",turn))+" / 2";break;
                    case "tk-g29-s1":summary=Remaining(general,"fangzhu",0);if(Left(general,"fangzhu",0)==0)specific="本任放逐限定次数已用。";break;
                    case "tk-g30-s1":summary="每来源每回合至多2木";break;
                    case "tk-g30-s2":summary="翻面 "+Left(general,"jiushi",phase)+" · 额外 "+Left(general,"jiushi-extra",0,3);if(Left(general,"jiushi",phase)==0)specific="本出牌阶段翻面分支已用；额外机会只在真实结束事件响应。";break;
                    case "tk-g39-s2":summary=Remaining(general,"jieming",0);if(Left(general,"jieming",0)==0)specific="本任节命限定次数已用。";break;
                    case "tk-g40-s1":summary=Remaining(general,"daoshu",phase);if(Left(general,"daoshu",phase)==0)specific="本出牌阶段盗书次数已用。";break;
                    case "tk-g41-s1":summary="武库 "+Math.Min(3,Value(general,"wuku",0))+" / 3";break;
                    case "tk-g41-s2":summary=Value(general,"sanchen",0)>0?"已觉醒 · 获得灭吴":"未觉醒 · 回合末检查";break;
                    case "tk-g41-s3":summary="武库 "+Math.Min(3,Value(general,"wuku",0))+" · 剩余 "+Left(general,"miewu",turn);if(Value(general,"sanchen",0)==0)specific="三陈尚未觉醒。";else if(Left(general,"miewu",turn)==0)specific="本回合资源与发展牌用途共用的次数已用。";else if(Value(general,"wuku",0)==0)specific="没有可支付的武库标记。";else if(Value(general,"wuku",0)<3)specific="资源用途可在建造或交易支付中选择；虚拟发展牌还需3武库。";break;
                    case "tk-g43-s1":summary="下一项："+new[]{"随机资源","自选资源","自选商品","自选资源","发展牌"}[Value(general,"qinqin",0)%5];break;
                    case "tk-g49-s1":case "tk-g49-s2":summary="田 "+(general.Counters.Any(c=>c.Key=="tian")?Math.Min(3,Value(general,"tian",0)):2)+" / 3";if(id=="tk-g49-s2"&&general.Counters.Any(c=>c.Key=="tian")&&Value(general,"tian",0)==0)specific="没有可支付的田标记。";break;
                    case "tk-g52-s2":summary=Remaining(general,"xiemu",phase);if(Left(general,"xiemu",phase)==0)specific="本出牌阶段协穆次数已用。";break;
                }
                string reason=null;
                if(!legal)
                {
                    if(specific!=null)reason=specific;
                    else if(info?.HasActiveEntry!=true)reason=info?.Kind==B45SkillKind.Passive?"持续规则生效；此按钮只查看说明。":"由对应真实事件触发；此按钮只查看说明。";
                    else if(view.PendingDecision!=null||view.SkillDecision!=null)reason="请先完成当前待决选择。";
                    else if(id=="tk-g07-s1")reason="在使用或交易的支付入口选择真实底牌与声明。";
                    else if(id!="tk-g23-s2"&&state.Clock.ActivePlayerId!=viewerId)reason="当前不是你的主动操作时机。";
                    else if(B2SkillEngine.IsB4HandLossForbidden(state,viewerId))reason="本出牌阶段禁止失去手牌或发展牌。";
                    else reason="当前时机、真实费用或合法目标不满足，请查看技能条件。";
                }
                return new B45SkillRuntimeStatus {SkillId=id,Summary=summary,UnavailableReason=reason,HasLegalAction=legal};
            }).ToArray();
        }
    }
}
