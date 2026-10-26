using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Catan.Core.ThreeKingdoms;

namespace Catan.Core.M5
{
    public sealed partial class Command
    {
        [DataMember(Order=330, EmitDefaultValue=false)] public int? JiaoziCardType { get; set; }
    }
    public sealed partial class GameState
    {
        [IgnoreDataMember] internal int? B6ConstructionCardType { get; set; }
    }
    public sealed partial class CombinedGameSession
    {
        private sealed partial class B2Host : IThreeKingdomsB6SeatTradeHost
        {
            public bool AreTerritoriesAdjacent(string first, string second)
            {
                if(first==second || !State.SeatPlayerIds.Contains(first) || !State.SeatPlayerIds.Contains(second)) return false;
                string[] Sites(string p)
                {
                    var points=game.Settlements.Concat(game.Cities).Where(b=>b.PlayerId==p).Select(b=>b.LocationId);
                    var roads=game.Roads.Where(r=>r.PlayerId==p&&B5Working(game,"Road",r.LocationId)).Select(r=>r.LocationId).Concat(game.Ships.Where(r=>r.PlayerId==p&&B5Working(game,"Ship",r.LocationId)).Select(r=>r.LocationId));
                    return points.Concat(session.scenario.Topology.Edges.Where(e=>roads.Contains(e.Id)).SelectMany(e=>e.Vertices)).Distinct().ToArray();
                }
                var a=Sites(first);var b=Sites(second);
                return a.Intersect(b).Any() || game.Tiles.Any(t=>t.Resource!="sea"&&t.Vertices.Any(a.Contains)&&t.Vertices.Any(b.Contains));
            }
            public int[] JiaoziChoices(B4VirtualUse construction) => session.SeatTradeB6ConstructionChoices(game,construction);
            public void CompleteJiaoziConstruction(B4VirtualUse construction,int cardType)
            {
                var c=Json.Read<Command>(construction.PayloadJson);c.JiaoziCardType=cardType;c.WindowId=State.WindowId;
                if(construction.CardId=="native")session.ApplyB2(game,c);
                else session.ApplyB2Native(game,c,true,construction.CardId=="virtual-prepare");
            }
        }

        private ResourceBag SeatTradeB6StandardCost(GameState s,Command c)
        {
            switch(c.Kind)
            {
                case CommandKind.BuildRoad:return new ResourceBag{Wood=1,Brick=1};
                case CommandKind.BuildSettlement:return new ResourceBag{Wood=1,Brick=1,Wool=1,Wheat=1};
                case CommandKind.BuildCity:if(!s.DowngradedCities.Any(x=>x.PlayerId==c.PlayerId&&x.LocationId==c.TargetId))return new ResourceBag{Wheat=2,Ore=3};return null;
                case CommandKind.BuildWall:return new ResourceBag{Brick=2};
                case CommandKind.BuildShip:return new ResourceBag{Wood=1,Wool=1};
                case CommandKind.PlayProgressCard:if(c.ProgressCard==ProgressCardKind.Medicine&&!s.DowngradedCities.Any(x=>x.PlayerId==c.PlayerId&&x.LocationId==c.TargetId))return new ResourceBag{Wheat=2,Ore=3};return null;
                default:return null;
            }
        }
        private bool BeginSeatTradeB6Construction(GameState s,Command c,bool virtualCard,bool virtualPrepare)
        {
            if(!B2SkillEngine.B6Enabled(s.ThreeKingdoms)||s.B5FreeConstruction||c.JiaoziCardType.HasValue||B2SkillEngine.JiaoziAdjustmentB6(CardHost(s),c.PlayerId)==0)return false;
            var standard=SeatTradeB6StandardCost(s,c);if(standard==null)return false;
            var use=new B4VirtualUse{PayloadJson=Json.Write(c),CardId=virtualCard?(virtualPrepare?"virtual-prepare":"virtual"):"native",SourceEventId=c.Id};
            Require(SeatTradeB6ConstructionChoices(s,use).Length>0,"InvalidConstruction","当前不能完成此项建造。");
            B2SkillEngine.QueueJiaoziB6(CardHost(s),use);return true;
        }
        private int[] SeatTradeB6ConstructionChoices(GameState s,B4VirtualUse use)
        {
            var c=Json.Read<Command>(use.PayloadJson);var standard=SeatTradeB6StandardCost(s,c);if(standard==null)return Array.Empty<int>();
            var result=new List<int>();
            foreach(var type in Enumerable.Range(0,5).Where(t=>standard[(Resource)t]>0))
            {
                var probe=s.CopyForTransaction();
                probe.ThreeKingdoms.Skills.Frames=probe.ThreeKingdoms.Skills.Frames.Where(f=>f.Kind!="seat-jiaozi").ToArray();
                probe.ThreeKingdoms.Skills.PendingDecision=null;probe.ThreeKingdoms.Skills.SuppressPump=true;
                var candidate=Json.Copy(c);candidate.JiaoziCardType=type;probe.B6ConstructionCardType=type;probe.B4PaymentSelection=candidate.PaymentCards;
                try{if(use.CardId=="native")ApplyBaseRules(probe,candidate);else ApplyProgressCommand(probe,probe.Players.Single(p=>p.Id==candidate.PlayerId),candidate,true,use.CardId=="virtual-prepare");result.Add(type);}
                catch(RuleException){}catch(InvalidOperationException){}
            }
            return result.ToArray();
        }
        private void PayB6Construction(GameState s,PlayerState p,ResourceBag discounted,ResourceBag standard)
        {
            var adjustment=s.B5FreeConstruction?0:B2SkillEngine.JiaoziAdjustmentB6(CardHost(s),p.Id);
            if(adjustment==0||discounted.Total==0){Pay(s,p,discounted);return;}
            Require(s.B6ConstructionCardType.HasValue,"JiaoziChoiceRequired","请选择骄姿调整的资源。");
            var adjusted=B2SkillEngine.JiaoziCostB6(Enumerable.Range(0,8).Select(i=>i<5?discounted[(Resource)i]:0).ToArray(),
                Enumerable.Range(0,8).Select(i=>i<5?standard[(Resource)i]:0).ToArray(),adjustment,s.B6ConstructionCardType.Value);
            var cost=new ResourceBag();for(int i=0;i<5;i++)cost[(Resource)i]=adjusted[i];Pay(s,p,cost);
        }
    }
}
