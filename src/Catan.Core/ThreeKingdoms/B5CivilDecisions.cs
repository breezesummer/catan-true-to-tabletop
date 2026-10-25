using System;
using System.Collections.Generic;
using System.Linq;

namespace Catan.Core.ThreeKingdoms
{
    public static partial class B2SkillEngine
    {
        private static void CivilSelectActions(IThreeKingdomsCardHost host,B2SkillFrame f,B2SkillDecision d,List<B2SkillAction> actions,int maximum)
        {
            var p=f.CivilB5;var hand=host.GetHand(f.PlayerId);if(p.Selection.Sum()>=maximum)return;
            for(int t=0;t<8;t++)if(hand[t]>p.Selection[t])
            {var a=CivilAction(host.State,f.SkillId,"civil-select","选择"+CardNames[t],null,null,t,d);a.CardSelectionKind="select";a.AvailableCount=hand[t]-p.Selection[t];a.Command.Counts=Copy(p.Selection);actions.Add(a);}
        }
        private static bool AddCivilDecisionActions(IThreeKingdomsCardHost host,B2SkillFrame f,B2SkillDecision d,List<B2SkillAction> actions)
        {
            if(!d.Kind.StartsWith("civil-",StringComparison.Ordinal))return false;Require(CivilReady(host),"B5 宿主不可用。");
            var s=host.State;var p=f.CivilB5;var owner=CivilOwner(f);var bh=(IThreeKingdomsB5Host)host;
            if(d.Kind=="civil-relation")foreach(var target in s.SeatPlayerIds.Where(t=>t!=owner))actions.Add(CivilAction(s,f.SkillId,"civil-relation","选择 "+target,target,null,-1,d));
            else if(d.Kind=="civil-declare-type")for(int t=0;t<8;t++){var a=CivilAction(s,f.SkillId,"civil-declare-type","声明"+CardNames[t],null,null,t,d);a.CardSelectionKind="declare";actions.Add(a);}
            else if(d.Kind=="civil-road")foreach(var o in bh.GetBoardOptions(owner,p.BoardKind,p.Argument).Where(o=>!CivilProtected(host,o.OtherPlayerId??o.PlayerId)))actions.Add(CivilAction(s,f.SkillId,"civil-road",o.Label,null,o.Id,-1,d));
            else if(d.Kind=="civil-luanwu")
            {
                if(host.GetHand(f.PlayerId)[1]>=2&&!IsB4HandLossForbidden(s,f.PlayerId)){var a=CivilAction(s,f.SkillId,"civil-pay-bricks","支付两张砖",null,null,1,d);a.Command.Counts[1]=2;a.AvailableCount=host.GetHand(f.PlayerId)[1];actions.Add(a);}
                actions.Add(CivilAction(s,f.SkillId,"civil-penalty","承受道路处理",null,null,-1,d));
            }
            else if(d.Kind=="civil-subset")
            {CivilSelectActions(host,f,d,actions,host.GetHand(f.PlayerId).Sum());if(p.Selection.Sum()>0){var a=CivilAction(s,f.SkillId,"civil-selected","确认暗选手牌",null,null,-1,d);a.Command.Counts=Copy(p.Selection);actions.Add(a);}}
            else if(d.Kind=="civil-stone-declare")for(int n=0;n<=p.Selection.Sum();n++)actions.Add(CivilAction(s,f.SkillId,"civil-stone-declare","声明矿石 "+n+" 张",null,null,n,d));
            else if(d.Kind=="civil-guess")
            {actions.Add(CivilAction(s,f.SkillId,"civil-true","猜声明为真",null,null,-1,d));actions.Add(CivilAction(s,f.SkillId,"civil-false","猜声明为假",null,null,-1,d));}
            else if(d.Kind=="civil-hand-select")CivilSelectActions(host,f,d,actions,f.Remaining);
            else if(d.Kind=="civil-copy")
            {foreach(var o in bh.GetBoardOptions(owner,p.BoardKind,null,p.Budget))actions.Add(CivilAction(s,f.SkillId,"civil-copy",o.Label,null,o.Id,-1,d));actions.Add(CivilAction(s,f.SkillId,"civil-finish-copy","放弃余下免费建设",null,null,-1,d));}
            else if(d.Kind=="civil-chouce")foreach(var target in s.SeatPlayerIds.Where(t=>host.GetHand(t).Sum()>0&&!CivilProtected(host,t)&&!IsB4HandLossForbidden(s,t)))actions.Add(CivilAction(s,f.SkillId,"civil-chouce","弃置 "+target+" 的手牌",target,null,-1,d));
            else if(d.Kind=="civil-fencheng")
            {
                if(host.GetHand(f.PlayerId).Sum()>p.Budget&&!IsB4HandLossForbidden(s,f.PlayerId))actions.Add(CivilAction(s,f.SkillId,"civil-fencheng-hand","选择弃置手牌",null,null,-1,d));
                if(bh.GetBoardOptions(owner,"damage-road",f.PlayerId).Length>=2)actions.Add(CivilAction(s,f.SkillId,"civil-fencheng-roads","处理两条道路",null,null,-1,d));
                actions.Add(CivilAction(s,f.SkillId,"civil-fencheng-burn","焚毁城市（无合法城市则无损失）",null,null,-1,d));
            }
            else if(d.Kind=="civil-fencheng-discard")
            {CivilSelectActions(host,f,d,actions,host.GetHand(f.PlayerId).Sum());if(p.Selection.Sum()>p.Budget){var a=CivilAction(s,f.SkillId,"civil-fencheng-commit","弃置选中的 "+p.Selection.Sum()+" 张",null,null,-1,d);a.Command.Counts=Copy(p.Selection);actions.Add(a);}}
            else if(d.Kind=="civil-two-roads")
            {
                string kind=f.Kind=="civil-liegong-roads"?"liegong-road":"damage-road";string arg=f.Kind=="civil-liegong-roads"?null:f.PlayerId;
                foreach(var o in bh.GetBoardOptions(owner,kind,arg).Where(o=>!p.SelectedIds.Contains(o.Id)&&!CivilProtected(host,o.OtherPlayerId??o.PlayerId)))actions.Add(CivilAction(s,f.SkillId,"civil-pick-road",o.Label,null,o.Id,-1,d));
            }
            else if(d.Kind=="civil-burn")foreach(var o in bh.GetBoardOptions(owner,"burn-city",f.PlayerId))actions.Add(CivilAction(s,f.SkillId,"civil-burn",o.Label,null,o.Id,-1,d));
            else if(d.Kind=="civil-juece")
            {
                foreach(var o in bh.GetBoardOptions(owner,"damage-road",f.PlayerId))actions.Add(CivilAction(s,f.SkillId,"civil-juece-road",o.Label,null,o.Id,-1,d));
                if(!IsB4HandLossForbidden(s,f.PlayerId))foreach(var c in host.GetProgressCards(f.PlayerId).Where(c=>!c.PublicPoint).GroupBy(c=>c.Id).Select(g=>g.First()))actions.Add(CivilAction(s,f.SkillId,"civil-juece-progress","弃置"+DevelopmentCardText.Name(c.Id),null,c.Id,-1,d));
            }
            else if(d.Kind=="civil-pojun")
            {for(int t=0;t<3;t++)if(p.Budget>0&&bh.GetImprovements(f.Targets[0])[t]>p.Tracks[t])actions.Add(CivilAction(s,f.SkillId,"civil-pojun-track","扣置"+new[]{"贸易","政治","科技"}[t]+"一级",null,null,t,d));actions.Add(CivilAction(s,f.SkillId,"civil-pojun-done","完成扣置",null,null,-1,d));}
            else if(d.Kind=="civil-yicheng-use")foreach(var use in bh.GetVirtualProgressUses(owner,"play").Where(u=>u.CardId=="Taxation"))actions.Add(CivilAction(s,f.SkillId,"civil-yicheng-use",use.Label,null,use.OptionId,-1,d));
            else if(d.Kind=="civil-niepan")for(int t=0;t<3;t++)if(bh.GetActualImprovements(owner)[t]>0&&!p.SelectedIds.Contains(t.ToString()))actions.Add(CivilAction(s,f.SkillId,"civil-niepan-track","失去"+new[]{"贸易","政治","科技"}[t]+"最高一级",null,null,t,d));
            return true;
        }
        private static bool ResolveCivilDecision(IThreeKingdomsCardHost host,B2SkillFrame f,B2SkillDecision d,B2SkillCommand c)
        {
            if(!d.Kind.StartsWith("civil-",StringComparison.Ordinal))return false;var legal=new List<B2SkillAction>();AddCivilDecisionActions(host,f,d,legal);Require(legal.Any(a=>SameCivilAction(a.Command,c)),"B5 选择已经失效或不属于当前玩家。");
            var s=host.State;var p=f.CivilB5;var owner=CivilOwner(f);var bh=(IThreeKingdomsB5Host)host;
            if(c.Option=="civil-select")
            {p.Selection[c.CardType]++;if(d.Kind=="civil-hand-select"&&f.Kind=="civil-yicheng"&&p.Selection.Sum()==2)f.Step=1;return true;}
            if(c.Option=="civil-relation")
            {Civil(s).Relations=Civil(s).Relations.Where(r=>r.TenureId!=f.TenureId).Concat(new[]{new B5CivilRelation {Kind=f.Kind=="civil-jieyin"?"jieyin":"xianfu",PlayerId=owner,TargetPlayerId=c.TargetPlayerIds[0],TenureId=f.TenureId}}).ToArray();Remove(s,f);Target(host,f.Id,owner,c.TargetPlayerIds[0]);return true;}
            if(c.Option=="civil-declare-type"){p.Declared=c.CardType;f.Step=1;return true;}
            if(c.Option=="civil-road")
            {
                var o=bh.GetBoardOptions(owner,p.BoardKind,p.Argument).Single(o=>o.Id==c.ProgressCardId);bh.ApplyBoardOption(owner,o);
                if(f.Kind=="civil-luanwu"){f.Step++;p.BoardKind=null;}else Remove(s,f);return true;
            }
            if(c.Option=="civil-pay-bricks")
            {string target=f.PlayerId;f.Step++;var two=new int[8];two[1]=2;CivilPay(host,target,two,f.Id+":payment:"+target);return true;}
            if(c.Option=="civil-penalty"){p.BoardKind="damage-road";return true;}
            if(c.Option=="civil-selected"){Require(Contains(host.GetHand(f.PlayerId),p.Selection),"暗选手牌已变化。");f.Step=1;return true;}
            if(c.Option=="civil-stone-declare"){p.Declared=c.CardType;f.Step=2;return true;}
            if(d.Kind=="civil-guess")
            {
                bool truth=p.Selection[4]==p.Declared;bool guessWon=(c.Option=="civil-true")==truth;string winner=guessWon?f.Targets[1]:f.Targets[0],loser=guessWon?f.Targets[0]:f.Targets[1];
                s.Skills.RevealedCards=Copy(p.Selection);Civil(s).Markers=Civil(s).Markers.Concat(new[]{new B5CivilMarker {PlayerId=loser,Key="draw-block",Value=f.Id,TurnId=s.Clock.TurnInstanceId}}).ToArray();
                B4SkillHistoryEngine.Complete(s,f.Id,"间书："+winner+" 胜、"+loser+" 败；败者本回合禁摸，胜者被随机弃一张手牌");Remove(s,f);B4DiscardRandom(host,winner,1,f.Id+":winner-discard");return true;
            }
            if(c.Option=="civil-copy")
            {
                var o=bh.GetBoardOptions(owner,p.BoardKind,null,p.Budget).Single(o=>o.Id==c.ProgressCardId);o.RootEventId=p.RootEventId;
                if(p.BoardKind=="free-military")p.Budget-=o.Level;else Remove(s,f);bh.ApplyBoardOption(owner,o);return true;
            }
            if(c.Option=="civil-finish-copy"){Remove(s,f);return true;}
            if(c.Option=="civil-chouce")
            {
                string target=c.TargetPlayerIds[0];int count=Civil(s).Relations.Any(r=>r.Kind=="xianfu"&&r.PlayerId==owner&&r.TargetPlayerId==target&&r.TenureId==f.TenureId)?2:1;
                if(target==owner){p.OtherPlayerId=owner;f.Remaining=Math.Min(count,host.GetHand(owner).Sum());return true;}
                Remove(s,f);B4DiscardRandom(host,target,count,f.Id+":discard");return true;
            }
            if(c.Option=="civil-fencheng-hand"){p.BoardKind="discard";return true;}
            if(c.Option=="civil-fencheng-roads"){p.BoardKind="damage-road";return true;}
            if(c.Option=="civil-fencheng-burn"){p.BoardKind="burn-city";return true;}
            if(c.Option=="civil-fencheng-commit")
            {Require(Contains(host.GetHand(f.PlayerId),p.Selection),"焚城所选弃牌已变化。");string player=f.PlayerId;var cards=Copy(p.Selection);CivilNextTarget(f,cards.Sum());B4Discard(host,player,cards,f.Id+":discard:"+player);return true;}
            if(c.Option=="civil-pick-road")
            {
                p.SelectedIds=p.SelectedIds.Concat(new[]{c.ProgressCardId}).ToArray();if(p.SelectedIds.Length<2)return true;
                string kind=f.Kind=="civil-liegong-roads"?"liegong-road":"damage-road",arg=f.Kind=="civil-liegong-roads"?null:f.PlayerId;
                var options=bh.GetBoardOptions(owner,kind,arg);var selected=p.SelectedIds.Select(id=>options.FirstOrDefault(o=>o.Id==id)).ToArray();Require(selected.All(o=>o!=null),"所选两条道路不再合法。");
                if(f.Kind=="civil-liegong-roads"){f.Step=1;f.Targets=selected.Select(o=>o.OtherPlayerId??o.PlayerId).Distinct().ToArray();foreach(var target in f.Targets)Target(host,f.Id,owner,target);}
                else {CivilNextTarget(f,0);foreach(var o in selected)bh.ApplyBoardOption(owner,o);}return true;
            }
            if(c.Option=="civil-burn")
            {var o=bh.GetBoardOptions(owner,"burn-city",f.PlayerId).Single(o=>o.Id==c.ProgressCardId);CivilNextTarget(f,0);bh.ApplyBoardOption(owner,o);return true;}
            if(c.Option=="civil-juece-road")
            {var o=bh.GetBoardOptions(owner,"damage-road",f.PlayerId).Single(o=>o.Id==c.ProgressCardId);f.Step++;bh.ApplyBoardOption(owner,o);return true;}
            if(c.Option=="civil-juece-progress"){var target=f.PlayerId;f.Step++;CivilDiscardProgress(host,target,c.ProgressCardId,f.Id+":progress:"+target);return true;}
            if(c.Option=="civil-pojun-track"){p.Tracks[c.CardType]++;p.Budget--;return true;}
            if(c.Option=="civil-pojun-done"){CivilCommitPojun(host,f);return true;}
            if(c.Option=="civil-yicheng-use")
            {
                Require(Contains(host.GetHand(owner),p.Selection)&&p.Selection.Sum()==2&&!IsB4HandLossForbidden(s,owner),"疑城的两张实体费用已失效。");
                p.VirtualUse=bh.GetVirtualProgressUses(owner,"play").Single(u=>u.OptionId==c.ProgressCardId&&u.CardId=="Taxation");Counter(Active(s,owner),"yicheng",0).Value=1;f.Step=2;B4Discard(host,owner,p.Selection,f.Id+":cost");return true;
            }
            if(c.Option=="civil-niepan-track"){p.SelectedIds=p.SelectedIds.Concat(new[]{c.CardType.ToString()}).ToArray();return true;}
            throw new InvalidOperationException("未知 B5 技能选择："+c.Option);
        }
        public static void ProjectCivilB5(IThreeKingdomsCardHost host,ThreeKingdomsView view,string viewer)
        {
            if(!CivilReady(host))return;var s=host.State;var cs=s.CivilB5;if(cs==null)return;
            var status=cs.Relations.Select(r=>r.PlayerId+"："+(r.Kind=="jieyin"?"结姻":"先辅")+" → "+r.TargetPlayerId)
                .Concat(cs.Markers.Where(m=>m.Key=="weimu-residual").Select(m=>m.PlayerId+"：帷幕残留一次"))
                .Concat(cs.Markers.Where(m=>m.Key=="draw-block"&&m.TurnId==s.Clock.TurnInstanceId).Select(m=>m.PlayerId+"：间书本回合禁摸"))
                .Concat(s.Generals.Where(g=>g.Region==GeneralRegion.Active&&g.GeneralId=="tk-g51").Select(g=>g.OwnerPlayerId+"：烈弓已记录 "+g.Counters.Count(c=>c.Key.StartsWith("liegong:",StringComparison.Ordinal)&&c.Value>0)+" 种牌"));
            view.SkillStatus=(view.SkillStatus??Array.Empty<string>()).Concat(status).ToArray();
        }
    }
}
