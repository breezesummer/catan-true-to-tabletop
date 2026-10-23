using System;
using System.Runtime.Serialization;

namespace Catan.Core.ThreeKingdoms
{
    public enum B3DicePurpose { Formal, Judgment, Comparison }

    [DataContract] public sealed class B3DiceModification
    {
        [DataMember(Order=1)] public string PlayerId { get; set; }
        [DataMember(Order=2)] public string TenureId { get; set; }
        [DataMember(Order=3)] public string PaymentEventId { get; set; }
        [DataMember(Order=4)] public int Attempt { get; set; }
        [DataMember(Order=5)] public int CandidateIndex { get; set; }
        [DataMember(Order=6)] public int DieIndex { get; set; }
        [DataMember(Order=7)] public int CardType { get; set; }
        [DataMember(Order=8)] public string TileId { get; set; }
        [DataMember(Order=9)] public int Before { get; set; }
        [DataMember(Order=10)] public int After { get; set; }
        [DataMember(Order=11)] public bool Applied { get; set; }
    }

    [DataContract] public sealed class B3DiceCandidate
    {
        [DataMember(Order=1)] public int Attempt { get; set; }
        [DataMember(Order=2)] public int Index { get; set; }
        [DataMember(Order=3)] public int OriginalRed { get; set; }
        [DataMember(Order=4)] public int OriginalWhite { get; set; }
        [DataMember(Order=5)] public int OriginalEventFace { get; set; }
        [DataMember(Order=6)] public int Red { get; set; }
        [DataMember(Order=7)] public int White { get; set; }
        [DataMember(Order=8)] public int EventFace { get; set; }
    }

    [DataContract] public sealed class B3DiceResult
    {
        [DataMember(Order=1)] public string EventId { get; set; }
        [DataMember(Order=2)] public string PlayerId { get; set; }
        [DataMember(Order=3)] public B3DicePurpose Purpose { get; set; }
        [DataMember(Order=4)] public int Red { get; set; }
        [DataMember(Order=5)] public int White { get; set; }
        // 与城市骑士一致：1–3 黑色，4 贸易，5 政治，6 科学；判定为 0。
        [DataMember(Order=6)] public int EventFace { get; set; }
        [DataMember(Order=7)] public bool ActualRoll { get; set; }
        [DataMember(Order=8)] public bool NumericActualRoll { get; set; }
        [DataMember(Order=9)] public bool EventActualRoll { get; set; }
        [DataMember(Order=10)] public int Attempt { get; set; }
        public int Total => Red + White;
    }

    [DataContract] public sealed class B3DiceRoll
    {
        [DataMember(Order=1)] public string EventId { get; set; }
        [DataMember(Order=2)] public string PlayerId { get; set; }
        [DataMember(Order=3)] public B3DicePurpose Purpose { get; set; }
        [DataMember(Order=4)] public string Stage { get; set; }
        [DataMember(Order=5)] public int Attempt { get; set; } = 1;
        [DataMember(Order=6)] public int CandidateCount { get; set; } = 1;
        [DataMember(Order=7)] public int CandidateCursor { get; set; }
        [DataMember(Order=8)] public B3DiceCandidate[] Candidates { get; set; } = Array.Empty<B3DiceCandidate>();
        [DataMember(Order=9)] public int[] SelectedIndices { get; set; } = new[] { -1, -1, -1 };
        [DataMember(Order=10)] public int ChoiceCursor { get; set; }
        [DataMember(Order=11)] public int FixedRed { get; set; }
        [DataMember(Order=12)] public int FixedWhite { get; set; }
        [DataMember(Order=13)] public string[] Responders { get; set; } = Array.Empty<string>();
        [DataMember(Order=14)] public string[] ResponderTenures { get; set; } = Array.Empty<string>();
        [DataMember(Order=15)] public int ResponderCursor { get; set; }
        [DataMember(Order=16)] public int ConsecutivePasses { get; set; }
        [DataMember(Order=17)] public B3DiceResult Result { get; set; }
        [DataMember(Order=18)] public bool Submitted { get; set; }
        [DataMember(Order=19)] public string ObserverTenure { get; set; }
        [DataMember(Order=20)] public B3DiceModification[] Modifications { get; set; } = Array.Empty<B3DiceModification>();
        [DataMember(Order=21)] public long TurnInstanceId { get; set; }
    }

    [DataContract] public sealed class B3DiceState
    {
        [DataMember(Order=1)] public long OpenedTurnId { get; set; } = -1;
        [DataMember(Order=2)] public long FormalRollTurnId { get; set; } = -1;
        [DataMember(Order=3)] public string[] FixedResponders { get; set; } = Array.Empty<string>();
        [DataMember(Order=4)] public string[] FixedResponderTenures { get; set; } = Array.Empty<string>();
        [DataMember(Order=5)] public int FixedResponderCursor { get; set; }
        [DataMember(Order=6)] public bool StartWindowOpen { get; set; }
        [DataMember(Order=7)] public B3DiceResult FixedResult { get; set; }
        [DataMember(Order=8)] public B3DiceRoll Roll { get; set; }
        [DataMember(Order=9)] public B2SkillDecision PendingDecision { get; set; }
    }
}
