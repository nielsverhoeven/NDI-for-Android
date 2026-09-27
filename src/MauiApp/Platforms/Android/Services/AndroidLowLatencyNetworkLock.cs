using Android.Content;
using Android.Net.Wifi;
using NdiForAndroid.Services;

namespace NdiForAndroid.Platforms.Android.Services;

/// <summary>
/// Holds a <see cref="WifiManager.WifiLock"/> while the viewer receives a stream. On API 29+ it uses
/// <c>WIFI_MODE_FULL_LOW_LATENCY</c> — the mode Android provides for real-time streaming: it
/// disables Wi-Fi power save (the radio no longer dozes between beacons, so the access point stops
/// buffering our packets and releasing them in bursts) and asks the chipset to favour latency.
/// The platform only honours it while the app is in the foreground with the screen on, which is
/// exactly when the viewer is visible. API 26–28 fall back to <c>WIFI_MODE_FULL_HIGH_PERF</c>.
/// Requires <c>android.permission.WAKE_LOCK</c> (declared in AndroidManifest.xml).
/// </summary>
public sealed class AndroidLowLatencyNetworkLock : ILowLatencyNetworkLock
{
    private const string LockTag = "ndi_viewer_low_latency";

    private readonly object _gate = new();
    private WifiManager.WifiLock? _lock;

    public void Acquire()
    {
        lock (_gate)
        {
            try
            {
                if (_lock is null)
                {
                    var wifiManager = global::Android.App.Application.Context
                        .GetSystemService(Context.WifiService) as WifiManager;
                    if (wifiManager is null)
                        return; // WifiManager unavailable — degrade silently.

                    _lock = wifiManager.CreateWifiLock(ChooseMode(), LockTag);
                    if (_lock is null)
                        return;

                    _lock.SetReferenceCounted(false);
                }

                if (!_lock.IsHeld)
                    _lock.Acquire();
            }
            catch
            {
                // Latency tuning is best-effort — never fail a stream start over it.
            }
        }
    }

    public void Release()
    {
        lock (_gate)
        {
            try
            {
                if (_lock?.IsHeld == true)
                    _lock.Release();
            }
            catch
            {
                // Best-effort teardown.
            }
        }
    }

    private static global::Android.Net.WifiMode ChooseMode()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
            return global::Android.Net.WifiMode.FullLowLatency;

        // FULL_HIGH_PERF is deprecated (and a no-op) from API 34, but it is only used on API 26–28
        // here, where it is the strongest mode available.
#pragma warning disable CS0618, CA1422
        return global::Android.Net.WifiMode.FullHighPerf;
#pragma warning restore CS0618, CA1422
    }
}
