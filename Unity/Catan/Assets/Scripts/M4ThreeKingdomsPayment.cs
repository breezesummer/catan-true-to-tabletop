#nullable disable
using System;
using System.Linq;
using Catan.Core;
using Catan.Core.M4;
using UnityEngine;
using Command = Catan.Core.M4.Command;
using CommandKind = Catan.Core.M4.CommandKind;
using GamePhase = Catan.Core.M4.GamePhase;

public sealed partial class M4App
{
    readonly B45PaymentContext directPayment=new B45PaymentContext();
    Command paymentCommand;
    bool paymentConfirming;
    string paymentPreviewKey;
    M7Preview paymentPreview;
    bool DirectPaymentAvailable=>ai!=null&&B45PaymentContext.Applicable(view?.ThreeKingdoms,seat);
    bool DirectPaymentCanInteract=>ai!=null&&ai.IsHumanTurn&&!ai.TablePresentationBlocked&&(!NetworkGame||ai.Network.CanInteract);
    internal bool ThreeKingdomsPaymentActive=>directPayment.Active;
    internal bool ThreeKingdomsPaymentContains(Vector2 logical)=>directPayment.Active&&ThreeKingdomsPaymentBar.Contains(logical);
    internal void CancelThreeKingdomsPayment(){directPayment.Reset();paymentCommand=null;paymentPreviewKey=null;}
    internal bool ReselectOrCancelThreeKingdomsPayment()
    {
        if(!directPayment.Active)return false;
        if(directPayment.Cards.Length>0)directPayment.ClearSelections();else{dock.Reset();CancelOperationDraft();}
        return true;
    }
    int[] PaymentCost(Command c)
    {
        switch(c.Kind)
        {
            case CommandKind.BuildRoad:return DockCost(1,1);
            case CommandKind.BuildSettlement:return DockCost(1,1,1,1);
            case CommandKind.BuildCity:return DockCost(wheat:2,ore:3);
            case CommandKind.BuildWall:return DockCost(brick:2);
            case CommandKind.RecruitKnight:case CommandKind.PromoteKnight:return DockCost(wool:1,ore:1);
            case CommandKind.ActivateKnight:return DockCost(wheat:1);
            case CommandKind.ImproveCity:var cost=new int[8];cost[5+(int)c.Track]=view.OwnImprovements[(int)c.Track]+1;return cost;
            default:return null;
        }
    }
    bool TryBeginDirectPayment(Command command,int[] knownCost=null,string title=null)
    {
        if(paymentConfirming||!DirectPaymentAvailable||command==null||command.DeclaredRealTypes!=null)return false;
        bool bank=command.Kind==CommandKind.BankTrade,trade=command.Kind==CommandKind.ProposeTrade;
        var cost=knownCost??PaymentCost(command);
        int count=bank?command.GiveAmount:trade?command.Give.Total+command.GiveCommodities.Total:cost?.Sum()??0;
        if(count<=0)return false;
        if(directPayment.Active&&paymentCommand?.Id==command.Id)return true;
        paymentCommand=B45PaymentContext.Copy(command);draft=paymentCommand;
        directPayment.Begin(view.ThreeKingdoms,seat,DockGoods(view.OwnResources,view.OwnCommodities),count,bank||trade?null:cost,title??CommandName(command.Kind));
        if(bank)directPayment.ConfigureBankTrade();
        if(!bank&&!trade)dock.Reset();
        paymentPreviewKey=null;status="点选实际支付牌，为每张牌选择用途，最后确认。";
        return directPayment.Active;
    }
    bool TryStagePaymentTarget(Command command)
    {
        if(!directPayment.Active&&!TryBeginDirectPayment(command))return false;
        paymentCommand.TargetId=command.TargetId;paymentCommand.SourceId=command.SourceId;
        draft=paymentCommand;status="目标已选中，请完成真实支付牌和用途，再确认。";return true;
    }
    // 仅在隔离预验中给于吉填入一种可能的声明，以探测几何/库存/时机；绝不写入草稿或提交。
    M7Preview PaymentAwarePreview(Command command)
    {
        if(!DirectPaymentAvailable||PaymentCost(command)==null)return Preview(command);
        if(directPayment.Active&&directPayment.Ready&&paymentCommand?.Kind==command.Kind)return Preview(BuildPaymentCommand(command));
        if(!B45PaymentContext.HasDeclaration(view.ThreeKingdoms,seat))return Preview(command);
        var cost=PaymentCost(command);var hand=DockGoods(view.OwnResources,view.OwnCommodities);
        if(hand.Sum()<cost.Sum())return new M7Preview(false,"真实手牌数量不足。");
        var probe=B45PaymentContext.Copy(command);
        probe.DeclaredRealTypes=Enumerable.Range(0,8).SelectMany(t=>Enumerable.Repeat(t,hand[t])).Take(cost.Sum()).ToArray();
        probe.DeclaredEffectiveTypes=Enumerable.Range(0,8).SelectMany(t=>Enumerable.Repeat(t,cost[t])).ToArray();
        probe.PaymentCards=Enumerable.Range(0,8).Select(t=>probe.DeclaredRealTypes.Count(v=>v==t)).ToArray();
        return Preview(probe);
    }
    bool PotentialPaymentBuild(CommandKind kind,int[] cost,int suffix)
    {
        if(!DirectPaymentAvailable||cost.Sum()==0||view.Phase!=GamePhase.Action||view.ActivePlayerId!=seat||view.PendingDecision!=null||view.TradeOffer!=null)return false;
        if(DockGoods(view.OwnResources,view.OwnCommodities).Sum()<cost.Sum())return false;
        var probe=New(kind);if(kind==CommandKind.ImproveCity){probe.Track=(ImprovementTrack)suffix;return PaymentAwarePreview(probe).Success;}
        var targets=kind==CommandKind.BuildRoad?view.Board.Edges.Select(e=>e.Id):view.Board.Vertices.Select(v=>v.Id);
        foreach(var target in targets)
        {
            if(kind==CommandKind.PromoteKnight&&suffix>1&&!view.Board.Knights.Any(k=>k.PlayerId==seat&&k.LocationId==target&&k.Level==suffix-1))continue;
            probe.TargetId=target;if(PaymentAwarePreview(probe).Success)return true;
        }
        return false;
    }
    void ConfigureDirectPaymentAvailability(OperationBuild build,bool legal,int suffix)
    {
        if(!DirectPaymentAvailable||build.Cost.Sum()==0)return;
        bool stocked=build.Icon=="city"||build.Count>0;
        bool possible=stocked&&(legal||PotentialPaymentBuild((CommandKind)Enum.Parse(typeof(CommandKind),build.Id.Split(':')[0]),build.Cost,suffix));
        build.Available=possible;build.UnavailableReason=possible?null:!stocked?"库存已用尽":"当前费用、时机或目标不满足";
    }
    string ValidatePaymentTrade(OperationSnapshot snapshot,OperationTrade trade)
    {
        if(!snapshot.CanTrade||!snapshot.CanInteract||snapshot.Offer!=null)return "当前阶段不能交易";
        if(trade?.Give?.Length!=8||trade.Receive?.Length!=8||trade.Give.Any(n=>n<0)||trade.Receive.Any(n=>n<0))return "交易数量无效";
        if(trade.Give.Sum()==0||trade.Receive.Sum()==0)return "请点选真实付出牌和换取牌";
        if(Enumerable.Range(0,8).Any(t=>trade.Give[t]>snapshot.Goods[t]))return "真实付出超过持有数量";
        if(Enumerable.Range(0,8).Any(t=>trade.Receive[t]>(t<5?19:12)||trade.Give[t]>(t<5?19:12)))return "数量超过供应";
        if(trade.Bank)
        {
            var port=dock.IsPortTrade?view.Board.Ports.FirstOrDefault(p=>p.Id==operationPortId):null;
            if(port!=null&&trade.Give.Sum()!=(port.Resource.HasValue?2:3)*trade.Receive.Sum())return "真实付出总数不符合此港口比例";
            return Enumerable.Range(0,8).Any(t=>trade.Receive[t]>snapshot.Bank[t])?"银行库存不足":null;
        }
        return trade.OtherPlayer==OperationTrade.AllPlayers&&snapshot.OtherPlayers.Length>0||snapshot.OtherPlayers.Contains(trade.OtherPlayer)?null:"请选择交易玩家";
    }
    bool StagePaymentTrade(Command command,OperationTrade trade)
    {
        if(!DirectPaymentAvailable)return false;
        if(command.Kind==CommandKind.BankTrade)command.GiveAmount=trade.Give.Sum();
        if(!TryBeginDirectPayment(command))return false;
        var selectedPort=trade.Bank&&dock.IsPortTrade?view.Board.Ports.FirstOrDefault(p=>p.Id==operationPortId):null;
        if(selectedPort!=null)directPayment.ConfigureBankTrade(selectedPort.Resource.HasValue?2:3,trade.Receive.Sum(),selectedPort.Resource.HasValue?(int)selectedPort.Resource.Value:-1);
        // 交易表已由用户挑过真实牌；保留具体展示序号，尚未替用户选择任何用途。
        for(int type=0;type<8;type++)
        {
            int needed=trade.Give[type];
            for(int ordinal=0;ordinal<Operations().Goods[type]&&needed>0;ordinal++)if(dock.Selection.IsCardSelected(type,ordinal)){directPayment.ToggleCard(type,ordinal);needed--;}
            for(int ordinal=0;ordinal<Operations().Goods[type]&&needed>0;ordinal++)if(!directPayment.IsSelected(type,ordinal)){directPayment.ToggleCard(type,ordinal);needed--;}
        }
        dock.Reset();return true;
    }
    Command BuildPaymentCommand(Command source=null)
    {
        var c=B45PaymentContext.Copy(source??paymentCommand);
        c.PaymentCards=directPayment.SelectedCounts;
        c.DeclaredRealTypes=directPayment.Declaring?directPayment.RealTypes:null;
        c.DeclaredEffectiveTypes=directPayment.Declaring?directPayment.EffectiveTypes:null;
        if(c.Kind==CommandKind.ProposeTrade)
        {
            var give=directPayment.Declaring?directPayment.SelectedCounts:directPayment.EffectiveCounts;
            for(int i=0;i<8;i++){if(i<5)c.Give[(Resource)i]=give[i];else c.GiveCommodities[(Commodity)(i-5)]=give[i];}
        }
        if(c.Kind==CommandKind.BankTrade)
        {
            int real=directPayment.RealTypes.FirstOrDefault(),use=directPayment.EffectiveTypes.FirstOrDefault();
            c.GiveIsCommodity=real>=5;if(real<5)c.GiveResource=(Resource)real;else c.GiveCommodity=(Commodity)(real-5);
            c.GiveEffectiveCardType=use;c.GiveAmount=directPayment.Cards.Length;
        }
        return c;
    }
    OperationHandSelection PaymentHandSelection=>!directPayment.Active?null:new OperationHandSelection{
        Active=true,CanSelectGood=(t,n)=>DirectPaymentCanInteract&&directPayment.CanSelect(t,n),IsGoodSelected=directPayment.IsSelected,SelectGood=(t,n)=>{if(DirectPaymentCanInteract)directPayment.ToggleCard(t,n);},
        CanSelectCard=id=>false,IsCardSelected=id=>false,SelectedGoods=()=>directPayment.Cards.Length,SelectedCards=()=>0,Reason="本次普通支付"};
    void SyncDirectPayment()
    {
        if(!directPayment.Active)return;
        if(ai.ThreeKingdomsSkillSelectionActive||view?.PendingDecision!=null||view?.TradeOffer!=null){CancelThreeKingdomsPayment();draft=null;pickField="";return;}
        directPayment.Refresh(view?.ThreeKingdoms,seat,view==null?null:DockGoods(view.OwnResources,view.OwnCommodities));
        if(!directPayment.Active){paymentCommand=null;draft=null;pickField="";paymentPreviewKey=null;}
    }
    void DrawDirectPayment()
    {
        if(!directPayment.Active||paymentCommand==null)return;
        bool previous=GUI.enabled;GUI.enabled=previous&&DirectPaymentCanInteract;
        bool ready=directPayment.Ready;
        string reason=directPayment.BankConstraintReason??directPayment.Notice??("费用用途："+directPayment.Requirement);
        if(ready&&paymentCommand.Kind==CommandKind.BankTrade&&directPayment.EffectiveTypes.Distinct().Count()!=1){ready=false;reason="一笔银行交换须使用同一种用途。";}
        if(ready)
        {
            var c=BuildPaymentCommand();string key=paymentCommand.TargetId+"|"+paymentCommand.SourceId+"|"+directPayment.Declaring+"|"+string.Join(",",directPayment.RealTypes)+"|"+string.Join(",",directPayment.EffectiveTypes);
            if(paymentPreviewKey!=key){paymentPreview=Preview(c);paymentPreviewKey=key;}
            ready=paymentPreview.Success;reason=ready?"已通过规则预验；确认后执行。":paymentPreview.Message;
        }
        var focused=directPayment.Current;Rect? anchor=focused==null?(Rect?)null:dock.HandCardRect(focused.RealType,focused.Ordinal);
        ThreeKingdomsPaymentBar.Draw(directPayment,anchor,ready&&DirectPaymentCanInteract,reason,()=>{
            var command=BuildPaymentCommand();var preview=Preview(command);
            if(!preview.Success){directPayment.Unlock(preview.Message);return;}
            if(!directPayment.Lock())return;
            paymentConfirming=true;try{Submit(command);}finally{paymentConfirming=false;}
        },()=>{dock.Reset();CancelOperationDraft();});GUI.enabled=previous;
    }
}
