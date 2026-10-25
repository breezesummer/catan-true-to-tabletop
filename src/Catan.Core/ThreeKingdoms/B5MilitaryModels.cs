using System;
using System.Runtime.Serialization;

namespace Catan.Core.ThreeKingdoms
{
    [DataContract] public sealed class B5PlayerCount
    {
        [DataMember(Order=1)] public string PlayerId { get; set; }
        [DataMember(Order=2)] public int Value { get; set; }
        [DataMember(Order=3)] public long TurnId { get; set; }
        [DataMember(Order=4)] public string TenureId { get; set; }
    }
    [DataContract] public sealed class B5TileHarvest
    {
        [DataMember(Order=1)] public string PlayerId { get; set; }
        [DataMember(Order=2)] public string TileId { get; set; }
        [DataMember(Order=3)] public string EventId { get; set; }
    }
    [DataContract] public sealed class B5RevealedHand
    {
        [DataMember(Order=1)] public string PlayerId { get; set; }
        [DataMember(Order=2)] public int[] Cards { get; set; } = new int[8];
        [DataMember(Order=3)] public bool Public { get; set; }
    }
    /// <summary>玩家永久效果与武将任次分开；比大小挂起区属于规则状态，不依赖界面。</summary>
    [DataContract] public sealed class B5MilitaryState
    {
        [DataMember(Order=1)] public string[] PermanentShanqiPlayers { get; set; } = Array.Empty<string>();
        [DataMember(Order=2)] public string[] PermanentYaowuPlayers { get; set; } = Array.Empty<string>();
        [DataMember(Order=3)] public B5PlayerCount[] TurnHandSnapshots { get; set; } = Array.Empty<B5PlayerCount>();
        [DataMember(Order=4)] public B5PlayerCount[] PublicHands { get; set; } = Array.Empty<B5PlayerCount>();
        [DataMember(Order=5)] public B5PlayerCount[] DelayedDeployments { get; set; } = Array.Empty<B5PlayerCount>();
        [DataMember(Order=6)] public B5PlayerCount[] HuoshouEnds { get; set; } = Array.Empty<B5PlayerCount>();
        [DataMember(Order=7)] public B5PlayerCount[] ScoreAdjustments { get; set; } = Array.Empty<B5PlayerCount>();
        [DataMember(Order=8)] public B5TileHarvest[] DoubleHarvest { get; set; } = Array.Empty<B5TileHarvest>();
        [DataMember(Order=9)] public long ImmediateInvasionTurnId { get; set; } = -1;
        [DataMember(Order=10)] public B2SkillFrame ComparisonSource { get; set; }
        [DataMember(Order=11)] public B2SkillFrame[] SuspendedFrames { get; set; } = Array.Empty<B2SkillFrame>();
        [DataMember(Order=12)] public bool SuspendedHarvest { get; set; }
    }
}
