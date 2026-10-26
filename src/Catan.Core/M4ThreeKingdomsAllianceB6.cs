using System;
using System.Linq;
using System.Runtime.Serialization;
using Catan.Core.ThreeKingdoms;

namespace Catan.Core.M4
{
    public sealed partial class PendingDecision
    {
        [DataMember(Order=150)] public bool AllianceReturnHome { get; set; }
    }
    public sealed partial class CitiesKnightsGameSession
    {
        private static int B6Technology(GameState s,string player,int track) =>
            B6AllianceRules.Members(s.ThreeKingdoms,player).Max(id=>s.Players.Single(p=>p.Id==id).Improvements[track]);
        private static bool B6ControlsKnight(GameState s,string player,Knight knight) => B6AllianceRules.Allies(s.ThreeKingdoms,player,knight.PlayerId);
        private string[] B6AllianceHomeVertices(GameState s,string player) => s.Roads.Where(r=>B6AllianceRules.Allies(s.ThreeKingdoms,player,r.PlayerId)&&B5Working(s,"Road",r.LocationId)).Select(r=>EdgeAt(r.LocationId)).SelectMany(e=>e.Vertices).Distinct().Where(v=>!Occupied(s,v)).OrderBy(v=>v,StringComparer.Ordinal).ToArray();
        private void B6ReturnAllianceKnights(GameState s)
        {
            var alliance=s.ThreeKingdoms?.AllianceB6;
            if(!B6AllianceRules.Enabled(s.ThreeKingdoms)||alliance?.KnightsNeedHomeCheck!=true||s.PendingDecision!=null||B2Busy(s))return;
            alliance.KnightsNeedHomeCheck=false;
            var affected=alliance.ReturnHomePlayerIds;alliance.ReturnHomePlayerIds=Array.Empty<string>();
            foreach(var k in s.Knights.Where(k=>affected.Contains(k.PlayerId)).OrderBy(k=>k.PlayerId,StringComparer.Ordinal).ThenBy(k=>k.LocationId,StringComparer.Ordinal).ToArray())
            {
                var routes=s.Roads.Where(r=>B6AllianceRules.Allies(s.ThreeKingdoms,k.PlayerId,r.PlayerId)&&B5Working(s,"Road",r.LocationId)).Select(r=>EdgeAt(r.LocationId)).ToArray();
                if(routes.Any(e=>e.Vertices.Contains(k.LocationId)))continue;
                string[] options=routes.SelectMany(e=>e.Vertices).Distinct().Where(v=>!Occupied(s,v)).OrderBy(v=>v,StringComparer.Ordinal).ToArray();
                s.Knights=s.Knights.Where(x=>x!=k).ToArray();
                if(options.Length>0)QueueDecision(s,new PendingDecision{Kind="DisplacedKnight",AllianceReturnHome=true,PlayerId=k.PlayerId,AnchorVertexId=k.LocationId,DisplacedKnight=k,Options=options});
            }
            if(s.DecisionQueue.Length>0)ContinueDecisions(s);
        }
    }
}
