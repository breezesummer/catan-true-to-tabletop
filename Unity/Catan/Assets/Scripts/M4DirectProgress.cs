using System;
using System.Linq;
using Catan.Core;
using Catan.Core.ThreeKingdoms;
using Catan.Core.M4;
using UnityEngine;
using Command = Catan.Core.M4.Command;
using CommandKind = Catan.Core.M4.CommandKind;

public sealed partial class M4App
{
    object smithingView;
    object smithingDraft;
    string[][] smithingPlans;
    string[] smithingSelected=Array.Empty<string>();
    bool AllianceSmithingB6=>view.ThreeKingdoms?.PackageVersion==GeneralCatalog.B6PackageVersion;
    string[][] SmithingPlans()
    {
        if(!ReferenceEquals(smithingView,view))
        {
            smithingView=view;
            int maxTargets=GeneralCatalog.UsesB5Package(view.ThreeKingdoms?.PackageVersion)&&view.ThreeKingdoms.Players.Any(p=>p.PlayerId==seat&&p.ActiveGeneralId=="tk-g32")?4:2;
            smithingPlans=AllianceSmithingB6?view.LegalActions.Where(c=>c.Kind==CommandKind.PlayProgressCard&&c.ProgressCard==ProgressCardKind.Smithing&&c.TargetIds?.Length>0).Select(c=>c.TargetIds).ToArray():TabletopSmithingSelection.Plans(view.Board.Knights.Where(k=>SharesAllianceAssetsB6(k.PlayerId)).Select(k=>k.LocationId).ToArray(),ids=>
            {var c=New(CommandKind.PlayProgressCard);c.ProgressCard=ProgressCardKind.Smithing;c.TargetIds=ids;return Preview(c).Success;},maxTargets);
        }
        if(!ReferenceEquals(smithingDraft,draft)||!TabletopSmithingSelection.CanConfirm(smithingPlans,smithingSelected))
        {smithingDraft=draft;smithingSelected=Array.Empty<string>();}
        return smithingPlans;
    }
    Command SmithingTarget(string target)
    {
        var plans=SmithingPlans();
        var selected=smithingSelected.Contains(target)?smithingSelected:smithingSelected.Concat(new[]{target}).ToArray();
        if(AllianceSmithingB6){selected=plans.FirstOrDefault(p=>p.Length==selected.Length&&p.All(selected.Contains));if(selected==null)return null;}
        else if(!TabletopSmithingSelection.CanConfirm(plans,selected))return null;
        var command=New(CommandKind.PlayProgressCard);command.ProgressCard=ProgressCardKind.Smithing;command.TargetIds=selected;return command;
    }
    void ChooseSmithingTarget(string target,Command command)
    {
        smithingSelected=(string[])command.TargetIds.Clone();
        bool more=AllianceSmithingB6?smithingPlans.Any(p=>p.Length>smithingSelected.Length&&smithingSelected.All(p.Contains)):TabletopSmithingSelection.NextTargets(smithingPlans,smithingSelected).Length>0;
        var choices=more?new[]{"确认晋升这 "+smithingSelected.Length+" 名骑士","继续点选骑士","重新选择"}:new[]{"确认晋升这 "+smithingSelected.Length+" 名骑士","重新选择"};
        var selectedDraft=draft;
        status="已选 "+smithingSelected.Length+" 名骑士；可以确认晋升"+(more?"或继续点选。":"。");ai.ShowTablePrompt(status);
        mapChoice.Open(board.ScreenPoint(target),"锻造：确认晋升数量",choices,index=>
        {
            if(!ReferenceEquals(draft,selectedDraft))return;
            if(index==0){Submit(command);return;}
            if(more&&index==1)status="已选 "+smithingSelected.Length+" 名骑士，请点选下一名；再次点选已选骑士可确认。";
            else{smithingSelected=Array.Empty<string>();status="请点选第一名可晋升骑士。";}
            ai.ShowTablePrompt(status);
        });
    }
    bool DirectProgressActive => view!=null && (draft!=null && draft.Kind==CommandKind.PlayProgressCard &&
        new[]{ProgressCardKind.Engineering,ProgressCardKind.Medicine,ProgressCardKind.Invention,ProgressCardKind.Smithing,ProgressCardKind.Merchant,ProgressCardKind.Diplomacy,ProgressCardKind.Intrigue,ProgressCardKind.Taxation,ProgressCardKind.Treason}.Contains(draft.ProgressCard)
        || view.PendingDecision!=null && view.PendingDecision.PlayerId==seat && TabletopSelectionPrompt.IsDirectMapDecision(view.PendingDecision.Kind)
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
        status=draft.ProgressCard==ProgressCardKind.Treason?"点选对手的骑士以选择该玩家；由对手决定移除哪名骑士。":draft.ProgressCard==ProgressCardKind.Smithing?"点选可晋升骑士；每次可确认当前数量，或继续选择下一名。":"请直接点选地图上的目标；右键或 Esc 取消。";
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
            command=SmithingTarget(target);
        }
        bool valid=command!=null&&(card&&kind==ProgressCardKind.Smithing||Preview(command).Success);
        PreviewDirectTarget(target,card?kind.ToString():null,command,valid);
        if(!click)return;
        if(!valid){ai.ShowTablePrompt("此目标不符合卡牌效果，请选择其他目标。");return;}
        if(!card&&view.PendingDecision.Kind=="ProgressTreasonPlace")
        {
            var options=view.LegalActions.Where(c=>!c.Decline&&c.TargetId==target).Select(DirectDecisionCommand).ToArray();
            mapChoice.Open(board.ScreenPoint(target),"选择安置骑士等级",options.Select(c=>c.KnightLevel+" 级骑士").ToArray(),i=>Submit(options[i]));return;
        }
        if(card&&kind==ProgressCardKind.Smithing)
        {
            ChooseSmithingTarget(target,command);return;
        }
        Submit(command);
    }
}
