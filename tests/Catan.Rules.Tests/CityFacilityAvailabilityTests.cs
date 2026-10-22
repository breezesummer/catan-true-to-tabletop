using Catan.Core;
using Xunit;
using M4 = Catan.Core.M4;
using M5 = Catan.Core.M5;

namespace Catan.Rules.Tests;

public sealed class CityFacilityAvailabilityTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CachedFacilityBecomesUsableAfterPresentationEndsWithoutTradeOrNewView(int track)
    {
        var snapshot = new OperationSnapshot { Scope = "same-game:P1", Revision = "same-view", Seat = "P1", CanInteract = false };
        snapshot.Goods[5 + track] = 2;
        var facility = new OperationBuild { Id = "ImproveCity:" + track, Count = 1, Track = track, Level = 1 };
        facility.Cost[5 + track] = 2;
        facility.ConfigureAvailability(true, snapshot.Goods);
        snapshot.Builds.Add(facility);
        var owned = (int[])snapshot.Goods.Clone();

        Assert.True(facility.Available);
        Assert.False(facility.CanPrepare(snapshot));
        snapshot.CanInteract = true;
        Assert.Same(facility, OperationPanelOptions.Facility(snapshot, track));
        Assert.True(facility.CanPrepare(snapshot));
        Assert.Equal(owned, snapshot.Goods);
        Assert.Equal("same-view", snapshot.Revision);

        snapshot.CanInteract = false;
        Assert.False(facility.CanPrepare(snapshot));
        Assert.True(facility.Available);
    }

    [Theory]
    [InlineData(false, 2, "当前阶段或连接条件没有合法目标")]
    [InlineData(true, 1, "缺少所需资源")]
    public void EndingPresentationDoesNotMakeIllegalOrUnaffordableFacilitiesUsable(bool legal, int paper, string reason)
    {
        var snapshot = new OperationSnapshot { CanInteract = false };
        snapshot.Goods[7] = paper;
        var facility = new OperationBuild { Count = 1, Track = 2, Level = 1 };
        facility.Cost[7] = 2;
        facility.ConfigureAvailability(legal, snapshot.Goods);
        snapshot.CanInteract = true;
        Assert.False(facility.CanPrepare(snapshot));
        Assert.Equal(reason, facility.UnavailableReason);
        Assert.Equal(paper, snapshot.Goods[7]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CitiesFacilitiesCanUpgradeDirectlyAndRejectInsufficientPaymentWithoutMutation(int track)
    {
        var game = M4CoreHarness.Fixture(state =>
        {
            state.Cities = new[] { M4CoreHarness.Piece(state.Tiles.First(tile => tile.Resource != "sea").Vertices[0]) };
            M4CoreHarness.Player(state).Improvements[track] = 1;
            M4CoreHarness.Player(state).Commodities[(M4.Commodity)track] = 2;
        });
        var before = game.Save();
        var command = M4CoreHarness.Cmd(M4.CommandKind.ImproveCity);
        command.Track = (M4.ImprovementTrack)track;
        Assert.Contains(game.GetPlayerView("P1").LegalActions, action => action.Kind == command.Kind && action.Track == command.Track);
        Assert.True(game.Preview(command).Success);
        Assert.Equal(before, game.Save());
        M4CoreHarness.Accepted(game, command);
        Assert.Equal(2, game.GetPlayerView("P1").OwnImprovements[track]);
        Assert.Equal(0, game.GetPlayerView("P1").OwnCommodities[(M4.Commodity)track]);
        command = M4CoreHarness.Cmd(M4.CommandKind.ImproveCity);
        command.Track = (M4.ImprovementTrack)track;
        M4CoreHarness.Rejected(game, command);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CombinedFacilitiesCanUpgradeDirectlyAndRejectInsufficientPaymentWithoutMutation(int track)
    {
        var game = M5Harness.Fixture(state =>
        {
            state.Cities = new[] { M5Harness.Piece(state.Tiles.First(tile => tile.Resource != "sea").Vertices[0]) };
            M5Harness.Player(state).Improvements[track] = 1;
            M5Harness.Player(state).Commodities[(M5.Commodity)track] = 2;
        });
        var before = game.Save();
        var command = M5Harness.Cmd(M5.CommandKind.ImproveCity);
        command.Track = (M5.ImprovementTrack)track;
        Assert.Contains(game.GetPlayerView("P1").LegalActions, action => action.Kind == command.Kind && action.Track == command.Track);
        Assert.True(game.Preview(command).Success);
        Assert.Equal(before, game.Save());
        M5Harness.Accepted(game, command);
        Assert.Equal(2, game.GetPlayerView("P1").OwnImprovements[track]);
        Assert.Equal(0, game.GetPlayerView("P1").OwnCommodities[(M5.Commodity)track]);
        command = M5Harness.Cmd(M5.CommandKind.ImproveCity);
        command.Track = (M5.ImprovementTrack)track;
        M5Harness.Rejected(game, command);
    }
}
