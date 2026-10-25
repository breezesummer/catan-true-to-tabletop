using System;
using System.Collections.Generic;
using System.Linq;

namespace Catan.Core.ThreeKingdoms
{
    // B5 W02/W10/W18/W20/W24/W38/W53。实体棋盘操作由 M4/M5 宿主执行。
    public static partial class B2SkillEngine
    {
        private static B5MilitaryState Military(ThreeKingdomsState s) => s.Military ?? (s.Military = new B5MilitaryState());
        public static bool HasB5Comparison(ThreeKingdomsState s) => B5Enabled(s) && s.Military?.ComparisonSource != null;
        public static bool B5AlwaysActiveKnights(ThreeKingdomsState s, string player) => B5Enabled(s) && Active(s,player)?.GeneralId == "tk-g24";
        public static bool B5ImmediateKnightAction(ThreeKingdomsState s, string player) => B5Enabled(s) && Active(s,player)?.GeneralId == "tk-g02";
        public static bool B5IgnoreKnightDisplacementLevel(ThreeKingdomsState s, string player) => B5AlwaysActiveKnights(s,player);
        public static bool B5InvasionImmune(ThreeKingdomsState s, string player) => B5Enabled(s) && Active(s,player)?.GeneralId == "tk-g20";
        public static bool B5RobberPirateImmune(ThreeKingdomsState s, string player) => B5InvasionImmune(s,player);
        public static bool B5IsZhangFei(ThreeKingdomsState s, string player) => B5Enabled(s) && Active(s,player)?.GeneralId == "tk-g38";
        public static int B5DiceModifier(ThreeKingdomsState s, string player, bool actual) => B5Enabled(s) && actual && Active(s,player)?.GeneralId == "tk-g53" ? 3 : 0;
        public static int B5EffectiveKnightLevel(ThreeKingdomsState s, string player, int actualLevel, int fieldKnightCount)
        {
            if (!B5Enabled(s)) return actualLevel;
            int level = fieldKnightCount == 1 && s.Military?.PermanentShanqiPlayers.Contains(player) == true ? 4 : actualLevel;
            return level + (B5ImmediateKnightAction(s,player) ? 1 : 0);
        }
        public static int B5HandLimitBonus(ThreeKingdomsState s, string player)
        {
            if (!B5Enabled(s)) return 0;
            return (Active(s,player)?.GeneralId == "tk-g10" ? s.SeatPlayerIds.Length : 0)
                + (s.Military?.PermanentYaowuPlayers.Contains(player) == true ? 4 : 0)
                - s.Generals.Count(g => g.Region == GeneralRegion.Active && g.GeneralId == "tk-g53" && g.OwnerPlayerId != player);
        }
        public static int B5ProgressLimitBonus(ThreeKingdomsState s, string player) => B5Enabled(s) && s.Military?.PermanentYaowuPlayers.Contains(player) == true ? 2 : 0;
        public static int B5MilitaryScoreAdjustment(ThreeKingdomsState s, string player) => B5Enabled(s) ? s.Military?.ScoreAdjustments.FirstOrDefault(x=>x.PlayerId==player)?.Value ?? 0 : 0;
        public static bool B5IsHandPublic(ThreeKingdomsState s, string player) => B5Enabled(s) && s.Military?.PublicHands.Any(x=>x.PlayerId==player) == true;
        public static bool B5DoubleHarvest(ThreeKingdomsState s, string player, string tile) => B5Enabled(s) && s.Military?.DoubleHarvest.Any(x=>x.PlayerId==player && x.TileId==tile) == true;
        public static void OnB5RobberMoved(ThreeKingdomsState s, string newTile)
        { if(B5Enabled(s) && s.Military != null) s.Military.DoubleHarvest=s.Military.DoubleHarvest.Where(x=>x.TileId==newTile).ToArray(); }

        public static void OnB5MilitaryGeneralActivated(ThreeKingdomsState s, GeneralInstance g)
        {
            if (!B5Enabled(s) || g.GeneralId != "tk-g02" || !Mark(s,"b5m-enter:"+g.TenureId)) return;
            if (s.Clock.Round == 0 || !s.OpeningComplete)
                Military(s).DelayedDeployments=Military(s).DelayedDeployments.Concat(new[]{new B5PlayerCount{PlayerId=g.OwnerPlayerId,TenureId=g.TenureId}}).ToArray();
            else Append(s,MilitaryFrame(s,g,"tk-g02-s1","b5m-deploy",g.TenureId));
        }
        public static void OnB5MilitaryGeneralRetired(ThreeKingdomsState s, GeneralInstance g)
        {
            if (!B5Enabled(s) || g.GeneralId != "tk-g02") return;
            var state=Military(s);state.DelayedDeployments=state.DelayedDeployments.Where(x=>x.TenureId!=g.TenureId).ToArray();
            if (Mark(s,"b5m-retire:"+g.TenureId)) Append(s,MilitaryFrame(s,g,"tk-g02-s2","b5m-return",g.TenureId));
        }
        public static void OnB5MilitaryTurnStart(IThreeKingdomsCardHost host, string eventId, string player)
        {
            var s=host.State;if(!B5Enabled(s)||!Mark(s,"b5m-turn-start:"+eventId))return;
            var m=Military(s);m.TurnHandSnapshots=m.TurnHandSnapshots.Where(x=>x.PlayerId!=player).Concat(new[]{new B5PlayerCount{PlayerId=player,Value=host.GetHand(player).Sum(),TurnId=s.Clock.TurnInstanceId}}).ToArray();
            foreach(var pending in m.DelayedDeployments.ToArray())
            {
                var g=Active(s,pending.PlayerId);if(g?.TenureId==pending.TenureId)Append(s,MilitaryFrame(s,g,"tk-g02-s1","b5m-deploy",pending.TenureId));
            }
            m.DelayedDeployments=Array.Empty<B5PlayerCount>();
        }
        public static void OnB5MilitaryOpportunityStart(IThreeKingdomsCardHost host, long opportunityId, string player, string[] skipped=null)
        {
            var s=host.State;if(!B5Enabled(s)||s.Military==null)return;
            var begun=(skipped??Array.Empty<string>()).Concat(new[]{player}).ToArray();
            s.Military.PublicHands=s.Military.PublicHands.Where(x=>!begun.Contains(x.PlayerId)||x.TurnId>=opportunityId).ToArray();
        }
        public static void OnB5MilitaryHarvestStart(IThreeKingdomsCardHost host, string eventId)
        {
            var s=host.State;if(!B5Enabled(s)||!Mark(s,"b5m-harvest:"+eventId))return;
            foreach(var player in Order(s))
            {
                var g=Active(s,player);if(g?.GeneralId!="tk-g38"||IsDrawBlocked(s,player))continue;
                var snapshot=s.Military?.TurnHandSnapshots.FirstOrDefault(x=>x.PlayerId==player);
                if(snapshot==null||host.GetHand(player).Sum()>=Math.Min(7,snapshot.Value))continue;
                var f=MilitaryFrame(s,g,"tk-g38-s2","b5m-tishen",eventId);f.Remaining=Math.Min(7,snapshot.Value);f.Optional=true;f.Started=false;Append(s,f);
            }
        }
        public static void OnB5MilitaryBuilt(IThreeKingdomsCardHost host,string eventId,string player,string assetKind)
        {
            var s=host.State;if(!B5Enabled(s)||!new[]{"Settlement","City","Knight","RecruitKnight","PromoteKnight"}.Contains(assetKind)||!Mark(s,"b5m-built:"+eventId))return;
            foreach(var id in Order(s))
            {
                var g=Active(s,id);if(g?.GeneralId!="tk-g53"||id==player||host.GetHand(player).Sum()==0||IsB4HandLossForbidden(s,player)||IsProtected(s,player))continue;
                var f=MilitaryFrame(s,g,"tk-g53-s1","b5m-tax",eventId);f.PlayerId=player;f.Targets=new[]{id};Append(s,f);
            }
        }
        public static void OnB5MilitaryEnd(IThreeKingdomsCardHost host,string eventId)
        {
            var s=host.State;if(!B5Enabled(s)||!Mark(s,"b5m-end:"+eventId)||s.Military==null)return;
            foreach(var e in s.Military.HuoshouEnds.Where(x=>x.TurnId==s.Clock.TurnInstanceId).ToArray())
            {
                var f=new B2SkillFrame{Id=Id(s,"frame"),PlayerId=e.PlayerId,TenureId=e.TenureId,SkillId="tk-g20-s1",Kind="b5m-huoshou-move",Started=true,SourceEventId=eventId,B4=new B4SkillPayload{OwnerPlayerId=e.PlayerId}};
                Append(s,f);
            }
            s.Military.HuoshouEnds=s.Military.HuoshouEnds.Where(x=>x.TurnId!=s.Clock.TurnInstanceId).ToArray();
        }
        public static void OnB5ProgressAffected(IThreeKingdomsCardHost host,string eventId,string source,string target,int color)
        {
            var s=host.State;if(!B5Enabled(s)||source==target||s.Military?.PermanentYaowuPlayers.Contains(target)!=true||!Mark(s,"b5m-yaowu:"+eventId+":"+target))return;
            var f=new B2SkillFrame{Id=Id(s,"frame"),PlayerId=color==1?source:target,SkillId="tk-g24-s1",Kind=color==1?"random":"resource",Remaining=1,Started=true,SourceEventId=eventId,B4=new B4SkillPayload{OwnerPlayerId=target}};
            Append(s,f);
        }
        public static void OnB5RaidResolved(IThreeKingdomsCardHost host,string eventId,bool cityCollapsed,bool pointAwarded,bool cardsLost,string robbedTileId=null)
        {
            var s=host.State;if(!B5Enabled(s)||!Mark(s,"b5m-raid:"+eventId))return;
            foreach(var player in Order(s))
            {
                var g=Active(s,player);if(g?.GeneralId!="tk-g20")continue;
                bool ownsRobbedTile=robbedTileId!=null && host is IThreeKingdomsB5Host board && board.GetProducingTileIds(player).Contains(robbedTileId);
                int flags=(cityCollapsed?1:0)|(pointAwarded?2:0)|(cardsLost?4:0)|(ownsRobbedTile?8:0);if(flags==0)continue;
                var f=MilitaryFrame(s,g,"tk-g20-s2","b5m-zaiqi",eventId);f.B4.DeclaredType=flags;
                if(robbedTileId!=null)f.B4.VirtualUse=new B4VirtualUse{OptionId=robbedTileId};Append(s,f);
            }
        }
        public static void CommitB5Huoshou(IThreeKingdomsCardHost host,string player,string eventId)
        {
            var s=host.State;var g=Active(s,player);Require(B5Enabled(s)&&g?.GeneralId=="tk-g20"&&Value(g,"huoshou",0)==0,"祸首限定次数或武将无效。");
            Counter(g,"huoshou",0).Value=1;var m=Military(s);m.ImmediateInvasionTurnId=s.Clock.TurnInstanceId;
            m.HuoshouEnds=m.HuoshouEnds.Concat(new[]{new B5PlayerCount{PlayerId=player,TurnId=s.Clock.TurnInstanceId,TenureId=g.TenureId}}).ToArray();
            ((IThreeKingdomsB5Host)host).ResolveInvasion(eventId,s.SeatPlayerIds,false);
        }

        private static B2SkillFrame MilitaryFrame(ThreeKingdomsState s,GeneralInstance g,string skill,string kind,string source)
        { var f=Frame(s,g,skill,kind,0,source);f.Started=true;f.B4=new B4SkillPayload{OwnerPlayerId=g.OwnerPlayerId};return f; }
        private static void AddB5MilitaryActions(IThreeKingdomsCardHost host,GeneralInstance g,List<B2SkillAction> actions)
        {
            var s=host.State;if(!B5Enabled(s)||s.Clock.ActivePlayerId!=g.OwnerPlayerId||s.Clock.Phase!=ThreeKingdomsPhase.Play||!(host is IThreeKingdomsB5Host board))return;
            var player=g.OwnerPlayerId;var hand=host.GetHand(player);bool blocked=IsB4HandLossForbidden(s,player);
            if(g.GeneralId=="tk-g10"&&!blocked)foreach(int type in Enumerable.Range(0,8).Where(t=>hand[t]>=2))
            { var a=Action(s,"tk-g10-s1","b5m-luanji","乱击：弃两张"+CardNames[type]);a.Command.CardType=type;a.Command.Counts[type]=2;a.RealCardType=type;a.CardSelectionKind="payment";a.AvailableCount=hand[type];actions.Add(a); }
            if(g.GeneralId=="tk-g18")
            {
                if(Value(g,"wusheng",0)==0&&!IsDrawBlocked(s,player)&&board.GetAllRedResourceTypes().Any(t=>host.GetBank()[t]>0))actions.Add(Action(s,"tk-g18-s1","b5m-wusheng","武圣：领取全地图红点的每种基础资源各一张"));
                if(Value(g,"shanqi",0)==0&&s.Military?.PermanentShanqiPlayers.Contains(player)!=true)actions.Add(Action(s,"tk-g18-s2","b5m-shanqi","单骑：整局仅一枚骑士时有效等级为四级"));
            }
            if(g.GeneralId=="tk-g24"&&Value(g,"yaowu",0)==0&&s.Military?.PermanentYaowuPlayers.Contains(player)!=true)actions.Add(Action(s,"tk-g24-s1","b5m-yaowu","耀武：本局手牌上限加四、发展牌上限加二，并获得受牌影响奖励"));
            if(g.GeneralId=="tk-g53"&&Value(g,"xianghai-tax",0)>=2&&Value(g,"chuhai",s.Clock.PhaseId)==0)
                foreach(var target in s.SeatPlayerIds.Where(p=>p!=player&&host.GetHand(p).Sum()>0&&!IsProtected(s,p)&&!IsB4HandLossForbidden(s,p)))
                {
                    int count=host.GetHand(target).Sum();if(s.SeatPlayerIds.Where(p=>p!=target).All(p=>host.GetHand(p).Sum()>count))continue;
                    var a=Action(s,"tk-g53-s2","b5m-chuhai","除害：与 "+target+" 比大小");a.Command.TargetPlayerIds=new[]{target};actions.Add(a);
                }
            var lvbu=s.Generals.FirstOrDefault(x=>x.Region==GeneralRegion.Active&&x.GeneralId=="tk-g02");
            if(lvbu!=null&&!blocked)foreach(var option in board.GetBoardOptions(player,"liyu"))
            {
                foreach(var payment in B5LiyuPayments(s,player,hand,option.Cost))
                {
                    string names=string.Join("、",Enumerable.Range(0,8).Where(t=>payment[t]>0).Select(t=>payment[t]+"张"+CardNames[t]));
                    var a=Action(s,"tk-g02-s2","b5m-liyu","利驭：以"+names+"支付"+option.Cost[3]+"张有效麦，"+option.Label);a.Command.ProgressCardId=option.Id;a.Command.Counts=payment;a.Command.TargetPlayerIds=new[]{lvbu.OwnerPlayerId};a.CardSelectionKind="payment";actions.Add(a);
                }
            }
        }
        private static int[][] B5LiyuPayments(ThreeKingdomsState s,string player,int[] hand,int[] cost)
        {
            var result=new List<int[]>();var cards=new int[8];var types=Enumerable.Range(0,8).Where(t=>hand[t]>0&&EffectiveCardTypes(s,player,t).Contains(3)).OrderBy(t=>t==3?0:1).ToArray();
            bool single=Active(s,player)?.GeneralId=="tk-g41";
            void Add(int index,int remaining,int converted)
            {
                if(remaining==0){if(B4PaymentAllocator.Allocate(s,player,hand,cost,cards)!=null)result.Add(Copy(cards));return;}
                if(index>=types.Length)return;int type=types[index];int max=Math.Min(hand[type],remaining);if(single&&type!=3)max=Math.Min(max,1-converted);
                for(int count=max;count>=0;count--){cards[type]=count;Add(index+1,remaining-count,converted+(type==3?0:count));}cards[type]=0;
            }
            Add(0,cost.Sum(),0);return result.ToArray();
        }
        private static bool ResolveB5MilitaryActive(IThreeKingdomsCardHost host,GeneralInstance g,B2SkillCommand c)
        {
            if(c.Option?.StartsWith("b5m-",StringComparison.Ordinal)!=true)return false;
            var s=host.State;var legal=new List<B2SkillAction>();AddB5MilitaryActions(host,g,legal);Require(legal.Any(a=>SameB4Action(a.Command,c)),"军事技能时机、费用、目标或次数无效。");
            var holder=c.Option=="b5m-liyu"?Active(s,c.TargetPlayerIds[0]):g;
            var f=MilitaryFrame(s,holder,c.SkillId,c.Option,null);f.PlayerId=g.OwnerPlayerId;f.Targets=(string[])c.TargetPlayerIds.Clone();
            if(c.Option=="b5m-liyu")
            {
                var option=((IThreeKingdomsB5Host)host).GetBoardOptions(g.OwnerPlayerId,"liyu").Single(x=>x.Id==c.ProgressCardId);
                f.B4.VirtualUse=new B4VirtualUse{OptionId=option.Id,PayloadJson=Json.Write(option)};Append(s,f);
                var matrix=B4PaymentAllocator.Allocate(s,g.OwnerPlayerId,host.GetHand(g.OwnerPlayerId),option.Cost,c.Counts);
                for(int real=0;real<8;real++)for(int use=0;use<8;use++)for(int n=0;n<matrix[real*8+use]&&real!=use;n++)CommitCardConversion(host,f.Id+":conversion:"+real+":"+use+":"+n,g.OwnerPlayerId,real,use);
                if(g.OwnerPlayerId==holder.OwnerPlayerId)B4Discard(host,g.OwnerPlayerId,c.Counts,f.Id+":cost");else B4Transfer(host,g.OwnerPlayerId,holder.OwnerPlayerId,c.Counts,f.Id+":cost",f.SkillId,true);
                return true;
            }
            if(c.Option=="b5m-luanji") { f.Targets=Order(s);Append(s,f);B4Discard(host,g.OwnerPlayerId,c.Counts,f.Id+":cost");return true; }
            if(c.Option=="b5m-wusheng")Counter(g,"wusheng",0).Value=1;
            if(c.Option=="b5m-shanqi")Counter(g,"shanqi",0).Value=1;
            if(c.Option=="b5m-yaowu")Counter(g,"yaowu",0).Value=1;
            if(c.Option=="b5m-chuhai")Counter(g,"chuhai",s.Clock.PhaseId).Value=1;
            Append(s,f);return true;
        }
        private static bool ProcessB5MilitaryFrame(IThreeKingdomsCardHost host,B2SkillFrame f,out bool waiting)
        {
            waiting=false;if(f.Kind?.StartsWith("b5m-",StringComparison.Ordinal)!=true)return false;
            var s=host.State;Require(B5Enabled(s)&&host is IThreeKingdomsB5Host,"军事技能需要B5棋盘宿主。");var board=(IThreeKingdomsB5Host)host;
            if(f.Kind=="b5m-deploy"||f.Kind=="b5m-return")
            {
                var options=board.GetBoardOptions(f.PlayerId,f.Kind=="b5m-deploy"?"deploy-knight":"return-knight");
                if(options.Length==0){Remove(s,f);return true;}
                Decide(s,f,f.Kind,"选择骑士位置",f.Kind=="b5m-deploy"?"无双：放置个人供应中最低实际等级的合法骑士，并激活。":"吕布隐退：逐枚将离开己方道路的骑士归位；无位置则退回供应。");waiting=true;return true;
            }
            if(f.Kind=="b5m-liyu")
            {
                var option=Json.Read<B5BoardOption>(f.B4.VirtualUse.PayloadJson);Remove(s,f);board.ApplyBoardOption(f.PlayerId,option);return true;
            }
            if(f.Kind=="b5m-shanqi") { var m=Military(s);m.PermanentShanqiPlayers=m.PermanentShanqiPlayers.Concat(new[]{f.PlayerId}).Distinct().ToArray();Remove(s,f);return true; }
            if(f.Kind=="b5m-yaowu") { var m=Military(s);m.PermanentYaowuPlayers=m.PermanentYaowuPlayers.Concat(new[]{f.PlayerId}).Distinct().ToArray();Remove(s,f);return true; }
            if(f.Kind=="b5m-wusheng")
            {
                var cards=new int[8];if(!IsDrawBlocked(s,f.PlayerId))foreach(int type in board.GetAllRedResourceTypes().Where(t=>t>=0&&t<5).Distinct())if(host.GetBank()[type]>0)cards[type]=1;
                host.RemoveBank(cards);Remove(s,f);Gain(host,f.PlayerId,cards,f.SkillId,f.Id);return true;
            }
            if(f.Kind=="b5m-tishen") { int count=Math.Max(0,f.Remaining-host.GetHand(f.PlayerId).Sum());Remove(s,f);Gain(host,f.PlayerId,TakeRandom(host,f.PlayerId,count),f.SkillId,f.Id);return true; }
            if(f.Kind=="b5m-tax")
            {
                if(host.GetHand(f.PlayerId).Sum()==0||IsProtected(s,f.PlayerId)||IsB4HandLossForbidden(s,f.PlayerId)){Remove(s,f);return true;}
                Decide(s,f,"b5m-tax","乡害：交给周处一张手牌","建造已完成；请选择一张真实资源或商品交给 "+f.Targets[0]+"。");waiting=true;return true;
            }
            if(f.Kind=="b5m-luanji")
            {
                if(f.Step<f.Targets.Length)
                {
                    f.PlayerId=f.Targets[f.Step];
                    if(IsB4HandLossForbidden(s,f.PlayerId)||!Enumerable.Range(0,8).Any(t=>host.GetHand(f.PlayerId)[t]>0&&EffectiveCardTypes(s,f.PlayerId,t).Contains(4))) { f.Step++;return true; }
                    Decide(s,f,"b5m-luanji-defend","乱击：是否抵御虚拟入侵","打出一张有效矿石后，本次城市、骑士、奖惩全部退出计算；也可参与入侵。");waiting=true;return true;
                }
                f.PlayerId=f.B4.OwnerPlayerId;f.Kind="b5m-luanji-reward";
                var participants=f.Targets.Where((p,index)=>f.B4.OtherSelection[index]==0).ToArray();
                if(participants.Length>0){board.ResolveInvasion(f.Id+":invasion",participants,true);waiting=true;}return true;
            }
            if(f.Kind=="b5m-luanji-reward")
            {
                var cards=new int[8];if(!IsDrawBlocked(s,f.PlayerId))cards[3]=Math.Min(f.Remaining,host.GetBank()[3]);host.RemoveBank(cards);Remove(s,f);Gain(host,f.PlayerId,cards,f.SkillId,f.Id);return true;
            }
            if(f.Kind=="b5m-zaiqi")
            {
                var actions=new List<B2SkillAction>();var temporary=new B2SkillDecision{Kind="b5m-zaiqi"};AddB5MilitaryDecisionActions(host,f,temporary,actions);
                if(actions.Count==0){Remove(s,f);return true;}
                Decide(s,f,"b5m-zaiqi","再起：选择本次事件的结果","只结算本次入侵或掠地实际满足的一项；只有失分可执行时仍须选择。");waiting=true;return true;
            }
            if(f.Kind=="b5m-huoshou-move")
            {
                if(board.GetTokenMoves(f.PlayerId).Length==0){Remove(s,f);return true;}
                Decide(s,f,"b5m-huoshou-move","祸首：回合结束移动","可以按普通规则移动一次强盗或海盗，也可放弃。");waiting=true;return true;
            }
            if(f.Kind=="b5m-chuhai") { SuspendB5Comparison(host,f);waiting=true;return true; }
            if(f.Kind=="b5m-chuhai-choice")
            {
                if(IsProtected(s,f.Targets[0])||IsB4HandLossForbidden(s,f.Targets[0])||host.GetHand(f.Targets[0]).Sum()==0){Remove(s,f);return true;}
                Decide(s,f,"b5m-chuhai-discard","除害：查看并弃置目标手牌","只有你可以查看目标的真实手牌。弃牌完成后，领取其缺少的资源及商品各一张。");waiting=true;return true;
            }
            if(f.Kind=="b5m-chuhai-reward")
            {
                var other=host.GetHand(f.Targets[0]);var cards=new int[8];if(!IsDrawBlocked(s,f.PlayerId))for(int t=0;t<8;t++)if(other[t]==0&&host.GetBank()[t]>0)cards[t]=1;
                host.RemoveBank(cards);Remove(s,f);Gain(host,f.PlayerId,cards,f.SkillId,f.Id);return true;
            }
            throw new InvalidOperationException("未知B5军事步骤："+f.Kind);
        }
        private static bool AddB5MilitaryDecisionActions(IThreeKingdomsCardHost host,B2SkillFrame f,B2SkillDecision d,List<B2SkillAction> actions)
        {
            if(d.Kind?.StartsWith("b5m-",StringComparison.Ordinal)!=true)return false;
            var s=host.State;var board=(IThreeKingdomsB5Host)host;
            if(d.Kind=="b5m-deploy"||d.Kind=="b5m-return")foreach(var option in board.GetBoardOptions(f.PlayerId,d.Kind=="b5m-deploy"?"deploy-knight":"return-knight"))
            { var a=Action(s,f.SkillId,d.Kind,option.Label,d);a.Command.ProgressCardId=option.Id;actions.Add(a); }
            if(d.Kind=="b5m-tax"||d.Kind=="b5m-chuhai-discard")
            {
                var owner=d.Kind=="b5m-tax"?f.PlayerId:f.Targets[0];
                foreach(int t in Enumerable.Range(0,8).Where(t=>host.GetHand(owner)[t]>0))
                { var a=Action(s,f.SkillId,d.Kind,(d.Kind=="b5m-tax"?"交出":"弃置目标的")+CardNames[t],d);a.Command.CardType=t;a.RealCardType=t;a.CardSelectionKind="select";a.AvailableCount=host.GetHand(owner)[t];actions.Add(a); }
            }
            if(d.Kind=="b5m-luanji-defend")
            {
                actions.Add(Action(s,f.SkillId,"b5m-participate","不抵御，参与本次虚拟入侵",d));
                if(!IsB4HandLossForbidden(s,f.PlayerId))foreach(int t in Enumerable.Range(0,8).Where(t=>host.GetHand(f.PlayerId)[t]>0&&EffectiveCardTypes(s,f.PlayerId,t).Contains(4)))
                {var a=Action(s,f.SkillId,"b5m-resist","打出"+CardNames[t]+"作为矿石抵御",d);a.Command.CardType=t;a.RealCardType=t;a.EffectiveCardType=4;a.CardSelectionKind="payment";a.AvailableCount=host.GetHand(f.PlayerId)[t];actions.Add(a);}
            }
            if(d.Kind=="b5m-zaiqi")
            {
                int flags=f.B4.DeclaredType;
                if((flags&1)!=0)foreach(var option in board.GetBoardOptions(f.PlayerId,"free-City")){var a=Action(s,f.SkillId,"b5m-zaiqi-city",option.Label,d);a.Command.ProgressCardId=option.Id;actions.Add(a);}
                if((flags&2)!=0)actions.Add(Action(s,f.SkillId,"b5m-zaiqi-point","失去一分（总分最低为零）",d));
                if((flags&4)!=0&&!IsDrawBlocked(s,f.PlayerId)&&host.GetBank().Any(n=>n>0))actions.Add(Action(s,f.SkillId,"b5m-zaiqi-card","选择一张资源或商品",d));
                if((flags&8)!=0&&f.B4.VirtualUse!=null)actions.Add(Action(s,f.SkillId,"b5m-zaiqi-double","本次强盗所在生产格对自己双倍产出",d));
            }
            if(d.Kind=="b5m-huoshou-move")
            {
                actions.Add(Action(s,f.SkillId,"b5m-huoshou-skip","放弃移动",d));
                foreach(var option in board.GetTokenMoves(f.PlayerId)){var a=Action(s,f.SkillId,"b5m-huoshou-move",option.Label,d);a.Command.ProgressCardId=option.OptionId;actions.Add(a);}
            }
            return true;
        }
        private static bool ResolveB5MilitaryDecision(IThreeKingdomsCardHost host,B2SkillFrame f,B2SkillDecision d,B2SkillCommand c)
        {
            if(d.Kind?.StartsWith("b5m-",StringComparison.Ordinal)!=true)return false;
            var s=host.State;var actions=new List<B2SkillAction>();AddB5MilitaryDecisionActions(host,f,d,actions);Require(actions.Any(a=>SameB4Action(a.Command,c)),"军事技能选择、费用或目标已经失效。");var board=(IThreeKingdomsB5Host)host;
            if(d.Kind=="b5m-deploy"||d.Kind=="b5m-return")
            {
                var option=board.GetBoardOptions(f.PlayerId,d.Kind=="b5m-deploy"?"deploy-knight":"return-knight").Single(x=>x.Id==c.ProgressCardId);
                if(d.Kind=="b5m-deploy")Remove(s,f);board.ApplyBoardOption(f.PlayerId,option);return true;
            }
            if(d.Kind=="b5m-tax")
            {
                var target=f.Targets[0];var g=Active(s,target);int before=host.GetHand(target).Sum();Remove(s,f);B4Transfer(host,f.PlayerId,target,One(c.CardType),f.Id,f.SkillId,true);
                int actual=Math.Max(0,host.GetHand(target).Sum()-before);if(g?.GeneralId=="tk-g53"&&g.TenureId==f.TenureId)Counter(g,"xianghai-tax",0).Value+=Math.Min(1,actual);return true;
            }
            if(d.Kind=="b5m-luanji-defend")
            {
                int cursor=f.Step++;if(c.Option=="b5m-resist")
                {
                    f.B4.OtherSelection[cursor]=1;f.Remaining++;
                    if(c.CardType!=4)CommitCardConversion(host,f.Id+":resist-conversion:"+cursor,f.PlayerId,c.CardType,4);
                    B4Discard(host,f.PlayerId,One(c.CardType),f.Id+":resist:"+cursor);
                }
                return true;
            }
            if(d.Kind=="b5m-zaiqi")
            {
                if(c.Option=="b5m-zaiqi-card"){f.Kind="mixed";f.Remaining=1;return true;}
                if(c.Option=="b5m-zaiqi-city"){var option=board.GetBoardOptions(f.PlayerId,"free-City").Single(x=>x.Id==c.ProgressCardId);Remove(s,f);board.ApplyBoardOption(f.PlayerId,option);return true;}
                if(c.Option=="b5m-zaiqi-point")
                {
                    var m=Military(s);var adjustment=m.ScoreAdjustments.FirstOrDefault(x=>x.PlayerId==f.PlayerId);if(adjustment==null){adjustment=new B5PlayerCount{PlayerId=f.PlayerId};m.ScoreAdjustments=m.ScoreAdjustments.Concat(new[]{adjustment}).ToArray();}adjustment.Value--;
                }
                if(c.Option=="b5m-zaiqi-double")
                {
                    var m=Military(s);string tile=f.B4.VirtualUse.OptionId;m.DoubleHarvest=m.DoubleHarvest.Where(x=>x.PlayerId!=f.PlayerId||x.TileId!=tile).Concat(new[]{new B5TileHarvest{PlayerId=f.PlayerId,TileId=tile,EventId=f.SourceEventId}}).ToArray();
                }
                Remove(s,f);return true;
            }
            if(d.Kind=="b5m-huoshou-move")
            {
                var move=c.Option=="b5m-huoshou-skip"?null:board.GetTokenMoves(f.PlayerId).Single(x=>x.OptionId==c.ProgressCardId);if(move!=null)board.MoveToken(f.PlayerId,move);Remove(s,f);return true;
            }
            if(d.Kind=="b5m-chuhai-discard")
            {
                f.Kind="b5m-chuhai-reward";B4SkillHistoryEngine.AddPrivateResult(s,f.Id,"弃置目标的"+CardNames[c.CardType],f.PlayerId,f.Targets[0]);B4Discard(host,f.Targets[0],One(c.CardType),f.Id+":discard");return true;
            }
            return true;
        }

        private static void SuspendB5Comparison(IThreeKingdomsCardHost host,B2SkillFrame f)
        {
            var s=host.State;var m=Military(s);Require(m.ComparisonSource==null,"已有比大小尚未完成。");
            m.ComparisonSource=f;m.SuspendedFrames=s.Skills.Frames.Where(x=>x.Id!=f.Id).ToArray();m.SuspendedHarvest=s.Skills.HarvestActive;s.Skills.Frames=Array.Empty<B2SkillFrame>();s.Skills.HarvestActive=false;
            f.Step=0;B3DiceEngine.BeginJudgment(host,f.PlayerId,f.Id+":comparison:0",true);
        }
        public static bool ResumeB5Comparison(IThreeKingdomsCardHost host)
        {
            var s=host.State;var m=s?.Military;if(!B5Enabled(s)||m?.ComparisonSource==null)return false;
            if(HasB4Judgment(s))return false;
            var f=m.ComparisonSource;
            if(f.Step==1&&s.Dice?.Roll?.EventId==f.Id+":comparison:0"&&s.Dice.Roll.Submitted)
            {
                if(HasPending(s)||B3DiceEngine.HasPending(s))return true;
                B3DiceEngine.BeginJudgment(host,f.Targets[0],f.Id+":comparison:1",true);
            }
            var result=B3DiceEngine.TakeFinal(host);if(result==null)return true;
            Require(result.EventId==f.Id+":comparison:"+f.Step,"比大小结果与已保存来源不一致。");
            f.B4.Selection[f.Step]=result.Total;f.Step++;
            OnDiceResolved(host,result.EventId,false,Array.Empty<string>(),result.NumericActualRoll,false,true,result.Total);
            if(f.Step==1)return true;
            // 第二次骰后观察先于比大小的奖励；恢复时保留已经排出的独立观察技能。
            s.Skills.Frames=s.Skills.Frames.Concat(m.SuspendedFrames).ToArray();s.Skills.HarvestActive=m.SuspendedHarvest;m.SuspendedFrames=Array.Empty<B2SkillFrame>();m.SuspendedHarvest=false;m.ComparisonSource=null;
            B4SkillHistoryEngine.Complete(s,f.Id,"比大小："+f.B4.Selection[0]+" 对 "+f.B4.Selection[1]);
            if(f.B4.Selection[0]>f.B4.Selection[1]){f.Kind="b5m-chuhai-choice";Append(s,f);}
            else m.PublicHands=m.PublicHands.Where(x=>x.PlayerId!=f.PlayerId).Concat(new[]{new B5PlayerCount{PlayerId=f.PlayerId,TurnId=s.Clock.OpportunityId,TenureId=f.TenureId}}).ToArray();
            Pump(host);return true;
        }
        public static void ProjectB5Military(IThreeKingdomsCardHost host,ThreeKingdomsView view,string viewer)
        {
            if(!B5Enabled(host.State))return;var s=host.State;var result=new List<B5RevealedHand>();
            foreach(var player in s.SeatPlayerIds.Where(p=>B5IsHandPublic(s,p)))result.Add(new B5RevealedHand{PlayerId=player,Cards=host.GetHand(player),Public=true});
            var f=s.Skills.Frames.FirstOrDefault(x=>x.Kind=="b5m-chuhai-choice"&&x.PlayerId==viewer);
            if(f!=null&&!result.Any(x=>x.PlayerId==f.Targets[0]))result.Add(new B5RevealedHand{PlayerId=f.Targets[0],Cards=host.GetHand(f.Targets[0]),Public=false});
            view.MilitaryHands=result.ToArray();
            var status=new List<string>();
            foreach(var g in s.Generals.Where(g=>g.Region==GeneralRegion.Active&&g.GeneralId=="tk-g53"))status.Add(g.OwnerPlayerId+"：乡害，真实投掷合计 +3（固定七不加），其他玩家手牌上限 -1");
            foreach(var p in s.Military?.PermanentShanqiPlayers??Array.Empty<string>())status.Add(p+"：整局单骑，恰好一枚骑士时有效四级");
            foreach(var p in s.Military?.PermanentYaowuPlayers??Array.Empty<string>())status.Add(p+"：整局耀武，手牌上限 +4、发展牌上限 +2");
            if((s.Board?.LastFormalTotal??0)>0)status.Add("最后正式骰局合计："+s.Board.LastFormalTotal+"（含有效合计修正；数字骰面保持原值）");
            view.SkillStatus=(view.SkillStatus??Array.Empty<string>()).Concat(status).ToArray();
        }
        public static string[] ValidateB5Military(ThreeKingdomsState s)
        {
            var m=s?.Military;if(m==null)return Array.Empty<string>();var errors=new List<string>();
            if(!B5Enabled(s))return new[]{"旧规则包不能包含B5军事状态。"};
            bool Player(string p)=>p!=null&&s.SeatPlayerIds.Contains(p);
            if(m.PermanentShanqiPlayers==null||m.PermanentYaowuPlayers==null||m.TurnHandSnapshots==null||m.PublicHands==null||m.DelayedDeployments==null||m.HuoshouEnds==null||m.ScoreAdjustments==null||m.DoubleHarvest==null||m.SuspendedFrames==null)return new[]{"B5军事存档缺少必需数组。"};
            if(m.PermanentShanqiPlayers.Concat(m.PermanentYaowuPlayers).Any(p=>!Player(p))||m.PermanentShanqiPlayers.Distinct().Count()!=m.PermanentShanqiPlayers.Length||m.PermanentYaowuPlayers.Distinct().Count()!=m.PermanentYaowuPlayers.Length)errors.Add("B5永久军事效果玩家无效或重复。");
            foreach(var list in new[]{m.TurnHandSnapshots,m.PublicHands,m.DelayedDeployments,m.HuoshouEnds,m.ScoreAdjustments})if(list.Any(x=>x==null||!Player(x.PlayerId))||list.Select(x=>x?.PlayerId).Distinct().Count()!=list.Length)errors.Add("B5玩家军事标记无效或重复。");
            if(errors.Count>0)return errors.ToArray();
            if(m.TurnHandSnapshots.Any(x=>x.Value<0||x.TurnId<0||x.TurnId>s.Clock.TurnInstanceId)||m.ScoreAdjustments.Any(x=>x.Value>0)||m.ImmediateInvasionTurnId < -1||m.ImmediateInvasionTurnId>s.Clock.TurnInstanceId)errors.Add("B5军事计数超出合法范围。");
            if(m.PublicHands.Any(x=>x.TurnId<0||x.TurnId>s.Clock.OpportunityId)||m.HuoshouEnds.Any(x=>x.TurnId<0||x.TurnId>s.Clock.TurnInstanceId))errors.Add("军事持续效果期限超出当前行动机会。");
            if(m.DoubleHarvest.Any(x=>x==null||!Player(x.PlayerId)||string.IsNullOrWhiteSpace(x.TileId)||string.IsNullOrWhiteSpace(x.EventId)))errors.Add("再起倍产缺少稳定地块或来源。");
            if(m.ComparisonSource==null&&(m.SuspendedFrames.Length>0||m.SuspendedHarvest))errors.Add("比大小挂起区缺少来源。");
            if(m.ComparisonSource!=null)
            {
                var f=m.ComparisonSource;if(f.Kind!="b5m-chuhai"||!Player(f.PlayerId)||f.Targets==null||f.Targets.Length!=1||!Player(f.Targets[0])||f.Step<0||f.Step>1||f.B4==null||!Valid(f.Cards)||f.Cards.Sum()!=0||!Valid(f.SelectedCards)||f.SelectedCards.Sum()!=0)errors.Add("除害比大小来源或阶段无效。");
                if(m.SuspendedFrames.Any(x=>x==null||x.Cards==null||x.Cards.Length!=8||x.Cards.Any(n=>n<0)))errors.Add("比大小挂起帧实体区无效。");
            }
            return errors.ToArray();
        }
    }
}
