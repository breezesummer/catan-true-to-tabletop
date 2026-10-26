using System;
using System.Collections.Generic;
using System.Linq;

namespace Catan.Core.ThreeKingdoms
{
    public static partial class B2SkillEngine
    {
        private static void AddMapB6Actions(IThreeKingdomsCardHost host,GeneralInstance g,List<B2SkillAction> actions)
        {
            var s=host.State;
            if(!B6Enabled(s)||g.GeneralId!="tk-g47"||s.Clock.ActivePlayerId!=g.OwnerPlayerId||s.Clock.Phase!=ThreeKingdomsPhase.Play||Value(g,"tamo",0)>0||!(host is IThreeKingdomsB6MapHost map))return;
            foreach(var swap in map.GetMapB6Swaps())
            {
                var a=Action(s,"tk-g47-s1","map-tamo","榻谟：交换 "+swap.FirstTileId+" 与 "+swap.SecondTileId);
                a.Command.TileIds=new[]{swap.FirstTileId,swap.SecondTileId};a.SelectionBoardIds=a.Command.TileIds.ToArray();actions.Add(a);
            }
        }
        private static bool ResolveMapB6Active(IThreeKingdomsCardHost host,GeneralInstance g,B2SkillCommand c)
        {
            if(c.Option!="map-tamo")return false;
            var s=host.State;
            Require(B6Enabled(s)&&g.GeneralId=="tk-g47"&&c.SkillId=="tk-g47-s1"&&s.Clock.ActivePlayerId==g.OwnerPlayerId&&s.Clock.Phase==ThreeKingdomsPhase.Play&&Value(g,"tamo",0)==0&&host is IThreeKingdomsB6MapHost&&c.TileIds?.Length==2,"当前不能发动榻谟。");
            ((IThreeKingdomsB6MapHost)host).ApplyMapB6Swap(c.TileIds[0],c.TileIds[1]);
            Counter(g,"tamo",0).Value=1;
            var f=Frame(s,g,c.SkillId,"map-tamo-complete",0,null);f.Started=true;Append(s,f);return true;
        }
        private static void AddMapB6EndFrames(IThreeKingdomsCardHost host,GeneralInstance g,string eventId)
        {
            if(!B6Enabled(host.State)||g.GeneralId!="tk-g47"||host.State.Clock.ActivePlayerId!=g.OwnerPlayerId||!MapB6Targets(host,g.OwnerPlayerId).Any())return;
            var f=Frame(host.State,g,"tk-g47-s2","map-zhimeng",0,eventId);f.Optional=true;Append(host.State,f);
        }
        private static string[] MapB6Targets(IThreeKingdomsCardHost host,string owner) => host.State.SeatPlayerIds.Where(p=>p!=owner&&MapB6CanRedistribute(host,owner,p)).ToArray();
        private static bool MapB6CanRedistribute(IThreeKingdomsCardHost host,string owner,string target)
        {
            var s=host.State;
            if(!s.SeatPlayerIds.Contains(target)||owner==target||IsB4HandLossForbidden(s,owner)||IsB4HandLossForbidden(s,target)||IsDrawBlocked(s,owner)||IsDrawBlocked(s,target)||IsProtected(s,target)||IsProtected(s,owner))return false;
            var own=host.GetHand(owner);var other=host.GetHand(target);
            if(!B6AllianceRules.CanTrade(s,owner,target)||Enumerable.Range(0,8).Any(t=>(own[t]>0&&!CanGainB6(s,target,t))||(other[t]>0&&!CanGainB6(s,owner,t))))return false;
            return own.Sum()+other.Sum()>0;
        }
        private static bool ProcessMapB6Frame(IThreeKingdomsCardHost host,B2SkillFrame f)
        {
            if(f.Kind=="map-tamo-complete"){Remove(host.State,f);return false;}
            if(f.Kind!="map-zhimeng")return false;
            if(f.Targets.Length==0)
            {
                if(!MapB6Targets(host,f.PlayerId).Any()){Remove(host.State,f);return false;}
                Decide(host.State,f,"map-zhimeng-target","智盟：选择均分手牌的玩家","双方随机均分资源与商品，奇数时你多得一张。");return true;
            }
            if(MapB6CanRedistribute(host,f.PlayerId,f.Targets[0]))MapB6Redistribute(host,f);
            Remove(host.State,f);return false;
        }
        private static bool AddMapB6DecisionActions(IThreeKingdomsCardHost host,B2SkillFrame f,B2SkillDecision d,List<B2SkillAction> actions)
        {
            if(d.Kind!="map-zhimeng-target")return false;
            foreach(var target in MapB6Targets(host,f.PlayerId))
            {var a=Action(host.State,f.SkillId,"map-zhimeng-target","智盟：与 "+target+" 均分手牌",d);a.Command.TargetPlayerIds=new[]{target};a.SelectionPlayerIds=new[]{target};actions.Add(a);}
            return true;
        }
        private static bool ResolveMapB6Decision(IThreeKingdomsCardHost host,B2SkillFrame f,B2SkillDecision d,B2SkillCommand c)
        {
            if(d.Kind!="map-zhimeng-target")return false;
            Require(c.Option=="map-zhimeng-target"&&c.TargetPlayerIds?.Length==1&&MapB6CanRedistribute(host,f.PlayerId,c.TargetPlayerIds[0]),"智盟目标已失效。");
            f.Targets=c.TargetPlayerIds.ToArray();Target(host,f.Id,f.PlayerId,f.Targets[0]);return true;
        }
        private static void MapB6Redistribute(IThreeKingdomsCardHost host,B2SkillFrame f)
        {
            var owner=f.PlayerId;var target=f.Targets[0];var first=host.GetHand(owner);var second=host.GetHand(target);
            var cards=new List<Tuple<int,bool>>();for(int t=0;t<8;t++){for(int n=0;n<first[t];n++)cards.Add(Tuple.Create(t,true));for(int n=0;n<second[t];n++)cards.Add(Tuple.Create(t,false));}
            for(int i=cards.Count-1;i>0;i--){int j=host.DrawRandom(i+1);var temp=cards[i];cards[i]=cards[j];cards[j]=temp;}
            int ownerCount=(cards.Count+1)/2;var lostFirst=new int[8];var lostSecond=new int[8];
            for(int i=0;i<cards.Count;i++){var card=cards[i];if(i<ownerCount&&!card.Item2)lostSecond[card.Item1]++;if(i>=ownerCount&&card.Item2)lostFirst[card.Item1]++;}
            host.RemoveHand(owner,lostFirst);host.RemoveHand(target,lostSecond);host.AddHand(owner,lostSecond);host.AddHand(target,lostFirst);
            var changes=new[]{new B2HandChange {PlayerId=owner,BeforeCount=first.Sum(),AfterCount=ownerCount,LostCount=lostFirst.Sum(),LostCards=lostFirst,GainedCards=lostSecond,SourcePlayerId=target,SourceSkillId=f.SkillId},new B2HandChange {PlayerId=target,BeforeCount=second.Sum(),AfterCount=cards.Count-ownerCount,LostCount=lostSecond.Sum(),LostCards=lostSecond,GainedCards=lostFirst,SourcePlayerId=owner,SourceSkillId=f.SkillId}};
            RecordCards(host,f.Id+":redistribute",changes);
        }
    }
}
