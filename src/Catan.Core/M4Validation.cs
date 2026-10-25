using System;
using System.IO;
using System.Linq;

namespace Catan.Core.M4
{
    public sealed partial class CitiesKnightsGameSession
    {
        private void ValidateExpansion(GameState s)
        {
            void Check(bool valid,string message) {if(!valid)throw new InvalidDataException(message);}
            Check(ValidBag(s.CommodityBank)&&s.Players.All(p=>ValidBag(p.Commodities)),"Invalid commodity bag.");
            foreach(var c in Commodities) Check(s.CommodityBank[c]+s.Players.Sum(p=>p.Commodities[c])+B2Escrow(s,5+(int)c)==12,"Commodity conservation: "+c);
            Check(s.Players.All(p=>p.Improvements!=null&&p.Improvements.Length==3&&p.Improvements.All(i=>i>=0&&i<=5)&&p.DefenderPoints>=0),"Invalid improvements or defender points.");
            Check(s.Knights.Select(k=>k.LocationId).Distinct().Count()==s.Knights.Length,"Duplicate knight location.");
            foreach(var k in s.Knights) Check(s.Players.Any(p=>p.Id==k.PlayerId)&&scenario.Topology.Vertices.Any(v=>v.Id==k.LocationId)&&!Buildings(s).Any(b=>b.LocationId==k.LocationId)&&k.Level>=1&&k.Level<=3&&k.ActivatedTurn<=s.Turn&&k.PromotedTurn<=s.Turn&&k.ActedTurn<=s.Turn,"Invalid knight.");
            var allKnights=s.Knights.Concat(s.PendingDecision?.DisplacedKnight==null?Array.Empty<Knight>():new[]{s.PendingDecision.DisplacedKnight}).Concat(s.DecisionQueue.Where(d=>d.DisplacedKnight!=null).Select(d=>d.DisplacedKnight)).ToArray();
            foreach(var p in s.Players)for(int level=1;level<=3;level++)Check(allKnights.Count(k=>k.PlayerId==p.Id&&k.Level==level)<=2,"Knight supply exhausted.");
            Check(s.Walls.Select(w=>w.LocationId).Distinct().Count()==s.Walls.Length&&s.Walls.All(w=>s.Cities.Any(c=>c.PlayerId==w.PlayerId&&c.LocationId==w.LocationId))&&s.Players.All(p=>s.Walls.Count(w=>w.PlayerId==p.Id)<=3),"Invalid walls.");
            Check(s.Metropolises.Select(m=>m.Track).Distinct().Count()==s.Metropolises.Length&&s.Metropolises.Select(m=>m.LocationId).Distinct().Count()==s.Metropolises.Length,"Duplicate metropolis.");
            foreach(var m in s.Metropolises)Check(Enum.IsDefined(typeof(ImprovementTrack),m.Track)&&s.Cities.Any(c=>c.PlayerId==m.PlayerId&&c.LocationId==m.LocationId)&&s.Players.Single(p=>p.Id==m.PlayerId).Improvements[(int)m.Track]>=4,"Invalid metropolis.");
            Check(s.DowngradedCities.All(c=>s.Settlements.Any(v=>v.PlayerId==c.PlayerId&&v.LocationId==c.LocationId)),"Invalid pillaged city.");
            Check(s.BarbarianPosition>=0&&s.BarbarianPosition<=7&&Enum.IsDefined(typeof(EventDie),s.LastEventDie),"Invalid barbarian track/event die.");
            Check(s.BarbarianPosition!=7||s.FirstBarbarianAttack&&(s.Phase==GamePhase.Finished||s.DecisionQueue.Any(d=>d.Kind=="BarbarianReturnHome"||B5(s)&&d.Kind=="B5InvasionComplete")),"Attack must finish before barbarians return home.");
            Check(s.BarbarianAttackStrength.HasValue==s.BarbarianDefenseStrength.HasValue,"蛮族战斗快照必须同时包含攻守力量。");
            if(s.BarbarianAttackStrength.HasValue)
                Check(s.BarbarianPosition==7&&(B5(s)?B5Board(s).Invasions.Any(i=>!i.IsVirtual&&i.AttackStrength==s.BarbarianAttackStrength&&i.DefenseStrength==s.BarbarianDefenseStrength):s.BarbarianAttackStrength>=s.Cities.Length&&s.BarbarianAttackStrength<=s.Cities.Length+s.Players.Length&&s.BarbarianDefenseStrength==s.Knights.Where(k=>k.Active).Sum(k=>k.Level)),"蛮族战斗快照与结算状态不符。");
            Check(s.DecisionQueue.All(d=>d!=null&&s.Players.Any(p=>p.Id==d.PlayerId)),"Invalid decision queue.");
            if(B5(s)){Check(s.Players.All(p=>Enumerable.Range(0,3).All(t=>p.Improvements[t]+Catan.Core.ThreeKingdoms.B5BoardRules.Suppressed(s.ThreeKingdoms,p.Id,t)<=5)),"实际科技等级超出供应。");foreach(var d in B5Board(s).Damages)Check(d.Kind=="Road"?s.Roads.Any(x=>x.PlayerId==d.PlayerId&&x.LocationId==d.LocationId):d.Kind=="City"||d.Kind=="Settlement"?s.Settlements.Any(x=>x.PlayerId==d.PlayerId&&x.LocationId==d.LocationId):false,"损害记录缺少对应实体。");}
            Check(s.Tiles.Length==scenario.Topology.Tiles.Length&&s.Tiles.Select(t=>t.Id).Distinct().Count()==s.Tiles.Length,"Invalid board tiles.");
            foreach(var tile in s.Tiles) {var original=scenario.Topology.Tiles.Single(t=>t.Id==tile.Id);Check(tile.Resource==original.Resource&&tile.Q==original.Q&&tile.R==original.R&&tile.Vertices.SequenceEqual(original.Vertices),"Immutable topology changed.");}
            Check(s.Tiles.Select(t=>t.Number??0).OrderBy(n=>n).SequenceEqual(scenario.Topology.Tiles.Select(t=>t.Number??0).OrderBy(n=>n)),"Number-disc conservation failed.");
        }
    }
}
