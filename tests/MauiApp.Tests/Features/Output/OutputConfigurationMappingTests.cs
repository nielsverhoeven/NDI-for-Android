using NdiForAndroid.Features.Output.Repositories;
using NdiForAndroid.Services;
using Xunit;

namespace NdiForAndroid.Tests.Features.Output;

/// <summary>
/// Tests the LastInputKind string ↔ <see cref="VideoInputKind"/> mapping used by
/// OutputConfigurationRepository. The SQLite wrapper itself lives in the
/// net10.0-android app assembly and cannot be referenced from this test project,
/// so the mapping logic is hosted (and tested) in Core.
/// </summary>
public class OutputConfigurationMappingTests
{
    [Theory]
    [InlineData("Screen", VideoInputKind.Screen)]
    [InlineData("CameraFront", VideoInputKind.CameraFront)]
    [InlineData("CameraRear", VideoInputKind.CameraRear)]
    [InlineData("camerarear", VideoInputKind.CameraRear)] // case-insensitive
    public void ParseInputKind_CurrentEnumNames_ParsesExactValue(string stored, VideoInputKind expected)
    {
        Assert.Equal(expected, OutputConfigurationMapping.ParseInputKind(stored));
    }

    [Theory]
    [InlineData("DeviceScreen")]  // legacy Kotlin-era value
    [InlineData("devicescreen")]
    public void ParseInputKind_LegacyDeviceScreen_MapsToScreen(string stored)
    {
        Assert.Equal(VideoInputKind.Screen, OutputConfigurationMapping.ParseInputKind(stored));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("DiscoveredNdi")] // legacy value with no capture-input equivalent
    [InlineData("garbage")]
    [InlineData("42")]            // numeric strings must not parse to out-of-range enum values
    public void ParseInputKind_UnknownOrEmpty_DefaultsToScreen(string? stored)
    {
        Assert.Equal(VideoInputKind.Screen, OutputConfigurationMapping.ParseInputKind(stored));
    }

    [Theory]
    [InlineData(VideoInputKind.Screen)]
    [InlineData(VideoInputKind.CameraFront)]
    [InlineData(VideoInputKind.CameraRear)]
    public void ToStorageString_RoundTripsThroughParse(VideoInputKind kind)
    {
        var stored = OutputConfigurationMapping.ToStorageString(kind);
        Assert.Equal(kind, OutputConfigurationMapping.ParseInputKind(stored));
    }
}
