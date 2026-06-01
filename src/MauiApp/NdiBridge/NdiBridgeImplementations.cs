using System.Runtime.InteropServices;

namespace NdiForAndroid.NdiBridge;

/// <summary>
/// P/Invoke discovery bridge against libndi.so.
/// Full implementation wired in T004.
/// </summary>
public sealed class NdiDiscoveryBridge : INdiDiscoveryBridge, IDisposable
{
    private string? _discoveryHost;
    private int? _discoveryPort;
    private bool _disposed;

    public void SetDiscoveryEndpoint(string? host, int? port)
    {
        _discoveryHost = host;
        _discoveryPort = port;
    }

    public Task<IReadOnlyList<NdiSourceEntry>> DiscoverSourcesAsync(CancellationToken cancellationToken = default)
    {
        // TODO (T004): invoke NDI native discovery via P/Invoke.
        return Task.FromResult<IReadOnlyList<NdiSourceEntry>>(Array.Empty<NdiSourceEntry>());
    }

    public Task<bool> IsDiscoveryServerReachableAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        // TODO (T004): UDP reachability check.
        return Task.FromResult(false);
    }

    public Task<NdiDiscoveryCheckResult> PerformDiscoveryCheckAsync(
        string host, int port, string correlationId, CancellationToken cancellationToken = default)
    {
        // TODO (T004): full NDI protocol handshake check.
        return Task.FromResult(new NdiDiscoveryCheckResult(false, "NOT_IMPLEMENTED", "NDI bridge not yet wired (T004)"));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}

/// <summary>
/// P/Invoke viewer bridge against libndi.so.
/// Full implementation wired in T004.
/// </summary>
public sealed class NdiViewerBridge : INdiViewerBridge, IDisposable
{
    private bool _disposed;

    public void StartReceiver(string sourceId)
    {
        // TODO (T004): call NDI receiver start via P/Invoke.
    }

    public void StopReceiver()
    {
        // TODO (T004): call NDI receiver stop via P/Invoke.
    }

    public NdiVideoFrame? GetLatestFrame() => null; // TODO (T004)

    public float GetDroppedFramePercent() => 0f;

    public (int Width, int Height) GetActualResolution() => (0, 0);

    public float GetMeasuredFps() => 0f;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}

/// <summary>
/// P/Invoke output bridge against libndi.so.
/// Full implementation wired in T004.
/// </summary>
public sealed class NdiOutputBridge : INdiOutputBridge, IDisposable
{
    private bool _disposed;

    public Task<bool> IsSourceReachableAsync(string sourceId, CancellationToken cancellationToken = default)
        => Task.FromResult(false); // TODO (T004)

    public Task StartOutputAsync(string sourceId, string streamName, CancellationToken cancellationToken = default)
        => Task.CompletedTask; // TODO (T004)

    public Task StopOutputAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask; // TODO (T004)

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
