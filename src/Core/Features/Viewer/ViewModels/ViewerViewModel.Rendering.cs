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

    private long _lastLatencyTraceTicks;

    /// <summary>
    /// Raised on the UI thread when a newly arrived frame is ready to draw (#416). The View's render
    /// plumbing subscribes; nothing else may. Coalesced, so a UI thread that falls behind the source
    /// frame rate drops intermediate frames instead of queueing them — the same newest-frame-wins
    /// contract the bridge's front/back buffer already has.
    /// </summary>
    public event EventHandler? FrameReady;

    /// <summary>
    /// Video pump thread. Stays O(1) and allocation-free: it runs up to 60 times a second on the
    /// thread a stop joins, so anything more expensive here lengthens every teardown (#408).
    /// </summary>
    private void OnBridgeVideoFrameReady(object? sender, EventArgs e)
    {
        // IsPlaying is written on the UI thread and read here unsynchronised on purpose. A bool read
        // cannot tear; a stale read costs one extra post or one missed one, and the View's fallback
        // timer covers a missed one. The gate matters because two ViewerViewModels can be alive at
        // once (the pushed ViewerPage and the Expanded pane) and both are subscribed to this
        // singleton bridge.
        if (!IsPlaying)
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

        var nowTicks = Environment.TickCount64;
        if (nowTicks - _lastLatencyTraceTicks < LatencyTraceIntervalMs)
            return;
        _lastLatencyTraceTicks = nowTicks;

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
