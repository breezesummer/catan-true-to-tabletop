using Xunit;

namespace Catan.Rules.Tests;

// These cases check player-visible outcomes without importing Unity or reproducing a rules engine.
public sealed class OperationDockTests
{
    const int Wood = 0, Brick = 1, Wool = 2, Wheat = 3, Ore = 4, Cloth = 5, Coin = 6, Paper = 7;

    [Fact]
    public void ChoosingRoadSelectsItsTwoCardsAndLeavesOwnedCardsUntouched()
    {
        var owned = Bag((Wood, 3), (Brick, 2), (Wool, 1));
        var selection = new OperationSelection();
        selection.Choose(Build("road", (Wood, 1), (Brick, 1)), owned);

        Assert.Equal("road", selection.BuildId);
        Assert.Equal(Bag((Wood, 1), (Brick, 1)), selection.Selected);
        Assert.Equal(Bag((Wood, 3), (Brick, 2), (Wool, 1)), owned);
    }

    [Fact]
    public void UnaffordableCitySelectsOnlyCardsActuallyOwned()
    {
        var city = Build("city", (Wheat, 2), (Ore, 3));
        var selection = new OperationSelection();
        selection.Choose(city, Bag((Wheat, 1), (Ore, 2), (Wood, 5)));

        Assert.Equal("city", selection.BuildId);
        Assert.Equal(Bag((Wheat, 1), (Ore, 2)), selection.Selected);
        Assert.False(selection.Matches(city));
    }

    [Fact]
    public void PickingAnotherBuildReplacesPreviousPaymentSelection()
    {
        var selection = new OperationSelection();
        var owned = Bag((Wood, 3), (Brick, 3), (Wheat, 3), (Ore, 3));
        selection.Choose(Build("road", (Wood, 1), (Brick, 1)), owned);
        selection.Choose(Build("city", (Wheat, 2), (Ore, 3)), owned);

        Assert.Equal("city", selection.BuildId);
        Assert.Equal(Bag((Wheat, 2), (Ore, 3)), selection.Selected);
    }

    [Fact]
    public void HandSelectionMatchesCoveredCostsIncludingSupersetsButNotMissingResources()
    {
        var selection = new OperationSelection();
        selection.Toggle(Wood, 0, 2);
        selection.Toggle(Wood, 1, 2);
        selection.Toggle(Brick, 0, 1);
        selection.Toggle(Wool, 0, 1);

        Assert.True(selection.Matches(Build("road", (Wood, 1), (Brick, 1))));
        Assert.True(selection.Matches(Build("ship", (Wood, 1), (Wool, 1))));
        Assert.False(selection.Matches(Build("settlement", (Wood, 1), (Brick, 1), (Wool, 1), (Wheat, 1))));
        Assert.False(selection.Matches(Build("city", (Wheat, 2), (Ore, 3))));
    }

    [Fact]
    public void EmptySelectionAndFreePlacementDoNotHighlightEveryBuild()
    {
        var selection = new OperationSelection();
        Assert.False(selection.Matches(Build("road", (Wood, 1), (Brick, 1))));
        Assert.False(selection.Matches(Build("setup-village")));
        selection.Toggle(Wood, 0, 1);
        Assert.False(selection.Matches(Build("setup-village")));
    }

    [Fact]
    public void ManuallyPickingAResourceLeavesBuildModeAndCanToggleItBackDown()
    {
        var selection = new OperationSelection();
        selection.Choose(Build("road", (Wood, 1), (Brick, 1)), Bag((Wood, 2), (Brick, 2)));
        selection.Toggle(Wool, 0, 1);

        Assert.Null(selection.BuildId);
        Assert.Equal(1, selection.Selected[Wool]);
        selection.Toggle(Wool, 0, 1);
        Assert.Equal(0, selection.Selected[Wool]);
        Assert.Equal(1, selection.Selected[Wood]);
    }

    [Fact]
    public void PickingPastAvailableCardsNeverSelectsMoreThanOwned()
    {
        var selection = new OperationSelection();
        selection.Toggle(Wood, 0, 2);
        selection.Toggle(Wood, 1, 2);
        selection.Toggle(Wood, 7, 2);
        selection.Toggle(Ore, 0, 0);

        Assert.Equal(Bag((Wood, 2)), selection.Selected);
    }

    [Fact]
    public void ClickingFifthCardSelectsThatExactCardAndClickingAgainPutsItBack()
    {
        var selection = new OperationSelection();
        selection.Toggle(Wood, 4, 5);
        Assert.Equal(Bag((Wood, 1)), selection.Selected);
        Assert.True(selection.IsCardSelected(Wood, 4));
        Assert.False(selection.IsCardSelected(Wood, 0));
        Assert.False(selection.IsCardSelected(Wood, 1));

        selection.Toggle(Wood, 4, 5);
        Assert.Equal(Bag(), selection.Selected);
        Assert.False(selection.IsCardSelected(Wood, 4));
    }

    [Fact]
    public void NonAdjacentCardsOfTheSameGoodRemainIndependentlySelected()
    {
        var selection = new OperationSelection();
        selection.Toggle(Wood, 4, 5);
        selection.Toggle(Wood, 1, 5);
        selection.Toggle(Wood, 0, 5);
        selection.Toggle(Wood, 1, 5);

        Assert.Equal(Bag((Wood, 2)), selection.Selected);
        Assert.True(selection.IsCardSelected(Wood, 0));
        Assert.True(selection.IsCardSelected(Wood, 4));
        Assert.False(selection.IsCardSelected(Wood, 1));
        Assert.False(selection.IsCardSelected(Wood, 2));
    }

    [Fact]
    public void DroppingTheSameSelectedCardIntoTradeCannotDuplicateItsPayment()
    {
        var selection = new OperationSelection();
        selection.SelectCard(Wood, 4, 5);
        selection.SelectCard(Wood, 4, 5);

        Assert.Equal(Bag((Wood, 1)), selection.Selected);
        Assert.True(selection.IsCardSelected(Wood, 4));
        Assert.False(selection.IsCardSelected(Wood, 0));
    }

    [Fact]
    public void IncreasingTradeQuantityKeepsTheCardPickedByTheMouse()
    {
        var selection = new OperationSelection();
        selection.Toggle(Wood, 4, 5);
        selection.SetCount(Wood, 2, 5);

        Assert.True(selection.IsCardSelected(Wood, 4));
        Assert.True(selection.IsCardSelected(Wood, 0));
        Assert.Equal(2, selection.Selected[Wood]);
        selection.SetCount(Wood, 9, 5);
        Assert.Equal(5, selection.Selected[Wood]);
        Assert.All(Enumerable.Range(0, 5), ordinal => Assert.True(selection.IsCardSelected(Wood, ordinal)));
    }

    [Fact]
    public void RepaintingKeepsNonAdjacentIdentitiesAndChangingSeatClearsThem()
    {
        var snapshot = Snapshot();
        var selection = new OperationSelection();
        selection.Sync(snapshot);
        selection.Toggle(Wood, 4, 12);
        selection.Sync(snapshot);
        Assert.True(selection.IsCardSelected(Wood, 4));
        Assert.False(selection.IsCardSelected(Wood, 0));

        snapshot.Seat = "P2";
        selection.Sync(snapshot);
        Assert.False(selection.IsCardSelected(Wood, 4));
        Assert.All(selection.Selected, count => Assert.Equal(0, count));
    }

    [Theory]
    [InlineData(-1, 0, 2)]
    [InlineData(8, 0, 2)]
    [InlineData(Wood, -1, 2)]
    [InlineData(Wood, 0, -1)]
    [InlineData(Wood, int.MaxValue, 2)]
    public void InvalidResourceClicksCannotCreateNegativeOrOutOfBoundsSelection(int good, int ordinal, int owned)
    {
        var selection = new OperationSelection();
        selection.Toggle(Brick, 0, 1);
        selection.Toggle(good, ordinal, owned);

        Assert.Equal(1, selection.Selected[Brick]);
        Assert.All(selection.Selected, count => Assert.InRange(count, 0, 2));
    }

    [Fact]
    public void RepaintingTheSameViewPreservesSelection()
    {
        var snapshot = Snapshot();
        var selection = SelectRoad(snapshot);
        selection.Sync(snapshot);

        Assert.Equal("road", selection.BuildId);
        Assert.Equal(Bag((Wood, 1), (Brick, 1)), selection.Selected);
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("revision")]
    [InlineData("seat")]
    [InlineData("hand")]
    [InlineData("hidden")]
    public void ViewChangesClearPrivateBuildAndResourceDrafts(string change)
    {
        var snapshot = Snapshot();
        var selection = SelectRoad(snapshot);
        switch (change)
        {
            case "scope": snapshot.Scope = "another-match:P1"; break;
            case "revision": snapshot.Revision = "view-2"; break;
            case "seat": snapshot.Seat = "P2"; break;
            case "hand": snapshot.Goods[Wool]++; break;
        }
        selection.Sync(change == "hidden" ? null! : snapshot);

        Assert.Null(selection.BuildId);
        Assert.All(selection.Selected, count => Assert.Equal(0, count));
    }

    [Fact]
    public void CancelClearsAllSelectedCardsAndThePreparedBuild()
    {
        var selection = SelectRoad(Snapshot());
        selection.Clear();

        Assert.Null(selection.BuildId);
        Assert.All(selection.Selected, count => Assert.Equal(0, count));
    }

    [Theory]
    [InlineData(0, "trade")]
    [InlineData(1, "politics")]
    [InlineData(2, "science")]
    public void OpeningAFacilityReturnsOnlyTheClickedConstructionBoard(int track, string expectedId)
    {
        var snapshot = Snapshot();
        snapshot.Builds.AddRange(new[]
        {
            new OperationBuild { Id = "trade", Track = 0, Level = 2 },
            new OperationBuild { Id = "politics", Track = 1, Level = 1 },
            new OperationBuild { Id = "science", Track = 2, Level = 4 }
        });

        var selected = OperationPanelOptions.Facility(snapshot, track);

        Assert.Equal(expectedId, selected.Id);
        Assert.Equal(track, selected.Track);
        Assert.Equal(3, snapshot.Builds.Count);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    public void MissingOrInvalidFacilityCannotOpenAnUnrelatedConstructionBoard(int track)
    {
        var snapshot = Snapshot();
        snapshot.Builds.Add(new OperationBuild { Id = "road", Track = -1 });
        snapshot.Builds.Add(new OperationBuild { Id = "politics", Track = 1 });

        Assert.Null(OperationPanelOptions.Facility(snapshot, track));
        Assert.Null(OperationPanelOptions.Facility(null!, 1));
    }

    [Fact]
    public void BankAndPlayerPaymentChoicesContainOnlyGoodsActuallyInTheHand()
    {
        var snapshot = Snapshot();
        snapshot.Goods = Bag((Brick, 1), (Wheat, 3));
        snapshot.Bank[Wood] = 0;

        Assert.Equal(new[] { Brick, Wheat }, OperationPanelOptions.PaymentGoods(snapshot));
        Assert.Equal(Bag((Brick, 1), (Wheat, 3)), snapshot.Goods);
        Assert.Equal(19, snapshot.Bank[Ore]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FilteringPaymentsStillAllowsRequestingAResourceMissingFromOwnHand(bool bank)
    {
        var snapshot = Snapshot();
        snapshot.Goods = Bag((Wood, 4));
        var trade = BankTrade(Wood, bank ? 4 : 1, Ore, 1);
        trade.Bank = bank;
        trade.OtherPlayer = "P2";

        Assert.Equal(new[] { Wood }, OperationPanelOptions.PaymentGoods(snapshot));
        Assert.Null(OperationSelection.ValidateTrade(snapshot, trade));
        Assert.Equal(0, snapshot.Goods[Ore]);
    }

    [Theory]
    [InlineData(5, new[] { Wood, Ore })]
    [InlineData(8, new[] { Wood, Ore, Coin, Paper })]
    public void PaymentChoicesRespectTheCurrentRulesCommodityVisibility(int count, int[] expected)
    {
        var snapshot = Snapshot();
        snapshot.GoodCount = count;
        snapshot.Goods = Bag((Wood, 2), (Ore, 1), (Coin, 2), (Paper, 1));

        Assert.Equal(expected, OperationPanelOptions.PaymentGoods(snapshot));
    }

    [Fact]
    public void ResourcePortPaymentChoicesContainOnlyItsMatchingOwnedResource()
    {
        var snapshot = Snapshot();
        snapshot.GoodCount = 8;
        snapshot.Goods = Bag((Wood, 3), (Wool, 1), (Ore, 4), (Cloth, 2));

        Assert.Equal(new[] { Wool }, OperationPanelOptions.PaymentGoods(snapshot, Wool));
        Assert.Equal(new[] { Wood, Wool, Ore, Cloth }, OperationPanelOptions.PaymentGoods(snapshot));
        Assert.Empty(OperationPanelOptions.PaymentGoods(snapshot, Wheat));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(Cloth)]
    [InlineData(Paper)]
    [InlineData(8)]
    public void InvalidResourcePortCannotExposeOtherPaymentGoods(int portGood)
    {
        var snapshot = Snapshot();
        snapshot.GoodCount = 8;
        Assert.Empty(OperationPanelOptions.PaymentGoods(snapshot, portGood));
    }

    [Fact]
    public void EmptyOrHiddenHandHasNoPaymentChoiceAndCannotKeepAPreviousResourceSelected()
    {
        var snapshot = Snapshot();
        snapshot.Goods = Bag();
        Assert.Empty(OperationPanelOptions.PaymentGoods(snapshot));
        Assert.Empty(OperationPanelOptions.PaymentGoods(null!));
        Assert.Equal(-1, OperationPanelOptions.PaymentChoice(OperationPanelOptions.PaymentGoods(snapshot), Wood));
        Assert.Equal(-1, OperationPanelOptions.PaymentChoice(null!, Wood));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyHandCannotSendEitherTradeTypeAndExplainsWhy(bool bank)
    {
        var snapshot = Snapshot();
        snapshot.Goods = Bag();
        var trade = new OperationTrade { Bank = bank, OtherPlayer = "P2" };

        Assert.Equal("当前没有可付出的资源或商品", OperationSelection.ValidateTrade(snapshot, trade));
        trade.Give[Wood] = 1;
        trade.Receive[Ore] = 1;
        Assert.Equal("当前没有可付出的资源或商品", OperationSelection.ValidateTrade(snapshot, trade));
        Assert.Equal(Bag(), snapshot.Goods);
    }

    [Fact]
    public void OwnedResourcesStillCannotSendAQuoteWithEitherSideEmpty()
    {
        var snapshot = Snapshot();
        var trade = new OperationTrade { OtherPlayer = "P2", Give = Bag((Wood, 1)) };
        Assert.Equal("请在两侧选择资源", OperationSelection.ValidateTrade(snapshot, trade));
        trade.Give = Bag();
        trade.Receive = Bag((Ore, 1));
        Assert.Equal("请在两侧选择资源", OperationSelection.ValidateTrade(snapshot, trade));
    }

    [Fact]
    public void OpeningTradeSelectsAnOwnedResourceAndPreservesAStillVisibleChoice()
    {
        var choices = new[] { Wool, Ore };

        Assert.Equal(Wool, OperationPanelOptions.PaymentChoice(choices, Wood));
        Assert.Equal(Ore, OperationPanelOptions.PaymentChoice(choices, Ore));
        Assert.Equal(Wool, OperationPanelOptions.PaymentChoice(choices, -1));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void BankUsesCurrentPortRateForMultipleLots(int rate)
    {
        var snapshot = Snapshot();
        snapshot.Rates[Wood] = rate;
        var trade = BankTrade(Wood, rate * 2, Ore, 2);

        Assert.Null(OperationSelection.ValidateTrade(snapshot, trade));
        Assert.Equal(12, snapshot.Goods[Wood]);
        Assert.Equal(19, snapshot.Bank[Ore]);
    }

    [Theory]
    [InlineData(4, 3)]
    [InlineData(3, 4)]
    [InlineData(2, 1)]
    [InlineData(1, 1)]
    public void BankRejectsAmountsThatDoNotUseAnAuthorizedRate(int rate, int payment)
    {
        var snapshot = Snapshot();
        snapshot.Rates[Wood] = rate;

        AssertRejected(snapshot, BankTrade(Wood, payment, Ore, 1));
    }

    [Fact]
    public void BankCannotProvideUnavailableStock()
    {
        var snapshot = Snapshot();
        snapshot.Bank[Ore] = 1;
        AssertRejected(snapshot, BankTrade(Wood, 8, Ore, 2));
        Assert.Null(OperationSelection.ValidateTrade(snapshot, BankTrade(Wood, 4, Ore, 1)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PaymentNeverExceedsCurrentHandForEitherTradeRoute(bool bank)
    {
        var snapshot = Snapshot();
        snapshot.Goods[Wood] = 3;
        var trade = BankTrade(Wood, 4, Ore, 1);
        trade.Bank = bank;
        trade.OtherPlayer = "P2";

        AssertRejected(snapshot, trade);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SameGoodCannotAppearOnBothSides(bool bank)
    {
        var trade = BankTrade(Wood, 4, Wood, 1);
        trade.Bank = bank;
        trade.OtherPlayer = "P2";
        AssertRejected(Snapshot(), trade);
    }

    [Fact]
    public void BankTradesOneGoodPerSideWhilePlayerOffersCanMixGoods()
    {
        var trade = new OperationTrade
        {
            Bank = true, OtherPlayer = "P2",
            Give = Bag((Wood, 1), (Wool, 1)), Receive = Bag((Brick, 1), (Ore, 1))
        };
        AssertRejected(Snapshot(), trade);
        trade.Bank = false;
        Assert.Null(OperationSelection.ValidateTrade(Snapshot(), trade));
    }

    [Fact]
    public void PlayerOfferDoesNotRequireBankStockOrExposeRecipientsHiddenHand()
    {
        var snapshot = Snapshot();
        Array.Clear(snapshot.Bank);
        var trade = BankTrade(Wood, 1, Ore, 2);
        trade.Bank = false;
        trade.OtherPlayer = "P2";

        Assert.Null(OperationSelection.ValidateTrade(snapshot, trade));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("P1")]
    [InlineData("P9")]
    public void PlayerOfferRequiresAnotherVisibleSeat(string? recipient)
    {
        var trade = BankTrade(Wood, 1, Ore, 1);
        trade.Bank = false;
        trade.OtherPlayer = recipient!;
        AssertRejected(Snapshot(), trade);
    }

    [Theory]
    [InlineData(Paper, true)]
    [InlineData(Cloth, true)]
    [InlineData(Coin, true)]
    [InlineData(Paper, false)]
    [InlineData(Cloth, false)]
    [InlineData(Coin, false)]
    public void CommodityTradeIsOnlyAvailableInRulesThatExposeCommodities(int commodity, bool payingCommodity)
    {
        var snapshot = Snapshot();
        var trade = payingCommodity ? BankTrade(commodity, 4, Ore, 1) : BankTrade(Wood, 4, commodity, 1);
        AssertRejected(snapshot, trade);
        snapshot.GoodCount = 8;
        Assert.Null(OperationSelection.ValidateTrade(snapshot, trade));
    }

    [Theory]
    [InlineData("phase")]
    [InlineData("other-seat")]
    [InlineData("pending-offer")]
    [InlineData("hidden-view")]
    public void PendingOrUnauthorizedViewsCannotStartATrade(string block)
    {
        var snapshot = Snapshot();
        if (block == "phase") snapshot.CanTrade = false;
        if (block == "other-seat") snapshot.CanInteract = false;
        if (block == "pending-offer") snapshot.Offer = new OperationOffer();
        AssertRejected(block == "hidden-view" ? null! : snapshot, BankTrade(Wood, 4, Ore, 1));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("null-give")]
    [InlineData("null-receive")]
    [InlineData("short-give")]
    [InlineData("long-receive")]
    [InlineData("negative-give")]
    [InlineData("negative-receive")]
    [InlineData("empty-give")]
    [InlineData("empty-receive")]
    [InlineData("overflow-give")]
    [InlineData("overflow-receive")]
    public void MalformedTradeDraftIsRejectedWithoutThrowingOrMutatingTheView(string kind)
    {
        var snapshot = Snapshot();
        var trade = BankTrade(Wood, 4, Ore, 1);
        switch (kind)
        {
            case "null-give": trade.Give = null!; break;
            case "null-receive": trade.Receive = null!; break;
            case "short-give": trade.Give = new int[7]; break;
            case "long-receive": trade.Receive = new int[9]; break;
            case "negative-give": trade.Give[Wood] = -1; break;
            case "negative-receive": trade.Receive[Ore] = -1; break;
            case "empty-give": Array.Clear(trade.Give); break;
            case "empty-receive": Array.Clear(trade.Receive); break;
            case "overflow-give": trade.Give[Wood] = int.MaxValue; trade.Give[Wool] = 1; break;
            case "overflow-receive": trade.Receive[Ore] = int.MaxValue; trade.Receive[Wool] = 1; break;
        }
        AssertRejected(snapshot, kind == "null" ? null! : trade);
        Assert.Equal(12, snapshot.Goods[Wood]);
        Assert.Equal(19, snapshot.Bank[Ore]);
    }

    [Theory]
    [InlineData(Ore, 20)]
    [InlineData(Paper, 13)]
    public void PlayerCannotRequestMoreOfAGoodThanItsFiniteSupply(int good, int count)
    {
        var snapshot = Snapshot();
        snapshot.GoodCount = 8;
        var trade = BankTrade(Wood, 1, good, count);
        trade.Bank = false;
        trade.OtherPlayer = "P2";

        AssertRejected(snapshot, trade);
    }

    static OperationSelection SelectRoad(OperationSnapshot snapshot)
    {
        var selection = new OperationSelection();
        selection.Sync(snapshot);
        selection.Choose(Build("road", (Wood, 1), (Brick, 1)), snapshot.Goods);
        return selection;
    }

    static OperationSnapshot Snapshot() => new()
    {
        Scope = "match-1:P1", Revision = "view-1", Seat = "P1", CanTrade = true, CanInteract = true,
        Goods = Bag((Wood, 12), (Brick, 2), (Wool, 2), (Wheat, 2), (Ore, 4), (Paper, 4), (Cloth, 4), (Coin, 4)),
        Bank = new[] { 19, 19, 19, 19, 19, 12, 12, 12 },
        Rates = new[] { 4, 4, 4, 4, 4, 4, 4, 4 }, OtherPlayers = new[] { "P2", "P3" }
    };

    static OperationBuild Build(string id, params (int Good, int Count)[] cost) => new() { Id = id, Cost = Bag(cost), Available = true };

    static OperationTrade BankTrade(int give, int giveCount, int receive, int receiveCount) => new()
    {
        Bank = true, Give = Bag((give, giveCount)), Receive = Bag((receive, receiveCount))
    };

    static int[] Bag(params (int Good, int Count)[] goods)
    {
        var result = new int[8];
        foreach (var item in goods) result[item.Good] = item.Count;
        return result;
    }

    static void AssertRejected(OperationSnapshot snapshot, OperationTrade trade)
    {
        Assert.False(string.IsNullOrWhiteSpace(OperationSelection.ValidateTrade(snapshot, trade)));
    }
    [Fact]
    public void B45HandLongPressAndSwipeCannotAlsoSelectOnRelease()
    {
        var gesture=new OperationHandGesture();
        gesture.Begin(100,100,0);Assert.False(gesture.Inspect(.49));Assert.True(gesture.Inspect(.5));
        Assert.False(gesture.Inspect(.8));Assert.False(gesture.Release(true));
        gesture.Begin(100,100,1);gesture.Move(125,100,18);Assert.True(gesture.Moved);
        Assert.False(gesture.Inspect(2));Assert.False(gesture.Release(true));
        gesture.Begin(100,100,3);gesture.Move(102,101,18);Assert.True(gesture.Release(true));
        gesture.Begin(100,100,4);Assert.False(gesture.Release(false));
        gesture.Begin(100,100,5);gesture.Cancel();Assert.False(gesture.Release(true));
    }
    [Fact]
    public void B45HandBridgeRequiresActiveAuthorizedCandidateAndPreservesIdentity()
    {
        var selection=new OperationHandSelection{CanSelectGood=(good,ordinal)=>good==3&&ordinal==2,CanSelectCard=id=>id=="progress:17"};
        Assert.False(selection.GoodAllowed(3,2));Assert.False(selection.CardAllowed("progress:17"));
        selection.Active=true;Assert.True(selection.GoodAllowed(3,2));Assert.False(selection.GoodAllowed(3,1));
        Assert.True(selection.CardAllowed("progress:17"));Assert.False(selection.CardAllowed("progress:18"));
        var order=new TabletopSpecialHandOrder();order.Sync(new[]{"progress:17","progress:18"});order.Sort(new[]{"progress:18","progress:17"});
        Assert.True(selection.CardAllowed(order.Sync(new[]{"progress:17","progress:18"})[1]));
    }
}
