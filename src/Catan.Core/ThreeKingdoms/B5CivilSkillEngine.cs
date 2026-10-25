using System;
using System.Collections.Generic;
using System.Linq;

namespace Catan.Core.ThreeKingdoms
{
    public static partial class B2SkillEngine
    {
        private static B5CivilState Civil(ThreeKingdomsState s) => s.CivilB5 ?? (s.CivilB5 = new B5CivilState());
        private static B2SkillFrame CivilFrame(ThreeKingdomsState s, GeneralInstance g, string skill, string kind, string source = null)
        {
            var f = Frame(s,g,skill,kind,0,source);
            f.B4 = new B4SkillPayload { OwnerPlayerId=g.OwnerPlayerId };
            f.CivilB5 = new B5CivilPayload { OwnerPlayerId=g.OwnerPlayerId,RootEventId=source??f.Id };
            return f;
        }
        private static string CivilOwner(B2SkillFrame f) => f.CivilB5?.OwnerPlayerId ?? f.PlayerId;
        private static bool CivilReady(IThreeKingdomsCardHost h) => B5Enabled(h?.State) && h is IThreeKingdomsB5Host;
        private static bool CivilProtected(IThreeKingdomsCardHost h,string p) => IsProtected(h.State,p) || (Active(h.State,p)?.GeneralId=="tk-g19" && h.GetHand(p).Sum()==0);
        private static void CivilReward(ThreeKingdomsState s,B2SkillFrame f,string kind,int count)
        { if(count>0) Append(s,new B2SkillFrame { Id=Id(s,"civil-reward"),SkillId=f.SkillId,TenureId=f.TenureId,PlayerId=CivilOwner(f),Kind=kind,Remaining=count,Started=true,SourceEventId=f.Id,B4=new B4SkillPayload {OwnerPlayerId=CivilOwner(f)} }); }
        private static B2SkillAction CivilAction(ThreeKingdomsState s,string skill,string option,string label,string target=null,string objectId=null,int type=-1,B2SkillDecision d=null)
        {
            var a=Action(s,skill,option,label,d); a.Command.ProgressCardId=objectId;a.Command.CardType=type;
            if(target!=null)a.Command.TargetPlayerIds=new[]{target};
            if(type>=0&&type<8){a.RealCardType=type;a.CardSelectionKind="payment";}
            return a;
        }
        private static bool SameCivilAction(B2SkillCommand a,B2SkillCommand b) => SameB4Action(a,b) &&
            (a.TileIds??Array.Empty<string>()).SequenceEqual(b.TileIds??Array.Empty<string>()) &&
            (a.Counts??new int[8]).SequenceEqual(b.Counts??new int[8]) && a.EffectiveCardType==b.EffectiveCardType;
        private static void AddCivilActions(IThreeKingdomsCardHost host,GeneralInstance g,List<B2SkillAction> actions)
        {
            if(!CivilReady(host))return;var s=host.State;var bh=(IThreeKingdomsB5Host)host;var player=g.OwnerPlayerId;
            if(s.Clock.ActivePlayerId!=player || s.Clock.Phase!=ThreeKingdomsPhase.Play)return;
            var hand=host.GetHand(player);bool loss=IsB4HandLossForbidden(s,player);
            if(g.GeneralId=="tk-g03"&&!loss&&hand.Sum()>0&&Value(g,"fanjian",s.Clock.PhaseId)==0)
                foreach(var target in s.SeatPlayerIds.Where(p=>p!=player)) actions.Add(CivilAction(s,"tk-g03-s2","civil-fanjian","反间：令 "+target+" 猜牌",target));
            if(g.GeneralId=="tk-g11")
            {
                if(Value(g,"luanwu",0)==0)actions.Add(CivilAction(s,"tk-g11-s2","civil-luanwu","乱武：依次令其他玩家支付两砖或处理道路"));
                if(Value(g,"jianshu",0)==0){var others=s.SeatPlayerIds.Where(p=>p!=player).ToArray();for(int i=0;i<others.Length;i++)for(int j=i+1;j<others.Length;j++){var a=CivilAction(s,"tk-g11-s3","civil-jianshu","间书：指定 "+others[i]+" 与 "+others[j]);a.Command.TargetPlayerIds=new[]{others[i],others[j]};actions.Add(a);}}
            }
            if(g.GeneralId=="tk-g16")foreach(var o in bh.GetBoardOptions(player,"kurou"))actions.Add(CivilAction(s,"tk-g16-s1","civil-kurou","苦肉："+o.Label,null,o.Id));
            if(g.GeneralId=="tk-g17"&&!loss&&Value(g,"qixi",s.Clock.PhaseId)<3)
                foreach(var target in s.SeatPlayerIds.Where(p=>p!=player&&!CivilProtected(host,p)))
                {
                    if(hand[1]>=1&&host.GetHand(target).Sum()>0&&!IsB4HandLossForbidden(s,target))actions.Add(CivilAction(s,"tk-g17-s1","civil-qixi-hand","奇袭：支付一砖，弃 "+target+" 一张手牌",target));
                    if(hand[1]>=2&&host.GetProgressCards(target).Any(p=>!p.PublicPoint)&&!IsB4HandLossForbidden(s,target))actions.Add(CivilAction(s,"tk-g17-s1","civil-qixi-progress","奇袭：支付两砖，弃 "+target+" 一张发展牌",target));
                    foreach(var o in bh.GetBoardOptions(player,"downgrade-tech",target).Where(o=>o.Level>0&&hand[1]>=o.Level))actions.Add(CivilAction(s,"tk-g17-s1","civil-qixi-tech","奇袭：支付 "+o.Level+" 砖，"+o.Label,target,o.Id));
                }
            if(g.GeneralId=="tk-g31"&&Value(g,"fencheng",0)==0)
                foreach(var p in s.SeatPlayerIds)actions.Add(CivilAction(s,"tk-g31-s1","civil-fencheng","焚城：从 "+p+" 开始",p));
            if(g.GeneralId=="tk-g32"&&!loss&&hand.Sum()>=2&&Value(g,"yicheng",0)==0&&bh.GetVirtualProgressUses(player,"play").Any(u=>u.CardId=="Taxation"))actions.Add(CivilAction(s,"tk-g32-s2","civil-yicheng","疑城：弃两张手牌，视为使用主教"));
            if(g.GeneralId=="tk-g35"&&!loss)
                for(int n=1;n<=hand[4];n++){var a=CivilAction(s,"tk-g35-s1","civil-lianhuan","连环：弃 "+n+" 张矿石换随机资源");a.Command.Counts[4]=n;a.RealCardType=4;a.CardSelectionKind="payment";a.AvailableCount=hand[4];actions.Add(a);}
            if(g.GeneralId=="tk-g51"&&g.Counters.Count(c=>c.Key.StartsWith("liegong:",StringComparison.Ordinal)&&c.Value>0)>=7)
            {
                foreach(var o in bh.GetBoardOptions(player,"liegong").Where(o=>!CivilProtected(host,o.OtherPlayerId??o.PlayerId)))actions.Add(CivilAction(s,"tk-g51-s1","civil-liegong","烈弓："+o.Label,null,o.Id));
                if(bh.GetBoardOptions(player,"liegong-road").Count(o=>!CivilProtected(host,o.OtherPlayerId??o.PlayerId))>=2)actions.Add(CivilAction(s,"tk-g51-s1","civil-liegong-roads","烈弓：选择两条不同道路"));
            }
            if(g.GeneralId=="tk-g54")
            {
                if(Value(g,"chujia",0)==0)actions.Add(CivilAction(s,"tk-g54-s2","civil-chujia","除痂：本局村庄与城市不会倒塌"));
                if(!loss&&Value(g,"qingnang",s.Clock.PhaseId)==0)
                    foreach(var o in bh.GetBoardOptions(player,"repair"))foreach(int real in Enumerable.Range(0,8).Where(t=>hand[t]>0))foreach(int effective in EffectiveCardTypes(s,player,real))
                    {var a=CivilAction(s,"tk-g54-s1","civil-qingnang","青囊：弃"+CardNames[real]+"，"+o.Label,null,o.Id,real);a.Command.EffectiveCardType=effective;a.EffectiveCardType=effective;actions.Add(a);}
            }
        }
        private static bool ResolveCivilActive(IThreeKingdomsCardHost host,GeneralInstance g,B2SkillCommand c)
        {
            if(c.Option==null||!c.Option.StartsWith("civil-",StringComparison.Ordinal))return false;
            var legal=new List<B2SkillAction>();AddCivilActions(host,g,legal);Require(legal.Any(a=>SameCivilAction(a.Command,c)),"B5 技能的时机、目标、费用或次数无效。");
            var s=host.State;var bh=(IThreeKingdomsB5Host)host;var f=CivilFrame(s,g,c.SkillId,c.Option);f.Started=true;f.Targets=(string[])c.TargetPlayerIds.Clone();f.CivilB5.Declared=c.CardType;
            if(c.Option=="civil-fanjian")Counter(g,"fanjian",s.Clock.PhaseId).Value=1;
            if(c.Option=="civil-luanwu"){Counter(g,"luanwu",0).Value=1;f.Targets=Order(s).Where(p=>p!=g.OwnerPlayerId).ToArray();}
            if(c.Option=="civil-jianshu")
            {Counter(g,"jianshu",0).Value=1;for(int i=0;i<2;i++)Civil(s).Jianshu=Civil(s).Jianshu.Concat(new[]{new B5JianshuEffect {Id=f.Id+":"+i,SourcePlayerId=g.OwnerPlayerId,TenureId=g.TenureId,PlayerId=f.Targets[i],OtherPlayerId=f.Targets[1-i]}}).ToArray();Append(s,f);foreach(var t in f.Targets)Target(host,f.Id,g.OwnerPlayerId,t);return true;}
            if(c.Option=="civil-kurou")f.CivilB5.BoardOption=bh.GetBoardOptions(g.OwnerPlayerId,"kurou").Single(o=>o.Id==c.ProgressCardId);
            if(c.Option.StartsWith("civil-qixi-",StringComparison.Ordinal))
            {
                Counter(g,"qixi",s.Clock.PhaseId).Value++;int cost=c.Option=="civil-qixi-hand"?1:2;
                if(c.Option=="civil-qixi-tech"){f.CivilB5.BoardOption=bh.GetBoardOptions(g.OwnerPlayerId,"downgrade-tech",f.Targets[0]).Single(o=>o.Id==c.ProgressCardId);cost=f.CivilB5.BoardOption.Level;}
                Append(s,f);var cards=new int[8];cards[1]=cost;CivilPay(host,g.OwnerPlayerId,cards,f.Id+":cost");Target(host,f.Id,g.OwnerPlayerId,f.Targets[0]);return true;
            }
            if(c.Option=="civil-fencheng")
            {Counter(g,"fencheng",0).Value=1;int index=Array.IndexOf(s.SeatPlayerIds,c.TargetPlayerIds[0]);f.Targets=s.SeatPlayerIds.Skip(index).Concat(s.SeatPlayerIds.Take(index)).ToArray();}
            if(c.Option=="civil-yicheng")f.Remaining=2;
            if(c.Option=="civil-lianhuan")
            {f.Remaining=c.Counts[4];Append(s,f);B4Discard(host,g.OwnerPlayerId,c.Counts,f.Id+":cost");return true;}
            if(c.Option=="civil-liegong"){f.CivilB5.BoardOption=bh.GetBoardOptions(g.OwnerPlayerId,"liegong").Single(o=>o.Id==c.ProgressCardId);f.Targets=new[]{f.CivilB5.BoardOption.OtherPlayerId??f.CivilB5.BoardOption.PlayerId};}
            if(c.Option=="civil-liegong"||c.Option=="civil-liegong-roads")g.Counters=g.Counters.Where(x=>!x.Key.StartsWith("liegong:",StringComparison.Ordinal)).ToArray();
            if(c.Option=="civil-qingnang")
            {
                f.CivilB5.BoardOption=bh.GetBoardOptions(g.OwnerPlayerId,"repair").Single(o=>o.Id==c.ProgressCardId);
                f.Targets=new[]{f.CivilB5.BoardOption.OtherPlayerId??f.CivilB5.BoardOption.PlayerId};
                int effective=c.EffectiveCardType??c.CardType;Counter(g,"qingnang",s.Clock.PhaseId).Value=f.CivilB5.BoardOption.Cost[effective]>0?0:1;
                Append(s,f);if(effective!=c.CardType)CommitCardConversion(host,f.Id+":conversion",g.OwnerPlayerId,c.CardType,effective);
                B4Discard(host,g.OwnerPlayerId,One(c.CardType),f.Id+":cost");foreach(var t in f.Targets.Where(t=>t!=g.OwnerPlayerId))Target(host,f.Id,g.OwnerPlayerId,t);return true;
            }
            if(c.Option=="civil-chujia")Counter(g,"chujia",0).Value=1;
            Append(s,f);foreach(var t in f.Targets.Where(t=>t!=g.OwnerPlayerId))Target(host,f.Id,g.OwnerPlayerId,t);return true;
        }
        private static void CivilPay(IThreeKingdomsCardHost host,string player,int[] cards,string eventId)
        {
            Require(!IsB4HandLossForbidden(host.State,player)&&Contains(host.GetHand(player),cards),"支付的真实手牌不足或禁止失去牌。");
            int before=host.GetHand(player).Sum();host.RemoveHand(player,cards);host.AddBank(cards);HoldReturnedCards(host,player,"payment",eventId,cards);
            RecordCards(host,eventId,new[]{new B2HandChange {PlayerId=player,BeforeCount=before,AfterCount=before-cards.Sum(),LostCount=cards.Sum(),LostCards=Copy(cards)}});
            OnCivilPaid(host,eventId,player,Enumerable.Range(0,8).Where(t=>cards[t]>0).ToArray());
        }
    }
}
