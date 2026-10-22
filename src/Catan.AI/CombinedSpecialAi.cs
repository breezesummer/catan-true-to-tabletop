using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core;
using Catan.Core.M5;
using PlayerView = Catan.Core.M5.PlayerView;
using Command = Catan.Core.M5.Command;
using CommandKind = Catan.Core.M5.CommandKind;
namespace Catan.AI
{
    public sealed partial class CombinedAi
    {
        private static Command SpecialAction(PlayerView v,List<Command> legal)
        {
            var own=v.Players.Single(p=>p.Id==v.PlayerId);
            if(v.ScenarioId=="wonders-of-catan")return legal.FirstOrDefault(c=>c.Kind==CommandKind.BuildWonder)??legal.FirstOrDefault(c=>c.Kind==CommandKind.ClaimWonder);
            if(v.ScenarioId=="pirate-islands") {
                var upgrade=legal.FirstOrDefault(c=>c.Kind==CommandKind.ConvertWarship);
                if(upgrade!=null&&v.Board.Ships.Count(s=>s.PlayerId==v.PlayerId&&s.IsWarship)<7)return upgrade;
                var ownKnights=v.Board.Knights.Where(k=>k.PlayerId==v.PlayerId).ToArray();
                if(v.Board.Ships.Any(s=>s.PlayerId==v.PlayerId&&!s.IsWarship)) {
                    var activate=legal.FirstOrDefault(c=>c.Kind==CommandKind.ActivateKnight);if(activate!=null)return activate;
                    if(ownKnights.Length==0){var recruit=legal.FirstOrDefault(c=>c.Kind==CommandKind.RecruitKnight);if(recruit!=null)return recruit;}
                }
                var attack=legal.FirstOrDefault(c=>c.Kind==CommandKind.AttackFortress);if(attack!=null&&own.ProgressCardCount<=4)return attack;
            }
            var targets=SpecialTargets(v);if(targets.Length==0)return null;
            var distance=SeaDistances(v,targets);
            return legal.Where(c=>c.Kind==CommandKind.BuildShip)
                .Select(c=>new {Command=c,Distance=v.Board.Edges.Single(e=>e.Id==c.TargetId).Vertices.Select(x=>distance.TryGetValue(x,out int d)?d:999).Min()})
                .Where(x=>x.Distance<999).OrderBy(x=>x.Distance).ThenBy(x=>x.Command.TargetId,StringComparer.Ordinal).Select(x=>x.Command).FirstOrDefault();
        }
        private static ResourceBag SpecialGoal(PlayerView v)
        {
            var own=v.Players.Single(p=>p.Id==v.PlayerId);
            if(v.ScenarioId=="wonders-of-catan"&&own.WonderId!=null&&own.WonderLevel<4)return v.Wonders.Single(w=>w.Id==own.WonderId).Cost.Copy();
            if(v.ScenarioId=="pirate-islands"&&v.Board.Ships.Any(s=>s.PlayerId==v.PlayerId&&!s.IsWarship)) {
                var knights=v.Board.Knights.Where(k=>k.PlayerId==v.PlayerId).ToArray();
                if(knights.Length==0)return Bag(wool:1,ore:1);
                if(knights.Any(k=>!k.Active))return Bag(wheat:1);
            }
            return SpecialTargets(v).Length>0&&own.ShipsRemaining>0?Bag(wood:1,wool:1):null;
        }
        private static string[] SpecialTargets(PlayerView v)
        {
            if(v.ScenarioId=="pirate-islands") {
                var f=v.Board.Fortresses.Single(x=>x.PlayerId==v.PlayerId);if(f.Strength==0)return Array.Empty<string>();
                bool beach=v.Board.Ships.Any(s=>s.PlayerId==v.PlayerId&&s.IsInvasionRoute&&v.Board.Edges.Single(e=>e.Id==s.LocationId).Vertices.Contains(f.BeachheadVertexId));
                return new[]{beach?f.VertexId:f.BeachheadVertexId};
            }
            if(v.ScenarioId=="cloth-for-catan")return v.Board.Villages.Where(x=>x.Cloth>0&&!x.TradingPlayerIds.Contains(v.PlayerId)).Select(x=>x.VertexId).ToArray();
            if(v.ScenarioId=="forgotten-tribe")return v.Board.Edges.Where(e=>v.Board.BonusEdgeIds.Contains(e.Id)||v.Board.GiftCardEdgeIds.Contains(e.Id)||v.Board.GiftPorts.Any(p=>p.EdgeId==e.Id)).SelectMany(e=>e.Vertices).Distinct().ToArray();
            return Array.Empty<string>();
        }
        private static Dictionary<string,int> SeaDistances(PlayerView v,string[] goals)
        {
            var dist=goals.Distinct().ToDictionary(x=>x,x=>0);var queue=new Queue<string>(dist.Keys);
            while(queue.Count>0){var vertex=queue.Dequeue();foreach(var edge in v.Board.Edges.Where(e=>e.Vertices.Contains(vertex))) {
                var tiles=v.Board.Tiles.Where(t=>edge.Vertices.All(t.Vertices.Contains)).ToArray();if(tiles.Length>1&&tiles.All(t=>t.Resource!="sea"&&t.Resource!="fog"))continue;
                if(v.Board.Roads.Any(r=>r.LocationId==edge.Id)||v.Board.Ships.Any(s=>s.PlayerId!=v.PlayerId&&s.LocationId==edge.Id))continue;
                foreach(var next in edge.Vertices)if(!dist.ContainsKey(next)){dist[next]=dist[vertex]+1;queue.Enqueue(next);}
            }}return dist;
        }
    }
}
