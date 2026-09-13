using NdiForAndroid.Features.Viewer;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Services;
using Xunit;

namespace NdiForAndroid.Tests.Features.Viewer;

public class ConnectionHintLinkTests
{
    [Theory]
    // The band is NOT an arm of IsWeakLink (#415 round 2): a strong 2.4 GHz radio is not a weak one.
    [InlineData(true, WifiBand.TwoPointFourGhz, -40, 200, false)]
    // The church link that produced #415 — weak on both measured arms, so still classified weak.
    [InlineData(true, WifiBand.TwoPointFourGhz, -78, 6, true)]
    [InlineData(true, WifiBand.FiveGhz, -45, 400, false)]
    [InlineData(true, WifiBand.FiveGhz, -78, 400, true)]
    [InlineData(true, WifiBand.FiveGhz, -55, 24, true)]
    [InlineData(false, WifiBand.TwoPointFourGhz, -90, 1, false)]
    // Sentinels: "the platform reported no RSSI / no link speed" must never read as weak. These
    // guards carry real weight now that the band arm no longer short-circuits ahead of them.
    [InlineData(true, WifiBand.FiveGhz, NetworkLinkSnapshot.UnknownRssi, 400, false)]
    [InlineData(true, WifiBand.FiveGhz, -45, NetworkLinkSnapshot.UnknownLinkSpeed, false)]
    [InlineData(true, WifiBand.Unknown, NetworkLinkSnapshot.UnknownRssi, NetworkLinkSnapshot.UnknownLinkSpeed, false)]
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
    // Tier 1 — the radio measures weak and frames were being lost: name the band and the signal.
    [InlineData(QualityProfile.Balanced, WifiBand.TwoPointFourGhz, -78, 6, true, "2.4 GHz / weak signal — switch to Smooth")]
    [InlineData(QualityProfile.Smooth, WifiBand.TwoPointFourGhz, -78, 6, true, "2.4 GHz / weak signal")]
    [InlineData(QualityProfile.Balanced, WifiBand.FiveGhz, -78, 400, true, "5 GHz / weak signal — switch to Smooth")]
    // Tier 2 — frames lost on a 2.4 GHz radio that measures fine: state the band, claim no cause.
    [InlineData(QualityProfile.Balanced, WifiBand.TwoPointFourGhz, -40, 200, true, "Connection weak on 2.4 GHz — try Smooth")]
    [InlineData(QualityProfile.Smooth, WifiBand.TwoPointFourGhz, -40, 200, true, "Connection weak on 2.4 GHz")]
    // Tier 3 — a healthy 5 GHz radio, or no drop evidence at all: the generic copy, which blames
    // nothing. The last two rows are the regression test for "an idle sender on a bad-looking radio
    // must not be told the radio is the problem".
    [InlineData(QualityProfile.Balanced, WifiBand.FiveGhz, -45, 400, true, "Connection weak — try Smooth")]
    [InlineData(QualityProfile.Balanced, WifiBand.TwoPointFourGhz, -78, 6, false, "Connection weak — try Smooth")]
    [InlineData(QualityProfile.Smooth, WifiBand.TwoPointFourGhz, -78, 6, false, "Connection weak")]
    public void HintText_WhileActive_Theory(
        QualityProfile profile,
        WifiBand band,
        int rssi,
        int linkSpeedMbps,
        bool sawDropEvidence,
        string expected)
    {
        var link = new NetworkLinkSnapshot(true, band, rssi, linkSpeedMbps, WifiStandard.Unknown);

        Assert.Equal(expected, ConnectionHintPolicy.HintText(true, profile, link, sawDropEvidence));
    }

    [Fact]
    public void HintText_UnknownLink_WithDropEvidence_IsTheGenericCopy()
    {
        Assert.Equal(
            "Connection weak — try Smooth",
            ConnectionHintPolicy.HintText(true, QualityProfile.Balanced, NetworkLinkSnapshot.Unknown, sawDropEvidence: true));
    }

    [Fact]
    public void HintText_NotActive_IsNullWhateverTheLink()
    {
        var link = new NetworkLinkSnapshot(true, WifiBand.TwoPointFourGhz, -90, 1, WifiStandard.Unknown);

        Assert.Null(ConnectionHintPolicy.HintText(false, QualityProfile.Balanced, link, sawDropEvidence: true));
    }
}
