using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiForAndroid.Features.AppState.Models;
using NdiForAndroid.Features.AppState.Repositories;
using NdiForAndroid.Features.Output.Repositories;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Services;

namespace NdiForAndroid.Features.Output.ViewModels;

public partial class OutputViewModel : ObservableObject, IDisposable
{
    private readonly INdiOutputBridge _bridge;
    private readonly IAppStateRepository _appStateRepo;
    private readonly IAppLifecycleService _lifecycle;
    private readonly IOutputConfigurationRepository _configRepo;
    private readonly IMainThreadDispatcher _dispatcher;

    [ObservableProperty]
    private string _streamName = "NDI-Android";

    [ObservableProperty]
    private bool _isOutputActive;

    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>Video input feeding the output: device screen or front/rear camera.</summary>
    [ObservableProperty]
    private VideoInputKind _selectedInputKind = VideoInputKind.Screen;

    /// <summary>Also capture and send the device microphone audio.</summary>
    [ObservableProperty]
    private bool _captureMicrophone;

    /// <summary>True while any connected receiver reports this sender on program tally.</summary>
    [ObservableProperty]
    private bool _isOnProgramTally;

    /// <summary>Number of receivers currently connected to this sender.</summary>
    [ObservableProperty]
    private int _connectionCount;

    /// <summary>
    /// When true, the output will capture and re-stream a discovered NDI source
    /// rather than broadcasting the selected local capture input.
    /// </summary>
    [ObservableProperty]
    private bool _isReStreamMode;

    /// <summary>
    /// The SourceId of the discovered NDI source to re-stream (set when switching
    /// into re-stream mode).
    /// </summary>
    [ObservableProperty]
    private string? _reStreamSourceId;

    public IReadOnlyList<VideoInputKind> AvailableInputKinds { get; } = new[]
    {
        VideoInputKind.Screen,
        VideoInputKind.CameraFront,
        VideoInputKind.CameraRear,
    };

    public OutputViewModel(
        INdiOutputBridge bridge,
        IAppStateRepository appStateRepo,
        IAppLifecycleService lifecycle,
        IOutputConfigurationRepository configRepo,
        IMainThreadDispatcher dispatcher)
    {
        _bridge = bridge;
        _appStateRepo = appStateRepo;
        _lifecycle = lifecycle;
        _configRepo = configRepo;
        _dispatcher = dispatcher;
        StatusMessage = "Tap Start to begin broadcasting from this device.";

        _lifecycle.AppResumed += OnAppResumed;
        _bridge.OutputStatusChanged += OnOutputStatusChanged;
    }

    /// <summary>Loads the persisted output configuration (called when the page appears).</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        var config = await _configRepo.GetAsync();
        if (config is null)
            return;

        if (!string.IsNullOrWhiteSpace(config.PreferredStreamName))
            StreamName = config.PreferredStreamName;

        SelectedInputKind = config.InputKind;
        CaptureMicrophone = config.CaptureMicrophone;
    }

    private async void OnAppResumed()
    {
        // Re-attach output session on resume if there was an active stream
        var state = await _appStateRepo.RestoreStateAsync();
        if (state.IsOutputActive && !string.IsNullOrEmpty(state.StreamName))
        {
            IsOutputActive = true;
            StatusMessage = "Output session restored.";
            StreamName = state.StreamName;
        }
    }

    /// <summary>Bridge status poll changed — marshal tally/connection refresh to the UI thread.</summary>
    private void OnOutputStatusChanged(object? sender, EventArgs e)
    {
        _dispatcher.BeginInvokeOnMainThread(() =>
        {
            IsOnProgramTally = _bridge.IsOnProgramTally;
            ConnectionCount = _bridge.ConnectionCount;
        });
    }

    [RelayCommand]
    private async Task ToggleReStreamModeAsync()
    {
        IsReStreamMode = !IsReStreamMode;

        if (IsReStreamMode)
        {
            // When switching to re-stream mode, default stream name uses the source identifier.
            StreamName = $"NDI-{string.Concat(ReStreamSourceId!.TakeWhile(char.IsLetterOrDigit)).Take(32)}";
            StatusMessage = "Re-stream mode: select a discovered source on the Sources page.";
        }
        else
        {
            StatusMessage = "Capture mode: stream your screen or camera as an NDI sender.";
        }

        // Persist mode so it survives app restarts / process death.
        var state = await _appStateRepo.RestoreStateAsync();
        await _appStateRepo.SaveAsync(new AppStateSnapshot(
            state.LastViewerSourceId,
            StreamName,
            false, // isOutputActive resets when switching modes
            state.LastSelectedSourceId));
    }

    [RelayCommand]
    private async Task StartOutputAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(StreamName))
        {
            StatusMessage = "Please enter a stream name before starting output.";
            return;
        }

        StatusMessage = null;

        try
        {
            if (IsReStreamMode && !string.IsNullOrEmpty(ReStreamSourceId))
            {
                // Start re-streaming from the selected NDI source.
                await _bridge.StartReStreamFromSourceAsync(
                    ReStreamSourceId, QualityProfile.Balanced, cancellationToken);

                IsOutputActive = true;
                StatusMessage = "Re-stream active";
            }
            else
            {
                // Start capture output. The capture source owns the Android foreground
                // session / permission flow — the VM only talks to the bridge.
                await _bridge.StartOutputAsync(
                    StreamName, SelectedInputKind, CaptureMicrophone, cancellationToken);

                IsOutputActive = true;
                StatusMessage = "Output active";

                // Persist the configuration only after a successful start.
                await _configRepo.SaveAsync(new OutputConfiguration(
                    StreamName, SelectedInputKind, CaptureMicrophone));
            }

            // Persist output state so it survives resume / process death.
            var snapshot = await _appStateRepo.RestoreStateAsync();
            await _appStateRepo.SaveAsync(new AppStateSnapshot(
                snapshot.LastViewerSourceId,
                StreamName,
                true,
                snapshot.LastSelectedSourceId));
        }
        catch (OperationCanceledException)
        {
            // The user declined the capture permission (e.g. MediaProjection consent).
            IsOutputActive = false;
            StatusMessage = "Permission declined — output not started.";
        }
        catch (Exception ex)
        {
            IsOutputActive = false;
            StatusMessage = $"Output failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task StopOutputAsync(CancellationToken cancellationToken)
    {
        if (IsReStreamMode)
        {
            await _bridge.StopReStreamAsync(cancellationToken);
        }
        else
        {
            await _bridge.StopOutputAsync(cancellationToken);
        }

        IsOutputActive = false;
        StatusMessage = null;
        IsOnProgramTally = false;
        ConnectionCount = 0;

        // Clear output state but keep viewer source.
        var snapshot = await _appStateRepo.RestoreStateAsync();
        await _appStateRepo.SaveAsync(new AppStateSnapshot(
            snapshot.LastViewerSourceId,
            null,
            false,
            snapshot.LastSelectedSourceId));
    }

    public void Dispose()
    {
        _lifecycle.AppResumed -= OnAppResumed;
        _bridge.OutputStatusChanged -= OnOutputStatusChanged;
    }
}
