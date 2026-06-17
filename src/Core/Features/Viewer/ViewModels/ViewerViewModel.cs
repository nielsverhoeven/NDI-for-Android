using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Services;

namespace NdiForAndroid.Features.Viewer.ViewModels;

/// <summary>
/// Drives the NDI viewer, including the automatic reconnection state machine.
/// All retry/countdown timing is driven by an injected <see cref="TimeProvider"/> and every
/// timer/poll callback marshals observable mutations onto the UI thread via the injected
/// <see cref="IMainThreadDispatcher"/>, keeping this Core ViewModel MAUI-free and unit-testable.
/// </summary>
public partial class ViewerViewModel : ObservableObject, IDisposable
{
    private const int RetryWindowSeconds = 15;
    private const int AttemptIntervalSeconds = 2;
    private const int MonitorIntervalSeconds = 1;
    private const string TerminalMessage = "Connection lost. Reconnection failed.";

    private readonly INdiViewerBridge _bridge;
    private readonly TimeProvider _timeProvider;
    private readonly IMainThreadDispatcher _mainThread;

    private bool _userInitiatedStop;
    private string? _lastSourceId;
    private int _remainingSeconds;
    private bool _disposed;

    private ITimer? _monitorTimer;
    private ITimer? _attemptTimer;
    private ITimer? _countdownTimer;

    [ObservableProperty]
    private string? _sourceId;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isReconnecting;

    [ObservableProperty]
    private string? _retryStatusMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReconnectCommand))]
    private bool _canReconnect;

    public ViewerViewModel(
        INdiViewerBridge bridge,
        TimeProvider timeProvider,
        IMainThreadDispatcher mainThread)
    {
        _bridge = bridge;
        _timeProvider = timeProvider;
        _mainThread = mainThread;
        StatusMessage = "Select a source on Home to start viewing.";
    }

    partial void OnSourceIdChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value))
            StartCommand.Execute(null);
    }

    // ----- Commands -------------------------------------------------------

    [RelayCommand]
    private void Start()
    {
        if (string.IsNullOrEmpty(SourceId))
        {
            StatusMessage = "Select a source on Home to start viewing.";
            return;
        }

        StartInternal(SourceId);
    }

    [RelayCommand]
    private void Stop()
    {
        // FR8: a user-initiated Stop must never trigger the retry flow.
        _userInitiatedStop = true;
        DisposeTimers();
        _bridge.StopReceiver();
        IsPlaying = false;
        IsReconnecting = false;
        CanReconnect = false;
        RetryStatusMessage = null;
        StatusMessage = null;
    }

    // FR6: abort the reconnect window immediately and transition to stopped.
    [RelayCommand]
    private void CancelRetry()
    {
        _userInitiatedStop = true;
        DisposeTimers();
        _bridge.StopReceiver();
        IsReconnecting = false;
        RetryStatusMessage = null;
        CanReconnect = false;
        IsPlaying = false;
        StatusMessage = null;
    }

    // FR7: restart the full flow with the last SourceId from the error state.
    [RelayCommand(CanExecute = nameof(CanReconnect))]
    private void Reconnect()
    {
        if (string.IsNullOrEmpty(_lastSourceId))
            return;

        CanReconnect = false;
        RetryStatusMessage = null;
        StartInternal(_lastSourceId);
    }

    // ----- State machine --------------------------------------------------

    private void StartInternal(string sourceId)
    {
        _userInitiatedStop = false;
        _lastSourceId = sourceId;
        DisposeTimers();

        _bridge.StartReceiver(sourceId);
        IsPlaying = true;
        IsReconnecting = false;
        CanReconnect = false;
        StatusMessage = "Connecting...";

        StartMonitor();
    }

    private void StartMonitor()
    {
        _monitorTimer?.Dispose();
        _monitorTimer = _timeProvider.CreateTimer(
            OnMonitorTick,
            null,
            TimeSpan.FromSeconds(MonitorIntervalSeconds),
            TimeSpan.FromSeconds(MonitorIntervalSeconds));
    }

    private void OnMonitorTick(object? state) => _mainThread.Invoke(PollConnection);

    private void PollConnection()
    {
        // FR1: detect an unexpected drop while playing (and not user-stopped).
        if (!IsPlaying || IsReconnecting || _userInitiatedStop)
            return;

        if (_bridge.GetConnectionState() == ConnectionState.Disconnected)
            BeginReconnectWindow();
    }

    // FR2: enter the fixed 15s reconnect window with countdown + attempt loops.
    private void BeginReconnectWindow()
    {
        DisposeTimers();

        IsReconnecting = true;
        CanReconnect = false;
        _remainingSeconds = RetryWindowSeconds;
        RetryStatusMessage = FormatRetry(_remainingSeconds);
        StatusMessage = "Connection lost. Reconnecting...";

        _countdownTimer = _timeProvider.CreateTimer(
            OnCountdownTick,
            null,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1));

        _attemptTimer = _timeProvider.CreateTimer(
            OnAttemptTick,
            null,
            TimeSpan.FromSeconds(AttemptIntervalSeconds),
            TimeSpan.FromSeconds(AttemptIntervalSeconds));
    }

    private void OnCountdownTick(object? state) => _mainThread.Invoke(TickCountdown);

    // FR4: per-second countdown indicator.
    private void TickCountdown()
    {
        if (!IsReconnecting)
            return;

        _remainingSeconds--;

        if (_remainingSeconds <= 0)
        {
            FailReconnect();
            return;
        }

        RetryStatusMessage = FormatRetry(_remainingSeconds);
    }

    private void OnAttemptTick(object? state) => _mainThread.Invoke(RunAttempt);

    // FR3: each attempt performs a full Stop -> Start cycle; first Connected wins.
    private void RunAttempt()
    {
        if (!IsReconnecting)
            return;

        if (string.IsNullOrEmpty(_lastSourceId))
        {
            FailReconnect();
            return;
        }

        _bridge.StopReceiver();
        _bridge.StartReceiver(_lastSourceId);

        if (_bridge.GetConnectionState() == ConnectionState.Connected)
            CompleteReconnect();
    }

    private void CompleteReconnect()
    {
        DisposeReconnectTimers();
        IsReconnecting = false;
        RetryStatusMessage = null;
        CanReconnect = false;
        IsPlaying = true;
        StatusMessage = "Connected.";

        // Resume drop detection for any future unexpected disconnect.
        StartMonitor();
    }

    // FR5: window expired with no Connected result -> terminal stopped/error state.
    private void FailReconnect()
    {
        DisposeTimers();
        IsReconnecting = false;
        IsPlaying = false;
        RetryStatusMessage = null;
        CanReconnect = true;
        StatusMessage = TerminalMessage;
    }

    private static string FormatRetry(int seconds) => $"Reconnecting... {seconds}s remaining";

    // ----- Timer lifecycle ------------------------------------------------

    private void DisposeReconnectTimers()
    {
        _attemptTimer?.Dispose();
        _attemptTimer = null;
        _countdownTimer?.Dispose();
        _countdownTimer = null;
    }

    private void DisposeTimers()
    {
        DisposeReconnectTimers();
        _monitorTimer?.Dispose();
        _monitorTimer = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        DisposeTimers();
        GC.SuppressFinalize(this);
    }
}
