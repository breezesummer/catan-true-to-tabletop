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
    void CancelOperationDraft(){preparedDevelopment=null;preparedPurchase=false;showSeafaring=false;tableNotes=false;tableVictim=null;CancelPickup();}
    static int[] DockCost(int wood=0,int brick=0,int wool=0,int wheat=0,int ore=0)=>new[]{wood,brick,wool,wheat,ore,0,0,0};
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
            for(int i=0;i<5;i++)s.Rates[i]=TradeRatio((Resource)i);
            s.OtherPlayers=view.Players.Where(p=>p.Id!=seat).Select(p=>p.Id).ToArray();
            bool setup=view.Phase==GamePhase.SetupSettlement,roadSetup=view.Phase==GamePhase.SetupRoad,freeRoad=view.Phase==GamePhase.RoadBuilding;
            AddDockBuild(s,setup?CommandKind.SetupSettlement:CommandKind.BuildSettlement,"定居点","settlement",own.Pieces.Settlements,setup?DockCost():DockCost(1,1,1,1),view.LegalVertexIds.Length>0);
            AddDockBuild(s,roadSetup?CommandKind.SetupRoad:freeRoad?CommandKind.PlaceFreeRoad:CommandKind.BuildRoad,"道路","road",own.Pieces.Roads,roadSetup||freeRoad?DockCost():DockCost(1,1),view.LegalEdgeIds.Length>0);
            AddDockBuild(s,CommandKind.BuildCity,"城市","city",own.Pieces.Cities,DockCost(wheat:2,ore:3),view.LegalCityVertexIds.Length>0);
            AddDockBuild(s,CommandKind.BuyDevelopmentCard,"发展卡","development",view.DevelopmentDeckCount,DockCost(wool:1,wheat:1,ore:1),view.Phase==GamePhase.Action&&host.Preview(NewCommand(CommandKind.BuyDevelopmentCard)).Success);
            AddDockBuild(s,roadSetup?CommandKind.SetupShip:freeRoad?CommandKind.PlaceFreeShip:CommandKind.BuildShip,"船","ship",own.ShipsRemaining,roadSetup||freeRoad?DockCost():DockCost(wood:1,wool:1),view.LegalShipEdgeIds.Length>0);
            int cardIndex=0;
            foreach(var c in view.OwnDevelopmentCards)
                s.Cards.Add(new OperationHandCard{Id=((int)c.Kind)+":"+cardIndex++,Name=DockDevelopmentName(c.Kind),Detail=c.Kind==DevelopmentCardKind.VictoryPoint?"隐藏胜利点，达到目标后可宣告胜利":c.BoughtTurn>=view.Turn?"本回合买入，下回合可用":"点击准备出牌，在桌面确认",Playable=c.Kind!=DevelopmentCardKind.VictoryPoint&&c.BoughtTurn<view.Turn&&!view.DevelopmentCardPlayedThisTurn&&(view.Phase==GamePhase.Action||view.Phase==GamePhase.ProductionAwaitRoll)&&view.TradeOffer==null&&view.ActivePlayerId==seat});
            if(view.TradeOffer!=null)
            {
                var t=view.TradeOffer;
                s.Offer=new OperationOffer{Proposer=t.ProposerId,Recipient=t.OtherPlayerId,Give=DockCost(t.Give.Wood,t.Give.Brick,t.Give.Wool,t.Give.Wheat,t.Give.Ore),Receive=DockCost(t.Receive.Wood,t.Receive.Brick,t.Receive.Wool,t.Receive.Wheat,t.Receive.Ore),CanAccept=seat==t.OtherPlayerId,CanReject=seat==t.OtherPlayerId,CanCancel=seat==t.ProposerId};
            }
            s.PrepareBuild=b=>{CancelOperationDraft();var k=(CommandKind)Enum.Parse(typeof(CommandKind),b.Id);if(k==CommandKind.BuyDevelopmentCard){preparedPurchase=true;status="已选发展卡，请在桌面确认购买。";}else Pickup(k);};
            s.PrepareCard=c=>{CancelOperationDraft();preparedDevelopment=(DevelopmentCardKind)int.Parse(c.Id.Split(':')[0]);status="已选择 "+c.Name+"，请在桌面确认出牌。";};
            s.Cancel=CancelOperationDraft;
            s.Trade=t=>
            {
                var error=OperationSelection.ValidateTrade(Operations(),t);if(error!=null)return error;
                var c=NewCommand(t.Bank?CommandKind.BankTrade:CommandKind.ProposeTrade);
                if(t.Bank){int g=Array.FindIndex(t.Give,n=>n>0),r=Array.FindIndex(t.Receive,n=>n>0);c.GiveResource=(Resource)g;c.ReceiveResource=(Resource)r;c.GiveAmount=t.Give[g];c.ReceiveAmount=t.Receive[r];}
                else{c.Give=new ResourceBag();c.Receive=new ResourceBag();c.OtherPlayerId=t.OtherPlayer;for(int i=0;i<5;i++){c.Give[(Resource)i]=t.Give[i];c.Receive[(Resource)i]=t.Receive[i];}}
                var result=Submit(c);return result!=null&&result.Success?null:status;
            };
            s.Respond=choice=>Submit(NewCommand(choice=="accept"?CommandKind.AcceptTrade:choice=="reject"?CommandKind.RejectTrade:CommandKind.CancelTrade));
            operationSnapshot=s;
        }
        operationSnapshot.Status=status;operationSnapshot.CanInteract=ai.IsHumanTurn;
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
        bool enabled=GUI.enabled;GUI.enabled=enabled&&!dock.HasOverlay&&!ai.TableMenuOpen;
        DrawBoardLabels();
        tabletop.Draw(font,seat,ai.CurrentActor,PhaseName(view.Phase),view.Turn,view.OwnVictoryPoints,view.LastDice1,view.LastDice2,
            view.Players.Select(p=>new TabletopSeat{Id=p.Id,Points=p.VictoryPoints,Resources=p.ResourceCount,Cards=p.DevelopmentCardCount,Roads=p.Pieces.Roads,Settlements=p.Pieces.Settlements,Cities=p.Pieces.Cities,Ships=p.ShipsRemaining}).ToArray(),
            ai.IsHumanTurn&&view.Phase==GamePhase.ProductionAwaitRoll&&view.TradeOffer==null,
            ai.IsHumanTurn&&view.Phase==GamePhase.Action&&view.TradeOffer==null,
            ()=>Submit(NewCommand(CommandKind.RollDice)),()=>Submit(NewCommand(CommandKind.EndTurn)),TableSeatClick,
            ()=>{tableNotes=!tableNotes;},"航海日志");
        tabletop.DrawStatus(ai.IsHumanTurn?TableInstruction():ai.WaitingText);
        if(ai.IsHumanTurn&&view.TradeOffer==null)DrawTableDecision();
        GUI.enabled=enabled&&!ai.TableMenuOpen;dock.Draw(Operations(),font);GUI.enabled=enabled;GUI.matrix=Matrix4x4.identity;
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
        if(holding)return "棋子已拿起 · 移向亮点后单击放下 · 右键 / Esc 放回";
        if(view.Phase==GamePhase.SetupSettlement)return "从面前拿起定居点，放到地图上的合法交点";
        if(view.Phase==GamePhase.SetupRoad)return "从面前拿起道路或船，连接刚放下的定居点";
        if(view.Phase==GamePhase.ProductionAwaitRoll)return "轮到你了 · 点击桌上的两颗骰子";
        if(view.Phase==GamePhase.RobberSteal)return "点击一名相邻对手的牌背，随机抽取资源";
        if(view.Phase==GamePhase.Discard)return "点选面前需要弃掉的资源牌，再确认弃牌";
        if(view.Phase==GamePhase.RobberMove)return "拿起地图上的强盗，放到另一块地形";
        return status;
    }
    void TableNote(string caption,Action content)
    {
        tabletop.BeginContext(caption);
        var label=new GUIStyle(text);label.normal.textColor=new Color(.25f,.13f,.06f);
        tableInk=label;content();tabletop.EndContext();
    }
    GUIStyle tableInk;
    bool TableButton(string caption)=>GUILayout.Button(caption,button,GUILayout.MinHeight(32));
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
                GUILayout.Label("付出羊毛、谷物、矿石各一张。",tableInk);
                if(TableButton("把资源交回银行 · 抽牌"))Submit(NewCommand(CommandKind.BuyDevelopmentCard));
                if(TableButton("放回")){CancelOperationDraft();dock.Reset();}
            });return;
        }
        if(preparedDevelopment.HasValue)
        {
            var kind=preparedDevelopment.Value;
            TableNote("打出 · "+DockDevelopmentName(kind),()=>{
                if(kind==DevelopmentCardKind.YearOfPlenty)
                {if(TableButton("第一张："+(plentyA<5?ResourceNames[plentyA]:"不取")))plentyA=(plentyA+1)%6;if(TableButton("第二张："+(plentyB<5?ResourceNames[plentyB]:"不取")))plentyB=(plentyB+1)%6;}
                if(kind==DevelopmentCardKind.Monopoly&&TableButton("收取："+ResourceNames[monopoly]))monopoly=(monopoly+1)%5;
                if(TableButton("将这张卡放到桌上"))
                {
                    var c=NewCommand(kind==DevelopmentCardKind.Knight?CommandKind.PlayKnight:kind==DevelopmentCardKind.RoadBuilding?CommandKind.PlayRoadBuilding:kind==DevelopmentCardKind.Monopoly?CommandKind.PlayMonopoly:CommandKind.PlayYearOfPlenty);
                    if(kind==DevelopmentCardKind.YearOfPlenty){c.Resources=new ResourceBag();if(plentyA<5)c.Resources[(Resource)plentyA]++;if(plentyB<5)c.Resources[(Resource)plentyB]++;}
                    if(kind==DevelopmentCardKind.Monopoly)c.Resource=(Resource)monopoly;Submit(c);
                }
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
