using System.Runtime.InteropServices;
using NdiForAndroid.NdiBridge.Interop;
using NdiForAndroid.Services;

namespace NdiForAndroid.NdiBridge;

/// <summary>
/// P/Invoke output bridge against libndi.so — advertises this device as an NDI sender.
///
/// Two independent sessions:
/// 1. Capture output: platform video (screen/camera) + optional microphone audio
///    fed into an NDI sender. Video is sent SYNCHRONOUSLY from the capture callback
///    because the producer owns and reuses the frame buffer — an async send would let
///    the SDK read a buffer that is being overwritten. (Async + ping-pong buffers is
///    a later optimization, see NDIlib_send_send_video_async_v2.)
/// 2. Re-stream: a dedicated receiver pumps frames from a remote NDI source straight
///    into a dedicated sender (zero-copy: the recv-owned native buffer is forwarded
///    to the synchronous send, then freed).
/// </summary>
public sealed class NdiOutputBridge : INdiOutputBridge, IDisposable
{
    private const uint ReStreamCaptureTimeoutMs = 1000;
    private static readonly TimeSpan StatusPollInterval = TimeSpan.FromSeconds(1);

    private readonly NdiRuntime _runtime;
    private readonly IVideoCaptureSource _videoSource;
    private readonly IAudioCaptureSource _audioSource;

    /// <summary>Guards the capture-output sender handle. Held across every native send
    /// call so StopOutputAsync can never destroy the handle mid-send.</summary>
    private readonly object _sendLock = new();

    /// <summary>Guards re-stream lifecycle state (handles + thread reference).</summary>
    private readonly object _reStreamLock = new();

    private IntPtr _send;
    private bool _micRequested;
    private Timer? _statusTimer;
    private volatile bool _isOnProgramTally;
    private volatile int _connectionCount;

    private IntPtr _reStreamRecv;
    private IntPtr _reStreamSend;
    private Thread? _reStreamThread;
    private volatile bool _reStreamRunning;

    private bool _disposed;

    public NdiOutputBridge(NdiRuntime runtime, IVideoCaptureSource videoSource, IAudioCaptureSource audioSource)
    {
        _runtime = runtime;
        _videoSource = videoSource;
        _audioSource = audioSource;
    }

    /// <inheritdoc />
    public event EventHandler? OutputStatusChanged;

    /// <inheritdoc />
    public bool IsOnProgramTally => _isOnProgramTally;

    /// <inheritdoc />
    public int ConnectionCount => _connectionCount;

    /// <inheritdoc />
    public bool IsReStreamActive => _reStreamRunning;

    // ── Capture output ───────────────────────────────────────────────────────

    public async Task StartOutputAsync(
        string streamName,
        VideoInputKind inputKind = VideoInputKind.Screen,
        bool captureMicrophone = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(streamName))
            throw new ArgumentException("Stream name is required.", nameof(streamName));

        // Full clean stop of any active session before starting a new one.
        await StopOutputAsync(CancellationToken.None).ConfigureAwait(false);

        if (!_runtime.EnsureInitialized())
            throw new InvalidOperationException(
                "The NDI runtime could not be initialized on this device (unsupported CPU or native library failure).");

        lock (_sendLock)
        {
            var namePtr = Marshal.StringToHGlobalAnsi(streamName);
            try
            {
                var create = new NdiSendCreateNative
                {
                    p_ndi_name = namePtr,
                    p_groups = IntPtr.Zero,
                    // The capture callback paces the stream — never let the SDK clock
                    // block the capture thread to wall-clock frame times.
                    clock_video = false,
                    clock_audio = false,
                };
                _send = NdiNativeMethods.NDIlib_send_create(ref create);
                NdiConnectionMetadata.Apply(_send, isSender: true, sessionName: "output");
            }
            finally
            {
                // The SDK copies the name string during create — safe to free now.
                Marshal.FreeHGlobal(namePtr);
            }

            if (_send == IntPtr.Zero)
            {
                _runtime.ReleaseHandle();
                throw new InvalidOperationException($"Failed to create the NDI sender '{streamName}'.");
            }
        }

        _micRequested = captureMicrophone;
        _videoSource.FrameReady += OnFrameReady;
        if (captureMicrophone)
            _audioSource.ChunkReady += OnAudioChunkReady;

        try
        {
            // For Screen this includes the MediaProjection consent flow; the task
            // faults with OperationCanceledException when the user declines.
            await _videoSource.StartAsync(inputKind, cancellationToken).ConfigureAwait(false);

            if (captureMicrophone)
                await _audioSource.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Permission declined or capture failure — tear the sender down and rethrow
            // so the caller can surface the reason (OperationCanceledException = declined).
            await StopOutputAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        _statusTimer = new Timer(PollSenderStatus, null, StatusPollInterval, StatusPollInterval);
    }

    public async Task StopOutputAsync(CancellationToken cancellationToken = default)
    {
        // Unsubscribe first so no new frames arrive while tearing down.
        _videoSource.FrameReady -= OnFrameReady;
        _audioSource.ChunkReady -= OnAudioChunkReady;

        var statusTimer = Interlocked.Exchange(ref _statusTimer, null);
        statusTimer?.Dispose();

        try
        {
            await _videoSource.StopAsync().ConfigureAwait(false);
        }
        catch
        {
            // Stop must always reach the sender teardown below.
        }

        if (_micRequested)
        {
            _micRequested = false;
            try
            {
                await _audioSource.StopAsync().ConfigureAwait(false);
            }
            catch
            {
                // Same: never skip native teardown.
            }
        }

        lock (_sendLock)
        {
            if (_send != IntPtr.Zero)
            {
                NdiNativeMethods.NDIlib_send_destroy(_send);
                _send = IntPtr.Zero;
                _runtime.ReleaseHandle();
            }
        }

        var statusChanged = _isOnProgramTally || _connectionCount != 0;
        _isOnProgramTally = false;
        _connectionCount = 0;
        if (statusChanged)
            RaiseOutputStatusChanged();
    }

    /// <summary>
    /// Runs on the capture thread. The producer owns <see cref="CapturedVideoFrame.Data"/>
    /// and reuses it after this handler returns, so the frame MUST be consumed with the
    /// synchronous send before returning.
    /// </summary>
    private void OnFrameReady(object? sender, CapturedVideoFrame frame)
    {
        try
        {
            if (frame.Width <= 0 || frame.Height <= 0 || frame.Data.Length == 0)
                return;

            var handle = GCHandle.Alloc(frame.Data, GCHandleType.Pinned);
            try
            {
                var native = new NdiVideoFrameV2Native
                {
                    xres = frame.Width,
                    yres = frame.Height,
                    FourCC = MapFourCC(frame.Format),
                    frame_rate_N = frame.FrameRateN,
                    frame_rate_D = frame.FrameRateD,
                    picture_aspect_ratio = frame.Width / (float)frame.Height,
                    frame_format_type = (int)NdiFrameFormatType.Progressive,
                    timecode = NdiVideoFrameV2Native.TimecodeSynthesize,
                    p_data = handle.AddrOfPinnedObject(),
                    line_stride_in_bytes = frame.StrideBytes,
                    p_metadata = IntPtr.Zero,
                };

                lock (_sendLock)
                {
                    if (_send == IntPtr.Zero)
                        return;

                    // Synchronous by design — see class remarks (producer-owned buffer).
                    NdiNativeMethods.NDIlib_send_send_video_v2(_send, ref native);
                }
            }
            finally
            {
                handle.Free();
            }
        }
        catch
        {
            // A capture callback must never take down the process.
        }
    }

    /// <summary>Runs on the audio capture thread — same consume-before-return rule as video.</summary>
    private void OnAudioChunkReady(object? sender, CapturedAudioChunk chunk)
    {
        try
        {
            if (chunk.FrameCount <= 0 || chunk.Channels <= 0 || chunk.InterleavedSamples.Length == 0)
                return;

            var handle = GCHandle.Alloc(chunk.InterleavedSamples, GCHandleType.Pinned);
            try
            {
                var native = new NdiAudioFrameInterleaved32fNative
                {
                    sample_rate = chunk.SampleRate,
                    no_channels = chunk.Channels,
                    no_samples = chunk.FrameCount,
                    timecode = NdiVideoFrameV2Native.TimecodeSynthesize,
                    p_data = handle.AddrOfPinnedObject(),
                };

                lock (_sendLock)
                {
                    if (_send == IntPtr.Zero)
                        return;

                    NdiNativeMethods.NDIlib_util_send_send_audio_interleaved_32f(_send, ref native);
                }
            }
            finally
            {
                handle.Free();
            }
        }
        catch
        {
            // A capture callback must never take down the process.
        }
    }

    /// <summary>1 s timer callback — polls tally + connection count while output is active.</summary>
    private void PollSenderStatus(object? state)
    {
        try
        {
            bool onProgram;
            int connections;

            lock (_sendLock)
            {
                if (_send == IntPtr.Zero)
                    return;

                var tally = default(NdiTallyNative);
                NdiNativeMethods.NDIlib_send_get_tally(_send, ref tally, 0);
                onProgram = tally.on_program;
                connections = NdiNativeMethods.NDIlib_send_get_no_connections(_send, 0);
            }

            var changed = onProgram != _isOnProgramTally || connections != _connectionCount;
            _isOnProgramTally = onProgram;
            _connectionCount = connections;

            if (changed)
                RaiseOutputStatusChanged();
        }
        catch
        {
            // Timer callbacks must never take down the process.
        }
    }

    private void RaiseOutputStatusChanged()
    {
        try
        {
            OutputStatusChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Subscriber failures must not break the bridge.
        }
    }

    // ── Re-stream ────────────────────────────────────────────────────────────

    public async Task StartReStreamFromSourceAsync(
        string sourceId,
        QualityProfile qualityProfile,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            throw new ArgumentException("Source id is required.", nameof(sourceId));

        // Full clean stop of any previous re-stream before starting a new one.
        await StopReStreamAsync(CancellationToken.None).ConfigureAwait(false);

        if (!_runtime.EnsureInitialized())
            throw new InvalidOperationException(
                "The NDI runtime could not be initialized on this device (unsupported CPU or native library failure).");

        // ONE runtime handle covers the recv + send pair; released in StopReStreamAsync.
        lock (_reStreamLock)
        {
            IntPtr recv;
            var recvNamePtr = Marshal.StringToHGlobalAnsi("NDI for Android Re-stream");
            var sourceIdPtr = Marshal.StringToHGlobalAnsi(sourceId);
            try
            {
                // Same single-id convention as NdiViewerBridge: host:port → url address,
                // anything else → mDNS NDI name.
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

                recv = NdiNativeMethods.NDIlib_recv_create_v3(ref create);
                if (recv == IntPtr.Zero)
                {
                    _runtime.ReleaseHandle();
                    throw new InvalidOperationException($"Failed to create the NDI receiver for '{sourceId}'.");
                }

                var source = create.source_to_connect_to;
                NdiNativeMethods.NDIlib_recv_connect(recv, ref source);
            }
            finally
            {
                // The SDK copies the create/connect strings — safe to free now.
                Marshal.FreeHGlobal(recvNamePtr);
                Marshal.FreeHGlobal(sourceIdPtr);
            }

            IntPtr send;
            var sendNamePtr = Marshal.StringToHGlobalAnsi($"Re-stream of {sourceId}");
            try
            {
                var sendCreate = new NdiSendCreateNative
                {
                    p_ndi_name = sendNamePtr,
                    p_groups = IntPtr.Zero,
                    // The incoming stream paces us — forward frames as they arrive.
                    clock_video = false,
                    clock_audio = false,
                };
                send = NdiNativeMethods.NDIlib_send_create(ref sendCreate);
                NdiConnectionMetadata.Apply(send, isSender: true, sessionName: "re-stream");
            }
            finally
            {
                Marshal.FreeHGlobal(sendNamePtr);
            }

            if (send == IntPtr.Zero)
            {
                NdiNativeMethods.NDIlib_recv_destroy(recv);
                _runtime.ReleaseHandle();
                throw new InvalidOperationException($"Failed to create the NDI re-stream sender for '{sourceId}'.");
            }

            _reStreamRecv = recv;
            _reStreamSend = send;
            _reStreamRunning = true;
            _reStreamThread = new Thread(() => ReStreamPumpLoop(recv, send))
            {
                IsBackground = true,
                Name = "ndi-restream-pump",
            };
            _reStreamThread.Start();
        }
    }

    public async Task StopReStreamAsync(CancellationToken cancellationToken = default)
    {
        Thread? pumpThread;

        lock (_reStreamLock)
        {
            _reStreamRunning = false;
            pumpThread = _reStreamThread;
            _reStreamThread = null;
        }

        // Join OUTSIDE the lock (viewer-bridge deadlock lesson); skip self-join in
        // case a caller ends up on the pump thread.
        if (pumpThread is not null && !ReferenceEquals(pumpThread, Thread.CurrentThread))
            await Task.Run(pumpThread.Join, CancellationToken.None).ConfigureAwait(false);

        lock (_reStreamLock)
        {
            var hadHandles = _reStreamRecv != IntPtr.Zero || _reStreamSend != IntPtr.Zero;

            if (_reStreamRecv != IntPtr.Zero)
            {
                NdiNativeMethods.NDIlib_recv_destroy(_reStreamRecv);
                _reStreamRecv = IntPtr.Zero;
            }

            if (_reStreamSend != IntPtr.Zero)
            {
                NdiNativeMethods.NDIlib_send_destroy(_reStreamSend);
                _reStreamSend = IntPtr.Zero;
            }

            // One EnsureInitialized handle was taken for the recv + send pair.
            if (hadHandles)
                _runtime.ReleaseHandle();
        }
    }

    /// <summary>
    /// Dedicated pump: video-only capture from the re-stream receiver, forwarded
    /// zero-copy into the re-stream sender. The recv-owned buffer stays valid until
    /// NDIlib_recv_free_video_v2, which is called AFTER the synchronous send returns.
    /// </summary>
    private void ReStreamPumpLoop(IntPtr recv, IntPtr send)
    {
        try
        {
            while (_reStreamRunning)
            {
                var video = default(NdiVideoFrameV2Native);
                var metadata = default(NdiMetadataFrameNative);

                var frameType = NdiNativeMethods.NDIlib_recv_capture_v3(
                    recv, ref video, IntPtr.Zero, ref metadata, ReStreamCaptureTimeoutMs);

                switch (frameType)
                {
                    case NdiFrameType.Video:
                        try
                        {
                            // Forward the SAME native buffer — no managed copy.
                            NdiNativeMethods.NDIlib_send_send_video_v2(send, ref video);
                        }
                        finally
                        {
                            // Mandatory per frame — leaking = native OOM within seconds.
                            NdiNativeMethods.NDIlib_recv_free_video_v2(recv, ref video);
                        }
                        break;

                    case NdiFrameType.Metadata:
                        // Not forwarded — free immediately.
                        NdiNativeMethods.NDIlib_recv_free_metadata(recv, ref metadata);
                        break;
                }
            }
        }
        catch
        {
            // A pump thread must never take down the process (unhandled exceptions
            // on background threads are fatal in .NET).
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Synchronous full teardown — safe here: no continuation depends on the caller context.
        StopOutputAsync().GetAwaiter().GetResult();
        StopReStreamAsync().GetAwaiter().GetResult();
    }

    private static uint MapFourCC(CapturedPixelFormat format) => format switch
    {
        CapturedPixelFormat.Rgba32 => NdiFourCC.RGBA,
        CapturedPixelFormat.Bgra32 => NdiFourCC.BGRA,
        CapturedPixelFormat.Nv12 => NdiFourCC.NV12,
        _ => NdiFourCC.RGBA,
    };

    /// <summary>Same mapping as the viewer bridge: Smooth uses the SDK low-bandwidth stream.</summary>
    private static NdiRecvBandwidth MapBandwidth(QualityProfile profile) => profile switch
    {
        QualityProfile.Smooth => NdiRecvBandwidth.Lowest,
        _ => NdiRecvBandwidth.Highest,
    };

    /// <summary>
    /// Heuristic shared with the viewer bridge: "host:port" (no spaces/parentheses,
    /// numeric port suffix) is an NDI url address; anything else is an mDNS NDI name.
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
}
