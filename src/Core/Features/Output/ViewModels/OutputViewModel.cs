using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiForAndroid.Features.AppState.Models;
using NdiForAndroid.Features.AppState.Repositories;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Services;

namespace NdiForAndroid.Features.Output.ViewModels;

public partial class OutputViewModel : ObservableObject, IDisposable
{
    private readonly INdiOutputBridge _bridge;
    private readonly IScreenSharePlatformService _screenSharePlatformService;
    private readonly IAppStateRepository _appStateRepo;
    private readonly IAppLifecycleService _lifecycle;

    [ObservableProperty]
    private string _streamName = "NDI-Android";

    [ObservableProperty]
    private bool _isOutputActive;

    [ObservableProperty]
    private string? _statusMessage;

    public OutputViewModel(
        INdiOutputBridge bridge,
        IScreenSharePlatformService screenSharePlatformService,
        IAppStateRepository appStateRepo,
        IAppLifecycleService lifecycle)
    {
        _bridge = bridge;
        _screenSharePlatformService = screenSharePlatformService;
        _appStateRepo = appStateRepo;
        _lifecycle = lifecycle;
        StatusMessage = "Tap Start to begin broadcasting from this device.";

        _lifecycle.AppResumed += OnAppResumed;
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
            await _screenSharePlatformService.StartForegroundSessionAsync(StreamName, cancellationToken);
            await _bridge.StartOutputAsync(StreamName, cancellationToken);
            IsOutputActive = true;
            StatusMessage = "Output active";
            // Persist output state so it survives resume / process death
            var snapshot = await _appStateRepo.RestoreStateAsync();
            await _appStateRepo.SaveAsync(new AppStateSnapshot(
                snapshot.LastViewerSourceId,
                StreamName,
                true,
                snapshot.LastSelectedSourceId));
        }
        catch (Exception ex)
        {
            IsOutputActive = false;
            StatusMessage = $"Output failed: {ex.Message}";
            await _screenSharePlatformService.StopForegroundSessionAsync(cancellationToken);
        }
    }

    [RelayCommand]
    private async Task StopOutputAsync(CancellationToken cancellationToken)
    {
        await _bridge.StopOutputAsync(cancellationToken);
        await _screenSharePlatformService.StopForegroundSessionAsync(cancellationToken);
        IsOutputActive = false;
        StatusMessage = null;
        // Clear output state but keep viewer source
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
    }
}
