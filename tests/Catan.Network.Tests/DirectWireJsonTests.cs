using Catan.Core;
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
}
