using Catan.Core;
using Xunit;

namespace Catan.Rules.Tests;

public sealed class FeedbackHistoryTests
{
    static T Set<T>(T value,string name,object? data) where T:class
    {typeof(T).GetProperty(name)!.SetValue(value,data);return value;}
    static GameHistoryChange Change(string player,string category,string name,int amount,int good=-1,string location="V01")
    {
        var c=new GameHistoryChange();Set(c,"PlayerId",player);Set(c,"Category",category);Set(c,"Name",name);Set(c,"Amount",amount);Set(c,"GoodIndex",good);Set(c,"LocationId",location);return c;
    }
    static GameHistoryEntry Entry(string kind,string action,params GameHistoryChange[] changes)
    {var e=new GameHistoryEntry();Set(e,"Kind",kind);Set(e,"Action",action);Set(e,"PlayerId","P1");Set(e,"Changes",changes);return e;}

    [Fact] public void StealingHasOneSentenceForBothPrivateAndRedactedViews()
    {
        var game=M2Harness.Fixture(s=>
        {
            s.Phase=Catan.Core.M2.GamePhase.RobberSteal;
            s.Settlements=[M2Harness.Piece(M2Harness.New().GetPlayerView("P1").Board.Tiles.Single(t=>t.Id==s.RobberTileId).Vertices[0],"P2")];
            s.PendingDecision=new(){Kind="Steal",PlayerId="P1",EligibleVictimIds=["P2"],ReturnPhase=Catan.Core.M2.GamePhase.Action};
            M2Harness.Player(s,"P2").Resources.Ore=1;
        });
        var c=M2Harness.Cmd(Catan.Core.M2.CommandKind.StealResource);c.OtherPlayerId="P2";M2Harness.Accepted(game,c);
        foreach(string viewer in new[]{"P1","P2","P3"})
        {
            var line=Assert.Single(TabletopHistoryModel.Lines(Assert.Single(game.GetHistory(viewer))));
            Assert.Equal("P1",line.Player);Assert.Equal("P2",line.Partner);Assert.Equal("偷取了",line.Prefix);Assert.Equal(" 1张手牌",line.PartnerAction);Assert.Empty(line.Cards);
        }
    }
    [Fact] public void WeddingAndMonopolyMergeBothSidesWithoutFillingUnknownGoods()
    {
        var wedding=Entry("ResolveProgressChoice","交出婚礼礼物",Change("P2","goods","木材",-2,0),Change("P1","goods","木材",2,0));
        var line=Assert.Single(TabletopHistoryModel.Lines(wedding));Assert.Equal("P1",line.Player);Assert.Equal("P2",line.Partner);Assert.Equal("收取了",line.Prefix);Assert.Equal("交出的婚礼礼物：",line.PartnerAction);Assert.Single(line.Cards);
        Set(wedding,"Changes",new[]{Change("P2","resource-count","手牌",-2),Change("P1","resource-count","手牌",2)});
        line=Assert.Single(TabletopHistoryModel.Lines(wedding));Assert.All(line.Cards,c=>Assert.Equal(-1,c.GoodIndex));
        var monopoly=Entry("PlayMonopoly","使用「垄断」",Change("P1","goods","矿石",5,4),Change("P2","goods","矿石",-2,4),Change("P3","goods","矿石",-3,4));
        var lines=TabletopHistoryModel.Lines(monopoly);Assert.Equal(3,lines.Length);Assert.Equal(2,lines.Count(l=>l.Partner!=null));Assert.DoesNotContain(lines,l=>l.Cards.Any(c=>c.Amount>0));
    }
    [Theory][InlineData(0,4,"银行")][InlineData(1,4,"大教堂")][InlineData(2,3,"引水渠")]
    public void FacilityCostAndNameAreOneColoredSentence(int track,int level,string name)
    {
        var e=Entry("ImproveCity","升级城市设施",Change("P1","goods","商品",-level,5+track),Change("P1","status",new[]{"贸易","政治","科学"}[track]+"改良（现为 "+level+" 级）",1));
        var line=Assert.Single(TabletopHistoryModel.Lines(e));Assert.Equal("使用",line.Prefix);Assert.Equal("建造了"+name,line.Suffix);Assert.Equal(name,line.Emphasis);Assert.Equal(track,line.Track);Assert.Single(line.Cards);
    }
    [Theory][InlineData("村庄")][InlineData("城市")]
    public void SetupBuildingsAreConstructionWithoutDuplicateGainedPieces(string piece)
    {var line=Assert.Single(TabletopHistoryModel.Lines(Entry("SetupSettlement","摆放开局建筑",Change("P1","piece",piece,1))));Assert.Equal("建造了开局"+piece,line.Prefix);}
    [Fact] public void MultipleSleepingKnightsAreOneStatusSentence()
    {
        var e=Entry("RollDice","掷骰",Change("P1","piece","1级骑士（激活）",-1),Change("P1","piece","1级骑士（休眠）",1),Change("P1","piece","2级骑士（激活）",-1,location:"V02"),Change("P1","piece","2级骑士（休眠）",1,location:"V02"));
        var lines=TabletopHistoryModel.Lines(e);Assert.Equal(2,lines.Length);Assert.Equal("的1级骑士、2级骑士已休眠",lines[1].Prefix);
    }
    [Theory][InlineData("锻铁匠",2)][InlineData("商业港口",0)][InlineData("婚礼",1)]
    public void PlayedProgressCardUsesAuthorizedTrackColor(string name,int track)
    {var line=Assert.Single(TabletopHistoryModel.Lines(Entry("PlayProgressCard","使用「"+name+"」",Change("P1","progress",name,-1))));Assert.Equal("使用「"+name+"」",line.Prefix);Assert.Equal(name,line.Emphasis);Assert.Equal(track,line.Track);Assert.Equal(-1,TabletopHistoryModel.CardTrack("发展卡"));}
    [Fact] public void HiddenOperationRetainsCaptionUntilStateChanges()
    {var window=new OperationWindowState();window.Sync("P1","1");window.Hide("婚礼赠礼");window.Sync("P1","1");Assert.True(window.Hidden);Assert.Equal("婚礼赠礼",window.Caption);window.Open();Assert.False(window.Hidden);window.Hide("金矿");window.Sync("P1","2");Assert.False(window.Hidden);Assert.Null(window.Caption);}
    [Fact] public void SmithingKeepsConfirmablePrefixesIncludingSupplyReleaseOrder()
    {
        var plans=TabletopSmithingSelection.Plans(["弱","强"],ids=>ids.SequenceEqual(new[]{"强","弱"})||ids.SequenceEqual(new[]{"强"}));
        Assert.Equal(2,plans.Length);Assert.Equal(new[]{"强"},TabletopSmithingSelection.NextTargets(plans,[]));
        Assert.Equal(new[]{"弱"},TabletopSmithingSelection.NextTargets(plans,["强"]));Assert.True(TabletopSmithingSelection.CanConfirm(plans,["强"]));
        Assert.True(TabletopSmithingSelection.CanConfirm(plans,["强","弱"]));Assert.False(TabletopSmithingSelection.CanConfirm(plans,["弱","强"]));
        plans=TabletopSmithingSelection.Plans(["一","二"],ids=>ids.SequenceEqual(new[]{"一"}));Assert.Equal(new[]{"一"},Assert.Single(plans));
        Assert.Empty(TabletopSmithingSelection.Plans(["一"],_=>false));
    }
    [Fact] public void SmithingDoubleEffectAllowsEveryLegalPrefixThroughFourWithoutDuplicates()
    {
        string[] order=["强一","弱一","强二","弱二"];int previews=0;
        var plans=TabletopSmithingSelection.Plans(["弱一","弱二","强一","强二","强二"],ids=>{previews++;return ids.SequenceEqual(order.Take(ids.Length));},4);
        Assert.Equal(4,plans.Length);Assert.InRange(previews,4,10);
        for(int count=1;count<=4;count++)Assert.True(TabletopSmithingSelection.CanConfirm(plans,order.Take(count).ToArray()));
        Assert.Equal(new[]{"强二"},TabletopSmithingSelection.NextTargets(plans,["强一","弱一"]));Assert.Empty(TabletopSmithingSelection.NextTargets(plans,order));
        Assert.False(TabletopSmithingSelection.CanConfirm(plans,["强一","弱一","强一"]));
    }
    [Fact] public void SmithingOrdinaryPackageStillStopsAtTwoAndNeverOffersInvalidContinuation()
    {
        var plans=TabletopSmithingSelection.Plans(["一","二","三","四"],ids=>!ids.Contains("四"));
        Assert.All(plans,plan=>Assert.InRange(plan.Length,1,2));Assert.Empty(TabletopSmithingSelection.NextTargets(plans,["一","二"]));
        Assert.DoesNotContain("四",TabletopSmithingSelection.NextTargets(plans,[]));Assert.False(TabletopSmithingSelection.CanConfirm(plans,[]));
    }
}
