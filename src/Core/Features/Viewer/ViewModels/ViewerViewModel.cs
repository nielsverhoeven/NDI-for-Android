using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiForAndroid.Features.AppState.Models;
using NdiForAndroid.Features.AppState.Repositories;
using NdiForAndroid.Features.ConnectionHistory.Services;
using NdiForAndroid.Features.DiagOverlay.Services;
using NdiForAndroid.Features.Viewer.Models;
using NdiForAndroid.Features.Ptz.Services;
using NdiForAndroid.Features.Ptz.ViewModels;
using NdiForAndroid.Features.Sources.Models;
using NdiForAndroid.Features.Sources.Repositories;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Services;

namespace NdiForAndroid.Features.Viewer.ViewModels;

/// <summary>Retry window constants for the automatic reconnection feature.</summary>
internal static class ReconnectConstants
{
    public const int RetryWindowSeconds = 15;
    public const int RetryAttemptIntervalSeconds = 2;
    public const int CountdownTickIntervalSeconds = 1;

    /// <summary>How many consecutive 1 s stats samples may report a bridge stuck in
    /// <see cref="ConnectionState.Connecting"/> before the level-triggered backstop opens a retry
    /// window. Five is a *latency* budget, not a stall budget: a receiver that has not produced its
    /// first frame five seconds after it was created is not going to. The unit is the stats sample,
    /// not the bridge's capture timeout — <c>NdiViewerBridge.VideoCaptureTimeoutMs</c> is 250 ms, so
    /// five samples are roughly twenty capture cycles. A source that is connected but silent reports
    /// <see cref="ConnectionState.Stalled"/> and never reaches this counter.</summary>
    public const int SustainedConnectingSamples = 5;

    /// <summary>How many consecutive 1 s stats samples an initial connect may spend with the bridge
    /// in <see cref="ConnectionState.Connecting"/> — a receiver exists and has not delivered a frame —
    /// before it ends in the "Could not connect to the source." failure state (#413). Fifteen, so an
    /// initial connect gets the same budget as a reconnect window. A bridge that reports
    /// <see cref="ConnectionState.Disconnected"/> instead has no receiver trying at all (no NDI runtime,
    /// as on the x86 CI emulator, or a receiver stopped behind the ViewModel's back) and never
    /// counts.</summary>
    public const int InitialConnectTimeoutSamples = 15;
}

public partial class ViewerViewModel : ObservableObject, IDisposable
{
    private const string TerminalMessage = "Connection lost. Reconnection failed.";
    private const int PtzNudgeDurationMs = 250;
    private const float PtzNudgeSpeed = 0.5f;

    private readonly INdiViewerBridge _bridge;
    private readonly TimeProvider _timeProvider;
    private readonly IMainThreadDispatcher _dispatcher;
    private readonly IAppStateRepository _appStateRepo;
    private readonly IAppLifecycleService _lifecycle;
    private readonly ISourceRepository _sourceRepository;
    private readonly IConnectionHistoryService _connectionHistory;
    private readonly IPtzControllerFactory _ptzControllerFactory;
    private readonly IImmersiveModeService _immersiveMode;
    private readonly IScreenReaderAnnouncer _announcer;
    private readonly IOrientationLockService _orientationLock;
    private readonly INetworkLinkService? _networkLink;
    private readonly IDiagnosticOverlayService? _diagnostics;

    [ObservableProperty]
    private string? _sourceId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QualityProfileLabel))]
    private bool _isPlaying;

    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>
    /// True after the user taps Stop, until playback starts again (#348). Drives the View's
    /// "Stopped" treatment of the video surface — the presenter only repaints on a new frame
    /// timestamp (draw-on-arrival plus the 33 ms fallback pull) while reading
    /// <see cref="CurrentFrame"/>, so without this the last frame stays painted forever with
    /// nothing on screen to say playback ended.
    /// </summary>
    [ObservableProperty]
    private bool _isStopped;

    /// <summary>
    /// What the video-surface badge says while <see cref="IsStopped"/> (#412): each path that ends
    /// playback names itself instead of every one of them reading "Stopped". A user Stop and a
    /// viewer another ViewModel took the bridge from say "Stopped" (their status line says
    /// "Stopped."), an expired retry window says "Connection lost", a cancelled one "Reconnect
    /// cancelled", an initial connect that never succeeded "Could not connect" (#413). Only rendered
    /// while IsStopped, so it is set just before IsStopped goes true and never needs resetting.
    /// </summary>
    [ObservableProperty]
    private string _videoSurfaceBadgeText = "Stopped";

    // Tally / PTZ / audio state surfaced from the bridge
    [ObservableProperty]
    private bool _isTallyProgram;

    [ObservableProperty]
    private bool _isPtzSupported;

    [ObservableProperty]
    private bool _isAudioEnabled;

    /// <summary>
    /// Latest decoded frame from the bridge. Read by the View's presenter on every frame-arrival
    /// (<see cref="FrameReady"/>) and on every 33 ms fallback tick — pushed *and* polled since #416.
    /// No change notification: the frame's timestamp is the dedupe key, and a per-frame
    /// PropertyChanged would defeat the coalescing.
    /// </summary>
    public NdiVideoFrame? CurrentFrame => _bridge.GetLatestFrame();

    // Reconnect state
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFullScreenRetryVisible))]
    private bool _isReconnecting;

    [ObservableProperty]
    private int _retryRemainingSeconds;

    [ObservableProperty]
    private string? _retryStatusMessage;

    [ObservableProperty]
    private bool _canReconnect;

    private ITimer? _countdownTimer;
    private ITimer? _attemptTimer;
    private volatile bool _userInitiatedStop;

    /// <summary>Incremented (Interlocked) wherever playback is ended with _userInitiatedStop set —
    /// Stop, CancelRetry, RetireDisowned and Dispose. <see cref="BeginReconnectWindow"/> reads it
    /// when a window is requested and drops the window if it has moved by the time the posted body
    /// runs, so a window queued behind one of those ends can never reopen after it (#409).</summary>
    private int _stopEpoch;

    /// <summary>True once the bridge has reported Connected at least once since this ViewModel last
    /// called Start(). Arms the level-triggered drop backstop: a receiver that has never connected
    /// is an initial-connect attempt, not a drop, and must keep today's behaviour.</summary>
    private bool _hasConnectedSinceStart;

    private string? _lastSourceId;

    /// <summary>Generation of the receiver this ViewModel last asked the bridge to start
    /// (<see cref="INdiViewerBridge.ReceiverGeneration"/>). The bridge is a singleton and more than
    /// one ViewerViewModel is alive at once — the Expanded two-pane <c>PaneViewer</c> is never
    /// disposed and a pushed ViewerPage gets its own transient instance — so "the bridge reported a
    /// drop" is not on its own a statement about *this* ViewModel's stream. -1 = never started one.</summary>
    private long _receiverGeneration = -1;

    /// <summary>Whether the open reconnect window has held the receiver: this ViewModel owned it
    /// when the window opened, or one of the window's own attempts re-claimed it. Set when a window
    /// opens and after each attempt; read only by <see cref="RunAttempt"/> and
    /// <see cref="FailReconnect"/>, which never act outside a window, so it needs no reset. Losing
    /// ownership after the window has held the receiver means another ViewModel took the bridge
    /// mid-countdown — the user started a stream elsewhere — so the window retires rather than steal
    /// it back or report a failed reconnect. A window that opened without the receiver
    /// (Reconnect on a pane that had already lost it, #423) has not held it yet, so its first
    /// attempt makes a real claim.</summary>
    private bool _windowHasHeldReceiver;

    // Quality profile selection — manual only (#330/#331: no automatic degradation; the
    // stats watchdog in ViewerViewModel.ConnectionHint.cs only feeds ConnectionHint).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QualityProfileLabel))]
    [NotifyPropertyChangedFor(nameof(QualityProfileShortLabel))]
    [NotifyPropertyChangedFor(nameof(NextQualityProfile))]
    [NotifyPropertyChangedFor(nameof(QualityProfileCycleDescription))]
    private QualityProfile _qualityProfile = QualityProfile.Balanced;

    /// <summary>Every enum value, in declaration order — the button strip is generated from this.</summary>
    public IReadOnlyList<QualityProfileOption> AvailableProfiles { get; } =
        Enum.GetValues<QualityProfile>().Select(p => new QualityProfileOption(p)).ToArray();

    /// <summary>User-visible label next to the status line; null (collapsed) while not playing.</summary>
    public string? QualityProfileLabel => IsPlaying ? $"Quality: {QualityProfile}" : null;

    /// <summary>One-letter label for the full-screen toolbar button (S / B / H).</summary>
    public string QualityProfileShortLabel => QualityProfile.ToString()[..1];

    /// <summary>Profile the full-screen cycle button switches to (wraps around AvailableProfiles).</summary>
    public QualityProfile NextQualityProfile
    {
        get
        {
            var index = -1;
            for (var i = 0; i < AvailableProfiles.Count; i++)
                if (AvailableProfiles[i].Profile == QualityProfile) { index = i; break; }
            return AvailableProfiles[(index + 1) % AvailableProfiles.Count].Profile;
        }
    }

    public string QualityProfileCycleDescription => $"Quality {QualityProfile}. Activate for {NextQualityProfile}.";

    // State machine
    private enum ReconnectState { Idle, InWindow, Attempting, Failed }
    private ReconnectState _reconnectState = ReconnectState.Idle;

    public ViewerViewModel(
        INdiViewerBridge bridge,
        TimeProvider timeProvider,
        IMainThreadDispatcher dispatcher,
        IAppStateRepository appStateRepo,
        IAppLifecycleService lifecycle,
        ISourceRepository sourceRepository,
        IConnectionHistoryService connectionHistory,
        IPtzControllerFactory ptzControllerFactory,
        PtzEndpointFormViewModel ptzEndpointForm,
        IImmersiveModeService immersiveMode,
        IScreenReaderAnnouncer announcer,
        IOrientationLockService orientationLock,
        // Optional with a default so no existing test's construction changes (fixtures call this
        // constructor positionally). Both are registered singletons, so ActivatorUtilities fills
        // them in the app.
        INetworkLinkService? networkLink = null,
        IDiagnosticOverlayService? diagnostics = null)
    {
        _bridge = bridge;
        _timeProvider = timeProvider;
        _dispatcher = dispatcher;
        _appStateRepo = appStateRepo;
        _lifecycle = lifecycle;
        _sourceRepository = sourceRepository;
        _connectionHistory = connectionHistory;
        _ptzControllerFactory = ptzControllerFactory;
        PtzEndpointForm = ptzEndpointForm;
        _immersiveMode = immersiveMode;
        _announcer = announcer;
        _orientationLock = orientationLock;
        _networkLink = networkLink;
        _diagnostics = diagnostics;
        RetryRemainingSeconds = ReconnectConstants.RetryWindowSeconds;
        StatusMessage = "Select a source on Home to start viewing.";
        _isAudioEnabled = bridge.IsAudioEnabled; // backing field: don't push the default back to the bridge
        SyncSelectedProfileOption(); // field initialisers do not fire OnQualityProfileChanged

        _lifecycle.AppPaused += OnAppPaused;
        _lifecycle.OrientationChanged += OnOrientationChanged;
        _bridge.ConnectionStateChanged += OnBridgeConnectionStateChanged;
        _bridge.TallyEchoChanged += OnBridgeTallyEchoChanged;
        _bridge.VideoFrameReady += OnBridgeVideoFrameReady;
        PtzEndpointForm.EndpointSaved += OnPtzEndpointSaved;
    }

    /// <summary>PTZ lifecycle hooks implemented in ViewerViewModel.Ptz.cs; declared here so this
    /// file compiles standalone even if that partial were ever removed.</summary>
    partial void StartPtz(NdiSource? source);
    partial void StopPtz();
    partial void DisposePtz();

    /// <summary>Forwards the user's audio toggle to the active bridge connection.</summary>
    partial void OnIsAudioEnabledChanged(bool value)
    {
        NotifyControlInteraction();
        _bridge.IsAudioEnabled = value;
    }

    private void OnBridgeConnectionStateChanged(object? sender, ConnectionState state)
    {
        _dispatcher.BeginInvokeOnMainThread(() =>
        {
            // PTZ support is only known once connection metadata has arrived.
            IsPtzSupported = _bridge.IsPtzSupported;

            // Arms the level-triggered drop backstop (see CheckForSustainedConnecting).
            if (state == ConnectionState.Connected)
                _hasConnectedSinceStart = true;

            // Minimal status refresh; drop handling stays with the reconnect state machine.
            // Ownership-gated: this event reaches every subscribed ViewModel, and one the bridge has
            // been taken from must not narrate another ViewModel's connection under its own
            // SourceId. ReleaseIfDisowned reconciles it within a second, but it must not lie in the
            // meantime — with a deep link to a different source that is one source's video labelled
            // with another source's status.
            if (IsPlaying && !IsReconnecting && OwnsActiveReceiver && state == ConnectionState.Connected)
                StatusMessage = "Connected.";

            // A connected source that has sent no video for 3 s (#414): the bridge demotes
            // Connected → Stalled and promotes back on the next frame, which the branch above turns
            // into "Connected." again. Same gate as that branch. Status text only — no restart and
            // no reconnect window, because recreating a receiver cannot make a sender send video.
            if (IsPlaying && !IsReconnecting && OwnsActiveReceiver && state == ConnectionState.Stalled)
                StatusMessage = "No video from source — still connected.";

            // Only the bridge can observe a real (re)connection: StartReceiver returns while the
            // receiver is still Connecting, so the attempt loop's own poll right after it never
            // sees Connected.
            if (state == ConnectionState.Connected && IsReconnecting)
                CompleteReconnect();

            if (state == ConnectionState.Disconnected)
                CheckForUnexpectedDrop();
        });
    }

    private void OnBridgeTallyEchoChanged(object? sender, NdiTallyEcho echo)
    {
        _dispatcher.BeginInvokeOnMainThread(() => IsTallyProgram = echo.OnProgram);
    }

    partial void OnIsPlayingChanged(bool value)
    {
        if (value)
        {
            IsStopped = false; // a fresh Start/Reconnect clears the previous Stop's badge (#348)
            StartStatsWatchdog();
        }
        else
        {
            StopStatsWatchdog();
        }
    }

    /// <summary>What TalkBack speaks when the source starts echoing program tally (#350).</summary>
    public const string TallyOnProgramAnnouncement = "On program";

    /// <summary>What TalkBack speaks when program tally clears while still playing (#350).</summary>
    public const string TallyOffProgramAnnouncement = "Off program";

    /// <summary>
    /// Redundant non-visual tally cue (WCAG 1.4.1, #350). Runs on the UI thread because
    /// <see cref="OnBridgeTallyEchoChanged"/> marshals through <see cref="IMainThreadDispatcher"/>
    /// before assigning <see cref="IsTallyProgram"/>. Gated on <see cref="IsPlaying"/>: Stop()
    /// clears IsPlaying before IsTallyProgram, so the user's own stop never triggers an
    /// "Off program" announcement.
    /// </summary>
    partial void OnIsTallyProgramChanged(bool value)
    {
        if (!IsPlaying)
            return;

        _announcer.Announce(value ? TallyOnProgramAnnouncement : TallyOffProgramAnnouncement);
    }

    partial void OnSourceIdChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value))
            StartCommand.Execute(null);
    }

    // ----- Commands -------------------------------------------------------

    [RelayCommand]
    private async Task Start()
    {
        if (string.IsNullOrEmpty(SourceId))
        {
            StatusMessage = "Select a source on Home to start viewing.";
            return;
        }

        _userInitiatedStop = false;
        _hasConnectedSinceStart = false; // a new receiver has not connected yet (see CheckForSustainedConnecting)
        _initialConnectSamples = 0; // and gets a full initial-connect budget, even mid-connect (#413)
        _lastSourceId = SourceId;
        CanReconnect = false; // clears the "watch again" affordance left over from a previous Stop (#348)

        // Restore quality profile for this source from cached sources
        try
        {
            var sources = await _sourceRepository.GetCachedSourcesAsync();
            var source = sources.FirstOrDefault(s => s.SourceId == SourceId);
            if (source != null)
            {
                QualityProfile = source.QualityProfile;
            }
        }
        catch { /* Silent fail – will default to Balanced */ }

        // Persist last active viewer source for resume recovery (non-critical housekeeping)
        var existingState = await _appStateRepo.RestoreStateAsync();
        _appStateRepo.SaveAsync(new AppStateSnapshot(SourceId, existingState.StreamName, existingState.IsOutputActive, existingState.LastSelectedSourceId)).FireAndForget();

        // Record connection event for history tracking (try to resolve display name from cache)
        string displayName = SourceId;
        NdiSource? cached = null;
        try
        {
            var cachedSources = await _sourceRepository.GetCachedSourcesAsync();
            cached = cachedSources.FirstOrDefault(s => s.SourceId == SourceId);
            if (!string.IsNullOrEmpty(cached?.DisplayName))
                displayName = cached.DisplayName;
        }
        catch { /* Silent fail – will fall back to sourceId */ }

        _connectionHistory.RecordConnectedAsync(SourceId, displayName, QualityProfile).FireAndForget();
        StartPtz(cached);

        _bridge.StartReceiver(SourceId, QualityProfile);
        _receiverGeneration = _bridge.ReceiverGeneration;
        _bridge.SetTally(onProgram: true, onPreview: false);
        IsPlaying = true;
        StatusMessage = "Connecting...";
    }

    [RelayCommand]
    private void Stop()
    {
        _userInitiatedStop = true;
        Interlocked.Increment(ref _stopEpoch); // cancels a window already posted (#409)
        DisposeTimers();
        _reconnectState = ReconnectState.Idle;
        _bridge.SetTally(onProgram: false, onPreview: false);
        _bridge.StopReceiverAsync().FireAndForget();
        BeginExitFullScreen();

        // Record disconnection for history tracking
        _connectionHistory.RecordDisconnectedAsync().FireAndForget();

        IsPlaying = false;
        IsReconnecting = false;
        // A visible way back in, so the screen is never left with neither status text nor a
        // control (#348, Nielsen #1) — the same Reconnect action a failed auto-retry offers.
        CanReconnect = true;
        IsTallyProgram = false;
        IsPtzSupported = false;
        VideoSurfaceBadgeText = "Stopped";
        IsStopped = true;
        StopPtz();
        RetryStatusMessage = null;
        StatusMessage = "Stopped.";
        RetryRemainingSeconds = ReconnectConstants.RetryWindowSeconds;
        RetryStatusMessage = null;
    }

    public void Dispose()
    {
        DisposeTimers();
        _statsTimer?.Dispose();
        _overlayAutoHideTimer?.Dispose();
        _immersiveMode.KeepScreenOn(false);
        _userInitiatedStop = true;
        Interlocked.Increment(ref _stopEpoch); // no window posted before this may open (#409)
        _lifecycle.AppPaused -= OnAppPaused;
        _lifecycle.OrientationChanged -= OnOrientationChanged;
        _bridge.ConnectionStateChanged -= OnBridgeConnectionStateChanged;
        _bridge.TallyEchoChanged -= OnBridgeTallyEchoChanged;
        _bridge.VideoFrameReady -= OnBridgeVideoFrameReady;

        // The ownership token's dual: at most one ViewModel may drive the shared bridge, so the one
        // that owns the receiver must hand it back when it goes away. ViewerPage disposes this
        // ViewModel once its page has left the nav stack (ViewerPage.xaml.cs:52-57) but never stops
        // the bridge, and the navigation handoff only fires on a primary-destination change — so
        // without this, Back out of a pushed viewer leaves a receiver pumping video *and audio*
        // with no ViewModel driving it, and any other live ViewerViewModel (the Expanded PaneViewer
        // is never disposed) is left permanently disowned: painting frames it does not own under
        // "Connected.", unable to ever auto-reconnect. Ownership-gated, so disposing a background
        // ViewModel can never stop the foreground one's stream. Placed after the unsubscribes so
        // this dying ViewModel does not re-enter its own handler, while every other subscriber
        // still sees the (Intentional) Disconnected.
        if (OwnsActiveReceiver)
        {
            _bridge.StopReceiverAsync().FireAndForget();

            // The session ends here, so close its connection-history row as Stop() does (#418).
            // Inside the ownership gate: the history service is a singleton with one active row,
            // and a ViewModel the bridge was taken from would close the owner's.
            _connectionHistory.RecordDisconnectedAsync().FireAndForget();
        }

        ForceExitFullScreen();
        DisposePtz();
    }

    private void DisposeTimers()
    {
        _countdownTimer?.Dispose();
        _countdownTimer = null;
        _attemptTimer?.Dispose();
        _attemptTimer = null;
    }

    // --- FR1/FR2: Drop detection + BeginReconnectWindow ---

    /// <summary>
    /// Call from a background callback (e.g. frame watchdog, connection lost) to begin the retry window.
    /// </summary>
    public void BeginReconnectWindow()
    {
        // This is a thread boundary, so the body below can run a later UI-thread turn — behind a
        // Stop the user had already queued (#409). Re-reading _userInitiatedStop there cannot tell
        // that Stop apart from the one Reconnect() legitimately opens a window after, so compare the
        // stop epoch instead: only an end of playback *between* this request and its body cancels it.
        var stopEpochAtRequest = Volatile.Read(ref _stopEpoch);
        _dispatcher.BeginInvokeOnMainThread(() =>
        {
            if (_reconnectState != ReconnectState.Idle || IsReconnecting)
                return; // Already in a reconnect window.

            if (Volatile.Read(ref _stopEpoch) != stopEpochAtRequest)
                return; // Stopped, cancelled, disowned or disposed since the window was requested.

            _reconnectState = ReconnectState.InWindow;
            _windowHasHeldReceiver = OwnsActiveReceiver; // see RunAttempt
            _userInitiatedStop = false;
            IsReconnecting = true;
            IsStopped = false; // a retry is not a stopped stream: never show "Stopped" and a countdown together
            RetryRemainingSeconds = ReconnectConstants.RetryWindowSeconds;
            RetryStatusMessage = $"Reconnecting... {RetryRemainingSeconds}s remaining";
            StartCountdown();
            StartAttemptTimer();
        });
    }

    /// <summary>True while the receiver this ViewModel started is still the one the shared bridge is
    /// running. False for any ViewerViewModel another instance has taken the bridge from; such a
    /// ViewModel must never open or complete a reconnect window nor narrate the bridge's state as its
    /// own (the drop trigger, the sustained-Connecting backstop, CompleteReconnect, the attempt loop's
    /// already-Connected shortcut and the "Connected." status are gated on this), nor stop a receiver
    /// that is no longer its own (Dispose, FailReconnect and CancelRetry are gated too), or two
    /// reconnect loops fight over one receiver and the user sees one source's video labelled with
    /// another source's status. User-initiated Stop is deliberately not gated, and neither is an
    /// attempt by a window that has not held the receiver yet: both act on this ViewModel's own
    /// session, and such an attempt claims the bridge. Once a window has held the receiver, losing
    /// it retires the window instead of attempting (RunAttempt), and ReleaseIfDisowned retires a
    /// disowned, still-playing ViewModel within one stats sample.</summary>
    private bool OwnsActiveReceiver => _bridge.ReceiverGeneration == _receiverGeneration;

    /// <summary>True once this ViewModel has asked the bridge for a receiver at least once. A
    /// ViewModel that never did was never an owner and therefore cannot be *dis*owned — without
    /// this term the level check in <see cref="ReleaseIfDisowned"/> would fire on any ViewModel
    /// whose <see cref="IsPlaying"/> was set without a Start(), which is how much of the unit suite
    /// constructs a "playing" viewer.</summary>
    private bool HasEverClaimedReceiver => _receiverGeneration >= 0;

    /// <summary>
    /// Call from a background callback to check if connection was unexpectedly dropped.
    /// </summary>
    public void CheckForUnexpectedDrop()
    {
        var connState = _bridge.GetConnectionState();
        if (connState == ConnectionState.Disconnected
            && _bridge.GetLastStopReason() == ReceiverStopReason.ConnectionLost
            && OwnsActiveReceiver
            && IsPlaying && !_userInitiatedStop && _reconnectState == ReconnectState.Idle)
        {
            BeginReconnectWindow();
        }
    }

    // --- FR3: 2s attempt loop ---

    private void StartAttemptTimer()
    {
        var interval = TimeSpan.FromSeconds(ReconnectConstants.RetryAttemptIntervalSeconds);
        _attemptTimer?.Dispose();
        _attemptTimer = _timeProvider.CreateTimer(
            _ => _dispatcher.BeginInvokeOnMainThread(RunAttempt),
            null, interval, interval);
    }

    private void RunAttempt()
    {
        if (_reconnectState != ReconnectState.InWindow && _reconnectState != ReconnectState.Attempting)
            return;

        // Overtaken mid-window (#423): this window held the receiver and another ViewModel has
        // taken the bridge since — the user started a stream elsewhere. An attempt would steal it
        // back and demote that ViewModel instead, so retire exactly as ReleaseIfDisowned would.
        // This tick can beat ReleaseIfDisowned's next stats sample, and that sample never comes for
        // a window Reconnect() opened while not playing. One flag is enough to tell this from the
        // #423 Reconnect-on-a-demoted-pane case below: see _windowHasHeldReceiver.
        if (_windowHasHeldReceiver && !OwnsActiveReceiver)
        {
            RetireDisowned();
            return;
        }

        // The receiver can recover on its own between ticks: the pump keeps running through a
        // ConnectionLost, so a frame may have arrived and moved the bridge to Connected. Tearing it
        // down here would destroy a healthy connection and hand the window a Connecting bridge.
        // Only for a receiver this ViewModel owns (#423): a bridge that is Connected for another
        // ViewModel's receiver is not this window's success — CompleteReconnect refuses a non-owner,
        // so taking this shortcut every tick let the window expire without a single attempt. A
        // window that has not held the receiver yet makes a real attempt instead, which claims it.
        if (OwnsActiveReceiver && _bridge.GetConnectionState() == ConnectionState.Connected)
        {
            CompleteReconnect();
            return;
        }

        _reconnectState = ReconnectState.Attempting;
        RetryStatusMessage = "Attempting reconnect...";

        var generationBeforeAttempt = _bridge.ReceiverGeneration;
        try
        {
            // Both calls only *request* work now; the bridge's lifecycle chain guarantees the stop
            // completes before the start creates, so the pair stays correctly ordered without this
            // method — or the UI thread — waiting for a pump join.
            _bridge.StopReceiverAsync().FireAndForget();

            if (!string.IsNullOrEmpty(SourceId))
                _bridge.StartReceiver(SourceId, QualityProfile);
        }
        catch
        {
            // Attempt failed – fall through to continue the window.
        }

        // Re-claimed outside the try (#419): StartReceiver bumps the token in its prologue, ahead of
        // work that can still fail, so a start that throws may already have replaced the receiver.
        // Recording only on the success path left this ViewModel one generation behind its own
        // receiver, and the next stats sample then retired the live window as disowned
        // (ReleaseIfDisowned). Only a generation that actually moved is recorded: a start rejected
        // before the bump (its argument check) replaced nothing, and recording the current
        // generation then would claim another ViewModel's receiver.
        var generationAfterAttempt = _bridge.ReceiverGeneration;
        if (generationAfterAttempt != generationBeforeAttempt)
        {
            _receiverGeneration = generationAfterAttempt;
            _windowHasHeldReceiver = true; // from here on, losing it means being overtaken
        }

        if (_reconnectState == ReconnectState.Attempting)
            _reconnectState = ReconnectState.InWindow;
    }

    /// <summary>
    /// Ends the current reconnect window as a success. Main-thread only, and deliberately **not**
    /// wrapped in <see cref="IMainThreadDispatcher"/>: both callers — the
    /// <see cref="OnBridgeConnectionStateChanged"/> dispatcher body and <see cref="RunAttempt"/>,
    /// itself posted to the main thread — are already on it. The wrapper used to post this body one
    /// turn later, so a countdown expiry queued *behind* the bridge's Connected event could run
    /// *in front* of it: the window completed, then FailReconnect stopped the healthy,
    /// just-reconnected receiver and wrote "Connection lost. Reconnection failed." over it. Running
    /// in the caller's turn removes that interleaving class instead of guarding one instance of it.
    /// The opposite order — the expiry posted *ahead* of the event — is a genuine race between the
    /// timer and pump threads and is resolved in <see cref="FailReconnect"/> (#409).
    /// </summary>
    private void CompleteReconnect()
    {
        // Defensive, not load-bearing: after RC19 no queued terminal transition can exist, but the
        // invariants still hold — a failed window is terminal, only the owner may declare success,
        // and success is only real if the bridge agrees right now (the event that got us here may
        // be stale because an attempt tick restarted the receiver).
        if (_reconnectState == ReconnectState.Failed)
            return;

        if (!OwnsActiveReceiver)
            return;

        if (_bridge.GetConnectionState() != ConnectionState.Connected)
            return;

        // Success is terminal for this reconnect window; leaving the state machine at Idle
        // (rather than a dedicated "Successful" state) lets the next drop open a new window.
        _reconnectState = ReconnectState.Idle;
        IsReconnecting = false;
        IsPlaying = true;
        StatusMessage = "Connected.";
        RetryRemainingSeconds = 0;
        RetryStatusMessage = null;
        DisposeTimers();
    }

    // --- FR4: 1s countdown ---

    private void StartCountdown()
    {
        var interval = TimeSpan.FromSeconds(ReconnectConstants.CountdownTickIntervalSeconds);
        _countdownTimer?.Dispose();
        _countdownTimer = _timeProvider.CreateTimer(
            _ => _dispatcher.BeginInvokeOnMainThread(TickCountdown),
            null, interval, interval);
    }

    private void TickCountdown()
    {
        if (_reconnectState != ReconnectState.InWindow && _reconnectState != ReconnectState.Attempting)
            return;

        RetryRemainingSeconds--;
        RetryStatusMessage = $"Reconnecting... {RetryRemainingSeconds}s remaining";

        if (RetryRemainingSeconds <= 0)
        {
            FailReconnect();
        }
    }

    // --- FR5: Expiry / fail ---

    /// <summary>
    /// Ends the current reconnect window as a failure — or as a success, if the bridge already
    /// reports this ViewModel's receiver Connected (#409), or retires it, if another ViewModel has
    /// taken a receiver the window held (#423). Main-thread only and, like
    /// <see cref="CompleteReconnect"/>, deliberately not dispatcher-wrapped: its only caller
    /// (<see cref="TickCountdown"/>) already runs on the main thread.
    /// </summary>
    private void FailReconnect()
    {
        // Symmetric with CompleteReconnect's guards and, like them, defensive after RC19 —
        // TickCountdown evaluated the same condition earlier in this same turn. The invariant is
        // what matters: only an open window may be failed, never an Idle or already-completed one.
        if (_reconnectState != ReconnectState.InWindow && _reconnectState != ReconnectState.Attempting)
            return;

        // Overtaken, and the expiry noticed first (#423): the same rule as RunAttempt's retire. The
        // window held the receiver and another ViewModel has taken it since — the connection was not
        // lost, another viewer is playing — so "Connection lost. Reconnection failed." would be
        // untrue. Leave the surface ReleaseIfDisowned and the mid-window retire leave instead.
        if (_windowHasHeldReceiver && !OwnsActiveReceiver)
        {
            RetireDisowned();
            return;
        }

        // Load-bearing, unlike the guard above (#409): the window's last tick (posted by the timer)
        // and the bridge's Connected event (posted by the pump) come from two threads and can reach
        // the UI thread in either order. When the tick wins, the source is already back — the bridge
        // reports Connected for this ViewModel's own receiver, and only the event saying so is still
        // queued. That is a reconnect, not a failure: failing here stopped the receiver that had just
        // connected and wrote the terminal message over it. CompleteReconnect re-checks both terms.
        if (OwnsActiveReceiver && _bridge.GetConnectionState() == ConnectionState.Connected)
        {
            CompleteReconnect();
            return;
        }

        DisposeTimers();
        _reconnectState = ReconnectState.Failed;
        IsReconnecting = false;
        IsPlaying = false;
        // Failed is terminal: never leave a receiver pumping behind the terminal message. The
        // pump survives a ConnectionLost (that is what lets NDI recover on its own), so without
        // this the orphan keeps burning battery and, if the source returns, repaints live video
        // underneath "Connection lost. Reconnection failed." (#348, Nielsen #1). The resulting
        // Disconnected is tagged Intentional by the bridge's stop-depth counter and this method
        // has already left _reconnectState at Failed, so it cannot re-open the window.
        // Ownership-gated (#423): a ViewModel the bridge has been taken from ends its own window,
        // but the receiver now running is another ViewModel's stream and is not its to stop.
        if (OwnsActiveReceiver)
        {
            _bridge.StopReceiverAsync().FireAndForget();

            // Record disconnection for history tracking, as Stop() does (#418) — owner only, for
            // the same reason as Dispose: the history service's single active row is the owner's.
            _connectionHistory.RecordDisconnectedAsync().FireAndForget();
        }
        // Before BeginExitFullScreen: on a compact device the exit only *requests* portrait, so
        // IsFullScreen stays true for up to 3s — the in-video badge is the only thing on screen
        // during that window, which is why it names this outcome rather than saying "Stopped" (#412).
        VideoSurfaceBadgeText = "Connection lost";
        IsStopped = true;
        // Playback has definitively ended, so leave full screen the way Stop() already does.
        // Idempotent; on a compact device in landscape this requests portrait and completes on
        // the resulting orientation change. Doing it here and not when the window opens is what
        // keeps a transient drop from force-rotating the device.
        BeginExitFullScreen();
        StatusMessage = "Connection lost. Reconnection failed.";
        RetryStatusMessage = null;
        CanReconnect = true;
    }

    // --- FR6: Cancel retry ---

    [RelayCommand]
    private void CancelRetry()
    {
        NotifyControlInteraction();
        DisposeTimers();
        _reconnectState = ReconnectState.Idle;
        // The user has opted out of *this* drop. Without this the bridge's next ConnectionLost
        // re-opens, a second later, the window the user just dismissed.
        _userInitiatedStop = true;
        Interlocked.Increment(ref _stopEpoch); // ...and neither may one already posted (#409)
        IsReconnecting = false;
        IsPlaying = false;
        // Same terminal contract as FailReconnect: no receiver left pumping behind a cancelled
        // message, and the video surface says so instead of holding a frozen frame. Ownership-gated
        // like FailReconnect (#423): never stop a receiver another ViewModel is playing.
        if (OwnsActiveReceiver)
        {
            _bridge.StopReceiverAsync().FireAndForget();

            // Record disconnection for history tracking, as Stop() does (#418) — owner only.
            _connectionHistory.RecordDisconnectedAsync().FireAndForget();
        }
        VideoSurfaceBadgeText = "Reconnect cancelled";
        IsStopped = true;
        BeginExitFullScreen();
        // Cancel must not be a dead end: the Reconnect button is the only control still on screen
        // once IsPlaying is false (PlaybackControlsView.xaml:86-87 hides the Stop row).
        CanReconnect = true;
        RetryRemainingSeconds = ReconnectConstants.RetryWindowSeconds;
        RetryStatusMessage = null;
        // RetryStatusMessage is only rendered inside the IsReconnecting stack, so the old
        // assignment was invisible the instant it was made — this belongs on the status line.
        StatusMessage = "Reconnection cancelled.";
    }

    // --- FR7: Reconnect command ---

    [RelayCommand]
    private void Reconnect()
    {
        NotifyControlInteraction();
        if (string.IsNullOrEmpty(SourceId) && string.IsNullOrEmpty(_lastSourceId))
            return;

        SourceId ??= _lastSourceId;
        CanReconnect = false;
        StatusMessage = "Attempting reconnect...";

        // Leaving Failed — BeginReconnectWindow's guard requires Idle to open a new window.
        _reconnectState = ReconnectState.Idle;
        BeginReconnectWindow();
    }

    // --- PTZ (only offered when IsPtzSupported) --- see ViewerViewModel.Ptz.cs

    // --- Quality Profile (manual selection only — #330/#331) ---

    partial void OnQualityProfileChanged(QualityProfile value)
    {
        SyncSelectedProfileOption();
        ResetConnectionHint(); // the new profile gets a fresh evaluation window
    }

    private void SyncSelectedProfileOption()
    {
        foreach (var option in AvailableProfiles)
            option.IsSelected = option.Profile == QualityProfile;
    }

    /// <summary>Accepts a <see cref="QualityProfile"/> (templated buttons), a <see cref="QualityProfileOption"/>, or the enum name as a string (legacy XAML / tests).</summary>
    [RelayCommand]
    private async Task ChangeQualityProfileAsync(object? param)
    {
        NotifyControlInteraction();

        QualityProfile? requested = param switch
        {
            QualityProfile p => p,
            QualityProfileOption o => o.Profile,
            string s when Enum.TryParse<QualityProfile>(s, ignoreCase: true, out var parsed) => parsed,
            _ => null,
        };
        if (requested is not { } profile || QualityProfile == profile) return;

        QualityProfile = profile;
        // A bandwidth-tier change recreates the receiver inside the bridge
        // (NdiViewerBridge.SetQualityProfile -> StartReceiver), which bumps the ownership token —
        // re-claim it, or this ViewModel silently stops being the bridge's client. Conditional,
        // because Balanced and High map to the same bandwidth tier (NdiViewerBridge.MapBandwidth),
        // so that change restarts nothing and bumps nothing: re-recording unconditionally would let
        // a ViewModel that never asked the bridge for a receiver claim ownership of another's — the
        // exact inversion the token exists to prevent.
        var generationBeforeProfileChange = _bridge.ReceiverGeneration;
        _bridge.SetQualityProfile(profile);
        if (_receiverGeneration == generationBeforeProfileChange)
            _receiverGeneration = _bridge.ReceiverGeneration;

        // Persist quality profile to source (if connected)
        if (!string.IsNullOrEmpty(SourceId))
        {
            var sources = await _sourceRepository.GetCachedSourcesAsync();
            var source = sources.FirstOrDefault(s => s.SourceId == SourceId);
            if (source != null)
            {
                var updatedSource = source with { QualityProfile = profile };
                await _sourceRepository.SaveSourceAsync(updatedSource);
            }
        }

        StatusMessage = $"Quality profile set to {profile}.";
    }

    /// <summary>Full-screen toolbar: Smooth → Balanced → High → Smooth.</summary>
    [RelayCommand]
    private Task CycleQualityProfile() => ChangeQualityProfileAsync(NextQualityProfile);
}
