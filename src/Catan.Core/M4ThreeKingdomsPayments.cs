using System;
using System.Linq;
using System.Runtime.Serialization;
using Catan.Core.ThreeKingdoms;

namespace Catan.Core.M4
{
    public sealed partial class GameState
    {
        [DataMember(Order=314, EmitDefaultValue=false)] public B3NativePayment[] ThreeKingdomsNativePayments { get; set; }
    }
    public sealed partial class CitiesKnightsGameSession
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
    }
}
