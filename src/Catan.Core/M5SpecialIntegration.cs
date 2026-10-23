using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Catan.Core.M5
{
    public sealed partial class CombinedGameSession
    {
        private void InitializeSpecial(GameState s)
        {
            if (seafaring.Id == "heading-for-new-shores" || seafaring.Id == "through-the-desert") return;
            s.Special = new ScenarioState {
                Ports=Json.Copy(seafaring.Ports), HiddenResources=seafaring.HiddenResources.ToArray(), HiddenNumbers=seafaring.HiddenNumbers.ToArray(),
                BonusEdgeIds=seafaring.BonusEdgeIds.ToArray(), GiftCardEdgeIds=seafaring.GiftCardEdgeIds.ToArray(), GiftPorts=Json.Copy(seafaring.GiftPorts),
                Villages=seafaring.Villages.Select(v=>new M3.VillageState {VertexId=v.VertexId,Number=v.Number}).ToArray(),
                Fortresses=seafaring.Fortresses.Select(f=>new M3.FortressState {PlayerId=f.PlayerId,VertexId=f.VertexId,BeachheadVertexId=f.BeachheadVertexId,StartingVertexId=f.StartingVertexId,InvasionStartingVertexId=f.StartingVertexId}).ToArray()
            };
            Shuffle(s.Special.HiddenResources,s.Random); Shuffle(s.Special.HiddenNumbers,s.Random);
            s.Special.ClothSupply=s.Special.Villages.Length==0?0:50-5*s.Special.Villages.Length;
            foreach(var f in seafaring.Fortresses) {
                var p=s.Players.Single(x=>x.Id==f.PlayerId); PlaceSettlement(s,p,f.StartingVertexId); p.Pieces.Settlements--;
                PlaceShip(s,p,f.StartingShipEdgeId); s.Ships.Last().IsInvasionRoute=true; RecordRegion(s,p,f.StartingVertexId,true);
            }
            if(seafaring.Id=="new-world") {
                s.Special.UnplacedPorts=s.Special.Ports.Select(p=>new M3.Port {Id=p.Id,Resource=p.Resource,Vertices=Array.Empty<string>()}).ToArray();
                Shuffle(s.Special.UnplacedPorts,s.Random);s.Special.Ports=Array.Empty<M3.Port>();s.Phase=GamePhase.SetupPort;
                s.PendingDecision=new PendingDecision {Kind="SetupPort",PlayerId=s.ActivePlayerId};
            }
        }
        private Port[] CurrentPorts(GameState s) => s.Special==null?ports:s.Special.Ports.Select(p=>new Port {Id=p.Id,Vertices=p.Vertices.ToArray(),Resource=p.Resource}).ToArray();
        private void ApplyBaseRules(GameState s,Command c)
        {
            if(s.Special==null){ApplyCore(s,c);return;}
            var before=s.Roads.Select(r=>r.LocationId).Concat(s.Ships.Select(r=>r.LocationId)).ToArray();
            ApplyCore(s,c);
            if(s.Special==null||s.Phase==GamePhase.Finished)return;
            if(seafaring.Id=="pirate-islands"&&s.Phase==GamePhase.RobberMove){s.PendingDecision.Kind="Steal";s.PendingDecision.Token="any";s.PendingDecision.EligibleVictimIds=s.Players.Where(p=>p.Id!=s.ActivePlayerId).Select(p=>p.Id).ToArray();s.Phase=GamePhase.RobberSteal;}
            int queued=s.DecisionQueue.Length;
            foreach(var edge in s.Roads.Select(r=>r.LocationId).Concat(s.Ships.Select(r=>r.LocationId)).Except(before).ToArray()) {
                Discover(s,c.PlayerId,edge);
                if(s.Ships.Any(r=>r.PlayerId==c.PlayerId&&r.LocationId==edge))CollectSpecialGifts(s,c.PlayerId,edge);
            }
            ConnectVillages(s);
            if(s.DecisionQueue.Length>0&&s.Phase!=GamePhase.PendingChoice&&s.PendingDecision!=null){s.Special.ResumeDecision=s.PendingDecision;s.Special.ResumePhase=s.Phase;s.PendingDecision=null;}
            if(s.PendingDecision==null&&s.DecisionQueue.Length>0){s.DecisionReturnPhase=s.Phase;ContinueDecisions(s);}
            if((s.Phase==GamePhase.Action||s.Phase==GamePhase.ProductionAwaitRoll)&&LegalPortEdges(s,s.ActivePlayerId).Length>0) {
                s.Special.PortReturnDecision=s.PendingDecision;
                s.PendingDecision=new PendingDecision {Kind="Port",PlayerId=s.ActivePlayerId,ReturnPhase=s.Phase};s.Phase=GamePhase.PortPlacement;
            }
        }
        private void AddGold(GameState s,string player,int amount)
        {
            if(amount>0&&!B2DrawBlocked(s,player))QueueDecision(s,new PendingDecision {Kind="Gold",PlayerId=player,Amount=amount,ReturnPhase=s.Phase});
        }
        private void CollectSpecialGifts(GameState s,string player,string edge)
        {
            var p=s.Players.Single(x=>x.Id==player);
            if(s.Special.BonusEdgeIds.Contains(edge)){p.BonusVictoryPoints++;s.Special.BonusEdgeIds=s.Special.BonusEdgeIds.Where(x=>x!=edge).ToArray();}
            if(s.Special.GiftCardEdgeIds.Contains(edge)) {
                s.Special.GiftCardEdgeIds=s.Special.GiftCardEdgeIds.Where(x=>x!=edge).ToArray();
                QueueDecision(s,new PendingDecision {Kind="TribeProgress",PlayerId=player,ReturnPhase=s.Phase});
            }
            var port=s.Special.GiftPorts.FirstOrDefault(x=>x.EdgeId==edge);
            if(port!=null){s.Special.HeldPorts=Append(s.Special.HeldPorts,new HeldPort {PlayerId=player,Port=Json.Copy(port)});s.Special.GiftPorts=s.Special.GiftPorts.Where(x=>x.Id!=port.Id).ToArray();}
        }
        private bool ApplySpecial(GameState s,PlayerState p,Command c)
        {
            if(s.Special==null)return false;
            switch(c.Kind) {
                case CommandKind.SetupPort:
                    Require(s.Phase==GamePhase.SetupPort&&s.Special.UnplacedPorts.Length>0,"WrongPhase","当前无需放置初始港口。");
                    Require(LegalPortEdges(s,p.Id).Contains(c.TargetId),"InvalidPortEdge","请选择合法海岸港位。");
                    var port=Json.Copy(s.Special.UnplacedPorts[0]);port.EdgeId=c.TargetId;port.Vertices=EdgeAt(c.TargetId).Vertices.ToArray();
                    s.Special.Ports=Append(s.Special.Ports,port);s.Special.UnplacedPorts=s.Special.UnplacedPorts.Skip(1).ToArray();s.Special.SetupPortIndex++;
                    s.ActivePlayerId=s.Players[(s.StartingPlayerIndex+(s.Special.UnplacedPorts.Length==0?0:s.Special.SetupPortIndex))%s.Players.Length].Id;
                    if(s.Special.UnplacedPorts.Length==0){s.Phase=GamePhase.SetupSettlement;s.PendingDecision=null;}else s.PendingDecision.PlayerId=s.ActivePlayerId;return true;
                case CommandKind.PlacePort:
                    Require(s.Phase==GamePhase.PortPlacement,"WrongPhase","当前没有待放置港口。");
                    var held=s.Special.HeldPorts.FirstOrDefault(h=>h.PlayerId==p.Id&&h.Port.Id==c.SourceId);
                    Require(held!=null&&LegalPortEdges(s,p.Id).Contains(c.TargetId),"InvalidPortEdge","港口须邻接己方建筑并与其他港口间隔。");
                    var placed=Json.Copy(held.Port);placed.EdgeId=c.TargetId;placed.Vertices=EdgeAt(c.TargetId).Vertices.ToArray();s.Special.Ports=Append(s.Special.Ports,placed);
                    s.Special.HeldPorts=s.Special.HeldPorts.Where(h=>h!=held).ToArray();s.Phase=s.PendingDecision.ReturnPhase;s.PendingDecision=s.Special.PortReturnDecision;s.Special.PortReturnDecision=null;return true;
                case CommandKind.StealCloth:
                    Require(s.Phase==GamePhase.RobberSteal&&s.PendingDecision.Token=="pirate"&&seafaring.Id=="cloth-for-catan"&&s.PendingDecision.EligibleVictimIds.Contains(c.OtherPlayerId),"InvalidVictim","请选择海盗旁有布匹的船主。");
                    var victim=s.Players.Single(x=>x.Id==c.OtherPlayerId);Require(victim.Cloth>0,"NoCloth","对方没有布匹。");victim.Cloth--;p.Cloth++;FinishDecision(s);return true;
                case CommandKind.ClaimWonder:
                    ActionPhase(s);var wonder=seafaring.Wonders.FirstOrDefault(w=>w.Id==c.TargetId);
                    Require(wonder!=null&&p.WonderId==null&&!s.Players.Any(x=>x.WonderId==c.TargetId),"UnavailableWonder","奇观已被认领或不可用。");
                    Require(WonderRequirement(s,p,wonder),"WonderRequirement","尚未满足认领条件。");p.WonderId=wonder.Id;return true;
                case CommandKind.BuildWonder:
                    ActionPhase(s);Require(p.WonderId!=null&&p.WonderId==c.TargetId&&p.WonderLevel<4,"InvalidWonder","请选择己方未完成奇观。");
                    Pay(s,p,seafaring.Wonders.Single(w=>w.Id==p.WonderId).Cost);p.WonderLevel++;return true;
                case CommandKind.ChooseInvasionRoute:
                    ActionPhase(s);Require(LegalInvasionRoots(s,p.Id).Contains(c.TargetId),"InvalidInvasionRoot","只能在离开主岛前改选西行起点。");SelectInvasionRoot(s,p.Id,c.TargetId);return true;
                case CommandKind.ConvertWarship:
                    ActionPhase(s);Require(seafaring.Id=="pirate-islands","WrongScenario","只有海盗群岛可升级战舰。");
                    var knight=OwnKnight(s,p,c.SourceId);Require(knight.Active&&knight.ActivatedTurn!=s.Turn&&knight.ActedTurn!=s.Turn,"KnightCannotAct","骑士须已激活且本回合可行动。");
                    var ship=InvasionShips(s,p.Id).FirstOrDefault(x=>!x.IsWarship&&x.LocationId==c.TargetId);
                    Require(ship!=null,"NoNormalShip","请选择进攻航线上的普通船。");knight.Active=false;knight.ActedTurn=s.Turn;ship.IsWarship=true;return true;
                case CommandKind.AttackFortress:
                    ActionPhase(s);Require(seafaring.Id=="pirate-islands"&&CanAttackFortress(s,p.Id),"FortressNotReached","尚未连接己方要塞。");EndSpecialTurn(s,p);return true;
                default:return false;
            }
        }
        private void EndSpecialTurn(GameState s,PlayerState p)
        {
            Require((B2Unlimited(s,p.Id)||B2Protected(s,p.Id)||p.ProgressCards.Length<=4),"ProgressHandLimit","结束回合前须将进步卡减至四张。");
            if(seafaring.Id=="pirate-islands"&&CanAttackFortress(s,p.Id)){AttackFortress(s,p);CheckVictory(s);if(s.Phase==GamePhase.Finished)return;}
            if(s.Special!=null&&s.Special.Villages.Count(v=>v.Cloth==0)>=5) {
                int best=s.Players.Max(x=>VictoryPoints(s,x.Id,true));var leaders=s.Players.Where(x=>VictoryPoints(s,x.Id,true)==best).ToArray();int cloth=leaders.Max(x=>x.Cloth);
                FinishSpecialGame(s,leaders.Where(x=>x.Cloth==cloth).Select(x=>x.Id).ToArray());return;
            }
            ClearProgressTurn(s);s.ShipMovedThisTurn=false;s.ActivePlayerId=s.Players[(Array.FindIndex(s.Players,x=>x.Id==p.Id)+1)%s.Players.Length].Id;s.Turn++;s.Phase=GamePhase.ProductionAwaitRoll;
        }
        private bool HasScenarioVictory(GameState s,string player)
        {
            var p=s.Players.Single(x=>x.Id==player);int score=VictoryPoints(s,player,true);
            if(seafaring.Id=="wonders-of-catan")return p.WonderLevel==4||score>=s.TargetVictoryPoints.Value&&p.WonderLevel>s.Players.Where(x=>x.Id!=player).Max(x=>x.WonderLevel);
            return score>=s.TargetVictoryPoints.Value&&(seafaring.Id!="pirate-islands"||s.Special.Fortresses.Single(f=>f.PlayerId==player).Strength==0);
        }
        private static void FinishSpecialGame(GameState s,string[] winners)
        {
            if(s.Special!=null)s.Special.WinnerPlayerIds=winners;s.WinnerPlayerId=winners[0];s.Phase=GamePhase.Finished;s.PendingDecision=null;s.DecisionQueue=Array.Empty<PendingDecision>();s.ProductionPending=false;s.Discards=Array.Empty<DiscardRequirement>();s.TradeOffer=null;
        }
        private void SpecialProduction(GameState s)
        {
            if(s.Special==null)return;
            int total=s.LastDice1+s.LastDice2;
            if(seafaring.Id=="pirate-islands"&&s.FirstBarbarianAttack&&s.PirateTileId!=null) {
                int power=Math.Min(s.LastDice1,s.LastDice2);s.Special.LastPirateStrength=power;
                s.PirateTileId=seafaring.PiratePath[(Array.IndexOf(seafaring.PiratePath,s.PirateTileId)+power)%seafaring.PiratePath.Length];
                var tile=s.Tiles.Single(t=>t.Id==s.PirateTileId);
                foreach(var p in TurnOrder(s).Where(p=>Buildings(s).Any(b=>b.PlayerId==p.Id&&tile.Vertices.Contains(b.LocationId)))) {
                    int fleet=s.Ships.Count(x=>x.PlayerId==p.Id&&x.IsWarship);
                    if(fleet>power)AddGold(s,p.Id,1);
                    else if(fleet<power&&!B2Protected(s,p.Id))for(int i=0;i<1+s.Cities.Count(c=>c.PlayerId==p.Id)&&HandCount(p)>0;i++) {
                        int index=Draw(s.Random,HandCount(p));bool removed=false;
                        foreach(var r in Resources){if(index<p.Resources[r]){Transfer(p.Resources,s.Bank,r,1);removed=true;break;}index-=p.Resources[r];}
                        if(!removed)foreach(var r in Commodities){if(index<p.Commodities[r]){Transfer(p.Commodities,s.CommodityBank,r,1);break;}index-=p.Commodities[r];}
                    }
                }
            }
            if(total==7)return;
            foreach(var v in s.Special.Villages.Where(v=>v.Number==total&&v.Cloth>0))foreach(var p in TurnOrder(s).Where(p=>v.TradingPlayerIds.Contains(p.Id))) {
                if(v.Cloth>0){v.Cloth--;p.Cloth++;}else if(s.Special.ClothSupply>0){s.Special.ClothSupply--;p.Cloth++;}
            }
        }
        private IEnumerable<Command> SpecialCandidates(GameState s,PlayerState p)
        {
            if(s.Special==null)yield break;
            if(s.PendingDecision?.PlayerId==p.Id&&s.PendingDecision.Kind=="TribeProgress")foreach(ImprovementTrack t in Enum.GetValues(typeof(ImprovementTrack)))yield return new Command {PlayerId=p.Id,Kind=CommandKind.ResolveChoice,Track=t};
            if(s.ActivePlayerId!=p.Id)yield break;
            if(s.Phase==GamePhase.SetupPort)foreach(var e in LegalPortEdges(s,p.Id))yield return new Command {PlayerId=p.Id,Kind=CommandKind.SetupPort,TargetId=e};
            if(s.Phase==GamePhase.PortPlacement)foreach(var h in s.Special.HeldPorts.Where(h=>h.PlayerId==p.Id))foreach(var e in LegalPortEdges(s,p.Id))yield return new Command {PlayerId=p.Id,Kind=CommandKind.PlacePort,SourceId=h.Port.Id,TargetId=e};
            if(s.Phase==GamePhase.RobberSteal)foreach(var id in s.PendingDecision.EligibleVictimIds)yield return new Command {PlayerId=p.Id,Kind=CommandKind.StealCloth,OtherPlayerId=id};
            if(s.Phase!=GamePhase.Action)yield break;
            foreach(var w in seafaring.Wonders)foreach(var k in new[]{CommandKind.ClaimWonder,CommandKind.BuildWonder})yield return new Command {PlayerId=p.Id,Kind=k,TargetId=w.Id};
            if(seafaring.Id=="pirate-islands") {
                foreach(var v in LegalInvasionRoots(s,p.Id))yield return new Command {PlayerId=p.Id,Kind=CommandKind.ChooseInvasionRoute,TargetId=v};
                foreach(var k in s.Knights.Where(k=>k.PlayerId==p.Id&&k.Active))foreach(var ship in InvasionShips(s,p.Id).Where(x=>!x.IsWarship))yield return new Command {PlayerId=p.Id,Kind=CommandKind.ConvertWarship,SourceId=k.LocationId,TargetId=ship.LocationId};
                yield return new Command {PlayerId=p.Id,Kind=CommandKind.AttackFortress};
            }
        }
    }
}
