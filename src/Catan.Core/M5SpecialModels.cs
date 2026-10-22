using System;
using System.Linq;
using System.Runtime.Serialization;
namespace Catan.Core.M5
{
public sealed partial class GameState
{
[DataMember(Order=300, EmitDefaultValue=false)] public ScenarioState Special { get; set; }
}
public sealed partial class PlayerState
{
[DataMember(Order=300, EmitDefaultValue=false)] public int Cloth { get; set; }
[DataMember(Order=301, EmitDefaultValue=false)] public string WonderId { get; set; }
[DataMember(Order=302, EmitDefaultValue=false)] public int WonderLevel { get; set; }
}
[DataContract] public sealed class ScenarioState {
[DataMember(Order=1)] public M3.Port[] Ports {get;set;} = Array.Empty<M3.Port>();
[DataMember(Order=2)] public string[] HiddenResources {get;set;} = Array.Empty<string>();
[DataMember(Order=3)] public int[] HiddenNumbers {get;set;} = Array.Empty<int>();
[DataMember(Order=4)] public string[] BonusEdgeIds {get;set;} = Array.Empty<string>();
[DataMember(Order=5)] public string[] GiftCardEdgeIds {get;set;} = Array.Empty<string>();
[DataMember(Order=6)] public M3.Port[] GiftPorts {get;set;} = Array.Empty<M3.Port>();
[DataMember(Order=7)] public M3.VillageState[] Villages {get;set;} = Array.Empty<M3.VillageState>();
[DataMember(Order=8)] public int ClothSupply {get;set;} = 0;
[DataMember(Order=9)] public M3.FortressState[] Fortresses {get;set;} = Array.Empty<M3.FortressState>();
[DataMember(Order=10)] public M3.Port[] UnplacedPorts {get;set;} = Array.Empty<M3.Port>();
[DataMember(Order=11)] public int SetupPortIndex {get;set;} = 0;
[DataMember(Order=12)] public PendingDecision PortReturnDecision {get;set;} = null;
[DataMember(Order=13)] public string[] WinnerPlayerIds {get;set;} = Array.Empty<string>();
[DataMember(Order=14)] public int LastPirateStrength {get;set;} = 0;
[DataMember(Order=15)] public HeldPort[] HeldPorts {get;set;} = Array.Empty<HeldPort>();
[DataMember(Order=16)] public PendingDecision ResumeDecision {get;set;} = null;
[DataMember(Order=17)] public GamePhase ResumePhase {get;set;} = GamePhase.Action;
[DataMember(Order=18)] public bool PirateProductionResolved {get;set;}
internal ScenarioState CopyForTransaction()
{
    var copy=(ScenarioState)MemberwiseClone();
    // 数组容器以替换方式更新；只分离会原地修改的村庄、要塞与待决对象。
    copy.Villages=Villages.Select(v=>new M3.VillageState {VertexId=v.VertexId,Number=v.Number,Cloth=v.Cloth,TradingPlayerIds=v.TradingPlayerIds.ToArray()}).ToArray();
    copy.Fortresses=Fortresses.Select(f=>new M3.FortressState {PlayerId=f.PlayerId,VertexId=f.VertexId,Strength=f.Strength,BeachheadVertexId=f.BeachheadVertexId,StartingVertexId=f.StartingVertexId,InvasionStartingVertexId=f.InvasionStartingVertexId}).ToArray();
    copy.PortReturnDecision=PortReturnDecision?.CopyForTransaction();copy.ResumeDecision=ResumeDecision?.CopyForTransaction();return copy;
}
}
[DataContract] public sealed class HeldPort { [DataMember] public string PlayerId {get;set;} [DataMember] public M3.Port Port {get;set;} }
public sealed partial class PublicPlayer { public int Cloth {get;internal set;} public string WonderId {get;internal set;} public int WonderLevel {get;internal set;} }
public sealed partial class BoardView { public M3.VillageState[] Villages {get;internal set;} = Array.Empty<M3.VillageState>(); public M3.FortressState[] Fortresses {get;internal set;} = Array.Empty<M3.FortressState>(); public string[] BonusEdgeIds {get;internal set;} = Array.Empty<string>(); public string[] GiftCardEdgeIds {get;internal set;} = Array.Empty<string>(); public M3.Port[] GiftPorts {get;internal set;} = Array.Empty<M3.Port>(); }
public sealed partial class PlayerView { public M3.WonderDefinition[] Wonders {get;internal set;} = Array.Empty<M3.WonderDefinition>(); public M3.Port[] HeldPorts {get;internal set;} = Array.Empty<M3.Port>(); }
}
