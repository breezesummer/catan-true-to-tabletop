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
    PlayerView operationView;
    OperationSnapshot operationSnapshot;
    int operationKnightLevel;
    bool operationFacilityLocked;
    bool DockBlocksMouse()
    {
        if(ai==null)return false;
        var p=new Vector2(Input.mousePosition.x*1440f/Screen.width,(Screen.height-Input.mousePosition.y)*900f/Screen.height);
        return p.x>=1015||p.y<100||p.y>=558||new Rect(24,105,212,29).Contains(p)||dock.BlocksBoardInput(p);
    }
    void CancelOperationDraft(){draft=null;pickField="";operationKnightLevel=0;operationFacilityLocked=false;if(board!=null)board.ClearPreview();}
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
                if(b.Track>=0){operationFacilityLocked=true;draft.Track=(ImprovementTrack)b.Track;status="已选 "+b.Name+"，在右侧确认升级。";}
                else{operationKnightLevel=b.Id.Contains(":")?int.Parse(b.Id.Split(':')[1]):0;pickField="target";pickType=draft.Kind==CommandKind.BuildRoad||draft.Kind==CommandKind.SetupRoad||draft.Kind==CommandKind.PlaceFreeRoad?"E":"V";status="在棋盘选择 "+b.Name+" 的目标，再确认。";}
            };
            s.PrepareCard=c=>{CancelOperationDraft();Select(CommandKind.PlayProgressCard);draft.ProgressCard=(ProgressCardKind)int.Parse(c.Id.Split(':')[0]);status="已选择 "+c.Name+"，在右侧设置目标并确认。";};
            s.Cancel=CancelOperationDraft;
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
        var build=new OperationBuild{Id=kind.ToString()+(suffix<0?"":":"+suffix),Name=name,Icon=icon,Count=count,Cost=cost,Available=ai.IsHumanTurn&&(count>0||kind==CommandKind.BuildCity)&&legal&&enough,Detail=cost.Sum()==0?"当前阶段免费放置":kind==CommandKind.PromoteKnight?"选择低一级的己方骑士进行晋升":"点击选择费用，然后选择棋盘目标",UnavailableReason=!ai.IsHumanTurn?"等待当前行动或待决席位":count==0?"库存已用尽":!enough?"缺少所需资源":!legal?"当前阶段或连接条件没有合法目标":null};if(icon=="knight")build.Level=suffix;s.Builds.Add(build);return build;
    }
    void DrawOperationLayout()
    {
        Styles();GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1440f,Screen.height/900f,1));cardArt.Begin(null);
        Panel(new Rect(1015,95,425,505),new Color(.065f,.12f,.135f));if(view==null){GUI.matrix=Matrix4x4.identity;return;}
        bool previousEnabled=GUI.enabled;GUI.enabled=previousEnabled&&!dock.BlocksBoardInput(Event.current.mousePosition);
        DrawBoardLabels();
        GUI.Label(new Rect(1035,107,385,33),seat+" · "+PhaseName(view.Phase)+" · "+view.OwnVictoryPoints+" 分",heading);
        GUI.Label(new Rect(1035,145,385,42),"第 "+view.Turn+" 回合 · 骰子 "+view.LastDice1+" + "+view.LastDice2+" · "+EventName(view.LastEventDie)+"\n蛮族 "+view.BarbarianPosition+" / 7 · 防御 "+view.Board.Knights.Where(k=>k.Active).Sum(k=>k.Level)+" / 力量 "+view.Board.Cities.Length,small);
        if(GUI.Button(new Rect(24,105,102,29),board.TopView?"斜视 [Tab]":"俯视 [Tab]",button))board.SetCamera(!board.TopView);
        if(GUI.Button(new Rect(136,105,100,29),"地形显隐",button)){terrain=!terrain;board.ShowTerrain(terrain);}
        GUILayout.BeginArea(new Rect(1032,193,394,279));scroll=GUILayout.BeginScrollView(scroll);DrawOperationControls();GUILayout.EndScrollView();GUILayout.EndArea();
        Panel(new Rect(1028,477,398,2),new Color(.2f,.29f,.30f));
        GUI.Label(new Rect(1035,484,380,23),"公开席位 · 手牌只显示总数",small);
        for(int i=0;i<view.Players.Length;i++)
        {
            var p=view.Players[i];GUI.backgroundColor=BoardRenderer.SeatColor(p.Id);GUI.Button(new Rect(1035+i*97,512,90,38),p.Id+" · "+p.VictoryPoints+"分\n"+p.ResourceCount+"手牌",button);
        }
        GUI.backgroundColor=Color.white;
        if(GUI.Button(new Rect(1035,558,120,30),"保存",button))SaveGame();if(GUI.Button(new Rect(1164,558,120,30),"恢复",button))LoadGame();if(GUI.Button(new Rect(1293,558,120,30),"新开一局",button))ai.ShowMenu();
        Panel(new Rect(0,558,1015,32),new Color(.045f,.09f,.10f));GUI.Label(new Rect(24,563,970,24),status,small);
        GUI.enabled=previousEnabled;dock.Draw(Operations(),font);GUI.matrix=Matrix4x4.identity;
    }
    void DrawOperationControls()
    {
        if(view.TradeOffer!=null){Text("交易提案已放在交易桌上。接受、拒绝或撤回后继续回合。");return;}
        if(!ai.IsHumanTurn&&view.Phase!=GamePhase.Finished){Text(ai.WaitingText);return;}
        if(view.Phase!=GamePhase.Action&&view.Phase!=GamePhase.ProductionAwaitRoll){DrawControls();return;}
        GUILayout.BeginHorizontal();if(Btn(view.Phase==GamePhase.ProductionAwaitRoll?"掷骰":"已掷骰")&&view.Phase==GamePhase.ProductionAwaitRoll){Submit(New(CommandKind.RollDice));GUIUtility.ExitGUI();}if(Btn("结束回合")){Submit(New(CommandKind.EndTurn));GUIUtility.ExitGUI();}GUILayout.EndHorizontal();
        if(draft!=null){DrawDraft();return;}
        Text("下方组件选择建造；骑士在这里激活与行动。",true);
        GUILayout.BeginHorizontal();ActionButton("激活骑士 · 谷物 1",CommandKind.ActivateKnight);ActionButton("移动骑士",CommandKind.MoveKnight);GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();ActionButton("驱离弱骑士",CommandKind.DisplaceKnight);ActionButton("驱逐强盗",CommandKind.ChaseRobber);GUILayout.EndHorizontal();
        if(view.CommercialHarborPlayers.Length>0)ActionButton("商业港口 · 提供资源",CommandKind.CommercialHarborOffer);
        Text(status,true);
    }
    internal OperationSnapshot VerificationOperationSnapshot=>Operations();
    internal OperationDock VerificationOperationDock=>dock;
}
