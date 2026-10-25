using System;
using System.Linq;
using System.Runtime.Serialization;
using Catan.Core.ThreeKingdoms;

namespace Catan.Core.M4
{
    public sealed partial class Command
    {
        [DataMember(Order=308,EmitDefaultValue=false)] public int[] DeclaredRealTypes { get; set; }
        [DataMember(Order=309,EmitDefaultValue=false)] public int[] DeclaredEffectiveTypes { get; set; }
    }
    public sealed partial class TradeOffer
    {
        // 仅权威存档保留底牌；公开报价只包含声明用途。
        [DataMember(Order=300,EmitDefaultValue=false)] public int[] B4RealGive { get; set; }
        [DataMember(Order=301,EmitDefaultValue=false)] public bool B4ConversionPayment { get; set; }
        [DataMember(Order=302,EmitDefaultValue=false)] public string B4DeclarationHistoryEventId { get; set; }
    }
    public sealed partial class GameState
    {
        [IgnoreDataMember] internal string B4DeclarationHistoryEventId { get; set; }
        [IgnoreDataMember] internal int[] B4DeclaredPayment { get; set; }
        [IgnoreDataMember] internal bool B4DeclaredPaymentConsumed { get; set; }
    }
    public sealed partial class CitiesKnightsGameSession
    {
        private bool BeginB4Declaration(GameState s,Command c)
        {
            if(c.DeclaredRealTypes==null&&c.DeclaredEffectiveTypes==null)return false;
            Require(c.WindowId==s.ThreeKingdoms.WindowId,"StaleWindow","声明窗口已失效。");
            Require(c.Kind!=CommandKind.UseGeneralSkill&&c.Kind!=CommandKind.ChooseGenerals&&c.Kind!=CommandKind.DeployGeneral&&c.Kind!=CommandKind.RetireGeneral,"InvalidDeclaration","请把声明绑定到具体支付或交易用途。");
            Require(s.ThreeKingdoms.Automation?.PendingDecision==null,"SkillDecisionPending","请先回答当前技能询问。");
            var matrix=DeclarationMatrix(c.DeclaredRealTypes,c.DeclaredEffectiveTypes);
            var probe=s.CopyForTransaction();probe.B4DeclaredPayment=matrix;probe.B4DeclaredPaymentConsumed=false;
            var command=Json.Copy(c);command.DeclaredRealTypes=null;command.DeclaredEffectiveTypes=null;
            // 整项用途先在隔离副本预验：目标或费用非法时既不询问，也不消耗随机。
            ApplyB2Native(probe,command);
            Require(probe.B4DeclaredPaymentConsumed||ValidateDeclarationTrade(s,c,matrix),"UnusedDeclaration","此动作没有使用声明的费用。");
            B2SkillEngine.BeginB4DeclarationBatch(CardHost(s),c.PlayerId,"guhuo-"+c.Id,c.DeclaredRealTypes,c.DeclaredEffectiveTypes,Json.Write(command));
            RefreshB2(s);return true;
        }
        private static int[] DeclarationMatrix(int[] real,int[] effective)
        {
            Require(real!=null&&effective!=null&&real.Length>0&&real.Length<=152&&real.Length==effective.Length&&real.All(t=>t>=0&&t<8)&&effective.All(t=>t>=0&&t<8),"InvalidDeclaration","声明的真实牌和用途数量不一致。");
            var matrix=new int[64];for(int i=0;i<real.Length;i++)matrix[real[i]*8+effective[i]]++;return matrix;
        }
        private static bool ValidateDeclarationTrade(GameState s,Command c,int[] matrix)
        {
            if(c.Kind!=CommandKind.ProposeTrade)return false;
            var actual=Enumerable.Range(0,8).Select(i=>i<5?(c.Give??new ResourceBag())[(Resource)i]:(c.GiveCommodities??new CommodityBag())[(Commodity)(i-5)]).ToArray();
            return Enumerable.Range(0,8).All(real=>Enumerable.Range(0,8).Sum(use=>matrix[real*8+use])==actual[real]);
        }
        private void ResumeB4Declaration(GameState s)
        {
            if(!B4(s)||B2Busy(s)||s.ThreeKingdoms.PendingDecision!=null)return;
            var result=B2SkillEngine.TakeB4DeclarationResult(CardHost(s));if(result==null||!result.Success)return;
            var c=Json.Read<Command>(result.OperationJson);c.WindowId=s.ThreeKingdoms.WindowId;
            s.B4DeclaredPayment=DeclarationMatrix(result.RealTypes,result.EffectiveTypes);s.B4DeclaredPaymentConsumed=false;
            // 已发生的质疑奖惩不回滚；响应改变目标合法性时仅取消原用途。
            var validation=s.CopyForTransaction();validation.B4DeclaredPayment=(int[])s.B4DeclaredPayment.Clone();
            try { ApplyB2Native(validation,c); }
            catch(RuleException) { s.B4DeclaredPayment=null;return; }
            s.B4DeclarationHistoryEventId=s.ThreeKingdoms.SkillHistory?.SingleOrDefault(e=>e.SourceEventId==result.EventId)?.EventId;
            try { ApplyB2Native(s,c); }
            finally { s.B4DeclaredPayment=null;s.B4DeclaredPaymentConsumed=false;s.B4DeclarationHistoryEventId=null; }
        }
        private void PayB4Declared(GameState s,PlayerState p,int[] needs)
        {
            var matrix=s.B4DeclaredPayment;
            Require(matrix?.Length==64&&!s.B4DeclaredPaymentConsumed,"InvalidDeclaration","声明费用不能重复支付。");
            Require(Enumerable.Range(0,8).All(use=>Enumerable.Range(0,8).Sum(real=>matrix[real*8+use])==needs[use]),"InvalidDeclaration","声明用途与实际费用不一致。");
            var realCards=Enumerable.Range(0,8).Select(real=>Enumerable.Range(0,8).Sum(use=>matrix[real*8+use])).ToArray();
            var host=CardHost(s);var before=host.GetHand(p.Id);Require(Enumerable.Range(0,8).All(t=>before[t]>=realCards[t]),"InsufficientResources","预留的真实牌已不足。");
            s.B4DeclaredPaymentConsumed=true;host.RemoveHand(p.Id,realCards);host.AddBank(realCards);
            var id="guhuo-payment-"+s.ThreeKingdoms.NextId++;
            s.ThreeKingdomsNativePayments=(s.ThreeKingdomsNativePayments??Array.Empty<B3NativePayment>()).Concat(new[]{new B3NativePayment {
                EventId=id,PlayerId=p.Id,Cards=realCards,BeforeCount=before.Sum(),AfterCount=before.Sum()-realCards.Sum()}}).ToArray();
            B2SkillEngine.HoldReturnedCards(host,p.Id,"payment",id,realCards);
            var changes=new[]{new B2HandChange {PlayerId=p.Id,BeforeCount=before.Sum(),AfterCount=before.Sum()-realCards.Sum(),LostCount=realCards.Sum(),LostCards=realCards,SourceSkillId="tk-g07-s1"}};
            B2SkillEngine.OnCardsMoved(host,id,changes);
            B2SkillEngine.OnCivilPaid(host,id,p.Id,Enumerable.Range(0,8).Where(t=>needs[t]>0).ToArray());
            if(s.B4DeclarationHistoryEventId!=null)B4SkillHistoryEngine.LinkCardFlow(s.ThreeKingdoms,s.B4DeclarationHistoryEventId,id,changes);
        }
    }
}
