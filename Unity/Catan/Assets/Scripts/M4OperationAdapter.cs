using System;
using System.Linq;
using Catan.Core;
using Catan.Core.M4;
using UnityEngine;
using CommandKind = Catan.Core.M4.CommandKind;
using GamePhase = Catan.Core.M4.GamePhase;
using PlayerView = Catan.Core.M4.PlayerView;

public sealed partial class M4App
{
    readonly OperationDock dock=new OperationDock();
    readonly TabletopUI tabletop=new TabletopUI();
    bool operationActions;
    string operationKnightTarget;
    PlayerView operationView;
    OperationSnapshot operationSnapshot;
    int operationKnightLevel;
    bool operationFacilityLocked;
    bool DockBlocksMouse(Vector2? logicalMouse=null)
    {
        if(ai==null)return false;
        var p=logicalMouse??new Vector2(Input.mousePosition.x*1440f/Screen.width,(Screen.height-Input.mousePosition.y)*900f/Screen.height);
        return ai.BlocksTableInput(p)||tabletop.BlocksBoardInput(p)||OperationContextVisible&&TabletopUI.ContextRect.Contains(p)||dock.BlocksBoardInput(p);
    }
    void CancelOperationDraft(){draft=null;pickField="";operationKnightLevel=0;operationFacilityLocked=false;operationActions=false;operationKnightTarget=null;if(board!=null)board.ClearPreview();}
    bool DirectTabletopPlacement=>draft!=null&&(draft.Kind==CommandKind.SetupSettlement||draft.Kind==CommandKind.SetupRoad||draft.Kind==CommandKind.BuildSettlement||draft.Kind==CommandKind.BuildRoad||draft.Kind==CommandKind.BuildCity||draft.Kind==CommandKind.BuildWall||draft.Kind==CommandKind.RecruitKnight||draft.Kind==CommandKind.PromoteKnight||draft.Kind==CommandKind.ActivateKnight||draft.Kind==CommandKind.ChaseRobber||draft.Kind==CommandKind.MoveRobber||draft.Kind==CommandKind.PlaceFreeRoad||draft.Kind==CommandKind.MoveKnight||draft.Kind==CommandKind.DisplaceKnight);
    bool OperationContextVisible=>view!=null&&ai.IsHumanTurn&&view.TradeOffer==null&&pickField==""&&(operationActions||draft!=null&&!DirectTabletopPlacement||view.Phase==GamePhase.Discard||view.Phase==GamePhase.PendingChoice||view.Phase==GamePhase.RobberSteal||view.Phase==GamePhase.RoadBuilding);
    static int[] DockCost(int wood=0,int brick=0,int wool=0,int wheat=0,int ore=0)=>new[]{wood,brick,wool,wheat,ore,0,0,0};
    static int[] DockGoods(ResourceBag r,CommodityBag c)=>Enumerable.Range(0,8).Select(i=>i<5?r[(Resource)i]:c[(Commodity)(i-5)]).ToArray();
    bool DockKnightTargetMatches(string target)=>operationKnightLevel<2||draft==null||draft.Kind!=CommandKind.PromoteKnight||view.Board.Knights.Any(k=>k.PlayerId==seat&&k.LocationId==target&&k.Level==operationKnightLevel-1);
    bool DockLegal(CommandKind kind)=>view.LegalActions.Any(c=>c.Kind==kind);
    OperationSnapshot Operations()
    {
        if(view==null)return null;
        if(operationView!=view)
        {
            operationView=view;var own=view.Players.First(p=>p.Id==seat);
            var s=new OperationSnapshot{Scope="M4:"+seat,Seat=seat,Revision=view.Turn+":"+view.Phase+":"+view.Events.Length,Phase=PhaseName(view.Phase),GoodCount=8,Goods=DockGoods(view.OwnResources,view.OwnCommodities),Bank=DockGoods(view.Bank,view.CommodityBank),OtherPlayers=view.Players.Where(p=>p.Id!=seat).Select(p=>p.Id).ToArray()};
            for(int i=0;i<8;i++)s.Rates[i]=TradeRate(i);
            bool setup=view.Phase==GamePhase.SetupSettlement,citySetup=setup&&view.Board.Settlements.Any(p=>p.PlayerId==seat),villageSetup=setup&&!citySetup;
            bool roadSetup=view.Phase==GamePhase.SetupRoad,freeRoad=view.Phase==GamePhase.RoadBuilding;
            AddDockBuild(s,villageSetup?CommandKind.SetupSettlement:CommandKind.BuildSettlement,"定居点","settlement",own.Pieces.Settlements,villageSetup?DockCost():DockCost(1,1,1,1),!citySetup&&view.LegalVertexIds.Length>0);
            AddDockBuild(s,roadSetup?CommandKind.SetupRoad:freeRoad?CommandKind.PlaceFreeRoad:CommandKind.BuildRoad,"道路","road",own.Pieces.Roads,roadSetup||freeRoad?DockCost():DockCost(1,1),view.LegalEdgeIds.Length>0);
            AddDockBuild(s,citySetup?CommandKind.SetupSettlement:CommandKind.BuildCity,"城市","city",own.Pieces.Cities,citySetup?DockCost():DockCost(wheat:2,ore:3),citySetup?view.LegalVertexIds.Length>0:view.LegalCityVertexIds.Length>0);
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
                var b=AddDockBuild(s,CommandKind.ImproveCity,new[]{"贸易设施","政治设施","科学设施"}[track],"facility",1,cost,legal,track);b.Track=track;b.Level=own.Improvements[track];b.Detail="下一级需 "+(own.Improvements[track]+1)+" 张"+Goods[5+track]+"；三级解锁能力，四 / 五级争夺大都会";
                if(b.Level==5){Array.Clear(b.Cost,0,8);b.Detail="设施已达到最高等级";b.UnavailableReason=b.Detail;}
            }
            int cardIndex=0;
            foreach(var card in view.OwnProgressCards)
                s.Cards.Add(new OperationHandCard{Id=((int)card)+":"+cardIndex++,Name=CardName(card),Detail=CardHelp[(int)card],Progress=true,ArtIndex=(int)card,Playable=view.LegalActions.Any(c=>c.Kind==CommandKind.PlayProgressCard&&c.ProgressCard==card)});
            if(view.TradeOffer!=null)
            {
                var t=view.TradeOffer;s.Offer=new OperationOffer{Proposer=t.ProposerId,Recipient=t.OtherPlayerId,Give=DockGoods(t.Give,t.GiveCommodities),Receive=DockGoods(t.Receive,t.ReceiveCommodities),CanAccept=seat==t.OtherPlayerId,CanReject=seat==t.OtherPlayerId,CanCancel=seat==t.ProposerId};
            }
            s.PrepareBuild=b=>
            {
                CancelOperationDraft();Select((CommandKind)Enum.Parse(typeof(CommandKind),b.Id.Split(':')[0]));
                if(b.Track>=0){operationFacilityLocked=true;draft.Track=(ImprovementTrack)b.Track;Submit(draft);}
                else{operationKnightLevel=b.Id.Contains(":")?int.Parse(b.Id.Split(':')[1]):0;pickField="target";pickType=draft.Kind==CommandKind.BuildRoad||draft.Kind==CommandKind.SetupRoad||draft.Kind==CommandKind.PlaceFreeRoad?"E":"V";status="已拿起 "+b.Name+"，点击地图上的合法位置放下；右键或 Esc 放回。";}
            };
            s.PrepareCard=c=>{CancelOperationDraft();Select(CommandKind.PlayProgressCard);draft.ProgressCard=(ProgressCardKind)int.Parse(c.Id.Split(':')[0]);status="已打出 "+c.Name+"，按桌上便签选择效果。";};
            s.Cancel=CancelOperationDraft;
            s.Notify=ai.ShowTablePrompt;
            s.Trade=t=>
            {
                string error=OperationSelection.ValidateTrade(Operations(),t);if(error!=null)return error;
                var c=New(t.Bank?CommandKind.BankTrade:CommandKind.ProposeTrade);
                if(t.Bank)
                {
                    int g=Array.FindIndex(t.Give,n=>n>0),r=Array.FindIndex(t.Receive,n=>n>0);c.GiveIsCommodity=g>=5;c.ReceiveIsCommodity=r>=5;
                    if(g<5)c.GiveResource=(Resource)g;else c.GiveCommodity=(Commodity)(g-5);if(r<5)c.ReceiveResource=(Resource)r;else c.ReceiveCommodity=(Commodity)(r-5);c.GiveAmount=t.Give[g];c.ReceiveAmount=t.Receive[r];
                }
                else{c.OtherPlayerId=t.OtherPlayer;for(int i=0;i<8;i++){if(i<5){c.Give[(Resource)i]=t.Give[i];c.Receive[(Resource)i]=t.Receive[i];}else{c.GiveCommodities[(Commodity)(i-5)]=t.Give[i];c.ReceiveCommodities[(Commodity)(i-5)]=t.Receive[i];}}}
                var result=Submit(c);return result!=null&&result.Success?null:status;
            };
            s.Respond=choice=>Submit(New(choice=="accept"?CommandKind.AcceptTrade:choice=="reject"?CommandKind.RejectTrade:CommandKind.CancelTrade));operationSnapshot=s;
        }
        operationSnapshot.Status=status;operationSnapshot.CanInteract=ai.IsHumanTurn;
        operationSnapshot.CanTrade=ai.IsHumanTurn&&view.ActivePlayerId==seat&&view.Phase==GamePhase.Action&&view.PendingDecision==null&&view.TradeOffer==null;
        return operationSnapshot;
    }
    OperationBuild AddDockBuild(OperationSnapshot s,CommandKind kind,string name,string icon,int count,int[] cost,bool legal,int suffix=-1)
    {
        bool enough=Enumerable.Range(0,8).All(i=>s.Goods[i]>=cost[i]);
        var build=new OperationBuild{Id=kind.ToString()+(suffix<0?"":":"+suffix),Name=name,Icon=icon,Count=count,Cost=cost,Available=ai.IsHumanTurn&&(count>0||kind==CommandKind.BuildCity)&&legal&&enough,Detail=cost.Sum()==0?"当前阶段免费放置":kind==CommandKind.PromoteKnight?"拿起高一级骑士，放到要晋升的己方骑士上":kind==CommandKind.ImproveCity?"点击翻开下一级设施并支付商品":"拿起棋子，再点击地图上的合法位置放下",UnavailableReason=!ai.IsHumanTurn?"等待当前行动或待决席位":count==0?"库存已用尽":!enough?"缺少所需资源":!legal?"当前阶段或连接条件没有合法目标":null};if(icon=="knight")build.Level=suffix;s.Builds.Add(build);return build;
    }
    void DrawOperationLayout()
    {
        Styles();GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1440f,Screen.height/900f,1));cardArt.Begin(null);
        if(view==null){GUI.matrix=Matrix4x4.identity;return;}
        bool previousEnabled=GUI.enabled,tableEnabled=previousEnabled&&!ai.TableMenuOpen;GUI.enabled=tableEnabled&&!dock.BlocksBoardInput(Event.current.mousePosition);
        DrawBoardLabels();
        tabletop.ObserveDice(view.Events.LastOrDefault(e=>e.Kind=="RollDice")?.Sequence??0,view.LastDice1,view.LastDice2,(int)view.LastEventDie);
        ai.TableDiceRolling=tabletop.DiceRolling;
        tabletop.Draw(font,seat,Actor(view),PhaseName(view.Phase),view.Turn,view.OwnVictoryPoints,view.LastDice1,view.LastDice2,
            view.Players.Select(p=>new TabletopSeat{Id=p.Id,Points=p.VictoryPoints,Resources=p.ResourceCount,Cards=p.ProgressCardCount,Roads=p.Pieces.Roads,Settlements=p.Pieces.Settlements,Cities=p.Pieces.Cities,Improvements=p.Improvements}).ToArray(),
            ai.IsHumanTurn&&view.Phase==GamePhase.ProductionAwaitRoll,ai.IsHumanTurn&&view.Phase==GamePhase.Action&&view.PendingDecision==null&&view.TradeOffer==null,
            ()=>Submit(New(CommandKind.RollDice)),()=>Submit(New(CommandKind.EndTurn)),target=>{CancelOperationDraft();dock.OpenTrade(target);},
            ()=>{bool open=!operationActions;dock.Reset();CancelOperationDraft();operationActions=open;},"骑士与特殊行动",
            "蛮族 "+view.BarbarianPosition+" / 7 · 防御 "+view.Board.Knights.Where(k=>k.Active).Sum(k=>k.Level)+" / "+view.Board.Cities.Length);
        if(ai.IsHumanTurn&&draft==null&&view.Phase==GamePhase.RobberMove){Select(CommandKind.MoveRobber);pickField="target";pickType="T";status="拿起强盗，点击新的地块放下。";}
        if(OperationContextVisible)tabletop.DrawContext(operationActions?"棋子行动":view.PendingDecision!=null?DecisionName(view.PendingDecision.Kind):"当前选择",DrawOperationControls);
        GUI.enabled=tableEnabled;dock.Draw(Operations(),font);GUI.enabled=previousEnabled;
        ai.UpdateTablePrompt(view.Turn,view.Phase.ToString(),view.Phase==GamePhase.ProductionAwaitRoll,TableInstruction(),status);GUI.matrix=Matrix4x4.identity;
    }
    string TableInstruction()
    {
        if(view.Phase==GamePhase.Finished)return view.WinnerPlayerId+" 获胜";
        if(pickField!="")return "请选择合法位置放下棋子";
        if(view.Phase==GamePhase.ProductionAwaitRoll)return "请掷骰";
        if(view.Phase==GamePhase.SetupSettlement)return view.Board.Settlements.Any(p=>p.PlayerId==seat)?"请放置城市":"请放置定居点";
        if(view.Phase==GamePhase.SetupRoad)return "请放置道路";
        if(view.Phase==GamePhase.Discard)return "请选择要弃掉的牌";
        if(view.Phase==GamePhase.RobberMove)return "请移动强盗";
        if(view.Phase==GamePhase.RobberSteal)return "请选择偷取对象";
        if(view.Phase==GamePhase.PendingChoice)return "请完成当前选择";
        if(view.Phase==GamePhase.RoadBuilding)return "请放置免费道路";
        return "请选择你的行动";
    }
    void DrawOperationControls()
    {
        if(!ai.IsHumanTurn||view.TradeOffer!=null)return;
        if(operationActions)
        {
            Text(operationKnightTarget==null?"点击地图上的己方骑士，也可在此选择动作。":"已拿起这名骑士，请选择动作。",true);
            TabletopAction("激活骑士 · 谷物 1",CommandKind.ActivateKnight);
            TabletopAction("移动骑士",CommandKind.MoveKnight);
            TabletopAction("驱离弱骑士",CommandKind.DisplaceKnight);
            TabletopAction("驱逐强盗",CommandKind.ChaseRobber);
            if(view.CommercialHarborPlayers.Length>0)TabletopAction("商业港口 · 提供资源",CommandKind.CommercialHarborOffer);
            if(Btn("放回棋子"))CancelOperationDraft();
            return;
        }
        if(view.Phase==GamePhase.RoadBuilding)
        {
            Text("从面前拿起道路，在地图上放下；无需再确认。",true);
            if(Btn("完成道路建设")){Submit(New(CommandKind.FinishRoadBuilding));GUIUtility.ExitGUI();}
            return;
        }
        if(view.Phase==GamePhase.Discard||view.Phase==GamePhase.PendingChoice||view.Phase==GamePhase.RobberSteal){DrawControls();return;}
        if(draft!=null)DrawDraft();
    }
    void TabletopAction(string text,CommandKind kind)
    {
        if(!Btn(text))return;
        string knight=operationKnightTarget;
        CancelOperationDraft();Select(kind);
        if(kind==CommandKind.CommercialHarborOffer)return;
        bool source=kind==CommandKind.MoveKnight||kind==CommandKind.DisplaceKnight;
        if(knight!=null)
        {
            if(source){draft.SourceId=knight;pickField="target";}
            else{draft.TargetId=knight;Submit(draft);GUIUtility.ExitGUI();return;}
        }
        else pickField=source?"source":"target";
        pickType="V";status=source?"点击骑士，再点击它要前往的位置；右键放回。":"点击要行动的己方骑士。";
    }
    bool PickTabletopObject(Vector2 mouse)
    {
        if(draft!=null||!ai.IsHumanTurn||view.Phase!=GamePhase.Action||view.PendingDecision!=null||view.TradeOffer!=null)return false;
        string vertex=Pick("V",mouse);
        if(vertex!=""&&view.Board.Knights.Any(k=>k.PlayerId==seat&&k.LocationId==vertex))
        {operationKnightTarget=vertex;operationActions=true;status="选择这名骑士要执行的动作。";return true;}
        return false;
    }
    internal OperationSnapshot VerificationOperationSnapshot=>Operations();
    internal OperationDock VerificationOperationDock=>dock;
}
