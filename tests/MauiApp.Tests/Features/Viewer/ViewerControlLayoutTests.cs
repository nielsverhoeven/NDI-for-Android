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

    [Theory]
    [InlineData(328, true)]
    [InlineData(439, true)]
    [InlineData(440, false)]
    [InlineData(696, false)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void ShouldStackCameraPresets_ReturnsExpected(double cameraControlsWidthDp, bool expected)
    {
        var result = ViewerControlLayout.ShouldStackCameraPresets(cameraControlsWidthDp);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(608, 440)]
    [InlineData(400, 320)]
    [InlineData(248, 248)]
    [InlineData(340, 312)]
    [InlineData(0, 440)]
    public void ChooseSheetExpandedHeightDp_ReturnsExpected(double sheetHostHeightDp, double expected)
    {
        var result = ViewerControlLayout.ChooseSheetExpandedHeightDp(sheetHostHeightDp);

        Assert.Equal(expected, result, 3);
    }

    [Theory]
    [InlineData(608, 320)]
    [InlineData(400, 220)]
    [InlineData(248, 136.4)]
    [InlineData(0, 320)]
    public void ChooseSheetPeekHeightDp_ReturnsExpected(double sheetHostHeightDp, double expected)
    {
        var result = ViewerControlLayout.ChooseSheetPeekHeightDp(sheetHostHeightDp);

        Assert.Equal(expected, result, 3);
    }

    [Theory]
    [InlineData(608, ViewerControlLayoutKind.Sheet, false, 240)]
    [InlineData(248, ViewerControlLayoutKind.Sheet, false, 105.6)]
    [InlineData(1168, ViewerControlLayoutKind.Deck, false, 240)]
    [InlineData(248, ViewerControlLayoutKind.Sheet, true, -1)]
    [InlineData(0, ViewerControlLayoutKind.Sheet, false, 240)]
    public void ChooseVideoHeightDp_ReturnsExpected(
        double contentHeightDp, ViewerControlLayoutKind layout, bool isFullScreen, double expected)
    {
        var result = ViewerControlLayout.ChooseVideoHeightDp(contentHeightDp, layout, isFullScreen);

        Assert.Equal(expected, result, 3);
    }

    [Theory]
    [InlineData(150)]
    [InlineData(194)]
    [InlineData(200)]
    [InlineData(220)]
    [InlineData(237)]
    [InlineData(248)]
    [InlineData(400)]
    [InlineData(608)]
    public void ChooseVideoHeightDp_SheetNeverOverlapsThePeek(double contentHeightDp)
    {
        var video = ViewerControlLayout.ChooseVideoHeightDp(contentHeightDp, ViewerControlLayoutKind.Sheet, false);
        var peek = ViewerControlLayout.ChooseSheetPeekHeightDp(contentHeightDp);

        Assert.True(video + ViewerControlLayout.VideoBorderStrokeDp + peek <= contentHeightDp + 0.001,
                    $"video {video} + stroke + peek {peek} exceeds host {contentHeightDp}");
    }
}
