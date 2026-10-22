using Catan.AI;
using Catan.Network.Contracts;
using Xunit;
using M2 = Catan.Core.M2;
using M3 = Catan.Core.M3;
using M4 = Catan.Core.M4;
using M5 = Catan.Core.M5;

namespace Catan.Network.Tests;

public sealed class VisiblePreviewTests
{
    [Theory]
    [InlineData("base")]
    [InlineData("seafarers")]
    [InlineData("cities-knights")]
    [InlineData("combined")]
    public void NormalSetupCanBePlacedThroughActualClientPreview(string rules)
    {
        string scenario = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "scenario.json"));
        dynamic session = rules switch
        {
            "base" => (object)M2.BaseGameSession.Create(scenario, 3, 97),
            "seafarers" => M3.SeafarersGameSession.Create("heading-for-new-shores", 3, 97),
            "cities-knights" => M4.CitiesKnightsGameSession.Create(scenario, 3, 97),
            _ => M5.CombinedGameSession.Create("through-the-desert", 3, 97)
        };
        // 初设两轮12次落子覆盖村/城市以及路/船，取实际规则提供的视图并经过网络codec。
        for (int i = 0; i < 12; i++)
        {
            dynamic publicView = session.GetPlayerView("P1"); string seat = publicView.ActivePlayerId;
            object raw = session.GetPlayerView(seat);
            object view = WireJson.Deserialize(WireJson.SerializeObject(raw), raw.GetType());
            dynamic command = rules switch
            {
                "base" => (object)new BaseGameAi().Decide((M2.PlayerView)view),
                "seafarers" => new SeafarersAi().Decide((M3.PlayerView)view),
                "cities-knights" => new CitiesKnightsAi().Decide((M4.PlayerView)view),
                _ => new CombinedAi().Decide((M5.PlayerView)view)
            };
            Assert.True(M7VisiblePreview.Try((object)command, view, out M7Preview legal));
            Assert.True(legal.Success);
            string target = command.TargetId; command.TargetId = "不存在的落点";
            Assert.True(M7VisiblePreview.Try((object)command, view, out M7Preview invalid)); Assert.False(invalid.Success);
            command.TargetId = target; command.Id = "preview-" + i;
            Assert.True((bool)session.Execute(command).Success);
        }
    }
    [Theory]
    [InlineData("cities-knights")]
    [InlineData("combined")]
    public void DiceAndMultiStepChoicesFallBackToServerPreview(string rules)
    {
        string scenario = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "scenario.json"));
        object view = rules == "cities-knights" ? (object)M4.CitiesKnightsGameSession.Create(scenario, 3, 97).GetPlayerView("P1") : M5.CombinedGameSession.Create("through-the-desert", 3, 97).GetPlayerView("P1");
        object command = rules == "cities-knights" ? (object)new M4.Command { Kind = M4.CommandKind.RollDice, PlayerId = "P1" } : new M5.Command { Kind = M5.CommandKind.RollDice, PlayerId = "P1" };
        Assert.False(M7VisiblePreview.Try(command, view, out _));
    }
}
