using System;
using System.Runtime.Serialization;

namespace Catan.Core.ThreeKingdoms
{
    [DataContract] public sealed class B5CivilRelation
    {
        [DataMember(Order=1)] public string Kind { get; set; }
        [DataMember(Order=2)] public string PlayerId { get; set; }
        [DataMember(Order=3)] public string TargetPlayerId { get; set; }
        [DataMember(Order=4)] public string TenureId { get; set; }
    }
    [DataContract] public sealed class B5JianshuEffect
    {
        [DataMember(Order=1)] public string Id { get; set; }
        [DataMember(Order=2)] public string SourcePlayerId { get; set; }
        [DataMember(Order=3)] public string TenureId { get; set; }
        [DataMember(Order=4)] public string PlayerId { get; set; }
        [DataMember(Order=5)] public string OtherPlayerId { get; set; }
    }
    [DataContract] public sealed class B5CivilMarker
    {
        [DataMember(Order=1)] public string PlayerId { get; set; }
        [DataMember(Order=2)] public string Key { get; set; }
        [DataMember(Order=3)] public string Value { get; set; }
        [DataMember(Order=4)] public long TurnId { get; set; }
    }
    [DataContract] public sealed class B5CivilState
    {
        [DataMember(Order=1)] public B5CivilRelation[] Relations { get; set; } = Array.Empty<B5CivilRelation>();
        [DataMember(Order=2)] public B5JianshuEffect[] Jianshu { get; set; } = Array.Empty<B5JianshuEffect>();
        [DataMember(Order=3)] public B5CivilMarker[] Markers { get; set; } = Array.Empty<B5CivilMarker>();
    }
    // 只保存选择、预算和稳定ID；Selection仍在手中，不属于实体暂存牌。
    [DataContract] public sealed class B5CivilPayload
    {
        [DataMember(Order=1)] public string OwnerPlayerId { get; set; }
        [DataMember(Order=2)] public string RootEventId { get; set; }
        [DataMember(Order=3)] public string Argument { get; set; }
        [DataMember(Order=4)] public string BoardKind { get; set; }
        [DataMember(Order=5)] public int[] Selection { get; set; } = new int[8];
        [DataMember(Order=6)] public string[] SelectedIds { get; set; } = Array.Empty<string>();
        [DataMember(Order=7)] public int[] Tracks { get; set; } = new int[3];
        [DataMember(Order=8)] public int Declared { get; set; } = -1;
        [DataMember(Order=9)] public int Budget { get; set; }
        [DataMember(Order=10)] public B4VirtualUse VirtualUse { get; set; }
        [DataMember(Order=11)] public B5BoardOption BoardOption { get; set; }
        [DataMember(Order=12)] public string OtherPlayerId { get; set; }
    }
}
