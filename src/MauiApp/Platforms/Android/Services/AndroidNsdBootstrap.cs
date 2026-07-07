using Android.Content;
using Android.Net.Nsd;
using NdiForAndroid.Services;

namespace NdiForAndroid.Platforms.Android.Services;

/// <summary>
/// Holds the Android <see cref="NsdManager"/> instance the NDI SDK requires for
/// mDNS discovery/registration. The manager must be obtained before any NDI object
/// is created and kept referenced while NDI is in use (per NDI platform docs).
/// </summary>
public sealed class AndroidNsdBootstrap : INdiPlatformBootstrap
{
    private NsdManager? _nsdManager;

    public void EnsureReady()
    {
        _nsdManager ??= (NsdManager?)global::Android.App.Application.Context
            .GetSystemService(Context.NsdService);
    }
}
