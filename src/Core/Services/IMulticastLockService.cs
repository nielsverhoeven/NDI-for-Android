namespace NdiForAndroid.Services;

/// <summary>
/// Abstracts the platform-specific multicast lock required for mDNS source discovery.
/// On Android this corresponds to <c>WifiManager.MulticastLock</c>.
/// On non-Android platforms a no-op implementation is used.
/// </summary>
public interface IMulticastLockService
{
    /// <summary>Acquires the multicast lock, enabling reception of multicast packets.</summary>
    Task AcquireAsync(CancellationToken cancellationToken = default);

    /// <summary>Releases the multicast lock. Safe to call even if not currently held.</summary>
    Task ReleaseAsync(CancellationToken cancellationToken = default);
}
