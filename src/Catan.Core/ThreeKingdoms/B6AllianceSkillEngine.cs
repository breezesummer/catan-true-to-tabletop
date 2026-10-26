using System;
using System.Collections.Generic;
using System.Linq;

namespace Catan.Core.ThreeKingdoms
{
    public static partial class B2SkillEngine
    {
        private static B6AllianceState AllianceB6(ThreeKingdomsState s) => s.AllianceB6??(s.AllianceB6=new B6AllianceState());
        public static B6AllianceState CloneB6Alliance(B6AllianceState value) => value==null?null:new B6AllianceState {
            Sources=value.Sources.Select(a=>new B6AllianceSource{SourcePlayerId=a.SourcePlayerId,TenureId=a.TenureId,Members=(string[])a.Members.Clone()}).ToArray(),
            WinnerPlayerIds=(string[])value.WinnerPlayerIds.Clone(),KnightsNeedHomeCheck=value.KnightsNeedHomeCheck,ReturnHomePlayerIds=(string[])value.ReturnHomePlayerIds.Clone()};
        public static void OnAllianceB6GeneralActivated(ThreeKingdomsState s,GeneralInstance g)
        {
            if(!B6AllianceRules.Enabled(s)||g.GeneralId!="tk-g50"||!Mark(s,"b6-alliance-entry:"+g.TenureId))return;
            var f=Frame(s,g,"tk-g50-s1","alliance-b6-zili",0,"entry:"+g.TenureId);f.B4=new B4SkillPayload{OwnerPlayerId=g.OwnerPlayerId};
            int index=Array.IndexOf(s.SeatPlayerIds,g.OwnerPlayerId);
            f.Targets=Enumerable.Range(1,s.SeatPlayerIds.Length-1).Select(i=>s.SeatPlayerIds[(index+i)%s.SeatPlayerIds.Length]).ToArray();
            AllianceB6(s).Sources=AllianceB6(s).Sources.Concat(new[]{new B6AllianceSource{SourcePlayerId=g.OwnerPlayerId,TenureId=g.TenureId,Members=new[]{g.OwnerPlayerId}}}).ToArray();
            Append(s,f);Append(s,Frame(s,g,"tk-g50-s2","alliance-b6-paiyi",0,"entry:"+g.TenureId));
        }
        public static void OnAllianceB6GeneralRetired(ThreeKingdomsState s,GeneralInstance g)
        {
            if(!B6AllianceRules.Enabled(s)||s.AllianceB6==null)return;
            var source=s.AllianceB6.Sources.FirstOrDefault(x=>x.TenureId==g.TenureId);
            if(source!=null)
            {
                s.AllianceB6.ReturnHomePlayerIds=s.AllianceB6.ReturnHomePlayerIds.Union(B6AllianceRules.Members(s,source.SourcePlayerId)).ToArray();
                s.AllianceB6.Sources=s.AllianceB6.Sources.Where(x=>x.TenureId!=g.TenureId).ToArray();s.AllianceB6.KnightsNeedHomeCheck=true;
            }
        }
        private static bool ProcessAllianceB6Frame(IThreeKingdomsCardHost host,B2SkillFrame f,out bool waiting)
        {
            waiting=false;if(!f.Kind.StartsWith("alliance-b6-",StringComparison.Ordinal))return false;
            var s=host.State;Require(B6AllianceRules.Enabled(s),"当前规则不包含联盟。");
            if(f.Kind=="alliance-b6-paiyi")
            {
                if(f.Step>=2||IsDrawBlocked(s,f.PlayerId)){Remove(s,f);return true;}
                int color=f.Step++==0?1:0;if(host.GetProgressDeckCount(color)>0)host.DrawProgress(f.PlayerId,color);return true;
            }
            string owner=f.B4.OwnerPlayerId;
            if(!AllianceB6(s).Sources.Any(x=>x.TenureId==f.TenureId)||f.Step>=f.Targets.Length){Remove(s,f);return true;}
            string target=f.Targets[f.Step];
            if(B6AllianceRules.Allies(s,owner,target)){NextAllianceB6(f);return true;}
            if(!B6AllianceRules.CanMerge(s,owner,target)){NextAllianceB6(f);return true;}
            f.PlayerId=f.Remaining==0||f.Remaining==4?target:owner;
            string kind=f.Remaining==0?"join":f.Remaining==1?"accept":f.Remaining==2||f.Remaining==3?"terms":"confirm";
            Decide(s,f,"alliance-b6-"+kind,"自立："+(kind=="join"?"选择阵营":kind=="accept"?"回应结盟请求":kind=="terms"?"选择结盟条件":"确认结盟条件"),
                f.Remaining==4?AllianceTermsB6(f):f.Remaining==2?"选择对方向你转让的资源或商品。":f.Remaining==3?"选择你向对方转让的资源或商品。":"盟友共享港口、科技能力及骑士，不能互相攻伐，共同胜利。");
            waiting=true;return true;
        }
        private static string AllianceTermsB6(B2SkillFrame f)
        {var cards=f.Cards.Sum()>0?f.Cards:f.SelectedCards;return (f.Cards.Sum()>0?"向钟会转让：":"钟会向你转让：")+string.Join("、",Enumerable.Range(0,8).Where(t=>cards[t]>0).Select(t=>CardNames[t]+"×"+cards[t]));}
        private static void NextAllianceB6(B2SkillFrame f){f.Step++;f.Remaining=0;f.Cards=new int[8];f.SelectedCards=new int[8];}
        private static bool AddAllianceB6DecisionActions(IThreeKingdomsCardHost host,B2SkillFrame f,B2SkillDecision d,List<B2SkillAction> actions)
        {
            if(!d.Kind.StartsWith("alliance-b6-",StringComparison.Ordinal))return false;
            var s=host.State;string owner=f.B4.OwnerPlayerId,target=f.Targets[f.Step];
            void AddChoice(string option,string label){var a=Action(s,f.SkillId,"alliance-b6-"+option,label,d);a.SelectionPlayerIds=new[]{target};actions.Add(a);}
            if(d.Kind=="alliance-b6-join"){AddChoice("join","请求成为盟友");AddChoice("reject","宣战");}
            else if(d.Kind=="alliance-b6-accept")
            {AddChoice("accept","同意结盟");AddChoice("reject","拒绝结盟");if(CanB6ControlExchange(s,owner,target,1,0)){AddChoice("demand","以对方转让手牌为条件");if(host.GetHand(owner).Sum()>0&&!IsB4HandLossForbidden(s,owner))AddChoice("gift","以赠予对方手牌为条件");}}
            else if(d.Kind=="alliance-b6-terms")
            {
                var selected=f.Remaining==2?f.Cards:f.SelectedCards;var hand=host.GetHand(owner);
                for(int t=0;t<8;t++)if(selected[t]<(f.Remaining==2?19:hand[t]))
                {var a=Action(s,f.SkillId,"alliance-b6-card","加入"+CardNames[t],d);a.Command.CardType=t;a.RealCardType=t;a.CardSelectionKind=f.Remaining==2?"declare":"select";a.AvailableCount=f.Remaining==2?19-selected[t]:hand[t]-selected[t];actions.Add(a);}
                if(selected.Sum()>0)AddChoice("offer","提出条件");AddChoice("reset","重新选择条件");AddChoice("reject","拒绝结盟");
            }
            else if(d.Kind=="alliance-b6-confirm"){if(CanAllianceB6Transfer(host,f))AddChoice("confirm","同意条件并结盟");AddChoice("reject","拒绝条件并宣战");}
            return true;
        }
        private static bool CanAllianceB6Transfer(IThreeKingdomsCardHost host,B2SkillFrame f)
        {
            string owner=f.B4.OwnerPlayerId,target=f.Targets[f.Step],from=f.Cards.Sum()>0?target:owner,to=f.Cards.Sum()>0?owner:target;var cards=f.Cards.Sum()>0?f.Cards:f.SelectedCards;
            return CanB6ControlExchange(host.State,from,to,cards.Sum(),0)&&B6AllianceRules.CanMerge(host.State,owner,target)&&!IsB4HandLossForbidden(host.State,from)&&!IsDrawBlocked(host.State,to)&&Contains(host.GetHand(from),cards)&&CanGainCardsB6(host.State,to,cards);
        }
        public static bool CanB6ControlExchange(ThreeKingdomsState s,string first,string second,int give,int take)
        {
            var grant=s?.ControlB6;
            return !B6Enabled(s)||grant==null||grant.OpportunityId!=s.Clock.OpportunityId||(grant.AssetPlayerId!=first&&grant.AssetPlayerId!=second)||give==take;
        }
        private static void MergeAllianceB6(ThreeKingdomsState s,B2SkillFrame f)
        {
            string owner=f.B4.OwnerPlayerId,target=f.Targets[f.Step];Require(B6AllianceRules.CanMerge(s,owner,target),"联盟人数已达上限。");
            var source=AllianceB6(s).Sources.Single(x=>x.TenureId==f.TenureId);source.Members=source.Members.Concat(new[]{target}).Distinct().ToArray();
        }
        private static bool ResolveAllianceB6Decision(IThreeKingdomsCardHost host,B2SkillFrame f,B2SkillDecision d,B2SkillCommand c)
        {
            if(!d.Kind.StartsWith("alliance-b6-",StringComparison.Ordinal))return false;
            var actions=new List<B2SkillAction>();AddAllianceB6DecisionActions(host,f,d,actions);Require(actions.Any(a=>SameCivilAction(a.Command,c)),"联盟选择已经失效。");
            var s=host.State;string owner=f.B4.OwnerPlayerId,target=f.Targets[f.Step];
            switch(c.Option)
            {
                case "alliance-b6-join":f.Remaining=1;break;
                case "alliance-b6-reject":NextAllianceB6(f);break;
                case "alliance-b6-accept":MergeAllianceB6(s,f);NextAllianceB6(f);break;
                case "alliance-b6-demand":f.Remaining=2;break;
                case "alliance-b6-gift":f.Remaining=3;break;
                case "alliance-b6-card":(f.Remaining==2?f.Cards:f.SelectedCards)[c.CardType]++;break;
                case "alliance-b6-reset":f.Cards=new int[8];f.SelectedCards=new int[8];f.Remaining=1;break;
                case "alliance-b6-offer":f.Remaining=4;break;
                case "alliance-b6-confirm":
                    Require(CanAllianceB6Transfer(host,f),"结盟条件无法执行。");bool demand=f.Cards.Sum()>0;var cards=Copy(demand?f.Cards:f.SelectedCards);
                    MergeAllianceB6(s,f);int step=f.Step;NextAllianceB6(f);B4Transfer(host,demand?target:owner,demand?owner:target,cards,f.Id+":terms:"+step,f.SkillId,true);break;
            }
            return true;
        }
        public static void ProjectAllianceB6(IThreeKingdomsCardHost host,ThreeKingdomsView view,string viewer)
        {if(!B6AllianceRules.Enabled(host.State))return;view.AllianceMembers=B6AllianceRules.Members(host.State,viewer);view.AllianceWinnerPlayerIds=host.State.AllianceB6?.WinnerPlayerIds??Array.Empty<string>();}
        public static string[] ValidateAllianceB6(ThreeKingdomsState s)
        {
            if(s.AllianceB6==null)return Array.Empty<string>();if(!B6AllianceRules.Enabled(s))return new[]{"旧规则包不能包含联盟状态。"};var a=s.AllianceB6;
            if(a.Sources==null||a.WinnerPlayerIds==null||a.ReturnHomePlayerIds==null)return new[]{"联盟状态缺少数组。"};
            if(a.Sources.Any(x=>x==null||x.Members==null||!s.SeatPlayerIds.Contains(x.SourcePlayerId)||string.IsNullOrEmpty(x.TenureId)||!x.Members.Contains(x.SourcePlayerId)||x.Members.Distinct().Count()!=x.Members.Length||x.Members.Any(p=>!s.SeatPlayerIds.Contains(p)))||a.Sources.Select(x=>x.TenureId).Distinct().Count()!=a.Sources.Length)return new[]{"联盟来源或成员无效。"};
            if(s.SeatPlayerIds.Any(p=>B6AllianceRules.Members(s,p).Length>1+s.SeatPlayerIds.Length/2)||a.WinnerPlayerIds.Concat(a.ReturnHomePlayerIds).Any(p=>!s.SeatPlayerIds.Contains(p)))return new[]{"联盟人数或胜利成员无效。"};return Array.Empty<string>();
        }
    }
}
