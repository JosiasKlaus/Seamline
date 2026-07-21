namespace Seamline.Core.Tests;

public class MonitorOrientationTests
{
    [Theory]
    [InlineData(MonitorOrientation.Landscape, 0)]
    [InlineData(MonitorOrientation.Portrait, 1)]
    [InlineData(MonitorOrientation.FlippedLandscape, 2)]
    [InlineData(MonitorOrientation.FlippedPortrait, 3)]
    public void Values_match_win32_dmdo_constants(MonitorOrientation orientation, int expected)
    {
        Assert.Equal(expected, (int)orientation);
    }
}
