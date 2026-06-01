using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiForAndroid.NdiBridge;

namespace NdiForAndroid.Features.Output.ViewModels;

public partial class OutputViewModel : ObservableObject
{
    private readonly INdiOutputBridge _bridge;

    [ObservableProperty]
    private string? _sourceId;

    [ObservableProperty]
    private bool _isOutputActive;

    [ObservableProperty]
    private string? _statusMessage;

    public OutputViewModel(INdiOutputBridge bridge)
    {
        _bridge = bridge;
    }

    [RelayCommand]
    private async Task StartOutputAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(SourceId)) return;
        await _bridge.StartOutputAsync(SourceId, "NDI-Android", cancellationToken);
        IsOutputActive = true;
        StatusMessage = "Output active";
    }

    [RelayCommand]
    private async Task StopOutputAsync(CancellationToken cancellationToken)
    {
        await _bridge.StopOutputAsync(cancellationToken);
        IsOutputActive = false;
        StatusMessage = null;
    }
}
