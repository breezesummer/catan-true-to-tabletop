using System;
using System.Linq;
using System.Runtime.Serialization;

namespace Catan.Core.ThreeKingdoms
{
    public static class B45SelectionMetadata
    {
        public static B2SkillAction WithBoard(B2SkillAction action,B5BoardOption option)
        {
            if(option.AssetKind!="Improvement")action.SelectionBoardIds=new[]{option.SourceId,option.TargetId}.Where(id=>!string.IsNullOrEmpty(id)).Distinct().ToArray();
            if(action.Command.Option=="civil-qixi-tech"){action.SelectionCost=new int[8];action.SelectionCost[1]=option.Level;}
            return action;
        }
        [DataContract] sealed class NativeTargets
        {
            [DataMember] public string TargetId {get;set;}
            [DataMember] public string SourceId {get;set;}
            [DataMember] public string[] TargetIds {get;set;}
        }
        public static void AnnotateDice(IThreeKingdomsCardHost host,B2SkillAction[] actions)
        {
            if(!GeneralCatalog.IsAutomaticPackage(host.State?.PackageVersion))return;
            var roll=host.State.Dice?.Roll;
            foreach(var action in actions??Array.Empty<B2SkillAction>())
            {
                var c=action.Command;
                if(c.Option=="modify")
                {action.SelectionDiceIndex=c.Counts[0];action.SelectionDiceValue=host.GetHarvestTiles(host.State.Dice.PendingDecision.PlayerId).FirstOrDefault(t=>t.TileId==c.TileIds.FirstOrDefault())?.Number;}
                else if(c.Option=="select"&&roll!=null)
                {
                    var candidate=roll.Candidates.FirstOrDefault(x=>x.Index==c.CardType);if(candidate==null)continue;
                    action.SelectionDiceIndex=roll.ChoiceCursor;
                    action.SelectionDiceValue=roll.ChoiceCursor==0?candidate.Red:roll.ChoiceCursor==1?candidate.White:candidate.EventFace;
                }
                else if(c.Option=="fixed") {action.SelectionDiceIndex=0;action.SelectionDiceValue=c.Counts[0];}
            }
        }
        // 只对已经过规则验证的用途增加公开稳定目标，不把原生载荷或私密数据传给客户端。
        public static void Annotate(IThreeKingdomsCardHost host,B2SkillAction[] actions,string viewerId)
        {
            if(!GeneralCatalog.IsAutomaticPackage(host.State?.PackageVersion))return;
            B4VirtualUse[] virtualUses=null;B4TokenMove[] moves=null;
            foreach(var action in actions??Array.Empty<B2SkillAction>())
            {
                var c=action.Command;
                if(c.Option=="response-liuli"&&host is IThreeKingdomsB4ResponseHost response)
                {
                    if(moves==null)moves=response.GetTokenMoves(viewerId);
                    var move=moves.FirstOrDefault(m=>m.OptionId==c.ProgressCardId);
                    if(move!=null){action.SelectionBoardIds=new[]{move.TileId};action.SelectionPlayerIds=string.IsNullOrEmpty(move.VictimPlayerId)?Array.Empty<string>():new[]{move.VictimPlayerId};}
                }
                if(action.VirtualCardId!=null&&host is IThreeKingdomsB4Host virtualHost)
                {
                    if(virtualUses==null)virtualUses=virtualHost.GetVirtualProgressUses(viewerId,(c.Option=="response-qice-use"||c.Option=="b45-optional-qice")?"prepare":"play");
                    var use=virtualUses.FirstOrDefault(u=>u.OptionId==c.ProgressCardId);if(use==null)continue;
                    action.SelectionPlayerIds=use.TargetPlayerIds?.ToArray()??Array.Empty<string>();
                    if(!string.IsNullOrEmpty(use.PayloadJson))
                    {
                        var native=Json.Read<NativeTargets>(use.PayloadJson);
                        action.SelectionBoardIds=new[]{native.SourceId,native.TargetId}.Concat(native.TargetIds??Array.Empty<string>()).Where(id=>!string.IsNullOrEmpty(id)).Distinct().ToArray();
                    }
                }
            }
        }
    }
}
