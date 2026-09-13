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
/// API levels: the API 31+ capability read (ConnectivityManager.GetNetworkCapabilities(...).TransportInfo)
/// is the preferred, non-deprecated probe. WifiManager.ConnectionInfo — deprecated from API 31 — is
/// the fallback on every API level: it runs whenever the capability probe is unavailable or returns
/// nothing (Wi-Fi not the active network, no active network at all, or below API 31), so the CA1422
/// suppression applies unconditionally rather than only on a legacy branch. WifiInfo.WifiStandard
/// exists from API 30; below that the standard reads Unknown.
/// </para>
/// </summary>
public sealed class AndroidNetworkLinkService : INetworkLinkService
{
    public NetworkLinkSnapshot GetSnapshot()
    {
        try
        {
            // using: WifiInfo is a JNI peer and this runs once a second for the life of a viewing
            // session. Nothing outlives this method, so release it here rather than leaving ~1 GREF
            // per second for the GC.
            using var info = ReadWifiInfo();

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

    /// <summary>
    /// The current Wi-Fi <em>association</em> — deliberately not "the network carrying the default
    /// route". Those differ exactly when this feature matters most: with a VPN up the active network
    /// is the tunnel, and at a closed venue AP (the #415 case) Wi-Fi has no internet so mobile data
    /// carries the default route. Both used to yield NetworkLinkSnapshot.Unknown, with no indication
    /// that the whole feature had gone inert, and the two API branches disagreed with each other on
    /// the same radio.
    /// </summary>
    private static WifiInfo? ReadWifiInfo()
    {
        var context = global::Android.App.Application.Context;

        if (OperatingSystem.IsAndroidVersionAtLeast(31)
            && context.GetSystemService(Context.ConnectivityService) is ConnectivityManager cm)
        {
            // Preferred probe: the supported, non-deprecated API, and correct in the common case
            // where Wi-Fi does carry the default route. using: two more JNI peers per second.
            using var network = cm.ActiveNetwork;
            if (network is not null)
            {
                using var caps = cm.GetNetworkCapabilities(network);
                if (caps?.TransportInfo is WifiInfo fromActiveNetwork)
                    return fromActiveNetwork;
            }
        }

        return ReadLegacyConnectionInfo(context);
    }

    /// <summary>
    /// WifiManager.ConnectionInfo: the primary Wi-Fi association, independent of routing. Deprecated
    /// from API 31 but neither removed nor throwing, and the only synchronous API that answers "what
    /// radio am I on" when Wi-Fi is not the active network — hence the suppression rather than a
    /// version branch. Only the location-sensitive fields (SSID, BSSID, network id) are redacted for
    /// callers without ACCESS_FINE_LOCATION; RSSI, PHY link speed and frequency — the three fields
    /// this service reads — are not. Confirm that once on the device (#415 measurement procedure
    /// step 3): if RSSI or link speed come back as -127 / -1 on API 31+, report it. Do not add a
    /// location permission.
    /// </summary>
    private static WifiInfo? ReadLegacyConnectionInfo(Context context)
    {
#pragma warning disable CA1422 // Deliberate on all API levels — see the summary above.
        return context.GetSystemService(Context.WifiService) is WifiManager wifi
            ? wifi.ConnectionInfo
            : null;
#pragma warning restore CA1422
    }

    /// <summary>Channel centre frequency (MHz) to band, per the IEEE 802.11 allocations. The 5 GHz
    /// arm runs to the start of the 6 GHz allocation rather than to 5900, so no frequency in
    /// 4900-7125 can fall through to Unknown.</summary>
    private static WifiBand MapBand(int frequencyMhz) => frequencyMhz switch
    {
        >= 2400 and <= 2500 => WifiBand.TwoPointFourGhz,
        >= 4900 and < 5925 => WifiBand.FiveGhz,
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
