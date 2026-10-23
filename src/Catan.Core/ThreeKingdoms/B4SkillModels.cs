using System;
using System.Runtime.Serialization;

namespace Catan.Core.ThreeKingdoms
{
    public interface IThreeKingdomsB4VirtualCostHost
    { bool CanUseVirtualProgressAfterPayment(string playerId, B4VirtualUse use, int[] consumedCards); }
    /// <summary>宿主返回已通过具体牌规则校验的完整用途；技能核心只支付实体费用并调用一次。</summary>
    public interface IThreeKingdomsB4Host : IThreeKingdomsCardHost
    {
        int GetHandLimit(string playerId);
        B4VirtualUse[] GetVirtualProgressUses(string playerId, string timing);
        void UseVirtualProgress(string playerId, B4VirtualUse use);
    }
    [DataContract] public sealed class B4VirtualUse
    {
        [DataMember(Order=1)] public string CardId { get; set; }
        [DataMember(Order=2)] public string OptionId { get; set; }
        [DataMember(Order=3)] public string Label { get; set; }
        [DataMember(Order=4)] public string[] TargetPlayerIds { get; set; } = Array.Empty<string>();
        [DataMember(Order=5)] public int CardType { get; set; } = -1;
        [DataMember(Order=6)] public string PayloadJson { get; set; }
        [DataMember(Order=7)] public string SourceSkillId { get; set; }
        [DataMember(Order=8)] public string SourceEventId { get; set; }
        [DataMember(Order=9)] public int RewardRandomCount { get; set; }
    }
    /// <summary>仅保存选择与后续步骤；这里的牌数量仍属于玩家手牌，不属于临时实体区。</summary>
    [DataContract] public sealed class B4SkillPayload
    {
        [DataMember(Order=1)] public int[] Selection { get; set; } = new int[8];
        [DataMember(Order=2)] public int[] OtherSelection { get; set; } = new int[8];
        [DataMember(Order=3)] public int DeclaredType { get; set; } = -1;
        [DataMember(Order=4)] public string OwnerPlayerId { get; set; }
        [DataMember(Order=5)] public string[] LockedTargets { get; set; } = Array.Empty<string>();
        [DataMember(Order=6)] public B4VirtualUse VirtualUse { get; set; }
    }
}
