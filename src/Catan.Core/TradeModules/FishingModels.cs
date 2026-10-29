using System;
using System.Linq;
using System.Runtime.Serialization;

namespace Catan.Core.TradeModules
{
    public enum FishAction { RemoveRobber, StealResource, TakeResource, BuildRoad, BuyDevelopmentCard }

    [DataContract]
    public sealed class FishingGround
    {
        [DataMember(Order=1)] public string Id { get; set; }
        [DataMember(Order=2)] public int Number { get; set; }
        [DataMember(Order=3)] public string[] VertexIds { get; set; }
        internal FishingGround Copy() => new FishingGround { Id = Id, Number = Number, VertexIds = VertexIds.ToArray() };
    }
    [DataContract]
    public sealed class FishHolding
    {
        [DataMember(Order=1)] public string PlayerId { get; set; }
        [DataMember(Order=2)] public string[] TokenIds { get; set; } = Array.Empty<string>();
        [DataMember(Order=3)] public bool ReplacementUsed { get; set; }
        internal FishHolding Copy() => new FishHolding { PlayerId = PlayerId, TokenIds = TokenIds.ToArray(), ReplacementUsed = ReplacementUsed };
    }
    [DataContract]
    public sealed class FishDrawClaim
    {
        [DataMember(Order=1)] public string PlayerId { get; set; }
        [DataMember(Order=2)] public string SourceId { get; set; }
        [DataMember(Order=3)] public string VertexId { get; set; }
        [DataMember(Order=4)] public int Count { get; set; }
        internal FishDrawClaim Copy() => (FishDrawClaim)MemberwiseClone();
    }
    /// <summary>仅权威持有；鱼池顺序、隐藏筹码及抽取游标不得直接发送。</summary>
    [DataContract]
    public sealed class FishingState
    {
        public const string CurrentVersion = "fishing-base-v001";
        [DataMember(Order=1)] public string Version { get; set; } = CurrentVersion;
        [DataMember(Order=2)] public string LakeTileId { get; set; }
        [DataMember(Order=3)] public FishingGround[] Grounds { get; set; }
        [DataMember(Order=4)] public string[] Supply { get; set; }
        [DataMember(Order=5)] public string[] Discard { get; set; } = Array.Empty<string>();
        [DataMember(Order=6)] public FishHolding[] Holdings { get; set; }
        [DataMember(Order=7)] public string OldBootPlayerId { get; set; }
        [DataMember(Order=8)] public FishDrawClaim[] Queue { get; set; } = Array.Empty<FishDrawClaim>();
        [DataMember(Order=9)] public int QueueIndex { get; set; }
        [DataMember(Order=10)] public int Remaining { get; set; }
        [DataMember(Order=11)] public string PendingPlayerId { get; set; }
        [DataMember(Order=12)] public string PendingDecisionId { get; set; }
        [DataMember(Order=13)] public long DecisionSequence { get; set; }
        [DataMember(Order=14)] public string MapVersion { get; set; } = FishingScenarioCatalog.CurrentVersion;
        internal FishingState Copy() => new FishingState
        {
            Version = Version, MapVersion = MapVersion, LakeTileId = LakeTileId, Grounds = Grounds.Select(g => g.Copy()).ToArray(),
            Supply = Supply.ToArray(), Discard = Discard.ToArray(), Holdings = Holdings.Select(h => h.Copy()).ToArray(),
            OldBootPlayerId = OldBootPlayerId, Queue = Queue.Select(q => q.Copy()).ToArray(), QueueIndex = QueueIndex,
            Remaining = Remaining, PendingPlayerId = PendingPlayerId, PendingDecisionId = PendingDecisionId, DecisionSequence = DecisionSequence
        };
    }
    public sealed class FishTokenView
    {
        public string Id { get; internal set; }
        public int Value { get; internal set; }
    }
    public sealed class FishPlayerView
    {
        public string PlayerId { get; internal set; }
        public int TokenCount { get; internal set; }
        public bool ReplacementUsed { get; internal set; }
    }
    public sealed class FishingView
    {
        public string Version { get; internal set; }
        public string MapVersion { get; internal set; }
        public string LakeTileId { get; internal set; }
        public int[] LakeNumbers { get; internal set; }
        public FishingGround[] Grounds { get; internal set; }
        public FishTokenView[] OwnTokens { get; internal set; }
        public FishPlayerView[] Players { get; internal set; }
        public int SupplyCount { get; internal set; }
        public int[] DiscardValues { get; internal set; }
        public string OldBootPlayerId { get; internal set; }
        public string PendingPlayerId { get; internal set; }
        public string PendingDecisionId { get; internal set; }
        public long StateSequence { get; internal set; }
        public int OwnVictoryTarget { get; internal set; }
    }
}
