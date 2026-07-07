namespace NdiForAndroid.Services;

/// <summary>Video input selected for NDI output.</summary>
public enum VideoInputKind
{
    Screen,
    CameraFront,
    CameraRear,
}

/// <summary>Pixel format of a captured frame. All are accepted natively by NDI send (no conversion).</summary>
public enum CapturedPixelFormat
{
    /// <summary>32-bit BGRA.</summary>
    Bgra32,

    /// <summary>32-bit RGBA (Android ImageReader RGBA_8888 — screen capture).</summary>
    Rgba32,

    /// <summary>NV12 (Y plane + interleaved UV — Android camera YUV_420_888 repacked).</summary>
    Nv12,
}

/// <summary>
/// One captured video frame. The producer owns and reuses <see cref="Data"/>:
/// consumers must finish with the buffer before the event handler returns
/// (the NDI bridge sends synchronously from the callback for this reason).
/// </summary>
public sealed record CapturedVideoFrame(
    int Width,
    int Height,
    CapturedPixelFormat Format,
    byte[] Data,
    int StrideBytes,
    int FrameRateN = 30,
    int FrameRateD = 1);

/// <summary>
/// One captured audio chunk: interleaved float PCM (±1.0), producer-owned buffer,
/// same consume-before-return rule as video.
/// </summary>
public sealed record CapturedAudioChunk(
    int SampleRate,
    int Channels,
    float[] InterleavedSamples,
    int FrameCount);

/// <summary>
/// Platform video capture (screen via MediaProjection, camera via Camera2).
/// Frames are raised on a capture thread — never the UI thread.
/// </summary>
public interface IVideoCaptureSource
{
    event EventHandler<CapturedVideoFrame>? FrameReady;

    bool IsActive { get; }

    /// <summary>
    /// Starts capture. For <see cref="VideoInputKind.Screen"/> this includes the
    /// MediaProjection consent flow; the returned task faults with
    /// <see cref="OperationCanceledException"/> if the user declines.
    /// </summary>
    Task StartAsync(VideoInputKind kind, CancellationToken cancellationToken = default);

    Task StopAsync();
}

/// <summary>Platform microphone capture (AudioRecord float PCM on Android).</summary>
public interface IAudioCaptureSource
{
    event EventHandler<CapturedAudioChunk>? ChunkReady;

    bool IsActive { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync();
}
