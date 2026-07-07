namespace NdiForAndroid.Services;

/// <summary>
/// Platform audio output for received NDI audio. On Android this is an
/// <c>AudioTrack</c> in float PCM mode; on other targets a no-op.
/// Thread-safety: Start/Stop are serialized by the caller (the viewer bridge's
/// audio pump); Write is only called between Start and Stop from a single thread.
/// </summary>
public interface IAudioPlaybackSink
{
    /// <summary>Prepares the output for the given format. Idempotent for an unchanged format.</summary>
    void Start(int sampleRate, int channels);

    /// <summary>Writes interleaved float PCM (±1.0 range), <paramref name="frameCount"/> frames per channel.</summary>
    void Write(float[] interleaved, int frameCount);

    /// <summary>Stops playback and releases the platform output. Safe to call when not started.</summary>
    void Stop();
}
