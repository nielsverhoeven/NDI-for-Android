using System.Runtime.InteropServices;
using NdiForAndroid.NdiBridge.Interop;
using NdiForAndroid.Services;

namespace NdiForAndroid.NdiBridge;

/// <summary>
/// P/Invoke discovery bridge against libndi.so with mDNS and Discovery Server dual-mode support.
/// A single long-lived finder is kept per active mode configuration (the NDI finder accumulates
/// sources across polls — recreating it per poll loses that state). Mode switching destroys the
/// finder; it is lazily recreated on the next <see cref="DiscoverSourcesAsync"/>.
/// All operations are serialized via a SemaphoreSlim(1) guard.
/// </summary>
public sealed class NdiDiscoveryBridge : INdiDiscoveryBridge, IDisposable
{
    private const uint WaitForSourcesTimeoutMs = 1500;

    private readonly NdiRuntime _runtime;
    private readonly IMulticastLockService _multicastLockService;
    private readonly SemaphoreSlim _modeLock = new(1, 1);

    private DiscoveryMode _activeMode = DiscoveryMode.Mdns;
    private IReadOnlyList<DiscoveryServerEndpoint> _serverEndpoints = Array.Empty<DiscoveryServerEndpoint>();
    private IntPtr _finder;
    private bool _disposed;

    public NdiDiscoveryBridge(NdiRuntime runtime, IMulticastLockService multicastLockService)
    {
        _runtime = runtime;
        _multicastLockService = multicastLockService;
    }

    /// <inheritdoc />
    public void SetDiscoveryMode(
        DiscoveryMode mode,
        IReadOnlyList<DiscoveryServerEndpoint>? serverEndpoints = null)
    {
        _modeLock.Wait();
        try
        {
            // The finder is bound to its create-time config — destroy it; the next
            // DiscoverSourcesAsync lazily recreates it with the new configuration.
            DestroyFinderLocked();

            _activeMode = mode;
            _serverEndpoints = mode == DiscoveryMode.DiscoveryServer && serverEndpoints is { Count: > 0 }
                ? serverEndpoints
                : Array.Empty<DiscoveryServerEndpoint>();

            // The NDI library reads its discovery-server config at initialize time;
            // NdiRuntime reinitializes when idle (or defers until the last handle drops).
            _runtime.SetDiscoveryServers(
                _activeMode == DiscoveryMode.DiscoveryServer
                    ? string.Join(',', _serverEndpoints.Select(e => $"{e.Host}:{e.Port}"))
                    : string.Empty);

            // Release multicast lock when switching away from mDNS.
            if (mode != DiscoveryMode.Mdns)
                _ = _multicastLockService.ReleaseAsync();
        }
        finally
        {
            _modeLock.Release();
        }
    }

    public async Task<IReadOnlyList<NdiSourceEntry>> DiscoverSourcesAsync(CancellationToken cancellationToken = default)
    {
        await _modeLock.WaitAsync(cancellationToken);
        try
        {
            var mode = _activeMode;

            // mDNS discovery silently times out on Android without a held multicast lock.
            if (mode == DiscoveryMode.Mdns)
                await _multicastLockService.AcquireAsync(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (!EnsureFinderLocked())
                return Array.Empty<NdiSourceEntry>();

            var finder = _finder;

            // wait_for_sources blocks up to 1.5 s — never block the caller's context.
            return await Task.Run(() => PollSources(finder, mode, cancellationToken), cancellationToken);
        }
        finally
        {
            _modeLock.Release();
        }
    }

    /// <summary>Creates the long-lived finder for the active mode. Must hold <see cref="_modeLock"/>.</summary>
    private bool EnsureFinderLocked()
    {
        if (_finder != IntPtr.Zero)
            return true;

        if (!_runtime.EnsureInitialized())
            return false; // Unsupported CPU / init failure — degrade to empty results.

        // p_extra_ips takes hosts only (no ports), comma-separated.
        var extraIps = _activeMode == DiscoveryMode.DiscoveryServer && _serverEndpoints.Count > 0
            ? string.Join(',', _serverEndpoints.Select(e => e.Host).Distinct(StringComparer.OrdinalIgnoreCase))
            : null;

        var extraIpsPtr = extraIps is null ? IntPtr.Zero : Marshal.StringToHGlobalAnsi(extraIps);
        try
        {
            var create = new NdiFindCreateNative
            {
                show_local_sources = true,
                p_groups = IntPtr.Zero,
                p_extra_ips = extraIpsPtr,
            };
            _finder = NdiNativeMethods.NDIlib_find_create_v2(ref create);
        }
        finally
        {
            // The SDK copies the create strings during find_create — safe to free now.
            if (extraIpsPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(extraIpsPtr);
        }

        if (_finder == IntPtr.Zero)
        {
            _runtime.ReleaseHandle();
            return false;
        }

        return true;
    }

    /// <summary>Destroys the finder and releases its runtime handle. Must hold <see cref="_modeLock"/>.</summary>
    private void DestroyFinderLocked()
    {
        if (_finder == IntPtr.Zero)
            return;

        NdiNativeMethods.NDIlib_find_destroy(_finder);
        _finder = IntPtr.Zero;
        _runtime.ReleaseHandle();
    }

    private static IReadOnlyList<NdiSourceEntry> PollSources(
        IntPtr finder, DiscoveryMode mode, CancellationToken cancellationToken)
    {
        NdiNativeMethods.NDIlib_find_wait_for_sources(finder, WaitForSourcesTimeoutMs);
        cancellationToken.ThrowIfCancellationRequested();

        var listPtr = NdiNativeMethods.NDIlib_find_get_current_sources(finder, out var count);
        if (listPtr == IntPtr.Zero || count == 0)
            return Array.Empty<NdiSourceEntry>();

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var structSize = Marshal.SizeOf<NdiSourceNative>();
        var entries = new List<NdiSourceEntry>((int)count);
        var seenSourceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var native = Marshal.PtrToStructure<NdiSourceNative>(listPtr + i * structSize);

            // Finder owns the list memory — copy the strings immediately; the next
            // finder call invalidates them.
            var ndiName = native.p_ndi_name == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(native.p_ndi_name);
            var urlAddress = native.p_url_address == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(native.p_url_address);

            var sourceId = !string.IsNullOrEmpty(urlAddress) ? urlAddress : ndiName;
            if (string.IsNullOrEmpty(sourceId) || !seenSourceIds.Add(sourceId))
                continue;

            entries.Add(new NdiSourceEntry(
                sourceId,
                string.IsNullOrEmpty(ndiName) ? sourceId : ndiName,
                urlAddress,
                true,
                now,
                mode));
        }

        return entries;
    }

    public Task<bool> IsDiscoveryServerReachableAsync(string host, int port, CancellationToken cancellationToken = default) =>
        NetworkReachability.IsTcpReachableAsync(host, port, cancellationToken);

    public async Task<NdiDiscoveryCheckResult> PerformDiscoveryCheckAsync(
        string host, int port, string correlationId, CancellationToken cancellationToken = default)
    {
        _ = correlationId;

        if (string.IsNullOrWhiteSpace(host) || port is < 1 or > 65535)
            return new NdiDiscoveryCheckResult(false, "UNKNOWN", "Invalid discovery endpoint");

        try
        {
            var reachable = await IsDiscoveryServerReachableAsync(host, port, cancellationToken);
            return reachable
                ? new NdiDiscoveryCheckResult(true, "NONE", null)
                : new NdiDiscoveryCheckResult(false, "ENDPOINT_UNREACHABLE", $"Cannot reach {host}:{port}");
        }
        catch (OperationCanceledException)
        {
            return new NdiDiscoveryCheckResult(false, "TIMEOUT", $"Discovery check timed out for {host}:{port}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _modeLock.Wait();
        try
        {
            DestroyFinderLocked();
        }
        finally
        {
            _modeLock.Release();
        }

        _modeLock.Dispose();
    }
}
