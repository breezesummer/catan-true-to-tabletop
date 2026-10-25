using System.Linq;
using Catan.Core.ThreeKingdoms;

namespace Catan.Core.M5
{
    public sealed partial class CombinedGameSession
    {
        // 空城只免疫尚未执行的外来负效；不等同克己，也不豁免发展卡容量。
        private static bool B3Immune(GameState s, string player)
        {
            if (!B3(s) || !s.ThreeKingdoms.Generals.Any(g => g.Region == GeneralRegion.Active && g.OwnerPlayerId == player && g.GeneralId == "tk-g19")) return false;
            var owner = s.Players.Single(p => p.Id == player);
            return HandCount(owner) == 0;
        }

        private static bool B3PendingImmune(GameState s, PendingDecision decision)
        {
            if (decision == null) return false;
            if(B5(s))
            {
                var target=decision.Kind=="ProgressEspionage"||decision.Kind=="ProgressGuildDues"?decision.OtherPlayerId:
                    decision.Kind=="ProgressWedding"||decision.Kind=="ProgressSabotage"||decision.Kind=="ProgressTreasonRemove"?decision.PlayerId:null;
                if(target!=null&&B5ProgressImmune(s,target))return true;
            }
            if(B4(s))
            {
                var losing=decision.Kind=="ProgressHarbor"||decision.Kind=="ProgressWedding"||decision.Kind=="ProgressSabotage"?decision.PlayerId:
                    decision.Kind=="ProgressEspionage"||decision.Kind=="ProgressGuildDues"?decision.OtherPlayerId:null;
                if(losing!=null&&B2SkillEngine.IsB4HandLossForbidden(s.ThreeKingdoms,losing)) return true;
            }
            switch (decision.Kind)
            {
                case "PillageCity":
                case "ProgressTreasonRemove":
                case "ProgressWedding":
                case "ProgressSabotage": return B3Immune(s, decision.PlayerId);
                case "ProgressEspionage":
                case "ProgressGuildDues": return B3Immune(s, decision.OtherPlayerId);
                default: return false;
            }
        }
    }
}
