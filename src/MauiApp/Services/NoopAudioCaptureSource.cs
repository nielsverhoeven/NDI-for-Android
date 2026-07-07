namespace NdiForAndroid.Services;

/// <summary>
/// Non-Android fallback for <see cref="IAudioCaptureSource"/>: capture never
/// starts, <see cref="IsActive"/> stays false and no chunks are raised.
/// </summary>
internal sealed class NoopAudioCaptureSource : IAudioCaptureSource
{
    // Explicit accessors: the event is intentionally never raised (avoids CS0067).
    public event EventHandler<CapturedAudioChunk>? ChunkReady
    {
        add { }
        remove { }
    }

    public bool IsActive => false;

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopAsync() => Task.CompletedTask;
}
