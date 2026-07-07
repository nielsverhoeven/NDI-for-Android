namespace NdiForAndroid.NdiBridge;

/// <summary>
/// P/Invoke output bridge against libndi.so — advertises this device as an NDI sender.
/// </summary>
public sealed class NdiOutputBridge : INdiOutputBridge, IDisposable
{
    private bool _disposed;
    private string? _activeStreamName;

    // Re-stream resources
    private readonly object _reStreamLock = new();
    private NdiSourceEntry? _reStreamSource;
    private QualityProfile _reStreamQuality;
    private bool _reStreamActive;

    public bool IsReStreamActive => _reStreamActive;

    public async Task StartOutputAsync(string streamName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(streamName))
            throw new ArgumentException("Stream name is required.", nameof(streamName));

        await Task.CompletedTask; // Placeholder for NDI sender P/Invoke (NDIlib_send_create).
        _activeStreamName = streamName;
    }

    public Task StopOutputAsync(CancellationToken cancellationToken = default)
    {
        _activeStreamName = null;
        return Task.CompletedTask;
    }

    public async Task StartReStreamFromSourceAsync(
        string sourceId,
        QualityProfile qualityProfile,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            throw new ArgumentException("Source id is required.", nameof(sourceId));

        lock (_reStreamLock)
        {
            if (_reStreamActive)
                return; // Already active — guard against double start.

            _reStreamSource = new NdiSourceEntry(
                sourceId,
                $"Re-stream of {sourceId}",
                null,
                true,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                DiscoveryMode.Mdns);
            _reStreamQuality = qualityProfile;
            _reStreamActive = true;

            // Placeholder: real implementation will:
            // 1. Create a dedicated receiver for the source via NDIlib_recv_create
            // 2. Start reading frames in a background loop (similar to viewer bridge pattern)
            // 3. Create an NDI sender and forward captured frames with NDIlib_send_video_v2
        }

        await Task.CompletedTask;
    }

    public Task StopReStreamAsync(CancellationToken cancellationToken = default)
    {
        lock (_reStreamLock)
        {
            _reStreamSource = null;
            _reStreamActive = false;

            // Placeholder: real implementation will:
            // 1. Stop reading frames loop
            // 2. Destroy receiver via NDIlib_recv_destroy
            // 3. Destroy sender via NDIlib_send_destroy
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _activeStreamName = null;

        lock (_reStreamLock)
        {
            _reStreamSource = null;
            _reStreamActive = false;
        }
    }
}
