using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiForAndroid.Features.AppState.Models;
using NdiForAndroid.Features.AppState.Repositories;
using NdiForAndroid.Features.Sources.Models;
using NdiForAndroid.Features.Sources.Repositories;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Services;
using Timer = System.Threading.Timer;

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
    private const int RetryWindowSeconds = 15;
    private const int AttemptIntervalSeconds = 2;
    private const int MonitorIntervalSeconds = 1;
    private const string TerminalMessage = "Connection lost. Reconnection failed.";

    private readonly INdiViewerBridge _bridge;
    private readonly TimeProvider _timeProvider;
    private readonly IMainThreadDispatcher _dispatcher;
    private readonly IAppStateRepository _appStateRepo;
    private readonly IAppLifecycleService _lifecycle;
    private readonly ISourceRepository _sourceRepository;

    [ObservableProperty]
    private string? _sourceId;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private string? _statusMessage;

    // Reconnect state
    [ObservableProperty]
    private bool _isReconnecting;

    [ObservableProperty]
    private int _retryRemainingSeconds;

    [ObservableProperty]
    private string? _retryStatusMessage;

    [ObservableProperty]
    private bool _canReconnect;

    private Timer? _countdownTimer;
    private Timer? _attemptTimer;
    private volatile bool _userInitiatedStop;
    private string? _lastSourceId;
    private bool _wasPlayingBeforeResume;

    // Quality profile selection
    [ObservableProperty]
    private QualityProfile _qualityProfile = QualityProfile.Balanced;

    private QualityProfile? _previousQualityProfile;
    private float? _lastMeasuredFps;
    private int _sustainedDropCount;
    private static readonly int AutoDegradationThreshold = 3;
    private const float DegradationThresholdFps = 15f;

    public IEnumerable<QualityProfile> AvailableProfiles => new[] { QualityProfile.Smooth, QualityProfile.Balanced, QualityProfile.High };

    /// <summary>Developer-visible label for the current quality profile.</summary>
    public string? QualityProfileLabel => IsPlaying ? $"QProfile: {QualityProfile}" : null;

    // State machine
    private enum ReconnectState { Idle, InWindow, Attempting, Successful, Failed }
    private ReconnectState _reconnectState = ReconnectState.Idle;

    public ViewerViewModel(
        INdiViewerBridge bridge,
        TimeProvider timeProvider,
        IMainThreadDispatcher dispatcher,
        IAppStateRepository appStateRepo,
        IAppLifecycleService lifecycle,
        ISourceRepository sourceRepository)
    {
        _bridge = bridge;
        _timeProvider = timeProvider;
        _dispatcher = dispatcher;
        _appStateRepo = appStateRepo;
        _lifecycle = lifecycle;
        _sourceRepository = sourceRepository;
        RetryRemainingSeconds = ReconnectConstants.RetryWindowSeconds;
        StatusMessage = "Select a source on Home to start viewing.";

        _lifecycle.AppResumed += OnAppResumed;
    }

    private void OnAppResumed()
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
                var sources = Task.Run(async () => await _sourceRepository.GetCachedSourcesAsync()).Result;
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
                StatusMessage = $"Connected. (QProfile: {QualityProfile})";
                RetryStatusMessage = null;
            }
        }
    }

    partial void OnIsPlayingChanged(bool value)
    {
        _wasPlayingBeforeResume = value;
    }

    partial void OnSourceIdChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value))
            StartCommand.Execute(null);
    }

    // ----- Commands -------------------------------------------------------

    [RelayCommand]
    private async void Start()
    {
        if (string.IsNullOrEmpty(SourceId))
        {
            StatusMessage = "Select a source on Home to start viewing.";
            return;
        }

        _userInitiatedStop = false;
        _lastSourceId = SourceId;
        
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

        // Persist last active viewer source for resume recovery
        _appStateRepo.SaveAsync(new AppStateSnapshot(SourceId, null, false, null)).ConfigureAwait(continueOnCapturedContext: true);
        
        _bridge.StartReceiver(SourceId, QualityProfile);
        IsPlaying = true;
        StatusMessage = $"Connecting... (QProfile: {QualityProfile})";
    }

    [RelayCommand]
    private void Stop()
    {
        _userInitiatedStop = true;
        DisposeTimers();
        _bridge.StopReceiver();
        IsPlaying = false;
        IsReconnecting = false;
        CanReconnect = false;
        RetryStatusMessage = null;
        StatusMessage = null;
        RetryRemainingSeconds = ReconnectConstants.RetryWindowSeconds;
        RetryStatusMessage = null;
    }

    public void Dispose()
    {
        DisposeTimers();
        _userInitiatedStop = true;
        _lifecycle.AppResumed -= OnAppResumed;
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
        _attemptTimer = new Timer(
            _ => _dispatcher.BeginInvokeOnMainThread(() => RunAttempt()),
            null,
            TimeSpan.FromSeconds(ReconnectConstants.RetryAttemptIntervalSeconds),
            TimeSpan.FromSeconds(ReconnectConstants.RetryAttemptIntervalSeconds));
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
                _bridge.StartReceiver(SourceId);
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

        if (_reconnectState != ReconnectState.Failed && _reconnectState != ReconnectState.Successful)
            _reconnectState = ReconnectState.InWindow;
    }

    private void CompleteReconnect()
    {
        _dispatcher.BeginInvokeOnMainThread(() =>
        {
            _reconnectState = ReconnectState.Successful;
            IsReconnecting = false;
            IsPlaying = true;
            StatusMessage = $"Connected. (QProfile: {QualityProfile})";
            RetryRemainingSeconds = 0;
            RetryStatusMessage = null;
            DisposeTimers();
        });
    }

    // --- FR4: 1s countdown ---

    private void StartCountdown()
    {
        _countdownTimer = new Timer(
            _ => TickCountdown(),
            null,
            TimeSpan.FromSeconds(ReconnectConstants.CountdownTickIntervalSeconds),
            TimeSpan.FromSeconds(ReconnectConstants.CountdownTickIntervalSeconds));
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
        if (string.IsNullOrEmpty(SourceId) && string.IsNullOrEmpty(_lastSourceId))
            return;

        SourceId ??= _lastSourceId;
        CanReconnect = false;
        StatusMessage = "Attempting reconnect...";

        BeginReconnectWindow();
    }

    // --- Quality Profile ---

    [RelayCommand]
    private async Task ChangeQualityProfileAsync(object? param)
    {
        if (param is not string profileName) return;

        if (!Enum.TryParse<QualityProfile>(profileName, ignoreCase: true, out var profile)) return;

        if (QualityProfile == profile) return;

        _previousQualityProfile ??= QualityProfile;
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

    /// <summary>
/// Monitor for quality degradation and auto-degrade if sustained drops detected.
/// Call from a frame update loop or watchdog timer (e.g., every ~5 seconds).
/// </summary>
    public async void CheckAutoDegradation(float currentFps, float dropPercent)
{
    // Only degrade if playing and not already in reconnect window
    if (!IsPlaying || IsReconnecting) return;

    // Already degraded — only auto-recover
    if (_previousQualityProfile != null)
    {
        if (currentFps > DegradationThresholdFps && dropPercent < 10f)
        {
            // Recover to previous profile
                QualityProfile = _previousQualityProfile.Value;
                _bridge.SetQualityProfile(QualityProfile);
            _previousQualityProfile = null;
            _sustainedDropCount = 0;
            StatusMessage = "Connection stabilized — quality restored.";
        }

        // Don't keep degrading if already at lowest
        return;
    }

    // Check for degradation: sustained low FPS or high drop rate
    if (currentFps < DegradationThresholdFps || dropPercent > 30f)
    {
        _sustainedDropCount++;
        if (_sustainedDropCount >= AutoDegradationThreshold && QualityProfile != QualityProfile.Smooth)
        {
            // Degrade one step
                var next = QualityProfile switch
            {
                QualityProfile.High => QualityProfile.Balanced,
                QualityProfile.Balanced => QualityProfile.Smooth,
                _ => QualityProfile.Smooth
            };

                _previousQualityProfile = QualityProfile;
                QualityProfile = next;
            _bridge.SetQualityProfile(next);

            // Persist
            if (!string.IsNullOrEmpty(SourceId))
            {
                var sources = await _sourceRepository.GetCachedSourcesAsync();
                var source = sources.FirstOrDefault(s => s.SourceId == SourceId);
                if (source != null)
                {
                    var updatedSource = source with { QualityProfile = next };
                    await _sourceRepository.SaveSourceAsync(updatedSource);
                }
            }

            StatusMessage = $"Auto-degraded quality to {next} due to poor connection.";
        }
    }
    else
    {
        // Good connection — reset drop counter but don't auto-recover yet
        _sustainedDropCount = Math.Max(0, _sustainedDropCount - 1);
    }
}

    public void ResumeQualityProfile()
    {
        if (_previousQualityProfile != null)
        {
            QualityProfile = _previousQualityProfile.Value;
            _bridge.SetQualityProfile(QualityProfile);
            StatusMessage = $"Quality profile resumed to {QualityProfile}.";
        }
    }
}
