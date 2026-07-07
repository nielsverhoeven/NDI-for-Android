namespace NdiForAndroid.Services;

/// <summary>No-op NDI platform bootstrap for non-Android build targets.</summary>
internal sealed class DefaultNdiPlatformBootstrap : INdiPlatformBootstrap
{
    public void EnsureReady()
    {
        // Nothing required off-Android.
    }
}
