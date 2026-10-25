using System;
using System.Linq;
using Catan.Core.ThreeKingdoms;

namespace Catan.Core.M4
{
    public sealed partial class CitiesKnightsGameSession
    {
        private static int B5ProgressLimit(GameState s,string player) => 4+B2SkillEngine.B5ProgressLimitBonus(s.ThreeKingdoms,player);
        private static B4ProgressResolution B5ProgressContext(GameState s) => s.ThreeKingdomsProgressResolutions?
            .FirstOrDefault(r=>!r.Queued && r.Card!=null && !r.Card.PublicPoint);
        private static bool B5ProgressImmune(GameState s,string player,ProgressCardKind? card=null,string useId=null)
        {
            if(!B5(s))return false;
            var context=B5ProgressContext(s);
            int color=card.HasValue?(int)ProgressCatalog.Track(card.Value):context?.Card?.Color??-1;
            return color==1 && B2SkillEngine.CivilBlueImmune(s.ThreeKingdoms,player,useId??context?.EventId);
        }
        private int B5ProgressFactor(GameState s,string source,string target) =>
            B5(s)?B2SkillEngine.CivilProgressMultiplier(CardHost(s),source,target):1;
        private void B5ProgressTargeted(GameState s,Command c,string target)
        {
            if(!B5(s)||c.Kind!=CommandKind.PlayProgressCard)return;
            B2SkillEngine.OnCivilProgressTargeted(CardHost(s),c.Id,c.PlayerId,target,(int)ProgressCatalog.Track(c.ProgressCard),B5ProgressHasEffectivePart(s,c,target));
            B2SkillEngine.Pump(CardHost(s));
        }
        private static bool B5ProgressHasEffectivePart(GameState s,Command c,string target)
        {
            var p=s.Players.Single(x=>x.Id==target);
            if(B2Protected(s,target)||B3Immune(s,target))return false;
            switch(c.ProgressCard)
            {
                case ProgressCardKind.Espionage:return p.ProgressCards.Length>0&&!B2SkillEngine.IsB4HandLossForbidden(s.ThreeKingdoms,target);
                case ProgressCardKind.Taxation:case ProgressCardKind.Wedding:return ProgressHandSize(p)>0&&!B2SkillEngine.IsB4HandLossForbidden(s.ThreeKingdoms,target);
                case ProgressCardKind.Sabotage:return ProgressHandSize(p)>=2&&!B2SkillEngine.IsB4HandLossForbidden(s.ThreeKingdoms,target);
                case ProgressCardKind.Encouragement:return s.Knights.Any(k=>k.PlayerId==target&&!k.Active);
                case ProgressCardKind.Treason:case ProgressCardKind.Intrigue:return s.Knights.Any(k=>k.PlayerId==target);
                case ProgressCardKind.Diplomacy:return s.Roads.Any(r=>r.PlayerId==target&&r.LocationId==c.TargetId);
                default:return true;
            }
        }
        private void B5ReportProgressChanges(GameState s,Command c,
            System.Collections.Generic.Dictionary<string,int[]> hands,B5BoardAsset[] assets,
            System.Collections.Generic.Dictionary<string,ProgressCardKind[]> progress,
            System.Collections.Generic.Dictionary<string,string> tiles)
        {
            if(!B5(s))return;
            var context=B5ProgressContext(s);if(context?.Card==null)return;
            if(c.Kind!=CommandKind.PlayProgressCard && c.Kind!=CommandKind.ResolveProgressChoice && c.Kind!=CommandKind.CommercialHarborOffer && c.Kind!=CommandKind.MoveRobber && c.Kind!=CommandKind.StealResource)return;
            var host=CardHost(s);var after=host.GetBoardAssets();
            var changedTiles=s.Tiles.Where(t=>tiles[t.Id]!=t.Resource+":"+t.Number).Select(t=>t.Id).ToArray();
            foreach(var player in s.Players)
            {
                if(player.Id==context.PlayerId)continue;
                bool handChanged=!hands[player.Id].SequenceEqual(host.GetHand(player.Id)) || !progress[player.Id].SequenceEqual(player.ProgressCards);
                bool boardChanged=Json.Write(assets.Where(a=>a.PlayerId==player.Id).ToArray())!=Json.Write(after.Where(a=>a.PlayerId==player.Id).ToArray());
                bool tileChanged=changedTiles.Length>0&&host.GetProducingTileIds(player.Id).Intersect(changedTiles).Any();
                if(handChanged||boardChanged||tileChanged)B2SkillEngine.OnB5ProgressAffected(host,context.EventId,context.PlayerId,player.Id,context.Card.Color);
            }
        }
    }
}
