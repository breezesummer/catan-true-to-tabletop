using System;
using System.Linq;
using Catan.Core;
using UnityEngine;
using CommandKind = Catan.Core.M3.CommandKind;
using GamePhase = Catan.Core.M3.GamePhase;
using PlayerView = Catan.Core.M3.PlayerView;
using DevelopmentCardKind = Catan.Core.M3.DevelopmentCardKind;

public sealed partial class M3App
{
    internal OperationSnapshot VerificationOperationSnapshot=>Operations();
    internal OperationDock VerificationOperationDock=>dock;
    readonly OperationDock dock = new OperationDock();
    readonly TabletopUI tabletop = new TabletopUI();
    bool tableNotes;
    string tableVictim;
    Vector2 pendingOperationScroll;
    string operationPortId;
    PlayerView operationView;
    OperationSnapshot operationSnapshot;
    DevelopmentCardKind? preparedDevelopment;
    bool preparedPurchase;
    bool showSeafaring; Vector2 operationScroll;
    bool DockBlocksMouse(Vector2? logical=null)
    {
        if(ai==null)return false;
        var p=logical??new Vector2(Input.mousePosition.x*1440f/Screen.width,(Screen.height-Input.mousePosition.y)*900f/Screen.height);
        return ai.BlocksTableInput(p)||tabletop.BlocksBoardInput(p)||dock.BlocksBoardInput(p);
    }
    void CancelOperationDraft(){if(!dock.TradeOpen)operationPortId=null;tabletop.SetContextAnchor(null);preparedDevelopment=null;preparedPurchase=false;showSeafaring=false;tableNotes=false;tableVictim=null;CancelPickup();}
    static int[] DockCost(int wood=0,int brick=0,int wool=0,int wheat=0,int ore=0)=>new[]{wood,brick,wool,wheat,ore,0,0,0};
    // 银行和港口分别展示各自权限；最终指令仍由同一规则入口裁决。
    OperationSnapshot TradeValidationSnapshot()
    {
        var source=Operations();
        if(!dock.IsPortTrade||operationPortId==null)return source;
        var locations=view.Board.Settlements.Concat(view.Board.Cities).Where(b=>b.PlayerId==seat).Select(b=>b.LocationId).ToArray();
        var port=view.Board.Ports.FirstOrDefault(p=>p.Id==operationPortId&&p.Vertices.Any(locations.Contains));
        if(port==null)return source;
        var result=new OperationSnapshot{CanTrade=source.CanTrade,CanInteract=source.CanInteract,GoodCount=source.GoodCount,Goods=source.Goods,Bank=source.Bank,OtherPlayers=source.OtherPlayers,Offer=source.Offer};
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
        if(!port.Vertices.Any(locations.Contains)){status="此港口未连接你的定居点或城市。";ai.ShowTablePrompt(status);return true;}
        CancelOperationDraft();
        int? good=port.Resource.HasValue?(int?)((int)port.Resource.Value):null;
        dock.OpenPortTrade(good,good.HasValue?ResourceNames[good.Value]+"港口 · 2:1":"通用港口 · 3:1");
        operationPortId=id;return true;
    }
    bool PickTabletopObject(Vector2 mouse)
    {
        if(view==null||!ai.IsHumanTurn||view.Phase!=GamePhase.Action||view.PendingDecision!=null||view.TradeOffer!=null)return false;
        if(PickTabletopPort(mouse))return true;
        string edge="";float distance=25f*Screen.width/1440f;
        foreach(var ship in view.Board.Ships.Where(s=>s.PlayerId==seat))
        {float d=Vector2.Distance(mouse,board.ScreenPoint(ship.LocationId));if(d<distance){distance=d;edge=ship.LocationId;}}
        if(edge!="")
        {
            if(!view.MovableShipEdgeIds.Contains(edge)){status="这艘船当前不能改道：只可移动开放航线末端、非本回合建造且不受海盗阻挡的船，每回合一次。";ai.ShowTablePrompt(status);return true;}
            CancelOperationDraft();Pickup(CommandKind.MoveShip);shipSource=edge;hover="";placementLegalIds=ComputeLegalTargets();
            board.SetSelectedShip(edge);status="船已选中，点击合法空海岸改道；右键或 Esc 取消。";return true;
        }
        return false;
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
            operationView=view;
            var own=view.Players.First(p=>p.Id==seat);
            var s=new OperationSnapshot{Scope="M3:"+seat,Seat=seat,Revision=view.Turn+":"+view.Phase+":"+view.Events.Length,Phase=PhaseName(view.Phase),GoodCount=5};
            s.Goods=DockCost(view.OwnResources.Wood,view.OwnResources.Brick,view.OwnResources.Wool,view.OwnResources.Wheat,view.OwnResources.Ore);
            s.Bank=DockCost(view.Bank.Wood,view.Bank.Brick,view.Bank.Wool,view.Bank.Wheat,view.Bank.Ore);
            for(int i=0;i<5;i++)s.Rates[i]=4;
            s.OtherPlayers=view.Players.Where(p=>p.Id!=seat).Select(p=>p.Id).ToArray();
            bool setup=view.Phase==GamePhase.SetupSettlement,roadSetup=view.Phase==GamePhase.SetupRoad,freeRoad=view.Phase==GamePhase.RoadBuilding;
            AddDockBuild(s,setup?CommandKind.SetupSettlement:CommandKind.BuildSettlement,"定居点","settlement",own.Pieces.Settlements,setup?DockCost():DockCost(1,1,1,1),view.LegalVertexIds.Length>0);
            AddDockBuild(s,roadSetup?CommandKind.SetupRoad:freeRoad?CommandKind.PlaceFreeRoad:CommandKind.BuildRoad,"道路","road",own.Pieces.Roads,roadSetup||freeRoad?DockCost():DockCost(1,1),view.LegalEdgeIds.Length>0);
            AddDockBuild(s,CommandKind.BuildCity,"城市","city",own.Pieces.Cities,DockCost(wheat:2,ore:3),view.LegalCityVertexIds.Length>0);
            AddDockBuild(s,CommandKind.BuyDevelopmentCard,"发展卡","development",view.DevelopmentDeckCount,DockCost(wool:1,wheat:1,ore:1),view.Phase==GamePhase.Action&&host.Preview(NewCommand(CommandKind.BuyDevelopmentCard)).Success);
            AddDockBuild(s,roadSetup?CommandKind.SetupShip:freeRoad?CommandKind.PlaceFreeShip:CommandKind.BuildShip,"船","ship",own.ShipsRemaining,roadSetup||freeRoad?DockCost():DockCost(wood:1,wool:1),view.LegalShipEdgeIds.Length>0);

            foreach(var c in view.OwnDevelopmentCards)
                s.Cards.Add(new OperationHandCard{Id=((int)c.Kind)+":"+s.Cards.Count(entry=>entry.Id.StartsWith(((int)c.Kind)+":",StringComparison.Ordinal)),Name=DockDevelopmentName(c.Kind),Detail=c.Kind==DevelopmentCardKind.VictoryPoint?"隐藏胜利点，达到目标后可宣告胜利":c.BoughtTurn>=view.Turn?"本回合买入，下回合可用":"点击准备出牌，在桌面确认",Playable=c.Kind!=DevelopmentCardKind.VictoryPoint&&c.BoughtTurn<view.Turn&&!view.DevelopmentCardPlayedThisTurn&&(view.Phase==GamePhase.Action||view.Phase==GamePhase.ProductionAwaitRoll)&&view.TradeOffer==null&&view.ActivePlayerId==seat});
            if(view.TradeOffer!=null)
            {
                var t=view.TradeOffer;
                s.Offer=new OperationOffer{Proposer=t.ProposerId,Recipient=t.OtherPlayerId,IsBroadcast=t.IsBroadcast,PendingRecipients=t.PendingRecipientIds,RejectedRecipients=t.RejectedPlayerIds??Array.Empty<string>(),Give=DockCost(t.Give.Wood,t.Give.Brick,t.Give.Wool,t.Give.Wheat,t.Give.Ore),Receive=DockCost(t.Receive.Wood,t.Receive.Brick,t.Receive.Wool,t.Receive.Wheat,t.Receive.Ore),CanAccept=t.CanRespond(seat),CanReject=t.CanRespond(seat),CanCancel=seat==t.ProposerId};
            }
            s.PrepareBuild=b=>{CancelOperationDraft();var k=(CommandKind)Enum.Parse(typeof(CommandKind),b.Id);if(k==CommandKind.BuyDevelopmentCard){preparedPurchase=true;status="已选发展卡，请在桌面确认购买。";}else Pickup(k);};
            s.PrepareCard=c=>{CancelOperationDraft();preparedDevelopment=(DevelopmentCardKind)int.Parse(c.Id.Split(':')[0]);status="已选择 "+c.Name+"，请在桌面确认出牌。";};
            s.Cancel=CancelOperationDraft;
            s.Notify=ai.ShowTablePrompt;
            s.Trade=t=>
            {
                var error=OperationSelection.ValidateTrade(TradeValidationSnapshot(),t);if(error!=null)return error;
                var c=NewCommand(t.Bank?CommandKind.BankTrade:CommandKind.ProposeTrade);
                if(t.Bank){int g=Array.FindIndex(t.Give,n=>n>0),r=Array.FindIndex(t.Receive,n=>n>0);c.GiveResource=(Resource)g;c.ReceiveResource=(Resource)r;c.GiveAmount=t.Give[g];c.ReceiveAmount=t.Receive[r];}
                else{c.Give=new ResourceBag();c.Receive=new ResourceBag();c.OtherPlayerId=t.OtherPlayer;for(int i=0;i<5;i++){c.Give[(Resource)i]=t.Give[i];c.Receive[(Resource)i]=t.Receive[i];}}
                var result=Submit(c);return result!=null&&result.Success?null:status;
            };
            s.Respond=choice=>Submit(NewCommand(choice=="accept"?CommandKind.AcceptTrade:choice=="reject"?CommandKind.RejectTrade:CommandKind.CancelTrade));
            operationSnapshot=s;
        }
        operationSnapshot.Status=status;operationSnapshot.CanInteract=ai.IsHumanTurn||view.TradeOffer!=null&&view.TradeOffer.ProposerId==seat;
        operationSnapshot.CanTrade=ai.IsHumanTurn&&view.ActivePlayerId==seat&&view.Phase==GamePhase.Action&&view.PendingDecision==null&&view.TradeOffer==null;
        return operationSnapshot;
    }
    void AddDockBuild(OperationSnapshot s,CommandKind kind,string name,string icon,int count,int[] cost,bool legal)
    {
        bool enough=Enumerable.Range(0,8).All(i=>s.Goods[i]>=cost[i]);
        bool available=ai.IsHumanTurn&&count>0&&legal&&enough;
        s.Builds.Add(new OperationBuild{Id=kind.ToString(),Name=name,Icon=icon,Count=count,Cost=cost,Available=available,Detail=cost.Sum()==0?"当前阶段免费放置":"点击选择费用，再在棋盘放置",UnavailableReason=!ai.IsHumanTurn?"等待当前行动或待决席位":count==0?"库存已用尽":!enough?"缺少所需资源":!legal?"当前阶段或连接条件没有合法目标":null});
    }
    static string DockDevelopmentName(DevelopmentCardKind kind)=>new[]{"骑士","道路建设","丰收","垄断","胜利点"}[(int)kind];
    void DrawOperationLayout()
    {
        Styles();GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1440f,Screen.height/900f,1));cardArt.Begin(null);
        if(view==null){GUI.matrix=Matrix4x4.identity;return;}
        bool enabled=GUI.enabled;GUI.enabled=enabled&&(Event.current.type==EventType.Repaint||!dock.HasOverlay&&!ai.TableMenuOpen&&!dock.BlocksBoardInput(Event.current.mousePosition)&&!tabletop.BlocksOverlayInput(Event.current.mousePosition));
        TabletopUI.DrawMapLabels(DrawBoardLabels);
        tabletop.BindTable(board.TableScene);dock.BindTable(board.TableScene);dock.BindCollections(tabletop.Collections,!tabletop.HistoryOpen&&!ai.TableMenuOpen);tabletop.BindCollectionOcclusion(dock.DrawLandingOcclusion);tabletop.HandFlightsVisible=dock.ResourceHandVisible;
        tabletop.BindOperations(Operations(),dock.PrepareDevelopmentPurchase,ai.HistoryEntries);
        tabletop.ObserveDice(view.Events.LastOrDefault(e=>e.Kind=="RollDice")?.Sequence??0,view.LastDice1,view.LastDice2,null);
        ai.TableDiceRolling=tabletop.DiceRolling;
        tabletop.Draw(font,seat,ai.CurrentActor,PhaseName(view.Phase),view.Turn,view.OwnVictoryPoints,view.LastDice1,view.LastDice2,
            view.Players.Select(p=>new TabletopSeat{Id=p.Id,Points=p.VictoryPoints,Score=TabletopScoreModel.For(view,p.Id),Resources=p.ResourceCount,Cards=p.DevelopmentCardCount,Roads=p.Pieces.Roads,Settlements=p.Pieces.Settlements,Cities=p.Pieces.Cities,Ships=p.ShipsRemaining}).ToArray(),
            ai.IsHumanTurn&&view.Phase==GamePhase.ProductionAwaitRoll&&view.TradeOffer==null,
            ai.IsHumanTurn&&view.Phase==GamePhase.Action&&view.TradeOffer==null,
            ()=>Submit(NewCommand(CommandKind.RollDice)),()=>Submit(NewCommand(CommandKind.EndTurn)),TableSeatClick,
            ()=>{tableNotes=!tableNotes;},"航海日志",facility:dock.OpenFacility);
        GUI.enabled=enabled&&!ai.TableMenuOpen&&!tabletop.HistoryOpen&&!dock.HasOverlay;
        if(ai.IsHumanTurn&&view.TradeOffer==null)DrawTableDecision();
        GUI.enabled=enabled&&(Event.current.type==EventType.Repaint||!ai.TableMenuOpen&&!tabletop.HistoryOpen&&(dock.HasOverlay||!tabletop.BlocksOverlayInput(Event.current.mousePosition)));dock.Draw(Operations(),font);GUI.enabled=enabled;
        tabletop.HandFlightsVisible=dock.ResourceHandVisible;if((tabletop.HistoryOpen||!dock.HasOverlay)&&!ai.TableMenuOpen)tabletop.DrawHistory(font,ai.HistoryEntries,ai.HistoryPlayers);
        ai.UpdateTablePrompt(view.Turn,view.Phase.ToString(),view.Phase==GamePhase.ProductionAwaitRoll,TableInstruction(),status);GUI.matrix=Matrix4x4.identity;
    }
    void TableSeatClick(string target)
    {
        if(ai.IsHumanTurn&&view.Phase==GamePhase.RobberSteal&&view.PendingDecision!=null&&view.PendingDecision.EligibleVictimIds.Contains(target))
        {if(view.PendingDecision.Token=="pirate"){tableVictim=target;return;}var c=NewCommand(CommandKind.StealResource);c.OtherPlayerId=target;Submit(c);return;}
        if(Operations().CanTrade)dock.OpenTrade(target);
        else status="当前阶段不能发起交易；先完成本回合或待决选择。";
    }
    string TableInstruction()
    {
        if(holding)return "请选择合法位置放下棋子";
        if(view.Phase==GamePhase.SetupSettlement)return "请放置定居点";
        if(view.Phase==GamePhase.SetupRoad)return "请放置道路或船";
        if(view.Phase==GamePhase.ProductionAwaitRoll)return "请掷骰";
        if(view.Phase==GamePhase.RobberSteal)return "请选择偷取对象";
        if(view.Phase==GamePhase.Discard)return "请选择要弃掉的牌";
        if(view.Phase==GamePhase.RobberMove)return "请移动强盗或海盗";
        if(view.Phase==GamePhase.Finished)return view.WinnerPlayerId+" 获胜";
        if(view.Phase==GamePhase.Action)return "请选择你的行动";
        return status;
    }
    void TableNote(string caption,Action content)
    {
        tabletop.BeginContext(caption);
        ModernOperationUI.Styles(font);var label=new GUIStyle(ModernOperationUI.Text);
        tableInk=label;content();tabletop.EndContext();
    }
    GUIStyle tableInk;
    bool TableButton(string caption)=>ModernOperationUI.LayoutButton(caption);
    internal void ShowScenarioActions()
    {CancelOperationDraft();dock.Reset();tableNotes=true;status="选择本剧本的航海行动；右键或 Esc 关闭。";}
    void DrawTableDecision()
    {
        if(view.Phase==GamePhase.RobberSteal&&tableVictim!=null&&view.PendingDecision.Token=="pirate")
        {
            TableNote("从 "+tableVictim+" 取走",()=>{
                if(TableButton("随机抽取一张资源牌")){var c=NewCommand(CommandKind.StealResource);c.OtherPlayerId=tableVictim;Submit(c);}
                if(TableButton("取走一份布匹")){var c=NewCommand(CommandKind.StealCloth);c.OtherPlayerId=tableVictim;Submit(c);}
                if(TableButton("改选其他玩家"))tableVictim=null;
            });return;
        }
        if(preparedPurchase)
        {
            TableNote("抽取一张发展卡",()=>{
                ModernOperationUI.Info("购买费用");ModernOperationUI.Cost(new[]{0,0,1,1,1});
                ModernOperationUI.Info("新买入的行动发展卡从之后的回合起可用。");
                GUILayout.Space(12);var purchase=NewCommand(CommandKind.BuyDevelopmentCard);
                var preview=host.Preview(purchase);if(!preview.Success)ModernOperationUI.Info(preview.Message,true);
                if(ModernOperationUI.LayoutButton("确认购买 · 抽取 1 张",preview.Success,true))Submit(purchase);
                if(TableButton("取消购买")){CancelOperationDraft();dock.Reset();}
            });return;
        }
        if(preparedDevelopment.HasValue)
        {
            var kind=preparedDevelopment.Value;
            TableNote("打出 · "+DockDevelopmentName(kind),()=>{
                if(kind==DevelopmentCardKind.YearOfPlenty)
                {
                    var names=ResourceNames.Take(5).Concat(new[]{"不取"}).ToArray();
                    plentyA=ModernOperationUI.Choices("第一张资源",plentyA,names,0);
                    plentyB=ModernOperationUI.Choices("第二张资源",plentyB,names,0);
                }
                if(kind==DevelopmentCardKind.Monopoly)monopoly=ModernOperationUI.Choices("收取所有对手的这一种资源",monopoly,ResourceNames.Take(5).ToArray(),0);
                if(kind==DevelopmentCardKind.Knight)ModernOperationUI.Info("打出后移动强盗，并选择可偷取的玩家。");
                if(kind==DevelopmentCardKind.RoadBuilding)ModernOperationUI.Info("免费放置两段建设；每段分别选择合法位置。");
                var c=NewCommand(kind==DevelopmentCardKind.Knight?CommandKind.PlayKnight:kind==DevelopmentCardKind.RoadBuilding?CommandKind.PlayRoadBuilding:kind==DevelopmentCardKind.Monopoly?CommandKind.PlayMonopoly:CommandKind.PlayYearOfPlenty);
                if(kind==DevelopmentCardKind.YearOfPlenty){c.Resources=new ResourceBag();if(plentyA<5)c.Resources[(Resource)plentyA]++;if(plentyB<5)c.Resources[(Resource)plentyB]++;}
                if(kind==DevelopmentCardKind.Monopoly)c.Resource=(Resource)monopoly;
                var preview=host.Preview(c);if(!preview.Success)ModernOperationUI.Info(preview.Message,true);
                GUILayout.Space(10);if(ModernOperationUI.LayoutButton("确认打出 · "+DockDevelopmentName(kind),preview.Success,true))Submit(c);
                if(TableButton("放回手牌")){CancelOperationDraft();dock.Reset();}
            });return;
        }
        if(view.Phase==GamePhase.Discard)
        {
            var required=view.Discards.FirstOrDefault(d=>d.PlayerId==seat);if(required==null)return;
            TableNote("归还资源",()=>{
                GUILayout.Label("需弃 "+required.Amount+" 张 · 已抽出 "+dock.Selection.Selected.Sum()+" 张",tableInk);
                bool before=GUI.enabled;GUI.enabled=before&&dock.Selection.Selected.Sum()==required.Amount;
                if(TableButton("将选中的牌放回银行")){var c=NewCommand(CommandKind.DiscardResources);c.Resources=new ResourceBag();for(int i=0;i<5;i++)c.Resources[(Resource)i]=dock.Selection.Selected[i];Submit(c);}GUI.enabled=before;
            });return;
        }
        if(view.Phase==GamePhase.RobberMove&&!holding)
        {
            TableNote("拿起强盗或海盗",()=>{if(TableButton("拿起强盗"))Pickup(CommandKind.MoveRobber);if(TableButton("拿起海盗"))Pickup(CommandKind.MovePirate);if(TableButton("把海盗放到外框海域"))Submit(NewCommand(CommandKind.MovePirate,"frame"));});
        }
        if(view.Phase==GamePhase.RoadBuilding&&!holding&&view.LegalEdgeIds.Length==0&&view.LegalShipEdgeIds.Length==0)
            TableNote("道路建设",()=>{if(TableButton("结束免费建设"))Submit(NewCommand(CommandKind.FinishRoadBuilding));});
                if(!holding&&(view.Phase==GamePhase.GoldChoice||view.Phase==GamePhase.PortPlacement||view.Phase==GamePhase.SetupPort))
        {
            tabletop.RegisterContext();TabletopUI.Note(TabletopUI.ContextRect);pendingOperationScroll=GUI.BeginScrollView(new Rect(1025,380,376,240),pendingOperationScroll,new Rect(1080,278,352,290));var previousInk=text.normal.textColor;var previousSmall=small.normal.textColor;text.normal.textColor=small.normal.textColor=new Color(.25f,.13f,.06f);DrawActions();text.normal.textColor=previousInk;small.normal.textColor=previousSmall;GUI.EndScrollView();
        }
        if(tableNotes&&!holding&&view.Phase==GamePhase.Action)
        {
                        tabletop.RegisterContext();TabletopUI.Note(TabletopUI.ContextRect);pendingOperationScroll=GUI.BeginScrollView(new Rect(1025,380,376,240),pendingOperationScroll,new Rect(1080,320,352,355));var previousInk=text.normal.textColor;var previousSmall=small.normal.textColor;text.normal.textColor=small.normal.textColor=new Color(.25f,.13f,.06f);DrawSeafaring();text.normal.textColor=previousInk;small.normal.textColor=previousSmall;
            if(Button(1085,640,331,"宣告胜利"))Submit(NewCommand(CommandKind.DeclareVictory));GUI.EndScrollView();
        }
    }
}
