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
    public const int DropDetectionGraceSeconds = 3;
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
    private string? _lastSourceId;
    private bool _wasPlayingBeforeResume;

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
        IScreenReaderAnnouncer announcer)
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
        RetryRemainingSeconds = ReconnectConstants.RetryWindowSeconds;
        StatusMessage = "Select a source on Home to start viewing.";
        _isAudioEnabled = bridge.IsAudioEnabled; // backing field: don't push the default back to the bridge
        SyncSelectedProfileOption(); // field initialisers do not fire OnQualityProfileChanged

        _lifecycle.AppResumed += OnAppResumed;
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

            // Minimal status refresh; drop handling stays with the reconnect state machine.
            if (IsPlaying && !IsReconnecting && state == ConnectionState.Connected)
                StatusMessage = "Connected.";
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
        _bridge.SetTally(onProgram: true, onPreview: false);
        IsPlaying = true;
        StatusMessage = "Connecting...";
    }

    [RelayCommand]
    private void Stop()
    {
        _userInitiatedStop = true;
        IsFullScreen = false;
        DisposeTimers();
        _reconnectState = ReconnectState.Idle;
        _bridge.SetTally(onProgram: false, onPreview: false);
        _bridge.StopReceiver();

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
        _bridge.ConnectionStateChanged -= OnBridgeConnectionStateChanged;
        _bridge.TallyEchoChanged -= OnBridgeTallyEchoChanged;
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
            RetryRemainingSeconds = ReconnectConstants.RetryWindowSeconds;
            RetryStatusMessage = $"Reconnecting... {RetryRemainingSeconds}s remaining";
            StartCountdown();
            StartAttemptTimer();
        });
    }

    /// <summary>
    /// Call from a background callback to check if connection was unexpectedly dropped.
    /// </summary>
    public void CheckForUnexpectedDrop()
    {
        var connState = _bridge.GetConnectionState();
        if (connState == ConnectionState.Disconnected && IsPlaying && !_userInitiatedStop && _reconnectState == ReconnectState.Idle)
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

        _reconnectState = ReconnectState.Attempting;
        RetryStatusMessage = "Attempting reconnect...";

        try
        {
            _bridge.StopReceiver();

            if (!string.IsNullOrEmpty(SourceId))
            {
                _bridge.StartReceiver(SourceId, QualityProfile);
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
        IsReconnecting = false;
        CanReconnect = false;
        RetryRemainingSeconds = ReconnectConstants.RetryWindowSeconds;
        RetryStatusMessage = "Reconnection cancelled.";
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
