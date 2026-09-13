namespace NdiForAndroid.Services;

/// <summary>Radio band of the current Wi-Fi association.</summary>
public enum WifiBand
{
    Unknown,
    TwoPointFourGhz,
    FiveGhz,
    SixGhz,
}

/// <summary>IEEE 802.11 generation as reported by the platform (Android 11+; Unknown below that).</summary>
public enum WifiStandard
{
    Unknown,
    Legacy,
    N,
    Ac,
    Ax,
    Ad,
    Be,
}

/// <summary>
/// One read-only sample of the device's current network link. Plain Core record — no Android types
/// cross this boundary, exactly as no NDI type crosses the bridge boundary. Deliberately carries no
/// SSID and no BSSID: both are redacted by the platform for callers without location permission, and
/// this service never requests one.
/// </summary>
public record NetworkLinkSnapshot(
    bool IsWifi,
    WifiBand Band,
    int Rssi,
    int LinkSpeedMbps,
    WifiStandard Standard)
{
    /// <summary>Sentinel for "the platform did not report an RSSI".</summary>
    public const int UnknownRssi = int.MinValue;

    /// <summary>Sentinel for "the platform did not report a link speed".</summary>
    public const int UnknownLinkSpeed = -1;

    /// <summary>No usable link information: non-Android host, no Wi-Fi association, or a failed read.</summary>
    public static readonly NetworkLinkSnapshot Unknown =
        new(false, WifiBand.Unknown, UnknownRssi, UnknownLinkSpeed, WifiStandard.Unknown);
}

/// <summary>
/// Reads the device's current network link. Diagnostic only: nothing in the app changes behaviour
/// because of it — the viewer hints, it never adjusts quality itself.
/// </summary>
public interface INetworkLinkService
{
    /// <summary>
    /// Samples the link. Cheap but not free — one binder round-trip on Android — so call it only
    /// from a background/thread-pool context such as the viewer's 1 s stats watchdog. Never from the
    /// UI thread, and never from an NDI pump thread. Never throws: returns
    /// <see cref="NetworkLinkSnapshot.Unknown"/> when nothing can be read.
    /// </summary>
    NetworkLinkSnapshot GetSnapshot();
}
