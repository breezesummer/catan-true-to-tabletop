using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Catan.Core.ThreeKingdoms;

namespace Catan.Core.M4
{
    public sealed partial class Command
    {
        [DataMember(Order=306,EmitDefaultValue=false)] public int[] PaymentCards { get; set; }
        [DataMember(Order=307,EmitDefaultValue=false)] public int? GiveEffectiveCardType { get; set; }
    }
    public sealed partial class GameState
    {
        [DataMember(Order=316,EmitDefaultValue=false)] public B4VirtualUse ThreeKingdomsVirtualUse { get; set; }
        [DataMember(Order=317,EmitDefaultValue=false)] public bool ThreeKingdomsDeferredVirtual { get; set; }
        [IgnoreDataMember] internal string[] B4TradeConversionHistoryIds { get; set; }
        [IgnoreDataMember] internal int[] B4PaymentSelection { get; set; }
        [IgnoreDataMember] internal System.Collections.Generic.Dictionary<string,int[]> B4SkillHandDelta { get; set; }
    }
    public sealed partial class CitiesKnightsGameSession
    {
        private sealed partial class B2Host : IThreeKingdomsB4Host, IThreeKingdomsB4VirtualCostHost
        {
            public int GetHandLimit(string playerId) => Math.Max(0,7+2*game.Walls.Count(w=>w.PlayerId==playerId)-B2SkillEngine.B4HandLimitPenalty(game.ThreeKingdoms,playerId));
            public B4VirtualUse[] GetVirtualProgressUses(string playerId,string timing) => session.B4VirtualUses(game,playerId,timing);
            public void UseVirtualProgress(string playerId,B4VirtualUse use) => session.B4UseVirtual(game,playerId,use);
            public bool CanUseVirtualProgressAfterPayment(string playerId,B4VirtualUse use,int[] consumedCards)
            {
                if(use==null||consumedCards==null||consumedCards.Length!=8||consumedCards.Any(n=>n<0))return false;
                var currentHand=GetHand(playerId);if(Enumerable.Range(0,8).Any(i=>currentHand[i]<consumedCards[i]))return false;
                // 用途已由 GetVirtualProgressUses 预检；只有药物与起重机会另外支付手牌。
                if(use.CardId!="Medicine"&&use.CardId!="Crane")return true;
                var probe=game.CopyForTransaction();var host=session.CardHost(probe);
                host.RemoveHand(playerId,consumedCards);host.AddBank(consumedCards);
                var general=probe.ThreeKingdoms.Generals.FirstOrDefault(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId==playerId);
                if(general?.GeneralId=="tk-g41") { general.Counters=general.Counters.Where(c=>c.Key!="miewu"&&c.Key!="wuku").Concat(new[]{new GeneralCounter{Key="miewu",TurnInstanceId=probe.ThreeKingdoms.Clock.TurnInstanceId,Value=1},new GeneralCounter{Key="wuku",Value=0}}).ToArray(); }
                try { var c=Json.Read<Command>(use.PayloadJson); session.ApplyProgressCommand(probe,probe.Players.Single(p=>p.Id==playerId),c,true,probe.ThreeKingdoms.Clock.Phase==ThreeKingdomsPhase.Preparation);return true; }
                catch(RuleException){return false;}catch(InvalidOperationException){return false;}
            }
        }
        private B4VirtualUse[] B4VirtualUses(GameState s,string player,string timing)
        {
            if(!B4(s)||s.ActivePlayerId!=player||s.PendingDecision!=null||s.TradeOffer!=null) return Array.Empty<B4VirtualUse>();
            var probe=s.CopyForTransaction(); var owner=probe.Players.Single(p=>p.Id==player);
            owner.ProgressCards=((ProgressCardKind[])Enum.GetValues(typeof(ProgressCardKind))).Where(c=>!ProgressCatalog.IsVictoryPoint(c)).ToArray();
            // 只在隔离副本枚举用途；真身、牌库及权威随机完全不变。
            if((timing=="prepare"||timing=="start")) probe.Phase=GamePhase.ProductionAwaitRoll;
            var result=new List<B4VirtualUse>();
            foreach(var c in B4VirtualCandidates(probe,owner))
            {
                if(c.ProgressCard==ProgressCardKind.Alchemy&&probe.ThreeKingdoms.Dice?.FixedResult!=null) continue;
                c.Id="virtual-probe";c.WindowId=probe.ThreeKingdoms.WindowId;
                try
                {
                    // 数字范围及圆片候选已纯验证；这些用途没有额外费用或目标响应。
                    if(c.ProgressCard==ProgressCardKind.Alchemy)
                    { if(probe.Phase!=GamePhase.ProductionAwaitRoll||probe.AlchemyDice1!=0)continue; }
                    else if(c.ProgressCard!=ProgressCardKind.Invention)
                    {
                        var check=probe.CopyForTransaction();
                        B2TargetPlayers(check,c);
                        ApplyProgressCommand(check,check.Players.Single(p=>p.Id==player),c,true,(timing=="prepare"||timing=="start"));
                    }
                    var payload=Json.Write(c);
                    result.Add(new B4VirtualUse {CardId=c.ProgressCard.ToString(),OptionId=Json.Hash(payload),PayloadJson=payload,
                        Label=B4VirtualLabel(c),
                        TargetPlayerIds=c.OtherPlayerId==null?Array.Empty<string>():new[]{c.OtherPlayerId},
                        CardType=c.ProgressCard==ProgressCardKind.ResourceMonopoly?(int)c.Resource:c.ProgressCard==ProgressCardKind.TradeMonopoly?5+(int)c.Commodity:-1});
                }
                catch(RuleException) { }
            }
            return result.ToArray();
        }
        private IEnumerable<Command> B4VirtualCandidates(GameState s,PlayerState owner)
        {
            var expanded=new HashSet<ProgressCardKind>();
            foreach(var candidate in ProgressCandidates(s,owner).Where(c=>c.Kind==CommandKind.PlayProgressCard))
            {
                var card=candidate.ProgressCard;
                if(card!=ProgressCardKind.Alchemy&&card!=ProgressCardKind.Invention&&card!=ProgressCardKind.Smithing) {yield return candidate;continue;}
                if(!expanded.Add(card))continue;
                if(card==ProgressCardKind.Alchemy)
                {
                    for(int first=1;first<=6;first++)for(int second=1;second<=6;second++)
                    {var c=Json.Copy(candidate);c.ChosenDice1=first;c.ChosenDice2=second;yield return c;}
                }
                else if(card==ProgressCardKind.Invention)
                {
                    var tiles=s.Tiles.Where(t=>SwappableNumber(t.Number)).ToArray();
                    for(int first=0;first<tiles.Length;first++)for(int second=first+1;second<tiles.Length;second++)
                    {var c=Json.Copy(candidate);c.SourceId=tiles[first].Id;c.TargetId=tiles[second].Id;yield return c;}
                }
                else
                {
                    var knights=s.Knights.Where(k=>k.PlayerId==owner.Id).ToArray();
                    for(int first=0;first<knights.Length;first++)
                    {
                        var single=Json.Copy(candidate);single.TargetIds=new[]{knights[first].LocationId};yield return single;
                        for(int second=first+1;second<knights.Length;second++)
                        {var c=Json.Copy(candidate);c.TargetIds=new[]{knights[first].LocationId,knights[second].LocationId};yield return c;}
                    }
                }
            }
        }
        private static string B4VirtualLabel(Command c)
        {
            var label=DevelopmentCardText.Name(c.ProgressCard.ToString());
            var names=new[]{"木材","砖块","羊毛","粮食","矿石","布匹","钱币","纸张"};
            if(c.ProgressCard==ProgressCardKind.Alchemy)label+=" "+c.ChosenDice1+" + "+c.ChosenDice2;
            if(c.ProgressCard==ProgressCardKind.Crane)label+="："+new[]{"贸易","政治","科技"}[(int)c.Track];
            if(c.ProgressCard==ProgressCardKind.ResourceMonopoly||c.ProgressCard==ProgressCardKind.MerchantFleet&&!c.ChooseCommodity)label+="："+names[(int)c.Resource];
            if(c.ProgressCard==ProgressCardKind.TradeMonopoly||c.ProgressCard==ProgressCardKind.MerchantFleet&&c.ChooseCommodity)label+="："+names[5+(int)c.Commodity];
            if(c.SourceId!=null)label+=" "+c.SourceId+" →";
            if(c.TargetId!=null)label+=" "+c.TargetId;
            if(c.TargetIds?.Length>0)label+=" "+string.Join("、",c.TargetIds);
            if(c.OtherPlayerId!=null)label+=" → "+c.OtherPlayerId;
            return label;
        }
        private void B4UseVirtual(GameState s,string player,B4VirtualUse use)
        {
            Require(B4(s)&&use!=null&&s.ThreeKingdomsVirtualUse==null,"VirtualUsePending","已有未完成的虚拟牌。");
            var c=Json.Read<Command>(use.PayloadJson);
            Require(c.Kind==CommandKind.PlayProgressCard&&c.PlayerId==player&&c.ProgressCard.ToString()==use.CardId&&!ProgressCatalog.IsVictoryPoint(c.ProgressCard),"InvalidVirtualUse","虚拟用牌内容无效。");
            bool prepare=s.ThreeKingdoms.Clock.Phase==ThreeKingdomsPhase.Preparation;
            var validation=s.CopyForTransaction();
            ApplyProgressCommand(validation,validation.Players.Single(p=>p.Id==player),c,true,prepare);
            c.Id="virtual-"+s.ThreeKingdoms.NextId++;
            s.ThreeKingdomsVirtualUse=Json.Copy(use);
            foreach(var target in B2TargetPlayers(s,c)) B2SkillEngine.OnTargeted(CardHost(s),c.Id,player,target,c.ProgressCard.ToString());
            if(B2Busy(s)) { s.ThreeKingdomsDeferredCommand=c;s.ThreeKingdomsDeferredVirtual=true; }
            else ApplyB2Native(s,c,true,prepare);
        }
        private void FinishB4Virtual(GameState s)
        {
            if(!B4(s)||s.ThreeKingdomsVirtualUse==null||s.ThreeKingdomsDeferredCommand!=null||s.ThreeKingdomsProgressResolutions?.Length>0||B2Busy(s)||s.PendingDecision!=null||s.DecisionQueue.Length>0) return;
            var use=s.ThreeKingdomsVirtualUse;s.ThreeKingdomsVirtualUse=null;
            B2SkillEngine.OnB4VirtualProgressCompleted(CardHost(s),Json.Read<Command>(use.PayloadJson).PlayerId,use);
        }
        private void PayB4(GameState s,PlayerState p,ResourceBag cost)
        { PayB4(s,p,Enumerable.Range(0,8).Select(i=>i<5?cost[(Resource)i]:0).ToArray()); }
        private void PayB4(GameState s,PlayerState p,int[] needs)
        {
            if(s.B4DeclaredPayment!=null) { PayB4Declared(s,p,needs); return; }
            Require(!B2SkillEngine.IsB4HandLossForbidden(s.ThreeKingdoms,p.Id),"HandLossForbidden","本次出牌阶段不能失去牌。");
            var host=CardHost(s);var before=host.GetHand(p.Id);
            var matrix=B4PaymentAllocator.Allocate(s.ThreeKingdoms,p.Id,before,needs,s.B4PaymentSelection);
            Require(matrix!=null,"InsufficientResources","真实手牌不能满足本次支付用途。");
            var paid=Enumerable.Range(0,8).Select(real=>Enumerable.Range(0,8).Sum(use=>matrix[real*8+use])).ToArray();
            var eventId="tk-pay-"+s.ThreeKingdoms.NextId++;
            var conversionHistoryIds=new List<string>();
            for(int real=0;real<8;real++) for(int use=0;use<8;use++) if(real!=use&&matrix[real*8+use]>0)
            {
                // 历史编号会公开，只携带批内序号，不能编码真实牌或用途。
                var conversionId=eventId+":conversion-"+conversionHistoryIds.Count;
                B2SkillEngine.CommitCardConversion(host,conversionId,p.Id,real,use);conversionHistoryIds.Add(conversionId);
            }
            host.RemoveHand(p.Id,paid);host.AddBank(paid);
            var payment=new B3NativePayment {EventId=eventId,PlayerId=p.Id,Cards=paid,BeforeCount=before.Sum(),AfterCount=before.Sum()-paid.Sum()};
            s.ThreeKingdomsNativePayments=(s.ThreeKingdomsNativePayments??Array.Empty<B3NativePayment>()).Concat(new[]{payment}).ToArray();
            B2SkillEngine.HoldReturnedCards(host,p.Id,"payment",eventId,paid);
            var changes=new[]{new B2HandChange {PlayerId=p.Id,BeforeCount=before.Sum(),AfterCount=payment.AfterCount,LostCount=paid.Sum(),LostCards=paid}};
            B2SkillEngine.OnCardsMoved(host,eventId,changes);
            foreach(var conversionId in conversionHistoryIds)
                B4SkillHistoryEngine.LinkCardFlow(s.ThreeKingdoms,conversionId,eventId,changes);
        }
        private void BankTradeB4(GameState s,PlayerState p,Command c)
        {
            int real=c.GiveIsCommodity?5+(int)c.GiveCommodity:(int)c.GiveResource, effective=c.GiveEffectiveCardType??real;
            Require(real>=0&&real<8&&effective>=0&&effective<8,"InvalidTrade","请选择有效的真实牌与用途。");
            var receive=new int[8]; bool bulk=c.Receive?.Total>0||c.ReceiveCommodities?.Total>0;
            if(bulk) { Require((c.Receive==null||ValidBag(c.Receive))&&(c.ReceiveCommodities==null||ValidBag(c.ReceiveCommodities)),"InvalidTrade","请求牌数量无效。"); for(int i=0;i<8;i++)receive[i]=i<5?(c.Receive??new ResourceBag())[(Resource)i]:(c.ReceiveCommodities??new CommodityBag())[(Commodity)(i-5)]; }
            else { int type=c.ReceiveIsCommodity?5+(int)c.ReceiveCommodity:(int)c.ReceiveResource;Require(type>=0&&type<8,"InvalidTrade","请求牌种无效。");receive[type]=c.ReceiveAmount; }
            int count=receive.Sum(); Require(count>0&&count<=19&&receive.All(n=>n>=0)&&receive[effective]==0&&c.GiveAmount>0&&c.GiveAmount<=19,"InvalidTrade","交换数量或牌种无效。");
            int rate=effective<5?TradeRate(s,p.Id,(Resource)effective):CommodityTradeRate(s,p.Id,(Commodity)(effective-5)); int wenji=B2SkillEngine.WenjiTradeRatio(s.ThreeKingdoms,p.Id,real);if(wenji>0)rate=Math.Min(rate,wenji);
            bool generic=ports.Any(port=>port.Resource==null&&Buildings(s).Any(b=>b.PlayerId==p.Id&&port.Vertices.Contains(b.LocationId)));
            Require(c.GiveAmount==4*count||c.GiveAmount==rate*count||generic&&c.GiveAmount==3*count,"InvalidRatio","交换比例无效。");
            var host=CardHost(s);var bank=host.GetBank();Require(Enumerable.Range(0,8).All(i=>bank[i]>=receive[i]),"InsufficientBank","银行没有所需牌。");
            var selected=c.PaymentCards==null?new int[8]:(int[])c.PaymentCards.Clone();if(c.PaymentCards==null)selected[real]=c.GiveAmount;
            var needs=new int[8];needs[effective]=c.GiveAmount;s.B4PaymentSelection=selected;PayB4(s,p,needs);s.B4PaymentSelection=null;
            // 整笔银行交换只有一次费用批次和一次实际获得；复合收牌不递归拆成多笔交易。
            for(int i=0;i<8;i++)if(receive[i]>0){if(i<5)Transfer(s.Bank,p.Resources,(Resource)i,receive[i]);else Transfer(s.CommodityBank,p.Commodities,(Commodity)(i-5),receive[i]);}
        }
    }
}
