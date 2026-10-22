using Xunit;

namespace Catan.Rules.Tests;

public sealed class TradeCardLayoutTests
{
    [Fact]
    public void FewCardsAreLargerThanCrowdedCards()
    {
        var one=OperationTradeCardLayout.Arrange(1,386,195);
        var three=OperationTradeCardLayout.Arrange(3,386,195);
        var five=OperationTradeCardLayout.Arrange(5,386,195);
        var eight=OperationTradeCardLayout.Arrange(8,386,195);
        Assert.True(one[0].Width>=100);
        Assert.True(three[0].Width>five[0].Width&&five[0].Width>eight[0].Width);
    }

    [Theory]
    [InlineData(386,195)]
    [InlineData(386,200)]
    [InlineData(386,218)]
    [InlineData(270,160)]
    public void EveryCardAndItsCountFitsWithoutOverlap(float width,float height)
    {
        for(int count=1;count<=8;count++)
        {
            var slots=OperationTradeCardLayout.Arrange(count,width,height);Assert.Equal(count,slots.Length);
            foreach(var slot in slots)
            {
                Assert.True(slot.Width>0&&slot.Height>0);
                Assert.InRange(slot.X,-.001f,width-slot.Width+.001f);
                Assert.InRange(slot.Y,-.001f,height-slot.Height-OperationTradeCardSlot.CaptionHeight+.001f);
                Assert.InRange(Math.Abs(slot.Width/slot.Height-OperationTradeCardLayout.Aspect),0,.0001f);
            }
            for(int a=0;a<count;a++)for(int b=a+1;b<count;b++)
            {
                var first=slots[a];var second=slots[b];
                Assert.True(first.X+first.Width<=second.X+.001f||second.X+second.Width<=first.X+.001f||
                    first.CaptionY+OperationTradeCardSlot.CaptionHeight<=second.Y+.001f||second.CaptionY+OperationTradeCardSlot.CaptionHeight<=first.Y+.001f);
            }
            foreach(var row in slots.GroupBy(s=>s.Y))
                Assert.InRange(Math.Abs((row.Min(s=>s.X)+row.Max(s=>s.X+s.Width))/2-width/2),0,.001f);
        }
    }

    [Fact]
    public void EmptyTradeHasNoCards()
        =>Assert.Empty(OperationTradeCardLayout.Arrange(0,386,195));
}
