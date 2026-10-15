using Xunit;

namespace Catan.Rules.Tests;

// 固定的屏幕/安全区预期独立于 Unity；手机显示和点击必须使用同一变换。
public sealed class AndroidTouchLayoutTests
{
    [Theory]
    [InlineData(1440, 900, 0, 0, 1440, 900)]
    [InlineData(1920, 1080, 0, 0, 1920, 1080)]
    [InlineData(2400, 1080, 0, 0, 2400, 1080)]
    [InlineData(2048, 1536, 0, 0, 2048, 1536)]
    [InlineData(1280, 720, 0, 0, 1280, 720)]
    public void LandscapeViewportUsesTheFullAvailablePhoneOrTabletDisplay(
        float screenWidth, float screenHeight, float left, float top, float width, float height)
    {
        var viewport = MobileViewport.Fit(screenWidth, screenHeight, 0, 0, screenWidth, screenHeight);
        Near(left, viewport.Left);
        Near(top, viewport.Top);
        Near(width, viewport.Width);
        Near(height, viewport.Height);
    }

    [Fact]
    public void AndroidBottomOriginSafeAreaIsConvertedToTopOriginBeforeFitting()
    {
        // 屏幕顶部留 40 px，底部留 24 px，左右留 80 / 60 px。
        var viewport = MobileViewport.Fit(1920, 1080, 80, 24, 1780, 1016);
        Near(80, viewport.Left);
        Near(40, viewport.Top);
        Near(1780, viewport.Width);
        Near(1016, viewport.Height);
        Assert.True(viewport.Left >= 80);
        Assert.True(viewport.Left + viewport.Width <= 1860);
        Assert.True(viewport.Top + viewport.Height <= 1056);
    }

    [Theory]
    [InlineData(2400, 1080, 120, 36, 2160, 1008)]
    [InlineData(1920, 1080, 80, 24, 1780, 1016)]
    [InlineData(2048, 1536, 0, 24, 2048, 1512)]
    [InlineData(1280, 720, 0, 0, 1280, 720)]
    public void DrawingAndTouchHitTestingRoundTripAtCornersCenterAndHudPositions(
        float screenWidth, float screenHeight, float safeLeft, float safeBottom, float safeWidth, float safeHeight)
    {
        var viewport = MobileViewport.Fit(screenWidth, screenHeight, safeLeft, safeBottom, safeWidth, safeHeight);
        var points = new[] { (0f, 0f), (1440f, 900f), (720f, 450f), (24f, 16f), (1330f, 825f), (770f, 745f) };
        foreach (var (x, y) in points)
        {
            Near(x, viewport.ToLogicalX(viewport.ToScreenX(x)));
            Near(y, viewport.ToLogicalY(viewport.ToScreenY(y)));
        }
        Near(0, viewport.ToLogicalX(viewport.Left));
        Near(0, viewport.ToLogicalY(viewport.Top));
        Near(1440, viewport.ToLogicalX(viewport.Left + viewport.Width));
        Near(900, viewport.ToLogicalY(viewport.Top + viewport.Height));
    }

    [Fact]
    public void TouchOutsideSafeAreaRemainsOutsideBoardInsteadOfClampingIntoAnAction()
    {
        var viewport = MobileViewport.Fit(2400, 1080, 120, 24, 2160, 1016);
        Assert.True(viewport.ToLogicalX(100) < 0);
        Assert.True(viewport.ToLogicalX(2300) > 1440);
        var tablet = MobileViewport.Fit(2048, 1536, 0, 24, 2048, 1472);
        Assert.True(tablet.ToLogicalY(20) < 0);
        Assert.True(tablet.ToLogicalY(1516) > 900);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(18, 0, true)]
    [InlineData(-18, 0, true)]
    [InlineData(10, 10, true)]
    [InlineData(18.01, 0, false)]
    [InlineData(0, -18.01, false)]
    [InlineData(13, 13, false)]
    public void TapToleranceUsesLogicalDistance(float dx, float dy, bool expected)
        => Assert.Equal(expected, MobileGestureMath.IsTap(dx, dy));

    [Theory]
    [InlineData(200, 220, -.032)]
    [InlineData(200, 180, .032)]
    [InlineData(200, 200, 0)]
    [InlineData(200, 600, -.08)]
    [InlineData(200, 20, .08)]
    [InlineData(0, 100, 0)]
    [InlineData(100, 0, 0)]
    [InlineData(-1, 100, 0)]
    public void PinchHasCorrectDirectionAndBoundsWithoutInvalidDistanceJumps(float previous, float current, float expected)
        => Near(expected, MobileGestureMath.PinchZoomDelta(previous, current));

    [Theory]
    [InlineData(float.NaN, 100)]
    [InlineData(100, float.NaN)]
    [InlineData(float.PositiveInfinity, 100)]
    [InlineData(100, float.PositiveInfinity)]
    public void InvalidTouchSamplesCannotCreateAnUnboundedCameraDelta(float previous, float current)
        => Assert.Equal(0, MobileGestureMath.PinchZoomDelta(previous, current));

    static void Near(float expected, float actual) => Assert.InRange(actual, expected - .002f, expected + .002f);
}
