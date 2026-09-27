namespace NdiForAndroid.NdiBridge;

/// <summary>
/// Latency budget for the NDI receive path — kept in Core (plain C#) so the rules are unit-testable
/// without libndi. The viewer bridge applies these to the receiver's queue depth
/// (<c>NDIlib_recv_get_queue</c>) on every captured frame.
/// <para>
/// Why this exists: the SDK queues every frame it receives. If the consumer ever falls behind
/// (a Wi-Fi power-save burst, a slow render, a sender clock that runs slightly fast) the backlog is
/// never drained by itself — the audio pump is paced by the DAC, so a 200 ms hiccup becomes 200 ms
/// of permanent extra audio delay, and a busy video pump shows frames that are already stale.
/// Low-latency monitors (e.g. the NDI monitor apps on iOS) always present the newest frame and keep
/// the audio backlog bounded; this policy does the same.
/// </para>
/// </summary>
public static class ReceiveLatencyPolicy
{
    /// <summary>
    /// Maximum audio (in ms) allowed to wait in the receiver queue on top of what the audio device
    /// buffers. It is the jitter buffer that absorbs Wi-Fi delivery bursts; anything above it is
    /// pure delay and is dropped so audio stays in step with the (newest-frame) video.
    /// </summary>
    public const int MaxQueuedAudioMs = 50;

    /// <summary>Audio device buffer to request, in ms — the receiver queue above absorbs network jitter.</summary>
    public const int AudioOutputBufferMs = 20;

    /// <summary>
    /// A captured video frame is stale when a newer one is already queued behind it: showing it
    /// only adds a frame of delay, so the pump skips the copy and moves on to the newest frame.
    /// </summary>
    public static bool IsVideoFrameStale(int queuedVideoFrames) => queuedVideoFrames > 0;

    /// <summary>Duration (ms) of the audio still queued behind the frame just captured.</summary>
    public static double QueuedAudioMs(int queuedAudioFrames, int samplesPerFrame, int sampleRate)
    {
        if (queuedAudioFrames <= 0 || samplesPerFrame <= 0 || sampleRate <= 0)
            return 0;

        return queuedAudioFrames * (double)samplesPerFrame * 1000.0 / sampleRate;
    }

    /// <summary>
    /// True when the audio queued behind the captured frame exceeds <see cref="MaxQueuedAudioMs"/>:
    /// the frame should be released unplayed so playback catches up to real time.
    /// </summary>
    public static bool ShouldDropAudioFrame(int queuedAudioFrames, int samplesPerFrame, int sampleRate)
        => QueuedAudioMs(queuedAudioFrames, samplesPerFrame, sampleRate) > MaxQueuedAudioMs;

    /// <summary>Audio device buffer size in bytes for float PCM, never below the platform minimum.</summary>
    public static int AudioOutputBufferBytes(int sampleRate, int channels, int platformMinimumBytes)
    {
        var target = (int)((long)sampleRate * channels * sizeof(float) * AudioOutputBufferMs / 1000);
        return Math.Max(platformMinimumBytes, target);
    }
}
