using Xunit;

namespace Catan.Rules.Tests;

public sealed class TabletopHandCounterTests
{
    [Theory]
    [InlineData(5, 7, "5/7", false)]
    [InlineData(7, 7, "7/7", false)]
    [InlineData(8, 7, "8/7", true)]
    [InlineData(9, 9, "9/9", false)]
    [InlineData(10, 9, "10/9", true)]
    [InlineData(4, 4, "4/4", false)]
    [InlineData(5, 4, "5/4", true)]
    [InlineData(5, 0, "5/∞", false)]
    [InlineData(25, 0, "25/∞", false)]
    public void PublicCountUsesItsRuleThreshold(int count, int limit, string caption, bool red)
    {
        var counter = new TabletopHandCounter(count, limit);
        Assert.Equal(caption, counter.Caption);
        Assert.Equal(red, counter.OverLimit);
    }
}
