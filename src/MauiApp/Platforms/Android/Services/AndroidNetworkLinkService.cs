using Android.Content;
using Android.Net;
using Android.Net.Wifi;
using NdiForAndroid.Services;
using WifiBand = NdiForAndroid.Services.WifiBand;
using WifiStandard = NdiForAndroid.Services.WifiStandard;

namespace NdiForAndroid.Platforms.Android.Services;

/// <summary>
/// Reads RSSI / PHY link speed / band from the current Wi-Fi association.
/// <para>
/// Permissions: ACCESS_WIFI_STATE and ACCESS_NETWORK_STATE only — both already declared in the
/// Android manifest. No location permission is requested or needed: only SSID/BSSID are
/// location-gated from Android 10 on, and this service never reads them.
/// </para>
/// <para>
/// API levels: WifiManager.ConnectionInfo is deprecated from API 31, where
/// ConnectivityManager.GetNetworkCapabilities(...).TransportInfo is the supported replacement. Both
/// branches ship, split on OperatingSystem.IsAndroidVersionAtLeast so the CA1422 analyser is
/// satisfied. WifiInfo.WifiStandard exists from API 30; below that the standard reads Unknown.
/// </para>
/// </summary>
public sealed class AndroidNetworkLinkService : INetworkLinkService
{
    public NetworkLinkSnapshot GetSnapshot()
    {
        try
        {
            var info = ReadWifiInfo();

            // Frequency is the associated/not-associated test that survives redaction: NetworkId and
            // BSSID both read as "unknown" without location permission on API 31+.
            if (info is null || info.Frequency <= 0)
                return NetworkLinkSnapshot.Unknown;

            return new NetworkLinkSnapshot(
                IsWifi: true,
                Band: MapBand(info.Frequency),
                Rssi: info.Rssi > -127 ? info.Rssi : NetworkLinkSnapshot.UnknownRssi,
                LinkSpeedMbps: info.LinkSpeed > 0 ? info.LinkSpeed : NetworkLinkSnapshot.UnknownLinkSpeed,
                Standard: MapStandard(info));
        }
        catch
        {
            // Diagnostics must never break playback.
            return NetworkLinkSnapshot.Unknown;
        }
    }

    private static WifiInfo? ReadWifiInfo()
    {
        var context = global::Android.App.Application.Context;

        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            if (context.GetSystemService(Context.ConnectivityService) is not ConnectivityManager cm)
                return null;

            var network = cm.ActiveNetwork;
            if (network is null)
                return null;

            // Null whenever Wi-Fi is not the active transport (cellular, ethernet, VPN).
            return cm.GetNetworkCapabilities(network)?.TransportInfo as WifiInfo;
        }

#pragma warning disable CA1422 // Deprecated from API 31 only; this branch cannot execute there.
        return context.GetSystemService(Context.WifiService) is WifiManager wifi
            ? wifi.ConnectionInfo
            : null;
#pragma warning restore CA1422
    }

    /// <summary>Channel centre frequency (MHz) to band, per the IEEE 802.11 allocations.</summary>
    private static WifiBand MapBand(int frequencyMhz) => frequencyMhz switch
    {
        >= 2400 and <= 2500 => WifiBand.TwoPointFourGhz,
        >= 4900 and <= 5900 => WifiBand.FiveGhz,
        >= 5925 and <= 7125 => WifiBand.SixGhz,
        _ => WifiBand.Unknown,
    };

    private static WifiStandard MapStandard(WifiInfo info)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(30))
            return WifiStandard.Unknown; // WifiInfo.WifiStandard is API 30+.

        // Raw ScanResult.WIFI_STANDARD_* values — WifiInfo.WifiStandard surfaces as int in this
        // binding, so the cast is an identity conversion.
        return (int)info.WifiStandard switch
        {
            1 => WifiStandard.Legacy,
            4 => WifiStandard.N,
            5 => WifiStandard.Ac,
            6 => WifiStandard.Ax,
            7 => WifiStandard.Ad,
            8 => WifiStandard.Be,
            _ => WifiStandard.Unknown,
        };
    }
}
