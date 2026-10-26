using System;
using System.Collections.Generic;
using System.Linq;

namespace Catan.Core.ThreeKingdoms
{
    public static partial class B2SkillEngine
    {
        private static bool ProcessCivilFrame(IThreeKingdomsCardHost host,B2SkillFrame f,out bool waiting)
        {
            waiting=false;if(!f.Kind.StartsWith("civil-",StringComparison.Ordinal))return false;
            Require(CivilReady(host),"旧规则包不能结算 B5 棋盘技能。");var s=host.State;var bh=(IThreeKingdomsB5Host)host;var p=f.CivilB5;var owner=CivilOwner(f);
            if(f.Kind=="civil-jieyin"||f.Kind=="civil-xianfu")
            {Decide(s,f,"civil-relation",Name(f.SkillId)+"：选择其他玩家","关系持续至本次武将隐退。");waiting=true;return true;}
            if(f.Kind=="civil-jianshu"){Remove(s,f);return true;}
            if(f.Kind=="civil-yingzi")
            {Remove(s,f);if(!IsDrawBlocked(s,owner)){CivilReward(s,f,"mixed",1);CivilReward(s,f,"random",1);}return true;}
            if(f.Kind=="civil-hengzheng")
            {
                if(f.Step==0){var a=s.Skills.Harvest.FirstOrDefault(a=>a.PlayerId==owner);if(a!=null){host.AddBank(a.Cards);a.Cards=new int[8];a.GoldCount=0;a.AqueductEligible=false;a.ChoiceComplete=true;a.Completed=true;a.ReplacementKind="hengzheng";}f.Step=1;foreach(var t in f.Targets)Target(host,f.Id,owner,t);return true;}
                if(f.Step>f.Targets.Length){Remove(s,f);return true;}var target=f.Targets[f.Step++-1];
                if(!CivilProtected(host,target,owner))B4Transfer(host,target,owner,RandomHand(host,target),f.Id+":take:"+target,f.SkillId);return true;
            }
            if(f.Kind=="civil-fanjian")
            {
                if(f.Step==0){f.PlayerId=f.Targets[0];Decide(s,f,"civil-declare-type","反间：声明牌种","选择一种资源或商品，然后随机取得对方一张真实手牌。");waiting=true;return true;}
                if(f.Step==1)
                {
                    f.Step=2;f.PlayerId=owner;var cards=RandomHand(host,owner);int real=Array.FindIndex(cards,n=>n>0);p.Budget=real>=0&&real!=p.Declared?1:0;
                    if(real<0){Remove(s,f);return true;}s.Skills.RevealedCards=Copy(cards);B4SkillHistoryEngine.Complete(s,f.Id,"对方声明"+CardNames[p.Declared]+"，公开取得"+CardNames[real]+(p.Budget==1?"；猜错":"；猜中"));
                    B4Transfer(host,owner,f.Targets[0],cards,f.Id+":give",f.SkillId,true);return true;
                }
                if(p.Budget==0||CivilProtected(host,f.Targets[0],owner)||!bh.GetBoardOptions(owner,"damage-road",f.Targets[0]).Any()){Remove(s,f);return true;}
                f.PlayerId=owner;p.BoardKind="damage-road";p.Argument=f.Targets[0];Decide(s,f,"civil-road","反间：处理目标一条道路","开放道路移除，封闭道路横置。");waiting=true;return true;
            }
            if(f.Kind=="civil-kurou")
            {var current=bh.GetBoardOptions(owner,"kurou").FirstOrDefault(o=>o.Id==p.BoardOption.Id);Remove(s,f);if(current!=null){bh.ApplyBoardOption(owner,current);CivilReward(s,f,"mixed",current.PaidCount);}return true;}
            if(f.Kind.StartsWith("civil-qixi-",StringComparison.Ordinal))
            {
                Remove(s,f);string target=f.Targets[0];if(CivilProtected(host,target,owner))return true;
                if(f.Kind=="civil-qixi-hand")B4DiscardRandom(host,target,1,f.Id+":discard");
                if(f.Kind=="civil-qixi-progress")CivilDiscardProgress(host,target,null,f.Id+":discard-progress",true);
                if(f.Kind=="civil-qixi-tech"){var o=bh.GetBoardOptions(owner,"downgrade-tech",target).FirstOrDefault(o=>o.Id==p.BoardOption.Id);if(o!=null)bh.ApplyBoardOption(owner,o);}return true;
            }
            if(f.Kind=="civil-luanwu")
            {
                if(f.Step>=f.Targets.Length){Remove(s,f);return true;}var target=f.Targets[f.Step];
                if(CivilProtected(host,target,owner)){f.Step++;return true;}
                if(p.BoardKind=="damage-road")
                {
                    f.PlayerId=owner;p.Argument=target;
                    if(!bh.GetBoardOptions(owner,"damage-road",target).Any()){f.Step++;p.BoardKind=null;return true;}
                    Decide(s,f,"civil-road","乱武：处理 "+target+" 的道路","由贾诩选择开放移除或封闭横置的道路。");waiting=true;return true;
                }
                f.PlayerId=target;Decide(s,f,"civil-luanwu","乱武：支付两砖或承受道路处理","不足两砖不能选择支付；无道路时处罚无效果。");waiting=true;return true;
            }
            if(f.Kind=="civil-jianshu-duel")
            {
                if(f.Targets.Any(t=>!B6AllianceRules.CanAttack(s,owner,t))){Remove(s,f);return true;}
                if(f.Step==0){f.PlayerId=f.Targets[0];if(host.GetHand(f.PlayerId).Sum()==0){Remove(s,f);return true;}Decide(s,f,"civil-subset","间书：暗选手牌","至少选择一张真实手牌，随后声明其中矿石数量。");waiting=true;return true;}
                if(f.Step==1){f.PlayerId=f.Targets[0];Decide(s,f,"civil-stone-declare","间书：声明矿石数量","所选手牌尚未向对方公开。");waiting=true;return true;}
                f.PlayerId=f.Targets[1];Decide(s,f,"civil-guess","间书：猜测声明真假","对方选择 "+p.Selection.Sum()+" 张牌，声明矿石 "+p.Declared+" 张。");waiting=true;return true;
            }
            if(f.Kind=="civil-benghuai")
            {p.BoardKind="damage-road";p.Argument=null;if(!bh.GetBoardOptions(owner,p.BoardKind).Any()){Remove(s,f);return true;}Decide(s,f,"civil-road","崩坏：选择一条道路","可选择包括自己在内的一名玩家的道路。");waiting=true;return true;}
            if(f.Kind=="civil-jieyin-discard")
            {
                if(IsB4HandLossForbidden(s,owner)||host.GetHand(owner).Sum()==0){Remove(s,f);return true;}
                f.Remaining=Math.Min(f.Remaining,host.GetHand(owner).Sum());
                if(p.Selection.Sum()>=f.Remaining){var cards=Copy(p.Selection);Remove(s,f);B4Discard(host,owner,cards,f.Id+":mirror");return true;}
                Decide(s,f,"civil-hand-select","结姻：同步弃牌","选择 "+f.Remaining+" 张手牌，不足时弃尽。");waiting=true;return true;
            }
            if(f.Kind=="civil-copy")
            {
                var options=bh.GetBoardOptions(owner,p.BoardKind,null,p.Budget);if(options.Length==0||p.BoardKind=="free-military"&&p.Budget==0){Remove(s,f);return true;}
                Decide(s,f,"civil-copy",Name(f.SkillId)+"：免费建设",p.BoardKind=="free-military"?"余等级预算 "+p.Budget+"，各次按建成等级扣减。":"仍须符合库存、连接及前置条件。");waiting=true;return true;
            }
            if(f.Kind=="civil-chouce")
            {
                if(!s.SeatPlayerIds.Any(t=>host.GetHand(t).Sum()>0&&!CivilProtected(host,t,owner)&&!IsB4HandLossForbidden(s,t))){Remove(s,f);return true;}
                if(p.OtherPlayerId==owner)
                {if(p.Selection.Sum()>=f.Remaining){var cards=Copy(p.Selection);Remove(s,f);B4Discard(host,owner,cards,f.Id+":self");return true;}Decide(s,f,"civil-hand-select","筹策：选择自己的弃牌","请选择实际持有的手牌。");waiting=true;return true;}
                Decide(s,f,"civil-chouce","筹策：选择弃牌对象","先辅对象弃两张，其他玩家弃一张；他人隐藏牌随机弃置。");waiting=true;return true;
            }
            if(f.Kind=="civil-fencheng")
            {
                if(f.Step>=f.Targets.Length){Remove(s,f);return true;}f.PlayerId=f.Targets[f.Step];
                if(CivilProtected(host,f.PlayerId,owner)){CivilNextTarget(f,0);return true;}
                if(p.BoardKind=="burn-city")
                {if(!bh.GetBoardOptions(owner,"burn-city",f.PlayerId).Any()){CivilNextTarget(f,0);return true;}Decide(s,f,"civil-burn","焚城：选择焚毁城市","城市与城墙完整移除；大都会须有合法迁移处。");waiting=true;return true;}
                if(p.BoardKind=="damage-road")
                {Decide(s,f,"civil-two-roads","焚城：选择两条道路","须选择两条不同的合法道路。");waiting=true;return true;}
                if(p.BoardKind=="discard")
                {Decide(s,f,"civil-fencheng-discard","焚城：选择弃牌","至少弃 "+(p.Budget+1)+" 张；其他连带弃牌不增加下一人的门槛。");waiting=true;return true;}
                Decide(s,f,"civil-fencheng","焚城：选择代价","弃至少 "+(p.Budget+1)+" 张手牌，或处理两条道路，否则焚毁一座城市。");waiting=true;return true;
            }
            if(f.Kind=="civil-juece")
            {
                if(p.Declared<0){p.Declared=0;foreach(var target in f.Targets)Target(host,f.Id,owner,target);return true;}
                if(f.Step>=f.Targets.Length){Remove(s,f);return true;}f.PlayerId=f.Targets[f.Step];
                if(CivilProtected(host,f.PlayerId,owner)||!bh.GetBoardOptions(owner,"damage-road",f.PlayerId).Any()&&(!host.GetProgressCards(f.PlayerId).Any(c=>!c.PublicPoint)||IsB4HandLossForbidden(s,f.PlayerId))){f.Step++;return true;}
                Decide(s,f,"civil-juece","绝策：道路或发展牌","自己选择一条道路或一张未使用发展牌。");waiting=true;return true;
            }
            if(f.Kind=="civil-pojun")
            {
                if(!B6AllianceRules.CanAttack(s,owner,f.Targets[0])){Remove(s,f);return true;}
                if(p.Budget==0||!Enumerable.Range(0,3).Any(i=>bh.GetImprovements(f.Targets[0])[i]>p.Tracks[i])){CivilCommitPojun(host,f);return true;}
                Decide(s,f,"civil-pojun","破军：分配扣置科技","三项合计还可扣置 "+p.Budget+" 级，也可立即完成。");waiting=true;return true;
            }
            if(f.Kind=="civil-weimu")
            {var cs=Civil(s);cs.Markers=cs.Markers.Where(m=>m.Key!="weimu-residual"||m.PlayerId!=owner).Concat(new[]{new B5CivilMarker {PlayerId=owner,Key="weimu-use",Value=f.SourceEventId,TurnId=s.Clock.TurnInstanceId}}).ToArray();Remove(s,f);return true;}
            if(f.Kind=="civil-yicheng")
            {
                if(f.Step==0){Decide(s,f,"civil-hand-select","疑城：选择两张手牌","两张真实手牌作为全部技能费用。");waiting=true;return true;}
                if(f.Step==1){var uses=bh.GetVirtualProgressUses(owner,"play").Where(u=>u.CardId=="Taxation").ToArray();if(uses.Length==0){Remove(s,f);return true;}Decide(s,f,"civil-yicheng-use","疑城：选择主教用途","费用在确认合法用途后一次支付。");waiting=true;return true;}
                var use=p.VirtualUse;Remove(s,f);use.SourceSkillId=f.SkillId;use.SourceEventId=f.Id;bh.UseVirtualProgress(owner,use);return true;
            }
            if(f.Kind=="civil-lianhuan"){Remove(s,f);CivilReward(s,f,"random",f.Remaining);return true;}
            if(f.Kind=="civil-niepan")
            {if(Active(s,owner)?.TenureId!=f.TenureId){Remove(s,f);return true;}if(p.SelectedIds.Length<2){Decide(s,f,"civil-niepan","涅槃：选择两项科技","各失去最高一层，然后弃尽手牌与未使用发展牌并获得永久一分。");waiting=true;return true;}CivilCommitNiepan(host,f);return true;}
            if(f.Kind=="civil-liegong")
            {var o=bh.GetBoardOptions(owner,"liegong").FirstOrDefault(x=>x.Id==p.BoardOption.Id);Remove(s,f);if(o!=null&&!CivilProtected(host,o.OtherPlayerId??o.PlayerId,owner))bh.ApplyBoardOption(owner,o);return true;}
            if(f.Kind=="civil-liegong-roads")
            {
                if(f.Step==1){var available=bh.GetBoardOptions(owner,"liegong-road");var selected=p.SelectedIds.Select(id=>available.FirstOrDefault(o=>o.Id==id)).ToArray();Remove(s,f);foreach(var o in selected.Where(o=>o!=null&&!CivilProtected(host,o.OtherPlayerId??o.PlayerId,owner)))bh.ApplyBoardOption(owner,o);return true;}
                if(bh.GetBoardOptions(owner,"liegong-road").Count(o=>!CivilProtected(host,o.OtherPlayerId??o.PlayerId,owner))<2){Remove(s,f);return true;}Decide(s,f,"civil-two-roads","烈弓：选择两条道路","两条不同合法道路同时锁定后结算。");waiting=true;return true;
            }
            if(f.Kind=="civil-qingnang")
            {var o=bh.GetBoardOptions(owner,"repair").FirstOrDefault(x=>x.Id==p.BoardOption.Id);Remove(s,f);if(o!=null){bh.ApplyBoardOption(owner,o);if((o.OtherPlayerId??o.PlayerId)==owner)CivilReward(s,f,"mixed",1);}return true;}
            if(f.Kind=="civil-chujia"){bh.SetCollapseProtection(owner);Remove(s,f);return true;}
            throw new InvalidOperationException("未知 B5 建设技能帧："+f.Kind);
        }
        private static void CivilNextTarget(B2SkillFrame f,int previousDiscard)
        {f.Step++;f.CivilB5.Budget=previousDiscard;f.CivilB5.Selection=new int[8];f.CivilB5.SelectedIds=Array.Empty<string>();f.CivilB5.BoardKind=null;}
        private static void CivilCommitPojun(IThreeKingdomsCardHost host,B2SkillFrame f)
        {var bh=(IThreeKingdomsB5Host)host;for(int i=0;i<3;i++)if(f.CivilB5.Tracks[i]>0)bh.ChangeImprovement(f.Targets[0],i,-Math.Min(f.CivilB5.Tracks[i],bh.GetImprovements(f.Targets[0])[i]),true);Remove(host.State,f);}
        private static void CivilCommitNiepan(IThreeKingdomsCardHost host,B2SkillFrame f)
        {
            var s=host.State;var bh=(IThreeKingdomsB5Host)host;var owner=CivilOwner(f);var g=Active(s,owner);var tracks=f.CivilB5.SelectedIds.Select(int.Parse).ToArray();
            Require(g?.TenureId==f.TenureId&&tracks.Length==2&&tracks.Distinct().Count()==2&&tracks.All(t=>bh.GetActualImprovements(owner)[t]>0)&&!IsB4HandLossForbidden(s,owner),"涅槃的科技或费用已经失效。");
            Counter(g,"niepan",0).Value=1;var hand=host.GetHand(owner);var cards=host.GetProgressCards(owner).Where(c=>!c.PublicPoint).ToArray();Remove(s,f);
            B4Discard(host,owner,hand,f.Id+":hand");foreach(var c in cards){host.RemoveProgress(owner,c.Id);host.ReturnProgress(c);}OnCivilProgressLost(host,f.Id+":progress",owner,cards.Length);
            foreach(int t in tracks)bh.ChangeImprovement(owner,t,-1);bh.AddPermanentScore(owner,1);
        }
        private static void CivilDiscardProgress(IThreeKingdomsCardHost host,string target,string cardId,string eventId,bool random=false)
        {
            if(IsB4HandLossForbidden(host.State,target))return;var cards=host.GetProgressCards(target).Where(c=>!c.PublicPoint).ToArray();if(cards.Length==0)return;
            var card=random?cards[host.DrawRandom(cards.Length)]:cards.FirstOrDefault(c=>c.Id==cardId);if(card==null)return;
            host.RemoveProgress(target,card.Id);host.ReturnProgress(card);OnCivilProgressLost(host,eventId,target,1);
        }
    }
}
