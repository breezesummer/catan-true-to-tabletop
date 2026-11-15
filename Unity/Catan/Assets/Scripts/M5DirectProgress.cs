using System;
using System.Linq;
using Catan.Core;
using Catan.Core.ThreeKingdoms;
using Catan.Core.M5;
using UnityEngine;
using Command = Catan.Core.M5.Command;
using CommandKind = Catan.Core.M5.CommandKind;

public sealed partial class M5App
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
        smithingSelected=smithingSelected.Contains(target)?smithingSelected.Where(id=>id!=target).ToArray():(string[])command.TargetIds.Clone();
        ai.ShowTablePrompt(status="请选择要晋升的骑士");
    }
    bool DirectProgressActive => view!=null && (draft!=null && draft.Kind==CommandKind.PlayProgressCard &&
        !ImmediateProgress(draft.ProgressCard)
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
    static bool ImmediateProgress(ProgressCardKind card)
    {
        switch(card)
        {
            case ProgressCardKind.Irrigation:case ProgressCardKind.Mining:case ProgressCardKind.RoadBuilding:
            case ProgressCardKind.CommercialHarbor:case ProgressCardKind.Encouragement:case ProgressCardKind.Wedding:
            case ProgressCardKind.Sabotage:return true;
            default:return false;
        }
    }

    bool SeatProgress=>draft?.Kind==CommandKind.PlayProgressCard&&(draft.ProgressCard==ProgressCardKind.Espionage||
        draft.ProgressCard==ProgressCardKind.GuildDues||draft.ProgressCard==ProgressCardKind.Treason);
    bool OfferingHarbor=>draft?.Kind==CommandKind.CommercialHarborOffer;
    bool CanOfferHarbor=>draft==null&&view?.CommercialHarborPlayers?.Length>0&&view.ActivePlayerId==seat&&view.PendingDecision==null&&view.Phase==Catan.Core.M5.GamePhase.Action&&view.TradeOffer==null&&!operationActions&&!dock.HasOverlay;
    static readonly Rect HarborEntry=new Rect(665,618,110,39);
    int harborOrdinal=-1;
    int harborResponseGood=-1;
    readonly OperationSelection privateProgressSelection=new OperationSelection();
    object privateProgressView;
    Vector2 privateProgressScroll;
    bool GuildDuesResponse=>view?.PendingDecision?.PlayerId==seat&&view.PendingDecision.Kind=="ProgressGuildDues";
    bool SmithingPicking=>draft?.Kind==CommandKind.PlayProgressCard&&draft.ProgressCard==ProgressCardKind.Smithing;
    bool AlchemyPicking=>draft?.Kind==CommandKind.PlayProgressCard&&draft.ProgressCard==ProgressCardKind.Alchemy;
    void RotateAlchemyDie(int index,int value)
    {
        if(!AlchemyPicking)return;
        if(index==0)draft.ChosenDice1=value;else draft.ChosenDice2=value;
    }
    bool ProgressConfirmActive=>AlchemyPicking||HarborResponse||GuildDuesResponse||SmithingPicking;
    bool ProgressConfirmReady=>Operations().CanInteract&&(HarborResponse?harborOrdinal>=0&&CanSelectHarborGood(harborResponseGood,harborOrdinal):
        GuildDuesResponse?privateProgressSelection.Selected.Sum()==view.PendingDecision.Amount:
        AlchemyPicking||SmithingPicking&&TabletopSmithingSelection.CanConfirm(SmithingPlans(),smithingSelected));
    void SyncProgressConfirmation()
    {
        if(GuildDuesResponse&&!ReferenceEquals(privateProgressView,view))
        {
            privateProgressView=view;
            privateProgressSelection.Sync(new OperationSnapshot{Scope="大商家:"+seat,Seat=seat,Revision=view.Turn+":"+view.Events.Length,
                Goods=DockGoods(view.PrivateTargetResources,view.PrivateTargetCommodities)});
        }
        if(!GuildDuesResponse){privateProgressView=null;privateProgressSelection.Clear();}
        if(HarborResponse&&!ReferenceEquals(harborResponseView,view))
        {harborResponseView=view;harborOrdinal=-1;harborResponseGood=-1;dock.FocusGeneralHand(false);}
        if(!HarborResponse){harborResponseView=null;harborResponseGood=-1;}
        tabletop.BindAlchemy(AlchemyPicking&&!tabletop.OperationHidden?draft.ChosenDice1:view.AlchemyDice1,
            AlchemyPicking&&!tabletop.OperationHidden?draft.ChosenDice2:view.AlchemyDice2,
            AlchemyPicking&&!tabletop.OperationHidden?(Action<int,int>)RotateAlchemyDie:null);
        tabletop.BindProgressConfirmation(ProgressConfirmActive&&!ai.ThreeKingdomsSelectionActive?(Action)ConfirmProgressSelection:null,ProgressConfirmActive&&ProgressConfirmReady);
    }
    void ConfirmProgressSelection()
    {
        if(!ProgressConfirmActive||!ProgressConfirmReady)return;
        if(AlchemyPicking){Submit(draft);return;}
        if(SmithingPicking)
        {var c=New(CommandKind.PlayProgressCard);c.ProgressCard=ProgressCardKind.Smithing;c.TargetIds=(string[])smithingSelected.Clone();Submit(c);return;}
        var command=New(CommandKind.ResolveProgressChoice);
        if(HarborResponse)command.Commodity=(Commodity)(harborResponseGood-5);
        else for(int t=0;t<8;t++){if(t<5)command.Resources[(Resource)t]=privateProgressSelection.Selected[t];else command.Commodities[(Commodity)(t-5)]=privateProgressSelection.Selected[t];}
        Submit(command);
    }
    void DrawProgressConfirmation()
    {
        if(!ProgressConfirmActive||AlchemyPicking)return;
        if(GeneralFrameUI.Choice(M6App.SkillCancelRect,"确认",ProgressConfirmReady,true))ConfirmProgressSelection();
    }
    object harborResponseView;
    OperationHandSelection harborHand;
    bool HarborResponse=>view?.PendingDecision?.PlayerId==seat&&view.PendingDecision?.Kind=="ProgressHarbor"&&
        view.LegalActions.Any(c=>c.Kind==CommandKind.ResolveProgressChoice&&!c.Decline);
    OperationHandSelection ProgressHandSelection()
    {
        if(!OfferingHarbor&&!HarborResponse)return null;
        if(HarborResponse&&!ReferenceEquals(harborResponseView,view))
        {harborResponseView=view;dock.FocusGeneralHand(false);}
        return harborHand??(harborHand=new OperationHandSelection{
            Active=true,CanSelectGood=CanSelectHarborGood,SelectGood=SelectHarborGood,
            IsGoodSelected=(good,index)=>harborOrdinal==index&&(HarborResponse?harborResponseGood==good:OfferingHarbor&&(int)draft.Resource==good),
            SelectedGoods=()=>harborOrdinal>=0?1:0
        });
    }
    bool CanSelectHarborGood(int good,int ordinal)
    {
        if(OfferingHarbor)return good<5&&view.LegalActions.Any(c=>c.Kind==CommandKind.CommercialHarborOffer&&(int)c.Resource==good);
        return HarborResponse&&good>=5&&view.LegalActions.Any(c=>!c.Decline&&c.Kind==CommandKind.ResolveProgressChoice&&(int)c.Commodity==good-5);
    }
    void SelectHarborGood(int good,int ordinal)
    {
        if(!CanSelectHarborGood(good,ordinal))return;
        if(HarborResponse)
        {
            harborOrdinal=harborOrdinal==ordinal&&harborResponseGood==good?-1:ordinal;harborResponseGood=good;return;
        }
        bool same=harborOrdinal==ordinal&&draft.Resource==(Resource)good;
        harborOrdinal=same?-1:ordinal;draft.Resource=(Resource)good;
        ai.ShowTablePrompt(status=same?"点选要送出的资源":"点选收取资源的玩家");
    }
    int? harborPlayedTurn;
    int priorHarborOffers;
    void BeginPlayedHarbor()
    {
        if(!harborPlayedTurn.HasValue||view==null)return;
        if(view.Turn!=harborPlayedTurn||view.ActivePlayerId!=seat){harborPlayedTurn=null;return;}
        if(!CanOfferHarbor||view.CommercialHarborPlayers.Length<=priorHarborOffers||ai==null||
            !ai.IsHumanTurn||ai.TablePresentationBlocked||ai.TableMenuOpen||ai.ThreeKingdomsSelectionActive)return;
        harborPlayedTurn=null;
        if(view.LegalActions.Any(c=>c.Kind==CommandKind.CommercialHarborOffer))StartHarborOffer();
    }
    void StartHarborOffer()
    {
        CancelOperationDraft();Select(CommandKind.CommercialHarborOffer);harborOrdinal=-1;dock.FocusGeneralHand(false);
        ai.ShowTablePrompt(status="点选要送出的资源");
    }
    bool EligibleProgressSeat(string player)=>OfferingHarbor?harborOrdinal>=0&&view.LegalActions.Any(c=>c.Kind==CommandKind.CommercialHarborOffer&&c.OtherPlayerId==player&&c.Resource==draft.Resource):SeatProgress&&view.LegalActions.Any(c=>c.Kind==CommandKind.PlayProgressCard&&
        c.ProgressCard==draft.ProgressCard&&c.OtherPlayerId==player);
    bool PickProgressSeat(string player)
    {
        if(!SeatProgress&&!OfferingHarbor)return false;
        if(EligibleProgressSeat(player)){draft.OtherPlayerId=player;Submit(draft);}
        return true;
    }

    bool DropProgressCard(OperationHandCard card,float x,float y)
    {
        var point=new Vector2(x,y);
        if(!TabletopScene.BoardArea.Contains(point)||ai.TablePresentationBlocked||ai.ThreeKingdomsSelectionActive)return false;
        var player=tabletop.PlayerPanelAt(point);
        if(player==null&&DockBlocksMouse(point))return false;
        var kind=(ProgressCardKind)card.ArtIndex;
        if(player!=null&&kind!=ProgressCardKind.Espionage&&kind!=ProgressCardKind.GuildDues&&kind!=ProgressCardKind.Treason)return false;
        PrepareProgressCard(card);
        if(player!=null)PickProgressSeat(player);
        else if(DirectProgressActive&&!ProgressChoiceRow)UpdateDirectProgress(MobileTabletop.ScreenPoint(point),true);
        return true;
    }

    object outlineView,outlineDraft;
    string outlineField,outlineSource;
    string[] nativePieceTargets=Array.Empty<string>();
    void LateUpdate()
    {
        if(board==null||view==null||ai==null||!ai.IsHumanTurn||ai.TableMenuOpen)return;
        if(ai.ThreeKingdomsSkillSelectionActive){
            foreach(var id in ai.GeneralPieceTargets())board.OutlinePiece(id,ai.GeneralTargetSelected(id));
            return;
        }
        if(DirectProgressActive){
            if(!ProgressChoiceRow)foreach(var id in ProgressTargets())board.OutlinePiece(id,id==draft?.SourceId||smithingSelected.Contains(id));
            return;
        }
        if(draft==null||string.IsNullOrEmpty(pickField))return;
        if(outlineView!=view||outlineDraft!=draft||outlineField!=pickField||outlineSource!=draft.SourceId){
            outlineView=view;outlineDraft=draft;outlineField=pickField;outlineSource=draft.SourceId;
            nativePieceTargets=draft.Kind==CommandKind.BuildCity?view.LegalCityVertexIds:view.LegalActions
                .Where(a=>a.Kind==draft.Kind&&(pickField=="source"||string.IsNullOrEmpty(draft.SourceId)||a.SourceId==draft.SourceId)
                    &&(a.Kind!=CommandKind.PromoteKnight||DockKnightTargetMatches(a.TargetId)))
                .Select(a=>pickField=="source"?a.SourceId:a.TargetId).Where(id=>id!=null).Distinct().ToArray();
        }
        foreach(var id in nativePieceTargets)board.OutlinePiece(id);
    }
    object marksView,marksDraft;
    string[] marksSelection,marks=Array.Empty<string>();
    string marksSource;
    string[] ProgressTargets()
    {
        if(ReferenceEquals(view,marksView)&&ReferenceEquals(draft,marksDraft)&&ReferenceEquals(smithingSelected,marksSelection)&&marksSource==draft?.SourceId)return marks;
        marksView=view;marksDraft=draft;marksSource=draft?.SourceId;
        var actions=view.LegalActions.AsEnumerable();
        if(draft?.Kind!=CommandKind.PlayProgressCard)
            marks=actions.Where(c=>!c.Decline&&!string.IsNullOrEmpty(c.TargetId)).Select(c=>c.TargetId).Distinct().ToArray();
        else if(draft.ProgressCard==ProgressCardKind.Invention)
            marks=view.Board.Tiles.Where(t=>t.Number==3||t.Number==4||t.Number==5||t.Number==9||t.Number==10||t.Number==11).Select(t=>t.Id).ToArray();
        else if(draft.ProgressCard==ProgressCardKind.Treason)
            marks=view.Board.Knights.Where(k=>EligibleProgressSeat(k.PlayerId)).Select(k=>k.LocationId).ToArray();
        else if(draft.ProgressCard==ProgressCardKind.Smithing)
            {
            var plans=SmithingPlans();
            var next=AllianceSmithingB6?plans.Where(p=>p.Length==smithingSelected.Length+1&&smithingSelected.All(p.Contains)).SelectMany(p=>p):
                TabletopSmithingSelection.NextTargets(plans,smithingSelected);
            marks=smithingSelected.Concat(next).Distinct().ToArray();
        }
        else marks=actions.Where(c=>c.Kind==CommandKind.PlayProgressCard&&c.ProgressCard==draft.ProgressCard&&!string.IsNullOrEmpty(c.TargetId)).Select(c=>c.TargetId).Distinct().ToArray();
        marksSelection=smithingSelected;
        return marks;
    }
    void DrawProgressMarks()
    {
        if(Event.current.type!=EventType.Repaint||tabletop.OperationHidden||ProgressChoiceRow)return;
        bool numbers=draft?.Kind==CommandKind.PlayProgressCard&&draft.ProgressCard==ProgressCardKind.Invention;
        foreach(var target in ProgressTargets())
        {
            if(board.HasOutlinePiece(target))continue;
            var point=MobileTabletop.Logical(board.ScreenPoint(target));
            bool chosen=target==draft?.SourceId||draft?.ProgressCard==ProgressCardKind.Smithing&&smithingSelected.Contains(target);
            var number=numbers?view.Board.Tiles.First(t=>t.Id==target).Number:null;
            if(!numbers&&board.DrawTileSelection(target,chosen))continue;
            ProgressTableUI.Mark(point,chosen,number);
        }
    }
    bool ProgressChoiceRow
    {
        get
        {
            if(view==null)return false;
            if(draft?.Kind==CommandKind.PlayProgressCard)
            {
                switch(draft.ProgressCard)
                {
                    case ProgressCardKind.Crane:case ProgressCardKind.Alchemy:
                    case ProgressCardKind.ResourceMonopoly:case ProgressCardKind.TradeMonopoly:case ProgressCardKind.MerchantFleet:return true;
                }
            }
            if(view.PendingDecision?.PlayerId!=seat)return false;
            switch(view.PendingDecision.Kind)
            {
                case "TribeProgress":case "DefenderProgress":return true;
                case "ProgressHarbor":return HarborResponse;
                case "ProgressEspionage":return view.PrivateTargetProgressCards?.Length>0;
                case "ProgressGuildDues":return view.PrivateTargetResources!=null;
                default:return false;
            }
        }
    }
    void PickProgressFacility(int track)
    {
        if(draft?.Kind!=CommandKind.PlayProgressCard||draft.ProgressCard!=ProgressCardKind.Crane)
        {if(!DirectProgressActive&&!ProgressChoiceRow)dock.OpenFacility(track);return;}
        if(view.LegalActions.Any(c=>c.Kind==CommandKind.PlayProgressCard&&c.ProgressCard==ProgressCardKind.Crane&&(int)c.Track==track))
        {draft.Track=(ImprovementTrack)track;Submit(draft);}
    }
    void DrawProgressChoiceRow()
    {
        var pending=view.PendingDecision;
        if(pending?.PlayerId==seat&&(pending.Kind=="DefenderProgress"||pending.Kind=="TribeProgress"))
        {
            for(int i=0;i<3;i++)
            {
                var track=(ImprovementTrack)i;
                bool legal=view.LegalActions.Any(c=>c.Kind==CommandKind.ResolveChoice&&c.Track==track);
                if(!ProgressTableUI.Card(i,3,EntityCardAssets.ProgressBack(i),legal))continue;
                var command=New(CommandKind.ResolveChoice);command.Track=track;Submit(command);return;
            }
            return;
        }
        if(pending!=null&&pending.PlayerId==seat&&pending.Kind=="ProgressEspionage")
        {
            var cards=view.PrivateTargetProgressCards.Distinct().ToArray();
            for(int i=0;i<cards.Length;i++)
            {
                var card=cards[i];
                if(ProgressTableUI.Card(i,cards.Length+1,EntityCardAssets.ProgressFront((int)card)))
                {var take=New(CommandKind.ResolveProgressChoice);take.SelectedProgressCard=card;Submit(take);return;}
            }
            if(ModernOperationUI.Button(ProgressTableUI.ButtonArea(cards.Length,cards.Length+1),"跳过"))
            {var skip=New(CommandKind.ResolveProgressChoice);skip.Decline=true;Submit(skip);}
            return;
        }
        if(pending!=null&&pending.PlayerId==seat&&pending.Kind=="ProgressGuildDues")
        {
            var goods=DockGoods(view.PrivateTargetResources,view.PrivateTargetCommodities);
            int count=Enumerable.Range(0,8).Where(t=>view.ThreeKingdoms?.BlockedGainCardTypes?.Contains(t)!=true).Sum(t=>goods[t]);
            privateProgressScroll=GUI.BeginScrollView(ProgressTableUI.Area,privateProgressScroll,new Rect(0,0,Mathf.Max(ProgressTableUI.Area.width-20,count*144),210));
            int index=0;
            for(int good=0;good<8;good++)for(int ordinal=0;ordinal<goods[good];ordinal++)
            {
                if(view.ThreeKingdoms?.BlockedGainCardTypes?.Contains(good)==true)continue;
                bool selected=privateProgressSelection.IsCardSelected(good,ordinal);
                var rect=new Rect(Mathf.Max(0,(ProgressTableUI.Area.width-20-count*144)/2)+index++*144,selected?0:10,132,198);
                EntityCardAssets.DrawCardFace(rect,EntityCardAssets.ResourceOrCommodityFront(good));
                if(ThreeKingdomsDirectInput.Read(rect,Operations().CanInteract&&(selected||privateProgressSelection.Selected.Sum()<pending.Amount),false)==1)
                    privateProgressSelection.Toggle(good,ordinal,goods[good]);
            }
            GUI.EndScrollView();
            ai.DrawHandInstruction("请选择取得 "+pending.Amount+" 张牌");
            return;
        }
        if(HarborResponse)return;
        if(draft?.Kind!=CommandKind.PlayProgressCard)return;
        if(draft.ProgressCard==ProgressCardKind.Crane)
        {
            // 城市设施的常驻按钮就是三个轨道的选择入口。
            return;
        }
        if(AlchemyPicking)return;
        int start=draft.ProgressCard==ProgressCardKind.TradeMonopoly?5:0;
        int end=draft.ProgressCard==ProgressCardKind.ResourceMonopoly?5:8;
        for(int good=start;good<end;good++)
        {
            if(!ProgressTableUI.Card(good-start,end-start,EntityCardAssets.ResourceOrCommodityFront(good)))continue;
            var c=draft;
            c.ChooseCommodity=good>=5;
            if(good<5)c.Resource=(Resource)good;else c.Commodity=(Commodity)(good-5);
            Submit(c);return;
        }
    }
    void BeginDirectProgress()
    {
        if(!DirectProgressActive)return;
        status=TabletopSelectionPrompt.ProgressInstruction(draft.ProgressCard.ToString());
        ai.ShowTablePrompt(status);
    }
    void PreviewDirectTarget(string target,string card,Command command,bool legal)
    {
        var knight=view.Board.Knights.FirstOrDefault(k=>k.LocationId==target);
        int level=knight?.Level??view.PendingDecision?.DisplacedKnight?.Level??command?.KnightLevel??view.PendingDecision?.KnightLevel??1;
        bool active=knight?.Active??view.PendingDecision?.DisplacedKnight?.Active??view.PendingDecision?.KnightActive??false;
        if(card=="Smithing")level++;
        var preview=TabletopPointerPreview.ForEffect(card,view.PendingDecision?.Kind,level,active);
        ConfigureMetropolisPreview(preview,target);
        board.PreviewPiece(target,preview,seat,legal);
    }
    void UpdateDirectProgress(Vector2 mouse,bool click)
    {
        bool card=draft!=null&&draft.Kind==CommandKind.PlayProgressCard;
        var kind=card?draft.ProgressCard:default(ProgressCardKind);
        if(card&&(ProgressChoiceRow||SeatProgress&&kind!=ProgressCardKind.Treason)){board.ClearPreview();return;}
        string type=card&&(kind==ProgressCardKind.Merchant||kind==ProgressCardKind.Taxation||kind==ProgressCardKind.Invention)?"T":card&&kind==ProgressCardKind.Diplomacy?"E":"V";
        string target=type=="V"?board.BuildingAt(mouse):null;
        if(string.IsNullOrEmpty(target))target=Pick(type,mouse);
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
                if(command.SourceId==target)
                {
                    if(click){draft.SourceId=null;ai.ShowTablePrompt(status="点选第一枚点数");}
                    return;
                }
                if(string.IsNullOrEmpty(draft.SourceId))
                {
                    var tile=view.Board.Tiles.FirstOrDefault(t=>t.Id==target);
                    bool eligible=tile!=null&&new[]{3,4,5,9,10,11}.Contains(tile.Number??0);
                    board.PreviewPiece(target,TabletopPointerPreview.ForEffect("Invention"),seat,eligible);
                    if(click&&eligible){draft.SourceId=target;status="请选择第二枚数字圆片";ai.ShowTablePrompt(status);}return;
                }
            }
            if(kind==ProgressCardKind.Smithing)command.TargetIds=new[]{target};
        }
        if(card&&kind==ProgressCardKind.Smithing)
        {
            command=SmithingTarget(target);
        }
        bool valid=command!=null&&ProgressTargets().Contains(target);
        PreviewDirectTarget(target,card?kind.ToString():null,command,valid);
        if(!click)return;
        if(!valid)return;
        if(!card&&view.PendingDecision.Kind=="ProgressTreasonPlace")
        {
            var options=view.LegalActions.Where(c=>!c.Decline&&c.TargetId==target).Select(DirectDecisionCommand).ToArray();
            if(options.Length==1)Submit(options[0]);
            else mapChoice.Open(board.ScreenPoint(target),"骑士等级",options.Select(c=>c.KnightLevel+" 级").ToArray(),i=>Submit(options[i]));
            return;
        }
        if(card&&kind==ProgressCardKind.Smithing)
        {
            ChooseSmithingTarget(target,command);return;
        }
        Submit(command);
    }
}
