using NdiForAndroid.Features.Viewer;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Services;
using Xunit;

namespace NdiForAndroid.Tests.Features.Viewer;

public class ConnectionHintLinkTests
{
    [Theory]
    [InlineData(true, WifiBand.TwoPointFourGhz, -40, 200, true)]
    [InlineData(true, WifiBand.FiveGhz, -45, 400, false)]
    [InlineData(true, WifiBand.FiveGhz, -78, 400, true)]
    [InlineData(true, WifiBand.FiveGhz, -55, 24, true)]
    [InlineData(false, WifiBand.TwoPointFourGhz, -90, 1, false)]
    public void IsWeakLink_Theory(bool isWifi, WifiBand band, int rssi, int linkSpeedMbps, bool expected)
    {
        var link = new NetworkLinkSnapshot(isWifi, band, rssi, linkSpeedMbps, WifiStandard.Unknown);

        Assert.Equal(expected, ConnectionHintPolicy.IsWeakLink(link));
    }

    [Fact]
    public void IsWeakLink_UnknownSnapshot_IsNeverWeak()
    {
        Assert.False(ConnectionHintPolicy.IsWeakLink(NetworkLinkSnapshot.Unknown));
    }

    [Theory]
    [InlineData(QualityProfile.Balanced, WifiBand.TwoPointFourGhz, -40, 200, "2.4 GHz / weak signal — switch to Smooth")]
    [InlineData(QualityProfile.Smooth, WifiBand.TwoPointFourGhz, -40, 200, "2.4 GHz / weak signal")]
    [InlineData(QualityProfile.Balanced, WifiBand.FiveGhz, -45, 400, "Connection weak — try Smooth")]
    [InlineData(QualityProfile.Balanced, WifiBand.FiveGhz, -78, 400, "5 GHz / weak signal — switch to Smooth")]
    public void HintText_WhileActive_Theory(QualityProfile profile, WifiBand band, int rssi, int linkSpeedMbps, string expected)
    {
        var link = new NetworkLinkSnapshot(true, band, rssi, linkSpeedMbps, WifiStandard.Unknown);

        Assert.Equal(expected, ConnectionHintPolicy.HintText(true, profile, link));
    }

    [Fact]
    public void HintText_NotActive_IsNullWhateverTheLink()
    {
        var link = new NetworkLinkSnapshot(true, WifiBand.TwoPointFourGhz, -90, 1, WifiStandard.Unknown);

        Assert.Null(ConnectionHintPolicy.HintText(false, QualityProfile.Balanced, link));
    }
}
