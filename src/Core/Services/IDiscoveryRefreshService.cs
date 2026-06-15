using NdiForAndroid.Features.Sources.Models;

namespace NdiForAndroid.Services;

/// <summary>
/// Coordinates automatic background polling for NDI source discovery.
/// Polling is active only while the app is in the foreground.
/// </summary>
public interface IDiscoveryRefreshService
{
    /// <summary>
    /// Raised on a background thread when a discovery poll completes (success or failure).
    /// Consumers must marshal to the UI thread if required.
    /// </summary>
    event EventHandler<DiscoverySnapshot> SnapshotReady;

    /// <summary>Starts the polling loop. Idempotent — safe to call multiple times.</summary>
    void Start();

    /// <summary>Stops the polling loop and cancels any in-flight request. Idempotent.</summary>
    void Stop();

    /// <summary>
    /// Triggers an immediate discovery poll, subject to debounce rules.
    /// Returns immediately; the result is delivered via <see cref="SnapshotReady"/>.
    /// </summary>
    void RequestRefresh();
}
