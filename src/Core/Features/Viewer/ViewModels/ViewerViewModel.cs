using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiForAndroid.Features.AppState.Models;
using NdiForAndroid.Features.AppState.Repositories;
using NdiForAndroid.Features.ConnectionHistory.Services;
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
    /// window. Must comfortably exceed the bridge's own 3 s stall demotion
    /// (<c>NdiViewerBridge.StalledAfterMs</c>) so a brief stall is not treated as a drop.</summary>
    public const int SustainedConnectingSamples = 5;
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

    [ObservableProperty]
    private string? _sourceId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QualityProfileLabel))]
    private bool _isPlaying;

    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>
    /// True after the user taps Stop, until playback starts again (#348). Drives the View's
    /// "Stopped" treatment of the video surface — the render loop keeps polling
    /// <see cref="CurrentFrame"/>, so without this the last frame stays painted forever with
    /// nothing on screen to say playback ended.
    /// </summary>
    [ObservableProperty]
    private bool _isStopped;

    // Tally / PTZ / audio state surfaced from the bridge
    [ObservableProperty]
    private bool _isTallyProgram;

    [ObservableProperty]
    private bool _isPtzSupported;

    [ObservableProperty]
    private bool _isAudioEnabled;

    /// <summary>
    /// Latest decoded frame from the bridge; polled by the View's render loop
    /// (~30 fps). No change notification — the frame's timestamp is the dedupe key.
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

    /// <summary>True once the bridge has reported Connected at least once since this ViewModel last
    /// called Start(). Arms the level-triggered drop backstop: a receiver that has never connected
    /// is an initial-connect attempt, not a drop, and must keep today's behaviour.</summary>
    private bool _hasConnectedSinceStart;

    private string? _lastSourceId;
    private bool _wasPlayingBeforeResume;

    /// <summary>Generation of the receiver this ViewModel last asked the bridge to start
    /// (<see cref="INdiViewerBridge.ReceiverGeneration"/>). The bridge is a singleton and more than
    /// one ViewerViewModel is alive at once — the Expanded two-pane <c>PaneViewer</c> is never
    /// disposed and a pushed ViewerPage gets its own transient instance — so "the bridge reported a
    /// drop" is not on its own a statement about *this* ViewModel's stream. -1 = never started one.</summary>
    private long _receiverGeneration = -1;

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
        IOrientationLockService orientationLock)
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
        RetryRemainingSeconds = ReconnectConstants.RetryWindowSeconds;
        StatusMessage = "Select a source on Home to start viewing.";
        _isAudioEnabled = bridge.IsAudioEnabled; // backing field: don't push the default back to the bridge
        SyncSelectedProfileOption(); // field initialisers do not fire OnQualityProfileChanged

        _lifecycle.AppResumed += OnAppResumed;
        _lifecycle.AppPaused += OnAppPaused;
        _lifecycle.OrientationChanged += OnOrientationChanged;
        _bridge.ConnectionStateChanged += OnBridgeConnectionStateChanged;
        _bridge.TallyEchoChanged += OnBridgeTallyEchoChanged;
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
            if (IsPlaying && !IsReconnecting && state == ConnectionState.Connected)
                StatusMessage = "Connected.";

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

    // async void is intentional: MAUI lifecycle event handler. All awaits are guarded so
    // no exception can escape and tear down the process.
    private async void OnAppResumed()
    {
        // If we were playing when the app went background, try to restore
        if (_wasPlayingBeforeResume && !string.IsNullOrEmpty(SourceId) && !IsPlaying)
        {
            _wasPlayingBeforeResume = false;
            RetryStatusMessage = "Restoring viewer...";
            IsReconnecting = true;

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

            _bridge.StartReceiver(SourceId, QualityProfile);
            _receiverGeneration = _bridge.ReceiverGeneration;
            var state = _bridge.GetConnectionState();
            if (state == ConnectionState.Connected)
            {
                IsPlaying = true;
                IsReconnecting = false;
                StatusMessage = "Connected.";
                RetryStatusMessage = null;
            }
        }
    }

    partial void OnIsPlayingChanged(bool value)
    {
        _wasPlayingBeforeResume = value;
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
        DisposeTimers();
        _reconnectState = ReconnectState.Idle;
        _bridge.SetTally(onProgram: false, onPreview: false);
        _bridge.StopReceiver();
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
        _lifecycle.AppResumed -= OnAppResumed;
        _lifecycle.AppPaused -= OnAppPaused;
        _lifecycle.OrientationChanged -= OnOrientationChanged;
        _bridge.ConnectionStateChanged -= OnBridgeConnectionStateChanged;
        _bridge.TallyEchoChanged -= OnBridgeTallyEchoChanged;
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
        _dispatcher.BeginInvokeOnMainThread(() =>
        {
            if (_reconnectState != ReconnectState.Idle || IsReconnecting)
                return; // Already in a reconnect window.

            _reconnectState = ReconnectState.InWindow;
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
    /// ViewModel must never drive the bridge, or two reconnect loops fight over one receiver and the
    /// user sees one source's video labelled with another source's status.</summary>
    private bool OwnsActiveReceiver => _bridge.ReceiverGeneration == _receiverGeneration;

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

        // The receiver can recover on its own between ticks: the pump keeps running through a
        // ConnectionLost, so a frame may have arrived and moved the bridge to Connected. Tearing it
        // down here would destroy a healthy connection and hand the window a Connecting bridge.
        if (_bridge.GetConnectionState() == ConnectionState.Connected)
        {
            CompleteReconnect();
            return;
        }

        _reconnectState = ReconnectState.Attempting;
        RetryStatusMessage = "Attempting reconnect...";

        try
        {
            _bridge.StopReceiver();

            if (!string.IsNullOrEmpty(SourceId))
            {
                _bridge.StartReceiver(SourceId, QualityProfile);
                _receiverGeneration = _bridge.ReceiverGeneration;
                var state = _bridge.GetConnectionState();

                if (state == ConnectionState.Connected)
                {
                    CompleteReconnect();
                    return;
                }
            }
        }
        catch
        {
            // Attempt failed – fall through to continue the window.
        }

        if (_reconnectState == ReconnectState.Attempting)
            _reconnectState = ReconnectState.InWindow;
    }

    private void CompleteReconnect()
    {
        _dispatcher.BeginInvokeOnMainThread(() =>
        {
            // This body runs one dispatcher turn behind its caller. Two things can have happened in
            // between: the countdown expired (FailReconnect ran and is terminal — do not resurrect
            // playback, force-rotate the device back out of full screen, or leave CanReconnect true
            // on a connected stream), or an attempt tick restarted the receiver, in which case the
            // event that got us here is stale and the bridge is Connecting, not Connected.
            if (_reconnectState == ReconnectState.Failed)
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
        });
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

    private void FailReconnect()
    {
        _dispatcher.BeginInvokeOnMainThread(() =>
        {
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
            _bridge.StopReceiver();
            // Before BeginExitFullScreen: on a compact device the exit only *requests* portrait, so
            // IsFullScreen stays true for up to 3s — the in-video Stopped badge is the only thing on
            // screen during that window.
            IsStopped = true;
            // Playback has definitively ended, so leave full screen the way Stop() already does.
            // Idempotent; on a compact device in landscape this requests portrait and completes on
            // the resulting orientation change. Doing it here and not when the window opens is what
            // keeps a transient drop from force-rotating the device.
            BeginExitFullScreen();
            StatusMessage = "Connection lost. Reconnection failed.";
            RetryStatusMessage = null;
            CanReconnect = true;
        });
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
        IsReconnecting = false;
        IsPlaying = false;
        // Same terminal contract as FailReconnect: no receiver left pumping behind a cancelled
        // message, and the video surface says so instead of holding a frozen frame.
        _bridge.StopReceiver();
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
        _bridge.SetQualityProfile(profile);
        // A bandwidth-tier change recreates the receiver inside the bridge
        // (NdiViewerBridge.SetQualityProfile -> StartReceiver), which bumps the ownership token —
        // re-claim it, or this ViewModel silently stops being the bridge's client.
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
