using System;
using System.Collections.Generic;
using System.Linq;

namespace Catan.Core.M5
{
    public sealed partial class CombinedGameSession
    {
        private static readonly Commodity[] Commodities = (Commodity[])Enum.GetValues(typeof(Commodity));
        private static bool ValidBag(CommodityBag b) => b != null && Commodities.All(c=>b[c]>=0&&b[c]<=12);
        private static bool Covers(CommodityBag a,CommodityBag b) => Commodities.All(c=>a[c]>=b[c]);
        private static void Transfer(CommodityBag from,CommodityBag to,Commodity c,int amount) { Require(amount>=0&&from[c]>=amount,"InsufficientCommodities","商品不足。"); from[c]-=amount; to[c]+=amount; }
        private static void TransferBag(CommodityBag from,CommodityBag to,CommodityBag amount) { foreach(var c in Commodities) Transfer(from,to,c,amount[c]); }
        private static int HandCount(PlayerState p) => p.Resources.Total+p.Commodities.Total;
        private IEnumerable<PlayerState> TurnOrder(GameState s) { int start=Array.FindIndex(s.Players,p=>p.Id==s.ActivePlayerId); return Enumerable.Range(0,s.Players.Length).Select(i=>s.Players[(start+i)%s.Players.Length]); }
        private void QueueDecision(GameState s,PendingDecision decision)
        {
            if(s.PendingDecision==null && s.DecisionQueue.Length==0 && !s.ProductionPending) s.DecisionReturnPhase=s.Phase;
            s.DecisionQueue=Append(s.DecisionQueue,decision);
        }
        private void ContinueDecisions(GameState s)
        {
            if(s.Phase==GamePhase.Finished)return;
            s.PendingDecision=null;
            while(s.DecisionQueue.Length>0)
            {
                var next=s.DecisionQueue[0];s.DecisionQueue=s.DecisionQueue.Skip(1).ToArray();
                if(next.Kind=="ProgressDraw") {DrawProgressCard(s,s.Players.Single(p=>p.Id==next.PlayerId),next.Track);CheckVictory(s);if(s.Phase==GamePhase.Finished)return;continue;}
                if(next.Kind=="BarbarianReturnHome") {foreach(var knight in s.Knights)knight.Active=false;s.BarbarianPosition=0;if(s.PirateTileId==null)s.PirateTileId=seafaring.InitialPirateTileId;continue;}
                if(next.Kind=="Aqueduct"&&s.Bank.Total==0)continue;
                s.PendingDecision=next;s.Phase=GamePhase.PendingChoice;return;
            }
            if(s.ProductionPending) { s.ProductionPending=false; ResolveProduction(s); return; }
            s.Phase=s.DecisionReturnPhase;
        }
        private void RollCitiesKnights(GameState s)
        {
            s.LastDice1=s.AlchemyDice1>0?s.AlchemyDice1:Draw(s.Random,6)+1;
            s.LastDice2=s.AlchemyDice2>0?s.AlchemyDice2:Draw(s.Random,6)+1;
            s.AlchemyDice1=0;s.AlchemyDice2=0;
            int eventRoll=Draw(s.Random,6); s.LastEventDie=eventRoll<3?EventDie.Barbarian:(EventDie)(eventRoll-2);
            s.ProductionPending=true; s.DecisionReturnPhase=GamePhase.Action;
            if(s.LastEventDie==EventDie.Barbarian) { s.BarbarianPosition++; if(s.BarbarianPosition==7) BarbarianAttack(s); }
            else EnqueueEventProgress(s,(ImprovementTrack)((int)s.LastEventDie-1),s.LastDice1);
            CheckVictory(s);if(s.Phase==GamePhase.Finished)return;
            ContinueDecisions(s);
        }
        private void ResolveProduction(GameState s)
        {
            int total=s.LastDice1+s.LastDice2;
            if(total==7)
            {
                s.Discards=TurnOrder(s).Where(p=>HandCount(p)>7+2*s.Walls.Count(w=>w.PlayerId==p.Id)).Select(p=>new DiscardRequirement {PlayerId=p.Id,Amount=HandCount(p)/2}).ToArray();
                if(s.Discards.Length>0) { s.PendingDecision=new PendingDecision {Kind="Robber",PlayerId=s.ActivePlayerId,ReturnPhase=GamePhase.Action}; s.Phase=GamePhase.Discard; }
                else if(s.FirstBarbarianAttack) { s.PendingDecision=new PendingDecision {Kind="Robber",PlayerId=s.ActivePlayerId,ReturnPhase=GamePhase.Action}; s.Phase=GamePhase.RobberMove; }
                else s.Phase=GamePhase.Action;
                return;
            }
            var before=s.Players.ToDictionary(p=>p.Id,HandCount); Produce(s,total); s.Phase=GamePhase.Action;
            var gold=s.Players.ToDictionary(p=>p.Id,p=>s.Tiles.Where(t=>t.Resource=="gold"&&t.Number==total&&t.Id!=s.RobberTileId).Sum(t=>s.Settlements.Count(b=>b.PlayerId==p.Id&&t.Vertices.Contains(b.LocationId))+2*s.Cities.Count(b=>b.PlayerId==p.Id&&t.Vertices.Contains(b.LocationId))));
            foreach(var p in TurnOrder(s).Where(p=>gold[p.Id]>0)) QueueDecision(s,new PendingDecision {Kind="Gold",PlayerId=p.Id,Amount=gold[p.Id],ReturnPhase=GamePhase.Action});
            foreach(var p in TurnOrder(s).Where(p=>p.Improvements[(int)ImprovementTrack.Science]>=3&&HandCount(p)==before[p.Id]&&gold[p.Id]==0))
                if(s.Bank.Total>0) QueueDecision(s,new PendingDecision {Kind="Aqueduct",PlayerId=p.Id,ReturnPhase=GamePhase.Action});
            if(s.DecisionQueue.Length>0) ContinueDecisions(s);
        }
        private void BarbarianAttack(GameState s)
        {
            s.FirstBarbarianAttack=true;
            if(s.RobberTileId==null) s.RobberTileId=seafaring.InitialRobberTileId;
            var strength=s.Players.ToDictionary(p=>p.Id,p=>s.Knights.Where(k=>k.PlayerId==p.Id&&k.Active).Sum(k=>k.Level));
            if(strength.Values.Sum()>=s.Cities.Length)
            {
                int best=strength.Values.Max(); var leaders=TurnOrder(s).Where(p=>strength[p.Id]==best).ToArray();
                if(leaders.Length==1) leaders[0].DefenderPoints++;
                else foreach(var p in leaders) QueueDecision(s,new PendingDecision {Kind="DefenderProgress",PlayerId=p.Id});
            }
            else
            {
                var vulnerable=TurnOrder(s).Where(p=>s.Cities.Any(c=>c.PlayerId==p.Id&&!s.Metropolises.Any(m=>m.LocationId==c.LocationId))).ToArray();
                if(vulnerable.Length>0)
                {
                    int weakest=vulnerable.Min(p=>strength[p.Id]);
                    foreach(var p in vulnerable.Where(p=>strength[p.Id]==weakest))
                        QueueDecision(s,new PendingDecision {Kind="PillageCity",PlayerId=p.Id,Options=s.Cities.Where(c=>c.PlayerId==p.Id&&!s.Metropolises.Any(m=>m.LocationId==c.LocationId)).Select(c=>c.LocationId).ToArray()});
                }
            }
            QueueDecision(s,new PendingDecision {Kind="BarbarianReturnHome",PlayerId=s.ActivePlayerId});
        }
        private void ResolveChoice(GameState s,PlayerState p,Command c)
        {
            Require(s.Phase==GamePhase.PendingChoice&&s.PendingDecision!=null&&s.PendingDecision.PlayerId==p.Id,"NotDecisionOwner","请由待决选择所属玩家操作。");
            var d=s.PendingDecision;
            Require(c.Kind!=CommandKind.ChooseGoldResources||d.Kind=="Gold","WrongDecision","当前不是金矿选择。");
            switch(d.Kind)
            {
                case "Gold":
                    Require(ValidBag(c.Resources)&&c.Resources.Total==Math.Min(d.Amount,s.Bank.Total)&&Covers(s.Bank,c.Resources)&&(c.Commodities==null||ValidBag(c.Commodities)&&c.Commodities.Total==0),"InvalidGoldChoice","金矿只能选择银行有库存的指定数量资源，不能选择商品。");
                    TransferBag(s.Bank,p.Resources,c.Resources); break;
                case "Aqueduct": Require(Enum.IsDefined(typeof(Resource),c.Resource),"InvalidResource","请选择资源。"); Transfer(s.Bank,p.Resources,c.Resource,1); break;
                case "PillageCity":
                    Require(d.Options.Contains(c.TargetId),"InvalidCity","请选择可被劫掠的城市。");
                    s.Cities=s.Cities.Where(x=>x.LocationId!=c.TargetId).ToArray(); s.Walls=s.Walls.Where(x=>x.LocationId!=c.TargetId).ToArray(); s.Settlements=Append(s.Settlements,Placement(c.TargetId,p.Id));
                    if(p.Pieces.Settlements>0) {p.Pieces.Settlements--;p.Pieces.Cities++;} else s.DowngradedCities=Append(s.DowngradedCities,Placement(c.TargetId,p.Id));
                    break;
                case "Metropolis":
                    Require(d.Options.Contains(c.TargetId)&&s.Cities.Any(x=>x.PlayerId==p.Id&&x.LocationId==c.TargetId)&&!s.Metropolises.Any(m=>m.LocationId==c.TargetId),"InvalidCity","请选择没有大都会的己方城市。");
                    s.Metropolises=s.Metropolises.Where(m=>m.Track!=d.Track).ToArray(); s.Metropolises=Append(s.Metropolises,new Metropolis {Track=d.Track,PlayerId=p.Id,LocationId=c.TargetId}); break;
                case "DefenderProgress": Require(Enum.IsDefined(typeof(ImprovementTrack),c.Track),"InvalidTrack","请选择进步牌牌堆。"); DrawProgressCard(s,p,c.Track); break;
                case "DisplacedKnight":
                    Require(d.Options.Contains(c.TargetId)&&!Occupied(s,c.TargetId),"InvalidKnightDestination","请选择合法的骑士撤退位置。");
                    d.DisplacedKnight.LocationId=c.TargetId; s.Knights=Append(s.Knights,d.DisplacedKnight); break;
                default: throw new RuleException("WrongDecision","当前选择需要对应进步牌操作。");
            }
            CheckVictory(s);if(s.Phase!=GamePhase.Finished) ContinueDecisions(s);
        }
        private void BuildCity(GameState s,PlayerState p,string target,int wheat=2,int ore=3)
        {
            bool replacement=s.DowngradedCities.Any(x=>x.PlayerId==p.Id&&x.LocationId==target);
            Require(replacement||!s.DowngradedCities.Any(x=>x.PlayerId==p.Id),"RestorePillagedCityFirst","必须先恢复倒置的城市。");
            Require(p.Pieces.Cities>0||replacement,"NoCities","城市库存不足。");
            Require(s.Settlements.Any(x=>x.PlayerId==p.Id&&x.LocationId==target),"NotOwnSettlement","只能升级自己的定居点。");
            Pay(s,p,new ResourceBag {Wheat=wheat,Ore=ore}); s.Settlements=s.Settlements.Where(x=>x.LocationId!=target).ToArray(); s.Cities=Append(s.Cities,Placement(target,p.Id));
            if(replacement) s.DowngradedCities=s.DowngradedCities.Where(x=>x.LocationId!=target).ToArray(); else {p.Pieces.Cities--;p.Pieces.Settlements++;}
        }
        private void BuildWall(GameState s,PlayerState p,string target,bool free=false)
        {
            Require(s.Walls.Count(w=>w.PlayerId==p.Id)<3,"NoWalls","城墙库存不足。");
            Require(s.Cities.Any(x=>x.PlayerId==p.Id&&x.LocationId==target)&&!s.Walls.Any(w=>w.LocationId==target),"InvalidWallCity","请选择没有城墙的己方城市。");
            if(!free) Pay(s,p,new ResourceBag {Brick=2}); s.Walls=Append(s.Walls,Placement(target,p.Id));
        }
        private void BuildImprovement(GameState s,PlayerState p,ImprovementTrack track,int discount=0)
        {
            Require(Enum.IsDefined(typeof(ImprovementTrack),track),"InvalidTrack","请选择有效改良方向。");
            Require(s.Cities.Any(c=>c.PlayerId==p.Id),"NoCity","至少需要一座城市才能改良。"); int level=p.Improvements[(int)track]+1;
            Require(level<=5,"MaximumImprovement","已达最高改良等级。");
            var holder=s.Metropolises.FirstOrDefault(m=>m.Track==track);
            bool acquire=level>=4&&(holder==null||holder.PlayerId!=p.Id&&s.Players.Single(x=>x.Id==holder.PlayerId).Improvements[(int)track]<level);
            var cities=s.Cities.Where(c=>c.PlayerId==p.Id&&!s.Metropolises.Any(m=>m.LocationId==c.LocationId)).Select(c=>c.LocationId).ToArray();
            Require(!acquire||cities.Length>0,"NoMetropolisCity","取得大都会必须有一座可用城市。");
            Transfer(p.Commodities,s.CommodityBank,(Commodity)(int)track,Math.Max(0,level-discount)); p.Improvements[(int)track]=level;
            if(acquire) {QueueDecision(s,new PendingDecision {Kind="Metropolis",PlayerId=p.Id,Track=track,Options=cities}); ContinueDecisions(s);}
        }
        private bool Occupied(GameState s,string vertex) => Buildings(s).Any(b=>b.LocationId==vertex)||s.Knights.Any(k=>k.LocationId==vertex);
        private bool Blocked(GameState s,string player,string vertex) => Buildings(s).Any(b=>b.PlayerId!=player&&b.LocationId==vertex)||s.Knights.Any(k=>k.PlayerId!=player&&k.LocationId==vertex);
        private void RecruitKnight(GameState s,PlayerState p,string target)
        {
            Require(scenario.Topology.Vertices.Any(v=>v.Id==target)&&!Occupied(s,target),"OccupiedVertex","骑士需要空顶点。");
            Require(OwnRouteEdges(s,p.Id).Any(e=>e.Vertices.Contains(target)),"DisconnectedKnight","骑士须连接自己的道路。");
            Require(s.Knights.Count(k=>k.PlayerId==p.Id&&k.Level==1)<2,"NoBasicKnights","基础骑士库存不足。");
            Pay(s,p,new ResourceBag {Wool=1,Ore=1}); s.Knights=Append(s.Knights,new Knight {PlayerId=p.Id,LocationId=target,Level=1});
        }
        private Knight OwnKnight(GameState s,PlayerState p,string target) {var knight=s.Knights.FirstOrDefault(k=>k.PlayerId==p.Id&&k.LocationId==target);Require(knight!=null,"NotOwnKnight","请选择己方骑士。");return knight;}
        private void PromoteKnight(GameState s,PlayerState p,string target)
        {
            var knight=OwnKnight(s,p,target); Require(knight.Level<3&&knight.PromotedTurn!=s.Turn,"CannotPromote","每位骑士每回合只能晋升一次。");
            Require(knight.Level<2||p.Improvements[(int)ImprovementTrack.Politics]>=3,"FortressRequired","晋升强大骑士需要三级政治改良。");
            Require(s.Knights.Count(k=>k.PlayerId==p.Id&&k.Level==knight.Level+1)<2,"NoKnightSupply","晋升等级的骑士库存不足。");
            Pay(s,p,new ResourceBag {Wool=1,Ore=1}); knight.Level++;knight.PromotedTurn=s.Turn;
        }
        private void ActivateKnight(GameState s,PlayerState p,string target) {var knight=OwnKnight(s,p,target);Require(!knight.Active,"AlreadyActive","骑士已经激活。");Pay(s,p,new ResourceBag {Wheat=1});knight.Active=true;knight.ActivatedTurn=s.Turn;}
        private HashSet<string> ReachableKnightVertices(GameState s,string player,string source)
        {
            var seen=new HashSet<string> {source};var queue=new Queue<string>();queue.Enqueue(source);
            while(queue.Count>0) {var v=queue.Dequeue();foreach(var edge in OwnRouteEdges(s,player).Where(e=>e.Vertices.Contains(v))) {var next=edge.Vertices.Single(x=>x!=v);if(!seen.Add(next))continue;if(!Blocked(s,player,next))queue.Enqueue(next);}}
            seen.Remove(source);return seen;
        }
        private void MoveKnight(GameState s,PlayerState p,Command c)
        {
            var knight=OwnKnight(s,p,c.SourceId); Require(knight.Active&&knight.ActivatedTurn!=s.Turn,"KnightCannotAct","只有此前激活的骑士可以行动。");
            Require(ReachableKnightVertices(s,p.Id,c.SourceId).Contains(c.TargetId),"DisconnectedKnight","骑士必须沿连续的己方道路移动。");
            if(c.Kind==CommandKind.MoveKnight) Require(!Occupied(s,c.TargetId),"OccupiedVertex","骑士需要空顶点。");
            else {var defender=s.Knights.FirstOrDefault(k=>k.LocationId==c.TargetId&&k.PlayerId!=p.Id);Require(defender!=null&&defender.Level<knight.Level,"CannotDisplace","只能驱逐较弱的对方骑士。");knight.LocationId=c.TargetId;DisplaceForProgress(s,defender);}
            knight.LocationId=c.TargetId;knight.Active=false;knight.ActedTurn=s.Turn;
            if(s.DecisionQueue.Length>0) ContinueDecisions(s);
        }
        private void DisplaceForProgress(GameState s,Knight knight)
        {
            var options=ReachableKnightVertices(s,knight.PlayerId,knight.LocationId).Where(v=>!Occupied(s,v)).OrderBy(v=>v).ToArray();
            s.Knights=s.Knights.Where(k=>k!=knight).ToArray();
            if(options.Length>0) QueueDecision(s,new PendingDecision {Kind="DisplacedKnight",PlayerId=knight.PlayerId,AnchorVertexId=knight.LocationId,DisplacedKnight=knight,Options=options});
        }
        private void ChaseRobber(GameState s,PlayerState p,string target)
        {
            var knight=OwnKnight(s,p,target);Require(s.FirstBarbarianAttack&&knight.Active&&knight.ActivatedTurn!=s.Turn,"KnightCannotAct","只有此前激活的骑士可以驱逐已进场的强盗。");
            Require(scenario.Topology.Tiles.Any(t=>t.Id==s.RobberTileId&&t.Vertices.Contains(target)),"KnightNotAtRobber","骑士必须位于强盗所在土地旁。");
            knight.Active=false;knight.ActedTurn=s.Turn;s.PendingDecision=new PendingDecision {Kind="Robber",PlayerId=p.Id,Token="robber-only",ReturnPhase=GamePhase.Action};s.Phase=GamePhase.RobberMove;
        }
        private void StealHandCard(GameState s,PlayerState p,PlayerState victim)
        {
            if(HandCount(victim)==0)return;int index=Draw(s.Random,HandCount(victim));
            foreach(var r in Resources) {if(index<victim.Resources[r]) {Transfer(victim.Resources,p.Resources,r,1);return;}index-=victim.Resources[r];}
            foreach(var r in Commodities) {if(index<victim.Commodities[r]) {Transfer(victim.Commodities,p.Commodities,r,1);return;}index-=victim.Commodities[r];}
        }
        private void BankTrade(GameState s,PlayerState p,Command c)
        {
            Require((c.GiveIsCommodity?Enum.IsDefined(typeof(Commodity),c.GiveCommodity):Enum.IsDefined(typeof(Resource),c.GiveResource))&&(c.ReceiveIsCommodity?Enum.IsDefined(typeof(Commodity),c.ReceiveCommodity):Enum.IsDefined(typeof(Resource),c.ReceiveResource)),"InvalidTrade","请选择有效资源或商品。");
            Require(c.GiveIsCommodity!=c.ReceiveIsCommodity||(c.GiveIsCommodity?c.GiveCommodity!=c.ReceiveCommodity:c.GiveResource!=c.ReceiveResource),"InvalidTrade","请选择不同的交换牌。");
            int rate=c.GiveIsCommodity?CommodityTradeRate(s,p.Id,c.GiveCommodity):TradeRate(s,p.Id,c.GiveResource);
            bool generic=ports.Any(port=>port.Resource==null&&Buildings(s).Any(b=>b.PlayerId==p.Id&&port.Vertices.Contains(b.LocationId)));
            Require(c.ReceiveAmount>0&&c.ReceiveAmount<=19&&c.GiveAmount>0&&c.GiveAmount<=19&&(c.GiveAmount==4*c.ReceiveAmount||c.GiveAmount==rate*c.ReceiveAmount||generic&&c.GiveAmount==3*c.ReceiveAmount),"InvalidRatio","交换比例无效。");
            if(c.GiveIsCommodity)Transfer(p.Commodities,s.CommodityBank,c.GiveCommodity,c.GiveAmount);else Transfer(p.Resources,s.Bank,c.GiveResource,c.GiveAmount);
            if(c.ReceiveIsCommodity)Transfer(s.CommodityBank,p.Commodities,c.ReceiveCommodity,c.ReceiveAmount);else Transfer(s.Bank,p.Resources,c.ReceiveResource,c.ReceiveAmount);
        }
        public int GetCommodityTradeRate(string playerId,Commodity commodity) {lock(gate) {if(!state.Players.Any(p=>p.Id==playerId)||!Enum.IsDefined(typeof(Commodity),commodity))throw new ArgumentException("Invalid player/commodity.");return CommodityTradeRate(state,playerId,commodity);}}
        private int CommodityTradeRate(GameState s,string player,Commodity commodity)
        {
            var p=s.Players.Single(x=>x.Id==player);int rate=p.Improvements[(int)ImprovementTrack.Trade]>=3?2:ports.Any(port=>port.Resource==null&&Buildings(s).Any(b=>b.PlayerId==player&&port.Vertices.Contains(b.LocationId)))?3:4;
            return Math.Min(rate,ProgressTradeRate(s,p,null,commodity));
        }
        private IEnumerable<Command> LegalExpansionActions(string player)
        {
            // A permission-safe convenience list; Execute remains the sole authority.
            var candidates=new List<Command>();
            if(state.Phase==GamePhase.PendingChoice&&state.PendingDecision.PlayerId==player)
            {
                var d=state.PendingDecision;
                foreach(var option in d.Options) candidates.Add(new Command {PlayerId=player,Kind=CommandKind.ResolveChoice,TargetId=option});
                if(d.Kind=="Aqueduct")foreach(var r in Resources)candidates.Add(new Command {PlayerId=player,Kind=CommandKind.ResolveChoice,Resource=r});
                if(d.Kind=="DefenderProgress")foreach(ImprovementTrack t in Enum.GetValues(typeof(ImprovementTrack)))candidates.Add(new Command {PlayerId=player,Kind=CommandKind.ResolveChoice,Track=t});
            }
            if(state.Phase==GamePhase.Action&&state.ActivePlayerId==player)
            {
                foreach(var v in scenario.Topology.Vertices)candidates.Add(new Command {PlayerId=player,Kind=CommandKind.RecruitKnight,TargetId=v.Id});
                foreach(var c in state.Cities.Where(x=>x.PlayerId==player))candidates.Add(new Command {PlayerId=player,Kind=CommandKind.BuildWall,TargetId=c.LocationId});
                foreach(ImprovementTrack t in Enum.GetValues(typeof(ImprovementTrack)))candidates.Add(new Command {PlayerId=player,Kind=CommandKind.ImproveCity,Track=t});
                foreach(var k in state.Knights.Where(x=>x.PlayerId==player))
                {
                    foreach(var kind in new[]{CommandKind.PromoteKnight,CommandKind.ActivateKnight,CommandKind.ChaseRobber})candidates.Add(new Command {PlayerId=player,Kind=kind,TargetId=k.LocationId});
                    if(k.Active&&k.ActivatedTurn!=state.Turn)foreach(var v in ReachableKnightVertices(state,player,k.LocationId)) candidates.Add(new Command {PlayerId=player,Kind=state.Knights.Any(x=>x.PlayerId!=player&&x.LocationId==v)?CommandKind.DisplaceKnight:CommandKind.MoveKnight,SourceId=k.LocationId,TargetId=v});
                }
            }
            candidates.AddRange(ProgressCandidates(state,state.Players.Single(p=>p.Id==player)));
            candidates.AddRange(SeafaringCandidates(state,state.Players.Single(p=>p.Id==player)));
            return candidates.Where(IsLegal);
        }
    }
}
