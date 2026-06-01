using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiForAndroid.NdiBridge;

namespace NdiForAndroid.Features.Viewer.ViewModels;

public partial class ViewerViewModel : ObservableObject
{
    private readonly INdiViewerBridge _bridge;

    [ObservableProperty]
    private string? _sourceId;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private string? _statusMessage;

    public ViewerViewModel(INdiViewerBridge bridge)
    {
        _bridge = bridge;
    }

    partial void OnSourceIdChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value))
            StartCommand.Execute(null);
    }

    [RelayCommand]
    private void Start()
    {
        if (string.IsNullOrEmpty(SourceId)) return;
        _bridge.StartReceiver(SourceId);
        IsPlaying = true;
        StatusMessage = "Connecting…";
    }

    [RelayCommand]
    private void Stop()
    {
        _bridge.StopReceiver();
        IsPlaying = false;
        StatusMessage = null;
    }
}
