using System;
using System.Linq;
using Catan.Core;
using Catan.Core.M5;
using UnityEngine;
using CommandKind = Catan.Core.M5.CommandKind;
using GamePhase = Catan.Core.M5.GamePhase;
using PlayerView = Catan.Core.M5.PlayerView;

public sealed partial class M5App
{
    readonly OperationDock dock=new OperationDock();
    readonly TabletopUI tabletop=new TabletopUI();
    bool operationActions;
    bool handOverflowDiscard;
    string operationKnightTarget;
    string operationPortId;
    PlayerView operationView;
    OperationSnapshot operationSnapshot;
    int operationKnightLevel;
    bool operationFacilityLocked;
    bool DockBlocksMouse(Vector2? logicalMouse=null)
    {
        if(ai==null)return false;
        var p=logicalMouse??MobileTabletop.Logical(MobileTabletop.Pointer);
        return ThreeKingdomsPaymentContains(p)||SpecialBlocksMouse(p)||mapChoice.Active||ResourceChoiceVisible||ai.BlocksTableInput(p)||tabletop.BlocksBoardInput(p)||OperationContextVisible&&tabletop.CurrentContextRect.Contains(p)||dock.BlocksBoardInput(p);
    }
    void CancelOperationDraft(){CancelThreeKingdomsPayment();mapChoice.Clear();resourceChoiceSelection=new ResourceBag();if(!dock.TradeOpen)operationPortId=null;tabletop.SetContextAnchor(null);draft=null;pickField="";operationKnightLevel=0;operationFacilityLocked=false;operationActions=false;operationKnightTarget=null;if(board!=null){board.SetSelectedShip(null);board.ClearPreview();}}
    bool DirectTabletopPlacement=>draft!=null&&(draft.Kind==CommandKind.SetupSettlement||draft.Kind==CommandKind.SetupRoad||draft.Kind==CommandKind.BuildSettlement||draft.Kind==CommandKind.BuildRoad||draft.Kind==CommandKind.BuildCity||draft.Kind==CommandKind.BuildWall||draft.Kind==CommandKind.RecruitKnight||draft.Kind==CommandKind.PromoteKnight||draft.Kind==CommandKind.ActivateKnight||draft.Kind==CommandKind.ChaseRobber||draft.Kind==CommandKind.MoveRobber||draft.Kind==CommandKind.PlaceFreeRoad||draft.Kind==CommandKind.MoveKnight||draft.Kind==CommandKind.DisplaceKnight||draft.Kind==CommandKind.SetupShip||draft.Kind==CommandKind.BuildShip||draft.Kind==CommandKind.PlaceFreeShip||draft.Kind==CommandKind.MoveShip||draft.Kind==CommandKind.MovePirate||draft.Kind==CommandKind.ChasePirate);
    bool OperationContextVisible=>!directPayment.Active&&!tabletop.OperationHidden&&view!=null&&!DirectProgressActive&&(!FreeRoadPlacementActive||FreeRoadIds.Length==0&&FreeShipIds.Length==0)&&Operations().Discard==null&&ai.IsHumanTurn&&view.TradeOffer==null&&pickField==""&&(operationActions||view.Phase==GamePhase.Action&&view.CommercialHarborPlayers.Length>0||draft!=null&&!DirectTabletopPlacement||view.Phase==GamePhase.Discard||view.Phase==GamePhase.PendingChoice||view.Phase==GamePhase.RobberSteal||view.Phase==GamePhase.RoadBuilding&&view.LegalEdgeIds.Length==0&&view.LegalShipEdgeIds.Length==0||view.Phase==GamePhase.RobberMove);
    static int[] DockCost(int wood=0,int brick=0,int wool=0,int wheat=0,int ore=0)=>new[]{wood,brick,wool,wheat,ore,0,0,0};
    static int[] DockGoods(ResourceBag r,CommodityBag c)=>Enumerable.Range(0,8).Select(i=>i<5?r[(Resource)i]:c[(Commodity)(i-5)]).ToArray();
    bool DockKnightTargetMatches(string target)=>operationKnightLevel<2||draft==null||draft.Kind!=CommandKind.PromoteKnight||view.Board.Knights.Any(k=>k.PlayerId==seat&&k.LocationId==target&&k.Level==operationKnightLevel-1);
    int BankTradeRate(int good)
    {
        if(good>=5)return view.Players.First(p=>p.Id==seat).Improvements[0]>=3||view.MerchantFleetCommodities.Contains((Commodity)(good-5))?2:4;
        return view.MerchantFleetResources.Contains((Resource)good)||view.MerchantPlayerId==seat&&view.Board.Tiles.Any(t=>t.Id==view.MerchantTileId&&t.Resource==new[]{"wood","brick","wool","wheat","ore"}[good])?2:4;
    }
    bool DockLegal(CommandKind kind)=>view.LegalActions.Any(c=>c.Kind==kind);
    // 银行和港口分别展示各自权限；最终指令仍由同一规则入口裁决。
    OperationSnapshot TradeValidationSnapshot()
    {
        var source=Operations();
        if(!dock.IsPortTrade||operationPortId==null)return source;
        var locations=view.Board.Settlements.Concat(view.Board.Cities).Where(b=>b.PlayerId==seat).Select(b=>b.LocationId).ToArray();
        var port=view.Board.Ports.FirstOrDefault(p=>p.Id==operationPortId&&p.Vertices.Any(locations.Contains));
        if(port==null)return source;
        var result=new OperationSnapshot{CanTrade=source.CanTrade,CanInteract=source.CanInteract,GoodCount=source.GoodCount,Goods=source.Goods,Bank=source.Bank,OtherPlayers=source.OtherPlayers,Offer=source.Offer,ValidateTradeDraft=source.ValidateTradeDraft};
        for(int i=0;i<source.GoodCount;i++)result.Rates[i]=port.Resource.HasValue?((int)port.Resource.Value==i?2:0):3;
        return result;
    }
    bool PickTabletopPort(Vector2 mouse)
    {
        if(!Operations().CanTrade)return false;
        string id=board.ReferenceMarkers.PickPort(mouse,board.BoardCamera);
        if(string.IsNullOrEmpty(id))return false;
        var locations=view.Board.Settlements.Concat(view.Board.Cities).Where(b=>b.PlayerId==seat).Select(b=>b.LocationId).ToArray();
        var port=view.Board.Ports.FirstOrDefault(p=>p.Id==id);
        if(port==null)return false;
        if(!port.Vertices.Any(locations.Contains)){status="此港口未连接你的村庄或城市。";ai.ShowTablePrompt(status);return true;}
        CancelOperationDraft();
        int? good=port.Resource.HasValue?(int?)((int)port.Resource.Value):null;
        dock.OpenPortTrade(good,good.HasValue?Goods[good.Value]+"港口 · 2:1":"通用港口 · 3:1");
        operationPortId=id;return true;
    }
    internal bool VerificationOpenPort(string id)
    {
        if(view==null||!Operations().CanTrade)return false;
        var locations=view.Board.Settlements.Concat(view.Board.Cities).Where(b=>b.PlayerId==seat).Select(b=>b.LocationId).ToArray();
        var port=view.Board.Ports.FirstOrDefault(p=>p.Id==id&&p.Vertices.Any(locations.Contains));
        if(port==null)return false;
        CancelOperationDraft();dock.OpenPortTrade(port.Resource.HasValue?(int?)((int)port.Resource.Value):null,"港口兑换");operationPortId=id;return true;
    }
    OperationSnapshot Operations()
    {
        if(view==null)return null;
        if(operationView!=view)
        {
            operationView=view;var own=view.Players.First(p=>p.Id==seat);
            var s=new OperationSnapshot{Scope="M5:"+seat,Seat=seat,PublicSequence=view.Events.LastOrDefault()?.Sequence??0,Revision=view.Turn+":"+view.Phase+":"+view.Events.Length,Phase=PhaseName(view.Phase),RequiresSelection=TabletopSelectionPrompt.RequiredPhase(view.Phase.ToString()),GoodCount=8,Goods=DockGoods(view.OwnResources,view.OwnCommodities),Bank=DockGoods(view.Bank,view.CommodityBank),OtherPlayers=view.Players.Where(p=>p.Id!=seat).Select(p=>p.Id).ToArray()};
            s.CollectionTiles=TabletopCollectionSources.Project(view.Board.Tiles,view.Board.Settlements,view.Board.Cities,view.Board.RobberTileId,s.GoodCount==8);s.CollectionPosition=id=>{var point=MobileTabletop.Logical(board.ScreenPoint(id));return new TabletopCardPose{X=point.x,Y=point.y};};
            for(int i=0;i<8;i++)s.Rates[i]=BankTradeRate(i);
            bool setup=view.Phase==GamePhase.SetupSettlement,citySetup=setup&&view.Board.Settlements.Any(p=>p.PlayerId==seat),villageSetup=setup&&!citySetup;
            bool roadSetup=view.Phase==GamePhase.SetupRoad,freeRoad=view.Phase==GamePhase.RoadBuilding;
            AddDockBuild(s,villageSetup?CommandKind.SetupSettlement:CommandKind.BuildSettlement,"村庄","settlement",own.Pieces.Settlements,villageSetup?DockCost():DockCost(1,1,1,1),!citySetup&&view.LegalVertexIds.Length>0);
            AddDockBuild(s,roadSetup?CommandKind.SetupRoad:freeRoad?CommandKind.PlaceFreeRoad:CommandKind.BuildRoad,"道路","road",own.Pieces.Roads,roadSetup||freeRoad?DockCost():DockCost(1,1),view.LegalEdgeIds.Length>0);
            AddDockBuild(s,citySetup?CommandKind.SetupSettlement:CommandKind.BuildCity,"城市","city",own.Pieces.Cities,citySetup?DockCost():DockCost(wheat:2,ore:3),citySetup?view.LegalVertexIds.Length>0:view.LegalCityVertexIds.Length>0);
            AddDockBuild(s,roadSetup?CommandKind.SetupShip:freeRoad?CommandKind.PlaceFreeShip:CommandKind.BuildShip,"船","ship",own.ShipsRemaining,roadSetup||freeRoad?DockCost():DockCost(wood:1,wool:1),view.LegalShipEdgeIds.Length>0);
            AddDockBuild(s,CommandKind.BuildWall,"城墙","wall",own.WallSupply,DockCost(brick:2),DockLegal(CommandKind.BuildWall));
            AddDockBuild(s,CommandKind.RecruitKnight,"基础骑士","knight",own.KnightSupply[0],DockCost(wool:1,ore:1),DockLegal(CommandKind.RecruitKnight),1);
            for(int level=2;level<=3;level++)
            {
                int previousLevel=level-1;
                bool legal=view.LegalActions.Any(c=>c.Kind==CommandKind.PromoteKnight&&view.Board.Knights.Any(k=>k.PlayerId==seat&&k.LocationId==c.TargetId&&k.Level==previousLevel));
                AddDockBuild(s,CommandKind.PromoteKnight,level==2?"强力骑士":"精锐骑士","knight",own.KnightSupply[level-1],DockCost(wool:1,ore:1),legal,level);
            }
            for(int track=0;track<3;track++)
            {
                int[] cost=new int[8];cost[5+track]=Math.Min(5,own.Improvements[track]+1);
                bool legal=view.LegalActions.Any(c=>c.Kind==CommandKind.ImproveCity&&(int)c.Track==track);
                var b=AddDockBuild(s,CommandKind.ImproveCity,new[]{"贸易设施","政治设施","科学设施"}[track],"facility",1,cost,legal,track);b.Track=track;b.Level=own.Improvements[track];b.Detail="下一级需 "+(own.Improvements[track]+1)+" 张"+Goods[5+track]+"；三级能力："+new[]{"商品以 2:1 兑换","允许晋升精锐骑士","非 7 且没有产出时，选取 1 张资源"}[track]+"；四 / 五级争夺大都会";
                if(b.Level==5){Array.Clear(b.Cost,0,8);b.Detail="设施已达到最高等级";b.UnavailableReason=b.Detail;}
            }

            foreach(var card in view.OwnProgressCards)
                s.Cards.Add(new OperationHandCard{Id=((int)card)+":"+s.Cards.Count(entry=>entry.Id.StartsWith(((int)card)+":",StringComparison.Ordinal)),Name=CardName(card),Detail=CardHelp[(int)card],Progress=true,ArtIndex=(int)card,Playable=view.LegalActions.Any(c=>c.Kind==CommandKind.PlayProgressCard&&c.ProgressCard==card)});
            if(view.TradeOffer!=null)
            {
                var t=view.TradeOffer;s.Offer=new OperationOffer{Proposer=t.ProposerId,Recipient=t.OtherPlayerId,IsBroadcast=t.IsBroadcast,PendingRecipients=t.PendingRecipientIds,RejectedRecipients=t.RejectedPlayerIds??Array.Empty<string>(),Give=DockGoods(t.Give,t.GiveCommodities),Receive=DockGoods(t.Receive,t.ReceiveCommodities),CanAccept=t.CanRespond(seat),CanReject=t.CanRespond(seat),CanCancel=seat==t.ProposerId};
            }
            s.PrepareBuild=b=>
            {
                CancelOperationDraft();Select((CommandKind)Enum.Parse(typeof(CommandKind),b.Id.Split(':')[0]));
                if(b.Track>=0){operationFacilityLocked=true;draft.Track=(ImprovementTrack)b.Track;if(!TryBeginDirectPayment(draft,b.Cost,b.Name))Submit(draft);}
                else{operationKnightLevel=b.Id.Contains(":")?int.Parse(b.Id.Split(':')[1]):0;TryBeginDirectPayment(draft,b.Cost,b.Name);pickField="target";pickType=draft.Kind==CommandKind.BuildRoad||draft.Kind==CommandKind.SetupRoad||draft.Kind==CommandKind.PlaceFreeRoad||IsShip(draft)?"E":"V";status="已拿起 "+b.Name+"，点击地图上的合法位置放下；右键或 Esc 放回。";}
            };
            s.PrepareCard=PrepareProgressCard;
            if(view.OwnProgressCards.Length<=4||view.Phase!=GamePhase.Action||view.ActivePlayerId!=seat)handOverflowDiscard=false;
            s.ResourceHandLimit=7+2*view.Board.Walls.Count(w=>w.PlayerId==seat);s.SpecialHandLimit=4;
            var handDiscard=view.Phase==GamePhase.Discard?view.Discards.FirstOrDefault(d=>d.PlayerId==seat):null;
            var handDecision=view.PendingDecision;
            bool specialDiscard=handOverflowDiscard||handDecision!=null&&handDecision.PlayerId==seat&&handDecision.Kind=="ProgressDiscard";
            bool effectDiscard=handDecision!=null&&handDecision.PlayerId==seat&&(handDecision.Kind=="ProgressSabotage"||handDecision.Kind=="ProgressWedding");
            if(handDiscard!=null||specialDiscard||effectDiscard)s.Discard=new OperationDiscard{
                Amount=handDiscard!=null?handDiscard.Amount:specialDiscard?1:handDecision.Amount,Special=specialDiscard,
                Prompt=effectDiscard&&handDecision.Kind=="ProgressWedding"?"赠礼":"弃牌",Confirm=(selected,card)=>{
                    var c=New(specialDiscard?CommandKind.DiscardProgressCard:effectDiscard?CommandKind.ResolveProgressChoice:CommandKind.DiscardResources);
                    if(specialDiscard){c.ProgressCard=(ProgressCardKind)int.Parse(card.Id.Split(':')[0]);c.SelectedProgressCard=c.ProgressCard;}
                    else for(int i=0;i<8;i++){if(i<5)c.Resources[(Resource)i]=selected[i];else c.Commodities[(Commodity)(i-5)]=selected[i];}
                    Submit(c);
                }};
            s.Cancel=CancelOperationDraft;
            s.Notify=ai.ShowTablePrompt;
            if(DirectPaymentAvailable)s.ValidateTradeDraft=t=>ValidatePaymentTrade(s,t);
            s.Trade=t=>
            {
                string error=OperationSelection.ValidateTrade(TradeValidationSnapshot(),t);if(error!=null)return error;
                var c=New(t.Bank?CommandKind.BankTrade:CommandKind.ProposeTrade);
                if(t.Bank)
                {
                    int g=Array.FindIndex(t.Give,n=>n>0),r=Array.FindIndex(t.Receive,n=>n>0);c.GiveIsCommodity=g>=5;c.ReceiveIsCommodity=r>=5;
                    if(g<5)c.GiveResource=(Resource)g;else c.GiveCommodity=(Commodity)(g-5);if(r<5)c.ReceiveResource=(Resource)r;else c.ReceiveCommodity=(Commodity)(r-5);c.GiveAmount=t.Give[g];c.ReceiveAmount=t.Receive.Sum();c.Receive=new ResourceBag();for(int i=0;i<5;i++)c.Receive[(Resource)i]=t.Receive[i];for(int i=5;i<8;i++)c.ReceiveCommodities[(Commodity)(i-5)]=t.Receive[i];
                }
                else{c.OtherPlayerId=t.OtherPlayer;for(int i=0;i<8;i++){if(i<5){c.Give[(Resource)i]=t.Give[i];c.Receive[(Resource)i]=t.Receive[i];}else{c.GiveCommodities[(Commodity)(i-5)]=t.Give[i];c.ReceiveCommodities[(Commodity)(i-5)]=t.Receive[i];}}}
                if(StagePaymentTrade(c,t))return null;
                var result=Submit(c);return result!=null&&result.Success?null:status;
            };
            s.Respond=choice=>Submit(New(choice=="accept"?CommandKind.AcceptTrade:choice=="reject"?CommandKind.RejectTrade:CommandKind.CancelTrade));operationSnapshot=s;
        }
        operationSnapshot.Status=status;operationSnapshot.CanInteract=ai.IsHumanTurn||view.TradeOffer!=null&&view.TradeOffer.ProposerId==seat;
        operationSnapshot.CanTrade=ai.IsHumanTurn&&view.ActivePlayerId==seat&&view.Phase==GamePhase.Action&&view.PendingDecision==null&&view.TradeOffer==null;
        return operationSnapshot;
    }
    OperationBuild AddDockBuild(OperationSnapshot s,CommandKind kind,string name,string icon,int count,int[] cost,bool legal,int suffix=-1)
    {
        var build=new OperationBuild{Id=kind.ToString()+(suffix<0?"":":"+suffix),Name=name,Icon=icon,Count=count,Cost=cost,Detail=cost.Sum()==0?"当前阶段免费放置":kind==CommandKind.PromoteKnight?"拿起高一级骑士，放到要晋升的己方骑士上":kind==CommandKind.ImproveCity?"点击翻开下一级设施并支付商品":"拿起棋子，再点击地图上的合法位置放下"};build.ConfigureAvailability(legal,s.Goods,kind!=CommandKind.BuildCity);ConfigureDirectPaymentAvailability(build,legal,suffix);if(icon=="knight")build.Level=suffix;s.Builds.Add(build);return build;
    }
    void DrawOperationLayout()
    {
        Styles();GUI.matrix=MobileTabletop.GuiMatrix;cardArt.Begin(null);
        if(view==null){GUI.matrix=Matrix4x4.identity;return;}
        ai.SyncThreeKingdomsSelection();SyncDirectPayment();
        tabletop.ThreeKingdoms=ai;dock.DirectSelection=directPayment.Active?PaymentHandSelection:ai.ThreeKingdomsHandSelection;
        dock.InspectGood=ai.InspectThreeKingdomsGood;dock.InspectCard=ai.InspectThreeKingdomsCard;
        tabletop.BindOperations(Operations(),dock.PrepareDevelopmentPurchase,ai.HistoryEntries);
        dock.Windows=tabletop.OperationWindows;mapChoice.Windows=tabletop.OperationWindows;
        bool previousEnabled=GUI.enabled,tableEnabled=previousEnabled&&!ai.TablePresentationBlocked&&!tabletop.HistoryOpen&&!ResourceChoiceVisible;GUI.enabled=previousEnabled&&(Event.current.type==EventType.Repaint||!mapChoice.Active&&tableEnabled&&!dock.BlocksHudInput(Event.current.mousePosition)&&!tabletop.BlocksOverlayInput(Event.current.mousePosition));
        TabletopUI.DrawMapLabels(DrawBoardLabels);
        tabletop.BindTable(board.TableScene);dock.BindTable(board.TableScene);dock.PlayerName=ai.PlayerDisplayName;dock.BindCollections(tabletop.Collections,!tabletop.HistoryOpen&&!ai.TablePresentationBlocked);tabletop.BindCollectionOcclusion(dock.DrawLandingOcclusion);tabletop.HandFlightsVisible=dock.ResourceHandVisible;
        tabletop.BindOperations(Operations(),dock.PrepareDevelopmentPurchase,ai.HistoryEntries);
        tabletop.BindDiscard(Operations(),dock);
        tabletop.BindLongestRoad(view.LongestRoadPlayerId,view.Players.FirstOrDefault(p=>p.Id==view.LongestRoadPlayerId)?.LongestRoadLength??0);
        tabletop.ObserveDice(view.Events.LastOrDefault(e=>e.Kind=="RollDice")?.Sequence??0,view.LastDice1,view.LastDice2,(int)view.LastEventDie,ai.CardPresentationRoll);
        tabletop.BindDefense(view.BarbarianPosition,view.DefenderStrength,view.BarbarianStrength);
        ai.TableDiceRolling=tabletop.DiceRolling;
        // 本轮绘制共用掷骰前的快照，避免点击掷骰后手牌先于动画刷新数量。
        var displayedOperations=Operations();
        tabletop.Draw(font,seat,Actor(view),PhaseName(view.Phase),view.Turn,view.OwnVictoryPoints,view.LastDice1,view.LastDice2,
            view.Players.Select(p=>new TabletopSeat{Id=p.Id,Points=p.VictoryPoints,Score=TabletopScoreModel.For(view,p.Id),Resources=p.ResourceCount,Cards=p.ProgressCardCount,ResourceHandLimit=7+2*view.Board.Walls.Count(w=>w.PlayerId==p.Id),SpecialHandLimit=4,Roads=p.Pieces.Roads,Settlements=p.Pieces.Settlements,Cities=p.Pieces.Cities,Ships=p.ShipsRemaining,Improvements=p.Improvements}).ToArray(),
            ai.IsHumanTurn&&view.Phase==GamePhase.ProductionAwaitRoll,ai.IsHumanTurn&&view.Phase==GamePhase.Action&&view.PendingDecision==null&&view.TradeOffer==null&&!directPayment.Active,
            ()=>Submit(New(CommandKind.RollDice)),EndTableTurn,target=>{CancelOperationDraft();dock.OpenTrade(target);},
            ()=>{bool open=!operationActions;dock.Reset();CancelOperationDraft();operationActions=open;},"骑士操作",facility:dock.OpenFacility);

        tabletop.SetContextAnchor(operationActions&&operationKnightTarget!=null?(Vector2?)MobileTabletop.Logical(board.ScreenPoint(operationKnightTarget)):null);
        GUI.enabled=tableEnabled&&!mapChoice.Active&&!dock.HasOverlay;
        if(!ai.ThreeKingdomsSelectionActive&&OperationContextVisible&&!ResourceChoiceVisible)tabletop.DrawContext(operationActions?"骑士行动":view.PendingDecision!=null?DecisionName(view.PendingDecision.Kind):draft!=null&&draft.Kind==CommandKind.PlayProgressCard?CardName(draft.ProgressCard):"当前选择",DrawOperationControls);
        GUI.enabled=previousEnabled&&(Event.current.type==EventType.Repaint||!mapChoice.Active&&!ai.TableInspectionBlocked&&!ResourceChoiceVisible&&!tabletop.HistoryOpen&&(dock.HasOverlay||!tabletop.BlocksOverlayInput(Event.current.mousePosition)));dock.Draw(displayedOperations,font);GUI.enabled=previousEnabled;
        if(ResourceChoiceVisible&&ai.IsHumanTurn&&!ai.TablePresentationBlocked&&!tabletop.HistoryOpen)
        {
            DrawPendingResources();
        }
        tabletop.HandFlightsVisible=dock.ResourceHandVisible;if(!ResourceChoiceVisible&&(tabletop.HistoryOpen||!dock.HasOverlay)&&!ai.TableInspectionBlocked)tabletop.DrawHistory(font,ai.HistoryEntries,ai.HistoryPlayers);
        ai.TableOverlayOpen=mapChoice.Active||dock.HasOverlay||tabletop.ContextVisible||tabletop.HistoryOpen||ResourceChoiceVisible;
        if(DirectProgressActive&&view.PendingDecision?.Kind=="ProgressTreasonPlace"&&!mapChoice.Active&&ai.IsHumanTurn&&ModernOperationUI.Button(new Rect(610,196,220,34),"放弃安置替补骑士")){var skip=New(CommandKind.ResolveProgressChoice);skip.Decline=true;Submit(skip);}
        if(!ai.TablePresentationBlocked&&ai.IsHumanTurn)mapChoice.Draw();
        DrawSpecialPanel();
        GUI.enabled=previousEnabled&&!ai.TablePresentationBlocked&&!tabletop.HistoryOpen;DrawDirectPayment();GUI.enabled=previousEnabled;
        RefreshTablePrompt();GUI.matrix=Matrix4x4.identity;
    }
    void RefreshTablePrompt()
    {
        if(ai!=null&&view!=null)ai.UpdateTablePrompt(view.Turn,view.Phase.ToString(),view.Phase==GamePhase.ProductionAwaitRoll,TableInstruction(),status,RequiresTableSelection);
    }
    internal void CancelForGeneralSelection(){dock.Reset();CancelOperationDraft();cardArt.Clear();tabletop.OperationWindows.Clear();}
    internal void EndThreeKingdomsTurn()=>EndTableTurn();
    void EndTableTurn()
    {
        if(directPayment.Active){status="请先确认或取消本次支付。";return;}
        if(view.OwnProgressCards.Length>4)
        {
            handOverflowDiscard=true;operationView=null;dock.Reset();CancelOperationDraft();
            ai.ShowTablePrompt("发展卡超过 4 张，请点选要弃掉的牌，再在右下角确认");
            return;
        }
        Submit(New(CommandKind.EndTurn));
    }
    bool RequiresTableSelection=>directPayment.Active||DirectProgressActive||pickField!=""||handOverflowDiscard||operationActions&&operationKnightTarget==null||TabletopSelectionPrompt.RequiredPhase(view.Phase.ToString());
    string TableInstruction()
    {
        if(DirectProgressActive)return draft!=null&&draft.Kind==CommandKind.PlayProgressCard?status:TabletopSelectionPrompt.Decision(view.PendingDecision.Kind);
        if(FreeRoadPlacementActive)return TabletopRoadPlacement.Instruction(view.PendingDecision?.Kind=="ProgressRoads"?view.PendingDecision.Amount:view.PendingDecision?.RemainingRoads??2,ai.HistoryEntries);
        if(view.ThreeKingdoms?.SkillDecision!=null)return "请在底部操作条完成武将技能选择";
        if(view.Phase==GamePhase.ThreeKingdoms)return view.ThreeKingdoms?.PendingDecision==null?"请在底部操作条处理三国阶段":"请在底部候选牌带完成私密选将";
        if(view.Phase==GamePhase.Finished)return view.WinnerPlayerId+" 获胜";
        if(pickField=="source")return draft?.Kind==CommandKind.MoveShip?"请选择要移动的船":"请选择要行动的己方骑士";
        if(pickField!="")return draft?.Kind==CommandKind.MoveRobber?"请选择强盗要前往的新地块":draft?.Kind==CommandKind.MovePirate?"请选择海盗要前往的新海域":"请选择地图上的合法目标";
        if(handOverflowDiscard)return "发展卡超过 4 张，请选择要弃掉的发展卡";
        if(view.Phase==GamePhase.ProductionAwaitRoll)return "请掷骰";
        if(view.Phase==GamePhase.SetupSettlement)return view.Board.Settlements.Any(p=>p.PlayerId==seat)?"请放置城市":"请放置村庄";
        if(view.Phase==GamePhase.SetupRoad)return "请放置道路或船";
        if(view.Phase==GamePhase.Discard)return "请选择要弃掉的牌";
        if(view.Phase==GamePhase.RobberMove)return "请移动强盗或海盗";
        if(view.Phase==GamePhase.RobberSteal)return "请选择偷取对象";
        if(view.Phase==GamePhase.PendingChoice)return TabletopSelectionPrompt.Decision(view.PendingDecision?.Kind);
        if(operationActions&&operationKnightTarget==null)return "请选择地图上的己方骑士，再选择行动";
        return "请选择你的行动";
    }
    void DrawOperationControls()
    {
        if(!ai.IsHumanTurn||view.TradeOffer!=null)return;
        if(operationActions)
        {
            var knight=view.Board.Knights.FirstOrDefault(k=>k.PlayerId==seat&&k.LocationId==operationKnightTarget);
            if(knight!=null)
            {
                Text(new[]{"","基础骑士","强力骑士","精锐骑士"}[knight.Level]+"  ·  "+(knight.Active?"已激活":"未激活"));
                if(knight.Active&&knight.ActivatedTurn==view.Turn)Text("本回合刚激活",true);
            }
            else Text("请选择己方骑士。",true);
            GUILayout.Space(8);
            TabletopAction("晋升 · 羊毛1 矿石1",CommandKind.PromoteKnight);
            TabletopAction("激活 · 小麦1",CommandKind.ActivateKnight);
            TabletopAction("移动骑士",CommandKind.MoveKnight);
            TabletopAction("驱离弱骑士",CommandKind.DisplaceKnight);
            TabletopAction("驱逐强盗",CommandKind.ChaseRobber);
            TabletopAction("驱逐海盗",CommandKind.ChasePirate);
            return;
        }
        if(view.Phase==GamePhase.RobberMove)
        {
            Text("拿起强盗或海盗，再在地图放下。",true);
            if(Btn("拿起强盗")){Select(CommandKind.MoveRobber);pickField="target";pickType="T";status="点击新的地块放下强盗。";}
            if(Btn("拿起海盗")){Select(CommandKind.MovePirate);pickField="target";pickType="T";status="点击海域放下海盗。";}
            if(Btn("将海盗放到外框海域")){Submit(New(CommandKind.MovePirate,"frame"));GUIUtility.ExitGUI();}
            return;
        }
        if(FreeRoadPlacementActive)
        {
            Text("当前没有可建位置，可以结束道路建设。",true);
            if(Btn("完成道路建设")){var command=New(view.PendingDecision?.Kind=="ProgressRoads"?CommandKind.ResolveProgressChoice:CommandKind.FinishRoadBuilding);command.Decline=true;Submit(command);GUIUtility.ExitGUI();}
            return;
        }
        if(view.Phase==GamePhase.Discard||view.Phase==GamePhase.PendingChoice||view.Phase==GamePhase.RobberSteal){DrawControls();return;}
        if(draft!=null)DrawDraft();
        else if(view.Phase==GamePhase.Action&&view.CommercialHarborPlayers.Length>0){Text("商港可向每位剩余对手提供一次资源。",true);TabletopAction("商港 · 提供资源",CommandKind.CommercialHarborOffer);}
    }
    void TabletopAction(string text,CommandKind kind)
    {
        bool from=kind==CommandKind.MoveKnight||kind==CommandKind.DisplaceKnight;
        bool knightAction=kind==CommandKind.PromoteKnight||kind==CommandKind.ActivateKnight||from||kind==CommandKind.ChaseRobber||kind==CommandKind.ChasePirate;
        bool legal=view.LegalActions.Any(c=>c.Kind==kind&&(!knightAction||operationKnightTarget==null||(from?c.SourceId:c.TargetId)==operationKnightTarget));
        if(!legal&&DirectPaymentAvailable){var paymentProbe=New(kind,operationKnightTarget);var paymentCost=PaymentCost(paymentProbe);if(paymentCost!=null)legal=operationKnightTarget!=null?PaymentAwarePreview(paymentProbe).Success:PotentialPaymentBuild(kind,paymentCost,0);}
        if(!ModernOperationUI.LayoutButton(text,legal))return;
        string knight=operationKnightTarget;
        CancelOperationDraft();Select(kind);
        if(kind==CommandKind.CommercialHarborOffer)return;
        if(kind==CommandKind.MoveShip){pickField="source";pickType="E";status="点击要移动的船，再点击目的海岸。";return;}
        bool source=kind==CommandKind.MoveKnight||kind==CommandKind.DisplaceKnight;
        if(knight!=null)
        {
            if(source){draft.SourceId=knight;pickField="target";}
            else{draft.TargetId=knight;Submit(draft);GUIUtility.ExitGUI();return;}
        }
        else pickField=source?"source":"target";
        pickType="V";status=source?"点击骑士，再点击它要前往的位置；右键放回。":"点击要行动的己方骑士。";
    }
    bool PickTabletopKnight(Vector2 mouse)
    {
        if(view==null||curtain||newGame||artOpen||!ai.IsHumanTurn||view.Phase!=GamePhase.Action||view.PendingDecision!=null||view.TradeOffer!=null)return false;
        string vertex=Pick("V",mouse);
        if(vertex==""||!view.Board.Knights.Any(k=>k.PlayerId==seat&&k.LocationId==vertex))return false;
        dock.Reset();CancelOperationDraft();operationKnightTarget=vertex;operationActions=true;
        var projected=board.ScreenPoint(vertex);tabletop.SetContextAnchor(MobileTabletop.Logical(projected));
        status="选择这名骑士的动作；右键空白处或 Esc 收起。";return true;
    }
    bool PickTabletopContext(Vector2 mouse)
    {
        if(draft!=null)return false;
        return PickTabletopKnight(mouse)||PickTabletopBuilding(mouse);
    }
    bool PickTabletopBuilding(Vector2 mouse)
    {
        if(view==null||curtain||newGame||artOpen||!ai.IsHumanTurn)return false;
        return OpenTabletopBuildingMenu(Pick("V",mouse));
    }
    bool OpenTabletopBuildingMenu(string vertex)
    {
        if(view==null||!ai.IsHumanTurn||view.Phase!=GamePhase.Action||view.PendingDecision!=null||view.TradeOffer!=null)return false;
        bool village=view.Board.Settlements.Any(p=>p.PlayerId==seat&&p.LocationId==vertex);
        bool city=view.Board.Cities.Any(p=>p.PlayerId==seat&&p.LocationId==vertex);
        if(!village&&!city)return false;
        dock.Reset();CancelOperationDraft();
        if(city&&view.Board.Walls.Any(p=>p.LocationId==vertex)){ai.ShowTablePrompt(status="这座城市已有城墙。");return true;}
        var kind=village?CommandKind.BuildCity:CommandKind.BuildWall;
        mapChoice.Open(board.ScreenPoint(vertex),village?"村庄升级":"城市城墙",new[]{village?"升级城市 · 小麦2 矿石3":"修建城墙 · 砖块2"},i=>{
            var command=New(kind,vertex);var preview=PaymentAwarePreview(command);
            if(preview.Success)Submit(command);else ai.ShowTablePrompt(status=preview.Message);
        });return true;
    }
    internal bool VerificationOpenBuilding(string vertex)=>OpenTabletopBuildingMenu(vertex);
    internal void VerificationChooseBuildingUpgrade()=>mapChoice.Choose(0);
    internal bool VerificationSelectKnight(string vertex)
    {
        if(view==null||!ai.IsHumanTurn||view.Phase!=GamePhase.Action||view.PendingDecision!=null||view.TradeOffer!=null||!view.Board.Knights.Any(k=>k.PlayerId==seat&&k.LocationId==vertex))return false;
        dock.Reset();CancelOperationDraft();operationKnightTarget=vertex;operationActions=true;return true;
    }
    internal string VerificationSelectedKnight=>operationKnightTarget;
    bool PickTabletopObject(Vector2 mouse)
    {
        if(draft!=null||!ai.IsHumanTurn||view.Phase!=GamePhase.Action||view.PendingDecision!=null||view.TradeOffer!=null)return false;
        if(PickTabletopPort(mouse))return true;
        if(MobileTabletop.Enabled&&PickTabletopKnight(mouse))return true;
        string edge=Pick("E",mouse);
        if(edge!=""&&view.Board.Ships.Any(s=>s.PlayerId==seat&&s.LocationId==edge))
        {
            if(!view.MovableShipEdgeIds.Contains(edge)){status="这艘船当前不能改道：只可移动开放航线末端、非本回合建造且不受海盗阻挡的船，每回合一次。";ai.ShowTablePrompt(status);return true;}
            CancelOperationDraft();Select(CommandKind.MoveShip);draft.SourceId=edge;pickField="target";pickType="E";
            board.SetSelectedShip(edge);status="船已选中，点击合法空海岸改道；右键或 Esc 取消。";return true;
        }
        if(operationActions)CancelOperationDraft();
        return false;
    }
    void PrepareProgressCard(OperationHandCard card)
    {
        CancelOperationDraft();Select(CommandKind.PlayProgressCard);
        draft.ProgressCard=(ProgressCardKind)int.Parse(card.Id.Split(':')[0]);
        switch(draft.ProgressCard)
        {
            case ProgressCardKind.Irrigation:case ProgressCardKind.Mining:case ProgressCardKind.RoadBuilding:
            case ProgressCardKind.CommercialHarbor:case ProgressCardKind.Encouragement:case ProgressCardKind.Wedding:case ProgressCardKind.Sabotage:
                Submit(draft);return;
        }
        if(DirectProgressActive){BeginDirectProgress();return;}
        status="请选择"+card.Name+"的效果。";
    }
    internal OperationSnapshot VerificationOperationSnapshot=>Operations();
    internal OperationDock VerificationOperationDock=>dock;
}
