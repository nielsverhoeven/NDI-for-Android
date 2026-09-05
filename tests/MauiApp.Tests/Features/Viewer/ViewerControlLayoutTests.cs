using NdiForAndroid.Features.Viewer;
using Xunit;

namespace NdiForAndroid.Tests.Features.Viewer;

public class ViewerControlLayoutTests
{
    [Theory]
    [InlineData(640, 470)]
    [InlineData(800, 600)]
    [InlineData(1000, 470)]
    [InlineData(640, 800)]
    public void Choose_WidthAndHeightAtOrAboveThresholds_ReturnsDeck(double widthDp, double heightDp)
    {
        var result = ViewerControlLayout.Choose(widthDp, heightDp);

        Assert.Equal(ViewerControlLayoutKind.Deck, result);
    }

    [Theory]
    [InlineData(639, 470)]
    [InlineData(640, 469)]
    [InlineData(360, 800)]
    [InlineData(800, 360)]
    [InlineData(0, 0)]
    public void Choose_WidthOrHeightBelowThresholds_ReturnsSheet(double widthDp, double heightDp)
    {
        var result = ViewerControlLayout.Choose(widthDp, heightDp);

        Assert.Equal(ViewerControlLayoutKind.Sheet, result);
    }
}
