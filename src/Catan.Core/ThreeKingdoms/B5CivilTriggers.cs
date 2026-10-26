using System;
using System.Collections.Generic;
using System.Linq;

namespace Catan.Core.ThreeKingdoms
{
    public static partial class B2SkillEngine
    {
        public static void OnCivilGeneralActivated(ThreeKingdomsState s,GeneralInstance g)
        {
            if(!B5Enabled(s))return;
            if(g.GeneralId=="tk-g11")Civil(s).Markers=Civil(s).Markers.Where(m=>m.Key!="weimu-residual"||m.PlayerId!=g.OwnerPlayerId).ToArray();
            if(g.GeneralId=="tk-g27"||g.GeneralId=="tk-g36")
                Append(s,CivilFrame(s,g,g.GeneralId+"-s1",g.GeneralId=="tk-g27"?"civil-jieyin":"civil-xianfu","entry:"+g.TenureId));
        }
        public static void OnCivilGeneralRetired(ThreeKingdomsState s,GeneralInstance g)
        {
            if(!B5Enabled(s))return;var cs=Civil(s);
            cs.Relations=cs.Relations.Where(r=>r.TenureId!=g.TenureId).ToArray();
            if(g.GeneralId=="tk-g11")
            {cs.Markers=cs.Markers.Where(m=>m.Key!="weimu-residual"||m.PlayerId!=g.OwnerPlayerId).Concat(new[]{new B5CivilMarker {PlayerId=g.OwnerPlayerId,Key="weimu-residual",Value=g.TenureId}}).ToArray();}
        }
        public static bool CivilDrawBlocked(ThreeKingdomsState s,string player)
        {
            if(!B5Enabled(s))return false;
            if(s.CivilB5?.Markers.Any(m=>m.PlayerId==player&&m.Key=="draw-block"&&m.TurnId==s.Clock.TurnInstanceId)==true)return true;
            return Active(s,player)?.GeneralId=="tk-g25"&&s.Clock.ActivePlayerId==player&&
                (s.Clock.Phase==ThreeKingdomsPhase.Draw||s.Clock.Phase==ThreeKingdomsPhase.Roll||s.Skills.HarvestActive);
        }
        public static void OnCivilHarvestStart(IThreeKingdomsCardHost host,string eventId)
        {
            if(!CivilReady(host)||!Mark(host.State,"civil-harvest:"+eventId))return;var s=host.State;var cs=Civil(s);var bh=(IThreeKingdomsB5Host)host;
            // 两次间书各自只消费指定者的下一次真实摸牌；翻面跳过没有调用此入口。
            foreach(var e in cs.Jianshu.Where(e=>e.PlayerId==s.Clock.ActivePlayerId).ToArray())
            {
                cs.Jianshu=cs.Jianshu.Where(x=>x.Id!=e.Id).ToArray();
                if(host.GetHand(e.PlayerId).Sum()==0)continue;
                Append(s,new B2SkillFrame { Id=Id(s,"jianshu"),Kind="civil-jianshu-duel",SkillId="tk-g11-s3",TenureId=e.TenureId,PlayerId=e.PlayerId,Targets=new[]{e.PlayerId,e.OtherPlayerId},Started=true,SourceEventId=e.Id,
                    B4=new B4SkillPayload {OwnerPlayerId=e.SourcePlayerId},CivilB5=new B5CivilPayload {OwnerPlayerId=e.SourcePlayerId,RootEventId=e.Id,OtherPlayerId=e.OtherPlayerId} });
            }
            var hands=s.SeatPlayerIds.ToDictionary(p=>p,p=>host.GetHand(p).Sum());var scores=s.SeatPlayerIds.ToDictionary(p=>p,p=>bh.GetScore(p));
            foreach(var player in Order(s))
            {
                var g=Active(s,player);if(g==null)continue;
                if(g.GeneralId=="tk-g03"&&!IsDrawBlocked(s,player))
                {var f=CivilFrame(s,g,"tk-g03-s1","civil-yingzi",eventId);Append(s,f);}
                if(g.GeneralId=="tk-g25"&&s.Clock.ActivePlayerId!=player&&!IsDrawBlocked(s,player)&&
                    (s.SeatPlayerIds.Where(p=>p!=player).All(p=>hands[player]<hands[p])||s.SeatPlayerIds.Where(p=>p!=player).All(p=>scores[player]<scores[p])))
                {var f=CivilFrame(s,g,"tk-g25-s2","civil-hengzheng",eventId);f.Optional=true;f.Targets=Order(s).Where(p=>p!=player).ToArray();Append(s,f);}
            }
        }
        public static void OnCivilPlayStart(IThreeKingdomsCardHost host,string eventId,string player)
        {
            if(!CivilReady(host)||!Mark(host.State,"civil-play:"+eventId))return;var s=host.State;var bh=(IThreeKingdomsB5Host)host;
            Civil(s).Markers=Civil(s).Markers.Where(m=>!new[]{"root","draw-block","weimu-use"}.Contains(m.Key)||m.TurnId>=s.Clock.TurnInstanceId).ToArray();
            bh.RestoreImprovements(player);
            var g=Active(s,player);
            if(g?.GeneralId=="tk-g35"&&Value(g,"niepan",0)==0&&!IsB4HandLossForbidden(s,player)&&bh.GetActualImprovements(player).Count(n=>n>0)>=2&&s.SeatPlayerIds.All(p=>bh.GetScore(player)<=bh.GetScore(p)))
            {var f=CivilFrame(s,g,"tk-g35-s2","civil-niepan",eventId);f.Optional=true;Append(s,f);}
        }
        public static void OnCivilEnd(IThreeKingdomsCardHost host,string eventId)
        {
            if(!CivilReady(host)||!Mark(host.State,"civil-end:"+eventId))return;var s=host.State;var bh=(IThreeKingdomsB5Host)host;var g=Active(s,s.Clock.ActivePlayerId);if(g==null)return;
            if(g.GeneralId=="tk-g25"&&!s.SeatPlayerIds.Where(p=>p!=g.OwnerPlayerId).All(p=>bh.GetScore(g.OwnerPlayerId)>bh.GetScore(p))&&bh.GetBoardOptions(g.OwnerPlayerId,"damage-road").Any())
            {var f=CivilFrame(s,g,"tk-g25-s1","civil-benghuai",eventId);f.Optional=true;Append(s,f);}
            if(g.GeneralId=="tk-g31")
            {
                var targets=Order(s).Where(p=>host.GetHand(p).Sum()==0&&!CivilProtected(host,p,g.OwnerPlayerId)).ToArray();
                if(targets.Any(p=>bh.GetBoardOptions(g.OwnerPlayerId,"damage-road",p).Any()||host.GetProgressCards(p).Any(c=>!c.PublicPoint)))
                {var f=CivilFrame(s,g,"tk-g31-s2","civil-juece",eventId);f.Optional=true;f.Targets=targets;Append(s,f);}
            }
        }
        public static void OnCivilTargeted(IThreeKingdomsCardHost host,string useId,string source,string target,string effectId)
        {
            if(!CivilReady(host)||!Mark(host.State,"civil-target:"+useId+":"+target))return;var s=host.State;var g=Active(s,target);
            if(g?.GeneralId=="tk-g36")Prepend(s,new[]{CivilFrame(s,g,"tk-g36-s2","civil-chouce",useId)});
        }
        public static void OnCivilProgressTargeted(IThreeKingdomsCardHost host,string useId,string source,string target,int color,bool hasEffectivePart=true)
        {
            if(!CivilReady(host)||!Mark(host.State,"civil-progress-target:"+useId+":"+target))return;var s=host.State;var g=Active(s,source);
            if(color==1&&hasEffectivePart&&Active(s,target)?.GeneralId=="tk-g11")
            {var immune=Active(s,target);string history="civil-weimu:"+useId+":"+target;B4SkillHistoryEngine.Begin(s,history,target,"tk-g11-s1",immune.TenureId,useId);B4SkillHistoryEngine.Complete(s,history,"免疫蓝色发展牌对自己的直接效果");}
            if(color==1&&hasEffectivePart&&Active(s,target)?.GeneralId!="tk-g11")
            {
                var residual=Civil(s).Markers.FirstOrDefault(m=>m.PlayerId==target&&m.Key=="weimu-residual");
                if(residual!=null)Append(s,new B2SkillFrame {Id=Id(s,"weimu"),Kind="civil-weimu",SkillId="tk-g11-s1",PlayerId=target,TenureId=residual.Value,Optional=true,Started=true,SourceEventId=useId,B4=new B4SkillPayload {OwnerPlayerId=target},CivilB5=new B5CivilPayload {OwnerPlayerId=target,RootEventId=useId}});
            }
            if(g?.GeneralId=="tk-g32"&&B6AllianceRules.CanAttack(s,source,target)&&((IThreeKingdomsB5Host)host).GetImprovements(target).Sum()>0)
            {
                int limit=host.GetHand(target).Sum()+host.GetProgressCards(target).Count(p=>!p.PublicPoint);
                if(limit>0){var f=CivilFrame(s,g,"tk-g32-s1","civil-pojun",useId);f.Targets=new[]{target};f.Optional=true;f.CivilB5.Budget=limit;Append(s,f);}
            }
        }
        public static bool CivilResidualOptional(ThreeKingdomsState s,B2SkillFrame f) => B5Enabled(s)&&f.Kind=="civil-weimu"&&s.CivilB5?.Markers.Any(m=>m.Key=="weimu-residual"&&m.PlayerId==f.PlayerId&&m.Value==f.TenureId)==true;
        public static bool CivilBlueImmune(ThreeKingdomsState s,string target,string useId=null) => B5Enabled(s)&&(Active(s,target)?.GeneralId=="tk-g11"||useId!=null&&s.CivilB5?.Markers.Any(m=>m.Key=="weimu-use"&&m.PlayerId==target&&m.Value==useId)==true);
        public static int CivilProgressMultiplier(IThreeKingdomsCardHost host,string source,string target)
        {
            if(!CivilReady(host)||Active(host.State,source)?.GeneralId!="tk-g32")return 1;
            var bh=(IThreeKingdomsB5Host)host;var a=bh.GetImprovements(source);var b=bh.GetImprovements(target);return Enumerable.Range(0,3).All(i=>b[i]<=a[i])?2:1;
        }
        private static string CivilRoot(ThreeKingdomsState s,string eventId)
        {
            if(eventId==null)return "unknown";
            var parent=Civil(s).Markers.Where(m=>m.Key=="root"&&(eventId==m.PlayerId||eventId.StartsWith(m.PlayerId+":",StringComparison.Ordinal))).OrderByDescending(m=>m.PlayerId.Length).FirstOrDefault();
            return parent?.Value??eventId;
        }
        private static void CivilRootMap(ThreeKingdomsState s,string child,string root)
        {if(!Civil(s).Markers.Any(m=>m.Key=="root"&&m.PlayerId==child))Civil(s).Markers=Civil(s).Markers.Concat(new[]{new B5CivilMarker {PlayerId=child,Key="root",Value=root,TurnId=s.Clock.TurnInstanceId}}).ToArray();}
        public static void OnCivilCardsMoved(IThreeKingdomsCardHost host,string batch,B2HandChange[] changes)
        {
            if(!CivilReady(host)||!Mark(host.State,"civil-cards:"+batch))return;var s=host.State;var root=CivilRoot(s,batch);
            foreach(var change in changes.Where(c=>c.LostProgressCount>0))OnCivilProgressLost(host,batch,change.PlayerId,change.LostProgressCount);
            foreach(var relation in Civil(s).Relations.Where(r=>r.Kind=="jieyin").ToArray())
            {
                var c=changes.FirstOrDefault(c=>c.PlayerId==relation.TargetPlayerId&&c.LostCount>0);var g=Active(s,relation.PlayerId);
                if(c==null||g?.TenureId!=relation.TenureId||!Mark(s,"civil-mirror:"+root+":"+relation.TenureId))continue;
                var f=CivilFrame(s,g,"tk-g27-s1","civil-jieyin-discard",root);f.Started=true;f.Remaining=c.LostCount;CivilRootMap(s,f.Id,root);Prepend(s,new[]{f});
            }
        }
        public static void OnCivilBuilt(IThreeKingdomsCardHost host,string eventId,string player,string buildingType,int level,string rootEventId=null)
        {
            if(!CivilReady(host)||!Mark(host.State,"civil-built:"+eventId))return;var s=host.State;var root=rootEventId??CivilRoot(s,eventId);var bh=(IThreeKingdomsB5Host)host;
            foreach(var r in Civil(s).Relations.Where(r=>r.TargetPlayerId==player).ToArray())
            {
                var g=Active(s,r.PlayerId);if(g?.TenureId!=r.TenureId)continue;
                string kind=null;if(r.Kind=="jieyin"&&new[]{"Road","Ship","Settlement","City","Wall","CityWall"}.Contains(buildingType))kind="free-"+(buildingType=="CityWall"?"Wall":buildingType);
                if(r.Kind=="xianfu"&&new[]{"Knight","Improvement"}.Contains(buildingType))kind="free-military";
                if(kind==null||!Mark(s,"civil-copy:"+root+":"+r.TenureId))continue;
                int budget=kind=="free-military"?level:0;if(!bh.GetBoardOptions(r.PlayerId,kind,null,budget).Any())continue;
                var f=CivilFrame(s,g,g.GeneralId+"-s1","civil-copy",root);f.Optional=true;f.CivilB5.BoardKind=kind;f.CivilB5.Budget=budget;CivilRootMap(s,f.Id,root);Append(s,f);
            }
        }
        public static void OnCivilProgressLost(IThreeKingdomsCardHost host,string eventId,string player,int count)
        {
            if(!CivilReady(host)||count<=0||!Mark(host.State,"civil-progress-loss:"+eventId+":"+player))return;var s=host.State;var g=Active(s,player);
            if(g?.GeneralId!="tk-g27"||Value(g,"xiaoji",s.Clock.TurnInstanceId)>0)return;
            Counter(g,"xiaoji",s.Clock.TurnInstanceId).Value=1;var f=CivilFrame(s,g,"tk-g27-s2","resource",eventId);f.Remaining=3;Append(s,f);
        }
        public static void OnCivilPaid(IThreeKingdomsCardHost host,string eventId,string player,int[] effectiveTypes,string progressCardId=null)
        {
            if(!CivilReady(host)||!Mark(host.State,"civil-paid:"+eventId))return;var s=host.State;var g=Active(s,player);
            if(g?.GeneralId!="tk-g51"||s.Clock.ActivePlayerId!=player||s.Clock.Phase!=ThreeKingdomsPhase.Play)return;
            foreach(var type in (effectiveTypes??Array.Empty<int>()).Where(t=>t>=0&&t<8).Distinct())Counter(g,"liegong:card:"+type,0).Value=1;
            if(!string.IsNullOrEmpty(progressCardId))Counter(g,"liegong:progress:"+progressCardId,0).Value=1;
        }
        public static string[] ValidateCivil(ThreeKingdomsState s)
        {
            if(!B5Enabled(s)||s.CivilB5==null)return Array.Empty<string>();var c=s.CivilB5;var errors=new List<string>();
            if(c.Relations==null||c.Jianshu==null||c.Markers==null)return new[]{"B5 关系与延时状态缺失。"};
            if(c.Relations.Any(r=>r==null||!new[]{"jieyin","xianfu"}.Contains(r.Kind)||!s.SeatPlayerIds.Contains(r.PlayerId)||!s.SeatPlayerIds.Contains(r.TargetPlayerId)||r.PlayerId==r.TargetPlayerId||string.IsNullOrEmpty(r.TenureId)))errors.Add("B5 关系身份无效。");
            if(c.Jianshu.Any(e=>e==null||!s.SeatPlayerIds.Contains(e.PlayerId)||!s.SeatPlayerIds.Contains(e.OtherPlayerId)||e.PlayerId==e.OtherPlayerId||!s.SeatPlayerIds.Contains(e.SourcePlayerId)||string.IsNullOrEmpty(e.Id)))errors.Add("间书延时身份无效。");
            if(c.Markers.Any(m=>m==null||m.Key==null||m.TurnId<0))errors.Add("B5 持有效果无效。");
            foreach(var f in s.Skills.Frames.Where(f=>f.CivilB5!=null)){var p=f.CivilB5;if(!Valid(p.Selection)||p.Tracks==null||p.Tracks.Length!=3||p.Tracks.Any(v=>v<0)||p.Budget<0||p.SelectedIds==null||!s.SeatPlayerIds.Contains(p.OwnerPlayerId))errors.Add("B5 技能选择或预算无效。");}
            return errors.ToArray();
        }
    }
}
