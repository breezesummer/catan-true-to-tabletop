using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Catan.Core.M5
{
    public sealed partial class CombinedGameSession
    {
        private static bool IsLand(Tile tile) => tile.Resource != "sea" && tile.Resource != "fog";
        private IEnumerable<Tile> EdgeTiles(GameState s, string id)
        {
            var edge = EdgeAt(id);
            return s.Tiles.Where(t => edge.Vertices.All(t.Vertices.Contains));
        }
        private IEnumerable<Edge> OwnRouteEdges(GameState s,string player) => s.Roads.Where(r=>r.PlayerId==player).Select(r=>EdgeAt(r.LocationId)).Concat(s.Ships.Where(r=>r.PlayerId==player).Select(r=>EdgeAt(r.LocationId)));
        private bool SeaEdge(GameState s,string edge) { var tiles=EdgeTiles(s,edge).ToArray();return tiles.Length==1||tiles.Any(t=>!IsLand(t)); }
        private bool PirateBlocks(GameState s,string edge) => s.PirateTileId!=null&&EdgeTiles(s,edge).Any(t=>t.Id==s.PirateTileId);

        private bool ApplySeafaring(GameState s,PlayerState p,Command c)
        {
            switch(c.Kind)
            {
                case CommandKind.SetupShip:
                    Require(s.Phase==GamePhase.SetupRoad&&s.PendingDecision!=null,"WrongPhase","先放置开局建筑。");
                    ValidateShip(s,p,c.TargetId,s.PendingDecision.AnchorVertexId);PlaceShip(s,p,c.TargetId);AdvanceSetup(s);return true;
                case CommandKind.BuildShip:
                    ActionPhase(s);ValidateShip(s,p,c.TargetId,null);Pay(s,p,new ResourceBag {Wood=1,Wool=1});PlaceShip(s,p,c.TargetId);return true;
                case CommandKind.MoveShip:
                    ActionPhase(s);ValidateMoveSource(s,p,c.SourceId);
                    Require(c.SourceId!=c.TargetId,"SameEdge","船须移动到另一条边。");
                    s.Ships=s.Ships.Where(x=>x.LocationId!=c.SourceId).ToArray();p.ShipsRemaining++;
                    ValidateShip(s,p,c.TargetId,null);PlaceShip(s,p,c.TargetId);s.ShipMovedThisTurn=true;return true;
                case CommandKind.MovePirate:
                    Require(s.FirstBarbarianAttack&&s.Phase==GamePhase.RobberMove&&s.PendingDecision!=null&&s.PendingDecision.Token!="robber-only","WrongPhase","当前不能移动海盗。");
                    Require(c.TargetId!=s.PirateTileId&&(c.TargetId=="frame"||s.Tiles.Any(t=>t.Id==c.TargetId&&t.Resource=="sea")),"InvalidPirateTile","海盗须移动至另一海格或边框。");
                    s.PirateTileId=c.TargetId;s.PendingDecision.Token="pirate";s.PendingDecision.EligibleVictimIds=PirateVictims(s,p.Id,c.TargetId);
                    if(s.PendingDecision.EligibleVictimIds.Length==0)FinishDecision(s);else {s.PendingDecision.Kind="Steal";s.Phase=GamePhase.RobberSteal;}return true;
                case CommandKind.ChasePirate:
                    ActionPhase(s);var knight=OwnKnight(s,p,c.TargetId);
                    Require(s.FirstBarbarianAttack&&knight.Active&&knight.ActivatedTurn!=s.Turn,"KnightCannotAct","只有此前激活的骑士可以驱逐已进场的海盗。");
                    Require(s.Tiles.Any(t=>t.Id==s.PirateTileId&&t.Vertices.Contains(c.TargetId)),"KnightNotAtPirate","骑士须在海盗所在海格旁。");
                    knight.Active=false;knight.ActedTurn=s.Turn;s.PendingDecision=new PendingDecision {Kind="Pirate",PlayerId=p.Id,Token="pirate-only",ReturnPhase=GamePhase.Action};s.Phase=GamePhase.RobberMove;return true;
                default:return false;
            }
        }
        private string[] PirateVictims(GameState s,string player,string tile) => tile=="frame"?Array.Empty<string>():s.Ships.Where(x=>x.PlayerId!=player&&EdgeTiles(s,x.LocationId).Any(t=>t.Id==tile)).Select(x=>x.PlayerId).Distinct().OrderBy(x=>x).ToArray();
        private void ValidateShip(GameState s,PlayerState p,string edge,string anchor)
        {
            var location=scenario.Topology.Edges.FirstOrDefault(e=>e.Id==edge);Require(location!=null,"UnknownEdge","请选择有效边。");
            Require(p.ShipsRemaining>0,"NoShips","船库存不足。");
            Require(!s.Roads.Any(r=>r.LocationId==edge)&&!s.Ships.Any(r=>r.LocationId==edge),"OccupiedEdge","此边已有道路或船。");
            Require(SeaEdge(s,edge),"ShipOnLand","船只能放在海边。");Require(!PirateBlocks(s,edge),"PirateBlocksShip","海盗封锁此边。");
            if(anchor!=null)Require(location.Vertices.Contains(anchor),"ShipNotAtAnchor","开局船须连接刚放置的建筑。");
            else Require(location.Vertices.Any(v=>Buildings(s).Any(b=>b.PlayerId==p.Id&&b.LocationId==v)||!Blocked(s,p.Id,v)&&s.Ships.Any(x=>x.PlayerId==p.Id&&EdgeAt(x.LocationId).Vertices.Contains(v))),"DisconnectedShip","船须连接己方建筑或船；路船转换须经己方建筑。");
        }
        private static void PlaceShip(GameState s,PlayerState p,string edge)
        {
            s.Ships=Append(s.Ships,new ShipPlacement {LocationId=edge,PlayerId=p.Id,BuiltTurn=s.Turn});p.ShipsRemaining--;
        }
        private bool ShipIsOpen(GameState s,ShipPlacement ship)
        {
            var edge=EdgeAt(ship.LocationId);
            var anchors=new HashSet<string>(Buildings(s).Where(b=>b.PlayerId==ship.PlayerId).Select(b=>b.LocationId).Concat(s.Knights.Where(k=>k.PlayerId==ship.PlayerId).Select(k=>k.LocationId)));
            var routes=s.Ships.Where(x=>x.PlayerId==ship.PlayerId).Select(x=>EdgeAt(x.LocationId)).ToArray();
            if(edge.Vertices.Any(v=>!anchors.Contains(v)&&!routes.Any(e=>e.Id!=edge.Id&&e.Vertices.Contains(v))))return true;

            // Official Seafarers FAQ loop exceptions: a ring returning to the same
            // anchor has two open ships immediately beside it; a ring without an
            // anchor has all ships open. Two distinct anchors close their paths.
            bool ClosedPath(string vertex,string start,HashSet<string> visited,bool includes)
            {
                if(vertex!=start&&anchors.Contains(vertex))return includes;
                foreach(var nextEdge in routes.Where(e=>e.Vertices.Contains(vertex)))
                {
                    var next=nextEdge.Vertices.Single(v=>v!=vertex);
                    if(!visited.Add(next))continue;
                    bool found=ClosedPath(next,start,visited,includes||nextEdge.Id==edge.Id);
                    visited.Remove(next);if(found)return true;
                }
                return false;
            }
            foreach(var anchor in anchors)
                if(ClosedPath(anchor,anchor,new HashSet<string>{anchor},false))return false;
            var path=new HashSet<string>{edge.Vertices[0]};
            bool OpenLoop(string vertex)
            {
                if(vertex==edge.Vertices[1])
                {
                    int count=path.Count(anchors.Contains);
                    return count==0||count==1&&edge.Vertices.Any(anchors.Contains);
                }
                foreach(var nextEdge in routes.Where(e=>e.Id!=edge.Id&&e.Vertices.Contains(vertex)))
                {
                    var next=nextEdge.Vertices.Single(v=>v!=vertex);
                    if(!path.Add(next))continue;
                    bool found=OpenLoop(next);path.Remove(next);if(found)return true;
                }
                return false;
            }
            return OpenLoop(edge.Vertices[0]);
        }
        private void ValidateMoveSource(GameState s,PlayerState p,string edge)
        {
            var ship=s.Ships.FirstOrDefault(x=>x.LocationId==edge&&x.PlayerId==p.Id);Require(ship!=null,"NotOwnShip","请选择己方船。");
            Require(!s.ShipMovedThisTurn,"ShipAlreadyMoved","每回合只能移动一艘船。");Require(ship.BuiltTurn<s.Turn,"NewShip","本回合新船不能移动。");
            Require(!PirateBlocks(s,edge),"PirateBlocksShip","海盗旁的船不能移动。");Require(ShipIsOpen(s,ship),"ClosedRoute","只能移动开放航线末端的船。");
            Require(!RemovalDisconnectsKnight(s,p.Id,edge),"KnightDisconnected","不能使己方骑士脱离道路或船的连接。");
        }
        private bool RemovalDisconnectsKnight(GameState s,string player,string edgeId)
        {
            // A route may have become separated by an opponent. Only a new loss of the
            // knight's connection is forbidden; moving ships elsewhere remains legal.
            var edges=OwnRouteEdges(s,player).ToArray();
            HashSet<string> Connected(bool remove)
            {
                var seen=new HashSet<string>(Buildings(s).Where(b=>b.PlayerId==player).Select(b=>b.LocationId));
                var queue=new Queue<string>(seen);
                while(queue.Count>0)
                {
                    var v=queue.Dequeue();
                    foreach(var e in edges.Where(e=>(!remove||e.Id!=edgeId)&&e.Vertices.Contains(v)))
                    {
                        var next=e.Vertices.Single(x=>x!=v);
                        if(seen.Add(next)&&!Blocked(s,player,next))queue.Enqueue(next);
                    }
                }
                return seen;
            }
            var before=Connected(false);var after=Connected(true);
            return s.Knights.Any(k=>k.PlayerId==player&&before.Contains(k.LocationId)&&!after.Contains(k.LocationId));
        }
        private bool HasLegalShip(GameState s,PlayerState p)
        {
            foreach(var e in scenario.Topology.Edges)try {ValidateShip(s,p,e.Id,null);return true;}catch(RuleException){}
            return false;
        }
        private bool HasLegalRoute(GameState s,PlayerState p) => HasLegalRoad(s,p)||HasLegalShip(s,p);
        private void ValidateSeafaringSettlement(GameState s,PlayerState p,string vertex,bool setup)
        {
            var land=s.Tiles.Where(t=>IsLand(t)&&t.Vertices.Contains(vertex)).ToArray();
            Require(land.Length>0,"SeaSettlement","定居点必须邻接陆地。");
            if(setup)Require(land.Any(t=>seafaring.StartingTileIds.Contains(t.Id)),"InvalidStartingRegion","开局只能在主岛指定区域放置。");
        }
        private void RecordRegion(GameState s,PlayerState p,string vertex,bool setup)
        {
            foreach(var region in seafaring.Regions.Where(r=>s.Tiles.Any(t=>r.TileIds.Contains(t.Id)&&t.Vertices.Contains(vertex)&&IsLand(t))))
            {
                if(setup&&!p.HomeRegions.Contains(region.Id))p.HomeRegions=Append(p.HomeRegions,region.Id);
                if(!setup&&!p.HomeRegions.Contains(region.Id)&&!p.SettledRegions.Contains(region.Id))p.BonusVictoryPoints+=seafaring.IslandBonusPoints;
                if(!p.SettledRegions.Contains(region.Id))p.SettledRegions=Append(p.SettledRegions,region.Id);
            }
        }
        private void AdvanceSetup(GameState s)
        {
            s.PendingDecision=null;s.SetupPlacementIndex++;
            if(s.SetupPlacementIndex==s.Players.Length*2) {s.ActivePlayerId=s.Players[s.StartingPlayerIndex].Id;s.Turn=1;s.Phase=GamePhase.ProductionAwaitRoll;}
            else {int offset=s.SetupPlacementIndex<s.Players.Length?s.SetupPlacementIndex:2*s.Players.Length-s.SetupPlacementIndex-1;s.ActivePlayerId=s.Players[(s.StartingPlayerIndex+offset)%s.Players.Length].Id;s.Phase=GamePhase.SetupSettlement;}
        }
        private void GrantStartingResources(GameState s,PlayerState p,string vertex)
        {
            // Cities & Knights starts with one settlement and one city, but the city
            // receives one RESOURCE per adjacent hex, never commodities.
            foreach(var tile in s.Tiles.Where(t=>t.Vertices.Contains(vertex)))
            {
                if(Resources.Any(r=>ResourceName(r)==tile.Resource)) {var r=ParseResource(tile.Resource);if(s.Bank[r]>0)Transfer(s.Bank,p.Resources,r,1);}
            }
        }
        private void PopulateSeafaringView(PlayerState own,PlayerView view)
        {
            view.ScenarioId=seafaring.Id;view.ScenarioName=seafaring.Name;view.TargetVictoryPoints=16;
            view.Board.Ships=Json.Copy(state.Ships);view.Board.PirateTileId=state.PirateTileId;view.ShipMovedThisTurn=state.ShipMovedThisTurn;
            foreach(var pub in view.Players) {var p=state.Players.Single(x=>x.Id==pub.Id);pub.ShipsRemaining=p.ShipsRemaining;pub.BonusVictoryPoints=p.BonusVictoryPoints;}
            view.LegalShipEdgeIds=Array.Empty<string>();view.MovableShipEdgeIds=Array.Empty<string>();
            if(state.ActivePlayerId!=own.Id||state.TradeOffer!=null)return;
            if(state.Phase==GamePhase.SetupRoad||state.Phase==GamePhase.Action)
            {
                var kind=state.Phase==GamePhase.SetupRoad?CommandKind.SetupShip:CommandKind.BuildShip;
                view.LegalShipEdgeIds=scenario.Topology.Edges.Where(e=>IsLegal(new Command {PlayerId=own.Id,Kind=kind,TargetId=e.Id})).Select(e=>e.Id).ToArray();
            }
            if(state.Phase==GamePhase.Action)view.MovableShipEdgeIds=state.Ships.Where(x=>x.PlayerId==own.Id).Where(x=> {try {ValidateMoveSource(state,own,x.LocationId);return true;}catch(RuleException){return false;}}).Select(x=>x.LocationId).ToArray();
        }
        private IEnumerable<Command> SeafaringCandidates(GameState s,PlayerState p)
        {
            if(s.Phase==GamePhase.PendingChoice&&s.PendingDecision.PlayerId==p.Id&&s.PendingDecision.Kind=="Gold")
            {
                int count=Math.Min(s.PendingDecision.Amount,s.Bank.Total);
                // Include every available single-type selection and one mixed selection.
                foreach(var r in Resources.Where(r=>s.Bank[r]>=count)) {var bag=new ResourceBag();bag[r]=count;yield return new Command {PlayerId=p.Id,Kind=CommandKind.ResolveChoice,Resources=bag};}
                var mixed=new ResourceBag();int left=count;foreach(var r in Resources){mixed[r]=Math.Min(left,s.Bank[r]);left-=mixed[r];}
                yield return new Command {PlayerId=p.Id,Kind=CommandKind.ResolveChoice,Resources=mixed};
            }
            if(s.ActivePlayerId!=p.Id)yield break;
            if(s.Phase==GamePhase.RobberMove)
            {
                foreach(var tile in s.Tiles.Where(t=>t.Resource=="sea"))yield return new Command {PlayerId=p.Id,Kind=CommandKind.MovePirate,TargetId=tile.Id};
                yield return new Command {PlayerId=p.Id,Kind=CommandKind.MovePirate,TargetId="frame"};
            }
            if(s.Phase==GamePhase.Action)
            {
                foreach(var knight in s.Knights.Where(k=>k.PlayerId==p.Id))yield return new Command {PlayerId=p.Id,Kind=CommandKind.ChasePirate,TargetId=knight.LocationId};
                foreach(var edge in scenario.Topology.Edges)yield return new Command {PlayerId=p.Id,Kind=CommandKind.BuildShip,TargetId=edge.Id};
            }
        }
        private void ValidateSeafaring(GameState s)
        {
            void Check(bool value,string message) {if(!value)throw new InvalidDataException(message);}
            Check(CombinedScenarios.Ids.Contains(s.ScenarioId)&&s.ScenarioId==seafaring.Id&&s.ScenarioContentHash==Json.Hash(Json.Write(seafaring)),"Invalid combination scenario identity.");
            Check(s.Ships!=null&&s.Ships.Select(x=>x.LocationId).Distinct().Count()==s.Ships.Length,"Duplicate ship.");
            foreach(var ship in s.Ships)Check(s.Players.Any(p=>p.Id==ship.PlayerId)&&scenario.Topology.Edges.Any(e=>e.Id==ship.LocationId)&&SeaEdge(s,ship.LocationId)&&!s.Roads.Any(r=>r.LocationId==ship.LocationId)&&ship.BuiltTurn>=0&&ship.BuiltTurn<=s.Turn,"Invalid ship.");
            foreach(var road in s.Roads)Check(EdgeTiles(s,road.LocationId).Any(IsLand),"Road at sea.");
            foreach(var building in Buildings(s))Check(s.Tiles.Any(t=>IsLand(t)&&t.Vertices.Contains(building.LocationId)),"Building at sea.");
            foreach(var p in s.Players)
            {
                Check(p.ShipsRemaining>=0&&p.ShipsRemaining+s.Ships.Count(x=>x.PlayerId==p.Id)==15,"Ship conservation failed.");
                Check(p.HomeRegions!=null&&p.SettledRegions!=null&&p.HomeRegions.Distinct().Count()==p.HomeRegions.Length&&p.SettledRegions.Distinct().Count()==p.SettledRegions.Length&&p.HomeRegions.All(p.SettledRegions.Contains)&&p.SettledRegions.All(id=>seafaring.Regions.Any(r=>r.Id==id)),"Invalid settlement regions.");
                Check(p.BonusVictoryPoints==p.SettledRegions.Count(id=>!p.HomeRegions.Contains(id))*seafaring.IslandBonusPoints,"Invalid region bonus.");
            }
            Check(!s.FirstBarbarianAttack?s.PirateTileId==null:s.PirateTileId=="frame"||s.Tiles.Any(t=>t.Id==s.PirateTileId&&t.Resource=="sea")||s.PirateTileId==null&&s.BarbarianPosition==7,"Invalid pirate location.");
            var choices=s.DecisionQueue.Concat(s.PendingDecision==null?Array.Empty<PendingDecision>():new[]{s.PendingDecision});
            foreach(var d in choices.Where(d=>d.Kind=="Gold"))Check(d.Amount>0&&d.Amount<=3*(s.Cities.Length*2+s.Settlements.Length),"Invalid gold claim.");
        }
    }
}
