using Xunit;

namespace Catan.Rules.Tests;

public sealed class TabletopCameraTests
{
    [Theory]
    [InlineData(false,32,58,68)]
    [InlineData(true,42,68,78)]
    public void NearDefaultAndFarViewsKeepTheirIntendedPitch(bool overhead,float close,float normal,float far)
    {
        Assert.Equal(close,TabletopCameraModel.Angle(.45f,overhead));
        Assert.Equal(normal,TabletopCameraModel.Angle(1,overhead));
        Assert.Equal(far,TabletopCameraModel.Angle(1.65f,overhead));
        Assert.Equal(close,TabletopCameraModel.Angle(-100,overhead));
        Assert.Equal(far,TabletopCameraModel.Angle(100,overhead));
        Assert.Equal(normal,TabletopCameraModel.Angle(float.NaN,overhead));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ZoomingInLowersPitchContinuouslyAndAlwaysRetainsADownwardView(bool overhead)
    {
        float previous=TabletopCameraModel.Angle(.45f,overhead);
        for(int step=1;step<=1200;step++)
        {
            float zoom=.45f+step*.001f;
            float current=TabletopCameraModel.Angle(zoom,overhead);
            Assert.InRange(current,32,78);
            Assert.True(current>=previous);
            Assert.InRange(current-previous,0,.08f);
            previous=current;
        }
        Assert.InRange(TabletopCameraModel.Angle(1.0001f,overhead)-TabletopCameraModel.Angle(.9999f,overhead),0,.001f);
    }
}
