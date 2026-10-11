using System;
using System.Linq;
using Catan.Core;
using UnityEngine;
using CommandKind = Catan.Core.M2.CommandKind;
using GamePhase = Catan.Core.M2.GamePhase;
using PlayerView = Catan.Core.M2.PlayerView;
using DevelopmentCardKind = Catan.Core.M2.DevelopmentCardKind;

public sealed partial class M2App
{
    internal OperationSnapshot VerificationOperationSnapshot=>Operations();
    internal OperationDock VerificationOperationDock=>dock;
    readonly OperationDock dock = new OperationDock();
    Vector2 pendingOperationScroll;
    PlayerView operationView;
    OperationSnapshot operationSnapshot;
    DevelopmentCardKind? preparedDevelopment;
    bool preparedPurchase;
    bool DockBlocksMouse()
    {
        if(ai==null)return false;
        var p=new Vector2(Input.mousePosition.x*1440f/Screen.width,(Screen.height-Input.mousePosition.y)*900f/Screen.height);
        return p.x>=1015||p.y<100||p.y>=558||new Rect(24,105,212,29).Contains(p)||dock.BlocksBoardInput(p);
    }
    void CancelOperationDraft(){preparedDevelopment=null;preparedPurchase=false;CancelPickup();}
    static int[] DockCost(int wood=0,int brick=0,int wool=0,int wheat=0,int ore=0)=>new[]{wood,brick,wool,wheat,ore,0,0,0};
    OperationSnapshot Operations()
    {
        if(view==null)return null;
        if(operationView!=view)
        {
            operationView=view;
            var own=view.Players.First(p=>p.Id==seat);
            var s=new OperationSnapshot{Scope="M2:"+seat,Seat=seat,Revision=view.Turn+":"+view.Phase+":"+view.Events.Length,Phase=PhaseName(view.Phase),GoodCount=5};
            s.Goods=DockCost(view.OwnResources.Wood,view.OwnResources.Brick,view.OwnResources.Wool,view.OwnResources.Wheat,view.OwnResources.Ore);
            s.Bank=DockCost(view.Bank.Wood,view.Bank.Brick,view.Bank.Wool,view.Bank.Wheat,view.Bank.Ore);
            for(int i=0;i<5;i++)s.Rates[i]=TradeRatio((Resource)i);
            s.OtherPlayers=view.Players.Where(p=>p.Id!=seat).Select(p=>p.Id).ToArray();
            bool setup=view.Phase==GamePhase.SetupSettlement,roadSetup=view.Phase==GamePhase.SetupRoad,freeRoad=view.Phase==GamePhase.RoadBuilding;
            AddDockBuild(s,setup?CommandKind.SetupSettlement:CommandKind.BuildSettlement,"定居点","settlement",own.Pieces.Settlements,setup?DockCost():DockCost(1,1,1,1),view.LegalVertexIds.Length>0);
            AddDockBuild(s,roadSetup?CommandKind.SetupRoad:freeRoad?CommandKind.PlaceFreeRoad:CommandKind.BuildRoad,"道路","road",own.Pieces.Roads,roadSetup||freeRoad?DockCost():DockCost(1,1),view.LegalEdgeIds.Length>0);
            AddDockBuild(s,CommandKind.BuildCity,"城市","city",own.Pieces.Cities,DockCost(wheat:2,ore:3),view.LegalCityVertexIds.Length>0);
            AddDockBuild(s,CommandKind.BuyDevelopmentCard,"发展卡","development",view.DevelopmentDeckCount,DockCost(wool:1,wheat:1,ore:1),view.Phase==GamePhase.Action&&host.Preview(NewCommand(CommandKind.BuyDevelopmentCard)).Success);
            int cardIndex=0;
            foreach(var c in view.OwnDevelopmentCards)
                s.Cards.Add(new OperationHandCard{Id=((int)c.Kind)+":"+cardIndex++,Name=DockDevelopmentName(c.Kind),Detail=c.Kind==DevelopmentCardKind.VictoryPoint?"隐藏胜利点，达到目标后可宣告胜利":c.BoughtTurn>=view.Turn?"本回合买入，下回合可用":"点击准备出牌，在右侧确认",Playable=c.Kind!=DevelopmentCardKind.VictoryPoint&&c.BoughtTurn<view.Turn&&!view.DevelopmentCardPlayedThisTurn&&(view.Phase==GamePhase.Action||view.Phase==GamePhase.ProductionAwaitRoll)&&view.TradeOffer==null&&view.ActivePlayerId==seat});
            if(view.TradeOffer!=null)
            {
                var t=view.TradeOffer;
                s.Offer=new OperationOffer{Proposer=t.ProposerId,Recipient=t.OtherPlayerId,Give=DockCost(t.Give.Wood,t.Give.Brick,t.Give.Wool,t.Give.Wheat,t.Give.Ore),Receive=DockCost(t.Receive.Wood,t.Receive.Brick,t.Receive.Wool,t.Receive.Wheat,t.Receive.Ore),CanAccept=seat==t.OtherPlayerId,CanReject=seat==t.OtherPlayerId,CanCancel=seat==t.ProposerId};
            }
            s.PrepareBuild=b=>{CancelOperationDraft();var k=(CommandKind)Enum.Parse(typeof(CommandKind),b.Id);if(k==CommandKind.BuyDevelopmentCard){preparedPurchase=true;status="已选发展卡，请在右侧确认购买。";}else Pickup(k);};
            s.PrepareCard=c=>{CancelOperationDraft();preparedDevelopment=(DevelopmentCardKind)int.Parse(c.Id.Split(':')[0]);status="已选择 "+c.Name+"，请在右侧确认出牌。";};
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
        Styles();GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1440f,Screen.height/900f,1));
        cardArt.Begin(null);
        Panel(new Rect(1015,95,425,505),panelColor);
        if(view==null){GUI.matrix=Matrix4x4.identity;return;}
        bool previousEnabled=GUI.enabled;GUI.enabled=previousEnabled&&!dock.BlocksBoardInput(Event.current.mousePosition);
        DrawBoardLabels();
        Label(1035,108,385,32,seat+" · "+PhaseName(view.Phase)+" · "+view.OwnVictoryPoints+" 分",title);
        Label(1035,145,385,38,"第 "+view.Turn+" 回合 · 骰子 "+view.LastDice1+" + "+view.LastDice2+"\n当前行动 "+view.ActivePlayerId,small);
        if(Button(24,105,102,board.TopView?"斜视 [Tab]":"俯视 [Tab]",29))board.SetCamera(!board.TopView);
        if(Button(136,105,100,"地形显隐",29)){terrain=!terrain;board.ShowTerrain(terrain);}
        if(view.TradeOffer!=null)Label(1035,204,365,66,"交易提案已放在交易桌上。\n接受、拒绝或撤回后继续回合。");
        else if(!ai.IsHumanTurn&&view.Phase!=GamePhase.Finished)Label(1035,204,365,180,ai.WaitingText);
        else if(view.Phase==GamePhase.Action||view.Phase==GamePhase.ProductionAwaitRoll)DrawOperationTurn();
        else
        {
            pendingOperationScroll=GUI.BeginScrollView(new Rect(1035,190,380,279),pendingOperationScroll,new Rect(1085,283,350,289));DrawActions();GUI.EndScrollView();
        }
        Panel(new Rect(1028,477,398,2),new Color(.2f,.29f,.30f));
        Label(1035,485,380,24,"公开席位 · 手牌只显示总数",small);
        for(int i=0;i<view.Players.Length;i++)
        {
            var p=view.Players[i];GUI.backgroundColor=BoardRenderer.SeatColor(p.Id);
            Button(1035+i*97,512,90,p.Id+" · "+p.VictoryPoints+"分\n"+p.ResourceCount+"资源",38);
        }
        GUI.backgroundColor=Color.white;
        if(Button(1035,558,120,"保存",30))SaveGame();if(Button(1164,558,120,"恢复",30))LoadGame();if(Button(1293,558,120,"新开一局",30))ai.ShowMenu();
        Panel(new Rect(0,558,1015,32),new Color(.045f,.09f,.10f));
        Label(24,563,970,24,status,small);
        GUI.enabled=previousEnabled;dock.Draw(Operations(),font);GUI.matrix=Matrix4x4.identity;
    }
    void DrawOperationTurn()
    {
        if(Button(1035,192,182,view.Phase==GamePhase.ProductionAwaitRoll?"掷骰 · 开始本回合":"已掷骰",34)&&view.Phase==GamePhase.ProductionAwaitRoll){Submit(NewCommand(CommandKind.RollDice));return;}
        if(Button(1227,192,182,"结束回合",34)){Submit(NewCommand(CommandKind.EndTurn));return;}
        if(preparedPurchase)
        {
            Label(1035,246,370,50,"购买发展卡\n羊毛 1 · 谷物 1 · 矿石 1");
            if(Button(1035,313,370,"确认购买")){Submit(NewCommand(CommandKind.BuyDevelopmentCard));preparedPurchase=false;return;}
        }
        else if(preparedDevelopment.HasValue)DrawPreparedDevelopment();
        else
        {
            Label(1035,245,375,91,holding?status:"点击下方组件会选中所需资源。\n也可先抽出资源牌，查看可建项目。\n交易在下方“交易桌”内完成。");
            if(Button(1035,370,370,"宣告胜利")){Submit(NewCommand(CommandKind.DeclareVictory));return;}
        }
        if((holding||preparedPurchase||preparedDevelopment.HasValue)&&Button(1035,427,370,"取消当前选择 [Esc]")){CancelOperationDraft();dock.Reset();}
    }
    void DrawPreparedDevelopment()
    {
        var kind=preparedDevelopment.Value;Label(1035,239,370,35,"准备出牌 · "+DockDevelopmentName(kind));
        if(kind==DevelopmentCardKind.YearOfPlenty)
        {
            if(Button(1035,280,180,"资源一："+(plentyA<5?ResourceNames[plentyA]:"不取")))plentyA=(plentyA+1)%6;
            if(Button(1225,280,180,"资源二："+(plentyB<5?ResourceNames[plentyB]:"不取")))plentyB=(plentyB+1)%6;
        }
        if(kind==DevelopmentCardKind.Monopoly&&Button(1035,280,370,"资源："+ResourceNames[monopoly]))monopoly=(monopoly+1)%5;
        Label(1035,328,370,41,"确认后进入此卡的后续选择。",small);
        if(Button(1035,376,370,"确认打出 "+DockDevelopmentName(kind)))
        {
            var c=NewCommand(kind==DevelopmentCardKind.Knight?CommandKind.PlayKnight:kind==DevelopmentCardKind.RoadBuilding?CommandKind.PlayRoadBuilding:kind==DevelopmentCardKind.Monopoly?CommandKind.PlayMonopoly:CommandKind.PlayYearOfPlenty);
            if(kind==DevelopmentCardKind.YearOfPlenty){c.Resources=new ResourceBag();if(plentyA<5)c.Resources[(Resource)plentyA]++;if(plentyB<5)c.Resources[(Resource)plentyB]++;}
            if(kind==DevelopmentCardKind.Monopoly)c.Resource=(Resource)monopoly;
            Submit(c);preparedDevelopment=null;
        }
    }
}
