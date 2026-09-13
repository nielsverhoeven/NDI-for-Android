namespace NdiForAndroid.Services;

/// <summary>
/// Abstracts Android screen-orientation locking for the full-screen viewer (#383/#384). On
/// non-Android platforms a no-op implementation is used.
/// </summary>
public interface IOrientationLockService
{
    /// <summary>Locks the device to landscape.</summary>
    void RequestLandscape();

    /// <summary>Locks the device to portrait.</summary>
    void RequestPortrait();

    /// <summary>Releases any orientation lock, returning to the user's system auto-rotate setting.</summary>
    void Release();
}
