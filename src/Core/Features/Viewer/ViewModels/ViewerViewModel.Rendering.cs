using System.Threading;
using NdiForAndroid.Features.DiagOverlay.Services;

namespace NdiForAndroid.Features.Viewer.ViewModels;

public partial class ViewerViewModel
{
    /// <summary>At most one latency line per second, matching NdiViewerBridge.StatsIntervalMs so
    /// the NDI-Lat and NdiStats streams line up one-to-one in a single logcat capture.</summary>
    private const long LatencyTraceIntervalMs = 1000;

    /// <summary>0 = no invalidate posted, 1 = one already queued. Written from the NDI video pump
    /// thread and cleared on the UI thread — Interlocked only, never a plain assignment.</summary>
    private int _framePostPending;

    // Written on the UI thread by SetRenderingActive, read on the pump thread — volatile, not locked.
    private volatile bool _isRenderingActive;

    // Wall-clock millisecond mark of the last traced latency line, on the injected TimeProvider.
    // Nullable so the first call always traces without a magic-zero sentinel.
    private long? _lastLatencyTraceMillis;

    /// <summary>
    /// Raised on the UI thread when a newly arrived frame is ready to draw (#416). The View's render
    /// plumbing subscribes; nothing else may. Coalesced, so a UI thread that falls behind the source
    /// frame rate drops intermediate frames instead of queueing them — the same newest-frame-wins
    /// contract the bridge's front/back buffer already has.
    /// </summary>
    public event EventHandler? FrameReady;

    /// <summary>Set by the View's StartRendering/StopRendering/Teardown; the View is the only writer.</summary>
    public void SetRenderingActive(bool active) => _isRenderingActive = active;

    // Video pump thread, up to 60/s. Not allocation-free: the closure below allocates a delegate
    // per post and the Android dispatcher an additional JNI Runnable.
    private void OnBridgeVideoFrameReady(object? sender, EventArgs e)
    {
        if (!IsPlaying || !_isRenderingActive)
            return;

        // Newest-frame-wins: at most one post is queued at any time. Without this a 60 fps source on
        // a busy UI thread queues an unbounded backlog of invalidates, each of which then paints a
        // frame that is already stale.
        if (Interlocked.Exchange(ref _framePostPending, 1) == 1)
            return;

        _dispatcher.BeginInvokeOnMainThread(() =>
        {
            // Cleared before the raise, so a frame arriving during the paint queues the next post
            // rather than being dropped. Bounds the in-flight count at two: one running, one queued.
            Interlocked.Exchange(ref _framePostPending, 0);
            FrameReady?.Invoke(this, EventArgs.Empty);
        });
    }

    /// <summary>
    /// Called by the View's paint handler once per painted frame, on the UI thread (#416). Cost when
    /// developer mode is off: one volatile bool read. When it is on: one TickCount64 read and, at
    /// most once a second, one formatted logcat line under NDI-Lat. Nothing here allocates per frame
    /// and nothing hops threads — the paint handler is already on the UI thread, so the value is
    /// produced and consumed in the same turn, which is why there is no per-frame dispatch anywhere
    /// in this path.
    /// </summary>
    public void ReportFrameDrawn(long receivedAtTickMillis, long capturedAtEpochMillis, bool timestampIsSynthesized)
    {
        if (_diagnostics?.IsDeveloperMode != true)
            return;

        // The throttle is a cadence and uses the injected TimeProvider (deterministic under
        // FakeTimeProvider); the measurement below is a duration and stays on Environment.TickCount64,
        // the clock the pump stamped the frame with. Do not merge these into one clock.
        var nowMillis = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        if (_lastLatencyTraceMillis is { } lastTraceMillis
            && nowMillis - lastTraceMillis < LatencyTraceIntervalMs)
            return;
        _lastLatencyTraceMillis = nowMillis;

        var nowTicks = Environment.TickCount64;

        // Single clock, no assumption about the sender: the only latency this app may state without
        // a caveat. It spans pump copy -> buffer swap -> coalesced UI post -> paint, i.e. exactly the
        // part of glass-to-glass the app owns. It does NOT include the Android display pipeline
        // (compositor + panel, typically another 2-3 frames) — only the stopwatch method sees that.
        var recvToDrawMs = receivedAtTickMillis > 0 ? nowTicks - receivedAtTickMillis : -1;

        // Sender -> draw. Meaningful only when the sender stamped the frame AND both clocks are
        // NTP-synced. Reported as -1 rather than as a plausible-looking number when the timestamp
        // was synthesized from the receiver's own clock.
        var senderToDrawMs = timestampIsSynthesized
            ? -1
            : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - capturedAtEpochMillis;

        _diagnostics.Trace(
            DiagnosticOverlayService.LatencyLogTag,
            "viewer.latency",
            $"recvToDrawMs={recvToDrawMs} senderToDrawMs={senderToDrawMs} synth={timestampIsSynthesized} " +
            $"fps={_bridge.GetMeasuredFps():0} profile={QualityProfile}");
    }
}
