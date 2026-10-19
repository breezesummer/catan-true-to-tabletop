using System;
using System.Linq;
using Catan.Core;
using Catan.Core.M4;
using UnityEngine;
using Command = Catan.Core.M4.Command;
using CommandKind = Catan.Core.M4.CommandKind;

public sealed partial class M4App
{
    object smithingView;
    string[][] smithingPlans;
    string[][] SmithingPlans()
    {
        if(!ReferenceEquals(smithingView,view))
        {
            smithingView=view;
            smithingPlans=TabletopSmithingSelection.Plans(view.Board.Knights.Where(k=>k.PlayerId==seat).Select(k=>k.LocationId).ToArray(),ids=>
            {var c=New(CommandKind.PlayProgressCard);c.ProgressCard=ProgressCardKind.Smithing;c.TargetIds=ids;return Preview(c).Success;});
        }
        return smithingPlans;
    }
    bool DirectProgressActive => view!=null && (draft!=null && draft.Kind==CommandKind.PlayProgressCard &&
        new[]{ProgressCardKind.Engineering,ProgressCardKind.Medicine,ProgressCardKind.Invention,ProgressCardKind.Smithing,ProgressCardKind.Merchant,ProgressCardKind.Diplomacy,ProgressCardKind.Intrigue,ProgressCardKind.Taxation,ProgressCardKind.Treason}.Contains(draft.ProgressCard)
        || view.PendingDecision!=null && view.PendingDecision.PlayerId==seat && new[]{"PillageCity","Metropolis","DisplacedKnight","ProgressTreasonRemove","ProgressTreasonPlace"}.Contains(view.PendingDecision.Kind)
        && view.LegalActions.Any(c=>!c.Decline&&!string.IsNullOrEmpty(c.TargetId)));
    Command DirectDecisionCommand(Command candidate)
    {
        if(candidate==null)return null;
        var command=New(candidate.Kind,candidate.TargetId);
        command.KnightLevel=candidate.KnightLevel;
        command.Decline=candidate.Decline;
        return command;
    }
    void BeginDirectProgress()
    {
        if(!DirectProgressActive)return;
        status=draft.ProgressCard==ProgressCardKind.Treason?"点选对手的骑士以选择该玩家；由对手决定移除哪名骑士。":"请直接点选地图上的目标；右键或 Esc 取消。";
        ai.ShowTablePrompt(status);
    }
    void PreviewDirectTarget(string target,string card,Command command,bool legal)
    {
        var knight=view.Board.Knights.FirstOrDefault(k=>k.LocationId==target);
        int level=knight?.Level??view.PendingDecision?.DisplacedKnight?.Level??command?.KnightLevel??view.PendingDecision?.KnightLevel??1;
        bool active=knight?.Active??view.PendingDecision?.DisplacedKnight?.Active??view.PendingDecision?.KnightActive??false;
        if(card=="Smithing")level++;
        board.PreviewPiece(target,TabletopPointerPreview.ForEffect(card,view.PendingDecision?.Kind,level,active),seat,legal);
    }
    void UpdateDirectProgress(Vector2 mouse,bool click)
    {
        bool card=draft!=null&&draft.Kind==CommandKind.PlayProgressCard;
        var kind=card?draft.ProgressCard:default(ProgressCardKind);
        string type=card&&(kind==ProgressCardKind.Merchant||kind==ProgressCardKind.Taxation||kind==ProgressCardKind.Invention)?"T":card&&kind==ProgressCardKind.Diplomacy?"E":"V";
        string target=Pick(type,mouse);
        if(string.IsNullOrEmpty(target)){board.ClearPreview();return;}
        Command command=null;
        if(!card)command=DirectDecisionCommand(view.LegalActions.FirstOrDefault(c=>!c.Decline&&c.TargetId==target));
        else
        {
            command=New(CommandKind.PlayProgressCard,target);command.ProgressCard=kind;
            if(kind==ProgressCardKind.Treason){var knight=view.Board.Knights.FirstOrDefault(k=>k.LocationId==target&&k.PlayerId!=seat);if(knight==null)command=null;else command.OtherPlayerId=knight.PlayerId;}
            if(kind==ProgressCardKind.Invention)
            {
                command.SourceId=draft.SourceId;
                if(string.IsNullOrEmpty(draft.SourceId))
                {
                    var tile=view.Board.Tiles.FirstOrDefault(t=>t.Id==target);
                    bool eligible=tile!=null&&new[]{3,4,5,9,10,11}.Contains(tile.Number??0);
                    board.PreviewPiece(target,TabletopPointerPreview.ForEffect("Invention"),seat,eligible);
                    if(click&&eligible){draft.SourceId=target;status="已选第一枚数字，点选第二枚数字交换。";ai.ShowTablePrompt(status);}return;
                }
            }
            if(kind==ProgressCardKind.Smithing)command.TargetIds=new[]{target};
        }
        if(card&&kind==ProgressCardKind.Smithing)
        {
            var plan=SmithingPlans().FirstOrDefault(ids=>ids.Contains(target)&&(string.IsNullOrEmpty(draft.SourceId)||target!=draft.SourceId&&ids.Contains(draft.SourceId)));
            command=plan==null?null:New(CommandKind.PlayProgressCard);
            if(command!=null){command.ProgressCard=kind;command.TargetIds=plan;}
        }
        bool valid=command!=null&&Preview(command).Success;
        PreviewDirectTarget(target,card?kind.ToString():null,command,valid);
        if(!click)return;
        if(!valid){ai.ShowTablePrompt("此目标不符合卡牌效果，请选择其他目标。");return;}
        if(!card&&view.PendingDecision.Kind=="ProgressTreasonPlace")
        {
            var options=view.LegalActions.Where(c=>!c.Decline&&c.TargetId==target).Select(DirectDecisionCommand).ToArray();
            mapChoice.Open(board.ScreenPoint(target),"选择安置骑士等级",options.Select(c=>c.KnightLevel+" 级骑士").ToArray(),i=>Submit(options[i]));return;
        }
        if(card&&kind==ProgressCardKind.Smithing&&command.TargetIds.Length==2&&string.IsNullOrEmpty(draft.SourceId))
        {
            draft.SourceId=target;status="已选第一名骑士，请点选第二名可晋升骑士。";ai.ShowTablePrompt(status);return;
        }
        Submit(command);
    }
}
