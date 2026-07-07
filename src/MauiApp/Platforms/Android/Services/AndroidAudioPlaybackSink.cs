using Android.Media;
using NdiForAndroid.Services;
using Encoding = Android.Media.Encoding;

namespace NdiForAndroid.Platforms.Android.Services;

/// <summary>
/// Android float-PCM audio output for received NDI audio, backed by a streaming
/// <see cref="AudioTrack"/>.
/// NDI sources can carry more than two audio channels; Android playback here is
/// capped at stereo. When the source has more than two channels the track is
/// created as stereo and <see cref="Write"/> keeps the first two channels of
/// every frame, dropping the rest (no downmix weighting).
/// Thread-safety: per <see cref="IAudioPlaybackSink"/>, Start/Stop are serialized
/// by the caller and Write only runs between them on a single thread.
/// </summary>
public sealed class AndroidAudioPlaybackSink : IAudioPlaybackSink
{
    private AudioTrack? _track;
    private int _sampleRate;
    private int _sourceChannels;
    private float[]? _stereoScratch;

    public void Start(int sampleRate, int channels)
    {
        if (_track is not null && _sampleRate == sampleRate && _sourceChannels == channels)
            return; // Format unchanged and track live — idempotent per contract.

        Stop();

        _sampleRate = sampleRate;
        _sourceChannels = channels;
        var trackChannels = Math.Clamp(channels, 1, 2);
        var channelMask = trackChannels == 1 ? ChannelOut.Mono : ChannelOut.Stereo;

        try
        {
            // GetMinBufferSize returns BYTES (one float sample = 4 bytes); double it for headroom.
            var minBytes = AudioTrack.GetMinBufferSize(sampleRate, channelMask, Encoding.PcmFloat);
            if (minBytes <= 0)
                minBytes = sampleRate * trackChannels * sizeof(float) / 10; // ~100 ms fallback

            var attributesBuilder = new AudioAttributes.Builder();
            attributesBuilder.SetUsage(AudioUsageKind.Media);
            attributesBuilder.SetContentType(AudioContentType.Movie);
            var attributes = attributesBuilder.Build();

            var formatBuilder = new AudioFormat.Builder();
            formatBuilder.SetEncoding(Encoding.PcmFloat);
            formatBuilder.SetSampleRate(sampleRate);
            formatBuilder.SetChannelMask(channelMask);
            var format = formatBuilder.Build();

            if (attributes is null || format is null)
                return; // Cannot build a valid output; stay silently stopped.

            var trackBuilder = new AudioTrack.Builder();
            trackBuilder.SetAudioAttributes(attributes);
            trackBuilder.SetAudioFormat(format);
            trackBuilder.SetBufferSizeInBytes(minBytes * 2);
            trackBuilder.SetTransferMode(AudioTrackMode.Stream);

            var track = trackBuilder.Build();
            track.Play();
            _track = track;
        }
        catch
        {
            // Audio output setup must never crash the receive pump — play video silently.
            _track = null;
        }
    }

    public void Write(float[] interleaved, int frameCount)
    {
        var track = _track;
        if (track is null || frameCount <= 0)
            return;

        try
        {
            if (_sourceChannels <= 2)
            {
                track.Write(interleaved, 0, frameCount * _sourceChannels, WriteMode.Blocking);
                return;
            }

            // Source has >2 channels but the track is stereo: keep the first two
            // channels of each interleaved frame.
            var needed = frameCount * 2;
            if (_stereoScratch is null || _stereoScratch.Length < needed)
                _stereoScratch = new float[needed];

            for (var frame = 0; frame < frameCount; frame++)
            {
                _stereoScratch[frame * 2] = interleaved[frame * _sourceChannels];
                _stereoScratch[frame * 2 + 1] = interleaved[frame * _sourceChannels + 1];
            }

            track.Write(_stereoScratch, 0, needed, WriteMode.Blocking);
        }
        catch
        {
            // A failed write (e.g. track died during route change) must never crash the pump.
        }
    }

    public void Stop()
    {
        var track = _track;
        _track = null;
        _sampleRate = 0;
        _sourceChannels = 0;

        if (track is null)
            return;

        try
        {
            track.Stop();
            track.Release();
            track.Dispose();
        }
        catch
        {
            // Audio teardown must never crash.
        }
    }
}
