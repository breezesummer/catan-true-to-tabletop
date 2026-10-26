using System;

namespace Catan.Core.ThreeKingdoms
{
    public interface IThreeKingdomsB6SeatTradeHost : IThreeKingdomsCardHost
    {
        bool AreTerritoriesAdjacent(string firstPlayerId, string secondPlayerId);
        int[] JiaoziChoices(B4VirtualUse construction);
        void CompleteJiaoziConstruction(B4VirtualUse construction, int cardType);
    }
}
