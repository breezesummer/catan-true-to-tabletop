using Catan.Core;
using Xunit;
namespace Catan.Rules.Tests;
public class MultiResourceBankTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void M2MixedRequestIsAtomicAndConservesSupply(bool shortage)
    {
        var game=M2Harness.Fixture(state=>{M2Harness.Player(state).Resources.Wood=8;if(shortage)M2Harness.Player(state,"P2").Resources.Wool=19;});
        var command=M2Harness.Cmd(Catan.Core.M2.CommandKind.BankTrade);
        command.GiveResource=Resource.Wood;command.GiveAmount=8;command.Receive=new ResourceBag{Brick=1,Wool=1};
        var before=game.GetPlayerView("P1");
        var result=game.Execute(command);
        Assert.Equal(!shortage,result.Success);
        var after=game.GetPlayerView("P1");
        Assert.Equal(shortage?8:0,after.OwnResources.Wood);
        Assert.Equal(shortage?0:1,after.OwnResources.Brick);
        Assert.Equal(shortage?0:1,after.OwnResources.Wool);
        foreach(Resource resource in Enum.GetValues<Resource>())
            Assert.Equal(before.Bank[resource]+before.OwnResources[resource],after.Bank[resource]+after.OwnResources[resource]);
        if(!shortage){Assert.True(game.Execute(command).IsDuplicate);Assert.Single(game.GetHistory("P1"));}
        else Assert.Empty(game.GetHistory("P1"));
    }
    [Fact]
    public void M2NegativeMixedRequestCannotHideBehindZeroTotal()
    {
        var game=M2Harness.Fixture(state=>M2Harness.Player(state).Resources.Wood=8);
        var command=M2Harness.Cmd(Catan.Core.M2.CommandKind.BankTrade);
        command.GiveResource=Resource.Wood;command.ReceiveResource=Resource.Ore;command.Receive=new ResourceBag{Brick=1,Wool=-1};
        Assert.False(game.Execute(command).Success);Assert.Equal(8,game.GetPlayerView("P1").OwnResources.Wood);Assert.Empty(game.GetHistory("P1"));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void M3MixedRequestIsAtomicAndConservesSupply(bool shortage)
    {
        var game=M3Harness.Fixture(state=>{M3Harness.Player(state).Resources.Wood=8;if(shortage)M3Harness.Player(state,"P2").Resources.Wool=19;});
        var command=M3Harness.Cmd(Catan.Core.M3.CommandKind.BankTrade);
        command.GiveResource=Resource.Wood;command.GiveAmount=8;command.Receive=new ResourceBag{Brick=1,Wool=1};
        var before=game.GetPlayerView("P1");
        var result=game.Execute(command);
        Assert.Equal(!shortage,result.Success);
        var after=game.GetPlayerView("P1");
        Assert.Equal(shortage?8:0,after.OwnResources.Wood);
        Assert.Equal(shortage?0:1,after.OwnResources.Brick);
        Assert.Equal(shortage?0:1,after.OwnResources.Wool);
        foreach(Resource resource in Enum.GetValues<Resource>())
            Assert.Equal(before.Bank[resource]+before.OwnResources[resource],after.Bank[resource]+after.OwnResources[resource]);
        if(!shortage){Assert.True(game.Execute(command).IsDuplicate);Assert.Single(game.GetHistory("P1"));}
        else Assert.Empty(game.GetHistory("P1"));
    }
    [Fact]
    public void M3NegativeMixedRequestCannotHideBehindZeroTotal()
    {
        var game=M3Harness.Fixture(state=>M3Harness.Player(state).Resources.Wood=8);
        var command=M3Harness.Cmd(Catan.Core.M3.CommandKind.BankTrade);
        command.GiveResource=Resource.Wood;command.ReceiveResource=Resource.Ore;command.Receive=new ResourceBag{Brick=1,Wool=-1};
        Assert.False(game.Execute(command).Success);Assert.Equal(8,game.GetPlayerView("P1").OwnResources.Wood);Assert.Empty(game.GetHistory("P1"));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void M4MixedRequestIsAtomicAndConservesSupply(bool shortage)
    {
        var game=M4CoreHarness.Fixture(state=>{M4CoreHarness.Player(state).Resources.Wood=8;if(shortage)M4CoreHarness.Player(state,"P2").Resources.Wool=19;});
        var command=M4CoreHarness.Cmd(Catan.Core.M4.CommandKind.BankTrade);
        command.GiveResource=Resource.Wood;command.GiveAmount=8;command.Receive=new ResourceBag{Brick=1,Wool=1};
        var before=game.GetPlayerView("P1");
        var result=game.Execute(command);
        Assert.Equal(!shortage,result.Success);
        var after=game.GetPlayerView("P1");
        Assert.Equal(shortage?8:0,after.OwnResources.Wood);
        Assert.Equal(shortage?0:1,after.OwnResources.Brick);
        Assert.Equal(shortage?0:1,after.OwnResources.Wool);
        foreach(Resource resource in Enum.GetValues<Resource>())
            Assert.Equal(before.Bank[resource]+before.OwnResources[resource],after.Bank[resource]+after.OwnResources[resource]);
        if(!shortage){Assert.True(game.Execute(command).IsDuplicate);Assert.Single(game.GetHistory("P1"));}
        else Assert.Empty(game.GetHistory("P1"));
    }
    [Fact]
    public void M4NegativeMixedRequestCannotHideBehindZeroTotal()
    {
        var game=M4CoreHarness.Fixture(state=>M4CoreHarness.Player(state).Resources.Wood=8);
        var command=M4CoreHarness.Cmd(Catan.Core.M4.CommandKind.BankTrade);
        command.GiveResource=Resource.Wood;command.ReceiveResource=Resource.Ore;command.Receive=new ResourceBag{Brick=1,Wool=-1};
        Assert.False(game.Execute(command).Success);Assert.Equal(8,game.GetPlayerView("P1").OwnResources.Wood);Assert.Empty(game.GetHistory("P1"));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void M5MixedRequestIsAtomicAndConservesSupply(bool shortage)
    {
        var game=M5Harness.Fixture(state=>{M5Harness.Player(state).Resources.Wood=8;if(shortage)M5Harness.Player(state,"P2").Resources.Wool=19;});
        var command=M5Harness.Cmd(Catan.Core.M5.CommandKind.BankTrade);
        command.GiveResource=Resource.Wood;command.GiveAmount=8;command.Receive=new ResourceBag{Brick=1,Wool=1};
        var before=game.GetPlayerView("P1");
        var result=game.Execute(command);
        Assert.Equal(!shortage,result.Success);
        var after=game.GetPlayerView("P1");
        Assert.Equal(shortage?8:0,after.OwnResources.Wood);
        Assert.Equal(shortage?0:1,after.OwnResources.Brick);
        Assert.Equal(shortage?0:1,after.OwnResources.Wool);
        foreach(Resource resource in Enum.GetValues<Resource>())
            Assert.Equal(before.Bank[resource]+before.OwnResources[resource],after.Bank[resource]+after.OwnResources[resource]);
        if(!shortage){Assert.True(game.Execute(command).IsDuplicate);Assert.Single(game.GetHistory("P1"));}
        else Assert.Empty(game.GetHistory("P1"));
    }
    [Fact]
    public void M5NegativeMixedRequestCannotHideBehindZeroTotal()
    {
        var game=M5Harness.Fixture(state=>M5Harness.Player(state).Resources.Wood=8);
        var command=M5Harness.Cmd(Catan.Core.M5.CommandKind.BankTrade);
        command.GiveResource=Resource.Wood;command.ReceiveResource=Resource.Ore;command.Receive=new ResourceBag{Brick=1,Wool=-1};
        Assert.False(game.Execute(command).Success);Assert.Equal(8,game.GetPlayerView("P1").OwnResources.Wood);Assert.Empty(game.GetHistory("P1"));
    }
    [Fact]
    public void InterfaceAcceptsMixedRequestedResourcesAndChecksEachBankPile()
    {
        var snapshot=new OperationSnapshot{CanTrade=true,CanInteract=true,GoodCount=5};
        snapshot.Goods[0]=8;snapshot.Rates[0]=4;snapshot.Bank[1]=1;snapshot.Bank[2]=1;
        var trade=new OperationTrade{Bank=true};trade.Give[0]=8;trade.Receive[1]=1;trade.Receive[2]=1;
        Assert.Null(OperationSelection.ValidateTrade(snapshot,trade));snapshot.Bank[2]=0;
        Assert.NotNull(OperationSelection.ValidateTrade(snapshot,trade));
    }
}
