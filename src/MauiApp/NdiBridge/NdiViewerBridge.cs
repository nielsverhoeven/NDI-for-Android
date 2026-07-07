using System.Runtime.InteropServices;
using NdiForAndroid.Features.DiagOverlay.Services;
using NdiForAndroid.NdiBridge.Interop;
using NdiForAndroid.Services;

namespace NdiForAndroid.NdiBridge;

/// <summary>
/// P/Invoke viewer bridge against libndi.so.
/// One receiver instance at a time, serviced by two dedicated pump threads
/// (video+metadata, audio) following the proven legacy threading model:
/// atomic running flag, latest-frame double buffer swapped under a lock,
/// and thread joins never performed while holding the state lock.
/// Events are raised on the pump threads — callers marshal to the UI thread.
/// </summary>
public sealed class NdiViewerBridge : INdiViewerBridge, IDisposable
{
    private const string ReceiverName = "NDI for Android Viewer";
    private const uint VideoCaptureTimeoutMs = 1000;
    private const uint AudioCaptureTimeoutMs = 500;
    private const long StalledAfterMs = 3000;
    private const long StatsIntervalMs = 1000;
    private const long FpsWindowMs = 1000;

    private readonly NdiRuntime _runtime;
    private readonly IAudioPlaybackSink _audioSink;
    private readonly IDiagnosticOverlayService? _diagnostics;

    /// <summary>Guards receiver lifecycle (_recv, threads, tally, quality, source id).</summary>
    private readonly object _stateLock = new();

    /// <summary>Guards the front/back frame buffers only — never held across native calls.</summary>
    private readonly object _frameLock = new();

    /// <summary>Guards _connectionState; kept separate so events are raised lock-free.</summary>
    private readonly object _connectionLock = new();

    private IntPtr _recv;
    private Thread? _videoThread;
    private Thread? _audioThread;
    private volatile bool _running;

    private string? _activeSourceId;
    private QualityProfile _qualityProfile = QualityProfile.Balanced;
    private ConnectionState _connectionState = ConnectionState.Disconnected;

    // Latest-frame double buffer. The pump copies each native frame into _backPixels
    // (only ever touched by the pump thread) and swaps front/back references under
    // _frameLock. GetLatestFrame hands out the front reference without copying —
    // copying 1080p BGRA on every UI poll would churn ~8 MB per poll.
    private int[]? _frontPixels;
    private int[]? _backPixels;
    private int _frameWidth;
    private int _frameHeight;
    private long _frameTimestampMillis;

    private volatile float _measuredFps;
    private volatile float _droppedFramePercent;
    private volatile bool _isPtzSupported;
    private volatile bool _audioEnabled = true;

    private bool _tallyOnProgram;
    private bool _tallyOnPreview;
    private NdiTallyEcho? _lastTallyEcho;

    private bool _disposed;

    public NdiViewerBridge(
        NdiRuntime runtime,
        IAudioPlaybackSink audioSink,
        IDiagnosticOverlayService? diagnostics = null)
    {
        _runtime = runtime;
        _audioSink = audioSink;
        _diagnostics = diagnostics;
    }

    public QualityProfile ActiveQualityProfile
    {
        get { lock (_stateLock) return _qualityProfile; }
    }

    /// <inheritdoc />
    public event EventHandler<ConnectionState>? ConnectionStateChanged;

    /// <inheritdoc />
    public event EventHandler<NdiTallyEcho>? TallyEchoChanged;

    /// <inheritdoc />
    public bool IsAudioEnabled
    {
        get => _audioEnabled;
        set => _audioEnabled = value;
    }

    /// <inheritdoc />
    public bool IsPtzSupported => _isPtzSupported;

    public void StartReceiver(string sourceId, QualityProfile qualityProfile = QualityProfile.Balanced)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            throw new ArgumentException("Source id is required.", nameof(sourceId));

        // Full clean stop of any existing receiver before creating a new one.
        StopReceiver();

        lock (_stateLock)
        {
            _qualityProfile = qualityProfile;

            if (!_runtime.EnsureInitialized())
            {
                // Unsupported CPU / init failure — degrade gracefully, never throw.
                TransitionState(ConnectionState.Disconnected);
                return;
            }

            var recvNamePtr = Marshal.StringToHGlobalAnsi(ReceiverName);
            var sourceIdPtr = Marshal.StringToHGlobalAnsi(sourceId);
            try
            {
                // We only carry a single source id string: when it looks like host:port
                // it is the canonical p_url_address; otherwise it is the mDNS NDI name.
                var isUrlAddress = LooksLikeUrlAddress(sourceId);
                var create = new NdiRecvCreateV3Native
                {
                    source_to_connect_to = new NdiSourceNative
                    {
                        p_ndi_name = isUrlAddress ? IntPtr.Zero : sourceIdPtr,
                        p_url_address = isUrlAddress ? sourceIdPtr : IntPtr.Zero,
                    },
                    color_format = (int)NdiRecvColorFormat.BGRX_BGRA,
                    bandwidth = (int)MapBandwidth(qualityProfile),
                    allow_video_fields = false,
                    p_ndi_recv_name = recvNamePtr,
                };

                _recv = NdiNativeMethods.NDIlib_recv_create_v3(ref create);
                if (_recv == IntPtr.Zero)
                {
                    _runtime.ReleaseHandle();
                    TransitionState(ConnectionState.Disconnected);
                    return;
                }

                var source = create.source_to_connect_to;
                NdiNativeMethods.NDIlib_recv_connect(_recv, ref source);
            }
            finally
            {
                // The SDK copies the create/connect strings — safe to free now.
                Marshal.FreeHGlobal(recvNamePtr);
                Marshal.FreeHGlobal(sourceIdPtr);
            }

            // Tally is retained across reconnects — re-apply after every recv create.
            ApplyTallyLocked();

            _activeSourceId = sourceId;
            _running = true;
            TransitionState(ConnectionState.Connecting);

            // A state-change handler may have synchronously stopped the receiver
            // (the lock is reentrant on this thread) — never pump a dead handle.
            if (!_running || _recv == IntPtr.Zero)
                return;

            var recv = _recv;
            _videoThread = new Thread(() => VideoPumpLoop(recv, sourceId))
            {
                IsBackground = true,
                Name = "ndi-video-pump",
            };
            _audioThread = new Thread(() => AudioPumpLoop(recv))
            {
                IsBackground = true,
                Name = "ndi-audio-pump",
            };
            _videoThread.Start();
            _audioThread.Start();
        }
    }

    public void StopReceiver()
    {
        Thread? videoThread;
        Thread? audioThread;

        lock (_stateLock)
        {
            _running = false;
            videoThread = _videoThread;
            audioThread = _audioThread;
            _videoThread = null;
            _audioThread = null;
        }

        // Join OUTSIDE the state lock (legacy deadlock lesson). Skip self-join in
        // case an event handler running on a pump thread calls StopReceiver.
        if (videoThread is not null && !ReferenceEquals(videoThread, Thread.CurrentThread))
            videoThread.Join();
        if (audioThread is not null && !ReferenceEquals(audioThread, Thread.CurrentThread))
            audioThread.Join();

        lock (_stateLock)
        {
            if (_recv != IntPtr.Zero)
            {
                NdiNativeMethods.NDIlib_recv_destroy(_recv);
                _recv = IntPtr.Zero;
                _runtime.ReleaseHandle();
            }

            _activeSourceId = null;
            _isPtzSupported = false;
            _measuredFps = 0f;
            _droppedFramePercent = 0f;
            _lastTallyEcho = null;
        }

        lock (_frameLock)
        {
            _frontPixels = null;
            _backPixels = null;
            _frameWidth = 0;
            _frameHeight = 0;
            _frameTimestampMillis = 0;
        }

        _audioSink.Stop(); // safe when not started
        TransitionState(ConnectionState.Disconnected);
    }

    public void SetQualityProfile(QualityProfile profile)
    {
        string? restartSourceId = null;

        lock (_stateLock)
        {
            var bandwidthChanged = MapBandwidth(_qualityProfile) != MapBandwidth(profile);
            _qualityProfile = profile;

            // Bandwidth is a create-time setting on the receiver — applying a new
            // tier requires recreating it with the same source.
            if (_recv != IntPtr.Zero && bandwidthChanged)
                restartSourceId = _activeSourceId;
        }

        if (restartSourceId is not null)
            StartReceiver(restartSourceId, profile);
    }

    public ConnectionState GetConnectionState()
    {
        lock (_connectionLock) return _connectionState;
    }

    public NdiVideoFrame? GetLatestFrame()
    {
        lock (_frameLock)
        {
            if (_frontPixels is null || _frameWidth <= 0 || _frameHeight <= 0)
                return null;

            // The record wraps the CURRENT front buffer without copying. It is
            // immutable-by-convention: callers must not mutate the pixels, and the
            // array may be recycled by the pump after the double buffer cycles twice.
            return new NdiVideoFrame(_frameWidth, _frameHeight, _frontPixels, _frameTimestampMillis);
        }
    }

    public float GetDroppedFramePercent() => _droppedFramePercent;

    public (int Width, int Height) GetActualResolution()
    {
        lock (_frameLock) return (_frameWidth, _frameHeight);
    }

    public float GetMeasuredFps() => _measuredFps;

    /// <inheritdoc />
    public void SetTally(bool onProgram, bool onPreview)
    {
        lock (_stateLock)
        {
            _tallyOnProgram = onProgram;
            _tallyOnPreview = onPreview;
            ApplyTallyLocked();
        }
    }

    // ── PTZ ──────────────────────────────────────────────────────────────────
    // All passthroughs hold the state lock so the recv handle cannot be
    // destroyed mid-call; StopReceiver only destroys under the same lock.

    public bool PtzPanTiltSpeed(float panSpeed, float tiltSpeed)
    {
        lock (_stateLock)
            return _recv != IntPtr.Zero && _isPtzSupported
                && NdiNativeMethods.NDIlib_recv_ptz_pan_tilt_speed(_recv, panSpeed, tiltSpeed);
    }

    public bool PtzZoomSpeed(float zoomSpeed)
    {
        lock (_stateLock)
            return _recv != IntPtr.Zero && _isPtzSupported
                && NdiNativeMethods.NDIlib_recv_ptz_zoom_speed(_recv, zoomSpeed);
    }

    public bool PtzStorePreset(int presetNo)
    {
        lock (_stateLock)
            return _recv != IntPtr.Zero && _isPtzSupported
                && NdiNativeMethods.NDIlib_recv_ptz_store_preset(_recv, presetNo);
    }

    public bool PtzRecallPreset(int presetNo, float speed = 1f)
    {
        lock (_stateLock)
            return _recv != IntPtr.Zero && _isPtzSupported
                && NdiNativeMethods.NDIlib_recv_ptz_recall_preset(_recv, presetNo, speed);
    }

    public bool PtzAutoFocus()
    {
        lock (_stateLock)
            return _recv != IntPtr.Zero && _isPtzSupported
                && NdiNativeMethods.NDIlib_recv_ptz_auto_focus(_recv);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopReceiver();
    }

    // ── Video pump ───────────────────────────────────────────────────────────

    private void VideoPumpLoop(IntPtr recv, string sourceId)
    {
        var frameTimes = new Queue<long>();
        var lastVideoTicks = Environment.TickCount64;
        long lastStatsTicks = 0;
        var hasEverConnected = false;

        try
        {
            while (_running)
            {
                var video = default(NdiVideoFrameV2Native);
                var metadata = default(NdiMetadataFrameNative);

                var frameType = NdiNativeMethods.NDIlib_recv_capture_v3(
                    recv, ref video, IntPtr.Zero, ref metadata, VideoCaptureTimeoutMs);

                var now = Environment.TickCount64;

                switch (frameType)
                {
                    case NdiFrameType.Video:
                        try
                        {
                            CopyVideoFrame(ref video);
                        }
                        finally
                        {
                            // Mandatory per frame — leaking = native OOM within seconds.
                            NdiNativeMethods.NDIlib_recv_free_video_v2(recv, ref video);
                        }

                        lastVideoTicks = now;
                        hasEverConnected = true;
                        frameTimes.Enqueue(now);
                        TransitionState(ConnectionState.Connected);
                        break;

                    case NdiFrameType.Metadata:
                        string? metadataXml;
                        try
                        {
                            // Copy the receiver-owned string before freeing the frame.
                            metadataXml = metadata.p_data == IntPtr.Zero
                                ? null
                                : Marshal.PtrToStringUTF8(metadata.p_data);
                        }
                        finally
                        {
                            NdiNativeMethods.NDIlib_recv_free_metadata(recv, ref metadata);
                        }

                        // Parse/raise only after the frame is freed — a handler could
                        // synchronously stop the receiver.
                        if (metadataXml is not null)
                            HandleMetadata(metadataXml);
                        break;

                    case NdiFrameType.StatusChange:
                        // PTZ support arrives with connection metadata — re-check here.
                        _isPtzSupported = NdiNativeMethods.NDIlib_recv_ptz_is_supported(recv);
                        break;

                    case NdiFrameType.None:
                        // Capture timed out — check for a stalled source or lost connection.
                        // Query the native handle BEFORE raising any event: a handler could
                        // synchronously stop the receiver and invalidate `recv`.
                        // Only demote to Disconnected once a connection existed; during the
                        // initial handshake no_connections is legitimately still 0.
                        var connectionLost = hasEverConnected
                            && NdiNativeMethods.NDIlib_recv_get_no_connections(recv) == 0;

                        if (GetConnectionState() == ConnectionState.Connected
                            && now - lastVideoTicks > StalledAfterMs)
                        {
                            TransitionState(ConnectionState.Connecting);
                        }

                        if (connectionLost)
                            TransitionState(ConnectionState.Disconnected);
                        break;
                }

                // Rolling 1 s FPS window (legacy measurement model).
                while (frameTimes.Count > 0 && now - frameTimes.Peek() > FpsWindowMs)
                    frameTimes.Dequeue();
                _measuredFps = frameTimes.Count;

                // _running re-check: an event handler raised above may have stopped the
                // receiver on this thread, in which case `recv` is no longer valid.
                if (_running && now - lastStatsTicks >= StatsIntervalMs)
                {
                    lastStatsTicks = now;
                    UpdateStats(recv, sourceId);
                }
            }
        }
        catch
        {
            // A pump thread must never take down the process (unhandled exceptions on
            // background threads are fatal in .NET). Report loss of the stream instead.
            TransitionState(ConnectionState.Disconnected);
        }
    }

    private void CopyVideoFrame(ref NdiVideoFrameV2Native video)
    {
        var width = video.xres;
        var height = video.yres;
        if (width <= 0 || height <= 0 || video.p_data == IntPtr.Zero)
            return;

        var pixelCount = width * height;
        if (_backPixels is null || _backPixels.Length != pixelCount)
            _backPixels = new int[pixelCount]; // reallocate only when the size changes

        // BGRX/BGRA little-endian byte order (B,G,R,A) read as an int is 0xAARRGGBB —
        // exactly the packed ARGB layout Android bitmaps expect, so a raw copy suffices.
        // Copy row-by-row honoring line_stride_in_bytes (never assume width * 4).
        for (var row = 0; row < height; row++)
        {
            var sourceRow = video.p_data + row * video.line_stride_in_bytes;
            Marshal.Copy(sourceRow, _backPixels, row * width, width);
        }

        // NDI timestamps are 100 ns units since the Unix epoch; INT64_MAX = undefined.
        var timestampMillis = video.timestamp is > 0 and < long.MaxValue
            ? video.timestamp / 10_000
            : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        lock (_frameLock)
        {
            (_frontPixels, _backPixels) = (_backPixels, _frontPixels);
            _frameWidth = width;
            _frameHeight = height;
            _frameTimestampMillis = timestampMillis;
        }
    }

    private void HandleMetadata(string xml)
    {
        if (xml.Length == 0 || !xml.Contains("ndi_tally_echo", StringComparison.OrdinalIgnoreCase))
            return;

        var echo = new NdiTallyEcho(
            ExtractXmlBoolAttribute(xml, "on_program"),
            ExtractXmlBoolAttribute(xml, "on_preview"));

        if (echo != _lastTallyEcho)
        {
            _lastTallyEcho = echo;
            TallyEchoChanged?.Invoke(this, echo);
        }
    }

    private void UpdateStats(IntPtr recv, string sourceId)
    {
        NdiNativeMethods.NDIlib_recv_get_performance(recv, out var total, out var dropped);
        _droppedFramePercent = total.video_frames > 0
            ? (float)(dropped.video_frames * 100.0 / total.video_frames)
            : 0f;

        if (_diagnostics is not null)
        {
            int width, height;
            lock (_frameLock)
            {
                width = _frameWidth;
                height = _frameHeight;
            }

            _diagnostics.UpdateViewerDiagnostics(_measuredFps, _droppedFramePercent, width, height, sourceId);
        }
    }

    // ── Audio pump ───────────────────────────────────────────────────────────

    private void AudioPumpLoop(IntPtr recv)
    {
        var interleaved = Array.Empty<float>();
        var channelScratch = Array.Empty<float>();
        var sinkStarted = false;

        try
        {
            while (_running)
            {
                var audio = default(NdiAudioFrameV3Native);
                var frameType = NdiNativeMethods.NDIlib_recv_capture_audio_v3(
                    recv, IntPtr.Zero, ref audio, IntPtr.Zero, AudioCaptureTimeoutMs);

                if (frameType != NdiFrameType.Audio)
                    continue;

                try
                {
                    if (!_audioEnabled)
                    {
                        if (sinkStarted)
                        {
                            _audioSink.Stop();
                            sinkStarted = false;
                        }
                        continue; // finally still frees the frame
                    }

                    var channels = audio.no_channels;
                    var samples = audio.no_samples;
                    if (channels <= 0 || samples <= 0 || audio.p_data == IntPtr.Zero)
                        continue;

                    _audioSink.Start(audio.sample_rate, channels); // idempotent for unchanged format
                    sinkStarted = true;

                    var needed = samples * channels;
                    if (interleaved.Length < needed)
                        interleaved = new float[needed];
                    if (channelScratch.Length < samples)
                        channelScratch = new float[samples];

                    // FLTP planar (channel-major, channel_stride_in_bytes apart) → interleaved.
                    for (var channel = 0; channel < channels; channel++)
                    {
                        var channelPtr = audio.p_data + channel * audio.channel_stride_in_bytes;
                        Marshal.Copy(channelPtr, channelScratch, 0, samples);
                        for (var sample = 0; sample < samples; sample++)
                            interleaved[sample * channels + channel] = channelScratch[sample];
                    }

                    _audioSink.Write(interleaved, samples);
                }
                finally
                {
                    // Mandatory per frame — leaking = native OOM within seconds.
                    NdiNativeMethods.NDIlib_recv_free_audio_v3(recv, ref audio);
                }
            }
        }
        catch
        {
            // A pump thread must never take down the process.
        }
        finally
        {
            _audioSink.Stop(); // safe when not started
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void TransitionState(ConnectionState newState)
    {
        bool changed;
        lock (_connectionLock)
        {
            changed = _connectionState != newState;
            _connectionState = newState;
        }

        if (changed)
            ConnectionStateChanged?.Invoke(this, newState);
    }

    /// <summary>Must hold <see cref="_stateLock"/>.</summary>
    private void ApplyTallyLocked()
    {
        if (_recv == IntPtr.Zero)
            return;

        var tally = new NdiTallyNative
        {
            on_program = _tallyOnProgram,
            on_preview = _tallyOnPreview,
        };
        NdiNativeMethods.NDIlib_recv_set_tally(_recv, ref tally);
    }

    /// <summary>
    /// Maps the quality profile to the receiver bandwidth tier — the only quality
    /// lever the standard SDK exposes. Smooth uses the SDK's low-bandwidth preview
    /// stream (lower resolution, lower latency); Balanced and High both use the
    /// full-bandwidth stream (further differentiation would need the Advanced SDK).
    /// </summary>
    private static NdiRecvBandwidth MapBandwidth(QualityProfile profile) => profile switch
    {
        QualityProfile.Smooth => NdiRecvBandwidth.Lowest,
        _ => NdiRecvBandwidth.Highest,
    };

    /// <summary>
    /// Heuristic: a source id of the form "host:port" (no spaces/parentheses, numeric
    /// port suffix) is an NDI url address; anything else is an mDNS NDI name like
    /// "MACHINE (Channel)".
    /// </summary>
    private static bool LooksLikeUrlAddress(string sourceId)
    {
        if (sourceId.Contains(' ') || sourceId.Contains('('))
            return false;

        var colon = sourceId.LastIndexOf(':');
        if (colon <= 0 || colon == sourceId.Length - 1)
            return false;

        for (var i = colon + 1; i < sourceId.Length; i++)
        {
            if (!char.IsAsciiDigit(sourceId[i]))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Extracts a boolean XML attribute value via string scanning — the tally echo
    /// payload is tiny and fixed-shape, so no XML parser dependency is warranted.
    /// </summary>
    private static bool ExtractXmlBoolAttribute(string xml, string attributeName)
    {
        var index = xml.IndexOf(attributeName + "=", StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return false;

        var valueStart = index + attributeName.Length + 1;
        if (valueStart < xml.Length && (xml[valueStart] == '"' || xml[valueStart] == '\''))
            valueStart++;

        return valueStart + 4 <= xml.Length
            && string.Compare(xml, valueStart, "true", 0, 4, StringComparison.OrdinalIgnoreCase) == 0;
    }
}
