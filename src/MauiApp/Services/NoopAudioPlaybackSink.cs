namespace NdiForAndroid.Services;

/// <summary>No-op audio playback sink for non-Android build targets.</summary>
internal sealed class NoopAudioPlaybackSink : IAudioPlaybackSink
{
    public void Start(int sampleRate, int channels)
    {
        // Nothing to play off-Android.
    }

    public void Write(float[] interleaved, int frameCount)
    {
        // Nothing to play off-Android.
    }

    public void Stop()
    {
        // Nothing to release off-Android.
    }
}
