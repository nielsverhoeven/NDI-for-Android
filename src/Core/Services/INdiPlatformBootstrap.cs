namespace NdiForAndroid.Services;

/// <summary>
/// Platform prerequisites that must be satisfied before any NDI native object is
/// created. On Android the NDI SDK requires the app to hold an <c>NsdManager</c>
/// instance for the lifetime of NDI usage ("the NDI library requires the use of the
/// NsdManager from Android, and there is no way for a third-party library to do this
/// on its own" — NDI SDK platform docs). On other platforms this is a no-op.
/// </summary>
public interface INdiPlatformBootstrap
{
    /// <summary>
    /// Ensures platform prerequisites are in place. Idempotent; call before creating
    /// any NDI finder/receiver/sender.
    /// </summary>
    void EnsureReady();
}
