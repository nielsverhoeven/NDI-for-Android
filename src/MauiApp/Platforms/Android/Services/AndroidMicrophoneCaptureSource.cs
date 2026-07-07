using Android.Media;
using Microsoft.Maui.ApplicationModel;
using NdiForAndroid.Services;
using Encoding = Android.Media.Encoding;

namespace NdiForAndroid.Platforms.Android.Services;

/// <summary>
/// Microphone capture for NDI send: 48 kHz stereo float PCM via <see cref="AudioRecord"/>,
/// read on a dedicated thread in 4800-sample interleaved chunks (2400 frames = 50 ms).
/// Chunks reuse a producer-owned buffer per the <see cref="IAudioCaptureSource"/>
/// contract (consumers finish with the buffer before the event handler returns).
/// </summary>
public sealed class AndroidMicrophoneCaptureSource : IAudioCaptureSource
{
    private const int SampleRate = 48000;
    private const int ChannelCount = 2;
    private const int ChunkSamples = 4800; // interleaved floats per blocking read

    private AudioRecord? _record;
    private Thread? _thread;
    private volatile bool _running;

    public event EventHandler<CapturedAudioChunk>? ChunkReady;

    public bool IsActive => _running;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_running)
            throw new InvalidOperationException("Microphone capture is already active; call StopAsync first.");

        // MAUI requires permission requests to originate on the main thread.
        var status = await MainThread.InvokeOnMainThreadAsync(
            () => Permissions.RequestAsync<Permissions.Microphone>()).ConfigureAwait(false);
        if (status != PermissionStatus.Granted)
            throw new OperationCanceledException("Microphone permission was denied.");
        cancellationToken.ThrowIfCancellationRequested();

        // GetMinBufferSize returns BYTES (one float sample = 4 bytes); double it for headroom.
        var minBytes = AudioRecord.GetMinBufferSize(SampleRate, ChannelIn.Stereo, Encoding.PcmFloat);
        if (minBytes <= 0)
            minBytes = SampleRate * ChannelCount * sizeof(float) / 10; // ~100 ms fallback

        var record = new AudioRecord(AudioSource.Mic, SampleRate, ChannelIn.Stereo, Encoding.PcmFloat, minBytes * 2);
        if (record.State != State.Initialized)
        {
            record.Release();
            throw new InvalidOperationException("AudioRecord failed to initialize (microphone busy or format unsupported).");
        }

        record.StartRecording();
        _record = record;
        _running = true;

        var thread = new Thread(CaptureLoop) { IsBackground = true, Name = "ndi-mic-capture" };
        _thread = thread;
        thread.Start();
    }

    public Task StopAsync()
    {
        _running = false;

        var record = _record;
        // Stop the recorder first so a blocking Read in the loop unwinds promptly.
        try { record?.Stop(); } catch { /* recorder may already be released */ }

        var thread = _thread;
        _thread = null;
        if (thread is not null && thread.IsAlive && thread != Thread.CurrentThread)
            thread.Join(2000);

        _record = null;
        try { record?.Release(); } catch { /* ignore — best-effort teardown */ }

        return Task.CompletedTask;
    }

    private void CaptureLoop()
    {
        // Producer-owned reusable buffer; consumers finish before ChunkReady returns.
        var buffer = new float[ChunkSamples];
        while (_running)
        {
            try
            {
                var record = _record;
                if (record is null)
                    break;

                // The final int parameter is the read mode; 0 == AudioRecord.READ_BLOCKING
                // (the binding exposes it as a plain int, not an enum).
                var read = record.Read(buffer, 0, buffer.Length, 0);
                if (read < 0)
                    break; // ERROR_* code — the recorder is unusable, exit the loop
                if (read == 0)
                    continue;

                ChunkReady?.Invoke(this, new CapturedAudioChunk(SampleRate, ChannelCount, buffer, read / ChannelCount));
            }
            catch
            {
                // The capture loop must never crash the process; stop capturing instead.
                break;
            }
        }
    }
}
