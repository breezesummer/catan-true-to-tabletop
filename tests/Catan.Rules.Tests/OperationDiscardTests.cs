using Xunit;

namespace Catan.Rules.Tests;

public sealed class OperationDiscardTests
{
    [Theory]
    [InlineData(3, false)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    public void RightHandConfirmationRequiresExactQuantity(int selected, bool ready)
    {
        var choice = new OperationDiscard { Amount = 4 };
        var actual = choice.Ready(new[] { selected, 0 }, new[] { 6, 1 }, null!);
        Assert.Equal(ready, actual);
        Assert.Equal(ready ? "确认" : "选择弃牌", choice.Caption(actual));
    }

    [Fact]
    public void ResourceAndCommoditySelectionShareTheRequiredCount()
    {
        var selection = new OperationSelection();
        selection.Toggle(0, 1, 2);
        selection.Toggle(5, 0, 1);
        var choice = new OperationDiscard { Amount = 2 };
        Assert.True(choice.Ready(selection.Selected, new[] { 2, 0, 0, 0, 0, 1, 0, 0 }, null!));
        selection.Toggle(0, 1, 2);
        Assert.False(choice.Ready(selection.Selected, new[] { 2, 0, 0, 0, 0, 1, 0, 0 }, null!));
    }

    [Fact]
    public void OutOfHandAndNegativeQuantitiesCannotBeConfirmed()
    {
        var choice = new OperationDiscard { Amount = 2 };
        Assert.False(choice.Ready(new[] { 2, 0 }, new[] { 1, 1 }, null!));
        Assert.False(choice.Ready(new[] { 3, -1 }, new[] { 3, 1 }, null!));
        Assert.False(choice.Ready(new[] { 2 }, new[] { 2, 0 }, null!));
    }

    [Fact]
    public void ProgressOverflowRequiresOneSelectedCard()
    {
        var choice = new OperationDiscard { Amount = 1, Special = true };
        Assert.False(choice.Ready(Array.Empty<int>(), Array.Empty<int>(), null!));
        Assert.True(choice.Ready(Array.Empty<int>(), Array.Empty<int>(), new OperationHandCard { Id = "3:0" }));
    }

    [Fact]
    public void AConfirmedRevisionOrNewPendingChoiceClearsPreviousCardSelection()
    {
        var selection = new OperationSelection();
        var snapshot = new OperationSnapshot { Scope = "test", Seat = "P1", Revision = "1", Goods = new[] { 3, 0, 0, 0, 0, 0, 0, 0 } };
        selection.Sync(snapshot);
        selection.Toggle(0, 2, 3);
        snapshot.Revision = "2";
        selection.Sync(snapshot);
        Assert.All(selection.Selected, amount => Assert.Equal(0, amount));
        Assert.False(selection.IsCardSelected(0, 2));
    }
}
