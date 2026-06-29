using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiForAndroid.Features.AppState.Models;
using NdiForAndroid.Features.AppState.Repositories;
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
    private readonly INdiViewerBridge _bridge;
    private readonly TimeProvider _timeProvider;
    private readonly IMainThreadDispatcher _dispatcher;
    private readonly IAppStateRepository _appStateRepo;
    private readonly IAppLifecycleService _lifecycle;

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

    // State machine
    private enum ReconnectState { Idle, InWindow, Attempting, Successful, Failed }
    private ReconnectState _reconnectState = ReconnectState.Idle;

    public ViewerViewModel(
        INdiViewerBridge bridge,
        TimeProvider timeProvider,
        IMainThreadDispatcher dispatcher,
        IAppStateRepository appStateRepo,
        IAppLifecycleService lifecycle)
    {
        _bridge = bridge;
        _timeProvider = timeProvider;
        _dispatcher = dispatcher;
        _appStateRepo = appStateRepo;
        _lifecycle = lifecycle;
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
            _bridge.StartReceiver(SourceId);
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
    }

    partial void OnSourceIdChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value))
            StartCommand.Execute(null);
    }

    [RelayCommand]
    private void Start()
    {
        if (string.IsNullOrEmpty(SourceId))
        {
            StatusMessage = "Select a source on Home to start viewing.";
            return;
        }

        _userInitiatedStop = false;
        _lastSourceId = SourceId;
        // Persist last active viewer source for resume recovery
        _appStateRepo.SaveAsync(new AppStateSnapshot(SourceId, null, false, null)).ConfigureAwait(continueOnCapturedContext: true);
        _bridge.StartReceiver(SourceId);
        IsPlaying = true;
        StatusMessage = "Connecting...";
    }

    [RelayCommand]
    private void Stop()
    {
        _userInitiatedStop = true;
        DisposeTimers();
        _bridge.StopReceiver();
        IsPlaying = false;
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
            StatusMessage = "Connected.";
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
}
