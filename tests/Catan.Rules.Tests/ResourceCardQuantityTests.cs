using Catan.Core;
using Xunit;

namespace Catan.Rules.Tests;

public sealed class ResourceCardQuantityTests
{
    [Fact]
    public void LeftClicksAddAndRightClicksSubtractWithoutGoingBelowZero()
    {
        int[] selected=new int[8];
        Assert.Null(OperationCardQuantity.Change(selected,2,1,19));
        Assert.Null(OperationCardQuantity.Change(selected,2,1,19));
        Assert.Null(OperationCardQuantity.Change(selected,2,-1,19));Assert.Equal(1,selected[2]);
        OperationCardQuantity.Change(selected,2,-1,19);OperationCardQuantity.Change(selected,2,-1,19);
        Assert.Equal(0,selected[2]);
    }
    [Fact]
    public void TotalLimitCanBeFreedByRemovingOneCard()
    {
        int[] selected=new int[8];selected[2]=2;
        Assert.NotNull(OperationCardQuantity.Change(selected,0,1,19,2));Assert.Equal(0,selected[0]);
        Assert.Null(OperationCardQuantity.Change(selected,2,-1,19,2));
        Assert.Null(OperationCardQuantity.Change(selected,0,1,19,2));Assert.Equal(2,selected.Sum());
    }
    [Fact]
    public void BankCardSwitchKeepsOneKindAndRightClickOnUnselectedKindDoesNotClearIt()
    {
        int[] selected=new int[8];selected[0]=4;
        OperationCardQuantity.Change(selected,4,-1,19,singleKind:true);Assert.Equal(4,selected[0]);
        OperationCardQuantity.Change(selected,2,1,19,singleKind:true);
        Assert.Equal(0,selected[0]);Assert.Equal(1,selected[2]);Assert.Equal(1,selected.Sum());
    }
    [Fact]
    public void UnavailableNewBankKindDoesNotDestroyExistingSelection()
    {
        int[] selected=new int[8];selected[0]=1;
        Assert.NotNull(OperationCardQuantity.Change(selected,4,1,0,singleKind:true));Assert.Equal(1,selected[0]);
    }
    [Fact]
    public void BankingStillRequiresCorrectRatioAfterRightClick()
    {
        var snapshot=new OperationSnapshot{CanTrade=true,CanInteract=true,GoodCount=5};snapshot.Goods[0]=4;snapshot.Bank[2]=19;snapshot.Rates[0]=4;
        var trade=new OperationTrade{Bank=true};
        for(int i=0;i<4;i++)OperationCardQuantity.Change(trade.Give,0,1,19,singleKind:true);
        OperationCardQuantity.Change(trade.Receive,2,1,19,singleKind:true);
        Assert.Null(OperationSelection.ValidateTrade(snapshot,trade));
        OperationCardQuantity.Change(trade.Give,0,-1,19,singleKind:true);
        Assert.NotNull(OperationSelection.ValidateTrade(snapshot,trade));Assert.Equal(3,trade.Give[0]);
    }
    [Fact]
    public void GoldRightClickReleasesQuotaAndNeverModifiesBank()
    {
        var bank=new ResourceBag{Wool=2,Wood=2};var selected=new ResourceBag{Wool=2};
        Assert.Null(OperationResourceChoice.Change(2,-1,2,selected,bank));
        Assert.Null(OperationResourceChoice.Change(0,1,2,selected,bank));
        Assert.Equal((1,1,2),(selected.Wool,selected.Wood,selected.Total));Assert.Equal((2,2),(bank.Wool,bank.Wood));
        OperationResourceChoice.Change(4,-1,2,selected,bank);Assert.Equal(0,selected.Ore);
        Assert.NotNull(OperationResourceChoice.Change(5,-1,2,selected,bank));
    }
    [Theory]
    [InlineData(-1,1)] [InlineData(8,1)] [InlineData(0,0)] [InlineData(0,2)]
    public void InvalidClicksCannotMutateSelection(int good,int delta)
    {
        int[] selected=new int[8];selected[2]=1;
        Assert.NotNull(OperationCardQuantity.Change(selected,good,delta,19));Assert.Equal(1,selected.Sum());
    }
}
