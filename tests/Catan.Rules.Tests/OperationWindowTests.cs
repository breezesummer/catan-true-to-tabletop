using Xunit;

namespace Catan.Rules.Tests;

public sealed class OperationWindowTests
{
    [Fact]
    public void ClosingAMinimizedFacilityRemovesTheTurnButtonResumeCaption()
    {
        var window=new OperationWindowState();window.Sync("P1","before-roll");
        window.Hide("城市设施");window.Open();window.Close();window.Describe("城市设施");
        window.Sync("P1","before-roll");
        Assert.False(window.Hidden);Assert.True(window.Closed);Assert.True(window.Suppressed);Assert.Null(window.Caption);
        window.Open();Assert.False(window.Suppressed);
    }

    [Fact]
    public void MinimizedHistoryRestoresOnlyWhenThePlayerRequestsIt()
    {
        var window=new OperationWindowState();window.Sync("P1","9");int restored=0;
        window.Hide("历史记录",()=>restored++);window.Sync("P1","9");
        Assert.Equal(0,restored);Assert.True(window.Hidden);Assert.Equal("历史记录",window.Caption);
        window.Resume();window.Resume();Assert.Equal(1,restored);Assert.False(window.Suppressed);
    }

    [Fact]
    public void ChangingSeatsDiscardsAnOldMinimizedWindowRestoreAction()
    {
        var window=new OperationWindowState();window.Sync("P1","9");bool restored=false;
        window.Hide("城市设施",()=>restored=true);window.Sync("P2","10");window.Resume();
        Assert.False(restored);Assert.Null(window.Caption);Assert.False(window.Suppressed);
    }

    [Fact]
    public void OpeningAnotherWindowDoesNotAlsoRestoreAMinimizedHistoryOrMenu()
    {
        var window=new OperationWindowState();bool restored=false;
        window.Hide("游戏菜单",()=>restored=true);window.Open();window.Resume();
        Assert.False(restored);Assert.False(window.Suppressed);
    }

    [Fact]
    public void AiActionsDoNotLoseTheMinimizedHistoryResumeEntry()
    {
        var window=new OperationWindowState();window.Sync("P1","9");bool restored=false;
        window.Hide("历史记录",()=>restored=true);window.Sync("P1","10");window.ResetInteraction();
        Assert.True(window.Hidden);Assert.Equal("历史记录",window.Caption);
        window.Resume();Assert.True(restored);
    }
}
