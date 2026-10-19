using System;

namespace Catan.Core
{
    /// <summary>仅供本机或权威端 AI 决策的对手手牌投影；不包含具体发展卡、进步卡或牌库。</summary>
    public sealed class OpponentHandView
    {
        public string PlayerId { get; internal set; }
        public ResourceBag Resources { get; internal set; }
        public CommodityHandCounts Commodities { get; internal set; } = new CommodityHandCounts();
        public int DevelopmentCardCount { get; internal set; }
        /// <summary>按贸易（黄色）、政治（蓝色）、科学（绿色）的顺序计数，只含手中尚未打出的牌。</summary>
        public int[] ProgressCardCounts { get; internal set; } = new int[3];
    }

    /// <summary>跨规则包的商品计数，顺序与城市与骑士的 Commodity 一致。</summary>
    public sealed class CommodityHandCounts
    {
        public int Cloth { get; internal set; }
        public int Coin { get; internal set; }
        public int Paper { get; internal set; }
        public int Total => Cloth + Coin + Paper;
        public int this[int commodity]
        {
            get
            {
                switch (commodity)
                {
                    case 0: return Cloth;
                    case 1: return Coin;
                    case 2: return Paper;
                    default: throw new ArgumentOutOfRangeException(nameof(commodity));
                }
            }
        }
    }
}
