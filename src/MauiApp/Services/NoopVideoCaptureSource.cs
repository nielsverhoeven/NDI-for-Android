namespace NdiForAndroid.Services;

/// <summary>
/// Non-Android fallback for <see cref="IVideoCaptureSource"/>: capture never
/// starts, <see cref="IsActive"/> stays false and no frames are raised.
/// </summary>
internal sealed class NoopVideoCaptureSource : IVideoCaptureSource
{
    // Explicit accessors: the event is intentionally never raised (avoids CS0067).
    public event EventHandler<CapturedVideoFrame>? FrameReady
    {
        add { }
        remove { }
    }

    public bool IsActive => false;

    public Task StartAsync(VideoInputKind kind, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopAsync() => Task.CompletedTask;
}
