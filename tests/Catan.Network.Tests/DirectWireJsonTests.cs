using Catan.Core;
using Catan.Core.ThreeKingdoms;
using Catan.Network.Contracts;
using Xunit;

namespace Catan.Network.Tests;

public sealed class DirectWireJsonTests
{
    [Theory]
    [InlineData("{\"Revision\":01}")][InlineData("{\"Revision\":+1}")]
    [InlineData("{\"Revision\":1.}")][InlineData("{\"Revision\":1e}")]
    [InlineData("{\"Revision\":null}")][InlineData("{\"Revision\":\"1\"}")]
    [InlineData("{\"Success\":1}")][InlineData("{\"Message\":false}")]
    [InlineData("{\"Message\":\"\\uXX00\"}")][InlineData("{\"Message\":\"\\x01\"}")]
    [InlineData("{\"Message\":\"a\nb\"}")][InlineData("{\"Rooms\":[{},]}")]
    [InlineData("{\"Room\":{\"Seats\":[],}}")][InlineData("{} {}")]
    [InlineData("{\"R\\u0065vision\":1,\"Revision\":2}")]
    [InlineData("{\"Room\":{\"Seats\":[],\"$type\":\"authority\"}}")]
    public void MalformedOrInjectedValuesAreRejected(string json)=>Assert.ThrowsAny<Exception>(()=>WireJson.Deserialize<NetworkResponse>(json));

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Message\":null,\"Room\":null,\"Revision\":-42}")]
    [InlineData("{\"Message\":\"汉字\\n\\t\\b\\f\\r\\\\\\/\\\"\\uD83C\\uDC04\",\"Success\":true}")]
    public void DefaultsNullsAndEscapesMatchLegacy(string json)
    {
        Assert.Equal(WireJson.Serialize(WireJson.DeserializeLegacy<NetworkResponse>(json)),WireJson.Serialize(WireJson.Deserialize<NetworkResponse>(json)));
    }
    [Fact]
    public void LengthAndArrayBudgetsAreEnforced()
    {
        Assert.ThrowsAny<Exception>(()=>WireJson.Deserialize<NetworkResponse>(new string(' ',NetworkProtocol.MaximumResponseBytes+1)));
        Assert.ThrowsAny<Exception>(()=>WireJson.Deserialize<RoomSummary[]>("["+string.Join(",",Enumerable.Repeat("null",100001))+"]"));
    }
    [Fact]
    public void MutableSkillResultAndInternalSettersMatchLegacy()
    {
        string json="[{\"Sequence\":9,\"SkillEventId\":\"skill-1\",\"SkillId\":\"test\",\"Changes\":[{\"GoodIndex\":-1,\"Amount\":2,\"IsPrivate\":true}]}]";
        Assert.Equal(WireJson.Serialize(WireJson.DeserializeLegacy<GameHistoryEntry[]>(json)),WireJson.Serialize(WireJson.Deserialize<GameHistoryEntry[]>(json)));
    }
    [Fact]
    public void IntegratedSkillProjectionKeepsB5AndDirectInteractionFieldsAcrossBothDecoders()
    {
        var view=new ThreeKingdomsView {PackageVersion=GeneralCatalog.B5DirectPackageVersion,
            PublicPoolGeneralIds=new[]{"tk-g54"},RetiredGeneralIds=new[]{"tk-g01"},
            SkillRuntimeStatus=new[]{new B45SkillRuntimeStatus {SkillId="tk-g16-s1",HasLegalAction=true,Summary="可发动"}},
            SkillActions=new[]{new B2SkillAction {Command=new B2SkillCommand {SkillId="tk-g17-s1",Option="civil-qixi-tech"},SelectionCost=new[]{0,3,0,0,0,0,0,0},SelectionBoardIds=new[]{"E003"}}}};
        using var buffer=new MemoryStream();new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(ThreeKingdomsView)).WriteObject(buffer,view);
        string json="{\"ThreeKingdoms\":"+System.Text.Encoding.UTF8.GetString(buffer.ToArray())+"}";
        var direct=WireJson.Deserialize<Catan.Core.M4.PlayerView>(json);var legacy=WireJson.DeserializeLegacy<Catan.Core.M4.PlayerView>(json);
        Assert.Equal(WireJson.Serialize(legacy),WireJson.Serialize(direct));Assert.Equal(view.SkillActions[0].SelectionCost,direct.ThreeKingdoms.SkillActions[0].SelectionCost);
        Assert.Equal(view.PublicPoolGeneralIds,direct.ThreeKingdoms.PublicPoolGeneralIds);Assert.True(Assert.Single(direct.ThreeKingdoms.SkillRuntimeStatus).HasLegalAction);
    }

}
