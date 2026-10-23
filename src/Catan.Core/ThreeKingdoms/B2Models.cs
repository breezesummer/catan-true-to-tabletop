using System;
using System.Runtime.Serialization;

namespace Catan.Core.ThreeKingdoms
{
    // 八种真实手牌依次为木、砖、羊、麦、矿、布、币、纸；宿主保持此顺序。
    public interface IThreeKingdomsCardHost
    {
        ThreeKingdomsState State { get; }
        int[] GetHand(string playerId);
        int[] GetBank();
        void RemoveHand(string playerId, int[] cards);
        void AddHand(string playerId, int[] cards);
        void RemoveBank(int[] cards);
        void AddBank(int[] cards);
        int DrawRandom(int exclusiveMaximum);
        B2ProgressCard[] GetProgressCards(string playerId);
        int GetProgressDeckCount(int color);
        void RemoveProgress(string playerId, string cardId);
        B2ProgressCard DrawProgress(string playerId, int color);
        void ReturnProgress(B2ProgressCard card);
        B2HarvestTile[] GetHarvestTiles(string playerId);
        int FinalDiceTotal { get; }
    }
    [DataContract] public sealed class B2ProgressCard
    {
        [DataMember(Order=1)] public string Id { get; set; }
        [DataMember(Order=2)] public int Color { get; set; }
        [DataMember(Order=3)] public bool PublicPoint { get; set; }
    }
    [DataContract] public sealed class B2HarvestTile
    {
        [DataMember(Order=1)] public string TileId { get; set; }
        [DataMember(Order=2)] public int Number { get; set; }
        [DataMember(Order=3)] public int[] AllowedCardTypes { get; set; } = Array.Empty<int>();
    }
    [DataContract] public sealed class B2SkillCommand
    {
        [DataMember(Order=1)] public string SkillId { get; set; }
        [DataMember(Order=2)] public string Option { get; set; }
        [DataMember(Order=3)] public string[] TargetPlayerIds { get; set; } = Array.Empty<string>();
        [DataMember(Order=4)] public int[] Counts { get; set; } = new int[8];
        [DataMember(Order=5)] public string[] TileIds { get; set; } = Array.Empty<string>();
        [DataMember(Order=6)] public string ProgressCardId { get; set; }
        [DataMember(Order=7)] public string DecisionId { get; set; }
        [DataMember(Order=8)] public string WindowId { get; set; }
        [DataMember(Order=9)] public int CardType { get; set; } = -1;
    }
    [DataContract] public sealed class B2SkillAction
    {
        [DataMember(Order=1)] public string Label { get; set; }
        [DataMember(Order=2)] public B2SkillCommand Command { get; set; }
    }
    [DataContract] public sealed class B2SkillDecision
    {
        [DataMember(Order=1)] public string PlayerId { get; set; }
        [DataMember(Order=2)] public string DecisionId { get; set; }
        [DataMember(Order=3)] public string WindowId { get; set; }
        [DataMember(Order=4)] public string Title { get; set; }
        [DataMember(Order=5)] public string Description { get; set; }
        [DataMember(Order=6)] public string Kind { get; set; }
        [DataMember(Order=7)] public string FrameId { get; set; }
    }
    [DataContract] public sealed class B2SkillFrame
    {
        [DataMember(Order=1)] public string Id { get; set; }
        [DataMember(Order=2)] public string SkillId { get; set; }
        [DataMember(Order=3)] public string PlayerId { get; set; }
        [DataMember(Order=4)] public string TenureId { get; set; }
        [DataMember(Order=5)] public string Kind { get; set; }
        [DataMember(Order=6)] public int Step { get; set; }
        [DataMember(Order=7)] public int Remaining { get; set; }
        [DataMember(Order=8)] public int[] Cards { get; set; } = new int[8];
        [DataMember(Order=9)] public string[] Targets { get; set; } = Array.Empty<string>();
        [DataMember(Order=10)] public B2ProgressCard ProgressCard { get; set; }
        [DataMember(Order=11)] public B2HarvestTile[] Tiles { get; set; } = Array.Empty<B2HarvestTile>();
        [DataMember(Order=12)] public bool Optional { get; set; }
        [DataMember(Order=13)] public string SourceEventId { get; set; }
        [DataMember(Order=14)] public int[] SelectedCards { get; set; } = new int[8];
        [DataMember(Order=15)] public bool Started { get; set; }
    }
    [DataContract] public sealed class B2TimedEffect
    {
        [DataMember(Order=1)] public string Id { get; set; }
        [DataMember(Order=2)] public string Kind { get; set; }
        [DataMember(Order=3)] public string PlayerId { get; set; }
        [DataMember(Order=4)] public string TargetPlayerId { get; set; }
        [DataMember(Order=5)] public long OpportunityId { get; set; }
        [DataMember(Order=6)] public long CreatedTurnId { get; set; }
    }
    [DataContract] public sealed class B2PlayerLedger
    {
        [DataMember(Order=1)] public string PlayerId { get; set; }
        [DataMember(Order=2)] public long TurnId { get; set; }
        [DataMember(Order=3)] public int LostHandCards { get; set; }
    }
    [DataContract] public sealed class B2HarvestAllocation
    {
        [DataMember(Order=1)] public string PlayerId { get; set; }
        [DataMember(Order=2)] public int[] Cards { get; set; } = new int[8];
        [DataMember(Order=3)] public bool Completed { get; set; }
        [DataMember(Order=4)] public int ActuallyReceived { get; set; }
        [DataMember(Order=5)] public string TenureId { get; set; }
        [DataMember(Order=6)] public string GeneralId { get; set; }
        [DataMember(Order=7)] public int GoldCount { get; set; }
        [DataMember(Order=8)] public bool AqueductEligible { get; set; }
        [DataMember(Order=9)] public bool ChoiceComplete { get; set; }
        [DataMember(Order=10)] public string ReplacementKind { get; set; }
        [DataMember(Order=11)] public int[] ForgoneCards { get; set; } = new int[8];
        [DataMember(Order=12)] public string[] Targets { get; set; } = Array.Empty<string>();
    }
    [DataContract] public sealed class B2SkillState
    {
        [DataMember(Order=1)] public B2SkillDecision PendingDecision { get; set; }
        [DataMember(Order=2)] public B2SkillFrame[] Frames { get; set; } = Array.Empty<B2SkillFrame>();
        [DataMember(Order=3)] public B2TimedEffect[] TimedEffects { get; set; } = Array.Empty<B2TimedEffect>();
        [DataMember(Order=4)] public B2PlayerLedger[] LossLedger { get; set; } = Array.Empty<B2PlayerLedger>();
        [DataMember(Order=5)] public string[] ProcessedEvents { get; set; } = Array.Empty<string>();
        [DataMember(Order=6)] public B2HarvestAllocation[] Harvest { get; set; } = Array.Empty<B2HarvestAllocation>();
        [DataMember(Order=7)] public long HarvestTurnId { get; set; }
        [DataMember(Order=8)] public bool HarvestActive { get; set; }
        [DataMember(Order=9)] public int[] RevealedCards { get; set; } = new int[8];
        [DataMember(Order=10)] public string[] DeferredCapacityPlayers { get; set; } = Array.Empty<string>();
        [DataMember(Order=11)] public int HarvestStage { get; set; }
        [DataMember(Order=12)] public B2SkillFrame[] AfterGoldFrames { get; set; } = Array.Empty<B2SkillFrame>();
        [DataMember(Order=13)] public int ProcessedSkippedCount { get; set; }
    }
    [DataContract] public sealed class B2HandChange
    {
        [DataMember(Order=1)] public string PlayerId { get; set; }
        [DataMember(Order=2)] public int BeforeCount { get; set; }
        [DataMember(Order=3)] public int AfterCount { get; set; }
        [DataMember(Order=4)] public int LostCount { get; set; }
        // 仅交给其他玩家的数量；支付银行、被偷和弃置均为零。
        [DataMember(Order=5)] public int GivenCount { get; set; }
    }
}
