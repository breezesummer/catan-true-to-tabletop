using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Catan.Core.ThreeKingdoms;

namespace Catan.Core.M4
{
    public sealed partial class GameState
    {
        [IgnoreDataMember] internal bool B5FreeConstruction { get; set; }
    }
    public sealed partial class CitiesKnightsGameSession
    {
        public const string ThreeKingdomsB5RulesVersion = "cities-knights-three-kingdoms-b5-v1";
        private static bool B5(GameState s) => B5BoardRules.Enabled(s.ThreeKingdoms);
        private static B5BoardState B5Board(GameState s) => s.ThreeKingdoms.Board ?? (s.ThreeKingdoms.Board = new B5BoardState());
        private static bool B5Working(GameState s,string kind,string id) => !B5BoardRules.Damaged(s.ThreeKingdoms,kind,id);
        private static int B5KnightLevel(GameState s,Knight k) => B5(s)?B2SkillEngine.B5EffectiveKnightLevel(s.ThreeKingdoms,k.PlayerId,k.Level,s.Knights.Count(x=>x.PlayerId==k.PlayerId)):k.Level;
        private static bool B5KnightCanAct(GameState s,Knight k) => k.Active && (k.ActivatedTurn!=s.Turn||B5(s)&&B2SkillEngine.B5ImmediateKnightAction(s.ThreeKingdoms,k.PlayerId));
        private static void B5Exhaust(GameState s,Knight k) { k.Active=false; k.ActedTurn=s.Turn; }
        private static int B5FinalDiceTotal(GameState s) => B5(s)&&s.ThreeKingdoms.Board?.LastFormalTotal>0?s.ThreeKingdoms.Board.LastFormalTotal:s.LastDice1+s.LastDice2;
        private static int B5HarvestAmount(GameState s,string player,Tile tile,int card)
        {
            var resource=card<5?(Resource)card:card==5?Resource.Wool:card==6?Resource.Ore:Resource.Wood;
            int value=(card<5?s.Settlements.Count(b=>b.PlayerId==player&&B5Working(s,"Settlement",b.LocationId)&&tile.Vertices.Contains(b.LocationId)):0)
                +(card<5&&(resource==Resource.Brick||resource==Resource.Wheat)?2:1)*s.Cities.Count(b=>b.PlayerId==player&&tile.Vertices.Contains(b.LocationId));
            return value*(B2SkillEngine.B5DoubleHarvest(s.ThreeKingdoms,player,tile.Id)?2:1);
        }
        private static int[] B5Cost(string kind,int level=1,int track=0)
        {
            var a=new int[8]; switch(kind) {case "Road":a[0]=a[1]=1;break;case "Ship":a[0]=a[2]=1;break;case "Settlement":a[0]=a[1]=a[2]=a[3]=1;break;case "City":a[3]=2;a[4]=3;break;case "Wall":a[1]=2;break;case "Knight":a[2]=a[4]=1;break;case "Improvement":a[5+track]=level;break;} return a;
        }
        private sealed partial class B2Host : IThreeKingdomsB5Host
        {
            public B5BoardAsset[] GetBoardAssets(string playerId=null) => session.B5Assets(game,playerId);
            public B5BoardOption[] GetBoardOptions(string playerId,string kind,string argument=null,int budget=0) => session.B5Options(game,playerId,kind,argument,budget);
            public void ApplyBoardOption(string playerId,B5BoardOption option) => session.B5ApplyOption(game,playerId,option);
            public int GetScore(string playerId) => VictoryPoints(game,playerId,true);
            public int[] GetImprovements(string playerId) => (int[])game.Players.Single(p=>p.Id==playerId).Improvements.Clone();
            public int[] GetActualImprovements(string playerId) => Enumerable.Range(0,3).Select(t=>game.Players.Single(p=>p.Id==playerId).Improvements[t]+B5BoardRules.Suppressed(State,playerId,t)).ToArray();
            public void ChangeImprovement(string playerId,int track,int delta,bool temporary=false) => session.B5ChangeImprovement(game,playerId,track,delta,temporary);
            public void RestoreImprovements(string playerId)
            {
                var b=B5Board(game); var p=game.Players.Single(x=>x.Id==playerId);var q=b.Suppressed.FirstOrDefault(x=>x.PlayerId==playerId);if(q==null)return;
                for(int i=0;i<3;i++)p.Improvements[i]+=q.Levels[i];b.Suppressed=b.Suppressed.Where(x=>x.PlayerId!=playerId).ToArray();session.B5RecalculateMetropolises(game);
            }
            public void SetCollapseProtection(string playerId) {var b=B5Board(game);if(!b.CollapseProtectedPlayers.Contains(playerId))b.CollapseProtectedPlayers=b.CollapseProtectedPlayers.Concat(new[]{playerId}).ToArray();}
            public void AddPermanentScore(string playerId,int delta) {var b=B5Board(game);var a=b.ScoreAdjustments.FirstOrDefault(x=>x.PlayerId==playerId);if(a==null){a=new B5ScoreAdjustment{PlayerId=playerId};b.ScoreAdjustments=b.ScoreAdjustments.Concat(new[]{a}).ToArray();}a.Amount+=delta;}
            public void ResolveInvasion(string eventId,string[] participants,bool isVirtual) => session.B5Invasion(game,eventId,participants,isVirtual);
            public string[] GetProducingTileIds(string playerId) => game.Tiles.Where(t=>t.Number.HasValue&&(game.Cities.Any(c=>c.PlayerId==playerId&&t.Vertices.Contains(c.LocationId))||game.Settlements.Any(c=>c.PlayerId==playerId&&B5Working(game,"Settlement",c.LocationId)&&t.Vertices.Contains(c.LocationId)))).Select(t=>t.Id).ToArray();
            public int[] GetAllRedResourceTypes() => game.Tiles.Where(t=>t.Number==6||t.Number==8).Select(t=>Array.FindIndex(Resources,r=>ResourceName(r)==t.Resource)).Where(t=>t>=0).Distinct().OrderBy(t=>t).ToArray();
        }
        private B5BoardAsset[] B5Assets(GameState s,string player=null)
        {
            var list=new List<B5BoardAsset>();
            void Add(string kind,string location,string owner,int level=1,bool active=false)
            {
                string id=kind+":"+(kind=="Improvement"?owner+":":"")+location;
                int paid=B5(s)?B5Board(s).Receipts.FirstOrDefault(r=>r.AssetId==id&&r.Level==level)?.PaidCount??0:0;
                var k=kind=="Knight"?s.Knights.FirstOrDefault(x=>x.LocationId==location):null;
                list.Add(new B5BoardAsset{Id=id,Kind=kind,LocationId=location,PlayerId=owner,Level=level,EffectiveLevel=k==null?(kind=="Improvement"?s.Players.Single(p=>p.Id==owner).Improvements[int.Parse(location)]:level):B5KnightLevel(s,k),Active=active,
                    Damaged=B5BoardRules.Damaged(s.ThreeKingdoms,kind,location),Open=kind=="Road"&&B5RoadOpen(s,location),RepairCost=B5Cost(kind,level,kind=="Improvement"?int.Parse(location):0),PaidCount=paid});
            }
            foreach(var r in s.Roads)Add("Road",r.LocationId,r.PlayerId);
            foreach(var r in s.Settlements)Add("Settlement",r.LocationId,r.PlayerId);
            foreach(var r in s.Cities)Add("City",r.LocationId,r.PlayerId,2);
            foreach(var r in s.Walls)Add("Wall",r.LocationId,r.PlayerId);
            foreach(var r in s.Knights)Add("Knight",r.LocationId,r.PlayerId,r.Level,r.Active||B5(s)&&B2SkillEngine.B5AlwaysActiveKnights(s.ThreeKingdoms,r.PlayerId));
            foreach(var p in s.Players)for(int i=0;i<3;i++){int actual=p.Improvements[i]+B5BoardRules.Suppressed(s.ThreeKingdoms,p.Id,i);if(actual>0)Add("Improvement",i.ToString(),p.Id,actual);}
            if(B5(s))foreach(var d in B5Board(s).Damages.Where(d=>d.Kind=="City"&&!list.Any(a=>a.Kind=="City"&&a.LocationId==d.LocationId)))Add("City",d.LocationId,d.PlayerId,2);
            return list.Where(a=>player==null||a.PlayerId==player).ToArray();
        }
        private bool B5RoadOpen(GameState s,string id)
        {
            var road=s.Roads.Single(r=>r.LocationId==id);var edge=EdgeAt(id);
            return edge.Vertices.Any(v=>!Buildings(s).Any(b=>b.PlayerId==road.PlayerId&&b.LocationId==v)&&!s.Knights.Any(k=>k.PlayerId==road.PlayerId&&k.LocationId==v)&&s.Roads.Count(r=>r.PlayerId==road.PlayerId&&EdgeAt(r.LocationId).Vertices.Contains(v)&&B5Working(s,"Road",r.LocationId))<=1);
        }
        private B5BoardOption[] B5Options(GameState s,string player,string kind,string argument,int budget)
        {
            if(!B5(s)||!s.Players.Any(p=>p.Id==player))return Array.Empty<B5BoardOption>();var result=new List<B5BoardOption>();
            void Add(B5BoardOption o){o.PlayerId=player;o.Kind=kind;o.Argument=argument;o.Budget=budget;o.Id=kind+":"+Json.Hash(Json.Write(o));result.Add(o);}
            if(kind=="deploy-knight")
            {
                var p=s.Players.Single(x=>x.Id==player);int level=Enumerable.Range(1,p.Improvements[1]>=3?3:2).FirstOrDefault(l=>s.Knights.Count(k=>k.PlayerId==player&&k.Level==l)<2);
                if(level>0)foreach(var v in scenario.Topology.Vertices.Where(v=>!Occupied(s,v.Id)&&s.Roads.Any(r=>r.PlayerId==player&&EdgeAt(r.LocationId).Vertices.Contains(v.Id)&&B5Working(s,"Road",r.LocationId))))Add(new B5BoardOption{TargetId=v.Id,AssetKind="Knight",Level=level,Label="无双：安放"+level+"级骑士 "+v.Id});
            }
            else if(kind=="liyu")
            {
                var lubu=s.ThreeKingdoms.Generals.FirstOrDefault(g=>g.Region==GeneralRegion.Active&&g.GeneralId=="tk-g02")?.OwnerPlayerId;
                if(lubu!=null)foreach(var knight in s.Knights.Where(k=>k.PlayerId==lubu&&B5KnightCanAct(s,k)))foreach(var v in scenario.Topology.Vertices.Where(v=>!Occupied(s,v.Id)&&s.Roads.Any(r=>EdgeAt(r.LocationId).Vertices.Contains(v.Id)&&B5Working(s,"Road",r.LocationId))))
                {
                    var cost=new int[8];cost[3]=B5KnightLevel(s,knight);Add(new B5BoardOption{SourceId=knight.LocationId,TargetId=v.Id,OtherPlayerId=lubu,AssetKind="Knight",Level=cost[3],Cost=cost,Label="利驭：移动至 "+v.Id});
                    foreach(var enemy in s.Knights.Where(k=>k.PlayerId!=lubu&&B5KnightLevel(s,k)<cost[3]&&scenario.Topology.Edges.Any(e=>e.Vertices.Contains(v.Id)&&e.Vertices.Contains(k.LocationId))))Add(new B5BoardOption{SourceId=knight.LocationId,TargetId=v.Id,OtherPlayerId=lubu,AssetKind="Knight",Level=cost[3],Cost=cost,PayloadJson="kill:"+enemy.LocationId,Label="利驭：移动并击杀 "+enemy.LocationId});
                    if(s.FirstBarbarianAttack&&s.Tiles.Any(t=>t.Id==s.RobberTileId&&t.Vertices.Contains(v.Id)))Add(new B5BoardOption{SourceId=knight.LocationId,TargetId=v.Id,OtherPlayerId=lubu,AssetKind="Knight",Level=cost[3],Cost=cost,PayloadJson="chase",Label="利驭：移动并驱赶强盗 "+v.Id});
                }
            }
            else if(kind=="return-knight")
            {
                foreach(var knight in s.Knights.Where(k=>k.PlayerId==player&&(argument==null||k.LocationId==argument)&&!s.Roads.Any(r=>r.PlayerId==player&&EdgeAt(r.LocationId).Vertices.Contains(k.LocationId)&&B5Working(s,"Road",r.LocationId))))
                {
                    var options=scenario.Topology.Vertices.Where(v=>!Occupied(s,v.Id)&&s.Roads.Any(r=>r.PlayerId==player&&EdgeAt(r.LocationId).Vertices.Contains(v.Id)&&B5Working(s,"Road",r.LocationId))).ToArray();
                    if(options.Length==0)Add(new B5BoardOption{SourceId=knight.LocationId,TargetId=null,AssetKind="Knight",Label="无合法位置，骑士退回供应"});
                    else foreach(var v in options)Add(new B5BoardOption{SourceId=knight.LocationId,TargetId=v.Id,AssetKind="Knight",Label="骑士归位 "+v.Id});
                }
            }
            else if(kind.StartsWith("free-",StringComparison.Ordinal))
            {
                foreach(var c in B5FreeCandidates(s,player,kind,budget))
                {
                    var probe=s.CopyForTransaction();try{B5ApplyFree(probe,c);var assetKind=B5CommandKind(c);int level=assetKind=="Improvement"?CardHost(probe).GetActualImprovements(player)[(int)c.Track]:assetKind=="Knight"?probe.Knights.Single(k=>k.PlayerId==player&&k.LocationId==c.TargetId).Level:assetKind=="City"?2:1;
                        if(kind=="free-military"&&level>budget)continue;Add(new B5BoardOption{TargetId=c.TargetId,AssetKind=assetKind,Level=level,PayloadJson=Json.Write(c),Label="免费建设"+B5KindName(assetKind)+" "+(assetKind=="Improvement"?new[]{"贸易","政治","科技"}[(int)c.Track]:c.TargetId)});}
                    catch(RuleException){}catch(InvalidOperationException){}
                }
            }
            else foreach(var a in B5Assets(s))
            {
                if(argument!=null&&a.PlayerId!=argument&&a.Id!=argument)continue;bool allowed=false;
                switch(kind)
                {
                    case "damage-road":case "liegong-road":allowed=a.Kind=="Road"&&!a.Damaged;break;
                    case "kurou":allowed=a.PlayerId==player&&!a.Damaged&&a.Kind!="Knight";break;
                    case "liegong":allowed=a.Kind!="Road"&&a.Kind!="Improvement"&&!a.Damaged;break;
                    case "burn-city":allowed=a.Kind=="City"&&!a.Damaged;break;
                    case "downgrade-tech":allowed=a.Kind=="Improvement";break;
                    case "repair":case "repair-own":allowed=a.Damaged&&(kind!="repair-own"||a.PlayerId==player);break;
                }
                if(!allowed)continue;
                if(a.Kind=="City"&&!a.Damaged&&s.Metropolises.Any(m=>m.LocationId==a.LocationId)&&!s.Cities.Any(c=>c.PlayerId==a.PlayerId&&c.LocationId!=a.LocationId&&!s.Metropolises.Any(m=>m.LocationId==c.LocationId)))continue;
                if(kind=="liegong"&&(a.Kind=="Settlement"||a.Kind=="City")&&B5BoardRules.CollapseProtected(s.ThreeKingdoms,a.PlayerId))continue;
                if(a.Kind=="Settlement"&&B5BoardRules.Damaged(s.ThreeKingdoms,"City",a.LocationId)&&kind!="repair"&&kind!="repair-own")continue;
                if(a.Damaged&&a.Kind=="City"&&!s.DowngradedCities.Any(d=>d.LocationId==a.LocationId)&&s.Players.Single(p=>p.Id==a.PlayerId).Pieces.Cities==0)continue;
                if(kind=="repair-own"&&B4PaymentAllocator.Allocate(s.ThreeKingdoms,player,CardHost(s).GetHand(player),a.RepairCost,null)==null)continue;
                Add(new B5BoardOption{TargetId=a.Id,OtherPlayerId=a.PlayerId,AssetKind=a.Kind,Level=a.Level,Cost=a.RepairCost,PaidCount=a.PaidCount,Label=(kind.StartsWith("repair")?"修复":kind=="kurou"?"苦肉拆除":kind=="downgrade-tech"?"降低设施":"损害")+B5KindName(a.Kind)+" "+a.LocationId});
            }
            return result.ToArray();
        }
        private static string B5KindName(string kind) => kind=="Road"?"道路":kind=="Ship"?"船":kind=="Settlement"?"村庄":kind=="City"?"城市":kind=="Wall"?"城墙":kind=="Knight"?"骑士":"设施";
        private IEnumerable<Command> B5FreeCandidates(GameState s,string player,string kind,int budget)
        {
            var types=kind=="free-military"?new[]{"Knight","Improvement"}:new[]{kind.Substring(5)};
            foreach(var type in types)
            {
                if(type=="Improvement") {for(int t=0;t<3;t++)yield return new Command{PlayerId=player,Kind=CommandKind.ImproveCity,Track=(ImprovementTrack)t};continue;}
                if(type=="Knight") {foreach(var k in s.Knights.Where(k=>k.PlayerId==player))yield return new Command{PlayerId=player,Kind=CommandKind.PromoteKnight,TargetId=k.LocationId};}
                CommandKind command;switch(type){case "Road":command=CommandKind.BuildRoad;break;case "Settlement":command=CommandKind.BuildSettlement;break;case "City":command=CommandKind.BuildCity;break;case "Wall":case "CityWall":command=CommandKind.BuildWall;break;case "Knight":command=CommandKind.RecruitKnight;break;default:continue;}
                foreach(var id in type=="Road"?scenario.Topology.Edges.Select(e=>e.Id):scenario.Topology.Vertices.Select(v=>v.Id))yield return new Command{PlayerId=player,Kind=command,TargetId=id};
            }
        }
        private static string B5CommandKind(Command c) => c.Kind==CommandKind.BuildRoad||c.Kind==CommandKind.PlaceFreeRoad?"Road":c.Kind==CommandKind.BuildSettlement?"Settlement":c.Kind==CommandKind.BuildCity?"City":c.Kind==CommandKind.BuildWall?"Wall":c.Kind==CommandKind.ImproveCity?"Improvement":c.Kind==CommandKind.RecruitKnight||c.Kind==CommandKind.PromoteKnight?"Knight":null;
        private void B5ApplyFree(GameState s,Command c)
        {
            var phase=s.Phase;var active=s.ActivePlayerId;var free=s.B5FreeConstruction;s.Phase=GamePhase.Action;s.ActivePlayerId=c.PlayerId;s.B5FreeConstruction=true;
            try{B5ValidateNative(s,c);ApplyBaseRules(s,c);}finally{s.B5FreeConstruction=free;s.ActivePlayerId=active;if(s.Phase==GamePhase.Action)s.Phase=phase;}
        }
        private void B5ApplyOption(GameState s,string player,B5BoardOption option,bool resume=false)
        {
            Require(B5(s)&&option!=null&&option.PlayerId==player,"InvalidBoardOption","棋盘操作身份无效。");
            if(!resume) {var expected=B5Options(s,player,option.Kind,option.Argument,option.Budget).FirstOrDefault(x=>x.Id==option.Id);Require(expected!=null,"StaleBoardOption","棋盘对象或合法条件已改变。");expected.RootEventId=option.RootEventId;option=expected;}
            var host=CardHost(s);string root=option.RootEventId??"b5-board-"+s.ThreeKingdoms.NextId++;
            if(option.Kind.StartsWith("free-",StringComparison.Ordinal))
            {
                var before=host.GetBoardAssets();var c=Json.Read<Command>(option.PayloadJson);c.Id="b5-free-"+s.ThreeKingdoms.NextId++;B5ApplyFree(s,c);B5RecordNativeConstruction(s,c,before,Array.Empty<B3NativePayment>(),root);RecalculateAwards(s);return;
            }
            if(option.Kind=="deploy-knight") {s.Knights=Append(s.Knights,new Knight{PlayerId=player,LocationId=option.TargetId,Level=option.Level,Active=true,ActivatedTurn=s.Turn});return;}
            if(option.Kind=="return-knight") {var k=s.Knights.Single(x=>x.PlayerId==player&&x.LocationId==option.SourceId);if(option.TargetId==null)s.Knights=s.Knights.Where(x=>x!=k).ToArray();else k.LocationId=option.TargetId;RecalculateAwards(s);return;}
            if(option.Kind=="liyu")
            {
                var k=s.Knights.Single(x=>x.PlayerId==option.OtherPlayerId&&x.LocationId==option.SourceId);k.LocationId=option.TargetId;B5Exhaust(s,k);
                if(option.PayloadJson?.StartsWith("kill:",StringComparison.Ordinal)==true){var target=option.PayloadJson.Substring(5);s.Knights=s.Knights.Where(x=>x.LocationId!=target||B3Immune(s,x.PlayerId)).ToArray();}
                if(option.PayloadJson=="chase"){s.PendingDecision=new PendingDecision{Kind="Robber",PlayerId=player,ReturnPhase=GamePhase.Action};s.Phase=GamePhase.RobberMove;}RecalculateAwards(s);return;
            }
            var a=host.GetBoardAssets().FirstOrDefault(x=>x.Id==option.TargetId);Require(a!=null,"MissingBoardAsset","棋盘实体不存在。");
            if(!resume&&a.Kind=="City"&&!a.Damaged&&s.Metropolises.Any(m=>m.LocationId==a.LocationId))
            {
                B5Board(s).DeferredOperations=Append(B5Board(s).DeferredOperations,Json.Copy(option));QueueDecision(s,new PendingDecision{Kind="B5MetropolisRelocate",PlayerId=a.PlayerId,AnchorVertexId=a.LocationId,Options=s.Cities.Where(c=>c.PlayerId==a.PlayerId&&c.LocationId!=a.LocationId&&!s.Metropolises.Any(m=>m.LocationId==c.LocationId)).Select(c=>c.LocationId).ToArray()});return;
            }
            if(option.Kind=="repair-own"){var parentPayments=s.ThreeKingdomsNativePayments;PayB4(s,s.Players.Single(p=>p.Id==player),option.Cost);s.ThreeKingdomsNativePayments=parentPayments;}
            if(option.Kind=="repair"||option.Kind=="repair-own")B5Repair(s,a);
            else if(a.Kind=="Improvement")B5ChangeImprovement(s,a.PlayerId,int.Parse(a.LocationId),-1,false);
            else B5DamageAsset(s,a,option.Kind);
            RecalculateAwards(s);
        }
        private static void B5ForgetAsset(GameState s,string kind,string location)
        {
            if(!B5(s))return;
            var b=B5Board(s);b.Damages=b.Damages.Where(d=>d.Kind!=kind||d.LocationId!=location).ToArray();
            b.Receipts=b.Receipts.Where(r=>r.AssetId!=kind+":"+location).ToArray();
        }
        private void B5DamageAsset(GameState s,B5BoardAsset a,string operation)
        {
            var p=s.Players.Single(x=>x.Id==a.PlayerId);bool liegong=operation=="liegong"||operation=="liegong-road";bool collapse=liegong&&(a.Kind=="City"||a.Kind=="Settlement");
            if(collapse&&B5BoardRules.CollapseProtected(s.ThreeKingdoms,a.PlayerId)||B3Immune(s,a.PlayerId)&&operation!="kurou")return;
            void Damage(string kind){var b=B5Board(s);if(!b.Damages.Any(d=>d.Kind==kind&&d.LocationId==a.LocationId))b.Damages=Append(b.Damages,new B5Damage{Kind=kind,LocationId=a.LocationId,PlayerId=a.PlayerId,Priority=liegong});}
            if(a.Kind=="Road") {if(operation=="kurou"||a.Open){s.Roads=s.Roads.Where(r=>r.LocationId!=a.LocationId).ToArray();p.Pieces.Roads++;}else Damage("Road");}
            else if(a.Kind=="Settlement") {if(collapse)Damage("Settlement");else {s.Settlements=s.Settlements.Where(x=>x.LocationId!=a.LocationId).ToArray();if(s.DowngradedCities.Any(x=>x.LocationId==a.LocationId)){s.DowngradedCities=s.DowngradedCities.Where(x=>x.LocationId!=a.LocationId).ToArray();p.Pieces.Cities++;}else p.Pieces.Settlements++;}}
            else if(a.Kind=="City")
            {
                s.Cities=s.Cities.Where(x=>x.LocationId!=a.LocationId).ToArray();s.Walls=s.Walls.Where(x=>x.LocationId!=a.LocationId).ToArray();
                if(operation=="burn-city")p.Pieces.Cities++;
                else {s.Settlements=Append(s.Settlements,Placement(a.LocationId,a.PlayerId));if(p.Pieces.Settlements>0){p.Pieces.Settlements--;p.Pieces.Cities++;}else s.DowngradedCities=Append(s.DowngradedCities,Placement(a.LocationId,a.PlayerId));if(collapse)Damage("City");}
            }
            else if(a.Kind=="Wall")s.Walls=s.Walls.Where(x=>x.LocationId!=a.LocationId).ToArray();
            else if(a.Kind=="Knight")s.Knights=s.Knights.Where(x=>x.LocationId!=a.LocationId).ToArray();
            if(!B5BoardRules.Damaged(s.ThreeKingdoms,a.Kind,a.LocationId))B5Board(s).Receipts=B5Board(s).Receipts.Where(r=>r.AssetId!=a.Id||r.Level!=a.Level).ToArray();
        }
        private void B5Repair(GameState s,B5BoardAsset a)
        {
            Require(a.Damaged,"NotDamaged","该对象没有可修复损害。");
            if(a.Kind=="City")
            {
                var p=s.Players.Single(x=>x.Id==a.PlayerId);bool reserved=s.DowngradedCities.Any(x=>x.LocationId==a.LocationId);Require(reserved||p.Pieces.Cities>0,"NoCities","修复城市的棋子库存不足。");
                s.Settlements=s.Settlements.Where(x=>x.LocationId!=a.LocationId).ToArray();s.Cities=Append(s.Cities,Placement(a.LocationId,a.PlayerId));
                if(reserved)s.DowngradedCities=s.DowngradedCities.Where(x=>x.LocationId!=a.LocationId).ToArray();else {p.Pieces.Cities--;p.Pieces.Settlements++;}
            }
            B5Board(s).Damages=B5Board(s).Damages.Where(d=>d.Kind!=a.Kind||d.LocationId!=a.LocationId).ToArray();
        }
        private void B5ChangeImprovement(GameState s,string player,int track,int delta,bool temporary)
        {
            Require(track>=0&&track<3&&delta<=0,"InvalidImprovementChange","设施损害须指定合法方向及非正数变化。");var p=s.Players.Single(x=>x.Id==player);var b=B5Board(s);var q=b.Suppressed.FirstOrDefault(x=>x.PlayerId==player);
            if(temporary){Require(p.Improvements[track]+delta>=0,"ExcessSuppression","扣置不能超过当前有效设施等级。");if(q==null){q=new B5SuppressedTechnology{PlayerId=player};b.Suppressed=Append(b.Suppressed,q);}q.Levels[track]-=delta;p.Improvements[track]+=delta;}
            else {int suppressed=q?.Levels[track]??0;int actual=p.Improvements[track]+suppressed;Require(actual+delta>=0,"InvalidImprovementChange","设施等级不足。");int next=actual+delta;int remain=Math.Min(suppressed,next);if(q!=null)q.Levels[track]=remain;p.Improvements[track]=next-remain;b.Receipts=b.Receipts.Where(r=>r.AssetId!="Improvement:"+player+":"+track||r.Level<=next).ToArray();}
            B5RecalculateMetropolises(s);
        }
        private void B5RecalculateMetropolises(GameState s)
        {
            for(int t=0;t<3;t++)
            {
                var track=(ImprovementTrack)t;var old=s.Metropolises.FirstOrDefault(m=>m.Track==track);
                var applicants=TurnOrder(s).Where(p=>p.Improvements[t]>=4&&(old?.PlayerId==p.Id||s.Cities.Count(c=>c.PlayerId==p.Id&&!s.Metropolises.Any(m=>m.LocationId==c.LocationId))>s.DecisionQueue.Count(d=>d.Kind=="Metropolis"&&d.PlayerId==p.Id&&d.Track!=track))).ToArray();
                int max=applicants.Length==0?0:applicants.Max(p=>p.Improvements[t]);var eligible=applicants.Where(p=>p.Improvements[t]==max).ToArray();
                if(old!=null&&eligible.Any(p=>p.Id==old.PlayerId))continue;
                s.Metropolises=s.Metropolises.Where(m=>m.Track!=track).ToArray();s.DecisionQueue=s.DecisionQueue.Where(d=>d.Kind!="Metropolis"||d.Track!=track).ToArray();
                if(eligible.Length>0){var owner=eligible[0];var cities=s.Cities.Where(c=>c.PlayerId==owner.Id&&!s.Metropolises.Any(m=>m.LocationId==c.LocationId)).Select(c=>c.LocationId).ToArray();if(cities.Length>0)QueueDecision(s,new PendingDecision{Kind="Metropolis",PlayerId=owner.Id,Track=track,Options=cities});}
            }
        }
        private void B5ResolveMetropolis(GameState s,PlayerState p,Command c,PendingDecision d)
        {
            Require(d.Options.Contains(c.TargetId)&&s.Cities.Any(x=>x.PlayerId==p.Id&&x.LocationId==c.TargetId)&&!s.Metropolises.Any(m=>m.LocationId==c.TargetId),"InvalidCity","请选择合法的大都会迁移城市。");
            var m=s.Metropolises.Single(x=>x.LocationId==d.AnchorVertexId);m.LocationId=c.TargetId;var op=B5Board(s).DeferredOperations.First(x=>x.TargetId=="City:"+d.AnchorVertexId);B5Board(s).DeferredOperations=B5Board(s).DeferredOperations.Where(x=>x!=op).ToArray();B5ApplyOption(s,op.PlayerId,op,true);
        }
        private void B5ValidateNative(GameState s,Command c)
        {
            if(!B5(s))return;string kind=B5CommandKind(c);if(kind==null)return;
            Require(!B5BoardRules.MustRepair(s.ThreeKingdoms,c.PlayerId,kind),"RepairDamagedFirst","必须先修复己方同类受损建筑。");
            if(kind=="City")Require(!B5BoardRules.Damaged(s.ThreeKingdoms,"Settlement",c.TargetId),"RepairDamagedFirst","必须先修复倒塌的村庄。");
            if(kind=="Road"||kind=="Settlement")
            {
                var edge=kind=="Road"?scenario.Topology.Edges.FirstOrDefault(e=>e.Id==c.TargetId):null;var vertices=kind=="Road"?edge?.Vertices??Array.Empty<string>():new[]{c.TargetId};
                var zhangfei=s.ThreeKingdoms.Generals.Where(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId!=c.PlayerId&&B2SkillEngine.B5IsZhangFei(s.ThreeKingdoms,g.OwnerPlayerId)).Select(g=>g.OwnerPlayerId).ToArray();
                bool touch=Buildings(s).Any(b=>zhangfei.Contains(b.PlayerId)&&vertices.Contains(b.LocationId))||s.Roads.Any(r=>zhangfei.Contains(r.PlayerId)&&EdgeAt(r.LocationId).Vertices.Any(vertices.Contains));
                Require(!touch,"PaoxiaoConnection","咆哮期间不能新增与张飞建筑直接相连的建筑。");
            }
        }
        private void B5RecordNativeConstruction(GameState s,Command c,B5BoardAsset[] before,B3NativePayment[] payments,string rootEvent=null)
        {
            if(!B5(s)||before==null)return;
            bool construction=B5CommandKind(c)!=null||c.Kind==CommandKind.SetupRoad||c.Kind==CommandKind.SetupSettlement||c.Kind==CommandKind.PlayProgressCard&&(c.ProgressCard==ProgressCardKind.Medicine||c.ProgressCard==ProgressCardKind.Engineering||c.ProgressCard==ProgressCardKind.Smithing||c.ProgressCard==ProgressCardKind.Crane)||c.Kind==CommandKind.ResolveProgressChoice;
            if(!construction)return;var after=CardHost(s).GetBoardAssets();var added=after.Where(a=>!a.Damaged&&!before.Any(x=>x.Id==a.Id&&x.Level==a.Level)&&(c.Kind!=CommandKind.ResolveProgressChoice||a.Kind=="Road"||a.Kind=="Ship")).ToArray();
            foreach(var a in added)
            {
                if(before.Any(x=>x.Id==a.Id&&x.Damaged))continue;
                int paid=(payments??Array.Empty<B3NativePayment>()).Where(p=>p.PlayerId==a.PlayerId).Sum(p=>p.Cards.Sum());var b=B5Board(s);b.Receipts=b.Receipts.Where(r=>r.AssetId!=a.Id||r.Level!=a.Level).Concat(new[]{new B5ConstructionReceipt{AssetId=a.Id,PlayerId=a.PlayerId,Level=a.Level,PaidCount=paid}}).ToArray();
                if(s.ThreeKingdoms.Clock.Phase==ThreeKingdomsPhase.Play){string eventId="b5-build-"+c.Id+"-"+a.Id;B2SkillEngine.OnCivilBuilt(CardHost(s),eventId,a.PlayerId,a.Kind,a.Level,rootEvent);B2SkillEngine.OnB5MilitaryBuilt(CardHost(s),eventId,a.PlayerId,a.Kind);}
            }
        }
        private void B5CompleteInvasion(GameState s,string eventId)
        {
            var b=B5Board(s);var invasion=b.Invasions.FirstOrDefault(x=>x.EventId==eventId);if(invasion==null)return;
            foreach(var k in s.Knights.Where(k=>invasion.Participants.Contains(k.PlayerId)))k.Active=false;
            if(!invasion.IsVirtual){s.BarbarianPosition=0;s.BarbarianAttackStrength=null;s.BarbarianDefenseStrength=null;}
            b.Invasions=b.Invasions.Where(x=>x!=invasion).ToArray();B2SkillEngine.OnB5RaidResolved(CardHost(s),eventId,invasion.CityCollapsed,invasion.PointAwarded,false,null);
        }
        private void B5Invasion(GameState s,string eventId,string[] participants,bool virtualAttack)
        {
            var b=B5Board(s);if(b.ProcessedInvasions.Contains(eventId))return;Require(participants!=null&&participants.Distinct().Count()==participants.Length&&participants.All(p=>s.Players.Any(x=>x.Id==p)),"InvalidParticipants","入侵参与者无效。");b.ProcessedInvasions=Append(b.ProcessedInvasions,eventId);
            var ids=participants;var strength=ids.ToDictionary(p=>p,p=>s.Knights.Where(k=>k.PlayerId==p&&(k.Active||B2SkillEngine.B5AlwaysActiveKnights(s.ThreeKingdoms,p))).Sum(k=>B5KnightLevel(s,k)));
            int attack=s.Cities.Count(c=>ids.Contains(c.PlayerId)),defense=strength.Values.Sum();bool point=false;
            if(!virtualAttack){s.BarbarianPosition=7;s.FirstBarbarianAttack=true;if(s.RobberTileId==null)s.RobberTileId=scenario.Topology.Tiles.Single(t=>t.Resource=="desert").Id;s.BarbarianAttackStrength=attack;s.BarbarianDefenseStrength=defense;}
            if(ids.Length>0&&defense>=attack){int max=strength.Values.Max();var winners=TurnOrder(s).Where(p=>ids.Contains(p.Id)&&strength[p.Id]==max).ToArray();if(winners.Length==1){winners[0].DefenderPoints++;point=true;}else foreach(var p in winners)QueueDecision(s,new PendingDecision{Kind="DefenderProgress",PlayerId=p.Id});}
            else if(ids.Length>0)
            {
                var vulnerable=TurnOrder(s).Where(p=>ids.Contains(p.Id)&&s.Cities.Any(c=>c.PlayerId==p.Id&&!s.Metropolises.Any(m=>m.LocationId==c.LocationId)) ).ToArray();
                if(vulnerable.Length>0){int least=vulnerable.Min(p=>strength[p.Id]);foreach(var p in vulnerable.Where(p=>strength[p.Id]==least&&!B3Immune(s,p.Id)&&!B2SkillEngine.B5InvasionImmune(s.ThreeKingdoms,p.Id)&&!B5BoardRules.CollapseProtected(s.ThreeKingdoms,p.Id)))QueueDecision(s,new PendingDecision{Kind="PillageCity",PlayerId=p.Id,Options=s.Cities.Where(c=>c.PlayerId==p.Id&&!s.Metropolises.Any(m=>m.LocationId==c.LocationId)).Select(c=>c.LocationId).ToArray()});}
            }
            b.Invasions=Append(b.Invasions,new B5InvasionRecord{EventId=eventId,IsVirtual=virtualAttack,Participants=ids,PointAwarded=point,AttackStrength=attack,DefenseStrength=defense});
            QueueDecision(s,new PendingDecision{Kind="B5InvasionComplete",PlayerId=s.ActivePlayerId,AnchorVertexId=eventId});
        }
    }
}
