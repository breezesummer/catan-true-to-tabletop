using System;
using System.Linq;
using Catan.Core;

namespace Catan.AI
{
    internal static class AiHeuristics
    {
        internal static readonly Resource[] Resources = (Resource[])Enum.GetValues(typeof(Resource));
        internal static ResourceBag Bag(int wood = 0, int brick = 0, int wool = 0, int wheat = 0, int ore = 0) =>
            new ResourceBag { Wood = wood, Brick = brick, Wool = wool, Wheat = wheat, Ore = ore };

        // Marginal value declines as a hand accumulates a resource. This is computed from
        // this seat's hand only; opposing hands are neither required nor reconstructed.
        internal static double TradeValue(ResourceBag hand, ResourceBag gain, ResourceBag cost)
        {
            return Resources.Sum(r => Marginal(hand[r], gain[r]) - Marginal(hand[r] - cost[r], cost[r]));
        }

        private static double Marginal(int held, int count)
        {
            double value = 0;
            for (var i = 0; i < count; i++) value += 1.0 + 2.0 / (1 + held + i);
            return value;
        }

        internal static bool LandEdge(Catan.Core.M3.BoardView board, Edge edge) =>
            board.Tiles.Any(t => t.Resource != "sea" && t.Resource != "fog" && edge.Vertices.All(t.Vertices.Contains));
    }
}
