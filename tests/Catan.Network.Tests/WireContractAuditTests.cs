using Catan.Core;
using Catan.Network.Contracts;
using Xunit;
using M2 = Catan.Core.M2;
using M3 = Catan.Core.M3;
using M4 = Catan.Core.M4;
using M5 = Catan.Core.M5;

namespace Catan.Network.Tests;

public sealed class WireContractAuditTests
{
    [Fact]
    public async Task ConcurrentWireDecodingKeepsSeatDataIndependentAndStillRejectsInvalidFields()
    {
        var game=M5.CombinedGameSession.Create("through-the-desert",4,97);
        var wires=Enumerable.Range(1,4).Select(n=>WireJson.Serialize(game.GetPlayerView("P"+n))).ToArray();
        await Task.WhenAll(Enumerable.Range(0,32).Select(n=>Task.Run(()=>
        {
            int seat=n%4;
            var view=WireJson.Deserialize<M5.PlayerView>(wires[seat]);
            Assert.Equal("P"+(seat+1),view.PlayerId);
            Assert.Equal(wires[seat],WireJson.Serialize(view));
            Assert.ThrowsAny<Exception>(()=>WireJson.Deserialize<NetworkResponse>("{\"Revision\":1,\"Revision\":2}"));
            Assert.ThrowsAny<Exception>(()=>WireJson.Deserialize<M5.PlayerView>("{\"OpponentHands\":[]}"));
        })));
    }
    private static string Scenario
    {
        get
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                var path = Path.Combine(dir.FullName, "docs", "acceptance", "v0.1", "scenario.json");
                if (File.Exists(path)) return File.ReadAllText(path);
            }
            throw new FileNotFoundException("缺少基础场景夹具。");
        }
    }
    public static IEnumerable<object[]> Configurations()
    {
        foreach (int seats in new[] { 3, 4 })
        {
            yield return new object[] { "base", "base", seats };
            yield return new object[] { "cities-knights", "base", seats };
            foreach (string scenario in M3.SeafarersScenarios.Ids) yield return new object[] { "seafarers", scenario, seats };
            foreach (string scenario in new[] { "heading-for-new-shores", "through-the-desert" }) yield return new object[] { "combined", scenario, seats };
        }
    }
    [Theory, MemberData(nameof(Configurations))]
    public void EveryRuleAndSeatViewRoundTripsWithoutAuthority(string rules, string scenario, int seats)
    {
        dynamic game = rules switch
        {
            "base" => (object)M2.BaseGameSession.Create(Scenario, seats, 97),
            "cities-knights" => M4.CitiesKnightsGameSession.Create(Scenario, seats, 97),
            "seafarers" => M3.SeafarersGameSession.Create(scenario, seats, 97),
            _ => M5.CombinedGameSession.Create(scenario, seats, 97)
        };
        for (int n = 1; n <= seats; n++)
        {
            object view = game.GetPlayerView("P" + n);
            string wire = WireJson.SerializeObject(view);
            object restored = WireJson.Deserialize(wire, view.GetType());
            Assert.Equal(wire, WireJson.SerializeObject(restored));
            foreach (string forbidden in new[] { "RandomState", "ProcessedCommands", "DevelopmentDeck\"", "ProgressDecks", "GameSave", "CredentialHash", "FogTileContents" }) Assert.DoesNotContain(forbidden, wire);
            dynamic typed = restored;
            Assert.Equal("P" + n, (string)typed.PlayerId);
            Assert.Equal(seats, (int)typed.Players.Length);
            foreach (var player in (Array)typed.Players)
            {
                var members = player.GetType().GetProperties().Select(p => p.Name).ToArray();
                Assert.DoesNotContain("Resources", members); Assert.DoesNotContain("DevelopmentCards", members); Assert.DoesNotContain("ProgressCards", members);
            }
        }
    }
    [Fact]
    public void AuthoritativeStateCannotBeEncodedAsWireRoot()
    {
        Assert.ThrowsAny<Exception>(() => WireJson.Serialize(M2.BaseGameSession.Create(Scenario, 3, 97).GetAuthoritativeStateForTesting()));
        Assert.ThrowsAny<Exception>(() => WireJson.Deserialize<M2.GameState>("{}"));
    }
    [Theory]
    [InlineData("base")][InlineData("seafarers")][InlineData("cities-knights")][InlineData("combined")]
    public void AiOpponentHandsCannotEnterNetworkSnapshotsOrBeInjectedByClients(string rules)
    {
        dynamic game = rules switch
        {
            "base" => (object)M2.BaseGameSession.Create(Scenario, 4, 97),
            "seafarers" => M3.SeafarersGameSession.Create("heading-for-new-shores", 4, 97),
            "cities-knights" => M4.CitiesKnightsGameSession.Create(Scenario, 4, 97),
            _ => M5.CombinedGameSession.Create("heading-for-new-shores", 4, 97)
        };
        object aiView = game.GetAiPlayerView("P1");
        Assert.Equal(3, ((OpponentHandView[])((dynamic)aiView).OpponentHands).Length);
        string wire = WireJson.SerializeObject(aiView);
        Assert.DoesNotContain("OpponentHands", wire);
        Assert.Equal(WireJson.SerializeObject((object)game.GetPlayerView("P1")), wire);
        dynamic restored = WireJson.Deserialize(wire, aiView.GetType());
        Assert.Empty((OpponentHandView[])restored.OpponentHands);
        Assert.ThrowsAny<Exception>(() => WireJson.Deserialize("{\"OpponentHands\":[]}", aiView.GetType()));
    }
    [Theory]
    [InlineData("cities")][InlineData("combined")]
    public void PublicBattleStrengthSurvivesTheClientServerWire(string rules)
    {
        dynamic game = rules == "cities"
            ? (object)M4.CitiesKnightsGameSession.Create(Scenario, 4, 53)
            : M5.CombinedGameSession.Create("heading-for-new-shores", 4, 53);
        for (int n = 0; n < 16; n++)
        {
            dynamic view = game.GetPlayerView("P1");
            bool settlement = view.Phase.ToString() == "SetupSettlement";
            string kind = settlement ? "SetupSettlement" : "SetupRoad";
            dynamic seat = game.GetPlayerView((string)view.ActivePlayerId);
            dynamic command = rules == "cities" ? (object)new M4.Command { Kind = Enum.Parse<M4.CommandKind>(kind) }
                : new M5.Command { Kind = Enum.Parse<M5.CommandKind>(kind) };
            command.Id = "公开兵力协议-" + n; command.PlayerId = seat.PlayerId;
            command.TargetId = settlement ? seat.LegalVertexIds[0] : seat.LegalEdgeIds[0];
            Assert.True((bool)game.Execute(command).Success);
        }
        object original = game.GetPlayerView("P1");
        dynamic restored = WireJson.Deserialize(WireJson.SerializeObject(original), original.GetType());
        Assert.Equal(4, (int)restored.BarbarianStrength);
        Assert.Equal(0, (int)restored.DefenderStrength);
        game.AssertInvariants();
    }
    [Theory]
    [InlineData("{\"Operation\":\"List\",\"Authority\":\"secret\"}")]
    [InlineData("{\"Operation\":\"List\",\"Operation\":\"Command\"}")]
    [InlineData("{\"$type\":\"Catan.Core.M2.GameState\"}")]
    public void UnknownDuplicateAndTypeInjectionFieldsAreRejected(string input) => Assert.ThrowsAny<Exception>(() => WireJson.Deserialize<NetworkRequest>(input));
    [Fact]
    public void UnknownCommandEnumIsRejected() => Assert.ThrowsAny<Exception>(() => WireJson.Deserialize<M2.Command>("{\"Kind\":999999}"));
    [Fact]
    public void UnicodePasswordsAndNamesRetainExactContent()
    {
        var input = new CreateRoomPayload { DisplayName = "张飞🀄", Password = "a\"\\\n密码", Settings = new RoomSettings { Name = "三国好友桌" } };
        var output = WireJson.Deserialize<CreateRoomPayload>(WireJson.Serialize(input));
        Assert.Equal(input.DisplayName, output.DisplayName); Assert.Equal(input.Password, output.Password); Assert.Equal(input.Settings.Name, output.Settings.Name);
    }
    [Fact]
    public void HistoryInternalSettersAndPrivateIndicatorsRoundTrip()
    {
        var game = M2.BaseGameSession.Create(Scenario, 3, 97);
        var view = game.GetPlayerView("P1");
        Assert.True(game.Execute(new M2.Command { Id = "wire-audit", PlayerId = "P1", Kind = M2.CommandKind.SetupSettlement, TargetId = view.LegalVertexIds[0] }).Success);
        var history = game.GetHistory("P1");
        var encoded = WireJson.Serialize(history);
        Assert.Equal(encoded, WireJson.Serialize(WireJson.Deserialize<GameHistoryEntry[]>(encoded)));
    }
}
