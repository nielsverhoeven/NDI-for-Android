namespace NdiForAndroid.Services;

/// <summary>
/// Keeps the network radio in its lowest-latency mode while a live NDI stream is being received.
/// On Android this is a <c>WifiManager.WifiLock</c> in <c>WIFI_MODE_FULL_LOW_LATENCY</c> mode, which
/// disables Wi-Fi power save: without it the radio dozes between beacons and the access point
/// buffers packets for it, delivering video/audio in 100 ms+ bursts. On non-Android targets a no-op.
/// Acquire/Release are idempotent and safe to call from any thread.
/// </summary>
public interface ILowLatencyNetworkLock
{
    /// <summary>Requests low-latency networking. Never throws; degrades to a no-op when unavailable.</summary>
    void Acquire();

    /// <summary>Returns the radio to its normal power policy. Safe to call when not held.</summary>
    void Release();
}
