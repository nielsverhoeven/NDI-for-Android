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
    // NDIlib_recv_capture_v3 returns the instant a frame is queued, so a 60 fps source never
    // reaches this timeout; it only bounds how long a pump idles (and therefore a queued lifecycle
    // stop's thread join) on a silent receiver.
    private const uint VideoCaptureTimeoutMs = 250;
    private const uint AudioCaptureTimeoutMs = 250;
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

    /// <summary>Guards <see cref="_lifecycleTail"/> only. Never held across any native call.</summary>
    private readonly object _lifecycleLock = new();

    /// <summary>
    /// Tail of the receiver-lifecycle chain. Every start and stop appends a continuation here, so
    /// the native teardown/create pairs execute one at a time and in request order, on the thread
    /// pool, never on the caller's thread. A task chain rather than a <see cref="SemaphoreSlim"/>:
    /// SemaphoreSlim does not document FIFO release order for async waiters, and ordering is the
    /// invariant this exists to provide — <c>_recv</c> is a single field, <see cref="NdiRuntime"/>'s
    /// handle refcount is process-wide, and the audio sink is a singleton, so a stop that lands
    /// after a start destroys the wrong receiver.
    /// </summary>
    private Task _lifecycleTail = Task.CompletedTask;

    private IntPtr _recv;
    private Thread? _videoThread;
    private Thread? _audioThread;
    private volatile bool _running;

    private string? _activeSourceId;
    private QualityProfile _qualityProfile = QualityProfile.Balanced;
    private ConnectionState _connectionState = ConnectionState.Disconnected;
    private ReceiverStopReason _lastStopReason = ReceiverStopReason.Intentional;

    /// <summary>Non-zero while a caller-requested <see cref="StopReceiverAsync"/> is tearing the
    /// receiver down. Every Disconnected transition raised inside that window is intentional by
    /// definition — including one the video pump raises from its own connection-lost check between
    /// <c>_running = false</c> and the thread join. A depth counter, not a flag: an event handler
    /// running on a pump thread may itself call <see cref="StopReceiverAsync"/>, enqueuing another
    /// stop while this one is still tearing down.</summary>
    private int _stopDepth;

    /// <summary>Backs <see cref="ReceiverGeneration"/>. Guarded by <see cref="_connectionLock"/> and
    /// deliberately not by <see cref="_stateLock"/>: the state lock is held across the native
    /// create/connect calls, and this must stay readable from the UI thread without blocking on them.</summary>
    private long _receiverGeneration;

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
    // Largest inter-frame gap (ms) since the last stats tick. Written and reset only on
    // the video pump thread; surfaced once per second via the developer-mode logcat line.
    private long _maxFrameGapMs;
    private volatile bool _isPtzSupported;
    private volatile bool _audioEnabled = true;

    private bool _tallyOnProgram;
    private bool _tallyOnPreview;
    private NdiTallyEcho? _lastTallyEcho;

    // volatile: written on the disposing thread (the DI container, UI thread on Android) and read
    // on the lifecycle worker by StartReceiverCore's early-out.
    private volatile bool _disposed;

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

    /// <inheritdoc />
    public void StartReceiver(string sourceId, QualityProfile qualityProfile = QualityProfile.Balanced)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            throw new ArgumentException("Source id is required.", nameof(sourceId));

        // Before the _stopDepth increment, not after: a guard placed later would leak the depth
        // counter, because nothing would be enqueued to run StopReceiverCore's balancing finally.
        if (_disposed)
            return;

        lock (_connectionLock)
        {
            // Ownership token: bumped in the caller's turn — before anything is queued, before the
            // transition below and before any failure path — so a caller that reads
            // ReceiverGeneration straight after this call reads its own generation, and a second
            // ViewModel taking the bridge over always disowns the first, even for the same source
            // id. Also what makes the Disconnected raised below harmless to a ViewModel mid
            // quality-profile change: it is not the owner for the duration of that one statement.
            _receiverGeneration++;

            // The implicit stop of the outgoing receiver, signalled and tagged exactly as
            // RequestStop does. StopReceiverCore's finally decrements this.
            _stopDepth++;
        }

        // See RequestStop: volatile, and deliberately not under _stateLock.
        _running = false;

        // Published synchronously so a SetQualityProfile that lands while this start sits in the
        // queue is not lost. SetQualityProfile's own restart is gated on _recv, which is zero for
        // the whole queue window, so without this the user's pick is silently dropped and the
        // receiver is created at the previous bandwidth tier (#408). StartReceiverCore reads this
        // field rather than its captured argument, so the last writer wins and the two are
        // serialized by this lock. This is the one prologue statement that can briefly block on a
        // concurrently executing StartReceiverCore's native create; see acceptedResiduals.
        lock (_stateLock) _qualityProfile = qualityProfile;

        // Same reason as the stop's blank: a source switch must never paint the previous source's
        // last frame as the new source's first. The old synchronous StopReceiver() inside
        // StartReceiver did this too.
        lock (_frameLock)
        {
            _frontPixels = null;
            _frameWidth = 0;
            _frameHeight = 0;
            _frameTimestampMillis = 0;
        }

        // The implicit stop's postcondition, identical to RequestStop's and identical to what the
        // pre-#408 inline StopReceiver() produced here. Tagged Intentional (_stopDepth > 0) and
        // raised with no lock held.
        TransitionState(ConnectionState.Disconnected);

        // One queued item, so no other lifecycle operation can interleave between the teardown of
        // the old receiver and the creation of the new one.
        EnqueueLifecycle(() =>
        {
            StopReceiverCore();
            StartReceiverCore(sourceId, qualityProfile);
        });
    }

    /// <summary>
    /// The native half of a start. Lifecycle worker only; the previous receiver is already fully
    /// torn down when this runs.
    /// </summary>
    private void StartReceiverCore(string sourceId, QualityProfile qualityProfile)
    {
        // Queued starts become no-ops once Dispose has run. Without this, Dispose's wait is bounded
        // by the whole backlog — up to a dozen items during an active reconnect loop, each with two
        // 250 ms joins — instead of by the item in flight, and a start that slipped through would
        // create a receiver nothing will ever destroy.
        if (_disposed)
            return;

        lock (_stateLock)
        {
            // Read, do not write. The requested profile was published by StartReceiver's prologue
            // and may since have been superseded by a SetQualityProfile that landed while this item
            // waited in the queue; the captured argument is stale in exactly that case (#408).
            qualityProfile = _qualityProfile;

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
                NdiConnectionMetadata.Apply(_recv, isSender: false, sessionName: "viewer");
            }
            catch
            {
                // EnsureInitialized bumped NdiRuntime's process-wide handle refcount. That count is
                // shared with the discovery and output bridges and gates NDIlib_destroy, so leaking
                // one entry permanently blocks a library re-init (a discovery-server change) —
                // round-1 ruling 2(b) is what makes that load-bearing. Before the lifecycle queue a
                // throw here propagated to the UI-thread caller and was at least visible; the chain
                // now swallows it, so the release has to be explicit.
                // Scoped to the create/connect region ON PURPOSE: no pump thread has been started
                // yet at this point. It must NEVER be widened past the two Thread.Start() calls
                // below — destroying the handle under a running pump is a native use-after-free,
                // which is a worse bug than the one being fixed (rule 6).
                if (_recv != IntPtr.Zero)
                {
                    NdiNativeMethods.NDIlib_recv_destroy(_recv);
                    _recv = IntPtr.Zero;
                }

                _runtime.ReleaseHandle();
                _activeSourceId = null;
                _running = false;
                TransitionState(ConnectionState.Disconnected);
                throw; // EnqueueLifecycle logs it (RC20)
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

            // A stop landed while this create ran: do not start the pumps and do not reopen
            // CopyVideoFrame's publish gate. The stop queued behind this item owns the teardown
            // and destroys _recv; leaving _running false keeps the prologue's blank true.
            bool stopPendingAfterCreate;
            lock (_connectionLock) stopPendingAfterCreate = _stopDepth > 0;
            if (stopPendingAfterCreate)
                return;

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

    /// <inheritdoc />
    public Task StopReceiverAsync()
    {
        // After Dispose the chain must not accept new work: Dispose returns once the chain has
        // drained, so an item appended afterwards would create or destroy state that nothing will
        // ever clean up again.
        if (_disposed)
            return Task.CompletedTask;

        return RequestStop();
    }

    /// <summary>
    /// A stop's synchronous prologue plus the enqueue of its native half. Split out from
    /// <see cref="StopReceiverAsync"/> so <see cref="Dispose"/> can still run a stop *after* it has
    /// set <see cref="_disposed"/> — the public entry point deliberately refuses to enqueue then.
    /// </summary>
    private Task RequestStop()
    {
        // Synchronous prologue, in the caller's turn. FOUR things must be true the instant this
        // returns, because callers order other work against them:
        //  - every Disconnected raised from here until the teardown completes is tagged
        //    Intentional, so no reconnect window opens on a stop the app requested;
        //  - the pumps are unwinding;
        //  - GetLatestFrame() returns null, so no consumer — the View's 33 ms render timer, a
        //    frame-ready signal already in flight, or a *second* ViewerViewModel's pane, whose own
        //    IsStopped is false — can paint one more frame of a stream the app has just ended
        //    (#348 / Nielsen #1);
        //  - GetConnectionState() is Disconnected, which is the postcondition
        //    ViewerViewModel.CompleteReconnect's "the bridge agrees right now" guard,
        //    RunAttempt's pre-check and CheckForSustainedConnecting's correctness argument were
        //    all written against.
        // Only the *native* half (thread joins, recv_destroy, the runtime handle) is queued. The
        // _stopDepth decrement lives in StopReceiverCore's finally, so the Intentional tag spans
        // the thread hop.
        lock (_connectionLock) _stopDepth++;

        // Deliberately NOT under _stateLock. StartReceiverCore holds that lock across
        // NDIlib_recv_create_v3 + NDIlib_recv_connect, so taking it here would put a native call
        // of unbounded duration on the UI thread inside the one method whose entire purpose is to
        // keep native work off it (#408). No lock is needed: _running is volatile, and every
        // interleaving with a concurrently executing StartReceiverCore is safe because the stop
        // this prologue enqueues is ordered *behind* that start on the lifecycle chain and is the
        // authority that joins the pumps and destroys the handle.
        _running = false;

        // Blanking the front buffer HERE, not on the worker, is what makes "the pumps are
        // unwinding" observable to every consumer at once. The pump publishes under this same lock
        // and re-reads _running inside it (CopyVideoFrame), and the write above happens before
        // this lock is taken, so there is no interleaving in which a publish survives this blank:
        // either the pump took the lock first and this blank runs after its swap, or it takes the
        // lock afterwards and sees _running == false.
        // _backPixels is deliberately left alone — it is pump-owned and CopyVideoFrame copies into
        // it through the field with no lock held, so nulling it here would fault a pump mid-copy.
        // StopReceiverCore nulls it after the joins, when no pump exists.
        lock (_frameLock)
        {
            _frontPixels = null;
            _frameWidth = 0;
            _frameHeight = 0;
            _frameTimestampMillis = 0;
        }

        // Restores the pre-#408 postcondition: the old synchronous StopReceiver() had already set
        // Disconnected before it returned. Raised on the caller's thread — which is also what the
        // synchronous stop did — and with no lock held, so a subscriber that re-enters the bridge
        // can neither deadlock nor observe a half-applied stop. _stopDepth was incremented first,
        // so this is tagged Intentional by construction.
        TransitionState(ConnectionState.Disconnected);

        return EnqueueLifecycle(StopReceiverCore);
    }

    /// <summary>
    /// The native half of a stop. Runs only on the lifecycle worker, never on the caller's thread
    /// and never on a pump thread. Idempotent: a second queued stop finds null thread fields and a
    /// zero handle and does nothing.
    /// </summary>
    private void StopReceiverCore()
    {
        try
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

            // Join OUTSIDE the state lock (legacy deadlock lesson). Unbounded on purpose: a timed
            // join followed by NDIlib_recv_destroy while a pump is inside NDIlib_recv_capture_v3
            // is a native use-after-free. Bounded in practice by VideoCaptureTimeoutMs /
            // AudioCaptureTimeoutMs. The self-join guard is retained as defence: this method is
            // only ever reached from the thread pool, so it can no longer trigger.
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

            // Idempotent after the prologue's blank; _backPixels can only be released here,
            // because only here is it certain no pump thread is copying into it.
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
        finally
        {
            // After TransitionState, exactly as before the split: the Disconnected raised above is
            // still inside the intentional-stop window.
            lock (_connectionLock) _stopDepth--;
        }
    }

    public void SetQualityProfile(QualityProfile profile)
    {
        string? restartSourceId = null;

        lock (_stateLock)
        {
            var bandwidthChanged = MapBandwidth(_qualityProfile) != MapBandwidth(profile);
            _qualityProfile = profile;

            // Bandwidth is a create-time setting on the receiver — applying a new
            // tier requires recreating it with the same source. Restart only when a receiver
            // both exists and is still wanted: while a stop is queued but not yet run, _recv and
            // _activeSourceId still name the outgoing receiver, so restarting from them would
            // reconnect to the previous source after a switch, or resurrect a receiver the app
            // just stopped. Nothing is lost by skipping it: the profile is published above and
            // the queued StartReceiverCore reads that field.
            bool stopPending;
            lock (_connectionLock) stopPending = _stopDepth > 0;

            if (_recv != IntPtr.Zero && bandwidthChanged && !stopPending)
                restartSourceId = _activeSourceId;
        }

        if (restartSourceId is not null)
        {
            // StartReceiver is queued now, but it bumps ReceiverGeneration synchronously, so a
            // caller's conditional re-claim of ownership still observes the bump in its own turn.
            StartReceiver(restartSourceId, profile);
        }
    }

    public ConnectionState GetConnectionState()
    {
        lock (_connectionLock) return _connectionState;
    }

    public ReceiverStopReason GetLastStopReason()
    {
        lock (_connectionLock) return _lastStopReason;
    }

    /// <inheritdoc />
    public long ReceiverGeneration
    {
        get { lock (_connectionLock) return _receiverGeneration; }
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
    // destroyed mid-call; StopReceiverCore only destroys under the same lock.

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

        // The one place that blocks: app shutdown must not leave a pump thread reading managed
        // state that is about to go away. Never call Dispose from the UI thread during normal
        // operation, and never from a bridge event handler — the queued teardown joins the pump
        // threads, so a pump-thread caller would deadlock on itself.
        // Bounded by the item in flight plus one join pair, NOT by the length of the backlog:
        // _disposed is already set, so every queued start is now a no-op (StartReceiverCore's
        // early-out) and every queued stop behind it finds null thread fields and a zero handle.
        // During an active reconnect loop the backlog can be a dozen items, which without that
        // early-out would be seconds on the UI thread at shutdown.
        // RequestStop, not StopReceiverAsync: the public entry point refuses to enqueue once
        // _disposed is set.
        RequestStop().GetAwaiter().GetResult();
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

                        // Single comparison only — no logging/allocation in the frame path.
                        if (hasEverConnected && now - lastVideoTicks > _maxFrameGapMs)
                            _maxFrameGapMs = now - lastVideoTicks;

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
                            // Still connected, just not sending video: Stalled, not Connecting.
                            // Connecting must mean "a receiver was created and has not delivered a
                            // frame yet" and nothing else — that is what lets ViewerViewModel treat
                            // sustained Connecting as a drop that was superseded by a restart
                            // (ViewerViewModel.ConnectionHint.CheckForSustainedConnecting).
                            TransitionState(ConnectionState.Stalled);
                        }

                        if (connectionLost)
                            TransitionState(ConnectionState.Disconnected, ReceiverStopReason.ConnectionLost);
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
            TransitionState(ConnectionState.Disconnected, ReceiverStopReason.ConnectionLost);
        }
    }

    private void CopyVideoFrame(ref NdiVideoFrameV2Native video)
    {
        var width = video.xres;
        var height = video.yres;
        if (width <= 0 || height <= 0 || video.p_data == IntPtr.Zero)
            return;

        const int MaxDimension = 8192;
        if (width > MaxDimension || height > MaxDimension || (long)width * height > int.MaxValue)
            return; // drop malformed/oversized frame; caller still frees it

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
            // Publish gate. A stop's prologue sets _running = false and then blanks the front
            // buffer under this lock; without this check a pump that was already inside
            // NDIlib_recv_capture_v3 at that moment swaps one more live frame back in *after* the
            // blank — and because nothing invalidates the canvas again once the buffers are gone,
            // that frame stays frozen on screen under the "Stopped" badge (#348, #408). One
            // republished frame is enough to reproduce the whole defect.
            // Read inside the lock, against a write that happens before the prologue takes it, so
            // the only two orders are publish-then-blank (blank wins) and blank-then-skip.
            // The already-completed copy into _backPixels is wasted: one frame, on a receiver that
            // is going away.
            if (!_running)
                return;

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

            // Developer mode only (persisted Settings toggle): one logcat line per second so
            // soak tests can measure fps / drops / frame gaps with `adb logcat -s NdiStats`.
            // Runs on the pump thread — must never throw into VideoPumpLoop, whose catch
            // would report the failure as a lost stream.
            if (_diagnostics.IsDeveloperMode)
            {
                try
                {
                    Android.Util.Log.Debug("NdiStats",
                        $"src=\"{sourceId}\" fps={_measuredFps:0} drop={_droppedFramePercent:0.00}% " +
                        $"total={total.video_frames} dropped={dropped.video_frames} " +
                        $"maxGapMs={_maxFrameGapMs} res={width}x{height} profile={_qualityProfile}");
                }
                catch
                {
                    // Logging is best-effort.
                }
            }
        }

        _maxFrameGapMs = 0;
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

    private void TransitionState(ConnectionState newState, ReceiverStopReason stopReason = ReceiverStopReason.Intentional)
    {
        bool changed;
        lock (_connectionLock)
        {
            // A stop has been requested and its queued teardown has not run yet (#408). A pump that
            // is still unwinding must not *promote* the state of a receiver that is about to be
            // destroyed: its loop condition is only re-read at the top, so a frame already queued
            // natively is still delivered after _running = false, and ViewerViewModel reads this
            // field to decide whether a Connected event is real (CompleteReconnect's third guard,
            // RunAttempt's pre-check). Before the lifecycle queue the stop's own Disconnected had
            // already landed by the time any such event could reach the UI thread.
            // Disconnected is always allowed through, so the stop's own transition and a pump's
            // connection-lost demotion both still apply — the latter re-tagged Intentional below,
            // exactly as before.
            if (_stopDepth > 0 && newState != ConnectionState.Disconnected)
                return;

            changed = _connectionState != newState;
            _connectionState = newState;
            _lastStopReason = _stopDepth > 0 ? ReceiverStopReason.Intentional : stopReason;
        }

        if (!changed)
            return;

        // Developer mode only: state transitions to logcat (rare, never per-frame).
        if (_diagnostics?.IsDeveloperMode == true)
        {
            try
            {
                Android.Util.Log.Debug("NDI-Bridge", $"Viewer connection state -> {newState}");
            }
            catch
            {
                // Logging is best-effort.
            }
        }

        // A subscriber's exception must never break the receiver lifecycle. This is raised from
        // three kinds of thread now: a pump thread (where the pump's catch would misreport the
        // fault as a lost stream), the lifecycle worker (where it would abort the create queued
        // behind a teardown in the same item, leaving the viewer at "Connecting..." forever with
        // nothing on screen to say so), and a caller's own turn (Stop/Start/handoff/Dispose).
        try
        {
            ConnectionStateChanged?.Invoke(this, newState);
        }
        catch
        {
            // A subscriber's fault is the subscriber's problem; the bridge's own state is already
            // committed above.
        }
    }

    /// <summary>
    /// Appends one lifecycle operation to the chain. The continuation swallows everything: a fault
    /// here must never break the chain for every later operation, and the pump-safety rule (a
    /// background-thread exception is fatal in .NET) applies to this worker too.
    /// Note for the next reader: <see cref="TaskContinuationOptions.RunContinuationsAsynchronously"/>
    /// is not what keeps <paramref name="work"/> off the caller's thread — it governs the
    /// *created* task's own continuations. What keeps the work off the caller's thread is the
    /// absence of <c>ExecuteSynchronously</c> together with <see cref="TaskScheduler.Default"/>,
    /// which queues even when the antecedent is already completed. Do not "simplify" either away.
    /// </summary>
    private Task EnqueueLifecycle(Action work)
    {
        lock (_lifecycleLock)
        {
            var next = _lifecycleTail.ContinueWith(
                _ =>
                {
                    try
                    {
                        work();
                    }
                    catch (Exception ex)
                    {
                        // Never fault the chain — every later start and stop would inherit the
                        // fault — and never kill the process, since an unhandled exception on a
                        // background thread is fatal in .NET. But never silently either: a fault
                        // here can leave the viewer at "Connecting..." forever, and RC18's
                        // non-throwing event raise removes the only realistic thrower without
                        // making the rest diagnosable. Not gated on developer mode: this is an
                        // error, not a probe, and it can happen at most once per lifecycle
                        // operation.
                        try
                        {
                            Android.Util.Log.Error("NDI-Bridge", $"Lifecycle operation faulted: {ex}");
                        }
                        catch
                        {
                            // Logging is best-effort.
                        }
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.RunContinuationsAsynchronously,
                TaskScheduler.Default);

            _lifecycleTail = next;
            return next;
        }
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
