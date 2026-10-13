using Catan.Core;
using Xunit;
using M2 = Catan.Core.M2;
using M3 = Catan.Core.M3;
using M4 = Catan.Core.M4;
using M5 = Catan.Core.M5;

namespace Catan.Rules.Tests;

// 固定可核对的展示输入，并用真实 PlayerView 检查隐藏分不干扰对手的展示。
public sealed class TabletopScoreTests
{
    static void Set(object target,string property,object? value) => target.GetType().GetProperty(property)!.SetValue(target,value);
    static PiecePlacement Piece(string player,string id) => new() { PlayerId=player,LocationId=id };
    static string[] Summary(TabletopScoreBreakdown score) => score.Rows.Select(r=>$"{r.Name}:{r.Points}:{r.Hidden}:{r.Detail}").ToArray();

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void NewGameHasNoInventedPoints(int mode)
    {
        var score=mode switch
        {
            2=>TabletopScoreModel.For(M2Harness.New().GetPlayerView("P1"),"P1"),
            3=>TabletopScoreModel.For(M3Harness.New().GetPlayerView("P1"),"P1"),
            4=>TabletopScoreModel.For(M4CoreHarness.New().GetPlayerView("P1"),"P1"),
            _=>TabletopScoreModel.For(M5Harness.New().GetPlayerView("P1"),"P1")
        };
        Assert.True(score.IsOwn);Assert.Equal(0,score.Total);Assert.Empty(score.Rows);
    }

    [Fact]
    public void BaseGameExplainsBuildingsBothAwardsAndOwnHiddenCards()
    {
        var view=M2Harness.New().GetPlayerView("P1");
        Set(view.Board,"Settlements",new[]{Piece("P1","V1"),Piece("P1","V2"),Piece("P2","V3")});
        Set(view.Board,"Cities",new[]{Piece("P1","V4"),Piece("P1","V5")});
        Set(view,"LongestRoadPlayerId","P1");Set(view,"LargestArmyPlayerId","P1");
        Set(view,"OwnDevelopmentCards",new[]{new M2.DevelopmentCard{Kind=M2.DevelopmentCardKind.VictoryPoint},new M2.DevelopmentCard{Kind=M2.DevelopmentCardKind.Knight},new M2.DevelopmentCard{Kind=M2.DevelopmentCardKind.VictoryPoint}});
        var score=TabletopScoreModel.For(view,"P1");
        Assert.Equal(new[]{"定居点","城市","最长道路","最大骑士军队","胜利点卡"},score.Rows.Select(r=>r.Name));
        Assert.Equal(new[]{2,4,2,2,2},score.Rows.Select(r=>r.Points));
        Assert.Equal(10,score.PublicPoints);Assert.Equal(2,score.HiddenPoints);Assert.Equal(12,score.Total);
        Assert.Single(score.Rows,r=>r.Hidden);
        var other=TabletopScoreModel.For(view,"P2");
        Assert.Equal(1,other.Total);Assert.False(other.IsOwn);Assert.Equal(0,other.HiddenPoints);
    }

    [Theory]
    [InlineData(0,0)]
    [InlineData(1,0)]
    [InlineData(5,2)]
    [InlineData(6,3)]
    public void SeafarersExplainsIslandRewardsAndOnlyCompleteClothPairs(int cloth,int clothPoints)
    {
        var view=M3Harness.New().GetPlayerView("P1");var player=view.Players[0];
        Set(view.Board,"Settlements",new[]{Piece("P1","V1")});Set(view.Board,"Cities",new[]{Piece("P1","V2")});
        Set(view,"LongestRoadPlayerId","P1");Set(player,"BonusVictoryPoints",4);Set(player,"Cloth",cloth);
        Set(view,"OwnDevelopmentCards",new[]{new M3.DevelopmentCard{Kind=M3.DevelopmentCardKind.VictoryPoint}});
        var score=TabletopScoreModel.For(view,"P1");
        Assert.Equal(9+clothPoints,score.PublicPoints);Assert.Equal(10+clothPoints,score.Total);
        Assert.Equal(clothPoints,score.Rows.Where(r=>r.Name=="布匹").Sum(r=>r.Points));
        Assert.Equal(4,score.Rows.Single(r=>r.Name=="剧本奖励").Points);
        Assert.Equal(2,score.Rows.Single(r=>r.Name=="最长航线").Points);
    }

    [Fact]
    public void CitiesAndKnightsCountsMetropolisAsTwoExtraAndPublicCardsForEverySeat()
    {
        var view=M4CoreHarness.New().GetPlayerView("P1");var player=view.Players[1];
        Set(view.Board,"Settlements",new[]{Piece("P2","V1")});Set(view.Board,"Cities",new[]{Piece("P2","V2")});
        Set(view.Board,"Metropolises",new[]{new M4.Metropolis{PlayerId="P2",LocationId="V2"}});
        Set(view,"LongestRoadPlayerId","P2");Set(view,"MerchantPlayerId","P2");Set(player,"DefenderPoints",3);
        Set(player,"ProgressVictoryCards",new[]{M4.ProgressCardKind.Printing,M4.ProgressCardKind.Constitution});
        var score=TabletopScoreModel.For(view,"P2");
        Assert.Equal(new[]{"定居点","城市","最长道路","大都会加分","卡坦保卫者","商人","印刷术","宪法"},score.Rows.Select(r=>r.Name));
        Assert.Equal(new[]{1,2,2,2,3,1,1,1},score.Rows.Select(r=>r.Points));
        Assert.Equal(13,score.PublicPoints);Assert.Equal(13,score.Total);Assert.All(score.Rows,r=>Assert.False(r.Hidden));
    }

    [Fact]
    public void CombinedGameIncludesBothExpansionPointSourcesWithoutLargestArmy()
    {
        var view=M5Harness.New().GetPlayerView("P1");var player=view.Players[0];
        Set(view.Board,"Cities",new[]{Piece("P1","V1")});Set(view,"LongestRoadPlayerId","P1");
        Set(view.Board,"Metropolises",new[]{new M5.Metropolis{PlayerId="P1",LocationId="V1"}});
        Set(player,"BonusVictoryPoints",4);Set(player,"DefenderPoints",2);Set(view,"MerchantPlayerId","P1");
        Set(player,"ProgressVictoryCards",new[]{M5.ProgressCardKind.Printing,M5.ProgressCardKind.Constitution});
        var score=TabletopScoreModel.For(view,"P1");
        Assert.Equal(15,score.Total);Assert.Equal(4,score.Rows.Single(r=>r.Name=="登岛奖励").Points);
        Assert.Equal(2,score.Rows.Single(r=>r.Name=="大都会加分").Points);
        Assert.DoesNotContain(score.Rows,r=>r.Name=="最大骑士军队"||r.Hidden);
    }

    [Fact]
    public void ChangingAnotherPlayersHiddenCardsDoesNotChangeTheirVisibleBreakdown()
    {
        var plain=M2Harness.Fixture(s=>M2Harness.Card(s,M2.DevelopmentCardKind.Knight,"P2"));
        var secret=M2Harness.Fixture(s=>M2Harness.Card(s,M2.DevelopmentCardKind.VictoryPoint,"P2"));
        var before=secret.Save();
        var a=TabletopScoreModel.For(plain.GetPlayerView("P1"),"P2");
        var b=TabletopScoreModel.For(secret.GetPlayerView("P1"),"P2");
        Assert.Equal(Summary(a),Summary(b));Assert.Equal(0,b.Total);Assert.Equal(0,b.HiddenPoints);
        var own=TabletopScoreModel.For(secret.GetPlayerView("P2"),"P2");
        Assert.Equal(1,own.Total);Assert.Equal(1,own.HiddenPoints);Assert.Equal(before,secret.Save());
    }

    [Fact]
    public void BaseGameOpponentScoreStaysPublicEvenWhenWinnerTotalIsRevealedByView()
    {
        var view=M2Harness.New().GetPlayerView("P1");Set(view,"WinnerPlayerId","P2");
        Set(view.Players[1],"VictoryPoints",11);Set(view.Board,"Cities",new[]{Piece("P2","V1")});
        var score=TabletopScoreModel.For(view,"P2");
        Assert.Equal(2,score.PublicPoints);Assert.Equal(2,score.Total);Assert.DoesNotContain(score.Rows,r=>r.Hidden);
    }

    [Fact]
    public void SeafarersOpponentScoreNeverInfersHiddenCardsFromTotal()
    {
        var view=M3Harness.New().GetPlayerView("P1");Set(view,"WinnerPlayerId","P2");
        Set(view.Players[1],"VictoryPoints",14);Set(view.Players[1],"BonusVictoryPoints",2);
        Set(view,"OwnDevelopmentCards",new[]{new M3.DevelopmentCard{Kind=M3.DevelopmentCardKind.VictoryPoint}});
        var score=TabletopScoreModel.For(view,"P2");
        Assert.Equal(2,score.Total);Assert.Equal(0,score.HiddenPoints);Assert.All(score.Rows,r=>Assert.False(r.Hidden));
    }

    [Fact]
    public void UnknownSeatDoesNotBorrowCurrentPlayersScore()
    {
        Assert.Null(TabletopScoreModel.For(M2Harness.New().GetPlayerView("P1"),"P0"));
        Assert.Null(TabletopScoreModel.For(M3Harness.New().GetPlayerView("P1"),"P0"));
        Assert.Null(TabletopScoreModel.For(M4CoreHarness.New().GetPlayerView("P1"),"P0"));
        Assert.Null(TabletopScoreModel.For(M5Harness.New().GetPlayerView("P1"),"P0"));
    }

    [Theory]
    [InlineData("wonders-of-catan")]
    [InlineData("pirate-islands")]
    public void SpecialVictoryConditionsDoNotInventAdditionalVictoryPoints(string scenario)
    {
        var view=M3Harness.New(scenario).GetPlayerView("P1");
        Set(view.Players[0],"WonderLevel",4);
        foreach(var fortress in view.Board.Fortresses)fortress.Strength=0;
        var score=TabletopScoreModel.For(view,"P1");
        Assert.Equal(view.OwnVictoryPoints,score.Total);
        Assert.DoesNotContain(score.Rows,r=>r.Name.Contains("奇观")||r.Name.Contains("堡垒"));
    }
}
