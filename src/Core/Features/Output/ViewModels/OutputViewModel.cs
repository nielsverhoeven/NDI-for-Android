using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Services;

namespace NdiForAndroid.Features.Output.ViewModels;

public partial class OutputViewModel : ObservableObject
{
    private readonly INdiOutputBridge _bridge;
    private readonly IScreenSharePlatformService _screenSharePlatformService;

    [ObservableProperty]
    private string? _sourceId;

    [ObservableProperty]
    private bool _isOutputActive;

    [ObservableProperty]
    private string? _statusMessage;

    public OutputViewModel(INdiOutputBridge bridge, IScreenSharePlatformService screenSharePlatformService)
    {
        _bridge = bridge;
        _screenSharePlatformService = screenSharePlatformService;
        StatusMessage = "Select a source on Home before starting output.";
    }

    [RelayCommand]
    private async Task StartOutputAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(SourceId))
        {
            StatusMessage = "Select a source on Home before starting output.";
            return;
        }

        StatusMessage = null;

        if (!await _bridge.IsSourceReachableAsync(SourceId, cancellationToken))
        {
            StatusMessage = "Source is not reachable.";
            return;
        }

        try
        {
            await _screenSharePlatformService.StartForegroundSessionAsync("NDI-Android", cancellationToken);
            await _bridge.StartOutputAsync(SourceId, "NDI-Android", cancellationToken);
            IsOutputActive = true;
            StatusMessage = "Output active";
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
    }
}
