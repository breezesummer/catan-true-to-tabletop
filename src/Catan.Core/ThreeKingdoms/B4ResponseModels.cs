using System;
using System.Runtime.Serialization;

namespace Catan.Core.ThreeKingdoms
{
    public interface IThreeKingdomsB4ResponseHost : IThreeKingdomsB4Host
    {
        B4TokenMove[] GetTokenMoves(string playerId);
        void MoveToken(string playerId, B4TokenMove move);
        void ReceiveExtraHarvest(string playerId, int finalTotal, string eventId);
        void RequestEndPlay(string playerId);
        bool TryCaptureProgressCard(string fromPlayerId, string toPlayerId, B2ProgressCard card, string eventId);
    }
    [DataContract] public sealed class B4TokenMove
    {
        [DataMember(Order=1)] public string OptionId { get; set; }
        [DataMember(Order=2)] public string Label { get; set; }
        [DataMember(Order=3)] public string TokenKind { get; set; }
        [DataMember(Order=4)] public string TileId { get; set; }
        [DataMember(Order=5)] public string VictimPlayerId { get; set; }
        [DataMember(Order=6)] public string PayloadJson { get; set; }
    }
    [DataContract] public sealed class B4CaoResidual
    {
        [DataMember(Order=1)] public string PlayerId { get; set; }
        [DataMember(Order=2)] public string TenureId { get; set; }
        [DataMember(Order=3)] public int AcquiredCount { get; set; }
    }
    [DataContract] public sealed class B4PlayerPenalty
    {
        [DataMember(Order=1)] public string PlayerId { get; set; }
        [DataMember(Order=2)] public int Amount { get; set; }
    }
    [DataContract] public sealed class B4DeclarationBatch
    {
        [DataMember(Order=1)] public string EventId { get; set; }
        [DataMember(Order=2)] public string PlayerId { get; set; }
        [DataMember(Order=3)] public string TenureId { get; set; }
        [DataMember(Order=4)] public int[] RealTypes { get; set; } = Array.Empty<int>();
        [DataMember(Order=5)] public int[] EffectiveTypes { get; set; } = Array.Empty<int>();
        [DataMember(Order=6)] public string OperationJson { get; set; }
        [DataMember(Order=7)] public int CardCursor { get; set; }
        [DataMember(Order=8)] public string[] Responders { get; set; } = Array.Empty<string>();
        [DataMember(Order=9)] public string[] Challengers { get; set; } = Array.Empty<string>();
        [DataMember(Order=10)] public int ResponderCursor { get; set; }
        [DataMember(Order=11)] public bool Complete { get; set; }
        [DataMember(Order=12)] public bool Success { get; set; }
    }
    [DataContract] public sealed class B4ResponseState
    {
        [DataMember(Order=1)] public B4CaoResidual[] CaoResiduals { get; set; } = Array.Empty<B4CaoResidual>();
        [DataMember(Order=2)] public B4PlayerPenalty[] HandLimitPenalties { get; set; } = Array.Empty<B4PlayerPenalty>();
        [DataMember(Order=3)] public B2SkillFrame JudgmentSource { get; set; }
        [DataMember(Order=4)] public B2SkillFrame[] SuspendedFrames { get; set; } = Array.Empty<B2SkillFrame>();
        [DataMember(Order=5)] public bool SuspendedHarvest { get; set; }
        [DataMember(Order=6)] public B4DeclarationBatch Declaration { get; set; }
    }
}
