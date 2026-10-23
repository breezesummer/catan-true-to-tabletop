using System;
using System.Collections.Generic;
using System.Linq;

namespace Catan.Core.ThreeKingdoms
{
    // 百科 W05/W07/W08/W14/W34/W49：持久期限、实体截获、判定来源与私密质疑。
    public static partial class B2SkillEngine
    {
        private static B4ResponseState Response(ThreeKingdomsState s) => s.Response ?? (s.Response = new B4ResponseState());
        public static B4ResponseState CloneB4Response(B4ResponseState r) => r == null ? null : new B4ResponseState
        {
            CaoResiduals = r.CaoResiduals.Select(x => new B4CaoResidual { PlayerId=x.PlayerId, TenureId=x.TenureId, AcquiredCount=x.AcquiredCount }).ToArray(),
            HandLimitPenalties = r.HandLimitPenalties.Select(x => new B4PlayerPenalty { PlayerId=x.PlayerId, Amount=x.Amount }).ToArray(),
            JudgmentSource = r.JudgmentSource == null ? null : CloneFrame(r.JudgmentSource),
            SuspendedFrames = r.SuspendedFrames.Select(CloneFrame).ToArray(), SuspendedHarvest=r.SuspendedHarvest,
            Declaration = CloneDeclaration(r.Declaration)
        };
        private static B4DeclarationBatch CloneDeclaration(B4DeclarationBatch d) => d == null ? null : new B4DeclarationBatch
        {
            EventId=d.EventId, PlayerId=d.PlayerId, TenureId=d.TenureId, RealTypes=(int[])d.RealTypes.Clone(), EffectiveTypes=(int[])d.EffectiveTypes.Clone(),
            OperationJson=d.OperationJson, CardCursor=d.CardCursor, Responders=(string[])d.Responders.Clone(), Challengers=(string[])d.Challengers.Clone(),
            ResponderCursor=d.ResponderCursor, Complete=d.Complete, Success=d.Success
        };
        public static void OnB4GeneralActivated(ThreeKingdomsState s, GeneralInstance g)
        {
            if (!B4Enabled(s)) return;
            if (g.GeneralId == "tk-g49") Counter(g, "tian", 0).Value=2;
            if (g.GeneralId == "tk-g14" && s.Response != null) s.Response.CaoResiduals=s.Response.CaoResiduals.Where(r => r.PlayerId != g.OwnerPlayerId).ToArray();
        }
        public static void OnB4GeneralRetired(ThreeKingdomsState s, GeneralInstance g)
        {
            if (!B4Enabled(s) || g.GeneralId != "tk-g14") return;
            var response=Response(s); response.CaoResiduals=response.CaoResiduals.Where(r => r.PlayerId != g.OwnerPlayerId).ToArray();
            int count=Value(g,"jianxiong",0);
            if (count<4) response.CaoResiduals=response.CaoResiduals.Concat(new[] { new B4CaoResidual { PlayerId=g.OwnerPlayerId, TenureId=g.TenureId, AcquiredCount=count } }).ToArray();
        }
        public static bool IsB4HandLossForbidden(ThreeKingdomsState s, string player) => B4Enabled(s) && s.Clock.Phase==ThreeKingdomsPhase.Play
            && s.Skills.TimedEffects.Any(e => e.Kind=="response-indulgence-active" && e.TargetPlayerId==player && e.CreatedTurnId==s.Clock.TurnInstanceId);
        public static bool IsB4SettlementForbidden(ThreeKingdomsState s, string player) => B4Enabled(s)
            && s.Skills.TimedEffects.Any(e => e.Kind=="response-jieying-active" && e.TargetPlayerId==player && e.CreatedTurnId==s.Clock.TurnInstanceId);
        public static int B4HandLimitPenalty(ThreeKingdomsState s,string player) => B4Enabled(s) ? s.Response?.HandLimitPenalties.FirstOrDefault(p => p.PlayerId==player)?.Amount ?? 0 : 0;
        public static bool HasB4Judgment(ThreeKingdomsState s) => B4Enabled(s) && s.Response?.JudgmentSource != null;
        private static bool IsB4ResidualOptional(ThreeKingdomsState s,B2SkillFrame f) => B4Enabled(s) && f.Kind=="response-jianxiong" && f.B4?.DeclaredType==1
            && s.Response?.CaoResiduals.Any(r=>r.PlayerId==f.PlayerId&&r.TenureId==f.TenureId&&r.AcquiredCount<4)==true;

        private static void AddResponseCardFrames(IThreeKingdomsCardHost host,string batch,B2HandChange c,GeneralInstance g,List<B2SkillFrame> frames)
        {
            if (!B4Enabled(host.State) || g.GeneralId!="tk-g49" || c.LostCount<=0 || host.State.Clock.ActivePlayerId==g.OwnerPlayerId) return;
            EnsureField(g); frames.Add(Frame(host.State,g,"tk-g49-s1","response-tuntian",0,batch));
        }
        private static void EnsureField(GeneralInstance g) { if (!g.Counters.Any(c=>c.Key=="tian")) Counter(g,"tian",0).Value=2; }
        private static void AddResponseEndFrames(IThreeKingdomsCardHost host,GeneralInstance g,string eventId)
        {
            var s=host.State; if (!B4Enabled(s)) return;
            if (g.GeneralId=="tk-g08" && s.Clock.ActivePlayerId==g.OwnerPlayerId)
            {
                var f=Frame(s,g,"tk-g08-s1","response-jieying",0,eventId); f.Optional=true; f.B4=new B4SkillPayload { OwnerPlayerId=g.OwnerPlayerId }; Append(s,f);
            }
            // 从任意在场将的第一次调用收集全体已挂出的劫营；来源隐退不撤销。
            if (!Mark(s,"response-jieying-end:"+eventId)) return;
            foreach (var e in s.Skills.TimedEffects.Where(e=>e.Kind=="response-jieying-active" && e.TargetPlayerId==s.Clock.ActivePlayerId && e.CreatedTurnId==s.Clock.TurnInstanceId).ToArray())
            {
                Append(s,new B2SkillFrame { Id=Id(s,"frame"),Kind="response-jieying-take",SkillId="tk-g08-s1",PlayerId=e.PlayerId,TenureId=e.SourceTenureId,Targets=new[]{e.TargetPlayerId},Started=true,SourceEventId=e.Id });
                s.Skills.TimedEffects=s.Skills.TimedEffects.Where(x=>x.Id!=e.Id).ToArray();
            }
        }
        private static void OnResponseOpportunityStart(IThreeKingdomsCardHost host,long opportunity,string player)
        {
            var s=host.State; if (!B4Enabled(s)) return;
            s.Skills.TimedEffects=s.Skills.TimedEffects.Where(e=>e.Kind!="response-indulgence-active" || e.CreatedTurnId>=s.Clock.TurnInstanceId).ToArray();
        }
        public static void OnB4Preparation(IThreeKingdomsCardHost host,string eventId)
        {
            var s=host.State; if (!B4Enabled(s) || !Mark(s,"response-preparation:"+eventId)) return;
            var g=Active(s,s.Clock.ActivePlayerId);
            if (g?.GeneralId=="tk-g34" && host.GetHand(g.OwnerPlayerId).Sum()>0 && !IsB4HandLossForbidden(s,g.OwnerPlayerId)
                && host is IThreeKingdomsB4Host vh && vh.GetVirtualProgressUses(g.OwnerPlayerId,"prepare")
                    .Any(u => !(host is IThreeKingdomsB4VirtualCostHost costHost) || costHost.CanUseVirtualProgressAfterPayment(g.OwnerPlayerId,u,host.GetHand(g.OwnerPlayerId))))
            { var f=Frame(s,g,"tk-g34-s1","response-qice",0,eventId); f.Optional=true; f.B4=new B4SkillPayload { OwnerPlayerId=g.OwnerPlayerId }; Append(s,f); }
            Pump(host);
        }
        public static void OnB4JudgmentStart(IThreeKingdomsCardHost host,string eventId)
        {
            var s=host.State; if (!B4Enabled(s) || !Mark(s,"response-judgment:"+eventId)) return;
            foreach (var e in s.Skills.TimedEffects.Where(x=>x.Kind=="response-indulgence-pending" && x.TargetPlayerId==s.Clock.ActivePlayerId).ToArray())
            {
                Append(s,new B2SkillFrame { Id=Id(s,"frame"),Kind="response-indulgence",SkillId="tk-g05-s2",PlayerId=e.TargetPlayerId,TenureId=e.SourceTenureId,Targets=new[]{e.PlayerId},Started=true,SourceEventId=e.Id,B4=new B4SkillPayload{OwnerPlayerId=e.PlayerId} });
                s.Skills.TimedEffects=s.Skills.TimedEffects.Where(x=>x.Id!=e.Id).ToArray();
            }
            Pump(host);
        }
        public static void OnB4PlayStart(IThreeKingdomsCardHost host,string eventId,string player)
        {
            var s=host.State; if (!B4Enabled(s) || !Mark(s,"response-play:"+eventId)) return;
            foreach (var e in s.Skills.TimedEffects.Where(x=>x.Kind=="response-jieying-pending" && x.TargetPlayerId==player).ToArray())
            {
                e.Kind="response-jieying-active"; e.CreatedTurnId=s.Clock.TurnInstanceId;
                Append(s,new B2SkillFrame { Id=Id(s,"frame"),Kind="resource",SkillId="tk-g08-s1",PlayerId=player,TenureId=e.SourceTenureId,Remaining=1,Started=true,SourceEventId=e.Id,B4=new B4SkillPayload{OwnerPlayerId=e.PlayerId} });
            }
            Pump(host);
        }
        public static void OnB4ProgressCompleted(IThreeKingdomsCardHost host,string eventId,string userPlayerId,B2ProgressCard card=null,bool virtualCard=false)
        {
            var s=host.State; if (!B4Enabled(s) || !Mark(s,"response-progress:"+eventId)) return;
            foreach (var player in Order(s))
            {
                var g=Active(s,player);
                var residual=s.Response?.CaoResiduals.FirstOrDefault(r=>r.PlayerId==player);
                if (player!=userPlayerId && card!=null && !virtualCard && host is IThreeKingdomsB4ResponseHost
                    && (g?.GeneralId=="tk-g14" && Value(g,"jianxiong",0)<4 || residual!=null && residual.AcquiredCount<4))
                {
                    bool active=g?.GeneralId=="tk-g14";
                    Append(s,new B2SkillFrame { Id=Id(s,"frame"),Kind="response-jianxiong",SkillId="tk-g14-s1",PlayerId=player,
                        TenureId=active ? g.TenureId : residual.TenureId, Targets=new[]{userPlayerId},ProgressCard=card,SourceEventId=eventId,
                        Started=true, Optional=!active, B4=new B4SkillPayload{ OwnerPlayerId=player,DeclaredType=active ? 0 : 1 } });
                }
                if (g?.GeneralId=="tk-g34" && s.Clock.ActivePlayerId!=player)
                { var f=Frame(s,g,"tk-g34-s2","response-zhiyu",0,eventId); f.Targets=new[]{userPlayerId}; f.Optional=true; f.B4=new B4SkillPayload{OwnerPlayerId=player}; Append(s,f); }
            }
            Pump(host);
        }
        private static void AddResponseActions(IThreeKingdomsCardHost host,GeneralInstance g,List<B2SkillAction> actions)
        {
            var s=host.State; if (!B4Enabled(s) || s.Clock.ActivePlayerId!=g.OwnerPlayerId || s.Clock.Phase!=ThreeKingdomsPhase.Play) return;
            var player=g.OwnerPlayerId; var hand=host.GetHand(player); bool blocked=IsB4HandLossForbidden(s,player);
            if (g.GeneralId=="tk-g05" && !blocked)
            {
                if (hand[3]>0 && Value(g,"liuli",s.Clock.PhaseId)==0 && host is IThreeKingdomsB4ResponseHost mover)
                    foreach (var move in mover.GetTokenMoves(player)) { var a=Action(s,"tk-g05-s1","response-liuli","流离："+move.Label); a.Command.ProgressCardId=move.OptionId; actions.Add(a); }
                if (hand[1]>=2 && Value(g,"guose",s.Clock.PhaseId)==0)
                    foreach (var target in s.SeatPlayerIds.Where(p=>p!=player && CanTarget(s,p,"Indulgence") && !IsProtected(s,p)
                        && !s.Skills.TimedEffects.Any(e=>e.Kind=="response-indulgence-pending" && e.TargetPlayerId==p)))
                        AddB4TargetAction(s,actions,"tk-g05-s2","response-guose","国色：对 "+target+" 使用乐不思蜀",target);
            }
            if (g.GeneralId=="tk-g49")
            {
                int field=g.Counters.Any(c=>c.Key=="tian") ? Value(g,"tian",0) : 2;
                if (field>0) foreach (var target in s.SeatPlayerIds.Where(p=>p!=player && host.GetHand(p).Sum()>0 && CanTarget(s,p,"Snatch") && !IsProtected(s,p) && !IsB4HandLossForbidden(s,p)))
                    AddB4TargetAction(s,actions,"tk-g49-s2","response-jixi","急袭：获得 "+target+" 一张随机手牌",target);
            }
            if (g.GeneralId=="tk-g08" && !blocked && Value(g,"poxi",s.Clock.PhaseId)==0)
                foreach (var target in s.SeatPlayerIds.Where(p=>p!=player && !IsProtected(s,p) && !IsB4HandLossForbidden(s,p)))
                {
                    var other=host.GetHand(target);
                    if (Enumerable.Range(0,8).Count(t=>hand[t]+other[t]>0)>=4)
                        AddB4TargetAction(s,actions,"tk-g08-s2","response-poxi","魄袭：查看 "+target+" 手牌并选四种牌",target);
                }
            foreach(var action in actions.Where(a=>a.Command.Option=="response-liuli"||a.Command.Option=="response-guose"))
            {
                int type=action.Command.Option=="response-liuli"?3:1;int count=type==3?1:2;
                action.RealCardType=type;action.CardSelectionKind="payment";action.AvailableCount=hand[type];action.Command.Counts[type]=count;
            }
        }
        private static bool ResolveResponseActive(IThreeKingdomsCardHost host,GeneralInstance g,B2SkillCommand c)
        {
            if (c.Option?.StartsWith("response-",StringComparison.Ordinal)!=true) return false;
            var s=host.State; var legal=new List<B2SkillAction>(); AddResponseActions(host,g,legal); Require(legal.Any(a=>SameB4Action(a.Command,c)),"响应技能时机、目标、费用或次数无效。");
            var f=Frame(s,g,c.SkillId,c.Option,0,null); f.Started=true; f.Targets=(string[])c.TargetPlayerIds.Clone(); f.B4=new B4SkillPayload{OwnerPlayerId=g.OwnerPlayerId};
            if(c.Option=="response-liuli")
            {
                var move=((IThreeKingdomsB4ResponseHost)host).GetTokenMoves(g.OwnerPlayerId).Single(m=>m.OptionId==c.ProgressCardId);
                f.B4.VirtualUse=new B4VirtualUse{OptionId=move.OptionId,CardId=move.TokenKind,PayloadJson=move.PayloadJson,TargetPlayerIds=new[]{move.TileId,move.VictimPlayerId},Label=move.Label};
                Counter(g,"liuli",s.Clock.PhaseId).Value=1; Append(s,f); B4Discard(host,g.OwnerPlayerId,One(3),f.Id+":cost"); return true;
            }
            if(c.Option=="response-guose") { Counter(g,"guose",s.Clock.PhaseId).Value=1; Append(s,f); var cost=One(1);cost[1]=2;B4Discard(host,g.OwnerPlayerId,cost,f.Id+":cost");Target(host,f.Id,g.OwnerPlayerId,f.Targets[0]);return true; }
            if(c.Option=="response-jixi") { EnsureField(g); Counter(g,"tian",0).Value--; Append(s,f); Target(host,f.Id,g.OwnerPlayerId,f.Targets[0]); return true; }
            if(c.Option=="response-poxi") Counter(g,"poxi",s.Clock.PhaseId).Value=1;
            Append(s,f);return true;
        }

        private static bool ProcessResponseFrame(IThreeKingdomsCardHost host,B2SkillFrame f,out bool waiting)
        {
            waiting=false;if(!f.Kind.StartsWith("response-",StringComparison.Ordinal))return false;
            var s=host.State;Require(B4Enabled(s),"旧规则包不能结算B4响应。");
            if(f.Kind=="response-tuntian" || f.Kind=="response-indulgence") { SuspendForB4Judgment(host,f);waiting=true;return true; }
            if(f.Kind=="response-guose") { bool applied=CanTarget(s,f.Targets[0],"Indulgence") && !IsProtected(s,f.Targets[0]) && !s.Skills.TimedEffects.Any(e=>e.Kind=="response-indulgence-pending"&&e.TargetPlayerId==f.Targets[0]);if(applied)AddTimed(s,"response-indulgence-pending",f.PlayerId,f.Targets[0],0);B4SkillHistoryEngine.Complete(s,f.Id,applied?"向 "+f.Targets[0]+" 放置乐不思蜀":"目标不能受到乐不思蜀影响");Remove(s,f);return true; }
            if(f.Kind=="response-liuli")
            { var u=f.B4.VirtualUse;B4SkillHistoryEngine.Complete(s,f.Id,"支付1张麦子，移动"+(u.CardId=="pirate"?"海盗":"强盗")+"至 "+u.TargetPlayerIds[0]);Remove(s,f);((IThreeKingdomsB4ResponseHost)host).MoveToken(f.PlayerId,new B4TokenMove{OptionId=u.OptionId,TokenKind=u.CardId,PayloadJson=u.PayloadJson,TileId=u.TargetPlayerIds[0],VictimPlayerId=u.TargetPlayerIds[1],Label=u.Label});return true; }
            if(f.Kind=="response-jixi")
            { var target=f.Targets[0];var cards=CanTarget(s,target,"Snatch")&&!IsProtected(s,target)&&!IsB4HandLossForbidden(s,target)?RandomHand(host,target):new int[8];B4SkillHistoryEngine.Complete(s,f.Id,"消耗1枚田，从 "+target+" 获得"+cards.Sum()+"张手牌",new[]{f.Id});Remove(s,f);if(cards.Sum()>0)B4Transfer(host,target,f.PlayerId,cards,f.Id,f.SkillId);OnB4ProgressCompleted(host,f.Id+":virtual",f.PlayerId,null,true);return true; }
            if(f.Kind=="response-jianxiong")
            {
                var g=Active(s,f.PlayerId);var residual=s.Response?.CaoResiduals.FirstOrDefault(r=>r.PlayerId==f.PlayerId&&r.TenureId==f.TenureId);
                bool active=g?.GeneralId=="tk-g14"&&g.TenureId==f.TenureId&&Value(g,"jianxiong",0)<4;
                bool valid=active || residual!=null&&residual.AcquiredCount<4;
                bool captured=valid && ((IThreeKingdomsB4ResponseHost)host).TryCaptureProgressCard(f.Targets[0],f.PlayerId,f.ProgressCard,f.SourceEventId);
                if(captured) { if(active)Counter(g,"jianxiong",0).Value++;else s.Response.CaoResiduals=s.Response.CaoResiduals.Where(r=>r!=residual).ToArray(); }
                B4SkillHistoryEngine.Complete(s,f.Id,captured?(f.ProgressCard.PublicPoint?"获得1张公开分值牌":"获得1张发展牌"):"没有可取得的发展牌",new[]{f.SourceEventId});
                if(captured)B4SkillHistoryEngine.AddPrivateResult(s,f.Id,"取得："+f.ProgressCard.Id,f.PlayerId);Remove(s,f);
                return true;
            }
            if(f.Kind=="response-jieying") { Decide(s,f,"response-jieying-target","劫营：选择目标","选择其他玩家，其下次真实出牌阶段获得资源并禁止建村，回合结束交出全部手牌。");waiting=true;return true; }
            if(f.Kind=="response-jieying-take") { var cards=!IsB4HandLossForbidden(s,f.Targets[0])&&!IsProtected(s,f.Targets[0])?host.GetHand(f.Targets[0]):new int[8];B4SkillHistoryEngine.Complete(s,f.Id,"从 "+f.Targets[0]+" 获得"+cards.Sum()+"张手牌",new[]{f.Id});Remove(s,f);if(cards.Sum()>0)B4Transfer(host,f.Targets[0],f.PlayerId,cards,f.Id,f.SkillId);return true; }
            if(f.Kind=="response-qice")
            {
                if(f.Step==0){Decide(s,f,"response-qice-use","奇策：选择发展牌用途","先选择合法非分值牌效果，再一次弃置所有手牌。");waiting=true;return true;}
                var use=f.B4.VirtualUse;use.SourceSkillId=f.SkillId;use.SourceEventId=f.Id;B4SkillHistoryEngine.Complete(s,f.Id,"弃置全部手牌，发动虚拟发展牌："+use.Label,new[]{f.Id+":cost"});Remove(s,f);((IThreeKingdomsB4Host)host).UseVirtualProgress(f.PlayerId,use);return true;
            }
            if(f.Kind=="response-zhiyu")
            {
                if(f.Step==0){f.Step=1;Gain(host,f.PlayerId,TakeRandom(host,f.PlayerId,1),f.SkillId,f.Id);return true;}
                var hand=host.GetHand(f.B4.OwnerPlayerId);s.Skills.RevealedCards=Copy(hand);
                B4SkillHistoryEngine.AddPrivateResult(s,f.Id,"展示真实手牌："+(hand.Sum()==0?"空手":string.Join("、",Enumerable.Range(0,8).Where(t=>hand[t]>0).Select(t=>CardNames[t]+" "+hand[t]+"张"))),s.SeatPlayerIds);
                if(hand.Count(n=>n>0)!=1 || host.GetHand(f.Targets[0]).Sum()==0 || IsProtected(s,f.Targets[0]) || IsB4HandLossForbidden(s,f.Targets[0])){B4SkillHistoryEngine.Complete(s,f.Id,"展示手牌后没有可执行的弃牌效果");Remove(s,f);return true;}
                f.PlayerId=f.Targets[0];Decide(s,f,"response-zhiyu-discard","智愚：弃置一张手牌","对方展示的真实手牌均为同一类型，选择一张手牌弃置。");waiting=true;return true;
            }
            if(f.Kind=="response-poxi")
            {
                if(f.Step==0){Decide(s,f,"response-poxi-select","魄袭：选择四种不同牌","分别选择自己或目标的牌；四种真实类型各一张。");waiting=true;return true;}
                int count=f.B4.Selection.Sum();B4SkillHistoryEngine.Complete(s,f.Id,"共弃置四种真实牌，其中自己弃置"+count+"张；"+new[]{"本局手牌上限减1","结束出牌阶段","没有追加效果","选择1张资源或商品","按正式骰点再次获得自己的产出"}[count],new[]{f.Id+":discard"});Remove(s,f);
                if(count==0){var r=Response(s);var p=r.HandLimitPenalties.FirstOrDefault(x=>x.PlayerId==f.PlayerId);if(p==null){p=new B4PlayerPenalty{PlayerId=f.PlayerId};r.HandLimitPenalties=r.HandLimitPenalties.Concat(new[]{p}).ToArray();}p.Amount++;}
                else if(count==1)((IThreeKingdomsB4ResponseHost)host).RequestEndPlay(f.PlayerId);
                else if(count==3){var reward=new B2SkillFrame{Id=Id(s,"frame"),Kind="mixed",PlayerId=f.PlayerId,SkillId=f.SkillId,SourceEventId=f.Id,Remaining=1,Started=true};Append(s,reward);}
                else if(count==4)((IThreeKingdomsB4ResponseHost)host).ReceiveExtraHarvest(f.PlayerId,host.FinalDiceTotal,f.Id);
                return true;
            }
            if(f.Kind=="response-guhuo")return ProcessGuhuo(host,f,out waiting);
            throw new InvalidOperationException("未知B4响应帧："+f.Kind);
        }

        private static bool AddResponseDecisionActions(IThreeKingdomsCardHost host,B2SkillFrame f,B2SkillDecision d,List<B2SkillAction> actions)
        {
            if(!d.Kind.StartsWith("response-",StringComparison.Ordinal))return false;var s=host.State;
            if(d.Kind=="response-jieying-target")foreach(var target in s.SeatPlayerIds.Where(p=>p!=f.PlayerId))AddResponseTarget(s,f,d,actions,"response-jieying-target","选择 "+target,target);
            if(d.Kind=="response-qice-use" && host is IThreeKingdomsB4Host vh && host.GetHand(f.PlayerId).Sum()>0)
                foreach(var use in vh.GetVirtualProgressUses(f.PlayerId,"prepare").Where(u => !(host is IThreeKingdomsB4VirtualCostHost costHost) || costHost.CanUseVirtualProgressAfterPayment(f.PlayerId,u,host.GetHand(f.PlayerId))))
                {var a=Action(s,f.SkillId,"response-qice-use",use.Label,d);a.Command.ProgressCardId=use.OptionId;a.VirtualCardId=use.CardId; a.ResultCardType=use.CardType>=0?(int?)use.CardType:null;actions.Add(a);}
            if(d.Kind=="response-zhiyu-discard")foreach(int type in Enumerable.Range(0,8).Where(t=>host.GetHand(f.PlayerId)[t]>0)){var a=Action(s,f.SkillId,"response-zhiyu-discard","弃置"+CardNames[type],d);a.Command.CardType=type;actions.Add(a);}
            if(d.Kind=="response-poxi-select")
            {
                int selected=f.B4.Selection.Sum()+f.B4.OtherSelection.Sum();
                if(selected<4)foreach(var player in new[]{f.PlayerId,f.Targets[0]})foreach(int type in Enumerable.Range(0,8).Where(t=>host.GetHand(player)[t]>0&&f.B4.Selection[t]+f.B4.OtherSelection[t]==0))
                {var a=Action(s,f.SkillId,"response-poxi-select",(player==f.PlayerId?"自己":"目标")+"："+CardNames[type],d);a.Command.CardType=type;a.Command.TargetPlayerIds=new[]{player};actions.Add(a);}
                if(selected==4)actions.Add(Action(s,f.SkillId,"response-poxi-confirm","确认弃置所选四种牌",d));
            }
            if(d.Kind=="response-guhuo") { actions.Add(Action(s,f.SkillId,"response-challenge","质疑声明为假",d));actions.Add(Action(s,f.SkillId,"response-trust","不质疑",d)); }
            foreach(var action in actions.Where(a=>a.Command.Option=="response-zhiyu-discard"||a.Command.Option=="response-poxi-select"))
            {action.RealCardType=action.Command.CardType;action.CardSelectionKind="select";action.AvailableCount=host.GetHand(action.Command.Option=="response-poxi-select"?action.Command.TargetPlayerIds[0]:f.PlayerId)[action.Command.CardType];}
            return true;
        }
        private static void AddResponseTarget(ThreeKingdomsState s,B2SkillFrame f,B2SkillDecision d,List<B2SkillAction> list,string option,string label,string target)
        {var a=Action(s,f.SkillId,option,label,d);a.Command.TargetPlayerIds=new[]{target};list.Add(a);}
        private static bool ResolveResponseDecision(IThreeKingdomsCardHost host,B2SkillFrame f,B2SkillDecision d,B2SkillCommand c)
        {
            if(!d.Kind.StartsWith("response-",StringComparison.Ordinal))return false;var s=host.State;var legal=new List<B2SkillAction>();AddResponseDecisionActions(host,f,d,legal);Require(legal.Any(a=>SameB4Action(a.Command,c)),"无效的响应选择。");
            if(d.Kind=="response-jieying-target"){B4SkillHistoryEngine.Complete(s,f.Id,"指定 "+c.TargetPlayerIds[0]+" 为劫营目标");Remove(s,f);AddTimed(s,"response-jieying-pending",f.PlayerId,c.TargetPlayerIds[0],0);return true;}
            if(d.Kind=="response-qice-use")
            {
                f.B4.VirtualUse=((IThreeKingdomsB4Host)host).GetVirtualProgressUses(f.PlayerId,"prepare").Single(u=>u.OptionId==c.ProgressCardId);
                f.Step=1;B4Discard(host,f.PlayerId,host.GetHand(f.PlayerId),f.Id+":cost");return true;
            }
            if(d.Kind=="response-zhiyu-discard"){B4SkillHistoryEngine.Complete(s,f.Id,"发展牌使用者 "+f.PlayerId+" 弃置1张手牌",new[]{f.Id+":discard"});Remove(s,f);B4Discard(host,f.PlayerId,One(c.CardType),f.Id+":discard");return true;}
            if(d.Kind=="response-poxi-select")
            {
                if(c.Option=="response-poxi-select"){if(c.TargetPlayerIds[0]==f.PlayerId)f.B4.Selection[c.CardType]++;else f.B4.OtherSelection[c.CardType]++;return true;}
                Require(Contains(host.GetHand(f.PlayerId),f.B4.Selection)&&Contains(host.GetHand(f.Targets[0]),f.B4.OtherSelection),"魄袭所选牌已变化。");
                f.Step=1;CommitPoxiDiscard(host,f);return true;
            }
            if(d.Kind=="response-guhuo")
            {var declaration=Response(s).Declaration;if(c.Option=="response-challenge")declaration.Challengers=declaration.Challengers.Concat(new[]{f.PlayerId}).ToArray();declaration.ResponderCursor++;return true;}
            return true;
        }
        private static void CommitPoxiDiscard(IThreeKingdomsCardHost host,B2SkillFrame f)
        {
            // 两方四张一次提交，触发只看到整个费用完成后的状态。
            var changes=new List<B2HandChange>();int part=0;
            foreach(var player in new[]{f.PlayerId,f.Targets[0]})
            {
                var cards=part++==0?f.B4.Selection:f.B4.OtherSelection;int before=host.GetHand(player).Sum();
                if(cards.Sum()==0)continue;host.RemoveHand(player,cards);host.AddBank(cards);HoldReturnedCards(host,player,"discard",f.Id+":discard:"+player,cards);
                changes.Add(new B2HandChange{PlayerId=player,BeforeCount=before,AfterCount=before-cards.Sum(),LostCount=cards.Sum(),LostCards=Copy(cards),SourceSkillId=f.SkillId});
            }
            RecordCards(host,f.Id+":discard",changes.ToArray());
        }

        private static void SuspendForB4Judgment(IThreeKingdomsCardHost host,B2SkillFrame f)
        {
            var s=host.State;var r=Response(s);Require(r.JudgmentSource==null,"已有判定来源尚未恢复。");
            r.JudgmentSource=f;r.SuspendedFrames=s.Skills.Frames.Where(x=>x.Id!=f.Id).ToArray();r.SuspendedHarvest=s.Skills.HarvestActive;
            s.Skills.Frames=Array.Empty<B2SkillFrame>();s.Skills.HarvestActive=false;
            B3DiceEngine.BeginJudgment(host,f.PlayerId,f.Id+":judgment");
        }
        public static bool ResumeB4Judgment(IThreeKingdomsCardHost host)
        {
            var s=host.State;var r=s?.Response;if(!B4Enabled(s)||r?.JudgmentSource==null)return false;
            var result=B3DiceEngine.TakeFinal(host);if(result==null)return true;
            var f=r.JudgmentSource;Require(result.EventId==f.Id+":judgment","判定结果不属于保存的来源。");
            s.Skills.Frames=r.SuspendedFrames;s.Skills.HarvestActive=r.SuspendedHarvest;r.SuspendedFrames=Array.Empty<B2SkillFrame>();r.JudgmentSource=null;r.SuspendedHarvest=false;
            if(f.Kind=="response-tuntian"){var g=Active(s,f.PlayerId);if(g?.TenureId==f.TenureId&&result.Total>5){EnsureField(g);Counter(g,"tian",0).Value=Math.Min(3,Value(g,"tian",0)+1);}}
            else if(result.Total<=7)AddTimed(s,"response-indulgence-active",f.Targets[0],f.PlayerId,0);
            B4SkillHistoryEngine.Complete(s,f.Id,"判定点数 "+result.Total);
            OnDiceResolved(host,result.EventId,false,Array.Empty<string>(),result.NumericActualRoll,false,true,result.Total);
            Pump(host);return true;
        }

        public static void BeginB4DeclarationBatch(IThreeKingdomsCardHost host,string player,string eventId,int[] realTypes,int[] effectiveTypes,string operationJson)
        {
            var s=host.State;var g=Active(s,player);
            Require(B4Enabled(s)&&g?.GeneralId=="tk-g07"&&!HasPending(s)&&!B3DiceEngine.HasPending(s)&&s.PendingDecision==null,"当前不能声明蛊惑用途。");
            Require(realTypes!=null&&effectiveTypes!=null&&realTypes.Length>0&&realTypes.Length==effectiveTypes.Length&&realTypes.All(t=>t>=0&&t<8)&&effectiveTypes.All(t=>t>=0&&t<8)
                &&!string.IsNullOrWhiteSpace(eventId)&&!string.IsNullOrWhiteSpace(operationJson),"蛊惑的整项用途或牌种无效。");
            var counts=new int[8];foreach(int type in realTypes)counts[type]++;Require(Contains(host.GetHand(player),counts)&&!IsB4HandLossForbidden(s,player),"蛊惑预留的真实手牌不足或不能失牌。");
            Require(s.Response?.Declaration==null,"上一次蛊惑原用途尚未恢复。");
            Require(!s.Skills.ProcessedEvents.Contains("declaration:"+eventId),"同一蛊惑用途不能重复声明。");Mark(s,"declaration:"+eventId);
            int seat=Array.IndexOf(s.SeatPlayerIds,player);var responders=s.SeatPlayerIds.Skip(seat+1).Concat(s.SeatPlayerIds.Take(seat)).ToArray();
            Response(s).Declaration=new B4DeclarationBatch{EventId=eventId,PlayerId=player,TenureId=g.TenureId,RealTypes=(int[])realTypes.Clone(),EffectiveTypes=(int[])effectiveTypes.Clone(),OperationJson=operationJson,Responders=responders};
            var f=Frame(s,g,"tk-g07-s1","response-guhuo",0,eventId);f.Started=true;f.B4=new B4SkillPayload{OwnerPlayerId=player};Append(s,f);Pump(host);
        }
        private static bool ProcessGuhuo(IThreeKingdomsCardHost host,B2SkillFrame f,out bool waiting)
        {
            var s=host.State;var d=Response(s).Declaration;waiting=false;
            if(d.ResponderCursor<d.Responders.Length)
            {
                f.PlayerId=d.Responders[d.ResponderCursor];Decide(s,f,"response-guhuo","蛊惑：承诺是否质疑",d.PlayerId+" 声明第 "+(d.CardCursor+1)+" 张预留牌为"+CardNames[d.EffectiveTypes[d.CardCursor]]+"。所有人提交后统一验证。");waiting=true;return true;
            }
            bool truth=d.RealTypes[d.CardCursor]==d.EffectiveTypes[d.CardCursor];bool failed=d.Challengers.Length>0&&!truth;
            var challengers=(string[])d.Challengers.Clone();int current=d.CardCursor;
            if(failed){d.Complete=true;d.Success=false;B4SkillHistoryEngine.Complete(s,f.Id,"第"+(current+1)+"张声明被识破，弃置该真实牌并取消原用途");Remove(s,f);B4Discard(host,d.PlayerId,One(d.RealTypes[current]),d.EventId+":failed:"+current);}
            else
            {
                d.CardCursor++;d.ResponderCursor=0;d.Challengers=Array.Empty<string>();
                if(d.CardCursor==d.RealTypes.Length){d.Complete=true;d.Success=true;B4SkillHistoryEngine.Complete(s,f.Id,"全部"+d.RealTypes.Length+"张声明通过，继续原用途");Remove(s,f);}
            }
            if(challengers.Length>0)
            {
                // 只有所有承诺完成后公开本张真实类型，后来的质疑者无法先看到答案。
                s.Skills.RevealedCards=One(d.RealTypes[current]);
                B4SkillHistoryEngine.AddPrivateResult(s,f.Id,"第"+(current+1)+"张真实牌为"+CardNames[d.RealTypes[current]]+"，质疑"+(failed?"成立":"不成立"),s.SeatPlayerIds);
                foreach(var player in challengers)
                {
                    if(failed){var reward=new B2SkillFrame{Id=Id(s,"frame"),Kind="random",PlayerId=player,Remaining=1,SourceEventId=f.Id,Started=true};Prepend(s,new[]{reward});}
                    else B4DiscardRandom(host,player,1,d.EventId+":challenge:"+current+":"+player);
                }
            }
            return true;
        }
        public static B4DeclarationBatch TakeB4DeclarationResult(IThreeKingdomsCardHost host)
        {
            var s=host.State;var d=s?.Response?.Declaration;if(d==null||!d.Complete||HasPending(s)||HasB4Judgment(s)||B3DiceEngine.HasPending(s))return null;
            var result=CloneDeclaration(d);s.Response.Declaration=null;return result;
        }

        public static string[] ValidateB4Response(IThreeKingdomsCardHost host)
        {
            var s=host.State;var r=s?.Response;var errors=new List<string>();if(r==null)return errors.ToArray();
            if(!B4Enabled(s))return new[]{"旧包不能包含B4响应状态。"};
            if(r.CaoResiduals==null||r.HandLimitPenalties==null||r.SuspendedFrames==null)return new[]{"B4响应存档缺少必需区域。"};
            if(r.CaoResiduals.Any(x=>x==null||!s.SeatPlayerIds.Contains(x.PlayerId)||string.IsNullOrWhiteSpace(x.TenureId)||x.AcquiredCount<0||x.AcquiredCount>=4)
                ||r.CaoResiduals.Select(x=>x?.PlayerId).Distinct().Count()!=r.CaoResiduals.Length)errors.Add("奸雄残留次数或身份无效。");
            if(r.HandLimitPenalties.Any(x=>x==null||!s.SeatPlayerIds.Contains(x.PlayerId)||x.Amount<0)
                ||r.HandLimitPenalties.Select(x=>x?.PlayerId).Distinct().Count()!=r.HandLimitPenalties.Length)errors.Add("本局手牌上限扣减无效。");
            if(r.SuspendedFrames.Any(f=>f==null||string.IsNullOrWhiteSpace(f.Id)||!Valid(f.Cards)||!Valid(f.SelectedCards)||f.Targets==null||f.Tiles==null||f.Remaining<0)
                ||r.SuspendedFrames.Select(f=>f?.Id).Concat(s.Skills.Frames.Select(f=>f.Id)).Distinct().Count()!=r.SuspendedFrames.Length+s.Skills.Frames.Length)errors.Add("挂起技能帧重复或实体区无效。");
            if(r.JudgmentSource==null&&(r.SuspendedFrames.Length>0||r.SuspendedHarvest))errors.Add("挂起技能缺少判定来源。");
            if(r.JudgmentSource is B2SkillFrame source)
            {
                if((source.Kind!="response-tuntian"&&source.Kind!="response-indulgence")||!s.SeatPlayerIds.Contains(source.PlayerId)||!Valid(source.Cards)||source.Cards.Sum()!=0
                    ||!Valid(source.SelectedCards)||source.SelectedCards.Sum()!=0||s.Dice?.Roll?.EventId!=source.Id+":judgment"||s.Dice.Roll.Purpose!=B3DicePurpose.Judgment||s.Dice.Roll.Submitted)
                    errors.Add("判定来源与未提交骰局不一致。");
            }
            if(r.Declaration is B4DeclarationBatch d)
            {
                if(!s.SeatPlayerIds.Contains(d.PlayerId)||string.IsNullOrWhiteSpace(d.EventId)||string.IsNullOrWhiteSpace(d.TenureId)||string.IsNullOrWhiteSpace(d.OperationJson)
                    ||d.RealTypes==null||d.EffectiveTypes==null||d.RealTypes.Length==0||d.RealTypes.Length!=d.EffectiveTypes.Length||d.RealTypes.Any(t=>t<0||t>7)||d.EffectiveTypes.Any(t=>t<0||t>7)
                    ||d.Responders==null||d.Challengers==null||d.Responders.Length!=s.SeatPlayerIds.Length-1||d.Responders.Distinct().Count()!=d.Responders.Length
                    ||d.Responders.Any(p=>p==d.PlayerId||!s.SeatPlayerIds.Contains(p))||d.Challengers.Distinct().Count()!=d.Challengers.Length||d.Challengers.Any(p=>!d.Responders.Contains(p))
                    ||d.CardCursor<0||d.CardCursor>d.RealTypes.Length||d.ResponderCursor<0||d.ResponderCursor>d.Responders.Length||d.Success&&!d.Complete)
                    errors.Add("蛊惑预留、用途或质疑承诺无效。");
                else
                {
                    var frames=s.Skills.Frames.Concat(r.SuspendedFrames).Where(f=>f.Kind=="response-guhuo"&&f.SourceEventId==d.EventId).ToArray();
                    if(frames.Length!=(d.Complete?0:1)||!d.Complete&&d.CardCursor>=d.RealTypes.Length)errors.Add("蛊惑待决缺少原用途恢复帧。");
                    if(!d.Complete){var cards=new int[8];foreach(int t in d.RealTypes)cards[t]++;if(!Contains(host.GetHand(d.PlayerId),cards))errors.Add("蛊惑预留牌已离开手牌。");}
                }
            }
            return errors.ToArray();
        }
    }
}
