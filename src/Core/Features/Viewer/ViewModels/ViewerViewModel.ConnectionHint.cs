using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using NdiForAndroid.Features.DiagOverlay.Services;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Services;

namespace NdiForAndroid.Features.Viewer.ViewModels;

public partial class ViewerViewModel
{
    /// <summary>The bridge refreshes fps / dropped % once per second (NdiViewerBridge.StatsIntervalMs), so sample at the same rate.</summary>
    private static readonly TimeSpan StatsSampleInterval = TimeSpan.FromSeconds(1);

    private ITimer? _statsTimer;
    private ConnectionHintPolicy.State _hintState = ConnectionHintPolicy.State.Idle;
    private int _sustainedConnectingSamples;

    /// <summary>
    /// "Connection weak — try Smooth" while the bridge reports sustained low fps / high drops;
    /// null otherwise. Purely advisory — nothing here ever calls SetQualityProfile (#331).
    /// </summary>
    [ObservableProperty]
    private string? _connectionHint;

    private void StartStatsWatchdog()
    {
        ResetConnectionHint();
        if (_statsTimer is null)
            _statsTimer = _timeProvider.CreateTimer(_ => SampleConnectionStats(), null, StatsSampleInterval, StatsSampleInterval);
        else
            _statsTimer.Change(StatsSampleInterval, StatsSampleInterval);
    }

    private void StopStatsWatchdog()
    {
        _statsTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        // The counter is otherwise reset only by CheckForSustainedConnecting's else-branch, which
        // cannot run while the timer is parked. Without this it would still be four samples deep
        // when playback resumes, and the first sample after the next StartReceiver — Connecting is
        // the normal state for about a second after every start — would open a spurious window.
        _sustainedConnectingSamples = 0;
        // Forget the last traced link as well, so a stop/start on the same band and classification
        // writes a fresh "Link" entry for the new attempt instead of nothing at all. An operator who
        // restarts playback to reproduce a problem must see the radio line for *that* attempt.
        _lastTracedLink = null;
        ResetConnectionHint();
    }

    /// <summary>Timer callback (thread-pool thread): read the bridge's lock-free counters and the
    /// device's link here, mutate observable state on the UI thread.</summary>
    private void SampleConnectionStats()
    {
        try
        {
            var state = _bridge.GetConnectionState();

            // Stalled counts as "the receiver is up": it is the state a starved link spends most of
            // its time in, and treating it as not-connected would both wipe an active hint and reset
            // the five-sample run on every demotion. This is not a claim that frames are arriving —
            // the fps term says that, and on a stall it reads 0.
            var connected = state is ConnectionState.Connected or ConnectionState.Stalled;
            var fps = _bridge.GetMeasuredFps();
            var dropPercent = _bridge.GetDroppedFramePercent();

            // The only place the link is read: a thread-pool thread at 1 Hz. Never the UI thread
            // (this is a binder round-trip) and never a pump thread.
            var link = _networkLink?.GetSnapshot() ?? NetworkLinkSnapshot.Unknown;

            _dispatcher.BeginInvokeOnMainThread(() => ApplyConnectionSample(connected, fps, dropPercent, link));
        }
        catch
        {
            // A timer callback must never take down the process; a missed sample is harmless.
        }
    }

    /// <summary>Applies one 1 s stats sample with no link information. Kept so existing hosts and
    /// tests can push samples directly. Main thread only.</summary>
    public void ApplyConnectionSample(bool connected, float fps, float dropPercent)
        => ApplyConnectionSample(connected, fps, dropPercent, NetworkLinkSnapshot.Unknown);

    /// <summary>
    /// Applies one 1 s stats sample together with the link it was taken on. Main thread only.
    /// <paramref name="connected"/> means "the receiver is up" — Connected or Stalled — not "frames
    /// are arriving".
    /// <paramref name="link"/> only ever changes the hint's wording, and only when the weak run
    /// actually lost frames: nothing here calls SetQualityProfile, and a receiver that is up while
    /// the source sends nothing is never attributed to the radio.
    /// </summary>
    public void ApplyConnectionSample(bool connected, float fps, float dropPercent, NetworkLinkSnapshot link)
    {
        ReleaseIfDisowned();
        CheckForSustainedConnecting();

        // Not a judgement about the link while (re)connecting or when the receiver is down (also
        // keeps the hint off the x86 emulator, where the bridge never reports Connected).
        if (!IsPlaying || IsReconnecting || !connected)
        {
            ResetConnectionHint();
            return;
        }

        TraceLink(link);

        _hintState = ConnectionHintPolicy.Next(_hintState, fps, dropPercent);
        ConnectionHint = ConnectionHintPolicy.HintText(
            _hintState.IsHintActive, QualityProfile, link, _hintState.SawDropEvidence);
    }

    /// <summary>
    /// Level-triggered backstop for the edge-triggered drop signal. A <c>Disconnected(ConnectionLost)</c>
    /// transition can be superseded before the UI thread reads it: any restart — a quality-profile
    /// bandwidth change, a source switch, the resume restore, the attempt loop's own final attempt —
    /// re-stamps the bridge to Connecting/Intentional, and the fresh receiver's connection-lost check
    /// can never fire because it has never been connected (<c>hasEverConnected</c> in the video pump).
    /// The receiver then sits in Connecting forever while this ViewModel still claims to be playing.
    /// Because the bridge reports a live-but-silent source as <see cref="ConnectionState.Stalled"/>,
    /// <see cref="ConnectionState.Connecting"/> means exactly one thing — "a receiver was created and
    /// has not delivered a frame yet" — so sustained Connecting on a ViewModel that has been
    /// Connected since its own Start, still owns the receiver, and is not already in a window, is
    /// the superseded-drop state and nothing else. A video stall does not reach it (Stalled is not
    /// Connecting); an intentional stop leaves the bridge <see cref="ConnectionState.Disconnected"/>,
    /// so a navigation handoff can never trip this; a source that never connected at all leaves
    /// <c>_hasConnectedSinceStart</c> false, so the initial-connect experience is unchanged (#413);
    /// and a ViewModel the bridge has been taken from fails the ownership term.
    /// </summary>
    private void CheckForSustainedConnecting()
    {
        if (IsPlaying
            && !IsReconnecting
            && !_userInitiatedStop
            && _hasConnectedSinceStart
            && _reconnectState == ReconnectState.Idle
            && OwnsActiveReceiver
            && _bridge.GetConnectionState() == ConnectionState.Connecting)
        {
            if (++_sustainedConnectingSamples >= ReconnectConstants.SustainedConnectingSamples)
            {
                _sustainedConnectingSamples = 0;
                BeginReconnectWindow();
            }

            return;
        }

        _sustainedConnectingSamples = 0;
    }

    /// <summary>
    /// The ownership token's dual, checked once a second on the existing stats watchdog. A
    /// ViewModel is disowned the instant another one asks the bridge for a receiver
    /// (<see cref="INdiViewerBridge.ReceiverGeneration"/>). From then on it may not drive the
    /// bridge — but until this runs it still *claims* to play: it paints the new owner's frames
    /// through <see cref="CurrentFrame"/> under its own <see cref="SourceId"/>, and every drop path
    /// rejects it on ownership, so it can never open a reconnect window again. That is exactly the
    /// #348 / Nielsen-#1 dead end this feature exists to remove, reached through the feature's own
    /// guard. Level-triggered on purpose: there is no single edge to hook — a pushed ViewerPage
    /// being popped, a deep link to another source, and another ViewModel's quality-profile restart
    /// all produce it — and the watchdog already ticks on every playing ViewModel.
    /// Never stops the receiver: it belongs to another ViewModel now. Leaves exactly the
    /// surface a user Stop leaves, so there is always a status line and one control (Reconnect),
    /// and records no connection-history event: this stream did not end, it changed hands.
    /// The View stops painting as soon as <see cref="ViewerViewModel.IsStopped"/> is set
    /// (ViewerView.OnRenderTick), so this pane does not keep showing the new owner's video.
    /// Note this does NOT cover the navigation handoff (#410) —
    /// <see cref="INdiViewerBridge.StopReceiverAsync"/> does not bump the token, so a pane whose
    /// bridge was stopped behind its back still owns it.
    /// </summary>
    private void ReleaseIfDisowned()
    {
        if (!IsPlaying || !HasEverClaimedReceiver || OwnsActiveReceiver)
            return;

        DisposeTimers();
        _reconnectState = ReconnectState.Idle;
        // Sticky, exactly as CancelRetry: a Disconnected raised by the new owner must not open a
        // window here. Start() and BeginReconnectWindow() clear it when the user comes back.
        _userInitiatedStop = true;
        IsReconnecting = false;
        IsPlaying = false; // before BeginExitFullScreen — RC5's compact auto-re-enter gate
        IsStopped = true;
        IsTallyProgram = false;
        IsPtzSupported = false;
        StopPtz();
        BeginExitFullScreen();
        CanReconnect = true;
        RetryRemainingSeconds = ReconnectConstants.RetryWindowSeconds;
        RetryStatusMessage = null;
        // Same copy as Stop(): from the user's point of view this pane stopped playing. Naming the
        // cause is a copy change that belongs with the badge wording (#412), not here.
        StatusMessage = "Stopped.";
    }

    private void ResetConnectionHint()
    {
        _hintState = ConnectionHintPolicy.State.Idle;
        ConnectionHint = null;
    }

    private NetworkLinkSnapshot? _lastTracedLink;

    /// <summary>
    /// Two outputs, deliberately different in cadence. (1) One developer-mode logcat line per stats
    /// sample under <see cref="DiagnosticOverlayService.LinkLogTag"/> — sink only, never the log
    /// buffer. (2) One in-app Diagnostic Log entry when the band or the weak/not-weak classification
    /// changes, so the buffer's 200-entry ring gets a handful of entries per session instead of one
    /// per second.
    /// </summary>
    private void TraceLink(NetworkLinkSnapshot link)
    {
        if (_diagnostics is null || !link.IsWifi)
            return;

        var changed = _lastTracedLink is null
            || _lastTracedLink.Band != link.Band
            || ConnectionHintPolicy.IsWeakLink(_lastTracedLink) != ConnectionHintPolicy.IsWeakLink(link);
        _lastTracedLink = link;

        if (changed)
            _diagnostics.LogBuffer.Add("Link", FormatLink(link));

        if (_diagnostics.IsDeveloperMode)
        {
            // weak= is the *measured* radio classification (RSSI / PHY rate) — not the band, and not
            // the hint. A 2.4 GHz link at -45 dBm reads weak=False here and still gets the band named
            // in the copy when frames are being lost.
            _diagnostics.Trace(
                DiagnosticOverlayService.LinkLogTag,
                "viewer.link",
                $"band={link.Band} rssi={link.Rssi} speedMbps={link.LinkSpeedMbps} " +
                $"std={link.Standard} weak={ConnectionHintPolicy.IsWeakLink(link)}");
        }
    }

    /// <summary>
    /// One line for the in-app Diagnostic Log, e.g. <c>Wi-Fi 2.4 GHz, -78 dBm, 6 Mbit/s</c>.
    /// Comma-separated on purpose: DiagnosticLogBuffer redacts anything matching its IPv6 pattern, so
    /// a "key:value" form here would be mangled.
    /// </summary>
    internal static string FormatLink(NetworkLinkSnapshot link)
    {
        var rssi = link.Rssi == NetworkLinkSnapshot.UnknownRssi ? "? dBm" : $"{link.Rssi} dBm";
        var speed = link.LinkSpeedMbps == NetworkLinkSnapshot.UnknownLinkSpeed
            ? "? Mbit/s"
            : $"{link.LinkSpeedMbps} Mbit/s";
        return $"Wi-Fi {ConnectionHintPolicy.LinkDescription(link)}, {rssi}, {speed}";
    }
}
