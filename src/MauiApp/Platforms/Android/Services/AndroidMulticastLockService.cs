using Android.Content;
using Android.Net.Wifi;
using NdiForAndroid.Services;

namespace NdiForAndroid.Platforms.Android.Services;

/// <summary>
/// Acquires / releases <see cref="WifiManager.MulticastLock"/> tagged <c>"ndi_mdns"</c>
/// to enable reception of multicast mDNS packets required for NDI zero-config discovery.
/// Compatible with Android API 26–35. No additional manifest permissions are required
/// beyond <c>CHANGE_WIFI_MULTICAST_STATE</c> which is already declared.
/// </summary>
public sealed class AndroidMulticastLockService : IMulticastLockService
{
    private WifiManager.MulticastLock? _lock;

    public Task AcquireAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_lock is null)
        {
            var wifiManager = (WifiManager)global::Android.App.Application.Context
                .GetSystemService(Context.WifiService)!;
            _lock = wifiManager.CreateMulticastLock("ndi_mdns")!;
            _lock.SetReferenceCounted(false);
        }

        if (!_lock.IsHeld)
            _lock.Acquire();

        return Task.CompletedTask;
    }

    public Task ReleaseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_lock?.IsHeld == true)
            _lock.Release();

        return Task.CompletedTask;
    }
}
