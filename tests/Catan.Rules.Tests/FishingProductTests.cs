using Catan.Core.TradeModules;
using Xunit;
using Xunit.Abstractions;
using M2=Catan.Core.M2;
using M4=Catan.Core.M4;

namespace Catan.Rules.Tests;

public sealed class FishingProductTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(3,false,false)][InlineData(4,false,false)][InlineData(3,true,false)][InlineData(4,true,false)]
    [InlineData(3,false,true)][InlineData(4,false,true)][InlineData(3,true,true)][InlineData(4,true,true)]
    public void CitiesProductAiCompletesAndReplays(int seats,bool harbor,bool kingdoms)
    {
        var session=M4.CitiesKnightsGameSession.Create(AcceptanceFixture.Json,seats,97,threeKingdoms:kingdoms,tradeModules:new(){Fishing=true,Harbormaster=harbor});
        var game=new ThreeKingdomsTestGame("cities",session);int count=0,spends=0,replacements=0;
        while(game.View().Phase.ToString()!="Finished" && count<15000)
        {
            dynamic command=game.Decide();
            if(command.Kind.ToString()=="SpendFish")spends++;
            if(command.Kind.ToString()=="ResolveFishReplacement")
            {
                replacements++;var checkpoint=game.Save();var restored=ThreeKingdomsTestGame.Load("cities",checkpoint);
                Assert.Equal(checkpoint,restored.Save());game.Accept(command);restored.Accept(command);Assert.Equal(game.Save(),restored.Save());
                Assert.True((bool)game.Execute(command).IsDuplicate);count++;continue;
            }
            game.Accept(command);count++;game.AssertInvariants();
            if(count==seats*8){var saved=game.Save();game=ThreeKingdomsTestGame.Load("cities",saved);Assert.Equal(saved,game.Save());}
        }
        Assert.Equal("Finished",game.View().Phase.ToString());Assert.True(spends>0);
        var final=game.Save();Assert.Equal(final,ThreeKingdomsTestGame.Load("cities",final).Save());
        output.WriteLine($"城市骑士/{seats}人/海港{harbor}/三国{kingdoms}：{count}指令，{game.View().Turn}回合，支付{spends}，换抽{replacements}。");
    }
    [Theory][InlineData(3,false)][InlineData(4,false)][InlineData(3,true)][InlineData(4,true)]
    public void BaseProductAiCompletesAndReplays(int seats,bool harbor)
    {
        var session=M2.BaseGameSession.Create(AcceptanceFixture.Json,seats,97,tradeModules:new(){Fishing=true,Harbormaster=harbor});
        var game=new M6Game("base",session);int count=0,spends=0;
        while(game.View().Phase.ToString()!="Finished"&&count<10000)
        {
            dynamic command=game.Decide(game.AiView(M6Game.Actor(game.View())));command.Id="捕鱼产品-"+count++;
            if(command.Kind.ToString()=="SpendFish")spends++;
            game.Accept(command);session.AssertInvariants();
        }
        Assert.Equal("Finished",game.View().Phase.ToString());Assert.True(spends>0);
        Assert.Equal(game.Save(),M6Game.Load("base",game.Save()).Save());
        output.WriteLine($"基础/{seats}人/海港{harbor}：{count}指令，{game.View().Turn}回合，支付{spends}。");
    }
}
