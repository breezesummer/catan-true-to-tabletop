using System;
using System.Runtime.Serialization;

namespace Catan.Core.ThreeKingdoms
{
    /// <summary>已离开手牌、尚未回银行的真实牌；七点弃牌全部完成前保留各来源。</summary>
    [DataContract] public sealed class B3ReturnedCards
    {
        [DataMember(Order=1)] public string PlayerId { get; set; }
        [DataMember(Order=2)] public string Cause { get; set; }
        [DataMember(Order=3)] public string EventId { get; set; }
        [DataMember(Order=4)] public int[] Cards { get; set; } = new int[8];
    }

    /// <summary>原生命令内部的真实支付快照；独立失牌事件已经提交，外层不得按净变化再记一次。</summary>
    [DataContract] public sealed class B3NativePayment
    {
        [DataMember(Order=1)] public string EventId { get; set; }
        [DataMember(Order=2)] public string PlayerId { get; set; }
        [DataMember(Order=3)] public int[] Cards { get; set; } = new int[8];
        [DataMember(Order=4)] public int BeforeCount { get; set; }
        [DataMember(Order=5)] public int AfterCount { get; set; }
        public B3NativePayment Copy() => new B3NativePayment { EventId = EventId, PlayerId = PlayerId, Cards = (int[])Cards.Clone(), BeforeCount = BeforeCount, AfterCount = AfterCount };
    }

    /// <summary>已提交建造之后尚未处理的海洋剧本步骤；不再次支付或再次放置建筑。</summary>
    [DataContract] public sealed class B3SpecialContinuation
    {
        [DataMember(Order=1)] public string PlayerId { get; set; }
        [DataMember(Order=2)] public string[] NewEdgeIds { get; set; } = Array.Empty<string>();
        public B3SpecialContinuation Copy() => new B3SpecialContinuation { PlayerId = PlayerId, NewEdgeIds = (string[])NewEdgeIds.Clone() };
    }
}
