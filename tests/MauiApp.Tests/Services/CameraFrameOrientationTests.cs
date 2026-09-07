using NdiForAndroid.Services;
using Xunit;

namespace NdiForAndroid.Tests.Services;

public class CameraFrameOrientationTests
{
    [Theory]
    // Rear camera, sensor 90 (typical phone): portrait 90, landscape-left 0, upside-down 270, landscape-right 180.
    [InlineData(90, 0, false, 90)]
    [InlineData(90, 90, false, 0)]
    [InlineData(90, 180, false, 270)]
    [InlineData(90, 270, false, 180)]
    // Front camera, sensor 270 (typical phone).
    [InlineData(270, 0, true, 270)]
    [InlineData(270, 90, true, 0)]
    [InlineData(270, 180, true, 90)]
    [InlineData(270, 270, true, 180)]
    // Landscape-natural tablet, rear sensor 0.
    [InlineData(0, 0, false, 0)]
    [InlineData(0, 90, false, 270)]
    [InlineData(0, 270, false, 90)]
    public void ComputeRotationDegrees_MatchesCamera2Formula(int sensor, int display, bool front, int expected) =>
        Assert.Equal(expected, CameraFrameOrientation.ComputeRotationDegrees(sensor, display, front));

    [Theory]
    [InlineData(-90, 0, false, 270)]
    [InlineData(450, 0, false, 90)]
    [InlineData(90, 89, false, 0)]
    public void ComputeRotationDegrees_NormalizesInputs(int sensor, int display, bool front, int expected) =>
        Assert.Equal(expected, CameraFrameOrientation.ComputeRotationDegrees(sensor, display, front));
}
