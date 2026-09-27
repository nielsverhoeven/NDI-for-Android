namespace NdiForAndroid.Services;

/// <summary>No-op implementation of <see cref="ILowLatencyNetworkLock"/> for non-Android build targets.</summary>
internal sealed class NoopLowLatencyNetworkLock : ILowLatencyNetworkLock
{
    public void Acquire()
    {
        // No radio power policy to change off-Android.
    }

    public void Release()
    {
        // Nothing to release off-Android.
    }
}
