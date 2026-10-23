using System;
using System.Linq;
using Catan.Core.ThreeKingdoms;

namespace Catan.Core.M5
{
    public sealed partial class CombinedGameSession
    {
        // 只有最终结果进入既有正式事件、入侵和生产入口，候选与改点均不能调用此入口。
        private void FinishB3Dice(GameState s)
        {
            if (!B3(s) || s.ThreeKingdoms.PendingDecision != null || B2SkillEngine.HasPending(s.ThreeKingdoms)) return;
            var host = CardHost(s);
            B3DiceEngine.Pump(host);
            if (B2SkillEngine.ResumeB4Judgment(host)) return;
            var result = B3DiceEngine.TakeFinal(host);
            if (result == null) return;
            if (result.Purpose != B3DicePurpose.Formal)
                throw new InvalidOperationException("判定须返回来源技能，不能作为正式生产结算。");
            var before = s.Players.ToDictionary(p => p.Id, p => host.GetHand(p.Id));
            s.LastDice1 = result.Red; s.LastDice2 = result.White;
            s.LastEventDie = result.EventFace <= 3 ? EventDie.Barbarian : (EventDie)(result.EventFace - 3);
            CommitCitiesKnightsDice(s);
            NormalizeSpecialSeven(s);
            // 海盗剧本的即时失牌属于本次最终骰局，按真实流失批次处理。
            var changes = s.Players.Select(p => {
                var after = host.GetHand(p.Id);
                var lost = Enumerable.Range(0, 8).Select(i => Math.Max(0, before[p.Id][i] - after[i])).ToArray();
                return new B2HandChange { PlayerId = p.Id, BeforeCount = before[p.Id].Sum(), AfterCount = after.Sum(), LostCount = lost.Sum() };
            }).ToArray();
            if (changes.Any(c => c.LostCount > 0)) B2SkillEngine.OnCardsMoved(host, result.EventId + "-scenario", changes);
        }

        private static bool IsB3DiceCommit(GameState before, GameState after) =>
            B3(after) && after.ThreeKingdoms.Dice?.Roll?.Submitted == true &&
            (before.ThreeKingdoms?.Dice?.Roll?.Submitted != true ||
             before.ThreeKingdoms.Dice.Roll.EventId != after.ThreeKingdoms.Dice.Roll.EventId);

        private static string B3HistoryKind(GameState before, GameState after, Command c) =>
            !B3(after) ? c.Kind.ToString() : IsB3DiceCommit(before, after) ? "RollDice" :
            c.Kind == CommandKind.RollDice ? "DiceCandidate" : c.Kind.ToString();

        private static bool IsB3BankPayment(Command c, PendingDecision pending) =>
            c.Kind == CommandKind.BuildShip || c.Kind == CommandKind.BuildWonder || c.Kind == CommandKind.BuildRoad || c.Kind == CommandKind.BuildSettlement || c.Kind == CommandKind.BuildCity ||
            c.Kind == CommandKind.BuildWall || c.Kind == CommandKind.ImproveCity || c.Kind == CommandKind.RecruitKnight ||
            c.Kind == CommandKind.PromoteKnight || c.Kind == CommandKind.ActivateKnight || c.Kind == CommandKind.BankTrade ||
            c.Kind == CommandKind.PlayProgressCard && (c.ProgressCard == ProgressCardKind.Medicine || c.ProgressCard == ProgressCardKind.Engineering);
    }
}
