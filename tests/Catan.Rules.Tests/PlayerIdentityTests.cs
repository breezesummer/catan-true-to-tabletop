using Xunit;

namespace Catan.Rules.Tests;

public sealed class PlayerIdentityTests
{
    [Fact]
    public void BuildingColorsRemainStableWhenBlueWinsTheStartingRoll()
    {
        var ids=new[]{"P1","P2","P3","P4"};
        Assert.Equal(new[]{"P2","P3","P4","P1"},PlayerIdentityModel.ActionOrder("P2",ids));
        Assert.Equal(new[]{"红色","蓝色","橙色","米白色"},ids.Select(PlayerIdentityModel.ColorLabel));
        Assert.Equal(new[]{"P3","P1","P2"},PlayerIdentityModel.ActionOrder("P3",ids.Take(3)));
    }
    [Fact]
    public void LocalHumanUsesTheirNameAndEveryAiHasADistinctNameFromTheRequestedPool()
    {
        var observed=new HashSet<string>();
        for(int seed=0;seed<32;seed++)
        {
            var names=PlayerIdentityModel.CreateNames(4,"P2","  海风  ",seed);
            Assert.Equal("海风",names[1]);
            var ai=names.Where((_,index)=>index!=1).ToArray();
            Assert.Equal(3,ai.Distinct().Count());
            Assert.All(ai,name=>Assert.Contains(name,PlayerIdentityModel.AiNames));
            observed.UnionWith(ai);
        }
        Assert.Equal(4,observed.Count);
    }
    [Fact]
    public void DisplayMappingReplacesOnlyWholePlayerIdsAndDoesNotReprocessUserNames()
    {
        var names=new Dictionary<string,string>{{"P1","P2 的伙伴"},{"P2","糖包"}};
        Assert.Equal("P2 的伙伴 向 糖包 报价；VP1 和 P12 保持原样",PlayerIdentityModel.ReplaceIds("P1 向 P2 报价；VP1 和 P12 保持原样",id=>names[id]));
        Assert.Equal("玩家",PlayerIdentityModel.NormalizeName(" \n\t "));
        Assert.Equal("海风",PlayerIdentityModel.NormalizeName("\n海风\t"));
        Assert.Equal(20,PlayerIdentityModel.NormalizeName(new string('岛',30)).Length);
    }
    [Theory]
    [InlineData("base")]
    [InlineData("seafarers")]
    [InlineData("cities")]
    [InlineData("combined")]
    public void StartingPlayerIsPublicIdenticalForEveryViewerAndSurvivesSaving(string rules)
    {
        var firstPlayers=new HashSet<string>();
        for(uint seed=1;seed<=12;seed++)
        {
            var game=Create(rules,seed);var initial=game.View("P1");
            Assert.Equal(initial.Active,initial.Starting);
            firstPlayers.Add(initial.Starting);
            foreach(var id in initial.Ids)Assert.Equal(initial.Starting,game.View(id).Starting);
            var restored=game.Reload(game.Save());
            Assert.Equal(initial.Starting,restored.View("P3").Starting);
            Assert.Equal(initial.Ids,restored.View("P3").Ids);
        }
        Assert.True(firstPlayers.Count>1,"建筑颜色不能被当作固定的先手排名。");
    }
    sealed record Probe(Func<string,(string Active,string Starting,string[] Ids)> View,Func<string> Save,Func<string,Probe> Reload);
    static Probe Create(string rules,uint seed)=>rules switch
    {
        "base"=>Wrap(Catan.Core.M2.BaseGameSession.Create(AcceptanceFixture.Json,4,seed)),
        "seafarers"=>Wrap(Catan.Core.M3.SeafarersGameSession.Create("heading-for-new-shores",4,seed)),
        "cities"=>Wrap(Catan.Core.M4.CitiesKnightsGameSession.Create(AcceptanceFixture.Json,4,seed)),
        _=>Wrap(Catan.Core.M5.CombinedGameSession.Create("heading-for-new-shores",4,seed))
    };
    static Probe Wrap(Catan.Core.M2.BaseGameSession game)=>new(id=>{var view=game.GetPlayerView(id);return(view.ActivePlayerId,view.StartingPlayerId,view.Players.Select(p=>p.Id).ToArray());},game.Save,json=>Wrap(Catan.Core.M2.BaseGameSession.Load(AcceptanceFixture.Json,json)));
    static Probe Wrap(Catan.Core.M3.SeafarersGameSession game)=>new(id=>{var view=game.GetPlayerView(id);return(view.ActivePlayerId,view.StartingPlayerId,view.Players.Select(p=>p.Id).ToArray());},game.Save,json=>Wrap(Catan.Core.M3.SeafarersGameSession.Load(json)));
    static Probe Wrap(Catan.Core.M4.CitiesKnightsGameSession game)=>new(id=>{var view=game.GetPlayerView(id);return(view.ActivePlayerId,view.StartingPlayerId,view.Players.Select(p=>p.Id).ToArray());},game.Save,json=>Wrap(Catan.Core.M4.CitiesKnightsGameSession.Load(AcceptanceFixture.Json,json)));
    static Probe Wrap(Catan.Core.M5.CombinedGameSession game)=>new(id=>{var view=game.GetPlayerView(id);return(view.ActivePlayerId,view.StartingPlayerId,view.Players.Select(p=>p.Id).ToArray());},game.Save,json=>Wrap(Catan.Core.M5.CombinedGameSession.Load(json)));
}
