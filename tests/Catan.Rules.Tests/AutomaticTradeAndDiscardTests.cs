using Catan.Core;
using Xunit;
using M2=Catan.Core.M2;
using M3=Catan.Core.M3;
using M4=Catan.Core.M4;
using M5=Catan.Core.M5;

namespace Catan.Rules.Tests;

public class AutomaticTradeAndDiscardTests
{
    [Theory]
    [InlineData("base")][InlineData("seafarers")][InlineData("cities")][InlineData("combined")]
    public void LegacyTradeHistoryLoadsButNewOffersUseCurrentPolicy(string rules)
    {
        var game=M6Game.Create(rules);
        for(int step=0;step<1000;step++)
        {
            dynamic view=game.View();string actor=view.ActivePlayerId;
            if(view.Phase.ToString()=="Action"&&view.TradeOffer==null&&((ResourceBag)game.View(actor).OwnResources).Total>0)break;
            var next=game.Decide(game.View(M6Game.Actor(view)));next.Id=Guid.NewGuid().ToString("N");game.Accept(next);
        }
        dynamic current=game.View();string player=current.ActivePlayerId;
        Resource give=Enum.GetValues<Resource>().First(r=>((ResourceBag)game.View(player).OwnResources)[r]>0);
        Resource receive=Enum.GetValues<Resource>().First(r=>r!=give);
        string recipient=((Array)current.Players).Cast<dynamic>().First(p=>(string)p.Id!=player).Id;
        // 测试专用反射模拟旧版本执行记录，不给产品命令添加兼容开关。
        var sessionField=typeof(M6Game).GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Single(f=>f.FieldType==typeof(object));
        object session=sessionField.GetValue(game)!;
        var legacy=session.GetType().GetField("legacyInteractionReplay",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
        legacy.SetValue(session,true);
        dynamic offer=rules switch{
            "base"=>M2Harness.Cmd(M2.CommandKind.ProposeTrade,player),"seafarers"=>M3Harness.Cmd(M3.CommandKind.ProposeTrade,player),
            "cities"=>M4CoreHarness.Cmd(M4.CommandKind.ProposeTrade,player),_=>M5Harness.Cmd(M5.CommandKind.ProposeTrade,player)};
        offer.OtherPlayerId=recipient;offer.Give=new ResourceBag();offer.Give[give]=1;offer.Receive=new ResourceBag();offer.Receive[receive]=19;
        game.Accept(offer);legacy.SetValue(session,false);
        Assert.NotNull((object?)game.View().TradeOffer);
        string saved=game.Save();var restored=M6Game.Load(rules,saved);Assert.Equal(saved,restored.Save());
        dynamic cancel=rules switch{
            "base"=>M2Harness.Cmd(M2.CommandKind.CancelTrade,player),"seafarers"=>M3Harness.Cmd(M3.CommandKind.CancelTrade,player),
            "cities"=>M4CoreHarness.Cmd(M4.CommandKind.CancelTrade,player),_=>M5Harness.Cmd(M5.CommandKind.CancelTrade,player)};
        restored.Accept(cancel);offer.Id=Guid.NewGuid().ToString("N");restored.Accept(offer);
        Assert.Null((object?)restored.View().TradeOffer);
        Assert.Equal(restored.Save(),M6Game.Load(rules,restored.Save()).Save());restored.AssertInvariants();
    }
    [Theory]
    [InlineData("base")][InlineData("seafarers")][InlineData("cities")][InlineData("combined")]
    public void InsufficientRecipientsAutomaticallyRejectWithoutTransfers(string rules)
    {
        void Hands(dynamic s){s.Players[0].Resources.Wood=2;s.Players[2].Resources.Ore=1;}
        object session=rules switch{
            "base"=>M2Harness.Fixture(s=>Hands(s)),"seafarers"=>M3Harness.Fixture(s=>Hands(s)),
            "cities"=>M4CoreHarness.Fixture(s=>Hands(s)),_=>M5Harness.Fixture(s=>Hands(s))};
        var game=new M6Game(rules,session);
        dynamic Cmd(string kind,string player="P1")=>rules switch{
            "base"=>M2Harness.Cmd(Enum.Parse<M2.CommandKind>(kind),player),"seafarers"=>M3Harness.Cmd(Enum.Parse<M3.CommandKind>(kind),player),
            "cities"=>M4CoreHarness.Cmd(Enum.Parse<M4.CommandKind>(kind),player),_=>M5Harness.Cmd(Enum.Parse<M5.CommandKind>(kind),player)};
        var offer=Cmd("ProposeTrade");offer.OtherPlayerId="*";offer.Give=new ResourceBag{Wood=2};offer.Receive=new ResourceBag{Ore=1};
        string before=game.Save();Assert.True((bool)((dynamic)session).Preview(offer).Success);Assert.Equal(before,game.Save());
        game.Accept(offer);
        Assert.Equal(new[]{"P3"},(string[])game.View().TradeOffer.PendingRecipientIds);
        Assert.Equal(new[]{"P2","P4"},(string[])game.View().TradeOffer.RejectedPlayerIds);
        Assert.Equal(2,(int)game.View().OwnResources.Wood);
        before=game.Save();Assert.False((bool)game.Execute(Cmd("AcceptTrade","P2")).Success);Assert.Equal(before,game.Save());
        game.Accept(Cmd("RejectTrade","P3"));Assert.Null((object?)game.View().TradeOffer);
        offer.Id=Guid.NewGuid().ToString("N");offer.OtherPlayerId="P2";game.Accept(offer);Assert.Null((object?)game.View().TradeOffer);
        offer.Id=Guid.NewGuid().ToString("N");offer.OtherPlayerId="*";offer.Receive.Ore=2;game.Accept(offer);Assert.Null((object?)game.View().TradeOffer);
        Assert.Equal(2,(int)game.View().OwnResources.Wood);Assert.Equal(1,(int)game.View("P3").OwnResources.Ore);game.AssertInvariants();
    }

    [Theory][InlineData(1)][InlineData(4)]
    public void CitiesCannotDiscardAtOrBelowLimit(int count)
    {
        var game=M4ProgressHarness.Fixture(M4.ProgressCardKind.Merchant,s=>s.Players[0].ProgressCards=Enumerable.Repeat(M4.ProgressCardKind.Merchant,count).ToArray());
        var command=M4CoreHarness.Cmd(M4.CommandKind.DiscardProgressCard);command.ProgressCard=M4.ProgressCardKind.Merchant;
        M4CoreHarness.Rejected(game,command);
        Assert.DoesNotContain(game.GetPlayerView("P1").LegalActions,c=>c.Kind==M4.CommandKind.DiscardProgressCard);
    }
    [Theory][InlineData(1)][InlineData(4)][InlineData(5)]
    public void CombinedOnlyDiscardsExcessCards(int count)
    {
        var game=M5Harness.Fixture(s=>s.Players[0].ProgressCards=Enumerable.Repeat(M5.ProgressCardKind.Merchant,count).ToArray());
        var command=M5Harness.Cmd(M5.CommandKind.DiscardProgressCard);command.ProgressCard=M5.ProgressCardKind.Merchant;
        if(count<=4)M5Harness.Rejected(game,command);
        else {M5Harness.Accepted(game,command);command.Id=Guid.NewGuid().ToString("N");M5Harness.Rejected(game,command);Assert.Equal(4,game.GetPlayerView("P1").OwnProgressCards.Length);}
        game.AssertInvariants();
    }
}
