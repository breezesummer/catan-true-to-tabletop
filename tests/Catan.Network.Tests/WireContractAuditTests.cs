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
