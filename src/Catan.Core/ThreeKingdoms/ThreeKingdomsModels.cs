using System;
using System.Runtime.Serialization;

namespace Catan.Core.ThreeKingdoms
{
    public enum ThreeKingdomsPhase { OpeningSelection, InitialPlacement, Preparation, Judgment, Roll, Draw, Play, End, RoundEnd, Finished }
    public enum ThreeKingdomsDecisionKind { SelectOpening, DeployOpening, SelectReplacement, DeployReplacement }
    public enum GeneralRegion { Pool, Candidate, ReservedDeployment, Active, Hidden, Retained }

    [DataContract] public sealed class GeneralCounter
    {
        [DataMember(Order=1)] public string Key { get; set; }
        [DataMember(Order=2)] public int Value { get; set; }
        [DataMember(Order=3)] public long TurnInstanceId { get; set; }
    }
    [DataContract] public sealed class GeneralInstance
    {
        [DataMember(Order=1)] public string InstanceId { get; set; }
        [DataMember(Order=2)] public string GeneralId { get; set; }
        [DataMember(Order=3)] public GeneralRegion Region { get; set; }
        [DataMember(Order=4)] public string OwnerPlayerId { get; set; }
        [DataMember(Order=5)] public string TenureId { get; set; }
        [DataMember(Order=6)] public int RemainingLife { get; set; }
        [DataMember(Order=7)] public int EnteredRound { get; set; }
        [DataMember(Order=8)] public int LastLifeDecrementRound { get; set; }
        [DataMember(Order=9)] public GeneralCounter[] Counters { get; set; } = Array.Empty<GeneralCounter>();
    }
    [DataContract] public sealed class ThreeKingdomsPlayer
    {
        [DataMember(Order=1)] public string PlayerId { get; set; }
        [DataMember(Order=2)] public bool SelectionComplete { get; set; }
        [DataMember(Order=3)] public bool FaceDown { get; set; }
    }
    [DataContract] public sealed class ThreeKingdomsDecision
    {
        [DataMember(Order=1)] public ThreeKingdomsDecisionKind Kind { get; set; }
        [DataMember(Order=2)] public string PlayerId { get; set; }
        [DataMember(Order=3)] public string DecisionId { get; set; }
        [DataMember(Order=4)] public string WindowId { get; set; }
        [DataMember(Order=5)] public string[] CandidateIds { get; set; } = Array.Empty<string>();
        [DataMember(Order=6)] public int RequiredCount { get; set; }
        [DataMember(Order=7)] public int CandidateCount { get; set; }
    }
    [DataContract] public sealed class ThreeKingdomsClock
    {
        [DataMember(Order=1)] public ThreeKingdomsPhase Phase { get; set; }
        [DataMember(Order=2)] public int Round { get; set; }
        [DataMember(Order=3)] public int NormalSeatCursor { get; set; }
        [DataMember(Order=4)] public string ActivePlayerId { get; set; }
        [DataMember(Order=5)] public long TurnInstanceId { get; set; }
        [DataMember(Order=6)] public long OpportunityId { get; set; }
        [DataMember(Order=7)] public long PhaseId { get; set; }
        [DataMember(Order=8)] public bool IsExtraTurn { get; set; }
        [DataMember(Order=9)] public string[] ExtraTurnQueue { get; set; } = Array.Empty<string>();
        [DataMember(Order=10)] public long NormalOpportunitiesConsumed { get; set; }
        [DataMember(Order=11)] public long ExtraOpportunitiesConsumed { get; set; }
        [DataMember(Order=12)] public string[] SkippedOpportunityPlayerIds { get; set; } = Array.Empty<string>();
        [DataMember(Order=13)] public long DrawPhaseTurnInstanceId { get; set; }
    }
    // B1 保存父子效果的精确步骤；B2 起由逐技能处理器消费，不执行卡面文字。
    [DataContract] public sealed class ThreeKingdomsEffectFrame
    {
        [DataMember(Order=1)] public string EffectId { get; set; }
        [DataMember(Order=2)] public string ParentEffectId { get; set; }
        [DataMember(Order=3)] public string RootEventId { get; set; }
        [DataMember(Order=4)] public string SourceTenureId { get; set; }
        [DataMember(Order=5)] public string SkillId { get; set; }
        [DataMember(Order=6)] public string PlayerId { get; set; }
        [DataMember(Order=7)] public int Step { get; set; }
        [DataMember(Order=8)] public bool CostPaid { get; set; }
        [DataMember(Order=9)] public string[] CompletedBranches { get; set; } = Array.Empty<string>();
        [DataMember(Order=10)] public string[] Targets { get; set; } = Array.Empty<string>();
        [DataMember(Order=11)] public long TurnInstanceId { get; set; }
        [DataMember(Order=12)] public long PhaseId { get; set; }
        [DataMember(Order=13)] public string ReturnWindowId { get; set; }
    }
    [DataContract] public sealed class ThreeKingdomsState
    {
        [DataMember(Order=1)] public string RulesVersion { get; set; }
        [DataMember(Order=2)] public string PackageVersion { get; set; }
        [DataMember(Order=3)] public string PackageContentHash { get; set; }
        [DataMember(Order=4)] public bool SkillsEnabled { get; set; }
        [DataMember(Order=5)] public GeneralInstance[] Generals { get; set; } = Array.Empty<GeneralInstance>();
        [DataMember(Order=6)] public ThreeKingdomsPlayer[] Players { get; set; } = Array.Empty<ThreeKingdomsPlayer>();
        [DataMember(Order=7)] public ThreeKingdomsClock Clock { get; set; } = new ThreeKingdomsClock();
        [DataMember(Order=8)] public ThreeKingdomsDecision PendingDecision { get; set; }
        [DataMember(Order=9)] public string WindowId { get; set; }
        [DataMember(Order=10)] public long NextId { get; set; } = 1;
        [DataMember(Order=11)] public bool OpeningComplete { get; set; }
        [DataMember(Order=12)] public int OpeningSeatCursor { get; set; }
        [DataMember(Order=13)] public bool ReplacementInProgress { get; set; }
        [DataMember(Order=14)] public ThreeKingdomsPhase ReplacementReturnPhase { get; set; }
        [DataMember(Order=15)] public string ReplacementPlayerId { get; set; }
        [DataMember(Order=16)] public string[] RoundEndTenureSnapshot { get; set; } = Array.Empty<string>();
        [DataMember(Order=17)] public string[] ExpiredPlayerQueue { get; set; } = Array.Empty<string>();
        [DataMember(Order=18)] public ThreeKingdomsEffectFrame[] EffectFrames { get; set; } = Array.Empty<ThreeKingdomsEffectFrame>();
        [DataMember(Order=19)] public string[] ProcessedTriggerIds { get; set; } = Array.Empty<string>();
        [DataMember(Order=20)] public uint InitialSeed { get; set; }
        [DataMember(Order=21)] public string RandomAlgorithmId { get; set; } = "xorshift32-rejection-v1";
        [DataMember(Order=22)] public string[] GeneralDrawOrder { get; set; } = Array.Empty<string>();
        [DataMember(Order=23)] public string[] SeatPlayerIds { get; set; } = Array.Empty<string>();
    }
    [DataContract] public sealed class ThreeKingdomsPlayerView
    {
        [DataMember(Order=1)] public string PlayerId { get; set; }
        [DataMember(Order=2)] public string ActiveGeneralId { get; set; }
        [DataMember(Order=3)] public string ActiveTenureId { get; set; }
        [DataMember(Order=4)] public int RemainingLife { get; set; }
        [DataMember(Order=5)] public int HiddenCount { get; set; }
        [DataMember(Order=6)] public string[] HiddenGeneralIds { get; set; } = Array.Empty<string>();
        [DataMember(Order=7)] public bool SelectionComplete { get; set; }
        [DataMember(Order=8)] public bool FaceDown { get; set; }
    }
    [DataContract] public sealed class ThreeKingdomsView
    {
        [DataMember(Order=1)] public string PackageVersion { get; set; }
        [DataMember(Order=2)] public string PackageContentHash { get; set; }
        [DataMember(Order=3)] public string PackageLabel { get; set; }
        [DataMember(Order=4)] public bool SkillsEnabled { get; set; }
        [DataMember(Order=5)] public string WindowId { get; set; }
        [DataMember(Order=6)] public ThreeKingdomsClock Clock { get; set; }
        [DataMember(Order=7)] public ThreeKingdomsDecision PendingDecision { get; set; }
        [DataMember(Order=8)] public ThreeKingdomsPlayerView[] Players { get; set; }
        [DataMember(Order=9)] public int PoolCount { get; set; }
        [DataMember(Order=10)] public bool OpeningComplete { get; set; }
        [DataMember(Order=11)] public bool CanRetire { get; set; }
    }
}
