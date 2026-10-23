using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using Catan.Core.ThreeKingdoms;

namespace Catan.Core.M5
{
    public sealed partial class GameState
    {
        [DataMember(Order=314, EmitDefaultValue=false)] public B3NativePayment[] ThreeKingdomsNativePayments { get; set; }
        [DataMember(Order=315, EmitDefaultValue=false)] public B3SpecialContinuation ThreeKingdomsSpecialContinuation { get; set; }
    }
    public sealed partial class CombinedGameSession
    {
        private void RecordB3NativePayment(GameState s, PlayerState p, ResourceBag cost, int before)
        {
            var host = CardHost(s);
            var payment = new B3NativePayment { EventId = "tk-pay-" + s.ThreeKingdoms.NextId++, PlayerId = p.Id,
                Cards = Enumerable.Range(0, 8).Select(i => i < 5 ? cost[(Resource)i] : 0).ToArray(), BeforeCount = before, AfterCount = host.GetHand(p.Id).Sum() };
            s.ThreeKingdomsNativePayments = (s.ThreeKingdomsNativePayments ?? Array.Empty<B3NativePayment>()).Concat(new[] { payment }).ToArray();
            B2SkillEngine.HoldReturnedCards(host, p.Id, "payment", payment.EventId, payment.Cards);
            B2SkillEngine.OnCardsMoved(host, payment.EventId, new[] { new B2HandChange { PlayerId = p.Id, BeforeCount = before, AfterCount = payment.AfterCount, LostCount = cost.Total } });
        }

        private void ResumeB3SpecialContinuation(GameState s)
        {
            if (!B3(s) || B2Busy(s) || s.ThreeKingdoms.PendingDecision != null || s.ThreeKingdomsSpecialContinuation == null) return;
            var continuation = s.ThreeKingdomsSpecialContinuation; s.ThreeKingdomsSpecialContinuation = null;
            CompleteSpecialChanges(s, continuation.PlayerId, continuation.NewEdgeIds);
        }

        private void ValidateB3SpecialContinuation(GameState s)
        {
            if (s.ThreeKingdomsNativePayments != null) throw new InvalidDataException("原生支付快照未在命令结尾清理。");
            var continuation = s.ThreeKingdomsSpecialContinuation;
            if (continuation == null) return;
            if (!B3(s) || s.Special == null || !s.Players.Any(p => p.Id == continuation.PlayerId) || continuation.NewEdgeIds == null ||
                continuation.NewEdgeIds.Length == 0 || continuation.NewEdgeIds.Distinct().Count() != continuation.NewEdgeIds.Length ||
                continuation.NewEdgeIds.Any(edge => !s.Roads.Any(r => r.PlayerId == continuation.PlayerId && r.LocationId == edge) && !s.Ships.Any(r => r.PlayerId == continuation.PlayerId && r.LocationId == edge)))
                throw new InvalidDataException("海洋场景建造续步无效。");
        }
    }
}
